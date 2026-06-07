# Celerity
[![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/v/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/vpre/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![Live benchmarks](https://img.shields.io/badge/benchmarks-live-0d6e6e?style=flat-square)](https://marius-bughiu.github.io/Celerity/dev/bench/) [![Coverage](https://marius-bughiu.github.io/Celerity/coverage/badge.svg)](https://marius-bughiu.github.io/Celerity/coverage/)

Celerity is a .NET library that provides specialized high-performance collections optimized for specific use cases. It includes data structures designed for better speed or memory efficiency compared to standard .NET collections. The package supports configurable load factors, multiple built-in hash functions, and allows users to define custom hash functions for fine-tuned performance.

## Collections

- `CelerityDictionary<TKey, TValue, THasher>` — generic dictionary with a struct hasher constraint.
- `RobinHoodDictionary<TKey, TValue, THasher>` — `CelerityDictionary`'s peer using Robin Hood open addressing: bounds probe-length variance so worst-case lookups stay close to average on clustered / adversarial keys (at the cost of a per-slot probe-distance `int`).
- `SwissDictionary<TKey, TValue, THasher>` — `CelerityDictionary`'s peer using Swiss-table SIMD group probing: a parallel control-byte array lets one `Vector128` compare test 16 slots per lookup, filtering candidates by a 7-bit hash tag before any key comparison (at the cost of a one-byte control tag per slot).
- `HashCachingDictionary<TKey, TValue, THasher>` — `CelerityDictionary`'s peer with a struct-of-arrays layout: a dense side array of 32-bit hash fingerprints lets probes scan metadata only and short-circuit expensive key equality on a single integer compare (at the cost of four bytes of metadata per slot). Best for lookup-heavy tables and costly-equality keys.
- `PooledCelerityDictionary<TKey, TValue, THasher>` — `CelerityDictionary`'s peer whose backing arrays are rented from `ArrayPool<T>.Shared` and returned on `Dispose`, cutting GC pressure for short-lived, frequently-rebuilt dictionaries. Same API plus `IDisposable`.
- `FrozenCelerityDictionary<TValue>` / `FrozenCelerityDictionary<TValue, THasher>` — build-once, read-many `string`-keyed dictionary that searches for a perfect (collision-free) hash so lookups are single-probe. Defaults to `StringFnV1AHasher`.
- `CelerityMultiMap<TKey, TValue, THasher>` — one-to-many map: each key groups multiple values (`Add` appends rather than overwrites). Implements `ILookup<TKey, TValue?>`.
- `SmallDictionary<TKey, TValue>` — flat-array, linear-scan dictionary tuned for the very-small (`n <= ~16`) case. No hasher: it never hashes, so a `0` / `null` / default key is stored inline rather than out-of-band.
- `IntDictionary<TValue>` / `IntDictionary<TValue, THasher>` — `int`-keyed specialization. Defaults to `Int32WangNaiveHasher`.
- `LongDictionary<TValue>` / `LongDictionary<TValue, THasher>` — `long`-keyed specialization. Defaults to `Int64WangNaiveHasher`.
- `CeleritySet<T, THasher>` — generic set counterpart to `CelerityDictionary`.
- `FrozenCeleritySet` / `FrozenCeleritySet<THasher>` — build-once, read-many `string` set that searches for a perfect (collision-free) hash so membership tests are single-probe. The set counterpart of `FrozenCelerityDictionary`; implements `IReadOnlySet<string>`. Defaults to `StringFnV1AHasher`.
- `IntSet` / `IntSet<THasher>` — `int`-keyed set specialization.
- `LongSet` / `LongSet<THasher>` — `long`-keyed set specialization. Defaults to `Int64WangNaiveHasher`.
- `BloomFilter<T, THasher>` — space-efficient **probabilistic** membership filter: bit-array storage with no false negatives and a tunable false-positive rate, using a fraction of a `HashSet<T>`'s memory. Add-and-test only (no `Remove`); derives its `k` bit probes from a single hasher call by double hashing.
- `BitSet` — dense, fixed-length **exact** bit vector packed into 64-bit words: `O(n/64)` population count (`Count`) via hardware popcount and SIMD-accelerated bulk `And` / `Or` / `Xor` / `Not`. A faster, count-aware alternative to `System.Collections.BitArray` for dense small-integer index sets.

All dictionaries implement `IReadOnlyDictionary<TKey, TValue?>` and ship allocation-free struct enumerators, `Keys` / `Values` views, and an `IEnumerable<KeyValuePair<TKey, TValue>>` constructor. The hash-table collections handle `default(TKey)` (or zero for `int` / `long` keys, `null` for reference-type keys) out-of-band so it never collides with the empty-slot sentinel; `SmallDictionary` has no hash table and stores the default key inline.

## Quick start

Install from NuGet:

```bash
dotnet add package Celerity.Collections
```

### `IntDictionary` — the int-keyed fast path

`IntDictionary<TValue>` defaults to `Int32WangNaiveHasher`, so most callers don't need to pick a hasher.

```csharp
using Celerity.Collections;

var counts = new IntDictionary<int>();
counts[42] = 1;
counts[42]++;            // indexer get/set
counts.TryAdd(7, 100);   // returns false if key already present, no overwrite
counts.Add(8, 200);      // throws ArgumentException if key already present

if (counts.TryGetValue(42, out var hits))
    Console.WriteLine(hits); // 2

counts.Remove(7);
Console.WriteLine(counts.Count); // 2

// foreach is allocation-free — Enumerator is a struct.
foreach (var kvp in counts)
    Console.WriteLine($"{kvp.Key} -> {kvp.Value}");
```

The zero key is a legitimate value, not the empty-slot sentinel — `counts[0] = 99` round-trips correctly. `LongDictionary<TValue>` follows the exact same surface for `long` keys (defaulting to `Int64WangNaiveHasher`).

### `CelerityDictionary` — generic keys with a struct hasher

For non-`int`/`long` keys, pick a hasher from `Celerity.Hashing` (or supply your own). `DefaultHasher<T>` falls back to `EqualityComparer<T>.Default.GetHashCode()` for arbitrary types.

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var byId = new CelerityDictionary<Guid, string, GuidHasher>();
byId[Guid.NewGuid()] = "alice";

var byName = new CelerityDictionary<string, int, StringFnV1AHasher>();
byName["bob"] = 1;

// DefaultHasher<T> works for any type but pays the EqualityComparer<T> dispatch.
var byKey = new CelerityDictionary<DateOnly, string, DefaultHasher<DateOnly>>();
byKey[DateOnly.FromDateTime(DateTime.UtcNow)] = "today";
```

The hasher is a `struct` and is supplied as a generic constraint, so the JIT devirtualizes and inlines the `Hash()` call on the probe path.

### `RobinHoodDictionary` — bounded probe variance for clustered keys

When keys bunch up (weak or identity hashers, attacker-influenced keys, naturally clustered IDs), linear probing grows long runs and worst-case lookups degrade. `RobinHoodDictionary` is a drop-in peer of `CelerityDictionary` — same API, same hashers — that uses Robin Hood open addressing to keep probe-length variance low, so tail-latency lookups stay close to the average. It also stops a *negative* lookup early using its probe-distance invariant.

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var dict = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
dict[42] = "hello";

if (dict.TryGetValue(42, out var val))
    Console.WriteLine(val); // "hello"
```

The trade-off is a per-slot probe-distance `int` of bookkeeping (so it allocates more than `CelerityDictionary`); on uniformly distributed keys with a good hasher, `CelerityDictionary` matches or beats it, so prefer Robin Hood specifically for the clustered / adversarial case.

### `SwissDictionary` — SIMD group probing for lookup-heavy tables

`SwissDictionary` is a drop-in peer of `CelerityDictionary` — same API, same hashers — that resolves collisions Swiss-table style. A parallel array of one-byte control tags lets a single `Vector128` compare test a whole 16-slot group per probe, and a 7-bit hash tag in each control byte filters out non-matching residents before any key comparison. The portable `Vector128` API JITs to SSE2 / AVX2 on x86, AdvSimd on Arm, and a scalar fallback elsewhere, so it is correct everywhere and fast where hardware SIMD is available.

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var dict = new SwissDictionary<int, string, Int32WangNaiveHasher>();
dict[42] = "hello";

if (dict.TryGetValue(42, out var val))
    Console.WriteLine(val); // "hello"
```

The trade-off is a one-byte control tag per slot (so it allocates a little more than `CelerityDictionary`) plus tombstone deletion reclaimed by an occasional rehash; reach for it on lookup-heavy workloads — large tables, many negative lookups, or clustered keys — where the group compare and tag filtering pay off.

### `HashCachingDictionary` — struct-of-arrays hash caching for costly-equality keys

`HashCachingDictionary` is a drop-in peer of `CelerityDictionary` — same API, same hashers — that takes the struct-of-arrays layout one step further. Alongside the parallel `keys` / `values` arrays it keeps a dense side array of 32-bit hash fingerprints, one per slot. A linear probe scans only that compact metadata buffer, comparing the cached fingerprint first, and dereferences a key (running the full `EqualityComparer<TKey>` check) only on a fingerprint match — so cache-cold lookups stay inside one buffer and expensive key comparisons (long strings, large value-type keys) are filtered out by a single integer compare.

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var dict = new HashCachingDictionary<string, int, StringFnV1AHasher>();
dict["hello"] = 42;

if (dict.TryGetValue("hello", out var val))
    Console.WriteLine(val); // 42
```

The trade-off is four bytes of metadata per slot (so it allocates a little more than `CelerityDictionary`); reach for it on lookup-dominated workloads, costly-equality keys, or large cache-cold tables. It is complementary to `SwissDictionary`: both keep a metadata side array, but this is a scalar, wider-fingerprint design with backward-shift (tombstone-free) deletion, whereas Swiss uses SIMD group probing over one-byte tags. For small tables of cheap (e.g. `int`) keys, `CelerityDictionary` has the smaller footprint and is roughly a wash.

### `PooledCelerityDictionary` — pooled storage for short-lived dictionaries

When you build and discard many dictionaries on a hot path (per request, per frame, per batch), their backing arrays are a steady source of Gen 0 — and eventually Large Object Heap — garbage. `PooledCelerityDictionary` is a drop-in peer of `CelerityDictionary` whose key/value arrays are rented from `ArrayPool<T>.Shared` and returned on `Dispose` (and on every internal resize), so a build/use/dispose cycle recycles buffers instead of allocating fresh ones.

```csharp
using Celerity.Collections;
using Celerity.Hashing;

using (var dict = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>())
{
    dict[42] = "hello";

    if (dict.TryGetValue(42, out var val))
        Console.WriteLine(val); // "hello"
} // backing arrays return to ArrayPool<T>.Shared here
```

Dispose it (a `using` scope is ideal) so the buffers return to the pool; forgetting to dispose is not a leak, you just lose the pooling benefit. After disposal every member throws `ObjectDisposedException`. For a long-lived dictionary the pooling buys nothing — stay on `CelerityDictionary`. Not thread-safe.

### `FrozenCelerityDictionary` — build-once string lookups

When a `string`-keyed table is built once and then read many times (route tables, config maps, interned vocabularies), `FrozenCelerityDictionary<TValue>` searches at construction for a perfect (collision-free) hash so each lookup is a single hash, a single array index, and a single equality check.

```csharp
using Celerity.Collections;

var routes = new FrozenCelerityDictionary<int>(new[]
{
    new KeyValuePair<string, int>("/",        0),
    new KeyValuePair<string, int>("/health",  1),
    new KeyValuePair<string, int>("/metrics", 2),
});

Console.WriteLine(routes.IsPerfectlyHashed); // True — single-probe lookups
Console.WriteLine(routes["/health"]);        // 1
Console.WriteLine(routes.ContainsKey("/x")); // False
```

It is immutable (no `Add` / `Remove`) and implements `IReadOnlyDictionary<string, TValue?>`. The default uses `StringFnV1AHasher`; supply a full-width or strong hasher via `FrozenCelerityDictionary<TValue, THasher>` (e.g. `StringFnV1AFullHasher` for non-ASCII keys) when you want the single-probe fast path for keys the default would collide. Lookups stay correct regardless — colliding keys fall back to a short probe.

### `CelerityMultiMap` — one key, many values

When each key needs a *group* of values (event handlers per event, members per group, postings per term), `CelerityMultiMap<TKey, TValue, THasher>` appends on `Add` instead of overwriting, and hands back an allocation-free `ValueGroup` view on lookup.

```csharp
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

var subs = new CelerityMultiMap<string, string, StringFnV1AHasher>();
subs.Add("orders", "billing");
subs.Add("orders", "fulfilment");
subs.Add("shipments", "tracking");

Console.WriteLine(subs.Count);            // 2 distinct keys
Console.WriteLine(subs.ValueCount);       // 3 values
Console.WriteLine(subs["orders"].Count);  // 2
foreach (string handler in subs["orders"]) { /* billing, fulfilment */ }

subs.Remove("orders", "billing");         // drop one value
subs.RemoveAll("shipments");              // drop a whole key
```

The indexer returns an empty group for an absent key (it never throws), and the map implements `ILookup<TKey, TValue?>`, so it flows through LINQ (`subs.ToDictionary(g => g.Key, g => g.Count())`). `default(TKey)` (`null` / `0` / `Guid.Empty`) is an ordinary key, stored out-of-band.

### `SmallDictionary` — the tiny-map fast path

When a dictionary almost always holds a handful of entries (`n <= ~16`) — per-scope symbol tables, AST attribute bags, per-request maps — `SmallDictionary<TKey, TValue>` skips hashing entirely and linear-scans a flat array. At small `n` that beats a hash table: no hash to compute, no probe chain, and the whole key array fits in a cache line or two. There is no hasher to pick.

```csharp
using Celerity.Collections;

var scope = new SmallDictionary<string, int>();
scope["x"] = 1;
scope["y"] = 2;
scope.TryAdd("x", 99);            // false — already present, unchanged

Console.WriteLine(scope["x"]);    // 1
Console.WriteLine(scope.Count);   // 2
if (scope.TryGetValue("y", out int y)) { /* y == 2 */ }

scope.Remove("x");
foreach (var kvp in scope) { /* ("y", 2) */ }
```

It implements `IReadOnlyDictionary<TKey, TValue?>` with the same surface as the other dictionaries. Because it never hashes, a `0` / `null` / default key is stored inline like any other — there is no out-of-band slot. Lookups are `O(n)`, so it is the wrong choice once instances grow large; reach for `IntDictionary` / `CelerityDictionary` then.

### Sets

`IntSet` and `CeleritySet<T, THasher>` mirror the dictionary types for membership-only workloads.

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var seen = new IntSet();
seen.Add(1);
seen.Add(2);
Console.WriteLine(seen.Contains(1)); // true
seen.Remove(2);

var visitedIds = new CeleritySet<Guid, GuidHasher>();
visitedIds.TryAdd(Guid.NewGuid()); // returns true on first add, false on duplicate
```

### `FrozenCeleritySet` — build-once string membership

When a `string`-keyed membership set is built once and then read many times (reserved-word
tables, stop-word lists, allow/deny lists), `FrozenCeleritySet` searches at construction for a
perfect (collision-free) hash so each `Contains` is a single hash, a single array index, and a
single equality check. It is the set counterpart of `FrozenCelerityDictionary`.

```csharp
using Celerity.Collections;

var reserved = new FrozenCeleritySet(new[]
{
    "select", "from", "where", "join", "group", "order",
});

Console.WriteLine(reserved.IsPerfectlyHashed);  // True (single-probe membership)
Console.WriteLine(reserved.Contains("join"));   // True
Console.WriteLine(reserved.Contains("celerity")); // False
```

It is immutable (no `Add` / `Remove`) and implements `IReadOnlySet<string>` (so `SetEquals`,
`IsSubsetOf`, `Overlaps`, … are all available). Duplicate elements are silently deduplicated, as
a set should. The default uses `StringFnV1AHasher`; supply a full-width or strong hasher via
`FrozenCeleritySet<THasher>` (e.g. `StringFnV1AFullHasher` for non-ASCII elements) when you want
the single-probe fast path for elements the default would collide. Membership tests stay correct
regardless — colliding elements fall back to a short probe.

### `BloomFilter` — probabilistic membership at a fraction of the memory

When you only need a **membership gate** — "have I seen this before?" — and can tolerate a small,
bounded false-positive rate, `BloomFilter` stores nothing but a bit array, so it uses a fraction of
the memory of a `HashSet<T>` and never grows with element size. It guarantees **no false negatives**
(a `false` is always correct), while a `true` may be a false positive with the rate you size it for.

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Sized for 1,000,000 expected items at a 0.1% false-positive rate.
var seen = new BloomFilter<string, StringMurmur3Hasher>(1_000_000, 0.001);

seen.Add("https://example.com/a");

Console.WriteLine(seen.Contains("https://example.com/a")); // True  (definitely added)
Console.WriteLine(seen.Contains("https://example.com/z")); // False (definitely not — no false negatives)
```

It is add-and-test only: there is no `Remove` (clearing one bit could erase an unrelated element),
no enumeration, and no way to retrieve the stored elements — use `CeleritySet` / `FrozenCeleritySet`
when you need exact membership or the elements back. The `k` bit probes are derived from a single
hasher call by double hashing, so any `IHashProvider<T>` works; `Capacity`, `BitCount`, `HashCount`,
and `CurrentFalsePositiveProbability` expose the sizing and current fill. Merge two equally-sized
filters with `UnionWith`.

### Construct from an existing collection

The dictionaries accept any `IEnumerable<KeyValuePair<TKey, TValue>>`. When the source implements `ICollection<T>`, its `Count` is used to pre-size the backing storage so the bulk fill avoids resize work.

```csharp
var bcl = new Dictionary<int, string> { [1] = "a", [2] = "b", [3] = "c" };
var fast = new IntDictionary<string>(bcl);

var fromKvps = new CelerityDictionary<string, int, StringFnV1AHasher>(
    new[]
    {
        new KeyValuePair<string, int>("alice", 1),
        new KeyValuePair<string, int>("bob",   2),
    });
```

Duplicate keys (including duplicate `default(TKey)` / zero-key entries) throw `ArgumentException`, matching BCL `Dictionary<,>` semantics.

### Custom hasher

Implement `IHashProvider<T>` as a `struct` to plug in your own hash function. See [Custom hashing](#custom-hashing) below for the contract and a worked example.

## Choosing a collection

Celerity ships specialised types because each one buys a different tradeoff. Use the table below to pick the right one; if your workload doesn't appear here, the BCL collection is usually the right starting point.

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
| Need a stable iteration order or multi-threaded access | BCL `Dictionary<,>`, `ConcurrentDictionary<,>` | Celerity is single-threaded and iteration order is unspecified. |

Notes on picking a hasher once the collection is settled:

- For `int` / `long` keys, the convenience subclasses (`IntDictionary<TValue>`, `IntSet`, `LongDictionary<TValue>`, `LongSet`) already pick a sensible default — only override when you have evidence of clustered or adversarial keys, in which case escalate to `Int32WangHasher` then `Int32Murmur3Hasher` (for `int` keys) or `Int64WangHasher` then `Int64Murmur3Hasher` (for `long` keys). The Wang full-finalizer tier is a cheaper middle option than Murmur3 while still mixing every input bit.
- For `uint` keys, `UInt32Hasher` is the cheap XOR-fold default; escalate to `UInt32WangHasher` (the full Thomas-Wang finalizer) then `UInt32Murmur3Hasher` (the Murmur3 `fmix32` finalizer) when the fold produces clustering or you need strong avalanche on adversarial keys, mirroring the `int` family's two-step `Int32WangHasher` → `Int32Murmur3Hasher` escalation.
- For `ulong` keys, `UInt64Hasher` (the Murmur3 `fmix64` finalizer) is the strong default; drop down to `UInt64WangHasher` (the full Thomas-Wang `hash64shift` finalizer — only shifts and adds, no multiplies) when profiling shows the two `fmix64` multiplies are a hot-path cost and the keys are already reasonably uniform, or all the way down to the cheap XOR-fold `UInt64WangNaiveHasher` (the `ulong` counterpart to `Int64WangNaiveHasher`) when the keys are uniform and latency matters most.
- For `string` keys, `StringFnV1AHasher` is the fast default for ASCII-dominated workloads (it folds only the low byte of each character). When you want the simplest, most familiar cheap hash, `StringDjb2Hasher` (Bernstein's classic djb2) is the minimal option — a shift-and-add per byte with no real multiply, no table, and no finalizer, folding the full UTF-16 character so it still distinguishes upper-byte-distinct characters (e.g. `'A'` / `'Ł'`); its tradeoff is weaker avalanche than the FNV-1a or one-at-a-time hashers, so it shines on short ASCII identifiers but clusters sooner on adversarial keys. `StringDjb2AHasher` (the djb2a XOR-folding variant) is its sibling at the same cost — it XORs each byte into the accumulator instead of adding it (`* 33 ^ b` vs `* 33 + b`), exactly the FNV-1 → FNV-1a relationship, which sidesteps djb2's low-bit carry bias for slightly cleaner diffusion while keeping the same weak avalanche. `StringSdbmHasher` (the sdbm classic) is its peer in the same cheapest cost class — `hash * 65599` lowered to two shifts and a subtract, also a full-character fold with no finalizer; its larger multiplier tends to distribute slightly better than djb2 on short keys, sharing the same weak avalanche. `StringElfHasher` (the PJW / ELF symbol-table hash) is a third peer in that cheapest class — a shift-and-add whose top nibble is folded back into the low byte and cleared each step, so it recirculates high-order entropy (a touch more diffusion than the pure shift-and-add classics) while staying multiply-free, with a non-negative 28-bit result. `StringCrc32Hasher` (the standard CRC-32 / zlib / IEEE 802.3 checksum) rounds out the cheap tier as its only **table-driven** member — a 256-entry byte lookup table, full-character fold; being a linear checksum it has weaker avalanche than the designed mixers, so reach for it primarily when you need to reproduce a CRC-32-based key distribution exactly (e.g. to match an external sharding or storage scheme), the same compatibility role `StringMurmur2Hasher` and the FNV-1 hashers fill for their external systems. `StringAdler32Hasher` (the standard Adler-32 / zlib / RFC 1950 checksum) is its table-free checksum sibling — two running 16-bit sums modulo 65521, full-character fold; it is even weaker than CRC-32 as a hash (its low 16 bits are just the byte-sum, so unrelated short keys cluster badly), so reach for it only when you need to reproduce an Adler-32-based key distribution exactly (e.g. zlib, PNG, or rsync rolling-checksum compatibility), not as a general-purpose hash. If you specifically need the original **FNV-1** ordering (multiply-then-XOR) rather than the generally preferred FNV-1a — for example to match an external system — `StringFnV1Hasher` provides it with the same full-character fold, at the cost of slightly weaker avalanche on the trailing byte; `StringFnV164Hasher` is its 64-bit-accumulator counterpart, the same FNV-1 ordering carried through a wider state that clusters less on long or numerous keys (effectively free on 64-bit platforms). For keys with significant non-ASCII content, step up to `StringFnV1AFullHasher`, which folds the full UTF-16 character at the same FNV-1a cost class and so does not collide characters that differ only in their upper byte (e.g. `'A'` / `'Ł'`); for long or numerous keys where the 32-bit accumulator starts clustering, `StringFnV1A64Hasher` does the same full-character fold into a 64-bit state (effectively free on 64-bit platforms); when FNV-1a's single-multiply mixing clusters your keys but you don't want to pay for a block hash, `StringJenkinsOaatHasher` (Bob Jenkins' one-at-a-time hash) gives stronger per-bit avalanche at the same cheap, multiply-free cost class; escalate further to a strong-avalanche option when key distribution is clustered or adversarial — `StringMurmur3Hasher` (the `fmix32`-finalized MurmurHash3) for short keys (its older same-family sibling `StringMurmur2Hasher` provides Austin Appleby's original MurmurHash2 — one multiply per block and a lighter two-shift finalizer — for compatibility with external systems that hash with MurmurHash2, e.g. Hadoop or Cassandra; prefer MurmurHash3 otherwise, as its stronger finalizer avoids the known MurmurHash2 weaknesses), `StringXxHash32Hasher` (xxHash32, which keeps four accumulators in flight) when keys are long enough for its throughput-oriented stripe loop to pull ahead, `StringXxHash64Hasher` (xxHash64, folded to 32 bits) which widens those accumulators and the stripe further for longer keys on 64-bit platforms, `StringMetroHash64Hasher` (MetroHash64, folded to 32 bits) — a strong-avalanche option in the same throughput-oriented, four-accumulator class as the xxHash family that is often competitive on mid-length keys — `StringCityHash64Hasher` (CityHash64, folded to 32 bits), whose length-classed structure (dedicated branches for ≤ 16, 17–32, and 33–64 bytes before its 64-byte main loop) often edges ahead on the short-to-mid keys typical of identifiers, or `StringXxHash3Hasher` (XXH3, the third-generation xxHash folded to 32 bits), which is *both* length-classed for short keys and runs an eight-lane accumulator loop in bulk — typically the fastest of the strong options across both short and long keys; profile on your own key shape to choose between them. When the keys come from an untrusted source (request paths, header names, user-supplied identifiers) and an adversary could deliberately craft colliding keys, step beyond the throughput family to a keyed PRF: `StringHalfSipHash24Hasher` (HalfSipHash-2-4, the 32-bit-word SipHash sibling) is the cheapest keyed option on short keys and 32-bit targets, with a native 32-bit output and no fold; `StringSipHash13Hasher` (SipHash-1-3, the reduced-round 64-bit variant Rust's `HashMap` uses by default) is the fastest of the full 64-bit-state keyed options, `StringSipHash24Hasher` (SipHash-2-4, the conservative variant Python and Ruby use) trades a little throughput for extra cryptographic margin, and `StringHighwayHash64Hasher` (Google's HighwayHash64, the keyed hash designed as a SIMD-friendly successor to SipHash) is the wider-state alternative — all four trade some throughput for keyed collision resistance, with HighwayHash's edge realised once it is vectorized (the current implementation is a verified portable scalar one).
- For arbitrary types, `DefaultHasher<T>` (which delegates to `EqualityComparer<T>.Default.GetHashCode()`) is a safe fallback. It still benefits from the struct-hasher devirtualisation; the inner `EqualityComparer<T>` dispatch is the only unavoidable cost. Replace it with a hand-written struct hasher if profiling shows `Hash` on the hot path.
- The full hasher matrix lives in [`docs/api/hashing.md`](docs/api/hashing.md).

A few cases where Celerity is **not** the right answer today:

- **Concurrent reads/writes from multiple threads.** Celerity collections are single-threaded; use `ConcurrentDictionary<,>` or wrap a BCL `Dictionary<,>` in your own lock.
- **You need `IDictionary<,>` (mutable interface), `LINQ`-heavy code that relies on `Count`-via-extension on the boxed surface, or anything that depends on a specific iteration order.** Celerity exposes `IReadOnlyDictionary<,>` only and does not guarantee iteration order across versions.

## Benchmarks

**Up to 2.4&times; faster than `Dictionary<int, int>`** on lookups, with zero allocations. The [live dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) tracks every shipped collection against its .NET BCL counterpart on every `main` push, with historical trends and per-PR regression comparisons. For high-precision local numbers, run `dotnet run -c Release` in [`src/Celerity.Benchmarks`](src/Celerity.Benchmarks) — hosted CI runners are noisier than your laptop and the dashboard reflects that.

The suite also includes `StringHasherBenchmark` and `IntegerHasherBenchmark`, head-to-head throughput comparisons of every built-in `string` and integer/`Guid` hasher (each baselined against the framework `GetHashCode()`; the string benchmark sweeps short-ASCII, long-ASCII, and non-ASCII key shapes). They render on the dashboard under **Hash function throughput**, and run locally with `dotnet run -c Release -- --filter "*HasherBenchmark*"`. Read the throughput numbers alongside the distribution metrics from `HashQualityEvaluator` when picking a hasher — a fast hasher that clusters is not a win. See [Benchmarking the string hashers](docs/api/hashing.md#benchmarking-the-string-hashers).

Beyond the CI-tracked core, an **extended local suite** exercises the harder questions a single random-key benchmark can't answer: multiple key distributions (uniform / sequential / clustered / adversarial), million-item scale tests, allocation profiling, concurrent read scaling, cache-locality effects, a mixed read-heavy workload, and a comparison against `FrozenDictionary<,>`. These are heavier and noisier, so they run on demand rather than on every PR — e.g. `dotnet run -c Release -- --filter "*Distribution*"`. See the [extended benchmark suite](docs/performance.md#extended-benchmark-suite) for what each one measures and how to read it.

## Custom hashing

You can bring your own custom hash provider by implementing the `IHashProvider<T>` interface.

```csharp
public interface IHashProvider<T>
{
    int Hash(T key);
}
```

Hashers must be **structs** when used with Celerity collections (`where THasher : struct, IHashProvider<T>`) so the JIT can devirtualize and inline `Hash()`. The package ships built-in hashers for `int`, `long`, `uint`, `ulong`, `Guid`, and `string`, plus a `DefaultHasher<T>` fallback that delegates to `EqualityComparer<T>.Default.GetHashCode()`. See [`docs/api/hashing.md`](docs/api/hashing.md) for the full list.

Not sure which hasher to pick for your key shape? `HashQualityEvaluator.Evaluate<T, THasher>(keys)` runs a representative key sample through a hasher and returns a `HashQualityReport` of collision count, bucket occupancy, max bucket load, chi-squared, and a normalized distribution score (`1.0` = ideal uniform). It's a diagnostic tool — run it offline to compare candidate hashers before committing one. See the [hash quality evaluation](docs/api/hashing.md#hash-quality-evaluation) section.

## Native AOT & trimming

Celerity is **Native AOT and trimming compatible**. The library carries no reflection, runtime code generation, or dynamic type loading — every collection is a generic over a struct hasher, and the only BCL primitives on the hot paths are `MemoryMarshal`, `Unsafe`, and `EqualityComparer<T>.Default`, all of which are AOT-safe. The assembly is marked [`<IsAotCompatible>true</IsAotCompatible>`](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#aot-compatibility-analyzers), so consuming it from a `PublishAot` app produces **no trim or AOT warnings**.

```bash
dotnet publish -r linux-x64 -c Release   # in an app that references Celerity.Collections
```

Compatibility is enforced on every build: the trim and AOT Roslyn analyzers run as part of the library's compilation, and CI publishes a Native AOT smoke-test app that exercises every collection and hasher and runs the resulting native binary. See [`docs/aot.md`](docs/aot.md) for details.

## API overview

The dictionaries (`CelerityDictionary`, `IntDictionary`, `LongDictionary`) expose a compact, allocation-conscious API that mirrors the parts of `Dictionary<TKey, TValue>` most users actually reach for: indexer get/set, `ContainsKey`, `TryGetValue`, `Add`, `TryAdd`, `Remove` (both the `bool Remove(key)` and `bool Remove(key, out TValue?)` overloads), `Clear`, `Count`, `Keys`, `Values`, and `GetEnumerator()`. They implement `IReadOnlyDictionary<TKey, TValue?>` and accept an `IEnumerable<KeyValuePair<TKey, TValue>>` source at construction.

The sets (`CeleritySet`, `IntSet`, `LongSet`) expose `Add`, `TryAdd`, `Contains`, `Remove`, `Clear`, `Count`, and a struct enumerator.

The zero / `default(TKey)` key (or element, for sets) is stored out-of-band so it never collides with the empty-slot sentinel used during probing. This includes `null` for reference-type keys.

For full API details — constructors, method signatures, parameters, exceptions, and usage examples — see the **[API reference docs](docs/README.md)**.

## Project docs

- [`docs/`](docs/README.md) — documentation index.
- [Performance tuning guide](docs/performance.md) — capacity, load factor, hasher selection, and benchmarking.
- [Migration guide](docs/migration.md) — moving from `Dictionary<,>`, `HashSet<>`, `ILookup<,>`, and `FrozenDictionary<,>`.
- [Troubleshooting](docs/troubleshooting.md) and [FAQ](docs/faq.md).
- [Testing & coverage guide](docs/testing.md) — test layers, property-based and fuzz harnesses, coverage gating.
- [API reference](docs/README.md#api-reference) — collections, hashing, utilities.
- [`ROADMAP.md`](ROADMAP.md) — planned milestones and long-term vision.
- [`CHANGELOG.md`](CHANGELOG.md) — release notes.
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — build, test, PR conventions.
- [GitHub Issues](https://github.com/marius-bughiu/Celerity/issues) — open backlog and bug reports.
