using Celerity.Collections;

namespace Celerity.Tests.Collections;

public class LongDictionaryTests
{
    [Fact]
    public void Indexer_ShouldInsertAndRetrieveValue()
    {
        var map = new LongDictionary<int>();
        map[10L] = 100;

        Assert.Equal(100, map[10L]);
    }

    [Fact]
    public void Indexer_ShouldThrowException_WhenKeyDoesNotExist()
    {
        var map = new LongDictionary<int>();
        Assert.Throws<KeyNotFoundException>(() => { var value = map[99L]; });
    }

    [Fact]
    public void Indexer_ShouldOverwriteExistingValue()
    {
        var map = new LongDictionary<int>();
        map[5L] = 500;
        map[5L] = 999; // Overwrite

        Assert.Equal(999, map[5L]);
    }

    [Fact]
    public void Remove_ShouldDeleteKeyAndMakeItUnreachable()
    {
        var map = new LongDictionary<int>();
        map[7L] = 700;

        bool removed = map.Remove(7L);
        Assert.True(removed);
        Assert.False(map.ContainsKey(7L));

        Assert.Throws<KeyNotFoundException>(() => { var value = map[7L]; });
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var map = new LongDictionary<int>();
        Assert.False(map.Remove(7L));
    }

    [Fact]
    public void Map_ShouldResize_WhenThresholdExceeded()
    {
        var map = new LongDictionary<int>(4);
        map[1L] = 10;
        map[2L] = 20;
        map[3L] = 30;
        map[4L] = 40; // Triggers resize

        Assert.Equal(4, map.Count);
        Assert.Equal(10, map[1L]);
        Assert.Equal(20, map[2L]);
        Assert.Equal(30, map[3L]);
        Assert.Equal(40, map[4L]);
    }

    [Fact]
    public void Ctor_ShouldRespectLargeCapacity_WithoutResizingOnBulkInsert()
    {
        var map = new LongDictionary<int>(capacity: 1024, loadFactor: 0.99f);
        for (int i = 1; i <= 500; i++)
        {
            map[i] = i * 10;
        }

        Assert.Equal(500, map.Count);
        for (int i = 1; i <= 500; i++)
        {
            Assert.Equal(i * 10, map[i]);
        }
    }

    // The zero-key collision with EMPTY_KEY = 0L is the same correctness bug as
    // #2 on IntDictionary; LongDictionary must handle it the same way.
    [Fact]
    public void Indexer_ShouldHandleZeroKey()
    {
        var map = new LongDictionary<string>();
        map[0L] = "zero";

        Assert.True(map.ContainsKey(0L));
        Assert.Equal("zero", map[0L]);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void Indexer_ShouldOverwriteZeroKey()
    {
        var map = new LongDictionary<string>();
        map[0L] = "zero";
        map[0L] = "still-zero";

        Assert.Equal(1, map.Count);
        Assert.Equal("still-zero", map[0L]);
    }

    [Fact]
    public void Remove_ShouldHandleZeroKey()
    {
        var map = new LongDictionary<string>();
        map[0L] = "zero";
        map[1L] = "one";

        Assert.True(map.Remove(0L));
        Assert.False(map.ContainsKey(0L));
        Assert.True(map.ContainsKey(1L));
        Assert.Equal(1, map.Count);
        Assert.False(map.Remove(0L));
    }

    [Fact]
    public void ZeroKey_ShouldSurviveResize()
    {
        var map = new LongDictionary<long>(4);
        map[0L] = -1L;
        map[1L] = 10L;
        map[2L] = 20L;
        map[3L] = 30L;
        map[4L] = 40L; // Triggers resize while the zero-key entry is live.

        Assert.Equal(5, map.Count);
        Assert.Equal(-1L, map[0L]);
        Assert.Equal(10L, map[1L]);
        Assert.Equal(40L, map[4L]);
    }

    [Fact]
    public void TryGetValue_ShouldReturnTrueAndValue_WhenKeyExists()
    {
        var map = new LongDictionary<string>();
        map[42L] = "answer";
        map[0L] = "zero";

        Assert.True(map.TryGetValue(42L, out var v1));
        Assert.Equal("answer", v1);

        Assert.True(map.TryGetValue(0L, out var v2));
        Assert.Equal("zero", v2);
    }

    [Fact]
    public void TryGetValue_ShouldReturnFalseAndDefault_WhenKeyMissing()
    {
        var map = new LongDictionary<string>();
        Assert.False(map.TryGetValue(42L, out var v1));
        Assert.Null(v1);

        Assert.False(map.TryGetValue(0L, out var v2));
        Assert.Null(v2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        var map = new LongDictionary<int>();
        for (int i = 0; i < 32; i++)
            map[i] = i * i;

        map.Clear();

        Assert.Equal(0, map.Count);
        for (int i = 0; i < 32; i++)
            Assert.False(map.ContainsKey(i));

        // Reusable after clearing.
        map[0L] = 100;
        map[5L] = 500;
        Assert.Equal(2, map.Count);
        Assert.Equal(100, map[0L]);
        Assert.Equal(500, map[5L]);
    }

    [Fact]
    public void Remove_Then_Reinsert_ManyKeys_ShouldNotLoseEntries()
    {
        var map = new LongDictionary<int>(8);
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

    // Long-specific: keys that are distinct as longs but collide as ints when
    // truncated. Without this, a buggy implementation that casts the key to int
    // somewhere on the probe path would silently overwrite. The two values below
    // share the same low 32 bits but differ in the high 32 bits.
    [Fact]
    public void Indexer_ShouldDistinguishKeys_ThatShareLowerInt32Bits()
    {
        const long lowOnly = 0x0000_0000_DEAD_BEEFL;
        const long withHighBits = unchecked((long)0xCAFE_0000_DEAD_BEEFL);

        var map = new LongDictionary<string>();
        map[lowOnly] = "low";
        map[withHighBits] = "high";

        Assert.Equal(2, map.Count);
        Assert.Equal("low", map[lowOnly]);
        Assert.Equal("high", map[withHighBits]);
    }

    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(int.MaxValue + 1L)]
    [InlineData(int.MinValue - 1L)]
    [InlineData(-1L)]
    public void Indexer_ShouldHandleExtremeKeys(long key)
    {
        var map = new LongDictionary<string>();
        map[key] = "x";

        Assert.True(map.ContainsKey(key));
        Assert.Equal("x", map[key]);
    }

    [Fact]
    public void Add_ShouldThrow_WhenKeyAlreadyExists()
    {
        var map = new LongDictionary<int>();
        map.Add(7L, 70);

        Assert.Throws<ArgumentException>(() => map.Add(7L, 99));
        Assert.Equal(70, map[7L]);
    }

    [Fact]
    public void Add_ShouldThrow_WhenZeroKeyAlreadyExists()
    {
        var map = new LongDictionary<int>();
        map.Add(0L, 100);

        Assert.Throws<ArgumentException>(() => map.Add(0L, 200));
        Assert.Equal(100, map[0L]);
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenKeyAlreadyExists()
    {
        var map = new LongDictionary<int>();
        Assert.True(map.TryAdd(7L, 70));
        Assert.False(map.TryAdd(7L, 99));
        Assert.Equal(70, map[7L]);
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenZeroKeyAlreadyExists()
    {
        var map = new LongDictionary<int>();
        Assert.True(map.TryAdd(0L, 100));
        Assert.False(map.TryAdd(0L, 200));
        Assert.Equal(100, map[0L]);
    }

    [Theory]
    [InlineData(-1)]
    public void Ctor_ShouldThrow_OnNegativeCapacity(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LongDictionary<int>(capacity: capacity));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void Ctor_ShouldThrow_OnInvalidLoadFactor(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LongDictionary<int>(loadFactor: loadFactor));
    }

    [Fact]
    public void Ctor_FromEnumerable_ShouldCopyAllEntries()
    {
        var source = new[]
        {
            new KeyValuePair<long, string>(0L, "zero"),
            new KeyValuePair<long, string>(1L, "one"),
            new KeyValuePair<long, string>(long.MaxValue, "max"),
            new KeyValuePair<long, string>(long.MinValue, "min"),
        };

        var map = new LongDictionary<string>(source);

        Assert.Equal(4, map.Count);
        Assert.Equal("zero", map[0L]);
        Assert.Equal("one", map[1L]);
        Assert.Equal("max", map[long.MaxValue]);
        Assert.Equal("min", map[long.MinValue]);
    }

    [Fact]
    public void Ctor_FromEnumerable_ShouldThrow_OnNullSource()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LongDictionary<int>((IEnumerable<KeyValuePair<long, int>>)null!));
    }

    [Fact]
    public void Ctor_FromEnumerable_ShouldThrow_OnDuplicateKeys()
    {
        var source = new[]
        {
            new KeyValuePair<long, int>(5L, 50),
            new KeyValuePair<long, int>(5L, 99),
        };

        Assert.Throws<ArgumentException>(() => new LongDictionary<int>(source));
    }

    [Fact]
    public void Ctor_FromEnumerable_ShouldThrow_OnDuplicateZeroKeys()
    {
        var source = new[]
        {
            new KeyValuePair<long, int>(0L, 1),
            new KeyValuePair<long, int>(0L, 2),
        };

        Assert.Throws<ArgumentException>(() => new LongDictionary<int>(source));
    }

    // ---------------- Enumeration ----------------

    [Fact]
    public void GetEnumerator_ShouldYieldAllEntries_IncludingZeroKeyFirst()
    {
        var map = new LongDictionary<string>();
        map[0L] = "zero";
        map[1L] = "one";
        map[2L] = "two";

        var seen = new List<KeyValuePair<long, string?>>();
        foreach (var kvp in map)
            seen.Add(kvp);

        Assert.Equal(3, seen.Count);
        // Zero-key entry is yielded first by contract.
        Assert.Equal(new KeyValuePair<long, string?>(0L, "zero"), seen[0]);

        var asMap = seen.ToDictionary(k => k.Key, v => v.Value);
        Assert.Equal("zero", asMap[0L]);
        Assert.Equal("one", asMap[1L]);
        Assert.Equal("two", asMap[2L]);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var map = new LongDictionary<int>();
        int n = 0;
        foreach (var _ in map) n++;
        Assert.Equal(0, n);
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenDictionaryMutatedMidEnumeration()
    {
        var map = new LongDictionary<int>();
        map[1L] = 10;
        map[2L] = 20;

        var enumerator = map.GetEnumerator();
        enumerator.MoveNext();
        map[3L] = 30; // Structural mutation invalidates the enumerator.

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Keys_ShouldYieldAllKeys_IncludingZero()
    {
        var map = new LongDictionary<int>();
        map[0L] = 0;
        map[1L] = 10;
        map[long.MaxValue] = 99;

        var keys = new HashSet<long>();
        foreach (long k in map.Keys)
            keys.Add(k);

        Assert.Equal(3, keys.Count);
        Assert.Contains(0L, keys);
        Assert.Contains(1L, keys);
        Assert.Contains(long.MaxValue, keys);
        Assert.Equal(3, map.Keys.Count);
    }

    [Fact]
    public void Values_ShouldYieldAllValues()
    {
        var map = new LongDictionary<string>();
        map[0L] = "zero";
        map[1L] = "one";

        var values = new HashSet<string?>();
        foreach (var v in map.Values)
            values.Add(v);

        Assert.Equal(2, values.Count);
        Assert.Contains("zero", values);
        Assert.Contains("one", values);
        Assert.Equal(2, map.Values.Count);
    }

    // ---------------- IReadOnlyDictionary boxed surface ----------------

    [Fact]
    public void AsIReadOnlyDictionary_ShouldExposeFullSurface()
    {
        var map = new LongDictionary<string>();
        map[0L] = "zero";
        map[1L] = "one";

        IReadOnlyDictionary<long, string?> view = map;

        Assert.Equal(2, view.Count);
        Assert.True(view.ContainsKey(0L));
        Assert.True(view.ContainsKey(1L));
        Assert.False(view.ContainsKey(99L));

        Assert.Equal("zero", view[0L]);
        Assert.True(view.TryGetValue(1L, out var v));
        Assert.Equal("one", v);

        Assert.Equal(2, view.Keys.Count());
        Assert.Equal(2, view.Values.Count());
        Assert.Equal(2, view.Count());
    }
}
