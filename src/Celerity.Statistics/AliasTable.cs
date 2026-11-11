using System.Buffers;
using System.Runtime.CompilerServices;
using Celerity.Primitives;

namespace Celerity.Statistics;

/// <summary>
/// A table for <strong>weighted random sampling</strong> from a fixed discrete distribution: built once from
/// <c>n</c> non-negative weights in <c>O(n)</c>, after which every draw returns outcome <c>i</c> with
/// probability <c>weights[i] / Σ weights</c> in <c>O(1)</c> — one 64-bit random value, one multiply and one
/// table load, however large <c>n</c> is.
/// </summary>
/// <remarks>
/// <para>
/// .NET has no weighted sampler: <see cref="Random.GetItems{T}(ReadOnlySpan{T}, int)"/> and
/// <see cref="Random.Shuffle{T}(Span{T})"/> are uniform only. The idiom written in its place is a prefix-sum
/// array searched with <see cref="Array.BinarySearch{T}(T[], T)"/>, which costs <c>O(log n)</c> dependent loads
/// per draw. Once that array falls out of cache, every probe is a miss. This is Vose's form of Walker's
/// <strong>alias method</strong>: each of <c>n</c> equal-probability buckets holds one outcome's own share and
/// names a single alias that owns the rest of the bucket, so a draw picks a bucket and then decides between
/// two outcomes.
/// </para>
/// <para>
/// <strong>Precision.</strong> The build runs in exact integer arithmetic, so it accumulates no
/// floating-point drift. Each outcome's share is rounded down to a multiple of <c>2^-32 / n</c>. The
/// outcome with the largest weight absorbs the rounding remainder, so the represented probabilities sum to
/// exactly 1. In practice every outcome's probability is within about <c>2^-32</c> of
/// <c>weights[i] / Σ weights</c>. The one visible consequence is that a positive weight whose share is below
/// <c>2^-32 / n</c> rounds to zero and is never drawn. A zero weight is <em>guaranteed</em> never to be drawn.
/// A draw consumes one full 64-bit value. Its high half picks the bucket and its low half makes the
/// in-bucket decision, so neither step needs rejection sampling, and the bias from mapping 2^64 values onto
/// <c>n</c> buckets is below <c>n / 2^64</c>.
/// </para>
/// <para>
/// <strong>Memory.</strong> 8 bytes per outcome: a 32-bit threshold and a 32-bit alias are packed into one
/// word, so a draw touches one cache line. The build borrows a 4-byte-per-outcome worklist from
/// <see cref="ArrayPool{T}.Shared"/> and returns it before the constructor finishes.
/// </para>
/// <para>
/// <strong>Thread safety.</strong> The table is immutable once constructed, so any number of threads may
/// sample from it at once, as long as each one passes its own generator. The generator is a parameter rather
/// than a field for exactly that reason. Like the struct PRNGs it takes, it is passed by <c>ref</c> and
/// advanced in place.
/// </para>
/// <para>
/// The weights are fixed at construction. A distribution whose weights change between draws needs an
/// <c>O(log n)</c> structure (a Fenwick-tree walk), and rebuilding this table is <c>O(n)</c>.
/// </para>
/// </remarks>
public sealed class AliasTable
{
    // One whole bucket in the build's fixed-point unit. A bucket's share is held as an integer in
    // [0, One], so the n buckets together hold exactly n * One, which fits a ulong for any span length.
    private const ulong One = 1UL << 32;

    // The entry written for a bucket that its own outcome fills completely. The threshold is the largest
    // 32-bit value and the alias is the outcome itself, so both arms of the in-bucket decision agree.
    private const ulong FullThreshold = 0xFFFF_FFFF_0000_0000UL;

    // Below this, a maximum weight is subnormal and its reciprocal overflows to infinity.
    private const double MinNormal = 2.2250738585072014e-308;

    // Lifts a subnormal weight set into the normal range before normalizing. A subnormal times 1e300 is at
    // most about 2.2e-8, far from overflow.
    private const double Lift = 1e300;

    // Entry i: the high 32 bits are the threshold below which a draw landing in bucket i returns i, and the
    // low 32 bits are the alias returned otherwise.
    private readonly ulong[] _table;

    /// <summary>
    /// Builds the table for the distribution that draws index <c>i</c> with probability proportional to
    /// <c>weights[i]</c>.
    /// </summary>
    /// <param name="weights">
    /// The relative weight of each outcome. The weights need not sum to 1, since only their ratios matter, and
    /// any finite non-negative values are accepted, including values near <see cref="double.MaxValue"/> whose
    /// sum would overflow. The span is read once and not retained.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="weights"/> is empty; a weight is negative, <see cref="double.NaN"/> or infinite; or
    /// every weight is zero.
    /// </exception>
    public AliasTable(ReadOnlySpan<double> weights)
    {
        int n = weights.Length;
        if (n == 0)
            throw new ArgumentException("At least one weight is required.", nameof(weights));

        // Validate, and find the largest weight. Dividing by it keeps every scaled weight in [0, 1], so the
        // sum below cannot overflow however large the inputs are.
        double maxWeight = 0d;
        int maxIndex = 0;
        for (int i = 0; i < n; i++)
        {
            double w = weights[i];
            if (!(w >= 0d) || double.IsPositiveInfinity(w))
            {
                throw new ArgumentException(
                    $"Every weight must be finite and non-negative; weights[{i}] is {w}.",
                    nameof(weights));
            }

            if (w > maxWeight)
            {
                maxWeight = w;
                maxIndex = i;
            }
        }

        if (maxWeight == 0d)
            throw new ArgumentException("At least one weight must be positive.", nameof(weights));

        // Normalize by one multiplication per weight rather than a division. The reciprocal of a subnormal
        // maximum overflows, so a subnormal set is first lifted into the normal range. That lift is a rounding
        // step of relative size 2^-53, as harmless as the multiplication itself.
        double lift = maxWeight < MinNormal ? Lift : 1d;
        double inverseMax = 1d / (maxWeight * lift);

        // Neumaier-compensated sum of the normalized weights. Its relative error does not grow with n, which
        // keeps the rounding remainder handed to the largest weight far smaller than that weight's share.
        double sum = 0d;
        double compensation = 0d;
        for (int i = 0; i < n; i++)
        {
            double x = weights[i] * lift * inverseMax;
            double t = sum + x;
            compensation += sum >= x ? (sum - t) + x : (x - t) + sum;
            sum = t;
        }

        sum += compensation;

        ulong total = (ulong)n << 32;

        // Applied to the normalized weight, never folded into inverseMax: for a small maximum, inverseMax is
        // close to double.MaxValue and its product with this scale overflows. The normalized weight is in
        // [0, 1] and the scale at most n * 2^32, so their product cannot.
        double scale = total / sum;

        // Each outcome's share in fixed point, rounded down. The remainder (at most about n units either way)
        // goes to the largest weight so the shares sum to exactly n * One. That share is at least about One, and the
        // remainder's magnitude is below One, so the adjusted share cannot go negative.
        var table = new ulong[n];
        ulong assigned = 0;
        for (int i = 0; i < n; i++)
        {
            // A share is below 2^63 (n < 2^31 buckets of 2^32), so the signed conversion is exact and it is
            // the cheaper instruction on x64.
            ulong share = (ulong)(long)(weights[i] * lift * inverseMax * scale);
            table[i] = share;
            assigned += share;
        }

        table[maxIndex] = unchecked(table[maxIndex] + (total - assigned));

        // Vose's pairing, on one worklist. Under-full outcomes stack up from the front and over-full ones down
        // from the back. Every outcome is in exactly one of the two stacks, so they never meet. The shares live
        // in the table itself until an outcome is finalized and its slot is overwritten with the packed entry.
        int[] work = ArrayPool<int>.Shared.Rent(n);
        try
        {
            int smallTop = 0;
            int largeBottom = n;
            for (int i = 0; i < n; i++)
            {
                if (table[i] < One)
                    work[smallTop++] = i;
                else
                    work[--largeBottom] = i;
            }

            // The unfinalized shares always sum to exactly One per unfinalized outcome. So while any outcome
            // is under-full, another must be over-full, and the large stack is never empty here.
            while (smallTop > 0)
            {
                int small = work[--smallTop];
                int large = work[largeBottom++];

                ulong smallShare = table[small];
                table[small] = (smallShare << 32) | (uint)large;

                ulong largeShare = table[large] + smallShare - One;
                table[large] = largeShare;
                if (largeShare < One)
                    work[smallTop++] = large;
                else
                    work[--largeBottom] = large;
            }

            // By the same invariant, every outcome left over holds exactly One: its bucket is its own.
            for (int k = largeBottom; k < n; k++)
            {
                int i = work[k];
                table[i] = FullThreshold | (uint)i;
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(work);
        }

        _table = table;
    }

    /// <summary>Gets the number of outcomes: the length of the weight span the table was built from.</summary>
    public int Count => _table.Length;

    /// <summary>
    /// Draws one outcome: an index in <c>[0, <see cref="Count"/>)</c>, returned with probability proportional
    /// to its weight. <c>O(1)</c>, and it allocates nothing.
    /// </summary>
    /// <typeparam name="TRng">The generator type. A struct, so the JIT devirtualizes and inlines it.</typeparam>
    /// <param name="rng">The generator, advanced in place by exactly one <see cref="IRandomSource.NextUInt64"/>.</param>
    /// <returns>The index of the drawn outcome.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Sample<TRng>(ref TRng rng)
        where TRng : struct, IRandomSource
        => Draw(_table, rng.NextUInt64());

    /// <summary>
    /// Fills <paramref name="destination"/> with independent draws, each an index in
    /// <c>[0, <see cref="Count"/>)</c> returned with probability proportional to its weight. Allocates nothing.
    /// </summary>
    /// <typeparam name="TRng">The generator type. A struct, so the JIT devirtualizes and inlines it.</typeparam>
    /// <param name="rng">The generator, advanced in place by one <see cref="IRandomSource.NextUInt64"/> per element.</param>
    /// <param name="destination">The span to fill. An empty span draws nothing and leaves the generator untouched.</param>
    public void Sample<TRng>(ref TRng rng, Span<int> destination)
        where TRng : struct, IRandomSource
    {
        ulong[] table = _table;
        for (int i = 0; i < destination.Length; i++)
            destination[i] = Draw(table, rng.NextUInt64());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Draw(ulong[] table, ulong random)
    {
        // The high word of random * n is a bucket in [0, n). The low word is the fractional position within
        // that bucket, and it is uniform given the bucket, so its top 32 bits make the in-bucket decision
        // without a second draw.
        ulong bucket = Math.BigMul(random, (ulong)table.Length, out ulong fraction);
        ulong entry = table[(int)bucket];
        return (uint)(fraction >> 32) < (uint)(entry >> 32) ? (int)bucket : (int)(uint)entry;
    }
}
