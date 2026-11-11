using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Celerity.Collections;
using Celerity.Hashing;

// SlidingHyperLogLog<int, ...> vs the exact way to answer "how many distinct keys since t?": a
// Dictionary<int, long> of each key's latest timestamp, counted by a scan over its values.
//
// Both sides are warm, steady-state windows built in GlobalSetup over ItemCount distinct keys, and
// both stay that size: every Add overwrites an existing key's timestamp (the dictionary through one
// GetValueRefOrAddDefault lookup, so the baseline pays no double hash), and the shared timestamp
// counter keeps rising across invocations exactly as a live stream's clock would. Neither side
// allocates in the measured region, so the [MemoryDiagnoser] column reads 0 for both — the memory
// story is retained size, which it does not report: the dictionary holds ~28 bytes per distinct key (entry plus bucket)
// and grows with the window, the sketch holds a fixed ~1.7 MB at the default precision whatever the
// window holds.
//
// - Add: ingest ItemCount events (cycling through the key space) into the warm window.
// - CountWindow: the distinct count over the most recent half of the timeline. The dictionary must
//   visit every key; the sketch visits its 4,096 registers, so it loses at small windows and wins
//   once the window holds more distinct keys than that.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class SlidingHyperLogLogBenchmark
{
    private int[] keys = null!;
    private Dictionary<int, long> lastSeen = null!;
    private SlidingHyperLogLog<int, Int32WangNaiveHasher> sketch = null!;
    private long clock;
    private long windowStart;

    [Params(1000, 100_000, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        lastSeen = new Dictionary<int, long>(ItemCount);
        sketch = new SlidingHyperLogLog<int, Int32WangNaiveHasher>();

        // Distinct keys, so the window really holds ItemCount of them and "half the timeline" is
        // exactly half the keys.
        Random rand = new(42);
        var drawn = new HashSet<int>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
        {
            int key;
            do key = rand.Next(1, int.MaxValue / 2); while (!drawn.Add(key));
            keys[i] = key;
        }

        // Fill the window with every key at its own timestamp, so the "most recent half" query below
        // genuinely selects half the keys.
        for (int i = 0; i < ItemCount; i++)
        {
            long t = clock++;
            lastSeen[keys[i]] = t;
            sketch.Add(keys[i], t);
        }

        windowStart = clock - ItemCount / 2;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public long Dictionary_Add()
    {
        Dictionary<int, long> map = lastSeen;
        long t = clock;
        foreach (int key in keys)
            CollectionsMarshal.GetValueRefOrAddDefault(map, key, out _) = t++;
        clock = t;
        return t;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public long SlidingHyperLogLog_Add()
    {
        SlidingHyperLogLog<int, Int32WangNaiveHasher> s = sketch;
        long t = clock;
        foreach (int key in keys)
            s.Add(key, t++);
        clock = t;
        return t;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CountWindow")]
    public int Dictionary_CountWindow()
    {
        long since = windowStart;
        int count = 0;
        foreach (long t in lastSeen.Values)
        {
            if (t >= since)
                count++;
        }

        return count;
    }

    [Benchmark]
    [BenchmarkCategory("CountWindow")]
    public long SlidingHyperLogLog_CountWindow()
    {
        return sketch.EstimateCardinality(windowStart);
    }
}
