using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Celerity.Sorting;

/// <summary>
/// <b>LSD radix sort over primitive keys</b> — four (32-bit) or eight (64-bit) counting passes with
/// purely sequential reads, <b>no comparisons and no data-dependent branches</b>, where
/// <see cref="Array.Sort(Array)"/> and <see cref="MemoryExtensions.Sort{T}(Span{T})"/> run a scalar
/// comparison introsort whose every partition step is a branch the predictor cannot learn on random
/// data. Supports keys alone, keys with a parallel payload, and keys with an index permutation
/// (<see cref="ArgSort(ReadOnlySpan{int}, Span{int})"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The documented BCL-beating workload</b> is a large primitive-key sort — sort-by-id, join keys,
/// particle indices, timestamps — from roughly <b>1,000 elements upward</b>, and the margin widens
/// with <c>n</c> because radix replaces <c>O(n log n)</c> mispredicting comparisons with
/// <c>O(n·d)</c> histogram-and-scatter passes whose cost is memory bandwidth rather than stalls.
/// The measured crossover is on the
/// <a href="https://marius-bughiu.github.io/Celerity/dev/bench/">benchmark dashboard</a>: below a
/// few hundred elements the fixed cost of the histogram pass dominates and
/// <see cref="Array.Sort(Array)"/> wins outright, so <b>use the BCL for small spans</b>.
/// </para>
/// <para>
/// <b>Why the BCL cannot close this gap:</b> <see cref="Array.Sort(Array)"/> is contractually
/// in-place, and radix needs <c>O(n)</c> scratch. That is the flexibility-for-speed trade this
/// library exists to make — and it is why every entry point here has a caller-supplied-scratch
/// overload, so a hot loop rents its buffers once instead of per call. The parameterless overloads
/// rent from <see cref="ArrayPool{T}"/>. Both forms use roughly 4 KB (32-bit keys) or 8 KB (64-bit
/// keys) of stack for the digit histograms.
/// </para>
/// <para>
/// <b>Naming:</b> <c>Sort</c> is the convenience form and rents its scratch;
/// <c>SortWithScratch</c> is the same sort with every buffer supplied by the caller and allocates
/// nothing. They are named apart deliberately rather than overloaded: <c>Sort(keys, values)</c> has
/// to keep meaning key-and-payload the way <see cref="Array.Sort(Array, Array)"/> does, and a
/// <c>Sort(keys, scratch)</c> overload would silently win that call whenever the payload happens to
/// have the same element type as the keys — sorting <c>int</c> ids alongside <c>int</c> indices
/// would then quietly overwrite the payload.
/// </para>
/// <para>
/// <b>Ordering:</b> signed integer keys cost nothing extra — the last pass simply starts its prefix
/// sum at the sign-bit bucket. <see cref="float"/> and <see cref="double"/> keys pay two additional
/// linear passes for an order-preserving bit transform, so unsigned keys are the fastest shape.
/// Sorting is <b>stable</b>: equal keys keep their input order, and the key+payload form therefore
/// keeps equal-keyed payloads in order too, which introsort does not guarantee.
/// </para>
/// <para>
/// <b>Two floating-point divergences from <see cref="Array.Sort(Array)"/>, both deliberate.</b>
/// <c>NaN</c> keys are ordered by their bit pattern — sign-bit-set NaNs sort before every number
/// and the rest sort after every number — where <see cref="Array.Sort(Array)"/> moves all NaNs to
/// the front. And <c>-0.0</c> sorts before <c>+0.0</c>, where the BCL comparer calls them equal and
/// leaves their relative order to the partitioning. Filter or normalize NaNs first if you need the
/// BCL's placement.
/// </para>
/// </remarks>
public static class RadixSort
{
    // ---- uint ---------------------------------------------------------------------------------

    /// <summary>Sorts <paramref name="keys"/> in ascending order, renting a scratch buffer.</summary>
    /// <param name="keys">The keys to sort in place.</param>
    public static void Sort(Span<uint> keys)
    {
        if (keys.Length < 2)
        {
            return;
        }

        uint[] scratch = ArrayPool<uint>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, scratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(scratch);
        }
    }

    /// <summary>Sorts <paramref name="keys"/> in ascending order using a caller-supplied scratch buffer, allocating nothing.</summary>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="scratch">A buffer of at least <c>keys.Length</c> elements. Its contents on entry are ignored and overwritten.</param>
    /// <exception cref="ArgumentException"><paramref name="scratch"/> is too short, or overlaps <paramref name="keys"/>.</exception>
    public static void SortWithScratch(Span<uint> keys, Span<uint> scratch)
    {
        SortingGuard.RequireLength(scratch.Length, keys.Length, nameof(scratch));
        SortingGuard.RequireNoOverlap(keys.Overlaps(scratch), nameof(scratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, scratch);
    }

    /// <summary>Sorts <paramref name="keys"/> in ascending order, moving <paramref name="values"/> with them.</summary>
    /// <typeparam name="TValue">The payload type. Never compared; only moved.</typeparam>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="values">The payload, parallel to <paramref name="keys"/> and at least as long. Permuted to match.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> is shorter than <paramref name="keys"/>, or shares storage with it.</exception>
    public static void Sort<TValue>(Span<uint> keys, Span<TValue> values)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        if (keys.Length < 2)
        {
            return;
        }

        uint[] keyScratch = ArrayPool<uint>.Shared.Rent(keys.Length);
        TValue[] valueScratch = ArrayPool<TValue>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, values, keyScratch.AsSpan(0, keys.Length), valueScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(keyScratch);
            ReturnValueScratch(valueScratch);
        }
    }

    /// <summary>Sorts <paramref name="keys"/> and their payload using caller-supplied scratch buffers, allocating nothing.</summary>
    /// <typeparam name="TValue">The payload type. Never compared; only moved.</typeparam>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="values">The payload, parallel to <paramref name="keys"/> and at least as long. Permuted to match.</param>
    /// <param name="keyScratch">A buffer of at least <c>keys.Length</c> keys. Its contents on entry are ignored and overwritten.</param>
    /// <param name="valueScratch">A buffer of at least <c>keys.Length</c> payload slots. Its contents on entry are ignored and overwritten.</param>
    /// <exception cref="ArgumentException">A buffer is too short, or any two of the four buffers share storage.</exception>
    public static void SortWithScratch<TValue>(Span<uint> keys, Span<TValue> values, Span<uint> keyScratch, Span<TValue> valueScratch)
    {
        ValidatePairBuffers(
            keys,
            values,
            keyScratch,
            valueScratch,
            nameof(values),
            nameof(keyScratch),
            nameof(valueScratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, values, keyScratch, valueScratch);
    }

    /// <summary>
    /// Writes into <paramref name="indices"/> the permutation that sorts <paramref name="keys"/>
    /// ascending, leaving <paramref name="keys"/> untouched.
    /// </summary>
    /// <param name="keys">The keys to rank. Not modified.</param>
    /// <param name="indices">Receives <c>keys.Length</c> indices into <paramref name="keys"/>, in ascending key order.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="indices"/> is shorter than <paramref name="keys"/>. It is also thrown when
    /// <paramref name="indices"/> shares storage with <paramref name="keys"/>, but only on the
    /// <see cref="int"/>-keyed overload: the aliasing check is a same-element-type test, and for
    /// every other key type an <see cref="int"/> index buffer can be made to overlap the keys only
    /// by reinterpreting one buffer as another type, which is out of contract rather than checked.
    /// </exception>
    /// <remarks>
    /// The point of an argsort is to avoid moving a wide payload: rank once, then gather. This form
    /// rents three buffers, so a hot loop that already owns its scratch should copy the keys itself
    /// and call <see cref="SortWithScratch{TValue}(Span{uint}, Span{TValue}, Span{uint}, Span{TValue})"/> with
    /// an identity index array as the payload — that is exactly what this does.
    /// </remarks>
    public static void ArgSort(ReadOnlySpan<uint> keys, Span<int> indices)
    {
        SortingGuard.RequireLength(indices.Length, keys.Length, nameof(indices));
        SortingGuard.RequireDistinctStorage(keys, indices, nameof(indices));
        FillIdentity(indices[..keys.Length]);
        if (keys.Length < 2)
        {
            return;
        }

        uint[] keyCopy = ArrayPool<uint>.Shared.Rent(keys.Length);
        uint[] keyScratch = ArrayPool<uint>.Shared.Rent(keys.Length);
        int[] indexScratch = ArrayPool<int>.Shared.Rent(keys.Length);
        try
        {
            keys.CopyTo(keyCopy);
            SortCore(
                keyCopy.AsSpan(0, keys.Length),
                indices[..keys.Length],
                keyScratch.AsSpan(0, keys.Length),
                indexScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(keyCopy);
            ArrayPool<uint>.Shared.Return(keyScratch);
            ArrayPool<int>.Shared.Return(indexScratch);
        }
    }

    private static void SortCore(Span<uint> keys, Span<uint> scratch) =>
        RadixKernel.Sort32<byte>(keys, default, scratch, default, hasValues: false, signed: false);

    private static void SortCore<TValue>(Span<uint> keys, Span<TValue> values, Span<uint> keyScratch, Span<TValue> valueScratch) =>
        RadixKernel.Sort32(keys, values, keyScratch, valueScratch, hasValues: true, signed: false);

    // ---- int ----------------------------------------------------------------------------------

    /// <inheritdoc cref="Sort(Span{uint})"/>
    public static void Sort(Span<int> keys)
    {
        if (keys.Length < 2)
        {
            return;
        }

        int[] scratch = ArrayPool<int>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, scratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<int>.Shared.Return(scratch);
        }
    }

    /// <inheritdoc cref="SortWithScratch(Span{uint}, Span{uint})"/>
    public static void SortWithScratch(Span<int> keys, Span<int> scratch)
    {
        SortingGuard.RequireLength(scratch.Length, keys.Length, nameof(scratch));
        SortingGuard.RequireNoOverlap(keys.Overlaps(scratch), nameof(scratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, scratch);
    }

    /// <inheritdoc cref="Sort{TValue}(Span{uint}, Span{TValue})"/>
    public static void Sort<TValue>(Span<int> keys, Span<TValue> values)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        if (keys.Length < 2)
        {
            return;
        }

        int[] keyScratch = ArrayPool<int>.Shared.Rent(keys.Length);
        TValue[] valueScratch = ArrayPool<TValue>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, values, keyScratch.AsSpan(0, keys.Length), valueScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<int>.Shared.Return(keyScratch);
            ReturnValueScratch(valueScratch);
        }
    }

    /// <inheritdoc cref="SortWithScratch{TValue}(Span{uint}, Span{TValue}, Span{uint}, Span{TValue})"/>
    public static void SortWithScratch<TValue>(Span<int> keys, Span<TValue> values, Span<int> keyScratch, Span<TValue> valueScratch)
    {
        ValidatePairBuffers(
            keys,
            values,
            keyScratch,
            valueScratch,
            nameof(values),
            nameof(keyScratch),
            nameof(valueScratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, values, keyScratch, valueScratch);
    }

    /// <inheritdoc cref="ArgSort(ReadOnlySpan{uint}, Span{int})"/>
    public static void ArgSort(ReadOnlySpan<int> keys, Span<int> indices)
    {
        SortingGuard.RequireLength(indices.Length, keys.Length, nameof(indices));
        SortingGuard.RequireDistinctStorage(keys, indices, nameof(indices));
        FillIdentity(indices[..keys.Length]);
        if (keys.Length < 2)
        {
            return;
        }

        int[] keyCopy = ArrayPool<int>.Shared.Rent(keys.Length);
        int[] keyScratch = ArrayPool<int>.Shared.Rent(keys.Length);
        int[] indexScratch = ArrayPool<int>.Shared.Rent(keys.Length);
        try
        {
            keys.CopyTo(keyCopy);
            SortCore(
                keyCopy.AsSpan(0, keys.Length),
                indices[..keys.Length],
                keyScratch.AsSpan(0, keys.Length),
                indexScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<int>.Shared.Return(keyCopy);
            ArrayPool<int>.Shared.Return(keyScratch);
            ArrayPool<int>.Shared.Return(indexScratch);
        }
    }

    private static void SortCore(Span<int> keys, Span<int> scratch) =>
        RadixKernel.Sort32<byte>(
            MemoryMarshal.Cast<int, uint>(keys),
            default,
            MemoryMarshal.Cast<int, uint>(scratch),
            default,
            hasValues: false,
            signed: true);

    private static void SortCore<TValue>(Span<int> keys, Span<TValue> values, Span<int> keyScratch, Span<TValue> valueScratch) =>
        RadixKernel.Sort32(
            MemoryMarshal.Cast<int, uint>(keys),
            values,
            MemoryMarshal.Cast<int, uint>(keyScratch),
            valueScratch,
            hasValues: true,
            signed: true);

    // ---- ulong --------------------------------------------------------------------------------

    /// <inheritdoc cref="Sort(Span{uint})"/>
    public static void Sort(Span<ulong> keys)
    {
        if (keys.Length < 2)
        {
            return;
        }

        ulong[] scratch = ArrayPool<ulong>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, scratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(scratch);
        }
    }

    /// <inheritdoc cref="SortWithScratch(Span{uint}, Span{uint})"/>
    public static void SortWithScratch(Span<ulong> keys, Span<ulong> scratch)
    {
        SortingGuard.RequireLength(scratch.Length, keys.Length, nameof(scratch));
        SortingGuard.RequireNoOverlap(keys.Overlaps(scratch), nameof(scratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, scratch);
    }

    /// <inheritdoc cref="Sort{TValue}(Span{uint}, Span{TValue})"/>
    public static void Sort<TValue>(Span<ulong> keys, Span<TValue> values)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        if (keys.Length < 2)
        {
            return;
        }

        ulong[] keyScratch = ArrayPool<ulong>.Shared.Rent(keys.Length);
        TValue[] valueScratch = ArrayPool<TValue>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, values, keyScratch.AsSpan(0, keys.Length), valueScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(keyScratch);
            ReturnValueScratch(valueScratch);
        }
    }

    /// <inheritdoc cref="SortWithScratch{TValue}(Span{uint}, Span{TValue}, Span{uint}, Span{TValue})"/>
    public static void SortWithScratch<TValue>(Span<ulong> keys, Span<TValue> values, Span<ulong> keyScratch, Span<TValue> valueScratch)
    {
        ValidatePairBuffers(
            keys,
            values,
            keyScratch,
            valueScratch,
            nameof(values),
            nameof(keyScratch),
            nameof(valueScratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, values, keyScratch, valueScratch);
    }

    /// <inheritdoc cref="ArgSort(ReadOnlySpan{uint}, Span{int})"/>
    public static void ArgSort(ReadOnlySpan<ulong> keys, Span<int> indices)
    {
        SortingGuard.RequireLength(indices.Length, keys.Length, nameof(indices));
        SortingGuard.RequireDistinctStorage(keys, indices, nameof(indices));
        FillIdentity(indices[..keys.Length]);
        if (keys.Length < 2)
        {
            return;
        }

        ulong[] keyCopy = ArrayPool<ulong>.Shared.Rent(keys.Length);
        ulong[] keyScratch = ArrayPool<ulong>.Shared.Rent(keys.Length);
        int[] indexScratch = ArrayPool<int>.Shared.Rent(keys.Length);
        try
        {
            keys.CopyTo(keyCopy);
            SortCore(
                keyCopy.AsSpan(0, keys.Length),
                indices[..keys.Length],
                keyScratch.AsSpan(0, keys.Length),
                indexScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(keyCopy);
            ArrayPool<ulong>.Shared.Return(keyScratch);
            ArrayPool<int>.Shared.Return(indexScratch);
        }
    }

    private static void SortCore(Span<ulong> keys, Span<ulong> scratch) =>
        RadixKernel.Sort64<byte>(keys, default, scratch, default, hasValues: false, signed: false);

    private static void SortCore<TValue>(Span<ulong> keys, Span<TValue> values, Span<ulong> keyScratch, Span<TValue> valueScratch) =>
        RadixKernel.Sort64(keys, values, keyScratch, valueScratch, hasValues: true, signed: false);

    // ---- long ---------------------------------------------------------------------------------

    /// <inheritdoc cref="Sort(Span{uint})"/>
    public static void Sort(Span<long> keys)
    {
        if (keys.Length < 2)
        {
            return;
        }

        long[] scratch = ArrayPool<long>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, scratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<long>.Shared.Return(scratch);
        }
    }

    /// <inheritdoc cref="SortWithScratch(Span{uint}, Span{uint})"/>
    public static void SortWithScratch(Span<long> keys, Span<long> scratch)
    {
        SortingGuard.RequireLength(scratch.Length, keys.Length, nameof(scratch));
        SortingGuard.RequireNoOverlap(keys.Overlaps(scratch), nameof(scratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, scratch);
    }

    /// <inheritdoc cref="Sort{TValue}(Span{uint}, Span{TValue})"/>
    public static void Sort<TValue>(Span<long> keys, Span<TValue> values)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        if (keys.Length < 2)
        {
            return;
        }

        long[] keyScratch = ArrayPool<long>.Shared.Rent(keys.Length);
        TValue[] valueScratch = ArrayPool<TValue>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, values, keyScratch.AsSpan(0, keys.Length), valueScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<long>.Shared.Return(keyScratch);
            ReturnValueScratch(valueScratch);
        }
    }

    /// <inheritdoc cref="SortWithScratch{TValue}(Span{uint}, Span{TValue}, Span{uint}, Span{TValue})"/>
    public static void SortWithScratch<TValue>(Span<long> keys, Span<TValue> values, Span<long> keyScratch, Span<TValue> valueScratch)
    {
        ValidatePairBuffers(
            keys,
            values,
            keyScratch,
            valueScratch,
            nameof(values),
            nameof(keyScratch),
            nameof(valueScratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, values, keyScratch, valueScratch);
    }

    /// <inheritdoc cref="ArgSort(ReadOnlySpan{uint}, Span{int})"/>
    public static void ArgSort(ReadOnlySpan<long> keys, Span<int> indices)
    {
        SortingGuard.RequireLength(indices.Length, keys.Length, nameof(indices));
        SortingGuard.RequireDistinctStorage(keys, indices, nameof(indices));
        FillIdentity(indices[..keys.Length]);
        if (keys.Length < 2)
        {
            return;
        }

        long[] keyCopy = ArrayPool<long>.Shared.Rent(keys.Length);
        long[] keyScratch = ArrayPool<long>.Shared.Rent(keys.Length);
        int[] indexScratch = ArrayPool<int>.Shared.Rent(keys.Length);
        try
        {
            keys.CopyTo(keyCopy);
            SortCore(
                keyCopy.AsSpan(0, keys.Length),
                indices[..keys.Length],
                keyScratch.AsSpan(0, keys.Length),
                indexScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<long>.Shared.Return(keyCopy);
            ArrayPool<long>.Shared.Return(keyScratch);
            ArrayPool<int>.Shared.Return(indexScratch);
        }
    }

    private static void SortCore(Span<long> keys, Span<long> scratch) =>
        RadixKernel.Sort64<byte>(
            MemoryMarshal.Cast<long, ulong>(keys),
            default,
            MemoryMarshal.Cast<long, ulong>(scratch),
            default,
            hasValues: false,
            signed: true);

    private static void SortCore<TValue>(Span<long> keys, Span<TValue> values, Span<long> keyScratch, Span<TValue> valueScratch) =>
        RadixKernel.Sort64(
            MemoryMarshal.Cast<long, ulong>(keys),
            values,
            MemoryMarshal.Cast<long, ulong>(keyScratch),
            valueScratch,
            hasValues: true,
            signed: true);

    // ---- float --------------------------------------------------------------------------------

    /// <inheritdoc cref="Sort(Span{uint})"/>
    public static void Sort(Span<float> keys)
    {
        if (keys.Length < 2)
        {
            return;
        }

        float[] scratch = ArrayPool<float>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, scratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<float>.Shared.Return(scratch);
        }
    }

    /// <inheritdoc cref="SortWithScratch(Span{uint}, Span{uint})"/>
    public static void SortWithScratch(Span<float> keys, Span<float> scratch)
    {
        SortingGuard.RequireLength(scratch.Length, keys.Length, nameof(scratch));
        SortingGuard.RequireNoOverlap(keys.Overlaps(scratch), nameof(scratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, scratch);
    }

    /// <inheritdoc cref="Sort{TValue}(Span{uint}, Span{TValue})"/>
    public static void Sort<TValue>(Span<float> keys, Span<TValue> values)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        if (keys.Length < 2)
        {
            return;
        }

        float[] keyScratch = ArrayPool<float>.Shared.Rent(keys.Length);
        TValue[] valueScratch = ArrayPool<TValue>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, values, keyScratch.AsSpan(0, keys.Length), valueScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<float>.Shared.Return(keyScratch);
            ReturnValueScratch(valueScratch);
        }
    }

    /// <inheritdoc cref="SortWithScratch{TValue}(Span{uint}, Span{TValue}, Span{uint}, Span{TValue})"/>
    public static void SortWithScratch<TValue>(Span<float> keys, Span<TValue> values, Span<float> keyScratch, Span<TValue> valueScratch)
    {
        ValidatePairBuffers(
            keys,
            values,
            keyScratch,
            valueScratch,
            nameof(values),
            nameof(keyScratch),
            nameof(valueScratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, values, keyScratch, valueScratch);
    }

    /// <inheritdoc cref="ArgSort(ReadOnlySpan{uint}, Span{int})"/>
    public static void ArgSort(ReadOnlySpan<float> keys, Span<int> indices)
    {
        SortingGuard.RequireLength(indices.Length, keys.Length, nameof(indices));
        SortingGuard.RequireDistinctStorage(keys, indices, nameof(indices));
        FillIdentity(indices[..keys.Length]);
        if (keys.Length < 2)
        {
            return;
        }

        float[] keyCopy = ArrayPool<float>.Shared.Rent(keys.Length);
        float[] keyScratch = ArrayPool<float>.Shared.Rent(keys.Length);
        int[] indexScratch = ArrayPool<int>.Shared.Rent(keys.Length);
        try
        {
            keys.CopyTo(keyCopy);
            SortCore(
                keyCopy.AsSpan(0, keys.Length),
                indices[..keys.Length],
                keyScratch.AsSpan(0, keys.Length),
                indexScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<float>.Shared.Return(keyCopy);
            ArrayPool<float>.Shared.Return(keyScratch);
            ArrayPool<int>.Shared.Return(indexScratch);
        }
    }

    private static void SortCore(Span<float> keys, Span<float> scratch)
    {
        Span<uint> bits = MemoryMarshal.Cast<float, uint>(keys);
        RadixKernel.FloatToOrdered32(bits);
        RadixKernel.Sort32<byte>(bits, default, MemoryMarshal.Cast<float, uint>(scratch), default, hasValues: false, signed: false);
        RadixKernel.OrderedToFloat32(bits);
    }

    private static void SortCore<TValue>(Span<float> keys, Span<TValue> values, Span<float> keyScratch, Span<TValue> valueScratch)
    {
        Span<uint> bits = MemoryMarshal.Cast<float, uint>(keys);
        RadixKernel.FloatToOrdered32(bits);
        RadixKernel.Sort32(bits, values, MemoryMarshal.Cast<float, uint>(keyScratch), valueScratch, hasValues: true, signed: false);
        RadixKernel.OrderedToFloat32(bits);
    }

    // ---- double -------------------------------------------------------------------------------

    /// <inheritdoc cref="Sort(Span{uint})"/>
    public static void Sort(Span<double> keys)
    {
        if (keys.Length < 2)
        {
            return;
        }

        double[] scratch = ArrayPool<double>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, scratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<double>.Shared.Return(scratch);
        }
    }

    /// <inheritdoc cref="SortWithScratch(Span{uint}, Span{uint})"/>
    public static void SortWithScratch(Span<double> keys, Span<double> scratch)
    {
        SortingGuard.RequireLength(scratch.Length, keys.Length, nameof(scratch));
        SortingGuard.RequireNoOverlap(keys.Overlaps(scratch), nameof(scratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, scratch);
    }

    /// <inheritdoc cref="Sort{TValue}(Span{uint}, Span{TValue})"/>
    public static void Sort<TValue>(Span<double> keys, Span<TValue> values)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        if (keys.Length < 2)
        {
            return;
        }

        double[] keyScratch = ArrayPool<double>.Shared.Rent(keys.Length);
        TValue[] valueScratch = ArrayPool<TValue>.Shared.Rent(keys.Length);
        try
        {
            SortCore(keys, values, keyScratch.AsSpan(0, keys.Length), valueScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<double>.Shared.Return(keyScratch);
            ReturnValueScratch(valueScratch);
        }
    }

    /// <inheritdoc cref="SortWithScratch{TValue}(Span{uint}, Span{TValue}, Span{uint}, Span{TValue})"/>
    public static void SortWithScratch<TValue>(Span<double> keys, Span<TValue> values, Span<double> keyScratch, Span<TValue> valueScratch)
    {
        ValidatePairBuffers(
            keys,
            values,
            keyScratch,
            valueScratch,
            nameof(values),
            nameof(keyScratch),
            nameof(valueScratch));
        if (keys.Length < 2)
        {
            return;
        }

        SortCore(keys, values, keyScratch, valueScratch);
    }

    /// <inheritdoc cref="ArgSort(ReadOnlySpan{uint}, Span{int})"/>
    public static void ArgSort(ReadOnlySpan<double> keys, Span<int> indices)
    {
        SortingGuard.RequireLength(indices.Length, keys.Length, nameof(indices));
        SortingGuard.RequireDistinctStorage(keys, indices, nameof(indices));
        FillIdentity(indices[..keys.Length]);
        if (keys.Length < 2)
        {
            return;
        }

        double[] keyCopy = ArrayPool<double>.Shared.Rent(keys.Length);
        double[] keyScratch = ArrayPool<double>.Shared.Rent(keys.Length);
        int[] indexScratch = ArrayPool<int>.Shared.Rent(keys.Length);
        try
        {
            keys.CopyTo(keyCopy);
            SortCore(
                keyCopy.AsSpan(0, keys.Length),
                indices[..keys.Length],
                keyScratch.AsSpan(0, keys.Length),
                indexScratch.AsSpan(0, keys.Length));
        }
        finally
        {
            ArrayPool<double>.Shared.Return(keyCopy);
            ArrayPool<double>.Shared.Return(keyScratch);
            ArrayPool<int>.Shared.Return(indexScratch);
        }
    }

    private static void SortCore(Span<double> keys, Span<double> scratch)
    {
        Span<ulong> bits = MemoryMarshal.Cast<double, ulong>(keys);
        RadixKernel.FloatToOrdered64(bits);
        RadixKernel.Sort64<byte>(bits, default, MemoryMarshal.Cast<double, ulong>(scratch), default, hasValues: false, signed: false);
        RadixKernel.OrderedToFloat64(bits);
    }

    private static void SortCore<TValue>(Span<double> keys, Span<TValue> values, Span<double> keyScratch, Span<TValue> valueScratch)
    {
        Span<ulong> bits = MemoryMarshal.Cast<double, ulong>(keys);
        RadixKernel.FloatToOrdered64(bits);
        RadixKernel.Sort64(bits, values, MemoryMarshal.Cast<double, ulong>(keyScratch), valueScratch, hasValues: true, signed: false);
        RadixKernel.OrderedToFloat64(bits);
    }

    // ---- shared -------------------------------------------------------------------------------

    // The whole argument contract of a key+payload+scratch overload: three lengths, and every pair
    // of buffers the sort writes kept in distinct storage. Callers pass their own nameof() so a
    // parameter rename cannot leave a stale name in the thrown ArgumentException.
    //
    // All six pairs are checked, not just the two obvious ones. The kernel ping-pongs between
    // (keys, values) and (keyScratch, valueScratch), so on alternating passes it writes a key and a
    // payload element to the same index of whichever pair is the destination — and if any two of
    // those four spans share storage, the second write lands on top of the first. That corrupts the
    // result silently rather than failing, which is exactly the failure mode worth an argument check.
    private static void ValidatePairBuffers<TKey, TValue>(
        Span<TKey> keys,
        Span<TValue> values,
        Span<TKey> keyScratch,
        Span<TValue> valueScratch,
        string valuesName,
        string keyScratchName,
        string valueScratchName)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, valuesName);
        SortingGuard.RequireLength(keyScratch.Length, keys.Length, keyScratchName);
        SortingGuard.RequireLength(valueScratch.Length, keys.Length, valueScratchName);

        SortingGuard.RequireNoOverlap(keys.Overlaps(keyScratch), keyScratchName);
        SortingGuard.RequireNoOverlap(values.Overlaps(valueScratch), valueScratchName);
        SortingGuard.RequireDistinctStorage(keys, values, valuesName);
        SortingGuard.RequireDistinctStorage(keys, valueScratch, valueScratchName);
        SortingGuard.RequireDistinctStorage(keyScratch, values, valuesName);
        SortingGuard.RequireDistinctStorage(keyScratch, valueScratch, valueScratchName);
    }

    private static void FillIdentity(Span<int> indices)
    {
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }
    }

    // A pooled payload buffer holding references keeps whatever it last held alive until the next
    // renter overwrites the slot, so clear it on the way back. Blittable payloads skip the memset.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReturnValueScratch<TValue>(TValue[] buffer) =>
        ArrayPool<TValue>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TValue>());
}
