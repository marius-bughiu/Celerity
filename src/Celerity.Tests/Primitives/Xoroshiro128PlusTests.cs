using System.Numerics;
using Celerity.Primitives;

namespace Celerity.Tests.Primitives;

/// <summary>
/// Dedicated coverage for <see cref="Xoroshiro128Plus"/>: an independent reimplementation cross-check
/// (SplitMix64 seeding plus the plus-scrambler advance) and seed-0 safety.
/// </summary>
public class Xoroshiro128PlusTests
{
    [Fact]
    public void MatchesIndependentReimplementation_AcrossSeeds()
    {
        foreach (ulong seed in new ulong[] { 0, 1, 42, 0xDEADBEEF, ulong.MaxValue })
        {
            var rng = new Xoroshiro128Plus(seed);

            ulong sm = seed;
            ulong Next()
            {
                sm += 0x9E3779B97F4A7C15UL;
                ulong z = sm;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }

            ulong s0 = Next(), s1 = Next();

            for (int i = 0; i < 500; i++)
            {
                ulong result = s0 + s1;
                ulong x = s1 ^ s0;
                s0 = BitOperations.RotateLeft(s0, 24) ^ x ^ (x << 16);
                s1 = BitOperations.RotateLeft(x, 37);

                Assert.Equal(result, rng.NextUInt64());
            }
        }
    }

    [Fact]
    public void Seed0_DoesNotStall()
    {
        var rng = new Xoroshiro128Plus(0);
        ulong first = rng.NextUInt64();
        bool varied = false;
        for (int i = 0; i < 100; i++)
            if (rng.NextUInt64() != first) varied = true;
        Assert.True(varied);
    }
}
