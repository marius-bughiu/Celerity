using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A set specialized for <b>enum element types</b>, backed by a dense bit vector
/// indexed on the enum's underlying integer value — the .NET analogue of Java's
/// <c>java.util.EnumSet</c>. Membership is a single bit test and the whole set-algebra
/// surface collapses to word-wise bitwise operations when the operand is another
/// <see cref="EnumSet{TEnum}"/>.
/// </summary>
/// <typeparam name="TEnum">
/// The enum type whose values the set stores. Constrained to <c>struct, Enum</c>.
/// </typeparam>
/// <remarks>
/// <para>
/// Where <see cref="HashSet{T}"/> hashes and boxes each element through
/// <see cref="EqualityComparer{T}"/> and stores an open-addressed table,
/// <see cref="EnumSet{TEnum}"/> stores one bit per possible element. For an enum whose
/// members are small non-negative integers (the default <c>0, 1, 2, …</c> declaration —
/// the overwhelmingly common case), <see cref="Add(TEnum)"/> / <see cref="Contains(TEnum)"/>
/// / <see cref="Remove(TEnum)"/> are a shift, a mask, and a single-<see cref="ulong"/> bit
/// operation: no hash, no probe chain, no per-element allocation. Set algebra against another
/// <see cref="EnumSet{TEnum}"/> is <c>O(words)</c> word-wise <c>OR</c> / <c>AND</c> /
/// <c>AND&#8209;NOT</c> / <c>XOR</c> over a handful of <see cref="ulong"/>s (usually just one),
/// versus <see cref="HashSet{T}"/>'s element-by-element rehash-and-probe.
/// </para>
/// <para>
/// <b>Supported enums.</b> The backing store is sized once from the enum's maximum defined
/// underlying value. The type therefore supports enums whose underlying values are small,
/// non-negative integers. An enum that declares a negative member, or whose maximum value
/// exceeds <c>65535</c> (a sparse or <c>[Flags]</c> power-of-two enum, for which a dense bit
/// vector is the wrong tool), throws <see cref="NotSupportedException"/> from the constructor —
/// use <see cref="CeleritySet{T, THasher}"/> for those. A runtime value outside the supported
/// range (an out-of-range cast) is rejected by <see cref="Add(TEnum)"/> /
/// <see cref="TryAdd(TEnum)"/> with <see cref="ArgumentOutOfRangeException"/>, and reported as
/// absent by <see cref="Contains(TEnum)"/> / <see cref="Remove(TEnum)"/>.
/// </para>
/// <para>
/// It implements the full <see cref="ISet{T}"/> surface (and therefore
/// <see cref="ICollection{T}"/> / <see cref="IEnumerable{T}"/>) with BCL
/// <see cref="HashSet{T}"/> semantics, ships an allocation-free struct enumerator, and — unlike
/// the hash-table sets — enumerates in <b>ascending underlying-value order</b>, a deterministic
/// bonus that falls out of walking the bit vector low bit first.
/// </para>
/// <para>
/// The type is single-threaded.
/// </para>
/// </remarks>
public class EnumSet<TEnum> : ISet<TEnum>
    where TEnum : struct, Enum
{
    private readonly ulong[] _words;
    private int _count;

    // Incremented on every structural mutation so active enumerators can detect
    // concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new, empty <see cref="EnumSet{TEnum}"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="TEnum"/> declares a negative underlying value, or its maximum
    /// underlying value exceeds the supported range (a sparse / <c>[Flags]</c> enum) — a dense
    /// bit vector is unsuitable; use <see cref="CeleritySet{T, THasher}"/> instead.
    /// </exception>
    public EnumSet()
    {
        string? reason = EnumSetInfo<TEnum>.UnsupportedReason;
        if (reason is not null)
            throw new NotSupportedException(reason);

        int wordCount = EnumSetInfo<TEnum>.WordCount;
        _words = wordCount == 0 ? Array.Empty<ulong>() : new ulong[wordCount];
    }

    /// <summary>
    /// Initializes a new <see cref="EnumSet{TEnum}"/> that contains the distinct elements
    /// copied from the specified <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose elements are copied into the new set. Duplicate elements are
    /// silently deduplicated, matching BCL <see cref="HashSet{T}"/> semantics.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="TEnum"/> is not a supported enum (see <see cref="EnumSet()"/>).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="source"/> contains a value outside the supported range.
    /// </exception>
    public EnumSet(IEnumerable<TEnum> source)
        : this()
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is EnumSet<TEnum> other)
        {
            // Same TEnum ⇒ identical word layout: copy the bit vector wholesale.
            Array.Copy(other._words, _words, _words.Length);
            _count = other._count;
            return;
        }

        foreach (TEnum item in source)
            TryAdd(item);
    }

    /// <summary>
    /// Creates a new <see cref="EnumSet{TEnum}"/> containing every <b>declared</b> constant of
    /// <typeparamref name="TEnum"/> — the full universe of the enum.
    /// </summary>
    /// <returns>A set of all declared enum members.</returns>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="TEnum"/> is not a supported enum (see <see cref="EnumSet()"/>).
    /// </exception>
    public static EnumSet<TEnum> All()
    {
        var set = new EnumSet<TEnum>();
        ulong[] defined = EnumSetInfo<TEnum>.DefinedMask;
        Array.Copy(defined, set._words, defined.Length);
        set.Recount();
        return set;
    }

    /// <summary>
    /// Gets the number of elements contained in the set.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Adds the specified element to the set.
    /// Throws <see cref="ArgumentException"/> if the element already exists.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <exception cref="ArgumentException"><paramref name="item"/> already exists in the set.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="item"/> is outside the enum's supported range.
    /// </exception>
    public void Add(TEnum item)
    {
        if (!TryAdd(item))
            throw new ArgumentException($"The element {item} already exists in the set.", nameof(item));
    }

    /// <summary>
    /// Attempts to add the specified element to the set.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>
    /// <c>true</c> if the element was added; <c>false</c> if it already exists (the set is
    /// unchanged).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="item"/> is outside the enum's supported range.
    /// </exception>
    public bool TryAdd(TEnum item)
    {
        if (!TryGetBit(item, out int bit))
            throw new ArgumentOutOfRangeException(nameof(item), item,
                $"The value is outside the range supported by EnumSet<{typeof(TEnum).Name}>.");

        ref ulong word = ref _words[bit >> 6];
        ulong mask = 1UL << (bit & 63);
        if ((word & mask) != 0)
            return false;

        word |= mask;
        _count++;
        _version++;
        return true;
    }

    /// <summary>
    /// Determines whether the set contains the specified element.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>
    /// <c>true</c> if the element is present; <c>false</c> otherwise (including for a value
    /// outside the enum's supported range).
    /// </returns>
    public bool Contains(TEnum item)
    {
        if (!TryGetBit(item, out int bit))
            return false;

        return (_words[bit >> 6] & (1UL << (bit & 63))) != 0;
    }

    /// <summary>
    /// Removes the specified element from the set.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>
    /// <c>true</c> if the element was removed; <c>false</c> if it was not present (including for
    /// a value outside the enum's supported range).
    /// </returns>
    public bool Remove(TEnum item)
    {
        if (!TryGetBit(item, out int bit))
            return false;

        ref ulong word = ref _words[bit >> 6];
        ulong mask = 1UL << (bit & 63);
        if ((word & mask) == 0)
            return false;

        word &= ~mask;
        _count--;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all elements from the set.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_words, 0, _words.Length);
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each element in <b>ascending
    /// underlying-value order</b>. If the set is modified during enumeration,
    /// <see cref="Enumerator.MoveNext"/> throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<TEnum> IEnumerable<TEnum>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── ISet<TEnum> / ICollection<TEnum> set-algebra surface ──────────────────
    // Against another EnumSet<TEnum> the whole surface is word-wise bitwise work
    // (the point of the type); for a general IEnumerable<TEnum> it falls back to the
    // shared SetOperations helper, which matches BCL HashSet<T> semantics exactly.

    /// <summary>
    /// Modifies the set to contain all elements that are present in itself, in
    /// <paramref name="other"/>, or in both.
    /// </summary>
    /// <param name="other">The collection to union into this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void UnionWith(IEnumerable<TEnum> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is EnumSet<TEnum> es)
        {
            ulong[] a = _words, b = es._words;
            for (int i = 0; i < a.Length; i++)
                a[i] |= b[i];
            Recount();
            _version++;
            return;
        }

        SetOperations.UnionWith(this, other);
    }

    /// <summary>
    /// Modifies the set to contain only elements that are also present in
    /// <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to intersect with this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void IntersectWith(IEnumerable<TEnum> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is EnumSet<TEnum> es)
        {
            ulong[] a = _words, b = es._words;
            for (int i = 0; i < a.Length; i++)
                a[i] &= b[i];
            Recount();
            _version++;
            return;
        }

        SetOperations.IntersectWith(this, other);
    }

    /// <summary>
    /// Removes every element in <paramref name="other"/> from the set.
    /// </summary>
    /// <param name="other">The collection of elements to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void ExceptWith(IEnumerable<TEnum> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is EnumSet<TEnum> es)
        {
            ulong[] a = _words, b = es._words;
            for (int i = 0; i < a.Length; i++)
                a[i] &= ~b[i];
            Recount();
            _version++;
            return;
        }

        SetOperations.ExceptWith(this, other);
    }

    /// <summary>
    /// Modifies the set to contain only elements that are present either in itself or in
    /// <paramref name="other"/>, but not both.
    /// </summary>
    /// <param name="other">The collection to apply the symmetric difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void SymmetricExceptWith(IEnumerable<TEnum> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is EnumSet<TEnum> es)
        {
            ulong[] a = _words, b = es._words;
            for (int i = 0; i < a.Length; i++)
                a[i] ^= b[i];
            Recount();
            _version++;
            return;
        }

        SetOperations.SymmetricExceptWith(this, other);
    }

    /// <summary>
    /// Determines whether the set is a subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of this set is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSubsetOf(IEnumerable<TEnum> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is EnumSet<TEnum> es)
            return IsSubsetOfCore(es);

        return SetOperations.IsSubsetOf(this, other);
    }

    /// <summary>
    /// Determines whether the set is a proper (strict) subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of this set is in <paramref name="other"/> and
    /// <paramref name="other"/> has at least one element this set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<TEnum> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is EnumSet<TEnum> es)
            return _count < es._count && IsSubsetOfCore(es);

        return SetOperations.IsProperSubsetOf(this, other);
    }

    /// <summary>
    /// Determines whether the set is a superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of <paramref name="other"/> is in this set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<TEnum> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is EnumSet<TEnum> es)
            return es.IsSubsetOfCore(this);

        return SetOperations.IsSupersetOf(this, other);
    }

    /// <summary>
    /// Determines whether the set is a proper (strict) superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of <paramref name="other"/> is in this set and this set has
    /// at least one element <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSupersetOf(IEnumerable<TEnum> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is EnumSet<TEnum> es)
            return _count > es._count && es.IsSubsetOfCore(this);

        return SetOperations.IsProperSupersetOf(this, other);
    }

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> share at least one element.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool Overlaps(IEnumerable<TEnum> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is EnumSet<TEnum> es)
        {
            ulong[] a = _words, b = es._words;
            for (int i = 0; i < a.Length; i++)
                if ((a[i] & b[i]) != 0)
                    return true;
            return false;
        }

        return SetOperations.Overlaps(this, other);
    }

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> contain the same distinct
    /// elements.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<TEnum> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is EnumSet<TEnum> es)
        {
            ulong[] a = _words, b = es._words;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        return SetOperations.SetEquals(this, other);
    }

    /// <summary>
    /// Copies the elements of the set to the specified <paramref name="array"/>, starting at
    /// <paramref name="arrayIndex"/>, in ascending underlying-value order.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(TEnum[] array, int arrayIndex) => SetOperations.CopyTo(this, _count, array, arrayIndex);

    // Adds the element, returning whether it was newly added (ISet<T> semantics) — the
    // non-throwing counterpart of the public throw-on-duplicate Add(T).
    bool ISet<TEnum>.Add(TEnum item) => TryAdd(item);

    // ICollection<T>.Add must not throw on a duplicate (unlike the public Add(T)), so it maps
    // to the non-throwing TryAdd.
    void ICollection<TEnum>.Add(TEnum item) => TryAdd(item);

    bool ICollection<TEnum>.IsReadOnly => false;

    // Word-wise "is every bit of this also set in other".
    private bool IsSubsetOfCore(EnumSet<TEnum> other)
    {
        ulong[] a = _words, b = other._words;
        for (int i = 0; i < a.Length; i++)
            if ((a[i] & ~b[i]) != 0)
                return false;
        return true;
    }

    // Recomputes _count from the bit vector after a bulk word operation.
    private void Recount()
    {
        int count = 0;
        ulong[] words = _words;
        for (int i = 0; i < words.Length; i++)
            count += BitOperations.PopCount(words[i]);
        _count = count;
    }

    // Reads the enum's underlying bits at its natural width (unsigned, so every value maps to a
    // non-negative index) and range-checks against the backing bit vector. The switch is on a
    // per-instantiation JIT constant, so it folds to a single read. Returns false for any value
    // outside [0, TotalBits) — negatives read as huge and fail the unsigned bound too.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetBit(TEnum value, out int index)
    {
        long v = Unsafe.SizeOf<TEnum>() switch
        {
            1 => Unsafe.As<TEnum, byte>(ref value),
            2 => Unsafe.As<TEnum, ushort>(ref value),
            4 => Unsafe.As<TEnum, uint>(ref value),
            _ => unchecked((long)Unsafe.As<TEnum, ulong>(ref value)),
        };

        if ((ulong)v < (ulong)(uint)EnumSetInfo<TEnum>.TotalBits)
        {
            index = (int)v;
            return true;
        }

        index = 0;
        return false;
    }

    // Reconstructs the enum value from a bit index in [0, TotalBits). The switch is a
    // per-instantiation JIT constant.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TEnum FromBit(int index)
    {
        switch (Unsafe.SizeOf<TEnum>())
        {
            case 1: { byte b = (byte)index; return Unsafe.As<byte, TEnum>(ref b); }
            case 2: { ushort s = (ushort)index; return Unsafe.As<ushort, TEnum>(ref s); }
            case 4: { uint i = (uint)index; return Unsafe.As<uint, TEnum>(ref i); }
            default: { ulong l = (uint)index; return Unsafe.As<ulong, TEnum>(ref l); }
        }
    }

    /// <summary>
    /// A struct enumerator over an <see cref="EnumSet{TEnum}"/>. Because it is a struct,
    /// iterating it via <c>foreach</c> avoids the allocation a compiler-generated
    /// <see cref="IEnumerator{T}"/> would incur. Elements are yielded in ascending
    /// underlying-value order.
    /// </summary>
    public struct Enumerator : IEnumerator<TEnum>
    {
        private readonly EnumSet<TEnum> _set;
        private readonly int _version;
        private int _wordIndex;
        private ulong _remaining;
        private TEnum _current;

        internal Enumerator(EnumSet<TEnum> set)
        {
            _set = set;
            _version = set._version;
            _wordIndex = -1;
            _remaining = 0;
            _current = default;
        }

        /// <summary>
        /// Gets the element at the current position of the enumerator.
        /// </summary>
        public TEnum Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next element.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new element; <c>false</c> if it has passed
        /// the end of the set.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the set was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            ulong[] words = _set._words;
            while (true)
            {
                if (_remaining != 0)
                {
                    int tz = BitOperations.TrailingZeroCount(_remaining);
                    _remaining &= _remaining - 1; // clear the lowest set bit
                    _current = FromBit((_wordIndex << 6) + tz);
                    return true;
                }

                _wordIndex++;
                if (_wordIndex >= words.Length)
                {
                    _current = default;
                    return false;
                }

                _remaining = words[_wordIndex];
            }
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

            _wordIndex = -1;
            _remaining = 0;
            _current = default;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }
}

/// <summary>
/// Per-<typeparamref name="TEnum"/> layout metadata for <see cref="EnumSet{TEnum}"/>, computed
/// once by the generic type initializer: how many 64-bit words the bit vector needs, and which
/// bits correspond to declared constants. Never throws — an unsupported enum is reported via
/// <see cref="UnsupportedReason"/> so <see cref="EnumSet{TEnum}"/> can surface a clean
/// <see cref="NotSupportedException"/> rather than a <see cref="TypeInitializationException"/>.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
internal static class EnumSetInfo<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>The largest underlying value the type will size a bit vector for.</summary>
    public const long MaxSupportedValue = 65535;

    /// <summary>The number of 64-bit words in the bit vector (<c>0</c> for an empty enum).</summary>
    public static readonly int WordCount;

    /// <summary>The number of addressable bit positions (<see cref="WordCount"/> × 64).</summary>
    public static readonly int TotalBits;

    /// <summary>The bit vector with exactly the declared constants set (used by <c>All()</c>).</summary>
    public static readonly ulong[] DefinedMask;

    /// <summary>Non-<c>null</c> when the enum is unsupported; the message for the thrown exception.</summary>
    public static readonly string? UnsupportedReason;

    static EnumSetInfo()
    {
        Array raw = Enum.GetValuesAsUnderlyingType(typeof(TEnum));
        bool isUlong = Enum.GetUnderlyingType(typeof(TEnum)) == typeof(ulong);

        long max = -1;
        long min = 0;
        bool overCap = false;

        foreach (object o in raw)
        {
            long v;
            if (isUlong)
            {
                ulong uv = (ulong)o;
                if (uv > (ulong)MaxSupportedValue)
                {
                    overCap = true;
                    continue;
                }

                v = (long)uv;
            }
            else
            {
                v = Convert.ToInt64(o);
            }

            if (v > max) max = v;
            if (v < min) min = v;
        }

        string name = typeof(TEnum).Name;
        if (min < 0)
        {
            UnsupportedReason =
                $"EnumSet<{name}> is not supported: the enum declares a negative underlying value ({min}). " +
                $"EnumSet indexes a dense bit vector by the underlying value and requires non-negative members; " +
                $"use CeleritySet<{name}> instead.";
            DefinedMask = Array.Empty<ulong>();
            return;
        }

        if (overCap || max > MaxSupportedValue)
        {
            UnsupportedReason =
                $"EnumSet<{name}> is not supported: the enum's maximum underlying value exceeds the supported range " +
                $"(0..{MaxSupportedValue}). A dense bit vector is the wrong tool for sparse or [Flags] power-of-two " +
                $"enums; use CeleritySet<{name}> instead.";
            DefinedMask = Array.Empty<ulong>();
            return;
        }

        WordCount = max < 0 ? 0 : (int)((max >> 6) + 1);
        TotalBits = WordCount * 64;

        ulong[] mask = WordCount == 0 ? Array.Empty<ulong>() : new ulong[WordCount];
        foreach (object o in raw)
        {
            long v = isUlong ? (long)(ulong)o : Convert.ToInt64(o);
            mask[(int)(v >> 6)] |= 1UL << (int)(v & 63);
        }

        DefinedMask = mask;
    }
}
