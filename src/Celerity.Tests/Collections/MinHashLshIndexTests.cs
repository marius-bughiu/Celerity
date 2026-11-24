using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class MinHashLshIndexTests
{
    private static MinHashSketch<int, Int32Murmur3Hasher> Sketch(IEnumerable<int> items, int precision = 8)
        => new(items, precision);

    private static MinHashSketch<int, Int32Murmur3Hasher> Sketch(int start, int count, int precision = 8)
        => new(Enumerable.Range(start, count), precision);

    // ---- Construction ------------------------------------------------------------------------------

    [Fact]
    public void ThresholdConstructor_ShouldChooseTheOptimalParameters()
    {
        var index = new MinHashLshIndex<string, int, Int32Murmur3Hasher>(0.5);

        var (bands, rows) = MinHashLshIndex<string, int, Int32Murmur3Hasher>.GetOptimalParameters(0.5, 8);
        Assert.Equal(bands, index.BandCount);
        Assert.Equal(rows, index.RowsPerBand);
        Assert.Equal(8, index.Precision);
        Assert.Equal(0, index.Count);
        Assert.Same(EqualityComparer<string>.Default, index.KeyComparer);
    }

    [Fact]
    public void ExplicitConstructor_ShouldKeepTheGivenParameters()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(bandCount: 16, rowsPerBand: 4, precision: 6);

        Assert.Equal(16, index.BandCount);
        Assert.Equal(4, index.RowsPerBand);
        Assert.Equal(6, index.Precision);
    }

    [Fact]
    public void Constructor_ShouldUseTheGivenKeyComparer()
    {
        var index = new MinHashLshIndex<string, int, Int32Murmur3Hasher>(0.5, keyComparer: StringComparer.OrdinalIgnoreCase);
        index.Add("Doc", Sketch(0, 10));

        Assert.Same(StringComparer.OrdinalIgnoreCase, index.KeyComparer);
        Assert.True(index.ContainsKey("DOC"));
        Assert.False(index.TryAdd("doc", Sketch(0, 10)));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void ThresholdConstructor_ShouldThrow_WhenThresholdIsNotStrictlyBetweenZeroAndOne(double threshold)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MinHashLshIndex<int, int, Int32Murmur3Hasher>(threshold));
        Assert.Equal("threshold", ex.ParamName);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(17)]
    public void Constructors_ShouldThrow_WhenPrecisionIsOutOfRange(int precision)
    {
        Assert.Equal("precision", Assert.Throws<ArgumentOutOfRangeException>(
            () => new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5, precision)).ParamName);
        Assert.Equal("precision", Assert.Throws<ArgumentOutOfRangeException>(
            () => new MinHashLshIndex<int, int, Int32Murmur3Hasher>(1, 1, precision)).ParamName);
    }

    [Theory]
    [InlineData(0, 1, "bandCount")]
    [InlineData(-1, 1, "bandCount")]
    [InlineData(257, 1, "bandCount")]
    [InlineData(1, 0, "rowsPerBand")]
    [InlineData(1, 257, "rowsPerBand")]
    [InlineData(16, 17, "rowsPerBand")]
    public void ExplicitConstructor_ShouldThrow_WhenTheBandsDoNotFitTheRegisters(int bands, int rows, string param)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MinHashLshIndex<int, int, Int32Murmur3Hasher>(bands, rows, precision: 8));
        Assert.Equal(param, ex.ParamName);
    }

    [Theory]
    [InlineData(256, 1)]
    [InlineData(1, 256)]
    [InlineData(16, 16)]
    public void ExplicitConstructor_ShouldAccept_BandsThatExactlyFillTheRegisters(int bands, int rows)
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(bands, rows);
        Assert.Equal(bands * rows, index.BandCount * index.RowsPerBand);
    }

    // ---- Parameter search and the S-curve -----------------------------------------------------------

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(0.8)]
    [InlineData(0.95)]
    public void GetOptimalParameters_ShouldPutTheCurvesKneeNearTheThreshold(double threshold)
    {
        var (bands, rows) = MinHashLshIndex<int, int, Int32Murmur3Hasher>.GetOptimalParameters(threshold, 8);

        Assert.InRange(bands * rows, 1, 256);

        // The curve's steepest point is close to (1/b)^(1/r); the search should put it within reach
        // of the threshold, and the curve should separate both sides of it.
        double knee = Math.Pow(1d / bands, 1d / rows);
        Assert.InRange(knee, threshold - 0.15, threshold + 0.15);

        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(bands, rows);
        Assert.True(index.CandidateProbability(Math.Min(1, threshold + 0.2)) > 0.8);
        Assert.True(index.CandidateProbability(Math.Max(0, threshold - 0.3)) < 0.35);
    }

    [Fact]
    public void GetOptimalParameters_ShouldMinimiseTheErrorAreaOverEveryCandidatePair()
    {
        // Brute-force the search at a small precision with a much finer integration, and check the
        // chosen pair is no more than a hair worse than the best one found that way.
        const double Threshold = 0.6;
        var (bands, rows) = MinHashLshIndex<int, int, Int32Murmur3Hasher>.GetOptimalParameters(Threshold, 5);

        static double Error(int b, int r)
        {
            const int Steps = 4000;
            double fp = 0, fn = 0;
            for (int i = 0; i < Steps; i++)
            {
                double s = (i + 0.5) / Steps;
                double p = 1 - Math.Pow(1 - Math.Pow(s, r), b);
                if (s < Threshold)
                    fp += p / Steps;
                else
                    fn += (1 - p) / Steps;
            }

            return fp + fn;
        }

        double best = double.MaxValue;
        for (int b = 1; b <= 32; b++)
        {
            for (int r = 1; r <= 32 / b; r++)
                best = Math.Min(best, Error(b, r));
        }

        Assert.InRange(bands * rows, 1, 32);
        Assert.True(Error(bands, rows) <= best + 0.002, $"chose ({bands}, {rows}) with {Error(bands, rows)}, best {best}");
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(double.NaN)]
    [InlineData(-0.5)]
    public void GetOptimalParameters_ShouldThrow_WhenThresholdIsNotStrictlyBetweenZeroAndOne(double threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MinHashLshIndex<int, int, Int32Murmur3Hasher>.GetOptimalParameters(threshold, 8));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(17)]
    public void GetOptimalParameters_ShouldThrow_WhenPrecisionIsOutOfRange(int precision)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => MinHashLshIndex<int, int, Int32Murmur3Hasher>.GetOptimalParameters(0.5, precision));
        Assert.Equal("precision", ex.ParamName);
    }

    [Fact]
    public void CandidateProbability_ShouldFollowTheBandingFormula()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(bandCount: 20, rowsPerBand: 5);

        Assert.Equal(0d, index.CandidateProbability(0d));
        Assert.Equal(1d, index.CandidateProbability(1d));
        Assert.Equal(1 - Math.Pow(1 - Math.Pow(0.6, 5), 20), index.CandidateProbability(0.6), 12);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void CandidateProbability_ShouldThrow_WhenJaccardIsOutOfRange(double jaccard)
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        Assert.Equal("jaccard", Assert.Throws<ArgumentOutOfRangeException>(() => index.CandidateProbability(jaccard)).ParamName);
    }

    // ---- Add / TryAdd / Remove / ContainsKey -------------------------------------------------------

    [Fact]
    public void Query_ShouldReturnAnIdenticalSet_AndNotAnUnrelatedOne()
    {
        var index = new MinHashLshIndex<string, int, Int32Murmur3Hasher>(0.5);
        index.Add("a", Sketch(0, 500));
        index.Add("b", Sketch(10_000, 500));

        Assert.Equal(new[] { "a" }, index.Query(Sketch(0, 500)));
        Assert.Equal(new[] { "b" }, index.Query(Sketch(10_000, 500)));
        Assert.Empty(index.Query(Sketch(50_000, 500)));
    }

    [Fact]
    public void Query_ShouldReturnEachCandidateOnce_EvenWhenItMatchesEveryBand()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(bandCount: 64, rowsPerBand: 4);
        for (int key = 0; key < 5; key++)
            index.Add(key, Sketch(0, 1000));

        List<int> results = index.Query(Sketch(0, 1000));

        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, results.OrderBy(k => k));
    }

    [Fact]
    public void Query_ShouldAppendToTheGivenCollection_AndReturnTheNumberAppended()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        index.Add(1, Sketch(0, 300));
        index.Add(2, Sketch(0, 300));
        var results = new List<int> { 99 };

        int appended = index.Query(Sketch(0, 300), results);

        Assert.Equal(2, appended);
        Assert.Equal(new[] { 1, 2, 99 }, results.OrderBy(k => k));
    }

    [Fact]
    public void Query_ShouldReturnZero_WhenTheIndexIsEmpty()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        var results = new List<int>();

        Assert.Equal(0, index.Query(Sketch(0, 10), results));
        Assert.Empty(results);
    }

    [Fact]
    public void Query_ShouldFindNearDuplicates_AboveTheThreshold()
    {
        var index = new MinHashLshIndex<string, int, Int32Murmur3Hasher>(0.7);
        index.Add("original", Sketch(0, 1000));
        index.Add("edited", Sketch(50, 1000));    // J = 950 / 1050 ≈ 0.90
        index.Add("other", Sketch(5_000, 1000));  // J = 0

        List<string> results = index.Query(Sketch(0, 1000));

        Assert.Contains("original", results);
        Assert.Contains("edited", results);
        Assert.DoesNotContain("other", results);
    }

    [Fact]
    public void Add_ShouldThrow_WhenTheKeyIsAlreadyPresent_AndLeaveTheIndexUnchanged()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        index.Add(1, Sketch(0, 100));

        var ex = Assert.Throws<ArgumentException>(() => index.Add(1, Sketch(5_000, 100)));
        Assert.Equal("key", ex.ParamName);
        Assert.Equal(1, index.Count);
        Assert.Equal(new[] { 1 }, index.Query(Sketch(0, 100)));
        Assert.Empty(index.Query(Sketch(5_000, 100)));
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenTheKeyIsAlreadyPresent()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);

        Assert.True(index.TryAdd(1, Sketch(0, 100)));
        Assert.False(index.TryAdd(1, Sketch(0, 100)));
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void AddAndQuery_ShouldThrow_WhenArgumentsAreNull()
    {
        var index = new MinHashLshIndex<string, int, Int32Murmur3Hasher>(0.5);
        var sketch = Sketch(0, 10);

        Assert.Equal("key", Assert.Throws<ArgumentNullException>(() => index.Add(null!, sketch)).ParamName);
        Assert.Equal("key", Assert.Throws<ArgumentNullException>(() => index.TryAdd(null!, sketch)).ParamName);
        Assert.Equal("sketch", Assert.Throws<ArgumentNullException>(() => index.TryAdd("a", null!)).ParamName);
        Assert.Equal("key", Assert.Throws<ArgumentNullException>(() => index.Remove(null!)).ParamName);
        Assert.Equal("key", Assert.Throws<ArgumentNullException>(() => index.ContainsKey(null!)).ParamName);
        Assert.Equal("sketch", Assert.Throws<ArgumentNullException>(() => index.Query(null!)).ParamName);
        Assert.Equal("sketch", Assert.Throws<ArgumentNullException>(() => index.Query(null!, new List<string>())).ParamName);
        Assert.Equal("results", Assert.Throws<ArgumentNullException>(() => index.Query(sketch, null!)).ParamName);
        Assert.Equal(0, index.Count);
    }

    [Fact]
    public void AddAndQuery_ShouldThrow_WhenTheSketchHasADifferentPrecision()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5, precision: 8);
        var wrong = Sketch(0, 10, precision: 7);

        Assert.Equal("sketch", Assert.Throws<ArgumentException>(() => index.Add(1, wrong)).ParamName);
        Assert.Equal("sketch", Assert.Throws<ArgumentException>(() => index.Query(wrong)).ParamName);
        Assert.Equal(0, index.Count);
    }

    [Fact]
    public void Remove_ShouldTakeTheKeyOutOfEveryBucket()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        index.Add(1, Sketch(0, 200));
        index.Add(2, Sketch(0, 200));
        index.Add(3, Sketch(0, 200));

        // 2 sits mid-chain in every bucket, 3 at the head and 1 at the tail.
        Assert.True(index.Remove(2));
        Assert.Equal(new[] { 1, 3 }, index.Query(Sketch(0, 200)).OrderBy(k => k));
        Assert.True(index.Remove(3));
        Assert.Equal(new[] { 1 }, index.Query(Sketch(0, 200)));
        Assert.True(index.Remove(1));
        Assert.Empty(index.Query(Sketch(0, 200)));
        Assert.Equal(0, index.Count);
        Assert.False(index.ContainsKey(1));
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenTheKeyIsAbsent()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        index.Add(1, Sketch(0, 10));

        Assert.False(index.Remove(2));
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void Add_ShouldReuseARemovedSlot()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        for (int key = 0; key < 4; key++)
            index.Add(key, Sketch(key * 1000, 100));

        index.Remove(1);
        index.Remove(2);
        index.Add(10, Sketch(10_000, 100));
        index.Add(11, Sketch(11_000, 100));
        index.Add(12, Sketch(12_000, 100));

        Assert.Equal(new[] { 0, 3, 10, 11, 12 }, index.OrderBy(k => k));
        Assert.Equal(new[] { 11 }, index.Query(Sketch(11_000, 100)));
        Assert.Empty(index.Query(Sketch(1000, 100)));
    }

    [Fact]
    public void Add_ShouldBandTheSketchAsItWas_SoLaterChangesDoNotMoveTheKey()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        var sketch = Sketch(0, 300);
        index.Add(1, sketch);

        sketch.Clear();
        foreach (int item in Enumerable.Range(90_000, 300))
            sketch.Add(item);

        Assert.Equal(new[] { 1 }, index.Query(Sketch(0, 300)));
        Assert.Empty(index.Query(sketch));
    }

    [Fact]
    public void ManyKeys_ShouldGrowTheStorage_AndStayFindable()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        for (int key = 0; key < 300; key++)
            index.Add(key, Sketch(key * 100, 60));

        Assert.Equal(300, index.Count);
        for (int key = 0; key < 300; key += 37)
            Assert.Contains(key, index.Query(Sketch(key * 100, 60)));
    }

    // ---- Small and empty sets (densification) -------------------------------------------------------

    [Fact]
    public void SmallSets_ShouldNotAllCollide_ThroughTheirEmptyRegisters()
    {
        // Five elements fill at most five of 256 registers. Without densification every band of
        // empty registers would match, and each query would return the whole index.
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        for (int key = 0; key < 200; key++)
            index.Add(key, Sketch(key * 10, 5));

        int total = 0;
        for (int key = 0; key < 200; key++)
        {
            List<int> results = index.Query(Sketch(key * 10, 5));
            Assert.Contains(key, results);
            total += results.Count;
        }

        Assert.True(total <= 260, $"{total} candidates for 200 queries of unrelated sets");
    }

    [Fact]
    public void SingleElementSets_ShouldMatchOnlyTheSameElement()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        for (int key = 0; key < 50; key++)
            index.Add(key, Sketch(new[] { key }));

        for (int key = 0; key < 50; key++)
            Assert.Equal(new[] { key }, index.Query(Sketch(new[] { key })));
    }

    [Fact]
    public void EmptySketches_ShouldMatchEachOther_AndNothingElse()
    {
        var index = new MinHashLshIndex<string, int, Int32Murmur3Hasher>(0.5);
        index.Add("empty1", Sketch(Array.Empty<int>()));
        index.Add("empty2", Sketch(Array.Empty<int>()));
        index.Add("full", Sketch(0, 100));

        Assert.Equal(new[] { "empty1", "empty2" }, index.Query(Sketch(Array.Empty<int>())).OrderBy(k => k));
        Assert.Equal(new[] { "full" }, index.Query(Sketch(0, 100)));
    }

    [Theory]
    [InlineData(10, 1024, 1)]   // 1,024 one-row bands: every scratch buffer is rented
    [InlineData(10, 300, 3)]    // 300 bands of 3 rows: rented band scratch and densified copy
    [InlineData(10, 8, 8)]      // few bands over a large sketch: only the filled-register list is rented
    public void LargePrecisions_ShouldWork_WhenScratchBuffersAreRented(int precision, int bands, int rows)
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(bands, rows, precision);
        index.Add(1, Sketch(0, 40, precision));       // sparse: densification falls back to the filled list
        index.Add(2, Sketch(10_000, 5_000, precision));    // dense
        index.Add(3, Sketch(70_000, 40, precision));

        Assert.Equal(new[] { 1 }, index.Query(Sketch(0, 40, precision)));
        Assert.Equal(new[] { 2 }, index.Query(Sketch(10_000, 5_000, precision)));
        Assert.True(index.Remove(1));
        Assert.Empty(index.Query(Sketch(0, 40, precision)));
    }

    // ---- Clear and reentrancy -----------------------------------------------------------------------

    [Fact]
    public void Clear_ShouldRemoveEveryKey_AndKeepTheIndexUsable()
    {
        var index = new MinHashLshIndex<string, int, Int32Murmur3Hasher>(0.5);
        index.Add("a", Sketch(0, 100));
        index.Add("b", Sketch(1000, 100));
        index.Remove("a");

        index.Clear();

        Assert.Equal(0, index.Count);
        Assert.Empty(index);
        Assert.False(index.ContainsKey("b"));
        Assert.Empty(index.Query(Sketch(1000, 100)));

        index.Add("c", Sketch(1000, 100));
        Assert.Equal(new[] { "c" }, index.Query(Sketch(1000, 100)));
    }

    [Fact]
    public void Clear_ShouldRemoveEveryKey_WhenKeysAreValueTyped()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        index.Add(1, Sketch(0, 100));

        index.Clear();

        Assert.Equal(0, index.Count);
        Assert.Empty(index.Query(Sketch(0, 100)));
    }

    [Fact]
    public void Query_ShouldThrow_WhenTheResultsCollectionModifiesTheIndex()
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        index.Add(1, Sketch(0, 100));
        index.Add(2, Sketch(0, 100));
        var results = new RemovingCollection(index);

        Assert.Throws<InvalidOperationException>(() => index.Query(Sketch(0, 100), results));
    }

    // A results collection whose Add removes the key it is given from the index.
    private sealed class RemovingCollection(MinHashLshIndex<int, int, Int32Murmur3Hasher> index) : List<int>, ICollection<int>
    {
        void ICollection<int>.Add(int item)
        {
            Add(item);
            index.Remove(item);
        }
    }
}
