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
    /// Returns the pre-computed Murmur3 hash for <c>42L</c>.
    /// </summary>
    private int ComputeExpectedMurmurHashFor42()
    {
        // Pre-computed using Int64Murmur3Hasher once.
        return -1908254260;
    }
}
