using BenchmarkDotNet.Attributes;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// How <see cref="StringInternTable"/>'s probe compares with <c>HashSet&lt;string&gt;</c>'s span lookup as the
/// tokens get longer: the length sweep behind the token-length claims in its documentation.
/// </summary>
/// <remarks>
/// <para>
/// The workload is <see cref="StringInternTableBenchmark"/>'s <c>Lookup</c> category, probing a warm table with
/// spans sliced from one buffer. Tokens are fixed at 3 to 64 characters, or drawn log-uniformly from 2–20 or 2–48
/// characters, as a parse that mixes short codes, words and identifiers would see them.
/// </para>
/// <para>
/// Each invocation probes sixteen tables, each with its own vocabulary of 64 random tokens, 6,250 times apiece in a
/// seeded random order. Which of 64 tokens happen to share a slot is luck of the draw: on a single vocabulary a
/// hasher's mean probe length ranged from 1.14 to 1.95, where the mean over many vocabularies is 1.47 for every
/// hasher, and at 8–20 ns a probe that luck moves a single-vocabulary ratio by tens of percent. The random order
/// keeps the branch predictor from learning the probe sequence, which a cyclic stream lets it do.
/// </para>
/// <para>
/// The arms are the default <see cref="StringCityHash64Hasher"/>, <see cref="StringXxHash3Hasher"/> (the choice for
/// long tokens) and <see cref="StringFnV1AFullHasher"/> (the default before 4.0). Tokens are ASCII; text that varies
/// both bytes of each UTF-16 code unit measured the same, since none of these hashers branches on content.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
public class StringInternTableTokenLengthBenchmark
{
    private const int Vocabularies = 16;
    private const int DistinctTokens = 64;
    private const int ProbesPerVocabulary = 6_250;
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789_";

    // One buffer holding every stream back to back; stream v is the slice
    // [v * ProbesPerVocabulary, (v + 1) * ProbesPerVocabulary) of the token list.
    private char[] buffer = null!;
    private (int Offset, int Length)[] stream = null!;

    private HashSet<string>.AlternateLookup<ReadOnlySpan<char>>[] sets = null!;
    private StringInternTable[] defaultTables = null!;
    private StringInternTable<StringXxHash3Hasher>[] xxHash3Tables = null!;
    private StringInternTable<StringFnV1AFullHasher>[] fnv1aFullTables = null!;

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
        var vocabularies = new string[Vocabularies][];
        stream = new (int, int)[Vocabularies * ProbesPerVocabulary];
        var text = new System.Text.StringBuilder(stream.Length * (max + 1));

        for (int v = 0; v < Vocabularies; v++)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string[] vocabulary = vocabularies[v] = new string[DistinctTokens];
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
                stream[v * ProbesPerVocabulary + i] = (text.Length, token.Length);
                text.Append(token).Append(',');
            }
        }

        buffer = text.ToString().ToCharArray();

        sets = new HashSet<string>.AlternateLookup<ReadOnlySpan<char>>[Vocabularies];
        defaultTables = new StringInternTable[Vocabularies];
        xxHash3Tables = new StringInternTable<StringXxHash3Hasher>[Vocabularies];
        fnv1aFullTables = new StringInternTable<StringFnV1AFullHasher>[Vocabularies];
        for (int v = 0; v < Vocabularies; v++)
        {
            var set = new HashSet<string>(DistinctTokens, StringComparer.Ordinal);
            defaultTables[v] = new StringInternTable(DistinctTokens);
            xxHash3Tables[v] = new StringInternTable<StringXxHash3Hasher>(DistinctTokens);
            fnv1aFullTables[v] = new StringInternTable<StringFnV1AFullHasher>(DistinctTokens);
            foreach (string token in vocabularies[v])
            {
                set.Add(token);
                defaultTables[v].GetOrAdd(token);
                xxHash3Tables[v].GetOrAdd(token);
                fnv1aFullTables[v].GetOrAdd(token);
            }

            sets[v] = set.GetAlternateLookup<ReadOnlySpan<char>>();
        }
    }

    [Benchmark(Baseline = true)]
    public int HashSet_Lookup()
    {
        char[] buf = buffer;
        (int Offset, int Length)[] s = stream;
        int hits = 0;
        for (int v = 0; v < Vocabularies; v++)
        {
            HashSet<string>.AlternateLookup<ReadOnlySpan<char>> lookup = sets[v];
            int end = (v + 1) * ProbesPerVocabulary;
            for (int i = v * ProbesPerVocabulary; i < end; i++)
            {
                (int offset, int length) = s[i];
                if (lookup.Contains(buf.AsSpan(offset, length)))
                    hits++;
            }
        }

        return hits;
    }

    [Benchmark]
    public int StringInternTable_Lookup() => Lookup(defaultTables);

    [Benchmark]
    public int StringInternTable_LookupXxHash3() => Lookup(xxHash3Tables);

    [Benchmark]
    public int StringInternTable_LookupFnV1AFull() => Lookup(fnv1aFullTables);

    private int Lookup<THasher>(StringInternTable<THasher>[] tables)
        where THasher : struct, IHashProvider<string>, ISpanHashProvider
    {
        char[] buf = buffer;
        (int Offset, int Length)[] s = stream;
        int hits = 0;
        for (int v = 0; v < tables.Length; v++)
        {
            StringInternTable<THasher> table = tables[v];
            int end = (v + 1) * ProbesPerVocabulary;
            for (int i = v * ProbesPerVocabulary; i < end; i++)
            {
                (int offset, int length) = s[i];
                if (table.Contains(buf.AsSpan(offset, length)))
                    hits++;
            }
        }

        return hits;
    }
}
