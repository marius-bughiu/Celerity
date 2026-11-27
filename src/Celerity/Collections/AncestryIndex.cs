using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable</b> index over a rooted forest — an org chart, a category taxonomy, a directory tree, a
/// commit graph's first-parent chain — given as a parent array. It answers "is <c>a</c> above <c>b</c>?" in
/// <c>O(1)</c>, the lowest common ancestor and the distance between two nodes in <c>O(1)</c>, the ancestor
/// <c>k</c> levels up in <c>O(log n)</c>, and hands back every descendant of a node as a contiguous span.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it replaces.</b> The BCL has no tree type, so a hierarchy in .NET is almost always an
/// <c>int[] parents</c> (or a parent property on an entity) and a loop that walks it: to ask whether a
/// manager is above an employee, walk the employee's chain until it reaches the manager or runs out; to find
/// two nodes' common ancestor, lift the deeper one to the other's depth and walk both up in step. Every step
/// of that walk is a load that depends on the one before, so a query costs the depth of the tree in dependent
/// cache misses — cheap for a shallow tree that fits in cache, and linear in the depth for a deep one, which is
/// exactly the shape (long management chains, deep folder trees, version histories) where the question gets
/// asked most. This index answers the same questions with a constant number of loads, whatever the depth.
/// </para>
/// <para>
/// <b>How.</b> The constructor numbers the nodes in <i>preorder</i> (children in ascending id order, trees in
/// ascending root id order), which places every subtree in one contiguous run: node <c>a</c> is an ancestor of
/// <c>b</c> exactly when <c>b</c>'s preorder number falls inside <c>a</c>'s run, one subtraction and one
/// unsigned compare. For the lowest common ancestor of two nodes <c>u</c>, <c>v</c> with <c>u</c> earlier in
/// preorder, the shallowest node strictly after <c>u</c> and up to <c>v</c> in preorder is a child of their
/// common ancestor, so the answer is that node's parent — a range-minimum query over the preorder depths. That
/// query is answered in <c>O(1)</c>: a sparse table over blocks of 64 positions handles the
/// whole blocks, and a 64-bit mask per position (a monotonic stack recorded as bits) handles the partial blocks
/// at either end with one trailing-zero count each. In a forest, two nodes in different trees have a root
/// between them in preorder, whose parent is <c>-1</c> — so the same query reports "no common ancestor"
/// without a separate tree-membership check.
/// </para>
/// <para>
/// <b>Subtrees as spans.</b> Because every subtree is a contiguous preorder run, <see cref="GetSubtree"/>
/// returns a node's descendants as a <see cref="ReadOnlySpan{T}"/> slice with no traversal and no allocation,
/// and <see cref="GetPreorderIndex"/> maps a node to its position so that a subtree becomes the index range
/// <c>[GetPreorderIndex(v), GetPreorderIndex(v) + GetSubtreeSize(v))</c>. That turns "sum the headcount cost
/// under this manager" into a range query on a <see cref="FenwickTree{T}"/> or
/// <see cref="SegmentTree{T, TMonoid}"/> laid out in preorder.
/// </para>
/// <para>
/// <b>What it costs.</b> Every pass of the build is linear except the block sparse table, which holds
/// <c>(n / 64) * (floor(log2(n / 64)) + 1)</c> entries: <c>O(n + (n / 64) log(n / 64))</c> in all, and fewer
/// than <c>0.4n</c> table entries for any forest an array can hold. The index retains about 41 bytes of array
/// payload per node on a bushy tree (40 fixed, plus 1.4 for the block table at 100,000 nodes) and up to four
/// more on a deep one, since the per-depth directory holds one <see cref="int"/> per distinct depth;
/// <see cref="MemoryUsageInBytes"/> reports the exact figure. The structure is fixed at construction: a changed
/// hierarchy means building a new index. That is the trade — on a tree that changes on every request, the
/// parent walk's zero setup cost wins, and on a shallow tree that fits in cache the walk is only a handful of
/// cached loads, so the margin there is small. The index earns its place on a hierarchy that is queried far
/// more often than it changes, and the deeper the tree, the larger the margin.
/// </para>
/// <para>
/// The type holds no mutable state after construction, so instances are safe to share across threads.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // parents[i] is the parent of node i, or -1 for a root.
/// //        0
/// //      /   \
/// //     1     2
/// //    / \     \
/// //   3   4     5
/// var tree = new AncestryIndex(new[] { -1, 0, 0, 1, 1, 2 });
///
/// tree.IsAncestor(1, 4);            // true
/// tree.LowestCommonAncestor(3, 5);  // 0
/// tree.Distance(3, 5);              // 4
/// tree.GetAncestor(4, 2);           // 0
/// tree.GetSubtree(1).ToArray();     // [1, 3, 4]
/// </code>
/// </example>
public sealed class AncestryIndex
{
    // Positions per block of the range-minimum structure: one bit per position in a ulong mask.
    private const int BlockShift = 6;
    private const int BlockSize = 1 << BlockShift;
    private const int BlockMask = BlockSize - 1;

    // Per node, indexed by node id. Everything a single-node query reads sits in one 16-byte entry, so a node
    // costs one cache line however many of its facts the query needs.
    private readonly NodeInfo[] _nodes;

    // Preorder position -> node id. The whole forest in preorder; every subtree is a contiguous slice.
    private readonly int[] _order;

    // Preorder position -> (depth, parent) of the node there. The range-minimum query runs over the depths and
    // its answer is the parent at the winning position, so both live in one entry.
    private readonly PreorderEntry[] _byPreorder;

    // Preorder position -> in-block monotonic stack after that position, one bit per position of its block.
    private readonly ulong[] _masks;

    // Sparse table over block minima, flat: row k (starting at k * _blockCount) holds, at offset b, the minimum
    // over blocks [b, b + 2^k) as a packed (depth << 32 | position) so a plain long compare picks the shallower.
    private readonly long[] _blockTable;
    private readonly int _blockCount;

    // Preorder positions grouped by depth, ascending inside each group: depth d occupies
    // [_levelStart[d], _levelStart[d + 1]). The level-ancestor query binary-searches one group.
    private readonly int[] _levelPositions;
    private readonly int[] _levelStart;

    private readonly int _rootCount;

    /// <summary>
    /// Builds the index over the forest described by <paramref name="parents"/>, in
    /// <c>O(n + (n / 64) log(n / 64))</c>: linear passes plus the block sparse table.
    /// </summary>
    /// <param name="parents">
    /// The parent of each node: <c>parents[i]</c> is the id of node <c>i</c>'s parent, or <c>-1</c> if node
    /// <c>i</c> is a root. Node ids are the indices <c>[0, parents.Length)</c>. Any number of roots is allowed,
    /// including none for an empty forest. Copied, so later changes to the source are not observed.
    /// </param>
    /// <exception cref="ArgumentException">
    /// An entry is less than <c>-1</c>, not a valid node id, or the node's own id; or the parent links contain
    /// a cycle, so some node has no root above it.
    /// </exception>
    /// <remarks>
    /// The constructor takes a <see cref="ReadOnlySpan{T}"/> because a parent array is the shape a hierarchy
    /// already has; an <c>int[]</c> or a <see cref="List{T}"/> via <c>CollectionsMarshal.AsSpan</c> passes
    /// straight in.
    /// </remarks>
    public AncestryIndex(ReadOnlySpan<int> parents)
    {
        int n = parents.Length;

        // ---- Validate and gather each node's children (CSR, ascending id within each parent) ----
        int[] childStart = new int[n + 1];
        int rootCount = 0;
        for (int i = 0; i < n; i++)
        {
            int p = parents[i];
            if (p == -1)
            {
                rootCount++;
                continue;
            }

            if ((uint)p >= (uint)n || p == i)
                ThrowInvalidParent(i, p, n);

            childStart[p + 1]++;
        }

        for (int i = 0; i < n; i++)
            childStart[i + 1] += childStart[i];

        int[] children = new int[n - rootCount];
        int[] cursor = new int[n];
        Array.Copy(childStart, cursor, n);
        for (int i = 0; i < n; i++)
        {
            int p = parents[i];
            if (p >= 0)
                children[cursor[p]++] = i;
        }

        // ---- Breadth-first order from the roots: depths, and a top-down order for the passes below ----
        // `cursor` is free again and becomes the queue; `depth` and `size` live in the node entries.
        var nodes = new NodeInfo[n];
        int[] queue = cursor;
        int tail = 0;
        for (int i = 0; i < n; i++)
        {
            if (parents[i] == -1)
                queue[tail++] = i;
        }

        for (int head = 0; head < tail; head++)
        {
            int u = queue[head];
            int childDepth = nodes[u].Depth + 1;
            for (int c = childStart[u], end = childStart[u + 1]; c < end; c++)
            {
                int child = children[c];
                nodes[child].Depth = childDepth;
                queue[tail++] = child;
            }
        }

        // A node on a cycle is never reached from a root, so the walk comes up short.
        if (tail != n)
            throw new ArgumentException(
                "The parent links contain a cycle: some node has no root above it.", nameof(parents));

        // ---- Subtree sizes, bottom-up (reverse breadth-first order visits every child before its parent) ----
        for (int i = 0; i < n; i++)
        {
            nodes[i].Size = 1;
            nodes[i].Parent = parents[i];
        }

        for (int i = n - 1; i >= 0; i--)
        {
            int v = queue[i];
            int p = nodes[v].Parent;
            if (p >= 0)
                nodes[p].Size += nodes[v].Size;
        }

        // ---- Preorder numbers, top-down: each child's run starts where its elder siblings' runs end ----
        int offset = 0;
        for (int i = 0; i < rootCount; i++)
        {
            int root = queue[i];
            nodes[root].Preorder = offset;
            offset += nodes[root].Size;
        }

        for (int i = 0; i < n; i++)
        {
            int u = queue[i];
            int next = nodes[u].Preorder + 1;
            for (int c = childStart[u], end = childStart[u + 1]; c < end; c++)
            {
                int child = children[c];
                nodes[child].Preorder = next;
                next += nodes[child].Size;
            }
        }

        int[] order = new int[n];
        var byPreorder = new PreorderEntry[n];
        int maxDepth = -1;
        for (int v = 0; v < n; v++)
        {
            ref NodeInfo info = ref nodes[v];
            order[info.Preorder] = v;
            byPreorder[info.Preorder] = new PreorderEntry(info.Depth, info.Parent);
            if (info.Depth > maxDepth)
                maxDepth = info.Depth;
        }

        _nodes = nodes;
        _order = order;
        _byPreorder = byPreorder;
        _rootCount = rootCount;

        // ---- Range-minimum structure over the preorder depths ----
        _masks = BuildMasks(byPreorder);
        _blockCount = (n + BlockMask) >> BlockShift;
        _blockTable = BuildBlockTable(byPreorder, _blockCount);

        // ---- Preorder positions grouped by depth, for the level-ancestor search ----
        int[] levelStart = new int[maxDepth + 2];
        for (int pos = 0; pos < n; pos++)
            levelStart[byPreorder[pos].Depth + 1]++;

        for (int d = 0; d <= maxDepth; d++)
            levelStart[d + 1] += levelStart[d];

        int[] levelPositions = new int[n];
        int[] fill = new int[maxDepth + 1];
        Array.Copy(levelStart, fill, maxDepth + 1);
        for (int pos = 0; pos < n; pos++)
            levelPositions[fill[byPreorder[pos].Depth]++] = pos;

        _levelStart = levelStart;
        _levelPositions = levelPositions;
    }

    /// <summary>Gets the number of nodes in the forest.</summary>
    public int Count => _nodes.Length;

    /// <summary>Gets the number of roots — nodes whose parent is <c>-1</c>. A single tree has one.</summary>
    public int RootCount => _rootCount;

    /// <summary>
    /// Gets the number of distinct depths: <c>0</c> for an empty forest, <c>1</c> for a forest of roots only,
    /// and one more than the depth of the deepest node otherwise.
    /// </summary>
    public int Height => _levelStart.Length - 1;

    /// <summary>
    /// Gets every node in preorder: each root followed by its subtree, children visited in ascending id order,
    /// and the trees in ascending root id order. The subtree of node <c>v</c> is the slice starting at
    /// <see cref="GetPreorderIndex"/><c>(v)</c> of length <see cref="GetSubtreeSize"/><c>(v)</c>.
    /// </summary>
    public ReadOnlySpan<int> Preorder => _order;

    /// <summary>
    /// Gets the payload bytes of the arrays the index retains: 40 per node, the block sparse table (about 1.4
    /// per node at 100,000 nodes, growing with <c>log(n / 64)</c>), and four per distinct depth, which is up to
    /// four more per node on a path-like tree. Object headers and the temporary arrays the constructor
    /// allocates and drops are not counted.
    /// </summary>
    public long MemoryUsageInBytes =>
        (long)_nodes.Length * Unsafe.SizeOf<NodeInfo>()
        + (long)_order.Length * sizeof(int)
        + (long)_byPreorder.Length * Unsafe.SizeOf<PreorderEntry>()
        + (long)_masks.Length * sizeof(ulong)
        + (long)_blockTable.Length * sizeof(long)
        + (long)_levelPositions.Length * sizeof(int)
        + (long)_levelStart.Length * sizeof(int);

    /// <summary>Returns the parent of <paramref name="node"/>, or <c>-1</c> if it is a root.</summary>
    /// <param name="node">The node id. Must be in <c>[0, Count)</c>.</param>
    /// <returns>The parent's id, or <c>-1</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int GetParent(int node) => Node(node, nameof(node)).Parent;

    /// <summary>Returns the depth of <paramref name="node"/>: <c>0</c> for a root, its parent's plus one otherwise.</summary>
    /// <param name="node">The node id. Must be in <c>[0, Count)</c>.</param>
    /// <returns>The number of edges between the node and its root.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int GetDepth(int node) => Node(node, nameof(node)).Depth;

    /// <summary>Returns the number of nodes in the subtree rooted at <paramref name="node"/>, itself included.</summary>
    /// <param name="node">The node id. Must be in <c>[0, Count)</c>.</param>
    /// <returns>The subtree's node count; <c>1</c> for a leaf.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int GetSubtreeSize(int node) => Node(node, nameof(node)).Size;

    /// <summary>
    /// Returns the position of <paramref name="node"/> in <see cref="Preorder"/>. Its subtree occupies the
    /// positions <c>[GetPreorderIndex(node), GetPreorderIndex(node) + GetSubtreeSize(node))</c>, which is what
    /// lets a per-node value array laid out in preorder answer subtree aggregates as range queries.
    /// </summary>
    /// <param name="node">The node id. Must be in <c>[0, Count)</c>.</param>
    /// <returns>The node's preorder position, in <c>[0, Count)</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int GetPreorderIndex(int node) => Node(node, nameof(node)).Preorder;

    /// <summary>
    /// Returns <paramref name="node"/> and all of its descendants, in preorder, as a slice of
    /// <see cref="Preorder"/> — no traversal and no allocation.
    /// </summary>
    /// <param name="node">The node id. Must be in <c>[0, Count)</c>.</param>
    /// <returns>The subtree's node ids, <paramref name="node"/> first.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public ReadOnlySpan<int> GetSubtree(int node)
    {
        ref readonly NodeInfo info = ref Node(node, nameof(node));
        return new ReadOnlySpan<int>(_order, info.Preorder, info.Size);
    }

    /// <summary>
    /// Returns whether <paramref name="ancestor"/> is <paramref name="node"/> itself or lies on the path from
    /// <paramref name="node"/> to its root, in <c>O(1)</c>.
    /// </summary>
    /// <param name="ancestor">The candidate ancestor. Must be in <c>[0, Count)</c>.</param>
    /// <param name="node">The candidate descendant. Must be in <c>[0, Count)</c>.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="node"/> is in the subtree rooted at <paramref name="ancestor"/>, including
    /// when the two are the same node; <c>false</c> otherwise, and always for nodes in different trees.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Either node is out of range.</exception>
    /// <remarks>
    /// The test is inclusive, as the lowest-common-ancestor convention is: a node is its own ancestor. For a
    /// strict test, also check <c>ancestor != node</c>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAncestor(int ancestor, int node)
    {
        ref readonly NodeInfo a = ref Node(ancestor, nameof(ancestor));
        int position = Node(node, nameof(node)).Preorder;
        return (uint)(position - a.Preorder) < (uint)a.Size;
    }

    /// <summary>
    /// Returns the deepest node that is an ancestor of both <paramref name="first"/> and
    /// <paramref name="second"/> (each counting as its own ancestor), in <c>O(1)</c>.
    /// </summary>
    /// <param name="first">A node id. Must be in <c>[0, Count)</c>.</param>
    /// <param name="second">A node id. Must be in <c>[0, Count)</c>.</param>
    /// <returns>
    /// The lowest common ancestor's id; the node itself when the two are equal or one is an ancestor of the
    /// other; or <c>-1</c> when they are in different trees of the forest.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Either node is out of range.</exception>
    public int LowestCommonAncestor(int first, int second)
    {
        int a = Node(first, nameof(first)).Preorder;
        int b = Node(second, nameof(second)).Preorder;
        if (a == b)
            return first;

        // Order the pair without a data-dependent branch: which node comes first in preorder is a coin flip for
        // arbitrary pairs, so a branch here mispredicts about half the time.
        int low = Math.Min(a, b);
        int high = Math.Max(a, b);
        return _byPreorder[ShallowestPosition(low + 1, high)].Parent;
    }

    /// <summary>
    /// Returns the number of edges on the path between <paramref name="first"/> and <paramref name="second"/>,
    /// in <c>O(1)</c>.
    /// </summary>
    /// <param name="first">A node id. Must be in <c>[0, Count)</c>.</param>
    /// <param name="second">A node id. Must be in <c>[0, Count)</c>.</param>
    /// <returns>
    /// <c>depth(first) + depth(second) - 2 * depth(lca)</c>; <c>0</c> for the same node; or <c>-1</c> when the
    /// two are in different trees and no path joins them.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Either node is out of range.</exception>
    public int Distance(int first, int second)
    {
        int lca = LowestCommonAncestor(first, second);
        if (lca < 0)
            return -1;

        // Each leg is at most the path length, and a path in a tree has at most Count - 1 edges, so this cannot
        // overflow where depth(first) + depth(second) could.
        int lcaDepth = _nodes[lca].Depth;
        return (_nodes[first].Depth - lcaDepth) + (_nodes[second].Depth - lcaDepth);
    }

    /// <summary>
    /// Returns the ancestor <paramref name="levels"/> steps above <paramref name="node"/>, in <c>O(log n)</c>:
    /// the node itself for <c>0</c>, its parent for <c>1</c>, and so on.
    /// </summary>
    /// <param name="node">The node id. Must be in <c>[0, Count)</c>.</param>
    /// <param name="levels">How many steps to climb. Must be non-negative.</param>
    /// <returns>
    /// The ancestor's id, or <c>-1</c> when <paramref name="levels"/> exceeds the node's depth (the climb
    /// would pass its root).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="node"/> is out of range, or <paramref name="levels"/> is negative.
    /// </exception>
    /// <remarks>
    /// The search runs over the nodes at the target depth, which are stored in preorder: the ancestor is the
    /// last of them that precedes <paramref name="node"/>, since any later one at that depth would sit inside
    /// the ancestor's own subtree, where every node is deeper. So the cost is a binary search over one depth's
    /// worth of nodes, whatever the number of levels climbed.
    /// </remarks>
    public int GetAncestor(int node, int levels)
    {
        ref readonly NodeInfo info = ref Node(node, nameof(node));
        if (levels < 0)
            throw new ArgumentOutOfRangeException(nameof(levels), levels, "levels must be non-negative.");

        if (levels > info.Depth)
            return -1;

        if (levels <= 1)
            return levels == 0 ? node : info.Parent;

        int depth = info.Depth - levels;
        int start = _levelStart[depth];
        ReadOnlySpan<int> level = new(_levelPositions, start, _levelStart[depth + 1] - start);

        // The node's own position is never in this level (it is deeper), so the search always misses and its
        // complement is the first position after the node; the ancestor is the one before that.
        int found = ~level.BinarySearch(info.Preorder) - 1;
        return _order[level[found]];
    }

    /// <summary>
    /// Returns the root of the tree that contains <paramref name="node"/> (the node itself if it is a root),
    /// in <c>O(log n)</c>.
    /// </summary>
    /// <param name="node">The node id. Must be in <c>[0, Count)</c>.</param>
    /// <returns>The root's id.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="node"/> is out of range.</exception>
    public int GetRoot(int node) => GetAncestor(node, Node(node, nameof(node)).Depth);

    // ---- Range-minimum query over the preorder depths ----

    // The position of a shallowest node in the inclusive preorder range [left, right], left <= right.
    private int ShallowestPosition(int left, int right)
    {
        int leftBlock = left >> BlockShift;
        int rightBlock = right >> BlockShift;
        if (leftBlock == rightBlock)
            return InBlock(left, right);

        // The tail of the left block, the head of the right block, and the whole blocks between, if any.
        // The candidates are combined with Math.Min rather than an if: which one wins is data-dependent, and a
        // conditional select costs less than the mispredicted branch it replaces.
        long best = Math.Min(
            Pack(InBlock(left, left | BlockMask)),
            Pack((rightBlock << BlockShift) + BitOperations.TrailingZeroCount(_masks[right])));

        if (rightBlock - leftBlock > 1)
            best = Math.Min(best, BlockRange(leftBlock + 1, rightBlock - 1));

        return (int)best;
    }

    // The position of a shallowest node in [left, right] inside one block. _masks[right] records the in-block
    // positions whose depth is below every later depth up to `right`; the lowest of them at or after `left`
    // is the range's minimum.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int InBlock(int left, int right)
    {
        ulong candidates = _masks[right] & (ulong.MaxValue << (left & BlockMask));
        return (right & ~BlockMask) + BitOperations.TrailingZeroCount(candidates);
    }

    // The packed minimum over the inclusive block range [first, last], from two overlapping power-of-two rows.
    private long BlockRange(int first, int last)
    {
        int level = BitOperations.Log2((uint)(last - first + 1));
        int row = level * _blockCount;
        return Math.Min(_blockTable[row + first], _blockTable[row + last - (1 << level) + 1]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long Pack(int position) => Pack(_byPreorder[position].Depth, position);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Pack(int depth, int position) => ((long)depth << 32) | (uint)position;

    private static ulong[] BuildMasks(PreorderEntry[] byPreorder)
    {
        int n = byPreorder.Length;
        ulong[] masks = new ulong[n];
        for (int blockStart = 0; blockStart < n; blockStart += BlockSize)
        {
            int blockEnd = Math.Min(blockStart + BlockSize, n);
            ulong stack = 0;
            for (int i = blockStart; i < blockEnd; i++)
            {
                int depth = byPreorder[i].Depth;

                // Pop every stacked position at least as deep as this one: from now on this position beats
                // them for any range that reaches it. What stays is strictly shallower than all that follow.
                while (stack != 0)
                {
                    int top = BitOperations.Log2(stack);
                    if (byPreorder[blockStart + top].Depth < depth)
                        break;

                    stack &= ~(1UL << top);
                }

                stack |= 1UL << (i - blockStart);
                masks[i] = stack;
            }
        }

        return masks;
    }

    private static long[] BuildBlockTable(PreorderEntry[] byPreorder, int blockCount)
    {
        if (blockCount == 0)
            return [];

        int levels = BitOperations.Log2((uint)blockCount) + 1;
        long[] table = new long[levels * blockCount];
        int n = byPreorder.Length;

        for (int b = 0; b < blockCount; b++)
        {
            int start = b << BlockShift;
            int end = Math.Min(start + BlockSize, n);
            long min = long.MaxValue;
            for (int i = start; i < end; i++)
            {
                long packed = Pack(byPreorder[i].Depth, i);
                if (packed < min)
                    min = packed;
            }

            table[b] = min;
        }

        for (int k = 1; k < levels; k++)
        {
            int half = 1 << (k - 1);
            int row = k * blockCount;
            int below = row - blockCount;
            for (int b = 0; b + (1 << k) <= blockCount; b++)
            {
                long low = table[below + b];
                long high = table[below + b + half];
                table[row + b] = low < high ? low : high;
            }
        }

        return table;
    }

    // ---- Argument checks ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref readonly NodeInfo Node(int node, string paramName)
    {
        NodeInfo[] nodes = _nodes;
        if ((uint)node >= (uint)nodes.Length)
            ThrowNodeOutOfRange(node, paramName);

        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(nodes), node);
    }

    [DoesNotReturn]
    private static void ThrowNodeOutOfRange(int node, string paramName) =>
        throw new ArgumentOutOfRangeException(paramName, node, "The node id must be in the range [0, Count).");

    [DoesNotReturn]
    private static void ThrowInvalidParent(int node, int parent, int count) =>
        throw new ArgumentException(
            parent == node
                ? $"parents[{node}] is {parent}: a node cannot be its own parent."
                : $"parents[{node}] is {parent}: a parent must be -1 or a node id in [0, {count}).",
            "parents");

    [StructLayout(LayoutKind.Sequential)]
    private struct NodeInfo
    {
        public int Parent;
        public int Depth;
        public int Preorder;
        public int Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PreorderEntry(int depth, int parent)
    {
        public readonly int Depth = depth;
        public readonly int Parent = parent;
    }
}
