using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// BiDictionary<int, int, ...> vs the bidirectional map every .NET caller writes today: a pair of
// Dictionary<int, int> instances, one per direction, kept in sync by hand. Every write touches both
// dictionaries, and a removal by key has to read the value back out of the forward map to know which
// inverse entry to drop. BiDictionary keeps both directions in one entry array threaded by two chain
// families, so each key and value is stored once.
//
// Values are `key ^ ValueMask`: XOR with a constant is a bijection on int, so distinct keys give
// distinct values and neither side ever rejects a duplicate. Insert builds the whole map (the pair does
// two Adds per entry); Lookup is a forward hit against a single Dictionary lookup; InverseLookup is
// TryGetKey against the inverse Dictionary; Remove takes every key in a shuffled order; Enumerate walks
// every pair. Both sides are pre-sized for ItemCount entries, so Insert measures inserts, not growth.
// The baseline is named `Dictionary_*` so the dashboard classifies it as the BCL reference.
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BiDictionaryBenchmark
{
    private const int ValueMask = 0x5bd1e995;

    private int[] keys = null!;          // ItemCount distinct keys, in insertion order
    private int[] values = null!;        // values[i] = keys[i] ^ ValueMask, distinct because the keys are
    private int[] removeOrder = null!;   // the same keys, shuffled
    private int biCapacity;              // the bucket count that holds ItemCount entries without growing

    private Dictionary<int, int> forward = null!;
    private Dictionary<int, int> inverse = null!;
    private BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher> bi = null!;

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

        values = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            values[i] = keys[i] ^ ValueMask;
        }

        removeOrder = (int[])keys.Clone();
        rand.Shuffle(removeOrder);

        // The constructor takes a bucket count; this is the smallest one whose 0.75 load factor
        // holds ItemCount entries.
        biCapacity = FastUtils.MinTableSizeFor(ItemCount, 0.75f);

        (forward, inverse) = NewPair();
        bi = NewBi();
    }

    private (Dictionary<int, int> Forward, Dictionary<int, int> Inverse) NewPair()
    {
        var f = new Dictionary<int, int>(ItemCount);
        var r = new Dictionary<int, int>(ItemCount);
        for (int i = 0; i < keys.Length; i++)
        {
            f.Add(keys[i], values[i]);
            r.Add(values[i], keys[i]);
        }
        return (f, r);
    }

    private BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher> NewBi()
    {
        var map = new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(biCapacity);
        for (int i = 0; i < keys.Length; i++)
        {
            map.Add(keys[i], values[i]);
        }
        return map;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public void Dictionary_Insert()
    {
        var f = new Dictionary<int, int>(ItemCount);
        var r = new Dictionary<int, int>(ItemCount);

        for (int i = 0; i < keys.Length; i++)
        {
            f.Add(keys[i], values[i]);
            r.Add(values[i], keys[i]);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public void BiDictionary_Insert()
    {
        var map = new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(biCapacity);

        for (int i = 0; i < keys.Length; i++)
        {
            map.Add(keys[i], values[i]);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int Dictionary_Lookup()
    {
        int sum = 0;
        foreach (var key in keys)
        {
            forward.TryGetValue(key, out int value);
            sum += value;
        }
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int BiDictionary_Lookup()
    {
        int sum = 0;
        foreach (var key in keys)
        {
            bi.TryGetValue(key, out int value);
            sum += value;
        }
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("InverseLookup")]
    public int Dictionary_InverseLookup()
    {
        int sum = 0;
        foreach (var value in values)
        {
            inverse.TryGetValue(value, out int key);
            sum += key;
        }
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("InverseLookup")]
    public int BiDictionary_InverseLookup()
    {
        int sum = 0;
        foreach (var value in values)
        {
            bi.TryGetKey(value, out int key);
            sum += key;
        }
        return sum;
    }

    [IterationSetup(Target = nameof(Dictionary_Remove))]
    public void SetupForDictionaryRemove()
    {
        (forward, inverse) = NewPair();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void Dictionary_Remove()
    {
        foreach (var key in removeOrder)
        {
            if (forward.Remove(key, out int value))
                inverse.Remove(value);
        }
    }

    [IterationSetup(Target = nameof(BiDictionary_Remove))]
    public void SetupForBiDictionaryRemove()
    {
        bi = NewBi();
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void BiDictionary_Remove()
    {
        foreach (var key in removeOrder)
        {
            bi.Remove(key);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long Dictionary_Enumerate()
    {
        long sum = 0;
        foreach (var kv in forward)
        {
            sum += kv.Key + kv.Value;
        }
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long BiDictionary_Enumerate()
    {
        long sum = 0;
        foreach (var kv in bi)
        {
            sum += kv.Key + kv.Value;
        }
        return sum;
    }
}
