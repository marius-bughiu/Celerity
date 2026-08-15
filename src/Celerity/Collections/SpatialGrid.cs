using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <b>mutable uniform-cell spatial index</b> over points in the plane: constant-time <see cref="Move"/> and
/// <see cref="Remove"/>, amortized constant-time <see cref="Add"/>, and radius, rectangle and nearest queries
/// that touch only the cells the query covers — the index for points that <i>move</i>, which
/// <see cref="KdTree{TValue}"/> is explicitly not.
/// </summary>
/// <typeparam name="TValue">The payload carried by each point.</typeparam>
/// <remarks>
/// <para>
/// .NET ships no spatial index of any kind, and <see cref="KdTree{TValue}"/> — the one this library added for
/// that gap — is build-once, so a population that moves every tick has to rebuild it every tick and pays more
/// for the index than the queries save. That is the commonest spatial workload rather than an edge case: game
/// entities and projectiles, drivers and couriers on a map, cursors and drag targets, particles in a
/// simulation, agents in a model. All of them move every frame, and all of them ask <i>what is near me</i>
/// every frame.
/// </para>
/// <para>
/// <b>The baseline is not bad, which is the point.</b> What a caller writes instead is a bucketed grid:
/// a <c>Dictionary&lt;(int, int), List&lt;T&gt;&gt;</c> keyed by cell, inserted into by dividing coordinates
/// by a cell size and queried by walking the cells a radius covers. That is a genuinely reasonable structure
/// and it is the one this type is measured against. What it costs is a tuple hash and a bucket probe per cell
/// touched, a <c>List&lt;T&gt;</c> object per occupied cell, and a pointer chase into it. This type keeps the
/// same idea and pays none of that — with one honest qualification the benchmark insists on: in steady state
/// neither side allocates, because a cell that empties keeps its list, so the difference is resident memory
/// and per-cell work rather than garbage.
/// </para>
/// <para>
/// <b>Layout.</b> A populated grid is three arrays and no per-cell or per-entry object. One holds the cells'
/// list heads; one holds fixed-size entry records — coordinates, owning cell, and the two links that thread
/// the entry through its cell's intrusive doubly-linked list; and one holds the payloads, kept separate
/// precisely so the cell walk never touches them and a query reads coordinates and links without dragging
/// <typeparamref name="TValue"/> through the cache. The double links are what make <see cref="Remove"/> and a cell-changing <see cref="Move"/>
/// unlink in constant time instead of scanning the cell to find the predecessor.
/// </para>
/// <para>
/// <b><see cref="Move"/> is the whole point</b> and is addressed by a <see cref="SpatialGridHandle"/> rather
/// than by value, the way <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}"/> makes decrease-key
/// addressable: <see cref="Add"/> hands back a handle, and moving through it is a coordinate write plus — only
/// when the point crossed a cell boundary — an unlink and a relink. There is no search, no hash of the
/// coordinates, and no comparison of <typeparamref name="TValue"/>, so the type needs no equality contract on
/// its payload and holds duplicates happily.
/// </para>
/// <para>
/// <b>Bounded extent, by choice.</b> The world rectangle and the cell size are declared up front, which buys a
/// dense cell array and index arithmetic in place of hashing a cell key. A point outside the declared world is
/// <i>clamped</i> into the nearest edge cell rather than rejected — queries stay exactly correct, because the
/// clamp is monotone and so is applied identically to a query's own cell range, but a population that drifts
/// outside piles onto the edge cells and degrades to a scan of them. Declare the world you actually have.
/// </para>
/// <para>
/// <b>The two failure modes, stated rather than discovered.</b> A uniform grid is the right structure for
/// <i>evenly spread</i> moving objects and the wrong one for heavily clustered ones:
/// </para>
/// <para>
/// 1. <b>Non-uniform density.</b> If most of the population lands in one cell, every query in that
/// neighbourhood degenerates to a scan of that cell.
/// </para>
/// <para>
/// That is worse than "it degrades to a scan", and the benchmark says so rather than rounding it to a caveat:
/// on clustered data this type is measured <b>about twice as slow</b> as the very hand-roll it replaces.
/// Inside a cell the two layouts genuinely differ — the hand-roll walks a contiguous <c>List&lt;T&gt;</c> and
/// issues independent loads per candidate, while this type walks an intrusive linked list, so each step is a
/// load that must complete before the next address is known. A cell holding two entries never shows that; a
/// cell holding five hundred is a five-hundred-long dependency chain.
/// </para>
/// <para>
/// <b>And there is no consolation elsewhere in this library, which was worth measuring rather than assuming.</b>
/// An earlier draft of these docs sent clustered points to <see cref="KdTree{TValue}"/>, reasoning that its
/// pruning adapts to density where a fixed cell size cannot. The measurement does not support it: on clustered
/// moving points a per-frame <see cref="KdTree{TValue}"/> rebuild is <i>level</i> with this type, both about
/// twice the hand-roll's time, and <c>KdTreeShapeBenchmark</c> separately finds the tree losing to <i>its</i>
/// own hand-roll under clustering. So the honest advice for heavily clustered points that move is the
/// unflattering one: the bucketed <c>Dictionary</c> of contiguous lists wins, and neither type here helps.
/// </para>
/// <para>
/// 2. <b>A query radius much larger than the cell.</b> The number of cells a query touches grows with the
/// square of <c>radius / cellSize</c>, so a wide query over a fine grid is worse than a scan. Cell size is the
/// knob that trades these two against each other and it is a constructor parameter for that reason: roughly
/// one that puts a handful of points in the average cell and is no smaller than the typical query radius.
/// </para>
/// <para>
/// <b>The margin is a property of how full the cells are, not of the type.</b> Both this type and the
/// hand-roll walk the same cells and run the same distance test on the same candidates; everything gained here
/// is <i>per cell</i>, so the ratio is per-cell overhead against per-candidate work and it thins as the cells
/// fill. On the frame workload above at 100,000 entities it is <b>5.0x</b> with about one point per cell and
/// two matches per query — the broadphase shape — and <b>1.12x</b> at ten points per cell and twenty-five
/// matches, with the cell size still tuned to the radius. Rebuilding a <see cref="KdTree{TValue}"/> every
/// frame instead measures <b>13.3x</b>. The frame figure is from CI's same-runner A/B; the shape sweep rides
/// the extended suite and is a development-machine measurement. Both are in <c>SpatialGridBenchmark</c> and
/// <c>SpatialGridShapeBenchmark</c>, and tabulated in the README.
/// </para>
/// <para>
/// <b>What the bound really is.</b> <see cref="Move"/>, <see cref="Remove"/> and <see cref="TryGetPoint"/>
/// are <c>O(1)</c> outright. <see cref="Add"/> is <c>O(1)</c> <i>amortized</i>: the one call in a growth cycle
/// that finds the free list empty and the entry array full resizes and copies both backing arrays, which is
/// <c>O(n)</c> for that call. A range
/// query is <c>O(cells touched + points in them)</c>, which is a statement about density rather than about
/// <see cref="Count"/>. That is an asymptotic statement and nothing more: the clustered measurement above is
/// the reminder that a matching bound does not mean matching time, since the intrusive list adds a dependent
/// load per candidate that a contiguous bucket does not. The nearest
/// query expands square rings outward and stops once the next ring cannot beat what it has, so on a populated
/// grid it settles in a handful of rings — but on a sparse one it can walk out to the world's edge, which is
/// <c>O(cells)</c>. Pass the <c>maxDistance</c> overload when there is a distance beyond which the answer does
/// not interest you; it bounds the rings rather than merely filtering the result.
/// </para>
/// <para>
/// <b>Two query tiers</b>, as <see cref="KdTree{TValue}"/> has: the <c>Contains</c>, <c>Count</c> and
/// <c>Copy</c> members and <see cref="TryFindNearest(double, double, out SpatialPoint{TValue})"/> allocate
/// nothing at all, and <see cref="GetWithin"/> / <see cref="GetInRectangle"/> are the convenience tier that
/// allocates the result. Every range query shares one cell walk that is generic over both a <c>struct</c>
/// region and a <c>struct</c> visitor, so the JIT specializes it per call site and inlines the per-match work
/// instead of paying a delegate or an interface call per hit.
/// </para>
/// <para>
/// <b>k-nearest is deliberately absent.</b> Bounding a ring search by the <i>k</i>-th best rather than the
/// best turns it into a heap walk whose ring bound is much weaker, and the workload this type exists for —
/// one proximity query per moving entity per frame — is a radius query, not a k-nearest one. Use
/// <see cref="CopyWithin"/>, or <see cref="KdTree{TValue}"/> when the point set is static enough to build.
/// </para>
/// <para>
/// <b>Capacity only grows.</b> There is no <c>TrimExcess</c>: a handle <i>is</i> a position in the entry
/// array, so compacting the array would invalidate every handle a caller is holding, which is the one thing
/// this type promises not to do. <see cref="Clear"/> retires all outstanding handles and returns the storage
/// to the free list for reuse, but keeps it.
/// </para>
/// <para>
/// A <i>stored</i> coordinate must be finite and at most <c>1e153</c> in magnitude — the same domain
/// <see cref="SpatialPoint{TValue}"/> documents and for the same reason: every query compares <i>squared</i>
/// distances, and beyond that bound a squared separation overflows and two far-apart points stop comparing as
/// far apart. <see cref="Add"/> and <see cref="Move"/> reject anything outside it.
/// </para>
/// <para>
/// The same arithmetic puts a <i>floor</i> under the domain, documented rather than enforced because no check
/// could see it, and identical to the one <see cref="KdTree{TValue}"/> carries: a separation below roughly
/// <c>1e-162</c> squares below the smallest subnormal and underflows to zero, so two points that close are
/// indistinguishable from coincident ones <i>by the distance test</i>, and a nearest query may order them
/// arbitrarily. Whether a query even reaches the other point is a second question with no promise attached:
/// the cell range is derived from the same coordinates, so two points astride a cell boundary at that
/// separation land in different cells and a zero-radius query visits only one of them. Below this scale the
/// type says nothing either way. Points that genuinely coincide are unaffected and well defined; it is the
/// nonzero-but-tinier-than-<c>1e-162</c> separation that cannot be represented. Comparing squared distances is
/// what keeps every query off the square root, and paying a scaled comparison on the hot path to resolve
/// separations no coordinate system produces is not a trade this type makes either.
/// </para>
/// <para>
/// <b>Query coordinates are not range-checked</b>, exactly as on <see cref="KdTree{TValue}"/>: that would be a
/// per-query cost for a case no real coordinate system reaches. Passing one beyond the same magnitude does not
/// throw — it yields distances this type cannot order, so the answer is meaningless rather than merely
/// imprecise. The one query coordinate that <i>is</i> special-cased is <see cref="double.NaN"/>, which has no
/// position and matches nothing: the nearest queries report no result and the range queries an empty one.
/// </para>
/// <para>
/// Enumeration yields every live entry in an unspecified order and is invalidated by <see cref="Add"/>,
/// <see cref="Remove"/> and a <see cref="Clear"/> that removes something. <see cref="Move"/> deliberately does
/// <i>not</i> invalidate it: moving neither adds nor removes an entry nor changes which slot holds it, so the
/// sequence an enumerator is walking is unaffected — an entry not yet reached is simply reported at its new
/// position.
/// </para>
/// </remarks>
public sealed class SpatialGrid<TValue> : IReadOnlyCollection<SpatialPoint<TValue>>
{
    // Coordinates, links and owning cell in one 32-byte record rather than in five parallel arrays: a cell
    // walk reads Next, X and Y of the same entry together, so splitting them by field would fetch three lines
    // to answer one question. The payload is deliberately *not* among them — a walk reads it only for the
    // handful of entries it actually returns.
    private struct Entry
    {
        public double X;
        public double Y;

        // The owning cell, or -1 when the slot is free. That doubles as the liveness flag, so no separate
        // occupancy bit is needed.
        public int Cell;

        // Next entry in the owning cell's list, or the next free slot when Cell is -1.
        public int Next;

        public int Prev;

        // Stepped every time the slot is vacated, which is what retires the handles that pointed at it. A live
        // slot's version is always at least 1, so the default handle can never resolve.
        public uint Version;
    }

    private readonly double _minX;
    private readonly double _minY;
    private readonly double _cellSize;
    private readonly double _inverseCellSize;
    private readonly int _columns;
    private readonly int _rows;
    private readonly int[] _cellHeads;

    private Entry[] _entries;
    private TValue?[] _values;

    // The high-water mark of allocated slots. Slots below it are either live or on the free list; slots above
    // it have never been used, which is what lets a fresh one start at version 1.
    private int _slotCount;
    private int _freeHead;
    private int _count;
    private int _version;

    /// <summary>Creates an empty grid over the world rectangle <c>[minX, maxX] &#215; [minY, maxY]</c>.</summary>
    /// <param name="minX">The world's left edge.</param>
    /// <param name="minY">The world's bottom edge.</param>
    /// <param name="maxX">The world's right edge. Must not precede <paramref name="minX"/>.</param>
    /// <param name="maxY">The world's top edge. Must not precede <paramref name="minY"/>.</param>
    /// <param name="cellSize">
    /// The side of one square cell, in world units. This is the tuning knob: roughly one that puts a handful of
    /// points in the average cell, and no smaller than the typical query radius.
    /// </param>
    /// <param name="capacity">How many entries to make room for up front. Storage grows as needed.</param>
    /// <exception cref="ArgumentException">An upper edge precedes its lower edge.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An edge is not finite or exceeds <c>1e153</c> in magnitude, <paramref name="cellSize"/> is not a
    /// positive finite number, <paramref name="capacity"/> is negative, or the world and the cell size together
    /// call for more cells than an array can hold.
    /// </exception>
    /// <remarks>
    /// The world is always at least one cell across on each axis, so a degenerate rectangle — a point, or a
    /// line — is a legal one-cell or one-row grid rather than an error.
    /// </remarks>
    public SpatialGrid(double minX, double minY, double maxX, double maxY, double cellSize, int capacity = 0)
    {
        ValidateEdge(minX, nameof(minX));
        ValidateEdge(minY, nameof(minY));
        ValidateEdge(maxX, nameof(maxX));
        ValidateEdge(maxY, nameof(maxY));

        if (maxX < minX)
            throw new ArgumentException("The world's right edge must not precede its left edge.", nameof(maxX));
        if (maxY < minY)
            throw new ArgumentException("The world's top edge must not precede its bottom edge.", nameof(maxY));

        if (double.IsNaN(cellSize) || cellSize <= 0 || double.IsInfinity(cellSize))
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "The cell size must be a positive, finite number.");

        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must not be negative.");

        _minX = minX;
        _minY = minY;
        MaxX = maxX;
        MaxY = maxY;
        _cellSize = cellSize;
        _inverseCellSize = 1.0 / cellSize;

        _columns = AxisCells(maxX - minX, cellSize, nameof(cellSize));
        _rows = AxisCells(maxY - minY, cellSize, nameof(cellSize));

        // Counted in double first: the product of two in-range int counts still overflows an int, and the
        // caller who asked for a metre-wide cell over a planet-sized world deserves the reason rather than a
        // negative length.
        double cells = (double)_columns * _rows;
        if (cells > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize,
                "The world rectangle and cell size together call for more cells than an array can hold. Use a larger cell size.");
        }

        _cellHeads = new int[_columns * _rows];
        _cellHeads.AsSpan().Fill(-1);

        _entries = capacity == 0 ? Array.Empty<Entry>() : new Entry[capacity];
        _values = capacity == 0 ? Array.Empty<TValue?>() : new TValue?[capacity];
        _freeHead = -1;
    }

    /// <summary>Gets the number of points currently in the grid.</summary>
    public int Count => _count;

    /// <summary>Gets the side of one cell, in world units, as passed to the constructor.</summary>
    public double CellSize => _cellSize;

    /// <summary>Gets the number of cell columns spanning the world's width. Always at least one.</summary>
    public int Columns => _columns;

    /// <summary>Gets the number of cell rows spanning the world's height. Always at least one.</summary>
    public int Rows => _rows;

    /// <summary>Gets the world's left edge, as passed to the constructor.</summary>
    public double MinX => _minX;

    /// <summary>Gets the world's bottom edge, as passed to the constructor.</summary>
    public double MinY => _minY;

    /// <summary>Gets the world's right edge, as passed to the constructor.</summary>
    public double MaxX { get; }

    /// <summary>Gets the world's top edge, as passed to the constructor.</summary>
    public double MaxY { get; }

    // ---- mutation -----------------------------------------------------------------------------------

    /// <summary>Adds a point at <c>(x, y)</c> and returns the handle that addresses it.</summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <param name="value">The payload to carry. May be <c>null</c> for a reference type.</param>
    /// <returns>A handle that stays valid until the entry is removed or the grid is cleared.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A coordinate is not finite, or exceeds <c>1e153</c> in magnitude.
    /// </exception>
    /// <remarks>
    /// A point outside the declared world is stored in the nearest edge cell rather than rejected. Duplicate
    /// coordinates are kept distinct: two points at the same place are two entries with two handles, and every
    /// query reports both.
    /// </remarks>
    public SpatialGridHandle Add(double x, double y, TValue? value)
    {
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));

        int slot = AllocateSlot();
        _entries[slot].X = x;
        _entries[slot].Y = y;
        _values[slot] = value;
        Link(slot, CellOf(x, y));

        _count++;
        _version++;
        return new SpatialGridHandle(slot, _entries[slot].Version);
    }

    /// <summary>Moves the entry addressed by <paramref name="handle"/> to <c>(x, y)</c> in constant time.</summary>
    /// <param name="handle">The handle returned when the entry was added.</param>
    /// <param name="x">The new horizontal coordinate.</param>
    /// <param name="y">The new vertical coordinate.</param>
    /// <exception cref="ArgumentException"><paramref name="handle"/> does not address a live entry of this grid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A coordinate is not finite, or exceeds <c>1e153</c> in magnitude.
    /// </exception>
    /// <remarks>
    /// A move within one cell writes two coordinates and nothing else; a move that crosses a cell boundary also
    /// unlinks and relinks the entry, which is a handful of array writes. Neither invalidates an enumerator:
    /// the set of entries and the slot each occupies are unchanged, so an entry not yet reached is simply
    /// reported at its new position.
    /// </remarks>
    public void Move(SpatialGridHandle handle, double x, double y)
    {
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));

        int slot = ResolveLive(handle);
        int cell = CellOf(x, y);
        if (cell != _entries[slot].Cell)
        {
            Unlink(slot);
            Link(slot, cell);
        }

        _entries[slot].X = x;
        _entries[slot].Y = y;
    }

    /// <summary>Removes the entry addressed by <paramref name="handle"/> in constant time.</summary>
    /// <param name="handle">The handle returned when the entry was added.</param>
    /// <exception cref="ArgumentException"><paramref name="handle"/> does not address a live entry of this grid.</exception>
    /// <remarks>The handle is retired: passing it again throws rather than addressing whatever reused the slot.</remarks>
    public void Remove(SpatialGridHandle handle)
    {
        int slot = ResolveLive(handle);
        Unlink(slot);
        Vacate(slot);

        _count--;
        _version++;
    }

    /// <summary>Reads back the point addressed by <paramref name="handle"/>.</summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <param name="point">Receives the point, or <c>default</c> when the handle is not live.</param>
    /// <returns><c>true</c> when the handle addresses a live entry; otherwise <c>false</c>.</returns>
    /// <remarks>This is also the way to ask whether a handle is still live, without risking an exception.</remarks>
    public bool TryGetPoint(SpatialGridHandle handle, out SpatialPoint<TValue> point)
    {
        if (!TryResolve(handle, out int slot))
        {
            point = default;
            return false;
        }

        point = new SpatialPoint<TValue>(_entries[slot].X, _entries[slot].Y, _values[slot]);
        return true;
    }

    /// <summary>Removes every entry, retiring every outstanding handle.</summary>
    /// <remarks>
    /// The storage is kept and returned to the free list, so refilling the grid allocates nothing. Clearing an
    /// already-empty grid changes nothing and does not invalidate enumerators.
    /// </remarks>
    public void Clear()
    {
        if (_count == 0)
            return;

        _cellHeads.AsSpan().Fill(-1);

        // Every slot goes back on the free list with its version stepped, which is what retires the handles.
        // Rebuilding the list rather than resetting _slotCount is deliberate: a slot reissued from scratch
        // would start at version 1 again and could collide with a handle the caller is still holding.
        for (int i = 0; i < _slotCount; i++)
        {
            // Only a *live* slot needs its version stepped. Vacate already retired the free ones, and stepping
            // them again would churn a vacated slot's version on every unrelated Clear — walking it back round
            // to a value some long-retired handle still holds after far fewer operations than that slot's own
            // vacations would take. The version a handle is checked against must move only when its own slot
            // is vacated, or the documented period is not the real one.
            if (_entries[i].Cell >= 0)
            {
                _entries[i].Cell = -1;
                _entries[i].Version = NextVersion(_entries[i].Version);
            }

            _entries[i].Next = i - 1;
        }

        _freeHead = _slotCount - 1;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
            Array.Clear(_values, 0, _slotCount);

        _count = 0;
        _version++;
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
    /// This is the convenience tier: it allocates the result and walks the cells twice, once to size the array
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
    /// This is the convenience tier: it allocates the result and walks the cells twice, once to size the array
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

    // ---- nearest ------------------------------------------------------------------------------------

    /// <summary>Finds the point closest to <c>(x, y)</c> by Euclidean distance.</summary>
    /// <param name="x">The horizontal coordinate to search from.</param>
    /// <param name="y">The vertical coordinate to search from.</param>
    /// <param name="nearest">Receives the closest point, or <c>default</c> when there is none.</param>
    /// <returns><c>true</c> if a point was found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Returns <c>false</c> only for an empty grid or a <see cref="double.NaN"/> query coordinate. Points at
    /// equal distance are tied and which one is reported is unspecified. On a sparse grid this can walk out to
    /// the world's edge — prefer the <c>maxDistance</c> overload when there is a distance beyond which the
    /// answer does not interest you.
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
    /// The bound is not only a filter: it caps how far the ring search expands, so a tight bound makes the
    /// query materially cheaper than an unbounded one followed by a distance test.
    /// </remarks>
    public bool TryFindNearest(double x, double y, double maxDistance, out SpatialPoint<TValue> nearest)
    {
        ValidateDistance(maxDistance, nameof(maxDistance));

        nearest = default;
        if (_count == 0 || double.IsNaN(x) || double.IsNaN(y))
            return false;

        // Seeded with the caller's bound rather than with infinity, so the bound stops the ring expansion
        // instead of merely filtering its answer. An unbounded query squares infinity to infinity and the
        // seeding is a no-op.
        double best = maxDistance * maxDistance;
        int bestSlot = -1;

        int centreColumn = ColumnOf(x);
        int centreRow = RowOf(y);
        int maxRing = Math.Max(
            Math.Max(centreColumn, _columns - 1 - centreColumn),
            Math.Max(centreRow, _rows - 1 - centreRow));

        for (int ring = 0; ring <= maxRing; ring++)
        {
            // The query point sits somewhere inside its own cell, so a cell at Chebyshev ring r is at least
            // (r - 1) cells away from it — one cell of slack for where in its cell the query actually is.
            // Once that floor beats the best distance so far, no further ring can improve the answer. (When
            // the query is outside the world its cell is clamped, which only makes the floor an
            // underestimate — more rings walked, never a missed point.)
            double ringFloor = (ring - 1) * _cellSize;
            if (ringFloor > 0 && ringFloor * ringFloor > best)
                break;

            VisitRing(ring, centreColumn, centreRow, x, y, ref best, ref bestSlot);
        }

        if (bestSlot < 0)
            return false;

        nearest = new SpatialPoint<TValue>(_entries[bestSlot].X, _entries[bestSlot].Y, _values[bestSlot]);
        return true;
    }

    /// <summary>Returns an enumerator over every live entry, in an unspecified order.</summary>
    /// <returns>A struct enumerator over the points.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<SpatialPoint<TValue>> IEnumerable<SpatialPoint<TValue>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    // The same domain SpatialPoint documents and KdTree enforces: past this magnitude a squared separation
    // overflows to infinity, and two far-apart points would compare equal rather than merely lose precision.
    private const double MaxMagnitude = 1e153;

    private static void ValidateEdge(double value, string paramName)
    {
        if (!(Math.Abs(value) <= MaxMagnitude))
            throw new ArgumentOutOfRangeException(paramName, value, "The world's edges must be finite and at most 1e153 in magnitude.");
    }

    private static void ValidateCoordinate(double value, string paramName)
    {
        if (!(Math.Abs(value) <= MaxMagnitude))
            throw new ArgumentOutOfRangeException(paramName, value, "A coordinate must be finite and at most 1e153 in magnitude.");
    }

    private static void ValidateDistance(double value, string paramName)
    {
        if (double.IsNaN(value) || value < 0)
            throw new ArgumentOutOfRangeException(paramName, value, "The distance must not be negative or NaN.");
    }

    // How many cells one axis needs. Counted in double throughout: an extent of 1e153 over a cell size of 1
    // is a legitimate pair of arguments whose quotient no integer type holds, and the caller is owed the
    // "too many cells" message rather than a wrapped count.
    private static int AxisCells(double extent, double cellSize, string paramName)
    {
        // Array.MaxLength rather than a hand-copied literal: it is the runtime's own ceiling, it is what
        // IndexedPriorityQueue.ClampGrowth compares against, and hardcoding it invites exactly the argument
        // about which of the several historical limits applies to an int[].
        double cells = Math.Ceiling(extent / cellSize);
        if (cells > Array.MaxLength)
            throw new ArgumentOutOfRangeException(paramName, cellSize, "The world rectangle and cell size together call for more cells than an array can hold. Use a larger cell size.");

        // A degenerate extent — a point or a line — is one cell across rather than none, so a grid always has
        // somewhere to put a point.
        return cells < 1 ? 1 : (int)cells;
    }

    private int ColumnOf(double x) => AxisIndex(x - _minX, _columns);

    private int RowOf(double y) => AxisIndex(y - _minY, _rows);

    // Clamped on both sides, which is what makes an out-of-world point storable and — because the clamp is
    // monotone and a query's own cell range is clamped the same way — keeps every query exactly correct.
    //
    // Every comparison is made in double before the cast, so no out-of-range conversion is ever performed. The
    // order also decides what a NaN does: it fails both tests and falls through to cell zero, rather than
    // reaching the cast and relying on how a NaN converts. Nothing stored can be NaN and no query with one
    // starts a walk, but the multiply itself can produce one — 0 times an infinite reciprocal, which a cell
    // size small enough to invert to infinity gives on a degenerate world — and an arithmetic accident is a
    // poor reason to depend on conversion semantics.
    private int AxisIndex(double offset, int limit)
    {
        double index = offset * _inverseCellSize;
        if (index >= limit)
            return limit - 1;
        if (index > 0)
            return (int)index;

        return 0;
    }

    private int CellOf(double x, double y) => (RowOf(y) * _columns) + ColumnOf(x);

    private int AllocateSlot()
    {
        if (_freeHead >= 0)
        {
            int reused = _freeHead;
            _freeHead = _entries[reused].Next;
            return reused;
        }

        if (_slotCount == _entries.Length)
            Grow();

        int slot = _slotCount++;

        // A slot that has never been used starts at version 1, so that the default handle — index 0,
        // version 0 — cannot resolve to the first entry ever added.
        _entries[slot].Version = 1;
        return slot;
    }

    private void Grow()
    {
        // Double the capacity, but clamp to Array.MaxLength and guard the int overflow a naive `* 2` would hit
        // once the entry array passes ~1 billion slots (the doubled value wraps negative), the same shape
        // IndexedPriorityQueue.Grow uses.
        int current = _entries.Length;
        int capacity = ClampGrowth(current == 0 ? 4 : current * 2, current);

        Array.Resize(ref _entries, capacity);
        Array.Resize(ref _values, capacity);
    }

    // The growth ceiling, split out because neither arm is reachable from the public API. Grow() has exactly
    // one caller — AllocateSlot(), and only when the free list is empty and every slot is live — so the clamp
    // needs 2^30 simultaneously live entries. Each is a 32-byte record, which puts _entries past the 2 GiB
    // single-object array limit long before the count gets there, at any available memory. The throw needs the
    // array already at Array.MaxLength, which is harder still. Both are kept because they are the only thing
    // between a saturated grid and a silently negative capacity.
    [ExcludeFromCodeCoverage(Justification = "Unreachable: needs 2^30 live entries of 32 bytes each, which " +
        "exceeds the 2 GiB single-object array limit regardless of available memory.")]
    private static int ClampGrowth(int capacity, int current)
    {
        if ((uint)capacity > (uint)Array.MaxLength)
            capacity = Array.MaxLength;
        if (capacity <= current)
            throw new InvalidOperationException("The spatial grid has reached its maximum capacity.");

        return capacity;
    }

    private void Link(int slot, int cell)
    {
        int head = _cellHeads[cell];
        _entries[slot].Cell = cell;
        _entries[slot].Prev = -1;
        _entries[slot].Next = head;

        if (head >= 0)
            _entries[head].Prev = slot;

        _cellHeads[cell] = slot;
    }

    private void Unlink(int slot)
    {
        int previous = _entries[slot].Prev;
        int next = _entries[slot].Next;

        if (previous >= 0)
            _entries[previous].Next = next;
        else
            _cellHeads[_entries[slot].Cell] = next;

        if (next >= 0)
            _entries[next].Prev = previous;
    }

    // Steps a vacated slot's version, cycling through [1, uint.MaxValue] and never through 0. A plain
    // increment would eventually wrap to 0 — 2^32 vacations of one slot, which a tight add/clear loop reaches
    // in minutes, not geological time — and a slot sitting at version 0 would be addressable by the `default`
    // handle, which this type documents as always rejected. The modulo keeps that guarantee absolute without
    // a branch the coverage gate could never see taken.
    //
    // What it does not fix, because no fixed-width version can: the versions cycle through [1, uint.MaxValue],
    // so after 4,294,967,295 vacations of the *same* slot they repeat and a handle retired exactly that long
    // ago starts matching again. That is the standing limitation of every generational slot map, and it is
    // documented on SpatialGridHandle rather than papered over.
    private static uint NextVersion(uint version) => (version % uint.MaxValue) + 1;

    private void Vacate(int slot)
    {
        _entries[slot].Cell = -1;
        _entries[slot].Version = NextVersion(_entries[slot].Version);
        _entries[slot].Next = _freeHead;
        _freeHead = slot;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
            _values[slot] = default;
    }

    private bool TryResolve(SpatialGridHandle handle, out int slot)
    {
        slot = handle.Index;
        return (uint)slot < (uint)_slotCount
            && _entries[slot].Cell >= 0
            && _entries[slot].Version == handle.Version;
    }

    private int ResolveLive(SpatialGridHandle handle)
    {
        if (!TryResolve(handle, out int slot))
        {
            throw new ArgumentException(
                "The handle does not address a live entry of this grid. It was removed, the grid was cleared, or it belongs to another grid.",
                nameof(handle));
        }

        return slot;
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

    // The single cell walk behind every range query. The region supplies both the exact predicate and the
    // axis-aligned bounds that decide which cells to visit at all, so the circle and the box share one walk
    // rather than getting a near-identical one each.
    private void Search<TRegion, TVisitor>(in TRegion region, ref TVisitor visitor)
        where TRegion : struct, IRegion
        where TVisitor : struct, IPointVisitor
    {
        if (_count == 0 || region.IsEmpty)
            return;

        int firstColumn = ColumnOf(region.LowX);
        int lastColumn = ColumnOf(region.HighX);
        int firstRow = RowOf(region.LowY);
        int lastRow = RowOf(region.HighY);

        for (int row = firstRow; row <= lastRow; row++)
        {
            int rowBase = row * _columns;
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                for (int slot = _cellHeads[rowBase + column]; slot >= 0; slot = _entries[slot].Next)
                {
                    if (region.Contains(_entries[slot].X, _entries[slot].Y) && !visitor.Visit(slot))
                        return;
                }
            }
        }
    }

    // The square annulus at Chebyshev distance `ring` from the query's own cell: the top and bottom rows in
    // full, then the left and right columns of what is left between them. At ring 0 the row pair collapses to
    // one row and the column range is empty, so the same code covers the centre cell without a special case.
    private void VisitRing(int ring, int centreColumn, int centreRow, double x, double y, ref double best, ref int bestSlot)
    {
        // The ring's edges are computed in long deliberately. A grid may legally be nearly Array.MaxLength
        // cells across on one axis, and an unbounded nearest search from a cell in the far half of it reaches
        // rings as wide as the grid — at which point `centre + ring` overflows int and wraps *negative*, sails
        // through the `< _columns` guard below, and indexes _cellHeads at a negative cell. Widening costs
        // nothing next to the cell walk that follows, and every narrowing cast back is bounded by the guard
        // that precedes it.
        long left = (long)centreColumn - ring;
        long right = (long)centreColumn + ring;
        long top = (long)centreRow - ring;
        long bottom = (long)centreRow + ring;

        int firstColumn = (int)Math.Max(left, 0);
        int lastColumn = (int)Math.Min(right, _columns - 1L);

        if (top >= 0)
            VisitRow((int)top, firstColumn, lastColumn, x, y, ref best, ref bestSlot);
        if (bottom != top && bottom < _rows)
            VisitRow((int)bottom, firstColumn, lastColumn, x, y, ref best, ref bestSlot);

        int firstRow = (int)Math.Max(top + 1, 0);
        int lastRow = (int)Math.Min(bottom - 1, _rows - 1L);

        if (left >= 0)
            VisitColumn((int)left, firstRow, lastRow, x, y, ref best, ref bestSlot);
        if (right != left && right < _columns)
            VisitColumn((int)right, firstRow, lastRow, x, y, ref best, ref bestSlot);
    }

    private void VisitRow(int row, int firstColumn, int lastColumn, double x, double y, ref double best, ref int bestSlot)
    {
        int rowBase = row * _columns;
        for (int column = firstColumn; column <= lastColumn; column++)
            VisitCell(rowBase + column, x, y, ref best, ref bestSlot);
    }

    private void VisitColumn(int column, int firstRow, int lastRow, double x, double y, ref double best, ref int bestSlot)
    {
        for (int row = firstRow; row <= lastRow; row++)
            VisitCell((row * _columns) + column, x, y, ref best, ref bestSlot);
    }

    private void VisitCell(int cell, double x, double y, ref double best, ref int bestSlot)
    {
        for (int slot = _cellHeads[cell]; slot >= 0; slot = _entries[slot].Next)
        {
            double dx = _entries[slot].X - x;
            double dy = _entries[slot].Y - y;
            double distance = (dx * dx) + (dy * dy);

            // Admits equality so that the seeded bound is inclusive, which is what the distance bound
            // documents: a point exactly at the limit is a result. The cost is that the last of a set of tied
            // points wins rather than the first, and ties are unspecified either way.
            if (distance <= best)
            {
                best = distance;
                bestSlot = slot;
            }
        }
    }

    // The query region, supplying the exact membership test and the axis-aligned bounds that pick the cells.
    // A readonly struct used as a generic type argument is specialized by the JIT and inlined into the walk,
    // so the shared traversal costs nothing over two hand-written ones — the same rule the struct hashers,
    // IMonoid<T> and KdTree's own regions follow.
    private interface IRegion
    {
        double LowX { get; }

        double HighX { get; }

        double LowY { get; }

        double HighY { get; }

        // A region no point can be in. NaN is the only way to get one: it has no position, so it neither
        // bounds a cell range nor passes a membership test, and the walk declines to start rather than
        // relying on how a NaN converts to a cell index.
        bool IsEmpty { get; }

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
            IsEmpty = double.IsNaN(x) || double.IsNaN(y);
        }

        public double LowX { get; }

        public double HighX { get; }

        public double LowY { get; }

        public double HighY { get; }

        public bool IsEmpty { get; }

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
            IsEmpty = double.IsNaN(minX) || double.IsNaN(minY) || double.IsNaN(maxX) || double.IsNaN(maxY);
        }

        public double LowX { get; }

        public double HighX { get; }

        public double LowY { get; }

        public double HighY { get; }

        public bool IsEmpty { get; }

        // The bounds are the predicate for a box, so the walk's cell range and its membership test agree by
        // construction — a box query only ever visits cells the bounds already admitted.
        public bool Contains(double x, double y) => x >= LowX && x <= HighX && y >= LowY && y <= HighY;
    }

    // What each range query does with a match.
    private interface IPointVisitor
    {
        // Returns false to stop the walk.
        bool Visit(int slot);
    }

    private struct AnyVisitor : IPointVisitor
    {
        public bool Found;

        public bool Visit(int slot)
        {
            Found = true;
            return false;
        }
    }

    private struct CountVisitor : IPointVisitor
    {
        public int Count;

        public bool Visit(int slot)
        {
            Count++;
            return true;
        }
    }

    private struct CopyVisitor : IPointVisitor
    {
        private readonly SpatialGrid<TValue> _grid;
        private readonly SpatialPoint<TValue>[] _destination;
        private readonly int _start;
        private int _index;

        internal CopyVisitor(SpatialGrid<TValue> grid, SpatialPoint<TValue>[] destination, int destinationIndex)
        {
            _grid = grid;
            _destination = destination;
            _start = destinationIndex;
            _index = destinationIndex;
        }

        internal readonly int Written => _index - _start;

        // The callers guarantee at least one free slot before starting the walk, so there is no entry guard
        // here: the first write is always in range, and the return value stops the walk the moment the last
        // slot is filled rather than letting it run on to a match it could not write anyway.
        public bool Visit(int slot)
        {
            _destination[_index++] = new SpatialPoint<TValue>(_grid._entries[slot].X, _grid._entries[slot].Y, _grid._values[slot]);
            return _index < _destination.Length;
        }
    }

    /// <summary>A struct enumerator over a grid's live entries.</summary>
    /// <remarks>
    /// The order is the entry array's slot order, which reflects the order entries were added and which slots
    /// removals freed for reuse. It is deterministic for a given sequence of operations, but it is an
    /// implementation detail and is neither insertion nor spatial order.
    /// </remarks>
    public struct Enumerator : IEnumerator<SpatialPoint<TValue>>
    {
        private readonly SpatialGrid<TValue> _grid;
        private readonly int _version;
        private int _slot;
        private SpatialPoint<TValue> _current;

        internal Enumerator(SpatialGrid<TValue> grid)
        {
            _grid = grid;
            _version = grid._version;
            _slot = 0;
            _current = default;
        }

        /// <summary>Gets the point at the current position of the enumerator.</summary>
        public readonly SpatialPoint<TValue> Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next live entry, skipping vacated slots.</summary>
        /// <returns><c>true</c> if there is a next point; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The grid was modified after the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _grid._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            while (_slot < _grid._slotCount)
            {
                int slot = _slot++;
                if (_grid._entries[slot].Cell < 0)
                    continue;

                _current = new SpatialPoint<TValue>(_grid._entries[slot].X, _grid._entries[slot].Y, _grid._values[slot]);
                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first entry.</summary>
        /// <exception cref="InvalidOperationException">The grid was modified after the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _grid._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _slot = 0;
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
