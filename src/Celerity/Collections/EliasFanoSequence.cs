using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable</b>, compressed sequence of non-negative, non-decreasing <see cref="long"/> values — posting
/// lists, record and byte offsets, monotone timestamps — stored, on a large sequence, in about <c>L + 4</c> bits
/// per value, where <c>L = floor(log2(max / n))</c>, while keeping constant-time random access on typical data
/// and fast successor (<see cref="CountLessThan"/>, <see cref="TryGetLowerBound"/>) queries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Build once, query many.</b> The values are encoded at construction and there is no mutating member; a
/// changed sequence means building a new one, which costs <c>O(n)</c>. Collect into an array or a
/// <see cref="List{T}"/> while the sequence is still growing and encode it once it is final.
/// </para>
/// <para>
/// The BCL has no compressed integer sequence of any kind. The honest baseline is the <c>long[]</c> a caller
/// already holds, which costs 64 bits a value however close together the values sit, and answers "where is
/// the first value at or above <c>x</c>" with <see cref="Array.BinarySearch{T}(T[], T)"/> when the values are
/// distinct — with repeats an exact match may land on any equal element, so the first one takes a
/// hand-written lower-bound search, which is what <see cref="CountLessThan"/> and <see cref="IndexOf"/>
/// return. The library's ordered containers answer the same lower bound at more than 64 bits a value, not less, and nothing else here
/// compresses the shape: <see cref="CompressedIntSet"/> is a 32-bit <i>set</i>, with no
/// duplicates and no positional access, and a <see cref="RankSelectBitVector"/> over the values themselves
/// needs one bit per point of the value range, which is out of reach for byte offsets into a large file.
/// A delta-plus-<c>VarInt</c> stream can be smaller still, but it has no random access: reading the
/// <c>i</c>-th value means decoding every value before it.
/// </para>
/// <para>
/// <b>Layout.</b> This is the Elias–Fano encoding. For <c>n</c> values whose largest is <c>m</c>, each value is
/// split at <c>L = floor(log2(m / n))</c> bits (<see cref="LowBitCount"/>). The <c>L</c> low bits are packed
/// verbatim, end to end. The high part <c>h</c> of the <c>i</c>-th value is recorded by setting bit
/// <c>h + i</c> of a second bit vector, which therefore holds <c>n</c> ones and one zero per high value
/// <c>0..(m &gt;&gt; L)</c> — under <c>3n + 1</c> bits, because the choice of <c>L</c> keeps
/// <c>m &gt;&gt; L</c> below <c>2n</c>. Reading the <c>i</c>-th value is a <i>select</i> over that vector (the
/// position of its <c>i</c>-th one, minus <c>i</c>, is the high part) and one read of the low bits; locating a
/// value is a select of the zeros that bound its high part, then a binary search of the low bits between
/// them. Each select starts from a sample of every 128th one (or zero): that sample and the next bracket
/// the target, a rank directory of one <see cref="int"/> per 256 bits narrows the bracket to one superblock by
/// binary search, and a scan of at most four words finishes. On data whose values spread out, 128 ones or
/// zeros span a superblock or two and the search takes a step or none, so a select is <c>O(1)</c>; a long
/// gap between consecutive values, or a long run of values sharing one high part, widens a bracket, and the
/// cost then grows with the logarithm of that gap rather than linearly — <c>O(log n)</c> in the worst case.
/// </para>
/// <para>
/// <b>Space.</b> <c>n * L</c> bits of low parts, under <c>3n + 1</c> bits of high parts, one
/// <see cref="long"/> per 128 ones and per 128 zeros of samples, and one <see cref="int"/> per 256 bits of rank
/// directory — about <c>L + 4</c> bits a value in all on a large sequence. Each array rounds up to a whole word
/// or entry, so a short sequence also pays a fixed few dozen bytes. A million values spaced on average 100 apart take about
/// 1.27&#160;MB against the 8&#160;MB of the
/// <c>long[]</c>. <see cref="MemoryUsageInBytes"/> reports the exact figure for a given instance.
/// </para>
/// <para>
/// The type holds no mutable state after construction, so instances are safe to share across threads.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The byte offset at which each record of a large append-only file starts.
/// var offsets = new EliasFanoSequence(recordOffsets);
///
/// // Where does record 123,456 start?
/// long start = offsets[123_456];
///
/// // Which record holds byte 7,000,000,000? The last one starting at or before it.
/// int record = offsets.CountLessThan(7_000_000_001) - 1;
/// </code>
/// </example>
public sealed class EliasFanoSequence : IReadOnlyList<long>
{
    private const int WordShift = 6;     // log2(64): a bit position >> 6 selects the word
    private const int WordMask = 63;     // a bit position & 63 selects the bit within a word
    private const int SampleShift = 7;   // log2(128): one select sample per 128 ones, and per 128 zeros
    private const int SuperShift = 8;    // log2(256): one rank-directory entry per 256-bit superblock

    // _lowWords packs the L low bits of every value end to end, value i at bit offset i * L. _highWords is the
    // unary high-part vector: value i sets bit (value >> L) + i, and high value b ends at the b-th zero.
    // _oneSamples[k] is the bit position of the (k * 128)-th one, _zeroSamples[k] that of the (k * 128)-th zero.
    // _superOnes[s] counts the ones before 256-bit superblock s of the high vector; the zeros before it are
    // derived as s * 256 minus that. Two consecutive samples bracket a select's target, and the directory
    // narrows the bracket to one superblock, so the final scan never covers more than four words.
    private readonly ulong[] _lowWords;
    private readonly ulong[] _highWords;
    private readonly long[] _oneSamples;
    private readonly long[] _zeroSamples;
    private readonly int[] _superOnes;
    private readonly int _count;
    private readonly int _lowBits;
    private readonly ulong _lowMask;
    private readonly long _maxHigh;   // the high part of the last value; -1 for an empty sequence

    /// <summary>
    /// Encodes <paramref name="values"/>.
    /// </summary>
    /// <param name="values">
    /// The values to encode, which must be non-negative and in non-decreasing order; repeats are allowed. They
    /// are copied into the encoding, so the caller's buffer may be reused or freed. A <c>long[]</c> converts
    /// implicitly; a <see cref="List{T}"/> reaches it through
    /// <see cref="System.Runtime.InteropServices.CollectionsMarshal.AsSpan{T}(List{T})"/> without a copy.
    /// There is deliberately no <see cref="IEnumerable{T}"/> overload: the encoding needs the count and the
    /// largest value before it writes anything, and a <c>long[]</c> argument would convert to both.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The first value is negative.</exception>
    /// <exception cref="ArgumentException">A value is smaller than the one before it.</exception>
    /// <remarks>
    /// Building is <c>O(n)</c> in three passes: one to validate the input — which has to finish first, because
    /// the split point is chosen from the last value — one to write the two bit vectors, and one over the high
    /// bit vector to fill the rank directory and take the select samples. The only allocations are the five
    /// arrays the instance keeps.
    /// </remarks>
    public EliasFanoSequence(ReadOnlySpan<long> values)
    {
        Validate(values);

        _count = values.Length;
        if (_count == 0)
        {
            _lowWords = [];
            _highWords = [];
            _oneSamples = [];
            _zeroSamples = [];
            _superOnes = [];
            _maxHigh = -1;
            return;
        }

        ulong max = (ulong)values[^1];
        ulong quotient = max / (ulong)_count;
        _lowBits = quotient == 0 ? 0 : BitOperations.Log2(quotient);
        _lowMask = (1UL << _lowBits) - 1;
        _maxHigh = (long)(max >> _lowBits);

        // _maxHigh < 2n by the choice of L, so the high vector is under 3n + 1 bits and every position fits
        // comfortably in a long; the low vector is at most 62 bits a value.
        long highLength = _count + _maxHigh + 1;
        _highWords = new ulong[WordCount(highLength)];
        _lowWords = new ulong[WordCount((long)_count * _lowBits)];

        for (int i = 0; i < values.Length; i++)
        {
            ulong value = (ulong)values[i];
            long position = (long)(value >> _lowBits) + i;
            _highWords[position >> WordShift] |= 1UL << (int)(position & WordMask);
            WriteLow(i, value & _lowMask);
        }

        _oneSamples = new long[((_count - 1) >> SampleShift) + 1];
        _zeroSamples = new long[(int)(_maxHigh >> SampleShift) + 1];
        _superOnes = new int[(_highWords.Length + (1 << (SuperShift - WordShift)) - 1) >> (SuperShift - WordShift)];
        TakeSamples();
    }

    /// <summary>Gets the number of values in the sequence.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets the number of low bits <c>L</c> each value is split at, <c>floor(log2(max / Count))</c>. The low
    /// parts cost exactly <c>L</c> bits a value; the high-part vector, the select samples and the rank directory
    /// add about four more on a large, typical sequence and under five on any large one. A short sequence pays
    /// more per value, because each array rounds up to a whole word or entry. <c>0</c> for an empty
    /// sequence or one whose largest value is below its count.
    /// </summary>
    public int LowBitCount => _lowBits;

    /// <summary>
    /// Gets the size in bytes of the encoding: the low-part words, the high-part words, both select samples
    /// and the rank directory. Compare it against <c>Count * sizeof(long)</c>, the array it replaces.
    /// </summary>
    /// <remarks>
    /// This is payload, not retained-object size: it excludes the CLR's per-array header and the instance's
    /// own fields, the same accounting <see cref="RankSelectBitVector.IndexSizeInBytes"/> uses. Those are a
    /// fixed few tens of bytes and do not scale with the data. The figure is a <see cref="long"/>, as
    /// <see cref="CompressedIntSet.MemoryUsageInBytes"/> is, because a large sequence of widely spread values
    /// passes <see cref="int.MaxValue"/> bytes.
    /// </remarks>
    public long MemoryUsageInBytes =>
        (((long)_lowWords.Length + _highWords.Length + _oneSamples.Length + _zeroSamples.Length) * sizeof(ulong))
        + ((long)_superOnes.Length * sizeof(int));

    /// <summary>Gets the value at the specified position.</summary>
    /// <param name="index">The zero-based position, over <c>[0, Count)</c>.</param>
    /// <returns>The value stored there.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    /// <remarks>
    /// One select over the high vector and one read of the low bits, against the array's single load. The
    /// select is <c>O(1)</c> on typical data and <c>O(log n)</c> in the worst case (see the type's remarks). A
    /// caller that walks the values in order should enumerate instead, which decodes each value from the one
    /// before it.
    /// </remarks>
    public long this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, Count).");

            return Get(index);
        }
    }

    /// <summary>
    /// Returns how many values are strictly less than <paramref name="value"/> — which is also the position
    /// of the first value at or above it, the <c>lower_bound</c> of the ordered containers.
    /// </summary>
    /// <param name="value">The bound. Any <see cref="long"/> is accepted; a negative bound yields <c>0</c>.</param>
    /// <returns>A count in <c>[0, Count]</c>; <see cref="Count"/> when every value is smaller.</returns>
    /// <remarks>
    /// Two zero-selects locate the run of values sharing <paramref name="value"/>'s high part, and a binary
    /// search of their low bits finishes the job. On spread-out data the selects are <c>O(1)</c> and the run
    /// holds a value or two, so the query is constant-time. A sequence that piles many values onto one high
    /// part — long runs of repeats, tight clusters far above the rest — pays <c>O(log r)</c> for a run of
    /// <c>r</c> values, and the selects that bound the run pay at most <c>O(log n)</c>.
    /// </remarks>
    public int CountLessThan(long value) => LowerBound(value, out _);

    /// <summary>
    /// Finds the smallest value that is <b>greater than or equal to</b> <paramref name="value"/> — the
    /// successor query, named after <see cref="BTreeSet{T, TComparer}.TryGetLowerBound"/>. An exact match is
    /// its own lower bound.
    /// </summary>
    /// <param name="value">The value to bound.</param>
    /// <param name="bound">The bounding value, or <c>0</c> when every value is smaller.</param>
    /// <returns><c>true</c> if such a value exists.</returns>
    /// <remarks>The cost of <see cref="CountLessThan"/>, plus one positional read when the match is not exact.</remarks>
    public bool TryGetLowerBound(long value, out long bound)
    {
        int index = LowerBound(value, out bool found);
        if (index == _count)
        {
            bound = 0;
            return false;
        }

        bound = found ? value : Get(index);
        return true;
    }

    /// <summary>Determines whether the sequence holds <paramref name="value"/>.</summary>
    /// <param name="value">The value to locate.</param>
    /// <returns><c>true</c> if at least one element equals <paramref name="value"/>.</returns>
    /// <remarks>The cost of <see cref="CountLessThan"/>.</remarks>
    public bool Contains(long value)
    {
        LowerBound(value, out bool found);
        return found;
    }

    /// <summary>
    /// Returns the position of the first occurrence of <paramref name="value"/>, or <c>-1</c> when the sequence
    /// does not hold it.
    /// </summary>
    /// <param name="value">The value to locate.</param>
    /// <returns>The zero-based position of the first element equal to <paramref name="value"/>, or <c>-1</c>.</returns>
    /// <remarks>
    /// The cost of <see cref="CountLessThan"/>; repeats do not make it slower than one occurrence does, beyond
    /// the logarithm of the run they form.
    /// </remarks>
    public int IndexOf(long value)
    {
        int index = LowerBound(value, out bool found);
        return found ? index : -1;
    }

    /// <summary>
    /// Returns an enumerator that decodes the values in order without allocating.
    /// </summary>
    /// <returns>A struct enumerator over the values.</returns>
    /// <remarks>
    /// Sequential decoding needs no select at all: the enumerator walks the set bits of the high vector one
    /// after another, <c>O(1)</c> amortized a value and <c>O(n)</c> for the whole pass (the high vector is under
    /// <c>3n + 1</c> bits), which is the cheapest way to read the sequence back.
    /// </remarks>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<long> IEnumerable<long>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // The value at an index already known to be in range: the high part is where the index-th one sits, less
    // the index (every one before it moved it one position right), and the low part is read verbatim.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long Get(int index)
    {
        long high = SelectOne(index) - index;
        return (long)(((ulong)high << _lowBits) | ReadLow(index));
    }

    // The position of the first value >= `value`, and whether that value equals it. Every value with high part
    // h sits between the (h - 1)-th zero and the h-th, and they are sorted by their low bits, so the answer is
    // a binary search of the low bits over that run alone.
    private int LowerBound(long value, out bool found)
    {
        found = false;
        if (value < 0 || _count == 0)
            return 0;

        long high = (long)((ulong)value >> _lowBits);
        if (high > _maxHigh)
            return _count;

        // Ones before a zero at position p number p minus the zeros before it, which is how a zero position
        // converts to a value index. High part 0 has no zero before it, so its run starts at the front.
        int lo = high == 0 ? 0 : (int)(SelectZero(high - 1) + 1 - high);
        int end = (int)(SelectZero(high) - high);
        int hi = end;

        ulong target = (ulong)value & _lowMask;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (ReadLow(mid) < target)
                lo = mid + 1;
            else
                hi = mid;
        }

        // A value inside the run shares the high part, so equality of the low bits is equality of the values.
        found = lo < end && ReadLow(lo) == target;
        return lo;
    }

    // Position of the rank-th one (zero-based) in the high vector. The sample at or below the rank and the one
    // after it bracket the target; a binary search of the rank directory between them finds its superblock,
    // and a scan of at most four words finishes. On data whose values spread out, 128 ones span a superblock
    // or two and the search is a step or none; only a long gap between consecutive high parts — values far
    // apart relative to the rest — widens the bracket, and then the cost is logarithmic in the gap rather than
    // linear in it.
    private long SelectOne(int rank)
    {
        int sample = rank >> SampleShift;
        int lo = (int)(_oneSamples[sample] >> SuperShift);
        int hi = sample + 1 < _oneSamples.Length ? (int)(_oneSamples[sample + 1] >> SuperShift) : _superOnes.Length - 1;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo + 1) >> 1);
            if (_superOnes[mid] <= rank)
                lo = mid;
            else
                hi = mid - 1;
        }

        int remaining = rank - _superOnes[lo];
        int word = lo << (SuperShift - WordShift);
        ulong bits = _highWords[word];

        int inWord;
        while ((inWord = BitOperations.PopCount(bits)) <= remaining)
        {
            remaining -= inWord;
            bits = _highWords[++word];
        }

        return ((long)word << WordShift) + RankSelectBitVector.SelectInWord(bits, remaining);
    }

    // The complement of SelectOne, with the zeros before a superblock derived from the ones before it. A long
    // run of ones — many values sharing one high part — is what widens this bracket. Padding bits past the end
    // of the high vector are zero, but they sit above the last real zero, so an in-range rank never resolves to
    // one.
    private long SelectZero(long rank)
    {
        int sample = (int)(rank >> SampleShift);
        int lo = (int)(_zeroSamples[sample] >> SuperShift);
        int hi = sample + 1 < _zeroSamples.Length ? (int)(_zeroSamples[sample + 1] >> SuperShift) : _superOnes.Length - 1;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo + 1) >> 1);
            if (ZerosBefore(mid) <= rank)
                lo = mid;
            else
                hi = mid - 1;
        }

        int remaining = (int)(rank - ZerosBefore(lo));
        int word = lo << (SuperShift - WordShift);
        ulong bits = ~_highWords[word];

        int inWord;
        while ((inWord = BitOperations.PopCount(bits)) <= remaining)
        {
            remaining -= inWord;
            bits = ~_highWords[++word];
        }

        return ((long)word << WordShift) + RankSelectBitVector.SelectInWord(bits, remaining);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long ZerosBefore(int superblock) => ((long)superblock << SuperShift) - _superOnes[superblock];

    // The low part of value `index`, which may straddle two words.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong ReadLow(int index)
    {
        if (_lowBits == 0)
            return 0;

        long bit = (long)index * _lowBits;
        int word = (int)(bit >> WordShift);
        int shift = (int)(bit & WordMask);
        ulong low = _lowWords[word] >> shift;
        if (shift + _lowBits > 64)
            low |= _lowWords[word + 1] << (64 - shift);

        return low & _lowMask;
    }

    private void WriteLow(int index, ulong low)
    {
        if (_lowBits == 0)
            return;

        long bit = (long)index * _lowBits;
        int word = (int)(bit >> WordShift);
        int shift = (int)(bit & WordMask);
        _lowWords[word] |= low << shift;
        if (shift + _lowBits > 64)
            _lowWords[word + 1] |= low >> (64 - shift);
    }

    // One pass over the high vector filling the rank directory and recording where every 128th one and every
    // 128th zero sits. A word holds at most 64 of either, so it can contain at most one sample of each kind.
    // The padding past the vector's end
    // is all zeros, but the zero samples are sized for the real zeros alone (one per 128 of the _maxHigh + 1),
    // so the last sample is taken before the scan can reach the padding.
    private void TakeSamples()
    {
        long onesBefore = 0;
        long zerosBefore = 0;
        int nextOne = 0;
        int nextZero = 0;

        for (int word = 0; word < _highWords.Length; word++)
        {
            ulong bits = _highWords[word];
            int ones = BitOperations.PopCount(bits);

            if ((word & ((1 << (SuperShift - WordShift)) - 1)) == 0)
                _superOnes[word >> (SuperShift - WordShift)] = (int)onesBefore;

            long oneTarget = (long)nextOne << SampleShift;
            if (nextOne < _oneSamples.Length && onesBefore + ones > oneTarget)
            {
                _oneSamples[nextOne++] = ((long)word << WordShift)
                    + RankSelectBitVector.SelectInWord(bits, (int)(oneTarget - onesBefore));
            }

            long zeroTarget = (long)nextZero << SampleShift;
            if (nextZero < _zeroSamples.Length && zerosBefore + (64 - ones) > zeroTarget)
            {
                _zeroSamples[nextZero++] = ((long)word << WordShift)
                    + RankSelectBitVector.SelectInWord(~bits, (int)(zeroTarget - zerosBefore));
            }

            onesBefore += ones;
            zerosBefore += 64 - ones;
        }
    }

    private static void Validate(ReadOnlySpan<long> values)
    {
        if (values.Length == 0)
            return;

        if (values[0] < 0)
            throw new ArgumentOutOfRangeException(nameof(values), values[0], "Values must be non-negative.");

        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] < values[i - 1])
                throw new ArgumentException(
                    $"Values must be in non-decreasing order, but the value at index {i} ({values[i]}) is smaller than the one before it ({values[i - 1]}).",
                    nameof(values));
        }
    }

    private static int WordCount(long bitCount) => checked((int)((bitCount + WordMask) >> WordShift));

    /// <summary>
    /// Decodes the values in order without allocating, walking the set bits of the high vector one after
    /// another.
    /// </summary>
    public struct Enumerator : IEnumerator<long>
    {
        private readonly EliasFanoSequence _sequence;
        private int _index;
        private int _word;
        private ulong _bits;
        private long _current;

        internal Enumerator(EliasFanoSequence sequence)
        {
            _sequence = sequence;
            _index = -1;
            _word = -1;
            _bits = 0;
            _current = 0;
        }

        /// <summary>Gets the value at the current position.</summary>
        public readonly long Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances to the next value.</summary>
        /// <returns><c>true</c> while a value remains; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            EliasFanoSequence sequence = _sequence;
            if (_index + 1 >= sequence._count)
            {
                _index = sequence._count;
                _current = 0;
                return false;
            }

            while (_bits == 0)
                _bits = sequence._highWords[++_word];

            long position = ((long)_word << WordShift) + BitOperations.TrailingZeroCount(_bits);
            _bits &= _bits - 1;
            _index++;

            long high = position - _index;
            _current = (long)(((ulong)high << sequence._lowBits) | sequence.ReadLow(_index));
            return true;
        }

        /// <summary>Resets the enumerator to before the first value.</summary>
        public void Reset()
        {
            _index = -1;
            _word = -1;
            _bits = 0;
            _current = 0;
        }

        /// <summary>Does nothing; the enumerator holds no resources.</summary>
        public readonly void Dispose()
        {
        }
    }
}
