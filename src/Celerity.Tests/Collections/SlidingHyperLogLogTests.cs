using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural tests for <see cref="SlidingHyperLogLog{T,THasher}"/>: the window semantics, the
/// out-of-band elements (<c>default(T)</c>, <c>null</c>), merging, clearing, and constructor
/// validation. Mirrors <see cref="HyperLogLogTests"/>, plus the time axis HyperLogLog does not have.
/// </summary>
public class SlidingHyperLogLogTests
{
    private static SlidingHyperLogLog<int, Int32Murmur3Hasher> NewSketch(int precision = SlidingHyperLogLog<int, Int32Murmur3Hasher>.DefaultPrecision)
        => new(precision);

    [Fact]
    public void EmptySketch_EstimatesZero_ForEveryWindow()
    {
        var s = NewSketch();
        Assert.Equal(0, s.EstimateCardinality());
        Assert.Equal(0, s.EstimateCardinality(long.MinValue));
        Assert.Equal(0, s.EstimateCardinality(0));
        Assert.Equal(0, s.EstimateCardinality(long.MaxValue));
    }

    [Fact]
    public void SingleElement_IsCounted_OnlyInWindowsThatContainIt()
    {
        var s = NewSketch();
        s.Add(42, timestamp: 100);

        Assert.Equal(1, s.EstimateCardinality());
        Assert.Equal(1, s.EstimateCardinality(since: 100)); // inclusive lower bound
        Assert.Equal(1, s.EstimateCardinality(since: 0));
        Assert.Equal(0, s.EstimateCardinality(since: 101));
    }

    [Fact]
    public void ReAddingAKey_MovesItIntoLaterWindows()
    {
        var s = NewSketch();
        s.Add(7, 10);
        Assert.Equal(0, s.EstimateCardinality(since: 20));

        s.Add(7, 30);
        Assert.Equal(1, s.EstimateCardinality(since: 20));
        Assert.Equal(1, s.EstimateCardinality()); // still one distinct element
    }

    [Fact]
    public void OutOfOrderAdds_KeepTheLatestTimestamp()
    {
        var s = NewSketch();
        s.Add(7, 30);
        s.Add(7, 10); // older: changes nothing

        Assert.Equal(1, s.EstimateCardinality(since: 20));
        Assert.Equal(0, s.EstimateCardinality(since: 31));
    }

    [Fact]
    public void ArrivalOrder_DoesNotChangeAnyEstimate()
    {
        var sorted = NewSketch(8);
        var shuffled = NewSketch(8);
        var events = Enumerable.Range(0, 5_000).Select(i => (Key: i % 1_700, Time: (long)i)).ToArray();

        foreach (var (key, time) in events)
            sorted.Add(key, time);
        foreach (var (key, time) in events.OrderBy(e => (e.Key * 7919) % 5_003))
            shuffled.Add(key, time);

        foreach (long since in new long[] { long.MinValue, 0, 1_000, 3_300, 4_999, 5_000 })
            Assert.Equal(sorted.EstimateCardinality(since), shuffled.EstimateCardinality(since));
    }

    [Fact]
    public void Window_CountsOnlyRecentDistinctKeys()
    {
        // Keys 0..9_999 at t = key, then keys 0..999 again at t = 20_000 + key.
        var s = NewSketch(14);
        for (int i = 0; i < 10_000; i++)
            s.Add(i, i);
        for (int i = 0; i < 1_000; i++)
            s.Add(i, 20_000 + i);

        AssertNear(10_000, s.EstimateCardinality(), s.StandardError);
        AssertNear(10_000, s.EstimateCardinality(0), s.StandardError);
        AssertNear(6_000, s.EstimateCardinality(5_000), s.StandardError);    // 5_000..9_999, plus 0..999 re-added later
        AssertNear(1_000, s.EstimateCardinality(10_000), s.StandardError);   // the re-added keys
        AssertNear(500, s.EstimateCardinality(20_500), s.StandardError);
        Assert.Equal(0, s.EstimateCardinality(21_000));
    }

    [Fact]
    public void EstimateCardinality_MatchesHyperLogLog_ForAllTimeAndForEveryWindow()
    {
        var events = new List<(int Key, long Time)>();
        var rng = new Random(606);
        for (int i = 0; i < 40_000; i++)
            events.Add((rng.Next(25_000), rng.NextInt64(-1_000, 1_000)));

        var sliding = NewSketch(10);
        foreach (var (key, time) in events)
            sliding.Add(key, time);

        foreach (long since in new long[] { long.MinValue, -1_000, -500, 0, 250, 999, 1_000 })
        {
            var hll = new HyperLogLog<int, Int32Murmur3Hasher>(10);
            foreach (var (key, time) in events)
            {
                if (time >= since)
                    hll.Add(key);
            }

            Assert.Equal(hll.EstimateCardinality(), sliding.EstimateCardinality(since));
        }
    }

    [Fact]
    public void ExtremeTimestamps_AreOrdinaryTimestamps()
    {
        var s = NewSketch();
        s.Add(1, long.MinValue);
        s.Add(2, long.MaxValue);

        Assert.Equal(2, s.EstimateCardinality());
        Assert.Equal(2, s.EstimateCardinality(long.MinValue));
        Assert.Equal(1, s.EstimateCardinality(long.MinValue + 1));
        Assert.Equal(1, s.EstimateCardinality(long.MaxValue));
    }

    [Fact]
    public void DuplicateAdds_DoNotInflateEstimate()
    {
        var s = NewSketch();
        for (int round = 0; round < 50; round++)
        {
            for (int i = 0; i < 100; i++)
                s.Add(i, round);
        }

        var once = new HyperLogLog<int, Int32Murmur3Hasher>(s.Precision);
        for (int i = 0; i < 100; i++)
            once.Add(i);

        Assert.Equal(once.EstimateCardinality(), s.EstimateCardinality());
        Assert.Equal(once.EstimateCardinality(), s.EstimateCardinality(since: 49));
    }

    [Fact]
    public void ZeroIntElement_IsAnOrdinaryElement()
    {
        var s = NewSketch();
        s.Add(0, 5);
        s.Add(0, 6);
        Assert.Equal(1, s.EstimateCardinality());
        s.Add(1, 7);
        Assert.Equal(2, s.EstimateCardinality());
    }

    [Fact]
    public void GuidEmpty_IsAnOrdinaryElement()
    {
        var s = new SlidingHyperLogLog<Guid, GuidHasher>();
        s.Add(Guid.Empty, 1);
        Assert.Equal(1, s.EstimateCardinality(1));
    }

    [Fact]
    public void NullReference_IsAddable_WithoutInvokingTheHasher()
    {
        // StringFnV1AHasher throws on null; the sketch must map null out of band.
        var s = new SlidingHyperLogLog<string?, NullThrowingHasher>();
        s.Add(null, 1);
        s.Add(null, 2);
        Assert.Equal(1, s.EstimateCardinality());
        Assert.Equal(1, s.EstimateCardinality(2));
        Assert.Equal(0, s.EstimateCardinality(3));
    }

    [Fact]
    public void StringElements_AreCounted()
    {
        var s = new SlidingHyperLogLog<string, StringFnV1AHasher>();
        s.Add("alice", 1);
        s.Add("bob", 2);
        s.Add("alice", 3);
        s.Add(string.Empty, 4);

        Assert.Equal(3, s.EstimateCardinality());
        Assert.Equal(2, s.EstimateCardinality(3)); // alice (re-added) and ""
    }

    [Fact]
    public void Clear_ResetsToEmpty_AndTheSketchStaysUsable()
    {
        var s = NewSketch();
        for (int i = 0; i < 1_000; i++)
            s.Add(i, i);
        Assert.NotEqual(0, s.EstimateCardinality());

        s.Clear();
        Assert.Equal(0, s.EstimateCardinality());
        Assert.Equal(0, s.EstimateCardinality(500));

        s.Add(3, 10);
        Assert.Equal(1, s.EstimateCardinality(10));
        Assert.Equal(0, s.EstimateCardinality(11));
    }

    [Fact]
    public void Clear_OnEmptySketch_IsANoOp()
    {
        var s = NewSketch();
        s.Clear();
        Assert.Equal(0, s.EstimateCardinality());
    }

    [Theory]
    [InlineData(4, 16)]
    [InlineData(12, 4096)]
    [InlineData(16, 65536)]
    public void Precision_DeterminesRegisterCount(int precision, int expectedRegisters)
    {
        var s = NewSketch(precision);
        Assert.Equal(precision, s.Precision);
        Assert.Equal(expectedRegisters, s.RegisterCount);
        Assert.Equal(1.04 / Math.Sqrt(expectedRegisters), s.StandardError, 12);
    }

    [Fact]
    public void DefaultPrecision_IsTwelve()
    {
        var s = new SlidingHyperLogLog<int, Int32Murmur3Hasher>();
        Assert.Equal(12, s.Precision);
        Assert.Equal(SlidingHyperLogLog<int, Int32Murmur3Hasher>.DefaultPrecision, s.Precision);
    }

    [Fact]
    public void Constructor_Throws_WhenPrecisionTooLow()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => NewSketch(SlidingHyperLogLog<int, Int32Murmur3Hasher>.MinPrecision - 1));
        Assert.Equal("precision", ex.ParamName);
    }

    [Fact]
    public void Constructor_Throws_WhenPrecisionTooHigh()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => NewSketch(SlidingHyperLogLog<int, Int32Murmur3Hasher>.MaxPrecision + 1));
        Assert.Equal("precision", ex.ParamName);
    }

    [Theory]
    [InlineData(SlidingHyperLogLog<int, Int32Murmur3Hasher>.MinPrecision)]
    [InlineData(SlidingHyperLogLog<int, Int32Murmur3Hasher>.MaxPrecision)]
    public void Constructor_Accepts_BoundaryPrecisions(int precision)
    {
        var s = NewSketch(precision);
        s.Add(1, 1);
        Assert.Equal(1, s.EstimateCardinality());
    }

    [Fact]
    public void UnionWith_EqualsTheSketchOfTheCombinedStream_ForEveryWindow()
    {
        var a = NewSketch(10);
        var b = NewSketch(10);
        var combined = NewSketch(10);
        for (int i = 0; i < 20_000; i++)
        {
            int key = i % 7_000;
            long time = i;
            if (i % 3 == 0)
                b.Add(key, time);
            else
                a.Add(key, time);
            combined.Add(key, time);
        }

        a.UnionWith(b);
        foreach (long since in new long[] { long.MinValue, 0, 7_000, 13_000, 19_999, 20_000 })
            Assert.Equal(combined.EstimateCardinality(since), a.EstimateCardinality(since));

        // The argument is left unmodified.
        Assert.True(b.EstimateCardinality() < a.EstimateCardinality());
    }

    [Fact]
    public void UnionWith_Self_ChangesNothing()
    {
        var s = NewSketch();
        for (int i = 0; i < 500; i++)
            s.Add(i, i);
        long before = s.EstimateCardinality(250);

        s.UnionWith(s);
        Assert.Equal(before, s.EstimateCardinality(250));
    }

    [Fact]
    public void UnionWith_Empty_ChangesNothing_AndEmptyAbsorbsOther()
    {
        var full = NewSketch();
        for (int i = 0; i < 500; i++)
            full.Add(i, i);
        long before = full.EstimateCardinality(100);

        full.UnionWith(NewSketch());
        Assert.Equal(before, full.EstimateCardinality(100));

        var empty = NewSketch();
        empty.UnionWith(full);
        Assert.Equal(before, empty.EstimateCardinality(100));
    }

    [Fact]
    public void UnionWith_Throws_OnNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => NewSketch().UnionWith(null!));
        Assert.Equal("other", ex.ParamName);
    }

    [Fact]
    public void UnionWith_Throws_OnPrecisionMismatch()
    {
        var ex = Assert.Throws<ArgumentException>(() => NewSketch(10).UnionWith(NewSketch(11)));
        Assert.Equal("other", ex.ParamName);
    }

    [Fact]
    public void AddAndQuery_DoNotAllocate()
    {
        var s = NewSketch();
        long now = 0;
        long estimate = 0;
        Action run = () =>
        {
            for (int i = 0; i < 10_000; i++)
                s.Add(i, ++now);
            estimate = s.EstimateCardinality(now - 5_000);
        };
        run(); // warm up

        // Quietest of three windows, exactly zero: a fixed 1 KB slack on one window flaked under coverage (#616).
        Assert.Equal(0, AllocationProbe.Quietest(run));
        Assert.True(estimate > 0);
    }

    private static void AssertNear(long expected, long actual, double standardError)
    {
        double bound = standardError * 4 + 0.01;
        double relativeError = Math.Abs(actual - expected) / (double)expected;
        Assert.True(relativeError <= bound,
            $"expected ~{expected}, got {actual}: relative error {relativeError:F4} > bound {bound:F4}");
    }

    private struct NullThrowingHasher : IHashProvider<string?>
    {
        public int Hash(string? key) => key is null ? throw new InvalidOperationException("hasher called with null") : key.Length;
    }
}
