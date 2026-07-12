using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Enumeration coverage for EnumMap<TEnum, TValue> (issue #263): ascending-underlying-value order
// (a deterministic bonus over Dictionary<,>), the allocation-free struct enumerator, and BCL
// modified-during-enumeration semantics. Uses the enums declared in EnumSetTests.cs.

public class EnumMapEnumerationTests
{
    [Fact]
    public void GetEnumerator_YieldsPairsInAscendingKeyOrder()
    {
        var map = new EnumMap<EnumSetWide, int>
        {
            [EnumSetWide.Top] = 3,       // 130
            [EnumSetWide.Zero] = 1,      // 0
            [EnumSetWide.High] = 4,      // 65
            [EnumSetWide.NextWord] = 2,  // 64
        };

        var pairs = new List<KeyValuePair<EnumSetWide, int>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        Assert.Equal(
            new[]
            {
                new KeyValuePair<EnumSetWide, int>(EnumSetWide.Zero, 1),
                new KeyValuePair<EnumSetWide, int>(EnumSetWide.NextWord, 2),
                new KeyValuePair<EnumSetWide, int>(EnumSetWide.High, 4),
                new KeyValuePair<EnumSetWide, int>(EnumSetWide.Top, 3),
            },
            pairs);
    }

    [Fact]
    public void GetEnumerator_OnEmptyMap_YieldsNothing()
    {
        var map = new EnumMap<EnumSetColor, int>();
        int seen = 0;
        foreach (var _ in map)
            seen++;
        Assert.Equal(0, seen);
    }

    [Fact]
    public void Enumerator_ReflectsEveryStoredPair()
    {
        var map = new EnumMap<EnumSetColor, int>
        {
            [EnumSetColor.Red] = 0,     // key 0, default value
            [EnumSetColor.Blue] = 20,
            [EnumSetColor.Magenta] = 50,
        };

        var round = new Dictionary<EnumSetColor, int>();
        foreach (var kvp in map)
            round[kvp.Key] = kvp.Value;

        Assert.Equal(3, round.Count);
        Assert.Equal(0, round[EnumSetColor.Red]);
        Assert.Equal(20, round[EnumSetColor.Blue]);
        Assert.Equal(50, round[EnumSetColor.Magenta]);
    }

    [Fact]
    public void MoveNext_Throws_WhenMapMutatedDuringEnumeration()
    {
        var map = new EnumMap<EnumSetColor, int> { [EnumSetColor.Red] = 1, [EnumSetColor.Blue] = 2 };

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var _ in map)
                map.Add(EnumSetColor.Green, 3); // structural change invalidates the enumerator
        });
    }

    [Fact]
    public void Overwrite_DoesNotInvalidateEnumerator()
    {
        // A pure value overwrite of an existing key is not a structural change, so it must not bump
        // the version or trip the modified-during-enumeration guard (matching Dictionary<,>).
        var map = new EnumMap<EnumSetColor, int> { [EnumSetColor.Red] = 1, [EnumSetColor.Blue] = 2 };

        foreach (var kvp in map)
            map[kvp.Key] = kvp.Value + 100; // overwrite in place

        Assert.Equal(101, map[EnumSetColor.Red]);
        Assert.Equal(102, map[EnumSetColor.Blue]);
    }

    [Fact]
    public void Reset_RestartsEnumeration()
    {
        var map = new EnumMap<EnumSetColor, int> { [EnumSetColor.Red] = 1, [EnumSetColor.Blue] = 2 };

        EnumMap<EnumSetColor, int>.Enumerator e = map.GetEnumerator();
        Assert.True(e.MoveNext());
        var first = e.Current;

        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(first, e.Current);
    }

    [Fact]
    public void Reset_Throws_AfterMutation()
    {
        var map = new EnumMap<EnumSetColor, int> { [EnumSetColor.Red] = 1 };
        EnumMap<EnumSetColor, int>.Enumerator e = map.GetEnumerator();
        map.Add(EnumSetColor.Blue, 2);

        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void NonGenericEnumerator_Works()
    {
        var map = new EnumMap<EnumSetColor, int> { [EnumSetColor.Red] = 1, [EnumSetColor.Blue] = 2 };

        IEnumerable enumerable = map;
        int seen = 0;
        foreach (object entry in enumerable)
        {
            Assert.IsType<KeyValuePair<EnumSetColor, int>>(entry);
            seen++;
        }

        Assert.Equal(2, seen);
    }

    [Fact]
    public void KeysAndValues_NonGenericEnumeration_Works()
    {
        var map = new EnumMap<EnumSetColor, int> { [EnumSetColor.Red] = 1, [EnumSetColor.Blue] = 2 };

        int keyCount = 0;
        foreach (object _ in (IEnumerable)map.Keys)
            keyCount++;

        int valueCount = 0;
        foreach (object _ in (IEnumerable)map.Values)
            valueCount++;

        Assert.Equal(2, keyCount);
        Assert.Equal(2, valueCount);
    }
}
