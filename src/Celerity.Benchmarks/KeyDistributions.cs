using Celerity.Hashing;

/// <summary>
/// The key-value distributions swept by the workload benchmarks.
/// </summary>
/// <remarks>
/// A hash table's measured throughput is meaningless without saying <em>which</em>
/// keys it was measured on: the BCL <see cref="Dictionary{TKey, TValue}"/> applies
/// a Fibonacci/prime mix to every hash, so it is near-immune to key shape, whereas
/// Celerity's fast struct hashers (e.g. <see cref="Int32WangNaiveHasher"/>) trade
/// avalanche for latency and therefore win or lose depending on the input. These
/// distributions exist so each benchmark states its assumptions explicitly.
/// </remarks>
public enum Distribution
{
    /// <summary>
    /// Random keys spread across the full positive range — the "average" case and
    /// the historical baseline of the suite. Fast hashers do well here because the
    /// input already carries entropy in every bit.
    /// </summary>
    Uniform,

    /// <summary>
    /// Dense, contiguous keys (<c>1, 2, 3, …</c>) — the common auto-increment /
    /// array-index workload. A weak low-bit hash maps these to consecutive buckets,
    /// which is actually collision-free, so this favours the cheap hashers.
    /// </summary>
    Sequential,

    /// <summary>
    /// A handful of dense blocks scattered across the range (e.g. per-shard or
    /// per-tenant ID ranges). Each block is locally sequential but the blocks are
    /// far apart, which stresses how the high bits of a key reach the bucket index.
    /// </summary>
    Clustered,

    /// <summary>
    /// Keys hand-crafted to collapse onto a tiny set of buckets under the
    /// <em>naive</em> XOR-fold hashers (<see cref="Int32WangNaiveHasher"/> /
    /// <see cref="Int64WangNaiveHasher"/>), driving their probe chains to O(n).
    /// The full Murmur3 / Wang finalizers shatter the same set, so this is where
    /// "pick the right hasher" stops being a slogan and shows up in the numbers.
    /// </summary>
    Adversarial,
}

/// <summary>
/// Deterministic key generators for the workload benchmarks. Every generator is
/// seeded identically so a key set is byte-for-byte reproducible across runs,
/// collections, and the A/B benchmark comparison in CI.
/// </summary>
public static class KeyDistributions
{
    private const int Seed = 42;

    /// <summary>
    /// The largest <see cref="Distribution.Adversarial"/> key set that can be built.
    /// The construction needs <c>key.low16 == key.high16</c>, which only yields
    /// <c>2^16 - 1</c> distinct non-zero keys.
    /// </summary>
    public const int MaxAdversarialCount = 0xFFFF;

    /// <summary>Builds <paramref name="count"/> distinct, non-zero <see cref="int"/> keys of the given <paramref name="distribution"/>.</summary>
    public static int[] Int32(Distribution distribution, int count) => distribution switch
    {
        Distribution.Uniform => UniformInt32(count),
        Distribution.Sequential => SequentialInt32(count),
        Distribution.Clustered => ClusteredInt32(count),
        Distribution.Adversarial => AdversarialInt32(count),
        _ => throw new ArgumentOutOfRangeException(nameof(distribution)),
    };

    /// <summary>Builds <paramref name="count"/> distinct, non-zero <see cref="long"/> keys of the given <paramref name="distribution"/>.</summary>
    public static long[] Int64(Distribution distribution, int count) => distribution switch
    {
        Distribution.Uniform => UniformInt64(count),
        Distribution.Sequential => SequentialInt64(count),
        Distribution.Clustered => ClusteredInt64(count),
        Distribution.Adversarial => AdversarialInt64(count),
        _ => throw new ArgumentOutOfRangeException(nameof(distribution)),
    };

    /// <summary>
    /// Returns a copy of <paramref name="source"/> shuffled with a fixed seed.
    /// Used by the cache-locality benchmark to compare in-order probing (keys hit
    /// buckets in roughly ascending order) against a randomized probe order.
    /// </summary>
    public static T[] Shuffle<T>(T[] source)
    {
        var copy = (T[])source.Clone();
        Random rand = new(Seed + 1);
        for (int i = copy.Length - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy;
    }

    private static int[] UniformInt32(int count)
    {
        Random rand = new(Seed);
        var set = new HashSet<int>(count);
        var keys = new int[count];
        int n = 0;
        while (n < count)
        {
            int key = rand.Next(1, int.MaxValue);
            if (set.Add(key))
            {
                keys[n++] = key;
            }
        }
        return keys;
    }

    private static int[] SequentialInt32(int count)
    {
        var keys = new int[count];
        for (int i = 0; i < count; i++)
        {
            keys[i] = i + 1; // skip 0 — the sentinel EMPTY_KEY in IntDictionary.
        }
        return keys;
    }

    private static int[] ClusteredInt32(int count)
    {
        // ~64 dense blocks whose base addresses are spread across the int range.
        // Within a block keys are contiguous; between blocks they jump by ~33M,
        // mimicking sharded/tenant ID spaces.
        const int clusters = 64;
        const int stride = int.MaxValue / clusters;
        var keys = new int[count];
        int perCluster = (count + clusters - 1) / clusters;
        int n = 0;
        for (int c = 0; c < clusters && n < count; c++)
        {
            long baseValue = 1L + (long)c * stride;
            for (int i = 0; i < perCluster && n < count; i++)
            {
                keys[n++] = (int)(baseValue + i);
            }
        }
        return keys;
    }

    private static int[] AdversarialInt32(int count)
    {
        ThrowIfAdversarialTooLarge(count);
        // key = (Y << 16) | Y  ⇒  Int32WangNaiveHasher.Hash = key ^ (key >> 16) = Y << 16,
        // whose low 16 bits are always 0. Every such key therefore lands in a handful of
        // buckets (bucket 0 for tables ≤ 2^16 slots), forcing the naive hasher's probe
        // chain to O(n). Murmur3 avalanches the same keys back to a uniform spread.
        var keys = new int[count];
        for (int i = 0; i < count; i++)
        {
            int y = i + 1; // Y ∈ [1, 65535]; Y != 0 keeps the key non-zero.
            keys[i] = (y << 16) | y;
        }
        return keys;
    }

    private static long[] UniformInt64(int count)
    {
        Random rand = new(Seed);
        var set = new HashSet<long>(count);
        var keys = new long[count];
        int n = 0;
        while (n < count)
        {
            long key = ((long)rand.Next() << 32) | (uint)rand.Next();
            if (key != 0 && set.Add(key))
            {
                keys[n++] = key;
            }
        }
        return keys;
    }

    private static long[] SequentialInt64(int count)
    {
        var keys = new long[count];
        for (int i = 0; i < count; i++)
        {
            keys[i] = i + 1;
        }
        return keys;
    }

    private static long[] ClusteredInt64(int count)
    {
        // Same shape as the int variant but with the cluster bases pushed into the
        // upper 32 bits — the layout the Int64WangNaiveHasher's extra high-half fold
        // is specifically designed to cope with (monotonic IDs + shard bits up top).
        const int clusters = 64;
        var keys = new long[count];
        int perCluster = (count + clusters - 1) / clusters;
        int n = 0;
        for (int c = 0; c < clusters && n < count; c++)
        {
            long baseValue = ((long)(c + 1) << 40) | 1;
            for (int i = 0; i < perCluster && n < count; i++)
            {
                keys[n++] = baseValue + i;
            }
        }
        return keys;
    }

    private static long[] AdversarialInt64(int count)
    {
        ThrowIfAdversarialTooLarge(count);
        // With the high 32 bits zero, Int64WangNaiveHasher reduces to the int32 fold,
        // so the same (Y << 16) | Y construction collapses the low 16 hash bits to 0.
        var keys = new long[count];
        for (int i = 0; i < count; i++)
        {
            long y = i + 1;
            keys[i] = (y << 16) | y;
        }
        return keys;
    }

    private static void ThrowIfAdversarialTooLarge(int count)
    {
        if (count > MaxAdversarialCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                $"Adversarial key sets are limited to {MaxAdversarialCount} distinct keys.");
        }
    }
}
