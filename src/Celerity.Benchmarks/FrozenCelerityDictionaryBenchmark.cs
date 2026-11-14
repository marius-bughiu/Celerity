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

    // frozenDictionary's span-keyed view for the SpanLookup category, resolved once as a caller holding it would.
    private FrozenDictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> frozenSpanLookup;

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
        frozenSpanLookup = frozenDictionary.GetAlternateLookup<ReadOnlySpan<char>>();
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
    // Both sides probe with a slice of the buffer and allocate nothing: FrozenDictionary through
    // GetAlternateLookup<ReadOnlySpan<char>>, which every runtime Celerity targets has, and the Celerity arm
    // through its own span overload. So this is the Lookup comparison again for a caller holding spans.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SpanLookup")]
    public int FrozenDictionary_SpanLookup()
    {
        int acc = 0;
        for (int i = 0; i < slices.Length; i++)
        {
            (int offset, int length) = slices[i];
            frozenSpanLookup.TryGetValue(buffer.AsSpan(offset, length), out int value);
            acc += value;
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
