using Celerity.Primitives;

namespace Celerity.Tests.Primitives;

/// <summary>
/// Dedicated coverage for <see cref="WyRand"/>: an independent reimplementation cross-check of the
/// add-then-128-bit-multiply-fold core, and a long-run distinct-output check.
/// </summary>
public class WyRandTests
{
    [Fact]
    public void MatchesIndependentReimplementation_AcrossSeeds()
    {
        foreach (ulong seed in new ulong[] { 0, 1, 42, 0xC0FFEE, ulong.MaxValue })
        {
            var rng = new WyRand(seed);
            ulong state = seed;
            for (int i = 0; i < 500; i++)
            {
                state += 0xA0761D6478BD642FUL;
                UInt128 t = (UInt128)state * (state ^ 0xE7037ED1A0B428DBUL);
                ulong expected = (ulong)(t >> 64) ^ (ulong)t;
                Assert.Equal(expected, rng.NextUInt64());
            }
        }
    }

    [Fact]
    public void Seed0_DoesNotStall()
    {
        var rng = new WyRand(0);
        ulong first = rng.NextUInt64();
        bool varied = false;
        for (int i = 0; i < 100; i++)
            if (rng.NextUInt64() != first) varied = true;
        Assert.True(varied);
    }
}
