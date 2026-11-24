using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// TinyLfuCache<int, int, ...> (W-TinyLFU) vs the idiomatic .NET LRU (a Dictionary keyed to LinkedList
// nodes over a LinkedList of entries) — the cache a caller writes today when the BCL offers none. Get,
// GetMissing and Put are the same arms as SieveCacheBenchmark's, so the two dashboards compare directly:
// a hit relinks a node in one of three segments and bumps four sketch counters, and an insert into a full
// cache compares two sketch estimates. ZipfWorkload is a get-then-put-on-miss loop over a seeded Zipf(1.0)
// stream, so each side pays for its own misses. LoopWorkload is the policy's reason to exist: a cyclic
// pass over 1.5x the capacity, where an LRU evicts every key just before it is requested again and so
// misses every request, while TinyLFU's frequency sketch keeps most of the loop resident.
// SieveCache_CrossLoopWorkload adds Celerity's SIEVE cache to that category under an op name of its own,
// so the dashboard keeps exactly two series per card while the BDN table shows all three, and
// LruCache_CrossGet does the same for Celerity's LRU on the Get arm. The baseline is named `Dictionary_*`
// so the dashboard classifies it as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class TinyLfuCacheBenchmark
{
    private const int Capacity = 1024;
    private const int ZipfKeys = 100_000;

    private int[] hitKeys = null!;      // keys resident in a warm cache (the whole capacity window)
    private int[] missKeys = null!;     // keys never inserted
    private int[] churnKeys = null!;    // a long stream of fresh keys that forces continuous eviction
    private int[] zipfWarmup = null!;   // primes the caches before each ZipfWorkload iteration
    private int[] zipfTrace = null!;    // the measured Zipf request stream
    private int[] loopWarmup = null!;   // primes the caches before each LoopWorkload iteration
    private int[] loopTrace = null!;    // the measured cyclic request stream

    private TinyLfuCache<int, int, Int32WangHasher> tiny = null!;
    private LruCache<int, int, Int32WangHasher> lru = null!;
    private SieveCache<int, int, Int32WangHasher> sieve = null!;
    private ClassicLru classic = null!;

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
            // Same shape as LruCacheBenchmark, so the two stay directly comparable.
            churnKeys[i] = warm + 1 + i;
        }

        zipfWarmup = Zipf(100_000, seed: 7);
        zipfTrace = Zipf(ItemCount, seed: 542);

        // One cyclic key sequence over 1.5x the capacity, disjoint from every other key set here. The
        // warm-up runs it long enough for the sketch to learn it; the measured trace continues it.
        const int LoopSpan = Capacity * 3 / 2;
        loopWarmup = new int[100_000];
        for (int i = 0; i < loopWarmup.Length; i++)
            loopWarmup[i] = 50_000_000 + i % LoopSpan;
        loopTrace = new int[ItemCount];
        for (int i = 0; i < loopTrace.Length; i++)
            loopTrace[i] = 50_000_000 + (loopWarmup.Length + i) % LoopSpan;

        tiny = new TinyLfuCache<int, int, Int32WangHasher>(Capacity);
        lru = new LruCache<int, int, Int32WangHasher>(Capacity);
        sieve = new SieveCache<int, int, Int32WangHasher>(Capacity);
        classic = new ClassicLru(Capacity);
        for (int i = 0; i < warm; i++)
        {
            tiny[hitKeys[i]] = hitKeys[i];
            lru[hitKeys[i]] = hitKeys[i];
            classic.Put(hitKeys[i], hitKeys[i]);
        }
    }

    // Inverse-CDF Zipf(1.0) sampler over ZipfKeys keys: key i has weight 1 / (i + 1).
    private static int[] Zipf(int requests, int seed)
    {
        var cdf = new double[ZipfKeys];
        double sum = 0;
        for (int i = 0; i < ZipfKeys; i++)
        {
            sum += 1.0 / (i + 1);
            cdf[i] = sum;
        }

        var rng = new Random(seed);
        var trace = new int[requests];
        for (int r = 0; r < requests; r++)
        {
            int idx = Array.BinarySearch(cdf, rng.NextDouble() * sum);
            trace[r] = idx >= 0 ? idx : Math.Min(~idx, ZipfKeys - 1);
        }
        return trace;
    }

    // Put and ZipfWorkload mutate the cache, so each rebuilds its starting state in its own
    // [IterationSetup]: a full warm window for the churn, a Zipf-primed cache for the workload.
    [IterationSetup(Target = nameof(Dictionary_Put))]
    public void ResetClassicForPut()
    {
        classic = new ClassicLru(Capacity);
        int warm = Math.Min(Capacity, ItemCount);
        for (int i = 0; i < warm; i++)
            classic.Put(hitKeys[i], hitKeys[i]);
    }

    [IterationSetup(Target = nameof(TinyLfuCache_Put))]
    public void ResetTinyForPut()
    {
        tiny = new TinyLfuCache<int, int, Int32WangHasher>(Capacity);
        int warm = Math.Min(Capacity, ItemCount);
        for (int i = 0; i < warm; i++)
            tiny[hitKeys[i]] = hitKeys[i];
    }

    [IterationSetup(Target = nameof(Dictionary_ZipfWorkload))]
    public void ResetClassicForZipf()
    {
        classic = new ClassicLru(Capacity);
        foreach (int k in zipfWarmup)
            if (!classic.TryGet(k, out _)) classic.Put(k, k);
    }

    [IterationSetup(Target = nameof(TinyLfuCache_ZipfWorkload))]
    public void ResetTinyForZipf()
    {
        tiny = new TinyLfuCache<int, int, Int32WangHasher>(Capacity);
        foreach (int k in zipfWarmup)
            if (!tiny.TryGet(k, out _)) tiny[k] = k;
    }

    [IterationSetup(Target = nameof(Dictionary_LoopWorkload))]
    public void ResetClassicForLoop()
    {
        classic = new ClassicLru(Capacity);
        foreach (int k in loopWarmup)
            if (!classic.TryGet(k, out _)) classic.Put(k, k);
    }

    [IterationSetup(Target = nameof(TinyLfuCache_LoopWorkload))]
    public void ResetTinyForLoop()
    {
        tiny = new TinyLfuCache<int, int, Int32WangHasher>(Capacity);
        foreach (int k in loopWarmup)
            if (!tiny.TryGet(k, out _)) tiny[k] = k;
    }

    [IterationSetup(Target = nameof(SieveCache_CrossLoopWorkload))]
    public void ResetSieveForLoop()
    {
        sieve = new SieveCache<int, int, Int32WangHasher>(Capacity);
        foreach (int k in loopWarmup)
            if (!sieve.TryGet(k, out _)) sieve[k] = k;
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
    public void TinyLfuCache_Put()
    {
        foreach (int k in churnKeys)
            tiny[k] = k;
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
    public long TinyLfuCache_Get()
    {
        long acc = 0;
        foreach (int k in hitKeys)
            if (tiny.TryGet(k, out int v)) acc += v;
        return acc;
    }

    // Celerity's own LRU on the same hits. Not a dashboard series: the op name is its own, so its
    // bucket holds one value and draws no card, while the shared category keeps it in the BDN table.
    [Benchmark]
    [BenchmarkCategory("Get")]
    public long LruCache_CrossGet()
    {
        long acc = 0;
        foreach (int k in hitKeys)
            if (lru.TryGet(k, out int v)) acc += v;
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
    public int TinyLfuCache_GetMissing()
    {
        int misses = 0;
        foreach (int k in missKeys)
            if (!tiny.TryGet(k, out _)) misses++;
        return misses;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ZipfWorkload")]
    public int Dictionary_ZipfWorkload()
    {
        int misses = 0;
        foreach (int k in zipfTrace)
        {
            if (!classic.TryGet(k, out _))
            {
                misses++;
                classic.Put(k, k);
            }
        }
        return misses;
    }

    [Benchmark]
    [BenchmarkCategory("ZipfWorkload")]
    public int TinyLfuCache_ZipfWorkload()
    {
        int misses = 0;
        foreach (int k in zipfTrace)
        {
            if (!tiny.TryGet(k, out _))
            {
                misses++;
                tiny[k] = k;
            }
        }
        return misses;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LoopWorkload")]
    public int Dictionary_LoopWorkload()
    {
        int misses = 0;
        foreach (int k in loopTrace)
        {
            if (!classic.TryGet(k, out _))
            {
                misses++;
                classic.Put(k, k);
            }
        }
        return misses;
    }

    [Benchmark]
    [BenchmarkCategory("LoopWorkload")]
    public int TinyLfuCache_LoopWorkload()
    {
        int misses = 0;
        foreach (int k in loopTrace)
        {
            if (!tiny.TryGet(k, out _))
            {
                misses++;
                tiny[k] = k;
            }
        }
        return misses;
    }

    // Celerity's SIEVE cache on the same loop, which it misses as completely as an LRU. Not a dashboard
    // series: the op name is its own, so its bucket holds one value and draws no card.
    [Benchmark]
    [BenchmarkCategory("LoopWorkload")]
    public int SieveCache_CrossLoopWorkload()
    {
        int misses = 0;
        foreach (int k in loopTrace)
        {
            if (!sieve.TryGet(k, out _))
            {
                misses++;
                sieve[k] = k;
            }
        }
        return misses;
    }

    // The idiomatic .NET LRU: a Dictionary from key to its node in a recency LinkedList (front = MRU).
    // Get/Put move the touched node to the front; an insert at capacity drops the back node.
    private sealed class ClassicLru
    {
        private readonly int _capacity;
        private readonly Dictionary<int, LinkedListNode<KeyValuePair<int, int>>> _map;
        private readonly LinkedList<KeyValuePair<int, int>> _order = new();

        public ClassicLru(int capacity)
        {
            _capacity = capacity;
            _map = new Dictionary<int, LinkedListNode<KeyValuePair<int, int>>>(capacity);
        }

        public bool TryGet(int key, out int value)
        {
            if (_map.TryGetValue(key, out LinkedListNode<KeyValuePair<int, int>>? node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
            value = 0;
            return false;
        }

        public void Put(int key, int value)
        {
            if (_map.TryGetValue(key, out LinkedListNode<KeyValuePair<int, int>>? node))
            {
                node.Value = new KeyValuePair<int, int>(key, value);
                _order.Remove(node);
                _order.AddFirst(node);
                return;
            }
            if (_map.Count == _capacity)
            {
                LinkedListNode<KeyValuePair<int, int>> lruNode = _order.Last!;
                _order.RemoveLast();
                _map.Remove(lruNode.Value.Key);
            }
            _map[key] = _order.AddFirst(new KeyValuePair<int, int>(key, value));
        }
    }
}
