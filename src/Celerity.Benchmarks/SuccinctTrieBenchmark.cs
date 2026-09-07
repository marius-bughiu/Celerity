using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// SuccinctTrie<int> vs the BCL's Dictionary<string, int>, on the same key shape TrieBenchmark uses so the two
// tries can be read side by side. The standing against the dictionary is the trie standing: it loses the
// exact-key operations (a walk of the key versus one hash) and wins PrefixMatch outright, because a
// dictionary has no prefix index and must scan every entry and run StartsWith on it.
//
// What this type sells over Trie<TValue> is retained footprint, and that is deliberately *not* benchmarked
// here: [MemoryDiagnoser] reports bytes allocated during the run, and the succinct build allocates a sort
// buffer and several transient lists it then drops — so the allocation column reads roughly level with the
// pointer-based trie while the structure that survives is ~38x smaller. Measuring the wrong quantity
// confidently is worse than not charting it, so the footprint figures live in docs/api/collections.md, where
// they are stated as retained bytes, and IndexSizeInBytes is pinned by the test suite instead.
//
// The Trie_Cross* arms are the honest other half of the trade: what the succinct encoding costs in query
// time against the pointer-based trie. They carry op names of their own (CrossLookup, CrossPrefixMatch) so
// each lands in a dashboard bucket by itself — two Celerity arms sharing an op would overwrite each other in
// the index the cards are built from — while the shared [BenchmarkCategory] keeps the BenchmarkDotNet report
// grouping all three against one baseline.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SuccinctTrieBenchmark
{
    private const int PrefixBuckets = 16;

    private string[] keys = null!;
    private string[] prefixes = null!;
    private string[] selectivePrefixes = null!;
    private KeyValuePair<string, int>[] entries = null!;

    // The same keys laid end to end in one buffer, with the slice bounds of each — the shape a tokenizer
    // actually holds its keys in, and the input to the SpanLookup category below.
    private char[] buffer = null!;
    private (int Offset, int Length)[] slices = null!;

    // Rebuilt per iteration by the [IterationSetup]s below for the Build category.
    private Dictionary<string, int> dict = null!;

    // Full, warm subjects for the non-destructive categories, built once in [GlobalSetup].
    private SuccinctTrie<int> succinctFull = null!;
    private Trie<int> trieFull = null!;
    private Dictionary<string, int> dictFull = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        prefixes = new string[PrefixBuckets];
        for (int b = 0; b < PrefixBuckets; b++)
            prefixes[b] = $"{(char)('a' + b / 26)}{(char)('a' + b % 26)}";

        keys = new string[ItemCount];
        entries = new KeyValuePair<string, int>[ItemCount];
        slices = new (int, int)[ItemCount];
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = $"{prefixes[i % PrefixBuckets]}_{i:D8}";
            entries[i] = new KeyValuePair<string, int>(keys[i], i);
            slices[i] = (text.Length, keys[i].Length);
            text.Append(keys[i]).Append(' ');
        }

        buffer = text.ToString().ToCharArray();

        // Sixteen prefixes long enough to match a handful of keys each, spread across the key space. This is
        // the shape the prefix operations are actually reached for — a user has typed most of a token and
        // wants the few completions — and it is the one where the asymptotics separate: the trie's cost is
        // the prefix plus its matches, while the dictionary still has to look at every key it holds.
        selectivePrefixes = new string[PrefixBuckets];
        for (int b = 0; b < PrefixBuckets; b++)
        {
            string key = keys[b * (ItemCount / PrefixBuckets)];
            selectivePrefixes[b] = key[..(key.Length - 1)];
        }

        succinctFull = new SuccinctTrie<int>(entries);
        trieFull = new Trie<int>();
        dictFull = new Dictionary<string, int>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
        {
            trieFull[keys[i]] = i;
            dictFull[keys[i]] = i;
        }
    }

    // ---- Build: turn the whole key set into a queryable index ----------------------------------------

    // Pre-size the baseline to ItemCount so the category measures per-insert cost, not the dictionary's
    // resize/rehash growth strategy (matching the other Add benchmarks in this repo). The succinct arm has no
    // per-insert path at all — the whole set goes to the constructor — which is what the category compares.
    [IterationSetup(Target = nameof(Dictionary_Build))]
    public void ResetDictForBuild() => dict = new Dictionary<string, int>(ItemCount);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public void Dictionary_Build()
    {
        for (int i = 0; i < keys.Length; i++)
            dict[keys[i]] = i;
    }

    [Benchmark]
    [BenchmarkCategory("Build")]
    public SuccinctTrie<int> SuccinctTrie_Build() => new(entries);

    // ---- Lookup: exact-key hit for every key (the dictionary's home turf) -----------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public long Dictionary_Lookup()
    {
        long acc = 0;
        foreach (string k in keys)
            acc += dictFull[k];
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public long SuccinctTrie_Lookup()
    {
        long acc = 0;
        foreach (string k in keys)
            acc += succinctFull[k];
        return acc;
    }

    // What the succinct encoding costs against the pointer-based trie on the same walk: a reference follow
    // per character becomes two selects over the LOUDS vector plus a binary search over the label slice.
    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public long Trie_CrossLookup()
    {
        long acc = 0;
        foreach (string k in keys)
            acc += trieFull[k];
        return acc;
    }

    // ---- PrefixMatch: sum the values of every entry under each prefix bucket (the trie's win) ---------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PrefixMatch")]
    public long Dictionary_PrefixMatch()
    {
        long acc = 0;
        foreach (string prefix in prefixes)
        {
            foreach (KeyValuePair<string, int> pair in dictFull)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                    acc += pair.Value;
            }
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("PrefixMatch")]
    public long SuccinctTrie_PrefixMatch()
    {
        long acc = 0;
        foreach (string prefix in prefixes)
        {
            foreach (KeyValuePair<string, int> pair in succinctFull.GetByPrefix(prefix))
                acc += pair.Value;
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("PrefixMatch")]
    public long Trie_CrossPrefixMatch()
    {
        long acc = 0;
        foreach (string prefix in prefixes)
        {
            foreach (KeyValuePair<string, int> pair in trieFull.GetByPrefix(prefix))
                acc += pair.Value;
        }
        return acc;
    }

    // ---- PrefixProbe: the few completions of a nearly-complete token (the asymptotic win) -------------
    // PrefixMatch above returns a sixteenth of the table per prefix, so the dictionary's scan finds a match
    // every sixteen entries and the trie is charged for materializing 100,000 result strings — a shape in
    // which neither trie separates from it. This arm is the same operation with the selectivity a prefix
    // index is reached for: the answer is a handful of keys, the trie descends to them directly, and the
    // dictionary still reads every entry it holds.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PrefixProbe")]
    public long Dictionary_PrefixProbe()
    {
        long acc = 0;
        foreach (string prefix in selectivePrefixes)
        {
            foreach (KeyValuePair<string, int> pair in dictFull)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                    acc += pair.Value;
            }
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("PrefixProbe")]
    public long SuccinctTrie_PrefixProbe()
    {
        long acc = 0;
        foreach (string prefix in selectivePrefixes)
        {
            foreach (KeyValuePair<string, int> pair in succinctFull.GetByPrefix(prefix))
                acc += pair.Value;
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("PrefixProbe")]
    public long Trie_CrossPrefixProbe()
    {
        long acc = 0;
        foreach (string prefix in selectivePrefixes)
        {
            foreach (KeyValuePair<string, int> pair in trieFull.GetByPrefix(prefix))
                acc += pair.Value;
        }
        return acc;
    }

    // ---- SpanLookup: the caller holds spans, not strings ----------------------------------------------
    // The baseline is what a net8.0 caller must do to probe any string-keyed collection: allocate the string
    // first. The trie descends the span directly, so the whole allocation and copy vanish — which is why this
    // category is the one with allocation numbers worth reading. (.NET 9's
    // Dictionary<string, V>.GetAlternateLookup closes this gap on that runtime; this project targets net8.0,
    // the floor where the BCL has no answer.)

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SpanLookup")]
    public long Dictionary_SpanLookup()
    {
        long acc = 0;
        for (int i = 0; i < slices.Length; i++)
        {
            (int offset, int length) = slices[i];
            acc += dictFull[new string(buffer, offset, length)];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("SpanLookup")]
    public long SuccinctTrie_SpanLookup()
    {
        long acc = 0;
        for (int i = 0; i < slices.Length; i++)
        {
            (int offset, int length) = slices[i];
            succinctFull.TryGetValue(buffer.AsSpan(offset, length), out int value);
            acc += value;
        }
        return acc;
    }
}
