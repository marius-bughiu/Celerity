using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="PersistentHashSet{T, THasher}"/> against a
/// <see cref="HashSet{T}"/> oracle driven through the same operation sequence.
///
/// <para>
/// <b>Persistence is the thing on trial, not the last answer.</b> A set that path-copies one node too few
/// still agrees with the oracle on the set you are holding — the damage lands on a set handed out
/// <i>earlier</i>, whose storage the newer one was supposed to share read-only and instead wrote through. So
/// every run keeps <b>snapshots</b>: a set and an independent copy of the oracle taken at intervals, all
/// re-checked element by element after the last operation.
/// </para>
///
/// <para>
/// <b>The hasher is an axis, because it decides which shapes the trie ever takes.</b> Under
/// <see cref="Int32IdentityHasher"/> the elements spread over the trie and full-hash collisions never happen,
/// so the collision-node paths are dead. Under a hasher that keeps only the low bits, every element set of
/// any size forces collision nodes, deep single-child chains, and the dissolve-back-up rule that collapses
/// them. Each generated run is therefore replayed through both, and the oracle does not care which.
/// </para>
///
/// <para>
/// <b>The set algebra gets its own generated operands.</b> Every operation reaches the trie through a
/// builder seeded from the receiver, and every one of them has a "nothing changed, hand back the receiver"
/// exit — so the property checks both the elements and, where the oracle says nothing changed, the
/// reference.
/// </para>
/// </summary>
public class PersistentHashSetDifferentialTests
{
    // Keeps only the low six bits, so an element space of a few hundred guarantees several elements per hash
    // and therefore collision nodes, chains of single-child nodes above them, and the dissolve rule.
    private struct LowBitsHasher : IHashProvider<int>
    {
        public int Hash(int key) => key & 63;
    }

    // Op count, element space, removal weight (percent of ops that remove) and a seed.
    private static readonly Gen<(int Ops, int Space, int RemoveWeight, uint Seed)> GenRuns =
        Gen.Select(Gen.Int[0, 1_200], Gen.Int[1, 900], Gen.Int[0, 55], Gen.UInt);

    // Two operand sizes, a shared element space and a seed: a small space makes the operands overlap, a
    // large one keeps them mostly disjoint, and the generator reaches both.
    private static readonly Gen<(int LeftSize, int RightSize, int Space, uint Seed)> GenOperands =
        Gen.Select(Gen.Int[0, 400], Gen.Int[0, 400], Gen.Int[1, 1_000], Gen.UInt);

    [Fact]
    public void EveryOperation_ShouldMatchTheHashSetOracle_UnderGeneratedRuns()
    {
        GenRuns.Sample(
            run =>
            {
                AssertAgreesWithOracle<Int32IdentityHasher>(run.Ops, run.Space, run.RemoveWeight, run.Seed);
                AssertAgreesWithOracle<LowBitsHasher>(run.Ops, run.Space, run.RemoveWeight, run.Seed);
            },
            iter: 120);
    }

    [Fact]
    public void ABuilderShouldMatchTheHashSetOracle_UnderGeneratedRuns()
    {
        GenRuns.Sample(
            run =>
            {
                AssertBuilderAgreesWithOracle<Int32IdentityHasher>(run.Ops, run.Space, run.RemoveWeight, run.Seed);
                AssertBuilderAgreesWithOracle<LowBitsHasher>(run.Ops, run.Space, run.RemoveWeight, run.Seed);
            },
            iter: 120);
    }

    [Fact]
    public void SetAlgebraAndQueries_ShouldMatchTheHashSetOracle_UnderGeneratedOperands()
    {
        GenOperands.Sample(
            run =>
            {
                AssertSetAlgebra<Int32IdentityHasher>(run.LeftSize, run.RightSize, run.Space, run.Seed);
                AssertSetAlgebra<LowBitsHasher>(run.LeftSize, run.RightSize, run.Space, run.Seed);
            },
            iter: 150);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(1_024)]
    [InlineData(1_025)]
    [InlineData(5_000)]
    public void FillThenDrain_ShouldMatchTheOracle_AtEveryElementCount(int count)
    {
        AssertFillThenDrain<Int32IdentityHasher>(count);
        AssertFillThenDrain<LowBitsHasher>(count);
    }

    // A run of Add / Remove against a HashSet oracle, with snapshots re-checked at the end.
    private static void AssertAgreesWithOracle<THasher>(int ops, int space, int removeWeight, uint seed)
        where THasher : struct, IHashProvider<int>
    {
        var rng = new Random((int)seed);
        PersistentHashSet<int, THasher> set = PersistentHashSet<int, THasher>.Empty;
        var oracle = new HashSet<int>();
        var snapshots = new List<(PersistentHashSet<int, THasher> Set, HashSet<int> Oracle)>();

        for (int step = 0; step < ops; step++)
        {
            int item = rng.Next(space);
            PersistentHashSet<int, THasher> before = set;

            bool changed;
            if (rng.Next(100) < removeWeight)
            {
                set = set.Remove(item);
                changed = oracle.Remove(item);
            }
            else
            {
                set = set.Add(item);
                changed = oracle.Add(item);
            }

            // A no-op edit must hand back the receiver itself, not an equal copy.
            if (!changed)
                Assert.Same(before, set);

            Assert.Equal(oracle.Count, set.Count);

            if (step % 23 == 0)
                snapshots.Add((set, new HashSet<int>(oracle)));
        }

        snapshots.Add((set, new HashSet<int>(oracle)));

        foreach ((PersistentHashSet<int, THasher> snapshot, HashSet<int> expected) in snapshots)
            AssertSameElements(expected, snapshot);
    }

    // The same run driven through a builder, snapshotting with ToImmutable so the transient path's isolation
    // is on trial alongside its arithmetic.
    private static void AssertBuilderAgreesWithOracle<THasher>(int ops, int space, int removeWeight, uint seed)
        where THasher : struct, IHashProvider<int>
    {
        var rng = new Random((int)seed);
        PersistentHashSet<int, THasher>.Builder builder = new();
        var oracle = new HashSet<int>();
        var snapshots = new List<(PersistentHashSet<int, THasher> Set, HashSet<int> Oracle)>();

        for (int step = 0; step < ops; step++)
        {
            int item = rng.Next(space);

            if (rng.Next(100) < removeWeight)
                Assert.Equal(oracle.Remove(item), builder.Remove(item));
            else
                Assert.Equal(oracle.Add(item), builder.Add(item));

            Assert.Equal(oracle.Count, builder.Count);

            if (step % 23 == 0)
                snapshots.Add((builder.ToImmutable(), new HashSet<int>(oracle)));
        }

        snapshots.Add((builder.ToImmutable(), new HashSet<int>(oracle)));

        foreach ((PersistentHashSet<int, THasher> snapshot, HashSet<int> expected) in snapshots)
            AssertSameElements(expected, snapshot);
    }

    // The four set-algebra operations and the six IReadOnlySet queries, each against HashSet<int> run on the
    // same operands. The right-hand operand is an array with repeats, because a duplicate is exactly what the
    // toggle and count-based shapes have to see through.
    private static void AssertSetAlgebra<THasher>(int leftSize, int rightSize, int space, uint seed)
        where THasher : struct, IHashProvider<int>
    {
        var rng = new Random((int)seed);

        int[] leftSource = new int[leftSize];
        for (int i = 0; i < leftSize; i++)
            leftSource[i] = rng.Next(space);

        int[] right = new int[rightSize];
        for (int i = 0; i < rightSize; i++)
            right[i] = rng.Next(space);

        var set = new PersistentHashSet<int, THasher>(leftSource);
        var oracle = new HashSet<int>(leftSource);
        AssertSameElements(oracle, set);

        AssertOperation(set, oracle, right, static (s, o) => s.Union(o), static (h, o) => h.UnionWith(o));
        AssertOperation(set, oracle, right, static (s, o) => s.Except(o), static (h, o) => h.ExceptWith(o));
        AssertOperation(set, oracle, right, static (s, o) => s.Intersect(o), static (h, o) => h.IntersectWith(o));
        AssertOperation(set, oracle, right, static (s, o) => s.SymmetricExcept(o), static (h, o) => h.SymmetricExceptWith(o));

        Assert.Equal(oracle.SetEquals(right), set.SetEquals(right));
        Assert.Equal(oracle.IsSubsetOf(right), set.IsSubsetOf(right));
        Assert.Equal(oracle.IsProperSubsetOf(right), set.IsProperSubsetOf(right));
        Assert.Equal(oracle.IsSupersetOf(right), set.IsSupersetOf(right));
        Assert.Equal(oracle.IsProperSupersetOf(right), set.IsProperSupersetOf(right));
        Assert.Equal(oracle.Overlaps(right), set.Overlaps(right));

        // The receiver is never touched by any of the above.
        AssertSameElements(oracle, set);
    }

    private static void AssertOperation<THasher>(
        PersistentHashSet<int, THasher> set,
        HashSet<int> oracle,
        int[] other,
        Func<PersistentHashSet<int, THasher>, int[], PersistentHashSet<int, THasher>> apply,
        Action<HashSet<int>, int[]> applyToOracle)
        where THasher : struct, IHashProvider<int>
    {
        var expected = new HashSet<int>(oracle);
        applyToOracle(expected, other);

        PersistentHashSet<int, THasher> actual = apply(set, other);
        AssertSameElements(expected, actual);

        if (expected.SetEquals(oracle))
            Assert.Same(set, actual);
    }

    private static void AssertFillThenDrain<THasher>(int count)
        where THasher : struct, IHashProvider<int>
    {
        PersistentHashSet<int, THasher> set = PersistentHashSet<int, THasher>.Empty;
        var snapshots = new List<(PersistentHashSet<int, THasher> Set, int Present)>();

        for (int item = 0; item < count; item++)
        {
            set = set.Add(item);
            if (item % 97 == 0)
                snapshots.Add((set, item + 1));
        }

        Assert.Equal(count, set.Count);

        // Draining takes every removal path backwards: dissolving collision nodes, inlining single-element
        // children, and finally emptying the root.
        PersistentHashSet<int, THasher> draining = set;
        for (int item = count - 1; item >= 0; item--)
        {
            draining = draining.Remove(item);
            Assert.Equal(item, draining.Count);
        }

        Assert.Same(PersistentHashSet<int, THasher>.Empty, draining);

        // The fill snapshots must be untouched by the drain that walked through their storage.
        foreach ((PersistentHashSet<int, THasher> snapshot, int present) in snapshots)
        {
            Assert.Equal(present, snapshot.Count);
            for (int item = 0; item < present; item++)
                Assert.True(snapshot.Contains(item));
        }
    }

    private static void AssertSameElements<THasher>(HashSet<int> expected, PersistentHashSet<int, THasher> actual)
        where THasher : struct, IHashProvider<int>
    {
        Assert.Equal(expected.Count, actual.Count);

        // The trie's canonical form is checked alongside its contents, because the collapse rule is the one
        // documented invariant a behavioural assertion cannot see.
        Assert.Equal(expected.Count, PersistentHashMapShape.AssertCanonical(actual));

        foreach (int item in expected)
            Assert.True(actual.Contains(item));

        // And nothing extra: enumeration is the only view that can expose an element no lookup asks for.
        var enumerated = new List<int>(actual);
        Assert.Equal(expected.Count, enumerated.Count);
        Assert.True(expected.SetEquals(enumerated));
    }
}
