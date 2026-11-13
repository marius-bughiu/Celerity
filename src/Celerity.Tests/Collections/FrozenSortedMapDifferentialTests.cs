using System.Numerics;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="FrozenSortedMap{TKey, TValue}"/> against the sorted,
/// parallel key and value arrays it replaces. CsCheck generates the input's axes (length, how widely the keys
/// spread and where the cluster starts) through <see cref="FrozenSortedSetOracle.RandomKeys{T}"/>, the set suite's
/// generator. The keys are deduplicated, since a map rejects a repeat, and each is given a value derived from
/// its position in the unsorted source. Any disagreement then shrinks to the smallest map that still fails,
/// with the seed printed.
///
/// <para>
/// What is on trial beyond the set's search is the pairing. The keys are sorted with their values alongside, so
/// a value that falls out of step with its key shows up as a confident, wrong lookup, never a crash. Every query
/// therefore checks the value as well as the key, at every key and at its neighbours on either side.
/// </para>
/// </summary>
public class FrozenSortedMapDifferentialTests
{
    // Length reaches past 272 keys, where an int map grows its second internal level, and a long map its third
    // past 648. SpreadBits from 0 (every key the same, so the map collapses to one entry) to the full width.
    private static readonly Gen<(int Length, int SpreadBits, ulong Origin, uint Seed)> GenEntries =
        Gen.Select(Gen.Int[0, 1200], Gen.Int[0, 128], Gen.ULong, Gen.UInt);

    [Fact]
    public void EveryQuery_ShouldMatchTheSortedArrayOracle_WhenTheKeysAreInt()
    {
        GenEntries.Sample(spec => AssertMatches(
            FrozenSortedSetOracle.RandomKeys<int>(spec.Length, spec.SpreadBits, spec.Origin, 0, spec.Seed)),
            iter: 100);
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheSortedArrayOracle_WhenTheKeysAreLong()
    {
        GenEntries.Sample(spec => AssertMatches(
            FrozenSortedSetOracle.RandomKeys<long>(spec.Length, spec.SpreadBits, spec.Origin, 0, spec.Seed)),
            iter: 100);
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheSortedArrayOracle_WhenTheKeysAreByte()
    {
        // Byte keys collide constantly, so dedupe leaves at most 256 entries over the widest (64-key) node.
        GenEntries.Sample(spec => AssertMatches(
            FrozenSortedSetOracle.RandomKeys<byte>(spec.Length, spec.SpreadBits, spec.Origin, 0, spec.Seed)),
            iter: 100);
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheSortedArrayOracle_WhenTheKeysAreInt128()
    {
        // Int128 has no hardware vector compare, so this drives the scalar node scan, through several levels at
        // four keys a node.
        GenEntries.Sample(spec => AssertMatches(
            FrozenSortedSetOracle.RandomKeys<Int128>(spec.Length, spec.SpreadBits, spec.Origin, 0, spec.Seed)),
            iter: 100);
    }

    /// <summary>
    /// Builds a map from the distinct keys of <paramref name="rawKeys"/>, each mapped to its source position as a
    /// string, through both constructor paths, and checks every query against the sorted parallel arrays.
    /// </summary>
    private static void AssertMatches<T>(T[] rawKeys)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        KeyValuePair<T, string>[] source = rawKeys.Distinct().Select((k, i) => new KeyValuePair<T, string>(k, "v" + i)).ToArray();
        KeyValuePair<T, string>[] oracle = source.OrderBy(static p => p.Key).ToArray();
        T[] keys = oracle.Select(static p => p.Key).ToArray();

        AssertMatches(new FrozenSortedMap<T, string>(source), oracle, keys);
        AssertMatches(new FrozenSortedMap<T, string>(source.Select(static p => p)), oracle, keys);
    }

    private static void AssertMatches<T>(FrozenSortedMap<T, string> map, KeyValuePair<T, string>[] oracle, T[] keys)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        int n = oracle.Length;
        Assert.Equal(n, map.Count);
        Assert.Equal(oracle, map.ToArray());
        Assert.True(map.Keys.AsSpan().SequenceEqual(keys));
        for (int i = 0; i < n; i++)
        {
            Assert.Equal(oracle[i].Key, map.GetKeyAtIndex(i));
            Assert.Equal(oracle[i].Value, map.GetValueAtIndex(i));
        }

        if (n != 0)
        {
            Assert.Equal(oracle[0], map.Min);
            Assert.Equal(oracle[^1], map.Max);
        }

        var probes = new List<T> { T.MinValue, T.MaxValue, T.Zero, T.One };
        foreach (T key in keys)
        {
            probes.Add(key);
            if (key != T.MinValue)
                probes.Add(key - T.One);
            if (key != T.MaxValue)
                probes.Add(key + T.One);
        }

        foreach (T probe in probes)
        {
            int lower = FrozenSortedSetOracle.LowerBound(keys, probe);
            bool present = lower < n && keys[lower] == probe;
            int above = probe == T.MaxValue ? n : FrozenSortedSetOracle.LowerBound(keys, probe + T.One);

            Assert.Equal(present, map.TryGetValue(probe, out string? value));
            Assert.Equal(present ? oracle[lower].Value : null, value);
            Assert.Equal(present, map.ContainsKey(probe));
            Assert.Equal(present ? lower : -1, map.IndexOfKey(probe));
            Assert.Equal(lower, map.CountLessThan(probe));

            AssertEntryAt(oracle, lower, map.TryGetLowerBound(probe, out KeyValuePair<T, string> ceiling), ceiling);
            AssertEntryAt(oracle, above, map.TryGetUpperBound(probe, out KeyValuePair<T, string> upper), upper);
            AssertEntryAt(oracle, above - 1, map.TryGetFloor(probe, out KeyValuePair<T, string> floor), floor);
            AssertEntryAt(oracle, lower - 1, map.TryGetPredecessor(probe, out KeyValuePair<T, string> predecessor), predecessor);

            T from = probe == T.MinValue ? probe : probe - T.One;
            Range range = map.GetIndexRange(from, probe);
            Assert.Equal(FrozenSortedSetOracle.LowerBound(keys, from), range.Start.Value);
            Assert.Equal(lower, range.End.Value);
        }
    }

    // A search that ran off either end must report "not found" with a default entry.
    private static void AssertEntryAt<T>(KeyValuePair<T, string>[] oracle, int index, bool found, KeyValuePair<T, string> entry)
    {
        bool inRange = index >= 0 && index < oracle.Length;
        Assert.Equal(inRange, found);
        Assert.Equal(inRange ? oracle[index] : default, entry);
    }
}
