using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A FIFO queue that keeps the <b>fold of everything it holds</b> under an arbitrary associative operation, so the
/// aggregate of the current contents is an <c>O(1)</c> read and enqueue and dequeue are <c>O(1)</c> amortized —
/// the <b>sliding-window aggregation</b> structure, with no inverse required.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TMonoid">
/// The fold. Constrained to <c>struct, IMonoid&lt;T&gt;</c> so the JIT specializes the queue for it and inlines
/// the combine instead of emitting an interface call per element.
/// </typeparam>
/// <remarks>
/// <para>
/// The usual way to keep a rolling aggregate is a running total: add what enters, subtract what leaves. That needs
/// an inverse, so it works for a sum and little else in <see cref="IMonoid{T}"/>'s reach — there is no "un-max" or
/// "un-gcd", and undoing an OR takes a count per bit. Without one, a <see cref="Queue{T}"/> has to re-fold the whole window on
/// every read, <c>O(w)</c> per read for a window of <c>w</c>. A <see cref="SegmentTree{T, TMonoid}"/> over a
/// ring of <c>w</c> slots gets that to <c>O(log w)</c> per step but only for a window whose length is fixed in
/// advance. This type does it in <c>O(1)</c> amortized for any window: the caller decides when to dequeue, so a
/// window bounded by count, by time ("the last five seconds"), or by anything else works the same way.
/// </para>
/// <para>
/// It is the classic <b>two-stacks queue</b>, laid out in one ring buffer. The older part of the contents (the
/// <i>front</i>) stores, beside each element, the fold of that element and everything after it up to the front's
/// end; the newer part (the <i>back</i>) keeps only a running fold. The aggregate is one combine of the two. When
/// a dequeue finds the front empty, the whole back becomes the front in one pass that computes the suffix folds,
/// so each element is combined a constant number of times over its life — <c>O(1)</c> amortized, though the
/// dequeue that triggers the pass costs <c>O(n)</c>.
/// </para>
/// <para>
/// Commutativity is not required. <see cref="Aggregate"/> folds oldest to newest, so a non-commutative monoid
/// (affine-map composition, "last write wins", string concatenation) gets the same answer a left-to-right scan of
/// the queue would.
/// </para>
/// <para>
/// <b>Where it loses.</b> For a sliding window <b>minimum or maximum</b> specifically, a hand-written monotonic
/// deque is the tighter answer: it drops every element that can never be the extremum again and reads the
/// extremum off its front, so it does fewer comparisons. This type is for the folds
/// that trick does not cover — bitwise <c>or</c> / <c>and</c>, gcd, a min-and-argmin pair, a composed function —
/// and for code that wants one structure for all of them. For a sum over an integer type, a running total is
/// simpler still.
/// </para>
/// <para>
/// The ring has a power-of-two capacity and grows by doubling; it holds at most <c>2^30</c> elements. It stores
/// two slots per element (the value and its suffix fold). Every enqueue and dequeue bumps the version, so an
/// active enumerator throws; a <see cref="Clear"/> of an already-empty queue changes nothing and does not. This
/// type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public sealed class AggregatingQueue<T, TMonoid> : IReadOnlyCollection<T>
    where TMonoid : struct, IMonoid<T>
{
    private const int DefaultCapacity = 4;

    /// <summary>The largest number of elements the queue can hold: the largest power of two an array can hold.</summary>
    private const int MaxCapacity = 1 << 30;

    // Ring buffer of the queued values, oldest at _head. Occupied logical positions are [0, _count), physical
    // (_head + i) & _mask. Capacity is always zero or a power of two, so the wrap is a mask.
    private T[] _values;

    // Parallel to _values. For a logical position i in the front, [0, _frontCount), the slot holds the fold of
    // values i .. _frontCount - 1 in order. Slots in the back hold nothing meaningful.
    private T[] _folds;

    private int _mask;
    private int _head;

    // Bumped on every enqueue and dequeue (and a non-empty Clear) so active enumerators throw. Declared between
    // the fields an enqueue writes and the ones a dequeue reads, so no two that one operation stores and the next
    // reloads sit side by side to be paired into one Arm64 ldp / stp.
    private int _version;

    private int _count;
    private int _frontCount;

    // The fold of the back: logical positions [_frontCount, _count), in order. The identity when the back is empty.
    private T _backFold;

    // Not readonly: a readonly field of a struct type is defensively copied on every member call. Matches
    // SegmentTree's monoid field.
    private TMonoid _monoid;

    /// <summary>Initializes a new, empty queue with no pre-allocated capacity.</summary>
    public AggregatingQueue()
        : this(0, default)
    {
    }

    /// <summary>
    /// Initializes a new, empty queue that can hold at least <paramref name="capacity"/> elements before it grows.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity, rounded up to a power of two. Must be in <c>[0, 2^30]</c>; <c>0</c> allocates nothing
    /// yet.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative or exceeds <c>2^30</c>.</exception>
    public AggregatingQueue(int capacity)
        : this(capacity, default)
    {
    }

    /// <summary>
    /// Initializes a new, empty queue that folds with the supplied <paramref name="monoid"/> and can hold at least
    /// <paramref name="capacity"/> elements before it grows.
    /// </summary>
    /// <param name="capacity">The initial capacity, rounded up to a power of two. Must be in <c>[0, 2^30]</c>.</param>
    /// <param name="monoid">The fold to use. Pass this overload only when the monoid carries state.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative or exceeds <c>2^30</c>.</exception>
    public AggregatingQueue(int capacity, TMonoid monoid)
    {
        if ((uint)capacity > MaxCapacity)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity,
                $"Capacity must be in the range [0, {MaxCapacity}].");

        _monoid = monoid;
        _backFold = monoid.Identity;
        Allocate(capacity == 0 ? 0 : (int)BitOperations.RoundUpToPowerOf2((uint)capacity));
    }

    /// <summary>
    /// Initializes a new queue holding the elements of <paramref name="collection"/>, enqueued in enumeration
    /// order, so the first element yielded is the oldest.
    /// </summary>
    /// <param name="collection">The elements to enqueue.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="collection"/> is a collection that reports more than <c>2^30</c> elements.
    /// </exception>
    public AggregatingQueue(IEnumerable<T> collection)
        : this(collection, default)
    {
    }

    /// <summary>
    /// Initializes a new queue that folds with the supplied <paramref name="monoid"/> and holds the elements of
    /// <paramref name="collection"/>, enqueued in enumeration order.
    /// </summary>
    /// <param name="collection">The elements to enqueue.</param>
    /// <param name="monoid">The fold to use. Pass this overload only when the monoid carries state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="collection"/> is a collection that reports more than <c>2^30</c> elements.
    /// </exception>
    public AggregatingQueue(IEnumerable<T> collection, TMonoid monoid)
    {
        ArgumentNullException.ThrowIfNull(collection);

        _monoid = monoid;
        _backFold = monoid.Identity;

        // A counted source sizes the ring up front, and an oversized count is rejected before anything is
        // allocated or enumerated. The count only sizes the ring: the elements are enumerated, not copied, so a
        // source that changes after it is counted leaves exactly what it yielded.
        int capacity = 0;
        if (collection is ICollection<T> counted)
        {
            capacity = counted.Count;
            if ((uint)capacity > MaxCapacity)
                throw new ArgumentException(
                    $"The source holds more than the maximum supported count of {MaxCapacity} elements.",
                    nameof(collection));
        }

        Allocate(capacity == 0 ? 0 : (int)BitOperations.RoundUpToPowerOf2((uint)capacity));

        foreach (T item in collection)
            Enqueue(item);

        _version = 0;
    }

    /// <summary>Gets the number of elements in the queue.</summary>
    public int Count => _count;

    /// <summary>Gets the number of elements the queue can hold before it grows. Always zero or a power of two.</summary>
    public int Capacity => _values.Length;

    /// <summary>
    /// Gets the fold of every element in the queue, oldest to newest — <c>Combine(…Combine(Combine(e0, e1), e2)…)</c>
    /// for the elements in queue order — or the monoid's identity when the queue is empty. <c>O(1)</c>: one combine.
    /// </summary>
    public T Aggregate
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _frontCount != 0 ? _monoid.Combine(_folds[_head], _backFold) : _backFold;
    }

    /// <summary>
    /// Adds <paramref name="item"/> as the newest element. <c>O(1)</c> amortized: one combine, plus a copy of the
    /// ring when it is full.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <exception cref="InvalidOperationException">The queue already holds <c>2^30</c> elements.</exception>
    public void Enqueue(T item)
    {
        // Fold first: a monoid can throw (SumMonoid<decimal> overflows), and nothing may be committed when it does,
        // or the queue would hold an element its Aggregate does not include.
        T backFold = _monoid.Combine(_backFold, item);

        _version++;

        if (_count == _values.Length)
            Grow();

        _values[(_head + _count) & _mask] = item;
        _count++;
        _backFold = backFold;
    }

    /// <summary>
    /// Removes and returns the oldest element. <c>O(1)</c> amortized; the call that finds the front empty pays
    /// <c>O(n)</c> combines to rebuild it from the back.
    /// </summary>
    /// <returns>The oldest element.</returns>
    /// <exception cref="InvalidOperationException">The queue is empty.</exception>
    public T Dequeue()
    {
        if (_count == 0)
            ThrowEmpty();

        return DequeueCore();
    }

    /// <summary>Removes the oldest element if there is one.</summary>
    /// <param name="item">The oldest element, or <c>default</c> when the queue is empty.</param>
    /// <returns><c>true</c> if an element was removed; <c>false</c> when the queue is empty.</returns>
    public bool TryDequeue([MaybeNullWhen(false)] out T item)
    {
        if (_count == 0)
        {
            item = default;
            return false;
        }

        item = DequeueCore();
        return true;
    }

    /// <summary>Returns the oldest element without removing it.</summary>
    /// <returns>The oldest element.</returns>
    /// <exception cref="InvalidOperationException">The queue is empty.</exception>
    public T Peek()
    {
        if (_count == 0)
            ThrowEmpty();

        return _values[_head];
    }

    /// <summary>Returns the oldest element without removing it, if there is one.</summary>
    /// <param name="item">The oldest element, or <c>default</c> when the queue is empty.</param>
    /// <returns><c>true</c> if the queue is non-empty; otherwise <c>false</c>.</returns>
    public bool TryPeek([MaybeNullWhen(false)] out T item)
    {
        if (_count == 0)
        {
            item = default;
            return false;
        }

        item = _values[_head];
        return true;
    }

    /// <summary>
    /// Removes every element and resets <see cref="Aggregate"/> to the identity, keeping the capacity. Clearing an
    /// already-empty queue changes nothing and does not invalidate enumerators.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        // Only the occupied run can hold references. A dequeue clears both slots it vacates and growth copies the
        // rest as they are, so every slot outside the run is already default, and clearing costs O(Count) rather
        // than O(Capacity) after a large burst has drained.
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            ClearOccupied(_values);
            ClearOccupied(_folds);
        }

        _head = 0;
        _count = 0;
        _frontCount = 0;
        _backFold = _monoid.Identity;
        _version++;
    }

    /// <summary>Returns an enumerator over the elements from oldest to newest.</summary>
    /// <returns>A struct enumerator over the elements.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    private T DequeueCore()
    {
        _version++;

        if (_frontCount == 0)
            Flip();

        int head = _head;
        T item = _values[head];
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            _values[head] = default!;
            _folds[head] = default!;
        }

        _head = (head + 1) & _mask;
        _count--;
        _frontCount--;
        return item;
    }

    // Turns the whole back into the front: one pass from newest to oldest that stores each element's suffix fold.
    // Only called with the front empty and the queue non-empty, so the back is everything.
    private void Flip()
    {
        T[] values = _values;
        T[] folds = _folds;
        int mask = _mask;
        int last = (_head + _count - 1) & mask;

        T fold = values[last];
        folds[last] = fold;
        for (int i = _count - 2; i >= 0; i--)
        {
            int slot = (_head + i) & mask;
            fold = _monoid.Combine(values[slot], fold);
            folds[slot] = fold;
        }

        _frontCount = _count;
        _backFold = _monoid.Identity;
    }

    // Clears the occupied run of a ring, which wraps at most once.
    private void ClearOccupied(T[] ring)
    {
        int firstRun = Math.Min(_count, ring.Length - _head);
        Array.Clear(ring, _head, firstRun);
        Array.Clear(ring, 0, _count - firstRun);
    }

    private void Grow()
    {
        int newCapacity = _values.Length == 0 ? DefaultCapacity : _values.Length * 2;
        ThrowIfPastMaxCapacity(newCapacity);

        T[] values = new T[newCapacity];
        T[] folds = new T[newCapacity];
        CopyLinear(_values, values);
        CopyLinear(_folds, folds);

        _values = values;
        _folds = folds;
        _mask = newCapacity - 1;
        _head = 0;
    }

    // Copies the occupied run of a full ring into the start of a larger array, oldest first, so each logical
    // position keeps its value and (in the front) its suffix fold.
    private void CopyLinear(T[] source, T[] destination)
    {
        int firstRun = source.Length - _head;
        Array.Copy(source, _head, destination, 0, firstRun);
        Array.Copy(source, 0, destination, firstRun, _head);
    }

    [MemberNotNull(nameof(_values), nameof(_folds))]
    private void Allocate(int capacity)
    {
        _values = capacity == 0 ? Array.Empty<T>() : new T[capacity];
        _folds = capacity == 0 ? Array.Empty<T>() : new T[capacity];
        _mask = capacity - 1;
    }

    // Reaching it needs a full ring of 2^30 elements, two arrays of them, so it is only observable from a test that
    // allocates many gigabytes. Excluded from coverage like Deque's equivalent ceiling.
    [ExcludeFromCodeCoverage(Justification = "Needs a full 2^30-element ring (two arrays of 2^30 elements).")]
    private static void ThrowIfPastMaxCapacity(int newCapacity)
    {
        if ((uint)newCapacity > MaxCapacity)
            throw new InvalidOperationException(
                $"The queue cannot hold more than {MaxCapacity} elements.");
    }

    [DoesNotReturn]
    private static void ThrowEmpty() =>
        throw new InvalidOperationException("The queue is empty.");

    [DoesNotReturn]
    private static void ThrowVersionChanged() =>
        throw new InvalidOperationException("The queue was modified during enumeration.");

    /// <summary>A struct enumerator over an <see cref="AggregatingQueue{T, TMonoid}"/>'s elements, oldest first.</summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly AggregatingQueue<T, TMonoid> _queue;
        private readonly int _version;
        private int _index;
        private T _current;

        internal Enumerator(AggregatingQueue<T, TMonoid> queue)
        {
            _queue = queue;
            _version = queue._version;
            _index = 0;
            _current = default!;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element.</summary>
        /// <returns><c>true</c> if there is a next element; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The queue was modified during enumeration.</exception>
        public bool MoveNext()
        {
            AggregatingQueue<T, TMonoid> queue = _queue;
            if (_version != queue._version)
                ThrowVersionChanged();

            if (_index < queue._count)
            {
                _current = queue._values[(queue._head + _index) & queue._mask];
                _index++;
                return true;
            }

            _current = default!;
            return false;
        }

        /// <summary>Resets the enumerator to before the oldest element.</summary>
        /// <exception cref="InvalidOperationException">The queue was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _queue._version)
                ThrowVersionChanged();

            _index = 0;
            _current = default!;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
