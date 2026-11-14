using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// LinkedSet<int, ...> vs the insertion-ordered sets a caller can build from the BCL, which ships none ready-made.
// Add, Contains and Enumerate run against .NET 9's OrderedDictionary<TKey, TValue> used as a set: keyed by the
// element, with a byte value nobody reads, the idiom ConcurrentDictionary<T, byte> made familiar. It keeps
// insertion order through removals and does everything these three arms ask of a set, so it is a fair stand-in for
// them; the value slot is part of what a caller using it pays. Remove runs against the ordered set a caller builds
// when removal must stay O(1): a Dictionary keyed to the nodes of a LinkedList of elements. OrderedDictionary's
// Remove shifts every later entry down, O(n), which is no stand-in for that arm (see LinkedDictionaryBenchmark).
// (HashSet<T> only keeps insertion order until the first removal, and a HashSet beside a List has an O(n) Remove,
// so neither is a fair stand-in for any of them. A KeyedCollection<T, T> subclass is another route to an ordered
// set; it is a list underneath, so it removes in O(n) too.)
//
// LinkedSet threads its order through the same entry array the hash chains index, so Add allocates nothing beyond
// the pre-sized table and Remove unlinks an entry in O(1). Contains looks up every present element. Remove takes
// every element in a shuffled order. Enumerate runs over a set that has had half its elements removed, so the
// cost of walking the order through a holey entry array is visible; OrderedDictionary compacts on every removal,
// so its walk is always a dense array. Every side is pre-sized for ItemCount elements (LinkedList has no
// capacity), so Add measures inserts, not growth. The baselines are named `OrderedDictionary_*` and
// `Dictionary_*` so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LinkedSetBenchmark
{
    private int[] keys = null!;          // ItemCount distinct elements, in insertion order
    private int[] removeOrder = null!;   // the same elements, shuffled
    private int linkedCapacity;          // the bucket count that holds ItemCount elements without growing

    private OrderedDictionary<int, byte> ordered = null!;
    private LinkedSet<int, Int32WangNaiveHasher> linked = null!;
    private ClassicOrderedSet classic = null!;   // Remove's baseline, rebuilt per iteration

    // Enumerate's sets: ItemCount adds, then every other element of removeOrder removed.
    private OrderedDictionary<int, byte> orderedHalf = null!;
    private LinkedSet<int, Int32WangNaiveHasher> linkedHalf = null!;

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
        // holds ItemCount elements.
        linkedCapacity = FastUtils.MinTableSizeFor(ItemCount, 0.75f);

        ordered = NewOrdered();
        linked = NewLinked();

        linkedHalf = NewLinked();
        for (int i = 0; i < removeOrder.Length; i += 2)
        {
            linkedHalf.Remove(removeOrder[i]);
        }
    }

    // OrderedDictionary's half-removed set is built only for the arm that reads it: each of its ItemCount / 2
    // removals is O(n), which costs seconds at 100,000 elements, and every other arm's process would pay for it.
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

    private OrderedDictionary<int, byte> NewOrdered()
    {
        var set = new OrderedDictionary<int, byte>(ItemCount);
        foreach (var key in keys)
        {
            set.TryAdd(key, 0);
        }
        return set;
    }

    private LinkedSet<int, Int32WangNaiveHasher> NewLinked()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(linkedCapacity);
        foreach (var key in keys)
        {
            set.Add(key);
        }
        return set;
    }

    private ClassicOrderedSet NewClassic()
    {
        var set = new ClassicOrderedSet(ItemCount);
        foreach (var key in keys)
        {
            set.Add(key);
        }
        return set;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void OrderedDictionary_Add()
    {
        var set = new OrderedDictionary<int, byte>(ItemCount);

        foreach (var key in keys)
        {
            set.TryAdd(key, 0);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void LinkedSet_Add()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(linkedCapacity);

        foreach (var key in keys)
        {
            set.Add(key);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public int OrderedDictionary_Contains()
    {
        int hits = 0;
        foreach (var key in keys)
        {
            if (ordered.ContainsKey(key))
                hits++;
        }
        return hits;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public int LinkedSet_Contains()
    {
        int hits = 0;
        foreach (var key in keys)
        {
            if (linked.Contains(key))
                hits++;
        }
        return hits;
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

    [IterationSetup(Target = nameof(LinkedSet_Remove))]
    public void SetupForLinkedSetRemove()
    {
        linked = NewLinked();
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void LinkedSet_Remove()
    {
        foreach (var key in removeOrder)
        {
            linked.Remove(key);
        }
    }

    // A caller using the dictionary as a set walks its Keys view, whose struct enumerator reads the dense entry
    // array in index order.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long OrderedDictionary_Enumerate()
    {
        long sum = 0;
        foreach (var item in orderedHalf.Keys)
        {
            sum += item;
        }
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long LinkedSet_Enumerate()
    {
        long sum = 0;
        foreach (var item in linkedHalf)
        {
            sum += item;
        }
        return sum;
    }

    // The ordered set a caller builds from BCL parts when removal must stay O(1): the map finds an element's
    // node, the list keeps the order, and Remove is O(1).
    private sealed class ClassicOrderedSet
    {
        private readonly Dictionary<int, LinkedListNode<int>> _map;
        private readonly LinkedList<int> _order = new();

        public ClassicOrderedSet(int capacity)
        {
            _map = new Dictionary<int, LinkedListNode<int>>(capacity);
        }

        public bool Add(int item)
        {
            if (_map.ContainsKey(item))
                return false;
            _map[item] = _order.AddLast(item);
            return true;
        }

        public bool Remove(int item)
        {
            if (_map.Remove(item, out LinkedListNode<int>? node))
            {
                _order.Remove(node);
                return true;
            }
            return false;
        }
    }
}
