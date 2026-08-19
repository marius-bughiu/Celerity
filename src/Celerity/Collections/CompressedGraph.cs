using System.Buffers;
using System.Collections;

namespace Celerity.Collections;

/// <summary>
/// A <b>compressed sparse row (CSR) graph</b>: a build-once, immutable directed graph over dense vertex ids
/// <c>[0, VertexCount)</c>, stored as two flat <c>int[]</c> so a vertex's neighbours are a
/// <see cref="ReadOnlySpan{T}"/> slice of one contiguous array rather than a heap object of their own.
/// </summary>
/// <remarks>
/// <para>
/// .NET ships nothing for this. There is no graph, no adjacency list, no adjacency matrix and no traversal
/// anywhere in <c>System.Collections</c> on any of net8/9/10, so the idiom that fills the vacuum is
/// <c>Dictionary&lt;int, List&lt;int&gt;&gt;</c> or <c>List&lt;int&gt;[]</c>. That shape pays for itself three
/// times over on the one loop a traversal actually runs — expand a vertex, read its neighbours: a hash lookup
/// per visit on the dictionary form, a <see cref="List{T}"/> object plus its backing array per vertex so
/// neighbouring vertices' neighbour sets land wherever the allocator put them, and an enumerator to walk each
/// one. CSR replaces all of it with an index and a slice: two allocations for the whole graph regardless of
/// how many vertices it has.
/// </para>
/// <para>
/// <b>Do not reach for this to make a traversal faster.</b> A third baseline is measured for exactly that
/// reason — an <c>int[][]</c> sized exactly and filled in vertex order — and against it the breadth-first
/// traversal wins about 11% at 100,000 vertices and <i>loses</i> at 1,000, as does the build. A caller who
/// lays the neighbour data out that well has already taken almost everything the flat array gives. What
/// survives is the <b>transpose</b> (4.4x, structurally: the jagged form must allocate a row per vertex where
/// this scatters into arrays it already holds), a footprint about 1.8x smaller, and not having to write, test
/// and maintain the traversal, Kahn's algorithm, the transpose, the deduplication and the sorted-target
/// invariant — none of which the BCL ships. Every ratio names the baseline it was measured against, because
/// the three baselines disagree by a lot; the numbers are in the benchmark dashboard and in
/// <c>docs/api/collections.md</c>.
/// </para>
/// <para>
/// The two shipped types that sit next to graphs do not replace this and are not replaced by it.
/// <see cref="SparseSet"/> is the <i>visited set</i> a traversal needs — bookkeeping, not the graph.
/// <see cref="DisjointSet{T}"/> answers <i>are these two connected</i> after a sequence of unions, which is a
/// connectivity oracle: it cannot enumerate a vertex's neighbours and is undirected by construction.
/// </para>
/// <para>
/// <b>Layout.</b> <c>_offsets</c> holds <c>VertexCount + 1</c> entries, where vertex <c>v</c>'s targets occupy
/// <c>_targets[_offsets[v].._offsets[v + 1]]</c>; the trailing entry is the edge count, which is what removes
/// the bounds special case from every slice. Each vertex's targets are stored in ascending order, which is
/// what makes <see cref="ContainsEdge"/> a binary search rather than a scan.
/// </para>
/// <para>
/// <b>Vertices are dense <see cref="int"/> ids, and the type is not generic.</b> Per-vertex payload belongs in
/// the caller's own array indexed by the same id — that is the whole benefit of dense ids, and threading a
/// <c>TValue</c> through the adjacency structure would only move the array inside the graph while adding a
/// type parameter to every signature. If the domain keys are not already dense integers, map them once
/// through a <see cref="CelerityDictionary{TKey, TValue, THasher}"/> and keep the graph on the ids.
/// </para>
/// <para>
/// <b>Directed, deduplicated, self-loops kept.</b> An undirected graph is built by supplying each edge in both
/// directions; there is no factory for it, because the alternative — silently doubling the caller's input — is
/// worse than one documented line. Duplicate edges collapse: this is an edge <i>set</i>, which is what makes
/// <see cref="Degree"/> a count of distinct neighbours and lets <see cref="ContainsEdge"/> binary-search. A
/// self-loop <c>(v, v)</c> is a legal edge and is preserved, so a traversal from <c>v</c> sees <c>v</c> among
/// its own neighbours and <see cref="TryCopyTopologicalOrder"/> correctly reports the graph as cyclic.
/// </para>
/// <para>
/// <b>Build-once.</b> The graph is immutable; adding an edge means building a new one, as with
/// <see cref="KdTree{TValue}"/>, <see cref="RTree{TValue}"/> and <see cref="IntervalTree{TKey, TValue}"/>.
/// Nothing mutates, so enumeration is never invalidated and concurrent readers need no synchronization — with
/// no comparer caveat here, since the type compares nothing but <see cref="int"/> ids.
/// </para>
/// <para>
/// <b>What is deliberately out.</b> Edge weights, shortest paths and any other algorithm that is a function of
/// weights: those are algorithms over a container rather than the container, and shipping one would commit the
/// type to a weight representation before anything needs it. The two traversals that <i>are</i> here are the
/// ones whose cost is dominated by the adjacency walk this layout exists to make fast, and each answers a
/// question .NET has no answer for at all.
/// </para>
/// <example>
/// <code>
/// // Four packages; an edge means "must be built before".
/// var builds = new CompressedGraph(4,
/// [
///     new GraphEdge(0, 1),
///     new GraphEdge(0, 2),
///     new GraphEdge(1, 3),
///     new GraphEdge(2, 3),
/// ]);
///
/// foreach (int dependent in builds.Neighbors(0))
///     Console.WriteLine(dependent);          // 1, then 2 — no allocation, no enumerator
///
/// if (builds.TryGetTopologicalOrder(out int[] order))
///     Console.WriteLine(string.Join(" ", order));   // 0 1 2 3
/// </code>
/// </example>
/// </remarks>
public sealed class CompressedGraph : IReadOnlyList<GraphEdge>
{
    // _offsets.Length is VertexCount + 1 and _targets.Length is EdgeCount, so neither count needs a field of
    // its own. Vertex v's targets are _targets[_offsets[v].._offsets[v + 1]], ascending and distinct.
    private readonly int[] _offsets;
    private readonly int[] _targets;

    /// <summary>Builds a graph on <paramref name="vertexCount"/> vertices over <paramref name="edges"/>.</summary>
    /// <param name="vertexCount">The number of vertices. Ids run over <c>[0, vertexCount)</c>.</param>
    /// <param name="edges">The directed edges. The sequence is read once; duplicates collapse.</param>
    /// <exception cref="ArgumentNullException"><paramref name="edges"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="vertexCount"/> is negative.</exception>
    /// <exception cref="ArgumentException">An edge has an endpoint outside <c>[0, vertexCount)</c>.</exception>
    /// <remarks>
    /// Building is <c>O(V + E log d)</c> for maximum out-degree <c>d</c> — a counting scatter, then one sort
    /// per vertex to put the targets in order. Isolated vertices cost an offset entry each and nothing more,
    /// so a graph declared far larger than its edges reach is cheap rather than wasteful.
    /// </remarks>
    public CompressedGraph(int vertexCount, IEnumerable<GraphEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);
        if (vertexCount < 0)
            throw new ArgumentOutOfRangeException(nameof(vertexCount), vertexCount, "Vertex count must be non-negative.");

        // A counted source is sized and copied once, as the sibling build-once types' constructors do; going
        // through a List<T> unconditionally would allocate and copy a second backing array for the commonest
        // sources of all. A ReadOnlySpan<GraphEdge> overload alongside this one would be ambiguous for an
        // array argument under C# 12, which is what the net8.0 floor compiles as.
        GraphEdge[] items;
        if (edges is ICollection<GraphEdge> counted)
        {
            items = new GraphEdge[counted.Count];
            counted.CopyTo(items, 0);
        }
        else
        {
            items = new List<GraphEdge>(edges).ToArray();
        }

        int[] offsets = new int[vertexCount + 1];
        int[] targets = new int[items.Length];
        Build(items, vertexCount, offsets, targets, nameof(edges));

        // Deduplication compacts in place, so the scatter buffer is oversized by exactly the number of
        // duplicates dropped. Trimming it is what makes EdgeCount the array's own length.
        int edgeCount = offsets[vertexCount];
        _offsets = offsets;
        _targets = edgeCount == targets.Length ? targets : targets[..edgeCount];
    }

    private CompressedGraph(int[] offsets, int[] targets)
    {
        _offsets = offsets;
        _targets = targets;
    }

    /// <summary>Gets the number of vertices. Ids run over <c>[0, VertexCount)</c>.</summary>
    public int VertexCount => _offsets.Length - 1;

    /// <summary>Gets the number of distinct directed edges, after duplicates collapsed at build time.</summary>
    public int EdgeCount => _targets.Length;

    // Deliberately explicit. A graph has two counts and neither owns the bare name: publishing Count as the
    // edge count would sit one keystroke from VertexCount and read as whichever the caller had in mind. The
    // interface still needs it, and IReadOnlyList<GraphEdge> is over the edges, so this is the edge count.
    int IReadOnlyCollection<GraphEdge>.Count => _targets.Length;

    /// <summary>Gets the edge at <paramref name="index"/> in source-major, then ascending-target, order.</summary>
    /// <param name="index">The zero-based position in edge order.</param>
    /// <returns>The edge stored at that position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, EdgeCount)</c>.</exception>
    /// <remarks>
    /// Recovering the source costs a binary search over the offsets, so this is <c>O(log V)</c> — random
    /// access exists for the <see cref="IReadOnlyList{T}"/> contract. Enumerating the graph, or walking
    /// <see cref="Neighbors"/> per vertex, is <c>O(1)</c> per edge and is what to reach for in a loop.
    /// </remarks>
    public GraphEdge this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_targets.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be in the range [0, EdgeCount).");

            return new GraphEdge(SourceOf(index), _targets[index]);
        }
    }

    /// <summary>Gets the targets of <paramref name="vertex"/>, in ascending order.</summary>
    /// <param name="vertex">The vertex whose out-edges to read.</param>
    /// <returns>A slice of the adjacency array — no copy, no allocation, no enumerator.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="vertex"/> is outside <c>[0, VertexCount)</c>.</exception>
    /// <remarks>
    /// This is the member the layout exists for: the returned span is a window onto the graph's own storage,
    /// so iterating it is a bounds-checked array walk and nothing else. The graph is immutable, so the span
    /// stays valid for as long as the graph is reachable.
    /// </remarks>
    public ReadOnlySpan<int> Neighbors(int vertex)
    {
        ThrowIfVertexOutOfRange(vertex, nameof(vertex));

        int start = _offsets[vertex];
        return _targets.AsSpan(start, _offsets[vertex + 1] - start);
    }

    /// <summary>Gets the number of distinct targets of <paramref name="vertex"/>.</summary>
    /// <param name="vertex">The vertex whose out-degree to read.</param>
    /// <returns>The out-degree, which is <c>0</c> for an isolated vertex.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="vertex"/> is outside <c>[0, VertexCount)</c>.</exception>
    public int Degree(int vertex)
    {
        ThrowIfVertexOutOfRange(vertex, nameof(vertex));

        return _offsets[vertex + 1] - _offsets[vertex];
    }

    /// <summary>Determines whether the edge <c>source -&gt; target</c> is present.</summary>
    /// <param name="source">The vertex the edge leaves.</param>
    /// <param name="target">The vertex the edge enters.</param>
    /// <returns><c>true</c> if the edge is present; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Either endpoint is outside <c>[0, VertexCount)</c>.</exception>
    /// <remarks>
    /// <c>O(log d)</c> in the out-degree, by binary search over the sorted target slice. An absent <i>edge</i>
    /// is an ordinary answer and returns <c>false</c>; an out-of-range <i>vertex</i> is a caller bug and
    /// throws, matching <see cref="Neighbors"/> and <see cref="Degree"/>. The edge is directed, so this asks
    /// only about that direction.
    /// </remarks>
    public bool ContainsEdge(int source, int target)
    {
        ThrowIfVertexOutOfRange(source, nameof(source));
        ThrowIfVertexOutOfRange(target, nameof(target));

        int start = _offsets[source];
        return _targets.AsSpan(start, _offsets[source + 1] - start).BinarySearch(target) >= 0;
    }

    /// <summary>Builds the transpose: the graph with every edge's direction reversed.</summary>
    /// <returns>A new graph over the same vertices whose neighbours are this graph's in-edges.</returns>
    /// <remarks>
    /// <c>O(V + E)</c> — a counting scatter, with no sort needed, because visiting the sources in ascending
    /// order already emits each reversed slice in ascending order. This is the operation the CSR layout makes
    /// nearly free and the <c>Dictionary&lt;int, List&lt;int&gt;&gt;</c> form makes a rebuild: which vertices
    /// point <i>at</i> this one — "which pages link here", "which packages was this built from" — and the
    /// reverse pass of a two-way reachability check. Mind the direction when the edges encode a dependency:
    /// under the usual <c>source must come before target</c> reading, a vertex's in-neighbours are what it
    /// depends <i>on</i>, not what depends on it.
    /// </remarks>
    public CompressedGraph Reverse()
    {
        int vertexCount = VertexCount;
        int[] offsets = new int[vertexCount + 1];
        int[] targets = new int[_targets.Length];

        foreach (int target in _targets)
            offsets[target]++;

        ExclusivePrefixSum(offsets, vertexCount);

        for (int source = 0; source < vertexCount; source++)
        {
            int end = _offsets[source + 1];
            for (int i = _offsets[source]; i < end; i++)
                targets[offsets[_targets[i]]++] = source;
        }

        ShiftOffsetsRight(offsets, vertexCount, _targets.Length);
        return new CompressedGraph(offsets, targets);
    }

    /// <summary>
    /// Writes the vertices reachable from <paramref name="source"/> into <paramref name="destination"/> in
    /// breadth-first order, starting with <paramref name="source"/> itself.
    /// </summary>
    /// <param name="source">The vertex to start from.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <returns>The number of vertices written.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is outside <c>[0, VertexCount)</c>.</exception>
    /// <remarks>
    /// The destination doubles as the traversal queue, so the only other state is a visited bitmap rented from
    /// <see cref="ArrayPool{T}"/> and returned — a repeated traversal allocates nothing. Writing stops when
    /// the buffer is full, and because the order is generated front to back, a truncated result is exactly the
    /// first <c>destination.Length</c> vertices of the full breadth-first order rather than an arbitrary
    /// subset. Size the buffer at <see cref="VertexCount"/> when every reachable vertex is needed.
    /// </remarks>
    public int CopyBreadthFirstOrder(int source, Span<int> destination)
    {
        ThrowIfVertexOutOfRange(source, nameof(source));

        if (destination.IsEmpty)
            return 0;

        // A one-slot buffer is full as soon as the source is written, so answering it must not cost a bitmap
        // sized for every vertex — the rent and the clear would both be O(VertexCount) to return one element.
        if (destination.Length == 1)
        {
            destination[0] = source;
            return 1;
        }

        int wordCount = (VertexCount + 63) >> 6;
        ulong[] rented = ArrayPool<ulong>.Shared.Rent(wordCount);
        Span<ulong> visited = rented.AsSpan(0, wordCount);
        try
        {
            // The pool hands back whatever the last borrower left, so the marks must start clear.
            visited.Clear();

            destination[0] = source;
            Mark(visited, source);

            // The one-slot case returned above, and the inner loop returns the moment the buffer fills, so no
            // iteration here ever starts with no room left.
            int count = 1;
            for (int head = 0; head < count; head++)
            {
                // The offsets are read directly rather than through Neighbors, whose range check is dead
                // weight here: a vertex came off this queue only because it was already a legal target.
                int vertex = destination[head];
                int end = _offsets[vertex + 1];
                for (int i = _offsets[vertex]; i < end; i++)
                {
                    int neighbor = _targets[i];
                    if (!TryMark(visited, neighbor))
                        continue;

                    destination[count++] = neighbor;
                    if (count == destination.Length)
                        return count;
                }
            }

            return count;
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Returns the vertices reachable from <paramref name="source"/> in breadth-first order, starting with
    /// <paramref name="source"/> itself.
    /// </summary>
    /// <param name="source">The vertex to start from.</param>
    /// <returns>The reachable vertices, in breadth-first order.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is outside <c>[0, VertexCount)</c>.</exception>
    /// <remarks>
    /// This is the convenience tier: it allocates a buffer for the whole vertex set, because how much of the
    /// graph a source reaches is not known until the walk has run, and copies the result down to size when the
    /// traversal reached less than all of it. Use <see cref="CopyBreadthFirstOrder"/> on a hot path.
    /// </remarks>
    public int[] GetBreadthFirstOrder(int source)
    {
        int[] order = new int[VertexCount];
        int count = CopyBreadthFirstOrder(source, order);
        return count == order.Length ? order : order[..count];
    }

    /// <summary>
    /// Writes a topological order of the vertices into <paramref name="destination"/> — every vertex before
    /// all of its targets — if one exists.
    /// </summary>
    /// <param name="destination">The buffer to fill. Must hold at least <see cref="VertexCount"/> vertices.</param>
    /// <returns><c>true</c> if the graph is acyclic and an order was written; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <see cref="VertexCount"/>.</exception>
    /// <remarks>
    /// Kahn's algorithm, <c>O(V + E)</c>, with the destination doubling as the queue and the in-degree counts
    /// rented from <see cref="ArrayPool{T}"/>. Unlike <see cref="CopyBreadthFirstOrder"/> the buffer may not
    /// be short: a topological order is all-or-nothing, and a prefix of one is not a useful answer. On
    /// <c>false</c> the graph has a cycle and the contents of <paramref name="destination"/> are unspecified.
    /// Which order is produced among the several a graph may admit is unspecified and may change.
    /// </remarks>
    public bool TryCopyTopologicalOrder(Span<int> destination)
    {
        int vertexCount = VertexCount;
        if (destination.Length < vertexCount)
            throw new ArgumentException("Destination must hold at least VertexCount vertices.", nameof(destination));

        int[] rented = ArrayPool<int>.Shared.Rent(vertexCount);
        Span<int> indegrees = rented.AsSpan(0, vertexCount);
        indegrees.Clear();
        try
        {
            foreach (int target in _targets)
                indegrees[target]++;

            int count = 0;
            for (int vertex = 0; vertex < vertexCount; vertex++)
            {
                if (indegrees[vertex] == 0)
                    destination[count++] = vertex;
            }

            for (int head = 0; head < count; head++)
            {
                int vertex = destination[head];
                int end = _offsets[vertex + 1];
                for (int i = _offsets[vertex]; i < end; i++)
                {
                    if (--indegrees[_targets[i]] == 0)
                        destination[count++] = _targets[i];
                }
            }

            // Every vertex on a cycle keeps a positive in-degree forever, so it is never enqueued.
            return count == vertexCount;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Returns a topological order of the vertices — every vertex before all of its targets — if one exists.
    /// </summary>
    /// <param name="order">
    /// Receives the order when the graph is acyclic, or an empty array when it is not.
    /// </param>
    /// <returns><c>true</c> if the graph is acyclic; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// This is the convenience tier and allocates the result. Use <see cref="TryCopyTopologicalOrder"/> on a
    /// hot path. Which order is produced among the several a graph may admit is unspecified and may change.
    /// </remarks>
    public bool TryGetTopologicalOrder(out int[] order)
    {
        int[] result = new int[VertexCount];
        if (!TryCopyTopologicalOrder(result))
        {
            order = [];
            return false;
        }

        order = result;
        return true;
    }

    /// <summary>Returns an enumerator over every edge in source-major, then ascending-target, order.</summary>
    /// <returns>A struct enumerator over the edges.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<GraphEdge> IEnumerable<GraphEdge>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static void ExclusivePrefixSum(int[] offsets, int vertexCount)
    {
        int sum = 0;
        for (int vertex = 0; vertex < vertexCount; vertex++)
        {
            int degree = offsets[vertex];
            offsets[vertex] = sum;
            sum += degree;
        }

        offsets[vertexCount] = sum;
    }

    // The scatter leaves offsets[v] one past vertex v's last slot — which is vertex v + 1's start — so the
    // array is the right one shifted by a place, and shifting it back is cheaper than keeping a second cursor
    // array alive through the build.
    private static void ShiftOffsetsRight(int[] offsets, int vertexCount, int edgeCount)
    {
        for (int vertex = vertexCount; vertex > 0; vertex--)
            offsets[vertex] = offsets[vertex - 1];

        offsets[0] = 0;
        offsets[vertexCount] = edgeCount;
    }

    private static void Mark(Span<ulong> visited, int vertex) => visited[vertex >> 6] |= 1UL << vertex;

    private static bool TryMark(Span<ulong> visited, int vertex)
    {
        ref ulong word = ref visited[vertex >> 6];
        ulong bit = 1UL << vertex;
        if ((word & bit) != 0)
            return false;

        word |= bit;
        return true;
    }

    private static void Build(GraphEdge[] items, int vertexCount, int[] offsets, int[] targets, string parameterName)
    {
        foreach (GraphEdge edge in items)
        {
            if ((uint)edge.Source >= (uint)vertexCount || (uint)edge.Target >= (uint)vertexCount)
            {
                throw new ArgumentException(
                    $"Edge {edge} has an endpoint outside [0, {vertexCount}).",
                    parameterName);
            }

            offsets[edge.Source]++;
        }

        ExclusivePrefixSum(offsets, vertexCount);

        foreach (GraphEdge edge in items)
            targets[offsets[edge.Source]++] = edge.Target;

        ShiftOffsetsRight(offsets, vertexCount, items.Length);
        Deduplicate(vertexCount, offsets, targets);
    }

    // Sorts each vertex's targets and compacts the duplicates out in place. The write cursor never overtakes
    // the read cursor — it only ever falls behind, by exactly the number of duplicates already dropped — so
    // the compaction needs no second array.
    private static void Deduplicate(int vertexCount, int[] offsets, int[] targets)
    {
        int write = 0;
        for (int vertex = 0; vertex < vertexCount; vertex++)
        {
            int start = offsets[vertex];
            int end = offsets[vertex + 1];
            offsets[vertex] = write;
            if (start == end)
                continue;

            Span<int> slice = targets.AsSpan(start, end - start);
            slice.Sort();

            int previous = slice[0];
            targets[write++] = previous;
            for (int i = 1; i < slice.Length; i++)
            {
                int target = slice[i];
                if (target == previous)
                    continue;

                targets[write++] = target;
                previous = target;
            }
        }

        offsets[vertexCount] = write;
    }

    private int SourceOf(int index)
    {
        // The smallest vertex whose slice ends past index. Isolated vertices repeat an offset, so this must be
        // an upper-bound search rather than an equality one.
        int low = 0;
        int high = VertexCount - 1;
        while (low < high)
        {
            int mid = low + ((high - low) >> 1);
            if (_offsets[mid + 1] > index)
                high = mid;
            else
                low = mid + 1;
        }

        return low;
    }

    private void ThrowIfVertexOutOfRange(int vertex, string parameterName)
    {
        if ((uint)vertex >= (uint)VertexCount)
            throw new ArgumentOutOfRangeException(parameterName, vertex, "Vertex must be in the range [0, VertexCount).");
    }

    /// <summary>Enumerates the edges of a <see cref="CompressedGraph"/> in source-major order.</summary>
    public struct Enumerator : IEnumerator<GraphEdge>
    {
        private readonly CompressedGraph _graph;
        private int _index;
        private int _source;
        private GraphEdge _current;

        internal Enumerator(CompressedGraph graph)
        {
            _graph = graph;
            _index = 0;
            _source = 0;
            _current = default;
        }

        /// <summary>Gets the edge at the current position of the enumerator.</summary>
        public readonly GraphEdge Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next edge.</summary>
        /// <returns><c>true</c> if there is a next edge; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            if (_index < _graph._targets.Length)
            {
                // Walking the sources forward keeps this O(1) amortized per edge, where the indexer's own
                // binary search would make a full enumeration O(E log V).
                while (_index >= _graph._offsets[_source + 1])
                    _source++;

                _current = new GraphEdge(_source, _graph._targets[_index]);
                _index++;
                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first edge.</summary>
        public void Reset()
        {
            _index = 0;
            _source = 0;
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
