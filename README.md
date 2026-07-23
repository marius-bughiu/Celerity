# Celerity
[![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/v/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/vpre/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![Live benchmarks](https://img.shields.io/badge/benchmarks-live-0d6e6e?style=flat-square)](https://marius-bughiu.github.io/Celerity/dev/bench/) [![Coverage](https://marius-bughiu.github.io/Celerity/coverage/badge.svg)](https://marius-bughiu.github.io/Celerity/coverage/)

Celerity is a .NET library of specialized, high-performance collections — drop-in alternatives to the BCL that trade flexibility for speed or memory on specific workloads. Hashers are structs supplied as generic constraints (so the JIT inlines them), load factors are configurable, and you can plug in your own hash functions.

```bash
dotnet add package Celerity.Collections
```

> **New here?** Jump to [**Choosing a collection**](#choosing-a-collection) — the table maps your workload to the right type in one line.

### Packages

As of 2.0.0 Celerity ships as three layered NuGet packages. **`Celerity.Collections` pulls in the other two transitively, so a single `dotnet add package Celerity.Collections` still gives you everything** — add the lower packages directly only if you want the hashers or primitives *without* the collections.

| Package | What it adds | Depends on |
|---|---|---|
| [`Celerity.Collections`](https://www.nuget.org/packages/Celerity.Collections/) | dictionaries, sets, frozen/perfect-hash collections, streaming sketches | `Celerity.Hashing`, `Celerity.Primitives` |
| `Celerity.Hashing` | `IHashProvider<T>`, the struct hashers, `HashQualityEvaluator` | `Celerity.Primitives` |
| `Celerity.Primitives` | `FastUtils`, struct PRNGs, `VarInt`, `FastGuid` | — |

> **Upgrading from 1.x?** Namespaces are unchanged except `FastUtils`, which moved from `Celerity` to `Celerity.Primitives`. See the [migration guide](docs/migration.md#200--the-package-split).

All three packages **multi-target `net8.0`, `net9.0`, and `net10.0`**, so NuGet hands your project the assembly built against its own runtime. `net8.0` (LTS) is the floor — Celerity runs anywhere from .NET 8 upward.

## Built with Celerity

Standalone libraries built **on top of** Celerity — each solves a real problem in a domain where a pure-managed .NET implementation is the right call (chatty per-element work over managed keys, fixed-memory streaming over unbounded input, or hashing that must be identical across runtimes) and dropping to a native C/C++ library would be a net loss. They ship as **separate NuGet packages** that depend on `Celerity.Collections` — add only the one you need.

| Package | What it gives you |
|---|---|
| [`Celerity.Ring`](https://www.nuget.org/packages/Celerity.Ring/)<br/>[![NuGet](https://img.shields.io/nuget/v/Celerity.Ring.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Ring/) | Consistent-hash & rendezvous (HRW) **rings** for sharding and request routing, generic over your key type and hasher — with **byte-identical** node assignment across OS / architecture / runtime (x64, arm64, Blazor WASM), so every node in a cluster agrees on the mapping. Fills a gap the BCL has no type for. [README](src/Celerity.Ring/README.md) |
| [`Celerity.Sentinel`](https://www.nuget.org/packages/Celerity.Sentinel/)<br/>[![NuGet](https://img.shields.io/nuget/v/Celerity.Sentinel.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Sentinel/) | Streaming **abuse / heavy-hitter detection** — surfaces the top offenders, per-key rate, and fan-out cardinality of a request stream in a **fixed footprint regardless of key cardinality**, so it survives the attacker key-rotation that drives a `Dictionary<,>` counter to OOM. Includes a striped tracker for concurrent hot paths. [README](src/Celerity.Sentinel/README.md) |
| [`Celerity.Cardinality`](https://www.nuget.org/packages/Celerity.Cardinality/)<br/>[![NuGet](https://img.shields.io/nuget/v/Celerity.Cardinality.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Cardinality/) | Mergeable approximate **`COUNT(DISTINCT)`** and windowed **dedup** over unbounded managed streams — exact for small inputs, promoting to a fixed ~16&#160;KB estimator past a threshold, with **deterministic cross-shard merge** identical on every runtime. [README](src/Celerity.Cardinality/README.md) |

> These are a separate tier from the core `Celerity.Collections` family above: they depend on it, but you don't get them by installing it — reach for one only when its problem is yours. (The NuGet badges populate on first publish.)

## Collections

**Dictionaries**

- `CelerityDictionary<TKey, TValue, THasher>` — the generic baseline: open-addressed dictionary with a struct hasher constraint.
- `RobinHoodDictionary<TKey, TValue, THasher>` — Robin Hood probing bounds probe-length variance, keeping worst-case lookups close to average on clustered / adversarial keys (cost: a per-slot probe-distance `int`).
- `SwissDictionary<TKey, TValue, THasher>` — Swiss-table SIMD group probing: one `Vector128` compare tests 16 slots per lookup, filtered by a 7-bit hash tag (cost: one control byte per slot). For lookup-heavy tables.
- `HashCachingDictionary<TKey, TValue, THasher>` — struct-of-arrays layout: a dense side array of 32-bit hash fingerprints lets probes scan metadata only and skip expensive key equality on a single integer compare (cost: four bytes per slot). For costly-equality keys.
- `PooledCelerityDictionary<TKey, TValue, THasher>` — backing arrays rented from `ArrayPool<T>.Shared` and returned on `Dispose`, cutting GC pressure for short-lived, frequently-rebuilt dictionaries. Same API plus `IDisposable`.
- `FrozenCelerityDictionary<TValue>` / `<TValue, THasher>` — build-once, read-many `string`-keyed dictionary that searches for a perfect (collision-free) hash so lookups are single-probe.
- `CelerityMultiMap<TKey, TValue, THasher>` — one-to-many map: `Add` appends instead of overwriting. Implements `ILookup<TKey, TValue?>`.
- `CelerityMultiSet<T, THasher>` — counting multiset (bag): each element maps to its multiplicity. Single-probe `Add`-increment for frequency counting, vs the two-probe `Dictionary<T,int>` idiom. `Count` (distinct) / `TotalCount` (occurrences).
- `SmallDictionary<TKey, TValue>` — flat-array, linear-scan dictionary for the very-small (`n <= ~16`) case. No hasher; the default key is stored inline.
- `EnumMap<TEnum, TValue>` — dense array-backed dictionary for **enum** keys (the .NET `EnumMap`): a lookup is a direct array index — no hashing, no probing, no collisions. Enumerates in ascending underlying-value order. The dictionary counterpart of `EnumSet`.
- `IntDictionary<TValue>` / `LongDictionary<TValue>` — `int` / `long`-keyed specializations (default to `Int32WangNaiveHasher` / `Int64WangNaiveHasher`).

**Sets**

- `CeleritySet<T, THasher>` — generic set counterpart to `CelerityDictionary`.
- `SwissSet<T, THasher>` — Swiss-table SIMD group probing for sets: one `Vector128` compare tests 16 slots per membership check, filtered by a 7-bit hash tag (cost: one control byte per slot). For membership-heavy sets with many negative lookups. The set counterpart of `SwissDictionary`.
- `RobinHoodSet<T, THasher>` — Robin Hood probing for sets: bounds probe-length variance and lets negative `Contains` lookups exit early on clustered / adversarial elements (cost: a per-slot probe-distance `int`). The set counterpart of `RobinHoodDictionary`.
- `HashCachingSet<T, THasher>` — struct-of-arrays layout for sets: a dense side array of 32-bit hash fingerprints lets probes scan metadata only and short-circuit expensive element equality on a single integer compare (cost: four bytes per slot). For lookup-heavy sets and costly-equality elements. The set counterpart of `HashCachingDictionary`.
- `PooledCeleritySet<T, THasher>` — backing array rented from `ArrayPool<T>.Shared` and returned on `Dispose`, cutting GC pressure for short-lived, frequently-rebuilt sets. Same API plus `IDisposable`. The set counterpart of `PooledCelerityDictionary`.
- `FrozenCeleritySet` / `<THasher>` — build-once, read-many `string` set with single-probe membership. Implements `IReadOnlySet<string>`.
- `IntSet` / `LongSet` — `int` / `long`-keyed set specializations.
- `SmallSet<T>` — flat-array, linear-scan set for the very-small (`n <= ~16`) case. No hasher; the default element is stored inline. The set counterpart of `SmallDictionary`.
- `EnumSet<TEnum>` — bit-vector set for enum keys (the .NET `EnumSet`): membership is a single bit test and set algebra is word-wise bitwise ops, with no hashing or boxing. Enumerates in ascending underlying-value order.
- `SparseSet` — bounded-universe integer set (Briggs–Torczon sparse set): `O(1)` `Clear` that leaves the backing arrays untouched, plus dense, cache-friendly iteration — for clear-and-rebuild "visited" sets over ids in `[0, N)` (graph traversal, ECS, sweep-line). Costs `O(Universe)` memory.

The mutable sets (`CeleritySet`, `SwissSet`, `RobinHoodSet`, `HashCachingSet`, `IntSet`, `LongSet`, `SmallSet`, `EnumSet`, `SparseSet`) all implement **`ISet<T>`** — the full `HashSet<T>` set-algebra surface (`UnionWith` / `IntersectWith` / `ExceptWith` / `SymmetricExceptWith` and the `IsSubsetOf` / `IsSupersetOf` / `Overlaps` / `SetEquals` query family, plus `CopyTo`) with BCL semantics — so they drop in wherever a `HashSet<T>` is used.

**Caches**

- `LruCache<TKey, TValue, THasher>` — fixed-capacity **least-recently-used cache** with `O(1)` get/put and automatic eviction of the least-recently-used entry. Recency runs through an intrusive doubly-linked list over fixed-size arrays, so the hot get/put/evict path **allocates nothing** — unlike the idiomatic `Dictionary` + `LinkedList` LRU that heap-allocates a node per insert. Reads count as uses (a hit promotes the entry to most-recently-used); `TryPeek` / `ContainsKey` inspect without disturbing recency.

**Sequences**

- `Deque<T>` — growable **double-ended queue** backed by a **circular buffer**: `O(1)` amortized push / pop / peek at **both** ends, plus `O(1)` random access by index. The array-backed deque the BCL lacks (`Queue<T>` is FIFO-only, `Stack<T>` LIFO-only, and `LinkedList<T>` allocates a node per element) — a bounded FIFO / sliding-window churn reuses the buffer with wrap-around and **allocates nothing**, and enumeration walks contiguous memory. Implements `IReadOnlyList<T>`.

**Union-find**

- `DisjointSet<T>` — **union-find** over arbitrary elements: partitions them into disjoint sets with near-`O(1)` amortized `Union` / `Find` / `Connected` via **union by size** + **path halving**. The union-find the BCL lacks — incremental connectivity, connected components, Kruskal MST, and undirected cycle detection in near-linear total time, where the `Dictionary` + `HashSet` set-merge substitute is quadratic. `GetComponents()` materializes the current partition.

**Probabilistic & bit-level**

- `BloomFilter<T, THasher>` — **probabilistic** membership: bit-array storage, **no false negatives**, tunable false-positive rate, a fraction of a `HashSet<T>`'s memory. Add-and-test only.
- `CuckooFilter<T, THasher>` — **probabilistic** membership that **supports deletion**: fingerprint buckets, **no false negatives**, tunable false-positive rate, ≤2 cache lines per lookup. The Bloom filter you can `Remove` from.
- `XorFilter<T, THasher>` — **probabilistic** membership that is **build-once & immutable**: ~9.84 bits/element (smaller than a Bloom filter at the same rate), three probes + two XORs per lookup (branch-free). The smallest, fastest-to-query filter for a fixed element set.
- `BitSet` — dense, **exact** bit vector in 64-bit words: `O(n/64)` hardware popcount (`Count`) and SIMD bulk `And`/`Or`/`Xor`/`Not`. A faster, count-aware `BitArray`.
- `HyperLogLog<T, THasher>` — **probabilistic** cardinality estimator: counts *distinct* elements from a fixed ~16&#160;KB of registers (~0.8% error), never growing with the data. Mergeable.
- `CountMinSketch<T, THasher>` — **probabilistic** frequency estimator: estimates per-element counts from a fixed grid, **never underestimating** (overestimate bounded by `epsilon · TotalCount`). Mergeable.
- `TopKSketch<T, THasher>` — **probabilistic** top-k / heavy-hitters sketch (Space-Saving): reports a stream's most frequent elements from a fixed `k` monitors in `O(k)` memory, **never underestimating** and never missing a hitter above `TotalCount / k`.

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
<summary><b>Specialized dictionaries</b> — RobinHood, Swiss, HashCaching, Pooled, Frozen, MultiMap, MultiSet, Small, EnumMap</summary>

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

`CelerityMultiSet<T, THasher>` is the counting sibling — each element maps to its multiplicity. It's the type to reach for when building a frequency histogram: `Add` is a single probe-and-increment, where the idiomatic `Dictionary<T,int>` counting pattern (`d[x] = d.GetValueOrDefault(x) + 1`) costs two probes per item. `Count` is the distinct-element count; `TotalCount` is the sum of all occurrences:

```csharp
var freq = new CelerityMultiSet<string, StringFnV1AHasher>();
foreach (string w in "the cat sat on the mat the".Split(' '))
    freq.Add(w);
Console.WriteLine(freq["the"]);       // 3
Console.WriteLine(freq.Count);        // 5 distinct words
Console.WriteLine(freq.TotalCount);   // 7 occurrences
freq.SetCount("cat", 0);              // remove an element entirely
```

`SmallDictionary<TKey, TValue>` skips hashing and linear-scans a flat array — at `n <= ~16` that beats a hash table (no hash, no probe chain, great cache locality). No hasher to pick; a `0` / `null` / default key is stored inline. Lookups are `O(n)`, so move to `IntDictionary` / `CelerityDictionary` once instances grow.

```csharp
var scope = new SmallDictionary<string, int>();
scope["x"] = 1;
scope.TryAdd("x", 99);          // false — already present
Console.WriteLine(scope["x"]);  // 1
```

`EnumMap<TEnum, TValue>` is the dense array-backed dictionary for **enum** keys — the .NET analogue of Java's `EnumMap` and the dictionary counterpart of `EnumSet`. It maps the enum's underlying value straight to an array slot, so `this[key]` / `TryGetValue` / `ContainsKey` / `Add` / `Remove` are a shift-mask-and-index — no hashing, no probing, no collisions — and a full sweep is a linear array walk. A parallel occupancy bit vector means a key mapped to `default(TValue)` is a genuine entry, distinct from an absent one. It supports enums whose members are small non-negative integers (the default declaration); negative or sparse `[Flags]` enums throw `NotSupportedException` (use `CelerityDictionary` there). Enumeration is deterministic — ascending by underlying value.

```csharp
enum Priority { Low, Normal, High, Critical }

var queued = new EnumMap<Priority, int> { [Priority.Low] = 3, [Priority.High] = 7 };
queued[Priority.High]++;                             // direct array index, no hashing
Console.WriteLine(queued.ContainsKey(Priority.Normal)); // False — a single bit test
```

</details>

<details>
<summary><b>Sets</b> — IntSet, CeleritySet, SwissSet, RobinHoodSet, HashCachingSet, FrozenCeleritySet, SmallSet, EnumSet, SparseSet</summary>

```csharp
var seen = new IntSet();
seen.Add(1);
Console.WriteLine(seen.Contains(1)); // true

var visited = new CeleritySet<Guid, GuidHasher>();
visited.TryAdd(Guid.NewGuid()); // true on first add, false on duplicate
```

The mutable sets implement `ISet<T>`, so the full `HashSet<T>` set algebra works (with matching semantics):

```csharp
var a = new IntSet(new[] { 1, 2, 3, 4 });
a.IntersectWith(new[] { 2, 4, 6 });        // a -> { 2, 4 }
a.UnionWith(new[] { 4, 5 });               // a -> { 2, 4, 5 }
Console.WriteLine(a.IsSubsetOf(new[] { 2, 4, 5, 9 })); // true

ISet<int> asSet = a;                        // usable anywhere ISet<int>/ICollection<int> is expected
Console.WriteLine(asSet.Add(7));            // true — ISet<T>.Add is the non-throwing add
```

`SwissSet<T, THasher>` is the SIMD-probed set — the set counterpart of `SwissDictionary`. One `Vector128` compare tests a whole 16-slot group per membership check and filters candidates by a 7-bit hash tag before any element comparison, so negative `Contains` lookups (the common case for a set) stay cheap. Same API as `CeleritySet`, at the cost of one control byte per slot.

```csharp
var swissSeen = new SwissSet<int, Int32WangNaiveHasher>();
swissSeen.Add(42);
Console.WriteLine(swissSeen.Contains(999)); // false — negative lookup short-circuits on the group scan
```

`RobinHoodSet<T, THasher>` is the Robin Hood-probed set — the set counterpart of `RobinHoodDictionary`. It stores each element's probe-sequence length (distance from its ideal slot) so inserts displace richer residents ("rob from the rich"), bounding probe-length variance; a negative `Contains` exits as soon as the probe distance exceeds a resident's stored distance. Same API as `CeleritySet`, at the cost of a per-slot probe-distance `int` — reach for it on clustered / adversarial elements.

```csharp
var rhSeen = new RobinHoodSet<int, Int32WangNaiveHasher>();
rhSeen.Add(42);
Console.WriteLine(rhSeen.Contains(999)); // false — the PSL invariant stops the probe early
```

`HashCachingSet<T, THasher>` is the struct-of-arrays set — the set counterpart of `HashCachingDictionary`. It keeps a dense side array of 32-bit hash fingerprints alongside the elements, so a probe scans only that compact metadata and dereferences an element (running the full equality check) only on a fingerprint match. Same API as `CeleritySet`, at the cost of four bytes of metadata per slot — reach for it on lookup-heavy sets and elements with expensive equality (long strings, large structs).

```csharp
var hcSeen = new HashCachingSet<string, StringFnV1AHasher>();
hcSeen.Add("alpha");
Console.WriteLine(hcSeen.Contains("omega")); // false — rejected on the fingerprint compare
```

`FrozenCeleritySet` is the build-once, read-many string set counterpart of `FrozenCelerityDictionary` — single-probe `Contains`, immutable, implements `IReadOnlySet<string>` (so `SetEquals`, `IsSubsetOf`, `Overlaps`, … are available), and silently dedupes. Use `FrozenCeleritySet<THasher>` (e.g. `StringFnV1AFullHasher`) for non-ASCII elements.

```csharp
var reserved = new FrozenCeleritySet(new[] { "select", "from", "where", "join" });
Console.WriteLine(reserved.IsPerfectlyHashed); // True
Console.WriteLine(reserved.Contains("join"));  // True
```

`SmallSet<T>` is the flat-array set — the set counterpart of `SmallDictionary`. It skips hashing and linear-scans a flat array, which at `n <= ~16` beats a hash table (no hash, no probe chain, great cache locality). No hasher to pick; a `0` / `null` / default element is stored inline. Lookups are `O(n)`, so move to `IntSet` / `CeleritySet` once instances grow. Implements `ISet<T>`, so the full set algebra works.

```csharp
var seenScope = new SmallSet<string>();
seenScope.Add("x");
Console.WriteLine(seenScope.TryAdd("x")); // False — already present, unchanged
```

`EnumSet<TEnum>` is the bit-vector set for **enum** keys — the .NET analogue of Java's `EnumSet`. It stores one bit per possible element, so `Add` / `Contains` / `Remove` are a single shift-mask-and-bit-op (no hashing, no boxing) and set algebra between two `EnumSet`s is a word-wise bitwise `OR` / `AND` / `XOR` over a handful of `ulong`s. It supports enums whose members are small non-negative integers (the default declaration); negative or sparse `[Flags]` enums throw `NotSupportedException` (use `CeleritySet` there). Enumeration is deterministic — ascending by underlying value. `EnumSet<TEnum>.All()` builds the full universe of declared constants.

```csharp
var granted = new EnumSet<Permission> { Permission.Read, Permission.Write };
var required = new EnumSet<Permission> { Permission.Read, Permission.Execute };
Console.WriteLine(granted.IsSupersetOf(required)); // False — word-wise subset test
granted.UnionWith(required);                        // one bitwise OR
```

`SparseSet` is the bounded-universe integer set — the classic Briggs–Torczon sparse set (a dense value array + a sparse index array). Over a fixed universe `[0, Universe)` chosen at construction, `Add` / `Contains` / `Remove` are `O(1)` with no hashing, but the point of the type is what `HashSet<int>` can't match: `Clear()` is `O(1)` (it resets the count and version without scanning or clearing the backing arrays, versus zeroing the whole table) and iteration is a dense, contiguous scan over exactly the present elements. That is the winning shape for clear-and-rebuild "visited" sets — graph BFS/DFS, ECS entity membership, sweep-line — where the set is emptied every iteration. The cost is `O(Universe)` memory and non-negative-values-only: a value outside `[0, Universe)` throws on `Add` and reads as absent on `Contains` / `Remove`. It is an opt-in specialized type, not a `HashSet<int>` replacement — for an unbounded or huge-and-sparse key space, reach for `IntSet`.

```csharp
var visited = new SparseSet(nodeCount);   // universe = ids in [0, nodeCount)
visited.Add(start);
Console.WriteLine(visited.TryAdd(start)); // False — already seen, unchanged
visited.Clear();                          // O(1) — ready for the next traversal
```

</details>

<details>
<summary><b>Probabilistic & bit-level</b> — BloomFilter, CuckooFilter, XorFilter, HyperLogLog, CountMinSketch, TopKSketch</summary>

`BloomFilter` is a membership gate that stores nothing but a bit array: **no false negatives** (a `false` is always correct), with a tunable false-positive rate. Add-and-test only (no `Remove`, no enumeration); merge equally-sized filters with `UnionWith`.

```csharp
var seen = new BloomFilter<string, StringMurmur3Hasher>(1_000_000, 0.001); // n, fp-rate
seen.Add("https://example.com/a");
Console.WriteLine(seen.Contains("https://example.com/a")); // True (definitely added)
Console.WriteLine(seen.Contains("https://example.com/z")); // False (no false negatives)
```

`CuckooFilter` is the membership filter you can **delete from** — the same no-false-negatives contract and tunable false-positive rate as `BloomFilter`, but backed by fingerprint buckets so it supports `Remove`, with lookups touching at most two cache lines. Use it for a *shrinking* set (sliding windows, cache-admission, expiring keys); `BloomFilter` is simpler when the set only grows.

```csharp
var recent = new CuckooFilter<long, Int64WangHasher>(100_000, 0.001); // n, fp-rate
recent.Add(42);
Console.WriteLine(recent.Contains(42)); // True (definitely added)
recent.Remove(42);                      // Bloom cannot do this
Console.WriteLine(recent.Contains(42)); // False
```

`XorFilter` is the **build-once, immutable** membership filter — the smallest and fastest to query. You hand the whole element set to the constructor (there is no `Add`/`Remove`); in return it packs to ~9.84 bits/element (smaller than a Bloom filter at the same ~0.4% rate) and every `Contains` is exactly three probes and two XORs, with no probe loop or data-dependent branch. Use it for a *fixed* set — static allow/deny lists, a precomputed membership gate in front of an expensive exact lookup.

```csharp
var known = new XorFilter<string, StringXxHash3Hasher>(issuedApiKeys); // built once, then read-only
Console.WriteLine(known.Contains("key-abc")); // True if issued (or a ~0.4% false positive)
Console.WriteLine(known.Contains("key-zzz")); // False (no false negatives)
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

`TopKSketch` reports a high-cardinality stream's **most frequent elements** from a fixed `k` monitors (the Space-Saving algorithm), so its memory is `O(k)` instead of one entry per distinct key. It never underestimates a monitored count and never misses an element whose true frequency exceeds `TotalCount / k`. Add-and-query only (no `Remove`, and no `UnionWith` — bounded top-k summaries have no exact merge).

```csharp
var hot = new TopKSketch<string, StringMurmur3Hasher>(capacity: 100); // track the top ~100
foreach (string url in requestStream)
    hot.Add(url);
foreach (TopKEntry<string> e in hot.GetTopK(10))
    Console.WriteLine($"{e.Element}: ~{e.Count} (±{e.Error})"); // heaviest first, bounded error
```

`BitSet` is a dense, exact bit vector — see [the API reference](docs/api/collections.md#bitset) for popcount, the SIMD bulk operators, and the set-bit enumerator.

</details>

<details>
<summary><b>Caches</b> — LruCache</summary>

`LruCache` is a fixed-capacity **least-recently-used cache**: `O(1)` get/put, and once at capacity every insert evicts the least-recently-used entry. Its recency order runs through an intrusive doubly-linked list threaded over fixed-size arrays, so after construction the hot path allocates nothing — where the idiomatic `Dictionary` + `LinkedList` LRU heap-allocates a node per insert. A **read is a use**: a hit (indexer get or `TryGet`) promotes the entry to most-recently-used; `TryPeek` / `ContainsKey` inspect without touching recency.

```csharp
var cache = new LruCache<long, string, Int64WangHasher>(capacity: 3);
cache[1] = "one";
cache[2] = "two";
cache[3] = "three";       // full: MRU..LRU = 3, 2, 1
_ = cache[1];             // a hit promotes 1 -> MRU..LRU = 1, 3, 2
cache[4] = "four";        // evicts the least-recently-used (2), not 1
Console.WriteLine(cache.ContainsKey(2)); // False (evicted)
Console.WriteLine(cache.ContainsKey(1)); // True  (spared by the read)
```

</details>

<details>
<summary><b>Sequences</b> — Deque</summary>

`Deque<T>` is a growable **double-ended queue** backed by a **circular buffer**: `O(1)` amortized push / pop / peek at both ends, plus `O(1)` random access by index. The BCL has no deque — `Queue<T>` is FIFO-only, `Stack<T>` LIFO-only, and only `LinkedList<T>` supports both ends, at the cost of a heap-allocated node per element. A bounded FIFO or sliding-window churn reuses the buffer with wrap-around, so after warm-up it allocates nothing, and enumeration walks contiguous memory. See [the API reference](docs/api/collections.md#dequet).

```csharp
var work = new Deque<int>(new[] { 1, 2, 3 }); // front-to-back: 1, 2, 3
work.PushFront(0);                            // [0, 1, 2, 3]
work.PushBack(4);                             // [0, 1, 2, 3, 4]
int hi = work.PopFront();                     // 0 — take from the front
int lo = work.PopBack();                      // 4 — take from the back
int mid = work[1];                            // 2 — O(1) random access, front-relative
```

</details>

<details>
<summary><b>Union-find</b> — DisjointSet</summary>

`DisjointSet<T>` is the **union-find** the BCL lacks: it partitions arbitrary elements into disjoint sets and answers `Union` / `Find` / `Connected` in near-`O(1)` amortized time via **union by size** + **path halving**. `Union` auto-adds missing elements, so it doubles as the edge-insertion primitive; `Connected` is a pure query that never mutates. Ideal for incremental connectivity, connected components, Kruskal's MST, and undirected cycle detection — a whole stream of merges runs in near-linear time, where a `Dictionary` + `HashSet` set-merge is quadratic.

```csharp
var uf = new DisjointSet<string>();
foreach (var (u, v) in new[] { ("a", "b"), ("b", "c"), ("d", "e") })
    uf.Union(u, v);

Console.WriteLine(uf.Connected("a", "c")); // True  (a-b-c chain)
Console.WriteLine(uf.Connected("a", "e")); // False (separate component)
Console.WriteLine(uf.SetCount);            // 2: {a,b,c} and {d,e}
Console.WriteLine(uf.ComponentSize("a"));  // 3

foreach (var component in uf.GetComponents())
    Console.WriteLine(string.Join(", ", component));
```

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
| **Counting** occurrences / frequency histogram (element → count) | `CelerityMultiSet<T, THasher>` | `Add` is a single probe-and-increment vs the two-probe `Dictionary<T,int>` counting idiom; `SetCount` / `Remove` / `RemoveAll` manage multiplicities, `Count` is distinct elements and `TotalCount` the sum. Pick the struct hasher for your element type. |
| Tiny dictionary (`n <= ~16`) that stays small | `SmallDictionary<TKey, TValue>` | Flat-array linear scan beats hashing at small `n` — no hash to compute, great cache locality, no hasher to pick. Degrades to `O(n)` for large key sets, so only when instances stay small. |
| Dictionary keyed by a small **enum** — config-by-enum, per-state data, enum→handler tables | `EnumMap<TEnum, TValue>` | Dense array indexed on the enum's underlying value (the .NET `EnumMap`): `this[key]` / `TryGetValue` / `Add` / `Remove` are a single direct array index — no hashing, no probing, no collisions — and a full sweep is a linear array walk. The dictionary counterpart of `EnumSet`; enumerates ascending by value. For enums whose members are small non-negative integers (the default); negative or sparse `[Flags]` enums are unsupported — use `CelerityDictionary<TEnum, TValue, THasher>` there. |
| Tiny set (`n <= ~16`) that stays small — per-scope "seen" sets, small membership guards, deduping a handful of items | `SmallSet<T>` | The set counterpart of `SmallDictionary`: flat-array linear scan beats hashing at small `n`, no hasher to pick, the default element is stored inline. Implements `ISet<T>`. Degrades to `O(n)` for large sets, so only when instances stay small. |
| Set of **enum** values — flag sets, permission sets, state sets over a small enum | `EnumSet<TEnum>` | Bit-vector set indexed on the enum's underlying value (the .NET `EnumSet`): `Add` / `Contains` / `Remove` are a single bit op — no hashing, no boxing — and set algebra between two `EnumSet`s is a word-wise bitwise `OR` / `AND` / `XOR`. Enumerates ascending by value; `All()` builds the full universe. For enums whose members are small non-negative integers (the default); negative or sparse `[Flags]` enums are unsupported — use `CeleritySet<TEnum, THasher>` there. |
| Set of small **non-negative ints** over a bounded range that is **cleared & rebuilt often** — "visited" sets in graph BFS/DFS, ECS entity membership, sweep-line | `SparseSet` | Briggs–Torczon sparse set (dense value array + sparse index array): `O(1)` `Clear` that leaves the backing arrays untouched (vs `HashSet<int>` zeroing its table) and dense, cache-friendly iteration over just the present elements. `Add` / `Contains` / `Remove` are `O(1)`, no hashing. Costs `O(Universe)` memory and stores only values in `[0, Universe)`; for an unbounded or huge-and-sparse key space use `IntSet` / `HashSet<int>`. |
| Set of `int` values | `IntSet` | Same fast path as `IntDictionary`, membership only. |
| Set of `long` values | `LongSet` | 64-bit equivalent of `IntSet`; defaults to `Int64WangNaiveHasher`. |
| Set of any other type | `CeleritySet<T, THasher>` | Same hasher choice as `CelerityDictionary`. |
| **Membership-heavy** set (large sets, many negative `Contains` lookups, clustered elements) where SIMD pays off | `SwissSet<T, THasher>` | Same API as `CeleritySet`, but Swiss-table group probing tests 16 slots per `Vector128` compare and filters candidates by a 7-bit hash tag before any element comparison. The set counterpart of `SwissDictionary`. Costs a one-byte control tag per slot; for small or write-dominated sets, `CeleritySet` is competitive. |
| Set with **clustered / adversarial** elements where worst-case `Contains` latency matters | `RobinHoodSet<T, THasher>` | Same API as `CeleritySet`, but Robin Hood probing bounds probe-length variance and lets negative lookups exit early via the probe-distance invariant so tail-latency lookups don't degrade on bunched elements. The set counterpart of `RobinHoodDictionary`. Costs a per-slot probe-distance `int`; for uniform elements with a good hasher, prefer `CeleritySet`. |
| **Lookup-heavy** set with **costly element equality** (long strings, large structs) or cache-cold `Contains` | `HashCachingSet<T, THasher>` | Same API as `CeleritySet`, but a dense side array of 32-bit hash fingerprints lets a probe scan metadata only and run the full equality check on a candidate element solely when its fingerprint matches — so negative lookups reject on a single integer compare. The set counterpart of `HashCachingDictionary`; complementary to the SIMD-probed `SwissSet`. Costs four bytes of metadata per slot; for small tables of cheap elements, `CeleritySet` is a wash. |
| **Short-lived** set rebuilt frequently on a hot path where GC pressure matters | `PooledCeleritySet<T, THasher>` | Same API as `CeleritySet` plus `IDisposable`; rents its backing array from `ArrayPool<T>.Shared` and returns it on `Dispose`, so build/use/dispose cycles recycle buffers instead of allocating. The set counterpart of `PooledCelerityDictionary`. Dispose it (a `using` scope); for long-lived sets the pooling buys nothing, so prefer `CeleritySet`. |
| Build-once, read-many membership set keyed by `string` | `FrozenCeleritySet` | Immutable; searches for a perfect (collision-free) hash at build time so `Contains` is single-probe. The set counterpart of `FrozenCelerityDictionary`; implements `IReadOnlySet<string>`. Tune the hasher via the `<THasher>` overload. |
| **Membership gate** where a small, bounded false-positive rate is acceptable in exchange for a large memory saving (dedup pre-filters, "have I seen this before?" guards in front of an expensive exact lookup) | `BloomFilter<T, THasher>` | Probabilistic: bit-array storage with **no false negatives** and a tunable false-positive rate, using a fraction of a `HashSet<T>`'s memory and never growing with element size. Add-and-test only — no `Remove`, no enumeration, no retrieval. If you need exact membership or to get the elements back, use `CeleritySet` / `FrozenCeleritySet`; if you need to **delete** from the filter, use `CuckooFilter`. |
| **Deletable membership gate** — the same approximate-membership trade-off as `BloomFilter` but for a set that **shrinks** as well as grows (sliding windows of recent keys, cache-admission filters, expiring-entry sets) | `CuckooFilter<T, THasher>` | Probabilistic: fingerprint-bucket storage with **no false negatives**, a tunable false-positive rate, and `Remove`. Lookups touch at most two buckets (≈ two cache lines). Only remove elements you actually added. Insertion can fail at very high load (reports *full*). If your set only grows or you reset it wholesale, `BloomFilter` is simpler and can be more compact at high target false-positive rates. |
| **Static membership gate** over a **fixed** element set known up front (precomputed allow/deny lists, a read-only "have I seen this?" gate in front of an expensive exact lookup) where the smallest, fastest filter matters | `XorFilter<T, THasher>` | Probabilistic, **build-once & immutable**: the whole set goes to the constructor (no `Add`/`Remove`). Packs to ~9.84 bits/element (smaller than a Bloom filter at the same ~0.4% rate) and every `Contains` is three probes + two XORs, branch-free — the fastest, most compact filter. If the set changes over the filter's lifetime, use `BloomFilter` (grows) or `CuckooFilter` (grows and shrinks) instead; the false-positive rate is fixed at ~0.4% (8-bit fingerprint), so use `BloomFilter` when you need a tunable rate. |
| **Dense set of small integer indices** (or a fixed universe of flags) where you count set bits or combine whole vectors — bitmaps, visited/presence masks, sieves | `BitSet` | Exact dense bit vector packed into 64-bit words: `O(n/64)` population count (`Count`) via hardware popcount and SIMD bulk `And`/`Or`/`Xor`/`Not`. A faster, count-aware `System.Collections.BitArray`. For **sparse** indices over a huge/unbounded domain, `IntSet` / `LongSet` is more memory-efficient; for approximate membership over arbitrary elements, use `BloomFilter`. |
| **Distinct count** over a large or unbounded stream (unique visitors / events, distinct-value cardinality, deduplicated counts across shards) where a small relative error is acceptable | `HyperLogLog<T, THasher>` | Probabilistic: estimates the distinct count from a fixed array of registers (16&#160;KB at the default precision) with a ~0.8% relative standard error, never growing with the cardinality — unlike a `HashSet<T>` that stores every distinct value. Add-and-estimate only; merge shard estimators with `UnionWith`. If you need an exact count or to test a specific element, use `HashSet<T>` / `CeleritySet`; for approximate *membership* rather than counting, use `BloomFilter`. |
| **Per-element frequency** of a *specific* element over a large or unbounded stream (approximate per-key counts, rate limiting, deduplicated frequency counts across shards) where a small one-sided overestimate is acceptable | `CountMinSketch<T, THasher>` | Probabilistic: estimates each element's frequency from a fixed grid of counters (sized from `epsilon` / `delta`) that never grows with the distinct-key count — unlike a `Dictionary<TKey, int>` frequency table. **Never underestimates**; overestimates bounded by `epsilon · TotalCount` with confidence `1 − delta`. Add-and-estimate only; merge shard sketches with `UnionWith`. If you need exact counts or to enumerate keys, use a `Dictionary<TKey, int>`; if you want the *set of* heaviest elements rather than a specific one's count, use `TopKSketch`; for the distinct *count* use `HyperLogLog`, for approximate *membership* use `BloomFilter`. |
| **Top-k / heavy hitters** — the *most frequent* elements of a large or unbounded, high-cardinality stream (top URLs / IPs, trending items, network flow monitoring, hot keys) where only the heaviest matter | `TopKSketch<T, THasher>` | Probabilistic (Space-Saving): keeps a fixed `k` monitors, so memory is `O(k)` regardless of the distinct-key count — unlike a `Dictionary<TKey, int>` that must materialize every distinct key just to rank the top few. **Never underestimates** a monitored count and never misses an element above `TotalCount / k`; each result carries a bounded `Error`. Add-and-query only (no `Remove`, no `UnionWith`). If you need the exact fully-ranked counts, use a dictionary frequency table; for a *specific* element's frequency use `CountMinSketch`. |
| **Bounded cache** with automatic eviction — memoize the last `N` results, an admission cache in front of an expensive lookup, any hot key→value store that must not grow without bound | `LruCache<TKey, TValue, THasher>` | Fixed-capacity least-recently-used cache: `O(1)` get/put, and once at capacity every insert evicts the least-recently-used entry. Its recency list is threaded through fixed-size arrays, so after construction the hot get/put/evict path **allocates nothing** — where the idiomatic `Dictionary` + `LinkedList` LRU allocates a `LinkedListNode` per insert. Reads are *uses* (they promote to most-recently-used); use `TryPeek` / `ContainsKey` to inspect without touching recency. Single-threaded — because reads mutate recency, even a read-mostly concurrent workload needs a write lock. |
| **Double-ended queue** — add/remove at both ends (bounded FIFO queue, sliding window, work-stealing / undo buffer) or a queue needing random access by position | `Deque<T>` | Growable double-ended queue backed by a **circular buffer**: `O(1)` amortized `PushFront` / `PushBack` / `PopFront` / `PopBack` / peek and `O(1)` random access by index. The BCL has no deque — `Queue<T>` is FIFO-only, `Stack<T>` LIFO-only, and `LinkedList<T>` (the only O(1)-both-ends type) allocates a node per element. A warm bounded churn reuses the buffer with wrap-around so it **allocates nothing**, and enumeration walks contiguous memory. For a strict FIFO queue that never pushes front / pops back, BCL `Queue<T>` is already a circular buffer and is simpler. |
| **Incremental connectivity / connected components** — union equivalence classes and ask whether two elements are in the same group (Kruskal MST, clustering, image segmentation, undirected cycle detection, "are these accounts linked?") | `DisjointSet<T>` | Union-find with **union by size** + **path halving**: near-`O(1)` amortized `Union` / `Find` / `Connected`, `O(α(n)) ≤ 4`. Runs a stream of merges + connectivity queries in near-linear total time, where the BCL substitutes are super-linear — a `Dictionary<T, HashSet<T>>` set-merge is `O(n²)` to coalesce `n` singletons, and a per-query BFS/DFS is `O(V+E)` every query. Grows only by merging (no un-union); it is not an `ISet<T>` — for element membership with add/remove/set-algebra use `CeleritySet` or `HashSet<T>`. |
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

The **`Celerity.Primitives`** package exposes low-level helpers that fill genuine BCL gaps. `FastUtils.FastMod` / `FastDiv` are Lemire's reciprocal modulo and division: when a **divisor is fixed at run time** and reused across a hot loop (hash buckets, ring buffers, sharding, rate limiting), precompute a reciprocal once and each `value % divisor` / `value / divisor` becomes a multiply-and-shift — **2–4× faster** than the long-latency hardware `DIV` (the same trick the BCL uses internally but keeps `private`). 32- and 64-bit overloads; both reproduce the built-in operators bit-for-bit.

```csharp
using Celerity.Primitives;

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

Finally, **`FastGuid`** generates GUIDs from a struct PRNG instead of the OS cryptographic RNG: a **non-cryptographic version 4** (random) and an RFC 9562 **version 7** (Unix-millisecond time-ordered). The version 7 layout is **big-endian**, so — unlike .NET 9's `Guid.CreateVersion7`, whose mixed-endian storage scrambles the sort order — the canonical string sorts in creation order, keeping database indexes compact; `GuidV7Generator<TRng>` adds a monotonic counter so a same-millisecond burst is still strictly increasing. Both run several times faster than RNG-backed `Guid.NewGuid()`. **Not for unguessable IDs** (security tokens etc.) — use `Guid.NewGuid()` there.

```csharp
var rng = new Xoshiro256StarStar(seed: 12345);
Guid traceId = FastGuid.CreateVersion4(ref rng);                                  // fast random id
Guid dbKey   = FastGuid.CreateVersion7(ref rng, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); // sortable
```

`FastUtils` also exposes **alignment helpers** — `AlignUp` / `AlignDown` / `IsAligned` for `int` / `long` sizes and pointer-sized `nuint` addresses — that round to a power-of-two boundary (the `internal` BCL `Align` trick, exposed): sub-allocating from a buffer, padding a stride to a SIMD width, or finding the start of the cache line a pointer sits in.

```csharp
int padded     = FastUtils.AlignUp(length, 16);       // round a byte count up to 16
nuint lineStart = FastUtils.AlignDown(address, 64);   // start of the containing cache line
```

And **`SpanBits`** is the **non-owning** counterpart to `BitSet`: bit `Get` / `Set` / `Clear` / `Flip`, hardware-`POPCNT` `PopCount`, and a `TZCNT` `NextSetBit` scan over a caller-owned `Span<ulong>` — a `stackalloc` buffer, a slice, or a pooled array — with **no heap object**. (`System.Collections.BitArray` is a heap class with no span access, no popcount, and no scan.) Use `BitSet` when you want an owning bit vector; use `SpanBits` when you already manage the storage.

```csharp
Span<ulong> bits = stackalloc ulong[SpanBits.WordCount(200)];   // 200-bit scratch bitmap, no allocation
SpanBits.Set(bits, 5);
for (int i = SpanBits.NextSetBit(bits, 0); i >= 0; i = SpanBits.NextSetBit(bits, i + 1)) { /* ... */ }
```

The **`BitWriter`** / **`BitReader`** ref-struct cursors are the **sequential, sub-byte** counterpart: they pack and unpack arbitrary-width bit **fields** (a 3-bit flag group, a 12-bit sample, a 20-bit offset) end-to-end over a caller-owned `Span<byte>`, LSB-first, with **no stream and no allocation** — so a record of odd-width fields occupies exactly `ceil(total_bits / 8)` bytes. Where `VarInt` is byte-granular and `SpanBits` is random-access, these append and consume whole multi-bit fields at a moving cursor. The BCL has no equivalent (`System.Collections.BitArray` sets one bit at a time and can't append a multi-bit field). Every `TryWrite` / `TryRead` is bounds-safe (returns `false` and leaves the cursor unchanged rather than writing a partial field), and only the low `bitCount` bits of a value are stored, so an out-of-range value never corrupts a following field.

```csharp
Span<byte> buffer = stackalloc byte[BitWriter.ByteCount(3 + 12 + 20)]; // 5 bytes
var writer = new BitWriter(buffer);
writer.TryWriteBits(5, 3); writer.TryWriteBits(3000, 12); writer.TryWriteBits(0xABCDE, 20);

var reader = new BitReader(buffer);
reader.TryReadBits(3, out ulong flags);   // 5
reader.TryReadBits(12, out ulong sample); // 3000
```

Then, **`SimdReductions`** ships the two span reductions that `System.Numerics.Tensors.TensorPrimitives` (which you should use for plain `Sum` / `Min` / `Max`) **doesn't** cover: a **fused single-pass `MinMax`** that computes both extrema in one pass instead of the two passes `TensorPrimitives.Min` + `TensorPrimitives.Max` cost (**~1.8× faster on large, out-of-cache `int` arrays** — a memory-bandwidth win; a wash for small in-cache spans), and an overflow-checked **`CheckedSum`** that widens `int` lanes to `long` so the SIMD accumulation can't overflow and throws `OverflowException` rather than wrapping like `TensorPrimitives.Sum` (**~4.6× faster than the only safe alternative, a scalar `checked` loop**).

```csharp
var (lo, hi) = SimdReductions.MinMax(samples);    // both extrema, one pass
int total    = SimdReductions.CheckedSum(samples); // throws on overflow instead of wrapping
```

Finally, **`Branchless`** is a guaranteed branch-free conditional select. The JIT already emits `cmov` for `Math.Min` / `Max` / `Abs` / `Clamp`, but it does **not** reliably if-convert a general data-dependent `condition ? a : b` — in a loop over an **unpredictable** `bool` it emits a real branch, and the misprediction penalty dominates. `Branchless.Select` picks a value with pure mask arithmetic (`whenFalse ^ ((whenTrue ^ whenFalse) & mask)`), so there is no jump to mispredict: the #198 spike measured a per-element blend over a 1,000,000-element array with a 50/50 unpredictable condition at **~6× faster** than the branchy ternary. Scalar overloads cover `int` / `long` / `uint` / `ulong` / `float` / `double` (floats bit-exact, signed zero and `NaN` preserved); bulk span overloads blend two arrays branch-free (and auto-vectorise). Reach for it only when the condition is genuinely unpredictable — a well-predicted branch is already free.

```csharp
int clamped = Branchless.Select(value > limit, limit, value); // no branch to mispredict
Branchless.Select(mask, a, b, destination);                   // destination[i] = mask[i] ? a[i] : b[i]
```

See [`docs/api/utilities.md`](docs/api/utilities.md#fastmod--fastdiv) for the full surface and the generator-selection table.

## Native AOT & trimming

Celerity is **Native AOT and trimming compatible** — no reflection, runtime code generation, or dynamic type loading. Every collection is a generic over a struct hasher, and the only BCL primitives on the hot paths (`MemoryMarshal`, `Unsafe`, `EqualityComparer<T>.Default`) are AOT-safe. The assembly is marked [`<IsAotCompatible>true</IsAotCompatible>`](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#aot-compatibility-analyzers), so a `PublishAot` app gets **no trim or AOT warnings**. Compatibility is enforced on every build (the trim/AOT analyzers run during compilation) and CI publishes a Native AOT smoke-test binary exercising every collection and hasher. See [`docs/aot.md`](docs/aot.md).

## API at a glance

The dictionaries mirror the parts of `Dictionary<TKey, TValue>` most callers reach for: indexer get/set, `ContainsKey`, `TryGetValue`, `Add`, `TryAdd`, `Remove` (both overloads), `Clear`, `EnsureCapacity` / `TrimExcess`, `Count`, `Keys`, `Values`, `GetEnumerator()`. They implement `IReadOnlyDictionary<TKey, TValue?>` and accept an `IEnumerable<KeyValuePair<TKey, TValue>>` at construction. The sets expose `Add`, `TryAdd`, `Contains`, `Remove`, `Clear`, `EnsureCapacity` / `TrimExcess`, `Count`, and a struct enumerator. `EnsureCapacity(n)` pre-grows the table once for a known-size bulk insert (no incremental rehashes); `TrimExcess()` rehashes back down to fit `Count`. The zero / `default(TKey)` key (or element) is stored out-of-band so it never collides with the empty-slot sentinel.

Full constructors, signatures, exceptions, and per-type examples: **[API reference](docs/README.md)**.

## Project docs

- [`docs/`](docs/README.md) — documentation index & [API reference](docs/README.md#api-reference).
- [Performance tuning](docs/performance.md) · [Migration guide](docs/migration.md) · [Troubleshooting](docs/troubleshooting.md) · [FAQ](docs/faq.md) · [Testing & coverage](docs/testing.md).
- [`ROADMAP.md`](ROADMAP.md) · [`CHANGELOG.md`](CHANGELOG.md) · [`CONTRIBUTING.md`](CONTRIBUTING.md) · [GitHub Issues](https://github.com/marius-bughiu/Celerity/issues).
