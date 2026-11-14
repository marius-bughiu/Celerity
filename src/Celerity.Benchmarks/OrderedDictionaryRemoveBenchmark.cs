using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// The removal <see cref="LinkedDictionaryBenchmark"/> cannot chart: draining .NET 9's
/// <see cref="OrderedDictionary{TKey, TValue}"/> against
/// <see cref="LinkedDictionary{TKey, TValue, THasher}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OrderedDictionary{TKey, TValue}"/> keeps its entries in one dense array, so <c>Remove</c> shifts
/// every later entry down a slot: <c>O(n)</c> a removal, and <c>O(n²)</c> for a drain. <see cref="LinkedDictionary{TKey, TValue, THasher}"/> unlinks the entry and frees its
/// slot in <c>O(1)</c>. The workload is the core class's Remove arm, on the same keys and the same shuffled order.
/// </para>
/// <para>
/// The two sizes are a decade apart so the shape is visible: the ratio should grow about tenfold between them.
/// The core suite's 100,000 keys are not measured, because one drain of that size takes seconds and a
/// single-invocation benchmark is warmed for about a hundred invocations before it is measured. That is the
/// reason this arm lives in the extended suite rather than on the dashboard card.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class OrderedDictionaryRemoveBenchmark
{
    private int[] keys = null!;          // ItemCount distinct keys, in insertion order
    private int[] removeOrder = null!;   // the same keys, shuffled
    private int linkedCapacity;          // the bucket count that holds ItemCount entries without growing

    private OrderedDictionary<int, int> ordered = null!;
    private LinkedDictionary<int, int, Int32WangNaiveHasher> linked = null!;

    [Params(1000, 10_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        // The same key stream and removal order as LinkedDictionaryBenchmark, so the 1,000-key LinkedDictionary
        // arm measures exactly the workload the core class charts.
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

        linkedCapacity = FastUtils.MinTableSizeFor(ItemCount, 0.75f);
    }

    [IterationSetup(Target = nameof(OrderedDictionary_Remove))]
    public void SetupForOrderedRemove()
    {
        ordered = new OrderedDictionary<int, int>(ItemCount);
        foreach (var key in keys)
        {
            ordered[key] = key;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void OrderedDictionary_Remove()
    {
        foreach (var key in removeOrder)
        {
            ordered.Remove(key);
        }
    }

    [IterationSetup(Target = nameof(LinkedDictionary_Remove))]
    public void SetupForLinkedRemove()
    {
        linked = new LinkedDictionary<int, int, Int32WangNaiveHasher>(linkedCapacity);
        foreach (var key in keys)
        {
            linked[key] = key;
        }
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
}
