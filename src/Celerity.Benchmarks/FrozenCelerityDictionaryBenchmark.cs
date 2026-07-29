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

    // The same keys laid end to end in one buffer, with the slice bounds of each — the shape a parser or
    // router actually holds its keys in, and the input to the SpanLookup category below.
    private char[] buffer = null!;
    private (int Offset, int Length)[] slices = null!;

    private FrozenDictionary<string, int> frozenDictionary = null!;
    private FrozenCelerityDictionary<int> frozenCelerity = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new string[ItemCount];
        pairs = new KeyValuePair<string, int>[ItemCount];
        slices = new (int, int)[ItemCount];
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < ItemCount; i++)
        {
            // Identifier-shaped, guaranteed-distinct keys.
            keys[i] = "celerity/key/" + i + "/" + (i * 2654435761u);
            pairs[i] = new KeyValuePair<string, int>(keys[i], i);
            slices[i] = (text.Length, keys[i].Length);
            text.Append(keys[i]).Append(' ');
        }

        buffer = text.ToString().ToCharArray();

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

    // ── SpanLookup (the caller holds spans, not strings) ──────────────────────
    // The baseline is what a net8.0 caller must do to probe any string-keyed collection: allocate the
    // string first. The Celerity arm probes the span directly, so the whole allocation and copy vanish —
    // which is the point, and why this category is the one with allocation numbers worth reading.
    // (.NET 9's Dictionary<string,V>.GetAlternateLookup closes this gap on that runtime; this project
    // targets net8.0, the floor where the BCL has no answer.)

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SpanLookup")]
    public int FrozenDictionary_SpanLookup()
    {
        int acc = 0;
        for (int i = 0; i < slices.Length; i++)
        {
            (int offset, int length) = slices[i];
            acc += frozenDictionary[new string(buffer, offset, length)];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("SpanLookup")]
    public int FrozenCelerityDictionary_SpanLookup()
    {
        int acc = 0;
        for (int i = 0; i < slices.Length; i++)
        {
            (int offset, int length) = slices[i];
            frozenCelerity.TryGetValue(buffer.AsSpan(offset, length), out int value);
            acc += value;
        }
        return acc;
    }
}
