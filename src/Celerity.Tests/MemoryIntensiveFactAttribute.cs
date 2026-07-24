namespace Celerity.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> for a test that needs a large allocation to reproduce the behaviour it
/// guards. The test is skipped — reported as skipped, not silently passed — when the environment does not
/// report enough headroom, so a memory-capped container or runner can never turn the build red on resource
/// grounds while every environment with room still runs the check.
/// </summary>
/// <remarks>
/// The decision is made at discovery time from <see cref="GCMemoryInfo.TotalAvailableMemoryBytes"/>, which
/// reflects the container/cgroup limit where one applies rather than the host's physical memory. A multiple
/// of the bare requirement is demanded so the test never allocates right up against the ceiling.
/// </remarks>
public sealed class MemoryIntensiveFactAttribute : FactAttribute
{
    // Headroom multiple over the stated requirement before the test is considered safe to run.
    private const int RequiredHeadroomFactor = 3;

    /// <summary>
    /// Marks a test as requiring <paramref name="requiredMegabytes"/> of allocatable memory.
    /// </summary>
    /// <param name="requiredMegabytes">The size of the allocation the test makes, in MiB.</param>
    public MemoryIntensiveFactAttribute(int requiredMegabytes)
    {
        long required = (long)requiredMegabytes * 1024 * 1024;
        long available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        // A non-positive reading means "unknown" — run the test rather than skip on missing information.
        if (available > 0 && available < required * RequiredHeadroomFactor)
        {
            Skip = $"Needs ~{requiredMegabytes} MiB of allocatable memory " +
                   $"(with {RequiredHeadroomFactor}x headroom); this environment reports " +
                   $"{available / (1024 * 1024)} MiB available.";
        }
    }
}
