namespace Celerity.Tests.Hashing;

/// <summary>
/// A shared corpus of strings covering every length class the block-oriented string hashers
/// dispatch on — empty, sub-word tails, exact word and stripe boundaries, and inputs long
/// enough to run the bulk loop several times — plus non-ASCII characters whose high byte is
/// set, so a hasher that consumed only the low byte of each <c>char</c> would be caught.
/// </summary>
/// <remarks>
/// Used by the per-hasher <c>Hash64</c> tests, which reconcile the 64-bit surface against the
/// same independent byte-oriented reference implementation the 32-bit tests already use.
/// Because the UTF-16 byte length is twice the character count, the sweep of lengths 0..40
/// crosses every 4-, 8-, 16-, 32- and 64-byte boundary the hashers branch on.
/// </remarks>
internal static class HasherStringCorpus
{
    /// <summary>The corpus.</summary>
    internal static readonly string[] Strings = Build();

    private static string[] Build()
    {
        var strings = new List<string>();

        // Lengths 0..40 chars (0..80 bytes) of a repeating alphabet.
        const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJ";
        for (int len = 0; len <= 40; len++)
            strings.Add(Alphabet[..len]);

        strings.AddRange(
        [
            "hello world",
            "HELLO",
            "Ł",                  // U+0141 (Ł) — non-ASCII, high byte set
            "Łatin",              // mixed ASCII / non-ASCII
            "日本語",      // CJK, all high bytes set
            "a\0b",                // embedded NUL
            "a b",
            new string('x', 63),
            new string('x', 64),
            new string('x', 65),
            new string('y', 127),
            new string('z', 128),
            new string('q', 257),
            string.Concat(Enumerable.Repeat("Łatin日本語-", 40)),
        ]);

        return [.. strings];
    }
}
