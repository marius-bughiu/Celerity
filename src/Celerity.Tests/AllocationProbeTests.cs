namespace Celerity.Tests;

/// <summary>
/// Pins the two properties the zero-allocation tests rely on: a one-off allocation in a single window is
/// absorbed (#616), and an allocation on every call is still reported.
/// </summary>
public class AllocationProbeTests
{
    [Fact]
    public void Quietest_ShouldAbsorbAnAllocationThatLandsInOneWindowOnly()
    {
        // The shape of the #616 flake: 1,536 bytes of runtime noise in an otherwise allocation-free window.
        int calls = 0;
        object? sink = null;
        Action run = () =>
        {
            if (calls++ == 0)
                sink = new byte[1_536];
        };

        Assert.Equal(0, AllocationProbe.Quietest(run));
        Assert.NotNull(sink);
        Assert.Equal(2, calls); // stopped at the first quiet window
    }

    [Fact]
    public void Quietest_ShouldReportAnAllocationThatRecursInEveryWindow()
    {
        object? sink = null;
        int calls = 0;
        Action run = () =>
        {
            calls++;
            sink = new byte[64];
        };

        Assert.True(AllocationProbe.Quietest(run) >= 64);
        Assert.NotNull(sink);
        Assert.Equal(AllocationProbe.DefaultWindows, calls);
    }

    [Fact]
    public void Quietest_ShouldRunTheRequestedNumberOfWindows()
    {
        int calls = 0;
        object? sink = null;
        Action run = () =>
        {
            calls++;
            sink = new byte[64];
        };

        AllocationProbe.Quietest(run, windows: 5);

        Assert.NotNull(sink);
        Assert.Equal(5, calls);
    }

    [Fact]
    public void Quietest_ShouldRejectInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(() => AllocationProbe.Quietest(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => AllocationProbe.Quietest(() => { }, windows: 0));
    }
}
