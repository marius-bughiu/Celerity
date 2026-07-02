using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="SwissSet{T, THasher}"/> under maximum
/// hash-collision pressure, with reference-type (null-default) elements, and
/// against a <see cref="HashSet{T}"/> oracle. The parts unique to Swiss-table
/// probing — SIMD group scanning that overflows a single group, tombstone
/// deletion that must not terminate a later lookup, and the tombstone-reclaiming
/// rehash — get the heaviest coverage here. Mirrors SwissDictionaryCollisionTests.
/// </summary>
public class SwissSetCollisionTests
{
    /// <summary>
    /// A test-only hasher that returns a constant value for every element, forcing
    /// every insert into the same home group so the probe overflows into
    /// subsequent groups (the multi-group probe path).
    /// </summary>
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    /// <summary>
    /// A test-only hasher for <see cref="string"/> elements that returns a
    /// constant value, forcing full collision into one home group.
    /// </summary>
    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    /// <summary>
    /// A test-only hasher that returns the element itself, so the home group is a
    /// predictable function of the element — used to build clusters that straddle
    /// group boundaries, the shape that drives the group-overflow probe.
    /// </summary>
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    // ---------------------------------------------------------------
    //  Full-collision tests — every element shares a home group
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var set = new SwissSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 40; i++)
            set.Add(i);

        Assert.Equal(40, set.Count);
        for (int i = 1; i <= 40; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void DuplicateAdd_ShouldReturnFalse_UnderFullCollision()
    {
        var set = new SwissSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 20; i++)
            Assert.True(set.TryAdd(i));
        for (int i = 1; i <= 20; i++)
            Assert.False(set.TryAdd(i));

        Assert.Equal(20, set.Count);
    }

    [Fact]
    public void Remove_ShouldLeaveOthersReachable_UnderFullCollision()
    {
        var set = new SwissSet<int, ConstantIntHasher>(64);
        for (int i = 1; i <= 30; i++)
            set.Add(i);

        Assert.True(set.Remove(3));
        Assert.Equal(29, set.Count);
        Assert.False(set.Contains(3));

        for (int i = 1; i <= 30; i++)
        {
            if (i == 3) continue;
            Assert.True(set.Contains(i));
        }
    }

    [Fact]
    public void RemoveThenReinsert_ShouldWork_UnderFullCollision()
    {
        var set = new SwissSet<int, ConstantIntHasher>(16);

        for (int i = 1; i <= 30; i++)
            set.Add(i);

        for (int i = 1; i <= 30; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(15, set.Count);

        // Reinserting must reuse the tombstones the removals left behind.
        for (int i = 1; i <= 30; i += 2)
            set.Add(i);

        Assert.Equal(30, set.Count);
        for (int i = 1; i <= 30; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void DefaultKey_ShouldWorkAlongside_CollisionChain()
    {
        var set = new SwissSet<int, ConstantIntHasher>(16);
        set.Add(0);
        for (int i = 1; i <= 20; i++)
            set.Add(i);

        Assert.Equal(21, set.Count);
        Assert.True(set.Contains(0));
        for (int i = 1; i <= 20; i++)
            Assert.True(set.Contains(i));

        Assert.True(set.Remove(0));
        Assert.Equal(20, set.Count);
        Assert.False(set.Contains(0));
        for (int i = 1; i <= 20; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Resize_ShouldPreserveAll_UnderFullCollision()
    {
        var set = new SwissSet<int, ConstantIntHasher>(
            capacity: 16, loadFactor: 0.5f);

        for (int i = 1; i <= 60; i++)
            set.Add(i);

        Assert.Equal(60, set.Count);
        for (int i = 1; i <= 60; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemaining_UnderFullCollision()
    {
        var set = new SwissSet<int, ConstantIntHasher>(
            capacity: 16, loadFactor: 0.5f);

        for (int i = 1; i <= 80; i++)
            set.Add(i);

        Assert.Equal(80, set.Count);

        for (int i = 1; i <= 80; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(40, set.Count);
        for (int i = 2; i <= 80; i += 2)
            Assert.True(set.Contains(i));
        for (int i = 1; i <= 80; i += 2)
            Assert.False(set.Contains(i));

        for (int i = 1; i <= 80; i += 2)
            set.Add(i);

        Assert.Equal(80, set.Count);
        for (int i = 1; i <= 80; i++)
            Assert.True(set.Contains(i));
    }

    // ---------------------------------------------------------------
    //  Tombstone-specific behaviour
    // ---------------------------------------------------------------

    [Fact]
    public void Lookup_ShouldFindElement_PastATombstone_UnderFullCollision()
    {
        // Fill one home group so it overflows, then delete an early entry: its
        // slot becomes a tombstone (the group has no empty), and a later resident
        // must still be reachable by probing *past* the tombstone.
        var set = new SwissSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 20; i++)
            set.Add(i);

        Assert.True(set.Remove(2));   // leaves a tombstone in the full home group
        Assert.True(set.Remove(5));

        for (int i = 1; i <= 20; i++)
        {
            if (i == 2 || i == 5)
            {
                Assert.False(set.Contains(i));
                continue;
            }
            Assert.True(set.Contains(i));
        }
    }

    [Fact]
    public void ChurnOfInsertRemove_ShouldNotGrowUnbounded_NorCorrupt()
    {
        // Many insert/remove cycles over a tiny element set stress the
        // tombstone-reclaiming rehash; correctness must hold throughout.
        var set = new SwissSet<int, ConstantIntHasher>(16);
        for (int round = 0; round < 500; round++)
        {
            for (int i = 1; i <= 10; i++)
                set.Add(i);
            for (int i = 1; i <= 10; i++)
                Assert.True(set.Remove(i));
            Assert.Equal(0, set.Count);
        }

        for (int i = 1; i <= 10; i++)
            set.Add(i);
        Assert.Equal(10, set.Count);
        for (int i = 1; i <= 10; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void TombstoneHeavyResize_ShouldRehashInPlace_NotDouble()
    {
        // Drives the same-size (tombstone-reclaiming) rehash branch of Resize:
        // a resize triggered while the *live* count is below half the threshold
        // must rehash at the current capacity rather than doubling, so churn that
        // accumulates tombstones cannot grow the table without bound.
        //
        // With IdentityIntHasher and capacity 32 there are two SIMD groups
        // (group = (key >> 7) & 1, threshold 24). Keys 128..143 all land in
        // group 1; keys 1..9 all land in group 0.
        var set = new SwissSet<int, IdentityIntHasher>(capacity: 32, loadFactor: 0.75f);

        // Fill group 1 completely, then delete it all: because the group is full,
        // every erase leaves a DELETED tombstone (16 of them), not an empty slot.
        for (int k = 128; k <= 143; k++)
            set.Add(k);
        for (int k = 128; k <= 143; k++)
            Assert.True(set.Remove(k));
        Assert.Equal(0, set.Count);

        // Now fill group 0 (no tombstones on its probe path, so these consume
        // empty slots and drain the growth budget). The insert that finds the
        // budget exhausted triggers a resize while only ~8 entries are live —
        // below threshold/2 — so the in-place rehash path runs and reclaims the
        // 16 group-1 tombstones instead of doubling to capacity 64.
        for (int k = 1; k <= 9; k++)
            set.Add(k);

        Assert.Equal(9, set.Count);
        for (int k = 1; k <= 9; k++)
            Assert.True(set.Contains(k));
        for (int k = 128; k <= 143; k++)
            Assert.False(set.Contains(k));

        // The reclaimed table still works: refill it past the original threshold.
        for (int k = 200; k <= 240; k++)
            set.Add(k);
        Assert.Equal(50, set.Count);
        for (int k = 1; k <= 9; k++)
            Assert.True(set.Contains(k));
        for (int k = 200; k <= 240; k++)
            Assert.True(set.Contains(k));
    }

    // ---------------------------------------------------------------
    //  Group-straddling clusters (identity hasher)
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldOverflowGroup_AndKeepEveryElementReachable()
    {
        var set = new SwissSet<int, IdentityIntHasher>(capacity: 16, loadFactor: 0.9f);
        int[] keys = { 16, 32, 48, 1, 17, 33, 49, 2, 18, 34 };
        foreach (int k in keys)
            set.Add(k);

        Assert.Equal(keys.Length, set.Count);
        foreach (int k in keys)
            Assert.True(set.Contains(k));
    }

    [Fact]
    public void NegativeLookup_ShouldMiss_OnClusteredKeys()
    {
        var set = new SwissSet<int, IdentityIntHasher>(capacity: 16, loadFactor: 0.9f);
        foreach (int k in new[] { 16, 32, 48, 1 })
            set.Add(k);

        Assert.False(set.Contains(64)); // never inserted
        Assert.False(set.Contains(80));
        Assert.False(set.Contains(7));
    }

    // ---------------------------------------------------------------
    //  String elements — exercises default(string) == null path
    // ---------------------------------------------------------------

    [Fact]
    public void StringElement_Null_ShouldRoundTrip()
    {
        var set = new SwissSet<string, ConstantStringHasher>();
        set.Add(null!);

        Assert.True(set.Contains(null!));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void StringElement_Null_ShouldCoexistWithNonNullElements()
    {
        var set = new SwissSet<string, ConstantStringHasher>(16);
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
        var set = new SwissSet<string, ConstantStringHasher>(16);
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
        var set = new SwissSet<string, ConstantStringHasher>();
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
    //  correctness check on the group-probe / tombstone machinery.
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
        // Small element universe forces collisions, group overflows, and dense
        // tombstone reuse far more than a sparse element space would.
        var set = new SwissSet<int, IdentityIntHasher>(capacity: 16, loadFactor: 0.75f);

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
