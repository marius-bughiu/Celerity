using Celerity.Hashing;

/// <summary>
/// Offline probe-length / collision report for the integer hashers, driven through the same
/// open-addressed linear-probing placement the Celerity collections use, printed as GitHub-flavored
/// markdown so the measured numbers can be pasted into <c>docs/performance.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// Run with <c>dotnet run -c Release -- --probe-analysis</c>. This is the deterministic, timing-free
/// half of the end-to-end hasher story (the throughput half is <see cref="HasherEndToEndBenchmark"/>):
/// it sweeps every integer hasher across all four <see cref="Distribution"/> shapes and reports the
/// average probe length, the worst-case probe length, and the open-addressing collision rate via
/// <see cref="ProbeStatisticsEvaluator"/>. Because probe geometry is a pure function of (hasher, keys,
/// table size) it does not depend on CI hardware or timing noise, so unlike the throughput numbers it
/// is stable enough to cite directly in the docs.
/// </para>
/// <para>
/// The headline this report makes visible: on uniform / sequential keys the cheap hashers keep the
/// average probe near 1 (any mixing is wasted), while on the adversarial shape the naive XOR-fold's
/// worst-case probe length explodes and only the full Wang / Murmur3 finalizers hold it down — the
/// exact case where a strong hasher "loses" the isolated microbench but wins end-to-end.
/// </para>
/// </remarks>
public static class ProbeAnalysisReportRunner
{
    // Capped at MaxAdversarialCount (0xFFFF) so the adversarial shape is buildable, and large enough
    // for the clustering to show. The collections' default 0.75 load factor sizes the table.
    private const int KeyCount = 10_000;

    public static void Run()
    {
        Console.WriteLine("# End-to-end probe analysis");
        Console.WriteLine();
        Console.WriteLine($"{KeyCount} keys per shape, placed into an open-addressed power-of-two table at the " +
                          "default 0.75 load factor with linear probing — exactly as the Celerity collections " +
                          "store keys. Avg / max probe length are the number of slots a successful lookup reads; " +
                          "1.00 is ideal. Lower is better.");
        Console.WriteLine();

        foreach (Distribution distribution in new[]
                 {
                     Distribution.Uniform, Distribution.Sequential, Distribution.Clustered, Distribution.Adversarial,
                 })
        {
            int[] i32 = KeyDistributions.Int32(distribution, KeyCount);
            var i32Rows = new List<Row>();
            EvalI32<Int32IdentityHasher>("Int32IdentityHasher", i32, i32Rows);
            EvalI32<Int32WangNaiveHasher>("Int32WangNaiveHasher", i32, i32Rows);
            EvalI32<Int32WangHasher>("Int32WangHasher", i32, i32Rows);
            EvalI32<Int32Murmur3Hasher>("Int32Murmur3Hasher", i32, i32Rows);
            PrintTable($"int hashers · {distribution}", i32Rows);

            long[] i64 = KeyDistributions.Int64(distribution, KeyCount);
            var i64Rows = new List<Row>();
            EvalI64<Int64IdentityHasher>("Int64IdentityHasher", i64, i64Rows);
            EvalI64<Int64WangNaiveHasher>("Int64WangNaiveHasher", i64, i64Rows);
            EvalI64<Int64WangHasher>("Int64WangHasher", i64, i64Rows);
            EvalI64<Int64Murmur3Hasher>("Int64Murmur3Hasher", i64, i64Rows);
            PrintTable($"long hashers · {distribution}", i64Rows);
        }
    }

    private readonly record struct Row(string Name, ProbeStatistics Stats);

    private static void EvalI32<T>(string name, int[] keys, List<Row> rows) where T : struct, IHashProvider<int>
        => rows.Add(new Row(name, ProbeStatisticsEvaluator.Evaluate<int, T>(keys)));

    private static void EvalI64<T>(string name, long[] keys, List<Row> rows) where T : struct, IHashProvider<long>
        => rows.Add(new Row(name, ProbeStatisticsEvaluator.Evaluate<long, T>(keys)));

    private static void PrintTable(string title, List<Row> rows)
    {
        // Shortest average probe first (best end-to-end behaviour).
        rows.Sort((a, b) => a.Stats.AverageProbeLength.CompareTo(b.Stats.AverageProbeLength));
        Console.WriteLine($"## {title}");
        Console.WriteLine();
        Console.WriteLine("| Hasher | Avg probe | Max probe | Collision rate |");
        Console.WriteLine("|---|---|---|---|");
        foreach (Row row in rows)
        {
            ProbeStatistics s = row.Stats;
            Console.WriteLine($"| `{row.Name}` | {s.AverageProbeLength:0.00} | {s.MaxProbeLength} | {s.CollisionRate:P1} |");
        }
        Console.WriteLine();
    }
}
