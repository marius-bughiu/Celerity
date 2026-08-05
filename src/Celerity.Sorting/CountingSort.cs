using System.Buffers;
using System.Runtime.CompilerServices;

namespace Celerity.Sorting;

/// <summary>
/// <b>Counting sort over a bounded key range</b> — one histogram pass and one rewrite, no
/// comparisons and no scratch for the keys-only forms, where
/// <see cref="MemoryExtensions.Sort{T}(Span{T})"/> runs a full <c>O(n log n)</c> introsort.
/// Covers <see cref="byte"/> keys, <see cref="ushort"/> keys, and <see cref="int"/> keys over a
/// caller-declared <c>[min, max]</c> range — the shape enum ordinals, bucket ids, small scores and
/// quantized values take.
/// </summary>
/// <remarks>
/// <para>
/// <b>The documented BCL-beating workload</b> is sorting many values drawn from few distinct keys:
/// histogram-and-rewrite is <c>O(n + range)</c>, so at a thousand elements over 256 buckets it does
/// a fraction of introsort's work and the gap grows linearly with <c>n</c>. The keys-only forms
/// never move an element twice — they overwrite each run in place from the counts — and allocate
/// nothing at all for <see cref="byte"/> keys.
/// </para>
/// <para>
/// <b>Where it loses:</b> when <c>range</c> approaches or exceeds <c>n</c>. The histogram costs
/// <c>range</c> regardless of how few elements there are, so sorting 100 values over a 1,000,000-wide
/// range is far slower than <see cref="Array.Sort(Array)"/>. The rule of thumb is <c>range ≲ n</c>;
/// past that, reach for <see cref="RadixSort"/> (which is insensitive to range) or the BCL. For
/// keys wider than the range abstraction can cover, <see cref="RadixSort"/> is the general answer.
/// </para>
/// <para>
/// <b>Naming</b> follows <see cref="RadixSort"/>: <c>Sort</c> is the convenience form and rents
/// whatever buffers it needs, <c>SortWithScratch</c> takes them from the caller and allocates
/// nothing. Keeping the two apart is what lets <c>Sort(keys, values)</c> always mean key-and-payload
/// even when the payload happens to be <c>int</c>, the same element type a counter buffer has.
/// </para>
/// <para>
/// The key+payload forms are <b>stable</b> — equal keys keep their input order — and need one
/// payload-sized scratch buffer, which the caller can supply to make the call allocate nothing.
/// </para>
/// </remarks>
public static class CountingSort
{
    /// <summary>The number of counters a <see cref="byte"/>-keyed sort uses.</summary>
    public const int ByteRange = 256;

    /// <summary>The number of counters a <see cref="ushort"/>-keyed sort uses.</summary>
    public const int UInt16Range = 65536;

    /// <summary>
    /// The number of counters an <see cref="int"/>-keyed sort over <c>[min, max]</c> needs, for
    /// sizing the <c>counts</c> buffer of the allocation-free overloads.
    /// </summary>
    /// <param name="min">The smallest key that may appear.</param>
    /// <param name="max">The largest key that may appear.</param>
    /// <returns><c>max - min + 1</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is less than <paramref name="min"/>, or the range exceeds <see cref="int.MaxValue"/> counters.</exception>
    public static int RequiredCounts(int min, int max) => ValidateRange(min, max);

    // ---- byte keys ----------------------------------------------------------------------------

    /// <summary>Sorts <paramref name="keys"/> in ascending order. Allocates nothing.</summary>
    /// <param name="keys">The keys to sort in place.</param>
    public static void Sort(Span<byte> keys)
    {
        if (keys.Length < 2)
        {
            return;
        }

        Span<int> counts = stackalloc int[ByteRange];
        SortKeysCore(keys, counts);
    }

    /// <summary>Sorts <paramref name="keys"/> in ascending order, moving <paramref name="values"/> with them.</summary>
    /// <typeparam name="TValue">The payload type. Never compared; only moved.</typeparam>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="values">The payload, parallel to <paramref name="keys"/> and at least as long. Permuted to match.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> is shorter than <paramref name="keys"/>, or shares storage with it.</exception>
    public static void Sort<TValue>(Span<byte> keys, Span<TValue> values)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        if (keys.Length < 2)
        {
            return;
        }

        TValue[] valueScratch = ArrayPool<TValue>.Shared.Rent(keys.Length);
        try
        {
            Span<int> counts = stackalloc int[ByteRange];
            SortPairsCore(keys, values, counts, valueScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ReturnValueScratch(valueScratch);
        }
    }

    /// <summary>Sorts <paramref name="keys"/> and their payload using a caller-supplied scratch buffer, allocating nothing.</summary>
    /// <typeparam name="TValue">The payload type. Never compared; only moved.</typeparam>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="values">The payload, parallel to <paramref name="keys"/> and at least as long. Permuted to match.</param>
    /// <param name="valueScratch">A buffer of at least <c>keys.Length</c> payload slots. Its contents on entry are ignored and overwritten.</param>
    /// <exception cref="ArgumentException">A buffer is too short, or two buffers share storage.</exception>
    public static void SortWithScratch<TValue>(Span<byte> keys, Span<TValue> values, Span<TValue> valueScratch)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        SortingGuard.RequireLength(valueScratch.Length, keys.Length, nameof(valueScratch));
        SortingGuard.RequireNoOverlap(values.Overlaps(valueScratch), nameof(valueScratch));
        SortingGuard.RequireDistinctStorage(keys, valueScratch, nameof(valueScratch));
        if (keys.Length < 2)
        {
            return;
        }

        Span<int> counts = stackalloc int[ByteRange];
        SortPairsCore(keys, values, counts, valueScratch);
    }

    private static void SortKeysCore(Span<byte> keys, Span<int> counts)
    {
        counts.Clear();
        for (int i = 0; i < keys.Length; i++)
        {
            counts[keys[i]]++;
        }

        Rewrite(keys, counts);
    }

    private static void SortPairsCore<TValue>(Span<byte> keys, Span<TValue> values, Span<int> counts, Span<TValue> valueScratch)
    {
        counts.Clear();
        for (int i = 0; i < keys.Length; i++)
        {
            counts[keys[i]]++;
        }

        ExclusivePrefixSum(counts);
        for (int i = 0; i < keys.Length; i++)
        {
            valueScratch[counts[keys[i]]++] = values[i];
        }

        WriteBackPairs(keys, values, counts, valueScratch);
    }

    // ---- ushort keys --------------------------------------------------------------------------

    /// <inheritdoc cref="Sort(Span{byte})"/>
    /// <remarks>Rents a <see cref="UInt16Range"/>-element counter array; use <see cref="SortWithScratch(Span{ushort}, Span{int})"/> to avoid it.</remarks>
    public static void Sort(Span<ushort> keys)
    {
        if (keys.Length < 2)
        {
            return;
        }

        int[] counts = ArrayPool<int>.Shared.Rent(UInt16Range);
        try
        {
            SortKeysCore(keys, counts.AsSpan(0, UInt16Range));
        }
        finally
        {
            ArrayPool<int>.Shared.Return(counts);
        }
    }

    /// <summary>Sorts <paramref name="keys"/> in ascending order using a caller-supplied counter buffer, allocating nothing.</summary>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="counts">A buffer of at least <see cref="UInt16Range"/> counters. Its contents on entry are ignored and overwritten.</param>
    /// <exception cref="ArgumentException"><paramref name="counts"/> is too short, or shares storage with <paramref name="keys"/>.</exception>
    public static void SortWithScratch(Span<ushort> keys, Span<int> counts)
    {
        SortingGuard.RequireLength(counts.Length, UInt16Range, nameof(counts));
        SortingGuard.RequireDistinctStorage(keys, counts, nameof(counts));
        if (keys.Length < 2)
        {
            return;
        }

        SortKeysCore(keys, counts[..UInt16Range]);
    }

    /// <inheritdoc cref="Sort{TValue}(Span{byte}, Span{TValue})"/>
    public static void Sort<TValue>(Span<ushort> keys, Span<TValue> values)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        if (keys.Length < 2)
        {
            return;
        }

        int[] counts = ArrayPool<int>.Shared.Rent(UInt16Range);
        TValue[] valueScratch = ArrayPool<TValue>.Shared.Rent(keys.Length);
        try
        {
            SortPairsCore(keys, values, counts.AsSpan(0, UInt16Range), valueScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<int>.Shared.Return(counts);
            ReturnValueScratch(valueScratch);
        }
    }

    /// <summary>Sorts <paramref name="keys"/> and their payload using caller-supplied buffers, allocating nothing.</summary>
    /// <typeparam name="TValue">The payload type. Never compared; only moved.</typeparam>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="values">The payload, parallel to <paramref name="keys"/> and at least as long. Permuted to match.</param>
    /// <param name="valueScratch">A buffer of at least <c>keys.Length</c> payload slots. Its contents on entry are ignored and overwritten.</param>
    /// <param name="counts">A buffer of at least <see cref="UInt16Range"/> counters. Its contents on entry are ignored and overwritten.</param>
    /// <exception cref="ArgumentException">A buffer is too short, or two buffers share storage.</exception>
    public static void SortWithScratch<TValue>(Span<ushort> keys, Span<TValue> values, Span<TValue> valueScratch, Span<int> counts)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        SortingGuard.RequireLength(valueScratch.Length, keys.Length, nameof(valueScratch));
        SortingGuard.RequireLength(counts.Length, UInt16Range, nameof(counts));
        SortingGuard.RequireDistinctStorage(keys, counts, nameof(counts));
        SortingGuard.RequireNoOverlap(values.Overlaps(valueScratch), nameof(valueScratch));
        SortingGuard.RequireDistinctStorage(keys, valueScratch, nameof(valueScratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortPairsCore(keys, values, counts[..UInt16Range], valueScratch);
    }

    private static void SortKeysCore(Span<ushort> keys, Span<int> counts)
    {
        counts.Clear();
        for (int i = 0; i < keys.Length; i++)
        {
            counts[keys[i]]++;
        }

        Rewrite(keys, counts);
    }

    private static void SortPairsCore<TValue>(Span<ushort> keys, Span<TValue> values, Span<int> counts, Span<TValue> valueScratch)
    {
        counts.Clear();
        for (int i = 0; i < keys.Length; i++)
        {
            counts[keys[i]]++;
        }

        ExclusivePrefixSum(counts);
        for (int i = 0; i < keys.Length; i++)
        {
            valueScratch[counts[keys[i]]++] = values[i];
        }

        WriteBackPairs(keys, values, counts, valueScratch);
    }

    // ---- int keys over a declared range -------------------------------------------------------

    /// <summary>Sorts <paramref name="keys"/> in ascending order, given that every key lies in <c>[min, max]</c>.</summary>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="min">The smallest key that may appear.</param>
    /// <param name="max">The largest key that may appear.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is less than <paramref name="min"/>, the range exceeds <see cref="int.MaxValue"/> counters, or a key falls outside <c>[min, max]</c>.</exception>
    public static void Sort(Span<int> keys, int min, int max)
    {
        int range = ValidateRange(min, max);
        if (IsAlreadySorted(keys, min, max))
        {
            return;
        }

        int[] counts = ArrayPool<int>.Shared.Rent(range);
        try
        {
            SortKeysCore(keys, min, max, counts.AsSpan(0, range));
        }
        finally
        {
            ArrayPool<int>.Shared.Return(counts);
        }
    }

    /// <summary>Sorts <paramref name="keys"/> over <c>[min, max]</c> using a caller-supplied counter buffer, allocating nothing.</summary>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="min">The smallest key that may appear.</param>
    /// <param name="max">The largest key that may appear.</param>
    /// <param name="counts">A buffer of at least <see cref="RequiredCounts"/> counters. Its contents on entry are ignored and overwritten.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is less than <paramref name="min"/>, the range exceeds <see cref="int.MaxValue"/> counters, or a key falls outside <c>[min, max]</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="counts"/> is too short, or shares storage with <paramref name="keys"/>.</exception>
    public static void SortWithScratch(Span<int> keys, int min, int max, Span<int> counts)
    {
        int range = ValidateRange(min, max);
        SortingGuard.RequireLength(counts.Length, range, nameof(counts));
        SortingGuard.RequireDistinctStorage(keys, counts, nameof(counts));
        if (IsAlreadySorted(keys, min, max))
        {
            return;
        }

        SortKeysCore(keys, min, max, counts[..range]);
    }

    /// <summary>Sorts <paramref name="keys"/> over <c>[min, max]</c>, moving <paramref name="values"/> with them.</summary>
    /// <typeparam name="TValue">The payload type. Never compared; only moved.</typeparam>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="values">The payload, parallel to <paramref name="keys"/> and at least as long. Permuted to match.</param>
    /// <param name="min">The smallest key that may appear.</param>
    /// <param name="max">The largest key that may appear.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> is shorter than <paramref name="keys"/>, or shares storage with it.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is less than <paramref name="min"/>, the range exceeds <see cref="int.MaxValue"/> counters, or a key falls outside <c>[min, max]</c>.</exception>
    public static void Sort<TValue>(Span<int> keys, Span<TValue> values, int min, int max)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        int range = ValidateRange(min, max);
        if (IsAlreadySorted(keys, min, max))
        {
            return;
        }

        int[] counts = ArrayPool<int>.Shared.Rent(range);
        TValue[] valueScratch = ArrayPool<TValue>.Shared.Rent(keys.Length);
        try
        {
            SortPairsCore(keys, values, min, max, counts.AsSpan(0, range), valueScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<int>.Shared.Return(counts);
            ReturnValueScratch(valueScratch);
        }
    }

    /// <summary>Sorts <paramref name="keys"/> over <c>[min, max]</c> and their payload using caller-supplied buffers, allocating nothing.</summary>
    /// <typeparam name="TValue">The payload type. Never compared; only moved.</typeparam>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="values">The payload, parallel to <paramref name="keys"/> and at least as long. Permuted to match.</param>
    /// <param name="min">The smallest key that may appear.</param>
    /// <param name="max">The largest key that may appear.</param>
    /// <param name="valueScratch">A buffer of at least <c>keys.Length</c> payload slots. Its contents on entry are ignored and overwritten.</param>
    /// <param name="counts">A buffer of at least <see cref="RequiredCounts"/> counters. Its contents on entry are ignored and overwritten.</param>
    /// <exception cref="ArgumentException">A buffer is too short, or two buffers share storage.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is less than <paramref name="min"/>, the range exceeds <see cref="int.MaxValue"/> counters, or a key falls outside <c>[min, max]</c>.</exception>
    public static void SortWithScratch<TValue>(Span<int> keys, Span<TValue> values, int min, int max, Span<TValue> valueScratch, Span<int> counts)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        SortingGuard.RequireLength(valueScratch.Length, keys.Length, nameof(valueScratch));
        int range = ValidateRange(min, max);
        SortingGuard.RequireLength(counts.Length, range, nameof(counts));
        SortingGuard.RequireDistinctStorage(keys, counts, nameof(counts));
        SortingGuard.RequireNoOverlap(values.Overlaps(valueScratch), nameof(valueScratch));
        SortingGuard.RequireDistinctStorage(keys, valueScratch, nameof(valueScratch));
        if (IsAlreadySorted(keys, min, max))
        {
            return;
        }

        SortPairsCore(keys, values, min, max, counts[..range], valueScratch);
    }

    /// <summary>
    /// Whether a declared-range sort has nothing to do — while still honouring the range contract.
    /// </summary>
    /// <remarks>
    /// A span of fewer than two keys is already sorted, but the counter buffer is sized to the range
    /// the caller declared, not to the input: sorting one key over <c>[0, 1_000_000]</c> would
    /// otherwise rent and clear four megabytes to do nothing. The lone key still has to be range-
    /// checked, because the histogram pass it is skipping is what would otherwise have thrown.
    /// </remarks>
    private static bool IsAlreadySorted(Span<int> keys, int min, int max)
    {
        if (keys.Length >= 2)
        {
            return false;
        }

        if (keys.Length == 1)
        {
            _ = KeyIndex(keys[0], min, max, nameof(keys));
        }

        return true;
    }

    private static void SortKeysCore(Span<int> keys, int min, int max, Span<int> counts)
    {
        counts.Clear();
        for (int i = 0; i < keys.Length; i++)
        {
            counts[KeyIndex(keys[i], min, max, nameof(keys))]++;
        }

        int pos = 0;
        for (int v = 0; v < counts.Length; v++)
        {
            int count = counts[v];
            if (count != 0)
            {
                keys.Slice(pos, count).Fill(min + v);
                pos += count;
            }
        }
    }

    private static void SortPairsCore<TValue>(Span<int> keys, Span<TValue> values, int min, int max, Span<int> counts, Span<TValue> valueScratch)
    {
        counts.Clear();
        for (int i = 0; i < keys.Length; i++)
        {
            counts[KeyIndex(keys[i], min, max, nameof(keys))]++;
        }

        ExclusivePrefixSum(counts);
        for (int i = 0; i < keys.Length; i++)
        {
            valueScratch[counts[keys[i] - min]++] = values[i];
        }

        valueScratch[..keys.Length].CopyTo(values);

        // After the scatter every counter has advanced to one past its run, so it is that run's end
        // position — which is exactly what the key rewrite needs, and why no key scratch is required.
        int pos = 0;
        for (int v = 0; v < counts.Length; v++)
        {
            int end = counts[v];
            if (end != pos)
            {
                keys[pos..end].Fill(min + v);
                pos = end;
            }
        }
    }

    // ---- shared -------------------------------------------------------------------------------

    private static void Rewrite(Span<byte> keys, Span<int> counts)
    {
        int pos = 0;
        for (int v = 0; v < counts.Length; v++)
        {
            int count = counts[v];
            if (count != 0)
            {
                keys.Slice(pos, count).Fill((byte)v);
                pos += count;
            }
        }
    }

    private static void Rewrite(Span<ushort> keys, Span<int> counts)
    {
        int pos = 0;
        for (int v = 0; v < counts.Length; v++)
        {
            int count = counts[v];
            if (count != 0)
            {
                keys.Slice(pos, count).Fill((ushort)v);
                pos += count;
            }
        }
    }

    private static void WriteBackPairs<TValue>(Span<byte> keys, Span<TValue> values, Span<int> counts, Span<TValue> valueScratch)
    {
        valueScratch[..keys.Length].CopyTo(values);

        int pos = 0;
        for (int v = 0; v < counts.Length; v++)
        {
            int end = counts[v];
            if (end != pos)
            {
                keys[pos..end].Fill((byte)v);
                pos = end;
            }
        }
    }

    private static void WriteBackPairs<TValue>(Span<ushort> keys, Span<TValue> values, Span<int> counts, Span<TValue> valueScratch)
    {
        valueScratch[..keys.Length].CopyTo(values);

        int pos = 0;
        for (int v = 0; v < counts.Length; v++)
        {
            int end = counts[v];
            if (end != pos)
            {
                keys[pos..end].Fill((ushort)v);
                pos = end;
            }
        }
    }

    private static void ExclusivePrefixSum(Span<int> counts)
    {
        int sum = 0;
        for (int v = 0; v < counts.Length; v++)
        {
            int count = counts[v];
            counts[v] = sum;
            sum += count;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int KeyIndex(int key, int min, int max, string paramName)
    {
        if (key < min || key > max)
        {
            throw KeyOutOfRange(key, min, max, paramName);
        }

        return key - min;
    }

    private static int ValidateRange(int min, int max)
    {
        if (max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, $"The range end must not be below its start ({min}).");
        }

        long range = (long)max - min + 1;
        if (range > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(max),
                max,
                $"The range [{min}, {max}] needs {range} counters, more than an array can hold. Use RadixSort for a full-width key domain.");
        }

        return (int)range;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArgumentOutOfRangeException KeyOutOfRange(int key, int min, int max, string paramName) =>
        new(paramName, key, $"Every key must lie in the declared range [{min}, {max}].");

    // A pooled payload buffer holding references keeps whatever it last held alive until the next
    // renter overwrites the slot, so clear it on the way back. Blittable payloads skip the memset.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReturnValueScratch<TValue>(TValue[] buffer) =>
        ArrayPool<TValue>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TValue>());
}
