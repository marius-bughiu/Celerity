using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable sorted map</b> backed by a <b>persistent B+ tree</b>: a <b>single-key edit</b> returns a new
/// map that <b>shares</b> all but one root-to-leaf path of the old one's storage — while a write that changes
/// nothing hands back the receiver — and the ordered queries the BCL's immutable sorted dictionary does not
/// have (<see cref="Min"/>, <see cref="Max"/>, <see cref="TryGetLowerBound"/>, <see cref="TryGetUpperBound"/>,
/// <see cref="TryGetFloor"/>, <see cref="TryGetPredecessor"/>, <see cref="EnumerateRange"/>) each cost one
/// <c>O(log n)</c> descent.
/// </summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <typeparam name="TComparer">
/// The key ordering. Must be a struct so the JIT specializes the tree for it and inlines the comparison;
/// <see cref="DefaultComparer{T}"/> forwards to <see cref="Comparer{T}.Default"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// <c>System.Collections.Immutable.ImmutableSortedDictionary&lt;TKey, TValue&gt;</c> is an <b>AVL tree</b> with
/// one heap node per entry. Its branching factor is two, so a lookup is a pointer chase per level — about
/// seventeen node dereferences at 100,000 entries — and every comparison is an interface call through the
/// <see cref="IComparer{T}"/> it holds. It also answers only <i>exact</i> questions: it has no
/// <c>Min</c> / <c>Max</c>, no floor or ceiling, and no range enumeration, so "the first key at or after
/// <i>x</i>" on an immutable sorted map is an <c>O(n)</c> scan from the start.
/// </para>
/// <para>
/// <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/> is a B+ tree of fan-out 32: every entry lives in a
/// leaf of up to 32 entries held in two flat arrays, and a branch holds up to 32 children and the separator
/// keys between them. 100,000 entries are four levels deep rather than seventeen, a level costs one binary
/// search over a contiguous key array, and in-order enumeration walks leaf arrays rather than a node per entry.
/// An edit copies only the nodes on its root-to-leaf path — four arrays' worth at 100,000 entries — and shares
/// every other node with the map it came from; a copied branch whose separators did not change shares its key
/// array too. <typeparamref name="TComparer"/> is a struct type argument, as in
/// <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>, so the comparison is inlined rather than dispatched.
/// </para>
/// <para>
/// The documented BCL-beating workload is the one <see cref="PersistentHashMap{TKey, TValue, THasher}"/>
/// serves, when the reader also needs <b>order</b>: a sorted snapshot that is read far more often than it is
/// written, must still be updatable without copying, and is queried by <i>position in the order</i> as well as
/// by key — a versioned order book or price ladder, a time-indexed state answering "the value as of <i>t</i>"
/// with <see cref="TryGetFloor"/>, a route or range table swapped atomically, or a sorted symbol table
/// threaded through a compiler pass. Measured figures are in
/// <see href="https://github.com/marius-bughiu/Celerity/blob/main/docs/api/collections.md">the API reference</see>.
/// </para>
/// <para>
/// <b>Concurrent readers need no synchronization</b>, for the reason a value needs none: no published map is
/// ever mutated after it is returned, so there is no state for two threads to race over. A shared
/// <i>variable</i> holding successive maps still needs the usual publication rules, and the guarantee covers
/// the map's <i>own</i> state only: every lookup calls <typeparamref name="TComparer"/>, and
/// <see cref="ContainsValue"/> calls <see cref="EqualityComparer{T}"/><c>.Default</c> on
/// <typeparamref name="TValue"/>, so a comparer, key or value whose comparison is not itself thread-safe makes
/// concurrent reads unsafe however immutable the map is.
/// </para>
/// <para>
/// <b>The comparer is <c>default(TComparer)</c>.</b> Every map — <see cref="Empty"/> included — orders by the
/// default value of <typeparamref name="TComparer"/>, because the type shares one static empty map and every
/// node operation is static. A comparer must therefore carry its configuration in its type rather than in
/// fields, as <see cref="DefaultComparer{T}"/> does — unlike <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>,
/// this type has no constructor taking a comparer instance. A <c>null</c> key is
/// legal whenever the comparer orders it: <see cref="Comparer{T}.Default"/> orders <c>null</c> before every
/// non-<c>null</c> key, so unlike the hashed family this map needs no out-of-band slot for
/// <c>default(TKey)</c>.
/// </para>
/// <para>
/// Two deliberate omissions, both mirroring <see cref="PersistentHashMap{TKey, TValue, THasher}"/>. There is no
/// <c>Clear()</c>: everywhere else in this library <c>Clear()</c> means in-place mutation, so the empty map is
/// spelled <see cref="Empty"/>. And this type does not implement <c>IImmutableDictionary&lt;TKey, TValue&gt;</c>,
/// whose <c>Clear()</c> and whose duplicate-tolerant <c>Add</c> contradict decisions this type takes; it
/// implements <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
/// </para>
/// </remarks>
public sealed class PersistentSortedMap<TKey, TValue, TComparer> : IReadOnlyDictionary<TKey, TValue?>
    where TComparer : struct, IComparer<TKey>
{
    // A leaf holds up to MaxFill entries and a branch up to MaxFill children. Every node except the root keeps
    // at least MinFill, which is what bounds the depth; an insert that overflows a node to MaxFill + 1 splits it
    // into MinFill and MinFill + 1, and a removal that drops one below MinFill borrows from or merges with a
    // sibling.
    private const int MaxFill = 32;
    private const int MinFill = MaxFill / 2;

    // Arrays a builder owns are allocated at this length, one past MaxFill, so a node can hold the transient
    // overflow an insert produces before it splits — and so an owned node never has to be reallocated to take
    // an insert in place. A persistent edit allocates exact-length arrays instead.
    private const int BuilderCapacity = MaxFill + 1;

    // The deepest tree int.MaxValue entries can build. A tree of depth d (leaves at level d) holds at least
    // 2 * MinFill^(d - 1) entries — a two-child root above minimal branches above minimal leaves — and
    // 2 * 16^7 = 2^29 is the last power under 2^31, so d <= 8. The enumerator's inline path is this deep.
    private const int MaxDepth = 8;

    private static readonly TKey[] NoKeys = Array.Empty<TKey>();
    private static readonly TValue?[] NoValues = Array.Empty<TValue?>();

    /// <summary>
    /// The empty map. <see cref="Remove"/> of the last entry returns this instance, so draining a map one key
    /// at a time ends at the same object every empty map starts from.
    /// </summary>
    public static readonly PersistentSortedMap<TKey, TValue, TComparer> Empty =
        new(new Node(NoKeys, NoValues, children: null, count: 0, owner: null), 0);

    private readonly Node _root;
    private readonly int _count;

    private PersistentSortedMap(Node root, int count)
    {
        _root = root;
        _count = count;
    }

    /// <summary>
    /// Initializes a new map containing the entries of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The entries to copy into the map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> contains duplicate keys.</exception>
    /// <remarks>
    /// The entries are inserted through a <see cref="Builder"/>, so no intermediate map is allocated per entry
    /// and no root-to-leaf path is copied per entry either. The source does not need to be sorted.
    /// </remarks>
    public PersistentSortedMap(IEnumerable<KeyValuePair<TKey, TValue>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var builder = new Builder();
        foreach (KeyValuePair<TKey, TValue> entry in source)
            builder.Add(entry.Key, entry.Value);

        PersistentSortedMap<TKey, TValue, TComparer> built = builder.ToImmutable();
        _root = built._root;
        _count = built._count;
    }

    /// <summary>
    /// Gets the number of entries in the map.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets a value indicating whether the map contains no entries.
    /// </summary>
    public bool IsEmpty => _count == 0;

    /// <summary>
    /// Gets the value stored under <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value stored under <paramref name="key"/>.</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> is not present.</exception>
    public TValue this[TKey key]
    {
        get
        {
            if (!TryGetValue(key, out TValue? value))
                throw new KeyNotFoundException($"The key '{key}' was not present in the map.");

            return value!;
        }
    }

    /// <summary>
    /// Gets an enumerable view over the keys in ascending order. The view is a lightweight struct and iterating
    /// it does not allocate.
    /// </summary>
    public KeyCollection Keys => new(this);

    /// <summary>
    /// Gets an enumerable view over the values, in the ascending order of their keys. The view is a lightweight
    /// struct and iterating it does not allocate.
    /// </summary>
    public ValueCollection Values => new(this);

    /// <summary>
    /// Gets the entry with the smallest key, in <c>O(log n)</c>.
    /// </summary>
    /// <returns>The first entry in key order.</returns>
    /// <exception cref="InvalidOperationException">The map is empty.</exception>
    public KeyValuePair<TKey, TValue?> Min =>
        TryGetMin(out KeyValuePair<TKey, TValue?> entry)
            ? entry
            : throw new InvalidOperationException("The map is empty.");

    /// <summary>
    /// Gets the entry with the largest key, in <c>O(log n)</c>.
    /// </summary>
    /// <returns>The last entry in key order.</returns>
    /// <exception cref="InvalidOperationException">The map is empty.</exception>
    public KeyValuePair<TKey, TValue?> Max =>
        TryGetMax(out KeyValuePair<TKey, TValue?> entry)
            ? entry
            : throw new InvalidOperationException("The map is empty.");

    /// <summary>
    /// Determines whether <paramref name="key"/> is present in the map.
    /// </summary>
    /// <param name="key">The key to look for.</param>
    /// <returns><c>true</c> if the key is present; otherwise <c>false</c>.</returns>
    public bool ContainsKey(TKey key) => TryGetValue(key, out _);

    /// <summary>
    /// Determines whether any entry holds <paramref name="value"/>, compared with
    /// <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    /// <param name="value">The value to look for.</param>
    /// <returns><c>true</c> if some entry holds the value; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Unlike <see cref="ContainsKey"/>, this is a full scan: <c>O(n)</c>, because the tree is ordered by key
    /// and nothing about a value says where it lives.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        foreach (KeyValuePair<TKey, TValue?> entry in this)
        {
            if (EqualityComparer<TValue?>.Default.Equals(entry.Value, value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Looks up <paramref name="key"/> without throwing when it is absent.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this returns <c>true</c>, the value stored under <paramref name="key"/>; otherwise <c>default</c>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// One binary search over a contiguous key array per level, over a tree of fan-out 32: four levels at
    /// 100,000 entries and never more than eight.
    /// </remarks>
    public bool TryGetValue(TKey key, out TValue? value) => TryFind(_root, key, out value);

    /// <summary>
    /// Gets the entry with the smallest key.
    /// </summary>
    /// <param name="entry">The first entry in key order, or <c>default</c> when the map is empty.</param>
    /// <returns><c>true</c> if the map is non-empty.</returns>
    public bool TryGetMin(out KeyValuePair<TKey, TValue?> entry)
    {
        if (_count == 0)
        {
            entry = default;
            return false;
        }

        entry = FirstEntry(_root);
        return true;
    }

    /// <summary>
    /// Gets the entry with the largest key.
    /// </summary>
    /// <param name="entry">The last entry in key order, or <c>default</c> when the map is empty.</param>
    /// <returns><c>true</c> if the map is non-empty.</returns>
    public bool TryGetMax(out KeyValuePair<TKey, TValue?> entry)
    {
        if (_count == 0)
        {
            entry = default;
            return false;
        }

        entry = LastEntry(_root);
        return true;
    }

    /// <summary>
    /// Finds the first entry whose key is <b>greater than or equal to</b> <paramref name="key"/> — the
    /// <c>lower_bound</c> of the ordered containers, or the <i>ceiling</i> of <paramref name="key"/> — in
    /// <c>O(log n)</c>. An exact match is its own lower bound.
    /// </summary>
    /// <param name="key">The key to bound.</param>
    /// <param name="entry">The bounding entry, or <c>default</c> when every key is smaller.</param>
    /// <returns><c>true</c> if such an entry exists.</returns>
    public bool TryGetLowerBound(TKey key, out KeyValuePair<TKey, TValue?> entry) =>
        TrySeek(_root, key, strict: false, out entry);

    /// <summary>
    /// Finds the first entry whose key is <b>strictly greater than</b> <paramref name="key"/> — the
    /// <c>upper_bound</c> of the ordered containers — in <c>O(log n)</c>.
    /// </summary>
    /// <param name="key">The key to bound.</param>
    /// <param name="entry">The bounding entry, or <c>default</c> when no key is larger.</param>
    /// <returns><c>true</c> if such an entry exists.</returns>
    public bool TryGetUpperBound(TKey key, out KeyValuePair<TKey, TValue?> entry) =>
        TrySeek(_root, key, strict: true, out entry);

    /// <summary>
    /// Finds the last entry whose key is <b>less than or equal to</b> <paramref name="key"/> — the <i>floor</i>
    /// of <paramref name="key"/> — in <c>O(log n)</c>. An exact match is its own floor. This is the "value as
    /// of <i>t</i>" query over a map keyed by time or version.
    /// </summary>
    /// <param name="key">The key to bound.</param>
    /// <param name="entry">The bounding entry, or <c>default</c> when every key is larger.</param>
    /// <returns><c>true</c> if such an entry exists.</returns>
    public bool TryGetFloor(TKey key, out KeyValuePair<TKey, TValue?> entry) =>
        TrySeekBackward(_root, key, strict: false, out entry);

    /// <summary>
    /// Finds the last entry whose key is <b>strictly less than</b> <paramref name="key"/> — its predecessor —
    /// in <c>O(log n)</c>.
    /// </summary>
    /// <param name="key">The key to bound.</param>
    /// <param name="entry">The bounding entry, or <c>default</c> when no key is smaller.</param>
    /// <returns><c>true</c> if such an entry exists.</returns>
    public bool TryGetPredecessor(TKey key, out KeyValuePair<TKey, TValue?> entry) =>
        TrySeekBackward(_root, key, strict: true, out entry);

    /// <summary>
    /// Enumerates, in ascending key order, every entry whose key lies in the half-open range
    /// <c>[fromInclusive, toExclusive)</c>. The scan seeks to the lower bound in <c>O(log n)</c> and then walks
    /// the leaves' contiguous arrays, so it costs <c>O(log n + k)</c> for <c>k</c> results rather than a full
    /// <c>O(n)</c> filter.
    /// </summary>
    /// <param name="fromInclusive">The inclusive lower bound of the range.</param>
    /// <param name="toExclusive">The exclusive upper bound of the range.</param>
    /// <returns>An allocation-free enumerable over the matching entries.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="toExclusive"/> orders before <paramref name="fromInclusive"/>.
    /// </exception>
    public RangeEnumerable EnumerateRange(TKey fromInclusive, TKey toExclusive)
    {
        if (default(TComparer).Compare(fromInclusive, toExclusive) > 0)
            throw new ArgumentException("toExclusive must not order before fromInclusive.", nameof(toExclusive));

        return new RangeEnumerable(this, fromInclusive, toExclusive);
    }

    /// <summary>
    /// Returns a map with <paramref name="key"/> mapped to <paramref name="value"/>, requiring the key to be
    /// absent. This map is unchanged.
    /// </summary>
    /// <param name="key">The key to insert.</param>
    /// <param name="value">The value to store under it.</param>
    /// <returns>A map of <c>Count + 1</c> entries sharing this one's storage.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is already present.</exception>
    /// <remarks>
    /// Matches <see cref="Dictionary{TKey, TValue}.Add(TKey, TValue)"/> — and differs from
    /// <c>ImmutableSortedDictionary&lt;TKey, TValue&gt;.Add</c>, which tolerates a duplicate key whose value is
    /// equal to the one already stored. Use <see cref="SetItem"/> for insert-or-overwrite.
    /// </remarks>
    public PersistentSortedMap<TKey, TValue, TComparer> Add(TKey key, TValue value)
    {
        PersistentSortedMap<TKey, TValue, TComparer> result = Put(key, value, overwrite: false, out bool added);
        if (!added)
            throw new ArgumentException($"An entry with the key '{key}' already exists.", nameof(key));

        return result;
    }

    /// <summary>
    /// Returns a map with <paramref name="key"/> mapped to <paramref name="value"/>, whether or not the key was
    /// already present. This map is unchanged.
    /// </summary>
    /// <param name="key">The key to insert or overwrite.</param>
    /// <param name="value">The value to store under it.</param>
    /// <returns>
    /// A map sharing this one's storage outside the affected root-to-leaf path, or this map when
    /// <paramref name="key"/> already maps to a value equal to <paramref name="value"/>.
    /// </returns>
    /// <remarks>
    /// Copying is confined to the root-to-leaf path: at most eight nodes of at most 32 entries or children each.
    /// An insert that overflows a node splits it, which adds one node per level it reaches.
    /// </remarks>
    public PersistentSortedMap<TKey, TValue, TComparer> SetItem(TKey key, TValue value) =>
        Put(key, value, overwrite: true, out _);

    /// <summary>
    /// Returns a map with every entry of <paramref name="items"/> inserted or overwritten. This map is
    /// unchanged.
    /// </summary>
    /// <param name="items">The entries to insert or overwrite.</param>
    /// <returns>
    /// A map holding this one's entries updated by <paramref name="items"/>, or this map when nothing changed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The updates run through one <see cref="Builder"/>, so <c>m</c> entries allocate one intermediate map
    /// rather than <c>m</c> of them, and later entries in <paramref name="items"/> win over earlier ones.
    /// </remarks>
    public PersistentSortedMap<TKey, TValue, TComparer> SetItems(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var builder = new Builder(this);
        foreach (KeyValuePair<TKey, TValue> entry in items)
            builder.Put(entry.Key, entry.Value, overwrite: true);

        return builder.Mutated ? builder.ToImmutable() : this;
    }

    /// <summary>
    /// Returns a map with <paramref name="key"/> removed. This map is unchanged.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>
    /// A map of <c>Count - 1</c> entries sharing this one's storage, this map when <paramref name="key"/> was
    /// absent, or <see cref="Empty"/> when it was the last entry.
    /// </returns>
    /// <remarks>
    /// A removal that leaves a node below half full borrows from or merges with a sibling, so every node but the
    /// root stays at least half full and the depth bound holds however the map was edited. The sibling is
    /// copied too, so a rebalancing removal copies up to two nodes per level it reaches.
    /// </remarks>
    public PersistentSortedMap<TKey, TValue, TComparer> Remove(TKey key)
    {
        bool removed = false;
        Node newRoot = RemoveFrom(_root, key, owner: null, ref removed);
        if (!removed)
            return this;

        return _count == 1
            ? Empty
            : new PersistentSortedMap<TKey, TValue, TComparer>(Collapse(newRoot), _count - 1);
    }

    /// <summary>
    /// Returns a map with every key in <paramref name="keys"/> removed. This map is unchanged.
    /// </summary>
    /// <param name="keys">The keys to remove. Keys that are absent are ignored.</param>
    /// <returns>A map without those keys, or this map when none of them was present.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="keys"/> is <c>null</c>.</exception>
    public PersistentSortedMap<TKey, TValue, TComparer> RemoveRange(IEnumerable<TKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var builder = new Builder(this);
        foreach (TKey key in keys)
            builder.Remove(key);

        return builder.Mutated ? builder.ToImmutable() : this;
    }

    /// <summary>
    /// Creates a mutable builder seeded with this map's entries, for making many changes without allocating a
    /// map per change.
    /// </summary>
    /// <returns>A builder holding this map's entries.</returns>
    public Builder ToBuilder() => new(this);

    /// <summary>
    /// Returns an enumerator over the map's entries in ascending key order.
    /// </summary>
    /// <returns>A struct enumerator over the entries.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator() =>
        GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    TValue? IReadOnlyDictionary<TKey, TValue?>.this[TKey key] => this[key];

    /// <inheritdoc/>
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue?>.Keys => Keys;

    /// <inheritdoc/>
    IEnumerable<TValue?> IReadOnlyDictionary<TKey, TValue?>.Values => Values;

    // ── The tree ──────────────────────────────────────────────────────────────────────────────────────

    // Refuses an insert that would wrap the count negative. The test lives inside the method rather than at
    // the call site so the hot path carries no branch of its own, and the ContainsKey probe runs only at the
    // ceiling — an overwrite of an existing key is still allowed on a full map. Mirrors
    // PersistentHashMap.ThrowIfFull.
    [ExcludeFromCodeCoverage(Justification = "Needs int.MaxValue entries — tens of gigabytes of leaf arrays " +
        "for any key and value type. The check exists so the count cannot silently wrap rather than because " +
        "a test can reach it.")]
    private void ThrowIfFull(TKey key)
    {
        if (_count == int.MaxValue && !ContainsKey(key))
            throw new InvalidOperationException("A map cannot hold more than int.MaxValue entries.");
    }

    // The comparer is constrained to a struct and the map orders by its default value, so `default(TComparer)`
    // is the value a field would hold and the JIT inlines the call through it identically. Taken here rather
    // than from an instance field because every node operation below is static.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Compare(TKey left, TKey right) => default(TComparer).Compare(left, right);

    // The position of `key` in a leaf, or the bitwise complement of the position it would be inserted at.
    private static int FindInLeaf(Node leaf, TKey key)
    {
        TKey[] keys = leaf.Keys;
        int lo = 0;
        int hi = leaf.Count - 1;
        while (lo <= hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            int order = Compare(keys[mid], key);
            if (order == 0)
                return mid;

            if (order < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return ~lo;
    }

    // The child of a branch whose key range holds `key`: the number of separators at or below it. Separator
    // i is a lower bound on every key under child i + 1 and orders after every key under child i, so this is
    // the one child `key` can live under.
    private static int ChildIndex(Node branch, TKey key)
    {
        TKey[] separators = branch.Keys;
        int lo = 0;
        int hi = branch.Count - 2;
        while (lo <= hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            if (Compare(separators[mid], key) <= 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return lo;
    }

    private static bool TryFind(Node node, TKey key, out TValue? value)
    {
        while (node.Children is { } children)
            node = children[ChildIndex(node, key)];

        int index = FindInLeaf(node, key);
        if (index >= 0)
        {
            value = node.Values[index];
            return true;
        }

        value = default;
        return false;
    }

    private static KeyValuePair<TKey, TValue?> FirstEntry(Node node)
    {
        while (node.Children is { } children)
            node = children[0];

        return new KeyValuePair<TKey, TValue?>(node.Keys[0], node.Values[0]);
    }

    private static KeyValuePair<TKey, TValue?> LastEntry(Node node)
    {
        while (node.Children is { } children)
            node = children[node.Count - 1];

        return new KeyValuePair<TKey, TValue?>(node.Keys[node.Count - 1], node.Values[node.Count - 1]);
    }

    // The first entry at or after `key` (or strictly after, when `strict`). The descent follows `key`, and on
    // the way down remembers the nearest subtree to the right of the path: if the leaf `key` lands in has
    // nothing large enough, the answer is that subtree's first entry, since every key in it orders after every
    // key on the path's side of its separator.
    private static bool TrySeek(Node node, TKey key, bool strict, out KeyValuePair<TKey, TValue?> entry)
    {
        Node? rightOfPath = null;
        while (node.Children is { } children)
        {
            int child = ChildIndex(node, key);
            if (child + 1 < node.Count)
                rightOfPath = children[child + 1];

            node = children[child];
        }

        int index = FindInLeaf(node, key);
        index = index >= 0 ? (strict ? index + 1 : index) : ~index;

        if (index < node.Count)
        {
            entry = new KeyValuePair<TKey, TValue?>(node.Keys[index], node.Values[index]);
            return true;
        }

        if (rightOfPath is not null)
        {
            entry = FirstEntry(rightOfPath);
            return true;
        }

        entry = default;
        return false;
    }

    // The mirror of TrySeek: the last entry at or before `key` (or strictly before, when `strict`), falling back
    // to the last entry of the nearest subtree left of the path.
    private static bool TrySeekBackward(Node node, TKey key, bool strict, out KeyValuePair<TKey, TValue?> entry)
    {
        Node? leftOfPath = null;
        while (node.Children is { } children)
        {
            int child = ChildIndex(node, key);
            if (child > 0)
                leftOfPath = children[child - 1];

            node = children[child];
        }

        int index = FindInLeaf(node, key);
        index = index >= 0 ? (strict ? index - 1 : index) : ~index - 1;

        if (index >= 0)
        {
            entry = new KeyValuePair<TKey, TValue?>(node.Keys[index], node.Values[index]);
            return true;
        }

        if (leftOfPath is not null)
        {
            entry = LastEntry(leftOfPath);
            return true;
        }

        entry = default;
        return false;
    }

    // Insert at the map level. `overwrite` false makes an existing key a no-op rather than a replacement, so
    // Add can report the duplicate without having written anything; `added` reports whether the key was new.
    private PersistentSortedMap<TKey, TValue, TComparer> Put(TKey key, TValue? value, bool overwrite, out bool added)
    {
        ThrowIfFull(key);

        added = false;
        bool changed = false;
        Node newRoot = PutInto(_root, key, value, overwrite, owner: null, ref added, ref changed);
        if (!changed)
            return this;

        return new PersistentSortedMap<TKey, TValue, TComparer>(
            GrowIfFull(newRoot, owner: null), added ? _count + 1 : _count);
    }

    // A root that overflowed splits, and the two halves become the children of a new root one level up. This
    // is the only way the tree gains depth, which is what keeps every leaf at the same level.
    private static Node GrowIfFull(Node root, object? owner)
    {
        if (root.Count <= MaxFill)
            return root;

        Node left = Split(root, owner, out Node right, out TKey separator);
        TKey[] separators = new TKey[owner is null ? 1 : BuilderCapacity];
        Node[] children = new Node[owner is null ? 2 : BuilderCapacity];
        separators[0] = separator;
        children[0] = left;
        children[1] = right;
        return new Node(separators, NoValues, children, count: 2, owner);
    }

    // A root branch left with one child after a merge is a level the tree no longer needs; dropping it is the
    // only way the tree loses depth.
    private static Node Collapse(Node root)
    {
        while (root.Children is { } children && root.Count == 1)
            root = children[0];

        return root;
    }

    // Insert into the subtree under `node`. `owner` is the builder's ownership token, or null for a persistent
    // write; a node the token owns is edited in place instead of copied. The returned node may hold
    // MaxFill + 1 entries or children — the caller splits it, because only the caller can insert the new
    // sibling next to it.
    private static Node PutInto(
        Node node, TKey key, TValue? value, bool overwrite, object? owner, ref bool added, ref bool changed)
    {
        if (node.Children is not { } children)
        {
            int index = FindInLeaf(node, key);
            if (index >= 0)
            {
                if (!overwrite || EqualityComparer<TValue?>.Default.Equals(node.Values[index], value))
                    return node;

                changed = true;
                return node.WithValue(index, value, owner);
            }

            added = true;
            changed = true;
            return node.InsertEntry(~index, key, value, owner);
        }

        int child = ChildIndex(node, key);
        Node newChild = PutInto(children[child], key, value, overwrite, owner, ref added, ref changed);
        if (!changed)
            return node;

        if (newChild.Count <= MaxFill)
            return node.WithChild(child, newChild, owner);

        Node left = Split(newChild, owner, out Node right, out TKey separator);
        return node.InsertChild(child, left, separator, right, owner);
    }

    // Splits a node holding MaxFill + 1 entries or children into a left half of MinFill and a right half of
    // MinFill + 1, and reports the separator the parent needs between them: for a leaf the right half's first
    // key, which stays in the leaf; for a branch the separator between the halves' children, which moves up.
    private static Node Split(Node full, object? owner, out Node right, out TKey separator)
    {
        const int LeftCount = MinFill;
        int rightCount = full.Count - LeftCount;

        if (full.Children is not { } children)
        {
            right = Node.Leaf(full.Keys, full.Values, LeftCount, rightCount, owner);
            separator = full.Keys[LeftCount];
            return full.Truncate(LeftCount, owner);
        }

        // The left half keeps LeftCount children and the LeftCount - 1 separators between them; separator
        // LeftCount - 1 moves up; the right half takes the rest.
        separator = full.Keys[LeftCount - 1];
        right = Node.Branch(full.Keys, LeftCount, children, LeftCount, rightCount, owner);
        return full.Truncate(LeftCount, owner);
    }

    // Remove from the subtree under `node`. The returned node may hold MinFill - 1 entries or children — the
    // caller rebalances it, because only the caller can reach its siblings.
    private static Node RemoveFrom(Node node, TKey key, object? owner, ref bool removed)
    {
        if (node.Children is not { } children)
        {
            int index = FindInLeaf(node, key);
            if (index < 0)
                return node;

            removed = true;
            return node.RemoveEntry(index, owner);
        }

        int child = ChildIndex(node, key);
        Node newChild = RemoveFrom(children[child], key, owner, ref removed);
        if (!removed)
            return node;

        if (newChild.Count >= MinFill)
            return node.WithChild(child, newChild, owner);

        return Rebalance(node, child, newChild, owner);
    }

    // `child` has dropped below MinFill. Pair it with a neighbour — the left one when there is one — and either
    // merge the two, when their entries fit in one node, or share their entries out evenly, so both halves end
    // at least half full. Every non-root branch has at least MinFill >= 2 children and a root branch at least
    // two, so a neighbour always exists.
    private static Node Rebalance(Node parent, int child, Node underfull, object? owner)
    {
        Node[] children = parent.Children!;
        int leftIndex = child > 0 ? child - 1 : child;
        Node left = child > 0 ? children[child - 1] : underfull;
        Node right = child > 0 ? underfull : children[child + 1];
        TKey separator = parent.Keys[leftIndex];
        int total = left.Count + right.Count;

        if (total <= MaxFill)
        {
            Node merged = Merge(left, separator, right, owner);
            return parent.ReplaceChildPair(leftIndex, merged, owner);
        }

        Node newLeft = Redistribute(left, separator, right, owner, out Node newRight, out TKey newSeparator);
        return parent.WithChildPair(leftIndex, newLeft, newSeparator, newRight, owner);
    }

    // Concatenates two neighbours into one node. For branches the parent's separator between them comes down
    // between the two runs of separators, since it is the bound between their children.
    private static Node Merge(Node left, TKey separator, Node right, object? owner)
    {
        int count = left.Count + right.Count;
        int capacity = owner is null ? count : BuilderCapacity;

        if (left.Children is not { } leftChildren)
        {
            var keys = new TKey[capacity];
            var values = new TValue?[capacity];
            Array.Copy(left.Keys, keys, left.Count);
            Array.Copy(right.Keys, 0, keys, left.Count, right.Count);
            Array.Copy(left.Values, values, left.Count);
            Array.Copy(right.Values, 0, values, left.Count, right.Count);
            return new Node(keys, values, children: null, count, owner);
        }

        var separators = new TKey[owner is null ? count - 1 : BuilderCapacity];
        var children = new Node[capacity];
        Array.Copy(left.Keys, separators, left.Count - 1);
        separators[left.Count - 1] = separator;
        Array.Copy(right.Keys, 0, separators, left.Count, right.Count - 1);
        Array.Copy(leftChildren, children, left.Count);
        Array.Copy(right.Children!, 0, children, left.Count, right.Count);
        return new Node(separators, NoValues, children, count, owner);
    }

    // Shares two neighbours' entries out evenly between two fresh nodes. Only called when the pair holds more
    // than MaxFill, so each half gets at least MinFill.
    private static Node Redistribute(
        Node left, TKey separator, Node right, object? owner, out Node newRight, out TKey newSeparator)
    {
        int count = left.Count + right.Count;
        int leftCount = count / 2;
        int rightCount = count - leftCount;

        // Built through one merged node of up to 2 * MaxFill - 1 entries rather than by shifting between the
        // two in place: a rebalance is the rare path, and the merged node is the simplest correct staging area
        // for both the leaf and the branch case.
        Node merged = Merge(left, separator, right, owner: null);

        if (merged.Children is not { } children)
        {
            newRight = Node.Leaf(merged.Keys, merged.Values, leftCount, rightCount, owner);
            newSeparator = merged.Keys[leftCount];
            return Node.Leaf(merged.Keys, merged.Values, 0, leftCount, owner);
        }

        newSeparator = merged.Keys[leftCount - 1];
        newRight = Node.Branch(merged.Keys, leftCount, children, leftCount, rightCount, owner);
        return Node.Branch(merged.Keys, 0, children, 0, leftCount, owner);
    }

    // A tree node. A leaf holds entries in Keys / Values[0..Count); a branch holds Count children and the
    // Count - 1 separators between them in Keys, and has no values. Arrays may be longer than Count — a
    // builder allocates them at BuilderCapacity, and a persistent copy can share a builder-made array — so
    // Count, never Length, is the bound.
    private sealed class Node
    {
        internal readonly TKey[] Keys;
        internal readonly TValue?[] Values;
        internal readonly Node[]? Children;
        internal int Count;

        // The builder whose token this is may edit the node in place; any other writer copies it. A published
        // map's nodes carry a token no live builder holds, so they are never edited again.
        internal readonly object? Owner;

        internal Node(TKey[] keys, TValue?[] values, Node[]? children, int count, object? owner)
        {
            Keys = keys;
            Values = values;
            Children = children;
            Count = count;
            Owner = owner;
        }

        private bool IsOwnedBy(object? owner) => owner is not null && ReferenceEquals(Owner, owner);

        private static int CapacityFor(int count, object? owner) => owner is null ? count : BuilderCapacity;

        internal static Node Leaf(TKey[] keys, TValue?[] values, int start, int count, object? owner)
        {
            int capacity = CapacityFor(count, owner);
            var newKeys = new TKey[capacity];
            var newValues = new TValue?[capacity];
            Array.Copy(keys, start, newKeys, 0, count);
            Array.Copy(values, start, newValues, 0, count);
            return new Node(newKeys, newValues, children: null, count, owner);
        }

        // A branch of `count` children starting at `childStart`, with the count - 1 separators that start at
        // `separatorStart`.
        internal static Node Branch(
            TKey[] separators, int separatorStart, Node[] children, int childStart, int count, object? owner)
        {
            var newSeparators = new TKey[owner is null ? count - 1 : BuilderCapacity];
            var newChildren = new Node[CapacityFor(count, owner)];
            Array.Copy(separators, separatorStart, newSeparators, 0, count - 1);
            Array.Copy(children, childStart, newChildren, 0, count);
            return new Node(newSeparators, NoValues, newChildren, count, owner);
        }

        // This node cut down to its first `count` entries or children: in place when the builder owns it,
        // clearing the vacated slots so they do not keep keys, values or subtrees alive, and as a fresh
        // exact-length copy otherwise.
        internal Node Truncate(int count, object? owner)
        {
            if (Children is { } children)
            {
                if (!IsOwnedBy(owner))
                    return Branch(Keys, 0, children, 0, count, owner);

                Array.Clear(Keys, count - 1, Count - count);
                Array.Clear(children, count, Count - count);
            }
            else
            {
                if (!IsOwnedBy(owner))
                    return Leaf(Keys, Values, 0, count, owner);

                ClearEntries(count, Count - count);
            }

            Count = count;
            return this;
        }

        internal Node WithValue(int index, TValue? value, object? owner)
        {
            if (IsOwnedBy(owner))
            {
                Values[index] = value;
                return this;
            }

            // A persistent copy can share the key array: the keys did not change and nothing will ever
            // write through a node no builder owns. A builder copy cannot, since it will edit its keys later.
            TKey[] keys = owner is null ? Keys : Copy(Keys, Count, BuilderCapacity);
            TValue?[] values = Copy(Values, Count, CapacityFor(Count, owner));
            values[index] = value;
            return new Node(keys, values, children: null, Count, owner);
        }

        internal Node WithChild(int index, Node child, object? owner)
        {
            if (IsOwnedBy(owner))
            {
                Children![index] = child;
                return this;
            }

            TKey[] separators = owner is null ? Keys : Copy(Keys, Count - 1, BuilderCapacity);
            Node[] children = Copy(Children!, Count, CapacityFor(Count, owner));
            children[index] = child;
            return new Node(separators, NoValues, children, Count, owner);
        }

        internal Node InsertEntry(int index, TKey key, TValue? value, object? owner)
        {
            int count = Count;
            Node target;
            if (IsOwnedBy(owner))
            {
                // An owned leaf's arrays are BuilderCapacity long and it holds at most MaxFill entries, so
                // the shift always has room.
                Array.Copy(Keys, index, Keys, index + 1, count - index);
                Array.Copy(Values, index, Values, index + 1, count - index);
                target = this;
            }
            else
            {
                int capacity = CapacityFor(count + 1, owner);
                target = new Node(
                    CopyWithGap(Keys, count, index, capacity),
                    CopyWithGap(Values, count, index, capacity),
                    children: null,
                    count,
                    owner);
            }

            target.Keys[index] = key;
            target.Values[index] = value;
            target.Count = count + 1;
            return target;
        }

        // Replaces child `index` with the two halves it split into, inserting the separator between them.
        internal Node InsertChild(int index, Node left, TKey separator, Node right, object? owner)
        {
            int count = Count;
            Node target;
            if (IsOwnedBy(owner))
            {
                Array.Copy(Children!, index + 1, Children!, index + 2, count - index - 1);
                Array.Copy(Keys, index, Keys, index + 1, count - 1 - index);
                target = this;
            }
            else
            {
                int capacity = CapacityFor(count + 1, owner);
                target = new Node(
                    CopyWithGap(Keys, count - 1, index, owner is null ? count : BuilderCapacity),
                    NoValues,
                    CopyWithGap(Children!, count, index + 1, capacity),
                    count,
                    owner);
            }

            target.Keys[index] = separator;
            target.Children![index] = left;
            target.Children[index + 1] = right;
            target.Count = count + 1;
            return target;
        }

        internal Node RemoveEntry(int index, object? owner)
        {
            int count = Count;
            if (IsOwnedBy(owner))
            {
                Array.Copy(Keys, index + 1, Keys, index, count - index - 1);
                Array.Copy(Values, index + 1, Values, index, count - index - 1);
                ClearEntries(count - 1, 1);
                Count = count - 1;
                return this;
            }

            int capacity = CapacityFor(count - 1, owner);
            return new Node(
                CopyWithout(Keys, count, index, capacity),
                CopyWithout(Values, count, index, capacity),
                children: null,
                count - 1,
                owner);
        }

        // Replaces children `index` and `index + 1` with the one node they merged into, dropping the separator
        // that stood between them.
        internal Node ReplaceChildPair(int index, Node merged, object? owner)
        {
            int count = Count;
            Node target;
            if (IsOwnedBy(owner))
            {
                Array.Copy(Children!, index + 2, Children!, index + 1, count - index - 2);
                Array.Copy(Keys, index + 1, Keys, index, count - index - 2);
                Children![count - 1] = null!;
                if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>())
                    Keys[count - 2] = default!;

                Count = count - 1;
                target = this;
            }
            else
            {
                target = new Node(
                    CopyWithout(Keys, count - 1, index, owner is null ? count - 2 : BuilderCapacity),
                    NoValues,
                    CopyWithout(Children!, count, index + 1, CapacityFor(count - 1, owner)),
                    count - 1,
                    owner);
            }

            target.Children![index] = merged;
            return target;
        }

        // Replaces children `index` and `index + 1` with a redistributed pair and the new separator between
        // them. The count does not change, so a persistent copy allocates nothing beyond the two arrays.
        internal Node WithChildPair(int index, Node left, TKey separator, Node right, object? owner)
        {
            Node target = this;
            if (!IsOwnedBy(owner))
            {
                target = new Node(
                    Copy(Keys, Count - 1, owner is null ? Count - 1 : BuilderCapacity),
                    NoValues,
                    Copy(Children!, Count, CapacityFor(Count, owner)),
                    Count,
                    owner);
            }

            target.Keys[index] = separator;
            target.Children![index] = left;
            target.Children[index + 1] = right;
            return target;
        }

        private void ClearEntries(int start, int length)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>())
                Array.Clear(Keys, start, length);

            if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
                Array.Clear(Values, start, length);
        }

        private static T[] Copy<T>(T[] source, int length, int capacity)
        {
            var copy = new T[capacity];
            Array.Copy(source, copy, length);
            return copy;
        }

        // A copy of source[0..length) with an unwritten slot opened at `gap`.
        private static T[] CopyWithGap<T>(T[] source, int length, int gap, int capacity)
        {
            var copy = new T[capacity];
            Array.Copy(source, copy, gap);
            Array.Copy(source, gap, copy, gap + 1, length - gap);
            return copy;
        }

        // A copy of source[0..length) with the element at `index` left out.
        private static T[] CopyWithout<T>(T[] source, int length, int index, int capacity)
        {
            var copy = new T[capacity];
            Array.Copy(source, copy, index);
            Array.Copy(source, index + 1, copy, index, length - index - 1);
            return copy;
        }
    }

    /// <summary>
    /// A mutable companion to <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/> for making a run of
    /// changes without allocating a map — or copying a root-to-leaf path — per change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A builder holds an ownership token, and every node it creates is stamped with it. A write that reaches a
    /// node the builder owns edits it in place; one that reaches a node shared with a published map copies it
    /// first, once, and owns the copy from then on. A run of <c>m</c> writes therefore copies each touched path
    /// at most once rather than <c>m</c> times. <see cref="ToImmutable"/> replaces the token, which makes every
    /// node the builder has handed out read-only again in one assignment — so the builder stays usable
    /// afterwards and no map it has produced can be changed behind a caller's back.
    /// </para>
    /// <para>
    /// The builder is not thread-safe. The maps it produces are, under the same callback caveat the containing
    /// type documents: a read calls <typeparamref name="TComparer"/>, so a stateful comparer or key is the
    /// caller's to reason about.
    /// </para>
    /// </remarks>
    public sealed class Builder
    {
        private object _owner;
        private Node _root;
        private int _count;

        /// <summary>
        /// Initializes a new, empty builder.
        /// </summary>
        public Builder()
            : this(Empty)
        {
        }

        internal Builder(PersistentSortedMap<TKey, TValue, TComparer> source)
        {
            _owner = new object();
            _root = source._root;
            _count = source._count;
        }

        /// <summary>
        /// Gets the number of entries the builder currently holds.
        /// </summary>
        public int Count => _count;

        // Whether any write has actually changed something, so SetItems / RemoveRange can hand back the
        // receiver rather than an equal copy when the caller's updates were all no-ops.
        internal bool Mutated { get; private set; }

        /// <summary>
        /// Gets or sets the value stored under <paramref name="key"/>. Setting inserts the key when it is
        /// absent.
        /// </summary>
        /// <param name="key">The key to look up or write.</param>
        /// <returns>The value stored under <paramref name="key"/>.</returns>
        /// <exception cref="KeyNotFoundException">On a read, <paramref name="key"/> is not present.</exception>
        public TValue this[TKey key]
        {
            get
            {
                if (!TryGetValue(key, out TValue? value))
                    throw new KeyNotFoundException($"The key '{key}' was not present in the builder.");

                return value!;
            }

            set => Put(key, value, overwrite: true);
        }

        /// <summary>
        /// Determines whether <paramref name="key"/> is present in the builder.
        /// </summary>
        /// <param name="key">The key to look for.</param>
        /// <returns><c>true</c> if the key is present; otherwise <c>false</c>.</returns>
        public bool ContainsKey(TKey key) => TryGetValue(key, out _);

        /// <summary>
        /// Looks up <paramref name="key"/> without throwing when it is absent.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">
        /// When this returns <c>true</c>, the value stored under <paramref name="key"/>; otherwise
        /// <c>default</c>.
        /// </param>
        /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
        public bool TryGetValue(TKey key, out TValue? value) => TryFind(_root, key, out value);

        /// <summary>
        /// Inserts <paramref name="key"/>, requiring it to be absent.
        /// </summary>
        /// <param name="key">The key to insert.</param>
        /// <param name="value">The value to store under it.</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is already present.</exception>
        public void Add(TKey key, TValue value)
        {
            // overwrite: false is what makes the rejection clean — a duplicate leaves the builder exactly as it
            // was, rather than replacing the value and then throwing.
            if (!Put(key, value, overwrite: false))
                throw new ArgumentException($"An entry with the key '{key}' already exists.", nameof(key));
        }

        /// <summary>
        /// Removes <paramref name="key"/> if it is present.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns><c>true</c> if the key was present and removed; otherwise <c>false</c>.</returns>
        public bool Remove(TKey key)
        {
            bool removed = false;
            Node newRoot = RemoveFrom(_root, key, _owner, ref removed);
            if (!removed)
                return false;

            _root = Collapse(newRoot);
            _count--;
            Mutated = true;
            return true;
        }

        /// <summary>
        /// Produces an immutable map holding the builder's current entries. The builder stays usable, and
        /// further changes to it do not affect any map it has already returned.
        /// </summary>
        /// <returns>A map holding the builder's entries.</returns>
        public PersistentSortedMap<TKey, TValue, TComparer> ToImmutable()
        {
            // Every node made under the old token becomes read-only the moment the builder stops recognizing
            // it, which is what makes handing out the tree by reference safe.
            _owner = new object();

            if (_count == 0)
            {
                // Drop the builder's own empty root too, so a later write starts from the shared empty leaf
                // rather than from a node the returned map could be said to have shared.
                _root = Empty._root;
                return Empty;
            }

            return new PersistentSortedMap<TKey, TValue, TComparer>(_root, _count);
        }

        // The builder's own ceiling guard, for the reason the map's carries: an insert past int.MaxValue would
        // wrap _count negative, and rejecting it before any node is written keeps a refused insert from leaving
        // the builder half-changed.
        [ExcludeFromCodeCoverage(Justification = "Needs int.MaxValue entries — tens of gigabytes of leaf " +
            "arrays for any key and value type. The check exists so the count cannot silently wrap rather " +
            "than because a test can reach it.")]
        private void ThrowIfFull(TKey key)
        {
            if (_count == int.MaxValue && !ContainsKey(key))
                throw new InvalidOperationException("A map cannot hold more than int.MaxValue entries.");
        }

        // Insert, reporting whether the key was new. `overwrite` false leaves an existing key untouched, so Add
        // can reject a duplicate without having changed the builder. Internal because SetItems needs the
        // nullable-tolerant form the public indexer cannot express.
        internal bool Put(TKey key, TValue? value, bool overwrite)
        {
            ThrowIfFull(key);

            bool added = false;
            bool changed = false;
            Node newRoot = PutInto(_root, key, value, overwrite, _owner, ref added, ref changed);
            if (!changed)
                return false;

            _root = GrowIfFull(newRoot, _owner);
            Mutated = true;
            if (added)
                _count++;

            return added;
        }
    }

    /// <summary>
    /// A struct enumerable view over the keys of a <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/>,
    /// in ascending order. Iterating it does not allocate; passing it through <see cref="IEnumerable{T}"/> will
    /// box the enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct KeyCollection : IEnumerable<TKey>
    {
        private readonly PersistentSortedMap<TKey, TValue, TComparer> _map;

        internal KeyCollection(PersistentSortedMap<TKey, TValue, TComparer> map) => _map = map;

        /// <summary>
        /// Gets the number of keys.
        /// </summary>
        public int Count => _map._count;

        /// <summary>
        /// Returns a struct enumerator over the keys in ascending order.
        /// </summary>
        /// <returns>A struct enumerator over the keys.</returns>
        public Enumerator GetEnumerator() => new(_map);

        /// <inheritdoc/>
        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// A struct enumerator over the keys of a <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private PersistentSortedMap<TKey, TValue, TComparer>.Enumerator _inner;

            internal Enumerator(PersistentSortedMap<TKey, TValue, TComparer> map) => _inner = map.GetEnumerator();

            /// <summary>Gets the key at the enumerator's current position.</summary>
            public TKey Current => _inner.Current.Key;

            /// <inheritdoc/>
            object? IEnumerator.Current => Current;

            /// <summary>Advances to the next key.</summary>
            /// <returns><c>true</c> if there is another key; otherwise <c>false</c>.</returns>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position, before the first key.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>Releases the resources used by the enumerator. The enumerator holds none.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }

    /// <summary>
    /// A struct enumerable view over the values of a
    /// <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/>, in the ascending order of their keys.
    /// Iterating it does not allocate; passing it through <see cref="IEnumerable{T}"/> will box the enumerator
    /// and is therefore not zero-allocation.
    /// </summary>
    public readonly struct ValueCollection : IEnumerable<TValue?>
    {
        private readonly PersistentSortedMap<TKey, TValue, TComparer> _map;

        internal ValueCollection(PersistentSortedMap<TKey, TValue, TComparer> map) => _map = map;

        /// <summary>
        /// Gets the number of values.
        /// </summary>
        public int Count => _map._count;

        /// <summary>
        /// Returns a struct enumerator over the values in the ascending order of their keys.
        /// </summary>
        /// <returns>A struct enumerator over the values.</returns>
        public Enumerator GetEnumerator() => new(_map);

        /// <inheritdoc/>
        IEnumerator<TValue?> IEnumerable<TValue?>.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// A struct enumerator over the values of a <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private PersistentSortedMap<TKey, TValue, TComparer>.Enumerator _inner;

            internal Enumerator(PersistentSortedMap<TKey, TValue, TComparer> map) => _inner = map.GetEnumerator();

            /// <summary>Gets the value at the enumerator's current position.</summary>
            public TValue? Current => _inner.Current.Value;

            /// <inheritdoc/>
            object? IEnumerator.Current => Current;

            /// <summary>Advances to the next value.</summary>
            /// <returns><c>true</c> if there is another value; otherwise <c>false</c>.</returns>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position, before the first value.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>Releases the resources used by the enumerator. The enumerator holds none.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }

    /// <summary>
    /// A struct enumerable over the entries of one key range of a
    /// <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/>, returned by <see cref="EnumerateRange"/>.
    /// </summary>
    public readonly struct RangeEnumerable : IEnumerable<KeyValuePair<TKey, TValue?>>
    {
        private readonly PersistentSortedMap<TKey, TValue, TComparer> _map;
        private readonly TKey _from;
        private readonly TKey _toExclusive;

        internal RangeEnumerable(PersistentSortedMap<TKey, TValue, TComparer> map, TKey from, TKey toExclusive)
        {
            _map = map;
            _from = from;
            _toExclusive = toExclusive;
        }

        /// <summary>Returns a struct enumerator over the entries in the range, in ascending key order.</summary>
        /// <returns>A struct enumerator over the matching entries.</returns>
        public Enumerator GetEnumerator() => new(_map, _from, _toExclusive);

        /// <inheritdoc/>
        IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator() =>
            GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A struct enumerator over the entries of a <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/> in
    /// ascending key order — the whole map, or one range of it.
    /// </summary>
    /// <remarks>
    /// The map cannot be modified, so this enumerator carries no version check and nothing can invalidate it.
    /// The root-to-leaf path is held inline in the struct — the tree is at most eight levels deep — which is
    /// what makes enumeration allocation-free.
    /// </remarks>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly Node _root;
        private readonly TKey _from;
        private readonly TKey _toExclusive;
        private readonly bool _bounded;

        // The branches from the root down to the current leaf, and which child of each the walk is in. Index 0
        // is the root; _depth branches sit above the leaf.
        private NodeStack _path;
        private CursorStack _cursors;
        private int _depth;
        private Node? _leaf;
        private int _index;
        private bool _finished;
        private KeyValuePair<TKey, TValue?> _current;

        internal Enumerator(PersistentSortedMap<TKey, TValue, TComparer> map)
            : this(map, default!, default!, bounded: false)
        {
        }

        internal Enumerator(PersistentSortedMap<TKey, TValue, TComparer> map, TKey from, TKey toExclusive)
            : this(map, from, toExclusive, bounded: true)
        {
        }

        private Enumerator(
            PersistentSortedMap<TKey, TValue, TComparer> map, TKey from, TKey toExclusive, bool bounded)
        {
            _root = map._root;
            _from = from;
            _toExclusive = toExclusive;
            _bounded = bounded;
            _path = default;
            _cursors = default;
            _depth = 0;
            _leaf = null;
            _index = 0;
            _finished = false;
            _current = default;
        }

        /// <summary>
        /// Gets the entry at the enumerator's current position, or <c>default</c> before the first
        /// <see cref="MoveNext"/> and after the last.
        /// </summary>
        public readonly KeyValuePair<TKey, TValue?> Current => _current;

        /// <inheritdoc/>
        readonly object? IEnumerator.Current => Current;

        /// <summary>
        /// Advances to the next entry.
        /// </summary>
        /// <returns><c>true</c> if there is another entry; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            if (_finished)
                return false;

            if (_leaf is null)
                Seek();
            else
                _index++;

            // Step to the next leaf when this one is spent. Only an empty root leaf has no entries at all; every
            // other leaf holds at least MinFill, so one step always lands on an entry.
            if (_index >= _leaf!.Count && !NextLeaf())
                return Finish();

            KeyValuePair<TKey, TValue?> entry = new(_leaf.Keys[_index], _leaf.Values[_index]);
            if (_bounded && Compare(entry.Key, _toExclusive) >= 0)
                return Finish();

            _current = entry;
            return true;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first entry.
        /// </summary>
        public void Reset()
        {
            _depth = 0;
            _leaf = null;
            _index = 0;
            _finished = false;
            _current = default;
        }

        /// <summary>
        /// Releases the resources used by the enumerator. The enumerator holds none.
        /// </summary>
        public readonly void Dispose()
        {
        }

        private bool Finish()
        {
            _finished = true;
            _current = default;
            return false;
        }

        // The first descent: down the leftmost path, or down the path to the range's lower bound. The leaf it
        // reaches can hold nothing at or after the bound — then MoveNext steps on to the next leaf, whose keys
        // all order at or after the separator the descent passed on its right.
        private void Seek()
        {
            Node node = _root;
            _depth = 0;
            while (node.Children is { } children)
            {
                int child = _bounded ? ChildIndex(node, _from) : 0;
                _path[_depth] = node;
                _cursors[_depth] = child;
                _depth++;
                node = children[child];
            }

            _leaf = node;
            if (_bounded)
            {
                int index = FindInLeaf(node, _from);
                _index = index >= 0 ? index : ~index;
            }
            else
            {
                _index = 0;
            }
        }

        // Climbs to the deepest branch with a child right of the path, steps into it and runs down its leftmost
        // edge. False when the path was the rightmost one.
        private bool NextLeaf()
        {
            int level = _depth - 1;
            while (level >= 0 && _cursors[level] + 1 >= _path[level].Count)
                level--;

            if (level < 0)
                return false;

            int child = ++_cursors[level];
            Node node = _path[level].Children![child];
            for (level++; node.Children is { } children; level++)
            {
                _path[level] = node;
                _cursors[level] = 0;
                node = children[0];
            }

            _leaf = node;
            _index = 0;
            return true;
        }
    }

    // The enumerator's inline descent stacks. MaxDepth bounds every tree int.MaxValue entries can build, so
    // these can never overflow.
    [InlineArray(MaxDepth)]
    private struct NodeStack
    {
        private Node _element0;
    }

    [InlineArray(MaxDepth)]
    private struct CursorStack
    {
        private int _element0;
    }
}
