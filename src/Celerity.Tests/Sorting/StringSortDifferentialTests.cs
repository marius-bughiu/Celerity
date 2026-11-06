using Celerity.Sorting;

namespace Celerity.Tests.Sorting;

/// <summary>
/// Reconciles every <see cref="StringSort"/> entry point against a stable ordinal oracle
/// (<see cref="Enumerable.OrderBy{TSource, TKey}(IEnumerable{TSource}, Func{TSource, TKey}, IComparer{TKey})"/>
/// with <see cref="StringComparer.Ordinal"/>) over seeded pseudo-random inputs. The packed window is
/// the error-prone part: a string that ends inside it and a string holding U+0000 there pack the
/// same, and a wrong split between them yields a plausible-looking order with one block misplaced,
/// not a crash. So the alphabets below are chosen to make those collisions common, not rare.
/// </summary>
/// <remarks>
/// Each case is a pure function of its seed, so a failure reproduces exactly. Lengths span the
/// trivial, insertion-only, stack-buffer and rented-buffer cases.
/// </remarks>
public class StringSortDifferentialTests
{
    public enum Shape
    {
        /// <summary>Lengths 0–12 over U+0000, 'a', 'b': prefixes, duplicates and NUL collisions everywhere.</summary>
        TinyAlphabetWithNul,

        /// <summary>Lengths 0–9 over the whole UTF-16 range, including lone surrogates and U+FFFF.</summary>
        AnyCodeUnit,

        /// <summary>A long shared prefix, then a short varying suffix.</summary>
        SharedPrefix,

        /// <summary>Like <see cref="SharedPrefix"/>, with about one key in eight null.</summary>
        WithNulls,
    }

    public static TheoryData<int, int, Shape> Cases
    {
        get
        {
            var data = new TheoryData<int, int, Shape>();
            foreach (int length in new[] { 2, 3, 16, 17, 64, 256, 257, 1000, 4000 })
            {
                foreach (int seed in new[] { 1, 2 })
                {
                    foreach (Shape shape in Enum.GetValues<Shape>())
                    {
                        data.Add(length, seed, shape);
                    }
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sort_ShouldMatchAStableOrdinalSort(int length, int seed, Shape shape)
    {
        string?[] keys = Generate(length, seed, shape);
        string?[] expected = StringSortTests.OrdinalStable(keys);

        StringSort.Sort(keys.AsSpan()!);

        for (int i = 0; i < length; i++)
        {
            Assert.Same(expected[i], keys[i]);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SortWithPayload_ShouldMatchAStableOrdinalSort(int length, int seed, Shape shape)
    {
        string?[] keys = Generate(length, seed, shape);
        int[] expected = StringSortTests.OrdinalStableOrder(keys);
        string?[] original = (string?[])keys.Clone();
        int[] values = Enumerable.Range(0, length).ToArray();

        StringSort.Sort(keys.AsSpan()!, values.AsSpan());

        Assert.Equal(expected, values);
        for (int i = 0; i < length; i++)
        {
            Assert.Same(original[expected[i]], keys[i]);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ArgSort_ShouldMatchAStableOrdinalSort(int length, int seed, Shape shape)
    {
        string?[] keys = Generate(length, seed, shape);
        int[] indices = new int[length];

        StringSort.ArgSort(keys!, indices.AsSpan());

        Assert.Equal(StringSortTests.OrdinalStableOrder(keys), indices);
    }

    private static string?[] Generate(int length, int seed, Shape shape)
    {
        var rand = new Random(seed * 7919 + (int)shape);
        string?[] keys = new string?[length];
        for (int i = 0; i < length; i++)
        {
            keys[i] = shape switch
            {
                Shape.TinyAlphabetWithNul => Random(rand, rand.Next(13), "\0ab"),
                Shape.AnyCodeUnit => RandomCodeUnits(rand, rand.Next(10)),
                Shape.SharedPrefix => "https://api.example.com/v2/" + Random(rand, rand.Next(1, 6), "abc/"),
                _ => rand.Next(8) == 0 ? null : "tenant:42:user:" + rand.Next(length).ToString("D6"),
            };
        }

        return keys;
    }

    private static string Random(Random rand, int length, string alphabet)
    {
        char[] chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = alphabet[rand.Next(alphabet.Length)];
        }

        return new string(chars);
    }

    private static string RandomCodeUnits(Random rand, int length)
    {
        char[] chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            // Weight the extremes so U+0000 and U+FFFF collide often.
            int pick = rand.Next(8);
            chars[i] = pick == 0 ? '\0' : pick == 1 ? '￿' : (char)rand.Next(0x10000);
        }

        return new string(chars);
    }
}
