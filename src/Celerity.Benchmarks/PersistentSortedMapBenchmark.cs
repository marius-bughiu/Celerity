using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// PersistentSortedMap<int, string> vs ImmutableSortedDictionary<int, string>, which is the BCL's only immutable
// sorted map. It is an AVL tree with one heap node per entry — key, value, two children and a height field — so
// its branching factor is two and every lookup is a pointer chase per level over nodes scattered across the
// heap. PersistentSortedMap is a path-copying B+ tree of fan-out 32: keys and values sit in flat per-leaf arrays,
// a level costs one binary search over a contiguous key array, and enumeration walks the leaves' arrays.
//
// The first four categories mirror PersistentHashMapBenchmark: the read the workload is dominated by, the two
// writes that path-copy, and the enumeration a snapshot consumer pays. The fifth, LowerBound, is the ordered
// query the type exists for. ImmutableSortedDictionary has no seek at all — no lower bound, no range view — so
// the cheapest correct BCL answer is to enumerate from the smallest key until the first key >= the probe, an
// O(n) scan per query against the tree's O(log n) descent.
//
// The baseline arms are named ImmutableSortedDictionary_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PersistentSortedMapBenchmark
{
    // Probe positions for the Lookup and Update categories, drawn once so both structures answer the same
    // sequence of keys. Capped so the probe count does not itself scale with ItemCount — what is measured is
    // the cost of one operation, not how many fit in an iteration.
    private const int ProbeCount = 10_000;

    // The LowerBound category uses far fewer probes, identically in both arms: the BCL arm is an O(n) scan per
    // probe, so 10,000 of them at ItemCount = 100,000 would be ~500 million node visits per invocation. The
    // per-probe cost is what the ratio reports, and it is unaffected by the count.
    private const int LowerBoundProbeCount = 100;

    private int[] keys = null!;
    private int[] probes = null!;
    private int[] lowerBoundProbes = null!;

    // One distinct value per Update step. Probes are drawn with replacement, so a key recurs — and both
    // structures return the receiver unchanged when SetItem writes a value equal to the one already stored.
    // Assigning a constant would therefore turn every repeat into an equality check rather than a path copy,
    // which at ItemCount = 1,000 is nine calls in ten. A per-step value keeps every operation a real update
    // in both arms.
    private string[] updateValues = null!;

    private PersistentSortedMap<int, string, DefaultComparer<int>> map = null!;
    private ImmutableSortedDictionary<int, string> immutable = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        // Even keys only, so a LowerBound probe can fall between two keys: an odd probe is a miss whose bound
        // is the next key up, and a probe past the largest key has no bound at all.
        keys = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            keys[i] = 2 * i;

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
            probes[i] = 2 * rand.Next(ItemCount);
            updateValues[i] = i.ToString();
        }

        // Uniform over [0, 2 * ItemCount], so about half are exact hits, half land between keys, and the top
        // value lands past the largest key.
        lowerBoundProbes = new int[LowerBoundProbeCount];
        for (int i = 0; i < LowerBoundProbeCount; i++)
            lowerBoundProbes[i] = rand.Next(2 * ItemCount + 1);

        var builder = new PersistentSortedMap<int, string, DefaultComparer<int>>.Builder();
        ImmutableSortedDictionary<int, string>.Builder bclBuilder = ImmutableSortedDictionary.CreateBuilder<int, string>();
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
    public int ImmutableSortedDictionary_Lookup()
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
    public int PersistentSortedMap_Lookup()
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
    public int ImmutableSortedDictionary_Insert()
    {
        ImmutableSortedDictionary<int, string> built = ImmutableSortedDictionary<int, string>.Empty;
        foreach (int key in keys)
            built = built.Add(key, "v");

        return built.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public int PersistentSortedMap_Insert()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> built =
            PersistentSortedMap<int, string, DefaultComparer<int>>.Empty;
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
    public int ImmutableSortedDictionary_Update()
    {
        ImmutableSortedDictionary<int, string> updated = immutable;
        for (int i = 0; i < probes.Length; i++)
            updated = updated.SetItem(probes[i], updateValues[i]);

        return updated.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Update")]
    public int PersistentSortedMap_Update()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> updated = map;
        for (int i = 0; i < probes.Length; i++)
            updated = updated.SetItem(probes[i], updateValues[i]);

        return updated.Count;
    }

    // ---- Enumerate: the whole map in key order, which is what a snapshot consumer pays ------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long ImmutableSortedDictionary_Enumerate()
    {
        long sum = 0;
        foreach (KeyValuePair<int, string> entry in immutable)
            sum += entry.Key;

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long PersistentSortedMap_Enumerate()
    {
        long sum = 0;
        foreach (KeyValuePair<int, string?> entry in map)
            sum += entry.Key;

        return sum;
    }

    // ---- LowerBound: the first key >= a probe, the ordered query the type exists for --------------
    // ImmutableSortedDictionary has no seek, so its arm scans in key order from the smallest key and stops at
    // the first key that is not below the probe — the cheapest answer it can give without copying the keys out.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LowerBound")]
    public long ImmutableSortedDictionary_LowerBound()
    {
        long sum = 0;
        foreach (int probe in lowerBoundProbes)
        {
            foreach (KeyValuePair<int, string> entry in immutable)
            {
                if (entry.Key >= probe)
                {
                    sum += entry.Key;
                    break;
                }
            }
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("LowerBound")]
    public long PersistentSortedMap_LowerBound()
    {
        long sum = 0;
        foreach (int probe in lowerBoundProbes)
        {
            if (map.TryGetLowerBound(probe, out KeyValuePair<int, string?> entry))
                sum += entry.Key;
        }

        return sum;
    }
}
