using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable hash map</b> backed by a <b>CHAMP</b> trie (Compressed Hash-Array Mapped Prefix-tree):
/// an <b>edit</b> returns a new map that <b>shares</b> all but one root-to-leaf path of the old one's
/// storage — while a write that changes nothing hands back the receiver — and a lookup is one
/// popcount-indexed array read per level over a 32-way trie.
/// </summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <typeparam name="THasher">
/// The hash provider. Must be a struct so the JIT devirtualizes the hash call.
/// </typeparam>
/// <remarks>
/// <para>
/// <c>System.Collections.Immutable.ImmutableDictionary&lt;TKey, TValue&gt;</c> is an <b>AVL tree keyed by hash
/// code</b>, with one heap node per entry carrying key, value, hash, left child, right child and a height
/// field. Its branching factor is two, so a lookup is a pointer chase per level — about seventeen node
/// dereferences at 100,000 entries, each one a likely cache miss before the key comparison even happens — and
/// a <c>SetItem</c> path-copies and rebalances that same depth.
/// </para>
/// <para>
/// <see cref="PersistentHashMap{TKey, TValue, THasher}"/> is the structure the functional languages reach for
/// instead: the <b>CHAMP</b> trie of Steindorfer and Vinju (OOPSLA 2015). It refines the hash-array mapped
/// trie of Bagwell that Clojure's <c>PersistentHashMap</c> is built on — that one interleaves entries and
/// sub-nodes in a single array under a single bitmap, and carries a distinct collision-node type — and it is
/// the layout Scala's <c>HashMap</c> adopted in 2.13. A node reads five bits of
/// the hash, so the branching factor is <b>32</b> and 100,000 entries are four levels deep rather than
/// seventeen. Entries live <b>inline in flat arrays</b> inside the node rather than in a heap node of their
/// own — a node holding <c>k</c> entries is three objects, not <c>k</c> — and the slot an entry occupies is
/// found by a <see cref="BitOperations.PopCount(uint)"/> over a 32-bit occupancy map, so a level costs one
/// popcount and one array load.
/// </para>
/// <para>
/// The documented BCL-beating workload is the one <c>ImmutableDictionary</c> is reached for and loses at: a
/// map <b>read far more often than it is written, that must still be updatable without copying</b> — a
/// configuration or feature-flag snapshot swapped atomically and read on every request, a symbol table
/// threaded through a compiler pass, an interpreter environment, or any per-version state handed to readers
/// on another thread. Measured figures are in
/// <see href="https://github.com/marius-bughiu/Celerity/blob/main/docs/api/collections.md">the API reference</see>.
/// </para>
/// <para>
/// <b>Concurrent readers need no synchronization</b>, for the reason a value needs none: no published map is
/// ever mutated after its constructor returns, so there is no state for two threads to race over. It shares
/// that with <see cref="PersistentVector{T}"/> and the library's build-once types, and differs from the
/// mutable dictionaries in the same way: an edit produces another map rather than changing this one. A shared
/// <i>variable</i> holding successive maps still needs the usual publication rules — and the guarantee is
/// about the map's <i>own</i> state, with the same caveat
/// <see cref="IntervalTree{TKey, TValue, TComparer}"/> and <see cref="SparseTable{T, TMonoid}"/> carry for
/// their callbacks: every lookup calls <typeparamref name="THasher"/> and then
/// <see cref="EqualityComparer{T}"/>.<c>Default.Equals</c> on <typeparamref name="TKey"/>, and
/// <see cref="ContainsValue"/> calls it on <typeparamref name="TValue"/> — so a hasher, a key, or a value
/// whose own <c>Equals</c> / <c>GetHashCode</c> is not itself thread-safe makes concurrent reads unsafe
/// however immutable the map is. Every hasher in <c>Celerity.Hashing</c> is a stateless struct and ordinary
/// keys and values compare without side effects, so the usual case is safe; a stateful one is the caller's to
/// reason about.
/// </para>
/// <para>
/// <b>The <c>default(TKey)</c> entry is held out of band.</b> As in
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/> and the rest of the family, the key
/// <c>default(TKey)</c> — <c>null</c> for a reference type, <c>0</c> for an integer — lives in a dedicated
/// slot on the map rather than in the trie, and is never handed to <typeparamref name="THasher"/>. That is
/// what lets a <c>null</c> key work with a hasher that rejects one — every string hasher in
/// <c>Celerity.Hashing</c> throws <see cref="ArgumentNullException"/> on a <c>null</c> key, since a hash of
/// its characters has nothing to read.
/// </para>
/// <para>
/// Two deliberate omissions, both mirroring <see cref="PersistentVector{T}"/>. There is no <c>Clear()</c>:
/// everywhere else in this library <c>Clear()</c> means in-place mutation, so the empty map is spelled
/// <see cref="Empty"/>. And this type does not implement <c>IImmutableDictionary&lt;TKey, TValue&gt;</c>,
/// because two of that interface's members contradict decisions taken above it: it requires a
/// <c>Clear()</c>, which is the name this library reserves for in-place mutation, and its <c>Add</c> returns
/// the receiver when the key is already present with an equal value, where this one throws as
/// <see cref="Dictionary{TKey, TValue}.Add(TKey, TValue)"/> does. Implementing the interface would mean
/// shipping those semantics under names this type already gives different ones. It implements
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/>, which asks for nothing it does not already do.
/// </para>
/// </remarks>
public sealed class PersistentHashMap<TKey, TValue, THasher> : IReadOnlyDictionary<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
{
    // The trie is 32-way: a hash contributes five bits per level, and a node's occupancy is a 32-bit map.
    private const int BranchBits = 5;
    private const int BranchFactor = 1 << BranchBits;
    private const int BranchMask = BranchFactor - 1;

    // Width of the hash the descent consumes. Levels sit at shift 0, 5, 10, 15, 20, 25 and 30 — the last of
    // them reading only the top two bits — so a shift at or past this has consumed the whole hash and the
    // node it names is a collision node. Two keys can only reach one by agreeing on all 32 bits.
    private const int HashBits = 32;

    // Deepest path the enumerator can walk: seven bitmap levels plus the collision node under the last.
    private const int MaxDepth = HashBits / BranchBits + 2;

    private static readonly TKey[] NoKeys = Array.Empty<TKey>();
    private static readonly TValue?[] NoValues = Array.Empty<TValue?>();
    private static readonly Node[] NoNodes = Array.Empty<Node>();

    /// <summary>
    /// The empty map. <see cref="Remove"/> of the last entry returns this instance, so draining a map one key
    /// at a time ends at the same object every empty map starts from.
    /// </summary>
    public static readonly PersistentHashMap<TKey, TValue, THasher> Empty =
        new(new Node(0, 0, NoKeys, NoValues, NoNodes, owner: null), 0, hasDefaultKey: false, default);

    private readonly Node _root;
    private readonly int _count;

    // The out-of-band default(TKey) entry. Held here rather than in the trie so the hasher never sees a null
    // key; _count includes it when _hasDefaultKey is true.
    private readonly bool _hasDefaultKey;
    private readonly TValue? _defaultKeyValue;

    private PersistentHashMap(Node root, int count, bool hasDefaultKey, TValue? defaultKeyValue)
    {
        _root = root;
        _count = count;
        _hasDefaultKey = hasDefaultKey;
        _defaultKeyValue = defaultKeyValue;
    }

    /// <summary>
    /// Initializes a new map containing the entries of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The entries to copy into the map.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> contains duplicate keys.</exception>
    /// <remarks>
    /// The entries are inserted through a <see cref="Builder"/>, so no intermediate map is allocated per
    /// entry and no root-to-leaf path is copied per entry either.
    /// </remarks>
    public PersistentHashMap(IEnumerable<KeyValuePair<TKey, TValue>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var builder = new Builder();
        foreach (KeyValuePair<TKey, TValue> entry in source)
            builder.Add(entry.Key, entry.Value);

        PersistentHashMap<TKey, TValue, THasher> built = builder.ToImmutable();
        _root = built._root;
        _count = built._count;
        _hasDefaultKey = built._hasDefaultKey;
        _defaultKeyValue = built._defaultKeyValue;
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
    /// Gets an enumerable view over the keys in the map. The view is a lightweight struct and iterating it
    /// does not allocate.
    /// </summary>
    public KeyCollection Keys => new(this);

    /// <summary>
    /// Gets an enumerable view over the values in the map. The view is a lightweight struct and iterating it
    /// does not allocate.
    /// </summary>
    public ValueCollection Values => new(this);

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
    /// Unlike <see cref="ContainsKey"/>, this is a full scan: <c>O(n)</c>, because the trie is indexed by key
    /// and nothing about a value says where it lives. The out-of-band <c>default(TKey)</c> entry needs no
    /// special case here — the enumerator yields it, so testing it separately would compare it twice on a
    /// miss, and <typeparamref name="TValue"/>'s equality is the caller's code.
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
    /// When this returns <c>true</c>, the value stored under <paramref name="key"/>; otherwise
    /// <c>default</c>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// One popcount and one array load per level, over a 32-way trie: four levels at 100,000 entries and
    /// never more than eight. The one exception is a key whose <b>whole 32-bit hash</b> is shared with
    /// others: those live together in a collision node, which is scanned linearly and is not bounded at 32
    /// entries. That is a property of the hash, not of the map — a hasher that spreads keys never builds one.
    /// </remarks>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        if (IsDefaultKey(key))
        {
            value = _hasDefaultKey ? _defaultKeyValue : default;
            return _hasDefaultKey;
        }

        return TryFind(_root, HashOf(key), key, out value);
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
    /// <c>ImmutableDictionary&lt;TKey, TValue&gt;.Add</c>, which tolerates a duplicate key whose value is
    /// equal to the one already stored. Use <see cref="SetItem"/> for insert-or-overwrite.
    /// </remarks>
    public PersistentHashMap<TKey, TValue, THasher> Add(TKey key, TValue value)
    {
        PersistentHashMap<TKey, TValue, THasher> result = Put(key, value, overwrite: false, out bool added);
        if (!added)
            throw new ArgumentException($"An entry with the key '{key}' already exists.", nameof(key));

        return result;
    }

    /// <summary>
    /// Returns a map with <paramref name="key"/> mapped to <paramref name="value"/>, whether or not the key
    /// was already present. This map is unchanged.
    /// </summary>
    /// <param name="key">The key to insert or overwrite.</param>
    /// <param name="value">The value to store under it.</param>
    /// <returns>
    /// A map sharing this one's storage outside the affected root-to-leaf path, or this map when
    /// <paramref name="key"/> already maps to a value equal to <paramref name="value"/>.
    /// </returns>
    /// <remarks>
    /// Copying is confined to the root-to-leaf path: at most eight nodes, seven of which hold at most 32
    /// entries each. Every other node is shared with this map. The eighth is the optional collision node, and
    /// it is the one unbounded case — overwriting a key that shares its whole 32-bit hash with <c>k</c>
    /// others rebuilds an array of <c>k + 1</c>.
    /// </remarks>
    public PersistentHashMap<TKey, TValue, THasher> SetItem(TKey key, TValue value) =>
        Put(key, value, overwrite: true, out _);

    /// <summary>
    /// Returns a map with every entry of <paramref name="items"/> inserted or overwritten. This map is
    /// unchanged.
    /// </summary>
    /// <param name="items">The entries to insert or overwrite.</param>
    /// <returns>
    /// A map holding this one's entries updated by <paramref name="items"/>, or this map when nothing
    /// changed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The updates run through one <see cref="Builder"/>, so <c>m</c> entries allocate one intermediate map
    /// rather than <c>m</c> of them, and later entries in <paramref name="items"/> win over earlier ones.
    /// </remarks>
    public PersistentHashMap<TKey, TValue, THasher> SetItems(IEnumerable<KeyValuePair<TKey, TValue>> items)
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
    /// A removal that leaves a sub-node holding a single entry <b>inlines</b> that entry into the parent, so
    /// the trie never keeps a level it no longer needs — the map that results from adding <c>n</c> keys and
    /// removing one is shaped exactly like the map built from the remaining <c>n - 1</c>.
    /// </remarks>
    public PersistentHashMap<TKey, TValue, THasher> Remove(TKey key)
    {
        if (IsDefaultKey(key))
        {
            if (!_hasDefaultKey)
                return this;

            return _count == 1
                ? Empty
                : new PersistentHashMap<TKey, TValue, THasher>(_root, _count - 1, hasDefaultKey: false, default);
        }

        bool removed = false;
        Node newRoot = RemoveFrom(_root, 0, HashOf(key), key, owner: null, ref removed);
        if (!removed)
            return this;

        return _count == 1
            ? Empty
            : new PersistentHashMap<TKey, TValue, THasher>(newRoot, _count - 1, _hasDefaultKey, _defaultKeyValue);
    }

    /// <summary>
    /// Returns a map with every key in <paramref name="keys"/> removed. This map is unchanged.
    /// </summary>
    /// <param name="keys">The keys to remove. Keys that are absent are ignored.</param>
    /// <returns>A map without those keys, or this map when none of them was present.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="keys"/> is <c>null</c>.</exception>
    public PersistentHashMap<TKey, TValue, THasher> RemoveRange(IEnumerable<TKey> keys)
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
    /// Returns an enumerator over the map's entries. The order is unspecified and may change across versions;
    /// do not rely on it. The out-of-band <c>default(TKey)</c> entry, when present, is yielded first.
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

    // ── The trie ──────────────────────────────────────────────────────────────────────────────────────

    // The hasher is stateless and constrained to a struct, so `default(THasher)` is the same value a field
    // would hold and the JIT devirtualizes the call through it identically. Taken here rather than from an
    // instance field because every node operation below is static.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashOf(TKey key) => default(THasher).Hash(key);

    // Routed through the shared helper rather than spelled EqualityComparer<TKey>.Default.Equals(key,
    // default) inline: under a __Canon-shared instantiation that call stays a real interface dispatch, and
    // EmptySlot.Is compiles to a plain null test for a reference key. Same substitution the twelve
    // open-addressed collections use, pinned by ReferenceKeyProbeTests.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDefaultKey(TKey key) => EmptySlot.Is(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool KeyEquals(TKey left, TKey right) => EqualityComparer<TKey>.Default.Equals(left, right);

    // The five hash bits this level reads. Unsigned so the top level shifts in zeros rather than sign bits;
    // at shift 30 only two bits remain, so that level uses four of its 32 slots.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Mask(int hash, int shift) => (int)(((uint)hash >> shift) & BranchMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Bit(int index) => 1 << index;

    // The dense array position a set bit occupies: how many occupied slots precede it. This is what makes a
    // node's arrays exactly as long as the number of entries it holds.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SlotOf(int bitmap, int bit) => BitOperations.PopCount((uint)(bitmap & (bit - 1)));

    // Iterative descent. A data slot is conclusive either way: if the key that lives there is not the one
    // being looked for, no other slot can hold it.
    private static bool TryFind(Node node, int hash, TKey key, out TValue? value)
    {
        for (int shift = 0; ; shift += BranchBits)
        {
            if (shift >= HashBits)
                return TryFindInCollision(node, key, out value);

            int bit = Bit(Mask(hash, shift));

            if ((node.DataMap & bit) != 0)
            {
                int slot = SlotOf(node.DataMap, bit);
                if (KeyEquals(node.Keys[slot], key))
                {
                    value = node.Values[slot];
                    return true;
                }

                value = default;
                return false;
            }

            if ((node.NodeMap & bit) == 0)
            {
                value = default;
                return false;
            }

            node = node.Nodes[SlotOf(node.NodeMap, bit)];
        }
    }

    private static bool TryFindInCollision(Node node, TKey key, out TValue? value)
    {
        for (int i = 0; i < node.Keys.Length; i++)
        {
            if (KeyEquals(node.Keys[i], key))
            {
                value = node.Values[i];
                return true;
            }
        }

        value = default;
        return false;
    }

    // Insert at the map level, threading the out-of-band default-key slot. `overwrite` false makes an
    // existing key a no-op rather than a replacement, so Add can report the duplicate without having written
    // anything; `added` reports whether the key was new, which is what Add turns into its exception.
    private PersistentHashMap<TKey, TValue, THasher> Put(
        TKey key, TValue? value, bool overwrite, out bool added)
    {
        if (IsDefaultKey(key))
        {
            added = !_hasDefaultKey;
            if (!added && (!overwrite || EqualityComparer<TValue?>.Default.Equals(_defaultKeyValue, value)))
                return this;

            return new PersistentHashMap<TKey, TValue, THasher>(
                _root, added ? _count + 1 : _count, hasDefaultKey: true, value);
        }

        added = false;
        bool changed = false;
        Node newRoot = PutInto(_root, 0, HashOf(key), key, value, overwrite, owner: null, ref added, ref changed);
        if (!changed)
            return this;

        return new PersistentHashMap<TKey, TValue, THasher>(
            newRoot, added ? _count + 1 : _count, _hasDefaultKey, _defaultKeyValue);
    }

    // Insert into the trie. `owner` is the builder's ownership token, or null for a persistent write; a node
    // the token owns is edited in place instead of copied, which is what spares a builder the root-to-leaf
    // path copy on every write after the first down a given path. The node the entry lands in still rebuilds
    // its own key and value arrays. `overwrite` false leaves an existing key untouched, which is what makes a
    // rejected duplicate Add change nothing at all.
    private static Node PutInto(
        Node node, int shift, int hash, TKey key, TValue? value, bool overwrite, object? owner,
        ref bool added, ref bool changed)
    {
        if (shift >= HashBits)
            return PutIntoCollision(node, key, value, overwrite, owner, ref added, ref changed);

        int bit = Bit(Mask(hash, shift));

        if ((node.DataMap & bit) != 0)
        {
            int slot = SlotOf(node.DataMap, bit);
            TKey resident = node.Keys[slot];

            if (KeyEquals(resident, key))
            {
                if (!overwrite || EqualityComparer<TValue?>.Default.Equals(node.Values[slot], value))
                    return node;

                changed = true;
                return node.WithValue(slot, value, owner);
            }

            // Two keys want the same slot: push both down into a fresh sub-trie deep enough to tell them
            // apart, which is a collision node when they agree on all 32 bits.
            added = true;
            changed = true;
            Node merged = MergeEntries(
                shift + BranchBits, HashOf(resident), resident, node.Values[slot], hash, key, value, owner);
            return node.MigrateDataToNode(bit, slot, merged, owner);
        }

        if ((node.NodeMap & bit) != 0)
        {
            int slot = SlotOf(node.NodeMap, bit);
            Node child = node.Nodes[slot];
            Node newChild = PutInto(
                child, shift + BranchBits, hash, key, value, overwrite, owner, ref added, ref changed);
            return changed ? node.WithNode(slot, newChild, owner) : node;
        }

        added = true;
        changed = true;
        return node.InsertData(bit, SlotOf(node.DataMap, bit), key, value, owner);
    }

    private static Node PutIntoCollision(
        Node node, TKey key, TValue? value, bool overwrite, object? owner, ref bool added, ref bool changed)
    {
        for (int i = 0; i < node.Keys.Length; i++)
        {
            if (!KeyEquals(node.Keys[i], key))
                continue;

            if (!overwrite || EqualityComparer<TValue?>.Default.Equals(node.Values[i], value))
                return node;

            changed = true;
            return node.WithValue(i, value, owner);
        }

        added = true;
        changed = true;
        return node.AppendCollisionEntry(key, value, owner);
    }

    // Builds the sub-trie that separates two entries, starting at `shift`. Recurses one level per shared
    // five-bit chunk; a shift past the hash width means the two hashes are equal in all 32 bits, which is the
    // only way a collision node is ever created.
    private static Node MergeEntries(
        int shift, int hash1, TKey key1, TValue? value1, int hash2, TKey key2, TValue? value2, object? owner)
    {
        if (shift >= HashBits)
            return new Node(0, 0, [key1, key2], [value1, value2], NoNodes, owner);

        int index1 = Mask(hash1, shift);
        int index2 = Mask(hash2, shift);

        if (index1 == index2)
        {
            Node child = MergeEntries(shift + BranchBits, hash1, key1, value1, hash2, key2, value2, owner);
            return new Node(0, Bit(index1), NoKeys, NoValues, [child], owner);
        }

        int dataMap = Bit(index1) | Bit(index2);
        return index1 < index2
            ? new Node(dataMap, 0, [key1, key2], [value1, value2], NoNodes, owner)
            : new Node(dataMap, 0, [key2, key1], [value2, value1], NoNodes, owner);
    }

    // Removal from the trie. Returns `node` itself when the key was absent, which is what lets Remove hand
    // back the receiver rather than an equal copy.
    private static Node RemoveFrom(Node node, int shift, int hash, TKey key, object? owner, ref bool removed)
    {
        if (shift >= HashBits)
            return RemoveFromCollision(node, key, owner, ref removed);

        int bit = Bit(Mask(hash, shift));

        if ((node.DataMap & bit) != 0)
        {
            int slot = SlotOf(node.DataMap, bit);
            if (!KeyEquals(node.Keys[slot], key))
                return node;

            removed = true;
            return node.RemoveData(bit, slot, owner);
        }

        if ((node.NodeMap & bit) == 0)
            return node;

        int nodeSlot = SlotOf(node.NodeMap, bit);
        Node newChild = RemoveFrom(node.Nodes[nodeSlot], shift + BranchBits, hash, key, owner, ref removed);
        if (!removed)
            return node;

        // A child left holding one entry and no children of its own is dissolved into this node. Doing it at
        // every level on the way back up is what keeps the trie canonical: the entry can never be stranded
        // below a node that exists only to reach it, and the collapse propagates upward on its own, since a
        // parent that inlines an entry is itself re-tested by *its* parent.
        if (newChild.IsSingleEntry)
            return node.MigrateNodeToData(bit, nodeSlot, newChild.Keys[0], newChild.Values[0], owner);

        return node.WithNode(nodeSlot, newChild, owner);
    }

    private static Node RemoveFromCollision(Node node, TKey key, object? owner, ref bool removed)
    {
        for (int i = 0; i < node.Keys.Length; i++)
        {
            if (!KeyEquals(node.Keys[i], key))
                continue;

            removed = true;
            return node.RemoveCollisionEntry(i, owner);
        }

        return node;
    }

    // A CHAMP node. Two 32-bit occupancy maps say what each of the 32 slots holds — an inline entry
    // (DataMap), a sub-node (NodeMap), or nothing — and the payload arrays are exactly as long as the number
    // of slots of each kind, addressed by the popcount of the bits below.
    //
    // A collision node is the same class with both maps zero and Keys holding every entry that shares one
    // 32-bit hash. It needs no marker: it can only sit under seven levels of descent, so the caller's shift
    // already says which shape it is looking at. That also makes enumeration blind to the difference, since
    // walking Keys then Nodes is correct for both.
    //
    // The fields are mutable so a builder can edit a node it owns in place. Nothing published ever is: a node
    // is only written to while Owner is the token of the builder that made it, and ToImmutable takes a fresh
    // token, so every node the builder has handed out becomes read-only from that moment.
    private sealed class Node
    {
        internal int DataMap;
        internal int NodeMap;
        internal TKey[] Keys;
        internal TValue?[] Values;
        internal Node[] Nodes;
        internal object? Owner;

        internal Node(int dataMap, int nodeMap, TKey[] keys, TValue?[] values, Node[] nodes, object? owner)
        {
            DataMap = dataMap;
            NodeMap = nodeMap;
            Keys = keys;
            Values = values;
            Nodes = nodes;
            Owner = owner;
        }

        // True for a node holding exactly one entry and no children — the shape a parent dissolves. Reads
        // Keys/Nodes rather than the bitmaps so it answers for a collision node too.
        internal bool IsSingleEntry => Keys.Length == 1 && Nodes.Length == 0;

        // A node with the same shape and one value replaced. The owned path writes the array in place, which
        // is sound because an owned node's arrays are its own — see Fork.
        internal Node WithValue(int slot, TValue? value, object? owner)
        {
            if (IsOwnedBy(owner))
            {
                Values[slot] = value;
                return this;
            }

            TValue?[] values = Clone(Values);
            values[slot] = value;
            return Fork(DataMap, NodeMap, Keys, values, Nodes, owner);
        }

        internal Node WithNode(int slot, Node child, object? owner)
        {
            if (IsOwnedBy(owner))
            {
                Nodes[slot] = child;
                return this;
            }

            Node[] nodes = Clone(Nodes);
            nodes[slot] = child;
            return Fork(DataMap, NodeMap, Keys, Values, nodes, owner);
        }

        internal Node InsertData(int bit, int slot, TKey key, TValue? value, object? owner) =>
            Fork(DataMap | bit, NodeMap, InsertAt(Keys, slot, key), InsertAt(Values, slot, value), Nodes, owner);

        internal Node RemoveData(int bit, int slot, object? owner) =>
            Fork(DataMap ^ bit, NodeMap, RemoveAt(Keys, slot), RemoveAt(Values, slot), Nodes, owner);

        // The entry at `slot` becomes a sub-node at the same bit: the bit moves from DataMap to NodeMap.
        internal Node MigrateDataToNode(int bit, int slot, Node child, object? owner)
        {
            TKey[] keys = RemoveAt(Keys, slot);
            TValue?[] values = RemoveAt(Values, slot);
            Node[] nodes = InsertAt(Nodes, SlotOf(NodeMap, bit), child);
            return Fork(DataMap ^ bit, NodeMap | bit, keys, values, nodes, owner);
        }

        // The reverse: a sub-node that has shrunk to one entry gives it up and the bit moves back to DataMap.
        internal Node MigrateNodeToData(int bit, int nodeSlot, TKey key, TValue? value, object? owner)
        {
            Node[] nodes = RemoveAt(Nodes, nodeSlot);
            int dataSlot = SlotOf(DataMap, bit);
            TKey[] keys = InsertAt(Keys, dataSlot, key);
            TValue?[] values = InsertAt(Values, dataSlot, value);
            return Fork(DataMap | bit, NodeMap ^ bit, keys, values, nodes, owner);
        }

        internal Node AppendCollisionEntry(TKey key, TValue? value, object? owner) =>
            Fork(0, 0, InsertAt(Keys, Keys.Length, key), InsertAt(Values, Values.Length, value), Nodes, owner);

        internal Node RemoveCollisionEntry(int slot, object? owner) =>
            Fork(0, 0, RemoveAt(Keys, slot), RemoveAt(Values, slot), Nodes, owner);

        // A node is editable only while it carries the *current* token of the builder writing to it. A
        // persistent write passes no token, and a node built persistently carries none, so the null-null case
        // must not read as ownership — hence the explicit null test rather than a bare reference comparison.
        private bool IsOwnedBy(object? owner) => owner is not null && ReferenceEquals(Owner, owner);

        // Takes the rebuilt arrays, in place when this node is the writer's own and into a fresh node
        // otherwise. What ownership saves is the node allocation and, above it, the path copy.
        //
        // The load-bearing rule is the second branch: a node passing *into* a builder's ownership may not
        // keep an array it still shares with the node it was forked from, or a later in-place write through
        // WithValue / WithNode would reach through into storage a published map is still reading. Every array
        // an owned node holds is therefore either freshly built by the caller above or cloned here.
        private Node Fork(int dataMap, int nodeMap, TKey[] keys, TValue?[] values, Node[] nodes, object? owner)
        {
            if (IsOwnedBy(owner))
            {
                DataMap = dataMap;
                NodeMap = nodeMap;
                Keys = keys;
                Values = values;
                Nodes = nodes;
                return this;
            }

            if (owner is not null)
            {
                if (ReferenceEquals(keys, Keys))
                    keys = Clone(keys);

                if (ReferenceEquals(values, Values))
                    values = Clone(values);

                if (ReferenceEquals(nodes, Nodes))
                    nodes = Clone(nodes);
            }

            return new Node(dataMap, nodeMap, keys, values, nodes, owner);
        }

        // An empty array is handed back as-is: there is no slot to write, so no aliasing of one can be
        // observed, and the static NoKeys / NoValues / NoNodes singletons stay singletons.
        private static T[] Clone<T>(T[] source)
        {
            if (source.Length == 0)
                return source;

            var copy = new T[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static T[] InsertAt<T>(T[] source, int index, T item)
        {
            var copy = new T[source.Length + 1];
            Array.Copy(source, copy, index);
            copy[index] = item;
            Array.Copy(source, index, copy, index + 1, source.Length - index);
            return copy;
        }

        private static T[] RemoveAt<T>(T[] source, int index)
        {
            var copy = new T[source.Length - 1];
            Array.Copy(source, copy, index);
            Array.Copy(source, index + 1, copy, index, source.Length - index - 1);
            return copy;
        }
    }

    /// <summary>
    /// A mutable accumulator that produces <see cref="PersistentHashMap{TKey, TValue, THasher}"/> instances
    /// without allocating one per change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The builder stamps every node it creates with an ownership token and writes such a node in place
    /// rather than copying it. The saving is the <b>ancestors</b>, and it is amortized rather than fixed: the
    /// first write down a path it does not yet own still forks every node on that path (cloning the payload
    /// arrays, so no owned node shares one with a published map), and every write after that reuses them in
    /// place. The node the entry actually lands in is rebuilt either way — an insert resizes both its key and
    /// its value array — so what the token removes is the fresh node per level from the root down, not the
    /// payload copy itself. <see cref="ToImmutable"/> takes a <i>new</i> token, which
    /// makes every node the builder has handed out read-only again in one assignment — so the builder stays
    /// usable afterwards and no map it has produced can be changed behind a caller's back.
    /// </para>
    /// <para>
    /// The builder is not thread-safe; the maps it produces are.
    /// </para>
    /// </remarks>
    public sealed class Builder
    {
        private object _owner;
        private Node _root;
        private int _count;
        private bool _hasDefaultKey;
        private TValue? _defaultKeyValue;

        /// <summary>
        /// Initializes a new, empty builder.
        /// </summary>
        public Builder()
            : this(Empty)
        {
        }

        internal Builder(PersistentHashMap<TKey, TValue, THasher> source)
        {
            _owner = new object();
            _root = source._root;
            _count = source._count;
            _hasDefaultKey = source._hasDefaultKey;
            _defaultKeyValue = source._defaultKeyValue;
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
        /// <exception cref="KeyNotFoundException">
        /// On a read, <paramref name="key"/> is not present.
        /// </exception>
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
        public bool TryGetValue(TKey key, out TValue? value)
        {
            if (IsDefaultKey(key))
            {
                value = _hasDefaultKey ? _defaultKeyValue : default;
                return _hasDefaultKey;
            }

            return TryFind(_root, HashOf(key), key, out value);
        }

        /// <summary>
        /// Inserts <paramref name="key"/>, requiring it to be absent.
        /// </summary>
        /// <param name="key">The key to insert.</param>
        /// <param name="value">The value to store under it.</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is already present.</exception>
        public void Add(TKey key, TValue value)
        {
            // overwrite: false is what makes the rejection clean — a duplicate leaves the builder exactly as
            // it was, rather than replacing the value and then throwing.
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
            if (IsDefaultKey(key))
            {
                if (!_hasDefaultKey)
                    return false;

                _hasDefaultKey = false;
                _defaultKeyValue = default;
                _count--;
                Mutated = true;
                return true;
            }

            bool removed = false;
            _root = RemoveFrom(_root, 0, HashOf(key), key, _owner, ref removed);
            if (!removed)
                return false;

            _count--;
            Mutated = true;
            return true;
        }

        /// <summary>
        /// Produces an immutable map holding the builder's current entries. The builder stays usable, and
        /// further changes to it do not affect any map it has already returned.
        /// </summary>
        /// <returns>A map holding the builder's entries.</returns>
        public PersistentHashMap<TKey, TValue, THasher> ToImmutable()
        {
            // Every node made under the old token becomes read-only the moment the builder stops recognizing
            // it, which is what makes handing out the trie by reference safe.
            _owner = new object();

            return _count == 0
                ? Empty
                : new PersistentHashMap<TKey, TValue, THasher>(_root, _count, _hasDefaultKey, _defaultKeyValue);
        }

        // Insert, reporting whether the key was new. `overwrite` false leaves an existing key untouched, so
        // Add can reject a duplicate without having changed the builder. Internal because SetItems needs the
        // nullable-tolerant form the public indexer cannot express: an indexer has one type for get and set,
        // and the get is pinned to the family's non-nullable TValue.
        internal bool Put(TKey key, TValue? value, bool overwrite)
        {
            if (IsDefaultKey(key))
            {
                bool isNew = !_hasDefaultKey;
                if (!isNew && (!overwrite || EqualityComparer<TValue?>.Default.Equals(_defaultKeyValue, value)))
                    return false;

                _hasDefaultKey = true;
                _defaultKeyValue = value;
                Mutated = true;
                if (isNew)
                    _count++;

                return isNew;
            }

            bool added = false;
            bool changed = false;
            _root = PutInto(_root, 0, HashOf(key), key, value, overwrite, _owner, ref added, ref changed);
            if (changed)
                Mutated = true;

            if (added)
                _count++;

            return added;
        }
    }

    /// <summary>
    /// A struct enumerable view over the keys of a
    /// <see cref="PersistentHashMap{TKey, TValue, THasher}"/>. Iterating it does not allocate; passing it
    /// through <see cref="IEnumerable{T}"/> will box the enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct KeyCollection : IEnumerable<TKey>
    {
        private readonly PersistentHashMap<TKey, TValue, THasher> _map;

        internal KeyCollection(PersistentHashMap<TKey, TValue, THasher> map) => _map = map;

        /// <summary>
        /// Gets the number of keys in the view (equal to the map's count).
        /// </summary>
        public int Count => _map._count;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the keys.
        /// </summary>
        /// <returns>A struct enumerator over the keys.</returns>
        public Enumerator GetEnumerator() => new(_map);

        /// <inheritdoc/>
        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => new Enumerator(_map);

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_map);

        /// <summary>
        /// A struct enumerator over the keys of a
        /// <see cref="PersistentHashMap{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private PersistentHashMap<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(PersistentHashMap<TKey, TValue, THasher> map) => _inner = map.GetEnumerator();

            /// <summary>Gets the current key.</summary>
            public TKey Current => _inner.Current.Key;

            /// <inheritdoc/>
            object? IEnumerator.Current => Current;

            /// <summary>Advances to the next key.</summary>
            /// <returns><c>true</c> if there is another key; otherwise <c>false</c>.</returns>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>Releases the resources used by the enumerator. The enumerator holds none.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }

    /// <summary>
    /// A struct enumerable view over the values of a
    /// <see cref="PersistentHashMap{TKey, TValue, THasher}"/>. Iterating it does not allocate; passing it
    /// through <see cref="IEnumerable{T}"/> will box the enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct ValueCollection : IEnumerable<TValue?>
    {
        private readonly PersistentHashMap<TKey, TValue, THasher> _map;

        internal ValueCollection(PersistentHashMap<TKey, TValue, THasher> map) => _map = map;

        /// <summary>
        /// Gets the number of values in the view (equal to the map's count).
        /// </summary>
        public int Count => _map._count;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the values.
        /// </summary>
        /// <returns>A struct enumerator over the values.</returns>
        public Enumerator GetEnumerator() => new(_map);

        /// <inheritdoc/>
        IEnumerator<TValue?> IEnumerable<TValue?>.GetEnumerator() => new Enumerator(_map);

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_map);

        /// <summary>
        /// A struct enumerator over the values of a
        /// <see cref="PersistentHashMap{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private PersistentHashMap<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(PersistentHashMap<TKey, TValue, THasher> map) => _inner = map.GetEnumerator();

            /// <summary>Gets the current value.</summary>
            public TValue? Current => _inner.Current.Value;

            /// <inheritdoc/>
            object? IEnumerator.Current => Current;

            /// <summary>Advances to the next value.</summary>
            /// <returns><c>true</c> if there is another value; otherwise <c>false</c>.</returns>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>Releases the resources used by the enumerator. The enumerator holds none.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }

    /// <summary>
    /// Walks a <see cref="PersistentHashMap{TKey, TValue, THasher}"/>, yielding every entry once.
    /// </summary>
    /// <remarks>
    /// The map cannot be modified, so this enumerator carries no version check and nothing can invalidate it.
    /// The descent stack is held inline in the struct — the trie is at most eight levels deep, so its depth
    /// is a compile-time constant — which is what makes enumeration allocation-free. The out-of-band
    /// <c>default(TKey)</c> entry is yielded first when present; the rest follow in an unspecified order.
    /// </remarks>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        // Depth sentinels. They are distinct because a walk that has run out must not restart: -1 says the
        // root has not been pushed yet, -2 says it has been popped and the enumeration is over.
        private const int NotStarted = -1;
        private const int Finished = -2;

        private readonly PersistentHashMap<TKey, TValue, THasher> _map;

        // The root-to-current path and, per level, how far through that node's entries-then-children the walk
        // has got. Index 0 is the root; _depth is -1 before the first MoveNext and after the last.
        private NodeStack _path;
        private CursorStack _cursors;
        private int _depth;
        private bool _defaultKeyPending;
        private KeyValuePair<TKey, TValue?> _current;

        internal Enumerator(PersistentHashMap<TKey, TValue, THasher> map)
        {
            _map = map;
            _path = default;
            _cursors = default;
            _depth = NotStarted;
            _defaultKeyPending = map._hasDefaultKey;
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
            if (_defaultKeyPending)
            {
                // The out-of-band entry comes first, and seeding the root here rather than on the next call
                // keeps the two entry points into the walk from having to agree twice.
                _defaultKeyPending = false;
                _current = new KeyValuePair<TKey, TValue?>(default!, _map._defaultKeyValue);
                _depth = 0;
                _path[0] = _map._root;
                _cursors[0] = 0;
                return true;
            }

            if (_depth == NotStarted)
            {
                _depth = 0;
                _path[0] = _map._root;
                _cursors[0] = 0;
            }

            while (_depth >= 0)
            {
                Node node = _path[_depth];
                int cursor = _cursors[_depth]++;

                // A node's entries come first, then its children — which is correct for a collision node too,
                // since that shape holds every one of its entries in Keys and has no children at all.
                if (cursor < node.Keys.Length)
                {
                    _current = new KeyValuePair<TKey, TValue?>(node.Keys[cursor], node.Values[cursor]);
                    return true;
                }

                int childIndex = cursor - node.Keys.Length;
                if (childIndex < node.Nodes.Length)
                {
                    _depth++;
                    _path[_depth] = node.Nodes[childIndex];
                    _cursors[_depth] = 0;
                    continue;
                }

                _depth--;
            }

            _depth = Finished;
            _current = default;
            return false;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first entry.
        /// </summary>
        public void Reset()
        {
            _depth = NotStarted;
            _defaultKeyPending = _map._hasDefaultKey;
            _current = default;
        }

        /// <summary>
        /// Releases the resources used by the enumerator. The enumerator holds none.
        /// </summary>
        public readonly void Dispose()
        {
        }
    }

    // The enumerator's inline descent stacks. MaxDepth is a hard structural bound — a 32-bit hash yields
    // seven five-bit levels and one collision node below them — so these can never overflow.
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
