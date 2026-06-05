using Celerity.Hashing;

/// <summary>
/// Offline distribution-quality report for every built-in hasher, printed as GitHub-flavored
/// markdown so the measured numbers can be pasted into <c>docs/api/hashing.md</c>.
/// </summary>
/// <remarks>
/// Run with <c>dotnet run -c Release -- --hash-quality</c>. This is the distribution half of the
/// hasher comparison: it drives <see cref="HashQualityEvaluator"/> over the <em>same</em> deterministic
/// key samples the throughput benchmarks use (<see cref="HasherKeySamples"/>), so the speed numbers
/// (from BenchmarkDotNet / the live dashboard) and these distribution numbers describe the same keys.
/// Distribution quality is deterministic — it does not depend on CI hardware or timing noise — so unlike
/// the throughput numbers it is stable enough to cite directly in the docs.
/// </remarks>
public static class HashQualityReportRunner
{
    // 2000 distinct keys over 4096 buckets ≈ 0.49 load factor — a representative healthy table.
    private const int BucketCount = 4096;

    public static void Run()
    {
        Console.WriteLine("# Hash quality report");
        Console.WriteLine();
        Console.WriteLine($"{HasherKeySamples.DefaultCount} distinct keys, {BucketCount} buckets " +
                          $"(load factor ≈ {(double)HasherKeySamples.DefaultCount / BucketCount:0.00}). " +
                          "Score 1.00 = ideal uniform; > 1.00 = clustering. Lower max-load and collisions are better.");
        Console.WriteLine();

        foreach (KeyShape shape in Enum.GetValues<KeyShape>())
        {
            string[] keys = HasherKeySamples.Strings(shape);
            var rows = new List<Row>();
            EvalStr<StringDjb2Hasher>("StringDjb2Hasher", keys, rows);
            EvalStr<StringDjb2AHasher>("StringDjb2AHasher", keys, rows);
            EvalStr<StringSdbmHasher>("StringSdbmHasher", keys, rows);
            EvalStr<StringElfHasher>("StringElfHasher", keys, rows);
            EvalStr<StringCrc32Hasher>("StringCrc32Hasher", keys, rows);
            EvalStr<StringFnV1Hasher>("StringFnV1Hasher", keys, rows);
            EvalStr<StringFnV164Hasher>("StringFnV164Hasher", keys, rows);
            EvalStr<StringFnV1AHasher>("StringFnV1AHasher", keys, rows);
            EvalStr<StringFnV1AFullHasher>("StringFnV1AFullHasher", keys, rows);
            EvalStr<StringFnV1A64Hasher>("StringFnV1A64Hasher", keys, rows);
            EvalStr<StringJenkinsOaatHasher>("StringJenkinsOaatHasher", keys, rows);
            EvalStr<StringMurmur2Hasher>("StringMurmur2Hasher", keys, rows);
            EvalStr<StringMurmur3Hasher>("StringMurmur3Hasher", keys, rows);
            EvalStr<StringXxHash32Hasher>("StringXxHash32Hasher", keys, rows);
            EvalStr<StringXxHash64Hasher>("StringXxHash64Hasher", keys, rows);
            EvalStr<StringXxHash3Hasher>("StringXxHash3Hasher", keys, rows);
            EvalStr<StringCityHash64Hasher>("StringCityHash64Hasher", keys, rows);
            EvalStr<StringMetroHash64Hasher>("StringMetroHash64Hasher", keys, rows);
            EvalStr<StringSipHash13Hasher>("StringSipHash13Hasher", keys, rows);
            EvalStr<StringSipHash24Hasher>("StringSipHash24Hasher", keys, rows);
            EvalStr<StringHalfSipHash24Hasher>("StringHalfSipHash24Hasher", keys, rows);
            EvalStr<StringHighwayHash64Hasher>("StringHighwayHash64Hasher", keys, rows);
            EvalStr<DefaultHasher<string>>("DefaultHasher<string> (BCL)", keys, rows);
            PrintTable($"string hashers · {shape}", rows);
        }

        // Integer / Guid hashers, grouped by key type.
        var i32 = new List<Row>();
        EvalI32<Int32WangNaiveHasher>("Int32WangNaiveHasher", HasherKeySamples.Int32(), i32);
        EvalI32<Int32WangHasher>("Int32WangHasher", HasherKeySamples.Int32(), i32);
        EvalI32<Int32Murmur3Hasher>("Int32Murmur3Hasher", HasherKeySamples.Int32(), i32);
        EvalI32<DefaultHasher<int>>("DefaultHasher<int> (BCL)", HasherKeySamples.Int32(), i32);
        PrintTable("int hashers", i32);

        var i64 = new List<Row>();
        EvalI64<Int64WangNaiveHasher>("Int64WangNaiveHasher", HasherKeySamples.Int64(), i64);
        EvalI64<Int64WangHasher>("Int64WangHasher", HasherKeySamples.Int64(), i64);
        EvalI64<Int64Murmur3Hasher>("Int64Murmur3Hasher", HasherKeySamples.Int64(), i64);
        EvalI64<DefaultHasher<long>>("DefaultHasher<long> (BCL)", HasherKeySamples.Int64(), i64);
        PrintTable("long hashers", i64);

        var u32 = new List<Row>();
        EvalU32<UInt32Hasher>("UInt32Hasher", HasherKeySamples.UInt32(), u32);
        EvalU32<UInt32WangHasher>("UInt32WangHasher", HasherKeySamples.UInt32(), u32);
        EvalU32<UInt32Murmur3Hasher>("UInt32Murmur3Hasher", HasherKeySamples.UInt32(), u32);
        EvalU32<DefaultHasher<uint>>("DefaultHasher<uint> (BCL)", HasherKeySamples.UInt32(), u32);
        PrintTable("uint hashers", u32);

        var u64 = new List<Row>();
        EvalU64<UInt64Hasher>("UInt64Hasher", HasherKeySamples.UInt64(), u64);
        EvalU64<UInt64WangHasher>("UInt64WangHasher", HasherKeySamples.UInt64(), u64);
        EvalU64<UInt64WangNaiveHasher>("UInt64WangNaiveHasher", HasherKeySamples.UInt64(), u64);
        EvalU64<DefaultHasher<ulong>>("DefaultHasher<ulong> (BCL)", HasherKeySamples.UInt64(), u64);
        PrintTable("ulong hashers", u64);

        var guid = new List<Row>();
        EvalGuid<GuidHasher>("GuidHasher", HasherKeySamples.Guids(), guid);
        EvalGuid<DefaultHasher<Guid>>("DefaultHasher<Guid> (BCL)", HasherKeySamples.Guids(), guid);
        PrintTable("Guid hashers", guid);
    }

    private readonly record struct Row(string Name, HashQualityReport Report);

    private static void EvalStr<T>(string name, string[] keys, List<Row> rows) where T : struct, IHashProvider<string>
        => rows.Add(new Row(name, HashQualityEvaluator.Evaluate<string, T>(keys, BucketCount)));

    private static void EvalI32<T>(string name, int[] keys, List<Row> rows) where T : struct, IHashProvider<int>
        => rows.Add(new Row(name, HashQualityEvaluator.Evaluate<int, T>(keys, BucketCount)));

    private static void EvalI64<T>(string name, long[] keys, List<Row> rows) where T : struct, IHashProvider<long>
        => rows.Add(new Row(name, HashQualityEvaluator.Evaluate<long, T>(keys, BucketCount)));

    private static void EvalU32<T>(string name, uint[] keys, List<Row> rows) where T : struct, IHashProvider<uint>
        => rows.Add(new Row(name, HashQualityEvaluator.Evaluate<uint, T>(keys, BucketCount)));

    private static void EvalU64<T>(string name, ulong[] keys, List<Row> rows) where T : struct, IHashProvider<ulong>
        => rows.Add(new Row(name, HashQualityEvaluator.Evaluate<ulong, T>(keys, BucketCount)));

    private static void EvalGuid<T>(string name, Guid[] keys, List<Row> rows) where T : struct, IHashProvider<Guid>
        => rows.Add(new Row(name, HashQualityEvaluator.Evaluate<Guid, T>(keys, BucketCount)));

    private static void PrintTable(string title, List<Row> rows)
    {
        // Best distribution first (score closest to ideal).
        rows.Sort((a, b) => a.Report.DistributionScore.CompareTo(b.Report.DistributionScore));
        Console.WriteLine($"## {title}");
        Console.WriteLine();
        Console.WriteLine("| Hasher | Score | Max load | Collisions |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var row in rows)
        {
            HashQualityReport r = row.Report;
            Console.WriteLine($"| `{row.Name}` | {r.DistributionScore:0.000} | {r.MaxBucketLoad} | {r.CollisionCount} |");
        }
        Console.WriteLine();
    }
}
