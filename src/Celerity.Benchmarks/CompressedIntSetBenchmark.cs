using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// CompressedIntSet vs HashSet<int>, the only exact BCL alternative for an unbounded 32-bit integer
// set. The pitch has two halves and this class measures both:
//
//   * Set algebra. HashSet<int> pays one hash probe and one random memory access per element; the
//     compressed set works inside a 65,536-value chunk with sorted merges or whole-word bitmap
//     operations, and skips a chunk neither side populates with a single key comparison. The three
//     Intersect categories sweep the key distribution — sparse (the posting-list shape the type is
//     sold for), dense, and clustered — because which container form each chunk lands in, and
//     therefore which code path runs, is entirely a function of that shape.
//   * Memory. [MemoryDiagnoser] is on and the Add category constructs the whole set, so the
//     Allocated column is the steady-state footprint of each representation rather than incidental
//     garbage. That column is half the reason to use this type.
//
// The sweep stays at the family's 1,000 / 100,000 item counts so the card sits alongside the other
// set cards on the dashboard and the CI A/B run stays affordable. The type's headline claim is
// stated for ~1M elements over a ~100M universe, which is the Sparse shape here at ten times the
// scale — the mechanism, and the ratio, are the same.
//
// The distribution is encoded in the category name rather than in a second [Params] on purpose: the
// dashboard's benchmark-name parser accepts exactly one `(ItemCount: N)` suffix, and a second
// parameter would make every row unparseable and the card blank.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CompressedIntSetBenchmark
{
    private int[] sparseKeys = null!;
    private int[] sparseOther = null!;
    private int[] denseKeys = null!;
    private int[] denseOther = null!;
    private int[] clusteredKeys = null!;
    private int[] clusteredOther = null!;

    // Operands rebuilt per iteration by the mutating categories below.
    private HashSet<int> hashSet = null!;
    private CompressedIntSet compressed = null!;
    private HashSet<int> hashSetOther = null!;
    private CompressedIntSet compressedOther = null!;

    // Separate, never-mutated instances for the read-only Contains category, so it cannot be
    // measured against whatever a mutating benchmark happened to leave behind.
    private HashSet<int> probeHashSet = null!;
    private CompressedIntSet probeCompressed = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        // Sparse: a 100x universe, so a chunk holds a few hundred values and every container is a
        // sorted array — the inverted-index / row-id-set shape.
        sparseKeys = Distinct(1, ItemCount, ItemCount * 100L);
        sparseOther = Distinct(2, ItemCount, ItemCount * 100L);

        // Dense: a 2x universe, so chunks fill past the array threshold and become bitmaps.
        denseKeys = Distinct(3, ItemCount, ItemCount * 2L);
        denseOther = Distinct(4, ItemCount, ItemCount * 2L);

        // Clustered: 32 dense blocks scattered over a 100-million-value range — locally contiguous,
        // globally sparse, which is what the run form exists for.
        clusteredKeys = Clustered(5, ItemCount);
        clusteredOther = Clustered(6, ItemCount);

        probeHashSet = new HashSet<int>(sparseKeys);
        probeCompressed = new CompressedIntSet(sparseKeys);
        probeCompressed.Optimize();

        hashSet = probeHashSet;
        compressed = probeCompressed;
        hashSetOther = new HashSet<int>(sparseOther);
        compressedOther = new CompressedIntSet(sparseOther);
    }

    private static int[] Distinct(int seed, int count, long universe)
    {
        var rand = new Random(seed);
        var seen = new HashSet<int>(count);
        while (seen.Count < count)
            seen.Add((int)(long)(rand.NextDouble() * universe));

        int[] keys = new int[count];
        seen.CopyTo(keys);
        return keys;
    }

    private static int[] Clustered(int seed, int count)
    {
        const int Blocks = 32;
        var rand = new Random(seed);
        int[] keys = new int[count];
        int perBlock = Math.Max(1, count / Blocks);
        int written = 0;
        while (written < count)
        {
            int origin = rand.Next(0, 100_000_000 - perBlock);
            for (int i = 0; i < perBlock && written < count; i++)
                keys[written++] = origin + i;
        }

        return keys;
    }

    // ---- Add: build the set from scratch. The Allocated column is the memory comparison ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public int HashSet_Add()
    {
        var set = new HashSet<int>();
        foreach (int key in sparseKeys)
            set.Add(key);

        return set.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public int CompressedIntSet_Add()
    {
        var set = new CompressedIntSet();
        foreach (int key in sparseKeys)
            set.TryAdd(key);

        set.Optimize();
        return set.Count;
    }

    // ---- Contains: the axis where a hash table is expected to win ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public bool HashSet_Contains()
    {
        bool result = false;
        foreach (int key in sparseKeys)
            result ^= probeHashSet.Contains(key);

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool CompressedIntSet_Contains()
    {
        bool result = false;
        foreach (int key in sparseKeys)
            result ^= probeCompressed.Contains(key);

        return result;
    }

    // ---- IntersectSparse: the headline workload ----

    [IterationSetup(Target = nameof(HashSet_IntersectSparse))]
    public void SetupForHashSetIntersectSparse()
    {
        hashSet = new HashSet<int>(sparseKeys);
        hashSetOther = new HashSet<int>(sparseOther);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IntersectSparse")]
    public void HashSet_IntersectSparse() => hashSet.IntersectWith(hashSetOther);

    [IterationSetup(Target = nameof(CompressedIntSet_IntersectSparse))]
    public void SetupForCompressedIntersectSparse()
    {
        compressed = new CompressedIntSet(sparseKeys);
        compressedOther = new CompressedIntSet(sparseOther);
    }

    [Benchmark]
    [BenchmarkCategory("IntersectSparse")]
    public void CompressedIntSet_IntersectSparse() => compressed.IntersectWith(compressedOther);

    // ---- IntersectDense: every chunk is a bitmap, so the work is word-parallel ----

    [IterationSetup(Target = nameof(HashSet_IntersectDense))]
    public void SetupForHashSetIntersectDense()
    {
        hashSet = new HashSet<int>(denseKeys);
        hashSetOther = new HashSet<int>(denseOther);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IntersectDense")]
    public void HashSet_IntersectDense() => hashSet.IntersectWith(hashSetOther);

    [IterationSetup(Target = nameof(CompressedIntSet_IntersectDense))]
    public void SetupForCompressedIntersectDense()
    {
        compressed = new CompressedIntSet(denseKeys);
        compressedOther = new CompressedIntSet(denseOther);
    }

    [Benchmark]
    [BenchmarkCategory("IntersectDense")]
    public void CompressedIntSet_IntersectDense() => compressed.IntersectWith(compressedOther);

    // ---- IntersectClustered: locally contiguous blocks, the run form's shape ----

    [IterationSetup(Target = nameof(HashSet_IntersectClustered))]
    public void SetupForHashSetIntersectClustered()
    {
        hashSet = new HashSet<int>(clusteredKeys);
        hashSetOther = new HashSet<int>(clusteredOther);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IntersectClustered")]
    public void HashSet_IntersectClustered() => hashSet.IntersectWith(hashSetOther);

    [IterationSetup(Target = nameof(CompressedIntSet_IntersectClustered))]
    public void SetupForCompressedIntersectClustered()
    {
        compressed = new CompressedIntSet(clusteredKeys);
        compressed.Optimize();
        compressedOther = new CompressedIntSet(clusteredOther);
        compressedOther.Optimize();
    }

    [Benchmark]
    [BenchmarkCategory("IntersectClustered")]
    public void CompressedIntSet_IntersectClustered() => compressed.IntersectWith(compressedOther);

    // ---- Union ----

    [IterationSetup(Target = nameof(HashSet_Union))]
    public void SetupForHashSetUnion()
    {
        hashSet = new HashSet<int>(sparseKeys);
        hashSetOther = new HashSet<int>(sparseOther);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Union")]
    public void HashSet_Union() => hashSet.UnionWith(hashSetOther);

    [IterationSetup(Target = nameof(CompressedIntSet_Union))]
    public void SetupForCompressedUnion()
    {
        compressed = new CompressedIntSet(sparseKeys);
        compressedOther = new CompressedIntSet(sparseOther);
    }

    [Benchmark]
    [BenchmarkCategory("Union")]
    public void CompressedIntSet_Union() => compressed.UnionWith(compressedOther);

    // ---- Except ----

    [IterationSetup(Target = nameof(HashSet_Except))]
    public void SetupForHashSetExcept()
    {
        hashSet = new HashSet<int>(sparseKeys);
        hashSetOther = new HashSet<int>(sparseOther);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Except")]
    public void HashSet_Except() => hashSet.ExceptWith(hashSetOther);

    [IterationSetup(Target = nameof(CompressedIntSet_Except))]
    public void SetupForCompressedExcept()
    {
        compressed = new CompressedIntSet(sparseKeys);
        compressedOther = new CompressedIntSet(sparseOther);
    }

    [Benchmark]
    [BenchmarkCategory("Except")]
    public void CompressedIntSet_Except() => compressed.ExceptWith(compressedOther);
}
