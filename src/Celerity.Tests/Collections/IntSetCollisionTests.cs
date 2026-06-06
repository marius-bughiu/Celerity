using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="IntSet{THasher}"/>
/// under maximum hash collision pressure.
/// </summary>
public class IntSetCollisionTests
{
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    /// <summary>
    /// A test-only hasher that returns the key itself. Combined with a
    /// power-of-two table size, <c>key &amp; mask</c> places each key at a
    /// predictable slot, which lets a test build a wrapped cluster whose
    /// entries have different natural slots — the shape needed to exercise
    /// every branch of the backward-shift cyclic comparison.
    /// </summary>
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var set = new IntSet<ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            set.Add(i);

        Assert.Equal(10, set.Count);
        for (int i = 1; i <= 10; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Remove_ShouldRehashCluster_UnderFullCollision()
    {
        var set = new IntSet<ConstantIntHasher>(16);
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
        var set = new IntSet<ConstantIntHasher>(8);

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
    public void Zero_ShouldWorkAlongside_CollisionChain()
    {
        var set = new IntSet<ConstantIntHasher>(16);
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
        var set = new IntSet<ConstantIntHasher>(
            capacity: 4, loadFactor: 0.5f);

        for (int i = 1; i <= 20; i++)
            set.Add(i);

        Assert.Equal(20, set.Count);
        for (int i = 1; i <= 20; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemainingItems_UnderFullCollision()
    {
        // Regression for the tightened Resize / RehashAfterRemove rewrite
        // (issue #83): both paths now reinsert directly into the slots array
        // without going through InsertNonZero, so they skip the equality check
        // in the probe walk and don't touch _count / _version per entry.
        // Mirrors the dictionary cases under maximum collision pressure:
        // bulk-add past the threshold to force multiple Resize calls, remove
        // every other item to drive RehashAfterRemove through long clusters,
        // then re-add and verify.
        var set = new IntSet<ConstantIntHasher>(
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

    [Fact]
    public void Remove_WrapAroundCluster_KeepsBypassEntriesPut_AndShiftsTheRest()
    {
        // Regression for the backward-shift deletion rewrite of
        // RehashAfterRemove (replaced by BackwardShiftRemove). With table
        // size 8 (mask = 7) and the identity hasher, items 6, 7, 8, 14 build
        // a cluster that crosses the array boundary:
        //   slot 6 -> 6  (natural 6)
        //   slot 7 -> 7  (natural 7)
        //   slot 0 -> 8  (natural 0; collided through 6, 7)
        //   slot 1 -> 14 (natural 6; displaced through 6, 7, 0)
        // Removing 6 must SKIP slots 7 and 0 (bypass cases for the
        // `i <= j` and `i > j` branches respectively) and SHIFT slot 1 into
        // the gap.
        var set = new IntSet<IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        set.Add(6);
        set.Add(7);
        set.Add(8);
        set.Add(14);

        Assert.True(set.Remove(6));

        Assert.Equal(3, set.Count);
        Assert.False(set.Contains(6));
        Assert.True(set.Contains(7));
        Assert.True(set.Contains(8));
        Assert.True(set.Contains(14));
    }

    [Fact]
    public void Remove_WrapAroundCluster_WithHomeAboveGap_ExercisesWrappedKeepBranch()
    {
        // Complements the test above. Table size 8 (mask = 7), identity hasher.
        // Items 6, 7, 15, 23 land as:
        //   slot 6 -> 6  (natural 6) — removed, becomes the gap at i = 6
        //   slot 7 -> 7  (natural 7)
        //   slot 0 -> 15 (natural 7; wrapped past 7)
        //   slot 1 -> 23 (natural 7; wrapped past 7, 0)
        // Removing 6 scans the wrapped slots 0 and 1, whose natural slot (7) is
        // GREATER than the gap (6), so the `i > j` bypass takes its `i < k` ==
        // true path — the branch the all-homes-below-the-gap cluster misses.
        var set = new IntSet<IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        set.Add(6);
        set.Add(7);
        set.Add(15);
        set.Add(23);

        Assert.True(set.Remove(6));

        Assert.Equal(3, set.Count);
        Assert.False(set.Contains(6));
        Assert.True(set.Contains(7));
        Assert.True(set.Contains(15));
        Assert.True(set.Contains(23));
    }
}
