using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class ProbeStatisticsEvaluatorTests
{
    // A hasher that maps every key to the same code — forces a maximal linear-probe chain.
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 7;
    }

    // A hasher that returns the key unchanged — collision-free on contiguous ranges.
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    private static int[] Range(int count, int start = 0)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = start + i;
        }

        return result;
    }

    // The adversarial construction the benchmarks use: key = (y << 16) | y collapses the low 16
    // bits of Int32WangNaiveHasher's XOR-fold to 0, so every such key probes from one home slot.
    private static int[] Adversarial(int count)
    {
        var keys = new int[count];
        for (int i = 0; i < count; i++)
        {
            int y = i + 1;
            keys[i] = (y << 16) | y;
        }

        return keys;
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_ThrowsArgumentNullException_WhenKeysNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => ProbeStatisticsEvaluator.Evaluate<int, IdentityIntHasher>(null!));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-1024)]
    public void Evaluate_ThrowsArgumentOutOfRange_WhenCapacityNegative(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProbeStatisticsEvaluator.Evaluate<int, IdentityIntHasher>(Range(4), capacity));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-0.5f)]
    [InlineData(1.5f)]
    [InlineData(float.NaN)]
    public void Evaluate_ThrowsArgumentOutOfRange_WhenLoadFactorOutOfRange(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ProbeStatisticsEvaluator.Evaluate<int, IdentityIntHasher>(Range(4), loadFactor: loadFactor));
    }

    [Fact]
    public void Evaluate_NullCheckBeatsBadLoadFactor()
    {
        // A null source must surface as ArgumentNullException even when loadFactor is also invalid.
        Assert.Throws<ArgumentNullException>(
            () => ProbeStatisticsEvaluator.Evaluate<int, IdentityIntHasher>(null!, loadFactor: 5f));
    }

    // ── Table sizing ────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_SizesTableForKeyCountWithLoadFactorHeadroom()
    {
        // 100 keys at 0.75 → ceil(100 / 0.75) = 134 → NextPowerOfTwo(134) = 256.
        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<int, Int32Murmur3Hasher>(Range(100));

        Assert.Equal(256, stats.TableSize);
        // Achieved load is the entry count over that table size.
        Assert.Equal(100.0 / 256.0, stats.LoadFactor, 6);
    }

    [Fact]
    public void Evaluate_TableSizeIsPowerOfTwo()
    {
        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<int, Int32Murmur3Hasher>(Range(1000));

        Assert.True((stats.TableSize & (stats.TableSize - 1)) == 0,
            $"Table size {stats.TableSize} should be a power of two.");
    }

    [Fact]
    public void Evaluate_CapacityFloorsTableSize()
    {
        // A small key set with a large explicit capacity sizes the table from the capacity.
        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<int, Int32Murmur3Hasher>(Range(4), capacity: 4096);

        Assert.Equal(4096, stats.TableSize);
    }

    [Fact]
    public void Evaluate_LowerLoadFactorYieldsLargerTableAndFewerCollisions()
    {
        // 1000 keys: at 0.9 → ceil(1112) → 2048; at 0.3 → ceil(3334) → 4096. Distinct sizes, so
        // the sparser table achieves a lower fill and therefore shorter probe chains.
        int[] keys = Range(1000, start: 1);

        ProbeStatistics dense = ProbeStatisticsEvaluator.Evaluate<int, Int32WangNaiveHasher>(keys, loadFactor: 0.9f);
        ProbeStatistics sparse = ProbeStatisticsEvaluator.Evaluate<int, Int32WangNaiveHasher>(keys, loadFactor: 0.3f);

        Assert.True(sparse.TableSize > dense.TableSize,
            $"Sparse table ({sparse.TableSize}) should be larger than dense ({dense.TableSize}).");
        Assert.True(sparse.LoadFactor < dense.LoadFactor);
        Assert.True(sparse.AverageProbeLength <= dense.AverageProbeLength);
    }

    // ── Empty / single sample ───────────────────────────────────────────────────

    [Fact]
    public void Evaluate_EmptySample_ReturnsNeutralReport()
    {
        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<int, IdentityIntHasher>(Array.Empty<int>());

        Assert.Equal(0, stats.KeyCount);
        Assert.Equal(0, stats.EntryCount);
        Assert.Equal(0, stats.DuplicateKeyCount);
        Assert.Equal(0, stats.CollisionCount);
        Assert.Equal(0.0, stats.CollisionRate);
        Assert.Equal(0L, stats.TotalProbeLength);
        Assert.Equal(0.0, stats.AverageProbeLength);
        Assert.Equal(0, stats.MaxProbeLength);
        Assert.Equal(0.0, stats.LoadFactor);
    }

    [Fact]
    public void Evaluate_SingleKey_ReportsSingleProbe()
    {
        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<int, IdentityIntHasher>(new[] { 99 });

        Assert.Equal(1, stats.KeyCount);
        Assert.Equal(1, stats.EntryCount);
        Assert.Equal(0, stats.CollisionCount);
        Assert.Equal(0.0, stats.CollisionRate);
        Assert.Equal(1L, stats.TotalProbeLength);
        Assert.Equal(1.0, stats.AverageProbeLength);
        Assert.Equal(1, stats.MaxProbeLength);
    }

    // ── Probe-length accounting ─────────────────────────────────────────────────

    [Fact]
    public void Evaluate_IdentityHasher_OnContiguousKeys_IsCollisionFree()
    {
        // Identity over a contiguous range maps each key to its own slot in a power-of-two table.
        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<int, IdentityIntHasher>(Range(256));

        Assert.Equal(0, stats.CollisionCount);
        Assert.Equal(0.0, stats.CollisionRate);
        Assert.Equal(1.0, stats.AverageProbeLength);
        Assert.Equal(1, stats.MaxProbeLength);
        Assert.Equal(256L, stats.TotalProbeLength);
    }

    [Fact]
    public void Evaluate_ConstantHasher_ProducesTriangularProbeChain()
    {
        // Every key hashes to the same slot, so the i-th insert probes i slots: lengths 1..n.
        const int n = 100;
        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<int, ConstantIntHasher>(Range(n, start: 1));

        Assert.Equal(n, stats.EntryCount);
        Assert.Equal(n - 1, stats.CollisionCount);                 // every entry after the first collides
        Assert.Equal(n, stats.MaxProbeLength);                     // the last insert walks the whole chain
        Assert.Equal((long)n * (n + 1) / 2, stats.TotalProbeLength); // 1 + 2 + ... + n
        Assert.Equal((n + 1) / 2.0, stats.AverageProbeLength, 6);
    }

    // ── Duplicate handling ──────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_DuplicateKeys_AreDeduplicated()
    {
        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<int, IdentityIntHasher>(new[] { 5, 5, 5, 6 });

        Assert.Equal(4, stats.KeyCount);
        Assert.Equal(2, stats.EntryCount);          // 5 and 6
        Assert.Equal(2, stats.DuplicateKeyCount);   // the two repeats of 5
    }

    // ── Quality comparison — the honest end-to-end story ─────────────────────────

    [Fact]
    public void Evaluate_StrongHasher_BreaksAdversarialChainThatNaiveHasherDoesNot()
    {
        int[] keys = Adversarial(5000);

        ProbeStatistics naive = ProbeStatisticsEvaluator.Evaluate<int, Int32WangNaiveHasher>(keys);
        ProbeStatistics murmur = ProbeStatisticsEvaluator.Evaluate<int, Int32Murmur3Hasher>(keys);

        // The naive XOR-fold collapses these keys into a few home slots, so probe chains blow up.
        Assert.True(naive.MaxProbeLength > 100,
            $"Naive hasher should degrade badly on adversarial keys (max probe was {naive.MaxProbeLength}).");
        // Murmur3 avalanches the same keys back to a near-uniform spread.
        Assert.True(murmur.AverageProbeLength < 2.0,
            $"Murmur3 should keep the average probe near 1 (was {murmur.AverageProbeLength}).");
        Assert.True(naive.AverageProbeLength > murmur.AverageProbeLength * 5,
            $"Naive avg probe ({naive.AverageProbeLength}) should dwarf Murmur3's ({murmur.AverageProbeLength}).");
    }

    [Fact]
    public void Evaluate_GoodHasher_OnUniformKeys_KeepsProbesShort()
    {
        // Random distinct keys are the case where even the cheap hashers do well.
        var rand = new Random(42);
        var set = new HashSet<int>();
        while (set.Count < 2000)
        {
            set.Add(rand.Next(1, int.MaxValue));
        }

        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<int, Int32WangNaiveHasher>(set.ToArray());

        Assert.InRange(stats.AverageProbeLength, 1.0, 2.0);
    }

    // ── Determinism ─────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_IsDeterministic_ForSameInput()
    {
        int[] keys = Range(300, start: 1);

        ProbeStatistics a = ProbeStatisticsEvaluator.Evaluate<int, Int32WangNaiveHasher>(keys);
        ProbeStatistics b = ProbeStatisticsEvaluator.Evaluate<int, Int32WangNaiveHasher>(keys);

        Assert.Equal(a.TableSize, b.TableSize);
        Assert.Equal(a.EntryCount, b.EntryCount);
        Assert.Equal(a.TotalProbeLength, b.TotalProbeLength);
        Assert.Equal(a.MaxProbeLength, b.MaxProbeLength);
        Assert.Equal(a.AverageProbeLength, b.AverageProbeLength);
    }

    // ── Generic over key type ────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_WorksWithStringHashers()
    {
        var keys = new[] { "alpha", "beta", "gamma", "delta", "epsilon" };

        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<string, StringMurmur3Hasher>(keys);

        Assert.Equal(5, stats.KeyCount);
        Assert.Equal(5, stats.EntryCount);
        Assert.True(stats.AverageProbeLength >= 1.0);
    }

    [Fact]
    public void Evaluate_WorksWithLongHashers()
    {
        var keys = new long[] { 1, 2, 3, 1L << 40, (1L << 40) | 1 };

        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<long, Int64WangNaiveHasher>(keys);

        Assert.Equal(5, stats.EntryCount);
        Assert.True(stats.MaxProbeLength >= 1);
    }

    // ── ToString ────────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_IncludesKeyMetrics()
    {
        ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<int, Int32Murmur3Hasher>(Range(10, start: 1));

        string text = stats.ToString();

        Assert.Contains("Entries=", text);
        Assert.Contains("AvgProbe=", text);
        Assert.Contains("MaxProbe=", text);
    }
}
