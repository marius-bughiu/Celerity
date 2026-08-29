using System.Text.RegularExpressions;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

/// <summary>
/// Family-wide naming and cross-width contract for the integer hashers: every integer
/// hasher names its algorithm in its type name, the four key widths ship the same four
/// tiers, and each unsigned hasher agrees bit-for-bit with its signed peer — with one
/// documented exception, the 32-bit naive fold, whose shift is arithmetic on one side and
/// logical on the other.
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
    /// <summary>
    /// The four tiers every integer width ships, cheapest first. <c>Identity</c> is the zero-work
    /// floor rather than a mixer, but it is part of the same ladder a caller escalates along, and
    /// it was the one tier the unsigned widths lacked.
    /// </summary>
    private static readonly string[] Tiers = ["Identity", "WangNaive", "Wang", "Murmur3"];

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
    public void EveryWidth_ShouldShipTheSameFourTiers_NamedAfterTheirAlgorithm(string width)
    {
        string[] missing = Tiers
            .Select(tier => $"{width}{tier}Hasher")
            .Where(name => IntegerHasherTypes().All(t => t.Name != name))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void NoIntegerHasher_ShouldCarryABareWidthOnlyName()
    {
        // `UInt32Hasher` / `UInt64Hasher` were the two offenders. They shipped as obsolete
        // aliases through v3.0.0 and are gone, so the rule is "not at all" rather than the
        // "not without a deprecation notice" it had to be while they were still on the roster.
        string[] offenders = IntegerHasherTypes()
            .Where(t => Regex.IsMatch(t.Name, "^U?Int(32|64)Hasher$"))
            .Select(t => t.Name)
            .ToArray();

        Assert.Empty(offenders);
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
        Assert.Equal(new Int32IdentityHasher().Hash(key), new UInt32IdentityHasher().Hash(bits));
        Assert.Equal(new Int32WangHasher().Hash(key), new UInt32WangHasher().Hash(bits));
        Assert.Equal(new Int32Murmur3Hasher().Hash(key), new UInt32Murmur3Hasher().Hash(bits));
    }

    [Theory]
    [MemberData(nameof(Int64Keys))]
    public void UInt64Hashers_ShouldAgreeWithTheirSignedPeers_OnTheSameBitPattern(long key)
    {
        ulong bits = (ulong)key;
        Assert.Equal(new Int64IdentityHasher().Hash(key), new UInt64IdentityHasher().Hash(bits));
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
