using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Milestone 1.2.0 — issue #61: SmallDictionary<TKey, TValue>, a flat-array,
// linear-scan dictionary optimized for the very-small (n <= ~16) case.
//
// These tests mirror the dedicated IntDictionaryTests behavioural coverage,
// adapted for a type that hashes nothing: there is no out-of-band default-key
// slot, so a 0 / null / default key is exercised as an ordinary key, and the
// "resize" cases assert the grow-and-keep-scanning behaviour rather than a
// rehash.
public class SmallDictionaryTests
{
    [Fact]
    public void Indexer_ShouldInsertAndRetrieveValue()
    {
        var map = new SmallDictionary<int, int>();
        map[10] = 100;

        Assert.Equal(100, map[10]);
    }

    [Fact]
    public void Indexer_ShouldThrowException_WhenKeyDoesNotExist()
    {
        var map = new SmallDictionary<int, int>();
        Assert.Throws<KeyNotFoundException>(() => { var value = map[99]; });
    }

    [Fact]
    public void Indexer_ShouldOverwriteExistingValue()
    {
        var map = new SmallDictionary<int, int>();
        map[5] = 500;
        map[5] = 999; // Overwrite

        Assert.Equal(999, map[5]);
        Assert.Single(map);
    }

    [Fact]
    public void Indexer_Overwrite_ShouldNotGrow_AtCapacity()
    {
        // A pure overwrite of an existing key must never grow the backing arrays:
        // only a brand-new entry can. We fill to capacity, then overwrite every
        // key and assert nothing is lost and the count is unchanged.
        var map = new SmallDictionary<int, int>(capacity: 4);
        for (int i = 0; i < 4; i++)
            map[i] = i;

        for (int i = 0; i < 4; i++)
            map[i] = i * 10;

        Assert.Equal(4, map.Count);
        for (int i = 0; i < 4; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void Remove_ShouldDeleteKeyAndMakeItUnreachable()
    {
        var map = new SmallDictionary<int, int>();
        map[7] = 700;

        bool removed = map.Remove(7);
        Assert.True(removed);
        Assert.False(map.ContainsKey(7));

        Assert.Throws<KeyNotFoundException>(() => { var value = map[7]; });
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var map = new SmallDictionary<int, int>();
        Assert.False(map.Remove(7));
    }

    [Fact]
    public void Remove_ShouldNotOrphanOtherEntries_WhenRemovingFromMiddle()
    {
        // Removal swaps the last entry into the vacated slot. Every surviving
        // key must remain reachable with its value regardless of which slot is
        // removed.
        var map = new SmallDictionary<int, string>();
        for (int i = 0; i < 10; i++)
            map[i] = $"v{i}";

        Assert.True(map.Remove(0)); // first
        Assert.True(map.Remove(5)); // middle
        Assert.True(map.Remove(9)); // last

        Assert.Equal(7, map.Count);
        for (int i = 0; i < 10; i++)
        {
            if (i == 0 || i == 5 || i == 9)
                Assert.False(map.ContainsKey(i));
            else
                Assert.Equal($"v{i}", map[i]);
        }
    }

    [Fact]
    public void Map_ShouldGrow_WhenCapacityExceeded()
    {
        var map = new SmallDictionary<int, int>(capacity: 4);
        for (int i = 1; i <= 50; i++)
            map[i] = i * 10;

        Assert.Equal(50, map.Count);
        for (int i = 1; i <= 50; i++)
            Assert.Equal(i * 10, map[i]);
    }

    // SmallDictionary hashes nothing, so a default key (0 / null / default) is an
    // ordinary inline entry rather than an out-of-band slot.
    [Fact]
    public void Indexer_ShouldHandleZeroKey_AsOrdinaryEntry()
    {
        var map = new SmallDictionary<int, string>();
        map[0] = "zero";
        map[1] = "one";

        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void Indexer_ShouldHandleNullKey_AsOrdinaryEntry()
    {
        var map = new SmallDictionary<string, int>();
        map["a"] = 1;
        map[null!] = 99;

        Assert.True(map.ContainsKey(null!));
        Assert.Equal(99, map[null!]);
        Assert.Equal(2, map.Count);

        Assert.True(map.Remove(null!));
        Assert.False(map.ContainsKey(null!));
        Assert.Single(map);
    }

    [Fact]
    public void Indexer_ShouldThrowKeyNotFound_ForAbsentZeroKey()
    {
        var map = new SmallDictionary<int, int>();
        Assert.Throws<KeyNotFoundException>(() => _ = map[0]);
    }

    [Fact]
    public void TryGetValue_ShouldReturnTrueAndValue_WhenKeyExists()
    {
        var map = new SmallDictionary<int, string>();
        map[42] = "answer";
        map[0] = "zero";

        Assert.True(map.TryGetValue(42, out var v1));
        Assert.Equal("answer", v1);

        Assert.True(map.TryGetValue(0, out var v2));
        Assert.Equal("zero", v2);
    }

    [Fact]
    public void TryGetValue_ShouldReturnFalseAndDefault_WhenKeyMissing()
    {
        var map = new SmallDictionary<int, string>();
        Assert.False(map.TryGetValue(42, out var v1));
        Assert.Null(v1);
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        var map = new SmallDictionary<int, int>();
        for (int i = 0; i < 32; i++)
            map[i] = i * i;

        map.Clear();

        Assert.Empty(map);
        for (int i = 0; i < 32; i++)
            Assert.False(map.ContainsKey(i));

        // Reusable after clearing.
        map[0] = 100;
        map[5] = 500;
        Assert.Equal(2, map.Count);
        Assert.Equal(100, map[0]);
        Assert.Equal(500, map[5]);
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var map = new SmallDictionary<int, int>();

        map.Clear(); // _count == 0 early-return path

        Assert.Empty(map.Keys);
    }

    [Fact]
    public void Remove_Then_Reinsert_ManyKeys_ShouldNotLoseEntries()
    {
        var map = new SmallDictionary<int, int>(8);
        for (int i = 1; i <= 100; i++)
            map[i] = i;

        for (int i = 1; i <= 100; i += 2)
            Assert.True(map.Remove(i));

        Assert.Equal(50, map.Count);

        for (int i = 1; i <= 100; i += 2)
            map[i] = -i;

        Assert.Equal(100, map.Count);
        for (int i = 1; i <= 100; i++)
        {
            int expected = i % 2 == 0 ? i : -i;
            Assert.Equal(expected, map[i]);
        }
    }

    [Fact]
    public void ZeroCapacity_ShouldDeferAllocation_AndStillInsert()
    {
        var map = new SmallDictionary<int, int>(capacity: 0);
        map[42] = 100;

        Assert.Equal(100, map[42]);
        Assert.Single(map);
    }
}
