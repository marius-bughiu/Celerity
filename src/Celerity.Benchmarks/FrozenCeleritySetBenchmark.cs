using System.Collections.Frozen;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class FrozenCeleritySetBenchmark
{
    private string[] items = null!;

    private FrozenSet<string> frozenSet = null!;
    private FrozenCeleritySet frozenCelerity = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        items = new string[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            // Identifier-shaped, guaranteed-distinct elements.
            items[i] = "celerity/key/" + i + "/" + (i * 2654435761u);
        }

        frozenSet = items.ToFrozenSet();
        frozenCelerity = new FrozenCeleritySet(items);
    }

    // ── Build (construct the frozen structure from the same source elements) ───
    // Baseline is the BCL FrozenSet<>, the read-optimized build-once counterpart —
    // the fair analogue for a frozen, perfect-hashed set (a mutable HashSet<> does
    // no build-time hashing optimization, so it is not a like-for-like baseline).

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public FrozenSet<string> FrozenSet_Build()
        => items.ToFrozenSet();

    [Benchmark]
    [BenchmarkCategory("Build")]
    public FrozenCeleritySet FrozenCeleritySet_Build()
        => new FrozenCeleritySet(items);

    // ── Contains (every element, on the prebuilt instances) ───────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public bool FrozenSet_Contains()
    {
        bool acc = false;
        foreach (var item in items)
            acc ^= frozenSet.Contains(item);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool FrozenCeleritySet_Contains()
    {
        bool acc = false;
        foreach (var item in items)
            acc ^= frozenCelerity.Contains(item);
        return acc;
    }
}
