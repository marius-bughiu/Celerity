using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// AhoCorasick vs the two things a caller reaches for when they have many needles and one haystack. The BCL has
// no multi-pattern matcher, so the honest baselines are the loop of single-needle scans and the regular
// expression alternation. The baseline arms are named String_* and Regex_* so the dashboard classifies them as
// the reference series.
//
// Two baselines, because either one alone would be a strawman. String_* is the k-IndexOf loop — the obvious
// hand-roll, and a genuinely fast one, because MemoryExtensions.IndexOf is vectorized while this pass reads a
// character at a time. That is why the pattern count is an axis here rather than a constant: the loop is
// O(k · n) and this is O(n), so at eight patterns the loop is expected to WIN and at 256 it is not, and both
// arms are published. Regex_* is what a caller writes when they get tired of the loop: one compiled
// alternation of every pattern, which .NET turns into its own literal prefilter and which is the strongest
// thing in the box.
//
// The pattern count is carried in the arm names rather than in [Params], because the dashboard sweeps a single
// ItemCount axis and the text length is the one that belongs on it. ItemCount is the text length, matching
// SuffixArrayBenchmark, and *Few is the eight-pattern variant of the same workload.
//
// Contains is the arm to read first, and its patterns are ABSENT: the loop then has to read the whole text
// once per pattern, which is the shape this type exists to collapse into one pass. Count is the same asymmetry
// with patterns that are present and repeated, where the loop additionally restarts from every hit. CountFew
// is the crossover arm and is expected to be a loss.
//
// FindAll compares against the alternation on a workload the loop cannot express at all — every occurrence of
// every pattern, with which pattern it was. Read it knowing the two sides do NOT report the same set: a Regex
// alternation consumes the text as it matches, so it reports leftmost non-overlapping matches, while this
// reports every occurrence including the ones that start inside another. That is a capability difference and
// not a measurement trick, and it means the Celerity arm is doing strictly more work — which is worth knowing
// when reading the ratio.
//
// Build is the price both sides charge before answering anything, and the Regex arm deliberately does NOT pass
// RegexOptions.Compiled there: compiling emits IL and would lose by a margin that says nothing about either
// structure. The query arms use the compiled form, which is the one a caller keeps around.
//
// The text is generated from a small vocabulary joined by spaces — a modest alphabet with many repeated
// substrings, which is what logs, documents and source files look like — and the present patterns are drawn
// from it, so they occur at realistic frequencies rather than once each.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class AhoCorasickBenchmark
{
    private const int ManyPatterns = 256;
    private const int FewPatterns = 8;

    private string text = null!;

    private string[] present = null!;
    private string[] absent = null!;
    private string[] few = null!;

    private AhoCorasick presentAutomaton = null!;
    private AhoCorasick absentAutomaton = null!;
    private AhoCorasick fewAutomaton = null!;

    private Regex presentRegex = null!;
    private Regex absentRegex = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        text = BuildText(rand, ItemCount);

        // Present patterns are substrings of the text at a spread of lengths, deduplicated so that both sides
        // are charged for the same number of distinct needles — the automaton collapses duplicates and the loop
        // would not.
        present = Distinct(rand, ManyPatterns, length => text.Substring(rand.Next(text.Length - length), length));

        // Absent patterns are the same shape but end in a character the vocabulary never produces, so the loop
        // has to read the whole text to rule each one out rather than bailing early.
        absent = Distinct(rand, ManyPatterns, length =>
            string.Concat(text.AsSpan(rand.Next(text.Length - length), length - 1), "#"));

        few = present[..FewPatterns];

        presentAutomaton = new AhoCorasick(present);
        absentAutomaton = new AhoCorasick(absent);
        fewAutomaton = new AhoCorasick(few);

        presentRegex = new Regex(Alternation(present), RegexOptions.Compiled | RegexOptions.CultureInvariant);
        absentRegex = new Regex(Alternation(absent), RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    // ---- Contains: absent patterns, which the loop cannot rule out without reading everything, k times ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public int String_Contains()
    {
        int found = 0;
        foreach (string pattern in absent)
        {
            if (text.Contains(pattern, StringComparison.Ordinal))
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool AhoCorasick_Contains() => absentAutomaton.ContainsAny(text);

    // ---- ContainsRegex: the same question asked of one compiled alternation ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ContainsRegex")]
    public bool Regex_ContainsRegex() => absentRegex.IsMatch(text);

    [Benchmark]
    [BenchmarkCategory("ContainsRegex")]
    public bool AhoCorasick_ContainsRegex() => absentAutomaton.ContainsAny(text);

    // ---- Count: every occurrence of every present pattern, which the loop restarts from each hit ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Count")]
    public int String_Count() => CountByScanning(present);

    [Benchmark]
    [BenchmarkCategory("Count")]
    public int AhoCorasick_Count() => presentAutomaton.CountMatches(text);

    // ---- CountFew: the same workload at eight patterns, where the vectorized loop is expected to win ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CountFew")]
    public int String_CountFew() => CountByScanning(few);

    [Benchmark]
    [BenchmarkCategory("CountFew")]
    public int AhoCorasick_CountFew() => fewAutomaton.CountMatches(text);

    // ---- FindAll: every match with which pattern it was, which the k-IndexOf loop cannot express ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FindAll")]
    public int Regex_FindAll()
    {
        int total = 0;
        foreach (Match match in presentRegex.Matches(text))
            total += match.Length;

        return total;
    }

    [Benchmark]
    [BenchmarkCategory("FindAll")]
    public int AhoCorasick_FindAll()
    {
        int total = 0;
        foreach (PatternMatch match in presentAutomaton.EnumerateMatches(text))
            total += match.Length;

        return total;
    }

    // ---- Build: the price both sides charge before answering anything ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public Regex Regex_Build() => new(Alternation(present), RegexOptions.CultureInvariant);

    [Benchmark]
    [BenchmarkCategory("Build")]
    public AhoCorasick AhoCorasick_Build() => new(present);

    // The loop a caller writes: one vectorized scan per pattern, restarted from the character after each hit so
    // that overlapping occurrences of the same pattern are counted the way the automaton counts them.
    private int CountByScanning(string[] patterns)
    {
        int total = 0;
        foreach (string pattern in patterns)
        {
            ReadOnlySpan<char> remaining = text;
            int offset;
            while ((offset = remaining.IndexOf(pattern)) >= 0)
            {
                total++;
                remaining = remaining[(offset + 1)..];
            }
        }

        return total;
    }

    // One alternation of every pattern. The patterns are alphabetic, but escaping them is what makes the
    // baseline the one a caller would actually write.
    private static string Alternation(string[] patterns)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < patterns.Length; i++)
        {
            if (i > 0)
                builder.Append('|');

            builder.Append(Regex.Escape(patterns[i]));
        }

        return builder.ToString();
    }

    // `count` distinct patterns at a spread of lengths. Distinct matters: the automaton collapses a repeated
    // pattern and the loop would scan for it twice, so a set with duplicates would flatter one side.
    private static string[] Distinct(Random rand, int count, Func<int, string> generate)
    {
        var chosen = new List<string>(count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (chosen.Count < count)
        {
            string candidate = generate(3 + rand.Next(10));
            if (seen.Add(candidate))
                chosen.Add(candidate);
        }

        return [.. chosen];
    }

    // A modest vocabulary joined by spaces: many repeated substrings over a small alphabet, which is the shape
    // of the text a multi-pattern scan is pointed at.
    private static string BuildText(Random rand, int length)
    {
        string[] vocabulary = new string[64];
        for (int i = 0; i < vocabulary.Length; i++)
        {
            int wordLength = 3 + rand.Next(7);
            char[] word = new char[wordLength];
            for (int c = 0; c < wordLength; c++)
                word[c] = (char)('a' + rand.Next(20));

            vocabulary[i] = new string(word);
        }

        var builder = new StringBuilder(length + 16);
        while (builder.Length < length)
        {
            builder.Append(vocabulary[rand.Next(vocabulary.Length)]);
            builder.Append(' ');
        }

        return builder.ToString(0, length);
    }
}
