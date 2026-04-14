using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class UInt32HasherTests
{
    private readonly UInt32Hasher _hasher = new UInt32Hasher();

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(1u, 1)]
    [InlineData(16u, 16)]
    [InlineData(65536u, 65537)]
    [InlineData(uint.MaxValue, -65536)]     // 0xFFFFFFFF ^ 0x0000FFFF = 0xFFFF0000
    [InlineData(0x80000000u, -2147450880)]  // 0x80000000 ^ 0x00008000 = 0x80008000
    public void Hash_ReturnsExpected(uint input, int expected)
    {
        int result = _hasher.Hash(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        uint value = 12345u;
        int result1 = _hasher.Hash(value);
        int result2 = _hasher.Hash(value);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Hash_DoesNotThrow()
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
