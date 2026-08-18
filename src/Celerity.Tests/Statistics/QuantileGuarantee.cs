namespace Celerity.Tests.Statistics;

/// <summary>
/// The one assertion every <c>DDSketch</c> accuracy test makes: that a reported quantile is
/// within a relative <c>α</c> of the true value.
/// </summary>
internal static class QuantileGuarantee
{
    /// <summary>
    /// Asserts the relative-error bound, with a few ulps of slack.
    /// </summary>
    /// <remarks>
    /// The slack is not a weakened guarantee. DDSketch's representative for a bucket is
    /// <c>(1 − α)·γ^i</c>, which is <em>exactly</em> <c>α</c> away from the top of the bucket
    /// it represents — that is what makes it the optimal choice — so a value sitting on a
    /// bucket boundary (<c>1.0</c>, for instance, which is <c>γ^0</c>) attains the bound with
    /// equality. Compared strictly, such a case tests which way <c>0.99</c> rounds in binary,
    /// not whether the sketch is accurate.
    /// </remarks>
    /// <param name="expected">The true value at the quantile.</param>
    /// <param name="actual">The value the sketch reported.</param>
    /// <param name="accuracy">The sketch's relative accuracy.</param>
    /// <param name="context">A description of the case, for the failure message.</param>
    internal static void Holds(double expected, double actual, double accuracy, string context)
    {
        double magnitude = Math.Abs(expected);
        double bound = accuracy * magnitude;

        Assert.True(
            Math.Abs(actual - expected) <= bound + (magnitude * 1e-12),
            $"{context}: expected {expected} ± {bound}, got {actual}.");
    }
}
