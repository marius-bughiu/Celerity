using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Primitives;

// SortedSpan vs the two things a .NET developer actually writes to intersect two sorted int arrays:
// HashSet<int> set algebra and LINQ. Neither exploits sortedness at all — the BCL has no set operation
// over spans anywhere — so both must build a hash table and then hash-and-probe one side, while the
// merge walks both sides in order and writes straight into caller memory.
//
// Reading the arms:
//
//   * The HashSet baseline includes building its table, because that is what intersecting two *arrays*
//     costs: the table cannot be carried over, since IntersectWith / ExceptWith consume the instance
//     they mutate. The Celerity arms write into a destination buffer allocated once in [GlobalSetup],
//     which is the usage the type is for (caller-owned, reusable memory) and the reason the
//     Allocated column reads zero for them.
//   * The Asymmetric category is the galloping shape — a short side against one 100x longer, where the
//     merge stops being O(n + m) and becomes O(k log m). It is the posting-list / row-id-filter case.
//   * IntersectCount and Overlaps have no destination at all, so they allocate *nothing*, where both
//     BCL forms must materialize a set before they can answer.
//
// The sweep stays at the family's 1,000 / 100,000 item counts so the card sits alongside the other
// cards on the dashboard and the per-PR A/B run stays affordable. That is below the scale the type's
// headline claim is stated at (1M x 1M, and 1k x 10M for the asymmetric shape) but the same shape;
// the full-scale measurements are recorded in ROADMAP.md.
//
// The LINQ arms carry an op suffix of their own (IntersectLinq / UnionLinq / ExceptLinq) rather than
// sharing the charted op name: the dashboard indexes one BCL and one Celerity series per (collection,
// op), so a second baseline under the same op would silently replace the first on the card.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SortedSpanBenchmark
{
    private int[] left = null!;
    private int[] right = null!;
    private int[] asymmetricSmall = null!;
    private int[] asymmetricLarge = null!;
    private int[] destination = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        // Two independent samples over a 2x universe, so roughly half of each side is shared — the
        // realistic "two ID lists that partly overlap" shape rather than a degenerate all-or-nothing one.
        left = SortedDistinct(1, ItemCount, ItemCount * 2L);
        right = SortedDistinct(2, ItemCount, ItemCount * 2L);

        // 100:1, comfortably past the 32x ratio at which the implementation starts galloping.
        asymmetricSmall = SortedDistinct(3, Math.Max(1, ItemCount / 100), ItemCount * 2L);
        asymmetricLarge = left;

        destination = new int[left.Length + right.Length];
    }

    // ── Intersect ────────────────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true), BenchmarkCategory("Intersect")]
    public int HashSet_Intersect()
    {
        var set = new HashSet<int>(left);
        set.IntersectWith(right);
        return set.Count;
    }

    [Benchmark, BenchmarkCategory("Intersect")]
    public int Linq_IntersectLinq() => left.Intersect(right).Count();

    [Benchmark, BenchmarkCategory("Intersect")]
    public int SortedSpan_Intersect() => SortedSpan.Intersect<int>(left, right, destination);

    // ── Union ────────────────────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true), BenchmarkCategory("Union")]
    public int HashSet_Union()
    {
        var set = new HashSet<int>(left);
        set.UnionWith(right);
        return set.Count;
    }

    [Benchmark, BenchmarkCategory("Union")]
    public int Linq_UnionLinq() => left.Union(right).Count();

    [Benchmark, BenchmarkCategory("Union")]
    public int SortedSpan_Union() => SortedSpan.Union<int>(left, right, destination);

    // ── Except ───────────────────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true), BenchmarkCategory("Except")]
    public int HashSet_Except()
    {
        var set = new HashSet<int>(left);
        set.ExceptWith(right);
        return set.Count;
    }

    [Benchmark, BenchmarkCategory("Except")]
    public int Linq_ExceptLinq() => left.Except(right).Count();

    [Benchmark, BenchmarkCategory("Except")]
    public int SortedSpan_Except() => SortedSpan.Except<int>(left, right, destination);

    // ── IntersectCount (no destination — allocation-free on the Celerity side) ────────────

    [Benchmark(Baseline = true), BenchmarkCategory("IntersectCount")]
    public int HashSet_IntersectCount()
    {
        // Counting rather than materializing is the fairest BCL form here — it skips the second
        // table IntersectWith would build — and it still has to hash and probe every element.
        var set = new HashSet<int>(left);
        int count = 0;
        foreach (int value in right)
        {
            if (set.Contains(value))
                count++;
        }

        return count;
    }

    [Benchmark, BenchmarkCategory("IntersectCount")]
    public int SortedSpan_IntersectCount() => SortedSpan.IntersectCount<int>(left, right);

    // ── Overlaps (early-exit membership test) ────────────────────────────────────────────

    [Benchmark(Baseline = true), BenchmarkCategory("Overlaps")]
    public bool HashSet_Overlaps() => new HashSet<int>(left).Overlaps(right);

    [Benchmark, BenchmarkCategory("Overlaps")]
    public bool SortedSpan_Overlaps() => SortedSpan.Overlaps<int>(left, right);

    // ── Asymmetric intersect: the galloping shape ────────────────────────────────────────

    [Benchmark(Baseline = true), BenchmarkCategory("IntersectAsymmetric")]
    public int HashSet_IntersectAsymmetric()
    {
        // Build the table on the short side and probe the long one: the cheaper of the two BCL
        // orderings, so the baseline is the strongest form rather than the most naive.
        var set = new HashSet<int>(asymmetricSmall);
        set.IntersectWith(asymmetricLarge);
        return set.Count;
    }

    [Benchmark, BenchmarkCategory("IntersectAsymmetric")]
    public int SortedSpan_IntersectAsymmetric()
        => SortedSpan.Intersect<int>(asymmetricSmall, asymmetricLarge, destination);

    private static int[] SortedDistinct(int seed, int count, long universe)
    {
        var rand = new Random(seed);
        var seen = new HashSet<int>(count);
        while (seen.Count < count)
        {
            seen.Add((int)(rand.NextInt64(0, universe)));
        }

        int[] values = seen.ToArray();
        Array.Sort(values);
        return values;
    }
}
