using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests for the <c>Add</c> and <c>TryAdd</c> methods on
/// <see cref="IntDictionary{TValue, THasher}"/>,
/// <see cref="LongDictionary{TValue, THasher}"/>,
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>, and
/// <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>.
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

        Assert.Single(map);
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
        Assert.Single(map);
        Assert.Equal(100, map[42]);
    }

    [Fact]
    public void IntDictionary_Add_ShouldStoreValue_ForZeroKey()
    {
        var map = new IntDictionary<string>();
        map.Add(0, "zero");

        Assert.Single(map);
        Assert.Equal("zero", map[0]);
    }

    [Fact]
    public void IntDictionary_Add_ShouldThrow_OnDuplicateZeroKey()
    {
        var map = new IntDictionary<string>();
        map.Add(0, "first");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(0, "second"));
        Assert.Single(map);
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
        Assert.Single(map);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void IntDictionary_TryAdd_ShouldReturnFalse_ForExistingKey()
    {
        var map = new IntDictionary<string>();
        map.TryAdd(5, "five");

        bool added = map.TryAdd(5, "FIVE");

        Assert.False(added);
        Assert.Single(map);
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

        Assert.Single(map);
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

        Assert.Single(map);
        Assert.Equal(100, map[42]);
    }

    [Fact]
    public void CelerityDictionary_Add_ShouldStoreValue_ForDefaultKey()
    {
        // default(int) == 0 is stored out-of-band.
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(0, "zero");

        Assert.Single(map);
        Assert.Equal("zero", map[0]);
    }

    [Fact]
    public void CelerityDictionary_Add_ShouldThrow_OnDuplicateDefaultKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(0, "first");

        Assert.Throws<ArgumentException>(() => map.Add(0, "second"));
        Assert.Single(map);
        Assert.Equal("first", map[0]);
    }

    [Fact]
    public void CelerityDictionary_Add_ShouldStoreValue_ForNullStringKey()
    {
        var map = new CelerityDictionary<string, int, StringFnV1AHasher>();
        map.Add(null!, 99);

        Assert.Single(map);
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
    //  RobinHoodDictionary — Add / TryAdd (mirror of CelerityDictionary)
    // ---------------------------------------------------------------

    [Fact]
    public void RobinHoodDictionary_Add_ShouldStoreValue_ForNewKey()
    {
        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(1, "one");

        Assert.Single(map);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void RobinHoodDictionary_Add_ShouldThrow_OnDuplicateKey()
    {
        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(1, "first");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1, "second"));
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void RobinHoodDictionary_Add_ShouldThrow_OnDuplicateKey_AndLeaveValueUnchanged()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        map.Add(42, 100);

        Assert.Throws<ArgumentException>(() => map.Add(42, 999));

        Assert.Single(map);
        Assert.Equal(100, map[42]);
    }

    [Fact]
    public void RobinHoodDictionary_Add_ShouldStoreValue_ForDefaultKey()
    {
        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(0, "zero");

        Assert.Single(map);
        Assert.Equal("zero", map[0]);
    }

    [Fact]
    public void RobinHoodDictionary_Add_ShouldThrow_OnDuplicateDefaultKey()
    {
        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(0, "first");

        Assert.Throws<ArgumentException>(() => map.Add(0, "second"));
        Assert.Single(map);
        Assert.Equal("first", map[0]);
    }

    [Fact]
    public void RobinHoodDictionary_Add_ShouldStoreValue_ForNullStringKey()
    {
        var map = new RobinHoodDictionary<string, int, StringFnV1AHasher>();
        map.Add(null!, 99);

        Assert.Single(map);
        Assert.Equal(99, map[null!]);
    }

    [Fact]
    public void RobinHoodDictionary_Add_ShouldSupportManyKeys()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>(capacity: 8);
        for (int i = 0; i <= 50; i++)
            map.Add(i, i * 10);

        Assert.Equal(51, map.Count);
        for (int i = 0; i <= 50; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void RobinHoodDictionary_TryAdd_ShouldReturnTrue_ForNewKey()
    {
        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
        bool added = map.TryAdd(5, "five");

        Assert.True(added);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void RobinHoodDictionary_TryAdd_ShouldReturnFalse_ForExistingKey()
    {
        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
        map.TryAdd(5, "five");

        bool added = map.TryAdd(5, "FIVE");

        Assert.False(added);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void RobinHoodDictionary_TryAdd_ShouldReturnFalse_ForExistingDefaultKey()
    {
        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
        map.TryAdd(0, "original");

        bool added = map.TryAdd(0, "replacement");

        Assert.False(added);
        Assert.Equal("original", map[0]);
    }

    [Fact]
    public void RobinHoodDictionary_TryAdd_ForNullStringKey_ShouldRoundTrip()
    {
        var map = new RobinHoodDictionary<string, string, StringFnV1AHasher>();
        bool added = map.TryAdd(null!, "null-val");

        Assert.True(added);

        bool addedAgain = map.TryAdd(null!, "other");
        Assert.False(addedAgain);
        Assert.Equal("null-val", map[null!]);
    }

    [Fact]
    public void RobinHoodDictionary_TryAdd_AfterClear_ShouldSucceed()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        map.TryAdd(7, 70);
        map.Clear();

        bool added = map.TryAdd(7, 700);

        Assert.True(added);
        Assert.Equal(700, map[7]);
    }

    // ---------------------------------------------------------------
    //  SwissDictionary — Add / TryAdd (mirror of CelerityDictionary)
    // ---------------------------------------------------------------

    [Fact]
    public void SwissDictionary_Add_ShouldStoreValue_ForNewKey()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(1, "one");

        Assert.Single(map);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void SwissDictionary_Add_ShouldThrow_OnDuplicateKey()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(1, "first");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1, "second"));
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void SwissDictionary_Add_ShouldThrow_OnDuplicateKey_AndLeaveValueUnchanged()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map.Add(42, 100);

        Assert.Throws<ArgumentException>(() => map.Add(42, 999));

        Assert.Single(map);
        Assert.Equal(100, map[42]);
    }

    [Fact]
    public void SwissDictionary_Add_ShouldStoreValue_ForDefaultKey()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(0, "zero");

        Assert.Single(map);
        Assert.Equal("zero", map[0]);
    }

    [Fact]
    public void SwissDictionary_Add_ShouldThrow_OnDuplicateDefaultKey()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(0, "first");

        Assert.Throws<ArgumentException>(() => map.Add(0, "second"));
        Assert.Single(map);
        Assert.Equal("first", map[0]);
    }

    [Fact]
    public void SwissDictionary_Add_ShouldStoreValue_ForNullStringKey()
    {
        var map = new SwissDictionary<string, int, StringFnV1AHasher>();
        map.Add(null!, 99);

        Assert.Single(map);
        Assert.Equal(99, map[null!]);
    }

    [Fact]
    public void SwissDictionary_Add_ShouldSupportManyKeys()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>(capacity: 16);
        for (int i = 0; i <= 50; i++)
            map.Add(i, i * 10);

        Assert.Equal(51, map.Count);
        for (int i = 0; i <= 50; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void SwissDictionary_TryAdd_ShouldReturnTrue_ForNewKey()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        bool added = map.TryAdd(5, "five");

        Assert.True(added);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void SwissDictionary_TryAdd_ShouldReturnFalse_ForExistingKey()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        map.TryAdd(5, "five");

        bool added = map.TryAdd(5, "FIVE");

        Assert.False(added);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void SwissDictionary_TryAdd_ShouldReturnFalse_ForExistingDefaultKey()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        map.TryAdd(0, "original");

        bool added = map.TryAdd(0, "replacement");

        Assert.False(added);
        Assert.Equal("original", map[0]);
    }

    [Fact]
    public void SwissDictionary_TryAdd_ForNullStringKey_ShouldRoundTrip()
    {
        var map = new SwissDictionary<string, string, StringFnV1AHasher>();
        bool added = map.TryAdd(null!, "null-val");

        Assert.True(added);

        bool addedAgain = map.TryAdd(null!, "other");
        Assert.False(addedAgain);
        Assert.Equal("null-val", map[null!]);
    }

    [Fact]
    public void SwissDictionary_TryAdd_AfterClear_ShouldSucceed()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map.TryAdd(7, 70);
        map.Clear();

        bool added = map.TryAdd(7, 700);

        Assert.True(added);
        Assert.Equal(700, map[7]);
    }

    // ---------------------------------------------------------------
    //  HashCachingDictionary — Add / TryAdd (mirror of CelerityDictionary)
    // ---------------------------------------------------------------

    [Fact]
    public void HashCachingDictionary_Add_ShouldStoreValue_ForNewKey()
    {
        var map = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(1, "one");

        Assert.Single(map);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void HashCachingDictionary_Add_ShouldThrow_OnDuplicateKey()
    {
        var map = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(1, "first");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1, "second"));
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void HashCachingDictionary_Add_ShouldThrow_OnDuplicateKey_AndLeaveValueUnchanged()
    {
        var map = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
        map.Add(42, 100);

        Assert.Throws<ArgumentException>(() => map.Add(42, 999));

        Assert.Single(map);
        Assert.Equal(100, map[42]);
    }

    [Fact]
    public void HashCachingDictionary_Add_ShouldStoreValue_ForDefaultKey()
    {
        var map = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(0, "zero");

        Assert.Single(map);
        Assert.Equal("zero", map[0]);
    }

    [Fact]
    public void HashCachingDictionary_Add_ShouldThrow_OnDuplicateDefaultKey()
    {
        var map = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();
        map.Add(0, "first");

        Assert.Throws<ArgumentException>(() => map.Add(0, "second"));
        Assert.Single(map);
        Assert.Equal("first", map[0]);
    }

    [Fact]
    public void HashCachingDictionary_Add_ShouldStoreValue_ForNullStringKey()
    {
        var map = new HashCachingDictionary<string, int, StringFnV1AHasher>();
        map.Add(null!, 99);

        Assert.Single(map);
        Assert.Equal(99, map[null!]);
    }

    [Fact]
    public void HashCachingDictionary_Add_ShouldSupportManyKeys()
    {
        var map = new HashCachingDictionary<int, int, Int32WangNaiveHasher>(capacity: 16);
        for (int i = 0; i <= 50; i++)
            map.Add(i, i * 10);

        Assert.Equal(51, map.Count);
        for (int i = 0; i <= 50; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void HashCachingDictionary_TryAdd_ShouldReturnTrue_ForNewKey()
    {
        var map = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();
        bool added = map.TryAdd(5, "five");

        Assert.True(added);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void HashCachingDictionary_TryAdd_ShouldReturnFalse_ForExistingKey()
    {
        var map = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();
        map.TryAdd(5, "five");

        bool added = map.TryAdd(5, "FIVE");

        Assert.False(added);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void HashCachingDictionary_TryAdd_ShouldReturnFalse_ForExistingDefaultKey()
    {
        var map = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();
        map.TryAdd(0, "original");

        bool added = map.TryAdd(0, "replacement");

        Assert.False(added);
        Assert.Equal("original", map[0]);
    }

    [Fact]
    public void HashCachingDictionary_TryAdd_ForNullStringKey_ShouldRoundTrip()
    {
        var map = new HashCachingDictionary<string, string, StringFnV1AHasher>();
        bool added = map.TryAdd(null!, "null-val");

        Assert.True(added);

        bool addedAgain = map.TryAdd(null!, "other");
        Assert.False(addedAgain);
        Assert.Equal("null-val", map[null!]);
    }

    [Fact]
    public void HashCachingDictionary_TryAdd_AfterClear_ShouldSucceed()
    {
        var map = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
        map.TryAdd(7, 70);
        map.Clear();

        bool added = map.TryAdd(7, 700);

        Assert.True(added);
        Assert.Equal(700, map[7]);
    }

    // ---------------------------------------------------------------
    //  LongDictionary — Add
    // ---------------------------------------------------------------

    [Fact]
    public void LongDictionary_Add_ShouldStoreValue_ForNewKey()
    {
        var map = new LongDictionary<string>();
        map.Add(1L, "one");

        Assert.Single(map);
        Assert.Equal("one", map[1L]);
    }

    [Fact]
    public void LongDictionary_Add_ShouldThrow_OnDuplicateKey()
    {
        var map = new LongDictionary<string>();
        map.Add(1L, "first");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1L, "second"));
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void LongDictionary_Add_ShouldThrow_OnDuplicateKey_AndLeaveValueUnchanged()
    {
        var map = new LongDictionary<int>();
        map.Add(42L, 100);

        Assert.Throws<ArgumentException>(() => map.Add(42L, 999));

        // Map must be untouched after the failed Add.
        Assert.Single(map);
        Assert.Equal(100, map[42L]);
    }

    [Fact]
    public void LongDictionary_Add_ShouldStoreValue_ForZeroKey()
    {
        var map = new LongDictionary<string>();
        map.Add(0L, "zero");

        Assert.Single(map);
        Assert.Equal("zero", map[0L]);
    }

    [Fact]
    public void LongDictionary_Add_ShouldThrow_OnDuplicateZeroKey()
    {
        var map = new LongDictionary<string>();
        map.Add(0L, "first");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(0L, "second"));
        Assert.Single(map);
        Assert.Equal("first", map[0L]);
    }

    [Fact]
    public void LongDictionary_Add_ShouldStoreValue_ForExtremeKeys()
    {
        // Keys spanning the full 64-bit range, including a pair sharing their
        // low 32 bits — a tripwire for accidental int-truncation on Add.
        var map = new LongDictionary<long>(capacity: 4);
        var keys = new[]
        {
            long.MinValue, -1L, 0L, 1L, int.MaxValue + 1L, long.MaxValue, 0x1_0000_0001L,
        };

        foreach (long k in keys)
            map.Add(k, k);

        Assert.Equal(keys.Length, map.Count);
        foreach (long k in keys)
            Assert.Equal(k, map[k]);
    }

    [Fact]
    public void LongDictionary_Add_ShouldSupportManyKeys()
    {
        var map = new LongDictionary<int>(capacity: 8);
        for (int i = 0; i <= 50; i++)
            map.Add(i, i * 10);

        Assert.Equal(51, map.Count);
        for (int i = 0; i <= 50; i++)
            Assert.Equal(i * 10, map[i]);
    }

    // ---------------------------------------------------------------
    //  LongDictionary — TryAdd
    // ---------------------------------------------------------------

    [Fact]
    public void LongDictionary_TryAdd_ShouldReturnTrue_ForNewKey()
    {
        var map = new LongDictionary<string>();
        bool added = map.TryAdd(5L, "five");

        Assert.True(added);
        Assert.Single(map);
        Assert.Equal("five", map[5L]);
    }

    [Fact]
    public void LongDictionary_TryAdd_ShouldReturnFalse_ForExistingKey()
    {
        var map = new LongDictionary<string>();
        map.TryAdd(5L, "five");

        bool added = map.TryAdd(5L, "FIVE");

        Assert.False(added);
        Assert.Single(map);
        Assert.Equal("five", map[5L]); // original value preserved
    }

    [Fact]
    public void LongDictionary_TryAdd_ShouldReturnTrue_ForZeroKey()
    {
        var map = new LongDictionary<string>();
        bool added = map.TryAdd(0L, "zero");

        Assert.True(added);
        Assert.True(map.ContainsKey(0L));
        Assert.Equal("zero", map[0L]);
    }

    [Fact]
    public void LongDictionary_TryAdd_ShouldReturnFalse_ForExistingZeroKey()
    {
        var map = new LongDictionary<string>();
        map.TryAdd(0L, "original");

        bool added = map.TryAdd(0L, "replacement");

        Assert.False(added);
        Assert.Equal("original", map[0L]);
    }

    [Fact]
    public void LongDictionary_TryAdd_AfterClear_ShouldSucceed()
    {
        var map = new LongDictionary<int>();
        map.TryAdd(7L, 70);
        map.Clear();

        bool added = map.TryAdd(7L, 700);

        Assert.True(added);
        Assert.Equal(700, map[7L]);
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

        Assert.Single(map);
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

        Assert.Single(map);
        Assert.Equal(2, map["key"]);

        Assert.Throws<ArgumentException>(() => map.Add("key", 3));
    }

    [Fact]
    public void LongDictionary_Add_ThenIndexerOverwrite_ShouldSucceed()
    {
        // Mirror of the Int / Celerity dictionary regression test. Pins down
        // that the indexer setter, after the ProbeForInsert(out bool wasEmpty)
        // refactor, does not bump _count when the slot was already occupied
        // by the same key. The Long path was the only dictionary not covered
        // in this file before.
        var map = new LongDictionary<int>();
        map.Add(3L, 30);
        map[3L] = 300;

        Assert.Single(map);
        Assert.Equal(300, map[3L]);

        Assert.Throws<ArgumentException>(() => map.Add(3L, 3000));
    }

    [Fact]
    public void Dictionaries_RepeatedIndexerOverwrite_ShouldKeepCountStable()
    {
        // Issue a long sequence of overwrites on every dictionary shape and
        // assert Count never drifts past the insert count. With the wasEmpty
        // out-bool on ProbeForInsert, an overwrite must hit the `wasEmpty =
        // false` branch every time after the first set; a regression that
        // wired the wrong branch would inflate Count linearly.
        var intDict = new IntDictionary<int>();
        var longDict = new LongDictionary<int>();
        var celDict = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        var rhDict = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        var swissDict = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        var hashCachingDict = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();

        const int Inserts = 32;
        const int Overwrites = 100;

        for (int i = 1; i <= Inserts; i++)
        {
            intDict[i] = i;
            longDict[(long)i] = i;
            celDict[i] = i;
            rhDict[i] = i;
            swissDict[i] = i;
            hashCachingDict[i] = i;
        }

        for (int round = 0; round < Overwrites; round++)
        {
            for (int i = 1; i <= Inserts; i++)
            {
                intDict[i] = round;
                longDict[(long)i] = round;
                celDict[i] = round;
                rhDict[i] = round;
                swissDict[i] = round;
                hashCachingDict[i] = round;
            }
        }

        Assert.Equal(Inserts, intDict.Count);
        Assert.Equal(Inserts, longDict.Count);
        Assert.Equal(Inserts, celDict.Count);
        Assert.Equal(Inserts, rhDict.Count);
        Assert.Equal(Inserts, swissDict.Count);
        Assert.Equal(Inserts, hashCachingDict.Count);
    }

    // ---------------------------------------------------------------
    //  SmallDictionary — Add / TryAdd
    // ---------------------------------------------------------------

    [Fact]
    public void SmallDictionary_Add_ShouldStoreValue_ForNewKey()
    {
        var map = new SmallDictionary<int, string>();
        map.Add(1, "one");

        Assert.Single(map);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void SmallDictionary_Add_ShouldThrow_OnDuplicateKey()
    {
        var map = new SmallDictionary<int, string>();
        map.Add(1, "first");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1, "second"));
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void SmallDictionary_Add_ShouldThrow_OnDuplicateKey_AndLeaveValueUnchanged()
    {
        var map = new SmallDictionary<int, int>();
        map.Add(42, 100);

        Assert.Throws<ArgumentException>(() => map.Add(42, 999));

        Assert.Single(map);
        Assert.Equal(100, map[42]);
    }

    [Fact]
    public void SmallDictionary_Add_ShouldStoreValue_ForZeroKey()
    {
        var map = new SmallDictionary<int, string>();
        map.Add(0, "zero");

        Assert.Single(map);
        Assert.Equal("zero", map[0]);
    }

    [Fact]
    public void SmallDictionary_TryAdd_ShouldReturnTrue_ForNewKey()
    {
        var map = new SmallDictionary<int, string>();

        Assert.True(map.TryAdd(1, "one"));
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void SmallDictionary_TryAdd_ShouldReturnFalse_OnDuplicateKey_AndNotOverwrite()
    {
        var map = new SmallDictionary<int, string>();
        map.TryAdd(1, "first");

        Assert.False(map.TryAdd(1, "second"));
        Assert.Single(map);
        Assert.Equal("first", map[1]);
    }
}
