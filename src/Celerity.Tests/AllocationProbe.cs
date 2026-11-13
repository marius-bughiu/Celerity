namespace Celerity.Tests;

/// <summary>
/// Measures the steady-state allocation of a block of code for the "allocates nothing once warm" tests.
/// </summary>
/// <remarks>
/// A one-off runtime event on the test thread (tiering or OSR work, a lazily created thread-static, coverage
/// instrumentation) can land in any single measured window and has turned exact-zero and fixed-slack asserts
/// red (#590, #616). A real per-call allocation shows up in every window instead. So the probe runs the block
/// in several windows and reports the quietest one. The caller warms every path first, so nothing first-time is
/// left to fall in window one alone, and allocates the delegate and its closure before calling, outside every
/// window.
/// </remarks>
internal static class AllocationProbe
{
    /// <summary>The default number of windows; one event would have to recur in all of them to show.</summary>
    public const int DefaultWindows = 3;

    /// <summary>
    /// Runs <paramref name="run"/> in up to <paramref name="windows"/> windows and returns the fewest bytes the
    /// current thread allocated in any one of them. Stops early at a window that allocated nothing.
    /// </summary>
    public static long Quietest(Action run, int windows = DefaultWindows)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentOutOfRangeException.ThrowIfLessThan(windows, 1);

        long quietest = long.MaxValue;
        for (int window = 0; window < windows && quietest != 0; window++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            run();
            quietest = Math.Min(quietest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return quietest;
    }
}
