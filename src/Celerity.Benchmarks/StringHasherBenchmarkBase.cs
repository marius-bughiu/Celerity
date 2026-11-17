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
/// heaviest shard that no shard count could lower. Every class inherits the same sample and the
/// <c>GetHashCode</c> loop from here, and the dashboard reads all four as one comparison against the
/// <c>Bcl_GetHashCode</c> baseline in <see cref="StringHasherBenchmark"/>.
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
/// Every hasher arm holds its own loop rather than forwarding to a shared generic helper. The JIT sizes
/// a method's inlining budget from that method's own IL, and an arm that only called
/// <c>HashAll&lt;THasher&gt;()</c> was a few bytes long. Its budget ran out partway down the deeper
/// hashers and left some of their helpers, <c>AggressiveInlining</c> ones included, as calls inside the
/// loop. On an Apple M4 that made the SipHash hashers measure 4–9x slower: with even one round left as
/// a call, their whole state lived on the stack. How much of a deep hasher the JIT inlines still depends
/// on the caller, so these arms measure one realistic caller, a loop over keys, not every call site.
/// <see cref="StringXxHash3Hasher"/>, for one, measures slower on 9–16-character keys in this loop
/// than it did behind the helper.
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
    /// through this one method, so they run identical code. Unlike the hashers, <c>GetHashCode</c>
    /// inlines completely even into a one-line arm, so forwarding here compiles to the same loop an arm
    /// of its own would.
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
