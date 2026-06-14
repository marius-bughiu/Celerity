using System;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// Guaranteed branch-free conditional selection — a <see cref="Select(bool,int,int)">scalar</see>
/// <c>condition ? whenTrue : whenFalse</c> and the <see cref="Select(System.ReadOnlySpan{bool},System.ReadOnlySpan{int},System.ReadOnlySpan{int},System.Span{int})">bulk</see>
/// per-element blend that compile to straight-line data-flow with <strong>no conditional jump</strong> (issue #198).
/// </summary>
/// <remarks>
/// <para>
/// The JIT already lowers the recognised <c>cmov</c> idioms — <see cref="Math.Min(int,int)"/> /
/// <see cref="Math.Max(int,int)"/> / <see cref="Math.Abs(int)"/> / <see cref="Math.Clamp(int,int,int)"/> — to
/// branchless instructions, so <strong>use those</strong> when they fit. What it does <em>not</em> reliably do is
/// "if-convert" a general data-dependent <c>condition ? a : b</c>: in a loop over an unpredictable
/// <c>bool</c>, RyuJIT emits a real branch, and on data the CPU cannot predict the mispredict penalty dominates
/// the loop. The #198 spike measured a tight blend over a 1,000,000-element array with a 50/50
/// <strong>unpredictable</strong> condition at <strong>~6× faster branch-free than the branchy ternary</strong>
/// — the textbook misprediction signature — which is why this primitive ships.
/// </para>
/// <para>
/// The mechanism is the standard mask trick: a <c>bool</c> is reinterpreted to its <c>0</c>/<c>1</c> byte and
/// negated to an all-zero / all-one mask, then <c>whenFalse ^ ((whenTrue ^ whenFalse) &amp; mask)</c> picks a
/// value with pure arithmetic — no comparison, no jump, a fixed data dependency the CPU never mispredicts.
/// Floating-point overloads reinterpret the operands to their integer bit-patterns, select, and reinterpret back,
/// so the chosen <see cref="float"/> / <see cref="double"/> is returned bit-exactly (including signed zero and
/// <c>NaN</c> payloads). The bulk span overloads apply the same straight-line arithmetic per element; because the
/// body has no branch, the JIT auto-vectorises it, so the blend also wins at array scale.
/// </para>
/// <para>
/// <strong>Scope and caveats.</strong> This is for the <em>unpredictable-condition</em> case — when the branch is
/// well-predicted (a loop-invariant flag, a sorted threshold), the predicted branch is essentially free and a
/// plain ternary is just as fast or faster, so reach for branchless select only when the condition is genuinely
/// data-dependent and random. The scalar overloads are <see cref="MethodImplOptions.AggressiveInlining">aggressively
/// inlined</see> so they stay branch-free at the call site. Every overload is allocation-free and AOT-safe.
/// <c>condition</c> must be an ordinary <see cref="bool"/> (value <c>0</c> or <c>1</c>, as produced by every C#
/// comparison and the runtime); a <c>bool</c> forged from an out-of-range byte via unsafe reinterpretation is
/// outside the contract.
/// </para>
/// </remarks>
public static class Branchless
{
    // ── Scalar conditional select ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <paramref name="whenTrue"/> if <paramref name="condition"/> is <see langword="true"/>, otherwise
    /// <paramref name="whenFalse"/> — without a conditional branch.
    /// </summary>
    /// <param name="condition">The selector. Must be an ordinary <see cref="bool"/> (see the type remarks).</param>
    /// <param name="whenTrue">The value returned when <paramref name="condition"/> is <see langword="true"/>.</param>
    /// <param name="whenFalse">The value returned when <paramref name="condition"/> is <see langword="false"/>.</param>
    /// <returns>The selected value, equal to <c><paramref name="condition"/> ? <paramref name="whenTrue"/> : <paramref name="whenFalse"/></c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Select(bool condition, int whenTrue, int whenFalse)
    {
        int mask = Mask(condition);
        return whenFalse ^ ((whenTrue ^ whenFalse) & mask);
    }

    /// <inheritdoc cref="Select(bool,int,int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Select(bool condition, long whenTrue, long whenFalse)
    {
        long mask = MaskLong(condition);
        return whenFalse ^ ((whenTrue ^ whenFalse) & mask);
    }

    /// <inheritdoc cref="Select(bool,int,int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Select(bool condition, uint whenTrue, uint whenFalse)
    {
        uint mask = (uint)Mask(condition);
        return whenFalse ^ ((whenTrue ^ whenFalse) & mask);
    }

    /// <inheritdoc cref="Select(bool,int,int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Select(bool condition, ulong whenTrue, ulong whenFalse)
    {
        ulong mask = (ulong)MaskLong(condition);
        return whenFalse ^ ((whenTrue ^ whenFalse) & mask);
    }

    /// <inheritdoc cref="Select(bool,int,int)"/>
    /// <remarks>
    /// The operands are reinterpreted to their 32-bit IEEE-754 patterns, selected, and reinterpreted back, so the
    /// chosen value is returned bit-exactly — signed zero and any <c>NaN</c> payload are preserved verbatim.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Select(bool condition, float whenTrue, float whenFalse)
    {
        int bits = Select(condition,
            BitConverter.SingleToInt32Bits(whenTrue),
            BitConverter.SingleToInt32Bits(whenFalse));
        return BitConverter.Int32BitsToSingle(bits);
    }

    /// <inheritdoc cref="Select(bool,float,float)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Select(bool condition, double whenTrue, double whenFalse)
    {
        long bits = Select(condition,
            BitConverter.DoubleToInt64Bits(whenTrue),
            BitConverter.DoubleToInt64Bits(whenFalse));
        return BitConverter.Int64BitsToDouble(bits);
    }

    // ── Bulk per-element blend ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes <c><paramref name="condition"/>[i] ? <paramref name="whenTrue"/>[i] : <paramref name="whenFalse"/>[i]</c>
    /// into <paramref name="destination"/> for every element — a branch-free bulk blend with no BCL equivalent
    /// (<c>TensorPrimitives</c> has no conditional select), so its tight body auto-vectorises instead of
    /// mispredicting a per-element branch.
    /// </summary>
    /// <param name="condition">The per-element selectors.</param>
    /// <param name="whenTrue">The values chosen where the matching condition is <see langword="true"/>.</param>
    /// <param name="whenFalse">The values chosen where the matching condition is <see langword="false"/>.</param>
    /// <param name="destination">The output. May alias <paramref name="whenTrue"/> or <paramref name="whenFalse"/>.</param>
    /// <exception cref="ArgumentException">The spans do not all have the same length.</exception>
    public static void Select(ReadOnlySpan<bool> condition, ReadOnlySpan<int> whenTrue, ReadOnlySpan<int> whenFalse, Span<int> destination)
    {
        ValidateLengths(condition.Length, whenTrue.Length, whenFalse.Length, destination.Length);
        for (int i = 0; i < condition.Length; i++)
        {
            int mask = Mask(condition[i]);
            destination[i] = whenFalse[i] ^ ((whenTrue[i] ^ whenFalse[i]) & mask);
        }
    }

    /// <inheritdoc cref="Select(System.ReadOnlySpan{bool},System.ReadOnlySpan{int},System.ReadOnlySpan{int},System.Span{int})"/>
    public static void Select(ReadOnlySpan<bool> condition, ReadOnlySpan<long> whenTrue, ReadOnlySpan<long> whenFalse, Span<long> destination)
    {
        ValidateLengths(condition.Length, whenTrue.Length, whenFalse.Length, destination.Length);
        for (int i = 0; i < condition.Length; i++)
        {
            long mask = MaskLong(condition[i]);
            destination[i] = whenFalse[i] ^ ((whenTrue[i] ^ whenFalse[i]) & mask);
        }
    }

    /// <inheritdoc cref="Select(System.ReadOnlySpan{bool},System.ReadOnlySpan{int},System.ReadOnlySpan{int},System.Span{int})"/>
    public static void Select(ReadOnlySpan<bool> condition, ReadOnlySpan<float> whenTrue, ReadOnlySpan<float> whenFalse, Span<float> destination)
    {
        ValidateLengths(condition.Length, whenTrue.Length, whenFalse.Length, destination.Length);
        for (int i = 0; i < condition.Length; i++)
            destination[i] = Select(condition[i], whenTrue[i], whenFalse[i]);
    }

    /// <inheritdoc cref="Select(System.ReadOnlySpan{bool},System.ReadOnlySpan{int},System.ReadOnlySpan{int},System.Span{int})"/>
    public static void Select(ReadOnlySpan<bool> condition, ReadOnlySpan<double> whenTrue, ReadOnlySpan<double> whenFalse, Span<double> destination)
    {
        ValidateLengths(condition.Length, whenTrue.Length, whenFalse.Length, destination.Length);
        for (int i = 0; i < condition.Length; i++)
            destination[i] = Select(condition[i], whenTrue[i], whenFalse[i]);
    }

    // ── Mask helpers ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Turns a <c>bool</c> into a full-width 32-bit mask: <c>-1</c> (all ones) for <see langword="true"/>,
    /// <c>0</c> for <see langword="false"/>, with no branch — the bool's underlying <c>0</c>/<c>1</c> byte negated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Mask(bool condition) => -(int)Unsafe.As<bool, byte>(ref condition);

    /// <inheritdoc cref="Mask(bool)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long MaskLong(bool condition) => -(long)Unsafe.As<bool, byte>(ref condition);

    private static void ValidateLengths(int condition, int whenTrue, int whenFalse, int destination)
    {
        if (whenTrue != condition || whenFalse != condition || destination != condition)
            throw new ArgumentException(
                "condition, whenTrue, whenFalse, and destination must all have the same length.");
    }
}
