using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="PersistentSortedSet{T, TComparer}"/> against a
/// <see cref="SortedSet{T}"/> oracle driven through the same operation sequence.
///
/// <para>
/// As in <see cref="PersistentSortedMapDifferentialTests"/>, <b>persistence is the thing on trial</b>: a set
/// that path-copies one node too few still agrees with the oracle on the set you are holding, and the damage
/// lands on a set handed out earlier. So every run keeps snapshots — a set and an independent copy of the oracle
/// — and re-checks all of them after the last operation, element by element, by every ordered query, and by the
/// tree's shape. The comparer is an axis (ascending and descending), and the builder replays the same run.
/// </para>
///
/// <para>
/// <b>The set algebra is reconciled too.</b> Each of the four producing operations and the six queries is run
/// against <see cref="SortedSet{T}"/>'s in-place counterpart on random operands, including operands with
/// duplicates and operands that are not a set at all.
/// </para>
/// </summary>
public class PersistentSortedSetDifferentialTests
{
    internal readonly struct Descending : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }

    private sealed class DescendingOracle : IComparer<int>
    {
        public static readonly DescendingOracle Instance = new();

        public int Compare(int x, int y) => y.CompareTo(x);
    }

    // Op count, element space, removal weight (percent of ops that remove) and a seed.
    private static readonly Gen<(int Ops, int Space, int RemoveWeight, uint Seed)> GenRuns =
        Gen.Select(Gen.Int[0, 3_000], Gen.Int[1, 4_000], Gen.Int[0, 60], Gen.UInt);

    // Two operands drawn from one element space, so they overlap by a varying amount — and the right-hand one
    // carries duplicates, which the algebra must collapse.
    private static readonly Gen<(int[] Left, int[] Right)> GenOperands =
        Gen.Int[0, 1_500].SelectMany(space =>
            Gen.Select(Gen.Int[0, space].Array[0, 2_000], Gen.Int[0, space].Array[0, 2_000]));

    [Fact]
    public void EveryOperation_ShouldMatchTheSortedSetOracle_UnderGeneratedRuns()
    {
        GenRuns.Sample(
            run =>
            {
                AssertAgreesWithOracle<DefaultComparer<int>>(run, Comparer<int>.Default);
                AssertAgreesWithOracle<Descending>(run, DescendingOracle.Instance);
            },
            iter: 60);
    }

    [Fact]
    public void ABuilderShouldMatchTheSortedSetOracle_UnderGeneratedRuns()
    {
        GenRuns.Sample(
            run =>
            {
                AssertBuilderAgreesWithOracle<DefaultComparer<int>>(run, Comparer<int>.Default);
                AssertBuilderAgreesWithOracle<Descending>(run, DescendingOracle.Instance);
            },
            iter: 60);
    }

    [Fact]
    public void SetAlgebra_ShouldMatchTheSortedSetOracle_UnderGeneratedOperands()
    {
        GenOperands.Sample(
            operands =>
            {
                var left = new PersistentSortedSet<int, DefaultComparer<int>>(operands.Left);
                var leftSnapshot = left.ToArray();

                AssertProduces(operands, left, left.Union(operands.Right), (s, o) => s.UnionWith(o));
                AssertProduces(operands, left, left.Except(operands.Right), (s, o) => s.ExceptWith(o));
                AssertProduces(operands, left, left.Intersect(operands.Right), (s, o) => s.IntersectWith(o));
                AssertProduces(operands, left, left.SymmetricExcept(operands.Right), (s, o) => s.SymmetricExceptWith(o));

                // The queries are reconciled against HashSet<int>, not SortedSet<int>: on .NET 10,
                // SortedSet<T>.IsSubsetOf answers true for some operands that hold duplicates even though an
                // element of the set is missing from them (found by this test; brute force agrees with HashSet).
                // For int, default equality and DefaultComparer agree, so HashSet is an exact oracle here.
                var oracle = new HashSet<int>(operands.Left);
                Assert.Equal(oracle.SetEquals(operands.Right), left.SetEquals(operands.Right));
                Assert.Equal(oracle.IsSubsetOf(operands.Right), left.IsSubsetOf(operands.Right));
                Assert.Equal(oracle.IsProperSubsetOf(operands.Right), left.IsProperSubsetOf(operands.Right));
                Assert.Equal(oracle.IsSupersetOf(operands.Right), left.IsSupersetOf(operands.Right));
                Assert.Equal(oracle.IsProperSupersetOf(operands.Right), left.IsProperSupersetOf(operands.Right));
                Assert.Equal(oracle.Overlaps(operands.Right), left.Overlaps(operands.Right));

                // The receiver is a value: none of the ten operations may have changed it.
                Assert.Equal(leftSnapshot, left);
            },
            iter: 200);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(528)]
    [InlineData(529)]
    [InlineData(1_024)]
    [InlineData(1_025)]
    [InlineData(20_000)]
    public void FillThenDrain_ShouldMatchTheOracle_AtEveryElementCount(int count)
    {
        AssertFillThenDrain(count, ascendingFill: true, frontDrain: true);
        AssertFillThenDrain(count, ascendingFill: false, frontDrain: false);
        AssertFillThenDrain(count, ascendingFill: true, frontDrain: false);
    }

    private static void AssertProduces(
        (int[] Left, int[] Right) operands,
        PersistentSortedSet<int, DefaultComparer<int>> receiver,
        PersistentSortedSet<int, DefaultComparer<int>> result,
        Action<SortedSet<int>, int[]> apply)
    {
        var oracle = new SortedSet<int>(operands.Left);
        apply(oracle, operands.Right);

        Assert.Equal(oracle, result);
        Assert.Equal(oracle.Count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(result));

        // An operation that changed nothing hands back the receiver rather than an equal copy.
        if (oracle.SetEquals(receiver))
            Assert.Same(receiver, result);
    }

    // A run of Add / Remove against a SortedSet oracle, with snapshots re-checked at the end.
    private static void AssertAgreesWithOracle<TComparer>(
        (int Ops, int Space, int RemoveWeight, uint Seed) run, IComparer<int> order)
        where TComparer : struct, IComparer<int>
    {
        var rng = new Random((int)run.Seed);
        PersistentSortedSet<int, TComparer> set = PersistentSortedSet<int, TComparer>.Empty;
        var oracle = new SortedSet<int>(order);
        var snapshots = new List<(PersistentSortedSet<int, TComparer> Set, SortedSet<int> Oracle)>();

        for (int step = 0; step < run.Ops; step++)
        {
            int item = rng.Next(run.Space);
            PersistentSortedSet<int, TComparer> before = set;

            if (rng.Next(100) < run.RemoveWeight)
            {
                set = set.Remove(item);
                if (!oracle.Remove(item))
                    Assert.Same(before, set);
            }
            else
            {
                set = set.Add(item);
                if (!oracle.Add(item))
                    Assert.Same(before, set);
            }

            Assert.Equal(oracle.Count, set.Count);

            if (step % 97 == 0)
                snapshots.Add((set, new SortedSet<int>(oracle, order)));
        }

        snapshots.Add((set, new SortedSet<int>(oracle, order)));

        foreach ((PersistentSortedSet<int, TComparer> snapshot, SortedSet<int> expected) in snapshots)
            AssertSameElements(expected, snapshot, run.Space, rng);
    }

    // The same run driven through a builder, snapshotting with ToImmutable so the transient path's isolation is
    // on trial alongside its arithmetic.
    private static void AssertBuilderAgreesWithOracle<TComparer>(
        (int Ops, int Space, int RemoveWeight, uint Seed) run, IComparer<int> order)
        where TComparer : struct, IComparer<int>
    {
        var rng = new Random((int)run.Seed);
        PersistentSortedSet<int, TComparer>.Builder builder = new();
        var oracle = new SortedSet<int>(order);
        var snapshots = new List<(PersistentSortedSet<int, TComparer> Set, SortedSet<int> Oracle)>();

        for (int step = 0; step < run.Ops; step++)
        {
            int item = rng.Next(run.Space);

            if (rng.Next(100) < run.RemoveWeight)
                Assert.Equal(oracle.Remove(item), builder.Remove(item));
            else
                Assert.Equal(oracle.Add(item), builder.Add(item));

            Assert.Equal(oracle.Count, builder.Count);

            if (step % 97 == 0)
                snapshots.Add((builder.ToImmutable(), new SortedSet<int>(oracle, order)));
        }

        snapshots.Add((builder.ToImmutable(), new SortedSet<int>(oracle, order)));

        foreach ((PersistentSortedSet<int, TComparer> snapshot, SortedSet<int> expected) in snapshots)
            AssertSameElements(expected, snapshot, run.Space, rng);
    }

    // Ascending fill then front drain hits the leftmost path's splits and merges; descending fill then back drain
    // the rightmost; ascending fill then back drain leaves the left neighbour to borrow from.
    private static void AssertFillThenDrain(int count, bool ascendingFill, bool frontDrain)
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = PersistentSortedSet<int, DefaultComparer<int>>.Empty;
        var snapshots = new List<(PersistentSortedSet<int, DefaultComparer<int>> Set, int Present)>();

        for (int i = 0; i < count; i++)
        {
            set = set.Add(ascendingFill ? i : count - 1 - i);
            if (i % 97 == 0)
                snapshots.Add((set, i + 1));
        }

        Assert.Equal(count, set.Count);
        Assert.Equal(count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(set));

        PersistentSortedSet<int, DefaultComparer<int>> draining = set;
        for (int i = 0; i < count; i++)
        {
            draining = draining.Remove(frontDrain ? i : count - 1 - i);
            Assert.Equal(count - 1 - i, draining.Count);

            if (i % 211 == 0)
                Assert.Equal(draining.Count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(draining));
        }

        Assert.Same(PersistentSortedSet<int, DefaultComparer<int>>.Empty, draining);

        // The fill snapshots must be untouched by the drain that walked through their storage.
        foreach ((PersistentSortedSet<int, DefaultComparer<int>> snapshot, int present) in snapshots)
        {
            int first = ascendingFill ? 0 : count - present;
            Assert.Equal(Enumerable.Range(first, present), snapshot);
            Assert.Equal(present, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(snapshot));
        }
    }

    private static void AssertSameElements<TComparer>(
        SortedSet<int> expected, PersistentSortedSet<int, TComparer> actual, int space, Random rng)
        where TComparer : struct, IComparer<int>
    {
        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(expected.Count, PersistentSortedMapShape.AssertValid<int, TComparer>(actual));

        // Enumeration is checked in order, which is the claim a sorted set makes beyond membership.
        Assert.Equal(expected, actual);

        foreach (int item in expected)
            Assert.True(actual.Contains(item));

        if (expected.Count == 0)
        {
            Assert.False(actual.TryGetMin(out _));
            Assert.False(actual.TryGetMax(out _));
        }
        else
        {
            Assert.Equal(expected.Min, actual.Min);
            Assert.Equal(expected.Max, actual.Max);
        }

        // The bound queries and a range at probes spread over (and just past) the element space.
        int[] sorted = expected.ToArray();
        IComparer<int> order = expected.Comparer;
        for (int probe = 0; probe < 24; probe++)
        {
            int item = rng.Next(-1, space + 1);
            Assert.Equal(actual.Contains(item), expected.Contains(item));
            AssertBound(sorted, order, item, strict: false, actual.TryGetLowerBound(item, out int lower), lower);
            AssertBound(sorted, order, item, strict: true, actual.TryGetUpperBound(item, out int upper), upper);
            AssertBackwardBound(sorted, order, item, strict: false, actual.TryGetFloor(item, out int floor), floor);
            AssertBackwardBound(sorted, order, item, strict: true, actual.TryGetPredecessor(item, out int predecessor), predecessor);

            int other = rng.Next(-1, space + 1);
            (int from, int to) = order.Compare(item, other) <= 0 ? (item, other) : (other, item);
            int[] inRange = sorted.Where(k => order.Compare(k, from) >= 0 && order.Compare(k, to) < 0).ToArray();
            Assert.Equal(inRange, actual.EnumerateRange(from, to));
        }
    }

    private static void AssertBackwardBound(int[] sorted, IComparer<int> order, int item, bool strict, bool found, int result)
    {
        int index = Array.FindLastIndex(sorted, k => strict ? order.Compare(k, item) < 0 : order.Compare(k, item) <= 0);
        Assert.Equal(index >= 0, found);
        if (index >= 0)
            Assert.Equal(sorted[index], result);
    }

    private static void AssertBound(int[] sorted, IComparer<int> order, int item, bool strict, bool found, int result)
    {
        int index = Array.FindIndex(sorted, k => strict ? order.Compare(k, item) > 0 : order.Compare(k, item) >= 0);
        Assert.Equal(index >= 0, found);
        if (index >= 0)
            Assert.Equal(sorted[index], result);
    }
}
