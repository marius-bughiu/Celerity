using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A build-once, read-many set of <see cref="string"/> elements using
/// <see cref="StringFnV1AHasher"/> by default. Supply a different string hasher via
/// the <see cref="FrozenCeleritySet{THasher}"/> generic overload when your elements
/// favour a different hash (e.g. <see cref="StringFnV1AFullHasher"/> for non-ASCII
/// elements, or one of the strong hashers for adversarial elements).
/// </summary>
public sealed class FrozenCeleritySet : FrozenCeleritySet<StringFnV1AHasher>
{
    /// <summary>
    /// Initializes a new <see cref="FrozenCeleritySet"/> from the specified
    /// elements, freezing the contents at construction.
    /// </summary>
    /// <param name="source">
    /// The elements to freeze. See
    /// <see cref="FrozenCeleritySet{THasher}(IEnumerable{string})"/> for the
    /// duplicate-element and <c>null</c>-element contract.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public FrozenCeleritySet(IEnumerable<string> source)
        : base(source)
    {
    }
}

/// <summary>
/// A build-once, read-many set for <see cref="string"/> elements, in the spirit of
/// the BCL <see cref="System.Collections.Frozen.FrozenSet{T}"/> but tunable through
/// Celerity's <see cref="IHashProvider{T}"/> so callers can pick a hash function
/// specialized for their element shape. It is the set counterpart of
/// <see cref="FrozenCelerityDictionary{TValue, THasher}"/>.
/// </summary>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
/// <remarks>
/// <para>
/// The set is immutable: all elements are supplied at construction and there are no
/// mutating members. In exchange the constructor searches a small parameter space
/// (table size × a mixing seed) for a <em>perfect</em> — that is, collision-free —
/// placement of the elements. When one is found (<see cref="IsPerfectlyHashed"/> is
/// then <c>true</c>) a membership test is a single hash, a single array index, and a
/// single equality check — no probing and no probe chains, the same shape as
/// <see cref="System.Collections.Frozen.FrozenSet{T}"/>.
/// </para>
/// <para>
/// A perfect placement is impossible when two distinct elements collide on the chosen
/// hasher's raw 32-bit code (for example <c>"A"</c> and <c>"Ł"</c> under the low-byte
/// <see cref="StringFnV1AHasher"/>, which returns the same code for both), because the
/// mixing seed is a pure function of that code and so cannot separate them. In that
/// case the build falls back to an open-addressed linear-probing table
/// (<see cref="IsPerfectlyHashed"/> is <c>false</c>); membership tests remain correct —
/// the equality check disambiguates the colliding elements — they simply cost a short
/// probe instead of a single index. Either way the result is always correct; supply a
/// full-width or strong hasher (<see cref="StringFnV1AFullHasher"/>,
/// <see cref="StringMurmur3Hasher"/>, …) if you want the perfect fast path for elements
/// the default collides.
/// </para>
/// <para>
/// Unlike <see cref="FrozenCelerityDictionary{TValue, THasher}"/>, which rejects
/// duplicate keys, duplicate elements are <em>silently deduplicated</em> — the
/// defining property of a set, matching BCL
/// <see cref="System.Collections.Frozen.FrozenSet{T}"/> and the mutable
/// <see cref="CeleritySet{T, THasher}"/>.
/// </para>
/// <para>
/// The <c>null</c> element is stored out-of-band (the hasher is never invoked with
/// <c>null</c>), exactly as the mutable Celerity sets handle <c>default(T)</c>, so it
/// never collides with the empty-slot sentinel. The empty string <c>""</c> is an
/// ordinary element.
/// </para>
/// </remarks>
public class FrozenCeleritySet<THasher> : IReadOnlySet<string>
    where THasher : struct, IHashProvider<string>
{
    // Number of distinct mixing seeds tried per candidate table size before giving
    // up on that size and trying the next-larger one. The search is one-time build
    // work, so a generous budget is cheap insurance for the single-probe fast path.
    private const int SEED_BUDGET = 512;

    // Largest power-of-two multiple of NextPowerOfTwo(n) the perfect-hash search will
    // grow the table to before falling back. 8 means up to 8× the minimum table size.
    private const int MAX_SIZE_MULTIPLIER = 8;

    private readonly string?[] _items;
    private readonly int _mask;
    private readonly int _seed;
    private readonly bool _isPerfect;
    private readonly THasher _hasher;

    private readonly int _count;

    // The null element collides with the empty-slot sentinel (a null array slot), so
    // it is stored out-of-band. _count includes it when _hasNull is true.
    private readonly bool _hasNull;

    /// <summary>
    /// Initializes a new <see cref="FrozenCeleritySet{THasher}"/> from the specified
    /// elements, freezing the contents at construction.
    /// </summary>
    /// <param name="source">
    /// The elements to freeze. A single <c>null</c> element is allowed and stored
    /// out-of-band. Duplicate elements (including a duplicate <c>null</c>) are silently
    /// deduplicated, matching the set semantics of BCL
    /// <see cref="System.Collections.Frozen.FrozenSet{T}"/> and
    /// <see cref="CeleritySet{T, THasher}"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> holds <c>2^30</c> or more distinct non-<c>null</c> elements,
    /// which a power-of-two frozen table cannot represent.
    /// </exception>
    public FrozenCeleritySet(IEnumerable<string> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _hasher = default;

        // Materialize and de-duplicate. The null element is split off out-of-band; the
        // rest are de-duplicated (ordinal) so the frozen table never has to.
        var itemList = new List<string>(source is ICollection<string> c ? c.Count : 0);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string item in source)
        {
            if (item is null)
            {
                _hasNull = true;
                continue;
            }

            if (seen.Add(item))
                itemList.Add(item);
        }

        int n = itemList.Count;
        _count = n + (_hasNull ? 1 : 0);

        // A frozen table is a power-of-two array indexed by a mask; the linear-probing
        // fallback additionally needs at least one empty slot to terminate a miss. Both
        // require the non-null element count to stay strictly below the 2^30 power-of-two
        // ceiling (NextPowerOfTwo caps there), so reject an impossible count up front with
        // a clear error rather than overflow the build search or hang the fallback probe.
        if (n >= FastUtils.MaxPowerOfTwoCapacity)
            throw new ArgumentException(
                $"A frozen set can hold at most {FastUtils.MaxPowerOfTwoCapacity - 1} non-null elements; got {n}.",
                nameof(source));

        // Precompute the raw hash code of every element once; the perfect-hash search
        // re-mixes these with each candidate seed rather than re-hashing the strings.
        int[] baseHashes = new int[n];
        for (int i = 0; i < n; i++)
            baseHashes[i] = _hasher.Hash(itemList[i]);

        if (TryBuildPerfect(itemList, baseHashes, out _items, out _mask, out _seed))
        {
            _isPerfect = true;
        }
        else
        {
            BuildFallback(itemList, baseHashes, out _items, out _mask);
            _seed = 0;
            _isPerfect = false;
        }
    }

    /// <summary>
    /// Gets the number of elements in the set (including the out-of-band
    /// <c>null</c> element, if present).
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets a value indicating whether the build found a collision-free (perfect)
    /// placement, so membership tests take the single-probe fast path. <c>false</c>
    /// means the set fell back to linear probing because two distinct elements share
    /// the chosen hasher's raw hash code; lookups are still correct, just not
    /// single-probe.
    /// </summary>
    public bool IsPerfectlyHashed => _isPerfect;

    /// <summary>
    /// Determines whether the set contains the specified element.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><c>true</c> if the element is found; otherwise, <c>false</c>.</returns>
    public bool Contains(string item)
    {
        if (item is null)
            return _hasNull;

        return FindSlot(item) >= 0;
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each element stored in the
    /// set. The enumeration order is unspecified and may change across versions; do
    /// not rely on it. The out-of-band <c>null</c> element (if present) is yielded
    /// first.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── IReadOnlySet<string> set-algebra members ──────────────────────────────
    // The whole frozen set is the left-hand operand; `other` is the right-hand
    // operand. Membership tests against `this` are O(1) (or a short probe), so the
    // superset / overlap shapes stream `other` directly. The subset / equality
    // shapes need the distinct count of `other`, so they materialize it once into an
    // ordinal HashSet — exactly what BCL set types do internally.

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> contain the same
    /// distinct elements.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<string> other)
    {
        HashSet<string?> o = MaterializeDistinct(other);
        if (o.Count != _count)
            return false;
        return AllElementsIn(o);
    }

    /// <summary>
    /// Determines whether the set is a subset of <paramref name="other"/> — every
    /// element of this set is also in <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of this set is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSubsetOf(IEnumerable<string> other)
    {
        HashSet<string?> o = MaterializeDistinct(other);
        if (_count > o.Count)
            return false;
        return AllElementsIn(o);
    }

    /// <summary>
    /// Determines whether the set is a proper (strict) subset of
    /// <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of this set is in <paramref name="other"/> and
    /// <paramref name="other"/> has at least one element this set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<string> other)
    {
        HashSet<string?> o = MaterializeDistinct(other);
        if (_count >= o.Count)
            return false;
        return AllElementsIn(o);
    }

    /// <summary>
    /// Determines whether the set is a superset of <paramref name="other"/> — every
    /// element of <paramref name="other"/> is also in this set.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of <paramref name="other"/> is in this set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (string item in other)
        {
            if (!Contains(item))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Determines whether the set is a proper (strict) superset of
    /// <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of <paramref name="other"/> is in this set and
    /// this set has at least one element <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSupersetOf(IEnumerable<string> other)
    {
        HashSet<string?> o = MaterializeDistinct(other);
        if (_count <= o.Count)
            return false;
        foreach (string? item in o)
        {
            if (!Contains(item!))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> share at least one
    /// element.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool Overlaps(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (string item in other)
        {
            if (Contains(item))
                return true;
        }
        return false;
    }

    // Materializes `other` into an ordinal HashSet of its distinct elements, matching
    // the EqualityComparer<string>.Default (ordinal) equality the frozen table uses.
    // HashSet tolerates a null element, so the out-of-band null is captured here too.
    private static HashSet<string?> MaterializeDistinct(IEnumerable<string> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new HashSet<string?>(other!, EqualityComparer<string?>.Default);
    }

    // Returns true iff every element of this set is contained in `o`.
    private bool AllElementsIn(HashSet<string?> o)
    {
        foreach (string item in this)
        {
            if (!o.Contains(item))
                return false;
        }
        return true;
    }

    // ── Perfect-hash construction ─────────────────────────────────────────────

    // Integer finalizer used to map an element's raw hash code (combined with the
    // search seed) to a table slot. It must be the SAME mapping at build and lookup
    // time. Based on the "lowbias32" mix; the seed is folded in so different seeds
    // produce different — but each individually well-spread — placements.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Mix(uint hash, int seed)
    {
        uint h = hash + unchecked((uint)seed * 0x9E3779B9u);
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        h *= 0x846CA68Bu;
        h ^= h >> 16;
        return h;
    }

    private bool TryBuildPerfect(
        List<string> itemList,
        int[] baseHashes,
        out string?[] items,
        out int mask,
        out int seed)
    {
        int n = itemList.Count;
        int minSize = FastUtils.NextPowerOfTwo(n == 0 ? 1 : n);

        // Probe candidate table sizes minSize, 2·minSize, … up to MAX_SIZE_MULTIPLIER·minSize.
        // Advance via the overflow-safe TryDoubleCapacity rather than a bare `size <<= 1`: once
        // a candidate reaches the 2^30 ceiling the shift would wrap negative and the next
        // `new string?[size]` would throw OverflowException instead of cleanly falling back.
        for (int size = minSize, mult = 1; mult <= MAX_SIZE_MULTIPLIER; mult <<= 1)
        {
            int m = size - 1;
            for (int s = 0; s <= SEED_BUDGET; s++)
            {
                if (TryPlace(itemList, baseHashes, size, m, s, out items))
                {
                    mask = m;
                    seed = s;
                    return true;
                }
            }

            if (!FastUtils.TryDoubleCapacity(size, out size))
                break; // reached the 2^30 table ceiling; fall back to linear probing
        }

        items = null!;
        mask = 0;
        seed = 0;
        return false;
    }

    private static bool TryPlace(
        List<string> itemList,
        int[] baseHashes,
        int size,
        int mask,
        int seed,
        out string?[] items)
    {
        var table = new string?[size];

        int n = itemList.Count;
        for (int i = 0; i < n; i++)
        {
            int slot = (int)(Mix(unchecked((uint)baseHashes[i]), seed) & (uint)mask);
            if (table[slot] is not null)
            {
                // Collision — this (size, seed) is not perfect.
                items = null!;
                return false;
            }

            table[slot] = itemList[i];
        }

        items = table;
        return true;
    }

    private static void BuildFallback(
        List<string> itemList,
        int[] baseHashes,
        out string?[] items,
        out int mask)
    {
        int n = itemList.Count;

        // Size with headroom so at least one slot stays empty — linear probing needs
        // an empty slot to terminate a miss, and to keep chains short. The constructor
        // rejects n >= 2^30, so n + 1 <= 2^30 and NextPowerOfTwo(n + 1) is a power of two
        // strictly greater than n (it never has to cap below n + 1), guaranteeing a vacant
        // slot.
        int size = FastUtils.NextPowerOfTwo(n + 1);

        int m = size - 1;
        var table = new string?[size];

        for (int i = 0; i < n; i++)
        {
            int slot = (int)(Mix(unchecked((uint)baseHashes[i]), 0) & (uint)m);
            while (table[slot] is not null)
                slot = (slot + 1) & m;

            table[slot] = itemList[i];
        }

        items = table;
        mask = m;
    }

    // Returns the slot holding <paramref name="item"/>, or -1 if absent. In perfect
    // mode this is a single index + equality check; in fallback mode it linear-probes
    // until it finds the element or hits an empty slot. The caller guarantees
    // item != null.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindSlot(string item)
    {
        string?[] items = _items;
        ref string? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        int mask = _mask;
        var comparer = EqualityComparer<string>.Default;
        int slot = (int)(Mix(unchecked((uint)_hasher.Hash(item)), _seed) & (uint)mask);

        if (_isPerfect)
        {
            string? candidate = Unsafe.Add(ref itemsRef, (nint)(uint)slot);
            return candidate is not null && comparer.Equals(candidate, item) ? slot : -1;
        }

        while (true)
        {
            string? candidate = Unsafe.Add(ref itemsRef, (nint)(uint)slot);
            if (candidate is null)
                return -1;
            if (comparer.Equals(candidate, item))
                return slot;
            slot = (slot + 1) & mask;
        }
    }

    /// <summary>
    /// A struct enumerator over a <see cref="FrozenCeleritySet{THasher}"/>. Because it
    /// is a struct, iterating it via <c>foreach</c> avoids the allocation a
    /// compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur. The out-of-band
    /// <c>null</c> element (if present) is yielded first. The set is immutable, so the
    /// enumerator needs no concurrent-modification check.
    /// </summary>
    public struct Enumerator : IEnumerator<string>
    {
        private readonly FrozenCeleritySet<THasher> _set;
        private int _index;
        private string? _current;
        private State _state;

        private enum State : byte
        {
            BeforeNull,
            InArray,
            Done
        }

        internal Enumerator(FrozenCeleritySet<THasher> set)
        {
            _set = set;
            _index = -1;
            _current = default;
            _state = State.BeforeNull;
        }

        /// <summary>
        /// Gets the element at the current position of the enumerator. This is
        /// <c>null</c> while positioned on the out-of-band <c>null</c> element.
        /// </summary>
        public string Current => _current!;

        object? IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next element.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new element; <c>false</c> if it
        /// has passed the end of the set.
        /// </returns>
        public bool MoveNext()
        {
            if (_state == State.BeforeNull)
            {
                _state = State.InArray;
                if (_set._hasNull)
                {
                    _current = null;
                    return true;
                }
            }

            if (_state == State.InArray)
            {
                string?[] items = _set._items;
                int length = items.Length;
                ref string? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
                while (++_index < length)
                {
                    string? item = Unsafe.Add(ref itemsRef, (nint)(uint)_index);
                    if (item is not null)
                    {
                        _current = item;
                        return true;
                    }
                }
                _state = State.Done;
            }

            _current = default;
            return false;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first element.
        /// </summary>
        public void Reset()
        {
            _index = -1;
            _current = default;
            _state = State.BeforeNull;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }
}
