using System.Collections;
using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// A <b>2-D k-d tree</b>: a build-once, immutable spatial index that answers <i>which point is nearest to
/// this one</i>, <i>which points lie within this radius</i> and <i>which lie inside this box</i> without
/// measuring every stored point — instead of the <c>O(n)</c> those questions otherwise cost on every query
/// regardless.
/// </summary>
/// <typeparam name="TValue">The payload carried by each point.</typeparam>
/// <remarks>
/// <para>
/// .NET ships nothing for this question. There is no k-d tree, no quadtree, no R-tree and no spatial index of
/// any kind in the BCL — <c>System.Drawing</c> ships geometry <i>primitives</i> with no index over them, and
/// <c>System.Numerics</c> ships vectors, not containers. The idiomatic answer is an array of points and a loop
/// that measures all of them. This is the gap the type fills: nearest store / driver / sensor to a coordinate,
/// viewport and map-tile culling, game collision broadphase, the neighbour queries that dominate k-means and
/// DBSCAN, snap-to-nearest in an editor, and duplicate-coordinate detection.
/// </para>
/// <para>
/// The sibling range structures index one ordered axis and cannot answer this.
/// <see cref="BTreeSet{T, TComparer}"/> orders keys and <see cref="SegmentTree{T, TMonoid}"/> folds values
/// stored at positions, but neither expresses proximity in two dimensions at once, because there is no total
/// order on the plane under which near points are neighbours: sorting by x puts two points a hair apart
/// vertically at opposite ends of the array.
/// </para>
/// <para>
/// <b>Layout.</b> The points are permuted into two flat arrays and a balanced binary tree is laid over
/// them <i>implicitly</i>: the node for the index range <c>[lo, hi)</c> sits at its midpoint and its subtree is
/// exactly <c>[lo, hi)</c>. Each level splits on the axis the level before it did not — x, then y, then x —
/// and the build puts the median on that axis at the midpoint, so everything left of a node is at or before it
/// on that axis and everything right is at or after. There are no nodes, no child pointers and no per-point
/// heap object; the whole structure is one interleaved coordinate array and one payload array. The median
/// is found by <b>introselect</b> rather than a full
/// sort, which keeps the build to an <i>expected</i> <c>O(n log n)</c> rather than the
/// <c>O(n log&#178; n)</c> a sort per level would cost — and the depth budget behind the <i>intro</i> bounds
/// the worst case at that same <c>O(n log&#178; n)</c> instead of letting it go quadratic. The budget is not
/// theatre: a middle-element pivot settles ordered and reverse-ordered input in <c>log m</c> passes, but an
/// organ-pipe arrangement — ascending values interleaved with descending ones, the shape of any path that goes
/// out and comes back — puts an extreme at the midpoint of every subrange and peels off one element per pass.
/// </para>
/// <para>
/// <b>The coordinate domain is bounded.</b> Every query compares <i>squared</i> distances, so the constructor
/// rejects a coordinate that is not finite or exceeds <c>1e153</c> in magnitude — beyond that a squared
/// separation overflows to infinity, and two far-apart points would compare equal instead of merely losing
/// precision. Query coordinates are not checked (that would be a per-query cost for a case no real
/// coordinate system reaches); passing one beyond the same magnitude gives distances this type cannot order.
/// </para>
/// <para>
/// The same arithmetic puts a <i>floor</i> under the domain, which is documented rather than enforced
/// because no build-time check could see it: a separation below roughly <c>1e-162</c> squares to a value
/// below the smallest subnormal and underflows to zero, so two points that close are indistinguishable from
/// coincident ones — a zero radius will match them and a nearest query may order them arbitrarily. Points
/// that genuinely coincide are unaffected and well defined; it is the nonzero-but-tinier-than-<c>1e-162</c>
/// separation that cannot be represented. Comparing squared distances is what keeps every query off the
/// square root, and paying a scaled or <c>hypot</c>-style comparison on the hot path to resolve separations
/// no coordinate system produces is not a trade this type makes.
/// </para>
/// <para>
/// <b>What the bound really is.</b> A k-d tree has no useful worst-case query bound — an adversarial point set
/// forces every query to visit every node, and even on friendly data the classic <c>O(log n)</c> figure for
/// nearest-neighbour is an average over uniformly distributed points, not a guarantee. What <i>is</i>
/// guaranteed is that a query visits at most <c>n</c> nodes, so each family is bounded by the hand-written
/// loop it replaces rather than by anything worse: <c>O(n)</c> for the nearest and range queries, whose
/// per-node work is constant, and <c>O(n log k)</c> for the k-nearest queries, whose per-candidate work is a
/// sift through the <c>k</c>-element heap — which is what the equivalent hand-rolled bounded-heap scan costs
/// too. The useful statement is the empirical one, and it is about <b>selectivity</b> rather than size:
/// pruning works by discarding subtrees that cannot hold a result, so a query whose answer is a large
/// fraction of the tree prunes little and converges on the scan.
/// </para>
/// <para>
/// <b>Which baseline you pick decides the headline, so read both.</b> Against the array-and-a-loop the BCL
/// leaves you with, the nearest query at 100,000 uniformly scattered points measures <b>278x</b> and the radius
/// query <b>54x</b>. Against a <i>hand-rolled</i> alternative — the points ordered by x, scanned outward from
/// the query and abandoned in each direction once the horizontal gap alone exceeds the best distance so far,
/// which is effectively a one-dimensional spatial index — the same queries measure <b>1.9x</b> and
/// <b>3.4x</b>, and at 1,000 points that hand-roll is slightly <i>ahead</i>. The second column is the honest
/// one: the second dimension is the only thing this type adds over it. Both are measured in
/// <c>KdTreeBenchmark</c> and tabulated in the README.
/// </para>
/// <para>
/// <b>Two dimensions specifically, not a generic <c>k</c>.</b> The dimensionality is a design decision here
/// rather than a type parameter: a generic-<c>k</c> tree has to take every coordinate as a
/// <c>ReadOnlySpan&lt;double&gt;</c> at the API boundary and store an array per point in the layout, which
/// costs exactly the flat-array design this type exists for. The plane is where the .NET workloads above live.
/// </para>
/// <para>
/// <b>Build-once.</b> The tree is immutable; adding a point means building a new one, as with
/// <see cref="FrozenCelerityDictionary{TValue}"/>, <see cref="XorFilter{T, THasher}"/> and
/// <see cref="RankSelectBitVector"/>. Keeping a k-d tree balanced under insertion needs periodic subtree
/// rebuilds, which is a different type with a different cost profile, not an overload of this one. Because
/// nothing mutates, enumeration is never invalidated and concurrent readers need no synchronization — and
/// unlike the comparer-parameterized trees there is no caveat to attach to that, since every query is
/// arithmetic on <see cref="double"/> and calls nothing the caller supplied.
/// </para>
/// <para>
/// <b>Two query tiers.</b> <see cref="TryFindNearest(double, double, out SpatialPoint{TValue})"/>, the
/// <c>Contains</c> and <c>Count</c> members and the three <c>Copy</c> methods allocate nothing at all;
/// <see cref="GetNearest"/>, <see cref="GetWithin"/> and <see cref="GetInRectangle"/> are the convenience tier
/// and allocate the result array. All of them share two traversals — one for the range queries, generic over
/// both a <c>struct</c> region and a <c>struct</c> visitor, and one for the nearest queries — so the JIT
/// specializes each per call site and inlines the per-match work rather than paying a delegate or an interface
/// call per hit.
/// </para>
/// <para>
/// Points are kept distinct: two points at identical coordinates stay two entries and a query reports both.
/// Duplicates are preserved. Entries are exposed through <see cref="IReadOnlyList{T}"/> in the tree's own
/// layout order, which is deterministic for a given input sequence but is not a spatial or an insertion order
/// and should not be relied on. Range queries likewise report matches in an unspecified order; only
/// <see cref="GetNearest"/> and <see cref="CopyNearest"/> order their results, by ascending distance.
/// </para>
/// <para>
/// A query coordinate of <see cref="double.NaN"/> has no position and matches nothing: the nearest queries
/// report no result and the range queries report an empty one. A stored coordinate can never be
/// <see cref="double.NaN"/> or infinite — the constructor rejects both, along with any magnitude past the
/// bound described above.
/// </para>
/// </remarks>
public sealed class KdTree<TValue> : IReadOnlyList<SpatialPoint<TValue>>
{
    // The implicit tree needs no links: the node covering the index range [lo, hi) is at
    // mid = lo + (hi - lo) / 2, its left child covers [lo, mid) and its right child [mid + 1, hi).
    //
    // The coordinates are interleaved as x, y, x, y rather than kept in an array each, so a node is contiguous;
    // the payload is not among them, because a walk reads coordinates at every node it visits and _values only
    // for the handful it actually returns.
    //
    // Interleaving was measured against the array-per-axis alternative rather than assumed, and the honest
    // result is that it made no difference: 168.4 us against 165.9 us on the 100k nearest-query arm, inside the
    // run-to-run spread. The reasoning that predicted a win — every node a query reaches needs both of its
    // coordinates, so splitting them by axis fetches two lines to answer one question — is sound as far as it
    // goes, and the likely reason it does not show is that the descent is latency-bound on the pointer-chase
    // through levels rather than bandwidth-bound within a node. It is kept for the smaller invariant (one
    // coordinate array instead of two that must stay the same length), not for a speed claim.
    private readonly double[] _coords;
    private readonly TValue?[] _values;

    /// <summary>Builds a tree over <paramref name="points"/>.</summary>
    /// <param name="points">The points to index. The sequence is read once and copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="points"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// A point has a coordinate that is not finite, or exceeds <c>1e153</c> in magnitude — the bound past which
    /// a squared distance overflows and the queries can no longer order one distance against another.
    /// </exception>
    public KdTree(IEnumerable<SpatialPoint<TValue>> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        // A counted source is sized and copied once, as the sibling trees' constructors do. Going through a
        // List<T> unconditionally would allocate and copy a second backing array for the commonest sources of
        // all — an array or a List — on a type whose build cost is already the tradeoff it asks callers to make.
        SpatialPoint<TValue>[] items;
        if (points is ICollection<SpatialPoint<TValue>> counted)
        {
            items = new SpatialPoint<TValue>[counted.Count];
            counted.CopyTo(items, 0);
        }
        else
        {
            items = new List<SpatialPoint<TValue>>(points).ToArray();
        }

        _coords = new double[items.Length * 2];
        _values = new TValue?[items.Length];
        Build(items, nameof(points));
    }

    /// <summary>Gets the number of points in the tree, counting duplicate coordinates.</summary>
    public int Count => _values.Length;

    /// <summary>Gets the point at <paramref name="index"/> in the tree's layout order.</summary>
    /// <param name="index">The zero-based position in layout order.</param>
    /// <returns>The point stored at that position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    /// <remarks>
    /// The order is the tree's internal layout, not insertion order and not any spatial order. It is
    /// deterministic for a given input sequence, but it is an implementation detail and may change.
    /// </remarks>
    public SpatialPoint<TValue> this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_values.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be in the range [0, Count).");

            return EntryAt(index);
        }
    }

    // ---- nearest ------------------------------------------------------------------------------------

    /// <summary>Finds the point closest to <c>(x, y)</c> by Euclidean distance.</summary>
    /// <param name="x">The horizontal coordinate to search from.</param>
    /// <param name="y">The vertical coordinate to search from.</param>
    /// <param name="nearest">Receives the closest point, or <c>default</c> when there is none.</param>
    /// <returns><c>true</c> if a point was found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Returns <c>false</c> only for an empty tree or a <see cref="double.NaN"/> query coordinate. Points at
    /// equal distance are tied and which one is reported is unspecified.
    /// </remarks>
    public bool TryFindNearest(double x, double y, out SpatialPoint<TValue> nearest)
        => TryFindNearest(x, y, double.PositiveInfinity, out nearest);

    /// <summary>
    /// Finds the point closest to <c>(x, y)</c> by Euclidean distance, considering only points no further
    /// than <paramref name="maxDistance"/> away.
    /// </summary>
    /// <param name="x">The horizontal coordinate to search from.</param>
    /// <param name="y">The vertical coordinate to search from.</param>
    /// <param name="maxDistance">The inclusive distance bound. Must not be negative.</param>
    /// <param name="nearest">Receives the closest point within the bound, or <c>default</c> when there is none.</param>
    /// <returns><c>true</c> if a point was found within the bound; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDistance"/> is negative or <see cref="double.NaN"/>.</exception>
    /// <remarks>
    /// The bound is not only a filter: it seeds the search's pruning radius, so a tight bound makes the query
    /// materially cheaper than an unbounded one followed by a distance test.
    /// </remarks>
    public bool TryFindNearest(double x, double y, double maxDistance, out SpatialPoint<TValue> nearest)
    {
        ValidateDistance(maxDistance, nameof(maxDistance));

        var visitor = new NearestVisitor(Square(maxDistance));
        if (SearchNearest(x, y, ref visitor) && visitor.Index >= 0)
        {
            nearest = EntryAt(visitor.Index);
            return true;
        }

        nearest = default;
        return false;
    }

    /// <summary>
    /// Writes the points closest to <c>(x, y)</c> into <paramref name="destination"/> in ascending distance
    /// order, allocating nothing.
    /// </summary>
    /// <param name="x">The horizontal coordinate to search from.</param>
    /// <param name="y">The vertical coordinate to search from.</param>
    /// <param name="destination">The buffer to fill. Its remaining room is the <c>k</c> of the query.</param>
    /// <param name="destinationIndex">The position in <paramref name="destination"/> to start writing at.</param>
    /// <returns>The number of points written, which is the lesser of the remaining room and <see cref="Count"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="destinationIndex"/> is outside <c>[0, destination.Length]</c>.</exception>
    /// <remarks>
    /// The buffer doubles as the search's own bounded max-heap, which is what makes the k-nearest query
    /// allocation-free: each candidate's distance is recomputed from its stored coordinates rather than parked
    /// in a parallel array. Points at equal distance are tied and their relative order is unspecified.
    /// </remarks>
    public int CopyNearest(double x, double y, SpatialPoint<TValue>[] destination, int destinationIndex = 0)
    {
        ValidateDestination(destination, destinationIndex);

        var visitor = new NearestSetVisitor(this, destination, destinationIndex, x, y);
        if (destinationIndex == destination.Length || !SearchNearest(x, y, ref visitor))
            return 0;

        return visitor.SortAndCount();
    }

    /// <summary>Returns the <paramref name="count"/> points closest to <c>(x, y)</c>, in ascending distance order.</summary>
    /// <param name="x">The horizontal coordinate to search from.</param>
    /// <param name="y">The vertical coordinate to search from.</param>
    /// <param name="count">How many points to return. Fewer are returned when the tree holds fewer.</param>
    /// <returns>The closest points, or an empty array when there are none.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <remarks>This is the convenience tier: it allocates the result. Use <see cref="CopyNearest"/> on a hot path.</remarks>
    public SpatialPoint<TValue>[] GetNearest(double x, double y, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must not be negative.");

        int size = Math.Min(count, _values.Length);
        if (size == 0)
            return Array.Empty<SpatialPoint<TValue>>();

        var result = new SpatialPoint<TValue>[size];
        int written = CopyNearest(x, y, result);

        // A NaN query matches nothing, so the exactly-sized buffer can still come back short.
        return written == size ? result : result[..written];
    }

    // ---- radius -------------------------------------------------------------------------------------

    /// <summary>
    /// Determines whether any point lies within <paramref name="radius"/> of <c>(x, y)</c>. Stops at the first
    /// match, which is what makes this the right member for a proximity or collision check.
    /// </summary>
    /// <param name="x">The horizontal coordinate of the circle's centre.</param>
    /// <param name="y">The vertical coordinate of the circle's centre.</param>
    /// <param name="radius">The inclusive radius. Must not be negative.</param>
    /// <returns><c>true</c> if at least one point lies within the radius; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is negative or <see cref="double.NaN"/>.</exception>
    public bool ContainsWithin(double x, double y, double radius)
    {
        AnyVisitor visitor = default;
        Search(Circle(x, y, radius), ref visitor);
        return visitor.Found;
    }

    /// <summary>Counts the points lying within <paramref name="radius"/> of <c>(x, y)</c>.</summary>
    /// <param name="x">The horizontal coordinate of the circle's centre.</param>
    /// <param name="y">The vertical coordinate of the circle's centre.</param>
    /// <param name="radius">The inclusive radius. Must not be negative.</param>
    /// <returns>The number of points within the radius.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is negative or <see cref="double.NaN"/>.</exception>
    public int CountWithin(double x, double y, double radius)
    {
        CountVisitor visitor = default;
        Search(Circle(x, y, radius), ref visitor);
        return visitor.Count;
    }

    /// <summary>
    /// Writes the points lying within <paramref name="radius"/> of <c>(x, y)</c> into
    /// <paramref name="destination"/>, allocating nothing.
    /// </summary>
    /// <param name="x">The horizontal coordinate of the circle's centre.</param>
    /// <param name="y">The vertical coordinate of the circle's centre.</param>
    /// <param name="radius">The inclusive radius. Must not be negative.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <param name="destinationIndex">The position in <paramref name="destination"/> to start writing at.</param>
    /// <returns>The number of points written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="radius"/> is negative or <see cref="double.NaN"/>, or <paramref name="destinationIndex"/>
    /// is outside <c>[0, destination.Length]</c>.
    /// </exception>
    /// <remarks>
    /// Writing stops when the buffer is full, so a return value equal to the remaining room may mean the
    /// matches were truncated. Size the buffer with <see cref="CountWithin"/> when every match is needed.
    /// Matches are reported in an unspecified order.
    /// </remarks>
    public int CopyWithin(double x, double y, double radius, SpatialPoint<TValue>[] destination, int destinationIndex = 0)
    {
        CopyVisitor visitor = CreateCopyVisitor(destination, destinationIndex);
        CircleRegion region = Circle(x, y, radius);
        if (destinationIndex == destination.Length)
            return 0;

        Search(region, ref visitor);
        return visitor.Written;
    }

    /// <summary>Returns the points lying within <paramref name="radius"/> of <c>(x, y)</c>.</summary>
    /// <param name="x">The horizontal coordinate of the circle's centre.</param>
    /// <param name="y">The vertical coordinate of the circle's centre.</param>
    /// <param name="radius">The inclusive radius. Must not be negative.</param>
    /// <returns>The matching points in an unspecified order, or an empty array when none match.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is negative or <see cref="double.NaN"/>.</exception>
    /// <remarks>
    /// This is the convenience tier: it allocates the result and walks the tree twice, once to size the array
    /// exactly and once to fill it. Use <see cref="CopyWithin"/> on a hot path.
    /// </remarks>
    public SpatialPoint<TValue>[] GetWithin(double x, double y, double radius)
    {
        int count = CountWithin(x, y, radius);
        if (count == 0)
            return Array.Empty<SpatialPoint<TValue>>();

        var result = new SpatialPoint<TValue>[count];
        CopyWithin(x, y, radius, result);
        return result;
    }

    // ---- rectangle ----------------------------------------------------------------------------------

    /// <summary>
    /// Determines whether any point lies inside the closed box <c>[minX, maxX] &#215; [minY, maxY]</c>. Stops
    /// at the first match.
    /// </summary>
    /// <param name="minX">The inclusive left edge.</param>
    /// <param name="minY">The inclusive bottom edge.</param>
    /// <param name="maxX">The inclusive right edge. Must not precede <paramref name="minX"/>.</param>
    /// <param name="maxY">The inclusive top edge. Must not precede <paramref name="minY"/>.</param>
    /// <returns><c>true</c> if at least one point lies inside the box; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException">An upper edge precedes its lower edge.</exception>
    public bool ContainsInRectangle(double minX, double minY, double maxX, double maxY)
    {
        AnyVisitor visitor = default;
        Search(Rectangle(minX, minY, maxX, maxY), ref visitor);
        return visitor.Found;
    }

    /// <summary>Counts the points inside the closed box <c>[minX, maxX] &#215; [minY, maxY]</c>.</summary>
    /// <param name="minX">The inclusive left edge.</param>
    /// <param name="minY">The inclusive bottom edge.</param>
    /// <param name="maxX">The inclusive right edge. Must not precede <paramref name="minX"/>.</param>
    /// <param name="maxY">The inclusive top edge. Must not precede <paramref name="minY"/>.</param>
    /// <returns>The number of points inside the box.</returns>
    /// <exception cref="ArgumentException">An upper edge precedes its lower edge.</exception>
    public int CountInRectangle(double minX, double minY, double maxX, double maxY)
    {
        CountVisitor visitor = default;
        Search(Rectangle(minX, minY, maxX, maxY), ref visitor);
        return visitor.Count;
    }

    /// <summary>
    /// Writes the points inside the closed box <c>[minX, maxX] &#215; [minY, maxY]</c> into
    /// <paramref name="destination"/>, allocating nothing.
    /// </summary>
    /// <param name="minX">The inclusive left edge.</param>
    /// <param name="minY">The inclusive bottom edge.</param>
    /// <param name="maxX">The inclusive right edge. Must not precede <paramref name="minX"/>.</param>
    /// <param name="maxY">The inclusive top edge. Must not precede <paramref name="minY"/>.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <param name="destinationIndex">The position in <paramref name="destination"/> to start writing at.</param>
    /// <returns>The number of points written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="destinationIndex"/> is outside <c>[0, destination.Length]</c>.</exception>
    /// <exception cref="ArgumentException">An upper edge precedes its lower edge.</exception>
    /// <remarks>
    /// Writing stops when the buffer is full, so a return value equal to the remaining room may mean the
    /// matches were truncated. Size the buffer with <see cref="CountInRectangle"/> when every match is needed.
    /// Matches are reported in an unspecified order.
    /// </remarks>
    public int CopyInRectangle(double minX, double minY, double maxX, double maxY, SpatialPoint<TValue>[] destination, int destinationIndex = 0)
    {
        CopyVisitor visitor = CreateCopyVisitor(destination, destinationIndex);
        RectangleRegion region = Rectangle(minX, minY, maxX, maxY);
        if (destinationIndex == destination.Length)
            return 0;

        Search(region, ref visitor);
        return visitor.Written;
    }

    /// <summary>Returns the points inside the closed box <c>[minX, maxX] &#215; [minY, maxY]</c>.</summary>
    /// <param name="minX">The inclusive left edge.</param>
    /// <param name="minY">The inclusive bottom edge.</param>
    /// <param name="maxX">The inclusive right edge. Must not precede <paramref name="minX"/>.</param>
    /// <param name="maxY">The inclusive top edge. Must not precede <paramref name="minY"/>.</param>
    /// <returns>The matching points in an unspecified order, or an empty array when none match.</returns>
    /// <exception cref="ArgumentException">An upper edge precedes its lower edge.</exception>
    /// <remarks>
    /// This is the convenience tier: it allocates the result and walks the tree twice, once to size the array
    /// exactly and once to fill it. Use <see cref="CopyInRectangle"/> on a hot path.
    /// </remarks>
    public SpatialPoint<TValue>[] GetInRectangle(double minX, double minY, double maxX, double maxY)
    {
        int count = CountInRectangle(minX, minY, maxX, maxY);
        if (count == 0)
            return Array.Empty<SpatialPoint<TValue>>();

        var result = new SpatialPoint<TValue>[count];
        CopyInRectangle(minX, minY, maxX, maxY, result);
        return result;
    }

    /// <summary>Returns an enumerator over every stored point in the tree's layout order.</summary>
    /// <returns>A struct enumerator over the points.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<SpatialPoint<TValue>> IEnumerable<SpatialPoint<TValue>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    // Validates the input, permutes it into the implicit tree's layout, then fills the parallel arrays.
    private void Build(SpatialPoint<TValue>[] items, string paramName)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (!IsStorable(items[i].X) || !IsStorable(items[i].Y))
            {
                throw new ArgumentException(
                    "A point's coordinates must be finite and at most 1e153 in magnitude.", paramName);
            }
        }

        Partition(items, 0, items.Length, depth: 0);

        for (int i = 0; i < items.Length; i++)
        {
            _coords[i * 2] = items[i].X;
            _coords[(i * 2) + 1] = items[i].Y;
            _values[i] = items[i].Value;
        }
    }

    // Puts the median on this level's axis at the midpoint of [lo, hi) and recurses into the halves, which is
    // the whole of the build: the tree's shape is implied by the index arithmetic, so nothing else is stored.
    // Recursion depth is the tree height — at most 31 for any array length, so the call stack is bounded by
    // construction.
    private static void Partition(SpatialPoint<TValue>[] items, int lo, int hi, int depth)
    {
        if (hi - lo <= 1)
            return;

        int mid = lo + ((hi - lo) >> 1);
        bool byX = (depth & 1) == 0;

        SelectNth(items, lo, hi, mid, byX, DepthLimit(hi - lo));
        Partition(items, lo, mid, depth + 1);
        Partition(items, mid + 1, hi, depth + 1);
    }

    // Introselect. The quickselect leaves items[nth] holding the element that a full sort of [lo, hi) on this
    // axis would put there, everything before it at or below it on that axis and everything after at or above,
    // in O(hi - lo) expected — where sorting the range would be O(m log m). That is the difference between an
    // expected-O(n log n) build and an O(n log^2 n) one.
    //
    // The depth budget is what stops the expected case from being the only case. A middle-element pivot handles
    // the ordered and reverse-ordered inputs a caller is most likely to hand over — both settle in log m steps —
    // but it is emphatically not degeneracy-proof, and the input that defeats it is not exotic: an organ pipe
    // (ascending values interleaved with descending ones, the shape of any path that goes out and comes back)
    // puts an extreme at the midpoint of every subrange, so each pass peels off one element and a single
    // selection goes quadratic. Measured on the simulation: 4,096 points, 2,048 passes, against a budget of 24.
    //
    // So after 2*log2(m) passes the range is sorted outright and the answer read off it, which bounds one
    // selection at O(m log m) and the whole build at O(n log^2 n) — never quadratic. This mirrors
    // PartialSort.SelectCore in Celerity.Sorting, deliberately: it is the same guarantee and the same shape, so
    // there is one introselect idea in the library rather than two.
    private static void SelectNth(SpatialPoint<TValue>[] items, int lo, int hi, int nth, bool byX, int depthLimit)
    {
        int left = lo;
        int right = hi - 1;

        while (left < right)
        {
            if (depthLimit-- == 0)
            {
                Array.Sort(items, left, right - left + 1, byX ? AxisOrder.ByX : AxisOrder.ByY);
                return;
            }

            double pivot = Axis(items[left + ((right - left) >> 1)], byX);
            int i = left;
            int j = right;

            while (i <= j)
            {
                while (Axis(items[i], byX) < pivot)
                    i++;
                while (Axis(items[j], byX) > pivot)
                    j--;

                if (i <= j)
                {
                    (items[i], items[j]) = (items[j], items[i]);
                    i++;
                    j--;
                }
            }

            // The two scans stop on equal keys, so a run of duplicates lands astride the pivot rather than
            // driving the recursion into a one-element step — which is what keeps an all-identical point set
            // from being the quadratic case.
            if (nth <= j)
                right = j;
            else if (nth >= i)
                left = i;
            else
                return;
        }
    }

    // Every query compares *squared* distances, so a coordinate large enough for a squared separation to
    // overflow to Infinity would break the comparisons rather than merely lose precision: two far-apart points
    // would both measure Infinity, compare equal, and a radius that also squares to Infinity would report them
    // as matches. Bounding the stored magnitude removes that whole class of answer. The largest separation
    // between two stored points on one axis is 2 * MaxMagnitude, so the largest squared distance is
    // 8 * MaxMagnitude^2 = 8e306, comfortably inside double.MaxValue (~1.8e308).
    //
    // Non-finite coordinates fail the same test, which is the point: NaN has no position to order or measure,
    // and an infinity measures NaN against itself (Infinity - Infinity), so a stored infinite point could not
    // even be found by a query for its own coordinates.
    private const double MaxMagnitude = 1e153;

    private static bool IsStorable(double value) => Math.Abs(value) <= MaxMagnitude;

    private static double Axis(in SpatialPoint<TValue> point, bool byX) => byX ? point.X : point.Y;

    // Two passes per halving, the same budget PartialSort uses: generous enough that a well-behaved input never
    // reaches it, tight enough that a degenerate one gives up early.
    private static int DepthLimit(int length) => 2 * (31 - BitOperations.LeadingZeroCount((uint)length));

    // Only ever used by the depth-budget fallback, so an allocation-free struct comparer would buy nothing:
    // Array.Sort<T>(T[], int, int, IComparer<T>) takes the comparer through an interface-typed parameter, so
    // a struct would be boxed on the way in regardless, and the path runs at most once per selection.
    private sealed class AxisOrder : IComparer<SpatialPoint<TValue>>
    {
        internal static readonly AxisOrder ByX = new(byX: true);
        internal static readonly AxisOrder ByY = new(byX: false);

        private readonly bool _byX;

        private AxisOrder(bool byX) => _byX = byX;

        public int Compare(SpatialPoint<TValue> x, SpatialPoint<TValue> y) =>
            _byX ? x.X.CompareTo(y.X) : x.Y.CompareTo(y.Y);
    }

    private static double Square(double value) => value * value;

    private static void ValidateDistance(double value, string paramName)
    {
        if (double.IsNaN(value) || value < 0)
            throw new ArgumentOutOfRangeException(paramName, value, "The distance must not be negative or NaN.");
    }

    private static CircleRegion Circle(double x, double y, double radius)
    {
        ValidateDistance(radius, nameof(radius));
        return new CircleRegion(x, y, radius);
    }

    private static RectangleRegion Rectangle(double minX, double minY, double maxX, double maxY)
    {
        if (maxX < minX)
            throw new ArgumentException("The box's right edge must not precede its left edge.", nameof(maxX));
        if (maxY < minY)
            throw new ArgumentException("The box's top edge must not precede its bottom edge.", nameof(maxY));

        return new RectangleRegion(minX, minY, maxX, maxY);
    }

    private static void ValidateDestination(SpatialPoint<TValue>[] destination, int destinationIndex)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if ((uint)destinationIndex > (uint)destination.Length)
            throw new ArgumentOutOfRangeException(nameof(destinationIndex), destinationIndex,
                "Destination index must be in the range [0, destination.Length].");
    }

    private CopyVisitor CreateCopyVisitor(SpatialPoint<TValue>[] destination, int destinationIndex)
    {
        ValidateDestination(destination, destinationIndex);
        return new CopyVisitor(this, destination, destinationIndex);
    }

    private void Search<TRegion, TVisitor>(in TRegion region, ref TVisitor visitor)
        where TRegion : struct, IRegion
        where TVisitor : struct, IPointVisitor
    {
        if (_values.Length != 0)
            Walk(0, _values.Length, 0, in region, ref visitor);
    }

    // The single traversal behind every range query. The region supplies both the exact predicate and the
    // axis-aligned bounds the pruning needs, so the circle and the box share one walk rather than getting a
    // near-identical one each.
    //
    // Returns false once the visitor has asked to stop, which unwinds the whole recursion rather than only the
    // current level.
    private bool Walk<TRegion, TVisitor>(int lo, int hi, int depth, in TRegion region, ref TVisitor visitor)
        where TRegion : struct, IRegion
        where TVisitor : struct, IPointVisitor
    {
        int mid = lo + ((hi - lo) >> 1);
        int at = mid * 2;
        bool byX = (depth & 1) == 0;
        double split = byX ? _coords[at] : _coords[at + 1];

        // Everything left of this node is at or below the split on this axis, so a region that starts above it
        // cannot reach into that subtree; the mirror image bounds the right one. This is the whole of the
        // pruning, and it is why a selective query never touches most of the tree.
        if (lo < mid && (byX ? region.LowX : region.LowY) <= split &&
            !Walk(lo, mid, depth + 1, in region, ref visitor))
        {
            return false;
        }

        if (region.Contains(_coords[at], _coords[at + 1]) && !visitor.Visit(mid))
            return false;

        if (mid + 1 < hi && (byX ? region.HighX : region.HighY) >= split &&
            !Walk(mid + 1, hi, depth + 1, in region, ref visitor))
        {
            return false;
        }

        return true;
    }

    // Returns false when the query itself is unanswerable — an empty tree or a NaN coordinate, which has no
    // position to be near. The range walk needs no such guard: a NaN bound fails every comparison, so its
    // pruning already declines to descend anywhere.
    private bool SearchNearest<TVisitor>(double x, double y, ref TVisitor visitor)
        where TVisitor : struct, INearestVisitor
    {
        if (_values.Length == 0 || double.IsNaN(x) || double.IsNaN(y))
            return false;

        WalkNearest(0, _values.Length, 0, x, y, ref visitor);
        return true;
    }

    // Descends the side of the split the query falls on first, so the visitor's threshold is as tight as
    // possible by the time the other side is considered — which is what lets most of the far side be skipped
    // outright rather than merely tested.
    private void WalkNearest<TVisitor>(int lo, int hi, int depth, double x, double y, ref TVisitor visitor)
        where TVisitor : struct, INearestVisitor
    {
        int mid = lo + ((hi - lo) >> 1);
        int at = mid * 2;

        double dx = _coords[at] - x;
        double dy = _coords[at + 1] - y;
        visitor.Offer(mid, (dx * dx) + (dy * dy));

        bool byX = (depth & 1) == 0;
        double delta = byX ? -dx : -dy;

        int nearLo, nearHi, farLo, farHi;
        if (delta < 0)
        {
            nearLo = lo;
            nearHi = mid;
            farLo = mid + 1;
            farHi = hi;
        }
        else
        {
            nearLo = mid + 1;
            nearHi = hi;
            farLo = lo;
            farHi = mid;
        }

        if (nearLo < nearHi)
            WalkNearest(nearLo, nearHi, depth + 1, x, y, ref visitor);

        // The splitting plane is |delta| away, so nothing beyond it can beat a candidate already closer than
        // that. Comparing squares keeps the whole search off the square root. The comparison admits equality
        // because the distance bounds this type documents are inclusive: a point exactly on the threshold is a
        // result, and a strict test would prune the subtree holding it.
        if (farLo < farHi && (delta * delta) <= visitor.Threshold)
            WalkNearest(farLo, farHi, depth + 1, x, y, ref visitor);
    }

    private SpatialPoint<TValue> EntryAt(int index) => new(_coords[index * 2], _coords[(index * 2) + 1], _values[index]);

    // The query region, supplying the exact membership test and the axis-aligned bounds the walk prunes on. A
    // readonly struct used as a generic type argument is specialized by the JIT and inlined into the walk, so
    // the shared traversal costs nothing over two hand-written ones — the same rule the struct hashers,
    // IMonoid<T> and DefaultComparer<T> follow.
    private interface IRegion
    {
        double LowX { get; }

        double HighX { get; }

        double LowY { get; }

        double HighY { get; }

        bool Contains(double x, double y);
    }

    private readonly struct CircleRegion : IRegion
    {
        private readonly double _x;
        private readonly double _y;
        private readonly double _radiusSquared;

        internal CircleRegion(double x, double y, double radius)
        {
            _x = x;
            _y = y;
            _radiusSquared = radius * radius;
            LowX = x - radius;
            HighX = x + radius;
            LowY = y - radius;
            HighY = y + radius;
        }

        public double LowX { get; }

        public double HighX { get; }

        public double LowY { get; }

        public double HighY { get; }

        public bool Contains(double x, double y)
        {
            double dx = x - _x;
            double dy = y - _y;
            return (dx * dx) + (dy * dy) <= _radiusSquared;
        }
    }

    private readonly struct RectangleRegion : IRegion
    {
        internal RectangleRegion(double minX, double minY, double maxX, double maxY)
        {
            LowX = minX;
            LowY = minY;
            HighX = maxX;
            HighY = maxY;
        }

        public double LowX { get; }

        public double HighX { get; }

        public double LowY { get; }

        public double HighY { get; }

        // The bounds are the predicate for a box, so the walk's pruning and its membership test agree by
        // construction — a box query only ever visits nodes whose subtree the bounds already admitted.
        public bool Contains(double x, double y) => x >= LowX && x <= HighX && y >= LowY && y <= HighY;
    }

    // What each range query does with a match.
    private interface IPointVisitor
    {
        // Returns false to stop the walk.
        bool Visit(int index);
    }

    private struct AnyVisitor : IPointVisitor
    {
        public bool Found;

        public bool Visit(int index)
        {
            Found = true;
            return false;
        }
    }

    private struct CountVisitor : IPointVisitor
    {
        public int Count;

        public bool Visit(int index)
        {
            Count++;
            return true;
        }
    }

    private struct CopyVisitor : IPointVisitor
    {
        private readonly KdTree<TValue> _tree;
        private readonly SpatialPoint<TValue>[] _destination;
        private readonly int _start;
        private int _index;

        internal CopyVisitor(KdTree<TValue> tree, SpatialPoint<TValue>[] destination, int destinationIndex)
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

    // What each nearest query does with a candidate. Threshold is the squared distance beyond which nothing can
    // improve the answer, which is what the walk prunes the far side of a split on.
    private interface INearestVisitor
    {
        double Threshold { get; }

        void Offer(int index, double distanceSquared);
    }

    private struct NearestVisitor : INearestVisitor
    {
        public int Index;

        private double _best;

        // Seeded with the caller's distance bound rather than with infinity, so the bound prunes the search
        // instead of merely filtering its answer. An unbounded query passes infinity and the seeding is a no-op.
        internal NearestVisitor(double maxDistanceSquared)
        {
            Index = -1;
            _best = maxDistanceSquared;
        }

        public readonly double Threshold => _best;

        // The comparison admits equality so that the seeded bound is inclusive, which is what the distance
        // bound documents: a point exactly at the limit is a result. The cost is that the last of a set of tied
        // points wins rather than the first, and ties are unspecified either way. A NaN distance cannot reach
        // here — stored coordinates are validated at build and a NaN query never starts the walk.
        public void Offer(int index, double distanceSquared)
        {
            if (distanceSquared <= _best)
            {
                _best = distanceSquared;
                Index = index;
            }
        }
    }

    // A bounded max-heap over the caller's own buffer, keyed on distance from the query. Holding the heap in
    // the destination is what makes the k-nearest query allocation-free; the key is recomputed from each
    // entry's stored coordinates, which is five flops against the cache miss a parallel distance array would
    // cost. The root is the worst of the k kept so far, so it is both the entry to evict and the threshold the
    // walk prunes on.
    private struct NearestSetVisitor : INearestVisitor
    {
        private readonly KdTree<TValue> _tree;
        private readonly SpatialPoint<TValue>[] _destination;
        private readonly int _start;
        private readonly int _capacity;
        private readonly double _x;
        private readonly double _y;
        private int _count;

        internal NearestSetVisitor(KdTree<TValue> tree, SpatialPoint<TValue>[] destination, int destinationIndex, double x, double y)
        {
            _tree = tree;
            _destination = destination;
            _start = destinationIndex;
            _capacity = destination.Length - destinationIndex;
            _x = x;
            _y = y;
            _count = 0;
        }

        public readonly double Threshold => _count < _capacity ? double.PositiveInfinity : DistanceAt(0);

        public void Offer(int index, double distanceSquared)
        {
            if (_count < _capacity)
            {
                _destination[_start + _count] = _tree.EntryAt(index);
                SiftUp(_count);
                _count++;
                return;
            }

            if (distanceSquared < DistanceAt(0))
            {
                _destination[_start] = _tree.EntryAt(index);
                SiftDown(0, _count);
            }
        }

        // Heapsort in place: repeatedly move the worst remaining entry to the end of the filled region, which
        // leaves the buffer in ascending distance order without a second array or a comparison delegate.
        internal int SortAndCount()
        {
            for (int end = _count - 1; end > 0; end--)
            {
                Swap(0, end);
                SiftDown(0, end);
            }

            return _count;
        }

        private readonly double DistanceAt(int offset)
        {
            SpatialPoint<TValue> point = _destination[_start + offset];
            double dx = point.X - _x;
            double dy = point.Y - _y;
            return (dx * dx) + (dy * dy);
        }

        private readonly void Swap(int a, int b) =>
            (_destination[_start + a], _destination[_start + b]) = (_destination[_start + b], _destination[_start + a]);

        private readonly void SiftUp(int child)
        {
            while (child > 0)
            {
                int parent = (child - 1) >> 1;
                if (DistanceAt(child) <= DistanceAt(parent))
                    return;

                Swap(child, parent);
                child = parent;
            }
        }

        private readonly void SiftDown(int parent, int size)
        {
            while (true)
            {
                int left = (parent << 1) + 1;
                if (left >= size)
                    return;

                int worst = left;
                int right = left + 1;
                if (right < size && DistanceAt(right) > DistanceAt(left))
                    worst = right;

                if (DistanceAt(worst) <= DistanceAt(parent))
                    return;

                Swap(worst, parent);
                parent = worst;
            }
        }
    }

    /// <summary>A struct enumerator over a k-d tree's entries in the tree's layout order.</summary>
    /// <remarks>
    /// The tree is immutable, so there is no version to check and no concurrent-modification failure mode:
    /// an enumerator can never be invalidated.
    /// </remarks>
    public struct Enumerator : IEnumerator<SpatialPoint<TValue>>
    {
        private readonly KdTree<TValue> _tree;
        private int _index;
        private SpatialPoint<TValue> _current;

        internal Enumerator(KdTree<TValue> tree)
        {
            _tree = tree;
            _index = 0;
            _current = default;
        }

        /// <summary>Gets the point at the current position of the enumerator.</summary>
        public readonly SpatialPoint<TValue> Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next point.</summary>
        /// <returns><c>true</c> if there is a next point; otherwise <c>false</c>.</returns>
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

        /// <summary>Resets the enumerator to before the first point.</summary>
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
