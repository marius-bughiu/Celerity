// 2.0.0 multi-package split — binary back-compat (#188).
//
// Before the split, the single Celerity.dll (PackageId Celerity.Collections)
// contained every public type: the collections, the Celerity.Hashing hashers,
// and the Celerity.Primitives utilities. The split moved the hashers into
// Celerity.Hashing.dll and the utilities into Celerity.Primitives.dll, but the
// assembly identity an already-compiled consumer bound those types to is still
// "Celerity". These [TypeForwardedTo] entries make the runtime resolve every
// moved type from its new assembly, so binaries compiled against the old single
// Celerity.dll keep working without recompilation. Source consumers are covered
// separately by the transitive package dependencies declared in Celerity.csproj.
//
// Namespaces are unchanged (Celerity.Hashing.* / Celerity.Primitives.*), so this
// is identity-only forwarding. Keep this list in sync with the public surface of
// the two lower packages.

using System.Runtime.CompilerServices;
using Celerity.Hashing;
using Celerity.Primitives;

// ---- Celerity.Hashing ----
[assembly: TypeForwardedTo(typeof(IHashProvider<>))]
[assembly: TypeForwardedTo(typeof(DefaultHasher<>))]
[assembly: TypeForwardedTo(typeof(HashQualityEvaluator))]
[assembly: TypeForwardedTo(typeof(HashQualityReport))]
[assembly: TypeForwardedTo(typeof(ProbeStatistics))]
[assembly: TypeForwardedTo(typeof(ProbeStatisticsEvaluator))]
[assembly: TypeForwardedTo(typeof(GuidHasher))]
[assembly: TypeForwardedTo(typeof(Int32IdentityHasher))]
[assembly: TypeForwardedTo(typeof(Int32Murmur3Hasher))]
[assembly: TypeForwardedTo(typeof(Int32WangHasher))]
[assembly: TypeForwardedTo(typeof(Int32WangNaiveHasher))]
[assembly: TypeForwardedTo(typeof(Int64IdentityHasher))]
[assembly: TypeForwardedTo(typeof(Int64Murmur3Hasher))]
[assembly: TypeForwardedTo(typeof(Int64WangHasher))]
[assembly: TypeForwardedTo(typeof(Int64WangNaiveHasher))]
[assembly: TypeForwardedTo(typeof(UInt32Hasher))]
[assembly: TypeForwardedTo(typeof(UInt32Murmur3Hasher))]
[assembly: TypeForwardedTo(typeof(UInt32WangHasher))]
[assembly: TypeForwardedTo(typeof(UInt64Hasher))]
[assembly: TypeForwardedTo(typeof(UInt64WangHasher))]
[assembly: TypeForwardedTo(typeof(UInt64WangNaiveHasher))]
[assembly: TypeForwardedTo(typeof(StringAdler32Hasher))]
[assembly: TypeForwardedTo(typeof(StringCityHash64Hasher))]
[assembly: TypeForwardedTo(typeof(StringCrc32Hasher))]
[assembly: TypeForwardedTo(typeof(StringDjb2Hasher))]
[assembly: TypeForwardedTo(typeof(StringDjb2AHasher))]
[assembly: TypeForwardedTo(typeof(StringElfHasher))]
[assembly: TypeForwardedTo(typeof(StringFnV1Hasher))]
[assembly: TypeForwardedTo(typeof(StringFnV164Hasher))]
[assembly: TypeForwardedTo(typeof(StringFnV1A64Hasher))]
[assembly: TypeForwardedTo(typeof(StringFnV1AFullHasher))]
[assembly: TypeForwardedTo(typeof(StringFnV1AHasher))]
[assembly: TypeForwardedTo(typeof(StringHalfSipHash24Hasher))]
[assembly: TypeForwardedTo(typeof(StringHighwayHash64Hasher))]
[assembly: TypeForwardedTo(typeof(StringJenkinsOaatHasher))]
[assembly: TypeForwardedTo(typeof(StringMetroHash64Hasher))]
[assembly: TypeForwardedTo(typeof(StringMurmur2Hasher))]
[assembly: TypeForwardedTo(typeof(StringMurmur3Hasher))]
[assembly: TypeForwardedTo(typeof(StringSdbmHasher))]
[assembly: TypeForwardedTo(typeof(StringSipHash13Hasher))]
[assembly: TypeForwardedTo(typeof(StringSipHash24Hasher))]
[assembly: TypeForwardedTo(typeof(StringXxHash32Hasher))]
[assembly: TypeForwardedTo(typeof(StringXxHash3Hasher))]
[assembly: TypeForwardedTo(typeof(StringXxHash64Hasher))]

// ---- Celerity.Primitives ----
[assembly: TypeForwardedTo(typeof(FastUtils))]
[assembly: TypeForwardedTo(typeof(FastGuid))]
[assembly: TypeForwardedTo(typeof(VarInt))]
[assembly: TypeForwardedTo(typeof(IRandomSource))]
[assembly: TypeForwardedTo(typeof(RandomSourceExtensions))]
[assembly: TypeForwardedTo(typeof(SplitMix64))]
[assembly: TypeForwardedTo(typeof(Xoshiro256StarStar))]
[assembly: TypeForwardedTo(typeof(Xoroshiro128Plus))]
[assembly: TypeForwardedTo(typeof(WyRand))]
[assembly: TypeForwardedTo(typeof(Pcg32))]
[assembly: TypeForwardedTo(typeof(GuidV7Generator<>))]
