namespace Celerity.Cardinality.Tests;

public class DistinctTests
{
    [Fact]
    public void SmallStream_IsExact()
    {
        var distinct = new StringDistinct();
        distinct.Add("a");
        distinct.Add("b");
        distinct.Add("c");

        Assert.True(distinct.IsExact);
        Assert.Equal(3, distinct.Count());
        Assert.Equal(0d, distinct.StandardError);
    }

    [Fact]
    public void Duplicates_DoNotInflateTheCount()
    {
        var distinct = new StringDistinct();
        for (int i = 0; i < 1000; i++)
            distinct.Add("same");

        Assert.True(distinct.IsExact);
        Assert.Equal(1, distinct.Count());
    }

    [Fact]
    public void PromotesToHyperLogLog_PastThreshold()
    {
        var distinct = new StringDistinct(exactThreshold: 100);
        for (int i = 0; i < 500; i++)
            distinct.Add($"key-{i}");

        Assert.False(distinct.IsExact);
        Assert.True(distinct.StandardError > 0d);

        long estimate = distinct.Count();
        double relativeError = Math.Abs(estimate - 500) / 500.0;
        Assert.True(relativeError < 0.10, $"estimate {estimate} vs 500 (error {relativeError:P1})");
    }

    [Fact]
    public void LargeCardinality_IsEstimatedWithinError()
    {
        var distinct = new StringDistinct();
        const int n = 200_000;
        for (int i = 0; i < n; i++)
            distinct.Add($"user-{i}");

        Assert.False(distinct.IsExact);
        long estimate = distinct.Count();
        double relativeError = Math.Abs(estimate - n) / (double)n;
        Assert.True(relativeError < 0.03, $"estimate {estimate} vs {n} (error {relativeError:P1})");
    }

    [Fact]
    public void Merge_BothExactAndSmall_StaysExact()
    {
        var a = new StringDistinct();
        var b = new StringDistinct();
        for (int i = 0; i < 100; i++) a.Add($"a-{i}");
        for (int i = 0; i < 100; i++) b.Add($"b-{i}");

        a.Merge(b);

        Assert.True(a.IsExact);
        Assert.Equal(200, a.Count());
    }

    [Fact]
    public void Merge_OverlappingExactSets_CountsTheUnion()
    {
        var a = new StringDistinct();
        var b = new StringDistinct();
        for (int i = 0; i < 100; i++) a.Add($"k-{i}");
        for (int i = 50; i < 150; i++) b.Add($"k-{i}");

        a.Merge(b);

        Assert.True(a.IsExact);
        Assert.Equal(150, a.Count()); // union of {0..99} and {50..149}
    }

    [Fact]
    public void Merge_ExactPlusEstimated_ProducesEstimate()
    {
        var big = new StringDistinct(exactThreshold: 100);
        for (int i = 0; i < 5_000; i++) big.Add($"big-{i}");
        Assert.False(big.IsExact);

        var small = new StringDistinct(exactThreshold: 100);
        for (int i = 0; i < 20; i++) small.Add($"small-{i}");
        Assert.True(small.IsExact);

        big.Merge(small);

        Assert.False(big.IsExact);
        long estimate = big.Count();
        double relativeError = Math.Abs(estimate - 5_020) / 5_020.0;
        Assert.True(relativeError < 0.10, $"estimate {estimate} vs 5020 (error {relativeError:P1})");
    }

    [Fact]
    public void Merge_TwoLargeShards_ApproximatesGlobalDistinct()
    {
        var shard1 = new StringDistinct();
        var shard2 = new StringDistinct();
        const int perShard = 100_000;
        for (int i = 0; i < perShard; i++) shard1.Add($"user-{i}");
        for (int i = perShard / 2; i < perShard + perShard / 2; i++) shard2.Add($"user-{i}"); // 50% overlap

        shard1.Merge(shard2);

        long estimate = shard1.Count();
        const int trueUnion = perShard + perShard / 2; // 150k distinct
        double relativeError = Math.Abs(estimate - trueUnion) / (double)trueUnion;
        Assert.True(relativeError < 0.03, $"estimate {estimate} vs {trueUnion} (error {relativeError:P1})");
    }

    [Fact]
    public void Merge_PrecisionMismatch_Throws()
    {
        var a = new StringDistinct(precision: 12);
        var b = new StringDistinct(precision: 14);
        Assert.Throws<ArgumentException>(() => a.Merge(b));
    }

    [Fact]
    public void Clear_ResetsToExact()
    {
        var distinct = new StringDistinct(exactThreshold: 50);
        for (int i = 0; i < 500; i++) distinct.Add($"k-{i}");
        Assert.False(distinct.IsExact);

        distinct.Clear();

        Assert.True(distinct.IsExact);
        Assert.Equal(0, distinct.Count());
    }

    [Fact]
    public void Constructor_InvalidPrecision_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringDistinct(precision: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringDistinct(precision: 99));
    }

    [Fact]
    public void Constructor_NegativeThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringDistinct(exactThreshold: -1));
    }

    [Fact]
    public void Works_WithIntegerKeys()
    {
        var distinct = new Distinct<long, Int64WangNaiveHasher>(exactThreshold: 100);
        for (long i = 0; i < 1000; i++)
            distinct.Add(i);

        long estimate = distinct.Count();
        double relativeError = Math.Abs(estimate - 1000) / 1000.0;
        Assert.True(relativeError < 0.10, $"estimate {estimate} vs 1000 (error {relativeError:P1})");
    }
}
