using System;
using System.Linq;
using Celerity.Primitives;
using CsCheck;

namespace Celerity.Tests.Utils;

/// <summary>
/// Property-based differential coverage for <see cref="SortedSpan"/> against a
/// <see cref="HashSet{T}"/> oracle. CsCheck generates the value domain, both side lengths and a seed;
/// two sorted samples are drawn from that shape and every operation is reconciled against the set
/// algebra <see cref="HashSet{T}"/> computes for the same values.
///
/// <para>
/// The generated shape is what makes this worth running. A narrow domain against a long span forces
/// heavy duplication, which is what exercises the duplicate-collapsing paths; independent side lengths
/// mean the ratio between them swings across the 32x threshold at which the implementation switches
/// from the linear merge to galloping, and every case is run in both argument orders so each galloping
/// direction is reached. A divergence shrinks to a minimal reproduction with the seed printed.
/// </para>
/// </summary>
public class SortedSpanDifferentialTests
{
    private static readonly Gen<(int Domain, int LeftCount, int RightCount, uint Seed)> GenShape =
        Gen.Select(Gen.Int[2, 500], Gen.Int[0, 300], Gen.Int[0, 300], Gen.UInt);

    [Fact]
    public void EveryOperation_ShouldMatchTheHashSetOracle()
    {
        GenShape.Sample(shape =>
        {
            var rand = new Random((int)shape.Seed);
            int[] left = SortedSample(rand, shape.LeftCount, shape.Domain);
            int[] right = SortedSample(rand, shape.RightCount, shape.Domain);

            AssertMatchesOracle(left, right);
            AssertMatchesOracle(right, left);
        });
    }

    private static void AssertMatchesOracle(int[] a, int[] b)
    {
        var setA = new HashSet<int>(a);
        var setB = new HashSet<int>(b);

        int[] expectedIntersect = setA.Intersect(setB).OrderBy(x => x).ToArray();
        int[] expectedUnion = setA.Union(setB).OrderBy(x => x).ToArray();
        int[] expectedExcept = setA.Except(setB).OrderBy(x => x).ToArray();

        // One buffer, sized by the loosest of the three upper bounds, reused across the operations —
        // a stale tail from an earlier call must never show up in a later result.
        var buffer = new int[a.Length + b.Length];

        Assert.Equal(expectedIntersect, buffer.AsSpan(0, SortedSpan.Intersect<int>(a, b, buffer)).ToArray());
        Assert.Equal(expectedUnion, buffer.AsSpan(0, SortedSpan.Union<int>(a, b, buffer)).ToArray());
        Assert.Equal(expectedExcept, buffer.AsSpan(0, SortedSpan.Except<int>(a, b, buffer)).ToArray());
        Assert.Equal(expectedIntersect.Length, SortedSpan.IntersectCount<int>(a, b));
        Assert.Equal(expectedIntersect.Length != 0, SortedSpan.Overlaps<int>(a, b));
    }

    private static int[] SortedSample(Random rand, int count, int domain)
    {
        var values = new int[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = rand.Next(0, domain);
        }

        Array.Sort(values);
        return values;
    }
}
