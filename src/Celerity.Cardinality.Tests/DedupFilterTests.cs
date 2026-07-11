namespace Celerity.Cardinality.Tests;

public class DedupFilterTests
{
    [Fact]
    public void TryMarkSeen_IsTrueFirstThenFalse()
    {
        var dedup = new StringDedupFilter(1000);
        Assert.True(dedup.TryMarkSeen("event-1"));
        Assert.False(dedup.TryMarkSeen("event-1"));
        Assert.True(dedup.TryMarkSeen("event-2"));
    }

    [Fact]
    public void Contains_HasNoFalseNegatives()
    {
        var dedup = new StringDedupFilter(1000);
        dedup.TryMarkSeen("known");
        Assert.True(dedup.Contains("known"));
        Assert.False(dedup.Contains("definitely-absent-xyz"));
    }

    [Fact]
    public void Dedup_OverAStream_KeepsOnlyFirstOccurrences()
    {
        var dedup = new StringDedupFilter(10_000);
        string[] stream = { "a", "b", "a", "c", "b", "a", "d" };

        var unique = stream.Where(dedup.TryMarkSeen).ToArray();

        Assert.Equal(new[] { "a", "b", "c", "d" }, unique);
    }

    [Fact]
    public void Remove_AllowsAKeyToBeSeenFreshAgain()
    {
        var dedup = new StringDedupFilter(1000);
        Assert.True(dedup.TryMarkSeen("k"));
        Assert.False(dedup.TryMarkSeen("k"));

        Assert.True(dedup.Remove("k"));

        Assert.True(dedup.TryMarkSeen("k")); // fresh again after aging out
    }

    [Fact]
    public void Count_TracksMarkedKeys()
    {
        var dedup = new StringDedupFilter(1000);
        dedup.TryMarkSeen("a");
        dedup.TryMarkSeen("b");
        dedup.TryMarkSeen("a"); // duplicate, not stored again by the dedup primitive
        Assert.Equal(2, dedup.Count);

        dedup.Remove("a");
        Assert.Equal(1, dedup.Count);
    }

    [Fact]
    public void SlidingWindow_KeepsFillBounded()
    {
        // Mark keys as they arrive and remove them as they age out; the live count stays near the window size
        // no matter how long the stream is.
        var dedup = new StringDedupFilter(2000);
        const int window = 1000;
        var live = new Queue<string>();

        for (int i = 0; i < 50_000; i++)
        {
            string key = $"key-{i}";
            dedup.TryMarkSeen(key);
            live.Enqueue(key);
            if (live.Count > window)
                dedup.Remove(live.Dequeue());
        }

        Assert.True(dedup.Count <= window + 1, $"live count {dedup.Count} exceeded window {window}");
        Assert.False(dedup.IsFull);
    }

    [Fact]
    public void UnionWith_CombinesTwoFilters()
    {
        var a = new StringDedupFilter(1000);
        var b = new StringDedupFilter(1000);
        a.TryMarkSeen("a-key");
        b.TryMarkSeen("b-key");

        a.UnionWith(b);

        Assert.True(a.Contains("a-key"));
        Assert.True(a.Contains("b-key"));
    }

    [Fact]
    public void Clear_ResetsTheFilter()
    {
        var dedup = new StringDedupFilter(1000);
        dedup.TryMarkSeen("x");
        dedup.Clear();
        Assert.Equal(0, dedup.Count);
        Assert.True(dedup.TryMarkSeen("x"));
    }

    [Fact]
    public void Constructor_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringDedupFilter(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringDedupFilter(1000, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringDedupFilter(1000, 1d));
    }

    [Fact]
    public void Works_WithIntegerKeys()
    {
        var dedup = new DedupFilter<long, Int64WangNaiveHasher>(1000);
        Assert.True(dedup.TryMarkSeen(42L));
        Assert.False(dedup.TryMarkSeen(42L));
    }
}
