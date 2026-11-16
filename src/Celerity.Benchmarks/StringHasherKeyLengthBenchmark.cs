using BenchmarkDotNet.Attributes;
using Celerity.Hashing;

/// <summary>
/// Raw hash cost of the throughput-oriented <c>string</c> hashers at fixed key lengths from 8 to 512 characters:
/// the length sweep behind the key-length claims in <see cref="StringXxHash3Hasher"/>'s documentation.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StringThroughputHasherBenchmark"/> draws its keys from three shapes, the longest 48 to 80
/// characters, so it never reaches <see cref="StringXxHash3Hasher"/>'s path for inputs over 120 characters. This
/// class fixes the length instead. <see cref="StringCityHash64Hasher"/> takes separate paths for up to 8, 9 to 16
/// and 17 to 32 characters and hashes longer keys 32 characters at a time; <see cref="StringXxHash3Hasher"/> takes
/// separate paths up to 64 and from 65 to 120 characters, and above 120 switches to its loop for long inputs. The
/// cells sit at the ends of those paths (8, 16, 32, 64, 120) and just past two of them (33, 121), where a hasher's
/// cost steps up, plus 48 between and 256 and 512 for long keys.
/// </para>
/// <para>
/// Each invocation hashes <see cref="HasherKeySamples.DefaultCount"/> distinct keys of the chosen length, built from
/// the same alphabet and seed as <see cref="HasherKeySamples"/>. They are ASCII: none of these hashers branches on
/// content, so text that varies both bytes of each UTF-16 code unit costs the same. Every arm holds its own loop
/// rather than forwarding to a shared helper, so how much of a hasher the JIT inlines does not depend on a
/// one-line wrapper's budget, and XOR-folds the codes into its return value so the loop is not eliminated.
/// <see cref="Bcl_GetHashCode"/> is the same-runner reference, as in the other <c>string</c> hasher classes.
/// </para>
/// <para>
/// This measures <b>raw hashing cost only</b>: the keys are independent, so successive hashes overlap. A table
/// probe can rank the hashers differently; <see cref="StringInternTableTokenLengthBenchmark"/> sweeps token lengths
/// through one.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
public class StringHasherKeyLengthBenchmark
{
    private string[] keys = null!;

    /// <summary>The length of every key, in UTF-16 code units.</summary>
    [Params(8, 16, 32, 33, 48, 64, 120, 121, 256, 512)]
    public int Length;

    [GlobalSetup]
    public void Setup() => keys = HasherKeySamples.FixedLengthAscii(Length);

    [Benchmark(Baseline = true)]
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
    public int XxHash3()
    {
        StringXxHash3Hasher hasher = default;
        string[] k = keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark]
    public int CityHash64()
    {
        StringCityHash64Hasher hasher = default;
        string[] k = keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark]
    public int XxHash64()
    {
        StringXxHash64Hasher hasher = default;
        string[] k = keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark]
    public int MetroHash64()
    {
        StringMetroHash64Hasher hasher = default;
        string[] k = keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark]
    public int XxHash32()
    {
        StringXxHash32Hasher hasher = default;
        string[] k = keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }

    [Benchmark]
    public int Murmur3()
    {
        StringMurmur3Hasher hasher = default;
        string[] k = keys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= hasher.Hash(k[i]);
        }
        return acc;
    }
}
