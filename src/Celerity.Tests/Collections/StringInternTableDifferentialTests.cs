using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="StringInternTable{THasher}"/> against an
/// independent <see cref="Dictionary{TKey, TValue}"/> oracle keyed by
/// <see cref="StringComparer.Ordinal"/>. CsCheck generates the stream of span interns, string interns,
/// lookups, enumerations and clears, and asserts after every operation that the two agree on count,
/// per-token membership, and — the property that makes the type worth having — that the table hands back
/// the <em>same reference</em> for every repeat of a token it has already seen.
/// </summary>
/// <remarks>
/// <para>
/// Tokens are drawn from a tiny alphabet at short lengths so hash collisions and probe chains are dense,
/// and the script interleaves the span and string entry points so a divergence between them surfaces.
/// The alphabet includes a non-ASCII character, so a hasher that reads bytes rather than chars cannot
/// pass by accident.
/// </para>
/// <para>
/// Both a weak and a strong hasher are driven through the same generated script. The weak one is the
/// interesting case — it collides often, so the probe chain does real work — but the strong one has to
/// agree with it on every observable, which is what separates a hashing bug from a table bug.
/// </para>
/// </remarks>
public class StringInternTableDifferentialTests
{
    private enum Op { InternSpan, InternString, Lookup, Enumerate, Clear }

    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 99].Select(n => n < 45 ? Op.InternSpan
                                 : n < 70 ? Op.InternString
                                 : n < 88 ? Op.Lookup
                                 : n < 97 ? Op.Enumerate
                                 : Op.Clear);

    // A tiny alphabet at short lengths, so tokens repeat constantly and probe chains stay dense.
    // The fourth symbol is non-ASCII, so a hasher that reads bytes rather than chars cannot pass.
    private static readonly Gen<char> GenSymbol = Gen.Int[0, 3].Select(i => "abcŁ"[i]);

    private static readonly Gen<(Op Kind, string Token)> GenOp =
        Gen.Select(GenKind, Gen.String[GenSymbol, 0, 4]);

    [Fact]
    public void StringInternTable_ShouldMatch_ADictionary_WithAWeakHasher() =>
        Run<StringFnV1AHasher>();

    [Fact]
    public void StringInternTable_ShouldMatch_ADictionary_WithAStrongHasher() =>
        Run<StringXxHash3Hasher>();

    private static void Run<THasher>()
        where THasher : struct, IHashProvider<string>, ISpanHashProvider
    {
        GenOp.List[0, 400].Sample(ops =>
        {
            // Capacity 2 so the table resizes early and often inside every script.
            var table = new StringInternTable<THasher>(capacity: 2);

            // The oracle maps a token's contents to the canonical instance the table returned first.
            var oracle = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (kind, token) in ops)
            {
                switch (kind)
                {
                    case Op.InternSpan:
                    {
                        // Intern from a span carved out of a larger buffer — the parser shape.
                        string padded = "<<" + token + ">>";
                        string interned = table.GetOrAdd(padded.AsSpan(2, token.Length));

                        Assert.Equal(token, interned);
                        if (oracle.TryGetValue(token, out string? canonical))
                            Assert.Same(canonical, interned);
                        else
                            oracle[token] = interned;
                        break;
                    }

                    case Op.InternString:
                    {
                        // Intern from a freshly allocated string: on a miss the supplied instance
                        // itself becomes canonical; on a hit the already-held instance comes back.
                        string supplied = new string(token.ToCharArray());
                        string interned = table.GetOrAdd(supplied);

                        if (oracle.TryGetValue(token, out string? canonical))
                        {
                            Assert.Same(canonical, interned);
                        }
                        else
                        {
                            Assert.Same(supplied, interned);
                            oracle[token] = interned;
                        }

                        break;
                    }

                    case Op.Lookup:
                    {
                        // Pure lookup: never mutates, and agrees with the oracle on both entry points.
                        bool expected = oracle.TryGetValue(token, out string? canonical);
                        Assert.Equal(expected, table.TryGet(token.AsSpan(), out string? actual));
                        Assert.Equal(expected, table.Contains(token.AsSpan()));
                        Assert.Equal(expected, table.Contains(token));
                        if (expected)
                            Assert.Same(canonical, actual);
                        else
                            Assert.Null(actual);

                        break;
                    }

                    case Op.Enumerate:
                    {
                        // Enumeration yields exactly the canonical instances, once each.
                        var seen = new List<string>();
                        foreach (string s in table)
                            seen.Add(s);

                        Assert.Equal(oracle.Count, seen.Count);
                        foreach (string s in seen)
                        {
                            Assert.True(oracle.TryGetValue(s, out string? canonical));
                            Assert.Same(canonical, s);
                        }

                        break;
                    }

                    case Op.Clear:
                        table.Clear();
                        oracle.Clear();
                        break;
                }

                Assert.Equal(oracle.Count, table.Count);
            }
        }, iter: 40);
    }
}
