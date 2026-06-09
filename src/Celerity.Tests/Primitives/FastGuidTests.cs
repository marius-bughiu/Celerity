using System;
using System.Collections.Generic;
using Celerity.Primitives;
using Xunit;

namespace Celerity.Tests.Primitives;

/// <summary>
/// Correctness coverage for <see cref="FastGuid"/> and <see cref="GuidV7Generator{TRng}"/> (issue #195):
/// version / variant bit correctness, the RFC&#160;9562 big-endian version&#160;7 layout (the 48-bit
/// timestamp in network byte order so the canonical string sorts in time order), strict monotonicity of the
/// generator within and across milliseconds (including counter exhaustion and a backwards clock), and
/// seed-determinism.
/// </summary>
public class FastGuidTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>The 16 GUID bytes in RFC&#160;9562 / big-endian order. <c>ToString("N")</c> renders the GUID's
    /// fields most-significant-first, so its 32 hex digits are exactly the big-endian byte sequence.</summary>
    private static byte[] BigEndianBytes(Guid g)
    {
        string n = g.ToString("N");
        var bytes = new byte[16];
        for (int i = 0; i < 16; i++)
            bytes[i] = Convert.ToByte(n.Substring(i * 2, 2), 16);
        return bytes;
    }

    private static int Version(Guid g) => BigEndianBytes(g)[6] >> 4;

    private static int VariantBits(Guid g) => BigEndianBytes(g)[8] & 0xC0;

    private static long TimestampMs(Guid g)
    {
        byte[] b = BigEndianBytes(g);
        return ((long)b[0] << 40) | ((long)b[1] << 32) | ((long)b[2] << 24)
             | ((long)b[3] << 16) | ((long)b[4] << 8) | b[5];
    }

    /// <summary>Lexicographic (unsigned, big-endian) comparison — the order a database index or a string sort sees.</summary>
    private static int CompareBigEndian(Guid x, Guid y)
    {
        byte[] a = BigEndianBytes(x);
        byte[] b = BigEndianBytes(y);
        for (int i = 0; i < 16; i++)
        {
            if (a[i] != b[i])
                return a[i] < b[i] ? -1 : 1;
        }
        return 0;
    }

    // ── Version 4 ────────────────────────────────────────────────────────────────

    [Fact]
    public void V4_HasCorrectVersionAndVariantBits()
    {
        var rng = new Xoshiro256StarStar(12345);
        for (int i = 0; i < 10_000; i++)
        {
            Guid g = FastGuid.CreateVersion4(ref rng);
            Assert.Equal(4, Version(g));
            Assert.Equal(0x80, VariantBits(g)); // RFC 4122 variant: top two bits are 1 0
        }
    }

    [Fact]
    public void V4_IsDeterministicForASeed()
    {
        var a = new SplitMix64(999);
        var b = new SplitMix64(999);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(FastGuid.CreateVersion4(ref a), FastGuid.CreateVersion4(ref b));
    }

    [Fact]
    public void V4_ProducesDistinctValues()
    {
        var rng = new WyRand(7);
        var seen = new HashSet<Guid>();
        for (int i = 0; i < 50_000; i++)
            Assert.True(seen.Add(FastGuid.CreateVersion4(ref rng)), "version 4 GUID collided");
    }

    // ── Version 7 (stateless) ────────────────────────────────────────────────────

    [Fact]
    public void V7_HasCorrectVersionAndVariantBits()
    {
        var rng = new Xoshiro256StarStar(0xC0FFEE);
        for (int i = 0; i < 10_000; i++)
        {
            Guid g = FastGuid.CreateVersion7(ref rng, 1_700_000_000_000L + i);
            Assert.Equal(7, Version(g));
            Assert.Equal(0x80, VariantBits(g));
        }
    }

    [Fact]
    public void V7_EmbedsTimestampBigEndian()
    {
        // The timestamp occupies the most-significant 48 bits regardless of the random tail, so it must
        // round-trip exactly and lead the canonical string form (the big-endian / sortable layout).
        var rng = new SplitMix64(42);
        const long ms = 0x010203040506L;
        Guid g = FastGuid.CreateVersion7(ref rng, ms);
        Assert.Equal(ms, TimestampMs(g));
        Assert.StartsWith("010203040506", g.ToString("N"));
    }

    [Fact]
    public void V7_SortsByTimestampAcrossMilliseconds()
    {
        var rng = new WyRand(123);
        Guid prev = FastGuid.CreateVersion7(ref rng, 1_000L);
        for (long ms = 1_001L; ms < 6_000L; ms++)
        {
            Guid next = FastGuid.CreateVersion7(ref rng, ms);
            Assert.True(CompareBigEndian(prev, next) < 0,
                $"v7 GUID for ms={ms} did not sort after ms={ms - 1}");
            prev = next;
        }
    }

    [Fact]
    public void V7_IsDeterministicForASeed()
    {
        var a = new SplitMix64(2024);
        var b = new SplitMix64(2024);
        for (long ms = 0; ms < 1000; ms++)
            Assert.Equal(FastGuid.CreateVersion7(ref a, ms), FastGuid.CreateVersion7(ref b, ms));
    }

    // ── Version 7 monotonic generator ────────────────────────────────────────────

    [Fact]
    public void Generator_IsStrictlyMonotonicWithinASingleMillisecond()
    {
        var gen = new GuidV7Generator<Xoshiro256StarStar>(new Xoshiro256StarStar(55));
        const long ms = 1_700_000_000_000L;

        Guid prev = gen.Next(ms);
        Assert.Equal(7, Version(prev));
        Assert.Equal(0x80, VariantBits(prev));

        for (int i = 0; i < 2000; i++) // well within the ~2048-increment headroom
        {
            Guid next = gen.Next(ms);
            Assert.True(CompareBigEndian(prev, next) < 0,
                $"generator was not strictly increasing at same-ms draw {i}");
            Assert.Equal(7, Version(next));
            Assert.Equal(0x80, VariantBits(next));
            prev = next;
        }
    }

    [Fact]
    public void Generator_StaysMonotonicWhenCounterOverflowsWithinAMillisecond()
    {
        // More than the 12-bit counter's 4096 capacity in one millisecond forces the borrow-from-next-ms
        // path; the sequence must remain strictly increasing throughout.
        var gen = new GuidV7Generator<WyRand>(new WyRand(88));
        const long ms = 2_000_000_000_000L;

        Guid prev = gen.Next(ms);
        for (int i = 0; i < 20_000; i++)
        {
            Guid next = gen.Next(ms);
            Assert.True(CompareBigEndian(prev, next) < 0,
                $"generator broke monotonicity across counter overflow at draw {i}");
            prev = next;
        }
    }

    [Fact]
    public void Generator_StaysMonotonicWhenClockMovesBackwards()
    {
        var gen = new GuidV7Generator<SplitMix64>(new SplitMix64(11));

        Guid prev = gen.Next(5_000L);
        // Feed a strictly decreasing timestamp; the counter must carry monotonicity.
        for (long ms = 4_999L; ms > 0L; ms--)
        {
            Guid next = gen.Next(ms);
            Assert.True(CompareBigEndian(prev, next) < 0,
                $"generator broke monotonicity when the clock went back to ms={ms}");
            prev = next;
        }
    }

    [Fact]
    public void Generator_IsMonotonicAcrossAdvancingMilliseconds()
    {
        var gen = new GuidV7Generator<Xoroshiro128Plus>(new Xoroshiro128Plus(404));
        Guid prev = gen.Next(10_000L);
        for (long ms = 10_001L; ms < 15_000L; ms++)
        {
            Guid next = gen.Next(ms);
            Assert.True(CompareBigEndian(prev, next) < 0, $"generator regressed at ms={ms}");
            Assert.Equal(ms, TimestampMs(next));
            prev = next;
        }
    }

    [Fact]
    public void Generator_IsDeterministicForASeed()
    {
        var a = new GuidV7Generator<SplitMix64>(new SplitMix64(7));
        var b = new GuidV7Generator<SplitMix64>(new SplitMix64(7));
        long ms = 9_000L;
        for (int i = 0; i < 5000; i++)
        {
            // Mix same-ms runs and ms advances to exercise both branches deterministically.
            if (i % 7 == 0) ms++;
            Assert.Equal(a.Next(ms), b.Next(ms));
        }
    }

    [Fact]
    public void Generator_ProducesDistinctValues()
    {
        var gen = new GuidV7Generator<WyRand>(new WyRand(2025));
        var seen = new HashSet<Guid>();
        long ms = 3_000_000L;
        for (int i = 0; i < 50_000; i++)
        {
            if (i % 13 == 0) ms++;
            Assert.True(seen.Add(gen.Next(ms)), "version 7 GUID collided");
        }
    }
}
