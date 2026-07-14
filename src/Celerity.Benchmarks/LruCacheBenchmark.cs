using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// LruCache<int, int, ...> vs the idiomatic .NET LRU (a Dictionary keyed to LinkedList nodes over a
// LinkedList of entries). Both give O(1) get/put with least-recently-used eviction; the difference is
// allocation and locality. The classic LRU heap-allocates a LinkedListNode per insertion and threads
// its recency order through pointers scattered across the managed heap, whereas LruCache threads an
// intrusive doubly-linked list through fixed-size arrays allocated once at construction, so its hot
// get/put/evict path allocates nothing. The [MemoryDiagnoser] Allocated column is the headline on the
// Put category: sustained eviction churn (the classic "memoize the last N results" workload) allocates
// a fresh node per insert in the baseline and zero in LruCache. The baseline is named `Dictionary_*` so
// the dashboard classifies it as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LruCacheBenchmark
{
    private const int Capacity = 1024;

    private int[] hitKeys = null!;      // keys resident in a warm cache (the whole capacity window)
    private int[] missKeys = null!;     // keys never inserted
    private int[] churnKeys = null!;    // a long stream of fresh keys that forces continuous eviction

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
            churnKeys[i] = warm + 1 + i; // disjoint from the warm set, all distinct -> every insert evicts
        }

        lru = new LruCache<int, int, Int32WangHasher>(Capacity);
        classic = new ClassicLru(Capacity);
        for (int i = 0; i < warm; i++)
        {
            lru[hitKeys[i]] = hitKeys[i];
            classic.Put(hitKeys[i], hitKeys[i]);
        }
    }

    // Put is the eviction-churn write path: a stream of never-seen keys into a full cache, so every
    // insert drops the current LRU and installs a fresh entry. Each benchmark rebuilds a warm cache in
    // its own [IterationSetup] so the churn always runs against a full window.
    [IterationSetup(Target = nameof(Dictionary_Put))]
    public void ResetClassicForPut()
    {
        classic = new ClassicLru(Capacity);
        int warm = Math.Min(Capacity, ItemCount);
        for (int i = 0; i < warm; i++)
            classic.Put(hitKeys[i], hitKeys[i]);
    }

    [IterationSetup(Target = nameof(LruCache_Put))]
    public void ResetLruForPut()
    {
        lru = new LruCache<int, int, Int32WangHasher>(Capacity);
        int warm = Math.Min(Capacity, ItemCount);
        for (int i = 0; i < warm; i++)
            lru[hitKeys[i]] = hitKeys[i];
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
    public void LruCache_Put()
    {
        foreach (int k in churnKeys)
            lru[k] = k;
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
    public long LruCache_Get()
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
    public int LruCache_GetMissing()
    {
        int misses = 0;
        foreach (int k in missKeys)
            if (!lru.TryGet(k, out _)) misses++;
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
                LinkedListNode<KeyValuePair<int, int>> lru = _order.Last!;
                _order.RemoveLast();
                _map.Remove(lru.Value.Key);
            }
            _map[key] = _order.AddFirst(new KeyValuePair<int, int>(key, value));
        }
    }
}
