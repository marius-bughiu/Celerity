using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// AncestryIndex vs the parent walk it replaces. The BCL has no tree type, so a hierarchy in .NET is an int[] of
// parents and a loop; the baseline arms are that loop at its best — given a precomputed depth array, which is
// what lets it lift the deeper node straight to the other's level instead of collecting an ancestor set. That
// depth array is the baseline's own setup cost, and the Build category charges it for it.
//
//   IsAncestor — "is a above b?": climb from b to a's depth and compare. Half the pairs are a true ancestor
//                and its descendant, half are uniform, so neither answer is free.
//   Lca        — the lowest common ancestor of two uniform nodes: lift the deeper, then climb both in step.
//   DeepLca    — the same query on a deep tree, where the walk's cost is the point.
//   Build      — the depth array (the baseline's only precomputation) against the whole index.
//
// Two tree shapes. Lca and IsAncestor run on a random recursive tree — every node's parent uniform among the
// nodes before it — whose depth is about ln n: 7 at a thousand nodes, 12 at a hundred thousand. That is the
// walk's best case, and the shape on which the index's margin is smallest. DeepLca runs on a tree whose every
// node hangs within four ids of its predecessor, so the depth is about 40% of n — an org chart with long
// management chains, a deep folder tree, a commit history. Its batch is smaller because the baseline's cost
// per query grows with n.
//
// Node ids are shuffled after the shape is built, so neither side gets the locality a "parents before
// children" numbering would hand the walk for free. The query pairs come from a pool far larger than one
// invocation's batch and the arms step through it, so the walk's data-dependent loop exits cannot be learned
// by the branch predictor from a short replayed batch.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class AncestryIndexBenchmark
{
    private const int Batch = 4_096;
    private const int DeepBatch = 256;
    private const int PoolSize = 1 << 19;
    private const int DeepPoolSize = 1 << 14;

    private int[] parents = null!;
    private int[] depth = null!;
    private int[] deepParents = null!;
    private int[] deepDepth = null!;

    private AncestryIndex index = null!;
    private AncestryIndex deepIndex = null!;

    private int[] lcaFirst = null!;
    private int[] lcaSecond = null!;
    private int[] ancestorCandidate = null!;
    private int[] ancestorNode = null!;
    private int[] deepFirst = null!;
    private int[] deepSecond = null!;

    private int cursor;
    private int deepCursor;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        parents = Shuffled(rand, ItemCount, i => rand.Next(i));
        deepParents = Shuffled(rand, ItemCount, i => i - 1 - rand.Next(Math.Min(i, 4)));
        depth = Depths(parents);
        deepDepth = Depths(deepParents);
        index = new AncestryIndex(parents);
        deepIndex = new AncestryIndex(deepParents);

        lcaFirst = new int[PoolSize];
        lcaSecond = new int[PoolSize];
        ancestorCandidate = new int[PoolSize];
        ancestorNode = new int[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            lcaFirst[i] = rand.Next(ItemCount);
            lcaSecond[i] = rand.Next(ItemCount);

            int node = rand.Next(ItemCount);
            int candidate = rand.Next(ItemCount);
            if ((i & 1) == 0)
            {
                // A true ancestor: climb a uniform number of levels.
                candidate = node;
                for (int k = rand.Next(depth[node] + 1); k > 0; k--)
                    candidate = parents[candidate];
            }

            ancestorCandidate[i] = candidate;
            ancestorNode[i] = node;
        }

        deepFirst = new int[DeepPoolSize];
        deepSecond = new int[DeepPoolSize];
        for (int i = 0; i < DeepPoolSize; i++)
        {
            deepFirst[i] = rand.Next(ItemCount);
            deepSecond[i] = rand.Next(ItemCount);
        }
    }

    // Each arm runs one batch from the pool and advances its cursor, so every invocation sees fresh pairs.
    private int NextBatch()
    {
        int start = cursor;
        cursor = (start + Batch) & (PoolSize - 1);
        return start;
    }

    private int NextDeepBatch()
    {
        int start = deepCursor;
        deepCursor = (start + DeepBatch) & (DeepPoolSize - 1);
        return start;
    }

    // ---- IsAncestor ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IsAncestor")]
    public int Array_IsAncestor()
    {
        int start = NextBatch();
        int hits = 0;
        for (int i = start; i < start + Batch; i++)
        {
            int candidate = ancestorCandidate[i];
            int node = ancestorNode[i];
            int target = depth[candidate];
            for (int d = depth[node]; d > target; d--)
                node = parents[node];

            if (node == candidate)
                hits++;
        }

        return hits;
    }

    [Benchmark]
    [BenchmarkCategory("IsAncestor")]
    public int AncestryIndex_IsAncestor()
    {
        int start = NextBatch();
        int hits = 0;
        for (int i = start; i < start + Batch; i++)
        {
            if (index.IsAncestor(ancestorCandidate[i], ancestorNode[i]))
                hits++;
        }

        return hits;
    }

    // ---- Lca on the shallow tree ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lca")]
    public long Array_Lca()
    {
        int start = NextBatch();
        long sink = 0;
        for (int i = start; i < start + Batch; i++)
            sink += WalkLca(parents, depth, lcaFirst[i], lcaSecond[i]);

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("Lca")]
    public long AncestryIndex_Lca()
    {
        int start = NextBatch();
        long sink = 0;
        for (int i = start; i < start + Batch; i++)
            sink += index.LowestCommonAncestor(lcaFirst[i], lcaSecond[i]);

        return sink;
    }

    // ---- Lca on the deep tree ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("DeepLca")]
    public long Array_DeepLca()
    {
        int start = NextDeepBatch();
        long sink = 0;
        for (int i = start; i < start + DeepBatch; i++)
            sink += WalkLca(deepParents, deepDepth, deepFirst[i], deepSecond[i]);

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("DeepLca")]
    public long AncestryIndex_DeepLca()
    {
        int start = NextDeepBatch();
        long sink = 0;
        for (int i = start; i < start + DeepBatch; i++)
            sink += deepIndex.LowestCommonAncestor(deepFirst[i], deepSecond[i]);

        return sink;
    }

    // ---- Build ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public int Array_Build() => Depths(parents).Length;

    [Benchmark]
    [BenchmarkCategory("Build")]
    public int AncestryIndex_Build() => new AncestryIndex(parents).Count;

    // The baseline LCA at its best: lift the deeper node to the other's depth, then climb both in step.
    private static int WalkLca(int[] parents, int[] depth, int a, int b)
    {
        int da = depth[a];
        int db = depth[b];
        for (; da > db; da--)
            a = parents[a];

        for (; db > da; db--)
            b = parents[b];

        while (a != b)
        {
            a = parents[a];
            b = parents[b];
        }

        return a;
    }

    // The baseline's precomputation: every node's depth, memoized so each edge is walked once.
    private static int[] Depths(int[] parents)
    {
        int n = parents.Length;
        int[] depth = new int[n];
        Array.Fill(depth, -1);
        int[] stack = new int[n];
        for (int v = 0; v < n; v++)
        {
            int top = 0;
            int u = v;
            while (u >= 0 && depth[u] < 0)
            {
                stack[top++] = u;
                u = parents[u];
            }

            int d = u < 0 ? -1 : depth[u];
            while (top > 0)
                depth[stack[--top]] = ++d;
        }

        return depth;
    }

    // A tree built in "parent before child" order by `parentOf`, then relabelled through a random permutation.
    private static int[] Shuffled(Random rand, int count, Func<int, int> parentOf)
    {
        int[] topological = new int[count];
        topological[0] = -1;
        for (int i = 1; i < count; i++)
            topological[i] = parentOf(i);

        int[] label = new int[count];
        for (int i = 0; i < count; i++)
            label[i] = i;

        rand.Shuffle(label);
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
            result[label[i]] = topological[i] < 0 ? -1 : label[topological[i]];

        return result;
    }
}
