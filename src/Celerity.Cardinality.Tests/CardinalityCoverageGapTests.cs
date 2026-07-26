namespace Celerity.Cardinality.Tests;

/// <summary>
/// Pins the corners of the <c>Celerity.Cardinality</c> public surface that the behaviour-driven suites in
/// <c>DedupFilterTests</c> and <c>DistinctTests</c> never reach, so they cannot silently regress.
/// </summary>
/// <remarks>
/// <para>
/// Three things are covered here. First, the sizing metadata — <see cref="DedupFilter{TKey, THasher}.Capacity"/>,
/// <see cref="DedupFilter{TKey, THasher}.FalsePositiveRate"/>, <see cref="Distinct{TKey, THasher}.Precision"/> and
/// <see cref="Distinct{TKey, THasher}.ExactThreshold"/>. These are what a caller reads back to reason about the
/// structure it was handed (log it, size a sibling shard to match, decide whether a merge is legal), so each must
/// report the value it was <em>constructed</em> with, not something the implementation rounded on the way in: the
/// cuckoo table behind the dedup filter rounds its bucket count up to a power of two, and the assertions below pin
/// that this rounding is invisible in the reported capacity.
/// </para>
/// <para>
/// Second, the saturation path of <see cref="DedupFilter{TKey, THasher}.TryMarkSeen"/>. A cuckoo filter loaded far
/// past its sizing eventually exhausts an insertion's eviction budget, and the dedup wrapper turns that into an
/// <see cref="InvalidOperationException"/> rather than silently dropping a key or reporting a new key as a
/// duplicate — the latter would be a false negative for the caller's dedup. The failure is loud <em>and</em>
/// recoverable, which is what makes the sliding-window usage safe, so both halves are pinned.
/// </para>
/// <para>
/// Third, the exact-to-HyperLogLog mode transition in <see cref="Distinct{TKey, THasher}"/>: the threshold
/// boundary itself, promotion triggered from inside <see cref="Distinct{TKey, THasher}.Merge"/> when the incoming
/// estimator has already promoted, and <see cref="Distinct{TKey, THasher}.Clear"/> from <em>both</em> modes. Clear
/// takes a different code path depending on whether the exact set is still alive or was released at promotion, and
/// in both cases the estimator has to come back empty, exact, and genuinely reusable — with none of the pre-clear
/// keys still counted.
/// </para>
/// </remarks>
public class CardinalityCoverageGapTests
{
    // The dedup filter is sized for a single live key, which is the smallest the API allows, so its cuckoo table
    // holds one bucket of four fingerprint slots. A handful of distinct keys therefore drives it to saturation.
    private const int SaturationKeyLimit = 512;

    /// <summary>
    /// Marks distinct keys until the filter reports itself full, recording the keys it accepted.
    /// </summary>
    /// <returns>The exception thrown by the saturating call, or <c>null</c> if the filter never saturated.</returns>
    private static InvalidOperationException? MarkUntilFull(DedupFilter<string, StringXxHash3Hasher> dedup, List<string> marked)
    {
        for (int i = 0; i < SaturationKeyLimit; i++)
        {
            string key = $"saturate-{i}";
            try
            {
                if (dedup.TryMarkSeen(key))
                    marked.Add(key);
            }
            catch (InvalidOperationException ex)
            {
                return ex;
            }
        }

        return null;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(12_345)]
    public void Capacity_ShouldReturnTheRequestedLiveKeyCount_WhenTheCountIsNotAPowerOfTwo(int expectedItems)
    {
        var dedup = new StringDedupFilter(expectedItems);

        // The backing cuckoo table rounds its bucket count up to a power of two; the reported capacity is the
        // caller's own sizing argument and must not be rounded with it.
        Assert.Equal(expectedItems, dedup.Capacity);
    }

    [Fact]
    public void FalsePositiveRate_ShouldReturnTheRequestedRate_WhenGivenExplicitly()
    {
        var dedup = new StringDedupFilter(1000, 0.001);

        Assert.Equal(0.001, dedup.FalsePositiveRate);
        Assert.Equal(1000, dedup.Capacity);
    }

    [Fact]
    public void FalsePositiveRate_ShouldReturnOnePercent_WhenNotSpecified()
    {
        var dedup = new StringDedupFilter(1000);

        Assert.Equal(StringDedupFilter.DefaultFalsePositiveRate, dedup.FalsePositiveRate);
        Assert.Equal(0.01, dedup.FalsePositiveRate);
    }

    [Fact]
    public void TryMarkSeen_ShouldThrowInvalidOperationException_WhenTheFilterIsFull()
    {
        // A narrow false-positive rate widens the fingerprint to its 16-bit ceiling, so a genuinely new key is not
        // mistaken for a duplicate on the way to saturation.
        var dedup = new StringDedupFilter(expectedItems: 1, falsePositiveRate: 0.0001);
        var marked = new List<string>();

        InvalidOperationException? thrown = MarkUntilFull(dedup, marked);

        Assert.True(thrown is not null,
            $"the filter never reported itself full after {SaturationKeyLimit} distinct keys (Count={dedup.Count}, IsFull={dedup.IsFull})");
        Assert.Contains("dedup filter is full", thrown!.Message);
        Assert.True(dedup.IsFull, "the filter should report IsFull once an insertion has exhausted its eviction budget");

        // Nothing already marked was lost when the insertion failed — the whole point of throwing rather than
        // reporting the new key as a duplicate.
        foreach (string key in marked)
            Assert.True(dedup.Contains(key), $"key '{key}' was marked before saturation but is no longer reported");
    }

    [Fact]
    public void Remove_ShouldMakeTheFilterUsableAgain_WhenItHadSaturated()
    {
        var dedup = new StringDedupFilter(expectedItems: 1, falsePositiveRate: 0.0001);
        var marked = new List<string>();

        InvalidOperationException? thrown = MarkUntilFull(dedup, marked);

        Assert.True(thrown is not null, $"the filter never reported itself full after {SaturationKeyLimit} distinct keys");
        Assert.NotEmpty(marked);

        // The exception tells the caller to remove aged-out keys; doing so must actually clear the full state,
        // otherwise the sliding-window usage would be a dead end.
        string agedOut = marked[0];
        Assert.True(dedup.Remove(agedOut));
        Assert.False(dedup.IsFull);

        // Its fingerprint was the only copy in the table (TryMarkSeen refuses to store a colliding one), so the
        // aged-out key is now definitively unseen and can be marked fresh.
        Assert.True(dedup.TryMarkSeen(agedOut));
    }

    [Fact]
    public void Precision_ShouldReturnTheConstructedSizing_WhenGivenExplicitly()
    {
        var distinct = new StringDistinct(precision: 12, exactThreshold: 64);

        Assert.Equal(12, distinct.Precision);
        Assert.Equal(64, distinct.ExactThreshold);
    }

    [Fact]
    public void Precision_ShouldReturnTheDocumentedDefaults_WhenNoSizingIsGiven()
    {
        var distinct = new StringDistinct();

        Assert.Equal(StringDistinct.DefaultPrecision, distinct.Precision);
        Assert.Equal(StringDistinct.DefaultExactThreshold, distinct.ExactThreshold);
        Assert.Equal(14, distinct.Precision);
        Assert.Equal(2048, distinct.ExactThreshold);
    }

    [Fact]
    public void Precision_ShouldDriveTheStandardError_WhenTheEstimatorHasPromoted()
    {
        var distinct = new StringDistinct(precision: 12, exactThreshold: 16);
        for (int i = 0; i < 200; i++)
            distinct.Add($"k-{i}");

        Assert.False(distinct.IsExact);
        Assert.Equal(12, distinct.Precision);
        // 2^12 registers, so the reported error is 1.04 / sqrt(4096) — the promoted HyperLogLog really was built
        // at the precision the estimator reports.
        Assert.Equal(1.04 / Math.Sqrt(1 << 12), distinct.StandardError, 12);
    }

    [Fact]
    public void Add_ShouldPromoteOnlyPastTheThreshold_WhenTheDistinctCountReachesIt()
    {
        const int threshold = 100;
        var distinct = new StringDistinct(exactThreshold: threshold);
        for (int i = 0; i < threshold; i++)
            distinct.Add($"k-{i}");

        // At exactly the threshold the estimator is still exact, so the count is precise.
        Assert.True(distinct.IsExact);
        Assert.Equal(threshold, distinct.Count());
        Assert.Equal(0d, distinct.StandardError);

        distinct.Add($"k-{threshold}");

        Assert.False(distinct.IsExact);
        Assert.True(distinct.StandardError > 0d);

        // Promotion replays the exact set into the HyperLogLog, so the estimate still reflects every key added
        // before the switch — it does not restart from the key that triggered it.
        long estimate = distinct.Count();
        double relativeError = Math.Abs(estimate - (threshold + 1)) / (double)(threshold + 1);
        Assert.True(relativeError < 0.10, $"estimate {estimate} vs {threshold + 1} (error {relativeError:P1})");
    }

    [Fact]
    public void Merge_ShouldPromoteThisEstimator_WhenTheOtherHasAlreadyPromoted()
    {
        var estimated = new StringDistinct(exactThreshold: 100);
        for (int i = 0; i < 5_000; i++)
            estimated.Add($"shared-{i}");
        Assert.False(estimated.IsExact);
        long estimatedCountBefore = estimated.Count();

        var exact = new StringDistinct(exactThreshold: 100);
        for (int i = 0; i < 20; i++)
            exact.Add($"local-{i}");
        Assert.True(exact.IsExact);

        // Merging an estimate into an exact estimator forces this side to promote first — an exact set cannot
        // absorb HyperLogLog registers.
        exact.Merge(estimated);

        Assert.False(exact.IsExact);
        Assert.True(exact.StandardError > 0d);

        const int trueUnion = 5_020; // the two key prefixes do not overlap
        long estimate = exact.Count();
        double relativeError = Math.Abs(estimate - trueUnion) / (double)trueUnion;
        Assert.True(relativeError < 0.10, $"estimate {estimate} vs {trueUnion} (error {relativeError:P1})");

        // The merge source is documented as left unmodified.
        Assert.Equal(estimatedCountBefore, estimated.Count());
    }

    [Fact]
    public void Clear_ShouldEmptyAndStayReusable_WhenStillExact()
    {
        var distinct = new StringDistinct(exactThreshold: 100);
        distinct.Add("a");
        distinct.Add("b");
        distinct.Add("a");
        Assert.Equal(2, distinct.Count());

        distinct.Clear();

        Assert.Equal(0, distinct.Count());
        Assert.True(distinct.IsExact);
        Assert.Equal(0d, distinct.StandardError);

        // The set was cleared rather than left populated: re-adding one of the pre-clear keys counts it once, and
        // the estimator is still exact.
        distinct.Add("a");
        distinct.Add("c");
        distinct.Add("c");
        Assert.Equal(2, distinct.Count());
        Assert.True(distinct.IsExact);
    }

    [Fact]
    public void Clear_ShouldEmptyAndStayReusable_WhenAlreadyPromoted()
    {
        var distinct = new StringDistinct(exactThreshold: 50);
        for (int i = 0; i < 500; i++)
            distinct.Add($"k-{i}");
        Assert.False(distinct.IsExact);

        distinct.Clear();

        Assert.True(distinct.IsExact);
        Assert.Equal(0, distinct.Count());
        Assert.Equal(0d, distinct.StandardError);

        // Promotion released the exact set, so Clear has to allocate a fresh one; re-adding a subset of the
        // pre-clear keys must give a precise count of exactly those keys.
        for (int i = 0; i < 10; i++)
            distinct.Add($"k-{i}");

        Assert.True(distinct.IsExact);
        Assert.Equal(10, distinct.Count());
    }
}
