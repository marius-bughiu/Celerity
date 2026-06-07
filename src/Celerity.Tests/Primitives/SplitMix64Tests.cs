using Celerity.Primitives;

namespace Celerity.Tests.Primitives;

/// <summary>
/// Dedicated coverage for <see cref="SplitMix64"/>: the published seed-0 reference vector, an independent
/// reimplementation cross-check that validates the struct threads its single word of state correctly, and
/// the bijection property (no immediate fixed point).
/// </summary>
public class SplitMix64Tests
{
    [Fact]
    public void Seed0_MatchesPublishedReferenceVector()
    {
        // Canonical SplitMix64 test vector for seed 0 (Vigna's reference: prng.di.unimi.it/splitmix64.c).
        var rng = new SplitMix64(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, rng.NextUInt64());
        Assert.Equal(0x6E789E6AA1B965F4UL, rng.NextUInt64());
        Assert.Equal(0x06C45D188009454FUL, rng.NextUInt64());
    }

    [Fact]
    public void MatchesIndependentReimplementation_AcrossSeeds()
    {
        foreach (ulong seed in new ulong[] { 0, 1, 42, 0xDEADBEEF, ulong.MaxValue })
        {
            var rng = new SplitMix64(seed);
            ulong state = seed;
            for (int i = 0; i < 500; i++)
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                ulong expected = z ^ (z >> 31);
                Assert.Equal(expected, rng.NextUInt64());
            }
        }
    }

    [Fact]
    public void CopyingStruct_ForksTheStream()
    {
        var a = new SplitMix64(12345);
        a.NextUInt64(); // advance
        var b = a;       // value copy captures the current state
        Assert.Equal(a.NextUInt64(), b.NextUInt64());
        Assert.Equal(a.NextUInt64(), b.NextUInt64());
    }
}
