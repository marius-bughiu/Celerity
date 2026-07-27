using Celerity.Collections;
using Celerity.Hashing;
using Xunit;

namespace Celerity.Tests.Collections;

/// <summary>
/// Regression coverage for the "mutable sets only declare <see cref="ISet{T}"/>"
/// defect: every mutable Celerity set must also be assignable to
/// <see cref="IReadOnlySet{T}"/>, and every member reached through that interface
/// must agree with a BCL <see cref="HashSet{T}"/> oracle. Closes #NNN.
/// </summary>
public class ReadOnlySetInterfaceTests
{
    private enum Suit { Clubs, Diamonds, Hearts, Spades }

    private static readonly int[] Seed = [1, 2, 3, 4, 5];
    private static readonly int[] Subset = [2, 3];
    private static readonly int[] Overlapping = [4, 5, 6, 7];
    private static readonly int[] Disjoint = [100, 200];

    // The exact scenario the issue reports as a compile error today: a method
    // whose parameter is typed as IReadOnlySet<T>, not ISet<T>.
    private static int CountViaReadOnlySet<T>(IReadOnlySet<T> set) => set.Count;

    private static void AssertParity<T>(IReadOnlySet<T> set, HashSet<T> oracle, T[] subset, T[] overlapping, T[] disjoint)
    {
        Assert.Equal(oracle.Count, set.Count);
        Assert.Equal(oracle.Count, CountViaReadOnlySet(set));

        foreach (T item in oracle)
            Assert.True(set.Contains(item));

        Assert.Equal(oracle.IsSubsetOf(subset), set.IsSubsetOf(subset));
        Assert.Equal(oracle.IsProperSubsetOf(subset), set.IsProperSubsetOf(subset));
        Assert.Equal(oracle.IsSupersetOf(subset), set.IsSupersetOf(subset));
        Assert.Equal(oracle.IsProperSupersetOf(subset), set.IsProperSupersetOf(subset));
        Assert.Equal(oracle.Overlaps(overlapping), set.Overlaps(overlapping));
        Assert.Equal(oracle.Overlaps(disjoint), set.Overlaps(disjoint));
        Assert.Equal(oracle.SetEquals(oracle), set.SetEquals(oracle));
    }

    [Fact]
    public void CeleritySet_ShouldImplementIReadOnlySet_WhenAssignedAndQueried()
    {
        var set = new CeleritySet<int, Int32WangNaiveHasher>(Seed);
        AssertParity<int>(set, new HashSet<int>(Seed), Subset, Overlapping, Disjoint);
    }

    [Fact]
    public void SwissSet_ShouldImplementIReadOnlySet_WhenAssignedAndQueried()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>(Seed);
        AssertParity<int>(set, new HashSet<int>(Seed), Subset, Overlapping, Disjoint);
    }

    [Fact]
    public void RobinHoodSet_ShouldImplementIReadOnlySet_WhenAssignedAndQueried()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>(Seed);
        AssertParity<int>(set, new HashSet<int>(Seed), Subset, Overlapping, Disjoint);
    }

    [Fact]
    public void HashCachingSet_ShouldImplementIReadOnlySet_WhenAssignedAndQueried()
    {
        var set = new HashCachingSet<int, Int32WangNaiveHasher>(Seed);
        AssertParity<int>(set, new HashSet<int>(Seed), Subset, Overlapping, Disjoint);
    }

    [Fact]
    public void PooledCeleritySet_ShouldImplementIReadOnlySet_WhenAssignedAndQueried()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>(Seed);
        AssertParity<int>(set, new HashSet<int>(Seed), Subset, Overlapping, Disjoint);
    }

    [Fact]
    public void SmallSet_ShouldImplementIReadOnlySet_WhenAssignedAndQueried()
    {
        var set = new SmallSet<int>(Seed);
        AssertParity<int>(set, new HashSet<int>(Seed), Subset, Overlapping, Disjoint);
    }

    [Fact]
    public void IntSet_ShouldImplementIReadOnlySet_WhenAssignedAndQueried()
    {
        var set = new IntSet(Seed);
        AssertParity<int>(set, new HashSet<int>(Seed), Subset, Overlapping, Disjoint);
    }

    [Fact]
    public void LongSet_ShouldImplementIReadOnlySet_WhenAssignedAndQueried()
    {
        long[] seed = [1L, 2L, 3L, 4L, 5L];
        long[] subset = [2L, 3L];
        long[] overlapping = [4L, 5L, 6L, 7L];
        long[] disjoint = [100L, 200L];

        var set = new LongSet(seed);
        AssertParity<long>(set, new HashSet<long>(seed), subset, overlapping, disjoint);
    }

    [Fact]
    public void SparseSet_ShouldImplementIReadOnlySet_WhenAssignedAndQueried()
    {
        var set = new SparseSet(universe: 256, Seed);
        AssertParity<int>(set, new HashSet<int>(Seed), Subset, Overlapping, Disjoint);
    }

    [Fact]
    public void EnumSet_ShouldImplementIReadOnlySet_WhenAssignedAndQueried()
    {
        Suit[] seed = [Suit.Clubs, Suit.Hearts];
        Suit[] subset = [Suit.Clubs];
        Suit[] overlapping = [Suit.Hearts, Suit.Spades];
        Suit[] disjoint = [Suit.Diamonds];

        var set = new EnumSet<Suit>(seed);
        AssertParity<Suit>(set, new HashSet<Suit>(seed), subset, overlapping, disjoint);
    }
}