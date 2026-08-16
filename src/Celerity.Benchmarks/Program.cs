using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

internal class Program
{
    // ── CI-tracked core suite ───────────────────────────────────────────────────
    // The lean, fast, low-variance set run on every PR by the `--ci` path and pushed
    // to gh-pages for continuous regression tracking. Keep this list tight: every
    // entry is benchmarked twice (PR + base) on the same runner, so additions here
    // directly lengthen CI and widen the A/B comparison surface.
    private static readonly Type[] CoreBenchmarks =
    {
        typeof(CelerityDictionaryBenchmark),
        typeof(RobinHoodDictionaryBenchmark),
        typeof(SwissDictionaryBenchmark),
        typeof(HashCachingDictionaryBenchmark),
        typeof(PooledCelerityDictionaryBenchmark),
        typeof(IntDictionaryBenchmark),
        typeof(LongDictionaryBenchmark),
        typeof(FrozenCelerityDictionaryBenchmark),
        typeof(CelerityMultiMapBenchmark),
        typeof(CelerityMultiSetBenchmark),
        typeof(SmallDictionaryBenchmark),
        typeof(EnumMapBenchmark),
        typeof(CeleritySetBenchmark),
        typeof(SwissSetBenchmark),
        typeof(RobinHoodSetBenchmark),
        typeof(HashCachingSetBenchmark),
        typeof(PooledCeleritySetBenchmark),
        typeof(FrozenCeleritySetBenchmark),
        typeof(IntSetBenchmark),
        typeof(LongSetBenchmark),
        typeof(SmallSetBenchmark),
        typeof(EnumSetBenchmark),
        typeof(SparseSetBenchmark),
        typeof(CompressedIntSetBenchmark),
        typeof(BloomFilterBenchmark),
        typeof(CuckooFilterBenchmark),
        typeof(XorFilterBenchmark),
        typeof(BitSetBenchmark),
        typeof(RankSelectBitVectorBenchmark),
        typeof(HyperLogLogBenchmark),
        typeof(CountMinSketchBenchmark),
        typeof(TopKSketchBenchmark),
        typeof(LruCacheBenchmark),
        typeof(DequeBenchmark),
        typeof(DisjointSetBenchmark),
        typeof(IndexedPriorityQueueBenchmark),
        typeof(TrieBenchmark),
        typeof(StringInternTableBenchmark),
        typeof(FenwickTreeBenchmark),
        typeof(SegmentTreeBenchmark),
        typeof(KdTreeBenchmark),
        typeof(RTreeBenchmark),
        typeof(IntervalTreeBenchmark),
        typeof(BTreeDictionaryBenchmark),
        typeof(BTreeSetBenchmark),
        typeof(RadixSortBenchmark),
        typeof(CountingSortBenchmark),
        typeof(PartialSortBenchmark),
        typeof(StringKeyProbeBenchmark),
        typeof(SortedSpanBenchmark),
        typeof(StringHasherBenchmark),
        typeof(IntegerHasherBenchmark),
    };

    // ── Extended suite (local / on-demand) ──────────────────────────────────────
    // Heavier, more exploratory benchmarks: multi-distribution sweeps, million-item
    // scale tests, concurrency fan-out, allocation profiling, and mixed workloads.
    // These are too slow and/or too noisy for the per-PR regression gate, so they are
    // NOT part of the `--ci` joined run. Run them locally via the interactive switcher
    // or a filter, e.g.:
    //   dotnet run -c Release -- --filter '*LargeDataset*'
    //   dotnet run -c Release -- --filter '*Distribution*'
    private static readonly Type[] ExtendedBenchmarks =
    {
        typeof(DistributionBenchmark),
        typeof(AdversarialHasherBenchmark),
        typeof(HasherEndToEndBenchmark),
        typeof(LargeDatasetBenchmark),
        typeof(MemoryAllocationBenchmark),
        typeof(ConcurrentAccessBenchmark),
        typeof(CacheLocalityBenchmark),
        typeof(LibraryComparisonBenchmark),
        typeof(RealWorldWorkloadBenchmark),
        typeof(FastModBenchmark),
        typeof(PrngBenchmark),
        typeof(VarIntBenchmark),
        typeof(BitPackingBenchmark),
        typeof(CountDigitsBenchmark),
        typeof(GuidBenchmark),
        typeof(SpanBitsBenchmark),
        typeof(SimdReductionsBenchmark),
        typeof(BranchlessBenchmark),
        typeof(SpaceFillingCurveBenchmark),
        typeof(SpaceFillingCurveLocalityBenchmark),
        typeof(EnsureCapacityBenchmark),
        typeof(KdTreeShapeBenchmark),
        typeof(RTreeShapeBenchmark),

        // "Built with Celerity" showcase packages (separate NuGet packages built on the core family):
        // isolated perf demos, so they ride the extended suite alongside the primitive benchmarks.
        typeof(RingBenchmark),
        typeof(SentinelBenchmark),
        typeof(CardinalityBenchmark),
    };

    static void Main(string[] args)
    {
        if (args.Contains("--hash-quality"))
        {
            // Offline distribution-quality report (deterministic; no BenchmarkDotNet run).
            // The throughput companion to this is the hasher benchmarks above.
            HashQualityReportRunner.Run();
            return;
        }

        if (args.Contains("--probe-analysis"))
        {
            // Offline end-to-end probe-length / collision report (deterministic; no BenchmarkDotNet
            // run). Drives each hasher through the real open-addressed linear-probing placement; the
            // throughput companion is HasherEndToEndBenchmark in the extended suite.
            ProbeAnalysisReportRunner.Run();
            return;
        }

        if (args.Contains("--ci-extended"))
        {
            // Weekly extended mode: run the heavy extended suite and emit a joined
            // JSON report for github-action-benchmark, published to the separate
            // dev/bench-extended dashboard. Driven by benchmarks-extended.yml on a
            // cron schedule — intentionally NOT part of the per-PR --ci gate.
            new BenchmarkSwitcher(ExtendedBenchmarks).RunAllJoined(new ExtendedCiConfig());
        }
        else if (args.Contains("--ci"))
        {
            // CI mode: run the core suite and emit a single joined JSON report
            // that github-action-benchmark can consume (BenchmarkRun-joined-report-full.json).
            //
            // Optional sharding for the parallel CI matrix: `--shard <total> <index>` runs
            // only this shard's slice of the core suite (greedy-balanced by case count), so
            // each runner measures a fraction of the suite at FULL accuracy (CiConfig is
            // unchanged) and the aggregate job merges the per-shard JSON reports. Without
            // `--shard` the whole suite runs in one process, exactly as before.
            var slice = SelectShard(CoreBenchmarks, args);

            // `--shard-classes-out <path>` records the slice this process resolved, and
            // `--shard-classes <path>` replays one. The workflow writes the head slice and
            // replays it for the base, which is what makes shard *i* the same slice on both
            // sides of the A/B even when the PR adds a benchmark class.
            WriteShardClasses(slice, args);

            // Resolve the slice and stop, without measuring anything. The shard packing is
            // otherwise only observable by running the suite for hours, which is what let
            // the head/base mismatch above go unnoticed for as long as it did.
            if (args.Contains("--shard-dry-run"))
            {
                Console.WriteLine($"Shard resolves to {slice.Length} class(es): {string.Join(", ", slice.Select(t => t.Name))}");
                return;
            }

            if (slice.Length == 0)
            {
                Console.WriteLine("No benchmark classes selected for this shard — nothing to run.");
                return;
            }

            new BenchmarkSwitcher(slice).RunAllJoined(new CiConfig());
        }
        else
        {
            // Local mode: expose every benchmark (core + extended) to the switcher,
            // forwarding args (interactive prompt if none given).
            var all = CoreBenchmarks.Concat(ExtendedBenchmarks).ToArray();
            new BenchmarkSwitcher(all).Run(args);
        }
    }

    // Select this shard's slice of the suite for the parallel CI matrix. `--shard
    // <total> <index>` partitions the suite across `total` runners; without it the
    // whole suite is returned. Partitioning is by benchmark class (never by case),
    // which preserves the workflow's same-runner A/B invariant: a class's PR-head and
    // main-base measurements always run together on the one shard runner, so hardware
    // variance still cancels per benchmark.
    private static Type[] SelectShard(Type[] benchmarks, string[] args)
    {
        // `--shard-classes <path>` replays a slice resolved by an earlier process instead
        // of packing one. Greedy bin-packing is a function of the whole class list, so the
        // PR head and the `main` base — which differ by exactly the classes the PR adds —
        // pack *differently*, and shard `i` was not the same slice on the two sides. That
        // let a job draw a light head slice and a heavy base slice and overrun its budget
        // even when every individual slice was well inside it. Replaying the head's
        // slice makes the base a subset of the head by construction, so the pair is bounded
        // by twice the head slice and shard `i` compares like with like.
        string? replayPath = ArgumentValue(args, "--shard-classes");
        if (replayPath is not null)
        {
            // Walk the file in order rather than filtering the suite by a set of names: the
            // head wrote its classes in the order the packer produced, and running the base
            // in a different order would reintroduce, in miniature, the systematic head/base
            // difference this whole handoff exists to remove.
            var byName = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var type in benchmarks)
            {
                byName[type.Name] = type;
            }

            var replayed = new List<Type>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in File.ReadLines(replayPath).Select(line => line.Trim()).Where(line => line.Length > 0))
            {
                if (!seen.Add(name))
                {
                    continue;
                }

                // A class the replaying side does not have is simply absent — that is the PR
                // head's newly added benchmark, which `main` cannot measure and which the
                // comparison therefore reports as new rather than as a delta.
                if (byName.TryGetValue(name, out var type))
                {
                    replayed.Add(type);
                }
                else
                {
                    Console.WriteLine($"Shard class '{name}' is not in this side's core suite — skipping it.");
                }
            }

            return replayed.ToArray();
        }

        int i = Array.IndexOf(args, "--shard");
        if (i < 0)
        {
            return benchmarks;
        }

        if (i + 2 >= args.Length
            || !int.TryParse(args[i + 1], out int total)
            || !int.TryParse(args[i + 2], out int index)
            || total < 1 || index < 0 || index >= total)
        {
            throw new ArgumentException("--shard requires <total> <index> with total >= 1 and 0 <= index < total.");
        }

        // Greedy longest-processing-time bin-packing: place the heaviest classes first,
        // each onto the currently-lightest shard, so wall time is balanced across shards
        // (round-robin would dump the 75-case StringHasher onto one runner).
        var buckets = new List<Type>[total];
        for (int b = 0; b < total; b++)
        {
            buckets[b] = new List<Type>();
        }

        var loads = new long[total];
        foreach (var type in benchmarks.OrderByDescending(CaseCount))
        {
            int lightest = 0;
            for (int b = 1; b < total; b++)
            {
                if (loads[b] < loads[lightest])
                {
                    lightest = b;
                }
            }

            buckets[lightest].Add(type);
            loads[lightest] += CaseCount(type);
        }

        return buckets[index].ToArray();
    }

    // Record the resolved slice so the other side of the A/B can replay it verbatim.
    private static void WriteShardClasses(Type[] slice, string[] args)
    {
        string? path = ArgumentValue(args, "--shard-classes-out");
        if (path is null)
        {
            return;
        }

        File.WriteAllLines(path, slice.Select(t => t.Name));
        Console.WriteLine($"Wrote {slice.Length} shard class name(s) to {path}.");
    }

    // The value following a flag, or null when the flag is absent. Throws when the flag is
    // present with nothing after it, so a mistyped CI invocation fails loudly rather than
    // silently measuring the wrong slice.
    private static string? ArgumentValue(string[] args, string flag)
    {
        int i = Array.IndexOf(args, flag);
        if (i < 0)
        {
            return null;
        }

        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"{flag} requires a file path.");
        }

        return args[i + 1];
    }

    // Approximate number of benchmark cases a class contributes: [Benchmark] methods
    // times the product of its [Params] cardinalities. A heuristic for shard balancing,
    // not an exact schedule, so [Arguments]-driven cases are simply undercounted.
    private static int CaseCount(Type type)
    {
        int methods = type.GetMethods()
            .Count(m => m.GetCustomAttribute<BenchmarkAttribute>() != null);

        int combos = 1;
        foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
        {
            var p = member.GetCustomAttribute<ParamsAttribute>();
            if (p?.Values is { Length: > 0 })
            {
                combos *= p.Values.Length;
            }
        }

        return Math.Max(1, methods) * combos;
    }
}
