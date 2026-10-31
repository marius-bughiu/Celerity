using System.Buffers;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Celerity.Collections;

/// <summary>
/// A <b>growable list stored in fixed-size segments</b>, each kept below the 85,000-byte Large Object Heap
/// threshold. Once the first segment is full, appending never copies an element already stored, and no
/// segment is ever allocated on the Large Object Heap — where <see cref="List{T}"/> doubles one contiguous
/// array, copying every element on each growth and, past about 21,000 <see cref="int"/>s, allocating every
/// new array on the LOH, where it counts toward a full gen-2 collection. It is the list for <i>a large sequence built by appending</i>: a result set, a
/// log, a parsed file, an index under construction.
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
/// <remarks>
/// <para>
/// <b>Layout.</b> Elements live in a spine of segments of <see cref="SegmentLength"/> elements each — the
/// largest power of two whose array stays within 64 KB, so 16,384 <see cref="int"/>s, 8,192 references or
/// <see cref="long"/>s, and 65,536 <see cref="byte"/>s. Element <c>i</c> is at segment <c>i &gt;&gt; k</c>,
/// slot <c>i &amp; (SegmentLength - 1)</c>. A full segment is never reallocated: growth adds one more. The
/// first segment alone starts small and doubles up to <see cref="SegmentLength"/>, exactly as a
/// <see cref="List{T}"/> would, so a short list costs what a short <see cref="List{T}"/> costs.
/// </para>
/// <para>
/// <b>Why not the BCL.</b> <see cref="List{T}"/>'s growth copies the whole array each time it doubles, so a
/// list built to <c>n</c> elements has allocated about <c>2n</c> slots and copied about <c>n</c>, and holds
/// the old and the new array at once while it copies — three times the data at the peak. Every array past
/// the LOH threshold is allocated there, zeroed, and charged to the gen-2 budget, so building large lists
/// triggers full collections that a gen-0-only workload would never see. Pre-sizing avoids all of that only
/// when the final size is known in advance. Past the first segment, this type allocates each slot once,
/// copies nothing on growth, keeps every segment in the small-object heap, and holds at most one partly
/// empty segment. The one array that does grow by doubling is the spine of segment references, a single
/// reference per segment: it reaches the LOH only past 8,192 segments (about 134 million <see cref="int"/>s).
/// </para>
/// <para>
/// <b>What this costs you.</b> An indexed read is two dependent loads (the segment, then the element) where
/// <see cref="List{T}"/> does one, so random access runs roughly 1.2–1.4x slower. The struct enumerator
/// also runs slower than <see cref="List{T}"/>'s — about 1.5x through <c>foreach</c>. A hot
/// sequential loop belongs on <see cref="GetSegment"/>, which hands out each segment as a span and scans at
/// array speed. <see cref="Insert"/>, <see cref="RemoveAt"/> and
/// <see cref="Remove"/> shift every later element, crossing segment boundaries, and are <c>O(n)</c>
/// exactly as <see cref="List{T}"/>'s are; a sequence edited in the middle belongs in
/// <see cref="BTreeList{T}"/>. An element larger than 32 KB is stored one per segment, which still stays under
/// the LOH threshold, since the runtime refuses arrays of any value type of 64 KB or more.
/// </para>
/// <para>
/// <b>Capacity.</b> <see cref="Clear"/> keeps the allocated segments, as <see cref="List{T}.Clear"/> keeps
/// its array; <see cref="TrimExcess"/> releases the unused ones. The list holds at most
/// <c>2^31 - SegmentLength</c> elements.
/// </para>
/// <para>
/// <b>Enumeration.</b> The struct enumerator is invalidated by every structural mutation — <see cref="Add"/>,
/// <see cref="AddRange"/>, <see cref="Insert"/>, <see cref="RemoveAt"/>, a successful <see cref="Remove"/>,
/// a <see cref="Clear"/> that had something to clear, and an <see cref="EnsureCapacity"/> or
/// <see cref="TrimExcess"/> that changed the capacity. Assignment through the indexer changes an element in
/// place and does <b>not</b> invalidate — the rule <see cref="Deque{T}"/> and <see cref="BTreeList{T}"/>
/// follow, and the one place this type deliberately differs from <see cref="List{T}"/>. This type is not
/// thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public sealed class SegmentedList<T> : IList<T>, IReadOnlyList<T>
{
    // The first segment's initial length, matching List<T>'s first allocation.
    private const int DefaultCapacity = 4;

    // A segment's byte budget: the largest power of two that, with the array header, stays under the
    // 85,000-byte LOH threshold.
    private const int SegmentByteBudget = 64 * 1024;

    // log2 of the segment length. An element wider than half the budget gets a one-element segment. The
    // quotient is never zero — the runtime cannot create an array of a value type of 64 KB or more — and the
    // `| 1` only makes that explicit without a branch.
    private static readonly int Shift = BitOperations.Log2((uint)(SegmentByteBudget / Unsafe.SizeOf<T>()) | 1u);
    private static readonly int Length = 1 << Shift;
    private static readonly int Mask = Length - 1;

    // The largest element count. One segment short of 2^31 slots, so the capacity itself fits in an int; a
    // growth past it trips the checked add in AllocateSegment.
    private static readonly int MaxCount = (int)((1u << 31) - (uint)Length);

    private T[][] _segments;  // the spine; slots at and past _allocated are null
    private int _allocated;   // segments allocated, in use or retained for reuse
    private int _capacity;    // total slots across the allocated segments
    private int _count;

    // The segment the next Add writes to and how many of its slots are filled. Kept in step with _count by
    // every mutation, so the common Add is one bounds check and one store.
    private T[] _tail;
    private int _tailCount;

    // Incremented on every structural mutation so active enumerators can detect it. An indexer set is an
    // in-place element change, not structural, and a Clear() of an empty list, a Remove(T) that finds
    // nothing, or an EnsureCapacity / TrimExcess that changes no capacity returns before touching it.
    private int _version;

    /// <summary>Initializes a new, empty list. The first <see cref="Add"/> allocates the first segment.</summary>
    public SegmentedList()
    {
        _segments = Array.Empty<T[]>();
        _tail = Array.Empty<T>();
    }

    /// <summary>
    /// Initializes a new, empty list with room for at least <paramref name="capacity"/> elements before it
    /// allocates again.
    /// </summary>
    /// <param name="capacity">The initial capacity. Must be non-negative; <c>0</c> allocates nothing yet.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative or larger than the most elements the list can hold.
    /// </exception>
    public SegmentedList(int capacity)
        : this()
    {
        EnsureCapacity(capacity);
        _version = 0;
    }

    /// <summary>
    /// Initializes a new list containing the elements of <paramref name="collection"/> in enumeration order.
    /// </summary>
    /// <param name="collection">The elements to copy into the list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <c>null</c>.</exception>
    public SegmentedList(IEnumerable<T> collection)
        : this()
    {
        AddRange(collection);
        _version = 0;
    }

    /// <summary>Gets the number of elements in the list.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets the number of elements the allocated segments can hold before the list allocates again.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the number of elements in one full segment for this element type: the largest power of two whose
    /// array fits in 64 KB. Every segment but the first holds exactly this many slots, and the first grows to
    /// it before a second is allocated.
    /// </summary>
    public int SegmentLength => Length;

    /// <summary>
    /// Gets the number of segments holding elements — <c>0</c> for an empty list. Segments retained after a
    /// <see cref="Clear"/> or a removal are not counted.
    /// </summary>
    public int SegmentCount => _count == 0 ? 0 : ((_count - 1) >> Shift) + 1;

    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// Gets or sets the element at <paramref name="index"/> in <c>O(1)</c>. Setting an element replaces it in
    /// place and does not invalidate enumerators.
    /// </summary>
    /// <param name="index">The zero-based position.</param>
    /// <returns>The element at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Count"/>.
    /// </exception>
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_count)
                ThrowIndexOutOfRange(index);
            return _segments[index >> Shift][index & Mask];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if ((uint)index >= (uint)_count)
                ThrowIndexOutOfRange(index);
            _segments[index >> Shift][index & Mask] = value;
        }
    }

    /// <summary>
    /// Appends <paramref name="item"/> to the end of the list in <c>O(1)</c>. When the last segment is full,
    /// one new segment is allocated; no element already stored is copied, except while the first segment is
    /// still growing toward <see cref="SegmentLength"/>.
    /// </summary>
    /// <param name="item">The element to append.</param>
    /// <exception cref="OverflowException">The list already holds the most elements it can.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        T[] tail = _tail;
        int tailCount = _tailCount;
        if ((uint)tailCount < (uint)tail.Length)
        {
            tail[tailCount] = item;
            _tailCount = tailCount + 1;
            _count++;
            _version++;
            return;
        }
        AddWithGrowth(item);
    }

    /// <summary>
    /// Appends the elements of <paramref name="collection"/> in enumeration order. An array, a
    /// <see cref="List{T}"/> or another <see cref="SegmentedList{T}"/> is copied a block at a time after one
    /// capacity check; any other <see cref="ICollection{T}"/> is snapshotted through its
    /// <see cref="ICollection{T}.CopyTo"/> first, as <see cref="List{T}"/> does. Adding a list to itself, or a
    /// collection that wraps it, appends one copy of its current elements.
    /// </summary>
    /// <param name="collection">The elements to append.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <c>null</c>.</exception>
    public void AddRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        switch (collection)
        {
            case T[] array:
                AppendSpan(array);
                break;
            case List<T> list:
                AppendSpan(CollectionsMarshal.AsSpan(list));
                break;
            case SegmentedList<T> other:
                AppendSegments(other);
                break;
            default:
                if (collection is ICollection<T> sized)
                    AppendCollection(sized);
                else
                {
                    foreach (T item in collection)
                        Add(item);
                }
                break;
        }
    }

    /// <summary>
    /// Inserts <paramref name="item"/> at <paramref name="index"/>, shifting the element there and every later
    /// one up by one position. This is <c>O(n - index)</c>, as on <see cref="List{T}"/>; inserting at
    /// <see cref="Count"/> is an <see cref="Add"/>.
    /// </summary>
    /// <param name="index">The position to insert at; <see cref="Count"/> appends.</param>
    /// <param name="item">The element to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than <see cref="Count"/>.
    /// </exception>
    public void Insert(int index, T item)
    {
        if ((uint)index > (uint)_count)
            ThrowIndexOutOfRange(index);

        int old = _count;
        if (index == old)
        {
            Add(item);
            return;
        }

        // Append a copy of the last element, which handles any growth, then shift [index, old - 1) up by one
        // segment at a time from the back. `hi` is the highest position still waiting for its new value.
        Add(this[old - 1]);
        int hi = old - 1;
        int lowSegment = index >> Shift;
        for (int s = hi >> Shift; s > lowSegment; s--)
        {
            T[] segment = _segments[s];
            Array.Copy(segment, 0, segment, 1, hi & Mask);
            segment[0] = _segments[s - 1][Mask];
            hi = (s << Shift) - 1;
        }

        T[] first = _segments[lowSegment];
        int offset = index & Mask;
        Array.Copy(first, offset, first, offset + 1, (hi & Mask) - offset);
        first[offset] = item;
    }

    /// <summary>
    /// Removes the element at <paramref name="index"/>, shifting every later one down by one position. This
    /// is <c>O(n - index)</c>, as on <see cref="List{T}"/>; removing the last element is <c>O(1)</c>.
    /// </summary>
    /// <param name="index">The position of the element to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Count"/>.
    /// </exception>
    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count)
            ThrowIndexOutOfRange(index);

        int lastIndex = _count - 1;
        int lastSegment = lastIndex >> Shift;
        int s = index >> Shift;
        int offset = index & Mask;
        T[] segment = _segments[s];
        while (s < lastSegment)
        {
            // Close the gap inside this segment, then pull the next segment's first element into its end.
            Array.Copy(segment, offset + 1, segment, offset, Mask - offset);
            T[] next = _segments[++s];
            segment[Mask] = next[0];
            segment = next;
            offset = 0;
        }

        int end = lastIndex & Mask;
        Array.Copy(segment, offset + 1, segment, offset, end - offset);
        segment[end] = default!;

        _count = lastIndex;
        _version++;
        SetTail();
    }

    /// <summary>
    /// Removes the first occurrence of <paramref name="item"/>, compared with
    /// <see cref="EqualityComparer{T}.Default"/>. Both the search and the shift are <c>O(n)</c>, as on
    /// <see cref="List{T}"/>.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns><c>true</c> if an element was removed; <c>false</c> if none was found.</returns>
    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0)
            return false;
        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Returns the position of the first occurrence of <paramref name="item"/>, compared with
    /// <see cref="EqualityComparer{T}.Default"/>, scanning front to back one segment at a time in <c>O(n)</c>.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>The zero-based position of the first match, or <c>-1</c> if there is none.</returns>
    public int IndexOf(T item)
    {
        int segments = SegmentCount;
        for (int s = 0; s < segments; s++)
        {
            int start = s << Shift;
            int i = Array.IndexOf(_segments[s], item, 0, Math.Min(_count - start, Length));
            if (i >= 0)
                return start + i;
        }
        return -1;
    }

    /// <summary>
    /// Determines whether the list contains <paramref name="item"/>, compared with
    /// <see cref="EqualityComparer{T}.Default"/>. This is a linear <c>O(n)</c> scan.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><c>true</c> if the element is found; otherwise <c>false</c>.</returns>
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <summary>
    /// Removes every element. The allocated segments are kept for reuse, as <see cref="List{T}.Clear"/>
    /// keeps its array; call <see cref="TrimExcess"/> to release them. Clearing an already-empty list is a
    /// true no-op and leaves active enumerators valid.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            int segments = SegmentCount;
            for (int s = 0; s < segments; s++)
                Array.Clear(_segments[s], 0, Math.Min(_count - (s << Shift), Length));
        }

        _count = 0;
        _version++;
        SetTail();
    }

    /// <summary>
    /// Ensures the list can hold at least <paramref name="capacity"/> elements without allocating again. Past
    /// <see cref="SegmentLength"/> this allocates whole segments and copies nothing already stored.
    /// </summary>
    /// <param name="capacity">The minimum capacity to guarantee.</param>
    /// <returns>The capacity of the list after the call (never less than <paramref name="capacity"/>).</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative or larger than the most elements the list can hold.
    /// </exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (capacity > MaxCount)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity,
                $"A {nameof(SegmentedList<T>)} of this element type holds at most {MaxCount} elements.");

        EnsureCapacityCore(capacity);
        return _capacity;
    }

    /// <summary>
    /// Releases the segments past the last one in use, and shrinks a list that fits in one segment to exactly
    /// <see cref="Count"/> slots. Does nothing, and leaves enumerators valid, when there is nothing to release.
    /// </summary>
    public void TrimExcess()
    {
        int used = SegmentCount;
        if (used <= 1)
        {
            if (_capacity == _count)
                return;
            ReleaseSegmentsFrom(_count == 0 ? 0 : 1);
            if (_count > 0 && _segments[0].Length != _count)
                ResizeFirst(_count);
        }
        else
        {
            if (_allocated == used)
                return;
            ReleaseSegmentsFrom(used);
        }

        _version++;
        SetTail();
    }

    /// <summary>
    /// Returns the elements of segment <paramref name="segmentIndex"/> as a contiguous span — the elements at
    /// positions <c>segmentIndex * SegmentLength</c> onward, up to <see cref="SegmentLength"/> of them. Walking
    /// segments <c>0</c> to <see cref="SegmentCount"/> - 1 visits every element in order at array speed, and
    /// is the way to hand the list to span-based or vectorized code.
    /// </summary>
    /// <param name="segmentIndex">The zero-based segment, below <see cref="SegmentCount"/>.</param>
    /// <returns>A read-only span over the segment's elements, valid until the list is next modified.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="segmentIndex"/> is negative or not less than <see cref="SegmentCount"/>.
    /// </exception>
    public ReadOnlySpan<T> GetSegment(int segmentIndex)
    {
        if ((uint)segmentIndex >= (uint)SegmentCount)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex), segmentIndex, "Segment index was out of range.");

        int start = segmentIndex << Shift;
        return new ReadOnlySpan<T>(_segments[segmentIndex], 0, Math.Min(_count - start, Length));
    }

    /// <summary>Copies the elements, front to back, into a new array.</summary>
    /// <returns>A new array of length <see cref="Count"/> holding the elements in order.</returns>
    public T[] ToArray()
    {
        if (_count == 0)
            return Array.Empty<T>();

        var array = new T[_count];
        CopyToCore(array, 0);
        return array;
    }

    /// <summary>
    /// Copies the elements, front to back, into <paramref name="array"/> starting at
    /// <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The destination does not have enough room from <paramref name="arrayIndex"/> onward.
    /// </exception>
    public void CopyTo(T[] array, int arrayIndex)
    {
        CopyToGuard.Validate(array, arrayIndex, _count, CopyToGuard.ElementsMessage);
        CopyToCore(array, arrayIndex);
    }

    /// <summary>
    /// Returns an allocation-free struct enumerator that yields the elements front to back. A structural
    /// modification during enumeration makes <see cref="Enumerator.MoveNext"/> throw
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this list.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- growth -------------------------------------------------------------------------------------

    // The Add slow path: the tail segment is full (or nothing is allocated yet).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithGrowth(T item)
    {
        int count = _count;
        int s = count >> Shift;
        int offset = count & Mask;

        // A segment retained by Clear() or a removal is reused as it stands. Otherwise the first segment
        // grows toward a full one, as a List<T>'s array would, and any later one is a fresh full segment.
        if (s >= _allocated || _segments[s].Length == offset)
        {
            if (s == 0)
                ResizeFirst(Math.Min(Math.Max(_capacity * 2, DefaultCapacity), Length));
            else
                AllocateSegment();
        }

        T[] segment = _segments[s];
        segment[offset] = item;
        _tail = segment;
        _tailCount = offset + 1;
        _count = count + 1;
        _version++;
    }

    // Grows the capacity to at least `capacity`, which must not exceed MaxCount. Bumps the version when
    // anything was allocated, because a first segment that grew was copied to a new array.
    private void EnsureCapacityCore(int capacity)
    {
        if (capacity <= _capacity)
            return;

        // A capacity below one full segment means at most the first segment is allocated, since a second
        // only follows a full first one.
        if (capacity <= Length)
        {
            // Everything still fits in the first segment: grow it the way a List<T> grows its array.
            ResizeFirst(Math.Max(capacity, Math.Min(Math.Max(_capacity * 2, DefaultCapacity), Length)));
        }
        else
        {
            if (_allocated > 0 && _segments[0].Length < Length)
                ResizeFirst(Length);
            int needed = (int)(((uint)capacity + (uint)Mask) >> Shift);
            while (_allocated < needed)
                AllocateSegment();
        }

        _version++;
        SetTail();
    }

    // Replaces the first segment with one of `length` slots, keeping its elements. The caller re-points the
    // tail afterwards when the first segment was the tail.
    private void ResizeFirst(int length)
    {
        var resized = new T[length];
        if (_allocated == 0)
        {
            // Nothing is allocated, so the spine is the shared empty array too.
            _segments = new T[DefaultCapacity][];
            _allocated = 1;
        }
        else
        {
            T[] old = _segments[0];
            Array.Copy(old, resized, Math.Min(_count, length));
            _capacity -= old.Length;
        }

        _segments[0] = resized;
        _capacity += length;
    }

    // Appends one full-length segment. The checked add is the only guard on the element ceiling: it can
    // only fail once the capacity is within one segment of 2^31, which nothing short of an Add loop reaches.
    private void AllocateSegment()
    {
        int capacity = checked(_capacity + Length);
        if (_allocated == _segments.Length)
            Array.Resize(ref _segments, Math.Max(_segments.Length * 2, DefaultCapacity));
        _segments[_allocated++] = new T[Length];
        _capacity = capacity;
    }

    // Drops every segment from `first` on. Every segment past the first is full-length, so releasing from 1
    // or later subtracts whole segments; releasing from 0 drops the spine too.
    private void ReleaseSegmentsFrom(int first)
    {
        if (first == 0)
        {
            _segments = Array.Empty<T[]>();
            _capacity = 0;
        }
        else
        {
            Array.Clear(_segments, first, _allocated - first);
            _capacity -= (_allocated - first) << Shift;
        }
        _allocated = first;
    }

    // Re-points the Add fast path at the segment holding the last element, or at the first segment when the
    // list is empty. A full tail sends the next Add to AddWithGrowth, which moves on to the next segment.
    private void SetTail()
    {
        if (_count == 0)
        {
            _tail = _allocated == 0 ? Array.Empty<T>() : _segments[0];
            _tailCount = 0;
            return;
        }

        int s = (_count - 1) >> Shift;
        _tail = _segments[s];
        _tailCount = _count - (s << Shift);
    }

    // ---- bulk copies --------------------------------------------------------------------------------

    private void AppendSpan(ReadOnlySpan<T> source)
    {
        if (source.IsEmpty)
            return;

        // Checked, so a total past int.MaxValue throws here rather than wrapping negative, skipping the
        // reservation and running the copy off the end of the allocated segments.
        EnsureCapacityCore(checked(_count + source.Length));
        while (!source.IsEmpty)
        {
            T[] segment = _segments[_count >> Shift];
            int offset = _count & Mask;
            int n = Math.Min(segment.Length - offset, source.Length);
            source.Slice(0, n).CopyTo(segment.AsSpan(offset));
            source = source.Slice(n);
            _count += n;
        }

        _version++;
        SetTail();
    }

    // Appends any other ICollection<T> through its CopyTo into a pooled buffer, as List<T> does: a snapshot
    // taken before anything is appended, so a collection that wraps this list (a ReadOnlyCollection<T> over
    // it, say) appends one copy rather than invalidating its own enumeration halfway through.
    private void AppendCollection(ICollection<T> source)
    {
        int n = source.Count;
        T[] buffer = ArrayPool<T>.Shared.Rent(n);
        try
        {
            source.CopyTo(buffer, 0);
            AppendSpan(new ReadOnlySpan<T>(buffer, 0, n));
        }
        finally
        {
            ArrayPool<T>.Shared.Return(buffer, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    // Appends `other` segment by segment. Safe when `other` is this list: the count is captured first, the
    // capacity is reserved before any span is taken, and the destination slots all lie past the source ones.
    private void AppendSegments(SegmentedList<T> other)
    {
        int n = other._count;
        if (n == 0)
            return;

        EnsureCapacityCore(checked(_count + n));
        for (int s = 0, copied = 0; copied < n; s++)
        {
            ReadOnlySpan<T> segment = new(other._segments[s], 0, Math.Min(n - copied, Length));
            AppendSpan(segment);
            copied += segment.Length;
        }
    }

    private void CopyToCore(T[] array, int arrayIndex)
    {
        int segments = SegmentCount;
        for (int s = 0; s < segments; s++)
        {
            int start = s << Shift;
            int n = Math.Min(_count - start, Length);
            Array.Copy(_segments[s], 0, array, arrayIndex + start, n);
        }
    }

    private static void ThrowIndexOutOfRange(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index), index, "Index was out of range.");

    /// <summary>
    /// A struct enumerator over a <see cref="SegmentedList{T}"/> that yields the elements front to back.
    /// Because it is a struct, iterating via <c>foreach</c> avoids the allocation a compiler-generated
    /// <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        // List<T>.Enumerator's shape: four fields and one position, few enough for the JIT to keep in
        // registers. Caching the current segment in more fields measured slower, not faster.
        private readonly SegmentedList<T> _list;
        private readonly int _version;
        private int _index;
        private T _current;

        internal Enumerator(SegmentedList<T> list)
        {
            _list = list;
            _version = list._version;
            _index = 0;
            _current = default!;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element.</summary>
        /// <returns><c>true</c> if the enumerator advanced to an element; <c>false</c> past the end.</returns>
        /// <exception cref="InvalidOperationException">The list was modified since the enumerator was created.</exception>
        public bool MoveNext()
        {
            SegmentedList<T> list = _list;
            int index = _index;
            if (_version == list._version && (uint)index < (uint)list._count)
            {
                _current = list._segments[index >> Shift][index & Mask];
                _index = index + 1;
                return true;
            }

            // The end-of-list path stays inline and throws through a static helper: a call that took this
            // struct's address would stop the JIT keeping its fields in registers, and that store-and-reload
            // on every element cost about 3.5x on a foreach.
            if (_version != list._version)
                ThrowVersionChanged();
            _index = list._count + 1;
            _current = default!;
            return false;
        }

        /// <summary>Resets the enumerator to its initial position, before the first element.</summary>
        /// <exception cref="InvalidOperationException">The list was modified since the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _list._version)
                ThrowVersionChanged();

            _index = 0;
            _current = default!;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public readonly void Dispose() { }

        private static void ThrowVersionChanged() =>
            throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
    }
}
