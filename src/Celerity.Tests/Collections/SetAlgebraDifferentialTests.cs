using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Differential tests for the <see cref="ISet{T}"/> set-algebra surface added to the
/// mutable set family (<see cref="CeleritySet{T, THasher}"/>,
/// <see cref="SwissSet{T, THasher}"/>, <see cref="RobinHoodSet{T, THasher}"/>,
/// <see cref="HashCachingSet{T, THasher}"/>, <see cref="IntSet{THasher}"/>,
/// <see cref="LongSet{THasher}"/>). Each set type is driven through a long randomized
/// battery of union / intersect / except / symmetric-except / subset / superset /
/// overlap / equality operations in lockstep with a BCL <see cref="HashSet{T}"/>
/// oracle; any divergence in the resulting contents or query result fails the test.
/// A deliberately small element universe (including the out-of-band zero / default
/// element) forces frequent overlaps, resizes, tombstone churn, and duplicate handling.
/// </summary>
public class SetAlgebraDifferentialTests
{
    // Small universe so `other` and the set overlap heavily; includes 0 (the
    // out-of-band default/zero slot) and negatives.
    private const int UniverseLow = -2;
    private const int UniverseHigh = 11; // exclusive

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void CeleritySet_MatchesHashSet(int seed) =>
        RunDifferential(() => new CeleritySet<int, Int32WangNaiveHasher>(), i => i, seed);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void SwissSet_MatchesHashSet(int seed) =>
        RunDifferential(() => new SwissSet<int, Int32WangNaiveHasher>(), i => i, seed);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void RobinHoodSet_MatchesHashSet(int seed) =>
        RunDifferential(() => new RobinHoodSet<int, Int32WangNaiveHasher>(), i => i, seed);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void HashCachingSet_MatchesHashSet(int seed) =>
        RunDifferential(() => new HashCachingSet<int, Int32WangNaiveHasher>(), i => i, seed);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void PooledCeleritySet_MatchesHashSet(int seed) =>
        RunDifferential(() => new PooledCeleritySet<int, Int32WangNaiveHasher>(), i => i, seed);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void IntSet_MatchesHashSet(int seed) =>
        RunDifferential(() => new IntSet(), i => i, seed);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void LongSet_MatchesHashSet(int seed) =>
        RunDifferential<long>(() => new LongSet(), i => i, seed);

    private static void RunDifferential<T>(Func<ISet<T>> factory, Func<int, T> map, int seed)
    {
        var rng = new Random(seed);
        ISet<T> set = factory();
        var oracle = new HashSet<T>();

        const int steps = 4000;
        for (int step = 0; step < steps; step++)
        {
            switch (rng.Next(12))
            {
                case 0:
                {
                    IEnumerable<T> other = RandomOther(rng, map);
                    set.UnionWith(other);
                    oracle.UnionWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 1:
                {
                    IEnumerable<T> other = RandomOther(rng, map);
                    set.IntersectWith(other);
                    oracle.IntersectWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 2:
                {
                    IEnumerable<T> other = RandomOther(rng, map);
                    set.ExceptWith(other);
                    oracle.ExceptWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 3:
                {
                    IEnumerable<T> other = RandomOther(rng, map);
                    set.SymmetricExceptWith(other);
                    oracle.SymmetricExceptWith(other);
                    AssertSame(set, oracle, step);
                    break;
                }
                case 4:
                {
                    IEnumerable<T> other = RandomOther(rng, map);
                    Assert.Equal(oracle.IsSubsetOf(other), set.IsSubsetOf(other));
                    break;
                }
                case 5:
                {
                    IEnumerable<T> other = RandomOther(rng, map);
                    Assert.Equal(oracle.IsSupersetOf(other), set.IsSupersetOf(other));
                    break;
                }
                case 6:
                {
                    IEnumerable<T> other = RandomOther(rng, map);
                    Assert.Equal(oracle.IsProperSubsetOf(other), set.IsProperSubsetOf(other));
                    break;
                }
                case 7:
                {
                    IEnumerable<T> other = RandomOther(rng, map);
                    Assert.Equal(oracle.IsProperSupersetOf(other), set.IsProperSupersetOf(other));
                    break;
                }
                case 8:
                {
                    IEnumerable<T> other = RandomOther(rng, map);
                    Assert.Equal(oracle.Overlaps(other), set.Overlaps(other));
                    break;
                }
                case 9:
                {
                    IEnumerable<T> other = RandomOther(rng, map);
                    Assert.Equal(oracle.SetEquals(other), set.SetEquals(other));
                    break;
                }
                case 10:
                {
                    // Self-aliasing: `other` is the set itself. Must match HashSet's
                    // own self-operation behaviour exactly.
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
                    // Single-element churn via ISet<T>.Add / Remove; assert the bool
                    // results agree too.
                    T e = map(rng.Next(UniverseLow, UniverseHigh));
                    if (rng.Next(2) == 0)
                        Assert.Equal(oracle.Add(e), set.Add(e));
                    else
                        Assert.Equal(oracle.Remove(e), set.Remove(e));
                    AssertSame(set, oracle, step);
                    break;
                }
            }
        }

        // Return any rented backing arrays to the pool (PooledCeleritySet); a no-op
        // for the non-pooled set types.
        (set as IDisposable)?.Dispose();
    }

    // Builds a random `other` sequence over the small universe, with duplicates. Half
    // the time it is returned as a plain array (an ICollection<T>, exercising the
    // count-based fast paths); the other half as a lazy, non-ICollection enumerable.
    private static IEnumerable<T> RandomOther<T>(Random rng, Func<int, T> map)
    {
        int length = rng.Next(0, 16);
        var items = new T[length];
        for (int i = 0; i < length; i++)
            items[i] = map(rng.Next(UniverseLow, UniverseHigh));

        return rng.Next(2) == 0 ? items : items.Select(x => x);
    }

    private static void AssertSame<T>(ISet<T> actual, HashSet<T> expected, int step)
    {
        Assert.True(expected.Count == actual.Count,
            $"step {step}: count mismatch — expected {expected.Count}, got {actual.Count}");
        foreach (T e in expected)
            Assert.True(actual.Contains(e), $"step {step}: actual missing element {e}");
        foreach (T e in actual)
            Assert.True(expected.Contains(e), $"step {step}: actual has extra element {e}");
    }
}
