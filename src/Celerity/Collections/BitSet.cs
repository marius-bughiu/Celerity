using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A dense, fixed-length array of bits packed into 64-bit words, with fast
/// population counting and SIMD-accelerated bulk boolean operations.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BitSet"/> is the exact, deterministic counterpart to the probabilistic
/// <see cref="BloomFilter{T, THasher}"/>: where a Bloom filter trades exactness for
/// memory, a <see cref="BitSet"/> stores one bit per index with no error. It is a
/// drop-in alternative to <see cref="System.Collections.BitArray"/> for workloads
/// that are dominated by two things the BCL type does not offer:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="Count"/> — the number of set bits (cardinality), computed in
/// <c>O(length / 64)</c> via <see cref="BitOperations.PopCount(ulong)"/> rather than
/// the bit-by-bit loop a caller would otherwise write over a
/// <see cref="System.Collections.BitArray"/>.
/// </description></item>
/// <item><description>
/// <see cref="And"/> / <see cref="Or"/> / <see cref="Xor"/> / <see cref="Not"/> —
/// in-place bulk boolean operations over the whole vector. Where
/// <see cref="System.Collections.BitArray"/> walks 32-bit words, <see cref="BitSet"/>
/// walks 64-bit words and, when the hardware supports it, a whole
/// <see cref="Vector{T}"/> of words per iteration.
/// </description></item>
/// </list>
/// <para>
/// The bit at index <c>i</c> lives in word <c>i / 64</c> at bit position <c>i % 64</c>.
/// Any bits in the final word beyond <see cref="Length"/> are kept clear at all times
/// (after <see cref="SetAll"/>, <see cref="Not"/>, and the bulk operators), so
/// <see cref="Count"/>, <see cref="Any"/>, and <see cref="All"/> never observe a stray
/// out-of-range bit.
/// </para>
/// <para>
/// The set is not thread-safe; concurrent mutation requires external synchronization.
/// Enumeration (<see cref="GetEnumerator"/> and <see cref="EnumerateSetBits"/>) is
/// invalidated by any structural mutation and throws
/// <see cref="InvalidOperationException"/> if the set is modified mid-iteration.
/// </para>
/// </remarks>
public sealed class BitSet : IEnumerable<bool>
{
    private const int WordShift = 6;     // log2(64): index >> 6 selects the word
    private const int WordMask = 63;     // index & 63 selects the bit within a word

    private readonly ulong[] _words;
    private readonly int _length;
    private int _version;

    /// <summary>
    /// Initializes a new <see cref="BitSet"/> of the specified length with every bit
    /// cleared.
    /// </summary>
    /// <param name="length">The number of bits. Must be non-negative; <c>0</c> creates
    /// an empty set.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is
    /// negative.</exception>
    public BitSet(int length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be non-negative.");

        _length = length;
        _words = new ulong[WordCount(length)];
    }

    /// <summary>
    /// Initializes a new <see cref="BitSet"/> of the specified length with every bit
    /// set to <paramref name="defaultValue"/>.
    /// </summary>
    /// <param name="length">The number of bits. Must be non-negative.</param>
    /// <param name="defaultValue">The initial value for every bit.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is
    /// negative.</exception>
    public BitSet(int length, bool defaultValue)
        : this(length)
    {
        if (defaultValue)
            SetAll(true);
    }

    /// <summary>
    /// Initializes a new <see cref="BitSet"/> from a <see cref="bool"/> array, where
    /// bit <c>i</c> is set if and only if <paramref name="values"/><c>[i]</c> is
    /// <c>true</c>. The new set has the same length as the array.
    /// </summary>
    /// <param name="values">The source values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is
    /// <c>null</c>.</exception>
    public BitSet(bool[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        _length = values.Length;
        _words = new ulong[WordCount(values.Length)];
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
                _words[i >> WordShift] |= 1UL << (i & WordMask);
        }
    }

    /// <summary>
    /// Gets the number of bits in the set.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets the number of bits that are set (the cardinality / population count),
    /// computed in <c>O(Length / 64)</c> using a hardware population count per word.
    /// </summary>
    /// <remarks>
    /// This is the headline advantage over <see cref="System.Collections.BitArray"/>,
    /// which exposes no cardinality and forces callers into a bit-by-bit loop.
    /// </remarks>
    public int Count
    {
        get
        {
            ulong[] words = _words;
            int count = 0;
            for (int i = 0; i < words.Length; i++)
                count += BitOperations.PopCount(words[i]);
            return count;
        }
    }

    /// <summary>
    /// Gets or sets the bit at the specified index.
    /// </summary>
    /// <param name="index">The zero-based bit index.</param>
    /// <returns><c>true</c> if the bit is set; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is
    /// negative or not less than <see cref="Length"/>.</exception>
    public bool this[int index]
    {
        get => Get(index);
        set => Set(index, value);
    }

    /// <summary>
    /// Returns the value of the bit at the specified index.
    /// </summary>
    /// <param name="index">The zero-based bit index.</param>
    /// <returns><c>true</c> if the bit is set; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is
    /// negative or not less than <see cref="Length"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int index)
    {
        if ((uint)index >= (uint)_length)
            ThrowIndexOutOfRange(index);

        return (_words[index >> WordShift] & (1UL << (index & WordMask))) != 0;
    }

    /// <summary>
    /// Sets the bit at the specified index to <paramref name="value"/>.
    /// </summary>
    /// <param name="index">The zero-based bit index.</param>
    /// <param name="value">The value to assign.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is
    /// negative or not less than <see cref="Length"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, bool value)
    {
        if ((uint)index >= (uint)_length)
            ThrowIndexOutOfRange(index);

        ulong bit = 1UL << (index & WordMask);
        ref ulong word = ref _words[index >> WordShift];
        if (value)
            word |= bit;
        else
            word &= ~bit;
        _version++;
    }

    /// <summary>
    /// Toggles the bit at the specified index, returning its new value.
    /// </summary>
    /// <param name="index">The zero-based bit index.</param>
    /// <returns>The value of the bit after flipping.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is
    /// negative or not less than <see cref="Length"/>.</exception>
    public bool Flip(int index)
    {
        if ((uint)index >= (uint)_length)
            ThrowIndexOutOfRange(index);

        ulong bit = 1UL << (index & WordMask);
        ref ulong word = ref _words[index >> WordShift];
        word ^= bit;
        _version++;
        return (word & bit) != 0;
    }

    /// <summary>
    /// Sets every bit in the set to <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value to assign to every bit.</param>
    public void SetAll(bool value)
    {
        if (value)
        {
            ulong[] words = _words;
            for (int i = 0; i < words.Length; i++)
                words[i] = ulong.MaxValue;
            ClearTail();
        }
        else
        {
            Array.Clear(_words, 0, _words.Length);
        }
        _version++;
    }

    /// <summary>
    /// Clears every bit in the set (equivalent to <c>SetAll(false)</c>).
    /// </summary>
    public void Clear()
    {
        Array.Clear(_words, 0, _words.Length);
        _version++;
    }

    /// <summary>
    /// Performs an in-place bitwise AND with <paramref name="other"/>, so each bit of
    /// this set becomes the AND of the corresponding bits of the two sets.
    /// </summary>
    /// <param name="other">The set to AND with. Left unmodified.</param>
    /// <returns>This set, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is
    /// <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="other"/> has a different
    /// <see cref="Length"/>.</exception>
    public BitSet And(BitSet other)
    {
        ulong[] a = _words;
        ulong[] b = ValidateSameLength(other);

        int i = 0;
        if (Vector.IsHardwareAccelerated && a.Length >= Vector<ulong>.Count)
        {
            int upper = a.Length - Vector<ulong>.Count;
            for (; i <= upper; i += Vector<ulong>.Count)
                (new Vector<ulong>(a, i) & new Vector<ulong>(b, i)).CopyTo(a, i);
        }
        for (; i < a.Length; i++)
            a[i] &= b[i];

        _version++;
        return this;
    }

    /// <summary>
    /// Performs an in-place bitwise OR with <paramref name="other"/>, so each bit of
    /// this set becomes the OR of the corresponding bits of the two sets.
    /// </summary>
    /// <param name="other">The set to OR with. Left unmodified.</param>
    /// <returns>This set, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is
    /// <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="other"/> has a different
    /// <see cref="Length"/>.</exception>
    public BitSet Or(BitSet other)
    {
        ulong[] a = _words;
        ulong[] b = ValidateSameLength(other);

        int i = 0;
        if (Vector.IsHardwareAccelerated && a.Length >= Vector<ulong>.Count)
        {
            int upper = a.Length - Vector<ulong>.Count;
            for (; i <= upper; i += Vector<ulong>.Count)
                (new Vector<ulong>(a, i) | new Vector<ulong>(b, i)).CopyTo(a, i);
        }
        for (; i < a.Length; i++)
            a[i] |= b[i];

        _version++;
        return this;
    }

    /// <summary>
    /// Performs an in-place bitwise XOR with <paramref name="other"/>, so each bit of
    /// this set becomes the XOR of the corresponding bits of the two sets.
    /// </summary>
    /// <param name="other">The set to XOR with. Left unmodified.</param>
    /// <returns>This set, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is
    /// <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="other"/> has a different
    /// <see cref="Length"/>.</exception>
    public BitSet Xor(BitSet other)
    {
        ulong[] a = _words;
        ulong[] b = ValidateSameLength(other);

        int i = 0;
        if (Vector.IsHardwareAccelerated && a.Length >= Vector<ulong>.Count)
        {
            int upper = a.Length - Vector<ulong>.Count;
            for (; i <= upper; i += Vector<ulong>.Count)
                (new Vector<ulong>(a, i) ^ new Vector<ulong>(b, i)).CopyTo(a, i);
        }
        for (; i < a.Length; i++)
            a[i] ^= b[i];

        _version++;
        return this;
    }

    /// <summary>
    /// Inverts every bit in the set in place (one's complement).
    /// </summary>
    /// <returns>This set, to allow chaining.</returns>
    public BitSet Not()
    {
        ulong[] a = _words;

        int i = 0;
        if (Vector.IsHardwareAccelerated && a.Length >= Vector<ulong>.Count)
        {
            int upper = a.Length - Vector<ulong>.Count;
            Vector<ulong> ones = new(ulong.MaxValue);
            for (; i <= upper; i += Vector<ulong>.Count)
                (new Vector<ulong>(a, i) ^ ones).CopyTo(a, i);
        }
        for (; i < a.Length; i++)
            a[i] = ~a[i];

        ClearTail();
        _version++;
        return this;
    }

    /// <summary>
    /// Returns <c>true</c> if any bit in the set is set.
    /// </summary>
    public bool Any()
    {
        ulong[] words = _words;
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i] != 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if every bit in the set is set. An empty set returns
    /// <c>true</c> (vacuously).
    /// </summary>
    public bool All()
    {
        ulong[] words = _words;
        int fullWords = _length >> WordShift;
        for (int i = 0; i < fullWords; i++)
        {
            if (words[i] != ulong.MaxValue)
                return false;
        }

        int rem = _length & WordMask;
        if (rem != 0)
        {
            ulong mask = (1UL << rem) - 1;
            if ((words[fullWords] & mask) != mask)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if no bit in the set is set (the negation of
    /// <see cref="Any"/>).
    /// </summary>
    public bool None() => !Any();

    /// <summary>
    /// Enumerates the indices of the set bits in ascending order, skipping over runs
    /// of clear bits a whole word at a time. Allocation-free.
    /// </summary>
    /// <returns>An enumerable over the indices of the set bits.</returns>
    /// <remarks>
    /// This is the efficient way to walk a sparse <see cref="BitSet"/>: a fully clear
    /// 64-bit word is skipped in a single comparison, and within a populated word the
    /// next set bit is found with <see cref="BitOperations.TrailingZeroCount(ulong)"/>.
    /// </remarks>
    public SetBitEnumerable EnumerateSetBits() => new(this);

    /// <summary>
    /// Returns an allocation-free enumerator that yields each bit's value, from index
    /// <c>0</c> to <see cref="Length"/> minus one.
    /// </summary>
    /// <returns>A struct enumerator over this set's bit values.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<bool> IEnumerable<bool>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Bits in the final word beyond _length must stay clear so Count / Any / All never
    // see a stray out-of-range bit after SetAll(true) or Not(). Full-word lengths
    // (rem == 0) need no masking.
    private void ClearTail()
    {
        int rem = _length & WordMask;
        if (rem != 0 && _words.Length > 0)
            _words[_words.Length - 1] &= (1UL << rem) - 1;
    }

    private ulong[] ValidateSameLength(BitSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._length != _length)
            throw new ArgumentException("The two bit sets must have the same length.", nameof(other));
        return other._words;
    }

    private static int WordCount(int length) => (int)(((uint)length + WordMask) >> WordShift);

    private static void ThrowIndexOutOfRange(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index), index, "Bit index was outside the bounds of the set.");

    /// <summary>
    /// A struct enumerator over a <see cref="BitSet"/> that yields each bit's value in
    /// index order. Being a struct, iterating via <c>foreach</c> avoids the allocation
    /// a compiler-generated <see cref="IEnumerator{T}"/> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<bool>
    {
        private readonly BitSet _set;
        private readonly int _version;
        private int _index;
        private bool _current;

        internal Enumerator(BitSet set)
        {
            _set = set;
            _version = set._version;
            _index = -1;
            _current = false;
        }

        /// <summary>
        /// Gets the bit value at the current position of the enumerator.
        /// </summary>
        public readonly bool Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next bit.
        /// </summary>
        /// <returns><c>true</c> if the enumerator advanced to a new bit; <c>false</c>
        /// if it passed the end of the set.</returns>
        /// <exception cref="InvalidOperationException">The set was modified since the
        /// enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            int next = _index + 1;
            if (next < _set._length)
            {
                _index = next;
                _current = (_set._words[next >> WordShift] & (1UL << (next & WordMask))) != 0;
                return true;
            }

            _current = false;
            return false;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first bit.
        /// </summary>
        /// <exception cref="InvalidOperationException">The set was modified since the
        /// enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _index = -1;
            _current = false;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public readonly void Dispose() { }
    }

    /// <summary>
    /// A lightweight, allocation-free enumerable returned by
    /// <see cref="EnumerateSetBits"/> that yields the indices of the set bits.
    /// </summary>
    public readonly struct SetBitEnumerable : IEnumerable<int>
    {
        private readonly BitSet _set;

        internal SetBitEnumerable(BitSet set) => _set = set;

        /// <summary>
        /// Returns a struct enumerator over the indices of the set bits.
        /// </summary>
        public SetBitEnumerator GetEnumerator() => new(_set);

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A struct enumerator over the indices of the set bits in a <see cref="BitSet"/>,
    /// skipping clear words in a single comparison and locating set bits within a word
    /// via <see cref="BitOperations.TrailingZeroCount(ulong)"/>.
    /// </summary>
    public struct SetBitEnumerator : IEnumerator<int>
    {
        private readonly BitSet _set;
        private readonly int _version;
        private int _wordIndex;
        private ulong _remaining;   // the current word with already-yielded bits cleared
        private int _current;

        internal SetBitEnumerator(BitSet set)
        {
            _set = set;
            _version = set._version;
            _wordIndex = -1;
            _remaining = 0;
            _current = 0;
        }

        /// <summary>
        /// Gets the index of the set bit at the current position of the enumerator.
        /// </summary>
        public readonly int Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next set bit.
        /// </summary>
        /// <returns><c>true</c> if another set bit was found; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The set was modified since the
        /// enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            ulong[] words = _set._words;
            while (_remaining == 0)
            {
                if (++_wordIndex >= words.Length)
                {
                    _current = 0;
                    return false;
                }
                _remaining = words[_wordIndex];
            }

            int bit = BitOperations.TrailingZeroCount(_remaining);
            _remaining &= _remaining - 1; // clear the lowest set bit
            _current = (_wordIndex << WordShift) + bit;
            return true;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first set bit.
        /// </summary>
        /// <exception cref="InvalidOperationException">The set was modified since the
        /// enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _wordIndex = -1;
            _remaining = 0;
            _current = 0;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public readonly void Dispose() { }
    }
}
