using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable</b> succinct index over a dense bit vector that answers <see cref="Rank(int)"/> — how many
/// bits are set below a position — in <c>O(1)</c>, and <see cref="Select(int)"/> — the position of the
/// <c>k</c>-th set bit — in <c>O(log n)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Build once, query many.</b> The vector is a snapshot taken at construction and there is no mutating
/// member: a caller that changes the underlying bits must build a new <see cref="RankSelectBitVector"/>, which
/// costs <c>O(Length / 64)</c>. That makes this the wrong type for anything that mutates as it queries — free-slot
/// allocators, ECS id compaction, live "visited" sets — where a rebuild per update is strictly worse than the
/// naive counting loop. Reach for <see cref="BitSet"/> (or <see cref="SparseSet"/>) there and snapshot into a
/// <see cref="RankSelectBitVector"/> only once the bits have settled.
/// </para>
/// <para>
/// The BCL has no counterpart. <see cref="System.Collections.BitArray"/> exposes neither rank nor select,
/// <see cref="BitOperations.PopCount(ulong)"/> is per-word, and nothing in .NET 8/9/10 ships succinct
/// data-structure support — so the honest baseline is the loop a caller writes by hand, popcounting every word
/// below the query position. That loop is <c>O(index / 64)</c>: at the midpoint of a 100-million-bit vector it is
/// roughly 780,000 iterations, which this type replaces with two index loads and one masked population count,
/// at a cost independent of the position.
/// </para>
/// <para>
/// The documented winning workloads are all build-once: dense↔sparse index remapping in column stores (map a
/// dense row ordinal to its position in a sparse column and back), succinct and compressed tries, and wavelet
/// trees. They share the shape this type is designed for — a bit vector that is filled, frozen, then queried
/// many times.
/// </para>
/// <para>
/// <b>Layout and space.</b> The index is two arrays: an <see cref="int"/> per 256-bit superblock holding the
/// number of set bits before it, plus a <see cref="byte"/> per 64-bit word holding the number of set bits before
/// that word <i>within its superblock</i> (at most 192, so a byte suffices — which is what pins the superblock at
/// 256 bits). Together they cost 8 bytes per 32 bytes of vector, i.e. <b>25% over the bits themselves</b>, the
/// same price as the classic rank9 layout and the standard cost of an exact rank that touches a single word.
/// <see cref="IndexSizeInBytes"/> reports the exact figure for a given instance.
/// </para>
/// <para>
/// The type holds no mutable state after construction, so instances are safe to share across threads.
/// </para>
/// </remarks>
public sealed class RankSelectBitVector
{
    private const int WordShift = 6;        // log2(64): index >> 6 selects the word
    private const int WordMask = 63;        // index & 63 selects the bit within a word
    private const int SuperblockShift = 8;  // log2(256): index >> 8 selects the superblock
    private const int WordsPerSuperblock = 1 << (SuperblockShift - WordShift);

    private readonly ulong[] _words;
    private readonly int[] _superRanks;   // set bits before each superblock, from the start of the vector
    private readonly byte[] _blockRanks;  // set bits before each word, from the start of its superblock
    private readonly int _length;
    private readonly int _count;

    /// <summary>
    /// Initializes a new <see cref="RankSelectBitVector"/> over a snapshot of <paramref name="bits"/>. Later
    /// mutations of <paramref name="bits"/> do not affect the vector.
    /// </summary>
    /// <param name="bits">The bit set to snapshot and index.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bits"/> is <c>null</c>.</exception>
    public RankSelectBitVector(BitSet bits)
        : this(SnapshotOf(bits), bits.Length)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="RankSelectBitVector"/> of <paramref name="length"/> bits over a copy of
    /// <paramref name="words"/>, where the bit at index <c>i</c> is bit <c>i % 64</c> of word <c>i / 64</c>.
    /// </summary>
    /// <param name="length">The number of bits. Must be non-negative.</param>
    /// <param name="words">
    /// The packed words. Must hold at least <c>ceil(length / 64)</c> entries; any further words, and any bits at
    /// or beyond <paramref name="length"/> in the final word, are ignored.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="words"/> is too short to hold <paramref name="length"/> bits.
    /// </exception>
    public RankSelectBitVector(int length, ReadOnlySpan<ulong> words)
        : this(SnapshotOf(length, words), length)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="RankSelectBitVector"/> of <paramref name="length"/> bits with the bits at
    /// <paramref name="positions"/> set. Repeated positions are idempotent.
    /// </summary>
    /// <param name="length">The number of bits. Must be non-negative.</param>
    /// <param name="positions">The zero-based positions to set, in any order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="positions"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is negative, or <paramref name="positions"/> yields a position outside
    /// <c>[0, length)</c>.
    /// </exception>
    public RankSelectBitVector(int length, IEnumerable<int> positions)
        : this(SnapshotOf(length, positions), length)
    {
    }

    // Takes ownership of `words`, which is always a private copy produced by one of the SnapshotOf helpers and
    // already masked clear at or beyond `length`, and builds the two-level index over it in O(length / 64).
    private RankSelectBitVector(ulong[] words, int length)
    {
        _words = words;
        _length = length;
        _superRanks = new int[SuperblockCount(length)];
        _blockRanks = new byte[words.Length];

        int total = 0;
        for (int s = 0; s < _superRanks.Length; s++)
        {
            _superRanks[s] = total;

            int within = 0;
            int first = s << (SuperblockShift - WordShift);
            int end = Math.Min(first + WordsPerSuperblock, words.Length);
            for (int w = first; w < end; w++)
            {
                _blockRanks[w] = (byte)within;
                within += BitOperations.PopCount(words[w]);
            }

            total += within;
        }

        _count = total;
    }

    /// <summary>
    /// Gets the number of bits in the vector.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets the number of set bits in the whole vector. Precomputed at construction, so this is a field read
    /// rather than the <c>O(Length / 64)</c> scan <see cref="BitSet.Count"/> performs.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the size in bytes of the rank/select index, excluding the bits themselves. This is the space price of
    /// the constant-time rank — 25% of the vector, rounded up to whole superblocks and words.
    /// </summary>
    public int IndexSizeInBytes => (_superRanks.Length * sizeof(int)) + _blockRanks.Length;

    /// <summary>
    /// Gets the value of the bit at the specified index.
    /// </summary>
    /// <param name="index">The zero-based bit index.</param>
    /// <returns><c>true</c> if the bit is set; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Length"/>.
    /// </exception>
    public bool this[int index] => Get(index);

    /// <summary>
    /// Returns the value of the bit at the specified index.
    /// </summary>
    /// <param name="index">The zero-based bit index.</param>
    /// <returns><c>true</c> if the bit is set; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Length"/>.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int index)
    {
        if ((uint)index >= (uint)_length)
            ThrowIndexOutOfRange(index);

        return (_words[index >> WordShift] & (1UL << (index & WordMask))) != 0;
    }

    /// <summary>
    /// Returns the number of set bits at positions strictly below <paramref name="index"/>, in <c>O(1)</c>.
    /// </summary>
    /// <param name="index">
    /// The exclusive upper bound, from <c>0</c> to <see cref="Length"/> inclusive. <c>Rank(0)</c> is <c>0</c> and
    /// <c>Rank(Length)</c> is <see cref="Count"/>.
    /// </param>
    /// <returns>The number of set bits below <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than <see cref="Length"/>.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Rank(int index)
    {
        if ((uint)index > (uint)_length)
            ThrowRankIndexOutOfRange(index);

        // Length itself has no word of its own when the vector ends on a word boundary, and the answer is the
        // precomputed total either way.
        if (index == _length)
            return _count;

        int word = index >> WordShift;
        ulong below = _words[word] & ((1UL << (index & WordMask)) - 1);
        return _superRanks[index >> SuperblockShift] + _blockRanks[word] + BitOperations.PopCount(below);
    }

    /// <summary>
    /// Returns the number of <i>clear</i> bits at positions strictly below <paramref name="index"/>, the
    /// complement identity <c>index - Rank(index)</c>.
    /// </summary>
    /// <param name="index">The exclusive upper bound, from <c>0</c> to <see cref="Length"/> inclusive.</param>
    /// <returns>The number of clear bits below <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than <see cref="Length"/>.
    /// </exception>
    public int Rank0(int index) => index - Rank(index);

    /// <summary>
    /// Returns the position of the <paramref name="rank"/>-th set bit, counting from zero, in <c>O(log n)</c>.
    /// </summary>
    /// <param name="rank">The zero-based ordinal of the set bit, from <c>0</c> to <see cref="Count"/> minus one.</param>
    /// <returns>The position <c>p</c> such that <c>Get(p)</c> is <c>true</c> and <c>Rank(p) == rank</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rank"/> is negative or not less than <see cref="Count"/>. Use <see cref="TrySelect"/> to
    /// probe an out-of-range ordinal without an exception.
    /// </exception>
    public int Select(int rank)
    {
        if ((uint)rank >= (uint)_count)
            ThrowRankOutOfRange(rank);

        return SelectCore(rank);
    }

    /// <summary>
    /// Attempts to locate the <paramref name="rank"/>-th set bit, counting from zero.
    /// </summary>
    /// <param name="rank">The zero-based ordinal of the set bit.</param>
    /// <param name="position">
    /// When this method returns <c>true</c>, the position of that set bit; otherwise <c>-1</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if <paramref name="rank"/> is in <c>[0, Count)</c>; otherwise <c>false</c>.
    /// </returns>
    public bool TrySelect(int rank, out int position)
    {
        if ((uint)rank >= (uint)_count)
        {
            position = -1;
            return false;
        }

        position = SelectCore(rank);
        return true;
    }

    /// <summary>
    /// Returns a new, mutable <see cref="BitSet"/> holding a copy of the indexed bits — the way to edit a vector
    /// and then rebuild the index over the result.
    /// </summary>
    /// <returns>A <see cref="BitSet"/> of <see cref="Length"/> bits with the same bits set.</returns>
    public BitSet ToBitSet() => new((ulong[])_words.Clone(), _length);

    // Binary-search the superblock index, walk the (at most four) words inside it, then resolve within the word.
    private int SelectCore(int rank)
    {
        // The last superblock whose prefix count is <= rank is the one holding the target bit: the prefix counts
        // are non-decreasing, so the next superblock starting above rank means the bit has not been passed yet.
        int lo = 0;
        int hi = _superRanks.Length - 1;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo + 1) >> 1);
            if (_superRanks[mid] <= rank)
                lo = mid;
            else
                hi = mid - 1;
        }

        int remaining = rank - _superRanks[lo];
        int word = lo << (SuperblockShift - WordShift);
        int end = Math.Min(word + WordsPerSuperblock, _words.Length);
        while (word + 1 < end && _blockRanks[word + 1] <= remaining)
            word++;

        return (word << WordShift) + SelectInWord(_words[word], remaining - _blockRanks[word]);
    }

    // Position of the `rank`-th set bit (counting from zero) of a word known to hold more than `rank` set bits,
    // by binary search over the bit positions: each step keeps the half that still contains the target.
    private static int SelectInWord(ulong word, int rank)
    {
        int position = 0;

        int lower = BitOperations.PopCount((uint)word);
        if (lower <= rank)
        {
            rank -= lower;
            word >>= 32;
            position += 32;
        }

        lower = BitOperations.PopCount(word & 0xFFFF);
        if (lower <= rank)
        {
            rank -= lower;
            word >>= 16;
            position += 16;
        }

        lower = BitOperations.PopCount(word & 0xFF);
        if (lower <= rank)
        {
            rank -= lower;
            word >>= 8;
            position += 8;
        }

        lower = BitOperations.PopCount(word & 0xF);
        if (lower <= rank)
        {
            rank -= lower;
            word >>= 4;
            position += 4;
        }

        lower = BitOperations.PopCount(word & 0x3);
        if (lower <= rank)
        {
            rank -= lower;
            word >>= 2;
            position += 2;
        }

        if ((int)(word & 1) <= rank)
            position++;

        return position;
    }

    private static ulong[] SnapshotOf(BitSet bits)
    {
        ArgumentNullException.ThrowIfNull(bits);
        return bits.SnapshotWords();
    }

    private static ulong[] SnapshotOf(int length, ReadOnlySpan<ulong> words)
    {
        ThrowIfNegativeLength(length);

        int wordCount = WordCount(length);
        if (words.Length < wordCount)
            throw new ArgumentException($"At least {wordCount} words are required to hold {length} bits.", nameof(words));

        ulong[] copy = new ulong[wordCount];
        words[..wordCount].CopyTo(copy);
        MaskTail(copy, length);
        return copy;
    }

    private static ulong[] SnapshotOf(int length, IEnumerable<int> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ThrowIfNegativeLength(length);

        ulong[] words = new ulong[WordCount(length)];
        foreach (int position in positions)
        {
            if ((uint)position >= (uint)length)
                throw new ArgumentOutOfRangeException(nameof(positions), position,
                    "Bit position was outside the bounds of the vector.");

            words[position >> WordShift] |= 1UL << (position & WordMask);
        }

        return words;
    }

    // Bits beyond `length` in the final word must be clear, or they would be counted by the index and reported by
    // Rank / Count / Select as if they were part of the vector.
    private static void MaskTail(ulong[] words, int length)
    {
        int rem = length & WordMask;
        if (rem != 0)
            words[words.Length - 1] &= (1UL << rem) - 1;
    }

    private static int WordCount(int length) => (int)(((uint)length + WordMask) >> WordShift);

    private static int SuperblockCount(int length) =>
        (int)(((uint)length + ((1u << SuperblockShift) - 1)) >> SuperblockShift);

    private static void ThrowIfNegativeLength(int length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be non-negative.");
    }

    private static void ThrowIndexOutOfRange(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index), index, "Bit index was outside the bounds of the vector.");

    private static void ThrowRankIndexOutOfRange(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index), index,
            "Rank index must be between 0 and Length, inclusive.");

    private static void ThrowRankOutOfRange(int rank) =>
        throw new ArgumentOutOfRangeException(nameof(rank), rank,
            "Rank must be between 0 and Count minus one, inclusive.");
}
