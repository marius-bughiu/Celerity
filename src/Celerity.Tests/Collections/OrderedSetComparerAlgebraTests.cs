using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Cross-collection coverage of the <see cref="ISet{T}"/> algebra on the two <b>comparer-ordered</b> sets,
/// <see cref="BTreeSet{T, TComparer}"/> and <see cref="RankedSet{T, TComparer}"/>. Both define membership as
/// "the comparer orders the two elements equal", so a comparer that collapses values
/// <see cref="EqualityComparer{T}.Default"/> keeps apart — a case-insensitive order, say — is the only way to
/// observe which side of each operation is keyed by what. The oracle throughout is
/// <see cref="SortedSet{T}"/> built on the equivalent <see cref="IComparer{T}"/>, which is the type these two
/// replace.
/// </summary>
/// <remarks>
/// This file is the regression suite for the two members that used to answer differently from
/// <see cref="SortedSet{T}"/>: <c>SymmetricExceptWith</c> de-duplicated <c>other</c> under default equality
/// and so toggled a collapsed pair twice, and <c>IsProperSupersetOf</c> compared cardinalities against that
/// same default-equality-distinct count. Neither ever probes <c>other</c>, so neither had any reason to be
/// keyed by anything but the comparer.
/// </remarks>
public class OrderedSetComparerAlgebraTests
{
    /// <summary>Orders strings case-insensitively, so the order disagrees with default equality.</summary>
    private readonly struct CaseInsensitiveOrdinal : IComparer<string>
    {
        public int Compare(string? x, string? y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Orders ints by their tens bucket, so 10..19 are one element and 20..29 another.</summary>
    private readonly struct ByTens : IComparer<int>
    {
        public int Compare(int x, int y) => (x / 10).CompareTo(y / 10);
    }

    private static ISet<string>[] CaseInsensitiveSets(params string[] seed) =>
    [
        new BTreeSet<string, CaseInsensitiveOrdinal>(seed, default),
        new RankedSet<string, CaseInsensitiveOrdinal>(seed, default),
    ];

    private static SortedSet<string> Oracle(params string[] seed) =>
        new(seed, StringComparer.OrdinalIgnoreCase);

    // ── The two regressions ───────────────────────────────────────────────────

    [Fact]
    public void SymmetricExceptWith_ShouldToggleACollapsedPairOnce_NotTwice()
    {
        // ["a", "A"] is one element to a case-insensitive order, so toggling it against a set that already
        // holds it must leave the set empty. Keying the de-duplication by EqualityComparer<string>.Default
        // saw two elements: "a" removed the member and "A" put one straight back.
        SortedSet<string> oracle = Oracle("a");
        oracle.SymmetricExceptWith(["a", "A"]);
        Assert.Empty(oracle);

        foreach (ISet<string> set in CaseInsensitiveSets("a"))
        {
            set.SymmetricExceptWith(["a", "A"]);
            Assert.Empty(set);
        }
    }

    [Fact]
    public void IsProperSupersetOf_ShouldCountTheOtherSideWithTheComparer()
    {
        // {"a", "b"} strictly contains the single element ["a", "A"] denotes. Counting that right-hand side
        // under default equality made it look like two elements, so the cardinality test failed.
        Assert.True(Oracle("a", "b").IsProperSupersetOf(["a", "A"]));

        foreach (ISet<string> set in CaseInsensitiveSets("a", "b"))
            Assert.True(set.IsProperSupersetOf(["a", "A"]));
    }

    // ── The whole table, against the oracle ───────────────────────────────────

    [Fact]
    public void EveryAlgebraMember_ShouldAgreeWithSortedSet_WhenTheComparerCollapses()
    {
        // The full ten-member surface on a set holding "a" against ["A"]: every one of them now answers as
        // SortedSet<string> with StringComparer.OrdinalIgnoreCase does.
        foreach (ISet<string> set in CaseInsensitiveSets("a"))
        {
            Assert.True(set.Contains("A"));

            Assert.True(set.Overlaps(["A"]));
            Assert.True(set.IsSupersetOf(["A"]));
            Assert.False(set.IsProperSupersetOf(["A"]));
            Assert.True(set.SetEquals(["A"]));
            Assert.True(set.IsSubsetOf(["A"]));
            Assert.False(set.IsProperSubsetOf(["A"]));
        }

        SortedSet<string> oracle = Oracle("a");
        Assert.True(oracle.Overlaps(["A"]));
        Assert.True(oracle.IsSupersetOf(["A"]));
        Assert.False(oracle.IsProperSupersetOf(["A"]));
        Assert.True(oracle.SetEquals(["A"]));
        Assert.True(oracle.IsSubsetOf(["A"]));
        Assert.False(oracle.IsProperSubsetOf(["A"]));

        // IntersectWith(["A"]) keeps the member rather than emptying the set.
        foreach (ISet<string> set in CaseInsensitiveSets("a"))
        {
            set.IntersectWith(["A"]);
            Assert.Equal(1, set.Count);
            Assert.True(set.Contains("a"));
        }

        // ExceptWith and UnionWith were already the comparer's, and stay that way.
        foreach (ISet<string> set in CaseInsensitiveSets("a"))
        {
            set.UnionWith(["A"]);
            Assert.Equal(1, set.Count);
            set.ExceptWith(["A"]);
            Assert.Empty(set);
        }
    }

    [Fact]
    public void IsProperSubsetOf_ShouldCountTheOtherSideWithTheComparer()
    {
        // ["a", "A", "b"] is two elements, so {"a"} is a proper subset of it and {"a", "b"} is not.
        Assert.True(Oracle("a").IsProperSubsetOf(["a", "A", "b"]));
        Assert.False(Oracle("a", "b").IsProperSubsetOf(["a", "A", "b"]));

        foreach (ISet<string> set in CaseInsensitiveSets("a"))
            Assert.True(set.IsProperSubsetOf(["a", "A", "b"]));
        foreach (ISet<string> set in CaseInsensitiveSets("a", "b"))
            Assert.False(set.IsProperSubsetOf(["a", "A", "b"]));
    }

    [Fact]
    public void SubsetAndEquality_ShouldRejectAnElementTheOtherSideDoesNotHold()
    {
        // The AllElementsIn probe failing: "b" is in the set and not in `other`.
        foreach (ISet<string> set in CaseInsensitiveSets("a", "b"))
        {
            Assert.False(set.IsSubsetOf(["A", "c"]));
            Assert.False(set.SetEquals(["A", "c"]));
            Assert.False(set.IsProperSubsetOf(["A", "c", "d"]));
            Assert.False(set.IsProperSupersetOf(["A", "c"]));
        }
    }

    // ── Edges: empty, single, self-aliasing, null ─────────────────────────────

    // A sequence with no ICollection<T> fast path, so `other` is materialized element by element.
    private static IEnumerable<string> Streamed(params string[] items)
    {
        foreach (string item in items)
            yield return item;
    }

    [Fact]
    public void AnEmptyOtherSide_ShouldBehaveTheSame_WhetherOrNotItIsACollection()
    {
        foreach (IEnumerable<string> empty in new IEnumerable<string>[] { Array.Empty<string>(), Streamed() })
        {
            foreach (ISet<string> set in CaseInsensitiveSets("a", "b"))
            {
                Assert.False(set.IsSubsetOf(empty));
                Assert.False(set.IsProperSubsetOf(empty));
                Assert.True(set.IsProperSupersetOf(empty));
                Assert.False(set.SetEquals(empty));

                set.SymmetricExceptWith(empty);
                Assert.Equal(2, set.Count);

                set.IntersectWith(empty);
                Assert.Empty(set);
            }

            // An empty set is not a proper superset of an empty right-hand side.
            foreach (ISet<string> set in CaseInsensitiveSets())
            {
                Assert.True(set.IsSubsetOf(empty));
                Assert.False(set.IsProperSupersetOf(empty));
                Assert.True(set.SetEquals(empty));
            }
        }
    }

    [Fact]
    public void ASingleElementOtherSide_ShouldSkipTheSortAndStillAnswer()
    {
        // Length <= 1 returns from MaterializeDistinct before sorting; the answers must not change.
        foreach (ISet<string> set in CaseInsensitiveSets("a"))
        {
            Assert.True(set.SetEquals(Streamed("A")));
            Assert.True(set.IsSubsetOf(Streamed("A")));
            Assert.False(set.IsProperSupersetOf(Streamed("A")));
        }

        foreach (ISet<string> set in CaseInsensitiveSets("a"))
        {
            set.SymmetricExceptWith(Streamed("A"));
            Assert.Empty(set);
        }
    }

    [Fact]
    public void AnEmptySet_ShouldShortCircuitIntersectAndSubset()
    {
        foreach (ISet<string> set in CaseInsensitiveSets())
        {
            set.IntersectWith(["a"]);
            Assert.Empty(set);
            Assert.True(set.IsSubsetOf(["a"]));
            Assert.False(set.IsProperSupersetOf(["a"]));
        }
    }

    [Fact]
    public void SelfAliasing_ShouldKeepTheBclAnswers()
    {
        foreach (ISet<string> set in CaseInsensitiveSets("a", "b"))
        {
            set.IntersectWith(set);
            Assert.Equal(2, set.Count);

            set.SymmetricExceptWith(set);
            Assert.Empty(set);
        }
    }

    [Fact]
    public void EveryMember_ShouldRejectANullOtherSide()
    {
        foreach (ISet<string> set in CaseInsensitiveSets("a"))
        {
            Assert.Throws<ArgumentNullException>(() => set.IntersectWith(null!));
            Assert.Throws<ArgumentNullException>(() => set.SymmetricExceptWith(null!));
            Assert.Throws<ArgumentNullException>(() => set.IsSubsetOf(null!));
            Assert.Throws<ArgumentNullException>(() => set.IsProperSubsetOf(null!));
            Assert.Throws<ArgumentNullException>(() => set.IsProperSupersetOf(null!));
            Assert.Throws<ArgumentNullException>(() => set.SetEquals(null!));
        }
    }

    [Fact]
    public void MaterializingTheOtherSide_ShouldNotReorderTheCallersOwnList()
    {
        // MaterializeDistinct sorts in place, so it must copy first — a caller's List<T> is not scratch space.
        List<string> other = ["c", "a", "b"];
        foreach (ISet<string> set in CaseInsensitiveSets("a"))
        {
            Assert.True(set.IsSubsetOf(other));
            set.IntersectWith(other);
            Assert.Equal<string[]>(["a"], [.. set]);
        }

        Assert.Equal<string[]>(["c", "a", "b"], [.. other]);
    }

    // ── Differential property ─────────────────────────────────────────────────

    private enum Op { IntersectWith, SymmetricExceptWith, UnionWith, ExceptWith }

    private static readonly Gen<(Op op, List<int> other)> GenStep =
        Gen.Select(
            Gen.Int[0, 3].Select(n => (Op)n),
            // A domain three buckets wide, so a generated right-hand side routinely holds two distinct
            // values the comparer collapses into one — which is the case the fix is about.
            Gen.Int[10, 39].List[0, 8]);

    [Fact]
    public void TheMutatingAlgebra_ShouldMatchSortedSet_UnderACollapsingComparer()
    {
        IComparer<int> oracleComparer = Comparer<int>.Create((x, y) => (x / 10).CompareTo(y / 10));

        GenStep.List[0, 30].Sample(steps =>
        {
            var bTree = new BTreeSet<int, ByTens>([10, 20], default);
            var ranked = new RankedSet<int, ByTens>([10, 20], default);
            var oracle = new SortedSet<int>([10, 20], oracleComparer);

            foreach ((Op op, List<int> other) in steps)
            {
                Apply(bTree, op, other);
                Apply(ranked, op, other);
                Apply(oracle, op, other);

                // Compare the equivalence classes rather than the stored representatives: which of two
                // comparer-equal values a set happens to keep is not part of either type's contract.
                int[] expected = [.. oracle.Select(v => v / 10)];
                Assert.Equal<int[]>(expected, [.. bTree.Select(v => v / 10)]);
                Assert.Equal<int[]>(expected, [.. ranked.Select(v => v / 10)]);

                Assert.Equal(oracle.SetEquals(other), bTree.SetEquals(other));
                Assert.Equal(oracle.SetEquals(other), ranked.SetEquals(other));
                Assert.Equal(oracle.IsSubsetOf(other), bTree.IsSubsetOf(other));
                Assert.Equal(oracle.IsSubsetOf(other), ranked.IsSubsetOf(other));
                Assert.Equal(oracle.IsProperSubsetOf(other), bTree.IsProperSubsetOf(other));
                Assert.Equal(oracle.IsProperSubsetOf(other), ranked.IsProperSubsetOf(other));
                Assert.Equal(oracle.IsProperSupersetOf(other), bTree.IsProperSupersetOf(other));
                Assert.Equal(oracle.IsProperSupersetOf(other), ranked.IsProperSupersetOf(other));
                Assert.Equal(oracle.IsSupersetOf(other), bTree.IsSupersetOf(other));
                Assert.Equal(oracle.IsSupersetOf(other), ranked.IsSupersetOf(other));
                Assert.Equal(oracle.Overlaps(other), bTree.Overlaps(other));
                Assert.Equal(oracle.Overlaps(other), ranked.Overlaps(other));
            }
        });
    }

    private static void Apply(ISet<int> set, Op op, List<int> other)
    {
        switch (op)
        {
            case Op.IntersectWith: set.IntersectWith(other); break;
            case Op.SymmetricExceptWith: set.SymmetricExceptWith(other); break;
            case Op.UnionWith: set.UnionWith(other); break;
            default: set.ExceptWith(other); break;
        }
    }
}
