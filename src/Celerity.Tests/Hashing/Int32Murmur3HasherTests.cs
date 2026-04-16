using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class Int32Murmur3HasherTests
{
    private readonly Int32Murmur3Hasher _hasher = new Int32Murmur3Hasher();

    // Reference values produced by the canonical MurmurHash3 fmix32 finalizer
    // (k ^= k >> 16; k *= 0x85ebca6b; k ^= k >> 13; k *= 0xc2b2ae35; k ^= k >> 16).
    // Exact values guard against accidental constant or shift edits.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1364076727)]             // 0x514E28B7
    [InlineData(-1, -2114883783)]            // 0x81F16F39 (input 0xFFFFFFFF)
    [InlineData(int.MinValue, 1832674720)]   // 0x6D3C65A0 (input 0x80000000)
    [InlineData(int.MaxValue, -104067416)]   // 0xF9CC0EA8 (input 0x7FFFFFFF)
    [InlineData(123456789, -1168058214)]     // 0xBA60D89A
    public void Hash_ReturnsExpected(int input, int expected)
    {
        Assert.Equal(expected, _hasher.Hash(input));
    }

    [Fact]
    public void Hash_Zero_ReturnsZero()
    {
        // Murmur3 fmix32 maps 0 -> 0 (each stage is a no-op on the zero state).
        Assert.Equal(0, _hasher.Hash(0));
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        int value = unchecked((int)0xDEADBEEF);
        int result1 = _hasher.Hash(value);
        int result2 = _hasher.Hash(value);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Hash_DistinctInputs_ProduceDistinctResultsForSmallRange()
    {
        // fmix32 is a bijection on 32 bits, so a sequential sweep of 1000 inputs
        // must produce 1000 distinct outputs. A collision here would indicate a
        // broken mixer rather than an expected birthday-paradox event.
        var seen = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)),
                $"Unexpected collision at input {i}.");
        }
    }

    [Fact]
    public void Hash_HighBit_InfluencesResult()
    {
        // Avalanche check: two inputs that differ only in the sign bit should
        // produce very different 32-bit hashes. The naive Wang-style mixer only
        // folds the top 16 bits into the bottom 16, so a single-bit flip in the
        // MSB barely changes the low half of the output — Murmur3 fmix32 must
        // do much better than that.
        int low = _hasher.Hash(1);
        int high = _hasher.Hash(unchecked((int)(1u | (1u << 31))));
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void Hash_SequentialInputs_AreNotIdentity()
    {
        // A good mixer maps adjacent integers to distant hashes. Regression
        // guard against accidentally returning the identity: hash(i) == i for
        // many sequential i would indicate the finalizer was bypassed.
        int identityMatches = 0;
        for (int i = 0; i < 256; i++)
        {
            if (_hasher.Hash(i) == i)
            {
                identityMatches++;
            }
        }

        // Only i = 0 is a fixed point of fmix32 in this range; anything more
        // than that would indicate a broken implementation.
        Assert.Equal(1, identityMatches);
    }

    [Fact]
    public void Hash_DoesNotThrow()
    {
        int[] testValues =
        {
            0,
            1,
            -1,
            int.MaxValue,
            int.MinValue,
            123456789,
            -987654321,
        };

        foreach (int val in testValues)
        {
            var exception = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(exception);
        }
    }
}
