using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="TinyLfuCache{TKey, TValue, THasher}"/>: entries are yielded as
/// the window, then probation, then the protected segment, each newest first; the struct enumerator
/// honours the version guard for inserts, evictions, removals, clears and every use that moves an
/// entry, while a use of the entry already at the front of the window or of protected, an overwrite
/// of such an entry, and every peek leave it valid; and the boxed <see cref="IEnumerable"/> path agrees
/// with the struct fast path.
/// </summary>
public class TinyLfuCacheEnumerationTests
{
    // Capacity 10: a one-entry window, a nine-entry main region, a protected share of seven.
    private static TinyLfuCache<int, int, Int32WangHasher> Cache(int capacity = 10) => new(capacity);

    private static List<int> KeysInOrder(TinyLfuCache<int, int, Int32WangHasher> cache)
    {
        var keys = new List<int>();
        foreach (var kvp in cache)
            keys.Add(kvp.Key);
        return keys;
    }

    [Fact]
    public void EmptyCache_YieldsNothing()
    {
        var cache = Cache();
        Assert.Empty(KeysInOrder(cache));
    }

    [Fact]
    public void Enumerates_WindowThenProbationThenProtected()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;             // window [3], probation [2, 1]
        Assert.Equal(new[] { 3, 2, 1 }, KeysInOrder(cache));

        _ = cache[1];
        _ = cache[2];
        _ = cache[1];              // protected [1, 2], probation empty
        Assert.Equal(new[] { 3, 1, 2 }, KeysInOrder(cache));
    }

    [Fact]
    public void Enumerates_WhenOnlyProtectedIsNonEmpty()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;             // window [2], probation [1]
        _ = cache[1];              // protected [1]
        Assert.True(cache.Remove(2));
        Assert.Equal(new[] { 1 }, KeysInOrder(cache));
    }

    [Fact]
    public void Enumeration_YieldsCurrentValues()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[1] = 11; // overwrite

        var pairs = new Dictionary<int, int>();
        foreach (var kvp in cache)
            pairs[kvp.Key] = kvp.Value;

        Assert.Equal(11, pairs[1]);
        Assert.Equal(20, pairs[2]);
    }

    [Theory]
    [InlineData("insert")]
    [InlineData("evict")]
    [InlineData("remove")]
    [InlineData("clear")]
    [InlineData("promote")]
    [InlineData("reorder-protected")]
    [InlineData("reorder-window")]
    public void StructuralMutation_DuringEnumeration_Throws(string mutation)
    {
        int capacity = mutation switch { "reorder-window" => 200, "reorder-protected" => 10, _ => 3 };
        var cache = Cache(capacity);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;             // capacity 3 or 10: window [3], probation [2, 1]; capacity 200: window [3, 2], probation [1]
        if (mutation == "reorder-protected")
        {
            _ = cache[1];
            _ = cache[2];          // protected [2, 1]
        }

        TinyLfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());

        switch (mutation)
        {
            case "insert": cache[9] = 90; break;
            case "evict": cache[9] = 90; Assert.Equal(3, cache.Count); break;
            case "remove": Assert.True(cache.Remove(1)); break;
            case "clear": cache.Clear(); break;
            case "promote": _ = cache[1]; break;
            case "reorder-protected": _ = cache[1]; break;
            case "reorder-window": Assert.True(cache.TryGet(2, out _)); break;
        }

        try
        {
            e.MoveNext();
            Assert.Fail("MoveNext should have thrown after a structural mutation.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public void UsesThatMoveNothing_AndPeeks_DuringEnumeration_DoNotThrow()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;             // window [3], probation [2, 1]
        _ = cache[1];              // protected [1]

        var seen = new List<int>();
        foreach (var kvp in cache)
        {
            seen.Add(kvp.Key);
            _ = cache[3];                        // already the front of the window
            Assert.True(cache.TryGet(1, out _)); // already the front of protected
            cache[3] = 33;
            cache.AddOrUpdate(1, 11);
            Assert.False(cache.TryAdd(2, 99));
            Assert.True(cache.TryPeek(2, out _));
            Assert.True(cache.ContainsKey(2));
            _ = cache.GetFrequencyEstimate(2);
        }

        Assert.Equal(new[] { 3, 2, 1 }, seen);
    }

    [Fact]
    public void FailedRemove_AndEmptyClear_DuringEnumeration_DoNotThrow()
    {
        var cache = Cache();
        cache[1] = 10;

        TinyLfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.False(cache.Remove(42));
        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());

        var empty = Cache();
        TinyLfuCache<int, int, Int32WangHasher>.Enumerator e2 = empty.GetEnumerator();
        empty.Clear();
        Assert.False(e2.MoveNext());
    }

    [Fact]
    public void Reset_RestartsFromTheFrontOfTheWindow()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;

        TinyLfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current.Key);
        Assert.True(e.MoveNext());
        e.Reset();
        Assert.Equal(default, e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current.Key);
    }

    [Fact]
    public void Reset_AfterStructuralMutation_Throws()
    {
        var cache = Cache();
        cache[1] = 10;

        TinyLfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        cache[2] = 20;
        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void BoxedEnumerable_AgreesWithStructPath()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;
        _ = cache[1];

        IEnumerable<KeyValuePair<int, int>> boxed = cache;
        var viaInterface = boxed.Select(kvp => kvp.Key).ToList();
        Assert.Equal(KeysInOrder(cache), viaInterface);

        IEnumerable nonGeneric = cache;
        var viaNonGeneric = new List<int>();
        IEnumerator raw = nonGeneric.GetEnumerator();
        while (raw.MoveNext())
            viaNonGeneric.Add(((KeyValuePair<int, int>)raw.Current).Key);
        Assert.Equal(KeysInOrder(cache), viaNonGeneric);
        (raw as IDisposable)?.Dispose();
    }

    [Fact]
    public void MoveNext_PastEnd_StaysFalse_AndClearsCurrent()
    {
        var cache = Cache();
        cache[1] = 10;

        TinyLfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.Equal(default, e.Current);
        Assert.False(e.MoveNext()); // idempotent after exhaustion
        e.Dispose();
    }
}
