namespace Celerity.Collections;

/// <summary>
/// One word found within an edit distance of a query — the result type of <see cref="EditDistanceIndex"/>.
/// </summary>
/// <remarks>
/// <para>
/// The match carries the word's id and its distance rather than the word itself, so producing it costs nothing:
/// <c>index[match.WordId]</c> is the word when it is actually wanted. <see cref="Distance"/> is the exact
/// Levenshtein distance between the query and that word, not an estimate or an upper bound.
/// </para>
/// <para>
/// Nothing here is validated: the type is a plain pair, and <see cref="EditDistanceIndex"/> is what guarantees
/// the id names one of its words and the distance is the true one.
/// </para>
/// </remarks>
public readonly struct FuzzyMatch : IEquatable<FuzzyMatch>
{
    /// <summary>Initializes a new match of one word at one distance.</summary>
    /// <param name="wordId">The id of the word that matched.</param>
    /// <param name="distance">The Levenshtein distance between the query and that word.</param>
    public FuzzyMatch(int wordId, int distance)
    {
        WordId = wordId;
        Distance = distance;
    }

    /// <summary>Gets the id of the word that matched.</summary>
    /// <remarks>
    /// An index into the <see cref="EditDistanceIndex"/> that produced this match, over <c>[0, Count)</c>.
    /// </remarks>
    public int WordId { get; }

    /// <summary>Gets the Levenshtein distance between the query and the word.</summary>
    /// <remarks>
    /// The minimum number of single-character insertions, deletions and substitutions that turn one into the
    /// other, counted over UTF-16 code units. Zero means the word is the query.
    /// </remarks>
    public int Distance { get; }

    /// <summary>Determines whether two matches name the same word at the same distance.</summary>
    /// <param name="left">The first match.</param>
    /// <param name="right">The second match.</param>
    /// <returns><c>true</c> if both components match; otherwise <c>false</c>.</returns>
    public static bool operator ==(FuzzyMatch left, FuzzyMatch right) => left.Equals(right);

    /// <summary>Determines whether two matches differ in word or in distance.</summary>
    /// <param name="left">The first match.</param>
    /// <param name="right">The second match.</param>
    /// <returns><c>true</c> if the matches differ; otherwise <c>false</c>.</returns>
    public static bool operator !=(FuzzyMatch left, FuzzyMatch right) => !left.Equals(right);

    /// <summary>Deconstructs the match into its components.</summary>
    /// <param name="wordId">Receives the id of the word that matched.</param>
    /// <param name="distance">Receives the distance between the query and that word.</param>
    public void Deconstruct(out int wordId, out int distance)
    {
        wordId = WordId;
        distance = Distance;
    }

    /// <summary>Determines whether this match names the same word at the same distance as another.</summary>
    /// <param name="other">The match to compare against.</param>
    /// <returns><c>true</c> if both components match; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Two matches from different indexes can compare equal — the id is not qualified by the index it came
    /// from, and there is no cheap way to qualify it.
    /// </remarks>
    public bool Equals(FuzzyMatch other) => WordId == other.WordId && Distance == other.Distance;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is FuzzyMatch other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(WordId, Distance);

    /// <summary>Returns a string of the form <c>#id ~distance</c>.</summary>
    /// <returns>A readable rendering of the match.</returns>
    public override string ToString() => $"#{WordId} ~{Distance}";
}
