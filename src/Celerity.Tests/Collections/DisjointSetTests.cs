using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Core behavioural coverage for <see cref="DisjointSet{T}"/>: construction, add/contains, union and its
/// merge/no-op return, find/representative semantics, connectivity queries, component sizing, the disjoint
/// set count, clear, and argument/lookup validation.
/// </summary>
public class DisjointSetTests
{
    [Fact]
    public void NewSet_IsEmpty()
    {
        var ds = new DisjointSet<int>();

        Assert.Equal(0, ds.Count);
        Assert.Equal(0, ds.SetCount);
        Assert.False(ds.Contains(1));
    }

    [Fact]
    public void Add_NewElement_ReturnsTrueAndCreatesSingleton()
    {
        var ds = new DisjointSet<string>();

        Assert.True(ds.Add("a"));
        Assert.Equal(1, ds.Count);
        Assert.Equal(1, ds.SetCount);
        Assert.True(ds.Contains("a"));
        Assert.Equal("a", ds.Find("a"));
        Assert.Equal(1, ds.ComponentSize("a"));
    }

    [Fact]
    public void Add_Duplicate_ReturnsFalseAndDoesNotChangeCounts()
    {
        var ds = new DisjointSet<int>();
        ds.Add(7);

        Assert.False(ds.Add(7));
        Assert.Equal(1, ds.Count);
        Assert.Equal(1, ds.SetCount);
    }

    [Fact]
    public void Union_TwoSingletons_MergesAndDropsSetCount()
    {
        var ds = new DisjointSet<int>();
        ds.Add(1);
        ds.Add(2);

        Assert.True(ds.Union(1, 2));
        Assert.Equal(2, ds.Count);
        Assert.Equal(1, ds.SetCount);
        Assert.True(ds.Connected(1, 2));
        Assert.Equal(ds.Find(1), ds.Find(2));
        Assert.Equal(2, ds.ComponentSize(1));
        Assert.Equal(2, ds.ComponentSize(2));
    }

    [Fact]
    public void Union_AutoAddsMissingElements()
    {
        var ds = new DisjointSet<int>();

        Assert.True(ds.Union(10, 20)); // neither present beforehand
        Assert.Equal(2, ds.Count);
        Assert.Equal(1, ds.SetCount);
        Assert.True(ds.Contains(10));
        Assert.True(ds.Contains(20));
        Assert.True(ds.Connected(10, 20));
    }

    [Fact]
    public void Union_AlreadyConnected_ReturnsFalse()
    {
        var ds = new DisjointSet<int>();
        ds.Union(1, 2);

        Assert.False(ds.Union(1, 2));
        Assert.False(ds.Union(2, 1));
        Assert.Equal(1, ds.SetCount);
    }

    [Fact]
    public void Union_SameElement_ReturnsFalse()
    {
        var ds = new DisjointSet<int>();

        Assert.False(ds.Union(5, 5)); // collapses to one singleton, no merge
        Assert.Equal(1, ds.Count);
        Assert.Equal(1, ds.SetCount);
    }

    [Fact]
    public void Union_IsTransitive()
    {
        var ds = new DisjointSet<int>();
        ds.Union(1, 2);
        ds.Union(2, 3);
        ds.Union(3, 4);

        Assert.Equal(4, ds.Count);
        Assert.Equal(1, ds.SetCount);
        Assert.True(ds.Connected(1, 4));
        Assert.Equal(4, ds.ComponentSize(1));
        // One representative shared by every member.
        int reps = new[] { 1, 2, 3, 4 }.Select(ds.Find).Distinct().Count();
        Assert.Equal(1, reps);
    }

    [Fact]
    public void MergingTwoChains_Unions_TwoComponentsIntoOne()
    {
        var ds = new DisjointSet<int>();
        ds.Union(1, 2);
        ds.Union(3, 4);
        Assert.Equal(2, ds.SetCount);
        Assert.False(ds.Connected(1, 3));

        ds.Union(2, 3);
        Assert.Equal(1, ds.SetCount);
        Assert.True(ds.Connected(1, 4));
        Assert.Equal(4, ds.ComponentSize(1));
    }

    [Fact]
    public void Connected_AbsentElement_ReturnsFalseAndDoesNotMutate()
    {
        var ds = new DisjointSet<int>();
        ds.Add(1);

        Assert.False(ds.Connected(1, 99));
        Assert.False(ds.Connected(99, 1));
        Assert.False(ds.Connected(98, 99));
        // No element was added by a query.
        Assert.Equal(1, ds.Count);
        Assert.False(ds.Contains(99));
    }

    [Fact]
    public void Find_AbsentElement_Throws()
    {
        var ds = new DisjointSet<int>();

        Assert.Throws<KeyNotFoundException>(() => ds.Find(42));
    }

    [Fact]
    public void ComponentSize_AbsentElement_Throws()
    {
        var ds = new DisjointSet<int>();

        Assert.Throws<KeyNotFoundException>(() => ds.ComponentSize(42));
    }

    [Fact]
    public void TryFind_PresentAndAbsent()
    {
        var ds = new DisjointSet<int>();
        ds.Union(1, 2);

        Assert.True(ds.TryFind(1, out int rep));
        Assert.Equal(ds.Find(2), rep);

        Assert.False(ds.TryFind(99, out int missing));
        Assert.Equal(0, missing);
    }

    [Fact]
    public void Clear_ResetsToEmpty()
    {
        var ds = new DisjointSet<int>();
        ds.Union(1, 2);
        ds.Union(3, 4);

        ds.Clear();

        Assert.Equal(0, ds.Count);
        Assert.Equal(0, ds.SetCount);
        Assert.False(ds.Contains(1));
        // Reusable after clear.
        ds.Add(1);
        Assert.Equal(1, ds.Count);
    }

    [Fact]
    public void CapacityConstructor_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DisjointSet<int>(-1));
    }

    [Fact]
    public void IEnumerableConstructor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DisjointSet<int>((IEnumerable<int>)null!));
    }

    [Fact]
    public void IEnumerableConstructor_SeedsSingletons_IgnoringDuplicates()
    {
        var ds = new DisjointSet<int>(new[] { 1, 2, 2, 3, 3, 3 });

        Assert.Equal(3, ds.Count);
        Assert.Equal(3, ds.SetCount);
        foreach (int v in new[] { 1, 2, 3 })
            Assert.Equal(1, ds.ComponentSize(v));
    }

    [Fact]
    public void GetComponents_PartitionsElements()
    {
        var ds = new DisjointSet<int>();
        ds.Union(1, 2);
        ds.Union(2, 3);
        ds.Union(10, 11);
        ds.Add(20);

        var components = ds.GetComponents();

        Assert.Equal(ds.SetCount, components.Count);
        Assert.Equal(3, components.Count);

        // Every element appears exactly once across all groups, and each group is internally connected.
        var seen = components.SelectMany(g => g).ToList();
        Assert.Equal(ds.Count, seen.Count);
        Assert.Equal(ds.Count, seen.Distinct().Count());

        foreach (var group in components)
        {
            int anchor = group[0];
            foreach (int member in group)
                Assert.True(ds.Connected(anchor, member));
        }

        // The three-member chain, the pair, and the singleton are all represented.
        var sizes = components.Select(g => g.Count).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, sizes);
    }

    [Fact]
    public void GetComponents_Empty_ReturnsEmptyList()
    {
        var ds = new DisjointSet<int>();

        Assert.Empty(ds.GetComponents());
    }

    [Fact]
    public void UnionBySize_AttachesSmallerUnderLarger()
    {
        // Build a large tree {1,2,3,4} and a small one {5}, then union; the large root must survive as the
        // representative, keeping trees shallow.
        var ds = new DisjointSet<int>();
        ds.Union(1, 2);
        ds.Union(1, 3);
        ds.Union(1, 4);
        int bigRep = ds.Find(1);

        ds.Add(5);
        ds.Union(5, 1);

        Assert.Equal(bigRep, ds.Find(5));
        Assert.Equal(5, ds.ComponentSize(3));
    }
}
