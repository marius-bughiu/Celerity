using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable</b> sorted map from integer keys to values that answers exact, floor ("the value as of
/// <c>t</c>"), ceiling and range lookups over the same static SIMD search tree as
/// <see cref="FrozenSortedSet{T}"/>, several times faster than a binary search over a sorted key array.
/// </summary>
/// <remarks>
/// <para>
/// <b>Build once, query many.</b> The entries are sorted by key at construction and there is no mutating member;
/// a changed map means building a new one, which costs a sort. Use it for a key-to-value table that is read far
/// more often than it changes: range partitions to their shard, effective dates to a price or a schema version,
/// IP or account-number ranges to their owner, a sorted dimension table.
/// </para>
/// <para>
/// The BCL has no ordered frozen map. <c>FrozenDictionary&lt;TKey, TValue&gt;</c> is unordered, so "the entry at
/// or below <c>x</c>" is a full scan, and <see cref="SortedList{TKey, TValue}"/> has no floor or ceiling member
/// at all. The usual hand-roll is a sorted <c>TKey[]</c> and a parallel <c>TValue[]</c>, sorted together with
/// <c>Array.Sort(keys, values)</c> and searched with <c>MemoryExtensions.BinarySearch</c>. That search is bound
/// by latency rather than work: <c>log2(n)</c> dependent loads, each a likely cache miss once the keys outgrow
/// L1. This map keeps the same two parallel arrays, but lays the keys out as <see cref="FrozenSortedSet{T}"/>'s
/// static B+ tree of cache-line nodes, so a search reads about five nodes for a million keys instead of making
/// twenty dependent steps. The position a search finds indexes the value array directly.
/// </para>
/// <para>
/// <b>When not to use it.</b> If every lookup is an exact key and order never matters, a hash lookup wins:
/// <c>FrozenDictionary&lt;TKey, TValue&gt;</c> answers an exact key with one hash and one or two loads, which no
/// search tree can match. This type's advantage is the ordered queries (<see cref="TryGetFloor"/>,
/// <see cref="TryGetLowerBound"/>, <see cref="CountLessThan"/>, <see cref="GetIndexRange"/>), which a hash map
/// cannot answer.
/// </para>
/// <para>
/// <see cref="Keys"/> is the map's own <see cref="FrozenSortedSet{T}"/>, so its rank, successor, range and
/// set-algebra members apply to the map's keys without a copy, and a position in it is a position in
/// <see cref="Values"/>. The type holds no mutable state after construction, so instances are safe to share
/// across threads (as long as the values themselves are).
/// </para>
/// </remarks>
/// <typeparam name="TKey">
/// The key type: an unmanaged binary integer such as <see cref="int"/>, <see cref="long"/>, <see cref="uint"/>,
/// <see cref="ulong"/>, <see cref="short"/> or <see cref="byte"/>.
/// </typeparam>
/// <typeparam name="TValue">The value type. Any type, including a reference type or <c>null</c> values.</typeparam>
/// <example>
/// <code>
/// // Range partitions: each entry is the first key a shard owns.
/// var shards = new FrozenSortedMap&lt;long, string&gt;(new Dictionary&lt;long, string&gt;
/// {
///     [long.MinValue] = "shard-a",
///     [1_000_000] = "shard-b",
///     [5_000_000] = "shard-c",
/// });
///
/// shards.TryGetFloor(3_200_000, out var owner);   // owner = { 1000000, "shard-b" }
///
/// // Every entry with a key in [from, to), as two spans over the map's own storage.
/// Range r = shards.GetIndexRange(0, 5_000_001);
/// ReadOnlySpan&lt;long&gt; keys = shards.Keys.AsSpan()[r];
/// ReadOnlySpan&lt;string&gt; values = shards.Values[r];
/// </code>
/// </example>
public sealed class FrozenSortedMap<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : unmanaged, IBinaryInteger<TKey>, IMinMaxValue<TKey>
{
    private readonly FrozenSortedSet<TKey> _keySet;
    private readonly TKey[] _keys;      // _keySet's padded leaves, for unchecked reads at a found position
    private readonly TValue[] _values;  // exactly Count values, in key order
    private readonly int _count;

    /// <summary>
    /// Builds the map from <paramref name="source"/>, whose entries may be in any order.
    /// </summary>
    /// <param name="source">
    /// The entries. They are copied, so the caller's collection may be reused. Any <see cref="ICollection{T}"/>
    /// (including a <see cref="Dictionary{TKey, TValue}"/> or an array) is sized from its <c>Count</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> holds two entries with the same key, or more entries than one padded key array
    /// can hold (a little under <see cref="Array.MaxLength"/>). A counted source is checked for size before
    /// anything is allocated.
    /// </exception>
    /// <remarks>
    /// Building costs one sort of the keys with their values alongside, <c>O(n log n)</c>, then one pass to
    /// reject duplicates and one over the search tree's internal levels, which are about <c>n / B</c> keys.
    /// A duplicate key is an error, as it is for <see cref="Dictionary{TKey, TValue}"/> and
    /// <see cref="SortedList{TKey, TValue}"/>, rather than a silent choice of one of the values.
    /// </remarks>
    public FrozenSortedMap(IEnumerable<KeyValuePair<TKey, TValue>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        KeyValuePair<TKey, TValue>[] pairs;
        if (source is ICollection<KeyValuePair<TKey, TValue>> collection)
        {
            ThrowIfTooLong(collection.Count);
            pairs = new KeyValuePair<TKey, TValue>[collection.Count];
            collection.CopyTo(pairs, 0);
        }
        else
        {
            pairs = source.ToArray();
            ThrowIfTooLong(pairs.Length);
        }

        int count = pairs.Length;
        var keys = new TKey[FrozenSortedSet<TKey>.PaddedLength(count)];
        var values = new TValue[count];
        for (int i = 0; i < count; i++)
        {
            keys[i] = pairs[i].Key;
            values[i] = pairs[i].Value;
        }

        keys.AsSpan(0, count).Sort(values.AsSpan());
        for (int i = 1; i < count; i++)
        {
            if (keys[i] == keys[i - 1])
                throw new ArgumentException($"An item with the same key has already been added. Key: {keys[i]}", nameof(source));
        }

        keys.AsSpan(count).Fill(TKey.MaxValue);

        _keySet = new FrozenSortedSet<TKey>(keys, count);
        _keys = keys;
        _values = values;
        _count = count;
    }

    /// <summary>Gets the number of entries in the map.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets the keys as the map's own <see cref="FrozenSortedSet{T}"/>, without a copy. Its positions are the
    /// map's positions: the key at <c>Keys[i]</c> maps to <c>Values[i]</c>.
    /// </summary>
    public FrozenSortedSet<TKey> Keys => _keySet;

    /// <summary>Gets the values in ascending key order, as a read-only span over the map's storage.</summary>
    public ReadOnlySpan<TValue> Values => _values;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _keySet;

    // Wrapped, so a caller cannot cast the interface view back to the backing array and write through it.
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Array.AsReadOnly(_values);

    /// <summary>Gets the entry with the smallest key.</summary>
    /// <exception cref="InvalidOperationException">The map is empty.</exception>
    public KeyValuePair<TKey, TValue> Min =>
        _count != 0 ? Entry(0) : throw new InvalidOperationException("The map is empty.");

    /// <summary>Gets the entry with the largest key.</summary>
    /// <exception cref="InvalidOperationException">The map is empty.</exception>
    public KeyValuePair<TKey, TValue> Max =>
        _count != 0 ? Entry(_count - 1) : throw new InvalidOperationException("The map is empty.");

    /// <summary>Gets the value mapped to <paramref name="key"/>.</summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value mapped to the key.</returns>
    /// <exception cref="KeyNotFoundException">The map has no entry for <paramref name="key"/>.</exception>
    public TValue this[TKey key]
    {
        get
        {
            int index = _keySet.LowerBound(key);
            if (index < _count && _keys[index] == key)
                return _values[index];

            throw new KeyNotFoundException($"The given key '{key}' was not present in the map.");
        }
    }

    /// <summary>Determines whether the map has an entry for <paramref name="key"/>.</summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is present.</returns>
    /// <remarks>One node read per level of the key tree, then one compare.</remarks>
    public bool ContainsKey(TKey key)
    {
        int index = _keySet.LowerBound(key);
        return index < _count && _keys[index] == key;
    }

    /// <summary>Gets the value mapped to <paramref name="key"/>, if there is one.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The mapped value, or <c>default</c> when the key is absent.</param>
    /// <returns><c>true</c> if the key is present.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        int index = _keySet.LowerBound(key);
        if (index < _count && _keys[index] == key)
        {
            value = _values[index];
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Determines whether any entry maps to <paramref name="value"/>, compared with
    /// <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    /// <param name="value">The value to locate; may be <c>null</c> for a reference type.</param>
    /// <returns><c>true</c> if some key maps to the value.</returns>
    /// <remarks>
    /// A linear scan of the value array, <c>O(n)</c>, as on <see cref="Dictionary{TKey, TValue}"/>: the values
    /// are in key order, not value order.
    /// </remarks>
    public bool ContainsValue(TValue value) => Array.IndexOf(_values, value) >= 0;

    /// <summary>
    /// Returns the position of <paramref name="key"/> in ascending key order, or <c>-1</c> when the map has no
    /// entry for it. Named after <see cref="SortedList{TKey, TValue}.IndexOfKey"/>.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>The zero-based position of the key, or <c>-1</c>.</returns>
    public int IndexOfKey(TKey key) => _keySet.IndexOf(key);

    /// <summary>Gets the key at the specified position in ascending key order.</summary>
    /// <param name="index">The zero-based position, over <c>[0, Count)</c>.</param>
    /// <returns>The <paramref name="index"/>-th smallest key.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    public TKey GetKeyAtIndex(int index) => _keySet[index];

    /// <summary>Gets the value whose key is at the specified position in ascending key order.</summary>
    /// <param name="index">The zero-based position, over <c>[0, Count)</c>.</param>
    /// <returns>The value mapped to the <paramref name="index"/>-th smallest key.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    public TValue GetValueAtIndex(int index)
    {
        if ((uint)index >= (uint)_count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, Count).");

        return _values[index];
    }

    /// <summary>
    /// Returns how many keys are strictly less than <paramref name="key"/>, which is also the position of the
    /// first entry at or above it.
    /// </summary>
    /// <param name="key">The bound. Any value of <typeparamref name="TKey"/> is accepted.</param>
    /// <returns>A count in <c>[0, Count]</c>; <see cref="Count"/> when every key is smaller.</returns>
    public int CountLessThan(TKey key) => _keySet.LowerBound(key);

    /// <summary>
    /// Finds the entry with the largest key <b>less than or equal to</b> <paramref name="key"/>: the <i>floor</i>
    /// of <paramref name="key"/>. An exact match is its own floor. This is the "value as of <i>t</i>" query over
    /// a map keyed by time or version, and the "which range owns <i>k</i>" query over a map keyed by each range's
    /// first key.
    /// </summary>
    /// <param name="key">The key to bound.</param>
    /// <param name="entry">The bounding entry, or <c>default</c> when every key is larger.</param>
    /// <returns><c>true</c> if such an entry exists.</returns>
    /// <remarks>One search, the cost of <see cref="TryGetValue"/>.</remarks>
    public bool TryGetFloor(TKey key, out KeyValuePair<TKey, TValue> entry)
    {
        int index = _keySet.LowerBound(key);
        if (index == _count || _keys[index] != key)
            index--;

        return TryGetAt(index, out entry);
    }

    /// <summary>
    /// Finds the entry with the largest key <b>strictly less than</b> <paramref name="key"/>: its predecessor.
    /// </summary>
    /// <param name="key">The key to bound.</param>
    /// <param name="entry">The bounding entry, or <c>default</c> when no key is smaller.</param>
    /// <returns><c>true</c> if such an entry exists.</returns>
    public bool TryGetPredecessor(TKey key, out KeyValuePair<TKey, TValue> entry) =>
        TryGetAt(_keySet.LowerBound(key) - 1, out entry);

    /// <summary>
    /// Finds the entry with the smallest key <b>greater than or equal to</b> <paramref name="key"/>: the
    /// <c>lower_bound</c> of the ordered containers, or the <i>ceiling</i> of <paramref name="key"/>. An exact
    /// match is its own lower bound.
    /// </summary>
    /// <param name="key">The key to bound.</param>
    /// <param name="entry">The bounding entry, or <c>default</c> when every key is smaller.</param>
    /// <returns><c>true</c> if such an entry exists.</returns>
    public bool TryGetLowerBound(TKey key, out KeyValuePair<TKey, TValue> entry) =>
        TryGetAt(_keySet.LowerBound(key), out entry);

    /// <summary>
    /// Finds the entry with the smallest key <b>strictly greater than</b> <paramref name="key"/>: the
    /// <c>upper_bound</c> of the ordered containers.
    /// </summary>
    /// <param name="key">The key to bound.</param>
    /// <param name="entry">The bounding entry, or <c>default</c> when no key is larger.</param>
    /// <returns><c>true</c> if such an entry exists.</returns>
    public bool TryGetUpperBound(TKey key, out KeyValuePair<TKey, TValue> entry)
    {
        // Nothing is above TKey.MaxValue, and key + 1 would wrap to TKey.MinValue.
        if (key == TKey.MaxValue)
        {
            entry = default;
            return false;
        }

        return TryGetAt(_keySet.LowerBound(key + TKey.One), out entry);
    }

    /// <summary>
    /// Returns the positions of the entries whose keys lie in the half-open range
    /// <c>[fromInclusive, toExclusive)</c>, as a <see cref="Range"/> that slices both <c>Keys.AsSpan()</c> and
    /// <see cref="Values"/>.
    /// </summary>
    /// <param name="fromInclusive">The inclusive lower bound of the range.</param>
    /// <param name="toExclusive">The exclusive upper bound of the range.</param>
    /// <returns>
    /// The positions <c>[CountLessThan(fromInclusive), CountLessThan(toExclusive))</c>; empty when no key falls in
    /// the range.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="toExclusive"/> is less than <paramref name="fromInclusive"/>.
    /// </exception>
    /// <remarks>
    /// Two searches and no copy, so the cost does not depend on how many entries the range holds. A range that
    /// should include <c>TKey.MaxValue</c> cannot be written half-open; take
    /// <c>CountLessThan(fromInclusive)..</c> instead.
    /// </remarks>
    public Range GetIndexRange(TKey fromInclusive, TKey toExclusive)
    {
        if (toExclusive < fromInclusive)
            throw new ArgumentException("toExclusive must not be less than fromInclusive.", nameof(toExclusive));

        return new Range(_keySet.LowerBound(fromInclusive), _keySet.LowerBound(toExclusive));
    }

    /// <summary>Returns an enumerator over the entries in ascending key order, without allocating.</summary>
    /// <returns>A struct enumerator over the entries.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() =>
        GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private KeyValuePair<TKey, TValue> Entry(int index) => new(_keys[index], _values[index]);

    // The entry at `index`, which a search may have placed one before the first entry or one past the last.
    private bool TryGetAt(int index, out KeyValuePair<TKey, TValue> entry)
    {
        if ((uint)index >= (uint)_count)
        {
            entry = default;
            return false;
        }

        entry = Entry(index);
        return true;
    }

    private static void ThrowIfTooLong(int length)
    {
        if (length > FrozenSortedSet<TKey>.MaxCount)
        {
            throw new ArgumentException(
                $"A FrozenSortedMap<{typeof(TKey).Name}, {typeof(TValue).Name}> holds at most {FrozenSortedSet<TKey>.MaxCount} entries.",
                "source");
        }
    }

    /// <summary>
    /// Enumerates the entries in ascending key order without allocating. The map is immutable, so there is no
    /// version to check.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly TKey[] _keys;
        private readonly TValue[] _values;
        private readonly int _count;
        private int _index;
        private KeyValuePair<TKey, TValue> _current;

        internal Enumerator(FrozenSortedMap<TKey, TValue> map)
        {
            _keys = map._keys;
            _values = map._values;
            _count = map._count;
            _index = -1;
            _current = default;
        }

        /// <summary>Gets the entry at the current position.</summary>
        public readonly KeyValuePair<TKey, TValue> Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances to the next entry.</summary>
        /// <returns><c>true</c> while an entry remains; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _count)
            {
                _index = next;
                _current = new KeyValuePair<TKey, TValue>(_keys[next], _values[next]);
                return true;
            }

            _index = _count;
            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first entry.</summary>
        public void Reset()
        {
            _index = -1;
            _current = default;
        }

        /// <summary>Does nothing; the enumerator holds no resources.</summary>
        public readonly void Dispose()
        {
        }
    }
}
