using System.Runtime.InteropServices;

namespace Celerity.Collections;

/// <summary>
/// Shared implementation of the <see cref="ISet{T}"/> set-algebra surface for the <b>comparer-ordered</b>
/// Celerity sets (<see cref="BTreeSet{T, TComparer}"/>, <see cref="RankedSet{T, TComparer}"/>), which define
/// membership as "<c>TComparer</c> orders the two elements equal" rather than as
/// <see cref="EqualityComparer{T}.Default"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SetOperations"/> materializes <c>other</c> into a <see cref="HashSet{T}"/> keyed by
/// <see cref="EqualityComparer{T}.Default"/>. For the hashed family that is exactly faithful — every
/// membership test there is <c>THasher</c> followed by <see cref="EqualityComparer{T}.Default"/> — but for an
/// ordered set whose comparer calls two values equal when the default equality comparer does not (a
/// case-insensitive order, say), it makes the right-hand side disagree with the set about how many distinct
/// elements it holds. This class keys <c>other</c> by the set's own comparer instead, so the whole algebra
/// answers as <see cref="SortedSet{T}"/> with the equivalent <see cref="IComparer{T}"/> does.
/// </para>
/// <para>
/// Only the six members that need to *know something about* <c>other</c> live here. <c>UnionWith</c>,
/// <c>ExceptWith</c>, <c>IsSupersetOf</c> and <c>Overlaps</c> stream <c>other</c> straight against the set's
/// own <c>Add</c> / <c>Remove</c> / <c>Contains</c>, which are already the comparer's, so they keep
/// delegating to <see cref="SetOperations"/> unchanged.
/// </para>
/// <para>
/// The comparer arrives as a <c>struct, IComparer&lt;T&gt;</c> type argument, so the sort, the duplicate
/// collapse and every comparison in the merge devirtualize and inline exactly as they do inside the tree
/// itself.
/// </para>
/// <para>
/// <b>Precondition.</b> <c>self</c> must enumerate in ascending <c>TComparer</c> order. That is part of both
/// ordered sets' public contract, and it is what lets the containment tests merge two sorted sequences
/// instead of binary-searching one per element — so a set that does not promise it must not be routed here.
/// </para>
/// </remarks>
internal static class OrderedSetOperations
{
    // ── Mutating operations ───────────────────────────────────────────────────

    /// <summary>Removes from <paramref name="self"/> every element not also in <paramref name="other"/>.</summary>
    internal static void IntersectWith<T, TComparer>(ISet<T> self, TComparer comparer, IEnumerable<T> other)
        where TComparer : struct, IComparer<T>
    {
        ArgumentNullException.ThrowIfNull(other);
        if (self.Count == 0 || ReferenceEquals(self, other))
            return;

        ReadOnlySpan<T> o = MaterializeDistinct(other, comparer);

        // Snapshot the current elements before mutating: removing while enumerating the live set would
        // invalidate the enumerator. An empty `other` leaves nothing for the merge to match, which clears
        // the set. The snapshot ascends, and so does `o`, so one linear merge decides every element.
        List<T> snapshot = new(self);
        int j = 0;
        foreach (T item in snapshot)
        {
            while (j < o.Length && comparer.Compare(o[j], item) < 0)
                j++;

            if (j < o.Length && comparer.Compare(o[j], item) == 0)
                j++; // kept, and consumed: distinctness means no later element can match this one too
            else
                self.Remove(item);
        }
    }

    /// <summary>Toggles each element of <paramref name="other"/> in <paramref name="self"/>.</summary>
    internal static void SymmetricExceptWith<T, TComparer>(ISet<T> self, TComparer comparer, IEnumerable<T> other)
        where TComparer : struct, IComparer<T>
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(self, other))
        {
            self.Clear(); // every element is in both, so the symmetric difference is empty
            return;
        }

        // Collapsing `other` under the comparer first is what keeps the toggle idempotent per *element*:
        // under a case-insensitive order ["a", "A"] is one element, so it toggles once, not back and forth.
        ReadOnlySpan<T> o = MaterializeDistinct(other, comparer);
        foreach (T item in o)
        {
            if (!self.Remove(item))
                self.Add(item);
        }
    }

    // ── Query operations ──────────────────────────────────────────────────────

    /// <summary>Returns <c>true</c> when every element of <paramref name="self"/> is in <paramref name="other"/>.</summary>
    internal static bool IsSubsetOf<T, TComparer>(ISet<T> self, TComparer comparer, IEnumerable<T> other)
        where TComparer : struct, IComparer<T>
    {
        ArgumentNullException.ThrowIfNull(other);
        if (self.Count == 0)
            return true; // the empty set is a subset of everything

        ReadOnlySpan<T> o = MaterializeDistinct(other, comparer);
        if (self.Count > o.Length)
            return false;
        return AllElementsIn(self, o, comparer);
    }

    /// <summary>Returns <c>true</c> when <paramref name="self"/> is a strict subset of <paramref name="other"/>.</summary>
    internal static bool IsProperSubsetOf<T, TComparer>(ISet<T> self, TComparer comparer, IEnumerable<T> other)
        where TComparer : struct, IComparer<T>
    {
        ArgumentNullException.ThrowIfNull(other);
        ReadOnlySpan<T> o = MaterializeDistinct(other, comparer);
        if (self.Count >= o.Length)
            return false;
        return AllElementsIn(self, o, comparer);
    }

    /// <summary>Returns <c>true</c> when <paramref name="self"/> is a strict superset of <paramref name="other"/>.</summary>
    internal static bool IsProperSupersetOf<T, TComparer>(ISet<T> self, TComparer comparer, IEnumerable<T> other)
        where TComparer : struct, IComparer<T>
    {
        ArgumentNullException.ThrowIfNull(other);

        // An empty right-hand side needs no special case: its distinct count is zero, so a non-empty set
        // falls through to a vacuously true containment loop and an empty one fails `self.Count <= 0`.
        ReadOnlySpan<T> o = MaterializeDistinct(other, comparer);
        if (self.Count <= o.Length)
            return false;

        foreach (T item in o)
        {
            if (!self.Contains(item))
                return false;
        }
        return true;
    }

    /// <summary>Returns <c>true</c> when the two sides hold the same elements.</summary>
    internal static bool SetEquals<T, TComparer>(ISet<T> self, TComparer comparer, IEnumerable<T> other)
        where TComparer : struct, IComparer<T>
    {
        ArgumentNullException.ThrowIfNull(other);
        ReadOnlySpan<T> o = MaterializeDistinct(other, comparer);
        if (o.Length != self.Count)
            return false;
        return AllElementsIn(self, o, comparer);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Materializes `other` into an ascending, comparer-distinct span: a private copy (never the caller's own
    // list — the sort is in place), sorted by TComparer, with each run of comparer-equal elements collapsed
    // to its first member. The returned span is backed by the copy's array, which the span itself keeps
    // alive, so the List<T> local going out of scope here is not a lifetime problem.
    private static ReadOnlySpan<T> MaterializeDistinct<T, TComparer>(IEnumerable<T> other, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        List<T> copy = new(other);
        Span<T> span = CollectionsMarshal.AsSpan(copy);
        if (span.Length <= 1)
            return span; // zero or one element is already sorted and already distinct

        span.Sort(comparer);

        int distinct = 1;
        for (int i = 1; i < span.Length; i++)
        {
            if (comparer.Compare(span[i], span[distinct - 1]) != 0)
                span[distinct++] = span[i];
        }
        return span[..distinct];
    }

    // Returns true iff every element of `self` is present in the sorted, distinct `other`.
    //
    // Both sides ascend under the same comparer — `self` because the ordered sets enumerate in comparer
    // order by contract, `other` because MaterializeDistinct has just sorted it — so this is a linear merge
    // rather than a binary search per element: O(n + m) comparisons instead of O(n log m), and both walks
    // are sequential rather than jumping around a span.
    private static bool AllElementsIn<T, TComparer>(ISet<T> self, ReadOnlySpan<T> other, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        int j = 0;
        foreach (T item in self)
        {
            while (j < other.Length && comparer.Compare(other[j], item) < 0)
                j++;

            if (j == other.Length || comparer.Compare(other[j], item) != 0)
                return false;

            j++; // consumed: both sides are distinct, so nothing later matches this element either
        }
        return true;
    }
}
