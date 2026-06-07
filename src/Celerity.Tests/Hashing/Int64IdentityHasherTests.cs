using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class Int64IdentityHasherTests
{
    private readonly Int64IdentityHasher _hasher = new Int64IdentityHasher();

    // The identity hasher returns the key's low 32 bits unchanged.
    private static int Expected(long key) => (int)key;

    // ── Pass-through of the low 32 bits ───────────────────────────────────────

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
    public void Hash_ReturnsLow32Bits(long input)
    {
        Assert.Equal(Expected(input), _hasher.Hash(input));
    }

    [Fact]
    public void Hash_MatchesCast_OnSmallKeys()
    {
        // For keys that fit in an int the hash equals the key, mirroring the
        // way int.GetHashCode() is identity — the zero-work floor for long keys
        // whose discriminating entropy lives in the low 32 bits.
        for (long i = -1000; i <= 1000; i++)
        {
            Assert.Equal((int)i, _hasher.Hash(i));
        }
    }

    // ── Documented collision: upper-32-bit-only differences truncate away ─────

    [Fact]
    public void Hash_IgnoresUpper32Bits_ByDesign()
    {
        // The zero-work floor keeps only the low half, so two keys that differ
        // ONLY in the upper 32 bits collide. This is the documented tradeoff
        // (and the reason Int64WangNaiveHasher exists — it folds the high half
        // back in). Pinning it down guards against an accidental "fix" that
        // would change the contract.
        long low  = 1L;
        long high = 1L | (1L << 48);
        Assert.Equal(_hasher.Hash(low), _hasher.Hash(high));
    }

    [Fact]
    public void Hash_LowBitDifferences_AreDistinct()
    {
        // Conversely, anything that differs in the low 32 bits is distinguished
        // exactly — the shape identity is meant for (dense sequential IDs).
        var seen = new HashSet<int>();
        for (long i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)), $"Unexpected collision at input {i}.");
        }
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash_IsDeterministic_AcrossCalls()
    {
        long value = 1234567890123456789L;
        Assert.Equal(_hasher.Hash(value), _hasher.Hash(value));
    }

    [Fact]
    public void Hash_IsDeterministic_AcrossInstances()
    {
        long value = -1234567890987654321L;
        Assert.Equal(new Int64IdentityHasher().Hash(value), new Int64IdentityHasher().Hash(value));
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

    // ── Integration: satisfies the hasher constraint on collections ──────────

    [Fact]
    public void Int64IdentityHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<long, string, Int64IdentityHasher>();

        dict[0L]  = "zero";    // default(long) — out-of-band slot, never hashed
        dict[1L]  = "one";
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
    public void Int64IdentityHasher_CanDriveLongDictionary()
    {
        var dict = new LongDictionary<string, Int64IdentityHasher>();

        dict[0L]  = "zero";
        dict[1L]  = "one";
        dict[-1L] = "neg-one";

        Assert.Equal(3, dict.Count);
        Assert.Equal("zero", dict[0L]);
        Assert.Equal("one", dict[1L]);
        Assert.Equal("neg-one", dict[-1L]);
    }

    [Fact]
    public void Int64IdentityHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<long, Int64IdentityHasher>();

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

    [Fact]
    public void Int64IdentityHasher_CanDriveLongSet()
    {
        var set = new LongSet<Int64IdentityHasher>();

        set.Add(0L);
        set.Add(1L);
        set.Add(-1L);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(0L));
        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(-1L));
        Assert.False(set.Contains(999L));
    }

    [Fact]
    public void Int64IdentityHasher_DrivesDictionary_OnDenseSequentialKeys()
    {
        // The workload identity is designed for: dense sequential long keys are
        // collision-free under identity (low 32 bits distinct) in an
        // open-addressed power-of-two table.
        var dict = new LongDictionary<long, Int64IdentityHasher>();
        for (long i = 0; i < 5000; i++)
        {
            dict[i] = i * 2;
        }

        Assert.Equal(5000, dict.Count);
        for (long i = 0; i < 5000; i++)
        {
            Assert.Equal(i * 2, dict[i]);
        }
    }
}
