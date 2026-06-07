using System;
using System.Collections.Generic;
using Celerity.Primitives;

namespace Celerity.Tests.Primitives;

/// <summary>
/// The cross-generator contract every Celerity struct PRNG must satisfy, run identically across the whole
/// suite (<see cref="SplitMix64"/>, <see cref="Xoshiro256StarStar"/>, <see cref="Xoroshiro128Plus"/>,
/// <see cref="WyRand"/>, <see cref="Pcg32"/>) via the shared <see cref="RandomSourceExtensions"/> surface:
/// determinism / reproducibility, the half-open ranges of the floating-point and bounded-integer helpers,
/// unbiased bounds, full-range coverage, and statistical sanity. Each generator gets its own <c>[Fact]</c>
/// rows so a failure names the generator, while the assertions live once in the generic helpers.
/// </summary>
public class RandomSourceContractTests
{
    // ── Reproducibility: same seed ⇒ identical stream; different seed ⇒ different stream ────────────────
    [Fact] public void SplitMix64_IsReproducible() => AssertReproducible(s => new SplitMix64(s));
    [Fact] public void Xoshiro_IsReproducible() => AssertReproducible(s => new Xoshiro256StarStar(s));
    [Fact] public void Xoroshiro_IsReproducible() => AssertReproducible(s => new Xoroshiro128Plus(s));
    [Fact] public void WyRand_IsReproducible() => AssertReproducible(s => new WyRand(s));
    [Fact] public void Pcg32_IsReproducible() => AssertReproducible(s => new Pcg32(s));

    private static void AssertReproducible<TRng>(Func<ulong, TRng> factory) where TRng : struct, IRandomSource
    {
        foreach (ulong seed in Seeds)
        {
            var a = factory(seed);
            var b = factory(seed);
            for (int i = 0; i < 1000; i++)
                Assert.Equal(a.NextUInt64(), b.NextUInt64());
        }

        // Two distinct seeds must not produce the same first output (overwhelmingly likely; a fixed check).
        var s1 = factory(1);
        var s2 = factory(2);
        Assert.NotEqual(s1.NextUInt64(), s2.NextUInt64());
    }

    // ── The generator never stalls (a stuck all-equal or all-zero stream is the classic seeding bug) ────
    [Fact] public void SplitMix64_DoesNotStall() => AssertDoesNotStall(s => new SplitMix64(s));
    [Fact] public void Xoshiro_DoesNotStall() => AssertDoesNotStall(s => new Xoshiro256StarStar(s));
    [Fact] public void Xoroshiro_DoesNotStall() => AssertDoesNotStall(s => new Xoroshiro128Plus(s));
    [Fact] public void WyRand_DoesNotStall() => AssertDoesNotStall(s => new WyRand(s));
    [Fact] public void Pcg32_DoesNotStall() => AssertDoesNotStall(s => new Pcg32(s));

    private static void AssertDoesNotStall<TRng>(Func<ulong, TRng> factory) where TRng : struct, IRandomSource
    {
        // Seed 0 is the dangerous one: a generator that seeds its multi-word state naively would lock at
        // zero. The SplitMix64 expansion must prevent that.
        var rng = factory(0);
        var seen = new HashSet<ulong>();
        ulong first = rng.NextUInt64();
        seen.Add(first);
        bool sawDifferent = false;
        for (int i = 0; i < 2000; i++)
        {
            ulong v = rng.NextUInt64();
            seen.Add(v);
            if (v != first) sawDifferent = true;
        }

        Assert.True(sawDifferent, "generator produced a constant stream");
        // 2001 draws from a good 64-bit generator should be (essentially) all distinct.
        Assert.True(seen.Count > 1990, $"only {seen.Count} distinct of 2001 — suspicious collision rate");
    }

    // ── NextDouble / NextSingle are confined to [0, 1) ──────────────────────────────────────────────────
    [Fact] public void SplitMix64_UnitInterval() => AssertUnitInterval(s => new SplitMix64(s));
    [Fact] public void Xoshiro_UnitInterval() => AssertUnitInterval(s => new Xoshiro256StarStar(s));
    [Fact] public void Xoroshiro_UnitInterval() => AssertUnitInterval(s => new Xoroshiro128Plus(s));
    [Fact] public void WyRand_UnitInterval() => AssertUnitInterval(s => new WyRand(s));
    [Fact] public void Pcg32_UnitInterval() => AssertUnitInterval(s => new Pcg32(s));

    private static void AssertUnitInterval<TRng>(Func<ulong, TRng> factory) where TRng : struct, IRandomSource
    {
        var rng = factory(0xC0FFEE);
        double sum = 0;
        for (int i = 0; i < 100_000; i++)
        {
            double d = rng.NextDouble();
            Assert.InRange(d, 0.0, 0.9999999999999999);
            sum += d;

            float f = rng.NextSingle();
            Assert.InRange(f, 0.0f, 0.99999994f);
        }

        // Mean of a uniform [0,1) sample should sit near 0.5; a wide tolerance keeps this deterministic-safe.
        double mean = sum / 100_000;
        Assert.InRange(mean, 0.48, 0.52);
    }

    // ── Bounded NextInt is in range, unbiased enough to hit every bucket, and never escapes ─────────────
    [Fact] public void SplitMix64_BoundedInt() => AssertBoundedInt(s => new SplitMix64(s));
    [Fact] public void Xoshiro_BoundedInt() => AssertBoundedInt(s => new Xoshiro256StarStar(s));
    [Fact] public void Xoroshiro_BoundedInt() => AssertBoundedInt(s => new Xoroshiro128Plus(s));
    [Fact] public void WyRand_BoundedInt() => AssertBoundedInt(s => new WyRand(s));
    [Fact] public void Pcg32_BoundedInt() => AssertBoundedInt(s => new Pcg32(s));

    private static void AssertBoundedInt<TRng>(Func<ulong, TRng> factory) where TRng : struct, IRandomSource
    {
        var rng = factory(42);

        // Single-arg overload: [0, max).
        const int max = 6;
        var counts = new int[max];
        for (int i = 0; i < 60_000; i++)
        {
            int v = rng.NextInt(max);
            Assert.InRange(v, 0, max - 1);
            counts[v]++;
        }
        foreach (int c in counts)
            Assert.True(c > 0, "a bucket was never hit — distribution is broken");

        // Two-arg overload with a negative lower bound.
        for (int i = 0; i < 50_000; i++)
        {
            int v = rng.NextInt(-10, 10);
            Assert.InRange(v, -10, 9);
        }

        // min == max returns min without consuming bias.
        Assert.Equal(7, rng.NextInt(7, 7));

        // 64-bit bounded range, including a span wider than 2^32.
        for (int i = 0; i < 50_000; i++)
        {
            long v = rng.NextInt64(-5_000_000_000L, 5_000_000_000L);
            Assert.InRange(v, -5_000_000_000L, 4_999_999_999L);
        }
        Assert.Equal(-3L, rng.NextInt64(-3, -3));
    }

    // ── NextInt argument validation ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void NextInt_InvalidBounds_Throw()
    {
        var rng = new Xoshiro256StarStar(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt64(5, 4));
    }

    // ── Full-int-range NextInt(min, max) covers the boundary endpoints ──────────────────────────────────
    [Fact]
    public void NextInt_FullRange_StaysInBounds()
    {
        var rng = new WyRand(123);
        for (int i = 0; i < 100_000; i++)
        {
            int v = rng.NextInt(int.MinValue, int.MaxValue);
            Assert.True(v >= int.MinValue && v < int.MaxValue);
        }

        var rng64 = new WyRand(456);
        for (int i = 0; i < 100_000; i++)
        {
            long v = rng64.NextInt64(long.MinValue, long.MaxValue);
            Assert.True(v >= long.MinValue && v < long.MaxValue);
        }
    }

    // ── NextBytes fills the whole span (including a non-multiple-of-8 tail) and is reproducible ─────────
    [Fact] public void SplitMix64_NextBytes() => AssertNextBytes(s => new SplitMix64(s));
    [Fact] public void Xoshiro_NextBytes() => AssertNextBytes(s => new Xoshiro256StarStar(s));
    [Fact] public void Pcg32_NextBytes() => AssertNextBytes(s => new Pcg32(s));

    private static void AssertNextBytes<TRng>(Func<ulong, TRng> factory) where TRng : struct, IRandomSource
    {
        // 21 bytes = two full 8-byte words plus a 5-byte tail.
        var a = new byte[21];
        var b = new byte[21];
        var r1 = factory(99);
        var r2 = factory(99);
        r1.NextBytes(a);
        r2.NextBytes(b);
        Assert.Equal(a, b); // reproducible

        // Every byte should have been written (the tail too): a freshly-zeroed buffer is overwhelmingly
        // unlikely to stay all-zero, and at least one of the last five (tail) bytes should be non-zero.
        bool tailWritten = false;
        for (int i = 16; i < 21; i++)
            if (a[i] != 0) tailWritten = true;
        Assert.True(tailWritten, "tail bytes were not filled");

        // Empty span is a no-op (must not throw).
        r1.NextBytes(Span<byte>.Empty);
    }

    // ── NextBool is roughly balanced ────────────────────────────────────────────────────────────────────
    [Fact]
    public void NextBool_IsBalanced()
    {
        var rng = new Xoshiro256StarStar(7);
        int trues = 0;
        for (int i = 0; i < 100_000; i++)
            if (rng.NextBool()) trues++;
        Assert.InRange(trues, 48_000, 52_000);
    }

    // ── Generic-algorithm reach: a shuffle driven by IRandomSource through the constrained generic path ─
    [Fact]
    public void GenericShuffle_IsPermutationAndReproducible()
    {
        int[] Shuffle<TRng>(TRng seedRng) where TRng : struct, IRandomSource
        {
            var rng = seedRng;
            var a = new int[100];
            for (int i = 0; i < a.Length; i++) a[i] = i;
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
            return a;
        }

        int[] first = Shuffle(new Xoshiro256StarStar(2024));
        int[] second = Shuffle(new Xoshiro256StarStar(2024));
        Assert.Equal(first, second); // deterministic

        // Still a permutation of 0..99.
        var sorted = (int[])first.Clone();
        Array.Sort(sorted);
        for (int i = 0; i < 100; i++) Assert.Equal(i, sorted[i]);

        // A different seed yields a different ordering.
        int[] other = Shuffle(new Xoshiro256StarStar(2025));
        Assert.NotEqual(first, other);
    }

    private static readonly ulong[] Seeds =
    {
        0, 1, 2, 42, 0xC0FFEE, 0xDEADBEEF, ulong.MaxValue, 0x9E3779B97F4A7C15UL,
    };
}
