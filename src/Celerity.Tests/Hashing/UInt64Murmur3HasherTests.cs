using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class UInt64Murmur3HasherTests
{
    private readonly UInt64Murmur3Hasher _hasher = new UInt64Murmur3Hasher();

    [Fact]
    public void Hash_Zero_ReturnsZero()
    {
        // Murmur3 fmix64 maps 0 -> 0 (each stage is a no-op on the zero state).
        Assert.Equal(0, _hasher.Hash(0UL));
    }

    // ── 64-bit surface (IHashProvider64<ulong>) ───────────────────────────────
    //
    // fmix64 is a bijection on 64 bits, so Hash64 exposes the whole mix rather than
    // truncating it, and Hash is exactly its low half. The anchors match
    // Int64Murmur3HasherTests on the same bit patterns.

    [Theory]
    [InlineData(0UL,                     0x0000000000000000UL)] // fmix64 fixes zero
    [InlineData(1UL,                     0xB456BCFC34C2CB2CUL)]
    [InlineData(ulong.MaxValue,          0x64B5720B4B825F21UL)]
    [InlineData(42UL,                    0x810879608E4259CCUL)]
    [InlineData(0x7FFFFFFFFFFFFFFFUL,    0xABB93DF0A930EDEAUL)]
    [InlineData(0x8000000000000000UL,    0x8F780810AF31A493UL)]
    [InlineData(1234567890123456789UL,   0x9C49C6098A8F367EUL)]
    public void Hash64_ReturnsExpected(ulong input, ulong expected)
    {
        Assert.Equal(expected, _hasher.Hash64(input));
        Assert.Equal(_hasher.Hash(input), (int)expected);
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        ulong value = 0xDEADBEEFCAFEBABEUL;
        int result1 = _hasher.Hash(value);
        int result2 = _hasher.Hash(value);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Hash64_IsDeterministic_AcrossInstances()
    {
        Assert.Equal(new UInt64Murmur3Hasher().Hash64(42UL), default(UInt64Murmur3Hasher).Hash64(42UL));
    }

    [Fact]
    public void Hash_DistinctInputs_ProduceDistinctResultsForSmallRange()
    {
        // Murmur3 fmix64 is a bijection on 64 bits; truncating to 32 bits on a
        // small sequential range should still produce distinct hashes with
        // overwhelming probability. A collision here would indicate a broken
        // mixer rather than an expected birthday-paradox event.
        var seen = new HashSet<int>();
        for (ulong i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)),
                $"Unexpected collision at input {i}.");
        }
    }

    [Fact]
    public void Hash_HighBits_InfluenceResult()
    {
        // Avalanche check: two inputs that differ only in their top bit
        // should produce different 32-bit hashes.
        int low = _hasher.Hash(1UL);
        int high = _hasher.Hash(1UL | (1UL << 63));
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void Hash_DoesNotThrow()
    {
        ulong[] testValues =
        {
            0UL,
            1UL,
            ulong.MaxValue,
            0x7FFFFFFFFFFFFFFFUL,
            0x8000000000000000UL,
            0xDEADBEEFCAFEBABEUL,
        };

        foreach (ulong val in testValues)
        {
            var exception = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(exception);
        }
    }
}
