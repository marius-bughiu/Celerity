using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="RTree{TValue}"/> usage examples published in <c>docs/api/collections.md</c> and the
/// README, and asserts the outputs those pages print in their comments.
///
/// <para>
/// This exists because one of them was wrong. The map-viewport example claimed three matches where the query
/// selects two — the London and Covent Garden boxes end below the query's bottom edge — and it was wrong for
/// the ordinary reason that its expected values were worked out by hand from four boxes with real-looking
/// coordinates. A published example is documentation a reader will copy, so an incorrect one is a defect
/// rather than a typo, and hand-checking is exactly the method that produced it.
/// </para>
///
/// <para>
/// The boxes and queries below are copied verbatim from those pages. Changing an example means changing this
/// file, which is the point: the assertions are what stop the two drifting apart.
/// </para>
/// </summary>
public class RTreeDocumentationExampleTests
{
    // Verbatim from docs/api/collections.md and README.md. A country-sized box alongside street-sized ones is
    // the shape the type is for, which is why the example is built this way rather than from tidy unit squares.
    private static RTree<string> Features() => new(
    [
        new SpatialBox<string>(-8.6, 49.9, 1.8, 60.9, "United Kingdom"),
        new SpatialBox<string>(-0.51, 51.28, 0.33, 51.69, "Greater London"),
        new SpatialBox<string>(-0.14, 51.50, -0.12, 51.51, "Covent Garden"),
        new SpatialBox<string>(-3.20, 55.94, -3.18, 55.96, "Edinburgh Old Town"),
    ]);

    [Fact]
    public void GetAtPoint_ShouldReportEveryEnclosingExtent_AsTheDocumentedExampleClaims()
    {
        RTree<string> features = Features();

        string[] covering = [.. features.GetAtPoint(-0.13, 51.51).Select(f => f.Value!).OrderBy(v => v)];

        Assert.Equal(["Covent Garden", "Greater London", "United Kingdom"], covering);
    }

    [Fact]
    public void CountOverlapping_ShouldReportThree_AsTheDocumentedViewportExampleClaims()
    {
        RTree<string> features = Features();

        Assert.Equal(3, features.CountOverlapping(-1.0, 51.0, 0.5, 52.0));
    }

    [Fact]
    public void ContainsAtPoint_ShouldReportTrue_AsTheDocumentedHitTestExampleClaims()
    {
        RTree<string> features = Features();

        Assert.True(features.ContainsAtPoint(-3.19, 55.95));
    }

    [Fact]
    public void CopyOverlapping_ShouldReportTwo_AsTheDocumentedBufferExampleClaims()
    {
        RTree<string> features = Features();
        var visible = new SpatialBox<string>[8];

        int shown = features.CopyOverlapping(-4.0, 55.0, 2.0, 61.0, visible);

        // Two, not three: Greater London tops out at y = 51.69 and Covent Garden at 51.51, both below the
        // query's bottom edge of 55.0. This is the assertion the published example had wrong.
        Assert.Equal(2, shown);
        Assert.Equal(["Edinburgh Old Town", "United Kingdom"], visible.Take(shown).Select(f => f.Value!).OrderBy(v => v));
    }

    [Fact]
    public void TryGetBounds_ShouldReportTheDocumentedRootExtent()
    {
        RTree<string> features = Features();

        Assert.True(features.TryGetBounds(out double minX, out double minY, out double maxX, out double maxY));
        Assert.Equal(-8.6, minX);
        Assert.Equal(49.9, minY);
        Assert.Equal(1.8, maxX);
        Assert.Equal(60.9, maxY);
    }
}
