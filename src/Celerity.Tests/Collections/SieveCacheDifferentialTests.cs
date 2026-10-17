using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="SieveCache{TKey, TValue, THasher}"/> against
/// an independent reference SIEVE built from a <see cref="List{T}"/> (index 0 is the newest entry),
/// a <see cref="Dictionary{TKey, TValue}"/> of values, a <see cref="HashSet{T}"/> of visited keys,
/// and a hand held as a key rather than a slot. CsCheck generates the capacity and a stream of
/// operations — put, get, try-add, remove, peek, clear — and asserts after every single operation
/// that the two agree on count, membership, every key's value, and the exact newest-to-oldest queue
/// order. Because SIEVE is deterministic, membership after each insert pins the eviction choice
/// exactly, including where the hand resumed and whether it wrapped.
///
/// <para>
/// The capacity is generated alongside the script because the two interact: a capacity of one makes
/// every insert a full lap of the hand, and a capacity near the key span makes evictions rare and
/// hits common. Shrinking both together turns a failure into a readable case.
/// </para>
/// </summary>
public class SieveCacheDifferentialTests
{
    // A reference SIEVE with the textbook definition and none of the slot machinery: a hit or an
    // overwrite sets the key's visited flag; an insert at capacity sweeps from the hand (or the
    // oldest entry) toward the newest, clearing flags, wrapping past the newest, and evicts the first
    // unvisited key, leaving the hand on its newer neighbour; peek/contains change nothing.
    private sealed class OracleSieve
    {
        private readonly int _cap;
        private readonly List<int> _order = new(); // [0] = newest, [^1] = oldest
        private readonly Dictionary<int, int> _values = new();
        private readonly HashSet<int> _visited = new();
        private int? _hand;

        public OracleSieve(int cap) => _cap = cap;

        public int Count => _values.Count;

        public void Put(int key, int value)
        {
            if (_values.ContainsKey(key))
            {
                _values[key] = value;
                _visited.Add(key);
                return;
            }
            if (_values.Count == _cap)
                Evict();
            _values[key] = value;
            _order.Insert(0, key);
        }

        private void Evict()
        {
            int i = _hand is int h ? _order.IndexOf(h) : _order.Count - 1;
            while (_visited.Remove(_order[i]))
            {
                i--;
                if (i < 0)
                    i = _order.Count - 1;
            }
            int victim = _order[i];
            _hand = i > 0 ? _order[i - 1] : null;
            _order.RemoveAt(i);
            _values.Remove(victim);
        }

        public bool TryGet(int key, out int value)
        {
            if (_values.TryGetValue(key, out value))
            {
                _visited.Add(key);
                return true;
            }
            return false;
        }

        public bool TryAdd(int key, int value)
        {
            if (_values.ContainsKey(key))
                return false;
            Put(key, value);
            return true;
        }

        public bool Remove(int key)
        {
            if (!_values.Remove(key))
                return false;
            int i = _order.IndexOf(key);
            if (_hand == key)
                _hand = i > 0 ? _order[i - 1] : null;
            _order.RemoveAt(i);
            _visited.Remove(key);
            return true;
        }

        public void Clear()
        {
            _order.Clear();
            _values.Clear();
            _visited.Clear();
            _hand = null;
        }

        public bool ContainsKey(int key) => _values.ContainsKey(key);

        public bool TryPeek(int key, out int value) => _values.TryGetValue(key, out value);

        public List<int> KeysNewestToOldest() => new(_order);
    }

    private enum Op { AddOrUpdate, TryGet, TryAdd, Remove, TryPeek, Clear }

    // Deliberately smaller than the operation count, so hits, overwrites and evictions collide on
    // the same keys. Key 0 is default(int), which the hash table stores out of band.
    private const int KeySpan = 20;

    // Weighted toward put and get, so the visited bits the hand sweeps are dense; clear is rare,
    // since it resets everything the script built up.
    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 99].Select(n => n < 35 ? Op.AddOrUpdate
                                 : n < 65 ? Op.TryGet
                                 : n < 75 ? Op.TryAdd
                                 : n < 87 ? Op.Remove
                                 : n < 98 ? Op.TryPeek
                                 : Op.Clear);

    private static readonly Gen<(Op Kind, int Key, int Value)> GenOp =
        Gen.Select(GenKind, Gen.Int[0, KeySpan - 1], Gen.Int);

    private static readonly Gen<(int Capacity, List<(Op Kind, int Key, int Value)> Ops)> GenScript =
        Gen.Select(Gen.Int[1, 16], GenOp.List[0, 300]);

    [Fact]
    public void SieveCache_ShouldMatch_AReferenceSieve()
    {
        GenScript.Sample(script =>
        {
            int capacity = script.Capacity;
            var cache = new SieveCache<int, int, Int32WangHasher>(capacity);
            var oracle = new OracleSieve(capacity);

            foreach (var (kind, key, value) in script.Ops)
            {
                switch (kind)
                {
                    case Op.AddOrUpdate:
                        cache.AddOrUpdate(key, value);
                        oracle.Put(key, value);
                        break;

                    case Op.TryGet:
                    {
                        bool expected = oracle.TryGet(key, out int expectedValue);
                        Assert.Equal(expected, cache.TryGet(key, out int actualValue));
                        if (expected)
                            Assert.Equal(expectedValue, actualValue);
                        break;
                    }

                    case Op.TryAdd:
                        Assert.Equal(oracle.TryAdd(key, value), cache.TryAdd(key, value));
                        break;

                    case Op.Remove:
                        Assert.Equal(oracle.Remove(key), cache.Remove(key));
                        break;

                    case Op.TryPeek:
                    {
                        // Peek must not mark the entry visited in either implementation.
                        bool expected = oracle.TryPeek(key, out int expectedValue);
                        Assert.Equal(expected, cache.TryPeek(key, out int actualValue));
                        if (expected)
                            Assert.Equal(expectedValue, actualValue);
                        break;
                    }

                    case Op.Clear:
                        cache.Clear();
                        oracle.Clear();
                        break;
                }

                // Full-state agreement after every operation.
                Assert.Equal(oracle.Count, cache.Count);
                Assert.True(cache.Count <= capacity);

                for (int k = 0; k < KeySpan; k++)
                {
                    Assert.Equal(oracle.ContainsKey(k), cache.ContainsKey(k));
                    if (oracle.TryPeek(k, out int expected))
                    {
                        Assert.True(cache.TryPeek(k, out int actual));
                        Assert.Equal(expected, actual);
                    }
                }

                // The exact queue order: insertion order, untouched by hits.
                var cacheOrder = new List<int>();
                foreach (var kvp in cache)
                    cacheOrder.Add(kvp.Key);
                Assert.Equal(oracle.KeysNewestToOldest(), cacheOrder);
            }
        }, iter: 40);
    }
}
