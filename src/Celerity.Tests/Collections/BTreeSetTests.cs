using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="BTreeSet{T, TComparer}"/>: the constructors, the add / contains /
/// remove core, the ordered surface (<c>Min</c>, <c>Max</c>, the two bounds, <c>EnumerateRange</c>), the
/// <see cref="ISet{T}"/> / <see cref="IReadOnlySet{T}"/> algebra, and the structural corners that only fire
/// once nodes split and merge. Enumeration lives in <see cref="BTreeSetEnumerationTests"/>; the randomized
/// reconciliation against <see cref="SortedSet{T}"/> lives in <see cref="BTreeSetDifferentialTests"/>.
/// </summary>
public class BTreeSetTests
{
    // A stateful struct comparer: proves the type parameter is not assumed to be default-constructed.
    private readonly struct DirectionalComparer : IComparer<int>
    {
        private readonly int _sign;

        public DirectionalComparer(bool ascending) => _sign = ascending ? 1 : -1;

        public int Compare(int x, int y) => _sign * x.CompareTo(y);
    }

    [Fact]
    public void Constructor_ShouldStartEmpty_WhenParameterless()
    {
        var set = new BTreeSet<int>();

        Assert.Equal(0, set.Count);
        Assert.False(set.Contains(0));
        Assert.Empty(set);
    }

    [Fact]
    public void Constructor_ShouldSeedFromSource_AndIgnoreDuplicates()
    {
        var set = new BTreeSet<int>(new[] { 3, 1, 2, 1, 3 });

        Assert.Equal(3, set.Count);
        Assert.Equal(new[] { 1, 2, 3 }, set);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new BTreeSet<int>(null!));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldUseSuppliedComparer_WhenComparerIsStateful()
    {
        var set = new BTreeSet<int, DirectionalComparer>(new DirectionalComparer(ascending: false));

        for (int i = 0; i < 100; i++)
            set.Add(i);

        Assert.Equal(Enumerable.Range(0, 100).Reverse(), set);
        Assert.Equal(99, set.Min);
        Assert.Equal(0, set.Max);
    }

    [Fact]
    public void Constructor_ShouldSeedWithSuppliedComparer()
    {
        var set = new BTreeSet<int, DirectionalComparer>(
            Enumerable.Range(0, 50), new DirectionalComparer(ascending: false));

        Assert.Equal(Enumerable.Range(0, 50).Reverse(), set);
    }

    [Fact]
    public void Add_ShouldThrow_WhenElementAlreadyPresent()
    {
        var set = new BTreeSet<int>();
        set.Add(7);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(7));
        Assert.Equal("item", ex.ParamName);
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenElementAlreadyPresent()
    {
        var set = new BTreeSet<int>();

        Assert.True(set.TryAdd(7));
        Assert.False(set.TryAdd(7));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void ISetAdd_ShouldReportWhetherTheElementWasNew()
    {
        ISet<int> set = new BTreeSet<int>();

        Assert.True(set.Add(1));
        Assert.False(set.Add(1));
        Assert.Equal(1, set.Count);
        Assert.False(set.IsReadOnly);
    }

    [Fact]
    public void ICollectionAdd_ShouldNotThrow_OnDuplicate()
    {
        ICollection<int> set = new BTreeSet<int>();

        set.Add(1);
        set.Add(1);

        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenElementIsAbsentOrSetIsEmpty()
    {
        var set = new BTreeSet<int>();

        Assert.False(set.Remove(1));

        set.Add(1);
        Assert.False(set.Remove(2));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Remove_ShouldEmptyTheTree_AndAllowReuse()
    {
        var set = new BTreeSet<int>();
        set.Add(5);

        Assert.True(set.Remove(5));

        Assert.Equal(0, set.Count);
        Assert.Empty(set);
        Assert.False(set.TryGetMin(out _));

        set.Add(6);
        Assert.Equal(new[] { 6 }, set);
    }

    [Fact]
    public void Clear_ShouldEmptyTheSet_AndAllowReuse()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 500));

        set.Clear();

        Assert.Equal(0, set.Count);
        Assert.Empty(set);
        Assert.False(set.Contains(250));

        set.Add(1);
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Clear_ShouldBeANoOp_WhenAlreadyEmpty()
    {
        var set = new BTreeSet<int>();

        set.Clear();

        Assert.Equal(0, set.Count);
    }

    // ---- structural corners --------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(512)]
    [InlineData(4000)]
    public void Add_ShouldKeepEveryElement_WhenNodesSplit(int count)
    {
        var set = new BTreeSet<int>();
        for (int i = 0; i < count; i++)
            set.Add(i);

        Assert.Equal(count, set.Count);
        Assert.Equal(Enumerable.Range(0, count), set);
        for (int i = 0; i < count; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Add_ShouldSortElements_WhenInsertedInShuffledOrder()
    {
        var rand = new Random(20260726);
        var set = new BTreeSet<int>();
        foreach (int value in Enumerable.Range(0, 2000).OrderBy(_ => rand.Next()))
            set.Add(value);

        Assert.Equal(Enumerable.Range(0, 2000), set);
    }

    [Theory]
    [InlineData("ascending")]
    [InlineData("descending")]
    [InlineData("alternating")]
    public void Remove_ShouldRebalance_WhenNodesUnderflow(string order)
    {
        const int Count = 1500;
        var set = new BTreeSet<int>(Enumerable.Range(0, Count));

        int[] removal = order switch
        {
            "ascending" => Enumerable.Range(0, Count).ToArray(),
            "descending" => Enumerable.Range(0, Count).Reverse().ToArray(),
            _ => Enumerable.Range(0, Count).Where(i => i % 2 == 0)
                    .Concat(Enumerable.Range(0, Count).Where(i => i % 2 == 1).Reverse())
                    .ToArray(),
        };

        var expected = new SortedSet<int>(Enumerable.Range(0, Count));
        foreach (int value in removal)
        {
            Assert.True(set.Remove(value));
            expected.Remove(value);

            Assert.Equal(expected.Count, set.Count);
            Assert.Equal(expected, set);
        }
    }

    [Fact]
    public void AddAndRemove_ShouldStayConsistent_WhenInterleaved()
    {
        var rand = new Random(4242);
        var set = new BTreeSet<int>();
        var oracle = new SortedSet<int>();

        for (int step = 0; step < 20_000; step++)
        {
            int value = rand.Next(0, 900);
            if (rand.Next(0, 2) == 0)
                Assert.Equal(oracle.Add(value), set.TryAdd(value));
            else
                Assert.Equal(oracle.Remove(value), set.Remove(value));
        }

        Assert.Equal(oracle.Count, set.Count);
        Assert.Equal(oracle, set);
    }

    [Fact]
    public void Add_ShouldAcceptNullElement_AndSortItBeforeEveryNonNullElement()
    {
        var set = new BTreeSet<string>();
        set.Add("b");
        set.Add(null!);
        set.Add("a");

        Assert.Equal(new string?[] { null, "a", "b" }, set);
        Assert.True(set.Contains(null!));
        Assert.True(set.Remove(null!));
        Assert.Equal(2, set.Count);
    }

    // ---- the ordered surface -------------------------------------------------

    [Fact]
    public void MinAndMax_ShouldThrow_WhenSetIsEmpty()
    {
        var set = new BTreeSet<int>();

        Assert.Throws<InvalidOperationException>(() => _ = set.Min);
        Assert.Throws<InvalidOperationException>(() => _ = set.Max);
        Assert.False(set.TryGetMin(out int min));
        Assert.Equal(0, min);
        Assert.False(set.TryGetMax(out int max));
        Assert.Equal(0, max);
    }

    [Fact]
    public void MinAndMax_ShouldTrackTheEnds_AcrossSplitsAndRemovals()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 1000));

        Assert.Equal(0, set.Min);
        Assert.Equal(999, set.Max);

        set.Remove(0);
        set.Remove(999);

        Assert.Equal(1, set.Min);
        Assert.Equal(998, set.Max);
    }

    [Fact]
    public void Bounds_ShouldMatchTheOracle_AtEveryPositionOfALargeTree()
    {
        int[] values = Enumerable.Range(0, 600).Select(i => i * 3).ToArray();
        var set = new BTreeSet<int>(values);

        for (int probe = -2; probe <= values[^1] + 2; probe++)
        {
            bool expectedLower = values.Any(v => v >= probe);
            Assert.Equal(expectedLower, set.TryGetLowerBound(probe, out int lower));
            if (expectedLower)
                Assert.Equal(values.First(v => v >= probe), lower);

            bool expectedUpper = values.Any(v => v > probe);
            Assert.Equal(expectedUpper, set.TryGetUpperBound(probe, out int upper));
            if (expectedUpper)
                Assert.Equal(values.First(v => v > probe), upper);
        }
    }

    [Fact]
    public void Bounds_ShouldFail_WhenSetIsEmpty()
    {
        var set = new BTreeSet<int>();

        Assert.False(set.TryGetLowerBound(0, out int lower));
        Assert.Equal(0, lower);
        Assert.False(set.TryGetUpperBound(0, out int upper));
        Assert.Equal(0, upper);
    }

    [Fact]
    public void EnumerateRange_ShouldYieldTheHalfOpenRange()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 1000));

        Assert.Equal(Enumerable.Range(100, 50), set.EnumerateRange(100, 150));
        Assert.Empty(set.EnumerateRange(50, 50));
        Assert.Empty(set.EnumerateRange(5000, 6000));
    }

    [Fact]
    public void EnumerateRange_ShouldThrow_WhenBoundsAreInverted()
    {
        var set = new BTreeSet<int>();

        var ex = Assert.Throws<ArgumentException>(() => set.EnumerateRange(10, 5));
        Assert.Equal("toExclusive", ex.ParamName);
    }

    [Fact]
    public void EnumerateRange_ShouldMatchTheOracle_ForManyRandomWindows()
    {
        var rand = new Random(90210);
        int[] values = Enumerable.Range(0, 800).Select(i => i * 2).ToArray();
        var set = new BTreeSet<int>(values);

        for (int trial = 0; trial < 200; trial++)
        {
            int from = rand.Next(-10, 1620);
            int to = from + rand.Next(0, 200);

            Assert.Equal(values.Where(v => v >= from && v < to), set.EnumerateRange(from, to));
        }
    }

    // ---- the ISet<T> algebra -------------------------------------------------

    [Fact]
    public void UnionWith_ShouldAddMissingElements()
    {
        var set = new BTreeSet<int>(new[] { 1, 3, 5 });

        set.UnionWith(new[] { 3, 4, 5, 6 });

        Assert.Equal(new[] { 1, 3, 4, 5, 6 }, set);
    }

    [Fact]
    public void IntersectWith_ShouldKeepOnlySharedElements()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 100));

        set.IntersectWith(new[] { 5, 50, 500 });

        Assert.Equal(new[] { 5, 50 }, set);
    }

    [Fact]
    public void ExceptWith_ShouldRemoveTheGivenElements()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 10));

        set.ExceptWith(new[] { 0, 2, 4, 6, 8, 100 });

        Assert.Equal(new[] { 1, 3, 5, 7, 9 }, set);
    }

    [Fact]
    public void SymmetricExceptWith_ShouldToggleMembership()
    {
        var set = new BTreeSet<int>(new[] { 1, 2, 3 });

        set.SymmetricExceptWith(new[] { 3, 4 });

        Assert.Equal(new[] { 1, 2, 4 }, set);
    }

    [Fact]
    public void SubsetAndSupersetQueries_ShouldMatchHashSetSemantics()
    {
        var set = new BTreeSet<int>(new[] { 1, 2, 3 });

        Assert.True(set.IsSubsetOf(new[] { 1, 2, 3, 4 }));
        Assert.True(set.IsProperSubsetOf(new[] { 1, 2, 3, 4 }));
        Assert.False(set.IsProperSubsetOf(new[] { 1, 2, 3 }));
        Assert.True(set.IsSupersetOf(new[] { 1, 2 }));
        Assert.True(set.IsProperSupersetOf(new[] { 1, 2 }));
        Assert.False(set.IsProperSupersetOf(new[] { 1, 2, 3 }));
        Assert.True(set.Overlaps(new[] { 3, 9 }));
        Assert.False(set.Overlaps(new[] { 9, 10 }));
        Assert.True(set.SetEquals(new[] { 3, 2, 1 }));
        Assert.False(set.SetEquals(new[] { 1, 2 }));
    }

    [Fact]
    public void IReadOnlySet_ShouldExposeTheSameQueries()
    {
        IReadOnlySet<int> set = new BTreeSet<int>(new[] { 1, 2, 3 });

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(2));
        Assert.True(set.IsSubsetOf(new[] { 1, 2, 3 }));
        Assert.True(set.IsSupersetOf(new[] { 1 }));
        Assert.True(set.IsProperSubsetOf(new[] { 1, 2, 3, 4 }));
        Assert.True(set.IsProperSupersetOf(new[] { 1, 2 }));
        Assert.True(set.Overlaps(new[] { 2 }));
        Assert.True(set.SetEquals(new[] { 1, 2, 3 }));
    }

    [Fact]
    public void CopyTo_ShouldWriteElementsInOrder()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 50).Reverse());

        var target = new int[52];
        set.CopyTo(target, 2);

        Assert.Equal(Enumerable.Range(0, 50), target.Skip(2));
    }

    [Fact]
    public void CopyTo_ShouldThrow_WhenTargetIsInvalid()
    {
        var set = new BTreeSet<int>(new[] { 1 });

        Assert.Throws<ArgumentNullException>(() => set.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new int[4], -1));
        Assert.Throws<ArgumentException>(() => set.CopyTo(new int[1], 1));
    }

    [Fact]
    public void Comparer_ShouldReportTheConfiguredComparer()
    {
        var set = new BTreeSet<int>();

        Assert.Equal(default(DefaultComparer<int>), set.Comparer);
        Assert.True(set.Comparer.Compare(1, 2) < 0);
    }
}
