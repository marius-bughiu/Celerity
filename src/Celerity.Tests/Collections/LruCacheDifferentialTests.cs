using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Deterministic, seeded differential coverage for <see cref="LruCache{TKey, TValue, THasher}"/>.
/// Each seed drives the same random stream of operations (put, get, try-add, remove, peek) into the
/// cache and into an independent reference LRU built from a <see cref="Dictionary{TKey, TValue}"/>
/// plus a <see cref="LinkedList{T}"/>, then asserts that after every single operation the two agree
/// on count, membership, every key's value, and — the property that actually pins down the eviction
/// policy — the exact most-recently-used&#8594;least-recently-used ordering. This is the strongest
/// guard against a recency-list or free-slot bug that only surfaces after many evictions.
/// </summary>
public class LruCacheDifferentialTests
{
    // A reference LRU with textbook semantics: get and put are "uses" that promote to the front
    // (MRU); an insert at capacity drops the back (LRU); peek/contains do not reorder.
    private sealed class OracleLru
    {
        private readonly int _cap;
        private readonly Dictionary<int, int> _values = new();
        private readonly Dictionary<int, LinkedListNode<int>> _nodes = new();
        private readonly LinkedList<int> _order = new(); // First = MRU, Last = LRU

        public OracleLru(int cap) => _cap = cap;

        public int Count => _values.Count;

        private void Promote(int key)
        {
            LinkedListNode<int> n = _nodes[key];
            _order.Remove(n);
            _order.AddFirst(n);
        }

        public void Put(int key, int value)
        {
            if (_values.ContainsKey(key))
            {
                _values[key] = value;
                Promote(key);
                return;
            }
            if (_values.Count == _cap)
            {
                int lru = _order.Last!.Value;
                _order.RemoveLast();
                _values.Remove(lru);
                _nodes.Remove(lru);
            }
            _values[key] = value;
            _nodes[key] = _order.AddFirst(key);
        }

        public bool TryGet(int key, out int value)
        {
            if (_values.TryGetValue(key, out value))
            {
                Promote(key);
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
            if (!_nodes.TryGetValue(key, out LinkedListNode<int>? n))
                return false;
            _order.Remove(n);
            _values.Remove(key);
            _nodes.Remove(key);
            return true;
        }

        public bool ContainsKey(int key) => _values.ContainsKey(key);

        public bool TryPeek(int key, out int value) => _values.TryGetValue(key, out value);

        public List<int> KeysMruToLru() => new(_order);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(16)]
    public void RandomOps_MatchReferenceLru(int capacity)
    {
        const int Seeds = 40;
        const int OpsPerSeed = 400;
        const int KeySpan = 20; // deliberately smaller than the op count so updates/evictions collide

        for (int seed = 0; seed < Seeds; seed++)
        {
            var rng = new Random(seed * 7919 + capacity);
            var cache = new LruCache<int, int, Int32WangHasher>(capacity);
            var oracle = new OracleLru(capacity);

            for (int op = 0; op < OpsPerSeed; op++)
            {
                int key = rng.Next(0, KeySpan); // includes 0 == default(int) to hit the out-of-band path
                int value = rng.Next();
                int choice = rng.Next(6);

                switch (choice)
                {
                    case 0:
                    case 1:
                        cache.AddOrUpdate(key, value);
                        oracle.Put(key, value);
                        break;
                    case 2:
                        bool cg = cache.TryGet(key, out int cv);
                        bool og = oracle.TryGet(key, out int ov);
                        Assert.Equal(og, cg);
                        if (og) Assert.Equal(ov, cv);
                        break;
                    case 3:
                        Assert.Equal(oracle.TryAdd(key, value), cache.TryAdd(key, value));
                        break;
                    case 4:
                        Assert.Equal(oracle.Remove(key), cache.Remove(key));
                        break;
                    case 5:
                        // Peek must not perturb recency in either implementation.
                        bool cp = cache.TryPeek(key, out int cpv);
                        bool opp = oracle.TryPeek(key, out int opv);
                        Assert.Equal(opp, cp);
                        if (opp) Assert.Equal(opv, cpv);
                        break;
                }

                // Full-state agreement after every operation.
                Assert.Equal(oracle.Count, cache.Count);

                for (int k = 0; k < KeySpan; k++)
                {
                    Assert.Equal(oracle.ContainsKey(k), cache.ContainsKey(k));
                    if (oracle.TryPeek(k, out int expected))
                    {
                        Assert.True(cache.TryPeek(k, out int actual));
                        Assert.Equal(expected, actual);
                    }
                }

                // The exact eviction order — the heart of the contract.
                var cacheOrder = new List<int>();
                foreach (var kvp in cache)
                    cacheOrder.Add(kvp.Key);
                Assert.Equal(oracle.KeysMruToLru(), cacheOrder);
            }
        }
    }
}
