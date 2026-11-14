using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// Trie<int> vs the BCL's Dictionary<string, int>. The dictionary wins the exact-key operations (one hash
// versus a character walk), so Add / Lookup / SpanLookup are the honest "you don't use a trie for these"
// arms. The headline is PrefixMatch: enumerating every entry whose key starts with a prefix. The trie answers
// it from the structure in O(prefix + matches); the dictionary has no prefix index and must scan every key
// and run StartsWith on it, so the trie's win widens with the table size. Keys share 16 two-letter prefix
// buckets so each prefix query returns ItemCount/16 matches. The baseline arms are named Dictionary_* so the
// dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class TrieBenchmark
{
    private const int PrefixBuckets = 16;

    private string[] keys = null!;
    private string[] prefixes = null!;

    // The same keys laid end to end in one buffer, with the slice bounds of each — the shape a tokenizer
    // actually holds its keys in, and the input to the SpanLookup category below.
    private char[] buffer = null!;
    private (int Offset, int Length)[] slices = null!;

    // Rebuilt per iteration by the [IterationSetup]s below for the build (Add) category.
    private Trie<int> trie = null!;
    private Dictionary<string, int> dict = null!;

    // Full, warm subjects for the non-destructive Lookup / PrefixMatch categories, built once in [GlobalSetup].
    private Trie<int> trieFull = null!;
    private Dictionary<string, int> dictFull = null!;

    // dictFull's span-keyed view for the SpanLookup category, resolved once as a caller holding it would.
    private Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> dictSpanLookup;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        prefixes = new string[PrefixBuckets];
        for (int b = 0; b < PrefixBuckets; b++)
            prefixes[b] = $"{(char)('a' + b / 26)}{(char)('a' + b % 26)}";

        keys = new string[ItemCount];
        slices = new (int, int)[ItemCount];
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = $"{prefixes[i % PrefixBuckets]}_{i:D8}";
            slices[i] = (text.Length, keys[i].Length);
            text.Append(keys[i]).Append(' ');
        }

        buffer = text.ToString().ToCharArray();

        trieFull = new Trie<int>();
        dictFull = new Dictionary<string, int>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
        {
            trieFull[keys[i]] = i;
            dictFull[keys[i]] = i;
        }

        dictSpanLookup = dictFull.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    // ---- Add: build the table from empty --------------------------------------------------------------

    // Pre-size the baseline to ItemCount so the Add category measures per-insert cost, not the dictionary's
    // resize/rehash growth strategy (matching the other Add benchmarks in this repo).
    [IterationSetup(Target = nameof(Dictionary_Add))]
    public void ResetDictForAdd() => dict = new Dictionary<string, int>(ItemCount);

    [IterationSetup(Target = nameof(Trie_Add))]
    public void ResetTrieForAdd() => trie = new Trie<int>();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void Dictionary_Add()
    {
        for (int i = 0; i < keys.Length; i++)
            dict[keys[i]] = i;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void Trie_Add()
    {
        for (int i = 0; i < keys.Length; i++)
            trie[keys[i]] = i;
    }

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
    public long Trie_Lookup()
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
    public long Trie_PrefixMatch()
    {
        long acc = 0;
        foreach (string prefix in prefixes)
        {
            foreach (KeyValuePair<string, int> pair in trieFull.GetByPrefix(prefix))
                acc += pair.Value;
        }
        return acc;
    }

    // ---- SpanLookup: the caller holds spans, not strings ----------------------------------------------
    // Both sides probe with a slice of the buffer and allocate nothing: the trie descends the span, and the
    // dictionary hashes it through GetAlternateLookup<ReadOnlySpan<char>>, which every runtime Celerity
    // targets has. So this is the Lookup comparison again for a caller holding spans: the trie's character
    // walk against one hash of the same characters.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SpanLookup")]
    public long Dictionary_SpanLookup()
    {
        long acc = 0;
        for (int i = 0; i < slices.Length; i++)
        {
            (int offset, int length) = slices[i];
            dictSpanLookup.TryGetValue(buffer.AsSpan(offset, length), out int value);
            acc += value;
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("SpanLookup")]
    public long Trie_SpanLookup()
    {
        long acc = 0;
        for (int i = 0; i < slices.Length; i++)
        {
            (int offset, int length) = slices[i];
            trieFull.TryGetValue(buffer.AsSpan(offset, length), out int value);
            acc += value;
        }
        return acc;
    }
}
