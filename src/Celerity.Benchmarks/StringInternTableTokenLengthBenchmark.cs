using BenchmarkDotNet.Attributes;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// How <see cref="StringInternTable"/>'s probe compares with <c>HashSet&lt;string&gt;</c>'s span lookup as the
/// tokens get longer: the length sweep behind the token-length claims in its documentation.
/// </summary>
/// <remarks>
/// <para>
/// The workload is <see cref="StringInternTableBenchmark"/>'s <c>Lookup</c> category, probing warm tables with
/// spans sliced from one buffer. Tokens are fixed at 3 to 64 characters, or drawn log-uniformly from 2–20 or 2–48
/// characters (averaging about 8 and 14), as a parse that mixes short codes, words and identifiers would see them.
/// </para>
/// <para>
/// Each invocation probes sixteen tables, each with its own vocabulary of 64 random tokens and so half full, 6,250
/// times apiece in a random order. Successive invocations take the next of sixteen such draws, so a run averages
/// 256 vocabularies and no invocation repeats the order the one before it probed.
/// </para>
/// <para>
/// Both are needed for a fair comparison. Which of 64 tokens happen to share a slot is luck of the draw: a draw of
/// sixteen vocabularies gave one hasher's tables up to 12% fewer probes per hit than another's, and on short tokens
/// that moved the time ratio by two to three times as much. Over all 256, the three hashers' mean probes per hit came
/// out within about 2% of each other at every length. And a branch predictor learns a probe order that repeats:
/// replaying one draw of 100,000 probes on every invocation let it learn which probes collide, which on the Apple M4
/// took a quarter or more off the tables' probe time and much less off the set's, overstating the tables' lead. The
/// sixteen streams, 1.6 million probes in all, are more than it learns. The tables are half full; nearer their 0.75
/// load factor they visit more slots per hit, and that is not measured here.
/// </para>
/// <para>
/// The arms are the default <see cref="StringCityHash64Hasher"/>, <see cref="StringXxHash3Hasher"/> (the other
/// block hasher compared) and <see cref="StringFnV1AFullHasher"/> (the default before 4.0). Tokens are ASCII: none
/// of these hashers branches on content, so text that varies both bytes of each UTF-16 code unit costs the same to
/// hash. The buffer holds every draw's stream back to back, about 200 MB at 64 characters.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
public class StringInternTableTokenLengthBenchmark
{
    private const int Draws = 16;
    private const int Vocabularies = 16;
    private const int Tables = Draws * Vocabularies;
    private const int DistinctTokens = 64;
    private const int ProbesPerVocabulary = 6_250;
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789_";

    // One buffer holding every stream back to back; table t's stream is the slice
    // [t * ProbesPerVocabulary, (t + 1) * ProbesPerVocabulary) of the token list, and draw d is tables
    // [d * Vocabularies, (d + 1) * Vocabularies).
    private char[] buffer = null!;
    private (int Offset, int Length)[] stream = null!;

    private HashSet<string>.AlternateLookup<ReadOnlySpan<char>>[] sets = null!;
    private StringInternTable[] defaultTables = null!;
    private StringInternTable<StringXxHash3Hasher>[] xxHash3Tables = null!;
    private StringInternTable<StringFnV1AFullHasher>[] fnv1aFullTables = null!;

    // The draw the next invocation probes; every arm cycles through all sixteen.
    private int draw;

    /// <summary>A fixed token length in characters, or a <c>min-max</c> range drawn log-uniformly.</summary>
    [Params("3", "8", "16", "32", "64", "2-20", "2-48")]
    public string Tokens = null!;

    [GlobalSetup]
    public void Setup()
    {
        (int min, int max) = Tokens.Split('-') is [string lo, string hi]
            ? (int.Parse(lo), int.Parse(hi))
            : (int.Parse(Tokens), int.Parse(Tokens));

        var rand = new Random(42);
        var vocabularies = new string[Tables][];
        var probed = new string[Tables * ProbesPerVocabulary];
        long characters = 0;

        for (int t = 0; t < Tables; t++)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string[] vocabulary = vocabularies[t] = new string[DistinctTokens];
            for (int n = 0; n < DistinctTokens;)
            {
                int length = min == max
                    ? min
                    : (int)Math.Round(Math.Exp(Math.Log(min) + rand.NextDouble() * (Math.Log(max) - Math.Log(min))));
                var chars = new char[length];
                for (int i = 0; i < length; i++)
                    chars[i] = Alphabet[rand.Next(Alphabet.Length)];

                string token = new(chars);
                if (seen.Add(token))
                    vocabulary[n++] = token;
            }

            for (int i = 0; i < ProbesPerVocabulary; i++)
            {
                // Every token in turn first, so each is guaranteed to occur; random draws after that.
                string token = vocabulary[i < DistinctTokens ? i : rand.Next(DistinctTokens)];
                probed[t * ProbesPerVocabulary + i] = token;
                characters += token.Length + 1;
            }
        }

        // Written straight into the buffer: building ~100M characters through a StringBuilder would hold three
        // copies of them at once.
        buffer = new char[checked((int)characters)];
        stream = new (int, int)[probed.Length];
        int at = 0;
        for (int i = 0; i < probed.Length; i++)
        {
            string token = probed[i];
            token.CopyTo(buffer.AsSpan(at));
            stream[i] = (at, token.Length);
            at += token.Length;
            buffer[at++] = ',';
        }

        sets = new HashSet<string>.AlternateLookup<ReadOnlySpan<char>>[Tables];
        defaultTables = new StringInternTable[Tables];
        xxHash3Tables = new StringInternTable<StringXxHash3Hasher>[Tables];
        fnv1aFullTables = new StringInternTable<StringFnV1AFullHasher>[Tables];
        for (int t = 0; t < Tables; t++)
        {
            var set = new HashSet<string>(DistinctTokens, StringComparer.Ordinal);
            defaultTables[t] = new StringInternTable(DistinctTokens);
            xxHash3Tables[t] = new StringInternTable<StringXxHash3Hasher>(DistinctTokens);
            fnv1aFullTables[t] = new StringInternTable<StringFnV1AFullHasher>(DistinctTokens);
            foreach (string token in vocabularies[t])
            {
                set.Add(token);
                defaultTables[t].GetOrAdd(token);
                xxHash3Tables[t].GetOrAdd(token);
                fnv1aFullTables[t].GetOrAdd(token);
            }

            sets[t] = set.GetAlternateLookup<ReadOnlySpan<char>>();
        }
    }

    [Benchmark(Baseline = true)]
    public int HashSet_Lookup()
    {
        int d = draw;
        draw = (d + 1) % Draws;
        char[] buf = buffer;
        (int Offset, int Length)[] s = stream;
        HashSet<string>.AlternateLookup<ReadOnlySpan<char>>[] lookups = sets;
        int hits = 0;
        for (int t = d * Vocabularies; t < (d + 1) * Vocabularies; t++)
        {
            HashSet<string>.AlternateLookup<ReadOnlySpan<char>> lookup = lookups[t];
            int end = (t + 1) * ProbesPerVocabulary;
            for (int i = t * ProbesPerVocabulary; i < end; i++)
            {
                (int offset, int length) = s[i];
                if (lookup.Contains(buf.AsSpan(offset, length)))
                    hits++;
            }
        }

        return hits;
    }

    // Each arm keeps its probe loop inline, as HashSet_Lookup does, rather than calling one shared generic helper. A
    // one-line arm that calls a helper is a tiny inline root, so the JIT inlines the helper and the hasher under it to
    // a different depth per instantiation. In this benchmark that shape slowed StringXxHash3Hasher's arm by up to about 2x
    // and the default's by up to about 1.6x.
    [Benchmark]
    public int StringInternTable_Lookup()
    {
        int d = draw;
        draw = (d + 1) % Draws;
        char[] buf = buffer;
        (int Offset, int Length)[] s = stream;
        StringInternTable[] tables = defaultTables;
        int hits = 0;
        for (int t = d * Vocabularies; t < (d + 1) * Vocabularies; t++)
        {
            StringInternTable table = tables[t];
            int end = (t + 1) * ProbesPerVocabulary;
            for (int i = t * ProbesPerVocabulary; i < end; i++)
            {
                (int offset, int length) = s[i];
                if (table.Contains(buf.AsSpan(offset, length)))
                    hits++;
            }
        }

        return hits;
    }

    [Benchmark]
    public int StringInternTable_LookupXxHash3()
    {
        int d = draw;
        draw = (d + 1) % Draws;
        char[] buf = buffer;
        (int Offset, int Length)[] s = stream;
        StringInternTable<StringXxHash3Hasher>[] tables = xxHash3Tables;
        int hits = 0;
        for (int t = d * Vocabularies; t < (d + 1) * Vocabularies; t++)
        {
            StringInternTable<StringXxHash3Hasher> table = tables[t];
            int end = (t + 1) * ProbesPerVocabulary;
            for (int i = t * ProbesPerVocabulary; i < end; i++)
            {
                (int offset, int length) = s[i];
                if (table.Contains(buf.AsSpan(offset, length)))
                    hits++;
            }
        }

        return hits;
    }

    [Benchmark]
    public int StringInternTable_LookupFnV1AFull()
    {
        int d = draw;
        draw = (d + 1) % Draws;
        char[] buf = buffer;
        (int Offset, int Length)[] s = stream;
        StringInternTable<StringFnV1AFullHasher>[] tables = fnv1aFullTables;
        int hits = 0;
        for (int t = d * Vocabularies; t < (d + 1) * Vocabularies; t++)
        {
            StringInternTable<StringFnV1AFullHasher> table = tables[t];
            int end = (t + 1) * ProbesPerVocabulary;
            for (int i = t * ProbesPerVocabulary; i < end; i++)
            {
                (int offset, int length) = s[i];
                if (table.Contains(buf.AsSpan(offset, length)))
                    hits++;
            }
        }

        return hits;
    }
}
