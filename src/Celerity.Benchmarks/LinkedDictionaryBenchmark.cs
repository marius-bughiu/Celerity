using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// LinkedDictionary<int, int, ...> vs the insertion-ordered maps the BCL offers. Insert, Lookup and Enumerate run
// against .NET 9's generic OrderedDictionary<TKey, TValue>, the BCL's own insertion-ordered map. Remove runs against
// the ordered map a caller builds from BCL parts when removal must stay O(1): a Dictionary keyed to the nodes of a
// LinkedList of entries. OrderedDictionary is not a stand-in for that arm. It keeps its entries in one dense array,
// so its Remove shifts every later entry down and costs O(n), and draining 100,000 keys that way takes seconds per
// invocation, more than this suite's schedule can spend on one card. OrderedDictionaryRemoveBenchmark, in the
// extended suite, measures that drain at sizes where it fits. (The non-generic
// System.Collections.Specialized.OrderedDictionary boxes every key and value and finds a key to remove by linear
// scan, so it is no contest.)
//
// LinkedDictionary threads its order through the same entry array the hash chains index, so Insert allocates
// nothing beyond the pre-sized table and Remove unlinks an entry in O(1). Remove takes every key in a shuffled
// order. Enumerate runs over a dictionary that has had half its keys removed, so the cost of walking the order
// through a holey entry array is visible. OrderedDictionary compacts on every removal, so its walk is always a
// dense array. Every side is pre-sized for ItemCount entries (LinkedList has no capacity), so Insert measures
// inserts, not growth. The baselines are named `OrderedDictionary_*` and `Dictionary_*` so the dashboard
// classifies them as the BCL reference.
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LinkedDictionaryBenchmark
{
    private int[] keys = null!;          // ItemCount distinct keys, in insertion order
    private int[] removeOrder = null!;   // the same keys, shuffled
    private int linkedCapacity;          // the bucket count that holds ItemCount entries without growing

    private OrderedDictionary<int, int> ordered = null!;
    private LinkedDictionary<int, int, Int32WangNaiveHasher> linked = null!;
    private ClassicOrderedDictionary classic = null!;   // Remove's baseline, rebuilt per iteration

    // Enumerate's dictionaries: ItemCount inserts, then every other key of removeOrder removed.
    private OrderedDictionary<int, int> orderedHalf = null!;
    private LinkedDictionary<int, int, Int32WangNaiveHasher> linkedHalf = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        var seen = new HashSet<int>(ItemCount);
        Random rand = new(42);
        for (int i = 0; i < ItemCount;)
        {
            int key = rand.Next(1, int.MaxValue);
            if (seen.Add(key))
                keys[i++] = key;
        }

        removeOrder = (int[])keys.Clone();
        rand.Shuffle(removeOrder);

        // The constructor takes a bucket count; this is the smallest one whose 0.75 load factor
        // holds ItemCount entries.
        linkedCapacity = FastUtils.MinTableSizeFor(ItemCount, 0.75f);

        ordered = NewOrdered();
        linked = NewLinked();

        linkedHalf = NewLinked();
        for (int i = 0; i < removeOrder.Length; i += 2)
        {
            linkedHalf.Remove(removeOrder[i]);
        }
    }

    // OrderedDictionary's half-removed table is built only for the arm that reads it: each of its ItemCount / 2
    // removals is O(n), which costs seconds at 100,000 keys, and every other arm's process would pay for it.
    [GlobalSetup(Target = nameof(OrderedDictionary_Enumerate))]
    public void SetupForOrderedEnumerate()
    {
        Setup();
        orderedHalf = NewOrdered();
        for (int i = 0; i < removeOrder.Length; i += 2)
        {
            orderedHalf.Remove(removeOrder[i]);
        }
    }

    private OrderedDictionary<int, int> NewOrdered()
    {
        var map = new OrderedDictionary<int, int>(ItemCount);
        foreach (var key in keys)
        {
            map[key] = key;
        }
        return map;
    }

    private LinkedDictionary<int, int, Int32WangNaiveHasher> NewLinked()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(linkedCapacity);
        foreach (var key in keys)
        {
            map[key] = key;
        }
        return map;
    }

    private ClassicOrderedDictionary NewClassic()
    {
        var map = new ClassicOrderedDictionary(ItemCount);
        foreach (var key in keys)
        {
            map.Set(key, key);
        }
        return map;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public void OrderedDictionary_Insert()
    {
        var map = new OrderedDictionary<int, int>(ItemCount);

        foreach (var key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public void LinkedDictionary_Insert()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(linkedCapacity);

        foreach (var key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int OrderedDictionary_Lookup()
    {
        int sum = 0;
        foreach (var key in keys)
        {
            ordered.TryGetValue(key, out int value);
            sum += value;
        }
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int LinkedDictionary_Lookup()
    {
        int sum = 0;
        foreach (var key in keys)
        {
            linked.TryGetValue(key, out int value);
            sum += value;
        }
        return sum;
    }

    [IterationSetup(Target = nameof(Dictionary_Remove))]
    public void SetupForDictionaryRemove()
    {
        classic = NewClassic();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void Dictionary_Remove()
    {
        foreach (var key in removeOrder)
        {
            classic.Remove(key);
        }
    }

    [IterationSetup(Target = nameof(LinkedDictionary_Remove))]
    public void SetupForLinkedDictionaryRemove()
    {
        linked = NewLinked();
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void LinkedDictionary_Remove()
    {
        foreach (var key in removeOrder)
        {
            linked.Remove(key);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long OrderedDictionary_Enumerate()
    {
        long sum = 0;
        foreach (var kv in orderedHalf)
        {
            sum += kv.Key + kv.Value;
        }
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long LinkedDictionary_Enumerate()
    {
        long sum = 0;
        foreach (var kv in linkedHalf)
        {
            sum += kv.Key + kv.Value;
        }
        return sum;
    }

    // The ordered map a caller builds from BCL parts when removal must stay O(1): the map finds a key's node,
    // the list keeps the order, and both Remove and an in-place overwrite are O(1).
    private sealed class ClassicOrderedDictionary
    {
        private readonly Dictionary<int, LinkedListNode<KeyValuePair<int, int>>> _map;
        private readonly LinkedList<KeyValuePair<int, int>> _order = new();

        public ClassicOrderedDictionary(int capacity)
        {
            _map = new Dictionary<int, LinkedListNode<KeyValuePair<int, int>>>(capacity);
        }

        public void Set(int key, int value)
        {
            if (_map.TryGetValue(key, out LinkedListNode<KeyValuePair<int, int>>? node))
            {
                node.Value = new KeyValuePair<int, int>(key, value);
                return;
            }
            _map[key] = _order.AddLast(new KeyValuePair<int, int>(key, value));
        }

        public bool Remove(int key)
        {
            if (_map.Remove(key, out LinkedListNode<KeyValuePair<int, int>>? node))
            {
                _order.Remove(node);
                return true;
            }
            return false;
        }
    }
}
