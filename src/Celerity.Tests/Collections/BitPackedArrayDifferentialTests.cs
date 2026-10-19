using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="BitPackedArray"/> against the <c>ulong[]</c> it packs: every
/// write lands in both, and after each one the packed array must read back exactly what the plain array holds.
/// CsCheck generates the run's axes — the width, the length, how many operations run, and the seed that fills
/// them in — so any disagreement shrinks to a minimal width and length with the seed printed.
/// </summary>
public class BitPackedArrayDifferentialTests
{
    // Every width from 1 to 64 is reachable: the widths that divide 64 never straddle a word, the others straddle
    // at a period set by gcd(width, 64), and 64 is the case where the two-step shifts are the only thing standing
    // between a read and a shift by 64. Lengths reach well past a few words at every width.
    private static readonly Gen<(int Bits, int Length, int Operations, uint Seed)> GenRun =
        Gen.Select(Gen.Int[1, 64], Gen.Int[0, 300], Gen.Int[0, 400], Gen.UInt);

    [Fact]
    public void EveryOperation_ShouldMatchTheArrayOracle()
    {
        GenRun.Sample(spec =>
        {
            var rand = new Random(unchecked((int)spec.Seed));
            var packed = new BitPackedArray(spec.Length, spec.Bits);
            var model = new ulong[spec.Length];
            ulong max = packed.MaxValue;

            for (int op = 0; op < spec.Operations; op++)
            {
                switch (rand.Next(5))
                {
                    case 0 when spec.Length > 0:
                    {
                        int index = rand.Next(spec.Length);
                        ulong value = NextValue(rand, max);
                        packed[index] = value;
                        model[index] = value;
                        break;
                    }
                    case 1:
                    {
                        int start = rand.Next(spec.Length + 1);
                        ulong[] run = new ulong[rand.Next(spec.Length - start + 1)];
                        for (int i = 0; i < run.Length; i++)
                            run[i] = NextValue(rand, max);
                        packed.SetRange(start, run);
                        run.CopyTo(model, start);
                        break;
                    }
                    case 2 when rand.Next(20) == 0:
                    {
                        ulong value = NextValue(rand, max);
                        packed.Fill(value);
                        Array.Fill(model, value);
                        break;
                    }
                    case 3 when rand.Next(40) == 0:
                        packed.Clear();
                        Array.Clear(model);
                        break;
                    default:
                    {
                        // A rejected write must leave every element as it was.
                        if (spec.Bits < 64 && spec.Length > 0)
                        {
                            int index = rand.Next(spec.Length);
                            Assert.Throws<ArgumentOutOfRangeException>(() => packed[index] = max + 1);
                        }
                        break;
                    }
                }

                AssertMatches(model, packed, rand);
            }

            Assert.Equal(model, packed.ToArray());
        }, iter: 300);
    }

    [Fact]
    public void Construction_ShouldRoundTripTheSource()
    {
        GenRun.Sample(spec =>
        {
            var rand = new Random(unchecked((int)spec.Seed));
            ulong max = spec.Bits == 64 ? ulong.MaxValue : (1UL << spec.Bits) - 1;
            var values = new ulong[spec.Length];
            for (int i = 0; i < values.Length; i++)
                values[i] = NextValue(rand, max);

            var packed = new BitPackedArray(values, spec.Bits);

            Assert.Equal(values, packed.ToArray());
            Assert.Equal(((((long)spec.Length * spec.Bits) + 63) / 64 + 1) * 8, packed.MemoryUsageInBytes);
        }, iter: 300);
    }

    private static void AssertMatches(ulong[] model, BitPackedArray packed, Random rand)
    {
        Assert.Equal(model.Length, packed.Count);
        for (int i = 0; i < model.Length; i++)
            Assert.Equal(model[i], packed[i]);

        // A bulk read from a random window has to agree with the element reads it replaces.
        int start = rand.Next(model.Length + 1);
        var window = new ulong[rand.Next(model.Length - start + 1)];
        packed.GetRange(start, window);
        Assert.Equal(model.AsSpan(start, window.Length).ToArray(), window);
    }

    // Values skewed towards the edges of the range, where a lost or stray high bit shows: all ones, a lone top
    // bit, zero, and uniformly random bits.
    private static ulong NextValue(Random rand, ulong max) => rand.Next(4) switch
    {
        0 => max,
        1 => (max >> 1) + 1,
        2 => 0,
        _ => (ulong)rand.NextInt64() * 2 + (ulong)rand.Next(2) & max,
    };
}
