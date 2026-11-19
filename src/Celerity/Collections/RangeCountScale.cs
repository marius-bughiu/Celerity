using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// Multiplies a value by an element count for the sum actions, where the count must not be converted to an
/// element type that cannot represent it.
/// </summary>
/// <remarks>
/// <see cref="INumberBase{TSelf}.CreateTruncating{TOther}(TOther)"/> is exact for a wrapping fixed-width integer, but
/// <see cref="Half"/>'s largest finite value is 65,504: a node of 65,536 elements would convert its count to
/// infinity, and a zero update would then produce <c>0 × ∞ = NaN</c>. <see cref="Half"/> is therefore scaled in
/// <see cref="float"/>, which holds every count exactly, and rounded back once. The <c>typeof</c> test is a JIT-time
/// constant, so every other element type compiles to the single multiplication.
/// </remarks>
internal static class RangeCountScale
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static T Multiply<T>(T value, int count)
        where T : struct, INumberBase<T>
    {
        if (typeof(T) == typeof(Half))
        {
            Half scaled = (Half)((float)Unsafe.As<T, Half>(ref value) * count);
            return Unsafe.As<Half, T>(ref scaled);
        }

        return value * T.CreateTruncating(count);
    }
}
