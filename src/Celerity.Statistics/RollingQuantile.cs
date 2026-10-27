namespace Celerity.Statistics;

/// <summary>
/// The exact quantile — a rolling median, p95 or any other fraction — of the most recent
/// <see cref="WindowSize"/> values of a stream, updated in <c>O(log WindowSize)</c> per value and read in
/// <c>O(1)</c>.
/// </summary>
/// <typeparam name="T">The type of the values. Ordered by <see cref="Comparer{T}.Default"/>.</typeparam>
/// <remarks>
/// <para>
/// Each <see cref="Add(T)"/> appends a value to a fixed-size sliding window; once the window is full, the
/// oldest value leaves it as the new one arrives. <see cref="Value"/> is then the element of zero-based rank
/// <c>⌊Quantile · (Count − 1)⌋</c> in the sorted window — what NumPy calls the <c>"lower"</c> quantile
/// method. A <see cref="Quantile"/> of <c>0.5</c> over an even window gives the lower of the two middle
/// values, <c>0</c> gives the window minimum and <c>1</c> its maximum. The answer is exact: it is always
/// one of the values in the window, never an interpolation or an estimate.
/// </para>
/// <para>
/// The BCL has nothing for this. The honest hand-roll keeps the window sorted in a <see cref="List{T}"/>
/// beside a <see cref="Queue{T}"/> of arrival order, and pays a binary search plus an <c>O(WindowSize)</c>
/// memory move for each removal and each insertion. That is fast for a small window and linear in it for a
/// large one. Re-sorting the window on every read is worse again. This type holds the window in two heaps
/// that meet at the quantile — a max-heap of the values at or below it and a min-heap of those above — and
/// remembers where each value sits, so the expiring value is removed in place rather than searched for.
/// Each <see cref="Add(T)"/> costs at most two heap sifts.
/// </para>
/// <para>
/// When the answer can be approximate, or the window is the whole stream, <see cref="DDSketch"/> answers
/// every quantile at once in a fixed footprint. This type answers one quantile exactly over a bounded
/// window, and stores the window to do it. The two heaps share one array of <see cref="WindowSize"/>
/// entries, each a value and the <see cref="int"/> slot it arrived in, beside one more <see cref="int"/> per
/// slot recording where that slot's value currently sits: <c>WindowSize</c> values plus twice as many integers.
/// </para>
/// <para>
/// The type is not thread-safe. There is no enumerator: the window is a stream position, and a caller who
/// needs its contents already has them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The median of the last 1,001 latency samples, updated as each one arrives.
/// var median = new RollingQuantile&lt;double&gt;(windowSize: 1_001, quantile: 0.5);
/// foreach (double latency in samples)
/// {
///     median.Add(latency);
///     if (median.IsFull &amp;&amp; latency &gt; 3 * median.Value)
///     {
///         Console.WriteLine($"spike: {latency}");
///     }
/// }
/// </code>
/// </example>
public sealed class RollingQuantile<T> where T : IComparable<T>
{
    // One array, two heaps. The max-heap of the low side ("lower") is stored from the front, its root at
    // index 0; the min-heap of the high side ("upper") is stored from the back, its root at the last index.
    // Their sizes always sum to the window's count, so they can never collide, whatever the quantile.
    private readonly Entry[] _entries;

    // Indexed by window slot: where that slot's value currently sits. A non-negative value is a position in
    // the lower heap; a negative one is the bitwise complement of a position in the upper heap.
    private readonly int[] _where;

    private readonly double _quantile;
    private int _lowerCount;
    private int _upperCount;

    // The slot the next value is written to. While the window fills, that is simply Count, so this stays
    // at 0; once the window is full it is the oldest slot, which the next value evicts and reuses.
    private int _head;

    /// <summary>
    /// Initializes an empty rolling quantile over a window of <paramref name="windowSize"/> values.
    /// </summary>
    /// <param name="windowSize">The number of most recent values the quantile is taken over. Must be at least <c>1</c>.</param>
    /// <param name="quantile">
    /// The fraction of the window at or below the reported value, from <c>0</c> (the minimum) to <c>1</c>
    /// (the maximum); <c>0.5</c> is the median.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="windowSize"/> is less than <c>1</c>, or <paramref name="quantile"/> is outside
    /// <c>[0, 1]</c> or is <see cref="double.NaN"/>.
    /// </exception>
    public RollingQuantile(int windowSize, double quantile)
    {
        if (windowSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowSize),
                windowSize,
                "Window size must be at least 1.");
        }

        // Written as a negated range test so that NaN, which fails every comparison, is rejected too.
        if (!(quantile >= 0d && quantile <= 1d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantile),
                quantile,
                "Quantile must be between 0 and 1 inclusive.");
        }

        _entries = new Entry[windowSize];
        _where = new int[windowSize];
        _quantile = quantile;
    }

    /// <summary>Gets the number of most recent values the quantile is taken over.</summary>
    public int WindowSize => _entries.Length;

    /// <summary>Gets the fraction of the window at or below <see cref="Value"/>, from <c>0</c> to <c>1</c>.</summary>
    public double Quantile => _quantile;

    /// <summary>
    /// Gets the number of values currently in the window — <see cref="WindowSize"/> once that many have been
    /// added, and the number added before that.
    /// </summary>
    public int Count => _lowerCount + _upperCount;

    /// <summary>Gets a value indicating whether the window is full, so each new value evicts the oldest.</summary>
    public bool IsFull => _lowerCount + _upperCount == _entries.Length;

    /// <summary>
    /// Gets the window's element of zero-based rank <c>⌊Quantile · (Count − 1)⌋</c> in sorted order.
    /// </summary>
    /// <exception cref="InvalidOperationException">The window is empty.</exception>
    /// <remarks>
    /// While the window is still filling, the rank is taken over the values seen so far. Check
    /// <see cref="IsFull"/> first when a partial window should not be read as a full one.
    /// </remarks>
    public T Value
    {
        get
        {
            if (_lowerCount == 0)
            {
                throw new InvalidOperationException("The window is empty.");
            }

            return _entries[0].Value;
        }
    }

    /// <summary>Gets the current quantile, unless the window is empty.</summary>
    /// <param name="value">
    /// When this method returns <c>true</c>, the same value <see cref="Value"/> returns; otherwise the default
    /// value of <typeparamref name="T"/>.
    /// </param>
    /// <returns><c>true</c> if the window holds at least one value; otherwise <c>false</c>.</returns>
    public bool TryGetValue(out T value)
    {
        if (_lowerCount == 0)
        {
            value = default!;
            return false;
        }

        value = _entries[0].Value;
        return true;
    }

    /// <summary>
    /// Appends a value to the window, evicting the oldest value first when the window is full.
    /// </summary>
    /// <param name="value">The value to add.</param>
    public void Add(T value)
    {
        int count = _lowerCount + _upperCount;
        if (count == _entries.Length)
        {
            Replace(value);
            return;
        }

        // While filling, the value takes slot `count`. The low side must hold rank ⌊q · count⌋ of the
        // grown window at its root, so its target size is that rank plus one. The rank rises by at most one
        // per value, and the clamp keeps it to that even where rounding of q · count could suggest otherwise,
        // so exactly one side grows by exactly one.
        int slot = count;
        int target = Math.Clamp((int)(_quantile * count) + 1, _lowerCount, _lowerCount + 1);
        if (target > _lowerCount)
        {
            if (_upperCount == 0 || Compare(value, UpperRoot.Value) <= 0)
            {
                PushLower(value, slot);
            }
            else
            {
                // The value belongs on the high side, but the low side is the one to grow: it takes the high
                // side's minimum, and the value takes that minimum's place.
                Entry moved = UpperRoot;
                UpperRoot = new Entry(value, slot);
                SiftDownUpper(0);
                PushLower(moved.Value, moved.Slot);
            }
        }
        else if (Compare(value, _entries[0].Value) >= 0)
        {
            PushUpper(value, slot);
        }
        else
        {
            // The mirror case: the value belongs on the low side, which hands its maximum across.
            Entry moved = _entries[0];
            _entries[0] = new Entry(value, slot);
            SiftDownLower(0);
            PushUpper(moved.Value, moved.Slot);
        }
    }

    /// <summary>Appends every value in a span to the window, in order.</summary>
    /// <param name="values">The values to add.</param>
    /// <remarks>
    /// There is no <see cref="IEnumerable{T}"/> overload: an overload set holding both a span and a sequence
    /// makes an array argument ambiguous under C# 12, and a sequence is a one-line <c>foreach</c> over
    /// <see cref="Add(T)"/>.
    /// </remarks>
    public void Add(ReadOnlySpan<T> values)
    {
        foreach (T value in values)
        {
            Add(value);
        }
    }

    /// <summary>Empties the window. The window size and quantile are kept.</summary>
    /// <remarks>Runs in <c>O(Count)</c>, releasing the references the window held.</remarks>
    public void Clear()
    {
        Array.Clear(_entries, 0, _lowerCount);
        Array.Clear(_entries, _entries.Length - _upperCount, _upperCount);
        _lowerCount = 0;
        _upperCount = 0;
        _head = 0;
    }

    // The window is full: the new value reuses the oldest value's slot, taking its position in whichever
    // heap held it when the value belongs on that side, or trading places with the other side's root when
    // it does not. Both heaps keep their sizes, so the quantile's rank does not move.
    private void Replace(T value)
    {
        int slot = _head;
        int position = _where[slot];
        if (position >= 0)
        {
            if (_upperCount == 0 || Compare(value, UpperRoot.Value) <= 0)
            {
                _entries[position] = new Entry(value, slot);
                FixLower(position);
            }
            else
            {
                // The high side's minimum is at least every low value, so it can only rise from here.
                Entry moved = UpperRoot;
                UpperRoot = new Entry(value, slot);
                SiftDownUpper(0);
                _entries[position] = moved;
                SiftUpLower(position);
            }
        }
        else
        {
            position = ~position;
            if (Compare(value, _entries[0].Value) >= 0)
            {
                Upper(position) = new Entry(value, slot);
                FixUpper(position);
            }
            else
            {
                // The low side's maximum is at most every high value, so it can only rise from here.
                Entry moved = _entries[0];
                _entries[0] = new Entry(value, slot);
                SiftDownLower(0);
                Upper(position) = moved;
                SiftUpUpper(position);
            }
        }

        _head = slot + 1 == _entries.Length ? 0 : slot + 1;
    }

    private static int Compare(T x, T y) => Comparer<T>.Default.Compare(x, y);

    private ref Entry UpperRoot => ref _entries[_entries.Length - 1];

    private ref Entry Upper(int index) => ref _entries[_entries.Length - 1 - index];

    private void PushLower(T value, int slot)
    {
        int index = _lowerCount++;
        _entries[index] = new Entry(value, slot);
        SiftUpLower(index);
    }

    private void PushUpper(T value, int slot)
    {
        int index = _upperCount++;
        Upper(index) = new Entry(value, slot);
        SiftUpUpper(index);
    }

    private void FixLower(int index)
    {
        if (index > 0 && Compare(_entries[index].Value, _entries[(index - 1) >> 1].Value) > 0)
        {
            SiftUpLower(index);
        }
        else
        {
            SiftDownLower(index);
        }
    }

    private void FixUpper(int index)
    {
        if (index > 0 && Compare(Upper(index).Value, Upper((index - 1) >> 1).Value) < 0)
        {
            SiftUpUpper(index);
        }
        else
        {
            SiftDownUpper(index);
        }
    }

    // Each sift carries one entry along its path and records the final position of every entry it moves, so
    // _where stays exact for the eviction that will eventually come for each slot.
    private void SiftUpLower(int index)
    {
        Entry entry = _entries[index];
        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            Entry above = _entries[parent];
            if (Compare(entry.Value, above.Value) <= 0)
            {
                break;
            }

            _entries[index] = above;
            _where[above.Slot] = index;
            index = parent;
        }

        _entries[index] = entry;
        _where[entry.Slot] = index;
    }

    private void SiftDownLower(int index)
    {
        Entry entry = _entries[index];
        int count = _lowerCount;

        // `index < count / 2` is "has a left child", tested before 2 · index + 1 is formed, so a window past
        // 2^30 entries cannot overflow the child index.
        while (index < count / 2)
        {
            int child = (2 * index) + 1;
            if (child + 1 < count && Compare(_entries[child + 1].Value, _entries[child].Value) > 0)
            {
                child++;
            }

            Entry below = _entries[child];
            if (Compare(below.Value, entry.Value) <= 0)
            {
                break;
            }

            _entries[index] = below;
            _where[below.Slot] = index;
            index = child;
        }

        _entries[index] = entry;
        _where[entry.Slot] = index;
    }

    private void SiftUpUpper(int index)
    {
        Entry entry = Upper(index);
        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            Entry above = Upper(parent);
            if (Compare(entry.Value, above.Value) >= 0)
            {
                break;
            }

            Upper(index) = above;
            _where[above.Slot] = ~index;
            index = parent;
        }

        Upper(index) = entry;
        _where[entry.Slot] = ~index;
    }

    private void SiftDownUpper(int index)
    {
        Entry entry = Upper(index);
        int count = _upperCount;

        // `index < count / 2` is "has a left child", tested before 2 · index + 1 is formed, so a window past
        // 2^30 entries cannot overflow the child index.
        while (index < count / 2)
        {
            int child = (2 * index) + 1;
            if (child + 1 < count && Compare(Upper(child + 1).Value, Upper(child).Value) < 0)
            {
                child++;
            }

            Entry below = Upper(child);
            if (Compare(below.Value, entry.Value) >= 0)
            {
                break;
            }

            Upper(index) = below;
            _where[below.Slot] = ~index;
            index = child;
        }

        Upper(index) = entry;
        _where[entry.Slot] = ~index;
    }

    // A window value and the slot it arrived in. The value is stored inline so a sift compares without a
    // second load; the slot lets the sift keep _where current.
    private readonly struct Entry
    {
        public readonly T Value;
        public readonly int Slot;

        public Entry(T value, int slot)
        {
            Value = value;
            Slot = slot;
        }
    }
}
