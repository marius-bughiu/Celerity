using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="Rope"/>: the character enumerator, the chunk enumerator, both
/// interface-typed enumerators, and the invalidation rule.
///
/// <para>
/// The rule is <b>structural</b> rather than textual, and the two operations that prove it are the ones that
/// come apart. Assignment through the indexer changes what the rope <i>says</i> and does not invalidate: it
/// replaces one code unit inside one leaf, splits nothing, relinks nothing and moves no chunk boundary, so the
/// sequence an enumerator is walking is unchanged. <see cref="Rope.TrimExcess"/> is the mirror image — it
/// leaves the text identical and <i>does</i> invalidate, because it rebuilds every leaf. Stating the rule as
/// "whatever changes the text" would get both of them backwards, so both are pinned here.
/// </para>
///
/// <para>
/// The no-op operations are pinned for the same reason and from the other side: inserting nothing, removing
/// nothing, clearing an empty rope, splitting at the length and appending an empty source all change no
/// character, so none of them may invalidate either.
/// </para>
/// </summary>
public class RopeEnumerationTests
{
    private const int Tiny = Rope.MinChunkSize;

    private static Rope Deep(string text) => new(text, Tiny);

    [Fact]
    public void GetEnumerator_ShouldYieldEveryCharacterInOrder_WhenTheRopeSpansManyLeaves()
    {
        string text = "the quick brown fox jumps over the lazy dog";
        Rope rope = Deep(text);

        var seen = new List<char>();
        foreach (char c in rope)
            seen.Add(c);

        Assert.Equal(text.ToCharArray(), seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheRopeIsEmpty()
    {
        var rope = new Rope();

        Rope.Enumerator enumerator = rope.GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal('\0', enumerator.Current);
    }

    [Fact]
    public void GetEnumerator_ShouldExposeTheDefaultCurrent_WhenMoveNextHasNotRunYet()
    {
        Rope rope = Deep("abc");

        Rope.Enumerator enumerator = rope.GetEnumerator();

        Assert.Equal('\0', enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal('a', enumerator.Current);
    }

    [Fact]
    public void GenericEnumerator_ShouldYieldEveryCharacter_WhenUsedThroughTheInterface()
    {
        Rope rope = Deep("abcdefghij");

        var seen = new List<char>();
        using (IEnumerator<char> enumerator = ((IEnumerable<char>)rope).GetEnumerator())
        {
            while (enumerator.MoveNext())
                seen.Add(enumerator.Current);
        }

        Assert.Equal("abcdefghij".ToCharArray(), seen);
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldEveryCharacter_WhenUsedThroughTheInterface()
    {
        Rope rope = Deep("abcdefghij");

        var seen = new List<char>();
        IEnumerator enumerator = ((IEnumerable)rope).GetEnumerator();
        while (enumerator.MoveNext())
            seen.Add((char)enumerator.Current!);

        Assert.Equal("abcdefghij".ToCharArray(), seen);
    }

    [Fact]
    public void Reset_ShouldRewindToTheStart_WhenTheRopeWasNotModified()
    {
        Rope rope = Deep("abcdefghijklmnop");
        Rope.Enumerator enumerator = rope.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Equal('b', enumerator.Current);

        enumerator.Reset();

        Assert.Equal('\0', enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal('a', enumerator.Current);
    }

    [Fact]
    public void Reset_ShouldThrow_WhenTheRopeWasModified()
    {
        Rope rope = Deep("abcdefghijklmnop");
        Rope.Enumerator enumerator = rope.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        rope.Insert(0, "!");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        Assert.Contains("Collection was modified", ex.Message);
    }

    [Fact]
    public void Dispose_ShouldDoNothing_WhenCalled()
    {
        Rope rope = Deep("abc");
        Rope.Enumerator enumerator = rope.GetEnumerator();

        enumerator.Dispose();

        Assert.True(enumerator.MoveNext());
    }

    [Theory]
    [MemberData(nameof(InvalidatingOperations))]
    public void MoveNext_ShouldThrow_WhenTheRopeWasModifiedDuringEnumeration(
        string name,
        Action<Rope> mutate)
    {
        Assert.NotNull(name);
        Rope rope = Deep("abcdefghijklmnop");
        Rope.Enumerator enumerator = rope.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        mutate(rope);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Theory]
    [MemberData(nameof(InvalidatingOperations))]
    public void ChunkMoveNext_ShouldThrow_WhenTheRopeWasModifiedDuringEnumeration(
        string name,
        Action<Rope> mutate)
    {
        Assert.NotNull(name);
        Rope rope = Deep("abcdefghijklmnop");
        Rope.ChunkEnumerator enumerator = rope.GetChunks();
        Assert.True(enumerator.MoveNext());

        mutate(rope);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    public static TheoryData<string, Action<Rope>> InvalidatingOperations => new()
    {
        { "Insert", rope => rope.Insert(2, "!") },
        { "Append", rope => rope.Append('!') },
        { "Remove", rope => rope.Remove(1, 2) },
        { "Clear", rope => rope.Clear() },
        { "TrimExcess", rope => rope.TrimExcess() },
        { "Split", rope => rope.Split(4) },
        { "AppendAndClear", rope => rope.AppendAndClear(new Rope("more", Tiny)) },
    };

    [Theory]
    [MemberData(nameof(NonInvalidatingOperations))]
    public void MoveNext_ShouldKeepGoing_WhenTheOperationChangedNoCharacterSequence(
        string name,
        Action<Rope> operation)
    {
        Assert.NotNull(name);
        Rope rope = Deep("abcdefghijklmnop");
        Rope.Enumerator enumerator = rope.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        operation(rope);

        Assert.True(enumerator.MoveNext());
        Assert.Equal('b', enumerator.Current);
    }

    public static TheoryData<string, Action<Rope>> NonInvalidatingOperations => new()
    {
        { "IndexerSet", rope => rope[10] = 'X' },
        { "InsertNothing", rope => rope.Insert(2, string.Empty) },
        { "RemoveNothing", rope => rope.Remove(2, 0) },
        { "SplitAtLength", rope => rope.Split(rope.Length) },
        { "AppendEmptySource", rope => rope.AppendAndClear(new Rope(Tiny)) },
    };

    [Fact]
    public void MoveNext_ShouldNotInvalidate_WhenAnEmptyRopeIsCleared()
    {
        var rope = new Rope();
        Rope.Enumerator enumerator = rope.GetEnumerator();

        rope.Clear();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldNotInvalidate_WhenAnEmptyRopeIsTrimmed()
    {
        var rope = new Rope();
        Rope.ChunkEnumerator enumerator = rope.GetChunks();

        rope.TrimExcess();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void GetChunks_ShouldYieldEveryRunInOrder_WhenIteratedInAForeach()
    {
        string text = "the quick brown fox jumps over the lazy dog";
        Rope rope = Deep(text);

        var rebuilt = new System.Text.StringBuilder();
        int runs = 0;
        foreach (ReadOnlySpan<char> chunk in rope.GetChunks())
        {
            rebuilt.Append(chunk);
            runs++;
        }

        Assert.Equal(text, rebuilt.ToString());
        Assert.Equal(rope.LeafCount, runs);
    }

    [Fact]
    public void GetChunks_ShouldYieldNothingAndAnEmptyCurrent_WhenTheRopeIsEmpty()
    {
        var rope = new Rope();

        Rope.ChunkEnumerator enumerator = rope.GetChunks();

        Assert.True(enumerator.Current.IsEmpty);
        Assert.False(enumerator.MoveNext());
        Assert.True(enumerator.Current.IsEmpty);
    }

    [Fact]
    public void GetChunks_ShouldResetCurrent_WhenTheLastRunHasBeenYielded()
    {
        Rope rope = Deep("abc");

        Rope.ChunkEnumerator enumerator = rope.GetChunks();

        Assert.True(enumerator.MoveNext());
        Assert.Equal("abc", enumerator.Current.ToString());
        Assert.False(enumerator.MoveNext());
        Assert.True(enumerator.Current.IsEmpty);
    }

    [Fact]
    public void GetEnumerator_OnAChunkEnumerator_ShouldReturnItself_WhenCalled()
    {
        Rope rope = Deep("abcdefghij");

        Rope.ChunkEnumerator enumerator = rope.GetChunks().GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.Current.IsEmpty);
    }
}
