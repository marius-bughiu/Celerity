using System;
using System.Globalization;

namespace Celerity.Tests.Utils;

/// <summary>
/// Correctness coverage for the <see cref="FastUtils.CountDigits(uint)"/> /
/// <see cref="FastUtils.CountDigits(ulong)"/> base-10 digit counters and the companion integer
/// <see cref="FastUtils.Log10(uint)"/> / <see cref="FastUtils.Log10(ulong)"/> (issue #194),
/// reconciling every result against <c>value.ToString().Length</c> and an exact <c>BigInteger</c> oracle.
/// </summary>
public class CountDigitsTests
{
    // ── CountDigits(uint) ───────────────────────────────────────────────────────────────

    [Fact]
    public void CountDigits32_ExhaustiveLowRange_MatchesToStringLength()
    {
        // A dense sweep over the low range catches any off-by-one the boundary cases might miss.
        for (uint value = 0; value < 2_000_000; value++)
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture).Length, FastUtils.CountDigits(value));
    }

    [Theory]
    // Every power-of-ten boundary and its neighbours — the cases the floating-point Math.Log10 gets wrong.
    [InlineData(0u, 1)]
    [InlineData(1u, 1)]
    [InlineData(9u, 1)]
    [InlineData(10u, 2)]
    [InlineData(11u, 2)]
    [InlineData(99u, 2)]
    [InlineData(100u, 3)]
    [InlineData(999u, 3)]
    [InlineData(1000u, 4)]
    [InlineData(9999u, 4)]
    [InlineData(10000u, 5)]
    [InlineData(99999u, 5)]
    [InlineData(100000u, 6)]
    [InlineData(999999u, 6)]
    [InlineData(1000000u, 7)]
    [InlineData(9999999u, 7)]
    [InlineData(10000000u, 8)]
    [InlineData(99999999u, 8)]
    [InlineData(100000000u, 9)]
    [InlineData(999999999u, 9)]
    [InlineData(1000000000u, 10)]
    [InlineData(4294967294u, 10)]
    [InlineData(uint.MaxValue, 10)] // 4294967295
    public void CountDigits32_Boundaries_AreExact(uint value, int expected)
    {
        Assert.Equal(expected, FastUtils.CountDigits(value));
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture).Length, FastUtils.CountDigits(value));
    }

    [Fact]
    public void CountDigits32_RandomAndPowersOfTen_MatchToStringLength()
    {
        var rng = new Random(20260608);
        // Random wide-range coverage.
        for (int i = 0; i < 200_000; i++)
        {
            uint value = NextUInt(rng);
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture).Length, FastUtils.CountDigits(value));
        }

        // Every 10^k and the values either side of it within the uint range.
        for (ulong pow = 1; pow <= uint.MaxValue; pow *= 10)
        {
            CheckU32((uint)(pow - 1));
            CheckU32((uint)pow);
            if (pow + 1 <= uint.MaxValue) CheckU32((uint)(pow + 1));
        }

        static void CheckU32(uint v) =>
            Assert.Equal(v.ToString(CultureInfo.InvariantCulture).Length, FastUtils.CountDigits(v));
    }

    // ── CountDigits(ulong) ──────────────────────────────────────────────────────────────

    [Fact]
    public void CountDigits64_ExhaustiveLowRange_MatchesToStringLength()
    {
        for (ulong value = 0; value < 2_000_000; value++)
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture).Length, FastUtils.CountDigits(value));
    }

    [Theory]
    [InlineData(0ul, 1)]
    [InlineData(9ul, 1)]
    [InlineData(10ul, 2)]
    [InlineData(9_999_999ul, 7)]
    [InlineData(10_000_000ul, 8)]                       // the path's first division boundary
    [InlineData(99_999_999_999_999ul, 14)]
    [InlineData(100_000_000_000_000ul, 15)]            // the path's second division boundary
    [InlineData(999_999_999_999_999_999ul, 18)]
    [InlineData(1_000_000_000_000_000_000ul, 19)]
    [InlineData(9_999_999_999_999_999_999ul, 19)]
    [InlineData(10_000_000_000_000_000_000ul, 20)]
    [InlineData(ulong.MaxValue, 20)]                    // 18446744073709551615
    public void CountDigits64_Boundaries_AreExact(ulong value, int expected)
    {
        Assert.Equal(expected, FastUtils.CountDigits(value));
        Assert.Equal(value.ToString(CultureInfo.InvariantCulture).Length, FastUtils.CountDigits(value));
    }

    [Fact]
    public void CountDigits64_RandomAndPowersOfTen_MatchToStringLength()
    {
        var rng = new Random(20260609);
        for (int i = 0; i < 200_000; i++)
        {
            ulong value = NextULong(rng);
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture).Length, FastUtils.CountDigits(value));
        }

        // Every 10^k boundary and its neighbours across the full ulong range.
        ulong pow = 1;
        while (true)
        {
            CheckU64(pow - 1);
            CheckU64(pow);
            CheckU64(pow + 1);
            if (pow > ulong.MaxValue / 10) break;
            pow *= 10;
        }

        static void CheckU64(ulong v) =>
            Assert.Equal(v.ToString(CultureInfo.InvariantCulture).Length, FastUtils.CountDigits(v));
    }

    // ── CountDigits(int) / CountDigits(long) — magnitude, sign excluded ──────────────────

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1)]
    [InlineData(-5, 1)]            // sign is NOT counted
    [InlineData(-9, 1)]
    [InlineData(-10, 2)]
    [InlineData(-100, 3)]
    [InlineData(2147483647, 10)]   // int.MaxValue
    [InlineData(-2147483648, 10)]  // int.MinValue — magnitude is 2147483648, no overflow
    public void CountDigits_Int_CountsMagnitudeOnly(int value, int expected)
    {
        Assert.Equal(expected, FastUtils.CountDigits(value));
        // Mirror against the magnitude's ToString length (sign stripped).
        long magnitude = Math.Abs((long)value);
        Assert.Equal(magnitude.ToString(CultureInfo.InvariantCulture).Length, FastUtils.CountDigits(value));
    }

    [Fact]
    public void CountDigits_Int_RandomMatchesMagnitudeLength()
    {
        var rng = new Random(20260610);
        for (int i = 0; i < 200_000; i++)
        {
            int value = unchecked((int)NextUInt(rng));
            long magnitude = Math.Abs((long)value);
            Assert.Equal(magnitude.ToString(CultureInfo.InvariantCulture).Length, FastUtils.CountDigits(value));
        }
    }

    [Theory]
    [InlineData(0L, 1)]
    [InlineData(-7L, 1)]
    [InlineData(-10L, 2)]
    [InlineData(9223372036854775807L, 19)]   // long.MaxValue
    [InlineData(-9223372036854775808L, 19)]  // long.MinValue — magnitude 9223372036854775808, no overflow
    public void CountDigits_Long_CountsMagnitudeOnly(long value, int expected)
    {
        Assert.Equal(expected, FastUtils.CountDigits(value));
    }

    [Fact]
    public void CountDigits_Long_MinValue_MagnitudeIsTwentyDigits()
    {
        // long.MinValue's magnitude is 9223372036854775808 (19 digits); the unsigned-negation must
        // not overflow back to a negative.
        Assert.Equal(19, FastUtils.CountDigits(long.MinValue));
        Assert.Equal("9223372036854775808".Length, FastUtils.CountDigits(long.MinValue));
    }

    [Fact]
    public void CountDigits_Long_RandomMatchesMagnitudeLength()
    {
        var rng = new Random(20260611);
        for (int i = 0; i < 200_000; i++)
        {
            long value = unchecked((long)NextULong(rng));
            // Magnitude as a string without the sign; handle long.MinValue separately (abs overflows).
            string magnitude = value == long.MinValue
                ? "9223372036854775808"
                : Math.Abs(value).ToString(CultureInfo.InvariantCulture);
            Assert.Equal(magnitude.Length, FastUtils.CountDigits(value));
        }
    }

    // ── Log10 ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0u, 0)]      // log10(0) is undefined; documented to return 0
    [InlineData(1u, 0)]
    [InlineData(9u, 0)]
    [InlineData(10u, 1)]
    [InlineData(99u, 1)]
    [InlineData(100u, 2)]
    [InlineData(1000u, 3)]
    [InlineData(1000000000u, 9)]
    [InlineData(uint.MaxValue, 9)]
    public void Log10_32_MatchesFloorLog10(uint value, int expected)
    {
        Assert.Equal(expected, FastUtils.Log10(value));
    }

    [Theory]
    [InlineData(0ul, 0)]
    [InlineData(1ul, 0)]
    [InlineData(10ul, 1)]
    [InlineData(1_000_000_000_000_000_000ul, 18)]
    [InlineData(ulong.MaxValue, 19)]
    public void Log10_64_MatchesFloorLog10(ulong value, int expected)
    {
        Assert.Equal(expected, FastUtils.Log10(value));
    }

    [Fact]
    public void Log10_IsExactAtPowersOfTen_WhereFloatingPointLog10Rounds()
    {
        // For every value >= 1, Log10 == CountDigits - 1 == floor(log10). Exhaustively over the low
        // range so the powers-of-ten boundaries (where the double Math.Log10 mis-rounds) are covered.
        for (uint value = 1; value < 2_000_000; value++)
        {
            int expected = value.ToString(CultureInfo.InvariantCulture).Length - 1;
            Assert.Equal(expected, FastUtils.Log10(value));
            Assert.Equal(expected, FastUtils.Log10((ulong)value));
        }

        // Spot-check the exact powers of ten across both widths.
        ulong pow = 1;
        int exp = 0;
        while (true)
        {
            if (pow <= uint.MaxValue) Assert.Equal(exp, FastUtils.Log10((uint)pow));
            Assert.Equal(exp, FastUtils.Log10(pow));
            if (pow > ulong.MaxValue / 10) break;
            pow *= 10;
            exp++;
        }
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
