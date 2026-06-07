using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class BloomFilterTests
{
    [Fact]
    public void Add_ThenContains_ShouldReturnTrue()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(100);
        filter.Add(42);
        Assert.True(filter.Contains(42));
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenNotAdded()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(100);
        filter.Add(42);
        // A never-added element: with only one element in a 100-capacity filter the
        // odds of a false positive on this specific value are negligible.
        Assert.False(filter.Contains(7));
    }

    [Fact]
    public void Add_ShouldIncrementCount_EvenForDuplicates()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(100);
        filter.Add(1);
        filter.Add(1); // a Bloom filter cannot detect the duplicate
        Assert.Equal(2, filter.Count);
    }

    [Fact]
    public void NoFalseNegatives_ForEveryAddedElement()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(1000);
        for (int i = 0; i < 1000; i++)
            filter.Add(i * 7 + 1);

        for (int i = 0; i < 1000; i++)
            Assert.True(filter.Contains(i * 7 + 1), $"false negative for {i * 7 + 1}");
    }

    [Fact]
    public void ZeroIntElement_IsAnOrdinaryElement()
    {
        // Unlike the hash-table collections, a Bloom filter has no empty-slot
        // sentinel, so default(int) == 0 needs no out-of-band handling.
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(100);
        Assert.False(filter.Contains(0));
        filter.Add(0);
        Assert.True(filter.Contains(0));
    }

    [Fact]
    public void GuidEmpty_IsAnOrdinaryElement()
    {
        var filter = new BloomFilter<Guid, GuidHasher>(100);
        Assert.False(filter.Contains(Guid.Empty));
        filter.Add(Guid.Empty);
        Assert.True(filter.Contains(Guid.Empty));
    }

    [Fact]
    public void NullReference_IsAddable_WithoutInvokingTheHasher()
    {
        // StringFnV1AHasher throws on null; BloomFilter maps a null reference to a
        // fixed base hash so it never calls the hasher with null.
        var filter = new BloomFilter<string, StringFnV1AHasher>(100);
        Assert.False(filter.Contains(null!));
        filter.Add(null!);
        Assert.True(filter.Contains(null!));
        Assert.True(filter.Contains(null!)); // stable
    }

    [Fact]
    public void StringElements_RoundTrip()
    {
        var filter = new BloomFilter<string, StringFnV1AHasher>(100);
        filter.Add("alice");
        filter.Add("bob");
        Assert.True(filter.Contains("alice"));
        Assert.True(filter.Contains("bob"));
        Assert.False(filter.Contains("carol"));
    }

    [Fact]
    public void EmptyString_IsAnOrdinaryElement()
    {
        var filter = new BloomFilter<string, StringFnV1AHasher>(100);
        filter.Add("");
        Assert.True(filter.Contains(""));
    }

    [Fact]
    public void Clear_ShouldResetMembershipAndCount()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(100);
        for (int i = 0; i < 50; i++)
            filter.Add(i);

        filter.Clear();

        Assert.Equal(0, filter.Count);
        for (int i = 0; i < 50; i++)
            Assert.False(filter.Contains(i));

        // Reusable after clear.
        filter.Add(5);
        Assert.True(filter.Contains(5));
        Assert.Equal(1, filter.Count);
    }

    [Fact]
    public void Clear_OnEmptyFilter_IsANoOp()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(100);
        filter.Clear();
        Assert.Equal(0, filter.Count);
    }

    [Fact]
    public void Sizing_ChoosesPowerOfTwoBitCountAndPositiveHashCount()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(1000, 0.01);

        Assert.Equal(1000, filter.Capacity);
        Assert.Equal(0.01, filter.FalsePositiveRate);
        Assert.True(filter.BitCount >= 64);
        // m must be a power of two.
        Assert.Equal(0, filter.BitCount & (filter.BitCount - 1));
        Assert.True(filter.HashCount >= 1);
    }

    [Fact]
    public void Sizing_LowerFalsePositiveRate_AllocatesMoreBits()
    {
        var loose = new BloomFilter<int, Int32WangNaiveHasher>(1000, 0.1);
        var tight = new BloomFilter<int, Int32WangNaiveHasher>(1000, 0.001);
        Assert.True(tight.BitCount > loose.BitCount);
    }

    [Fact]
    public void Sizing_TinyCapacity_StillAllocatesAtLeastOneWord()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(1, 0.5);
        Assert.True(filter.BitCount >= 64);
        filter.Add(123);
        Assert.True(filter.Contains(123));
    }

    [Fact]
    public void CurrentFalsePositiveProbability_IsZero_WhenEmpty()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(1000);
        Assert.Equal(0d, filter.CurrentFalsePositiveProbability);
    }

    [Fact]
    public void CurrentFalsePositiveProbability_RisesAsFilterFills()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(1000);
        for (int i = 0; i < 500; i++)
            filter.Add(i);
        double half = filter.CurrentFalsePositiveProbability;

        for (int i = 500; i < 2000; i++)
            filter.Add(i);
        double over = filter.CurrentFalsePositiveProbability;

        Assert.True(half > 0d);
        Assert.True(over > half);
    }

    // ---------------------------------------------------------------
    //  IEnumerable constructor
    // ---------------------------------------------------------------

    [Fact]
    public void IEnumerableConstructor_AddsAllElements()
    {
        var source = new[] { 1, 2, 3, 4, 5 };
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(source);

        Assert.Equal(5, filter.Count);
        foreach (int i in source)
            Assert.True(filter.Contains(i));
    }

    [Fact]
    public void IEnumerableConstructor_SizesFromCollectionCount()
    {
        var source = Enumerable.Range(0, 5000).ToList();
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(source, 0.01);

        Assert.Equal(5000, filter.Capacity);
        Assert.Equal(5000, filter.Count);
    }

    [Fact]
    public void IEnumerableConstructor_SizesFromNonCollectionSource()
    {
        // A lazy sequence is not an ICollection<T>, so the ctor counts it in a pass.
        IEnumerable<int> source = Enumerable.Range(0, 2000).Where(i => true);
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(source);

        Assert.Equal(2000, filter.Capacity);
        Assert.Equal(2000, filter.Count);
        for (int i = 0; i < 2000; i++)
            Assert.True(filter.Contains(i));
    }

    [Fact]
    public void IEnumerableConstructor_EmptySource_BuildsUsableFilter()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(Array.Empty<int>());
        Assert.Equal(0, filter.Count);
        filter.Add(1);
        Assert.True(filter.Contains(1));
    }

    [Fact]
    public void IEnumerableConstructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BloomFilter<int, Int32WangNaiveHasher>((IEnumerable<int>)null!));
    }

    // Null source must beat an out-of-range false-positive rate.
    [Fact]
    public void IEnumerableConstructor_NullSource_BeatsBadFalsePositiveRate()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BloomFilter<int, Int32WangNaiveHasher>((IEnumerable<int>)null!, 2.0));
    }

    // ---------------------------------------------------------------
    //  Constructor validation
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_Throws_WhenExpectedItemsIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BloomFilter<int, Int32WangNaiveHasher>(0));
    }

    [Fact]
    public void Constructor_Throws_WhenExpectedItemsIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BloomFilter<int, Int32WangNaiveHasher>(-5));
    }

    [Fact]
    public void Constructor_Throws_WhenFalsePositiveRateIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BloomFilter<int, Int32WangNaiveHasher>(100, 0d));
    }

    [Fact]
    public void Constructor_Throws_WhenFalsePositiveRateIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BloomFilter<int, Int32WangNaiveHasher>(100, 1d));
    }

    [Fact]
    public void Constructor_Throws_WhenFalsePositiveRateIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BloomFilter<int, Int32WangNaiveHasher>(100, -0.1));
    }

    [Fact]
    public void Constructor_Throws_WhenFalsePositiveRateExceedsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BloomFilter<int, Int32WangNaiveHasher>(100, 1.5));
    }

    [Fact]
    public void Constructor_Throws_WhenFalsePositiveRateIsNaN()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BloomFilter<int, Int32WangNaiveHasher>(100, double.NaN));
    }

    // ---------------------------------------------------------------
    //  UnionWith
    // ---------------------------------------------------------------

    [Fact]
    public void UnionWith_MergesMembership()
    {
        var a = new BloomFilter<int, Int32WangNaiveHasher>(1000);
        var b = new BloomFilter<int, Int32WangNaiveHasher>(1000);

        for (int i = 0; i < 100; i++) a.Add(i);
        for (int i = 100; i < 200; i++) b.Add(i);

        a.UnionWith(b);

        for (int i = 0; i < 200; i++)
            Assert.True(a.Contains(i), $"false negative for {i} after union");
        Assert.Equal(200, a.Count);
    }

    [Fact]
    public void UnionWith_LeavesOtherUnmodified()
    {
        var a = new BloomFilter<int, Int32WangNaiveHasher>(1000);
        var b = new BloomFilter<int, Int32WangNaiveHasher>(1000);
        a.Add(1);
        b.Add(2);

        a.UnionWith(b);

        Assert.Equal(1, b.Count);
        Assert.False(b.Contains(1));
    }

    [Fact]
    public void UnionWith_Null_Throws()
    {
        var a = new BloomFilter<int, Int32WangNaiveHasher>(100);
        Assert.Throws<ArgumentNullException>(() => a.UnionWith(null!));
    }

    [Fact]
    public void UnionWith_IncompatibleBitCount_Throws()
    {
        var a = new BloomFilter<int, Int32WangNaiveHasher>(100);
        var b = new BloomFilter<int, Int32WangNaiveHasher>(100_000);
        Assert.Throws<ArgumentException>(() => a.UnionWith(b));
    }
}
