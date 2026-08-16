using System.Collections;

namespace Celerity.Collections;

/// <summary>
/// An <b>R-tree</b>: a build-once, immutable spatial index over axis-aligned <i>rectangles</i>, answering
/// <i>which of these boxes overlap this box</i> and <i>which contain this point</i> without testing every
/// stored box — instead of the <c>O(n)</c> those questions otherwise cost on every query regardless.
/// </summary>
/// <typeparam name="TValue">The payload carried by each box.</typeparam>
/// <remarks>
/// <para>
/// .NET ships nothing for this question. There is no R-tree, no bounding-volume hierarchy and no spatial index
/// of any kind in the BCL — <c>System.Drawing</c> ships a <c>Rectangle</c> with an <c>IntersectsWith</c> on it
/// and no index over a collection of them. The idiomatic answer is an array of boxes and a loop that tests all
/// of them. This is the gap the type fills: collision broadphase for bodies that have a size rather than
/// particles, map label and marker placement, hit-testing a UI or a canvas, viewport culling of sized objects,
/// and spatial joins between two box sets.
/// </para>
/// <para>
/// <b><see cref="KdTree{TValue}"/> cannot stand in.</b> A point index answers <i>what is near this
/// coordinate</i>; it has nothing to say about an object that occupies an area, because a box can overlap the
/// query while its centre sits far outside it — a long thin road segment crossing the viewport is the ordinary
/// case, not a contrived one. That is the two-dimensional form of exactly the argument that made
/// <see cref="IntervalTree{TKey, TValue}"/> necessary next to <see cref="BTreeSet{T, TComparer}"/> on one axis.
/// </para>
/// <para>
/// <b>A uniform-cell grid is the other neighbour, and the reason to prefer one is mutability rather than
/// shape.</b> The received wisdom is that bucketing extents into fixed cells wins when they are all about one
/// size, because then a cell size exists that fits them all — and that an R-tree earns its keep only when
/// extents vary by orders of magnitude. <c>RTreeShapeBenchmark</c> measures that rather than repeating it, and
/// it does not hold on query cost: against a bucketed grid sized by the standard mean-extent heuristic, this
/// type is 1.25x ahead on the varying shape and <i>1.98x</i> ahead on the uniform one, because a grid's query
/// cost is dominated by the cells the query covers rather than the boxes in them, while an R-tree's node boxes
/// get tighter as the extents get more alike. A grid tuned to a known query size would close some of that gap.
/// What remains a real reason to reach for a grid is that it is mutable and this type is not.
/// </para>
/// <para>
/// <b>Layout.</b> The boxes are permuted by <b>STR (Sort-Tile-Recursive) packing</b> and a fixed-fanout tree is
/// laid over them <i>implicitly</i>: leaf <c>i</c> owns entries <c>[i&#215;16, i&#215;16+16)</c>, and a node at
/// any level above owns the same fixed run of the level below it. There are no per-node heap objects and no
/// child pointers — the whole structure is one flat array of entry extents, one payload array, and one flat
/// array of node bounding boxes with the leaf level first. STR is what makes the implicit layout legitimate:
/// sorting a level by centre-x, cutting it into <c>&#8730;</c>(node count) vertical slices, then sorting each
/// slice by centre-y puts spatially near boxes at adjacent indices, so a run of consecutive entries has a
/// tight bounding box rather than an arbitrary one.
/// </para>
/// <para>
/// The packing is recursive rather than applied once per level, which is what keeps the tree implicit: the
/// tiling at the root partitions the entries into the subtree-sized runs its children own, and each run is
/// then tiled again for the level below. A sort per level makes the build <c>O(n log n log n)</c> in the
/// number of levels — five sorts at 100,000 boxes — which is the price of the index and is what a caller
/// amortizes by querying it repeatedly.
/// </para>
/// <para>
/// <b>What the bound really is.</b> An R-tree has no useful worst-case query bound: overlapping node boxes
/// mean a query can descend into several children at each level, and an adversarial box set forces it into all
/// of them. What <i>is</i> guaranteed is that a query visits each node and each entry at most once, so every
/// family is <c>O(n)</c> — bounded by the hand-written loop it replaces rather than by anything worse. The
/// useful statement is empirical and is about <b>selectivity</b>: pruning discards subtrees whose bounding box
/// misses the query, so a query whose answer is a large fraction of the tree prunes little and converges on
/// the scan.
/// </para>
/// <para>
/// <b>Which baseline you pick decides the headline, so read both.</b> At 100,000 boxes whose extents span
/// three orders of magnitude, with a query selecting roughly a thousandth of them, the overlap query measures
/// <b>175x</b> against the array-and-a-loop the BCL leaves you with, and <b>10.2x</b> against a
/// <i>hand-rolled</i> alternative — the boxes ordered by <c>minX</c>, binary-searched to
/// <c>query.minX - maxWidth</c> and scanned forward while <c>minX &#8804; query.maxX</c>, which is effectively
/// a one-dimensional R-tree. The second column is the honest one: the second dimension and the extent
/// hierarchy are the only things this type adds over it. <b>At 1,000 boxes that margin falls to 1.35x</b>, and
/// on the point query to 1.12x — the index has not yet paid for its indirection, and the crossover is stated
/// rather than rounded away. The ratios also track <b>selectivity</b> rather than size, so a query ten times
/// wider narrows them considerably. Measured in <c>RTreeBenchmark</c> and tabulated in the README.
/// </para>
/// <para>
/// <b>Build-once.</b> The tree is immutable; adding a box means building a new one, as with
/// <see cref="KdTree{TValue}"/>, <see cref="FrozenCelerityDictionary{TValue}"/> and
/// <see cref="RankSelectBitVector"/>. Keeping an R-tree balanced under insertion needs node splits and
/// reinsertion, which is a different type with a different cost profile, not an overload of this one — do not
/// reach for it when the extents move every frame, since rebuilding per frame costs more than the queries
/// save. Because nothing mutates, enumeration is never invalidated and concurrent readers need no
/// synchronization, with no comparer caveat to attach to that: every query is a comparison on
/// <see cref="double"/> and calls nothing the caller supplied.
/// </para>
/// <para>
/// <b>Two query tiers.</b> The <c>Contains</c> and <c>Count</c> members and the two <c>Copy</c> methods
/// allocate nothing at all; <see cref="GetOverlapping"/> and <see cref="GetAtPoint"/> are the convenience tier
/// and allocate the result array. All of them share one traversal, generic over a <c>struct</c> visitor, so
/// the JIT specializes it per call site and inlines the per-match work rather than paying a delegate or an
/// interface call per hit. There is one query <i>region</i> rather than two, because a point query is an
/// overlap query against a degenerate box and splitting them would buy nothing.
/// </para>
/// <para>
/// Boxes are kept distinct: two boxes with identical extents stay two entries and a query reports both.
/// Entries are exposed through <see cref="IReadOnlyList{T}"/> in the tree's own packed order, which is
/// deterministic for a given input sequence but is not a spatial or an insertion order and should not be
/// relied on. Queries likewise report matches in an unspecified order.
/// </para>
/// <para>
/// Edges are closed, so boxes that touch along an edge or at a corner do overlap and a point exactly on an
/// edge is inside. A query coordinate of <see cref="double.NaN"/> has no position and matches nothing. A
/// stored coordinate can never be <see cref="double.NaN"/> or infinite, and neither upper edge may precede its
/// lower one — the constructor rejects all three.
/// </para>
/// </remarks>
public sealed class RTree<TValue> : IReadOnlyList<SpatialBox<TValue>>
{
    // The fanout. Sixteen children of four doubles each is 512 bytes — eight cache lines scanned linearly to
    // answer one node, against the pointer chase a per-node object would cost. It is also what makes the tree
    // implicit: a node's children are a fixed run of the level below, so nothing links them.
    private const int NodeCapacity = 16;

    // Entry extents, interleaved as minX, minY, maxX, maxY, in packed order — the payload is kept out of them
    // because a query reads extents at every entry a leaf holds and _values only for the ones it returns.
    private readonly double[] _boxes;
    private readonly TValue?[] _values;

    // Every node's bounding box, same interleaving, with the leaf level first and the root last. _levelStart
    // has one entry per level plus a sentinel, so level L occupies [_levelStart[L], _levelStart[L + 1]) and the
    // count of any level is a subtraction. Empty for an empty tree, which is the one case with no root.
    private readonly double[] _nodes;
    private readonly int[] _levelStart;

    /// <summary>Builds a tree over <paramref name="boxes"/>.</summary>
    /// <param name="boxes">The boxes to index. The sequence is read once and copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="boxes"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// A box has a coordinate that is not finite, or an upper edge that precedes its lower edge.
    /// </exception>
    public RTree(IEnumerable<SpatialBox<TValue>> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        // A counted source is sized and copied once, as the sibling trees' constructors do. Going through a
        // List<T> unconditionally would allocate and copy a second backing array for the commonest sources of
        // all — an array or a List — on a type whose build cost is already the tradeoff it asks callers to make.
        SpatialBox<TValue>[] items;
        if (boxes is ICollection<SpatialBox<TValue>> counted)
        {
            items = new SpatialBox<TValue>[counted.Count];
            counted.CopyTo(items, 0);
        }
        else
        {
            items = new List<SpatialBox<TValue>>(boxes).ToArray();
        }

        Validate(items, nameof(boxes));

        _boxes = new double[items.Length * 4];
        _values = new TValue?[items.Length];
        _levelStart = MeasureLevels(items.Length);
        _nodes = new double[_levelStart[^1] * 4];

        Pack(items);
        Fill(items);
    }

    /// <summary>Gets the number of boxes in the tree, counting duplicate extents.</summary>
    public int Count => _values.Length;

    /// <summary>Gets the box at <paramref name="index"/> in the tree's packed order.</summary>
    /// <param name="index">The zero-based position in packed order.</param>
    /// <returns>The box stored at that position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    /// <remarks>
    /// The order is the tree's internal packing, not insertion order and not any spatial order. It is
    /// deterministic for a given input sequence, but it is an implementation detail and may change.
    /// </remarks>
    public SpatialBox<TValue> this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_values.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be in the range [0, Count).");

            return EntryAt(index);
        }
    }

    /// <summary>Gets the bounding box of every stored box — the root's own extent.</summary>
    /// <param name="minX">Receives the inclusive left edge, or <c>0</c> when the tree is empty.</param>
    /// <param name="minY">Receives the inclusive bottom edge, or <c>0</c> when the tree is empty.</param>
    /// <param name="maxX">Receives the inclusive right edge, or <c>0</c> when the tree is empty.</param>
    /// <param name="maxY">Receives the inclusive top edge, or <c>0</c> when the tree is empty.</param>
    /// <returns><c>true</c> if the tree holds at least one box; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// This is read straight off the root rather than computed, so it costs nothing. It is the cheapest way to
    /// reject a query that cannot match anything at all.
    /// </remarks>
    public bool TryGetBounds(out double minX, out double minY, out double maxX, out double maxY)
    {
        if (_values.Length == 0)
        {
            minX = minY = maxX = maxY = 0;
            return false;
        }

        int at = (_levelStart[^1] - 1) * 4;
        minX = _nodes[at];
        minY = _nodes[at + 1];
        maxX = _nodes[at + 2];
        maxY = _nodes[at + 3];
        return true;
    }

    // ---- overlap ------------------------------------------------------------------------------------

    /// <summary>
    /// Determines whether any stored box overlaps the closed box <c>[minX, maxX] &#215; [minY, maxY]</c>. Stops
    /// at the first match, which is what makes this the right member for a collision or hit test.
    /// </summary>
    /// <param name="minX">The inclusive left edge of the query.</param>
    /// <param name="minY">The inclusive bottom edge of the query.</param>
    /// <param name="maxX">The inclusive right edge of the query. Must not precede <paramref name="minX"/>.</param>
    /// <param name="maxY">The inclusive top edge of the query. Must not precede <paramref name="minY"/>.</param>
    /// <returns><c>true</c> if at least one stored box overlaps the query; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException">An upper edge precedes its lower edge.</exception>
    public bool ContainsOverlapping(double minX, double minY, double maxX, double maxY)
    {
        AnyVisitor visitor = default;
        Search(Region(minX, minY, maxX, maxY), ref visitor);
        return visitor.Found;
    }

    /// <summary>Counts the stored boxes overlapping the closed box <c>[minX, maxX] &#215; [minY, maxY]</c>.</summary>
    /// <param name="minX">The inclusive left edge of the query.</param>
    /// <param name="minY">The inclusive bottom edge of the query.</param>
    /// <param name="maxX">The inclusive right edge of the query. Must not precede <paramref name="minX"/>.</param>
    /// <param name="maxY">The inclusive top edge of the query. Must not precede <paramref name="minY"/>.</param>
    /// <returns>The number of stored boxes overlapping the query.</returns>
    /// <exception cref="ArgumentException">An upper edge precedes its lower edge.</exception>
    public int CountOverlapping(double minX, double minY, double maxX, double maxY)
    {
        CountVisitor visitor = default;
        Search(Region(minX, minY, maxX, maxY), ref visitor);
        return visitor.Count;
    }

    /// <summary>
    /// Writes the stored boxes overlapping the closed box <c>[minX, maxX] &#215; [minY, maxY]</c> into
    /// <paramref name="destination"/>, allocating nothing.
    /// </summary>
    /// <param name="minX">The inclusive left edge of the query.</param>
    /// <param name="minY">The inclusive bottom edge of the query.</param>
    /// <param name="maxX">The inclusive right edge of the query. Must not precede <paramref name="minX"/>.</param>
    /// <param name="maxY">The inclusive top edge of the query. Must not precede <paramref name="minY"/>.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <param name="destinationIndex">The position in <paramref name="destination"/> to start writing at.</param>
    /// <returns>The number of boxes written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="destinationIndex"/> is outside <c>[0, destination.Length]</c>.</exception>
    /// <exception cref="ArgumentException">An upper edge precedes its lower edge.</exception>
    /// <remarks>
    /// Writing stops when the buffer is full, so a return value equal to the remaining room may mean the
    /// matches were truncated. Size the buffer with <see cref="CountOverlapping"/> when every match is needed.
    /// Matches are reported in an unspecified order.
    /// </remarks>
    public int CopyOverlapping(double minX, double minY, double maxX, double maxY, SpatialBox<TValue>[] destination, int destinationIndex = 0)
    {
        ValidateDestination(destination, destinationIndex);
        return CopyRegion(Region(minX, minY, maxX, maxY), destination, destinationIndex);
    }

    /// <summary>Returns the stored boxes overlapping the closed box <c>[minX, maxX] &#215; [minY, maxY]</c>.</summary>
    /// <param name="minX">The inclusive left edge of the query.</param>
    /// <param name="minY">The inclusive bottom edge of the query.</param>
    /// <param name="maxX">The inclusive right edge of the query. Must not precede <paramref name="minX"/>.</param>
    /// <param name="maxY">The inclusive top edge of the query. Must not precede <paramref name="minY"/>.</param>
    /// <returns>The matching boxes in an unspecified order, or an empty array when none match.</returns>
    /// <exception cref="ArgumentException">An upper edge precedes its lower edge.</exception>
    /// <remarks>
    /// This is the convenience tier: it allocates the result and walks the tree twice, once to size the array
    /// exactly and once to fill it. Use <see cref="CopyOverlapping"/> on a hot path.
    /// </remarks>
    public SpatialBox<TValue>[] GetOverlapping(double minX, double minY, double maxX, double maxY)
        => GetRegion(Region(minX, minY, maxX, maxY));

    // ---- point --------------------------------------------------------------------------------------

    /// <summary>
    /// Determines whether any stored box contains the point <c>(x, y)</c>, edges included. Stops at the first
    /// match.
    /// </summary>
    /// <param name="x">The horizontal coordinate to test.</param>
    /// <param name="y">The vertical coordinate to test.</param>
    /// <returns><c>true</c> if at least one stored box contains the point; otherwise <c>false</c>.</returns>
    public bool ContainsAtPoint(double x, double y)
    {
        AnyVisitor visitor = default;
        Search(new PlaneRegion(x, y, x, y), ref visitor);
        return visitor.Found;
    }

    /// <summary>Counts the stored boxes containing the point <c>(x, y)</c>, edges included.</summary>
    /// <param name="x">The horizontal coordinate to test.</param>
    /// <param name="y">The vertical coordinate to test.</param>
    /// <returns>The number of stored boxes containing the point.</returns>
    public int CountAtPoint(double x, double y)
    {
        CountVisitor visitor = default;
        Search(new PlaneRegion(x, y, x, y), ref visitor);
        return visitor.Count;
    }

    /// <summary>
    /// Writes the stored boxes containing the point <c>(x, y)</c> into <paramref name="destination"/>,
    /// allocating nothing.
    /// </summary>
    /// <param name="x">The horizontal coordinate to test.</param>
    /// <param name="y">The vertical coordinate to test.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <param name="destinationIndex">The position in <paramref name="destination"/> to start writing at.</param>
    /// <returns>The number of boxes written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="destinationIndex"/> is outside <c>[0, destination.Length]</c>.</exception>
    /// <remarks>
    /// Writing stops when the buffer is full, so a return value equal to the remaining room may mean the
    /// matches were truncated. Size the buffer with <see cref="CountAtPoint"/> when every match is needed.
    /// Matches are reported in an unspecified order.
    /// </remarks>
    public int CopyAtPoint(double x, double y, SpatialBox<TValue>[] destination, int destinationIndex = 0)
    {
        ValidateDestination(destination, destinationIndex);
        return CopyRegion(new PlaneRegion(x, y, x, y), destination, destinationIndex);
    }

    /// <summary>Returns the stored boxes containing the point <c>(x, y)</c>, edges included.</summary>
    /// <param name="x">The horizontal coordinate to test.</param>
    /// <param name="y">The vertical coordinate to test.</param>
    /// <returns>The matching boxes in an unspecified order, or an empty array when none match.</returns>
    /// <remarks>
    /// This is the convenience tier: it allocates the result and walks the tree twice, once to size the array
    /// exactly and once to fill it. Use <see cref="CopyAtPoint"/> on a hot path.
    /// </remarks>
    public SpatialBox<TValue>[] GetAtPoint(double x, double y) => GetRegion(new PlaneRegion(x, y, x, y));

    /// <summary>Returns an enumerator over every stored box in the tree's packed order.</summary>
    /// <returns>A struct enumerator over the boxes.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<SpatialBox<TValue>> IEnumerable<SpatialBox<TValue>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    private static void Validate(SpatialBox<TValue>[] items, string paramName)
    {
        for (int i = 0; i < items.Length; i++)
        {
            SpatialBox<TValue> box = items[i];

            // Non-finite edges are rejected rather than stored. A NaN edge fails every comparison, so such a
            // box could not be found even by a query for its own extent, and an infinite one would overlap
            // every query while giving the packing a centre it cannot order.
            if (!double.IsFinite(box.MinX) || !double.IsFinite(box.MinY) ||
                !double.IsFinite(box.MaxX) || !double.IsFinite(box.MaxY))
            {
                throw new ArgumentException("A box's coordinates must be finite.", paramName);
            }

            if (box.MaxX < box.MinX || box.MaxY < box.MinY)
                throw new ArgumentException("A box's upper edge must not precede its lower edge.", paramName);
        }
    }

    // The level sizes, bottom-up: ceil(n / 16) leaves, then a node per 16 of the level below until one is left.
    // Returned as start offsets with a trailing sentinel, so a level's count is a subtraction and the total node
    // count is the last element. An empty tree gets a single zero — no levels, no root, and nothing to walk.
    private static int[] MeasureLevels(int count)
    {
        if (count == 0)
            return [0];

        var starts = new List<int>(6) { 0 };
        int total = 0;
        int level = (count + NodeCapacity - 1) / NodeCapacity;

        while (true)
        {
            total += level;
            starts.Add(total);
            if (level == 1)
                return [.. starts];

            level = (level + NodeCapacity - 1) / NodeCapacity;
        }
    }

    // STR packing, applied recursively from the root down rather than once per level. Tiling the whole entry
    // array into the runs the root's children own, then tiling each run for the level below, is what lets the
    // tree stay implicit: after this, entries [i * cap, i * cap + cap) are exactly subtree i at every level.
    private void Pack(SpatialBox<TValue>[] items)
    {
        int levels = _levelStart.Length - 1;
        if (levels < 2)
            return;

        // Entries per child of the root: 16 when its children are leaves, 16^2 one level further up. A long
        // because 16^8 overruns an int at the tallest tree an int-indexed array can hold.
        long capacity = NodeCapacity;
        for (int i = 2; i < levels; i++)
            capacity *= NodeCapacity;

        Tile(items, new double[items.Length], 0, items.Length, capacity);
    }

    // Orders [lo, hi) so that each consecutive run of `capacity` entries is one child subtree of this node:
    // sort by centre-x, cut into sqrt(children) vertical slices, sort each slice by centre-y. The slice count
    // is what makes the result a tiling rather than a strip — sqrt is the choice that leaves the tiles roughly
    // square, which is what keeps a node's bounding box tight in both axes instead of one.
    private static void Tile(SpatialBox<TValue>[] items, double[] keys, int lo, int hi, long capacity)
    {
        int children = (int)((hi - lo + capacity - 1) / capacity);

        // One child means the whole range is that child's subtree, so there is nothing at this level to cut.
        if (children > 1)
        {
            int slices = (int)Math.Ceiling(Math.Sqrt(children));
            long sliceEntries = (long)((children + slices - 1) / slices) * capacity;

            SortRange(items, keys, lo, hi, byX: true);
            for (long slice = lo; slice < hi; slice += sliceEntries)
                SortRange(items, keys, (int)slice, (int)Math.Min(hi, slice + sliceEntries), byX: false);
        }

        // At this point the children are leaves, and the order of entries inside one leaf changes nothing:
        // a leaf's bounding box is the same however its own entries are arranged.
        if (capacity <= NodeCapacity)
            return;

        for (long child = lo; child < hi; child += capacity)
            Tile(items, keys, (int)child, (int)Math.Min(hi, child + capacity), capacity / NodeCapacity);
    }

    // Sorts [lo, hi) on the centre of one axis. The keys go into a scratch array and ride along with the items
    // through Array.Sort's two-array overload, which compares doubles directly — an IComparer<SpatialBox<T>>
    // would box on the way in and pay an interface call per comparison, on the one path whose whole cost is
    // comparisons.
    private static void SortRange(SpatialBox<TValue>[] items, double[] keys, int lo, int hi, bool byX)
    {
        for (int i = lo; i < hi; i++)
        {
            SpatialBox<TValue> box = items[i];
            keys[i] = byX ? Centre(box.MinX, box.MaxX) : Centre(box.MinY, box.MaxY);
        }

        Array.Sort(keys, items, lo, hi - lo);
    }

    // Halving each edge before adding them cannot overflow for any pair of finite doubles, where the obvious
    // (min + max) / 2 overflows to infinity near the top of the range and would hand the sort a key it cannot
    // order against its neighbours.
    private static double Centre(double min, double max) => (min * 0.5) + (max * 0.5);

    // Copies the packed entries into the flat arrays, then folds the bounding boxes upward: each leaf over the
    // run of entries it owns, each node above over the run of nodes it owns.
    private void Fill(SpatialBox<TValue>[] items)
    {
        // An empty tree has no levels at all, so there is no leaf row to fold into and no root to fold up to.
        if (items.Length == 0)
            return;

        for (int i = 0; i < items.Length; i++)
        {
            SpatialBox<TValue> box = items[i];
            int at = i * 4;
            _boxes[at] = box.MinX;
            _boxes[at + 1] = box.MinY;
            _boxes[at + 2] = box.MaxX;
            _boxes[at + 3] = box.MaxY;
            _values[i] = box.Value;
        }

        int leaves = _levelStart[1];
        for (int leaf = 0; leaf < leaves; leaf++)
        {
            int start = leaf * NodeCapacity;
            Cover(_boxes, start, Math.Min(items.Length, start + NodeCapacity), leaf);
        }

        for (int level = 1; level < _levelStart.Length - 1; level++)
        {
            int below = _levelStart[level - 1];
            int belowCount = _levelStart[level] - below;

            for (int node = 0; node < _levelStart[level + 1] - _levelStart[level]; node++)
            {
                int start = node * NodeCapacity;
                Cover(_nodes, below + start, below + Math.Min(belowCount, start + NodeCapacity), _levelStart[level] + node);
            }
        }
    }

    // The bounding box of source entries [start, end) written to node `target`. The source is the entry array
    // for a leaf and the node array for everything above, and both are interleaved the same way, so one routine
    // does the whole fold.
    private void Cover(double[] source, int start, int end, int target)
    {
        double minX = double.PositiveInfinity;
        double minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity;
        double maxY = double.NegativeInfinity;

        for (int i = start; i < end; i++)
        {
            int at = i * 4;
            minX = Math.Min(minX, source[at]);
            minY = Math.Min(minY, source[at + 1]);
            maxX = Math.Max(maxX, source[at + 2]);
            maxY = Math.Max(maxY, source[at + 3]);
        }

        int to = target * 4;
        _nodes[to] = minX;
        _nodes[to + 1] = minY;
        _nodes[to + 2] = maxX;
        _nodes[to + 3] = maxY;
    }

    private static PlaneRegion Region(double minX, double minY, double maxX, double maxY)
    {
        if (maxX < minX)
            throw new ArgumentException("The box's right edge must not precede its left edge.", nameof(maxX));
        if (maxY < minY)
            throw new ArgumentException("The box's top edge must not precede its bottom edge.", nameof(maxY));

        return new PlaneRegion(minX, minY, maxX, maxY);
    }

    private static void ValidateDestination(SpatialBox<TValue>[] destination, int destinationIndex)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if ((uint)destinationIndex > (uint)destination.Length)
            throw new ArgumentOutOfRangeException(nameof(destinationIndex), destinationIndex,
                "Destination index must be in the range [0, destination.Length].");
    }

    private int CopyRegion(in PlaneRegion region, SpatialBox<TValue>[] destination, int destinationIndex)
    {
        if (destinationIndex == destination.Length)
            return 0;

        var visitor = new CopyVisitor(this, destination, destinationIndex);
        Search(in region, ref visitor);
        return visitor.Written;
    }

    private SpatialBox<TValue>[] GetRegion(in PlaneRegion region)
    {
        CountVisitor counter = default;
        Search(in region, ref counter);
        if (counter.Count == 0)
            return Array.Empty<SpatialBox<TValue>>();

        var result = new SpatialBox<TValue>[counter.Count];
        var visitor = new CopyVisitor(this, result, 0);
        Search(in region, ref visitor);
        return result;
    }

    private void Search<TVisitor>(in PlaneRegion region, ref TVisitor visitor)
        where TVisitor : struct, IBoxVisitor
    {
        if (_values.Length != 0)
            Walk(_levelStart.Length - 2, 0, in region, ref visitor);
    }

    // The single traversal behind every query. A node whose bounding box misses the query cannot hold a match,
    // which is the whole of the pruning and is why a selective query never touches most of the tree; testing it
    // on entry rather than at each child is what lets the root be rejected by the same line.
    //
    // Returns false once the visitor has asked to stop, which unwinds the whole recursion rather than only the
    // current level. Depth is the level count — at most eight for any int-indexed array — so the call stack is
    // bounded by construction.
    private bool Walk<TVisitor>(int level, int node, in PlaneRegion region, ref TVisitor visitor)
        where TVisitor : struct, IBoxVisitor
    {
        int at = (_levelStart[level] + node) * 4;
        if (!region.Overlaps(_nodes[at], _nodes[at + 1], _nodes[at + 2], _nodes[at + 3]))
            return true;

        int start = node * NodeCapacity;

        if (level == 0)
        {
            int end = Math.Min(_values.Length, start + NodeCapacity);
            for (int entry = start; entry < end; entry++)
            {
                int box = entry * 4;
                if (region.Overlaps(_boxes[box], _boxes[box + 1], _boxes[box + 2], _boxes[box + 3]) &&
                    !visitor.Visit(entry))
                {
                    return false;
                }
            }

            return true;
        }

        int lastChild = Math.Min(_levelStart[level] - _levelStart[level - 1], start + NodeCapacity);
        for (int child = start; child < lastChild; child++)
        {
            if (!Walk(level - 1, child, in region, ref visitor))
                return false;
        }

        return true;
    }

    private SpatialBox<TValue> EntryAt(int index)
    {
        int at = index * 4;
        return new SpatialBox<TValue>(_boxes[at], _boxes[at + 1], _boxes[at + 2], _boxes[at + 3], _values[index]);
    }

    // The query region. One shape covers both families, because asking which boxes contain a point is asking
    // which overlap a box whose edges are equal — a separate point region would run the same four comparisons.
    // A NaN query coordinate fails all of them, so such a query prunes everything and reports nothing.
    private readonly struct PlaneRegion
    {
        private readonly double _minX;
        private readonly double _minY;
        private readonly double _maxX;
        private readonly double _maxY;

        internal PlaneRegion(double minX, double minY, double maxX, double maxY)
        {
            _minX = minX;
            _minY = minY;
            _maxX = maxX;
            _maxY = maxY;
        }

        // Closed edges on both sides, so boxes meeting at an edge or a corner overlap — which is what makes a
        // degenerate query box a point test rather than a query that matches nothing.
        internal bool Overlaps(double minX, double minY, double maxX, double maxY)
            => minX <= _maxX && maxX >= _minX && minY <= _maxY && maxY >= _minY;
    }

    // What each query does with a match. A readonly struct used as a generic type argument is specialized by
    // the JIT and inlined into the walk, so the shared traversal costs nothing over three hand-written ones —
    // the same rule the struct hashers, IMonoid<T> and DefaultComparer<T> follow.
    private interface IBoxVisitor
    {
        // Returns false to stop the walk.
        bool Visit(int index);
    }

    private struct AnyVisitor : IBoxVisitor
    {
        public bool Found;

        public bool Visit(int index)
        {
            Found = true;
            return false;
        }
    }

    private struct CountVisitor : IBoxVisitor
    {
        public int Count;

        public bool Visit(int index)
        {
            Count++;
            return true;
        }
    }

    private struct CopyVisitor : IBoxVisitor
    {
        private readonly RTree<TValue> _tree;
        private readonly SpatialBox<TValue>[] _destination;
        private readonly int _start;
        private int _index;

        internal CopyVisitor(RTree<TValue> tree, SpatialBox<TValue>[] destination, int destinationIndex)
        {
            _tree = tree;
            _destination = destination;
            _start = destinationIndex;
            _index = destinationIndex;
        }

        internal readonly int Written => _index - _start;

        // The callers guarantee at least one free slot before starting the walk, so there is no entry guard
        // here: the first write is always in range, and the return value stops the walk the moment the last
        // slot is filled rather than letting it run on to a match it could not write anyway.
        public bool Visit(int index)
        {
            _destination[_index++] = _tree.EntryAt(index);
            return _index < _destination.Length;
        }
    }

    /// <summary>A struct enumerator over an R-tree's entries in the tree's packed order.</summary>
    /// <remarks>
    /// The tree is immutable, so there is no version to check and no concurrent-modification failure mode:
    /// an enumerator can never be invalidated.
    /// </remarks>
    public struct Enumerator : IEnumerator<SpatialBox<TValue>>
    {
        private readonly RTree<TValue> _tree;
        private int _index;
        private SpatialBox<TValue> _current;

        internal Enumerator(RTree<TValue> tree)
        {
            _tree = tree;
            _index = 0;
            _current = default;
        }

        /// <summary>Gets the box at the current position of the enumerator.</summary>
        public readonly SpatialBox<TValue> Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next box.</summary>
        /// <returns><c>true</c> if there is a next box; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            if (_index < _tree._values.Length)
            {
                _current = _tree.EntryAt(_index);
                _index++;
                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first box.</summary>
        public void Reset()
        {
            _index = 0;
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
