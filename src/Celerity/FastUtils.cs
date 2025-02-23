namespace Celerity;

public static class FastUtils
{
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
