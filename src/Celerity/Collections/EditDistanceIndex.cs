using System.Buffers;
using System.Collections;
using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// A <b>symmetric-delete index</b>: a build-once, immutable index over a fixed set of words that finds every
/// word within a Levenshtein distance of a query without computing a distance to each word.
/// </summary>
/// <remarks>
/// <para>
/// This is the question none of the other text types answers. <see cref="Trie{TValue}"/> and
/// <see cref="SuccinctTrie{TValue}"/> match exact prefixes, <see cref="AhoCorasick"/> finds exact patterns
/// inside a text, and <see cref="SuffixArray"/> finds exact substrings. Spell-check suggestions, did-you-mean,
/// fuzzy lookup over product names or identifiers and near-duplicate detection all need <i>approximate</i>
/// matching, and the BCL has nothing for it. The caller's option is a linear scan that computes a bounded
/// distance to every word. A careful scan skips words whose length differs by more than <c>k</c> and stops a
/// row early once the distance must exceed <c>k</c>, but it still pays one distance computation per word of a
/// plausible length.
/// </para>
/// <para>
/// <b>How it works.</b> If two strings are within Levenshtein distance <c>k</c>, some string can be reached
/// from <i>each</i> of them by deleting at most <c>k</c> characters: a substitution is one deletion from each
/// side, an insertion into one is a deletion from the other, and a deletion is a deletion. So the build
/// indexes every <b>deletion variant</b> of every word — every string reachable by removing up to
/// <see cref="MaxDistance"/> of its characters — and a query generates its own variants, looks each one up,
/// and verifies the words it finds with an exact bounded Levenshtein distance. The results are <b>exact</b>:
/// every word within the distance is reported, and every reported distance is the true one. A variant is
/// keyed by a 32-bit hash, so a collision can only add a candidate, which the verification then rejects.
/// </para>
/// <para>
/// <b>The cost is memory and build time</b>, and that is the trade this type makes. A word of length
/// <c>L</c> contributes <c>C(L, 0) + C(L, 1) + … + C(L, k)</c> variants: about 11 at <c>k = 1</c> and about
/// 56 at <c>k = 2</c> for a ten-letter word, each stored as one 8-byte posting under a directory of 2 to 4
/// bytes more. <see cref="PostingCount"/> reports the total. Building is a sort of those postings. A query
/// generates <c>C(m, 0) + … + C(m, d)</c> variants of its own length <c>m</c>, so it is cheap for words and
/// short phrases and not intended for long documents. A query whose length lies more than <c>d</c> outside the
/// range from the shortest word's length to the longest's is answered at once without generating any, so a
/// long input against a list of short words costs nothing. Every measured ratio is in
/// <c>docs/api/collections.md</c>.
/// </para>
/// <para>
/// <b>Ordinal, over UTF-16 code units.</b> Characters are compared by value, as
/// <see cref="StringComparison.Ordinal"/> compares them, and an insertion, deletion or substitution is one
/// code unit. There is no culture-aware or case-insensitive mode: fold the words and the query the same way
/// when case should not count. A transposition is two edits, so <c>"teh"</c> is distance 2 from
/// <c>"the"</c>.
/// </para>
/// <para>
/// <b>Duplicates collapse.</b> Supplying the same word twice yields one entry, keeping the id of its first
/// appearance, so <see cref="Count"/> is the number of <i>distinct</i> words. The empty string is a legal word:
/// its distance to a query is the query's length.
/// </para>
/// <para>
/// <b>Build-once.</b> The index is immutable; changing the word set means building a new one, as with
/// <see cref="AhoCorasick"/> and <see cref="SuffixArray"/>. Queries write only to buffers rented for the call,
/// so concurrent readers need no synchronization.
/// </para>
/// <example>
/// <code>
/// var index = new EditDistanceIndex(["apple", "apply", "ample", "maple", "angle"], maxDistance: 2);
///
/// Console.WriteLine(index.ContainsWithin("appel", 1));   // False — a swapped pair is two edits
/// Console.WriteLine(index.CountWithin("aple", 1));       // 3
///
/// foreach (var match in index.FindWithin("aple", 2))
///     Console.WriteLine($"{index[match.WordId]} at {match.Distance}");
/// // apple at 1
/// // ample at 1
/// // maple at 1
/// // apply at 2
/// // angle at 2
///
/// if (index.TryFindNearest("appl", 2, out var nearest))
///     Console.WriteLine(index[nearest.WordId]);          // apple — apply is as close, and came later
/// </code>
/// </example>
/// </remarks>
public sealed class EditDistanceIndex : IReadOnlyList<string>
{
    // The distinct words in id order. Retained because every candidate is verified against its word and because
    // a FuzzyMatch carries an id the caller resolves through the indexer.
    private readonly string[] _words;

    // Every (variant, word) pair as one ulong — the variant's 32-bit hash in the high half and the word id in
    // the low half — sorted ascending and deduplicated, so the postings of one hash are contiguous and a word
    // whose variants repeat (any word with a repeated letter) is listed once under each.
    private readonly ulong[] _postings;

    // A radix directory over the hash's top bits: the postings whose hash starts with prefix p are
    // _postings[_directory[p].._directory[p + 1]). The directory has between half as many and as many entries
    // as there are postings, so a bucket holds one or two of them on average and a lookup reads one line.
    private readonly int[] _directory;
    private readonly int _shift;

    // The shortest and longest word lengths. A word within d of a query is within d of its length, so a query
    // whose length is further than d from this range cannot match anything and is answered without generating
    // a variant. This is what keeps a long query (user input, in a did-you-mean) from costing C(m, d) hashes
    // against a word list whose longest entry is a dozen characters.
    private readonly int _shortest;
    private readonly int _longest;

    // Above this many characters a scratch buffer is rented rather than taken from the stack.
    private const int StackLimit = 256;

    // What a query's candidate buffer starts at before it doubles.
    private const int InitialCandidates = 64;

    /// <summary>Builds an index over <paramref name="words"/> that answers queries up to <paramref name="maxDistance"/>.</summary>
    /// <param name="words">
    /// The words to index. Duplicates collapse to one entry, keeping the id of the first appearance; the
    /// sequence is enumerated exactly once and not retained, though the distinct word strings themselves are
    /// kept.
    /// </param>
    /// <param name="maxDistance">
    /// The largest distance any query will ask for. Every query's distance must be within
    /// <c>[0, maxDistance]</c>, and the index's size grows steeply with it.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="words"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A word is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxDistance"/> is negative, or the deletion variants of the distinct words would need
    /// more than <see cref="Array.MaxLength"/> postings.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The postings are counted before anything is allocated for them, so an index too large to hold is rejected
    /// up front rather than by an impossible allocation. Each word's variants are then hashed into one array,
    /// which is sorted and deduplicated in place: <c>O(P log P)</c> in the posting count <c>P</c>.
    /// </para>
    /// <para>
    /// The build's peak memory is the posting array once, plus a second, exact-length copy when deduplication
    /// removed anything (any word with a repeated letter makes some of its variants equal).
    /// </para>
    /// </remarks>
    public EditDistanceIndex(IEnumerable<string> words, int maxDistance)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDistance);

        // Duplicates are collapsed here, so the postings are sized from what the index will actually hold. The
        // running total is checked against the largest array before anything is allocated for it.
        var accepted = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        int shortest = int.MaxValue;
        int longest = 0;
        foreach (string word in words)
        {
            if (word is null)
                throw new ArgumentException("Words must not contain a null.", nameof(words));

            if (!seen.Add(word))
                continue;

            accepted.Add(word);
            total += VariantCount(word.Length, maxDistance);
            if (total > Array.MaxLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDistance),
                    maxDistance,
                    "The deletion variants of these words would need more than Array.MaxLength postings; lower maxDistance or index fewer words.");
            }

            shortest = Math.Min(shortest, word.Length);
            longest = Math.Max(longest, word.Length);
        }

        _shortest = shortest;
        _longest = longest;

        _words = [.. accepted];
        MaxDistance = maxDistance;

        ulong[] postings = new ulong[total];
        char[]? rented = null;
        int scratchLength = ScratchLength(longest, maxDistance);
        Span<char> scratch = scratchLength <= StackLimit
            ? stackalloc char[scratchLength]
            : (rented = ArrayPool<char>.Shared.Rent(scratchLength));
        try
        {
            var sink = new PostingSink(postings);
            for (int id = 0; id < _words.Length; id++)
            {
                sink.WordId = (uint)id;
                VisitVariants(_words[id], 0, maxDistance, scratch, ref sink);
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<char>.Shared.Return(rented);
        }

        Array.Sort(postings);

        int unique = 0;
        for (int i = 0; i < postings.Length; i++)
        {
            if (unique == 0 || postings[i] != postings[unique - 1])
                postings[unique++] = postings[i];
        }

        _postings = unique == postings.Length ? postings : postings.AsSpan(0, unique).ToArray();

        // floor(log2(P)) bits, so the directory has between P/2 and P buckets. A single posting or none needs no
        // directory bits at all, and the shift of 32 then sends every hash to the one bucket.
        int bits = _postings.Length <= 1 ? 0 : BitOperations.Log2((uint)_postings.Length);
        _shift = 32 - bits;
        _directory = new int[(1 << bits) + 1];

        int position = 0;
        for (int bucket = 0; bucket < 1 << bits; bucket++)
        {
            _directory[bucket] = position;
            while (position < _postings.Length && Bucket(_postings[position]) == bucket)
                position++;
        }

        _directory[1 << bits] = position;
    }

    /// <summary>Gets the number of distinct words the index holds.</summary>
    /// <remarks>Duplicates in the source collapsed, so this can be smaller than what was supplied.</remarks>
    public int Count => _words.Length;

    /// <summary>Gets the largest distance a query may ask for — the one the index was built for.</summary>
    public int MaxDistance { get; }

    /// <summary>Gets the number of (variant, word) postings the index stores.</summary>
    /// <remarks>
    /// The size the footprint is proportional to, at 8 bytes a posting plus a directory of 2 to 4 bytes per
    /// posting more. A word of length <c>L</c> contributes up to <c>C(L, 0) + … + C(L, MaxDistance)</c> of them,
    /// and fewer when it repeats a letter, since two different sets of deletions can then leave the same string
    /// (deleting either <c>a</c> of <c>"aab"</c> gives <c>"ab"</c>), or in the rare case that two of its variants
    /// share a 32-bit hash.
    /// </remarks>
    public int PostingCount => _postings.Length;

    /// <summary>Gets the words in id order, copying nothing.</summary>
    /// <remarks>
    /// The allocation-free way to reach the whole set; the indexer is the way to resolve a single
    /// <see cref="FuzzyMatch.WordId"/>.
    /// </remarks>
    public ReadOnlySpan<string> Words => _words;

    /// <summary>Gets the word with id <paramref name="wordId"/>.</summary>
    /// <param name="wordId">The id, over <c>[0, Count)</c>.</param>
    /// <returns>The word that id names.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="wordId"/> is outside <c>[0, Count)</c>.</exception>
    /// <remarks>This is how a <see cref="FuzzyMatch"/> is turned back into the word it found.</remarks>
    public string this[int wordId]
    {
        get
        {
            if ((uint)wordId >= (uint)_words.Length)
                throw new ArgumentOutOfRangeException(nameof(wordId), wordId, "Word id must be within [0, Count).");

            return _words[wordId];
        }
    }

    /// <summary>Reports whether any word is within <paramref name="maxDistance"/> of <paramref name="query"/>.</summary>
    /// <param name="query">The string to look up.</param>
    /// <param name="maxDistance">The largest distance that counts, over <c>[0, MaxDistance]</c>.</param>
    /// <returns><c>true</c> when at least one word is within the distance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxDistance"/> is outside <c>[0, MaxDistance]</c>.
    /// </exception>
    /// <remarks>
    /// The cheapest tier: the candidates are verified in the order the lookups found them, without sorting or
    /// deduplicating them, and verification stops at the first word that is close enough.
    /// </remarks>
    public bool ContainsWithin(ReadOnlySpan<char> query, int maxDistance)
    {
        ValidateDistance(maxDistance);

        var candidates = Gather(query, maxDistance);
        try
        {
            foreach (int id in candidates.Items)
            {
                if (BoundedDistance(query, _words[id], maxDistance) <= maxDistance)
                    return true;
            }

            return false;
        }
        finally
        {
            candidates.Dispose();
        }
    }

    /// <summary>Counts the words within <paramref name="maxDistance"/> of <paramref name="query"/>.</summary>
    /// <param name="query">The string to look up.</param>
    /// <param name="maxDistance">The largest distance that counts, over <c>[0, MaxDistance]</c>.</param>
    /// <returns>The number of distinct words within the distance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxDistance"/> is outside <c>[0, MaxDistance]</c>.
    /// </exception>
    /// <remarks>
    /// This is the size <see cref="CopyWithin"/> needs to hold every match. Nothing is allocated; the candidate
    /// buffer is rented for the call and returned.
    /// </remarks>
    public int CountWithin(ReadOnlySpan<char> query, int maxDistance)
    {
        ValidateDistance(maxDistance);

        var candidates = Gather(query, maxDistance);
        try
        {
            int found = 0;
            foreach (int id in candidates.Unique())
            {
                if (BoundedDistance(query, _words[id], maxDistance) <= maxDistance)
                    found++;
            }

            return found;
        }
        finally
        {
            candidates.Dispose();
        }
    }

    /// <summary>Finds the word closest to <paramref name="query"/>, if one is within <paramref name="maxDistance"/>.</summary>
    /// <param name="query">The string to look up.</param>
    /// <param name="maxDistance">The largest distance that counts, over <c>[0, MaxDistance]</c>.</param>
    /// <param name="match">The closest word, and the lowest id among those at that distance; the default when there is none.</param>
    /// <returns><c>true</c> when at least one word is within the distance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxDistance"/> is outside <c>[0, MaxDistance]</c>.
    /// </exception>
    /// <remarks>
    /// The candidates are verified in ascending id order with a bound that tightens to one below the best
    /// distance found so far, so a far candidate checked after a near one is abandoned early, and an exact
    /// match ends the search. Ties go to the lowest id, which is the word that appeared first in the source.
    /// </remarks>
    public bool TryFindNearest(ReadOnlySpan<char> query, int maxDistance, out FuzzyMatch match)
    {
        ValidateDistance(maxDistance);

        var candidates = Gather(query, maxDistance);
        try
        {
            int bound = maxDistance;
            int bestId = -1;
            int bestDistance = 0;
            foreach (int id in candidates.Unique())
            {
                int distance = BoundedDistance(query, _words[id], bound);
                if (distance > bound)
                    continue;

                bestId = id;
                bestDistance = distance;
                if (distance == 0)
                    break;

                bound = distance - 1;
            }

            match = bestId >= 0 ? new FuzzyMatch(bestId, bestDistance) : default;
            return bestId >= 0;
        }
        finally
        {
            candidates.Dispose();
        }
    }

    /// <summary>Returns every word within <paramref name="maxDistance"/> of <paramref name="query"/>.</summary>
    /// <param name="query">The string to look up.</param>
    /// <param name="maxDistance">The largest distance that counts, over <c>[0, MaxDistance]</c>.</param>
    /// <returns>
    /// The matches in ascending distance, and in ascending id among those at one distance, or an empty array
    /// when no word is within the distance.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxDistance"/> is outside <c>[0, MaxDistance]</c>.
    /// </exception>
    /// <remarks>
    /// The convenience tier: it allocates the result. <see cref="CopyWithin"/> writes the same sequence into a
    /// caller's buffer instead.
    /// </remarks>
    public FuzzyMatch[] FindWithin(ReadOnlySpan<char> query, int maxDistance)
    {
        ValidateDistance(maxDistance);

        var found = Collect(query, maxDistance);
        try
        {
            if (found.Length == 0)
                return [];

            var result = new FuzzyMatch[found.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = Unpack(found[i]);

            return result;
        }
        finally
        {
            found.Dispose();
        }
    }

    /// <summary>
    /// Writes the words within <paramref name="maxDistance"/> of <paramref name="query"/> into
    /// <paramref name="destination"/>, closest first, allocating nothing.
    /// </summary>
    /// <param name="query">The string to look up.</param>
    /// <param name="maxDistance">The largest distance that counts, over <c>[0, MaxDistance]</c>.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <param name="destinationIndex">The position in <paramref name="destination"/> to start writing at.</param>
    /// <returns>The number of matches written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxDistance"/> is outside <c>[0, MaxDistance]</c>, or <paramref name="destinationIndex"/>
    /// is outside <c>[0, destination.Length]</c>.
    /// </exception>
    /// <remarks>
    /// The order is the one <see cref="FindWithin"/> returns — ascending distance, then ascending id — and when
    /// there are more matches than room, the ones written are the <b>closest</b>: a buffer of five holds the
    /// five best suggestions, not the first five found. Every match is still verified to rank them, so a small
    /// buffer does not make the query cheaper. <see cref="CountWithin"/> sizes the buffer when every match is
    /// needed.
    /// </remarks>
    public int CopyWithin(ReadOnlySpan<char> query, int maxDistance, FuzzyMatch[] destination, int destinationIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if ((uint)destinationIndex > (uint)destination.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(destinationIndex), destinationIndex, "Destination index must be within [0, destination.Length].");
        }

        ValidateDistance(maxDistance);

        var found = Collect(query, maxDistance);
        try
        {
            int written = Math.Min(found.Length, destination.Length - destinationIndex);
            for (int i = 0; i < written; i++)
                destination[destinationIndex + i] = Unpack(found[i]);

            return written;
        }
        finally
        {
            found.Dispose();
        }
    }

    /// <summary>Returns an enumerator over the words, in id order.</summary>
    /// <returns>A struct enumerator over the words.</returns>
    /// <remarks>
    /// Enumerating the index yields its <i>words</i>; <see cref="FindWithin"/> is what yields matches against a
    /// query.
    /// </remarks>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // The exact Levenshtein distance between a and b when it is at most `bound`, otherwise bound + 1. Shared
    // prefixes and suffixes are stripped first, since they never cost an edit, and a length gap wider than the
    // bound is rejected without a table. Otherwise one row of the table is kept, over the shorter string, and
    // the walk stops as soon as a whole row exceeds the bound, because a row's minimum never decreases.
    private static int BoundedDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b, int bound)
    {
        // No two strings are further apart than the longer one's length, so a bound above that admits nothing
        // more, and clamping it keeps bound + 1 from wrapping when a caller passes int.MaxValue for "any".
        bound = Math.Min(bound, Math.Max(a.Length, b.Length));

        int prefix = a.CommonPrefixLength(b);
        a = a[prefix..];
        b = b[prefix..];

        int suffix = 0;
        while (suffix < a.Length && suffix < b.Length && a[^(suffix + 1)] == b[^(suffix + 1)])
            suffix++;

        a = a[..^suffix];
        b = b[..^suffix];

        if (a.Length < b.Length)
        {
            ReadOnlySpan<char> swap = a;
            a = b;
            b = swap;
        }

        if (a.Length - b.Length > bound)
            return bound + 1;

        if (b.Length == 0)
            return a.Length;

        int[]? rented = null;
        Span<int> row = b.Length < StackLimit
            ? stackalloc int[b.Length + 1]
            : (rented = ArrayPool<int>.Shared.Rent(b.Length + 1));
        try
        {
            for (int j = 0; j <= b.Length; j++)
                row[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                char value = a[i - 1];
                int diagonal = row[0];
                row[0] = i;
                int rowMinimum = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int above = row[j];
                    int cell = diagonal + (value == b[j - 1] ? 0 : 1);
                    cell = Math.Min(cell, above + 1);
                    cell = Math.Min(cell, row[j - 1] + 1);
                    row[j] = cell;
                    diagonal = above;
                    rowMinimum = Math.Min(rowMinimum, cell);
                }

                if (rowMinimum > bound)
                    return bound + 1;
            }

            return Math.Min(row[b.Length], bound + 1);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<int>.Shared.Return(rented);
        }
    }

    // The number of subsets of at most `deletions` positions out of `length`, which is how many variants a word
    // of that length generates. Saturates just past Array.MaxLength, which is all the caller compares it to;
    // each term is at most that limit times `length` before the division, so a long holds it.
    private static long VariantCount(int length, int deletions)
    {
        long total = 0;
        long term = 1;
        int depth = Math.Min(length, deletions);
        for (int j = 0; j <= depth; j++)
        {
            total += term;
            if (total > Array.MaxLength)
                return (long)Array.MaxLength + 1;

            term = term * (length - j) / (j + 1);
        }

        return total;
    }

    // The scratch a variant walk needs: one buffer per deletion level below the string itself, each one
    // character shorter than the last.
    private static int ScratchLength(int length, int deletions)
    {
        int depth = Math.Min(length, deletions);
        long total = 0;
        for (int j = 1; j <= depth; j++)
            total += length - j;

        return (int)Math.Min(total, Array.MaxLength);
    }

    private int Bucket(ulong posting) => (int)((posting >> 32) >> _shift);

    private void ValidateDistance(int maxDistance)
    {
        if ((uint)maxDistance > (uint)MaxDistance)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDistance), maxDistance, "Distance must be within [0, MaxDistance].");
        }
    }

    // Every candidate a query's variants name, duplicates included, in a buffer rented for the call. Asking for
    // fewer deletions than the index was built with is still exact: a word within d of the query shares a
    // variant with it reachable in at most d deletions from each, and the word's variants up to MaxDistance
    // include all of those.
    private CandidateBuffer Gather(ReadOnlySpan<char> query, int maxDistance)
    {
        var candidates = new CandidateBuffer(this, InitialCandidates);
        if (query.Length > (long)_longest + maxDistance || query.Length < (long)_shortest - maxDistance)
            return candidates;

        char[]? rented = null;
        int scratchLength = ScratchLength(query.Length, maxDistance);
        Span<char> scratch = scratchLength <= StackLimit
            ? stackalloc char[scratchLength]
            : (rented = ArrayPool<char>.Shared.Rent(scratchLength));
        try
        {
            VisitVariants(query, 0, maxDistance, scratch, ref candidates);
            return candidates;
        }
        finally
        {
            if (rented is not null)
                ArrayPool<char>.Shared.Return(rented);
        }
    }

    // Appends every word id posted under `hash` to the buffer. The postings of one hash are contiguous and
    // sorted, and they all sit in the one directory bucket the hash's top bits name.
    private void AppendPostings(uint hash, ref CandidateBuffer candidates)
    {
        int bucket = (int)((ulong)hash >> _shift);
        int end = _directory[bucket + 1];
        for (int position = _directory[bucket]; position < end; position++)
        {
            ulong posting = _postings[position];
            uint postedHash = (uint)(posting >> 32);
            if (postedHash < hash)
                continue;

            if (postedHash > hash)
                break;

            candidates.Add((int)(uint)posting);
        }
    }

    // Every verified match, packed as (distance << 32 | id) so that sorting the longs sorts by distance and then
    // by id, in a buffer rented for the call.
    private ResultBuffer Collect(ReadOnlySpan<char> query, int maxDistance)
    {
        var candidates = Gather(query, maxDistance);
        try
        {
            var found = new ResultBuffer(candidates.Count);
            foreach (int id in candidates.Unique())
            {
                int distance = BoundedDistance(query, _words[id], maxDistance);
                if (distance <= maxDistance)
                    found.Add(((long)distance << 32) | (uint)id);
            }

            found.Sort();
            return found;
        }
        finally
        {
            candidates.Dispose();
        }
    }

    private static FuzzyMatch Unpack(long packed) => new((int)(uint)packed, (int)(packed >> 32));

    // Calls the visitor with the hash of `current` and of every string reachable from it by deleting up to
    // `deletions` more characters at positions from `from` onwards. Restricting each level to positions at or
    // after the last deletion visits every subset of deleted positions exactly once, which is what makes
    // VariantCount an exact count. Each level writes its child into its own slice of `scratch`, so the parent
    // string is never overwritten while it is still being read.
    private static void VisitVariants<TVisitor>(
        ReadOnlySpan<char> current, int from, int deletions, Span<char> scratch, ref TVisitor visitor)
        where TVisitor : struct, IVariantVisitor
    {
        visitor.Visit((uint)string.GetHashCode(current));
        if (deletions == 0 || current.Length == 0)
            return;

        int childLength = current.Length - 1;
        Span<char> child = scratch[..childLength];
        Span<char> rest = scratch[childLength..];
        for (int position = from; position < current.Length; position++)
        {
            current[..position].CopyTo(child);
            current[(position + 1)..].CopyTo(child[position..]);
            VisitVariants(child, position, deletions - 1, rest, ref visitor);
        }
    }

    // Receives the hash of each variant a walk produces.
    private interface IVariantVisitor
    {
        void Visit(uint hash);
    }

    // The build's visitor: writes one posting per variant, in generation order, for the sort to arrange.
    private struct PostingSink : IVariantVisitor
    {
        private readonly ulong[] _postings;
        private int _next;

        public PostingSink(ulong[] postings)
        {
            _postings = postings;
            _next = 0;
            WordId = 0;
        }

        public uint WordId;

        public void Visit(uint hash) => _postings[_next++] = ((ulong)hash << 32) | WordId;
    }

    // The query's visitor: looks each variant up and collects the ids posted under it, in a pooled buffer that
    // doubles as it fills. Unique() sorts them and walks each distinct id once, which is the order
    // TryFindNearest's tie rule relies on.
    private struct CandidateBuffer : IVariantVisitor, IDisposable
    {
        private readonly EditDistanceIndex _index;
        private int[] _items;

        public CandidateBuffer(EditDistanceIndex index, int capacity)
        {
            _index = index;
            _items = ArrayPool<int>.Shared.Rent(capacity);
            Count = 0;
        }

        public int Count { get; private set; }

        public readonly ReadOnlySpan<int> Items => _items.AsSpan(0, Count);

        public void Visit(uint hash) => _index.AppendPostings(hash, ref this);

        public void Add(int wordId)
        {
            if (Count == _items.Length)
            {
                int[] bigger = ArrayPool<int>.Shared.Rent(_items.Length * 2);
                _items.AsSpan(0, Count).CopyTo(bigger);
                ArrayPool<int>.Shared.Return(_items);
                _items = bigger;
            }

            _items[Count++] = wordId;
        }

        public readonly UniqueEnumerator Unique()
        {
            Span<int> items = _items.AsSpan(0, Count);
            items.Sort();
            return new UniqueEnumerator(items);
        }

        public void Dispose()
        {
            ArrayPool<int>.Shared.Return(_items);

            _items = [];
            Count = 0;
        }
    }

    // Walks a sorted span, yielding each distinct value once.
    private ref struct UniqueEnumerator
    {
        private readonly ReadOnlySpan<int> _items;
        private int _index;

        public UniqueEnumerator(ReadOnlySpan<int> items)
        {
            _items = items;
            _index = -1;
        }

        public readonly int Current => _items[_index];

        public readonly UniqueEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            int next = _index + 1;
            while (next < _items.Length && next > 0 && _items[next] == _items[next - 1])
                next++;

            _index = next;
            return next < _items.Length;
        }
    }

    // The verified matches of one query, packed and sorted, in a pooled buffer sized to the candidate count —
    // which bounds them, since every match was a candidate.
    private struct ResultBuffer : IDisposable
    {
        private long[] _items;

        public ResultBuffer(int capacity)
        {
            _items = ArrayPool<long>.Shared.Rent(Math.Max(capacity, 1));
            Length = 0;
        }

        public int Length { get; private set; }

        public readonly long this[int index] => _items[index];

        public void Add(long packed) => _items[Length++] = packed;

        public readonly void Sort() => _items.AsSpan(0, Length).Sort();

        public void Dispose()
        {
            ArrayPool<long>.Shared.Return(_items);

            _items = [];
            Length = 0;
        }
    }

    /// <summary>Enumerates the words of an <see cref="EditDistanceIndex"/> in id order.</summary>
    /// <remarks>
    /// The index is immutable, so there is no version check here and no way for an enumerator to be
    /// invalidated.
    /// </remarks>
    public struct Enumerator : IEnumerator<string>
    {
        private readonly string[] _words;
        private int _index;
        private string _current;

        internal Enumerator(EditDistanceIndex index)
        {
            _words = index._words;
            _index = 0;
            _current = string.Empty;
        }

        /// <summary>Gets the word at the current id.</summary>
        public readonly string Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances to the next word.</summary>
        /// <returns><c>true</c> when a word was produced; <c>false</c> at the end of the set.</returns>
        public bool MoveNext()
        {
            if (_index >= _words.Length)
                return false;

            _current = _words[_index++];
            return true;
        }

        /// <summary>Resets the enumerator to before the first word.</summary>
        public void Reset()
        {
            _index = 0;
            _current = string.Empty;
        }

        /// <summary>Does nothing; the enumerator holds no unmanaged resource.</summary>
        public readonly void Dispose()
        {
        }
    }
}
