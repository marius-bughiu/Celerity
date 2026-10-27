using Celerity.Statistics;

namespace Celerity.Tests.Statistics;

public class RollingQuantileTests
{
    [Fact]
    public void Constructor_ShouldStartEmpty_WithTheRequestedShape()
    {
        var rolling = new RollingQuantile<int>(windowSize: 5, quantile: 0.25d);

        Assert.Equal(5, rolling.WindowSize);
        Assert.Equal(0.25d, rolling.Quantile);
        Assert.Equal(0, rolling.Count);
        Assert.False(rolling.IsFull);
    }

    [Fact]
    public void Value_ShouldThrow_WhenTheWindowIsEmpty()
    {
        var rolling = new RollingQuantile<int>(3, 0.5d);

        Assert.Throws<InvalidOperationException>(() => rolling.Value);
        Assert.False(rolling.TryGetValue(out int value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void Value_ShouldBeTheMedian_OfAnOddWindow()
    {
        var rolling = new RollingQuantile<int>(5, 0.5d);
        rolling.Add(new[] { 9, 1, 7, 3, 5 });

        Assert.True(rolling.IsFull);
        Assert.Equal(5, rolling.Value);
        Assert.True(rolling.TryGetValue(out int value));
        Assert.Equal(5, value);
    }

    [Fact]
    public void Value_ShouldBeTheLowerMiddleValue_OfAnEvenWindow()
    {
        var rolling = new RollingQuantile<int>(4, 0.5d);
        rolling.Add(new[] { 40, 10, 30, 20 });

        // Rank ⌊0.5 · 3⌋ = 1 of [10, 20, 30, 40].
        Assert.Equal(20, rolling.Value);
    }

    [Fact]
    public void Value_ShouldTrackThePartialWindow_WhileItFills()
    {
        var rolling = new RollingQuantile<int>(100, 0.5d);

        rolling.Add(10);
        Assert.Equal(10, rolling.Value);

        rolling.Add(30);
        Assert.Equal(10, rolling.Value);

        rolling.Add(20);
        Assert.Equal(20, rolling.Value);
        Assert.Equal(3, rolling.Count);
        Assert.False(rolling.IsFull);
    }

    [Fact]
    public void Add_ShouldEvictTheOldestValue_OnceTheWindowIsFull()
    {
        var rolling = new RollingQuantile<int>(3, 0.5d);
        rolling.Add(new[] { 100, 1, 2 });
        Assert.Equal(2, rolling.Value);

        // 100 leaves the window: [1, 2, 3].
        rolling.Add(3);
        Assert.Equal(2, rolling.Value);

        // 1 leaves: [2, 3, 50].
        rolling.Add(50);
        Assert.Equal(3, rolling.Value);

        // 2 and 3 leave: [50, 60, 70].
        rolling.Add(60);
        rolling.Add(70);
        Assert.Equal(60, rolling.Value);
        Assert.Equal(3, rolling.Count);
    }

    [Fact]
    public void QuantileZero_ShouldBeTheWindowMinimum()
    {
        var rolling = new RollingQuantile<int>(3, 0d);
        rolling.Add(new[] { 5, 2, 8 });
        Assert.Equal(2, rolling.Value);

        rolling.Add(9); // [2, 8, 9]
        Assert.Equal(2, rolling.Value);

        rolling.Add(7); // [8, 9, 7]
        Assert.Equal(7, rolling.Value);
    }

    [Fact]
    public void QuantileOne_ShouldBeTheWindowMaximum()
    {
        var rolling = new RollingQuantile<int>(3, 1d);
        rolling.Add(new[] { 5, 2, 8 });
        Assert.Equal(8, rolling.Value);

        rolling.Add(1); // [2, 8, 1]
        Assert.Equal(8, rolling.Value);

        rolling.Add(0); // [8, 1, 0]
        Assert.Equal(8, rolling.Value);

        rolling.Add(3); // [1, 0, 3]
        Assert.Equal(3, rolling.Value);
    }

    [Fact]
    public void WindowOfOne_ShouldReportTheLatestValue()
    {
        var rolling = new RollingQuantile<int>(1, 0.5d);
        foreach (int value in new[] { 4, -2, 9, 9, 0 })
        {
            rolling.Add(value);
            Assert.Equal(value, rolling.Value);
            Assert.True(rolling.IsFull);
        }
    }

    [Fact]
    public void Value_ShouldHandleEveryValueEqual()
    {
        var rolling = new RollingQuantile<int>(8, 0.75d);
        for (int i = 0; i < 50; i++)
        {
            rolling.Add(7);
            Assert.Equal(7, rolling.Value);
        }
    }

    [Fact]
    public void Clear_ShouldEmptyTheWindow_AndKeepItsShape()
    {
        var rolling = new RollingQuantile<int>(3, 0.5d);
        rolling.Add(new[] { 1, 2, 3, 4 });

        rolling.Clear();

        Assert.Equal(0, rolling.Count);
        Assert.False(rolling.TryGetValue(out _));
        Assert.Equal(3, rolling.WindowSize);
        Assert.Equal(0.5d, rolling.Quantile);

        // Refilling after a clear starts a fresh window, oldest-first from the new values.
        rolling.Add(new[] { 30, 10, 20, 40 });
        Assert.Equal(20, rolling.Value); // [10, 20, 40]
    }

    [Fact]
    public void Clear_ShouldBeANoOp_OnAnEmptyWindow()
    {
        var rolling = new RollingQuantile<string>(2, 0.5d);
        rolling.Clear();

        Assert.Equal(0, rolling.Count);
        rolling.Add("b");
        Assert.Equal("b", rolling.Value);
    }

    [Fact]
    public void Clear_ShouldReleaseTheReferencesTheWindowHeld()
    {
        var rolling = new RollingQuantile<Box>(4, 0.5d);
        WeakReference[] weak = Fill(rolling);

        rolling.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.All(weak, w => Assert.False(w.IsAlive));
        GC.KeepAlive(rolling);
    }

    [Fact]
    public void ReferenceValues_ShouldBeOrderedByTheDefaultComparer_WithNullFirst()
    {
        var rolling = new RollingQuantile<string>(3, 0d);
        rolling.Add(new[] { null!, "pear", "apple" });
        Assert.Null(rolling.Value);

        rolling.Add("fig"); // null leaves: [pear, apple, fig]
        Assert.Equal("apple", rolling.Value);
    }

    [Fact]
    public void DoubleValues_ShouldOrderNaNBelowEveryNumber()
    {
        var rolling = new RollingQuantile<double>(3, 0d);
        rolling.Add(new[] { 1d, double.NaN, -5d });

        Assert.True(double.IsNaN(rolling.Value));
    }

    [Fact]
    public void Add_ShouldAcceptAnEmptySpan()
    {
        var rolling = new RollingQuantile<int>(2, 0.5d);
        rolling.Add(ReadOnlySpan<int>.Empty);

        Assert.Equal(0, rolling.Count);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference[] Fill(RollingQuantile<Box> rolling)
    {
        var weak = new WeakReference[6];
        for (int i = 0; i < weak.Length; i++)
        {
            var box = new Box(i);
            weak[i] = new WeakReference(box);
            rolling.Add(box);
        }

        return weak;
    }

    private sealed class Box : IComparable<Box>
    {
        public Box(int value) => Value = value;

        public int Value { get; }

        public int CompareTo(Box? other) => other is null ? 1 : Value.CompareTo(other.Value);
    }
}
