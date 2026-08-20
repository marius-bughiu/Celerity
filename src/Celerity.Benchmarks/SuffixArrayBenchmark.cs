using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// SuffixArray vs the two things a caller reaches for instead. There is no BCL text index of any kind, so the
// honest baselines are the scan the BCL does ship and the inverted index a caller writes when they notice the
// text is fixed. The baseline arms are named String_* and Dictionary_* so the dashboard classifies them as the
// reference series.
//
// Two baselines, because measuring only the scan would flatter the type past the point of being useful.
// String_* is MemoryExtensions.IndexOf — vectorized, allocation-free and genuinely fast, but O(n) per query
// however many queries follow, which is the whole asymmetry this type exists to exploit. Dictionary_* is the
// smarter hand-roll: an inverted index of every k-gram of the text into a Dictionary<string, int[]>, which
// answers a k-length pattern with one hash lookup and is expected to WIN the query arms. What it cannot do is
// answer for any other pattern length, and it is measured on build cost and (through the allocation column) on
// footprint alongside, which is where it pays for that.
//
// Contains is the arm the type is strongest on and the one to read first: the patterns are ABSENT, so the scan
// has to read the entire text to say so while the index pays log n. Count is the same shape with patterns that
// are present and repeated, where the scan additionally has to restart from every hit. Occurrences retrieves
// every position into a caller-owned buffer on both sides, so neither is charged for an allocation the other
// avoids.
//
// CountIndexed is the arm against the k-gram index rather than the scan, at the one pattern length that index
// can answer. It is the arm to judge this type by on query cost alone: if the suffix array were only being
// compared against re-scanning, the comparison would be against a strawman a competent caller would not write.
//
// Build is the price both indexes charge before answering anything, and it is why the crossover matters: a
// single query against a text read once is a loss here, and the query arms only start paying it back at some
// number of queries the build arm is what determines. Both sides index the same text.
//
// The text is generated from a small vocabulary with spaces, which is the shape (a modest alphabet, many
// repeated substrings, a heavy skew toward short tokens) that real logs, documents and source files have, and
// the shape that makes the suffix order interesting rather than settled by the first character. The queries
// are drawn from that text so they are present at realistic frequencies rather than at one position each.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SuffixArrayBenchmark
{
    // The k-gram index answers exactly this pattern length and nothing else, which is the trade the
    // CountIndexed arm is there to price.
    private const int GramLength = 8;

    private const int QueryCount = 16;

    private string text = null!;
    private SuffixArray index = null!;
    private Dictionary<string, int[]> grams = null!;

    private string[] present = null!;
    private string[] absent = null!;
    private string[] gramQueries = null!;
    private int[] positions = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        text = BuildText(rand, ItemCount);
        index = new SuffixArray(text);
        grams = BuildGramIndex(text);

        // Present queries are substrings of the text at a spread of lengths, so the arms measure a mix of
        // patterns that occur once and patterns that occur many times.
        present = new string[QueryCount];
        gramQueries = new string[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            int length = 3 + rand.Next(10);
            present[i] = text.Substring(rand.Next(text.Length - length), length);
            gramQueries[i] = text.Substring(rand.Next(text.Length - GramLength), GramLength);
        }

        // Absent queries are the same shape but end in a character the vocabulary never produces, so the scan
        // has to read the whole text to rule each one out rather than bailing at the first character.
        absent = new string[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            int length = 3 + rand.Next(10);
            absent[i] = string.Concat(text.AsSpan(rand.Next(text.Length - length), length - 1), "#");
        }

        int widest = 0;
        foreach (string pattern in present)
            widest = Math.Max(widest, index.CountOccurrences(pattern));

        positions = new int[Math.Max(widest, 1)];
    }

    // ---- Contains: the absent pattern, which the scan cannot rule out without reading everything ----

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
    public int SuffixArray_Contains()
    {
        int found = 0;
        foreach (string pattern in absent)
        {
            if (index.Contains(pattern))
                found++;
        }

        return found;
    }

    // ---- Count: every occurrence of a present pattern, which the scan has to restart from each hit ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Count")]
    public int String_Count()
    {
        int total = 0;
        foreach (string pattern in present)
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

    [Benchmark]
    [BenchmarkCategory("Count")]
    public int SuffixArray_Count()
    {
        int total = 0;
        foreach (string pattern in present)
            total += index.CountOccurrences(pattern);

        return total;
    }

    // ---- CountIndexed: against the k-gram inverted index, at the length it can answer ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CountIndexed")]
    public int Dictionary_CountIndexed()
    {
        int total = 0;
        foreach (string pattern in gramQueries)
        {
            if (grams.TryGetValue(pattern, out int[]? found))
                total += found.Length;
        }

        return total;
    }

    [Benchmark]
    [BenchmarkCategory("CountIndexed")]
    public int SuffixArray_CountIndexed()
    {
        int total = 0;
        foreach (string pattern in gramQueries)
            total += index.CountOccurrences(pattern);

        return total;
    }

    // ---- Occurrences: every position into a buffer the caller already owns, on both sides ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Occurrences")]
    public int String_Occurrences()
    {
        int total = 0;
        foreach (string pattern in present)
        {
            int written = 0;
            int offset = 0;
            while (offset <= text.Length - pattern.Length)
            {
                int hit = text.IndexOf(pattern, offset, StringComparison.Ordinal);
                if (hit < 0)
                    break;

                positions[written++] = hit;
                offset = hit + 1;
            }

            total += written;
        }

        return total;
    }

    [Benchmark]
    [BenchmarkCategory("Occurrences")]
    public int SuffixArray_Occurrences()
    {
        int total = 0;
        foreach (string pattern in present)
            total += index.CopyOccurrences(pattern, positions);

        return total;
    }

    // ---- Build: the price both indexes charge before answering anything ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public Dictionary<string, int[]> Dictionary_Build() => BuildGramIndex(text);

    [Benchmark]
    [BenchmarkCategory("Build")]
    public SuffixArray SuffixArray_Build() => new(text);

    // The inverted index a caller writes instead: every k-gram of the text mapped to the positions it starts
    // at. Built through a List<int> per gram and frozen to an int[], which is what makes the lookup arm a
    // single hash probe and one array length rather than a list walk.
    private static Dictionary<string, int[]> BuildGramIndex(string source)
    {
        var pending = new Dictionary<string, List<int>>(source.Length);
        for (int position = 0; position + GramLength <= source.Length; position++)
        {
            string gram = source.Substring(position, GramLength);
            if (!pending.TryGetValue(gram, out List<int>? found))
            {
                found = [];
                pending[gram] = found;
            }

            found.Add(position);
        }

        var built = new Dictionary<string, int[]>(pending.Count);
        foreach (KeyValuePair<string, List<int>> entry in pending)
            built[entry.Key] = [.. entry.Value];

        return built;
    }

    // A modest vocabulary joined by spaces: many repeated substrings over a small alphabet, which is the shape
    // of the text this type is for and the one that makes the suffix order non-trivial.
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

        var builder = new System.Text.StringBuilder(length + 16);
        while (builder.Length < length)
        {
            builder.Append(vocabulary[rand.Next(vocabulary.Length)]);
            builder.Append(' ');
        }

        return builder.ToString(0, length);
    }
}
