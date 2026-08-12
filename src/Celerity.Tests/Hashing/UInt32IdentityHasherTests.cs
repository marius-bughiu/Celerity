using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class UInt32IdentityHasherTests
{
    private readonly UInt32IdentityHasher _hasher = new UInt32IdentityHasher();

    // The identity hasher reinterprets the key's 32 bits as a signed integer.
    private static int Expected(uint key) => unchecked((int)key);

    // ── Pass-through: the hash IS the key's bit pattern ───────────────────────

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(42u)]
    [InlineData(65536u)]
    [InlineData(uint.MaxValue)]
    [InlineData(0x8000_0000u)]
    [InlineData(0x7FFF_FFFFu)]
    [InlineData(1234567890u)]
    public void Hash_ReturnsKeyBitsUnchanged(uint input)
    {
        // The defining property of the zero-work floor: identity.
        Assert.Equal(Expected(input), _hasher.Hash(input));
    }

    [Fact]
    public void Hash_MatchesUIntGetHashCode()
    {
        // uint.GetHashCode() is itself this reinterpretation, so the identity
        // hasher must reproduce the framework hash exactly — that is the whole
        // point of labelling it the zero-work floor no mixing hasher can beat.
        uint[] values = { 0u, 1u, 42u, uint.MaxValue, 0x8000_0000u, 1234567890u };
        foreach (uint v in values)
        {
            Assert.Equal(v.GetHashCode(), _hasher.Hash(v));
        }
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash_IsDeterministic_AcrossCalls()
    {
        uint value = 1234567890u;
        Assert.Equal(_hasher.Hash(value), _hasher.Hash(value));
    }

    [Fact]
    public void Hash_IsDeterministic_AcrossInstances()
    {
        // Hashers are stateless structs; two independent instances agree.
        uint value = 0x8000_0001u;
        Assert.Equal(new UInt32IdentityHasher().Hash(value), new UInt32IdentityHasher().Hash(value));
    }

    // ── Distribution: bijective, so collision-free on any contiguous range ────

    [Fact]
    public void Hash_ConsecutiveInputs_ProduceDistinctResults()
    {
        // Unlike the 64-bit identity hasher, this one loses nothing: uint and int
        // are the same width, so it is a bijection on the full 32-bit space and
        // collision-free on any contiguous range.
        var seen = new HashSet<int>();
        for (uint i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)), $"Unexpected collision at input {i}.");
        }
    }

    [Fact]
    public void Hash_TopBitKeys_AreDistinctFromTheirLowCounterparts()
    {
        // The cast is a reinterpretation, not a truncation: a key with the top
        // bit set keeps that bit — as the sign — rather than losing it, so it
        // cannot collide with the same key without it.
        Assert.NotEqual(_hasher.Hash(1u), _hasher.Hash(1u | (1u << 31)));
        Assert.True(_hasher.Hash(0x8000_0000u) < 0);
    }

    [Fact]
    public void Hash_DoesNotThrow()
    {
        uint[] testValues = { 0u, 1u, uint.MaxValue, 0x7FFF_FFFFu, 0x8000_0000u, 1234567890u, 987654321u };
        foreach (uint val in testValues)
        {
            var ex = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(ex);
        }
    }

    // ── Integration: satisfies the hasher constraint on collections ──────────
    //
    // This is the gap the type exists to close: the collections take the hasher
    // as a type parameter and invoke it internally, so a uint-keyed collection
    // needs an IHashProvider<uint>. A cast to Int32IdentityHasher at the call
    // site is not an option, because there is no call site.

    [Fact]
    public void UInt32IdentityHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<uint, string, UInt32IdentityHasher>();

        dict[0u]  = "zero";    // default(uint) — out-of-band slot, never hashed
        dict[1u]  = "one";
        dict[uint.MaxValue] = "max";
        dict[42u] = "forty-two";

        Assert.Equal(4, dict.Count);
        Assert.Equal("zero", dict[0u]);
        Assert.Equal("one", dict[1u]);
        Assert.Equal("max", dict[uint.MaxValue]);
        Assert.Equal("forty-two", dict[42u]);
        Assert.True(dict.ContainsKey(0u));
        Assert.False(dict.ContainsKey(999u));
    }

    [Fact]
    public void UInt32IdentityHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<uint, UInt32IdentityHasher>();

        set.Add(0u);    // default(uint) — out-of-band slot
        set.Add(1u);
        set.Add(uint.MaxValue);
        set.Add(42u);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(0u));
        Assert.True(set.Contains(1u));
        Assert.True(set.Contains(uint.MaxValue));
        Assert.True(set.Contains(42u));
        Assert.False(set.Contains(999u));
    }

    [Fact]
    public void UInt32IdentityHasher_CanDriveASketch()
    {
        // The sketches carry the same constraint, so they had the same gap.
        var filter = new BloomFilter<uint, UInt32IdentityHasher>(expectedItems: 1000);
        for (uint i = 0; i < 500; i++)
        {
            filter.Add(i);
        }

        for (uint i = 0; i < 500; i++)
        {
            Assert.True(filter.Contains(i));
        }
    }

    [Fact]
    public void UInt32IdentityHasher_DrivesDictionary_OnDenseSequentialKeys()
    {
        // The workload identity is designed for: dense sequential uint keys are
        // collision-free under identity in an open-addressed power-of-two table,
        // so a few thousand inserts round-trip without a single mixing op.
        var dict = new CelerityDictionary<uint, uint, UInt32IdentityHasher>();
        for (uint i = 0; i < 5000; i++)
        {
            dict[i] = i * 2;
        }

        Assert.Equal(5000, dict.Count);
        for (uint i = 0; i < 5000; i++)
        {
            Assert.Equal(i * 2, dict[i]);
        }
    }
}
