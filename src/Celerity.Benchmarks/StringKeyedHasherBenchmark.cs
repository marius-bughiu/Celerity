using BenchmarkDotNet.Attributes;
using Celerity.Hashing;

/// <summary>
/// Throughput of the keyed <c>string</c> hashers — SipHash-1-3, SipHash-2-4, HalfSipHash-2-4 and
/// HighwayHash64 — the hash-flooding-resistant top of the escalation ladder.
/// </summary>
/// <remarks>
/// One of four classes that together compare every built-in <c>string</c> hash provider; see
/// <see cref="StringHasherBenchmarkBase"/> for the shared harness and why the comparison is split. The
/// baselines live in <see cref="StringHasherBenchmark"/>; <see cref="Bcl_GetHashCode"/> here is only the
/// same-runner reference the dashboard rescales this class's rows through.
/// </remarks>
public class StringKeyedHasherBenchmark : StringHasherBenchmarkBase
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
    public int SipHash13()
    {
        StringSipHash13Hasher hasher = default;
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
    public int SipHash24()
    {
        StringSipHash24Hasher hasher = default;
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
    public int HalfSipHash24()
    {
        StringHalfSipHash24Hasher hasher = default;
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
    public int HighwayHash64()
    {
        StringHighwayHash64Hasher hasher = default;
        string[] k = Keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    // ---- IHashProvider64: the un-folded 64-bit surface ----

    // The most expensive per-byte representative of the Hash-vs-Hash64 delta; the cheapest one, and the
    // reason there are only two, is in StringThroughputHasherBenchmark.

    [Benchmark]
    [BenchmarkCategory("Hash")]
    public ulong SipHash24_Hash64()
    {
        StringSipHash24Hasher hasher = default;
        string[] k = Keys;
        ulong acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash64(k[i]);
        }
        return acc;
    }
}
