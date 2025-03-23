using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class Int32WangNaiveHasherTests
{
    private readonly Int32WangNaiveHasher _hasher = new Int32WangNaiveHasher();

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(16, 16)]
    [InlineData(65536, 65537)]
    [InlineData(-1, 0)]
    [InlineData(int.MaxValue, 2147450880)]
    [InlineData(int.MinValue, 2147450880)]
    public void Hash_ReturnsExpected(int input, int expected)
    {
        // Act
        int result = _hasher.Hash(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        // Arrange
        int value = 12345;

        // Act
        int result1 = _hasher.Hash(value);
        int result2 = _hasher.Hash(value);

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Hash_DoesNotThrow()
    {
        // Here we just ensure the method doesn't throw an exception for any int.
        // We can test a representative set of values, including edge cases.

        int[] testValues =
        {
            0,
            1,
            -1,
            int.MaxValue,
            int.MinValue,
            123456789,
            -987654321
        };

        foreach (int val in testValues)
        {
            var exception = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(exception);
        }
    }
}
