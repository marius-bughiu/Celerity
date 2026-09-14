using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// PersistentHashSet<int> vs ImmutableHashSet<int>, which is the BCL's only immutable hash set. It is an AVL
// tree keyed by hash code with one heap node per distinct hash — hash, bucket, two children and a height
// field — so its branching factor is two and every membership test is a pointer chase per level over nodes
// scattered across the heap. PersistentHashSet is a CHAMP trie: branching factor 32, elements inline in flat
// arrays, and a level costs one popcount plus one array load.
//
// The four categories are the operations the type's documentation makes claims about: the membership test
// the workload is dominated by, the two edits that path-copy, and the enumeration a snapshot consumer pays.
//
// The baseline arms are named ImmutableHashSet_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PersistentHashSetBenchmark
{
    // Probe positions for the Contains category, drawn once so both structures answer the same sequence.
    // Capped so the probe count does not itself scale with ItemCount — what is measured is the cost of one
    // operation, not how many fit in an iteration.
    private const int ProbeCount = 10_000;

    private int[] elements = null!;
    private int[] probes = null!;

    // The elements the Remove category takes out, in order: distinct and all present, so every step is a real
    // path copy in both arms rather than a no-op that hands back the receiver. Half the set at most, so the
    // 1,000-element run is not measuring a drain to empty.
    private int[] removals = null!;

    private PersistentHashSet<int, Int32WangNaiveHasher> set = null!;
    private ImmutableHashSet<int> immutable = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        elements = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            elements[i] = i;

        // Fisher-Yates, so insertion order is not element order and neither structure gets a sorted build.
        for (int i = ItemCount - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (elements[i], elements[j]) = (elements[j], elements[i]);
        }

        // Drawn from twice the element range, so about half the probes miss: an allow- or deny-list check is
        // as often a miss as a hit, and a miss is where a hash trie can stop at an empty slot.
        probes = new int[ProbeCount];
        for (int i = 0; i < ProbeCount; i++)
            probes[i] = rand.Next(ItemCount * 2);

        removals = elements[..Math.Min(ProbeCount, ItemCount / 2)];

        var builder = new PersistentHashSet<int, Int32WangNaiveHasher>.Builder();
        ImmutableHashSet<int>.Builder bclBuilder = ImmutableHashSet.CreateBuilder<int>();
        foreach (int element in elements)
        {
            builder.Add(element);
            bclBuilder.Add(element);
        }

        set = builder.ToImmutable();
        immutable = bclBuilder.ToImmutable();
    }

    // ---- Contains: the axis the workload is dominated by ------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public int ImmutableHashSet_Contains()
    {
        int found = 0;
        foreach (int probe in probes)
        {
            if (immutable.Contains(probe))
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public int PersistentHashSet_Contains()
    {
        int found = 0;
        foreach (int probe in probes)
        {
            if (set.Contains(probe))
                found++;
        }

        return found;
    }

    // ---- Insert: build the whole set from empty, one element at a time ----------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public int ImmutableHashSet_Insert()
    {
        ImmutableHashSet<int> built = ImmutableHashSet<int>.Empty;
        foreach (int element in elements)
            built = built.Add(element);

        return built.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public int PersistentHashSet_Insert()
    {
        PersistentHashSet<int, Int32WangNaiveHasher> built = PersistentHashSet<int, Int32WangNaiveHasher>.Empty;
        foreach (int element in elements)
            built = built.Add(element);

        return built.Count;
    }

    // ---- Remove: take elements out one at a time, keeping the result -----------------------------
    // Each step is applied to the previous result rather than to the original, so the path copies land on
    // storage the previous step created — the shape a caller produces when threading edits through a value.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public int ImmutableHashSet_Remove()
    {
        ImmutableHashSet<int> trimmed = immutable;
        foreach (int element in removals)
            trimmed = trimmed.Remove(element);

        return trimmed.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public int PersistentHashSet_Remove()
    {
        PersistentHashSet<int, Int32WangNaiveHasher> trimmed = set;
        foreach (int element in removals)
            trimmed = trimmed.Remove(element);

        return trimmed.Count;
    }

    // ---- Enumerate: the whole set, which is what a snapshot consumer pays -------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long ImmutableHashSet_Enumerate()
    {
        long sum = 0;
        foreach (int element in immutable)
            sum += element;

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long PersistentHashSet_Enumerate()
    {
        long sum = 0;
        foreach (int element in set)
            sum += element;

        return sum;
    }
}
