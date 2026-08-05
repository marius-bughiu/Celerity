namespace Celerity.Sorting;

/// <summary>
/// The LSD radix passes that every <see cref="RadixSort"/> entry point funnels into, plus the
/// order-preserving bit transforms that let signed and IEEE-754 keys reuse them.
/// </summary>
/// <remarks>
/// There is one kernel per key width rather than one generic kernel over
/// <c>IBinaryInteger&lt;T&gt;</c>: the digit extraction is the innermost operation in the whole
/// package and is not worth routing through generic-math conversions. Payload movement is a
/// parameter of the same kernel (<c>hasValues</c>, tested once per pass rather than per element)
/// so the histogram, the skip rule, the signed prefix order and the ping-pong parity are written
/// once and cannot drift between the keys-only and key+payload forms.
/// </remarks>
internal static class RadixKernel
{
    internal const int DigitBits = 8;
    internal const int Radix = 1 << DigitBits;
    internal const int DigitMask = Radix - 1;

    internal const int Passes32 = 32 / DigitBits;
    internal const int Passes64 = 64 / DigitBits;

    /// <summary>
    /// Sorts <paramref name="keys"/> ascending as unsigned 32-bit values, carrying
    /// <paramref name="values"/> along when <paramref name="hasValues"/> is set.
    /// </summary>
    /// <param name="keys">The keys, at least two of them. Sorted in place.</param>
    /// <param name="values">The payload, parallel to <paramref name="keys"/>. Ignored unless <paramref name="hasValues"/>.</param>
    /// <param name="keyScratch">A key-sized ping-pong buffer. Its contents on entry are irrelevant.</param>
    /// <param name="valueScratch">A payload-sized ping-pong buffer. Ignored unless <paramref name="hasValues"/>.</param>
    /// <param name="hasValues">Whether the payload spans participate.</param>
    /// <param name="signed">
    /// Whether the top digit carries a sign bit. When set, the last pass runs its prefix sum from
    /// bucket 128 and wraps, so the keys with the sign bit set land before the rest — which is the
    /// whole cost of supporting signed keys. Nothing else in the kernel knows about signedness.
    /// </param>
    internal static void Sort32<TValue>(
        Span<uint> keys,
        Span<TValue> values,
        Span<uint> keyScratch,
        Span<TValue> valueScratch,
        bool hasValues,
        bool signed)
    {
        int n = keys.Length;

        // Every pass's histogram from a single read of the keys. The alternative — one counting
        // pass per digit — reads the keys four times for the same numbers.
        Span<int> counts = stackalloc int[Passes32 * Radix];
        counts.Clear();
        for (int i = 0; i < n; i++)
        {
            uint k = keys[i];
            counts[(int)(k & DigitMask)]++;
            counts[Radix + (int)((k >> 8) & DigitMask)]++;
            counts[(2 * Radix) + (int)((k >> 16) & DigitMask)]++;
            counts[(3 * Radix) + (int)((k >> 24) & DigitMask)]++;
        }

        bool inScratch = false;
        for (int pass = 0; pass < Passes32; pass++)
        {
            int shift = pass * DigitBits;
            Span<uint> src = inScratch ? keyScratch : keys;
            Span<int> hist = counts.Slice(pass * Radix, Radix);

            // Every key shares this digit, so the pass would be an identity permutation. This is
            // what makes a small-range key set cost one pass instead of four.
            if (hist[(int)((src[0] >> shift) & DigitMask)] == n)
            {
                continue;
            }

            ExclusivePrefixSum(hist, signed && pass == Passes32 - 1);

            Span<uint> dst = inScratch ? keys : keyScratch;
            if (hasValues)
            {
                Span<TValue> valueSrc = inScratch ? valueScratch : values;
                Span<TValue> valueDst = inScratch ? values : valueScratch;
                for (int i = 0; i < n; i++)
                {
                    uint k = src[i];
                    int p = hist[(int)((k >> shift) & DigitMask)]++;
                    dst[p] = k;
                    valueDst[p] = valueSrc[i];
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    uint k = src[i];
                    int p = hist[(int)((k >> shift) & DigitMask)]++;
                    dst[p] = k;
                }
            }

            inScratch = !inScratch;
        }

        // An odd number of executed passes leaves the answer in the scratch buffer.
        if (inScratch)
        {
            keyScratch[..n].CopyTo(keys);
            if (hasValues)
            {
                valueScratch[..n].CopyTo(values);
            }
        }
    }

    /// <inheritdoc cref="Sort32{TValue}(Span{uint}, Span{TValue}, Span{uint}, Span{TValue}, bool, bool)"/>
    internal static void Sort64<TValue>(
        Span<ulong> keys,
        Span<TValue> values,
        Span<ulong> keyScratch,
        Span<TValue> valueScratch,
        bool hasValues,
        bool signed)
    {
        int n = keys.Length;

        Span<int> counts = stackalloc int[Passes64 * Radix];
        counts.Clear();
        for (int i = 0; i < n; i++)
        {
            ulong k = keys[i];
            counts[(int)(k & DigitMask)]++;
            counts[Radix + (int)((k >> 8) & DigitMask)]++;
            counts[(2 * Radix) + (int)((k >> 16) & DigitMask)]++;
            counts[(3 * Radix) + (int)((k >> 24) & DigitMask)]++;
            counts[(4 * Radix) + (int)((k >> 32) & DigitMask)]++;
            counts[(5 * Radix) + (int)((k >> 40) & DigitMask)]++;
            counts[(6 * Radix) + (int)((k >> 48) & DigitMask)]++;
            counts[(7 * Radix) + (int)((k >> 56) & DigitMask)]++;
        }

        bool inScratch = false;
        for (int pass = 0; pass < Passes64; pass++)
        {
            int shift = pass * DigitBits;
            Span<ulong> src = inScratch ? keyScratch : keys;
            Span<int> hist = counts.Slice(pass * Radix, Radix);

            if (hist[(int)((src[0] >> shift) & DigitMask)] == n)
            {
                continue;
            }

            ExclusivePrefixSum(hist, signed && pass == Passes64 - 1);

            Span<ulong> dst = inScratch ? keys : keyScratch;
            if (hasValues)
            {
                Span<TValue> valueSrc = inScratch ? valueScratch : values;
                Span<TValue> valueDst = inScratch ? values : valueScratch;
                for (int i = 0; i < n; i++)
                {
                    ulong k = src[i];
                    int p = hist[(int)((k >> shift) & DigitMask)]++;
                    dst[p] = k;
                    valueDst[p] = valueSrc[i];
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    ulong k = src[i];
                    int p = hist[(int)((k >> shift) & DigitMask)]++;
                    dst[p] = k;
                }
            }

            inScratch = !inScratch;
        }

        if (inScratch)
        {
            keyScratch[..n].CopyTo(keys);
            if (hasValues)
            {
                valueScratch[..n].CopyTo(values);
            }
        }
    }

    /// <summary>
    /// Turns bucket counts into the exclusive scatter offsets the pass writes at. When
    /// <paramref name="signFirst"/> is set the walk starts at bucket 128 and wraps, which places
    /// the sign-bit-set buckets ahead of the rest.
    /// </summary>
    private static void ExclusivePrefixSum(Span<int> histogram, bool signFirst)
    {
        int start = signFirst ? Radix / 2 : 0;
        int sum = 0;
        for (int i = 0; i < Radix; i++)
        {
            int bucket = (start + i) & DigitMask;
            int count = histogram[bucket];
            histogram[bucket] = sum;
            sum += count;
        }
    }

    /// <summary>
    /// Maps IEEE-754 binary32 bit patterns onto unsigned integers that compare in the same order —
    /// negatives inverted so larger magnitudes come first, non-negatives lifted above them.
    /// </summary>
    /// <remarks>
    /// This is the error-prone half of float support, so it is spelled out. For a negative key the
    /// sign bit is set and the magnitude bits increase as the value decreases, so the whole pattern
    /// is complemented: that clears the top bit (putting negatives below every non-negative) and
    /// reverses the magnitude order. For a non-negative key only the top bit is set, which lifts it
    /// above every complemented negative and leaves the already-correct magnitude order alone.
    /// Both branches are bijections, so <see cref="OrderedToFloat32"/> undoes them exactly —
    /// including for NaN, which this treats as an ordinary bit pattern.
    /// </remarks>
    internal static void FloatToOrdered32(Span<uint> bits)
    {
        for (int i = 0; i < bits.Length; i++)
        {
            uint u = bits[i];
            bits[i] = (u & 0x8000_0000u) != 0 ? ~u : u | 0x8000_0000u;
        }
    }

    /// <summary>Inverts <see cref="FloatToOrdered32"/>, restoring the original bit patterns.</summary>
    internal static void OrderedToFloat32(Span<uint> bits)
    {
        for (int i = 0; i < bits.Length; i++)
        {
            uint u = bits[i];
            bits[i] = (u & 0x8000_0000u) != 0 ? u & 0x7FFF_FFFFu : ~u;
        }
    }

    /// <inheritdoc cref="FloatToOrdered32"/>
    internal static void FloatToOrdered64(Span<ulong> bits)
    {
        for (int i = 0; i < bits.Length; i++)
        {
            ulong u = bits[i];
            bits[i] = (u & 0x8000_0000_0000_0000ul) != 0 ? ~u : u | 0x8000_0000_0000_0000ul;
        }
    }

    /// <inheritdoc cref="OrderedToFloat32"/>
    internal static void OrderedToFloat64(Span<ulong> bits)
    {
        for (int i = 0; i < bits.Length; i++)
        {
            ulong u = bits[i];
            bits[i] = (u & 0x8000_0000_0000_0000ul) != 0 ? u & 0x7FFF_FFFF_FFFF_FFFFul : ~u;
        }
    }
}
