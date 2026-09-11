using System.Buffers;
using System.Runtime.CompilerServices;

namespace Celerity.Ring;

/// <summary>
/// A rendezvous (highest-random-weight, "HRW") hash that maps keys to nodes by scoring every node against the
/// key and picking the highest — parameterized on the caller's key type and a zero-cost inlined
/// <see cref="IHashProvider{T}"/>. Like <see cref="ConsistentHashRing{TNode, TKey, THasher}"/> it moves only a
/// key's share of traffic when a node joins or leaves, but it keeps <strong>no ring array</strong>: there is
/// nothing to rebuild on a membership change, which suits small, churning clusters. Scoring is pure integer
/// arithmetic, so (given a deterministic key hasher) the key→node mapping is byte-identical on every process,
/// runtime, and architecture.
/// </summary>
/// <remarks>
/// <para>
/// For a key, each node's score is a 64-bit avalanche of its identity hash combined with the key's hash
/// (Thaler &amp; Ravishankar, 1998); the node with the maximum score owns the key. Because the score depends
/// only on the (node, key) pair, removing a node re-routes exactly its keys — each to its next-highest scorer
/// — and no others, with no state to migrate. A lookup is <c>O(NodeCount)</c> (scaled by total weight; see
/// below), which is why HRW shines for modest clusters where the ring's rebuild cost is not worth paying.
/// </para>
/// <para>
/// A node's <em>weight</em> is realized by giving it that many integer sub-labels and scoring it by its best
/// sub-label, so a weight-<c>w</c> node attracts about <c>w</c>× the keys of a weight-1 node — all through
/// integer <c>max</c>, with no floating-point <c>log</c> whose last-bit rounding could differ across
/// platforms and silently break agreement. The cost of a lookup therefore scales with the <em>total</em>
/// weight of the cluster, not just the node count.
/// </para>
/// <para>
/// The same determinism caveat as the ring applies: hash keys with a specified deterministic
/// <typeparamref name="THasher"/> (<see cref="StringXxHash3Hasher"/>, <see cref="GuidHasher"/>, an integer
/// hasher, …), not <see cref="DefaultHasher{T}"/> over <see cref="string"/>, if cross-process agreement
/// matters. Routing (<see cref="GetNode"/> / <see cref="TryGetNode"/> / <see cref="GetReplicas(TKey, int)"/>
/// / <see cref="GetReplicas(TKey, Span{TNode})"/>) is lock-free over an immutable snapshot, as is <see cref="NodeCount"/>, which reads the same snapshot;
/// mutations must be serialized by the caller.
/// </para>
/// <para>
/// <see cref="Contains"/> is the one exception: it queries the node registry, an ordinary
/// <see cref="Dictionary{TKey, TValue}"/> that a mutation writes in place, so it is <strong>not</strong> safe
/// to call while another thread is inside <see cref="Add"/> or <see cref="Remove"/>. Serialize it with the
/// mutations; the snapshot carries node payloads but not the identities they were added under, so there is no
/// lock-free membership test to reach for instead.
/// </para>
/// </remarks>
/// <typeparam name="TNode">The node payload returned by a lookup.</typeparam>
/// <typeparam name="TKey">The key type routed to a node.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to hash a key. Must be a value type implementing <see cref="IHashProvider{T}"/> so the JIT
/// can devirtualize and inline it. Use a deterministic hasher for cross-process agreement.
/// </typeparam>
public class RendezvousHash<TNode, TKey, THasher>
    where THasher : struct, IHashProvider<TKey>
{
    // Replica ranking keeps one 16-byte candidate per requested replica. Up to this many (512 bytes) they live
    // on the stack; a larger request rents them from the shared array pool.
    private const int StackRankedCandidates = 32;

    private readonly THasher _keyHasher;
    private readonly StringXxHash3Hasher _nodeHasher;
    private readonly Dictionary<string, Registration> _registry;

    private volatile Snapshot _snapshot;

    /// <summary>Initializes a new, empty <see cref="RendezvousHash{TNode, TKey, THasher}"/>.</summary>
    public RendezvousHash()
    {
        _keyHasher = default;
        _nodeHasher = default;
        _registry = new Dictionary<string, Registration>(StringComparer.Ordinal);
        _snapshot = Snapshot.Empty;
    }

    /// <summary>Gets the number of nodes currently in the pool.</summary>
    /// <remarks>
    /// Read from the published snapshot rather than the registry, so it is lock-free and always agrees with what
    /// routing sees. A rebuild replaces the whole snapshot, so this equals the registry count outside a mutation.
    /// </remarks>
    public int NodeCount => _snapshot.Nodes.Length;

    /// <summary>
    /// Adds a node to the pool under a stable identity string.
    /// </summary>
    /// <param name="nodeId">
    /// The node's stable identity, hashed to score it. Pools that must agree on routing have to use the same
    /// identity string for the same node. Must be non-<c>null</c>.
    /// </param>
    /// <param name="node">The payload returned when a key routes to this node.</param>
    /// <param name="weight">
    /// The node's relative capacity, realized as that many integer sub-labels; a node of weight <c>w</c>
    /// attracts roughly <c>w</c>× the keys of a weight-1 node and costs <c>w</c>× as much to score. Must be at
    /// least 1.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="nodeId"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">A node with the same <paramref name="nodeId"/> is already in the pool.</exception>
    public void Add(string nodeId, TNode node, int weight = 1)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        if (weight < 1)
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weight must be at least 1.");
        if (_registry.ContainsKey(nodeId))
            throw new ArgumentException($"A node with id '{nodeId}' is already in the pool.", nameof(nodeId));

        _registry.Add(nodeId, new Registration(node, weight));
        Rebuild();
    }

    /// <summary>Removes a node from the pool by its identity.</summary>
    /// <param name="nodeId">The identity the node was added under.</param>
    /// <returns><c>true</c> if a node was found and removed; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="nodeId"/> is <c>null</c>.</exception>
    public bool Remove(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        if (!_registry.Remove(nodeId))
            return false;

        Rebuild();
        return true;
    }

    /// <summary>Determines whether a node with the specified identity is in the pool.</summary>
    /// <param name="nodeId">The identity to test.</param>
    /// <returns><c>true</c> if the pool contains that node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="nodeId"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Unlike routing, this reads the node registry rather than the published snapshot, so it must not run
    /// concurrently with <see cref="Add"/> or <see cref="Remove"/>.
    /// </remarks>
    public bool Contains(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        return _registry.ContainsKey(nodeId);
    }

    /// <summary>Routes a key to the node that scores highest against it.</summary>
    /// <param name="key">The key to route.</param>
    /// <returns>The owning node.</returns>
    /// <exception cref="InvalidOperationException">The pool is empty (no nodes have been added).</exception>
    public TNode GetNode(TKey key)
    {
        Snapshot snapshot = _snapshot;
        if (snapshot.Nodes.Length == 0)
            throw new InvalidOperationException("The pool is empty; add at least one node before routing keys.");

        int best = BestNode(snapshot, (uint)_keyHasher.Hash(key));
        return snapshot.Nodes[best];
    }

    /// <summary>Attempts to route a key, returning <c>false</c> instead of throwing when the pool is empty.</summary>
    /// <param name="key">The key to route.</param>
    /// <param name="node">
    /// When this method returns <c>true</c>, the owning node; otherwise the default value of
    /// <typeparamref name="TNode"/>.
    /// </param>
    /// <returns><c>true</c> if the key was routed; <c>false</c> if the pool is empty.</returns>
    public bool TryGetNode(TKey key, out TNode node)
    {
        Snapshot snapshot = _snapshot;
        if (snapshot.Nodes.Length == 0)
        {
            node = default!;
            return false;
        }

        node = snapshot.Nodes[BestNode(snapshot, (uint)_keyHasher.Hash(key))];
        return true;
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> nodes for a key in descending score order — the natural replica
    /// set under HRW (each key's ranked preference list).
    /// </summary>
    /// <param name="key">The key to route.</param>
    /// <param name="count">The maximum number of nodes to return. <c>0</c> returns an empty list.</param>
    /// <returns>The highest-scoring nodes for the key, best first; fewer than <paramref name="count"/> when the pool is smaller.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <remarks>
    /// Allocates the returned array, plus — for more than 32 replicas — candidate scratch rented from
    /// <see cref="ArrayPool{T}.Shared"/>, which allocates only when the pool has no buffer to lend. On a
    /// per-request path, prefer <see cref="GetReplicas(TKey, Span{TNode})"/>, which writes the same nodes into a
    /// caller-supplied buffer and skips the result array.
    /// </remarks>
    public IReadOnlyList<TNode> GetReplicas(TKey key, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Replica count must be non-negative.");

        Snapshot snapshot = _snapshot;
        int take = Math.Min(count, snapshot.Nodes.Length);
        if (take == 0)
            return Array.Empty<TNode>();

        var replicas = new TNode[take];
        RankReplicas(snapshot, (uint)_keyHasher.Hash(key), replicas);
        return replicas;
    }

    /// <summary>
    /// Writes the highest-scoring nodes for a key into <paramref name="destination"/> in descending score order —
    /// the allocation-free form of <see cref="GetReplicas(TKey, int)"/>, with the buffer's length as the replica
    /// count.
    /// </summary>
    /// <param name="key">The key to route.</param>
    /// <param name="destination">
    /// The buffer to fill, best first. Its length is the number of nodes asked for; an empty buffer asks for none.
    /// </param>
    /// <returns>
    /// The number of nodes written: <c>destination.Length</c>, or <see cref="NodeCount"/> when the pool is
    /// smaller; <c>0</c> when the pool is empty. Elements past the returned count are left unchanged.
    /// </returns>
    /// <remarks>
    /// Writes exactly the nodes <see cref="GetReplicas(TKey, int)"/> returns for the same count, in the same
    /// order, read from one consistent snapshot. Every node is scored once, but only the requested replicas are
    /// ranked, so asking for a few nodes of a large pool costs little more than <see cref="GetNode"/>. Nothing is
    /// allocated for up to 32 replicas, whose candidates live on the stack; a larger buffer rents them from
    /// <see cref="ArrayPool{T}.Shared"/>.
    /// </remarks>
    public int GetReplicas(TKey key, Span<TNode> destination)
    {
        Snapshot snapshot = _snapshot;
        int take = Math.Min(destination.Length, snapshot.Nodes.Length);
        if (take == 0)
            return 0;

        RankReplicas(snapshot, (uint)_keyHasher.Hash(key), destination.Slice(0, take));
        return take;
    }

    // Fills `replicas` with the replicas.Length highest-ranked nodes, best first. The caller guarantees
    // 1 <= replicas.Length <= NodeCount. Each node is scored once and offered to a bounded min-heap holding the
    // best candidates so far with the weakest at the root, so selecting k of n costs O(n log k) rather than the
    // O(n log n) of sorting every node.
    private static void RankReplicas(Snapshot snapshot, uint keyHash, Span<TNode> replicas)
    {
        TNode[] nodes = snapshot.Nodes;
        int take = replicas.Length;
        if (take == 1)
        {
            replicas[0] = nodes[BestNode(snapshot, keyHash)];
            return;
        }

        UInt128[]? rented = null;
        Span<UInt128> heap = take <= StackRankedCandidates
            ? stackalloc UInt128[StackRankedCandidates]
            : (rented = ArrayPool<UInt128>.Shared.Rent(take));
        heap = heap.Slice(0, take);

        try
        {
            for (int i = 0; i < take; i++)
                heap[i] = RankKey(NodeScore(snapshot, i, keyHash), i);
            for (int i = (take >> 1) - 1; i >= 0; i--)
                SiftDown(heap, take, i);

            for (int i = take; i < nodes.Length; i++)
            {
                UInt128 candidate = RankKey(NodeScore(snapshot, i, keyHash), i);
                if (candidate > heap[0])
                {
                    heap[0] = candidate;
                    SiftDown(heap, take, 0);
                }
            }

            // Pop the weakest remaining candidate into the last unfilled slot until the heap is empty, which
            // leaves `replicas` best first.
            for (int size = take - 1; size >= 0; size--)
            {
                replicas[size] = nodes[NodeIndexOf(heap[0])];
                heap[0] = heap[size];
                SiftDown(heap, size, 0);
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<UInt128>.Shared.Return(rented);
        }
    }

    // Packs a candidate into one value whose unsigned order is its rank: the score in the high 64 bits, and the
    // node index complemented in the low 32, so on equal scores the lower index (ordinal identity order) ranks
    // higher — the same tie-break BestNode applies. Indices are distinct, so no two keys are ever equal.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt128 RankKey(ulong score, int nodeIndex) => ((UInt128)score << 32) | (uint)~nodeIndex;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NodeIndexOf(UInt128 rankKey) => ~(int)(uint)rankKey;

    // Restores the min-heap order below `parent` within heap[0..count), keeping the weakest candidate at the root.
    private static void SiftDown(Span<UInt128> heap, int count, int parent)
    {
        UInt128 item = heap[parent];
        while (true)
        {
            int child = (parent << 1) + 1;
            if (child >= count)
                break;
            if (child + 1 < count && heap[child + 1] < heap[child])
                child++;
            if (item < heap[child])
                break;

            heap[parent] = heap[child];
            parent = child;
        }

        heap[parent] = item;
    }

    // Returns the index of the highest-scoring node for a key hash. Iterating in ascending node-index order and
    // updating only on a strictly greater score makes the lowest-index node win any (astronomically unlikely)
    // score tie, so the result is deterministic.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BestNode(Snapshot snapshot, uint keyHash)
    {
        int best = 0;
        ulong bestScore = NodeScore(snapshot, 0, keyHash);
        int nodeCount = snapshot.Nodes.Length;
        for (int i = 1; i < nodeCount; i++)
        {
            ulong score = NodeScore(snapshot, i, keyHash);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }

    // A node's score for a key: the maximum avalanche over its weight sub-labels. More sub-labels raise the
    // expected maximum, which is how integer weighting attracts proportionally more keys without any float.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong NodeScore(Snapshot snapshot, int nodeIndex, uint keyHash)
    {
        uint nodeHash = snapshot.NodeHashes[nodeIndex];
        int weight = snapshot.Weights[nodeIndex];

        ulong best = RingHash.Mix64(nodeHash, keyHash);
        for (int r = 1; r < weight; r++)
        {
            ulong score = RingHash.Mix64(RingHash.VirtualNodePosition(nodeHash, r), keyHash);
            if (score > best)
                best = score;
        }

        return best;
    }

    // Rebuilds the immutable snapshot from the registry with nodes in ordinal identity order, so the arrays are
    // a pure function of the (nodeId, weight) set — identical on every process.
    private void Rebuild()
    {
        int nodeCount = _registry.Count;
        if (nodeCount == 0)
        {
            _snapshot = Snapshot.Empty;
            return;
        }

        var nodeIds = new string[nodeCount];
        _registry.Keys.CopyTo(nodeIds, 0);
        Array.Sort(nodeIds, StringComparer.Ordinal);

        var nodes = new TNode[nodeCount];
        var nodeHashes = new uint[nodeCount];
        var weights = new int[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            Registration registration = _registry[nodeIds[i]];
            nodes[i] = registration.Node;
            nodeHashes[i] = (uint)_nodeHasher.Hash(nodeIds[i]);
            weights[i] = registration.Weight;
        }

        _snapshot = new Snapshot(nodes, nodeHashes, weights);
    }

    private sealed class Registration
    {
        internal Registration(TNode node, int weight)
        {
            Node = node;
            Weight = weight;
        }

        internal TNode Node { get; }

        internal int Weight { get; }
    }

    private sealed class Snapshot
    {
        internal static readonly Snapshot Empty =
            new Snapshot(Array.Empty<TNode>(), Array.Empty<uint>(), Array.Empty<int>());

        internal Snapshot(TNode[] nodes, uint[] nodeHashes, int[] weights)
        {
            Nodes = nodes;
            NodeHashes = nodeHashes;
            Weights = weights;
        }

        internal TNode[] Nodes { get; }

        internal uint[] NodeHashes { get; }

        internal int[] Weights { get; }
    }
}

/// <summary>
/// A <see cref="RendezvousHash{TNode, TKey, THasher}"/> specialized for <see cref="string"/> keys with the
/// deterministic, cross-runtime <see cref="StringXxHash3Hasher"/> — the common case, so callers routing string
/// keys avoid spelling out the three type arguments.
/// </summary>
/// <typeparam name="TNode">The node payload returned by a lookup.</typeparam>
public sealed class StringRendezvousHash<TNode> : RendezvousHash<TNode, string, StringXxHash3Hasher>
{
    /// <summary>Initializes a new, empty <see cref="StringRendezvousHash{TNode}"/>.</summary>
    public StringRendezvousHash()
    {
    }
}
