using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Regression coverage for issue #233: the indexer setter must invalidate active
/// enumerators only on a <em>structural</em> change (a genuinely new key being
/// added), never on an in-place value overwrite of a key that already exists.
/// This matches BCL <see cref="Dictionary{TKey, TValue}"/>, which bumps its
/// version on add/remove/clear but not on <c>dict[existingKey] = newValue</c>, so
/// the common "iterate and update values in place" pattern is legal.
///
/// Before the fix every mutable Celerity dictionary bumped <c>_version</c> on every
/// indexer set — including the pure-overwrite branch and the already-present
/// default/zero-key branch — so the loops below threw
/// <see cref="InvalidOperationException"/> on the second <c>MoveNext</c>. Each test
/// also asserts the inverse guard (adding a new key mid-enumeration still throws),
/// so the fix cannot silently disable structural-mutation detection.
/// </summary>
public class IndexerOverwriteEnumerationTests
{
    [Fact]
    public void CelerityDictionary_OverwriteDuringEnumeration_DoesNotThrow()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100; // default (zero) key — out-of-band slot
        map[1] = 10;
        map[2] = 20;

        AssertOverwriteAllowedAddRejected(
            getEnumerator: () => map.GetEnumerator(),
            overwriteExisting: () => map[1] = 11,
            overwriteDefault: () => map[0] = 101,
            addNewKey: () => map[99] = 990);

        Assert.Equal(11, map[1]);
        Assert.Equal(101, map[0]);
    }

    [Fact]
    public void IntDictionary_OverwriteDuringEnumeration_DoesNotThrow()
    {
        var map = new IntDictionary<int>();
        map[0] = 100; // zero key — out-of-band slot
        map[1] = 10;
        map[2] = 20;

        AssertOverwriteAllowedAddRejected(
            getEnumerator: () => map.GetEnumerator(),
            overwriteExisting: () => map[1] = 11,
            overwriteDefault: () => map[0] = 101,
            addNewKey: () => map[99] = 990);

        Assert.Equal(11, map[1]);
        Assert.Equal(101, map[0]);
    }

    [Fact]
    public void LongDictionary_OverwriteDuringEnumeration_DoesNotThrow()
    {
        var map = new LongDictionary<int>();
        map[0L] = 100; // zero key — out-of-band slot
        map[1L] = 10;
        map[2L] = 20;

        AssertOverwriteAllowedAddRejected(
            getEnumerator: () => map.GetEnumerator(),
            overwriteExisting: () => map[1L] = 11,
            overwriteDefault: () => map[0L] = 101,
            addNewKey: () => map[99L] = 990);

        Assert.Equal(11, map[1L]);
        Assert.Equal(101, map[0L]);
    }

    [Fact]
    public void RobinHoodDictionary_OverwriteDuringEnumeration_DoesNotThrow()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100; // default key — out-of-band slot
        map[1] = 10;
        map[2] = 20;

        AssertOverwriteAllowedAddRejected(
            getEnumerator: () => map.GetEnumerator(),
            overwriteExisting: () => map[1] = 11,
            overwriteDefault: () => map[0] = 101,
            addNewKey: () => map[99] = 990);

        Assert.Equal(11, map[1]);
        Assert.Equal(101, map[0]);
    }

    [Fact]
    public void SwissDictionary_OverwriteDuringEnumeration_DoesNotThrow()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100; // default key — out-of-band slot
        map[1] = 10;
        map[2] = 20;

        AssertOverwriteAllowedAddRejected(
            getEnumerator: () => map.GetEnumerator(),
            overwriteExisting: () => map[1] = 11,
            overwriteDefault: () => map[0] = 101,
            addNewKey: () => map[99] = 990);

        Assert.Equal(11, map[1]);
        Assert.Equal(101, map[0]);
    }

    [Fact]
    public void HashCachingDictionary_OverwriteDuringEnumeration_DoesNotThrow()
    {
        var map = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100; // default key — out-of-band slot
        map[1] = 10;
        map[2] = 20;

        AssertOverwriteAllowedAddRejected(
            getEnumerator: () => map.GetEnumerator(),
            overwriteExisting: () => map[1] = 11,
            overwriteDefault: () => map[0] = 101,
            addNewKey: () => map[99] = 990);

        Assert.Equal(11, map[1]);
        Assert.Equal(101, map[0]);
    }

    [Fact]
    public void PooledCelerityDictionary_OverwriteDuringEnumeration_DoesNotThrow()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100; // default key — out-of-band slot
        map[1] = 10;
        map[2] = 20;

        AssertOverwriteAllowedAddRejected(
            getEnumerator: () => map.GetEnumerator(),
            overwriteExisting: () => map[1] = 11,
            overwriteDefault: () => map[0] = 101,
            addNewKey: () => map[99] = 990);

        Assert.Equal(11, map[1]);
        Assert.Equal(101, map[0]);
    }

    [Fact]
    public void SmallDictionary_OverwriteDuringEnumeration_DoesNotThrow()
    {
        var map = new SmallDictionary<int, int>();
        map[0] = 100; // default key — stored inline, but still a pure overwrite
        map[1] = 10;
        map[2] = 20;

        AssertOverwriteAllowedAddRejected(
            getEnumerator: () => map.GetEnumerator(),
            overwriteExisting: () => map[1] = 11,
            overwriteDefault: () => map[0] = 101,
            addNewKey: () => map[99] = 990);

        Assert.Equal(11, map[1]);
        Assert.Equal(101, map[0]);
    }

    /// <summary>
    /// Each scenario runs on a freshly obtained struct enumerator (from
    /// <paramref name="getEnumerator"/>) so the captured version is current at the
    /// moment of the mutation under test. Overwriting an existing key — and the
    /// already-present default key — must let the walk complete; inserting a
    /// brand-new key must invalidate the enumerator. The constraint
    /// <c>struct, IEnumerator</c> lets the helper drive each dictionary's distinct
    /// struct enumerator without boxing.
    /// </summary>
    private static void AssertOverwriteAllowedAddRejected<TEnumerator>(
        Func<TEnumerator> getEnumerator,
        Action overwriteExisting,
        Action overwriteDefault,
        Action addNewKey)
        where TEnumerator : struct, System.Collections.IEnumerator
    {
        // 1. Overwrite an existing key mid-enumeration: not structural, no throw.
        TEnumerator e1 = getEnumerator();
        Assert.True(e1.MoveNext());
        overwriteExisting();
        DrainWithoutThrow(ref e1);

        // 2. Overwrite the already-present default/zero key: also not structural.
        TEnumerator e2 = getEnumerator();
        Assert.True(e2.MoveNext());
        overwriteDefault();
        DrainWithoutThrow(ref e2);

        // 3. Add a brand-new key mid-enumeration: structural, must still throw.
        TEnumerator e3 = getEnumerator();
        Assert.True(e3.MoveNext());
        addNewKey();
        bool threw = false;
        try
        {
            e3.MoveNext();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.True(threw, "Adding a new key mid-enumeration must invalidate the enumerator.");
    }

    private static void DrainWithoutThrow<TEnumerator>(ref TEnumerator enumerator)
        where TEnumerator : struct, System.Collections.IEnumerator
    {
        while (enumerator.MoveNext())
        {
            // Touch Current so the walk cannot be optimized away.
            _ = enumerator.Current;
        }
    }
}
