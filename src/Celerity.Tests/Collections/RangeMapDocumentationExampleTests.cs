using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="RangeMap{TKey, TValue}"/> walkthrough published in <c>docs/collections.md</c> and
/// asserts what its comments claim.
///
/// <para>
/// A published example is documentation a reader will copy, so a wrong one is a defect. This one once read
/// <c>owners[300];</c> and <c>owners.Count;</c> as bare statements, which C# rejects (CS0201): the walkthrough
/// did not compile.
/// </para>
/// </summary>
public class RangeMapDocumentationExampleTests
{
    [Fact]
    public void GuideExample_ShouldHoldTheRangesItsCommentsName()
    {
        // ---- the published example: keep in sync with docs/collections.md --------------------------------
        //
        // // Who owns each slice of a keyspace, edited as shards move.
        // var owners = new RangeMap<int, string>();
        // owners.Set(0, 1024, "node-a");
        // owners.Set(256, 512, "node-b");        // splits node-a's range in two
        // owners.Set(512, 768, "node-b");        // merges with the adjacent node-b range
        //
        // Console.WriteLine(owners[300]);        // node-b
        // Console.WriteLine(owners.Count);       // 3
        // owners.TryGetRange(300, out var run);  // [256, 768) = node-b
        //
        // owners.Remove(700, 800);               // trims the straddled ranges rather than dropping them
        // foreach (var range in owners.EnumerateOverlapping(200, 300))
        //     Console.WriteLine(range);          // [0, 256) = node-a, [256, 700) = node-b
        //
        // ---------------------------------------------------------------------------------------------------

        var owners = new RangeMap<int, string>();
        owners.Set(0, 1024, "node-a");

        owners.Set(256, 512, "node-b");
        Assert.Equal(["[0, 256) = node-a", "[256, 512) = node-b", "[512, 1024) = node-a"], Render(owners));

        owners.Set(512, 768, "node-b");
        Assert.Equal(["[0, 256) = node-a", "[256, 768) = node-b", "[768, 1024) = node-a"], Render(owners));

        Assert.Equal("node-b", owners[300]);
        Assert.Equal(3, owners.Count);
        Assert.True(owners.TryGetRange(300, out var run));
        Assert.Equal("[256, 768) = node-b", run.ToString());

        owners.Remove(700, 800);
        Assert.Equal(["[0, 256) = node-a", "[256, 700) = node-b", "[800, 1024) = node-a"], Render(owners));
        Assert.Equal(["[0, 256) = node-a", "[256, 700) = node-b"], Render(owners.EnumerateOverlapping(200, 300)));
    }

    private static List<string> Render(IEnumerable<Interval<int, string>> ranges) =>
        ranges.Select(range => range.ToString()).ToList();
}
