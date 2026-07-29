using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

/// <summary>
/// Family-wide contract tests for <see cref="ISpanHashProvider"/>: which built-in hashers
/// implement it, and the one invariant every implementation must hold —
/// <c>Hash(s) == Hash(s.AsSpan())</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is not a nice-to-have. The span lookups on the string-keyed collections hash a span
/// and then compare the result against keys that were placed using the <em>string</em>
/// overload. If the two overloads ever disagreed for some input, the lookup would not be
/// slow — it would report a stored key as absent. That failure is silent, data-dependent,
/// and would survive every existing test, so it is pinned here directly.
/// </para>
/// <para>
/// The parity assertions run over <see cref="HasherStringCorpus"/>, which already sweeps every
/// length class the block-oriented hashers branch on (empty, sub-word tails, exact word and
/// stripe boundaries, several bulk-loop iterations) plus non-ASCII characters whose high byte
/// is set. Each string is checked both as a whole-string span and as a <em>slice of a larger
/// buffer</em> — the shape a real parser hands in, and the one that would catch an
/// implementation that read past its span or keyed off the buffer rather than the slice.
/// </para>
/// </remarks>
public class SpanHashParityTests
{
    /// <summary>
    /// The built-in hashers that hash a character span. Every <c>String*</c> hasher qualifies:
    /// each already walks the characters, so the span overload is the same body. The integer
    /// and <see cref="Guid"/> hashers do not — a character span is not their key shape.
    /// </summary>
    public static readonly string[] ExpectedSpanHashers =
    [
        nameof(StringAdler32Hasher),
        nameof(StringCityHash64Hasher),
        nameof(StringCrc32Hasher),
        nameof(StringDjb2AHasher),
        nameof(StringDjb2Hasher),
        nameof(StringElfHasher),
        nameof(StringFnV164Hasher),
        nameof(StringFnV1A64Hasher),
        nameof(StringFnV1AFullHasher),
        nameof(StringFnV1AHasher),
        nameof(StringFnV1Hasher),
        nameof(StringHalfSipHash24Hasher),
        nameof(StringHighwayHash64Hasher),
        nameof(StringJenkinsOaatHasher),
        nameof(StringMetroHash64Hasher),
        nameof(StringMurmur2Hasher),
        nameof(StringMurmur3Hasher),
        nameof(StringSdbmHasher),
        nameof(StringSipHash13Hasher),
        nameof(StringSipHash24Hasher),
        nameof(StringXxHash32Hasher),
        nameof(StringXxHash3Hasher),
        nameof(StringXxHash64Hasher),
    ];

    private static IEnumerable<Type> AllHasherTypes() =>
        typeof(IHashProvider<>).Assembly
            .GetExportedTypes()
            .Where(t => t.IsValueType && Array.Exists(t.GetInterfaces(), IsHashProvider32));

    private static bool IsHashProvider32(Type i) =>
        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHashProvider<>);

    private static bool IsSpanHashProvider(Type t) =>
        Array.Exists(t.GetInterfaces(), i => i == typeof(ISpanHashProvider));

    // ── Roster ────────────────────────────────────────────────────────────────

    [Fact]
    public void ISpanHashProvider_ShouldBeImplementedByExactlyTheExpectedHashers()
    {
        string[] actual = AllHasherTypes()
            .Where(IsSpanHashProvider)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ExpectedSpanHashers.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            actual);
    }

    [Fact]
    public void ISpanHashProvider_ShouldNotBeImplementedByTheNonStringHashers()
    {
        string[] nonStringSpanHashers = AllHasherTypes()
            .Where(t => IsSpanHashProvider(t) && !t.Name.StartsWith("String", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToArray();

        Assert.Empty(nonStringSpanHashers);
    }

    // ── The Hash(s) == Hash(s.AsSpan()) contract, per hasher ──────────────────

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenAdler32() => AssertParity<StringAdler32Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenCityHash64() => AssertParity<StringCityHash64Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenCrc32() => AssertParity<StringCrc32Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenDjb2() => AssertParity<StringDjb2Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenDjb2A() => AssertParity<StringDjb2AHasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenElf() => AssertParity<StringElfHasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenFnV1() => AssertParity<StringFnV1Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenFnV164() => AssertParity<StringFnV164Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenFnV1A() => AssertParity<StringFnV1AHasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenFnV1A64() => AssertParity<StringFnV1A64Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenFnV1AFull() => AssertParity<StringFnV1AFullHasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenHalfSipHash24() => AssertParity<StringHalfSipHash24Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenHighwayHash64() => AssertParity<StringHighwayHash64Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenJenkinsOaat() => AssertParity<StringJenkinsOaatHasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenMetroHash64() => AssertParity<StringMetroHash64Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenMurmur2() => AssertParity<StringMurmur2Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenMurmur3() => AssertParity<StringMurmur3Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenSdbm() => AssertParity<StringSdbmHasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenSipHash13() => AssertParity<StringSipHash13Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenSipHash24() => AssertParity<StringSipHash24Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenXxHash32() => AssertParity<StringXxHash32Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenXxHash3() => AssertParity<StringXxHash3Hasher>();

    [Fact]
    public void Hash_ShouldMatchStringOverload_WhenXxHash64() => AssertParity<StringXxHash64Hasher>();

    // ── Null handling on the string overload is unchanged ─────────────────────

    [Fact]
    public void Hash_ShouldThrowArgumentNullException_WhenStringKeyIsNull()
    {
        var hasher = new StringFnV1AHasher();
        Assert.Throws<ArgumentNullException>(() => hasher.Hash((string)null!));
    }

    [Fact]
    public void Hash_ShouldReturnTheEmptyStringCode_WhenSpanIsDefault()
    {
        // A span has no null state: default(ReadOnlySpan<char>) is empty, and empty means "".
        var hasher = new StringFnV1AHasher();
        Assert.Equal(hasher.Hash(string.Empty), hasher.Hash(default(ReadOnlySpan<char>)));
    }

    private static void AssertParity<THasher>()
        where THasher : struct, IHashProvider<string>, ISpanHashProvider
    {
        var hasher = default(THasher);

        foreach (string s in HasherStringCorpus.Strings)
        {
            int fromString = hasher.Hash(s);

            Assert.Equal(fromString, hasher.Hash(s.AsSpan()));

            // The same characters as a slice of a larger buffer, so the hasher cannot be
            // reading past the span or keying off anything but the slice's contents.
            string padded = "ÿÿ" + s + "ÿÿ";
            Assert.Equal(fromString, hasher.Hash(padded.AsSpan(2, s.Length)));

            // And as a slice of a char[] the caller owns, which is not a string at all.
            char[] buffer = new char[s.Length + 3];
            s.AsSpan().CopyTo(buffer.AsSpan(1));
            Assert.Equal(fromString, hasher.Hash(buffer.AsSpan(1, s.Length)));
        }
    }
}
