using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="LinkedSet{T, THasher}"/> under maximum hash-collision pressure,
/// with reference-type (null-default) elements, and against a <see cref="HashSet{T}"/> oracle.
/// Mirrors <c>LinkedDictionaryCollisionTests</c>: unlinking an entry from the head, middle and tail
/// of a bucket chain (by element and from either end of the order), free-list reuse under churn, and
/// growth that relinks every chain.
/// </summary>
/// <remarks>
/// A new entry is pushed onto the <i>front</i> of its bucket's chain, so under a constant hasher the
/// newest entry heads the chain and the oldest sits at its end. <c>TryRemoveFirst</c> on a full
/// collision therefore removes the entry at the far end of the chain, and <c>TryRemoveLast</c> the
/// one at its head.
/// </remarks>
public class LinkedSetCollisionTests
{
    /// <summary>
    /// A test-only hasher that returns a constant value for every element, forcing every insert into
    /// the same bucket chain.
    /// </summary>
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    /// <summary>
    /// A test-only hasher for <see cref="string"/> elements that returns a constant value, forcing
    /// full collision into one chain with equal stored hashes, so every step falls through to Equals.
    /// </summary>
    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    /// <summary>
    /// A test-only hasher that returns the element itself, so the bucket is a predictable function of
    /// the element.
    /// </summary>
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    private struct ZeroStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 0;
    }

    private static int[] ItemsOf<THasher>(LinkedSet<int, THasher> set)
        where THasher : struct, IHashProvider<int>
    {
        var items = new List<int>();
        foreach (int item in set)
            items.Add(item);
        return items.ToArray();
    }

    private static string?[] ItemsOf<THasher>(LinkedSet<string, THasher> set)
        where THasher : struct, IHashProvider<string>
    {
        var items = new List<string?>();
        foreach (string? item in set)
            items.Add(item);
        return items.ToArray();
    }

    // ---------------------------------------------------------------
    //  Full-collision tests — every element shares one chain
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var set = new LinkedSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 40; i++)
            set.Add(i);

        Assert.Equal(40, set.Count);
        for (int i = 1; i <= 40; i++)
            Assert.True(set.Contains(i));
        Assert.Equal(Enumerable.Range(1, 40), ItemsOf(set));
        Assert.False(set.Contains(41));
    }

    [Fact]
    public void DuplicateAdd_ShouldBeRejected_UnderFullCollision()
    {
        var set = new LinkedSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 20; i++)
            set.Add(i);
        for (int i = 1; i <= 20; i++)
            Assert.False(set.TryAdd(i));

        Assert.Equal(20, set.Count);
        Assert.Equal(Enumerable.Range(1, 20), ItemsOf(set));
    }

    [Fact]
    public void Remove_ShouldLeaveOthersReachable_UnderFullCollision()
    {
        var set = new LinkedSet<int, ConstantIntHasher>(64);
        for (int i = 1; i <= 30; i++)
            set.Add(i);

        Assert.True(set.Remove(3)); // mid-chain
        Assert.Equal(29, set.Count);
        Assert.False(set.Contains(3));
        Assert.False(set.Remove(3));

        for (int i = 1; i <= 30; i++)
            Assert.Equal(i != 3, set.Contains(i));
    }

    [Fact]
    public void Remove_ShouldUnlinkTheChainHeadMiddleAndTail_UnderFullCollision()
    {
        var set = new LinkedSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            set.Add(i);
        // Chain (head → tail): 6, 5, 4, 3, 2, 1.

        Assert.True(set.Remove(6)); // chain head: chainPrev == Nil
        Assert.True(set.Remove(1)); // chain tail: chainPrev != Nil
        Assert.True(set.Remove(3)); // middle

        Assert.Equal(new[] { 2, 4, 5 }, ItemsOf(set));
        foreach (int k in new[] { 2, 4, 5 })
            Assert.True(set.Contains(k));
        foreach (int k in new[] { 1, 3, 6 })
            Assert.False(set.Contains(k));
    }

    [Fact]
    public void RemoveThenReinsert_ShouldWork_UnderFullCollision()
    {
        var set = new LinkedSet<int, ConstantIntHasher>(16);

        for (int i = 1; i <= 30; i++)
            set.Add(i);

        for (int i = 1; i <= 30; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(15, set.Count);
        int capacity = set.EnsureCapacity(0);

        // Reinserting must reuse the slots the removals put on the free list.
        for (int i = 1; i <= 30; i += 2)
            set.Add(i);

        Assert.Equal(30, set.Count);
        Assert.Equal(capacity, set.EnsureCapacity(0));

        // The re-added odd elements go to the end, after every surviving even element.
        var expectedOrder = Enumerable.Range(1, 30).Where(i => i % 2 == 0)
            .Concat(Enumerable.Range(1, 30).Where(i => i % 2 == 1));
        Assert.Equal(expectedOrder, ItemsOf(set));
    }

    [Fact]
    public void DefaultElement_ShouldWorkAlongside_CollisionChain()
    {
        var set = new LinkedSet<int, ConstantIntHasher>(16);
        set.Add(0);
        for (int i = 1; i <= 20; i++)
            set.Add(i);

        Assert.Equal(21, set.Count);
        Assert.Equal(Enumerable.Range(0, 21), ItemsOf(set));

        Assert.True(set.Remove(0));
        Assert.Equal(20, set.Count);
        Assert.False(set.Contains(0));
        for (int i = 1; i <= 20; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Resize_ShouldPreserveAll_UnderFullCollision()
    {
        var set = new LinkedSet<int, ConstantIntHasher>(capacity: 16, loadFactor: 0.5f);

        for (int i = 1; i <= 60; i++)
            set.Add(i);

        Assert.Equal(60, set.Count);
        for (int i = 1; i <= 60; i++)
            Assert.True(set.Contains(i));
        Assert.Equal(Enumerable.Range(1, 60), ItemsOf(set));
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemaining_UnderFullCollision()
    {
        var set = new LinkedSet<int, ConstantIntHasher>(capacity: 16, loadFactor: 0.5f);

        for (int i = 1; i <= 80; i++)
            set.Add(i);

        for (int i = 1; i <= 80; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(40, set.Count);
        for (int i = 1; i <= 80; i++)
            Assert.Equal(i % 2 == 0, set.Contains(i));

        set.TrimExcess(); // rebuild relinks the surviving chain

        for (int i = 1; i <= 80; i += 2)
            set.Add(i);

        Assert.Equal(80, set.Count);
        for (int i = 1; i <= 80; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void ChurnOfInsertRemove_ShouldNotGrowUnbounded_NorCorrupt()
    {
        // Many insert/remove cycles over a tiny element set: every removal frees a slot for the next
        // insert, so the table must never grow past its first size.
        var set = new LinkedSet<int, ConstantIntHasher>(16);
        int capacity = set.EnsureCapacity(0);
        for (int round = 0; round < 500; round++)
        {
            for (int i = 1; i <= 10; i++)
                set.Add(i);
            for (int i = 1; i <= 10; i++)
                Assert.True(set.Remove(i));
            Assert.Empty(set);
        }

        Assert.Equal(capacity, set.EnsureCapacity(0));
        for (int i = 1; i <= 10; i++)
            set.Add(i);
        Assert.Equal(Enumerable.Range(1, 10), ItemsOf(set));
    }

    // ---------------------------------------------------------------
    //  Ordered removal from either end of a collision chain
    // ---------------------------------------------------------------

    [Fact]
    public void TryRemoveFirst_ShouldUnlinkAnEntryAtTheFarEndOfItsChain()
    {
        // The oldest entry sits at the end of the chain, so TryRemoveFirst must walk the chain
        // to find its predecessor (the non-head arm of the chain unlink).
        var set = new LinkedSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 8; i++)
            set.Add(i);

        for (int expected = 1; expected <= 8; expected++)
        {
            Assert.True(set.TryRemoveFirst(out int item));
            Assert.Equal(expected, item);
            for (int k = expected + 1; k <= 8; k++)
                Assert.True(set.Contains(k));
        }

        Assert.Empty(set);
        Assert.False(set.TryRemoveFirst(out _));
    }

    [Fact]
    public void TryRemoveLast_ShouldUnlinkTheHeadOfItsChain()
    {
        var set = new LinkedSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 8; i++)
            set.Add(i);

        for (int expected = 8; expected >= 1; expected--)
        {
            Assert.True(set.TryRemoveLast(out int item));
            Assert.Equal(expected, item);
            for (int k = 1; k < expected; k++)
                Assert.True(set.Contains(k));
        }

        Assert.Empty(set);
        Assert.False(set.TryRemoveLast(out _));
    }

    [Fact]
    public void TryRemoveFirst_ShouldWalkPastAnUnrelatedChainMember_AfterReordering()
    {
        // MoveToFront changes the order list, not the chain: after it, the order head (5) is the
        // chain head, and the order tail (1) is the chain's far end.
        var set = new LinkedSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 5; i++)
            set.Add(i);
        Assert.True(set.MoveToFront(5));
        Assert.True(set.MoveToEnd(1));
        // Order: 5, 2, 3, 4, 1. Chain: 5, 4, 3, 2, 1.

        Assert.True(set.TryRemoveFirst(out int first));
        Assert.Equal(5, first);
        Assert.True(set.TryRemoveLast(out int last));
        Assert.Equal(1, last);
        Assert.True(set.MoveToEnd(2));
        Assert.True(set.TryRemoveLast(out last)); // 2 sits at the end of the chain
        Assert.Equal(2, last);

        Assert.Equal(new[] { 3, 4 }, ItemsOf(set));
        Assert.True(set.Contains(3));
        Assert.True(set.Contains(4));
        Assert.False(set.Contains(1));
        Assert.False(set.Contains(2));
        Assert.False(set.Contains(5));
    }

    [Fact]
    public void TryRemoveLast_ShouldWalkToAMidChainEntry()
    {
        // Order tail mid-chain: after MoveToEnd(3) the newest element sits in the middle of the chain.
        var set = new LinkedSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 5; i++)
            set.Add(i);
        Assert.True(set.MoveToEnd(3));
        // Order: 1, 2, 4, 5, 3. Chain: 5, 4, 3, 2, 1.

        Assert.True(set.TryRemoveLast(out int last));
        Assert.Equal(3, last);
        Assert.Equal(new[] { 1, 2, 4, 5 }, ItemsOf(set));
        foreach (int k in new[] { 1, 2, 4, 5 })
            Assert.True(set.Contains(k));
    }

    [Fact]
    public void TryRemoveFirst_ShouldFindTheEntry_InASharedBucketWithDifferentHashes()
    {
        // IdentityIntHasher at 4 buckets: elements 1, 5, 9 share bucket 1 with distinct full hashes.
        var set = new LinkedSet<int, IdentityIntHasher>(capacity: 4, loadFactor: 0.75f);
        set.Add(1);
        set.Add(5);
        set.Add(9); // chain: 9, 5, 1

        Assert.True(set.TryRemoveFirst(out int item));
        Assert.Equal(1, item);
        Assert.Equal(new[] { 5, 9 }, ItemsOf(set));
        Assert.True(set.Contains(5));
        Assert.True(set.Contains(9));
    }

    // ---------------------------------------------------------------
    //  Shared-bucket clusters (identity hasher)
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldChainInOneBucket_AndKeepEveryElementReachable()
    {
        var set = new LinkedSet<int, IdentityIntHasher>(capacity: 16, loadFactor: 0.9f);
        int[] items = { 16, 32, 48, 1, 17, 33, 49, 2, 18, 34 };
        foreach (int k in items)
            set.Add(k);

        Assert.Equal(items.Length, set.Count);
        foreach (int k in items)
            Assert.True(set.Contains(k));
        Assert.Equal(items, ItemsOf(set));
    }

    [Fact]
    public void NegativeLookup_ShouldMiss_OnClusteredElements()
    {
        var set = new LinkedSet<int, IdentityIntHasher>(capacity: 16, loadFactor: 0.9f);
        foreach (int k in new[] { 16, 32, 48, 1 })
            set.Add(k);

        Assert.False(set.Contains(64)); // never inserted, same bucket as 16/32/48
        Assert.False(set.Contains(80));
        Assert.False(set.Contains(7));  // empty bucket
        Assert.False(set.Remove(64));
        Assert.False(set.MoveToEnd(64));
        Assert.False(set.MoveToFront(64));
    }

    // ---------------------------------------------------------------
    //  String elements — default(string) == null and the stored-hash test
    // ---------------------------------------------------------------

    [Fact]
    public void StringElement_Null_ShouldCoexistWithNonNullElements()
    {
        var set = new LinkedSet<string, ConstantStringHasher>(16);
        set.Add(null!);
        set.Add("alpha");
        set.Add("beta");
        set.Add("gamma");

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(null!));
        Assert.True(set.Contains("alpha"));
        Assert.False(set.Contains("delta"));
        Assert.Equal(new[] { null, "alpha", "beta", "gamma" }, ItemsOf(set));
    }

    [Fact]
    public void StringElement_Null_Remove_ShouldWork()
    {
        var set = new LinkedSet<string, ConstantStringHasher>(16);
        set.Add(null!);
        set.Add("a");

        Assert.True(set.Remove(null!));
        Assert.False(set.Contains(null!));
        Assert.Single(set);
        Assert.True(set.Contains("a"));
    }

    [Fact]
    public void StringElement_Clear_ShouldResetNullElement()
    {
        var set = new LinkedSet<string, ConstantStringHasher>();
        set.Add(null!);
        set.Add("x");

        set.Clear();

        Assert.Empty(set);
        Assert.False(set.Contains(null!));
        Assert.False(set.Contains("x"));

        set.Add(null!);
        Assert.Single(set);
        Assert.True(set.Contains(null!));
    }

    [Fact]
    public void StringElement_SharedBucket_ShouldSkipBothHashMismatchesAndElementMismatches()
    {
        // CacheCollidingStringHasher puts every element in bucket 0 with one of two full hashes by
        // length parity, so a reference-element lookup meets both a stored-hash mismatch (skipped
        // without Equals) and a stored-hash match whose element differs.
        var set = new LinkedSet<string, CacheCollidingStringHasher>(16);
        string[] items = { "a", "bb", "ccc", "dddd", "e", "ff" };
        foreach (string s in items)
            set.Add(s);

        foreach (string s in items)
            Assert.True(set.Contains(s));
        Assert.False(set.Contains("g"));   // odd length: walks past even-hash and odd-hash entries
        Assert.False(set.Contains("gg"));  // even length
        Assert.False(set.TryAdd("bb"));

        Assert.True(set.Remove("a"));       // the far end of the chain
        Assert.True(set.Remove("dddd"));    // mid-chain
        Assert.False(set.Remove("zz"));
        Assert.Equal(new[] { "bb", "ccc", "e", "ff" }, ItemsOf(set));

        Assert.True(set.TryRemoveFirst(out string? first)); // "bb" sits mid-chain now
        Assert.Equal("bb", first);
        Assert.Equal(new[] { "ccc", "e", "ff" }, ItemsOf(set));
    }

    [Fact]
    public void StringElement_NullAndHashZeroElementsShareABucket_WithoutConfusion()
    {
        // The null element is stored under hash 0. With a constant hasher of 0, every other element
        // has the same stored hash, so only Equals tells them apart.
        var set = new LinkedSet<string, ZeroStringHasher>(16);
        set.Add("x");
        set.Add(null!);
        set.Add("y");

        Assert.True(set.Contains("x"));
        Assert.True(set.Contains(null!));
        Assert.True(set.Contains("y"));
        Assert.True(set.Remove(null!));
        Assert.False(set.Contains(null!));
        Assert.Equal(new[] { "x", "y" }, ItemsOf(set));
    }

    // ---------------------------------------------------------------
    //  Seeded randomized check against HashSet<int>. The CsCheck property
    //  (shrinking, full order model) lives in LinkedSetDifferentialTests.
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
        // A small element universe on an identity hasher keeps chains long and the free list busy.
        var set = new LinkedSet<int, IdentityIntHasher>(capacity: 16, loadFactor: 0.75f);

        for (int step = 0; step < 5000; step++)
        {
            int item = rng.Next(0, 64);
            switch (rng.Next(0, 3))
            {
                case 0:
                    Assert.Equal(oracle.Add(item), set.TryAdd(item));
                    break;
                case 1:
                    Assert.Equal(oracle.Remove(item), set.Remove(item));
                    break;
                case 2:
                    Assert.Equal(oracle.Contains(item), set.Contains(item));
                    break;
            }

            Assert.Equal(oracle.Count, set.Count);
        }

        Assert.True(set.SetEquals(oracle));
        foreach (int item in set)
            Assert.Contains(item, oracle);
    }
}
