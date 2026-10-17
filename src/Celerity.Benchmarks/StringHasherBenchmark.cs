using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Celerity.Hashing;

/// <summary>
/// Throughput of the classic byte-at-a-time <c>string</c> hashers and checksums, plus the two BCL
/// baselines every <c>string</c> hasher benchmark is read against.
/// </summary>
/// <remarks>
/// <para>
/// One of four classes that together compare every built-in <c>string</c> hash provider; the sample, the
/// <see cref="StringHasherBenchmarkBase.Shape"/> sweep and the hashing loops are shared through
/// <see cref="StringHasherBenchmarkBase"/>, which explains the split. This class keeps the original name
/// and the baselines, so the baseline rows publish under the same names they always have.
/// </para>
/// <para>
/// Two baselines bracket the comparison: <see cref="Bcl_GetHashCode"/> calls <see cref="string.GetHashCode()"/>
/// directly, and <see cref="EqualityComparer_Default"/> routes through
/// <see cref="EqualityComparer{T}.Default"/> — the realistic thing a developer actually <em>replaces</em>
/// when swapping a BCL <c>Dictionary&lt;string,&gt;</c> for a struct hasher, since that is the call the BCL
/// dictionary makes per probe. Note that <see cref="string.GetHashCode()"/> is a purpose-built,
/// per-process-randomized <c>Marvin32</c> — the fixed-seed hashers are <strong>not</strong> a
/// HashDoS defence, so this baseline is the speed floor for a <em>randomized, DoS-resistant</em> hash, not
/// a zero-work one. The dashboard reads every <c>string</c> hasher row, in all four classes, relative to
/// <see cref="Bcl_GetHashCode"/>.
/// </para>
/// </remarks>
public class StringHasherBenchmark : StringHasherBenchmarkBase
{
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Hash")]
    public int Bcl_GetHashCode()
    {
        string[] k = Keys;
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
        string[] k = Keys;
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
    public int JenkinsOaat() => HashAll<StringJenkinsOaatHasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Crc32() => HashAll<StringCrc32Hasher>();

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Adler32() => HashAll<StringAdler32Hasher>();
}
