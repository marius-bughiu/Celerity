using System.Collections.Generic;
using System.Numerics;
using Celerity.Primitives;

namespace Celerity.Tests.Primitives;

/// <summary>
/// Dedicated coverage for <see cref="Pcg32"/>: an independent reimplementation cross-check of the
/// XSH-RR output permutation and LCG advance, the independent-stream property, the
/// <see cref="Pcg32.NextUInt32"/> native-output width, and the two-draw <see cref="Pcg32.NextUInt64"/>
/// composition.
/// </summary>
public class Pcg32Tests
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong DefaultStream = 0xDA3E39CB94B95BDBUL;

    [Fact]
    public void NextUInt32_MatchesIndependentReimplementation()
    {
        foreach (ulong seed in new ulong[] { 0, 1, 42, 0xDEADBEEF, ulong.MaxValue })
        {
            var rng = new Pcg32(seed);

            // Independent reference seeding + step.
            ulong inc = (DefaultStream << 1) | 1UL;
            ulong state = 0UL;
            state = state * Multiplier + inc;
            state += seed;
            state = state * Multiplier + inc;

            uint Ref()
            {
                ulong old = state;
                state = old * Multiplier + inc;
                uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
                int rot = (int)(old >> 59);
                return BitOperations.RotateRight(xorShifted, rot);
            }

            for (int i = 0; i < 500; i++)
                Assert.Equal(Ref(), rng.NextUInt32());
        }
    }

    [Fact]
    public void NextUInt64_ConcatenatesTwo32BitDraws()
    {
        var a = new Pcg32(777);
        var b = new Pcg32(777);

        ulong combined = a.NextUInt64();
        ulong hi = b.NextUInt32();
        ulong lo = b.NextUInt32();
        Assert.Equal((hi << 32) | lo, combined);
    }

    [Fact]
    public void DifferentStreams_AreUncorrelated()
    {
        // Same seed, different sequence selectors ⇒ different (independent) streams.
        var a = new Pcg32(42, sequence: 1);
        var b = new Pcg32(42, sequence: 2);

        int differences = 0;
        for (int i = 0; i < 1000; i++)
            if (a.NextUInt32() != b.NextUInt32()) differences++;
        Assert.True(differences > 990, $"streams looked correlated ({differences}/1000 differed)");
    }

    [Fact]
    public void NextUInt32_DoesNotStall_AndCoversSpace()
    {
        var rng = new Pcg32(0);
        var seen = new HashSet<uint>();
        for (int i = 0; i < 2000; i++) seen.Add(rng.NextUInt32());
        Assert.True(seen.Count > 1990);
    }
}
