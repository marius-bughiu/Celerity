using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class UInt32WangNaiveHasherTests
{
    private readonly UInt32WangNaiveHasher _hasher = new UInt32WangNaiveHasher();

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(1u, 1)]
    [InlineData(16u, 16)]
    [InlineData(65536u, 65537)]
    [InlineData(uint.MaxValue, -65536)]     // 0xFFFFFFFF ^ 0x0000FFFF = 0xFFFF0000
    [InlineData(0x80000000u, -2147450880)]  // 0x80000000 ^ 0x00008000 = 0x80008000
    public void Hash_ShouldReturnTheDocumentedCode_WhenGivenAKnownKey(uint input, int expected)
    {
        int result = _hasher.Hash(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Hash_ShouldReturnTheSameCode_WhenCalledTwiceWithTheSameKey()
    {
        uint value = 12345u;
        int result1 = _hasher.Hash(value);
        int result2 = _hasher.Hash(value);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Hash_ShouldAgreeAcrossInstances_WhenTheHasherIsDefaultConstructed()
    {
        Assert.Equal(new UInt32WangNaiveHasher().Hash(12345u), default(UInt32WangNaiveHasher).Hash(12345u));
    }

    [Fact]
    public void Hash_ShouldChange_WhenOnlyTheTopBitOfTheKeyChanges()
    {
        // The whole point of the fold over a bare identity: the top half reaches the result.
        Assert.NotEqual(_hasher.Hash(1u), _hasher.Hash(1u | (1u << 31)));
    }

    [Fact]
    public void Hash_ShouldNotThrow_ForAnyKeyInTheValueRange()
    {
        uint[] testValues =
        {
            0u,
            1u,
            uint.MaxValue,
            0x7FFFFFFFu,
            0x80000000u,
            123456789u,
            987654321u,
        };

        foreach (uint val in testValues)
        {
            var exception = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(exception);
        }
    }
}
