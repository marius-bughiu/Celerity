using System.Text;
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
/// (see <c>docs/api/hashing.md</c>) when choosing a hasher for a given key shape.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class StringHasherBenchmark
{
    /// <summary>The key shapes swept by <see cref="Shape"/>.</summary>
    public enum KeyShape
    {
        /// <summary>Short lowercase-alphanumeric identifiers (6–12 chars), the common map-key case.</summary>
        ShortAscii,

        /// <summary>Long ASCII path / URL-like keys (48–80 chars).</summary>
        LongAscii,

        /// <summary>Shorter mixed Latin + CJK text (10–20 chars) that exercises the full-width fold.</summary>
        NonAscii,
    }

    private const int KeyCount = 2_000;

    private string[] keys = null!;

    [Params(KeyShape.ShortAscii, KeyShape.LongAscii, KeyShape.NonAscii)]
    public KeyShape Shape;

    [GlobalSetup]
    public void Setup()
    {
        // Deterministic seed so the key sample is identical across runs and across hashers.
        Random rand = new(42);
        keys = new string[KeyCount];
        for (int i = 0; i < KeyCount; i++)
        {
            keys[i] = Shape switch
            {
                KeyShape.ShortAscii => MakeAscii(rand, rand.Next(6, 13)),
                KeyShape.LongAscii => MakeAscii(rand, rand.Next(48, 81)),
                KeyShape.NonAscii => MakeNonAscii(rand, rand.Next(10, 21)),
                _ => throw new InvalidOperationException(),
            };
        }
    }

    private static string MakeAscii(Random rand, int length)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789_/";
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append(alphabet[rand.Next(alphabet.Length)]);
        }
        return sb.ToString();
    }

    private static string MakeNonAscii(Random rand, int length)
    {
        // Mix Latin letters with CJK code points (U+4E00–U+9FFF) so both bytes of the
        // UTF-16 code unit vary — the case the full-width string hashers are built for.
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append(rand.Next(2) == 0
                ? (char)('a' + rand.Next(26))
                : (char)(0x4E00 + rand.Next(0x9FFF - 0x4E00 + 1)));
        }
        return sb.ToString();
    }

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
