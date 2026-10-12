using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

// Differential coverage for SlotMap<TValue> against a BCL Dictionary<SlotKey, int> oracle.
//
// CsCheck generates the operation script — add / remove / remove-with-out-value / lookup /
// indexer-set / contains-value / clear / capacity churn / CopyTo round trip — and a
// SlotMap<int> is driven through it in lockstep with the oracle. Every operation's *return
// value* is compared as well as the resulting contents, so a Remove that hands back the wrong
// value, or a lookup that resolves a retired key, fails the property and shrinks to the
// operations that caused it rather than to a seed.
//
// What a slot map adds to the ordinary dictionary contract is that the oracle cannot mint keys:
// the map does. So the harness also remembers every key the map has *ever* issued, and lookups,
// removes and overwrites pick from that whole history (plus the default key). A retired key
// must read as absent forever, and no Add may hand out a key that was issued before — the two
// halves of the generational guarantee. Removal-heavy scripts keep the free list busy, which is
// where a slot gets reused and where the version check is the only thing standing between an
// old key and the new occupant.
public class SlotMapDifferentialTests
{
    private enum Op
    {
        Add, Add2, Remove, RemoveOutValue, Lookup, IndexerSet, ContainsValueProbe,
        Clear, EnsureCapacity, CopyToRoundTrip,
    }

    private static readonly Gen<Op> GenKind = Gen.Int[0, 9].Select(n => (Op)n);

    // pick indexes the history of issued keys (modulo its length); a pick of -1 stands for the
    // default key, which no map ever issues.
    private static readonly Gen<(Op Kind, int Pick, int Value, int Capacity)> GenOp =
        Gen.Select(GenKind, Gen.Int[-1, 200], Gen.Int[-5, 50], Gen.Int[0, 64]);

    [Fact]
    public void SlotMap_ShouldMatch_ADictionary()
    {
        GenOp.List[0, 300].Sample(ops =>
        {
            var map = new SlotMap<int>();
            var oracle = new Dictionary<SlotKey, int>();
            var issued = new List<SlotKey>();
            var everIssued = new HashSet<SlotKey>();
            int step = 0;

            foreach (var (kind, pick, value, capacity) in ops)
            {
                SlotKey key = pick < 0 || issued.Count == 0 ? default : issued[pick % issued.Count];

                switch (kind)
                {
                    case Op.Add:
                    case Op.Add2:
                    {
                        SlotKey added = map.Add(value);
                        Assert.True(everIssued.Add(added),
                            $"step {step}: Add re-issued key {added}, which an earlier Add already handed out");
                        Assert.NotEqual(default, added);
                        issued.Add(added);
                        oracle.Add(added, value);
                        AssertSame(map, oracle, issued, step);
                        break;
                    }

                    case Op.Remove:
                        Assert.Equal(oracle.Remove(key), map.Remove(key));
                        AssertSame(map, oracle, issued, step);
                        break;

                    case Op.RemoveOutValue:
                    {
                        bool expected = oracle.Remove(key, out int expectedValue);
                        bool actual = map.Remove(key, out int actualValue);
                        Assert.Equal(expected, actual);
                        Assert.Equal(expectedValue, actualValue);
                        AssertSame(map, oracle, issued, step);
                        break;
                    }

                    case Op.Lookup:
                    {
                        bool expected = oracle.TryGetValue(key, out int expectedValue);

                        Assert.Equal(expected, map.ContainsKey(key));
                        Assert.Equal(expected, map.TryGetValue(key, out int actualValue));
                        Assert.Equal(expectedValue, actualValue);

                        if (expected)
                            Assert.Equal(expectedValue, map[key]);
                        else
                            Assert.Throws<KeyNotFoundException>(() => map[key]);

                        break;
                    }

                    case Op.IndexerSet:
                        // Overwrite only: a slot map cannot insert under a key, so a retired or
                        // default key throws and leaves the map untouched.
                        if (oracle.ContainsKey(key))
                        {
                            map[key] = value;
                            oracle[key] = value;
                        }
                        else
                        {
                            Assert.Throws<KeyNotFoundException>(() => map[key] = value);
                        }

                        AssertSame(map, oracle, issued, step);
                        break;

                    case Op.ContainsValueProbe:
                        Assert.Equal(oracle.ContainsValue(value), map.ContainsValue(value));
                        break;

                    case Op.Clear:
                        map.Clear();
                        oracle.Clear();
                        AssertSame(map, oracle, issued, step);
                        break;

                    case Op.EnsureCapacity:
                        Assert.True(map.EnsureCapacity(capacity) >= capacity);
                        AssertSame(map, oracle, issued, step);
                        break;

                    case Op.CopyToRoundTrip:
                    {
                        var entries = new KeyValuePair<SlotKey, int>[oracle.Count];
                        map.CopyTo(entries, 0);
                        var copied = new Dictionary<SlotKey, int>();
                        foreach (KeyValuePair<SlotKey, int> entry in entries)
                            Assert.True(copied.TryAdd(entry.Key, entry.Value),
                                $"step {step}: CopyTo emitted key {entry.Key} twice");

                        Assert.Equal(oracle, copied);
                        break;
                    }
                }

                step++;
            }
        }, iter: 60);
    }

    private static void AssertSame(
        SlotMap<int> actual, Dictionary<SlotKey, int> expected, List<SlotKey> issued, int step)
    {
        Assert.True(expected.Count == actual.Count,
            $"step {step}: count mismatch — expected {expected.Count}, got {actual.Count}");

        // Every key ever issued: the live ones resolve to the oracle's value, the retired ones are
        // absent — however many times their slots have been reused since.
        foreach (SlotKey key in issued)
        {
            bool live = expected.TryGetValue(key, out int expectedValue);
            Assert.True(live == actual.TryGetValue(key, out int value),
                $"step {step}: key {key} — expected live={live}");
            Assert.True(expectedValue == value,
                $"step {step}: key {key} — expected {expectedValue}, got {value}");
        }

        Assert.False(actual.ContainsKey(default), $"step {step}: the default key resolved");

        foreach (KeyValuePair<SlotKey, int> entry in actual)
            Assert.True(expected.TryGetValue(entry.Key, out int value) && value == entry.Value,
                $"step {step}: actual has extra or wrong entry {entry.Key} = {entry.Value}");
    }
}
