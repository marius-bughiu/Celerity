// Native AOT smoke test for Celerity (#32).
//
// This console app exercises every collection shape and a representative spread
// of hashers so that `dotnet publish /p:PublishAot=true` is forced to compile
// each generic instantiation down to native code. It is run by the AOT CI job:
// a non-zero exit code (any failed assertion) fails the build, proving the
// library works end-to-end under Native AOT, not just that the static analyzers
// are happy.

using Celerity.Collections;
using Celerity.Hashing;

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

if (failures == 0)
{
    Console.WriteLine("Celerity AOT smoke test: all checks passed.");
    return 0;
}

Console.Error.WriteLine($"Celerity AOT smoke test: {failures} check(s) failed.");
return 1;
