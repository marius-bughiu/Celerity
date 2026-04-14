using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class UInt64HasherTests
{
    private readonly UInt64Hasher _hasher = new UInt64Hasher();

    [Fact]
    public void Hash_Zero_ReturnsZero()
    {
        // Murmur3 fmix64 maps 0 -> 0 (each stage is a no-op on the zero state).
        Assert.Equal(0, _hasher.Hash(0UL));
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
