using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Hashing;

/// <summary>
/// Shared harness for the <c>string</c> hasher throughput benchmarks: the key sample, the
/// <see cref="Shape"/> sweep and the <c>string.GetHashCode()</c> reference loop.
/// </summary>
/// <remarks>
/// <para>
/// The comparison is one logical benchmark spread over four classes, split by hasher family:
/// <see cref="StringHasherBenchmark"/> (the two BCL baselines plus the classic byte-at-a-time hashers
/// and checksums), <see cref="StringFnvHasherBenchmark"/>, <see cref="StringThroughputHasherBenchmark"/>
/// and <see cref="StringKeyedHasherBenchmark"/>. The split exists only for CI: the benchmark workflow
/// shards the suite by class, and the comparison as a single 81-case class set a floor under the
/// heaviest shard that no shard count could lower. Every class inherits the same sample and loops from
/// here, and the dashboard reads all four as one comparison against the <c>Bcl_GetHashCode</c> baseline
/// in <see cref="StringHasherBenchmark"/>.
/// </para>
/// <para>
/// Splitting gives up one thing the single class had: every row shared a runner with the baseline. The
/// packer can put a family class on a different hosted runner, and runners differ enough that raw times
/// from two of them would read as hasher speedups or slowdowns. So each family class also measures
/// <c>Bcl_GetHashCode</c> (through the same <see cref="HashAllBcl"/> as the baseline, unmarked) as a
/// same-runner reference, and the dashboard rescales that family's rows by baseline / reference rather
/// than comparing raw times across runners. The references are not drawn.
/// </para>
/// <para>
/// Each benchmark hashes the same <see cref="HasherKeySamples.DefaultCount"/> distinct keys through one
/// hasher. The arm calls <c>Hash</c> on a <c>default</c> instance of the hasher struct, a direct call
/// with no interface dispatch, as in a collection specialized for that hasher. Every arm XOR-folds its
/// codes into a single returned value, so BenchmarkDotNet consumes the result and the loop cannot be
/// dead-code-eliminated.
/// </para>
/// <para>
/// Every hasher arm holds its own loop rather than calling a shared generic helper. An arm that only
/// forwards to <c>HashAll&lt;THasher&gt;()</c> is a tiny method, and the JIT sizes its inlining budget
/// from the method it compiles, so it ran out partway through the deeper hashers and left some of
/// their helpers as calls inside the loop: a cost of the benchmark's shape, not the hasher's.
/// </para>
/// <para>
/// The <see cref="Shape"/> parameter sweeps three representative key shapes — short ASCII identifiers,
/// long ASCII paths, and shorter non-ASCII / CJK text — because the length-classed hashers
/// (<see cref="StringCityHash64Hasher"/>, <see cref="StringXxHash3Hasher"/>) and the four-accumulator
/// stripe hashers (<see cref="StringXxHash32Hasher"/>, <see cref="StringXxHash64Hasher"/>) behave very
/// differently across lengths, and the full-width hashers do extra work on non-ASCII upper bytes.
/// </para>
/// <para>
/// This measures <b>throughput only</b>. A fast hasher that clusters is not a win, so pair these numbers
/// with the offline distribution metric from
/// <see cref="HashQualityEvaluator.Evaluate{T, THasher}(System.Collections.Generic.IEnumerable{T}, int)"/>
/// (see <c>docs/api/hashing.md</c>) when choosing a hasher for a given key shape. Treat it as a
/// <strong>raw-mixing-cost diagnostic only</strong>; the headline metric is end-to-end table throughput.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public abstract class StringHasherBenchmarkBase
{
    private string[] keys = null!;

    [Params(KeyShape.ShortAscii, KeyShape.LongAscii, KeyShape.NonAscii)]
    public KeyShape Shape;

    [GlobalSetup]
    public void Setup() => keys = HasherKeySamples.Strings(Shape);

    /// <summary>The key sample for the current <see cref="Shape"/>.</summary>
    protected string[] Keys => keys;

    /// <summary>
    /// Hashes every key through <see cref="string.GetHashCode()"/>, XOR-folding the codes into a single
    /// returned value like the hasher arms. The baseline and every family's same-runner reference measure
    /// through this one method, so they run identical code.
    /// </summary>
    protected int HashAllBcl()
    {
        string[] k = keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= k[i].GetHashCode();
        }
        return acc;
    }
}
