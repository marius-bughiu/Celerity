namespace Celerity.Tests.Utils;

public class NextPowerOfTwoTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 4)]
    [InlineData(5, 8)]
    [InlineData(6, 8)]
    [InlineData(7, 8)]
    [InlineData(8, 8)]
    [InlineData(9, 16)]
    [InlineData(15, 16)]
    [InlineData(16, 16)]
    [InlineData(17, 32)]
    [InlineData(31, 32)]
    [InlineData(32, 32)]
    [InlineData(33, 64)]
    [InlineData(64, 64)]
    [InlineData(65, 128)]
    [InlineData(100, 128)]
    [InlineData(127, 128)]
    [InlineData(128, 128)]
    [InlineData(129, 256)]
    [InlineData(255, 256)]
    [InlineData(256, 256)]
    [InlineData(257, 512)]
    [InlineData(511, 512)]
    [InlineData(512, 512)]
    [InlineData(513, 1024)]
    [InlineData(1023, 1024)]
    [InlineData(1024, 1024)]
    [InlineData(1025, 2048)]
    [InlineData(2047, 2048)]
    [InlineData(2048, 2048)]
    [InlineData(2049, 4096)]
    [InlineData(4095, 4096)]
    [InlineData(4096, 4096)]
    [InlineData(4097, 8192)]
    [InlineData(9999, 16384)]
    [InlineData(65535, 65536)]
    [InlineData(65536, 65536)]
    [InlineData(65537, 131072)]
    [InlineData(int.MaxValue, 1 << 30)]
    public void NextPowerOfTwo_ShouldReturnCorrectValue(int input, int expected)
    {
        int result = FastUtils.NextPowerOfTwo(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NextPowerOfTwo_ShouldHandleNegativeNumbers()
    {
        Assert.Equal(1, FastUtils.NextPowerOfTwo(-5));
        Assert.Equal(1, FastUtils.NextPowerOfTwo(-1));
        Assert.Equal(1, FastUtils.NextPowerOfTwo(int.MinValue));
    }
}
