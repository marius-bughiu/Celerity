using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// LinkedSet<int, ...> vs the idiomatic insertion-ordered set a net8.0 caller writes today: a
// Dictionary keyed to the nodes of a LinkedList of elements. (HashSet<T> only keeps insertion order
// until the first removal, and a HashSet beside a List has an O(n) Remove, so neither is a fair
// stand-in.) Both sides keep insertion order through an O(1) Remove; they differ in where the order
// lives. The baseline allocates a LinkedListNode per element and chases heap pointers; LinkedSet
// threads its order through the same entry array the hash chains index, so Add allocates nothing
// beyond the pre-sized table. Contains looks up every present element. Remove takes every element in
// a shuffled order. Enumerate runs over a set that has had half its elements removed, so the cost of
// walking the order through a holey entry array is visible. Both sides are pre-sized for ItemCount
// elements (LinkedList has no capacity), so Add measures inserts, not growth. The baseline is named
// `Dictionary_*` so the dashboard classifies it as the BCL reference.
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LinkedSetBenchmark
{
    private int[] keys = null!;          // ItemCount distinct elements, in insertion order
    private int[] removeOrder = null!;   // the same elements, shuffled
    private int linkedCapacity;          // the bucket count that holds ItemCount elements without growing

    private ClassicOrderedSet classic = null!;
    private LinkedSet<int, Int32WangNaiveHasher> linked = null!;

    // Enumerate's sets: ItemCount adds, then every other element of removeOrder removed.
    private ClassicOrderedSet classicHalf = null!;
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

        classic = NewClassic();
        linked = NewLinked();

        classicHalf = NewClassic();
        linkedHalf = NewLinked();
        for (int i = 0; i < removeOrder.Length; i += 2)
        {
            classicHalf.Remove(removeOrder[i]);
            linkedHalf.Remove(removeOrder[i]);
        }
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

    private LinkedSet<int, Int32WangNaiveHasher> NewLinked()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(linkedCapacity);
        foreach (var key in keys)
        {
            set.Add(key);
        }
        return set;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void Dictionary_Add()
    {
        var set = new ClassicOrderedSet(ItemCount);

        foreach (var key in keys)
        {
            set.Add(key);
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
    public int Dictionary_Contains()
    {
        int hits = 0;
        foreach (var key in keys)
        {
            if (classic.Contains(key))
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

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long Dictionary_Enumerate()
    {
        long sum = 0;
        foreach (var item in classicHalf.Order)
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

    // The ordered set a caller builds from BCL parts: the map finds an element's node, the list
    // keeps the order, and Remove is O(1).
    private sealed class ClassicOrderedSet
    {
        private readonly Dictionary<int, LinkedListNode<int>> _map;
        private readonly LinkedList<int> _order = new();

        public ClassicOrderedSet(int capacity)
        {
            _map = new Dictionary<int, LinkedListNode<int>>(capacity);
        }

        public LinkedList<int> Order => _order;

        public bool Contains(int item) => _map.ContainsKey(item);

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
