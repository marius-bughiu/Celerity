using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// The open-addressed collections mark a vacant slot by leaving it at <c>default(T)</c>. This is the
/// single place that asks "is this slot vacant?", written so the question costs nothing for a
/// reference-type element.
/// </summary>
/// <remarks>
/// <para>
/// The naive spelling is <c>EqualityComparer&lt;T&gt;.Default.Equals(slot, default(T))</c>. For a
/// value-type <c>T</c> the JIT's <see cref="EqualityComparer{T}"/> intrinsic
/// devirtualizes and inlines that, so it is already free. For a <b>reference</b> type it is not: the
/// collection JITs as a <c>__Canon</c>-shared body and the call stays a real interface dispatch — one
/// virtual call per probe iteration, to ask whether a reference is <c>null</c>.
/// </para>
/// <para>
/// <c>typeof(T).IsValueType</c> is a JIT-time constant (<c>__Canon</c> only ever stands in for a
/// reference type), so exactly one arm below is compiled into each instantiation: value types keep the
/// intrinsic comparison, reference types get a plain null test. This mirrors Guiding Principle #2 —
/// the same reason the hashers are structs passed as generic constraints.
/// </para>
/// <para>
/// The substitution is exact, not an approximation. Every <see cref="EqualityComparer{T}"/> the runtime
/// supplies for a reference type answers a <c>null</c> right-hand side structurally, before it consults
/// the element's own <c>Equals</c> — so <c>Equals(x, null)</c> is <c>true</c> exactly when <c>x</c> is
/// <c>null</c>, even for an element type whose <c>Equals</c> claims equality with everything.
/// <c>ReferenceKeyProbeTests</c> pins that.
/// </para>
/// </remarks>
internal static class EmptySlot
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> is <c>default(T)</c> — i.e. the slot is vacant,
    /// or (on the out-of-band default-key paths) the element <i>is</i> the sentinel and cannot be stored
    /// in the table.
    /// </summary>
    /// <typeparam name="T">The stored element or key type.</typeparam>
    /// <param name="value">The slot contents to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Is<T>(T? value)
    {
        if (typeof(T).IsValueType)
        {
            return EqualityComparer<T>.Default.Equals(value, default);
        }

        return value is null;
    }
}
