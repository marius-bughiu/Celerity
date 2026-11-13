using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <b>positional list</b> laid out as a <b>gap buffer</b>: one array whose free space is kept where the
/// caller is editing, so inserting or removing next to the previous edit is <c>O(1)</c> while
/// <see cref="List{T}"/> shifts every later element and costs <c>O(n)</c>. It is the list for <i>a sequence
/// edited around a moving cursor</i> — a text or token buffer behind a caret, a timeline edited at a playhead —
/// and it reads at close to the speed of an array.
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
/// <remarks>
/// <para>
/// <b>Layout.</b> The elements live in one backing array split in two by a run of unused slots, the
/// <i>gap</i>. Elements <c>[0, <see cref="GapPosition"/>)</c> sit before it and the rest sit after it, at the
/// array's end. An insert at <see cref="GapPosition"/> writes into the gap's first slot and a removal next to
/// it widens the gap by one; neither moves another element. An edit anywhere else first <i>moves</i> the gap
/// there, copying exactly the elements between the old and the new position — so an edit costs
/// <c>O(distance from the previous edit)</c>, not <c>O(n)</c>. When the gap is used up the array doubles and
/// the gap is reopened at the edit position in the same copy.
/// </para>
/// <para>
/// <b>Why not the BCL.</b> <see cref="List{T}"/> pays <c>O(n)</c> for every <see cref="List{T}.Insert"/> and
/// <see cref="List{T}.RemoveAt"/> that is not at the end, however close together the edits are: at 100,000
/// elements an edit near the middle shifts about 50,000 of them. <see cref="LinkedList{T}"/> edits in
/// <c>O(1)</c> but has no indexer and allocates a node per element. <see cref="BTreeList{T}"/> edits anywhere in
/// <c>O(log n)</c>, but pays a tree descent on every read. This type keeps <see cref="List{T}"/>'s read path: an
/// indexer read is one array load at an offset computed without a branch, so a random read with the gap in the
/// middle costs 1.1–1.3x what <see cref="List{T}"/>'s does, where a tree descent costs several times that.
/// </para>
/// <para>
/// <b>What this costs you.</b> The cost of an edit is the distance the gap has to travel. Edits that jump
/// around the list at random move it about a third of the list each time, which is the same order as
/// <see cref="List{T}"/>'s shift and much worse than <see cref="BTreeList{T}"/>'s <c>O(log n)</c>: a list edited
/// at random positions belongs in <see cref="BTreeList{T}"/>. <see cref="Add"/> appends at the end, so the
/// first append after editing in the middle moves the gap to the end. Like <see cref="List{T}"/>, the backing
/// array only grows; <see cref="TrimExcess"/> releases the spare capacity.
/// </para>
/// <para>
/// <b>Search is linear.</b> <see cref="IndexOf"/>, <see cref="Contains"/> and <see cref="Remove"/> scan both
/// halves with <see cref="EqualityComparer{T}.Default"/>, exactly as <see cref="List{T}"/>'s do, and do not move
/// the gap; <see cref="Remove"/> then removes the match like <see cref="RemoveAt"/>.
/// </para>
/// <para>
/// <b>Enumeration.</b> The struct enumerator yields the elements front to back and is invalidated by every
/// structural mutation — an insert, a removal that removed something, a <see cref="Clear"/> that had
/// something to clear, and a reallocation by <see cref="EnsureCapacity"/> or <see cref="TrimExcess"/>.
/// Assignment through the indexer changes an element in place and does <b>not</b> invalidate, the rule
/// <see cref="BTreeList{T}"/> and <see cref="Deque{T}"/> follow and the one place this type deliberately differs
/// from <see cref="List{T}"/>. This type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public sealed class GapBuffer<T> : IList<T>, IReadOnlyList<T>
{
    private const int DefaultCapacity = 4;

    // Elements occupy [0, _gapStart) and [_gapEnd, _items.Length); the gap [_gapStart, _gapEnd) holds
    // default(T), so a reference type keeps nothing alive through a slot it no longer uses.
    private T[] _items;
    private int _gapStart;

    // Incremented on every structural mutation so active enumerators can detect it. An indexer set is an
    // in-place element change, not structural, and an edit that changes nothing (an empty range, a Clear() of
    // an empty buffer, a Remove(T) that finds nothing) returns before touching it.
    //
    // Declared between the gap's bounds on purpose; see Add. Adjacent, the JIT reads the two bounds with one
    // paired load, which overlaps the previous append's 4-byte store to _gapStart and so cannot be forwarded
    // from it on Arm64.
    private int _version;
    private int _gapEnd;

    /// <summary>
    /// Initializes a new, empty buffer with no pre-allocated capacity. The first insert allocates the backing
    /// array.
    /// </summary>
    public GapBuffer()
    {
        _items = Array.Empty<T>();
    }

    /// <summary>
    /// Initializes a new, empty buffer whose backing array is pre-sized to hold at least
    /// <paramref name="capacity"/> elements before the first growth.
    /// </summary>
    /// <param name="capacity">The initial capacity. Must be non-negative; <c>0</c> allocates nothing yet.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public GapBuffer(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        _items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
        _gapEnd = capacity;
    }

    /// <summary>
    /// Initializes a new buffer containing the elements of <paramref name="collection"/> in enumeration order,
    /// with the gap at the end.
    /// </summary>
    /// <param name="collection">The elements to copy into the buffer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <c>null</c>.</exception>
    public GapBuffer(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (collection is ICollection<T> c)
        {
            // Fewer than n when the source shrank after it was counted; the slots it left become the gap.
            int n = c.Count;
            _items = n == 0 ? Array.Empty<T>() : new T[n];
            _gapStart = CountedSource.CopyTo(c, _items, nameof(collection));
            _gapEnd = n;
        }
        else
        {
            _items = Array.Empty<T>();
            foreach (T item in collection)
                Add(item);
            _version = 0;
        }
    }

    /// <summary>Gets the number of elements in the buffer.</summary>
    public int Count => _items.Length - (_gapEnd - _gapStart);

    /// <summary>
    /// Gets the number of elements the buffer can hold before its backing array must grow.
    /// </summary>
    public int Capacity => _items.Length;

    /// <summary>
    /// Gets the position the gap currently sits at: the number of elements before it. An
    /// <see cref="Insert"/> at this position, or a <see cref="RemoveAt"/> of the element on either side of it,
    /// moves no other element; an edit at position <c>p</c> first copies <c>|p - GapPosition|</c> elements.
    /// Public so the cost of the next edit is observable rather than folklore.
    /// </summary>
    public int GapPosition => _gapStart;

    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// Gets or sets the element at <paramref name="index"/> in <c>O(1)</c>, wherever the gap is. Setting an
    /// element replaces it in place and does not invalidate enumerators.
    /// </summary>
    /// <param name="index">The zero-based position.</param>
    /// <returns>The element at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Count"/>.
    /// </exception>
    public T this[int index]
    {
        get
        {
            T[] items = _items;
            int slot = Physical(index);
            if ((uint)slot >= (uint)items.Length)
                ThrowIndexOutOfRange(index);
            return items[slot];
        }
        set
        {
            T[] items = _items;
            int slot = Physical(index);
            if ((uint)slot >= (uint)items.Length)
                ThrowIndexOutOfRange(index);
            items[slot] = value;
        }
    }

    /// <summary>
    /// Appends <paramref name="item"/> to the end of the buffer. <c>O(1)</c> amortized once the gap is at the
    /// end; the first append after editing elsewhere moves the gap there.
    /// </summary>
    /// <param name="item">The element to append.</param>
    public void Add(T item)
    {
        // The append fast path, shaped like List<T>.Add's so it inlines: with the gap at the end and not used
        // up, an append is one store. The unsigned compare also proves the store in bounds.
        //
        // Every load and store here is placed so the JIT pairs none of them. The next append reloads _gapStart,
        // and on Arm64 a load overlapping a paired store, or a paired load overlapping a single store, cannot be
        // forwarded from that store and waits for it to retire: either pairing measured 3x List<T>.Add in a
        // steady-state loop. So the version is bumped first, ahead of the loads, as List<T> does (the slow path
        // bumps it again, which is harmless), and the field order keeps _gapStart and _gapEnd apart.
        _version++;
        T[] items = _items;
        int gapStart = _gapStart;
        if (_gapEnd == items.Length && (uint)gapStart < (uint)items.Length)
        {
            _gapStart = gapStart + 1;
            items[gapStart] = item;
            return;
        }
        AddSlow(item);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddSlow(T item) => InsertCore(Count, item);

    /// <summary>
    /// Inserts <paramref name="item"/> at <paramref name="index"/>, shifting the element there and every later
    /// one up by one position. <c>O(1)</c> amortized at <see cref="GapPosition"/>, and otherwise
    /// <c>O(|index - GapPosition|)</c> to move the gap first.
    /// </summary>
    /// <param name="index">The position to insert at; <see cref="Count"/> appends.</param>
    /// <param name="item">The element to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than <see cref="Count"/>.
    /// </exception>
    public void Insert(int index, T item)
    {
        if ((uint)index > (uint)Count)
            ThrowIndexOutOfRange(index);
        InsertCore(index, item);
    }

    /// <summary>
    /// Inserts the elements of <paramref name="items"/> at <paramref name="index"/>, in order, shifting the
    /// element there and every later one up by <c>items.Length</c> positions. Costs the gap move to
    /// <paramref name="index"/> plus one copy of <paramref name="items"/>, and leaves the gap just after the
    /// inserted run. An empty span changes nothing and does not invalidate enumerators.
    /// </summary>
    /// <param name="index">The position to insert at; <see cref="Count"/> appends.</param>
    /// <param name="items">The elements to insert. May be a span over this buffer's own elements.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than <see cref="Count"/>.
    /// </exception>
    public void InsertRange(int index, ReadOnlySpan<T> items)
    {
        if ((uint)index > (uint)Count)
            ThrowIndexOutOfRange(index);
        if (items.IsEmpty)
            return;

        // A span over this buffer's own array (from GetSpans) would be rewritten by the gap move below before
        // it is read, so copy it out first. Growth would have made it safe, but only by accident.
        if (_items.AsSpan().Overlaps(items))
            items = items.ToArray();

        int n = items.Length;
        if (_gapEnd - _gapStart < n)
            Grow(checked(Count + n), index);
        else
            MoveGap(index);

        items.CopyTo(_items.AsSpan(_gapStart));
        _gapStart += n;
        _version++;
    }

    /// <summary>
    /// Removes the element at <paramref name="index"/>, shifting every later one down by one position.
    /// <c>O(1)</c> for the element on either side of <see cref="GapPosition"/> (a backspace or a delete at the
    /// cursor), and otherwise <c>O(distance)</c> to move the gap first.
    /// </summary>
    /// <param name="index">The position of the element to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Count"/>.
    /// </exception>
    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)Count)
            ThrowIndexOutOfRange(index);
        RemoveCore(index, 1);
    }

    /// <summary>
    /// Removes <paramref name="count"/> elements starting at <paramref name="index"/>, shifting every later one
    /// down. A range that touches or straddles the gap is absorbed into it without moving any element;
    /// otherwise the gap moves to the nearer end of the range first. A <paramref name="count"/> of <c>0</c>
    /// changes nothing and does not invalidate enumerators.
    /// </summary>
    /// <param name="index">The position of the first element to remove.</param>
    /// <param name="count">The number of elements to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> or <paramref name="count"/> is negative.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="index"/> and <paramref name="count"/> do not denote a range of elements in the buffer.
    /// </exception>
    public void RemoveRange(int index, int count)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be non-negative.");
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be non-negative.");
        if (Count - index < count)
            throw new ArgumentException("Index and count do not denote a valid range of elements.");
        if (count == 0)
            return;

        RemoveCore(index, count);
    }

    /// <summary>
    /// Removes the first occurrence of <paramref name="item"/>, compared with
    /// <see cref="EqualityComparer{T}.Default"/>. The search is a linear <c>O(n)</c> scan, as on
    /// <see cref="List{T}"/>; the removal itself then costs what <see cref="RemoveAt"/> does.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns><c>true</c> if an element was removed; <c>false</c> if none was found.</returns>
    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0)
            return false;
        RemoveCore(index, 1);
        return true;
    }

    /// <summary>
    /// Returns the position of the first occurrence of <paramref name="item"/>, compared with
    /// <see cref="EqualityComparer{T}.Default"/>, scanning front to back in <c>O(n)</c>. The gap does not move.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>The zero-based position of the first match, or <c>-1</c> if there is none.</returns>
    public int IndexOf(T item)
    {
        int i = Array.IndexOf(_items, item, 0, _gapStart);
        if (i >= 0)
            return i;

        i = Array.IndexOf(_items, item, _gapEnd, _items.Length - _gapEnd);
        return i < 0 ? -1 : i - (_gapEnd - _gapStart);
    }

    /// <summary>
    /// Determines whether the buffer contains <paramref name="item"/>, compared with
    /// <see cref="EqualityComparer{T}.Default"/>. This is a linear <c>O(n)</c> scan.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><c>true</c> if the element is found; otherwise <c>false</c>.</returns>
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <summary>
    /// Removes every element. The backing array is retained (use <see cref="TrimExcess"/> to release it) and
    /// the whole of it becomes the gap, at position <c>0</c>. Clearing an already-empty buffer is a true no-op
    /// and leaves active enumerators valid.
    /// </summary>
    public void Clear()
    {
        if (Count == 0)
            return;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(_items, 0, _gapStart);
            Array.Clear(_items, _gapEnd, _items.Length - _gapEnd);
        }
        _gapStart = 0;
        _gapEnd = _items.Length;
        _version++;
    }

    /// <summary>
    /// Exposes the elements without copying, as the run before the gap and the run after it. Concatenated,
    /// <paramref name="beforeGap"/> and <paramref name="afterGap"/> are the buffer's elements front to back;
    /// either may be empty. The gap does not move, and the spans are invalidated by the next structural edit.
    /// </summary>
    /// <param name="beforeGap">The elements at positions <c>[0, <see cref="GapPosition"/>)</c>.</param>
    /// <param name="afterGap">The elements at positions <c>[<see cref="GapPosition"/>, <see cref="Count"/>)</c>.</param>
    public void GetSpans(out ReadOnlySpan<T> beforeGap, out ReadOnlySpan<T> afterGap)
    {
        beforeGap = new ReadOnlySpan<T>(_items, 0, _gapStart);
        afterGap = new ReadOnlySpan<T>(_items, _gapEnd, _items.Length - _gapEnd);
    }

    /// <summary>Copies the elements, front to back, into a new array.</summary>
    /// <returns>A new array of length <see cref="Count"/> holding the elements in order.</returns>
    public T[] ToArray()
    {
        int count = Count;
        if (count == 0)
            return Array.Empty<T>();

        var array = new T[count];
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
        CopyToGuard.Validate(array, arrayIndex, Count, CopyToGuard.ElementsMessage);
        CopyToCore(array, arrayIndex);
    }

    /// <summary>
    /// Ensures the buffer can hold at least <paramref name="capacity"/> elements without growing, allocating a
    /// larger backing array if needed. The gap keeps its position.
    /// </summary>
    /// <param name="capacity">The minimum capacity to guarantee.</param>
    /// <returns>The capacity of the buffer after the call (never less than <paramref name="capacity"/>).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (_items.Length < capacity)
        {
            Grow(capacity, _gapStart);
            _version++;
        }
        return _items.Length;
    }

    /// <summary>
    /// Releases unused capacity, shrinking the backing array to exactly <see cref="Count"/>. The gap keeps its
    /// position but becomes empty, so the next insert grows the array again. Does nothing if the buffer
    /// already fills its array.
    /// </summary>
    public void TrimExcess()
    {
        int count = Count;
        if (count == _items.Length)
            return;

        var trimmed = count == 0 ? Array.Empty<T>() : new T[count];
        CopyToCore(trimmed, 0);
        _items = trimmed;
        _gapEnd = _gapStart;
        _version++;
    }

    /// <summary>
    /// Returns an allocation-free struct enumerator that yields the elements front to back. A structural
    /// modification during enumeration makes <see cref="Enumerator.MoveNext"/> throw
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this buffer.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- gap machinery ------------------------------------------------------------------------------

    // Maps a logical position to its slot: positions at or past the gap are shifted over it. The shift is a
    // mask, not a branch — `_gapStart - 1 - index` is negative exactly when the position is past the gap —
    // because a random read with the gap in the middle would otherwise mispredict about half the time, which
    // measured 2.5x a List<T> read against about 1.2x for the mask.
    //
    // It also serves as the indexer's range check. The slot is inside [0, _items.Length) exactly when the
    // position is inside [0, Count): a position before the gap maps to itself, below _gapStart; one past it
    // maps below the array length only if it is below Count; and a negative position maps to a negative slot,
    // including the very negative ones whose `_gapStart - 1 - index` overflows, since adding the gap's length
    // to them still leaves them below _gapEnd - 2^31. So one unsigned compare against the array length, the
    // one the array access makes anyway, rejects every bad position.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Physical(int index) => index + ((_gapEnd - _gapStart) & ((_gapStart - 1 - index) >> 31));

    // Moves the gap so it starts at `position`, copying the elements between the old and new start across it,
    // then clears the slots those elements left inside the gap. That clear is the smaller of the distance moved
    // and the gap's length: when the move is longer than the gap, every slot the gap now covers was
    // overwritten by the copy except the gap's length of them.
    private void MoveGap(int position)
    {
        T[] items = _items;
        int gapLength = _gapEnd - _gapStart;
        if (position < _gapStart)
        {
            int moved = _gapStart - position;
            Array.Copy(items, position, items, _gapEnd - moved, moved);
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                Array.Clear(items, position, Math.Min(moved, gapLength));
            _gapStart = position;
            _gapEnd -= moved;
        }
        else if (position > _gapStart)
        {
            int moved = position - _gapStart;
            Array.Copy(items, _gapEnd, items, _gapStart, moved);
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                int cleared = Math.Min(moved, gapLength);
                Array.Clear(items, _gapEnd + moved - cleared, cleared);
            }
            _gapStart = position;
            _gapEnd += moved;
        }
    }

    // Reallocates the backing array to hold at least `min` elements, reopening the gap at `position` in the
    // same pass: the elements before `position` go to the front and the rest to the end, so a growth that an
    // insert triggers costs one copy of the elements rather than a copy and then a gap move.
    private void Grow(int min, int position)
    {
        int newCapacity = _items.Length == 0 ? DefaultCapacity : _items.Length * 2;
        newCapacity = ClampToArrayMaxLength(newCapacity);
        if (newCapacity < min)
            newCapacity = min;

        var grown = new T[newCapacity];
        int count = Count;
        int tail = count - position;
        CopyRange(0, grown, 0, position);
        CopyRange(position, grown, newCapacity - tail, tail);
        _items = grown;
        _gapStart = position;
        _gapEnd = newCapacity - tail;
    }

    // Copies `length` elements starting at logical position `from` into `destination` at `destinationIndex`,
    // in up to two runs either side of the gap.
    private void CopyRange(int from, T[] destination, int destinationIndex, int length)
    {
        int beforeGap = Math.Clamp(_gapStart - from, 0, length);
        Array.Copy(_items, from, destination, destinationIndex, beforeGap);
        Array.Copy(_items, from + beforeGap + (_gapEnd - _gapStart), destination, destinationIndex + beforeGap,
            length - beforeGap);
    }

    private void CopyToCore(T[] array, int arrayIndex) => CopyRange(0, array, arrayIndex, Count);

    private void InsertCore(int index, T item)
    {
        if (_gapStart == _gapEnd)
            Grow(Count + 1, index);
        else
            MoveGap(index);

        _items[_gapStart++] = item;
        _version++;
    }

    // Removes [index, index + count) by moving the gap to the nearest point of that range — no move at all when
    // the gap already touches or splits it — and then widening the gap over the elements on each side.
    private void RemoveCore(int index, int count)
    {
        MoveGap(Math.Clamp(_gapStart, index, index + count));

        int before = _gapStart - index;
        int after = count - before;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(_items, index, before);
            Array.Clear(_items, _gapEnd, after);
        }
        _gapStart = index;
        _gapEnd += after;
        _version++;
    }

    // The doubling ceiling. Reaching it needs a backing array of more than 2^30 elements, so it is only
    // observable from a test that first allocates gigabytes. Excluded from coverage so the gate does not
    // depend on whether a runner had the headroom to run such a test; Deque<T> pins the same expression.
    [ExcludeFromCodeCoverage(Justification = "Needs a >1 billion element backing array; the identical " +
        "clamp is pinned by DequeGrowthTests behind [MemoryIntensiveFact].")]
    private static int ClampToArrayMaxLength(int newCapacity) =>
        (uint)newCapacity > (uint)Array.MaxLength ? Array.MaxLength : newCapacity;

    private static void ThrowIndexOutOfRange(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index), index, "Index was out of range.");

    /// <summary>
    /// A struct enumerator over a <see cref="GapBuffer{T}"/> that yields the elements front to back.
    /// Because it is a struct, iterating via <c>foreach</c> avoids the allocation a compiler-generated
    /// <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        // The array and the gap's bounds are snapshotted: anything that could change them (an edit, a growth, a
        // trim) bumps the version, which MoveNext checks first, and an indexer set writes into this same array.
        // _index is a physical slot, so a step is one compare against the gap rather than a position mapping.
        private readonly GapBuffer<T> _buffer;
        private readonly T[] _items;
        private readonly int _gapStart;
        private readonly int _gapEnd;
        private readonly int _version;
        private int _index;
        private T _current;

        internal Enumerator(GapBuffer<T> buffer)
        {
            _buffer = buffer;
            _items = buffer._items;
            _gapStart = buffer._gapStart;
            _gapEnd = buffer._gapEnd;
            _version = buffer._version;
            _index = 0;
            _current = default!;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element.</summary>
        /// <returns><c>true</c> if the enumerator advanced to an element; <c>false</c> past the end.</returns>
        /// <exception cref="InvalidOperationException">The buffer was modified since the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _buffer._version)
                ThrowVersionChanged();

            int i = _index;
            if (i == _gapStart)
                i = _gapEnd;

            T[] items = _items;
            if ((uint)i < (uint)items.Length)
            {
                _current = items[i];
                _index = i + 1;
                return true;
            }

            _current = default!;
            return false;
        }

        /// <summary>Resets the enumerator to its initial position, before the first element.</summary>
        /// <exception cref="InvalidOperationException">The buffer was modified since the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _buffer._version)
                ThrowVersionChanged();

            _index = 0;
            _current = default!;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public readonly void Dispose() { }

        // Static, so the throw path does not take the enumerator's address and stop the JIT promoting its
        // fields to registers.
        private static void ThrowVersionChanged() =>
            throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
    }
}
