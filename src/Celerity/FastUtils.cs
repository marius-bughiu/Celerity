namespace Celerity;

/// <summary>
/// Provides fast utility methods for common math and system operations.
/// </summary>
public static class FastUtils
{
    /// <summary>
    /// Returns the smallest power of two greater than or equal to the specified number.
    /// If <paramref name="n"/> is 0 or negative, 1 is returned.
    /// If <paramref name="n"/> is very large, the method caps the result to avoid overflow.
    /// </summary>
    /// <param name="n">The integer to find the next power of two for.</param>
    /// <returns>The smallest power of two greater than or equal to <paramref name="n"/>.</returns>
    public static int NextPowerOfTwo(int n)
    {
        if (n <= 0) return 1;
        if (n >= (1 << 30)) return 1 << 30; // Prevent overflow

        // Decrement n to ensure that exact powers of two aren't mistakenly doubled
        n--; 

        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        return n + 1;
    }

    /// <summary>
    /// Safely casts a <see cref="long"/> to <see cref="int"/>. Throws an
    /// <see cref="OverflowException"/> if the value cannot fit into an
    /// <see cref="int"/>.
    /// </summary>
    /// <param name="value">The <see cref="long"/> value to cast.</param>
    /// <returns>The value cast to <see cref="int"/> if within range.</returns>
    public static int SafeLongToInt(long value)
    {
        if (value > int.MaxValue || value < int.MinValue)
            throw new OverflowException($"{value} does not fit into an Int32.");

        return (int)value;
    }

    /// <summary>
    /// Safely casts an <see cref="int"/> to <see cref="short"/>. Throws an
    /// <see cref="OverflowException"/> if the value cannot fit into a
    /// <see cref="short"/>.
    /// </summary>
    /// <param name="value">The <see cref="int"/> value to cast.</param>
    /// <returns>The value cast to <see cref="short"/> if within range.</returns>
    public static short SafeIntToShort(int value)
    {
        if (value > short.MaxValue || value < short.MinValue)
            throw new OverflowException($"{value} does not fit into an Int16.");

        return (short)value;
    }

    /// <summary>
    /// Safely casts an <see cref="int"/> to <see cref="byte"/>. Throws an
    /// <see cref="OverflowException"/> if the value is outside the range
    /// of <see cref="byte"/> (0-255).
    /// </summary>
    /// <param name="value">The value to cast.</param>
    /// <returns>The cast value.</returns>
    public static byte SafeIntToByte(int value)
    {
        if (value > byte.MaxValue || value < byte.MinValue)
            throw new OverflowException($"{value} does not fit into a Byte.");

        return (byte)value;
    }

    /// <summary>
    /// Safely casts an <see cref="int"/> to <see cref="char"/>. Throws an
    /// <see cref="OverflowException"/> if the value cannot fit into a
    /// <see cref="char"/>.
    /// </summary>
    /// <param name="value">The value to cast.</param>
    /// <returns>The cast value.</returns>
    public static char SafeIntToChar(int value)
    {
        if (value > char.MaxValue || value < char.MinValue)
            throw new OverflowException($"{value} does not fit into a Char.");

        return (char)value;
    }
}
