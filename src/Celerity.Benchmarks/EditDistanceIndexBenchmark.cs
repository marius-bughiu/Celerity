using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// EditDistanceIndex vs the only thing a caller can write instead. The BCL has no approximate string lookup at
// all, so the baseline is a linear scan over a string[] that computes a bounded Levenshtein distance to every
// word. The baseline arms are named Array_* so the dashboard classifies them as the reference series.
//
// The scan is the careful one, not a strawman: it skips every word whose length differs from the query's by
// more than k without touching its characters, and its distance is the SAME bounded, row-early-exit algorithm
// the index verifies candidates with — BoundedDistance below is a copy of EditDistanceIndex.BoundedDistance,
// which is private. So the two sides pay an identical price per distance computed, and the ratio isolates the
// one thing the index changes: how many words get a distance computed at all. The scan computes one for every
// word of a plausible length; the index computes one for each word that shares a deletion variant with the
// query, after generating and looking up that query's C(m, 0) + ... + C(m, k) variants.
//
// ItemCount is the number of distinct words. The words are synthetic, generated from a fixed seed: a stem of
// three to nine letters drawn with English letter frequencies, inflected into a family of one to four words
// with endings like -s, -ed, -ing and -er. Families matter. Independent random strings have almost no close
// neighbours, so every query would find only its source word and the candidate lists the index verifies would
// be unrealistically short, which flatters it. A family puts real words one to three edits apart, as plurals
// and tenses do in a dictionary, and that is what the verification cost is made of. The 64 queries are
// words of the set with ONE random substitution each — the spell-check shape: most have the original word at
// distance 1 and a few neighbours further out, and some substitutions land on another word of the set.
//
// FindWithin1 and FindWithin2 are the arms the pre-registered kill criteria (issue #584) are read off. Both
// sides produce the same thing — every match as a FuzzyMatch, ordered by distance and then id, as an array per
// query — so the Celerity arm calls the allocating FindWithin it is named for, and the baseline collects into
// a List, sorts it with the same order and copies it out. A query has a handful of matches, so the output work
// is noise against either side's search; it is matched rather than dropped so the allocation column compares
// like with like. FindWithin1 queries an index built for k = 1, which is what a caller who only ever asks for
// one edit builds; FindWithin2 and Nearest query one built for k = 2.
//
// Nearest is the did-you-mean shape: the single closest word within 2 edits, lowest id among ties. Both sides
// tighten the bound to one below the best distance found so far, and both stop at an exact match.
//
// Build carries no bar and is NOT a win: the scan has no build, so the baseline is one scan pass for a single
// query at k = 2, and the ratio reads as the number of scan queries the build costs — the break-even point
// before the index has paid for itself. Its allocation column is the index's footprint plus the build's
// transient deduplication set and posting array; PostingCount is the size it is proportional to. The query
// arms use indexes built once in GlobalSetup and never pay for it.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class EditDistanceIndexBenchmark
{
    private const int QueryCount = 64;

    // Matches EditDistanceIndex's own stack limit, so the copied verifier rents at the same point.
    private const int StackLimit = 256;

    // English letter frequencies, in tenths of a percent: a skewed alphabet makes the words share deletion
    // variants the way dictionary words do, which is the case the index's candidate lists are longest on.
    private static readonly int[] LetterWeights =
    [
        82, 15, 28, 43, 127, 22, 20, 61, 70, 2, 8, 40, 24,
        67, 75, 19, 1, 60, 63, 91, 28, 10, 24, 2, 20, 1,
    ];

    // The endings a stem is inflected with; the empty one keeps the bare stem.
    private static readonly string[] Endings = ["", "s", "es", "ed", "ing", "er", "ers", "ly", "ness"];

    private string[] words = null!;
    private string[] queries = null!;

    private EditDistanceIndex index1 = null!;
    private EditDistanceIndex index2 = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        int totalWeight = 0;
        foreach (int weight in LetterWeights)
            totalWeight += weight;

        var chosen = new List<string>(ItemCount);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (chosen.Count < ItemCount)
        {
            // Two dice summed: stems of 3 to 9 letters, peaking at 6.
            int length = 3 + rand.Next(4) + rand.Next(4);
            var stem = new char[length];
            for (int i = 0; i < length; i++)
                stem[i] = Letter(rand, totalWeight);

            int family = 1 + rand.Next(4);
            for (int member = 0; member < family && chosen.Count < ItemCount; member++)
            {
                string candidate = string.Concat(new string(stem), Endings[rand.Next(Endings.Length)]);
                if (seen.Add(candidate))
                    chosen.Add(candidate);
            }
        }

        words = [.. chosen];

        queries = new string[QueryCount];
        for (int q = 0; q < QueryCount; q++)
        {
            char[] query = words[rand.Next(words.Length)].ToCharArray();
            int position = rand.Next(query.Length);
            char replacement;
            do
            {
                replacement = Letter(rand, totalWeight);
            }
            while (replacement == query[position]);

            query[position] = replacement;
            queries[q] = new string(query);
        }

        index1 = new EditDistanceIndex(words, maxDistance: 1);
        index2 = new EditDistanceIndex(words, maxDistance: 2);
    }

    // ---- FindWithin1: every word within one edit, closest first ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FindWithin1")]
    public int Array_FindWithin1() => ScanAll(1);

    [Benchmark]
    [BenchmarkCategory("FindWithin1")]
    public int EditDistanceIndex_FindWithin1() => FindAll(index1, 1);

    // ---- FindWithin2: every word within two edits, closest first ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FindWithin2")]
    public int Array_FindWithin2() => ScanAll(2);

    [Benchmark]
    [BenchmarkCategory("FindWithin2")]
    public int EditDistanceIndex_FindWithin2() => FindAll(index2, 2);

    // ---- Nearest: the single closest word within two edits, lowest id among ties ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Nearest")]
    public int Array_Nearest()
    {
        int total = 0;
        foreach (string query in queries)
        {
            int bound = 2;
            int bestId = -1;
            for (int id = 0; id < words.Length; id++)
            {
                string word = words[id];
                if (Math.Abs(word.Length - query.Length) > bound)
                    continue;

                int distance = BoundedDistance(query, word, bound);
                if (distance > bound)
                    continue;

                bestId = id;
                if (distance == 0)
                    break;

                bound = distance - 1;
            }

            if (bestId >= 0)
                total += bestId + 1;
        }

        return total;
    }

    [Benchmark]
    [BenchmarkCategory("Nearest")]
    public int EditDistanceIndex_Nearest()
    {
        int total = 0;
        foreach (string query in queries)
        {
            if (index2.TryFindNearest(query, 2, out FuzzyMatch match))
                total += match.WordId + 1;
        }

        return total;
    }

    // ---- Build: the price of the index, against one scan query it has to outlast ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public int Array_Build() => Scan(queries[0], 2).Length;

    [Benchmark]
    [BenchmarkCategory("Build")]
    public EditDistanceIndex EditDistanceIndex_Build() => new(words, maxDistance: 2);

    private int FindAll(EditDistanceIndex index, int maxDistance)
    {
        int total = 0;
        foreach (string query in queries)
            total += index.FindWithin(query, maxDistance).Length;

        return total;
    }

    private int ScanAll(int maxDistance)
    {
        int total = 0;
        foreach (string query in queries)
            total += Scan(query, maxDistance).Length;

        return total;
    }

    // The careful hand-roll: a length filter before any character is read, then a bounded distance that stops
    // a row early, with the matches ordered the way FindWithin orders them.
    private FuzzyMatch[] Scan(string query, int maxDistance)
    {
        var found = new List<FuzzyMatch>();
        for (int id = 0; id < words.Length; id++)
        {
            string word = words[id];
            if (Math.Abs(word.Length - query.Length) > maxDistance)
                continue;

            int distance = BoundedDistance(query, word, maxDistance);
            if (distance <= maxDistance)
                found.Add(new FuzzyMatch(id, distance));
        }

        found.Sort(static (left, right) => left.Distance != right.Distance
            ? left.Distance.CompareTo(right.Distance)
            : left.WordId.CompareTo(right.WordId));

        return [.. found];
    }

    private static char Letter(Random rand, int totalWeight)
    {
        int roll = rand.Next(totalWeight);
        for (int letter = 0; letter < LetterWeights.Length; letter++)
        {
            roll -= LetterWeights[letter];
            if (roll < 0)
                return (char)('a' + letter);
        }

        return 'z';
    }

    // A verbatim copy of EditDistanceIndex.BoundedDistance (private to the library), so the scan verifies with
    // exactly the algorithm the index does: the exact distance when it is at most `bound`, otherwise bound + 1.
    private static int BoundedDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b, int bound)
    {
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
}
