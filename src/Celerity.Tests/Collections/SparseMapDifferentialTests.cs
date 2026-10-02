using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

// Issue #473: differential coverage for SparseMap<TValue> against a BCL
// Dictionary<int, int> oracle.
//
// CsCheck generates the operation script — add / try-add / indexer-set / remove /
// remove-with-out-value / lookup / clear-and-reuse / capacity churn — and a
// SparseMap<int>(Universe) is driven through it in lockstep with the oracle. Every
// operation's *return value* is compared as well as the resulting contents, so a
// TryAdd that lies about whether it inserted, or a Remove that hands back the wrong
// value, fails the property and shrinks to the operations that caused it rather than
// to a seed.
//
// The small, dense universe forces frequent key reuse, dense-array growth, swap-removes
// and — crucially — clear-then-reuse cycles, which stress the stale-sparse round-trip
// membership check that keeps Clear off the O(capacity) path. The out-of-range probes
// cover the read surface's "absent" answer and the write surface's throw in the same
// script.
public class SparseMapDifferentialTests
{
    private const int Universe = 40;

    private enum Op
    {
        Add, TryAdd, IndexerSet, Remove, RemoveOutValue, Lookup, ContainsValueProbe,
        ClearAndReuse, EnsureCapacity, TrimExcess, OutOfRangeWrite, CopyToRoundTrip,
    }

    private static readonly Gen<Op> GenKind = Gen.Int[0, 11].Select(n => (Op)n);

    // key stays inside the universe; probe reaches outside it on both sides so the
    // out-of-range answers are generated rather than hand-placed.
    private static readonly Gen<(Op Kind, int Key, int Value, int Probe, int Capacity)> GenOp =
        Gen.Select(
            GenKind,
            Gen.Int[0, Universe - 1],
            Gen.Int[-5, 50],
            Gen.Int[-2, Universe + 1],
            Gen.Int[0, Universe]);

    [Fact]
    public void SparseMap_ShouldMatch_ADictionary()
    {
        GenOp.List[0, 300].Sample(ops =>
        {
            var map = new SparseMap<int>(Universe);
            var oracle = new Dictionary<int, int>();
            int step = 0;

            foreach (var (kind, key, value, probe, capacity) in ops)
            {
                switch (kind)
                {
                    case Op.Add:
                        if (oracle.ContainsKey(key))
                        {
                            Assert.Throws<ArgumentException>(() => map.Add(key, value));
                        }
                        else
                        {
                            map.Add(key, value);
                            oracle.Add(key, value);
                        }

                        AssertSame(map, oracle, step);
                        break;

                    case Op.TryAdd:
                        Assert.Equal(oracle.TryAdd(key, value), map.TryAdd(key, value));
                        AssertSame(map, oracle, step);
                        break;

                    case Op.IndexerSet:
                        map[key] = value;
                        oracle[key] = value;
                        AssertSame(map, oracle, step);
                        break;

                    case Op.Remove:
                        Assert.Equal(oracle.Remove(key), map.Remove(key));
                        AssertSame(map, oracle, step);
                        break;

                    case Op.RemoveOutValue:
                    {
                        bool expected = oracle.Remove(key, out int expectedValue);
                        bool actual = map.Remove(key, out int actualValue);
                        Assert.Equal(expected, actual);
                        Assert.Equal(expectedValue, actualValue);
                        AssertSame(map, oracle, step);
                        break;
                    }

                    case Op.Lookup:
                    {
                        // Including out-of-range probes, which must read as absent rather than
                        // throw — the read surface's half of the bounded-universe contract.
                        bool inRange = probe >= 0 && probe < Universe;
                        int expectedValue = 0;
                        bool expected = inRange && oracle.TryGetValue(probe, out expectedValue);

                        Assert.Equal(expected, map.ContainsKey(probe));
                        Assert.Equal(expected, map.TryGetValue(probe, out int actualValue));
                        Assert.Equal(expectedValue, actualValue);

                        if (expected)
                            Assert.Equal(oracle[probe], map[probe]);
                        else
                            Assert.Throws<KeyNotFoundException>(() => map[probe]);

                        break;
                    }

                    case Op.ContainsValueProbe:
                        Assert.Equal(oracle.ContainsValue(value), map.ContainsValue(value));
                        break;

                    case Op.ClearAndReuse:
                        map.Clear();
                        oracle.Clear();
                        AssertSame(map, oracle, step);

                        // Re-add so the sparse array holds live-again slots that were stale a
                        // moment ago.
                        map[key] = value;
                        oracle[key] = value;
                        AssertSame(map, oracle, step);
                        break;

                    case Op.EnsureCapacity:
                        Assert.True(map.EnsureCapacity(capacity) >= Math.Min(capacity, Universe));
                        AssertSame(map, oracle, step);
                        break;

                    case Op.TrimExcess:
                        // Trim to a capacity the map can legally take: never below the count and
                        // never above the universe.
                        map.TrimExcess(Math.Max(capacity, map.Count));
                        AssertSame(map, oracle, step);
                        break;

                    case Op.OutOfRangeWrite:
                        // The write surface's half: a key outside [0, Universe) throws and leaves
                        // the map untouched.
                        if (probe < 0 || probe >= Universe)
                        {
                            Assert.Throws<ArgumentOutOfRangeException>(() => map.Add(probe, value));
                            Assert.Throws<ArgumentOutOfRangeException>(() => map.TryAdd(probe, value));
                            Assert.Throws<ArgumentOutOfRangeException>(() => map[probe] = value);
                            AssertSame(map, oracle, step);
                        }

                        break;

                    case Op.CopyToRoundTrip:
                    {
                        var entries = new KeyValuePair<int, int>[oracle.Count];
                        map.CopyTo(entries, 0);
                        var copied = new Dictionary<int, int>();
                        foreach (KeyValuePair<int, int> entry in entries)
                            Assert.True(copied.TryAdd(entry.Key, entry.Value),
                                $"step {step}: CopyTo emitted key {entry.Key} twice");

                        Assert.Equal(oracle.Count, copied.Count);
                        foreach (KeyValuePair<int, int> entry in oracle)
                            Assert.Equal(entry.Value, copied[entry.Key]);

                        break;
                    }
                }

                step++;
            }
        }, iter: 40);
    }

    private static void AssertSame(SparseMap<int> actual, Dictionary<int, int> expected, int step)
    {
        Assert.True(expected.Count == actual.Count,
            $"step {step}: count mismatch — expected {expected.Count}, got {actual.Count}");

        foreach (KeyValuePair<int, int> entry in expected)
        {
            Assert.True(actual.TryGetValue(entry.Key, out int value),
                $"step {step}: actual missing key {entry.Key}");
            Assert.True(entry.Value == value,
                $"step {step}: key {entry.Key} — expected {entry.Value}, got {value}");
        }

        foreach (KeyValuePair<int, int> entry in actual)
            Assert.True(expected.ContainsKey(entry.Key),
                $"step {step}: actual has extra key {entry.Key}");
    }
}
