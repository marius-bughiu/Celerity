using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Celerity.Collections;

/// <summary>
/// A mutable, fixed-length array of unsigned integers, each stored in exactly <see cref="BitsPerValue"/> bits
/// (1 to 64), packed end to end into 64-bit words. It sits between <see cref="System.Collections.BitArray"/>,
/// which only stores one bit per element, and an <c>int[]</c> or <c>long[]</c>, which round every value up to a
/// primitive width.
/// </summary>
/// <remarks>
/// <para>
/// <b>The workload.</b> Use it for a large array of small integers whose range is known: dictionary-encoded
/// column codes, quantized features, node or symbol ids below some <c>2^w</c>, palette indices, or small
/// counters. A million 20-bit values take 2.5&#160;MB here, against 4&#160;MB as an <c>int[]</c> and 8&#160;MB
/// as a <c>long[]</c>. Each element is still read and written in <c>O(1)</c>, in place. .NET ships no
/// multi-bit integer array narrower than a primitive: <see cref="System.Collections.BitArray"/> is the one-bit
/// case only, as is the library's own <see cref="BitSet"/>, and
/// <see cref="System.Collections.Specialized.BitVector32"/> packs a few fields into a single 32-bit integer. <c>BitReader</c> / <c>BitWriter</c> in <c>Celerity.Primitives</c>
/// pack arbitrary widths, but as a sequential stream with no random access or in-place update.
/// <see cref="EliasFanoSequence"/> compresses further, but only a sorted sequence, and it is immutable.
/// </para>
/// <para>
/// <b>The price.</b> A read is two loads, two shifts and a mask, where an <c>int[]</c> read is one load. Writes
/// are a read-modify-write of two words. When the array is small enough to stay in cache, every access is
/// therefore slower than the primitive array's. When it is not, the smaller footprint means fewer cache misses,
/// and the gap narrows. The API reference publishes the measured ratios.
/// </para>
/// <para>
/// <b>Layout.</b> Element <c>i</c> occupies bits <c>[i * w, (i + 1) * w)</c> of the word array, least-significant
/// bit first, so an element can straddle two words whenever <c>w</c> does not divide 64. The array keeps one
/// spare word past the last data word, always zero. That lets every read take the same two-load path: it never
/// branches on whether the element straddles, which keeps random access branch-free. Bits past the last element
/// are kept zero as well. The payload is <c>ceil(Count * w / 64) + 1</c> words, which
/// <see cref="MemoryUsageInBytes"/> reports.
/// </para>
/// <para>
/// <b>Values that do not fit are rejected, not truncated.</b> Every write checks the value against
/// <see cref="MaxValue"/> and throws when it is wider than <see cref="BitsPerValue"/>, because silently
/// keeping the low bits would corrupt the data with no error. Use <see cref="BitsRequired(ulong)"/> to choose a
/// width from the largest value you need to store.
/// </para>
/// <para>
/// The array is not thread-safe; concurrent mutation requires external synchronization. Enumeration is
/// invalidated by any write and throws <see cref="InvalidOperationException"/> if the array is modified
/// mid-iteration. As with <see cref="BitSet"/>, a write that stores the value already there still counts as a
/// write, and <see cref="Clear"/> always invalidates, because establishing that every element is already zero
/// costs the same scan as clearing it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // A million ids, every one below 2^20, take 20 bits each instead of an int's 32.
/// int bits = BitPackedArray.BitsRequired(maxId);   // 20 when maxId is 1_000_000
/// var ids = new BitPackedArray(1_000_000, bits);
///
/// ids[42] = 999_999;
/// ulong id = ids[42];
///
/// // Unpack a run of values into a buffer in one sequential pass.
/// Span&lt;ulong&gt; buffer = stackalloc ulong[256];
/// ids.GetRange(0, buffer);
/// </code>
/// </example>
public sealed class BitPackedArray : IReadOnlyList<ulong>
{
    private const int WordShift = 6;      // log2(64): a bit position >> 6 selects the word
    private const int WordMask = 63;      // a bit position & 63 selects the bit within that word
    private const int MaxBits = 64;

    // Element i occupies bits [i * _bits, (i + 1) * _bits). The final word is padding that is never part of any
    // element and stays zero, so a read can always load the word after an element's first word.
    private readonly ulong[] _words;
    private readonly int _count;
    private readonly int _bits;
    private readonly ulong _mask;
    private int _version;

    /// <summary>
    /// Initializes a new array of <paramref name="length"/> elements, each <paramref name="bitsPerValue"/> bits
    /// wide, with every element zero.
    /// </summary>
    /// <param name="length">The number of elements. Must be non-negative; <c>0</c> creates an empty array.</param>
    /// <param name="bitsPerValue">The width of each element in bits, from 1 to 64.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is negative or needs more words than one array can hold, or
    /// <paramref name="bitsPerValue"/> is outside <c>[1, 64]</c>.
    /// </exception>
    /// <remarks>
    /// The length ceiling depends on the width. Below 64 bits a value, every non-negative <see cref="int"/>
    /// length fits. At exactly 64 bits it is <c>Array.MaxLength - 1</c>, because of the padding word.
    /// </remarks>
    public BitPackedArray(int length, int bitsPerValue)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be non-negative.");
        ValidateBits(bitsPerValue);
        if (!FitsInOneArray(length, bitsPerValue))
            throw new ArgumentOutOfRangeException(nameof(length), length,
                $"A length of {length} at {bitsPerValue} bits a value needs more words than one array can hold.");

        _count = length;
        _bits = bitsPerValue;
        _mask = MaskFor(bitsPerValue);
        _words = new ulong[WordCountFor(length, bitsPerValue)];
    }

    /// <summary>
    /// Initializes a new array holding <paramref name="values"/> in enumeration order, each stored in
    /// <paramref name="bitsPerValue"/> bits.
    /// </summary>
    /// <param name="values">
    /// The initial values. Every one must be at most <c>2^bitsPerValue - 1</c>. A <c>ulong[]</c> or
    /// <see cref="List{T}"/> is packed directly; any other counted collection has its length checked before
    /// anything is allocated, and is then copied once; any other sequence is materialized once.
    /// </param>
    /// <param name="bitsPerValue">The width of each element in bits, from 1 to 64.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitsPerValue"/> is outside <c>[1, 64]</c>.</exception>
    /// <exception cref="ArgumentException">
    /// A value is wider than <paramref name="bitsPerValue"/>, or there are more values than one array of words
    /// can hold at that width.
    /// </exception>
    public BitPackedArray(IEnumerable<ulong> values, int bitsPerValue)
    {
        ArgumentNullException.ThrowIfNull(values);
        ValidateBits(bitsPerValue);

        ReadOnlySpan<ulong> source;
        if (values is ulong[] array)
        {
            source = array;
        }
        else if (values is List<ulong> list)
        {
            source = CollectionsMarshal.AsSpan(list);
        }
        else if (values is ICollection<ulong> collection)
        {
            // Checked before the copy is allocated, so an oversized source reports the documented exception
            // rather than failing inside an impossible allocation.
            int counted = collection.Count;
            ThrowIfSourceTooLong(counted, bitsPerValue);
            ulong[] copy = new ulong[counted];
            collection.CopyTo(copy, 0);
            source = copy;
        }
        else
        {
            source = values.ToArray();
        }

        ThrowIfSourceTooLong(source.Length, bitsPerValue);

        _count = source.Length;
        _bits = bitsPerValue;
        _mask = MaskFor(bitsPerValue);
        _words = new ulong[WordCountFor(source.Length, bitsPerValue)];

        // The fresh words are all zero, so the values can be streamed into them a whole word at a time instead of
        // read-modify-writing two words each. The width check rides along as one OR, and the instance never
        // escapes a constructor that throws, so it does not matter that the words were written first.
        if (Pack(source) > _mask)
            ThrowIfAnyTooWide(source, 0, nameof(values));
    }

    /// <summary>Gets the number of elements in the array (its fixed length).</summary>
    public int Count => _count;

    /// <summary>Gets the width of each element in bits, from 1 to 64.</summary>
    public int BitsPerValue => _bits;

    /// <summary>
    /// Gets the largest value an element can hold, <c>2^BitsPerValue - 1</c>; <see cref="ulong.MaxValue"/> at
    /// 64 bits.
    /// </summary>
    public ulong MaxValue => _mask;

    /// <summary>
    /// Gets the size in bytes of the packed words, <c>(ceil(Count * BitsPerValue / 64) + 1) * 8</c>, the last
    /// word being the padding that keeps reads branch-free. Compare it against <c>Count * sizeof(int)</c> or
    /// <c>Count * sizeof(long)</c>, the array it replaces.
    /// </summary>
    /// <remarks>
    /// This is payload, not retained-object size: it excludes the CLR's array header and the instance's own
    /// fields, the same accounting <see cref="EliasFanoSequence.MemoryUsageInBytes"/> uses. The figure is a
    /// <see cref="long"/>, as that one is, because a large array of wide values passes <see cref="int.MaxValue"/>
    /// bytes.
    /// </remarks>
    public long MemoryUsageInBytes => (long)_words.Length * sizeof(ulong);

    /// <summary>
    /// Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index, over <c>[0, Count)</c>.</param>
    /// <returns>The element stored there.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside <c>[0, Count)</c>, or the assigned value is greater than
    /// <see cref="MaxValue"/>.
    /// </exception>
    public ulong this[int index]
    {
        get => Get(index);
        set => Set(index, value);
    }

    /// <summary>
    /// Returns the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index, over <c>[0, Count)</c>.</param>
    /// <returns>The element stored there.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Get(int index)
    {
        if ((uint)index >= (uint)_count)
            ThrowIndexOutOfRange(index);

        return Read((long)index * _bits);
    }

    /// <summary>
    /// Stores <paramref name="value"/> at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index, over <c>[0, Count)</c>.</param>
    /// <param name="value">The value to store, at most <see cref="MaxValue"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is outside <c>[0, Count)</c>, or <paramref name="value"/> is greater than
    /// <see cref="MaxValue"/>.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, ulong value)
    {
        if ((uint)index >= (uint)_count)
            ThrowIndexOutOfRange(index);
        if (value > _mask)
            ThrowValueTooWide(value);

        Write((long)index * _bits, value);
        _version++;
    }

    /// <summary>
    /// Copies <c>destination.Length</c> consecutive elements, starting at <paramref name="index"/>, into
    /// <paramref name="destination"/>. This unpacks a run in one sequential pass over the words, which is the
    /// fast way to read many elements.
    /// </summary>
    /// <param name="index">The index of the first element to copy, over <c>[0, Count]</c>.</param>
    /// <param name="destination">The buffer to fill. Its length is the number of elements copied.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, Count]</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is longer than the <c>Count - index</c> elements that follow
    /// <paramref name="index"/>.
    /// </exception>
    public void GetRange(int index, Span<ulong> destination)
    {
        ThrowIfRangeInvalid(index, destination.Length, nameof(destination));

        int bits = _bits;
        long position = (long)index * bits;
        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = Read(position);
            position += bits;
        }
    }

    /// <summary>
    /// Stores <paramref name="values"/> into consecutive elements starting at <paramref name="index"/>. Every
    /// value is checked before any is written, so a rejected call leaves the array unchanged.
    /// </summary>
    /// <param name="index">The index of the first element to overwrite, over <c>[0, Count]</c>.</param>
    /// <param name="values">The values to store, each at most <see cref="MaxValue"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, Count]</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="values"/> is longer than the <c>Count - index</c> elements that follow
    /// <paramref name="index"/>, or one of its values is greater than <see cref="MaxValue"/>.
    /// </exception>
    /// <remarks>
    /// An empty <paramref name="values"/> writes nothing and leaves active enumerators valid; any other call
    /// invalidates them.
    /// </remarks>
    public void SetRange(int index, ReadOnlySpan<ulong> values)
    {
        ThrowIfRangeInvalid(index, values.Length, nameof(values));
        ThrowIfAnyTooWide(values, index, nameof(values));
        if (values.IsEmpty)
            return;

        WriteRange(index, values);
        _version++;
    }

    /// <summary>
    /// Sets every element to <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value to store, at most <see cref="MaxValue"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is greater than <see cref="MaxValue"/>.</exception>
    public void Fill(ulong value)
    {
        if (value > _mask)
            ThrowValueTooWide(value);

        if (value == 0)
        {
            Array.Clear(_words);
        }
        else
        {
            int bits = _bits;
            long position = 0;
            for (int i = 0; i < _count; i++)
            {
                Write(position, value);
                position += bits;
            }
        }
        _version++;
    }

    /// <summary>
    /// Sets every element to zero (equivalent to <c>Fill(0)</c>). Always invalidates active enumerators; see the
    /// type's remarks.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_words);
        _version++;
    }

    /// <summary>
    /// Copies every element, in index order, into <paramref name="array"/> starting at
    /// <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="arrayIndex"/> is negative or greater than the length of <paramref name="array"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The elements do not fit in <paramref name="array"/> after <paramref name="arrayIndex"/>.
    /// </exception>
    public void CopyTo(ulong[] array, int arrayIndex)
    {
        CopyToGuard.Validate(array, arrayIndex, _count, CopyToGuard.ElementsMessage);
        GetRange(0, array.AsSpan(arrayIndex, _count));
    }

    /// <summary>
    /// Returns the narrowest width, in bits, that can hold every value from <c>0</c> to
    /// <paramref name="maxValue"/>: <c>floor(log2(maxValue)) + 1</c>, and <c>1</c> for a
    /// <paramref name="maxValue"/> of <c>0</c>, since an element is never narrower than one bit.
    /// </summary>
    /// <param name="maxValue">The largest value the array must be able to store.</param>
    /// <returns>A width from 1 to 64, suitable as a <c>bitsPerValue</c> argument.</returns>
    public static int BitsRequired(ulong maxValue) => MaxBits - BitOperations.LeadingZeroCount(maxValue | 1);

    /// <summary>Returns a struct enumerator over the elements, in index order.</summary>
    /// <returns>An enumerator positioned before the first element.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<ulong> IEnumerable<ulong>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Reads the element whose first bit is at `position`. The low part comes from the element's first word; the
    // high part from the next one, shifted in two steps (<< 1, then << 63 - shift) so that a shift of zero moves
    // the next word out entirely instead of shifting by 64, which C# would reduce to a shift by 0. The next word
    // always exists because of the padding word, and the mask drops any bits from it that belong to the next
    // element, so no branch decides whether the element straddles.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong Read(long position)
    {
        int shift = (int)position & WordMask;
        ref ulong word = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_words), (nint)(position >> WordShift));
        ulong low = word >> shift;
        ulong high = (Unsafe.Add(ref word, 1) << 1) << (WordMask - shift);
        return (low | high) & _mask;
    }

    // Writes `value` (already checked against _mask) as the element whose first bit is at `position`. The part
    // that spills into the next word is shifted right in the same two steps as Read, so when nothing spills both
    // the cleared mask and the written bits come out zero and the next word is left as it was.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(long position, ulong value)
    {
        int shift = (int)position & WordMask;
        int back = WordMask - shift;
        ulong mask = _mask;
        ref ulong word = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_words), (nint)(position >> WordShift));
        word = (word & ~(mask << shift)) | (value << shift);
        ref ulong next = ref Unsafe.Add(ref word, 1);
        next = (next & ~((mask >> 1) >> back)) | ((value >> 1) >> back);
    }

    // Streams `values` into the zeroed words from element 0: each value is ORed into an accumulator at the next
    // free bit, and a full accumulator is stored as one word, carrying the bits that did not fit into the next.
    // Returns the OR of every value so the caller can check the width once. A value wider than the mask has
    // already spilled into the words by then, which only the constructor tolerates.
    private ulong Pack(ReadOnlySpan<ulong> values)
    {
        int bits = _bits;
        ref ulong word = ref MemoryMarshal.GetArrayDataReference(_words);
        ulong combined = 0;
        ulong pending = 0;
        int filled = 0;
        foreach (ulong value in values)
        {
            combined |= value;
            pending |= value << filled;
            filled += bits;
            if (filled >= MaxBits)
            {
                word = pending;
                word = ref Unsafe.Add(ref word, 1);
                filled -= MaxBits;
                // The bits of `value` above the ones that fit; two steps, so that a value which fit exactly
                // (filled == 0) carries nothing rather than shifting by `bits`, which may be 64.
                pending = (value >> 1) >> (bits - filled - 1);
            }
        }

        word = pending;
        return combined;
    }

    private void WriteRange(int index, ReadOnlySpan<ulong> values)
    {
        int bits = _bits;
        long position = (long)index * bits;
        for (int i = 0; i < values.Length; i++)
        {
            Write(position, values[i]);
            position += bits;
        }
    }

    // Validates a [index, index + length) window against the array, the List<T>.GetRange split of exceptions: an
    // index outside [0, Count] is the index's fault, a window that runs past the end is the buffer's.
    private void ThrowIfRangeInvalid(int index, int length, string bufferName)
    {
        if ((uint)index > (uint)_count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, Count].");
        if (length > _count - index)
            throw new ArgumentException(
                $"The buffer holds {length} elements, but only {_count - index} follow index {index}.", bufferName);
    }

    // One OR over the whole span finds out whether any value is too wide; only a failing call pays for the scan
    // that names the first offender.
    private void ThrowIfAnyTooWide(ReadOnlySpan<ulong> values, int firstIndex, string paramName)
    {
        ulong combined = 0;
        foreach (ulong value in values)
            combined |= value;
        if (combined <= _mask)
            return;

        int offender = 0;
        while (values[offender] <= _mask)
            offender++;
        throw new ArgumentException(
            $"The value {values[offender]} for element {firstIndex + offender} does not fit in {_bits} bits " +
            $"(the largest is {_mask}).", paramName);
    }

    private static void ValidateBits(int bitsPerValue)
    {
        if ((uint)(bitsPerValue - 1) >= MaxBits)
            throw new ArgumentOutOfRangeException(nameof(bitsPerValue), bitsPerValue,
                "Bits per value must be between 1 and 64.");
    }

    private static void ThrowIfSourceTooLong(int count, int bitsPerValue)
    {
        if (!FitsInOneArray(count, bitsPerValue))
            throw new ArgumentException(
                $"{count} values at {bitsPerValue} bits a value need more words than one array can hold.", "values");
    }

    private static bool FitsInOneArray(int length, int bitsPerValue) =>
        WordCountFor(length, bitsPerValue) <= Array.MaxLength;

    // The data words, rounded up, plus the padding word Read and Write rely on.
    private static long WordCountFor(int length, int bitsPerValue) =>
        (((long)length * bitsPerValue + WordMask) >> WordShift) + 1;

    // The two-step shift keeps a width of 64 from becoming a shift by 64 (which C# reduces to a shift by 0).
    private static ulong MaskFor(int bitsPerValue) => ((1UL << (bitsPerValue - 1)) << 1) - 1;

    [DoesNotReturn]
    private static void ThrowIndexOutOfRange(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index), index, "Index was outside the bounds of the array.");

    [DoesNotReturn]
    private void ThrowValueTooWide(ulong value) =>
        throw new ArgumentOutOfRangeException(nameof(value), value,
            $"The value does not fit in {_bits} bits (the largest is {_mask}).");

    /// <summary>
    /// A struct enumerator over a <see cref="BitPackedArray"/> that yields each element in index order. Being a
    /// struct, iterating via <c>foreach</c> avoids the allocation a compiler-generated
    /// <see cref="IEnumerator{T}"/> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<ulong>
    {
        private readonly BitPackedArray _array;
        private readonly int _version;
        private int _index;
        private ulong _current;

        internal Enumerator(BitPackedArray array)
        {
            _array = array;
            _version = array._version;
            _index = -1;
            _current = 0;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly ulong Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element.</summary>
        /// <returns><c>true</c> if another element was read; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The array was modified since the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _array._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            // Compared as unsigned so that a call after the end, with _index parked at Count, cannot wrap at
            // int.MaxValue and restart the walk.
            uint next = (uint)_index + 1;
            if (next < (uint)_array._count)
            {
                _index = (int)next;
                _current = _array.Read((long)_index * _array._bits);
                return true;
            }

            _index = _array._count;
            _current = 0;
            return false;
        }

        /// <summary>Resets the enumerator to its initial position, before the first element.</summary>
        /// <exception cref="InvalidOperationException">The array was modified since the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _array._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _index = -1;
            _current = 0;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public readonly void Dispose() { }
    }
}
