using System;
using Celerity.Primitives;

namespace Celerity.Tests.Primitives;

/// <summary>
/// Drives the rejection branch of Lemire's nearly-divisionless bounded-range algorithm in
/// <see cref="RandomSourceExtensions"/> — the retry loop that a real PRNG essentially never enters.
/// </summary>
/// <remarks>
/// <para>
/// <c>NextInt</c> / <c>NextInt64</c> multiply one random word by the range and keep the high half. That is
/// unbiased only if the low half is discarded uniformly, so the algorithm rejects and redraws whenever the
/// low half lands under <c>threshold = (2^W - range) % range</c> — the count of residues that would otherwise
/// make some outputs marginally more likely than others. For a real generator that window is vanishingly
/// narrow (for <c>range = 3</c> it is a single value out of 2^32), which is exactly why the retry body is
/// unreachable from the statistical tests in <c>RandomSourceContractTests</c>: the unbiasedness guarantee the
/// library advertises rests on a code path no natural stream visits.
/// </para>
/// <para>
/// So these tests substitute a <see cref="ScriptedRandomSource"/> — a struct <see cref="IRandomSource"/> that
/// replays a fixed list of 64-bit words and counts its draws — and hand-pick words that provably land in the
/// rejection window. Each test asserts both halves of the contract: the returned sample is still in range,
/// and the generator was drawn from more than once (proving the redraw actually happened rather than the
/// first, biased sample being returned). Because the extension methods take the generator by <c>ref</c>, the
/// struct's draw counter is mutated in place and is readable from the caller's local afterwards.
/// </para>
/// <para>
/// The arithmetic behind the chosen words is spelled out per test; the fake throws if it is drawn from more
/// often than scripted, so an unexpected extra redraw fails loudly instead of silently reading past the end.
/// </para>
/// </remarks>
public class RandomSourceRejectionSamplingTests
{
    /// <summary>
    /// A deterministic <see cref="IRandomSource"/> that replays a fixed list of 64-bit words in order and
    /// records how many were drawn. Throws once the script is exhausted, so an unexpected extra redraw is a
    /// visible failure rather than a silent one.
    /// </summary>
    private struct ScriptedRandomSource : IRandomSource
    {
        private readonly ulong[] _values;
        private int _drawn;

        public ScriptedRandomSource(params ulong[] values)
        {
            _values = values;
            _drawn = 0;
        }

        /// <summary>The number of 64-bit words drawn so far.</summary>
        public int DrawCount => _drawn;

        public ulong NextUInt64()
        {
            if (_drawn >= _values.Length)
                throw new InvalidOperationException(
                    $"The scripted random source was drawn from more than the scripted {_values.Length} time(s).");

            return _values[_drawn++];
        }
    }

    /// <summary>
    /// Packs a 32-bit value into the high 32 bits of a 64-bit word. <c>NextUInt32</c> is defined as
    /// <c>NextUInt64() &gt;&gt; 32</c>, so this is how a script controls the 32-bit path's draws.
    /// </summary>
    private static ulong High32(uint value) => (ulong)value << 32;

    // ── 32-bit path ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NextInt_ShouldRedraw_WhenTheFirstSampleFallsInsideTheRejectionWindow()
    {
        // range = 3 ⇒ threshold = (2^32 - 3) % 3 = 1, so only a zero low half is rejected.
        //   draw 1: x = 0          → m = 0,           low = 0          → 0 < 1, rejected.
        //   draw 2: x = 0x80000000 → m = 0x1_80000000, low = 0x80000000 → accepted, high half = 1.
        var rng = new ScriptedRandomSource(High32(0u), High32(0x8000_0000u));

        int value = rng.NextInt(3);

        Assert.Equal(1, value);
        Assert.InRange(value, 0, 2);
        Assert.Equal(2, rng.DrawCount);
    }

    [Fact]
    public void NextInt_ShouldKeepRedrawing_WhenSeveralConsecutiveSamplesAreRejected()
    {
        // Same range = 3 / threshold = 1 arithmetic, rejected three times before an acceptable word arrives.
        var rng = new ScriptedRandomSource(
            High32(0u), High32(0u), High32(0u), High32(0x8000_0000u));

        int value = rng.NextInt(3);

        Assert.Equal(1, value);
        Assert.Equal(4, rng.DrawCount);
    }

    [Fact]
    public void NextInt_ShouldRedraw_WhenTheRejectedLowHalfIsNonZero()
    {
        // range = 6 ⇒ threshold = (2^32 - 6) % 6 = 4, so a whole band of low halves is rejected, not just 0.
        //   draw 1: x = 715_827_883 → m = 4_294_967_298 = 2^32 + 2, low = 2 → 2 < 4, rejected.
        //   draw 2: x = 0x40000000  → m = 6 * 2^30 = 2^32 + 2^31,   low = 0x80000000 → accepted, high half = 1.
        var rng = new ScriptedRandomSource(High32(715_827_883u), High32(0x4000_0000u));

        int value = rng.NextInt(6);

        Assert.Equal(1, value);
        Assert.InRange(value, 0, 5);
        Assert.Equal(2, rng.DrawCount);
    }

    [Fact]
    public void NextInt_ShouldRedraw_WhenTheOffsetOverloadRejectsTheFirstSample()
    {
        // The (min, max) overload funnels through the same bounded helper with range = max - min, then adds
        // the offset back, so the rejection window is identical.
        var rng = new ScriptedRandomSource(High32(0u), High32(0x8000_0000u));

        int value = rng.NextInt(100, 103);

        Assert.Equal(101, value);
        Assert.InRange(value, 100, 102);
        Assert.Equal(2, rng.DrawCount);
    }

    [Fact]
    public void NextInt_ShouldNotRedraw_WhenTheFirstSampleIsOutsideTheRejectionWindow()
    {
        // The contrast case: an acceptable first word must consume exactly one draw, which is what makes the
        // higher draw counts above evidence of the retry loop rather than of a per-call fixed cost.
        var rng = new ScriptedRandomSource(High32(0x8000_0000u));

        int value = rng.NextInt(3);

        Assert.Equal(1, value);
        Assert.Equal(1, rng.DrawCount);
    }

    // ── 64-bit path ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NextInt64_ShouldRedraw_WhenTheFirstSampleFallsInsideTheRejectionWindow()
    {
        // range = 3 ⇒ threshold = (2^64 - 3) % 3 = 1 (2^64 ≡ 1 mod 3), so only a zero low half is rejected.
        //   draw 1: x = 0                  → m = 0,          low = 0     → 0 < 1, rejected.
        //   draw 2: x = 2^63               → m = 2^64 + 2^63, low = 2^63 → accepted, high half = 1.
        var rng = new ScriptedRandomSource(0UL, 0x8000_0000_0000_0000UL);

        long value = rng.NextInt64(0, 3);

        Assert.Equal(1L, value);
        Assert.InRange(value, 0L, 2L);
        Assert.Equal(2, rng.DrawCount);
    }

    [Fact]
    public void NextInt64_ShouldKeepRedrawing_WhenSeveralConsecutiveSamplesAreRejected()
    {
        var rng = new ScriptedRandomSource(0UL, 0UL, 0UL, 0x8000_0000_0000_0000UL);

        long value = rng.NextInt64(0, 3);

        Assert.Equal(1L, value);
        Assert.Equal(4, rng.DrawCount);
    }

    [Fact]
    public void NextInt64_ShouldRedraw_WhenTheRejectedLowHalfIsNonZero()
    {
        // range = 6 ⇒ threshold = (2^64 - 6) % 6 = 4.
        //   draw 1: x = 3_074_457_345_618_258_603 → 6x = 2^64 + 2, low = 2 → 2 < 4, rejected.
        //   draw 2: x = 2^62                      → 6x = 2^64 + 2^63, low = 2^63 → accepted, high half = 1.
        var rng = new ScriptedRandomSource(3_074_457_345_618_258_603UL, 0x4000_0000_0000_0000UL);

        long value = rng.NextInt64(0, 6);

        Assert.Equal(1L, value);
        Assert.InRange(value, 0L, 5L);
        Assert.Equal(2, rng.DrawCount);
    }

    [Fact]
    public void NextInt64_ShouldRedraw_WhenTheLowerBoundIsNegative()
    {
        // The offset is applied after the bounded draw, so a negative floor does not change the window.
        var rng = new ScriptedRandomSource(0UL, 0x8000_0000_0000_0000UL);

        long value = rng.NextInt64(-1_000L, -997L);

        Assert.Equal(-999L, value);
        Assert.InRange(value, -1_000L, -998L);
        Assert.Equal(2, rng.DrawCount);
    }

    [Fact]
    public void NextInt64_ShouldNotRedraw_WhenTheFirstSampleIsOutsideTheRejectionWindow()
    {
        var rng = new ScriptedRandomSource(0x8000_0000_0000_0000UL);

        long value = rng.NextInt64(0, 3);

        Assert.Equal(1L, value);
        Assert.Equal(1, rng.DrawCount);
    }

    // ── The fake itself behaves like a generator ────────────────────────────────────────────────────────

    [Fact]
    public void ScriptedRandomSource_ShouldThrow_WhenDrawnFromBeyondItsScript()
    {
        // Guards the tests above: if the retry loop ever consumed more words than scripted, the assertions
        // would fail with this exception rather than reading stale data.
        var rng = new ScriptedRandomSource(1UL);

        Assert.Equal(1UL, rng.NextUInt64());
        Assert.Throws<InvalidOperationException>(() => { rng.NextUInt64(); });
    }
}
