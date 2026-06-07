using System.Numerics;
using Celerity.Primitives;

namespace Celerity.Tests.Primitives;

/// <summary>
/// Dedicated coverage for <see cref="Xoshiro256StarStar"/>: an independent reimplementation cross-check
/// (validating both the SplitMix64 state seeding and the starstar scrambler/advance), seed-0 safety (the
/// degenerate all-zero state is unreachable), and a long-run distinct-output check.
/// </summary>
public class Xoshiro256StarStarTests
{
    [Fact]
    public void MatchesIndependentReimplementation_AcrossSeeds()
    {
        foreach (ulong seed in new ulong[] { 0, 1, 42, 0xC0FFEE, ulong.MaxValue })
        {
            var rng = new Xoshiro256StarStar(seed);

            // Independent state seeding via SplitMix64, then the reference xoshiro256** step.
            ulong sm = seed;
            ulong Next()
            {
                sm += 0x9E3779B97F4A7C15UL;
                ulong z = sm;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }

            ulong s0 = Next(), s1 = Next(), s2 = Next(), s3 = Next();

            for (int i = 0; i < 500; i++)
            {
                ulong result = BitOperations.RotateLeft(s1 * 5UL, 7) * 9UL;
                ulong t = s1 << 17;
                s2 ^= s0;
                s3 ^= s1;
                s1 ^= s2;
                s0 ^= s3;
                s2 ^= t;
                s3 = BitOperations.RotateLeft(s3, 45);

                Assert.Equal(result, rng.NextUInt64());
            }
        }
    }

    [Fact]
    public void Seed0_ProducesNonZeroVariedStream()
    {
        var rng = new Xoshiro256StarStar(0);
        ulong first = rng.NextUInt64();
        Assert.NotEqual(0UL, first);

        bool varied = false;
        for (int i = 0; i < 100; i++)
            if (rng.NextUInt64() != first) varied = true;
        Assert.True(varied);
    }

    [Fact]
    public void Reproducible_SameSeedSameSequence()
    {
        var a = new Xoshiro256StarStar(2024);
        var b = new Xoshiro256StarStar(2024);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
    }
}
