using System.Collections.Frozen;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// MinimalPerfectHash<string, StringXxHash3Hasher> vs FrozenDictionary<string, int> as a key → slot map.
// FrozenDictionary is the BCL's read-optimized, build-once answer to "give me the slot for this key", and the
// stronger of the two BCL baselines: it tunes its string hashing to the key set at build time, so it is faster
// to probe than Dictionary<string, int>. Both sides map the same key set onto [0, n).
//
// What this class cannot show is the reason to pick the function: retained memory. [MemoryDiagnoser] reports
// bytes *allocated* by an arm, which for Build includes construction scratch both sides throw away. The
// retained size is the function's BitsPerKey (about 3.7 bits, under half a byte, per key) against roughly
// 60 bytes per key for the frozen dictionary, measured with a forced full GC around construction.
//
// Lookup probes every key through 16 independent shuffles per invocation. A single replayed order of 1,000
// keys is short enough for the branch predictor to learn which level each key settles in, which would flatter
// the function's level walk.
//
// VerifiedLookup is the honest comparison for a caller that can be asked about keys outside the set: the
// function cannot tell a non-member from a member, so the caller stores the key at its slot and compares. The
// frozen dictionary verifies as part of every lookup already, so its arm is the same TryGetValue.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class MinimalPerfectHashBenchmark
{
    private const int Shuffles = 16;

    private string[] keys = null!;
    private string[] probes = null!;
    private KeyValuePair<string, int>[] pairs = null!;
    private string[] keysBySlot = null!;

    private FrozenDictionary<string, int> frozenDictionary = null!;
    private MinimalPerfectHash<string, StringXxHash3Hasher> mph = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);
        keys = new string[ItemCount];
        pairs = new KeyValuePair<string, int>[ItemCount];
        var distinct = new HashSet<string>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
        {
            string key;
            do
            {
                // URL-shaped, ~40 characters, guaranteed distinct.
                key = "https://example.com/item/" + rand.NextInt64().ToString("x");
            }
            while (!distinct.Add(key));

            keys[i] = key;
            pairs[i] = new KeyValuePair<string, int>(key, i);
        }

        probes = new string[ItemCount * Shuffles];
        for (int s = 0; s < Shuffles; s++)
        {
            string[] order = (string[])keys.Clone();
            rand.Shuffle(order);
            order.CopyTo(probes, s * ItemCount);
        }

        frozenDictionary = pairs.ToFrozenDictionary(p => p.Key, p => p.Value);
        mph = new MinimalPerfectHash<string, StringXxHash3Hasher>(keys);

        keysBySlot = new string[mph.Count];
        foreach (string key in keys)
            keysBySlot[mph.GetIndex(key)] = key;
    }

    // ── Build (the same key set mapped onto [0, n)) ──────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public FrozenDictionary<string, int> FrozenDictionary_Build()
        => pairs.ToFrozenDictionary(p => p.Key, p => p.Value);

    [Benchmark]
    [BenchmarkCategory("Build")]
    public MinimalPerfectHash<string, StringXxHash3Hasher> MinimalPerfectHash_Build()
        => new(keys);

    // ── Lookup (member keys only: the slot for each) ─────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public long FrozenDictionary_Lookup()
    {
        long acc = 0;
        foreach (string key in probes)
            acc += frozenDictionary[key];
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public long MinimalPerfectHash_Lookup()
    {
        long acc = 0;
        foreach (string key in probes)
            acc += mph.GetIndex(key);
        return acc;
    }

    // ── VerifiedLookup (a lookup that can also reject a non-member) ──────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("VerifiedLookup")]
    public long FrozenDictionary_VerifiedLookup()
    {
        long acc = 0;
        foreach (string key in probes)
        {
            if (frozenDictionary.TryGetValue(key, out int slot))
                acc += slot;
        }

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("VerifiedLookup")]
    public long MinimalPerfectHash_VerifiedLookup()
    {
        long acc = 0;
        string[] stored = keysBySlot;
        foreach (string key in probes)
        {
            int slot = mph.GetIndex(key);
            if ((uint)slot < (uint)stored.Length && string.Equals(stored[slot], key))
                acc += slot;
        }

        return acc;
    }
}
