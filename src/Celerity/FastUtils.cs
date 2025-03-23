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
}
