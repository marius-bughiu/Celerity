namespace Celerity.Collections;

/// <summary>
/// An opaque, stable key to one value in a <see cref="SlotMap{TValue}"/>, issued by
/// <see cref="SlotMap{TValue}.Add"/> &#8212; what makes every lookup and removal a direct array index
/// rather than a hash and a probe.
/// </summary>
/// <remarks>
/// <para>
/// A key stays valid, addressing the same value, for as long as that value is in the map &#8212; through any
/// number of other values being added and removed around it. Removing the value, or clearing the map,
/// retires the key: the map then reports it absent rather than silently addressing whatever value later
/// reused its slot, because a slot carries a version that is stepped every time it is filled and every time
/// it is vacated.
/// </para>
/// <para>
/// <b>A key belongs to the map that issued it.</b> Passing one to a different <see cref="SlotMap{TValue}"/>
/// is a programming error the type cannot detect &#8212; the versions are per-map, so a key from one map may
/// well match a live value in another and address it. Keep keys with their map, as one would with an index
/// into an array.
/// </para>
/// <para>
/// The <c>default</c> key refers to nothing and is reported absent by every map, so a field that has not been
/// assigned yet reads as missing rather than addressing the first value. That guarantee is absolute: a slot's
/// version is odd exactly while it holds a value, so no live value can ever wear the version <c>0</c> the
/// <c>default</c> key carries.
/// </para>
/// <para>
/// The one limitation a fixed-width version cannot escape, stated rather than left to be discovered: a slot's
/// version steps twice per use (once when filled, once when vacated) and wraps at 32 bits, so the odd
/// versions repeat after <b>2,147,483,648</b> reuses of the <i>same</i> slot, and a key retired exactly that
/// long ago starts resolving again. Every generational slot map has a ceiling of this kind; reaching this one
/// takes two billion removals whose slots all land back on the one.
/// </para>
/// </remarks>
public readonly struct SlotKey : IEquatable<SlotKey>
{
    internal SlotKey(int index, uint version)
    {
        Index = index;
        Version = version;
    }

    // A live slot's version is always odd, so the default key — index 0, version 0 — can never resolve to
    // one.
    internal int Index { get; }

    internal uint Version { get; }

    /// <summary>Determines whether two keys carry the same slot and version.</summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    /// <returns><c>true</c> when the keys are equal; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Equality is on the token, not on identity: a key carries no map, so two equal keys refer to the same
    /// value only when they came from the same map. Comparing keys issued by different maps is meaningless,
    /// and can compare equal.
    /// </remarks>
    public static bool operator ==(SlotKey left, SlotKey right) => left.Equals(right);

    /// <summary>Determines whether two keys carry a different slot or version.</summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    /// <returns><c>true</c> when the keys differ; otherwise <c>false</c>.</returns>
    public static bool operator !=(SlotKey left, SlotKey right) => !left.Equals(right);

    /// <summary>Determines whether this key carries the same slot and version as <paramref name="other"/>.</summary>
    /// <param name="other">The key to compare against.</param>
    /// <returns><c>true</c> when both the slot and its version match; otherwise <c>false</c>.</returns>
    /// <remarks>Token equality, with the cross-map caveat described on <see cref="op_Equality"/>.</remarks>
    public bool Equals(SlotKey other) => Index == other.Index && Version == other.Version;

    /// <summary>Determines whether <paramref name="obj"/> is a key carrying the same slot and version.</summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><c>true</c> when it is an equal <see cref="SlotKey"/>; otherwise <c>false</c>.</returns>
    public override bool Equals(object? obj) => obj is SlotKey other && Equals(other);

    /// <summary>Returns a hash code for the key.</summary>
    /// <returns>A hash code combining the slot and its version.</returns>
    public override int GetHashCode() => HashCode.Combine(Index, Version);

    /// <summary>Returns a readable rendering of the key, for debugging.</summary>
    /// <returns>A string of the form <c>#index.version</c>.</returns>
    public override string ToString() => $"#{Index}.{Version}";
}
