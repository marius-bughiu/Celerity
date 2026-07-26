using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// The reference-type-key probe path, which the rest of the tracked suite does not cover: every other
/// dictionary / set benchmark here keys on <c>int</c> or <c>long</c>.
/// </summary>
/// <remarks>
/// <para>
/// A value-type key lets the JIT devirtualize and inline <c>EqualityComparer&lt;T&gt;.Default</c>, so the
/// probe body is straight-line code. A reference-type key does not: the collection JITs as a
/// <c>__Canon</c>-shared body and each comparison is a real interface dispatch. That makes the string-keyed
/// tables the ones worth tracking for probe-path codegen work.
/// </para>
/// <para>
/// <c>LookupMissing</c> is the probe-heavy arm on purpose: a hit stops at the matching slot, while a miss
/// walks the whole cluster to the first vacant slot, so it pays the empty-slot test once per iteration.
/// Both instances are built at the library default load factor — the shipping configuration, not a
/// contrived one.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class StringKeyProbeBenchmark
{
    private string[] keys = null!;
    private string[] missingKeys = null!;

    private Dictionary<string, int> dictionary = null!;
    private CelerityDictionary<string, int, StringXxHash3Hasher> celerityDictionary = null!;

    private HashSet<string> hashSet = null!;
    private CeleritySet<string, StringXxHash3Hasher> celeritySet = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new string[ItemCount];
        missingKeys = new string[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            // Identifier-shaped, guaranteed-distinct keys, matching the shape the
            // frozen-collection benchmarks use.
            keys[i] = "celerity/key/" + i + "/" + (i * 2654435761u);
            missingKeys[i] = "celerity/absent/" + i + "/" + (i * 2246822519u);
        }

        dictionary = new Dictionary<string, int>(ItemCount);
        celerityDictionary = new CelerityDictionary<string, int, StringXxHash3Hasher>(ItemCount);
        hashSet = new HashSet<string>(ItemCount);
        celeritySet = new CeleritySet<string, StringXxHash3Hasher>(ItemCount);

        for (int i = 0; i < ItemCount; i++)
        {
            dictionary[keys[i]] = i;
            celerityDictionary[keys[i]] = i;
            hashSet.Add(keys[i]);
            celeritySet.TryAdd(keys[i]);
        }
    }

    // ── Lookup (every key present) ────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int Dictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
            acc += dictionary[key];
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int CelerityDictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
            acc += celerityDictionary[key];
        return acc;
    }

    // ── LookupMissing (every probe walks to a vacant slot) ────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LookupMissing")]
    public int Dictionary_LookupMissing()
    {
        int acc = 0;
        foreach (var key in missingKeys)
            if (dictionary.TryGetValue(key, out int value))
                acc += value;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("LookupMissing")]
    public int CelerityDictionary_LookupMissing()
    {
        int acc = 0;
        foreach (var key in missingKeys)
            if (celerityDictionary.TryGetValue(key, out int value))
                acc += value;
        return acc;
    }

    // ── Contains (the set counterpart, every element present) ─────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public int HashSet_Contains()
    {
        int hits = 0;
        foreach (var key in keys)
            if (hashSet.Contains(key))
                hits++;
        return hits;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public int CeleritySet_Contains()
    {
        int hits = 0;
        foreach (var key in keys)
            if (celeritySet.Contains(key))
                hits++;
        return hits;
    }
}
