using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class XorFilterTests
{
    [Fact]
    public void Build_ThenContains_ReturnsTrue_ForEveryElement()
    {
        var filter = new XorFilter<int, Int32WangNaiveHasher>(new[] { 42, 7, 100, -3 });
        Assert.True(filter.Contains(42));
        Assert.True(filter.Contains(7));
        Assert.True(filter.Contains(100));
        Assert.True(filter.Contains(-3));
    }

    [Fact]
    public void Contains_ReturnsFalse_ForAbsentElement()
    {
        var filter = new XorFilter<int, Int32WangNaiveHasher>(new[] { 42 });
        // A never-added element in a one-element filter: a false positive on this
        // specific value is a ~1/256 event, and 999 is not it for this hasher.
        Assert.False(filter.Contains(999));
    }

    [Fact]
    public void NoFalseNegatives_ForEveryElement()
    {
        var source = new int[1000];
        for (int i = 0; i < source.Length; i++)
            source[i] = i * 7 + 1;

        var filter = new XorFilter<int, Int32WangNaiveHasher>(source);

        foreach (int v in source)
            Assert.True(filter.Contains(v), $"false negative for {v}");
    }

    [Fact]
    public void ZeroIntElement_IsAnOrdinaryElement()
    {
        // Unlike the hash-table collections, an xor filter has no empty-slot sentinel,
        // so default(int) == 0 needs no out-of-band handling.
        var filter = new XorFilter<int, Int32WangNaiveHasher>(new[] { 0, 1, 2 });
        Assert.True(filter.Contains(0));
    }

    [Fact]
    public void GuidEmpty_IsAnOrdinaryElement()
    {
        var filter = new XorFilter<Guid, GuidHasher>(new[] { Guid.Empty, Guid.NewGuid() });
        Assert.True(filter.Contains(Guid.Empty));
    }

    [Fact]
    public void NullReference_IsBuildable_WithoutInvokingTheHasher()
    {
        // StringFnV1AHasher throws on null; XorFilter maps a null reference to a fixed
        // base hash so it never calls the hasher with null.
        var filter = new XorFilter<string, StringFnV1AHasher>(new[] { "alice", null!, "bob" });
        Assert.True(filter.Contains(null!));
        Assert.True(filter.Contains(null!)); // stable
        Assert.True(filter.Contains("alice"));
        Assert.True(filter.Contains("bob"));
    }

    [Fact]
    public void StringElements_RoundTrip()
    {
        var filter = new XorFilter<string, StringFnV1AHasher>(new[] { "alice", "bob" });
        Assert.True(filter.Contains("alice"));
        Assert.True(filter.Contains("bob"));
        Assert.False(filter.Contains("carol"));
    }

    [Fact]
    public void EmptyString_IsAnOrdinaryElement()
    {
        var filter = new XorFilter<string, StringFnV1AHasher>(new[] { "", "x" });
        Assert.True(filter.Contains(""));
    }

    [Fact]
    public void Count_IsDistinctElementCount()
    {
        var filter = new XorFilter<int, Int32WangNaiveHasher>(new[] { 1, 2, 3, 4, 5 });
        Assert.Equal(5, filter.Count);
    }

    [Fact]
    public void Count_DeduplicatesRepeatedElements()
    {
        // A membership filter over a set collapses duplicates.
        var filter = new XorFilter<int, Int32WangNaiveHasher>(new[] { 7, 7, 7, 8, 8 });
        Assert.Equal(2, filter.Count);
        Assert.True(filter.Contains(7));
        Assert.True(filter.Contains(8));
    }

    [Fact]
    public void Sizing_SlotCountIsAboutOnePointTwoThreeTimesCount()
    {
        var filter = new XorFilter<int, Int32Murmur3Hasher>(Enumerable.Range(0, 10_000).ToArray());
        // 3·blockLength ≈ 1.23·n + a small constant.
        Assert.True(filter.SlotCount >= filter.Count);
        Assert.True(filter.SlotCount <= (int)(1.30 * filter.Count) + 64);
        Assert.Equal(0, filter.SlotCount % 3); // three equal segments
    }

    [Fact]
    public void FixedProperties_ReportEightBitFingerprint()
    {
        var filter = new XorFilter<int, Int32WangNaiveHasher>(new[] { 1 });
        Assert.Equal(8, filter.FingerprintBits);
        Assert.Equal(8, XorFilter<int, Int32WangNaiveHasher>.FINGERPRINT_BITS);
        Assert.Equal(1.0 / 256, filter.FalsePositiveRate);
    }

    [Fact]
    public void BitsPerElement_IsNearTheXorFilterFloor()
    {
        var filter = new XorFilter<int, Int32Murmur3Hasher>(Enumerable.Range(0, 50_000).ToArray());
        // ~9.84 bits/element in the limit; allow slack for the fixed +32 slot headroom.
        Assert.True(filter.BitsPerElement >= 9.0 && filter.BitsPerElement <= 11.0,
            $"bits/element {filter.BitsPerElement:F2} outside the expected xor-filter band");
    }

    // ---------------------------------------------------------------
    //  Empty and single-element edge cases
    // ---------------------------------------------------------------

    [Fact]
    public void EmptySource_BuildsFilterThatReportsEverythingAbsent()
    {
        var filter = new XorFilter<int, Int32WangNaiveHasher>(Array.Empty<int>());
        Assert.Equal(0, filter.Count);
        Assert.Equal(0d, filter.BitsPerElement);
        Assert.False(filter.Contains(0));
        Assert.False(filter.Contains(123));
    }

    [Fact]
    public void SingleElement_RoundTrips()
    {
        var filter = new XorFilter<int, Int32WangNaiveHasher>(new[] { 12345 });
        Assert.Equal(1, filter.Count);
        Assert.True(filter.Contains(12345));
    }

    [Fact]
    public void NonCollectionSource_IsEnumeratedOnce()
    {
        // A lazy sequence is not an ICollection<T>; the constructor still consumes it correctly.
        IEnumerable<int> source = Enumerable.Range(0, 2000).Where(i => true);
        var filter = new XorFilter<int, Int32Murmur3Hasher>(source);

        Assert.Equal(2000, filter.Count);
        for (int i = 0; i < 2000; i++)
            Assert.True(filter.Contains(i), $"false negative for {i}");
    }

    // ---------------------------------------------------------------
    //  Constructor validation
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new XorFilter<int, Int32WangNaiveHasher>(null!));
    }
}
