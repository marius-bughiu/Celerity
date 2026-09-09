using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="PersistentHashMap{TKey, TValue, THasher}"/> against a
/// <see cref="Dictionary{TKey, TValue}"/> oracle driven through the same operation sequence.
///
/// <para>
/// <b>Persistence is the thing on trial, not the last answer.</b> A map that path-copies one node too few
/// still agrees with the oracle on the map you are holding — the damage lands on a map handed out
/// <i>earlier</i>, whose storage the newer one was supposed to share read-only and instead wrote through. So
/// every run keeps <b>snapshots</b>: a map and an independent copy of the oracle taken at intervals, all
/// re-checked entry by entry after the last operation.
/// </para>
///
/// <para>
/// <b>The hasher is an axis, because it decides which shapes the trie ever takes.</b> Under
/// <see cref="Int32IdentityHasher"/> the keys spread over the trie and full-hash collisions never happen, so
/// the collision-node paths are dead. Under a hasher that keeps only the low bits, every key set of any size
/// forces collision nodes, deep single-child chains, and the dissolve-back-up rule that collapses them. Each
/// generated run is therefore replayed through both, and the oracle does not care which.
/// </para>
///
/// <para>
/// <b>The builder is replayed against the same run.</b> Its transient path reaches the same node operations
/// by a different route — writing in place where the persistent path copies — so a run that agrees
/// persistently and disagrees through a builder is exactly the ownership bug the token exists to prevent.
/// </para>
/// </summary>
public class PersistentHashMapDifferentialTests
{
    // Keeps only the low six bits, so a key space of a few hundred guarantees several keys per hash and
    // therefore collision nodes, chains of single-child nodes above them, and the dissolve rule on the way
    // back out.
    private struct LowBitsHasher : IHashProvider<int>
    {
        public int Hash(int key) => key & 63;
    }

    // Op count, key space, removal weight (percent of ops that remove) and a seed. The key space is what
    // decides collision density under LowBitsHasher and trie depth under the identity hasher.
    private static readonly Gen<(int Ops, int KeySpace, int RemoveWeight, uint Seed)> GenRuns =
        Gen.Select(Gen.Int[0, 1_200], Gen.Int[1, 900], Gen.Int[0, 55], Gen.UInt);

    [Fact]
    public void EveryOperation_ShouldMatchTheDictionaryOracle_UnderGeneratedRuns()
    {
        GenRuns.Sample(
            run =>
            {
                AssertAgreesWithOracle<Int32IdentityHasher>(run.Ops, run.KeySpace, run.RemoveWeight, run.Seed);
                AssertAgreesWithOracle<LowBitsHasher>(run.Ops, run.KeySpace, run.RemoveWeight, run.Seed);
            },
            iter: 120);
    }

    [Fact]
    public void ABuilderShouldMatchTheDictionaryOracle_UnderGeneratedRuns()
    {
        GenRuns.Sample(
            run =>
            {
                AssertBuilderAgreesWithOracle<Int32IdentityHasher>(run.Ops, run.KeySpace, run.RemoveWeight, run.Seed);
                AssertBuilderAgreesWithOracle<LowBitsHasher>(run.Ops, run.KeySpace, run.RemoveWeight, run.Seed);
            },
            iter: 120);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(1_024)]
    [InlineData(1_025)]
    [InlineData(5_000)]
    public void FillThenDrain_ShouldMatchTheOracle_AtEveryKeyCount(int count)
    {
        AssertFillThenDrain<Int32IdentityHasher>(count);
        AssertFillThenDrain<LowBitsHasher>(count);
    }

    // A run of Add / SetItem / Remove against a Dictionary oracle, with snapshots re-checked at the end.
    private static void AssertAgreesWithOracle<THasher>(int ops, int keySpace, int removeWeight, uint seed)
        where THasher : struct, IHashProvider<int>
    {
        var rng = new Random((int)seed);
        PersistentHashMap<int, string, THasher> map = PersistentHashMap<int, string, THasher>.Empty;
        var oracle = new Dictionary<int, string>();
        var snapshots = new List<(PersistentHashMap<int, string, THasher> Map, Dictionary<int, string> Oracle)>();

        for (int step = 0; step < ops; step++)
        {
            int key = rng.Next(keySpace);
            string value = $"v{step}";

            if (rng.Next(100) < removeWeight)
            {
                PersistentHashMap<int, string, THasher> before = map;
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

            if (step % 23 == 0)
                snapshots.Add((map, new Dictionary<int, string>(oracle)));
        }

        snapshots.Add((map, new Dictionary<int, string>(oracle)));

        foreach ((PersistentHashMap<int, string, THasher> snapshot, Dictionary<int, string> expected) in snapshots)
            AssertSameEntries(expected, snapshot);
    }

    // The same run driven through a builder, snapshotting with ToImmutable so the transient path's isolation
    // is on trial alongside its arithmetic.
    private static void AssertBuilderAgreesWithOracle<THasher>(int ops, int keySpace, int removeWeight, uint seed)
        where THasher : struct, IHashProvider<int>
    {
        var rng = new Random((int)seed);
        PersistentHashMap<int, string, THasher>.Builder builder = new();
        var oracle = new Dictionary<int, string>();
        var snapshots = new List<(PersistentHashMap<int, string, THasher> Map, Dictionary<int, string> Oracle)>();

        for (int step = 0; step < ops; step++)
        {
            int key = rng.Next(keySpace);
            string value = $"v{step}";

            if (rng.Next(100) < removeWeight)
            {
                Assert.Equal(oracle.Remove(key), builder.Remove(key));
            }
            else
            {
                builder[key] = value;
                oracle[key] = value;
            }

            Assert.Equal(oracle.Count, builder.Count);

            if (step % 23 == 0)
                snapshots.Add((builder.ToImmutable(), new Dictionary<int, string>(oracle)));
        }

        snapshots.Add((builder.ToImmutable(), new Dictionary<int, string>(oracle)));

        foreach ((PersistentHashMap<int, string, THasher> snapshot, Dictionary<int, string> expected) in snapshots)
            AssertSameEntries(expected, snapshot);
    }

    private static void AssertFillThenDrain<THasher>(int count)
        where THasher : struct, IHashProvider<int>
    {
        PersistentHashMap<int, string, THasher> map = PersistentHashMap<int, string, THasher>.Empty;
        var snapshots = new List<(PersistentHashMap<int, string, THasher> Map, int Present)>();

        for (int key = 0; key < count; key++)
        {
            map = map.Add(key, key.ToString());
            if (key % 97 == 0)
                snapshots.Add((map, key + 1));
        }

        Assert.Equal(count, map.Count);

        // Draining takes every removal path backwards: dissolving collision nodes, inlining single-entry
        // children, and finally emptying the root.
        PersistentHashMap<int, string, THasher> draining = map;
        for (int key = count - 1; key >= 0; key--)
        {
            draining = draining.Remove(key);
            Assert.Equal(key, draining.Count);
        }

        Assert.Same(PersistentHashMap<int, string, THasher>.Empty, draining);

        // The fill snapshots must be untouched by the drain that walked through their storage.
        foreach ((PersistentHashMap<int, string, THasher> snapshot, int present) in snapshots)
        {
            Assert.Equal(present, snapshot.Count);
            for (int key = 0; key < present; key++)
                Assert.Equal(key.ToString(), snapshot[key]);
        }
    }

    private static void AssertSameEntries<THasher>(
        Dictionary<int, string> expected, PersistentHashMap<int, string, THasher> actual)
        where THasher : struct, IHashProvider<int>
    {
        Assert.Equal(expected.Count, actual.Count);

        // The trie's canonical form is checked alongside its contents, because the collapse rule is the one
        // documented invariant a behavioural assertion cannot see: a map that never dissolved a single-entry
        // node would still answer every lookup and enumerate every entry.
        Assert.Equal(expected.Count, PersistentHashMapShape.AssertCanonical(actual));

        foreach (KeyValuePair<int, string> entry in expected)
        {
            Assert.True(actual.TryGetValue(entry.Key, out string? value));
            Assert.Equal(entry.Value, value);
        }

        // And nothing extra: enumeration is the only view that can expose an entry no lookup asks for.
        var enumerated = actual.ToDictionary(entry => entry.Key, entry => entry.Value);
        Assert.Equal(expected.Count, enumerated.Count);
        foreach (KeyValuePair<int, string> entry in expected)
            Assert.Equal(entry.Value, enumerated[entry.Key]);
    }
}
