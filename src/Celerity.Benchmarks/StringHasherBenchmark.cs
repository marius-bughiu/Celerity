using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Hashing;

/// <summary>
/// Head-to-head throughput comparison of every built-in <c>string</c> hash provider.
/// </summary>
/// <remarks>
/// <para>
/// Each benchmark hashes the same sample of <see cref="KeyCount"/> distinct keys through one hasher,
/// invoked via the <c>where THasher : struct, IHashProvider&lt;string&gt;</c> constraint so the JIT
/// devirtualizes and inlines <c>Hash</c> exactly as it does on a real Celerity collection's probe path.
/// The BCL <see cref="string.GetHashCode()"/> is the baseline, so each ratio reads as "this hasher
/// relative to the framework string hash".
/// </para>
/// <para>
/// The <see cref="Shape"/> parameter sweeps three representative key shapes — short ASCII identifiers,
/// long ASCII paths, and shorter non-ASCII / CJK text — because the length-classed hashers
/// (<see cref="StringCityHash64Hasher"/>, <see cref="StringXxHash3Hasher"/>) and the four-accumulator
/// stripe hashers (<see cref="StringXxHash32Hasher"/>, <see cref="StringXxHash64Hasher"/>) behave very
/// differently across lengths, and the full-width hashers do extra work on non-ASCII upper bytes.
/// </para>
/// <para>
/// This benchmark measures <b>throughput only</b>. A fast hasher that clusters is not a win, so pair
/// these numbers with the offline distribution metric from
/// <see cref="HashQualityEvaluator.Evaluate{T, THasher}(System.Collections.Generic.IEnumerable{T}, int)"/>
/// (see <c>docs/api/hashing.md</c>) when choosing a hasher for a given key shape. Treat it as a
/// <strong>raw-mixing-cost diagnostic only</strong>; the headline metric is end-to-end table throughput.
/// </para>
/// <para>
/// Two baselines bracket the comparison: <see cref="Bcl_GetHashCode"/> calls <see cref="string.GetHashCode()"/>
/// directly, and <see cref="EqualityComparer_Default"/> routes through
/// <see cref="EqualityComparer{T}.Default"/> — the realistic thing a developer actually <em>replaces</em>
/// when swapping a BCL <c>Dictionary&lt;string,&gt;</c> for a struct hasher, since that is the call the BCL
/// dictionary makes per probe. Note that <see cref="string.GetHashCode()"/> is a purpose-built,
/// per-process-randomized <c>Marvin32</c> — the fixed-seed hashers below are <strong>not</strong> a
/// HashDoS defence, so this baseline is the speed floor for a <em>randomized, DoS-resistant</em> hash, not
/// a zero-work one.
/// </para>
/// <para>
/// Every arm XOR-folds its codes into a single returned value, so BenchmarkDotNet consumes the result and
/// the loop cannot be dead-code-eliminated.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class StringHasherBenchmark
{
    private string[] keys = null!;

    [Params(KeyShape.ShortAscii, KeyShape.LongAscii, KeyShape.NonAscii)]
    public KeyShape Shape;

    [GlobalSetup]
    public void Setup() => keys = HasherKeySamples.Strings(Shape);

    /// <summary>
    /// Hashes every key through <typeparamref name="THasher"/>, XOR-folding the codes into a single
    /// returned value so the JIT cannot elide the loop. The hasher is a stateless struct, instantiated
    /// once via <c>default</c> outside the loop, matching how the collections hold theirs.
    /// </summary>
    private int HashAll<THasher>() where THasher : struct, IHashProvider<string>
    {
        THasher hasher = default;
        string[] k = keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Hash")]
    public int Bcl_GetHashCode()
    {
        string[] k = keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= k[i].GetHashCode();
        }
        return acc;
    }

    /// <summary>
    /// The realistic <strong>replaced</strong> baseline: hashes every key through
    /// <see cref="EqualityComparer{T}.Default"/>, the exact call a BCL <c>Dictionary&lt;string,&gt;</c> makes
    /// per probe. The comparer is fetched once into a local (as the dictionary caches it) and the per-call
    /// dispatch is left intact, because eliminating it is precisely what a struct hasher saves.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int EqualityComparer_Default()
    {
        EqualityComparer<string> comparer = EqualityComparer<string>.Default;
        string[] k = keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= comparer.GetHashCode(k[i]);
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Djb2() => HashAll<StringDjb2Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Djb2A() => HashAll<StringDjb2AHasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Sdbm() => HashAll<StringSdbmHasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Elf() => HashAll<StringElfHasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Crc32() => HashAll<StringCrc32Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Adler32() => HashAll<StringAdler32Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int FnV1() => HashAll<StringFnV1Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int FnV1_64() => HashAll<StringFnV164Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int FnV1A() => HashAll<StringFnV1AHasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int FnV1A_Full() => HashAll<StringFnV1AFullHasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int FnV1A_64() => HashAll<StringFnV1A64Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int JenkinsOaat() => HashAll<StringJenkinsOaatHasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Murmur2() => HashAll<StringMurmur2Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Murmur3() => HashAll<StringMurmur3Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int XxHash32() => HashAll<StringXxHash32Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int XxHash64() => HashAll<StringXxHash64Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int XxHash3() => HashAll<StringXxHash3Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int CityHash64() => HashAll<StringCityHash64Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int MetroHash64() => HashAll<StringMetroHash64Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int SipHash13() => HashAll<StringSipHash13Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int SipHash24() => HashAll<StringSipHash24Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int HalfSipHash24() => HashAll<StringHalfSipHash24Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int HighwayHash64() => HashAll<StringHighwayHash64Hasher>();
}
