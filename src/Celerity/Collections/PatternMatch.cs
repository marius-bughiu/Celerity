namespace Celerity.Collections;

/// <summary>
/// One occurrence of one pattern in a text — the result type of <see cref="AhoCorasick"/>.
/// </summary>
/// <remarks>
/// <para>
/// The match carries a position and a length rather than the matched text, so producing it costs nothing: the
/// substring is <c>text.Slice(match.Start, match.Length)</c> when it is actually wanted, and the enumerating
/// tier never allocates one. <see cref="PatternId"/> indexes back into the automaton that produced the match,
/// so <c>automaton[match.PatternId]</c> is the pattern that matched — which is the piece a
/// <see cref="MemoryExtensions.IndexOfAny{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/>-shaped API cannot give you
/// without a second pass.
/// </para>
/// <para>
/// Nothing here is validated: the type is a plain triple, and <see cref="AhoCorasick"/> is what guarantees the
/// range lies inside the text it scanned.
/// </para>
/// </remarks>
public readonly struct PatternMatch : IEquatable<PatternMatch>
{
    /// <summary>Initializes a new match of one pattern at one position.</summary>
    /// <param name="patternId">The id of the pattern that matched.</param>
    /// <param name="start">The position in the text where the match begins.</param>
    /// <param name="length">The number of characters matched, which is the pattern's length.</param>
    public PatternMatch(int patternId, int start, int length)
    {
        PatternId = patternId;
        Start = start;
        Length = length;
    }

    /// <summary>Gets the id of the pattern that matched.</summary>
    /// <remarks>
    /// An index into the <see cref="AhoCorasick"/> that produced this match, over <c>[0, Count)</c>.
    /// </remarks>
    public int PatternId { get; }

    /// <summary>Gets the position in the text where the match begins.</summary>
    public int Start { get; }

    /// <summary>Gets the number of characters matched.</summary>
    /// <remarks>Always the length of the pattern; the automaton matches literally.</remarks>
    public int Length { get; }

    /// <summary>Gets the position just past the last matched character.</summary>
    /// <remarks>
    /// <c>Start + Length</c>. Matches are reported in ascending order of this value, so it is the one to
    /// compare when the reporting order matters.
    /// </remarks>
    public int End => Start + Length;

    /// <summary>Determines whether two matches are the same pattern over the same range.</summary>
    /// <param name="left">The first match.</param>
    /// <param name="right">The second match.</param>
    /// <returns><c>true</c> if all three components match; otherwise <c>false</c>.</returns>
    public static bool operator ==(PatternMatch left, PatternMatch right) => left.Equals(right);

    /// <summary>Determines whether two matches differ in pattern or in position.</summary>
    /// <param name="left">The first match.</param>
    /// <param name="right">The second match.</param>
    /// <returns><c>true</c> if the matches differ; otherwise <c>false</c>.</returns>
    public static bool operator !=(PatternMatch left, PatternMatch right) => !left.Equals(right);

    /// <summary>Deconstructs the match into its components.</summary>
    /// <param name="patternId">Receives the id of the pattern that matched.</param>
    /// <param name="start">Receives the position where the match begins.</param>
    /// <param name="length">Receives the number of characters matched.</param>
    public void Deconstruct(out int patternId, out int start, out int length)
    {
        patternId = PatternId;
        start = Start;
        length = Length;
    }

    /// <summary>Determines whether this match is the same pattern over the same range as another.</summary>
    /// <param name="other">The match to compare against.</param>
    /// <returns><c>true</c> if all three components match; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Two matches from different automatons can compare equal — the id is not qualified by the automaton it
    /// came from, and there is no cheap way to qualify it.
    /// </remarks>
    public bool Equals(PatternMatch other) =>
        PatternId == other.PatternId && Start == other.Start && Length == other.Length;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PatternMatch other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(PatternId, Start, Length);

    /// <summary>Returns a string of the form <c>#id @[start, end)</c>.</summary>
    /// <returns>A readable rendering of the match.</returns>
    public override string ToString() => $"#{PatternId} @[{Start}, {End})";
}
