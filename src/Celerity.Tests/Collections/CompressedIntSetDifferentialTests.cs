using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="CompressedIntSet"/> against a
/// <see cref="HashSet{T}"/> oracle. CsCheck generates the value domain (its origin and width) and a
/// seed, and two sets are then driven through a long interleaved script of add / remove / range-add
/// / optimize / clear and the full set-algebra surface, reconciling after every step.
///
/// <para>
/// The domain is what makes this worth running: a narrow one forces every value into a single chunk
/// and drives it through the array &#8594; bitmap &#8594; run transitions repeatedly, a wide one
/// spreads the values across hundreds of chunks so the chunk-index merge does the work, and the
/// origins include <see cref="int.MinValue"/> and the top of the range so the sign-flipped chunk key
/// is exercised at both ends. The container state machine is the whole risk surface of this type,
/// and a script that reaches a divergence shrinks to a minimal reproduction with the seed printed.
/// </para>
/// </summary>
public class CompressedIntSetDifferentialTests
{
    // Origins chosen so the generated domain straddles a chunk boundary, sits inside one chunk,
    // crosses zero, or butts against either end of the 32-bit range.
    private static readonly int[] Origins =
    {
        0,
        -50,
        65_500,
        -70_000,
        int.MinValue,
        int.MaxValue - 100_000,
    };

    private static readonly Gen<(int OriginIndex, int Width, uint Seed)> GenScript =
        Gen.Select(Gen.Int[0, Origins.Length - 1], Gen.Int[4, 30_000], Gen.UInt);

    [Fact]
    public void EveryOperation_ShouldMatchTheHashSetOracle()
    {
        GenScript.Sample(spec =>
        {
            int origin = Origins[spec.OriginIndex];
            var rng = new Random((int)spec.Seed);

            var sut = new CompressedIntSet();
            var oracle = new HashSet<int>();
            var otherSut = new CompressedIntSet();
            var otherOracle = new HashSet<int>();

            for (int step = 0; step < 200; step++)
            {
                switch (rng.Next(14))
                {
                    case 0:
                    case 1:
                    case 2:
                    {
                        int value = Value(rng, origin, spec.Width);
                        Assert.Equal(oracle.Add(value), sut.TryAdd(value));
                        break;
                    }

                    case 3:
                    {
                        int value = Value(rng, origin, spec.Width);
                        Assert.Equal(oracle.Remove(value), sut.Remove(value));
                        break;
                    }

                    case 4:
                    {
                        // A range-add is the only way a run container is produced without Optimize.
                        int lo = Value(rng, origin, spec.Width);
                        int hi = lo + rng.Next(0, Math.Min(spec.Width, 400));
                        long added = sut.AddRange(lo, hi);
                        long expected = 0;
                        for (long v = lo; v <= hi; v++)
                        {
                            if (oracle.Add((int)v))
                                expected++;
                        }

                        Assert.Equal(expected, added);
                        break;
                    }

                    case 5:
                        sut.Optimize();
                        break;

                    case 6:
                    {
                        int value = Value(rng, origin, spec.Width);
                        Assert.Equal(otherOracle.Add(value), otherSut.TryAdd(value));
                        break;
                    }

                    case 7:
                        sut.UnionWith(otherSut);
                        oracle.UnionWith(otherOracle);
                        break;

                    case 8:
                        sut.IntersectWith(otherSut);
                        oracle.IntersectWith(otherOracle);
                        break;

                    case 9:
                        sut.ExceptWith(otherSut);
                        oracle.ExceptWith(otherOracle);
                        break;

                    case 10:
                        sut.SymmetricExceptWith(otherSut);
                        oracle.SymmetricExceptWith(otherOracle);
                        break;

                    case 11:
                        Assert.Equal(oracle.Overlaps(otherOracle), sut.Overlaps(otherSut));
                        Assert.Equal(oracle.SetEquals(otherOracle), sut.SetEquals(otherSut));
                        Assert.Equal(oracle.IsSubsetOf(otherOracle), sut.IsSubsetOf(otherSut));
                        Assert.Equal(oracle.IsSupersetOf(otherOracle), sut.IsSupersetOf(otherSut));
                        Assert.Equal(oracle.IsProperSubsetOf(otherOracle), sut.IsProperSubsetOf(otherSut));
                        Assert.Equal(oracle.IsProperSupersetOf(otherOracle), sut.IsProperSupersetOf(otherSut));
                        Assert.Equal((long)oracle.Count(otherOracle.Contains), sut.IntersectCount(otherSut));
                        break;

                    case 12:
                        otherSut.Clear();
                        otherOracle.Clear();
                        break;

                    default:
                    {
                        int value = Value(rng, origin, spec.Width);
                        Assert.Equal(oracle.Contains(value), sut.Contains(value));
                        break;
                    }
                }

                Assert.Equal(oracle.Count, sut.Count);
                Assert.Equal((long)oracle.Count, sut.Cardinality);
            }

            // Enumeration must reproduce the oracle exactly, and in ascending signed order.
            Assert.Equal(oracle.OrderBy(v => v), sut);

            // And the same must hold after a final re-encode, which changes representation only.
            sut.Optimize();
            Assert.Equal(oracle.OrderBy(v => v), sut);
            Assert.Equal(oracle.Count, sut.Count);
        }, iter: 50);
    }

    [Fact]
    public void RangeAddAndOptimize_ShouldPreserveContents_ForOverlappingRanges()
    {
        Gen.Select(Gen.Int[0, Origins.Length - 1], Gen.UInt).Sample(spec =>
        {
            int origin = Origins[spec.Item1];
            var rng = new Random((int)spec.Item2);

            var sut = new CompressedIntSet();
            var oracle = new HashSet<int>();

            for (int i = 0; i < 12; i++)
            {
                // Clamped in long arithmetic: the origins include both ends of the 32-bit range.
                int lo = (int)Math.Clamp((long)origin + rng.Next(0, 200_000), int.MinValue, int.MaxValue);
                int hi = (int)Math.Clamp((long)lo + rng.Next(0, 40_000), int.MinValue, int.MaxValue);
                sut.AddRange(lo, hi);
                for (long v = lo; v <= hi; v++)
                    oracle.Add((int)v);

                if (rng.Next(3) == 0)
                    sut.Optimize();

                Assert.Equal(oracle.Count, sut.Count);
            }

            sut.Optimize();
            Assert.Equal(oracle.OrderBy(v => v), sut);
        }, iter: 40);
    }

    private static int Value(Random rng, int origin, int width) => origin + rng.Next(width);
}
