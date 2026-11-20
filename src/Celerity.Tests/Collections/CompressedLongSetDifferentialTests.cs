using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="CompressedLongSet"/> against a
/// <see cref="HashSet{T}"/> oracle. CsCheck generates the value domain (its origin and width) and a
/// seed, and two sets are then driven through a long interleaved script of add / remove / range-add
/// / optimize / clear and the full set-algebra surface, reconciling after every step.
///
/// <para>
/// The domain is what makes this worth running: a narrow one forces every value into a single chunk
/// and drives it through the array &#8594; bitmap &#8594; run transitions repeatedly, and a
/// generated band count copies that domain into up to four bands spaced either three chunks apart
/// or exactly 2^32 apart — values sharing their low 32 bits, which only the upper half of the 48-bit
/// key tells apart — so the chunk-index merge and skip paths do real work. The origins include <see cref="long.MinValue"/>, the top of the range, both 32-bit boundaries and a
/// point past 2^40, so the sign-flipped 48-bit chunk key is exercised at both ends and across the
/// <see cref="int"/> range's edges. The container state machine is the whole risk surface of this
/// type, and a script that reaches a divergence shrinks to a minimal reproduction with the seed
/// printed.
/// </para>
/// </summary>
public class CompressedLongSetDifferentialTests
{
    // Origins chosen so the generated domain straddles a chunk boundary, sits inside one chunk,
    // crosses zero, crosses either 32-bit boundary, or butts against either end of the 64-bit range.
    private static readonly long[] Origins =
    {
        0,
        -50,
        65_500,
        -70_000,
        (1L << 40) - 10_000,
        (long)int.MaxValue - 10_000,
        (long)int.MinValue - 10_000,
        long.MinValue,
        long.MaxValue - 100_000,
    };

    // The two band spacings: three chunks (neighbouring index entries with a gap between them) and
    // 2^32 (identical low 32 bits, distinct chunk keys).
    private static readonly long[] BandStrides = { 3L * 65_536, 1L << 32 };

    private static readonly Gen<(int OriginIndex, int Width, int Bands, int StrideIndex, uint Seed)> GenScript =
        Gen.Select(Gen.Int[0, Origins.Length - 1], Gen.Int[4, 30_000], Gen.Int[1, 4], Gen.Int[0, 1], Gen.UInt);

    [Fact]
    public void EveryOperation_ShouldMatchTheHashSetOracle()
    {
        GenScript.Sample(spec =>
        {
            long origin = Origins[spec.OriginIndex];
            var rng = new Random((int)spec.Seed);

            // Bands step away from whichever end of the range the origin is near, so none overflows.
            long bandStep = origin >= 0 ? -BandStrides[spec.StrideIndex] : BandStrides[spec.StrideIndex];
            long Value(Random random) => origin + (random.Next(spec.Bands) * bandStep) + random.Next(spec.Width);

            var sut = new CompressedLongSet();
            var oracle = new HashSet<long>();
            var otherSut = new CompressedLongSet();
            var otherOracle = new HashSet<long>();

            for (int step = 0; step < 200; step++)
            {
                switch (rng.Next(14))
                {
                    case 0:
                    case 1:
                    case 2:
                    {
                        long value = Value(rng);
                        Assert.Equal(oracle.Add(value), sut.TryAdd(value));
                        break;
                    }

                    case 3:
                    {
                        long value = Value(rng);
                        Assert.Equal(oracle.Remove(value), sut.Remove(value));
                        break;
                    }

                    case 4:
                    {
                        // A range-add is the only way a run container is produced without Optimize.
                        long lo = Value(rng);
                        long hi = lo + rng.Next(0, Math.Min(spec.Width, 400));
                        long added = sut.AddRange(lo, hi);
                        long expected = 0;
                        for (long v = lo; v <= hi; v++)
                        {
                            if (oracle.Add(v))
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
                        long value = Value(rng);
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
                        long value = Value(rng);
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
            long origin = Origins[spec.Item1];
            var rng = new Random((int)spec.Item2);

            var sut = new CompressedLongSet();
            var oracle = new HashSet<long>();

            for (int i = 0; i < 12; i++)
            {
                // Clamped against the top of the range: one origin sits 100,000 below long.MaxValue,
                // and an unchecked add past it would wrap to long.MinValue.
                long lo = origin + rng.Next(0, origin > long.MaxValue - 200_000 ? 100_000 : 200_000);
                long hi = lo > long.MaxValue - 40_000 ? long.MaxValue : lo + rng.Next(0, 40_000);
                sut.AddRange(lo, hi);
                for (long v = lo; v <= hi && v >= lo; v++)
                    oracle.Add(v);

                if (rng.Next(3) == 0)
                    sut.Optimize();

                Assert.Equal(oracle.Count, sut.Count);
            }

            sut.Optimize();
            Assert.Equal(oracle.OrderBy(v => v), sut);
        }, iter: 40);
    }
}
