using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Unit coverage for <see cref="FrozenSortedMap{TKey, TValue}"/>: construction, exact lookup, the ordered
/// queries at both ends of the key domain, positional access and the views over keys and values.
///
/// <para>
/// The search itself is <see cref="FrozenSortedSet{T}"/>'s and is held to its oracle in that type's suites; what
/// is new here is that every value travels with its key through the sort, that a duplicate key is rejected
/// rather than silently resolved, and that the floor / predecessor arithmetic is right at the first and last
/// entry and next to the <c>TKey.MaxValue</c> padding.
/// </para>
/// </summary>
public class FrozenSortedMapTests
{
    // Unsorted, so a value that did not follow its key through the sort shows up as a wrong lookup.
    private static readonly KeyValuePair<int, string>[] Source =
    [
        new(30, "thirty"),
        new(-10, "minus ten"),
        new(20, "twenty"),
        new(0, "zero"),
    ];

    private static FrozenSortedMap<int, string> Map() => new(Source);

    // ---- Construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSourceIsNull()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => new FrozenSortedMap<int, string>(null!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldSortTheEntriesByKey_AndKeepEachValueWithItsKey()
    {
        FrozenSortedMap<int, string> map = Map();

        Assert.Equal(4, map.Count);
        Assert.Equal(new[] { -10, 0, 20, 30 }, map.Keys.AsSpan().ToArray());
        Assert.Equal(new[] { "minus ten", "zero", "twenty", "thirty" }, map.Values.ToArray());
        Assert.Equal("twenty", map[20]);
        Assert.Equal("minus ten", map[-10]);
    }

    [Fact]
    public void Constructor_ShouldBuildTheSameMap_FromCountedAndUncountedSources()
    {
        var counted = new FrozenSortedMap<int, string>(Source);
        var uncounted = new FrozenSortedMap<int, string>(Source.Select(static p => p));
        var fromDictionary = new FrozenSortedMap<int, string>(Source.ToDictionary(p => p.Key, p => p.Value));

        Assert.Equal(counted, uncounted);
        Assert.Equal(counted, fromDictionary);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_ShouldThrowArgumentException_OnADuplicateKey_FromBothSourcePaths(bool counted)
    {
        KeyValuePair<int, string>[] source = [new(5, "a"), new(1, "b"), new(5, "c")];
        IEnumerable<KeyValuePair<int, string>> input = counted ? source : source.Select(static p => p);

        ArgumentException ex = Assert.Throws<ArgumentException>(() => new FrozenSortedMap<int, string>(input));

        Assert.Equal("source", ex.ParamName);
        Assert.Contains("Key: 5", ex.Message);
    }

    [Fact]
    public void Constructor_ShouldAcceptAnEmptySource()
    {
        var map = new FrozenSortedMap<int, string>([]);

        Assert.Equal(0, map.Count);
        Assert.Empty(map);
        Assert.False(map.ContainsKey(0));
        Assert.False(map.TryGetFloor(int.MaxValue, out _));
        Assert.False(map.TryGetLowerBound(int.MinValue, out _));
        Assert.Equal(new Range(0, 0), map.GetIndexRange(int.MinValue, int.MaxValue));
        Assert.Throws<InvalidOperationException>(() => map.Min);
        Assert.Throws<InvalidOperationException>(() => map.Max);
    }

    [Fact]
    public void Constructor_ShouldCopyTheSource_SoTheCallersArrayCanBeReused()
    {
        KeyValuePair<int, int>[] source = [new(1, 10), new(2, 20)];
        var map = new FrozenSortedMap<int, int>(source);

        source[0] = new(1, 999);

        Assert.Equal(10, map[1]);
    }

    // ---- Exact lookup ------------------------------------------------------------------------------

    [Fact]
    public void Indexer_ShouldThrowKeyNotFoundException_ForAMissingKey()
    {
        FrozenSortedMap<int, string> map = Map();

        Assert.Throws<KeyNotFoundException>(() => map[5]);
        Assert.Throws<KeyNotFoundException>(() => map[int.MaxValue]);   // the padding value, not a key here
    }

    [Fact]
    public void TryGetValue_And_ContainsKey_ShouldReportPresentAndMissingKeys()
    {
        FrozenSortedMap<int, string> map = Map();

        Assert.True(map.TryGetValue(0, out string? zero));
        Assert.Equal("zero", zero);
        Assert.True(map.ContainsKey(30));

        Assert.False(map.TryGetValue(31, out string? missing));
        Assert.Null(missing);
        Assert.False(map.ContainsKey(-11));
        Assert.False(map.ContainsKey(int.MaxValue));
    }

    [Fact]
    public void Lookups_ShouldFindTKeyMaxValue_WhenItIsARealKey()
    {
        // MaxValue is also the padding that fills the last node; as a real key it must still be found, and it
        // is the last entry.
        var map = new FrozenSortedMap<int, int>([new(int.MaxValue, 1), new(int.MinValue, 2), new(0, 3)]);

        Assert.Equal(1, map[int.MaxValue]);
        Assert.Equal(new KeyValuePair<int, int>(int.MaxValue, 1), map.Max);
        Assert.Equal(new KeyValuePair<int, int>(int.MinValue, 2), map.Min);
        Assert.True(map.TryGetFloor(int.MaxValue, out KeyValuePair<int, int> floor));
        Assert.Equal(int.MaxValue, floor.Key);
        Assert.False(map.TryGetUpperBound(int.MaxValue, out _));
        Assert.Equal(2, map.CountLessThan(int.MaxValue));
    }

    [Fact]
    public void ContainsValue_ShouldScanEveryValue_IncludingNullAndDefault()
    {
        var map = new FrozenSortedMap<int, string?>([new(1, "one"), new(2, null)]);

        Assert.True(map.ContainsValue("one"));
        Assert.True(map.ContainsValue(null));
        Assert.False(map.ContainsValue("two"));

        var ints = new FrozenSortedMap<int, int>([new(1, 0), new(2, 7)]);
        Assert.True(ints.ContainsValue(0));
        Assert.False(new FrozenSortedMap<int, int>([new(1, 7)]).ContainsValue(0));
    }

    // ---- Ordered queries ---------------------------------------------------------------------------

    [Theory]
    [InlineData(int.MinValue, false, 0)]
    [InlineData(-11, false, 0)]
    [InlineData(-10, true, -10)]
    [InlineData(-9, true, -10)]
    [InlineData(19, true, 0)]
    [InlineData(20, true, 20)]
    [InlineData(30, true, 30)]
    [InlineData(int.MaxValue, true, 30)]
    public void TryGetFloor_ShouldReturnTheLastEntryAtOrBelowTheKey(int key, bool found, int expectedKey)
    {
        FrozenSortedMap<int, string> map = Map();

        Assert.Equal(found, map.TryGetFloor(key, out KeyValuePair<int, string> entry));
        if (found)
            Assert.Equal(new KeyValuePair<int, string>(expectedKey, map[expectedKey]), entry);
        else
            Assert.Equal(default, entry);
    }

    [Theory]
    [InlineData(int.MinValue, false, 0)]
    [InlineData(-10, false, 0)]
    [InlineData(-9, true, -10)]
    [InlineData(20, true, 0)]
    [InlineData(21, true, 20)]
    [InlineData(int.MaxValue, true, 30)]
    public void TryGetPredecessor_ShouldReturnTheLastEntryStrictlyBelowTheKey(int key, bool found, int expectedKey)
    {
        FrozenSortedMap<int, string> map = Map();

        Assert.Equal(found, map.TryGetPredecessor(key, out KeyValuePair<int, string> entry));
        Assert.Equal(found ? expectedKey : 0, entry.Key);
    }

    [Theory]
    [InlineData(int.MinValue, true, -10)]
    [InlineData(-10, true, -10)]
    [InlineData(1, true, 20)]
    [InlineData(30, true, 30)]
    [InlineData(31, false, 0)]
    [InlineData(int.MaxValue, false, 0)]
    public void TryGetLowerBound_ShouldReturnTheFirstEntryAtOrAboveTheKey(int key, bool found, int expectedKey)
    {
        FrozenSortedMap<int, string> map = Map();

        Assert.Equal(found, map.TryGetLowerBound(key, out KeyValuePair<int, string> entry));
        Assert.Equal(found ? expectedKey : 0, entry.Key);
        Assert.Equal(found ? map[expectedKey] : null, entry.Value);
    }

    [Theory]
    [InlineData(int.MinValue, true, -10)]
    [InlineData(-10, true, 0)]
    [InlineData(20, true, 30)]
    [InlineData(30, false, 0)]
    [InlineData(int.MaxValue, false, 0)]
    public void TryGetUpperBound_ShouldReturnTheFirstEntryStrictlyAboveTheKey(int key, bool found, int expectedKey)
    {
        FrozenSortedMap<int, string> map = Map();

        Assert.Equal(found, map.TryGetUpperBound(key, out KeyValuePair<int, string> entry));
        Assert.Equal(found ? expectedKey : 0, entry.Key);
    }

    [Fact]
    public void CountLessThan_And_IndexOfKey_ShouldReportPositionsInKeyOrder()
    {
        FrozenSortedMap<int, string> map = Map();

        Assert.Equal(0, map.CountLessThan(int.MinValue));
        Assert.Equal(2, map.CountLessThan(20));
        Assert.Equal(3, map.CountLessThan(21));
        Assert.Equal(4, map.CountLessThan(int.MaxValue));

        Assert.Equal(2, map.IndexOfKey(20));
        Assert.Equal(-1, map.IndexOfKey(21));
    }

    [Fact]
    public void GetIndexRange_ShouldSliceTheKeysAndValuesInTheHalfOpenRange()
    {
        FrozenSortedMap<int, string> map = Map();

        Range range = map.GetIndexRange(0, 30);

        Assert.Equal(new[] { 0, 20 }, map.Keys.AsSpan()[range].ToArray());
        Assert.Equal(new[] { "zero", "twenty" }, map.Values[range].ToArray());
        Assert.Equal(new Range(1, 1), map.GetIndexRange(-5, -5));
        Assert.Equal(new Range(0, 4), map.GetIndexRange(int.MinValue, int.MaxValue));
    }

    [Fact]
    public void GetIndexRange_ShouldThrow_WhenTheBoundsAreReversed()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => Map().GetIndexRange(10, 9));

        Assert.Equal("toExclusive", ex.ParamName);
    }

    // ---- Positional access and views ---------------------------------------------------------------

    [Fact]
    public void GetKeyAtIndex_And_GetValueAtIndex_ShouldReadThePosition_AndRejectOutOfRange()
    {
        FrozenSortedMap<int, string> map = Map();

        Assert.Equal(20, map.GetKeyAtIndex(2));
        Assert.Equal("twenty", map.GetValueAtIndex(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetKeyAtIndex(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetKeyAtIndex(-1));
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.GetValueAtIndex(4));
        Assert.Equal("index", ex.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetValueAtIndex(-1));
    }

    [Fact]
    public void Keys_ShouldBeTheSameFrozenSortedSet_OnEveryRead()
    {
        FrozenSortedMap<int, string> map = Map();

        Assert.Same(map.Keys, map.Keys);
        Assert.True(map.Keys.Contains(30));
        Assert.Equal(4, map.Keys.Count);
    }

    [Fact]
    public void InterfaceValues_ShouldNotExposeTheBackingArray()
    {
        IReadOnlyDictionary<int, string> ro = Map();

        IEnumerable<string> values = ro.Values;

        Assert.IsNotType<string[]>(values);
        Assert.Equal(new[] { "minus ten", "zero", "twenty", "thirty" }, values);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)values)[0] = "changed");
    }

    // ---- Depth: the multi-level trees -----------------------------------------------------------

    [Theory]
    [InlineData(16)]       // one leaf
    [InlineData(17)]       // two leaves, one internal level
    [InlineData(273)]      // the second internal level
    [InlineData(5_000)]    // the third
    public void Lookups_ShouldFindEveryEntry_AcrossTreeDepths(int count)
    {
        // Odd keys only, so every even probe is a miss that falls between two entries.
        var map = new FrozenSortedMap<int, long>(
            Enumerable.Range(0, count).Reverse().Select(i => new KeyValuePair<int, long>((2 * i) + 1, i * 7L)));

        for (int i = 0; i < count; i++)
        {
            int key = (2 * i) + 1;
            Assert.Equal(i * 7L, map[key]);
            Assert.True(map.TryGetFloor(key + 1, out KeyValuePair<int, long> floor));
            Assert.Equal(key, floor.Key);
            Assert.Equal(i * 7L, floor.Value);
            Assert.False(map.ContainsKey(key - 1));
        }
    }

    [Fact]
    public void Lookups_ShouldWork_ForAScalarSearchKeyType()
    {
        // Int128 has no hardware Vector128 support, so the node scan is the scalar loop.
        var map = new FrozenSortedMap<Int128, string>(
            [new(Int128.MaxValue, "max"), new(Int128.MinValue, "min"), new(Int128.Zero, "zero")]);

        Assert.Equal("max", map[Int128.MaxValue]);
        Assert.True(map.TryGetFloor(-1, out KeyValuePair<Int128, string> floor));
        Assert.Equal("min", floor.Value);
    }
}
