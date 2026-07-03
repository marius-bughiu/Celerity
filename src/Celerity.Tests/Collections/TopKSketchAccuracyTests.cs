using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Statistical / guarantee coverage for <see cref="TopKSketch{T, THasher}"/>. These pin the
/// Space-Saving theorems that hold for <em>every</em> input regardless of eviction tie-breaking:
/// no underestimate, the <c>[count − error, count]</c> frequency interval, the
/// heavy-hitter-never-missed property, and exactness when the capacity is not exceeded.
/// </summary>
public class TopKSketchAccuracyTests
{
    private static Dictionary<int, int> TrueFrequencies(int[] stream)
    {
        var freq = new Dictionary<int, int>();
        foreach (int x in stream)
            freq[x] = freq.GetValueOrDefault(x) + 1;
        return freq;
    }

    [Fact]
    public void NoEviction_CountsAreExact_AndTopKMatchesTrueRanking()
    {
        // Capacity comfortably exceeds the distinct count: the sketch is an exact frequency
        // table, so it must agree with a Dictionary count-and-sort element for element.
        var rand = new Random(7);
        int[] stream = new int[10_000];
        for (int i = 0; i < stream.Length; i++)
            stream[i] = rand.Next(0, 50);   // 50 distinct

        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(capacity: 128);
        foreach (int x in stream)
            sketch.Add(x);

        Dictionary<int, int> truth = TrueFrequencies(stream);

        Assert.Equal(truth.Count, sketch.Count);
        Assert.Equal(stream.Length, sketch.TotalCount);

        foreach (var (key, count) in truth)
        {
            Assert.True(sketch.TryGetCount(key, out long estimate, out long error));
            Assert.Equal(count, estimate);   // exact, no eviction ever happened
            Assert.Equal(0, error);
        }

        // The full ranking must match the true ranking by count.
        TopKEntry<int>[] top = sketch.GetTopK();
        Assert.Equal(truth.Count, top.Length);
        List<int> expectedTop = truth.OrderByDescending(kv => kv.Value).Take(5).Select(kv => kv.Value).ToList();
        List<int> actualTop = top.Take(5).Select(e => (int)e.Count).ToList();
        Assert.Equal(expectedTop, actualTop);
    }

    [Fact]
    public void EveryMonitor_SatisfiesTheFrequencyIntervalAndNeverUnderestimates()
    {
        // A high-cardinality stream that forces heavy eviction: 20 monitors over a domain far
        // larger than the capacity. The hard Space-Saving guarantees must hold for every survivor.
        var rand = new Random(99);
        int[] stream = new int[50_000];
        for (int i = 0; i < stream.Length; i++)
        {
            // Zipf-ish: bias toward small values so some genuine heavy hitters exist.
            int r = rand.Next(0, 1000);
            stream[i] = (r < 700) ? rand.Next(0, 30) : rand.Next(30, 5000);
        }

        var sketch = new TopKSketch<int, Int32WangHasher>(capacity: 20);
        foreach (int x in stream)
            sketch.Add(x);

        Dictionary<int, int> truth = TrueFrequencies(stream);
        Assert.Equal(stream.Length, sketch.TotalCount);
        Assert.True(sketch.Count <= 20);

        foreach (TopKEntry<int> e in sketch.GetTopK())
        {
            int trueCount = truth[e.Element];
            // Never underestimates.
            Assert.True(e.Count >= trueCount, $"underestimate: {e.Element} est {e.Count} < true {trueCount}");
            // True frequency lies within [count - error, count].
            Assert.True(e.Count - e.Error <= trueCount,
                $"interval violated: {e.Element} count {e.Count} error {e.Error} true {trueCount}");
        }
    }

    [Fact]
    public void GenuineHeavyHitters_AreNeverMissed()
    {
        // Any element with true frequency > TotalCount / Capacity is guaranteed to be monitored.
        // Build a stream where a handful of elements dominate a sea of noise.
        var stream = new List<int>();
        int[] hot = { 1001, 1002, 1003 };
        foreach (int h in hot)
            for (int i = 0; i < 1000; i++)
                stream.Add(h);                 // 3 * 1000 = 3000 hot occurrences

        var rand = new Random(2024);
        for (int i = 0; i < 1000; i++)
            stream.Add(rand.Next(0, 900));     // 1000 low-frequency noise occurrences

        int[] shuffled = stream.OrderBy(_ => rand.Next()).ToArray();

        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(capacity: 20);
        foreach (int x in shuffled)
            sketch.Add(x);

        // N / k = 4000 / 20 = 200; each hot element (1000) is well above the threshold.
        foreach (int h in hot)
        {
            Assert.True(sketch.TryGetCount(h, out long count, out _), $"heavy hitter {h} was evicted");
            Assert.True(count >= 1000);
        }

        // And they are the top 3 by count.
        List<int> top3 = sketch.GetTopK(3).Select(e => e.Element).ToList();
        Assert.Equal(hot.OrderBy(x => x), top3.OrderBy(x => x));
    }

    [Fact]
    public void Top1_IsTheTrueMostFrequent_WhenItDominates()
    {
        var rand = new Random(555);
        int[] stream = new int[20_000];
        for (int i = 0; i < stream.Length; i++)
            stream[i] = (i % 3 == 0) ? 777 : rand.Next(0, 4000);  // 777 is ~1/3 of the stream

        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(capacity: 32);
        foreach (int x in stream)
            sketch.Add(x);

        TopKEntry<int> top1 = sketch.GetTopK(1)[0];
        Assert.Equal(777, top1.Element);
    }

    [Fact]
    public void MonitorCounts_AreSortedDescending_UnderHeavyEviction()
    {
        var rand = new Random(4321);
        var sketch = new TopKSketch<int, Int32WangHasher>(capacity: 16);
        for (int i = 0; i < 30_000; i++)
            sketch.Add(rand.Next(0, 2000));

        TopKEntry<int>[] top = sketch.GetTopK();
        for (int i = 1; i < top.Length; i++)
            Assert.True(top[i - 1].Count >= top[i].Count, "GetTopK not sorted descending by count");
    }
}
