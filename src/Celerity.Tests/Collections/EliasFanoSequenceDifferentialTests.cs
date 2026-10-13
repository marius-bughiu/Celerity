using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="EliasFanoSequence"/> against the <c>long[]</c> it
/// compresses: a positional read is an array load, a lower bound is a linear scan for the first value at or
/// above the probe. CsCheck generates the input's axes — length, gap width, starting offset, and how often a
/// value repeats — so any disagreement shrinks to a minimal sequence with the seed printed.
/// </summary>
public class EliasFanoSequenceDifferentialTests
{
    // Lengths straddle the 64-bit word and 128-entry select-sample boundaries the encoding pivots on. The gap
    // sweep runs from 1 (dense: L = 0, every value its own high part) through gaps wide enough to need tens of
    // low bits, and the starting offset pushes some sequences far from zero so the quotient max / n — and with
    // it L — is set by the offset rather than by the gaps, which is the clustered shape a binary search of the
    // run has to handle. RepeatPercent turns some gaps into zero to exercise duplicates.
    private static readonly Gen<(int Length, int GapLog, long Origin, int RepeatPercent, uint Seed)> GenSequence =
        Gen.Select(Gen.Int[0, 700], Gen.Int[0, 40], Gen.Long[0, long.MaxValue / 4], Gen.Int[0, 100], Gen.UInt);

    [Fact]
    public void EveryQuery_ShouldMatchTheArrayOracle()
    {
        GenSequence.Sample(spec =>
        {
            long[] values = BuildSequence(spec.Length, spec.GapLog, spec.Origin, spec.RepeatPercent, spec.Seed);
            var sequence = new EliasFanoSequence(values);
            var rand = new Random(unchecked((int)spec.Seed) ^ 0x2545F491);

            Assert.Equal(values.Length, sequence.Count);
            for (int i = 0; i < values.Length; i++)
                Assert.Equal(values[i], sequence[i]);

            // Probe every stored value, its neighbours either side, and random points across (and beyond) the
            // range — the neighbours are where an off-by-one in the run boundaries shows.
            var probes = new List<long> { -1, 0, long.MaxValue };
            foreach (long value in values)
            {
                probes.Add(value);
                probes.Add(value - 1);
                probes.Add(value + 1);
            }

            long top = values.Length == 0 ? 1000 : values[^1];
            for (int i = 0; i < 50; i++)
                probes.Add(rand.NextInt64(0, top / 2 + 2) * 2 + rand.Next(2));

            foreach (long probe in probes)
            {
                int expected = LowerBound(values, probe);
                Assert.Equal(expected, sequence.CountLessThan(probe));

                bool present = expected < values.Length && values[expected] == probe;
                Assert.Equal(present, sequence.Contains(probe));
                Assert.Equal(present ? expected : -1, sequence.IndexOf(probe));

                Assert.Equal(expected < values.Length, sequence.TryGetLowerBound(probe, out long bound));
                Assert.Equal(expected < values.Length ? values[expected] : 0, bound);
            }
        }, iter: 300);
    }

    [Fact]
    public void Enumeration_ShouldReplayTheSequence()
    {
        GenSequence.Sample(spec =>
        {
            long[] values = BuildSequence(spec.Length, spec.GapLog, spec.Origin, spec.RepeatPercent, spec.Seed);
            var sequence = new EliasFanoSequence(values);

            Assert.Equal(values, sequence.ToArray());
        }, iter: 300);
    }

    [Fact]
    public void MemoryUsage_ShouldStayWithinTheEncodingBound()
    {
        GenSequence.Sample(spec =>
        {
            long[] values = BuildSequence(spec.Length, spec.GapLog, spec.Origin, spec.RepeatPercent, spec.Seed);
            var sequence = new EliasFanoSequence(values);

            // n * L low bits, under 3n + 1 high bits, and one long per 128 ones and per 128 zeros (the zeros
            // number under 2n + 1). Each array rounds up to a whole word, and the sample arrays hold at least
            // one entry each.
            long n = values.Length;
            long bound = n == 0
                ? 0
                : ((n * sequence.LowBitCount + 63) / 64 + (3 * n + 1 + 63) / 64 + (n / 128 + 1) + ((2 * n + 1) / 128 + 1)) * 8;
            Assert.InRange(sequence.MemoryUsageInBytes, 0, bound);
        }, iter: 300);
    }

    // A non-decreasing sequence starting at `origin`, whose gaps are drawn up to 2^gapLog, with roughly
    // `repeatPercent` of them forced to zero. Values that would overflow a long are clamped to long.MaxValue,
    // which is itself a run of repeats at the top of the range.
    private static long[] BuildSequence(int length, int gapLog, long origin, int repeatPercent, uint seed)
    {
        var rand = new Random(unchecked((int)seed));
        long[] values = new long[length];
        long current = origin;
        long gapLimit = 1L << gapLog;
        for (int i = 0; i < length; i++)
        {
            if (i > 0 && rand.Next(100) >= repeatPercent)
            {
                long gap = rand.NextInt64(1, gapLimit + 1);
                current = gap > long.MaxValue - current ? long.MaxValue : current + gap;
            }

            values[i] = current;
        }

        return values;
    }

    private static int LowerBound(long[] values, long probe)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] >= probe)
                return i;
        }

        return values.Length;
    }
}
