using BenchmarkDotNet.Attributes;
using Celerity.Sentinel;

/// <summary>
/// The categorical win of <see cref="StringAbuseTracker"/>: producing a top-offenders report plus a
/// distinct-key estimate over a high-cardinality request stream in a <em>fixed</em> footprint, versus
/// the exact approach a developer writes — a <see cref="Dictionary{TKey,TValue}"/> frequency table and a
/// <see cref="HashSet{T}"/> of distinct keys, then an <c>O(n log n)</c> sort to rank the top few.
/// </summary>
/// <remarks>
/// The exact approach's memory grows with the number of <em>distinct</em> keys (a few MB at 100k, and
/// unbounded as an attacker rotates through unique tokens), so it eventually OOMs; the sketch tracker's
/// four fixed sketches stay flat (~2&#160;MB) regardless of cardinality. The <c>Allocated</c> column and
/// its crossover are the story — a fixed cost that is <em>heavier</em> at low cardinality but a small
/// fraction of the exact cost at high cardinality. Sentinel trades per-element CPU (a keyed hash plus
/// four sketch updates) for that constant memory and an <c>O(k)</c> top-k report, so it is the tool when
/// the exact approach would exhaust memory — not when you have the RAM to hold every key. Both sides
/// produce the same answer (top-5 offenders + distinct count), so the comparison is apples-to-apples.
/// Isolated, allocation-heavy baseline, so it lives in the extended suite.
/// </remarks>
[MemoryDiagnoser]
public class SentinelBenchmark
{
    [Params(10_000, 100_000)]
    public int DistinctKeys;

    private string[] stream = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A flood of distinct "rotating" keys with one persistent heavy hitter interleaved in.
        var list = new List<string>(DistinctKeys + DistinctKeys / 20 + 1);
        for (int k = 0; k < DistinctKeys; k++)
        {
            list.Add($"tok-{k:x}");
            if (k % 20 == 0)
                list.Add("attacker-token");
        }

        var arr = list.ToArray();
        var rand = new Random(42);
        for (int j = arr.Length - 1; j > 0; j--)
        {
            int s = rand.Next(j + 1);
            (arr[j], arr[s]) = (arr[s], arr[j]);
        }

        stream = arr;
    }

    // Exact: frequency table + distinct set, then a sort for the top-5. Memory grows with distinct keys.
    [Benchmark(Baseline = true)]
    public long Exact_DictionaryReport()
    {
        var freq = new Dictionary<string, int>();
        var seen = new HashSet<string>();
        foreach (var key in stream)
        {
            freq[key] = freq.GetValueOrDefault(key) + 1;
            seen.Add(key);
        }

        long acc = seen.Count;
        foreach (var kv in freq.OrderByDescending(kv => kv.Value).Take(5))
            acc += kv.Value;
        return acc;
    }

    // Sketch: four fixed-size sketches; O(k) top report; distinct via HyperLogLog. Flat memory.
    [Benchmark]
    public long Sketch_AbuseTrackerReport()
    {
        var tracker = new StringAbuseTracker();
        foreach (var key in stream)
            tracker.Observe(key);

        long acc = tracker.EstimateDistinctKeys();
        foreach (var offender in tracker.Snapshot(5).Offenders)
            acc += offender.EstimatedCount;
        return acc;
    }
}
