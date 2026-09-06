using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="DisjointSet{T}"/> against an independent naive
/// reference model (a <see cref="Dictionary{TKey, TValue}"/> from element to its explicit member
/// <see cref="HashSet{T}"/>, merged the slow O(n) way). CsCheck generates the starting capacity and the
/// stream of add / union / connectivity operations, and asserts after every operation that the two agree
/// on element count and set count, then reconciles the whole universe — per-element connectivity,
/// component size and the <c>GetComponents</c> partition — once the stream is done. This is the strongest
/// guard against a union-by-size / path-halving bug that only surfaces after many interleaved merges.
///
/// <para>
/// The universe is deliberately small relative to the operation count so components genuinely coalesce:
/// over a wide domain almost every union joins two singletons, and the size-ordered merge that is the
/// actual risk surface is never reached.
/// </para>
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

    private enum Op { Add, Union, Connected }

    private const int Universe = 60;

    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 2].Select(n => (Op)n);

    private static readonly Gen<(Op Kind, int A, int B)> GenOp =
        Gen.Select(GenKind, Gen.Int[0, Universe - 1], Gen.Int[0, Universe - 1]);

    private static readonly Gen<(int StartCapacity, List<(Op Kind, int A, int B)> Ops)> GenScript =
        Gen.Select(Gen.Int[0, 4], GenOp.List[0, 500]);

    [Fact]
    public void DisjointSet_ShouldMatch_ANaiveUnionFind()
    {
        GenScript.Sample(script =>
        {
            var ds = new DisjointSet<int>(script.StartCapacity);
            var oracle = new OracleUnionFind();

            foreach (var (kind, a, b) in script.Ops)
            {
                switch (kind)
                {
                    case Op.Add:
                        ds.Add(a);
                        oracle.Add(a);
                        break;

                    case Op.Union:
                        ds.Union(a, b);
                        oracle.Union(a, b);
                        break;

                    case Op.Connected:
                        Assert.Equal(oracle.Connected(a, b), ds.Connected(a, b));
                        break;
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
        }, iter: 40);
    }
}
