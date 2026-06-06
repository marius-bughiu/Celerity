using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests for the <c>IEnumerable&lt;T&gt;</c> constructor on
/// <see cref="IntSet{THasher}"/>, <see cref="LongSet{THasher}"/>,
/// <see cref="CeleritySet{T, THasher}"/>, and the build-once
/// <see cref="FrozenCeleritySet{THasher}"/>.
///
/// Mirrors <see cref="IEnumerableConstructorTests"/> for the dictionary
/// equivalents, but follows BCL <see cref="HashSet{T}"/> semantics rather than
/// <see cref="Dictionary{TKey, TValue}"/> semantics: duplicate elements are
/// silently deduplicated rather than throwing.
/// </summary>
public class SetIEnumerableConstructorTests
{
    // ──────────────────────────────────────────────────────────────
    //  IntSet — source argument validation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntSet_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<int>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() => new IntSet(source!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void IntSet_ShouldStillValidate_LoadFactor_WhenConstructedFromSource()
    {
        var source = new[] { 1 };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IntSet(source, loadFactor: 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IntSet(source, loadFactor: 0f));
    }

    // ──────────────────────────────────────────────────────────────
    //  IntSet — happy path
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntSet_ShouldSupportEmptySource()
    {
        var set = new IntSet(Array.Empty<int>());

        Assert.Equal(0, set.Count);
        Assert.False(set.Contains(0));
        Assert.False(set.Contains(1));
    }

    [Fact]
    public void IntSet_ShouldCopyAllElements_FromArraySource()
    {
        var source = new[] { 1, 2, 3, 4, 5 };

        var set = new IntSet(source);

        Assert.Equal(5, set.Count);
        foreach (int item in source)
            Assert.True(set.Contains(item));
    }

    [Fact]
    public void IntSet_ShouldCopyAllElements_FromListSource()
    {
        var source = new List<int> { 10, 20, 30 };

        var set = new IntSet(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(10));
        Assert.True(set.Contains(20));
        Assert.True(set.Contains(30));
    }

    [Fact]
    public void IntSet_ShouldCopyAllElements_FromNonCollectionEnumerableSource()
    {
        // Enumerable.Range is not an ICollection<int>; it forces the
        // non-collection capacity-fallback path.
        IEnumerable<int> source = Enumerable.Range(1, 50);

        var set = new IntSet(source);

        Assert.Equal(50, set.Count);
        for (int i = 1; i <= 50; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void IntSet_ShouldSilentlyDedupe_DuplicateElements()
    {
        var source = new[] { 1, 2, 1, 3, 2, 4, 1 };

        var set = new IntSet(source);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(3));
        Assert.True(set.Contains(4));
    }

    [Fact]
    public void IntSet_ShouldSilentlyDedupe_DuplicateZeroElements()
    {
        // Zero lives in the out-of-band slot — make sure the dedupe path covers it.
        var source = new[] { 0, 1, 0, 2, 0 };

        var set = new IntSet(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
    }

    [Fact]
    public void IntSet_ShouldCaptureZeroElement_FromSource()
    {
        var source = new[] { 0, 7, 13 };

        var set = new IntSet(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(7));
        Assert.True(set.Contains(13));
    }

    [Fact]
    public void IntSet_ShouldHandleLargeSource()
    {
        IEnumerable<int> source = Enumerable.Range(1, 500);

        var set = new IntSet(source);

        Assert.Equal(500, set.Count);
        for (int i = 1; i <= 500; i++)
            Assert.True(set.Contains(i), $"missing element {i}");
    }

    [Fact]
    public void IntSet_ShouldBeIndependent_FromSourceArray()
    {
        var source = new[] { 1, 2, 3 };
        var set = new IntSet(source);

        // Mutating the source after construction must not affect the set.
        source[0] = 999;

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(1));
        Assert.False(set.Contains(999));
    }

    [Fact]
    public void IntSet_ShouldUseCallerCapacity_WhenLargerThanSourceCount()
    {
        var source = new[] { 1, 2, 3 };

        // Caller asks for capacity 1024 — should not throw and should still copy.
        var set = new IntSet(source, capacity: 1024);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(3));
    }

    [Fact]
    public void IntSet_ShouldRoundtrip_FromAnotherIntSetEnumeration()
    {
        var original = new IntSet { 0, 1, 2, 3, 4, 5 };

        var copy = new IntSet(original);

        Assert.Equal(original.Count, copy.Count);
        foreach (int item in original)
            Assert.True(copy.Contains(item));
    }

    [Fact]
    public void IntSet_OpenGeneric_ShouldAcceptIEnumerableSource()
    {
        var source = new[] { 1, 2, 3 };

        var set = new IntSet<Int32WangNaiveHasher>(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(3));
    }

    // ──────────────────────────────────────────────────────────────
    //  LongSet — source argument validation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void LongSet_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<long>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() => new LongSet(source!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void LongSet_ShouldStillValidate_LoadFactor_WhenConstructedFromSource()
    {
        var source = new[] { 1L };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LongSet(source, loadFactor: 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LongSet(source, loadFactor: 0f));
    }

    // ──────────────────────────────────────────────────────────────
    //  LongSet — happy path
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void LongSet_ShouldSupportEmptySource()
    {
        var set = new LongSet(Array.Empty<long>());

        Assert.Equal(0, set.Count);
        Assert.False(set.Contains(0L));
        Assert.False(set.Contains(1L));
    }

    [Fact]
    public void LongSet_ShouldCopyAllElements_FromArraySource()
    {
        var source = new[] { 1L, 2L, 3L, 4L, 5L };

        var set = new LongSet(source);

        Assert.Equal(5, set.Count);
        foreach (long item in source)
            Assert.True(set.Contains(item));
    }

    [Fact]
    public void LongSet_ShouldCopyAllElements_FromListSource()
    {
        var source = new List<long> { 10L, 20L, 30L };

        var set = new LongSet(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(10L));
        Assert.True(set.Contains(20L));
        Assert.True(set.Contains(30L));
    }

    [Fact]
    public void LongSet_ShouldCopyAllElements_FromNonCollectionEnumerableSource()
    {
        IEnumerable<long> source = Enumerable.Range(1, 50).Select(i => (long)i);

        var set = new LongSet(source);

        Assert.Equal(50, set.Count);
        for (long i = 1; i <= 50; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void LongSet_ShouldSilentlyDedupe_DuplicateElements()
    {
        var source = new[] { 1L, 2L, 1L, 3L, 2L, 4L, 1L };

        var set = new LongSet(source);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(2L));
        Assert.True(set.Contains(3L));
        Assert.True(set.Contains(4L));
    }

    [Fact]
    public void LongSet_ShouldSilentlyDedupe_DuplicateZeroElements()
    {
        var source = new[] { 0L, 1L, 0L, 2L, 0L };

        var set = new LongSet(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(0L));
        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(2L));
    }

    [Fact]
    public void LongSet_ShouldCaptureZeroElement_FromSource()
    {
        var source = new[] { 0L, 7L, 13L };

        var set = new LongSet(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(0L));
        Assert.True(set.Contains(7L));
        Assert.True(set.Contains(13L));
    }

    [Fact]
    public void LongSet_ShouldHandleLargeSource()
    {
        IEnumerable<long> source = Enumerable.Range(1, 500).Select(i => (long)i);

        var set = new LongSet(source);

        Assert.Equal(500, set.Count);
        for (long i = 1; i <= 500; i++)
            Assert.True(set.Contains(i), $"missing element {i}");
    }

    [Fact]
    public void LongSet_ShouldBeIndependent_FromSourceArray()
    {
        var source = new[] { 1L, 2L, 3L };
        var set = new LongSet(source);

        source[0] = 999L;

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(1L));
        Assert.False(set.Contains(999L));
    }

    [Fact]
    public void LongSet_ShouldUseCallerCapacity_WhenLargerThanSourceCount()
    {
        var source = new[] { 1L, 2L, 3L };

        var set = new LongSet(source, capacity: 1024);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(2L));
        Assert.True(set.Contains(3L));
    }

    [Fact]
    public void LongSet_ShouldRoundtrip_FromAnotherLongSetEnumeration()
    {
        var original = new LongSet { 0L, 1L, 2L, 3L, 4L, 5L };

        var copy = new LongSet(original);

        Assert.Equal(original.Count, copy.Count);
        foreach (long item in original)
            Assert.True(copy.Contains(item));
    }

    [Fact]
    public void LongSet_OpenGeneric_ShouldAcceptIEnumerableSource()
    {
        var source = new[] { 1L, 2L, 3L };

        var set = new LongSet<Int64WangHasher>(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(2L));
        Assert.True(set.Contains(3L));
    }

    [Fact]
    public void LongSet_ShouldPreserve_ExtremeKeyValues_FromSource()
    {
        var source = new[]
        {
            long.MaxValue,
            long.MinValue,
            (long)int.MaxValue + 1L,
            (long)int.MinValue - 1L,
            -1L,
        };

        var set = new LongSet(source);

        Assert.Equal(source.Length, set.Count);
        foreach (long item in source)
            Assert.True(set.Contains(item), $"missing element {item}");
    }

    // ──────────────────────────────────────────────────────────────
    //  CeleritySet — source argument validation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void CeleritySet_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<string>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CeleritySet<string, StringFnV1AHasher>(source!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void CeleritySet_ShouldStillValidate_LoadFactor_WhenConstructedFromSource()
    {
        var source = new[] { "a" };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CeleritySet<string, StringFnV1AHasher>(source, loadFactor: 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CeleritySet<string, StringFnV1AHasher>(source, loadFactor: 0f));
    }

    // ──────────────────────────────────────────────────────────────
    //  CeleritySet — happy path
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void CeleritySet_ShouldSupportEmptySource()
    {
        var set = new CeleritySet<string, StringFnV1AHasher>(Array.Empty<string>());

        Assert.Equal(0, set.Count);
        Assert.False(set.Contains("anything"));
    }

    [Fact]
    public void CeleritySet_ShouldCopyAllElements_FromArraySource()
    {
        var source = new[] { "a", "b", "c" };

        var set = new CeleritySet<string, StringFnV1AHasher>(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains("a"));
        Assert.True(set.Contains("b"));
        Assert.True(set.Contains("c"));
    }

    [Fact]
    public void CeleritySet_ShouldCopyAllElements_FromNonCollectionEnumerableSource()
    {
        IEnumerable<int> source = Enumerable.Range(1, 50);

        var set = new CeleritySet<int, Int32WangNaiveHasher>(source);

        Assert.Equal(50, set.Count);
        for (int i = 1; i <= 50; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void CeleritySet_ShouldSilentlyDedupe_DuplicateElements()
    {
        var source = new[] { "a", "b", "a", "c", "b", "a" };

        var set = new CeleritySet<string, StringFnV1AHasher>(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains("a"));
        Assert.True(set.Contains("b"));
        Assert.True(set.Contains("c"));
    }

    [Fact]
    public void CeleritySet_ShouldSilentlyDedupe_DuplicateNullElements()
    {
        // null is the out-of-band slot for reference-typed sets — ensure dedupe covers it.
        var source = new string?[] { "a", null, "b", null, "c", null };

        var set = new CeleritySet<string?, StringFnV1AHasher>(source);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(null));
        Assert.True(set.Contains("a"));
        Assert.True(set.Contains("b"));
        Assert.True(set.Contains("c"));
    }

    [Fact]
    public void CeleritySet_ShouldSilentlyDedupe_DuplicateDefaultValueTypeElements()
    {
        // 0 is the out-of-band default(int) slot for value-typed sets — ensure
        // dedupe covers it as well.
        var source = new[] { 0, 1, 0, 2, 0 };

        var set = new CeleritySet<int, Int32WangNaiveHasher>(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
    }

    [Fact]
    public void CeleritySet_ShouldCaptureNullElement_FromSource()
    {
        var source = new string?[] { null, "x", "y" };

        var set = new CeleritySet<string?, StringFnV1AHasher>(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(null));
        Assert.True(set.Contains("x"));
        Assert.True(set.Contains("y"));
    }

    [Fact]
    public void CeleritySet_ShouldHandleLargeSource()
    {
        IEnumerable<int> source = Enumerable.Range(1, 500);

        var set = new CeleritySet<int, Int32WangNaiveHasher>(source);

        Assert.Equal(500, set.Count);
        for (int i = 1; i <= 500; i++)
            Assert.True(set.Contains(i), $"missing element {i}");
    }

    [Fact]
    public void CeleritySet_ShouldBeIndependent_FromSourceArray()
    {
        var source = new[] { "a", "b", "c" };
        var set = new CeleritySet<string, StringFnV1AHasher>(source);

        source[0] = "MUTATED";

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains("a"));
        Assert.False(set.Contains("MUTATED"));
    }

    [Fact]
    public void CeleritySet_ShouldUseCallerCapacity_WhenLargerThanSourceCount()
    {
        var source = new[] { "a", "b", "c" };

        var set = new CeleritySet<string, StringFnV1AHasher>(source, capacity: 1024);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains("a"));
        Assert.True(set.Contains("b"));
        Assert.True(set.Contains("c"));
    }

    [Fact]
    public void CeleritySet_ShouldRoundtrip_FromAnotherCeleritySetEnumeration()
    {
        var original = new CeleritySet<int, Int32WangNaiveHasher> { 0, 1, 2, 3, 4, 5 };

        var copy = new CeleritySet<int, Int32WangNaiveHasher>(original);

        Assert.Equal(original.Count, copy.Count);
        foreach (int item in original)
            Assert.True(copy.Contains(item));
    }

    [Fact]
    public void CeleritySet_ShouldRoundtrip_FromIntSetEnumeration()
    {
        // Cross-collection copy: build a CeleritySet from an IntSet.
        var ints = new IntSet { 0, 5, 10, 15 };

        var copy = new CeleritySet<int, Int32WangNaiveHasher>(ints);

        Assert.Equal(ints.Count, copy.Count);
        foreach (int item in ints)
            Assert.True(copy.Contains(item));
    }

    // ──────────────────────────────────────────────────────────────
    //  FrozenCeleritySet — source argument validation
    // ──────────────────────────────────────────────────────────────
    //
    //  FrozenCeleritySet is immutable and string-only: its single constructor
    //  takes an IEnumerable<string> and there is NO capacity / loadFactor (the
    //  perfect-hash build sizes the table itself), so the loadFactor / capacity
    //  rows above genuinely do not apply. The IEnumerable-source contract — null
    //  rejection, dedupe, the out-of-band null element, large-source fidelity, and
    //  source independence — does, and is mirrored here.

    [Fact]
    public void FrozenCeleritySet_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<string>? source = null;

        Assert.Throws<ArgumentNullException>(() => new FrozenCeleritySet(source!));
    }

    // ──────────────────────────────────────────────────────────────
    //  FrozenCeleritySet — happy path
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void FrozenCeleritySet_ShouldSupportEmptySource()
    {
        var set = new FrozenCeleritySet(Array.Empty<string>());

        Assert.Empty(set);
        Assert.False(set.Contains("anything"));
    }

    [Fact]
    public void FrozenCeleritySet_ShouldCopyAllElements_FromArraySource()
    {
        var source = new[] { "a", "b", "c" };

        var set = new FrozenCeleritySet(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains("a"));
        Assert.True(set.Contains("b"));
        Assert.True(set.Contains("c"));
    }

    [Fact]
    public void FrozenCeleritySet_ShouldCopyAllElements_FromNonCollectionEnumerableSource()
    {
        // Enumerable.Select is not an ICollection<string>; it forces the
        // non-collection capacity-fallback path of the materialization.
        IEnumerable<string> source = Enumerable.Range(1, 50).Select(i => "n" + i);

        var set = new FrozenCeleritySet(source);

        Assert.Equal(50, set.Count);
        for (int i = 1; i <= 50; i++)
            Assert.True(set.Contains("n" + i));
    }

    [Fact]
    public void FrozenCeleritySet_ShouldSilentlyDedupe_DuplicateElements()
    {
        var source = new[] { "a", "b", "a", "c", "b", "a" };

        var set = new FrozenCeleritySet(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains("a"));
        Assert.True(set.Contains("b"));
        Assert.True(set.Contains("c"));
    }

    [Fact]
    public void FrozenCeleritySet_ShouldSilentlyDedupe_DuplicateNullElements()
    {
        // null is the out-of-band element — ensure dedupe covers it.
        var source = new[] { "a", null!, "b", null!, "c", null! };

        var set = new FrozenCeleritySet(source);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(null!));
        Assert.True(set.Contains("a"));
        Assert.True(set.Contains("b"));
        Assert.True(set.Contains("c"));
    }

    [Fact]
    public void FrozenCeleritySet_ShouldCaptureNullElement_FromSource()
    {
        var source = new[] { null!, "x", "y" };

        var set = new FrozenCeleritySet(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(null!));
        Assert.True(set.Contains("x"));
        Assert.True(set.Contains("y"));
    }

    [Fact]
    public void FrozenCeleritySet_ShouldHandleLargeSource()
    {
        IEnumerable<string> source = Enumerable.Range(1, 500).Select(i => "item-" + i);

        var set = new FrozenCeleritySet(source);

        Assert.Equal(500, set.Count);
        for (int i = 1; i <= 500; i++)
            Assert.True(set.Contains("item-" + i), $"missing element item-{i}");
    }

    [Fact]
    public void FrozenCeleritySet_ShouldBeIndependent_FromSourceArray()
    {
        var source = new[] { "a", "b", "c" };
        var set = new FrozenCeleritySet(source);

        // Mutating the source after construction must not affect the frozen set.
        source[0] = "MUTATED";

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains("a"));
        Assert.False(set.Contains("MUTATED"));
    }

    [Fact]
    public void FrozenCeleritySet_OpenGeneric_ShouldAcceptIEnumerableSource()
    {
        var source = new[] { "a", "b", "c" };

        var set = new FrozenCeleritySet<StringMurmur3Hasher>(source);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains("a"));
        Assert.True(set.Contains("b"));
        Assert.True(set.Contains("c"));
    }

    [Fact]
    public void FrozenCeleritySet_ShouldRoundtrip_FromAnotherFrozenSetEnumeration()
    {
        var original = new FrozenCeleritySet(new[] { "a", "b", "c", "d" });

        var copy = new FrozenCeleritySet(original);

        Assert.Equal(original.Count, copy.Count);
        foreach (string item in original)
            Assert.True(copy.Contains(item));
    }
}
