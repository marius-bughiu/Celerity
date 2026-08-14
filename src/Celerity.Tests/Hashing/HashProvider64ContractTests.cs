using System.Reflection;
using Celerity.Hashing;

#pragma warning disable CS0618 // UInt64Hasher is an obsolete alias but still on the roster.

namespace Celerity.Tests.Hashing;

/// <summary>
/// Family-wide contract tests for <see cref="IHashProvider64{T}"/>: which built-in hashers
/// implement it, and the invariants every implementation must hold.
/// </summary>
/// <remarks>
/// <para>
/// The interface is a claim about <em>entropy</em>, not about the presence of a method. A
/// hasher may only implement it when its result carries genuine 64-bit information — which
/// rules out every hasher whose key type is 32 bits wide, and every hasher that computes 32
/// bits of state and would have to widen. The roster test below pins that judgement so a
/// later change cannot mechanically sprinkle <c>Hash64</c> onto hashers that have nothing to
/// expose; a sketch parameterized on such a hasher would silently believe it had escaped the
/// 2^32 floor.
/// </para>
/// </remarks>
public class HashProvider64ContractTests
{
    /// <summary>
    /// The built-in hashers that carry genuine 64-bit entropy. Either the algorithm already
    /// computes a 64-bit state internally and used to fold it away (the string hashers), or
    /// the key type is 64/128 bits wide and the mixer is a bijection over it (the integer
    /// and <see cref="Guid"/> hashers).
    /// </summary>
    public static readonly string[] Expected64BitHashers =
    [
        nameof(GuidHasher),
        nameof(Int64Murmur3Hasher),
        nameof(Int64WangHasher),
        nameof(StringCityHash64Hasher),
        nameof(StringFnV164Hasher),
        nameof(StringFnV1A64Hasher),
        nameof(StringHighwayHash64Hasher),
        nameof(StringMetroHash64Hasher),
        nameof(StringSipHash13Hasher),
        nameof(StringSipHash24Hasher),
        nameof(StringXxHash3Hasher),
        nameof(StringXxHash64Hasher),
        nameof(UInt64Hasher), // obsolete alias for UInt64Murmur3Hasher; still ships, so still here
        nameof(UInt64Murmur3Hasher),
        nameof(UInt64WangHasher),
    ];

    private static IEnumerable<Type> AllHasherTypes() =>
        typeof(IHashProvider<>).Assembly
            .GetExportedTypes()
            .Where(t => t.IsValueType && Array.Exists(t.GetInterfaces(), IsHashProvider32));

    private static bool IsHashProvider32(Type i) =>
        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHashProvider<>);

    private static bool IsHashProvider64(Type t) =>
        Array.Exists(t.GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHashProvider64<>));

    // ── Roster ────────────────────────────────────────────────────────────────

    [Fact]
    public void IHashProvider64_IsImplementedByExactlyTheExpectedHashers()
    {
        string[] actual = AllHasherTypes()
            .Where(IsHashProvider64)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            Expected64BitHashers.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            actual);
    }

    [Fact]
    public void NarrowKeyHashers_DoNotClaim64BitEntropy()
    {
        // A 32-bit key type has at most 2^32 distinct values, so no mixer over it can
        // produce 64 bits of information — implementing IHashProvider64<T> here would be a
        // false claim that a sketch would act on. Same for the naive folds and for
        // DefaultHasher<T>, which is bounded by the 32-bit object.GetHashCode(), and same
        // for the identity hashers: the 64-bit ones truncate to the low half rather than
        // mixing, so they have no 64-bit code to publish at all.
        Type[] offenders = AllHasherTypes()
            .Where(IsHashProvider64)
            .Where(t => t.Name.StartsWith("Int32", StringComparison.Ordinal)
                     || t.Name.StartsWith("UInt32", StringComparison.Ordinal)
                     || t.Name.Contains("Naive", StringComparison.Ordinal)
                     || t.Name.Contains("Identity", StringComparison.Ordinal)
                     || t.Name.StartsWith("Default", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryHasher64_IsAStructSoTheJitCanDevirtualizeIt()
    {
        foreach (Type t in AllHasherTypes().Where(IsHashProvider64))
        {
            Assert.True(t.IsValueType, $"{t.Name} must be a struct.");
            Assert.NotNull(t.GetMethod("Hash64", BindingFlags.Public | BindingFlags.Instance));
        }
    }

    // ── Reduction agreement: Hash is the documented narrowing of Hash64 ───────

    public static TheoryData<long> LongKeys =>
        [0L, 1L, -1L, 42L, long.MaxValue, long.MinValue, 1234567890123456789L, -9876543210L];

    [Theory]
    [MemberData(nameof(LongKeys))]
    public void Int64Hashers_HashIsTheLowHalfOfHash64(long key)
    {
        Assert.Equal(new Int64WangHasher().Hash(key), (int)new Int64WangHasher().Hash64(key));
        Assert.Equal(new Int64Murmur3Hasher().Hash(key), (int)new Int64Murmur3Hasher().Hash64(key));

        ulong bits = (ulong)key;
        Assert.Equal(new UInt64WangHasher().Hash(bits), (int)new UInt64WangHasher().Hash64(bits));
        Assert.Equal(new UInt64Murmur3Hasher().Hash(bits), (int)new UInt64Murmur3Hasher().Hash64(bits));
        Assert.Equal(new UInt64Hasher().Hash(bits), (int)new UInt64Hasher().Hash64(bits));
    }

    [Theory]
    [MemberData(nameof(LongKeys))]
    public void UInt64Hashers_AgreeWithTheirSignedPeersOnTheSameBitPattern(long key)
    {
        ulong bits = (ulong)key;
        Assert.Equal(new Int64WangHasher().Hash64(key), new UInt64WangHasher().Hash64(bits));
        Assert.Equal(new Int64Murmur3Hasher().Hash64(key), new UInt64Murmur3Hasher().Hash64(bits));
        Assert.Equal(new Int64Murmur3Hasher().Hash64(key), new UInt64Hasher().Hash64(bits));
    }

    [Fact]
    public void GuidHasher_HashIsTheLowHalfOfHash64()
    {
        foreach (Guid g in new[]
                 {
                     Guid.Empty,
                     new Guid("12345678-1234-1234-1234-1234567890AB"),
                     new Guid("DEADBEEF-CAFE-BABE-F00D-123456789ABC"),
                     new Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"),
                 })
        {
            Assert.Equal(new GuidHasher().Hash(g), (int)new GuidHasher().Hash64(g));
        }
    }

    public static TheoryData<string> StringKeys =>
        ["", "a", "ab", "abc", "abcdefgh", "hello world", "Ł", "日本語", new string('x', 257)];

    [Theory]
    [MemberData(nameof(StringKeys))]
    public void StringHashers64_HashIsTheXorFoldOfHash64(string key)
    {
        AssertFold(new StringCityHash64Hasher().Hash64(key), new StringCityHash64Hasher().Hash(key));
        AssertFold(new StringFnV164Hasher().Hash64(key), new StringFnV164Hasher().Hash(key));
        AssertFold(new StringFnV1A64Hasher().Hash64(key), new StringFnV1A64Hasher().Hash(key));
        AssertFold(new StringHighwayHash64Hasher().Hash64(key), new StringHighwayHash64Hasher().Hash(key));
        AssertFold(new StringMetroHash64Hasher().Hash64(key), new StringMetroHash64Hasher().Hash(key));
        AssertFold(new StringSipHash13Hasher().Hash64(key), new StringSipHash13Hasher().Hash(key));
        AssertFold(new StringSipHash24Hasher().Hash64(key), new StringSipHash24Hasher().Hash(key));
        AssertFold(new StringXxHash3Hasher().Hash64(key), new StringXxHash3Hasher().Hash(key));
        AssertFold(new StringXxHash64Hasher().Hash64(key), new StringXxHash64Hasher().Hash(key));

        static void AssertFold(ulong hash64, int hash32) =>
            Assert.Equal(unchecked((int)(hash64 ^ (hash64 >> 32))), hash32);
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash64_IsDeterministic_AcrossInstances()
    {
        // The hashers are stateless structs, so two independently-constructed instances —
        // and the `default` instance the collections and sketches build — must agree.
        Assert.Equal(new Int64WangHasher().Hash64(42L), default(Int64WangHasher).Hash64(42L));
        Assert.Equal(new UInt64Murmur3Hasher().Hash64(42UL), default(UInt64Murmur3Hasher).Hash64(42UL));
        Assert.Equal(new StringXxHash64Hasher().Hash64("x"), default(StringXxHash64Hasher).Hash64("x"));
        Assert.Equal(new GuidHasher().Hash64(Guid.Empty), default(GuidHasher).Hash64(Guid.Empty));
    }

    // ── High-half entropy is real, not a widened 32-bit code ─────────────────

    [Fact]
    public void Hash64_UsesTheFullRange_NotJustTheLowHalf()
    {
        // The defect this interface exists to fix is a hash whose reachable space is 2^32.
        // Over a few thousand keys a genuine 64-bit hash produces thousands of distinct
        // *high* halves; a widened 32-bit code with a fixed high half would produce one.
        var highHalves = new HashSet<uint>();
        var hasher = new Int64WangHasher();
        for (long i = 0; i < 4096; i++)
            highHalves.Add((uint)(hasher.Hash64(i) >> 32));

        Assert.True(highHalves.Count > 4000, $"only {highHalves.Count} distinct high halves");
    }

    [Fact]
    public void Hash64_StringHashers_ThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new StringCityHash64Hasher().Hash64(null!));
        Assert.Throws<ArgumentNullException>(() => new StringFnV164Hasher().Hash64(null!));
        Assert.Throws<ArgumentNullException>(() => new StringFnV1A64Hasher().Hash64(null!));
        Assert.Throws<ArgumentNullException>(() => new StringHighwayHash64Hasher().Hash64(null!));
        Assert.Throws<ArgumentNullException>(() => new StringMetroHash64Hasher().Hash64(null!));
        Assert.Throws<ArgumentNullException>(() => new StringSipHash13Hasher().Hash64(null!));
        Assert.Throws<ArgumentNullException>(() => new StringSipHash24Hasher().Hash64(null!));
        Assert.Throws<ArgumentNullException>(() => new StringXxHash3Hasher().Hash64(null!));
        Assert.Throws<ArgumentNullException>(() => new StringXxHash64Hasher().Hash64(null!));
    }
}
