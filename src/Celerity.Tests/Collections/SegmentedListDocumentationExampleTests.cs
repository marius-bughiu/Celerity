using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="SegmentedList{T}"/> usage example published in <c>docs/api/collections.md</c> and the
/// README, and asserts what its comments claim.
///
/// <para>
/// A published example is documentation a reader will copy, so a wrong comment in it is a defect. The claims
/// most worth pinning are the geometry ones — the segment length for <see cref="int"/>, how many segments a
/// given count spans, and the capacity left after <see cref="SegmentedList{T}.TrimExcess"/> — because they
/// follow from the 64 KB budget and the growth policy, and a change to either moves them silently.
/// </para>
/// </summary>
public class SegmentedListDocumentationExampleTests
{
    [Fact]
    public void UsageExample_ShouldBehaveAsThePublishedCommentsClaim_WhenRunEndToEnd()
    {
        // ---- the published example: keep in sync with docs/api/collections.md and the README ----------
        //
        // var log = new SegmentedList<int>();
        // for (int i = 0; i < 100_000; i++)
        //     log.Add(i);                          // no element is ever copied once its segment is full
        //
        // log.AddRange(new[] { 100_000, 100_001 });
        //
        // Console.WriteLine(log.Count);            // 100002
        // Console.WriteLine(log.SegmentLength);    // 16384 — 64 KB of ints, under the LOH threshold
        // Console.WriteLine(log.SegmentCount);     // 7
        // Console.WriteLine(log[54_321]);          // 54321
        //
        // // Walk the storage a segment at a time, at array speed.
        // long sum = 0;
        // for (int s = 0; s < log.SegmentCount; s++)
        //     foreach (int x in log.GetSegment(s))
        //         sum += x;
        // Console.WriteLine(sum);                  // 5000150001
        //
        // log.Insert(0, -1);                       // O(n), shifting across every segment
        // log.RemoveAt(1);                         // removes the 0 that the insert pushed to position 1
        // Console.WriteLine(log[0]);               // -1
        // Console.WriteLine(log[1]);               // 1
        //
        // while (log.Count > 20_000)
        //     log.RemoveAt(log.Count - 1);         // removing the last element is O(1)
        // Console.WriteLine(log.Capacity);         // 114688 — emptied segments are kept for reuse
        // log.TrimExcess();
        // Console.WriteLine(log.Capacity);         // 32768 — two segments in use, the rest released
        //
        // ---------------------------------------------------------------------------------------------------

        var log = new SegmentedList<int>();
        for (int i = 0; i < 100_000; i++)
            log.Add(i);

        log.AddRange(new[] { 100_000, 100_001 });

        Assert.Equal(100_002, log.Count);
        Assert.Equal(16_384, log.SegmentLength);
        Assert.Equal(7, log.SegmentCount);
        Assert.Equal(54_321, log[54_321]);

        long sum = 0;
        for (int s = 0; s < log.SegmentCount; s++)
            foreach (int x in log.GetSegment(s))
                sum += x;
        Assert.Equal(5_000_150_001L, sum);

        log.Insert(0, -1);
        log.RemoveAt(1);
        Assert.Equal(-1, log[0]);
        Assert.Equal(1, log[1]);
        Assert.Equal(100_002, log.Count);

        while (log.Count > 20_000)
            log.RemoveAt(log.Count - 1);
        Assert.Equal(114_688, log.Capacity);
        log.TrimExcess();
        Assert.Equal(32_768, log.Capacity);

        Assert.Equal(20_000, log.Count);
        Assert.Equal(2, log.SegmentCount);
        Assert.Equal(19_999, log[^1]);
    }
}
