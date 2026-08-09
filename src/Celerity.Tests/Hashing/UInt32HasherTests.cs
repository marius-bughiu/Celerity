using System.Reflection;
using Celerity.Hashing;

#pragma warning disable CS0618 // The type under test is the obsolete alias.

namespace Celerity.Tests.Hashing;

/// <summary>
/// The deprecated <see cref="UInt32Hasher"/> alias. Its replacement,
/// <see cref="UInt32WangNaiveHasher"/>, carries the behavioural suite; what is pinned here is
/// that the alias still ships, still hashes identically, and still says it is deprecated.
/// </summary>
public class UInt32HasherTests
{
    private readonly UInt32Hasher _hasher = new UInt32Hasher();

    [Theory]
    [InlineData(0u, 0)]
    [InlineData(1u, 1)]
    [InlineData(16u, 16)]
    [InlineData(65536u, 65537)]
    [InlineData(uint.MaxValue, -65536)]     // 0xFFFFFFFF ^ 0x0000FFFF = 0xFFFF0000
    [InlineData(0x80000000u, -2147450880)]  // 0x80000000 ^ 0x00008000 = 0x80008000
    public void Hash_ShouldReturnTheSameCodeAsBefore_WhenCalledThroughTheAlias(uint input, int expected)
    {
        Assert.Equal(expected, _hasher.Hash(input));
    }

    [Fact]
    public void Hash_ShouldAgreeWithUInt32WangNaiveHasher_ForEveryKeyShape()
    {
        // The alias forwards rather than repeating the mixer, so this cannot drift by
        // construction — but a future edit that inlines the body back in would be caught here.
        var replacement = new UInt32WangNaiveHasher();
        foreach (uint key in new[]
                 {
                     0u, 1u, 16u, 255u, 65536u, 0x7FFFFFFFu, 0x80000000u,
                     123456789u, 987654321u, uint.MaxValue,
                 })
        {
            Assert.Equal(replacement.Hash(key), _hasher.Hash(key));
        }
    }

    [Fact]
    public void Type_ShouldBeMarkedObsolete_PointingAtItsReplacement()
    {
        var attribute = typeof(UInt32Hasher).GetCustomAttribute<ObsoleteAttribute>();

        Assert.NotNull(attribute);
        Assert.False(attribute.IsError, "the alias must still compile for one deprecation cycle");
        Assert.Contains(nameof(UInt32WangNaiveHasher), attribute.Message);
    }
}
