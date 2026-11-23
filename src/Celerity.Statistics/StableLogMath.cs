namespace Celerity.Statistics;

/// <summary>
/// The two cancellation-free logarithm helpers the samplers need and .NET does not ship:
/// <c>double.ExpM1</c> is <c>Math.Exp(x) - 1</c> and <c>double.LogP1</c> is
/// <c>Math.Log(x + 1)</c>, so both lose exactly the bits these exist to keep.
/// </summary>
internal static class StableLogMath
{
    /// <summary>
    /// <c>e^x − 1</c> for an <paramref name="x"/> in <c>(−0.7, 0]</c>, accurate where the
    /// subtraction alone would cancel.
    /// </summary>
    /// <remarks>
    /// This is Kahan's form: the ratio <c>(u − 1) / log(u)</c> stays well conditioned where
    /// <c>u − 1</c> does not, so scaling it by the exact <paramref name="x"/> restores the lost
    /// bits.
    /// </remarks>
    /// <param name="x">The exponent. In <c>(−0.7, 0]</c>, so <c>e^x</c> never approaches 0.</param>
    /// <returns><c>e^x − 1</c>.</returns>
    internal static double ExpM1(double x)
    {
        double u = Math.Exp(x);

        if (u == 1d)
        {
            return x;
        }

        return (u - 1d) * x / Math.Log(u);
    }

    /// <summary>
    /// <c>log(1 + x)</c>, accurate for an <paramref name="x"/> far below the precision of
    /// <c>1 + x</c>.
    /// </summary>
    /// <remarks>
    /// Kahan's correction: <c>log(u) / (u − 1)</c> is well conditioned even where <c>u − 1</c>
    /// has lost most of its bits to cancellation, so scaling it by the exact
    /// <paramref name="x"/> puts them back.
    /// </remarks>
    /// <param name="x">The offset from 1. Greater than <c>-1</c>.</param>
    /// <returns><c>log(1 + x)</c>.</returns>
    internal static double Log1P(double x)
    {
        double shifted = 1d + x;

        if (shifted == 1d)
        {
            return x;
        }

        return x * (Math.Log(shifted) / (shifted - 1d));
    }
}
