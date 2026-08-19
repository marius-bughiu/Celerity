using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// CompressedGraph vs the adjacency map it replaces. There is no BCL counterpart of any kind — .NET ships no
// graph, no adjacency list, no adjacency matrix and no traversal anywhere in System.Collections — so the
// honest baseline is what a caller writes instead. The baseline arms are named Dictionary_* and List_* so the
// dashboard classifies them as the reference series.
//
// Two baselines, because there are two things a caller might write, and only measuring the weaker one would
// flatter the type. The idiomatic one is Dictionary<int, List<int>>, which pays a hash lookup per vertex
// visited on top of the indirection. The better one — a List<int>[] indexed by vertex id — throws the hashing
// away and is what a developer who has noticed the ids are dense writes instead; it is the arm to judge this
// type by, because what is left between it and CSR is purely the cost of the adjacency layout: one List<int>
// object plus its backing array per vertex, landing wherever the allocator put them, against one contiguous
// target array the whole traversal walks close to in order.
//
// The traversal arms give both baselines a flat int[] as the queue rather than a Queue<int>, which is the same
// queue the type uses internally — without that the arms would be measuring the queue as much as the graph.
//
// They are still an *end-to-end* traversal comparison rather than an isolation of the adjacency layout, and it
// is worth being exact about why: the baselines mark visits in a bool[], which is what a hand-rolled BFS
// actually uses, while CompressedGraph packs the same marks into a ulong bitmap — 12.5 KB against 100 KB at
// 100,000 vertices. That is a real part of the measured difference (a smaller clear, and a visited set that
// stays in cache) and it is not the adjacency. Swapping a bitmap into the baselines would isolate the layout
// but would stop measuring what a caller actually writes, so the arms keep the realistic hand-roll and the
// claim is scoped to the whole traversal instead. Neighbors below is the arm that isolates the layout on its
// own — no queue, no visited set — and its ratio is the one to read as the adjacency-only number.
//
// Neighbors is the innermost loop on its own: walk every vertex's targets and sum them, with no traversal
// bookkeeping at all. It is the narrowest statement of what the layout buys and the one whose ratio should be
// read as the ceiling. Traverse and TraverseArray are the workload that matters — a full breadth-first pass —
// and they are the numbers the type is sold on. Topological is Kahn's algorithm, the dependency-resolution
// question .NET also has no answer for; both sides do the same O(V + E) work, so the ratio is again the
// adjacency. Reverse is the transpose, which CSR does as a counting scatter over two arrays and the baseline
// has to rebuild list by list. Build is the price of the structure, which a caller recovers by querying it.
//
// The graph is a random DAG (every edge points from a lower id to a higher one) at average degree 8, which is
// the sparse shape real dependency, link and reachability graphs have and the shape CSR is for — it is also
// what lets the topological arms measure a completed order rather than an early cycle report. The ratio is a
// function of that sparsity: the denser the graph, the more of the total cost is the scan over the targets
// themselves, which both sides pay identically, so a very dense graph converges toward parity.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CompressedGraphBenchmark
{
    private const int AverageDegree = 8;

    private GraphEdge[] edges = null!;
    private CompressedGraph graph = null!;
    private Dictionary<int, List<int>> map = null!;
    private List<int>[] lists = null!;
    private int[][] tight = null!;
    private int[] queue = null!;
    private bool[] visited = null!;
    private int[] indegrees = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        // Distinct edges, so neither side has to deduplicate at build time and the Build arms compare the
        // structures rather than one side's collapsing pass.
        var distinct = new HashSet<GraphEdge>();
        var generated = new List<GraphEdge>(ItemCount * AverageDegree);
        while (generated.Count < ItemCount * AverageDegree)
        {
            // Two draws ordered low-to-high: an acyclic graph, without the degenerate degree distribution a
            // single "source, then something after it" draw would give the lowest-numbered vertices.
            int a = rand.Next(ItemCount);
            int b = rand.Next(ItemCount);
            if (a == b)
                continue;

            GraphEdge edge = a < b ? new GraphEdge(a, b) : new GraphEdge(b, a);
            if (distinct.Add(edge))
                generated.Add(edge);
        }

        edges = [.. generated];

        graph = new CompressedGraph(ItemCount, edges);
        map = BuildMap();
        lists = BuildLists();
        tight = BuildTight();

        queue = new int[ItemCount];
        visited = new bool[ItemCount];
        indegrees = new int[ItemCount];
    }

    // ---- Neighbors: the innermost loop alone — walk every vertex's targets, no traversal bookkeeping ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Neighbors")]
    public long List_Neighbors()
    {
        long sum = 0;
        for (int vertex = 0; vertex < lists.Length; vertex++)
        {
            List<int> targets = lists[vertex];
            for (int i = 0; i < targets.Count; i++)
                sum += targets[i];
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Neighbors")]
    public long CompressedGraph_Neighbors()
    {
        long sum = 0;
        for (int vertex = 0; vertex < graph.VertexCount; vertex++)
        {
            foreach (int target in graph.Neighbors(vertex))
                sum += target;
        }

        return sum;
    }

    // ---- Traverse: a full breadth-first pass against the idiomatic Dictionary<int, List<int>> ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Traverse")]
    public int Dictionary_Traverse()
    {
        Array.Clear(visited);

        int count = 1;
        queue[0] = 0;
        visited[0] = true;
        for (int head = 0; head < count; head++)
        {
            List<int> targets = map[queue[head]];
            for (int i = 0; i < targets.Count; i++)
            {
                int target = targets[i];
                if (visited[target])
                    continue;

                visited[target] = true;
                queue[count++] = target;
            }
        }

        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Traverse")]
    public int CompressedGraph_Traverse() => graph.CopyBreadthFirstOrder(0, queue);

    // ---- TraverseArray: the same pass against the better hand-roll, which has thrown the hashing away ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TraverseArray")]
    public int List_TraverseArray()
    {
        Array.Clear(visited);

        int count = 1;
        queue[0] = 0;
        visited[0] = true;
        for (int head = 0; head < count; head++)
        {
            List<int> targets = lists[queue[head]];
            for (int i = 0; i < targets.Count; i++)
            {
                int target = targets[i];
                if (visited[target])
                    continue;

                visited[target] = true;
                queue[count++] = target;
            }
        }

        return count;
    }

    [Benchmark]
    [BenchmarkCategory("TraverseArray")]
    public int CompressedGraph_TraverseArray() => graph.CopyBreadthFirstOrder(0, queue);

    // ---- TraverseTight: the same pass against the best hand-roll short of CSR ----
    //
    // This arm exists because a claim about the other two needed evidence. List<int>[] allocates each list's
    // backing array lazily, as the random edge order grows it, so its neighbour arrays are *not* laid out in
    // vertex order however tidily the empty List objects were created. int[][] sized exactly and filled in
    // vertex order is: same jagged indirection, but the neighbour data is contiguous and in traversal-relevant
    // order, which is the one property CSR is supposed to be buying. The gap between this arm and
    // TraverseArray is therefore the layout effect on its own, and the gap that remains here is what CSR wins
    // *after* a caller has already done everything short of flattening the arrays.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TraverseTight")]
    public int Array_TraverseTight()
    {
        Array.Clear(visited);

        int count = 1;
        queue[0] = 0;
        visited[0] = true;
        for (int head = 0; head < count; head++)
        {
            int[] targets = tight[queue[head]];
            for (int i = 0; i < targets.Length; i++)
            {
                int target = targets[i];
                if (visited[target])
                    continue;

                visited[target] = true;
                queue[count++] = target;
            }
        }

        return count;
    }

    [Benchmark]
    [BenchmarkCategory("TraverseTight")]
    public int CompressedGraph_TraverseTight() => graph.CopyBreadthFirstOrder(0, queue);

    // ---- Topological: Kahn's algorithm, the dependency-resolution question with no BCL answer ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Topological")]
    public int List_Topological()
    {
        Array.Clear(indegrees);
        for (int vertex = 0; vertex < lists.Length; vertex++)
        {
            List<int> targets = lists[vertex];
            for (int i = 0; i < targets.Count; i++)
                indegrees[targets[i]]++;
        }

        int count = 0;
        for (int vertex = 0; vertex < lists.Length; vertex++)
        {
            if (indegrees[vertex] == 0)
                queue[count++] = vertex;
        }

        for (int head = 0; head < count; head++)
        {
            List<int> targets = lists[queue[head]];
            for (int i = 0; i < targets.Count; i++)
            {
                if (--indegrees[targets[i]] == 0)
                    queue[count++] = targets[i];
            }
        }

        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Topological")]
    public bool CompressedGraph_Topological() => graph.TryCopyTopologicalOrder(queue);

    // ---- Reverse: the transpose, a counting scatter here and a rebuild for the baseline ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Reverse")]
    public List<int>[] List_Reverse()
    {
        var reversed = new List<int>[lists.Length];
        for (int vertex = 0; vertex < reversed.Length; vertex++)
            reversed[vertex] = [];

        for (int vertex = 0; vertex < lists.Length; vertex++)
        {
            List<int> targets = lists[vertex];
            for (int i = 0; i < targets.Count; i++)
                reversed[targets[i]].Add(vertex);
        }

        return reversed;
    }

    [Benchmark]
    [BenchmarkCategory("Reverse")]
    public CompressedGraph CompressedGraph_Reverse() => graph.Reverse();

    // ---- ReverseTight: the transpose against a jagged form that does NOT pay a list per vertex ----
    //
    // List_Reverse allocates a List<int> for every vertex and grows it, which is most of what it costs. That
    // is the honest cost of transposing *that* structure, but it is not a bound on what a jagged hand-roll can
    // do: counting the in-degrees first lets each target array be allocated at its exact size and filled by a
    // scatter, which is the same algorithm CSR uses, one indirection out. This arm is what the published
    // transpose ratio has to be read against — quoting the List_Reverse number as the win over a well-laid-out
    // int[][] would repeat exactly the mistake the TraverseTight arm exists to correct.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ReverseTight")]
    public int[][] Array_ReverseTight()
    {
        var indegree = new int[tight.Length];
        for (int vertex = 0; vertex < tight.Length; vertex++)
        {
            int[] targets = tight[vertex];
            for (int i = 0; i < targets.Length; i++)
                indegree[targets[i]]++;
        }

        var reversed = new int[tight.Length][];
        for (int vertex = 0; vertex < reversed.Length; vertex++)
            reversed[vertex] = new int[indegree[vertex]];

        var cursor = new int[tight.Length];
        for (int vertex = 0; vertex < tight.Length; vertex++)
        {
            int[] targets = tight[vertex];
            for (int i = 0; i < targets.Length; i++)
            {
                int target = targets[i];
                reversed[target][cursor[target]++] = vertex;
            }
        }

        return reversed;
    }

    [Benchmark]
    [BenchmarkCategory("ReverseTight")]
    public CompressedGraph CompressedGraph_ReverseTight() => graph.Reverse();

    // ---- BuildTight: construction against the same exact-sized jagged form ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("BuildTight")]
    public int[][] Array_BuildTight()
    {
        // Count the out-degrees, allocate each row exactly, scatter. No List<T> growth anywhere, which is the
        // fairest thing the jagged representation can do from the same edge array.
        var degree = new int[ItemCount];
        foreach (GraphEdge edge in edges)
            degree[edge.Source]++;

        var built = new int[ItemCount][];
        for (int vertex = 0; vertex < ItemCount; vertex++)
            built[vertex] = new int[degree[vertex]];

        var cursor = new int[ItemCount];
        foreach (GraphEdge edge in edges)
            built[edge.Source][cursor[edge.Source]++] = edge.Target;

        for (int vertex = 0; vertex < ItemCount; vertex++)
            Array.Sort(built[vertex]);

        return built;
    }

    [Benchmark]
    [BenchmarkCategory("BuildTight")]
    public CompressedGraph CompressedGraph_BuildTight() => new(ItemCount, edges);

    // ---- Build: the structure the queries above amortize ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public Dictionary<int, List<int>> Dictionary_Build() => BuildMap();

    [Benchmark]
    [BenchmarkCategory("Build")]
    public CompressedGraph CompressedGraph_Build() => new(ItemCount, edges);

    // The idiomatic adjacency map. The generated edges are already distinct, so this holds exactly the edge
    // set CompressedGraph does and the traversal arms do the same amount of work.
    private Dictionary<int, List<int>> BuildMap()
    {
        var built = new Dictionary<int, List<int>>(ItemCount);
        for (int vertex = 0; vertex < ItemCount; vertex++)
            built[vertex] = [];

        foreach (GraphEdge edge in edges)
            built[edge.Source].Add(edge.Target);

        return built;
    }

    // Exactly sized, filled in vertex order, targets ascending: the same edge set the graph holds, laid out as
    // well as a jagged structure can lay it out.
    private int[][] BuildTight()
    {
        var built = new int[ItemCount][];
        for (int vertex = 0; vertex < ItemCount; vertex++)
        {
            List<int> targets = lists[vertex];
            int[] copy = new int[targets.Count];
            targets.CopyTo(copy);
            Array.Sort(copy);
            built[vertex] = copy;
        }

        return built;
    }

    private List<int>[] BuildLists()
    {
        var built = new List<int>[ItemCount];
        for (int vertex = 0; vertex < ItemCount; vertex++)
            built[vertex] = [];

        foreach (GraphEdge edge in edges)
            built[edge.Source].Add(edge.Target);

        return built;
    }
}
