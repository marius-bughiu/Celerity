using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="SlidingHyperLogLog{T,THasher}"/> against an exact oracle:
/// a plain <see cref="HyperLogLog{T,THasher}"/> of the same precision, rebuilt for each query from only
/// the events whose timestamp is at or after <c>since</c>.
///
/// <para>
/// The two sketches route elements and compute estimates through the same code, so for every window the
/// answers must agree <em>bit for bit</em>, not within an error bound. That turns a probabilistic type
/// into one with an exact oracle, and is what makes the property sharp: a walk-down that stops one rank
/// early, a row offset off by one, a merge that maxes the wrong array, or an out-of-order add that
/// overwrites a newer timestamp all show up as a different integer rather than a slightly worse
/// estimate. The generator keeps the key domain small relative to the event count so keys repeat at
/// several timestamps, keeps precision low so registers collide, and draws timestamps in random order
/// from a narrow range so equal and out-of-order timestamps are common.
/// </para>
/// </summary>
public class SlidingHyperLogLogDifferentialTests
{
    private static readonly Gen<(int Precision, (int Key, long Time)[] Events, long[] Sinces)> GenCases =
        Gen.Select(
            Gen.Int[4, 9],
            Gen.Select(Gen.Int[0, 400], Gen.Long[-50, 50]).Array[0, 600],
            Gen.Long[-60, 60].Array[1, 8]);

    [Fact]
    public void EveryWindow_ShouldEqualAHyperLogLogOfTheInWindowEvents_UnderGeneratedStreams()
    {
        GenCases.Sample(c =>
        {
            var sliding = new SlidingHyperLogLog<int, Int32Murmur3Hasher>(c.Precision);
            foreach (var (key, time) in c.Events)
                sliding.Add(key, time);

            Assert.Equal(Oracle(c.Precision, c.Events, long.MinValue), sliding.EstimateCardinality());
            foreach (long since in c.Sinces)
                Assert.Equal(Oracle(c.Precision, c.Events, since), sliding.EstimateCardinality(since));
        }, iter: 300);
    }

    [Fact]
    public void UnionWith_ShouldEqualTheSketchOfTheCombinedStream_UnderGeneratedSplits()
    {
        Gen.Select(GenCases, Gen.UInt).Sample(t =>
        {
            var (c, splitSeed) = t;
            var left = new SlidingHyperLogLog<int, Int32Murmur3Hasher>(c.Precision);
            var right = new SlidingHyperLogLog<int, Int32Murmur3Hasher>(c.Precision);
            var rng = new Random((int)splitSeed);
            foreach (var (key, time) in c.Events)
                (rng.Next(2) == 0 ? left : right).Add(key, time);

            left.UnionWith(right);
            foreach (long since in c.Sinces.Append(long.MinValue))
                Assert.Equal(Oracle(c.Precision, c.Events, since), left.EstimateCardinality(since));
        }, iter: 200);
    }

    private static long Oracle(int precision, (int Key, long Time)[] events, long since)
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(precision);
        foreach (var (key, time) in events)
        {
            if (time >= since)
                hll.Add(key);
        }

        return hll.EstimateCardinality();
    }
}
