using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The public surface of <see cref="PersistentVector{T}"/>: construction, the read paths, the three
/// persistent operations and every documented guard.
///
/// <para>
/// The randomized reconciliation against a <see cref="List{T}"/> oracle — and with it the trie's shape
/// changes at 32, 1024 and 32,768 — lives in <see cref="PersistentVectorDifferentialTests"/>. What is pinned
/// here is what a caller can read from the documentation: that the operations return a <i>new</i> vector and
/// leave the receiver alone, that the tail fast paths and the trie paths agree, and that the exceptions are
/// the ones the XML docs promise.
/// </para>
/// </summary>
public class PersistentVectorTests
{
    // Past 1,024, so a vector built from it has a two-level trie and indices that land in the trie, in the
    // last trie leaf, and in the tail are all distinct cases.
    private const int TwoLevelLength = 1_100;

    [Fact]
    public void Empty_ShouldHaveNoElements()
    {
        PersistentVector<int> vector = PersistentVector<int>.Empty;

        Assert.Equal(0, vector.Count);
        Assert.True(vector.IsEmpty);
        Assert.Empty(vector);
        Assert.Same(Array.Empty<int>(), vector.ToArray());
    }

    [Fact]
    public void Constructor_ShouldCopyTheSourceInOrder()
    {
        var source = new[] { 5, 3, 9, 1 };

        PersistentVector<int> vector = new(source);

        Assert.Equal(4, vector.Count);
        Assert.False(vector.IsEmpty);
        Assert.Equal(source, vector.ToArray());
    }

    [Fact]
    public void Constructor_ShouldProduceAnEmptyVector_WhenTheSourceIsEmpty()
    {
        PersistentVector<string> vector = new(Array.Empty<string>());

        Assert.Equal(0, vector.Count);
        Assert.True(vector.IsEmpty);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenTheSourceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PersistentVector<int>(null!));
    }

    [Fact]
    public void Constructor_ShouldAgreeWithRepeatedAdd_AcrossTheTrieLevels()
    {
        int[] source = Enumerable.Range(0, TwoLevelLength).ToArray();

        PersistentVector<int> built = new(source);

        PersistentVector<int> appended = PersistentVector<int>.Empty;
        foreach (int value in source)
            appended = appended.Add(value);

        Assert.Equal(built.ToArray(), appended.ToArray());
    }

    [Fact]
    public void Add_ShouldLeaveTheReceiverUnchanged()
    {
        PersistentVector<int> original = new(new[] { 1, 2, 3 });

        PersistentVector<int> extended = original.Add(4);

        Assert.Equal(new[] { 1, 2, 3 }, original.ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4 }, extended.ToArray());
    }

    [Fact]
    public void Indexer_ShouldReadFromBothTheTrieAndTheTail()
    {
        PersistentVector<int> vector = new(Enumerable.Range(0, TwoLevelLength));

        // One index from the first leaf, one from deep inside the trie, one from the last full trie leaf, and
        // one from the tail — the four positions the descent treats differently.
        Assert.Equal(0, vector[0]);
        Assert.Equal(500, vector[500]);
        Assert.Equal(1_055, vector[1_055]);
        Assert.Equal(TwoLevelLength - 1, vector[TwoLevelLength - 1]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenTheIndexIsOutsideTheVector(int index)
    {
        PersistentVector<int> vector = new(new[] { 1, 2, 3 });

        Assert.Throws<ArgumentOutOfRangeException>(() => vector[index]);
    }

    [Fact]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenTheVectorIsEmpty()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PersistentVector<int>.Empty[0]);
    }

    [Fact]
    public void AddRange_ShouldAppendInOrder()
    {
        PersistentVector<int> vector = new(new[] { 1, 2 });

        PersistentVector<int> extended = vector.AddRange(new[] { 3, 4, 5 });

        Assert.Equal(new[] { 1, 2 }, vector.ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, extended.ToArray());
    }

    [Fact]
    public void AddRange_ShouldReturnTheSameInstance_WhenTheSourceIsEmpty()
    {
        PersistentVector<int> vector = new(new[] { 1, 2 });

        Assert.Same(vector, vector.AddRange(Array.Empty<int>()));
    }

    [Fact]
    public void AddRange_ShouldCrossTheTailBoundary()
    {
        PersistentVector<int> vector = PersistentVector<int>.Empty.AddRange(Enumerable.Range(0, TwoLevelLength));

        Assert.Equal(TwoLevelLength, vector.Count);
        Assert.Equal(Enumerable.Range(0, TwoLevelLength).ToArray(), vector.ToArray());
    }

    [Fact]
    public void AddRange_ShouldThrowArgumentNullException_WhenTheSourceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => PersistentVector<int>.Empty.AddRange(null!));
    }

    [Fact]
    public void SetItem_ShouldReplaceInTheTail_WithoutTouchingTheTrie()
    {
        PersistentVector<int> vector = new(Enumerable.Range(0, TwoLevelLength));

        PersistentVector<int> updated = vector.SetItem(TwoLevelLength - 1, -1);

        Assert.Equal(-1, updated[TwoLevelLength - 1]);
        Assert.Equal(TwoLevelLength - 1, vector[TwoLevelLength - 1]);
        Assert.Equal(0, updated[0]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void SetItem_ShouldThrowArgumentOutOfRangeException_WhenTheIndexIsOutsideTheVector(int index)
    {
        PersistentVector<int> vector = new(new[] { 1, 2, 3 });

        Assert.Throws<ArgumentOutOfRangeException>(() => vector.SetItem(index, 0));
    }

    [Fact]
    public void RemoveLast_ShouldLeaveTheReceiverUnchanged()
    {
        PersistentVector<int> original = new(new[] { 1, 2, 3 });

        PersistentVector<int> shorter = original.RemoveLast();

        Assert.Equal(new[] { 1, 2, 3 }, original.ToArray());
        Assert.Equal(new[] { 1, 2 }, shorter.ToArray());
    }

    [Fact]
    public void RemoveLast_ShouldReturnTheEmptyInstance_WhenTheVectorHoldsOneElement()
    {
        PersistentVector<int> vector = PersistentVector<int>.Empty.Add(7);

        Assert.Same(PersistentVector<int>.Empty, vector.RemoveLast());
    }

    [Fact]
    public void RemoveLast_ShouldThrowInvalidOperationException_WhenTheVectorIsEmpty()
    {
        Assert.Throws<InvalidOperationException>(() => PersistentVector<int>.Empty.RemoveLast());
    }

    [Fact]
    public void CopyTo_ShouldWriteEveryElementAtTheGivenOffset()
    {
        PersistentVector<int> vector = new(Enumerable.Range(0, TwoLevelLength));
        var destination = new int[TwoLevelLength + 3];

        vector.CopyTo(destination, 2);

        Assert.Equal(0, destination[0]);
        Assert.Equal(0, destination[1]);
        Assert.Equal(Enumerable.Range(0, TwoLevelLength).ToArray(), destination.Skip(2).Take(TwoLevelLength));
        Assert.Equal(0, destination[TwoLevelLength + 2]);
    }

    [Fact]
    public void CopyTo_ShouldWriteNothing_WhenTheVectorIsEmpty()
    {
        var destination = new[] { 9, 9 };

        PersistentVector<int>.Empty.CopyTo(destination, 0);

        Assert.Equal(new[] { 9, 9 }, destination);
    }

    [Fact]
    public void CopyTo_ShouldThrowArgumentNullException_WhenTheDestinationIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => PersistentVector<int>.Empty.CopyTo(null!, 0));
    }

    [Fact]
    public void CopyTo_ShouldThrowArgumentOutOfRangeException_WhenTheOffsetIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PersistentVector<int>.Empty.CopyTo(new int[1], -1));
    }

    [Fact]
    public void CopyTo_ShouldThrowArgumentException_WhenTheDestinationIsTooSmall()
    {
        PersistentVector<int> vector = new(new[] { 1, 2, 3 });

        Assert.Throws<ArgumentException>(() => vector.CopyTo(new int[3], 1));
    }

    [Fact]
    public void ToArray_ShouldMaterializeTheWholeVector()
    {
        PersistentVector<int> vector = new(Enumerable.Range(0, TwoLevelLength));

        Assert.Equal(Enumerable.Range(0, TwoLevelLength).ToArray(), vector.ToArray());
    }

    [Fact]
    public void GrowthPredicate_ShouldNotBeAbleToOverflowItsShift_AtAnyReachableCount()
    {
        // The trie grows when (count >> 5) > (1 << shift). A vector of int.MaxValue elements cannot drive
        // `shift` past 30, so `1 << shift` is always a positive int and never a masked or negative one — but
        // that rests on arithmetic no test can reach by actually building the vector, and a reviewer reading
        // the line in isolation will keep flagging it. So the arithmetic itself is pinned here.
        const int BranchBits = 5;

        // k is (count >> BranchBits), which is what the predicate compares against 1 << shift.
        int maxReachableK = int.MaxValue >> BranchBits;

        int shift = BranchBits;
        while (maxReachableK > (1 << shift))
            shift += BranchBits;

        Assert.Equal(30, shift);
        Assert.True(1 << shift > 0, "1 << shift must stay a positive int at the highest reachable shift.");

        // And the shift the vector settles at addresses every index an int can hold, so it never needs a
        // level the predicate cannot ask for: 6 node levels plus the leaf is 32^7 indices.
        Assert.True(Math.Pow(32, (shift / BranchBits) + 1) > int.MaxValue);
    }

    [Fact]
    public void ReferenceElements_ShouldRoundTripIncludingNulls()
    {
        // The trie stores leaves as T[], so a null reference element is an ordinary value rather than the
        // "vacant slot" marker it is in the hash-backed collections.
        PersistentVector<string?> vector = new(new string?[] { "a", null, "c" });

        Assert.Equal(3, vector.Count);
        Assert.Null(vector[1]);
        Assert.Equal(new string?[] { "a", null, "c" }, vector.ToArray());
    }
}
