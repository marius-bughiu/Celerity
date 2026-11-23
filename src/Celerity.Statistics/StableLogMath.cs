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

    /// <summary>
    /// <c>(1 − e^−a) / a</c> for an <paramref name="a"/> in <c>[0, ln 2)</c>: the probability mass
    /// below a threshold divided by its first-order term, which stays in <c>(0.72, 1]</c> however
    /// small <paramref name="a"/> gets, even zero.
    /// </summary>
    /// <remarks>Kahan's form again: with <c>u = e^−a</c>, the ratio is <c>(u − 1) / ln u</c>.</remarks>
    /// <param name="a">The mass exponent. In <c>[0, ln 2)</c>.</param>
    /// <returns><c>(1 − e^−a) / a</c>, and its limit <c>1</c> at <c>a = 0</c>.</returns>
    internal static double OneMinusExpNegativeRatio(double a)
    {
        double u = Math.Exp(-a);

        if (u == 1d)
        {
            return 1d;
        }

        return (u - 1d) / Math.Log(u);
    }

    /// <summary>
    /// <c>log(1 + y) / y</c> for a <paramref name="y"/> in <c>(−1, 0]</c>, which stays in
    /// <c>[1, ∞)</c> and is <c>1</c> in the limit, so a product it scales never underflows on its
    /// account.
    /// </summary>
    /// <param name="y">The offset from 1. In <c>(−1, 0]</c>.</param>
    /// <returns><c>log(1 + y) / y</c>, and its limit <c>1</c> at <c>y = 0</c>.</returns>
    internal static double Log1PRatio(double y)
    {
        double shifted = 1d + y;

        if (shifted == 1d)
        {
            return 1d;
        }

        return Math.Log(shifted) / (shifted - 1d);
    }
}
