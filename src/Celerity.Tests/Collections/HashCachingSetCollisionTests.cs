using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="HashCachingSet{T, THasher}"/> under maximum
/// hash-collision pressure, with reference-type (null-default) elements, and
/// against a <see cref="HashSet{T}"/> oracle. HashCachingSet shares CeleritySet's
/// linear-probing + backward-shift machinery, layering a cached-fingerprint side
/// array on top, so it must satisfy the same correctness contract — including the
/// fingerprint short-circuit never letting a colliding element be missed (issue
/// #65). Mirrors HashCachingDictionaryCollisionTests and RobinHoodSetCollisionTests.
/// </summary>
public class HashCachingSetCollisionTests
{
    /// <summary>
    /// A test-only hasher that returns a constant value for every element, forcing
    /// every insert into a single linear-probing chain — and, because every element
    /// then shares the same cached fingerprint, the worst case for the fingerprint
    /// filter (every probe falls through to the full element compare).
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
    /// A test-only hasher that returns the element itself. Combined with a
    /// power-of-two table size, <c>element &amp; mask</c> places each element at a
    /// predictable slot, which lets a test build a wrapped cluster whose entries
    /// have different natural slots — the shape needed to exercise every branch of
    /// the backward-shift cyclic comparison.
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
        var set = new HashCachingSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            set.Add(i);

        Assert.Equal(10, set.Count);
        for (int i = 1; i <= 10; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void DuplicateAdd_ShouldReturnFalse_UnderFullCollision()
    {
        var set = new HashCachingSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            Assert.True(set.TryAdd(i));
        for (int i = 1; i <= 10; i++)
            Assert.False(set.TryAdd(i));

        Assert.Equal(10, set.Count);
    }

    [Fact]
    public void Remove_ShouldShiftCluster_UnderFullCollision()
    {
        var set = new HashCachingSet<int, ConstantIntHasher>(16);
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
        var set = new HashCachingSet<int, ConstantIntHasher>(8);

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
        var set = new HashCachingSet<int, ConstantIntHasher>(16);
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
        var set = new HashCachingSet<int, ConstantIntHasher>(
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
        // Bulk-insert past the load-factor threshold to force multiple Resize calls
        // under a single forced-collision chain — each resize re-homes entries
        // straight from their cached fingerprints without rehashing — then remove
        // every other element to drive BackwardShiftRemove through long clusters,
        // then reinsert and verify.
        var set = new HashCachingSet<int, ConstantIntHasher>(
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
    //  Backward-shift deletion — the cached-fingerprint natural slot
    //  drives the cyclic bypass comparison; every branch must be hit.
    // ---------------------------------------------------------------

    [Fact]
    public void Remove_WrapAroundCluster_KeepsBypassEntriesPut_AndShiftsTheRest()
    {
        // Regression for the backward-shift deletion (BackwardShiftRemove). With
        // table size 8 (mask = 7) and the identity hasher, elements 6, 7, 8, 14 build
        // a cluster that crosses the array boundary:
        //   slot 6 -> 6  (natural 6)
        //   slot 7 -> 7  (natural 7)
        //   slot 0 -> 8  (natural 0; collided through 6, 7)
        //   slot 1 -> 14 (natural 6; displaced through 6, 7, 0)
        // Removing 6 must SKIP slots 7 and 0 (bypass cases for the `i <= j` and
        // `i > j` branches respectively) and SHIFT slot 1 into the gap. The cached
        // fingerprint supplies each candidate's natural slot, so the shift logic
        // never rehashes.
        var set = new HashCachingSet<int, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        foreach (int k in new[] { 6, 7, 8, 14 })
            set.Add(k);

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
        // Elements 6, 7, 15, 23 land as:
        //   slot 6 -> 6  (natural 6) — removed, becomes the gap at i = 6
        //   slot 7 -> 7  (natural 7)
        //   slot 0 -> 15 (natural 7; wrapped past 7)
        //   slot 1 -> 23 (natural 7; wrapped past 7, 0)
        // Removing 6 scans the wrapped slots 0 and 1, whose natural slot (7) is
        // GREATER than the gap (6), so the `i > j` bypass takes its `i < k` == true
        // path — the branch the all-homes-below-the-gap cluster misses.
        var set = new HashCachingSet<int, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        foreach (int k in new[] { 6, 7, 15, 23 })
            set.Add(k);

        Assert.True(set.Remove(6));

        Assert.Equal(3, set.Count);
        Assert.False(set.Contains(6));
        Assert.True(set.Contains(7));
        Assert.True(set.Contains(15));
        Assert.True(set.Contains(23));
    }

    // ---------------------------------------------------------------
    //  Remove-then-reinsert with the standard hasher (parity with
    //  the rest of the family)
    // ---------------------------------------------------------------

    [Fact]
    public void RemoveThenReinsert_ManyElements_ShouldNotLoseEntries()
    {
        var set = new HashCachingSet<int, Int32WangNaiveHasher>(8);
        for (int i = 1; i <= 100; i++)
            set.Add(i);

        for (int i = 1; i <= 100; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(50, set.Count);

        for (int i = 1; i <= 100; i += 2)
            set.Add(i);

        Assert.Equal(100, set.Count);
        for (int i = 1; i <= 100; i++)
            Assert.True(set.Contains(i));
    }

    // ---------------------------------------------------------------
    //  String elements — exercises default(string) == null path
    // ---------------------------------------------------------------

    [Fact]
    public void StringElement_Null_ShouldRoundTrip()
    {
        var set = new HashCachingSet<string, ConstantStringHasher>();
        set.Add(null!);

        Assert.True(set.Contains(null!));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void StringElement_Null_ShouldCoexistWithNonNullElements()
    {
        var set = new HashCachingSet<string, ConstantStringHasher>(16);
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
        var set = new HashCachingSet<string, ConstantStringHasher>(16);
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
        var set = new HashCachingSet<string, ConstantStringHasher>();
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
    //  correctness check on the fingerprint / shift-back machinery.
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
        // the fingerprint filter and backward-shift far more than a sparse element
        // space would.
        var set = new HashCachingSet<int, IdentityIntHasher>(capacity: 8, loadFactor: 0.75f);

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
