using Celerity.Statistics;
using CsCheck;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Property coverage for <see cref="RunningCovariance"/>: its three ways in — per-pair adds, the
/// blocked span overload, and merging shards split anywhere — must describe the same stream.
/// </summary>
/// <remarks>
/// <para>
/// The per-pair recurrence and the span path compute the same statistics by different arithmetic
/// (an update per pair against an exact two-pass per 256-pair block), so neither is bit-identical
/// to the other; they are held to a tolerance scaled by the magnitude of the values, which is the
/// scale their rounding error lives on.
/// </para>
/// <para>
/// Coordinates are drawn from a mix of wide-range doubles, small integers with heavy duplication
/// (so whole blocks can have no spread), and a narrow spread on a large offset (the shape the
/// running-sums shortcut gets wrong). Lengths run past two blocks so the block boundary, the
/// vector loop and the scalar tail are all reached.
/// </para>
/// </remarks>
public class RunningCovarianceDifferentialTests
{
    private static readonly Gen<double[]> Coordinates = Gen.OneOf(
        Gen.Double[-1e6, 1e6].Array[0, 600],
        Gen.Int[-3, 3].Select(i => i * 0.1d).Array[0, 600],
        Gen.Double[0d, 4d].Select(d => 1e7 + d).Array[0, 600]);

    private static readonly Gen<(double[] X, double[] Y, int Split)> Case =
        Gen.Select(Coordinates, Coordinates, Gen.Int[0, 600], (x, y, split) =>
        {
            int length = Math.Min(x.Length, y.Length);
            return (x[..length], y[..length], Math.Min(split, length));
        });

    [Fact]
    public void SpanAdd_ShouldAgreeWithPerPairAdds_ForAnyStream()
    {
        Case.Sample(
            c =>
            {
                var perPair = new RunningCovariance();
                for (int i = 0; i < c.X.Length; i++)
                {
                    perPair.Add(c.X[i], c.Y[i]);
                }

                AssertSameStream(perPair, new RunningCovariance(c.X, c.Y), c.X, c.Y);
            },
            iter: 300);
    }

    [Fact]
    public void Merge_ShouldAgreeWithOneSequentialPass_WhereverTheStreamIsSplit()
    {
        Case.Sample(
            c =>
            {
                var whole = new RunningCovariance(c.X, c.Y);

                var left = new RunningCovariance(c.X.AsSpan(0, c.Split), c.Y.AsSpan(0, c.Split));
                var right = new RunningCovariance();
                for (int i = c.Split; i < c.X.Length; i++)
                {
                    right.Add(c.X[i], c.Y[i]);
                }

                AssertSameStream(whole, RunningCovariance.Combine(left, right), c.X, c.Y);
            },
            iter: 300);
    }

    private static void AssertSameStream(
        RunningCovariance expected,
        RunningCovariance actual,
        double[] xs,
        double[] ys)
    {
        Assert.Equal(xs.Length, actual.Count);
        if (xs.Length == 0)
        {
            Assert.Equal(expected, actual);
            return;
        }

        double scaleX = xs.Max(Math.Abs) + 1d;
        double scaleY = ys.Max(Math.Abs) + 1d;
        const double Relative = 1e-10;

        Assert.Equal(expected.MeanX, actual.MeanX, scaleX * Relative);
        Assert.Equal(expected.MeanY, actual.MeanY, scaleY * Relative);
        Assert.Equal(expected.PopulationVarianceX, actual.PopulationVarianceX, scaleX * scaleX * Relative);
        Assert.Equal(expected.PopulationVarianceY, actual.PopulationVarianceY, scaleY * scaleY * Relative);
        Assert.Equal(expected.PopulationCovariance, actual.PopulationCovariance, scaleX * scaleY * Relative);

        // An exactly constant coordinate must read as exactly constant on every path, or the
        // NaN contract for slope and correlation depends on how the pairs arrived.
        Assert.Equal(xs.Min() == xs.Max(), actual.PopulationVarianceX == 0d);
        Assert.Equal(ys.Min() == ys.Max(), actual.PopulationVarianceY == 0d);

        double r = actual.Correlation;
        Assert.True(double.IsNaN(r) || (r >= -1d && r <= 1d), $"Correlation {r} is outside [-1, 1].");
    }
}
