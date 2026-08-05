using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    /// <summary>
    /// Requires two buffers the sort writes to occupy distinct storage, even when they are declared
    /// with different element types.
    /// </summary>
    /// <remarks>
    /// Only the same-typed case is checked, and that is not a gap: two spans whose element types
    /// differ cannot be views over one another through safe code, so the reachable overlap is
    /// exactly the same-typed one — passing a buffer as both the keys and the payload, or handing
    /// the same scratch to two roles. The <c>typeof</c> test is a JIT-time constant per
    /// instantiation, so a mismatched pair (<c>int</c> keys with a <c>string</c> payload) compiles
    /// the whole check away.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RequireDistinctStorage<TLeft, TRight>(Span<TLeft> left, Span<TRight> right, string paramName)
    {
        if (typeof(TLeft) == typeof(TRight) && SharesStorage<TLeft, TRight>(left, right))
        {
            throw Aliases(paramName);
        }
    }

    /// <inheritdoc cref="RequireDistinctStorage{TLeft, TRight}(Span{TLeft}, Span{TRight}, string)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RequireDistinctStorage<TLeft, TRight>(ReadOnlySpan<TLeft> left, Span<TRight> right, string paramName)
    {
        if (typeof(TLeft) == typeof(TRight) && SharesStorage<TLeft, TRight>(left, right))
        {
            throw Aliases(paramName);
        }
    }

    // Only ever reached once the caller has established that the two element types are the same, so
    // viewing the right-hand span through the left-hand element type is a reinterpretation of an
    // identical layout. It has to go through the element reference rather than the span: Unsafe.As
    // cannot take a ref struct as a type argument.
    private static bool SharesStorage<TLeft, TRight>(ReadOnlySpan<TLeft> left, Span<TRight> right)
    {
        if (right.IsEmpty)
        {
            return false;
        }

        Span<TLeft> reinterpreted = MemoryMarshal.CreateSpan(
            ref Unsafe.As<TRight, TLeft>(ref MemoryMarshal.GetReference(right)),
            right.Length);

        return left.Overlaps(reinterpreted);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArgumentException Aliases(string paramName) =>
        new(
            "The buffer must not share storage with another span the sort writes; overlapping buffers corrupt the result rather than failing.",
            paramName);

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
