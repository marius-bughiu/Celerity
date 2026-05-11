using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests for the <c>IEnumerable&lt;T&gt;</c> constructor on
/// <see cref="IntSet{THasher}"/> and <see cref="CeleritySet{T, THasher}"/>.
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
}
