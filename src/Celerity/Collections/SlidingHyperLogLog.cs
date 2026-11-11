using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A fixed-memory distinct-count estimator over a <strong>time window</strong>: it answers
/// "roughly how many distinct elements have I seen since <em>t</em>?" for any <em>t</em> chosen
/// at query time, from memory that never grows with the stream. Parameterized on a custom
/// <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="HyperLogLog{T, THasher}"/> counts distinct elements since it was created and
/// cannot forget, so "unique users in the last five minutes" needs either a fresh sketch per
/// window or a ring of per-bucket sketches that answers only at bucket granularity. The exact
/// answer is a dictionary of last-seen timestamps, whose memory grows with every distinct key
/// and whose every windowed count is a full scan. This type is a <em>sliding HyperLogLog</em>
/// (Chabchoub &amp; Hébrail, 2010) in its rank-indexed form: for every register, and for every
/// rank that register could hold, it remembers the <em>latest</em> timestamp at which that rank
/// was observed. A query for "since <em>t</em>" takes, per register, the largest rank whose
/// latest timestamp is at or after <em>t</em>, and feeds those ranks to the ordinary
/// HyperLogLog estimator.
/// </para>
/// <para>
/// Elements are routed to registers and ranks exactly as <see cref="HyperLogLog{T, THasher}"/>
/// routes them and the estimate is computed by the same code, so the results are interchangeable:
/// <see cref="EstimateCardinality()"/> equals, bit for bit, a <see cref="HyperLogLog{T, THasher}"/>
/// of the same precision fed the same elements, and <see cref="EstimateCardinality(long)"/> equals
/// one fed only the elements whose timestamp is at or after <c>since</c>. The relative standard
/// error is therefore the HyperLogLog one, ≈ <c>1.04 / sqrt(2^precision)</c>, for every window.
/// </para>
/// <para>
/// <strong>Timestamps are opaque <see cref="long"/> values</strong> that only need to be ordered:
/// <see cref="DateTime.Ticks"/>, Unix milliseconds, <see cref="Environment.TickCount64"/>, or an
/// event sequence number all work. Each cell keeps the maximum timestamp it has seen, so adds may
/// arrive out of order and the state is the same as if they had arrived sorted. For the same
/// reason two sketches merge exactly with <see cref="UnionWith"/> (an element-wise maximum), and
/// the merge is byte-identical across runtimes given a deterministic hasher.
/// </para>
/// <para>
/// The trade for windowing is memory: <c>2^precision · (65 − precision)</c> timestamps of 8 bytes
/// each, plus one byte per register — about 1.7&#160;MB at the default precision of 12
/// (≈1.6% error), against a plain HyperLogLog's 4&#160;KB at the same precision. That figure is
/// fixed at construction and is the same whether the window holds a thousand distinct elements
/// or a billion. Old timestamps are never purged because they never need to be: a query simply
/// ignores cells older than its window.
/// </para>
/// <para>
/// Adding an element costs one hasher call and at most two stores, and allocates nothing. A query
/// visits all <c>m</c> registers and walks each one down from its all-time rank to its in-window
/// rank: about <c>log2(all-time distinct / in-window distinct)</c> steps — a handful for a
/// five-minute window over a day of history — and never more than <c>65 − precision</c>. It
/// allocates nothing either. Like
/// <see cref="HyperLogLog{T, THasher}"/>, the sketch has no empty-slot sentinel, so
/// <c>default(T)</c> is counted like any other element, and a <c>null</c> reference is mapped to a
/// fixed hash so the hasher is never invoked with <c>null</c>.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements whose distinct count is estimated.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it. A hasher that also
/// implements <see cref="IHashProvider64{T}"/> supplies all 64 hash bits directly; see
/// <see cref="HyperLogLog{T, THasher}"/> for why that matters beyond ~10^8 distinct elements.
/// </typeparam>
public class SlidingHyperLogLog<T, THasher> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default register-index precision used when a constructor does not specify one: 12,
    /// giving <c>m = 4096</c> registers, a relative standard error of about 1.6%, and about
    /// 1.7&#160;MB of state. Lower than <see cref="HyperLogLog{T, THasher}.DefaultPrecision"/>
    /// because every register here carries a timestamp per possible rank.
    /// </summary>
    public const int DefaultPrecision = 12;

    /// <summary>The minimum supported precision (<c>m = 16</c> registers).</summary>
    public const int MinPrecision = 4;

    /// <summary>The maximum supported precision (<c>m = 65536</c> registers, about 25&#160;MB of state).</summary>
    public const int MaxPrecision = 16;

    // _lastSeen[register * _rankCount + (rank - 1)] is the latest timestamp at which `register`
    // observed `rank`, or long.MinValue when it never has. _maxRank[register] is the largest rank
    // the register has ever observed — exactly a plain HyperLogLog's register — and is where every
    // query starts walking down from.
    private readonly long[] _lastSeen;
    private readonly byte[] _maxRank;
    private readonly int _precision;     // p
    private readonly int _registerCount; // m = 2^p
    private readonly int _rankCount;     // 65 - p: ranks run 1 .. 65 - p

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="SlidingHyperLogLog{T, THasher}"/> class
    /// with the specified register-index precision.
    /// </summary>
    /// <param name="precision">
    /// The number of bits of each element's hash used to select a register. The sketch holds
    /// <c>2^precision · (65 − precision)</c> eight-byte timestamps, so each step up roughly doubles
    /// the memory and divides the relative standard error (≈ <c>1.04 / sqrt(2^precision)</c>) by
    /// √2. Must be between <see cref="MinPrecision"/> and <see cref="MaxPrecision"/> inclusive.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is outside the inclusive range
    /// [<see cref="MinPrecision"/>, <see cref="MaxPrecision"/>].
    /// </exception>
    public SlidingHyperLogLog(int precision = DefaultPrecision)
    {
        if (precision < MinPrecision || precision > MaxPrecision)
            throw new ArgumentOutOfRangeException(nameof(precision), precision,
                $"Precision must be between {MinPrecision} and {MaxPrecision} inclusive.");

        _precision = precision;
        _registerCount = 1 << precision;
        _rankCount = 65 - precision;
        _maxRank = new byte[_registerCount];
        _lastSeen = new long[_registerCount * _rankCount];
        Array.Fill(_lastSeen, long.MinValue);
    }

    /// <summary>Gets the register-index precision (<c>p</c>) the sketch was constructed with.</summary>
    public int Precision => _precision;

    /// <summary>Gets the number of registers (<c>m = 2^precision</c>).</summary>
    public int RegisterCount => _registerCount;

    /// <summary>
    /// Gets the relative standard error of every estimate, ≈ <c>1.04 / sqrt(m)</c> — the same for
    /// any window, since a windowed estimate is an ordinary HyperLogLog estimate of the in-window
    /// elements.
    /// </summary>
    public double StandardError => 1.04 / Math.Sqrt(_registerCount);

    /// <summary>
    /// Adds an element observed at <paramref name="timestamp"/>.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <param name="timestamp">
    /// When the element was observed, on whatever ordered scale the caller queries with (ticks,
    /// milliseconds, a sequence number). Any <see cref="long"/> is accepted, and timestamps need
    /// not arrive in order: adding an element at a timestamp older than one already recorded for
    /// its cell changes nothing.
    /// </param>
    public void Add(T item, long timestamp)
    {
        ulong hash = HyperLogLogHash<T, THasher>.Hash64(item);
        int register = HyperLogLogCore.RegisterOf(hash, _precision);
        int rank = HyperLogLogCore.RankOf(hash, _precision);

        ref long cell = ref _lastSeen[register * _rankCount + rank - 1];
        if (timestamp > cell)
            cell = timestamp;

        if (rank > _maxRank[register])
            _maxRank[register] = (byte)rank;
    }

    /// <summary>
    /// Estimates the number of distinct elements added at <em>any</em> timestamp since construction
    /// or the last <see cref="Clear"/> — the figure a plain <see cref="HyperLogLog{T, THasher}"/> of
    /// the same precision would report for the same elements.
    /// </summary>
    /// <returns>The estimated all-time cardinality; an empty sketch returns <c>0</c> exactly.</returns>
    public long EstimateCardinality() => EstimateCardinality(long.MinValue);

    /// <summary>
    /// Estimates the number of distinct elements added with a timestamp at or after
    /// <paramref name="since"/>.
    /// </summary>
    /// <param name="since">
    /// The start of the window, inclusive, on the caller's timestamp scale. For "the last five
    /// minutes" pass <c>now − 5 minutes</c>. Elements whose most recent timestamp is earlier are
    /// not counted. <see cref="long.MinValue"/> counts everything.
    /// </param>
    /// <returns>
    /// The estimated in-window cardinality, rounded to the nearest whole number, within about
    /// <see cref="StandardError"/>; <c>0</c> exactly when nothing was added at or after
    /// <paramref name="since"/>.
    /// </returns>
    /// <remarks>
    /// Visits all <c>m</c> registers, walking each down about
    /// <c>log2(all-time distinct / in-window distinct)</c> ranks (at most <c>65 − precision</c>). The window has no upper bound: an element added with
    /// a timestamp later than the caller's notion of "now" is counted, because only its latest
    /// timestamp is kept. Bit-identical to <see cref="HyperLogLog{T, THasher}.EstimateCardinality"/>
    /// over exactly the elements whose timestamp is at or after <paramref name="since"/>.
    /// </remarks>
    public long EstimateCardinality(long since)
    {
        long[] lastSeen = _lastSeen;
        byte[] maxRank = _maxRank;
        int rankCount = _rankCount;
        int m = _registerCount;

        double sum = 0d;
        int zeroRegisters = 0;
        for (int i = 0; i < m; i++)
        {
            // Walk down from the register's all-time rank to the largest rank seen in the window.
            // No cell is below long.MinValue, so with since == long.MinValue the walk stops at once
            // and yields the all-time rank. A never-set cell holds long.MinValue, which any later
            // `since` skips — as it does an element really added at long.MinValue.
            int r = maxRank[i];
            int rowEnd = i * rankCount - 1; // index of rank r is rowEnd + r
            while (r != 0 && lastSeen[rowEnd + r] < since)
                r--;

            sum += 1.0 / (1UL << r); // 2^-r; r <= 65 - precision < 64
            if (r == 0)
                zeroRegisters++;
        }

        return HyperLogLogCore.Estimate(sum, zeroRegisters, m, Hash64Source<T, THasher>.IsNative64);
    }

    /// <summary>
    /// Merges another sketch into this one in place, so this sketch afterwards answers every window
    /// for the <em>union</em> of both input streams. Both sketches must have been constructed with
    /// the same <see cref="Precision"/>.
    /// </summary>
    /// <param name="other">The sketch to merge in. Left unmodified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> has a different precision, so the two sketches are not compatible.
    /// </exception>
    /// <remarks>
    /// The merge takes the element-wise maximum of every timestamp, which is exactly the state the
    /// combined stream would have produced in any arrival order — so the merged sketch carries no
    /// error beyond the usual HyperLogLog estimate of the union, for every window.
    /// </remarks>
    public void UnionWith(SlidingHyperLogLog<T, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._precision != _precision)
            throw new ArgumentException("The two sketches must have the same precision to be merged.", nameof(other));

        long[] lastSeen = _lastSeen;
        long[] otherLastSeen = other._lastSeen;
        for (int i = 0; i < lastSeen.Length; i++)
        {
            if (otherLastSeen[i] > lastSeen[i])
                lastSeen[i] = otherLastSeen[i];
        }

        byte[] maxRank = _maxRank;
        byte[] otherMaxRank = other._maxRank;
        for (int i = 0; i < maxRank.Length; i++)
        {
            if (otherMaxRank[i] > maxRank[i])
                maxRank[i] = otherMaxRank[i];
        }
    }

    /// <summary>
    /// Resets the sketch to empty, forgetting every timestamp. The precision and register count are
    /// preserved.
    /// </summary>
    public void Clear()
    {
        Array.Fill(_lastSeen, long.MinValue);
        Array.Clear(_maxRank);
    }
}
