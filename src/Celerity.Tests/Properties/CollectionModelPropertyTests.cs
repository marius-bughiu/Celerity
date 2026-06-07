using System;
using System.Collections.Generic;
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Properties;

// Issue #29 — property-based / model-based testing.
//
// Every Celerity collection claims drop-in parity with a BCL counterpart
// (Dictionary<,>, HashSet<>, a one-to-many lookup). These tests make that the
// explicit contract: CsCheck generates randomized sequences of mutating
// operations, applies the identical sequence to both the Celerity collection
// and an oracle BCL collection, and asserts the two stay observably equal —
// Count, per-key lookups across the whole key domain, and full enumeration.
//
// The key domains are deliberately tiny (and include 0 / negatives) so that
// collisions, resizes, the out-of-band zero/default-key slot, and backward-shift
// deletion all fire densely. CsCheck shrinks any failing sequence to a minimal
// reproduction and prints the seed for replay.
public class CollectionModelPropertyTests
{
    // A randomized mutation against a key/value dictionary.
    private enum DictOp { Set, Remove, TryAdd, Clear }

    private static readonly Gen<(DictOp op, int key, int val)> GenDictOp =
        Gen.Select(
            Gen.Int[0, 99].Select(n => n < 45 ? DictOp.Set
                                     : n < 80 ? DictOp.Remove
                                     : n < 98 ? DictOp.TryAdd
                                     : DictOp.Clear),
            Gen.Int[-8, 24],   // key domain spans 0 and negatives for the special slots
            Gen.Int[0, 1_000]);

    private static readonly Gen<List<(DictOp, int, int)>> GenDictOps =
        GenDictOp.List[0, 120];

    // ---- Generic CelerityDictionary<int,int> vs Dictionary<int,int> ---------

    [Fact]
    public void CelerityDictionary_ShouldMatch_BclDictionary()
    {
        GenDictOps.Sample(ops =>
        {
            var sut = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
            var oracle = new Dictionary<int, int>();

            foreach (var (op, key, val) in ops)
            {
                switch (op)
                {
                    case DictOp.Set:
                        sut[key] = val;
                        oracle[key] = val;
                        break;
                    case DictOp.Remove:
                        Assert.Equal(oracle.Remove(key), sut.Remove(key));
                        break;
                    case DictOp.TryAdd:
                        Assert.Equal(oracle.TryAdd(key, val), sut.TryAdd(key, val));
                        break;
                    case DictOp.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                }
            }

            AssertDictEquivalent(sut, oracle);
        }, iter: 2000);
    }

    // ---- RobinHoodDictionary<int,int> vs Dictionary<int,int> ----------------

    [Fact]
    public void RobinHoodDictionary_ShouldMatch_BclDictionary()
    {
        GenDictOps.Sample(ops =>
        {
            var sut = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
            var oracle = new Dictionary<int, int>();

            foreach (var (op, key, val) in ops)
            {
                switch (op)
                {
                    case DictOp.Set:
                        sut[key] = val;
                        oracle[key] = val;
                        break;
                    case DictOp.Remove:
                        Assert.Equal(oracle.Remove(key), sut.Remove(key));
                        break;
                    case DictOp.TryAdd:
                        Assert.Equal(oracle.TryAdd(key, val), sut.TryAdd(key, val));
                        break;
                    case DictOp.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                }
            }

            AssertDictEquivalent(sut, oracle);
        }, iter: 2000);
    }

    // ---- PooledCelerityDictionary<int,int> vs Dictionary<int,int> -----------

    [Fact]
    public void PooledCelerityDictionary_ShouldMatch_BclDictionary()
    {
        GenDictOps.Sample(ops =>
        {
            using var sut = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
            var oracle = new Dictionary<int, int>();

            foreach (var (op, key, val) in ops)
            {
                switch (op)
                {
                    case DictOp.Set:
                        sut[key] = val;
                        oracle[key] = val;
                        break;
                    case DictOp.Remove:
                        Assert.Equal(oracle.Remove(key), sut.Remove(key));
                        break;
                    case DictOp.TryAdd:
                        Assert.Equal(oracle.TryAdd(key, val), sut.TryAdd(key, val));
                        break;
                    case DictOp.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                }
            }

            AssertDictEquivalent(sut, oracle);
        }, iter: 2000);
    }

    // ---- SwissDictionary<int,int> vs Dictionary<int,int> --------------------

    [Fact]
    public void SwissDictionary_ShouldMatch_BclDictionary()
    {
        GenDictOps.Sample(ops =>
        {
            var sut = new SwissDictionary<int, int, Int32WangNaiveHasher>();
            var oracle = new Dictionary<int, int>();

            foreach (var (op, key, val) in ops)
            {
                switch (op)
                {
                    case DictOp.Set:
                        sut[key] = val;
                        oracle[key] = val;
                        break;
                    case DictOp.Remove:
                        Assert.Equal(oracle.Remove(key), sut.Remove(key));
                        break;
                    case DictOp.TryAdd:
                        Assert.Equal(oracle.TryAdd(key, val), sut.TryAdd(key, val));
                        break;
                    case DictOp.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                }
            }

            AssertDictEquivalent(sut, oracle);
        }, iter: 2000);
    }

    // ---- HashCachingDictionary<int,int> vs Dictionary<int,int> --------------

    [Fact]
    public void HashCachingDictionary_ShouldMatch_BclDictionary()
    {
        GenDictOps.Sample(ops =>
        {
            var sut = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
            var oracle = new Dictionary<int, int>();

            foreach (var (op, key, val) in ops)
            {
                switch (op)
                {
                    case DictOp.Set:
                        sut[key] = val;
                        oracle[key] = val;
                        break;
                    case DictOp.Remove:
                        Assert.Equal(oracle.Remove(key), sut.Remove(key));
                        break;
                    case DictOp.TryAdd:
                        Assert.Equal(oracle.TryAdd(key, val), sut.TryAdd(key, val));
                        break;
                    case DictOp.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                }
            }

            AssertDictEquivalent(sut, oracle);
        }, iter: 2000);
    }

    [Fact]
    public void IntDictionary_ShouldMatch_BclDictionary()
    {
        GenDictOps.Sample(ops =>
        {
            var sut = new IntDictionary<int>();
            var oracle = new Dictionary<int, int>();

            foreach (var (op, key, val) in ops)
            {
                switch (op)
                {
                    case DictOp.Set: sut[key] = val; oracle[key] = val; break;
                    case DictOp.Remove: Assert.Equal(oracle.Remove(key), sut.Remove(key)); break;
                    case DictOp.TryAdd: Assert.Equal(oracle.TryAdd(key, val), sut.TryAdd(key, val)); break;
                    case DictOp.Clear: sut.Clear(); oracle.Clear(); break;
                }
            }

            Assert.Equal(oracle.Count, sut.Count);
            for (int k = -8; k <= 24; k++)
            {
                bool expected = oracle.TryGetValue(k, out int ev);
                bool actual = sut.TryGetValue(k, out int av);
                Assert.Equal(expected, actual);
                if (expected) Assert.Equal(ev, av);
            }
        }, iter: 2000);
    }

    [Fact]
    public void LongDictionary_ShouldMatch_BclDictionary()
    {
        GenDictOps.Sample(ops =>
        {
            var sut = new LongDictionary<int>();
            var oracle = new Dictionary<long, int>();

            foreach (var (op, key, val) in ops)
            {
                long lk = (long)key << 33 | (uint)key; // spread across the 64-bit space
                switch (op)
                {
                    case DictOp.Set: sut[lk] = val; oracle[lk] = val; break;
                    case DictOp.Remove: Assert.Equal(oracle.Remove(lk), sut.Remove(lk)); break;
                    case DictOp.TryAdd: Assert.Equal(oracle.TryAdd(lk, val), sut.TryAdd(lk, val)); break;
                    case DictOp.Clear: sut.Clear(); oracle.Clear(); break;
                }
            }

            Assert.Equal(oracle.Count, sut.Count);
            foreach (var kv in oracle)
            {
                Assert.True(sut.TryGetValue(kv.Key, out int av));
                Assert.Equal(kv.Value, av);
            }
        }, iter: 2000);
    }

    [Fact]
    public void SmallDictionary_ShouldMatch_BclDictionary()
    {
        GenDictOps.Sample(ops =>
        {
            var sut = new SmallDictionary<int, int>();
            var oracle = new Dictionary<int, int>();

            foreach (var (op, key, val) in ops)
            {
                switch (op)
                {
                    case DictOp.Set: sut[key] = val; oracle[key] = val; break;
                    case DictOp.Remove: Assert.Equal(oracle.Remove(key), sut.Remove(key)); break;
                    case DictOp.TryAdd: Assert.Equal(oracle.TryAdd(key, val), sut.TryAdd(key, val)); break;
                    case DictOp.Clear: sut.Clear(); oracle.Clear(); break;
                }
            }

            Assert.Equal(oracle.Count, sut.Count);
            for (int k = -8; k <= 24; k++)
            {
                bool expected = oracle.TryGetValue(k, out int ev);
                bool actual = sut.TryGetValue(k, out int av);
                Assert.Equal(expected, actual);
                if (expected) Assert.Equal(ev, av);
            }
        }, iter: 2000);
    }

    // ---- Sets vs HashSet ----------------------------------------------------

    private enum SetOp { Add, Remove, Clear }

    private static readonly Gen<List<(SetOp, int)>> GenSetOps =
        Gen.Select(
            Gen.Int[0, 99].Select(n => n < 55 ? SetOp.Add : n < 97 ? SetOp.Remove : SetOp.Clear),
            Gen.Int[-8, 24])
        .List[0, 120];

    [Fact]
    public void CeleritySet_ShouldMatch_BclHashSet()
    {
        GenSetOps.Sample(ops =>
        {
            var sut = new CeleritySet<int, Int32WangNaiveHasher>();
            var oracle = new HashSet<int>();

            foreach (var (op, item) in ops)
            {
                switch (op)
                {
                    case SetOp.Add: Assert.Equal(oracle.Add(item), sut.TryAdd(item)); break;
                    case SetOp.Remove: Assert.Equal(oracle.Remove(item), sut.Remove(item)); break;
                    case SetOp.Clear: sut.Clear(); oracle.Clear(); break;
                }
            }

            Assert.Equal(oracle.Count, sut.Count);
            for (int k = -8; k <= 24; k++)
                Assert.Equal(oracle.Contains(k), sut.Contains(k));
            Assert.True(oracle.SetEquals(sut.ToHashSet()));
        }, iter: 2000);
    }

    [Fact]
    public void IntSet_ShouldMatch_BclHashSet()
    {
        GenSetOps.Sample(ops =>
        {
            var sut = new IntSet();
            var oracle = new HashSet<int>();

            foreach (var (op, item) in ops)
            {
                switch (op)
                {
                    case SetOp.Add: Assert.Equal(oracle.Add(item), sut.TryAdd(item)); break;
                    case SetOp.Remove: Assert.Equal(oracle.Remove(item), sut.Remove(item)); break;
                    case SetOp.Clear: sut.Clear(); oracle.Clear(); break;
                }
            }

            Assert.Equal(oracle.Count, sut.Count);
            for (int k = -8; k <= 24; k++)
                Assert.Equal(oracle.Contains(k), sut.Contains(k));
        }, iter: 2000);
    }

    [Fact]
    public void LongSet_ShouldMatch_BclHashSet()
    {
        GenSetOps.Sample(ops =>
        {
            var sut = new LongSet();
            var oracle = new HashSet<long>();

            foreach (var (op, item) in ops)
            {
                long li = (long)item << 33 | (uint)item;
                switch (op)
                {
                    case SetOp.Add: Assert.Equal(oracle.Add(li), sut.TryAdd(li)); break;
                    case SetOp.Remove: Assert.Equal(oracle.Remove(li), sut.Remove(li)); break;
                    case SetOp.Clear: sut.Clear(); oracle.Clear(); break;
                }
            }

            Assert.Equal(oracle.Count, sut.Count);
        }, iter: 2000);
    }

    // ---- BloomFilter (probabilistic) vs HashSet<int> ------------------------

    // A Bloom filter has no Remove and may report false positives, so the model
    // property is one-directional: every element ever added (and not cleared away)
    // must still test present — the no-false-negatives guarantee. Absence is not
    // asserted because a true Contains may be a legitimate false positive.
    private enum BloomOp { Add, Clear }

    private static readonly Gen<List<(BloomOp, int)>> GenBloomOps =
        Gen.Select(
            Gen.Int[0, 99].Select(n => n < 92 ? BloomOp.Add : BloomOp.Clear),
            Gen.Int[-8, 24])
        .List[0, 120];

    [Fact]
    public void BloomFilter_ShouldHaveNoFalseNegatives_VsBclHashSet()
    {
        GenBloomOps.Sample(ops =>
        {
            // Sized comfortably for the tiny key domain so the test exercises the
            // add/clear machinery rather than over-fill behaviour.
            var sut = new BloomFilter<int, Int32WangNaiveHasher>(64);
            var oracle = new HashSet<int>();

            foreach (var (op, item) in ops)
            {
                switch (op)
                {
                    case BloomOp.Add: sut.Add(item); oracle.Add(item); break;
                    case BloomOp.Clear: sut.Clear(); oracle.Clear(); break;
                }
            }

            // No false negatives: every present element is reported present.
            foreach (int k in oracle)
                Assert.True(sut.Contains(k), $"false negative for {k}");
        }, iter: 2000);
    }

    // ---- BitSet (exact, deterministic) vs bool[] ----------------------------

    // A BitSet is an exact dense bit vector, so it admits a full two-directional
    // model: a bool[] of the same length is the oracle, and after any sequence of
    // single-bit operations every bit and the population count must match exactly.
    private enum BitOp { SetTrue, SetFalse, Flip, SetAllTrue, SetAllFalse, Clear }

    private const int BitSetLength = 137; // > 2 words with a partial tail word

    private static readonly Gen<List<(BitOp, int)>> GenBitOps =
        Gen.Select(
            Gen.Int[0, 99].Select(n =>
                n < 50 ? BitOp.SetTrue :
                n < 80 ? BitOp.SetFalse :
                n < 92 ? BitOp.Flip :
                n < 95 ? BitOp.SetAllTrue :
                n < 98 ? BitOp.SetAllFalse : BitOp.Clear),
            Gen.Int[0, BitSetLength - 1])
        .List[0, 160];

    [Fact]
    public void BitSet_ShouldMatch_BoolArrayModel()
    {
        GenBitOps.Sample(ops =>
        {
            var sut = new BitSet(BitSetLength);
            var oracle = new bool[BitSetLength];

            foreach (var (op, index) in ops)
            {
                switch (op)
                {
                    case BitOp.SetTrue: sut.Set(index, true); oracle[index] = true; break;
                    case BitOp.SetFalse: sut.Set(index, false); oracle[index] = false; break;
                    case BitOp.Flip: oracle[index] = sut.Flip(index); break;
                    case BitOp.SetAllTrue: sut.SetAll(true); Array.Fill(oracle, true); break;
                    case BitOp.SetAllFalse: sut.SetAll(false); Array.Fill(oracle, false); break;
                    case BitOp.Clear: sut.Clear(); Array.Clear(oracle); break;
                }
            }

            int expectedCount = 0;
            for (int i = 0; i < BitSetLength; i++)
            {
                Assert.Equal(oracle[i], sut[i]);
                if (oracle[i]) expectedCount++;
            }
            Assert.Equal(expectedCount, sut.Count);

            // The set-bit walk must yield exactly the true indices, in order.
            var expectedSetBits = new List<int>();
            for (int i = 0; i < BitSetLength; i++)
                if (oracle[i]) expectedSetBits.Add(i);
            Assert.Equal(expectedSetBits, sut.EnumerateSetBits().ToList());
        }, iter: 2000);
    }

    private static readonly Gen<(bool[], bool[])> GenBitPair =
        Gen.Select(Gen.Bool.Array[BitSetLength], Gen.Bool.Array[BitSetLength]);

    [Fact]
    public void BitSet_BulkOps_ShouldMatch_ElementwiseModel()
    {
        GenBitPair.Sample(pair =>
        {
            var (aRef, bRef) = pair;
            var b = new BitSet(bRef);

            var and = new BitSet(aRef).And(b);
            var or = new BitSet(aRef).Or(b);
            var xor = new BitSet(aRef).Xor(b);
            var not = new BitSet(aRef).Not();

            for (int i = 0; i < BitSetLength; i++)
            {
                Assert.Equal(aRef[i] && bRef[i], and[i]);
                Assert.Equal(aRef[i] || bRef[i], or[i]);
                Assert.Equal(aRef[i] ^ bRef[i], xor[i]);
                Assert.Equal(!aRef[i], not[i]);
            }
        }, iter: 1000);
    }

    // ---- HyperLogLog (probabilistic) vs HashSet<int> ------------------------

    // HyperLogLog estimates a distinct count, so the model property pairs it against a
    // HashSet whose exact Count is ground truth. Operating over a small key domain keeps
    // the estimator deep in its linear-counting regime, where the estimate is exact
    // apart from the rare register collision that can undercount by a register — so the
    // estimate must match the exact distinct count within a small slack and never
    // overcount beyond rounding. A *collision-free* hasher (Murmur3's bijective fmix32)
    // is required: the estimator counts distinct hash values, so a hasher that maps two
    // distinct keys to the same code (as the naive xor-fold hasher does on this domain)
    // would legitimately undercount and is not an estimator bug.
    private enum HllOp { Add, Clear }

    private static readonly Gen<List<(HllOp, int)>> GenHllOps =
        Gen.Select(
            Gen.Int[0, 99].Select(n => n < 92 ? HllOp.Add : HllOp.Clear),
            Gen.Int[-8, 24])
        .List[0, 120];

    [Fact]
    public void HyperLogLog_ShouldEstimateDistinctCount_VsBclHashSet()
    {
        GenHllOps.Sample(ops =>
        {
            var sut = new HyperLogLog<int, Int32Murmur3Hasher>();
            var oracle = new HashSet<int>();

            foreach (var (op, item) in ops)
            {
                switch (op)
                {
                    case HllOp.Add: sut.Add(item); oracle.Add(item); break;
                    case HllOp.Clear: sut.Clear(); oracle.Clear(); break;
                }
            }

            long estimate = sut.EstimateCardinality();
            int exact = oracle.Count;
            Assert.InRange(estimate, exact - 3, exact + 1);
        }, iter: 2000);
    }

    // ---- CountMinSketch (probabilistic) vs Dictionary<int,long> -------------

    // A Count-Min sketch never underestimates a frequency (counters only accumulate, and
    // collisions only inflate them), so the model property is one-directional: every
    // element's estimate must be at least its exact count in a Dictionary frequency table.
    // Overestimates are not asserted (a collision may legitimately inflate an estimate).
    private enum CmsOp { Add, AddWeighted, Clear }

    private static readonly Gen<List<(CmsOp, int, int)>> GenCmsOps =
        Gen.Select(
            Gen.Int[0, 99].Select(n =>
                n < 70 ? CmsOp.Add :
                n < 92 ? CmsOp.AddWeighted : CmsOp.Clear),
            Gen.Int[-8, 24],
            Gen.Int[1, 9])
        .List[0, 120];

    [Fact]
    public void CountMinSketch_ShouldNeverUnderestimate_VsBclFrequencyTable()
    {
        GenCmsOps.Sample(ops =>
        {
            var sut = new CountMinSketch<int, Int32WangNaiveHasher>();
            var oracle = new Dictionary<int, long>();

            foreach (var (op, item, weight) in ops)
            {
                switch (op)
                {
                    case CmsOp.Add:
                        sut.Add(item);
                        oracle[item] = oracle.GetValueOrDefault(item) + 1;
                        break;
                    case CmsOp.AddWeighted:
                        sut.Add(item, weight);
                        oracle[item] = oracle.GetValueOrDefault(item) + weight;
                        break;
                    case CmsOp.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                }
            }

            // Never underestimates: every element's estimate is at least its true count.
            foreach (var (k, count) in oracle)
                Assert.True(sut.EstimateCount(k) >= count, $"underestimate for {k}");
        }, iter: 2000);
    }

    // ---- MultiMap vs Dictionary<int,List<int>> ------------------------------

    private enum MultiOp { Add, RemoveValue, RemoveAll }

    private static readonly Gen<List<(MultiOp, int, int)>> GenMultiOps =
        Gen.Select(
            Gen.Int[0, 99].Select(n => n < 60 ? MultiOp.Add : n < 90 ? MultiOp.RemoveValue : MultiOp.RemoveAll),
            Gen.Int[-4, 12],
            Gen.Int[0, 30])
        .List[0, 120];

    [Fact]
    public void CelerityMultiMap_ShouldMatch_ListOfValuesModel()
    {
        GenMultiOps.Sample(ops =>
        {
            var sut = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
            var oracle = new Dictionary<int, List<int>>();

            foreach (var (op, key, val) in ops)
            {
                switch (op)
                {
                    case MultiOp.Add:
                        sut.Add(key, val);
                        if (!oracle.TryGetValue(key, out var list))
                            oracle[key] = list = new List<int>();
                        list.Add(val);
                        break;
                    case MultiOp.RemoveValue:
                    {
                        bool expected = oracle.TryGetValue(key, out var l) && l.Remove(val);
                        if (expected && oracle[key].Count == 0)
                            oracle.Remove(key);
                        Assert.Equal(expected, sut.Remove(key, val));
                        break;
                    }
                    case MultiOp.RemoveAll:
                        Assert.Equal(oracle.Remove(key), sut.RemoveAll(key));
                        break;
                }
            }

            Assert.Equal(oracle.Count, sut.Count);
            Assert.Equal(oracle.Values.Sum(l => l.Count), sut.ValueCount);
            for (int k = -4; k <= 12; k++)
            {
                bool present = oracle.TryGetValue(k, out var expectedList);
                Assert.Equal(present, sut.ContainsKey(k));
                // Group order must match insertion order on both sides.
                int[] actual = sut[k].ToArray();
                Assert.Equal(present ? expectedList!.ToArray() : Array.Empty<int>(), actual);
            }
        }, iter: 2000);
    }

    // ---- FrozenCelerityDictionary (build-once) vs Dictionary<string,int> ----

    [Fact]
    public void FrozenCelerityDictionary_ShouldMatch_BclDictionary()
    {
        // Distinct string keys (frozen rejects duplicates) paired with values.
        Gen<Dictionary<string, int>> genSource =
            Gen.Select(Gen.Int[0, 60], Gen.Int[0, 1_000])
               .List[0, 40]
               .Select(pairs =>
               {
                   var d = new Dictionary<string, int>();
                   foreach (var (k, v) in pairs)
                       d[$"key_{k}"] = v; // last-write-wins guarantees uniqueness
                   return d;
               });

        genSource.Sample(source =>
        {
            var frozen = new FrozenCelerityDictionary<int>(source);

            Assert.Equal(source.Count, frozen.Count);
            foreach (var kv in source)
            {
                Assert.True(frozen.TryGetValue(kv.Key, out int av));
                Assert.Equal(kv.Value, av);
                Assert.True(frozen.ContainsKey(kv.Key));
            }

            // Absent keys must miss.
            for (int k = 61; k <= 70; k++)
                Assert.False(frozen.ContainsKey($"key_{k}"));

            // Full enumeration round-trips to the same map.
            var roundTrip = frozen.ToDictionary(p => p.Key, p => p.Value);
            Assert.Equal(source.OrderBy(p => p.Key), roundTrip.OrderBy(p => p.Key));
        }, iter: 2000);
    }

    // ---- FrozenCeleritySet (build-once) vs HashSet<string> ------------------

    [Fact]
    public void FrozenCeleritySet_ShouldMatch_BclHashSet()
    {
        // A duplicate-rich source over a tiny element universe; the frozen set must
        // de-duplicate to the same distinct membership as the BCL HashSet oracle.
        Gen<List<string>> genSource =
            Gen.Int[0, 40].Select(k => $"item_{k}").List[0, 60];

        genSource.Sample(source =>
        {
            var frozen = new FrozenCeleritySet(source);
            var oracle = new HashSet<string>(source, StringComparer.Ordinal);

            Assert.Equal(oracle.Count, frozen.Count);
            foreach (string item in oracle)
                Assert.True(frozen.Contains(item));

            // Absent elements must miss.
            for (int k = 41; k <= 50; k++)
                Assert.False(frozen.Contains($"item_{k}"));

            // Full enumeration round-trips to the same membership.
            Assert.True(oracle.SetEquals(frozen.ToHashSet(StringComparer.Ordinal)));

            // Set-algebra parity against the oracle.
            Assert.True(frozen.SetEquals(oracle));
            Assert.True(frozen.IsSubsetOf(oracle));
            Assert.True(frozen.IsSupersetOf(oracle));
        }, iter: 2000);
    }

    // Asserts a CelerityDictionary is observably equal to a BCL oracle across the
    // full key domain, Count, and enumeration.
    private static void AssertDictEquivalent(
        CelerityDictionary<int, int, Int32WangNaiveHasher> sut,
        Dictionary<int, int> oracle)
    {
        Assert.Equal(oracle.Count, sut.Count);

        for (int k = -8; k <= 24; k++)
        {
            bool expected = oracle.TryGetValue(k, out int ev);
            bool actual = sut.TryGetValue(k, out int av);
            Assert.Equal(expected, actual);
            if (expected) Assert.Equal(ev, av);
        }

        var enumerated = new Dictionary<int, int>();
        foreach (var kv in sut)
            enumerated[kv.Key] = kv.Value;
        Assert.Equal(oracle.OrderBy(p => p.Key), enumerated.OrderBy(p => p.Key));
    }

    private static void AssertDictEquivalent(
        RobinHoodDictionary<int, int, Int32WangNaiveHasher> sut,
        Dictionary<int, int> oracle)
    {
        Assert.Equal(oracle.Count, sut.Count);

        for (int k = -8; k <= 24; k++)
        {
            bool expected = oracle.TryGetValue(k, out int ev);
            bool actual = sut.TryGetValue(k, out int av);
            Assert.Equal(expected, actual);
            if (expected) Assert.Equal(ev, av);
        }

        var enumerated = new Dictionary<int, int>();
        foreach (var kv in sut)
            enumerated[kv.Key] = kv.Value;
        Assert.Equal(oracle.OrderBy(p => p.Key), enumerated.OrderBy(p => p.Key));
    }

    private static void AssertDictEquivalent(
        SwissDictionary<int, int, Int32WangNaiveHasher> sut,
        Dictionary<int, int> oracle)
    {
        Assert.Equal(oracle.Count, sut.Count);

        for (int k = -8; k <= 24; k++)
        {
            bool expected = oracle.TryGetValue(k, out int ev);
            bool actual = sut.TryGetValue(k, out int av);
            Assert.Equal(expected, actual);
            if (expected) Assert.Equal(ev, av);
        }

        var enumerated = new Dictionary<int, int>();
        foreach (var kv in sut)
            enumerated[kv.Key] = kv.Value;
        Assert.Equal(oracle.OrderBy(p => p.Key), enumerated.OrderBy(p => p.Key));
    }

    private static void AssertDictEquivalent(
        HashCachingDictionary<int, int, Int32WangNaiveHasher> sut,
        Dictionary<int, int> oracle)
    {
        Assert.Equal(oracle.Count, sut.Count);

        for (int k = -8; k <= 24; k++)
        {
            bool expected = oracle.TryGetValue(k, out int ev);
            bool actual = sut.TryGetValue(k, out int av);
            Assert.Equal(expected, actual);
            if (expected) Assert.Equal(ev, av);
        }

        var enumerated = new Dictionary<int, int>();
        foreach (var kv in sut)
            enumerated[kv.Key] = kv.Value;
        Assert.Equal(oracle.OrderBy(p => p.Key), enumerated.OrderBy(p => p.Key));
    }

    private static void AssertDictEquivalent(
        PooledCelerityDictionary<int, int, Int32WangNaiveHasher> sut,
        Dictionary<int, int> oracle)
    {
        Assert.Equal(oracle.Count, sut.Count);

        for (int k = -8; k <= 24; k++)
        {
            bool expected = oracle.TryGetValue(k, out int ev);
            bool actual = sut.TryGetValue(k, out int av);
            Assert.Equal(expected, actual);
            if (expected) Assert.Equal(ev, av);
        }

        var enumerated = new Dictionary<int, int>();
        foreach (var kv in sut)
            enumerated[kv.Key] = kv.Value;
        Assert.Equal(oracle.OrderBy(p => p.Key), enumerated.OrderBy(p => p.Key));
    }
}
