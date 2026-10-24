using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Dedicated tests for LinkedSet, mirroring LinkedDictionaryTests for the ordered
// surface and HashCachingSetTests for the shared set contract: insertion order
// that survives removals, MoveToEnd / MoveToFront, TryGetFirst / TryGetLast,
// TryRemoveFirst / TryRemoveLast, in-table storage of the default element, the
// order the set-algebra operations leave behind, and the sizing rules (growth,
// EnsureCapacity, TrimExcess, free-list reuse). The chain-walk stress (every
// element in one bucket) is in LinkedSetCollisionTests.
public class LinkedSetTests
{
    private static int[] ItemsOf<THasher>(LinkedSet<int, THasher> set)
        where THasher : struct, IHashProvider<int>
    {
        var items = new List<int>();
        foreach (int item in set)
            items.Add(item);
        return items.ToArray();
    }

    private static string?[] ItemsOf<THasher>(LinkedSet<string, THasher> set)
        where THasher : struct, IHashProvider<string>
    {
        var items = new List<string?>();
        foreach (string? item in set)
            items.Add(item);
        return items.ToArray();
    }

    // ---------------------------------------------------------------
    //  Shared set contract (mirror of HashCachingSetTests)
    // ---------------------------------------------------------------

    [Fact]
    public void TryAdd_ShouldAddAndContain()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();

        Assert.True(set.TryAdd(10));
        Assert.True(set.Contains(10));
        Assert.Single(set);
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenDuplicate()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        Assert.True(set.TryAdd(1));
        Assert.False(set.TryAdd(1));

        Assert.Single(set);
    }

    [Fact]
    public void Add_ShouldThrow_WhenDuplicate()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(1);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(1));
        Assert.Equal("item", ex.ParamName);
        Assert.Single(set);
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenNotPresent()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        Assert.False(set.Contains(7));

        set.Add(8);
        Assert.False(set.Contains(7));
    }

    [Fact]
    public void Remove_ShouldDeleteElementAndMakeItUnreachable()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(7);

        Assert.True(set.Remove(7));
        Assert.False(set.Contains(7));
        Assert.Empty(set);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenNotPresent()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        Assert.False(set.Remove(7));

        set.Add(8);
        Assert.False(set.Remove(7));
        Assert.Single(set);
    }

    [Fact]
    public void Set_ShouldResize_WhenThresholdExceeded()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(16, loadFactor: 0.5f);
        for (int i = 1; i <= 40; i++)
            set.Add(i); // forces at least one resize

        Assert.Equal(40, set.Count);
        for (int i = 1; i <= 40; i++)
            Assert.True(set.Contains(i));
        Assert.Equal(Enumerable.Range(1, 40), ItemsOf(set));
    }

    [Fact]
    public void ZeroIntElement_ShouldRoundTrip()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        Assert.False(set.Contains(0));

        Assert.True(set.TryAdd(0));
        Assert.False(set.TryAdd(0));
        Assert.True(set.Contains(0));
        Assert.Single(set);

        set.Add(1);
        Assert.True(set.Remove(0));
        Assert.False(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.False(set.Remove(0));
    }

    [Fact]
    public void DefaultElement_ShouldSurviveResize()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(16, loadFactor: 0.5f);
        set.Add(0);
        for (int i = 1; i <= 40; i++)
            set.Add(i); // triggers resize while the default element is live.

        Assert.Equal(41, set.Count);
        Assert.True(set.Contains(0));
        Assert.Equal(Enumerable.Range(0, 41), ItemsOf(set));
    }

    [Fact]
    public void GuidEmpty_ShouldRoundTrip()
    {
        var set = new LinkedSet<Guid, GuidIdentityHasher>();
        var other = Guid.NewGuid();
        set.Add(other);
        set.Add(Guid.Empty);

        Assert.True(set.Contains(Guid.Empty));
        Assert.True(set.Contains(other));
        Assert.Equal(new[] { other, Guid.Empty }, set.ToArray());
        Assert.True(set.Remove(Guid.Empty));
        Assert.False(set.Contains(Guid.Empty));
    }

    [Fact]
    public void Clear_ShouldRemoveAllElements_AndKeepCapacity()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 0; i < 32; i++)
            set.Add(i);
        int capacity = set.EnsureCapacity(0);

        set.Clear();

        Assert.Empty(set);
        Assert.Equal(capacity, set.EnsureCapacity(0));
        for (int i = 0; i < 32; i++)
            Assert.False(set.Contains(i));
        Assert.False(set.TryGetFirst(out _));
        Assert.False(set.TryGetLast(out _));

        set.Add(5);
        set.Add(0);
        Assert.Equal(new[] { 5, 0 }, ItemsOf(set));
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        var enumerator = set.GetEnumerator();

        set.Clear(); // _count == 0 early-return path: no version bump

        Assert.Empty(set);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void ReferenceType_Null_ShouldRoundTrip()
    {
        var set = new LinkedSet<string, StringFnV1AHasher>();
        Assert.False(set.Contains(null!));

        Assert.True(set.TryAdd(null!));
        Assert.False(set.TryAdd(null!));
        set.Add("a");

        Assert.True(set.Contains(null!));
        Assert.Equal(new[] { null, "a" }, ItemsOf(set));
        Assert.True(set.Remove(null!));
        Assert.False(set.Contains(null!));
        Assert.Equal(new[] { "a" }, ItemsOf(set));
    }

    // ---------------------------------------------------------------
    //  Constructor validation and sizing
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldRejectNegativeCapacity()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new LinkedSet<int, Int32WangNaiveHasher>(-1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(float.NaN)]
    public void Constructor_ShouldRejectInvalidLoadFactor(float loadFactor)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new LinkedSet<int, Int32WangNaiveHasher>(16, loadFactor));
        Assert.Equal("loadFactor", ex.ParamName);
    }

    [Theory]
    [InlineData(0, 0.75f, 1)]     // one bucket, (int)0.75 == 0 → floor of one entry
    [InlineData(1, 0.75f, 1)]
    [InlineData(16, 0.75f, 12)]
    [InlineData(17, 0.75f, 24)]   // rounds up to 32 buckets
    [InlineData(16, 0.01f, 1)]    // (int)0.16 == 0 → floor of one entry
    [InlineData(64, 0.5f, 32)]
    public void Constructor_ShouldSizeEntriesFromBucketsAndLoadFactor(int capacity, float loadFactor, int expectedEntries)
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity, loadFactor);

        Assert.Equal(expectedEntries, set.EnsureCapacity(0));
    }

    [Fact]
    public void Constructor_WithCapacityZero_ShouldStillTakeElements()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(0);
        for (int i = 1; i <= 10; i++)
            set.Add(i);

        Assert.Equal(Enumerable.Range(1, 10), ItemsOf(set));
    }

    [Fact]
    public void SourceConstructor_ShouldRejectNullSource_BeforeValidatingTheRest()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new LinkedSet<int, Int32WangNaiveHasher>((IEnumerable<int>)null!, capacity: -1, loadFactor: 5f));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_ShouldRejectNegativeCapacity()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LinkedSet<int, Int32WangNaiveHasher>(new[] { 1, 2, 3 }, capacity: -1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(float.NaN)]
    public void SourceConstructor_ShouldRejectInvalidLoadFactor_ForCollectionSource(float loadFactor)
    {
        // A non-empty ICollection source takes the sizing helper's "count > 0" arm with a load
        // factor it must not size with; the primary constructor then rejects it.
        var source = new List<int> { 1, 2 };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LinkedSet<int, Int32WangNaiveHasher>(source, capacity: 16, loadFactor: loadFactor));
        Assert.Equal("loadFactor", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_ShouldRejectInvalidLoadFactor_ForEmptySource()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LinkedSet<int, Int32WangNaiveHasher>(new List<int>(), capacity: 16, loadFactor: 0f));
        Assert.Equal("loadFactor", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_ShouldPreserveSourceOrder_AndKeepFirstOccurrences()
    {
        var source = new[] { 5, 0, -3, 5, 42, 0, -3, 7 };

        var set = new LinkedSet<int, Int32WangNaiveHasher>(source);

        Assert.Equal(new[] { 5, 0, -3, 42, 7 }, ItemsOf(set));
        Assert.Equal(5, set.Count);
    }

    [Fact]
    public void SourceConstructor_ShouldDedupNulls_ForReferenceElements()
    {
        var source = new[] { "b", null, "a", null, "b" };

        var set = new LinkedSet<string, NullRejectingStringHasher>(source!);

        Assert.Equal(new[] { "b", null, "a" }, ItemsOf(set));
    }

    [Fact]
    public void SourceConstructor_ShouldPresizeForCollectionSource()
    {
        var source = Enumerable.Range(1, 100).ToArray();

        var set = new LinkedSet<int, Int32WangNaiveHasher>(source, capacity: 1);

        // MinTableSizeFor(100, 0.75) = 256 buckets → 192 entries, reached without growing.
        Assert.Equal(192, set.EnsureCapacity(0));
        Assert.Equal(Enumerable.Range(1, 100), ItemsOf(set));
    }

    [Fact]
    public void SourceConstructor_ShouldKeepLargerCapacity_WhenSourceIsSmaller()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 1 }, capacity: 64);

        Assert.Equal(48, set.EnsureCapacity(0));
    }

    [Fact]
    public void SourceConstructor_WithEmptyCollection_ShouldUseTheCapacity()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new List<int>(), capacity: 16);

        Assert.Empty(set);
        Assert.Equal(12, set.EnsureCapacity(0));
    }

    [Fact]
    public void SourceConstructor_ShouldNotPresize_ForLazyEnumerable()
    {
        static IEnumerable<int> Lazy()
        {
            for (int i = 1; i <= 100; i++)
                yield return i;
        }

        var set = new LinkedSet<int, Int32WangNaiveHasher>(Lazy(), capacity: 1);

        Assert.Equal(100, set.Count);
        Assert.Equal(Enumerable.Range(1, 100), ItemsOf(set));
    }

    // ---------------------------------------------------------------
    //  Insertion order
    // ---------------------------------------------------------------

    [Fact]
    public void Enumeration_ShouldFollowInsertionOrder_NotHashOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        int[] items = { 90, 3, 57, -12, 1000, 0, 8, 41 };
        foreach (int item in items)
            set.Add(item);

        Assert.Equal(items, ItemsOf(set));
    }

    [Fact]
    public void Remove_ShouldKeepTheRelativeOrderOfTheRest()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 8; i++)
            set.Add(i);

        Assert.True(set.Remove(1)); // head
        Assert.True(set.Remove(8)); // tail
        Assert.True(set.Remove(4)); // middle

        Assert.Equal(new[] { 2, 3, 5, 6, 7 }, ItemsOf(set));
    }

    [Fact]
    public void Remove_ShouldEmptyTheOrderList_WhenTheLastElementGoes()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(1);

        Assert.True(set.Remove(1)); // head and tail at once

        Assert.False(set.TryGetFirst(out _));
        Assert.False(set.TryGetLast(out _));
        set.Add(2);
        Assert.Equal(new[] { 2 }, ItemsOf(set));
    }

    [Fact]
    public void ReAddAfterRemove_ShouldAppendAtTheEnd_EvenWhenReusingAFreedSlot()
    {
        // Contrast with BCL HashSet, whose freed slot puts the re-added element back
        // where the removed one was.
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 5; i++)
            set.Add(i);

        Assert.True(set.Remove(2));
        set.Add(2);
        set.Add(9);

        Assert.Equal(new[] { 1, 3, 4, 5, 2, 9 }, ItemsOf(set));
    }

    [Fact]
    public void TryAddDuplicate_ShouldKeepTheElementsPosition()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

        Assert.False(set.TryAdd(1));

        Assert.Equal(new[] { 1, 2 }, ItemsOf(set));
    }

    [Fact]
    public void DefaultElement_ShouldKeepItsOrderPosition()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(3);
        set.Add(0);
        set.Add(7);

        Assert.Equal(new[] { 3, 0, 7 }, ItemsOf(set));

        Assert.True(set.Remove(3));
        Assert.True(set.TryGetFirst(out int first));
        Assert.Equal(0, first);

        Assert.True(set.MoveToEnd(0));
        Assert.Equal(new[] { 7, 0 }, ItemsOf(set));
    }

    [Fact]
    public void NullStringElement_ShouldKeepItsOrderPosition_AndNeverReachTheHasher()
    {
        // NullRejectingStringHasher throws on null, so any null-element call that reached it would fail.
        var set = new LinkedSet<string, NullRejectingStringHasher>();
        set.Add("a");
        set.Add(null!);
        set.Add("b");

        Assert.Equal(new[] { "a", null, "b" }, ItemsOf(set));
        Assert.True(set.Contains(null!));
        Assert.False(set.TryAdd(null!));
        Assert.Throws<ArgumentException>(() => set.Add(null!));
        Assert.True(set.MoveToFront(null!));
        Assert.Equal(new[] { null, "a", "b" }, ItemsOf(set));
        Assert.True(set.MoveToEnd(null!));
        Assert.Equal(new[] { "a", "b", null }, ItemsOf(set));

        // Growth and rebuilds reuse the stored hash, so they never re-hash the null element either.
        for (int i = 0; i < 40; i++)
            set.Add($"k{i}");
        set.TrimExcess();
        set.EnsureCapacity(500);
        Assert.True(set.Contains(null!));

        Assert.True(set.Remove(null!));
        Assert.False(set.Contains(null!));
        Assert.False(set.Remove(null!));
        Assert.False(set.MoveToEnd(null!));
        Assert.False(set.MoveToFront(null!));
    }

    [Fact]
    public void NullStringElement_ShouldBeRemovableFromEitherEnd_WithoutReachingTheHasher()
    {
        var set = new LinkedSet<string, NullRejectingStringHasher>();
        set.Add(null!);
        set.Add("x");
        set.Add("y");

        Assert.True(set.TryRemoveFirst(out string? first));
        Assert.Null(first);

        set.Add(null!);
        Assert.True(set.TryRemoveLast(out string? last));
        Assert.Null(last);
        Assert.Equal(new[] { "x", "y" }, ItemsOf(set));
    }

    [Fact]
    public void NullableElement_WithoutValue_ShouldNeverReachTheHasher()
    {
        var set = new LinkedSet<int?, NullRejectingNullableIntHasher>();
        set.Add(1);
        set.Add(null);
        set.Add(2);

        Assert.Equal(new int?[] { 1, null, 2 }, set.ToArray());
        Assert.True(set.Contains(null));
        Assert.False(set.TryAdd(null));
        Assert.True(set.Remove(null));
        Assert.False(set.Contains(null));
        Assert.Equal(new int?[] { 1, 2 }, set.ToArray());
    }

    [Fact]
    public void DefaultIntElement_ShouldNeverReachTheHasher()
    {
        var set = new LinkedSet<int, DefaultRejectingIntHasher>();
        set.Add(5);
        set.Add(0);

        Assert.True(set.Contains(0));
        Assert.True(set.MoveToFront(0));
        Assert.Equal(new[] { 0, 5 }, ItemsOf(set));
        Assert.True(set.Remove(0));
        Assert.Equal(new[] { 5 }, ItemsOf(set));
    }

    // ---------------------------------------------------------------
    //  MoveToEnd / MoveToFront
    // ---------------------------------------------------------------

    [Fact]
    public void MoveToEnd_ShouldMoveAPresentElementToTheEnd()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 4; i++)
            set.Add(i);

        Assert.True(set.MoveToEnd(1)); // from the head
        Assert.Equal(new[] { 2, 3, 4, 1 }, ItemsOf(set));
        Assert.True(set.MoveToEnd(3)); // from the middle
        Assert.Equal(new[] { 2, 4, 1, 3 }, ItemsOf(set));
        Assert.Equal(4, set.Count);

        Assert.True(set.TryGetLast(out int last));
        Assert.Equal(3, last);
        Assert.True(set.TryGetFirst(out int first));
        Assert.Equal(2, first);
    }

    [Fact]
    public void MoveToEnd_ShouldReturnFalse_WhenElementIsAbsent()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        Assert.False(set.MoveToEnd(1));

        set.Add(1);
        var enumerator = set.GetEnumerator();
        Assert.False(set.MoveToEnd(2));
        Assert.True(enumerator.MoveNext()); // a miss is not a modification
    }

    [Fact]
    public void MoveToEnd_ShouldBeANoOp_WhenElementIsAlreadyLast()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            set.Add(i);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.MoveToEnd(3));

        // No version bump: the enumerator keeps going.
        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current);
        Assert.Equal(new[] { 1, 2, 3 }, ItemsOf(set));
    }

    [Fact]
    public void MoveToEnd_ShouldBeANoOp_OnASingleElement()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(1);
        var enumerator = set.GetEnumerator();

        Assert.True(set.MoveToEnd(1));
        Assert.True(set.MoveToFront(1));

        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveToEnd_ShouldInvalidateEnumerator_WhenItMovesTheElement()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            set.Add(i);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.MoveToEnd(1));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveToFront_ShouldMoveAPresentElementToTheFront()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 4; i++)
            set.Add(i);

        Assert.True(set.MoveToFront(4)); // from the tail
        Assert.Equal(new[] { 4, 1, 2, 3 }, ItemsOf(set));
        Assert.True(set.MoveToFront(2)); // from the middle
        Assert.Equal(new[] { 2, 4, 1, 3 }, ItemsOf(set));

        Assert.True(set.TryGetFirst(out int first));
        Assert.Equal(2, first);
        Assert.True(set.TryGetLast(out int last));
        Assert.Equal(3, last);
    }

    [Fact]
    public void MoveToFront_ShouldReturnFalse_WhenElementIsAbsent()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        Assert.False(set.MoveToFront(1));

        set.Add(1);
        Assert.False(set.MoveToFront(2));
        Assert.Equal(new[] { 1 }, ItemsOf(set));
    }

    [Fact]
    public void MoveToFront_ShouldBeANoOp_WhenElementIsAlreadyFirst()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            set.Add(i);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.MoveToFront(1));

        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current);
    }

    [Fact]
    public void MoveToFront_ShouldInvalidateEnumerator_WhenItMovesTheElement()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            set.Add(i);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.MoveToFront(3));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveToFrontThenMoveToEnd_ShouldRoundTrip_OnTwoElements()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

        Assert.True(set.MoveToFront(2));
        Assert.Equal(new[] { 2, 1 }, ItemsOf(set));
        Assert.True(set.MoveToEnd(2));
        Assert.Equal(new[] { 1, 2 }, ItemsOf(set));
    }

    // ---------------------------------------------------------------
    //  TryGetFirst / TryGetLast / TryRemoveFirst / TryRemoveLast
    // ---------------------------------------------------------------

    [Fact]
    public void TryGetFirstAndLast_ShouldReturnFalseAndDefault_WhenEmpty()
    {
        var set = new LinkedSet<string, StringFnV1AHasher>();

        Assert.False(set.TryGetFirst(out string? first));
        Assert.Null(first);
        Assert.False(set.TryGetLast(out string? last));
        Assert.Null(last);
    }

    [Fact]
    public void TryGetFirstAndLast_ShouldReturnTheOldestAndNewest()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(9);
        set.Add(4);
        set.Add(6);

        Assert.True(set.TryGetFirst(out int first));
        Assert.Equal(9, first);
        Assert.True(set.TryGetLast(out int last));
        Assert.Equal(6, last);
        Assert.Equal(3, set.Count); // a peek removes nothing
    }

    [Fact]
    public void TryGetFirstAndLast_ShouldAgree_OnASingleElement()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(7);

        Assert.True(set.TryGetFirst(out int first));
        Assert.True(set.TryGetLast(out int last));
        Assert.Equal(7, first);
        Assert.Equal(7, last);
    }

    [Fact]
    public void TryRemoveFirstAndLast_ShouldReturnFalse_WhenEmpty()
    {
        var set = new LinkedSet<string, StringFnV1AHasher>();
        var enumerator = set.GetEnumerator();

        Assert.False(set.TryRemoveFirst(out string? first));
        Assert.Null(first);
        Assert.False(set.TryRemoveLast(out string? last));
        Assert.Null(last);

        Assert.False(enumerator.MoveNext()); // no version bump
    }

    [Fact]
    public void TryRemoveFirst_ShouldDrainInFifoOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        int[] items = { 5, 0, 3, 9, 1 };
        foreach (int item in items)
            set.Add(item);

        var drained = new List<int>();
        while (set.TryRemoveFirst(out int item))
        {
            Assert.False(set.Contains(item));
            drained.Add(item);
        }

        Assert.Equal(items, drained);
        Assert.Empty(set);
    }

    [Fact]
    public void TryRemoveLast_ShouldDrainInLifoOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        int[] items = { 5, 0, 3, 9, 1 };
        foreach (int item in items)
            set.Add(item);

        var drained = new List<int>();
        while (set.TryRemoveLast(out int item))
        {
            Assert.False(set.Contains(item));
            drained.Add(item);
        }

        Assert.Equal(Enumerable.Reverse(items), drained);
        Assert.Empty(set);
    }

    [Fact]
    public void TryRemoveFirst_ShouldInvalidateEnumerator()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(set.TryRemoveFirst(out _));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void TryRemoveFirstAndLast_ShouldLeaveTheTableUsable()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 6; i++)
            set.Add(i);

        Assert.True(set.TryRemoveFirst(out int first));
        Assert.True(set.TryRemoveLast(out int last));
        Assert.Equal(1, first);
        Assert.Equal(6, last);

        set.Add(7);
        set.Add(1);
        Assert.Equal(new[] { 2, 3, 4, 5, 7, 1 }, ItemsOf(set));
        for (int i = 1; i <= 5; i++)
            Assert.True(set.Contains(i));
        Assert.False(set.Contains(6));
    }

    // ---------------------------------------------------------------
    //  Growth, free-list reuse, EnsureCapacity, TrimExcess
    // ---------------------------------------------------------------

    [Fact]
    public void Growth_WithTinyLoadFactor_ShouldDoubleSeveralTimesInOneGrow()
    {
        // 16 buckets at 0.01 hold max(1, (int)0.16) = 1 entry. The second add must double
        // 16 → 32 → 64 → 128 → 256 before (int)(256 * 0.01) = 2 exceeds Count.
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 16, loadFactor: 0.01f);
        Assert.Equal(1, set.EnsureCapacity(0));

        set.Add(1);
        Assert.Equal(1, set.EnsureCapacity(0));
        set.Add(2);
        Assert.Equal(2, set.EnsureCapacity(0));

        for (int i = 3; i <= 50; i++)
            set.Add(i);

        Assert.Equal(50, set.Count);
        Assert.Equal(Enumerable.Range(1, 50), ItemsOf(set));
        for (int i = 1; i <= 50; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Growth_ShouldDoubleOnce_AtAnOrdinaryLoadFactor()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 4, loadFactor: 0.75f);
        Assert.Equal(3, set.EnsureCapacity(0));

        for (int i = 1; i <= 4; i++)
            set.Add(i);

        Assert.Equal(6, set.EnsureCapacity(0));
        Assert.Equal(new[] { 1, 2, 3, 4 }, ItemsOf(set));
    }

    [Fact]
    public void FreeListReuse_ShouldNotGrowTheTable()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 16);
        for (int i = 1; i <= 12; i++)
            set.Add(i); // exactly full: 12 entries
        Assert.Equal(12, set.EnsureCapacity(0));

        for (int round = 0; round < 100; round++)
        {
            int victim = (round % 12) + 1 + (round / 12) * 100;
            int replacement = victim + 100;
            Assert.True(set.Remove(victim));
            set.Add(replacement); // takes the freed slot
            Assert.Equal(12, set.Count);
            Assert.Equal(12, set.EnsureCapacity(0));
        }
    }

    [Fact]
    public void FreeListReuse_ShouldTakeMultipleFreedSlots_BeforeGrowing()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 16);
        for (int i = 1; i <= 12; i++)
            set.Add(i);

        Assert.True(set.Remove(3));
        Assert.True(set.Remove(7));
        Assert.True(set.Remove(11));

        set.Add(20);
        set.Add(21);
        set.Add(22);
        Assert.Equal(12, set.EnsureCapacity(0)); // three freed slots reused

        set.Add(23); // free list empty and table full → grows now
        Assert.Equal(24, set.EnsureCapacity(0));
        Assert.Equal(new[] { 1, 2, 4, 5, 6, 8, 9, 10, 12, 20, 21, 22, 23 }, ItemsOf(set));
    }

    [Fact]
    public void EnsureCapacity_ShouldRejectNegativeCapacity()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => set.EnsureCapacity(-1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void EnsureCapacity_ShouldGrowAndPreserveOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 4);
        for (int i = 10; i >= 1; i--)
            set.Add(i);
        set.Remove(5);
        set.Remove(10);

        int capacity = set.EnsureCapacity(500);

        Assert.True(capacity >= 500);
        Assert.Equal(new[] { 9, 8, 7, 6, 4, 3, 2, 1 }, ItemsOf(set));
        set.Add(100);
        Assert.Equal(new[] { 9, 8, 7, 6, 4, 3, 2, 1, 100 }, ItemsOf(set));
        Assert.True(set.TryGetFirst(out int first));
        Assert.Equal(9, first);
        for (int i = 1; i <= 9; i++)
            Assert.Equal(i != 5, set.Contains(i));
    }

    [Fact]
    public void EnsureCapacity_ShouldNotShrinkOrBumpVersion_WhenAlreadyLargeEnough()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 64);
        set.Add(1);
        var enumerator = set.GetEnumerator();

        Assert.Equal(48, set.EnsureCapacity(10));
        Assert.Equal(48, set.EnsureCapacity(48));

        Assert.True(enumerator.MoveNext()); // no rebuild, no version bump
    }

    [Fact]
    public void EnsureCapacity_ShouldInvalidateEnumerator_WhenItGrows()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 4);
        set.Add(1);
        var enumerator = set.GetEnumerator();

        set.EnsureCapacity(100);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void EnsureCapacity_OnEmpty_ShouldGrowAndStayUsable()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 1);

        Assert.True(set.EnsureCapacity(100) >= 100);
        Assert.Empty(set);
        Assert.False(set.TryGetFirst(out _));

        set.Add(1);
        Assert.Equal(new[] { 1 }, ItemsOf(set));
    }

    // One bucket always holds one entry, so sizing for a single entry must not apply the load
    // factor: at 1e-6 that would ask for 2^20 buckets (4 MiB) whose entry capacity is still one.
    // Entry capacity reads 1 either way, so the regression shows only in the bytes allocated.
    private const float TinyLoadFactor = 0.000001f;
    private const long SingletonAllocationCeiling = 64 * 1024;

    [Fact]
    public void SourceConstructor_WithOneElement_ShouldNotOversizeForATinyLoadFactor()
    {
        var source = new List<int> { 7 };

        long before = GC.GetAllocatedBytesForCurrentThread();
        var set = new LinkedSet<int, Int32WangNaiveHasher>(source, capacity: 16, loadFactor: TinyLoadFactor);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < SingletonAllocationCeiling, $"allocated {allocated} bytes");
        Assert.True(set.Contains(7));
        Assert.Equal(1, set.EnsureCapacity(0));
    }

    [Fact]
    public void TrimExcess_WithOneElement_ShouldNotOversizeForATinyLoadFactor()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 16, loadFactor: TinyLoadFactor);
        set.Add(7);

        long before = GC.GetAllocatedBytesForCurrentThread();
        set.TrimExcess();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < SingletonAllocationCeiling, $"allocated {allocated} bytes");
        Assert.True(set.Contains(7));
        Assert.Equal(1, set.EnsureCapacity(0));
    }

    [Fact]
    public void TrimExcess_ShouldShrinkCompactAndPreserveOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 200; i++)
            set.Add(i);
        for (int i = 1; i <= 200; i++)
        {
            if (i % 10 != 0)
                set.Remove(i);
        }
        Assert.True(set.MoveToFront(100));

        set.TrimExcess();

        // MinTableSizeFor(20, 0.75) = 32 buckets → 24 entries.
        Assert.Equal(24, set.EnsureCapacity(0));
        Assert.Equal(new[] { 100, 10, 20, 30, 40, 50, 60, 70, 80, 90, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200 }, ItemsOf(set));
        for (int i = 1; i <= 200; i++)
            Assert.Equal(i % 10 == 0, set.Contains(i));

        Assert.True(set.TryGetLast(out int last));
        Assert.Equal(200, last);
        Assert.True(set.TryRemoveLast(out _));
        set.Add(5);
        Assert.True(set.TryGetLast(out last));
        Assert.Equal(5, last);

        // The compacted table is exactly full of live entries after four more adds.
        for (int i = 1; i <= 4; i++)
            set.Add(1000 + i);
        Assert.Equal(24, set.Count);
        Assert.Equal(24, set.EnsureCapacity(0));
    }

    [Fact]
    public void TrimExcess_ShouldBeANoOp_WhenAlreadyAtTheMinimumSize()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 4);
        set.Add(1);
        set.Add(2);
        // MinTableSizeFor(2, 0.75) = 4 buckets, the current size.

        var enumerator = set.GetEnumerator();
        set.TrimExcess();

        Assert.True(enumerator.MoveNext()); // no rebuild, no version bump
        Assert.Equal(3, set.EnsureCapacity(0));
    }

    [Fact]
    public void TrimExcess_ShouldInvalidateEnumerator_WhenItRebuilds()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 256);
        set.Add(1);
        var enumerator = set.GetEnumerator();

        set.TrimExcess();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void TrimExcess_OnEmptySetWithABigTable_ShouldRebuildToOneBucket()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 1024);
        for (int i = 0; i < 100; i++)
            set.Add(i);
        for (int i = 0; i < 100; i++)
            set.Remove(i);

        set.TrimExcess(); // Rebuild with zero live entries

        Assert.Empty(set);
        Assert.Equal(1, set.EnsureCapacity(0));
        Assert.False(set.TryGetFirst(out _));
        Assert.False(set.TryGetLast(out _));
        Assert.Empty(ItemsOf(set));

        set.Add(7);
        set.Add(0);
        Assert.Equal(new[] { 7, 0 }, ItemsOf(set));
    }

    [Fact]
    public void TrimExcessWithCapacity_ShouldSizeForTheRequestedCount_AndPreserveOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 4);
        set.Add(3);
        set.Add(1);
        set.Add(2);

        set.TrimExcess(100); // grows: MinTableSizeFor(100, 0.75) = 256 buckets → 192

        Assert.Equal(192, set.EnsureCapacity(0));
        Assert.Equal(new[] { 3, 1, 2 }, ItemsOf(set));

        set.TrimExcess(3); // shrinks back: MinTableSizeFor(3, 0.75) = 4 → 3

        Assert.Equal(3, set.EnsureCapacity(0));
        Assert.Equal(new[] { 3, 1, 2 }, ItemsOf(set));
    }

    [Fact]
    public void TrimExcessWithCapacity_ShouldRejectACapacityBelowCount()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => set.TrimExcess(1));
        Assert.Equal("capacity", ex.ParamName);
        Assert.Equal(new[] { 1, 2 }, ItemsOf(set));
    }

    // ---------------------------------------------------------------
    //  Set algebra: the order the operations leave behind
    // ---------------------------------------------------------------

    [Fact]
    public void UnionWith_ShouldAppendNewElements_InOthersOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 3, 1, 2 });

        set.UnionWith(new[] { 9, 1, 0, 9, 7, 3 });

        Assert.Equal(new[] { 3, 1, 2, 9, 0, 7 }, ItemsOf(set));
    }

    [Fact]
    public void UnionWith_Self_ShouldBeANoOp()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 3, 1, 2 });

        set.UnionWith(set);

        Assert.Equal(new[] { 3, 1, 2 }, ItemsOf(set));
    }

    [Fact]
    public void IntersectWith_ShouldKeepTheSurvivorsInOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 5, 0, 4, 1, 3, 2 });

        set.IntersectWith(new[] { 2, 4, 9, 5, 0 });

        Assert.Equal(new[] { 5, 0, 4, 2 }, ItemsOf(set));
    }

    [Fact]
    public void IntersectWith_EmptyCollection_ShouldClear()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 5, 0, 4 });

        set.IntersectWith(Array.Empty<int>());

        Assert.Empty(set);
    }

    [Fact]
    public void ExceptWith_ShouldKeepTheSurvivorsInOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 5, 0, 4, 1, 3, 2 });

        set.ExceptWith(new[] { 4, 9, 0, 0 });

        Assert.Equal(new[] { 5, 1, 3, 2 }, ItemsOf(set));
    }

    [Fact]
    public void ExceptWith_Self_ShouldClear()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 5, 0, 4 });

        set.ExceptWith(set);

        Assert.Empty(set);
    }

    [Fact]
    public void SymmetricExceptWith_ShouldKeepSurvivors_ThenAppendOtherOnlyElements()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 5, 0, 4, 1 });

        set.SymmetricExceptWith(new[] { 8, 4, 6, 8, 0, 7 });

        Assert.Equal(new[] { 5, 1, 8, 6, 7 }, ItemsOf(set));
    }

    [Fact]
    public void SymmetricExceptWith_Self_ShouldClear()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 5, 0, 4 });

        set.SymmetricExceptWith(set);

        Assert.Empty(set);
    }

    [Fact]
    public void SetAlgebra_ShouldRejectNullOther()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 1 });

        Assert.Throws<ArgumentNullException>(() => set.UnionWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.IntersectWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.ExceptWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.SymmetricExceptWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsProperSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsProperSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.Overlaps(null!));
        Assert.Throws<ArgumentNullException>(() => set.SetEquals(null!));
    }

    [Fact]
    public void SetQueries_ShouldMatchHashSet()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 3, 0, 1 });
        var bcl = new HashSet<int>(new[] { 3, 0, 1 });
        int[][] others =
        {
            Array.Empty<int>(),
            new[] { 1, 0, 3 },
            new[] { 1, 0, 3, 3, 9 },
            new[] { 0, 1 },
            new[] { 7, 8 },
            new[] { 3, 7 },
        };

        foreach (int[] other in others)
        {
            Assert.Equal(bcl.IsSubsetOf(other), set.IsSubsetOf(other));
            Assert.Equal(bcl.IsProperSubsetOf(other), set.IsProperSubsetOf(other));
            Assert.Equal(bcl.IsSupersetOf(other), set.IsSupersetOf(other));
            Assert.Equal(bcl.IsProperSupersetOf(other), set.IsProperSupersetOf(other));
            Assert.Equal(bcl.Overlaps(other), set.Overlaps(other));
            Assert.Equal(bcl.SetEquals(other), set.SetEquals(other));
        }

        // SetEquals ignores order.
        Assert.True(set.SetEquals(new[] { 1, 3, 0 }));
    }

    // ---------------------------------------------------------------
    //  CopyTo
    // ---------------------------------------------------------------

    [Fact]
    public void CopyTo_ShouldCopyInOrder_AtTheOffset()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(30);
        set.Add(10);
        set.Add(0);
        set.Add(20);
        set.Remove(10);
        set.MoveToFront(20);

        var items = new int[5];
        set.CopyTo(items, 2);

        Assert.Equal(new[] { 0, 0, 20, 30, 0 }, items);
    }

    [Fact]
    public void CopyTo_ShouldDoNothing_WhenEmpty()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();

        set.CopyTo(new int[0], 0);
    }

    [Fact]
    public void CopyTo_ShouldValidateArguments()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 1, 2, 3 });

        Assert.Throws<ArgumentNullException>(() => set.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new int[3], -1));
        Assert.Throws<ArgumentException>(() => set.CopyTo(new int[3], 1));
    }

    // ---------------------------------------------------------------
    //  Explicit interface members
    // ---------------------------------------------------------------

    [Fact]
    public void ICollectionAdd_ShouldNotThrow_OnADuplicate()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        ICollection<int> collection = set;

        Assert.False(collection.IsReadOnly);

        collection.Add(2);
        collection.Add(1);
        collection.Add(2); // duplicate: silently ignored, keeps its position

        Assert.Equal(new[] { 2, 1 }, ItemsOf(set));
        Assert.True(collection.Contains(1));
        Assert.True(collection.Remove(2));
        Assert.Equal(1, collection.Count);
    }

    [Fact]
    public void ISetAdd_ShouldReportWhetherTheElementWasAdded()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        ISet<int> iset = set;

        Assert.True(iset.Add(4));
        Assert.True(iset.Add(0));
        Assert.False(iset.Add(4));

        Assert.Equal(new[] { 4, 0 }, ItemsOf(set));
    }

    [Fact]
    public void IReadOnlySet_ShouldForwardToThePrimarySurface()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 5, 0 });
        IReadOnlySet<int> ro = set;

        Assert.Equal(2, ro.Count);
        Assert.True(ro.Contains(0));
        Assert.False(ro.Contains(1));
        Assert.Equal(new[] { 5, 0 }, ro.ToArray());
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldBoxedElementsInOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        set.Add(2);
        set.Add(1);

        IEnumerator e = ((IEnumerable)set).GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal(1, e.Current);
        Assert.False(e.MoveNext());
        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current);
    }

    // A test-only hasher used to exercise the Guid path. Uses the GUID's hash
    // code; not part of the public library.
    private struct GuidIdentityHasher : IHashProvider<Guid>
    {
        public int Hash(Guid key) => key.GetHashCode();
    }
}
