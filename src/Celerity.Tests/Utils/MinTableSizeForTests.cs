namespace Celerity.Tests.Utils;

/// <summary>
/// Tests for <see cref="FastUtils.MinTableSizeFor(int, float)"/>, the hash-table sizing primitive
/// behind <c>EnsureCapacity</c> / <c>TrimExcess</c> (issue #231). The helper returns the smallest
/// power-of-two table size whose truncated load-factor threshold (<c>(int)(size * loadFactor)</c>)
/// admits the requested entry count — i.e. the size a table must reach to hold that many entries
/// without resizing — capped at <see cref="FastUtils.MaxPowerOfTwoCapacity"/>.
/// </summary>
public class MinTableSizeForTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NonPositiveCount_ReturnsOne(int count)
    {
        Assert.Equal(1, FastUtils.MinTableSizeFor(count, 0.75f));
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.9f)]
    public void Result_IsPowerOfTwo_AndThresholdAdmitsCount(float loadFactor)
    {
        foreach (int count in new[] { 1, 2, 3, 7, 8, 9, 13, 16, 17, 100, 1000, 12_345, 100_000 })
        {
            int size = FastUtils.MinTableSizeFor(count, loadFactor);

            Assert.True((size & (size - 1)) == 0, $"size {size} must be a power of two");
            Assert.True((int)(size * loadFactor) >= count,
                $"threshold for size {size} at lf {loadFactor} must admit {count}");
        }
    }

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.9f)]
    public void Result_IsMinimal_HalfSizeWouldNotFit(float loadFactor)
    {
        foreach (int count in new[] { 1, 2, 3, 7, 9, 17, 100, 1000, 12_345, 100_000 })
        {
            int size = FastUtils.MinTableSizeFor(count, loadFactor);
            if (size <= 1)
                continue;

            // If a smaller power-of-two table could also hold the count, the helper picked too big.
            Assert.True((int)((size >> 1) * loadFactor) < count,
                $"half of {size} at lf {loadFactor} should not admit {count}");
        }
    }

    [Fact]
    public void Result_IsAtLeastNextPowerOfTwo()
    {
        foreach (int count in new[] { 1, 5, 16, 17, 1000 })
            Assert.True(FastUtils.MinTableSizeFor(count, 0.75f) >= FastUtils.NextPowerOfTwo(count));
    }

    [Fact]
    public void OutOfRangeLoadFactor_IsClampedRatherThanDividingByZeroOrOne()
    {
        // A degenerate load factor must not throw or loop forever; it is clamped into (0, 1).
        Assert.True(FastUtils.MinTableSizeFor(100, 0f) >= FastUtils.NextPowerOfTwo(100));
        Assert.True(FastUtils.MinTableSizeFor(100, 1f) >= FastUtils.NextPowerOfTwo(100));
        Assert.True(FastUtils.MinTableSizeFor(100, 2f) >= FastUtils.NextPowerOfTwo(100));
    }

    [Fact]
    public void HugeCount_CapsAtMaxPowerOfTwoCapacity()
    {
        // No power-of-two table can hold int.MaxValue entries; the helper saturates at the ceiling
        // rather than overflowing.
        Assert.Equal(FastUtils.MaxPowerOfTwoCapacity, FastUtils.MinTableSizeFor(int.MaxValue, 0.75f));
    }
}
