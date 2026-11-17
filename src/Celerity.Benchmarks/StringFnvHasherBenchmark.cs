using BenchmarkDotNet.Attributes;
using Celerity.Hashing;

/// <summary>
/// Throughput of the FNV-1 / FNV-1a <c>string</c> hashers: the 32- and 64-bit variants and the full-width
/// FNV-1a.
/// </summary>
/// <remarks>
/// One of four classes that together compare every built-in <c>string</c> hash provider; see
/// <see cref="StringHasherBenchmarkBase"/> for the shared harness and why the comparison is split. The
/// baselines live in <see cref="StringHasherBenchmark"/>; <see cref="Bcl_GetHashCode"/> here is only the
/// same-runner reference the dashboard rescales this class's rows through.
/// </remarks>
public class StringFnvHasherBenchmark : StringHasherBenchmarkBase
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
    public int FnV1()
    {
        StringFnV1Hasher hasher = default;
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
    public int FnV1_64()
    {
        StringFnV164Hasher hasher = default;
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
    public int FnV1A()
    {
        StringFnV1AHasher hasher = default;
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
    public int FnV1A_Full()
    {
        StringFnV1AFullHasher hasher = default;
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
    public int FnV1A_64()
    {
        StringFnV1A64Hasher hasher = default;
        string[] k = Keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }
}
