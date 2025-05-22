using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class Int64SpookyHasherTests
{
    private readonly Int64SpookyHasher _hasher = new Int64SpookyHasher();

    [Fact]
    public void Hash_Zero_ReturnsExpected()
    {
        long key = 0L;

        int result = _hasher.Hash(key);

        Assert.Equal(-749474287, result);
    }

    [Theory]
    [InlineData(1L, 1324709874)]
    [InlineData(-1L, -282399519)]
    [InlineData(long.MaxValue, -2060992555)]
    [InlineData(long.MinValue, 1673291361)]
    [InlineData(1234567890123456789L, -1087435504)]
    public void Hash_KnownValues_ReturnExpected(long key, int expected)
    {
        int result = _hasher.Hash(key);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        long key = 42L;

        int h1 = _hasher.Hash(key);
        int h2 = _hasher.Hash(key);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Hash_ProducesKnownSample()
    {
        long sampleKey = 42L;
        int expected = -1500868800; // precomputed

        int actual = _hasher.Hash(sampleKey);

        Assert.Equal(expected, actual);
    }
}
