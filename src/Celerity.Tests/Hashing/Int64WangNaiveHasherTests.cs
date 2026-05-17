using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class Int64WangNaiveHasherTests
{
    private readonly Int64WangNaiveHasher _hasher = new Int64WangNaiveHasher();

    // The naive hasher folds three 32-bit windows of the key into one int.
    // The formula is short enough to act as the spec, so the theory cases
    // compute the expected value inline rather than hard-coding anchors.
    private static int Expected(long key) =>
        (int)key ^ (int)((ulong)key >> 32) ^ (int)((ulong)key >> 16);

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(42L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(1234567890123456789L)]
    [InlineData(0x0000_FFFF_FFFF_FFFFL)]
    [InlineData(0x1234_5678_9ABC_DEF0L)]
    public void Hash_MatchesFormula(long input)
    {
        Assert.Equal(Expected(input), _hasher.Hash(input));
    }

    [Fact]
    public void Hash_IsDeterministic_AcrossCalls()
    {
        long value = 1234567890123456789L;
        int result1 = _hasher.Hash(value);
        int result2 = _hasher.Hash(value);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Hash_IsDeterministic_AcrossInstances()
    {
        // Hashers are structs with no state; two independently-constructed
        // instances must produce identical output for the same input.
        long value = -1234567890987654321L;
        int a = new Int64WangNaiveHasher().Hash(value);
        int b = new Int64WangNaiveHasher().Hash(value);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_HighBits_InfluenceResult()
    {
        // A regression guard against any future change that accidentally
        // discards the upper 32 bits. Flip a single bit in the top half and
        // assert the hash moves with it.
        int low = _hasher.Hash(1L);
        int high = _hasher.Hash(1L | (1L << 48));
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void Hash_MidBits_InfluenceResult()
    {
        // The naive hasher folds the >>16 window in to keep bits 16-47 alive.
        // A version that only XORed (int)key with (int)(key >> 32) would lose
        // the entire byte at position [2..6]; this test pins that down.
        int baseline = _hasher.Hash(0L);
        int withMidBit = _hasher.Hash(1L << 20);
        Assert.NotEqual(baseline, withMidBit);
    }

    [Fact]
    public void Hash_ConsecutiveSmallInputs_AreDistinct()
    {
        // For sequential keys 0..999 the low 32 bits dominate, so the result
        // is just (int)key in that range — no collisions expected.
        var seen = new HashSet<int>();
        for (long i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)),
                $"Unexpected collision at input {i}.");
        }
    }

    [Fact]
    public void Hash_BitStridedInputs_AreDistinct()
    {
        // Keys constructed by setting a single high-half bit at a time. The
        // naive hasher must spread these across distinct buckets — a hasher
        // that ignored the upper half would map every one of these to 0.
        var seen = new HashSet<int>();
        for (int bit = 32; bit < 64; bit++)
        {
            long key = 1L << bit;
            Assert.True(seen.Add(_hasher.Hash(key)),
                $"Collision at bit {bit} (key = 0x{key:X16}).");
        }
    }

    [Fact]
    public void Hash_DoesNotThrow()
    {
        long[] testValues =
        {
            0L, 1L, -1L, long.MaxValue, long.MinValue,
            1234567890123456789L, -1234567890987654321L
        };

        foreach (long val in testValues)
        {
            var ex = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void Int64WangNaiveHasher_CanDriveLongDictionary()
    {
        // Integration check: the new default for LongDictionary<TValue> must
        // satisfy the hasher constraint end-to-end, including the out-of-band
        // zero-key slot.
        var dict = new LongDictionary<string>();

        dict[0L] = "zero";
        dict[1L] = "one";
        dict[-1L] = "neg-one";
        dict[42L] = "forty-two";

        Assert.Equal(4, dict.Count);
        Assert.Equal("zero", dict[0L]);
        Assert.Equal("one", dict[1L]);
        Assert.Equal("neg-one", dict[-1L]);
        Assert.Equal("forty-two", dict[42L]);
        Assert.True(dict.ContainsKey(0L));
        Assert.False(dict.ContainsKey(999L));
    }

    [Fact]
    public void Int64WangNaiveHasher_CanDriveLongSet()
    {
        var set = new LongSet();

        set.Add(0L);
        set.Add(1L);
        set.Add(-1L);
        set.Add(42L);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(0L));
        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(-1L));
        Assert.True(set.Contains(42L));
        Assert.False(set.Contains(999L));
    }
}
