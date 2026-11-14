using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A build-once, read-many set of <see cref="string"/> elements using
/// <see cref="StringFnV1AHasher"/> by default. Supply a different string hasher via
/// the <see cref="FrozenCeleritySet{THasher}"/> generic overload when your elements
/// favour a different hash (e.g. <see cref="StringFnV1AFullHasher"/> for non-ASCII
/// elements, one of the strong hashers for adversarial elements, or a block hasher
/// such as <see cref="StringXxHash32Hasher"/> for faster membership tests on longer
/// elements: every test hashes the whole element, and this default folds one character
/// per step).
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
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> holds <c>2^30</c> or more distinct non-<c>null</c> elements,
    /// which a power-of-two frozen table cannot represent.
    /// </exception>
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
/// mutating members. The constructor first searches a small parameter space (table
/// size × a mixing seed) for a <em>perfect</em> — that is, collision-free — placement
/// of the elements. When one is found (<see cref="IsPerfectlyHashed"/> is then
/// <c>true</c>) a membership test is a single hash, a single array index, and a single
/// equality check, with no probing.
/// </para>
/// <para>
/// As for <see cref="FrozenCelerityDictionary{TValue, THasher}"/>, which shares this
/// layout, a perfect placement is realistic only for small sets: it is near-certain up to
/// about 100 elements, for most counts up to 333 a matter of odds that depend on where
/// the count falls between powers of two, and nil past that, where the build skips the
/// search. It is also impossible when two distinct elements collide on the chosen hasher's
/// raw 32-bit code (for example <c>"A"</c> and <c>"Ł"</c> under the low-byte
/// <see cref="StringFnV1AHasher"/>). Otherwise the build uses an open-addressed
/// linear-probing table at most half full (<see cref="IsPerfectlyHashed"/> is
/// <c>false</c>), where a hit inspects at most about 1.5 slots on average. Membership
/// tests are correct either way: the equality check tells colliding elements apart.
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
    private readonly string?[] _items;

    // The slots that hold an element, ascending. Enumeration walks these rather than every
    // slot: past a perfect placement the table is at most half full, so a scan of all of it
    // would visit one to three empty slots per element, each behind a branch that is close
    // to a coin flip.
    private readonly int[] _occupied;
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
        // a clear error rather than build a fallback table with no empty slot, whose miss
        // probe would never end.
        ThrowIfElementCountExceedsCeiling(n, nameof(source));

        // Precompute the raw hash code of every element once; the perfect-hash search
        // re-mixes these with each candidate seed rather than re-hashing the strings.
        int[] baseHashes = new int[n];
        for (int i = 0; i < n; i++)
            baseHashes[i] = _hasher.Hash(itemList[i]);

        _isPerfect = FrozenHashLayout.TryFindPerfect(baseHashes, out int size, out _seed);
        if (!_isPerfect)
            size = FrozenHashLayout.FallbackSize(n);

        // One placement loop serves both layouts: under a perfect (size, seed) every element's
        // home slot is vacant, so the probe below never advances; otherwise it is the
        // linear-probing insert, with seed 0 and a table at most half full.
        int mask = size - 1;
        var items = new string?[size];
        for (int i = 0; i < n; i++)
        {
            int slot = (int)(FrozenHashLayout.Mix(unchecked((uint)baseHashes[i]), _seed) & (uint)mask);
            while (items[slot] is not null)
                slot = (slot + 1) & mask;

            items[slot] = itemList[i];
        }

        var occupied = new int[n];
        for (int slot = 0, next = 0; next < n; slot++)
        {
            if (items[slot] is not null)
                occupied[next++] = slot;
        }

        _items = items;
        _occupied = occupied;
        _mask = mask;
    }

    /// <summary>
    /// Gets the number of elements in the set (including the out-of-band
    /// <c>null</c> element, if present).
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets a value indicating whether the build found a collision-free (perfect)
    /// placement, so membership tests take the single-probe fast path. <c>false</c>
    /// means the set uses a linear-probing table at most half full instead: always the
    /// case past 333 non-<c>null</c> elements, often from about 110 elements up (the odds
    /// depend on where the count falls between powers of two), and whenever two distinct
    /// elements share the chosen hasher's raw hash code. Membership tests are correct either
    /// way.
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
    // The whole frozen set is the left-hand operand; `other` is the right-hand operand.
    // IsSupersetOf and Overlaps stream `other` against Contains and may stop early.
    // IsSubsetOf, IsProperSubsetOf, IsProperSupersetOf and SetEquals materialize `other`
    // once into an ordinal HashSet, including for empty and same-instance inputs.
    // Streaming does not copy `other`, but obtaining its enumerator can still allocate.

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

    // The element-count ceiling — the mirror of FrozenCelerityDictionary's key-count guard, and unreachable for
    // the same reason: `n` counts the elements already materialized into a List<string>, so reaching 2^30 needs
    // an 8.6 GB backing string[], past the 2 GiB single-object array limit, before this check is read. A source
    // that merely *reports* a huge ICollection.Count cannot reach it either; that count is only a capacity hint.
    [ExcludeFromCodeCoverage(Justification = "Unreachable: 2^30 materialized elements exceed the 2 GiB " +
        "single-object array limit, so the guard cannot be reached regardless of available memory.")]
    private static void ThrowIfElementCountExceedsCeiling(int n, string paramName)
    {
        if (n >= FastUtils.MaxPowerOfTwoCapacity)
            throw new ArgumentException(
                $"A frozen set can hold at most {FastUtils.MaxPowerOfTwoCapacity - 1} non-null elements; got {n}.",
                paramName);
    }

    // Returns the slot holding <paramref name="item"/>, or -1 if absent. In perfect
    // mode this is a single index + equality check; in fallback mode it linear-probes
    // until it finds the element or hits an empty slot. The caller guarantees
    // item != null.
    // The hasher instance the span-lookup extension methods hand back to the probe below.
    // The field is private and the extension methods live outside the type, so they need a
    // way to reach it without the class itself carrying an ISpanHashProvider constraint —
    // which could not be added to THasher without breaking every existing instantiation.
    internal THasher Hasher => _hasher;

    // The span twin of FindSlot. Generic in its own hasher type parameter (rather than
    // reusing THasher) so the ISpanHashProvider constraint lives on the method: the class
    // constraint stays exactly what it has always been, and the JIT still devirtualizes the
    // hash call because TSpanHasher is a struct type parameter. Both the perfect and the
    // linear-probing fallback shapes are mirrored exactly, including the same Mix/seed, so
    // the span path and the string path resolve to the same slot.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FindSlot<TSpanHasher>(ReadOnlySpan<char> item, TSpanHasher hasher)
        where TSpanHasher : struct, ISpanHashProvider
    {
        string?[] items = _items;
        ref string? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        int mask = _mask;
        int slot = (int)(FrozenHashLayout.Mix(unchecked((uint)hasher.Hash(item)), _seed) & (uint)mask);

        if (_isPerfect)
        {
            string? candidate = Unsafe.Add(ref itemsRef, (nint)(uint)slot);
            return candidate is not null && item.SequenceEqual(candidate.AsSpan()) ? slot : -1;
        }

        while (true)
        {
            string? candidate = Unsafe.Add(ref itemsRef, (nint)(uint)slot);
            if (candidate is null)
                return -1;
            if (item.SequenceEqual(candidate.AsSpan()))
                return slot;
            slot = (slot + 1) & mask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindSlot(string item)
    {
        string?[] items = _items;
        ref string? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        int mask = _mask;
        var comparer = EqualityComparer<string>.Default;
        int slot = (int)(FrozenHashLayout.Mix(unchecked((uint)_hasher.Hash(item)), _seed) & (uint)mask);

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

        // The set's arrays, read once here rather than through _set on every MoveNext.
        private readonly int[] _occupied;
        private readonly string?[] _items;

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
            _occupied = set._occupied;
            _items = set._items;
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
                if (++_index < _occupied.Length)
                {
                    nint slot = (nint)(uint)Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_occupied), (nint)(uint)_index);
                    _current = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_items), slot);
                    return true;
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
