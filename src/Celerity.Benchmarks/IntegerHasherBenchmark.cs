using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Hashing;

/// <summary>
/// Head-to-head throughput comparison of the built-in fixed-width integer and <see cref="System.Guid"/>
/// hash providers, grouped by key type.
/// </summary>
/// <remarks>
/// <para>
/// Each benchmark hashes the same sample of <see cref="KeyCount"/> keys through one hasher, invoked via
/// the <c>where THasher : struct, IHashProvider&lt;T&gt;</c> constraint so the JIT devirtualizes and
/// inlines <c>Hash</c> exactly as it does on a real Celerity collection's probe path. Within each
/// <see cref="BenchmarkCategoryAttribute"/> (one per key type) the BCL <c>GetHashCode()</c> is the
/// baseline, so each ratio reads as "this hasher relative to the framework hash for that type".
/// </para>
/// <para>
/// Unlike <c>StringHasherBenchmark</c> there is no key-shape parameter: hashing a fixed-width integer is
/// branch-free and constant-time regardless of the key's value, so the distribution of the sample
/// affects collision <em>quality</em> (measured offline by
/// <see cref="HashQualityEvaluator.Evaluate{T, THasher}(System.Collections.Generic.IEnumerable{T}, int)"/>)
/// but not <c>Hash</c> throughput. Read these numbers alongside that distribution metric — a fast hasher
/// that clusters is not a win.
/// </para>
/// <para>
/// Method names follow the <c>{Type}_{Hasher}</c> convention (e.g. <c>Int32_WangNaive</c>) so the
/// gh-pages dashboard can group hashers by key type.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IntegerHasherBenchmark
{
    private const int KeyCount = 2_000;

    private int[] intKeys = null!;
    private long[] longKeys = null!;
    private uint[] uintKeys = null!;
    private ulong[] ulongKeys = null!;
    private Guid[] guidKeys = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Deterministic seed so the key sample is identical across runs and across hashers.
        Random rand = new(42);
        intKeys = new int[KeyCount];
        longKeys = new long[KeyCount];
        uintKeys = new uint[KeyCount];
        ulongKeys = new ulong[KeyCount];
        guidKeys = new Guid[KeyCount];

        byte[] guidBytes = new byte[16];
        for (int i = 0; i < KeyCount; i++)
        {
            intKeys[i] = rand.Next(1, int.MaxValue);
            longKeys[i] = ((long)rand.Next() << 32) | (uint)rand.Next();
            uintKeys[i] = (uint)rand.Next(1, int.MaxValue);
            ulongKeys[i] = ((ulong)(uint)rand.Next() << 32) | (uint)rand.Next();
            rand.NextBytes(guidBytes);
            guidKeys[i] = new Guid(guidBytes);
        }
    }

    /// <summary>
    /// Hashes every key through <typeparamref name="THasher"/>, XOR-folding the codes into a single
    /// returned value so the JIT cannot elide the loop. The hasher is a stateless struct, instantiated
    /// once via <c>default</c> outside the loop, matching how the collections hold theirs.
    /// </summary>
    private static int HashAll<T, THasher>(T[] keys) where THasher : struct, IHashProvider<T>
    {
        THasher hasher = default;
        int acc = 0;
        for (int i = 0; i < keys.Length; i++)
        {
            acc ^= hasher.Hash(keys[i]);
        }
        return acc;
    }

    // ---- Int32 ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Int32")]
    public int Int32_Bcl()
    {
        int[] k = intKeys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= k[i].GetHashCode();
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Int32")]
    public int Int32_WangNaive() => HashAll<int, Int32WangNaiveHasher>(intKeys);

    [Benchmark]
    [BenchmarkCategory("Int32")]
    public int Int32_Wang() => HashAll<int, Int32WangHasher>(intKeys);

    [Benchmark]
    [BenchmarkCategory("Int32")]
    public int Int32_Murmur3() => HashAll<int, Int32Murmur3Hasher>(intKeys);

    // ---- Int64 ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Int64")]
    public int Int64_Bcl()
    {
        long[] k = longKeys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= k[i].GetHashCode();
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Int64")]
    public int Int64_WangNaive() => HashAll<long, Int64WangNaiveHasher>(longKeys);

    [Benchmark]
    [BenchmarkCategory("Int64")]
    public int Int64_Wang() => HashAll<long, Int64WangHasher>(longKeys);

    [Benchmark]
    [BenchmarkCategory("Int64")]
    public int Int64_Murmur3() => HashAll<long, Int64Murmur3Hasher>(longKeys);

    // ---- UInt32 ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("UInt32")]
    public int UInt32_Bcl()
    {
        uint[] k = uintKeys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= k[i].GetHashCode();
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("UInt32")]
    public int UInt32_Default() => HashAll<uint, UInt32Hasher>(uintKeys);

    [Benchmark]
    [BenchmarkCategory("UInt32")]
    public int UInt32_Wang() => HashAll<uint, UInt32WangHasher>(uintKeys);

    [Benchmark]
    [BenchmarkCategory("UInt32")]
    public int UInt32_Murmur3() => HashAll<uint, UInt32Murmur3Hasher>(uintKeys);

    // ---- UInt64 ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("UInt64")]
    public int UInt64_Bcl()
    {
        ulong[] k = ulongKeys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= k[i].GetHashCode();
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("UInt64")]
    public int UInt64_Default() => HashAll<ulong, UInt64Hasher>(ulongKeys);

    [Benchmark]
    [BenchmarkCategory("UInt64")]
    public int UInt64_Wang() => HashAll<ulong, UInt64WangHasher>(ulongKeys);

    [Benchmark]
    [BenchmarkCategory("UInt64")]
    public int UInt64_WangNaive() => HashAll<ulong, UInt64WangNaiveHasher>(ulongKeys);

    // ---- Guid ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Guid")]
    public int Guid_Bcl()
    {
        Guid[] k = guidKeys;
        int acc = 0;
        for (int i = 0; i < k.Length; i++)
        {
            acc ^= k[i].GetHashCode();
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Guid")]
    public int Guid_Celerity() => HashAll<Guid, GuidHasher>(guidKeys);
}
