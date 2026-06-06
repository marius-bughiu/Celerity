using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// A mixed, read-heavy operation stream meant to look like a real cache / index
/// rather than a single-operation micro-benchmark: ~80% lookups, ~12% writes
/// (split between updating existing keys and inserting new ones), and ~8% removes,
/// with key selection skewed toward a hot 10% of the key space (a Zipfian-ish
/// access pattern typical of caches and session stores).
/// </summary>
/// <remarks>
/// <para>
/// The whole <see cref="OpCount"/>-long operation stream is precomputed in
/// <see cref="GlobalSetup"/> so the timed loop is pure dictionary work — no RNG, no
/// branching on freshly-drawn values. Each method gets a per-iteration
/// <see cref="IterationSetupAttribute"/> that rebuilds its dictionary from the base
/// key set, so every measured replay starts from the same state (the setup cost is
/// excluded from the timing).
/// </para>
/// <para>
/// Lookups use <c>TryGetValue</c> because the stream deliberately removes keys that
/// later operations may touch — a realistic mix of hits and misses, not an
/// all-hits idealization.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RealWorldWorkloadBenchmark
{
    private const byte OpGet = 0;
    private const byte OpSet = 1;
    private const byte OpRemove = 2;

    private int[] baseKeys = null!;
    private byte[] opKinds = null!;
    private int[] opKeys = null!;

    private Dictionary<int, int> dictionary = null!;
    private IntDictionary<int> intDictionary = null!;
    private CelerityDictionary<int, int, Int32WangNaiveHasher> celerityDictionary = null!;

    [Params(100_000)]
    public int ItemCount;

    [Params(500_000)]
    public int OpCount;

    [GlobalSetup]
    public void Setup()
    {
        baseKeys = KeyDistributions.Int32(Distribution.Uniform, ItemCount);

        // A pool of fresh keys, guaranteed distinct from the base set, to feed the
        // insert operations.
        var baseSet = new HashSet<int>(baseKeys);
        int freshCount = Math.Max(1, ItemCount / 5);
        var fresh = new int[freshCount];
        Random rand = new(123);
        for (int i = 0; i < freshCount;)
        {
            int candidate = rand.Next(1, int.MaxValue);
            if (baseSet.Add(candidate))
            {
                fresh[i++] = candidate;
            }
        }

        opKinds = new byte[OpCount];
        opKeys = new int[OpCount];
        int hotCount = Math.Max(1, ItemCount / 10);
        int freshPtr = 0;

        for (int i = 0; i < OpCount; i++)
        {
            double r = rand.NextDouble();
            if (r < 0.80)
            {
                opKinds[i] = OpGet;
                opKeys[i] = PickHotBiased(rand, hotCount);
            }
            else if (r < 0.92)
            {
                opKinds[i] = OpSet;
                opKeys[i] = rand.Next(2) == 0
                    ? baseKeys[rand.Next(ItemCount)]          // update an existing key
                    : fresh[freshPtr++ % fresh.Length];       // insert a new key
            }
            else
            {
                opKinds[i] = OpRemove;
                // Remove from the cold tail so the hot set stays mostly resident.
                opKeys[i] = baseKeys[rand.Next(hotCount, ItemCount)];
            }
        }
    }

    private int PickHotBiased(Random rand, int hotCount)
        => rand.NextDouble() < 0.70
            ? baseKeys[rand.Next(hotCount)]
            : baseKeys[rand.Next(ItemCount)];

    [IterationSetup(Target = nameof(Dictionary_Workload))]
    public void ResetDictionary()
    {
        dictionary = new Dictionary<int, int>(ItemCount);
        foreach (var key in baseKeys)
        {
            dictionary[key] = key;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Workload")]
    public long Dictionary_Workload()
    {
        long acc = 0;
        for (int i = 0; i < opKinds.Length; i++)
        {
            int key = opKeys[i];
            switch (opKinds[i])
            {
                case OpGet:
                    if (dictionary.TryGetValue(key, out int v)) acc += v;
                    break;
                case OpSet:
                    dictionary[key] = key;
                    break;
                default:
                    dictionary.Remove(key);
                    break;
            }
        }
        return acc;
    }

    [IterationSetup(Target = nameof(IntDictionary_Workload))]
    public void ResetIntDictionary()
    {
        intDictionary = new IntDictionary<int>(ItemCount);
        foreach (var key in baseKeys)
        {
            intDictionary[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Workload")]
    public long IntDictionary_Workload()
    {
        long acc = 0;
        for (int i = 0; i < opKinds.Length; i++)
        {
            int key = opKeys[i];
            switch (opKinds[i])
            {
                case OpGet:
                    if (intDictionary.TryGetValue(key, out int v)) acc += v;
                    break;
                case OpSet:
                    intDictionary[key] = key;
                    break;
                default:
                    intDictionary.Remove(key);
                    break;
            }
        }
        return acc;
    }

    [IterationSetup(Target = nameof(CelerityDictionary_Workload))]
    public void ResetCelerityDictionary()
    {
        celerityDictionary = new CelerityDictionary<int, int, Int32WangNaiveHasher>(ItemCount);
        foreach (var key in baseKeys)
        {
            celerityDictionary[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Workload")]
    public long CelerityDictionary_Workload()
    {
        long acc = 0;
        for (int i = 0; i < opKinds.Length; i++)
        {
            int key = opKeys[i];
            switch (opKinds[i])
            {
                case OpGet:
                    if (celerityDictionary.TryGetValue(key, out int v)) acc += v;
                    break;
                case OpSet:
                    celerityDictionary[key] = key;
                    break;
                default:
                    celerityDictionary.Remove(key);
                    break;
            }
        }
        return acc;
    }
}
