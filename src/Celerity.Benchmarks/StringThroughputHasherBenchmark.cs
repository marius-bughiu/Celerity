using BenchmarkDotNet.Attributes;
using Celerity.Hashing;

/// <summary>
/// Throughput of the unkeyed, throughput-oriented <c>string</c> hashers: Murmur2 / Murmur3, the xxHash
/// family, CityHash64 and MetroHash64.
/// </summary>
/// <remarks>
/// One of four classes that together compare every built-in <c>string</c> hash provider; see
/// <see cref="StringHasherBenchmarkBase"/> for the shared harness and why the comparison is split. The
/// baselines live in <see cref="StringHasherBenchmark"/>; <see cref="Bcl_GetHashCode"/> here is only the
/// same-runner reference the dashboard rescales this class's rows through.
/// </remarks>
public class StringThroughputHasherBenchmark : StringHasherBenchmarkBase
{
    /// <summary>
    /// Same-runner reference, not a comparison row: <see cref="StringHasherBenchmark.Bcl_GetHashCode"/>
    /// re-measured in this class, which CI may shard onto a different runner. Deliberately not marked
    /// <c>Baseline</c>, so a joined run still has one baseline per group.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Bcl_GetHashCode() => HashAllBcl();

    // Each hasher arm holds its own loop instead of forwarding to a shared generic helper;
    // StringHasherBenchmarkBase explains why.
    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Murmur2()
    {
        StringMurmur2Hasher hasher = default;
        string[] k = Keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int Murmur3()
    {
        StringMurmur3Hasher hasher = default;
        string[] k = Keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int XxHash32()
    {
        StringXxHash32Hasher hasher = default;
        string[] k = Keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int XxHash64()
    {
        StringXxHash64Hasher hasher = default;
        string[] k = Keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int XxHash3()
    {
        StringXxHash3Hasher hasher = default;
        string[] k = Keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int CityHash64()
    {
        StringCityHash64Hasher hasher = default;
        string[] k = Keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public int MetroHash64()
    {
        StringMetroHash64Hasher hasher = default;
        string[] k = Keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    // ---- IHashProvider64: the un-folded 64-bit surface ----

    // The nine 64-bit string hashers compute a ulong internally and Hash is exactly that value plus one
    // xor-fold, so the Hash-vs-Hash64 delta is structural and identical across all nine. Two
    // representatives are enough to put that delta on the dashboard: the cheapest bulk hasher here, and
    // the most expensive per-byte one (SipHash24_Hash64) in StringKeyedHasherBenchmark. Duplicating all
    // nine would add 21 more cases for no additional information.

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public ulong XxHash64_Hash64()
    {
        StringXxHash64Hasher hasher = default;
        string[] k = Keys;
        ulong acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash64(k[i]);
        }
        return acc;
    }
}
