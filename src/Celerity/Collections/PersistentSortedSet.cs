using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable sorted set</b> backed by a <b>persistent B+ tree</b>: a <b>single-element edit</b> returns a
/// new set that <b>shares</b> all but one root-to-leaf path of the old one's storage (plus a neighbour per level
/// on a removal that rebalances) — while an edit that changes nothing hands back the receiver — and the ordered
/// queries (<see cref="Min"/>, <see cref="Max"/>, <see cref="TryGetLowerBound"/>, <see cref="TryGetUpperBound"/>,
/// <see cref="TryGetFloor"/>, <see cref="TryGetPredecessor"/>) each cost one <c>O(log n)</c> descent, and
/// <see cref="EnumerateRange"/> costs <c>O(log n + k)</c>.
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
/// <typeparam name="TComparer">
/// The element ordering, which is also the set's notion of equality. Must be a struct so the JIT specializes
/// the tree for it and inlines the comparison; <see cref="DefaultComparer{T}"/> forwards to
/// <see cref="Comparer{T}.Default"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the set half of <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/>: the same tree, with the
/// value array taken out. <c>System.Collections.Immutable.ImmutableSortedSet&lt;T&gt;</c> is an <b>AVL tree</b>
/// with one heap node per element, each carrying the element, two children, a height and a subtree count. Its
/// branching factor is two, so a membership test is a pointer chase per level — about seventeen node
/// dereferences at 100,000 elements — and every comparison is an interface call through the
/// <see cref="IComparer{T}"/> it holds. It has <c>Min</c> and <c>Max</c>, but no floor, ceiling or range view:
/// "the first element at or after <i>x</i>" is an <c>IndexOf</c> binary search followed by an indexer descent,
/// and a range is that seek followed by one more descent per element.
/// </para>
/// <para>
/// Here every element lives in a leaf of up to 32 held in one flat array, and a branch holds up to 32 children
/// and the separators between them. 100,000 elements are four levels deep rather than seventeen, a level costs
/// one binary search over a contiguous array, and in-order enumeration — of the whole set or of a range — walks
/// leaf arrays rather than a node per element. An edit copies only the nodes on its root-to-leaf path and shares
/// every other node with the set it came from. <typeparamref name="TComparer"/> is a struct type argument, as in
/// <see cref="BTreeSet{T, TComparer}"/>, so the comparison is inlined rather than dispatched.
/// </para>
/// <para>
/// The documented BCL-beating workload is the one <see cref="PersistentHashSet{T, THasher}"/> serves, when the
/// reader also needs <b>order</b>: a sorted snapshot that is read far more often than it is written, must still
/// be updatable without copying, and is queried by <i>position in the order</i> as well as by membership — the
/// free-id or free-slot pool handed to readers as a version, a set of active timestamps or sequence numbers
/// asked "the next one after <i>t</i>", a sorted allow-list or symbol set threaded through a compiler pass, or
/// the visited frontier of a search that backtracks. Measured figures are in
/// <see href="https://github.com/marius-bughiu/Celerity/blob/main/docs/api/collections.md">the API reference</see>.
/// </para>
/// <para>
/// <b>Concurrent readers need no synchronization</b>, for the reason a value needs none: no published set is
/// ever mutated after it is returned, so there is no state for two threads to race over. A shared
/// <i>variable</i> holding successive sets still needs the usual publication rules, and the guarantee covers
/// the set's <i>own</i> state only: every lookup calls <typeparamref name="TComparer"/>, so a comparer or
/// element whose comparison is not itself thread-safe makes concurrent reads unsafe however immutable the set
/// is.
/// </para>
/// <para>
/// <b>Equality is the comparer's.</b> Two elements <typeparamref name="TComparer"/> orders equal are the same
/// element here — under a case-insensitive order, <c>"a"</c> and <c>"A"</c> are one — and the whole surface
/// answers that way, the set-algebra methods included, as <see cref="SortedSet{T}"/> does. <b>The comparer is
/// <c>default(TComparer)</c></b>: every set, <see cref="Empty"/> included, orders by the default value of
/// <typeparamref name="TComparer"/>, because the type shares one static empty set and every node operation is
/// static, so a comparer must carry its configuration in its type rather than in fields. A <c>null</c> element
/// is legal whenever the comparer orders it: <see cref="Comparer{T}.Default"/> orders <c>null</c> first, so
/// unlike the hashed family this set needs no out-of-band slot for <c>default(T)</c>.
/// </para>
/// <para>
/// Three deliberate omissions. There is no <c>Clear()</c>: everywhere else in this library <c>Clear()</c> means
/// in-place mutation, so the empty set is spelled <see cref="Empty"/>. This type does not implement
/// <c>IImmutableSet&lt;T&gt;</c>, whose <c>Clear()</c> contradicts that; it implements
/// <see cref="IReadOnlySet{T}"/>. And there is no positional access — no indexer and no <c>IndexOf</c> — because
/// the tree keeps no subtree counts, which is what keeps an edit's copy to one array per level. A caller who
/// needs rank and select over a sorted set wants <see cref="RankedSet{T, TComparer}"/> (mutable) or
/// <c>ImmutableSortedSet&lt;T&gt;</c>, whose AVL nodes carry the count.
/// </para>
/// </remarks>
public sealed class PersistentSortedSet<T, TComparer> : IReadOnlySet<T>
    where TComparer : struct, IComparer<T>
{
    // A leaf holds up to MaxFill elements and a branch up to MaxFill children. Every node except the root keeps
    // at least MinFill, which is what bounds the depth; an insert that overflows a node to MaxFill + 1 splits it
    // into MinFill and MinFill + 1, and a removal that drops one below MinFill borrows from or merges with a
    // sibling. Mirrors PersistentSortedMap.
    private const int MaxFill = 32;
    private const int MinFill = MaxFill / 2;

    // Arrays a builder owns are allocated at this length, one past MaxFill, so a node can hold the transient
    // overflow an insert produces before it splits. A persistent edit allocates exact-length arrays instead.
    private const int BuilderCapacity = MaxFill + 1;

    // The deepest tree int.MaxValue elements can build: a tree of depth d holds at least 2 * MinFill^(d - 1)
    // elements, and 2 * 16^7 = 2^29 is the last power under 2^31, so d <= 8. The enumerator's inline path is
    // this deep.
    private const int MaxDepth = 8;

    private static readonly T[] NoKeys = Array.Empty<T>();

    /// <summary>
    /// The empty set. <see cref="Remove"/> of the last element returns this instance, so draining a set one
    /// element at a time ends at the same object every empty set starts from.
    /// </summary>
    public static readonly PersistentSortedSet<T, TComparer> Empty =
        new(new Node(NoKeys, children: null, count: 0, owner: null), 0);

    private readonly Node _root;
    private readonly int _count;

    private PersistentSortedSet(Node root, int count)
    {
        _root = root;
        _count = count;
    }

    /// <summary>
    /// Initializes a new set containing the distinct elements of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The elements to copy into the set. Duplicates are ignored.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The elements are inserted through a <see cref="Builder"/>, so no intermediate set is allocated per element
    /// and no root-to-leaf path is copied per element either. The source does not need to be sorted, and
    /// duplicates — under <typeparamref name="TComparer"/> — are ignored rather than rejected, as
    /// <see cref="SortedSet{T}"/>'s sequence constructor does; the first of a run of equal elements is the one
    /// kept.
    /// </remarks>
    public PersistentSortedSet(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var builder = new Builder();
        foreach (T item in source)
            builder.Add(item);

        PersistentSortedSet<T, TComparer> built = builder.ToImmutable();
        _root = built._root;
        _count = built._count;
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
    /// Gets the smallest element, in <c>O(log n)</c>.
    /// </summary>
    /// <returns>The first element in order.</returns>
    /// <exception cref="InvalidOperationException">The set is empty.</exception>
    public T Min =>
        TryGetMin(out T? item)
            ? item
            : throw new InvalidOperationException("The set is empty.");

    /// <summary>
    /// Gets the largest element, in <c>O(log n)</c>.
    /// </summary>
    /// <returns>The last element in order.</returns>
    /// <exception cref="InvalidOperationException">The set is empty.</exception>
    public T Max =>
        TryGetMax(out T? item)
            ? item
            : throw new InvalidOperationException("The set is empty.");

    /// <summary>
    /// Determines whether <paramref name="item"/> is in the set — whether some element orders equal to it under
    /// <typeparamref name="TComparer"/>.
    /// </summary>
    /// <param name="item">The element to look for.</param>
    /// <returns><c>true</c> if the element is present; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// One binary search over a contiguous array per level, over a tree of fan-out 32: four levels at 100,000
    /// elements and never more than eight.
    /// </remarks>
    public bool Contains(T item) => TryFind(_root, item, out _);

    /// <summary>
    /// Looks up the element <typeparamref name="TComparer"/> orders equal to <paramref name="equalValue"/> and
    /// returns the instance the set holds.
    /// </summary>
    /// <param name="equalValue">The value to look for.</param>
    /// <param name="actualValue">
    /// When this returns <c>true</c>, the stored element equal to <paramref name="equalValue"/>; otherwise
    /// <paramref name="equalValue"/> itself.
    /// </param>
    /// <returns><c>true</c> if an equal element is present; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// The point is to recover the <i>stored</i> instance — the original casing under a case-insensitive order,
    /// say. On a miss the argument comes back unchanged, as <c>ImmutableSortedSet&lt;T&gt;.TryGetValue</c> and
    /// <see cref="PersistentHashSet{T, THasher}.TryGetValue"/> hand it back.
    /// </remarks>
    public bool TryGetValue(T equalValue, out T actualValue)
    {
        if (TryFind(_root, equalValue, out T? stored))
        {
            actualValue = stored;
            return true;
        }

        actualValue = equalValue;
        return false;
    }

    /// <summary>
    /// Gets the smallest element.
    /// </summary>
    /// <param name="item">The first element in order, or <c>default</c> when the set is empty.</param>
    /// <returns><c>true</c> if the set is non-empty.</returns>
    public bool TryGetMin([MaybeNullWhen(false)] out T item)
    {
        if (_count == 0)
        {
            item = default;
            return false;
        }

        item = FirstElement(_root);
        return true;
    }

    /// <summary>
    /// Gets the largest element.
    /// </summary>
    /// <param name="item">The last element in order, or <c>default</c> when the set is empty.</param>
    /// <returns><c>true</c> if the set is non-empty.</returns>
    public bool TryGetMax([MaybeNullWhen(false)] out T item)
    {
        if (_count == 0)
        {
            item = default;
            return false;
        }

        item = LastElement(_root);
        return true;
    }

    /// <summary>
    /// Finds the first element that orders <b>at or after</b> <paramref name="item"/> — the <c>lower_bound</c>
    /// of the ordered containers, or the <i>ceiling</i> of <paramref name="item"/> — in <c>O(log n)</c>. An
    /// element equal to <paramref name="item"/> is its own lower bound.
    /// </summary>
    /// <param name="item">The value to bound.</param>
    /// <param name="result">The bounding element, or <c>default</c> when every element is smaller.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetLowerBound(T item, [MaybeNullWhen(false)] out T result) =>
        TrySeek(_root, item, strict: false, out result);

    /// <summary>
    /// Finds the first element that orders <b>strictly after</b> <paramref name="item"/> — the
    /// <c>upper_bound</c> of the ordered containers, or its successor — in <c>O(log n)</c>.
    /// </summary>
    /// <param name="item">The value to bound.</param>
    /// <param name="result">The bounding element, or <c>default</c> when no element is larger.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetUpperBound(T item, [MaybeNullWhen(false)] out T result) =>
        TrySeek(_root, item, strict: true, out result);

    /// <summary>
    /// Finds the last element that orders <b>at or before</b> <paramref name="item"/> — the <i>floor</i> of
    /// <paramref name="item"/> — in <c>O(log n)</c>. An element equal to <paramref name="item"/> is its own
    /// floor.
    /// </summary>
    /// <param name="item">The value to bound.</param>
    /// <param name="result">The bounding element, or <c>default</c> when every element is larger.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetFloor(T item, [MaybeNullWhen(false)] out T result) =>
        TrySeekBackward(_root, item, strict: false, out result);

    /// <summary>
    /// Finds the last element that orders <b>strictly before</b> <paramref name="item"/> — its predecessor — in
    /// <c>O(log n)</c>.
    /// </summary>
    /// <param name="item">The value to bound.</param>
    /// <param name="result">The bounding element, or <c>default</c> when no element is smaller.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetPredecessor(T item, [MaybeNullWhen(false)] out T result) =>
        TrySeekBackward(_root, item, strict: true, out result);

    /// <summary>
    /// Enumerates, in ascending order, every element in the half-open range
    /// <c>[fromInclusive, toExclusive)</c>. The scan seeks to the lower bound in <c>O(log n)</c> and then walks
    /// the leaves' contiguous arrays, so it costs <c>O(log n + k)</c> for <c>k</c> results rather than a full
    /// <c>O(n)</c> filter.
    /// </summary>
    /// <param name="fromInclusive">The inclusive lower bound of the range.</param>
    /// <param name="toExclusive">The exclusive upper bound of the range.</param>
    /// <returns>An allocation-free enumerable over the matching elements.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="toExclusive"/> orders before <paramref name="fromInclusive"/>.
    /// </exception>
    public RangeEnumerable EnumerateRange(T fromInclusive, T toExclusive)
    {
        if (Compare(fromInclusive, toExclusive) > 0)
            throw new ArgumentException("toExclusive must not order before fromInclusive.", nameof(toExclusive));

        return new RangeEnumerable(this, fromInclusive, toExclusive);
    }

    /// <summary>
    /// Returns a set with <paramref name="item"/> added. This set is unchanged.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>
    /// A set of <c>Count + 1</c> elements sharing this one's storage, or this set when an equal element was
    /// already present.
    /// </returns>
    /// <remarks>
    /// Copying is confined to the root-to-leaf path: at most eight nodes of at most 32 elements or children
    /// each. An insert that overflows a node splits it, which adds one node per level it reaches. Matches
    /// <c>ImmutableSortedSet&lt;T&gt;.Add</c>, which also hands back the receiver for an element already
    /// present, and keeps the stored instance rather than replacing it with the argument.
    /// </remarks>
    public PersistentSortedSet<T, TComparer> Add(T item)
    {
        ThrowIfFull(item);

        bool added = false;
        Node newRoot = PutInto(_root, item, owner: null, ref added);
        if (!added)
            return this;

        return new PersistentSortedSet<T, TComparer>(GrowIfFull(newRoot, owner: null), _count + 1);
    }

    /// <summary>
    /// Returns a set with <paramref name="item"/> removed. This set is unchanged.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>
    /// A set of <c>Count - 1</c> elements sharing this one's storage, this set when <paramref name="item"/> was
    /// absent, or <see cref="Empty"/> when it was the last element.
    /// </returns>
    /// <remarks>
    /// A removal that leaves a node below half full borrows from or merges with a sibling, so every node but the
    /// root stays at least half full and the depth bound holds however the set was edited. The sibling is copied
    /// too, so a rebalancing removal copies up to two nodes per level it reaches.
    /// </remarks>
    public PersistentSortedSet<T, TComparer> Remove(T item)
    {
        bool removed = false;
        Node newRoot = RemoveFrom(_root, item, owner: null, ref removed);
        if (!removed)
            return this;

        return _count == 1
            ? Empty
            : new PersistentSortedSet<T, TComparer>(Collapse(newRoot), _count - 1);
    }

    /// <summary>
    /// Returns a set holding every element of this set and of <paramref name="other"/>. This set is unchanged.
    /// </summary>
    /// <param name="other">The elements to add. Duplicates, and elements already present, are ignored.</param>
    /// <returns>The union, or this set when <paramref name="other"/> added nothing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The additions run through one <see cref="Builder"/>, so <c>m</c> elements allocate one intermediate set
    /// rather than <c>m</c> of them.
    /// </remarks>
    public PersistentSortedSet<T, TComparer> Union(IEnumerable<T> other)
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
    public PersistentSortedSet<T, TComparer> Except(IEnumerable<T> other)
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
    /// Returns a set holding only the elements of this set that are also in <paramref name="other"/>. This set
    /// is unchanged.
    /// </summary>
    /// <param name="other">The elements to keep.</param>
    /// <returns>The intersection, or this set when every element of it is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Builds the result up from the elements of <paramref name="other"/> this set contains, rather than
    /// removing the rest from a copy — so a small intersection of a large set costs a small set's worth of
    /// allocation. <paramref name="other"/> is collapsed under <typeparamref name="TComparer"/> and sorted first,
    /// so the result is built in ascending order. The stored instances are kept, not the equal ones
    /// <paramref name="other"/> supplied.
    /// </remarks>
    public PersistentSortedSet<T, TComparer> Intersect(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other) || _count == 0)
            return this;

        ReadOnlySpan<T> distinct = OrderedSetOperations.MaterializeDistinct(other, default(TComparer));
        var builder = new Builder();
        foreach (T item in distinct)
        {
            if (TryFind(_root, item, out T? stored))
                builder.Add(stored);
        }

        // The result is a subset of this set, so an equal count means an equal set.
        return builder.Count == _count ? this : builder.ToImmutable();
    }

    /// <summary>
    /// Returns a set holding the elements that are in exactly one of this set and <paramref name="other"/>. This
    /// set is unchanged.
    /// </summary>
    /// <param name="other">
    /// The elements to toggle. Elements <typeparamref name="TComparer"/> orders equal toggle once, not once per
    /// occurrence.
    /// </param>
    /// <returns>The symmetric difference, or this set when <paramref name="other"/> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public PersistentSortedSet<T, TComparer> SymmetricExcept(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        // Every element is in both, so none survives — and, as in Except, an empty receiver is already the
        // answer and is handed back rather than swapped for the singleton.
        if (ReferenceEquals(this, other))
            return _count == 0 ? this : Empty;

        // Collapsed under the comparer first, so a run of equal elements toggles once — the same rule
        // SortedSet<T>.SymmetricExceptWith follows.
        ReadOnlySpan<T> distinct = OrderedSetOperations.MaterializeDistinct(other, default(TComparer));
        var builder = new Builder(this);
        foreach (T item in distinct)
        {
            if (!builder.Remove(item))
                builder.Add(item);
        }

        return builder.Mutated ? builder.ToImmutable() : this;
    }

    // ── IReadOnlySet<T> queries ───────────────────────────────────────────────────────────────────────
    // The whole set is the left-hand operand; `other` is the right-hand one. IsSupersetOf and Overlaps stream
    // `other` against Contains and may stop early. The other four need the *distinct* elements of `other`, and
    // distinct means distinct under TComparer — so they go through OrderedSetOperations, which collapses
    // `other` with the comparer and merges it against this set's ascending enumeration, rather than through a
    // HashSet<T> keyed by EqualityComparer<T>.Default, which would disagree with the set under a comparer that
    // equates unequal values. All six answer a same-instance argument without enumerating it.

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> contain the same distinct elements.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return ReferenceEquals(this, other) || OrderedSetOperations.SetEquals(this, default(TComparer), other);
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
        return ReferenceEquals(this, other) || OrderedSetOperations.IsSubsetOf(this, default(TComparer), other);
    }

    /// <summary>
    /// Determines whether the set is a proper (strict) subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of the set is in <paramref name="other"/> and <paramref name="other"/> has at
    /// least one element the set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return !ReferenceEquals(this, other)
            && OrderedSetOperations.IsProperSubsetOf(this, default(TComparer), other);
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
        if (ReferenceEquals(this, other))
            return true;

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
        return !ReferenceEquals(this, other)
            && OrderedSetOperations.IsProperSupersetOf(this, default(TComparer), other);
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
        if (ReferenceEquals(this, other))
            return true;

        foreach (T item in other)
        {
            if (Contains(item))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a mutable builder seeded with this set's elements, for making many changes without allocating a
    /// set per change.
    /// </summary>
    /// <returns>A builder holding this set's elements.</returns>
    public Builder ToBuilder() => new(this);

    /// <summary>
    /// Returns an enumerator over the set's elements in ascending order.
    /// </summary>
    /// <returns>A struct enumerator over the elements.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── The tree ──────────────────────────────────────────────────────────────────────────────────────

    // Refuses an insert that would wrap the count negative. The Contains probe runs only at the ceiling, so
    // re-adding an element already present is still allowed on a full set. Mirrors PersistentSortedMap's.
    [ExcludeFromCodeCoverage(Justification = "Needs int.MaxValue elements — gigabytes of leaf arrays for any " +
        "element type. The check exists so the count cannot silently wrap rather than because a test can " +
        "reach it.")]
    private void ThrowIfFull(T item)
    {
        if (_count == int.MaxValue && !Contains(item))
            throw new InvalidOperationException("A set cannot hold more than int.MaxValue elements.");
    }

    // The comparer is constrained to a struct and the set orders by its default value, so `default(TComparer)`
    // is the value a field would hold and the JIT inlines the call through it identically.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Compare(T left, T right) => default(TComparer).Compare(left, right);

    // The position of `item` in a leaf, or the bitwise complement of the position it would be inserted at.
    private static int FindInLeaf(Node leaf, T item)
    {
        T[] keys = leaf.Keys;
        int lo = 0;
        int hi = leaf.Count - 1;
        while (lo <= hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            int order = Compare(keys[mid], item);
            if (order == 0)
                return mid;

            if (order < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return ~lo;
    }

    // The child of a branch whose range holds `item`: the number of separators at or below it.
    private static int ChildIndex(Node branch, T item)
    {
        T[] separators = branch.Keys;
        int lo = 0;
        int hi = branch.Count - 2;
        while (lo <= hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            if (Compare(separators[mid], item) <= 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return lo;
    }

    private static bool TryFind(Node node, T item, [MaybeNullWhen(false)] out T stored)
    {
        while (node.Children is { } children)
            node = children[ChildIndex(node, item)];

        int index = FindInLeaf(node, item);
        if (index >= 0)
        {
            stored = node.Keys[index];
            return true;
        }

        stored = default;
        return false;
    }

    private static T FirstElement(Node node)
    {
        while (node.Children is { } children)
            node = children[0];

        return node.Keys[0];
    }

    private static T LastElement(Node node)
    {
        while (node.Children is { } children)
            node = children[node.Count - 1];

        return node.Keys[node.Count - 1];
    }

    // The first element at or after `item` (or strictly after, when `strict`). The descent follows `item`, and on
    // the way down remembers the nearest subtree to the right of the path: if the leaf `item` lands in has
    // nothing large enough, the answer is that subtree's first element.
    private static bool TrySeek(Node node, T item, bool strict, [MaybeNullWhen(false)] out T result)
    {
        Node? rightOfPath = null;
        while (node.Children is { } children)
        {
            int child = ChildIndex(node, item);
            if (child + 1 < node.Count)
                rightOfPath = children[child + 1];

            node = children[child];
        }

        int index = FindInLeaf(node, item);
        index = index >= 0 ? (strict ? index + 1 : index) : ~index;

        if (index < node.Count)
        {
            result = node.Keys[index];
            return true;
        }

        if (rightOfPath is not null)
        {
            result = FirstElement(rightOfPath);
            return true;
        }

        result = default;
        return false;
    }

    // The mirror of TrySeek: the last element at or before `item` (or strictly before, when `strict`), falling
    // back to the last element of the nearest subtree left of the path.
    private static bool TrySeekBackward(Node node, T item, bool strict, [MaybeNullWhen(false)] out T result)
    {
        Node? leftOfPath = null;
        while (node.Children is { } children)
        {
            int child = ChildIndex(node, item);
            if (child > 0)
                leftOfPath = children[child - 1];

            node = children[child];
        }

        int index = FindInLeaf(node, item);
        index = index >= 0 ? (strict ? index - 1 : index) : ~index - 1;

        if (index >= 0)
        {
            result = node.Keys[index];
            return true;
        }

        if (leftOfPath is not null)
        {
            result = LastElement(leftOfPath);
            return true;
        }

        result = default;
        return false;
    }

    // A root that overflowed splits, and the two halves become the children of a new root one level up. This is
    // the only way the tree gains depth, which is what keeps every leaf at the same level.
    private static Node GrowIfFull(Node root, object? owner)
    {
        if (root.Count <= MaxFill)
            return root;

        Node left = Split(root, owner, out Node right, out T separator);
        T[] separators = new T[owner is null ? 1 : BuilderCapacity];
        Node[] children = new Node[owner is null ? 2 : BuilderCapacity];
        separators[0] = separator;
        children[0] = left;
        children[1] = right;
        return new Node(separators, children, count: 2, owner);
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
    // write; a node the token owns is edited in place instead of copied. An element already present changes
    // nothing — the stored instance is kept — so `added` is also "changed". The returned node may hold
    // MaxFill + 1 elements or children; the caller splits it.
    private static Node PutInto(Node node, T item, object? owner, ref bool added)
    {
        if (node.Children is not { } children)
        {
            int index = FindInLeaf(node, item);
            if (index >= 0)
                return node;

            added = true;
            return node.InsertElement(~index, item, owner);
        }

        int child = ChildIndex(node, item);
        Node newChild = PutInto(children[child], item, owner, ref added);
        if (!added)
            return node;

        if (newChild.Count <= MaxFill)
            return node.WithChild(child, newChild, owner);

        Node left = Split(newChild, owner, out Node right, out T separator);
        return node.InsertChild(child, left, separator, right, owner);
    }

    // Splits a node holding MaxFill + 1 elements or children into a left half of MinFill and a right half of
    // MinFill + 1, and reports the separator the parent needs between them: for a leaf the right half's first
    // element, which stays in the leaf; for a branch the separator between the halves' children, which moves up.
    private static Node Split(Node full, object? owner, out Node right, out T separator)
    {
        const int LeftCount = MinFill;
        int rightCount = full.Count - LeftCount;

        if (full.Children is not { } children)
        {
            right = Node.Leaf(full.Keys, LeftCount, rightCount, owner);
            separator = full.Keys[LeftCount];
            return full.Truncate(LeftCount, owner);
        }

        separator = full.Keys[LeftCount - 1];
        right = Node.Branch(full.Keys, LeftCount, children, LeftCount, rightCount, owner);
        return full.Truncate(LeftCount, owner);
    }

    // Remove from the subtree under `node`. The returned node may hold MinFill - 1 elements or children; the
    // caller rebalances it.
    private static Node RemoveFrom(Node node, T item, object? owner, ref bool removed)
    {
        if (node.Children is not { } children)
        {
            int index = FindInLeaf(node, item);
            if (index < 0)
                return node;

            removed = true;
            return node.RemoveElement(index, owner);
        }

        int child = ChildIndex(node, item);
        Node newChild = RemoveFrom(children[child], item, owner, ref removed);
        if (!removed)
            return node;

        if (newChild.Count >= MinFill)
            return node.WithChild(child, newChild, owner);

        return Rebalance(node, child, newChild, owner);
    }

    // `child` has dropped below MinFill. Pair it with a neighbour — the left one when there is one — and either
    // merge the two, when they fit in one node, or share their contents out evenly.
    private static Node Rebalance(Node parent, int child, Node underfull, object? owner)
    {
        Node[] children = parent.Children!;
        int leftIndex = child > 0 ? child - 1 : child;
        Node left = child > 0 ? children[child - 1] : underfull;
        Node right = child > 0 ? underfull : children[child + 1];
        T separator = parent.Keys[leftIndex];
        int total = left.Count + right.Count;

        if (total <= MaxFill)
        {
            Node merged = Merge(left, separator, right, owner);
            return parent.ReplaceChildPair(leftIndex, merged, owner);
        }

        Node newLeft = Redistribute(left, separator, right, owner, out Node newRight, out T newSeparator);
        return parent.WithChildPair(leftIndex, newLeft, newSeparator, newRight, owner);
    }

    // Concatenates two neighbours into one node. For branches the parent's separator between them comes down
    // between the two runs of separators.
    private static Node Merge(Node left, T separator, Node right, object? owner)
    {
        int count = left.Count + right.Count;
        int capacity = owner is null ? count : BuilderCapacity;

        if (left.Children is not { } leftChildren)
        {
            var keys = new T[capacity];
            Array.Copy(left.Keys, keys, left.Count);
            Array.Copy(right.Keys, 0, keys, left.Count, right.Count);
            return new Node(keys, children: null, count, owner);
        }

        var separators = new T[owner is null ? count - 1 : BuilderCapacity];
        var children = new Node[capacity];
        Array.Copy(left.Keys, separators, left.Count - 1);
        separators[left.Count - 1] = separator;
        Array.Copy(right.Keys, 0, separators, left.Count, right.Count - 1);
        Array.Copy(leftChildren, children, left.Count);
        Array.Copy(right.Children!, 0, children, left.Count, right.Count);
        return new Node(separators, children, count, owner);
    }

    // Shares two neighbours' contents out evenly between two fresh nodes. Only called when the pair holds more
    // than MaxFill, so each half gets at least MinFill.
    private static Node Redistribute(
        Node left, T separator, Node right, object? owner, out Node newRight, out T newSeparator)
    {
        int count = left.Count + right.Count;
        int leftCount = count / 2;
        int rightCount = count - leftCount;

        // Staged through one merged node, as PersistentSortedMap does: the rare path, and the simplest correct
        // staging area for both the leaf and the branch case.
        Node merged = Merge(left, separator, right, owner: null);

        if (merged.Children is not { } children)
        {
            newRight = Node.Leaf(merged.Keys, leftCount, rightCount, owner);
            newSeparator = merged.Keys[leftCount];
            return Node.Leaf(merged.Keys, 0, leftCount, owner);
        }

        newSeparator = merged.Keys[leftCount - 1];
        newRight = Node.Branch(merged.Keys, leftCount, children, leftCount, rightCount, owner);
        return Node.Branch(merged.Keys, 0, children, 0, leftCount, owner);
    }

    // A tree node. A leaf holds elements in Keys[0..Count); a branch holds Count children and the Count - 1
    // separators between them in Keys. Arrays may be longer than Count — a builder allocates them at
    // BuilderCapacity — so Count, never Length, is the bound.
    private sealed class Node
    {
        internal readonly T[] Keys;
        internal readonly Node[]? Children;
        internal int Count;

        // The builder whose token this is may edit the node in place; any other writer copies it.
        internal readonly object? Owner;

        internal Node(T[] keys, Node[]? children, int count, object? owner)
        {
            Keys = keys;
            Children = children;
            Count = count;
            Owner = owner;
        }

        private bool IsOwnedBy(object? owner) => owner is not null && ReferenceEquals(Owner, owner);

        private static int CapacityFor(int count, object? owner) => owner is null ? count : BuilderCapacity;

        internal static Node Leaf(T[] keys, int start, int count, object? owner)
        {
            var newKeys = new T[CapacityFor(count, owner)];
            Array.Copy(keys, start, newKeys, 0, count);
            return new Node(newKeys, children: null, count, owner);
        }

        // A branch of `count` children starting at `childStart`, with the count - 1 separators that start at
        // `separatorStart`.
        internal static Node Branch(
            T[] separators, int separatorStart, Node[] children, int childStart, int count, object? owner)
        {
            var newSeparators = new T[owner is null ? count - 1 : BuilderCapacity];
            var newChildren = new Node[CapacityFor(count, owner)];
            Array.Copy(separators, separatorStart, newSeparators, 0, count - 1);
            Array.Copy(children, childStart, newChildren, 0, count);
            return new Node(newSeparators, newChildren, count, owner);
        }

        // This node cut down to its first `count` elements or children: in place when the builder owns it,
        // clearing the vacated slots so they keep nothing alive, and as a fresh exact-length copy otherwise.
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
                    return Leaf(Keys, 0, count, owner);

                ClearKeys(count, Count - count);
            }

            Count = count;
            return this;
        }

        internal Node WithChild(int index, Node child, object? owner)
        {
            if (IsOwnedBy(owner))
            {
                Children![index] = child;
                return this;
            }

            // A persistent copy can share the separator array: it did not change and nothing will ever write
            // through a node no builder owns. A builder copy cannot, since it will edit its separators later.
            T[] separators = owner is null ? Keys : Copy(Keys, Count - 1, BuilderCapacity);
            Node[] children = Copy(Children!, Count, CapacityFor(Count, owner));
            children[index] = child;
            return new Node(separators, children, Count, owner);
        }

        internal Node InsertElement(int index, T item, object? owner)
        {
            int count = Count;
            Node target;
            if (IsOwnedBy(owner))
            {
                // An owned leaf's array is BuilderCapacity long and it holds at most MaxFill elements, so the
                // shift always has room.
                Array.Copy(Keys, index, Keys, index + 1, count - index);
                target = this;
            }
            else
            {
                target = new Node(
                    CopyWithGap(Keys, count, index, CapacityFor(count + 1, owner)), children: null, count, owner);
            }

            target.Keys[index] = item;
            target.Count = count + 1;
            return target;
        }

        // Replaces child `index` with the two halves it split into, inserting the separator between them.
        internal Node InsertChild(int index, Node left, T separator, Node right, object? owner)
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
                target = new Node(
                    CopyWithGap(Keys, count - 1, index, owner is null ? count : BuilderCapacity),
                    CopyWithGap(Children!, count, index + 1, CapacityFor(count + 1, owner)),
                    count,
                    owner);
            }

            target.Keys[index] = separator;
            target.Children![index] = left;
            target.Children[index + 1] = right;
            target.Count = count + 1;
            return target;
        }

        internal Node RemoveElement(int index, object? owner)
        {
            int count = Count;
            if (IsOwnedBy(owner))
            {
                Array.Copy(Keys, index + 1, Keys, index, count - index - 1);
                ClearKeys(count - 1, 1);
                Count = count - 1;
                return this;
            }

            return new Node(
                CopyWithout(Keys, count, index, CapacityFor(count - 1, owner)), children: null, count - 1, owner);
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
                ClearKeys(count - 2, 1);
                Count = count - 1;
                target = this;
            }
            else
            {
                target = new Node(
                    CopyWithout(Keys, count - 1, index, owner is null ? count - 2 : BuilderCapacity),
                    CopyWithout(Children!, count, index + 1, CapacityFor(count - 1, owner)),
                    count - 1,
                    owner);
            }

            target.Children![index] = merged;
            return target;
        }

        // Replaces children `index` and `index + 1` with a redistributed pair and the new separator between
        // them. The count does not change.
        internal Node WithChildPair(int index, Node left, T separator, Node right, object? owner)
        {
            Node target = this;
            if (!IsOwnedBy(owner))
            {
                target = new Node(
                    Copy(Keys, Count - 1, owner is null ? Count - 1 : BuilderCapacity),
                    Copy(Children!, Count, CapacityFor(Count, owner)),
                    Count,
                    owner);
            }

            target.Keys[index] = separator;
            target.Children![index] = left;
            target.Children[index + 1] = right;
            return target;
        }

        private void ClearKeys(int start, int length)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                Array.Clear(Keys, start, length);
        }

        private static TItem[] Copy<TItem>(TItem[] source, int length, int capacity)
        {
            var copy = new TItem[capacity];
            Array.Copy(source, copy, length);
            return copy;
        }

        // A copy of source[0..length) with an unwritten slot opened at `gap`.
        private static TItem[] CopyWithGap<TItem>(TItem[] source, int length, int gap, int capacity)
        {
            var copy = new TItem[capacity];
            Array.Copy(source, copy, gap);
            Array.Copy(source, gap, copy, gap + 1, length - gap);
            return copy;
        }

        // A copy of source[0..length) with the element at `index` left out.
        private static TItem[] CopyWithout<TItem>(TItem[] source, int length, int index, int capacity)
        {
            var copy = new TItem[capacity];
            Array.Copy(source, copy, index);
            Array.Copy(source, index + 1, copy, index, length - index - 1);
            return copy;
        }
    }

    /// <summary>
    /// A mutable companion to <see cref="PersistentSortedSet{T, TComparer}"/> for making a run of changes
    /// without allocating a set — or copying a root-to-leaf path — per change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A builder holds an ownership token, and every node it creates is stamped with it. A write that reaches a
    /// node the builder owns edits it in place; one that reaches a node shared with a published set copies it
    /// first, once, and owns the copy from then on. <see cref="ToImmutable"/> replaces the token, which makes
    /// every node the builder has handed out read-only again in one assignment — so the builder stays usable
    /// afterwards and no set it has produced can be changed behind a caller's back.
    /// </para>
    /// <para>
    /// The builder is not thread-safe. The sets it produces are, under the comparer caveat the containing type
    /// documents.
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

        internal Builder(PersistentSortedSet<T, TComparer> source)
        {
            _owner = new object();
            _root = source._root;
            _count = source._count;
        }

        /// <summary>
        /// Gets the number of elements the builder currently holds.
        /// </summary>
        public int Count => _count;

        // Whether any write has actually changed something, so Union / Except / SymmetricExcept can hand back
        // the receiver rather than an equal copy when the caller's updates were all no-ops.
        internal bool Mutated { get; private set; }

        /// <summary>
        /// Determines whether <paramref name="item"/> is in the builder.
        /// </summary>
        /// <param name="item">The element to look for.</param>
        /// <returns><c>true</c> if an equal element is present; otherwise <c>false</c>.</returns>
        public bool Contains(T item) => TryFind(_root, item, out _);

        /// <summary>
        /// Adds <paramref name="item"/> if no equal element is present.
        /// </summary>
        /// <param name="item">The element to add.</param>
        /// <returns><c>true</c> if the element was added; <c>false</c> if an equal one was already present.</returns>
        public bool Add(T item)
        {
            ThrowIfFull(item);

            bool added = false;
            Node newRoot = PutInto(_root, item, _owner, ref added);
            if (!added)
                return false;

            _root = GrowIfFull(newRoot, _owner);
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
            bool removed = false;
            Node newRoot = RemoveFrom(_root, item, _owner, ref removed);
            if (!removed)
                return false;

            _root = Collapse(newRoot);
            _count--;
            Mutated = true;
            return true;
        }

        /// <summary>
        /// Produces an immutable set holding the builder's current elements. The builder stays usable, and
        /// further changes to it do not affect any set it has already returned.
        /// </summary>
        /// <returns>A set holding the builder's elements.</returns>
        public PersistentSortedSet<T, TComparer> ToImmutable()
        {
            // Every node made under the old token becomes read-only the moment the builder stops recognizing it.
            _owner = new object();

            if (_count == 0)
            {
                _root = Empty._root;
                return Empty;
            }

            return new PersistentSortedSet<T, TComparer>(_root, _count);
        }

        // The builder's own ceiling guard, for the reason the set's carries.
        [ExcludeFromCodeCoverage(Justification = "Needs int.MaxValue elements — gigabytes of leaf arrays for " +
            "any element type. The check exists so the count cannot silently wrap rather than because a test " +
            "can reach it.")]
        private void ThrowIfFull(T item)
        {
            if (_count == int.MaxValue && !Contains(item))
                throw new InvalidOperationException("A set cannot hold more than int.MaxValue elements.");
        }
    }

    /// <summary>
    /// A struct enumerable over the elements of one range of a <see cref="PersistentSortedSet{T, TComparer}"/>,
    /// returned by <see cref="EnumerateRange"/>.
    /// </summary>
    public readonly struct RangeEnumerable : IEnumerable<T>
    {
        private readonly PersistentSortedSet<T, TComparer> _set;
        private readonly T _from;
        private readonly T _toExclusive;

        internal RangeEnumerable(PersistentSortedSet<T, TComparer> set, T from, T toExclusive)
        {
            _set = set;
            _from = from;
            _toExclusive = toExclusive;
        }

        /// <summary>Returns a struct enumerator over the elements in the range, in ascending order.</summary>
        /// <returns>A struct enumerator over the matching elements.</returns>
        public Enumerator GetEnumerator() => new(_set, _from, _toExclusive);

        /// <inheritdoc/>
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A struct enumerator over the elements of a <see cref="PersistentSortedSet{T, TComparer}"/> in ascending
    /// order — the whole set, or one range of it.
    /// </summary>
    /// <remarks>
    /// The set cannot be modified, so this enumerator carries no version check and nothing can invalidate it.
    /// The root-to-leaf path is held inline in the struct — the tree is at most eight levels deep — which is
    /// what makes enumeration allocation-free.
    /// </remarks>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly Node _root;
        private readonly T _from;
        private readonly T _toExclusive;
        private readonly bool _bounded;

        // The branches from the root down to the current leaf, and which child of each the walk is in.
        private NodeStack _path;
        private CursorStack _cursors;
        private int _depth;
        private Node? _leaf;
        private int _index;
        private bool _finished;
        private T _current;

        internal Enumerator(PersistentSortedSet<T, TComparer> set)
            : this(set, default!, default!, bounded: false)
        {
        }

        internal Enumerator(PersistentSortedSet<T, TComparer> set, T from, T toExclusive)
            : this(set, from, toExclusive, bounded: true)
        {
        }

        private Enumerator(PersistentSortedSet<T, TComparer> set, T from, T toExclusive, bool bounded)
        {
            _root = set._root;
            _from = from;
            _toExclusive = toExclusive;
            _bounded = bounded;
            _path = default;
            _cursors = default;
            _depth = 0;
            _leaf = null;
            _index = 0;
            _finished = false;
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
            if (_finished)
                return false;

            if (_leaf is null)
                Seek();
            else
                _index++;

            // Step to the next leaf when this one is spent. Only an empty root leaf has no elements at all;
            // every other leaf holds at least MinFill, so one step always lands on an element.
            if (_index >= _leaf!.Count && !NextLeaf())
                return Finish();

            T item = _leaf.Keys[_index];
            if (_bounded && Compare(item, _toExclusive) >= 0)
                return Finish();

            _current = item;
            return true;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first element.
        /// </summary>
        public void Reset()
        {
            _depth = 0;
            _leaf = null;
            _index = 0;
            _finished = false;
            _current = default!;
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
            _current = default!;
            return false;
        }

        // The first descent: down the leftmost path, or down the path to the range's lower bound.
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

    // The enumerator's inline descent stacks. MaxDepth bounds every tree int.MaxValue elements can build, so
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
