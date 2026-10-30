using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The contract of <see cref="EditDistanceIndex"/> by example: construction and its validation, the
/// distinct-word surface, each query tier on hand-checked distances, the argument checks every query shares,
/// and the long-string paths that move the variant scratch and the distance row off the stack.
///
/// <para>
/// Exactness against an oracle lives in <see cref="EditDistanceIndexDifferentialTests"/>; this file pins the
/// cases a reader can check by eye, and the edges that random generation reaches rarely or not at all.
/// </para>
/// </summary>
public class EditDistanceIndexTests
{
    private static readonly string[] Fruit = ["apple", "apply", "ample", "maple", "angle"];

    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldThrow_WhenWordsIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new EditDistanceIndex(null!, 1));
        Assert.Equal("words", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenAWordIsNull()
    {
        var exception = Assert.Throws<ArgumentException>(() => new EditDistanceIndex(["a", null!], 1));
        Assert.Equal("words", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMaxDistanceIsNegative()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new EditDistanceIndex(Fruit, -1));
        Assert.Equal("maxDistance", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowBeforeAllocating_WhenThePostingsWouldExceedTheLargestArray()
    {
        // C(1000, 4) is about 4.1e10 variants for one word, nineteen times Array.MaxLength. The count is checked
        // before the posting array exists, so this costs nothing rather than an OutOfMemoryException.
        string word = new('x', 1000);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new EditDistanceIndex([word], 4));
        Assert.Equal("maxDistance", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldCollapseDuplicates_KeepingTheFirstId()
    {
        var index = new EditDistanceIndex(["kitten", "sitting", "kitten", "mitten", "sitting"], 1);

        Assert.Equal(3, index.Count);
        Assert.Equal(["kitten", "sitting", "mitten"], index.Words.ToArray());
        Assert.Equal(1, index.MaxDistance);
    }

    [Fact]
    public void Constructor_ShouldReadTheSourceExactlyOnce()
    {
        int enumerations = 0;
        IEnumerable<string> Source()
        {
            enumerations++;
            yield return "alpha";
            yield return "beta";
        }

        var index = new EditDistanceIndex(Source(), 2);

        Assert.Equal(1, enumerations);
        Assert.Equal(2, index.Count);
    }

    [Fact]
    public void EmptyIndex_ShouldMatchNothing()
    {
        var index = new EditDistanceIndex([], 2);

        Assert.Equal(0, index.Count);
        Assert.Equal(0, index.PostingCount);
        Assert.False(index.ContainsWithin("anything", 2));
        Assert.Equal(0, index.CountWithin("", 2));
        Assert.False(index.TryFindNearest("x", 2, out FuzzyMatch match));
        Assert.Equal(default, match);
        Assert.Empty(index.FindWithin("x", 2));
    }

    [Fact]
    public void EmptyWord_ShouldBeLegal_AndBeItsQuerysLengthAway()
    {
        var index = new EditDistanceIndex(["", "abc"], 2);

        Assert.Equal([new FuzzyMatch(0, 0)], index.FindWithin("", 0));
        Assert.Equal([new FuzzyMatch(0, 2), new FuzzyMatch(1, 1)], index.FindWithin("ab", 2).OrderBy(match => match.WordId));
        Assert.Equal([new FuzzyMatch(1, 0)], index.FindWithin("abc", 2));
    }

    [Fact]
    public void SingleEmptyWordAtDistanceZero_ShouldHoldOnePosting()
    {
        // The smallest non-empty index: one posting, which is the case the directory sizes to no bits at all.
        var index = new EditDistanceIndex([""], 0);

        Assert.Equal(1, index.PostingCount);
        Assert.True(index.ContainsWithin("", 0));
        Assert.False(index.ContainsWithin("a", 0));
    }

    // ---- PostingCount ------------------------------------------------------------------------------

    [Fact]
    public void PostingCount_ShouldCountEverySubsetOfDeletions_WhenNoLetterRepeats()
    {
        // "abcd" at two deletions: C(4,0) + C(4,1) + C(4,2) = 1 + 4 + 6.
        Assert.Equal(11, new EditDistanceIndex(["abcd"], 2).PostingCount);
    }

    [Fact]
    public void PostingCount_ShouldCollapseVariantsThatRepeatedLettersMakeEqual()
    {
        // "aab" at one deletion is "aab", "ab", "ab", "aa": deleting either 'a' gives the same string.
        Assert.Equal(3, new EditDistanceIndex(["aab"], 1).PostingCount);
    }

    [Fact]
    public void PostingCount_ShouldStopAtTheWordsLength_WhenMaxDistanceExceedsIt()
    {
        // "ab" can lose at most both letters: 1 + 2 + 1 variants however large the distance.
        Assert.Equal(4, new EditDistanceIndex(["ab"], 5).PostingCount);
    }

    // ---- the indexer -------------------------------------------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Indexer_ShouldThrow_WhenTheIdIsOutOfRange(int wordId)
    {
        var index = new EditDistanceIndex(Fruit, 1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => index[wordId]);
        Assert.Equal("wordId", exception.ParamName);
    }

    [Fact]
    public void Indexer_ShouldResolveEveryId()
    {
        var index = new EditDistanceIndex(Fruit, 1);

        for (int id = 0; id < Fruit.Length; id++)
            Assert.Equal(Fruit[id], index[id]);
    }

    // ---- the query tiers ---------------------------------------------------------------------------

    [Fact]
    public void ContainsWithin_ShouldCountAnInsertionADeletionAndASubstitutionAsOneEdit()
    {
        var index = new EditDistanceIndex(["kitten"], 1);

        Assert.True(index.ContainsWithin("kitten", 0));
        Assert.True(index.ContainsWithin("kittens", 1));
        Assert.True(index.ContainsWithin("kiten", 1));
        Assert.True(index.ContainsWithin("sitten", 1));
        Assert.False(index.ContainsWithin("sittin", 1));
    }

    [Fact]
    public void ContainsWithin_ShouldCountATranspositionAsTwoEdits()
    {
        var index = new EditDistanceIndex(["the"], 2);

        Assert.False(index.ContainsWithin("teh", 1));
        Assert.True(index.ContainsWithin("teh", 2));
    }

    [Fact]
    public void ContainsWithin_ShouldKeepVerifying_WhenTheFirstCandidateIsTooFar()
    {
        // The query "bca" and the words "abc" and "bcx" all reach the variant "bc" in one deletion, so both words
        // are candidates. "abc" is two edits from the query and is rejected; "bcx" is one and is found.
        Assert.False(new EditDistanceIndex(["abc"], 1).ContainsWithin("bca", 1));
        Assert.True(new EditDistanceIndex(["abc", "bcx"], 1).ContainsWithin("bca", 1));
    }

    [Fact]
    public void CountWithin_ShouldCountEachWordOnce_EvenWhenSeveralVariantsFindIt()
    {
        // "aaaa" shares many variants with "aaa", so the candidate list holds it repeatedly.
        var index = new EditDistanceIndex(["aaaa", "aaab", "bbbb"], 2);

        Assert.Equal(2, index.CountWithin("aaa", 1));
        Assert.Equal(2, index.CountWithin("aaa", 2));
    }

    [Fact]
    public void FindWithin_ShouldOrderByDistanceThenById()
    {
        var index = new EditDistanceIndex(Fruit, 2);

        Assert.Equal(
            [new FuzzyMatch(0, 1), new FuzzyMatch(2, 1), new FuzzyMatch(3, 1), new FuzzyMatch(1, 2), new FuzzyMatch(4, 2)],
            index.FindWithin("aple", 2));
    }

    [Fact]
    public void FindWithin_ShouldHonourASmallerDistanceThanTheIndexWasBuiltFor()
    {
        var index = new EditDistanceIndex(Fruit, 2);

        Assert.Equal([new FuzzyMatch(0, 1), new FuzzyMatch(2, 1), new FuzzyMatch(3, 1)], index.FindWithin("aple", 1));
        Assert.Empty(index.FindWithin("aple", 0));
    }

    [Fact]
    public void FindWithin_ShouldReturnTheSharedEmptyArray_WhenNothingMatches()
    {
        var index = new EditDistanceIndex(Fruit, 1);

        Assert.Same(Array.Empty<FuzzyMatch>(), index.FindWithin("zzzzzz", 1));
    }

    [Fact]
    public void TryFindNearest_ShouldBreakTiesTowardsTheLowestId()
    {
        var index = new EditDistanceIndex(Fruit, 2);

        Assert.True(index.TryFindNearest("appl", 2, out FuzzyMatch nearest));
        Assert.Equal(new FuzzyMatch(0, 1), nearest);
    }

    [Fact]
    public void TryFindNearest_ShouldPreferACloserWordWithAHigherId()
    {
        // "abcx" is id 0 at distance 2 from "abyz"; "abyx" is id 1 at distance 1. The bound has to tighten
        // after the first and still admit the second.
        var index = new EditDistanceIndex(["abcx", "abyx", "abzz"], 2);

        Assert.True(index.TryFindNearest("abyz", 2, out FuzzyMatch nearest));
        Assert.Equal(new FuzzyMatch(1, 1), nearest);
    }

    [Fact]
    public void TryFindNearest_ShouldStopAtAnExactMatch()
    {
        var index = new EditDistanceIndex(["abcd", "abce", "abcf"], 1);

        Assert.True(index.TryFindNearest("abce", 1, out FuzzyMatch nearest));
        Assert.Equal(new FuzzyMatch(1, 0), nearest);
    }

    [Fact]
    public void TryFindNearest_ShouldReturnFalseAndTheDefault_WhenNothingIsCloseEnough()
    {
        var index = new EditDistanceIndex(Fruit, 1);

        Assert.False(index.TryFindNearest("banana", 1, out FuzzyMatch nearest));
        Assert.Equal(default, nearest);
    }

    [Fact]
    public void CopyWithin_ShouldWriteTheClosestMatches_WhenTheBufferIsTooSmall()
    {
        var index = new EditDistanceIndex(Fruit, 2);
        var destination = new FuzzyMatch[2];

        Assert.Equal(2, index.CopyWithin("aple", 2, destination));
        Assert.Equal([new FuzzyMatch(0, 1), new FuzzyMatch(2, 1)], destination);
    }

    [Fact]
    public void CopyWithin_ShouldStartAtTheDestinationIndex_AndLeaveTheRestAlone()
    {
        var index = new EditDistanceIndex(Fruit, 1);
        var sentinel = new FuzzyMatch(-1, -1);
        FuzzyMatch[] destination = [sentinel, sentinel, sentinel, sentinel, sentinel];

        Assert.Equal(3, index.CopyWithin("aple", 1, destination, 1));
        Assert.Equal([sentinel, new FuzzyMatch(0, 1), new FuzzyMatch(2, 1), new FuzzyMatch(3, 1), sentinel], destination);
    }

    [Fact]
    public void CopyWithin_ShouldWriteNothing_AtTheEndOfTheBuffer()
    {
        var index = new EditDistanceIndex(Fruit, 1);
        var destination = new FuzzyMatch[3];

        Assert.Equal(0, index.CopyWithin("aple", 1, destination, 3));
    }

    [Fact]
    public void CopyWithin_ShouldThrow_WhenTheDestinationIsNull()
    {
        var index = new EditDistanceIndex(Fruit, 1);

        var exception = Assert.Throws<ArgumentNullException>(() => index.CopyWithin("aple", 1, null!));
        Assert.Equal("destination", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void CopyWithin_ShouldThrow_WhenTheDestinationIndexIsOutOfRange(int destinationIndex)
    {
        var index = new EditDistanceIndex(Fruit, 1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => index.CopyWithin("aple", 1, new FuzzyMatch[3], destinationIndex));
        Assert.Equal("destinationIndex", exception.ParamName);
    }

    [Fact]
    public void CopyWithin_ShouldReportTheNullBufferFirst_WhenTheDistanceIsAlsoBad()
    {
        var index = new EditDistanceIndex(Fruit, 1);

        Assert.Throws<ArgumentNullException>(() => index.CopyWithin("aple", 5, null!));
    }

    // ---- queries whose length rules out every word --------------------------------------------------

    [Fact]
    public void AQueryLongerThanEveryWordByMoreThanTheDistance_ShouldMatchNothing()
    {
        // 100,000 characters at distance 2 would be about 5e9 variants if they were generated. The length gap
        // to the longest word answers it first, so this test finishing at all is the assertion that matters.
        var index = new EditDistanceIndex(Fruit, 2);
        string query = new('a', 100_000);

        Assert.False(index.ContainsWithin(query, 2));
        Assert.Equal(0, index.CountWithin(query, 2));
        Assert.False(index.TryFindNearest(query, 2, out _));
        Assert.Empty(index.FindWithin(query, 2));
    }

    [Fact]
    public void AQueryShorterThanEveryWordByMoreThanTheDistance_ShouldMatchNothing()
    {
        var index = new EditDistanceIndex(Fruit, 2);

        Assert.False(index.ContainsWithin("ap", 2));
        Assert.True(index.ContainsWithin("apl", 2));
    }

    [Fact]
    public void TheLengthGate_ShouldAdmitAQueryExactlyTheDistanceAway()
    {
        // "apple" plus two letters is the longest query that can still be within two edits of it.
        var index = new EditDistanceIndex(Fruit, 2);

        Assert.Equal([new FuzzyMatch(0, 2)], index.FindWithin("applexx", 2));
        Assert.Empty(index.FindWithin("applexxx", 2));
    }

    [Fact]
    public void TheLengthGate_ShouldCheckTheRangeOfLengths_NotEachLength()
    {
        // Words of length 2 and 20 leave a gap: a 10-character query is more than one edit from both, but inside
        // the range, so it is looked up (and correctly finds nothing) rather than turned away by the gate.
        var index = new EditDistanceIndex(["ab", new string('c', 20)], 1);

        Assert.Empty(index.FindWithin(new string('c', 10), 1));
        Assert.Equal([new FuzzyMatch(1, 1)], index.FindWithin(new string('c', 19), 1));
    }

    // ---- an unbounded distance --------------------------------------------------------------------

    [Fact]
    public void IntMaxValueAsTheDistance_ShouldStillReportTrueDistances()
    {
        // int.MaxValue is what a caller passes to mean "any distance". The verifier's bound + 1 must not wrap
        // to a negative distance, which would report every word at int.MinValue and pick the wrong nearest.
        var index = new EditDistanceIndex(["abc", "xyz"], int.MaxValue);

        Assert.Equal([new FuzzyMatch(0, 1), new FuzzyMatch(1, 3)], index.FindWithin("abd", int.MaxValue));
        Assert.True(index.TryFindNearest("abd", int.MaxValue, out FuzzyMatch nearest));
        Assert.Equal(new FuzzyMatch(0, 1), nearest);
        Assert.Equal(2, index.CountWithin("abd", int.MaxValue));
    }

    // ---- code units ---------------------------------------------------------------------------------

    [Fact]
    public void ASurrogatePair_ShouldCountAsTwoCodeUnits()
    {
        // U+1F600 and U+1F601 share a high surrogate and differ in the low one: one edit, not two characters
        // of difference. U+1F600 against "ab" is two substitutions of code units.
        var index = new EditDistanceIndex(["\U0001F600"], 2);

        Assert.Equal([new FuzzyMatch(0, 1)], index.FindWithin("\U0001F601", 2));
        Assert.Equal([new FuzzyMatch(0, 2)], index.FindWithin("ab", 2));
    }

    // ---- allocation ---------------------------------------------------------------------------------

    [Fact]
    public void TheNonAllocatingTiers_ShouldAllocateNothing_OnceWarm()
    {
        var index = new EditDistanceIndex(Fruit, 2);
        var destination = new FuzzyMatch[8];

        // Warm the pool and the JIT before measuring.
        for (int i = 0; i < 3; i++)
        {
            index.ContainsWithin("aple", 2);
            index.CountWithin("aple", 2);
            index.TryFindNearest("aple", 2, out _);
            index.CopyWithin("aple", 2, destination);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool contains = index.ContainsWithin("aple", 2);
        int count = index.CountWithin("aple", 2);
        bool found = index.TryFindNearest("aple", 2, out _);
        int written = index.CopyWithin("aple", 2, destination);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(contains && found);
        Assert.Equal(5, count);
        Assert.Equal(5, written);
    }

    // ---- the distance argument every query shares ---------------------------------------------------

    public static TheoryData<string> Queries => ["ContainsWithin", "CountWithin", "TryFindNearest", "FindWithin", "CopyWithin"];

    [Theory]
    [MemberData(nameof(Queries))]
    public void EveryQuery_ShouldThrow_WhenTheDistanceIsNegative(string query) =>
        AssertDistanceRejected(query, -1);

    [Theory]
    [MemberData(nameof(Queries))]
    public void EveryQuery_ShouldThrow_WhenTheDistanceExceedsWhatTheIndexWasBuiltFor(string query) =>
        AssertDistanceRejected(query, 2);

    private static void AssertDistanceRejected(string query, int distance)
    {
        var index = new EditDistanceIndex(Fruit, 1);

        Action call = query switch
        {
            "ContainsWithin" => () => index.ContainsWithin("aple", distance),
            "CountWithin" => () => index.CountWithin("aple", distance),
            "TryFindNearest" => () => index.TryFindNearest("aple", distance, out _),
            "FindWithin" => () => index.FindWithin("aple", distance),
            _ => () => index.CopyWithin("aple", distance, new FuzzyMatch[4]),
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(call);
        Assert.Equal("maxDistance", exception.ParamName);
    }

    // ---- long strings, where the scratch and the distance row are rented rather than stack-allocated --

    [Fact]
    public void LongWordsAndQueries_ShouldStillBeExact()
    {
        // 400-character strings. Building and querying at two deletions needs 399 + 398 characters of variant
        // scratch. `far` differs from `word` at both ends, so neither the shared-prefix nor the shared-suffix
        // trim shortens the pair and its distance row is longer than the stack limit; it is a candidate because
        // deleting both ends of either leaves the same middle.
        var rng = new Random(11);
        char[] body = new char[398];
        for (int i = 0; i < body.Length; i++)
            body[i] = (char)('a' + rng.Next(4));

        string middle = new(body);
        string word = "x" + middle + "y";
        char[] edited = word.ToCharArray();
        edited[200] = edited[200] == 'z' ? 'w' : 'z';

        string near = new(edited);
        string far = "p" + middle + "q";
        string unrelated = new('d', 400);

        var index = new EditDistanceIndex([word, unrelated], 2);

        Assert.True(index.TryFindNearest(near, 1, out FuzzyMatch nearest));
        Assert.Equal(new FuzzyMatch(0, 1), nearest);
        Assert.Equal([new FuzzyMatch(0, 0)], index.FindWithin(word, 0));
        Assert.Equal([new FuzzyMatch(0, 2)], index.FindWithin(far, 2));
        Assert.False(index.ContainsWithin(far, 1));
    }

    [Fact]
    public void ManyCandidates_ShouldOutgrowTheInitialCandidateBuffer()
    {
        // Every two-letter word over a 26-letter alphabet is within two edits of the empty query, and each one
        // is posted under the empty variant: 676 candidates, against an initial buffer of 64.
        List<string> words = [];
        for (char first = 'a'; first <= 'z'; first++)
        {
            for (char second = 'a'; second <= 'z'; second++)
                words.Add(new string([first, second]));
        }

        var index = new EditDistanceIndex(words, 2);

        Assert.Equal(676, index.CountWithin("", 2));
        Assert.Equal(676, index.FindWithin("", 2).Length);
        Assert.True(index.TryFindNearest("", 2, out FuzzyMatch nearest));
        Assert.Equal(new FuzzyMatch(0, 2), nearest);
    }
}
