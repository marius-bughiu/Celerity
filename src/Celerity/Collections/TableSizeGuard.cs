namespace Celerity.Collections;

/// <summary>
/// The one place the hash tables' <c>EnsureCapacity(int)</c> / <c>TrimExcess(int)</c> reachability
/// check is written (issue #570). Both methods promise the table holds at least <c>capacity</c>
/// entries afterwards, but <see cref="FastUtils.MinTableSizeFor"/> stops at
/// <see cref="FastUtils.MaxPowerOfTwoCapacity"/> slots, so at a low enough load factor no table it
/// can return holds that many. Before this guard the caller resized to that capped table anyway (a
/// multi-gigabyte allocation) and handed back a capacity below the one asked for.
/// </summary>
/// <remarks>
/// The check runs before anything is allocated, and throws <see cref="ArgumentOutOfRangeException"/>
/// for <c>capacity</c>, the same exception the methods' negative and below-<c>Count</c> guards
/// already throw. Constructors do not route through it: their capacity is an initial-size hint,
/// not a no-resize guarantee.
/// </remarks>
internal static class TableSizeGuard
{
    /// <summary>
    /// Returns <see cref="FastUtils.MinTableSizeFor"/>'s size for <paramref name="capacity"/>, after
    /// checking that its threshold, <c>(int)(size * loadFactor)</c>, actually reaches it.
    /// </summary>
    internal static int MinTableSizeFor(int capacity, float loadFactor)
    {
        int size = FastUtils.MinTableSizeFor(capacity, loadFactor);
        ThrowIfUnreachable((int)(size * loadFactor), capacity, loadFactor);
        return size;
    }

    /// <summary>
    /// Throws when <paramref name="reachable"/>, the entry capacity of the largest table the caller
    /// can build, is below <paramref name="capacity"/>. For a type whose threshold is not
    /// <c>(int)(size * loadFactor)</c>.
    /// </summary>
    internal static void ThrowIfUnreachable(int reachable, int capacity, float loadFactor)
    {
        if (reachable < capacity)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity,
                $"No table can hold {capacity} entries at load factor {loadFactor}; the largest holds {reachable}.");
    }
}
