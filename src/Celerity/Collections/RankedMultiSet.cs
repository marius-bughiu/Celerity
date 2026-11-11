using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <see cref="RankedMultiSet{T, TComparer}"/> ordered by <see cref="Comparer{T}.Default"/>. This is the
/// convenience alias that closes over <see cref="DefaultComparer{T}"/>, the same way
/// <see cref="RankedSet{T}"/> fronts its comparer-parameterized form.
/// </summary>
/// <typeparam name="T">The element type. Must be orderable by <see cref="Comparer{T}.Default"/>.</typeparam>
public class RankedMultiSet<T> : RankedMultiSet<T, DefaultComparer<T>>
{
    /// <summary>Initializes a new, empty multiset ordered by <see cref="Comparer{T}.Default"/>.</summary>
    public RankedMultiSet()
    {
    }

    /// <summary>
    /// Initializes a new multiset ordered by <see cref="Comparer{T}.Default"/> and seeded with
    /// <paramref name="source"/>. Every element is kept, duplicates included, and equal elements keep the
    /// order <paramref name="source"/> enumerates them in.
    /// </summary>
    /// <param name="source">The elements to insert.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public RankedMultiSet(IEnumerable<T> source)
        : base(source)
    {
    }
}

/// <summary>
/// An <b>order-statistics multiset</b>: a sorted collection that keeps duplicates and answers the positional
/// questions in <c>O(log n)</c> on a collection that is still changing. <i>How many elements order before
/// this one?</i> is <see cref="CountLessThan"/>, <i>how many copies are there?</i> is
/// <see cref="CountOf"/>, and <i>what is the k-th smallest?</i> is <see cref="this[int]"/>.
/// </summary>
/// <typeparam name="T">The element type, ordered by <typeparamref name="TComparer"/>.</typeparam>
/// <typeparam name="TComparer">
/// The comparer that defines the element order. Must be a value type implementing
/// <see cref="IComparer{T}"/> so the JIT can devirtualize and inline it. Use
/// <see cref="DefaultComparer{T}"/> (or the one-parameter <see cref="RankedMultiSet{T}"/> alias) for the
/// natural order.
/// </typeparam>
/// <remarks>
/// <para>
/// <b>The gap.</b> .NET has no sorted collection that keeps duplicates. <see cref="SortedSet{T}"/>,
/// <see cref="SortedList{TKey, TValue}"/> and <see cref="SortedDictionary{TKey, TValue}"/> all reject a
/// second equal element. The usual workarounds each cost something. A composite element such as
/// <c>SortedSet&lt;(T, long Seq)&gt;</c> widens every element and still has no rank. A hand-rolled sorted
/// <see cref="List{T}"/> memmoves half the array on every insert and remove. A count per distinct key in a
/// <see cref="SortedDictionary{TKey, TValue}"/> loses the elements themselves and has no rank either.
/// <see cref="RankedSet{T, TComparer}"/> has the rank, but it is a set, so a window of latency samples or a
/// leaderboard with tied scores silently loses its repeats. This type is <see cref="RankedSet{T, TComparer}"/>
/// with that restriction lifted.
/// </para>
/// <para>
/// <b>Equal elements are kept in insertion order.</b> <see cref="Add"/> places a new element after every
/// element the comparer orders equal to it, and <see cref="Remove"/> takes the earliest-added of them. So the
/// type is <i>stable</i>: among ties it is first in, first out, and enumeration and <see cref="this[int]"/>
/// return equal elements in the order they arrived. That matters when the comparer looks at only part of
/// the element. For example, events ordered by timestamp keep their arrival order within one timestamp.
/// The constructors that take a source insert in enumeration order, so they are stable too.
/// </para>
/// <para>
/// <b>The layout</b> is <see cref="RankedSet{T, TComparer}"/>'s: sorted buckets of <c>Θ(√n)</c> capacity
/// with a Fenwick tree over their lengths. A run of equal elements may span several buckets. Every query
/// therefore searches for a <i>boundary</i> (the first element not less than the probe, or the first one
/// greater than it) rather than for any match.
/// </para>
/// <para>
/// <b>The cost.</b> <see cref="Contains"/>, <see cref="CountOf"/>, <see cref="IndexOf"/>,
/// <see cref="LastIndexOf"/>, <see cref="CountLessThan"/>, <see cref="CountLessThanOrEqual"/>, the bounds and
/// <see cref="this[int]"/> are <c>O(log n)</c>. <see cref="Add"/>, <see cref="Remove"/> and
/// <see cref="RemoveAt"/> are <c>O(√n)</c>: an <c>O(log n)</c> search (two binary searches, or a Fenwick
/// descent for <see cref="RemoveAt"/>), an <c>O(log b)</c> Fenwick update and a memmove of at most one bucket.
/// <see cref="RemoveAll"/> removes a run of <c>k</c> equal elements in one pass: it trims the run's two end
/// buckets, drops the buckets between with one shift and rebuilds the positional index once, so it is
/// <c>O(√n + k)</c> rather than <c>k</c> separate removals. The high-water caveat <see cref="RankedSet{T, TComparer}"/> documents applies unchanged: bucket
/// arrays are never narrowed as elements leave, and <see cref="TrimExcess"/> is the remedy after a sharp
/// contraction.
/// </para>
/// <para>
/// <b>When not to use it.</b> When elements are distinct, <see cref="RankedSet{T, TComparer}"/> is the same
/// structure with set semantics and the <see cref="ISet{T}"/> algebra. When the collection is built once and
/// only queried, a sorted array is <c>O(1)</c> to index and unbeatable. At about a thousand elements the
/// hand-rolled sorted <see cref="List{T}"/> is competitive or faster, because its memmove fits in cache. The
/// measured ratios are in <c>docs/api/collections.md</c>.
/// </para>
/// <para>
/// Whether a <c>null</c> element is legal is <typeparamref name="TComparer"/>'s decision. Under
/// <see cref="DefaultComparer{T}"/>, <see cref="Comparer{T}.Default"/> orders <c>null</c> first and it is an
/// ordinary element. This type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// <example>
/// <code>
/// // The last 1,000 latency samples, ties and all, with any percentile on demand.
/// var window = new RankedMultiSet&lt;int&gt;();
/// var arrivals = new Queue&lt;int&gt;();
/// foreach (int latency in samples)
/// {
///     window.Add(latency);
///     arrivals.Enqueue(latency);
///     if (arrivals.Count &gt; 1_000)
///     {
///         window.Remove(arrivals.Dequeue());   // removes one copy, not every equal sample
///     }
///
///     int p50 = window[(window.Count - 1) / 2];
///     int p99 = window[(window.Count - 1) * 99 / 100];
///     int at250 = window.CountOf(250);            // how many samples were exactly 250 ms
/// }
/// </code>
/// </example>
/// </remarks>
public class RankedMultiSet<T, TComparer> : ICollection<T>, IReadOnlyList<T>
    where TComparer : struct, IComparer<T>
{
    /// <summary>The smallest a bucket is ever made. Matches <see cref="RankedSet{T, TComparer}"/>.</summary>
    private const int MinBucketCapacity = 512;

    /// <summary>The initial number of bucket slots. A power of two, as the Fenwick descent requires.</summary>
    private const int InitialSlots = 4;

    // The elements, in non-descending order across buckets and inside each one. Only [0, _bucketCount) is
    // live, and only _lengths[i] of bucket i is meaningful. No live bucket is ever empty.
    private T[][] _buckets = [];
    private int[] _lengths = [];

    // The last live element of each bucket, kept out of line so the bucket searches stay in one array.
    private T[] _maxes = [];

    // A 1-based Fenwick tree over the bucket lengths, of length slots + 1 where slots is a power of two.
    private int[] _tree = new int[1];

    private int _bucketCount;

    // Deliberately not `readonly`: see RankedSet. A readonly struct field forces a defensive copy per call.
    private TComparer _comparer;

    private int _count;

    // Bumped on every mutation that changes the content or moves elements (TrimExcess). A lookup, a Remove
    // of an absent element and a RemoveAll that finds nothing are not mutations.
    private int _version;

    /// <summary>Initializes a new, empty multiset ordered by <c>default(TComparer)</c>.</summary>
    public RankedMultiSet()
    {
    }

    /// <summary>Initializes a new, empty multiset ordered by <paramref name="comparer"/>.</summary>
    /// <param name="comparer">The comparer defining the element order.</param>
    public RankedMultiSet(TComparer comparer)
    {
        _comparer = comparer;
    }

    /// <summary>
    /// Initializes a new multiset ordered by <c>default(TComparer)</c> and seeded with
    /// <paramref name="source"/>. Every element is kept, and equal elements keep their enumeration order.
    /// </summary>
    /// <param name="source">The elements to insert.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public RankedMultiSet(IEnumerable<T> source)
        : this(source, default)
    {
    }

    /// <summary>
    /// Initializes a new multiset ordered by <paramref name="comparer"/> and seeded with
    /// <paramref name="source"/>. Every element is kept, and equal elements keep their enumeration order.
    /// </summary>
    /// <param name="source">The elements to insert.</param>
    /// <param name="comparer">The comparer defining the element order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public RankedMultiSet(IEnumerable<T> source, TComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(source);

        _comparer = comparer;
        foreach (T item in source)
            Add(item);
    }

    /// <summary>Gets the comparer that defines this multiset's element order.</summary>
    public TComparer Comparer => _comparer;

    /// <summary>Gets the number of elements in the multiset, counting every duplicate.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets the element at <paramref name="index"/> in ascending order (the <c>(index + 1)</c>-th smallest,
    /// counting duplicates) in <c>O(log n)</c>. Equal elements appear in the order they were added.
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

    /// <summary>Gets the smallest element, in <c>O(1)</c>. Among ties, the earliest added.</summary>
    /// <returns>The first element in order.</returns>
    /// <exception cref="InvalidOperationException">The multiset is empty.</exception>
    public T Min =>
        TryGetMin(out T item) ? item : throw new InvalidOperationException("The multiset is empty.");

    /// <summary>Gets the largest element, in <c>O(1)</c>. Among ties, the latest added.</summary>
    /// <returns>The last element in order.</returns>
    /// <exception cref="InvalidOperationException">The multiset is empty.</exception>
    public T Max =>
        TryGetMax(out T item) ? item : throw new InvalidOperationException("The multiset is empty.");

    /// <summary>
    /// Adds an element, in <c>O(√n)</c>. It is always added, even when equal elements are already present, and
    /// it is placed after all of them.
    /// </summary>
    /// <param name="item">The element to add.</param>
    public void Add(T item)
    {
        if (_bucketCount == 0)
        {
            CreateFirstBucket(item);
            return;
        }

        // The insertion point is the upper bound: after every element ordering equal to `item`, which is what
        // keeps ties in arrival order. Past every maximum it is the end of the last bucket.
        int bucket = LocateBucketAbove(item);
        if (bucket == _bucketCount)
            bucket--;

        int offset = UpperBoundInBucket(_buckets[bucket], _lengths[bucket], item);

        if (_lengths[bucket] == _buckets[bucket].Length)
        {
            // Grow in place while the bucket is under the capacity this many elements calls for, and split
            // only once it is at that capacity. See RankedSet.TargetCapacity for why this bounds the cost.
            int targetCapacity = TargetCapacity(_count + 1);
            if (_buckets[bucket].Length < targetCapacity)
            {
                GrowBucket(bucket, targetCapacity);
            }
            else
            {
                int half = SplitBucket(bucket);

                // An offset of exactly `half` stays on the left half, appended at its end.
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
    }

    /// <summary>Determines whether at least one element equal to <paramref name="item"/> is present, in <c>O(log n)</c>.</summary>
    /// <param name="item">The element to look for.</param>
    /// <returns><c>true</c> if an equal element is present.</returns>
    public bool Contains(T item)
    {
        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
            return false;

        int offset = LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item);
        return _comparer.Compare(_buckets[bucket][offset], item) == 0;
    }

    /// <summary>
    /// Counts the elements equal to <paramref name="item"/>, in <c>O(log n)</c>, however many there are.
    /// </summary>
    /// <param name="item">The element to count.</param>
    /// <returns>The number of elements the comparer orders equal to <paramref name="item"/>.</returns>
    public int CountOf(T item) => CountLessThanOrEqual(item) - CountLessThan(item);

    /// <summary>
    /// Gets the rank of the first element equal to <paramref name="item"/> (the number of elements that order
    /// strictly before it) in <c>O(log n)</c>, or <c>-1</c> when none is present.
    /// </summary>
    /// <param name="item">The element to rank.</param>
    /// <returns>The zero-based position of the first equal element, or <c>-1</c>.</returns>
    public int IndexOf(T item)
    {
        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
            return -1;

        int offset = LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item);
        return _comparer.Compare(_buckets[bucket][offset], item) == 0 ? PrefixSum(bucket) + offset : -1;
    }

    /// <summary>
    /// Gets the position of the last element equal to <paramref name="item"/>, in <c>O(log n)</c>, or
    /// <c>-1</c> when none is present.
    /// </summary>
    /// <param name="item">The element to rank.</param>
    /// <returns>The zero-based position of the last equal element, or <c>-1</c>.</returns>
    public int LastIndexOf(T item)
    {
        int upTo = CountLessThanOrEqual(item);
        return upTo > 0 && _comparer.Compare(this[upTo - 1], item) == 0 ? upTo - 1 : -1;
    }

    /// <summary>
    /// Counts the elements that order strictly before <paramref name="item"/>, in <c>O(log n)</c>, whether or
    /// not <paramref name="item"/> is present. This is the rank a percentile query needs.
    /// </summary>
    /// <param name="item">The element to bound by.</param>
    /// <returns>The number of elements smaller than <paramref name="item"/>.</returns>
    public int CountLessThan(T item)
    {
        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
            return _count;

        return PrefixSum(bucket) + LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item);
    }

    /// <summary>
    /// Counts the elements that order before <paramref name="item"/> or equal to it, in <c>O(log n)</c>.
    /// </summary>
    /// <param name="item">The element to bound by.</param>
    /// <returns>The number of elements not greater than <paramref name="item"/>.</returns>
    public int CountLessThanOrEqual(T item)
    {
        int bucket = LocateBucketAbove(item);
        if (bucket == _bucketCount)
            return _count;

        return PrefixSum(bucket) + UpperBoundInBucket(_buckets[bucket], _lengths[bucket], item);
    }

    /// <summary>
    /// Removes one element equal to <paramref name="item"/>, the earliest added of them, in <c>O(√n)</c>.
    /// </summary>
    /// <param name="item">The element to remove one copy of.</param>
    /// <returns><c>true</c> if an element was removed; <c>false</c> if none was equal.</returns>
    public bool Remove(T item)
    {
        int bucket = LocateBucket(item);
        if (bucket == _bucketCount)
            return false;

        int offset = LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item);
        if (_comparer.Compare(_buckets[bucket][offset], item) != 0)
            return false;

        Delete(bucket, offset, 1);
        return true;
    }

    /// <summary>
    /// Removes every element equal to <paramref name="item"/>. A run spanning several buckets is cut out in
    /// one pass: the two partial buckets at its ends are trimmed, the buckets between are dropped with one
    /// shift, and the positional index is rebuilt once.
    /// </summary>
    /// <param name="item">The element to remove every copy of.</param>
    /// <returns>The number of elements removed.</returns>
    public int RemoveAll(T item)
    {
        int first = LocateBucket(item);
        if (first == _bucketCount)
            return 0;

        int from = LowerBoundInBucket(_buckets[first], _lengths[first], item);

        // The run ends in the bucket holding the upper bound, or at the very end when nothing orders after
        // `item`.
        int last = LocateBucketAbove(item);
        int to;
        if (last == _bucketCount)
        {
            last--;
            to = _lengths[last];
        }
        else
        {
            to = UpperBoundInBucket(_buckets[last], _lengths[last], item);
        }

        if (first == last)
        {
            if (from == to)
                return 0;

            Delete(first, from, to - from);
            return to - from;
        }

        // The run spans buckets: it is the tail of `first`, every bucket strictly between, and the head of
        // `last`. A bucket before `last` whose maximum is not above `item` can only hold copies past the lower
        // bound, so the run is not empty here.
        int removed = _lengths[first] - from + to;
        for (int i = first + 1; i < last; i++)
            removed += _lengths[i];

        T[] tail = _buckets[last];
        int tailLength = _lengths[last] - to;
        Array.Copy(tail, to, tail, 0, tailLength);
        Array.Clear(tail, tailLength, to);
        _lengths[last] = tailLength;

        T[] head = _buckets[first];
        Array.Clear(head, from, _lengths[first] - from);
        _lengths[first] = from;
        if (from > 0)
            _maxes[first] = head[from - 1];

        // Every bucket the run emptied goes in one shift, and the Fenwick tree is rebuilt once, rather than
        // once per dropped bucket.
        int dropFrom = from == 0 ? first : first + 1;
        int dropTo = tailLength == 0 ? last + 1 : last;
        RemoveBucketRange(dropFrom, dropTo);
        _count -= removed;
        _version++;

        // The two buckets now meeting at the seam merge when their union fits in half the left one, the
        // same bound Delete merges at.
        int seam = dropFrom - 1;
        if (seam >= 0 && seam + 1 < _bucketCount && _lengths[seam] + _lengths[seam + 1] <= _buckets[seam].Length / 2)
            MergeBuckets(seam, seam + 1);
        else
            RebuildTree();

        return removed;
    }

    /// <summary>
    /// Removes the element at <paramref name="index"/> in ascending order, in <c>O(√n)</c>.
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
        Delete(bucket, offset, 1);
    }

    /// <summary>
    /// Removes every element and releases the buckets and the slot arrays behind them, leaving the multiset
    /// exactly as a new one.
    /// </summary>
    /// <remarks>
    /// On a multiset that is already empty this still releases the slot arrays a run of removals left
    /// behind. That changes nothing observable, so it does not invalidate an enumerator; only a clear that
    /// really removed elements does.
    /// </remarks>
    public void Clear()
    {
        if (_count == 0)
        {
            if (_buckets.Length != 0)
                ReleaseSlots();

            return;
        }

        ReleaseSlots();
        _count = 0;
        _version++;
    }

    private void ReleaseSlots()
    {
        _buckets = [];
        _lengths = [];
        _maxes = [];
        _tree = new int[1];
        _bucketCount = 0;
    }

    /// <summary>
    /// Rebuilds the multiset at the bucket capacity its <i>current</i> element count calls for, in
    /// <c>O(n)</c>, packing buckets three-quarters full. The order of equal elements is preserved.
    /// </summary>
    /// <remarks>
    /// Bucket arrays are never narrowed as elements leave, so after a bulk removal a mutation shifts a bucket
    /// sized for what the multiset used to hold. This puts that back. It invalidates active enumerators and
    /// is a no-op on an empty multiset that holds no buckets.
    /// </remarks>
    public void TrimExcess()
    {
        if (_count == 0)
        {
            if (_buckets.Length != 0)
                ReleaseSlots();

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

    /// <summary>Gets the smallest element; among ties, the earliest added.</summary>
    /// <param name="item">The first element in order, or <c>default</c> when the multiset is empty.</param>
    /// <returns><c>true</c> if the multiset is non-empty.</returns>
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

    /// <summary>Gets the largest element; among ties, the latest added.</summary>
    /// <param name="item">The last element in order, or <c>default</c> when the multiset is empty.</param>
    /// <returns><c>true</c> if the multiset is non-empty.</returns>
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
    /// Finds the first element in order that is <b>greater than or equal to</b> <paramref name="item"/>, in
    /// <c>O(log n)</c>. When equal elements are present it is the earliest added of them.
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
        bound = _buckets[bucket][LowerBoundInBucket(_buckets[bucket], _lengths[bucket], item)];
        return true;
    }

    /// <summary>
    /// Finds the first element in order that is <b>strictly greater than</b> <paramref name="item"/>, in
    /// <c>O(log n)</c>.
    /// </summary>
    /// <param name="item">The element to bound.</param>
    /// <param name="bound">The bounding element, or <c>default</c> when no element is larger.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetUpperBound(T item, out T bound)
    {
        int bucket = LocateBucketAbove(item);
        if (bucket == _bucketCount)
        {
            bound = default!;
            return false;
        }

        // The bucket's maximum is greater than `item`, so the in-bucket upper bound cannot run off its end.
        bound = _buckets[bucket][UpperBoundInBucket(_buckets[bucket], _lengths[bucket], item)];
        return true;
    }

    /// <summary>
    /// Enumerates, in ascending order, every element in the half-open range
    /// <c>[fromInclusive, toExclusive)</c>, duplicates included. The scan seeks to the lower bound in
    /// <c>O(log n)</c> and then walks contiguous arrays, so it costs <c>O(log n + k)</c> for <c>k</c> results.
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
    /// Copies every element, in ascending order, to <paramref name="array"/> starting at
    /// <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(T[] array, int arrayIndex)
    {
        CopyToGuard.Validate(array, arrayIndex, _count, CopyToGuard.ElementsMessage);

        for (int i = 0; i < _bucketCount; i++)
        {
            Array.Copy(_buckets[i], 0, array, arrayIndex, _lengths[i]);
            arrayIndex += _lengths[i];
        }
    }

    /// <summary>
    /// Returns a struct enumerator over the elements in ascending order, duplicates included. Iterating it
    /// via <c>foreach</c> allocates nothing.
    /// </summary>
    /// <returns>A struct enumerator over this multiset.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    bool ICollection<T>.IsReadOnly => false;

    // ---- internals ---------------------------------------------------------------------------------

    // The first bucket whose maximum is not smaller than `item`: the bucket holding the lower bound. Returns
    // _bucketCount when `item` orders after every element.
    private int LocateBucket(T item)
    {
        int lo = 0;
        int hi = _bucketCount;

        while (lo < hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (_comparer.Compare(_maxes[mid], item) < 0)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    // The first bucket whose maximum is greater than `item`: the bucket holding the upper bound. Returns
    // _bucketCount when no element orders after `item`.
    private int LocateBucketAbove(T item)
    {
        int lo = 0;
        int hi = _bucketCount;

        while (lo < hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (_comparer.Compare(_maxes[mid], item) <= 0)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    // The index of the first element in the bucket that is not smaller than `item`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LowerBoundInBucket(T[] bucket, int length, T item)
    {
        int lo = 0;
        int hi = length;

        while (lo < hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (_comparer.Compare(bucket[mid], item) < 0)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    // The index of the first element in the bucket that is greater than `item`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int UpperBoundInBucket(T[] bucket, int length, T item)
    {
        int lo = 0;
        int hi = length;

        while (lo < hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (_comparer.Compare(bucket[mid], item) <= 0)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
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

    // The smallest power of two whose square is at least `count`, never below MinBucketCapacity. The same
    // rule as RankedSet.TargetCapacity, which explains why it keeps the structural cost amortized constant.
    // The loop terminates without a clamp: `int` counts stop below 65536², so the capacity stops at 65536.
    private static int TargetCapacity(int count)
    {
        int capacity = MinBucketCapacity;
        while ((long)capacity * capacity < count)
            capacity <<= 1;

        return capacity;
    }

    private void GrowBucket(int bucket, int capacity)
    {
        var grown = new T[capacity];
        Array.Copy(_buckets[bucket], grown, _lengths[bucket]);
        _buckets[bucket] = grown;
    }

    // Splits a full bucket into two half-full adjacent ones and returns the length of the left half.
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

    // Removes `count` consecutive elements of one bucket starting at `offset`. The caller guarantees the run
    // lies inside the bucket.
    private void Delete(int bucket, int offset, int count)
    {
        T[] target = _buckets[bucket];
        int length = _lengths[bucket] - count;
        Array.Copy(target, offset + count, target, offset, length - offset);
        Array.Clear(target, length, count);
        _lengths[bucket] = length;
        AddToTree(bucket, -count);
        _count -= count;
        _version++;

        if (length == 0)
        {
            DropBucket(bucket);
            return;
        }

        if (offset == length)
            _maxes[bucket] = target[length - 1];

        // Merge a thinned-out bucket into a neighbour only when the union stays at or below half the
        // destination's capacity, exactly as RankedSet does.
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

    // Removes the bucket slots [from, to) with one shift of the three parallel arrays. The caller rebuilds
    // the Fenwick tree.
    private void RemoveBucketRange(int from, int to)
    {
        int dropped = to - from;
        int tail = _bucketCount - to;
        Array.Copy(_buckets, to, _buckets, from, tail);
        Array.Copy(_lengths, to, _lengths, from, tail);
        Array.Copy(_maxes, to, _maxes, from, tail);

        _bucketCount -= dropped;
        Array.Clear(_buckets, _bucketCount, dropped);
        Array.Clear(_lengths, _bucketCount, dropped);
        Array.Clear(_maxes, _bucketCount, dropped);
    }

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

    // Binary lifting down the Fenwick tree from the largest power of two not above the live bucket count.
    // The caller has checked 0 <= index < Count. See RankedSet.Locate for why no bounds test is needed.
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
    /// A struct enumerator over a <see cref="RankedMultiSet{T, TComparer}"/>'s elements in ascending order.
    /// It holds only a bucket and an offset, so iterating it via <c>foreach</c> allocates nothing.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly RankedMultiSet<T, TComparer> _set;
        private readonly int _version;
        private int _bucket;
        private int _offset;
        private T _current;

        internal Enumerator(RankedMultiSet<T, TComparer> set)
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
        /// <exception cref="InvalidOperationException">The multiset was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The multiset was modified during enumeration.");

            if (_bucket < _set._bucketCount)
            {
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
        /// <exception cref="InvalidOperationException">The multiset was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The multiset was modified during enumeration.");

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
        private readonly RankedMultiSet<T, TComparer> _set;
        private readonly T _from;
        private readonly T _toExclusive;

        internal RangeEnumerable(RankedMultiSet<T, TComparer> set, T from, T toExclusive)
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
    /// A struct enumerator over the elements of one range of a <see cref="RankedMultiSet{T, TComparer}"/>, in
    /// ascending order.
    /// </summary>
    public struct RangeEnumerator : IEnumerator<T>
    {
        private readonly RankedMultiSet<T, TComparer> _set;
        private readonly T _from;
        private readonly T _toExclusive;
        private readonly int _version;
        private int _bucket;
        private int _offset;
        private T _current;
        private bool _finished;

        internal RangeEnumerator(RankedMultiSet<T, TComparer> set, T from, T toExclusive)
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
        /// <exception cref="InvalidOperationException">The multiset was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The multiset was modified during enumeration.");

            if (_finished || _bucket == _set._bucketCount)
            {
                _current = default!;
                return false;
            }

            T item = _set._buckets[_bucket][_offset];

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
        /// <exception cref="InvalidOperationException">The multiset was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The multiset was modified during enumeration.");

            _current = default!;
            _finished = false;
            Seek();
        }

        // Positions the cursor at the range's lower bound, or marks the scan finished when every element
        // orders before it.
        private void Seek()
        {
            _bucket = _set.LocateBucket(_from);
            _offset = 0;
            if (_bucket == _set._bucketCount)
            {
                _finished = true;
                return;
            }

            _offset = _set.LowerBoundInBucket(_set._buckets[_bucket], _set._lengths[_bucket], _from);
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
