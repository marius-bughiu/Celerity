using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// CompressedLongSet vs HashSet<long>, the only exact BCL alternative for an unbounded 64-bit integer
// set. This mirrors CompressedIntSetBenchmark category for category — the same three key
// distributions (sparse, dense, clustered), the same item counts, the same per-iteration rebuild of
// the mutated operands — with every key moved past 2^40, so the data could not be stored in a
// CompressedIntSet at all. The containers are shared code, so the interesting comparison is against
// the BCL, not against the 32-bit sibling: what this class pins is that the 64-bit set keeps the int
// set's lead over HashSet<long> with its wider chunk index.
//
// The notes on CompressedIntSetBenchmark apply unchanged. [MemoryDiagnoser]'s Allocated column on
// the Add category is total bytes allocated, an upper bound on the settled footprint (both arms
// grow from empty, so both pay their discarded intermediates); MemoryUsageInBytes gives the settled
// figure. The sweep is the family's 1,000 / 100,000, a tenth of the headline scale but the same 100x
// sparse universe ratio. And the distribution lives in the category name, because the dashboard's
// name parser accepts exactly one `(ItemCount: N)` suffix.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CompressedLongSetBenchmark
{
    private long[] sparseKeys = null!;
    private long[] sparseOther = null!;
    private long[] denseKeys = null!;
    private long[] denseOther = null!;
    private long[] clusteredKeys = null!;
    private long[] clusteredOther = null!;

    // Operands rebuilt per iteration by the mutating categories below.
    private HashSet<long> hashSet = null!;
    private CompressedLongSet compressed = null!;
    private HashSet<long> hashSetOther = null!;
    private CompressedLongSet compressedOther = null!;

    // Separate, never-mutated instances for the read-only Contains category, so it cannot be
    // measured against whatever a mutating benchmark happened to leave behind.
    private HashSet<long> probeHashSet = null!;
    private CompressedLongSet probeCompressed = null!;

    // Every key is offset by 2^40, so none fits an int and every chunk key uses the upper bits a
    // 32-bit set does not have.
    private const long Base = 1L << 40;

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

        probeHashSet = new HashSet<long>(sparseKeys);
        probeCompressed = new CompressedLongSet(sparseKeys);
        probeCompressed.Optimize();

        hashSet = probeHashSet;
        compressed = probeCompressed;
        hashSetOther = new HashSet<long>(sparseOther);
        compressedOther = new CompressedLongSet(sparseOther);
    }

    private static long[] Distinct(int seed, int count, long universe)
    {
        var rand = new Random(seed);
        var seen = new HashSet<long>(count);
        while (seen.Count < count)
            seen.Add(Base + (long)(rand.NextDouble() * universe));

        long[] keys = new long[count];
        seen.CopyTo(keys);
        return keys;
    }

    // Blocks are laid one per fixed stride and jittered inside it, so they cannot overlap and every
    // key is distinct by construction. Drawing free-floating origins instead would let two blocks
    // collide, which silently shrinks the set below ItemCount and skews both the time and the memory
    // comparison — and does so differently for each distribution.
    private static long[] Clustered(int seed, int count)
    {
        const int Blocks = 32;
        const int Universe = 100_000_000;
        const int Stride = Universe / Blocks;

        var rand = new Random(seed);
        int perBlock = Math.Max(1, ((count + Blocks) - 1) / Blocks); // ceiling: Blocks of these cover count
        long[] keys = new long[count];
        int written = 0;
        for (int block = 0; block < Blocks && written < count; block++)
        {
            long origin = Base + (block * Stride) + rand.Next(0, Stride - perBlock);
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
        var set = new HashSet<long>();
        foreach (long key in sparseKeys)
            set.Add(key);

        return set.Count;
    }

    // The Optimize() call is deliberate and is counted against this arm: it is what a caller does
    // after a bulk load, and it is what makes the Allocated column report the settled footprint
    // rather than an un-compacted one. It costs this arm time to make the memory number honest.
    [Benchmark]
    [BenchmarkCategory("Add")]
    public int CompressedLongSet_Add()
    {
        var set = new CompressedLongSet();
        foreach (long key in sparseKeys)
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
        foreach (long key in sparseKeys)
            result ^= probeHashSet.Contains(key);

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool CompressedLongSet_Contains()
    {
        bool result = false;
        foreach (long key in sparseKeys)
            result ^= probeCompressed.Contains(key);

        return result;
    }

    // ---- IntersectSparse: the headline workload ----

    [IterationSetup(Target = nameof(HashSet_IntersectSparse))]
    public void SetupForHashSetIntersectSparse()
    {
        hashSet = new HashSet<long>(sparseKeys);
        hashSetOther = new HashSet<long>(sparseOther);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IntersectSparse")]
    public void HashSet_IntersectSparse() => hashSet.IntersectWith(hashSetOther);

    [IterationSetup(Target = nameof(CompressedLongSet_IntersectSparse))]
    public void SetupForCompressedIntersectSparse()
    {
        compressed = new CompressedLongSet(sparseKeys);
        compressedOther = new CompressedLongSet(sparseOther);
    }

    [Benchmark]
    [BenchmarkCategory("IntersectSparse")]
    public void CompressedLongSet_IntersectSparse() => compressed.IntersectWith(compressedOther);

    // ---- IntersectDense: every chunk is a bitmap, so the work is word-parallel ----

    [IterationSetup(Target = nameof(HashSet_IntersectDense))]
    public void SetupForHashSetIntersectDense()
    {
        hashSet = new HashSet<long>(denseKeys);
        hashSetOther = new HashSet<long>(denseOther);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IntersectDense")]
    public void HashSet_IntersectDense() => hashSet.IntersectWith(hashSetOther);

    [IterationSetup(Target = nameof(CompressedLongSet_IntersectDense))]
    public void SetupForCompressedIntersectDense()
    {
        compressed = new CompressedLongSet(denseKeys);
        compressedOther = new CompressedLongSet(denseOther);
    }

    [Benchmark]
    [BenchmarkCategory("IntersectDense")]
    public void CompressedLongSet_IntersectDense() => compressed.IntersectWith(compressedOther);

    // ---- IntersectClustered: locally contiguous blocks, the run form's shape ----

    [IterationSetup(Target = nameof(HashSet_IntersectClustered))]
    public void SetupForHashSetIntersectClustered()
    {
        hashSet = new HashSet<long>(clusteredKeys);
        hashSetOther = new HashSet<long>(clusteredOther);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IntersectClustered")]
    public void HashSet_IntersectClustered() => hashSet.IntersectWith(hashSetOther);

    [IterationSetup(Target = nameof(CompressedLongSet_IntersectClustered))]
    public void SetupForCompressedIntersectClustered()
    {
        compressed = new CompressedLongSet(clusteredKeys);
        compressed.Optimize();
        compressedOther = new CompressedLongSet(clusteredOther);
        compressedOther.Optimize();
    }

    [Benchmark]
    [BenchmarkCategory("IntersectClustered")]
    public void CompressedLongSet_IntersectClustered() => compressed.IntersectWith(compressedOther);

    // ---- Union ----

    [IterationSetup(Target = nameof(HashSet_Union))]
    public void SetupForHashSetUnion()
    {
        hashSet = new HashSet<long>(sparseKeys);
        hashSetOther = new HashSet<long>(sparseOther);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Union")]
    public void HashSet_Union() => hashSet.UnionWith(hashSetOther);

    [IterationSetup(Target = nameof(CompressedLongSet_Union))]
    public void SetupForCompressedUnion()
    {
        compressed = new CompressedLongSet(sparseKeys);
        compressedOther = new CompressedLongSet(sparseOther);
    }

    [Benchmark]
    [BenchmarkCategory("Union")]
    public void CompressedLongSet_Union() => compressed.UnionWith(compressedOther);

    // ---- Except ----

    [IterationSetup(Target = nameof(HashSet_Except))]
    public void SetupForHashSetExcept()
    {
        hashSet = new HashSet<long>(sparseKeys);
        hashSetOther = new HashSet<long>(sparseOther);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Except")]
    public void HashSet_Except() => hashSet.ExceptWith(hashSetOther);

    [IterationSetup(Target = nameof(CompressedLongSet_Except))]
    public void SetupForCompressedExcept()
    {
        compressed = new CompressedLongSet(sparseKeys);
        compressedOther = new CompressedLongSet(sparseOther);
    }

    [Benchmark]
    [BenchmarkCategory("Except")]
    public void CompressedLongSet_Except() => compressed.ExceptWith(compressedOther);
}
