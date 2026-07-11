using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// EnumSet<TEnum> vs HashSet<TEnum>. The workload is a bounded enum universe: unlike the
// open-addressed sets there is no [Params] item count — the universe is the enum's set of
// members. BenchEnum has 40 contiguous members (one 64-bit word), the shape EnumSet is built
// for. Add / Contains / Remove show the per-element win (a shift + mask + single-word bit op
// vs a hash-and-probe); Union is the headline set-algebra win, where EnumSet folds the whole
// set together with one word-wise OR while HashSet re-hashes and probes every element.
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class EnumSetBenchmark
{
    private BenchEnum[] all = null!;
    private BenchEnum[] firstHalf = null!;
    private BenchEnum[] secondHalf = null!;

    private HashSet<BenchEnum> hashSet = null!;
    private EnumSet<BenchEnum> enumSet = null!;

    private HashSet<BenchEnum> hashSetA = null!;
    private HashSet<BenchEnum> hashSetB = null!;
    private EnumSet<BenchEnum> enumSetA = null!;
    private EnumSet<BenchEnum> enumSetB = null!;

    [GlobalSetup]
    public void Setup()
    {
        all = Enum.GetValues<BenchEnum>();
        int mid = all.Length / 2;
        firstHalf = all.Take(mid + 4).ToArray();   // overlapping halves so Union has real work
        secondHalf = all.Skip(mid - 4).ToArray();

        hashSet = new HashSet<BenchEnum>(all);
        enumSet = new EnumSet<BenchEnum>(all);

        hashSetA = new HashSet<BenchEnum>(firstHalf);
        hashSetB = new HashSet<BenchEnum>(secondHalf);
        enumSetA = new EnumSet<BenchEnum>(firstHalf);
        enumSetB = new EnumSet<BenchEnum>(secondHalf);
    }

    // ── Add (build fresh, add every member) ───────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void HashSet_Add()
    {
        var set = new HashSet<BenchEnum>();
        foreach (var e in all)
            set.Add(e);
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void EnumSet_Add()
    {
        var set = new EnumSet<BenchEnum>();
        foreach (var e in all)
            set.Add(e);
    }

    // ── Contains (every member, on the prebuilt instances) ────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public bool HashSet_Contains()
    {
        bool acc = false;
        foreach (var e in all)
            acc ^= hashSet.Contains(e);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool EnumSet_Contains()
    {
        bool acc = false;
        foreach (var e in all)
            acc ^= enumSet.Contains(e);
        return acc;
    }

    // ── Remove (every member; refilled each iteration) ────────────────────────

    [IterationSetup(Target = nameof(HashSet_Remove))]
    public void SetupForHashSetRemove() => hashSet = new HashSet<BenchEnum>(all);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void HashSet_Remove()
    {
        foreach (var e in all)
            hashSet.Remove(e);
    }

    [IterationSetup(Target = nameof(EnumSet_Remove))]
    public void SetupForEnumSetRemove() => enumSet = new EnumSet<BenchEnum>(all);

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void EnumSet_Remove()
    {
        foreach (var e in all)
            enumSet.Remove(e);
    }

    // ── Union (copy A, union B into it) — the word-wise set-algebra win ────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Union")]
    public HashSet<BenchEnum> HashSet_Union()
    {
        var set = new HashSet<BenchEnum>(hashSetA);
        set.UnionWith(hashSetB);
        return set;
    }

    [Benchmark]
    [BenchmarkCategory("Union")]
    public EnumSet<BenchEnum> EnumSet_Union()
    {
        var set = new EnumSet<BenchEnum>(enumSetA);
        set.UnionWith(enumSetB);
        return set;
    }
}

// A 40-member enum (contiguous 0..39, one 64-bit word) — the small, dense, non-negative shape
// EnumSet is designed for.
public enum BenchEnum
{
    V00, V01, V02, V03, V04, V05, V06, V07, V08, V09,
    V10, V11, V12, V13, V14, V15, V16, V17, V18, V19,
    V20, V21, V22, V23, V24, V25, V26, V27, V28, V29,
    V30, V31, V32, V33, V34, V35, V36, V37, V38, V39,
}
