using System;
using System.Collections.Generic;
using Celerity.Primitives;

namespace Celerity.Tests.Primitives;

/// <summary>
/// Covers <see cref="GuidV7Generator{TRng}.Next()"/> — the parameterless, wall-clock entry point — which the
/// main <c>FastGuidTests</c> suite skips entirely because every assertion there uses the explicit-timestamp
/// overload so the clock can be scripted.
/// </summary>
/// <remarks>
/// <para>
/// That gap matters: <c>Next()</c> is the overload real callers use (the timestamped one exists for tests and
/// custom clocks), and it is the only place where the generator reads <c>DateTimeOffset.UtcNow</c> and
/// converts it to Unix milliseconds. A regression there — the wrong epoch, seconds instead of milliseconds, a
/// local-time read — would leave every other test green while shipping GUIDs whose embedded timestamp does not
/// match the wall clock and therefore no longer sorts against IDs minted by other processes.
/// </para>
/// <para>
/// So these tests pin the same invariants the timestamped overload's tests pin — RFC&#160;9562 version and
/// variant bits, the 48-bit big-endian timestamp in the leading bytes, strict monotonicity and distinctness
/// across a burst — plus the two properties only the parameterless overload can have: the embedded timestamp
/// must bracket a wall-clock reading taken around the call, and the call must advance the <em>same</em>
/// monotonic state the timestamped overload uses, so mixing the two overloads on one generator cannot produce
/// a value that sorts backwards.
/// </para>
/// </remarks>
public class GuidV7GeneratorCoverageTests
{
    // ── Helpers (RFC 9562 / big-endian views; duplicated from FastGuidTests, which keeps them private) ───

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

    // ── The wall-clock read ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Next_ShouldEmbedTheCurrentUnixMillisecond_WhenNoTimestampIsSupplied()
    {
        // A freshly constructed generator has no recorded timestamp, so the first draw takes the
        // advance-the-clock branch verbatim: the embedded 48-bit field is exactly the reading Next() took,
        // which must sit between two readings straddling the call.
        var gen = new GuidV7Generator<Xoshiro256StarStar>(new Xoshiro256StarStar(20260726));

        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Guid g = gen.Next();
        long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // The wall clock is not monotonic, so neither bound is trustworthy to the millisecond: a backward NTP
        // step before the call inverts the straddle, and one *after* the call can leave the embedded reading
        // above both samples. Ordering the bounds fixes the first; a second of slack fixes the second.
        //
        // Nothing this test exists to catch is lost by that slack. It is here to prove Next() reads the wall
        // clock in the right epoch and the right unit, and those failures are not close calls — a .NET-ticks
        // or 1900 epoch is wrong by decades, and seconds-for-milliseconds by a factor of a thousand. A
        // millisecond-exact bound would only buy the ability to fail on a clock adjustment.
        const long SlackMs = 1000;
        Assert.InRange(TimestampMs(g), Math.Min(before, after) - SlackMs, Math.Max(before, after) + SlackMs);

        // The timestamp also leads the canonical string form, which is what makes v7 lexicographically
        // sortable — a wrong-epoch or wrong-unit read would break that even if the range check passed.
        Assert.StartsWith(TimestampMs(g).ToString("x12"), g.ToString("N"), StringComparison.Ordinal);
    }

    [Fact]
    public void Next_ShouldProduceVersion7AndTheRfc9562Variant_WhenNoTimestampIsSupplied()
    {
        var gen = new GuidV7Generator<WyRand>(new WyRand(0xC0FFEE));
        for (int i = 0; i < 500; i++)
        {
            Guid g = gen.Next();
            Assert.Equal(7, Version(g));
            Assert.Equal(0x80, VariantBits(g)); // RFC 9562 variant: the top two bits are 1 0.
        }
    }

    // ── Monotonicity of the wall-clock path ─────────────────────────────────────────────────────────────

    [Fact]
    public void Next_ShouldBeStrictlyIncreasingAndDistinct_WhenCalledRepeatedly()
    {
        // A tight burst lands many draws inside a single millisecond, so this exercises the monotonic-counter
        // branch through the wall-clock overload — the case the stateless FastGuid.CreateVersion7 cannot make
        // strictly increasing and that this generator exists to fix.
        var gen = new GuidV7Generator<SplitMix64>(new SplitMix64(55));
        var seen = new HashSet<Guid>();

        Guid prev = gen.Next();
        seen.Add(prev);
        long previousTimestamp = TimestampMs(prev);

        for (int i = 0; i < 5_000; i++)
        {
            Guid next = gen.Next();
            Assert.True(CompareBigEndian(prev, next) < 0,
                $"Next() was not strictly increasing at draw {i}");
            Assert.True(seen.Add(next), $"Next() repeated a value at draw {i}");

            // The embedded timestamp may stay put (same millisecond) or move forward, but never backwards.
            long timestamp = TimestampMs(next);
            Assert.True(timestamp >= previousTimestamp,
                $"the embedded timestamp went backwards at draw {i}: {timestamp} < {previousTimestamp}");

            previousTimestamp = timestamp;
            prev = next;
        }
    }

    // ── Shared state with the timestamped overload ──────────────────────────────────────────────────────

    [Fact]
    public void Next_ShouldAdvanceTheSameMonotonicState_WhenMixedWithTheTimestampedOverload()
    {
        // Next() must record its wall-clock reading in the same field Next(long) consults. If it did not, a
        // subsequent explicit call with an older timestamp would emit a GUID that sorts before the previous
        // one — exactly the ordering break the generator promises cannot happen.
        var gen = new GuidV7Generator<WyRand>(new WyRand(4242));

        Guid fromClock = gen.Next();
        Guid fromStaleClock = gen.Next(1_000L); // far in the past: 1970-01-01T00:00:01Z

        Assert.True(CompareBigEndian(fromClock, fromStaleClock) < 0,
            "a stale explicit timestamp after Next() produced a value that sorts backwards");

        // The stale timestamp is discarded in favour of the wall-clock millisecond Next() already recorded.
        Assert.Equal(TimestampMs(fromClock), TimestampMs(fromStaleClock));
        Assert.NotEqual(1_000L, TimestampMs(fromStaleClock));
        Assert.Equal(7, Version(fromStaleClock));
        Assert.Equal(0x80, VariantBits(fromStaleClock));
    }

    [Fact]
    public void Next_ShouldContinueTheSequence_WhenAnEarlierTimestampedCallSetTheState()
    {
        // The mirror of the previous test: a timestamped call far in the past must not make the following
        // wall-clock draw non-monotonic (here the clock jumps forward, taking the advance branch).
        var gen = new GuidV7Generator<Xoroshiro128Plus>(new Xoroshiro128Plus(404));

        Guid fromPast = gen.Next(1_000L);
        Guid fromClock = gen.Next();

        Assert.Equal(1_000L, TimestampMs(fromPast));
        Assert.True(CompareBigEndian(fromPast, fromClock) < 0,
            "Next() did not sort after a GUID stamped in 1970");
        Assert.True(TimestampMs(fromClock) > 1_000L,
            "Next() did not move the embedded timestamp forward to the wall clock");
    }
}
