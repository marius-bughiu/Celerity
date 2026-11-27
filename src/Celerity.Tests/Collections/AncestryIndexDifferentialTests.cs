using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="AncestryIndex"/> against the parent walks it replaces: depth by
/// counting steps to the root, ancestry by climbing from the descendant, the lowest common ancestor by
/// collecting one node's ancestors and climbing from the other, and preorder by an explicit-stack depth-first
/// search with the children sorted by id.
///
/// <para>
/// The range-minimum query is what is on trial. It splits a preorder range into a partial block on each side
/// and a run of whole blocks between, and each piece has its own off-by-one to get wrong: a mask shifted one
/// bit too far, a block boundary computed from the wrong end, a sparse-table row read one block short. On a
/// small tree every query stays inside one 64-position block, so the generated shapes run up to a few hundred
/// nodes and include deep ones, where most pairs are blocks apart, and wide ones, where a node's children sit
/// in a long preorder run of equal depths.
/// </para>
///
/// <para>
/// Node ids are shuffled after the shape is built, so a parent's id is unrelated to its children's. Without
/// that, every generated tree would have parents numbered before their children — the order a naive
/// implementation happens to handle — and the preorder's "children by ascending id" rule would never be
/// exercised against a parent listed after its child.
/// </para>
/// </summary>
public class AncestryIndexDifferentialTests
{
    // Above this many nodes every pair is no longer checked; a fixed sample of pairs is used instead.
    private const int ExhaustivePairLimit = 80;

    public enum Shape
    {
        // parent[i] uniform in [0, i): shallow, depth ~ ln n.
        Recursive,

        // parent[i] within a few ids of i: deep, most pairs blocks apart in preorder.
        Deep,

        // A handful of hubs with many children each: long runs of equal depth.
        Wide,

        // Several independent trees: exercises the "different trees" answers.
        Forest,
    }

    private static readonly Gen<(int Count, Shape Shape, uint Seed)> GenForests =
        Gen.Select(Gen.Int[0, 400], Gen.Enum<Shape>(), Gen.UInt);

    [Fact]
    public void EveryQuery_ShouldMatchTheParentWalk_UnderGeneratedForests()
    {
        GenForests.Sample(spec => AssertAgreesWithOracle(Build(spec.Count, spec.Shape, spec.Seed), spec.Seed),
            iter: 300);
    }

    [Theory]
    [InlineData(5_000, Shape.Deep, 1u)]
    [InlineData(5_000, Shape.Recursive, 2u)]
    [InlineData(5_000, Shape.Wide, 3u)]
    [InlineData(5_000, Shape.Forest, 4u)]
    public void EveryQuery_ShouldMatchTheParentWalk_OnLargerSeededForests(int count, Shape shape, uint seed)
    {
        AssertAgreesWithOracle(Build(count, shape, seed), seed);
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheParentWalk_OnEveryLabelledForestOfFourNodes()
    {
        // Every parent array over four nodes whose entries are -1 or another node: 4^4 = 256 candidates, the
        // ones with a cycle rejected, the rest checked — every labelled rooted forest of that size.
        const int N = 4;
        int built = 0;
        int[] parents = new int[N];
        for (int code = 0; code < 256; code++)
        {
            int rest = code;
            for (int i = 0; i < N; i++)
            {
                int choice = rest % N;
                rest /= N;
                parents[i] = choice == i ? -1 : choice;
            }

            if (HasCycle(parents))
            {
                Assert.Throws<ArgumentException>(() => new AncestryIndex(parents));
                continue;
            }

            AssertAgreesWithOracle(parents, (uint)code);
            built++;
        }

        // Cayley's count for labelled rooted forests on n nodes is (n + 1)^(n - 1).
        Assert.Equal(125, built);
    }

    private static int[] Build(int count, Shape shape, uint seed)
    {
        var rng = new Random((int)seed);
        int[] topological = new int[count];
        int hubs = Math.Max(1, count / 40);
        for (int i = 0; i < count; i++)
        {
            topological[i] = shape switch
            {
                _ when i == 0 => -1,
                Shape.Recursive => rng.Next(i),
                Shape.Deep => i - 1 - rng.Next(Math.Min(i, 3)),
                Shape.Wide => i < hubs ? rng.Next(-1, i) : rng.Next(hubs),
                _ => rng.Next(8) == 0 ? -1 : rng.Next(i),
            };
        }

        // Relabel through a random permutation so parent ids are unrelated to child ids.
        int[] label = Enumerable.Range(0, count).ToArray();
        rng.Shuffle(label);
        int[] parents = new int[count];
        for (int i = 0; i < count; i++)
            parents[label[i]] = topological[i] < 0 ? -1 : label[topological[i]];

        return parents;
    }

    private static void AssertAgreesWithOracle(int[] parents, uint seed)
    {
        int n = parents.Length;
        var index = new AncestryIndex(parents);
        var oracle = new Oracle(parents);

        Assert.Equal(n, index.Count);
        Assert.Equal(parents.Count(p => p == -1), index.RootCount);
        Assert.Equal(n == 0 ? 0 : oracle.Depth.Max() + 1, index.Height);
        Assert.Equal(oracle.Preorder, index.Preorder.ToArray());

        for (int v = 0; v < n; v++)
        {
            Assert.Equal(parents[v], index.GetParent(v));
            Assert.Equal(oracle.Depth[v], index.GetDepth(v));
            Assert.Equal(oracle.Root(v), index.GetRoot(v));

            int[] subtree = oracle.Subtree(v);
            Assert.Equal(subtree.Length, index.GetSubtreeSize(v));
            Assert.Equal(subtree, index.GetSubtree(v).ToArray());
            Assert.Equal(oracle.PreorderIndex[v], index.GetPreorderIndex(v));

            // Every level from the node itself (k = 0) to one past its root (k = depth + 1, which is -1).
            List<int> path = oracle.PathToRoot(v);
            for (int k = 0; k < path.Count; k++)
                Assert.Equal(path[k], index.GetAncestor(v, k));

            Assert.Equal(-1, index.GetAncestor(v, path.Count));
        }

        if (n == 0)
            return;

        if (n <= ExhaustivePairLimit)
        {
            for (int a = 0; a < n; a++)
            {
                for (int b = 0; b < n; b++)
                    AssertPair(index, oracle, a, b);
            }
        }
        else
        {
            var rng = new Random((int)(seed ^ 0x5bd1e995));
            for (int i = 0; i < 4_000; i++)
            {
                int a = rng.Next(n);

                // Half the pairs are an ancestor and its descendant, which a uniform draw on a wide tree rarely
                // produces.
                int b = i % 2 == 0 ? rng.Next(n) : oracle.Climb(a, rng.Next(oracle.Depth[a] + 1));
                AssertPair(index, oracle, a, b);
                AssertPair(index, oracle, b, a);
            }
        }
    }

    private static void AssertPair(AncestryIndex index, Oracle oracle, int a, int b)
    {
        int lca = oracle.Lca(a, b);
        Assert.Equal(lca, index.LowestCommonAncestor(a, b));
        Assert.Equal(lca < 0 ? -1 : oracle.Depth[a] + oracle.Depth[b] - 2 * oracle.Depth[lca], index.Distance(a, b));
        Assert.Equal(oracle.IsAncestor(a, b), index.IsAncestor(a, b));
    }

    private static bool HasCycle(int[] parents)
    {
        for (int start = 0; start < parents.Length; start++)
        {
            int v = start;
            for (int steps = 0; v >= 0; steps++)
            {
                if (steps > parents.Length)
                    return true;

                v = parents[v];
            }
        }

        return false;
    }

    // The naive answers, computed only from the parent array.
    private sealed class Oracle
    {
        private readonly int[] _parents;
        private readonly List<int>[] _children;

        public Oracle(int[] parents)
        {
            _parents = parents;
            int n = parents.Length;
            _children = new List<int>[n];
            for (int v = 0; v < n; v++)
                _children[v] = [];

            for (int v = 0; v < n; v++)
            {
                if (parents[v] >= 0)
                    _children[parents[v]].Add(v);
            }

            Depth = new int[n];
            for (int v = 0; v < n; v++)
            {
                for (int u = parents[v]; u >= 0; u = parents[u])
                    Depth[v]++;
            }

            var order = new List<int>(n);
            var stack = new Stack<int>();
            for (int root = n - 1; root >= 0; root--)
            {
                if (parents[root] == -1)
                    stack.Push(root);
            }

            while (stack.Count > 0)
            {
                int v = stack.Pop();
                order.Add(v);

                // _children[v] is already ascending; push in reverse so the smallest id pops first.
                for (int c = _children[v].Count - 1; c >= 0; c--)
                    stack.Push(_children[v][c]);
            }

            Preorder = order.ToArray();
            PreorderIndex = new int[n];
            for (int i = 0; i < n; i++)
                PreorderIndex[Preorder[i]] = i;
        }

        public int[] Depth { get; }

        public int[] Preorder { get; }

        public int Climb(int v, int levels)
        {
            for (int i = 0; i < levels && v >= 0; i++)
                v = _parents[v];

            return v;
        }

        public int Root(int v) => Climb(v, Depth[v]);

        public bool IsAncestor(int ancestor, int v)
        {
            for (; v >= 0; v = _parents[v])
            {
                if (v == ancestor)
                    return true;
            }

            return false;
        }

        public int Lca(int a, int b)
        {
            var above = new HashSet<int>();
            for (int v = a; v >= 0; v = _parents[v])
                above.Add(v);

            for (int v = b; v >= 0; v = _parents[v])
            {
                if (above.Contains(v))
                    return v;
            }

            return -1;
        }

        public int[] PreorderIndex { get; private set; } = [];

        // v, its parent, its grandparent, ... its root.
        public List<int> PathToRoot(int v)
        {
            var path = new List<int>();
            for (; v >= 0; v = _parents[v])
                path.Add(v);

            return path;
        }

        // The subtree of v in preorder, by its own depth-first search over the child lists.
        public int[] Subtree(int v)
        {
            var order = new List<int>();
            var stack = new Stack<int>();
            stack.Push(v);
            while (stack.Count > 0)
            {
                int u = stack.Pop();
                order.Add(u);
                for (int c = _children[u].Count - 1; c >= 0; c--)
                    stack.Push(_children[u][c]);
            }

            return order.ToArray();
        }
    }
}
