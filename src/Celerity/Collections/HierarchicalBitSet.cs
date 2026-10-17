using System.Collections;
using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// An <b>ordered set of non-negative integers over a bounded universe</b> <c>[0, Universe)</c>, stored as a
/// bit vector with a 64-ary tree of summary words above it. Membership is one bit test, and the ordered
/// queries — <see cref="TryGetMin"/>, <see cref="TryGetMax"/>, <see cref="TryGetLowerBound"/>,
/// <see cref="TryGetUpperBound"/>, <see cref="TryGetFloor"/> and <see cref="TryGetPredecessor"/> — read at
/// most two words per tree level, so they cost <c>O(log₆₄ Universe)</c>: at most four levels for a
/// universe of 16.7 million, and six for the largest one an <see cref="int"/> allows.
/// </summary>
/// <remarks>
/// <para>
/// <b>Layout.</b> Level 0 is a plain bit vector with one bit per storable value. Each level above it holds
/// one bit per word of the level below, set exactly when that word is non-zero, and the levels stop at a
/// single word. A successor query masks the value's own leaf word; if nothing remains, it climbs to the
/// first level that has a set bit to the right, then descends through one trailing-zero count per level.
/// <see cref="Add(int)"/> and <see cref="Remove(int)"/> touch a summary level only when a word turns
/// non-zero or zero, so most updates write a single word; the worst case, a value alone in its word at every
/// level, writes one word per level, the same <c>O(log₆₄ Universe)</c> as a query. The summaries add about one sixty-third to
/// the bit vector, so the whole structure is close to <c>Universe / 8</c> bytes whatever <see cref="Count"/>
/// is.
/// </para>
/// <para>
/// <b>The documented BCL-beating workload</b> is an ordered set of integers from a known range under a mix
/// of updates and "next / previous present value" queries: the lowest free id or port or slot, the next
/// occupied timer bucket or calendar slot, the next live row in a scan, and a monotone integer priority
/// queue that needs <c>Remove</c>. The BCL's answer is <see cref="SortedSet{T}"/>, a red-black tree with one
/// heap object per element whose successor query goes through <c>GetViewBetween</c>. The library's
/// <see cref="BTreeSet{T, TComparer}"/> serves an unbounded key space well, but it still searches
/// <c>log₃₂(n)</c> nodes where this type reads a few words.
/// </para>
/// <para>
/// <b>Tradeoffs.</b> Memory is proportional to <see cref="Universe"/>, not to <see cref="Count"/>, so a few
/// values spread over a large range are better served by <see cref="BTreeSet{T, TComparer}"/> or
/// <see cref="CompressedIntSet"/>. <see cref="Clear"/> zeroes the bit vector, which is
/// <c>O(Universe / 64)</c>. When order is not part of the question, <see cref="SparseSet"/> has an
/// <c>O(1)</c> <see cref="SparseSet.Clear"/> and enumerates only the present values. A value outside
/// <c>[0, Universe)</c> is rejected by <see cref="Add(int)"/> / <see cref="TryAdd(int)"/> with
/// <see cref="ArgumentOutOfRangeException"/>, and reported as absent by <see cref="Contains(int)"/> /
/// <see cref="Remove(int)"/>. The ordered queries accept any <see cref="int"/> and answer as though the set
/// were a sorted set of its members, so <c>TryGetLowerBound(-5)</c> returns the minimum.
/// </para>
/// <para>
/// It implements the full <see cref="ISet{T}"/> surface with BCL <see cref="HashSet{T}"/> semantics through
/// the shared set-algebra helper, with <see cref="SparseSet"/>'s caveat: a mutating operation that would
/// <em>add</em> an out-of-universe value throws <see cref="ArgumentOutOfRangeException"/>. Enumeration is
/// in ascending order. The type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public class HierarchicalBitSet : ISet<int>, IReadOnlySet<int>
{
    // log2 of the bits in a word, and the in-word index mask. Every level packs 64 children per word.
    private const int WordShift = 6;
    private const int WordMask = 63;

    // All levels in one array, leaf level first. Level L occupies [_levelOffsets[L], _levelOffsets[L + 1]),
    // so _levelOffsets has one more entry than there are levels and its last entry is _words.Length. The
    // leaf level starts at 0, which is what lets Contains / Add / Remove index _words directly.
    private readonly ulong[] _words;
    private readonly int[] _levelOffsets;
    private readonly int _levelCount;
    private readonly int _universe;
    private int _count;

    // Incremented on every structural mutation so active enumerators can detect concurrent
    // modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new, empty <see cref="HierarchicalBitSet"/> whose storable values are the integers in
    /// <c>[0, universe)</c>.
    /// </summary>
    /// <param name="universe">
    /// The exclusive upper bound of storable values. The bit vector and its summaries are allocated once,
    /// at about <c>universe / 8</c> bytes, so choose it to match the actual value range. A value of
    /// <c>0</c> creates a set that can store nothing.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="universe"/> is negative.</exception>
    public HierarchicalBitSet(int universe)
    {
        if (universe < 0)
            throw new ArgumentOutOfRangeException(nameof(universe), universe, "Universe must be non-negative.");

        _universe = universe;
        if (universe == 0)
        {
            _words = Array.Empty<ulong>();
            _levelOffsets = new int[1];
            return;
        }

        // Level sizes shrink by 64 each step until one word covers the level below. An int universe needs
        // at most six levels (64^5 < 2^31 <= 64^6), so the sizes fit a stack buffer.
        Span<int> sizes = stackalloc int[8];
        int levels = 0;
        int total = 0;
        long bits = universe;
        while (true)
        {
            int words = (int)((bits + WordMask) >> WordShift);
            sizes[levels++] = words;
            total += words;
            if (words == 1)
                break;

            bits = words;
        }

        _levelCount = levels;
        _levelOffsets = new int[levels + 1];
        for (int level = 0; level < levels; level++)
            _levelOffsets[level + 1] = _levelOffsets[level] + sizes[level];

        _words = new ulong[total];
    }

    /// <summary>
    /// Initializes a new <see cref="HierarchicalBitSet"/> over <c>[0, universe)</c> that contains the
    /// distinct values copied from the specified <paramref name="source"/>.
    /// </summary>
    /// <param name="universe">The exclusive upper bound of storable values (see <see cref="HierarchicalBitSet(int)"/>).</param>
    /// <param name="source">
    /// The values to copy into the new set. Duplicates are silently deduplicated, matching BCL
    /// <see cref="HashSet{T}"/> semantics, and the order of <paramref name="source"/> does not matter.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="universe"/> is negative, or <paramref name="source"/> contains a value outside
    /// <c>[0, universe)</c>.
    /// </exception>
    public HierarchicalBitSet(int universe, IEnumerable<int> source)
        : this(NonNullUniverse(universe, source))
    {
        foreach (int item in source)
            TryAdd(item);
    }

    // Runs as part of the chained-ctor argument expression so the null check beats the primary ctor's
    // universe validation, mirroring SparseSet: a null source surfaces as ArgumentNullException even when
    // the caller also passed a negative universe.
    private static int NonNullUniverse(int universe, IEnumerable<int> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return universe;
    }

    /// <summary>
    /// Gets the exclusive upper bound of the values this set can store; every element is a non-negative
    /// integer strictly less than this.
    /// </summary>
    public int Universe => _universe;

    /// <summary>
    /// Gets the number of elements contained in the set.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Adds the specified value to the set.
    /// Throws <see cref="ArgumentException"/> if the value already exists.
    /// </summary>
    /// <param name="item">The value to add; must be in <c>[0, Universe)</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="item"/> already exists in the set.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="item"/> is outside <c>[0, Universe)</c>.</exception>
    public void Add(int item)
    {
        if (!TryAdd(item))
            throw new ArgumentException($"The element {item} already exists in the set.", nameof(item));
    }

    /// <summary>
    /// Attempts to add the specified value to the set. A summary level is written only when the value is the
    /// first one in its word, so most adds write a single word; the worst case, a value alone in its word at
    /// every level, writes one word per level, <c>O(log₆₄ Universe)</c>.
    /// </summary>
    /// <param name="item">The value to add; must be in <c>[0, Universe)</c>.</param>
    /// <returns>
    /// <c>true</c> if the value was added; <c>false</c> if it already exists (the set is unchanged).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="item"/> is outside <c>[0, Universe)</c>.</exception>
    public bool TryAdd(int item)
    {
        if ((uint)item >= (uint)_universe)
            throw new ArgumentOutOfRangeException(nameof(item), item,
                $"The value must be in [0, {_universe}).");

        ulong[] words = _words;
        int index = item >> WordShift;
        ulong bit = 1UL << item;
        ulong word = words[index];
        if ((word & bit) != 0)
            return false;

        words[index] = word | bit;
        _count++;
        _version++;

        // The word was empty, so its parent bit was clear: set it, and keep climbing while each parent
        // word was empty too.
        if (word == 0)
        {
            int[] offsets = _levelOffsets;
            for (int level = 1; level < _levelCount; level++)
            {
                bit = 1UL << index;
                index >>= WordShift;
                ref ulong parent = ref words[offsets[level] + index];
                ulong before = parent;
                parent = before | bit;
                if (before != 0)
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether the set contains the specified value, with one bit test.
    /// </summary>
    /// <param name="item">The value to locate.</param>
    /// <returns>
    /// <c>true</c> if the value is present; <c>false</c> otherwise (including for a value outside
    /// <c>[0, Universe)</c>).
    /// </returns>
    public bool Contains(int item)
        => (uint)item < (uint)_universe && (_words[item >> WordShift] & (1UL << item)) != 0;

    /// <summary>
    /// Removes the specified value from the set. A summary level is written only when the value was the last
    /// one in its word, so most removals write a single word; the worst case, a value alone in its word at
    /// every level, writes one word per level, <c>O(log₆₄ Universe)</c>.
    /// </summary>
    /// <param name="item">The value to remove.</param>
    /// <returns>
    /// <c>true</c> if the value was removed; <c>false</c> if it was not present (including for a value
    /// outside <c>[0, Universe)</c>).
    /// </returns>
    public bool Remove(int item)
    {
        if ((uint)item >= (uint)_universe)
            return false;

        ulong[] words = _words;
        int index = item >> WordShift;
        ulong bit = 1UL << item;
        ulong word = words[index];
        if ((word & bit) == 0)
            return false;

        word &= ~bit;
        words[index] = word;
        _count--;
        _version++;

        // The word just emptied, so clear its parent bit, and keep climbing while each parent empties too.
        if (word == 0)
        {
            int[] offsets = _levelOffsets;
            for (int level = 1; level < _levelCount; level++)
            {
                bit = 1UL << index;
                index >>= WordShift;
                ref ulong parent = ref words[offsets[level] + index];
                ulong after = parent & ~bit;
                parent = after;
                if (after != 0)
                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Removes all elements from the set by zeroing the bit vector and its summaries, which is
    /// <c>O(Universe / 64)</c>. A set that is already empty is left untouched.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_words);
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Gets the smallest element in <c>O(log₆₄ Universe)</c>.
    /// </summary>
    /// <param name="item">The smallest element, or <c>0</c> when the set is empty.</param>
    /// <returns><c>true</c> if the set is non-empty.</returns>
    public bool TryGetMin(out int item)
    {
        if (_count == 0)
        {
            item = 0;
            return false;
        }

        // Descend from the single top word, following the lowest set bit at each level.
        ulong[] words = _words;
        int[] offsets = _levelOffsets;
        int position = 0;
        for (int level = _levelCount - 1; level >= 0; level--)
            position = (position << WordShift) + BitOperations.TrailingZeroCount(words[offsets[level] + position]);

        item = position;
        return true;
    }

    /// <summary>
    /// Gets the largest element in <c>O(log₆₄ Universe)</c>.
    /// </summary>
    /// <param name="item">The largest element, or <c>0</c> when the set is empty.</param>
    /// <returns><c>true</c> if the set is non-empty.</returns>
    public bool TryGetMax(out int item)
    {
        if (_count == 0)
        {
            item = 0;
            return false;
        }

        ulong[] words = _words;
        int[] offsets = _levelOffsets;
        int position = 0;
        for (int level = _levelCount - 1; level >= 0; level--)
            position = (position << WordShift) + (WordMask - BitOperations.LeadingZeroCount(words[offsets[level] + position]));

        item = position;
        return true;
    }

    /// <summary>
    /// Finds the smallest element that is <b>greater than or equal to</b> <paramref name="item"/> — the
    /// <c>lower_bound</c> of the ordered containers — in <c>O(log₆₄ Universe)</c>. An exact match is its own
    /// lower bound.
    /// </summary>
    /// <param name="item">The value to bound. Any <see cref="int"/> is accepted; a negative value bounds to the minimum.</param>
    /// <param name="bound">The bounding element, or <c>0</c> when every element is smaller.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetLowerBound(int item, out int bound)
    {
        int found = NextSetBit(item < 0 ? 0 : item);
        bound = found < 0 ? 0 : found;
        return found >= 0;
    }

    /// <summary>
    /// Finds the smallest element <b>strictly greater than</b> <paramref name="item"/> — the
    /// <c>upper_bound</c> of the ordered containers — in <c>O(log₆₄ Universe)</c>.
    /// </summary>
    /// <param name="item">The value to bound. Any <see cref="int"/> is accepted.</param>
    /// <param name="bound">The bounding element, or <c>0</c> when no element is larger.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetUpperBound(int item, out int bound)
    {
        // Nothing exceeds int.MaxValue, and item + 1 would wrap to the minimum.
        if (item == int.MaxValue)
        {
            bound = 0;
            return false;
        }

        return TryGetLowerBound(item + 1, out bound);
    }

    /// <summary>
    /// Finds the largest element that is <b>less than or equal to</b> <paramref name="item"/> in
    /// <c>O(log₆₄ Universe)</c>. An exact match is its own floor.
    /// </summary>
    /// <param name="item">
    /// The value to bound. Any <see cref="int"/> is accepted; a value at or above <see cref="Universe"/>
    /// bounds to the maximum.
    /// </param>
    /// <param name="bound">The bounding element, or <c>0</c> when every element is larger.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetFloor(int item, out int bound)
    {
        int found = PreviousSetBit(item >= _universe ? _universe - 1 : item);
        bound = found < 0 ? 0 : found;
        return found >= 0;
    }

    /// <summary>
    /// Finds the largest element <b>strictly less than</b> <paramref name="item"/> in
    /// <c>O(log₆₄ Universe)</c>.
    /// </summary>
    /// <param name="item">The value to bound. Any <see cref="int"/> is accepted.</param>
    /// <param name="bound">The bounding element, or <c>0</c> when no element is smaller.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetPredecessor(int item, out int bound)
    {
        // Nothing precedes int.MinValue, and item - 1 would wrap to the maximum.
        if (item == int.MinValue)
        {
            bound = 0;
            return false;
        }

        return TryGetFloor(item - 1, out bound);
    }

    /// <summary>
    /// Enumerates, in ascending order, every element in the half-open range
    /// <c>[fromInclusive, toExclusive)</c>. The scan seeks to the first element with one successor query and
    /// then walks the bit vector a word at a time, skipping empty stretches through the summaries.
    /// </summary>
    /// <param name="fromInclusive">The inclusive lower bound of the range. Any <see cref="int"/> is accepted.</param>
    /// <param name="toExclusive">The exclusive upper bound of the range. Any <see cref="int"/> is accepted.</param>
    /// <returns>An allocation-free enumerable over the matching elements.</returns>
    /// <exception cref="ArgumentException"><paramref name="toExclusive"/> is less than <paramref name="fromInclusive"/>.</exception>
    public RangeEnumerable EnumerateRange(int fromInclusive, int toExclusive)
    {
        if (toExclusive < fromInclusive)
            throw new ArgumentException("toExclusive must not be less than fromInclusive.", nameof(toExclusive));

        return new RangeEnumerable(this, fromInclusive, toExclusive);
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields the elements in ascending order. If the set is
    /// modified during enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this, 0, _universe);

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── ISet<int> / ICollection<int> set-algebra surface ──────────────────────
    // Shared with the mutable set family via SetOperations, written once against the ISet<T> primitives
    // every set exposes; the semantics match BCL HashSet<int> exactly. A mutating op that would add an
    // out-of-universe value throws ArgumentOutOfRangeException via TryAdd — the bounded-universe caveat
    // noted on the type.

    /// <summary>
    /// Modifies the set to contain all elements that are present in itself, in
    /// <paramref name="other"/>, or in both.
    /// </summary>
    /// <param name="other">The collection to union into this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="other"/> contains a value outside <c>[0, Universe)</c>.</exception>
    public void UnionWith(IEnumerable<int> other) => SetOperations.UnionWith(this, other);

    /// <summary>
    /// Modifies the set to contain only elements that are also present in
    /// <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to intersect with this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void IntersectWith(IEnumerable<int> other) => SetOperations.IntersectWith(this, other);

    /// <summary>
    /// Removes every element in <paramref name="other"/> from the set.
    /// </summary>
    /// <param name="other">The collection of elements to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void ExceptWith(IEnumerable<int> other) => SetOperations.ExceptWith(this, other);

    /// <summary>
    /// Modifies the set to contain only elements that are present either in itself or in
    /// <paramref name="other"/>, but not both.
    /// </summary>
    /// <param name="other">The collection to apply the symmetric difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="other"/> contains a value outside <c>[0, Universe)</c> that must be added.</exception>
    public void SymmetricExceptWith(IEnumerable<int> other) => SetOperations.SymmetricExceptWith(this, other);

    /// <summary>
    /// Determines whether the set is a subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of this set is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSubsetOf(IEnumerable<int> other) => SetOperations.IsSubsetOf(this, other);

    /// <summary>
    /// Determines whether the set is a proper (strict) subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of this set is in <paramref name="other"/> and
    /// <paramref name="other"/> has at least one element this set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<int> other) => SetOperations.IsProperSubsetOf(this, other);

    /// <summary>
    /// Determines whether the set is a superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of <paramref name="other"/> is in this set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<int> other) => SetOperations.IsSupersetOf(this, other);

    /// <summary>
    /// Determines whether the set is a proper (strict) superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of <paramref name="other"/> is in this set and this set has at
    /// least one element <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSupersetOf(IEnumerable<int> other) => SetOperations.IsProperSupersetOf(this, other);

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> share at least one element.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool Overlaps(IEnumerable<int> other) => SetOperations.Overlaps(this, other);

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> contain the same distinct elements.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<int> other) => SetOperations.SetEquals(this, other);

    /// <summary>
    /// Copies the elements of the set, in ascending order, to the specified <paramref name="array"/>,
    /// starting at <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(int[] array, int arrayIndex) => SetOperations.CopyTo(this, _count, array, arrayIndex);

    // Adds the element, returning whether it was newly added (ISet<T> semantics) — the
    // non-throwing (on duplicates) counterpart of the public throw-on-duplicate Add(int).
    bool ISet<int>.Add(int item) => TryAdd(item);

    // ICollection<T>.Add must not throw on a duplicate (unlike the public Add(int)), so it maps to
    // the non-throwing TryAdd.
    void ICollection<int>.Add(int item) => TryAdd(item);

    bool ICollection<int>.IsReadOnly => false;

    // The smallest element >= from, or -1 when there is none. `from` must be non-negative; a value at or
    // past the universe has no successor.
    private int NextSetBit(int from)
    {
        if ((uint)from >= (uint)_universe)
            return -1;

        ulong[] words = _words;
        int[] offsets = _levelOffsets;
        int level = 0;
        int position = from;

        // Climb: mask off everything below `position` in its word; if nothing is left, the answer lies in
        // a later word, which is position (index + 1) one level up.
        while (true)
        {
            int index = position >> WordShift;
            if (index >= offsets[level + 1] - offsets[level])
                return -1;

            // A shift count is taken mod 64, so this keeps the bits at and above position & 63.
            ulong word = words[offsets[level] + index] & (ulong.MaxValue << position);
            if (word != 0)
            {
                position = (index << WordShift) + BitOperations.TrailingZeroCount(word);
                break;
            }

            if (++level == _levelCount)
                return -1;

            position = index + 1;
        }

        // Descend: every set summary bit has a non-empty child word, so take its lowest bit at each level.
        while (level > 0)
        {
            level--;
            position = (position << WordShift) + BitOperations.TrailingZeroCount(words[offsets[level] + position]);
        }

        return position;
    }

    // The largest element <= from, or -1 when there is none. `from` must be below the universe; a negative
    // value has no predecessor.
    private int PreviousSetBit(int from)
    {
        if (from < 0)
            return -1;

        ulong[] words = _words;
        int[] offsets = _levelOffsets;
        int level = 0;
        int position = from;

        while (true)
        {
            int index = position >> WordShift;

            // Keep the bits at and below position & 63.
            ulong word = words[offsets[level] + index] & (ulong.MaxValue >> (WordMask - (position & WordMask)));
            if (word != 0)
            {
                position = (index << WordShift) + (WordMask - BitOperations.LeadingZeroCount(word));
                break;
            }

            // Nothing lies before word 0. The top level is a single word, so this is also what ends the
            // climb there: the level count needs no check of its own.
            if (index == 0)
                return -1;

            level++;
            position = index - 1;
        }

        while (level > 0)
        {
            level--;
            position = (position << WordShift) + (WordMask - BitOperations.LeadingZeroCount(words[offsets[level] + position]));
        }

        return position;
    }

    /// <summary>
    /// An allocation-free enumerable over the elements of a <see cref="HierarchicalBitSet"/> in a half-open
    /// range, returned by <see cref="EnumerateRange"/>.
    /// </summary>
    public readonly struct RangeEnumerable : IEnumerable<int>
    {
        private readonly HierarchicalBitSet _set;
        private readonly int _from;
        private readonly int _to;

        internal RangeEnumerable(HierarchicalBitSet set, int fromInclusive, int toExclusive)
        {
            _set = set;
            _from = fromInclusive;
            _to = toExclusive;
        }

        /// <summary>
        /// Returns a struct enumerator over the range, in ascending order.
        /// </summary>
        /// <returns>A struct enumerator over the range.</returns>
        public Enumerator GetEnumerator() => new Enumerator(_set, _from, _to);

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A struct enumerator over a <see cref="HierarchicalBitSet"/>, in ascending order. It holds the
    /// remaining bits of the current leaf word, so a dense run costs one trailing-zero count per element,
    /// and it reaches the next non-empty word with a successor query that skips empty stretches through the
    /// summaries.
    /// </summary>
    public struct Enumerator : IEnumerator<int>
    {
        private readonly HierarchicalBitSet _set;
        private readonly int _version;
        private readonly int _start;
        private readonly int _end;

        // Where the next successor query starts once _bits runs out. A long, because the word after the
        // last one can start at 2^31.
        private long _next;

        // The unvisited set bits of the current leaf word, and the value of that word's bit 0.
        private ulong _bits;
        private int _wordBase;
        private int _current;

        internal Enumerator(HierarchicalBitSet set, int fromInclusive, int toExclusive)
        {
            _set = set;
            _version = set._version;
            _start = fromInclusive < 0 ? 0 : fromInclusive;
            _end = toExclusive > set._universe ? set._universe : toExclusive;
            _next = _start;
            _bits = 0;
            _wordBase = 0;
            _current = 0;
        }

        /// <summary>
        /// Gets the element at the current position of the enumerator.
        /// </summary>
        public int Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next element.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new element; <c>false</c> if it has passed the end of
        /// the set or range.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the set was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            HierarchicalBitSet set = _set;
            if (_version != set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            ulong bits = _bits;
            if (bits == 0)
            {
                if (_next >= _end)
                    return Finish();

                int found = set.NextSetBit((int)_next);
                if (found < 0 || found >= _end)
                    return Finish();

                int index = found >> WordShift;
                bits = set._words[index] & (ulong.MaxValue << found);
                _wordBase = index << WordShift;
                _next = (long)_wordBase + 64;
            }

            int value = _wordBase + BitOperations.TrailingZeroCount(bits);
            if (value >= _end)
                return Finish();

            _bits = bits & (bits - 1);
            _current = value;
            return true;
        }

        // Parks the enumerator past the end, so every later MoveNext answers false after its version check.
        private bool Finish()
        {
            _bits = 0;
            _next = _end;
            _current = 0;
            return false;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first element.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the set was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _next = _start;
            _bits = 0;
            _wordBase = 0;
            _current = 0;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }
}
