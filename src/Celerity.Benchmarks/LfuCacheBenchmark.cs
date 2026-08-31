using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// LfuCache<int, int, ...> vs the idiomatic .NET LFU (a Dictionary from key to its value/frequency/stamp
// triple, plus a SortedSet ordered by that triple so the eviction victim is the set's minimum). Both
// give least-frequently-used eviction with a least-recently-used tie-break; the differences are the
// complexity class and the allocation. The idiomatic version pays O(log n) per operation — every hit
// removes and reinserts a set node because the key's frequency changed — and allocates a red-black
// tree node per insertion, whereas LfuCache keeps its frequency buckets and their entry chains in
// fixed-size arrays allocated once at construction, so its policy bookkeeping is O(1) worst case
// rather than logarithmic and the hot get/put/evict path allocates nothing. (End to end an operation
// is expected O(1), not worst-case: it also probes the open-addressed key index, whose probe length
// grows on clustered keys like every other hash-backed collection here.) The [MemoryDiagnoser] Allocated column is the headline on the
// Put category: sustained eviction churn allocates a fresh tree node per insert in the baseline and
// zero in LfuCache. Get is where the complexity gap shows up on its own, since a hit is a pure
// frequency bump — two constant-time relinks here against a remove-plus-reinsert there. The baseline is
// named `Dictionary_*` so the dashboard classifies it as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LfuCacheBenchmark
{
    private const int Capacity = 1024;

    private int[] hitKeys = null!;      // keys resident in a warm cache (the whole capacity window)
    private int[] missKeys = null!;     // keys never inserted
    private int[] churnKeys = null!;    // a long stream of fresh keys that forces continuous eviction

    private LfuCache<int, int, Int32WangHasher> lfu = null!;
    private ClassicLfu classic = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        int warm = Math.Min(Capacity, ItemCount);
        hitKeys = new int[warm];
        missKeys = new int[ItemCount];
        churnKeys = new int[ItemCount];

        var rand = new Random(42);
        for (int i = 0; i < warm; i++)
            hitKeys[i] = i + 1; // 1..warm
        for (int i = 0; i < ItemCount; i++)
        {
            missKeys[i] = rand.Next(int.MaxValue / 2, int.MaxValue);
            // Disjoint from the warm set and all distinct, so an insert can only evict, never update.
            // At ItemCount=1000 the warm set is 1000 against a Capacity of 1024, so the first 24 puts
            // fill the remaining slots before the churn becomes eviction-for-eviction; that is 2.4% of
            // the arm, it lands on both implementations identically, and the shape is inherited from
            // LruCacheBenchmark so the two stay directly comparable.
            churnKeys[i] = warm + 1 + i;
        }

        lfu = new LfuCache<int, int, Int32WangHasher>(Capacity);
        classic = new ClassicLfu(Capacity);
        for (int i = 0; i < warm; i++)
        {
            lfu[hitKeys[i]] = hitKeys[i];
            classic.Put(hitKeys[i], hitKeys[i]);
        }
    }

    // Put is the eviction-churn write path: a stream of never-seen keys into a full cache, so every
    // insert drops the current victim and installs a fresh entry. Each benchmark rebuilds a warm cache
    // in its own [IterationSetup] so the churn always runs against a full window.
    [IterationSetup(Target = nameof(Dictionary_Put))]
    public void ResetClassicForPut()
    {
        classic = new ClassicLfu(Capacity);
        int warm = Math.Min(Capacity, ItemCount);
        for (int i = 0; i < warm; i++)
            classic.Put(hitKeys[i], hitKeys[i]);
    }

    [IterationSetup(Target = nameof(LfuCache_Put))]
    public void ResetLfuForPut()
    {
        lfu = new LfuCache<int, int, Int32WangHasher>(Capacity);
        int warm = Math.Min(Capacity, ItemCount);
        for (int i = 0; i < warm; i++)
            lfu[hitKeys[i]] = hitKeys[i];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Put")]
    public void Dictionary_Put()
    {
        foreach (int k in churnKeys)
            classic.Put(k, k);
    }

    [Benchmark]
    [BenchmarkCategory("Put")]
    public void LfuCache_Put()
    {
        foreach (int k in churnKeys)
            lfu[k] = k;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Get")]
    public long Dictionary_Get()
    {
        long acc = 0;
        foreach (int k in hitKeys)
            if (classic.TryGet(k, out int v)) acc += v;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Get")]
    public long LfuCache_Get()
    {
        long acc = 0;
        foreach (int k in hitKeys)
            if (lfu.TryGet(k, out int v)) acc += v;
        return acc;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("GetMissing")]
    public int Dictionary_GetMissing()
    {
        int misses = 0;
        foreach (int k in missKeys)
            if (!classic.TryGet(k, out _)) misses++;
        return misses;
    }

    [Benchmark]
    [BenchmarkCategory("GetMissing")]
    public int LfuCache_GetMissing()
    {
        int misses = 0;
        foreach (int k in missKeys)
            if (!lfu.TryGet(k, out _)) misses++;
        return misses;
    }

    // The idiomatic .NET LFU: a Dictionary from key to (value, frequency, last-use stamp), plus a
    // SortedSet over (frequency, stamp, key) whose Min is exactly the eviction victim — lowest
    // frequency, and among equals the least recently used. Every use changes the frequency, so it has
    // to leave and re-enter the set.
    private sealed class ClassicLfu
    {
        private readonly int _capacity;
        private readonly Dictionary<int, (int Value, long Freq, long Seq)> _map;
        private readonly SortedSet<(long Freq, long Seq, int Key)> _order = new();
        private long _clock;

        public ClassicLfu(int capacity)
        {
            _capacity = capacity;
            _map = new Dictionary<int, (int, long, long)>(capacity);
        }

        public bool TryGet(int key, out int value)
        {
            if (_map.TryGetValue(key, out (int Value, long Freq, long Seq) entry))
            {
                _order.Remove((entry.Freq, entry.Seq, key));
                long seq = ++_clock;
                _map[key] = (entry.Value, entry.Freq + 1, seq);
                _order.Add((entry.Freq + 1, seq, key));
                value = entry.Value;
                return true;
            }
            value = 0;
            return false;
        }

        public void Put(int key, int value)
        {
            if (_map.TryGetValue(key, out (int Value, long Freq, long Seq) entry))
            {
                _order.Remove((entry.Freq, entry.Seq, key));
                long updated = ++_clock;
                _map[key] = (value, entry.Freq + 1, updated);
                _order.Add((entry.Freq + 1, updated, key));
                return;
            }

            if (_map.Count == _capacity)
            {
                (long Freq, long Seq, int Key) victim = _order.Min;
                _order.Remove(victim);
                _map.Remove(victim.Key);
            }

            long seq = ++_clock;
            _map[key] = (value, 1, seq);
            _order.Add((1, seq, key));
        }
    }
}
