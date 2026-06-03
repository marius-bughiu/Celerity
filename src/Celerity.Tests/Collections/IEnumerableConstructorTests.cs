using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests for the <c>IEnumerable&lt;KeyValuePair&lt;TKey, TValue&gt;&gt;</c>
/// constructor on <see cref="IntDictionary{TValue, THasher}"/> and
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>.
///
/// Covers BCL <c>Dictionary&lt;,&gt;</c> parity: null source throws
/// <see cref="ArgumentNullException"/>, duplicate keys throw
/// <see cref="ArgumentException"/>, collection sources size the backing
/// storage from their <c>Count</c>, non-collection enumerables also copy
/// correctly, and the out-of-band zero / default-key slot is populated.
/// </summary>
public class IEnumerableConstructorTests
{
    // ──────────────────────────────────────────────────────────────
    //  IntDictionary — source argument validation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntDictionary_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<KeyValuePair<int, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new IntDictionary<string>(source!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void IntDictionary_ShouldThrow_OnDuplicateKeysInSource()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(1, "one-again"),
        };

        var ex = Assert.Throws<ArgumentException>(() => new IntDictionary<string>(source));
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void IntDictionary_ShouldStillValidate_LoadFactor_WhenConstructedFromSource()
    {
        var source = new[] { new KeyValuePair<int, int>(1, 1) };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IntDictionary<int>(source, loadFactor: 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IntDictionary<int>(source, loadFactor: 0f));
    }

    // ──────────────────────────────────────────────────────────────
    //  IntDictionary — happy path
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntDictionary_ShouldSupportEmptySource()
    {
        var map = new IntDictionary<string>(Array.Empty<KeyValuePair<int, string>>());

        Assert.Empty(map);
        Assert.False(map.ContainsKey(0));
        Assert.False(map.ContainsKey(1));
    }

    [Fact]
    public void IntDictionary_ShouldCopyAllEntries_FromArraySource()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(3, "three"),
        };

        var map = new IntDictionary<string>(source);

        Assert.Equal(3, map.Count);
        Assert.Equal("one", map[1]);
        Assert.Equal("two", map[2]);
        Assert.Equal("three", map[3]);
    }

    [Fact]
    public void IntDictionary_ShouldCopyAllEntries_FromListSource()
    {
        var source = new List<KeyValuePair<int, int>>
        {
            new(10, 100),
            new(20, 200),
            new(30, 300),
        };

        var map = new IntDictionary<int>(source);

        Assert.Equal(3, map.Count);
        Assert.Equal(100, map[10]);
        Assert.Equal(200, map[20]);
        Assert.Equal(300, map[30]);
    }

    [Fact]
    public void IntDictionary_ShouldCopyAllEntries_FromNonCollectionEnumerable()
    {
        // A deferred sequence is NOT an ICollection<T>, so the ctor's
        // Count-based sizing fast path does not apply and it falls back to
        // the capacity parameter.
        IEnumerable<KeyValuePair<int, string>> NonCollectionSource()
        {
            yield return new KeyValuePair<int, string>(1, "one");
            yield return new KeyValuePair<int, string>(2, "two");
            yield return new KeyValuePair<int, string>(3, "three");
        }

        var map = new IntDictionary<string>(NonCollectionSource());

        Assert.Equal(3, map.Count);
        Assert.Equal("one", map[1]);
        Assert.Equal("two", map[2]);
        Assert.Equal("three", map[3]);
    }

    [Fact]
    public void IntDictionary_ShouldCaptureZeroKey_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(0, "zero"),
            new KeyValuePair<int, string>(1, "one"),
        };

        var map = new IntDictionary<string>(source);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void IntDictionary_ShouldDetectDuplicateZeroKey()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(0, "zero"),
            new KeyValuePair<int, string>(0, "zero-again"),
        };

        Assert.Throws<ArgumentException>(() => new IntDictionary<string>(source));
    }

    [Fact]
    public void IntDictionary_ShouldCopy_LargeSource_WithoutDataLoss()
    {
        var source = Enumerable.Range(1, 500)
            .Select(i => new KeyValuePair<int, int>(i, i * 2))
            .ToArray();

        var map = new IntDictionary<int>(source);

        Assert.Equal(500, map.Count);
        for (int i = 1; i <= 500; i++)
            Assert.Equal(i * 2, map[i]);
    }

    [Fact]
    public void IntDictionary_ShouldBeIndependent_OfSourceAfterConstruction()
    {
        var source = new List<KeyValuePair<int, string>>
        {
            new(1, "one"),
            new(2, "two"),
        };

        var map = new IntDictionary<string>(source);

        // Mutating the source after construction should not affect the map.
        source.Add(new KeyValuePair<int, string>(3, "three"));
        source[0] = new KeyValuePair<int, string>(1, "MUTATED");

        Assert.Equal(2, map.Count);
        Assert.Equal("one", map[1]);
        Assert.False(map.ContainsKey(3));
    }

    [Fact]
    public void IntDictionary_ShouldHonor_Explicit_CapacityLargerThanSourceCount()
    {
        var source = new[] { new KeyValuePair<int, int>(1, 1) };

        // Should not throw and should behave correctly regardless of whether
        // the caller-requested capacity dominates the source count.
        var map = new IntDictionary<int>(source, capacity: 1024);

        Assert.Single(map);
        Assert.Equal(1, map[1]);
    }

    // ──────────────────────────────────────────────────────────────
    //  IntDictionary<TValue, THasher> base class — source ctor
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntDictionary_Generic_ShouldCopyAllEntries_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(10, "ten"),
            new KeyValuePair<int, string>(20, "twenty"),
        };

        var map = new IntDictionary<string, Int32WangNaiveHasher>(source);

        Assert.Equal(2, map.Count);
        Assert.Equal("ten", map[10]);
        Assert.Equal("twenty", map[20]);
    }

    [Fact]
    public void IntDictionary_Generic_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<KeyValuePair<int, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new IntDictionary<string, Int32WangNaiveHasher>(source!));

        Assert.Equal("source", ex.ParamName);
    }

    // ──────────────────────────────────────────────────────────────
    //  LongDictionary — source argument validation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void LongDictionary_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<KeyValuePair<long, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new LongDictionary<string>(source!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void LongDictionary_ShouldThrow_OnDuplicateKeysInSource()
    {
        var source = new[]
        {
            new KeyValuePair<long, string>(1, "one"),
            new KeyValuePair<long, string>(2, "two"),
            new KeyValuePair<long, string>(1, "one-again"),
        };

        var ex = Assert.Throws<ArgumentException>(() => new LongDictionary<string>(source));
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void LongDictionary_ShouldStillValidate_LoadFactor_WhenConstructedFromSource()
    {
        var source = new[] { new KeyValuePair<long, int>(1, 1) };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LongDictionary<int>(source, loadFactor: 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LongDictionary<int>(source, loadFactor: 0f));
    }

    // ──────────────────────────────────────────────────────────────
    //  LongDictionary — happy path
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void LongDictionary_ShouldSupportEmptySource()
    {
        var map = new LongDictionary<string>(Array.Empty<KeyValuePair<long, string>>());

        Assert.Empty(map);
        Assert.False(map.ContainsKey(0L));
        Assert.False(map.ContainsKey(1L));
    }

    [Fact]
    public void LongDictionary_ShouldCopyAllEntries_FromArraySource()
    {
        var source = new[]
        {
            new KeyValuePair<long, string>(1, "one"),
            new KeyValuePair<long, string>(2, "two"),
            new KeyValuePair<long, string>(3, "three"),
        };

        var map = new LongDictionary<string>(source);

        Assert.Equal(3, map.Count);
        Assert.Equal("one", map[1L]);
        Assert.Equal("two", map[2L]);
        Assert.Equal("three", map[3L]);
    }

    [Fact]
    public void LongDictionary_ShouldCopyAllEntries_FromListSource()
    {
        var source = new List<KeyValuePair<long, int>>
        {
            new(10, 100),
            new(20, 200),
            new(30, 300),
        };

        var map = new LongDictionary<int>(source);

        Assert.Equal(3, map.Count);
        Assert.Equal(100, map[10L]);
        Assert.Equal(200, map[20L]);
        Assert.Equal(300, map[30L]);
    }

    [Fact]
    public void LongDictionary_ShouldCopyAllEntries_FromNonCollectionEnumerable()
    {
        // A deferred sequence is NOT an ICollection<T>, so the ctor's
        // Count-based sizing fast path does not apply and it falls back to
        // the capacity parameter.
        IEnumerable<KeyValuePair<long, string>> NonCollectionSource()
        {
            yield return new KeyValuePair<long, string>(1, "one");
            yield return new KeyValuePair<long, string>(2, "two");
            yield return new KeyValuePair<long, string>(3, "three");
        }

        var map = new LongDictionary<string>(NonCollectionSource());

        Assert.Equal(3, map.Count);
        Assert.Equal("one", map[1L]);
        Assert.Equal("two", map[2L]);
        Assert.Equal("three", map[3L]);
    }

    [Fact]
    public void LongDictionary_ShouldCaptureZeroKey_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<long, string>(0, "zero"),
            new KeyValuePair<long, string>(1, "one"),
        };

        var map = new LongDictionary<string>(source);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(0L));
        Assert.Equal("zero", map[0L]);
        Assert.Equal("one", map[1L]);
    }

    [Fact]
    public void LongDictionary_ShouldDetectDuplicateZeroKey()
    {
        var source = new[]
        {
            new KeyValuePair<long, string>(0, "zero"),
            new KeyValuePair<long, string>(0, "zero-again"),
        };

        Assert.Throws<ArgumentException>(() => new LongDictionary<string>(source));
    }

    [Fact]
    public void LongDictionary_ShouldCopy_LargeSource_WithoutDataLoss()
    {
        var source = Enumerable.Range(1, 500)
            .Select(i => new KeyValuePair<long, int>(i, i * 2))
            .ToArray();

        var map = new LongDictionary<int>(source);

        Assert.Equal(500, map.Count);
        for (int i = 1; i <= 500; i++)
            Assert.Equal(i * 2, map[i]);
    }

    [Fact]
    public void LongDictionary_ShouldCapture_ExtremeKeys_FromSource()
    {
        // A signed-64-bit key set the source ctor must copy without 32-bit
        // truncation — including keys that share their low 32 bits.
        var source = new[]
        {
            new KeyValuePair<long, long>(long.MinValue, 1),
            new KeyValuePair<long, long>(-1L, 2),
            new KeyValuePair<long, long>(0L, 3),
            new KeyValuePair<long, long>(int.MaxValue + 1L, 4),
            new KeyValuePair<long, long>(long.MaxValue, 5),
            new KeyValuePair<long, long>(0x1_0000_0001L, 6), // shares low 32 bits with 1L (absent)
        };

        var map = new LongDictionary<long>(source);

        Assert.Equal(6, map.Count);
        Assert.Equal(1, map[long.MinValue]);
        Assert.Equal(2, map[-1L]);
        Assert.Equal(3, map[0L]);
        Assert.Equal(4, map[int.MaxValue + 1L]);
        Assert.Equal(5, map[long.MaxValue]);
        Assert.Equal(6, map[0x1_0000_0001L]);
    }

    [Fact]
    public void LongDictionary_ShouldBeIndependent_OfSourceAfterConstruction()
    {
        var source = new List<KeyValuePair<long, string>>
        {
            new(1, "one"),
            new(2, "two"),
        };

        var map = new LongDictionary<string>(source);

        // Mutating the source after construction should not affect the map.
        source.Add(new KeyValuePair<long, string>(3, "three"));
        source[0] = new KeyValuePair<long, string>(1, "MUTATED");

        Assert.Equal(2, map.Count);
        Assert.Equal("one", map[1L]);
        Assert.False(map.ContainsKey(3L));
    }

    [Fact]
    public void LongDictionary_ShouldHonor_Explicit_CapacityLargerThanSourceCount()
    {
        var source = new[] { new KeyValuePair<long, int>(1, 1) };

        // Should not throw and should behave correctly regardless of whether
        // the caller-requested capacity dominates the source count.
        var map = new LongDictionary<int>(source, capacity: 1024);

        Assert.Single(map);
        Assert.Equal(1, map[1L]);
    }

    // ──────────────────────────────────────────────────────────────
    //  LongDictionary<TValue, THasher> base class — source ctor
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void LongDictionary_Generic_ShouldCopyAllEntries_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<long, string>(10, "ten"),
            new KeyValuePair<long, string>(20, "twenty"),
        };

        var map = new LongDictionary<string, Int64WangNaiveHasher>(source);

        Assert.Equal(2, map.Count);
        Assert.Equal("ten", map[10L]);
        Assert.Equal("twenty", map[20L]);
    }

    [Fact]
    public void LongDictionary_Generic_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<KeyValuePair<long, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new LongDictionary<string, Int64WangNaiveHasher>(source!));

        Assert.Equal("source", ex.ParamName);
    }

    // ──────────────────────────────────────────────────────────────
    //  CelerityDictionary — source argument validation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void CelerityDictionary_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<KeyValuePair<int, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CelerityDictionary<int, string, Int32WangNaiveHasher>(source!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void CelerityDictionary_ShouldThrow_OnDuplicateKeysInSource()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("a", 3),
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            new CelerityDictionary<string, int, StringFnV1AHasher>(source));
        Assert.Contains("a", ex.Message);
    }

    [Fact]
    public void CelerityDictionary_ShouldStillValidate_LoadFactor_WhenConstructedFromSource()
    {
        var source = new[] { new KeyValuePair<int, int>(1, 1) };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CelerityDictionary<int, int, Int32WangNaiveHasher>(source, loadFactor: 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CelerityDictionary<int, int, Int32WangNaiveHasher>(source, loadFactor: 0f));
    }

    // ──────────────────────────────────────────────────────────────
    //  CelerityDictionary — happy path
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void CelerityDictionary_ShouldSupportEmptySource()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>(
            Array.Empty<KeyValuePair<int, string>>());

        Assert.Empty(map);
        Assert.False(map.ContainsKey(0));
        Assert.False(map.ContainsKey(1));
    }

    [Fact]
    public void CelerityDictionary_ShouldCopyAllEntries_FromArraySource()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>("alpha", 1),
            new KeyValuePair<string, int>("beta", 2),
            new KeyValuePair<string, int>("gamma", 3),
        };

        var map = new CelerityDictionary<string, int, StringFnV1AHasher>(source);

        Assert.Equal(3, map.Count);
        Assert.Equal(1, map["alpha"]);
        Assert.Equal(2, map["beta"]);
        Assert.Equal(3, map["gamma"]);
    }

    [Fact]
    public void CelerityDictionary_ShouldCopyAllEntries_FromNonCollectionEnumerable()
    {
        IEnumerable<KeyValuePair<int, string>> NonCollectionSource()
        {
            yield return new KeyValuePair<int, string>(1, "one");
            yield return new KeyValuePair<int, string>(2, "two");
            yield return new KeyValuePair<int, string>(3, "three");
        }

        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>(NonCollectionSource());

        Assert.Equal(3, map.Count);
        Assert.Equal("one", map[1]);
        Assert.Equal("two", map[2]);
        Assert.Equal("three", map[3]);
    }

    [Fact]
    public void CelerityDictionary_ShouldCaptureDefaultIntKey_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(0, "zero"),
            new KeyValuePair<int, string>(1, "one"),
        };

        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>(source);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void CelerityDictionary_ShouldCaptureNullStringKey_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>(null!, 0),
            new KeyValuePair<string, int>("one", 1),
            new KeyValuePair<string, int>("two", 2),
        };

        var map = new CelerityDictionary<string, int, StringFnV1AHasher>(source);

        Assert.Equal(3, map.Count);
        Assert.True(map.ContainsKey(null!));
        Assert.Equal(0, map[null!]);
        Assert.Equal(1, map["one"]);
        Assert.Equal(2, map["two"]);
    }

    [Fact]
    public void CelerityDictionary_ShouldDetectDuplicateDefaultKey()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(0, "zero"),
            new KeyValuePair<int, string>(0, "zero-again"),
        };

        Assert.Throws<ArgumentException>(() =>
            new CelerityDictionary<int, string, Int32WangNaiveHasher>(source));
    }

    [Fact]
    public void CelerityDictionary_ShouldCopy_LargeSource_WithoutDataLoss()
    {
        var source = Enumerable.Range(1, 500)
            .Select(i => new KeyValuePair<string, int>($"key-{i}", i))
            .ToArray();

        var map = new CelerityDictionary<string, int, StringFnV1AHasher>(source);

        Assert.Equal(500, map.Count);
        for (int i = 1; i <= 500; i++)
            Assert.Equal(i, map[$"key-{i}"]);
    }

    [Fact]
    public void CelerityDictionary_ShouldBeIndependent_OfSourceAfterConstruction()
    {
        var source = new List<KeyValuePair<int, string>>
        {
            new(1, "one"),
            new(2, "two"),
        };

        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>(source);

        source.Add(new KeyValuePair<int, string>(3, "three"));
        source[0] = new KeyValuePair<int, string>(1, "MUTATED");

        Assert.Equal(2, map.Count);
        Assert.Equal("one", map[1]);
        Assert.False(map.ContainsKey(3));
    }

    // ──────────────────────────────────────────────────────────────
    //  Copy from existing Celerity dictionaries
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntDictionary_ShouldCopy_FromAnotherIntDictionary()
    {
        // IntDictionary itself enumerates KeyValuePair<int, TValue?>, so a
        // projection to the non-null KVP shape is required to round-trip.
        var original = new IntDictionary<string>();
        original.Add(0, "zero");
        original.Add(1, "one");
        original.Add(2, "two");

        var copy = new IntDictionary<string>(
            original.Select(kvp => new KeyValuePair<int, string>(kvp.Key, kvp.Value!)));

        Assert.Equal(3, copy.Count);
        Assert.Equal("zero", copy[0]);
        Assert.Equal("one", copy[1]);
        Assert.Equal("two", copy[2]);
    }

    [Fact]
    public void CelerityDictionary_ShouldCopy_FromAnotherCelerityDictionary()
    {
        var original = new CelerityDictionary<string, int, StringFnV1AHasher>();
        original.Add("alpha", 1);
        original.Add("beta", 2);

        var copy = new CelerityDictionary<string, int, StringFnV1AHasher>(
            original.Select(kvp => new KeyValuePair<string, int>(kvp.Key, kvp.Value)));

        Assert.Equal(2, copy.Count);
        Assert.Equal(1, copy["alpha"]);
        Assert.Equal(2, copy["beta"]);
    }

    [Fact]
    public void LongDictionary_ShouldCopy_FromAnotherLongDictionary()
    {
        // LongDictionary itself enumerates KeyValuePair<long, TValue?>, so a
        // projection to the non-null KVP shape is required to round-trip.
        var original = new LongDictionary<string>();
        original.Add(0L, "zero");
        original.Add(1L, "one");
        original.Add(2L, "two");

        var copy = new LongDictionary<string>(
            original.Select(kvp => new KeyValuePair<long, string>(kvp.Key, kvp.Value!)));

        Assert.Equal(3, copy.Count);
        Assert.Equal("zero", copy[0L]);
        Assert.Equal("one", copy[1L]);
        Assert.Equal("two", copy[2L]);
    }

    // ──────────────────────────────────────────────────────────────
    //  Readonly interface view
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntDictionary_Constructed_FromSource_ShouldFlow_Through_IReadOnlyDictionary()
    {
        var source = new[]
        {
            new KeyValuePair<int, int>(0, 0),
            new KeyValuePair<int, int>(1, 10),
            new KeyValuePair<int, int>(2, 20),
        };

        IReadOnlyDictionary<int, int> view = new IntDictionary<int>(source);

        Assert.Equal(3, view.Count);
        Assert.True(view.ContainsKey(0));
        Assert.Equal(0, view[0]);
        Assert.Equal(10, view[1]);
        Assert.Equal(20, view[2]);
    }

    [Fact]
    public void LongDictionary_Constructed_FromSource_ShouldFlow_Through_IReadOnlyDictionary()
    {
        var source = new[]
        {
            new KeyValuePair<long, int>(0, 0),
            new KeyValuePair<long, int>(1, 10),
            new KeyValuePair<long, int>(2, 20),
        };

        IReadOnlyDictionary<long, int> view = new LongDictionary<int>(source);

        Assert.Equal(3, view.Count);
        Assert.True(view.ContainsKey(0L));
        Assert.Equal(0, view[0L]);
        Assert.Equal(10, view[1L]);
        Assert.Equal(20, view[2L]);
    }
}
