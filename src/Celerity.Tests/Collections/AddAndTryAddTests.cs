using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests for the <c>Add</c> and <c>TryAdd</c> methods on
/// <see cref="IntDictionary{TValue, THasher}"/> and
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>.
/// </summary>
public class AddAndTryAddTests
{
    // ---------------------------------------------------------------
    //  IntDictionary — Add
    // ---------------------------------------------------------------

    [Fact]
    public void IntDictionary_Add_ShouldStoreValue_ForNewKey()
    {
        var map = new IntDictionary<string>();
        map.Add(1, "one");

        Assert.Equal(1, map.Count);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void IntDictionary_Add_ShouldThrow_OnDuplicateKey()
    {
        var map = new IntDictionary<string>();
        map.Add(1, "first");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1, "second"));
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void IntDictionary_Add_ShouldThrow_OnDuplicateKey_AndLeaveValueUnchanged()
    {
        var map = new IntDictionary<int>();
        map.Add(42, 100);

        Assert.Throws<ArgumentException>(() => map.Add(42, 999));

        // Map must be untouched after the failed Add.
        Assert.Equal(1, map.Count);
        Assert.Equal(100, map[42]);
    }

    [Fact]
    public void IntDictionary_Add_ShouldStoreValue_ForZeroKey()
    {
        var map = new IntDictionary<string>();
        map.Add(0, "zero");

        Assert.Equal(1, map.Count);
        Assert.Equal("zero", map[0]);
    }

    [Fact]
    public void IntDictionary_Add_ShouldThrow_OnDuplicateZeroKey()
    {
        var map = new IntDictionary<string>();
        map.Add(0, "first");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(0, "second"));
        Assert.Equal(1, map.Count);
        Assert.Equal("first", map[0]);
    }

    [Fact]
    public void IntDictionary_Add_ShouldSupportManyKeys()
    {
        var map = new IntDictionary<int>(capacity: 8);
        for (int i = 0; i <= 50; i++)
            map.Add(i, i * 10);

        Assert.Equal(51, map.Count);
        for (int i = 0; i <= 50; i++)
            Assert.Equal(i * 10, map[i]);
    }

    // ---------------------------------------------------------------
    //  IntDictionary — TryAdd
    // ---------------------------------------------------------------

    [Fact]
    public void IntDictionary_TryAdd_ShouldReturnTrue_ForNewKey()
    {
        var map = new IntDictionary<string>();
        bool added = map.TryAdd(5, "five");

        Assert.True(added);
        Assert.Equal(1, map.Count);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void IntDictionary_TryAdd_ShouldReturnFalse_ForExistingKey()
    {
        var map = new IntDictionary<string>();
        map.TryAdd(5, "five");

        bool added = map.TryAdd(5, "FIVE");

        Assert.False(added);
        Assert.Equal(1, map.Count);
        Assert.Equal("five", map[5]); // original value preserved
    }

    [Fact]
    public void IntDictionary_TryAdd_ShouldReturnTrue_ForZeroKey()
    {
        var map = new IntDictionary<string>();
        bool added = map.TryAdd(0, "zero");

        Assert.True(added);
        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
    }

    [Fact]
    public void IntDictionary_TryAdd_ShouldReturnFalse_ForExistingZeroKey()
    {
        var map = new IntDictionary<string>();
        map.TryAdd(0, "original");

        bool added = map.TryAdd(0, "replacement");

        Assert.False(added);
        Assert.Equal("original", map[0]);
    }

    [Fact]
    public void IntDictionary_TryAdd_AfterClear_ShouldSucceed()
    {
        var map = new IntDictionary<int>();
        map.TryAdd(7, 70);
        map.Clear();

        bool added = map.TryAdd(7, 700);

        Assert.True(added);
        Assert.Equal(700, map[7]);
    }

    // ---------------------------------------------------------------
    //  CelerityDictionary — Add
    // ---------------------------------------------------------------

    [Fact]
    public void CelerityDictionary_Add_ShouldStoreValue_ForNewKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(1, "one");

        Assert.Equal(1, map.Count);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void CelerityDictionary_Add_ShouldThrow_OnDuplicateKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(1, "first");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1, "second"));
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void CelerityDictionary_Add_ShouldThrow_OnDuplicateKey_AndLeaveValueUnchanged()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map.Add(42, 100);

        Assert.Throws<ArgumentException>(() => map.Add(42, 999));

        Assert.Equal(1, map.Count);
        Assert.Equal(100, map[42]);
    }

    [Fact]
    public void CelerityDictionary_Add_ShouldStoreValue_ForDefaultKey()
    {
        // default(int) == 0 is stored out-of-band.
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(0, "zero");

        Assert.Equal(1, map.Count);
        Assert.Equal("zero", map[0]);
    }

    [Fact]
    public void CelerityDictionary_Add_ShouldThrow_OnDuplicateDefaultKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(0, "first");

        Assert.Throws<ArgumentException>(() => map.Add(0, "second"));
        Assert.Equal(1, map.Count);
        Assert.Equal("first", map[0]);
    }

    [Fact]
    public void CelerityDictionary_Add_ShouldStoreValue_ForNullStringKey()
    {
        var map = new CelerityDictionary<string, int, StringFnV1AHasher>();
        map.Add(null!, 99);

        Assert.Equal(1, map.Count);
        Assert.Equal(99, map[null!]);
    }

    [Fact]
    public void CelerityDictionary_Add_ShouldThrow_OnDuplicateNullStringKey()
    {
        var map = new CelerityDictionary<string, int, StringFnV1AHasher>();
        map.Add(null!, 1);

        Assert.Throws<ArgumentException>(() => map.Add(null!, 2));
        Assert.Equal(1, map[null!]);
    }

    [Fact]
    public void CelerityDictionary_Add_ShouldSupportManyKeys()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 8);
        for (int i = 0; i <= 50; i++)
            map.Add(i, i * 10);

        Assert.Equal(51, map.Count);
        for (int i = 0; i <= 50; i++)
            Assert.Equal(i * 10, map[i]);
    }

    // ---------------------------------------------------------------
    //  CelerityDictionary — TryAdd
    // ---------------------------------------------------------------

    [Fact]
    public void CelerityDictionary_TryAdd_ShouldReturnTrue_ForNewKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        bool added = map.TryAdd(5, "five");

        Assert.True(added);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void CelerityDictionary_TryAdd_ShouldReturnFalse_ForExistingKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map.TryAdd(5, "five");

        bool added = map.TryAdd(5, "FIVE");

        Assert.False(added);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void CelerityDictionary_TryAdd_ShouldReturnTrue_ForDefaultKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        bool added = map.TryAdd(0, "zero");

        Assert.True(added);
        Assert.Equal("zero", map[0]);
    }

    [Fact]
    public void CelerityDictionary_TryAdd_ShouldReturnFalse_ForExistingDefaultKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map.TryAdd(0, "original");

        bool added = map.TryAdd(0, "replacement");

        Assert.False(added);
        Assert.Equal("original", map[0]);
    }

    [Fact]
    public void CelerityDictionary_TryAdd_ForNullStringKey_ShouldRoundTrip()
    {
        var map = new CelerityDictionary<string, string, StringFnV1AHasher>();
        bool added = map.TryAdd(null!, "null-val");

        Assert.True(added);

        bool addedAgain = map.TryAdd(null!, "other");
        Assert.False(addedAgain);
        Assert.Equal("null-val", map[null!]);
    }

    [Fact]
    public void CelerityDictionary_TryAdd_AfterClear_ShouldSucceed()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map.TryAdd(7, 70);
        map.Clear();

        bool added = map.TryAdd(7, 700);

        Assert.True(added);
        Assert.Equal(700, map[7]);
    }

    // ---------------------------------------------------------------
    //  Mix of Add/indexer — verify they compose correctly
    // ---------------------------------------------------------------

    [Fact]
    public void IntDictionary_Add_ThenIndexerOverwrite_ShouldSucceed()
    {
        // Add populates; indexer setter overwrites without throwing.
        var map = new IntDictionary<int>();
        map.Add(3, 30);
        map[3] = 300;

        Assert.Equal(1, map.Count);
        Assert.Equal(300, map[3]);

        // A second Add must now throw again.
        Assert.Throws<ArgumentException>(() => map.Add(3, 3000));
    }

    [Fact]
    public void CelerityDictionary_Add_ThenIndexerOverwrite_ShouldSucceed()
    {
        var map = new CelerityDictionary<string, int, StringFnV1AHasher>();
        map.Add("key", 1);
        map["key"] = 2;

        Assert.Equal(1, map.Count);
        Assert.Equal(2, map["key"]);

        Assert.Throws<ArgumentException>(() => map.Add("key", 3));
    }
}
