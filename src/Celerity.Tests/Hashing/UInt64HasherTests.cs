using System.Reflection;
using Celerity.Hashing;

#pragma warning disable CS0618 // The type under test is the obsolete alias.

namespace Celerity.Tests.Hashing;

/// <summary>
/// The deprecated <see cref="UInt64Hasher"/> alias. Its replacement,
/// <see cref="UInt64Murmur3Hasher"/>, carries the behavioural suite; what is pinned here is
/// that the alias still ships, still hashes identically on both surfaces, and still says it
/// is deprecated.
/// </summary>
public class UInt64HasherTests
{
    private readonly UInt64Hasher _hasher = new UInt64Hasher();

    [Theory]
    [InlineData(0UL,                     0x0000000000000000UL)] // fmix64 fixes zero
    [InlineData(1UL,                     0xB456BCFC34C2CB2CUL)]
    [InlineData(ulong.MaxValue,          0x64B5720B4B825F21UL)]
    [InlineData(42UL,                    0x810879608E4259CCUL)]
    [InlineData(0x7FFFFFFFFFFFFFFFUL,    0xABB93DF0A930EDEAUL)]
    [InlineData(0x8000000000000000UL,    0x8F780810AF31A493UL)]
    [InlineData(1234567890123456789UL,   0x9C49C6098A8F367EUL)]
    public void Hash64_ShouldReturnTheSameCodeAsBefore_WhenCalledThroughTheAlias(ulong input, ulong expected)
    {
        Assert.Equal(expected, _hasher.Hash64(input));
        Assert.Equal(_hasher.Hash(input), (int)expected);
    }

    [Fact]
    public void Hash_ShouldAgreeWithUInt64Murmur3Hasher_OnBothSurfaces()
    {
        var replacement = new UInt64Murmur3Hasher();
        foreach (ulong key in new[]
                 {
                     0UL, 1UL, 42UL, 0x7FFFFFFFFFFFFFFFUL, 0x8000000000000000UL,
                     0xDEADBEEFCAFEBABEUL, 1234567890123456789UL, ulong.MaxValue,
                 })
        {
            Assert.Equal(replacement.Hash(key), _hasher.Hash(key));
            Assert.Equal(replacement.Hash64(key), _hasher.Hash64(key));
        }
    }

    [Fact]
    public void Type_ShouldBeMarkedObsolete_PointingAtItsReplacement()
    {
        var attribute = typeof(UInt64Hasher).GetCustomAttribute<ObsoleteAttribute>();

        Assert.NotNull(attribute);
        Assert.False(attribute.IsError, "the alias must still compile for one deprecation cycle");
        Assert.Contains(nameof(UInt64Murmur3Hasher), attribute.Message);
    }

    [Fact]
    public void Type_ShouldStillImplementIHashProvider64_SoSketchesKeepTheir64BitPath()
    {
        // Dropping the interface from the alias would silently push an existing sketch back
        // to the 2^32 entropy floor the interface exists to escape.
        Assert.IsAssignableFrom<IHashProvider64<ulong>>(new UInt64Hasher());
    }
}
