namespace Celerity.Tests.Utils;

public class SafeCastTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void SafeLongToInt_ReturnsValue_WhenWithinRange(long value)
    {
        int result = FastUtils.SafeLongToInt(value);
        Assert.Equal((int)value, result);
    }

    [Theory]
    [InlineData((long)int.MaxValue + 1)]
    [InlineData((long)int.MinValue - 1)]
    public void SafeLongToInt_Throws_WhenOutOfRange(long value)
    {
        Assert.Throws<OverflowException>(() => FastUtils.SafeLongToInt(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(short.MaxValue)]
    [InlineData(short.MinValue)]
    public void SafeIntToShort_ReturnsValue_WhenWithinRange(int value)
    {
        short result = FastUtils.SafeIntToShort(value);
        Assert.Equal((short)value, result);
    }

    [Theory]
    [InlineData((int)short.MaxValue + 1)]
    [InlineData((int)short.MinValue - 1)]
    public void SafeIntToShort_Throws_WhenOutOfRange(int value)
    {
        Assert.Throws<OverflowException>(() => FastUtils.SafeIntToShort(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void SafeIntToByte_ReturnsValue_WhenWithinRange(int value)
    {
        byte result = FastUtils.SafeIntToByte(value);
        Assert.Equal((byte)value, result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void SafeIntToByte_Throws_WhenOutOfRange(int value)
    {
        Assert.Throws<OverflowException>(() => FastUtils.SafeIntToByte(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(char.MaxValue)]
    public void SafeIntToChar_ReturnsValue_WhenWithinRange(int value)
    {
        char result = FastUtils.SafeIntToChar(value);
        Assert.Equal((char)value, result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(char.MaxValue + 1)]
    public void SafeIntToChar_Throws_WhenOutOfRange(int value)
    {
        Assert.Throws<OverflowException>(() => FastUtils.SafeIntToChar(value));
    }
}
