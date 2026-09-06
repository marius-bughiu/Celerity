using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="LruCache{TKey, TValue, THasher}"/> against an
/// independent reference LRU built from a <see cref="Dictionary{TKey, TValue}"/> plus a
/// <see cref="LinkedList{T}"/>. CsCheck generates the capacity and the stream of operations — put,
/// get, try-add, remove, peek — and asserts that after every single operation the two agree on
/// count, membership, every key's value, and — the property that actually pins down the eviction
/// policy — the exact most-recently-used&#8594;least-recently-used ordering. This is the strongest
/// guard against a recency-list or free-slot bug that only surfaces after many evictions.
///
/// <para>
/// The capacity is generated alongside the script because the two interact: a capacity of one makes
/// every insert an eviction, and a capacity near the key span makes evictions rare and updates
/// common. Shrinking both together is what turns a failure into a readable case — typically a
/// capacity of one or two and a handful of operations.
/// </para>
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

    private enum Op { AddOrUpdate, TryGet, TryAdd, Remove, TryPeek }

    // Deliberately smaller than the operation count, so updates and evictions collide on the same
    // keys instead of the cache filling once with distinct ones. Key 0 is default(int), which is
    // the out-of-band slot the hash table stores separately.
    private const int KeySpan = 20;

    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 5].Select(n => n < 2 ? Op.AddOrUpdate : (Op)(n - 1));

    private static readonly Gen<(Op Kind, int Key, int Value)> GenOp =
        Gen.Select(GenKind, Gen.Int[0, KeySpan - 1], Gen.Int);

    private static readonly Gen<(int Capacity, List<(Op Kind, int Key, int Value)> Ops)> GenScript =
        Gen.Select(Gen.Int[1, 16], GenOp.List[0, 300]);

    [Fact]
    public void LruCache_ShouldMatch_AReferenceLru()
    {
        GenScript.Sample(script =>
        {
            int capacity = script.Capacity;
            var cache = new LruCache<int, int, Int32WangHasher>(capacity);
            var oracle = new OracleLru(capacity);

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
                        // Peek must not perturb recency in either implementation.
                        bool expected = oracle.TryPeek(key, out int expectedValue);
                        Assert.Equal(expected, cache.TryPeek(key, out int actualValue));
                        if (expected)
                            Assert.Equal(expectedValue, actualValue);
                        break;
                    }
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
        }, iter: 40);
    }
}
