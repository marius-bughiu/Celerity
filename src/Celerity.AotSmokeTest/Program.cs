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

if (failures == 0)
{
    Console.WriteLine("Celerity AOT smoke test: all checks passed.");
    return 0;
}

Console.Error.WriteLine($"Celerity AOT smoke test: {failures} check(s) failed.");
return 1;
