using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class BinaryFuseFilterTests
{
    [Fact]
    public void Build_ThenContains_ReturnsTrue_ForEveryElement()
    {
        var filter = new BinaryFuseFilter<int, Int32WangNaiveHasher>(new[] { 42, 7, 100, -3 });
        Assert.True(filter.Contains(42));
        Assert.True(filter.Contains(7));
        Assert.True(filter.Contains(100));
        Assert.True(filter.Contains(-3));
    }

    [Fact]
    public void Contains_ReturnsFalse_ForAbsentElement()
    {
        var filter = new BinaryFuseFilter<int, Int32WangNaiveHasher>(new[] { 42 });
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

        var filter = new BinaryFuseFilter<int, Int32WangNaiveHasher>(source);

        foreach (int v in source)
            Assert.True(filter.Contains(v), $"false negative for {v}");
    }

    [Fact]
    public void ZeroIntElement_IsAnOrdinaryElement()
    {
        // Unlike the hash-table collections, a binary fuse filter has no empty-slot sentinel,
        // so default(int) == 0 needs no out-of-band handling.
        var filter = new BinaryFuseFilter<int, Int32WangNaiveHasher>(new[] { 0, 1, 2 });
        Assert.True(filter.Contains(0));
    }

    [Fact]
    public void GuidEmpty_IsAnOrdinaryElement()
    {
        var filter = new BinaryFuseFilter<Guid, GuidHasher>(new[] { Guid.Empty, Guid.NewGuid() });
        Assert.True(filter.Contains(Guid.Empty));
    }

    [Fact]
    public void NullReference_IsBuildable_WithoutInvokingTheHasher()
    {
        // StringFnV1AHasher throws on null; BinaryFuseFilter maps a null reference to a fixed
        // base hash so it never calls the hasher with null.
        var filter = new BinaryFuseFilter<string, StringFnV1AHasher>(new[] { "alice", null!, "bob" });
        Assert.True(filter.Contains(null!));
        Assert.True(filter.Contains(null!)); // stable
        Assert.True(filter.Contains("alice"));
        Assert.True(filter.Contains("bob"));
    }

    [Fact]
    public void StringElements_RoundTrip()
    {
        var filter = new BinaryFuseFilter<string, StringFnV1AHasher>(new[] { "alice", "bob" });
        Assert.True(filter.Contains("alice"));
        Assert.True(filter.Contains("bob"));
        Assert.False(filter.Contains("carol"));
    }

    [Fact]
    public void EmptyString_IsAnOrdinaryElement()
    {
        var filter = new BinaryFuseFilter<string, StringFnV1AHasher>(new[] { "", "x" });
        Assert.True(filter.Contains(""));
    }

    [Fact]
    public void Count_IsDistinctElementCount()
    {
        var filter = new BinaryFuseFilter<int, Int32WangNaiveHasher>(new[] { 1, 2, 3, 4, 5 });
        Assert.Equal(5, filter.Count);
    }

    [Fact]
    public void Count_DeduplicatesRepeatedElements()
    {
        // A membership filter over a set collapses duplicates.
        var filter = new BinaryFuseFilter<int, Int32WangNaiveHasher>(new[] { 7, 7, 7, 8, 8 });
        Assert.Equal(2, filter.Count);
        Assert.True(filter.Contains(7));
        Assert.True(filter.Contains(8));
    }

    [Fact]
    public void Sizing_SlotCountFollowsThePaperSizingRule()
    {
        var filter = new BinaryFuseFilter<int, Int32Murmur3Hasher>(Enumerable.Range(0, 10_000).ToArray());
        // At 10,000 elements the rule asks for 0.875 + 0.25 · ln(10⁶) / ln(10⁴) = 1.25 slots per element,
        // rounded up to whole segments (of 512 at this size) plus the two trailing ones.
        Assert.True(filter.SlotCount >= (int)(1.25 * filter.Count));
        Assert.True(filter.SlotCount <= (int)(1.25 * filter.Count) + 3 * 512);
        Assert.Equal(0, filter.SlotCount % 512); // whole power-of-two segments
    }

    [Fact]
    public void Sizing_IsSmallerThanAnXorFilter_OnlyForLargeSets()
    {
        // The documented crossover: the fuse layout needs fewer spare slots than an xor filter once the
        // set is large, and more when it is small.
        int[] large = Enumerable.Range(0, 200_000).ToArray();
        var fuseLarge = new BinaryFuseFilter<int, Int32Murmur3Hasher>(large);
        var xorLarge = new XorFilter<int, Int32Murmur3Hasher>(large);
        Assert.True(fuseLarge.SlotCount < xorLarge.SlotCount,
            $"fuse {fuseLarge.SlotCount} slots against xor {xorLarge.SlotCount} at 200,000 elements");

        int[] small = Enumerable.Range(0, 1_000).ToArray();
        var fuseSmall = new BinaryFuseFilter<int, Int32Murmur3Hasher>(small);
        var xorSmall = new XorFilter<int, Int32Murmur3Hasher>(small);
        Assert.True(fuseSmall.SlotCount > xorSmall.SlotCount,
            $"fuse {fuseSmall.SlotCount} slots against xor {xorSmall.SlotCount} at 1,000 elements");
    }

    [Fact]
    public void FixedProperties_ReportEightBitFingerprint()
    {
        var filter = new BinaryFuseFilter<int, Int32WangNaiveHasher>(new[] { 1 });
        Assert.Equal(8, BinaryFuseFilter<int, Int32WangNaiveHasher>.FingerprintBits);
        Assert.Equal(1.0 / 256, filter.FalsePositiveRate);
    }

    [Fact]
    public void BitsPerElement_IsNearTheBinaryFuseFloor_ForAMillionElements()
    {
        var filter = new BinaryFuseFilter<int, Int32Murmur3Hasher>(Enumerable.Range(0, 1_000_000).ToArray());
        // 1.125 slots per element at a million, so 9.0 bits; allow for rounding to whole segments.
        Assert.True(filter.BitsPerElement >= 9.0 && filter.BitsPerElement <= 9.2,
            $"bits/element {filter.BitsPerElement:F2} outside the expected binary-fuse band");
    }

    // ---------------------------------------------------------------
    //  Empty and single-element edge cases
    // ---------------------------------------------------------------

    [Fact]
    public void EmptySource_BuildsFilterThatReportsEverythingAbsent()
    {
        var filter = new BinaryFuseFilter<int, Int32WangNaiveHasher>(Array.Empty<int>());
        Assert.Equal(0, filter.Count);
        Assert.Equal(0d, filter.BitsPerElement);
        Assert.False(filter.Contains(0));
        Assert.False(filter.Contains(123));
    }

    [Fact]
    public void SingleElement_RoundTrips()
    {
        var filter = new BinaryFuseFilter<int, Int32WangNaiveHasher>(new[] { 12345 });
        Assert.Equal(1, filter.Count);
        Assert.True(filter.Contains(12345));
    }

    [Fact]
    public void NonCollectionSource_IsEnumeratedOnce()
    {
        // A lazy sequence is not an ICollection<T>; the constructor still consumes it correctly.
        IEnumerable<int> source = Enumerable.Range(0, 2000).Where(i => true);
        var filter = new BinaryFuseFilter<int, Int32Murmur3Hasher>(source);

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
            () => new BinaryFuseFilter<int, Int32WangNaiveHasher>(null!));
    }
}
