namespace Celerity.Collections;

/// <summary>
/// An opaque, stable reference to one pending timer in a <see cref="TimerWheel{TValue}"/> — what makes
/// <see cref="TimerWheel{TValue}.Cancel"/> constant-time rather than a search.
/// </summary>
/// <remarks>
/// <para>
/// A handle is issued by <see cref="TimerWheel{TValue}.Schedule"/> and stays valid, addressing the same timer,
/// for as long as that timer is pending — through any number of cascades between the wheel's levels, and
/// through other timers being scheduled and cancelled around it. Firing the timer, cancelling it, or clearing
/// the wheel retires the handle: the wheel then rejects it rather than silently addressing whatever timer
/// later reused the storage, because a slot carries a version that is stepped every time it is vacated.
/// </para>
/// <para>
/// <b>A handle belongs to the wheel that issued it.</b> Passing one to a different
/// <see cref="TimerWheel{TValue}"/> is a programming error the type cannot detect — the versions are
/// per-wheel, so a handle from one wheel may well match a live timer in another and cancel the wrong one.
/// Keep handles with their wheel, as one would with an index into an array.
/// </para>
/// <para>
/// The <c>default</c> handle refers to nothing and is rejected by every wheel, so a field that has not been
/// assigned yet fails to cancel rather than cancelling the first timer scheduled. That guarantee is absolute:
/// a slot's version cycles through <c>[1, uint.MaxValue]</c> and never reaches zero, so no pending timer can
/// ever wear the version the <c>default</c> handle carries.
/// </para>
/// <para>
/// The one limitation a fixed-width version cannot escape, stated rather than left to be discovered: the
/// versions cycle through <c>[1, uint.MaxValue]</c>, so they repeat after <b>4,294,967,295</b> vacations of
/// the <i>same</i> slot, and a handle retired exactly that long ago starts resolving again. Every generational
/// slot map has this ceiling; reaching it takes four billion firings whose slots all land back on the one.
/// </para>
/// </remarks>
public readonly struct TimerHandle : IEquatable<TimerHandle>
{
    internal TimerHandle(int index, uint version)
    {
        Index = index;
        Version = version;
    }

    // A pending slot's version is always at least 1, so the default handle — index 0, version 0 — can never
    // resolve to one.
    internal int Index { get; }

    internal uint Version { get; }

    /// <summary>Determines whether two handles carry the same slot and version.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns><c>true</c> when the handles are equal; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Equality is on the token, not on identity: a handle carries no wheel, so two equal handles refer to the
    /// same timer only when they came from the same wheel. Comparing handles issued by different wheels is
    /// meaningless, and can compare equal.
    /// </remarks>
    public static bool operator ==(TimerHandle left, TimerHandle right) => left.Equals(right);

    /// <summary>Determines whether two handles carry a different slot or version.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns><c>true</c> when the handles differ; otherwise <c>false</c>.</returns>
    public static bool operator !=(TimerHandle left, TimerHandle right) => !left.Equals(right);

    /// <summary>Determines whether this handle carries the same slot and version as <paramref name="other"/>.</summary>
    /// <param name="other">The handle to compare against.</param>
    /// <returns><c>true</c> when both the slot and its version match; otherwise <c>false</c>.</returns>
    /// <remarks>Token equality, with the cross-wheel caveat described on <see cref="op_Equality"/>.</remarks>
    public bool Equals(TimerHandle other) => Index == other.Index && Version == other.Version;

    /// <summary>Determines whether <paramref name="obj"/> is a handle carrying the same slot and version.</summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><c>true</c> when it is an equal <see cref="TimerHandle"/>; otherwise <c>false</c>.</returns>
    public override bool Equals(object? obj) => obj is TimerHandle other && Equals(other);

    /// <summary>Returns a hash code for the handle.</summary>
    /// <returns>A hash code combining the slot and its version.</returns>
    public override int GetHashCode() => HashCode.Combine(Index, Version);

    /// <summary>Returns a readable rendering of the handle, for debugging.</summary>
    /// <returns>A string of the form <c>#index.version</c>.</returns>
    public override string ToString() => $"#{Index}.{Version}";
}
