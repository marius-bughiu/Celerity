using System;
using Celerity.Primitives;

namespace Celerity.Tests.Utils;

/// <summary>
/// Correctness coverage for the <see cref="Branchless"/> guaranteed branch-free conditional select
/// (issue #198): the scalar <c>Select(bool, a, b)</c> for <c>int</c>/<c>long</c>/<c>uint</c>/<c>ulong</c>/
/// <c>float</c>/<c>double</c> and the bulk per-element span blend, each reconciled against the plain
/// <c>condition ? whenTrue : whenFalse</c> ternary it replaces — including the bit-exact float/double cases
/// (signed zero, <c>NaN</c>), aliasing, and the length-mismatch contract.
/// </summary>
public class BranchlessTests
{
    // ── Scalar Select: picks the right operand for both polarities ───────────────────────────

    [Theory]
    [InlineData(true, 7, -3, 7)]
    [InlineData(false, 7, -3, -3)]
    [InlineData(true, int.MinValue, int.MaxValue, int.MinValue)]
    [InlineData(false, int.MinValue, int.MaxValue, int.MaxValue)]
    [InlineData(true, 0, 0, 0)]
    public void Select_Int_MatchesTernary(bool condition, int whenTrue, int whenFalse, int expected)
    {
        Assert.Equal(expected, Branchless.Select(condition, whenTrue, whenFalse));
        Assert.Equal(condition ? whenTrue : whenFalse, Branchless.Select(condition, whenTrue, whenFalse));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Select_Long_MatchesTernary(bool condition)
    {
        long t = long.MinValue, f = long.MaxValue;
        Assert.Equal(condition ? t : f, Branchless.Select(condition, t, f));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Select_UInt_MatchesTernary(bool condition)
    {
        uint t = uint.MaxValue, f = 0u;
        Assert.Equal(condition ? t : f, Branchless.Select(condition, t, f));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Select_ULong_MatchesTernary(bool condition)
    {
        ulong t = ulong.MaxValue, f = 1234567890123UL;
        Assert.Equal(condition ? t : f, Branchless.Select(condition, t, f));
    }

    // ── Float / double: bit-exact, including signed zero and NaN ─────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Select_Float_MatchesTernary(bool condition)
    {
        float t = 3.5f, f = -2.25f;
        Assert.Equal(condition ? t : f, Branchless.Select(condition, t, f));
    }

    [Fact]
    public void Select_Float_PreservesSignedZeroBitExactly()
    {
        // +0f and -0f are == but have different bit patterns; the select must return the chosen one verbatim.
        Assert.Equal(BitConverter.SingleToInt32Bits(-0f), BitConverter.SingleToInt32Bits(Branchless.Select(true, -0f, +0f)));
        Assert.Equal(BitConverter.SingleToInt32Bits(+0f), BitConverter.SingleToInt32Bits(Branchless.Select(false, -0f, +0f)));
    }

    [Fact]
    public void Select_Float_PreservesNaNPayload()
    {
        float nan = BitConverter.Int32BitsToSingle(unchecked((int)0xFFC00001));
        Assert.Equal(BitConverter.SingleToInt32Bits(nan), BitConverter.SingleToInt32Bits(Branchless.Select(true, nan, 1f)));
        Assert.Equal(BitConverter.SingleToInt32Bits(nan), BitConverter.SingleToInt32Bits(Branchless.Select(false, 1f, nan)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Select_Double_MatchesTernary(bool condition)
    {
        double t = double.NegativeInfinity, f = double.MaxValue;
        Assert.Equal(condition ? t : f, Branchless.Select(condition, t, f));
    }

    [Fact]
    public void Select_Double_PreservesSignedZeroBitExactly()
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0d), BitConverter.DoubleToInt64Bits(Branchless.Select(true, -0d, +0d)));
        Assert.Equal(BitConverter.DoubleToInt64Bits(+0d), BitConverter.DoubleToInt64Bits(Branchless.Select(false, -0d, +0d)));
    }

    // ── Randomized reconciliation against the ternary oracle ─────────────────────────────────

    [Fact]
    public void Select_Int_RandomReconcilesWithTernary()
    {
        var rng = new Random(0x198);
        for (int n = 0; n < 10_000; n++)
        {
            bool c = rng.Next(2) == 0;
            int t = rng.Next(int.MinValue, int.MaxValue);
            int f = rng.Next(int.MinValue, int.MaxValue);
            Assert.Equal(c ? t : f, Branchless.Select(c, t, f));
        }
    }

    [Fact]
    public void Select_Long_RandomReconcilesWithTernary()
    {
        var rng = new Random(0x199);
        for (int n = 0; n < 10_000; n++)
        {
            bool c = rng.Next(2) == 0;
            long t = rng.NextInt64();
            long f = rng.NextInt64();
            Assert.Equal(c ? t : f, Branchless.Select(c, t, f));
        }
    }

    // ── Bulk span blend ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Select_IntSpan_BlendsPerElement()
    {
        var cond = new[] { true, false, true, false, true };
        var t = new[] { 1, 2, 3, 4, 5 };
        var f = new[] { 10, 20, 30, 40, 50 };
        var dst = new int[5];

        Branchless.Select(cond, t, f, dst);

        Assert.Equal(new[] { 1, 20, 3, 40, 5 }, dst);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(257)]
    [InlineData(1000)]
    public void Select_IntSpan_MatchesScalarOracle(int length)
    {
        var rng = new Random(0x198 + length);
        var cond = new bool[length];
        var t = new int[length];
        var f = new int[length];
        var dst = new int[length];
        var expected = new int[length];
        for (int i = 0; i < length; i++)
        {
            cond[i] = rng.Next(2) == 0;
            t[i] = rng.Next(int.MinValue, int.MaxValue);
            f[i] = rng.Next(int.MinValue, int.MaxValue);
            expected[i] = cond[i] ? t[i] : f[i];
        }

        Branchless.Select(cond, t, f, dst);

        Assert.Equal(expected, dst);
    }

    [Fact]
    public void Select_LongSpan_MatchesScalarOracle()
    {
        var rng = new Random(0x19A);
        const int length = 333;
        var cond = new bool[length];
        var t = new long[length];
        var f = new long[length];
        var dst = new long[length];
        var expected = new long[length];
        for (int i = 0; i < length; i++)
        {
            cond[i] = rng.Next(2) == 0;
            t[i] = rng.NextInt64();
            f[i] = rng.NextInt64();
            expected[i] = cond[i] ? t[i] : f[i];
        }

        Branchless.Select(cond, t, f, dst);

        Assert.Equal(expected, dst);
    }

    [Fact]
    public void Select_FloatSpan_MatchesScalarOracle()
    {
        var rng = new Random(0x19B);
        const int length = 257;
        var cond = new bool[length];
        var t = new float[length];
        var f = new float[length];
        var dst = new float[length];
        var expected = new float[length];
        for (int i = 0; i < length; i++)
        {
            cond[i] = rng.Next(2) == 0;
            t[i] = (float)(rng.NextDouble() * 2000 - 1000);
            f[i] = (float)(rng.NextDouble() * 2000 - 1000);
            expected[i] = cond[i] ? t[i] : f[i];
        }

        Branchless.Select(cond, t, f, dst);

        Assert.Equal(expected, dst);
    }

    [Fact]
    public void Select_DoubleSpan_MatchesScalarOracle()
    {
        var rng = new Random(0x19C);
        const int length = 129;
        var cond = new bool[length];
        var t = new double[length];
        var f = new double[length];
        var dst = new double[length];
        var expected = new double[length];
        for (int i = 0; i < length; i++)
        {
            cond[i] = rng.Next(2) == 0;
            t[i] = rng.NextDouble() * 2000 - 1000;
            f[i] = rng.NextDouble() * 2000 - 1000;
            expected[i] = cond[i] ? t[i] : f[i];
        }

        Branchless.Select(cond, t, f, dst);

        Assert.Equal(expected, dst);
    }

    [Fact]
    public void Select_Span_CanWriteInPlaceOverWhenTrue()
    {
        // destination aliasing whenTrue must still produce the per-element ternary (each element is read
        // before it is overwritten).
        var cond = new[] { true, false, true };
        var t = new[] { 1, 2, 3 };
        var f = new[] { 9, 8, 7 };

        Branchless.Select(cond, t, f, t);

        Assert.Equal(new[] { 1, 8, 3 }, t);
    }

    [Fact]
    public void Select_EmptySpans_NoOp()
    {
        Branchless.Select(ReadOnlySpan<bool>.Empty, ReadOnlySpan<int>.Empty, ReadOnlySpan<int>.Empty, Span<int>.Empty);
    }

    // ── Length-mismatch contract ─────────────────────────────────────────────────────────────

    [Fact]
    public void Select_Span_MismatchedLengths_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Branchless.Select(new[] { true, false }, new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new int[3]));
        Assert.Throws<ArgumentException>(() =>
            Branchless.Select(new[] { true, false, true }, new[] { 1, 2 }, new[] { 4, 5, 6 }, new int[3]));
        Assert.Throws<ArgumentException>(() =>
            Branchless.Select(new[] { true, false, true }, new[] { 1, 2, 3 }, new[] { 4, 5 }, new int[3]));
        Assert.Throws<ArgumentException>(() =>
            Branchless.Select(new[] { true, false, true }, new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new int[2]));
    }
}
