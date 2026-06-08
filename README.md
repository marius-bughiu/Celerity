# Celerity
[![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/v/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/vpre/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![Live benchmarks](https://img.shields.io/badge/benchmarks-live-0d6e6e?style=flat-square)](https://marius-bughiu.github.io/Celerity/dev/bench/) [![Coverage](https://marius-bughiu.github.io/Celerity/coverage/badge.svg)](https://marius-bughiu.github.io/Celerity/coverage/)

Celerity is a .NET library of specialized, high-performance collections — drop-in alternatives to the BCL that trade flexibility for speed or memory on specific workloads. Hashers are structs supplied as generic constraints (so the JIT inlines them), load factors are configurable, and you can plug in your own hash functions.

```bash
dotnet add package Celerity.Collections
```

> **New here?** Jump to [**Choosing a collection**](#choosing-a-collection) — the table maps your workload to the right type in one line.

## Collections

**Dictionaries**

- `CelerityDictionary<TKey, TValue, THasher>` — the generic baseline: open-addressed dictionary with a struct hasher constraint.
- `RobinHoodDictionary<TKey, TValue, THasher>` — Robin Hood probing bounds probe-length variance, keeping worst-case lookups close to average on clustered / adversarial keys (cost: a per-slot probe-distance `int`).
- `SwissDictionary<TKey, TValue, THasher>` — Swiss-table SIMD group probing: one `Vector128` compare tests 16 slots per lookup, filtered by a 7-bit hash tag (cost: one control byte per slot). For lookup-heavy tables.
- `HashCachingDictionary<TKey, TValue, THasher>` — struct-of-arrays layout: a dense side array of 32-bit hash fingerprints lets probes scan metadata only and skip expensive key equality on a single integer compare (cost: four bytes per slot). For costly-equality keys.
- `PooledCelerityDictionary<TKey, TValue, THasher>` — backing arrays rented from `ArrayPool<T>.Shared` and returned on `Dispose`, cutting GC pressure for short-lived, frequently-rebuilt dictionaries. Same API plus `IDisposable`.
- `FrozenCelerityDictionary<TValue>` / `<TValue, THasher>` — build-once, read-many `string`-keyed dictionary that searches for a perfect (collision-free) hash so lookups are single-probe.
- `CelerityMultiMap<TKey, TValue, THasher>` — one-to-many map: `Add` appends instead of overwriting. Implements `ILookup<TKey, TValue?>`.
- `SmallDictionary<TKey, TValue>` — flat-array, linear-scan dictionary for the very-small (`n <= ~16`) case. No hasher; the default key is stored inline.
- `IntDictionary<TValue>` / `LongDictionary<TValue>` — `int` / `long`-keyed specializations (default to `Int32WangNaiveHasher` / `Int64WangNaiveHasher`).

**Sets**

- `CeleritySet<T, THasher>` — generic set counterpart to `CelerityDictionary`.
- `FrozenCeleritySet` / `<THasher>` — build-once, read-many `string` set with single-probe membership. Implements `IReadOnlySet<string>`.
- `IntSet` / `LongSet` — `int` / `long`-keyed set specializations.

**Probabilistic & bit-level**

- `BloomFilter<T, THasher>` — **probabilistic** membership: bit-array storage, **no false negatives**, tunable false-positive rate, a fraction of a `HashSet<T>`'s memory. Add-and-test only.
- `BitSet` — dense, **exact** bit vector in 64-bit words: `O(n/64)` hardware popcount (`Count`) and SIMD bulk `And`/`Or`/`Xor`/`Not`. A faster, count-aware `BitArray`.
- `HyperLogLog<T, THasher>` — **probabilistic** cardinality estimator: counts *distinct* elements from a fixed ~16&#160;KB of registers (~0.8% error), never growing with the data. Mergeable.
- `CountMinSketch<T, THasher>` — **probabilistic** frequency estimator: estimates per-element counts from a fixed grid, **never underestimating** (overestimate bounded by `epsilon · TotalCount`). Mergeable.

All dictionaries implement `IReadOnlyDictionary<TKey, TValue?>` and ship allocation-free struct enumerators, `Keys` / `Values` views, and an `IEnumerable<KeyValuePair<TKey, TValue>>` constructor. The hash-table collections store `default(TKey)` (zero / `null`) out-of-band so it never collides with the empty-slot sentinel; `SmallDictionary` stores it inline.

## Quick start

Two examples cover the common surface; every other type has a runnable example in the [API reference](docs/api/collections.md) and in the collapsible sections below.

`IntDictionary<TValue>` defaults to `Int32WangNaiveHasher`, so most callers don't pick a hasher:

```csharp
using Celerity.Collections;

var counts = new IntDictionary<int>();
counts[42] = 1;
counts[42]++;            // indexer get/set
counts.TryAdd(7, 100);   // false if present, no overwrite
counts.Add(8, 200);      // throws if present

if (counts.TryGetValue(42, out var hits))
    Console.WriteLine(hits); // 2

counts.Remove(7);
foreach (var kvp in counts) // allocation-free struct enumerator
    Console.WriteLine($"{kvp.Key} -> {kvp.Value}");
```

The zero key is a legitimate value, not the sentinel — `counts[0] = 99` round-trips. `LongDictionary<TValue>` is the same surface for `long` keys.

For non-`int`/`long` keys, pick a hasher from `Celerity.Hashing` (or supply your own); `DefaultHasher<T>` falls back to `EqualityComparer<T>.Default`:

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var byId = new CelerityDictionary<Guid, string, GuidHasher>();
byId[Guid.NewGuid()] = "alice";

var byName = new CelerityDictionary<string, int, StringFnV1AHasher>();
byName["bob"] = 1;
```

The hasher is a `struct` generic constraint, so the JIT devirtualizes and inlines `Hash()` on the probe path.

<details>
<summary><b>Specialized dictionaries</b> — RobinHood, Swiss, HashCaching, Pooled, Frozen, MultiMap, Small</summary>

All four `CelerityDictionary` peers below are drop-in (same API, same hashers) and differ only in collision strategy / storage:

```csharp
// RobinHood — bounds probe variance for clustered / adversarial keys (also ends
// negative lookups early). Cost: a per-slot probe-distance int.
var rh = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
rh[42] = "hello";

// Swiss — SIMD group probing for lookup-heavy tables (large tables, many negative
// lookups). One Vector128 compare tests 16 slots, filtered by a 7-bit tag.
var swiss = new SwissDictionary<int, string, Int32WangNaiveHasher>();
swiss[42] = "hello";

// HashCaching — a 32-bit fingerprint side array skips costly key equality on a
// single int compare. For long-string / large value-type keys, cache-cold tables.
var hc = new HashCachingDictionary<string, int, StringFnV1AHasher>();
hc["hello"] = 42;

// Pooled — backing arrays rented from ArrayPool<T>.Shared. Dispose returns them;
// forgetting is not a leak, just no pooling benefit. After Dispose, members throw.
using var pooled = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
pooled[42] = "hello";
```

`FrozenCelerityDictionary<TValue>` is build-once, read-many and searches for a perfect (collision-free) hash at construction, so each lookup is single-probe. Immutable; implements `IReadOnlyDictionary<string, TValue?>`. Use the `<TValue, THasher>` overload (e.g. `StringFnV1AFullHasher` for non-ASCII keys) for the single-probe fast path on keys the default would collide — lookups stay correct regardless.

```csharp
var routes = new FrozenCelerityDictionary<int>(new[]
{
    new KeyValuePair<string, int>("/",        0),
    new KeyValuePair<string, int>("/health",  1),
    new KeyValuePair<string, int>("/metrics", 2),
});
Console.WriteLine(routes.IsPerfectlyHashed); // True
Console.WriteLine(routes["/health"]);        // 1
```

`CelerityMultiMap<TKey, TValue, THasher>` groups many values per key (`Add` appends), hands back an allocation-free `ValueGroup` on lookup, returns an empty group for absent keys, and implements `ILookup<TKey, TValue?>` (so it flows through LINQ):

```csharp
var subs = new CelerityMultiMap<string, string, StringFnV1AHasher>();
subs.Add("orders", "billing");
subs.Add("orders", "fulfilment");
Console.WriteLine(subs["orders"].Count);  // 2
subs.Remove("orders", "billing");         // drop one value
subs.RemoveAll("orders");                 // drop a whole key
```

`SmallDictionary<TKey, TValue>` skips hashing and linear-scans a flat array — at `n <= ~16` that beats a hash table (no hash, no probe chain, great cache locality). No hasher to pick; a `0` / `null` / default key is stored inline. Lookups are `O(n)`, so move to `IntDictionary` / `CelerityDictionary` once instances grow.

```csharp
var scope = new SmallDictionary<string, int>();
scope["x"] = 1;
scope.TryAdd("x", 99);          // false — already present
Console.WriteLine(scope["x"]);  // 1
```

</details>

<details>
<summary><b>Sets</b> — IntSet, CeleritySet, FrozenCeleritySet</summary>

```csharp
var seen = new IntSet();
seen.Add(1);
Console.WriteLine(seen.Contains(1)); // true

var visited = new CeleritySet<Guid, GuidHasher>();
visited.TryAdd(Guid.NewGuid()); // true on first add, false on duplicate
```

`FrozenCeleritySet` is the build-once, read-many string set counterpart of `FrozenCelerityDictionary` — single-probe `Contains`, immutable, implements `IReadOnlySet<string>` (so `SetEquals`, `IsSubsetOf`, `Overlaps`, … are available), and silently dedupes. Use `FrozenCeleritySet<THasher>` (e.g. `StringFnV1AFullHasher`) for non-ASCII elements.

```csharp
var reserved = new FrozenCeleritySet(new[] { "select", "from", "where", "join" });
Console.WriteLine(reserved.IsPerfectlyHashed); // True
Console.WriteLine(reserved.Contains("join"));  // True
```

</details>

<details>
<summary><b>Probabilistic & bit-level</b> — BloomFilter, HyperLogLog, CountMinSketch</summary>

`BloomFilter` is a membership gate that stores nothing but a bit array: **no false negatives** (a `false` is always correct), with a tunable false-positive rate. Add-and-test only (no `Remove`, no enumeration); merge equally-sized filters with `UnionWith`.

```csharp
var seen = new BloomFilter<string, StringMurmur3Hasher>(1_000_000, 0.001); // n, fp-rate
seen.Add("https://example.com/a");
Console.WriteLine(seen.Contains("https://example.com/a")); // True (definitely added)
Console.WriteLine(seen.Contains("https://example.com/z")); // False (no false negatives)
```

`HyperLogLog` estimates the **distinct count** of a stream from a fixed ~16&#160;KB of registers that never grow with the data (~0.8% error). Add-and-estimate only; `Precision` sets the accuracy trade-off (`StandardError` ≈ `1.04/√m`); merge equal-precision estimators with `UnionWith`.

```csharp
var unique = new HyperLogLog<long, Int64Murmur3Hasher>();
for (long id = 0; id < 10_000_000; id++)
    unique.Add(id % 1_000_000);
Console.WriteLine(unique.EstimateCardinality()); // ≈ 1,000,000 (±~0.8%), from 16 KB
```

`CountMinSketch` estimates **per-element frequencies** from a fixed grid of counters that never grows with the distinct-key count, and **never underestimates** (overestimate bounded by `epsilon · TotalCount`). Add-and-estimate only; `epsilon` / `delta` set the trade-off; merge equally-sized sketches with `UnionWith`.

```csharp
var hits = new CountMinSketch<string, StringMurmur3Hasher>(epsilon: 0.001, delta: 0.001);
foreach (string url in requestStream)
    hits.Add(url);
Console.WriteLine(hits.EstimateCount("/api/login")); // >= true count, over by <= 0.1% of total
```

`BitSet` is a dense, exact bit vector — see [the API reference](docs/api/collections.md#bitset) for popcount, the SIMD bulk operators, and the set-bit enumerator.

</details>

<details>
<summary><b>Construct from an existing collection</b></summary>

The dictionaries accept any `IEnumerable<KeyValuePair<TKey, TValue>>`; an `ICollection<T>` source is used to pre-size the backing storage so the bulk fill avoids resizes. Duplicate keys (including duplicate `default(TKey)`) throw `ArgumentException`, matching BCL `Dictionary<,>`.

```csharp
var bcl = new Dictionary<int, string> { [1] = "a", [2] = "b", [3] = "c" };
var fast = new IntDictionary<string>(bcl);

var fromKvps = new CelerityDictionary<string, int, StringFnV1AHasher>(new[]
{
    new KeyValuePair<string, int>("alice", 1),
    new KeyValuePair<string, int>("bob",   2),
});
```

</details>

## Choosing a collection

Each type buys a different tradeoff. Find your workload below; if it isn't here, the BCL collection is usually the right starting point.

| Your workload | Use | Why |
|---|---|---|
| Dictionary keyed by `int` | `IntDictionary<TValue>` | Avoids generic boxing / `EqualityComparer<int>` dispatch; defaults to `Int32WangNaiveHasher`. |
| Dictionary keyed by `long` | `LongDictionary<TValue>` | 64-bit equivalent of `IntDictionary`; defaults to `Int64WangNaiveHasher`. |
| Dictionary keyed by `Guid`, `string`, or any other type | `CelerityDictionary<TKey, TValue, THasher>` | Pick a struct hasher from `Celerity.Hashing` (e.g. `GuidHasher`, `StringFnV1AHasher`) so the JIT can inline `Hash()` on the probe path. |
| Dictionary with **clustered / adversarial** keys where worst-case lookup latency matters | `RobinHoodDictionary<TKey, TValue, THasher>` | Same API as `CelerityDictionary`, but Robin Hood probing bounds probe-length variance so tail-latency lookups don't degrade on bunched keys. Costs a per-slot probe-distance `int`; for uniform keys with a good hasher, prefer `CelerityDictionary`. |
| **Lookup-heavy** dictionary (large tables, many negative lookups) where SIMD pays off | `SwissDictionary<TKey, TValue, THasher>` | Same API as `CelerityDictionary`, but Swiss-table group probing tests 16 slots per `Vector128` compare and filters candidates by a 7-bit hash tag before any key comparison. Costs a one-byte control tag per slot; for small or write-dominated tables, `CelerityDictionary` is competitive. |
| **Lookup-heavy** dictionary with **costly key equality** (long strings, large value-type keys) or large cache-cold tables | `HashCachingDictionary<TKey, TValue, THasher>` | Same API as `CelerityDictionary`, but a dense side array of 32-bit hash fingerprints lets probes scan metadata only and short-circuit the key comparison on a single integer compare. Costs four bytes of metadata per slot; complementary to `SwissDictionary` (scalar wide fingerprint vs SIMD one-byte tags). For small tables of cheap keys, `CelerityDictionary` is roughly a wash. |
| **Short-lived** dictionary rebuilt frequently on a hot path where GC pressure matters | `PooledCelerityDictionary<TKey, TValue, THasher>` | Same API as `CelerityDictionary` plus `IDisposable`; rents its backing arrays from `ArrayPool<T>.Shared` and returns them on `Dispose`, so build/use/dispose cycles recycle buffers instead of allocating. Dispose it (a `using` scope); for long-lived dictionaries the pooling buys nothing, so prefer `CelerityDictionary`. |
| Build-once, read-many lookup table keyed by `string` | `FrozenCelerityDictionary<TValue>` | Immutable; searches for a perfect (collision-free) hash at build time so lookups are single-probe. Tune the hasher via the `<TValue, THasher>` overload. |
| One key maps to **many** values (one-to-many) | `CelerityMultiMap<TKey, TValue, THasher>` | `Add` appends to a per-key value group instead of overwriting; implements `ILookup<,>`. Pick the struct hasher for your key type, as with `CelerityDictionary`. |
| Tiny dictionary (`n <= ~16`) that stays small | `SmallDictionary<TKey, TValue>` | Flat-array linear scan beats hashing at small `n` — no hash to compute, great cache locality, no hasher to pick. Degrades to `O(n)` for large key sets, so only when instances stay small. |
| Set of `int` values | `IntSet` | Same fast path as `IntDictionary`, membership only. |
| Set of `long` values | `LongSet` | 64-bit equivalent of `IntSet`; defaults to `Int64WangNaiveHasher`. |
| Set of any other type | `CeleritySet<T, THasher>` | Same hasher choice as `CelerityDictionary`. |
| Build-once, read-many membership set keyed by `string` | `FrozenCeleritySet` | Immutable; searches for a perfect (collision-free) hash at build time so `Contains` is single-probe. The set counterpart of `FrozenCelerityDictionary`; implements `IReadOnlySet<string>`. Tune the hasher via the `<THasher>` overload. |
| **Membership gate** where a small, bounded false-positive rate is acceptable in exchange for a large memory saving (dedup pre-filters, "have I seen this before?" guards in front of an expensive exact lookup) | `BloomFilter<T, THasher>` | Probabilistic: bit-array storage with **no false negatives** and a tunable false-positive rate, using a fraction of a `HashSet<T>`'s memory and never growing with element size. Add-and-test only — no `Remove`, no enumeration, no retrieval. If you need exact membership or to get the elements back, use `CeleritySet` / `FrozenCeleritySet`. |
| **Dense set of small integer indices** (or a fixed universe of flags) where you count set bits or combine whole vectors — bitmaps, visited/presence masks, sieves | `BitSet` | Exact dense bit vector packed into 64-bit words: `O(n/64)` population count (`Count`) via hardware popcount and SIMD bulk `And`/`Or`/`Xor`/`Not`. A faster, count-aware `System.Collections.BitArray`. For **sparse** indices over a huge/unbounded domain, `IntSet` / `LongSet` is more memory-efficient; for approximate membership over arbitrary elements, use `BloomFilter`. |
| **Distinct count** over a large or unbounded stream (unique visitors / events, distinct-value cardinality, deduplicated counts across shards) where a small relative error is acceptable | `HyperLogLog<T, THasher>` | Probabilistic: estimates the distinct count from a fixed array of registers (16&#160;KB at the default precision) with a ~0.8% relative standard error, never growing with the cardinality — unlike a `HashSet<T>` that stores every distinct value. Add-and-estimate only; merge shard estimators with `UnionWith`. If you need an exact count or to test a specific element, use `HashSet<T>` / `CeleritySet`; for approximate *membership* rather than counting, use `BloomFilter`. |
| **Per-element frequency** over a large or unbounded stream (heavy hitters / top-k, approximate per-key counts, rate limiting, deduplicated frequency counts across shards) where a small one-sided overestimate is acceptable | `CountMinSketch<T, THasher>` | Probabilistic: estimates each element's frequency from a fixed grid of counters (sized from `epsilon` / `delta`) that never grows with the distinct-key count — unlike a `Dictionary<TKey, int>` frequency table. **Never underestimates**; overestimates bounded by `epsilon · TotalCount` with confidence `1 − delta`. Add-and-estimate only; merge shard sketches with `UnionWith`. If you need exact counts or to enumerate keys, use a `Dictionary<TKey, int>`; for the distinct *count* use `HyperLogLog`, for approximate *membership* use `BloomFilter`. |
| Need a stable iteration order or multi-threaded access | BCL `Dictionary<,>`, `ConcurrentDictionary<,>` | Celerity is single-threaded and iteration order is unspecified. |

**Celerity is not the right answer when** you need concurrent access (use `ConcurrentDictionary<,>` or your own lock — Celerity is single-threaded), the mutable `IDictionary<,>` interface, or a guaranteed iteration order (Celerity exposes `IReadOnlyDictionary<,>` only and does not promise order across versions).

## Choosing a hasher

Once the collection is settled, pick a hasher for your key shape. Defaults are good; escalate only with evidence (clustering, adversarial input). The [full hasher matrix](docs/api/hashing.md) documents every option and its tradeoff.

| Key type | Default | When to escalate |
|---|---|---|
| `int` / `long` | `Int32WangNaiveHasher` / `Int64WangNaiveHasher` (built into `IntDictionary` / `LongDictionary`) | Uniform / trusted keys (dense sequential IDs) → *drop* to `Int32IdentityHasher` / `Int64IdentityHasher` (the zero-work floor — no mixing, nothing beats it on speed). Clustered keys → `Int32WangHasher` → `Int32Murmur3Hasher` (the Wang full finalizer is a cheaper middle tier than Murmur3). |
| `uint` / `ulong` | `UInt32Hasher` (cheap XOR-fold) / `UInt64Hasher` (`fmix64`) | `uint`: → `UInt32WangHasher` → `UInt32Murmur3Hasher`. `ulong`: drop to `UInt64WangHasher` / `UInt64WangNaiveHasher` when the `fmix64` multiplies cost more than they buy on uniform keys. |
| `string` (ASCII) | `StringFnV1AHasher` (folds the low byte per char) | Non-ASCII or long keys → `StringFnV1AFullHasher` / `StringFnV1A64Hasher`. Clustered keys → strong-avalanche `StringMurmur3Hasher`, `StringXxHash3Hasher`, etc. |
| `string` (untrusted input) | `DefaultHasher<string>` (BCL Marvin32, per-process-randomized) | A **keyed** PRF — `StringSipHash13Hasher` (Rust's default), `StringSipHash24Hasher`, `StringHalfSipHash24Hasher`, or `StringHighwayHash64Hasher` — but only resists hash-flooding if seeded with a *secret, per-process-random* key; with a fixed seed it is deterministic, not DoS-resistant (see caveat below). |
| `Guid` | `GuidHasher` | — |
| Any other type | `DefaultHasher<T>` (delegates to `EqualityComparer<T>.Default`) | Replace with a hand-written struct hasher if profiling shows `Hash` on the hot path. |

The value of a struct hasher is **distribution quality (avalanche), determinism, and the zero-cost devirtualized generic** — *not* raw hashing speed. For `int` keys especially, `GetHashCode()` is already the identity (zero work), so no mixing hasher beats it on speed; `Int32IdentityHasher` / `Int64IdentityHasher` expose that zero-work floor explicitly so you can *skip* mixing when keys are already uniform, and you escalate to a mixer only when distribution (not speed) demands it.

> **Fixed-seed hashers are not a HashDoS defence.** `string.GetHashCode()` is already a purpose-built **Marvin32** with per-process random seeding; a hardcoded-seed Murmur3 / FNV / xxHash is *not* more flood-resistant — usually **less**, because an attacker who knows the fixed algorithm and seed can precompute colliding keys offline. What stops hash-flooding is a **keyed** PRF with a *secret, per-process-random* key, not merely picking a "stronger" fixed hash. For untrusted `string` keys, the BCL `string.GetHashCode()` (`DefaultHasher<string>`) is the safe default; reach for the keyed SipHash / HighwayHash hashers only when you also supply a secret seed. The fixed-seed hashers' real strength is **reproducibility** (same code across processes and runtimes), which `GetHashCode()` deliberately does not give you.

The hashing library also ships classic / compatibility hashes (djb2, sdbm, ELF/PJW, CRC-32, Adler-32, FNV-1, MurmurHash2, CityHash, MetroHash, xxHash32/64) for matching an external system's key distribution — see [`docs/api/hashing.md`](docs/api/hashing.md) for the complete list, costs, and avalanche notes, and use `HashQualityEvaluator` (below) to compare candidates on your own keys.

## Benchmarks

**Up to 2.4&times; faster than `Dictionary<int, int>`** on lookups, with zero allocations — this is a *collection-layout* win (open addressing with direct `==` key comparison and no per-call `EqualityComparer<T>` dispatch), **independent of the hasher**. It does *not* mean the hashers beat `GetHashCode()` on speed (they don't, and for `int` cannot — see [Choosing a hasher](#choosing-a-hasher)). The [live dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) tracks every shipped collection against its BCL counterpart on every `main` push, with historical trends and per-PR regression comparisons. For high-precision local numbers, run `dotnet run -c Release` in [`src/Celerity.Benchmarks`](src/Celerity.Benchmarks) — hosted CI runners are noisier than your laptop.

The suite also includes `StringHasherBenchmark` and `IntegerHasherBenchmark` (every built-in hasher bracketed by two baselines — the direct `GetHashCode()` and `EqualityComparer<T>.Default.GetHashCode()`, the per-probe call a BCL `Dictionary<,>` actually makes; rendered under **Hash function throughput** on the dashboard; run locally with `--filter "*HasherBenchmark*"`). Treat these as a **raw-mixing-cost diagnostic only** and read them alongside the distribution metrics from `HashQualityEvaluator` — a fast hasher that clusters is not a win. The isolated `Hash()` number alone is misleading (for `int`, `GetHashCode()` is identity — *zero* work — so no mixer can beat it), so the extended suite adds `HasherEndToEndBenchmark`, which times each hasher **through the dictionary** across all four key shapes, and a deterministic probe-length report (`dotnet run -c Release -- --probe-analysis`) — the cases where a strong hasher "loses" the microbench but wins end-to-end. See [measuring probe length](docs/performance.md#measure-probe-length-not-just-hash-speed).

An **extended local suite** answers the harder questions a single random-key benchmark can't: multiple key distributions (uniform / sequential / clustered / adversarial), million-item scale, allocation profiling, concurrent read scaling, cache locality, mixed read-heavy workloads, and a `FrozenDictionary<,>` comparison. These run on demand — e.g. `dotnet run -c Release -- --filter "*Distribution*"`. See the [extended benchmark suite](docs/performance.md#extended-benchmark-suite).

## Custom hashing

Implement `IHashProvider<T>` as a **struct** (required by `where THasher : struct, IHashProvider<T>`) so the JIT can devirtualize and inline `Hash()`:

```csharp
public interface IHashProvider<T>
{
    int Hash(T key);
}
```

The package ships built-in hashers for `int`, `long`, `uint`, `ulong`, `Guid`, and `string`, plus a `DefaultHasher<T>` fallback. Not sure which fits your key shape? `HashQualityEvaluator.Evaluate<T, THasher>(keys)` runs a key sample through a hasher and returns a `HashQualityReport` (collision count, bucket occupancy, max bucket load, chi-squared, and a normalized distribution score where `1.0` = ideal uniform) — a diagnostic to compare candidates offline before committing. For the metric a lookup actually pays, `ProbeStatisticsEvaluator.Evaluate<T, THasher>(keys)` replays the real open-addressed linear-probing placement and returns a `ProbeStatistics` (average / worst-case **probe length** and the open-addressing collision rate). See [`docs/api/hashing.md`](docs/api/hashing.md#hash-quality-evaluation).

## Primitives

`FastUtils` exposes low-level math helpers that fill genuine BCL gaps. `FastMod` / `FastDiv` are Lemire's reciprocal modulo and division: when a **divisor is fixed at run time** and reused across a hot loop (hash buckets, ring buffers, sharding, rate limiting), precompute a reciprocal once and each `value % divisor` / `value / divisor` becomes a multiply-and-shift — **2–4× faster** than the long-latency hardware `DIV` (the same trick the BCL uses internally but keeps `private`). 32- and 64-bit overloads; both reproduce the built-in operators bit-for-bit.

```csharp
ulong multiplier = FastUtils.GetFastModMultiplier(shardCount);   // once
uint shard = FastUtils.FastMod(key, shardCount, multiplier);     // == key % shardCount, per item
```

The `Celerity.Primitives` namespace also ships a curated suite of **struct PRNGs** — `Xoshiro256StarStar` (general-purpose default), `Xoroshiro128Plus` (fast doubles), `WyRand` (raw throughput), `SplitMix64` (seed expander), and `Pcg32` (statistical reputation + independent streams). `System.Random` is a heap class behind virtual dispatch whose **seeded** path falls back to the legacy Knuth algorithm; these are value types with no allocation and no virtual dispatch, and the shared `NextDouble` / `NextSingle` / bounded-and-unbiased `NextInt` / `NextBytes` surface inlines through a `where TRng : struct, IRandomSource` constraint, so they work generically (a zero-cost shuffle) and reproducibly from an explicit seed.

```csharp
using Celerity.Primitives;

var rng = new Xoshiro256StarStar(seed: 12345);   // deterministic
double unit = rng.NextDouble();                  // [0, 1)
int dieRoll = rng.NextInt(1, 7);                 // [1, 7), unbiased (Lemire)
```

`Celerity.Primitives` also ships **`VarInt`**, a span-based variable-length integer codec: LEB128 for `uint` / `ulong` and zig-zag + LEB128 for `int` / `long`, encoding straight over a caller-owned `Span<byte>` with **no stream and no allocation**. The BCL exposes 7-bit-encoded integers only on `BinaryWriter` / `BinaryReader` (stream-bound and allocating); `VarInt` is the no-alloc span path custom wire codecs and serializers actually want. Every `TryWrite` / `TryRead` is bounds-safe (returns `false` on a short or truncated buffer, never throws).

```csharp
Span<byte> buffer = stackalloc byte[VarInt.MaxVarIntLength64];
VarInt.TryWriteVarInt(buffer, 300u, out int n);              // n == 2
VarInt.TryReadVarInt(buffer, out uint value, out int read);  // value == 300
```

`FastUtils` also exposes **`CountDigits`** — the base-10 digit count of an integer, for sizing a buffer before `TryFormat`, aligning fixed-width numeric columns, or pre-measuring log / CSV / JSON output. The BCL's fast LZCNT-based counter is `internal`, and the only public base-10 log is the floating-point `Math.Log10`, which is slower and **mis-rounds at exact powers of ten**. `CountDigits` is exact and branch-lean (the 32-bit path is a single `Log2`/LZCNT plus a table lookup); the companion integer `Log10` is `CountDigits - 1`. 32- and 64-bit unsigned overloads, plus signed overloads that count the magnitude (sign excluded, `MinValue` handled without overflow).

```csharp
int width = FastUtils.CountDigits(1234u);   // 4
Span<char> buf = stackalloc char[width];
(1234u).TryFormat(buf, out _);
```

See [`docs/api/utilities.md`](docs/api/utilities.md#fastmod--fastdiv) for the full surface and the generator-selection table.

## Native AOT & trimming

Celerity is **Native AOT and trimming compatible** — no reflection, runtime code generation, or dynamic type loading. Every collection is a generic over a struct hasher, and the only BCL primitives on the hot paths (`MemoryMarshal`, `Unsafe`, `EqualityComparer<T>.Default`) are AOT-safe. The assembly is marked [`<IsAotCompatible>true</IsAotCompatible>`](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#aot-compatibility-analyzers), so a `PublishAot` app gets **no trim or AOT warnings**. Compatibility is enforced on every build (the trim/AOT analyzers run during compilation) and CI publishes a Native AOT smoke-test binary exercising every collection and hasher. See [`docs/aot.md`](docs/aot.md).

## API at a glance

The dictionaries mirror the parts of `Dictionary<TKey, TValue>` most callers reach for: indexer get/set, `ContainsKey`, `TryGetValue`, `Add`, `TryAdd`, `Remove` (both overloads), `Clear`, `Count`, `Keys`, `Values`, `GetEnumerator()`. They implement `IReadOnlyDictionary<TKey, TValue?>` and accept an `IEnumerable<KeyValuePair<TKey, TValue>>` at construction. The sets expose `Add`, `TryAdd`, `Contains`, `Remove`, `Clear`, `Count`, and a struct enumerator. The zero / `default(TKey)` key (or element) is stored out-of-band so it never collides with the empty-slot sentinel.

Full constructors, signatures, exceptions, and per-type examples: **[API reference](docs/README.md)**.

## Project docs

- [`docs/`](docs/README.md) — documentation index & [API reference](docs/README.md#api-reference).
- [Performance tuning](docs/performance.md) · [Migration guide](docs/migration.md) · [Troubleshooting](docs/troubleshooting.md) · [FAQ](docs/faq.md) · [Testing & coverage](docs/testing.md).
- [`ROADMAP.md`](ROADMAP.md) · [`CHANGELOG.md`](CHANGELOG.md) · [`CONTRIBUTING.md`](CONTRIBUTING.md) · [GitHub Issues](https://github.com/marius-bughiu/Celerity/issues).
