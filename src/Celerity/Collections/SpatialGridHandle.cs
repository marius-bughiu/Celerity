namespace Celerity.Collections;

/// <summary>
/// An opaque, stable reference to one entry in a <see cref="SpatialGrid{TValue}"/> — what makes
/// <see cref="SpatialGrid{TValue}.Move"/> and <see cref="SpatialGrid{TValue}.Remove"/> constant-time rather
/// than a search.
/// </summary>
/// <remarks>
/// <para>
/// A handle is issued by <see cref="SpatialGrid{TValue}.Add"/> and stays valid, addressing the same entry, for
/// as long as that entry is in the grid — through any number of moves, and through other entries being added
/// and removed around it. Removing the entry, or clearing the grid, retires the handle: the grid then rejects
/// it rather than silently addressing whatever entry later reused the storage, because a slot carries a version
/// that is stepped every time it is vacated.
/// </para>
/// <para>
/// <b>A handle belongs to the grid that issued it.</b> Passing one to a different
/// <see cref="SpatialGrid{TValue}"/> is a programming error the type cannot detect — the versions are
/// per-grid, so a handle from one grid may well match a live entry in another and address the wrong point.
/// Keep handles with their grid, as one would with an index into an array.
/// </para>
/// <para>
/// The <c>default</c> handle refers to nothing and is rejected by every grid, so a field that has not been
/// assigned yet fails loudly rather than addressing the first entry.
/// </para>
/// </remarks>
public readonly struct SpatialGridHandle : IEquatable<SpatialGridHandle>
{
    internal SpatialGridHandle(int index, uint version)
    {
        Index = index;
        Version = version;
    }

    // A live slot's version is always at least 1, so the default handle — index 0, version 0 — can never
    // resolve to one.
    internal int Index { get; }

    internal uint Version { get; }

    /// <summary>Determines whether two handles refer to the same entry of the same grid.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns><c>true</c> when the handles are equal; otherwise <c>false</c>.</returns>
    public static bool operator ==(SpatialGridHandle left, SpatialGridHandle right) => left.Equals(right);

    /// <summary>Determines whether two handles refer to different entries.</summary>
    /// <param name="left">The first handle.</param>
    /// <param name="right">The second handle.</param>
    /// <returns><c>true</c> when the handles differ; otherwise <c>false</c>.</returns>
    public static bool operator !=(SpatialGridHandle left, SpatialGridHandle right) => !left.Equals(right);

    /// <summary>Determines whether this handle refers to the same entry as <paramref name="other"/>.</summary>
    /// <param name="other">The handle to compare against.</param>
    /// <returns><c>true</c> when both the slot and its version match; otherwise <c>false</c>.</returns>
    public bool Equals(SpatialGridHandle other) => Index == other.Index && Version == other.Version;

    /// <summary>Determines whether <paramref name="obj"/> is an equal handle.</summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns><c>true</c> when it is a <see cref="SpatialGridHandle"/> referring to the same entry.</returns>
    public override bool Equals(object? obj) => obj is SpatialGridHandle other && Equals(other);

    /// <summary>Returns a hash code for the handle.</summary>
    /// <returns>A hash code combining the slot and its version.</returns>
    public override int GetHashCode() => HashCode.Combine(Index, Version);

    /// <summary>Returns a readable rendering of the handle, for debugging.</summary>
    /// <returns>A string of the form <c>#index.version</c>.</returns>
    public override string ToString() => $"#{Index}.{Version}";
}
