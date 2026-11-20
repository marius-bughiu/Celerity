using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// The container engine behind <see cref="CompressedIntSet"/> and <see cref="CompressedLongSet"/>:
/// a sorted index of 65,536-value chunks, each stored as a sorted <c>ushort[]</c>, a 1024-word
/// bitmap or run-length pairs, plus the chunk-wise set algebra between two such indexes.
/// </summary>
/// <remarks>
/// <para>
/// The store knows nothing about the element type. A wrapper splits each value into a chunk
/// <em>key</em> (the bits above the low 16, with the sign bit flipped so that ascending unsigned key
/// order is ascending signed value order) and a 16-bit <em>offset</em> within the chunk, and composes
/// them back when it enumerates. A 32-bit wrapper's keys stay below 2^16 and a 64-bit wrapper's below
/// 2^48, so one <see cref="ulong"/> key serves both and the container code exists once.
/// </para>
/// <para>
/// It is a mutable struct held in a non-<see langword="readonly"/> field of its wrapper, so every call
/// operates on that field in place and a lookup pays no extra indirection. Never copy it: a copy shares
/// the chunk array but not the count, and the two would diverge on the next write. Operations that take
/// a second store take it by <see langword="ref"/> for the same reason.
/// </para>
/// <para>
/// The store does not track a version. Each mutator returns (or lets the caller derive from
/// <see cref="Cardinality"/>) whether anything observable changed, and the wrapper owns the enumerator
/// invalidation contract.
/// </para>
/// </remarks>
internal struct CompressedChunkStore
{
    internal const int BitmapWords = 1024;          // 65,536 bits / 64
    private const int BitmapBytes = BitmapWords * 8;

    // The array/bitmap crossover, and the reason it is 4096: 4096 ushorts and 1024 ulongs are both
    // 8 KB, so above this point the bitmap is never larger and answers Contains in O(1) rather than
    // O(log n). It is also the cap on how many ushorts an array container ever holds (one more,
    // transiently, on the insert that tips it over before it is promoted).
    private const int ArrayToBitmapThreshold = 4096;

    private const int InitialValueCapacity = 4;

    internal enum ContainerKind : byte
    {
        /// <summary>Sorted distinct offsets in <c>Values[0, Length)</c>.</summary>
        Array,

        /// <summary>A 1024-word bitmap over the chunk's 65,536 offsets.</summary>
        Bitmap,

        /// <summary>(start, lengthMinusOne) pairs in <c>Values[0, Length)</c>, ascending and disjoint.</summary>
        Run,
    }

    internal struct Chunk
    {
        public ulong Key;
        public ushort[]? Values;
        public ulong[]? Bitmap;
        public int Cardinality;
        public int Length;
        public ContainerKind Kind;
    }

    // Chunks, ascending by Key, in [0, ChunkCount). A chunk is never empty: the last removal from
    // one drops it, so a missing key means an empty range and a present key means at least one value.
    internal Chunk[] Chunks;
    internal int ChunkCount;
    internal long Cardinality;

    // Scratch used by the bulk container paths to assemble a result before it replaces the target,
    // so a set-algebra pass over thousands of chunks does not allocate a temporary per chunk. Values
    // are appended in ascending order and the builder switches to the bitmap once it outgrows the
    // array form, which caps the scratch at 8 KB + 8 KB regardless of the operand sizes.
    private ushort[]? _scratchValues;
    private ulong[]? _scratchBitmap;
    private int _scratchCount;
    private bool _scratchIsBitmap;

    public CompressedChunkStore()
    {
        Chunks = Array.Empty<Chunk>();
    }

    internal readonly long MemoryUsageInBytes
    {
        get
        {
            long total = (long)Chunks.Length * Unsafe.SizeOf<Chunk>();
            for (int i = 0; i < ChunkCount; i++)
            {
                ref Chunk c = ref Chunks[i];
                total += c.Kind == ContainerKind.Bitmap ? BitmapBytes : (long)c.Values!.Length * sizeof(ushort);
            }

            return total;
        }
    }

    // ── Single-element operations ─────────────────────────────────────────────

    internal bool Add(ulong key, ushort low)
    {
        int index = FindChunk(key);
        if (index < 0)
        {
            index = ~index;
            InsertEmptyChunk(index, key);
        }

        if (!ContainerAdd(ref Chunks[index], low))
            return false;

        Cardinality++;
        return true;
    }

    internal readonly bool Contains(ulong key, ushort low)
    {
        int index = FindChunk(key);
        return index >= 0 && ContainerContains(in Chunks[index], low);
    }

    internal bool Remove(ulong key, ushort low)
    {
        int index = FindChunk(key);
        if (index < 0)
            return false;

        if (!ContainerRemove(ref Chunks[index], low))
            return false;

        Cardinality--;
        if (Chunks[index].Cardinality == 0)
            RemoveChunkAt(index);

        return true;
    }

    // Adds every value from (firstKey, firstLow) to (lastKey, lastLow) inclusive. The caller has
    // already checked the range is non-empty and that it spans a storable number of chunks.
    // Cardinality is updated chunk by chunk rather than once at the end, so it stays equal to what
    // the index holds even if a wide range fails part-way through on an allocation.
    internal long AddRange(ulong firstKey, ushort firstLow, ulong lastKey, ushort lastLow)
    {
        long added = 0;
        for (ulong k = firstKey; k <= lastKey; k++)
        {
            ushort lo = k == firstKey ? firstLow : (ushort)0;
            ushort hi = k == lastKey ? lastLow : (ushort)0xFFFF;
            long chunkAdded = AddRunToChunk(k, lo, hi);
            Cardinality += chunkAdded;
            added += chunkAdded;
        }

        return added;
    }

    // Returns whether anything was removed.
    internal bool Clear()
    {
        if (Cardinality == 0)
            return false;

        Array.Clear(Chunks, 0, ChunkCount);
        ChunkCount = 0;
        Cardinality = 0;
        return true;
    }

    internal void Optimize()
    {
        for (int i = 0; i < ChunkCount; i++)
            OptimizeChunk(ref Chunks[i]);

        if (Chunks.Length != ChunkCount)
            Array.Resize(ref Chunks, ChunkCount);
    }

    // ── Chunk index ───────────────────────────────────────────────────────────

    // Binary search over the ascending chunk keys. Returns the index of the chunk, or the bitwise
    // complement of the index it would be inserted at.
    internal readonly int FindChunk(ulong key)
    {
        int lo = 0, hi = ChunkCount - 1;
        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            ulong probe = Chunks[mid].Key;
            if (probe < key)
                lo = mid + 1;
            else if (probe > key)
                hi = mid - 1;
            else
                return mid;
        }

        return ~lo;
    }

    private void InsertEmptyChunk(int index, ulong key)
    {
        MakeRoomForChunk(index);
        Chunks[index] = new Chunk
        {
            Key = key,
            Kind = ContainerKind.Array,
            Values = new ushort[InitialValueCapacity],
            Length = 0,
            Cardinality = 0,
        };
    }

    private void InsertRunChunk(int index, ulong key, ushort lo, ushort hi)
    {
        MakeRoomForChunk(index);
        Chunks[index] = new Chunk
        {
            Key = key,
            Kind = ContainerKind.Run,
            Values = new ushort[] { lo, (ushort)(hi - lo) },
            Length = 2,
            Cardinality = hi - lo + 1,
        };
    }

    private void MakeRoomForChunk(int index)
    {
        // Doubling is clamped to Array.MaxLength: a 32-bit wrapper never comes near it (at most
        // 65,536 chunks), but a 64-bit wrapper's index is bounded only by memory.
        if (ChunkCount == Chunks.Length)
            Array.Resize(ref Chunks, Chunks.Length == 0 ? 4 : (int)Math.Min(2L * Chunks.Length, Array.MaxLength));

        Array.Copy(Chunks, index, Chunks, index + 1, ChunkCount - index);
        ChunkCount++;
    }

    private void RemoveChunkAt(int index)
    {
        Array.Copy(Chunks, index + 1, Chunks, index, ChunkCount - index - 1);
        ChunkCount--;
        Chunks[ChunkCount] = default;
    }

    // ── Container reads ───────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool BitmapContains(ulong[] bitmap, ushort low) =>
        (bitmap[low >> 6] & (1UL << (low & 63))) != 0;

    private static bool ContainerContains(in Chunk c, ushort low)
    {
        if (c.Kind == ContainerKind.Bitmap)
            return BitmapContains(c.Bitmap!, low);

        if (c.Kind == ContainerKind.Array)
            return BinarySearchValues(c.Values!, c.Length, low) >= 0;

        // Run: locate the pair whose start is the greatest one not above `low`.
        ushort[] runs = c.Values!;
        int lo = 0, hi = (c.Length >> 1) - 1;
        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            int start = runs[mid << 1];
            if (low < start)
                hi = mid - 1;
            else if (low > start + runs[(mid << 1) + 1])
                lo = mid + 1;
            else
                return true;
        }

        return false;
    }

    // Returns the index of `value` in the sorted prefix, or the bitwise complement of its
    // insertion point.
    private static int BinarySearchValues(ushort[] values, int length, ushort value)
    {
        int lo = 0, hi = length - 1;
        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            ushort probe = values[mid];
            if (probe < value)
                lo = mid + 1;
            else if (probe > value)
                hi = mid - 1;
            else
                return mid;
        }

        return ~lo;
    }

    private static int PopCount(ulong[] bitmap)
    {
        int count = 0;
        for (int w = 0; w < BitmapWords; w++)
            count += BitOperations.PopCount(bitmap[w]);

        return count;
    }

    // ── Container form changes ────────────────────────────────────────────────

    // Materializes the chunk as a bitmap. A no-op when it already is one.
    private static void ToBitmap(ref Chunk c)
    {
        if (c.Kind == ContainerKind.Bitmap)
            return;

        ulong[] bits = new ulong[BitmapWords];
        var cursor = new ContainerCursor(in c);
        while (cursor.MoveNext(out ushort v))
            bits[v >> 6] |= 1UL << (v & 63);

        c.Bitmap = bits;
        c.Kind = ContainerKind.Bitmap;
        c.Values = null;
        c.Length = 0;
    }

    // Materializes the chunk as a sorted array. Only ever called when the cardinality fits one.
    private static void ToArray(ref Chunk c)
    {
        ushort[] values = new ushort[c.Cardinality];
        int n = 0;
        var cursor = new ContainerCursor(in c);
        while (cursor.MoveNext(out ushort v))
            values[n++] = v;

        c.Values = values;
        c.Length = n;
        c.Kind = ContainerKind.Array;
        c.Bitmap = null;
    }

    // Expands a run container back to whichever of array/bitmap suits its cardinality, because
    // every write path below assumes one of those two. A no-op for a chunk already in that form.
    private static void ToNaturalForm(ref Chunk c)
    {
        if (c.Kind != ContainerKind.Run)
            return;

        if (c.Cardinality > ArrayToBitmapThreshold)
            ToBitmap(ref c);
        else
            ToArray(ref c);
    }

    // Demotes a bitmap that a bulk rewrite left below the 4096 crossover. Every caller has just
    // produced a bitmap, so this is the only direction the crossover can be crossed here.
    private static void NormalizeBitmap(ref Chunk c)
    {
        if (c.Cardinality <= ArrayToBitmapThreshold)
            ToArray(ref c);
    }

    // Counts the maximal runs of consecutive values in the chunk.
    private static int CountRuns(in Chunk c)
    {
        int runs = 0;
        int previous = -2;
        var cursor = new ContainerCursor(in c);
        while (cursor.MoveNext(out ushort v))
        {
            if (v != previous + 1)
                runs++;
            previous = v;
        }

        return runs;
    }

    private static void ToRun(ref Chunk c, int runs)
    {
        ushort[] pairs = new ushort[runs << 1];
        int n = 0;
        int start = -1;
        int previous = -2;
        var cursor = new ContainerCursor(in c);
        while (cursor.MoveNext(out ushort v))
        {
            if (v != previous + 1)
            {
                if (start >= 0)
                {
                    pairs[n++] = (ushort)start;
                    pairs[n++] = (ushort)(previous - start);
                }

                start = v;
            }

            previous = v;
        }

        pairs[n++] = (ushort)start;
        pairs[n++] = (ushort)(previous - start);

        c.Values = pairs;
        c.Length = n;
        c.Kind = ContainerKind.Run;
        c.Bitmap = null;
    }

    private static void OptimizeChunk(ref Chunk c)
    {
        int naturalBytes = c.Cardinality <= ArrayToBitmapThreshold ? c.Cardinality * sizeof(ushort) : BitmapBytes;
        int runs = CountRuns(in c);

        // A run pair is two ushorts, so run encoding wins exactly when the data is clustered enough
        // that fewer than half as many pairs as the natural form needs entries.
        if (runs * 2 * sizeof(ushort) < naturalBytes)
        {
            ToRun(ref c, runs);
            return;
        }

        if (c.Cardinality > ArrayToBitmapThreshold)
            ToBitmap(ref c);
        else
            ToArray(ref c); // also trims an oversized array container to its exact length
    }

    // ── Single-element container writes ───────────────────────────────────────

    private static bool ContainerAdd(ref Chunk c, ushort low)
    {
        ToNaturalForm(ref c);

        if (c.Kind == ContainerKind.Bitmap)
        {
            ref ulong word = ref c.Bitmap![low >> 6];
            ulong mask = 1UL << (low & 63);
            if ((word & mask) != 0)
                return false;

            word |= mask;
            c.Cardinality++;
            return true;
        }

        int found = BinarySearchValues(c.Values!, c.Length, low);
        if (found >= 0)
            return false;

        int insert = ~found;
        if (c.Length == c.Values!.Length)
            Array.Resize(ref c.Values, Math.Min(ArrayToBitmapThreshold + 1, c.Values.Length * 2));

        Array.Copy(c.Values, insert, c.Values, insert + 1, c.Length - insert);
        c.Values[insert] = low;
        c.Length++;
        c.Cardinality++;

        if (c.Cardinality > ArrayToBitmapThreshold)
            ToBitmap(ref c);

        return true;
    }

    private static bool ContainerRemove(ref Chunk c, ushort low)
    {
        ToNaturalForm(ref c);

        if (c.Kind == ContainerKind.Bitmap)
        {
            ref ulong word = ref c.Bitmap![low >> 6];
            ulong mask = 1UL << (low & 63);
            if ((word & mask) == 0)
                return false;

            word &= ~mask;
            c.Cardinality--;
            return true;
        }

        int found = BinarySearchValues(c.Values!, c.Length, low);
        if (found < 0)
            return false;

        Array.Copy(c.Values!, found + 1, c.Values!, found, c.Length - found - 1);
        c.Length--;
        c.Cardinality--;
        return true;
    }

    // ── Range insertion ───────────────────────────────────────────────────────

    private long AddRunToChunk(ulong key, ushort lo, ushort hi)
    {
        int index = FindChunk(key);
        if (index < 0)
        {
            InsertRunChunk(~index, key, lo, hi);
            return hi - lo + 1;
        }

        ref Chunk c = ref Chunks[index];
        int before = c.Cardinality;
        ToBitmap(ref c);
        SetBitRange(c.Bitmap!, lo, hi);
        c.Cardinality = PopCount(c.Bitmap!);
        NormalizeBitmap(ref c);
        return c.Cardinality - before;
    }

    private static void SetBitRange(ulong[] bits, int lo, int hi)
    {
        int loWord = lo >> 6, hiWord = hi >> 6;
        ulong loMask = ulong.MaxValue << (lo & 63);

        if (loWord == hiWord)
        {
            bits[loWord] |= loMask & LowMask(hi & 63);
            return;
        }

        bits[loWord] |= loMask;
        for (int w = loWord + 1; w < hiWord; w++)
            bits[w] = ulong.MaxValue;

        bits[hiWord] |= LowMask(hi & 63);
    }

    // Bits 0..bit inclusive.
    private static ulong LowMask(int bit) => bit == 63 ? ulong.MaxValue : (1UL << (bit + 1)) - 1;

    // ── Result scratch ────────────────────────────────────────────────────────

    private void ScratchReset()
    {
        _scratchValues ??= new ushort[ArrayToBitmapThreshold];
        _scratchCount = 0;
        if (_scratchIsBitmap)
        {
            Array.Clear(_scratchBitmap!, 0, BitmapWords);
            _scratchIsBitmap = false;
        }
    }

    // Appends the next value of the result. Callers must append in ascending order.
    private void ScratchAppend(ushort value)
    {
        if (_scratchIsBitmap)
        {
            _scratchBitmap![value >> 6] |= 1UL << (value & 63);
            _scratchCount++;
            return;
        }

        if (_scratchCount == ArrayToBitmapThreshold)
        {
            _scratchBitmap ??= new ulong[BitmapWords];
            for (int i = 0; i < _scratchCount; i++)
            {
                ushort v = _scratchValues![i];
                _scratchBitmap[v >> 6] |= 1UL << (v & 63);
            }

            _scratchIsBitmap = true;
            _scratchBitmap[value >> 6] |= 1UL << (value & 63);
            _scratchCount++;
            return;
        }

        _scratchValues![_scratchCount++] = value;
    }

    // Replaces the chunk's container with whatever the scratch accumulated, reusing the chunk's
    // existing storage when it is big enough so a steady-state set-algebra loop stops allocating.
    private readonly void ScratchWriteTo(ref Chunk c)
    {
        if (_scratchIsBitmap)
        {
            ulong[] bits = c.Bitmap ?? new ulong[BitmapWords];
            Array.Copy(_scratchBitmap!, bits, BitmapWords);
            c.Bitmap = bits;
            c.Kind = ContainerKind.Bitmap;
            c.Values = null;
            c.Length = 0;
        }
        else
        {
            ushort[] values = c.Values is { } existing && existing.Length >= _scratchCount
                ? existing
                : new ushort[_scratchCount];
            Array.Copy(_scratchValues!, values, _scratchCount);
            c.Values = values;
            c.Kind = ContainerKind.Array;
            c.Length = _scratchCount;
            c.Bitmap = null;
        }

        c.Cardinality = _scratchCount;
    }

    // ── Container set algebra ─────────────────────────────────────────────────
    // Each operation has a word-parallel path for the dense (bitmap ⊕ bitmap) case and a
    // merge/filter path for everything else, driven by ContainerCursor so a run container on either
    // side needs no special case and is never mutated just because it was read.

    private void UnionInto(ref Chunk lhs, in Chunk rhs)
    {
        if (lhs.Kind == ContainerKind.Bitmap || rhs.Kind == ContainerKind.Bitmap ||
            lhs.Cardinality + rhs.Cardinality > ArrayToBitmapThreshold)
        {
            ToBitmap(ref lhs);
            ulong[] bits = lhs.Bitmap!;
            if (rhs.Kind == ContainerKind.Bitmap)
            {
                ulong[] other = rhs.Bitmap!;
                for (int w = 0; w < BitmapWords; w++)
                    bits[w] |= other[w];
            }
            else
            {
                var cursor = new ContainerCursor(in rhs);
                while (cursor.MoveNext(out ushort v))
                    bits[v >> 6] |= 1UL << (v & 63);
            }

            lhs.Cardinality = PopCount(bits);
            NormalizeBitmap(ref lhs);
            return;
        }

        ScratchReset();
        var a = new ContainerCursor(in lhs);
        var b = new ContainerCursor(in rhs);
        bool hasA = a.MoveNext(out ushort va);
        bool hasB = b.MoveNext(out ushort vb);
        while (hasA && hasB)
        {
            if (va < vb)
            {
                ScratchAppend(va);
                hasA = a.MoveNext(out va);
            }
            else if (va > vb)
            {
                ScratchAppend(vb);
                hasB = b.MoveNext(out vb);
            }
            else
            {
                ScratchAppend(va);
                hasA = a.MoveNext(out va);
                hasB = b.MoveNext(out vb);
            }
        }

        while (hasA)
        {
            ScratchAppend(va);
            hasA = a.MoveNext(out va);
        }

        while (hasB)
        {
            ScratchAppend(vb);
            hasB = b.MoveNext(out vb);
        }

        ScratchWriteTo(ref lhs);
    }

    private void IntersectInto(ref Chunk lhs, in Chunk rhs)
    {
        if (lhs.Kind == ContainerKind.Bitmap && rhs.Kind == ContainerKind.Bitmap)
        {
            ulong[] bits = lhs.Bitmap!, other = rhs.Bitmap!;
            for (int w = 0; w < BitmapWords; w++)
                bits[w] &= other[w];

            lhs.Cardinality = PopCount(bits);
            NormalizeBitmap(ref lhs);
            return;
        }

        ScratchReset();
        if (rhs.Kind == ContainerKind.Bitmap)
        {
            // Probing a bitmap is a single masked load, so walk the sparse side against it.
            var cursor = new ContainerCursor(in lhs);
            while (cursor.MoveNext(out ushort v))
            {
                if (BitmapContains(rhs.Bitmap!, v))
                    ScratchAppend(v);
            }
        }
        else if (lhs.Kind == ContainerKind.Bitmap)
        {
            var cursor = new ContainerCursor(in rhs);
            while (cursor.MoveNext(out ushort v))
            {
                if (BitmapContains(lhs.Bitmap!, v))
                    ScratchAppend(v);
            }
        }
        else
        {
            // Both sides are sorted, so a single linear merge beats a binary search per element —
            // and this is the shape the sparse workload the type is sold for actually hits.
            var a = new ContainerCursor(in lhs);
            var b = new ContainerCursor(in rhs);
            bool hasA = a.MoveNext(out ushort va);
            bool hasB = b.MoveNext(out ushort vb);
            while (hasA && hasB)
            {
                if (va < vb)
                {
                    hasA = a.MoveNext(out va);
                }
                else if (va > vb)
                {
                    hasB = b.MoveNext(out vb);
                }
                else
                {
                    ScratchAppend(va);
                    hasA = a.MoveNext(out va);
                    hasB = b.MoveNext(out vb);
                }
            }
        }

        ScratchWriteTo(ref lhs);
    }

    private void ExceptInto(ref Chunk lhs, in Chunk rhs)
    {
        if (lhs.Kind == ContainerKind.Bitmap && rhs.Kind == ContainerKind.Bitmap)
        {
            ulong[] bits = lhs.Bitmap!, other = rhs.Bitmap!;
            for (int w = 0; w < BitmapWords; w++)
                bits[w] &= ~other[w];

            lhs.Cardinality = PopCount(bits);
            NormalizeBitmap(ref lhs);
            return;
        }

        ScratchReset();
        if (rhs.Kind == ContainerKind.Bitmap)
        {
            var cursor = new ContainerCursor(in lhs);
            while (cursor.MoveNext(out ushort v))
            {
                if (!BitmapContains(rhs.Bitmap!, v))
                    ScratchAppend(v);
            }
        }
        else
        {
            // Both sides sorted: one linear merge, emitting the values only the left side has.
            var a = new ContainerCursor(in lhs);
            var b = new ContainerCursor(in rhs);
            bool hasA = a.MoveNext(out ushort va);
            bool hasB = b.MoveNext(out ushort vb);
            while (hasA && hasB)
            {
                if (va < vb)
                {
                    ScratchAppend(va);
                    hasA = a.MoveNext(out va);
                }
                else if (va > vb)
                {
                    hasB = b.MoveNext(out vb);
                }
                else
                {
                    hasA = a.MoveNext(out va);
                    hasB = b.MoveNext(out vb);
                }
            }

            while (hasA)
            {
                ScratchAppend(va);
                hasA = a.MoveNext(out va);
            }
        }

        ScratchWriteTo(ref lhs);
    }

    private void SymmetricExceptInto(ref Chunk lhs, in Chunk rhs)
    {
        if (lhs.Kind == ContainerKind.Bitmap || rhs.Kind == ContainerKind.Bitmap ||
            lhs.Cardinality + rhs.Cardinality > ArrayToBitmapThreshold)
        {
            ToBitmap(ref lhs);
            ulong[] bits = lhs.Bitmap!;
            if (rhs.Kind == ContainerKind.Bitmap)
            {
                ulong[] other = rhs.Bitmap!;
                for (int w = 0; w < BitmapWords; w++)
                    bits[w] ^= other[w];
            }
            else
            {
                var cursor = new ContainerCursor(in rhs);
                while (cursor.MoveNext(out ushort v))
                    bits[v >> 6] ^= 1UL << (v & 63);
            }

            lhs.Cardinality = PopCount(bits);
            NormalizeBitmap(ref lhs);
            return;
        }

        ScratchReset();
        var a = new ContainerCursor(in lhs);
        var b = new ContainerCursor(in rhs);
        bool hasA = a.MoveNext(out ushort va);
        bool hasB = b.MoveNext(out ushort vb);
        while (hasA && hasB)
        {
            if (va < vb)
            {
                ScratchAppend(va);
                hasA = a.MoveNext(out va);
            }
            else if (va > vb)
            {
                ScratchAppend(vb);
                hasB = b.MoveNext(out vb);
            }
            else
            {
                hasA = a.MoveNext(out va);
                hasB = b.MoveNext(out vb);
            }
        }

        while (hasA)
        {
            ScratchAppend(va);
            hasA = a.MoveNext(out va);
        }

        while (hasB)
        {
            ScratchAppend(vb);
            hasB = b.MoveNext(out vb);
        }

        ScratchWriteTo(ref lhs);
    }

    private static int ContainerIntersectCount(in Chunk a, in Chunk b)
    {
        if (a.Kind == ContainerKind.Bitmap && b.Kind == ContainerKind.Bitmap)
        {
            ulong[] x = a.Bitmap!, y = b.Bitmap!;
            int count = 0;
            for (int w = 0; w < BitmapWords; w++)
                count += BitOperations.PopCount(x[w] & y[w]);

            return count;
        }

        // The two counting queries below probe the larger side rather than merging the two sorted
        // cursors the way the mutating operators do. That is deliberate: probing lets Overlaps bail
        // on the first hit (a merge would have to keep stepping the smaller side), and neither query
        // writes a result, so the merge's one advantage — producing the output in order for free —
        // buys nothing here.
        int total = 0;
        if (a.Cardinality <= b.Cardinality)
        {
            var cursor = new ContainerCursor(in a);
            while (cursor.MoveNext(out ushort v))
            {
                if (ContainerContains(in b, v))
                    total++;
            }
        }
        else
        {
            var cursor = new ContainerCursor(in b);
            while (cursor.MoveNext(out ushort v))
            {
                if (ContainerContains(in a, v))
                    total++;
            }
        }

        return total;
    }

    private static bool ContainerOverlaps(in Chunk a, in Chunk b)
    {
        if (a.Kind == ContainerKind.Bitmap && b.Kind == ContainerKind.Bitmap)
        {
            ulong[] x = a.Bitmap!, y = b.Bitmap!;
            for (int w = 0; w < BitmapWords; w++)
            {
                if ((x[w] & y[w]) != 0)
                    return true;
            }

            return false;
        }

        if (a.Cardinality <= b.Cardinality)
        {
            var cursor = new ContainerCursor(in a);
            while (cursor.MoveNext(out ushort v))
            {
                if (ContainerContains(in b, v))
                    return true;
            }
        }
        else
        {
            var cursor = new ContainerCursor(in b);
            while (cursor.MoveNext(out ushort v))
            {
                if (ContainerContains(in a, v))
                    return true;
            }
        }

        return false;
    }

    // ── Whole-store set algebra ───────────────────────────────────────────────
    // None of these is ever called with `other` aliasing this store: every wrapper short-circuits
    // the self case before it gets here, except for the read-only IsSubsetOf, where aliasing is
    // harmless.

    private static Chunk CloneChunk(in Chunk source)
    {
        Chunk copy = source;
        if (source.Kind == ContainerKind.Bitmap)
            copy.Bitmap = (ulong[])source.Bitmap!.Clone();
        else
            copy.Values = source.Values![..source.Length];

        return copy;
    }

    internal readonly long IntersectCount(ref CompressedChunkStore other)
    {
        long total = 0;
        int i = 0, j = 0;
        while (i < ChunkCount && j < other.ChunkCount)
        {
            ulong a = Chunks[i].Key, b = other.Chunks[j].Key;
            if (a < b)
                i++;
            else if (a > b)
                j++;
            else
                total += ContainerIntersectCount(in Chunks[i++], in other.Chunks[j++]);
        }

        return total;
    }

    // Each mutating whole-store operation returns whether the cardinality changed, which for these
    // operations is exactly whether the contents changed (a union only adds, an intersection or
    // difference only removes). Symmetric difference is the exception and is handled there.

    internal bool UnionWith(ref CompressedChunkStore other)
    {
        if (other.ChunkCount == 0)
            return false;

        long before = Cardinality;
        Chunk[] merged = new Chunk[ChunkCount + other.ChunkCount];
        int i = 0, j = 0, n = 0;
        while (i < ChunkCount && j < other.ChunkCount)
        {
            ulong a = Chunks[i].Key, b = other.Chunks[j].Key;
            if (a < b)
            {
                merged[n++] = Chunks[i++];
            }
            else if (a > b)
            {
                merged[n++] = CloneChunk(in other.Chunks[j++]);
            }
            else
            {
                Chunk combined = Chunks[i++];
                UnionInto(ref combined, in other.Chunks[j++]);
                merged[n++] = combined;
            }
        }

        while (i < ChunkCount)
            merged[n++] = Chunks[i++];

        while (j < other.ChunkCount)
            merged[n++] = CloneChunk(in other.Chunks[j++]);

        Chunks = merged;
        ChunkCount = n;
        Cardinality = SumCardinality();
        return Cardinality != before;
    }

    internal bool IntersectWith(ref CompressedChunkStore other)
    {
        long before = Cardinality;
        int i = 0, j = 0, n = 0;
        while (i < ChunkCount && j < other.ChunkCount)
        {
            ulong a = Chunks[i].Key, b = other.Chunks[j].Key;
            if (a < b)
            {
                i++;
            }
            else if (a > b)
            {
                j++;
            }
            else
            {
                // The compaction cursor `n` only ever trails `i`, so writing the surviving chunk
                // back here never overwrites one the loop has yet to read.
                Chunk combined = Chunks[i++];
                IntersectInto(ref combined, in other.Chunks[j++]);
                if (combined.Cardinality != 0)
                    Chunks[n++] = combined;
            }
        }

        TruncateChunks(n);
        Cardinality = SumCardinality();
        return Cardinality != before;
    }

    internal bool ExceptWith(ref CompressedChunkStore other)
    {
        long before = Cardinality;
        int i = 0, j = 0, n = 0;
        while (i < ChunkCount && j < other.ChunkCount)
        {
            ulong a = Chunks[i].Key, b = other.Chunks[j].Key;
            if (a < b)
            {
                Chunks[n++] = Chunks[i++];
            }
            else if (a > b)
            {
                j++;
            }
            else
            {
                Chunk combined = Chunks[i++];
                ExceptInto(ref combined, in other.Chunks[j++]);
                if (combined.Cardinality != 0)
                    Chunks[n++] = combined;
            }
        }

        while (i < ChunkCount)
            Chunks[n++] = Chunks[i++];

        TruncateChunks(n);
        Cardinality = SumCardinality();
        return Cardinality != before;
    }

    internal bool SymmetricExceptWith(ref CompressedChunkStore other)
    {
        if (other.ChunkCount == 0)
            return false;

        Chunk[] merged = new Chunk[ChunkCount + other.ChunkCount];
        int i = 0, j = 0, n = 0;
        while (i < ChunkCount && j < other.ChunkCount)
        {
            ulong a = Chunks[i].Key, b = other.Chunks[j].Key;
            if (a < b)
            {
                merged[n++] = Chunks[i++];
            }
            else if (a > b)
            {
                merged[n++] = CloneChunk(in other.Chunks[j++]);
            }
            else
            {
                Chunk combined = Chunks[i++];
                SymmetricExceptInto(ref combined, in other.Chunks[j++]);
                if (combined.Cardinality != 0)
                    merged[n++] = combined;
            }
        }

        while (i < ChunkCount)
            merged[n++] = Chunks[i++];

        while (j < other.ChunkCount)
            merged[n++] = CloneChunk(in other.Chunks[j++]);

        Chunks = merged;
        ChunkCount = n;
        Cardinality = SumCardinality();

        // A symmetric difference against a non-empty set always toggles at least one element.
        return true;
    }

    internal readonly bool IsSubsetOf(ref CompressedChunkStore other)
    {
        for (int i = 0; i < ChunkCount; i++)
        {
            int j = other.FindChunk(Chunks[i].Key);
            if (j < 0)
                return false;

            ref Chunk mine = ref Chunks[i];
            ref Chunk theirs = ref other.Chunks[j];
            if (mine.Cardinality > theirs.Cardinality)
                return false;

            var cursor = new ContainerCursor(in mine);
            while (cursor.MoveNext(out ushort v))
            {
                if (!ContainerContains(in theirs, v))
                    return false;
            }
        }

        return true;
    }

    internal readonly bool Overlaps(ref CompressedChunkStore other)
    {
        int i = 0, j = 0;
        while (i < ChunkCount && j < other.ChunkCount)
        {
            ulong a = Chunks[i].Key, b = other.Chunks[j].Key;
            if (a < b)
                i++;
            else if (a > b)
                j++;
            else if (ContainerOverlaps(in Chunks[i++], in other.Chunks[j++]))
                return true;
        }

        return false;
    }

    // Takes over `replacement`'s chunk index and returns whether the cardinality changed. Only ever
    // called with a store built locally by the caller, so stealing its arrays cannot alias anything
    // anyone else still holds.
    internal bool TakeOver(ref CompressedChunkStore replacement)
    {
        bool changed = replacement.Cardinality != Cardinality;
        Chunks = replacement.Chunks;
        ChunkCount = replacement.ChunkCount;
        Cardinality = replacement.Cardinality;
        return changed;
    }

    private void TruncateChunks(int count)
    {
        Array.Clear(Chunks, count, ChunkCount - count);
        ChunkCount = count;
    }

    private readonly long SumCardinality()
    {
        long total = 0;
        for (int i = 0; i < ChunkCount; i++)
            total += Chunks[i].Cardinality;

        return total;
    }

    // ── Cursors ───────────────────────────────────────────────────────────────

    // Yields a container's values in ascending order, whichever form it is stored in. Writing the
    // generic set-algebra paths against this is what keeps a run container from needing its own
    // case in every operation — and from being decompressed just because it was read.
    internal struct ContainerCursor
    {
        private readonly Chunk _chunk;
        private int _index;
        private ulong _word;
        private int _wordIndex;
        private int _runNext;
        private int _runEnd;

        internal ContainerCursor(in Chunk chunk)
        {
            _chunk = chunk;
            _index = 0;
            _word = 0;
            _wordIndex = -1;
            _runNext = 0;
            _runEnd = -1;
        }

        internal bool MoveNext(out ushort value)
        {
            if (_chunk.Kind == ContainerKind.Array)
            {
                if (_index < _chunk.Length)
                {
                    value = _chunk.Values![_index++];
                    return true;
                }

                value = 0;
                return false;
            }

            if (_chunk.Kind == ContainerKind.Run)
            {
                if (_runNext > _runEnd)
                {
                    if (_index >= _chunk.Length)
                    {
                        value = 0;
                        return false;
                    }

                    _runNext = _chunk.Values![_index];
                    _runEnd = _runNext + _chunk.Values[_index + 1];
                    _index += 2;
                }

                value = (ushort)_runNext++;
                return true;
            }

            while (_word == 0)
            {
                if (++_wordIndex >= BitmapWords)
                {
                    value = 0;
                    return false;
                }

                _word = _chunk.Bitmap![_wordIndex];
            }

            int bit = BitOperations.TrailingZeroCount(_word);
            _word &= _word - 1;
            value = (ushort)((_wordIndex << 6) + bit);
            return true;
        }
    }
}
