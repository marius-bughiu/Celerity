using Celerity.Primitives;
using Celerity.Statistics;
using CsCheck;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Differential coverage for <see cref="AliasTable"/> against the distribution it is built from. The oracle
/// is the weights themselves, read through an evenly spaced sweep of generator outputs, so an outcome's
/// observed frequency is its exact probability up to the sweep's spacing (see
/// <see cref="AliasTableOracle"/>). The second oracle is the prefix-sum search a caller would write instead:
/// both draw from the same distribution, so over the same sweep their frequencies agree to within the same
/// spacing.
/// </summary>
/// <remarks>
/// Weight vectors are drawn with frequent zeros, frequent ties, and magnitudes spread over many orders, which
/// covers both Vose stacks, an outcome that moves between them, and the rounding remainder landing on a tied
/// maximum.
/// </remarks>
public class AliasTableDifferentialTests
{
    private static readonly Gen<double> Weight = Gen.OneOf(
        Gen.Const(0d),
        Gen.Const(1d),
        Gen.Int[1, 5].Select(i => (double)i),
        Gen.Double[0d, 1d],
        Gen.Int[-30, 30].Select(e => Math.Pow(10d, e)));

    private static readonly Gen<double[]> Weights =
        Weight.Array[1, 48].Where(w => w.Any(x => x > 0d));

    [Fact]
    public void Sample_ShouldMatchTheWeights_ForAnyWeightVector()
    {
        Weights.Sample(AliasTableOracle.AssertSweepMatchesWeights, iter: 200);
    }

    [Fact]
    public void Sample_ShouldAgreeWithAPrefixSumSearch_ForAnyWeightVector()
    {
        Weights.Sample(
            weights =>
            {
                const int Draws = 1 << 16;
                var table = new AliasTable(weights);

                var cumulative = new double[weights.Length];
                double running = 0d;
                double max = weights.Max();
                for (int i = 0; i < weights.Length; i++)
                {
                    running += weights[i] / max;
                    cumulative[i] = running;
                }

                var aliasCounts = new int[weights.Length];
                var searchCounts = new int[weights.Length];
                for (int k = 0; k < Draws; k++)
                {
                    // Same evenly spaced point for both: the alias table reads it as 64 bits, the search as a
                    // double in [0, 1).
                    ulong x = (ulong)k << 48;
                    var rng = new FixedRandom(x);
                    aliasCounts[table.Sample(ref rng)]++;

                    double u = (x >> 11) * (1.0 / (1UL << 53)) * running;
                    int index = Array.BinarySearch(cumulative, u);
                    index = index < 0 ? ~index : index + 1;
                    searchCounts[Math.Min(index, weights.Length - 1)]++;
                }

                double tolerance = (2d * weights.Length + 2d) / Draws;
                for (int i = 0; i < weights.Length; i++)
                {
                    Assert.InRange(
                        (double)aliasCounts[i] / Draws,
                        ((double)searchCounts[i] / Draws) - tolerance,
                        ((double)searchCounts[i] / Draws) + tolerance);
                }
            },
            iter: 100);
    }

    private readonly struct FixedRandom(ulong value) : IRandomSource
    {
        public ulong NextUInt64() => value;
    }
}
