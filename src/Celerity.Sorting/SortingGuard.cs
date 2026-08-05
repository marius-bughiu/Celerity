using System.Runtime.CompilerServices;

namespace Celerity.Sorting;

/// <summary>
/// The argument checks the three sorters share. Each failure path builds its exception in a separate
/// non-inlined factory that the guard <c>throw</c>s, so the checked path stays a single predictable
/// compare in the caller and the exception messages cannot drift between entry points.
/// </summary>
internal static class SortingGuard
{
    /// <summary>Requires <paramref name="buffer"/> to hold at least <paramref name="required"/> elements.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RequireLength(int buffer, int required, string paramName)
    {
        if (buffer < required)
        {
            throw TooShort(buffer, required, paramName);
        }
    }

    /// <summary>Requires two buffers that are written independently not to share storage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RequireNoOverlap(bool overlaps, string paramName)
    {
        if (overlaps)
        {
            throw Overlaps(paramName);
        }
    }

    /// <summary>Requires <paramref name="count"/> to be a valid prefix length of a span of <paramref name="length"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RequireCountInRange(int count, int length, string paramName)
    {
        if ((uint)count > (uint)length)
        {
            throw CountOutOfRange(count, length, paramName);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArgumentException TooShort(int buffer, int required, string paramName) =>
        new(
            $"The buffer must hold at least {required} element(s); it holds {buffer}.",
            paramName);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArgumentException Overlaps(string paramName) =>
        new(
            "The buffer must not overlap the span it is a scratch buffer for; the sort writes both.",
            paramName);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArgumentOutOfRangeException CountOutOfRange(int count, int length, string paramName) =>
        new(
            paramName,
            count,
            $"The count must be between 0 and the span length ({length}).");
}
