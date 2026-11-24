using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Celerity.Collections;

/// <summary>
/// The approximate, self-ageing access-frequency counter behind <see cref="TinyLfuCache{TKey, TValue, THasher}"/>:
/// a count-min sketch of 4-bit saturating counters, sixteen to a <see cref="ulong"/>, whose counters are
/// all halved each time the number of recorded accesses reaches a sample size proportional to the
/// cache's capacity.
/// </summary>
/// <remarks>
/// <para>
/// Each key maps to one 64-byte block of eight words and to four counters inside it, one in each pair
/// of words, picked from a 64-bit mix of the key's 32-bit hash (the layout of Caffeine's sketch, so a
/// key's counters share a cache line); its estimate is the smallest of the four, which collisions can
/// only inflate.
/// A counter saturates at 15. TinyLFU only ever compares two estimates, and once a key has been seen 15
/// times inside one sample period it has beaten nearly every candidate it will meet, so four bits buy
/// the same decisions as a full count at an eighth of the memory.
/// </para>
/// <para>
/// The halving is what makes the frequencies <i>recent</i>: an access counts fully until the next
/// halving, half after it, a quarter after the one after. A key that was hot long ago therefore loses
/// to a key that is hot now, which is the failure an unaged LFU cannot avoid. This is the reset
/// operation of Einziger, Friedman and Manes, <i>"TinyLFU: A Highly Efficient Cache Admission
/// Policy"</i>, with the sample size at ten times the capacity, as in their paper and in Caffeine. The
/// sample size bounds the accesses the sketch <i>holds</i>, not the gap between halvings: a halving
/// keeps at most half of what was recorded, so the first comes after ten times the capacity and later
/// ones after five to ten times, depending on how much of the sample sits in odd counters (about
/// six times on uniform and all-unique traffic).
/// </para>
/// <para>
/// The table holds one <see cref="ulong"/> (sixteen counters) per unit of capacity, rounded up to a
/// power of two and capped at 2<sup>27</sup> words (1 GiB), so it costs 8&#8211;16 bytes per cache entry, depending on how far the rounding reaches.
/// It is a mutable struct: the cache holds it in a non-<see langword="readonly"/> field and calls it in
/// place.
/// </para>
/// </remarks>
internal struct FrequencySketch
{
    // Sixteen 4-bit counters per word. The masks pick the low bit of every counter and clear the high
    // bit of every counter after a right shift, so a halving is one shift and one AND per word.
    private const ulong OneMask = 0x1111_1111_1111_1111UL;
    private const ulong ResetMask = 0x7777_7777_7777_7777UL;
    private const int MaxTableLength = 1 << 27;
    private const int MinTableLength = 8;

    private readonly ulong[] _table;
    private readonly uint _blockMask;
    private readonly int _sampleSize;
    private int _size;

    /// <summary>
    /// Allocates an empty sketch sized for a cache of <paramref name="capacity"/> entries.
    /// </summary>
    /// <param name="capacity">The cache's capacity. The caller has validated it is at least 1.</param>
    internal FrequencySketch(int capacity)
    {
        int length = (int)Math.Clamp(BitOperations.RoundUpToPowerOf2((uint)capacity), MinTableLength, MaxTableLength);
        _table = new ulong[length];
        _blockMask = (uint)(length >> 3) - 1;
        _sampleSize = (int)Math.Min(10L * capacity, int.MaxValue);
        _size = 0;
    }

    /// <summary>
    /// Returns the estimated number of recent accesses to the key whose hash is <paramref name="hash"/>,
    /// from <c>0</c> to <c>15</c>.
    /// </summary>
    internal readonly int Frequency(int hash)
    {
        ref ulong block = ref Block(hash, out uint counterHash);
        int min = 15;
        for (int i = 0; i < 4; i++)
        {
            uint h = counterHash >> (i << 3);
            int shift = (int)((h >> 1) & 15) << 2;
            ulong word = Unsafe.Add(ref block, (int)(h & 1) + (i << 1));
            min = Math.Min(min, (int)((word >> shift) & 15));
        }
        return min;
    }

    /// <summary>
    /// Records one access to the key whose hash is <paramref name="hash"/>, halving every counter if
    /// that brings the recorded accesses up to the sample size.
    /// </summary>
    internal void Increment(int hash)
    {
        ref ulong block = ref Block(hash, out uint counterHash);
        ulong added = Bump(ref block, counterHash, 0)
                    | Bump(ref block, counterHash >> 8, 2)
                    | Bump(ref block, counterHash >> 16, 4)
                    | Bump(ref block, counterHash >> 24, 6);

        // An access whose four counters had all saturated adds nothing, so it does not advance the
        // period either; the key is already as hot as the sketch can say.
        if (added != 0 && ++_size == _sampleSize)
            Halve();
    }

    // Adds one to the counter that bits h pick in the word pair at offset row, unless it has saturated at
    // 15, without a branch. Returns the amount added, 1 or 0.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Bump(ref ulong block, uint h, int row)
    {
        int shift = (int)((h >> 1) & 15) << 2;
        ref ulong word = ref Unsafe.Add(ref block, (int)(h & 1) + row);
        ulong inc = (((word >> shift) & 15) + 1) >> 4 ^ 1;
        word += inc << shift;
        return inc;
    }

    /// <summary>Forgets every recorded access.</summary>
    internal void Clear()
    {
        Array.Clear(_table);
        _size = 0;
    }

    // Halves every counter. A counter that was odd loses half an access to the truncation, and each key
    // owns four counters, so the recorded size drops by a quarter of the odd counters before halving,
    // keeping it an honest count of what the table still holds.
    private void Halve()
    {
        ulong[] table = _table;
        int odd = 0;
        for (int i = 0; i < table.Length; i++)
        {
            ulong word = table[i];
            odd += BitOperations.PopCount(word & OneMask);
            table[i] = (word >> 1) & ResetMask;
        }
        _size = (_size - (odd >> 2)) >> 1;
    }

    // Maps a hash to its 64-byte block (eight words) and the bits that pick its four counters in it,
    // as Caffeine's sketch does: row i reads word 2i or 2i + 1 of the block, so a key's counters share
    // one cache line. The 32-bit hash goes through a 64-bit multiply-xorshift mix first, so an identity
    // or otherwise weak integer hasher still spreads over the table. The block mask keeps the eight
    // reads inside the table, which is why they need no bounds check.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly ref ulong Block(int hash, out uint counterHash)
    {
        ulong z = ((uint)hash + 0x9E37_79B9_7F4A_7C15UL) * 0xBF58_476D_1CE4_E5B9UL;
        z = (z ^ (z >> 31)) * 0x94D0_49BB_1331_11EBUL;
        z ^= z >> 29;
        counterHash = (uint)z;
        int block = (int)((uint)(z >> 32) & _blockMask) << 3;
        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_table), block);
    }
}
