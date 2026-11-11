using System.Buffers;
using System.Runtime.CompilerServices;

namespace Celerity.Ring;

/// <summary>
/// A Maglev consistent hash (Eisenbud et&#160;al., NSDI 2016): routes a key to a node with one hash and one
/// array load, with near-perfect load balance and close-to-minimal disruption when a node joins or leaves —
/// parameterized on the caller's key type and a zero-cost inlined <see cref="IHashProvider{T}"/>. Like the other
/// types in this package it fills a gap the BCL has no type for, and (given a deterministic key hasher) produces
/// <strong>byte-identical</strong> key→node assignments on every process, runtime, and CPU architecture.
/// </summary>
/// <remarks>
/// <para>
/// <b>How it routes.</b> The type keeps a lookup table of <see cref="TableSize"/> slots, a prime. Each node walks
/// its own permutation of the slots, derived from a deterministic hash of its identity, and the nodes take turns
/// claiming the next free slot of their permutation until the table is full. A key is routed to the node in slot
/// <c>hash(key) mod TableSize</c>: a modulo and an array load, where <see cref="ConsistentHashRing{TNode, TKey, THasher}"/>
/// binary-searches its sorted virtual nodes and <see cref="RendezvousHash{TNode, TKey, THasher}"/> scores every
/// node.
/// </para>
/// <para>
/// <b>Balance.</b> The nodes take turns, a node of weight <c>w</c> claiming <c>w</c> slots per round, so it owns
/// between <c>w·⌊M / W⌋</c> and <c>w·(⌊M / W⌋ + 1)</c> slots (<c>M</c> the table size, <c>W</c> the total weight):
/// within one round of its fair share, and within one slot when every weight is 1. The last, partial round goes
/// to the nodes whose identities sort first. A ring with the default 160 virtual nodes per node leaves its most
/// loaded node several percent over the mean.
/// </para>
/// <para>
/// <b>Disruption.</b> When a node leaves, every key it owned moves, as on any consistent hash; Maglev also moves a
/// small fraction of the other keys, because the table is refilled and the remaining nodes' turns land
/// differently. Keep the table at least 100 times the node count (the paper's guidance) to keep that extra small.
/// When you need strictly minimal movement, use <see cref="ConsistentHashRing{TNode, TKey, THasher}"/> or
/// <see cref="RendezvousHash{TNode, TKey, THasher}"/>.
/// </para>
/// <para>
/// <b>Cost of a change.</b> <see cref="Add"/> and <see cref="Remove"/> rebuild the whole table, which costs
/// <c>O(M log M)</c> time and allocates the <c>4M</c>-byte table. That suits a fleet whose membership changes far
/// less often than it routes.
/// </para>
/// <para>
/// <b>Replicas.</b> <see cref="GetReplicas(TKey, int)"/> returns the key's owner followed by the next distinct
/// nodes met walking forward through the table. Because slots are interleaved, the backups are well spread, but
/// only the primary carries Maglev's balance and disruption guarantees.
/// </para>
/// <para>
/// <b>Determinism.</b> Node identities are hashed with <see cref="StringXxHash3Hasher"/> and processed in ordinal
/// order, and the fill is pure integer arithmetic, so every member that supplies the same node set, weights,
/// table size, and (deterministic) key hasher builds the identical table. Do <strong>not</strong> use
/// <see cref="DefaultHasher{T}"/> for <see cref="string"/> keys if you need cross-process agreement, because it
/// delegates to the per-run-randomized BCL hash.
/// </para>
/// <para>
/// <b>Concurrency.</b> Routing (<see cref="GetNode"/> / <see cref="TryGetNode"/> / the two
/// <c>GetReplicas</c> overloads), <see cref="NodeCount"/> and <see cref="TableSize"/> are lock-free: each
/// mutation publishes a fresh immutable snapshot with a single volatile write. <see cref="Add"/>,
/// <see cref="Remove"/> and <see cref="Contains"/> touch the node registry and must be serialized by the caller.
/// </para>
/// </remarks>
/// <typeparam name="TNode">The node payload returned by a lookup (an endpoint, a connection, an id, …).</typeparam>
/// <typeparam name="TKey">The key type routed to a node.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to place a key in the table. Must be a value type implementing <see cref="IHashProvider{T}"/>
/// so the JIT can devirtualize and inline it on the routing hot path. Use a deterministic hasher for
/// cross-process agreement.
/// </typeparam>
public class MaglevHash<TNode, TKey, THasher>
    where THasher : struct, IHashProvider<TKey>
{
    /// <summary>
    /// The default lookup-table size when a constructor does not specify one: 65,537, a prime. It keeps the extra
    /// disruption small up to about 650 nodes of weight 1 (100 slots per node); use a larger prime for a larger
    /// fleet.
    /// </summary>
    public const int DefaultTableSize = 65_537;

    /// <summary>
    /// The largest accepted lookup-table size: 16,777,213, the largest prime below 2^24. The table then takes
    /// 64 MB.
    /// </summary>
    public const int MaxTableSize = 16_777_213;

    // The replica walk's seen-set is one bit per physical node. Up to this many 64-bit words (1,024 nodes,
    // 128 bytes) it lives on the stack; a larger table rents it from the shared array pool.
    private const int StackSeenWords = 16;

    private readonly int _tableSize;
    private readonly THasher _keyHasher;
    private readonly StringXxHash3Hasher _nodeHasher;
    private readonly Dictionary<string, Registration> _registry;
    private long _totalWeight;

    private volatile Snapshot _snapshot;

    /// <summary>
    /// Initializes a new, empty <see cref="MaglevHash{TNode, TKey, THasher}"/>.
    /// </summary>
    /// <param name="tableSize">
    /// The number of lookup-table slots. Must be a prime between 2 and <see cref="MaxTableSize"/>, and at least
    /// the total weight of the nodes added later. Aim for 100 or more slots per node: a larger table lowers the
    /// extra disruption of a membership change and evens out weights, at the cost of memory (4 bytes per slot)
    /// and slower <see cref="Add"/> / <see cref="Remove"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="tableSize"/> is less than 2, greater than <see cref="MaxTableSize"/>, or not prime.
    /// </exception>
    public MaglevHash(int tableSize = DefaultTableSize)
    {
        if (tableSize < 2 || tableSize > MaxTableSize)
            throw new ArgumentOutOfRangeException(nameof(tableSize), tableSize, $"Table size must be a prime between 2 and {MaxTableSize}.");
        if (!IsPrime(tableSize))
            throw new ArgumentOutOfRangeException(nameof(tableSize), tableSize, $"Table size must be prime; the next prime is {NextPrime(tableSize)}.");

        _tableSize = tableSize;
        _keyHasher = default;
        _nodeHasher = default;
        _registry = new Dictionary<string, Registration>(StringComparer.Ordinal);
        _snapshot = Snapshot.Empty;
    }

    /// <summary>Gets the number of physical nodes currently in the table.</summary>
    /// <remarks>Read from the published snapshot, so it is lock-free and always agrees with what routing sees.</remarks>
    public int NodeCount => _snapshot.Nodes.Length;

    /// <summary>Gets the number of lookup-table slots (the constructor argument).</summary>
    public int TableSize => _tableSize;

    /// <summary>
    /// Adds a node under a stable identity string and rebuilds the lookup table.
    /// </summary>
    /// <param name="nodeId">
    /// The node's stable identity, which fixes its permutation of the table. Two instances that must agree on
    /// routing have to use the same identity string for the same node. Must be non-<c>null</c>.
    /// </param>
    /// <param name="node">The payload returned when a key routes to this node.</param>
    /// <param name="weight">
    /// The node's relative capacity. A node of weight <c>w</c> takes <c>w</c> turns per round of the fill, so it
    /// owns about <c>w</c> times the slots of a weight-1 node. Must be at least 1.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="nodeId"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is less than 1.</exception>
    /// <exception cref="ArgumentException">A node with the same <paramref name="nodeId"/> is already in the table.</exception>
    /// <exception cref="InvalidOperationException">
    /// The total weight of the nodes would exceed <see cref="TableSize"/>, so some node could own no slot;
    /// construct the instance with a larger table.
    /// </exception>
    public void Add(string nodeId, TNode node, int weight = 1)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        if (weight < 1)
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weight must be at least 1.");
        if (_registry.ContainsKey(nodeId))
            throw new ArgumentException($"A node with id '{nodeId}' is already in the table.", nameof(nodeId));

        long totalWeight = _totalWeight + weight;
        if (totalWeight > _tableSize)
            throw new InvalidOperationException(
                $"The total node weight would be {totalWeight}, exceeding the table size of {_tableSize}. Use a larger table.");

        // Build the new table before touching the registry, so a failure (an out-of-memory table allocation, say)
        // leaves the registry, the weight total and the published snapshot exactly as they were.
        var registration = new Registration(node, weight);
        List<KeyValuePair<string, Registration>> members = Members(exclude: null);
        members.Add(new KeyValuePair<string, Registration>(nodeId, registration));
        Snapshot next = Build(members);

        _registry.Add(nodeId, registration);
        _totalWeight = totalWeight;
        _snapshot = next;
    }

    /// <summary>
    /// Removes a node by its identity and rebuilds the lookup table.
    /// </summary>
    /// <param name="nodeId">The identity the node was added under.</param>
    /// <returns><c>true</c> if a node was found and removed; <c>false</c> if no node had that identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="nodeId"/> is <c>null</c>.</exception>
    public bool Remove(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        if (!_registry.TryGetValue(nodeId, out Registration? registration))
            return false;

        // As in Add: build first, then commit, so a failed rebuild changes nothing.
        Snapshot next = Build(Members(exclude: nodeId));

        _registry.Remove(nodeId);
        _totalWeight -= registration.Weight;
        _snapshot = next;
        return true;
    }

    /// <summary>Determines whether a node with the specified identity is in the table.</summary>
    /// <param name="nodeId">The identity to test.</param>
    /// <returns><c>true</c> if the table contains that node.</returns>
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

    /// <summary>Routes a key to its owning node: the node in slot <c>hash(key) mod TableSize</c>.</summary>
    /// <param name="key">The key to route.</param>
    /// <returns>The owning node.</returns>
    /// <exception cref="InvalidOperationException">The table is empty (no nodes have been added).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TNode GetNode(TKey key)
    {
        Snapshot snapshot = _snapshot;
        TNode[] nodes = snapshot.Nodes;
        if (nodes.Length == 0)
            throw new InvalidOperationException("The table is empty; add at least one node before routing keys.");

        return nodes[snapshot.Table[snapshot.SlotOf((uint)_keyHasher.Hash(key))]];
    }

    /// <summary>
    /// Attempts to route a key to its owning node, returning <c>false</c> instead of throwing when the table is
    /// empty.
    /// </summary>
    /// <param name="key">The key to route.</param>
    /// <param name="node">
    /// When this method returns <c>true</c>, the owning node; otherwise the default value of
    /// <typeparamref name="TNode"/>.
    /// </param>
    /// <returns><c>true</c> if the key was routed; <c>false</c> if the table is empty.</returns>
    public bool TryGetNode(TKey key, out TNode node)
    {
        Snapshot snapshot = _snapshot;
        TNode[] nodes = snapshot.Nodes;
        if (nodes.Length == 0)
        {
            node = default!;
            return false;
        }

        node = nodes[snapshot.Table[snapshot.SlotOf((uint)_keyHasher.Hash(key))]];
        return true;
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> <em>distinct</em> nodes for a key: its owner, then the next distinct
    /// nodes met walking forward through the table.
    /// </summary>
    /// <param name="key">The key to route.</param>
    /// <param name="count">The maximum number of distinct nodes to return. <c>0</c> returns an empty list.</param>
    /// <returns>
    /// The distinct nodes, owner first. Fewer than <paramref name="count"/> are returned when the table has fewer
    /// nodes; an empty list when it is empty.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <remarks>
    /// Allocates the returned array, plus — with more than 1,024 physical nodes — a seen-set rented from
    /// <see cref="ArrayPool{T}.Shared"/>, which allocates only when the pool has no buffer to lend. On a
    /// per-request path, prefer <see cref="GetReplicas(TKey, Span{TNode})"/>.
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
        WalkReplicas(snapshot, (uint)_keyHasher.Hash(key), replicas);
        return replicas;
    }

    /// <summary>
    /// Writes the <em>distinct</em> nodes for a key into <paramref name="destination"/> — the allocation-free form
    /// of <see cref="GetReplicas(TKey, int)"/>, with the buffer's length as the replica count.
    /// </summary>
    /// <param name="key">The key to route.</param>
    /// <param name="destination">
    /// The buffer to fill, owner first. Its length is the number of distinct nodes asked for; an empty buffer asks
    /// for none.
    /// </param>
    /// <returns>
    /// The number of nodes written: <c>destination.Length</c>, or <see cref="NodeCount"/> when the table has fewer
    /// nodes; <c>0</c> when it is empty. Elements past the returned count are left unchanged.
    /// </returns>
    /// <remarks>
    /// Writes exactly the nodes <see cref="GetReplicas(TKey, int)"/> returns for the same count, in the same order,
    /// read from one consistent snapshot. Nothing is allocated with up to 1,024 physical nodes, where the walk's
    /// seen-set lives on the stack; a larger table rents it from <see cref="ArrayPool{T}.Shared"/>.
    /// </remarks>
    public int GetReplicas(TKey key, Span<TNode> destination)
    {
        Snapshot snapshot = _snapshot;
        int take = Math.Min(destination.Length, snapshot.Nodes.Length);
        if (take == 0)
            return 0;

        WalkReplicas(snapshot, (uint)_keyHasher.Hash(key), destination.Slice(0, take));
        return take;
    }

    // Fills `replicas` with the first replicas.Length distinct owners met walking forward from the key's slot. The
    // caller guarantees 1 <= replicas.Length <= NodeCount, and every node owns at least one slot (the total weight
    // never exceeds the table size), so the walk completes within one lap of the table.
    private static void WalkReplicas(Snapshot snapshot, uint keyHash, Span<TNode> replicas)
    {
        int[] table = snapshot.Table;
        TNode[] nodes = snapshot.Nodes;

        int slot = (int)snapshot.SlotOf(keyHash);
        int owner = table[slot];
        replicas[0] = nodes[owner];
        if (replicas.Length == 1)
            return;

        // One bit per physical node, set once that node has been written.
        int words = (nodes.Length + 63) >> 6;
        ulong[]? rented = null;
        Span<ulong> seen = words <= StackSeenWords
            ? stackalloc ulong[StackSeenWords]
            : (rented = ArrayPool<ulong>.Shared.Rent(words));
        seen = seen.Slice(0, words);
        seen.Clear();

        try
        {
            seen[owner >> 6] |= 1UL << (owner & 63);
            for (int written = 1; written < replicas.Length;)
            {
                if (++slot == table.Length)
                    slot = 0;

                owner = table[slot];
                ref ulong word = ref seen[owner >> 6];
                ulong bit = 1UL << (owner & 63);
                if ((word & bit) == 0)
                {
                    word |= bit;
                    replicas[written++] = nodes[owner];
                }
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<ulong>.Shared.Return(rented);
        }
    }

    // The registered nodes, minus `exclude` when it is non-null.
    private List<KeyValuePair<string, Registration>> Members(string? exclude)
    {
        var members = new List<KeyValuePair<string, Registration>>(_registry.Count + 1);
        foreach (KeyValuePair<string, Registration> entry in _registry)
        {
            if (!string.Equals(entry.Key, exclude, StringComparison.Ordinal))
                members.Add(entry);
        }

        return members;
    }

    // Builds the lookup table for `members` without publishing it. Nodes are processed in ordinal order of their
    // identities, and each node's permutation is a pure function of its identity and the table size, so the table
    // is a pure function of the (nodeId, weight) set and the configuration — identical on every process.
    private Snapshot Build(List<KeyValuePair<string, Registration>> members)
    {
        int nodeCount = members.Count;
        if (nodeCount == 0)
            return Snapshot.Empty;

        members.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));

        uint size = (uint)_tableSize;
        var nodes = new TNode[nodeCount];
        var weights = new int[nodeCount];
        var cursor = new uint[nodeCount];
        var skip = new uint[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            Registration registration = members[i].Value;
            nodes[i] = registration.Node;
            weights[i] = registration.Weight;

            // The permutation of node i is offset, offset + skip, offset + 2·skip, … (mod size). A prime size and
            // 1 <= skip < size make it visit every slot exactly once.
            ulong hash = _nodeHasher.Hash64(members[i].Key);
            cursor[i] = (uint)hash % size;
            skip[i] = (uint)(hash >> 32) % (size - 1) + 1;
        }

        var table = new int[size];
        table.AsSpan().Fill(-1);

        // Nodes take turns, `weight` claims per round each, so a node of weight w owns at least w slots once the
        // first round completes, and the first round always completes because the total weight is at most the
        // table size. Each claim advances the node along its permutation to its next unclaimed slot.
        uint filled = 0;
        while (true)
        {
            for (int i = 0; i < nodeCount; i++)
            {
                uint c = cursor[i];
                uint step = skip[i];
                for (int turn = weights[i]; turn > 0; turn--)
                {
                    while (table[c] >= 0)
                    {
                        c += step;
                        if (c >= size)
                            c -= size;
                    }

                    table[c] = i;
                    if (++filled == size)
                        return new Snapshot(table, nodes, size);

                    c += step;
                    if (c >= size)
                        c -= size;
                }

                cursor[i] = c;
            }
        }
    }

    // Trial division is enough for a constructor argument no larger than MaxTableSize (divisors up to 4,096).
    private static bool IsPrime(int value)
    {
        if (value < 4)
            return value >= 2;
        if ((value & 1) == 0)
            return false;

        for (int divisor = 3; divisor <= value / divisor; divisor += 2)
        {
            if (value % divisor == 0)
                return false;
        }

        return true;
    }

    // Only called with a value in [2, MaxTableSize] that is not prime, so the search stays far below int.MaxValue.
    private static int NextPrime(int value)
    {
        while (!IsPrime(++value))
        {
        }

        return value;
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

    // An immutable routing table. Table[s] is the index into Nodes of the node owning slot s. Published atomically
    // via the volatile _snapshot field so readers never observe a partially built table.
    private sealed class Snapshot
    {
        internal static readonly Snapshot Empty = new Snapshot(Array.Empty<int>(), Array.Empty<TNode>(), 1);

        internal Snapshot(int[] table, TNode[] nodes, uint size)
        {
            Table = table;
            Nodes = nodes;
            Size = size;
            Multiplier = FastUtils.GetFastModMultiplier(size);
        }

        internal int[] Table { get; }

        internal TNode[] Nodes { get; }

        internal uint Size { get; }

        internal ulong Multiplier { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal uint SlotOf(uint keyHash) => FastUtils.FastMod(keyHash, Size, Multiplier);
    }
}

/// <summary>
/// A <see cref="MaglevHash{TNode, TKey, THasher}"/> specialized for <see cref="string"/> keys with the
/// deterministic, cross-runtime <see cref="StringXxHash3Hasher"/> — the common case, so callers routing string
/// keys avoid spelling out the three type arguments.
/// </summary>
/// <typeparam name="TNode">The node payload returned by a lookup.</typeparam>
public sealed class StringMaglevHash<TNode> : MaglevHash<TNode, string, StringXxHash3Hasher>
{
    /// <summary>
    /// Initializes a new, empty <see cref="StringMaglevHash{TNode}"/>.
    /// </summary>
    /// <param name="tableSize">The number of lookup-table slots, a prime; see the base constructor.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="tableSize"/> is less than 2, greater than <see cref="MaglevHash{TNode, TKey, THasher}.MaxTableSize"/>, or not prime.
    /// </exception>
    public StringMaglevHash(int tableSize = DefaultTableSize)
        : base(tableSize)
    {
    }
}
