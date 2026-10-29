using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/> against a
/// <see cref="SortedDictionary{TKey, TValue}"/> oracle driven through the same operation sequence.
///
/// <para>
/// <b>Persistence is the thing on trial, not the last answer.</b> A map that path-copies one node too few — or
/// rebalances a removal by writing into a sibling it was supposed to copy — still agrees with the oracle on the
/// map you are holding; the damage lands on a map handed out <i>earlier</i>. So every run keeps
/// <b>snapshots</b>: a map and an independent copy of the oracle taken at intervals, all re-checked after the
/// last operation, entry by entry, by every ordered query, and by the tree's shape.
/// </para>
///
/// <para>
/// <b>The comparer is an axis</b>, because the descent, the bound queries and the separators all follow it:
/// a run is replayed under ascending and descending order, and the oracle is given the same order.
/// </para>
///
/// <para>
/// <b>The builder is replayed against the same run.</b> Its transient path reaches the same node operations by
/// a different route — editing in place where the persistent path copies, and shifting inside arrays a
/// persistent edit reallocates — so a run that agrees persistently and disagrees through a builder is exactly
/// the ownership bug the token exists to prevent.
/// </para>
/// </summary>
public class PersistentSortedMapDifferentialTests
{
    internal readonly struct Descending : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }

    private sealed class DescendingOracle : IComparer<int>
    {
        public static readonly DescendingOracle Instance = new();

        public int Compare(int x, int y) => y.CompareTo(x);
    }

    // Op count, key space, removal weight (percent of ops that remove) and a seed. The key space decides how
    // deep the tree gets — a few thousand keys is three levels — and the removal weight how often a removal
    // underflows a node and has to borrow or merge.
    private static readonly Gen<(int Ops, int KeySpace, int RemoveWeight, uint Seed)> GenRuns =
        Gen.Select(Gen.Int[0, 3_000], Gen.Int[1, 4_000], Gen.Int[0, 60], Gen.UInt);

    [Fact]
    public void EveryOperation_ShouldMatchTheSortedDictionaryOracle_UnderGeneratedRuns()
    {
        GenRuns.Sample(
            run =>
            {
                AssertAgreesWithOracle<DefaultComparer<int>>(run, Comparer<int>.Default);
                AssertAgreesWithOracle<Descending>(run, DescendingOracle.Instance);
            },
            iter: 60);
    }

    [Fact]
    public void ABuilderShouldMatchTheSortedDictionaryOracle_UnderGeneratedRuns()
    {
        GenRuns.Sample(
            run =>
            {
                AssertBuilderAgreesWithOracle<DefaultComparer<int>>(run, Comparer<int>.Default);
                AssertBuilderAgreesWithOracle<Descending>(run, DescendingOracle.Instance);
            },
            iter: 60);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(528)]
    [InlineData(529)]
    [InlineData(1_024)]
    [InlineData(1_025)]
    [InlineData(20_000)]
    public void FillThenDrain_ShouldMatchTheOracle_AtEveryKeyCount(int count)
    {
        AssertFillThenDrain(count, ascendingFill: true, frontDrain: true);
        AssertFillThenDrain(count, ascendingFill: false, frontDrain: false);
        AssertFillThenDrain(count, ascendingFill: true, frontDrain: false);
    }

    // A run of Add / SetItem / Remove against a SortedDictionary oracle, with snapshots re-checked at the end.
    private static void AssertAgreesWithOracle<TComparer>(
        (int Ops, int KeySpace, int RemoveWeight, uint Seed) run, IComparer<int> order)
        where TComparer : struct, IComparer<int>
    {
        var rng = new Random((int)run.Seed);
        PersistentSortedMap<int, string, TComparer> map = PersistentSortedMap<int, string, TComparer>.Empty;
        var oracle = new SortedDictionary<int, string>(order);
        var snapshots = new List<(PersistentSortedMap<int, string, TComparer> Map, SortedDictionary<int, string> Oracle)>();

        for (int step = 0; step < run.Ops; step++)
        {
            int key = rng.Next(run.KeySpace);
            string value = $"v{step}";

            if (rng.Next(100) < run.RemoveWeight)
            {
                PersistentSortedMap<int, string, TComparer> before = map;
                map = map.Remove(key);

                if (!oracle.Remove(key))
                    Assert.Same(before, map);
            }
            else if (!oracle.ContainsKey(key))
            {
                // Add and SetItem must agree on an absent key; alternating exercises both entry points.
                map = (step & 1) == 0 ? map.Add(key, value) : map.SetItem(key, value);
                oracle[key] = value;
            }
            else
            {
                map = map.SetItem(key, value);
                oracle[key] = value;
            }

            Assert.Equal(oracle.Count, map.Count);

            if (step % 97 == 0)
                snapshots.Add((map, new SortedDictionary<int, string>(oracle, order)));
        }

        snapshots.Add((map, new SortedDictionary<int, string>(oracle, order)));

        foreach ((PersistentSortedMap<int, string, TComparer> snapshot, SortedDictionary<int, string> expected) in snapshots)
            AssertSameEntries(expected, snapshot, run.KeySpace, rng);
    }

    // The same run driven through a builder, snapshotting with ToImmutable so the transient path's isolation
    // is on trial alongside its arithmetic.
    private static void AssertBuilderAgreesWithOracle<TComparer>(
        (int Ops, int KeySpace, int RemoveWeight, uint Seed) run, IComparer<int> order)
        where TComparer : struct, IComparer<int>
    {
        var rng = new Random((int)run.Seed);
        PersistentSortedMap<int, string, TComparer>.Builder builder = new();
        var oracle = new SortedDictionary<int, string>(order);
        var snapshots = new List<(PersistentSortedMap<int, string, TComparer> Map, SortedDictionary<int, string> Oracle)>();

        for (int step = 0; step < run.Ops; step++)
        {
            int key = rng.Next(run.KeySpace);
            string value = $"v{step}";

            if (rng.Next(100) < run.RemoveWeight)
            {
                Assert.Equal(oracle.Remove(key), builder.Remove(key));
            }
            else
            {
                builder[key] = value;
                oracle[key] = value;
            }

            Assert.Equal(oracle.Count, builder.Count);

            if (step % 97 == 0)
                snapshots.Add((builder.ToImmutable(), new SortedDictionary<int, string>(oracle, order)));
        }

        snapshots.Add((builder.ToImmutable(), new SortedDictionary<int, string>(oracle, order)));

        foreach ((PersistentSortedMap<int, string, TComparer> snapshot, SortedDictionary<int, string> expected) in snapshots)
            AssertSameEntries(expected, snapshot, run.KeySpace, rng);
    }

    // Ascending fill then front drain hits the leftmost path's splits and merges; descending fill then back
    // drain the rightmost; ascending fill then back drain leaves the left neighbour to borrow from.
    private static void AssertFillThenDrain(int count, bool ascendingFill, bool frontDrain)
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map =
            PersistentSortedMap<int, string, DefaultComparer<int>>.Empty;
        var snapshots = new List<(PersistentSortedMap<int, string, DefaultComparer<int>> Map, int Present)>();

        for (int i = 0; i < count; i++)
        {
            int key = ascendingFill ? i : count - 1 - i;
            map = map.Add(key, key.ToString());
            if (i % 97 == 0)
                snapshots.Add((map, i + 1));
        }

        Assert.Equal(count, map.Count);
        Assert.Equal(count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(map));

        PersistentSortedMap<int, string, DefaultComparer<int>> draining = map;
        for (int i = 0; i < count; i++)
        {
            int key = frontDrain ? i : count - 1 - i;
            draining = draining.Remove(key);
            Assert.Equal(count - 1 - i, draining.Count);

            if (i % 211 == 0)
                Assert.Equal(draining.Count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(draining));
        }

        Assert.Same(PersistentSortedMap<int, string, DefaultComparer<int>>.Empty, draining);

        // The fill snapshots must be untouched by the drain that walked through their storage.
        foreach ((PersistentSortedMap<int, string, DefaultComparer<int>> snapshot, int present) in snapshots)
        {
            Assert.Equal(present, snapshot.Count);
            int first = ascendingFill ? 0 : count - present;
            for (int key = first; key < first + present; key++)
                Assert.Equal(key.ToString(), snapshot[key]);

            Assert.Equal(present, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(snapshot));
        }
    }

    private static void AssertSameEntries<TComparer>(
        SortedDictionary<int, string> expected,
        PersistentSortedMap<int, string, TComparer> actual,
        int keySpace,
        Random rng)
        where TComparer : struct, IComparer<int>
    {
        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(expected.Count, PersistentSortedMapShape.AssertValid<int, TComparer>(actual));

        // Enumeration is checked in order, which is the claim a sorted map makes beyond membership.
        Assert.Equal(expected.Select(e => (e.Key, (string?)e.Value)), actual.Select(e => (e.Key, e.Value)));

        foreach (KeyValuePair<int, string> entry in expected)
        {
            Assert.True(actual.TryGetValue(entry.Key, out string? value));
            Assert.Equal(entry.Value, value);
        }

        if (expected.Count == 0)
        {
            Assert.False(actual.TryGetMin(out _));
            Assert.False(actual.TryGetMax(out _));
        }
        else
        {
            Assert.Equal(expected.First().Key, actual.Min.Key);
            Assert.Equal(expected.Last().Key, actual.Max.Key);
        }

        // The bound queries and a range at probes spread over (and just past) the key space, present or not.
        int[] sorted = expected.Keys.ToArray();
        IComparer<int> order = expected.Comparer;
        for (int probe = 0; probe < 24; probe++)
        {
            int key = rng.Next(-1, keySpace + 1);
            AssertBound(sorted, order, key, strict: false, actual.TryGetLowerBound(key, out KeyValuePair<int, string?> lower), lower);
            AssertBound(sorted, order, key, strict: true, actual.TryGetUpperBound(key, out KeyValuePair<int, string?> upper), upper);
            AssertBackwardBound(sorted, order, key, strict: false, actual.TryGetFloor(key, out KeyValuePair<int, string?> floor), floor);
            AssertBackwardBound(sorted, order, key, strict: true, actual.TryGetPredecessor(key, out KeyValuePair<int, string?> predecessor), predecessor);

            int other = rng.Next(-1, keySpace + 1);
            (int from, int to) = order.Compare(key, other) <= 0 ? (key, other) : (other, key);
            int[] inRange = sorted.Where(k => order.Compare(k, from) >= 0 && order.Compare(k, to) < 0).ToArray();
            Assert.Equal(inRange, actual.EnumerateRange(from, to).Select(e => e.Key));
        }
    }

    private static void AssertBackwardBound(
        int[] sorted, IComparer<int> order, int key, bool strict, bool found, KeyValuePair<int, string?> entry)
    {
        int index = Array.FindLastIndex(sorted, k => strict ? order.Compare(k, key) < 0 : order.Compare(k, key) <= 0);
        Assert.Equal(index >= 0, found);
        if (index >= 0)
            Assert.Equal(sorted[index], entry.Key);
    }

    private static void AssertBound(
        int[] sorted, IComparer<int> order, int key, bool strict, bool found, KeyValuePair<int, string?> entry)
    {
        int index = Array.FindIndex(sorted, k => strict ? order.Compare(k, key) > 0 : order.Compare(k, key) >= 0);
        Assert.Equal(index >= 0, found);
        if (index >= 0)
            Assert.Equal(sorted[index], entry.Key);
    }
}
