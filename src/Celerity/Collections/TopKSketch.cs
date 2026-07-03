using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A space-bounded <em>top-k / heavy-hitters</em> sketch — it reports the most frequent
/// elements of a stream — parameterized on a custom <see cref="IHashProvider{T}"/>
/// implementation.
/// </summary>
/// <remarks>
/// <para>
/// A top-k sketch answers "which elements occur most often, and roughly how often?" using a
/// fixed amount of memory that does <strong>not</strong> grow with the number of distinct
/// elements. Where a <see cref="Dictionary{TKey, TValue}"/> frequency table must store every
/// distinct key to later sort out the heaviest — memory proportional to the distinct count —
/// this sketch keeps only a fixed <see cref="Capacity"/> of <em>monitors</em>
/// (element / count / error triples), so it counts a stream of any cardinality in
/// <c>O(Capacity)</c> space. That is the win when the distinct count is large and only the
/// heaviest hitters matter: log analytics (top URLs / IPs), network flow monitoring, trending
/// items, database query hot-spots.
/// </para>
/// <para>
/// It implements the <strong>Space-Saving</strong> algorithm (Metwally, Agrawal &amp; El
/// Abbadi, 2005). Each observed element that is already monitored has its counter incremented.
/// An unmonitored element, while fewer than <see cref="Capacity"/> monitors are in use, takes a
/// fresh monitor with error <c>0</c>. Once all monitors are in use, the element occupying the
/// monitor with the <em>smallest</em> count is evicted: the new element reuses that monitor,
/// inheriting the evicted count as its <see cref="TopKEntry{T}.Error"/> and setting its count to
/// that minimum plus the new occurrences. This "give the newcomer the current minimum" rule is
/// what bounds the error and yields the guarantees below.
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>No false negatives among true heavy hitters.</b> Any element whose true frequency exceeds
/// <c><see cref="TotalCount"/> / <see cref="Capacity"/></c> is guaranteed to still be monitored,
/// so a large enough <see cref="Capacity"/> cannot miss a genuine heavy hitter.
/// </description></item>
/// <item><description>
/// <b>Bounded, one-sided error.</b> A monitor's <see cref="TopKEntry{T}.Count"/> never
/// underestimates its element's true frequency, and overestimates it by at most the monitor's
/// <see cref="TopKEntry{T}.Error"/> — so the truth lies in <c>[Count − Error, Count]</c>.
/// </description></item>
/// </list>
/// <para>
/// The monitors are held in an indexed binary min-heap keyed on count (the minimum, the next
/// eviction victim, sits at the root), and an element→monitor index dogfoods
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/> — which is where <typeparamref name="THasher"/>
/// is used, and which also supplies out-of-band handling for a <c>default(T)</c> or <c>null</c>
/// element (so a string hasher is never invoked with <c>null</c>, matching the rest of the
/// family). Both a repeat observation and an eviction cost <c>O(log Capacity)</c>.
/// </para>
/// <para>
/// The sketch is add-and-query only. Like a Bloom filter it has no <c>Remove</c> (decrementing a
/// monitor would break the never-underestimate guarantee), and unlike <see cref="CountMinSketch{T, THasher}"/>
/// or <see cref="HyperLogLog{T, THasher}"/> it has no <c>UnionWith</c>: two bounded top-k
/// summaries cannot be merged into the exact top-k of the combined stream without error beyond
/// each summary's own, so no merge is offered rather than a lossy one that violates the type's
/// bounds. Use <see cref="Clear"/> to reset.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements whose frequencies are tracked.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class TopKSketch<T, THasher> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default number of monitors (<c>k</c>) used when a constructor does not specify a
    /// capacity: 128.
    /// </summary>
    public const int DEFAULT_CAPACITY = 128;

    private readonly int _capacity;
    private readonly T?[] _elements;   // heap-ordered monitor elements
    private readonly long[] _counts;   // heap key: monitor counts (min at index 0)
    private readonly long[] _errors;   // per-monitor overestimate bound
    private readonly CelerityDictionary<T, int, THasher> _index; // element -> heap position

    private int _size;                 // number of monitors currently in use (<= _capacity)
    private long _totalCount;          // total occurrences observed

    /// <summary>
    /// Initializes a new, empty <see cref="TopKSketch{T,THasher}"/> that monitors up to
    /// <paramref name="capacity"/> elements.
    /// </summary>
    /// <param name="capacity">
    /// The number of monitors (<c>k</c>) to keep — the maximum number of distinct elements
    /// tracked at once, and the space bound. A larger capacity tracks more candidates and
    /// tightens the guarantees, at proportional memory. Must be at least 1.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    public TopKSketch(int capacity = DEFAULT_CAPACITY)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");

        _capacity = capacity;
        _elements = new T?[capacity];
        _counts = new long[capacity];
        _errors = new long[capacity];
        _index = new CelerityDictionary<T, int, THasher>(capacity);
        // Pre-size the element index for the full monitor set so filling it never rehashes.
        _index.EnsureCapacity(capacity);
    }

    /// <summary>
    /// Initializes a new <see cref="TopKSketch{T,THasher}"/> with the specified
    /// <paramref name="capacity"/> and pre-populated by adding each element of
    /// <paramref name="source"/> once.
    /// </summary>
    /// <param name="source">
    /// The elements to add. Each occurrence counts once, so duplicates in the source raise the
    /// tracked frequency.
    /// </param>
    /// <param name="capacity">The number of monitors to keep; see the primary constructor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    public TopKSketch(IEnumerable<T> source, int capacity = DEFAULT_CAPACITY)
        : this(NullChecked(source, capacity))
    {
        foreach (T item in source)
            Add(item);
    }

    // Runs as part of the chained-ctor argument expression so the null check beats the primary
    // ctor's capacity validation: a null source must surface as ArgumentNullException, not
    // ArgumentOutOfRangeException, even when the caller also passed an invalid capacity.
    private static int NullChecked(IEnumerable<T> source, int capacity)
    {
        ArgumentNullException.ThrowIfNull(source);
        return capacity;
    }

    /// <summary>
    /// Gets the number of monitors the sketch keeps — the maximum number of distinct elements
    /// tracked at once (the <c>k</c> in top-k).
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the number of elements currently monitored, between <c>0</c> and
    /// <see cref="Capacity"/>.
    /// </summary>
    public int Count => _size;

    /// <summary>
    /// Gets the total number of occurrences observed since construction or the last
    /// <see cref="Clear"/> (the stream length; the <c>N</c> the heavy-hitter guarantee's
    /// <c>N / Capacity</c> threshold is relative to).
    /// </summary>
    public long TotalCount => _totalCount;

    /// <summary>
    /// Records one occurrence of an element.
    /// </summary>
    /// <param name="item">The element observed.</param>
    public void Add(T item) => Add(item, 1);

    /// <summary>
    /// Records <paramref name="count"/> occurrences of an element.
    /// </summary>
    /// <param name="item">The element observed.</param>
    /// <param name="count">The number of occurrences to record. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    /// <remarks>
    /// A monitor count (and <see cref="TotalCount"/>) that would exceed <see cref="long.MaxValue"/>
    /// saturates there rather than wrapping negative, so the never-underestimate guarantee holds
    /// even under counts no in-memory sketch could otherwise represent.
    /// </remarks>
    public void Add(T item, long count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be positive.");

        _totalCount = SaturatingAdd(_totalCount, count);

        // Already monitored: bump its counter. A larger count can only sink toward the leaves of
        // a min-heap, never rise, so a single sift-down restores the heap.
        if (_index.TryGetValue(item, out int pos))
        {
            _counts[pos] = SaturatingAdd(_counts[pos], count);
            SiftDown(pos);
            return;
        }

        // Free monitor available: give the newcomer a fresh one with zero error.
        if (_size < _capacity)
        {
            int fresh = _size++;
            _elements[fresh] = item;
            _counts[fresh] = count;
            _errors[fresh] = 0;
            _index[item] = fresh;
            SiftUp(fresh);
            return;
        }

        // All monitors in use: evict the minimum (the heap root) and hand its slot to the
        // newcomer, which inherits the evicted count as its error floor.
        long minCount = _counts[0];
        _index.Remove(_elements[0]!);
        _elements[0] = item;
        _errors[0] = minCount;
        _counts[0] = SaturatingAdd(minCount, count);
        _index[item] = 0;
        SiftDown(0);
    }

    /// <summary>
    /// Attempts to read the tracked count and error for an element.
    /// </summary>
    /// <param name="item">The element to look up.</param>
    /// <param name="count">
    /// When the method returns <c>true</c>, the element's estimated occurrence count (an upper
    /// bound on its true frequency); otherwise <c>0</c>.
    /// </param>
    /// <param name="error">
    /// When the method returns <c>true</c>, the maximum amount <paramref name="count"/>
    /// overestimates the truth (the true frequency is at least <c>count − error</c>); otherwise
    /// <c>0</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if <paramref name="item"/> is currently monitored; <c>false</c> if it is not —
    /// in which case its true frequency is at most the smallest monitored count.
    /// </returns>
    public bool TryGetCount(T item, out long count, out long error)
    {
        if (_index.TryGetValue(item, out int pos))
        {
            count = _counts[pos];
            error = _errors[pos];
            return true;
        }

        count = 0;
        error = 0;
        return false;
    }

    /// <summary>
    /// Returns every currently monitored element, ordered by estimated count descending.
    /// </summary>
    /// <returns>
    /// An array of at most <see cref="Capacity"/> entries, most frequent first.
    /// </returns>
    public TopKEntry<T>[] GetTopK() => GetTopK(_size);

    /// <summary>
    /// Returns the <paramref name="count"/> most frequent monitored elements, ordered by
    /// estimated count descending.
    /// </summary>
    /// <param name="count">
    /// The maximum number of entries to return. Values greater than <see cref="Count"/> return
    /// all monitored elements; <c>0</c> returns an empty array.
    /// </param>
    /// <returns>The top <paramref name="count"/> entries, most frequent first.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public TopKEntry<T>[] GetTopK(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be non-negative.");

        int take = Math.Min(count, _size);
        if (take == 0)
            return Array.Empty<TopKEntry<T>>();

        var all = new TopKEntry<T>[_size];
        for (int i = 0; i < _size; i++)
            all[i] = new TopKEntry<T>(_elements[i]!, _counts[i], _errors[i]);

        // Sort by count descending; the heap is only min-ordered, so a full sort is needed to
        // rank the monitors. This is a query-time cost, not on the Add hot path.
        Array.Sort(all, static (x, y) => y.Count.CompareTo(x.Count));

        if (take == _size)
            return all;

        var result = new TopKEntry<T>[take];
        Array.Copy(all, result, take);
        return result;
    }

    /// <summary>
    /// Resets the sketch to empty, discarding every monitor. The capacity is preserved.
    /// </summary>
    public void Clear()
    {
        if (_size == 0 && _totalCount == 0)
            return;

        Array.Clear(_elements, 0, _size);
        _size = 0;
        _totalCount = 0;
        _index.Clear();
    }

    // Restores the min-heap after the monitor at index i had its count increased (or a new
    // monitor with a possibly-large count was appended): the element can only move toward the
    // leaves.
    private void SiftDown(int i)
    {
        int n = _size;
        while (true)
        {
            int left = 2 * i + 1;
            int right = left + 1;
            int smallest = i;
            if (left < n && _counts[left] < _counts[smallest])
                smallest = left;
            if (right < n && _counts[right] < _counts[smallest])
                smallest = right;
            if (smallest == i)
                break;
            Swap(i, smallest);
            i = smallest;
        }
    }

    // Restores the min-heap after a fresh monitor was appended at index i: a small count bubbles
    // up toward the root.
    private void SiftUp(int i)
    {
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (_counts[parent] <= _counts[i])
                break;
            Swap(i, parent);
            i = parent;
        }
    }

    // Swaps two monitors and keeps the element->position index in sync for both. Centralizing
    // every positional move here is what keeps _index consistent with the heap at all times.
    private void Swap(int a, int b)
    {
        (_elements[a], _elements[b]) = (_elements[b], _elements[a]);
        (_counts[a], _counts[b]) = (_counts[b], _counts[a]);
        (_errors[a], _errors[b]) = (_errors[b], _errors[a]);
        _index[_elements[a]!] = a;
        _index[_elements[b]!] = b;
    }

    // Adds two non-negative longs, clamping to long.MaxValue instead of wrapping past it. Counts
    // and _totalCount are only ever increased by validated-positive amounts, so a sum that comes
    // back negative can only be an overflow — clamping there keeps the estimate at or above the
    // truth, preserving the never-underestimate guarantee.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SaturatingAdd(long a, long b)
    {
        long sum = unchecked(a + b);
        return sum < 0 ? long.MaxValue : sum;
    }
}
