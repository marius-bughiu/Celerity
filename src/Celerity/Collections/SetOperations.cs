namespace Celerity.Collections;

/// <summary>
/// Shared implementation of the <see cref="ISet{T}"/> set-algebra surface for the
/// mutable Celerity set family (<see cref="CeleritySet{T, THasher}"/>,
/// <see cref="SwissSet{T, THasher}"/>, <see cref="IntSet{THasher}"/>,
/// <see cref="LongSet{THasher}"/>). Every set in the family exposes the same
/// primitives — <c>Count</c>, <c>Contains</c>, <c>Add</c> (add-if-absent),
/// <c>Remove</c>, <c>Clear</c>, and enumeration — so the ten set-algebra operations
/// are written once here against those primitives rather than duplicated per type.
/// </summary>
/// <remarks>
/// <para>
/// The semantics match BCL <see cref="HashSet{T}"/> exactly, including
/// duplicate-tolerant <c>other</c> sequences (materialized to a distinct
/// <see cref="HashSet{T}"/> once, so a repeated element is processed at most once),
/// self-aliasing (<c>other</c> being the same instance as the set), and the
/// out-of-band <c>default(T)</c>/zero element each set handles specially — all of
/// which are covered by the concrete set's own <c>Contains</c>/<c>Add</c>/<c>Remove</c>.
/// </para>
/// <para>
/// The subset / equality shapes need the distinct count of <c>other</c>,
/// so they materialize it once into an ordinal <see cref="HashSet{T}"/> keyed by
/// <see cref="EqualityComparer{T}.Default"/> — the same equality every Celerity set
/// uses — exactly what BCL set types do internally. The superset / overlap shapes
/// stream <c>other</c> directly against the set's O(1) membership test.
/// </para>
/// </remarks>
internal static class SetOperations
{
    // ── Mutating operations ───────────────────────────────────────────────────

    internal static void UnionWith<T>(ISet<T> self, IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(self, other))
            return; // union with itself is a no-op

        foreach (T item in other)
            self.Add(item);
    }

    internal static void ExceptWith<T>(ISet<T> self, IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (self.Count == 0)
            return;
        if (ReferenceEquals(self, other))
        {
            self.Clear();
            return;
        }

        foreach (T item in other)
            self.Remove(item);
    }

    internal static void IntersectWith<T>(ISet<T> self, IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (self.Count == 0 || ReferenceEquals(self, other))
            return;

        // An empty right-hand side intersects to nothing.
        if (other is ICollection<T> collection && collection.Count == 0)
        {
            self.Clear();
            return;
        }

        HashSet<T> o = MaterializeDistinct(other);

        // Snapshot the current elements before mutating: removing while enumerating
        // the live set would invalidate the enumerator. Drop every element that is
        // not also in `other`.
        List<T> snapshot = new(self);
        foreach (T item in snapshot)
        {
            if (!o.Contains(item))
                self.Remove(item);
        }
    }

    internal static void SymmetricExceptWith<T>(ISet<T> self, IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(self, other))
        {
            self.Clear(); // every element is in both, so the symmetric difference is empty
            return;
        }

        // Toggle each distinct element of `other`: present → remove, absent → add.
        // Materializing to a distinct set first ensures a duplicated element toggles
        // at most once (matching BCL semantics).
        HashSet<T> o = MaterializeDistinct(other);
        foreach (T item in o)
        {
            if (!self.Remove(item))
                self.Add(item);
        }
    }

    // ── Query operations ──────────────────────────────────────────────────────

    internal static bool IsSubsetOf<T>(ISet<T> self, IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (self.Count == 0)
            return true; // the empty set is a subset of everything

        HashSet<T> o = MaterializeDistinct(other);
        if (self.Count > o.Count)
            return false;
        return AllElementsIn(self, o);
    }

    internal static bool IsProperSubsetOf<T>(ISet<T> self, IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        HashSet<T> o = MaterializeDistinct(other);
        if (self.Count >= o.Count)
            return false;
        return AllElementsIn(self, o);
    }

    internal static bool IsSupersetOf<T>(ISet<T> self, IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (T item in other)
        {
            if (!self.Contains(item))
                return false;
        }
        return true;
    }

    internal static bool IsProperSupersetOf<T>(ISet<T> self, IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // An empty right-hand side: a proper superset iff the set is non-empty.
        if (other is ICollection<T> collection && collection.Count == 0)
            return self.Count > 0;

        HashSet<T> o = MaterializeDistinct(other);
        if (self.Count <= o.Count)
            return false;
        foreach (T item in o)
        {
            if (!self.Contains(item))
                return false;
        }
        return true;
    }

    internal static bool Overlaps<T>(ISet<T> self, IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (self.Count == 0)
            return false;

        foreach (T item in other)
        {
            if (self.Contains(item))
                return true;
        }
        return false;
    }

    internal static bool SetEquals<T>(ISet<T> self, IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        HashSet<T> o = MaterializeDistinct(other);
        if (o.Count != self.Count)
            return false;
        return AllElementsIn(self, o);
    }

    // ── ICollection<T>.CopyTo ─────────────────────────────────────────────────

    // Copies the `count` elements of `source` into `array` starting at
    // `arrayIndex`, matching HashSet<T>.CopyTo's argument validation and exceptions.
    internal static void CopyTo<T>(IEnumerable<T> source, int count, T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Array index must be non-negative.");
        if (arrayIndex > array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Array index is beyond the end of the destination array.");
        if (array.Length - arrayIndex < count)
            throw new ArgumentException("The destination array has insufficient space to copy the set's elements.", nameof(array));

        int i = arrayIndex;
        foreach (T item in source)
            array[i++] = item;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Materializes `other` into a HashSet of its distinct elements keyed by the same
    // default equality the Celerity sets use. HashSet<T> tolerates a null / default
    // element, so the out-of-band default is captured here too.
    private static HashSet<T> MaterializeDistinct<T>(IEnumerable<T> other) =>
        new HashSet<T>(other, EqualityComparer<T>.Default);

    // Returns true iff every element of `self` is contained in `o`.
    private static bool AllElementsIn<T>(ISet<T> self, HashSet<T> o)
    {
        foreach (T item in self)
        {
            if (!o.Contains(item))
                return false;
        }
        return true;
    }
}
