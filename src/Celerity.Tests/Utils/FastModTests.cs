using System;
using System.Collections.Generic;

namespace Celerity.Tests.Utils;

/// <summary>
/// Correctness coverage for the Lemire <see cref="FastUtils.FastMod(uint, uint, ulong)"/> /
/// <see cref="FastUtils.FastDiv(uint, ulong)"/> reciprocal modulo and division helpers (issue #191),
/// reconciling every result against the built-in <c>%</c> / <c>/</c> operators.
/// </summary>
public class FastModTests
{
    // Representative 32-bit divisors: small values, powers of two and their neighbours, primes, and the
    // extreme high end of the range (including the largest divisor and one just below 2^31 / 2^32).
    private static readonly uint[] Divisors32 =
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 16, 17, 31, 32, 33, 63, 64, 97, 100, 127, 128, 255, 256,
        1000, 1009, 1023, 1024, 65535, 65536, 1_000_003, 0x4000_0000, 0x7FFF_FFFF, 0x8000_0000,
        0xFFFF_FFFE, 0xFFFF_FFFF,
    };

    private static readonly ulong[] Divisors64 =
    {
        1, 2, 3, 4, 5, 7, 8, 16, 17, 31, 32, 97, 128, 1000, 1024, 65536, 1_000_003,
        4_294_967_295UL, 4_294_967_296UL, 1_000_000_007UL, 9_999_999_967UL,
        0x4000_0000_0000_0000UL, 0x7FFF_FFFF_FFFF_FFFFUL, 0x8000_0000_0000_0000UL,
        0xFFFF_FFFF_FFFF_FFFEUL, 0xFFFF_FFFF_FFFF_FFFFUL,
    };

    private static IEnumerable<uint> SampleValues32(uint divisor, Random rng)
    {
        // Boundary values around 0 and multiples of the divisor, plus the top of the range.
        yield return 0;
        yield return 1;
        yield return 2;
        for (uint k = 1; k <= 4; k++)
        {
            // d*k - 1, d*k, d*k + 1 (computed in 64-bit then masked back to test wrap-around too).
            ulong m = (ulong)divisor * k;
            yield return (uint)((m - 1) & 0xFFFF_FFFF);
            yield return (uint)(m & 0xFFFF_FFFF);
            yield return (uint)((m + 1) & 0xFFFF_FFFF);
        }
        yield return uint.MaxValue;
        yield return uint.MaxValue - 1;
        yield return 0x8000_0000;
        yield return 0x7FFF_FFFF;

        for (int i = 0; i < 4000; i++)
            yield return NextUInt(rng);
    }

    private static IEnumerable<ulong> SampleValues64(ulong divisor, Random rng)
    {
        yield return 0;
        yield return 1;
        yield return 2;
        for (ulong k = 1; k <= 4; k++)
        {
            ulong m = divisor * k; // may wrap; still a valid dividend to check
            yield return m - 1;
            yield return m;
            yield return m + 1;
        }
        yield return ulong.MaxValue;
        yield return ulong.MaxValue - 1;
        yield return 0x8000_0000_0000_0000UL;
        yield return 0x7FFF_FFFF_FFFF_FFFFUL;

        for (int i = 0; i < 4000; i++)
            yield return NextULong(rng);
    }

    [Fact]
    public void FastMod32_ShouldMatchOperator_AcrossDivisorsAndValues()
    {
        var rng = new Random(20260607);
        foreach (uint divisor in Divisors32)
        {
            ulong multiplier = FastUtils.GetFastModMultiplier(divisor);
            foreach (uint value in SampleValues32(divisor, rng))
            {
                uint expected = value % divisor;
                uint actual = FastUtils.FastMod(value, divisor, multiplier);
                Assert.True(expected == actual,
                    $"FastMod({value}, {divisor}) = {actual}, expected {expected}");
            }
        }
    }

    [Fact]
    public void FastDiv32_ShouldMatchOperator_AcrossDivisorsAndValues()
    {
        var rng = new Random(20260608);
        foreach (uint divisor in Divisors32)
        {
            if (divisor < 2) continue; // FastDiv requires divisor >= 2 (see remarks).
            ulong multiplier = FastUtils.GetFastModMultiplier(divisor);
            foreach (uint value in SampleValues32(divisor, rng))
            {
                uint expected = value / divisor;
                uint actual = FastUtils.FastDiv(value, multiplier);
                Assert.True(expected == actual,
                    $"FastDiv({value}, /{divisor}) = {actual}, expected {expected}");
            }
        }
    }

    [Fact]
    public void FastMod64_ShouldMatchOperator_AcrossDivisorsAndValues()
    {
        var rng = new Random(20260609);
        foreach (ulong divisor in Divisors64)
        {
            UInt128 multiplier = FastUtils.GetFastModMultiplier(divisor);
            foreach (ulong value in SampleValues64(divisor, rng))
            {
                ulong expected = value % divisor;
                ulong actual = FastUtils.FastMod(value, divisor, multiplier);
                Assert.True(expected == actual,
                    $"FastMod({value}, {divisor}) = {actual}, expected {expected}");
            }
        }
    }

    [Fact]
    public void FastDiv64_ShouldMatchOperator_AcrossDivisorsAndValues()
    {
        var rng = new Random(20260610);
        foreach (ulong divisor in Divisors64)
        {
            if (divisor < 2) continue; // FastDiv requires divisor >= 2 (see remarks).
            UInt128 multiplier = FastUtils.GetFastModMultiplier(divisor);
            foreach (ulong value in SampleValues64(divisor, rng))
            {
                ulong expected = value / divisor;
                ulong actual = FastUtils.FastDiv(value, multiplier);
                Assert.True(expected == actual,
                    $"FastDiv({value}, /{divisor}) = {actual}, expected {expected}");
            }
        }
    }

    [Theory]
    [InlineData(3u)]
    [InlineData(7u)]
    [InlineData(10u)]
    [InlineData(1000u)]
    public void FastMod32_ExhaustiveOverLowRange_ShouldMatchOperator(uint divisor)
    {
        // A dense, fully exhaustive sweep over a contiguous low range catches any off-by-one that a
        // random sample might skip, complementing the wide-range random fuzz above.
        ulong multiplier = FastUtils.GetFastModMultiplier(divisor);
        for (uint value = 0; value < 2_000_000; value++)
        {
            Assert.Equal(value % divisor, FastUtils.FastMod(value, divisor, multiplier));
            Assert.Equal(value / divisor, FastUtils.FastDiv(value, multiplier));
        }
    }

    [Fact]
    public void FastMod_DivisorOne_ReturnsZero()
    {
        // value % 1 is always 0; the (overflowed-to-zero) multiplier still yields the right answer.
        ulong m32 = FastUtils.GetFastModMultiplier(1u);
        Assert.Equal(0u, FastUtils.FastMod(0u, 1u, m32));
        Assert.Equal(0u, FastUtils.FastMod(1u, 1u, m32));
        Assert.Equal(0u, FastUtils.FastMod(uint.MaxValue, 1u, m32));

        UInt128 m64 = FastUtils.GetFastModMultiplier(1ul);
        Assert.Equal(0ul, FastUtils.FastMod(0ul, 1ul, m64));
        Assert.Equal(0ul, FastUtils.FastMod(ulong.MaxValue, 1ul, m64));
    }

    [Fact]
    public void GetFastModMultiplier_ZeroDivisor_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FastUtils.GetFastModMultiplier(0u));
        Assert.Throws<ArgumentOutOfRangeException>(() => FastUtils.GetFastModMultiplier(0ul));
    }

    [Fact]
    public void GetFastModMultiplier_MatchesCeilingOfReciprocal()
    {
        // ceil(2^64 / d) for the 32-bit overload, ceil(2^128 / d) for the 64-bit one.
        Assert.Equal(ulong.MaxValue / 7u + 1, FastUtils.GetFastModMultiplier(7u));
        Assert.Equal(UInt128.MaxValue / 7ul + 1, FastUtils.GetFastModMultiplier(7ul));
    }

    private static uint NextUInt(Random rng)
    {
        Span<byte> b = stackalloc byte[4];
        rng.NextBytes(b);
        return BitConverter.ToUInt32(b);
    }

    private static ulong NextULong(Random rng)
    {
        Span<byte> b = stackalloc byte[8];
        rng.NextBytes(b);
        return BitConverter.ToUInt64(b);
    }
}
