using System.Collections.Frozen;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// Positions the Celerity integer dictionaries against the strongest hash-map
/// implementations in the box, not just the mutable <see cref="Dictionary{TKey, TValue}"/>:
/// the read-optimized <see cref="FrozenDictionary{TKey, TValue}"/> is the BCL's own
/// "build once, read many" answer and the most demanding lookup baseline shipped
/// with .NET.
/// </summary>
/// <remarks>
/// <para>
/// The issue suggested comparing against third-party libraries such as FastHashSet
/// and FASTER. Those are deliberately <em>not</em> included here:
/// </para>
/// <list type="bullet">
/// <item>FASTER (now Tsavorite) is a log-structured, session-based, optionally
///   persistent key-value <em>store</em>, not a drop-in <c>Dictionary&lt;,&gt;</c>.
///   Its API and durability model make a per-op micro-benchmark an apples-to-oranges
///   comparison.</item>
/// <item>Pulling external packages into this project is constrained by the repo's
///   dependency policy. <see cref="FrozenDictionary{TKey, TValue}"/> is the highest-
///   performance, dependency-free, like-for-like peer available, so it is the
///   comparator used here (and already in <c>FrozenCelerityDictionaryBenchmark</c>
///   for the string-keyed frozen map).</item>
/// </list>
/// <para>
/// Only lookups are compared: <see cref="FrozenDictionary{TKey, TValue}"/> pays a
/// heavy one-time build cost in exchange for fast reads, so a construction race
/// against it would not be meaningful for the mutable dictionaries.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LibraryComparisonBenchmark
{
    private int[] keys = null!;
    private Dictionary<int, int> dictionary = null!;
    private FrozenDictionary<int, int> frozenDictionary = null!;
    private IntDictionary<int> intDictionary = null!;
    private CelerityDictionary<int, int, Int32WangNaiveHasher> celerityDictionary = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = KeyDistributions.Int32(Distribution.Uniform, ItemCount);

        dictionary = new Dictionary<int, int>(ItemCount);
        intDictionary = new IntDictionary<int>(ItemCount);
        celerityDictionary = new CelerityDictionary<int, int, Int32WangNaiveHasher>(ItemCount);
        foreach (var key in keys)
        {
            dictionary[key] = key;
            intDictionary[key] = key;
            celerityDictionary[key] = key;
        }

        frozenDictionary = dictionary.ToFrozenDictionary();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int Dictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
        {
            acc += dictionary[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int FrozenDictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
        {
            acc += frozenDictionary[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int IntDictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
        {
            acc += intDictionary[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int CelerityDictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
        {
            acc += celerityDictionary[key];
        }
        return acc;
    }
}
