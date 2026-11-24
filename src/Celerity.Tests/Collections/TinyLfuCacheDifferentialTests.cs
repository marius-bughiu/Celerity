using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="TinyLfuCache{TKey, TValue, THasher}"/> against
/// an independent reference W-TinyLFU built from three <see cref="List{T}"/> segments (index 0 is the
/// most-recently-used entry) and a <see cref="Dictionary{TKey, TValue}"/> of values. CsCheck generates
/// the capacity and a stream of operations (put, get, try-add, remove, peek, clear) and asserts after
/// every operation that the two agree on count, membership, every key's value, and the exact order of
/// the window, probation and protected segments.
///
/// <para>
/// The reference shares exactly one thing with the cache: the frequency estimates. Admission compares
/// two sketch estimates, and the sketch's collisions and saturation are part of its output, so the
/// reference asks the cache for <see cref="TinyLfuCache{TKey, TValue, THasher}.GetFrequencyEstimate(TKey)"/>
/// of the candidate and the victim just before each insert. Everything else (the segment shares, which
/// entry is the candidate and which the victim, the strict-win rule, promotion and demotion) is
/// re-derived from the documented policy. The sketch itself is checked against exact counts in
/// <c>TinyLfuCacheEvictionTests</c>.
/// </para>
/// <para>
/// Two capacity ranges run: 1–16, where the window holds one entry and the protected share runs from
/// zero up, and 200–260, where the window holds two and evictions need a key span to match.
/// </para>
/// </summary>
public class TinyLfuCacheDifferentialTests
{
    private sealed class OracleTinyLfu
    {
        private readonly int _cap;
        private readonly int _windowCap;
        private readonly int _mainCap;
        private readonly int _protectedCap;
        private readonly List<int> _window = new();
        private readonly List<int> _probation = new();
        private readonly List<int> _protected = new();
        private readonly Dictionary<int, int> _values = new();

        public OracleTinyLfu(int cap)
        {
            _cap = cap;
            _windowCap = Math.Max(1, cap / 100);
            _mainCap = cap - _windowCap;
            _protectedCap = _mainCap * 4 / 5;
        }

        public int Count => _values.Count;

        public void Put(int key, int value, Func<int, int> estimate)
        {
            if (_values.ContainsKey(key))
            {
                _values[key] = value;
                Use(key);
                return;
            }

            if (_values.Count == _cap)
            {
                int candidate = _window[^1];
                _window.RemoveAt(_window.Count - 1);
                int evicted = candidate;
                if (_mainCap > 0)
                {
                    int victim = _probation[^1];
                    if (estimate(candidate) > estimate(victim))
                    {
                        evicted = victim;
                        _probation.RemoveAt(_probation.Count - 1);
                        _probation.Insert(0, candidate);
                    }
                }
                _values.Remove(evicted);
            }
            else if (_window.Count == _windowCap)
            {
                int overflow = _window[^1];
                _window.RemoveAt(_window.Count - 1);
                _probation.Insert(0, overflow);
            }

            _values[key] = value;
            _window.Insert(0, key);
        }

        private void Use(int key)
        {
            if (_window.Remove(key))
            {
                _window.Insert(0, key);
            }
            else if (_protected.Remove(key))
            {
                _protected.Insert(0, key);
            }
            else
            {
                _probation.Remove(key);
                _protected.Insert(0, key);
                if (_protected.Count > _protectedCap)
                {
                    int demoted = _protected[^1];
                    _protected.RemoveAt(_protected.Count - 1);
                    _probation.Insert(0, demoted);
                }
            }
        }

        public bool TryGet(int key, out int value)
        {
            if (_values.TryGetValue(key, out value))
            {
                Use(key);
                return true;
            }
            return false;
        }

        public bool TryAdd(int key, int value, Func<int, int> estimate)
        {
            if (_values.ContainsKey(key))
                return false;
            Put(key, value, estimate);
            return true;
        }

        public bool Remove(int key)
        {
            if (!_values.Remove(key))
                return false;
            if (!_window.Remove(key) && !_probation.Remove(key))
                _protected.Remove(key);
            return true;
        }

        public void Clear()
        {
            _window.Clear();
            _probation.Clear();
            _protected.Clear();
            _values.Clear();
        }

        public bool ContainsKey(int key) => _values.ContainsKey(key);

        public bool TryPeek(int key, out int value) => _values.TryGetValue(key, out value);

        public List<int> KeysInOrder() => [.. _window, .. _probation, .. _protected];
    }

    private enum Op { AddOrUpdate, TryGet, TryAdd, Remove, TryPeek, Clear }

    // Weighted toward put and get, so entries move between all three segments; clear is rare, since
    // it resets everything the script built up, the sketch included.
    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 199].Select(n => n < 70 ? Op.AddOrUpdate
                                  : n < 140 ? Op.TryGet
                                  : n < 160 ? Op.TryAdd
                                  : n < 178 ? Op.Remove
                                  : n < 198 ? Op.TryPeek
                                  : Op.Clear);

    // The key span is the capacity plus a margin, so the cache fills, evicts and re-admits. Key 0 is
    // default(int), kept in range so the default key stays covered.
    private static Gen<(int Capacity, List<(Op Kind, int Key, int Value)> Ops)> GenScript(int minCap, int maxCap, int maxOps)
        => Gen.Int[minCap, maxCap].SelectMany(cap =>
            Gen.Select(GenKind, Gen.Int[0, cap + Math.Max(4, cap / 8)], Gen.Int).List[0, maxOps]
               .Select(ops => (cap, ops)));

    [Fact]
    public void TinyLfuCache_ShouldMatch_AReferenceTinyLfu_AtSmallCapacities()
        => RunAgainstOracle<Int32WangHasher>(GenScript(1, 16, 400), iter: 60);

    [Fact]
    public void TinyLfuCache_ShouldMatch_AReferenceTinyLfu_WithATwoEntryWindow()
        => RunAgainstOracle<Int32WangHasher>(GenScript(200, 260, 2_000), iter: 15);

    // Every key shares one chain of the key index, and keys of the same parity share one set of sketch
    // counters, so many admission contests tie and are broken in the victim's favour, and lookups,
    // evictions and removals all walk the same chain.
    [Fact]
    public void TinyLfuCache_ShouldMatch_AReferenceTinyLfu_WhenEveryKeyCollides()
        => RunAgainstOracle<CacheCollidingHasher>(GenScript(1, 16, 400), iter: 40);

    private static void RunAgainstOracle<THasher>(
        Gen<(int Capacity, List<(Op Kind, int Key, int Value)> Ops)> gen, int iter)
        where THasher : struct, IHashProvider<int>
    {
        gen.Sample(script =>
        {
            int capacity = script.Capacity;
            int keySpan = capacity + Math.Max(4, capacity / 8) + 1;
            var cache = new TinyLfuCache<int, int, THasher>(capacity);
            var oracle = new OracleTinyLfu(capacity);
            Func<int, int> estimate = cache.GetFrequencyEstimate;

            foreach (var (kind, key, value) in script.Ops)
            {
                switch (kind)
                {
                    case Op.AddOrUpdate:
                        oracle.Put(key, value, estimate);
                        cache.AddOrUpdate(key, value);
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
                        Assert.Equal(oracle.TryAdd(key, value, estimate), cache.TryAdd(key, value));
                        break;

                    case Op.Remove:
                        Assert.Equal(oracle.Remove(key), cache.Remove(key));
                        break;

                    case Op.TryPeek:
                    {
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

                for (int k = 0; k < keySpan; k++)
                {
                    Assert.Equal(oracle.ContainsKey(k), cache.ContainsKey(k));
                    if (oracle.TryPeek(k, out int expected))
                    {
                        Assert.True(cache.TryPeek(k, out int actual));
                        Assert.Equal(expected, actual);
                    }
                }

                // The exact segment order: window, probation, protected, each newest first.
                var cacheOrder = new List<int>();
                foreach (var kvp in cache)
                    cacheOrder.Add(kvp.Key);
                Assert.Equal(oracle.KeysInOrder(), cacheOrder);
            }
        }, iter: iter);
    }
}
