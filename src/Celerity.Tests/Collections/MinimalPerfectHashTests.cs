using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class MinimalPerfectHashTests
{
    // Asserts the defining property: every key of the set gets its own index, and the indices are exactly
    // [0, Count).
    internal static void AssertBijection<T, THasher>(MinimalPerfectHash<T, THasher> mph, IEnumerable<T> distinctKeys)
        where THasher : struct, IHashProvider<T>
    {
        var seen = new bool[mph.Count];
        int keys = 0;
        foreach (T key in distinctKeys)
        {
            int index = mph.GetIndex(key);
            Assert.InRange(index, 0, mph.Count - 1);
            Assert.False(seen[index], $"index {index} was given to two keys (second: {key})");
            seen[index] = true;
            keys++;
        }

        Assert.Equal(mph.Count, keys);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(1000)]
    [InlineData(100_000)]
    public void EveryKey_GetsADistinctIndexInRange(int n)
    {
        int[] keys = Enumerable.Range(0, n).Select(i => i * 7919 + 13).ToArray();
        var mph = new MinimalPerfectHash<int, Int32Murmur3Hasher>(keys);

        Assert.Equal(n, mph.Count);
        AssertBijection(mph, keys);
    }

    [Fact]
    public void GetIndex_IsStable()
    {
        int[] keys = Enumerable.Range(0, 500).ToArray();
        var mph = new MinimalPerfectHash<int, Int32Murmur3Hasher>(keys);

        foreach (int key in keys)
            Assert.Equal(mph.GetIndex(key), mph.GetIndex(key));
    }

    [Fact]
    public void StringKeys_WithA64BitHasher_FormABijection()
    {
        string[] keys = Enumerable.Range(0, 20_000).Select(i => $"https://example.com/item/{i}").ToArray();
        var mph = new MinimalPerfectHash<string, StringXxHash3Hasher>(keys);

        Assert.Equal(keys.Length, mph.Count);
        Assert.Equal(0, mph.FallbackCount);
        AssertBijection(mph, keys);
    }

    [Fact]
    public void StringKeys_WithA32BitHasher_FormABijection()
    {
        string[] keys = Enumerable.Range(0, 20_000).Select(i => $"word-{i}").ToArray();
        var mph = new MinimalPerfectHash<string, StringFnV1AHasher>(keys);

        Assert.Equal(keys.Length, mph.Count);
        AssertBijection(mph, keys);
    }

    [Fact]
    public void ValuesArray_IndexedByGetIndex_RoundTrips()
    {
        // The intended use: a dense value array addressed by the function, with the keys stored alongside
        // so a non-member can be told apart.
        string[] words = ["alpha", "beta", "gamma", "delta", "epsilon"];
        var mph = new MinimalPerfectHash<string, StringXxHash3Hasher>(words);
        var storedKeys = new string[mph.Count];
        var lengths = new int[mph.Count];
        foreach (string w in words)
        {
            int i = mph.GetIndex(w);
            storedKeys[i] = w;
            lengths[i] = w.Length;
        }

        foreach (string w in words)
            Assert.Equal(w.Length, lengths[mph.GetIndex(w)]);

        int probe = mph.GetIndex("zeta");
        Assert.True(probe == -1 || storedKeys[probe] != "zeta");
    }

    [Fact]
    public void NonMember_ReturnsMinusOneOrAnIndexInRange()
    {
        int[] keys = Enumerable.Range(0, 1000).ToArray();
        var mph = new MinimalPerfectHash<int, Int32Murmur3Hasher>(keys);

        bool sawMinusOne = false;
        bool sawInRange = false;
        for (int probe = 1_000_000; probe < 1_010_000; probe++)
        {
            int index = mph.GetIndex(probe);
            Assert.InRange(index, -1, mph.Count - 1);
            sawMinusOne |= index == -1;
            sawInRange |= index >= 0;
        }

        // Both outcomes occur: a non-member that misses every level falls through to -1, and one that lands
        // on a set bit borrows that key's index.
        Assert.True(sawMinusOne);
        Assert.True(sawInRange);
    }

    [Fact]
    public void ZeroAndDefaultKeys_AreOrdinaryKeys()
    {
        var ints = new MinimalPerfectHash<int, Int32Murmur3Hasher>([0, 1, 2]);
        AssertBijection(ints, [0, 1, 2]);

        var guids = new MinimalPerfectHash<Guid, GuidHasher>([Guid.Empty, Guid.NewGuid()]);
        Assert.Equal(2, guids.Count);
        Assert.InRange(guids.GetIndex(Guid.Empty), 0, 1);
    }

    [Fact]
    public void NullReference_IsAKey_WithoutInvokingTheHasher()
    {
        // StringFnV1AHasher throws on null; the function maps null to a fixed hash instead.
        string[] keys = ["alice", null!, "bob", string.Empty];
        var mph = new MinimalPerfectHash<string, StringFnV1AHasher>(keys);

        Assert.Equal(4, mph.Count);
        AssertBijection(mph, keys);
    }

    [Fact]
    public void Duplicates_CollapseToOneIndex()
    {
        int[] keys = [5, 9, 5, 5, 12, 9];
        var mph = new MinimalPerfectHash<int, Int32Murmur3Hasher>(keys);

        Assert.Equal(3, mph.Count);
        Assert.Equal(0, mph.FallbackCount); // equal keys are folded up front, not sent to the exact table
        AssertBijection(mph, [5, 9, 12]);
    }

    [Fact]
    public void ASourceOfOnlyDuplicates_StillUsesTheLevels()
    {
        // Every key given twice: none may end up in the exact table, or the function would store them all.
        int[] keys = Enumerable.Range(0, 5000).SelectMany(i => new[] { i, i }).ToArray();
        var mph = new MinimalPerfectHash<int, Int32Murmur3Hasher>(keys);

        Assert.Equal(5000, mph.Count);
        Assert.Equal(0, mph.FallbackCount);
        AssertBijection(mph, Enumerable.Range(0, 5000));
    }

    [Fact]
    public void EmptySource_HasNoIndices()
    {
        var mph = new MinimalPerfectHash<int, Int32Murmur3Hasher>(Array.Empty<int>());

        Assert.Equal(0, mph.Count);
        Assert.Equal(0, mph.LevelCount);
        Assert.Equal(0, mph.FallbackCount);
        Assert.Equal(0d, mph.BitsPerKey);
        Assert.Equal(-1, mph.GetIndex(0));
        Assert.Equal(-1, mph.GetIndex(42));
    }

    [Fact]
    public void SingleKey_GetsIndexZero()
    {
        var mph = new MinimalPerfectHash<int, Int32Murmur3Hasher>([12345]);
        Assert.Equal(1, mph.Count);
        Assert.Equal(0, mph.GetIndex(12345));
    }

    [Fact]
    public void NonArraySource_IsMaterializedOnce()
    {
        int enumerations = 0;
        IEnumerable<int> Source()
        {
            enumerations++;
            for (int i = 0; i < 2000; i++)
                yield return i;
        }

        var mph = new MinimalPerfectHash<int, Int32Murmur3Hasher>(Source());

        Assert.Equal(1, enumerations);
        Assert.Equal(2000, mph.Count);
        AssertBijection(mph, Enumerable.Range(0, 2000));
    }

    // ---------------------------------------------------------------
    //  Sizing and gamma
    // ---------------------------------------------------------------

    [Fact]
    public void DefaultGamma_IsTwo()
    {
        var mph = new MinimalPerfectHash<int, Int32Murmur3Hasher>([1, 2, 3]);
        Assert.Equal(MinimalPerfectHash<int, Int32Murmur3Hasher>.DefaultGamma, mph.Gamma);
        Assert.Equal(2.0, mph.Gamma);
    }

    [Theory]
    [InlineData(1.0, 2.9, 3.8)]
    [InlineData(2.0, 3.5, 4.4)]
    [InlineData(3.0, 4.2, 5.4)]
    public void BitsPerKey_MatchesTheGammaBudget(double gamma, double low, double high)
    {
        // BBHash spends about gamma * e^(1/gamma) bits per key across its levels, plus 12.5% for the
        // popcount index; checked at a size where the 64-bit floor on small levels no longer matters.
        int[] keys = Enumerable.Range(0, 200_000).ToArray();
        var mph = new MinimalPerfectHash<int, Int32Murmur3Hasher>(keys, gamma);

        Assert.Equal(gamma, mph.Gamma);
        Assert.InRange(mph.BitsPerKey, low, high);
        AssertBijection(mph, keys);
    }

    [Fact]
    public void LargerGamma_UsesFewerLevels()
    {
        int[] keys = Enumerable.Range(0, 100_000).ToArray();
        var tight = new MinimalPerfectHash<int, Int32Murmur3Hasher>(keys, 1.0);
        var loose = new MinimalPerfectHash<int, Int32Murmur3Hasher>(keys, 5.0);

        Assert.True(loose.LevelCount < tight.LevelCount,
            $"gamma 5 used {loose.LevelCount} levels, gamma 1 used {tight.LevelCount}");
        Assert.True(loose.BitsPerKey > tight.BitsPerKey);
    }

    [Fact]
    public void GammaBounds_AreAccepted()
    {
        int[] keys = Enumerable.Range(0, 300).ToArray();
        AssertBijection(new MinimalPerfectHash<int, Int32Murmur3Hasher>(keys, MinimalPerfectHash<int, Int32Murmur3Hasher>.MinGamma), keys);
        AssertBijection(new MinimalPerfectHash<int, Int32Murmur3Hasher>(keys, MinimalPerfectHash<int, Int32Murmur3Hasher>.MaxGamma), keys);
    }

    // ---------------------------------------------------------------
    //  Constructor validation
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MinimalPerfectHash<int, Int32Murmur3Hasher>(null!));
        Assert.Throws<ArgumentNullException>(() => new MinimalPerfectHash<int, Int32Murmur3Hasher>(null!, 2.0));
    }

    [Theory]
    [InlineData(0.99)]
    [InlineData(10.01)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_GammaOutOfRange_Throws(double gamma)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MinimalPerfectHash<int, Int32Murmur3Hasher>([1, 2], gamma));
        Assert.Equal("gamma", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullSource_BeatsABadGamma()
    {
        Assert.Throws<ArgumentNullException>(() => new MinimalPerfectHash<int, Int32Murmur3Hasher>(null!, double.NaN));
    }
}
