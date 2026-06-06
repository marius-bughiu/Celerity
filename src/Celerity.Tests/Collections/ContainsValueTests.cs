using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class ContainsValueTests
{
    // ---------------- IntDictionary ----------------

    [Fact]
    public void IntDictionary_EmptyMap_ReturnsFalse()
    {
        var map = new IntDictionary<int>();
        Assert.False(map.ContainsValue(0));
        Assert.False(map.ContainsValue(42));
    }

    [Fact]
    public void IntDictionary_FindsValueInRegularSlot()
    {
        var map = new IntDictionary<int> { [1] = 100, [2] = 200, [3] = 300 };
        Assert.True(map.ContainsValue(200));
    }

    [Fact]
    public void IntDictionary_ReturnsFalseForMissingValue()
    {
        var map = new IntDictionary<int> { [1] = 100, [2] = 200 };
        Assert.False(map.ContainsValue(999));
    }

    [Fact]
    public void IntDictionary_FindsValueOnlyInZeroKeySlot()
    {
        var map = new IntDictionary<int>();
        map[0] = 777;
        Assert.True(map.ContainsValue(777));
    }

    [Fact]
    public void IntDictionary_DefaultValueLookup_ZeroValue()
    {
        // After insert, value 0 must be reachable via ContainsValue.
        var map = new IntDictionary<int> { [5] = 0, [6] = 1 };
        Assert.True(map.ContainsValue(0));

        // But an empty dictionary must NOT report 0 — the EMPTY_KEY slots
        // are filled with default(TValue) and must be skipped by the scan.
        var empty = new IntDictionary<int>();
        Assert.False(empty.ContainsValue(0));
    }

    [Fact]
    public void IntDictionary_DefaultValueLookup_OnlyZeroKeyHasDefaultValue()
    {
        // Same trap as above but with the value sitting only in the
        // out-of-band zero-key slot.
        var map = new IntDictionary<int>();
        map[0] = 0;
        Assert.True(map.ContainsValue(0));
    }

    [Fact]
    public void IntDictionary_NullValueLookup_ReferenceType()
    {
        var map = new IntDictionary<string?>();
        map[1] = "one";
        map[2] = null;
        Assert.True(map.ContainsValue(null));

        var noNulls = new IntDictionary<string> { [1] = "one", [2] = "two" };
        Assert.False(noNulls.ContainsValue(null));
    }

    [Fact]
    public void IntDictionary_DuplicateValues_ReturnsTrue()
    {
        var map = new IntDictionary<int>
        {
            [1] = 42,
            [2] = 42,
            [3] = 42,
        };
        Assert.True(map.ContainsValue(42));
    }

    [Fact]
    public void IntDictionary_SurvivesResize()
    {
        var map = new IntDictionary<int>(capacity: 4);
        for (int i = 1; i <= 100; i++)
            map[i] = i * 10;

        Assert.True(map.ContainsValue(770));
        Assert.False(map.ContainsValue(-1));
    }

    [Fact]
    public void IntDictionary_AfterRemove_ReturnsFalse()
    {
        var map = new IntDictionary<int> { [1] = 100, [2] = 200 };
        map.Remove(1);
        Assert.False(map.ContainsValue(100));
        Assert.True(map.ContainsValue(200));
    }

    [Fact]
    public void IntDictionary_AfterZeroKeyRemove_ReturnsFalse()
    {
        var map = new IntDictionary<int>();
        map[0] = 555;
        map[1] = 100;
        map.Remove(0);
        Assert.False(map.ContainsValue(555));
        Assert.True(map.ContainsValue(100));
    }

    // ---------------- LongDictionary ----------------

    [Fact]
    public void LongDictionary_EmptyMap_ReturnsFalse()
    {
        var map = new LongDictionary<int>();
        Assert.False(map.ContainsValue(0));
        Assert.False(map.ContainsValue(42));
    }

    [Fact]
    public void LongDictionary_FindsValueInRegularSlot()
    {
        var map = new LongDictionary<int> { [1L] = 100, [2L] = 200, [3L] = 300 };
        Assert.True(map.ContainsValue(200));
    }

    [Fact]
    public void LongDictionary_ReturnsFalseForMissingValue()
    {
        var map = new LongDictionary<int> { [1L] = 100, [2L] = 200 };
        Assert.False(map.ContainsValue(999));
    }

    [Fact]
    public void LongDictionary_FindsValueOnlyInZeroKeySlot()
    {
        var map = new LongDictionary<int>();
        map[0L] = 777;
        Assert.True(map.ContainsValue(777));
    }

    [Fact]
    public void LongDictionary_DefaultValueLookup_ZeroValue()
    {
        var empty = new LongDictionary<int>();
        Assert.False(empty.ContainsValue(0));

        var map = new LongDictionary<int> { [5L] = 0, [6L] = 1 };
        Assert.True(map.ContainsValue(0));
    }

    [Fact]
    public void LongDictionary_NullValueLookup_ReferenceType()
    {
        var map = new LongDictionary<string?>();
        map[1L] = "one";
        map[2L] = null;
        Assert.True(map.ContainsValue(null));
    }

    [Fact]
    public void LongDictionary_SurvivesResize()
    {
        var map = new LongDictionary<int>(capacity: 4);
        for (long i = 1; i <= 100; i++)
            map[i] = (int)(i * 10);

        Assert.True(map.ContainsValue(770));
        Assert.False(map.ContainsValue(-1));
    }

    // ---------------- CelerityDictionary ----------------

    [Fact]
    public void CelerityDictionary_EmptyMap_ReturnsFalse()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(map.ContainsValue(0));
        Assert.False(map.ContainsValue(42));
    }

    [Fact]
    public void CelerityDictionary_FindsValueInRegularSlot()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher> { [1] = 100, [2] = 200, [3] = 300 };
        Assert.True(map.ContainsValue(200));
    }

    [Fact]
    public void CelerityDictionary_ReturnsFalseForMissingValue()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher> { [1] = 100, [2] = 200 };
        Assert.False(map.ContainsValue(999));
    }

    [Fact]
    public void CelerityDictionary_FindsValueOnlyInDefaultKeySlot_IntKey()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 777;
        Assert.True(map.ContainsValue(777));
    }

    [Fact]
    public void CelerityDictionary_FindsValueOnlyInDefaultKeySlot_NullStringKey()
    {
        var map = new CelerityDictionary<string, int, StringFnV1AHasher>();
        map[null!] = 777;
        Assert.True(map.ContainsValue(777));
    }

    [Fact]
    public void CelerityDictionary_DefaultValueLookup_ZeroValue()
    {
        // EMPTY_KEY slots in the probe array are populated with default(TKey)
        // and default(TValue). ContainsValue must skip those.
        var empty = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(empty.ContainsValue(0));

        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher> { [5] = 0, [6] = 1 };
        Assert.True(map.ContainsValue(0));
    }

    [Fact]
    public void CelerityDictionary_NullValueLookup_ReferenceType()
    {
        var map = new CelerityDictionary<int, string?, Int32WangNaiveHasher>();
        map[1] = "one";
        map[2] = null;
        Assert.True(map.ContainsValue(null));

        var noNulls = new CelerityDictionary<int, string, Int32WangNaiveHasher> { [1] = "one", [2] = "two" };
        Assert.False(noNulls.ContainsValue(null));
    }

    [Fact]
    public void CelerityDictionary_DuplicateValues_ReturnsTrue()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>
        {
            [1] = 42,
            [2] = 42,
            [3] = 42,
        };
        Assert.True(map.ContainsValue(42));
    }

    [Fact]
    public void CelerityDictionary_SurvivesResize()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 4);
        for (int i = 1; i <= 100; i++)
            map[i] = i * 10;

        Assert.True(map.ContainsValue(770));
        Assert.False(map.ContainsValue(-1));
    }

    [Fact]
    public void CelerityDictionary_AfterDefaultKeyRemove_ReturnsFalse()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 555;
        map[1] = 100;
        map.Remove(0);
        Assert.False(map.ContainsValue(555));
        Assert.True(map.ContainsValue(100));
    }

    [Fact]
    public void CelerityDictionary_AfterClear_ReturnsFalse()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher> { [1] = 100, [2] = 200 };
        map[0] = 300;
        map.Clear();
        Assert.False(map.ContainsValue(100));
        Assert.False(map.ContainsValue(200));
        Assert.False(map.ContainsValue(300));
    }

    // ---------------- RobinHoodDictionary ----------------

    [Fact]
    public void RobinHoodDictionary_EmptyMap_ReturnsFalse()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(map.ContainsValue(0));
        Assert.False(map.ContainsValue(42));
    }

    [Fact]
    public void RobinHoodDictionary_FindsValueInRegularSlot()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher> { [1] = 100, [2] = 200, [3] = 300 };
        Assert.True(map.ContainsValue(200));
    }

    [Fact]
    public void RobinHoodDictionary_ReturnsFalseForMissingValue()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher> { [1] = 100, [2] = 200 };
        Assert.False(map.ContainsValue(999));
    }

    [Fact]
    public void RobinHoodDictionary_FindsValueOnlyInDefaultKeySlot_IntKey()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 777;
        Assert.True(map.ContainsValue(777));
    }

    [Fact]
    public void RobinHoodDictionary_FindsValueOnlyInDefaultKeySlot_NullStringKey()
    {
        var map = new RobinHoodDictionary<string, int, StringFnV1AHasher>();
        map[null!] = 777;
        Assert.True(map.ContainsValue(777));
    }

    [Fact]
    public void RobinHoodDictionary_DefaultValueLookup_ZeroValue()
    {
        // EMPTY_KEY slots in the probe array are populated with default(TKey)
        // and default(TValue). ContainsValue must skip those.
        var empty = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(empty.ContainsValue(0));

        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher> { [5] = 0, [6] = 1 };
        Assert.True(map.ContainsValue(0));
    }

    [Fact]
    public void RobinHoodDictionary_NullValueLookup_ReferenceType()
    {
        var map = new RobinHoodDictionary<int, string?, Int32WangNaiveHasher>();
        map[1] = "one";
        map[2] = null;
        Assert.True(map.ContainsValue(null));

        var noNulls = new RobinHoodDictionary<int, string, Int32WangNaiveHasher> { [1] = "one", [2] = "two" };
        Assert.False(noNulls.ContainsValue(null));
    }

    [Fact]
    public void RobinHoodDictionary_DuplicateValues_ReturnsTrue()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>
        {
            [1] = 42,
            [2] = 42,
            [3] = 42,
        };
        Assert.True(map.ContainsValue(42));
    }

    [Fact]
    public void RobinHoodDictionary_SurvivesResize()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>(capacity: 4);
        for (int i = 1; i <= 100; i++)
            map[i] = i * 10;

        Assert.True(map.ContainsValue(770));
        Assert.False(map.ContainsValue(-1));
    }

    [Fact]
    public void RobinHoodDictionary_AfterDefaultKeyRemove_ReturnsFalse()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 555;
        map[1] = 100;
        map.Remove(0);
        Assert.False(map.ContainsValue(555));
        Assert.True(map.ContainsValue(100));
    }

    [Fact]
    public void RobinHoodDictionary_AfterClear_ReturnsFalse()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher> { [1] = 100, [2] = 200 };
        map[0] = 300;
        map.Clear();
        Assert.False(map.ContainsValue(100));
        Assert.False(map.ContainsValue(200));
        Assert.False(map.ContainsValue(300));
    }

    // ---------------- FrozenCelerityDictionary ----------------
    // Read-only, so the resize / remove / clear variants do not apply.

    private static FrozenCelerityDictionary<TValue> Frozen<TValue>(
        params (string key, TValue value)[] pairs)
        => new FrozenCelerityDictionary<TValue>(
            pairs.Select(p => new KeyValuePair<string, TValue>(p.key, p.value)));

    [Fact]
    public void FrozenCelerityDictionary_EmptyMap_ReturnsFalse()
    {
        var map = Frozen<int>();
        Assert.False(map.ContainsValue(0));
        Assert.False(map.ContainsValue(42));
    }

    [Fact]
    public void FrozenCelerityDictionary_FindsValueInRegularSlot()
    {
        var map = Frozen(("a", 100), ("b", 200), ("c", 300));
        Assert.True(map.ContainsValue(200));
    }

    [Fact]
    public void FrozenCelerityDictionary_ReturnsFalseForMissingValue()
    {
        var map = Frozen(("a", 100), ("b", 200));
        Assert.False(map.ContainsValue(999));
    }

    [Fact]
    public void FrozenCelerityDictionary_FindsValueOnlyInNullKeySlot()
    {
        var map = Frozen<int>((null!, 777));
        Assert.True(map.ContainsValue(777));
    }

    [Fact]
    public void FrozenCelerityDictionary_DefaultValueLookup_ZeroValue()
    {
        // Empty slots in the frozen table hold default(TValue); ContainsValue must
        // skip them and only report a real entry whose value is the default.
        var empty = Frozen<int>();
        Assert.False(empty.ContainsValue(0));

        var map = Frozen(("a", 0), ("b", 1));
        Assert.True(map.ContainsValue(0));
    }

    [Fact]
    public void FrozenCelerityDictionary_NullValueLookup_ReferenceType()
    {
        var map = Frozen(("a", "one"), ("b", (string?)null));
        Assert.True(map.ContainsValue(null));

        var noNulls = Frozen(("a", "one"), ("b", "two"));
        Assert.False(noNulls.ContainsValue(null));
    }

    [Fact]
    public void FrozenCelerityDictionary_DuplicateValues_ReturnsTrue()
    {
        var map = Frozen(("a", 42), ("b", 42), ("c", 42));
        Assert.True(map.ContainsValue(42));
    }

    // ---------------- CelerityMultiMap ----------------
    // ContainsValue scans every value group (plus the out-of-band default-key
    // group), matching BCL ContainsValue semantics across the whole map.

    [Fact]
    public void CelerityMultiMap_EmptyMap_ReturnsFalse()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        Assert.False(map.ContainsValue(0));
        Assert.False(map.ContainsValue(42));
    }

    [Fact]
    public void CelerityMultiMap_FindsValueInRegularGroup()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 100);
        map.Add("a", 200);
        map.Add("b", 300);
        Assert.True(map.ContainsValue(200));
        Assert.True(map.ContainsValue(300));
    }

    [Fact]
    public void CelerityMultiMap_ReturnsFalseForMissingValue()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 100);
        map.Add("b", 200);
        Assert.False(map.ContainsValue(999));
    }

    [Fact]
    public void CelerityMultiMap_FindsValueOnlyInDefaultKeyGroup()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add(null!, 777);
        Assert.True(map.ContainsValue(777));
    }

    [Fact]
    public void CelerityMultiMap_NullValueLookup_ReferenceType()
    {
        var map = new CelerityMultiMap<string, string, StringFnV1AHasher>();
        map.Add("a", "one");
        map.Add("b", null!);
        Assert.True(map.ContainsValue(null));

        var noNulls = new CelerityMultiMap<string, string, StringFnV1AHasher>();
        noNulls.Add("a", "one");
        noNulls.Add("b", "two");
        Assert.False(noNulls.ContainsValue(null));
    }

    [Fact]
    public void CelerityMultiMap_DuplicateValues_ReturnsTrue()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 42);
        map.Add("b", 42);
        Assert.True(map.ContainsValue(42));
    }

    // ---------------- SmallDictionary ----------------

    [Fact]
    public void SmallDictionary_EmptyMap_ReturnsFalse()
    {
        var map = new SmallDictionary<int, int>();
        Assert.False(map.ContainsValue(0));
        Assert.False(map.ContainsValue(42));
    }

    [Fact]
    public void SmallDictionary_FindsValueInRegularSlot()
    {
        var map = new SmallDictionary<int, int> { [1] = 100, [2] = 200, [3] = 300 };
        Assert.True(map.ContainsValue(200));
    }

    [Fact]
    public void SmallDictionary_ReturnsFalseForMissingValue()
    {
        var map = new SmallDictionary<int, int> { [1] = 100, [2] = 200 };
        Assert.False(map.ContainsValue(999));
    }

    [Fact]
    public void SmallDictionary_DefaultValueLookup_ZeroValue()
    {
        // A stored value of 0 must be reachable; an empty map must NOT report 0
        // (the unused tail of the backing array must be excluded from the scan).
        var map = new SmallDictionary<int, int> { [5] = 0, [6] = 1 };
        Assert.True(map.ContainsValue(0));

        var empty = new SmallDictionary<int, int>();
        Assert.False(empty.ContainsValue(0));
    }

    [Fact]
    public void SmallDictionary_AfterRemove_DoesNotReportRemovedValue()
    {
        // Removal swaps the last entry into the gap and clears the freed tail
        // slot, so a removed value must not linger in the scan range.
        var map = new SmallDictionary<int, int> { [1] = 100, [2] = 200, [3] = 300 };
        Assert.True(map.Remove(2));
        Assert.False(map.ContainsValue(200));
        Assert.True(map.ContainsValue(100));
        Assert.True(map.ContainsValue(300));
    }

    [Fact]
    public void SmallDictionary_NullValue_ReturnsTrue_WhenPresent()
    {
        var map = new SmallDictionary<int, string?> { [1] = null, [2] = "x" };
        Assert.True(map.ContainsValue(null));
    }
}
