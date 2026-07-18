using System.Runtime.CompilerServices;

namespace Celerity.Ring;

/// <summary>
/// A consistent-hashing ring that maps keys to nodes so that adding or removing a node only remaps the keys
/// on the affected arcs of the ring — parameterized on the caller's key type and a zero-cost inlined
/// <see cref="IHashProvider{T}"/>. It fills a gap the BCL has no type for, and (given a deterministic key
/// hasher) produces <strong>byte-identical</strong> key→node assignments on every process, runtime, and CPU
/// architecture — the property a sharded fleet needs so every member agrees on where a key lives.
/// </summary>
/// <remarks>
/// <para>
/// Each physical node is placed on a <c>[0, 2^32)</c> ring at many <em>virtual node</em> positions (the
/// classic Karger et&#160;al. / ketama technique), so load spreads evenly and a node's departure hands its
/// keys to many successors rather than dumping them all on one. A key is routed to the first virtual node
/// at or clockwise of the key's hash. With <c>V</c> virtual nodes per node, adding or removing a node moves
/// only about <c>1 / NodeCount</c> of all keys — versus the near-total reshuffle a <c>hash % nodeCount</c>
/// scheme causes on any membership change.
/// </para>
/// <para>
/// <b>Determinism is a correctness feature, not a nicety.</b> A ring built from
/// <c>string.GetHashCode()</c> assigns keys differently in every process (that hash is randomized per run),
/// so two nodes in the same cluster silently disagree about ownership. This ring hashes keys through the
/// supplied <typeparamref name="THasher"/> and places virtual nodes with a fixed, specified Celerity string
/// hash (<see cref="StringXxHash3Hasher"/>) mixed by pure integer arithmetic — no randomized hash, no
/// endianness-dependent step — so as long as every member supplies the same node set, weights, virtual-node
/// count, and (deterministic) key hasher, they all build the identical ring and route every key the same
/// way. Prefer a specified hasher such as <see cref="StringXxHash3Hasher"/>, <see cref="GuidHasher"/>, or an
/// integer hasher; do <strong>not</strong> use <see cref="DefaultHasher{T}"/> for <see cref="string"/> keys
/// if you need cross-process agreement, because it delegates to the per-run-randomized BCL hash.
/// </para>
/// <para>
/// Reads (<see cref="GetNode"/> / <see cref="TryGetNode"/> / <see cref="GetReplicas"/>) are lock-free: each
/// mutation publishes a fresh immutable snapshot with a single volatile write, and a reader takes one
/// consistent snapshot for the duration of the call. Reads are therefore safe to run concurrently with each
/// other and with a mutation (a reader sees either the old or the new topology, never a torn one).
/// Mutations (<see cref="Add"/> / <see cref="Remove"/>) rebuild the snapshot and must be serialized by the
/// caller — they are not safe to run concurrently with one another.
/// </para>
/// </remarks>
/// <typeparam name="TNode">The node payload returned by a lookup (an endpoint, a connection, an id, …).</typeparam>
/// <typeparam name="TKey">The key type routed to a node.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to place a key on the ring. Must be a value type implementing <see cref="IHashProvider{T}"/>
/// so the JIT can devirtualize and inline it on the routing hot path. Use a deterministic hasher for
/// cross-process agreement.
/// </typeparam>
public class ConsistentHashRing<TNode, TKey, THasher>
    where THasher : struct, IHashProvider<TKey>
{
    /// <summary>
    /// The default number of virtual nodes created per unit of node weight when a constructor does not specify
    /// one: 160. Higher values smooth load distribution at the cost of a larger ring and slower rebuilds.
    /// </summary>
    public const int DefaultVirtualNodesPerNode = 160;

    // Hard ceiling on the total virtual-node count across all nodes, guarding the rebuild against an
    // accidental (weight * virtualNodesPerNode * nodeCount) explosion that would exhaust memory.
    private const long MaxTotalVirtualNodes = 1L << 27; // ~134 million

    private readonly int _virtualNodesPerNode;
    private readonly THasher _keyHasher;
    private readonly StringXxHash3Hasher _nodeHasher;
    private readonly Dictionary<string, Registration> _registry;

    private volatile Snapshot _snapshot;

    /// <summary>
    /// Initializes a new, empty <see cref="ConsistentHashRing{TNode, TKey, THasher}"/>.
    /// </summary>
    /// <param name="virtualNodesPerNode">
    /// The number of virtual nodes to create per unit of node weight. Must be at least 1. Larger values even
    /// out the key distribution across nodes at the cost of a bigger ring and slower <see cref="Add"/> /
    /// <see cref="Remove"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="virtualNodesPerNode"/> is less than 1.</exception>
    public ConsistentHashRing(int virtualNodesPerNode = DefaultVirtualNodesPerNode)
    {
        if (virtualNodesPerNode < 1)
            throw new ArgumentOutOfRangeException(nameof(virtualNodesPerNode), virtualNodesPerNode, "Virtual node count must be at least 1.");

        _virtualNodesPerNode = virtualNodesPerNode;
        _keyHasher = default;
        _nodeHasher = default;
        _registry = new Dictionary<string, Registration>(StringComparer.Ordinal);
        _snapshot = Snapshot.Empty;
    }

    /// <summary>Gets the number of physical nodes currently on the ring.</summary>
    public int NodeCount => _registry.Count;

    /// <summary>Gets the total number of virtual nodes across all physical nodes (the size of the ring array).</summary>
    public int VirtualNodeCount => _snapshot.Positions.Length;

    /// <summary>Gets the number of virtual nodes created per unit of node weight (the constructor argument).</summary>
    public int VirtualNodesPerNode => _virtualNodesPerNode;

    /// <summary>
    /// Adds a node to the ring under a stable identity string and rebuilds the routing snapshot.
    /// </summary>
    /// <param name="nodeId">
    /// The node's stable identity, used to place its virtual nodes. Two rings that must agree on routing have
    /// to use the same identity string for the same node. Must be non-<c>null</c>.
    /// </param>
    /// <param name="node">The payload returned when a key routes to this node.</param>
    /// <param name="weight">
    /// The node's relative capacity. A node of weight <c>w</c> receives <c>w × VirtualNodesPerNode</c> virtual
    /// nodes, so it attracts roughly <c>w</c>× the keys of a weight-1 node. Must be at least 1.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="nodeId"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">A node with the same <paramref name="nodeId"/> is already on the ring.</exception>
    /// <exception cref="InvalidOperationException">
    /// The resulting ring would exceed the maximum total virtual-node count; lower <paramref name="weight"/> or
    /// the constructor's virtual-node count.
    /// </exception>
    public void Add(string nodeId, TNode node, int weight = 1)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        if (weight < 1)
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weight must be at least 1.");
        if (_registry.ContainsKey(nodeId))
            throw new ArgumentException($"A node with id '{nodeId}' is already on the ring.", nameof(nodeId));

        // Keep Add atomic: if Rebuild rejects the resulting topology (e.g. the total virtual-node guard), roll
        // the registration back so a failed Add leaves the registry and the routing snapshot consistent rather
        // than stranding a node that Contains/NodeCount report but routing never sees.
        _registry.Add(nodeId, new Registration(node, weight));
        try
        {
            Rebuild();
        }
        catch
        {
            _registry.Remove(nodeId);
            throw;
        }
    }

    /// <summary>
    /// Removes a node from the ring by its identity and rebuilds the routing snapshot.
    /// </summary>
    /// <param name="nodeId">The identity the node was added under.</param>
    /// <returns><c>true</c> if a node was found and removed; <c>false</c> if no node had that identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="nodeId"/> is <c>null</c>.</exception>
    public bool Remove(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        if (!_registry.Remove(nodeId))
            return false;

        Rebuild();
        return true;
    }

    /// <summary>Determines whether a node with the specified identity is on the ring.</summary>
    /// <param name="nodeId">The identity to test.</param>
    /// <returns><c>true</c> if the ring contains that node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="nodeId"/> is <c>null</c>.</exception>
    public bool Contains(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        return _registry.ContainsKey(nodeId);
    }

    /// <summary>
    /// Routes a key to its owning node — the node whose first virtual node lies at or clockwise of the key's
    /// hash.
    /// </summary>
    /// <param name="key">The key to route.</param>
    /// <returns>The owning node.</returns>
    /// <exception cref="InvalidOperationException">The ring is empty (no nodes have been added).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TNode GetNode(TKey key)
    {
        Snapshot snapshot = _snapshot;
        uint[] positions = snapshot.Positions;
        if (positions.Length == 0)
            throw new InvalidOperationException("The ring is empty; add at least one node before routing keys.");

        int slot = OwnerSlot(positions, (uint)_keyHasher.Hash(key));
        return snapshot.Nodes[snapshot.OwnerIndex[slot]];
    }

    /// <summary>
    /// Attempts to route a key to its owning node, returning <c>false</c> instead of throwing when the ring is
    /// empty.
    /// </summary>
    /// <param name="key">The key to route.</param>
    /// <param name="node">
    /// When this method returns <c>true</c>, the owning node; otherwise the default value of
    /// <typeparamref name="TNode"/>.
    /// </param>
    /// <returns><c>true</c> if the key was routed; <c>false</c> if the ring is empty.</returns>
    public bool TryGetNode(TKey key, out TNode node)
    {
        Snapshot snapshot = _snapshot;
        uint[] positions = snapshot.Positions;
        if (positions.Length == 0)
        {
            node = default!;
            return false;
        }

        int slot = OwnerSlot(positions, (uint)_keyHasher.Hash(key));
        node = snapshot.Nodes[snapshot.OwnerIndex[slot]];
        return true;
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> <em>distinct</em> nodes for a key, walking clockwise from the
    /// key's owner — the natural replica set for a key (primary first, then successors).
    /// </summary>
    /// <param name="key">The key to route.</param>
    /// <param name="count">The maximum number of distinct nodes to return. <c>0</c> returns an empty list.</param>
    /// <returns>
    /// The distinct owning nodes, primary first. Fewer than <paramref name="count"/> are returned when the ring
    /// has fewer nodes; an empty list when the ring is empty.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public IReadOnlyList<TNode> GetReplicas(TKey key, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Replica count must be non-negative.");

        Snapshot snapshot = _snapshot;
        uint[] positions = snapshot.Positions;
        int nodeCount = snapshot.Nodes.Length;
        int take = Math.Min(count, nodeCount);
        if (take == 0)
            return Array.Empty<TNode>();

        var result = new List<TNode>(take);
        var seen = new bool[nodeCount];
        int start = OwnerSlot(positions, (uint)_keyHasher.Hash(key));

        for (int step = 0; step < positions.Length && result.Count < take; step++)
        {
            int slot = start + step;
            if (slot >= positions.Length)
                slot -= positions.Length;

            int ownerIndex = snapshot.OwnerIndex[slot];
            if (!seen[ownerIndex])
            {
                seen[ownerIndex] = true;
                result.Add(snapshot.Nodes[ownerIndex]);
            }
        }

        return result;
    }

    // Finds the ring slot that owns a key hash: the first virtual node at or clockwise of `keyPosition`,
    // wrapping to slot 0 when the key hashes past the last virtual node. A lower-bound binary search over the
    // sorted position array.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int OwnerSlot(uint[] positions, uint keyPosition)
    {
        int lo = 0;
        int hi = positions.Length;
        while (lo < hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (positions[mid] < keyPosition)
                lo = mid + 1;
            else
                hi = mid;
        }

        // lo is in [0, length]; length means the key hashed past every position, so it wraps to the first.
        return lo == positions.Length ? 0 : lo;
    }

    // Rebuilds the immutable routing snapshot from the current node registry and publishes it. Node identities
    // are processed in ordinal order and virtual nodes are sorted by (position, ownerIndex), so the produced
    // arrays are a pure function of the (nodeId, weight) set and the configuration — identical on every process.
    private void Rebuild()
    {
        int nodeCount = _registry.Count;
        if (nodeCount == 0)
        {
            _snapshot = Snapshot.Empty;
            return;
        }

        // Deterministic node order: sort identities ordinally and index nodes in that order.
        var nodeIds = new string[nodeCount];
        _registry.Keys.CopyTo(nodeIds, 0);
        Array.Sort(nodeIds, StringComparer.Ordinal);

        var nodes = new TNode[nodeCount];
        var nodeHashes = new uint[nodeCount];
        long totalVirtual = 0;
        for (int i = 0; i < nodeCount; i++)
        {
            Registration registration = _registry[nodeIds[i]];
            nodes[i] = registration.Node;
            nodeHashes[i] = (uint)_nodeHasher.Hash(nodeIds[i]);
            totalVirtual += (long)registration.Weight * _virtualNodesPerNode;
        }

        if (totalVirtual > MaxTotalVirtualNodes)
            throw new InvalidOperationException(
                $"The ring would need {totalVirtual} virtual nodes, exceeding the maximum of {MaxTotalVirtualNodes}. Reduce weights or the virtual-node count.");

        // Pack each virtual node as (position << 32 | ownerIndex) so a single sort orders by position and then
        // by owner index, giving a stable, deterministic layout even when two virtual nodes collide on a
        // position.
        var packed = new ulong[(int)totalVirtual];
        int cursor = 0;
        for (int i = 0; i < nodeCount; i++)
        {
            int virtualCount = _registry[nodeIds[i]].Weight * _virtualNodesPerNode;
            uint nodeHash = nodeHashes[i];
            for (int r = 0; r < virtualCount; r++)
            {
                uint position = RingHash.VirtualNodePosition(nodeHash, r);
                packed[cursor++] = (((ulong)position) << 32) | (uint)i;
            }
        }

        Array.Sort(packed);

        var positions = new uint[packed.Length];
        var ownerIndex = new int[packed.Length];
        for (int i = 0; i < packed.Length; i++)
        {
            positions[i] = (uint)(packed[i] >> 32);
            ownerIndex[i] = (int)(uint)packed[i];
        }

        _snapshot = new Snapshot(positions, ownerIndex, nodes);
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

    // An immutable routing table. Positions[i] is the ring position of virtual node i (sorted ascending),
    // OwnerIndex[i] the index into Nodes of the physical node owning it. Published atomically via the volatile
    // _snapshot field so readers never observe a partially built table.
    private sealed class Snapshot
    {
        internal static readonly Snapshot Empty =
            new Snapshot(Array.Empty<uint>(), Array.Empty<int>(), Array.Empty<TNode>());

        internal Snapshot(uint[] positions, int[] ownerIndex, TNode[] nodes)
        {
            Positions = positions;
            OwnerIndex = ownerIndex;
            Nodes = nodes;
        }

        internal uint[] Positions { get; }

        internal int[] OwnerIndex { get; }

        internal TNode[] Nodes { get; }
    }
}

/// <summary>
/// A <see cref="ConsistentHashRing{TNode, TKey, THasher}"/> specialized for <see cref="string"/> keys with the
/// deterministic, cross-runtime <see cref="StringXxHash3Hasher"/> — the common case, so callers routing string
/// keys avoid spelling out the three type arguments.
/// </summary>
/// <typeparam name="TNode">The node payload returned by a lookup.</typeparam>
public sealed class StringConsistentHashRing<TNode> : ConsistentHashRing<TNode, string, StringXxHash3Hasher>
{
    /// <summary>
    /// Initializes a new, empty <see cref="StringConsistentHashRing{TNode}"/>.
    /// </summary>
    /// <param name="virtualNodesPerNode">The number of virtual nodes per unit weight; see the base constructor.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="virtualNodesPerNode"/> is less than 1.</exception>
    public StringConsistentHashRing(int virtualNodesPerNode = DefaultVirtualNodesPerNode)
        : base(virtualNodesPerNode)
    {
    }
}
