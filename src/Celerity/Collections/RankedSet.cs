using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <see cref="RankedSet{T, TComparer}"/> ordered by <see cref="Comparer{T}.Default"/> — the convenience
/// alias that closes over <see cref="DefaultComparer{T}"/>, exactly as <see cref="BTreeSet{T}"/> fronts its
/// comparer-parameterized form.
/// </summary>
/// <typeparam name="T">The element type. Must be orderable by <see cref="Comparer{T}.Default"/>.</typeparam>
public class RankedSet<T> : RankedSet<T, DefaultComparer<T>>
{
    /// <summary>Initializes a new, empty set ordered by <see cref="Comparer{T}.Default"/>.</summary>
    public RankedSet()
    {
    }

    /// <summary>
    /// Initializes a new set ordered by <see cref="Comparer{T}.Default"/> and seeded with
    /// <paramref name="source"/>. Duplicates in <paramref name="source"/> are ignored.
    /// </summary>
    /// <param name="source">The elements to insert. Enumeration order does not affect the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public RankedSet(IEnumerable<T> source)
        : base(source)
    {
    }
}

/// <summary>
/// An <b>order-statistics set</b>: a sorted set that also answers the two positional questions no BCL ordered
/// container can — <i>where does this element rank?</i> (<see cref="IndexOf"/> for one that is present,
/// <see cref="CountLessThan"/> for the rank one <i>would</i> take whether or not it is) and <i>what is the
/// k-th smallest?</i> (<see cref="this[int]"/>) — both in <c>O(log n)</c>, on a set that is still being
/// inserted into and removed from.
/// </summary>
/// <typeparam name="T">The element type, ordered by <typeparamref name="TComparer"/>.</typeparam>
/// <typeparam name="TComparer">
/// The comparer that defines the element order. Must be a value type implementing
/// <see cref="IComparer{T}"/> so the JIT can devirtualize and inline it — an interface-typed comparer would
/// cost a virtual call for every element inspected by a binary search. Use <see cref="DefaultComparer{T}"/>
/// (or the one-parameter <see cref="RankedSet{T}"/> alias) for the natural order.
/// </typeparam>
/// <remarks>
/// <para>
/// This library could already rank and select three different ways, and never over a set that changes:
/// <see cref="RankSelectBitVector"/> over an <i>immutable</i> bit vector, <see cref="FenwickTree{T}"/> and
/// <see cref="SegmentTree{T, TMonoid}"/> over a <i>fixed-length</i> sequence of positions. The ordered
/// containers — <see cref="BTreeSet{T, TComparer}"/>, <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>,
/// <see cref="Trie{TValue}"/> — have no rank at all. This type is that missing corner.
/// </para>
/// <para>
/// <b>The BCL has no counterpart</b>, which is the unusual part of the case for this type.
/// <see cref="SortedSet{T}"/> exposes neither a rank nor a positional accessor, so the answers are
/// <c>set.ElementAt(k)</c> — <c>O(k)</c> enumeration — and <c>set.Count(x =&gt; x &lt; v)</c>, which is
/// <c>O(n)</c>. <see cref="SortedList{TKey, TValue}"/> <i>does</i> have <c>IndexOfKey</c> and positional
/// access, but its insert and remove memmove one contiguous array and are <c>O(n)</c>, which is the same
/// trade a hand-rolled sorted <see cref="List{T}"/> makes. Nothing in .NET is <c>O(log n)</c> on both halves,
/// and that is the gap this fills: live leaderboards (<i>what rank is this score, and who is 500th</i>),
/// exact percentiles over a moving window, a sweep line that needs the median of its active set.
/// </para>
/// <para>
/// <b>The layout.</b> Elements live in a jagged array of sorted buckets — <b>sqrt decomposition</b>, with a
/// Fenwick tree over the bucket lengths carrying the positional half. Finding an element is a binary search
/// over the per-bucket maxima followed by a binary search inside one contiguous, prefetch-friendly array;
/// <see cref="IndexOf"/> adds one prefix sum over that tree, and the indexer is one binary-lifting descent
/// down it plus a direct array index. A bucket grows in place while it is smaller than the capacity the
/// current element count calls for, and splits in half only once it is at that capacity; it merges with a
/// neighbour when it falls below a quarter of it and the union fits in half.
/// </para>
/// <para>
/// <b>The cost, stated exactly.</b> <see cref="Contains"/>, <see cref="IndexOf"/>,
/// <see cref="CountLessThan"/>, the bounds and <see cref="this[int]"/> are <c>O(log n)</c>.
/// <b><see cref="Add"/> and <see cref="Remove"/> are <c>O(√n)</c></b>, not <c>O(log n)</c>: two binary
/// searches and an <c>O(log b)</c> Fenwick update, plus a memmove of at most one bucket. That memmove is the
/// term that grows, and it is the cheapest linear-time operation the machine has: one bounded, contiguous
/// copy, against a tree's chain of dependent pointer loads. The structural work — a split shifts every later
/// bucket slot and rebuilds the tree, <c>Θ(b)</c> — is what the capacity rule is for: holding the bucket
/// capacity at <c>Θ(√n)</c> pins <c>b</c> at <c>Θ(√n)</c> and the number of splits at <c>Θ(√n)</c>, so their
/// total is <c>Θ(n)</c> and the amortized structural cost per mutation is a constant. A fixed capacity would
/// instead leave it growing with <c>n</c>.
/// </para>
/// <para>
/// <b>The one place that bound is off, and what to do about it.</b> Neither a bucket's array nor the slot
/// array behind the Fenwick tree is ever narrowed as elements leave, so after a contraction both terms are in
/// the <i>high-water</i> count rather than the current one:
/// </para>
/// <list type="bullet">
/// <item><description>
/// an ordinary insert or remove shifts a bucket sized for what the set used to hold —
/// <c>O(min(n, √Nmax))</c>;
/// </description></item>
/// <item><description>
/// a <b>structural</b> one — the merge or the bucket drop a removal can trigger — rebuilds the tree across
/// every historical slot, so it is <c>Θ(√Nmax)</c> outright, with no <c>min</c> to save it.
/// </description></item>
/// </list>
/// <para>
/// A set that grew to a hundred million and shrank to ten thousand pays those rather than <c>O(√n)</c>.
/// Growth is where the capacity rule is exercised and contraction is where it is not, so this costs nothing
/// to a set whose size is roughly stable — a sliding window, a leaderboard — and it is the reason
/// <see cref="TrimExcess"/> exists: one <c>O(n)</c> rebuild at the current size puts both bounds back.
/// <see cref="Clear"/> resets them too, by releasing everything.
/// </para>
/// <para>
/// <b>Where it wins and where it does not.</b> The documented win is the mixed workload the type exists for —
/// interleaved insert and remove with rank and select queries against the live set — where the
/// <see cref="SortedSet{T}"/> answer is linear and the sorted-<see cref="List{T}"/> answer pays an
/// <c>O(n)</c> memmove per mutation. It does <i>not</i> win at pure selection against a sorted
/// <see cref="List{T}"/>, where indexing one array is <c>O(1)</c> and unbeatable, and it is not a
/// <see cref="BTreeSet{T, TComparer}"/> replacement: reach for that one when the positional questions are
/// never asked. Small sets fit in cache and every option is competitive there. The measured ratios are in
/// <c>docs/api/collections.md</c>.
/// </para>
/// <para>
/// <b>Footprint.</b> One array object per bucket — roughly one per three quarters of a bucket capacity in
/// steady state — and no per-element node, against <see cref="SortedSet{T}"/>'s one heap node per element.
/// Bucket arrays are allocated at their full capacity, so element storage runs about <c>1.3x</c> the elements
/// themselves in steady state and up to <c>2x</c> immediately after a round of splits. The capacity only ever
/// rises with the element count; a set that has been large and is now small keeps the wider arrays until it
/// is <see cref="TrimExcess"/>ed or <see cref="Clear"/>ed.
/// </para>
/// <para>
/// Membership is defined by <typeparamref name="TComparer"/> — two elements are the same element when the
/// comparer orders them equal. The <see cref="ISet{T}"/> algebra members materialize the right-hand side into
/// a <see cref="HashSet{T}"/>, so they compare <i>that</i> side with
/// <see cref="EqualityComparer{T}.Default"/>, matching the rest of the family. That matters only for a
/// comparer that orders two elements equal when <see cref="EqualityComparer{T}.Default"/> does not — a
/// case-insensitive order, say — and then it matters for exactly the four members that ask whether an
/// element of <i>this</i> set is in the right-hand side: <see cref="IntersectWith"/>,
/// <see cref="SetEquals"/>, <see cref="IsSubsetOf"/> and <see cref="IsProperSubsetOf"/>. Under such an order
/// a set holding <c>"a"</c> answers <c>true</c> to <c>Contains("A")</c> and still empties under
/// <c>IntersectWith(["A"])</c>, which is where this differs from <see cref="SortedSet{T}"/>. The members that
/// only ever probe this set — <see cref="UnionWith"/>, <see cref="ExceptWith"/>,
/// <see cref="SymmetricExceptWith"/>, <see cref="Overlaps"/>, <see cref="IsSupersetOf"/> and
/// <see cref="IsProperSupersetOf"/> — follow the comparer throughout. Like the rest of the family,
/// <see cref="Add"/> throws on a duplicate — use <see cref="TryAdd"/> when the element may already be
/// present. Whether a <c>null</c> element is legal is <typeparamref name="TComparer"/>'s decision, not this
/// type's: under <see cref="DefaultComparer{T}"/> — and so under the <see cref="RankedSet{T}"/> alias —
/// <see cref="Comparer{T}.Default"/> orders <c>null</c> before every non-<c>null</c> element and it is an
/// ordinary member, while a hand-written comparer that dereferences its arguments will throw on one. This
/// type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// <example>
/// <code>
/// var scores = new RankedSet&lt;int&gt;([120, 340, 90, 500, 275]);
///
/// Console.WriteLine(scores[0]);                  // 90  — the smallest
/// Console.WriteLine(scores[scores.Count - 1]);   // 500 — the largest
/// Console.WriteLine(scores.IndexOf(275));        // 2   — its rank in sorted order
/// Console.WriteLine(scores.CountLessThan(300));  // 3   — 90, 120, 275
///
/// scores.TryAdd(310);
/// Console.WriteLine(scores.IndexOf(275));        // 2   — still O(log n), on the live set
/// </code>
/// </example>
/// </remarks>
public class RankedSet<T, TComparer> : ISet<T>, IReadOnlySet<T>, IReadOnlyList<T>
    where TComparer : struct, IComparer<T>
{
    /// <summary>
    /// The smallest a bucket is ever made. The memmove an insert costs is half a bucket on average, so at
    /// this size it is one to two kilobytes for the element widths that dominate ordered-set use — a handful
    /// of cache lines, and cheaper than the tree descent it replaces.
    /// </summary>
    private const int MinBucketCapacity = 512;

    /// <summary>The initial number of bucket slots. A power of two, as the Fenwick descent requires.</summary>
    private const int InitialSlots = 4;

    // The elements, in ascending order across buckets and inside each one. Only [0, _bucketCount) is live,
    // and only _lengths[i] of bucket i is meaningful; a bucket's capacity is its array's own length, which
    // differs between buckets as the target capacity grows. No live bucket is ever empty, which is what lets
    // a rank descent and the enumerators skip that case.
    private T[][] _buckets = [];
    private int[] _lengths = [];

    // The last live element of each bucket, kept out of line so locating the owning bucket is a binary search
    // over one contiguous array rather than a walk that dereferences a bucket per probe.
    private T[] _maxes = [];

    // A Fenwick tree over the bucket lengths, 1-based, of length slots + 1 where slots is _buckets.Length and
    // a power of two. This is what makes rank and select O(log b) rather than O(b): a prefix sum gives the
    // number of elements before a bucket, and a binary-lifting descent gives the bucket holding a rank.
    private int[] _tree = new int[1];

    private int _bucketCount;

    // Deliberately not `readonly`: an interface call through a readonly field of an unconstrained struct type
    // forces a defensive copy per call, which is the cost the struct-comparer design exists to remove.
    private TComparer _comparer;

    private int _count;

    // Bumped on every mutation that changes the observable content, so active enumerators detect concurrent
    // modification. A lookup, a failed TryAdd, and a Remove of an absent element are not mutations.
    private int _version;

    /// <summary>Initializes a new, empty set ordered by <c>default(TComparer)</c>.</summary>
    public RankedSet()
    {
    }

    /// <summary>Initializes a new, empty set ordered by <paramref name="comparer"/>.</summary>
    /// <param name="comparer">The comparer defining the element order.</param>
    public RankedSet(TComparer comparer)
    {
        _comparer = comparer;
    }

    /// <summary>
    /// Initializes a new set ordered by <c>default(TComparer)</c> and seeded with <paramref name="source"/>.
    /// Duplicates in <paramref name="source"/> are ignored.
    /// </summary>
    /// <param name="source">The elements to insert. Enumeration order does not affect the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public RankedSet(IEnumerable<T> source)
        : this(source, default)
    {
    }

    /// <summary>
    /// Initializes a new set ordered by <paramref name="comparer"/> and seeded with <paramref name="source"/>.
    /// Duplicates in <paramref name="source"/> are ignored.
    /// </summary>
    /// <param name="source">The elements to insert. Enumeration order does not affect the result.</param>
    /// <param name="comparer">The comparer defining the element order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public RankedSet(IEnumerable<T> source, TComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(source);

        _comparer = comparer;
        foreach (T item in source)
            Insert(item);
    }

    /// <summary>Gets the comparer that defines this set's element order.</summary>
    public TComparer Comparer => _comparer;

    /// <summary>Gets the number of elements in the set.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets the element at <paramref name="index"/> in ascending order — the <c>(index + 1)</c>-th smallest —
    /// in <c>O(log n)</c>. This is the accessor <see cref="SortedSet{T}"/> does not have; its nearest
    /// equivalent, <c>set.ElementAt(index)</c>, walks the tree and is <c>O(index)</c>.
    /// </summary>
    /// <param name="index">The zero-based rank of the element to return.</param>
    /// <returns>The element occupying <paramref name="index"/> in sorted order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or at least <see cref="Count"/>.
    /// </exception>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));

            Locate(index, out int bucket, out int offset);
            return _buckets[bucket][offset];
        }
    }

    /// <summary>Gets the smallest element, in <c>O(1)</c>.</summary>
    /// <returns>The first element in order.</returns>
    /// <exception cref="InvalidOperationException">The set is empty.</exception>
    public T Min =>
        TryGetMin(out T item) ? item : throw new InvalidOperationException("The set is empty.");

    /// <summary>Gets the largest element, in <c>O(1)</c>.</summary>
    /// <returns>The last element in order.</returns>
    /// <exception cref="InvalidOperationException">The set is empty.</exception>
    public T Max =>
        TryGetMax(out T item) ? item : throw new InvalidOperationException("The set is empty.");

    /// <summary>
    /// Adds an element, throwing when it is already present. <c>O(√n)</c> — see the type's remarks; the
    /// growing term is a memmove of at most one bucket, not a comparison count.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <exception cref="ArgumentException"><paramref name="item"/> is already present.</exception>
    public void Add(T item)
    {
        if (!Insert(item))
            throw new ArgumentException($"The element '{item}' already exists in the set.", nameof(item));
    }

    /// <summary>
    /// Adds an element only when it is absent, in <c>O(√n)</c>. The non-throwing counterpart of
    /// <see cref="Add"/>.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns><c>true</c> if the element was added; <c>false</c> if it was already present.</returns>
    public bool TryAdd(T item) => Insert(item);

    /// <summary>Determines whether <paramref name="item"/> is present, in <c>O(log n)</c>.</summary>
    /// <param name="item">The element to look for.</param>
    /// <returns><c>true</c> if the element is present.</returns>
    public bool Contains(T item)
    {
        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
            return false;

        LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item, out bool found);
        return found;
    }

    /// <summary>
    /// Gets the rank of <paramref name="item"/> — the number of elements that order before it — in
    /// <c>O(log n)</c>, or <c>-1</c> when it is absent.
    /// </summary>
    /// <param name="item">The element to rank.</param>
    /// <returns>The zero-based position of <paramref name="item"/> in sorted order, or <c>-1</c>.</returns>
    public int IndexOf(T item)
    {
        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
            return -1;

        int offset = LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item, out bool found);
        return found ? PrefixSum(bucket) + offset : -1;
    }

    /// <summary>
    /// Counts the elements that order strictly before <paramref name="item"/>, in <c>O(log n)</c> — the rank
    /// <paramref name="item"/> would occupy, whether or not it is present, which is what a percentile query
    /// needs. <see cref="IndexOf"/> answers the same question only for elements that are in the set.
    /// </summary>
    /// <param name="item">The element to bound by.</param>
    /// <returns>The number of elements smaller than <paramref name="item"/>.</returns>
    public int CountLessThan(T item)
    {
        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
            return _count;

        int offset = LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item, out _);
        return PrefixSum(bucket) + offset;
    }

    /// <summary>
    /// Counts the elements that order before <paramref name="item"/> or equal to it, in <c>O(log n)</c>.
    /// </summary>
    /// <param name="item">The element to bound by.</param>
    /// <returns>The number of elements not greater than <paramref name="item"/>.</returns>
    public int CountLessThanOrEqual(T item)
    {
        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
            return _count;

        int offset = LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item, out bool found);
        return PrefixSum(bucket) + offset + (found ? 1 : 0);
    }

    /// <summary>Removes <paramref name="item"/>, in <c>O(√n)</c> — see the type's remarks.</summary>
    /// <param name="item">The element to remove.</param>
    /// <returns><c>true</c> if an element was removed; <c>false</c> if it was absent.</returns>
    public bool Remove(T item)
    {
        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
            return false;

        int offset = LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item, out bool found);
        if (!found)
            return false;

        Delete(bucket, offset);
        return true;
    }

    /// <summary>
    /// Removes the element at <paramref name="index"/> in ascending order, in <c>O(√n)</c> — finding it is
    /// <c>O(log n)</c> and the removal shifts at most one bucket. No BCL ordered container offers removal by
    /// rank at all.
    /// </summary>
    /// <param name="index">The zero-based rank of the element to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or at least <see cref="Count"/>.
    /// </exception>
    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Locate(index, out int bucket, out int offset);
        Delete(bucket, offset);
    }

    /// <summary>
    /// Removes every element. The set releases its buckets <i>and</i> the slot and index arrays behind them,
    /// so it is left exactly as a new one — which is what makes this reset the high-water terms in the
    /// remarks, and not merely empty the set.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        _buckets = [];
        _lengths = [];
        _maxes = [];
        _tree = new int[1];
        _bucketCount = 0;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Rebuilds the set at the bucket capacity its <i>current</i> element count calls for, in <c>O(n)</c>.
    /// </summary>
    /// <remarks>
    /// A bucket's array is never narrowed as elements leave, so a set that has contracted sharply keeps
    /// buckets sized for what it used to hold — and a mutation shifts one of those, which is the one case
    /// where the documented <c>O(√n)</c> is really <c>O(√(high-water n))</c>. This puts it back, and packs
    /// the buckets three-quarters full so there is room to insert without splitting immediately. Call it
    /// after a bulk removal; there is no reason to call it on a set that has only ever grown. It invalidates
    /// active enumerators — the elements do not change, but they move — and is a no-op on an empty set that
    /// holds no buckets.
    /// </remarks>
    public void TrimExcess()
    {
        if (_count == 0)
        {
            if (_buckets.Length == 0)
                return;

            _buckets = [];
            _lengths = [];
            _maxes = [];
            _tree = new int[1];
            return;
        }

        int capacity = TargetCapacity(_count);
        int fill = capacity - (capacity / 4);
        int needed = ((_count - 1) / fill) + 1;

        int slots = InitialSlots;
        while (slots < needed)
            slots <<= 1;

        var ordered = new T[_count];
        CopyTo(ordered, 0);

        var buckets = new T[slots][];
        var lengths = new int[slots];
        var maxes = new T[slots];

        int written = 0;
        int bucket = 0;
        while (written < _count)
        {
            int take = Math.Min(fill, _count - written);
            var packed = new T[capacity];
            Array.Copy(ordered, written, packed, 0, take);

            buckets[bucket] = packed;
            lengths[bucket] = take;
            maxes[bucket] = packed[take - 1];

            written += take;
            bucket++;
        }

        _buckets = buckets;
        _lengths = lengths;
        _maxes = maxes;
        _bucketCount = bucket;
        _tree = new int[slots + 1];
        RebuildTree();
        _version++;
    }

    /// <summary>Gets the smallest element.</summary>
    /// <param name="item">The first element in order, or <c>default</c> when the set is empty.</param>
    /// <returns><c>true</c> if the set is non-empty.</returns>
    public bool TryGetMin(out T item)
    {
        if (_bucketCount == 0)
        {
            item = default!;
            return false;
        }

        item = _buckets[0][0];
        return true;
    }

    /// <summary>Gets the largest element.</summary>
    /// <param name="item">The last element in order, or <c>default</c> when the set is empty.</param>
    /// <returns><c>true</c> if the set is non-empty.</returns>
    public bool TryGetMax(out T item)
    {
        if (_bucketCount == 0)
        {
            item = default!;
            return false;
        }

        item = _maxes[_bucketCount - 1];
        return true;
    }

    /// <summary>
    /// Finds the smallest element that is <b>greater than or equal to</b> <paramref name="item"/> — the
    /// <c>lower_bound</c> of the ordered containers — in <c>O(log n)</c>. An exact match is its own lower
    /// bound.
    /// </summary>
    /// <param name="item">The element to bound.</param>
    /// <param name="bound">The bounding element, or <c>default</c> when every element is smaller.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetLowerBound(T item, out T bound)
    {
        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
        {
            bound = default!;
            return false;
        }

        // The bucket's maximum is at least `item`, so the in-bucket lower bound cannot run off its end.
        int offset = LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item, out _);
        bound = _buckets[bucket][offset];
        return true;
    }

    /// <summary>
    /// Finds the smallest element <b>strictly greater than</b> <paramref name="item"/> — the
    /// <c>upper_bound</c> of the ordered containers — in <c>O(log n)</c>.
    /// </summary>
    /// <param name="item">The element to bound.</param>
    /// <param name="bound">The bounding element, or <c>default</c> when no element is larger.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetUpperBound(T item, out T bound)
    {
        bound = default!;

        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
            return false;

        int offset = LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item, out bool found);
        if (found)
            offset++;

        // An exact hit on the bucket's maximum pushes the bound into the next bucket, if there is one.
        if (offset == _lengths[bucket])
        {
            bucket++;
            if (bucket == _bucketCount)
                return false;

            offset = 0;
        }

        bound = _buckets[bucket][offset];
        return true;
    }

    /// <summary>
    /// Enumerates, in ascending order, every element in the half-open range
    /// <c>[fromInclusive, toExclusive)</c>. The scan seeks to the lower bound in <c>O(log n)</c> and then
    /// walks the buckets' contiguous arrays, so it costs <c>O(log n + k)</c> for <c>k</c> results rather than
    /// a full <c>O(n)</c> filter.
    /// </summary>
    /// <param name="fromInclusive">The inclusive lower bound of the range.</param>
    /// <param name="toExclusive">The exclusive upper bound of the range.</param>
    /// <returns>An allocation-free enumerable over the matching elements.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="toExclusive"/> orders before <paramref name="fromInclusive"/>.
    /// </exception>
    public RangeEnumerable EnumerateRange(T fromInclusive, T toExclusive)
    {
        if (_comparer.Compare(fromInclusive, toExclusive) > 0)
            throw new ArgumentException(
                "toExclusive must not order before fromInclusive.", nameof(toExclusive));

        return new RangeEnumerable(this, fromInclusive, toExclusive);
    }

    /// <summary>
    /// Returns a struct enumerator over the elements in ascending order. Because it is a struct that holds
    /// only a bucket and an offset, iterating it via <c>foreach</c> allocates nothing.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── ISet<T> / ICollection<T> set-algebra surface ──────────────────────────
    // Shared across the mutable set family via SetOperations, written once against the ISet<T> primitives
    // every set exposes; the semantics match BCL HashSet<T>.

    /// <summary>
    /// Modifies the set to contain all elements that are present in itself, in <paramref name="other"/>, or
    /// in both.
    /// </summary>
    /// <param name="other">The collection to union into this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void UnionWith(IEnumerable<T> other) => SetOperations.UnionWith(this, other);

    /// <summary>Modifies the set to contain only elements that are also present in <paramref name="other"/>.</summary>
    /// <param name="other">The collection to intersect with this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void IntersectWith(IEnumerable<T> other) => SetOperations.IntersectWith(this, other);

    /// <summary>Removes every element in <paramref name="other"/> from the set.</summary>
    /// <param name="other">The collection of elements to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void ExceptWith(IEnumerable<T> other) => SetOperations.ExceptWith(this, other);

    /// <summary>
    /// Modifies the set to contain only elements that are present either in itself or in
    /// <paramref name="other"/>, but not both.
    /// </summary>
    /// <param name="other">The collection to apply the symmetric difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void SymmetricExceptWith(IEnumerable<T> other) => SetOperations.SymmetricExceptWith(this, other);

    /// <summary>Determines whether the set is a subset of <paramref name="other"/>.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of this set is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSubsetOf(IEnumerable<T> other) => SetOperations.IsSubsetOf(this, other);

    /// <summary>Determines whether the set is a proper (strict) subset of <paramref name="other"/>.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of this set is in <paramref name="other"/> and <paramref name="other"/>
    /// has at least one element this set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<T> other) => SetOperations.IsProperSubsetOf(this, other);

    /// <summary>Determines whether the set is a superset of <paramref name="other"/>.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of <paramref name="other"/> is in this set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<T> other) => SetOperations.IsSupersetOf(this, other);

    /// <summary>Determines whether the set is a proper (strict) superset of <paramref name="other"/>.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of <paramref name="other"/> is in this set and this set has at least one
    /// element <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSupersetOf(IEnumerable<T> other) => SetOperations.IsProperSupersetOf(this, other);

    /// <summary>Determines whether the set and <paramref name="other"/> share at least one element.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool Overlaps(IEnumerable<T> other) => SetOperations.Overlaps(this, other);

    /// <summary>Determines whether the set and <paramref name="other"/> contain the same distinct elements.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<T> other) => SetOperations.SetEquals(this, other);

    /// <summary>
    /// Copies the elements of the set, in ascending order, to <paramref name="array"/> starting at
    /// <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(T[] array, int arrayIndex) => SetOperations.CopyTo(this, _count, array, arrayIndex);

    // Adds the element, returning whether it was newly added (ISet<T> semantics) — the non-throwing
    // counterpart of the public throw-on-duplicate Add(T).
    bool ISet<T>.Add(T item) => TryAdd(item);

    // ICollection<T>.Add must not throw on a duplicate (unlike the public Add(T)), so it maps to TryAdd.
    void ICollection<T>.Add(T item) => TryAdd(item);

    bool ICollection<T>.IsReadOnly => false;

    // ---- internals ---------------------------------------------------------------------------------

    // The index of the first bucket whose maximum is not smaller than `item` — the only bucket that can hold
    // it — or _bucketCount when `item` orders after every element. Binary search over the out-of-line maxima:
    // the bucket count is in the low thousands even at a million elements, so this array stays in cache.
    private int LocateBucket(T item)
    {
        int lo = 0;
        int hi = _bucketCount - 1;

        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (_comparer.Compare(_maxes[mid], item) < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return lo;
    }

    // Binary search inside one bucket. Returns the index of the match when `found`, and otherwise the index
    // of the first element greater than `item` — which is both the insertion point and the number of elements
    // in this bucket that order before `item`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LowerBoundInBucket(T[] bucket, int length, T item, out bool found)
    {
        int lo = 0;
        int hi = length - 1;

        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            int cmp = _comparer.Compare(bucket[mid], item);
            if (cmp == 0)
            {
                found = true;
                return mid;
            }

            if (cmp < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        found = false;
        return lo;
    }

    private bool Insert(T item)
    {
        if (_bucketCount == 0)
        {
            CreateFirstBucket(item);
            return true;
        }

        // An element past every maximum belongs at the end of the last bucket. LocateBucket reports that as
        // "no bucket can hold it", which is the answer a membership test needs and not the one an insert does.
        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
            bucket--;

        int offset = LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item, out bool found);
        if (found)
            return false;

        if (_lengths[bucket] == _buckets[bucket].Length)
        {
            // Growing a full bucket in place is free of structural work — no slot shifts, no index rebuild —
            // so it is preferred while the bucket is still smaller than what this many elements calls for.
            // Only a bucket already at the target capacity splits, and that is what bounds the bucket count
            // to O(sqrt n) and amortizes the split's cost away. See TargetCapacity.
            int targetCapacity = TargetCapacity(_count + 1);
            if (_buckets[bucket].Length < targetCapacity)
            {
                GrowBucket(bucket, targetCapacity);
            }
            else
            {
                int half = SplitBucket(bucket);

                // The insertion point moves with the elements. An offset of exactly `half` stays on the left
                // half, appended at its end, which the split left room for.
                if (offset > half)
                {
                    bucket++;
                    offset -= half;
                }
            }
        }

        T[] target = _buckets[bucket];
        int length = _lengths[bucket];
        Array.Copy(target, offset, target, offset + 1, length - offset);
        target[offset] = item;
        _lengths[bucket] = ++length;

        if (offset == length - 1)
            _maxes[bucket] = item;

        AddToTree(bucket, 1);
        _count++;
        _version++;
        return true;
    }

    private void CreateFirstBucket(T item)
    {
        if (_buckets.Length == 0)
            AllocateSlots(InitialSlots);

        _buckets[0] = new T[MinBucketCapacity];
        _buckets[0][0] = item;
        _lengths[0] = 1;
        _maxes[0] = item;
        _bucketCount = 1;

        AddToTree(0, 1);
        _count = 1;
        _version++;
    }

    // The capacity a bucket is grown or split at, for a set of `count` elements: the smallest power of two
    // whose square is at least `count`, never below MinBucketCapacity. Callers pass the count the set will
    // have once the mutation in flight has landed — at a square boundary the pre-insert count still asks for
    // the narrower capacity, which would split the bucket the rule says to grow.
    //
    // This is what keeps the structure's cost honest, and it is not a tuning knob. A split inserts a bucket
    // between two others, so it shifts every later slot and rebuilds the Fenwick tree over them — Theta(b) in
    // the bucket count. With a *fixed* capacity C, b grows as n / C and a split happens every C / 2 inserts,
    // so that Theta(b) work amortizes to Theta(n / C²) *per insert* — it grows with n, and past a few tens of
    // millions of elements it dominates everything else. Holding the capacity at Theta(sqrt n) instead pins
    // b at Theta(sqrt n) and the splits at Theta(sqrt n) of them, so their total is Theta(n) and the
    // amortized structural cost per mutation is a constant. What is left growing is the in-bucket memmove,
    // at Theta(sqrt n) — which is why Add and Remove are documented as O(sqrt n) rather than O(log n), and
    // why the constant matters: a memmove is the cheapest linear-time operation the machine has.
    //
    // The loop terminates without a clamp: `int` counts stop below 65536², so the capacity stops at 65536.
    private static int TargetCapacity(int count)
    {
        int capacity = MinBucketCapacity;
        while ((long)capacity * capacity < count)
            capacity <<= 1;

        return capacity;
    }

    // Widens one bucket's array in place. Nothing structural moves — the bucket count, the slot order and the
    // Fenwick tree are all untouched — so this is the cheap half of what a full bucket can do.
    private void GrowBucket(int bucket, int capacity)
    {
        var grown = new T[capacity];
        Array.Copy(_buckets[bucket], grown, _lengths[bucket]);
        _buckets[bucket] = grown;
    }

    // Splits a full bucket into two half-full ones, so the insert that triggered it has room, and returns the
    // length of the left half. The halves are adjacent, so every later bucket shifts up one slot and the
    // Fenwick tree is rebuilt — Theta(b), which is what TargetCapacity amortizes away.
    private int SplitBucket(int bucket)
    {
        if (_bucketCount == _buckets.Length)
            AllocateSlots(_buckets.Length * 2);

        int tail = _bucketCount - bucket - 1;
        Array.Copy(_buckets, bucket + 1, _buckets, bucket + 2, tail);
        Array.Copy(_lengths, bucket + 1, _lengths, bucket + 2, tail);
        Array.Copy(_maxes, bucket + 1, _maxes, bucket + 2, tail);

        T[] left = _buckets[bucket];
        int capacity = left.Length;
        int half = capacity / 2;
        var right = new T[capacity];
        Array.Copy(left, half, right, 0, capacity - half);
        Array.Clear(left, half, capacity - half);

        _buckets[bucket + 1] = right;
        _lengths[bucket + 1] = capacity - half;
        _maxes[bucket + 1] = _maxes[bucket];
        _lengths[bucket] = half;
        _maxes[bucket] = left[half - 1];
        _bucketCount++;

        RebuildTree();
        return half;
    }

    private void Delete(int bucket, int offset)
    {
        T[] target = _buckets[bucket];
        int length = _lengths[bucket] - 1;
        Array.Copy(target, offset + 1, target, offset, length - offset);
        target[length] = default!;
        _lengths[bucket] = length;
        AddToTree(bucket, -1);
        _count--;
        _version++;

        if (length == 0)
        {
            DropBucket(bucket);
            return;
        }

        if (offset == length)
            _maxes[bucket] = target[length - 1];

        // Merge a thinned-out bucket into a neighbour, but only when the union stays at or below half the
        // destination's capacity — so a merge cannot be undone by the next insert, and a quarter of a bucket's
        // worth of operations has to pass before either boundary is crossed again. Both thresholds are read
        // off the destination bucket's own array, since capacities differ once the target has grown.
        if (length >= target.Length / 4)
            return;

        if (bucket + 1 < _bucketCount && length + _lengths[bucket + 1] <= target.Length / 2)
            MergeBuckets(bucket, bucket + 1);
        else if (bucket > 0 && _lengths[bucket - 1] + length <= _buckets[bucket - 1].Length / 2)
            MergeBuckets(bucket - 1, bucket);
    }

    private void MergeBuckets(int left, int right)
    {
        Array.Copy(_buckets[right], 0, _buckets[left], _lengths[left], _lengths[right]);
        _lengths[left] += _lengths[right];
        _maxes[left] = _maxes[right];
        DropBucket(right);
    }

    private void DropBucket(int bucket)
    {
        int tail = _bucketCount - bucket - 1;
        Array.Copy(_buckets, bucket + 1, _buckets, bucket, tail);
        Array.Copy(_lengths, bucket + 1, _lengths, bucket, tail);
        Array.Copy(_maxes, bucket + 1, _maxes, bucket, tail);

        _bucketCount--;
        _buckets[_bucketCount] = null!;
        _lengths[_bucketCount] = 0;
        _maxes[_bucketCount] = default!;

        RebuildTree();
    }

    // Grows the three parallel bucket arrays and the Fenwick tree to `slots`, always a power of two — the
    // descent in Locate relies on the tree's length being one.
    private void AllocateSlots(int slots)
    {
        Array.Resize(ref _buckets, slots);
        Array.Resize(ref _lengths, slots);
        Array.Resize(ref _maxes, slots);
        _tree = new int[slots + 1];
        RebuildTree();
    }

    private void RebuildTree()
    {
        int slots = _tree.Length - 1;
        Array.Clear(_tree, 0, _tree.Length);
        for (int i = 0; i < _bucketCount; i++)
            _tree[i + 1] = _lengths[i];

        // In-place build: each node folds into its parent, so the whole tree costs O(b) rather than b updates
        // of O(log b) each.
        for (int i = 1; i <= slots; i++)
        {
            int parent = i + (i & -i);
            if (parent <= slots)
                _tree[parent] += _tree[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddToTree(int bucket, int delta)
    {
        int slots = _tree.Length - 1;
        for (int i = bucket + 1; i <= slots; i += i & -i)
            _tree[i] += delta;
    }

    // The number of elements held by the buckets before `bucket`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PrefixSum(int bucket)
    {
        int sum = 0;
        for (int i = bucket; i > 0; i -= i & -i)
            sum += _tree[i];

        return sum;
    }

    // Binary lifting down the Fenwick tree: finds the bucket holding the element of rank `index` and the
    // offset inside it, in O(log b) and without touching a single element.
    //
    // The descent starts from the largest power of two that does not exceed the *live* bucket count, not from
    // the tree's capacity. The slot array is never narrowed as buckets go away, so a set that has contracted
    // would otherwise pay log2 of the most buckets it has ever held on every positional query — the mutation
    // path carries a high-water cost by design, and the query path must not.
    //
    // The caller has already checked 0 <= index < Count, and that is what lets the descent skip a bounds test
    // at every step. Either the starting stride equals the live bucket count, in which case it covers the
    // whole prefix and so can never be taken for an index below the total; or it is strictly below the bucket
    // count, in which case the tree's capacity is at least twice it and the strides sum to less than that.
    private void Locate(int index, out int bucket, out int offset)
    {
        int position = 0;
        int remaining = index;

        for (int step = 1 << BitOperations.Log2((uint)_bucketCount); step > 0; step >>= 1)
        {
            int candidate = position + step;
            if (_tree[candidate] <= remaining)
            {
                position = candidate;
                remaining -= _tree[candidate];
            }
        }

        bucket = position;
        offset = remaining;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="RankedSet{T, TComparer}"/>'s elements in ascending order.
    /// Because it is a struct holding only a bucket and an offset, iterating it via <c>foreach</c> allocates
    /// nothing.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly RankedSet<T, TComparer> _set;
        private readonly int _version;
        private int _bucket;
        private int _offset;
        private T _current;

        internal Enumerator(RankedSet<T, TComparer> set)
        {
            _set = set;
            _version = set._version;
            _bucket = 0;
            _offset = 0;
            _current = default!;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element in order.</summary>
        /// <returns><c>true</c> if there is a next element; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The set was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The set was modified during enumeration.");

            if (_bucket < _set._bucketCount)
            {
                // No live bucket is empty, so the cursor always points at an element while it is in range.
                _current = _set._buckets[_bucket][_offset];
                if (++_offset == _set._lengths[_bucket])
                {
                    _bucket++;
                    _offset = 0;
                }

                return true;
            }

            _current = default!;
            return false;
        }

        /// <summary>Resets the enumerator to before the first element.</summary>
        /// <exception cref="InvalidOperationException">The set was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The set was modified during enumeration.");

            _bucket = 0;
            _offset = 0;
            _current = default!;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }

    /// <summary>
    /// The result of <see cref="EnumerateRange"/>: an allocation-free view over the elements of one range, in
    /// ascending order.
    /// </summary>
    public readonly struct RangeEnumerable : IEnumerable<T>
    {
        private readonly RankedSet<T, TComparer> _set;
        private readonly T _from;
        private readonly T _toExclusive;

        internal RangeEnumerable(RankedSet<T, TComparer> set, T from, T toExclusive)
        {
            _set = set;
            _from = from;
            _toExclusive = toExclusive;
        }

        /// <summary>Returns a struct enumerator over the elements in the range.</summary>
        /// <returns>A struct enumerator over the matching elements.</returns>
        public RangeEnumerator GetEnumerator() => new RangeEnumerator(_set, _from, _toExclusive);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A struct enumerator over the elements of one range of a <see cref="RankedSet{T, TComparer}"/>, in
    /// ascending order.
    /// </summary>
    public struct RangeEnumerator : IEnumerator<T>
    {
        private readonly RankedSet<T, TComparer> _set;
        private readonly T _from;
        private readonly T _toExclusive;
        private readonly int _version;
        private int _bucket;
        private int _offset;
        private T _current;
        private bool _finished;

        internal RangeEnumerator(RankedSet<T, TComparer> set, T from, T toExclusive)
        {
            _set = set;
            _from = from;
            _toExclusive = toExclusive;
            _version = set._version;
            _bucket = 0;
            _offset = 0;
            _current = default!;
            _finished = false;
            Seek();
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element in the range.</summary>
        /// <returns><c>true</c> if there is a next element; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The set was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The set was modified during enumeration.");

            if (_finished || _bucket == _set._bucketCount)
            {
                _current = default!;
                return false;
            }

            T item = _set._buckets[_bucket][_offset];

            // The walk is ascending, so the first element at or past the upper bound ends the scan for good.
            if (_set._comparer.Compare(item, _toExclusive) >= 0)
            {
                _finished = true;
                _current = default!;
                return false;
            }

            if (++_offset == _set._lengths[_bucket])
            {
                _bucket++;
                _offset = 0;
            }

            _current = item;
            return true;
        }

        /// <summary>Resets the enumerator to before the first element of the range.</summary>
        /// <exception cref="InvalidOperationException">The set was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The set was modified during enumeration.");

            _bucket = 0;
            _offset = 0;
            _current = default!;
            _finished = false;
            Seek();
        }

        // Positions the cursor at the range's lower bound, or marks the scan finished when every element
        // orders before it.
        private void Seek()
        {
            _bucket = _set.LocateBucket(_from);
            if (_bucket == _set._bucketCount)
            {
                _finished = true;
                return;
            }

            _offset = _set.LowerBoundInBucket(_set._buckets[_bucket], _set._lengths[_bucket], _from, out _);
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
