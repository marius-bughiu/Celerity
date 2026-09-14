using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable hash set</b> backed by a <b>CHAMP</b> trie (Compressed Hash-Array Mapped Prefix-tree):
/// a <b>single-element edit</b> returns a new set that <b>shares</b> all but one root-to-leaf path of the
/// old one's storage — while an edit that changes nothing hands back the receiver — and a membership test
/// is one popcount-indexed array read per level over a 32-way trie. (The set-algebra methods reach as many
/// branches as their operands do; they run through a <see cref="Builder"/> for that reason.)
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
/// <typeparam name="THasher">
/// The hash provider. Must be a struct so the JIT devirtualizes the hash call.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the set half of <see cref="PersistentHashMap{TKey, TValue, THasher}"/>: the same trie, with the
/// value array taken out. <c>System.Collections.Immutable.ImmutableHashSet&lt;T&gt;</c> is an <b>AVL tree
/// keyed by hash code</b>, with one heap node per distinct hash carrying the hash, a bucket, left and right
/// children and a height field. Its branching factor is two, so a membership test is a pointer chase per
/// level — about seventeen node dereferences at 100,000 elements, each one a likely cache miss before the
/// element comparison even happens — and an <c>Add</c> or <c>Remove</c> path-copies and rebalances that
/// same depth.
/// </para>
/// <para>
/// Here a node reads five bits of the hash, so the branching factor is <b>32</b> and 100,000 elements are
/// four levels deep rather than seventeen. Elements live <b>inline in a flat array</b> inside the node
/// rather than in a heap node of their own — a node holding <c>k</c> elements is three objects, not
/// <c>k</c> — and the slot an element occupies is found by a <see cref="BitOperations.PopCount(uint)"/> over
/// a 32-bit occupancy map, so a level costs one popcount and one array load.
/// </para>
/// <para>
/// The documented BCL-beating workload is the one <c>ImmutableHashSet</c> is reached for and loses at: a
/// set <b>tested far more often than it is written, that must still be updatable without copying</b> — an
/// allow-list or deny-list swapped atomically and consulted on every request, the visited set threaded
/// through a backtracking search, the set of names in scope in a compiler pass, or any per-version
/// membership state handed to readers on another thread. Measured figures are in
/// <see href="https://github.com/marius-bughiu/Celerity/blob/main/docs/api/collections.md">the API reference</see>.
/// </para>
/// <para>
/// <b>Concurrent readers need no synchronization</b>, for the reason a value needs none: no published set is
/// ever mutated after its constructor returns, so there is no state for two threads to race over. It shares
/// that with <see cref="PersistentHashMap{TKey, TValue, THasher}"/>, <see cref="PersistentVector{T}"/> and
/// the library's build-once types, and differs from the mutable sets in the same way: an edit produces
/// another set rather than changing this one. A shared <i>variable</i> holding successive sets still needs
/// the usual publication rules — and the guarantee is about the set's <i>own</i> state: every membership
/// test calls <typeparamref name="THasher"/> and then <see cref="EqualityComparer{T}"/>.<c>Default.Equals</c>
/// on <typeparamref name="T"/>, so a hasher or an element whose own <c>Equals</c> / <c>GetHashCode</c> is not
/// itself thread-safe makes concurrent reads unsafe however immutable the set is. Every hasher in
/// <c>Celerity.Hashing</c> is a stateless struct and ordinary elements compare without side effects, so the
/// usual case is safe; a stateful one is the caller's to reason about.
/// </para>
/// <para>
/// <b>The <c>default(T)</c> element is held out of band.</b> As in <see cref="CeleritySet{T, THasher}"/> and
/// the rest of the family, the element <c>default(T)</c> — <c>null</c> for a reference type, <c>0</c> for an
/// integer — is a flag on the set rather than an entry in the trie, and is never handed to
/// <typeparamref name="THasher"/>. That is what lets a <c>null</c> element work with a hasher that rejects
/// one — every string hasher in <c>Celerity.Hashing</c> throws <see cref="ArgumentNullException"/> on a
/// <c>null</c> key, since a hash of its characters has nothing to read.
/// </para>
/// <para>
/// Two deliberate omissions, both mirroring <see cref="PersistentHashMap{TKey, TValue, THasher}"/>. There is
/// no <c>Clear()</c>: everywhere else in this library <c>Clear()</c> means in-place mutation, so the empty
/// set is spelled <see cref="Empty"/>. And this type does not implement <c>IImmutableSet&lt;T&gt;</c>,
/// because that interface requires a <c>Clear()</c> returning an empty instance — the name this library
/// reserves for in-place mutation. It implements <see cref="IReadOnlySet{T}"/>, which asks for nothing it
/// does not already do.
/// </para>
/// </remarks>
public sealed class PersistentHashSet<T, THasher> : IReadOnlySet<T>
    where THasher : struct, IHashProvider<T>
{
    // The trie is 32-way: a hash contributes five bits per level, and a node's occupancy is a 32-bit map.
    private const int BranchBits = 5;
    private const int BranchFactor = 1 << BranchBits;
    private const int BranchMask = BranchFactor - 1;

    // Width of the hash the descent consumes. Levels sit at shift 0, 5, 10, 15, 20, 25 and 30 — the last of
    // them reading only the top two bits — so a shift at or past this has consumed the whole hash and the
    // node it names is a collision node. Two elements can only reach one by agreeing on all 32 bits.
    private const int HashBits = 32;

    // Deepest path the enumerator can walk: seven bitmap levels plus the collision node under the last.
    private const int MaxDepth = HashBits / BranchBits + 2;

    private static readonly T[] NoItems = Array.Empty<T>();
    private static readonly Node[] NoNodes = Array.Empty<Node>();

    /// <summary>
    /// The empty set. <see cref="Remove"/> of the last element returns this instance, so draining a set one
    /// element at a time ends at the same object every empty set starts from.
    /// </summary>
    public static readonly PersistentHashSet<T, THasher> Empty =
        new(new Node(0, 0, NoItems, NoNodes, owner: null), 0, hasDefault: false);

    private readonly Node _root;
    private readonly int _count;

    // The out-of-band default(T) element. Held here rather than in the trie so the hasher never sees a null
    // element; _count includes it when _hasDefault is true.
    private readonly bool _hasDefault;

    private PersistentHashSet(Node root, int count, bool hasDefault)
    {
        _root = root;
        _count = count;
        _hasDefault = hasDefault;
    }

    /// <summary>
    /// Initializes a new set containing the distinct elements of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The elements to copy into the set. Duplicates are ignored.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The elements are inserted through a <see cref="Builder"/>, so no intermediate set is allocated per
    /// element and no root-to-leaf path is copied per element either. Duplicates are ignored rather than
    /// rejected, as <see cref="HashSet{T}"/>'s sequence constructor does.
    /// </remarks>
    public PersistentHashSet(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var builder = new Builder();
        foreach (T item in source)
            builder.Add(item);

        PersistentHashSet<T, THasher> built = builder.ToImmutable();
        _root = built._root;
        _count = built._count;
        _hasDefault = built._hasDefault;
    }

    /// <summary>
    /// Gets the number of elements in the set.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets a value indicating whether the set contains no elements.
    /// </summary>
    public bool IsEmpty => _count == 0;

    /// <summary>
    /// Determines whether <paramref name="item"/> is in the set.
    /// </summary>
    /// <param name="item">The element to look for.</param>
    /// <returns><c>true</c> if the element is present; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// One popcount and one array load per level, over a 32-way trie: four levels at 100,000 elements and
    /// never more than eight. The one exception is an element whose <b>whole 32-bit hash</b> is shared with
    /// others: those live together in a collision node, which is scanned linearly and is not bounded at 32
    /// elements. That is a property of the hash, not of the set — a hasher that spreads elements never
    /// builds one.
    /// </remarks>
    public bool Contains(T item) => TryGetValue(item, out _);

    /// <summary>
    /// Looks up the element equal to <paramref name="equalValue"/> and returns the instance the set holds.
    /// </summary>
    /// <param name="equalValue">The value to look for.</param>
    /// <param name="actualValue">
    /// When this returns <c>true</c>, the stored element equal to <paramref name="equalValue"/>; otherwise
    /// <paramref name="equalValue"/> itself.
    /// </param>
    /// <returns><c>true</c> if an equal element is present; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// The point is to recover the <i>stored</i> instance — to canonicalize an equal-but-distinct reference,
    /// say. On a miss the argument comes back unchanged, as <c>ImmutableHashSet&lt;T&gt;.TryGetValue</c> — the
    /// type this one replaces — hands it back. <see cref="HashSet{T}.TryGetValue(T, out T)"/> writes
    /// <c>default</c> instead; returning the argument keeps a non-nullable <typeparamref name="T"/> non-null on
    /// both paths, and the return value is what says whether the element was found.
    /// </remarks>
    public bool TryGetValue(T equalValue, out T actualValue)
    {
        if (IsDefault(equalValue))
        {
            actualValue = equalValue;
            return _hasDefault;
        }

        return TryFind(_root, HashOf(equalValue), equalValue, out actualValue);
    }

    /// <summary>
    /// Returns a set with <paramref name="item"/> added. This set is unchanged.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>
    /// A set of <c>Count + 1</c> elements sharing this one's storage, or this set when
    /// <paramref name="item"/> was already present.
    /// </returns>
    /// <remarks>
    /// Copying is confined to the root-to-leaf path: at most eight nodes, seven of which hold at most 32
    /// elements each. Every other node is shared with this set. The eighth is the optional collision node,
    /// and it is the one unbounded case — adding an element that shares its whole 32-bit hash with <c>k</c>
    /// others rebuilds an array of <c>k + 1</c>. Matches <c>ImmutableHashSet&lt;T&gt;.Add</c>, which also
    /// hands back the receiver for an element already present.
    /// </remarks>
    public PersistentHashSet<T, THasher> Add(T item)
    {
        ThrowIfFull(item);

        if (IsDefault(item))
            return _hasDefault ? this : new PersistentHashSet<T, THasher>(_root, _count + 1, hasDefault: true);

        bool added = false;
        Node newRoot = PutInto(_root, 0, HashOf(item), item, owner: null, ref added);
        return added ? new PersistentHashSet<T, THasher>(newRoot, _count + 1, _hasDefault) : this;
    }

    /// <summary>
    /// Returns a set with <paramref name="item"/> removed. This set is unchanged.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>
    /// A set of <c>Count - 1</c> elements sharing this one's storage, this set when
    /// <paramref name="item"/> was absent, or <see cref="Empty"/> when it was the last element.
    /// </returns>
    /// <remarks>
    /// A removal that leaves a sub-node holding a single element <b>inlines</b> that element into the parent,
    /// so the trie never keeps a level it no longer needs — the set that results from adding <c>n</c>
    /// elements and removing one is shaped exactly like the set built from the remaining <c>n - 1</c>.
    /// </remarks>
    public PersistentHashSet<T, THasher> Remove(T item)
    {
        if (IsDefault(item))
        {
            if (!_hasDefault)
                return this;

            return _count == 1 ? Empty : new PersistentHashSet<T, THasher>(_root, _count - 1, hasDefault: false);
        }

        bool removed = false;
        Node newRoot = RemoveFrom(_root, 0, HashOf(item), item, owner: null, ref removed);
        if (!removed)
            return this;

        return _count == 1 ? Empty : new PersistentHashSet<T, THasher>(newRoot, _count - 1, _hasDefault);
    }

    /// <summary>
    /// Returns a set holding every element of this set and of <paramref name="other"/>. This set is
    /// unchanged.
    /// </summary>
    /// <param name="other">The elements to add. Duplicates, and elements already present, are ignored.</param>
    /// <returns>The union, or this set when <paramref name="other"/> added nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The additions run through one <see cref="Builder"/>, so <c>m</c> elements allocate one intermediate
    /// set rather than <c>m</c> of them.
    /// </remarks>
    public PersistentHashSet<T, THasher> Union(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
            return this;

        var builder = new Builder(this);
        foreach (T item in other)
            builder.Add(item);

        return builder.Mutated ? builder.ToImmutable() : this;
    }

    /// <summary>
    /// Returns a set holding the elements of this set that are not in <paramref name="other"/>. This set is
    /// unchanged.
    /// </summary>
    /// <param name="other">The elements to remove. Elements that are absent are ignored.</param>
    /// <returns>The difference, or this set when none of <paramref name="other"/> was present.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public PersistentHashSet<T, THasher> Except(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        // Everything goes, unless there was nothing to begin with: the sequence constructor can build an empty
        // set that is not the Empty singleton, and removing nothing from it must still hand back the receiver.
        if (ReferenceEquals(this, other))
            return _count == 0 ? this : Empty;

        var builder = new Builder(this);
        foreach (T item in other)
            builder.Remove(item);

        return builder.Mutated ? builder.ToImmutable() : this;
    }

    /// <summary>
    /// Returns a set holding only the elements of this set that are also in <paramref name="other"/>. This
    /// set is unchanged.
    /// </summary>
    /// <param name="other">The elements to keep.</param>
    /// <returns>The intersection, or this set when every element of it is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Builds the result up from the elements of <paramref name="other"/> this set contains, rather than
    /// removing the rest from a copy — so the work is proportional to <paramref name="other"/>, and a small
    /// intersection of a large set costs a small set's worth of allocation. The stored instances are kept,
    /// not the equal ones <paramref name="other"/> supplied.
    /// </remarks>
    public PersistentHashSet<T, THasher> Intersect(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other) || _count == 0)
            return this;

        var builder = new Builder();
        foreach (T item in other)
        {
            if (TryGetValue(item, out T stored))
                builder.Add(stored);
        }

        // The result is a subset of this set, so an equal count means an equal set.
        return builder.Count == _count ? this : builder.ToImmutable();
    }

    /// <summary>
    /// Returns a set holding the elements that are in exactly one of this set and
    /// <paramref name="other"/>. This set is unchanged.
    /// </summary>
    /// <param name="other">The elements to toggle. A duplicate toggles once, not once per occurrence.</param>
    /// <returns>The symmetric difference, or this set when <paramref name="other"/> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public PersistentHashSet<T, THasher> SymmetricExcept(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        // Every element is in both, so none survives — and, as in Except, an empty receiver is already the
        // answer and is handed back rather than swapped for the singleton.
        if (ReferenceEquals(this, other))
            return _count == 0 ? this : Empty;

        // Materialized to its distinct elements first, so a repeated element toggles once — the same rule
        // HashSet<T>.SymmetricExceptWith follows.
        HashSet<T> distinct = MaterializeDistinct(other);

        var builder = new Builder(this);
        foreach (T item in distinct)
        {
            if (!builder.Remove(item))
                builder.Add(item);
        }

        return builder.Mutated ? builder.ToImmutable() : this;
    }

    // ── IReadOnlySet<T> queries ───────────────────────────────────────────────────────────────────────
    // The whole set is the left-hand operand; `other` is the right-hand one. Membership against `this` is a
    // short trie descent, so the superset / overlap shapes stream `other` directly. The subset / equality
    // shapes need the distinct count of `other`, so they materialize it once — exactly what the BCL set
    // types, and FrozenCeleritySet, do.

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> contain the same distinct elements.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
            return true;

        HashSet<T> distinct = MaterializeDistinct(other);
        return distinct.Count == _count && AllElementsIn(distinct);
    }

    /// <summary>
    /// Determines whether every element of the set is also in <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the set is a subset of <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_count == 0)
            return true;

        HashSet<T> distinct = MaterializeDistinct(other);
        return _count <= distinct.Count && AllElementsIn(distinct);
    }

    /// <summary>
    /// Determines whether the set is a proper (strict) subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of the set is in <paramref name="other"/> and <paramref name="other"/>
    /// has at least one element the set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        HashSet<T> distinct = MaterializeDistinct(other);
        return _count < distinct.Count && AllElementsIn(distinct);
    }

    /// <summary>
    /// Determines whether every element of <paramref name="other"/> is also in the set.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the set is a superset of <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (T item in other)
        {
            if (!Contains(item))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the set is a proper (strict) superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of <paramref name="other"/> is in the set and the set has at least one
    /// element <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        HashSet<T> distinct = MaterializeDistinct(other);
        if (_count <= distinct.Count)
            return false;

        foreach (T item in distinct)
        {
            if (!Contains(item))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> share at least one element.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool Overlaps(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_count == 0)
            return false;

        foreach (T item in other)
        {
            if (Contains(item))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a mutable builder seeded with this set's elements, for making many changes without allocating
    /// a set per change.
    /// </summary>
    /// <returns>A builder holding this set's elements.</returns>
    public Builder ToBuilder() => new(this);

    /// <summary>
    /// Returns an enumerator over the set's elements. The order is unspecified and may change across
    /// versions; do not rely on it. The out-of-band <c>default(T)</c> element, when present, is yielded first.
    /// </summary>
    /// <returns>A struct enumerator over the elements.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── The trie ──────────────────────────────────────────────────────────────────────────────────────

    // Refuses an insert that would wrap the count negative. The Contains probe runs only at the ceiling, so
    // re-adding an element already present is still allowed on a full set. Mirrors PersistentHashMap's.
    [ExcludeFromCodeCoverage(Justification = "Needs int.MaxValue elements — tens of gigabytes of trie nodes " +
        "for any element type. The check exists so the count cannot silently wrap rather than because a test " +
        "can reach it.")]
    private void ThrowIfFull(T item)
    {
        if (_count == int.MaxValue && !Contains(item))
            throw new InvalidOperationException("A set cannot hold more than int.MaxValue elements.");
    }

    // The hasher is stateless and constrained to a struct, so `default(THasher)` is the same value a field
    // would hold and the JIT devirtualizes the call through it identically. Taken here rather than from an
    // instance field because every node operation below is static.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashOf(T item) => default(THasher).Hash(item);

    // Routed through the shared helper rather than spelled EqualityComparer<T>.Default.Equals(item, default)
    // inline: under a __Canon-shared instantiation that call stays a real interface dispatch, and
    // EmptySlot.Is compiles to a plain null test for a reference element. Pinned by ReferenceKeyProbeTests.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDefault(T item) => EmptySlot.Is(item);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ItemEquals(T left, T right) => EqualityComparer<T>.Default.Equals(left, right);

    // The five hash bits this level reads. Unsigned so the top level shifts in zeros rather than sign bits;
    // at shift 30 only two bits remain, so that level uses four of its 32 slots.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Mask(int hash, int shift) => (int)(((uint)hash >> shift) & BranchMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Bit(int index) => 1 << index;

    // The dense array position a set bit occupies: how many occupied slots precede it. This is what makes a
    // node's arrays exactly as long as the number of elements it holds.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SlotOf(int bitmap, int bit) => BitOperations.PopCount((uint)(bitmap & (bit - 1)));

    // Iterative descent. A data slot is conclusive either way: if the element that lives there is not the
    // one being looked for, no other slot can hold it.
    private static bool TryFind(Node node, int hash, T item, out T actual)
    {
        for (int shift = 0; ; shift += BranchBits)
        {
            if (shift >= HashBits)
                return TryFindInCollision(node, item, out actual);

            int bit = Bit(Mask(hash, shift));

            if ((node.DataMap & bit) != 0)
            {
                T resident = node.Items[SlotOf(node.DataMap, bit)];
                if (ItemEquals(resident, item))
                {
                    actual = resident;
                    return true;
                }

                actual = item;
                return false;
            }

            if ((node.NodeMap & bit) == 0)
            {
                actual = item;
                return false;
            }

            node = node.Nodes[SlotOf(node.NodeMap, bit)];
        }
    }

    private static bool TryFindInCollision(Node node, T item, out T actual)
    {
        foreach (T resident in node.Items)
        {
            if (ItemEquals(resident, item))
            {
                actual = resident;
                return true;
            }
        }

        actual = item;
        return false;
    }

    // Insert into the trie. `owner` is the builder's ownership token, or null for a persistent write; a node
    // the token owns is edited in place instead of copied, which is what spares a builder the root-to-leaf
    // path copy on every write after the first down a given path. An element already present leaves the
    // trie untouched and `added` false, which is what lets Add hand back the receiver.
    private static Node PutInto(Node node, int shift, int hash, T item, object? owner, ref bool added)
    {
        if (shift >= HashBits)
            return PutIntoCollision(node, item, owner, ref added);

        int bit = Bit(Mask(hash, shift));

        if ((node.DataMap & bit) != 0)
        {
            int slot = SlotOf(node.DataMap, bit);
            T resident = node.Items[slot];

            if (ItemEquals(resident, item))
                return node;

            // Two elements want the same slot: push both down into a fresh sub-trie deep enough to tell them
            // apart, which is a collision node when they agree on all 32 bits.
            added = true;
            Node merged = MergeItems(shift + BranchBits, HashOf(resident), resident, hash, item, owner);
            return node.MigrateDataToNode(bit, slot, merged, owner);
        }

        if ((node.NodeMap & bit) != 0)
        {
            int slot = SlotOf(node.NodeMap, bit);
            Node newChild = PutInto(node.Nodes[slot], shift + BranchBits, hash, item, owner, ref added);
            return added ? node.WithNode(slot, newChild, owner) : node;
        }

        added = true;
        return node.InsertData(bit, SlotOf(node.DataMap, bit), item, owner);
    }

    private static Node PutIntoCollision(Node node, T item, object? owner, ref bool added)
    {
        foreach (T resident in node.Items)
        {
            if (ItemEquals(resident, item))
                return node;
        }

        added = true;
        return node.AppendCollisionItem(item, owner);
    }

    // Builds the sub-trie that separates two elements, starting at `shift`. Recurses one level per shared
    // five-bit chunk; a shift past the hash width means the two hashes are equal in all 32 bits, which is the
    // only way a collision node is ever created.
    private static Node MergeItems(int shift, int hash1, T item1, int hash2, T item2, object? owner)
    {
        if (shift >= HashBits)
            return new Node(0, 0, [item1, item2], NoNodes, owner);

        int index1 = Mask(hash1, shift);
        int index2 = Mask(hash2, shift);

        if (index1 == index2)
        {
            Node child = MergeItems(shift + BranchBits, hash1, item1, hash2, item2, owner);
            return new Node(0, Bit(index1), NoItems, [child], owner);
        }

        int dataMap = Bit(index1) | Bit(index2);
        return index1 < index2
            ? new Node(dataMap, 0, [item1, item2], NoNodes, owner)
            : new Node(dataMap, 0, [item2, item1], NoNodes, owner);
    }

    // Removal from the trie. Returns `node` itself when the element was absent, which is what lets Remove
    // hand back the receiver rather than an equal copy.
    private static Node RemoveFrom(Node node, int shift, int hash, T item, object? owner, ref bool removed)
    {
        if (shift >= HashBits)
            return RemoveFromCollision(node, item, owner, ref removed);

        int bit = Bit(Mask(hash, shift));

        if ((node.DataMap & bit) != 0)
        {
            int slot = SlotOf(node.DataMap, bit);
            if (!ItemEquals(node.Items[slot], item))
                return node;

            removed = true;
            return node.RemoveData(bit, slot, owner);
        }

        if ((node.NodeMap & bit) == 0)
            return node;

        int nodeSlot = SlotOf(node.NodeMap, bit);
        Node newChild = RemoveFrom(node.Nodes[nodeSlot], shift + BranchBits, hash, item, owner, ref removed);
        if (!removed)
            return node;

        // A child left holding one element and no children of its own is dissolved into this node. Doing it
        // at every level on the way back up is what keeps the trie canonical: the element can never be
        // stranded below a node that exists only to reach it, and the collapse propagates upward on its own,
        // since a parent that inlines an element is itself re-tested by *its* parent.
        if (newChild.IsSingleItem)
            return node.MigrateNodeToData(bit, nodeSlot, newChild.Items[0], owner);

        return node.WithNode(nodeSlot, newChild, owner);
    }

    private static Node RemoveFromCollision(Node node, T item, object? owner, ref bool removed)
    {
        for (int i = 0; i < node.Items.Length; i++)
        {
            if (!ItemEquals(node.Items[i], item))
                continue;

            removed = true;
            return node.RemoveCollisionItem(i, owner);
        }

        return node;
    }

    // Materializes `other` into a HashSet of its distinct elements keyed by the same default equality this
    // set uses. HashSet tolerates a null / default element, so the out-of-band element is captured too.
    private static HashSet<T> MaterializeDistinct(IEnumerable<T> other) => new(other, EqualityComparer<T>.Default);

    // Returns true iff every element of this set is contained in `other`.
    private bool AllElementsIn(HashSet<T> other)
    {
        foreach (T item in this)
        {
            if (!other.Contains(item))
                return false;
        }

        return true;
    }

    // A CHAMP node: PersistentHashMap's node with the value array taken out. Two 32-bit occupancy maps say
    // what each of the 32 slots holds — an inline element (DataMap), a sub-node (NodeMap), or nothing — and
    // the payload arrays are exactly as long as the number of slots of each kind, addressed by the popcount of
    // the bits below.
    //
    // A collision node is the same class with both maps zero and Items holding every element that shares one
    // 32-bit hash. It needs no marker: it can only sit under seven levels of descent, so the caller's shift
    // already says which shape it is looking at. That also makes enumeration blind to the difference, since
    // walking Items then Nodes is correct for both.
    //
    // The fields are mutable so a builder can edit a node it owns in place. Nothing published ever is: a node
    // is only written to while Owner is the token of the builder that made it, and ToImmutable takes a fresh
    // token, so every node the builder has handed out becomes read-only from that moment.
    private sealed class Node
    {
        internal int DataMap;
        internal int NodeMap;
        internal T[] Items;
        internal Node[] Nodes;
        internal object? Owner;

        internal Node(int dataMap, int nodeMap, T[] items, Node[] nodes, object? owner)
        {
            DataMap = dataMap;
            NodeMap = nodeMap;
            Items = items;
            Nodes = nodes;
            Owner = owner;
        }

        // True for a node holding exactly one element and no children — the shape a parent dissolves. Reads
        // Items/Nodes rather than the bitmaps so it answers for a collision node too.
        internal bool IsSingleItem => Items.Length == 1 && Nodes.Length == 0;

        internal Node WithNode(int slot, Node child, object? owner)
        {
            if (IsOwnedBy(owner))
            {
                Nodes[slot] = child;
                return this;
            }

            Node[] nodes = Clone(Nodes);
            nodes[slot] = child;
            return Fork(DataMap, NodeMap, Items, nodes, owner);
        }

        internal Node InsertData(int bit, int slot, T item, object? owner) =>
            Fork(DataMap | bit, NodeMap, InsertAt(Items, slot, item), Nodes, owner);

        internal Node RemoveData(int bit, int slot, object? owner) =>
            Fork(DataMap ^ bit, NodeMap, RemoveAt(Items, slot), Nodes, owner);

        // The element at `slot` becomes a sub-node at the same bit: the bit moves from DataMap to NodeMap.
        internal Node MigrateDataToNode(int bit, int slot, Node child, object? owner)
        {
            T[] items = RemoveAt(Items, slot);
            Node[] nodes = InsertAt(Nodes, SlotOf(NodeMap, bit), child);
            return Fork(DataMap ^ bit, NodeMap | bit, items, nodes, owner);
        }

        // The reverse: a sub-node that has shrunk to one element gives it up and the bit moves back to DataMap.
        internal Node MigrateNodeToData(int bit, int nodeSlot, T item, object? owner)
        {
            Node[] nodes = RemoveAt(Nodes, nodeSlot);
            T[] items = InsertAt(Items, SlotOf(DataMap, bit), item);
            return Fork(DataMap | bit, NodeMap ^ bit, items, nodes, owner);
        }

        internal Node AppendCollisionItem(T item, object? owner) =>
            Fork(0, 0, InsertAt(Items, Items.Length, item), Nodes, owner);

        internal Node RemoveCollisionItem(int slot, object? owner) =>
            Fork(0, 0, RemoveAt(Items, slot), Nodes, owner);

        // A node is editable only while it carries the *current* token of the builder writing to it. A
        // persistent write passes no token, and a node built persistently carries none, so the null-null case
        // must not read as ownership — hence the explicit null test rather than a bare reference comparison.
        private bool IsOwnedBy(object? owner) => owner is not null && ReferenceEquals(Owner, owner);

        // Takes the rebuilt arrays, in place when this node is the writer's own and into a fresh node
        // otherwise. What ownership saves is the node allocation and, above it, the path copy.
        //
        // The load-bearing rule is the second branch: a node passing *into* a builder's ownership may not
        // keep an array it still shares with the node it was forked from, or a later in-place write through
        // WithNode would reach through into storage a published set is still reading. Every array an owned
        // node holds is therefore either freshly built by the caller above or cloned here.
        private Node Fork(int dataMap, int nodeMap, T[] items, Node[] nodes, object? owner)
        {
            if (IsOwnedBy(owner))
            {
                DataMap = dataMap;
                NodeMap = nodeMap;
                Items = items;
                Nodes = nodes;
                return this;
            }

            if (owner is not null)
            {
                if (ReferenceEquals(items, Items))
                    items = Clone(items);

                if (ReferenceEquals(nodes, Nodes))
                    nodes = Clone(nodes);
            }

            return new Node(dataMap, nodeMap, items, nodes, owner);
        }

        // An empty array is handed back as-is: there is no slot to write, so no aliasing of one can be
        // observed, and the static NoItems / NoNodes singletons stay singletons.
        private static TElement[] Clone<TElement>(TElement[] source)
        {
            if (source.Length == 0)
                return source;

            var copy = new TElement[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static TElement[] InsertAt<TElement>(TElement[] source, int index, TElement item)
        {
            var copy = new TElement[source.Length + 1];
            Array.Copy(source, copy, index);
            copy[index] = item;
            Array.Copy(source, index, copy, index + 1, source.Length - index);
            return copy;
        }

        private static TElement[] RemoveAt<TElement>(TElement[] source, int index)
        {
            var copy = new TElement[source.Length - 1];
            Array.Copy(source, copy, index);
            Array.Copy(source, index + 1, copy, index, source.Length - index - 1);
            return copy;
        }
    }

    /// <summary>
    /// A mutable accumulator that produces <see cref="PersistentHashSet{T, THasher}"/> instances without
    /// allocating one per change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The builder stamps every node it creates with an ownership token and writes such a node in place
    /// rather than copying it. The saving is the <b>ancestors</b>, and it is amortized rather than fixed: the
    /// first write down a path it does not yet own still forks every node on that path (cloning the payload
    /// arrays, so no owned node shares one with a published set), and every write after that reuses them in
    /// place. The node the element actually lands in is rebuilt either way — an insert resizes its element
    /// array — so what the token removes is the fresh node per level from the root down, not the payload copy
    /// itself. <see cref="ToImmutable"/> takes a <i>new</i> token, which makes every node the builder has
    /// handed out read-only again in one assignment — so the builder stays usable afterwards and no set it has
    /// produced can be changed behind a caller's back.
    /// </para>
    /// <para>
    /// The builder is not thread-safe. The sets it produces are, under the same callback caveat the
    /// containing type documents: a read calls <typeparamref name="THasher"/> and
    /// <see cref="EqualityComparer{T}"/><c>.Default.Equals</c>, so a stateful hasher or element is the
    /// caller's to reason about.
    /// </para>
    /// </remarks>
    public sealed class Builder
    {
        private object _owner;
        private Node _root;
        private int _count;
        private bool _hasDefault;

        /// <summary>
        /// Initializes a new, empty builder.
        /// </summary>
        public Builder()
            : this(Empty)
        {
        }

        internal Builder(PersistentHashSet<T, THasher> source)
        {
            _owner = new object();
            _root = source._root;
            _count = source._count;
            _hasDefault = source._hasDefault;
        }

        /// <summary>
        /// Gets the number of elements the builder currently holds.
        /// </summary>
        public int Count => _count;

        // Whether any write has actually changed something, so the set-algebra methods can hand back the
        // receiver rather than an equal copy when every change the caller asked for was a no-op.
        internal bool Mutated { get; private set; }

        /// <summary>
        /// Determines whether <paramref name="item"/> is in the builder.
        /// </summary>
        /// <param name="item">The element to look for.</param>
        /// <returns><c>true</c> if the element is present; otherwise <c>false</c>.</returns>
        public bool Contains(T item) =>
            IsDefault(item) ? _hasDefault : TryFind(_root, HashOf(item), item, out _);

        /// <summary>
        /// Adds <paramref name="item"/> if it is absent.
        /// </summary>
        /// <param name="item">The element to add.</param>
        /// <returns><c>true</c> if the element was added; <c>false</c> if it was already present.</returns>
        public bool Add(T item)
        {
            ThrowIfFull(item);

            if (IsDefault(item))
            {
                if (_hasDefault)
                    return false;

                _hasDefault = true;
                _count++;
                Mutated = true;
                return true;
            }

            bool added = false;
            _root = PutInto(_root, 0, HashOf(item), item, _owner, ref added);
            if (!added)
                return false;

            _count++;
            Mutated = true;
            return true;
        }

        /// <summary>
        /// Removes <paramref name="item"/> if it is present.
        /// </summary>
        /// <param name="item">The element to remove.</param>
        /// <returns><c>true</c> if the element was present and removed; otherwise <c>false</c>.</returns>
        public bool Remove(T item)
        {
            if (IsDefault(item))
            {
                if (!_hasDefault)
                    return false;

                _hasDefault = false;
                _count--;
                Mutated = true;
                return true;
            }

            bool removed = false;
            _root = RemoveFrom(_root, 0, HashOf(item), item, _owner, ref removed);
            if (!removed)
                return false;

            _count--;
            Mutated = true;
            return true;
        }

        /// <summary>
        /// Produces an immutable set holding the builder's current elements. The builder stays usable, and
        /// further changes to it do not affect any set it has already returned.
        /// </summary>
        /// <returns>A set holding the builder's elements.</returns>
        public PersistentHashSet<T, THasher> ToImmutable()
        {
            // Every node made under the old token becomes read-only the moment the builder stops recognizing
            // it, which is what makes handing out the trie by reference safe.
            _owner = new object();

            return _count == 0 ? Empty : new PersistentHashSet<T, THasher>(_root, _count, _hasDefault);
        }

        // The builder's own ceiling guard, for the reason the set's carries: an insert past int.MaxValue
        // would wrap _count negative, and rejecting it before any node is written keeps a refused insert
        // from leaving the builder half-changed.
        [ExcludeFromCodeCoverage(Justification = "Needs int.MaxValue elements — tens of gigabytes of trie " +
            "nodes for any element type. The check exists so the count cannot silently wrap rather than " +
            "because a test can reach it.")]
        private void ThrowIfFull(T item)
        {
            if (_count == int.MaxValue && !Contains(item))
                throw new InvalidOperationException("A set cannot hold more than int.MaxValue elements.");
        }
    }

    /// <summary>
    /// Walks a <see cref="PersistentHashSet{T, THasher}"/>, yielding every element once.
    /// </summary>
    /// <remarks>
    /// The set cannot be modified, so this enumerator carries no version check and nothing can invalidate it.
    /// The descent stack is held inline in the struct — the trie is at most eight levels deep, so its depth
    /// is a compile-time constant — which is what makes enumeration allocation-free. The out-of-band
    /// <c>default(T)</c> element is yielded first when present; the rest follow in an unspecified order.
    /// </remarks>
    public struct Enumerator : IEnumerator<T>
    {
        // Depth sentinels. They are distinct because a walk that has run out must not restart: -1 says the
        // root has not been pushed yet, -2 says it has been popped and the enumeration is over.
        private const int NotStarted = -1;
        private const int Finished = -2;

        private readonly PersistentHashSet<T, THasher> _set;

        // The root-to-current path and, per level, how far through that node's elements-then-children the
        // walk has got. Index 0 is the root.
        private NodeStack _path;
        private CursorStack _cursors;
        private int _depth;
        private bool _defaultPending;
        private T _current;

        internal Enumerator(PersistentHashSet<T, THasher> set)
        {
            _set = set;
            _path = default;
            _cursors = default;
            _depth = NotStarted;
            _defaultPending = set._hasDefault;
            _current = default!;
        }

        /// <summary>
        /// Gets the element at the enumerator's current position, or <c>default</c> before the first
        /// <see cref="MoveNext"/> and after the last.
        /// </summary>
        public readonly T Current => _current;

        /// <inheritdoc/>
        readonly object? IEnumerator.Current => Current;

        /// <summary>
        /// Advances to the next element.
        /// </summary>
        /// <returns><c>true</c> if there is another element; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            if (_defaultPending)
            {
                // The out-of-band element comes first, and seeding the root here rather than on the next call
                // keeps the two entry points into the walk from having to agree twice.
                _defaultPending = false;
                _current = default!;
                _depth = 0;
                _path[0] = _set._root;
                _cursors[0] = 0;
                return true;
            }

            if (_depth == NotStarted)
            {
                _depth = 0;
                _path[0] = _set._root;
                _cursors[0] = 0;
            }

            while (_depth >= 0)
            {
                Node node = _path[_depth];
                int cursor = _cursors[_depth]++;

                // A node's elements come first, then its children — which is correct for a collision node
                // too, since that shape holds every one of its elements in Items and has no children at all.
                if (cursor < node.Items.Length)
                {
                    _current = node.Items[cursor];
                    return true;
                }

                int childIndex = cursor - node.Items.Length;
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
            _current = default!;
            return false;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first element.
        /// </summary>
        public void Reset()
        {
            _depth = NotStarted;
            _defaultPending = _set._hasDefault;
            _current = default!;
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
