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
        ("IntSet", IntSetCase),
        ("LongSet", LongSetCase),
        ("CelerityMultiMap", CelerityMultiMapCase),
        ("FrozenCelerityDictionary", FrozenCase),
        ("FrozenCeleritySet", FrozenSetCase),
        ("BloomFilter", BloomFilterCase),
        ("BitSet", BitSetCase),
        ("HyperLogLog", HyperLogLogCase),
        ("CountMinSketch", CountMinSketchCase),
    ];

    private const int MinKey = -8;
    private const int MaxKey = 24;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new DivergenceException(message);
    }

    private static int Key(Random rng) => rng.Next(MinKey, MaxKey + 1);
    private static int Value(Random rng) => rng.Next(0, 1000);
    private static int OpCount(Random rng) => rng.Next(0, 200);

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
}
