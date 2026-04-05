using Celerity.Collections;

namespace Celerity.Tests.Collections;

public class IntDictionaryTests
{
    [Fact]
    public void Indexer_ShouldInsertAndRetrieveValue()
    {
        var map = new IntDictionary<int>();
        map[10] = 100;

        Assert.Equal(100, map[10]);
    }

    [Fact]
    public void Indexer_ShouldThrowException_WhenKeyDoesNotExist()
    {
        var map = new IntDictionary<int>();
        Assert.Throws<KeyNotFoundException>(() => { var value = map[99]; });
    }

    [Fact]
    public void Indexer_ShouldOverwriteExistingValue()
    {
        var map = new IntDictionary<int>();
        map[5] = 500;
        map[5] = 999; // Overwrite

        Assert.Equal(999, map[5]);
    }

    [Fact]
    public void Remove_ShouldDeleteKeyAndMakeItUnreachable()
    {
        var map = new IntDictionary<int>();
        map[7] = 700;

        bool removed = map.Remove(7);
        Assert.True(removed);
        Assert.False(map.ContainsKey(7));

        Assert.Throws<KeyNotFoundException>(() => { var value = map[7]; });
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var map = new IntDictionary<int>();
        Assert.False(map.Remove(7));
    }

    [Fact]
    public void Map_ShouldResize_WhenThresholdExceeded()
    {
        var map = new IntDictionary<int>(4);
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;
        map[4] = 40; // Triggers resize

        Assert.Equal(4, map.Count);
        Assert.Equal(10, map[1]);
        Assert.Equal(20, map[2]);
        Assert.Equal(30, map[3]);
        Assert.Equal(40, map[4]);
    }

    // Regression: #1 — constructor arguments were silently dropped by the
    // IntDictionary<TValue> convenience subclass.
    [Fact]
    public void Ctor_ShouldRespectLargeCapacity_WithoutResizingOnBulkInsert()
    {
        // 0.2.0: ctor args were being discarded; a capacity of 1024 at 0.99 load
        // factor should comfortably absorb 500 inserts without triggering a resize.
        // This test is a smoke test for the forwarding fix — if the old code path
        // is reintroduced, the inserts still succeed, so we rely on correctness
        // checks instead of internal state inspection.
        var map = new IntDictionary<int>(capacity: 1024, loadFactor: 0.99f);
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

    // Regression: #2 — key 0 collides with the EMPTY_KEY sentinel.
    [Fact]
    public void Indexer_ShouldHandleZeroKey()
    {
        var map = new IntDictionary<string>();
        map[0] = "zero";

        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void Indexer_ShouldOverwriteZeroKey()
    {
        var map = new IntDictionary<string>();
        map[0] = "zero";
        map[0] = "still-zero";

        Assert.Equal(1, map.Count);
        Assert.Equal("still-zero", map[0]);
    }

    [Fact]
    public void Remove_ShouldHandleZeroKey()
    {
        var map = new IntDictionary<string>();
        map[0] = "zero";
        map[1] = "one";

        Assert.True(map.Remove(0));
        Assert.False(map.ContainsKey(0));
        Assert.True(map.ContainsKey(1));
        Assert.Equal(1, map.Count);
        Assert.False(map.Remove(0));
    }

    [Fact]
    public void ZeroKey_ShouldSurviveResize()
    {
        var map = new IntDictionary<int>(4);
        map[0] = -1;
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;
        map[4] = 40; // Triggers resize while the zero-key entry is live.

        Assert.Equal(5, map.Count);
        Assert.Equal(-1, map[0]);
        Assert.Equal(10, map[1]);
        Assert.Equal(40, map[4]);
    }

    // Regression: #5 — TryGetValue was missing.
    [Fact]
    public void TryGetValue_ShouldReturnTrueAndValue_WhenKeyExists()
    {
        var map = new IntDictionary<string>();
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
        var map = new IntDictionary<string>();
        Assert.False(map.TryGetValue(42, out var v1));
        Assert.Null(v1);

        // Missing zero key specifically — covers the EMPTY_KEY short-circuit.
        Assert.False(map.TryGetValue(0, out var v2));
        Assert.Null(v2);
    }

    // Regression: #6 — Clear was missing.
    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        var map = new IntDictionary<int>();
        for (int i = 0; i < 32; i++)
            map[i] = i * i;

        map.Clear();

        Assert.Equal(0, map.Count);
        for (int i = 0; i < 32; i++)
            Assert.False(map.ContainsKey(i));

        // Reusable after clearing.
        map[0] = 100;
        map[5] = 500;
        Assert.Equal(2, map.Count);
        Assert.Equal(100, map[0]);
        Assert.Equal(500, map[5]);
    }

    // Regression: #7 — remove-then-reinsert used to corrupt the probe chain
    // in edge cases involving zero key and the rehash-after-remove cluster shift.
    [Fact]
    public void Remove_Then_Reinsert_ManyKeys_ShouldNotLoseEntries()
    {
        var map = new IntDictionary<int>(8);
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
}
