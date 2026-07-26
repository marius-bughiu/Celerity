using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Enum key types whose underlying integer width exercises every arm of EnumMap's key <-> index
// conversion. Declared here (rather than reusing the ones in EnumSetTests.cs) because the 2-byte
// widths have no equivalent there, and because these tests deliberately pick maximum values that
// leave room for an out-of-range cast below the underlying type's own maximum.
internal enum EnumMapByteKey : byte { Zero = 0, Mid = 7, High = 100 }      // TotalBits = 128
internal enum EnumMapUShortKey : ushort { Zero = 0, Mid = 9, High = 1000 } // TotalBits = 1024
internal enum EnumMapShortKey : short { Zero = 0, Mid = 5, High = 300 }    // TotalBits = 320
internal enum EnumMapLongKey : long { Zero = 0, Mid = 13, High = 500 }     // TotalBits = 512
internal enum EnumMapULongKey : ulong { Zero = 0, Mid = 4, High = 700 }    // TotalBits = 704

/// <summary>
/// Pins <see cref="EnumMap{TEnum, TValue}"/>'s behaviour across every <b>underlying integer width</b>
/// an enum may declare, and the strongly-typed enumerators of its <c>Keys</c> / <c>Values</c> views.
///
/// <para>
/// <b>Why the widths matter.</b> An enum key is not an <see cref="int"/> — it is whatever integer
/// type the enum declares, and <see cref="EnumMap{TEnum, TValue}"/> reinterprets its bits at that
/// natural width in both directions: key to bit index on every write and lookup, and bit index back
/// to key on every enumeration step. Each width is a separate reinterpretation, so a defect in one
/// (reading two bytes as four, sign-extending a <c>short</c>, truncating an eight-byte value) is
/// invisible to tests written against the default <see cref="int"/>-backed enum. The
/// <see cref="int"/> case is the one the rest of the suite exercises; the round trips below cover
/// <c>byte</c>, <c>ushort</c>, <c>short</c>, <c>long</c> and <c>ulong</c>, and they enumerate rather
/// than only read by key, because the reverse conversion runs <i>only</i> from the enumerator.
/// </para>
///
/// <para>
/// <b>Unsigned reads.</b> The forward conversion reads the key's bits <i>unsigned</i> at its natural
/// width so that every value maps to a non-negative candidate index and a single unsigned bound
/// check rejects everything out of range. The consequence is observable and worth pinning: a
/// negative key produced by an out-of-range cast (<c>(EnumMapShortKey)(-1)</c>) reads as a very
/// large index and is rejected exactly like any other out-of-range key — writes throw
/// <see cref="ArgumentOutOfRangeException"/>, reads report absent — rather than wrapping onto a
/// valid slot and silently corrupting a neighbouring entry.
/// </para>
///
/// <para>
/// <b>Keys / Values enumerators.</b> Both views expose a strongly-typed struct
/// <c>GetEnumerator()</c> alongside the boxing <see cref="IEnumerable{T}"/> implementations; that is
/// what makes <c>foreach</c> over <c>map.Keys</c> allocation-free, and it is a different method from
/// the interface one. These tests bind to it explicitly and drive it by hand, then exercise
/// <c>Reset()</c> on it — the forwarding member that lets a caller replay a view from the beginning
/// without re-reading the property.
/// </para>
/// </summary>
public class EnumMapUnderlyingTypeCoverageTests
{
    // ── Round trips per underlying width ──────────────────────────────────────

    [Fact]
    public void Enumerate_ShouldRoundTripKeysAndValues_WhenUnderlyingTypeIsByte()
    {
        var map = new EnumMap<EnumMapByteKey, string>
        {
            [EnumMapByteKey.High] = "high",
            [EnumMapByteKey.Zero] = "zero",
            [EnumMapByteKey.Mid] = "mid",
        };

        Assert.Equal(3, map.Count);
        Assert.Equal("high", map[EnumMapByteKey.High]);

        var pairs = new List<KeyValuePair<EnumMapByteKey, string?>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        Assert.Equal(
            new[]
            {
                new KeyValuePair<EnumMapByteKey, string?>(EnumMapByteKey.Zero, "zero"),
                new KeyValuePair<EnumMapByteKey, string?>(EnumMapByteKey.Mid, "mid"),
                new KeyValuePair<EnumMapByteKey, string?>(EnumMapByteKey.High, "high"),
            },
            pairs);

        Assert.True(map.Remove(EnumMapByteKey.Mid, out string? removed));
        Assert.Equal("mid", removed);
        Assert.False(map.ContainsKey(EnumMapByteKey.Mid));
        Assert.Equal(
            new[] { EnumMapByteKey.Zero, EnumMapByteKey.High },
            map.Keys.ToArray());
    }

    [Fact]
    public void Enumerate_ShouldRoundTripKeysAndValues_WhenUnderlyingTypeIsUShort()
    {
        // 1000 does not fit in a byte, so a one-byte read of this key would truncate to 232.
        var map = new EnumMap<EnumMapUShortKey, int>
        {
            [EnumMapUShortKey.High] = 3,
            [EnumMapUShortKey.Zero] = 1,
            [EnumMapUShortKey.Mid] = 2,
        };

        Assert.Equal(3, map.Count);
        Assert.Equal(3, map[EnumMapUShortKey.High]);

        var keys = new List<EnumMapUShortKey>();
        var values = new List<int>();
        foreach (var kvp in map)
        {
            keys.Add(kvp.Key);
            values.Add(kvp.Value);
        }

        Assert.Equal(
            new[] { EnumMapUShortKey.Zero, EnumMapUShortKey.Mid, EnumMapUShortKey.High },
            keys);
        Assert.Equal(new[] { 1, 2, 3 }, values);

        Assert.True(map.Remove(EnumMapUShortKey.High));
        Assert.Equal(2, map.Count);
        Assert.False(map.ContainsKey(EnumMapUShortKey.High));
    }

    [Fact]
    public void Enumerate_ShouldRoundTripKeysAndValues_WhenUnderlyingTypeIsSignedShort()
    {
        var map = new EnumMap<EnumMapShortKey, string>();
        map.Add(EnumMapShortKey.High, "high");
        map.Add(EnumMapShortKey.Zero, "zero");

        var keys = new List<EnumMapShortKey>();
        foreach (var kvp in map)
            keys.Add(kvp.Key);

        Assert.Equal(new[] { EnumMapShortKey.Zero, EnumMapShortKey.High }, keys);
        Assert.Equal("high", map[EnumMapShortKey.High]);
        Assert.False(map.ContainsKey(EnumMapShortKey.Mid));
    }

    [Fact]
    public void Enumerate_ShouldRoundTripKeysAndValues_WhenUnderlyingTypeIsLong()
    {
        var map = new EnumMap<EnumMapLongKey, string>
        {
            [EnumMapLongKey.High] = "high",
            [EnumMapLongKey.Mid] = "mid",
            [EnumMapLongKey.Zero] = "zero",
        };

        var pairs = new List<KeyValuePair<EnumMapLongKey, string?>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        Assert.Equal(
            new[]
            {
                new KeyValuePair<EnumMapLongKey, string?>(EnumMapLongKey.Zero, "zero"),
                new KeyValuePair<EnumMapLongKey, string?>(EnumMapLongKey.Mid, "mid"),
                new KeyValuePair<EnumMapLongKey, string?>(EnumMapLongKey.High, "high"),
            },
            pairs);

        Assert.True(map.Remove(EnumMapLongKey.Zero));
        Assert.Equal(new[] { EnumMapLongKey.Mid, EnumMapLongKey.High }, map.Keys.ToArray());
    }

    [Fact]
    public void Enumerate_ShouldRoundTripKeysAndValues_WhenUnderlyingTypeIsULong()
    {
        var map = new EnumMap<EnumMapULongKey, int>
        {
            [EnumMapULongKey.High] = 700,
            [EnumMapULongKey.Zero] = 0,
        };

        var keys = new List<EnumMapULongKey>();
        var values = new List<int>();
        foreach (var kvp in map)
        {
            keys.Add(kvp.Key);
            values.Add(kvp.Value);
        }

        Assert.Equal(new[] { EnumMapULongKey.Zero, EnumMapULongKey.High }, keys);
        Assert.Equal(new[] { 0, 700 }, values);
        Assert.Equal(700, map[EnumMapULongKey.High]);
    }

    // ── Out-of-range keys, per underlying width ───────────────────────────────

    [Fact]
    public void Add_ShouldThrowArgumentOutOfRange_WhenByteKeyIsBeyondTheBackingStore()
    {
        // EnumMapByteKey's maximum member is 100, so the store addresses 0..127. 200 is a perfectly
        // valid byte and still outside the map's range.
        var map = new EnumMap<EnumMapByteKey, int>();
        var bad = (EnumMapByteKey)200;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.Add(bad, 1));
        Assert.Equal("key", ex.ParamName);
        Assert.False(map.ContainsKey(bad));
        Assert.False(map.TryGetValue(bad, out int value));
        Assert.Equal(0, value);
        Assert.Empty(map);
    }

    [Fact]
    public void Add_ShouldThrowArgumentOutOfRange_WhenUShortKeyIsBeyondTheBackingStore()
    {
        var map = new EnumMap<EnumMapUShortKey, int>();
        var bad = (EnumMapUShortKey)60000;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map[bad] = 1);
        Assert.Equal("key", ex.ParamName);
        Assert.False(map.ContainsKey(bad));
        Assert.False(map.Remove(bad));
    }

    [Fact]
    public void Add_ShouldThrowArgumentOutOfRange_WhenSignedShortKeyIsNegative()
    {
        // The key's bits are read unsigned at their natural width, so -1 reads as 65535 — far past
        // the 320-bit store — instead of sign-extending onto a valid slot.
        var map = new EnumMap<EnumMapShortKey, int> { [EnumMapShortKey.Zero] = 7 };
        var negative = (EnumMapShortKey)(-1);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.TryAdd(negative, 1));
        Assert.Equal("key", ex.ParamName);
        Assert.False(map.ContainsKey(negative));
        Assert.Throws<KeyNotFoundException>(() => map[negative]);
        Assert.Equal(7, map[EnumMapShortKey.Zero]); // the in-range entry is untouched
        Assert.Single(map);
    }

    [Fact]
    public void Add_ShouldThrowArgumentOutOfRange_WhenLongKeyIsNegative()
    {
        var map = new EnumMap<EnumMapLongKey, int> { [EnumMapLongKey.Mid] = 5 };
        var negative = (EnumMapLongKey)(-1);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.Add(negative, 1));
        Assert.Equal("key", ex.ParamName);
        Assert.False(map.ContainsKey(negative));
        Assert.Single(map);
    }

    [Fact]
    public void Add_ShouldThrowArgumentOutOfRange_WhenULongKeyIsBeyondTheBackingStore()
    {
        var map = new EnumMap<EnumMapULongKey, int>();
        var bad = (EnumMapULongKey)70000;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.TryAdd(bad, 1));
        Assert.Equal("key", ex.ParamName);
        Assert.False(map.TryGetValue(bad, out _));
    }

    // ── Strongly-typed Keys / Values enumerators ──────────────────────────────

    [Fact]
    public void KeysGetEnumerator_ShouldYieldKeysInAscendingOrder_WhenDrivenDirectly()
    {
        var map = new EnumMap<EnumMapByteKey, int>
        {
            [EnumMapByteKey.High] = 3,
            [EnumMapByteKey.Zero] = 1,
            [EnumMapByteKey.Mid] = 2,
        };

        // Bind to the struct GetEnumerator() on the view itself, not the IEnumerable<T> one.
        EnumMap<EnumMapByteKey, int>.KeyCollection.Enumerator e = map.Keys.GetEnumerator();

        Assert.True(e.MoveNext());
        Assert.Equal(EnumMapByteKey.Zero, e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal(EnumMapByteKey.Mid, e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal(EnumMapByteKey.High, e.Current);
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void ValuesGetEnumerator_ShouldYieldValuesOrderedByKey_WhenDrivenDirectly()
    {
        var map = new EnumMap<EnumMapUShortKey, string>
        {
            [EnumMapUShortKey.High] = "third",
            [EnumMapUShortKey.Zero] = "first",
            [EnumMapUShortKey.Mid] = "second",
        };

        EnumMap<EnumMapUShortKey, string>.ValueCollection.Enumerator e = map.Values.GetEnumerator();

        Assert.True(e.MoveNext());
        Assert.Equal("first", e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal("second", e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal("third", e.Current);
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void KeysEnumeratorReset_ShouldRestartFromTheFirstKey_WhenCalledMidEnumeration()
    {
        var map = new EnumMap<EnumMapLongKey, int>
        {
            [EnumMapLongKey.Zero] = 1,
            [EnumMapLongKey.Mid] = 2,
        };

        EnumMap<EnumMapLongKey, int>.KeyCollection.Enumerator e = map.Keys.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(EnumMapLongKey.Zero, e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal(EnumMapLongKey.Mid, e.Current);

        e.Reset();

        Assert.True(e.MoveNext());
        Assert.Equal(EnumMapLongKey.Zero, e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal(EnumMapLongKey.Mid, e.Current);
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void ValuesEnumeratorReset_ShouldRestartFromTheFirstValue_WhenCalledMidEnumeration()
    {
        var map = new EnumMap<EnumMapLongKey, string>
        {
            [EnumMapLongKey.Zero] = "a",
            [EnumMapLongKey.High] = "b",
        };

        EnumMap<EnumMapLongKey, string>.ValueCollection.Enumerator e = map.Values.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal("a", e.Current);

        e.Reset();

        Assert.True(e.MoveNext());
        Assert.Equal("a", e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal("b", e.Current);
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void KeysEnumeratorReset_ShouldThrowInvalidOperation_WhenTheMapWasMutated()
    {
        // Reset forwards to the underlying map enumerator, so it inherits the version guard: a
        // structural change must invalidate an outstanding Keys enumerator too.
        var map = new EnumMap<EnumMapByteKey, int> { [EnumMapByteKey.Zero] = 1 };
        EnumMap<EnumMapByteKey, int>.KeyCollection.Enumerator e = map.Keys.GetEnumerator();

        map.Add(EnumMapByteKey.Mid, 2);

        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void ValuesEnumeratorReset_ShouldThrowInvalidOperation_WhenTheMapWasMutated()
    {
        var map = new EnumMap<EnumMapByteKey, int> { [EnumMapByteKey.Zero] = 1 };
        EnumMap<EnumMapByteKey, int>.ValueCollection.Enumerator e = map.Values.GetEnumerator();

        map.Remove(EnumMapByteKey.Zero);

        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }
}
