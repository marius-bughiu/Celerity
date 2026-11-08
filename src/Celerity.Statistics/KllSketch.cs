using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Primitives;

namespace Celerity.Statistics;

/// <summary>
/// A mergeable quantile sketch with a <strong>rank</strong>-error guarantee, over any comparable type: the
/// KLL sketch of Karnin, Lang and Liberty. It summarizes a stream of any length in about <c>3 · K</c>
/// retained values, and every answer it gives is one of the values it was fed.
/// </summary>
/// <typeparam name="T">The type of the values. Ordered by <see cref="Comparer{T}.Default"/>.</typeparam>
/// <remarks>
/// <para>
/// <see cref="DDSketch"/> answers quantiles of <see cref="double"/> values, and its promise is about the
/// <em>value</em>: the reported p99 is within a relative <c>α</c> of the true one. This type answers
/// quantiles of strings, timestamps, <see cref="decimal"/>, version numbers or any other
/// <see cref="IComparable{T}"/>, and its promise is about the <em>rank</em>: the value it reports for a
/// quantile <c>q</c> sits within about <c>±ε · Count</c> positions of rank <c>q · Count</c> in the sorted
/// stream. That is the guarantee that holds where a relative one says nothing — values near zero, mixed
/// signs, discrete data with heavy ties — and it is the one a "what fraction of requests were below
/// this?" question needs, which <see cref="GetRank(T)"/> answers directly.
/// </para>
/// <para>
/// The BCL has no quantile type, so the alternative is to keep every value in a <see cref="List{T}"/> and
/// sort it per query: memory that grows with the stream and <c>O(n log n)</c> an answer once the stream has
/// moved. This sketch holds a stack of compactors. Level <c>h</c> holds values that each stand for
/// <c>2^h</c> of the stream; when the buffer fills, the lowest level over its capacity is sorted, every
/// other value of it is kept (which half is a coin flip), and the survivors are merged one level up with
/// their weight doubled. Capacities shrink geometrically by <c>2/3</c> down the stack from <c>K</c> at the
/// top, so the whole sketch retains about <c>3 · K</c> values however long the stream runs.
/// </para>
/// <para>
/// <strong>Accuracy.</strong> The error is probabilistic, over the coin flips, and depends only on
/// <see cref="K"/>: at the default <c>K = 200</c> the normalized rank error is typically under 1% and,
/// across the fixed-seed streams this library tests, never more than 2%. Doubling <c>K</c> roughly halves
/// it. Until a stream outgrows the first compaction — about <c>K</c> values — nothing has been discarded
/// and every answer is exact. <see cref="GetQuantile(double)"/> at <c>0</c> and <c>1</c> returns the exact
/// <see cref="Min"/> and <see cref="Max"/>, which the sketch tracks beside the compactors.
/// </para>
/// <para>
/// <strong>Randomness.</strong> The coin comes from a <see cref="Pcg32"/> seeded at construction, so two
/// sketches built with the same <see cref="K"/> and seed and fed the same stream are identical. The
/// unseeded constructors use a fixed default seed. The guarantee assumes the stream does not depend on the
/// coin flips; a stream crafted by someone who knows the seed can defeat it, so seed from a secret when the
/// input is adversarial.
/// </para>
/// <para>
/// Two sketches merge with <see cref="Merge(KllSketch{T})"/>, level by level, so per-shard sketches combine
/// into one summary of the whole without replaying any input. Adding a value allocates nothing except when
/// the stack grows a level, which happens <c>O(log Count)</c> times over a stream. A query sorts the retained values
/// into a cached view — <c>O(K log K)</c>, independent of <see cref="Count"/> — and reads it by binary
/// search. An <see cref="Add(T)"/> that does not compact inserts its value into that view in place, in
/// <c>O(K)</c>, so a stream that queries after every value pays the full sort only once every few values
/// rather than every time.
/// </para>
/// <para>
/// Not thread-safe, including for concurrent readers: a query may build the cached view.
/// </para>
/// <para>
/// Values are assumed to compare without throwing. <see cref="Add(T)"/> and <see cref="Merge(KllSketch{T})"/>
/// compare against the extremes before changing anything, but a <see cref="IComparable{T}.CompareTo(T)"/> that
/// throws part-way through a compaction leaves the sketch's contents unspecified.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The median of a stream of event timestamps, and the fraction of events before a cutoff.
/// var sketch = new KllSketch&lt;DateTimeOffset&gt;();
/// foreach (DateTimeOffset seen in events)
/// {
///     sketch.Add(seen);
/// }
///
/// DateTimeOffset median = sketch.GetQuantile(0.5);
/// double before = sketch.GetRank(cutoff); // the fraction of events at or before the cutoff
/// </code>
/// </example>
public sealed class KllSketch<T> where T : IComparable<T>
{
    /// <summary>The smallest accepted <see cref="K"/>, which is also the floor on every level's capacity.</summary>
    public const int MinK = 8;

    /// <summary>The largest accepted <see cref="K"/>.</summary>
    public const int MaxK = 65_535;

    /// <summary>The <see cref="K"/> the parameterless constructor uses.</summary>
    public const int DefaultK = 200;

    // The seed the unseeded constructors use. Any constant works; this one is the golden-ratio increment.
    private const ulong DefaultSeed = 0x9E37_79B9_7F4A_7C15UL;

    // Past this depth a level's capacity is the floor for every K, and 3^depth would no longer fit a long.
    private const int MaxScaledDepth = 30;

    private static readonly long[] PowersOfThree = BuildPowersOfThree();

    private readonly int _k;

    // One buffer holds every level, lowest first, packed against the end: free space, then level 0, then
    // level 1 and so on up to the top. Level h is [_levels[h], _levels[h + 1]); _levels[0] is therefore the
    // free space, and a new value is written just below it. Its length is the total capacity of the stack.
    private T[] _items;
    private int[] _levels;
    private int _levelCount;

    private long _count;
    private T _min = default!;
    private T _max = default!;

    private Pcg32 _rng;
    private ulong _coinBits;
    private int _coinsLeft;

    // The retained values in sorted order with their cumulative weights. Rebuilt by the first query after a
    // compaction, merge or clear; an add that does not compact patches it in place. Valid while _sortedValid
    // is set.
    private T[]? _sortedItems;
    private long[]? _sortedWeights;
    private int _sortedLength;
    private bool _sortedValid;

    /// <summary>Initializes an empty sketch with <see cref="DefaultK"/> and the default seed.</summary>
    public KllSketch()
        : this(DefaultK, DefaultSeed)
    {
    }

    /// <summary>Initializes an empty sketch with the given accuracy parameter and the default seed.</summary>
    /// <param name="k">
    /// The accuracy parameter, from <see cref="MinK"/> to <see cref="MaxK"/>: the capacity of the top level,
    /// and roughly a third of the values retained. Doubling it roughly halves the rank error.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> is outside <c>[MinK, MaxK]</c>.</exception>
    public KllSketch(int k)
        : this(k, DefaultSeed)
    {
    }

    /// <summary>Initializes an empty sketch with the given accuracy parameter and coin seed.</summary>
    /// <param name="k">
    /// The accuracy parameter, from <see cref="MinK"/> to <see cref="MaxK"/>: the capacity of the top level,
    /// and roughly a third of the values retained. Doubling it roughly halves the rank error.
    /// </param>
    /// <param name="seed">
    /// The seed of the generator that decides which half of a level survives each compaction. Every value is
    /// valid; the same seed, <paramref name="k"/> and stream always give the same sketch.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="k"/> is outside <c>[MinK, MaxK]</c>.</exception>
    public KllSketch(int k, ulong seed)
    {
        if (k < MinK || k > MaxK)
        {
            throw new ArgumentOutOfRangeException(
                nameof(k),
                k,
                $"K must be between {MinK} and {MaxK}.");
        }

        _k = k;
        _rng = new Pcg32(seed);
        _levels = new int[4];
        _items = new T[k];
        Reset();
    }

    /// <summary>Gets the accuracy parameter the sketch was built with.</summary>
    public int K => _k;

    /// <summary>Gets the number of values the sketch has consumed, including those folded in by a merge.</summary>
    public long Count => _count;

    /// <summary>Gets a value indicating whether the sketch has consumed no values.</summary>
    public bool IsEmpty => _count == 0;

    /// <summary>
    /// Gets the number of values the sketch currently retains — its footprint, in values. About
    /// <c>3 · K</c> once the stream is long, and <see cref="Count"/> until the first compaction.
    /// </summary>
    public int RetainedCount => _items.Length - _levels[0];

    /// <summary>Gets the smallest value consumed. Tracked exactly, not read off the compactors.</summary>
    /// <exception cref="InvalidOperationException">The sketch is empty.</exception>
    public T Min
    {
        get
        {
            ThrowIfEmpty();
            return _min;
        }
    }

    /// <summary>Gets the largest value consumed. Tracked exactly, not read off the compactors.</summary>
    /// <exception cref="InvalidOperationException">The sketch is empty.</exception>
    public T Max
    {
        get
        {
            ThrowIfEmpty();
            return _max;
        }
    }

    /// <summary>Adds one value to the sketch.</summary>
    /// <param name="value">The value to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="OverflowException"><see cref="Count"/> is already <see cref="long.MaxValue"/>.</exception>
    public void Add(T value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        // Reachable only through merges, which can double the count each time; checked before anything
        // changes, as Merge checks its own sum.
        if (_count == long.MaxValue)
        {
            throw new OverflowException("The sketch's count would exceed Int64.MaxValue.");
        }

        // The extremes are compared before anything is written, so a CompareTo that throws leaves the
        // sketch exactly as it was.
        bool newMin = _count == 0 || Comparer<T>.Default.Compare(value, _min) < 0;
        bool newMax = _count == 0 || Comparer<T>.Default.Compare(value, _max) > 0;

        // The view is marked stale before anything changes and marked live again only once the patch below
        // has gone in, so an exception part-way through cannot leave a stale view that looks current.
        bool patchView = _sortedValid;
        _sortedValid = false;
        if (_levels[0] == 0)
        {
            CompressOnce();
            patchView = false;
        }

        int slot = --_levels[0];
        _items[slot] = value;

        if (newMin)
        {
            _min = value;
        }

        if (newMax)
        {
            _max = value;
        }

        _count++;

        // An add that did not compact changes the retained set by one value of weight one, so a live view
        // takes it in place rather than being rebuilt by the next query.
        if (patchView)
        {
            InsertIntoView(value);
            _sortedValid = true;
        }
    }

    /// <summary>Adds every value in a span to the sketch, in order.</summary>
    /// <param name="values">The values to add.</param>
    /// <exception cref="ArgumentNullException">
    /// A value is <c>null</c>. The values before it have already been added.
    /// </exception>
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

    /// <summary>
    /// Gets the value at a quantile of the stream: a retained value whose rank is within the sketch's error
    /// of zero-based rank <c>⌊quantile · (Count − 1)⌋</c>.
    /// </summary>
    /// <param name="quantile">
    /// The fraction of the stream at or below the answer, from <c>0</c> (the exact <see cref="Min"/>) to
    /// <c>1</c> (the exact <see cref="Max"/>); <c>0.5</c> is the median.
    /// </param>
    /// <returns>A value the sketch was fed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="quantile"/> is outside <c>[0, 1]</c> or is <see cref="double.NaN"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The sketch is empty.</exception>
    public T GetQuantile(double quantile)
    {
        // Written as a negated range test so that NaN, which fails every comparison, is rejected too.
        if (!(quantile >= 0d && quantile <= 1d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantile),
                quantile,
                "Quantile must be between 0 and 1 inclusive.");
        }

        ThrowIfEmpty();

        // ⌊quantile · (Count − 1)⌋. Past 2^53 the product rounds, which moves the rank by far less than the
        // sketch's own error; the one case that matters, a quantile of 1 at a count near long.MaxValue, could
        // round past the range of a long, so it is answered without the multiply.
        long last = _count - 1;
        long rank = quantile == 1d ? last : (long)(quantile * last);
        if (rank == 0)
        {
            return _min;
        }

        if (rank >= last)
        {
            return _max;
        }

        EnsureSorted();

        // The first retained value whose cumulative weight passes the rank. The total weight is Count, and
        // the rank is below it, so the search always lands inside the view.
        long[] weights = _sortedWeights!;
        int lo = 0;
        int hi = _sortedLength - 1;
        while (lo < hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            if (weights[mid] > rank)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return _sortedItems![lo];
    }

    /// <summary>Gets the values at several quantiles at once, into a caller-owned span.</summary>
    /// <param name="quantiles">The quantiles, each from <c>0</c> to <c>1</c>.</param>
    /// <param name="destination">Receives the value at each quantile, in the same order.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is shorter than <paramref name="quantiles"/>, or overlaps it.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A quantile is outside <c>[0, 1]</c> or is <see cref="double.NaN"/>.</exception>
    /// <exception cref="InvalidOperationException">The sketch is empty and <paramref name="quantiles"/> is not.</exception>
    /// <remarks>Allocates nothing once the sorted view has been built.</remarks>
    public void GetQuantiles(ReadOnlySpan<double> quantiles, Span<T> destination)
    {
        if (destination.Length < quantiles.Length)
        {
            throw new ArgumentException(
                "The destination is shorter than the quantile list.",
                nameof(destination));
        }

        // Only a double destination can share memory with the quantile list, and writing forward into one
        // that starts inside the list would overwrite a quantile before it is read.
        if (typeof(T) == typeof(double)
            && quantiles.Overlaps(MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<T, double>(ref MemoryMarshal.GetReference(destination)),
                destination.Length)))
        {
            throw new ArgumentException(
                "The destination must not overlap the quantile list; it is written while the " +
                "list is still being read.",
                nameof(destination));
        }

        for (int i = 0; i < quantiles.Length; i++)
        {
            destination[i] = GetQuantile(quantiles[i]);
        }
    }

    /// <summary>
    /// Gets the estimated fraction of the stream that is less than or equal to <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value to rank. It need not be one the sketch was fed.</param>
    /// <returns>
    /// A fraction from <c>0</c> to <c>1</c>, within the sketch's rank error of the true one: <c>0</c> below
    /// every retained value and <c>1</c> at or above <see cref="Max"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The sketch is empty.</exception>
    public double GetRank(T value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        ThrowIfEmpty();
        EnsureSorted();

        // The number of retained values at or below the probe: an upper-bound binary search.
        T[] items = _sortedItems!;
        int lo = 0;
        int hi = _sortedLength;
        while (lo < hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            if (Comparer<T>.Default.Compare(items[mid], value) <= 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo == 0 ? 0d : (double)_sortedWeights![lo - 1] / _count;
    }

    /// <summary>
    /// Folds another sketch's values into this one, as if every value it consumed had been added here.
    /// </summary>
    /// <param name="other">
    /// The sketch to fold in. It is left unchanged, and may be this sketch. Its <see cref="K"/> may differ;
    /// this sketch keeps its own, and the result is no more accurate than the less accurate input.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="OverflowException">The combined <see cref="Count"/> would overflow.</exception>
    /// <remarks>
    /// Runs in time proportional to the two sketches' retained values, and allocates a new buffer.
    /// </remarks>
    public void Merge(KllSketch<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other._count == 0)
        {
            return;
        }

        long count = checked(_count + other._count);

        // Every read of the other sketch happens here, before any field of this one changes, so merging a
        // sketch into itself sees one consistent copy of it.
        int levelCount = Math.Max(_levelCount, other._levelCount);
        int retained = RetainedCount + other.RetainedCount;
        int length = Math.Max(TotalCapacity(_k, levelCount), retained);
        var items = new T[length];
        int[] levels = new int[Math.Max(_levels.Length, levelCount + 2)];

        // Lay the combined levels out from the top down so the buffer stays packed against its end.
        int end = length;
        levels[levelCount] = end;
        for (int h = levelCount - 1; h >= 0; h--)
        {
            int ownSize = h < _levelCount ? _levels[h + 1] - _levels[h] : 0;
            int otherSize = h < other._levelCount ? other._levels[h + 1] - other._levels[h] : 0;
            int start = end - ownSize - otherSize;
            if (ownSize > 0)
            {
                Array.Copy(_items, _levels[h], items, start, ownSize);
            }

            if (otherSize > 0)
            {
                Array.Copy(other._items, other._levels[h], items, start + ownSize, otherSize);
            }

            // Every level but the lowest is kept sorted; two sorted runs side by side are not.
            if (h > 0 && ownSize > 0 && otherSize > 0)
            {
                Array.Sort(items, start, ownSize + otherSize);
            }

            levels[h] = start;
            end = start;
        }

        // The extremes are compared before any field is replaced, so a CompareTo that throws leaves this
        // sketch exactly as it was.
        bool wasEmpty = _count == 0;
        T otherMin = other._min;
        T otherMax = other._max;
        bool newMin = wasEmpty || Comparer<T>.Default.Compare(otherMin, _min) < 0;
        bool newMax = wasEmpty || Comparer<T>.Default.Compare(otherMax, _max) > 0;

        _items = items;
        _levels = levels;
        _levelCount = levelCount;

        if (newMin)
        {
            _min = otherMin;
        }

        if (newMax)
        {
            _max = otherMax;
        }

        _count = count;
        _sortedValid = false;

        // Compact until the stack fits its own capacity again, then trim the buffer to exactly that.
        while (RetainedCount > TotalCapacity(_k, _levelCount))
        {
            CompressOnce();
        }

        int capacity = TotalCapacity(_k, _levelCount);
        if (_items.Length != capacity)
        {
            var trimmed = new T[capacity];
            int shift = capacity - _items.Length;
            Array.Copy(_items, _levels[0], trimmed, _levels[0] + shift, RetainedCount);
            for (int h = 0; h <= _levelCount; h++)
            {
                _levels[h] += shift;
            }

            _items = trimmed;
        }
    }

    /// <summary>Empties the sketch. <see cref="K"/> is kept, and the coin generator continues its sequence.</summary>
    /// <remarks>
    /// Releases the values the sketch held and shrinks the buffer back to a single level. The cached sorted
    /// view is zeroed but keeps its size, so a sketch that grew large and is reused keeps that much memory.
    /// </remarks>
    public void Clear()
    {
        if (_items.Length == _k)
        {
            Array.Clear(_items);
        }
        else
        {
            _items = new T[_k];
        }

        if (_sortedItems is not null)
        {
            Array.Clear(_sortedItems);
        }

        Reset();
    }

    private void Reset()
    {
        _levelCount = 1;
        _levels[0] = _k;
        _levels[1] = _k;
        _count = 0;
        _min = default!;
        _max = default!;
        _sortedLength = 0;
        _sortedValid = false;
    }

    private void ThrowIfEmpty()
    {
        if (_count == 0)
        {
            throw new InvalidOperationException("The sketch is empty.");
        }
    }

    // Frees space by compacting the lowest level at or over its capacity. One always exists when the buffer
    // is full, because the capacities sum to the buffer's length; if it is the top level, the stack first
    // grows a level for its survivors to move into.
    private void CompressOnce()
    {
        int level = 0;
        while (_levels[level + 1] - _levels[level] < LevelCapacity(_k, _levelCount, level))
        {
            level++;
        }

        if (level == _levelCount - 1)
        {
            AddLevel();
        }

        CompactLevel(level);
    }

    private void AddLevel()
    {
        int newCount = _levelCount + 1;
        if (_levels.Length < newCount + 1)
        {
            Array.Resize(ref _levels, _levels.Length * 2);
        }

        int length = Math.Max(_items.Length, TotalCapacity(_k, newCount));
        int shift = length - _items.Length;
        if (shift > 0)
        {
            var items = new T[length];
            Array.Copy(_items, _levels[0], items, _levels[0] + shift, RetainedCount);
            for (int h = 0; h <= _levelCount; h++)
            {
                _levels[h] += shift;
            }

            _items = items;
        }

        // The new top level starts empty, at the end of the buffer.
        _levels[newCount] = _levels[_levelCount];
        _levelCount = newCount;
    }

    // Halves one level into the next. Level `level` is never the top when this runs.
    private void CompactLevel(int level)
    {
        T[] items = _items;
        int start = _levels[level];
        int end = _levels[level + 1];
        int upperEnd = _levels[level + 2];

        // An odd level keeps its first value back at its own weight; the rest pair off.
        int pairedStart = start + ((end - start) & 1);
        int half = (end - pairedStart) >> 1;

        // Level 0 is filled in arrival order; every higher level is already sorted.
        if (level == 0)
        {
            Array.Sort(items, pairedStart, end - pairedStart);
        }

        // Keep the even- or odd-indexed values of the sorted run, packed to its front. The read index never
        // falls behind the write index, so the copy is safe in place.
        int offset = NextCoin();
        for (int i = 0; i < half; i++)
        {
            items[pairedStart + i] = items[pairedStart + offset + (2 * i)];
        }

        // Merge the survivors with the next level into the space that ends where that level ends. The write
        // index starts `half` past the survivors and never passes the next level's read index, so neither
        // run is overwritten before it is read.
        int a = pairedStart;
        int aEnd = pairedStart + half;
        int b = end;
        int write = aEnd;
        while (a < aEnd && b < upperEnd)
        {
            items[write++] = Comparer<T>.Default.Compare(items[b], items[a]) < 0 ? items[b++] : items[a++];
        }

        while (a < aEnd)
        {
            items[write++] = items[a++];
        }

        // Slide everything below the survivors — the lower levels and any held-back value — up into the space
        // they vacated, and release the slots that become free.
        int low = _levels[0];
        Array.Copy(items, low, items, low + half, pairedStart - low);
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(items, low, half);
        }

        for (int h = 0; h <= level; h++)
        {
            _levels[h] += half;
        }

        _levels[level + 1] = pairedStart + half;
    }

    private int NextCoin()
    {
        if (_coinsLeft == 0)
        {
            _coinBits = _rng.NextUInt64();
            _coinsLeft = 64;
        }

        int bit = (int)(_coinBits & 1UL);
        _coinBits >>= 1;
        _coinsLeft--;
        return bit;
    }

    // Rebuilds the sorted view: every retained value with its weight, sorted, weights made cumulative.
    private void EnsureSorted()
    {
        if (_sortedValid)
        {
            return;
        }

        // Sized to the whole buffer, not just what is retained, so the in-place insert of an add that does
        // not compact always has a free slot.
        int retained = RetainedCount;
        if (_sortedItems is null || _sortedItems.Length < _items.Length)
        {
            _sortedItems = new T[_items.Length];
            _sortedWeights = new long[_items.Length];
        }

        T[] sorted = _sortedItems;
        long[] weights = _sortedWeights!;
        int write = 0;
        for (int h = 0; h < _levelCount; h++)
        {
            long weight = 1L << h;
            for (int i = _levels[h]; i < _levels[h + 1]; i++)
            {
                sorted[write] = _items[i];
                weights[write] = weight;
                write++;
            }
        }

        Array.Sort(sorted, weights, 0, retained);

        long cumulative = 0;
        for (int i = 0; i < retained; i++)
        {
            cumulative += weights[i];
            weights[i] = cumulative;
        }

        // A view that shrank (after a merge or Clear) would otherwise keep stale references past its end.
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() && _sortedLength > retained)
        {
            Array.Clear(sorted, retained, _sortedLength - retained);
        }

        _sortedLength = retained;
        _sortedValid = true;
    }

    // Inserts one weight-one value into the live view after its equals, keeping the weights cumulative.
    // The view is at least as long as the buffer, and the add that called this did not fill it.
    private void InsertIntoView(T value)
    {
        T[] items = _sortedItems!;
        long[] weights = _sortedWeights!;
        int length = _sortedLength;

        int lo = 0;
        int hi = length;
        while (lo < hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            if (Comparer<T>.Default.Compare(items[mid], value) <= 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        Array.Copy(items, lo, items, lo + 1, length - lo);
        Array.Copy(weights, lo, weights, lo + 1, length - lo);
        items[lo] = value;
        weights[lo] = (lo == 0 ? 0L : weights[lo - 1]) + 1L;
        for (int i = lo + 1; i <= length; i++)
        {
            weights[i]++;
        }

        _sortedLength = length + 1;
    }

    // The capacity of one level in a stack of `levelCount`: K at the top, shrinking by 2/3 per level below it,
    // never under MinK. Computed in integers, as round(K · (2/3)^depth), so it is identical on every platform.
    private static int LevelCapacity(int k, int levelCount, int level)
    {
        int depth = levelCount - 1 - level;
        if (depth > MaxScaledDepth)
        {
            return MinK;
        }

        long scaled = (((long)k << (depth + 1)) / PowersOfThree[depth] + 1) >> 1;
        return (int)Math.Max(scaled, MinK);
    }

    private static int TotalCapacity(int k, int levelCount)
    {
        int total = 0;
        for (int h = 0; h < levelCount; h++)
        {
            total += LevelCapacity(k, levelCount, h);
        }

        return total;
    }

    private static long[] BuildPowersOfThree()
    {
        var powers = new long[MaxScaledDepth + 1];
        powers[0] = 1;
        for (int i = 1; i < powers.Length; i++)
        {
            powers[i] = powers[i - 1] * 3;
        }

        return powers;
    }
}
