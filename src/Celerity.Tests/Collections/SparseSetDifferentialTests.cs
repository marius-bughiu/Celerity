using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #287: differential coverage for SparseSet against a BCL HashSet<int> oracle.
//
// SparseSet cannot join the shared SetAlgebraDifferentialTests: that harness draws
// from a universe that includes negatives (UniverseLow = -2), which SparseSet rejects
// by design (its universe is [0, Universe)). So — exactly like EnumSet's own
// EnumSetAlgebraDifferentialTests — SparseSet carries a dedicated non-negative-universe
// differential here.
//
// A SparseSet(Universe) is driven through a long randomized battery of add / try-add /
// remove / contains / clear plus the full ISet<int> set-algebra surface (union /
// intersect / except / symmetric-except / subset / superset / overlap / equality), in
// lockstep with a HashSet<int> oracle. Any divergence in resulting contents or a query
// result fails. The small, dense universe forces frequent overlaps, dense-array growth,
// swap-removes, and — crucially — clear-then-reuse cycles, which stress the stale-sparse
// round-trip membership check that makes Clear O(1).
public class SparseSetDifferentialTests
{
    private const int Universe = 40;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void SparseSet_MatchesHashSet(int seed)
    {
        var rng = new Random(seed);
        var set = new SparseSet(Universe);
        var oracle = new HashSet<int>();

        const int steps = 6000;
        for (int step = 0; step < steps; step++)
        {
            switch (rng.Next(14))
            {
                case 0:
                {
                    IEnumerable<int> other = RandomOther(rng);
                    set.UnionWith(other);
                    oracle.UnionWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 1:
                {
                    IEnumerable<int> other = RandomOther(rng);
                    set.IntersectWith(other);
                    oracle.IntersectWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 2:
                {
                    IEnumerable<int> other = RandomOther(rng);
                    set.ExceptWith(other);
                    oracle.ExceptWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 3:
                {
                    IEnumerable<int> other = RandomOther(rng);
                    set.SymmetricExceptWith(other);
                    oracle.SymmetricExceptWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 4:
                {
                    IEnumerable<int> other = RandomOther(rng);
                    Assert.Equal(oracle.IsSubsetOf(other), set.IsSubsetOf(other));
                    break;
                }
                case 5:
                {
                    IEnumerable<int> other = RandomOther(rng);
                    Assert.Equal(oracle.IsSupersetOf(other), set.IsSupersetOf(other));
                    break;
                }
                case 6:
                {
                    IEnumerable<int> other = RandomOther(rng);
                    Assert.Equal(oracle.IsProperSubsetOf(other), set.IsProperSubsetOf(other));
                    break;
                }
                case 7:
                {
                    IEnumerable<int> other = RandomOther(rng);
                    Assert.Equal(oracle.IsProperSupersetOf(other), set.IsProperSupersetOf(other));
                    break;
                }
                case 8:
                {
                    IEnumerable<int> other = RandomOther(rng);
                    Assert.Equal(oracle.Overlaps(other), set.Overlaps(other));
                    break;
                }
                case 9:
                {
                    IEnumerable<int> other = RandomOther(rng);
                    Assert.Equal(oracle.SetEquals(other), set.SetEquals(other));
                    break;
                }
                case 10:
                {
                    // Self-aliasing: `other` is the set itself.
                    switch (rng.Next(4))
                    {
                        case 0: set.UnionWith(set); oracle.UnionWith(oracle); break;
                        case 1: set.IntersectWith(set); oracle.IntersectWith(oracle); break;
                        case 2: set.ExceptWith(set); oracle.ExceptWith(oracle); break;
                        default: set.SymmetricExceptWith(set); oracle.SymmetricExceptWith(oracle); break;
                    }
                    AssertSame(set, oracle, step);
                    break;
                }
                case 11:
                {
                    // Clear-then-reuse: the O(1) clear must leave no stale membership.
                    set.Clear();
                    oracle.Clear();
                    AssertSame(set, oracle, step);
                    // Re-add a few so the sparse array holds live-again slots that were
                    // stale a moment ago.
                    for (int k = 0; k < 4; k++)
                    {
                        int v = rng.Next(0, Universe);
                        Assert.Equal(oracle.Add(v), set.TryAdd(v));
                    }
                    AssertSame(set, oracle, step);
                    break;
                }
                case 12:
                {
                    // Contains agreement over the whole universe (and a couple of
                    // out-of-range probes, which must read as absent).
                    int probe = rng.Next(-2, Universe + 2);
                    bool expected = probe >= 0 && probe < Universe && oracle.Contains(probe);
                    Assert.Equal(expected, set.Contains(probe));
                    break;
                }
                default:
                {
                    // Single-element churn via ISet<int>.Add / Remove; assert the bool
                    // results agree too.
                    int e = rng.Next(0, Universe);
                    if (rng.Next(2) == 0)
                        Assert.Equal(oracle.Add(e), ((ISet<int>)set).Add(e));
                    else
                        Assert.Equal(oracle.Remove(e), set.Remove(e));
                    AssertSame(set, oracle, step);
                    break;
                }
            }
        }
    }

    // Builds a random `other` sequence over the non-negative universe, with duplicates.
    // Half the time it is a plain array (an ICollection<int>, exercising the count-based
    // fast paths); the other half a lazy, non-ICollection enumerable.
    private static IEnumerable<int> RandomOther(Random rng)
    {
        int length = rng.Next(0, 16);
        var items = new int[length];
        for (int i = 0; i < length; i++)
            items[i] = rng.Next(0, Universe);

        return rng.Next(2) == 0 ? items : items.Select(x => x);
    }

    private static void AssertSame(SparseSet actual, HashSet<int> expected, int step)
    {
        Assert.True(expected.Count == actual.Count,
            $"step {step}: count mismatch — expected {expected.Count}, got {actual.Count}");
        foreach (int e in expected)
            Assert.True(actual.Contains(e), $"step {step}: actual missing element {e}");
        foreach (int e in actual)
            Assert.True(expected.Contains(e), $"step {step}: actual has extra element {e}");
    }
}
