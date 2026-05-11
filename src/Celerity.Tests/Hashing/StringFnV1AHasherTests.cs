using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringFnV1AHasherTests
{
    private readonly StringFnV1AHasher _hasher = new StringFnV1AHasher();

    /// <summary>
    /// Test known FNV-1a 32-bit outputs for certain strings.
    /// We interpret the 32-bit result as a signed int in C#.
    /// </summary>
    [Theory]
    // These expected values match the final signed-32-bit interpretation.
    //  FNV-1a("")   -> 0x811C9DC5 -> 2166136261 unsigned -> -2128831035 signed
    //  FNV-1a("a")  -> 0xE40C292C -> 3826002220 unsigned -> -468965076  signed
    //  FNV-1a("hello") -> 0x4F9F2CAB -> 1335831723 as signed (positive)
    [InlineData("", -2128831035)]
    [InlineData("a", -468965076)]
    [InlineData("hello", 1335831723)]
    public void Hash_String_MatchesKnownFNV1aValues(string input, int expected)
    {
        // Act
        int actual = _hasher.Hash(input);

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Hashing the same string multiple times should always yield the same result.
    /// </summary>
    [Fact]
    public void Hash_SameStringRepeatedly_ReturnsSameValue()
    {
        // Arrange
        const string testString = "Consistent Hashing";

        // Act
        int hash1 = _hasher.Hash(testString);
        int hash2 = _hasher.Hash(testString);
        int hash3 = _hasher.Hash(testString);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.Equal(hash2, hash3);
    }

    /// <summary>
    /// Verify that two strings differing only in case produce different hashes.
    /// FNV-1a is case-sensitive for ASCII letters (unless you transform the input).
    /// </summary>
    [Fact]
    public void Hash_DifferentCaseStrings_ProduceDifferentValues()
    {
        // Arrange
        const string lower = "hello";
        const string upper = "HELLO";

        // Act
        int hashLower = _hasher.Hash(lower);
        int hashUpper = _hasher.Hash(upper);

        // Assert
        Assert.NotEqual(hashLower, hashUpper);
    }

    /// <summary>
    /// Demonstrates what happens if you pass null into the current implementation.
    /// The current code will throw NullReferenceException (or potentially an NRE).
    /// If you'd prefer an explicit ArgumentNullException, modify the struct.
    /// </summary>
    [Fact]
    public void Hash_NullString_ThrowsNullReferenceException()
    {
        Assert.Throws<NullReferenceException>(() => _hasher.Hash(null));
    }
}
