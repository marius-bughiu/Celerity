using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// SieveCache<int, int, ...> vs the idiomatic .NET LRU (a Dictionary keyed to LinkedList nodes over a
// LinkedList of entries) — the cache a caller writes today when the BCL offers none. SIEVE replaces
// the policy as well as the storage: a hit sets one visited flag instead of relinking a node, and an
// eviction sweeps a hand over an insertion-ordered queue. Get is the headline, since that is where the
// two policies do different work per operation; LruCache_CrossGet adds Celerity's own array-backed LRU
// to the same category, under an op name of its own so the dashboard keeps exactly two series per card.
// Put is eviction churn into a full cache (the [MemoryDiagnoser] Allocated column is zero for SIEVE
// and a LinkedListNode per insert for the baseline). ZipfWorkload is the policy-sensitive arm: a
// get-then-put-on-miss loop over a seeded Zipf(1.0) stream, where each side pays for its own misses,
// so it measures speed and hit ratio together. The baseline is named `Dictionary_*` so the dashboard
// classifies it as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SieveCacheBenchmark
{
    private const int Capacity = 1024;
    private const int ZipfKeys = 100_000;

    private int[] hitKeys = null!;      // keys resident in a warm cache (the whole capacity window)
    private int[] missKeys = null!;     // keys never inserted
    private int[] churnKeys = null!;    // a long stream of fresh keys that forces continuous eviction
    private int[] zipfWarmup = null!;   // primes the caches before each ZipfWorkload iteration
    private int[] zipfTrace = null!;    // the measured Zipf request stream

    private SieveCache<int, int, Int32WangHasher> sieve = null!;
    private LruCache<int, int, Int32WangHasher> lru = null!;
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

        sieve = new SieveCache<int, int, Int32WangHasher>(Capacity);
        lru = new LruCache<int, int, Int32WangHasher>(Capacity);
        classic = new ClassicLru(Capacity);
        for (int i = 0; i < warm; i++)
        {
            sieve[hitKeys[i]] = hitKeys[i];
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

    [IterationSetup(Target = nameof(SieveCache_Put))]
    public void ResetSieveForPut()
    {
        sieve = new SieveCache<int, int, Int32WangHasher>(Capacity);
        int warm = Math.Min(Capacity, ItemCount);
        for (int i = 0; i < warm; i++)
            sieve[hitKeys[i]] = hitKeys[i];
    }

    [IterationSetup(Target = nameof(Dictionary_ZipfWorkload))]
    public void ResetClassicForZipf()
    {
        classic = new ClassicLru(Capacity);
        foreach (int k in zipfWarmup)
            if (!classic.TryGet(k, out _)) classic.Put(k, k);
    }

    [IterationSetup(Target = nameof(SieveCache_ZipfWorkload))]
    public void ResetSieveForZipf()
    {
        sieve = new SieveCache<int, int, Int32WangHasher>(Capacity);
        foreach (int k in zipfWarmup)
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
    public void SieveCache_Put()
    {
        foreach (int k in churnKeys)
            sieve[k] = k;
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
    public long SieveCache_Get()
    {
        long acc = 0;
        foreach (int k in hitKeys)
            if (sieve.TryGet(k, out int v)) acc += v;
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
    public int SieveCache_GetMissing()
    {
        int misses = 0;
        foreach (int k in missKeys)
            if (!sieve.TryGet(k, out _)) misses++;
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
    public int SieveCache_ZipfWorkload()
    {
        int misses = 0;
        foreach (int k in zipfTrace)
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
