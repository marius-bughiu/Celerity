using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="SuffixArray"/>: the build, the suffix and longest-common-prefix arrays
/// it produces, and every query tier — <see cref="SuffixArray.Contains"/>,
/// <see cref="SuffixArray.CountOccurrences"/>, <see cref="SuffixArray.IndexOf"/>,
/// <see cref="SuffixArray.TryGetOccurrences"/>, <see cref="SuffixArray.CopyOccurrences"/>,
/// <see cref="SuffixArray.GetOccurrences"/> and <see cref="SuffixArray.TryGetLongestRepeatedSubstring"/>.
///
/// <para>
/// The cases that decide whether the build is right are the ones a tidy fixture does not contain: a text whose
/// characters are all distinct (the prefix-doubling loop never runs, because the first ranking pass already
/// separates every suffix), a text of one repeated character (the loop runs the maximum number of rounds and
/// every round has to wrap a shift past the sentinel), a text whose suffixes collide for a long prefix and
/// separate only at the end, and a text long enough that the doubling passes over a rank range wider than the
/// alphabet. Empty and single-character texts pin the degenerate ends.
/// </para>
///
/// <para>
/// The query cases that matter are the ones the binary search can get wrong at a boundary: a pattern that
/// sorts below every suffix, above every suffix, and between two of them; a pattern that is a prefix of a
/// suffix shorter than itself; overlapping occurrences; and the empty pattern, which matches everywhere and is
/// the one input where the lower and upper bounds are the ends of the whole array.
/// </para>
///
/// <para>
/// The randomized reconciliation against a naive sorted-substring oracle lives in
/// <see cref="SuffixArrayDifferentialTests"/>, and the <see cref="IReadOnlyList{T}"/> surface in
/// <see cref="SuffixArrayEnumerationTests"/>.
/// </para>
/// </summary>
public class SuffixArrayTests
{
    // "banana" is the textbook case for a reason: its suffixes interleave (a, ana, anana, banana, na, nana),
    // "ana" occurs twice overlapping, and no two suffixes are separated by their first character alone.
    private const string Banana = "banana";

    private static string[] SortedSuffixes(string text) =>
        [.. Enumerable.Range(0, text.Length).Select(i => text[i..]).Order(StringComparer.Ordinal)];

    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldOrderEverySuffix_WhenTheTextRepeatsAPrefix()
    {
        var index = new SuffixArray(Banana);

        Assert.Equal(6, index.Length);
        Assert.Equal([5, 3, 1, 0, 4, 2], index.Suffixes.ToArray());
    }

    [Fact]
    public void Constructor_ShouldCopyTheText_WhenTheSourceBufferIsMutatedAfterwards()
    {
        char[] source = "banana".ToCharArray();

        var index = new SuffixArray(source);
        source[0] = 'z';

        Assert.Equal("banana", index.Text.ToString());
        Assert.Equal([5, 3, 1, 0, 4, 2], index.Suffixes.ToArray());
    }

    [Fact]
    public void Constructor_ShouldProduceAnEmptyIndex_WhenTheTextIsEmpty()
    {
        var index = new SuffixArray(string.Empty);

        Assert.Equal(0, index.Length);
        Assert.True(index.Text.IsEmpty);
        Assert.True(index.Suffixes.IsEmpty);
        Assert.True(index.LongestCommonPrefixes.IsEmpty);
    }

    [Fact]
    public void Constructor_ShouldProduceAnEmptyIndex_WhenTheTextIsANullString()
    {
        // A null string converts to an empty span rather than throwing, which is the documented behaviour of
        // the span parameter and the reason the constructor raises no ArgumentNullException.
        string? absent = null;

        var index = new SuffixArray(absent);

        Assert.Equal(0, index.Length);
        Assert.True(index.Suffixes.IsEmpty);
    }

    [Fact]
    public void Constructor_ShouldHoldTheOnlySuffix_WhenTheTextIsOneCharacter()
    {
        var index = new SuffixArray("x");

        Assert.Equal(1, index.Length);
        Assert.Equal([0], index.Suffixes.ToArray());
        Assert.Equal([0], index.LongestCommonPrefixes.ToArray());
    }

    [Fact]
    public void Constructor_ShouldSkipTheDoublingRounds_WhenEveryCharacterIsDistinct()
    {
        // The first ranking pass already separates every suffix, so the doubling loop never runs at all — the
        // one path through the build that does no counting sort.
        var index = new SuffixArray("dbca");

        Assert.Equal([3, 1, 2, 0], index.Suffixes.ToArray());
        Assert.Equal([0, 0, 0, 0], index.LongestCommonPrefixes.ToArray());
    }

    [Fact]
    public void Constructor_ShouldOrderSuffixesByLength_WhenEveryCharacterIsTheSame()
    {
        // The worst case for prefix doubling: every round separates one more suffix, and every shift wraps
        // past the sentinel.
        var index = new SuffixArray("aaaaa");

        Assert.Equal([4, 3, 2, 1, 0], index.Suffixes.ToArray());
        Assert.Equal([0, 1, 2, 3, 4], index.LongestCommonPrefixes.ToArray());
    }

    [Fact]
    public void Constructor_ShouldSeparateSuffixes_WhenTheyShareALongPrefix()
    {
        // The suffixes at 0 and 4 agree for four characters and part only at the fifth, so the order is not
        // settled until the doubling has run past that prefix.
        var index = new SuffixArray("abababc");

        string[] ordered = [.. index.Suffixes.ToArray().Select(i => "abababc"[i..])];
        Assert.Equal(SortedSuffixes("abababc"), ordered);
    }

    [Fact]
    public void Constructor_ShouldOrderByCodeUnit_WhenTheTextHoldsCharactersAboveTheAsciiRange()
    {
        // Ordinal means UTF-16 code units, so 'Ł' (U+0141) sorts above 'z' (U+007A) rather than near 'L'.
        var index = new SuffixArray("zŁa");

        Assert.Equal([2, 0, 1], index.Suffixes.ToArray());
    }

    [Fact]
    public void Constructor_ShouldOrderEverySuffix_WhenTheTextIsLongerThanOneDoublingRound()
    {
        const string text = "mississippi river mississippi delta mississippi";

        var index = new SuffixArray(text);

        string[] ordered = [.. index.Suffixes.ToArray().Select(i => text[i..])];
        Assert.Equal(SortedSuffixes(text), ordered);
    }

    // ---- the longest-common-prefix array -----------------------------------------------------------

    [Fact]
    public void LongestCommonPrefixes_ShouldMeasureAgainstThePrecedingSuffix_WhenTheTextRepeatsAPrefix()
    {
        // Order: a, ana, anana, banana, na, nana.
        var index = new SuffixArray(Banana);

        Assert.Equal([0, 1, 3, 0, 0, 2], index.LongestCommonPrefixes.ToArray());
    }

    // ---- Contains ----------------------------------------------------------------------------------

    [Theory]
    [InlineData("banana", true)]
    [InlineData("ana", true)]
    [InlineData("a", true)]
    [InlineData("nan", true)]
    [InlineData("bananas", false)]
    [InlineData("nana ", false)]
    [InlineData("c", false)]
    [InlineData("Z", false)]
    public void Contains_ShouldAgreeWithTheText_WhenThePatternIsPresentOrAbsent(string pattern, bool expected)
    {
        Assert.Equal(expected, new SuffixArray(Banana).Contains(pattern));
    }

    [Fact]
    public void Contains_ShouldReturnTrue_WhenThePatternIsEmptyAndTheTextIsNot()
    {
        Assert.True(new SuffixArray(Banana).Contains(string.Empty));
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenBothThePatternAndTheTextAreEmpty()
    {
        // The documented rule with no special case: the empty pattern matches at every start position, and an
        // empty text has none.
        Assert.False(new SuffixArray(string.Empty).Contains(string.Empty));
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenThePatternSortsAboveEverySuffix()
    {
        // "z" is above every suffix, so the lower bound lands past the end of the array — the boundary the
        // bounds check in Contains exists for.
        Assert.False(new SuffixArray(Banana).Contains("z"));
    }

    // ---- CountOccurrences --------------------------------------------------------------------------

    [Theory]
    [InlineData("a", 3)]
    [InlineData("na", 2)]
    [InlineData("ana", 2)]
    [InlineData("banana", 1)]
    [InlineData("b", 1)]
    [InlineData("q", 0)]
    [InlineData("bananas", 0)]
    public void CountOccurrences_ShouldCountEveryPosition_WhenOccurrencesOverlap(string pattern, int expected)
    {
        Assert.Equal(expected, new SuffixArray(Banana).CountOccurrences(pattern));
    }

    [Fact]
    public void CountOccurrences_ShouldReturnTheTextLength_WhenThePatternIsEmpty()
    {
        Assert.Equal(6, new SuffixArray(Banana).CountOccurrences(string.Empty));
        Assert.Equal(0, new SuffixArray(string.Empty).CountOccurrences(string.Empty));
    }

    // ---- IndexOf -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("a", 1)]
    [InlineData("na", 2)]
    [InlineData("ana", 1)]
    [InlineData("banana", 0)]
    [InlineData("q", -1)]
    public void IndexOf_ShouldReturnTheLowestPosition_WhenThePatternOccursMoreThanOnce(string pattern, int expected)
    {
        Assert.Equal(expected, new SuffixArray(Banana).IndexOf(pattern));
    }

    [Fact]
    public void IndexOf_ShouldMatchStringIndexOf_WhenTheTextHasManyOccurrences()
    {
        const string text = "the cat sat on the mat";
        var index = new SuffixArray(text);

        foreach (string pattern in new[] { "the", "at", "on", " ", "mat", "cat sat" })
            Assert.Equal(text.IndexOf(pattern, StringComparison.Ordinal), index.IndexOf(pattern));
    }

    [Fact]
    public void IndexOf_ShouldReturnZero_WhenThePatternIsEmptyAndTheTextIsNot()
    {
        Assert.Equal(0, new SuffixArray(Banana).IndexOf(string.Empty));
    }

    [Fact]
    public void IndexOf_ShouldReturnMinusOne_WhenBothThePatternAndTheTextAreEmpty()
    {
        Assert.Equal(-1, new SuffixArray(string.Empty).IndexOf(string.Empty));
    }

    // ---- TryGetOccurrences -------------------------------------------------------------------------

    [Fact]
    public void TryGetOccurrences_ShouldSliceTheIndex_WhenThePatternOccurs()
    {
        var index = new SuffixArray(Banana);

        Assert.True(index.TryGetOccurrences("ana", out ReadOnlySpan<int> occurrences));

        // Lexicographic order, not positional: "ana" precedes "anana".
        Assert.Equal([3, 1], occurrences.ToArray());
    }

    [Fact]
    public void TryGetOccurrences_ShouldReturnFalseAndAnEmptySpan_WhenThePatternIsAbsent()
    {
        var index = new SuffixArray(Banana);

        Assert.False(index.TryGetOccurrences("q", out ReadOnlySpan<int> occurrences));
        Assert.True(occurrences.IsEmpty);
    }

    [Fact]
    public void TryGetOccurrences_ShouldReturnEverySuffix_WhenThePatternIsEmpty()
    {
        var index = new SuffixArray(Banana);

        Assert.True(index.TryGetOccurrences(string.Empty, out ReadOnlySpan<int> occurrences));
        Assert.Equal(index.Suffixes.ToArray(), occurrences.ToArray());
    }

    // ---- CopyOccurrences ---------------------------------------------------------------------------

    [Fact]
    public void CopyOccurrences_ShouldWriteAscendingPositions_WhenTheBufferIsLargeEnough()
    {
        var index = new SuffixArray(Banana);
        int[] destination = new int[6];

        int written = index.CopyOccurrences("a", destination);

        Assert.Equal(3, written);
        Assert.Equal([1, 3, 5], destination[..written]);
    }

    [Fact]
    public void CopyOccurrences_ShouldStartAtTheOffset_WhenADestinationIndexIsGiven()
    {
        var index = new SuffixArray(Banana);
        int[] destination = [-1, -1, -1, -1, -1];

        int written = index.CopyOccurrences("na", destination, 2);

        Assert.Equal(2, written);
        Assert.Equal([-1, -1, 2, 4, -1], destination);
    }

    [Fact]
    public void CopyOccurrences_ShouldTruncate_WhenTheBufferIsTooSmall()
    {
        var index = new SuffixArray(Banana);
        int[] destination = new int[2];

        int written = index.CopyOccurrences("a", destination);

        Assert.Equal(2, written);
        // The truncated set is whichever two the lexicographic order reached first, sorted on the way out.
        Assert.Equal(destination.Order(), destination);
    }

    [Fact]
    public void CopyOccurrences_ShouldWriteNothing_WhenTheDestinationIndexIsAtTheEnd()
    {
        var index = new SuffixArray(Banana);
        int[] destination = new int[3];

        Assert.Equal(0, index.CopyOccurrences("a", destination, 3));
        Assert.Equal([0, 0, 0], destination);
    }

    [Fact]
    public void CopyOccurrences_ShouldWriteNothing_WhenThePatternIsAbsent()
    {
        var index = new SuffixArray(Banana);
        int[] destination = new int[3];

        Assert.Equal(0, index.CopyOccurrences("q", destination));
    }

    [Fact]
    public void CopyOccurrences_ShouldThrowArgumentNullException_WhenTheDestinationIsNull()
    {
        var index = new SuffixArray(Banana);

        Assert.Throws<ArgumentNullException>(() => index.CopyOccurrences("a", null!));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void CopyOccurrences_ShouldThrowArgumentOutOfRangeException_WhenTheDestinationIndexIsOutsideTheBuffer(int destinationIndex)
    {
        var index = new SuffixArray(Banana);
        int[] destination = new int[3];

        Assert.Throws<ArgumentOutOfRangeException>(() => index.CopyOccurrences("a", destination, destinationIndex));
    }

    // ---- GetOccurrences ----------------------------------------------------------------------------

    [Fact]
    public void GetOccurrences_ShouldReturnAscendingPositions_WhenThePatternOccurs()
    {
        Assert.Equal([1, 3], new SuffixArray(Banana).GetOccurrences("ana"));
    }

    [Fact]
    public void GetOccurrences_ShouldReturnAnEmptyArray_WhenThePatternIsAbsent()
    {
        Assert.Empty(new SuffixArray(Banana).GetOccurrences("q"));
    }

    [Fact]
    public void GetOccurrences_ShouldReturnEveryPosition_WhenThePatternIsEmpty()
    {
        Assert.Equal([0, 1, 2, 3, 4, 5], new SuffixArray(Banana).GetOccurrences(string.Empty));
    }

    // ---- TryGetLongestRepeatedSubstring ------------------------------------------------------------

    [Fact]
    public void TryGetLongestRepeatedSubstring_ShouldFindTheLongestRepeat_WhenOneExists()
    {
        var index = new SuffixArray(Banana);

        Assert.True(index.TryGetLongestRepeatedSubstring(out int start, out int length));
        Assert.Equal("ana", index.Text.Slice(start, length).ToString());
    }

    [Fact]
    public void TryGetLongestRepeatedSubstring_ShouldAllowOverlap_WhenTheTextIsOneRepeatedCharacter()
    {
        var index = new SuffixArray("aaa");

        Assert.True(index.TryGetLongestRepeatedSubstring(out int start, out int length));
        Assert.Equal("aa", index.Text.Slice(start, length).ToString());
    }

    [Fact]
    public void TryGetLongestRepeatedSubstring_ShouldReportTheLexicographicallySmallest_WhenTwoRepeatsTieOnLength()
    {
        // "ab" (at 0 and 2) and "cd" (at 5 and 7) both repeat at length two and nothing longer does, so this
        // is the only shape that can tell the documented tie-break from its opposite: relaxing the scan's
        // strict comparison to >= would report "cd" and every other test here would still pass.
        var index = new SuffixArray("abab_cdcd");

        Assert.True(index.TryGetLongestRepeatedSubstring(out int start, out int length));
        Assert.Equal(2, length);
        Assert.Equal("ab", index.Text.Slice(start, length).ToString());
        Assert.Equal(0, start);
    }

    [Fact]
    public void TryGetLongestRepeatedSubstring_ShouldReturnFalse_WhenNoCharacterRepeats()
    {
        var index = new SuffixArray("abcd");

        Assert.False(index.TryGetLongestRepeatedSubstring(out int start, out int length));
        Assert.Equal(0, start);
        Assert.Equal(0, length);
    }

    [Fact]
    public void TryGetLongestRepeatedSubstring_ShouldReturnFalse_WhenTheTextIsEmpty()
    {
        Assert.False(new SuffixArray(string.Empty).TryGetLongestRepeatedSubstring(out _, out _));
    }

    [Fact]
    public void TryGetLongestRepeatedSubstring_ShouldReportOneOfTheOccurrences_WhenTheRepeatIsAWord()
    {
        const string text = "the cat sat on the mat";
        var index = new SuffixArray(text);

        Assert.True(index.TryGetLongestRepeatedSubstring(out int start, out int length));
        Assert.Equal("the ", index.Text.Slice(start, length).ToString());
        Assert.Equal(2, index.CountOccurrences(index.Text.Slice(start, length)));
    }
}
