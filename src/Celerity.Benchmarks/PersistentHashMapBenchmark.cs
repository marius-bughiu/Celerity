using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// PersistentHashMap<int, string> vs ImmutableDictionary<int, string>, which is the BCL's only immutable map.
// It is an AVL tree keyed by hash code with one heap node per entry — key, value, hash, two children and a
// height field — so its branching factor is two and every lookup is a pointer chase per level over nodes
// scattered across the heap. PersistentHashMap is a CHAMP trie: branching factor 32, entries inline in flat
// arrays, and a level costs one popcount plus one array load.
//
// The four categories are the operations the type's documentation makes claims about: the read the workload
// is dominated by, the two writes that path-copy, and the enumeration a snapshot consumer pays.
//
// The baseline arms are named ImmutableDictionary_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PersistentHashMapBenchmark
{
    // Probe positions for the Lookup and Update categories, drawn once so both structures answer the same
    // sequence of keys. Capped so the probe count does not itself scale with ItemCount — what is measured is
    // the cost of one operation, not how many fit in an iteration.
    private const int ProbeCount = 10_000;

    private int[] keys = null!;
    private int[] probes = null!;

    // One distinct value per Update step. Probes are drawn with replacement, so a key recurs — and both
    // structures return the receiver unchanged when SetItem writes a value equal to the one already stored.
    // Assigning a constant would therefore turn every repeat into an equality check rather than a path copy,
    // which at ItemCount = 1,000 is nine calls in ten. A per-step value keeps every operation a real update
    // in both arms.
    private string[] updateValues = null!;

    private PersistentHashMap<int, string, Int32WangNaiveHasher> map = null!;
    private ImmutableDictionary<int, string> immutable = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        keys = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            keys[i] = i;

        // Fisher-Yates, so insertion order is not key order and neither structure gets a sorted build.
        for (int i = ItemCount - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (keys[i], keys[j]) = (keys[j], keys[i]);
        }

        probes = new int[ProbeCount];
        updateValues = new string[ProbeCount];
        for (int i = 0; i < ProbeCount; i++)
        {
            probes[i] = rand.Next(ItemCount);
            updateValues[i] = i.ToString();
        }

        var builder = new PersistentHashMap<int, string, Int32WangNaiveHasher>.Builder();
        ImmutableDictionary<int, string>.Builder bclBuilder = ImmutableDictionary.CreateBuilder<int, string>();
        foreach (int key in keys)
        {
            builder.Add(key, "v");
            bclBuilder.Add(key, "v");
        }

        map = builder.ToImmutable();
        immutable = bclBuilder.ToImmutable();
    }

    // ---- Lookup: the axis the workload is dominated by --------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int ImmutableDictionary_Lookup()
    {
        int found = 0;
        foreach (int probe in probes)
        {
            if (immutable.TryGetValue(probe, out _))
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int PersistentHashMap_Lookup()
    {
        int found = 0;
        foreach (int probe in probes)
        {
            if (map.TryGetValue(probe, out _))
                found++;
        }

        return found;
    }

    // ---- Insert: build the whole map from empty, one entry at a time ------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public int ImmutableDictionary_Insert()
    {
        ImmutableDictionary<int, string> built = ImmutableDictionary<int, string>.Empty;
        foreach (int key in keys)
            built = built.Add(key, "v");

        return built.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public int PersistentHashMap_Insert()
    {
        PersistentHashMap<int, string, Int32WangNaiveHasher> built =
            PersistentHashMap<int, string, Int32WangNaiveHasher>.Empty;
        foreach (int key in keys)
            built = built.Add(key, "v");

        return built.Count;
    }

    // ---- Update: overwrite one entry at a time, keeping the result --------------------------------
    // Each step is applied to the previous result rather than to the original, so the path copies land on
    // storage the previous step created — the shape a caller actually produces when threading a sequence of
    // edits through an immutable value.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Update")]
    public int ImmutableDictionary_Update()
    {
        ImmutableDictionary<int, string> updated = immutable;
        for (int i = 0; i < probes.Length; i++)
            updated = updated.SetItem(probes[i], updateValues[i]);

        return updated.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Update")]
    public int PersistentHashMap_Update()
    {
        PersistentHashMap<int, string, Int32WangNaiveHasher> updated = map;
        for (int i = 0; i < probes.Length; i++)
            updated = updated.SetItem(probes[i], updateValues[i]);

        return updated.Count;
    }

    // ---- Enumerate: the whole map, which is what a snapshot consumer pays -------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long ImmutableDictionary_Enumerate()
    {
        long sum = 0;
        foreach (KeyValuePair<int, string> entry in immutable)
            sum += entry.Key;

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long PersistentHashMap_Enumerate()
    {
        long sum = 0;
        foreach (KeyValuePair<int, string?> entry in map)
            sum += entry.Key;

        return sum;
    }
}
