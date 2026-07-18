using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Deterministic, seeded differential coverage for <see cref="DisjointSet{T}"/>. Each seed drives the same
/// random stream of add / union / connectivity operations into the disjoint-set and into an independent
/// naive reference model (a <see cref="Dictionary{TKey, TValue}"/> from element to its explicit member
/// <see cref="HashSet{T}"/>, merged the slow O(n) way), then asserts after every operation that the two
/// agree on element count, set count, per-element connectivity, and component size. This is the strongest
/// guard against a union-by-size / path-halving bug that only surfaces after many interleaved merges.
/// </summary>
public class DisjointSetDifferentialTests
{
    // A textbook-obvious union-find: each element maps to the shared HashSet of its whole component. Union
    // copies the smaller component into the larger and repoints its members. O(n) per union by design — it
    // is the correctness oracle, not the fast path.
    private sealed class OracleUnionFind
    {
        private readonly Dictionary<int, HashSet<int>> _componentOf = new();

        public int Count => _componentOf.Count;

        public int SetCount => _componentOf.Values.Distinct().Count();

        public bool Contains(int x) => _componentOf.ContainsKey(x);

        public void Add(int x)
        {
            if (!_componentOf.ContainsKey(x))
                _componentOf[x] = new HashSet<int> { x };
        }

        public void Union(int a, int b)
        {
            Add(a);
            Add(b);
            HashSet<int> ca = _componentOf[a];
            HashSet<int> cb = _componentOf[b];
            if (ReferenceEquals(ca, cb))
                return;

            // Merge the smaller into the larger.
            if (ca.Count < cb.Count)
                (ca, cb) = (cb, ca);
            foreach (int m in cb)
            {
                ca.Add(m);
                _componentOf[m] = ca;
            }
        }

        public bool Connected(int a, int b) =>
            _componentOf.TryGetValue(a, out HashSet<int>? ca) &&
            _componentOf.TryGetValue(b, out HashSet<int>? cb) &&
            ReferenceEquals(ca, cb);

        public int ComponentSize(int x) => _componentOf[x].Count;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RandomOps_MatchReferenceModel(int startCapacity)
    {
        const int Seeds = 40;
        const int OpsPerSeed = 600;
        const int Universe = 60; // small universe so components genuinely coalesce

        for (int seed = 0; seed < Seeds; seed++)
        {
            var rng = new Random(seed * 7919 + startCapacity);
            var ds = new DisjointSet<int>(startCapacity);
            var oracle = new OracleUnionFind();

            for (int op = 0; op < OpsPerSeed; op++)
            {
                int roll = rng.Next(3);
                if (roll == 0)
                {
                    int x = rng.Next(Universe);
                    ds.Add(x);
                    oracle.Add(x);
                }
                else if (roll == 1)
                {
                    int a = rng.Next(Universe);
                    int b = rng.Next(Universe);
                    ds.Union(a, b);
                    oracle.Union(a, b);
                }
                else
                {
                    int a = rng.Next(Universe);
                    int b = rng.Next(Universe);
                    Assert.Equal(oracle.Connected(a, b), ds.Connected(a, b));
                }

                Assert.Equal(oracle.Count, ds.Count);
                Assert.Equal(oracle.SetCount, ds.SetCount);
            }

            // Full reconciliation over the universe after the whole stream.
            for (int a = 0; a < Universe; a++)
            {
                Assert.Equal(oracle.Contains(a), ds.Contains(a));
                if (!oracle.Contains(a))
                    continue;

                Assert.Equal(oracle.ComponentSize(a), ds.ComponentSize(a));
                for (int b = 0; b < Universe; b++)
                    Assert.Equal(oracle.Connected(a, b), ds.Connected(a, b));
            }

            // GetComponents must reproduce exactly the oracle's partition.
            var components = ds.GetComponents();
            Assert.Equal(oracle.SetCount, components.Count);
            int totalMembers = components.Sum(g => g.Count);
            Assert.Equal(ds.Count, totalMembers);
            foreach (var group in components)
            {
                int anchor = group[0];
                foreach (int member in group)
                    Assert.True(oracle.Connected(anchor, member));
            }
        }
    }
}
