using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class CuckooFilterTests
{
    [Fact]
    public void Add_ThenContains_ShouldReturnTrue()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        filter.Add(42);
        Assert.True(filter.Contains(42));
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenNotAdded()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        filter.Add(42);
        Assert.False(filter.Contains(7));
    }

    [Fact]
    public void Add_ShouldIncrementCount()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        filter.Add(1);
        filter.Add(2);
        Assert.Equal(2, filter.Count);
    }

    [Fact]
    public void Add_Duplicate_StoresASecondCopy()
    {
        // A cuckoo filter cannot detect the duplicate, so it stores a second fingerprint.
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        filter.Add(1);
        filter.Add(1);
        Assert.Equal(2, filter.Count);
        Assert.True(filter.Contains(1));
    }

    [Fact]
    public void NoFalseNegatives_ForEveryAddedElement()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(2000);
        for (int i = 0; i < 1000; i++)
            filter.Add(i * 7 + 1);

        for (int i = 0; i < 1000; i++)
            Assert.True(filter.Contains(i * 7 + 1), $"false negative for {i * 7 + 1}");
    }

    [Fact]
    public void TryAdd_ReturnsTrue_WhenRoom()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        Assert.True(filter.TryAdd(5));
        Assert.True(filter.Contains(5));
        Assert.Equal(1, filter.Count);
    }

    // ---------------------------------------------------------------
    //  Remove — the differentiator from BloomFilter
    // ---------------------------------------------------------------

    [Fact]
    public void Remove_DeletesMembership()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        filter.Add(42);
        Assert.True(filter.Contains(42));

        Assert.True(filter.Remove(42));
        Assert.False(filter.Contains(42));
        Assert.Equal(0, filter.Count);
    }

    [Fact]
    public void Remove_NotPresent_ReturnsFalse()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        filter.Add(1);
        Assert.False(filter.Remove(999));
        Assert.Equal(1, filter.Count);
        Assert.True(filter.Contains(1));
    }

    [Fact]
    public void Remove_FromEmptyFilter_ReturnsFalse()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        Assert.False(filter.Remove(1));
        Assert.Equal(0, filter.Count);
    }

    [Fact]
    public void Remove_OneOfTwoDuplicates_LeavesOneCopy()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        filter.Add(7);
        filter.Add(7);
        Assert.Equal(2, filter.Count);

        Assert.True(filter.Remove(7));
        Assert.Equal(1, filter.Count);
        Assert.True(filter.Contains(7)); // a copy remains

        Assert.True(filter.Remove(7));
        Assert.False(filter.Contains(7));
        Assert.Equal(0, filter.Count);
    }

    [Fact]
    public void AddRemoveChurn_KeepsMembershipConsistent()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(5000);

        // Add evens, then remove every fourth, and verify membership tracks exactly.
        for (int i = 0; i < 4000; i += 2)
            filter.Add(i);
        for (int i = 0; i < 4000; i += 4)
            Assert.True(filter.Remove(i));

        for (int i = 0; i < 4000; i++)
        {
            bool shouldBePresent = (i % 2 == 0) && (i % 4 != 0);
            if (shouldBePresent)
                Assert.True(filter.Contains(i), $"false negative for {i}");
        }
    }

    [Fact]
    public void Remove_DoesNotAffectOtherElements()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(1000);
        for (int i = 0; i < 500; i++)
            filter.Add(i);

        Assert.True(filter.Remove(100));

        for (int i = 0; i < 500; i++)
        {
            if (i == 100) continue;
            Assert.True(filter.Contains(i), $"removing 100 wrongly affected {i}");
        }
    }

    // ---------------------------------------------------------------
    //  Default / null element handling
    // ---------------------------------------------------------------

    [Fact]
    public void ZeroIntElement_IsAnOrdinaryElement()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        Assert.False(filter.Contains(0));
        filter.Add(0);
        Assert.True(filter.Contains(0));
        Assert.True(filter.Remove(0));
        Assert.False(filter.Contains(0));
    }

    [Fact]
    public void GuidEmpty_IsAnOrdinaryElement()
    {
        var filter = new CuckooFilter<Guid, GuidHasher>(100);
        Assert.False(filter.Contains(Guid.Empty));
        filter.Add(Guid.Empty);
        Assert.True(filter.Contains(Guid.Empty));
    }

    [Fact]
    public void NullReference_IsAddable_WithoutInvokingTheHasher()
    {
        // StringFnV1AHasher throws on null; CuckooFilter maps a null reference to a fixed
        // base hash so it never calls the hasher with null.
        var filter = new CuckooFilter<string, StringFnV1AHasher>(100);
        Assert.False(filter.Contains(null!));
        filter.Add(null!);
        Assert.True(filter.Contains(null!));
        Assert.True(filter.Contains(null!)); // stable
        Assert.True(filter.Remove(null!));
        Assert.False(filter.Contains(null!));
    }

    [Fact]
    public void StringElements_RoundTrip()
    {
        var filter = new CuckooFilter<string, StringFnV1AHasher>(100);
        filter.Add("alice");
        filter.Add("bob");
        Assert.True(filter.Contains("alice"));
        Assert.True(filter.Contains("bob"));
        Assert.False(filter.Contains("carol"));

        Assert.True(filter.Remove("alice"));
        Assert.False(filter.Contains("alice"));
        Assert.True(filter.Contains("bob"));
    }

    [Fact]
    public void EmptyString_IsAnOrdinaryElement()
    {
        var filter = new CuckooFilter<string, StringFnV1AHasher>(100);
        filter.Add("");
        Assert.True(filter.Contains(""));
    }

    // ---------------------------------------------------------------
    //  Clear
    // ---------------------------------------------------------------

    [Fact]
    public void Clear_ShouldResetMembershipAndCount()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
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
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        filter.Clear();
        Assert.Equal(0, filter.Count);
    }

    // ---------------------------------------------------------------
    //  Sizing
    // ---------------------------------------------------------------

    [Fact]
    public void Sizing_ChoosesPowerOfTwoBucketCountAndValidFingerprintWidth()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(1000, 0.01);

        Assert.Equal(1000, filter.Capacity);
        Assert.Equal(0.01, filter.FalsePositiveRate);
        Assert.True(filter.BucketCount >= 1);
        // Bucket count must be a power of two.
        Assert.Equal(0, filter.BucketCount & (filter.BucketCount - 1));
        Assert.InRange(filter.FingerprintBits, 1, 16);
    }

    [Fact]
    public void Sizing_LowerFalsePositiveRate_WidensFingerprint()
    {
        var loose = new CuckooFilter<int, Int32Murmur3Hasher>(1000, 0.1);
        var tight = new CuckooFilter<int, Int32Murmur3Hasher>(1000, 0.001);
        Assert.True(tight.FingerprintBits > loose.FingerprintBits);
    }

    [Fact]
    public void Sizing_VeryLowFalsePositiveRate_ClampsFingerprintTo16Bits()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(1000, 1e-9);
        Assert.Equal(16, filter.FingerprintBits);
    }

    [Fact]
    public void Sizing_TinyCapacity_StillUsable()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(1, 0.5);
        Assert.True(filter.BucketCount >= 1);
        filter.Add(123);
        Assert.True(filter.Contains(123));
    }

    [Fact]
    public void LoadFactor_RisesWithFill()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(1000);
        Assert.Equal(0d, filter.LoadFactor);

        for (int i = 0; i < 500; i++)
            filter.Add(i);

        Assert.True(filter.LoadFactor > 0d && filter.LoadFactor <= 1d);
    }

    // ---------------------------------------------------------------
    //  Fullness / capacity boundary
    // ---------------------------------------------------------------

    [Fact]
    public void FillingBeyondCapacity_ReportsFull_WithoutLosingElements()
    {
        // A single-bucket filter (expectedItems == 1) holds only BucketSize fingerprints
        // plus one victim, so it fills quickly and deterministically.
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(1, 0.01);

        var added = new List<int>();
        int v = 0;
        while (filter.TryAdd(v))
        {
            added.Add(v);
            v++;
            if (v > 100_000) break; // safety valve; should fill long before this
        }

        Assert.True(filter.IsFull);
        Assert.NotEmpty(added);

        // No false negatives, even at the failure boundary (the homeless fingerprint is
        // parked in the victim cache, not dropped).
        foreach (int a in added)
            Assert.True(filter.Contains(a), $"false negative for {a} at the fill boundary");

        // Add throws when full; TryAdd reports false.
        Assert.Throws<InvalidOperationException>(() => filter.Add(v));
        Assert.False(filter.TryAdd(v));
    }

    [Fact]
    public void Remove_FreesSpace_AfterFull()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(1, 0.01);

        var added = new List<int>();
        int v = 0;
        while (filter.TryAdd(v))
        {
            added.Add(v);
            v++;
            if (v > 100_000) break;
        }
        Assert.True(filter.IsFull);

        // Removing an element frees a slot and re-homes the parked victim, so the filter
        // becomes writable again.
        Assert.True(filter.Remove(added[0]));
        Assert.False(filter.IsFull);
        Assert.True(filter.TryAdd(v));
    }

    // ---------------------------------------------------------------
    //  IEnumerable constructor
    // ---------------------------------------------------------------

    [Fact]
    public void IEnumerableConstructor_AddsAllElements()
    {
        var source = new[] { 1, 2, 3, 4, 5 };
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(source);

        Assert.Equal(5, filter.Count);
        foreach (int i in source)
            Assert.True(filter.Contains(i));
    }

    [Fact]
    public void IEnumerableConstructor_SizesFromCollectionCount()
    {
        var source = Enumerable.Range(0, 5000).ToList();
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(source, 0.01);

        Assert.Equal(5000, filter.Capacity);
        Assert.Equal(5000, filter.Count);
    }

    [Fact]
    public void IEnumerableConstructor_SizesFromNonCollectionSource()
    {
        IEnumerable<int> source = Enumerable.Range(0, 2000).Where(i => true);
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(source);

        Assert.Equal(2000, filter.Capacity);
        Assert.Equal(2000, filter.Count);
        for (int i = 0; i < 2000; i++)
            Assert.True(filter.Contains(i));
    }

    [Fact]
    public void IEnumerableConstructor_EmptySource_BuildsUsableFilter()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(Array.Empty<int>());
        Assert.Equal(0, filter.Count);
        filter.Add(1);
        Assert.True(filter.Contains(1));
    }

    [Fact]
    public void IEnumerableConstructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CuckooFilter<int, Int32Murmur3Hasher>((IEnumerable<int>)null!));
    }

    [Fact]
    public void IEnumerableConstructor_NullSource_BeatsBadFalsePositiveRate()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CuckooFilter<int, Int32Murmur3Hasher>((IEnumerable<int>)null!, 2.0));
    }

    // ---------------------------------------------------------------
    //  Constructor validation
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_Throws_WhenExpectedItemsIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CuckooFilter<int, Int32Murmur3Hasher>(0));
    }

    [Fact]
    public void Constructor_Throws_WhenExpectedItemsIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CuckooFilter<int, Int32Murmur3Hasher>(-5));
    }

    [Fact]
    public void Constructor_Throws_WhenFalsePositiveRateIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CuckooFilter<int, Int32Murmur3Hasher>(100, 0d));
    }

    [Fact]
    public void Constructor_Throws_WhenFalsePositiveRateIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CuckooFilter<int, Int32Murmur3Hasher>(100, 1d));
    }

    [Fact]
    public void Constructor_Throws_WhenFalsePositiveRateIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CuckooFilter<int, Int32Murmur3Hasher>(100, -0.1));
    }

    [Fact]
    public void Constructor_Throws_WhenFalsePositiveRateExceedsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CuckooFilter<int, Int32Murmur3Hasher>(100, 1.5));
    }

    [Fact]
    public void Constructor_Throws_WhenFalsePositiveRateIsNaN()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CuckooFilter<int, Int32Murmur3Hasher>(100, double.NaN));
    }

    // ---------------------------------------------------------------
    //  UnionWith
    // ---------------------------------------------------------------

    [Fact]
    public void UnionWith_MergesMembership()
    {
        var a = new CuckooFilter<int, Int32Murmur3Hasher>(2000);
        var b = new CuckooFilter<int, Int32Murmur3Hasher>(2000);

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
        var a = new CuckooFilter<int, Int32Murmur3Hasher>(1000);
        var b = new CuckooFilter<int, Int32Murmur3Hasher>(1000);
        a.Add(1);
        b.Add(2);

        a.UnionWith(b);

        Assert.Equal(1, b.Count);
        Assert.False(b.Contains(1));
        Assert.True(b.Contains(2));
    }

    [Fact]
    public void UnionWith_Null_Throws()
    {
        var a = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        Assert.Throws<ArgumentNullException>(() => a.UnionWith(null!));
    }

    [Fact]
    public void UnionWith_IncompatibleBucketCount_Throws()
    {
        var a = new CuckooFilter<int, Int32Murmur3Hasher>(100);
        var b = new CuckooFilter<int, Int32Murmur3Hasher>(100_000);
        Assert.Throws<ArgumentException>(() => a.UnionWith(b));
    }

    [Fact]
    public void UnionWith_IncompatibleFingerprintWidth_Throws()
    {
        // Same expected items but different target rates → different fingerprint widths.
        var a = new CuckooFilter<int, Int32Murmur3Hasher>(1000, 0.1);
        var b = new CuckooFilter<int, Int32Murmur3Hasher>(1000, 0.001);
        Assert.Throws<ArgumentException>(() => a.UnionWith(b));
    }
}
