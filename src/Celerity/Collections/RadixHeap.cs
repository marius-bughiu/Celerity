using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <b>monotone priority queue</b> over <see cref="ulong"/> priorities: a radix heap whose
/// <see cref="Enqueue"/> is an <c>O(1)</c> append and whose <see cref="Dequeue"/> is amortized
/// <c>O(log C)</c> in the priority range, with no comparison on the push path. It requires that no priority
/// enqueued is smaller than the last one dequeued (<see cref="Floor"/>) — the shape of Dijkstra and A* with
/// non-negative integer weights, of discrete-event simulation over integer timestamps, and of a scheduler
/// draining deadlines in order.
/// </summary>
/// <typeparam name="TElement">
/// The type of the queued elements. Elements are payload, not keys: the same element may be queued any
/// number of times, and the heap never compares or hashes one.
/// </typeparam>
/// <remarks>
/// <para>
/// The layout is the radix heap of Ahuja, Mehlhorn, Orlin and Tarjan (1990), in its 64-bit form: 65 buckets,
/// where an entry with priority <c>k</c> sits in bucket <c>bitlength(k XOR Floor)</c> — bucket 0 holds the
/// entries equal to <see cref="Floor"/>, and bucket <c>i</c> those that first differ from it at bit
/// <c>i - 1</c>. Every entry in a lower bucket is smaller than every entry in a higher one. Enqueue appends to
/// one bucket. Dequeue pops bucket 0; when bucket 0 is empty it finds the lowest non-empty bucket, makes that
/// bucket's minimum the new <see cref="Floor"/>, and redistributes the bucket's entries into strictly lower
/// buckets. Each entry can only move down, so it is moved at most 64 times over its life, and each move is a
/// sequential pass over one array rather than a sift through a tree.
/// </para>
/// <para>
/// The documented BCL-beating workload is a <b>large monotone queue</b>: a single-source shortest path over
/// integer weights, an event simulation holding many pending events, a priority-ordered drain of a batch. The
/// BCL <see cref="PriorityQueue{TElement, TPriority}"/> is a comparison-based 4-ary heap that pays a sift of
/// <c>O(log n)</c> levels — a cache miss each at scale — on every enqueue and dequeue, and cannot use the
/// monotonicity. Priorities of another integer type map in directly: cast a non-negative <see cref="int"/> or
/// <see cref="long"/>, or shift a signed range by its minimum.
/// </para>
/// <para>
/// Where it does <b>not</b> win: a <b>small</b> queue, where every bucket array has to be allocated and grown
/// while a heap of a few hundred entries fits in cache; a queue whose priorities are not monotone, which this
/// type rejects; and a queue that needs a non-integer priority, decrease-key or the maximum — use
/// <see cref="PriorityQueue{TElement, TPriority}"/>, <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}"/>
/// or <see cref="MinMaxHeap{TElement, TPriority, TComparer}"/> there. Ties are broken arbitrarily, so two
/// entries with equal priority come out in no guaranteed order; the enumerator walks the buckets from lowest
/// to highest, which is neither priority nor insertion order within a bucket. A drained bucket keeps its
/// array, so the heap can hold a few times its current entry count in capacity; <see cref="TrimExcess"/>
/// releases it. This type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public class RadixHeap<TElement> : IReadOnlyCollection<(TElement Element, ulong Priority)>
{
    private const int BucketCount = 65;
    private const int DefaultBucketCapacity = 4;

    private readonly Bucket[] _buckets = new Bucket[BucketCount];

    // Bit i - 1 is set exactly when bucket i (1..64) is non-empty, so Dequeue finds the bucket to redistribute
    // with one TrailingZeroCount. Bucket 0 is tracked by its own count.
    private ulong _occupied;
    private ulong _floor;
    private int _count;

    // Bumped on every observable mutation (and on any reallocation of a bucket array) so live enumerators fail
    // fast. A Clear of an empty heap and a TrimExcess that frees nothing do not bump it.
    private int _version;

    /// <summary>Initializes a new, empty heap whose <see cref="Floor"/> is 0.</summary>
    public RadixHeap()
    {
        for (int i = 0; i < BucketCount; i++)
            _buckets[i].Items = Array.Empty<(TElement, ulong)>();
    }

    /// <summary>
    /// Initializes a new heap seeded with <paramref name="items"/>, in any order, with a <see cref="Floor"/> of 0.
    /// Each entry is an <c>O(1)</c> append, so seeding is <c>O(n)</c>. Duplicate elements and duplicate
    /// priorities are both kept.
    /// </summary>
    /// <param name="items">The element/priority pairs to seed the heap with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    public RadixHeap(IEnumerable<(TElement Element, ulong Priority)> items)
        : this()
    {
        ArgumentNullException.ThrowIfNull(items);

        foreach ((TElement element, ulong priority) in items)
            Push(BucketOf(priority, 0), element, priority);
    }

    /// <summary>Gets the number of entries in the heap.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets the smallest priority <see cref="Enqueue"/> accepts: the priority of the last entry dequeued, or 0
    /// for a heap that has never been dequeued from (or was cleared since). Every queued priority is at least
    /// this value. <see cref="Peek"/> and <see cref="TryPeek"/> never change it.
    /// </summary>
    public ulong Floor => _floor;

    /// <summary>
    /// Adds <paramref name="element"/> with the given <paramref name="priority"/> in <c>O(1)</c> (amortized over
    /// bucket growth).
    /// </summary>
    /// <param name="element">The element to add. It need not be distinct from elements already queued.</param>
    /// <param name="priority">The priority to associate with <paramref name="element"/>. Must be at least <see cref="Floor"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="priority"/> is less than <see cref="Floor"/>.</exception>
    public void Enqueue(TElement element, ulong priority)
    {
        if (priority < _floor)
            ThrowBelowFloor(priority);

        Push(BucketOf(priority, _floor), element, priority);
        _version++;
    }

    /// <summary>
    /// Returns the element with the minimum priority without removing it. This is <c>O(1)</c> when an entry at
    /// <see cref="Floor"/> is queued, and otherwise a scan of the lowest non-empty bucket. It moves nothing, so
    /// it never raises <see cref="Floor"/>.
    /// </summary>
    /// <returns>The minimum-priority element.</returns>
    /// <exception cref="InvalidOperationException">The heap is empty.</exception>
    public TElement Peek()
    {
        if (!TryPeek(out TElement? element, out _))
            ThrowEmpty();

        return element;
    }

    /// <summary>Attempts to read the element with the minimum priority without removing it.</summary>
    /// <param name="element">When this method returns <c>true</c>, the minimum-priority element; otherwise <c>default</c>.</param>
    /// <param name="priority">When this method returns <c>true</c>, that element's priority; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if the heap was non-empty; otherwise <c>false</c>.</returns>
    public bool TryPeek([MaybeNullWhen(false)] out TElement element, out ulong priority)
    {
        ref Bucket zero = ref _buckets[0];
        if (zero.Count > 0)
        {
            (element, priority) = zero.Items[zero.Count - 1];
            return true;
        }

        if (_occupied == 0)
        {
            element = default;
            priority = default;
            return false;
        }

        ref Bucket lowest = ref _buckets[BitOperations.TrailingZeroCount(_occupied) + 1];
        (element, priority) = lowest.Items[MinSlot(lowest.Items, lowest.Count)];
        return true;
    }

    /// <summary>
    /// Removes and returns the element with the minimum priority, in amortized <c>O(log C)</c>, and raises
    /// <see cref="Floor"/> to its priority.
    /// </summary>
    /// <returns>The minimum-priority element.</returns>
    /// <exception cref="InvalidOperationException">The heap is empty.</exception>
    public TElement Dequeue()
    {
        if (!TryDequeue(out TElement? element, out _))
            ThrowEmpty();

        return element;
    }

    /// <summary>
    /// Attempts to remove and return the element with the minimum priority, raising <see cref="Floor"/> to its
    /// priority.
    /// </summary>
    /// <param name="element">When this method returns <c>true</c>, the removed minimum-priority element; otherwise <c>default</c>.</param>
    /// <param name="priority">When this method returns <c>true</c>, that element's priority; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if an entry was removed; <c>false</c> if the heap was empty.</returns>
    public bool TryDequeue([MaybeNullWhen(false)] out TElement element, out ulong priority)
    {
        ref Bucket zero = ref _buckets[0];
        if (zero.Count == 0)
        {
            if (_occupied == 0)
            {
                element = default;
                priority = default;
                return false;
            }

            Redistribute(BitOperations.TrailingZeroCount(_occupied) + 1);
        }

        int last = --zero.Count;
        (element, priority) = zero.Items[last];
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TElement>())
            zero.Items[last] = default;

        _count--;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes every entry and resets <see cref="Floor"/> to 0, returning the heap to its freshly-constructed
    /// state. The bucket arrays are retained.
    /// </summary>
    public void Clear()
    {
        _floor = 0;
        if (_count == 0)
            return;

        for (int i = 0; i < BucketCount; i++)
        {
            ref Bucket bucket = ref _buckets[i];
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TElement>())
                Array.Clear(bucket.Items, 0, bucket.Count);

            bucket.Count = 0;
        }

        _occupied = 0;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Shrinks every bucket array to its current entry count, releasing the capacity that drained buckets keep.
    /// An empty bucket gives up its array entirely.
    /// </summary>
    public void TrimExcess()
    {
        bool trimmed = false;
        for (int i = 0; i < BucketCount; i++)
        {
            ref Bucket bucket = ref _buckets[i];
            if (bucket.Items.Length == bucket.Count)
                continue;

            if (bucket.Count == 0)
                bucket.Items = Array.Empty<(TElement, ulong)>();
            else
                Array.Resize(ref bucket.Items, bucket.Count);

            trimmed = true;
        }

        if (trimmed)
            _version++;
    }

    /// <summary>
    /// Returns an allocation-free struct enumerator over the heap's entries in <b>bucket order</b>: every entry of
    /// a lower bucket before any entry of a higher one, but in no particular order within a bucket, so this is
    /// not priority order. To visit entries by priority, dequeue them.
    /// </summary>
    /// <returns>A struct enumerator over the element/priority pairs.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<(TElement Element, ulong Priority)> IEnumerable<(TElement Element, ulong Priority)>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    // The bucket for a priority relative to a floor it is no smaller than: 0 when they are equal, otherwise one
    // more than the index of the highest bit at which they differ.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BucketOf(ulong priority, ulong floor)
        => BucketCount - 1 - BitOperations.LeadingZeroCount(priority ^ floor);

    // Appends to a bucket and accounts for the entry. Does not bump _version; the public caller does.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Push(int index, TElement element, ulong priority)
    {
        ref Bucket bucket = ref _buckets[index];
        int count = bucket.Count;
        (TElement, ulong)[] items = bucket.Items;
        if ((uint)count >= (uint)items.Length)
            items = Grow(ref bucket);

        items[count] = (element, priority);
        bucket.Count = count + 1;
        if (index != 0)
            _occupied |= 1UL << (index - 1);

        _count++;
    }

    // Empties bucket `index` (the lowest non-empty one, bucket 0 being empty) into lower buckets. Its minimum
    // becomes the floor. Every entry in it agrees with the old floor above bit index - 1 and differs from it at
    // that bit, and so does the new floor, so each entry lands in a bucket below `index`; the minimum itself
    // and anything tied with it land in bucket 0. The entries are re-pushed straight from the bucket's array,
    // which nothing else writes to during the pass.
    private void Redistribute(int index)
    {
        ref Bucket bucket = ref _buckets[index];
        (TElement Element, ulong Priority)[] items = bucket.Items;
        int count = bucket.Count;
        ulong floor = items[MinSlot(items, count)].Priority;
        _floor = floor;

        bucket.Count = 0;
        _occupied &= ~(1UL << (index - 1));
        _count -= count;
        for (int i = 0; i < count; i++)
        {
            (TElement element, ulong priority) = items[i];
            Push(BucketOf(priority, floor), element, priority);
        }

        if (RuntimeHelpers.IsReferenceOrContainsReferences<TElement>())
            Array.Clear(items, 0, count);
    }

    private static int MinSlot((TElement Element, ulong Priority)[] items, int count)
    {
        int best = 0;
        ulong min = items[0].Priority;
        for (int i = 1; i < count; i++)
        {
            ulong priority = items[i].Priority;
            if (priority < min)
            {
                min = priority;
                best = i;
            }
        }

        return best;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (TElement, ulong)[] Grow(ref Bucket bucket)
    {
        int current = bucket.Items.Length;
        int newCapacity = current == 0 ? DefaultBucketCapacity : current * 2;
        Array.Resize(ref bucket.Items, ClampGrowth(newCapacity, current));
        return bucket.Items;
    }

    // The growth ceiling, split out because neither arm is reachable from a test: a bucket is full only when it
    // holds that many live entries, so the doubling passes Array.MaxLength only with more than 2^30 entries in a
    // single bucket — 16 GiB of (int, ulong) entries before the resize allocates the next array. Both are kept
    // because they are the only thing between a saturated bucket and a silently negative capacity.
    [ExcludeFromCodeCoverage(Justification = "Unreachable in a test run: needs more than 2^30 live entries " +
        "in one bucket before the doubling passes Array.MaxLength.")]
    private static int ClampGrowth(int newCapacity, int current)
    {
        if ((uint)newCapacity > (uint)Array.MaxLength)
            newCapacity = Array.MaxLength;
        if (newCapacity <= current)
            throw new InvalidOperationException("The heap has reached its maximum capacity.");

        return newCapacity;
    }

    [DoesNotReturn]
    private void ThrowBelowFloor(ulong priority)
        => throw new ArgumentOutOfRangeException(nameof(priority), priority,
            $"Priority must be at least Floor ({_floor}), the priority of the last entry dequeued.");

    [DoesNotReturn]
    private static void ThrowEmpty() => throw new InvalidOperationException("The heap is empty.");

    private struct Bucket
    {
        public (TElement Element, ulong Priority)[] Items;
        public int Count;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="RadixHeap{TElement}"/>'s entries in bucket order (not priority
    /// order). Because it is a struct, iterating it via <c>foreach</c> avoids the allocation a
    /// compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<(TElement Element, ulong Priority)>
    {
        private readonly RadixHeap<TElement> _heap;
        private readonly int _version;
        private int _bucket;
        private int _index;
        private (TElement Element, ulong Priority) _current;

        internal Enumerator(RadixHeap<TElement> heap)
        {
            _heap = heap;
            _version = heap._version;
            _bucket = 0;
            _index = 0;
            _current = default;
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public readonly (TElement Element, ulong Priority) Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next entry.</summary>
        /// <returns><c>true</c> if there is a next entry; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The heap was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _heap._version)
                throw new InvalidOperationException("The heap was modified during enumeration.");

            Bucket[] buckets = _heap._buckets;
            while (_bucket < BucketCount)
            {
                ref Bucket bucket = ref buckets[_bucket];
                if (_index < bucket.Count)
                {
                    _current = bucket.Items[_index];
                    _index++;
                    return true;
                }

                _bucket++;
                _index = 0;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first entry.</summary>
        /// <exception cref="InvalidOperationException">The heap was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _heap._version)
                throw new InvalidOperationException("The heap was modified during enumeration.");

            _bucket = 0;
            _index = 0;
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
