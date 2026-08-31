using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Deterministic, seeded differential coverage for <see cref="LfuCache{TKey, TValue, THasher}"/>.
/// Each seed drives the same random stream of operations (put, get, try-add, remove, peek) into the
/// cache and into an independent reference LFU that keeps a use count and a monotonic last-use stamp
/// per key and derives the order by sorting — the definition of the policy, with none of the bucket
/// machinery. After every single operation the two are asserted to agree on count, membership, every
/// key's value, every key's frequency, and — the property that actually pins down the eviction
/// policy — the exact most-frequently-used&#8594;least-frequently-used ordering including the
/// least-recently-used tie-break. This is the strongest guard against a bucket-chain or free-slot bug
/// that only surfaces after many evictions.
/// </summary>
public class LfuCacheDifferentialTests
{
    // A reference LFU with textbook semantics and no data structure worth the name: every entry
    // carries its use count and the stamp of its most recent use, and the eviction order is simply
    // "lowest count first, oldest stamp breaking ties". Get and put are uses; peek/contains are not.
    private sealed class OracleLfu
    {
        private readonly int _cap;
        private readonly Dictionary<int, int> _values = new();
        private readonly Dictionary<int, long> _freq = new();
        private readonly Dictionary<int, long> _stamp = new();
        private long _clock;

        public OracleLfu(int cap) => _cap = cap;

        public int Count => _values.Count;

        private void Use(int key)
        {
            _freq[key] += 1;
            _stamp[key] = ++_clock;
        }

        private int Victim()
        {
            // Lowest frequency wins; among equals the oldest stamp (least recently used) wins.
            int victim = -1;
            long bestFreq = long.MaxValue;
            long bestStamp = long.MaxValue;
            foreach (int key in _values.Keys)
            {
                long f = _freq[key];
                long s = _stamp[key];
                if (f < bestFreq || (f == bestFreq && s < bestStamp))
                {
                    victim = key;
                    bestFreq = f;
                    bestStamp = s;
                }
            }
            return victim;
        }

        public void Put(int key, int value)
        {
            if (_values.ContainsKey(key))
            {
                _values[key] = value;
                Use(key);
                return;
            }
            if (_values.Count == _cap)
            {
                int victim = Victim();
                _values.Remove(victim);
                _freq.Remove(victim);
                _stamp.Remove(victim);
            }
            _values[key] = value;
            _freq[key] = 1;
            _stamp[key] = ++_clock;
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
            _freq.Remove(key);
            _stamp.Remove(key);
            return true;
        }

        public bool ContainsKey(int key) => _values.ContainsKey(key);

        public bool TryPeek(int key, out int value) => _values.TryGetValue(key, out value);

        public bool TryGetFrequency(int key, out long frequency) => _freq.TryGetValue(key, out frequency);

        public int LeastFrequentlyUsed() => Victim();

        public List<int> KeysMfuToLfu()
            => _values.Keys
                .OrderByDescending(k => _freq[k])
                .ThenByDescending(k => _stamp[k])
                .ToList();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(16)]
    public void RandomOps_MatchReferenceLfu(int capacity)
    {
        const int Seeds = 40;
        const int OpsPerSeed = 400;
        const int KeySpan = 20; // deliberately smaller than the op count so updates/evictions collide

        for (int seed = 0; seed < Seeds; seed++)
        {
            var rng = new Random(seed * 7919 + capacity);
            var cache = new LfuCache<int, int, Int32WangHasher>(capacity);
            var oracle = new OracleLfu(capacity);

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
                        // Peek must not count as a use in either implementation.
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

                    bool of = oracle.TryGetFrequency(k, out long expectedFreq);
                    bool cf = cache.TryGetFrequency(k, out long actualFreq);
                    Assert.Equal(of, cf);
                    if (of) Assert.Equal(expectedFreq, actualFreq);
                }

                // The exact eviction order — the heart of the contract.
                var cacheOrder = new List<int>();
                foreach (var kvp in cache)
                    cacheOrder.Add(kvp.Key);
                Assert.Equal(oracle.KeysMfuToLfu(), cacheOrder);

                // ...and the single entry that order makes the next victim.
                if (oracle.Count > 0)
                {
                    Assert.True(cache.TryPeekLeastFrequentlyUsed(out int victim, out _));
                    Assert.Equal(oracle.LeastFrequentlyUsed(), victim);
                }
                else
                {
                    Assert.False(cache.TryPeekLeastFrequentlyUsed(out _, out _));
                }
            }
        }
    }
}
