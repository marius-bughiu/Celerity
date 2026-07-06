using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="RobinHoodSet{T, THasher}"/> under maximum
/// hash-collision pressure, with reference-type (null-default) elements, and
/// against a <see cref="HashSet{T}"/> oracle. The displacement ("rob from the
/// rich") and backward-shift-with-distance-decrement deletion paths are the parts
/// unique to Robin Hood probing, so they get the heaviest coverage here. Mirrors
/// RobinHoodDictionaryCollisionTests and SwissSetCollisionTests.
/// </summary>
public class RobinHoodSetCollisionTests
{
    /// <summary>
    /// A test-only hasher that returns a constant value for every element, forcing
    /// every insert into a single linear-probing chain.
    /// </summary>
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    /// <summary>
    /// A test-only hasher for <see cref="string"/> elements that returns a
    /// constant value, forcing full collision.
    /// </summary>
    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    /// <summary>
    /// A test-only hasher that returns the element itself, so <c>element &amp; mask</c>
    /// places each element at a predictable slot — used to build clusters whose
    /// entries have different ideal slots, the shape that drives the Robin Hood
    /// displacement and shift-back branches.
    /// </summary>
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    // ---------------------------------------------------------------
    //  Full-collision tests — every element shares a home slot
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var set = new RobinHoodSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            set.Add(i);

        Assert.Equal(10, set.Count);
        for (int i = 1; i <= 10; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void DuplicateAdd_ShouldReturnFalse_UnderFullCollision()
    {
        var set = new RobinHoodSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            Assert.True(set.TryAdd(i));
        for (int i = 1; i <= 10; i++)
            Assert.False(set.TryAdd(i));

        Assert.Equal(10, set.Count);
    }

    [Fact]
    public void Remove_ShouldShiftCluster_UnderFullCollision()
    {
        var set = new RobinHoodSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            set.Add(i);

        Assert.True(set.Remove(3));
        Assert.Equal(5, set.Count);
        Assert.False(set.Contains(3));

        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(4));
        Assert.True(set.Contains(5));
        Assert.True(set.Contains(6));
    }

    [Fact]
    public void RemoveThenReinsert_ShouldWork_UnderFullCollision()
    {
        var set = new RobinHoodSet<int, ConstantIntHasher>(8);

        for (int i = 1; i <= 10; i++)
            set.Add(i);

        for (int i = 1; i <= 10; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(5, set.Count);

        for (int i = 1; i <= 10; i += 2)
            set.Add(i);

        Assert.Equal(10, set.Count);
        for (int i = 1; i <= 10; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void DefaultKey_ShouldWorkAlongside_CollisionChain()
    {
        var set = new RobinHoodSet<int, ConstantIntHasher>(16);
        set.Add(0);
        for (int i = 1; i <= 5; i++)
            set.Add(i);

        Assert.Equal(6, set.Count);
        Assert.True(set.Contains(0));
        for (int i = 1; i <= 5; i++)
            Assert.True(set.Contains(i));

        Assert.True(set.Remove(0));
        Assert.Equal(5, set.Count);
        Assert.False(set.Contains(0));
        for (int i = 1; i <= 5; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Resize_ShouldPreserveAll_UnderFullCollision()
    {
        var set = new RobinHoodSet<int, ConstantIntHasher>(
            capacity: 4, loadFactor: 0.5f);

        for (int i = 1; i <= 20; i++)
            set.Add(i);

        Assert.Equal(20, set.Count);
        for (int i = 1; i <= 20; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemaining_UnderFullCollision()
    {
        var set = new RobinHoodSet<int, ConstantIntHasher>(
            capacity: 4, loadFactor: 0.5f);

        for (int i = 1; i <= 40; i++)
            set.Add(i);

        Assert.Equal(40, set.Count);

        for (int i = 1; i <= 40; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(20, set.Count);
        for (int i = 2; i <= 40; i += 2)
            Assert.True(set.Contains(i));
        for (int i = 1; i <= 40; i += 2)
            Assert.False(set.Contains(i));

        for (int i = 1; i <= 40; i += 2)
            set.Add(i);

        Assert.Equal(40, set.Count);
        for (int i = 1; i <= 40; i++)
            Assert.True(set.Contains(i));
    }

    // ---------------------------------------------------------------
    //  Displacement-specific tests (the Robin Hood "rob" path)
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldDisplaceRicherResident_AndKeepEveryElementReachable()
    {
        // Identity hasher + size 8 (mask 7). Elements 8 and 16 both have ideal slot
        // 0; element 1 has ideal slot 1. Inserting 8, 16 fills slots 0,1
        // (distances 0,1). Inserting 1 (ideal slot 1, distance 0) meets element 16
        // at slot 1 with distance 1 > 0, so 1 must NOT displace it; it walks to slot
        // 2. Whichever way the swaps fall, every element stays reachable.
        var set = new RobinHoodSet<int, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        int[] keys = { 8, 16, 1, 9, 24, 17 };
        foreach (int k in keys)
            set.Add(k);

        Assert.Equal(keys.Length, set.Count);
        foreach (int k in keys)
            Assert.True(set.Contains(k));
    }

    [Fact]
    public void NegativeLookup_ShouldTerminate_OnClusteredElements()
    {
        // A clustered identity-hashed table where the absent element's ideal slot is
        // occupied by a longer-distance resident: the Robin Hood early-exit must
        // stop the probe rather than scanning the whole cluster — the test asserts
        // correctness (Contains false), which is what the early-exit must preserve.
        var set = new RobinHoodSet<int, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        foreach (int k in new[] { 8, 16, 24, 1 })
            set.Add(k);

        Assert.False(set.Contains(32)); // ideal slot 0, never inserted
        Assert.False(set.Contains(40)); // ideal slot 0
        Assert.False(set.Contains(7));  // ideal slot 7, empty
    }

    [Fact]
    public void BackwardShift_ShouldKeepDisplacedElementsReachable()
    {
        // Build a wrap-around cluster with the identity hasher, then remove an early
        // member so the backward-shift pulls the tail back and decrements each
        // moved element's probe distance. Every survivor must remain reachable.
        var set = new RobinHoodSet<int, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        int[] keys = { 6, 7, 14, 15, 22, 23 }; // ideal slots 6,7,6,7,6,7 — heavy clustering
        foreach (int k in keys)
            set.Add(k);

        Assert.True(set.Remove(7));
        Assert.False(set.Contains(7));
        Assert.Equal(5, set.Count);
        foreach (int k in keys)
        {
            if (k == 7) continue;
            Assert.True(set.Contains(k));
        }

        // Reinsert and confirm the table is still consistent.
        set.Add(7);
        Assert.Equal(6, set.Count);
        foreach (int k in keys)
            Assert.True(set.Contains(k));
    }

    // ---------------------------------------------------------------
    //  String elements — exercises default(string) == null path
    // ---------------------------------------------------------------

    [Fact]
    public void StringElement_Null_ShouldRoundTrip()
    {
        var set = new RobinHoodSet<string, ConstantStringHasher>();
        set.Add(null!);

        Assert.True(set.Contains(null!));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void StringElement_Null_ShouldCoexistWithNonNullElements()
    {
        var set = new RobinHoodSet<string, ConstantStringHasher>(16);
        set.Add(null!);
        set.Add("alpha");
        set.Add("beta");
        set.Add("gamma");

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(null!));
        Assert.True(set.Contains("alpha"));
        Assert.True(set.Contains("beta"));
        Assert.True(set.Contains("gamma"));
    }

    [Fact]
    public void StringElement_Null_Remove_ShouldWork()
    {
        var set = new RobinHoodSet<string, ConstantStringHasher>(16);
        set.Add(null!);
        set.Add("a");

        Assert.True(set.Remove(null!));
        Assert.False(set.Contains(null!));
        Assert.Equal(1, set.Count);
        Assert.True(set.Contains("a"));
    }

    [Fact]
    public void StringElement_Clear_ShouldResetNullElement()
    {
        var set = new RobinHoodSet<string, ConstantStringHasher>();
        set.Add(null!);
        set.Add("x");

        set.Clear();

        Assert.Equal(0, set.Count);
        Assert.False(set.Contains(null!));
        Assert.False(set.Contains("x"));

        set.Add(null!);
        Assert.Equal(1, set.Count);
        Assert.True(set.Contains(null!));
    }

    // ---------------------------------------------------------------
    //  Differential fuzz against HashSet<int> — the strongest
    //  correctness check on the displacement / shift-back machinery.
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void RandomizedOps_ShouldMatchBclHashSet(int seed)
    {
        var rng = new Random(seed);
        var oracle = new HashSet<int>();
        // Small element universe forces collisions and long probe chains, exercising
        // displacement and backward-shift far more than a sparse element space would.
        var set = new RobinHoodSet<int, IdentityIntHasher>(capacity: 8, loadFactor: 0.75f);

        for (int step = 0; step < 5000; step++)
        {
            int key = rng.Next(0, 64);
            int op = rng.Next(0, 3);
            switch (op)
            {
                case 0: // add
                    bool oAdded = oracle.Add(key);
                    bool mAdded = set.TryAdd(key);
                    Assert.Equal(oAdded, mAdded);
                    break;
                case 1: // remove
                    bool oRemoved = oracle.Remove(key);
                    bool mRemoved = set.Remove(key);
                    Assert.Equal(oRemoved, mRemoved);
                    break;
                case 2: // lookup
                    Assert.Equal(oracle.Contains(key), set.Contains(key));
                    break;
            }

            Assert.Equal(oracle.Count, set.Count);
        }

        // Final full reconciliation.
        Assert.Equal(oracle.Count, set.Count);
        foreach (int k in oracle)
            Assert.True(set.Contains(k));
        foreach (int k in set)
            Assert.Contains(k, oracle);
    }
}
