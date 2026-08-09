using System.Reflection;
using System.Text.RegularExpressions;
using Celerity.Hashing;

#pragma warning disable CS0618 // The obsolete aliases are part of what is under test here.

namespace Celerity.Tests.Hashing;

/// <summary>
/// Family-wide naming and cross-width contract for the integer hashers: every live integer
/// hasher names its algorithm in its type name, the four key widths ship the same three
/// mixing tiers, and an unsigned hasher agrees bit-for-bit with its signed peer.
/// </summary>
/// <remarks>
/// <para>
/// Hasher selection is the main knob this library exposes, and callers pick by analogy across
/// widths. That only works if the name carries the algorithm. It stopped working once
/// <c>UInt32Hasher</c> meant the cheapest tier for <c>uint</c> while the identically-shaped
/// <c>UInt64Hasher</c> meant the strongest tier for <c>ulong</c> — a caller moving from 32-bit
/// to 64-bit keys changed hash strength, not just key width, and nothing said so. These tests
/// pin the repaired scheme so the drift cannot recur silently: a new bare-named integer hasher,
/// or a width missing a tier, fails here.
/// </para>
/// </remarks>
public class IntegerHasherFamilyNamingTests
{
    /// <summary>The three mixing tiers every integer width ships, cheapest first.</summary>
    private static readonly string[] Tiers = ["WangNaive", "Wang", "Murmur3"];

    private static readonly string[] Widths = ["Int32", "UInt32", "Int64", "UInt64"];

    private static IEnumerable<Type> IntegerHasherTypes() =>
        typeof(IHashProvider<>).Assembly
            .GetExportedTypes()
            .Where(t => t.IsValueType)
            .Where(t => Array.Exists(t.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHashProvider<>)))
            .Where(t => Regex.IsMatch(t.Name, "^U?Int(32|64)"));

    [Theory]
    [InlineData("Int32")]
    [InlineData("UInt32")]
    [InlineData("Int64")]
    [InlineData("UInt64")]
    public void EveryWidth_ShouldShipTheSameThreeMixingTiers_NamedAfterTheirAlgorithm(string width)
    {
        string[] missing = Tiers
            .Select(tier => $"{width}{tier}Hasher")
            .Where(name => IntegerHasherTypes().All(t => t.Name != name))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void NoLiveIntegerHasher_ShouldCarryABareWidthOnlyName()
    {
        // `UInt32Hasher` / `UInt64Hasher` still ship as obsolete aliases, so the rule is
        // "not without a deprecation notice" rather than "not at all".
        string[] offenders = IntegerHasherTypes()
            .Where(t => Regex.IsMatch(t.Name, "^U?Int(32|64)Hasher$"))
            .Where(t => t.GetCustomAttribute<ObsoleteAttribute>() is null)
            .Select(t => t.Name)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheIdentityTier_ShouldRemainSignedOnly_AsADeliberateAsymmetry()
    {
        // The zero-work floor is reached from an unsigned key with a free cast at the call
        // site (`new Int32IdentityHasher().Hash((int)u)`), so a UInt32IdentityHasher would be
        // a type that adds nothing. Recorded here rather than left to be rediscovered as a gap.
        Assert.Contains(IntegerHasherTypes(), t => t.Name == "Int32IdentityHasher");
        Assert.Contains(IntegerHasherTypes(), t => t.Name == "Int64IdentityHasher");
        Assert.DoesNotContain(IntegerHasherTypes(), t => t.Name == "UInt32IdentityHasher");
        Assert.DoesNotContain(IntegerHasherTypes(), t => t.Name == "UInt64IdentityHasher");
    }

    // ── Cross-width agreement on the same bit pattern ─────────────────────────

    public static TheoryData<int> Int32Keys =>
        [0, 1, 16, 255, 65536, int.MaxValue, int.MinValue, -1, 123456789, -987654321];

    public static TheoryData<long> Int64Keys =>
        [0L, 1L, -1L, 42L, long.MaxValue, long.MinValue, 1234567890123456789L, -9876543210L];

    [Theory]
    [MemberData(nameof(Int32Keys))]
    public void UInt32Hashers_ShouldAgreeWithTheirSignedPeers_OnTheSameBitPattern(int key)
    {
        uint bits = (uint)key;
        Assert.Equal(new Int32WangHasher().Hash(key), new UInt32WangHasher().Hash(bits));
        Assert.Equal(new Int32Murmur3Hasher().Hash(key), new UInt32Murmur3Hasher().Hash(bits));
    }

    [Theory]
    [MemberData(nameof(Int64Keys))]
    public void UInt64Hashers_ShouldAgreeWithTheirSignedPeers_OnTheSameBitPattern(long key)
    {
        ulong bits = (ulong)key;
        Assert.Equal(new Int64WangNaiveHasher().Hash(key), new UInt64WangNaiveHasher().Hash(bits));
        Assert.Equal(new Int64WangHasher().Hash(key), new UInt64WangHasher().Hash(bits));
        Assert.Equal(new Int64Murmur3Hasher().Hash(key), new UInt64Murmur3Hasher().Hash(bits));
    }

    [Fact]
    public void TheNaive32BitFold_ShouldDivergeBetweenTheSignedAndUnsignedPeers_OnANegativeKey()
    {
        // The one pair that is deliberately not bit-identical: `key >> 16` is arithmetic on
        // int and logical on uint, so a set top bit sign-extends on one side and not the other
        // (-1 folds to 0; 0xFFFFFFFF folds to 0xFFFF0000). Documented on both types; pinned
        // here so nobody "fixes" one side into agreement and changes a shipped hash.
        Assert.Equal(0, new Int32WangNaiveHasher().Hash(-1));
        Assert.Equal(unchecked((int)0xFFFF0000), new UInt32WangNaiveHasher().Hash(uint.MaxValue));

        // ...and they still agree wherever the top bit is clear.
        Assert.Equal(new Int32WangNaiveHasher().Hash(123456789),
                     new UInt32WangNaiveHasher().Hash(123456789u));
    }
}
