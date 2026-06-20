namespace Celerity.Tests.Utils;

/// <summary>
/// Regression tests for the centralized resize-growth guard (<see cref="FastUtils.DoubleCapacity"/>)
/// and the <see cref="FastUtils.MaxPowerOfTwoCapacity"/> ceiling it enforces.
///
/// Before #221 every open-addressed collection's <c>Resize()</c> doubled its backing array with a
/// bare <c>capacity * 2</c> and no upper bound. A table that had grown to <c>2^30</c> slots would
/// compute <c>2^31</c> on its next resize, overflowing a signed <see cref="int"/> to a negative
/// value (<see cref="int.MinValue"/>) and corrupting the <c>newSize - 1</c> slot mask. These tests
/// pin the guard at the boundary directly — actually allocating a 2^30-slot table needs 8+ GB, so
/// the resize paths route through this single helper precisely so the boundary is cheaply testable.
/// </summary>
public class DoubleCapacityTests
{
    [Fact]
    public void MaxPowerOfTwoCapacity_IsTwoToThe30th()
    {
        Assert.Equal(1 << 30, FastUtils.MaxPowerOfTwoCapacity);
        Assert.Equal(1_073_741_824, FastUtils.MaxPowerOfTwoCapacity);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(4, 8)]
    [InlineData(16, 32)]
    [InlineData(1024, 2048)]
    [InlineData(1 << 20, 1 << 21)]
    [InlineData(1 << 28, 1 << 29)]
    public void DoubleCapacity_BelowCeiling_DoublesWithoutOverflow(int input, int expected)
    {
        int result = FastUtils.DoubleCapacity(input);

        Assert.Equal(expected, result);
        Assert.True(result > 0, "doubling below the ceiling must never overflow to a negative value");
    }

    [Fact]
    public void DoubleCapacity_AtHalfTheCeiling_ReachesTheCeilingExactly()
    {
        // The last legal growth: 2^29 doubles to exactly 2^30, the maximum capacity. This is the
        // largest input that must still succeed.
        int result = FastUtils.DoubleCapacity(FastUtils.MaxPowerOfTwoCapacity / 2);

        Assert.Equal(FastUtils.MaxPowerOfTwoCapacity, result);
    }

    [Fact]
    public void DoubleCapacity_AtTheCeiling_ThrowsInsteadOfOverflowing()
    {
        // The pre-#221 bug: 2^30 * 2 == 2^31 overflows to int.MinValue. The guard must throw a
        // clear error instead of returning a negative, mask-corrupting size.
        var ex = Assert.Throws<InvalidOperationException>(
            () => FastUtils.DoubleCapacity(FastUtils.MaxPowerOfTwoCapacity));

        Assert.Contains("maximum capacity", ex.Message);
    }

    [Theory]
    [InlineData((1 << 30) + 1)]
    [InlineData(int.MaxValue)]
    public void DoubleCapacity_AboveTheCeiling_Throws(int input)
    {
        Assert.Throws<InvalidOperationException>(() => FastUtils.DoubleCapacity(input));
    }
}
