using System.Buffers;
using System.Collections;

namespace Celerity.Collections;

/// <summary>
/// An <b>Aho–Corasick automaton</b>: a build-once, immutable index over a fixed set of patterns that finds
/// <i>every</i> occurrence of <i>every</i> pattern in one left-to-right pass over a text, at a cost that does
/// not grow with the number of patterns.
/// </summary>
/// <remarks>
/// <para>
/// This is the direction neither of the other text types goes. <see cref="Trie{TValue}"/> matches a
/// <i>prefix of the query</i> — it walks from the root and stops, so finding its keys <i>inside</i> a text
/// means re-walking from every position. <see cref="SuffixArray"/> indexes <i>one fixed text</i> and answers
/// one pattern at a time, which is the wrong way round when the text is what streams past and the pattern set
/// is what is fixed. Here the <b>patterns</b> are indexed and the text is read once.
/// </para>
/// <para>
/// The BCL has nothing that answers the same question.
/// <see cref="string.IndexOf(string, StringComparison)"/> and
/// <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/> are single-needle scans, so
/// <c>k</c> patterns cost <c>k</c> passes over the text — that factor of <c>k</c> is the whole point of this
/// type, and it is what the measured margins are made of.
/// <see cref="System.Text.RegularExpressions.Regex"/> with an alternation is the real workaround and a
/// genuinely strong one; it is measured here as a baseline rather than dismissed, in its compiled and
/// allocation-free forms. .NET 9's <c>SearchValues&lt;string&gt;</c> does index the needles, but it answers
/// <c>IndexOfAny</c> — <i>where is the first of these</i> — not <i>every occurrence of every pattern, and
/// which one</i>, and it does not exist on this library's <c>net8.0</c> floor.
/// </para>
/// <para>
/// <b>Overlapping matches are reported, not resolved.</b> Over <c>"ushers"</c> with the patterns
/// <c>"he"</c>, <c>"she"</c> and <c>"hers"</c> there are three matches — <c>"she"</c> at 1, <c>"he"</c> at 2
/// and <c>"hers"</c> at 2 — and all three are produced. A <see cref="System.Text.RegularExpressions.Regex"/>
/// alternation consumes the text as it matches and so reports one of them; picking a winner is a policy this
/// type leaves to the caller, because the caller is the only one who knows whether the right policy is
/// longest, first-listed or all of them.
/// </para>
/// <para>
/// <b>The reporting order is by end position.</b> Matches come out in ascending
/// <see cref="PatternMatch.End"/>, and among matches ending at the same position, <b>longest first</b> — which
/// is the order the automaton discovers them in, walking from the state it is in down the chain of shorter
/// suffixes. It is <i>not</i> ascending <see cref="PatternMatch.Start"/>: with the patterns <c>"bc"</c> and
/// <c>"abcd"</c> over <c>"abcd"</c>, <c>"bc"</c> is reported first because it ends first, even though
/// <c>"abcd"</c> starts earlier. Sort by <see cref="PatternMatch.Start"/> when leftmost order is what is
/// wanted; the pass cannot produce it, since a match that starts earlier may not be known to exist yet.
/// </para>
/// <para>
/// <b>The cost.</b> Building is <c>O(total pattern length)</c>. A scan is <c>O(n + matches)</c> in the text
/// length and the number of matches reported, and — this is the claim worth checking against the baselines —
/// <b>not a function of how many patterns there are</b>, beyond the <c>log b</c> of a binary search through
/// one state's <c>b</c> outgoing edges. The <c>k</c>-<c>IndexOf</c> loop is <c>O(k · n)</c>, so the margin
/// grows with the pattern count — and it starts <i>negative</i>, because
/// <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/> is vectorized and this pass is
/// a character at a time. Where the crossover sits depends on the <i>shape</i> as well as the count: counting
/// present patterns turns over near 35–50 of them, while merely ruling <i>absent</i> ones out does not turn
/// over until several hundred, because a scan that finds no candidate to verify is a pure vectorized sweep and
/// this pass is not. Every measured ratio is in <c>docs/api/collections.md</c>.
/// </para>
/// <para>
/// <b>Footprint.</b> The automaton holds one state per distinct character position across the patterns, plus
/// the root: a child edge (a <see cref="char"/> and an <see cref="int"/>), a failure link, an output and an
/// output link per state, about <b>22 bytes per pattern character</b> before shared prefixes are counted, and
/// shared prefixes share their states. On top of that sits a fixed 512-byte direct-mapped table for the root's
/// ASCII transitions, which is where a scan spends most of its steps. The patterns themselves are retained, because
/// <see cref="PatternMatch"/> carries an id and the caller needs to resolve it. Build scratch is rented from
/// <see cref="ArrayPool{T}"/> and returned, which is not the same as free — a first or contended build still
/// allocates it.
/// </para>
/// <para>
/// <b>Ordinal, over UTF-16 code units.</b> Characters are compared by value, which is what
/// <see cref="StringComparison.Ordinal"/> compares. There is no culture-aware or case-insensitive mode: fold
/// the patterns and the text the same way before building when case-insensitive matching is wanted. A
/// surrogate pair is two code units, so a pattern may match starting at a low surrogate — check the boundary
/// in the caller's own text when that matters.
/// </para>
/// <para>
/// <b>Duplicates collapse; the empty pattern is rejected.</b> Supplying the same pattern twice yields one
/// entry, keeping the id of its first appearance, so <see cref="Count"/> is the number of <i>distinct</i>
/// patterns. The empty string would match at every position and make <see cref="ContainsAny"/> vacuously
/// <c>true</c>, so it throws rather than being silently absorbed. An empty pattern set is legal and matches
/// nothing.
/// </para>
/// <para>
/// <b>Build-once.</b> The automaton is immutable; changing the pattern set means building a new one, as with
/// <see cref="SuffixArray"/>, <see cref="KdTree{TValue}"/>, <see cref="RTree{TValue}"/> and
/// <see cref="CompressedGraph"/>. Nothing mutates, so concurrent readers need no synchronization and an
/// enumerator can never be invalidated.
/// </para>
/// <example>
/// <code>
/// var automaton = new AhoCorasick(["he", "she", "his", "hers"]);
///
/// Console.WriteLine(automaton.ContainsAny("ushers"));   // True
/// Console.WriteLine(automaton.CountMatches("ushers"));  // 3 — overlaps included
///
/// foreach (var match in automaton.EnumerateMatches("ushers"))
///     Console.WriteLine($"{automaton[match.PatternId]} at {match.Start}");
/// // she at 1
/// // he at 2
/// // hers at 2
/// </code>
/// </example>
/// </remarks>
public sealed class AhoCorasick : IReadOnlyList<string>
{
    // The distinct patterns in id order. Retained because a PatternMatch carries an id and the caller resolves
    // it through the indexer, and because a match's length is the pattern's length.
    private readonly string[] _patterns;

    // The goto function, compressed-sparse-row over the states: the outgoing edges of state s are
    // _childChars[_childStart[s].._childStart[s + 1]] with _childNodes holding the target of each, sorted
    // ascending by character so a transition is a binary search. Every state but the root is entered by
    // exactly one edge, so the two edge arrays are exactly one shorter than the state count.
    private readonly int[] _childStart;
    private readonly char[] _childChars;
    private readonly int[] _childNodes;

    // _fail[s] is the state for the longest proper suffix of s's path that is also a path from the root; the
    // root's is itself. _output[s] is the id of the pattern ending exactly at s, or -1. _outputLink[s] is the
    // nearest state strictly up the failure chain that has an output, or -1 — the shortcut that makes
    // reporting cost the number of matches rather than the depth.
    private readonly int[] _fail;
    private readonly int[] _output;
    private readonly int[] _outputLink;

    // The root's edges, direct-mapped by character. The root is where a scan spends most of its time — every
    // character that continues no partial match is resolved there — so paying a binary search for it dominated
    // everything else; this is one load instead. Non-ASCII falls back to the binary search, and the table is a
    // fixed 512 bytes whatever the pattern set looks like.
    private const int RootTableSize = 128;

    private readonly int[] _rootTransitions;

    // What the build's scratch trie starts at, in states, before it doubles. Small enough that a handful of
    // short patterns does not rent a page it will not use, large enough that a realistic pattern set never
    // grows at all.
    private const int InitialScratchStates = 4096;

    /// <summary>Builds an automaton over <paramref name="patterns"/>.</summary>
    /// <param name="patterns">
    /// The patterns to match. Duplicates collapse to one entry, keeping the id of the first appearance; the
    /// sequence is enumerated exactly once, never copied, and not retained — a duplicate-heavy source is not
    /// charged for the entries it collapses — though the distinct pattern strings themselves are kept.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="patterns"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// A pattern is <c>null</c>, or a pattern is the empty string — which would match at every position.
    /// </exception>
    /// <exception cref="OverflowException">
    /// The distinct patterns total more than <see cref="int.MaxValue"/> characters.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <c>O(total pattern length)</c> expected: the patterns are threaded onto a trie — one hash probe per
    /// character to find or create the edge, which is what keeps a state's branching factor out of the cost —
    /// the trie is flattened into the compressed row layout in breadth-first order, so a scan walks states
    /// roughly in the order it reaches them, and the failure and output links are computed in that same pass,
    /// each from a state of strictly smaller depth that the order guarantees is already written.
    /// </para>
    /// <para>
    /// The trie the flattening reads is scratch: its arrays are rented from <see cref="ArrayPool{T}"/> and
    /// returned, and the edge map and the duplicate set are dropped with it, so what the automaton retains is
    /// the flat arrays and the patterns and nothing else. That scratch is grown as states are created rather
    /// than sized for the worst case, so shared prefixes make the build cheaper in memory as well as in
    /// states — 10,000 patterns sharing a 1,000-character prefix build a trie of some 11,000 states, and only
    /// that is rented, not the ten million characters they add up to.
    /// </para>
    /// </remarks>
    public AhoCorasick(IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        // Duplicates are collapsed here rather than discovered in the trie, so everything below is sized from
        // what the automaton will actually hold: a thousand references to one large string cost one copy of
        // it, not a thousand. The source is read exactly once and never materialized, and neither collection
        // is pre-sized from it, so a duplicate-heavy sequence is not charged for the entries it collapses. The
        // running total is checked because it is a caller-supplied sum and a silent wrap would size the
        // scratch negatively.
        var accepted = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int totalChars = 0;
        foreach (string pattern in patterns)
        {
            if (pattern is null)
                throw new ArgumentException("Patterns must not contain a null.", nameof(patterns));
            if (pattern.Length == 0)
            {
                throw new ArgumentException(
                    "Patterns must not contain the empty string, which would match at every position.",
                    nameof(patterns));
            }

            if (!seen.Add(pattern))
                continue;

            accepted.Add(pattern);
            checked
            {
                totalChars += pattern.Length;
            }
        }

        _patterns = [.. accepted];

        // One trie state per pattern character is the worst case (no two patterns share a prefix), plus the
        // root. The sibling lists are what a state's children live on until the flattening sorts them, and
        // `transitions` is how an edge is *found* while they are being built: a state's children are a linked
        // list, so scanning it would be quadratic in the branching factor, which a set of many one-character
        // patterns reaches. One hash probe per pattern character keeps the build linear in their total length.
        // Grown as states are created rather than sized for the worst case, because the two can be orders of
        // magnitude apart: the state count is the number of *distinct prefixes*, so 10,000 patterns sharing a
        // 1,000-character prefix build a trie of some 11,000 states out of ten million characters, and renting
        // for the sum would ask the pool for well over a hundred megabytes to hold it.
        int capacity = (int)Math.Min((long)totalChars + 1, InitialScratchStates);
        char[] edgeChars = ArrayPool<char>.Shared.Rent(capacity);
        int[] firstChild = ArrayPool<int>.Shared.Rent(capacity);
        int[] nextSibling = ArrayPool<int>.Shared.Rent(capacity);
        int[] terminal = ArrayPool<int>.Shared.Rent(capacity);

        // The pool hands back an array at least as long as asked for and often longer; the usable capacity is
        // whatever the shortest of the four allows.
        capacity = Math.Min(Math.Min(edgeChars.Length, firstChild.Length), Math.Min(nextSibling.Length, terminal.Length));

        var transitions = new Dictionary<long, int>(capacity);
        try
        {
            firstChild[0] = -1;
            nextSibling[0] = -1;
            terminal[0] = -1;

            int stateCount = 1;

            for (int id = 0; id < _patterns.Length; id++)
            {
                int node = 0;
                foreach (char value in _patterns[id])
                {
                    long edge = ((long)node << 16) | value;
                    if (!transitions.TryGetValue(edge, out int child))
                    {
                        if (stateCount == capacity)
                        {
                            // Widened, because both sides of the clamp overflow an int at the extreme: the
                            // doubling once capacity passes 2^30, and the state ceiling when the patterns
                            // total int.MaxValue characters. stateCount can never pass totalChars, so the
                            // ceiling is always above the current capacity and the doubling terminates.
                            long ceiling = Math.Min((long)totalChars + 1, Array.MaxLength);
                            int wanted = (int)Math.Min((long)capacity * 2, ceiling);
                            edgeChars = Grow(edgeChars, stateCount, wanted);
                            firstChild = Grow(firstChild, stateCount, wanted);
                            nextSibling = Grow(nextSibling, stateCount, wanted);
                            terminal = Grow(terminal, stateCount, wanted);
                            capacity = Math.Min(
                                Math.Min(edgeChars.Length, firstChild.Length),
                                Math.Min(nextSibling.Length, terminal.Length));
                        }

                        child = stateCount++;
                        edgeChars[child] = value;
                        firstChild[child] = -1;
                        terminal[child] = -1;
                        nextSibling[child] = firstChild[node];
                        firstChild[node] = child;
                        transitions[edge] = child;
                    }

                    node = child;
                }

                // The patterns are distinct by construction, so no two of them end at the same state and this
                // never overwrites an id.
                terminal[node] = id;
            }

            _childStart = new int[stateCount + 1];
            _childChars = new char[stateCount - 1];
            _childNodes = new int[stateCount - 1];
            _fail = new int[stateCount];
            _output = new int[stateCount];
            _outputLink = new int[stateCount];
            _rootTransitions = new int[RootTableSize];

            Flatten(stateCount, edgeChars, firstChild, nextSibling, terminal);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(edgeChars);
            ArrayPool<int>.Shared.Return(firstChild);
            ArrayPool<int>.Shared.Return(nextSibling);
            ArrayPool<int>.Shared.Return(terminal);
        }
    }

    /// <summary>Gets the number of distinct patterns the automaton matches.</summary>
    /// <remarks>Duplicates in the source collapsed, so this can be smaller than what was supplied.</remarks>
    public int Count => _patterns.Length;

    /// <summary>Gets the number of automaton states, including the root.</summary>
    /// <remarks>
    /// The number of distinct prefixes across the patterns, plus one. This is the size the footprint is
    /// proportional to, and it is the measure of how much the patterns share: <c>Count + 1</c> states would
    /// mean every pattern is a single character, and one state per pattern character means no two patterns
    /// share a prefix at all.
    /// </remarks>
    public int StateCount => _fail.Length;

    /// <summary>Gets the patterns in id order, copying nothing.</summary>
    /// <remarks>
    /// The allocation-free way to reach the whole set; the indexer is the way to resolve a single
    /// <see cref="PatternMatch.PatternId"/>.
    /// </remarks>
    public ReadOnlySpan<string> Patterns => _patterns;

    /// <summary>Gets the pattern with id <paramref name="patternId"/>.</summary>
    /// <param name="patternId">The id, over <c>[0, Count)</c>.</param>
    /// <returns>The pattern that id names.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="patternId"/> is outside <c>[0, Count)</c>.</exception>
    /// <remarks>This is how a <see cref="PatternMatch"/> is turned back into the pattern that produced it.</remarks>
    public string this[int patternId]
    {
        get
        {
            if ((uint)patternId >= (uint)_patterns.Length)
                throw new ArgumentOutOfRangeException(nameof(patternId), patternId, "Pattern id must be within [0, Count).");

            return _patterns[patternId];
        }
    }

    /// <summary>Reports whether any pattern occurs in <paramref name="text"/>.</summary>
    /// <param name="text">The text to scan.</param>
    /// <returns><c>true</c> when at least one pattern occurs.</returns>
    /// <remarks>
    /// The cheapest tier: the pass stops at the first match and no <see cref="PatternMatch"/> is ever formed.
    /// Absent patterns are the case with no shortcut <i>here</i> — the text has to be read to the end to rule
    /// them out — but they are <b>not</b> the case the <c>k</c>-<c>IndexOf</c> loop is slowest on, and this is
    /// the arm the loop still wins at 256 patterns. It reads the whole text too, but a scan that never finds a
    /// candidate worth verifying stays inside its vectorized sweep, so it covers many characters per step where
    /// this covers one. <see cref="CountMatches"/> over patterns that are <i>present</i> is the shape that
    /// turns over, because every hit drops the loop out of that sweep and makes it restart.
    /// </remarks>
    public bool ContainsAny(ReadOnlySpan<char> text)
    {
        int state = 0;
        for (int position = 0; position < text.Length; position++)
        {
            state = Step(state, text[position]);
            if (FirstOutput(state) >= 0)
                return true;
        }

        return false;
    }

    /// <summary>Counts every occurrence of every pattern in <paramref name="text"/>.</summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>The number of matches, counting overlaps.</returns>
    /// <remarks>
    /// <c>O(n + matches)</c>, and nothing is materialized: the output chain is walked for its length rather
    /// than for its contents. The result is a <see cref="long"/> because overlapping occurrences each count
    /// and the total is <b>not</b> bounded by the text length — a thousand nested patterns over a few million
    /// characters is a practical input that exceeds <see cref="int.MaxValue"/> matches, and the count is the
    /// one number here that has to survive it. <see cref="FindAll"/> and <see cref="CopyMatches"/> cannot:
    /// one is bounded by the largest array and the other by the caller's buffer.
    /// </remarks>
    public long CountMatches(ReadOnlySpan<char> text)
    {
        long total = 0;
        int state = 0;
        for (int position = 0; position < text.Length; position++)
        {
            state = Step(state, text[position]);
            for (int node = FirstOutput(state); node >= 0; node = _outputLink[node])
                total++;
        }

        return total;
    }

    /// <summary>Finds the first match in <paramref name="text"/>.</summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="match">The first match in reporting order; the default when there is none.</param>
    /// <returns><c>true</c> when at least one pattern occurs.</returns>
    /// <remarks>
    /// "First" is <b>first in reporting order</b> — the match that <i>ends</i> earliest, and the longest of
    /// those if several end together. That is not necessarily the match that <i>starts</i> earliest: with the
    /// patterns <c>"bc"</c> and <c>"abcd"</c> over <c>"abcd"</c> this returns <c>"bc"</c> at position 1. A
    /// single left-to-right pass cannot do better — at the moment <c>"bc"</c> completes, the automaton has no
    /// way to know whether the longer pattern starting earlier will complete at all.
    /// </remarks>
    public bool TryFindFirst(ReadOnlySpan<char> text, out PatternMatch match)
    {
        int state = 0;
        for (int position = 0; position < text.Length; position++)
        {
            state = Step(state, text[position]);

            int node = FirstOutput(state);
            if (node >= 0)
            {
                match = MatchAt(node, position + 1);
                return true;
            }
        }

        match = default;
        return false;
    }

    /// <summary>
    /// Writes every match in <paramref name="text"/> into <paramref name="destination"/>, allocating nothing.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <param name="destinationIndex">The position in <paramref name="destination"/> to start writing at.</param>
    /// <returns>The number of matches written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="destinationIndex"/> is outside <c>[0, destination.Length]</c>.</exception>
    /// <remarks>
    /// Writing stops when the buffer is full — and so does the scan, since there is nothing left to record —
    /// so a return value equal to the remaining room may mean the matches were truncated. Size the buffer with
    /// <see cref="CountMatches"/> when every match is needed, or use <see cref="EnumerateMatches"/>, which
    /// needs no buffer at all.
    /// </remarks>
    public int CopyMatches(ReadOnlySpan<char> text, PatternMatch[] destination, int destinationIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if ((uint)destinationIndex > (uint)destination.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(destinationIndex), destinationIndex, "Destination index must be within [0, destination.Length].");
        }

        int room = destination.Length - destinationIndex;
        int written = 0;
        int state = 0;
        for (int position = 0; position < text.Length && written < room; position++)
        {
            state = Step(state, text[position]);
            for (int node = FirstOutput(state); node >= 0 && written < room; node = _outputLink[node])
                destination[destinationIndex + written++] = MatchAt(node, position + 1);
        }

        return written;
    }

    /// <summary>Returns every match in <paramref name="text"/>.</summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>The matches in reporting order, or an empty array when no pattern occurs.</returns>
    /// <remarks>
    /// The convenience tier: it allocates the result and grows it as it goes. Use
    /// <see cref="EnumerateMatches"/> or <see cref="CopyMatches"/> on a hot path.
    /// </remarks>
    public PatternMatch[] FindAll(ReadOnlySpan<char> text)
    {
        var found = new List<PatternMatch>();
        foreach (PatternMatch match in EnumerateMatches(text))
            found.Add(match);

        if (found.Count == 0)
            return [];

        return [.. found];
    }

    /// <summary>Enumerates every match in <paramref name="text"/> without allocating.</summary>
    /// <param name="text">The text to scan.</param>
    /// <returns>A <c>ref struct</c> enumerator over the matches, in reporting order.</returns>
    /// <remarks>
    /// This is the tier to reach for: the pass is driven by the enumerator, so a caller that stops early stops
    /// the scan, and nothing is allocated whether there is one match or a million. The enumerator holds the
    /// text as a span and so cannot outlive it or be captured in a lambda, iterator or field.
    /// </remarks>
    public MatchEnumerator EnumerateMatches(ReadOnlySpan<char> text) => new(this, text);

    /// <summary>Returns an enumerator over the patterns, in id order.</summary>
    /// <returns>A struct enumerator over the patterns.</returns>
    /// <remarks>
    /// Enumerating the automaton yields its <i>patterns</i>; <see cref="EnumerateMatches"/> is what yields
    /// matches against a text.
    /// </remarks>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // The state reached from `state` on `value`: the child edge if there is one, otherwise the same question
    // asked of the failure state, bottoming out at the root, which absorbs any character it has no edge for.
    // Amortized O(1) per character over a scan — each step descends at most one level and the failure walk can
    // only climb what descending paid for — plus the binary search through one state's edges.
    private int Step(int state, char value)
    {
        while (true)
        {
            if (state == 0)
            {
                if (value < RootTableSize)
                    return _rootTransitions[value];

                int rootEdge = FindEdge(0, value);
                return rootEdge >= 0 ? _childNodes[rootEdge] : 0;
            }

            int edge = FindEdge(state, value);
            if (edge >= 0)
                return _childNodes[edge];

            state = _fail[state];
        }
    }

    // Replaces a scratch buffer with a longer one from the pool, carrying the live prefix across and handing
    // the old one back.
    private static T[] Grow<T>(T[] buffer, int used, int minimum)
    {
        T[] bigger = ArrayPool<T>.Shared.Rent(minimum);
        buffer.AsSpan(0, used).CopyTo(bigger);
        ArrayPool<T>.Shared.Return(buffer);
        return bigger;
    }

    // The index of `state`'s edge on `value` within the edge arrays, or -1. The edges of one state are sorted
    // by character, which is what the flattening arranges and what makes this a binary search.
    private int FindEdge(int state, char value)
    {
        int low = _childStart[state];
        int high = _childStart[state + 1] - 1;
        while (low <= high)
        {
            int mid = (int)(((uint)low + (uint)high) >> 1);
            char edge = _childChars[mid];
            if (edge == value)
                return mid;

            if (edge < value)
                low = mid + 1;
            else
                high = mid - 1;
        }

        return -1;
    }

    // The first state of the output chain for `state`: itself when a pattern ends there, otherwise the nearest
    // one up the failure chain that has an output. -1 when nothing matches here.
    private int FirstOutput(int state) => _output[state] >= 0 ? state : _outputLink[state];

    // The match recorded by output state `node` for a pass that has just consumed through `end`. The length is
    // the pattern's, because the automaton matches literally.
    private PatternMatch MatchAt(int node, int end)
    {
        int id = _output[node];
        int length = _patterns[id].Length;
        return new PatternMatch(id, end - length, length);
    }

    // Walks the scratch trie breadth-first, writing the compressed row layout and the failure and output links
    // as it goes. Breadth-first is what makes the single pass possible: a state's failure target has strictly
    // smaller depth and therefore a smaller id, so it is already written by the time this needs it — and it
    // also lays the states out roughly in the order a scan touches them.
    private void Flatten(int stateCount, char[] edgeChars, int[] firstChild, int[] nextSibling, int[] terminal)
    {
        int[] sourceBuffer = ArrayPool<int>.Shared.Rent(stateCount);
        try
        {
            // sources[newId] is the scratch-trie state that new id came from; the array doubles as the
            // breadth-first queue, since ids are handed out in the order states are enqueued.
            Span<int> sources = sourceBuffer.AsSpan(0, stateCount);
            sources[0] = 0;

            _fail[0] = 0;
            _output[0] = -1;
            _outputLink[0] = -1;

            int assigned = 1;
            int edges = 0;

            for (int state = 0; state < stateCount; state++)
            {
                int source = sources[state];
                _childStart[state] = edges;

                int first = edges;
                for (int child = firstChild[source]; child >= 0; child = nextSibling[child])
                {
                    _childChars[edges] = edgeChars[child];
                    _childNodes[edges] = child;
                    edges++;
                }

                // Sorted by edge character so the transition can binary-search. A paired span sort rather than
                // an insertion sort: a state's branching factor is small in every realistic pattern set, but
                // it is bounded only by the UTF-16 alphabet, and a root row tens of thousands of edges wide
                // would make an insertion sort the most expensive thing in the build.
                _childChars.AsSpan(first, edges - first).Sort(_childNodes.AsSpan(first, edges - first));

                // Ids are handed out in sorted order, so a state's children are contiguous and ascending.
                for (int i = first; i < edges; i++)
                {
                    int child = assigned++;
                    sources[child] = _childNodes[i];
                    _childNodes[i] = child;
                }

                // The row has to be closed before the failure walk below, which reads the edges of states it
                // reaches through Step and so needs their end offsets in place.
                _childStart[state + 1] = edges;

                // The root is flattened first, and every failure walk below runs through Step, which consults
                // this table — so it has to be filled before the first one, which is on the next state.
                if (state == 0)
                {
                    for (int i = first; i < edges; i++)
                    {
                        if (_childChars[i] < RootTableSize)
                            _rootTransitions[_childChars[i]] = _childNodes[i];
                    }
                }

                for (int i = first; i < edges; i++)
                {
                    int child = _childNodes[i];
                    _output[child] = terminal[sources[child]];

                    // The root's children fail to the root: asking Step for the root's own transition would
                    // find the child itself and make the link a self-loop.
                    int failure = state == 0 ? 0 : Step(_fail[state], _childChars[i]);
                    _fail[child] = failure;
                    _outputLink[child] = _output[failure] >= 0 ? failure : _outputLink[failure];
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(sourceBuffer);
        }
    }

    /// <summary>Enumerates the patterns of an <see cref="AhoCorasick"/> in id order.</summary>
    /// <remarks>
    /// The automaton is immutable, so there is no version check here and no way for an enumerator to be
    /// invalidated.
    /// </remarks>
    public struct Enumerator : IEnumerator<string>
    {
        private readonly string[] _patterns;
        private int _index;
        private string _current;

        internal Enumerator(AhoCorasick automaton)
        {
            _patterns = automaton._patterns;
            _index = 0;
            _current = string.Empty;
        }

        /// <summary>Gets the pattern at the current id.</summary>
        public readonly string Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances to the next pattern.</summary>
        /// <returns><c>true</c> when a pattern was produced; <c>false</c> at the end of the set.</returns>
        public bool MoveNext()
        {
            if (_index >= _patterns.Length)
                return false;

            _current = _patterns[_index++];
            return true;
        }

        /// <summary>Resets the enumerator to before the first pattern.</summary>
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

    /// <summary>
    /// Enumerates the matches of an <see cref="AhoCorasick"/> against a text, driving the scan as it goes.
    /// </summary>
    /// <remarks>
    /// A <c>ref struct</c>, because it holds the text as a <see cref="ReadOnlySpan{T}"/> rather than copying
    /// it. Nothing is allocated and nothing is buffered: the enumerator carries the automaton state and the
    /// position of the output chain it is part way through, so stopping early stops the scan.
    /// </remarks>
    public ref struct MatchEnumerator
    {
        private readonly AhoCorasick _automaton;
        private readonly ReadOnlySpan<char> _text;
        private int _position;
        private int _state;
        private int _outputNode;
        private PatternMatch _current;

        internal MatchEnumerator(AhoCorasick automaton, ReadOnlySpan<char> text)
        {
            _automaton = automaton;
            _text = text;
            _position = 0;
            _state = 0;
            _outputNode = -1;
            _current = default;
        }

        /// <summary>Gets the match the enumerator is positioned on.</summary>
        public readonly PatternMatch Current => _current;

        /// <summary>Returns this enumerator, so that it can be used directly in a <c>foreach</c>.</summary>
        /// <returns>This enumerator.</returns>
        public readonly MatchEnumerator GetEnumerator() => this;

        /// <summary>Advances to the next match, consuming as much of the text as that takes.</summary>
        /// <returns><c>true</c> when a match was produced; <c>false</c> at the end of the text.</returns>
        public bool MoveNext()
        {
            while (true)
            {
                // Several patterns can end at the same position, so the chain from the current state is drained
                // before the text advances again — longest first, which is the order it is linked in.
                if (_outputNode >= 0)
                {
                    _current = _automaton.MatchAt(_outputNode, _position);
                    _outputNode = _automaton._outputLink[_outputNode];
                    return true;
                }

                if (_position >= _text.Length)
                    return false;

                _state = _automaton.Step(_state, _text[_position]);
                _position++;
                _outputNode = _automaton.FirstOutput(_state);
            }
        }
    }
}
