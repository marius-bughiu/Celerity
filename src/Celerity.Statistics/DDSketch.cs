namespace Celerity.Statistics;

/// <summary>
/// A mergeable quantile sketch with a <em>relative</em>-error guarantee, in memory
/// proportional to the logarithm of the value range rather than to the number of values.
/// </summary>
/// <remarks>
/// <para>
/// The BCL ships no quantile type. To answer "what is the 99th percentile latency?" a caller
/// keeps every sample in a <see cref="List{T}"/>, sorts it, and indexes — memory that grows
/// without bound and an <c>O(n log n)</c> sort per query. That is fine for a thousand samples
/// and impossible for a billion, and it is the shape this sketch replaces.
/// </para>
/// <para>
/// DDSketch (Masson, Rim &amp; Lee, 2019) maps each value to the bucket
/// <c>ceil(log_γ(v))</c> of a geometric ladder with <c>γ = (1 + α) / (1 − α)</c> and counts
/// how many values landed in each. Because the ladder is geometric rather than uniform, the
/// returned quantile is guaranteed accurate to a <strong>relative</strong> <c>α</c> —
/// <c>|reported − actual| ≤ α · |actual|</c> — which is the guarantee latency work actually
/// wants: 1% of 10&#160;ms and 1% of 10&#160;s, not a fixed number of milliseconds that is
/// meaningless at one end of the range and useless at the other. At the default 1% accuracy a
/// bucket spans 2%, so covering nanoseconds to hours takes about 1,500 of them — inside the
/// default budget.
/// </para>
/// <para>
/// The guarantee holds for <em>any</em> quantile of <em>any</em> distribution, without the
/// data being sorted, seen twice, or known in advance — and unlike a t-digest it holds at the
/// median as strongly as at the tail. Two sketches built with the same accuracy merge
/// exactly with <see cref="Merge(DDSketch)"/>, so per-shard sketches combine into a global
/// one without re-reading anything.
/// </para>
/// <para>
/// <strong>Negative values and zero are handled, not rejected.</strong> Negatives go into a
/// mirrored second ladder and zero into its own counter, because <c>log</c> has nothing to
/// say about either. The mirror is a true one: a negative value is filed under the
/// <em>negated</em> index of its magnitude, so the second ladder runs in ascending value order
/// like the first and its bin budget discards the most negative values rather than the ones
/// nearest zero. Only NaN and the infinities are rejected.
/// </para>
/// <para>
/// <strong>Memory is capped, and the cap is visible.</strong> A stream spanning an
/// unexpectedly wide range would otherwise allocate a bucket per decade forever, so a bin
/// budget bounds each ladder. When it is exhausted the <em>lowest</em> buckets collapse
/// together — the choice that protects the high quantiles people actually query — and
/// <see cref="HasCollapsed"/> turns <c>true</c>, at which point the <c>α</c> guarantee no
/// longer holds for values in the collapsed low tail. It is reported rather than hidden so a
/// caller can widen <c>maxBins</c> instead of trusting a number that has quietly stopped
/// being accurate.
/// </para>
/// <para>
/// <see cref="Count"/>, <see cref="Sum"/>, <see cref="Min"/> and <see cref="Max"/> are tracked
/// exactly and are not subject to <c>α</c>; only the quantiles are approximate. Note that
/// <c>GetQuantile(0)</c> and <c>GetQuantile(1)</c> return the <em>bucketed</em> extremes, so
/// use <see cref="Min"/> / <see cref="Max"/> when the exact ones are wanted.
/// </para>
/// <para>
/// Adding a value is a <c>log</c>, a <c>ceil</c> and an array increment; it allocates only
/// when a ladder has to grow. Not thread-safe.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var latencies = new DDSketch(relativeAccuracy: 0.01);
/// foreach (double ms in requestLatencies)
/// {
///     latencies.Add(ms);
/// }
///
/// Console.WriteLine($"p50 {latencies.GetQuantile(0.5):F2} ms, p99 {latencies.GetQuantile(0.99):F2} ms");
/// </code>
/// </example>
public sealed class DDSketch
{
    /// <summary>
    /// The relative accuracy used when a constructor does not specify one: 1%.
    /// </summary>
    public const double DefaultRelativeAccuracy = 0.01d;

    /// <summary>
    /// The per-ladder bin budget used when a constructor does not specify one: 2048.
    /// </summary>
    /// <remarks>
    /// The value range a budget covers is proportional to <c>maxBins × α</c>, so it shrinks as
    /// the accuracy tightens: 2048 bins span about 17 decades at the default 1% accuracy and
    /// under two at 0.1%. <see cref="HasCollapsed"/> reports when the budget has bound.
    /// </remarks>
    public const int DefaultMaxBins = 2048;

    /// <summary>
    /// The smallest relative accuracy the sketch accepts: <c>1e-6</c>. Below it the bucket
    /// index of an extreme <see cref="double"/> no longer fits in an <see cref="int"/>.
    /// </summary>
    public const double MinRelativeAccuracy = 1e-6d;

    /// <summary>
    /// The largest bin budget the sketch accepts: <c>2^26</c>. The whole reachable bucket
    /// range at <see cref="MinRelativeAccuracy"/> is an order of magnitude wider than this, so
    /// a larger budget could never bind — and it keeps the floor arithmetic clear of
    /// <see cref="int"/> overflow.
    /// </summary>
    public const int MaxBinBudget = 1 << 26;

    private readonly double _gamma;
    private readonly double _logGamma;
    private readonly double _indexMultiplier;
    private readonly double _logValueMultiplier;
    private readonly BucketStore _positive;
    private readonly BucketStore _negative;
    private long _zeroCount;
    private long _count;
    private double _sum;
    private double _min = double.PositiveInfinity;
    private double _max = double.NegativeInfinity;

    /// <summary>
    /// Initializes a sketch with the default 1% relative accuracy and the default bin budget.
    /// </summary>
    public DDSketch()
        : this(DefaultRelativeAccuracy, DefaultMaxBins)
    {
    }

    /// <summary>
    /// Initializes a sketch with the specified relative accuracy and the default bin budget.
    /// </summary>
    /// <param name="relativeAccuracy">
    /// The relative error bound <c>α</c>, in <c>[1e-6, 1)</c>. Smaller is more accurate and
    /// uses more buckets.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="relativeAccuracy"/> is outside <c>[1e-6, 1)</c>, or is
    /// <see cref="double.NaN"/>.
    /// </exception>
    public DDSketch(double relativeAccuracy)
        : this(relativeAccuracy, DefaultMaxBins)
    {
    }

    /// <summary>
    /// Initializes a sketch with the specified relative accuracy and bin budget.
    /// </summary>
    /// <param name="relativeAccuracy">
    /// The relative error bound <c>α</c>, in <c>[1e-6, 1)</c>.
    /// </param>
    /// <param name="maxBins">
    /// The maximum number of live buckets per ladder (the positive and negative ladders are
    /// budgeted separately). Must be in <c>[1, <see cref="MaxBinBudget"/>]</c>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="relativeAccuracy"/> is outside <c>[1e-6, 1)</c> or is
    /// <see cref="double.NaN"/>, or <paramref name="maxBins"/> is outside
    /// <c>[1, <see cref="MaxBinBudget"/>]</c>.
    /// </exception>
    public DDSketch(double relativeAccuracy, int maxBins)
    {
        if (!(relativeAccuracy >= MinRelativeAccuracy) || relativeAccuracy >= 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(relativeAccuracy),
                relativeAccuracy,
                $"Relative accuracy must be in [{MinRelativeAccuracy}, 1).");
        }

        if (maxBins < 1 || maxBins > MaxBinBudget)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBins),
                maxBins,
                $"The bin budget must be in [1, {MaxBinBudget}].");
        }

        RelativeAccuracy = relativeAccuracy;
        MaxBins = maxBins;
        _gamma = (1d + relativeAccuracy) / (1d - relativeAccuracy);
        _logGamma = Math.Log(_gamma);
        _indexMultiplier = 1d / _logGamma;
        _logValueMultiplier = Math.Log(1d - relativeAccuracy);
        _positive = new BucketStore(maxBins);
        _negative = new BucketStore(maxBins);
    }

    /// <summary>Gets the relative error bound the sketch was built with.</summary>
    public double RelativeAccuracy { get; }

    /// <summary>Gets the per-ladder bin budget the sketch was built with.</summary>
    public int MaxBins { get; }

    /// <summary>Gets the exact number of values added.</summary>
    public long Count => _count;

    /// <summary>Gets the exact sum of the values added, or <c>0</c> if none have been.</summary>
    public double Sum => _sum;

    /// <summary>
    /// Gets the exact arithmetic mean of the values added, or <see cref="double.NaN"/> if none
    /// have been.
    /// </summary>
    public double Average => _count == 0 ? double.NaN : _sum / _count;

    /// <summary>
    /// Gets the exact smallest value added — not the bucketed one — or
    /// <see cref="double.NaN"/> if none have been.
    /// </summary>
    public double Min => _count == 0 ? double.NaN : _min;

    /// <summary>
    /// Gets the exact largest value added — not the bucketed one — or
    /// <see cref="double.NaN"/> if none have been.
    /// </summary>
    public double Max => _count == 0 ? double.NaN : _max;

    /// <summary>
    /// Gets the number of live buckets across both ladders, excluding the zero counter — the
    /// sketch's memory footprint, in buckets.
    /// </summary>
    public int BinCount => _positive.BinCount + _negative.BinCount;

    /// <summary>
    /// Gets a value indicating whether the bin budget has been exhausted and low buckets have
    /// been collapsed together. Once <c>true</c>, the <see cref="RelativeAccuracy"/>
    /// guarantee no longer holds for quantiles that fall in the collapsed low tail.
    /// </summary>
    public bool HasCollapsed => _positive.HasCollapsed || _negative.HasCollapsed;

    /// <summary>Adds a single value to the sketch.</summary>
    /// <param name="value">The value to add. Must be finite; zero and negatives are accepted.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="double.NaN"/> or infinite.
    /// </exception>
    public void Add(double value) => Add(value, 1L);

    /// <summary>Adds a value to the sketch with a multiplicity.</summary>
    /// <param name="value">The value to add. Must be finite; zero and negatives are accepted.</param>
    /// <param name="count">How many occurrences of the value to add. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="double.NaN"/> or infinite, or
    /// <paramref name="count"/> is not positive, or adding it would overflow
    /// <see cref="Count"/>.
    /// </exception>
    public void Add(double value, long count)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Only finite values can be added to the sketch.");
        }

        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "The multiplicity must be positive.");
        }

        // Checked before anything is mutated: a wrapped count makes every subsequent quantile
        // rank meaningless, and a half-applied Add would be worse than a rejected one.
        if (count > long.MaxValue - _count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "Adding this many occurrences would overflow the sketch's exact count.");
        }

        if (value > 0d)
        {
            _positive.Add(IndexOf(value), count);
        }
        else if (value < 0d)
        {
            // Negated, so the ladder is ordered by value rather than by magnitude: the most
            // negative value takes the lowest index, which is what the store collapses first.
            _negative.Add(-IndexOf(-value), count);
        }
        else
        {
            _zeroCount += count;
        }

        _count += count;
        _sum += value * count;

        if (value < _min)
        {
            _min = value;
        }

        if (value > _max)
        {
            _max = value;
        }
    }

    /// <summary>Adds every value in a span to the sketch.</summary>
    /// <param name="values">The values to add. All must be finite.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// One of <paramref name="values"/> is <see cref="double.NaN"/> or infinite. Values before
    /// it have already been added.
    /// </exception>
    public void Add(ReadOnlySpan<double> values)
    {
        foreach (double value in values)
        {
            Add(value, 1L);
        }
    }

    /// <summary>
    /// Returns the value at the specified quantile, accurate to a relative
    /// <see cref="RelativeAccuracy"/>.
    /// </summary>
    /// <param name="quantile">The quantile to query, in <c>[0, 1]</c> — <c>0.99</c> for p99.</param>
    /// <returns>
    /// The bucketed value at that quantile, clamped to <see cref="Min"/>..<see cref="Max"/>, or
    /// <see cref="double.NaN"/> if the sketch is empty.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="quantile"/> is outside <c>[0, 1]</c> or is <see cref="double.NaN"/>.
    /// </exception>
    public double GetQuantile(double quantile)
    {
        if (!(quantile >= 0d) || quantile > 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantile),
                quantile,
                "The quantile must be in [0, 1].");
        }

        if (_count == 0)
        {
            return double.NaN;
        }

        // Rank of the requested element in ascending value order: the negative ladder first
        // (filed under the negated magnitude, so the most negative value takes the *smallest*
        // index and this walk ascends like the positive one), then the zeros, then the rest.
        double rank = quantile * (_count - 1);

        if (rank < _negative.Total)
        {
            long cumulative = 0;
            int index = _negative.MinIndex;
            for (; index < _negative.MaxIndex; index++)
            {
                cumulative += _negative.CountAt(index);
                if (cumulative > rank)
                {
                    break;
                }
            }

            return ClampToObserved(-ValueOf(-index));
        }

        rank -= _negative.Total;

        if (rank < _zeroCount)
        {
            return 0d;
        }

        rank -= _zeroCount;

        long positiveCumulative = 0;
        int positiveIndex = _positive.MinIndex;
        for (; positiveIndex < _positive.MaxIndex; positiveIndex++)
        {
            positiveCumulative += _positive.CountAt(positiveIndex);
            if (positiveCumulative > rank)
            {
                break;
            }
        }

        return ClampToObserved(ValueOf(positiveIndex));
    }

    /// <summary>
    /// Writes the values at several quantiles into a caller-owned span, allocating nothing.
    /// </summary>
    /// <param name="quantiles">The quantiles to query, each in <c>[0, 1]</c>.</param>
    /// <param name="destination">
    /// Receives one value per quantile, in the same order. Must be at least as long as
    /// <paramref name="quantiles"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is shorter than <paramref name="quantiles"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// One of <paramref name="quantiles"/> is outside <c>[0, 1]</c> or is
    /// <see cref="double.NaN"/>.
    /// </exception>
    public void GetQuantiles(ReadOnlySpan<double> quantiles, Span<double> destination)
    {
        if (destination.Length < quantiles.Length)
        {
            throw new ArgumentException(
                "The destination is shorter than the quantile list.",
                nameof(destination));
        }

        for (int i = 0; i < quantiles.Length; i++)
        {
            destination[i] = GetQuantile(quantiles[i]);
        }
    }

    /// <summary>
    /// Folds another sketch's values into this one, as if every value it consumed had been
    /// added here.
    /// </summary>
    /// <param name="other">
    /// The sketch to fold in, which must have the same <see cref="RelativeAccuracy"/>. It is
    /// left unchanged. Merging an empty sketch is a no-op.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> was built with a different <see cref="RelativeAccuracy"/>, so
    /// its buckets do not line up with this sketch's, or the combined <see cref="Count"/> would
    /// overflow.
    /// </exception>
    /// <remarks>
    /// The two sketches may differ in <see cref="MaxBins"/>; the result keeps this sketch's
    /// budget, and collapses if the union exceeds it.
    /// </remarks>
    public void Merge(DDSketch other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other.RelativeAccuracy != RelativeAccuracy)
        {
            throw new ArgumentException(
                "Sketches can only be merged when they share the same relative accuracy " +
                $"(this: {RelativeAccuracy}, other: {other.RelativeAccuracy}).",
                nameof(other));
        }

        if (other._count == 0)
        {
            return;
        }

        // Before either store is touched, for the same reason Add checks first: a merge that
        // wrapped the count halfway through would leave the sketch neither merged nor intact.
        if (other._count > long.MaxValue - _count)
        {
            throw new ArgumentException(
                "Merging these sketches would overflow the exact count.",
                nameof(other));
        }

        _positive.Merge(other._positive);
        _negative.Merge(other._negative);
        _zeroCount += other._zeroCount;
        _count += other._count;
        _sum += other._sum;
        _min = Math.Min(_min, other._min);
        _max = Math.Max(_max, other._max);
    }

    /// <summary>Removes every value from the sketch, keeping its accuracy and bin budget.</summary>
    public void Clear()
    {
        _positive.Clear();
        _negative.Clear();
        _zeroCount = 0;
        _count = 0;
        _sum = 0d;
        _min = double.PositiveInfinity;
        _max = double.NegativeInfinity;
    }

    /// <summary>Maps a strictly positive value onto its bucket index.</summary>
    private int IndexOf(double value) => (int)Math.Ceiling(Math.Log(value) * _indexMultiplier);

    /// <summary>
    /// Maps a bucket index back to the representative value <c>(1 − α)·γ^i</c>, which is
    /// within a relative <c>α</c> of every value the bucket can hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written as <c>(1 − α)·γ^i</c> rather than the algebraically identical
    /// <c>2·γ^i / (γ + 1)</c>: the latter reconstructs <c>1 − α</c> through two more roundings,
    /// which is enough to push a value sitting exactly on a bucket boundary a hair outside the
    /// guarantee it is supposed to meet exactly.
    /// </para>
    /// <para>
    /// And evaluated in log space rather than as <c>(1 − α) · Math.Pow(γ, i)</c>, because the
    /// power alone overflows before the multiplier can bring it back: at the default accuracy
    /// <see cref="double.MaxValue"/> lands in bucket 35,488, whose <c>γ^i</c> is not a finite
    /// <see cref="double"/>. <see cref="ClampToObserved"/> catches what is still out of range
    /// after that.
    /// </para>
    /// </remarks>
    private double ValueOf(int index) => Math.Exp((index * _logGamma) + _logValueMultiplier);

    /// <summary>
    /// Pulls a bucket representative back inside the values actually seen.
    /// </summary>
    /// <remarks>
    /// The true quantile is between <see cref="Min"/> and <see cref="Max"/> by definition, so
    /// this can only move an estimate closer to it — a bucket representative sits at a fixed
    /// point in its bucket and the values that landed there need not surround it. It is cheap
    /// insurance rather than a correction: what keeps the top of the <see cref="double"/> range
    /// finite is evaluating <see cref="ValueOf"/> in log space.
    /// </remarks>
    private double ClampToObserved(double value) => Math.Clamp(value, _min, _max);

    /// <summary>
    /// A contiguous count-per-bucket window over a range of bucket indices, which grows to
    /// cover the indices it is given and, once it reaches its bin budget, collapses its
    /// lowest buckets rather than growing further.
    /// </summary>
    /// <remarks>
    /// The window is stored as a plain <c>long[]</c> plus the bucket index of its first
    /// element, so a bucket lookup is one subtraction and one array access. It is kept
    /// centred with slack on both sides, so a stream that trends in one direction pays for a
    /// shift only once per batch of new buckets rather than once per value.
    /// </remarks>
    private sealed class BucketStore
    {
        private const int InitialCapacity = 8;

        private readonly int _maxBins;
        private long[] _counts;
        private int _offset;
        private int _minIndex = int.MaxValue;
        private int _maxIndex = int.MinValue;
        private long _total;

        internal BucketStore(int maxBins)
        {
            _maxBins = maxBins;
            _counts = new long[Math.Min(InitialCapacity, maxBins)];
        }

        internal long Total => _total;

        internal int MinIndex => _minIndex;

        internal int MaxIndex => _maxIndex;

        internal bool HasCollapsed { get; private set; }

        internal int BinCount => _total == 0 ? 0 : _maxIndex - _minIndex + 1;

        internal long CountAt(int index) => _counts[index - _offset];

        internal void Add(int index, long count)
        {
            if (_total == 0)
            {
                _offset = index - ((_counts.Length - 1) / 2);
                _minIndex = index;
                _maxIndex = index;
                _counts[index - _offset] = count;
                _total = count;
                return;
            }

            // The slot is resolved into a local first, deliberately: SlotFor can replace
            // _counts with a larger array, and a compound assignment would have evaluated the
            // old array reference before calling it.
            int slot = SlotFor(index);
            _counts[slot] += count;
            _total += count;
        }

        internal void Merge(BucketStore other)
        {
            // A collapse in the source is not undone by copying its surviving buckets, so the
            // flag has to travel with them or the result would claim an accuracy it lost.
            HasCollapsed |= other.HasCollapsed;

            for (int index = other._minIndex; index <= other._maxIndex; index++)
            {
                long count = other.CountAt(index);
                if (count != 0)
                {
                    Add(index, count);
                }
            }
        }

        internal void Clear()
        {
            Array.Clear(_counts);
            _minIndex = int.MaxValue;
            _maxIndex = int.MinValue;
            _total = 0;
            HasCollapsed = false;
        }

        /// <summary>
        /// Returns the array slot holding <paramref name="index"/>'s count, extending or
        /// collapsing the window as needed. A bucket below the budget's floor resolves to the
        /// lowest live slot, which is where its count belongs once collapsed.
        /// </summary>
        private int SlotFor(int index)
        {
            if (index >= _minIndex && index <= _maxIndex)
            {
                return index - _offset;
            }

            if (index > _maxIndex)
            {
                int floor = index - _maxBins + 1;
                if (floor > _minIndex)
                {
                    CollapseBelow(floor);
                }

                EnsureWindow(_minIndex, index);
                _maxIndex = index;
                return index - _offset;
            }

            if (index <= _maxIndex - _maxBins)
            {
                HasCollapsed = true;
                return _minIndex - _offset;
            }

            EnsureWindow(index, _maxIndex);
            _minIndex = index;
            return index - _offset;
        }

        /// <summary>
        /// Folds every bucket below <paramref name="floor"/> into it. When the whole live
        /// window is below the floor, the window becomes the single bucket at the floor.
        /// </summary>
        private void CollapseBelow(int floor)
        {
            HasCollapsed = true;

            long collapsed = 0;
            int last = Math.Min(floor - 1, _maxIndex);
            for (int index = _minIndex; index <= last; index++)
            {
                collapsed += _counts[index - _offset];
                _counts[index - _offset] = 0;
            }

            if (floor <= _maxIndex)
            {
                _counts[floor - _offset] += collapsed;
                _minIndex = floor;
                return;
            }

            _offset = floor;
            _minIndex = floor;
            _maxIndex = floor;
            _counts[0] = collapsed;
        }

        /// <summary>
        /// Makes the backing array able to address every bucket in <c>[min, max]</c>, growing
        /// it or sliding the live window inside it. The caller guarantees the range fits the
        /// bin budget.
        /// </summary>
        private void EnsureWindow(int min, int max)
        {
            int needed = max - min + 1;
            int live = _maxIndex - _minIndex + 1;

            if (needed > _counts.Length)
            {
                int length = Math.Min(_maxBins, Math.Max(needed, _counts.Length * 2));
                long[] grown = new long[length];
                int grownOffset = min - ((length - needed) / 2);
                Array.Copy(_counts, _minIndex - _offset, grown, _minIndex - grownOffset, live);
                _counts = grown;
                _offset = grownOffset;
                return;
            }

            if (min >= _offset && max < _offset + _counts.Length)
            {
                return;
            }

            int newOffset = min - ((_counts.Length - needed) / 2);
            int from = _minIndex - _offset;
            int to = _minIndex - newOffset;
            Array.Copy(_counts, from, _counts, to, live);

            // Zero whatever the live window vacated, so untouched buckets keep reading as 0.
            int clearLength = Math.Min(Math.Abs(to - from), live);
            int clearStart = to > from ? from : Math.Max(to + live, from);
            Array.Clear(_counts, clearStart, clearLength);

            _offset = newOffset;
        }
    }
}
