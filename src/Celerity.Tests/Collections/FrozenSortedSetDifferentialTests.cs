using System.Numerics;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="FrozenSortedSet{T}"/> against the sorted, distinct array
/// it replaces: a positional read is an array load and every successor-shaped query is a binary search for the
/// first key at or above the probe. CsCheck generates the input's axes — length, how widely the keys spread,
/// where the cluster starts and how often a key repeats — so any disagreement shrinks to the smallest set that
/// still fails, with the seed printed.
///
/// <para>
/// The layout is what is on trial. The internal levels are computed from leaf positions rather than inserted,
/// so a wrong separator index or a level that is one node short does not fail loudly: it sends a few probes one
/// subtree to the left, and they report a confident, wrong rank. That shows up only when the tree has several
/// levels and the probe lands exactly on, or just beside, a separator, which is what the generator aims at.
/// <see cref="int"/> and <see cref="long"/> take the SIMD node search; <see cref="Int128"/> and <see cref="char"/>
/// take the scalar one, and <see cref="byte"/> is the widest node (64 keys) over the narrowest domain.
/// </para>
/// </summary>
public class FrozenSortedSetDifferentialTests
{
    // Length reaches past 272 keys, where an int set grows its second internal level (depth 3); a long set
    // reaches depth 4 past 648 keys and an Int128 one depth 5 past 500. The deeper int thresholds are pinned
    // in FrozenSortedSetTests, which keeps this suite affordable under coverage instrumentation. SpreadBits runs from 0 (every key the same, so dedupe collapses the set to one) to
    // the full width, and the origin moves the cluster anywhere in the domain, including across the
    // MaxValue / MinValue wrap. RepeatPercent re-draws some keys from those already chosen.
    private static readonly Gen<(int Length, int SpreadBits, ulong Origin, int RepeatPercent, uint Seed)> GenKeys =
        Gen.Select(Gen.Int[0, 1500], Gen.Int[0, 128], Gen.ULong, Gen.Int[0, 60], Gen.UInt);

    [Fact]
    public void EveryQuery_ShouldMatchTheArrayOracle_WhenTheKeysAreInt()
    {
        GenKeys.Sample(spec => FrozenSortedSetOracle.AssertMatches(
            FrozenSortedSetOracle.RandomKeys<int>(spec.Length, spec.SpreadBits, spec.Origin, spec.RepeatPercent, spec.Seed)),
            iter: 100);
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheArrayOracle_WhenTheKeysAreLong()
    {
        GenKeys.Sample(spec => FrozenSortedSetOracle.AssertMatches(
            FrozenSortedSetOracle.RandomKeys<long>(spec.Length, spec.SpreadBits, spec.Origin, spec.RepeatPercent, spec.Seed)),
            iter: 100);
    }

    // The scalar key types run fewer samples: their node scan is a loop, which coverage instrumentation slows
    // by an order of magnitude more than the SIMD compare.
    [Fact]
    public void EveryQuery_ShouldMatchTheArrayOracle_WhenTheKeysAreInt128()
    {
        GenKeys.Sample(spec => FrozenSortedSetOracle.AssertMatches(
            FrozenSortedSetOracle.RandomKeys<Int128>(spec.Length, spec.SpreadBits, spec.Origin, spec.RepeatPercent, spec.Seed)),
            iter: 60);
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheArrayOracle_WhenTheKeysAreChar()
    {
        GenKeys.Sample(spec => FrozenSortedSetOracle.AssertMatches(
            FrozenSortedSetOracle.RandomKeys<char>(spec.Length, spec.SpreadBits, spec.Origin, spec.RepeatPercent, spec.Seed)),
            iter: 40);
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheArrayOracle_WhenTheKeysAreByte()
    {
        GenKeys.Sample(spec => FrozenSortedSetOracle.AssertMatches(
            FrozenSortedSetOracle.RandomKeys<byte>(spec.Length, spec.SpreadBits, spec.Origin, spec.RepeatPercent, spec.Seed)),
            iter: 100);
    }

    [Fact]
    public void SetAlgebra_ShouldMatchHashSet_OnGeneratedPairs()
    {
        // Two sets drawn around the same origin overlap partially; the narrow spread makes subset and equality
        // cases common rather than vanishingly rare.
        Gen.Select(Gen.Int[0, 400], Gen.Int[0, 400], Gen.Int[0, 10], Gen.UInt).Sample(spec =>
        {
            int[] left = FrozenSortedSetOracle.RandomKeys<int>(spec.Item1, spec.Item3, 0, 0, spec.Item4);
            int[] right = FrozenSortedSetOracle.RandomKeys<int>(spec.Item2, spec.Item3, 0, 0, spec.Item4 ^ 0x9E3779B9);
            FrozenSortedSetSetAlgebraTests.AssertAlgebraMatchesHashSet(left, right);
        }, iter: 300);
    }
}

/// <summary>
/// The oracle shared by the <see cref="FrozenSortedSet{T}"/> suites: builds the set and checks every query it
/// answers against a sorted, distinct copy of the same keys.
/// </summary>
internal static class FrozenSortedSetOracle
{
    /// <summary>
    /// Builds a <see cref="FrozenSortedSet{T}"/> from <paramref name="source"/> through the counted path, and again
    /// through the uncounted one, and checks both against the sorted distinct oracle.
    /// </summary>
    /// <typeparam name="T">The key type.</typeparam>
    /// <param name="source">The keys, in any order and possibly repeated.</param>
    /// <returns>The set built from the counted path.</returns>
    public static FrozenSortedSet<T> AssertMatches<T>(T[] source)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        T[] oracle = source.Distinct().Order().ToArray();

        var counted = new FrozenSortedSet<T>(source);
        var uncounted = new FrozenSortedSet<T>(source.Select(static x => x));

        AssertMatches(counted, oracle);
        AssertMatches(uncounted, oracle);
        Assert.Equal(counted.MemoryUsageInBytes, uncounted.MemoryUsageInBytes);
        return counted;
    }

    /// <summary>Checks every query <paramref name="set"/> answers against <paramref name="oracle"/>.</summary>
    /// <typeparam name="T">The key type.</typeparam>
    /// <param name="set">The set under test.</param>
    /// <param name="oracle">The expected keys, ascending and distinct.</param>
    public static void AssertMatches<T>(FrozenSortedSet<T> set, T[] oracle)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        Assert.Equal(oracle.Length, set.Count);
        Assert.Equal(ExpectedDepth(oracle.Length, FrozenSortedSet<T>.NodeSize), set.Depth);
        Assert.True(set.AsSpan().SequenceEqual(oracle));
        for (int i = 0; i < oracle.Length; i++)
            Assert.Equal(oracle[i], set[i]);

        var enumerated = new List<T>(oracle.Length);
        foreach (T key in set)
            enumerated.Add(key);
        Assert.Equal(oracle, enumerated);

        if (oracle.Length != 0)
        {
            Assert.Equal(oracle[0], set.Min);
            Assert.Equal(oracle[^1], set.Max);
        }

        // Every key and its neighbours either side — where an off-by-one in a separator shows — plus both ends
        // of the domain and zero.
        var probes = new List<T> { T.MinValue, T.MaxValue, T.Zero, T.One };
        foreach (T key in oracle)
        {
            probes.Add(key);
            if (key != T.MinValue)
                probes.Add(key - T.One);
            if (key != T.MaxValue)
                probes.Add(key + T.One);
        }

        foreach (T probe in probes)
        {
            int expected = LowerBound(oracle, probe);
            bool present = expected < oracle.Length && oracle[expected] == probe;

            Assert.Equal(expected, set.CountLessThan(probe));
            Assert.Equal(present, set.Contains(probe));
            Assert.Equal(present ? expected : -1, set.IndexOf(probe));

            Assert.Equal(expected < oracle.Length, set.TryGetLowerBound(probe, out T lower));
            Assert.Equal(expected < oracle.Length ? oracle[expected] : default, lower);

            int above = probe == T.MaxValue ? oracle.Length : LowerBound(oracle, probe + T.One);
            Assert.Equal(above < oracle.Length, set.TryGetUpperBound(probe, out T upper));
            Assert.Equal(above < oracle.Length ? oracle[above] : default, upper);

            // A range from the previous probe-shaped point to this one, in whichever order makes it valid.
            T from = probe == T.MinValue ? probe : probe - T.One;
            T to = probe;
            Assert.True(set.SliceRange(from, to).SequenceEqual(
                oracle.AsSpan(LowerBound(oracle, from), LowerBound(oracle, to) - LowerBound(oracle, from))));
        }

        if (oracle.Length != 0)
        {
            T low = oracle[0];
            T high = oracle[^1];
            Assert.True(set.SliceRange(low, high).SequenceEqual(oracle.AsSpan(0, oracle.Length - 1)));
            Assert.True(set.SliceRange(T.MinValue, T.MaxValue).SequenceEqual(
                oracle.AsSpan(0, LowerBound(oracle, T.MaxValue))));
        }
    }

    /// <summary>
    /// <paramref name="length"/> keys clustered at <paramref name="origin"/>, each a random offset of up to
    /// <paramref name="spreadBits"/> bits added with wrap-around, with about <paramref name="repeatPercent"/> of
    /// them re-drawn from the keys already chosen.
    /// </summary>
    /// <typeparam name="T">The key type.</typeparam>
    /// <param name="length">How many keys to draw, repeats included.</param>
    /// <param name="spreadBits">The offset width; values past the type's width cover the whole domain.</param>
    /// <param name="origin">Where the cluster starts, truncated to the key type.</param>
    /// <param name="repeatPercent">The rough share of keys that repeat an earlier one.</param>
    /// <param name="seed">The fill seed.</param>
    /// <returns>The keys, unsorted.</returns>
    public static T[] RandomKeys<T>(int length, int spreadBits, ulong origin, int repeatPercent, uint seed)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        var rand = new Random(unchecked((int)seed));
        UInt128 mask = spreadBits >= 128 ? UInt128.MaxValue : (UInt128.One << spreadBits) - UInt128.One;
        T start = T.CreateTruncating(origin);
        var keys = new T[length];
        for (int i = 0; i < length; i++)
        {
            if (i > 0 && rand.Next(100) < repeatPercent)
            {
                keys[i] = keys[rand.Next(i)];
                continue;
            }

            var offset = new UInt128((ulong)rand.NextInt64(long.MinValue, long.MaxValue), (ulong)rand.NextInt64(long.MinValue, long.MaxValue));
            keys[i] = start + T.CreateTruncating(offset & mask);
        }

        return keys;
    }

    /// <summary>The depth a tree of <paramref name="count"/> keys and nodes of <paramref name="nodeSize"/> has.</summary>
    /// <param name="count">The distinct key count.</param>
    /// <param name="nodeSize">Keys per node.</param>
    /// <returns>Nodes read per search, root to leaf.</returns>
    public static int ExpectedDepth(int count, int nodeSize)
    {
        int nodes = Math.Max(1, (count + nodeSize - 1) / nodeSize);
        int depth = 1;
        while (nodes > 1)
        {
            nodes = (nodes + nodeSize) / (nodeSize + 1);
            depth++;
        }

        return depth;
    }

    /// <summary>The first position in <paramref name="sorted"/> whose key is not below <paramref name="probe"/>.</summary>
    /// <typeparam name="T">The key type.</typeparam>
    /// <param name="sorted">Ascending keys.</param>
    /// <param name="probe">The bound.</param>
    /// <returns>A position in <c>[0, sorted.Length]</c>.</returns>
    public static int LowerBound<T>(T[] sorted, T probe)
        where T : IBinaryInteger<T>
    {
        int lo = 0;
        int hi = sorted.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (sorted[mid] < probe)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }
}
