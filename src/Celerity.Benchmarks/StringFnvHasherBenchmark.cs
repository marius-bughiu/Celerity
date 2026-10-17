using BenchmarkDotNet.Attributes;
using Celerity.Hashing;

/// <summary>
/// Throughput of the FNV-1 / FNV-1a <c>string</c> hashers: the 32- and 64-bit variants and the full-width
/// FNV-1a.
/// </summary>
/// <remarks>
/// One of four classes that together compare every built-in <c>string</c> hash provider; see
/// <see cref="StringHasherBenchmarkBase"/> for the shared harness and why the comparison is split. The
/// baselines live in <see cref="StringHasherBenchmark"/>, so this class reports absolute times and the
/// dashboard computes each row's ratio against that baseline.
/// </remarks>
public class StringFnvHasherBenchmark : StringHasherBenchmarkBase
{
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
}
