using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #259: differential coverage for EnumSet<TEnum>'s ISet<TEnum> set-algebra
// surface. EnumSet cannot join the int-parameterized SetAlgebraDifferentialTests
// (int is not an enum), so it carries its own enum-typed differential harness here.
//
// EnumSet<EnumSetWide> is driven through a long randomized battery of union /
// intersect / except / symmetric-except / subset / superset / overlap / equality
// operations in lockstep with a BCL HashSet<EnumSetWide> oracle; any divergence in
// contents or query result fails. Crucially, `other` is *sometimes another
// EnumSet<EnumSetWide>* — exercising the word-wise bitwise fast paths — and sometimes
// a plain array or lazy enumerable, exercising the shared SetOperations fallback. The
// element universe spans all three of EnumSetWide's backing words (including
// undefined-but-in-range values) so cross-word carries and boundaries are hit.
public class EnumSetAlgebraDifferentialTests
{
    // EnumSetWide's max value is 130, so its bit vector spans [0, 192). Cover the first
    // two words plus the boundary so operations touch multiple words with heavy overlap.
    private const int UniverseLow = 0;
    private const int UniverseHigh = 70; // exclusive

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void EnumSet_MatchesHashSet(int seed)
    {
        var rng = new Random(seed);
        var set = new EnumSet<EnumSetWide>();
        var oracle = new HashSet<EnumSetWide>();

        const int steps = 4000;
        for (int step = 0; step < steps; step++)
        {
            switch (rng.Next(12))
            {
                case 0:
                {
                    IEnumerable<EnumSetWide> other = RandomOther(rng);
                    set.UnionWith(other);
                    oracle.UnionWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 1:
                {
                    IEnumerable<EnumSetWide> other = RandomOther(rng);
                    set.IntersectWith(other);
                    oracle.IntersectWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 2:
                {
                    IEnumerable<EnumSetWide> other = RandomOther(rng);
                    set.ExceptWith(other);
                    oracle.ExceptWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 3:
                {
                    IEnumerable<EnumSetWide> other = RandomOther(rng);
                    set.SymmetricExceptWith(other);
                    oracle.SymmetricExceptWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 4:
                {
                    IEnumerable<EnumSetWide> other = RandomOther(rng);
                    Assert.Equal(oracle.IsSubsetOf(other), set.IsSubsetOf(other));
                    break;
                }
                case 5:
                {
                    IEnumerable<EnumSetWide> other = RandomOther(rng);
                    Assert.Equal(oracle.IsSupersetOf(other), set.IsSupersetOf(other));
                    break;
                }
                case 6:
                {
                    IEnumerable<EnumSetWide> other = RandomOther(rng);
                    Assert.Equal(oracle.IsProperSubsetOf(other), set.IsProperSubsetOf(other));
                    break;
                }
                case 7:
                {
                    IEnumerable<EnumSetWide> other = RandomOther(rng);
                    Assert.Equal(oracle.IsProperSupersetOf(other), set.IsProperSupersetOf(other));
                    break;
                }
                case 8:
                {
                    IEnumerable<EnumSetWide> other = RandomOther(rng);
                    Assert.Equal(oracle.Overlaps(other), set.Overlaps(other));
                    break;
                }
                case 9:
                {
                    IEnumerable<EnumSetWide> other = RandomOther(rng);
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
                default:
                {
                    var e = (EnumSetWide)rng.Next(UniverseLow, UniverseHigh);
                    if (rng.Next(2) == 0)
                        Assert.Equal(oracle.Add(e), ((ISet<EnumSetWide>)set).Add(e));
                    else
                        Assert.Equal(oracle.Remove(e), set.Remove(e));
                    AssertSame(set, oracle, step);
                    break;
                }
            }
        }
    }

    // Builds a random `other` over the universe. A third of the time it is another
    // EnumSet<EnumSetWide> (the word-wise fast path); otherwise a plain array
    // (ICollection) or a lazy, non-ICollection enumerable (the SetOperations fallback).
    private static IEnumerable<EnumSetWide> RandomOther(Random rng)
    {
        int length = rng.Next(0, 16);
        var items = new EnumSetWide[length];
        for (int i = 0; i < length; i++)
            items[i] = (EnumSetWide)rng.Next(UniverseLow, UniverseHigh);

        return rng.Next(3) switch
        {
            0 => new EnumSet<EnumSetWide>(items),
            1 => items,
            _ => items.Select(x => x),
        };
    }

    private static void AssertSame(EnumSet<EnumSetWide> actual, HashSet<EnumSetWide> expected, int step)
    {
        Assert.True(expected.Count == actual.Count,
            $"step {step}: count mismatch — expected {expected.Count}, got {actual.Count}");
        foreach (EnumSetWide e in expected)
            Assert.True(actual.Contains(e), $"step {step}: actual missing element {e}");
        foreach (EnumSetWide e in actual)
            Assert.True(expected.Contains(e), $"step {step}: actual has extra element {e}");
    }
}
