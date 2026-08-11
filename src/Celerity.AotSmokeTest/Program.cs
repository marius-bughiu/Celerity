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
using Celerity.Sorting;

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

    var u32 = new CelerityDictionary<uint, int, UInt32WangNaiveHasher>();
    u32[3000000000u] = 1;
    Check(u32.ContainsKey(3000000000u), "CelerityDictionary<uint, UInt32WangNaiveHasher>");

    var u32w = new CelerityDictionary<uint, int, UInt32WangHasher>();
    u32w[3000000000u] = 1;
    Check(u32w.ContainsKey(3000000000u), "CelerityDictionary<uint, UInt32WangHasher>");

    var u32m = new CelerityDictionary<uint, int, UInt32Murmur3Hasher>();
    u32m[3000000000u] = 1;
    Check(u32m.ContainsKey(3000000000u), "CelerityDictionary<uint, UInt32Murmur3Hasher>");

    var u64 = new CelerityDictionary<ulong, int, UInt64Murmur3Hasher>();
    u64[ulong.MaxValue] = 1;
    Check(u64.ContainsKey(ulong.MaxValue), "CelerityDictionary<ulong, UInt64Murmur3Hasher>");

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

    // The read-only interface is declared alongside ISet<T>; reaching the queries through it
    // pins that the explicit forwarders survive trimming on a hasher-parameterized generic.
    IReadOnlySet<int> ro = s;
    Check(ro.Count == 2 && ro.Contains(0) && ro.IsSubsetOf(new[] { 0, 1, 9 }) && !ro.Overlaps(new[] { 7 }),
        "IntSet IReadOnlySet<int>");
    Check(((IReadOnlySet<Guid>)gs).SetEquals(new[] { g, Guid.Empty }), "CeleritySet IReadOnlySet<T>");
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

// CelerityMultiSet — counting multiset (element -> multiplicity): counting Adds,
// the out-of-band default/null element, the two removal shapes, SetCount, and the
// (element, count) enumeration.
{
    var bag = new CelerityMultiSet<string, StringFnV1AHasher>();
    bag.Add("a");
    bag.Add("a");
    bag.Add("b", 3);
    bag.Add(null!, 2); // out-of-band default element
    Check(bag.Count == 3 && bag.TotalCount == 7, "CelerityMultiSet counts");
    Check(bag["a"] == 2 && bag[null!] == 2, "CelerityMultiSet multiplicity + null element");
    Check(bag.Remove("a") && bag["a"] == 1, "CelerityMultiSet.Remove decrements");
    Check(bag.RemoveAll("b") && !bag.Contains("b"), "CelerityMultiSet.RemoveAll");
    Check(bag.SetCount("c", 5) == 0 && bag["c"] == 5, "CelerityMultiSet.SetCount creates");
    Check(bag.SetCount("c", 0) == 5 && !bag.Contains("c"), "CelerityMultiSet.SetCount removes");

    int distinct = 0;
    foreach (var pair in bag) distinct += pair.Value > 0 ? 1 : 0;
    Check(distinct == bag.Count, "CelerityMultiSet enumeration");

    var bagFromSeq = new CelerityMultiSet<int, Int32WangNaiveHasher>(new[] { 1, 1, 2 });
    Check(bagFromSeq[1] == 2 && bagFromSeq[2] == 1, "CelerityMultiSet IEnumerable<T> counting ctor");
}

// LruCache — fixed-capacity least-recently-used cache. Exercise put/get, the
// recency-preserving eviction, a promoting read sparing an entry, peek/remove, the
// out-of-band default/zero key, and the MRU->LRU struct enumerator.
{
    var cache = new LruCache<int, string, Int32WangNaiveHasher>(3);
    cache[0] = "zero"; // out-of-band default key
    cache[1] = "one";
    cache[2] = "two";
    Check(cache.Count == 3 && cache[0] == "zero", "LruCache put/get + default key");

    _ = cache[0];      // promote 0 -> MRU..LRU = 0, 2, 1
    cache[3] = "three"; // evicts the least-recently-used (1), not 0
    Check(!cache.ContainsKey(1) && cache.ContainsKey(0), "LruCache evicts LRU, spares read");

    Check(cache.TryPeek(0, out string? peeked) && peeked == "zero", "LruCache TryPeek");
    Check(cache.TryPeekLeastRecentlyUsed(out int lruKey, out _) && lruKey == 2, "LruCache peek LRU");
    Check(cache.Remove(2, out string? removed) && removed == "two", "LruCache Remove out value");

    var order = new List<int>();
    foreach (var kvp in cache) order.Add(kvp.Key);
    Check(order.Count == 2 && order[0] == 3, "LruCache MRU-first enumeration");

    var seeded = new LruCache<int, int, Int32WangNaiveHasher>(2,
        new[] { new KeyValuePair<int, int>(1, 10), new KeyValuePair<int, int>(2, 20), new KeyValuePair<int, int>(3, 30) });
    Check(seeded.Count == 2 && !seeded.ContainsKey(1) && seeded.ContainsKey(3), "LruCache source ctor evicts oldest");
}

// Deque — growable double-ended queue over a circular buffer. Exercise both-ends push/pop,
// the front-relative indexer, wrap-around growth, Try* peeks, the front-to-back struct
// enumerator, and the IEnumerable constructor.
{
    var dq = new Deque<int>(new[] { 1, 2, 3 }); // front-to-back: 1, 2, 3
    dq.PushFront(0);  // [0, 1, 2, 3]
    dq.PushBack(4);   // [0, 1, 2, 3, 4]
    Check(dq.Count == 5 && dq[0] == 0 && dq[4] == 4, "Deque push both ends + indexer");
    Check(dq.PopFront() == 0 && dq.PopBack() == 4, "Deque pop both ends");
    Check(dq.PeekFront() == 1 && dq.PeekBack() == 3, "Deque peek both ends");

    // Force wrap-around and growth over a small buffer.
    var churn = new Deque<int>(4);
    for (int i = 0; i < 100; i++) churn.PushBack(i);
    for (int i = 0; i < 50; i++) Check(churn.PopFront() == i, "Deque wrap-around FIFO churn");
    Check(churn.Count == 50 && churn[0] == 50, "Deque count after churn");

    Check(churn.TryPeekFront(out int f) && f == 50, "Deque TryPeekFront");
    var empty = new Deque<int>();
    Check(!empty.TryPopBack(out _), "Deque TryPopBack on empty");

    var order = new List<int>();
    var seq = new Deque<int>();
    seq.PushBack(2); seq.PushFront(1); seq.PushBack(3);
    foreach (int x in seq) order.Add(x);
    Check(order.Count == 3 && order[0] == 1 && order[2] == 3, "Deque front-to-back enumeration");
}

// DisjointSet — union-find over arbitrary elements. Exercise add, auto-adding union, the
// merge/no-op return, representative find, connectivity queries, component sizing, the set
// count, growth across many singletons, grouped components, and the struct enumerator.
{
    var ds = new DisjointSet<int>(new[] { 1, 2, 3, 4 });
    Check(ds.Count == 4 && ds.SetCount == 4, "DisjointSet seeds singletons");

    Check(ds.Union(1, 2) && ds.Union(3, 4), "DisjointSet union merges");
    Check(ds.Union(2, 3), "DisjointSet union joins two components");
    Check(!ds.Union(1, 4), "DisjointSet union of already-connected is a no-op");
    Check(ds.SetCount == 1 && ds.Connected(1, 4), "DisjointSet all connected");
    Check(ds.ComponentSize(1) == 4 && ds.Find(1).Equals(ds.Find(4)), "DisjointSet component size + shared representative");

    Check(ds.Union(10, 20), "DisjointSet union auto-adds missing elements");
    Check(ds.Contains(10) && !ds.Connected(1, 10), "DisjointSet distinct components");

    var grown = new DisjointSet<int>(0);
    for (int i = 1; i < 500; i++) grown.Union(i - 1, i);
    Check(grown.Count == 500 && grown.SetCount == 1 && grown.ComponentSize(0) == 500, "DisjointSet chain union across growth");

    var comps = ds.GetComponents();
    Check(comps.Count == ds.SetCount, "DisjointSet GetComponents count");

    var order = new List<int>();
    foreach (int x in new DisjointSet<int>(new[] { 7, 8, 9 })) order.Add(x);
    Check(order.Count == 3 && order[0] == 7 && order[2] == 9, "DisjointSet insertion-order enumeration");
}

// IndexedPriorityQueue — addressable binary min-heap. Exercise enqueue/peek/dequeue
// min-order, the decrease-key Update, arbitrary Remove, priority lookups, and growth.
{
    var pq = new IndexedPriorityQueue<int, int, Int32WangNaiveHasher>();
    pq.Enqueue(1, 30);
    pq.Enqueue(2, 10);
    pq.Enqueue(3, 20);
    Check(pq.Count == 3 && pq.Peek() == 2, "IndexedPriorityQueue min at top");
    Check(!pq.TryEnqueue(2, 5), "IndexedPriorityQueue rejects duplicate element");

    pq.Update(3, 1); // decrease-key
    Check(pq.Peek() == 3 && pq.GetPriority(3) == 1, "IndexedPriorityQueue decrease-key");
    Check(pq.Remove(1, out int removed) && removed == 30, "IndexedPriorityQueue remove arbitrary out value");
    Check(pq.TryGetPriority(2, out int p2) && p2 == 10 && !pq.Contains(1), "IndexedPriorityQueue priority lookup + absence");

    Check(pq.Dequeue() == 3 && pq.Dequeue() == 2 && pq.Count == 0, "IndexedPriorityQueue dequeue order");

    var grown = new IndexedPriorityQueue<int, int, Int32WangNaiveHasher>(0);
    for (int i = 500; i > 0; i--) grown.Enqueue(i, i);
    Check(grown.Count == 500 && grown.Peek() == 1, "IndexedPriorityQueue enqueue across growth");
    var prev = int.MinValue;
    var monotonic = true;
    while (grown.TryDequeue(out _, out int pr)) { if (pr < prev) monotonic = false; prev = pr; }
    Check(monotonic, "IndexedPriorityQueue drains in ascending priority order");

    var maxHeap = new IndexedPriorityQueue<string, int, DefaultHasher<string>>(
        Comparer<int>.Create((a, b) => b.CompareTo(a)));
    maxHeap.Enqueue("a", 1);
    maxHeap.Enqueue("b", 3);
    maxHeap.Enqueue("c", 2);
    Check(maxHeap.Dequeue() == "b", "IndexedPriorityQueue custom comparer (max-heap)");
}

// SparseSet — bounded-universe sparse integer set (Briggs–Torczon). Exercise add /
// contains / swap-remove, the out-of-range rejection, the O(1) clear-then-reuse path
// (which must reject stale sparse entries), and the dense-array enumerator.
{
    var ss = new SparseSet(64);
    for (int i = 0; i < 10; i++) ss.Add(i);
    Check(ss.Count == 10 && ss.Universe == 64 && ss.Contains(0) && ss.Contains(9), "SparseSet add + contains");
    Check(!ss.TryAdd(5), "SparseSet.TryAdd duplicate");
    Check(!ss.Contains(64) && !ss.Contains(-1), "SparseSet out-of-range reads absent");
    Check(ss.Remove(5) && !ss.Contains(5) && ss.Contains(9), "SparseSet swap-remove keeps survivors");

    ss.Clear();
    Check(ss.Count == 0 && !ss.Contains(0) && !ss.Contains(9), "SparseSet O(1) clear rejects stale entries");
    ss.Add(9); // 9 was present before Clear — must not false-positive until re-added
    Check(ss.Count == 1 && ss.Contains(9) && !ss.Contains(0), "SparseSet reusable after clear");

    var reached = new SparseSet(128, new[] { 3, 3, 7, 1, 7 }); // dedupes
    var seen = new List<int>();
    foreach (int x in reached) seen.Add(x);
    Check(reached.Count == 3 && seen.Count == 3, "SparseSet source ctor dedupe + enumeration");
    ((ISet<int>)reached).UnionWith(new[] { 1, 2 });
    Check(reached.Count == 4 && reached.Contains(2), "SparseSet ISet<int> union within universe");
}

// CompressedIntSet — chunk-compressed 32-bit integer set. Drive every one of the three
// container forms (sorted array, bitmap, run-length) through the AOT compiler, plus the
// chunk-wise set algebra, the range add that produces runs, and Optimize.
{
    var cis = new CompressedIntSet(new[] { 5, 5, -3, 900_000, int.MinValue, int.MaxValue });
    Check(cis.Count == 5 && cis.Cardinality == 5, "CompressedIntSet source ctor dedupe");
    Check(cis.Contains(int.MinValue) && cis.Contains(int.MaxValue) && !cis.Contains(0),
        "CompressedIntSet spans the whole int range");

    var order = new List<int>();
    foreach (int x in cis) order.Add(x);
    Check(order.Count == 5 && order[0] == int.MinValue && order[4] == int.MaxValue,
        "CompressedIntSet enumerates in ascending signed order");

    // Past the array→bitmap crossover, then back down via Optimize.
    var dense = new CompressedIntSet();
    for (int i = 0; i < 5000; i++) dense.TryAdd(i * 2);
    Check(dense.Count == 5000 && dense.MemoryUsageInBytes >= 8192, "CompressedIntSet bitmap promotion");

    // A range add on a fresh chunk is stored as a single run pair.
    var runs = new CompressedIntSet();
    Check(runs.AddRange(1_000_000, 1_100_000) == 100_001, "CompressedIntSet AddRange");
    runs.Optimize();
    Check(runs.Count == 100_001 && runs.MemoryUsageInBytes < 1024, "CompressedIntSet run encoding");
    Check(runs.Contains(1_050_000) && !runs.Contains(1_100_001), "CompressedIntSet run probe");

    var left = new CompressedIntSet(new[] { 1, 2, 3, 900_000 });
    var right = new CompressedIntSet(new[] { 2, 3, 4 });
    Check(left.IntersectCount(right) == 2, "CompressedIntSet IntersectCount");
    left.IntersectWith(right);
    Check(left.Count == 2 && left.Contains(2) && left.Contains(3), "CompressedIntSet chunk-wise intersect");
    ((ISet<int>)left).UnionWith(new[] { -1, 3 });
    Check(left.Count == 3 && left.Contains(-1), "CompressedIntSet ISet<int> union");
    Check(((IReadOnlySet<int>)left).IsSubsetOf(new[] { -1, 2, 3, 7 }), "CompressedIntSet IReadOnlySet<int>");
}

// FenwickTree — Binary Indexed Tree over a numeric sequence. This is the one collection
// built on generic math (INumber<T>), so the static abstract interface members resolve
// through constrained calls the AOT compiler must specialize per T — worth pinning here
// over more than one T. Exercise the O(n) seeded build, point update, prefix / range sums,
// the indexer round-trip, the no-op update, clear-then-reuse, and the struct enumerator.
{
    var ft = new FenwickTree<long>(new long[] { 3, 1, 4, 1, 5, 9 });
    Check(ft.Count == 6 && ft.Total == 23, "FenwickTree seeded build + total");
    Check(ft.PrefixSum(0) == 0 && ft.PrefixSum(3) == 8 && ft.PrefixSum(6) == 23, "FenwickTree prefix sums");
    Check(ft.RangeSum(2, 5) == 10 && ft.RangeSum(4, 4) == 0, "FenwickTree range sum + empty range");

    ft.Add(0, 10);
    Check(ft[0] == 13 && ft.Total == 33, "FenwickTree point update");
    ft[1] = 100;
    Check(ft[1] == 100 && ft.Total == 132, "FenwickTree indexer set");

    var before = new List<long>();
    foreach (long v in ft) before.Add(v);
    Check(before.Count == 6 && before[0] == 13 && before[1] == 100, "FenwickTree enumerates logical values");

    ft.Add(2, 0); // no-op: must not invalidate the enumerator below
    var during = 0;
    foreach (long _ in ft) { ft[3] = ft[3]; during++; } // no-op assignment mid-enumeration
    Check(during == 6, "FenwickTree no-op update does not invalidate enumerators");

    ft.Clear();
    Check(ft.Count == 6 && ft.Total == 0 && ft[0] == 0, "FenwickTree clear resets values, keeps length");

    // A second T (and a larger tree) so the generic-math instantiation is exercised twice.
    var wide = new FenwickTree<int>(1000);
    for (int i = 0; i < 1000; i++) wide.Add(i, i);
    Check(wide.Total == 499_500 && wide.PrefixSum(10) == 45, "FenwickTree int instantiation at scale");
}

// SegmentTree — range aggregates over a struct monoid. Two ILC-specific things are pinned here. The fold
// arrives as a generic type argument, so every built-in monoid is a separate instantiation the compiler has
// to specialize ahead of time (and MinMonoid / MaxMonoid reach static abstract IMinMaxValue<T> members for
// their identity, the same generic-math shape as FenwickTree). And T is unconstrained, so a reference-typed
// element type must work with no JIT to fall back on. Exercise the O(n) seeded build, the point update, the
// range query at a non-power-of-two length (where the 2n layout's leaf rotation is live), Combine, clear and
// the struct enumerator.
{
    var st = new SegmentTree<long, MinMonoid<long>>(new long[] { 3, 1, 4, 1, 5, 9, 2 });
    Check(st.Count == 7 && st.Aggregate == 1, "SegmentTree seeded build + aggregate");
    Check(st.Query(0, 3) == 1 && st.Query(4, 7) == 2 && st.Query(2, 2) == long.MaxValue,
        "SegmentTree range queries + empty range");

    st[1] = 8;
    Check(st[1] == 8 && st.Query(0, 3) == 3, "SegmentTree point update refolds the path");

    st.Combine(0, 0);
    Check(st[0] == 0 && st.Aggregate == 0, "SegmentTree monoid-native update");

    var values = new List<long>();
    foreach (long v in st) values.Add(v);
    Check(values.Count == 7 && values[1] == 8, "SegmentTree enumerates logical values");

    st.Clear();
    Check(st.Count == 7 && st.Aggregate == long.MaxValue, "SegmentTree clear resets to identity, keeps length");

    // A second monoid over a second element type, so the fold really is specialized per instantiation.
    var masks = new SegmentTree<int, BitwiseAndMonoid<int>>(new[] { 0b1111, 0b1110, 0b1100 });
    Check(masks.Aggregate == 0b1100 && masks.Query(0, 2) == 0b1110, "SegmentTree bitwise-and instantiation");

    // A reference-typed element type, which has no value-type layout for ILC to specialize around.
    var words = new SegmentTree<string, AotConcatMonoid>(new[] { "a", "b", "c" });
    Check(words.Aggregate == "abc" && words.Query(1, 3) == "bc", "SegmentTree reference-typed elements");
}

// BTreeDictionary / BTreeSet — the ordered collections. Two things are worth pinning under ILC here:
// the struct-comparer generic (DefaultComparer<T> plus a hand-written one, so the constrained
// IComparer<T> calls specialize per comparer), and the [InlineArray] traversal buffers behind the
// enumerators, which are the only inline arrays in the library. Exercise the split path (well past a
// node's 31 keys), the rebalancing remove path, the ordered surface, and both enumerators.
{
    var map = new BTreeDictionary<int, int>();
    for (int i = 999; i >= 0; i--) map.Add(i, i * 2);
    Check(map.Count == 1000 && map[500] == 1000, "BTreeDictionary bulk add across splits");
    Check(map.Min.Key == 0 && map.Max.Key == 999, "BTreeDictionary min/max");
    Check(map.TryGetLowerBound(500, out var lb) && lb.Key == 500, "BTreeDictionary lower bound");
    Check(map.TryGetUpperBound(500, out var ub) && ub.Key == 501, "BTreeDictionary upper bound");

    var scanned = 0;
    foreach (var entry in map.EnumerateRange(100, 150)) scanned++;
    Check(scanned == 50, "BTreeDictionary range scan");

    var ordered = 0;
    var previous = int.MinValue;
    foreach (var entry in map)
    {
        Check(entry.Key > previous, "BTreeDictionary enumerates in ascending key order");
        previous = entry.Key;
        ordered++;
    }

    Check(ordered == 1000, "BTreeDictionary full enumeration");

    for (int i = 0; i < 1000; i += 2) Check(map.Remove(i, out var removed) && removed == i * 2, "BTreeDictionary remove with rebalancing");
    Check(map.Count == 500 && map.Min.Key == 1, "BTreeDictionary state after removals");

    IDictionary<int, int> asDictionary = map;
    Check(asDictionary.Keys.Count == 500 && asDictionary.Values.Count == 500, "BTreeDictionary IDictionary views");

    var set = new BTreeSet<int>(Enumerable.Range(0, 1000).Reverse());
    Check(set.Count == 1000 && set.Min == 0 && set.Max == 999, "BTreeSet source ctor across splits");
    Check(!set.TryAdd(10) && set.Remove(10) && !set.Contains(10), "BTreeSet duplicate + remove");
    var inRange = 0;
    foreach (int item in set.EnumerateRange(200, 260)) inRange++;
    Check(inRange == 60, "BTreeSet range scan");
    ((ISet<int>)set).IntersectWith(new[] { 1, 2, 3, 5000 });
    Check(set.Count == 3 && set.Max == 3, "BTreeSet ISet<int> intersect");

    // A hand-written struct comparer: a second closed generic instantiation for ILC to compile.
    var descending = new BTreeSet<int, DescendingIntComparer>();
    for (int i = 0; i < 100; i++) descending.Add(i);
    Check(descending.Min == 99 && descending.Max == 0, "BTreeSet custom struct comparer order");
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

// Trie — ordered prefix tree over string keys. The only collection that walks a key character by
// character instead of hashing it, and the only one whose surface mixes an allocation-free struct
// enumerator with compiler-generated iterators (GetByPrefix / GetKeysWithPrefix / Keys / Values), so
// the AOT publish must compile both traversal shapes. Exercise the indexer / Add / TryAdd /
// TryGetValue, the empty-string key stored on the root, GetByPrefix (ascending order, and the
// O(prefix + matches) contract — it descends to the prefix node and walks only that subtree, never
// the whole trie), TryGetLongestPrefix, Remove with bottom-up pruning, ordered enumeration, and the
// IReadOnlyDictionary<string, TValue?> surface.
{
    var trie = new Trie<int>(new[]
    {
        new KeyValuePair<string, int>("car", 3),
        new KeyValuePair<string, int>("cart", 4),
        new KeyValuePair<string, int>("cat", 5),
        new KeyValuePair<string, int>("dog", 6),
    });
    trie[""] = 0; // the empty string is a valid key, held on the root node
    Check(trie.Count == 5 && trie["cart"] == 4 && trie[""] == 0, "Trie bulk-load ctor + empty-string key");
    Check(trie.TryAdd("care", 40), "Trie.TryAdd new key");
    Check(!trie.TryAdd("care", 99) && trie["care"] == 40, "Trie.TryAdd duplicate leaves the value");
    trie.Add("dot", 7);
    Check(trie.TryGetValue("cat", out int cat) && cat == 5, "Trie.TryGetValue");
    Check(!trie.TryGetValue("ca", out _), "Trie interior node is not a key");
    Check(trie.ContainsKey("dog") && !trie.ContainsKey("do"), "Trie.ContainsKey");
    Check(trie.ContainsPrefix("ca") && !trie.ContainsPrefix("z"), "Trie.ContainsPrefix");

    // GetByPrefix descends to the prefix node and yields only that subtree, in ascending ordinal order.
    var prefixed = new List<string>();
    foreach (var kvp in trie.GetByPrefix("car")) prefixed.Add(kvp.Key);
    Check(prefixed.Count == 3 && prefixed[0] == "car" && prefixed[1] == "care" && prefixed[2] == "cart",
        "Trie.GetByPrefix ordered subtree walk");
    Check(!trie.GetByPrefix("zz").Any(), "Trie.GetByPrefix on a missing prefix yields nothing");

    var keys = new List<string>(trie.GetKeysWithPrefix("do"));
    Check(keys.Count == 2 && keys[0] == "dog" && keys[1] == "dot", "Trie.GetKeysWithPrefix");

    // Longest stored key that is a prefix of the query — the routing-table shape.
    Check(trie.TryGetLongestPrefix("cartoon", out string? longest, out int longestValue)
        && longest == "cart" && longestValue == 4, "Trie.TryGetLongestPrefix interior match");
    Check(trie.TryGetLongestPrefix("cat", out string? exact, out _) && exact == "cat",
        "Trie.TryGetLongestPrefix exact match");
    Check(trie.TryGetLongestPrefix("zzz", out string? rootKey, out int rootValue)
        && rootKey!.Length == 0 && rootValue == 0, "Trie.TryGetLongestPrefix falls back to the empty key");

    // Remove prunes the nodes that no longer lead to a key, leaving siblings and prefixes intact.
    Check(trie.Remove("cart", out int removed) && removed == 4, "Trie.Remove out value");
    Check(!trie.ContainsKey("cart") && trie["car"] == 3 && trie["care"] == 40,
        "Trie.Remove prunes without disturbing siblings");
    Check(!trie.Remove("cart"), "Trie.Remove absent key");

    // The struct enumerator yields every entry in ascending ordinal key order.
    var ordered = new List<string>();
    foreach (var kvp in trie) ordered.Add(kvp.Key);
    bool ascending = ordered.Count == trie.Count;
    for (int i = 1; i < ordered.Count; i++)
        ascending &= string.CompareOrdinal(ordered[i - 1], ordered[i]) < 0;
    Check(ascending, "Trie struct enumerator yields every entry in ascending ordinal key order");

    // IReadOnlyDictionary<string, TValue?> conformance. The interface indexer is implemented
    // explicitly (its getter is nullable-valued), so it is reachable only through the interface.
    IReadOnlyDictionary<string, int> view = trie;
    Check(view.Count == trie.Count && view["cat"] == 5 && view.ContainsKey("dog"),
        "Trie IReadOnlyDictionary indexer + ContainsKey");
    Check(view.TryGetValue("care", out int viaInterface) && viaInterface == 40,
        "Trie IReadOnlyDictionary TryGetValue");
    Check(view.Keys.Count() == view.Count && view.Values.Sum() == 0 + 3 + 40 + 5 + 6 + 7,
        "Trie IReadOnlyDictionary Keys/Values");
    int boxed = 0;
    foreach (var _ in (IEnumerable<KeyValuePair<string, int>>)trie) boxed++;
    Check(boxed == trie.Count, "Trie boxed IEnumerable enumeration");

    // A reference-type TValue and a larger build, so the AOT publish compiles a second generic
    // instantiation plus the child-array growth and the Clear path.
    var wide = new Trie<string>();
    for (int i = 0; i < 500; i++) wide[$"key{i:D3}"] = $"v{i}";
    Check(wide.Count == 500 && wide["key499"] == "v499", "Trie<string> build at scale");
    Check(wide.GetKeysWithPrefix("key1").Count() == 100, "Trie<string> prefix slice at scale");
    wide.Clear();
    Check(wide.Count == 0 && !wide.ContainsPrefix("key"), "Trie.Clear");
}

// Span-keyed lookups + StringInternTable. These are the paths where the JIT's devirtualization
// of a struct hasher through a *method*-level generic constraint (ISpanHashProvider on the
// SpanLookupExtensions methods, not on the collection's own type parameter) has to survive AOT
// compilation — every instantiation below is one the ILC has to see and generate.
{
    // The key sits inside a larger buffer, exactly as a parser would hold it: nothing here is
    // ever turned into a string before the probe.
    char[] buffer = "..alpha..beta..".ToCharArray();
    ReadOnlySpan<char> alpha = buffer.AsSpan(2, 5);
    ReadOnlySpan<char> beta = buffer.AsSpan(9, 4);
    ReadOnlySpan<char> missing = "gamma".AsSpan();

    var pairs = new[]
    {
        new KeyValuePair<string, int>("alpha", 1),
        new KeyValuePair<string, int>("beta", 2),
    };

    var frozenDict = new FrozenCelerityDictionary<int, StringXxHash3Hasher>(pairs);
    Check(frozenDict.TryGetValue(alpha, out int fdA) && fdA == 1, "FrozenCelerityDictionary span TryGetValue");
    Check(frozenDict.ContainsKey(beta) && !frozenDict.ContainsKey(missing), "FrozenCelerityDictionary span ContainsKey");

    var frozenSet = new FrozenCeleritySet<StringXxHash3Hasher>(new[] { "alpha", "beta" });
    Check(frozenSet.Contains(alpha) && !frozenSet.Contains(missing), "FrozenCeleritySet span Contains");

    var dict = new CelerityDictionary<string, int, StringFnV1AFullHasher>();
    dict.Add("alpha", 1);
    dict.Add("beta", 2);
    Check(dict.TryGetValue(beta, out int dB) && dB == 2, "CelerityDictionary span TryGetValue");
    Check(dict.ContainsKey(alpha) && !dict.ContainsKey(missing), "CelerityDictionary span ContainsKey");

    var set = new CeleritySet<string, StringFnV1AFullHasher>();
    set.Add("alpha");
    Check(set.Contains(alpha) && !set.Contains(missing), "CeleritySet span Contains");

    var spanTrie = new Trie<int>();
    spanTrie["alpha"] = 1;
    Check(spanTrie.TryGetValue(alpha, out int tA) && tA == 1, "Trie span TryGetValue");
    Check(spanTrie.ContainsKey(alpha) && spanTrie.ContainsPrefix("alp".AsSpan()), "Trie span ContainsKey/ContainsPrefix");

    // Every String*Hasher must answer Hash(s) == Hash(s.AsSpan()) after AOT compilation too —
    // the contract the span probes above are built on.
    var spanHasher = new StringXxHash3Hasher();
    Check(spanHasher.Hash("alpha") == spanHasher.Hash(alpha), "ISpanHashProvider string/span parity");

    // StringInternTable: the miss path allocates, every repeat returns the same reference.
    var interned = new StringInternTable();
    string first = interned.GetOrAdd(alpha);
    string second = interned.GetOrAdd("xxalphaxx".AsSpan(2, 5));
    Check(first == "alpha" && ReferenceEquals(first, second), "StringInternTable canonicalizes by contents");
    Check(interned.Count == 1 && interned.Contains(alpha) && !interned.Contains(missing), "StringInternTable Count/Contains");
    Check(interned.TryGet(alpha, out string? got) && ReferenceEquals(got, first), "StringInternTable.TryGet");
    interned.GetOrAdd(beta);
    int internedSeen = 0;
    foreach (string _ in interned) internedSeen++;
    Check(internedSeen == 2, "StringInternTable struct enumerator");
    interned.Clear();
    Check(interned.Count == 0, "StringInternTable.Clear");

    // A second hasher instantiation, so the ILC generates more than one closed generic.
    var internedStrong = new StringInternTable<StringMurmur3Hasher>();
    Check(ReferenceEquals(internedStrong.GetOrAdd(alpha), internedStrong.GetOrAdd(alpha)),
        "StringInternTable<StringMurmur3Hasher> canonicalizes");
}

// EnumMap — dense array-backed dictionary for enum keys (the .NET EnumMap). Exercise
// the indexer, TryAdd/Add, TryGetValue, Remove, the parallel occupancy vector
// (default value distinct from absent), and the ascending-order struct enumerator.
// DayOfWeek (0..6, contiguous) is a supported small non-negative enum; the switch on
// Unsafe.SizeOf<TEnum>() and the Unsafe.As reinterpret cast must compile to native
// code under AOT.
{
    var m = new EnumMap<DayOfWeek, string>();
    m[DayOfWeek.Monday] = "mon";
    Check(m.TryAdd(DayOfWeek.Tuesday, "tue"), "EnumMap.TryAdd new key");
    Check(!m.TryAdd(DayOfWeek.Tuesday, "x"), "EnumMap.TryAdd duplicate");
    m.Add(DayOfWeek.Sunday, "sun"); // Sunday == 0 is an ordinary key, not a sentinel
    Check(m.TryGetValue(DayOfWeek.Monday, out var v) && v == "mon", "EnumMap indexer round-trip");
    Check(m[DayOfWeek.Sunday] == "sun", "EnumMap zero-valued key round-trip");
    Check(m.Remove(DayOfWeek.Tuesday), "EnumMap.Remove");
    Check(m.Count == 2, "EnumMap count");

    var keys = new List<DayOfWeek>();
    foreach (var kvp in m) keys.Add(kvp.Key);
    Check(keys.Count == 2 && keys[0] == DayOfWeek.Sunday && keys[1] == DayOfWeek.Monday,
        "EnumMap ascending-order enumeration");

    bool enumMapRejectsOutOfRange = false;
    try { _ = new EnumMap<DateTimeKind, int>() { [(DateTimeKind)999] = 1 }; }
    catch (ArgumentOutOfRangeException) { enumMapRejectsOutOfRange = true; }
    Check(enumMapRejectsOutOfRange, "EnumMap rejects out-of-range key");
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

// CuckooFilter — probabilistic membership filter that, unlike BloomFilter, supports
// deletion. Exercise Add / TryAdd / Contains / Remove / Clear / Count / UnionWith and
// the IEnumerable ctor across int / Guid / string instantiations so the AOT publish
// compiles the partial-key cuckoo probe + eviction path and the fingerprint masking.
{
    var filter = new CuckooFilter<int, Int32Murmur3Hasher>(1000);
    filter.Add(42);
    filter.Add(0); // zero is an ordinary element, not a sentinel
    Check(filter.Contains(42) && filter.Contains(0), "CuckooFilter add/contains");
    Check(!filter.Contains(7), "CuckooFilter negative lookup");
    Check(filter.Count == 2, "CuckooFilter count");
    Check(filter.BucketCount >= 1 && (filter.BucketCount & (filter.BucketCount - 1)) == 0,
        "CuckooFilter power-of-two bucket count");
    Check(filter.FingerprintBits is >= 1 and <= 16, "CuckooFilter fingerprint width");

    // Remove — the differentiator from BloomFilter — deletes without false negatives.
    Check(filter.Remove(42) && !filter.Contains(42), "CuckooFilter remove");
    Check(filter.Count == 1, "CuckooFilter count after remove");

    // No false negatives across a larger fill.
    var big = new CuckooFilter<int, Int32Murmur3Hasher>(2000);
    for (int i = 1; i <= 500; i++) big.Add(i * 3);
    bool noFalseNegatives = true;
    for (int i = 1; i <= 500; i++) noFalseNegatives &= big.Contains(i * 3);
    Check(noFalseNegatives, "CuckooFilter no false negatives");
    Check(big.LoadFactor > 0d, "CuckooFilter load factor");

    // UnionWith merges two equally-sized filters.
    var other = new CuckooFilter<int, Int32Murmur3Hasher>(1000);
    other.Add(99999);
    filter.UnionWith(other);
    Check(filter.Contains(99999), "CuckooFilter UnionWith");

    filter.Clear();
    Check(filter.Count == 0 && !filter.Contains(0), "CuckooFilter clear");

    // String elements via the IEnumerable ctor, plus the out-of-band null reference
    // (StringFnV1AHasher throws on null; CuckooFilter must not call it with null).
    var strFilter = new CuckooFilter<string, StringFnV1AHasher>(new[] { "alice", "bob" });
    strFilter.Add(null!);
    Check(strFilter.Contains("alice") && strFilter.Contains("bob") && strFilter.Contains(null!),
        "CuckooFilter<string> ctor + null element");
    Check(strFilter.Remove(null!) && !strFilter.Contains(null!),
        "CuckooFilter<string> remove null element");

    var guidFilter = new CuckooFilter<Guid, GuidHasher>(100);
    guidFilter.Add(Guid.Empty); // ordinary element, no out-of-band slot
    Check(guidFilter.Contains(Guid.Empty), "CuckooFilter<Guid> empty-guid element");
}

// XorFilter — build-once, immutable probabilistic membership filter (no out-of-band
// slot; default(T) is an ordinary element, a null reference is mapped to a fixed base
// hash so the hasher is never called with null). Exercise the IEnumerable ctor across
// int / Guid / string instantiations so the AOT publish compiles the peeling
// construction and the three-probe query path, plus the empty-filter short-circuit.
{
    var filter = new XorFilter<int, Int32WangNaiveHasher>(new[] { 42, 0, 7, 100, -3 });
    Check(filter.Contains(42) && filter.Contains(0) && filter.Contains(-3), "XorFilter build/contains");
    Check(filter.Count == 5, "XorFilter count");
    Check(filter.SlotCount % 3 == 0 && filter.SlotCount >= filter.Count, "XorFilter slot count");
    Check(filter.FingerprintBits == 8, "XorFilter fingerprint width");

    // No false negatives across a larger fill (exercises the peel + reseed path).
    var big = new XorFilter<int, Int32WangNaiveHasher>(Enumerable.Range(1, 2000).Select(i => i * 3).ToArray());
    bool noFalseNegatives = true;
    for (int i = 1; i <= 2000; i++) noFalseNegatives &= big.Contains(i * 3);
    Check(noFalseNegatives, "XorFilter no false negatives");
    Check(big.BitsPerElement > 8d && big.BitsPerElement < 12d, "XorFilter bits/element");

    // Empty filter reports everything absent via the _count == 0 short-circuit.
    var empty = new XorFilter<int, Int32WangNaiveHasher>(Array.Empty<int>());
    Check(empty.Count == 0 && !empty.Contains(1), "XorFilter empty reports absent");

    // String elements via the IEnumerable ctor, plus the out-of-band null reference
    // (StringFnV1AHasher throws on null; XorFilter must not call it with null).
    var strFilter = new XorFilter<string, StringFnV1AHasher>(new[] { "alice", "bob", null! });
    Check(strFilter.Contains("alice") && strFilter.Contains("bob") && strFilter.Contains(null!),
        "XorFilter<string> ctor + null element");

    var guidXor = new XorFilter<Guid, GuidHasher>(new[] { Guid.Empty, Guid.NewGuid() });
    Check(guidXor.Contains(Guid.Empty), "XorFilter<Guid> empty-guid element");
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

// RankSelectBitVector — immutable succinct rank/select index over a dense bit vector.
// Exercise all three constructors, the O(1) Rank / Rank0, the O(log n) Select and its
// Try counterpart, and ToBitSet, over a vector long enough to span several 256-bit
// superblocks so the AOT publish compiles the two-level index build, the superblock
// binary search, and the within-word popcount narrowing.
{
    var sparse = new RankSelectBitVector(1000, new[] { 0, 63, 64, 255, 256, 700, 999 });
    Check(sparse.Length == 1000 && sparse.Count == 7, "RankSelectBitVector positions ctor");
    Check(sparse.Rank(0) == 0 && sparse.Rank(64) == 2 && sparse.Rank(1000) == 7, "RankSelectBitVector rank");
    Check(sparse.Rank0(64) == 62 && sparse.Rank0(1000) == 993, "RankSelectBitVector rank0");
    Check(sparse.Select(0) == 0 && sparse.Select(3) == 255 && sparse.Select(6) == 999, "RankSelectBitVector select");
    Check(sparse.TrySelect(1, out int secondSetBit) && secondSetBit == 63, "RankSelectBitVector try-select");
    Check(!sparse.TrySelect(7, out int noSuchBit) && noSuchBit == -1, "RankSelectBitVector try-select past the end");
    Check(sparse[256] && !sparse[257], "RankSelectBitVector get");
    Check(sparse.IndexSizeInBytes > 0, "RankSelectBitVector index size");

    var dense = new BitSet(600);
    for (int i = 0; i < 600; i += 3) dense[i] = true;
    var indexed = new RankSelectBitVector(dense);
    bool rankOk = true;
    for (int i = 0; i <= 600; i++) rankOk &= indexed.Rank(i) == ((i + 2) / 3);
    Check(rankOk, "RankSelectBitVector BitSet snapshot rank");
    bool selectOk = true;
    for (int k = 0; k < indexed.Count; k++) selectOk &= indexed.Select(k) == k * 3;
    Check(selectOk, "RankSelectBitVector BitSet snapshot select");

    var packed = new RankSelectBitVector(128, new ulong[] { ulong.MaxValue, 0b101UL });
    Check(packed.Count == 66 && packed.Select(65) == 66, "RankSelectBitVector packed-words ctor");

    BitSet roundTrip = indexed.ToBitSet();
    Check(roundTrip.Length == 600 && roundTrip.Count == indexed.Count, "RankSelectBitVector ToBitSet");
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

// FastUtils.MinTableSizeFor + EnsureCapacity / TrimExcess (#231) — the capacity-management surface.
// Compiles the new sizing primitive and the shared Resize(int) re-size paths under Native AOT.
{
    Check(FastUtils.MinTableSizeFor(0, 0.75f) == 1, "MinTableSizeFor(0) == 1");
    int sized = FastUtils.MinTableSizeFor(1000, 0.75f);
    Check((sized & (sized - 1)) == 0 && (int)(sized * 0.75f) >= 1000, "MinTableSizeFor(1000) admits 1000");

    var dict = new IntDictionary<int>();
    int reported = dict.EnsureCapacity(500);
    Check(reported >= 500, "IntDictionary.EnsureCapacity grows");
    for (int i = 1; i <= 500; i++) dict[i] = i * 2;
    for (int i = 11; i <= 500; i++) dict.Remove(i);
    dict.TrimExcess();
    bool dictOk = dict.Count == 10;
    for (int i = 1; i <= 10; i++) if (!dict.TryGetValue(i, out int dv) || dv != i * 2) dictOk = false;
    Check(dictOk, "IntDictionary EnsureCapacity/TrimExcess round-trip");

    var set = new CeleritySet<string, StringFnV1AHasher>();
    set.EnsureCapacity(200);
    for (int i = 0; i < 200; i++) set.Add($"s{i}");
    set.TrimExcess();
    Check(set.Count == 200 && set.Contains("s199"), "CeleritySet EnsureCapacity/TrimExcess round-trip");
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

// VarInt codec (#193) — span-based LEB128 (uint/ulong) + zig-zag (int/long). Forces the
// BitOperations length path, the bounds-safe write/read loops, and the zig-zag transforms to
// compile under Native AOT and confirms they round-trip on the native runtime, including the
// 10-byte ulong.MaxValue case and the short-buffer / truncated failure paths.
{
    Span<byte> buf = stackalloc byte[VarInt.MaxVarIntLength64];

    // Unsigned round-trip across length classes.
    ulong[] unsigned = { 0, 1, 127, 128, 300, uint.MaxValue, 1UL << 56, ulong.MaxValue };
    bool uOk = true;
    foreach (ulong value in unsigned)
    {
        if (!VarInt.TryWriteVarInt(buf, value, out int written)) { uOk = false; break; }
        if (written != VarInt.VarIntLength(value)) { uOk = false; break; }
        if (!VarInt.TryReadVarInt(buf, out ulong read, out int consumed) || read != value || consumed != written)
        { uOk = false; break; }
    }
    Check(uOk, "VarInt unsigned LEB128 round-trip");

    // Signed (zig-zag) round-trip including the extremes.
    long[] signed = { 0, -1, 1, -2, 2, long.MaxValue, long.MinValue };
    bool sOk = true;
    foreach (long value in signed)
    {
        if (!VarInt.TryWriteVarInt(buf, value, out int written)) { sOk = false; break; }
        if (!VarInt.TryReadVarInt(buf, out long read, out int consumed) || read != value || consumed != written)
        { sOk = false; break; }
    }
    Check(sOk, "VarInt signed zig-zag round-trip");

    // Bounds safety: a too-small destination and a truncated source both fail without throwing.
    Span<byte> tiny = stackalloc byte[1];
    Check(!VarInt.TryWriteVarInt(tiny, 300u, out int w) && w == 0, "VarInt short-buffer write fails");
    ReadOnlySpan<byte> truncated = stackalloc byte[] { 0x80 };
    Check(!VarInt.TryReadVarInt(truncated, out uint _, out int r) && r == 0, "VarInt truncated read fails");
}

// BitWriter / BitReader — sequential sub-byte bit I/O. Forces the ref-struct cursors, the LSB-first
// clear-then-set write loop, the multi-byte-straddling field path, and the bounds-safe failure paths to
// compile under Native AOT and confirms a mixed-width field record round-trips on the native runtime.
{
    // A record of odd-width fields totalling 64 bits → exactly 8 bytes.
    (ulong Value, int Bits)[] fields = { (5, 3), (3000, 12), (1, 1), (0xABCDE, 20), (0x0FFFFFFF, 28) };
    Span<byte> buf = stackalloc byte[BitWriter.ByteCount(64)];

    var writer = new BitWriter(buf);
    bool wOk = true;
    foreach (var (value, bits) in fields)
        if (!writer.TryWriteBits(value, bits)) { wOk = false; break; }
    wOk &= writer.BitsWritten == 64 && writer.BytesWritten == 8;
    Check(wOk, "BitWriter mixed-width field pack");

    var reader = new BitReader(buf);
    bool rOk = true;
    foreach (var (value, bits) in fields)
        if (!reader.TryReadBits(bits, out ulong read) || read != value) { rOk = false; break; }
    Check(rOk, "BitReader mixed-width field round-trip");

    // Bounds safety: a field that overflows the buffer fails without advancing; a full read is exhausted.
    Span<byte> one = stackalloc byte[1];
    var w2 = new BitWriter(one);
    Check(w2.TryWriteBits(0b111, 3) && !w2.TryWriteBits(0b111111, 6) && w2.BitsWritten == 3,
        "BitWriter refuses an overfull field without mutating");
}

// FastGuid (#195) — non-crypto v4 + RFC 9562 big-endian v7 from a struct PRNG, and the strictly monotonic
// GuidV7Generator. Forces the ref-generic CreateVersion4 / CreateVersion7 instantiations and the mutable
// monotonic-counter struct to compile under Native AOT, and confirms version / variant bits, the big-endian
// timestamp placement, and same-millisecond monotonicity hold on the native runtime.
{
    // ToString("N") renders the GUID fields most-significant-first, so its 32 hex digits are the big-endian
    // byte sequence — the lens used to read the version / variant nibbles and the timestamp prefix.
    static int VersionNibble(Guid g) => Convert.ToInt32(g.ToString("N").Substring(12, 1), 16);
    static int VariantNibble(Guid g) => Convert.ToInt32(g.ToString("N").Substring(16, 1), 16);

    var rng = new Xoshiro256StarStar(0xABCDEF);

    Guid v4 = FastGuid.CreateVersion4(ref rng);
    Check(VersionNibble(v4) == 4 && (VariantNibble(v4) & 0xC) == 0x8, "FastGuid v4 version/variant bits");

    const long ms = 0x010203040506L;
    Guid v7 = FastGuid.CreateVersion7(ref rng, ms);
    Check(VersionNibble(v7) == 7 && (VariantNibble(v7) & 0xC) == 0x8, "FastGuid v7 version/variant bits");
    Check(v7.ToString("N").StartsWith("010203040506"), "FastGuid v7 big-endian timestamp prefix");

    // Distinctness across a v4 burst.
    var v4Seen = new HashSet<Guid>();
    bool v4Distinct = true;
    for (int i = 0; i < 1000; i++) v4Distinct &= v4Seen.Add(FastGuid.CreateVersion4(ref rng));
    Check(v4Distinct, "FastGuid v4 distinct over a burst");

    // Strict monotonicity of the generator within a single millisecond and across counter overflow.
    var gen = new GuidV7Generator<WyRand>(new WyRand(0xABCDEF));
    Guid prev = gen.Next(ms);
    bool monotonic = true;
    for (int i = 0; i < 20_000; i++)
    {
        Guid cur = gen.Next(ms);
        if (string.CompareOrdinal(prev.ToString("N"), cur.ToString("N")) >= 0) { monotonic = false; break; }
        prev = cur;
    }
    Check(monotonic, "GuidV7Generator strictly monotonic within one millisecond (incl. counter overflow)");
}

// FastUtils.CountDigits / Log10 (#194) — base-10 digit count via Log2 (LZCNT) + magic table (32-bit)
// and a comparison ladder (64-bit), plus magnitude-only signed overloads and the integer Log10. Forces
// the BitOperations.Log2 / table-lookup paths to compile under Native AOT and confirms they reproduce
// value.ToString().Length on the native runtime, including the int/long MinValue magnitude edge cases.
{
    bool ok32 = true;
    for (uint value = 0; value < 50_000; value++)
        if (FastUtils.CountDigits(value) != value.ToString().Length) { ok32 = false; break; }
    Check(ok32, "FastUtils.CountDigits(uint) matches ToString().Length");
    Check(FastUtils.CountDigits(uint.MaxValue) == 10, "FastUtils.CountDigits(uint.MaxValue) == 10");

    ulong[] samples64 = { 0, 9, 10, 9_999_999, 10_000_000, 99_999_999_999_999, 100_000_000_000_000,
        9_999_999_999_999_999_999UL, ulong.MaxValue };
    bool ok64 = true;
    foreach (ulong value in samples64)
        if (FastUtils.CountDigits(value) != value.ToString().Length) { ok64 = false; break; }
    Check(ok64, "FastUtils.CountDigits(ulong) matches ToString().Length");

    // Signed overloads count the magnitude only (sign excluded), and MinValue must not overflow.
    Check(FastUtils.CountDigits(-5) == 1 && FastUtils.CountDigits(int.MinValue) == 10,
        "FastUtils.CountDigits(int) magnitude-only + MinValue");
    Check(FastUtils.CountDigits(-7L) == 1 && FastUtils.CountDigits(long.MinValue) == 19,
        "FastUtils.CountDigits(long) magnitude-only + MinValue");

    // Integer Log10 == digit count - 1, exact at powers of ten.
    Check(FastUtils.Log10(0u) == 0 && FastUtils.Log10(999u) == 2 && FastUtils.Log10(1000u) == 3
        && FastUtils.Log10(ulong.MaxValue) == 19, "FastUtils.Log10 exact at powers of ten");
}

// FastUtils alignment helpers + SpanBits (#196) — AlignUp/AlignDown/IsAligned over int/long/nuint and the
// non-owning span bit-packing helpers (Get/Set/Clear/Flip/PopCount/NextSetBit). Forces the
// BitOperations.IsPow2 validation and the POPCNT/TZCNT bit paths to compile under Native AOT.
{
    bool alignOk = FastUtils.AlignUp(7, 8) == 8 && FastUtils.AlignDown(7, 8) == 0 && FastUtils.IsAligned(16, 8)
        && FastUtils.AlignUp(5_000_000_001L, 4096L) == 5_000_003_584L
        && FastUtils.AlignUp((nuint)13, (nuint)16) == (nuint)16 && FastUtils.IsAligned((nuint)64, (nuint)64);
    Check(alignOk, "FastUtils alignment helpers (int/long/nuint)");

    bool alignThrows = false;
    try { FastUtils.AlignUp(0, 6); } catch (ArgumentOutOfRangeException) { alignThrows = true; }
    Check(alignThrows, "FastUtils.AlignUp rejects non-power-of-two alignment");

    Span<ulong> bits = stackalloc ulong[SpanBits.WordCount(192)];
    SpanBits.Set(bits, 5);
    SpanBits.Set(bits, 64);
    SpanBits.Set(bits, 130);
    bool spanOk = SpanBits.Get(bits, 5) && SpanBits.Get(bits, 64) && !SpanBits.Get(bits, 6)
        && SpanBits.PopCount(bits) == 3
        && SpanBits.NextSetBit(bits, 0) == 5 && SpanBits.NextSetBit(bits, 6) == 64
        && SpanBits.NextSetBit(bits, 131) == -1;
    SpanBits.Clear(bits, 64);
    spanOk &= !SpanBits.Get(bits, 64) && SpanBits.Flip(bits, 64) && SpanBits.PopCount(bits) == 3;
    Check(spanOk, "SpanBits get/set/clear/flip/popcount/scan");
}

// SimdReductions (#197) — fused single-pass MinMax (int/long/uint/ulong) and the overflow-checked, int-widening
// CheckedSum. Forces the Vector<T> Min/Max fold, the Vector.Widen accumulation, and the checked narrowing to
// compile under Native AOT.
{
    var data = new int[40];
    for (int i = 0; i < data.Length; i++) data[i] = i - 20; // -20..19
    data[0] = int.MinValue;
    data[^1] = int.MaxValue;
    var (min, max) = SimdReductions.MinMax(data);
    Check(min == int.MinValue && max == int.MaxValue, "SimdReductions.MinMax(int) extrema");

    var (lmin, lmax) = SimdReductions.MinMax(new[] { 5L, long.MinValue, 100L, long.MaxValue });
    Check(lmin == long.MinValue && lmax == long.MaxValue, "SimdReductions.MinMax(long) extrema");

    var (umin, umax) = SimdReductions.MinMax(new[] { 5u, 0u, uint.MaxValue, 7u });
    Check(umin == 0u && umax == uint.MaxValue, "SimdReductions.MinMax(uint) extrema");

    var sumData = new int[33];
    for (int i = 0; i < sumData.Length; i++) sumData[i] = i + 1; // 1..33 => 561
    Check(SimdReductions.CheckedSum(sumData) == 561, "SimdReductions.CheckedSum matches arithmetic");

    bool sumThrows = false;
    try { _ = SimdReductions.CheckedSum(new[] { int.MaxValue, int.MaxValue }); }
    catch (OverflowException) { sumThrows = true; }
    Check(sumThrows, "SimdReductions.CheckedSum throws on overflow");
}

// Branchless (#198) — guaranteed branch-free conditional select. Forces the mask-trick scalar overloads
// (int/long/float/double) and the bulk per-element span blend to compile under Native AOT.
{
    Check(Branchless.Select(true, 7, -3) == 7 && Branchless.Select(false, 7, -3) == -3, "Branchless.Select(int) both polarities");
    Check(Branchless.Select(true, long.MinValue, long.MaxValue) == long.MinValue, "Branchless.Select(long)");
    Check(Branchless.Select(false, 3.5f, -2.25f) == -2.25f, "Branchless.Select(float)");
    Check(Branchless.Select(true, double.NegativeInfinity, 1d) == double.NegativeInfinity, "Branchless.Select(double)");

    var cond = new[] { true, false, true, false };
    var t = new[] { 1, 2, 3, 4 };
    var f = new[] { 10, 20, 30, 40 };
    var dst = new int[4];
    Branchless.Select(cond, t, f, dst);
    Check(dst[0] == 1 && dst[1] == 20 && dst[2] == 3 && dst[3] == 40, "Branchless.Select span blend");

    bool blendThrows = false;
    try { Branchless.Select(new[] { true, false }, new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, new int[3]); }
    catch (ArgumentException) { blendThrows = true; }
    Check(blendThrows, "Branchless.Select throws on length mismatch");
}

// SortedSpan (#313) — set algebra over already-sorted spans. Forces the generic merge, the galloping
// path taken when one side is 32x the other, and the destination-too-short throw to compile under
// Native AOT; the generic is constrained to IComparisonOperators<T, T, bool>, so this also pins that
// ILC can specialize a generic-math constraint per value type without a JIT to fall back on.
{
    ReadOnlySpan<int> a = [1, 3, 3, 5, 7, 9];
    ReadOnlySpan<int> b = [3, 5, 6, 9, 11];
    Span<int> destination = stackalloc int[16];

    int written = SortedSpan.Intersect(a, b, destination);
    Check(written == 3 && destination[0] == 3 && destination[1] == 5 && destination[2] == 9,
        "SortedSpan.Intersect collapses duplicates");

    written = SortedSpan.Union(a, b, destination);
    Check(written == 7 && destination[0] == 1 && destination[6] == 11, "SortedSpan.Union");

    written = SortedSpan.Except(a, b, destination);
    Check(written == 2 && destination[0] == 1 && destination[1] == 7, "SortedSpan.Except");

    Check(SortedSpan.IntersectCount(a, b) == 3 && SortedSpan.Overlaps(a, b), "SortedSpan.IntersectCount / Overlaps");

    // A long side forces the galloping path rather than the linear merge.
    var longRun = new int[1000];
    for (int i = 0; i < longRun.Length; i++) longRun[i] = i * 2;
    ReadOnlySpan<int> probes = [3, 500, 4000];
    written = SortedSpan.Intersect(probes, longRun, destination);
    Check(written == 1 && destination[0] == 500, "SortedSpan.Intersect galloping path");
    Check(SortedSpan.Except(probes, (ReadOnlySpan<int>)longRun, destination) == 2, "SortedSpan.Except galloping path");

    bool destinationThrows = false;
    try { _ = SortedSpan.Intersect(a, b, Span<int>.Empty); } catch (ArgumentException) { destinationThrows = true; }
    Check(destinationThrows, "SortedSpan throws on a destination that is too short");
}

// IHashProvider64 (#304) — the 64-bit hasher surface and the sketch dispatch that selects it.
// The dispatch is a JIT/ILC-folded type test plus a once-boxed interface reference held in a
// static generic field, so this forces ILC to compile both the folded constant and the boxed
// interface call for a value-type hasher — the shapes most likely to differ from the JIT.
{
    Check(new Int64WangHasher().Hash64(42L) != 0UL, "Int64WangHasher.Hash64");
    Check((int)new Int64Murmur3Hasher().Hash64(42L) == new Int64Murmur3Hasher().Hash(42L),
        "Int64Murmur3Hasher.Hash64 low half == Hash");
    Check((int)new UInt64Murmur3Hasher().Hash64(42UL) == new UInt64Murmur3Hasher().Hash(42UL),
        "UInt64Murmur3Hasher.Hash64 low half == Hash");
#pragma warning disable CS0618 // The obsolete alias still ships, so ILC must still compile it.
    Check(new UInt64Hasher().Hash64(42UL) == new UInt64Murmur3Hasher().Hash64(42UL),
        "UInt64Hasher alias agrees with UInt64Murmur3Hasher");
    Check(new UInt32Hasher().Hash(3000000000u) == new UInt32WangNaiveHasher().Hash(3000000000u),
        "UInt32Hasher alias agrees with UInt32WangNaiveHasher");
#pragma warning restore CS0618
    Check((int)new UInt64WangHasher().Hash64(42UL) == new UInt64WangHasher().Hash(42UL),
        "UInt64WangHasher.Hash64 low half == Hash");
    Guid probeGuid = new Guid("12345678-1234-1234-1234-1234567890AB");
    Check((int)new GuidHasher().Hash64(probeGuid) == new GuidHasher().Hash(probeGuid),
        "GuidHasher.Hash64 low half == Hash");

    ulong s64 = new StringXxHash64Hasher().Hash64("celerity");
    Check(unchecked((int)(s64 ^ (s64 >> 32))) == new StringXxHash64Hasher().Hash("celerity"),
        "StringXxHash64Hasher.Hash64 xor-fold == Hash");

    // The sketches must take the 64-bit path and stay accurate on it.
    var hll64 = new HyperLogLog<long, Int64WangHasher>();
    for (long i = 0; i < 20_000; i++) hll64.Add(i);
    long est = hll64.EstimateCardinality();
    Check(est > 18_000 && est < 22_000, "HyperLogLog with a 64-bit hasher");

    var bloom64 = new BloomFilter<long, Int64Murmur3Hasher>(2_000, 0.01);
    for (long i = 0; i < 2_000; i++) bloom64.Add(i);
    bool bloomOk = true;
    for (long i = 0; i < 2_000; i++) bloomOk &= bloom64.Contains(i);
    Check(bloomOk, "BloomFilter with a 64-bit hasher: no false negatives");

    var cms64 = new CountMinSketch<string, StringXxHash64Hasher>(0.001, 0.01);
    cms64.Add("celerity", 5);
    cms64.Add(null!); // null must still bypass the hasher on the 64-bit path
    Check(cms64.EstimateCount("celerity") >= 5 && cms64.EstimateCount(null!) >= 1,
        "CountMinSketch with a 64-bit string hasher + null element");

    var xor64 = new XorFilter<long, Int64WangHasher>(new long[] { 1, 2, 3 });
    Check(xor64.Contains(1) && xor64.Contains(2) && xor64.Contains(3),
        "XorFilter with a 64-bit hasher");

    // HashQualityEvaluator's 64-bit surface.
    var report64 = HashQualityEvaluator.Evaluate64<long, Int64WangHasher>(new long[] { 1, 2, 3, 4 }, 8);
    Check(report64.KeyCount == 4 && report64.DistinctHashCount == 4, "HashQualityEvaluator.Evaluate64");
}

// IDictionary<,> — the mutable BCL interface on the dictionary family. Worth pinning under ILC
// specifically: Keys / Values are readonly structs reached through ICollection<T>, so the interface
// path boxes them and dispatches through an unboxing stub the AOT compiler has to generate. A plain
// foreach over the concrete type never exercises that.
{
    void DriveInterface(IDictionary<int, string?> map, string label)
    {
        map.Add(0, "zero");                                        // out-of-band default-key slot
        map.Add(new KeyValuePair<int, string?>(1, "one"));
        map[2] = "two";

        Check(map.Count == 3 && !map.IsReadOnly, $"{label} as IDictionary: count / IsReadOnly");
        Check(map.Contains(new KeyValuePair<int, string?>(1, "one")), $"{label} as IDictionary: Contains(pair)");
        Check(!map.Contains(new KeyValuePair<int, string?>(1, "uno")), $"{label} as IDictionary: pair mismatch");

        ICollection<int> keys = map.Keys;
        ICollection<string?> values = map.Values;
        Check(keys.Count == 3 && keys.Contains(0), $"{label} as IDictionary: boxed key view");
        Check(values.Count == 3 && values.Contains("two"), $"{label} as IDictionary: boxed value view");

        var keyBuffer = new int[3];
        keys.CopyTo(keyBuffer, 0);
        Check(keyBuffer[0] + keyBuffer[1] + keyBuffer[2] == 3, $"{label} as IDictionary: key view CopyTo");

        try
        {
            keys.Add(9);
            Check(false, $"{label} as IDictionary: key view is read-only");
        }
        catch (NotSupportedException)
        {
            Check(true, $"{label} as IDictionary: key view is read-only");
        }

        var pairs = new KeyValuePair<int, string?>[3];
        map.CopyTo(pairs, 0);
        Check(pairs.Length == 3, $"{label} as IDictionary: CopyTo");

        Check(!map.Remove(new KeyValuePair<int, string?>(2, "stale")), $"{label} as IDictionary: stale pair kept");
        Check(map.Remove(new KeyValuePair<int, string?>(2, "two")), $"{label} as IDictionary: Remove(pair)");
        Check(map.Remove(0) && !map.ContainsKey(0), $"{label} as IDictionary: Remove(key)");

        map.Clear();
        Check(map.Count == 0, $"{label} as IDictionary: Clear");
    }

    DriveInterface(new CelerityDictionary<int, string, Int32WangNaiveHasher>(), "CelerityDictionary");
    DriveInterface(new SwissDictionary<int, string, Int32WangNaiveHasher>(), "SwissDictionary");
    DriveInterface(new RobinHoodDictionary<int, string, Int32WangNaiveHasher>(), "RobinHoodDictionary");
    DriveInterface(new HashCachingDictionary<int, string, Int32WangNaiveHasher>(), "HashCachingDictionary");
    DriveInterface(new SmallDictionary<int, string>(), "SmallDictionary");
    DriveInterface(new IntDictionary<string>(), "IntDictionary");

    using (var pooled = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>())
        DriveInterface(pooled, "PooledCelerityDictionary");

    // The two key shapes the helper cannot take: a long key and an enum key.
    IDictionary<long, string?> longMap = new LongDictionary<string>();
    longMap.Add(1L << 40, "high");
    Check(longMap.Keys.Contains(1L << 40) && longMap.Values.Contains("high"),
        "LongDictionary as IDictionary: boxed views");

    IDictionary<DayOfWeek, string?> enumMap = new EnumMap<DayOfWeek, string>();
    enumMap.Add(DayOfWeek.Monday, "mon");
    Check(enumMap.Keys.Contains(DayOfWeek.Monday) && !enumMap.ContainsKey(DayOfWeek.Sunday),
        "EnumMap as IDictionary: boxed key view");
}

// Celerity.Sorting — RadixSort / CountingSort / PartialSort (#309).
//
// Every shape that forces a distinct native instantiation: both radix widths, both signed and
// IEEE-754 key transforms, the key+payload generic over a reference payload (which is where the
// pooled scratch has to be cleared on return), the argsort, the bounded-range counting sort, and
// both PartialSort comparer forms — the IComparable<T> default and a caller-supplied struct
// comparer, which is a second closed generic over the same selection code.
{
    int[] signed = [3, -1, int.MinValue, 0, int.MaxValue, -7, 2];
    RadixSort.Sort(signed.AsSpan());
    Check(signed[0] == int.MinValue && signed[^1] == int.MaxValue, "RadixSort int order");

    uint[] unsigned = [7, 0, uint.MaxValue, 3];
    RadixSort.SortWithScratch(unsigned.AsSpan(), new uint[4].AsSpan());
    Check(unsigned[0] == 0 && unsigned[^1] == uint.MaxValue, "RadixSort uint order with scratch");

    long[] wide = [1L << 40, -1L << 40, 0L];
    RadixSort.Sort(wide.AsSpan());
    Check(wide[0] == -(1L << 40) && wide[^1] == 1L << 40, "RadixSort long order");

    ulong[] wideUnsigned = [ulong.MaxValue, 0UL, 1UL << 40];
    RadixSort.SortWithScratch(wideUnsigned.AsSpan(), new ulong[3].AsSpan());
    Check(wideUnsigned[0] == 0UL && wideUnsigned[^1] == ulong.MaxValue, "RadixSort ulong order with scratch");

    float[] singles = [1.5f, -2.5f, 0f, float.PositiveInfinity, float.NegativeInfinity];
    RadixSort.Sort(singles.AsSpan());
    Check(float.IsNegativeInfinity(singles[0]) && float.IsPositiveInfinity(singles[^1]), "RadixSort float order");

    double[] doubles = [1.5, -2.5, 0d, double.PositiveInfinity, double.NegativeInfinity];
    RadixSort.Sort(doubles.AsSpan());
    Check(double.IsNegativeInfinity(doubles[0]) && double.IsPositiveInfinity(doubles[^1]), "RadixSort double order");

    // Reference-typed payload: the pooled value scratch is cleared on return for this instantiation.
    int[] payloadKeys = [3, 1, 2];
    string[] payload = ["three", "one", "two"];
    RadixSort.Sort<string>(payloadKeys.AsSpan(), payload.AsSpan());
    Check(payload[0] == "one" && payload[^1] == "three", "RadixSort key+payload");

    int[] pairKeys = [5, 4, 6];
    int[] pairValues = [50, 40, 60];
    RadixSort.SortWithScratch(pairKeys.AsSpan(), pairValues.AsSpan(), new int[3].AsSpan(), new int[3].AsSpan());
    Check(pairValues[0] == 40 && pairValues[^1] == 60, "RadixSort key+payload with scratch");

    int[] rankKeys = [30, 10, 20];
    int[] ranks = new int[3];
    RadixSort.ArgSort(rankKeys, ranks.AsSpan());
    Check(ranks[0] == 1 && ranks[^1] == 0 && rankKeys[0] == 30, "RadixSort.ArgSort ranks without reordering");

    byte[] bytes = [5, 255, 0, 5, 1];
    CountingSort.Sort(bytes.AsSpan());
    Check(bytes[0] == 0 && bytes[^1] == 255, "CountingSort byte order");

    ushort[] shorts = [40_000, 0, 65_535, 7];
    CountingSort.Sort(shorts.AsSpan());
    Check(shorts[0] == 0 && shorts[^1] == 65_535, "CountingSort ushort order");

    int[] ranged = [3, -2, 0, 3, -5];
    string[] rangedPayload = ["a", "b", "c", "d", "e"];
    CountingSort.Sort<string>(ranged.AsSpan(), rangedPayload.AsSpan(), -5, 3);
    Check(ranged[0] == -5 && rangedPayload[0] == "e", "CountingSort range key+payload");

    int[] scratchRanged = [2, 1, 0];
    CountingSort.SortWithScratch(scratchRanged.AsSpan(), 0, 2, new int[CountingSort.RequiredCounts(0, 2)].AsSpan());
    Check(scratchRanged[0] == 0 && scratchRanged[^1] == 2, "CountingSort range with counter buffer");

    int[] selection = [9, 1, 8, 2, 7, 3, 6, 4, 5, 0];
    PartialSort.Sort(selection.AsSpan(), 3);
    Check(selection[0] == 0 && selection[1] == 1 && selection[2] == 2, "PartialSort.Sort smallest prefix");

    PartialSort.Select(selection.AsSpan(), 5, default(DescendingIntComparer));
    Array.Sort(selection, 0, 5);
    Check(selection[0] == 5 && selection[4] == 9, "PartialSort.Select with a struct comparer");

    int[] top = new int[3];
    Check(PartialSort.TopK<int>(selection, top.AsSpan()) == 3 && top[0] == 9 && top[^1] == 7, "PartialSort.TopK");
}

if (failures == 0)
{
    Console.WriteLine("Celerity AOT smoke test: all checks passed.");
    return 0;
}

Console.Error.WriteLine($"Celerity AOT smoke test: {failures} check(s) failed.");
return 1;

// A hand-written struct comparer for the BTreeSet instantiation above: a second closed generic, so ILC
// compiles the constrained IComparer<int> call for a comparer other than DefaultComparer<int>.
internal readonly struct DescendingIntComparer : IComparer<int>
{
    public int Compare(int x, int y) => y.CompareTo(x);
}

// A hand-written monoid for the SegmentTree instantiation above: a reference-typed, non-commutative fold, so
// ILC compiles the constrained IMonoid<T> call for something other than the built-in numeric monoids.
internal readonly struct AotConcatMonoid : IMonoid<string>
{
    public string Identity => string.Empty;

    public string Combine(string left, string right) => left + right;
}
