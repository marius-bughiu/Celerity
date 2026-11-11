using System.Numerics;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Unit coverage for <see cref="FrozenSortedSet{T}"/>: construction (sort, dedupe, the counted and uncounted
/// source paths), the node size and depth of the layout, the exact <see cref="FrozenSortedSet{T}.MemoryUsageInBytes"/>,
/// every query at the edges a lower bound gets wrong, and every supported key type. Enumeration lives in
/// <see cref="FrozenSortedSetEnumerationTests"/>, the set algebra in <see cref="FrozenSortedSetSetAlgebraTests"/>
/// and the randomized reconciliation in <see cref="FrozenSortedSetDifferentialTests"/>.
///
/// <para>
/// Two edges get particular attention. <c>T.MaxValue</c> is both a legal key and the padding that fills the last
/// node, so a set that holds it, a set that does not, and a set whose last node is exactly full must all answer
/// the same way the sorted array would. And the internal levels only exist once the keys outgrow one node, with a
/// third level past <c>16 * 17 = 272</c> <see cref="int"/> keys, so the tests cross each of those thresholds.
/// </para>
/// </summary>
public class FrozenSortedSetTests
{
    // ── Construction ────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldThrow_WhenSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new FrozenSortedSet<int>(null!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldSortAndDedupe_TheSource()
    {
        var set = new FrozenSortedSet<int>([5, -3, 5, 0, 12, -3, -3, 7]);

        Assert.Equal(5, set.Count);
        Assert.Equal(new[] { -3, 0, 5, 7, 12 }, set.AsSpan().ToArray());
    }

    [Fact]
    public void Constructor_ShouldCopyTheSource()
    {
        int[] source = [1, 2, 3];
        var set = new FrozenSortedSet<int>(source);

        source[0] = 99;

        Assert.Equal(1, set[0]);
        Assert.False(set.Contains(99));
    }

    [Fact]
    public void Constructor_ShouldAcceptAnUncountedSource()
    {
        var set = new FrozenSortedSet<int>(Enumerable.Range(0, 40).Select(i => 39 - i));

        Assert.Equal(Enumerable.Range(0, 40), set.AsSpan().ToArray());
    }

    [Fact]
    public void Constructor_ShouldShrinkTheLeaves_WhenDuplicatesLeaveFewerNodes()
    {
        // 32 copies of one key are copied into two nodes and dedupe to one key, so the leaf array is resized
        // down to a single node: 16 ints, 64 bytes, and no internal level.
        var set = new FrozenSortedSet<int>(Enumerable.Repeat(5, 32).ToArray());

        Assert.Equal(1, set.Count);
        Assert.Equal(1, set.Depth);
        Assert.Equal(64, set.MemoryUsageInBytes);
        Assert.True(set.Contains(5));
    }

    [Fact]
    public void Constructor_ShouldKeepTheLeaves_WhenACountedSourceHasNoDuplicates()
    {
        // 16 distinct keys fill exactly one node, so no resize and no padding.
        var set = new FrozenSortedSet<int>(Enumerable.Range(100, 16).ToArray());

        Assert.Equal(16, set.Count);
        Assert.Equal(1, set.Depth);
        Assert.Equal(64, set.MemoryUsageInBytes);
    }

    // ── The empty set ───────────────────────────────────────────────────────

    [Fact]
    public void EmptySet_ShouldAnswerEveryQuery()
    {
        var set = new FrozenSortedSet<int>([]);

        Assert.Equal(0, set.Count);
        Assert.Equal(1, set.Depth);
        Assert.Equal(64, set.MemoryUsageInBytes);
        Assert.True(set.AsSpan().IsEmpty);
        Assert.False(set.Contains(0));
        Assert.False(set.Contains(int.MaxValue));
        Assert.Equal(-1, set.IndexOf(int.MaxValue));
        Assert.Equal(0, set.CountLessThan(int.MaxValue));
        Assert.Equal(0, set.CountLessThan(int.MinValue));
        Assert.False(set.TryGetLowerBound(int.MinValue, out int lower));
        Assert.Equal(0, lower);
        Assert.False(set.TryGetUpperBound(int.MinValue, out int upper));
        Assert.Equal(0, upper);
        Assert.True(set.SliceRange(int.MinValue, int.MaxValue).IsEmpty);
        Assert.Throws<InvalidOperationException>(() => set.Min);
        Assert.Throws<InvalidOperationException>(() => set.Max);
        Assert.Throws<ArgumentOutOfRangeException>(() => set[0]);
    }

    // ── Layout: NodeSize, Depth, MemoryUsageInBytes ─────────────────────────

    [Fact]
    public void NodeSize_ShouldBeOneCacheLineOfKeys()
    {
        Assert.Equal(64, FrozenSortedSet<byte>.NodeSize);
        Assert.Equal(64, FrozenSortedSet<sbyte>.NodeSize);
        Assert.Equal(32, FrozenSortedSet<short>.NodeSize);
        Assert.Equal(32, FrozenSortedSet<ushort>.NodeSize);
        Assert.Equal(32, FrozenSortedSet<char>.NodeSize);
        Assert.Equal(16, FrozenSortedSet<int>.NodeSize);
        Assert.Equal(16, FrozenSortedSet<uint>.NodeSize);
        Assert.Equal(8, FrozenSortedSet<long>.NodeSize);
        Assert.Equal(8, FrozenSortedSet<ulong>.NodeSize);
        Assert.Equal(64 / IntPtr.Size, FrozenSortedSet<nint>.NodeSize);
        Assert.Equal(4, FrozenSortedSet<Int128>.NodeSize);
        Assert.Equal(4, FrozenSortedSet<UInt128>.NodeSize);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(16, 1)]
    [InlineData(17, 2)]
    [InlineData(272, 2)]
    [InlineData(273, 3)]
    [InlineData(4624, 3)]
    [InlineData(4625, 4)]
    public void Depth_ShouldGrowAtEachNodeThreshold_WhenTheKeysAreInt(int count, int depth)
    {
        var set = new FrozenSortedSet<int>(Enumerable.Range(0, count).ToArray());

        Assert.Equal(depth, set.Depth);
        FrozenSortedSetOracle.AssertMatches(set, Enumerable.Range(0, count).ToArray());
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(20, 2)]
    [InlineData(21, 3)]
    [InlineData(101, 4)]
    public void Depth_ShouldGrowAtEachNodeThreshold_WhenTheKeysAreInt128(int count, int depth)
    {
        Int128[] keys = Enumerable.Range(0, count).Select(i => (Int128)i * 1_000_000_007).ToArray();
        var set = new FrozenSortedSet<Int128>(keys);

        Assert.Equal(depth, set.Depth);
        FrozenSortedSetOracle.AssertMatches(set, keys);
    }

    [Fact]
    public void Depth_ShouldBeTwo_WhenEveryByteIsAKey()
    {
        // 256 keys in 64-key nodes: four leaves under one root.
        byte[] keys = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var set = new FrozenSortedSet<byte>(keys);

        Assert.Equal(2, set.Depth);
        FrozenSortedSetOracle.AssertMatches(set, keys);
    }

    [Theory]
    [InlineData(0, 64)]                                       // one padded node
    [InlineData(17, (32 * 4) + (16 * 4) + 4)]                 // two leaves, one root, one level start
    [InlineData(273, (288 * 4) + (3 * 16 * 4) + (2 * 4))]     // 18 leaves, two then one internal node
    public void MemoryUsageInBytes_ShouldCountLeavesInternalLevelsAndLevelStarts(int count, long expected)
    {
        var set = new FrozenSortedSet<int>(Enumerable.Range(0, count).ToArray());

        Assert.Equal(expected, set.MemoryUsageInBytes);
    }

    [Fact]
    public void MemoryUsageInBytes_ShouldScaleWithTheKeyWidth()
    {
        // Nine longs: two 8-key leaves, one 8-key root, one level start.
        var set = new FrozenSortedSet<long>(Enumerable.Range(0, 9).Select(i => (long)i).ToArray());

        Assert.Equal((16 * 8) + (8 * 8) + 4, set.MemoryUsageInBytes);
    }

    // ── Queries ─────────────────────────────────────────────────────────────

    [Fact]
    public void Indexer_ShouldThrow_WhenTheIndexIsOutOfRange()
    {
        var set = new FrozenSortedSet<int>([1, 2, 3]);

        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => set[-1]).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => set[3]).ParamName);
        Assert.Equal(3, set[2]);
    }

    [Fact]
    public void MinAndMax_ShouldReturnTheEnds()
    {
        var set = new FrozenSortedSet<int>([40, -7, 12]);

        Assert.Equal(-7, set.Min);
        Assert.Equal(40, set.Max);
    }

    [Fact]
    public void Queries_ShouldAnswerBetweenAndOnKeys()
    {
        var set = new FrozenSortedSet<int>([10, 20, 30]);

        Assert.True(set.Contains(20));
        Assert.False(set.Contains(21));
        Assert.Equal(1, set.IndexOf(20));
        Assert.Equal(-1, set.IndexOf(15));
        Assert.Equal(-1, set.IndexOf(31));

        Assert.Equal(0, set.CountLessThan(10));
        Assert.Equal(1, set.CountLessThan(11));
        Assert.Equal(3, set.CountLessThan(31));

        Assert.True(set.TryGetLowerBound(20, out int bound));
        Assert.Equal(20, bound);
        Assert.True(set.TryGetLowerBound(21, out bound));
        Assert.Equal(30, bound);
        Assert.False(set.TryGetLowerBound(31, out bound));
        Assert.Equal(0, bound);

        Assert.True(set.TryGetUpperBound(20, out bound));
        Assert.Equal(30, bound);
        Assert.True(set.TryGetUpperBound(int.MinValue, out bound));
        Assert.Equal(10, bound);
        Assert.False(set.TryGetUpperBound(30, out bound));
        Assert.Equal(0, bound);
    }

    [Fact]
    public void SliceRange_ShouldReturnTheHalfOpenRange()
    {
        var set = new FrozenSortedSet<int>(Enumerable.Range(0, 1000).Select(i => i * 3).ToArray());

        Assert.Equal(new[] { 9, 12, 15 }, set.SliceRange(9, 18).ToArray());
        Assert.Equal(new[] { 9, 12, 15, 18 }, set.SliceRange(8, 19).ToArray());
        Assert.True(set.SliceRange(10, 12).IsEmpty);
        Assert.True(set.SliceRange(12, 12).IsEmpty);
        Assert.Equal(1000, set.SliceRange(int.MinValue, int.MaxValue).Length);
    }

    [Fact]
    public void SliceRange_ShouldThrow_WhenTheBoundsAreReversed()
    {
        var set = new FrozenSortedSet<int>([1, 2, 3]);

        var ex = Assert.Throws<ArgumentException>(() => set.SliceRange(3, 2));

        Assert.Equal("toExclusive", ex.ParamName);
    }

    // ── MinValue and MaxValue as real keys ──────────────────────────────────

    [Fact]
    public void MaxValue_ShouldBeFound_WhenItIsAKey_AlongsidePadding()
    {
        var set = new FrozenSortedSet<int>([int.MaxValue, 5, int.MinValue]);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(int.MaxValue));
        Assert.True(set.Contains(int.MinValue));
        Assert.Equal(2, set.IndexOf(int.MaxValue));
        Assert.Equal(0, set.IndexOf(int.MinValue));
        Assert.Equal(2, set.CountLessThan(int.MaxValue));
        Assert.Equal(0, set.CountLessThan(int.MinValue));
        Assert.Equal(int.MinValue, set.Min);
        Assert.Equal(int.MaxValue, set.Max);

        Assert.True(set.TryGetLowerBound(6, out int bound));
        Assert.Equal(int.MaxValue, bound);
        Assert.True(set.TryGetUpperBound(int.MaxValue - 1, out bound));
        Assert.Equal(int.MaxValue, bound);
        Assert.False(set.TryGetUpperBound(int.MaxValue, out bound));
        Assert.Equal(0, bound);

        // A half-open range can never include MaxValue itself.
        Assert.Equal(new[] { int.MinValue, 5 }, set.SliceRange(int.MinValue, int.MaxValue).ToArray());
    }

    [Fact]
    public void MaxValue_ShouldNotBeFound_WhenOnlyThePaddingHoldsIt()
    {
        var set = new FrozenSortedSet<int>([1, 2, 3]);

        Assert.False(set.Contains(int.MaxValue));
        Assert.Equal(-1, set.IndexOf(int.MaxValue));
        Assert.Equal(3, set.CountLessThan(int.MaxValue));
        Assert.False(set.TryGetLowerBound(int.MaxValue, out _));
    }

    [Fact]
    public void MaxValue_ShouldBeFound_WhenItEndsAnExactlyFullTree()
    {
        // 272 keys fill 17 leaves under one root exactly, so there is no padding at all and MaxValue is the
        // last real key of the last leaf.
        int[] keys = [.. Enumerable.Range(0, 271), int.MaxValue];
        var set = new FrozenSortedSet<int>(keys);

        Assert.Equal(2, set.Depth);
        Assert.True(set.Contains(int.MaxValue));
        Assert.Equal(271, set.IndexOf(int.MaxValue));
        FrozenSortedSetOracle.AssertMatches(set, keys);
    }

    [Fact]
    public void MaxValueRuns_ShouldBeFound_AcrossSeveralLevels()
    {
        // The top of a deep tree sits against MaxValue: separators equal to MaxValue are real keys there.
        int[] keys = Enumerable.Range(0, 5000).Select(i => int.MaxValue - (i * 2)).ToArray();

        FrozenSortedSetOracle.AssertMatches(keys);
    }

    // ── Every key type ──────────────────────────────────────────────────────

    public static TheoryData<int, int> TypeCases()
    {
        var data = new TheoryData<int, int>();
        foreach (int length in new[] { 0, 1, 15, 16, 17, 100, 273, 1000 })
        {
            data.Add(length, 8);       // clustered: dense keys and many repeats
            data.Add(length, 128);     // spread over the whole domain
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void Int_ShouldMatchTheOracle(int length, int spreadBits) => Check<int>(length, spreadBits);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void Long_ShouldMatchTheOracle(int length, int spreadBits) => Check<long>(length, spreadBits);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void UInt_ShouldMatchTheOracle(int length, int spreadBits) => Check<uint>(length, spreadBits);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void ULong_ShouldMatchTheOracle(int length, int spreadBits) => Check<ulong>(length, spreadBits);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void Short_ShouldMatchTheOracle(int length, int spreadBits) => Check<short>(length, spreadBits);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void UShort_ShouldMatchTheOracle(int length, int spreadBits) => Check<ushort>(length, spreadBits);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void Byte_ShouldMatchTheOracle(int length, int spreadBits) => Check<byte>(length, spreadBits);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void SByte_ShouldMatchTheOracle(int length, int spreadBits) => Check<sbyte>(length, spreadBits);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void NInt_ShouldMatchTheOracle(int length, int spreadBits) => Check<nint>(length, spreadBits);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void Char_ShouldMatchTheOracle(int length, int spreadBits) => Check<char>(length, spreadBits);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void Int128_ShouldMatchTheOracle(int length, int spreadBits) => Check<Int128>(length, spreadBits);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public void UInt128_ShouldMatchTheOracle(int length, int spreadBits) => Check<UInt128>(length, spreadBits);

    [Fact]
    public void EdgeKeys_ShouldBeFound_ForEveryKeyType()
    {
        CheckEdges<int>();
        CheckEdges<long>();
        CheckEdges<uint>();
        CheckEdges<ulong>();
        CheckEdges<short>();
        CheckEdges<ushort>();
        CheckEdges<byte>();
        CheckEdges<sbyte>();
        CheckEdges<nint>();
        CheckEdges<char>();
        CheckEdges<Int128>();
        CheckEdges<UInt128>();
    }

    private static void Check<T>(int length, int spreadBits)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        // The origin sits just below the middle of the domain for signed types, so clustered keys straddle zero.
        T[] keys = FrozenSortedSetOracle.RandomKeys<T>(length, spreadBits, unchecked((ulong)-100), 10, (uint)(length * 31 + spreadBits));
        FrozenSortedSetOracle.AssertMatches(keys);
    }

    private static void CheckEdges<T>()
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        T[] keys = [T.MaxValue, T.MinValue, T.Zero, T.One, T.MaxValue - T.One, T.MinValue + T.One, T.MaxValue];
        FrozenSortedSet<T> set = FrozenSortedSetOracle.AssertMatches(keys);

        Assert.Equal(T.MinValue, set.Min);
        Assert.Equal(T.MaxValue, set.Max);
        Assert.True(set.Contains(T.MaxValue));
        Assert.True(set.Contains(T.MinValue));
        Assert.False(set.TryGetUpperBound(T.MaxValue, out _));

        // A full node whose last slot is MaxValue: no padding to lean on.
        int nodeSize = FrozenSortedSet<T>.NodeSize;
        T[] full = new T[nodeSize];
        for (int i = 0; i < nodeSize; i++)
            full[i] = T.MaxValue - T.CreateTruncating(i);
        FrozenSortedSetOracle.AssertMatches(full);
    }
}
