using Celerity.Collections;
using Celerity.Hashing;
using static Celerity.Tests.Collections.MinimalPerfectHashTests;

namespace Celerity.Tests.Collections;

/// <summary>
/// Worst-case hashing tests for <see cref="MinimalPerfectHash{T,THasher}"/>. No level can separate two keys
/// whose 64-bit hashes are equal, so those keys are answered from an exact table — and the levels must still
/// mark every position such a hash lands on as taken, or a lookup of one of those keys could stop at another
/// key's bit and return that key's index. These tests drive that path with hashers that collide on purpose.
/// </summary>
public class MinimalPerfectHashCollisionTests
{
    // Every key shares one hash: nothing can be placed in a level.
    private struct ConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    // Keys below 200 collide in pairs (0 with 1, 2 with 3, ...); every other key hashes to itself, which the
    // 32-bit path then avalanches.
    private struct LowPairsHasher : IHashProvider<int>
    {
        public int Hash(int key) => key < 200 ? key >> 1 : key;
    }

    // Supplies the raw 64-bit hash, so a test can choose level-0 positions exactly: in a 64-bit level a key
    // lands on bit (hash >> 58).
    private struct RawHasher : IHashProvider<int>, IHashProvider64<int>
    {
        public int Hash(int key) => key;

        public ulong Hash64(int key) => (ulong)(uint)key;
    }

    [Fact]
    public void ConstantHasher_AnswersEveryKeyFromTheExactTable()
    {
        int[] keys = [1, 2, 3, 4, 5];
        var mph = new MinimalPerfectHash<int, ConstantHasher>(keys);

        Assert.Equal(5, mph.Count);
        Assert.Equal(0, mph.LevelCount);
        Assert.Equal(5, mph.FallbackCount);
        AssertBijection(mph, keys);
        Assert.Equal(-1, mph.GetIndex(99));
    }

    [Fact]
    public void ConstantHasher_WithDuplicates_CountsDistinctKeys()
    {
        // One run holding equal and unequal keys: it goes to the exact table, which folds the equal ones.
        int[] keys = [7, 7, 8, 7, 8, 9];
        var mph = new MinimalPerfectHash<int, ConstantHasher>(keys);

        Assert.Equal(3, mph.Count);
        Assert.Equal(3, mph.FallbackCount);
        AssertBijection(mph, [7, 8, 9]);
    }

    [Fact]
    public void ConstantHasher_WithOneKeyRepeated_PlacesItInALevel()
    {
        var mph = new MinimalPerfectHash<int, ConstantHasher>([4, 4, 4]);

        Assert.Equal(1, mph.Count);
        Assert.Equal(0, mph.FallbackCount);
        Assert.Equal(1, mph.LevelCount);
        Assert.Equal(0, mph.GetIndex(4));
    }

    [Fact]
    public void CollidingPairs_GoToTheExactTable_AndTheRestToTheLevels()
    {
        int[] keys = Enumerable.Range(0, 5000).ToArray();
        var mph = new MinimalPerfectHash<int, LowPairsHasher>(keys);

        Assert.Equal(5000, mph.Count);
        Assert.True(mph.LevelCount > 0);
        AssertBijection(mph, keys);

        // All 200 colliding keys are in the exact table, whose indices follow the levels' block. A small late
        // level that settles nothing can add a few stragglers to it, so the count is a floor.
        Assert.InRange(mph.FallbackCount, 200, 220);
        for (int key = 0; key < 200; key++)
            Assert.InRange(mph.GetIndex(key), mph.Count - mph.FallbackCount, mph.Count - 1);
    }

    [Fact]
    public void CollidingPairs_NeverBorrowALevelKeysIndex_AtAnyGamma()
    {
        // The poison entry is what keeps a colliding key's positions clear in every level. Many small sets at
        // several gammas give the levels plenty of chances to place another key on one of those positions.
        for (int seed = 0; seed < 40; seed++)
        {
            int[] keys = Enumerable.Range(0, 260 + seed * 17).ToArray();
            foreach (double gamma in new[] { 1.0, 1.5, 3.0 })
            {
                var mph = new MinimalPerfectHash<int, LowPairsHasher>(keys, gamma);
                AssertBijection(mph, keys);
            }
        }
    }

    [Fact]
    public void ALevelThatSettlesNothing_SendsItsKeysToTheExactTable()
    {
        // Two distinct hashes whose top six bits agree collide in the 64-bit level 0, so it settles nothing
        // and construction stops there rather than adding levels.
        var mph = new MinimalPerfectHash<int, RawHasher>([1, 2]);

        Assert.Equal(2, mph.Count);
        Assert.Equal(0, mph.LevelCount);
        Assert.Equal(2, mph.FallbackCount);
        AssertBijection(mph, [1, 2]);
    }

    [Fact]
    public void AStalledLevel_AlongsideGenuineCollisions_KeepsEveryIndexDistinct()
    {
        // Keys 1 and 2 stall level 0 as above while keys 10 and 11 share a hash outright, so the exact table
        // receives both the colliding run and the stalled keys.
        var mph = new MinimalPerfectHash<int, StallAndCollideHasher>([1, 2, 10, 11]);

        Assert.Equal(4, mph.Count);
        Assert.Equal(4, mph.FallbackCount);
        AssertBijection(mph, [1, 2, 10, 11]);
    }

    // 1 and 2 get distinct hashes that collide in a 64-bit level; 10 and 11 share a hash outright.
    private struct StallAndCollideHasher : IHashProvider<int>, IHashProvider64<int>
    {
        public int Hash(int key) => key;

        public ulong Hash64(int key) => key >= 10 ? 0xFFFF_0000_0000_0000UL : (ulong)key;
    }

    // Pairs below 100 collide, as LowPairsHasher; every call is counted, so a test can pin how often the
    // function hashes. The count is per hasher type and only the test below uses this one.
    private struct CountingPairsHasher : IHashProvider<int>
    {
        public static int Calls;

        public int Hash(int key)
        {
            Interlocked.Increment(ref Calls);
            return key < 100 ? key >> 1 : key;
        }
    }

    [Fact]
    public void TheHasherRunsOncePerKey_EvenForKeysInTheExactTable()
    {
        int[] keys = Enumerable.Range(0, 2000).ToArray();
        CountingPairsHasher.Calls = 0;
        var mph = new MinimalPerfectHash<int, CountingPairsHasher>(keys);

        Assert.Equal(keys.Length, CountingPairsHasher.Calls);
        Assert.True(mph.FallbackCount >= 100);

        // A key from the exact table, one from the levels, and non-members: one call each.
        foreach (int probe in new[] { 3, 1500, 7_000_000, -5 })
        {
            CountingPairsHasher.Calls = 0;
            mph.GetIndex(probe);
            Assert.Equal(1, CountingPairsHasher.Calls);
        }
    }

    [Fact]
    public void TheExactTable_IsSizedByDistinctKeys_NotOccurrences()
    {
        // A mixed collision run of 100,000 copies of one key plus one other key holds two distinct keys, and
        // the table must keep two entries, not one per occurrence.
        int[] keys = Enumerable.Repeat(1, 100_000).Append(2).ToArray();
        var mph = new MinimalPerfectHash<int, ConstantHasher>(keys);

        Assert.Equal(2, mph.Count);
        Assert.Equal(2, mph.FallbackCount);
        AssertBijection(mph, [1, 2]);

        var stored = (Array)typeof(MinimalPerfectHash<int, ConstantHasher>)
            .GetField("_fallbackKeys", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(mph)!;
        Assert.Equal(2, stored.Length);
    }

    [Fact]
    public void ManyDistinctKeysSharingOneHash_BuildInLinearTime()
    {
        // 50,000 distinct keys under a constant hasher, each given twice: folding them pairwise would cost
        // over a billion comparisons. Lookups still scan the one shared-hash run, as in any hash table under
        // a constant hasher, so only a sample is checked.
        int[] keys = Enumerable.Range(0, 50_000).SelectMany(i => new[] { i, i }).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var mph = new MinimalPerfectHash<int, ConstantHasher>(keys);
        sw.Stop();

        Assert.Equal(50_000, mph.Count);
        Assert.Equal(50_000, mph.FallbackCount);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"construction took {sw.Elapsed}");

        var sample = Enumerable.Range(0, 50).Select(i => i * 997).ToArray();
        Assert.Equal(sample.Length, sample.Select(mph.GetIndex).Distinct().Count());
        Assert.All(sample, k => Assert.InRange(mph.GetIndex(k), 0, mph.Count - 1));
    }

    [Fact]
    public void NonMembers_MissTheExactTable_WhicheverSideOfItTheirHashFalls()
    {
        // The table holds hashes 1 and 2; probes hash below, between-and-equal-but-different, and above them.
        var mph = new MinimalPerfectHash<int, RawHasher>([1, 2]);

        Assert.Equal(-1, mph.GetIndex(0));
        Assert.Equal(-1, mph.GetIndex(3));
        Assert.Equal(-1, mph.GetIndex(int.MaxValue));

        var constant = new MinimalPerfectHash<int, ConstantHasher>([10, 20]);
        Assert.Equal(-1, constant.GetIndex(30)); // same hash as both entries, equal to neither
    }

    [Fact]
    public void A32BitHasher_ManyKeys_HandlesItsBirthdayCollisions()
    {
        // 300,000 distinct strings through a 32-bit hasher: a handful of full-hash collisions are expected
        // (about n^2 / 2^33 ≈ 10), and each must land in the exact table without disturbing the levels.
        string[] keys = Enumerable.Range(0, 300_000).Select(i => $"k{i:x}-{i * 2654435761u:x}").ToArray();
        var mph = new MinimalPerfectHash<string, StringFnV1AHasher>(keys);

        Assert.Equal(keys.Length, mph.Count);
        AssertBijection(mph, keys);
    }
}
