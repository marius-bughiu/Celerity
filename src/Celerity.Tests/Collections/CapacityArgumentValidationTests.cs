using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Pins the argument-validation contract of the capacity-management surface
/// (<c>EnsureCapacity(int)</c> / <c>TrimExcess(int)</c>) across every mutable hash-table type in
/// the family, and fills in the behavioural coverage of <see cref="LongDictionary{TValue}"/>'s
/// <c>TrimExcess</c> overloads.
///
/// <para>
/// <see cref="EnsureCapacityAndTrimExcessTests"/> pins the <i>happy path</i> — pre-sizing away
/// resizes, and shrinking without losing data — but spot-checks the guards on only two or three
/// representative types. That is a real gap: the guards are hand-copied into each of the sixteen
/// collections rather than shared through a common base, so "the code is identical" is an
/// assumption, not a fact the tests hold. A copy/paste slip in one type (a <c>&lt;=</c> for a
/// <c>&lt;</c>, a wrong <c>nameof</c>, a message from the sibling guard) would sail through the
/// existing suite. This class asserts each guard on each type, and asserts the full observable
/// shape of the failure rather than just the exception type:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Negative capacity.</b> <c>EnsureCapacity(-1)</c> throws
/// <see cref="ArgumentOutOfRangeException"/> with <c>ParamName == "capacity"</c>, the offending
/// value echoed in <see cref="ArgumentOutOfRangeException.ActualValue"/>, and the
/// "Capacity must be non-negative." message. Callers routinely surface these to users, so the
/// parameter name and the actual value are part of the contract, not incidental detail.
/// </description></item>
/// <item><description>
/// <b>Capacity below Count.</b> <c>TrimExcess(n)</c> with <c>n &lt; Count</c> throws rather than
/// silently sizing a table too small to hold the live entries — the alternative would be data
/// loss or a non-terminating probe loop, because these tables resolve collisions by open
/// addressing and assume a vacant slot always exists. The message is the distinct
/// "Capacity must be at least the current Count." so the two guards cannot be confused.
/// </description></item>
/// </list>
///
/// <para>
/// The final section covers <see cref="LongDictionary{TValue}"/>'s <c>TrimExcess()</c> and
/// <c>TrimExcess(int)</c>, which had no direct test at all. Both directions of the
/// <c>newSize != table.Length</c> decision are exercised, and the "table left untouched" case is
/// asserted through an observable channel — a live enumerator survives the call, which it could
/// not if the dictionary had rehashed and bumped its modification version.
/// </para>
/// </summary>
public class CapacityArgumentValidationTests
{
    private const int N = 100;

    /// <summary>
    /// Asserts the shared "negative capacity" guard: the exception type, the parameter name the
    /// caller sees, the echoed offending value, and the message that distinguishes this guard
    /// from the <c>TrimExcess</c> one.
    /// </summary>
    private static void AssertNegativeCapacityRejected(Action call, int capacity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(call);
        Assert.Equal("capacity", ex.ParamName);
        Assert.Equal(capacity, (int)ex.ActualValue!);
        Assert.Contains("Capacity must be non-negative.", ex.Message);
    }

    /// <summary>
    /// Asserts the shared "capacity below Count" guard on <c>TrimExcess(int)</c>.
    /// </summary>
    private static void AssertCapacityBelowCountRejected(Action call, int capacity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(call);
        Assert.Equal("capacity", ex.ParamName);
        Assert.Equal(capacity, (int)ex.ActualValue!);
        Assert.Contains("Capacity must be at least the current Count.", ex.Message);
    }

    // ── EnsureCapacity(capacity): negative capacity is rejected ──────────────────────

    [Fact]
    public void CelerityDictionary_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var map = new CelerityDictionary<string, int, DefaultHasher<string>>();
        AssertNegativeCapacityRejected(() => map.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void RobinHoodDictionary_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var map = new RobinHoodDictionary<string, int, DefaultHasher<string>>();
        AssertNegativeCapacityRejected(() => map.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void SwissDictionary_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var map = new SwissDictionary<string, int, DefaultHasher<string>>();
        AssertNegativeCapacityRejected(() => map.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void HashCachingDictionary_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var map = new HashCachingDictionary<string, int, DefaultHasher<string>>();
        AssertNegativeCapacityRejected(() => map.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void PooledCelerityDictionary_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        using var map = new PooledCelerityDictionary<string, int, DefaultHasher<string>>();
        AssertNegativeCapacityRejected(() => map.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void LongDictionary_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var map = new LongDictionary<int>();
        AssertNegativeCapacityRejected(() => map.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void CelerityMultiMap_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var map = new CelerityMultiMap<int, int, DefaultHasher<int>>();
        AssertNegativeCapacityRejected(() => map.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void CelerityMultiSet_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var set = new CelerityMultiSet<int, DefaultHasher<int>>();
        AssertNegativeCapacityRejected(() => set.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void IntSet_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var set = new IntSet<DefaultHasher<int>>();
        AssertNegativeCapacityRejected(() => set.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void LongSet_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var set = new LongSet<DefaultHasher<long>>();
        AssertNegativeCapacityRejected(() => set.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void SwissSet_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var set = new SwissSet<string, DefaultHasher<string>>();
        AssertNegativeCapacityRejected(() => set.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void RobinHoodSet_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var set = new RobinHoodSet<string, DefaultHasher<string>>();
        AssertNegativeCapacityRejected(() => set.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void HashCachingSet_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var set = new HashCachingSet<string, DefaultHasher<string>>();
        AssertNegativeCapacityRejected(() => set.EnsureCapacity(-1), -1);
    }

    [Fact]
    public void PooledCeleritySet_EnsureCapacity_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        using var set = new PooledCeleritySet<string, DefaultHasher<string>>();
        AssertNegativeCapacityRejected(() => set.EnsureCapacity(-1), -1);
    }

    // ── TrimExcess(capacity): a capacity below the live Count is rejected ────────────

    [Fact]
    public void CelerityDictionary_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var map = new CelerityDictionary<string, int, DefaultHasher<string>>();
        for (int i = 1; i <= 4; i++)
            map[$"k{i}"] = i;

        AssertCapacityBelowCountRejected(() => map.TrimExcess(1), 1);

        // The rejected call must have left the dictionary untouched.
        Assert.Equal(4, map.Count);
        Assert.Equal(3, map["k3"]);
    }

    [Fact]
    public void RobinHoodDictionary_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var map = new RobinHoodDictionary<string, int, DefaultHasher<string>>();
        for (int i = 1; i <= 4; i++)
            map[$"k{i}"] = i;

        AssertCapacityBelowCountRejected(() => map.TrimExcess(1), 1);
        Assert.Equal(4, map.Count);
    }

    [Fact]
    public void SwissDictionary_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var map = new SwissDictionary<string, int, DefaultHasher<string>>();
        for (int i = 1; i <= 4; i++)
            map[$"k{i}"] = i;

        AssertCapacityBelowCountRejected(() => map.TrimExcess(1), 1);
        Assert.Equal(4, map.Count);
    }

    [Fact]
    public void HashCachingDictionary_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var map = new HashCachingDictionary<string, int, DefaultHasher<string>>();
        for (int i = 1; i <= 4; i++)
            map[$"k{i}"] = i;

        AssertCapacityBelowCountRejected(() => map.TrimExcess(1), 1);
        Assert.Equal(4, map.Count);
    }

    [Fact]
    public void PooledCelerityDictionary_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        using var map = new PooledCelerityDictionary<string, int, DefaultHasher<string>>();
        for (int i = 1; i <= 4; i++)
            map[$"k{i}"] = i;

        AssertCapacityBelowCountRejected(() => map.TrimExcess(1), 1);
        Assert.Equal(4, map.Count);
    }

    [Fact]
    public void LongDictionary_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var map = new LongDictionary<int>();
        for (long i = 1; i <= 4; i++)
            map[i] = (int)i;

        AssertCapacityBelowCountRejected(() => map.TrimExcess(1), 1);
        Assert.Equal(4, map.Count);
        Assert.Equal(3, map[3]);
    }

    [Fact]
    public void SmallDictionary_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var map = new SmallDictionary<int, int>();
        for (int i = 1; i <= 4; i++)
            map[i] = i * 10;

        AssertCapacityBelowCountRejected(() => map.TrimExcess(1), 1);
        Assert.Equal(4, map.Count);
        Assert.Equal(30, map[3]);
    }

    [Fact]
    public void CelerityMultiMap_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var map = new CelerityMultiMap<int, int, DefaultHasher<int>>();
        for (int i = 1; i <= 4; i++)
            map.Add(i, i * 10);

        // Count is the number of distinct keys, which is what the guard compares against.
        AssertCapacityBelowCountRejected(() => map.TrimExcess(1), 1);
        Assert.Equal(4, map.Count);
    }

    [Fact]
    public void CelerityMultiSet_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var set = new CelerityMultiSet<int, DefaultHasher<int>>();
        for (int i = 1; i <= 4; i++)
            set.Add(i, i * 10);

        AssertCapacityBelowCountRejected(() => set.TrimExcess(1), 1);
        Assert.Equal(4, set.Count);
        Assert.Equal(30, set[3]);
    }

    [Fact]
    public void CeleritySet_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var set = new CeleritySet<string, DefaultHasher<string>>();
        for (int i = 1; i <= 4; i++)
            set.Add($"v{i}");

        AssertCapacityBelowCountRejected(() => set.TrimExcess(1), 1);
        Assert.Equal(4, set.Count);
        Assert.True(set.Contains("v3"));
    }

    [Fact]
    public void SwissSet_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var set = new SwissSet<string, DefaultHasher<string>>();
        for (int i = 1; i <= 4; i++)
            set.Add($"v{i}");

        AssertCapacityBelowCountRejected(() => set.TrimExcess(1), 1);
        Assert.Equal(4, set.Count);
    }

    [Fact]
    public void RobinHoodSet_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var set = new RobinHoodSet<string, DefaultHasher<string>>();
        for (int i = 1; i <= 4; i++)
            set.Add($"v{i}");

        AssertCapacityBelowCountRejected(() => set.TrimExcess(1), 1);
        Assert.Equal(4, set.Count);
    }

    [Fact]
    public void HashCachingSet_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var set = new HashCachingSet<string, DefaultHasher<string>>();
        for (int i = 1; i <= 4; i++)
            set.Add($"v{i}");

        AssertCapacityBelowCountRejected(() => set.TrimExcess(1), 1);
        Assert.Equal(4, set.Count);
    }

    [Fact]
    public void PooledCeleritySet_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        using var set = new PooledCeleritySet<string, DefaultHasher<string>>();
        for (int i = 1; i <= 4; i++)
            set.Add($"v{i}");

        AssertCapacityBelowCountRejected(() => set.TrimExcess(1), 1);
        Assert.Equal(4, set.Count);
    }

    [Fact]
    public void LongSet_TrimExcess_ShouldThrowArgumentOutOfRange_WhenCapacityIsBelowCount()
    {
        var set = new LongSet<DefaultHasher<long>>();
        for (long i = 1; i <= 4; i++)
            set.Add(i);

        AssertCapacityBelowCountRejected(() => set.TrimExcess(1), 1);
        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(3L));
    }

    // ── LongDictionary.TrimExcess: previously untested outright ──────────────────────

    [Fact]
    public void LongDictionary_TrimExcess_ShouldPreserveSurvivingEntries_WhenMostEntriesRemoved()
    {
        var map = new LongDictionary<int>();
        for (long i = 1; i <= N; i++)
            map[i] = (int)(i * 10);
        for (long i = 6; i <= N; i++)
            Assert.True(map.Remove(i));

        // The table grew through several doublings on the way to 100 entries; trimming rehashes
        // the five survivors down into a table sized for them.
        map.TrimExcess();

        Assert.Equal(5, map.Count);
        for (long i = 1; i <= 5; i++)
            Assert.Equal((int)(i * 10), map[i]);
        Assert.False(map.ContainsKey(50));

        // A rehash that mis-sized the table would orphan entries or wedge the probe loop, so
        // exercise the shrunken table with a fresh insert and lookup.
        map[999] = 9990;
        Assert.Equal(9990, map[999]);
        Assert.Equal(6, map.Count);
    }

    [Fact]
    public void LongDictionary_TrimExcess_ShouldLeaveTableUntouched_WhenAlreadyAtMinimalSize()
    {
        var map = new LongDictionary<int>();
        for (long i = 1; i <= N; i++)
            map[i] = (int)i;
        for (long i = 6; i <= N; i++)
            map.Remove(i);

        // First trim: the computed size differs from the current table, so the dictionary
        // rehashes — an observable structural change that invalidates live enumerators.
        var beforeShrink = map.GetEnumerator();
        map.TrimExcess();
        Assert.Throws<InvalidOperationException>(() => { beforeShrink.MoveNext(); });

        // Second trim: the table is already the minimal size for the same Count, so the call
        // must be a genuine no-op — no reallocation, and no version bump, which is why the
        // enumerator taken beforehand still runs to completion.
        var afterShrink = map.GetEnumerator();
        map.TrimExcess();

        int seen = 0;
        while (afterShrink.MoveNext())
            seen++;

        Assert.Equal(5, seen);
        Assert.Equal(5, map.Count);
        for (long i = 1; i <= 5; i++)
            Assert.Equal((int)i, map[i]);
    }

    [Fact]
    public void LongDictionary_TrimExcess_ShouldGrowTable_WhenExplicitCapacityExceedsCurrentSize()
    {
        var map = new LongDictionary<int>();
        for (long i = 1; i <= 5; i++)
            map[i] = (int)(i * 3);
        map.TrimExcess(); // shrink to the minimum for five entries first

        // TrimExcess(capacity) sizes the table for the requested capacity in either direction,
        // so asking for far more than Count re-grows it rather than throwing or no-opping.
        map.TrimExcess(64);

        Assert.Equal(5, map.Count);
        for (long i = 1; i <= 5; i++)
            Assert.Equal((int)(i * 3), map[i]);

        // The grown table holds the requested capacity without another resize.
        for (long i = 6; i <= 64; i++)
            map[i] = (int)(i * 3);
        Assert.Equal(64, map.Count);
        Assert.Equal(64 * 3, map[64]);
    }

    [Fact]
    public void LongDictionary_TrimExcess_ShouldPreserveOutOfBandZeroKey_WhenShrinking()
    {
        var map = new LongDictionary<int>();
        // Key 0 collides with the empty-slot sentinel and is stored outside the probe table;
        // the rehash walks the table only, so the zero entry must survive by construction.
        for (long i = 0; i <= N; i++)
            map[i] = (int)(i + 1);
        for (long i = 6; i <= N; i++)
            map.Remove(i);

        map.TrimExcess();

        Assert.Equal(6, map.Count); // keys 0..5
        for (long i = 0; i <= 5; i++)
            Assert.Equal((int)(i + 1), map[i]);
        Assert.True(map.TryGetValue(0L, out int zeroValue));
        Assert.Equal(1, zeroValue);
    }
}
