using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Fuzz;

/// <summary>
/// Raised the instant a Celerity collection's observable state diverges from its
/// BCL oracle inside a fuzz case. The message captures what differed; the case
/// seed (printed by the driver) reproduces the exact sequence.
/// </summary>
internal sealed class DivergenceException(string message) : Exception(message);

/// <summary>
/// The differential fuzz cases. Each takes a seeded <see cref="Random"/>, drives
/// the same randomized operation sequence against a Celerity collection and an
/// equivalent BCL collection, and throws <see cref="DivergenceException"/> the
/// moment they disagree. A case is a pure function of its RNG, so a single seed
/// reproduces it byte-for-byte.
/// </summary>
internal static class Differential
{
    /// <summary>Every registered fuzz target, keyed by name for <c>--target</c>.</summary>
    public static readonly (string Name, Action<Random> Run)[] All =
    [
        ("CelerityDictionary", CelerityDictionaryCase),
        ("RobinHoodDictionary", RobinHoodDictionaryCase),
        ("SwissDictionary", SwissDictionaryCase),
        ("HashCachingDictionary", HashCachingDictionaryCase),
        ("PooledCelerityDictionary", PooledCelerityDictionaryCase),
        ("IntDictionary", IntDictionaryCase),
        ("LongDictionary", LongDictionaryCase),
        ("SmallDictionary", SmallDictionaryCase),
        ("CeleritySet", CeleritySetCase),
        ("RobinHoodSet", RobinHoodSetCase),
        ("SwissSet", SwissSetCase),
        ("HashCachingSet", HashCachingSetCase),
        ("PooledCeleritySet", PooledCeleritySetCase),
        ("IntSet", IntSetCase),
        ("LongSet", LongSetCase),
        ("SmallSet", SmallSetCase),
        ("SparseSet", SparseSetCase),
        ("BTreeDictionary", BTreeDictionaryCase),
        ("BTreeSet", BTreeSetCase),
        ("CelerityMultiMap", CelerityMultiMapCase),
        ("FrozenCelerityDictionary", FrozenCase),
        ("FrozenCeleritySet", FrozenSetCase),
        ("StringInternTable", StringInternTableCase),
        ("BloomFilter", BloomFilterCase),
        ("CuckooFilter", CuckooFilterCase),
        ("XorFilter", XorFilterCase),
        ("BitSet", BitSetCase),
        ("HyperLogLog", HyperLogLogCase),
        ("CountMinSketch", CountMinSketchCase),
        ("BloomFilterMerge", BloomFilterMergeCase),
        ("CuckooFilterMerge", CuckooFilterMergeCase),
        ("HyperLogLogMerge", HyperLogLogMergeCase),
        ("CountMinSketchMerge", CountMinSketchMergeCase),
        ("BloomFilterHash64", BloomFilterHash64Case),
        ("CuckooFilterHash64", CuckooFilterHash64Case),
        ("XorFilterHash64", XorFilterHash64Case),
        ("HyperLogLogHash64", HyperLogLogHash64Case),
        ("CountMinSketchHash64", CountMinSketchHash64Case),
    ];

    private const int MinKey = -8;
    private const int MaxKey = 24;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new DivergenceException(message);
    }

    // The ordered collections pack 31 keys into a node, so the hash-table key domain above would never
    // split one. These two widen the domain and lengthen the run so a case builds two to three levels and
    // drives splits, borrows, and merges instead of staying inside a single root leaf.
    private const int MinOrderedKey = -8;
    private const int MaxOrderedKey = 300;

    private static int Key(Random rng) => rng.Next(MinKey, MaxKey + 1);
    private static int Value(Random rng) => rng.Next(0, 1000);
    private static int OpCount(Random rng) => rng.Next(0, 200);

    private static int OrderedKey(Random rng) => rng.Next(MinOrderedKey, MaxOrderedKey + 1);
    private static int OrderedOpCount(Random rng) => rng.Next(0, 800);

    // ---- key/value dictionaries --------------------------------------------

    private static void CelerityDictionaryCase(Random rng)
    {
        var sut = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        var oracle = new Dictionary<int, int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int key = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 5:
                    int v = Value(rng);
                    sut[key] = v;
                    oracle[key] = v;
                    break;
                case < 8:
                    Check(sut.Remove(key) == oracle.Remove(key), $"Remove({key}) disagreed");
                    break;
                case < 9:
                    int v2 = Value(rng);
                    Check(sut.TryAdd(key, v2) == oracle.TryAdd(key, v2), $"TryAdd({key}) disagreed");
                    break;
                default:
                    sut.Clear();
                    oracle.Clear();
                    break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
        {
            bool e = oracle.TryGetValue(k, out int ev);
            bool a = sut.TryGetValue(k, out int av);
            Check(e == a, $"TryGetValue({k}) presence {a} != {e}");
            Check(!e || ev == av, $"value[{k}] {av} != {ev}");
        }

        var seen = new Dictionary<int, int>();
        foreach (var kv in sut)
            Check(seen.TryAdd(kv.Key, kv.Value), $"enumeration yielded duplicate key {kv.Key}");
        Check(seen.Count == oracle.Count, $"enumeration count {seen.Count} != {oracle.Count}");
        foreach (var kv in oracle)
            Check(seen.TryGetValue(kv.Key, out int sv) && sv == kv.Value, $"enumeration missing/wrong {kv.Key}");
    }

    private static void RobinHoodDictionaryCase(Random rng)
    {
        var sut = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        var oracle = new Dictionary<int, int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int key = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 5:
                    int v = Value(rng);
                    sut[key] = v;
                    oracle[key] = v;
                    break;
                case < 8:
                    Check(sut.Remove(key) == oracle.Remove(key), $"Remove({key}) disagreed");
                    break;
                case < 9:
                    int v2 = Value(rng);
                    Check(sut.TryAdd(key, v2) == oracle.TryAdd(key, v2), $"TryAdd({key}) disagreed");
                    break;
                default:
                    sut.Clear();
                    oracle.Clear();
                    break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
        {
            bool e = oracle.TryGetValue(k, out int ev);
            bool a = sut.TryGetValue(k, out int av);
            Check(e == a, $"TryGetValue({k}) presence {a} != {e}");
            Check(!e || ev == av, $"value[{k}] {av} != {ev}");
        }

        var seen = new Dictionary<int, int>();
        foreach (var kv in sut)
            Check(seen.TryAdd(kv.Key, kv.Value), $"enumeration yielded duplicate key {kv.Key}");
        Check(seen.Count == oracle.Count, $"enumeration count {seen.Count} != {oracle.Count}");
        foreach (var kv in oracle)
            Check(seen.TryGetValue(kv.Key, out int sv) && sv == kv.Value, $"enumeration missing/wrong {kv.Key}");
    }

    private static void PooledCelerityDictionaryCase(Random rng)
    {
        using var sut = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        var oracle = new Dictionary<int, int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int key = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 5:
                    int v = Value(rng);
                    sut[key] = v;
                    oracle[key] = v;
                    break;
                case < 8:
                    Check(sut.Remove(key) == oracle.Remove(key), $"Remove({key}) disagreed");
                    break;
                case < 9:
                    int v2 = Value(rng);
                    Check(sut.TryAdd(key, v2) == oracle.TryAdd(key, v2), $"TryAdd({key}) disagreed");
                    break;
                default:
                    sut.Clear();
                    oracle.Clear();
                    break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
        {
            bool e = oracle.TryGetValue(k, out int ev);
            bool a = sut.TryGetValue(k, out int av);
            Check(e == a, $"TryGetValue({k}) presence {a} != {e}");
            Check(!e || ev == av, $"value[{k}] {av} != {ev}");
        }

        var seen = new Dictionary<int, int>();
        foreach (var kv in sut)
            Check(seen.TryAdd(kv.Key, kv.Value), $"enumeration yielded duplicate key {kv.Key}");
        Check(seen.Count == oracle.Count, $"enumeration count {seen.Count} != {oracle.Count}");
        foreach (var kv in oracle)
            Check(seen.TryGetValue(kv.Key, out int sv) && sv == kv.Value, $"enumeration missing/wrong {kv.Key}");
    }

    private static void SwissDictionaryCase(Random rng)
    {
        var sut = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        var oracle = new Dictionary<int, int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int key = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 5:
                    int v = Value(rng);
                    sut[key] = v;
                    oracle[key] = v;
                    break;
                case < 8:
                    Check(sut.Remove(key) == oracle.Remove(key), $"Remove({key}) disagreed");
                    break;
                case < 9:
                    int v2 = Value(rng);
                    Check(sut.TryAdd(key, v2) == oracle.TryAdd(key, v2), $"TryAdd({key}) disagreed");
                    break;
                default:
                    sut.Clear();
                    oracle.Clear();
                    break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
        {
            bool e = oracle.TryGetValue(k, out int ev);
            bool a = sut.TryGetValue(k, out int av);
            Check(e == a, $"TryGetValue({k}) presence {a} != {e}");
            Check(!e || ev == av, $"value[{k}] {av} != {ev}");
        }

        var seen = new Dictionary<int, int>();
        foreach (var kv in sut)
            Check(seen.TryAdd(kv.Key, kv.Value), $"enumeration yielded duplicate key {kv.Key}");
        Check(seen.Count == oracle.Count, $"enumeration count {seen.Count} != {oracle.Count}");
        foreach (var kv in oracle)
            Check(seen.TryGetValue(kv.Key, out int sv) && sv == kv.Value, $"enumeration missing/wrong {kv.Key}");
    }

    private static void HashCachingDictionaryCase(Random rng)
    {
        var sut = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
        var oracle = new Dictionary<int, int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int key = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 5:
                    int v = Value(rng);
                    sut[key] = v;
                    oracle[key] = v;
                    break;
                case < 8:
                    Check(sut.Remove(key) == oracle.Remove(key), $"Remove({key}) disagreed");
                    break;
                case < 9:
                    int v2 = Value(rng);
                    Check(sut.TryAdd(key, v2) == oracle.TryAdd(key, v2), $"TryAdd({key}) disagreed");
                    break;
                default:
                    sut.Clear();
                    oracle.Clear();
                    break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
        {
            bool e = oracle.TryGetValue(k, out int ev);
            bool a = sut.TryGetValue(k, out int av);
            Check(e == a, $"TryGetValue({k}) presence {a} != {e}");
            Check(!e || ev == av, $"value[{k}] {av} != {ev}");
        }

        var seen = new Dictionary<int, int>();
        foreach (var kv in sut)
            Check(seen.TryAdd(kv.Key, kv.Value), $"enumeration yielded duplicate key {kv.Key}");
        Check(seen.Count == oracle.Count, $"enumeration count {seen.Count} != {oracle.Count}");
        foreach (var kv in oracle)
            Check(seen.TryGetValue(kv.Key, out int sv) && sv == kv.Value, $"enumeration missing/wrong {kv.Key}");
    }

    private static void IntDictionaryCase(Random rng)
    {
        var sut = new IntDictionary<int>();
        var oracle = new Dictionary<int, int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int key = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 5: int v = Value(rng); sut[key] = v; oracle[key] = v; break;
                case < 8: Check(sut.Remove(key) == oracle.Remove(key), $"Remove({key})"); break;
                case < 9: int v2 = Value(rng); Check(sut.TryAdd(key, v2) == oracle.TryAdd(key, v2), $"TryAdd({key})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
        {
            bool e = oracle.TryGetValue(k, out int ev);
            bool a = sut.TryGetValue(k, out int av);
            Check(e == a && (!e || ev == av), $"lookup({k}) {a}/{av} != {e}/{ev}");
        }
    }

    private static void SmallDictionaryCase(Random rng)
    {
        var sut = new SmallDictionary<int, int>();
        var oracle = new Dictionary<int, int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int key = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 5: int v = Value(rng); sut[key] = v; oracle[key] = v; break;
                case < 8: Check(sut.Remove(key) == oracle.Remove(key), $"Remove({key})"); break;
                case < 9: int v2 = Value(rng); Check(sut.TryAdd(key, v2) == oracle.TryAdd(key, v2), $"TryAdd({key})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
        {
            bool e = oracle.TryGetValue(k, out int ev);
            bool a = sut.TryGetValue(k, out int av);
            Check(e == a && (!e || ev == av), $"lookup({k}) {a}/{av} != {e}/{ev}");
        }

        var seen = new Dictionary<int, int>();
        foreach (var kv in sut)
            Check(seen.TryAdd(kv.Key, kv.Value), $"enumeration yielded duplicate key {kv.Key}");
        Check(seen.Count == oracle.Count, $"enumeration count {seen.Count} != {oracle.Count}");
    }

    private static void LongDictionaryCase(Random rng)
    {
        var sut = new LongDictionary<int>();
        var oracle = new Dictionary<long, int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            long key = Spread(Key(rng));
            switch (rng.Next(0, 10))
            {
                case < 5: int v = Value(rng); sut[key] = v; oracle[key] = v; break;
                case < 8: Check(sut.Remove(key) == oracle.Remove(key), $"Remove({key})"); break;
                case < 9: int v2 = Value(rng); Check(sut.TryAdd(key, v2) == oracle.TryAdd(key, v2), $"TryAdd({key})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        foreach (var kv in oracle)
            Check(sut.TryGetValue(kv.Key, out int av) && av == kv.Value, $"lookup({kv.Key}) missing/wrong");
    }

    // ---- sets ---------------------------------------------------------------

    private static void CeleritySetCase(Random rng)
    {
        var sut = new CeleritySet<int, Int32WangNaiveHasher>();
        var oracle = new HashSet<int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 6: Check(sut.TryAdd(item) == oracle.Add(item), $"Add({item})"); break;
                case < 9: Check(sut.Remove(item) == oracle.Remove(item), $"Remove({item})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
            Check(sut.Contains(k) == oracle.Contains(k), $"Contains({k})");
        int enumerated = 0;
        foreach (int item in sut)
        {
            Check(oracle.Contains(item), $"enumeration yielded absent {item}");
            enumerated++;
        }
        Check(enumerated == oracle.Count, $"enumeration count {enumerated} != {oracle.Count}");
    }

    private static void IntSetCase(Random rng)
    {
        var sut = new IntSet();
        var oracle = new HashSet<int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 6: Check(sut.TryAdd(item) == oracle.Add(item), $"Add({item})"); break;
                case < 9: Check(sut.Remove(item) == oracle.Remove(item), $"Remove({item})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
            Check(sut.Contains(k) == oracle.Contains(k), $"Contains({k})");
    }

    private static void LongSetCase(Random rng)
    {
        var sut = new LongSet();
        var oracle = new HashSet<long>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            long item = Spread(Key(rng));
            switch (rng.Next(0, 10))
            {
                case < 6: Check(sut.TryAdd(item) == oracle.Add(item), $"Add({item})"); break;
                case < 9: Check(sut.Remove(item) == oracle.Remove(item), $"Remove({item})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
    }

    // ---- specialized sets (probe/deletion machinery vs a HashSet oracle) -----

    // The specialized sets carry the library's most intricate probe/deletion
    // machinery — Robin Hood displacement + backward-shift, SIMD group probing,
    // fingerprint caching, and ArrayPool lifecycle — so each is driven against a
    // HashSet<int> oracle exactly like CeleritySetCase, the layer most likely to
    // surface a wrap-around / tombstone / backward-shift edge case. A naive
    // collision-heavy hasher (Int32WangNaiveHasher) keeps probe chains long.

    private static void RobinHoodSetCase(Random rng)
    {
        var sut = new RobinHoodSet<int, Int32WangNaiveHasher>();
        var oracle = new HashSet<int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 6: Check(sut.TryAdd(item) == oracle.Add(item), $"Add({item})"); break;
                case < 9: Check(sut.Remove(item) == oracle.Remove(item), $"Remove({item})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
            Check(sut.Contains(k) == oracle.Contains(k), $"Contains({k})");
        int enumerated = 0;
        foreach (int item in sut)
        {
            Check(oracle.Contains(item), $"enumeration yielded absent {item}");
            enumerated++;
        }
        Check(enumerated == oracle.Count, $"enumeration count {enumerated} != {oracle.Count}");
    }

    private static void SwissSetCase(Random rng)
    {
        var sut = new SwissSet<int, Int32WangNaiveHasher>();
        var oracle = new HashSet<int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 6: Check(sut.TryAdd(item) == oracle.Add(item), $"Add({item})"); break;
                case < 9: Check(sut.Remove(item) == oracle.Remove(item), $"Remove({item})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
            Check(sut.Contains(k) == oracle.Contains(k), $"Contains({k})");
        int enumerated = 0;
        foreach (int item in sut)
        {
            Check(oracle.Contains(item), $"enumeration yielded absent {item}");
            enumerated++;
        }
        Check(enumerated == oracle.Count, $"enumeration count {enumerated} != {oracle.Count}");
    }

    private static void HashCachingSetCase(Random rng)
    {
        var sut = new HashCachingSet<int, Int32WangNaiveHasher>();
        var oracle = new HashSet<int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 6: Check(sut.TryAdd(item) == oracle.Add(item), $"Add({item})"); break;
                case < 9: Check(sut.Remove(item) == oracle.Remove(item), $"Remove({item})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
            Check(sut.Contains(k) == oracle.Contains(k), $"Contains({k})");
        int enumerated = 0;
        foreach (int item in sut)
        {
            Check(oracle.Contains(item), $"enumeration yielded absent {item}");
            enumerated++;
        }
        Check(enumerated == oracle.Count, $"enumeration count {enumerated} != {oracle.Count}");
    }

    private static void PooledCeleritySetCase(Random rng)
    {
        using var sut = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        var oracle = new HashSet<int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 6: Check(sut.TryAdd(item) == oracle.Add(item), $"Add({item})"); break;
                case < 9: Check(sut.Remove(item) == oracle.Remove(item), $"Remove({item})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
            Check(sut.Contains(k) == oracle.Contains(k), $"Contains({k})");
        int enumerated = 0;
        foreach (int item in sut)
        {
            Check(oracle.Contains(item), $"enumeration yielded absent {item}");
            enumerated++;
        }
        Check(enumerated == oracle.Count, $"enumeration count {enumerated} != {oracle.Count}");
    }

    // SmallSet is the flat-array, linear-scan set (no hasher); reconcile the same
    // Add / Remove / Clear churn and enumeration against a HashSet<int> oracle as
    // CeleritySet, over the tiny key domain the type is built for.
    private static void SmallSetCase(Random rng)
    {
        var sut = new SmallSet<int>();
        var oracle = new HashSet<int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 6: Check(sut.TryAdd(item) == oracle.Add(item), $"Add({item})"); break;
                case < 9: Check(sut.Remove(item) == oracle.Remove(item), $"Remove({item})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = MinKey; k <= MaxKey; k++)
            Check(sut.Contains(k) == oracle.Contains(k), $"Contains({k})");
        int enumerated = 0;
        foreach (int item in sut)
        {
            Check(oracle.Contains(item), $"enumeration yielded absent {item}");
            enumerated++;
        }
        Check(enumerated == oracle.Count, $"enumeration count {enumerated} != {oracle.Count}");
    }

    // SparseSet is the bounded-universe integer set (Briggs–Torczon). It stores only
    // non-negative values below its universe, so it draws from [0, SparseUniverse)
    // rather than the shared [-8, 24] key domain (which spans negatives it rejects by
    // design). Same Add / Remove / Clear churn and enumeration reconciled against a
    // HashSet<int> oracle — the O(1) clear that never wipes the sparse array makes the
    // clear-then-reuse steps the interesting ones.
    private const int SparseUniverse = 32;

    private static void SparseSetCase(Random rng)
    {
        var sut = new SparseSet(SparseUniverse);
        var oracle = new HashSet<int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = rng.Next(0, SparseUniverse);
            switch (rng.Next(0, 10))
            {
                case < 6: Check(sut.TryAdd(item) == oracle.Add(item), $"Add({item})"); break;
                case < 9: Check(sut.Remove(item) == oracle.Remove(item), $"Remove({item})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        for (int k = 0; k < SparseUniverse; k++)
            Check(sut.Contains(k) == oracle.Contains(k), $"Contains({k})");
        int enumerated = 0;
        foreach (int item in sut)
        {
            Check(oracle.Contains(item), $"enumeration yielded absent {item}");
            enumerated++;
        }
        Check(enumerated == oracle.Count, $"enumeration count {enumerated} != {oracle.Count}");
    }

    // ---- ordered (B-tree) collections ---------------------------------------
    //
    // Unlike the hash-table targets, these are order-sensitive: the oracle is a SortedDictionary /
    // SortedSet and the check compares the enumerated *sequence*, element for element. A B-tree that has
    // lost balance or dropped a promoted key can still answer lookups correctly for a while, so the
    // sequence — not just membership — is what actually catches a bad split, borrow, or merge.

    private static void BTreeDictionaryCase(Random rng)
    {
        var sut = new BTreeDictionary<int, int>();
        var oracle = new SortedDictionary<int, int>();
        int ops = OrderedOpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int key = OrderedKey(rng);
            switch (rng.Next(0, 20))
            {
                case < 9: int v = Value(rng); sut[key] = v; oracle[key] = v; break;
                case < 14: int v2 = Value(rng); Check(sut.TryAdd(key, v2) == oracle.TryAdd(key, v2), $"TryAdd({key})"); break;
                case < 19:
                    bool expected = oracle.TryGetValue(key, out int expectedValue);
                    oracle.Remove(key);
                    bool actual = sut.Remove(key, out int actualValue);
                    Check(expected == actual, $"Remove({key}) {actual} != {expected}");
                    Check(!expected || expectedValue == actualValue, $"Remove({key}) value {actualValue} != {expectedValue}");
                    break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");

        for (int k = MinOrderedKey - 2; k <= MaxOrderedKey + 2; k++)
        {
            bool e = oracle.TryGetValue(k, out int ev);
            bool a = sut.TryGetValue(k, out int av);
            Check(e == a && (!e || ev == av), $"lookup({k}) {a}/{av} != {e}/{ev}");

            // The ordered surface has to agree at every probe too, including both open ends.
            bool expectedLower = false;
            int expectedLowerKey = 0;
            bool expectedUpper = false;
            int expectedUpperKey = 0;
            foreach (int oracleKey in oracle.Keys)
            {
                if (!expectedLower && oracleKey >= k)
                {
                    expectedLower = true;
                    expectedLowerKey = oracleKey;
                }

                if (!expectedUpper && oracleKey > k)
                {
                    expectedUpper = true;
                    expectedUpperKey = oracleKey;
                    break;
                }
            }

            Check(sut.TryGetLowerBound(k, out KeyValuePair<int, int> lower) == expectedLower, $"lower({k})");
            Check(!expectedLower || lower.Key == expectedLowerKey, $"lower({k}) {lower.Key} != {expectedLowerKey}");
            Check(sut.TryGetUpperBound(k, out KeyValuePair<int, int> upper) == expectedUpper, $"upper({k})");
            Check(!expectedUpper || upper.Key == expectedUpperKey, $"upper({k}) {upper.Key} != {expectedUpperKey}");
        }

        CheckSameSequence(sut.Select(e => e.Key), oracle.Keys, "enumeration");
        CheckSameSequence(sut.Select(e => e.Value), oracle.Values, "values");

        // A range scan must be the same sequence as the equivalent filter over the oracle.
        int from = OrderedKey(rng);
        int to = from + rng.Next(0, 120);
        CheckSameSequence(
            sut.EnumerateRange(from, to).Select(e => e.Key),
            oracle.Keys.Where(k => k >= from && k < to),
            $"range[{from},{to})");
    }

    private static void BTreeSetCase(Random rng)
    {
        var sut = new BTreeSet<int>();
        var oracle = new SortedSet<int>();
        int ops = OrderedOpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = OrderedKey(rng);
            switch (rng.Next(0, 20))
            {
                case < 11: Check(sut.TryAdd(item) == oracle.Add(item), $"Add({item})"); break;
                case < 19: Check(sut.Remove(item) == oracle.Remove(item), $"Remove({item})"); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");

        for (int k = MinOrderedKey - 2; k <= MaxOrderedKey + 2; k++)
            Check(sut.Contains(k) == oracle.Contains(k), $"Contains({k})");

        CheckSameSequence(sut, oracle, "enumeration");

        int from = OrderedKey(rng);
        int to = from + rng.Next(0, 120);
        CheckSameSequence(
            sut.EnumerateRange(from, to),
            oracle.Where(v => v >= from && v < to),
            $"range[{from},{to})");
    }

    // Order-sensitive comparison: both sequences must yield the same elements in the same positions.
    private static void CheckSameSequence(IEnumerable<int> actual, IEnumerable<int> expected, string what)
    {
        using IEnumerator<int> a = actual.GetEnumerator();
        using IEnumerator<int> e = expected.GetEnumerator();
        int index = 0;

        while (true)
        {
            bool hasA = a.MoveNext();
            bool hasE = e.MoveNext();
            if (hasA != hasE)
                throw new DivergenceException($"{what} length differs at index {index}");
            if (!hasA)
                return;
            if (a.Current != e.Current)
                throw new DivergenceException($"{what}[{index}] {a.Current} != {e.Current}");

            index++;
        }
    }

    // ---- bloom filter (probabilistic, one-directional) ----------------------

    // A Bloom filter permits false positives but never false negatives, so the
    // oracle check is one-directional: every element the HashSet holds must test
    // present. Absence is not reconciled (a true Contains may be a legitimate false
    // positive). Sized for the tiny key domain so add / clear churn dominates.
    private static void BloomFilterCase(Random rng)
    {
        var sut = new BloomFilter<int, Int32WangNaiveHasher>(64);
        var oracle = new HashSet<int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 9: sut.Add(item); oracle.Add(item); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        foreach (int k in oracle)
            Check(sut.Contains(k), $"false negative for {k}");
    }

    // ---- cuckoo filter (probabilistic, deletable, one-directional) ----------

    // A cuckoo filter permits false positives but never false negatives, and unlike a
    // Bloom filter it supports Remove, so the oracle is a multiset (per-key copy counts).
    // The check is one-directional: every key with a live copy must test present. Inserts
    // go through TryAdd so the oracle only records stored copies (the filter can refuse an
    // insert once full); a Remove of a present key must succeed. Sized generously so the
    // add / remove / clear churn dominates over fill behaviour.
    private static void CuckooFilterCase(Random rng)
    {
        var sut = new CuckooFilter<int, Int32WangNaiveHasher>(512);
        var oracle = new Dictionary<int, int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 7:
                    if (sut.TryAdd(item))
                        oracle[item] = oracle.TryGetValue(item, out int c) ? c + 1 : 1;
                    break;
                case < 9:
                    if (oracle.TryGetValue(item, out int n) && n > 0)
                    {
                        Check(sut.Remove(item), $"present element {item} failed to remove");
                        if (n == 1) oracle.Remove(item);
                        else oracle[item] = n - 1;
                    }
                    break;
                default:
                    sut.Clear();
                    oracle.Clear();
                    break;
            }
        }

        foreach (var kv in oracle)
            if (kv.Value > 0)
                Check(sut.Contains(kv.Key), $"false negative for {kv.Key}");
    }

    // ---- xor filter (probabilistic, build-once, one-directional) ------------

    // An xor filter is built once from a fixed element set and is then immutable. It permits false positives
    // but never false negatives, so the check is one-directional: every element of the construction set must
    // test present. The set is drawn from the tiny key domain (so duplicates exercise the constructor's
    // set-dedupe) and includes a fresh random-range membership build each trial, stressing the peel + reseed
    // construction path against different layouts.
    private static void XorFilterCase(Random rng)
    {
        var oracle = new HashSet<int>();
        int size = OpCount(rng); // 0..199 elements, including the empty-set edge case
        for (int i = 0; i < size; i++)
            oracle.Add(Key(rng));

        var sut = new XorFilter<int, Int32WangNaiveHasher>(oracle);

        // Count is the distinct-element-*hash* count: the weak naive hasher can collide two distinct ints onto
        // one 64-bit key, which the constructor folds into a single entry, so it is a lower bound on the oracle
        // (never above, since dedupe only ever removes).
        Check(sut.Count <= oracle.Count, $"distinct count {sut.Count} exceeds oracle {oracle.Count}");

        foreach (int k in oracle)
            Check(sut.Contains(k), $"false negative for {k}");

        // An empty filter must report every probe absent (the _count == 0 short-circuit).
        if (oracle.Count == 0)
            for (int k = MinKey; k <= MaxKey; k++)
                Check(!sut.Contains(k), $"empty xor filter reported {k} present");
    }

    // ---- bit set (exact, two-directional) -----------------------------------

    // A BitSet is exact, so it reconciles fully against a bool[] oracle after every
    // single-bit op and after the bulk boolean operators. A length with a partial
    // tail word is used so tail-bit masking (SetAll / Not) is exercised; the SIMD
    // bulk paths kick in once the word count reaches the vector width.
    private static void BitSetCase(Random rng)
    {
        int length = rng.Next(1, 320);
        var sut = new BitSet(length);
        var oracle = new bool[length];
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int index = rng.Next(0, length);
            switch (rng.Next(0, 12))
            {
                case < 5: sut.Set(index, true); oracle[index] = true; break;
                case < 8: sut.Set(index, false); oracle[index] = false; break;
                case < 10: oracle[index] = sut.Flip(index); break;
                case < 11:
                    bool all = rng.Next(2) == 0;
                    sut.SetAll(all);
                    Array.Fill(oracle, all);
                    break;
                default:
                    sut.Clear();
                    Array.Clear(oracle);
                    break;
            }
        }

        int expectedCount = 0;
        var expectedSetBits = new List<int>();
        for (int i = 0; i < length; i++)
        {
            Check(sut[i] == oracle[i], $"bit {i} disagreed");
            if (oracle[i]) { expectedCount++; expectedSetBits.Add(i); }
        }
        Check(sut.Count == expectedCount, "Count disagreed");

        var actualSetBits = new List<int>();
        foreach (int idx in sut.EnumerateSetBits())
            actualSetBits.Add(idx);
        Check(actualSetBits.SequenceEqual(expectedSetBits), "EnumerateSetBits disagreed");

        // Bulk operators against a second random vector.
        var otherBits = new bool[length];
        for (int i = 0; i < length; i++)
            otherBits[i] = rng.Next(2) == 0;
        var other = new BitSet(otherBits);

        bool[] FromBitSet(BitSet s)
        {
            var arr = new bool[length];
            for (int i = 0; i < length; i++) arr[i] = s[i];
            return arr;
        }

        var and = new BitSet(FromBitSet(sut)).And(other);
        var or = new BitSet(FromBitSet(sut)).Or(other);
        var xor = new BitSet(FromBitSet(sut)).Xor(other);
        var not = new BitSet(FromBitSet(sut)).Not();
        for (int i = 0; i < length; i++)
        {
            Check(and[i] == (oracle[i] && otherBits[i]), $"And bit {i}");
            Check(or[i] == (oracle[i] || otherBits[i]), $"Or bit {i}");
            Check(xor[i] == (oracle[i] ^ otherBits[i]), $"Xor bit {i}");
            Check(not[i] == !oracle[i], $"Not bit {i}");
        }
    }

    // ---- cardinality estimator ----------------------------------------------

    // HyperLogLog estimates a distinct count, so the oracle is a HashSet whose exact
    // Count is the ground truth. The tiny key domain (<= 33 distinct values) sits deep
    // in the linear-counting regime of a precision-14 estimator, where the estimate is
    // exact apart from the rare register collision that can undercount by a register —
    // so the estimate must equal the exact count within a small slack (never an
    // overcount beyond rounding). A *collision-free* hasher (Murmur3's bijective fmix32)
    // is required here: the estimator counts distinct hash values, so the naive xor-fold
    // hasher — which maps distinct keys in this domain to the same code — would
    // legitimately undercount, unlike the dictionaries that tolerate any hasher.
    private static void HyperLogLogCase(Random rng)
    {
        var sut = new HyperLogLog<int, Int32Murmur3Hasher>();
        var oracle = new HashSet<int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 9: sut.Add(item); oracle.Add(item); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        long estimate = sut.EstimateCardinality();
        int exact = oracle.Count;
        Check(estimate >= exact - 3 && estimate <= exact + 1,
            $"cardinality estimate {estimate} not within slack of exact {exact}");
    }

    // ---- frequency estimator (one-directional) ------------------------------

    // A Count-Min sketch never underestimates a frequency (collisions only inflate
    // counters), so the oracle check is one-directional: every element's estimate must
    // be at least its exact count in a Dictionary frequency table. Overestimates are not
    // reconciled (a collision may legitimately inflate an estimate). Counts (including
    // weighted Adds) accumulate, and Clear resets both.
    private static void CountMinSketchCase(Random rng)
    {
        var sut = new CountMinSketch<int, Int32WangNaiveHasher>();
        var oracle = new Dictionary<int, long>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int item = Key(rng);
            switch (rng.Next(0, 10))
            {
                case < 7:
                    sut.Add(item);
                    oracle[item] = oracle.GetValueOrDefault(item) + 1;
                    break;
                case < 9:
                    long w = rng.Next(1, 10);
                    sut.Add(item, w);
                    oracle[item] = oracle.GetValueOrDefault(item) + w;
                    break;
                default:
                    sut.Clear();
                    oracle.Clear();
                    break;
            }
        }

        foreach (var (k, count) in oracle)
            Check(sut.EstimateCount(k) >= count, $"underestimate for {k}: {sut.EstimateCount(k)} < {count}");
    }

    // ---- probabilistic merges (UnionWith) -----------------------------------

    // The four probabilistic types each merge two equally-parameterised siblings with
    // UnionWith. The merge is the least-exercised correctness path — it touches the raw
    // bit/counter/register state directly rather than going through Add — so each case
    // builds two independent filters from separate random streams, merges, and checks the
    // type's defining invariant survives the merge: a merged filter must report everything
    // either operand held, exactly as a single filter fed both streams would.

    // Bloom: merge is a bitwise OR, so the no-false-negative guarantee extends to the union —
    // every element added to either filter must still test present in the merged filter.
    private static void BloomFilterMergeCase(Random rng)
    {
        var a = new BloomFilter<int, Int32WangNaiveHasher>(256);
        var b = new BloomFilter<int, Int32WangNaiveHasher>(256);
        var inA = new HashSet<int>();
        var inB = new HashSet<int>();

        int opsA = OpCount(rng);
        for (int i = 0; i < opsA; i++) { int k = Key(rng); a.Add(k); inA.Add(k); }
        int opsB = OpCount(rng);
        for (int i = 0; i < opsB; i++) { int k = Key(rng); b.Add(k); inB.Add(k); }

        long expectedCount = a.Count + (long)b.Count;
        a.UnionWith(b);

        foreach (int k in inA) Check(a.Contains(k), $"false negative for {k} (from A) after merge");
        foreach (int k in inB) Check(a.Contains(k), $"false negative for {k} (from B) after merge");
        Check(a.Count == expectedCount, $"merged Count {a.Count} != {expectedCount}");
    }

    // Cuckoo: merge re-homes every stored fingerprint into the destination. The destination's
    // own elements are never lost even when the merge overflows (it throws part-way), so their
    // presence is checked unconditionally; the source's elements are guaranteed present only on
    // a merge that ran to completion. Inserts go through TryAdd so the presence sets only record
    // fingerprints the filter actually stored (it can refuse once a hot bucket pair saturates).
    private static void CuckooFilterMergeCase(Random rng)
    {
        var a = new CuckooFilter<int, Int32WangNaiveHasher>(512);
        var b = new CuckooFilter<int, Int32WangNaiveHasher>(512);
        var inA = new HashSet<int>();
        var inB = new HashSet<int>();

        int opsA = OpCount(rng);
        for (int i = 0; i < opsA; i++) { int k = Key(rng); if (a.TryAdd(k)) inA.Add(k); }
        int opsB = OpCount(rng);
        for (int i = 0; i < opsB; i++) { int k = Key(rng); if (b.TryAdd(k)) inB.Add(k); }

        bool completed;
        try { a.UnionWith(b); completed = true; }
        catch (InvalidOperationException) { completed = false; }

        foreach (int k in inA) Check(a.Contains(k), $"false negative for {k} (from A) after merge");
        if (completed)
            foreach (int k in inB) Check(a.Contains(k), $"false negative for {k} (from B) after merge");
    }

    // HyperLogLog: merge takes the per-register maximum, which is exactly the register state the
    // combined stream would have produced, so the merged estimate must sit within the same small
    // linear-counting slack of the exact union cardinality as a single estimator does. The tiny
    // key domain (<= 33 distinct values) keeps the union deep in the linear-counting regime, and
    // the collision-free Murmur3 hasher (as in the single-estimator case) keeps the estimate exact
    // apart from the rare register collision that can undercount by a register.
    private static void HyperLogLogMergeCase(Random rng)
    {
        var a = new HyperLogLog<int, Int32Murmur3Hasher>();
        var b = new HyperLogLog<int, Int32Murmur3Hasher>();
        var union = new HashSet<int>();

        int opsA = OpCount(rng);
        for (int i = 0; i < opsA; i++) { int k = Key(rng); a.Add(k); union.Add(k); }
        int opsB = OpCount(rng);
        for (int i = 0; i < opsB; i++) { int k = Key(rng); b.Add(k); union.Add(k); }

        a.UnionWith(b);

        long estimate = a.EstimateCardinality();
        int exact = union.Count;
        Check(estimate >= exact - 3 && estimate <= exact + 1,
            $"merged cardinality estimate {estimate} not within slack of exact {exact}");
    }

    // Count-Min: merge adds counters elementwise, so the never-underestimate guarantee extends to
    // the combined stream — every element's merged estimate must be at least its summed exact
    // frequency — and the total count is exactly the sum of the two operands' totals.
    private static void CountMinSketchMergeCase(Random rng)
    {
        var a = new CountMinSketch<int, Int32WangNaiveHasher>();
        var b = new CountMinSketch<int, Int32WangNaiveHasher>();
        var oracle = new Dictionary<int, long>();
        long totalA = 0, totalB = 0;

        int opsA = OpCount(rng);
        for (int i = 0; i < opsA; i++)
        {
            int k = Key(rng);
            long w = rng.Next(1, 10);
            a.Add(k, w);
            oracle[k] = oracle.GetValueOrDefault(k) + w;
            totalA += w;
        }
        int opsB = OpCount(rng);
        for (int i = 0; i < opsB; i++)
        {
            int k = Key(rng);
            long w = rng.Next(1, 10);
            b.Add(k, w);
            oracle[k] = oracle.GetValueOrDefault(k) + w;
            totalB += w;
        }

        a.UnionWith(b);

        foreach (var (k, count) in oracle)
            Check(a.EstimateCount(k) >= count, $"underestimate for {k} after merge: {a.EstimateCount(k)} < {count}");
        Check(a.TotalCount == totalA + totalB, $"merged TotalCount {a.TotalCount} != {totalA + totalB}");
    }

    // ---- multi-map ----------------------------------------------------------

    private static void CelerityMultiMapCase(Random rng)
    {
        var sut = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        var oracle = new Dictionary<int, List<int>>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            int key = rng.Next(-4, 13);
            switch (rng.Next(0, 10))
            {
                case < 6:
                    int v = rng.Next(0, 30);
                    sut.Add(key, v);
                    if (!oracle.TryGetValue(key, out var list))
                        oracle[key] = list = new List<int>();
                    list.Add(v);
                    break;
                case < 9:
                    int rv = rng.Next(0, 30);
                    bool expected = oracle.TryGetValue(key, out var l) && l.Remove(rv);
                    if (expected && oracle[key].Count == 0)
                        oracle.Remove(key);
                    Check(sut.Remove(key, rv) == expected, $"Remove({key},{rv})");
                    break;
                default:
                    Check(sut.RemoveAll(key) == oracle.Remove(key), $"RemoveAll({key})");
                    break;
            }
        }

        Check(sut.Count == oracle.Count, $"Count {sut.Count} != {oracle.Count}");
        int valueCount = oracle.Values.Sum(x => x.Count);
        Check(sut.ValueCount == valueCount, $"ValueCount {sut.ValueCount} != {valueCount}");
        for (int k = -4; k <= 12; k++)
        {
            bool present = oracle.TryGetValue(k, out var expectedList);
            Check(sut.ContainsKey(k) == present, $"ContainsKey({k})");
            int[] actual = [.. sut[k]];
            int[] want = present ? expectedList!.ToArray() : [];
            Check(actual.Length == want.Length, $"group[{k}] length {actual.Length} != {want.Length}");
            for (int j = 0; j < want.Length; j++)
                Check(actual[j] == want[j], $"group[{k}][{j}] {actual[j]} != {want[j]}");
        }
    }

    // ---- frozen (build-once) ------------------------------------------------

    private static void FrozenCase(Random rng)
    {
        var oracle = new Dictionary<string, int>();
        int entries = rng.Next(0, 40);
        for (int i = 0; i < entries; i++)
            oracle[$"key_{rng.Next(0, 60)}"] = Value(rng); // last write wins -> unique keys

        var frozen = new FrozenCelerityDictionary<int>(oracle);

        Check(frozen.Count == oracle.Count, $"Count {frozen.Count} != {oracle.Count}");
        foreach (var kv in oracle)
            Check(frozen.TryGetValue(kv.Key, out int av) && av == kv.Value, $"lookup({kv.Key}) missing/wrong");
        for (int k = 60; k <= 70; k++)
            Check(!frozen.ContainsKey($"key_{k}"), $"absent key_{k} reported present");

        int seen = 0;
        foreach (var kv in frozen)
        {
            Check(oracle.TryGetValue(kv.Key, out int ov) && ov == kv.Value, $"enumeration wrong for {kv.Key}");
            seen++;
        }
        Check(seen == oracle.Count, $"enumeration count {seen} != {oracle.Count}");
    }

    private static void StringInternTableCase(Random rng)
    {
        // A tiny, duplicate-rich token universe so hits dominate misses and probe chains stay
        // dense; the table starts undersized so the run drives several resizes.
        var table = new StringInternTable(capacity: 2);

        // The oracle maps a token's contents to the canonical instance first handed out.
        var oracle = new Dictionary<string, string>(StringComparer.Ordinal);

        int steps = rng.Next(0, 400);
        for (int i = 0; i < steps; i++)
        {
            string token = $"tok_{rng.Next(0, 30)}";
            int op = rng.Next(100);

            if (op < 50)
            {
                // Intern from a span carved out of a larger buffer — the parser shape. Only a
                // miss may materialize a string; a hit must return the instance already held.
                string padded = $"<<{token}>>";
                string interned = table.GetOrAdd(padded.AsSpan(2, token.Length));

                Check(interned == token, $"GetOrAdd(span) returned {interned} for {token}");
                if (oracle.TryGetValue(token, out string? canonical))
                    Check(ReferenceEquals(canonical, interned), $"GetOrAdd(span) re-allocated {token}");
                else
                    oracle[token] = interned;
            }
            else if (op < 70)
            {
                string supplied = new string(token.ToCharArray());
                string interned = table.GetOrAdd(supplied);

                if (oracle.TryGetValue(token, out string? canonical))
                    Check(ReferenceEquals(canonical, interned), $"GetOrAdd(string) re-allocated {token}");
                else
                {
                    Check(ReferenceEquals(supplied, interned), $"GetOrAdd(string) did not adopt {token}");
                    oracle[token] = interned;
                }
            }
            else if (op < 90)
            {
                bool expected = oracle.TryGetValue(token, out string? canonical);
                bool actual = table.TryGet(token.AsSpan(), out string? found);
                Check(expected == actual, $"TryGet({token}) {actual} != {expected}");
                Check(!expected || ReferenceEquals(canonical, found), $"TryGet({token}) returned a non-canonical instance");
                Check(table.Contains(token.AsSpan()) == expected, $"Contains(span {token}) != {expected}");
                Check(table.Contains(token) == expected, $"Contains(string {token}) != {expected}");
            }
            else
            {
                int seen = 0;
                foreach (string s in table)
                {
                    Check(oracle.TryGetValue(s, out string? canonical) && ReferenceEquals(canonical, s),
                        $"enumeration yielded a non-canonical or absent {s}");
                    seen++;
                }
                Check(seen == oracle.Count, $"enumeration count {seen} != {oracle.Count}");
            }

            Check(table.Count == oracle.Count, $"Count {table.Count} != {oracle.Count}");
        }
    }

    private static void FrozenSetCase(Random rng)
    {
        // A deliberately tiny, duplicate-rich element universe so the build's dedupe
        // and the perfect-hash / fallback paths fire densely.
        var sourceList = new List<string>();
        var oracle = new HashSet<string>();
        int entries = rng.Next(0, 50);
        for (int i = 0; i < entries; i++)
        {
            string item = $"item_{rng.Next(0, 30)}";
            sourceList.Add(item); // duplicates allowed in the source
            oracle.Add(item);
        }

        var frozen = new FrozenCeleritySet(sourceList);

        Check(frozen.Count == oracle.Count, $"Count {frozen.Count} != {oracle.Count}");
        foreach (string item in oracle)
            Check(frozen.Contains(item), $"Contains({item}) missing");
        for (int k = 30; k <= 40; k++)
            Check(!frozen.Contains($"item_{k}"), $"absent item_{k} reported present");

        int seen = 0;
        foreach (string item in frozen)
        {
            Check(oracle.Contains(item), $"enumeration yielded absent {item}");
            seen++;
        }
        Check(seen == oracle.Count, $"enumeration count {seen} != {oracle.Count}");

        // Set-algebra parity against the BCL oracle.
        Check(frozen.SetEquals(oracle) == oracle.SetEquals(oracle), "SetEquals(self)");
        Check(frozen.IsSupersetOf(oracle) == oracle.IsSupersetOf(oracle), "IsSupersetOf(self)");
    }

    // Spreads a small int across the 64-bit space so the long collections see
    // high-bit-only differences, not just sign-extended small ints.
    private static long Spread(int k) => (long)k << 33 | (uint)k;

    /// <summary>Formats a one-line summary of the registered targets.</summary>
    public static string TargetList()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < All.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(All[i].Name);
        }
        return sb.ToString();
    }

    // ---- the IHashProvider64 path on every sketch (one-directional) ---------

    // The five sketches take a hasher's 64-bit surface when it provides one (#304), which is a
    // *different* code path from the widened 32-bit one the cases above drive: the hash is the
    // hasher's own ulong rather than a SplitMix64 / fmix64 avalanche of a 32-bit code, so the
    // fingerprint, bucket-index and register-rank derivations all see different bits. These cases
    // mirror their 32-bit siblings over `long` keys with Int64WangHasher (which implements
    // IHashProvider64<long>) so the new path is reconciled against the same BCL oracles.

    private const long MinKey64 = -8L;
    private const long MaxKey64 = 24L;

    private static long Key64(Random rng) => rng.NextInt64(MinKey64, MaxKey64 + 1);

    private static void BloomFilterHash64Case(Random rng)
    {
        var sut = new BloomFilter<long, Int64WangHasher>(64);
        var oracle = new HashSet<long>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            long item = Key64(rng);
            switch (rng.Next(0, 10))
            {
                case < 9: sut.Add(item); oracle.Add(item); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        foreach (long k in oracle)
            Check(sut.Contains(k), $"false negative for {k}");
    }

    private static void CuckooFilterHash64Case(Random rng)
    {
        var sut = new CuckooFilter<long, Int64WangHasher>(512);
        var oracle = new Dictionary<long, int>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            long item = Key64(rng);
            switch (rng.Next(0, 10))
            {
                case < 7:
                    if (sut.TryAdd(item))
                        oracle[item] = oracle.TryGetValue(item, out int c) ? c + 1 : 1;
                    break;
                case < 9:
                    if (oracle.TryGetValue(item, out int n) && n > 0)
                    {
                        Check(sut.Remove(item), $"present element {item} failed to remove");
                        if (n == 1) oracle.Remove(item);
                        else oracle[item] = n - 1;
                    }
                    break;
                default:
                    sut.Clear();
                    oracle.Clear();
                    break;
            }
        }

        foreach (var kv in oracle)
            if (kv.Value > 0)
                Check(sut.Contains(kv.Key), $"false negative for {kv.Key}");
    }

    private static void XorFilterHash64Case(Random rng)
    {
        var oracle = new HashSet<long>();
        int size = OpCount(rng);
        for (int i = 0; i < size; i++)
            oracle.Add(Key64(rng));

        var sut = new XorFilter<long, Int64WangHasher>(oracle);

        // hash64shift is a bijection on 64 bits, so unlike the naive-hasher case above no two
        // distinct longs can share a key: the distinct count must match the oracle exactly.
        Check(sut.Count == oracle.Count, $"distinct count {sut.Count} != oracle {oracle.Count}");

        foreach (long k in oracle)
            Check(sut.Contains(k), $"false negative for {k}");

        if (oracle.Count == 0)
            for (long k = MinKey64; k <= MaxKey64; k++)
                Check(!sut.Contains(k), $"empty xor filter reported {k} present");
    }

    private static void HyperLogLogHash64Case(Random rng)
    {
        var sut = new HyperLogLog<long, Int64WangHasher>();
        var oracle = new HashSet<long>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            long item = Key64(rng);
            switch (rng.Next(0, 10))
            {
                case < 9: sut.Add(item); oracle.Add(item); break;
                default: sut.Clear(); oracle.Clear(); break;
            }
        }

        long estimate = sut.EstimateCardinality();
        int exact = oracle.Count;
        Check(estimate >= exact - 3 && estimate <= exact + 1,
            $"cardinality estimate {estimate} not within slack of exact {exact}");
    }

    private static void CountMinSketchHash64Case(Random rng)
    {
        var sut = new CountMinSketch<long, Int64WangHasher>();
        var oracle = new Dictionary<long, long>();
        int ops = OpCount(rng);

        for (int i = 0; i < ops; i++)
        {
            long item = Key64(rng);
            switch (rng.Next(0, 10))
            {
                case < 7:
                    sut.Add(item);
                    oracle[item] = oracle.TryGetValue(item, out long c) ? c + 1 : 1;
                    break;
                case < 9:
                    int weight = rng.Next(1, 20);
                    sut.Add(item, weight);
                    oracle[item] = oracle.TryGetValue(item, out long w) ? w + weight : weight;
                    break;
                default:
                    sut.Clear();
                    oracle.Clear();
                    break;
            }
        }

        foreach (var kv in oracle)
            Check(sut.EstimateCount(kv.Key) >= kv.Value,
                $"underestimate for {kv.Key}: {sut.EstimateCount(kv.Key)} < {kv.Value}");
    }
}
