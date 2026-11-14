using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// The table layout shared by <see cref="FrozenCelerityDictionary{TValue, THasher}"/> and
/// <see cref="FrozenCeleritySet{THasher}"/>: the slot mapping both the build and every lookup use,
/// the search for a collision-free (perfect) placement, and the size of the linear-probing table
/// built when no perfect placement is found.
/// </summary>
/// <remarks>
/// <para>
/// A perfect placement maps each of <c>n</c> keys to its own slot through one seeded mix of its raw
/// hash code. For a single seed that succeeds with probability
/// <c>∏(1 − i/m) ≤ e^(−n(n−1)/2m)</c> in a table of <c>m</c> slots (the birthday bound), so it is
/// realistic only while <c>m</c> grows with <c>n²</c>: a few hundred keys at most within the
/// <see cref="MaxSizeMultiplier"/> cap, and never past 333. Larger key sets always take the
/// linear-probing table. The
/// search therefore skips every candidate size the bound rules out instead of spending its whole
/// seed budget there, and places the trial keys into one reused stamp buffer rather than a fresh
/// pair of table arrays per attempt.
/// </para>
/// </remarks>
internal static class FrozenHashLayout
{
    // Highest mixing seed tried per candidate table size before moving to the next size: seeds 0
    // through SeedBudget, so 513 in all.
    private const int SeedBudget = 512;

    // Largest multiple of NextPowerOfTwo(n) the perfect search grows the table to. 8 means up to 8×
    // the minimum table size.
    private const int MaxSizeMultiplier = 8;

    // A candidate size m is skipped once n(n − 1) > HopelessRatio · m, the point where the birthday
    // bound leaves every seed in the budget together less than a one-in-a-thousand chance:
    // (SeedBudget + 1) · e^(−HopelessRatio / 2) = 513 · e^(−13.5) ≈ 7e-4. A size just inside that
    // line still costs a whole pass of seeds, so a tighter line would mostly buy slower builds.
    private const int HopelessRatio = 27;

    // Maximum fraction of the linear-probing table's slots that hold a key. At most half full, a hit
    // inspects at most about 1.5 slots on average and a miss at most about 2.5, and a vacant slot to
    // end a miss's probe always exists.
    private const float FallbackLoadFactor = 0.5f;

    /// <summary>
    /// Maps a key's raw hash code, combined with the table's seed, to a well-spread 32-bit value
    /// whose low bits pick the slot. The build and every lookup must use this same mapping.
    /// </summary>
    /// <remarks>
    /// Based on the "lowbias32" integer finalizer; the seed is folded in first so different seeds
    /// give different, but each individually well-spread, placements of the same keys.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Mix(uint hash, int seed)
    {
        uint h = hash + unchecked((uint)seed * 0x9E3779B9u);
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        h *= 0x846CA68Bu;
        h ^= h >> 16;
        return h;
    }

    /// <summary>
    /// Searches table sizes <c>NextPowerOfTwo(n)</c> through <see cref="MaxSizeMultiplier"/> times
    /// that, and up to <c><see cref="SeedBudget"/> + 1</c> seeds per size, for a placement in which
    /// every hash in <paramref name="baseHashes"/> lands in its own slot.
    /// </summary>
    /// <param name="baseHashes">The raw hash code of each distinct key.</param>
    /// <param name="size">The power-of-two table size of the placement found; 0 if none was.</param>
    /// <param name="seed">The seed of the placement found; 0 if none was.</param>
    /// <returns><c>true</c> if a collision-free placement was found.</returns>
    internal static bool TryFindPerfect(int[] baseHashes, out int size, out int seed)
    {
        int n = baseHashes.Length;
        long pairs = (long)n * (n - 1);
        int minSize = FastUtils.NextPowerOfTwo(n);

        // Hopeless even at the largest candidate size: skip the search outright. Past this check
        // n(n − 1) <= 27 · 8 · NextPowerOfTwo(n), which holds only for n <= 333, so every candidate
        // size below is at most 4,096 slots and the doubling cannot overflow.
        if (pairs > (long)HopelessRatio * MaxSizeMultiplier * minSize)
        {
            size = 0;
            seed = 0;
            return false;
        }

        int maxSize = minSize * MaxSizeMultiplier;

        // One stamp per slot, reused by every attempt: a slot is taken in the current attempt when its
        // stamp equals that attempt's generation, so nothing is cleared or allocated between attempts.
        var stamps = new int[maxSize];
        int generation = 0;

        for (size = minSize; size <= maxSize; size <<= 1)
        {
            if (pairs > (long)HopelessRatio * size)
                continue;

            uint mask = (uint)size - 1;
            for (seed = 0; seed <= SeedBudget; seed++)
            {
                if (TryPlace(baseHashes, stamps, mask, seed, ++generation))
                    return true;
            }
        }

        size = 0;
        seed = 0;
        return false;
    }

    /// <summary>
    /// Returns the size of the linear-probing table for <paramref name="count"/> keys: the smallest
    /// power of two that keeps the load at or below <see cref="FallbackLoadFactor"/>, capped at
    /// <see cref="FastUtils.MaxPowerOfTwoCapacity"/> slots. Either way it is strictly greater than
    /// <paramref name="count"/>, which the constructors keep below that cap, so a miss's probe always
    /// ends at a vacant slot.
    /// </summary>
    internal static int FallbackSize(int count) => FastUtils.MinTableSizeFor(count, FallbackLoadFactor);

    // Places every hash with one (size, seed), stamping each slot it takes with this attempt's
    // generation, and reports whether any two landed in the same slot.
    private static bool TryPlace(int[] baseHashes, int[] stamps, uint mask, int seed, int generation)
    {
        foreach (int hash in baseHashes)
        {
            uint slot = Mix(unchecked((uint)hash), seed) & mask;
            if (stamps[slot] == generation)
                return false;
            stamps[slot] = generation;
        }

        return true;
    }
}
