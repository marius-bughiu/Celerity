using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that every probabilistic sketch routes through <see cref="IHashProvider64{T}"/> when
/// its hasher provides one, and keeps the widened 32-bit path — including the out-of-band
/// <c>null</c> handling — when it does not.
/// </summary>
/// <remarks>
/// <para>
/// The sketches never store their elements, so a hash collision is indistinguishable from a
/// genuine hit forever. A 32-bit hasher widened into 64 bits reaches only 2^32 values, which
/// puts a floor under every sketch's error budget that no amount of memory can lift — the
/// defect this interface exists to remove.
/// </para>
/// <para>
/// The dispatch itself is internal and compiled away per instantiation, so it cannot be
/// asserted directly. Instead these tests use <see cref="DisagreeingHasher"/>, whose 32-bit
/// and 64-bit surfaces deliberately disagree: <c>Hash</c> is a constant, so a sketch still on
/// the 32-bit path collapses every element onto one slot, while <c>Hash64</c> separates them.
/// Any sketch that fails to take the 64-bit path shows up as a total loss of resolution.
/// </para>
/// </remarks>
public class SketchHashProvider64Tests
{
    /// <summary>
    /// A hasher whose two surfaces disagree: <c>Hash</c> is constant, <c>Hash64</c> is a
    /// bijective mix. Only useful as a probe — it is deliberately a broken 32-bit hasher.
    /// </summary>
    private struct DisagreeingHasher : IHashProvider<long>, IHashProvider64<long>
    {
        public int Hash(long key) => 0;

        public ulong Hash64(long key)
        {
            ulong x = (ulong)key;
            x ^= x >> 33;
            x *= 0xff51afd7ed558ccdUL;
            x ^= x >> 33;
            x *= 0xc4ceb9fe1a85ec53UL;
            x ^= x >> 33;
            return x;
        }
    }

    /// <summary>The same mix, published only as a 32-bit fold — the reduced-entropy path.</summary>
    private struct Fold32Hasher : IHashProvider<long>
    {
        public int Hash(long key)
        {
            ulong x = default(DisagreeingHasher).Hash64(key);
            return unchecked((int)(x ^ (x >> 32)));
        }
    }

    private const int N = 4_000;

    // ── Every sketch takes the 64-bit path when one is available ─────────────

    [Fact]
    public void HyperLogLog_UsesHash64_WhenTheHasherProvidesOne()
    {
        var hll = new HyperLogLog<long, DisagreeingHasher>();
        for (long i = 0; i < N; i++)
            hll.Add(i);

        // On the 32-bit path every element would hash to the same constant and land in one
        // register, estimating a cardinality of roughly 1.
        long estimate = hll.EstimateCardinality();
        Assert.InRange(estimate, N * 0.9, N * 1.1);
    }

    [Fact]
    public void BloomFilter_UsesHash64_WhenTheHasherProvidesOne()
    {
        var filter = new BloomFilter<long, DisagreeingHasher>(N, 0.01);
        for (long i = 0; i < N; i++)
            filter.Add(i);

        // A constant hash sets the same k bits for every element, so *every* absent element
        // would come back a false positive. With the 64-bit path the rate stays near 1%.
        int falsePositives = 0;
        for (long i = N; i < N * 2; i++)
        {
            if (filter.Contains(i))
                falsePositives++;
        }

        Assert.True(falsePositives < N / 10,
            $"{falsePositives} false positives out of {N} — the filter looks blind to the element value.");
    }

    [Fact]
    public void CuckooFilter_UsesHash64_WhenTheHasherProvidesOne()
    {
        var filter = new CuckooFilter<long, DisagreeingHasher>(N, 0.01);
        for (long i = 0; i < N; i++)
            Assert.True(filter.TryAdd(i), $"insert of {i} failed");

        int falsePositives = 0;
        for (long i = N; i < N * 2; i++)
        {
            if (filter.Contains(i))
                falsePositives++;
        }

        Assert.True(falsePositives < N / 10,
            $"{falsePositives} false positives out of {N} — the filter looks blind to the element value.");
    }

    [Fact]
    public void XorFilter_UsesHash64_WhenTheHasherProvidesOne()
    {
        long[] elements = new long[N];
        for (int i = 0; i < N; i++)
            elements[i] = i;

        // A constant hash makes every element share one key, so the peeling construction
        // could not even separate them; reaching a built filter at all already depends on
        // the 64-bit path.
        var filter = new XorFilter<long, DisagreeingHasher>(elements);

        foreach (long e in elements)
            Assert.True(filter.Contains(e), $"false negative for {e}");

        int falsePositives = 0;
        for (long i = N; i < N * 2; i++)
        {
            if (filter.Contains(i))
                falsePositives++;
        }

        Assert.True(falsePositives < N / 10,
            $"{falsePositives} false positives out of {N} — the filter looks blind to the element value.");
    }

    [Fact]
    public void CountMinSketch_UsesHash64_WhenTheHasherProvidesOne()
    {
        var sketch = new CountMinSketch<long, DisagreeingHasher>(0.001, 0.01);
        for (long i = 0; i < N; i++)
            sketch.Add(i);

        // A constant hash pools every element into the same cell of every row, so each
        // element's estimate would be the whole stream. The 64-bit path keeps them apart.
        for (long i = 0; i < N; i += 97)
            Assert.InRange(sketch.EstimateCount(i), 1L, 10L);
    }

    // ── The 32-bit path still works, and is what a 32-bit-only hasher gets ────

    [Fact]
    public void Sketches_StillWork_WithA32BitOnlyHasher()
    {
        var hll = new HyperLogLog<long, Fold32Hasher>();
        var bloom = new BloomFilter<long, Fold32Hasher>(N, 0.01);
        var cuckoo = new CuckooFilter<long, Fold32Hasher>(N, 0.01);
        var cms = new CountMinSketch<long, Fold32Hasher>(0.001, 0.01);

        for (long i = 0; i < N; i++)
        {
            hll.Add(i);
            bloom.Add(i);
            cuckoo.Add(i);
            cms.Add(i);
        }

        Assert.InRange(hll.EstimateCardinality(), N * 0.9, N * 1.1);
        for (long i = 0; i < N; i++)
        {
            Assert.True(bloom.Contains(i));
            Assert.True(cuckoo.Contains(i));
            Assert.True(cms.EstimateCount(i) >= 1);
        }
    }

    // ── Reference-type elements: null still bypasses the hasher ──────────────

    [Fact]
    public void Sketches_WithA64BitStringHasher_HandleNullWithoutCallingTheHasher()
    {
        // The string hashers throw on null; the sketches map null to a fixed hash instead of
        // calling them. That has to hold on the 64-bit path too.
        var hll = new HyperLogLog<string, StringXxHash64Hasher>();
        var bloom = new BloomFilter<string, StringXxHash64Hasher>(16, 0.01);
        var cuckoo = new CuckooFilter<string, StringXxHash64Hasher>(16, 0.01);
        var cms = new CountMinSketch<string, StringXxHash64Hasher>(0.01, 0.01);

        hll.Add(null!);
        bloom.Add(null!);
        Assert.True(cuckoo.TryAdd(null!));
        cms.Add(null!);

        Assert.Equal(1, hll.EstimateCardinality());
        Assert.True(bloom.Contains(null!));
        Assert.True(cuckoo.Contains(null!));
        Assert.True(cms.EstimateCount(null!) >= 1);

        var xor = new XorFilter<string, StringXxHash64Hasher>(["a", null!, "c"]);
        Assert.True(xor.Contains(null!));
        Assert.True(xor.Contains("a"));
        Assert.True(xor.Contains("c"));
    }

    [Fact]
    public void Sketches_WithA64BitStringHasher_SeparateDistinctStrings()
    {
        var hll = new HyperLogLog<string, StringXxHash64Hasher>();
        var bloom = new BloomFilter<string, StringMetroHash64Hasher>(N, 0.01);
        var cms = new CountMinSketch<string, StringCityHash64Hasher>(0.001, 0.01);

        for (int i = 0; i < N; i++)
        {
            hll.Add($"element-{i}");
            bloom.Add($"element-{i}");
            cms.Add($"element-{i}");
        }

        Assert.InRange(hll.EstimateCardinality(), N * 0.9, N * 1.1);
        for (int i = 0; i < N; i++)
        {
            Assert.True(bloom.Contains($"element-{i}"));
            Assert.InRange(cms.EstimateCount($"element-{i}"), 1L, 10L);
        }
    }
}
