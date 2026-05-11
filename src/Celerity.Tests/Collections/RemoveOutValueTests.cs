using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests for the BCL-parity <c>Remove(key, out value)</c> overload on both
/// <see cref="IntDictionary{TValue}"/> / <see cref="IntDictionary{TValue, THasher}"/>
/// and <see cref="CelerityDictionary{TKey, TValue, THasher}"/>. These tests cover
/// the standard-key path, the out-of-band zero / default-key path, the
/// not-found path, and the rehash-after-remove path under forced collision.
/// </summary>
public class RemoveOutValueTests
{
    /// <summary>
    /// A test-only hasher that returns a constant value for every key,
    /// forcing every insert into a single linear-probing chain so the
    /// rehash-after-remove logic runs the longest possible cluster.
    /// </summary>
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    // ---------------------------------------------------------------
    //  IntDictionary
    // ---------------------------------------------------------------

    [Fact]
    public void IntDictionary_RemoveOutValue_ReturnsTrueAndCapturedValue_ForExistingKey()
    {
        var map = new IntDictionary<string>();
        map[7] = "seven";

        bool removed = map.Remove(7, out string? captured);

        Assert.True(removed);
        Assert.Equal("seven", captured);
        Assert.False(map.ContainsKey(7));
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void IntDictionary_RemoveOutValue_ReturnsFalseAndDefault_ForMissingKey()
    {
        var map = new IntDictionary<string>();
        map[1] = "one";

        bool removed = map.Remove(99, out string? captured);

        Assert.False(removed);
        Assert.Null(captured);
        Assert.Equal(1, map.Count);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void IntDictionary_RemoveOutValue_ReturnsFalseAndDefault_ForMissingKey_OnEmptyMap()
    {
        var map = new IntDictionary<int>();

        bool removed = map.Remove(42, out int captured);

        Assert.False(removed);
        Assert.Equal(default, captured);
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void IntDictionary_RemoveOutValue_CapturesZeroKeyValue()
    {
        // The zero key is stored out-of-band; the out-value must surface that
        // dedicated slot before it is cleared.
        var map = new IntDictionary<string>();
        map[0] = "zero";
        map[1] = "one";

        Assert.Equal(2, map.Count);

        bool removed = map.Remove(0, out string? captured);

        Assert.True(removed);
        Assert.Equal("zero", captured);
        Assert.False(map.ContainsKey(0));
        Assert.Equal(1, map.Count);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void IntDictionary_RemoveOutValue_ReturnsFalse_WhenZeroKeyAbsent()
    {
        var map = new IntDictionary<string>();
        map[1] = "one";

        bool removed = map.Remove(0, out string? captured);

        Assert.False(removed);
        Assert.Null(captured);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void IntDictionary_RemoveOutValue_CapturesDefaultValueType_Correctly()
    {
        // Verify value-type values don't surface as an unexpected sentinel
        // (e.g. the captured-value slot must not be conflated with EMPTY_VALUE).
        var map = new IntDictionary<int>();
        map[5] = 0;

        bool removed = map.Remove(5, out int captured);

        Assert.True(removed);
        Assert.Equal(0, captured);
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void IntDictionary_RemoveOutValue_RehashesCluster_UnderFullCollision()
    {
        // Force every key into a single linear-probing chain, then remove
        // an interior element. The rehash-after-remove path must run; the
        // captured value must be the original interior value, not a value
        // that was shuffled during the rehash.
        var map = new IntDictionary<string, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            map[i] = $"v{i}";

        bool removed = map.Remove(3, out string? captured);

        Assert.True(removed);
        Assert.Equal("v3", captured);
        Assert.Equal(5, map.Count);
        Assert.False(map.ContainsKey(3));

        // All other keys must still be reachable after the rehash.
        for (int i = 1; i <= 6; i++)
        {
            if (i == 3) continue;
            Assert.True(map.ContainsKey(i));
            Assert.Equal($"v{i}", map[i]);
        }
    }

    [Fact]
    public void IntDictionary_RemoveOutValue_AndReinsertSameKey_Works()
    {
        var map = new IntDictionary<int>();
        map[10] = 100;

        Assert.True(map.Remove(10, out int captured));
        Assert.Equal(100, captured);

        map[10] = 200;
        Assert.Equal(200, map[10]);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void IntDictionary_RemoveOutValue_BumpsVersion_AndInvalidatesEnumerator()
    {
        // Removing via the new overload must still trip the version-counter
        // mid-enumeration guard, exactly like the void-Remove overload.
        var map = new IntDictionary<int>();
        for (int i = 1; i <= 4; i++)
            map[i] = i;

        var e = map.GetEnumerator();
        Assert.True(e.MoveNext());

        Assert.True(map.Remove(2, out int captured));
        Assert.Equal(2, captured);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    // ---------------------------------------------------------------
    //  CelerityDictionary
    // ---------------------------------------------------------------

    [Fact]
    public void CelerityDictionary_RemoveOutValue_ReturnsTrueAndCapturedValue_ForExistingKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[7] = "seven";

        bool removed = map.Remove(7, out string? captured);

        Assert.True(removed);
        Assert.Equal("seven", captured);
        Assert.False(map.ContainsKey(7));
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void CelerityDictionary_RemoveOutValue_ReturnsFalseAndDefault_ForMissingKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";

        bool removed = map.Remove(99, out string? captured);

        Assert.False(removed);
        Assert.Null(captured);
        Assert.Equal(1, map.Count);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void CelerityDictionary_RemoveOutValue_CapturesDefaultIntKeyValue()
    {
        // default(int) == 0 lives in the out-of-band slot for Celerity too.
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";
        map[1] = "one";

        bool removed = map.Remove(0, out string? captured);

        Assert.True(removed);
        Assert.Equal("zero", captured);
        Assert.False(map.ContainsKey(0));
        Assert.Equal(1, map.Count);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void CelerityDictionary_RemoveOutValue_CapturesNullStringKeyValue()
    {
        // For reference-type keys the out-of-band slot stores the null key;
        // the captured value must come from that slot.
        var map = new CelerityDictionary<string, int, StringFnV1AHasher>();
        map[null!] = 42;
        map["alpha"] = 1;

        bool removed = map.Remove(null!, out int captured);

        Assert.True(removed);
        Assert.Equal(42, captured);
        Assert.False(map.ContainsKey(null!));
        Assert.Equal(1, map.Count);
        Assert.Equal(1, map["alpha"]);
    }

    [Fact]
    public void CelerityDictionary_RemoveOutValue_ReturnsFalse_WhenDefaultKeyAbsent()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";

        bool removed = map.Remove(0, out string? captured);

        Assert.False(removed);
        Assert.Null(captured);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void CelerityDictionary_RemoveOutValue_RehashesCluster_UnderFullCollision()
    {
        var map = new CelerityDictionary<string, int, ConstantStringHasher>(16);
        map["a"] = 1;
        map["b"] = 2;
        map["c"] = 3;
        map["d"] = 4;
        map["e"] = 5;

        bool removed = map.Remove("c", out int captured);

        Assert.True(removed);
        Assert.Equal(3, captured);
        Assert.Equal(4, map.Count);
        Assert.False(map.ContainsKey("c"));

        Assert.Equal(1, map["a"]);
        Assert.Equal(2, map["b"]);
        Assert.Equal(4, map["d"]);
        Assert.Equal(5, map["e"]);
    }

    [Fact]
    public void CelerityDictionary_RemoveOutValue_BumpsVersion_AndInvalidatesEnumerator()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 4; i++)
            map[i] = i;

        var e = map.GetEnumerator();
        Assert.True(e.MoveNext());

        Assert.True(map.Remove(2, out int captured));
        Assert.Equal(2, captured);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    // ---------------------------------------------------------------
    //  Cross-cutting: void Remove still works the same
    // ---------------------------------------------------------------

    [Fact]
    public void IntDictionary_VoidRemove_StillReturnsTrueForFoundKey()
    {
        // Regression: the void Remove overload now delegates to the
        // out-value overload internally; it must continue to behave
        // identically to its previous standalone implementation.
        var map = new IntDictionary<string>();
        map[1] = "one";

        Assert.True(map.Remove(1));
        Assert.False(map.ContainsKey(1));
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void CelerityDictionary_VoidRemove_StillReturnsTrueForFoundKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";

        Assert.True(map.Remove(1));
        Assert.False(map.ContainsKey(1));
        Assert.Equal(0, map.Count);
    }
}
