using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Pins the <em>degenerate-input</em> and <em>saturation</em> corners of the probabilistic
/// sketches — the arms of the internal clamps that only fire for parameter combinations no
/// ordinary caller reaches, and which therefore silently rot unless they are pinned.
///
/// <para>
/// Four distinct corners are covered, each one an arithmetic clamp guarding a value the rest of
/// the type assumes is well-formed:
/// </para>
/// <list type="number">
/// <item><description>
/// <b><see cref="BloomFilter{T,THasher}"/> hash-count floor.</b> The optimal hash count
/// <c>k = round((m/n)·ln 2)</c> rounds to <em>zero</em> whenever the requested false-positive
/// rate is loose enough that the sized bit array is smaller than <c>0.72·n</c> bits. A zero
/// <c>k</c> would make <c>Add</c> set no bits at all and <c>Contains</c> return <c>true</c> for
/// everything — a filter with a 100% false-positive rate and, worse, no observable failure. The
/// floor of one keeps the no-false-negatives contract meaningful, so the tests assert both the
/// clamped <c>HashCount</c> and that membership still round-trips.
/// </description></item>
/// <item><description>
/// <b><see cref="BloomFilter{T,THasher}"/> source-size floor.</b> Building a filter from an
/// <em>empty</em> sequence that is not an <see cref="ICollection{T}"/> counts zero elements, but
/// the primary constructor demands a strictly positive expected count. The floor of one keeps the
/// empty-source case a valid, usable filter rather than an
/// <see cref="ArgumentOutOfRangeException"/> leaking out of a constructor the caller never
/// invoked directly. The non-collection path matters: <see cref="ICollection{T}"/> sources take a
/// different branch, so the test deliberately uses an iterator method.
/// </description></item>
/// <item><description>
/// <b><see cref="HyperLogLog{T,THasher}"/> tabulated bias constants.</b> The harmonic-mean
/// estimator's bias constant <c>alpha_m</c> uses hand-tabulated values from the original paper for
/// the three smallest register counts and a closed-form approximation above them. The
/// <c>m = 32</c> and <c>m = 64</c> table entries are reachable only at precisions 5 and 6 — two
/// values inside the supported range that no default-precision test touches.
/// </description></item>
/// <item><description>
/// <b><see cref="TopKSketch{T,THasher}"/> counter saturation.</b> Monitor counts and
/// <see cref="TopKSketch{T,THasher}.TotalCount"/> accumulate unchecked, so a weighted
/// <c>Add</c> large enough to carry past <see cref="long.MaxValue"/> would wrap negative. A
/// negative count is not merely wrong, it inverts the Space-Saving guarantee (the sketch would
/// under-estimate, and the min-heap would treat the wrapped monitor as the next eviction victim).
/// The saturating add clamps at <see cref="long.MaxValue"/> instead, on both the repeat-observation
/// and the eviction path.
/// </description></item>
/// </list>
///
/// <para>
/// Finally, <see cref="TopKEntry{T}.ToString"/> — a diagnostic surface that is easy to break
/// without any test noticing — has its exact format pinned for both an exact entry (zero error)
/// and an entry that inherited an evicted monitor's count as its error floor.
/// </para>
/// </summary>
public class SketchDegenerateParameterTests
{
    // ---------------------------------------------------------------------------------------
    // BloomFilter: the k < 1 hash-count clamp.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// A false-positive rate loose enough that the optimal hash count rounds to zero must still
    /// produce a filter that hashes each element at least once.
    /// </summary>
    /// <remarks>
    /// Arithmetic verified by hand for each row (Ln2 = 0.6931471805599453,
    /// Ln2Squared = 0.4804530139182014):
    /// <para>
    /// n = 1000, p = 0.9: mOptimal = -(1000 · ln 0.9) / (ln 2)² = 105.3605 / 0.4804530 = 219.29,
    /// ceiling 220, next power of two 256 (&gt;= the 64-bit minimum, so no further clamp).
    /// k = round((256 / 1000) · ln 2) = round(0.2560 · 0.6931472) = round(0.17744) = 0 -&gt; clamped to 1.
    /// </para>
    /// <para>
    /// n = 10000, p = 0.95: mOptimal = -(10000 · ln 0.95) / (ln 2)² = 512.933 / 0.4804530 = 1067.6,
    /// ceiling 1068, next power of two 2048.
    /// k = round((2048 / 10000) · ln 2) = round(0.2048 · 0.6931472) = round(0.14196) = 0 -&gt; clamped to 1.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(1000, 0.9, 256)]
    [InlineData(10000, 0.95, 2048)]
    public void Constructor_ShouldClampHashCountToOne_WhenOptimalKRoundsToZero(
        int expectedItems, double falsePositiveRate, int expectedBitCount)
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(expectedItems, falsePositiveRate);

        Assert.Equal(1, filter.HashCount);
        Assert.Equal(expectedBitCount, filter.BitCount);
        Assert.Equal(expectedItems, filter.Capacity);
        Assert.Equal(falsePositiveRate, filter.FalsePositiveRate);

        // A zero hash count would leave every bit clear, so the real proof the clamp did its job
        // is that the filter still stores membership: no false negatives for anything added.
        for (int i = 0; i < 200; i++)
            filter.Add(i * 31 + 7);

        for (int i = 0; i < 200; i++)
            Assert.True(filter.Contains(i * 31 + 7), $"false negative for {i * 31 + 7}");

        Assert.Equal(200, filter.Count);
    }

    /// <summary>
    /// With the hash count clamped to one, an empty filter must still answer <c>false</c> for
    /// unseen elements — i.e. the clamp produced a real one-probe filter, not a table of set bits
    /// that answers <c>true</c> for everything.
    /// </summary>
    [Fact]
    public void Contains_ShouldReturnFalse_WhenHashCountIsClampedAndFilterIsEmpty()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(1000, 0.9);
        Assert.Equal(1, filter.HashCount);

        int negatives = 0;
        for (int i = 0; i < 100; i++)
        {
            if (!filter.Contains(i))
                negatives++;
        }

        Assert.Equal(100, negatives);
    }

    // ---------------------------------------------------------------------------------------
    // BloomFilter: the expected-item-count floor on the non-ICollection source path.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// An empty sequence that is not an <see cref="ICollection{T}"/> counts zero elements, which
    /// the primary constructor would reject; the floor of one must turn it into a minimal but
    /// fully working filter.
    /// </summary>
    /// <remarks>
    /// With the floored n = 1 and the default p = 0.01:
    /// mOptimal = -(1 · ln 0.01) / (ln 2)² = 4.60517 / 0.4804530 = 9.585, ceiling 10, next power of
    /// two 16 — below the one-word minimum, so the bit count is raised to 64. The hash count then
    /// comes out as round((64 / 1) · ln 2) = round(44.361) = 44, well clear of the clamp.
    /// </remarks>
    [Fact]
    public void Constructor_ShouldFloorExpectedItemsToOne_WhenSourceIsEmptyAndNotACollection()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(EmptySequence());

        Assert.Equal(1, filter.Capacity);
        Assert.Equal(0, filter.Count);
        Assert.Equal(64, filter.BitCount);   // never a zero-sized table
        Assert.Equal(44, filter.HashCount);

        // The floored filter is a usable filter, not a husk.
        filter.Add(4242);
        Assert.True(filter.Contains(4242));
        Assert.Equal(1, filter.Count);
    }

    /// <summary>
    /// The counting pass on the non-<see cref="ICollection{T}"/> path must size the filter from the
    /// number of elements it actually walked, and must then re-enumerate the source to add them —
    /// the complementary arm of the same floor.
    /// </summary>
    [Fact]
    public void Constructor_ShouldSizeFromCountedElements_WhenSourceIsNotACollection()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(ThreeElementSequence());

        Assert.Equal(3, filter.Capacity);
        Assert.Equal(3, filter.Count);
        Assert.True(filter.Contains(11));
        Assert.True(filter.Contains(22));
        Assert.True(filter.Contains(33));
    }

    // A hand-written iterator: unlike Array.Empty<int>() or Enumerable.Empty<int>() it does not
    // implement ICollection<int>, so it forces the counting-pass branch of the source sizing.
    private static IEnumerable<int> EmptySequence()
    {
        yield break;
    }

    private static IEnumerable<int> ThreeElementSequence()
    {
        yield return 11;
        yield return 22;
        yield return 33;
    }

    // ---------------------------------------------------------------------------------------
    // HyperLogLog: the tabulated alpha constants for m = 32 and m = 64.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// The two smallest precisions above the minimum select the paper's tabulated bias constants
    /// (<c>alpha_32 = 0.697</c>, <c>alpha_64 = 0.709</c>) rather than the closed-form
    /// approximation. An empty estimator exercises that path deterministically and must still
    /// report exactly zero: the raw harmonic-mean estimate of an all-zero register array is
    /// <c>alpha_m · m</c> (22.3 at m = 32, 45.4 at m = 64), well inside the small-range threshold
    /// of <c>2.5 · m</c>, so linear counting takes over and yields <c>m · ln(m/m) = 0</c>.
    /// </summary>
    [Theory]
    [InlineData(5, 32)]
    [InlineData(6, 64)]
    public void EstimateCardinality_ShouldReturnZero_WhenTinyPrecisionEstimatorIsEmpty(
        int precision, int expectedRegisterCount)
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(precision);

        Assert.Equal(precision, hll.Precision);
        Assert.Equal(expectedRegisterCount, hll.RegisterCount);
        Assert.Equal(0, hll.EstimateCardinality());
    }

    /// <summary>
    /// A small distinct stream at precision 5 (<c>m = 32</c>) and precision 6 (<c>m = 64</c>) must
    /// still estimate its cardinality within the estimator's own — very wide — error budget. These
    /// precisions carry a 18.4% and 13.0% relative standard error respectively, so the bound is
    /// loose by design; it is still a real bound, and it is what catches a wrong bias constant or
    /// a mis-shifted register index (either of which collapses the estimate toward zero or blows it
    /// far past the truth).
    /// </summary>
    [Theory]
    [InlineData(5, 16)]
    [InlineData(6, 32)]
    public void EstimateCardinality_ShouldStayWithinErrorBound_WhenPrecisionSelectsATabulatedAlpha(
        int precision, int n)
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(precision);
        for (int i = 0; i < n; i++)
            hll.Add(i);

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - n) / (double)n;

        // Same shape as HyperLogLogAccuracyTests: a small multiple of the theoretical standard
        // error plus absolute slack for the small-range regime.
        double bound = hll.StandardError * 3 + 0.1;
        Assert.True(relativeError <= bound,
            $"precision={precision}, n={n}: estimate {estimate} had relative error {relativeError:F4} > bound {bound:F4}");
    }

    /// <summary>
    /// Adding a duplicate-heavy stream at a tabulated-alpha precision must not push the estimate
    /// past the distinct count — the registers record maxima, so repeats are idempotent regardless
    /// of how small the register array is.
    /// </summary>
    [Fact]
    public void EstimateCardinality_ShouldIgnoreDuplicates_WhenPrecisionIsSix()
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(6);
        for (int i = 0; i < 32; i++)
            hll.Add(i);

        long afterDistinct = hll.EstimateCardinality();

        for (int repeat = 0; repeat < 50; repeat++)
        {
            for (int i = 0; i < 32; i++)
                hll.Add(i);
        }

        Assert.Equal(afterDistinct, hll.EstimateCardinality());
    }

    // ---------------------------------------------------------------------------------------
    // TopKSketch: saturating counter arithmetic.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Two weighted adds of <see cref="long.MaxValue"/> for the same element overflow both the
    /// monitor counter and the running total: unchecked,
    /// <c>long.MaxValue + long.MaxValue == -2</c>. Both must clamp at <see cref="long.MaxValue"/>
    /// rather than wrapping negative, so the never-underestimate guarantee survives.
    /// </summary>
    [Fact]
    public void Add_ShouldSaturateMonitorCount_WhenRepeatObservationOverflows()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(4);

        sketch.Add(1, long.MaxValue);           // fresh monitor: count == long.MaxValue exactly
        Assert.Equal(long.MaxValue, sketch.TotalCount);

        sketch.Add(1, long.MaxValue);           // repeat observation: wraps to -2, clamps back

        Assert.Equal(long.MaxValue, sketch.TotalCount);
        Assert.True(sketch.TryGetCount(1, out long count, out long error));
        Assert.Equal(long.MaxValue, count);
        Assert.Equal(0, error);                 // never shared a monitor -> still exact-bounded
        Assert.Equal(1, sketch.Count);
    }

    /// <summary>
    /// The eviction path adds the evicted minimum to the newcomer's weight, which overflows just
    /// as readily. The newcomer must end up with a saturated count and the evicted count as its
    /// error, and the evicted element must be gone.
    /// </summary>
    [Fact]
    public void Add_ShouldSaturateMonitorCount_WhenEvictionInheritsAnOverflowingMinimum()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(1);

        sketch.Add(1, long.MaxValue);
        sketch.Add(2, long.MaxValue);           // evicts 1: MaxValue + MaxValue wraps to -2

        Assert.False(sketch.TryGetCount(1, out _, out _));
        Assert.True(sketch.TryGetCount(2, out long count, out long error));
        Assert.Equal(long.MaxValue, count);
        Assert.Equal(long.MaxValue, error);     // inherited the evicted monitor's count
        Assert.Equal(long.MaxValue, sketch.TotalCount);
        Assert.Equal(1, sketch.Count);
    }

    /// <summary>
    /// A saturated monitor must still rank first in a top-k query — the clamp keeps the count
    /// positive, so the descending sort is unaffected. A wrapped negative count would have sunk it
    /// to the bottom.
    /// </summary>
    [Fact]
    public void GetTopK_ShouldRankSaturatedMonitorFirst_WhenItsCountOverflowed()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(4);
        sketch.Add(9, long.MaxValue);
        sketch.Add(9, long.MaxValue);
        sketch.Add(8, 1_000);

        TopKEntry<int>[] top = sketch.GetTopK();

        Assert.Equal(2, top.Length);
        Assert.Equal(9, top[0].Element);
        Assert.Equal(long.MaxValue, top[0].Count);
        Assert.Equal(8, top[1].Element);
        Assert.Equal(1_000, top[1].Count);
    }

    // ---------------------------------------------------------------------------------------
    // TopKEntry.ToString
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// The diagnostic string of an entry that has never shared a monitor: error zero, so the count
    /// is exact.
    /// </summary>
    [Fact]
    public void ToString_ShouldFormatElementCountAndError_WhenEntryIsExact()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(4);
        sketch.Add(7, 3);

        TopKEntry<int>[] top = sketch.GetTopK();

        Assert.Single(top);
        Assert.Equal("7 (3, err 0)", top[0].ToString());
    }

    /// <summary>
    /// The diagnostic string of an entry that took over an evicted monitor: with capacity 1,
    /// adding 2 evicts 1, so the newcomer inherits the evicted count 5 as its error and reports
    /// 5 + 3 = 8 occurrences.
    /// </summary>
    [Fact]
    public void ToString_ShouldFormatInheritedError_WhenEntryTookOverAnEvictedMonitor()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(1);
        sketch.Add(1, 5);
        sketch.Add(2, 3);

        TopKEntry<int>[] top = sketch.GetTopK();

        Assert.Single(top);
        Assert.Equal(8, top[0].Count);
        Assert.Equal(5, top[0].Error);
        Assert.Equal("2 (8, err 5)", top[0].ToString());
    }

    /// <summary>
    /// A directly constructed entry formats the same way, pinning the format independently of how
    /// the sketch happens to populate its monitors.
    /// </summary>
    [Fact]
    public void ToString_ShouldFormatDirectlyConstructedEntry()
    {
        var entry = new TopKEntry<string>("alpha", 42, 7);
        Assert.Equal("alpha (42, err 7)", entry.ToString());
    }
}
