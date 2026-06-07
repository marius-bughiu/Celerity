// Native AOT smoke test for Celerity (#32).
//
// This console app exercises every collection shape and a representative spread
// of hashers so that `dotnet publish /p:PublishAot=true` is forced to compile
// each generic instantiation down to native code. It is run by the AOT CI job:
// a non-zero exit code (any failed assertion) fails the build, proving the
// library works end-to-end under Native AOT, not just that the static analyzers
// are happy.

using Celerity;
using Celerity.Collections;
using Celerity.Hashing;
using Celerity.Primitives;

int failures = 0;

void Check(bool condition, string message)
{
    if (!condition)
    {
        Console.Error.WriteLine($"FAIL: {message}");
        failures++;
    }
}

// IntDictionary (default Int32WangNaiveHasher) — indexer, TryAdd/Add, TryGetValue,
// Remove, zero-key out-of-band slot, struct enumerator.
{
    var d = new IntDictionary<int>();
    d[42] = 1;
    d[42]++;
    Check(d.TryAdd(7, 100), "IntDictionary.TryAdd new key");
    Check(!d.TryAdd(7, 999), "IntDictionary.TryAdd duplicate");
    d.Add(8, 200);
    d[0] = 99; // zero key is a legitimate value, not the empty sentinel
    Check(d.TryGetValue(42, out var v) && v == 2, "IntDictionary indexer round-trip");
    Check(d[0] == 99, "IntDictionary zero-key round-trip");
    Check(d.Remove(7), "IntDictionary.Remove");
    var sum = 0;
    foreach (var kvp in d) sum += kvp.Value;
    Check(sum == 2 + 200 + 99, "IntDictionary enumeration");
    Check(d.Count == 3, "IntDictionary count");
}

// LongDictionary (default Int64WangNaiveHasher) — upper-32-bits distinctness.
{
    var d = new LongDictionary<string>();
    d[1L << 40] = "high";
    d[1L] = "low";
    Check(d.Count == 2, "LongDictionary distinct upper/lower bits");
    Check(d.TryGetValue(1L << 40, out var v) && v == "high", "LongDictionary round-trip");
}

// CelerityDictionary with a spread of hashers.
{
    var byGuid = new CelerityDictionary<Guid, string, GuidHasher>();
    var id = Guid.NewGuid();
    byGuid[id] = "alice";
    byGuid[Guid.Empty] = "empty"; // out-of-band default-key slot
    Check(byGuid[id] == "alice", "CelerityDictionary<Guid> round-trip");
    Check(byGuid[Guid.Empty] == "empty", "CelerityDictionary<Guid> empty-key slot");

    var byStr = new CelerityDictionary<string, int, StringMurmur3Hasher>();
    byStr["hello"] = 1;
    byStr["Ł"] = 2; // non-ASCII, distinct from low-byte-equal chars
    Check(byStr.TryGetValue("hello", out var hv) && hv == 1, "CelerityDictionary<string> round-trip");

    var fnv = new CelerityDictionary<string, int, StringFnV1AHasher>();
    fnv["a"] = 1;
    Check(fnv.ContainsKey("a"), "CelerityDictionary<string, StringFnV1AHasher>");

    var fnv1 = new CelerityDictionary<string, int, StringFnV1Hasher>();
    fnv1["A"] = 1;
    fnv1["Ł"] = 2; // FNV-1 full-width fold keeps upper-byte-distinct keys separate
    Check(fnv1.ContainsKey("Ł") && fnv1.Count == 2,
        "CelerityDictionary<string, StringFnV1Hasher>");

    var fnvFull = new CelerityDictionary<string, int, StringFnV1AFullHasher>();
    fnvFull["A"] = 1;
    fnvFull["Ł"] = 2; // full-width fold keeps upper-byte-distinct keys separate
    Check(fnvFull.ContainsKey("Ł") && fnvFull.Count == 2,
        "CelerityDictionary<string, StringFnV1AFullHasher>");

    var fnv64 = new CelerityDictionary<string, int, StringFnV1A64Hasher>();
    fnv64["A"] = 1;
    fnv64["Ł"] = 2; // 64-bit full-width fold keeps upper-byte-distinct keys separate
    Check(fnv64.ContainsKey("Ł") && fnv64.Count == 2,
        "CelerityDictionary<string, StringFnV1A64Hasher>");

    var fnv1_64 = new CelerityDictionary<string, int, StringFnV164Hasher>();
    fnv1_64["A"] = 1;
    fnv1_64["Ł"] = 2; // FNV-1 64-bit full-width fold keeps upper-byte-distinct keys separate
    Check(fnv1_64.ContainsKey("Ł") && fnv1_64.Count == 2,
        "CelerityDictionary<string, StringFnV164Hasher>");

    var oaat = new CelerityDictionary<string, int, StringJenkinsOaatHasher>();
    oaat["A"] = 1;
    oaat["Ł"] = 2; // one-at-a-time full-width mix keeps upper-byte-distinct keys separate
    Check(oaat.ContainsKey("Ł") && oaat.Count == 2,
        "CelerityDictionary<string, StringJenkinsOaatHasher>");

    var djb2 = new CelerityDictionary<string, int, StringDjb2Hasher>();
    djb2["A"] = 1;
    djb2["Ł"] = 2; // djb2 full-width fold keeps upper-byte-distinct keys separate
    Check(djb2.ContainsKey("Ł") && djb2.Count == 2,
        "CelerityDictionary<string, StringDjb2Hasher>");

    var djb2a = new CelerityDictionary<string, int, StringDjb2AHasher>();
    djb2a["A"] = 1;
    djb2a["Ł"] = 2; // djb2a full-width fold keeps upper-byte-distinct keys separate
    Check(djb2a.ContainsKey("Ł") && djb2a.Count == 2,
        "CelerityDictionary<string, StringDjb2AHasher>");

    var sdbm = new CelerityDictionary<string, int, StringSdbmHasher>();
    sdbm["A"] = 1;
    sdbm["Ł"] = 2; // sdbm full-width fold keeps upper-byte-distinct keys separate
    Check(sdbm.ContainsKey("Ł") && sdbm.Count == 2,
        "CelerityDictionary<string, StringSdbmHasher>");

    var elf = new CelerityDictionary<string, int, StringElfHasher>();
    elf["A"] = 1;
    elf["Ł"] = 2; // ELF full-width fold keeps upper-byte-distinct keys separate
    Check(elf.ContainsKey("Ł") && elf.Count == 2,
        "CelerityDictionary<string, StringElfHasher>");

    var crc32 = new CelerityDictionary<string, int, StringCrc32Hasher>();
    crc32["A"] = 1;
    crc32["Ł"] = 2; // CRC-32 full-width fold keeps upper-byte-distinct keys separate
    Check(crc32.ContainsKey("Ł") && crc32.Count == 2,
        "CelerityDictionary<string, StringCrc32Hasher>");

    var adler32 = new CelerityDictionary<string, int, StringAdler32Hasher>();
    adler32["A"] = 1;
    adler32["Ł"] = 2; // Adler-32 full-width fold keeps upper-byte-distinct keys separate
    Check(adler32.ContainsKey("Ł") && adler32.Count == 2,
        "CelerityDictionary<string, StringAdler32Hasher>");

    var murmur2 = new CelerityDictionary<string, int, StringMurmur2Hasher>();
    murmur2["A"] = 1;
    murmur2["Ł"] = 2; // MurmurHash2 full-width fold keeps upper-byte-distinct keys separate
    Check(murmur2.ContainsKey("Ł") && murmur2.Count == 2,
        "CelerityDictionary<string, StringMurmur2Hasher>");

    var xxh32 = new CelerityDictionary<string, int, StringXxHash32Hasher>();
    xxh32["A"] = 1;
    xxh32["Ł"] = 2; // xxHash32 full-width fold keeps upper-byte-distinct keys separate
    Check(xxh32.ContainsKey("Ł") && xxh32.Count == 2,
        "CelerityDictionary<string, StringXxHash32Hasher>");

    var xxh64 = new CelerityDictionary<string, int, StringXxHash64Hasher>();
    xxh64["A"] = 1;
    xxh64["Ł"] = 2; // xxHash64 full-width fold keeps upper-byte-distinct keys separate
    Check(xxh64.ContainsKey("Ł") && xxh64.Count == 2,
        "CelerityDictionary<string, StringXxHash64Hasher>");

    var metro64 = new CelerityDictionary<string, int, StringMetroHash64Hasher>();
    metro64["A"] = 1;
    metro64["Ł"] = 2; // MetroHash64 full-width fold keeps upper-byte-distinct keys separate
    Check(metro64.ContainsKey("Ł") && metro64.Count == 2,
        "CelerityDictionary<string, StringMetroHash64Hasher>");

    var city64 = new CelerityDictionary<string, int, StringCityHash64Hasher>();
    city64["A"] = 1;
    city64["Ł"] = 2; // CityHash64 full-width fold keeps upper-byte-distinct keys separate
    Check(city64.ContainsKey("Ł") && city64.Count == 2,
        "CelerityDictionary<string, StringCityHash64Hasher>");

    var sip13 = new CelerityDictionary<string, int, StringSipHash13Hasher>();
    sip13["A"] = 1;
    sip13["Ł"] = 2; // SipHash-1-3 full-width fold keeps upper-byte-distinct keys separate
    Check(sip13.ContainsKey("Ł") && sip13.Count == 2,
        "CelerityDictionary<string, StringSipHash13Hasher>");

    var sip24 = new CelerityDictionary<string, int, StringSipHash24Hasher>();
    sip24["A"] = 1;
    sip24["Ł"] = 2; // SipHash-2-4 full-width fold keeps upper-byte-distinct keys separate
    Check(sip24.ContainsKey("Ł") && sip24.Count == 2,
        "CelerityDictionary<string, StringSipHash24Hasher>");

    var halfSip24 = new CelerityDictionary<string, int, StringHalfSipHash24Hasher>();
    halfSip24["A"] = 1;
    halfSip24["Ł"] = 2; // HalfSipHash-2-4 full-width fold keeps upper-byte-distinct keys separate
    Check(halfSip24.ContainsKey("Ł") && halfSip24.Count == 2,
        "CelerityDictionary<string, StringHalfSipHash24Hasher>");

    var highway64 = new CelerityDictionary<string, int, StringHighwayHash64Hasher>();
    highway64["A"] = 1;
    highway64["Ł"] = 2; // HighwayHash64 full-width fold keeps upper-byte-distinct keys separate
    Check(highway64.ContainsKey("Ł") && highway64.Count == 2,
        "CelerityDictionary<string, StringHighwayHash64Hasher>");

    var xxh3 = new CelerityDictionary<string, int, StringXxHash3Hasher>();
    xxh3["A"] = 1;
    xxh3["Ł"] = 2; // XXH3 full-width fold keeps upper-byte-distinct keys separate
    Check(xxh3.ContainsKey("Ł") && xxh3.Count == 2,
        "CelerityDictionary<string, StringXxHash3Hasher>");

    // DefaultHasher<T> routes through EqualityComparer<T>.Default — the most
    // AOT-sensitive path in the library.
    var def = new CelerityDictionary<int, int, DefaultHasher<int>>();
    def[5] = 50;
    Check(def[5] == 50, "CelerityDictionary<int, DefaultHasher<int>>");

    var u32 = new CelerityDictionary<uint, int, UInt32Hasher>();
    u32[3000000000u] = 1;
    Check(u32.ContainsKey(3000000000u), "CelerityDictionary<uint, UInt32Hasher>");

    var u32w = new CelerityDictionary<uint, int, UInt32WangHasher>();
    u32w[3000000000u] = 1;
    Check(u32w.ContainsKey(3000000000u), "CelerityDictionary<uint, UInt32WangHasher>");

    var u32m = new CelerityDictionary<uint, int, UInt32Murmur3Hasher>();
    u32m[3000000000u] = 1;
    Check(u32m.ContainsKey(3000000000u), "CelerityDictionary<uint, UInt32Murmur3Hasher>");

    var u64 = new CelerityDictionary<ulong, int, UInt64Hasher>();
    u64[ulong.MaxValue] = 1;
    Check(u64.ContainsKey(ulong.MaxValue), "CelerityDictionary<ulong, UInt64Hasher>");

    var u64w = new CelerityDictionary<ulong, int, UInt64WangHasher>();
    u64w[ulong.MaxValue] = 1;
    Check(u64w.ContainsKey(ulong.MaxValue), "CelerityDictionary<ulong, UInt64WangHasher>");

    var u64wn = new CelerityDictionary<ulong, int, UInt64WangNaiveHasher>();
    u64wn[ulong.MaxValue] = 1;
    Check(u64wn.ContainsKey(ulong.MaxValue), "CelerityDictionary<ulong, UInt64WangNaiveHasher>");

    var murmurInt = new CelerityDictionary<int, int, Int32Murmur3Hasher>();
    murmurInt[1] = 1;
    Check(murmurInt.ContainsKey(1), "CelerityDictionary<int, Int32Murmur3Hasher>");

    // Identity hashers — the zero-work floor. Exercise the out-of-band zero-key
    // slot (Hash(0) == 0 == EMPTY_KEY) plus a dense sequential fill, the shape
    // identity is designed for.
    var identInt = new IntDictionary<string, Int32IdentityHasher>();
    identInt[0] = "zero";
    identInt[1] = "one";
    identInt[-1] = "neg-one";
    Check(identInt[0] == "zero" && identInt[1] == "one" && identInt[-1] == "neg-one"
        && !identInt.ContainsKey(999), "IntDictionary<string, Int32IdentityHasher>");

    var identIntSet = new IntSet<Int32IdentityHasher>();
    for (int i = 0; i < 256; i++) identIntSet.Add(i);
    Check(identIntSet.Count == 256 && identIntSet.Contains(0) && identIntSet.Contains(255)
        && !identIntSet.Contains(256), "IntSet<Int32IdentityHasher>");

    var identLong = new LongDictionary<string, Int64IdentityHasher>();
    identLong[0L] = "zero";
    identLong[1L] = "one";
    identLong[-1L] = "neg-one";
    Check(identLong[0L] == "zero" && identLong[1L] == "one" && identLong[-1L] == "neg-one"
        && !identLong.ContainsKey(999L), "LongDictionary<string, Int64IdentityHasher>");

    var identLongSet = new LongSet<Int64IdentityHasher>();
    for (long i = 0; i < 256; i++) identLongSet.Add(i);
    Check(identLongSet.Count == 256 && identLongSet.Contains(0L) && identLongSet.Contains(255L)
        && !identLongSet.Contains(256L), "LongSet<Int64IdentityHasher>");

    var wangInt = new CelerityDictionary<int, int, Int32WangHasher>();
    wangInt[1] = 1;
    Check(wangInt.ContainsKey(1), "CelerityDictionary<int, Int32WangHasher>");

    var wangLong = new CelerityDictionary<long, int, Int64WangHasher>();
    wangLong[1L] = 1;
    Check(wangLong.ContainsKey(1L), "CelerityDictionary<long, Int64WangHasher>");

    var murmurLong = new CelerityDictionary<long, int, Int64Murmur3Hasher>();
    murmurLong[1L] = 1;
    Check(murmurLong.ContainsKey(1L), "CelerityDictionary<long, Int64Murmur3Hasher>");
}

// PooledCelerityDictionary — ArrayPool-backed, disposable dictionary. Exercise the
// full surface plus the rent/return lifecycle (Dispose) so the AOT publish compiles
// the new generic instantiations and the ArrayPool<T?> code paths.
{
    using (var pooled = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>())
    {
        pooled[42] = 1;
        pooled[42]++;
        pooled[0] = 99; // out-of-band default key
        Check(pooled.TryAdd(7, 100), "PooledCelerityDictionary.TryAdd new key");
        Check(!pooled.TryAdd(7, 999), "PooledCelerityDictionary.TryAdd duplicate");
        Check(pooled[42] == 2 && pooled[0] == 99, "PooledCelerityDictionary round-trip");
        Check(pooled.Remove(7), "PooledCelerityDictionary.Remove");
        var sum = 0;
        foreach (var kvp in pooled) sum += kvp.Value;
        Check(sum == 2 + 99, "PooledCelerityDictionary enumeration");
        Check(pooled.Count == 2, "PooledCelerityDictionary count");
    }

    // Reference-type key/value instantiation exercises the clear-on-return path.
    using var pooledStr = new PooledCelerityDictionary<string, string, StringFnV1AHasher>();
    pooledStr[null!] = "null-key"; // out-of-band null key
    pooledStr["a"] = "alpha";
    Check(pooledStr[null!] == "null-key" && pooledStr["a"] == "alpha",
        "PooledCelerityDictionary<string, string> null-key + round-trip");
}

// Sets — IntSet, LongSet, CeleritySet.
{
    var s = new IntSet();
    s.Add(1);
    Check(s.TryAdd(2), "IntSet.TryAdd new");
    Check(!s.TryAdd(1), "IntSet.TryAdd duplicate");
    s.Add(0); // zero element out-of-band
    Check(s.Contains(0) && s.Contains(1), "IntSet.Contains");
    Check(s.Remove(2), "IntSet.Remove");
    var count = 0;
    foreach (var _ in s) count++;
    Check(count == s.Count && s.Count == 2, "IntSet enumeration/count");

    var ls = new LongSet();
    ls.Add(1L << 40);
    ls.Add(1L);
    Check(ls.Count == 2 && ls.Contains(1L << 40), "LongSet upper-bits distinctness");

    var gs = new CeleritySet<Guid, GuidHasher>();
    var g = Guid.NewGuid();
    gs.Add(g);
    gs.Add(Guid.Empty);
    Check(gs.Contains(g) && gs.Contains(Guid.Empty), "CeleritySet<Guid>");
}

// IEnumerable constructors (collection-count sizing path).
{
    var source = new Dictionary<int, int> { [1] = 1, [2] = 2, [3] = 3 };
    var d = new IntDictionary<int>(source);
    Check(d.Count == 3, "IntDictionary IEnumerable ctor");

    var setSource = new[] { 1, 2, 2, 3 };
    var set = new IntSet(setSource);
    Check(set.Count == 3, "IntSet IEnumerable ctor dedupe");
}

// FrozenCelerityDictionary — build-once perfect-hash dictionary (default and
// custom-hasher generic instantiations), the out-of-band null key, and the
// base-hash-collision fallback path ('A' / 'Ł' under the low-byte FNV-1a hasher).
{
    var frozen = new FrozenCelerityDictionary<int>(new[]
    {
        new KeyValuePair<string, int>("alice", 1),
        new KeyValuePair<string, int>("bob", 2),
        new KeyValuePair<string, int>(null!, 99),
    });
    Check(frozen.Count == 3 && frozen["alice"] == 1 && frozen[null!] == 99,
        "FrozenCelerityDictionary<int> build + null key");

    var frozenMurmur = new FrozenCelerityDictionary<int, StringMurmur3Hasher>(new[]
    {
        new KeyValuePair<string, int>("x", 10),
        new KeyValuePair<string, int>("y", 20),
    });
    Check(frozenMurmur["y"] == 20 && !frozenMurmur.ContainsKey("z"),
        "FrozenCelerityDictionary<int, StringMurmur3Hasher>");

    var frozenFallback = new FrozenCelerityDictionary<int, StringFnV1AHasher>(new[]
    {
        new KeyValuePair<string, int>("A", 1),
        new KeyValuePair<string, int>("Ł", 2),
    });
    Check(frozenFallback["A"] == 1 && frozenFallback["Ł"] == 2,
        "FrozenCelerityDictionary fallback keeps base-hash-colliding keys distinct");
}

// FrozenCeleritySet — build-once perfect-hash set (default and custom-hasher
// generic instantiations), the out-of-band null element, the IReadOnlySet surface,
// and the base-hash-collision fallback path ('A' / 'Ł' under the low-byte FNV-1a).
{
    var frozen = new FrozenCeleritySet(new[] { "alice", "bob", null! });
    Check(frozen.Count == 3 && frozen.Contains("alice") && frozen.Contains(null!),
        "FrozenCeleritySet build + null element");
    Check(frozen.IsSupersetOf(new[] { "alice" }) && frozen.Overlaps(new[] { "bob", "z" }),
        "FrozenCeleritySet IReadOnlySet surface");

    var frozenMurmur = new FrozenCeleritySet<StringMurmur3Hasher>(new[] { "x", "y" });
    Check(frozenMurmur.Contains("y") && !frozenMurmur.Contains("z"),
        "FrozenCeleritySet<StringMurmur3Hasher>");

    var frozenFallbackSet = new FrozenCeleritySet<StringFnV1AHasher>(new[] { "A", "Ł" });
    Check(frozenFallbackSet.Contains("A") && frozenFallbackSet.Contains("Ł") && frozenFallbackSet.Count == 2,
        "FrozenCeleritySet fallback keeps base-hash-colliding elements distinct");
}

// CelerityMultiMap — one-to-many map (default and custom-hasher generic
// instantiations), grouping Adds, the out-of-band default-key group, the two
// removal shapes, and the ILookup<,> surface.
{
    var multi = new CelerityMultiMap<string, int, StringFnV1AHasher>();
    multi.Add("a", 1);
    multi.Add("a", 2);
    multi.Add("b", 3);
    multi.Add(null!, 99); // out-of-band default-key group
    Check(multi.Count == 3 && multi.ValueCount == 4, "CelerityMultiMap counts");
    Check(multi["a"].Count == 2 && multi[null!][0] == 99, "CelerityMultiMap group + null key");
    Check(multi.Remove("a", 1) && multi["a"].Count == 1, "CelerityMultiMap.Remove single value");
    Check(multi.RemoveAll("b") && !multi.ContainsKey("b"), "CelerityMultiMap.RemoveAll");

    System.Linq.ILookup<string, int> lookup = multi;
    Check(lookup.Contains("a") && System.Linq.Enumerable.Count(lookup["a"]) == 1,
        "CelerityMultiMap ILookup surface");

    var multiGuid = new CelerityMultiMap<System.Guid, int, GuidHasher>();
    multiGuid.Add(System.Guid.Empty, 7);
    Check(multiGuid[System.Guid.Empty][0] == 7, "CelerityMultiMap<Guid, int, GuidHasher>");
}

// SmallDictionary — flat-array, linear-scan dictionary (default key inline, no
// hasher). Exercise the indexer, TryAdd/Add, TryGetValue, Remove, the swap-remove
// path, the inline default/zero key, and the struct enumerator.
{
    var d = new SmallDictionary<int, int>();
    d[42] = 1;
    d[42]++;
    Check(d.TryAdd(7, 100), "SmallDictionary.TryAdd new key");
    Check(!d.TryAdd(7, 999), "SmallDictionary.TryAdd duplicate");
    d.Add(8, 200);
    d[0] = 99; // zero key is an ordinary inline entry, not a sentinel
    Check(d.TryGetValue(42, out var v) && v == 2, "SmallDictionary indexer round-trip");
    Check(d[0] == 99, "SmallDictionary zero-key round-trip");
    Check(d.Remove(7), "SmallDictionary.Remove");
    var sum = 0;
    foreach (var kvp in d) sum += kvp.Value;
    Check(sum == 2 + 200 + 99, "SmallDictionary enumeration");
    Check(d.Count == 3, "SmallDictionary count");

    var byStr = new SmallDictionary<string, int>(new[]
    {
        new KeyValuePair<string, int>("a", 1),
        new KeyValuePair<string, int>("b", 2),
    });
    byStr[null!] = 99; // null key is an ordinary inline entry
    Check(byStr["a"] == 1 && byStr[null!] == 99 && byStr.Count == 3,
        "SmallDictionary<string, int> IEnumerable ctor + null key");
}

// SwissDictionary — SIMD group-probing dictionary (default key out-of-band, like
// the other hash-table dictionaries). Exercise the indexer, TryAdd/Add,
// TryGetValue, Remove (tombstone path), the out-of-band zero / null key, resize
// under collision, and the struct enumerator, across a spread of hashers so the
// Vector128 group-compare path is compiled to native code under AOT.
{
    var d = new SwissDictionary<int, int, Int32WangNaiveHasher>();
    d[42] = 1;
    d[42]++;
    Check(d.TryAdd(7, 100), "SwissDictionary.TryAdd new key");
    Check(!d.TryAdd(7, 999), "SwissDictionary.TryAdd duplicate");
    d.Add(8, 200);
    d[0] = 99; // zero key stored out-of-band, never hashed
    Check(d.TryGetValue(42, out var v) && v == 2, "SwissDictionary indexer round-trip");
    Check(d[0] == 99, "SwissDictionary zero-key round-trip");
    Check(d.Remove(7), "SwissDictionary.Remove (tombstone)");
    var sum = 0;
    foreach (var kvp in d) sum += kvp.Value;
    Check(sum == 2 + 200 + 99, "SwissDictionary enumeration");
    Check(d.Count == 3, "SwissDictionary count");

    // Force several resizes / group overflows to compile the rehash + SIMD probe.
    var grow = new SwissDictionary<int, int, Int32WangNaiveHasher>(capacity: 16);
    for (int i = 1; i <= 500; i++) grow[i] = i * 3;
    bool ok = true;
    for (int i = 1; i <= 500; i++) ok &= grow[i] == i * 3;
    Check(ok && grow.Count == 500, "SwissDictionary resize round-trip");

    var byStr = new SwissDictionary<string, int, StringMurmur3Hasher>(new[]
    {
        new KeyValuePair<string, int>("alice", 1),
        new KeyValuePair<string, int>("bob", 2),
    });
    byStr[null!] = 99; // null key stored out-of-band
    Check(byStr["alice"] == 1 && byStr[null!] == 99 && byStr.Count == 3,
        "SwissDictionary<string, int> IEnumerable ctor + null key");

    var byGuid = new SwissDictionary<Guid, string, GuidHasher>();
    byGuid[Guid.Empty] = "empty"; // out-of-band default-key slot
    var gid = Guid.NewGuid();
    byGuid[gid] = "alice";
    Check(byGuid[gid] == "alice" && byGuid[Guid.Empty] == "empty",
        "SwissDictionary<Guid> round-trip + empty-key slot");
}

// HashCachingDictionary — struct-of-arrays dictionary with a cached-fingerprint
// side array (default key out-of-band, like the other hash-table dictionaries).
// Exercise the indexer, TryAdd/Add, TryGetValue, Remove (backward-shift path),
// the out-of-band zero / null key, resize under collision, and the struct
// enumerator, across a spread of hashers so the fingerprint probe path is
// compiled to native code under AOT.
{
    var d = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
    d[42] = 1;
    d[42]++;
    Check(d.TryAdd(7, 100), "HashCachingDictionary.TryAdd new key");
    Check(!d.TryAdd(7, 999), "HashCachingDictionary.TryAdd duplicate");
    d.Add(8, 200);
    d[0] = 99; // zero key stored out-of-band, never hashed
    Check(d.TryGetValue(42, out var v) && v == 2, "HashCachingDictionary indexer round-trip");
    Check(d[0] == 99, "HashCachingDictionary zero-key round-trip");
    Check(d.Remove(7), "HashCachingDictionary.Remove (backward-shift)");
    var sum = 0;
    foreach (var kvp in d) sum += kvp.Value;
    Check(sum == 2 + 200 + 99, "HashCachingDictionary enumeration");
    Check(d.Count == 3, "HashCachingDictionary count");

    // Force several resizes / collision clusters to compile the rehash + probe.
    var grow = new HashCachingDictionary<int, int, Int32WangNaiveHasher>(capacity: 16);
    for (int i = 1; i <= 500; i++) grow[i] = i * 3;
    bool ok = true;
    for (int i = 1; i <= 500; i++) ok &= grow[i] == i * 3;
    Check(ok && grow.Count == 500, "HashCachingDictionary resize round-trip");

    var byStr = new HashCachingDictionary<string, int, StringMurmur3Hasher>(new[]
    {
        new KeyValuePair<string, int>("alice", 1),
        new KeyValuePair<string, int>("bob", 2),
    });
    byStr[null!] = 99; // null key stored out-of-band
    Check(byStr["alice"] == 1 && byStr[null!] == 99 && byStr.Count == 3,
        "HashCachingDictionary<string, int> IEnumerable ctor + null key");

    var byGuid = new HashCachingDictionary<Guid, string, GuidHasher>();
    byGuid[Guid.Empty] = "empty"; // out-of-band default-key slot
    var gid = Guid.NewGuid();
    byGuid[gid] = "alice";
    Check(byGuid[gid] == "alice" && byGuid[Guid.Empty] == "empty",
        "HashCachingDictionary<Guid> round-trip + empty-key slot");
}

// BloomFilter — probabilistic membership filter (no out-of-band slot; default(T) is
// an ordinary element, a null reference is mapped to a fixed base hash so the hasher
// is never called with null). Exercise Add / Contains / Clear / Count / UnionWith and
// the IEnumerable ctor across int / Guid / string instantiations so the AOT publish
// compiles the double-hashing probe path and the popcount-based fill estimate.
{
    var filter = new BloomFilter<int, Int32WangNaiveHasher>(1000);
    filter.Add(42);
    filter.Add(0); // zero is an ordinary element, not a sentinel
    Check(filter.Contains(42) && filter.Contains(0), "BloomFilter add/contains");
    Check(!filter.Contains(7), "BloomFilter negative lookup");
    Check(filter.Count == 2, "BloomFilter count");
    Check(filter.BitCount >= 64 && (filter.BitCount & (filter.BitCount - 1)) == 0,
        "BloomFilter power-of-two bit count");
    Check(filter.HashCount >= 1, "BloomFilter hash count");

    // No false negatives across a larger fill.
    var big = new BloomFilter<int, Int32WangNaiveHasher>(1000);
    for (int i = 1; i <= 500; i++) big.Add(i * 3);
    bool noFalseNegatives = true;
    for (int i = 1; i <= 500; i++) noFalseNegatives &= big.Contains(i * 3);
    Check(noFalseNegatives, "BloomFilter no false negatives");
    Check(big.CurrentFalsePositiveProbability > 0d, "BloomFilter current FP probability");

    // UnionWith merges two equally-sized filters.
    var other = new BloomFilter<int, Int32WangNaiveHasher>(1000);
    other.Add(99999);
    filter.UnionWith(other);
    Check(filter.Contains(99999), "BloomFilter UnionWith");

    filter.Clear();
    Check(filter.Count == 0 && !filter.Contains(42), "BloomFilter clear");

    // String elements via the IEnumerable ctor, plus the out-of-band null reference
    // (StringFnV1AHasher throws on null; BloomFilter must not call it with null).
    var strFilter = new BloomFilter<string, StringFnV1AHasher>(new[] { "alice", "bob" });
    strFilter.Add(null!);
    Check(strFilter.Contains("alice") && strFilter.Contains("bob") && strFilter.Contains(null!),
        "BloomFilter<string> ctor + null element");

    var guidFilter = new BloomFilter<Guid, GuidHasher>(100);
    guidFilter.Add(Guid.Empty); // ordinary element, no out-of-band slot
    Check(guidFilter.Contains(Guid.Empty), "BloomFilter<Guid> empty-guid element");
}

// BitSet — dense exact bit vector. Exercise Set / Get / Flip / SetAll / Count
// (popcount), the SIMD-accelerated bulk And / Or / Xor / Not, the tail-bit masking
// past Length, and both enumerators so the AOT publish compiles the Vector<ulong>
// bulk paths and the TrailingZeroCount set-bit walk.
{
    var bits = new BitSet(130); // 3 words, 62 tail bits past Length
    bits.Set(0, true);
    bits[64] = true;
    bits[129] = true;
    Check(bits.Length == 130 && bits.Count == 3, "BitSet set + popcount");
    Check(bits[0] && bits[64] && bits[129] && !bits[1], "BitSet get");
    Check(bits.Flip(1) && bits[1], "BitSet flip");
    bits.Set(1, false);

    bits.SetAll(true);
    Check(bits.Count == 130 && bits.All(), "BitSet SetAll + tail masking");
    bits.Not();
    Check(bits.Count == 0 && bits.None(), "BitSet Not");

    var a = new BitSet(1000);
    var b = new BitSet(1000);
    for (int i = 0; i < 1000; i += 2) a[i] = true;     // evens
    for (int i = 0; i < 1000; i += 3) b[i] = true;     // multiples of 3
    var union = new BitSet(1000);
    union.Or(a).Or(b);
    bool orOk = true;
    for (int i = 0; i < 1000; i++) orOk &= union[i] == (i % 2 == 0 || i % 3 == 0);
    Check(orOk, "BitSet SIMD Or");

    var inter = new BitSet((bool[])ToBoolArray(a));
    inter.And(b);
    bool andOk = true;
    for (int i = 0; i < 1000; i++) andOk &= inter[i] == (i % 2 == 0 && i % 3 == 0);
    Check(andOk, "BitSet SIMD And");

    var sparse = new BitSet(300);
    sparse[7] = true;
    sparse[256] = true;
    var setBits = new List<int>();
    foreach (int idx in sparse.EnumerateSetBits()) setBits.Add(idx);
    Check(setBits.Count == 2 && setBits[0] == 7 && setBits[1] == 256, "BitSet EnumerateSetBits");

    int trueCount = 0;
    foreach (bool bit in sparse) if (bit) trueCount++;
    Check(trueCount == 2, "BitSet value enumerator");

    static bool[] ToBoolArray(BitSet src)
    {
        var arr = new bool[src.Length];
        for (int i = 0; i < src.Length; i++) arr[i] = src[i];
        return arr;
    }
}

// HyperLogLog — probabilistic cardinality estimator (no out-of-band slot; default(T)
// is an ordinary element, a null reference is mapped to a fixed base hash so the hasher
// is never called with null). Exercise Add / EstimateCardinality / Clear / UnionWith
// and the IEnumerable ctor across int / Guid / string instantiations so the AOT publish
// compiles the SplitMix64 avalanche, the LeadingZeroCount rank path, and the harmonic-
// mean estimate with linear-counting correction.
{
    var hll = new HyperLogLog<int, Int32WangNaiveHasher>();
    hll.Add(42);
    hll.Add(0); // zero is an ordinary element, not a sentinel
    hll.Add(42); // duplicate collapses
    Check(hll.EstimateCardinality() == 2, "HyperLogLog distinct count");
    Check(hll.Precision == HyperLogLog<int, Int32WangNaiveHasher>.DEFAULT_PRECISION,
        "HyperLogLog default precision");
    Check(hll.RegisterCount == 1 << 14, "HyperLogLog register count");
    Check(hll.StandardError > 0d, "HyperLogLog standard error");

    // Larger fill: estimate must land within a few standard errors of the truth.
    var big = new HyperLogLog<int, Int32WangNaiveHasher>();
    for (int i = 0; i < 50_000; i++) big.Add(i);
    long estimate = big.EstimateCardinality();
    double relErr = Math.Abs(estimate - 50_000) / 50_000.0;
    Check(relErr <= big.StandardError * 4 + 0.01, "HyperLogLog estimate within bound");

    // UnionWith merges two equal-precision estimators (disjoint streams).
    var other = new HyperLogLog<int, Int32WangNaiveHasher>();
    for (int i = 50_000; i < 100_000; i++) other.Add(i);
    big.UnionWith(other);
    long union = big.EstimateCardinality();
    Check(Math.Abs(union - 100_000) / 100_000.0 <= big.StandardError * 4 + 0.01,
        "HyperLogLog UnionWith");

    hll.Clear();
    Check(hll.EstimateCardinality() == 0, "HyperLogLog clear");

    // String elements via the IEnumerable ctor, plus the out-of-band null reference
    // (StringFnV1AHasher throws on null; HyperLogLog must not call it with null).
    var strHll = new HyperLogLog<string, StringFnV1AHasher>(new[] { "alice", "bob", "alice" });
    strHll.Add(null!);
    Check(strHll.EstimateCardinality() == 3, "HyperLogLog<string> ctor + null element");

    var guidHll = new HyperLogLog<Guid, GuidHasher>();
    guidHll.Add(Guid.Empty); // ordinary element, no out-of-band slot
    Check(guidHll.EstimateCardinality() == 1, "HyperLogLog<Guid> empty-guid element");
}

// CountMinSketch — probabilistic frequency estimator (no out-of-band slot; default(T)
// is an ordinary element, a null reference is mapped to a fixed base hash so the hasher
// is never called with null). Exercise Add / Add(count) / EstimateCount / Clear /
// UnionWith and the IEnumerable ctor across int / Guid / string instantiations so the
// AOT publish compiles the SplitMix64 avalanche and the double-hashing column probe.
{
    var cms = new CountMinSketch<int, Int32WangNaiveHasher>();
    cms.Add(42, 5);
    cms.Add(0, 3); // zero is an ordinary element, not a sentinel
    cms.Add(42);   // 42 now totals 6
    Check(cms.EstimateCount(42) >= 6, "CountMinSketch never underestimates");
    Check(cms.EstimateCount(0) >= 3, "CountMinSketch zero-element count");
    Check(cms.TotalCount == 9, "CountMinSketch total count");
    Check(cms.Width >= 4 && (cms.Width & (cms.Width - 1)) == 0, "CountMinSketch power-of-two width");
    Check(cms.Depth >= 1, "CountMinSketch positive depth");

    // No underestimates across a larger skewed fill.
    var big = new CountMinSketch<int, Int32WangNaiveHasher>(0.001, 0.01);
    var truth = new Dictionary<int, long>();
    for (int i = 0; i < 50_000; i++)
    {
        int key = i % 500;
        big.Add(key);
        truth[key] = truth.GetValueOrDefault(key) + 1;
    }
    bool noUnderestimate = true;
    foreach (var (key, count) in truth)
        noUnderestimate &= big.EstimateCount(key) >= count;
    Check(noUnderestimate, "CountMinSketch no underestimates over a large fill");

    // UnionWith merges two equally-sized sketches.
    var other = new CountMinSketch<int, Int32WangNaiveHasher>();
    other.Add(99999, 4);
    cms.UnionWith(other);
    Check(cms.EstimateCount(99999) >= 4, "CountMinSketch UnionWith");

    cms.Clear();
    Check(cms.TotalCount == 0 && cms.EstimateCount(42) == 0, "CountMinSketch clear");

    // String elements via the IEnumerable ctor, plus the out-of-band null reference
    // (StringFnV1AHasher throws on null; CountMinSketch must not call it with null).
    var strCms = new CountMinSketch<string, StringFnV1AHasher>(new[] { "alice", "alice", "bob" });
    strCms.Add(null!);
    Check(strCms.EstimateCount("alice") >= 2 && strCms.EstimateCount(null!) >= 1,
        "CountMinSketch<string> ctor + null element");

    var guidCms = new CountMinSketch<Guid, GuidHasher>();
    guidCms.Add(Guid.Empty, 2); // ordinary element, no out-of-band slot
    Check(guidCms.EstimateCount(Guid.Empty) >= 2, "CountMinSketch<Guid> empty-guid element");
}

// FastUtils.FastMod / FastDiv (#191) — Lemire reciprocal modulo / division, 32- and 64-bit.
// Forces the BigMul / UInt128 reciprocal paths to compile under Native AOT and confirms they
// reproduce the built-in operators on the native runtime.
{
    const uint d32 = 1000u;
    ulong m32 = FastUtils.GetFastModMultiplier(d32);
    bool ok32 = true;
    for (uint value = 0; value < 50_000; value++)
    {
        if (FastUtils.FastMod(value, d32, m32) != value % d32) { ok32 = false; break; }
        if (FastUtils.FastDiv(value, m32) != value / d32) { ok32 = false; break; }
    }
    Check(ok32, "FastUtils.FastMod/FastDiv (32-bit) match operators");

    const ulong d64 = 1_000_000_007UL;
    UInt128 m64 = FastUtils.GetFastModMultiplier(d64);
    ulong[] samples64 = { 0, 1, d64 - 1, d64, d64 + 1, 123_456_789_012_345UL, ulong.MaxValue };
    bool ok64 = true;
    foreach (ulong value in samples64)
    {
        if (FastUtils.FastMod(value, d64, m64) != value % d64) { ok64 = false; break; }
        if (FastUtils.FastDiv(value, m64) != value / d64) { ok64 = false; break; }
    }
    Check(ok64, "FastUtils.FastMod/FastDiv (64-bit) match operators");
}

// Struct PRNG suite (#192) — value-type, allocation-free, seed-deterministic generators. Exercise every
// generator's NextUInt64 plus the constrained-generic RandomSourceExtensions surface (NextUInt32 /
// NextDouble / NextSingle / NextBool / bounded NextInt / NextInt64 / NextBytes) and a generic algorithm
// driven through the `where TRng : struct, IRandomSource` path, so the Native AOT publish compiles each
// generic instantiation (the SplitMix64 seeding, the UInt128 wyrand fold, and the Lemire bounded range).
{
    static void ExerciseRng<TRng>(TRng seeded, string name, ref int fails) where TRng : struct, IRandomSource
    {
        // Determinism: a fresh copy from the same state reproduces the stream.
        var a = seeded;
        var b = seeded;
        bool deterministic = true;
        for (int i = 0; i < 100; i++)
            if (a.NextUInt64() != b.NextUInt64()) deterministic = false;
        if (!deterministic) { Console.Error.WriteLine($"FAIL: {name} NextUInt64 determinism"); fails++; }

        var rng = seeded;
        bool ranges = true;
        for (int i = 0; i < 10_000; i++)
        {
            if (rng.NextDouble() is < 0.0 or >= 1.0) ranges = false;
            if (rng.NextSingle() is < 0.0f or >= 1.0f) ranges = false;
            int bounded = rng.NextInt(1, 7);
            if (bounded is < 1 or > 6) ranges = false;
            long bounded64 = rng.NextInt64(-1_000_000_000L, 1_000_000_000L);
            if (bounded64 is < -1_000_000_000L or >= 1_000_000_000L) ranges = false;
            rng.NextBool();
            rng.NextUInt32();
        }
        if (!ranges) { Console.Error.WriteLine($"FAIL: {name} derived-range"); fails++; }

        Span<byte> buf = stackalloc byte[21];
        rng.NextBytes(buf);

        // Generic Fisher-Yates shuffle through the constrained-generic path yields a permutation.
        var shuffleRng = seeded;
        var arr = new int[64];
        for (int i = 0; i < arr.Length; i++) arr[i] = i;
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = shuffleRng.NextInt(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        Array.Sort(arr);
        bool permutation = true;
        for (int i = 0; i < arr.Length; i++) if (arr[i] != i) permutation = false;
        if (!permutation) { Console.Error.WriteLine($"FAIL: {name} generic shuffle permutation"); fails++; }
    }

    ExerciseRng(new SplitMix64(0xABCDEF), nameof(SplitMix64), ref failures);
    ExerciseRng(new Xoshiro256StarStar(0xABCDEF), nameof(Xoshiro256StarStar), ref failures);
    ExerciseRng(new Xoroshiro128Plus(0xABCDEF), nameof(Xoroshiro128Plus), ref failures);
    ExerciseRng(new WyRand(0xABCDEF), nameof(WyRand), ref failures);
    ExerciseRng(new Pcg32(0xABCDEF), nameof(Pcg32), ref failures);

    // Pcg32's native 32-bit output and independent-stream feature.
    var pcgStreamA = new Pcg32(42, 1);
    var pcgStreamB = new Pcg32(42, 2);
    Check(pcgStreamA.NextUInt32() != pcgStreamB.NextUInt32(), "Pcg32 independent streams differ");
}

if (failures == 0)
{
    Console.WriteLine("Celerity AOT smoke test: all checks passed.");
    return 0;
}

Console.Error.WriteLine($"Celerity AOT smoke test: {failures} check(s) failed.");
return 1;
