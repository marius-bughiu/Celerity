using System.Buffers;
using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A mergeable sketch of a set that estimates its number of distinct elements and, unlike
/// <see cref="HyperLogLog{T,THasher}"/>, also the sizes of <em>intersections</em> and
/// <em>differences</em> between sets — <c>|A ∩ B|</c>, <c>|A \ B|</c> — without storing
/// either set, parameterized on a custom <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// This is a <em>theta sketch</em> (the K-Minimum-Values framework of Dasgupta, Lang, Rhodes
/// and Thaler, 2016). Every element is hashed to a 63-bit value, and the sketch keeps the
/// <see cref="NominalEntries"/> (<c>k</c>) smallest distinct values it has seen together with
/// a threshold <c>θ</c>: every value below <c>θ</c> is retained and none above it. The
/// cardinality is estimated as the retained count divided by <c>θ</c>. <c>θ</c> is set from the
/// data (as the <c>(k + 1)</c>-th smallest hash), not fixed in advance, so this is not
/// independent sampling at a fixed rate; the estimator is unbiased by the K-Minimum-Values
/// result, which the theta-sketch framework extends to merges and set expressions. While fewer than about <c>k</c> distinct elements have
/// been seen, <c>θ</c> is <c>1</c>, every element is retained and the count is exact
/// (<see cref="IsEstimationMode"/> is <c>false</c>).
/// </para>
/// <para>
/// The sample is what makes set expressions possible. Two sketches built with the same hasher
/// sample the same elements at the same hash values, so restricting both to the smaller of
/// their two thresholds gives two samples taken at one rate: the values present in both are a
/// sample of the intersection, the values present in only the first a sample of the
/// difference. <see cref="Compact"/> freezes a sketch into a <see cref="CompactThetaSketch{T,THasher}"/>
/// whose <see cref="CompactThetaSketch{T,THasher}.Intersect"/>,
/// <see cref="CompactThetaSketch{T,THasher}.Except"/> and
/// <see cref="CompactThetaSketch{T,THasher}.Union"/> combine sketches into new ones, so an
/// expression such as <c>(A ∪ B) ∩ C</c> composes. <see cref="HyperLogLog{T,THasher}"/>
/// can only union; estimating an intersection from it by inclusion–exclusion
/// (<c>|A| + |B| − |A ∪ B|</c>) subtracts two large estimates to get a small one, so its
/// error is relative to the union rather than to the answer.
/// </para>
/// <para>
/// The relative standard error of a single sketch's estimate is about
/// <c>1 / sqrt(k)</c> (<see cref="StandardError"/>): 1.6% at the default <c>k = 4096</c>.
/// An intersection or difference is estimated from the share of the sample that falls in it,
/// so its error grows as that share shrinks: an intersection holding a fraction <c>f</c> of
/// the union has a relative error of about <c>1 / sqrt(f · k)</c>.
/// <see cref="GetLowerBound"/> and <see cref="GetUpperBound"/> give an approximate confidence
/// interval that accounts for this, and <see cref="CompactThetaSketch{T,THasher}"/> exposes the
/// same pair.
/// </para>
/// <para>
/// Memory grows with the set until it reaches about <c>k</c> distinct elements and then stops:
/// the sketch holds between <c>k</c> and <c>1.5 · k</c> eight-byte hash values in an
/// open-addressed table of <c>2 · k</c> slots, 64&#160;KB at the default precision. Once the
/// sketch is in estimation mode, an element whose hash is above <c>θ</c> costs one hash and
/// one compare and is rejected without touching the table, and <c>θ</c> keeps falling as the
/// stream grows, so almost every element of a long stream takes that path.
/// </para>
/// <para>
/// Hashing follows <see cref="HyperLogLog{T,THasher}"/>: a hasher implementing
/// <see cref="IHashProvider64{T}"/> supplies all 64 bits directly, and a 32-bit-only hasher
/// has its code avalanched to 64 bits by the SplitMix64 finalizer — a bijection, so it
/// reaches 2^32 distinct hashes and two elements collide only when their 32-bit codes do.
/// Sketches are only comparable when they were built with the same <typeparamref name="THasher"/>,
/// which the type system enforces. There is no empty-slot sentinel among the elements, so
/// <c>default(T)</c> is counted like any other element; a <c>null</c> reference is mapped to a
/// fixed hash so the hasher is never invoked with <c>null</c>.
/// </para>
/// <para>
/// The sketch is add-only: it cannot remove an element or say whether a specific element was
/// added. It is not thread-safe.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the set's elements.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class ThetaSketch<T, THasher> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default precision used when a constructor does not specify one: 12, so the sketch
    /// keeps <c>k = 4096</c> hash values (at most 64&#160;KB) and has a relative standard error
    /// of about 1.6%.
    /// </summary>
    public const int DefaultPrecision = 12;

    /// <summary>The minimum supported precision (<c>k = 16</c>).</summary>
    public const int MinPrecision = 4;

    /// <summary>The maximum supported precision (<c>k = 1,048,576</c>, a 16&#160;MB table).</summary>
    public const int MaxPrecision = 20;

    // The table starts at 32 slots (256 bytes) and doubles while the sketch is still exact, so a
    // sketch of a small set stays small. With MinPrecision = 4 the full table, 2^(p + 1), is
    // never smaller than this.
    private const int InitialTableLog = 5;

    // Retained values are (hash >> 1) + 1, so they lie in [1, 2^63] and zero marks an empty slot.
    private ulong[] _table;
    private int _tableLog;
    private int _count;
    private int _growAt;
    private ulong _threshold;
    private ulong _selectState = 0x9E3779B97F4A7C15UL; // xorshift64 state for the quickselect pivot
    private readonly int _precision;
    private readonly THasher _hasher;

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="ThetaSketch{T,THasher}"/> class
    /// with the specified precision.
    /// </summary>
    /// <param name="precision">
    /// The base-2 logarithm of <see cref="NominalEntries"/>, the number of hash values the
    /// sketch keeps once it is in estimation mode. Each step up doubles the memory ceiling and
    /// divides the standard error by about 1.41. Must be between <see cref="MinPrecision"/>
    /// and <see cref="MaxPrecision"/> inclusive.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is outside the inclusive range
    /// [<see cref="MinPrecision"/>, <see cref="MaxPrecision"/>].
    /// </exception>
    public ThetaSketch(int precision = DefaultPrecision)
    {
        if (precision < MinPrecision || precision > MaxPrecision)
            throw new ArgumentOutOfRangeException(nameof(precision), precision,
                $"Precision must be between {MinPrecision} and {MaxPrecision} inclusive.");

        _precision = precision;
        _threshold = ThetaMath.FullThreshold;
        _hasher = default;
        _table = null!;
        Allocate(InitialTableLog);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThetaSketch{T,THasher}"/> class with the
    /// specified precision and populated with the elements of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The elements to add. Duplicates do not change the sketch.</param>
    /// <param name="precision">The precision; see the primary constructor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is outside the inclusive range
    /// [<see cref="MinPrecision"/>, <see cref="MaxPrecision"/>].
    /// </exception>
    public ThetaSketch(IEnumerable<T> source, int precision = DefaultPrecision)
        : this(NullCheckedPrecision(source, precision))
    {
        foreach (T item in source)
            Add(item);
    }

    // Runs as part of the chained-ctor argument expression so the null check beats the
    // primary ctor's precision validation: a null source must surface as
    // ArgumentNullException, not ArgumentOutOfRangeException, even when the caller also
    // passed an out-of-range precision.
    private static int NullCheckedPrecision(IEnumerable<T> source, int precision)
    {
        ArgumentNullException.ThrowIfNull(source);
        return precision;
    }

    /// <summary>Gets the precision the sketch was constructed with.</summary>
    public int Precision => _precision;

    /// <summary>
    /// Gets <c>k = 2^precision</c>, the number of hash values the sketch keeps when it settles
    /// into estimation mode. Between rebuilds it may hold up to half as many again.
    /// </summary>
    public int NominalEntries => 1 << _precision;

    /// <summary>
    /// Gets the number of hash values the sketch currently holds. While
    /// <see cref="IsEstimationMode"/> is <c>false</c> this is the exact number of distinct
    /// elements added (up to hash collisions).
    /// </summary>
    public int RetainedCount => _count;

    /// <summary>
    /// Gets the sampling rate <c>θ</c> in <c>(0, 1]</c>: the probability with which any one
    /// distinct element is among the retained values. It is <c>1</c> until the sketch first
    /// fills and falls as the stream grows.
    /// </summary>
    public double Theta => ThetaMath.ToTheta(_threshold);

    /// <summary>
    /// Gets a value indicating whether the sketch has started sampling — that is, whether
    /// <see cref="Theta"/> is below <c>1</c> and the cardinality is an estimate rather than an
    /// exact count.
    /// </summary>
    public bool IsEstimationMode => _threshold < ThetaMath.FullThreshold;

    /// <summary>
    /// Gets a value indicating whether the sketch summarizes the empty set: nothing has been
    /// added (or merged in) since construction or the last <see cref="Clear"/>.
    /// </summary>
    public bool IsEmpty => _count == 0 && !IsEstimationMode;

    /// <summary>
    /// Gets the <em>nominal</em> relative standard error of <see cref="EstimateCardinality"/>
    /// in estimation mode, <c>1 / sqrt(k)</c>: the error of a sketch fed its stream directly,
    /// which holds at least <c>k</c> values once it samples. It is fixed by
    /// <see cref="Precision"/> and does not track the sketch's contents, so it overstates the
    /// accuracy of a sketch that holds fewer values — one that has merged in a lower-precision
    /// sketch or a sparse set expression through <see cref="UnionWith(CompactThetaSketch{T,THasher})"/>.
    /// It is a dispersion, not a bound; <see cref="GetLowerBound"/> and
    /// <see cref="GetUpperBound"/> give an interval computed from the values actually held, and
    /// are the figures to use for merged results. In exact mode the count carries no error.
    /// </summary>
    public double StandardError => 1d / Math.Sqrt(NominalEntries);

    /// <summary>
    /// Adds an element to the sketch. Adding an element already represented, or one whose
    /// hash falls above the current threshold, does not change the sketch.
    /// </summary>
    /// <param name="item">The element to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        ulong value = (Hash64(item) >> 1) + 1;
        if (value <= _threshold)
            Insert(value);
    }

    /// <summary>
    /// Estimates the number of distinct elements added to the sketch: the retained count
    /// divided by <see cref="Theta"/>, which is exact while the sketch is not in estimation
    /// mode.
    /// </summary>
    /// <returns>The estimated number of distinct elements.</returns>
    public double EstimateCardinality() => ThetaMath.Estimate(_count, _threshold);

    /// <summary>
    /// Gets an approximate lower bound on the number of distinct elements, at the given
    /// number of standard deviations. It is never below <see cref="RetainedCount"/>, and it
    /// equals the exact count when the sketch is not in estimation mode.
    /// </summary>
    /// <param name="standardDeviations">1, 2 or 3. The interval between the lower and upper
    /// bounds at that width covers the true count with roughly 68%, 95% or 99.7% confidence.</param>
    /// <returns>The lower bound.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="standardDeviations"/> is not 1, 2 or 3.
    /// </exception>
    public double GetLowerBound(int standardDeviations) =>
        ThetaMath.LowerBound(_count, _threshold, standardDeviations);

    /// <summary>
    /// Gets an approximate upper bound on the number of distinct elements, at the given
    /// number of standard deviations. It equals the exact count when the sketch is not in
    /// estimation mode.
    /// </summary>
    /// <param name="standardDeviations">1, 2 or 3. The interval between the lower and upper
    /// bounds at that width covers the true count with roughly 68%, 95% or 99.7% confidence.</param>
    /// <returns>The upper bound.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="standardDeviations"/> is not 1, 2 or 3.
    /// </exception>
    public double GetUpperBound(int standardDeviations) =>
        ThetaMath.UpperBound(_count, _threshold, standardDeviations);

    /// <summary>
    /// Merges another sketch into this one in place, so this sketch afterwards summarizes the
    /// <em>union</em> of both sets. The two sketches may have different precisions; the result
    /// keeps this sketch's.
    /// </summary>
    /// <param name="other">The sketch to merge in. Left unmodified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The merged sketch keeps every value of either sketch below the smaller of their two
    /// thresholds, trimmed to this sketch's <see cref="NominalEntries"/>, which is the state a
    /// sketch of that precision fed the union directly could have reached; it carries no error
    /// beyond that of the coarser of the two inputs. Merging a sketch into itself does nothing.
    /// </remarks>
    public void UnionWith(ThetaSketch<T, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(other, this))
            return;

        LowerThreshold(other._threshold);
        foreach (ulong value in other._table)
        {
            if (value != 0 && value <= _threshold)
                Insert(value);
        }
    }

    /// <summary>
    /// Merges a compact sketch into this one in place, so this sketch afterwards summarizes
    /// the <em>union</em> of both sets. The compact sketch may be the result of a set
    /// expression, so an expression such as <c>(A ∩ B) ∪ C</c> can be accumulated here.
    /// </summary>
    /// <param name="other">The compact sketch to merge in.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void UnionWith(CompactThetaSketch<T, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        LowerThreshold(other.Threshold);
        foreach (ulong value in other.Values)
        {
            // The values are ascending, so the first one past the threshold ends the merge. The
            // threshold can fall during the loop (a rebuild), which only ends it sooner.
            if (value > _threshold)
                break;
            Insert(value);
        }
    }

    /// <summary>
    /// Returns an immutable, sorted snapshot of the sketch that can be combined with others by
    /// <see cref="CompactThetaSketch{T,THasher}.Union"/>,
    /// <see cref="CompactThetaSketch{T,THasher}.Intersect"/> and
    /// <see cref="CompactThetaSketch{T,THasher}.Except"/>. Later changes to this sketch do not
    /// affect it.
    /// </summary>
    /// <returns>The compact snapshot, holding <see cref="RetainedCount"/> values.</returns>
    /// <remarks>This copies and sorts the retained values: <c>O(n log n)</c> in
    /// <see cref="RetainedCount"/>.</remarks>
    public CompactThetaSketch<T, THasher> Compact()
    {
        ulong[] values = _count == 0 ? [] : new ulong[_count];
        CopyRetained(values);
        values.AsSpan().Sort();
        return new CompactThetaSketch<T, THasher>(values, _threshold);
    }

    /// <summary>
    /// Resets the sketch to empty. The precision is preserved, and so is the table the sketch
    /// has grown to, so refilling it does not allocate again.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_table);
        _count = 0;
        _threshold = ThetaMath.FullThreshold;
    }

    private void Insert(ulong value)
    {
        ulong[] table = _table;
        int mask = table.Length - 1;
        int index = Slot(value, _tableLog);
        while (true)
        {
            ulong slot = table[index];
            if (slot == value)
                return;
            if (slot == 0)
                break;
            index = (index + 1) & mask;
        }

        table[index] = value;
        if (++_count > _growAt)
            Grow();
    }

    // The table is full enough: double it while the sketch has not yet reached its 2k-slot
    // ceiling, otherwise keep the k smallest values and lower the threshold to just below the
    // (k + 1)-th. The rebuild selects over 1.5k values in expected linear time and frees room
    // for another 0.5k inserts, so it costs O(1) per retained insert amortized — and once the
    // threshold falls, retained inserts are the rare case.
    private void Grow()
    {
        if (_tableLog <= _precision)
        {
            ulong[] old = _table;
            Allocate(_tableLog + 1);
            _count = 0;
            foreach (ulong value in old)
            {
                if (value != 0)
                    InsertUnique(value);
            }
            return;
        }

        int k = NominalEntries;
        ulong[] scratch = ArrayPool<ulong>.Shared.Rent(_count);
        try
        {
            Span<ulong> values = scratch.AsSpan(0, _count);
            CopyRetained(values);
            SelectSmallest(values, k);

            // Values are distinct and at least 1, so values[k] >= k + 1 and the threshold stays
            // positive: the k smallest are exactly the values at or below it.
            _threshold = values[k] - 1;
            Array.Clear(_table);
            _count = 0;
            foreach (ulong value in values[..k])
                InsertUnique(value);
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(scratch);
        }
    }

    // Quickselect: reorders `values` so that values[k] is the (k + 1)-th smallest and every
    // value before it is smaller. The values are distinct, so Hoare partitioning always makes
    // progress. The pivot is drawn at a pseudo-random position rather than a fixed one, so no
    // arrangement of the input — sorted, reversed, organ-pipe — is a reliable worst case and the
    // expected cost stays linear. A full sort here made filling a sketch 1.3x slower than a
    // HashSet<int> at 100,000 elements.
    private void SelectSmallest(Span<ulong> values, int k)
    {
        int lo = 0, hi = values.Length - 1;
        ulong state = _selectState;
        while (lo < hi)
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            ulong pivot = values[lo + (int)(state % (ulong)(hi - lo + 1))];

            int i = lo, j = hi;
            while (i <= j)
            {
                while (values[i] < pivot)
                    i++;
                while (values[j] > pivot)
                    j--;
                if (i <= j)
                {
                    (values[i], values[j]) = (values[j], values[i]);
                    i++;
                    j--;
                }
            }

            // [lo, j] holds values below or at the pivot, [i, hi] values at or above it, and
            // anything strictly between is the pivot itself, already in its final place.
            if (k <= j)
                hi = j;
            else if (k >= i)
                lo = i;
            else
                break;
        }

        _selectState = state;
    }

    // Lowers the threshold to `threshold` when that is smaller, dropping every retained value
    // above it. The table keeps its size: the count only falls.
    private void LowerThreshold(ulong threshold)
    {
        if (threshold >= _threshold)
            return;

        _threshold = threshold;

        // Clearing slots in place would break the probe chains of the values after them, so
        // collect the survivors and re-place them in an emptied table.
        ulong[] table = _table;
        ulong[] scratch = ArrayPool<ulong>.Shared.Rent(_count);
        try
        {
            int survivors = 0;
            foreach (ulong value in table)
            {
                if (value != 0 && value <= threshold)
                    scratch[survivors++] = value;
            }

            Array.Clear(table);
            _count = 0;
            foreach (ulong value in scratch.AsSpan(0, survivors))
                InsertUnique(value);
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(scratch);
        }
    }

    // Places a value known to be absent and known to fit without growing.
    private void InsertUnique(ulong value)
    {
        ulong[] table = _table;
        int mask = table.Length - 1;
        int index = Slot(value, _tableLog);
        while (table[index] != 0)
            index = (index + 1) & mask;
        table[index] = value;
        _count++;
    }

    private void CopyRetained(Span<ulong> destination)
    {
        int n = 0;
        foreach (ulong value in _table)
        {
            if (value != 0)
                destination[n++] = value;
        }
    }

    private void Allocate(int tableLog)
    {
        _tableLog = tableLog;
        _table = new ulong[1 << tableLog];

        // Grow (or rebuild) once the table is three-quarters full, which keeps linear-probe
        // chains short. At the ceiling of 2k slots that is 1.5k values.
        _growAt = (3 << tableLog) >> 2;
    }

    // Fibonacci hashing: the retained values are all small once the threshold has fallen, so
    // take the slot from the top bits of a multiplicative mix rather than from the value's
    // low bits directly.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Slot(ulong value, int tableLog) =>
        (int)((value * 0x9E3779B97F4A7C15UL) >> (64 - tableLog));

    // Produces the 64-bit hash the sample value is derived from, exactly as HyperLogLog does:
    // an IHashProvider64<T> hasher's own result is used as-is, a 32-bit code is avalanched by
    // the SplitMix64 finalizer, and a null reference maps to a fixed hash so the hasher is
    // never invoked with null. Both guards are JIT-time constants.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong Hash64(T item)
    {
        if (!typeof(T).IsValueType && item is null)
            return ThetaMath.Mix64(0);

        if (Hash64Source<T, THasher>.IsNative64)
            return Hash64Source<T, THasher>.Hash64(item);

        return ThetaMath.Mix64((uint)_hasher.Hash(item));
    }
}
