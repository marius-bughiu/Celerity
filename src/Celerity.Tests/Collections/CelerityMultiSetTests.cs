using System;
using System.Collections.Generic;
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Issue #235: CelerityMultiSet<T, THasher>, the counting-multiset (bag/counter)
// sibling of CelerityMultiMap. This suite covers the core surface: counting Adds,
// Add(item, count), the indexer / GetCount (zero-on-miss), Contains, the two
// removal shapes (Remove decrements, RemoveAll discards), SetCount (incl. the
// remove-at-zero path), Clear, Count vs TotalCount, the out-of-band default
// element (0, null, Guid.Empty), per-element overflow guarding, and the
// IEnumerable<T> counting constructor.
public class CelerityMultiSetTests
{
    [Fact]
    public void Add_ShouldCountRepeatedElements()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a");
        set.Add("a");
        set.Add("a");

        Assert.Equal(1, set.Count);          // one distinct element
        Assert.Equal(3L, set.TotalCount);    // three occurrences
        Assert.Equal(3, set["a"]);
    }

    [Fact]
    public void Add_ShouldKeepDistinctElementsSeparate()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a");
        set.Add("b");
        set.Add("a");

        Assert.Equal(2, set.Count);
        Assert.Equal(3L, set.TotalCount);
        Assert.Equal(2, set["a"]);
        Assert.Equal(1, set["b"]);
    }

    [Fact]
    public void AddWithCount_ShouldIncrementByThatMany()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a", 5);
        set.Add("a", 2);

        Assert.Equal(7, set["a"]);
        Assert.Equal(7L, set.TotalCount);
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void AddWithZeroCount_ShouldBeNoOp_AndNotRegisterElement()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("ghost", 0);

        Assert.Equal(0, set.Count);
        Assert.Equal(0L, set.TotalCount);
        Assert.False(set.Contains("ghost"));
    }

    [Fact]
    public void AddWithNegativeCount_ShouldThrow()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Add("a", -1));
    }

    [Fact]
    public void Add_ShouldThrowOverflow_WhenMultiplicityExceedsIntMax()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a", int.MaxValue);
        Assert.Throws<OverflowException>(() => set.Add("a"));
        // The failed add left the count untouched.
        Assert.Equal(int.MaxValue, set["a"]);
    }

    [Fact]
    public void Indexer_ShouldReturnZero_ForMissingElement()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a");

        Assert.Equal(0, set["missing"]);
        Assert.Equal(0, set.GetCount("missing"));
    }

    [Fact]
    public void IndexerSet_ShouldSetExactMultiplicity()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set["a"] = 4;
        Assert.Equal(4, set["a"]);
        Assert.Equal(4L, set.TotalCount);

        set["a"] = 2; // overwrite down
        Assert.Equal(2, set["a"]);
        Assert.Equal(2L, set.TotalCount);
    }

    [Fact]
    public void IndexerSet_ToZero_ShouldRemoveElement()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a", 3);
        set["a"] = 0;

        Assert.False(set.Contains("a"));
        Assert.Equal(0, set.Count);
        Assert.Equal(0L, set.TotalCount);
    }

    [Fact]
    public void IndexerSet_Negative_ShouldThrow()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        Assert.Throws<ArgumentOutOfRangeException>(() => set["a"] = -1);
    }

    [Fact]
    public void Contains_ShouldReflectPresence()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a");

        Assert.True(set.Contains("a"));
        Assert.False(set.Contains("b"));
    }

    [Fact]
    public void Remove_ShouldDecrementByOne_AndKeepElementWhilePositive()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a", 2);

        Assert.True(set.Remove("a"));
        Assert.Equal(1, set["a"]);
        Assert.Equal(1, set.Count);
        Assert.Equal(1L, set.TotalCount);
    }

    [Fact]
    public void Remove_ShouldDropElement_WhenLastOccurrenceRemoved()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a");

        Assert.True(set.Remove("a"));
        Assert.False(set.Contains("a"));
        Assert.Equal(0, set.Count);
        Assert.Equal(0L, set.TotalCount);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenElementAbsent()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a");

        Assert.False(set.Remove("b"));
        Assert.Equal(1L, set.TotalCount); // unchanged
    }

    [Fact]
    public void RemoveAll_ShouldDiscardEveryOccurrence()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a", 5);
        set.Add("b", 2);

        Assert.True(set.RemoveAll("a"));
        Assert.False(set.Contains("a"));
        Assert.Equal(1, set.Count);
        Assert.Equal(2L, set.TotalCount); // only b's two occurrences remain
    }

    [Fact]
    public void RemoveAll_ShouldReturnFalse_ForMissingElement()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        Assert.False(set.RemoveAll("missing"));
    }

    [Fact]
    public void SetCount_ShouldReturnPreviousCount_AndOverwrite()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a", 3);

        Assert.Equal(3, set.SetCount("a", 10));
        Assert.Equal(10, set["a"]);
        Assert.Equal(10L, set.TotalCount);
    }

    [Fact]
    public void SetCount_FromZero_ShouldCreateElement()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        Assert.Equal(0, set.SetCount("a", 4));
        Assert.Equal(4, set["a"]);
        Assert.Equal(1, set.Count);
        Assert.Equal(4L, set.TotalCount);
    }

    [Fact]
    public void SetCount_ToZero_ShouldRemoveElement()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a", 7);

        Assert.Equal(7, set.SetCount("a", 0));
        Assert.False(set.Contains("a"));
        Assert.Equal(0, set.Count);
        Assert.Equal(0L, set.TotalCount);
    }

    [Fact]
    public void SetCount_ToSameValue_ShouldBeNoOp()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a", 3);

        Assert.Equal(3, set.SetCount("a", 3));
        Assert.Equal(3, set["a"]);
        Assert.Equal(3L, set.TotalCount);
    }

    [Fact]
    public void SetCount_Negative_ShouldThrow()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        Assert.Throws<ArgumentOutOfRangeException>(() => set.SetCount("a", -2));
    }

    [Fact]
    public void Clear_ShouldResetCountsAndContents()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add("a", 2);
        set.Add("b");
        set.Add(null!, 3);

        set.Clear();

        Assert.Equal(0, set.Count);
        Assert.Equal(0L, set.TotalCount);
        Assert.False(set.Contains("a"));
        Assert.False(set.Contains(null!));
        Assert.Equal(0, set["a"]);
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        set.Clear(); // _count == 0 early-return path
        Assert.Equal(0, set.Count);
        Assert.Equal(0L, set.TotalCount);
    }

    // ---------------- default-element (out-of-band) handling ----------------

    [Fact]
    public void NullElement_ShouldRoundTrip_AsOrdinaryElement()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add(null!);
        set.Add(null!);
        set.Add("a");

        Assert.True(set.Contains(null!));
        Assert.Equal(2, set[null!]);
        Assert.Equal(2, set.Count);
        Assert.Equal(3L, set.TotalCount);
    }

    [Fact]
    public void NullElement_Remove_ShouldDecrementThenDrop()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add(null!, 2);

        Assert.True(set.Remove(null!));
        Assert.Equal(1, set[null!]);

        Assert.True(set.Remove(null!));
        Assert.False(set.Contains(null!));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void NullElement_RemoveAll_ShouldDiscard()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add(null!, 5);

        Assert.True(set.RemoveAll(null!));
        Assert.False(set.Contains(null!));
        Assert.Equal(0L, set.TotalCount);
    }

    [Fact]
    public void NullElement_SetCount_ShouldCreateOverwriteAndRemove()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();

        Assert.Equal(0, set.SetCount(null!, 3)); // create
        Assert.Equal(3, set[null!]);
        Assert.Equal(1, set.Count);

        Assert.Equal(3, set.SetCount(null!, 5)); // overwrite
        Assert.Equal(5, set[null!]);

        Assert.Equal(5, set.SetCount(null!, 0)); // remove
        Assert.False(set.Contains(null!));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void ZeroElement_ShouldRoundTrip_ForIntElements()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        set.Add(0, 2);
        set.Add(1);

        Assert.True(set.Contains(0));
        Assert.Equal(2, set[0]);
        Assert.Equal(2, set.Count);
        Assert.Equal(3L, set.TotalCount);
    }

    [Fact]
    public void EmptyGuidElement_ShouldRoundTrip()
    {
        var set = new CelerityMultiSet<Guid, GuidHasher>();
        set.Add(Guid.Empty, 2);
        var id = Guid.NewGuid();
        set.Add(id);

        Assert.Equal(2, set[Guid.Empty]);
        Assert.Equal(1, set[id]);
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_ForAbsentDefaultElement()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();

        // default element (0) was never added.
        Assert.False(set.Remove(0));
        Assert.False(set.RemoveAll(0));
    }

    // ---------------- constructor from IEnumerable<T> ----------------

    [Fact]
    public void Constructor_FromSequence_ShouldCountOccurrences()
    {
        var source = new[] { "a", "b", "a", "c", "a", "b" };

        var set = new CelerityMultiSet<string, StringFnV1AHasher>(source);

        Assert.Equal(3, set.Count);       // a, b, c
        Assert.Equal(6L, set.TotalCount);
        Assert.Equal(3, set["a"]);
        Assert.Equal(2, set["b"]);
        Assert.Equal(1, set["c"]);
    }

    [Fact]
    public void Constructor_FromSequence_ShouldThrow_ForNullSource()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CelerityMultiSet<string, StringFnV1AHasher>(null!));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Constructor_FromSequence_ShouldCountNullElement()
    {
        var source = new string?[] { null, "a", null };

        var set = new CelerityMultiSet<string, StringFnV1AHasher>(source!);

        Assert.Equal(2, set[null!]);
        Assert.Equal(1, set["a"]);
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void GrowthUnderManyElements_ShouldPreserveAllCounts()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>(capacity: 4);
        for (int i = 0; i < 500; i++)
            set.Add(i, i + 1);

        Assert.Equal(500, set.Count);
        long expectedTotal = 0;
        for (int i = 0; i < 500; i++)
        {
            Assert.Equal(i + 1, set[i]);
            expectedTotal += i + 1;
        }
        Assert.Equal(expectedTotal, set.TotalCount);
    }

    [Fact]
    public void Elements_ShouldYieldEachDistinctElementOnce()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        set.Add(1, 3);
        set.Add(2, 1);
        set.Add(0, 5); // default element

        var elements = set.Elements.ToList();
        elements.Sort();

        Assert.Equal(new[] { 0, 1, 2 }, elements);
        Assert.Equal(3, set.Elements.Count);
    }
}
