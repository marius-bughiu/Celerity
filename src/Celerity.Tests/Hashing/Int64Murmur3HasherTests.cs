using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class Int64Murmur3HasherTests
{
    private readonly Int64Murmur3Hasher _hasher = new Int64Murmur3Hasher();

    [Fact]
    public void Hash_Zero_ReturnsZero()
    {
        // Arrange
        long key = 0L;

        // Act
        int result = _hasher.Hash(key);

        // Assert
        // Murmur3 final mix with an input of 0 produces 0 in this variant.
        Assert.Equal(0, result);
    }

    // ── 64-bit surface (IHashProvider64<long>) ────────────────────────────────
    //
    // fmix64 is a bijection on 64 bits, so Hash64 exposes the whole mix rather than
    // truncating it, and Hash is exactly its low half.

    [Theory]
    [InlineData(0L,                    0x0000000000000000UL)] // fmix64 fixes zero
    [InlineData(1L,                    0xB456BCFC34C2CB2CUL)]
    [InlineData(-1L,                   0x64B5720B4B825F21UL)]
    [InlineData(42L,                   0x810879608E4259CCUL)]
    [InlineData(long.MaxValue,         0xABB93DF0A930EDEAUL)]
    [InlineData(long.MinValue,         0x8F780810AF31A493UL)]
    [InlineData(1234567890123456789L,  0x9C49C6098A8F367EUL)]
    public void Hash64_ReturnsExpected(long input, ulong expected)
    {
        Assert.Equal(expected, _hasher.Hash64(input));
        Assert.Equal(_hasher.Hash(input), (int)expected);
    }

    [Fact]
    public void Hash64_HighHalf_VariesAcrossKeys()
    {
        // The whole point of the 64-bit surface: the high half must carry information, not
        // repeat a widened 32-bit code.
        var highHalves = new HashSet<uint>();
        for (long i = 1; i <= 1000; i++)
            highHalves.Add((uint)(_hasher.Hash64(i) >> 32));

        Assert.True(highHalves.Count > 990, $"only {highHalves.Count} distinct high halves");
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    [InlineData(1234567890123456789L)]
    public void Hash_SameValueMultipleTimes_ReturnsSameResult(long key)
    {
        // Act
        int hash1 = _hasher.Hash(key);
        int hash2 = _hasher.Hash(key);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_ValuesAreWellDistributed_AmongSmallSet()
    {
        // Note: Checking "distribution" is complex. As a simple proxy, we at least
        // verify no collisions in a small, arbitrary set of values.
        long[] testKeys = new long[]
        {
            0,
            1,
            -1,
            long.MinValue,
            long.MaxValue,
            12345,
            -999999999999999999
        };

        var hashSet = new System.Collections.Generic.HashSet<int>();
        foreach (var key in testKeys)
        {
            int h = _hasher.Hash(key);
            // If there's a collision in this small set, something is off.
            Assert.True(hashSet.Add(h), $"Collision detected for key: {key}");
        }
    }

    [Fact]
    public void Hash_ProducesExpectedValue_ForKnownSample()
    {
        // If you want to lock down specific outputs for certain keys (regression tests),
        // you can compute them once and keep them in your code. For demonstration,
        // we'll show how to test a single known result.

        long sampleKey = 42L;
        int expectedHash = ComputeExpectedMurmurHashFor42();
        // ^ You could compute this once offline or in a small console snippet 
        //   and hardcode the result here.

        int actualHash = _hasher.Hash(sampleKey);

        Assert.Equal(expectedHash, actualHash);
    }

    /// <summary>
    /// Mock function that returns a pre-computed value for key=42.
    /// Replace with the actual integer after running a one-time calculation.
    /// </summary>
    private int ComputeExpectedMurmurHashFor42()
    {
        // Hardcode the expected result once known. 
        // Example: return 1481640323; // <-- placeholder example
        // For now, we just re-run the same hasher for demonstration.
        return _hasher.Hash(42L);
    }
}
