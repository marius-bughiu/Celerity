namespace Celerity.Tests.Utils;

/// <summary>
/// Regression tests for the non-throwing capacity-growth guard
/// (<see cref="FastUtils.TryDoubleCapacity"/>), the loop-friendly sibling of
/// <see cref="FastUtils.DoubleCapacity"/>.
///
/// The frozen collections' perfect-hash build search (#228) probes a series of candidate
/// table sizes and must stop cleanly at the <c>2^30</c> ceiling rather than throw. Before the
/// fix it advanced the candidate with a bare <c>size &lt;&lt;= 1</c> under a <c>size &lt;= 2^30</c>
/// loop guard, but once <c>size</c> reached <c>2^30</c> the shift wrapped to <see cref="int.MinValue"/>,
/// which still satisfies <c>size &lt;= 2^30</c>, so the next iteration allocated a negative-length
/// array and threw <see cref="OverflowException"/>. Routing the doubling through this helper makes
/// the boundary cheaply testable here — actually building a 2^30-key frozen table needs tens of GB.
/// </summary>
public class TryDoubleCapacityTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(4, 8)]
    [InlineData(16, 32)]
    [InlineData(1024, 2048)]
    [InlineData(1 << 20, 1 << 21)]
    [InlineData(1 << 28, 1 << 29)]
    public void TryDoubleCapacity_BelowCeiling_DoublesWithoutOverflow(int input, int expected)
    {
        bool ok = FastUtils.TryDoubleCapacity(input, out int doubled);

        Assert.True(ok);
        Assert.Equal(expected, doubled);
        Assert.True(doubled > 0, "doubling below the ceiling must never overflow to a negative value");
    }

    [Fact]
    public void TryDoubleCapacity_AtHalfTheCeiling_ReachesTheCeilingExactly()
    {
        // The last legal growth: 2^29 doubles to exactly 2^30, the maximum capacity.
        bool ok = FastUtils.TryDoubleCapacity(FastUtils.MaxPowerOfTwoCapacity / 2, out int doubled);

        Assert.True(ok);
        Assert.Equal(FastUtils.MaxPowerOfTwoCapacity, doubled);
    }

    [Fact]
    public void TryDoubleCapacity_AtTheCeiling_ReturnsFalseInsteadOfOverflowing()
    {
        // The pre-#228 bug analogue: 2^30 << 1 == 2^31 wraps to int.MinValue. The guard must
        // report "cannot grow" instead of returning a negative, allocation-corrupting size.
        bool ok = FastUtils.TryDoubleCapacity(FastUtils.MaxPowerOfTwoCapacity, out int doubled);

        Assert.False(ok);
        Assert.Equal(FastUtils.MaxPowerOfTwoCapacity, doubled); // unchanged on failure
    }

    [Theory]
    [InlineData((1 << 30) + 1)]
    [InlineData(int.MaxValue)]
    public void TryDoubleCapacity_AboveTheCeiling_ReturnsFalse(int input)
    {
        bool ok = FastUtils.TryDoubleCapacity(input, out int doubled);

        Assert.False(ok);
        Assert.Equal(input, doubled);
    }

    [Fact]
    public void TryDoubleCapacity_NeverThrowsAndNeverOverflows_OverAPowerOfTwoSweep()
    {
        // Walk every power of two from 1 up through and past the ceiling: the helper must
        // always succeed below 2^30, fail at/above it, and never return a negative size.
        for (int size = 1; size > 0; size <<= 1)
        {
            bool ok = FastUtils.TryDoubleCapacity(size, out int doubled);

            if (size < FastUtils.MaxPowerOfTwoCapacity)
            {
                Assert.True(ok);
                Assert.Equal(size * 2, doubled);
                Assert.True(doubled > 0);
            }
            else
            {
                Assert.False(ok);
                Assert.Equal(size, doubled);
            }
        }
    }
}
