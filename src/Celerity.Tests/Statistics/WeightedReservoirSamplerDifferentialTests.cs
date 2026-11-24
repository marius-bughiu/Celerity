using Celerity.Statistics;
using CsCheck;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Property coverage for <see cref="WeightedReservoirSampler{T, TRng}"/> against an exact model of
/// what any weighted sample of the stream must satisfy, and against the sampler's own two entry
/// points.
/// </summary>
/// <remarks>
/// <para>
/// A random sample has no single right answer, so the oracle checks what every right answer has in
/// common: the counters equal the model's, the retained items are distinct offered items of
/// positive weight, the sample size is <c>min(k, positives)</c>, and while the positives fit the
/// sample is exactly them. The distribution itself is checked against exact successive-sampling
/// probabilities in <see cref="WeightedReservoirSamplerTests"/>.
/// </para>
/// <para>
/// The second oracle is the type against itself: the batch overload must consume the generator and
/// round the running sums exactly as per-item calls do, so for one seed the two produce the same
/// sample in the same order and a bit-identical <see cref="WeightedReservoirSampler{T, TRng}.TotalWeight"/>.
/// </para>
/// <para>
/// Weights are drawn with frequent zeros and with magnitudes from the subnormal range to near
/// <see cref="double.MaxValue"/>, so keys and jumps are exercised at every scale the log-space
/// representation has to survive.
/// </para>
/// </remarks>
public class WeightedReservoirSamplerDifferentialTests
{
    private static readonly Gen<double> Weight = Gen.OneOf(
        Gen.Const(0d),
        Gen.Const(1d),
        Gen.Double[0d, 10d],
        Gen.Int[-320, 300].Select(e => Math.Pow(10d, e)));

    private static readonly Gen<(int Capacity, double[] Weights, ulong Seed, int Split)> Case =
        Gen.Select(
            Gen.Int[1, 24],
            Weight.Array[0, 400],
            Gen.ULong,
            Gen.Int[0, 400],
            (capacity, weights, seed, split) => (capacity, weights, seed, Math.Min(split, weights.Length)));

    [Fact]
    public void Add_ShouldSatisfyTheSamplingInvariants_ForAnyStream()
    {
        Case.Sample(
            c =>
            {
                var sampler = new WeightedReservoirSampler<int>(c.Capacity, c.Seed);
                for (int i = 0; i < c.Weights.Length; i++)
                {
                    sampler.Add(i, c.Weights[i]);
                }

                AssertInvariants(sampler, c.Capacity, c.Weights, Enumerable.Range(0, c.Weights.Length));
            },
            iter: 300);
    }

    [Fact]
    public void AddSpan_ShouldMatchPerItemAdds_ForAnyStreamAndSplit()
    {
        Case.Sample(
            c =>
            {
                int[] items = Enumerable.Range(0, c.Weights.Length).ToArray();
                var perItem = new WeightedReservoirSampler<int>(c.Capacity, c.Seed);
                var batched = new WeightedReservoirSampler<int>(c.Capacity, c.Seed);

                for (int i = 0; i < items.Length; i++)
                {
                    perItem.Add(items[i], c.Weights[i]);
                }

                batched.Add(items.AsSpan(0, c.Split), c.Weights.AsSpan(0, c.Split));
                batched.Add(items.AsSpan(c.Split), c.Weights.AsSpan(c.Split));

                Assert.Equal(perItem.Sample.ToArray(), batched.Sample.ToArray());
                Assert.Equal(perItem.TotalSeen, batched.TotalSeen);
                Assert.Equal(
                    BitConverter.DoubleToInt64Bits(perItem.TotalWeight),
                    BitConverter.DoubleToInt64Bits(batched.TotalWeight));
            },
            iter: 300);
    }

    [Fact]
    public void Merge_ShouldSatisfyTheSamplingInvariants_OverTheUnion_AndKeepSampling()
    {
        Case.Sample(
            c =>
            {
                var left = new WeightedReservoirSampler<int>(c.Capacity, c.Seed);
                var right = new WeightedReservoirSampler<int>(c.Capacity, ~c.Seed);

                // The two shards take the two halves of the stream; the merged sampler then takes
                // the stream a second time under fresh ids, so the post-merge jump is exercised too.
                for (int i = 0; i < c.Weights.Length; i++)
                {
                    (i < c.Split ? left : right).Add(i, c.Weights[i]);
                }

                left.Merge(right);
                AssertInvariants(left, c.Capacity, c.Weights, Enumerable.Range(0, c.Weights.Length));

                for (int i = 0; i < c.Weights.Length; i++)
                {
                    left.Add(c.Weights.Length + i, c.Weights[i]);
                }

                double[] twice = [.. c.Weights, .. c.Weights];
                AssertInvariants(left, c.Capacity, twice, Enumerable.Range(0, twice.Length));
            },
            iter: 300);
    }

    private static void AssertInvariants(
        WeightedReservoirSampler<int> sampler,
        int capacity,
        double[] weights,
        IEnumerable<int> ids)
    {
        int[] positives = ids.Where(i => weights[i] > 0d).ToArray();
        int[] retained = sampler.Sample.ToArray();

        Assert.Equal(weights.Length, sampler.TotalSeen);
        Assert.Equal(Math.Min(capacity, positives.Length), sampler.Count);
        Assert.Equal(positives.Length >= capacity, sampler.IsFull);
        Assert.Equal(retained.Length, retained.Distinct().Count());
        Assert.All(retained, item => Assert.True(weights[item] > 0d, $"Item {item} has weight {weights[item]}."));

        if (positives.Length <= capacity)
        {
            Assert.Equal(positives, retained.Order());
        }
    }
}
