using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="Rope"/>: the constructors and their validation, the indexer's two
/// halves, every mutating operation — <see cref="Rope.Insert(int, ReadOnlySpan{char})"/>,
/// <see cref="Rope.Remove"/>, <see cref="Rope.Split"/>, <see cref="Rope.AppendAndClear"/>,
/// <see cref="Rope.Clear"/>, <see cref="Rope.TrimExcess"/> — and the read paths
/// <see cref="Rope.IndexOf(char, int)"/>, <see cref="Rope.CopyTo(int, Span{char}, int)"/> and
/// <see cref="Rope.ToString(int, int)"/>.
///
/// <para>
/// The cases that decide whether the tree is right are the ones where an edit meets a <i>boundary</i>, because
/// that is where the AVL machinery runs rather than a single leaf memmove: an insert that exactly fills its
/// leaf against one that overflows it by a character; an insert at a leaf's first and last positions, which
/// are the two ends the overflow path reuses the leaf for; a removal that empties a leaf, that empties one
/// child of a node, and that straddles two children; and a split at a leaf's first, last and interior
/// positions. Almost every test here therefore runs on a rope built with the minimum chunk size, where a
/// dozen characters is already a multi-level tree.
/// </para>
///
/// <para>
/// The randomized reconciliation against <see cref="System.Text.StringBuilder"/> lives in
/// <see cref="RopeDifferentialTests"/>, the enumerators in <see cref="RopeEnumerationTests"/>, and the
/// documentation's worked example in <see cref="RopeDocumentationExampleTests"/>.
/// </para>
/// </summary>
public class RopeTests
{
    private const int Tiny = Rope.MinChunkSize;

    private static string Repeat(char value, int count) => new(value, count);

    private static Rope Deep(string text) => new(text, Tiny);

    // A rope of exactly one leaf filled to its capacity. The constructor deliberately leaves a quarter of each
    // leaf free, so the leaf is topped up in place before a test that needs the next insert to overflow it.
    private static Rope OneFullLeaf()
    {
        var rope = new Rope(Repeat('x', Tiny - (Tiny / 4)), Tiny);
        rope.Append(Repeat('x', Tiny / 4));
        return rope;
    }

    // The leaf count a compact rope of this length has: a rebuild fills each leaf to three quarters of the
    // chunk size, leaving the last quarter as room for the next edit to land in without splitting.
    private static int IdealLeaves(Rope rope)
    {
        int fill = rope.ChunkSize - (rope.ChunkSize / 4);
        return (rope.Length + fill - 1) / fill;
    }

    [Fact]
    public void Constructor_ShouldProduceAnEmptyRope_WhenGivenNoText()
    {
        var rope = new Rope();

        Assert.Equal(0, rope.Length);
        Assert.Equal(0, rope.LeafCount);
        Assert.Equal(0, rope.Depth);
        Assert.Equal(Rope.DefaultChunkSize, rope.ChunkSize);
        Assert.Equal(string.Empty, rope.ToString());
    }

    [Fact]
    public void Constructor_ShouldHonourTheChunkSize_WhenOneIsGiven()
    {
        var rope = new Rope(Tiny);

        Assert.Equal(Tiny, rope.ChunkSize);
        Assert.Equal(0, rope.Length);
    }

    [Fact]
    public void Constructor_ShouldHoldTheText_WhenGivenAString()
    {
        var rope = new Rope("hello");

        Assert.Equal(5, rope.Length);
        Assert.Equal(1, rope.LeafCount);
        Assert.Equal(1, rope.Depth);
        Assert.Equal("hello", rope.ToString());
    }

    [Fact]
    public void Constructor_ShouldHoldTheText_WhenGivenASpan()
    {
        var rope = new Rope("hello".AsSpan());

        Assert.Equal("hello", rope.ToString());
    }

    [Fact]
    public void Constructor_ShouldBuildBalancedLeavesWithSlack_WhenTheTextSpansManyChunks()
    {
        var rope = new Rope(Repeat('x', 100), Tiny);

        Assert.Equal(100, rope.Length);
        Assert.Equal(IdealLeaves(rope), rope.LeafCount);
        Assert.Equal(Repeat('x', 100), rope.ToString());
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheTextIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new Rope((string)null!));
        Assert.Throws<ArgumentNullException>(() => new Rope((string)null!, Tiny));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheChunkSizeIsBelowTheFloor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rope(Rope.MinChunkSize - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rope("hi", Rope.MinChunkSize - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rope("hi".AsSpan(), 0));
    }

    [Fact]
    public void Indexer_ShouldReturnEveryCharacter_WhenTheRopeSpansManyLeaves()
    {
        string text = "abcdefghijklmnopqrstuvwxyz0123456789";
        Rope rope = Deep(text);

        for (int i = 0; i < text.Length; i++)
            Assert.Equal(text[i], rope[i]);
    }

    [Fact]
    public void Indexer_ShouldReplaceTheCharacterInPlace_WhenAssigned()
    {
        Rope rope = Deep("abcdefghijklmnop");

        rope[0] = 'A';
        rope[9] = 'J';
        rope[15] = 'P';

        Assert.Equal("AbcdefghiJklmnoP", rope.ToString());
        Assert.Equal('J', rope[9]);
    }

    [Fact]
    public void Indexer_ShouldThrow_WhenTheIndexIsOutOfRange()
    {
        Rope rope = Deep("abc");

        Assert.Throws<ArgumentOutOfRangeException>(() => rope[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => rope[3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => rope[-1] = 'x');
        Assert.Throws<ArgumentOutOfRangeException>(() => rope[3] = 'x');
    }

    [Fact]
    public void Append_ShouldGrowTheRope_WhenGivenACharacterAStringAndASpan()
    {
        var rope = new Rope(Tiny);

        rope.Append('a');
        rope.Append("bc");
        rope.Append("de".AsSpan());

        Assert.Equal("abcde", rope.ToString());
    }

    [Fact]
    public void Append_ShouldThrow_WhenTheStringIsNull()
    {
        var rope = new Rope();

        Assert.Throws<ArgumentNullException>(() => rope.Append((string)null!));
    }

    [Fact]
    public void Append_ShouldPackIntoTheTrailingLeaf_WhenAppendingOneCharacterAtATime()
    {
        var rope = new Rope(Tiny);
        for (int i = 0; i < 64; i++)
            rope.Append((char)('a' + (i % 26)));

        Assert.Equal(64, rope.Length);

        // Appending never leaves more leaves behind than a compact rope of the same length would have, which
        // is what the append path is written to achieve: the leaf that overflows is reused as the head of the
        // replacement rather than copied, and the next character lands in the fresh tail leaf.
        Assert.True(
            rope.LeafCount <= IdealLeaves(rope) + 1,
            $"LeafCount {rope.LeafCount} is more than one above the compact {IdealLeaves(rope)}.");
    }

    [Fact]
    public void Insert_ShouldPlaceTheTextAtEveryPosition_WhenTheLeafHasRoom()
    {
        var rope = new Rope("ace", Tiny);

        rope.Insert(1, "b");
        rope.Insert(3, "d");
        rope.Insert(0, "@");
        rope.Insert(rope.Length, "!");

        Assert.Equal("@abcde!", rope.ToString());
        Assert.Equal(1, rope.LeafCount);
    }

    [Fact]
    public void Insert_ShouldSplitTheLeaf_WhenTheInsertionOverflowsIt()
    {
        Rope rope = OneFullLeaf();
        Assert.Equal(1, rope.LeafCount);

        rope.Insert(4, "YY");

        Assert.Equal(Repeat('x', 4) + "YY" + Repeat('x', 4), rope.ToString());
        Assert.True(rope.LeafCount > 1);
    }

    [Fact]
    public void Insert_ShouldReuseTheLeaf_WhenTheInsertionIsAtEitherEndOfAFullOne()
    {
        Rope atEnd = OneFullLeaf();
        atEnd.Insert(Tiny, "Z");
        Assert.Equal(Repeat('x', Tiny) + "Z", atEnd.ToString());

        Rope atStart = OneFullLeaf();
        atStart.Insert(0, "Z");
        Assert.Equal("Z" + Repeat('x', Tiny), atStart.ToString());
    }

    [Fact]
    public void Insert_ShouldSpanManyLeaves_WhenTheInsertedTextIsLongerThanAChunk()
    {
        Rope rope = Deep("abcdef");

        rope.Insert(3, Repeat('Z', 100));

        Assert.Equal("abc" + Repeat('Z', 100) + "def", rope.ToString());
        Assert.Equal(106, rope.Length);
    }

    [Fact]
    public void Insert_ShouldDoNothing_WhenTheTextIsEmpty()
    {
        Rope rope = Deep("abc");
        int leaves = rope.LeafCount;

        rope.Insert(1, string.Empty);
        rope.Insert(1, ReadOnlySpan<char>.Empty);

        Assert.Equal("abc", rope.ToString());
        Assert.Equal(leaves, rope.LeafCount);
    }

    [Fact]
    public void Insert_ShouldThrow_WhenTheArgumentsAreInvalid()
    {
        Rope rope = Deep("abc");

        Assert.Throws<ArgumentNullException>(() => rope.Insert(0, (string)null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.Insert(-1, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.Insert(4, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.Insert(-1, 'x'));
    }

    [Fact]
    public void Insert_ShouldGrowFromEmpty_WhenTheRopeHasNoRootYet()
    {
        var rope = new Rope(Tiny);

        rope.Insert(0, Repeat('q', 40));

        Assert.Equal(40, rope.Length);
        Assert.Equal(Repeat('q', 40), rope.ToString());
    }

    [Fact]
    public void Remove_ShouldDeleteTheRange_WhenItFallsInsideOneLeaf()
    {
        Rope rope = Deep("abcdef");

        rope.Remove(2, 2);

        Assert.Equal("abef", rope.ToString());
    }

    [Fact]
    public void Remove_ShouldDeleteTheRange_WhenItStraddlesTwoChildren()
    {
        string text = "abcdefghijklmnopqrstuvwxyz";
        Rope rope = Deep(text);

        rope.Remove(5, 15);

        Assert.Equal(text[..5] + text[20..], rope.ToString());
    }

    [Fact]
    public void Remove_ShouldDropWholeLeaves_WhenTheRangeCoversThem()
    {
        Rope rope = Deep(Repeat('x', 80));

        rope.Remove(8, 64);

        Assert.Equal(16, rope.Length);
        Assert.Equal(Repeat('x', 16), rope.ToString());
    }

    [Fact]
    public void Remove_ShouldEmptyTheRope_WhenTheRangeIsEverything()
    {
        Rope rope = Deep("abcdefghijklmnop");

        rope.Remove(0, rope.Length);

        Assert.Equal(0, rope.Length);
        Assert.Equal(0, rope.LeafCount);
        Assert.Equal(0, rope.Depth);
        Assert.Equal(string.Empty, rope.ToString());
    }

    [Fact]
    public void Remove_ShouldTrimEachEnd_WhenTheRangeIsAPrefixOrASuffix()
    {
        Rope prefix = Deep("abcdefghij");
        prefix.Remove(0, 3);
        Assert.Equal("defghij", prefix.ToString());

        Rope suffix = Deep("abcdefghij");
        suffix.Remove(7, 3);
        Assert.Equal("abcdefg", suffix.ToString());
    }

    [Fact]
    public void Remove_ShouldDoNothing_WhenTheCountIsZero()
    {
        Rope rope = Deep("abc");

        rope.Remove(1, 0);
        rope.Remove(3, 0);

        Assert.Equal("abc", rope.ToString());
    }

    [Fact]
    public void Remove_ShouldThrow_WhenTheRangeIsInvalid()
    {
        Rope rope = Deep("abcdef");

        Assert.Throws<ArgumentOutOfRangeException>(() => rope.Remove(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.Remove(7, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.Remove(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.Remove(4, 3));
    }

    [Fact]
    public void Clear_ShouldEmptyTheRope_WhenItHasText()
    {
        Rope rope = Deep("abcdefghij");

        rope.Clear();

        Assert.Equal(0, rope.Length);
        Assert.Equal(string.Empty, rope.ToString());

        rope.Append("again");
        Assert.Equal("again", rope.ToString());
    }

    [Fact]
    public void Clear_ShouldDoNothing_WhenTheRopeIsAlreadyEmpty()
    {
        var rope = new Rope();

        rope.Clear();

        Assert.Equal(0, rope.Length);
    }

    [Fact]
    public void TrimExcess_ShouldCompactTheLeaves_WhenEditingHasFragmentedThem()
    {
        var rope = new Rope(Repeat('x', 64), Tiny);
        for (int i = 0; i < 16; i++)
            rope.Insert(i * 3, "!");

        rope.TrimExcess();

        Assert.Equal(IdealLeaves(rope), rope.LeafCount);
    }

    [Fact]
    public void TrimExcess_ShouldDoNothing_WhenTheRopeIsEmpty()
    {
        var rope = new Rope();

        rope.TrimExcess();

        Assert.Equal(0, rope.Length);
        Assert.Equal(0, rope.LeafCount);
    }

    [Fact]
    public void Rope_ShouldRebuildItself_WhenEditingFragmentsItPastTheThreshold()
    {
        var rope = new Rope(Tiny);

        // Single-character inserts at the front are the worst case for fill: each one lands at the head of the
        // leading leaf and splits it once the leaf fills.
        for (int i = 0; i < 400; i++)
            rope.Insert(0, "z");

        int ideal = IdealLeaves(rope);
        Assert.Equal(400, rope.Length);
        Assert.True(
            rope.LeafCount <= (2 * ideal) + 8,
            $"LeafCount {rope.LeafCount} exceeds the rebuild threshold for {ideal} ideal leaves.");
        Assert.Equal(Repeat('z', 400), rope.ToString());
    }

    [Fact]
    public void Split_ShouldCutTheRopeInTwo_WhenTheIndexIsInTheMiddle()
    {
        string text = "abcdefghijklmnopqrstuvwxyz";
        Rope rope = Deep(text);

        Rope tail = rope.Split(10);

        Assert.Equal(text[..10], rope.ToString());
        Assert.Equal(text[10..], tail.ToString());
        Assert.Equal(Tiny, tail.ChunkSize);
    }

    [Fact]
    public void Split_ShouldMoveEverything_WhenTheIndexIsZero()
    {
        Rope rope = Deep("abcdefghij");

        Rope tail = rope.Split(0);

        Assert.Equal(0, rope.Length);
        Assert.Equal("abcdefghij", tail.ToString());
    }

    [Fact]
    public void Split_ShouldReturnAnEmptyRope_WhenTheIndexIsTheLength()
    {
        Rope rope = Deep("abcdefghij");

        Rope tail = rope.Split(rope.Length);

        Assert.Equal(0, tail.Length);
        Assert.Equal("abcdefghij", rope.ToString());
    }

    [Fact]
    public void Split_ShouldCutInsideAForeignLeaf_WhenTheLeafIsWiderThanThisRopesChunkSize()
    {
        // A leaf moved in from a rope with a larger chunk size is wider than this rope's own chunk size, so
        // cutting inside it has to allocate a replacement leaf wider than the chunk size too.
        var wide = new Rope(Repeat('w', 64), 64);
        var narrow = new Rope("abcd", Tiny);
        narrow.AppendAndClear(wide);

        Rope tail = narrow.Split(20);

        Assert.Equal("abcd" + Repeat('w', 16), narrow.ToString());
        Assert.Equal(Repeat('w', 48), tail.ToString());
    }

    [Fact]
    public void Split_ShouldThrow_WhenTheIndexIsOutOfRange()
    {
        Rope rope = Deep("abc");

        Assert.Throws<ArgumentOutOfRangeException>(() => rope.Split(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.Split(4));
    }

    [Fact]
    public void AppendAndClear_ShouldMoveTheTextAndEmptyTheSource_WhenBothRopesHaveText()
    {
        Rope left = Deep("abcdefghij");
        Rope right = Deep("KLMNOPQRST");

        left.AppendAndClear(right);

        Assert.Equal("abcdefghijKLMNOPQRST", left.ToString());
        Assert.Equal(0, right.Length);
        Assert.Equal(0, right.LeafCount);
    }

    [Fact]
    public void AppendAndClear_ShouldJoinTreesOfEitherHeight_WhenTheSidesAreVeryDifferentSizes()
    {
        Rope big = Deep(Repeat('b', 200));
        Rope small = Deep("s");

        big.AppendAndClear(small);
        Assert.Equal(Repeat('b', 200) + "s", big.ToString());

        Rope other = Deep("t");
        other.AppendAndClear(big);
        Assert.Equal("t" + Repeat('b', 200) + "s", other.ToString());
    }

    [Fact]
    public void AppendAndClear_ShouldDoNothing_WhenTheSourceIsEmpty()
    {
        Rope rope = Deep("abc");
        var empty = new Rope();

        rope.AppendAndClear(empty);

        Assert.Equal("abc", rope.ToString());
    }

    [Fact]
    public void AppendAndClear_ShouldThrow_WhenTheSourceIsNullOrThisRope()
    {
        Rope rope = Deep("abc");

        Assert.Throws<ArgumentNullException>(() => rope.AppendAndClear(null!));
        Assert.Throws<ArgumentException>(() => rope.AppendAndClear(rope));
    }

    [Fact]
    public void SplitAndAppendAndClear_ShouldRoundTrip_WhenAppliedInSequence()
    {
        string text = "the quick brown fox jumps over the lazy dog";
        Rope rope = Deep(text);

        Rope tail = rope.Split(19);
        rope.AppendAndClear(tail);

        Assert.Equal(text, rope.ToString());
        Assert.Equal(0, tail.Length);
    }

    [Fact]
    public void IndexOf_ShouldFindTheFirstOccurrence_WhenTheCharacterIsPresent()
    {
        Rope rope = Deep("abcdefghijabcdefghij");

        Assert.Equal(0, rope.IndexOf('a'));
        Assert.Equal(9, rope.IndexOf('j'));
        Assert.Equal(10, rope.IndexOf('a', 1));
        Assert.Equal(19, rope.IndexOf('j', 10));
    }

    [Fact]
    public void IndexOf_ShouldReturnMinusOne_WhenTheCharacterIsAbsentOrPastTheStart()
    {
        Rope rope = Deep("abcdefghij");

        Assert.Equal(-1, rope.IndexOf('z'));
        Assert.Equal(-1, rope.IndexOf('a', 1));
        Assert.Equal(-1, rope.IndexOf('a', rope.Length));
        Assert.Equal(-1, new Rope().IndexOf('a'));
    }

    [Fact]
    public void IndexOf_ShouldThrow_WhenTheStartIndexIsOutOfRange()
    {
        Rope rope = Deep("abc");

        Assert.Throws<ArgumentOutOfRangeException>(() => rope.IndexOf('a', -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.IndexOf('a', 4));
    }

    [Fact]
    public void CopyTo_ShouldWriteTheWholeRope_WhenGivenAFittingDestination()
    {
        string text = "abcdefghijklmnopqrstuvwxyz";
        Rope rope = Deep(text);

        var destination = new char[text.Length];
        rope.CopyTo(destination);

        Assert.Equal(text, new string(destination));
    }

    [Fact]
    public void CopyTo_ShouldWriteTheRange_WhenGivenAnIndexAndCount()
    {
        string text = "abcdefghijklmnopqrstuvwxyz";
        Rope rope = Deep(text);

        var destination = new char[10];
        rope.CopyTo(8, destination, 10);

        Assert.Equal(text.Substring(8, 10), new string(destination));
    }

    [Fact]
    public void CopyTo_ShouldWriteNothing_WhenTheCountIsZero()
    {
        Rope rope = Deep("abc");

        rope.CopyTo(1, Span<char>.Empty, 0);

        Assert.Equal("abc", rope.ToString());
    }

    [Fact]
    public void CopyTo_ShouldThrow_WhenTheRangeOrDestinationIsInvalid()
    {
        Rope rope = Deep("abcdef");

        Assert.Throws<ArgumentOutOfRangeException>(() => rope.CopyTo(-1, new char[6], 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.CopyTo(7, new char[6], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.CopyTo(0, new char[6], -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.CopyTo(4, new char[6], 3));
        Assert.Throws<ArgumentException>(() => rope.CopyTo(0, new char[2], 6));
        Assert.Throws<ArgumentException>(() => rope.CopyTo(new char[2]));
    }

    [Fact]
    public void ToString_ShouldReturnTheRange_WhenGivenAnIndexAndCount()
    {
        string text = "abcdefghijklmnopqrstuvwxyz";
        Rope rope = Deep(text);

        Assert.Equal(text, rope.ToString());
        Assert.Equal(text.Substring(3, 20), rope.ToString(3, 20));
        Assert.Equal(string.Empty, rope.ToString(3, 0));
        Assert.Equal(string.Empty, new Rope().ToString());
    }

    [Fact]
    public void ToString_ShouldThrow_WhenTheRangeIsInvalid()
    {
        Rope rope = Deep("abcdef");

        Assert.Throws<ArgumentOutOfRangeException>(() => rope.ToString(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.ToString(7, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.ToString(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rope.ToString(4, 3));
    }

    [Fact]
    public void ReadOnlyList_ShouldExposeCountAndTheIndexer_WhenViewedThroughTheInterface()
    {
        IReadOnlyList<char> rope = Deep("abcdef");

        Assert.Equal(6, rope.Count);
        Assert.Equal('a', rope[0]);
        Assert.Equal('f', rope[5]);
    }
}
