using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests for the <c>IEnumerable&lt;KeyValuePair&lt;TKey, TValue&gt;&gt;</c>
/// constructor on <see cref="IntDictionary{TValue, THasher}"/>,
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/> and
/// <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>.
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
    //  RobinHoodDictionary — source argument validation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void RobinHoodDictionary_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<KeyValuePair<int, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new RobinHoodDictionary<int, string, Int32WangNaiveHasher>(source!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void RobinHoodDictionary_ShouldThrow_OnDuplicateKeysInSource()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("a", 3),
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            new RobinHoodDictionary<string, int, StringFnV1AHasher>(source));
        Assert.Contains("a", ex.Message);
    }

    [Fact]
    public void RobinHoodDictionary_ShouldStillValidate_LoadFactor_WhenConstructedFromSource()
    {
        var source = new[] { new KeyValuePair<int, int>(1, 1) };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RobinHoodDictionary<int, int, Int32WangNaiveHasher>(source, loadFactor: 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RobinHoodDictionary<int, int, Int32WangNaiveHasher>(source, loadFactor: 0f));
    }

    // ──────────────────────────────────────────────────────────────
    //  RobinHoodDictionary — happy path
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void RobinHoodDictionary_ShouldSupportEmptySource()
    {
        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>(
            Array.Empty<KeyValuePair<int, string>>());

        Assert.Empty(map);
        Assert.False(map.ContainsKey(0));
        Assert.False(map.ContainsKey(1));
    }

    [Fact]
    public void RobinHoodDictionary_ShouldCopyAllEntries_FromArraySource()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>("alpha", 1),
            new KeyValuePair<string, int>("beta", 2),
            new KeyValuePair<string, int>("gamma", 3),
        };

        var map = new RobinHoodDictionary<string, int, StringFnV1AHasher>(source);

        Assert.Equal(3, map.Count);
        Assert.Equal(1, map["alpha"]);
        Assert.Equal(2, map["beta"]);
        Assert.Equal(3, map["gamma"]);
    }

    [Fact]
    public void RobinHoodDictionary_ShouldCopyAllEntries_FromNonCollectionEnumerable()
    {
        IEnumerable<KeyValuePair<int, string>> NonCollectionSource()
        {
            yield return new KeyValuePair<int, string>(1, "one");
            yield return new KeyValuePair<int, string>(2, "two");
            yield return new KeyValuePair<int, string>(3, "three");
        }

        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>(NonCollectionSource());

        Assert.Equal(3, map.Count);
        Assert.Equal("one", map[1]);
        Assert.Equal("two", map[2]);
        Assert.Equal("three", map[3]);
    }

    [Fact]
    public void RobinHoodDictionary_ShouldCaptureDefaultIntKey_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(0, "zero"),
            new KeyValuePair<int, string>(1, "one"),
        };

        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>(source);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void RobinHoodDictionary_ShouldCaptureNullStringKey_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>(null!, 0),
            new KeyValuePair<string, int>("one", 1),
            new KeyValuePair<string, int>("two", 2),
        };

        var map = new RobinHoodDictionary<string, int, StringFnV1AHasher>(source);

        Assert.Equal(3, map.Count);
        Assert.True(map.ContainsKey(null!));
        Assert.Equal(0, map[null!]);
        Assert.Equal(1, map["one"]);
        Assert.Equal(2, map["two"]);
    }

    [Fact]
    public void RobinHoodDictionary_ShouldDetectDuplicateDefaultKey()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(0, "zero"),
            new KeyValuePair<int, string>(0, "zero-again"),
        };

        Assert.Throws<ArgumentException>(() =>
            new RobinHoodDictionary<int, string, Int32WangNaiveHasher>(source));
    }

    [Fact]
    public void RobinHoodDictionary_ShouldCopy_LargeSource_WithoutDataLoss()
    {
        var source = Enumerable.Range(1, 500)
            .Select(i => new KeyValuePair<string, int>($"key-{i}", i))
            .ToArray();

        var map = new RobinHoodDictionary<string, int, StringFnV1AHasher>(source);

        Assert.Equal(500, map.Count);
        for (int i = 1; i <= 500; i++)
            Assert.Equal(i, map[$"key-{i}"]);
    }

    [Fact]
    public void RobinHoodDictionary_ShouldBeIndependent_OfSourceAfterConstruction()
    {
        var source = new List<KeyValuePair<int, string>>
        {
            new(1, "one"),
            new(2, "two"),
        };

        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>(source);

        source.Add(new KeyValuePair<int, string>(3, "three"));
        source[0] = new KeyValuePair<int, string>(1, "MUTATED");

        Assert.Equal(2, map.Count);
        Assert.Equal("one", map[1]);
        Assert.False(map.ContainsKey(3));
    }

    // ──────────────────────────────────────────────────────────────
    //  PooledCelerityDictionary — source ctor (mirror of CelerityDictionary)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void PooledCelerityDictionary_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<KeyValuePair<int, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>(source!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldThrow_OnDuplicateKeysInSource()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("a", 3),
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            new PooledCelerityDictionary<string, int, StringFnV1AHasher>(source));
        Assert.Contains("a", ex.Message);
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldStillValidate_LoadFactor_WhenConstructedFromSource()
    {
        var source = new[] { new KeyValuePair<int, int>(1, 1) };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(source, loadFactor: 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(source, loadFactor: 0f));
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldSupportEmptySource()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>(
            Array.Empty<KeyValuePair<int, string>>());

        Assert.Empty(map);
        Assert.False(map.ContainsKey(0));
        Assert.False(map.ContainsKey(1));
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldCopyAllEntries_FromArraySource()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>("alpha", 1),
            new KeyValuePair<string, int>("beta", 2),
            new KeyValuePair<string, int>("gamma", 3),
        };

        using var map = new PooledCelerityDictionary<string, int, StringFnV1AHasher>(source);

        Assert.Equal(3, map.Count);
        Assert.Equal(1, map["alpha"]);
        Assert.Equal(2, map["beta"]);
        Assert.Equal(3, map["gamma"]);
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldCopyAllEntries_FromNonCollectionEnumerable()
    {
        IEnumerable<KeyValuePair<int, string>> NonCollectionSource()
        {
            yield return new KeyValuePair<int, string>(1, "one");
            yield return new KeyValuePair<int, string>(2, "two");
            yield return new KeyValuePair<int, string>(3, "three");
        }

        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>(NonCollectionSource());

        Assert.Equal(3, map.Count);
        Assert.Equal("one", map[1]);
        Assert.Equal("two", map[2]);
        Assert.Equal("three", map[3]);
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldCaptureDefaultIntKey_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(0, "zero"),
            new KeyValuePair<int, string>(1, "one"),
        };

        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>(source);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldCaptureNullStringKey_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>(null!, 0),
            new KeyValuePair<string, int>("one", 1),
            new KeyValuePair<string, int>("two", 2),
        };

        using var map = new PooledCelerityDictionary<string, int, StringFnV1AHasher>(source);

        Assert.Equal(3, map.Count);
        Assert.True(map.ContainsKey(null!));
        Assert.Equal(0, map[null!]);
        Assert.Equal(1, map["one"]);
        Assert.Equal(2, map["two"]);
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldDetectDuplicateDefaultKey()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(0, "zero"),
            new KeyValuePair<int, string>(0, "zero-again"),
        };

        Assert.Throws<ArgumentException>(() =>
            new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>(source));
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldCopy_LargeSource_WithoutDataLoss()
    {
        var source = Enumerable.Range(1, 500)
            .Select(i => new KeyValuePair<string, int>($"key-{i}", i))
            .ToArray();

        using var map = new PooledCelerityDictionary<string, int, StringFnV1AHasher>(source);

        Assert.Equal(500, map.Count);
        for (int i = 1; i <= 500; i++)
            Assert.Equal(i, map[$"key-{i}"]);
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldBeIndependent_OfSourceAfterConstruction()
    {
        var source = new List<KeyValuePair<int, string>>
        {
            new(1, "one"),
            new(2, "two"),
        };

        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>(source);

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
    public void RobinHoodDictionary_ShouldCopy_FromAnotherRobinHoodDictionary()
    {
        var original = new RobinHoodDictionary<string, int, StringFnV1AHasher>();
        original.Add("alpha", 1);
        original.Add("beta", 2);

        var copy = new RobinHoodDictionary<string, int, StringFnV1AHasher>(
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

    // ──────────────────────────────────────────────────────────────
    //  FrozenCelerityDictionary — IEnumerable source ctor (its only ctor)
    //  No capacity / loadFactor parameters, so those validations do not apply.
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void FrozenCelerityDictionary_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<KeyValuePair<string, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new FrozenCelerityDictionary<string>(source!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void FrozenCelerityDictionary_ShouldThrow_OnDuplicateKeysInSource()
    {
        var source = new[]
        {
            new KeyValuePair<string, string>("a", "one"),
            new KeyValuePair<string, string>("b", "two"),
            new KeyValuePair<string, string>("a", "dup"),
        };

        Assert.Throws<ArgumentException>(() => new FrozenCelerityDictionary<string>(source));
    }

    [Fact]
    public void FrozenCelerityDictionary_ShouldSupportEmptySource()
    {
        var map = new FrozenCelerityDictionary<string>(
            Array.Empty<KeyValuePair<string, string>>());

        Assert.Empty(map);
        Assert.False(map.ContainsKey("anything"));
    }

    [Fact]
    public void FrozenCelerityDictionary_ShouldCopyAllEntries_FromArraySource()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>("ten", 10),
            new KeyValuePair<string, int>("twenty", 20),
            new KeyValuePair<string, int>("thirty", 30),
        };

        var map = new FrozenCelerityDictionary<int>(source);

        Assert.Equal(3, map.Count);
        Assert.Equal(10, map["ten"]);
        Assert.Equal(20, map["twenty"]);
        Assert.Equal(30, map["thirty"]);
    }

    [Fact]
    public void FrozenCelerityDictionary_ShouldCopyAllEntries_FromNonCollectionEnumerable()
    {
        IEnumerable<KeyValuePair<string, string>> NonCollectionSource()
        {
            yield return new KeyValuePair<string, string>("1", "one");
            yield return new KeyValuePair<string, string>("2", "two");
            yield return new KeyValuePair<string, string>("3", "three");
        }

        var map = new FrozenCelerityDictionary<string>(NonCollectionSource());

        Assert.Equal(3, map.Count);
        Assert.Equal("one", map["1"]);
        Assert.Equal("two", map["2"]);
        Assert.Equal("three", map["3"]);
    }

    [Fact]
    public void FrozenCelerityDictionary_ShouldCaptureNullStringKey_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<string, string>(null!, "nullval"),
            new KeyValuePair<string, string>("a", "one"),
        };

        var map = new FrozenCelerityDictionary<string>(source);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(null!));
        Assert.Equal("nullval", map[null!]);
        Assert.Equal("one", map["a"]);
    }

    [Fact]
    public void FrozenCelerityDictionary_ShouldDetectDuplicateNullKey()
    {
        var source = new[]
        {
            new KeyValuePair<string, string>(null!, "first"),
            new KeyValuePair<string, string>(null!, "again"),
        };

        Assert.Throws<ArgumentException>(() => new FrozenCelerityDictionary<string>(source));
    }

    [Fact]
    public void FrozenCelerityDictionary_ShouldCopy_LargeSource_WithoutDataLoss()
    {
        var source = Enumerable.Range(1, 500)
            .Select(i => new KeyValuePair<string, int>("key-" + i, i * 2))
            .ToArray();

        var map = new FrozenCelerityDictionary<int>(source);

        Assert.Equal(500, map.Count);
        for (int i = 1; i <= 500; i++)
            Assert.Equal(i * 2, map["key-" + i]);
    }

    [Fact]
    public void FrozenCelerityDictionary_ShouldBeIndependent_OfSourceAfterConstruction()
    {
        var source = new List<KeyValuePair<string, string>>
        {
            new("1", "one"),
            new("2", "two"),
        };

        var map = new FrozenCelerityDictionary<string>(source);

        // Mutating the source after construction must not affect the frozen map.
        source.Add(new KeyValuePair<string, string>("3", "three"));
        source[0] = new KeyValuePair<string, string>("1", "MUTATED");

        Assert.Equal(2, map.Count);
        Assert.Equal("one", map["1"]);
        Assert.False(map.ContainsKey("3"));
    }

    [Fact]
    public void FrozenCelerityDictionary_Constructed_FromSource_ShouldFlow_Through_IReadOnlyDictionary()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>("a", 0),
            new KeyValuePair<string, int>("b", 10),
            new KeyValuePair<string, int>("c", 20),
        };

        IReadOnlyDictionary<string, int> view = new FrozenCelerityDictionary<int>(source);

        Assert.Equal(3, view.Count);
        Assert.True(view.ContainsKey("a"));
        Assert.Equal(0, view["a"]);
        Assert.Equal(10, view["b"]);
        Assert.Equal(20, view["c"]);
    }

    // ──────────────────────────────────────────────────────────────
    //  CelerityMultiMap — the IEnumerable<KeyValuePair<,>> constructor.
    //  Shares the null-source / sizing / copy-fidelity invariants with the
    //  dictionaries, but DIFFERS on duplicate keys: a multi-map groups them
    //  rather than throwing, so the duplicate-key-throws assertions do not
    //  apply (the grouping behaviour is asserted instead).
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void CelerityMultiMap_ShouldThrow_ForNullSource()
    {
        IEnumerable<KeyValuePair<int, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CelerityMultiMap<int, string, Int32WangNaiveHasher>(source!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void CelerityMultiMap_ShouldGroupDuplicateKeys_InSourceOrder()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("a", 3),
            new KeyValuePair<string, int>("a", 4),
        };

        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>(source);

        Assert.Equal(2, map.Count);
        Assert.Equal(4, map.ValueCount);
        Assert.Equal(new[] { 1, 3, 4 }, map["a"].ToArray());
        Assert.Equal(new[] { 2 }, map["b"].ToArray());
    }

    [Fact]
    public void CelerityMultiMap_ShouldCopy_FromNonCollectionEnumerable()
    {
        IEnumerable<KeyValuePair<int, string>> NonCollectionSource()
        {
            yield return new KeyValuePair<int, string>(1, "one");
            yield return new KeyValuePair<int, string>(2, "two");
            yield return new KeyValuePair<int, string>(1, "uno");
        }

        var map = new CelerityMultiMap<int, string, Int32WangNaiveHasher>(NonCollectionSource());

        Assert.Equal(2, map.Count);
        Assert.Equal(new[] { "one", "uno" }, map[1].ToArray());
        Assert.Equal(new[] { "two" }, map[2].ToArray());
    }

    [Fact]
    public void CelerityMultiMap_ShouldCaptureNullStringKey_FromSource()
    {
        var source = new[]
        {
            new KeyValuePair<string, string>(null!, "nullval"),
            new KeyValuePair<string, string>(null!, "again"),
            new KeyValuePair<string, string>("a", "one"),
        };

        var map = new CelerityMultiMap<string, string, StringFnV1AHasher>(source);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(null!));
        Assert.Equal(new[] { "nullval", "again" }, map[null!].ToArray());
        Assert.Equal(new[] { "one" }, map["a"].ToArray());
    }

    [Fact]
    public void CelerityMultiMap_ShouldCopy_LargeSource_WithoutDataLoss()
    {
        var source = Enumerable.Range(1, 500)
            .Select(i => new KeyValuePair<string, int>("key-" + i, i * 2))
            .ToArray();

        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>(source);

        Assert.Equal(500, map.Count);
        for (int i = 1; i <= 500; i++)
            Assert.Equal(new[] { i * 2 }, map["key-" + i].ToArray());
    }

    [Fact]
    public void CelerityMultiMap_ShouldBeIndependent_OfSourceAfterConstruction()
    {
        var source = new List<KeyValuePair<string, string>>
        {
            new("1", "one"),
            new("2", "two"),
        };

        var map = new CelerityMultiMap<string, string, StringFnV1AHasher>(source);

        source.Add(new KeyValuePair<string, string>("3", "three"));
        source[0] = new KeyValuePair<string, string>("1", "MUTATED");

        Assert.Equal(2, map.Count);
        Assert.Equal(new[] { "one" }, map["1"].ToArray());
        Assert.False(map.ContainsKey("3"));
    }

    [Fact]
    public void CelerityMultiMap_ShouldFlow_Through_ILookup()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("a", 2),
            new KeyValuePair<string, int>("b", 10),
        };

        ILookup<string, int> view = new CelerityMultiMap<string, int, StringFnV1AHasher>(source);

        Assert.Equal(2, view.Count);
        Assert.True(view.Contains("a"));
        Assert.Equal(new[] { 1, 2 }, view["a"].ToArray());
        Assert.Equal(new[] { 10 }, view["b"].ToArray());
    }

    // ──────────────────────────────────────────────────────────────
    //  SmallDictionary — the IEnumerable<KeyValuePair<,>> constructor.
    //  Shares the null-source / sizing / copy-fidelity / duplicate-key
    //  invariants with the hash dictionaries. It has no loadFactor parameter,
    //  so the loadFactor-validation variant does not apply.
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void SmallDictionary_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<KeyValuePair<int, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SmallDictionary<int, string>(source!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void SmallDictionary_ShouldThrow_OnDuplicateKeysInSource()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(1, "one-again"),
        };

        var ex = Assert.Throws<ArgumentException>(() => new SmallDictionary<int, string>(source));
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void SmallDictionary_ShouldCopyAllEntries_FromArraySource()
    {
        var source = new[]
        {
            new KeyValuePair<int, int>(1, 10),
            new KeyValuePair<int, int>(2, 20),
            new KeyValuePair<int, int>(0, 99), // zero key is an ordinary entry
        };

        var map = new SmallDictionary<int, int>(source);

        Assert.Equal(3, map.Count);
        Assert.Equal(10, map[1]);
        Assert.Equal(20, map[2]);
        Assert.Equal(99, map[0]);
    }

    [Fact]
    public void SmallDictionary_ShouldCopyAllEntries_FromNonCollectionEnumerable()
    {
        IEnumerable<KeyValuePair<int, int>> Source()
        {
            for (int i = 1; i <= 5; i++)
                yield return new KeyValuePair<int, int>(i, i * 10);
        }

        var map = new SmallDictionary<int, int>(Source());

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void SmallDictionary_ShouldCopy_LargeSource_WithoutDataLoss()
    {
        var source = Enumerable.Range(1, 500)
            .Select(i => new KeyValuePair<int, int>(i, i * 3))
            .ToArray();

        var map = new SmallDictionary<int, int>(source);

        Assert.Equal(500, map.Count);
        for (int i = 1; i <= 500; i++)
            Assert.Equal(i * 3, map[i]);
    }
}
