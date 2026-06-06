using System.Collections.Frozen;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class FrozenCelerityDictionaryBenchmark
{
    private string[] keys = null!;
    private KeyValuePair<string, int>[] pairs = null!;

    private FrozenDictionary<string, int> frozenDictionary = null!;
    private FrozenCelerityDictionary<int> frozenCelerity = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new string[ItemCount];
        pairs = new KeyValuePair<string, int>[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            // Identifier-shaped, guaranteed-distinct keys.
            keys[i] = "celerity/key/" + i + "/" + (i * 2654435761u);
            pairs[i] = new KeyValuePair<string, int>(keys[i], i);
        }

        frozenDictionary = pairs.ToFrozenDictionary(p => p.Key, p => p.Value);
        frozenCelerity = new FrozenCelerityDictionary<int>(pairs);
    }

    // ── Build (construct the frozen structure from the same source pairs) ──────
    // Baseline is the BCL FrozenDictionary<,>, the read-optimized build-once
    // counterpart — the fair analogue for a frozen, perfect-hashed dictionary
    // (a mutable Dictionary<,> does no build-time hashing optimization, so it is
    // not a like-for-like baseline here).

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public FrozenDictionary<string, int> FrozenDictionary_Build()
        => pairs.ToFrozenDictionary(p => p.Key, p => p.Value);

    [Benchmark]
    [BenchmarkCategory("Build")]
    public FrozenCelerityDictionary<int> FrozenCelerityDictionary_Build()
        => new FrozenCelerityDictionary<int>(pairs);

    // ── Lookup (every key, on the prebuilt instances) ─────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int FrozenDictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
            acc += frozenDictionary[key];
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int FrozenCelerityDictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
            acc += frozenCelerity[key];
        return acc;
    }
}
