using Celerity.Statistics;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Unit coverage for <see cref="KllSketch{T}"/>: the empty sketch, the exact regime before the first
/// compaction, the exact extremes, the rank query, merging (including into itself), the cached sorted view
/// and <see cref="KllSketch{T}.Clear"/>. Accuracy against an oracle lives in
/// <see cref="KllSketchDifferentialTests"/>.
/// </summary>
public class KllSketchTests
{
    [Fact]
    public void EmptySketch_ShouldReportNothing_AndThrowOnEveryQuery()
    {
        var sketch = new KllSketch<int>();

        Assert.True(sketch.IsEmpty);
        Assert.Equal(0, sketch.Count);
        Assert.Equal(0, sketch.RetainedCount);
        Assert.Throws<InvalidOperationException>(() => sketch.Min);
        Assert.Throws<InvalidOperationException>(() => sketch.Max);
        Assert.Throws<InvalidOperationException>(() => sketch.GetQuantile(0.5d));
        Assert.Throws<InvalidOperationException>(() => sketch.GetRank(1));
        Assert.Throws<InvalidOperationException>(() => sketch.GetQuantiles(new[] { 0.5d }, new int[1]));

        // Clearing a sketch that has never been queried has no view to release.
        sketch.Clear();
        Assert.True(sketch.IsEmpty);
    }

    [Fact]
    public void GetQuantile_ShouldBeExact_UntilTheFirstCompaction()
    {
        const int K = 64;
        var random = new Random(11);
        int[] values = Enumerable.Range(0, K).Select(_ => random.Next(1_000)).ToArray();
        var sketch = new KllSketch<int>(K);
        sketch.Add(values);

        Assert.Equal(K, sketch.RetainedCount);
        int[] sorted = values.Order().ToArray();
        for (int i = 0; i <= 100; i++)
        {
            double q = i / 100d;
            Assert.Equal(sorted[(int)(q * (K - 1))], sketch.GetQuantile(q));
        }

        foreach (int probe in new[] { -1, 0, 500, 999, 1_000 })
        {
            double expected = sorted.Count(v => v <= probe) / (double)K;
            Assert.Equal(expected, sketch.GetRank(probe));
        }
    }

    [Fact]
    public void MinAndMax_ShouldBeExact_WhicheverOrderTheValuesArrive()
    {
        var sketch = new KllSketch<int>(KllSketch<int>.MinK);
        sketch.Add(new[] { 50, 50, 10, 90, 30, 10, 90 });
        Assert.Equal(10, sketch.Min);
        Assert.Equal(90, sketch.Max);

        // Long enough to compact many times, so neither extreme need still be retained.
        for (int i = 0; i < 10_000; i++)
        {
            sketch.Add(20 + (i % 60));
        }

        sketch.Add(-5);
        sketch.Add(1_000);
        for (int i = 0; i < 10_000; i++)
        {
            sketch.Add(20 + (i % 60));
        }

        Assert.Equal(-5, sketch.Min);
        Assert.Equal(1_000, sketch.Max);
        Assert.Equal(-5, sketch.GetQuantile(0d));
        Assert.Equal(1_000, sketch.GetQuantile(1d));
        Assert.Equal(1d, sketch.GetRank(1_000));
        Assert.Equal(0d, sketch.GetRank(-6));
    }

    [Fact]
    public void TinyQuantiles_ShouldResolveToTheExactMinimum_WhenTheirRankFloorsToZero()
    {
        var sketch = new KllSketch<int>();
        sketch.Add(new[] { 7, 3, 9 });

        Assert.Equal(3, sketch.GetQuantile(0.49d));
        Assert.Equal(7, sketch.GetQuantile(0.5d));
        Assert.Equal(7, sketch.GetQuantile(0.99d));
        Assert.Equal(9, sketch.GetQuantile(1d));
    }

    [Fact]
    public void RetainedCount_ShouldStayBounded_HoweverLongTheStream()
    {
        var sketch = new KllSketch<int>();
        for (int i = 0; i < 1_000_000; i++)
        {
            sketch.Add(i);
        }

        Assert.Equal(1_000_000, sketch.Count);
        Assert.InRange(sketch.RetainedCount, 1, 700);
    }

    [Fact]
    public void SameSeedAndStream_ShouldGiveTheSameSketch()
    {
        var first = new KllSketch<int>(32, seed: 99UL);
        var second = new KllSketch<int>(32, seed: 99UL);
        var random = new Random(3);
        for (int i = 0; i < 20_000; i++)
        {
            int value = random.Next();
            first.Add(value);
            second.Add(value);
        }

        Assert.Equal(first.RetainedCount, second.RetainedCount);
        for (int i = 0; i <= 50; i++)
        {
            Assert.Equal(first.GetQuantile(i / 50d), second.GetQuantile(i / 50d));
        }
    }

    [Fact]
    public void Queries_ShouldSeeEveryAdd_ThroughTheCachedView()
    {
        // A reference type, so the view's release of slots past a shrunken end is exercised too.
        var sketch = new KllSketch<string>(KllSketch<string>.MinK);
        for (int i = 0; i < 8; i++)
        {
            sketch.Add(i.ToString("D4"));
        }

        Assert.Equal("0003", sketch.GetQuantile(0.5d));
        Assert.Equal(8, sketch.RetainedCount);

        // The ninth value compacts level 0, so the retained set shrinks below the view's last length.
        sketch.Add("0008");
        Assert.True(sketch.RetainedCount < 9);
        string median = sketch.GetQuantile(0.5d);
        Assert.InRange(string.CompareOrdinal(median, "0002"), 0, int.MaxValue);
        Assert.InRange(string.CompareOrdinal(median, "0006"), int.MinValue, 0);

        // Grow the stack well past the first view's length, then query again.
        for (int i = 9; i < 5_000; i++)
        {
            sketch.Add(i.ToString("D4"));
        }

        Assert.Equal("4999", sketch.GetQuantile(1d));
        Assert.InRange(sketch.GetRank("2500"), 0.3d, 0.7d);
    }

    [Fact]
    public void QueryingAfterEveryAdd_ShouldAnswerExactlyAsARebuiltViewDoes()
    {
        // The patched-in-place view must be indistinguishable from one sorted from scratch. Two sketches
        // with the same seed see the same stream; one is queried after every value (so most adds patch its
        // live view), the other only at checkpoints (so its view is always rebuilt).
        var patched = new KllSketch<int>(16, seed: 5UL);
        var rebuilt = new KllSketch<int>(16, seed: 5UL);
        var random = new Random(17);
        for (int i = 0; i < 5_000; i++)
        {
            // Narrow and wide draws, plus new extremes, so values land at the front, the back and among ties.
            int value = (i % 97) switch
            {
                0 => -1_000 - i,
                1 => 1_000 + i,
                _ => i % 3 == 0 ? random.Next(10) : random.Next(1_000),
            };

            patched.Add(value);
            rebuilt.Add(value);
            _ = patched.GetQuantile(0.5d);

            if (i % 250 == 249)
            {
                for (int q = 0; q <= 40; q++)
                {
                    Assert.Equal(rebuilt.GetQuantile(q / 40d), patched.GetQuantile(q / 40d));
                }

                foreach (int probe in new[] { int.MinValue, -1_500, 0, 5, 500, 2_000, int.MaxValue })
                {
                    Assert.Equal(rebuilt.GetRank(probe), patched.GetRank(probe));
                }
            }
        }
    }

    [Fact]
    public void Merge_ShouldBeANoOp_WhenTheOtherSketchIsEmpty()
    {
        var sketch = new KllSketch<int>();
        sketch.Add(new[] { 1, 2, 3 });
        sketch.Merge(new KllSketch<int>());

        Assert.Equal(3, sketch.Count);
        Assert.Equal(2, sketch.GetQuantile(0.5d));
    }

    [Fact]
    public void Merge_IntoAnEmptySketch_ShouldAdoptTheOther()
    {
        var other = new KllSketch<int>();
        other.Add(new[] { 5, 1, 9 });
        var sketch = new KllSketch<int>();
        sketch.Merge(other);

        Assert.Equal(3, sketch.Count);
        Assert.Equal(1, sketch.Min);
        Assert.Equal(9, sketch.Max);
        Assert.Equal(5, sketch.GetQuantile(0.5d));
        Assert.Equal(3, other.Count);
    }

    [Fact]
    public void Merge_ShouldBeExact_WhileTheCombinedValuesFitWithoutCompaction()
    {
        var left = new KllSketch<int>();
        var right = new KllSketch<int>();
        left.Add(new[] { 10, 30, 50 });
        right.Add(new[] { 20, 40, 5, 60 });
        left.Merge(right);

        Assert.Equal(7, left.Count);
        Assert.Equal(7, left.RetainedCount);
        Assert.Equal(5, left.Min);
        Assert.Equal(60, left.Max);
        Assert.Equal(30, left.GetQuantile(0.5d));

        // Neither extreme from the other side: both stay put.
        var inner = new KllSketch<int>();
        inner.Add(new[] { 20, 30 });
        left.Merge(inner);
        Assert.Equal(5, left.Min);
        Assert.Equal(60, left.Max);
    }

    [Theory]
    [InlineData(100, 50_000)]
    [InlineData(50_000, 100)]
    [InlineData(20_000, 20_000)]
    public void Merge_ShouldCombineStacksOfAnyHeight(int leftCount, int rightCount)
    {
        var left = new KllSketch<int>(KllSketch<int>.MinK * 4, seed: 1UL);
        var right = new KllSketch<int>(KllSketch<int>.MinK * 4, seed: 2UL);
        for (int i = 0; i < leftCount; i++)
        {
            left.Add(i * 2);
        }

        for (int i = 0; i < rightCount; i++)
        {
            right.Add((i * 2) + 1);
        }

        int rightRetained = right.RetainedCount;
        left.Merge(right);

        int total = leftCount + rightCount;
        Assert.Equal(total, left.Count);
        Assert.Equal(rightCount, right.Count);
        Assert.Equal(rightRetained, right.RetainedCount);
        Assert.Equal(0, left.Min);
        Assert.Equal(Math.Max((leftCount - 1) * 2, (rightCount * 2) - 1), left.Max);

        // The merged sketch keeps answering within its error; a K of 32 is coarse, so the bound is loose.
        double median = left.GetQuantile(0.5d);
        double trueRank = (Math.Min(median, (leftCount - 1) * 2) / 2d + 1) + (Math.Min(median, (rightCount * 2) - 1) / 2d);
        Assert.InRange(trueRank / total, 0.3d, 0.7d);
    }

    [Fact]
    public void Merge_ShouldAcceptADifferentK_AndKeepItsOwn()
    {
        var coarse = new KllSketch<int>(KllSketch<int>.MinK);
        var fine = new KllSketch<int>(400);
        for (int i = 0; i < 10_000; i++)
        {
            coarse.Add(i);
            fine.Add(i + 10_000);
        }

        fine.Merge(coarse);
        Assert.Equal(400, fine.K);
        Assert.Equal(20_000, fine.Count);
        Assert.Equal(0, fine.Min);
        Assert.Equal(19_999, fine.Max);
        Assert.InRange(fine.GetQuantile(0.5d), 7_000, 13_000);
    }

    [Fact]
    public void Merge_ShouldDoubleTheStream_WhenASketchIsMergedIntoItself()
    {
        var sketch = new KllSketch<int>(KllSketch<int>.MinK);
        sketch.Add(new[] { 4, 1, 3, 2 });
        sketch.Merge(sketch);

        Assert.Equal(8, sketch.Count);
        Assert.Equal(1, sketch.Min);
        Assert.Equal(4, sketch.Max);
        Assert.Equal(0.5d, sketch.GetRank(2));
        Assert.Equal(1d, sketch.GetRank(4));
    }

    [Fact]
    public void Merge_ShouldThrowOnCountOverflow_AndLeaveTheSketchUnchanged()
    {
        // Each self-merge doubles the count, so 62 of them reach 2^62 from one value.
        var sketch = new KllSketch<long>(KllSketch<long>.MinK);
        sketch.Add(new[] { 3L, 1L, 2L });
        for (int i = 0; i < 61; i++)
        {
            sketch.Merge(sketch);
        }

        long count = sketch.Count;
        Assert.Equal(3L << 61, count);
        Assert.Equal(1L, sketch.GetQuantile(0d));
        Assert.Equal(3L, sketch.GetQuantile(1d));
        Assert.Equal(2L, sketch.GetQuantile(0.5d));
        Assert.InRange(sketch.GetRank(2L), 0.4d, 0.9d);

        Assert.Throws<OverflowException>(() => sketch.Merge(sketch));
        Assert.Equal(count, sketch.Count);
        Assert.Equal(3L, sketch.GetQuantile(1d));
    }

    [Fact]
    public void Add_ShouldThrowOnCountOverflow_AndLeaveTheSketchUnchanged()
    {
        // Self-merge then add, 62 times, takes a count of 1 to 2^63 − 1 while retaining only a handful.
        var sketch = new KllSketch<long>(KllSketch<long>.MinK);
        sketch.Add(5L);
        for (int i = 0; i < 62; i++)
        {
            sketch.Merge(sketch);
            sketch.Add(5L);
        }

        Assert.Equal(long.MaxValue, sketch.Count);
        int retained = sketch.RetainedCount;

        Assert.Throws<OverflowException>(() => sketch.Add(6L));
        Assert.Equal(long.MaxValue, sketch.Count);
        Assert.Equal(retained, sketch.RetainedCount);
        Assert.Equal(5L, sketch.Max);
        Assert.Equal(1d, sketch.GetRank(5L));
    }

    [Fact]
    public void Add_ShouldLeaveTheSketchUnchanged_WhenComparingTheNewValueThrows()
    {
        var sketch = new KllSketch<Fragile>();
        sketch.Add(new[] { new Fragile(1), new Fragile(3), new Fragile(2) });
        Assert.Equal(2, sketch.GetQuantile(0.5d).Value);

        Fragile.Armed = true;
        try
        {
            Assert.Throws<InvalidOperationException>(() => sketch.Add(new Fragile(9)));
        }
        finally
        {
            Fragile.Armed = false;
        }

        Assert.Equal(3, sketch.Count);
        Assert.Equal(3, sketch.RetainedCount);
        Assert.Equal(3, sketch.Max.Value);
        Assert.Equal(1d, sketch.GetRank(new Fragile(3)));
    }

    [Fact]
    public void Merge_ShouldLeaveTheSketchUnchanged_WhenComparingTheExtremesThrows()
    {
        var sketch = new KllSketch<Fragile>();
        sketch.Add(new Fragile(4));
        var other = new KllSketch<Fragile>();
        other.Add(new Fragile(7));

        Fragile.Armed = true;
        try
        {
            Assert.Throws<InvalidOperationException>(() => sketch.Merge(other));
        }
        finally
        {
            Fragile.Armed = false;
        }

        Assert.Equal(1, sketch.Count);
        Assert.Equal(1, sketch.RetainedCount);
        Assert.Equal(1d, sketch.GetRank(new Fragile(4)));
        Assert.Equal(4, sketch.Max.Value);
    }

    [Fact]
    public void Clear_ShouldEmptyTheSketch_AndLeaveItReusable()
    {
        var sketch = new KllSketch<string>(KllSketch<string>.MinK);
        sketch.Add(new[] { "b", "a", "c" });
        Assert.Equal("b", sketch.GetQuantile(0.5d));

        // Before any compaction: the buffer is still one level's worth.
        sketch.Clear();
        Assert.True(sketch.IsEmpty);
        Assert.Equal(0, sketch.RetainedCount);
        Assert.Throws<InvalidOperationException>(() => sketch.GetQuantile(0.5d));

        // After the stack has grown: the buffer shrinks back to one level.
        for (int i = 0; i < 1_000; i++)
        {
            sketch.Add(i.ToString("D4"));
        }

        Assert.True(sketch.RetainedCount > KllSketch<string>.MinK);
        sketch.Clear();
        Assert.Equal(0, sketch.Count);
        Assert.Equal(0, sketch.RetainedCount);

        sketch.Add(new[] { "y", "x", "z" });
        Assert.Equal("x", sketch.Min);
        Assert.Equal("z", sketch.Max);
        Assert.Equal("y", sketch.GetQuantile(0.5d));
    }

    // A key whose comparison can be made to throw, to pin which operations change nothing when it does.
    private readonly struct Fragile : IComparable<Fragile>
    {
        [ThreadStatic]
        private static bool armed;

        public Fragile(int value) => Value = value;

        public static bool Armed
        {
            get => armed;
            set => armed = value;
        }

        public int Value { get; }

        public int CompareTo(Fragile other)
        {
            if (armed)
            {
                throw new InvalidOperationException("Comparison failed.");
            }

            return Value.CompareTo(other.Value);
        }
    }
}
