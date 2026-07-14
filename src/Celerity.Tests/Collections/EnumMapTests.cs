using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #263: EnumMap<TEnum, TValue>, a dense array-backed dictionary specialized for enum keys —
// the .NET analogue of Java's EnumMap and the dictionary counterpart of EnumSet<TEnum> (#259).
// A lookup is a direct array index (no hash, no probe chain); presence is tracked in a parallel
// occupancy bit vector so a key mapped to default(TValue) is distinct from an absent key.
//
// EnumMap is generic over `TEnum : struct, Enum`, so — exactly like EnumSet — it cannot slot into
// the int-keyed cross-collection shared dictionary suites (ConstructorValidationTests,
// IEnumerableConstructorTests, ReadOnlyDictionaryInterfaceTests, LoadFactorBoundaryTests, ...):
// `int` is not an enum, and EnumMap has no hasher, load factor, probe chain, or capacity
// constructor anyway. Its equivalent coverage lives here and in EnumMapEnumerationTests, using the
// real enums declared in EnumSetTests.cs (EnumSetColor / EnumSetWide / EnumSetByte / ... — same
// namespace).

public class EnumMapTests
{
    [Fact]
    public void Add_ThenIndexer_ReturnsValue()
    {
        var map = new EnumMap<EnumSetColor, string>();
        map.Add(EnumSetColor.Blue, "blue");

        Assert.Equal("blue", map[EnumSetColor.Blue]);
        Assert.Single(map);
        Assert.True(map.ContainsKey(EnumSetColor.Blue));
    }

    [Fact]
    public void Add_Throws_OnDuplicateKey()
    {
        var map = new EnumMap<EnumSetColor, int>();
        map.Add(EnumSetColor.Green, 1);

        var ex = Assert.Throws<ArgumentException>(() => map.Add(EnumSetColor.Green, 2));
        Assert.Contains("Green", ex.Message);
        Assert.Equal(1, map[EnumSetColor.Green]); // unchanged
        Assert.Single(map);
    }

    [Fact]
    public void TryAdd_ReturnsTrueThenFalse()
    {
        var map = new EnumMap<EnumSetColor, int>();

        Assert.True(map.TryAdd(EnumSetColor.Red, 10));
        Assert.False(map.TryAdd(EnumSetColor.Red, 20));
        Assert.Equal(10, map[EnumSetColor.Red]); // first write wins
        Assert.Single(map);
    }

    [Fact]
    public void Indexer_Get_ThrowsKeyNotFound_ForAbsentKey()
    {
        var map = new EnumMap<EnumSetColor, int>();
        map.Add(EnumSetColor.Red, 1);

        Assert.Throws<KeyNotFoundException>(() => map[EnumSetColor.Magenta]);
    }

    [Fact]
    public void Indexer_Set_AddsThenOverwrites()
    {
        var map = new EnumMap<EnumSetColor, int>();

        map[EnumSetColor.Cyan] = 1; // add
        Assert.Single(map);
        Assert.Equal(1, map[EnumSetColor.Cyan]);

        map[EnumSetColor.Cyan] = 99; // overwrite
        Assert.Single(map);
        Assert.Equal(99, map[EnumSetColor.Cyan]);
    }

    [Fact]
    public void ZeroValuedKey_IsAnOrdinaryKey()
    {
        // EnumSetColor.Red == 0 (bit 0). Unlike the hash-table dictionaries there is no out-of-band
        // zero-key slot; bit 0 is just a bit in the occupancy vector.
        var map = new EnumMap<EnumSetColor, string>();
        Assert.False(map.ContainsKey(EnumSetColor.Red));

        map.Add(EnumSetColor.Red, "zero");
        Assert.True(map.ContainsKey(EnumSetColor.Red));
        Assert.Equal("zero", map[EnumSetColor.Red]);
        Assert.Single(map);

        Assert.True(map.Remove(EnumSetColor.Red));
        Assert.False(map.ContainsKey(EnumSetColor.Red));
        Assert.Empty(map);
    }

    [Fact]
    public void DefaultValue_IsDistinctFromAbsent()
    {
        // The whole point of the parallel occupancy vector: a key mapped to default(TValue) (0 for
        // int, null for a reference type) must be reported present, not absent.
        var map = new EnumMap<EnumSetColor, int>();
        map.Add(EnumSetColor.Green, 0); // default(int)

        Assert.True(map.ContainsKey(EnumSetColor.Green));
        Assert.True(map.TryGetValue(EnumSetColor.Green, out int v));
        Assert.Equal(0, v);
        Assert.Single(map);

        var refMap = new EnumMap<EnumSetColor, string?>();
        refMap.Add(EnumSetColor.Blue, null); // default(string?)
        Assert.True(refMap.ContainsKey(EnumSetColor.Blue));
        Assert.True(refMap.TryGetValue(EnumSetColor.Blue, out string? rv));
        Assert.Null(rv);
        Assert.Single(refMap);
    }

    [Fact]
    public void TryGetValue_ReturnsFalse_ForAbsentKey()
    {
        var map = new EnumMap<EnumSetColor, int>();
        map.Add(EnumSetColor.Red, 5);

        Assert.False(map.TryGetValue(EnumSetColor.Yellow, out int v));
        Assert.Equal(default, v);
    }

    [Fact]
    public void Remove_DeletesEntry_AndReturnsValue()
    {
        var map = new EnumMap<EnumSetColor, string>();
        map.Add(EnumSetColor.Cyan, "cyan");

        Assert.True(map.Remove(EnumSetColor.Cyan, out string? removed));
        Assert.Equal("cyan", removed);
        Assert.False(map.ContainsKey(EnumSetColor.Cyan));
        Assert.Empty(map);
    }

    [Fact]
    public void Remove_ReturnsFalse_ForAbsentKey()
    {
        var map = new EnumMap<EnumSetColor, int>();
        map.Add(EnumSetColor.Blue, 1);

        Assert.False(map.Remove(EnumSetColor.Yellow, out int v));
        Assert.Equal(default, v);
        Assert.Single(map);
    }

    [Fact]
    public void Remove_ThenReAdd_ClearsOldValue()
    {
        // A removed reference slot is cleared, so re-adding does not resurrect the old value and
        // the old object is no longer reachable through the map.
        var map = new EnumMap<EnumSetColor, string>();
        map.Add(EnumSetColor.Red, "first");
        map.Remove(EnumSetColor.Red);
        Assert.False(map.TryGetValue(EnumSetColor.Red, out _));

        map.Add(EnumSetColor.Red, "second");
        Assert.Equal("second", map[EnumSetColor.Red]);
    }

    [Fact]
    public void Clear_EmptiesTheMap()
    {
        var map = new EnumMap<EnumSetColor, int>();
        map.Add(EnumSetColor.Red, 1);
        map.Add(EnumSetColor.Blue, 2);

        map.Clear();

        Assert.Empty(map);
        Assert.False(map.ContainsKey(EnumSetColor.Red));
        Assert.False(map.ContainsKey(EnumSetColor.Blue));
    }

    [Fact]
    public void Clear_OnEmptyMap_IsNoOp()
    {
        var map = new EnumMap<EnumSetColor, int>();
        map.Clear();
        Assert.Empty(map);
    }

    [Fact]
    public void MultiWordEnum_StoresAcrossWords()
    {
        // EnumSetWide spans 3 words (max value 130). Exercise keys in each word.
        var map = new EnumMap<EnumSetWide, int>
        {
            [EnumSetWide.Zero] = 0,       // word 0, bit 0
            [EnumSetWide.WordEdge] = 63,  // word 0, bit 63
            [EnumSetWide.NextWord] = 64,  // word 1, bit 0
            [EnumSetWide.High] = 65,      // word 1, bit 1
            [EnumSetWide.Top] = 130,      // word 2, bit 2
        };

        Assert.Equal(5, map.Count);
        Assert.Equal(63, map[EnumSetWide.WordEdge]);
        Assert.Equal(64, map[EnumSetWide.NextWord]);
        Assert.Equal(130, map[EnumSetWide.Top]);
        Assert.False(map.ContainsKey(EnumSetWide.One));

        Assert.True(map.Remove(EnumSetWide.NextWord));
        Assert.False(map.ContainsKey(EnumSetWide.NextWord));
        Assert.Equal(4, map.Count);
    }

    [Fact]
    public void ContainsValue_FindsPresentValueIncludingDefault()
    {
        var map = new EnumMap<EnumSetColor, int>
        {
            [EnumSetColor.Red] = 0,
            [EnumSetColor.Green] = 42,
        };

        Assert.True(map.ContainsValue(0));   // default(int), and genuinely present
        Assert.True(map.ContainsValue(42));
        Assert.False(map.ContainsValue(99));
    }

    [Fact]
    public void ContainsValue_OnEmptyMap_ReturnsFalse()
    {
        var map = new EnumMap<EnumSetColor, int>();
        Assert.False(map.ContainsValue(0));
    }

    [Fact]
    public void SourceConstructor_CopiesPairs()
    {
        var map = new EnumMap<EnumSetColor, int>(new[]
        {
            new KeyValuePair<EnumSetColor, int>(EnumSetColor.Red, 1),
            new KeyValuePair<EnumSetColor, int>(EnumSetColor.Blue, 2),
        });

        Assert.Equal(2, map.Count);
        Assert.Equal(1, map[EnumSetColor.Red]);
        Assert.Equal(2, map[EnumSetColor.Blue]);
    }

    [Fact]
    public void SourceConstructor_Throws_OnDuplicateKey()
    {
        Assert.Throws<ArgumentException>(() => new EnumMap<EnumSetColor, int>(new[]
        {
            new KeyValuePair<EnumSetColor, int>(EnumSetColor.Red, 1),
            new KeyValuePair<EnumSetColor, int>(EnumSetColor.Red, 2),
        }));
    }

    [Fact]
    public void SourceConstructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new EnumMap<EnumSetColor, int>(null!));
    }

    [Fact]
    public void SourceConstructor_FromAnotherEnumMap_CopiesAndIsIndependent()
    {
        var source = new EnumMap<EnumSetWide, int>
        {
            [EnumSetWide.Zero] = 1,
            [EnumSetWide.Top] = 2,
            [EnumSetWide.High] = 3,
        };
        var copy = new EnumMap<EnumSetWide, int>(source);

        Assert.Equal(source.Count, copy.Count);
        Assert.Equal(2, copy[EnumSetWide.Top]);

        // Independence: mutating the copy does not affect the source.
        copy[EnumSetWide.One] = 9;
        Assert.False(source.ContainsKey(EnumSetWide.One));
        copy[EnumSetWide.Top] = 99;
        Assert.Equal(2, source[EnumSetWide.Top]);
    }

    [Fact]
    public void NegativeEnum_IsUnsupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() => new EnumMap<EnumSetNegative, int>());
        Assert.Contains("negative", ex.Message);
    }

    [Fact]
    public void SparseEnum_IsUnsupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() => new EnumMap<EnumSetSparse, int>());
        Assert.Contains("range", ex.Message);
    }

    [Fact]
    public void EmptyEnum_IsSupportedButHoldsNothing()
    {
        var map = new EnumMap<EnumSetEmpty, int>();
        Assert.Empty(map);
        Assert.False(map.ContainsKey(default));
    }

    [Fact]
    public void OutOfRangeKey_WritesThrow_ReadsReportAbsent()
    {
        var map = new EnumMap<EnumSetColor, int>();
        var bad = (EnumSetColor)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => map.Add(bad, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.TryAdd(bad, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map[bad] = 1);

        Assert.Throws<KeyNotFoundException>(() => map[bad]);
        Assert.False(map.ContainsKey(bad));
        Assert.False(map.TryGetValue(bad, out _));
        Assert.False(map.Remove(bad));
    }

    [Fact]
    public void OutOfRangeKey_InSourceConstructor_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnumMap<EnumSetColor, int>(new[]
        {
            new KeyValuePair<EnumSetColor, int>(EnumSetColor.Red, 1),
            new KeyValuePair<EnumSetColor, int>((EnumSetColor)500, 2),
        }));
    }

    [Fact]
    public void ByteBackedEnum_RoundTrips()
    {
        var map = new EnumMap<EnumSetByte, string>();
        foreach (EnumSetByte value in Enum.GetValues<EnumSetByte>())
            map.Add(value, value.ToString());

        Assert.Equal(3, map.Count);
        Assert.Equal("C", map[EnumSetByte.C]); // C == 200
    }

    [Fact]
    public void LongBackedEnum_RoundTrips()
    {
        var map = new EnumMap<EnumSetLong, int>
        {
            [EnumSetLong.L0] = 0,
            [EnumSetLong.L1] = 5,
            [EnumSetLong.L2] = 100,
        };

        Assert.Equal(3, map.Count);
        Assert.Equal(100, map[EnumSetLong.L2]);
    }

    [Fact]
    public void ULongBackedEnum_RoundTrips()
    {
        var map = new EnumMap<EnumSetULong, int>
        {
            [EnumSetULong.U0] = 0,
            [EnumSetULong.U1] = 9,
            [EnumSetULong.U2] = 300,
        };

        Assert.Equal(3, map.Count);
        Assert.Equal(300, map[EnumSetULong.U2]);
        Assert.False(map.ContainsKey((EnumSetULong)1));
    }

    // ── Keys / Values views ───────────────────────────────────────────────────

    [Fact]
    public void KeysView_YieldsKeysInAscendingOrder()
    {
        var map = new EnumMap<EnumSetWide, int>
        {
            [EnumSetWide.Top] = 3,
            [EnumSetWide.Zero] = 1,
            [EnumSetWide.NextWord] = 2,
        };

        Assert.Equal(3, map.Keys.Count);
        Assert.Equal(
            new[] { EnumSetWide.Zero, EnumSetWide.NextWord, EnumSetWide.Top },
            map.Keys.ToArray());
    }

    [Fact]
    public void ValuesView_YieldsValuesOrderedByAscendingKey()
    {
        var map = new EnumMap<EnumSetWide, int>
        {
            [EnumSetWide.Top] = 30,      // key 130
            [EnumSetWide.Zero] = 10,     // key 0
            [EnumSetWide.NextWord] = 20, // key 64
        };

        Assert.Equal(3, map.Values.Count);
        Assert.Equal(new[] { 10, 20, 30 }, map.Values.ToArray());
    }

    // ── IReadOnlyDictionary<TEnum, TValue?> surface ───────────────────────────

    [Fact]
    public void ReadOnlyDictionaryInterface_ExposesTheSameData()
    {
        IReadOnlyDictionary<EnumSetColor, int> map = new EnumMap<EnumSetColor, int>
        {
            [EnumSetColor.Red] = 1,
            [EnumSetColor.Blue] = 2,
        };

        Assert.Equal(2, map.Count);
        Assert.Equal(1, map[EnumSetColor.Red]);
        Assert.True(map.ContainsKey(EnumSetColor.Blue));
        Assert.True(map.TryGetValue(EnumSetColor.Blue, out int v));
        Assert.Equal(2, v);
        Assert.Equal(new[] { EnumSetColor.Red, EnumSetColor.Blue }, map.Keys.OrderBy(k => (int)k));
        Assert.Equal(new[] { 1, 2 }, map.Values.OrderBy(x => x));
    }
}
