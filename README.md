# Celerity
[![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/v/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/vpre/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![Live benchmarks](https://img.shields.io/badge/benchmarks-live-0d6e6e?style=flat-square)](https://marius-bughiu.github.io/Celerity/dev/bench/) [![Coverage](https://marius-bughiu.github.io/Celerity/coverage/badge.svg)](https://marius-bughiu.github.io/Celerity/coverage/)

Celerity is a .NET library of specialized, high-performance collections — drop-in alternatives to the BCL that trade flexibility for speed or memory on specific workloads. Hashers are structs supplied as generic constraints (so the JIT inlines them), load factors are configurable, and you can plug in your own hash functions.

```bash
dotnet add package Celerity.Collections
```

> **New here?** Jump to [**Choosing a collection**](#choosing-a-collection) — the table maps your workload to the right type in one line.

### Packages

Celerity's core ships as layered NuGet packages. **`Celerity.Collections` pulls in the hashing and primitives packages transitively, so a single `dotnet add package Celerity.Collections` still gives you everything on that line** — add the lower packages directly only if you want the hashers or primitives *without* the collections. `Celerity.Sorting` and `Celerity.Statistics` are siblings rather than dependencies of the collections: add them when you want the sorts or the streaming summaries.

| Package | What it adds | Depends on |
|---|---|---|
| [`Celerity.Collections`](https://www.nuget.org/packages/Celerity.Collections/) | dictionaries, sets, frozen/perfect-hash collections, streaming sketches | `Celerity.Hashing`, `Celerity.Primitives` |
| `Celerity.Hashing` | `IHashProvider<T>` / `IHashProvider64<T>`, the struct hashers, `HashQualityEvaluator` | `Celerity.Primitives` |
| `Celerity.Primitives` | `FastUtils`, struct PRNGs, `VarInt`, `FastGuid`, `SortedSpan`, `MortonCurve` / `HilbertCurve` | — |
| [`Celerity.Sorting`](https://www.nuget.org/packages/Celerity.Sorting/) | `RadixSort`, `CountingSort`, `PartialSort` — non-comparison sorts and selection over primitive keys | `Celerity.Primitives` |
| [`Celerity.Statistics`](https://www.nuget.org/packages/Celerity.Statistics/) | `DDSketch`, `ReservoirSampler`, `RunningStatistics` — streaming quantiles, sampling and moments in bounded memory | `Celerity.Primitives` |

> **Upgrading from 1.x?** Namespaces are unchanged except `FastUtils`, which moved from `Celerity` to `Celerity.Primitives`. See the [migration guide](docs/migration.md#200--the-package-split).

All five packages **multi-target `net8.0`, `net9.0`, and `net10.0`**, so NuGet hands your project the assembly built against its own runtime. `net8.0` (LTS) is the floor — Celerity runs anywhere from .NET 8 upward.

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
- `CompressedIntSet` — **exact, compressed** set of 32-bit integers: each 65,536-value chunk is stored as a sorted array or a bitmap by density, with an opt-in run-length form for clustered data that `Optimize()` and `AddRange` produce. Set algebra runs word-parallel inside a chunk and skips a whole chunk with one comparison, memory is ~10x below `HashSet<int>` (far more when dense or clustered), and enumeration is **in ascending order**. For huge-and-sparse integer sets — posting lists, row-id sets, cohort intersection. No portable Roaring format: this is an in-process structure, not an interop codec.

The mutable sets (`CeleritySet`, `SwissSet`, `RobinHoodSet`, `HashCachingSet`, `PooledCeleritySet`, `IntSet`, `LongSet`, `SmallSet`, `EnumSet`, `SparseSet`, `CompressedIntSet`) all implement **`ISet<T>`** and **`IReadOnlySet<T>`** — the full `HashSet<T>` set-algebra surface (`UnionWith` / `IntersectWith` / `ExceptWith` / `SymmetricExceptWith` and the `IsSubsetOf` / `IsSupersetOf` / `Overlaps` / `SetEquals` query family, plus `CopyTo`) with BCL semantics — so they drop in wherever a `HashSet<T>` is used. (The bounded-domain sets, `EnumSet` and `SparseSet`, are the exception to "drop in anywhere": they store only values in their fixed domain, so a mutating op that must add an out-of-domain value throws.)

**Caches**

- `LruCache<TKey, TValue, THasher>` — fixed-capacity **least-recently-used cache** with `O(1)` get/put and automatic eviction of the least-recently-used entry. Recency runs through an intrusive doubly-linked list over fixed-size arrays, so the hot get/put/evict path **allocates nothing** — unlike the idiomatic `Dictionary` + `LinkedList` LRU that heap-allocates a node per insert. Reads count as uses (a hit promotes the entry to most-recently-used); `TryPeek` / `ContainsKey` inspect without disturbing recency.

**Sequences**

- `Deque<T>` — growable **double-ended queue** backed by a **circular buffer**: `O(1)` amortized push / pop / peek at **both** ends, plus `O(1)` random access by index. The array-backed deque the BCL lacks (`Queue<T>` is FIFO-only, `Stack<T>` LIFO-only, and `LinkedList<T>` allocates a node per element) — a bounded FIFO / sliding-window churn reuses the buffer with wrap-around and **allocates nothing**, and enumeration walks contiguous memory. Implements `IReadOnlyList<T>`.

**Union-find**

- `DisjointSet<T>` — **union-find** over arbitrary elements: partitions them into disjoint sets with near-`O(1)` amortized `Union` / `Find` / `Connected` via **union by size** + **path halving**. The union-find the BCL lacks — incremental connectivity, connected components, Kruskal MST, and undirected cycle detection in near-linear total time, where the `Dictionary` + `HashSet` set-merge substitute is quadratic. `GetComponents()` materializes the current partition.

**Priority queue**

- `IndexedPriorityQueue<TElement, TPriority, THasher>` — **addressable** binary min-heap: unlike the BCL `PriorityQueue<,>` it can **change a queued element's priority** (`Update` / decrease-key) and **remove an arbitrary element** in `O(log n)`, and answer `Contains` / `TryGetPriority` in `O(1)`. The heap the priority-relaxation loop of Dijkstra / Prim / A\* needs — no lazy-deletion heap growth. Each element is a key (appears once); pass a custom `IComparer<TPriority>` for a max-heap.

**Prefix trees**

- `Trie<TValue>` — ordered **prefix tree** mapping string keys to values. `GetByPrefix` lists every entry whose key starts with a prefix in `O(prefix + matches)`, and `TryGetLongestPrefix` finds the longest stored key that is a prefix of a query in `O(query)`. The trie the BCL lacks — autocomplete, longest-prefix routing, and ordered (ascending-ordinal) iteration, where a `Dictionary<string, TValue>` has no prefix index and must scan every key and run `StartsWith`. Exact `Add` / `TryGetValue` favour a `Dictionary` (one hash vs a character walk); the trie earns its place on the prefix operations. Implements `IReadOnlyDictionary<string, TValue?>`.

**Span-keyed string lookups**

- `StringInternTable` / `StringInternTable<THasher>` — a **canonicalizing token table** probed with a `ReadOnlySpan<char>`: `GetOrAdd` returns the one shared `string` for those characters and allocates **only on a miss**. A 10M-cell parse over 100 distinct tokens creates 100 strings instead of 10,000,000. The collection you cannot build on the pre-.NET-9 BCL — `HashSet<string>.TryGetValue` makes you allocate the string *before* you can discover you already had it, and `string.Intern` is process-wide, never collected, and still needs a `string`. Implements `IReadOnlyCollection<string>`.
- The same span-keyed probes ship on `FrozenCelerityDictionary`, `FrozenCeleritySet`, `CelerityDictionary<string, …>`, `CeleritySet<string, …>`, and `Trie<TValue>` — so a tokenizer, CSV/log reader, or route dispatcher holding a slice of its input buffer never has to call `new string(span)` per lookup. Works on all three target frameworks, including the `net8.0` floor where the BCL has no equivalent (.NET 9 added `Dictionary<string,V>.GetAlternateLookup`). See [span-keyed lookups](docs/api/collections.md#span-keyed-lookups).

**Sorted (ordered) collections**

- `BTreeDictionary<TKey, TValue, TComparer>` / `BTreeDictionary<TKey, TValue>` — a **sorted map backed by a B-tree**: up to **31 keys per node** in flat arrays, so a lookup visits `log₃₂(n)` nodes instead of chasing `log₂(n)` pointers — roughly **4 cache misses instead of ~20 at `n = 1M`**. Adds the ordered surface a hash table cannot answer: `Min`, `Max`, `TryGetLowerBound` / `TryGetUpperBound`, `EnumerateRange` in `O(log n + k)`, and in-order enumeration. The B-tree the BCL lacks — `SortedDictionary<,>` is a red-black tree with one heap object per entry, and `SortedList<,>` memmoves the tail on every insert. Implements `IDictionary<TKey, TValue?>` and `IReadOnlyDictionary<TKey, TValue?>`.
- `BTreeSet<T, TComparer>` / `BTreeSet<T>` — the set counterpart, with the same ordered surface and no values to store, so the memory saving over `SortedSet<T>` is larger still. Implements `ISet<T>` and `IReadOnlySet<T>`.
- `RankedSet<T, TComparer>` / `RankedSet<T>` — an **order-statistics set**: the same ordered surface, plus the two positional questions no BCL ordered container can answer — `IndexOf(item)` (the element's **rank**) and `set[k]` (the **k-th smallest**), both `O(log n)`, on a set that is still being inserted into and removed from — with `Add` / `Remove` at `O(√n)`, a bounded memmove rather than a whole-array one, which is the sqrt-decomposition trade — plus `CountLessThan` / `CountLessThanOrEqual` for the rank an *absent* element would take, and `RemoveAt(rank)`, which the BCL has no equivalent for at all. `SortedSet<T>` has neither rank nor positional access (`ElementAt(k)` is `O(k)`); `SortedList<,>` and a hand-rolled sorted `List<T>` have both but memmove half the array on every insert. Elements sit in sorted buckets whose capacity tracks `√n` (never below 512), with a Fenwick tree over the bucket lengths carrying the positional half. Implements `ISet<T>`, `IReadOnlySet<T>` **and `IReadOnlyList<T>`** — the only set here that is also a list, and it can be for exactly that reason.

All three take their ordering as a **struct** `IComparer<T>` type parameter (`DefaultComparer<T>` by default), exactly as the hashers are struct type parameters, so the comparison inlines instead of costing a virtual call per key inspected.

**Range aggregates**

- `FenwickTree<T>` — a **Binary Indexed Tree** over a fixed-length numeric sequence (`where T : struct, INumber<T>`): **point update** and **prefix / range sum** both in `O(log n)`, in one flat array with no per-node overhead. The prefix-sum structure the BCL lacks — running aggregates, rank / order-statistics counters, cumulative-frequency tables — where a plain array is `O(n)` per query (recompute the slice) *or* `O(n)` per update (fix the suffix). Wins precisely when updates and partial-sum queries interleave.
- `SegmentTree<T, TMonoid>` — range aggregates over an arbitrary **associative** fold: **point update** and **range query** both in `O(log n)`, in one flat array of `2n` cells. The half of the range-query space a Fenwick tree cannot reach — its query is the *difference* of two prefix folds, so it needs an inverse, while a segment tree stores each node's fold outright. That puts range **min**, **max**, **gcd**, bitwise **and**/**or** and any monoid you write in reach. The BCL has no range-aggregate structure at all, so the baseline is a plain array scanned per query. `SumMonoid` / `MinMonoid` / `MaxMonoid` / `BitwiseAndMonoid` / `BitwiseOrMonoid` ship built in, as struct type parameters so the fold inlines; non-commutative folds are safe, since the query preserves index order.
- `KdTree<TValue>` — a build-once **2-D spatial index**: *which point is nearest to this one*, *which lie within this radius*, *which lie inside this box*, without measuring every point. .NET ships **no spatial index of any kind** — no k-d tree, no quadtree, no R-tree — so the alternative is an array and a loop over all of it. The nearest, predicate, count and copy tiers allocate nothing (the k-nearest search heaps inside your own buffer), and the whole structure is one interleaved coordinate array plus the payloads, with no per-point node. Nearest store or driver, viewport culling, collision broadphase, k-means / DBSCAN neighbour queries. The ratio tracks **selectivity**, and a hand-rolled sorted scan is a much closer baseline than the naive one — see [Spatial index](#spatial-index).
- `SpatialGrid<TValue>` — the **mutable** counterpart: constant-time **`Move`** and `Remove` (and amortized-constant `Add`) addressed by a handle, plus radius, rectangle and nearest queries that touch only the cells they cover. `KdTree` is build-once and its own docs say rebuilding it per frame costs more than the queries save, which leaves the commonest spatial workload unanswered — entities, couriers, cursors, particles and agents all move every tick and all ask *what is near me* every tick. The baseline is not a strawman (a `Dictionary<(int, int), List<T>>` bucketed grid is what a competent developer writes), and a populated grid here is three flat arrays — cell heads, entry records, payloads — with no per-cell or per-entry object. **5.0x** that hand-roll on a frame of 100,000 entities — but everything gained is *per cell*, not per point, so the margin thins as cells fill and **reverses on clustered data**; see [Spatial index](#spatial-index).
- `RTree<TValue>` — a build-once **2-D extent index**: *which of these boxes overlap this box*, *which contain this point*, without testing every box. `KdTree` cannot stand in — a box can overlap the query while its centre sits far outside it — and .NET ships nothing for extents either: `System.Drawing` has a `Rectangle` with an `IntersectsWith` and no index over a *collection* of them. The boxes are permuted by **STR (Sort-Tile-Recursive) packing** with a fixed-fanout tree laid over them implicitly, so there are no per-node objects and no child pointers; the predicate, count and copy tiers allocate nothing. Collision broadphase for bodies with a size, map label placement, canvas hit-testing, viewport culling, spatial joins. The standard advice that it only earns its keep when extents vary by orders of magnitude, and that uniform ones belong to a bucketed grid, is measured against a grid arm rather than repeated — and does not survive: the margin is *widest* on uniform extents, and mutability rather than shape is the reason to prefer a grid. See [Extent index](#extent-index).
- `IntervalTree<TKey, TValue>` — a **build-once index over half-open `[start, end)` ranges** answering *which ranges cover this point* and *which overlap this window* in time that tracks the matches found rather than the intervals stored (`O(log n + k)` when they cluster, `O(min(n, (k + 1) log n))` when scattered, `O(n)` worst case once zero-length intervals are stored). The BCL has no interval structure at all, so the alternative is a `List<T>` scanned per query; sorting it by start lets the scan stop at the upper bound but not skip the front, since a range beginning far to the left can still cover the point, so it stays linear (both baselines are benchmarked). Booking- and scheduling-conflict checks, IP-range lookup, effective-dated pricing, "which trace spans were live at *t*". The whole structure is four flat arrays with no per-interval node, every query shape shares one traversal specialized per `struct` visitor, and the predicate / count / copy tiers allocate nothing.
- `CompressedGraph` — a build-once **graph**, stored compressed sparse row (CSR): two flat `int[]`, so a vertex's neighbours are a `ReadOnlySpan<int>` slice of one contiguous array rather than a heap object of their own. .NET ships no graph, no adjacency list and no traversal of any kind, so the idiom is `Dictionary<int, List<int>>` — or `List<int>[]` once you notice the ids are dense — and both are benchmarked. Dependency and build ordering, package and module resolution, link and follower graphs, reachability and impact analysis. Neighbour iteration, breadth-first traversal, `Reverse()` (the transpose, `O(V + E)`, which the adjacency map has to rebuild list by list) and a topological order, with the destination buffer doubling as the traversal queue so a repeated walk allocates nothing once the `ArrayPool` it borrows its visited set from is warm. **Not a speed win over a hand-roll you write carefully**, and the benchmark says so: against an `int[][]` sized exactly and filled in vertex order, the traversal and the build are both within about 1.2x at 100k vertices — a wash — and both *lose* at 1k. What survives is the **transpose (2.5–3.0x)**, a **1.8x smaller footprint**, and not writing or maintaining the traversal, Kahn's algorithm, the transpose and the sorted-target invariant. Against the idiomatic `Dictionary<int, List<int>>` the same arms are 2.5–2.6x, 3.1–3.8x and 3.6x less memory.
- `SuffixArray` — a build-once **text index**: every suffix of a block of text in sorted order, plus the longest-common-prefix array beside it, so *where does this substring occur* costs `O(m log n)` in the **pattern** length rather than `O(n)` in the text. .NET ships no text index at all — `string.IndexOf`, `MemoryExtensions.IndexOf` and `Regex` are scans that re-read the text on every query, and .NET 9's `SearchValues<string>` indexes the *needles*, not the haystack. `Contains` / `CountOccurrences` / `IndexOf`, a zero-copy `TryGetOccurrences` that returns a slice of the index itself, and `TryGetLongestRepeatedSubstring`, which no scan-shaped API can express. At 100,000 characters counting a pattern measures **68x** the scan and ruling an absent one out **64x**; at 1,000 characters both fall to about **1.3x**. **Build-once: one query against a text read once is a loss** — the build is repaid at roughly 1,000 counting queries, and a `Dictionary<string, int[]>` k-gram index beats this by 10.3x per query at the one pattern length it can answer.
- `AhoCorasick` — a build-once **multi-pattern matcher**: a fixed set of patterns compiled into one automaton that finds *every* occurrence of *every* pattern in **one** left-to-right pass, at a cost that does not grow with how many patterns there are. Neither neighbour reaches this — `Trie` matches a *prefix of the query* and cannot resume inside a text, and `SuffixArray` indexes one fixed *text* and answers one pattern at a time — and the BCL has nothing either: `string.IndexOf` is a single-needle scan, so `k` patterns cost `k` passes, and .NET 9's `SearchValues<string>` answers only *where is the first of these*. `ContainsAny` / `CountMatches` / `TryFindFirst`, an allocation-free `EnumerateMatches` that drives the scan as it is pulled, and `CopyMatches` into a caller-owned buffer. Overlapping matches are **reported, not resolved** — `"he"`, `"she"` and `"hers"` over `"ushers"` is three matches. Against a compiled `Regex` alternation of 256 patterns over 100,000 characters: **10.0x** on membership and **4.8x** on enumerating every match, both allocation-free. **The pattern count and the shape both decide** — against the k-`IndexOf` loop it is 4.1x on counting present patterns, but that loop *wins* the 256-pattern absent-membership arm (0.98x) and wins the eight-pattern arm by 5.0x, because `MemoryExtensions.IndexOf` sweeps many characters per step while this pass reads one.
- `TimerWheel<TValue>` — a **hierarchical timing wheel**: a container of pending **deadlines**, with `O(1)` `Cancel`, amortized-`O(1)` `Schedule` and an `Advance` bounded by the wheel's geometry — `O(levels x slots + fired + cascaded)` — rather than by the ticks it crosses. A data structure, not a scheduler — no thread, no clock, no callback; the caller owns time. The workload is defined by **cancellation** (the reply arrives, the lease is renewed), and that is where the BCL fails: `PriorityQueue` has no removal on `net8.0` and the one .NET 9 added is `O(n)`, so the standard workaround is lazy deletion — a heap that grows with the timers that will never fire and a hash probe per pop. Timers thread through one flat entry array as intrusive lists, so there is no object per timer. On a round of 100,000 timeouts — schedule, cancel nine in ten, run the clock out — CI measures **6.81x** that heap and **7.09x** `IndexedPriorityQueue`, which cancels for real, falling to 2.56x at a thousand. Taken apart, `Cancel` and `Drain` are where it wins and **`Schedule` is where it loses — roughly 2x slower than a heap insert at 100,000**, because appending to a contiguous array beats writing a scattered slot head and a back-link. Also traded: a finite **horizon** (2^32 ticks by default, about 49 days at a millisecond), **no ordering** within a fired batch, and about a fifth more allocated per round than either heap. This is a large-population type — at a thousand timers `Cancel` is 2.2x slower and the tick-by-tick drive 1.8x slower.
- `Rope` — a **rope**: a balanced tree of bounded character runs, so an edit anywhere in a large block of text costs `O(log n)` instead of the `O(n)` a contiguous buffer charges. The library's only *mutable* text type — `Trie`, `SuffixArray` and `AhoCorasick` all search text that does not change. `StringBuilder` is a chunk list whose head is the *end*, so appending is excellent and `Insert`, `Remove` and every other operation are linear in the **document**: ten times the text is ten times the cost of one edit. It also has two operations the BCL has at no cost at all — `Split` cuts a document in two and `AppendAndClear` joins two back, both `O(log n)`, where `Concat` / `Substring` / slice-and-copy are full copies. Leaves are filled to three quarters of `ChunkSize` on purpose, so the ordinary short edit lands in a leaf that has room and **allocates nothing**; filling them to capacity instead measured a 2.9x loss. On a round of 200 scattered insert/remove pairs it measures **98x** `StringBuilder` at a million characters and **1.49x** at ten thousand, and a hundred split-and-rejoin cycles are **713x** at a million (186 KB against 400 MB). **Three operations are outright losses**: appending is **36.8x slower** — use a `StringBuilder` if that is all you do — random access **7.1x**, since a builder built from a string is one chunk and indexes directly, and `ToString()` 1.68x. About 2.7 bytes per character.

**Probabilistic & bit-level**

- `BloomFilter<T, THasher>` — **probabilistic** membership: bit-array storage, **no false negatives**, tunable false-positive rate, a fraction of a `HashSet<T>`'s memory. Add-and-test only.
- `CuckooFilter<T, THasher>` — **probabilistic** membership that **supports deletion**: fingerprint buckets, **no false negatives**, tunable false-positive rate, ≤2 cache lines per lookup. The Bloom filter you can `Remove` from.
- `XorFilter<T, THasher>` — **probabilistic** membership that is **build-once & immutable**: ~9.84 bits/element (smaller than a Bloom filter at the same rate), three probes + two XORs per lookup (branch-free). The smallest, fastest-to-query filter for a fixed element set.
- `BitSet` — dense, **exact** bit vector in 64-bit words: `O(n/64)` hardware popcount (`Count`) and SIMD bulk `And`/`Or`/`Xor`/`Not`. A faster, count-aware `BitArray`.
- `RankSelectBitVector` — **immutable** succinct index over a dense bit vector: `Rank(i)` (set bits below `i`) in `O(1)` and `Select(k)` (position of the `k`-th set bit) in `O(log n)`, for 25% space over the bits. The BCL has no rank or select anywhere, so the baseline is a hand-rolled `O(i/64)` popcount loop. **Build-once** — any mutation means rebuilding the index, so keep mutating vectors in `BitSet` and snapshot once they settle.
- `HyperLogLog<T, THasher>` — **probabilistic** cardinality estimator: counts *distinct* elements from a fixed ~16&#160;KB of registers (~0.8% error), never growing with the data. Mergeable.
- `CountMinSketch<T, THasher>` — **probabilistic** frequency estimator: estimates per-element counts from a fixed grid, **never underestimating** (overestimate bounded by `epsilon · TotalCount`). Mergeable.
- `TopKSketch<T, THasher>` — **probabilistic** top-k / heavy-hitters sketch (Space-Saving): reports a stream's most frequent elements from a fixed `k` monitors in `O(k)` memory, **never underestimating** and never missing a hitter above `TotalCount / k`.

The mutable dictionaries implement **both** `IDictionary<TKey, TValue?>` and `IReadOnlyDictionary<TKey, TValue?>`, so they drop into an existing API taking either BCL interface; the immutable `FrozenCelerityDictionary` and the prefix-tree `Trie<TValue>` implement the read-only one only. All of them ship allocation-free struct enumerators, `Keys` / `Values` views, and an `IEnumerable<KeyValuePair<TKey, TValue>>` constructor. The hash-table collections store `default(TKey)` (zero / `null`) out-of-band so it never collides with the empty-slot sentinel; `SmallDictionary` stores it inline.

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
<summary><b>Sets</b> — IntSet, CeleritySet, SwissSet, RobinHoodSet, HashCachingSet, FrozenCeleritySet, SmallSet, EnumSet, SparseSet, CompressedIntSet</summary>

```csharp
var seen = new IntSet();
seen.Add(1);
Console.WriteLine(seen.Contains(1)); // true

var visited = new CeleritySet<Guid, GuidHasher>();
visited.TryAdd(Guid.NewGuid()); // true on first add, false on duplicate
```

The mutable sets implement `ISet<T>` and `IReadOnlySet<T>`, so the full `HashSet<T>` set algebra works (with matching semantics):

```csharp
var a = new IntSet(new[] { 1, 2, 3, 4 });
a.IntersectWith(new[] { 2, 4, 6 });        // a -> { 2, 4 }
a.UnionWith(new[] { 4, 5 });               // a -> { 2, 4, 5 }
Console.WriteLine(a.IsSubsetOf(new[] { 2, 4, 5, 9 })); // true

ISet<int> asSet = a;                        // usable anywhere ISet<int>/ICollection<int> is expected
Console.WriteLine(asSet.Add(7));            // true — ISet<T>.Add is the non-throwing add

IReadOnlySet<int> asReadOnly = a;           // ISet<T> does not derive from IReadOnlySet<T>, so both are declared
Console.WriteLine(asReadOnly.Overlaps(new[] { 5, 11 })); // true
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

`SmallSet<T>` is the flat-array set — the set counterpart of `SmallDictionary`. It skips hashing and linear-scans a flat array, which at `n <= ~16` beats a hash table (no hash, no probe chain, great cache locality). No hasher to pick; a `0` / `null` / default element is stored inline. Lookups are `O(n)`, so move to `IntSet` / `CeleritySet` once instances grow. Implements `ISet<T>` and `IReadOnlySet<T>`, so the full set algebra works.

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

`SparseSet` is the bounded-universe integer set — the classic Briggs–Torczon sparse set (a dense value array + a sparse index array). Over a fixed universe `[0, Universe)` chosen at construction, `Add` / `Contains` / `Remove` are `O(1)` with no hashing, but the point of the type is what `HashSet<int>` can't match: `Clear()` is `O(1)` (it resets the count without scanning or clearing the backing arrays, versus zeroing the whole table) and iteration is a dense, contiguous scan over exactly the present elements. That is the winning shape for clear-and-rebuild "visited" sets — graph BFS/DFS, ECS entity membership, sweep-line — where the set is emptied every iteration. The cost is `O(Universe)` memory and non-negative-values-only: a value outside `[0, Universe)` throws on `Add` and reads as absent on `Contains` / `Remove`. It is an opt-in specialized type, not a `HashSet<int>` replacement — for an unbounded or huge-and-sparse key space, reach for `IntSet`.

```csharp
var visited = new SparseSet(nodeCount);   // universe = ids in [0, nodeCount)
visited.Add(start);
Console.WriteLine(visited.TryAdd(start)); // False — already seen, unchanged
visited.Clear();                          // O(1) — ready for the next traversal
```

`CompressedIntSet` is the **exact, compressed** integer set — the huge-and-sparse shape none of the above serves. It splits the 32-bit value space into 65,536-value chunks and stores each chunk as a sorted `ushort[]` or a 1024-word bitmap by density, with a third run-length form for clustered data that `Optimize()` and `AddRange` produce. Set algebra then works *inside* a chunk (a sorted merge, or one ANDed word per 64 values) instead of one hash probe per element, and a chunk neither side populates is skipped with a **single comparison** — so intersecting two posting lists costs the number of populated chunks, not the number of elements. Memory lands ~10x below `HashSet<int>` for sparse data and far lower for dense or clustered data, and enumeration is **in ascending signed order**, which `HashSet<int>` does not offer. Two honest caveats: point lookups are still faster in a hash table, and there is **no portable Roaring format** — Celerity ships no serializers, so this is an in-process structure, not Lucene / Druid / Spark interop. Compression is explicit: `Optimize()` is what produces the run form, after the data has settled.

```csharp
var termA = new CompressedIntSet(PostingsFor("celerity"));      // ~1M ids over ~100M docs
var termB = new CompressedIntSet(PostingsFor("collections"));
termA.Optimize();                                                // re-encode; run form appears here

long both = termA.IntersectCount(termB);                         // size of the overlap, nothing materialized
termA.IntersectWith(termB);                                      // chunk-wise; skips unshared chunks
foreach (int id in termA) Render(id);                            // ascending, allocation-free

var recent = new CompressedIntSet();
recent.AddRange(90_000_000, 99_999_999);                         // 10M ids as one run pair per chunk
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

`RankSelectBitVector` is the **build-once** succinct index over such a vector — see [the API reference](docs/api/collections.md#rankselectbitvector) for the full surface. `Rank(i)` returns how many bits are set below `i` in `O(1)`, and `Select(k)` returns the position of the `k`-th set bit in `O(log n)`. Nothing in .NET offers either, so the alternative is the `O(i/64)` popcount loop a caller writes by hand — at the midpoint of a 100-million-bit vector, ~780,000 iterations replaced by two index loads and one masked popcount. The index costs 25% over the bits and is **invalidated by any mutation**, so mutate a `BitSet` and snapshot it once the bits have settled.

```csharp
var present = new BitSet(rowCount);
foreach (int row in nonNullRows) present[row] = true;

var index = new RankSelectBitVector(present);   // freeze, then query
int denseSlot = index.Rank(row);                // logical row -> dense slot, O(1)
int logicalRow = index.Select(denseSlot);       // and back, O(log n)
```

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
<summary><b>Addressable priority queue</b> — IndexedPriorityQueue</summary>

`IndexedPriorityQueue<TElement, TPriority, THasher>` is an **addressable** binary min-heap: unlike the BCL `PriorityQueue<,>` it keeps an element→heap-slot index (a dogfooded `CelerityDictionary`) so it can **change a queued element's priority** and **remove an arbitrary element** in `O(log n)`, and look one up in `O(1)`. That is exactly what the priority-relaxation loop of Dijkstra / Prim / A\* needs — the BCL heap forces *lazy deletion* (re-enqueue + skip stale entries), which grows the heap by one entry per update. Each element is a key (it appears once); pass a custom `IComparer<TPriority>` for a max-heap.

```csharp
var pq = new IndexedPriorityQueue<string, int, DefaultHasher<string>>();
pq.Enqueue("a", 10);
pq.Enqueue("b", 30);
pq.Enqueue("c", 20);

pq.Update("b", 5);              // decrease-key: 'b' jumps to the front
Console.WriteLine(pq.Peek());  // b

Console.WriteLine(pq.Dequeue()); // b (priority 5)
Console.WriteLine(pq.Dequeue()); // a (priority 10)
Console.WriteLine(pq.Remove("c", out int p)); // True; p == 20
```

</details>

<details>
<summary><b>Prefix trees</b> — Trie</summary>

`Trie<TValue>` is the ordered **prefix tree** the BCL lacks: it maps string keys to values and answers the prefix queries a `Dictionary<string, TValue>` can't do without an `O(n)` scan. `GetByPrefix` lists every entry under a prefix in `O(prefix + matches)` and in ascending key order; `TryGetLongestPrefix` finds the most specific stored key that prefixes a query. Reach for it for autocomplete, longest-prefix routing, or ordered iteration — not for pure exact-key lookups, where a `Dictionary` (one hash vs a character walk) wins. See [the API reference](docs/api/collections.md#trietvalue).

```csharp
var routes = new Trie<string>();
routes["/"] = "home";
routes["/api"] = "api-root";
routes["/api/v1/users"] = "users-v1";
routes["/api/v1/orders"] = "orders-v1";

// Autocomplete: every entry under a prefix, already sorted.
foreach (var (path, handler) in routes.GetByPrefix("/api/v1/"))
    Console.WriteLine($"{path} -> {handler}"); // /api/v1/orders, then /api/v1/users

// Longest-prefix routing: the most specific stored route that prefixes the request.
if (routes.TryGetLongestPrefix("/api/v1/users/42", out string? route, out string? handler))
    Console.WriteLine($"{route} -> {handler}"); // /api/v1/users -> users-v1
```

</details>

<details>
<summary><b>Sorted maps and sets with range scans</b> — BTreeDictionary / BTreeSet</summary>

`BTreeDictionary<TKey, TValue>` and `BTreeSet<T>` keep their keys in order across nodes of up to 31 keys held in flat arrays, so a lookup visits `log₃₂(n)` nodes rather than chasing `log₂(n)` pointers the way `SortedDictionary<,>` / `SortedSet<>` (red-black trees, one heap object per entry) must. They add the ordered surface a hash table has no answer for — bounds and `O(log n + k)` range scans.

```csharp
var series = new BTreeDictionary<long, double>();
series[1_000] = 1.5;
series[1_010] = 2.5;
series[1_020] = 3.5;

Console.WriteLine(series.Min.Key);   // 1000
Console.WriteLine(series.Max.Key);   // 1020

// First key at or after 1005, and the first strictly after it.
series.TryGetLowerBound(1_005, out var atOrAfter);   // 1010
series.TryGetUpperBound(1_010, out var strictlyAfter); // 1020

// Seek in O(log n), then walk contiguous node arrays — no full scan, no allocation.
foreach (var sample in series.EnumerateRange(1_000, 1_020))
    Console.WriteLine(sample.Key); // 1000, 1010
```

</details>

<details>
<summary><b>Rank and select over a set that changes</b> — RankedSet</summary>

`RankedSet<T>` is the ordered set that also answers *what position would this element occupy* and *what is the k-th smallest*, in `O(log n)` each, while the set is being mutated. The BCL has no counterpart: `SortedSet<T>` exposes no rank and no indexer, so those answers are a linear walk; a sorted `List<T>` has both but pays an `O(n)` memmove per insert. Live leaderboards, exact percentiles over a moving window, the median of a sweep line's active set.

```csharp
var scores = new RankedSet<int>([120, 340, 90, 500, 275]);

Console.WriteLine(scores[0]);                    // 90  — the smallest
Console.WriteLine(scores[scores.Count / 2]);     // 275 — the exact median
Console.WriteLine(scores.IndexOf(340));          // 3   — its rank in sorted order
Console.WriteLine(scores.CountLessThan(300));    // 3   — the rank 300 *would* take

scores.TryAdd(310);
scores.RemoveAt(0);                              // drop the lowest, by rank
Console.WriteLine(scores.IndexOf(340));          // 3   — still O(log n), on the live set
```

</details>

<details>
<summary><b>Prefix sums with live updates</b> — FenwickTree</summary>

`FenwickTree<T>` (`where T : struct, INumber<T>`) is a **Binary Indexed Tree**: a fixed-length numeric sequence that answers **prefix / range sums** and applies **point updates** both in `O(log n)`, in one array with no per-node overhead. The BCL ships nothing for the interleaved update + prefix-sum-query workload — a plain array is `O(n)` per query or `O(n)` per update. It wins precisely when both interleave (running aggregates, rank counters, cumulative-frequency tables).

```csharp
var tree = new FenwickTree<long>(new long[] { 3, 1, 4, 1, 5, 9 });

Console.WriteLine(tree.PrefixSum(3));   // 8  (3 + 1 + 4)
Console.WriteLine(tree.RangeSum(2, 5)); // 10 (4 + 1 + 5)

tree.Add(0, 10);                        // point update, O(log n)
Console.WriteLine(tree[0]);             // 13
Console.WriteLine(tree.Total);          // 33
```

</details>

<details>
<summary><b>Range min / max / any associative fold with live updates</b> — SegmentTree</summary>

`SegmentTree<T, TMonoid>` answers the aggregate of any half-open range under an arbitrary **associative** fold, with **point updates** and **range queries** both in `O(log n)`. It is the half of the range-query space `FenwickTree<T>` cannot reach: a Fenwick range query is the *difference* of two prefix sums, so the operation must have an inverse — minimum has none. The fold arrives as a **struct** type parameter, exactly like the hashers, so `Combine` inlines instead of costing a virtual call per level.

```csharp
// A live order book: the cheapest ask in any price band, while prices keep moving.
var book = new SegmentTree<long, MinMonoid<long>>(new long[] { 105, 102, 108, 101, 110, 103 });

Console.WriteLine(book.Query(0, 4));   // 101 — cheapest in the first band
Console.WriteLine(book.Aggregate);     // 101 — cheapest overall

book[3] = 999;                         // that order was filled, O(log n)
Console.WriteLine(book.Query(0, 4));   // 102 — refolded

// Any monoid works. Write a struct with an Identity and an associative Combine:
public readonly struct GcdMonoid : IMonoid<uint>
{
    public uint Identity => 0;                  // gcd(0, a) == a
    public uint Combine(uint left, uint right)
    {
        while (right != 0) (left, right) = (right, left % right);
        return left;
    }
}
```

</details>

<details>
<summary><b>Nearest point, points within a radius, points inside a box</b> — KdTree</summary>

`KdTree<TValue>` indexes points in the plane once and then answers proximity questions without measuring every point. .NET has no spatial index at all, so the alternative is an array and a loop. The nearest, count, predicate and copy tiers allocate nothing.

```csharp
var depots = new KdTree<string>(new[]
{
    new SpatialPoint<string>(51.51, -0.13, "London"),
    new SpatialPoint<string>(53.48, -2.24, "Manchester"),
    new SpatialPoint<string>(55.95, -3.19, "Edinburgh"),
    new SpatialPoint<string>(52.49, -1.89, "Birmingham"),
});

// Which depot serves this address?
if (depots.TryFindNearest(52.20, -2.00, out SpatialPoint<string> nearest))
    Console.WriteLine(nearest.Value);                    // Birmingham

// The three closest, nearest first.
foreach (var d in depots.GetNearest(52.20, -2.00, 3))
    Console.WriteLine(d.Value);                          // Birmingham, Manchester, London

// Inside a delivery radius, and inside the map viewport — neither allocates.
Console.WriteLine(depots.CountWithin(53.00, -2.00, 1.5));                  // 2 — Manchester and Birmingham
var visible = new SpatialPoint<string>[4];
Console.WriteLine(depots.CopyInRectangle(51.0, -3.0, 54.0, 0.0, visible)); // 3
```

A distance bound is not just a filter — it seeds the search's pruning radius, so `TryFindNearest(x, y, maxDistance: r, out _)` is materially cheaper than an unbounded query you then test.

</details>

<details>
<summary><b>What is near me, for points that move</b> — SpatialGrid</summary>

`SpatialGrid<TValue>` is the mutable counterpart. `Add` hands back a handle, and `Move` through that handle is `O(1)` — a coordinate write plus, only when the point crossed a cell boundary, an unlink and a relink. No search, no rehash, and no equality contract on the payload.

```csharp
// A world 10,000 units square, in 100-unit cells — roughly the query radius, which is the tuning rule.
var world = new SpatialGrid<string>(0, 0, 10_000, 10_000, cellSize: 100);

SpatialGridHandle courier = world.Add(120, 340, "courier-7");
world.Add(180, 300, "rider-3");
world.Add(9_000, 9_000, "depot");

// Every tick: move what moved, then ask what is near it. Neither allocates.
world.Move(courier, 150, 320);
Console.WriteLine(world.CountWithin(150, 320, 90));       // 2 — the courier itself and the rider

// Removing retires the handle: it is rejected afterwards, not silently pointed at a recycled slot.
world.Remove(courier);
Console.WriteLine(world.TryGetPoint(courier, out _));     // False
```

Pick the cell size to put a handful of points in the average cell, and no smaller than your typical query radius. Points spread **evenly** are what this wants. If yours cluster hard, measured advice: the hand-rolled `Dictionary`-of-`List`s beats this type there, and rebuilding a `KdTree` per frame is no better than it either — see [Spatial index](#spatial-index).

</details>

<details>
<summary><b>Which boxes overlap this box, which contain this point</b> — RTree</summary>

`RTree<TValue>` indexes axis-aligned rectangles once and then answers extent questions without testing every box. `KdTree` cannot answer these: a box can overlap the query while its centre sits far outside it. The predicate, count and copy tiers allocate nothing.

```csharp
// Note the extents: a country-sized box alongside street-sized ones is exactly the shape
// this type is for — one grid cell size cannot suit both ends.
var features = new RTree<string>(new[]
{
    new SpatialBox<string>(-8.6, 49.9, 1.8, 60.9, "United Kingdom"),
    new SpatialBox<string>(-0.51, 51.28, 0.33, 51.69, "Greater London"),
    new SpatialBox<string>(-0.14, 51.50, -0.12, 51.51, "Covent Garden"),
    new SpatialBox<string>(-3.20, 55.94, -3.18, 55.96, "Edinburgh Old Town"),
});

// Which features cover this coordinate? Every enclosing extent, whatever its size.
foreach (var f in features.GetAtPoint(-0.13, 51.51))
    Console.WriteLine(f.Value);          // United Kingdom, Greater London, Covent Garden

// Visible in the viewport, and a hit test that stops at the first match — neither allocates.
Console.WriteLine(features.CountOverlapping(-1.0, 51.0, 0.5, 52.0));   // 3
Console.WriteLine(features.ContainsAtPoint(-3.19, 55.95));             // True
```

Edges are closed, so boxes meeting at an edge or a corner do overlap and a point on an edge is inside — which is also what makes a point query an overlap query against a degenerate box.

</details>

<details>
<summary><b>Which ranges cover this point / overlap this window</b> — IntervalTree</summary>

`IntervalTree<TKey, TValue>` is a build-once index over half-open `[start, end)` ranges. .NET ships nothing for this — no interval tree, no interval map, no range-overlap query anywhere in `System.Collections` — so the alternative is a `List<T>` filtered per query. Sorting that list by start lets the scan stop once a start passes the query, but it cannot skip the front (a range beginning far to the left can still cover the point), so it stays linear; both baselines are benchmarked.

```csharp
// A room's bookings. Build once; query many.
DateTime day = DateTime.Today;

var bookings = new IntervalTree<DateTime, string>(new[]
{
    new Interval<DateTime, string>(day.AddHours(9),  day.AddHours(10), "standup"),
    new Interval<DateTime, string>(day.AddHours(13), day.AddHours(15), "design review"),
});

// The conflict check: stops at the first overlap, allocates nothing.
bookings.Overlaps(day.AddHours(10), day.AddHours(11));   // false — the seam does not conflict
bookings.ContainsPoint(day.AddHours(14));                // true

// Every match, into a buffer you own — no allocation on the query path.
var matches = new Interval<DateTime, string>[8];
int found = bookings.CopyOverlapping(day.AddHours(9), day.AddHours(14), matches);

// Or the convenience tier, when an array per query is fine.
foreach (var meeting in bookings.GetContaining(day.AddHours(14)))
    Console.WriteLine(meeting.Value);                    // design review
```

Intervals are half-open, so `[0, 10)` and `[10, 20)` tile without conflicting. Overlapping ranges stay distinct entries — this is not a coalescing interval map — and the tree is immutable, so adding a range means building a new one.

</details>

<details>
<summary><b>Walk a graph, or resolve a dependency order</b> — CompressedGraph</summary>

`CompressedGraph` is a build-once directed graph over dense vertex ids, stored compressed sparse row: an offset per vertex and one contiguous target array. .NET ships nothing for this — no graph, no adjacency list, no adjacency matrix, no traversal — so the alternative is a `Dictionary<int, List<int>>`, or a `List<int>[]` once you notice the ids are dense; both are benchmarked.

```csharp
// Five packages; an edge means "must be built before". Vertex 2 depends on nothing.
var builds = new CompressedGraph(5, new[]
{
    new GraphEdge(0, 1),
    new GraphEdge(0, 3),
    new GraphEdge(1, 4),
    new GraphEdge(3, 4),
});

// Neighbours are a slice of the graph's own storage — no copy, no enumerator, no allocation.
foreach (int dependent in builds.Neighbors(0))
    Console.WriteLine(dependent);                       // 1, then 3

builds.ContainsEdge(0, 4);                              // false — the path is 0 -> 1 -> 4, not a direct edge

// Build order, or false when the dependencies are circular.
if (builds.TryGetTopologicalOrder(out int[] order))
    Console.WriteLine(string.Join(" ", order));         // 0 2 1 3 4

// "What does 4 depend on?" — the transpose, O(V + E) rather than a rebuild. Mind the direction: an edge
// means "must be built before", so 4's in-neighbours are its dependencies, not its dependents.
CompressedGraph incoming = builds.Reverse();            // incoming.Neighbors(4) is 1, 3

// Everything reachable from 0, into a buffer you own.
var reached = new int[builds.VertexCount];
int count = builds.CopyBreadthFirstOrder(0, reached);   // 0 1 3 4
```

Vertices are dense ids in `[0, VertexCount)`, so per-vertex payload goes in an array of your own indexed the same way. Duplicate edges collapse, self-loops are kept, and an undirected graph is built by supplying each edge in both directions. The graph is immutable, so adding an edge means building a new one.

</details>

<details>
<summary><b>Search one block of text over and over</b> — SuffixArray</summary>

`SuffixArray` is a build-once index over a block of text: every suffix in sorted order, plus the longest-common-prefix array. Locating a pattern is a binary search over that order, so it costs `O(m log n)` in the *pattern* length where `string.IndexOf` costs `O(n)` in the text — every time. That is the floor the members build on rather than the cost of each: `IndexOf` adds an `O(k)` pass over the `k` matches, sorted retrieval adds `O(k log k)`, and the longest-repeat query is `O(n)` over the LCP array. .NET ships no text index at all.

```csharp
var index = new SuffixArray("the cat sat on the mat");

index.CountOccurrences("at");                          // 3 — a pair of binary searches, not a scan
index.IndexOf("the");                                  // 0
index.Contains("dog");                                 // false — ruled out in log n, not a full read

// Every position, ascending.
index.GetOccurrences("at");                            // 5, 9, 20

// The zero-copy tier: a slice of the index itself, in suffix order rather than positional.
index.TryGetOccurrences("the", out ReadOnlySpan<int> found);   // true, found.Length is 2

// The question no scan can express — the naive answer is quadratic.
index.TryGetLongestRepeatedSubstring(out int start, out int length);
index.Text.Slice(start, length).ToString();             // "the "
```

Ordinal, over UTF-16 code units — fold the text and the pattern the same way for case-insensitive matching. About 10 bytes per character, and immutable: changing the text means rebuilding. **Read the build cost against the query count before reaching for it**: at 100,000 characters the queries measure 64–68x the scan and the build is repaid at roughly 1,000 of them, while at 1,000 characters the margin is 1.3x and the crossover is ~3,900. The second baseline is not the scan but the `Dictionary<string, int[]>` k-gram index a caller writes instead, which **wins by 10.3x** per query at the one pattern length it can answer.

</details>

<details>
<summary><b>Scan a text once for many patterns</b> — AhoCorasick</summary>

`AhoCorasick` compiles a fixed set of patterns into one automaton and finds *every* occurrence of *every* pattern in a single left-to-right pass, at `O(n + matches)` — a cost that does not grow with how many patterns there are. `Trie` matches a *prefix of the query* and cannot resume inside a text; `SuffixArray` indexes one fixed *text* and answers one pattern at a time. This is the other way round: the **patterns** are indexed and the text is read once. .NET ships nothing for it — `string.IndexOf` is a single-needle scan, so `k` patterns cost `k` passes.

```csharp
var alerts = new AhoCorasick(["OutOfMemory", "StackOverflow", "Timeout", "Deadlock"]);

// The allocation-free tier: the enumerator drives the scan as it is pulled, and a line with no
// match simply yields nothing — do NOT guard it with ContainsAny, which would scan the line twice.
foreach (string line in File.ReadLines(path))
{
    foreach (PatternMatch match in alerts.EnumerateMatches(line))
        Console.WriteLine($"{alerts[match.PatternId]} at {match.Start}");
}

// ContainsAny is the tier for when a boolean is all you need: it stops at the first match rather
// than running the line out. It takes a ReadOnlySpan<char>, so it is not a Func<string, bool>.
foreach (string line in File.ReadLines(path))
{
    if (alerts.ContainsAny(line))
        Quarantine(line);
}

// Overlapping matches are reported, not resolved — picking a winner is your policy, not the matcher's.
var automaton = new AhoCorasick(["he", "she", "hers"]);
automaton.CountMatches("ushers");                      // 3 — she at 1, he at 2, hers at 2
```

Matches come out in ascending **end** position, longest first among those ending together — not in ascending `Start`, which a single pass cannot produce. Ordinal over UTF-16 code units; duplicates collapse and the empty pattern is rejected; immutable, so changing the pattern set means rebuilding. **The pattern count and the workload shape both decide**: against a compiled `Regex` alternation of 256 patterns over 100,000 characters this measures 10.0x on membership and 4.8x on enumerating every match, with neither side allocating — but against the plain k-`IndexOf` loop it is 4.1x on counting present patterns and a **0.98x loss** on rejecting absent ones, where a scan that never finds a candidate to verify stays inside its vectorized sweep. At **eight** patterns the loop wins by 5.0x. Break-even is near fifty patterns for counting and several hundred for absent membership; below that, write the loop.

</details>

<details>
<summary><b>Which of these pending things have timed out</b> — TimerWheel</summary>

`TimerWheel<TValue>` is a container of pending deadlines with `O(1)` `Cancel` and amortized-`O(1)` `Schedule`. It owns no thread and no clock: you drive it with `Advance`, and everything due comes back at once. The BCL answer is a `PriorityQueue`, which **cannot cancel** — no `Remove` on `net8.0`, and `O(n)` on .NET 9 — so the workaround is lazy deletion, and the heap fills with timers that will never fire.

```csharp
// A tick is a millisecond here; the default 256 x 4 geometry reaches about 49 days.
var timeouts = new TimerWheel<PendingRequest>();
var fired = new List<PendingRequest?>();      // reused, so a steady-state tick allocates nothing

TimerHandle armed = timeouts.Schedule(delayTicks: 30_000, request);
timeouts.Cancel(armed);                       // the reply beat the clock — O(1), payload released

fired.Clear();                                // Advance *appends* — reuse is only safe if you empty it first
timeouts.Advance(now, fired);                 // fires everything with deadline <= now
foreach (PendingRequest? timedOut in fired)
    timedOut!.Fail(new TimeoutException());

// A jump costs the wheel, not the distance: at worst every slot at every level, never the tick count.
fired.Clear();
timeouts.Advance(timeouts.CurrentTick + 5_000_000, fired);
```

Timers thread through one flat entry array as intrusive lists, so there is no object per timer, and a cascade only ever moves one *down* a level. Two things are traded for the constant time: a finite **horizon** — a delay of `Horizon` ticks or more is rejected rather than misplaced — and **no ordering** within a fired batch, since a wheel buckets rather than sorts. **Read the population before reaching for it.** On a round of 100,000 timeouts (schedule, cancel nine in ten, run the clock out) CI measures **6.81x** the BCL heap and **7.09x** `IndexedPriorityQueue`; at a thousand it is 2.56x. Taken apart, the wheel loses `Schedule` outright — about 2x slower than a heap insert at 100,000, though still 2.71x faster than the addressable heap, which pays a hash write per insert — and loses `Cancel` to a "cancel" that removes nothing and defers the work to the drain, which is why the round that charges both halves is the row to read. At a thousand timers the tick-by-tick drive is 1.8x slower too. It also allocates about a fifth more per round than either heap.

</details>

<details>
<summary><b>Edit a large block of text in the middle</b> &mdash; Rope</summary>

`Rope` is a balanced tree of bounded character runs, so an edit costs the *depth* of the document rather than its length. `StringBuilder` is a chunk list whose head is the end: appending is excellent, and `Insert` / `Remove` walk the list and then shift what follows, which is linear in the document every time. A rope also splits and joins in `O(log n)`, which the BCL cannot do at any price.

```csharp
var document = new Rope(File.ReadAllText("chapter.md"));

document.Insert(11, "very ");        // O(log n + inserted length) — and for a short edit that
                                     // lands in a leaf with room, allocation-free
document.Remove(0, 7);
char first = document[0];

// Cut the document in two and put it back the other way round, both O(log n) and without
// copying the document — a split copies at most the one leaf the cut lands inside.
// AppendAndClear *moves*, which is why it empties its argument.
Rope tail = document.Split(document.IndexOf('\n') + 1);
tail.AppendAndClear(document);
document.AppendAndClear(tail);

// The zero-copy read path: no intermediate string.
foreach (ReadOnlySpan<char> chunk in document.GetChunks())
    Console.Out.Write(chunk);
```

Reach for it when the text keeps changing in the middle &mdash; editor buffers, template splicing, log assembly, diff application. **Do not reach for it to append**, which is 36.8x slower than a `StringBuilder`, or to index, which is 7.1x slower.

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
| Dictionary keyed by `Guid`, `string`, or any other type | `CelerityDictionary<TKey, TValue, THasher>` | Pick a struct hasher from `Celerity.Hashing` (e.g. `GuidHasher`, `StringFnV1AHasher`) so the JIT can inline `Hash()` on the probe path. For **`string` keys**, try `HashCachingDictionary` (next rows but one) first: the BCL `Dictionary` stores a hash code per entry, and matching that is what closes the gap on reference-type keys — see the [performance guide](docs/performance.md#reference-type-keys-cache-the-hash). |
| Dictionary with **clustered / adversarial** keys where worst-case lookup latency matters | `RobinHoodDictionary<TKey, TValue, THasher>` | Same API as `CelerityDictionary`, but Robin Hood probing bounds probe-length variance so tail-latency lookups don't degrade on bunched keys. Costs a per-slot probe-distance `int`; for uniform keys with a good hasher, prefer `CelerityDictionary`. |
| **Lookup-heavy** dictionary (large tables, many negative lookups) where SIMD pays off | `SwissDictionary<TKey, TValue, THasher>` | Same API as `CelerityDictionary`, but Swiss-table group probing tests 16 slots per `Vector128` compare and filters candidates by a 7-bit hash tag before any key comparison. Costs a one-byte control tag per slot; for small or write-dominated tables, `CelerityDictionary` is competitive. |
| **Lookup-heavy** dictionary with **costly key equality** (long strings, large value-type keys) or large cache-cold tables | `HashCachingDictionary<TKey, TValue, THasher>` | Same API as `CelerityDictionary`, but a dense side array of 32-bit hash fingerprints lets probes scan metadata only and short-circuit the key comparison on a single integer compare. Costs four bytes of metadata per slot; complementary to `SwissDictionary` (scalar wide fingerprint vs SIMD one-byte tags). For small tables of cheap keys, `CelerityDictionary` is roughly a wash. On 100k `string` keys it is the difference between losing to the BCL `Dictionary` and beating it on the negative-lookup path — see the [performance guide](docs/performance.md#reference-type-keys-cache-the-hash). |
| **Short-lived** dictionary rebuilt frequently on a hot path where GC pressure matters | `PooledCelerityDictionary<TKey, TValue, THasher>` | Same API as `CelerityDictionary` plus `IDisposable`; rents its backing arrays from `ArrayPool<T>.Shared` and returns them on `Dispose`, so build/use/dispose cycles recycle buffers instead of allocating. Dispose it (a `using` scope); for long-lived dictionaries the pooling buys nothing, so prefer `CelerityDictionary`. |
| Build-once, read-many lookup table keyed by `string` | `FrozenCelerityDictionary<TValue>` | Immutable; searches for a perfect (collision-free) hash at build time so lookups are single-probe. Tune the hasher via the `<TValue, THasher>` overload. |
| One key maps to **many** values (one-to-many) | `CelerityMultiMap<TKey, TValue, THasher>` | `Add` appends to a per-key value group instead of overwriting; implements `ILookup<,>`. Pick the struct hasher for your key type, as with `CelerityDictionary`. |
| **Counting** occurrences / frequency histogram (element → count) | `CelerityMultiSet<T, THasher>` | `Add` is a single probe-and-increment vs the two-probe `Dictionary<T,int>` counting idiom; `SetCount` / `Remove` / `RemoveAll` manage multiplicities, `Count` is distinct elements and `TotalCount` the sum. Pick the struct hasher for your element type. |
| Tiny dictionary (`n <= ~16`) that stays small | `SmallDictionary<TKey, TValue>` | Flat-array linear scan beats hashing at small `n` — no hash to compute, great cache locality, no hasher to pick. Degrades to `O(n)` for large key sets, so only when instances stay small. |
| Dictionary keyed by a small **enum** — config-by-enum, per-state data, enum→handler tables | `EnumMap<TEnum, TValue>` | Dense array indexed on the enum's underlying value (the .NET `EnumMap`): `this[key]` / `TryGetValue` / `Add` / `Remove` are a single direct array index — no hashing, no probing, no collisions — and a full sweep is a linear array walk. The dictionary counterpart of `EnumSet`; enumerates ascending by value. For enums whose members are small non-negative integers (the default); negative or sparse `[Flags]` enums are unsupported — use `CelerityDictionary<TEnum, TValue, THasher>` there. |
| Tiny set (`n <= ~16`) that stays small — per-scope "seen" sets, small membership guards, deduping a handful of items | `SmallSet<T>` | The set counterpart of `SmallDictionary`: flat-array linear scan beats hashing at small `n`, no hasher to pick, the default element is stored inline. Implements `ISet<T>` and `IReadOnlySet<T>`. Degrades to `O(n)` for large sets, so only when instances stay small. |
| Set of **enum** values — flag sets, permission sets, state sets over a small enum | `EnumSet<TEnum>` | Bit-vector set indexed on the enum's underlying value (the .NET `EnumSet`): `Add` / `Contains` / `Remove` are a single bit op — no hashing, no boxing — and set algebra between two `EnumSet`s is a word-wise bitwise `OR` / `AND` / `XOR`. Enumerates ascending by value; `All()` builds the full universe. For enums whose members are small non-negative integers (the default); negative or sparse `[Flags]` enums are unsupported — use `CeleritySet<TEnum, THasher>` there. |
| Set of small **non-negative ints** over a bounded range that is **cleared & rebuilt often** — "visited" sets in graph BFS/DFS, ECS entity membership, sweep-line | `SparseSet` | Briggs–Torczon sparse set (dense value array + sparse index array): `O(1)` `Clear` that leaves the backing arrays untouched (vs `HashSet<int>` zeroing its table) and dense, cache-friendly iteration over just the present elements. `Add` / `Contains` / `Remove` are `O(1)`, no hashing. Costs `O(Universe)` memory and stores only values in `[0, Universe)`; for an unbounded or huge-and-sparse key space use `IntSet` / `HashSet<int>`. |
| **Huge, sparse set of 32-bit ints** where the work is **set algebra**, not point lookups — inverted-index posting lists, column-store row-id sets, bitmap analytics, cohort intersection; also any int set where memory is the constraint | `CompressedIntSet` | Exact, compressed: the value space is split into 65,536-value chunks and each chunk is stored as a sorted `ushort[]`, a 1024-word bitmap, or run-length pairs — whichever is smallest. `UnionWith` / `IntersectWith` / `ExceptWith` work inside a chunk (a sorted merge, or one ANDed word per 64 values) and skip a chunk neither side populates with a single comparison, so cost tracks *populated chunks* rather than elements. Memory is ~10x below `HashSet<int>` when sparse and far lower when dense or clustered; enumeration is **in ascending order**. Point `Contains` is still faster in a hash table — use `IntSet` / `HashSet<int>` if lookups are the whole workload. `BitSet` beats it when the universe is small and dense, `SparseSet` when it is small and cleared every iteration. **No portable Roaring format** (Celerity ships no serializers), so it is not a Lucene / Druid / Spark interop path. |
| Sorting many primitive keys — ids, join keys, timestamps — at a thousand elements and up | `RadixSort` (package `Celerity.Sorting`) | Four or eight branch-free counting passes instead of introsort's `O(n log n)` mispredicting comparisons, in keys-only, key+payload, and argsort forms. **Stable.** Needs `O(n)` scratch, which is why `Array.Sort` cannot do this at all. Below a few hundred elements `Array.Sort` wins — the crossover is measured on the [dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/). |
| Sorting values drawn from **few distinct keys** — enum ordinals, bucket ids, quantized scores | `CountingSort` (package `Celerity.Sorting`) | One histogram pass and one run-fill, `O(n + range)`; the keys-only forms never move an element twice and allocate nothing for `byte` keys. Loses once `range` approaches `n` — use `RadixSort` there. |
| Only the **top / bottom *k*** of a large span is wanted | `PartialSort` (package `Celerity.Sorting`) | `O(n)` introselect for the *k* smallest in place, or an `O(n log k)` bounded heap into a destination when the source must not be reordered. Against LINQ the win is allocation and boxing, not asymptotics — `OrderBy().Take(k)` already partial-sorts. |
| **Percentiles over a stream** — p50 / p90 / p99 latency, payload size, queue depth, over data you cannot keep | `DDSketch` (package `Celerity.Statistics`) | The BCL has no quantile type at all, so the alternative is retaining every sample in a `List<double>` and sorting per query: unbounded memory and `O(n log n)` an answer. The sketch is accurate to a **relative** `α` at *every* quantile — 1% of 10 ms and 1% of 10 s — in memory proportional to the log of the value range, and merges bucket-exactly across shards (unless a shard has already exhausted its bin budget, which `HasCollapsed` reports). ⚠️ If your data is static and fits in memory, **sort it once and index**; that arm is on the dashboard and the sketch loses it. |
| A **bounded uniform sample** of a stream of unknown length — log lines, trace spans, request bodies | `ReservoirSampler<T>` (package `Celerity.Statistics`) | Li's Algorithm L: `O(k)` memory and `O(k · log(n / k))` random draws over the whole stream, never needing to know `n`. `OrderBy(random).Take(k)` sorts the entire sequence to keep `k` of it and cannot run on a stream at all. Seeded, so the sample is reproducible on a given runtime and platform (not byte-identical across them — the skip arithmetic goes through `Math.Log` / `Math.Exp`, whose last bit .NET does not fix). |
| **Mean / variance / skew** of a stream, or of each of many buckets | `RunningStatistics` (package `Celerity.Statistics`) | `System.Linq` has `Average` and nothing else. One pass, no allocation, and numerically stable where the `sum` / `sumOfSquares` shortcut everyone writes returns a negative variance once the mean is large relative to the spread. A `struct`, so `default` is a valid empty accumulator and per-bucket arrays cost nothing. |
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
| **Dense set of small integer indices** (or a fixed universe of flags) where you count set bits or combine whole vectors — bitmaps, visited/presence masks, sieves | `BitSet` | Exact dense bit vector packed into 64-bit words: `O(n/64)` population count (`Count`) via hardware popcount and SIMD bulk `And`/`Or`/`Xor`/`Not`. A faster, count-aware `System.Collections.BitArray`. For **sparse** indices over a huge/unbounded domain, `IntSet` / `LongSet` is more memory-efficient — and `CompressedIntSet` more so again when the work is set algebra rather than point lookups; for approximate membership over arbitrary elements, use `BloomFilter`. To ask *positional* questions of a settled bit vector — "how many set bits below here", "where is the k-th one" — index it with `RankSelectBitVector`. |
| **Positional queries over a bit vector that has stopped changing** — dense↔sparse index remapping in a column store, succinct / compressed tries, wavelet trees | `RankSelectBitVector` | Immutable succinct index over the bits of a `BitSet` (or packed `ulong[]`): `Rank(i)` — set bits below `i` — in `O(1)` from a two-level popcount index, and `Select(k)` — position of the `k`-th set bit — in `O(log n)`. The BCL has no rank or select at all, so the baseline is a hand-rolled `O(i/64)` popcount loop; the index replaces ~780,000 iterations with two loads and one masked popcount at the midpoint of a 100M-bit vector. Costs 25% space over the bits, and is **build-once**: any mutation requires an `O(n/64)` rebuild, so a vector that keeps changing should stay a plain `BitSet` (or `SparseSet`) and be snapshotted only once it settles. |
| **Distinct count** over a large or unbounded stream (unique visitors / events, distinct-value cardinality, deduplicated counts across shards) where a small relative error is acceptable | `HyperLogLog<T, THasher>` | Probabilistic: estimates the distinct count from a fixed array of registers (16&#160;KB at the default precision) with a ~0.8% relative standard error, never growing with the cardinality — unlike a `HashSet<T>` that stores every distinct value. Add-and-estimate only; merge shard estimators with `UnionWith`. If you need an exact count or to test a specific element, use `HashSet<T>` / `CeleritySet`; for approximate *membership* rather than counting, use `BloomFilter`. |
| **Per-element frequency** of a *specific* element over a large or unbounded stream (approximate per-key counts, rate limiting, deduplicated frequency counts across shards) where a small one-sided overestimate is acceptable | `CountMinSketch<T, THasher>` | Probabilistic: estimates each element's frequency from a fixed grid of counters (sized from `epsilon` / `delta`) that never grows with the distinct-key count — unlike a `Dictionary<TKey, int>` frequency table. **Never underestimates**; overestimates bounded by `epsilon · TotalCount` with confidence `1 − delta`. Add-and-estimate only; merge shard sketches with `UnionWith`. If you need exact counts or to enumerate keys, use a `Dictionary<TKey, int>`; if you want the *set of* heaviest elements rather than a specific one's count, use `TopKSketch`; for the distinct *count* use `HyperLogLog`, for approximate *membership* use `BloomFilter`. |
| **Top-k / heavy hitters** — the *most frequent* elements of a large or unbounded, high-cardinality stream (top URLs / IPs, trending items, network flow monitoring, hot keys) where only the heaviest matter | `TopKSketch<T, THasher>` | Probabilistic (Space-Saving): keeps a fixed `k` monitors, so memory is `O(k)` regardless of the distinct-key count — unlike a `Dictionary<TKey, int>` that must materialize every distinct key just to rank the top few. **Never underestimates** a monitored count and never misses an element above `TotalCount / k`; each result carries a bounded `Error`. Add-and-query only (no `Remove`, no `UnionWith`). If you need the exact fully-ranked counts, use a dictionary frequency table; for a *specific* element's frequency use `CountMinSketch`. |
| **Bounded cache** with automatic eviction — memoize the last `N` results, an admission cache in front of an expensive lookup, any hot key→value store that must not grow without bound | `LruCache<TKey, TValue, THasher>` | Fixed-capacity least-recently-used cache: `O(1)` get/put, and once at capacity every insert evicts the least-recently-used entry. Its recency list is threaded through fixed-size arrays, so after construction the hot get/put/evict path **allocates nothing** — where the idiomatic `Dictionary` + `LinkedList` LRU allocates a `LinkedListNode` per insert. Reads are *uses* (they promote to most-recently-used); use `TryPeek` / `ContainsKey` to inspect without touching recency. Single-threaded — because reads mutate recency, even a read-mostly concurrent workload needs a write lock. |
| **Double-ended queue** — add/remove at both ends (bounded FIFO queue, sliding window, work-stealing / undo buffer) or a queue needing random access by position | `Deque<T>` | Growable double-ended queue backed by a **circular buffer**: `O(1)` amortized `PushFront` / `PushBack` / `PopFront` / `PopBack` / peek and `O(1)` random access by index. The BCL has no deque — `Queue<T>` is FIFO-only, `Stack<T>` LIFO-only, and `LinkedList<T>` (the only O(1)-both-ends type) allocates a node per element. A warm bounded churn reuses the buffer with wrap-around so it **allocates nothing**, and enumeration walks contiguous memory. For a strict FIFO queue that never pushes front / pops back, BCL `Queue<T>` is already a circular buffer and is simpler. |
| **Incremental connectivity / connected components** — union equivalence classes and ask whether two elements are in the same group (Kruskal MST, clustering, image segmentation, undirected cycle detection, "are these accounts linked?") | `DisjointSet<T>` | Union-find with **union by size** + **path halving**: near-`O(1)` amortized `Union` / `Find` / `Connected`, `O(α(n)) ≤ 4`. Runs a stream of merges + connectivity queries in near-linear total time, where the BCL substitutes are super-linear — a `Dictionary<T, HashSet<T>>` set-merge is `O(n²)` to coalesce `n` singletons, and a per-query BFS/DFS is `O(V+E)` every query. Grows only by merging (no un-union); it is not an `ISet<T>` — for element membership with add/remove/set-algebra use `CeleritySet` or `HashSet<T>`. |
| **Priority queue whose priorities change** — a best-so-far frontier you relax (Dijkstra / Prim / A\*), or an event scheduler that reschedules / cancels pending items | `IndexedPriorityQueue<TElement, TPriority, THasher>` | Addressable binary min-heap with an element→slot index: `Update` (decrease-/increase-key) and `Remove` an arbitrary element in `O(log n)`, `Contains` / `TryGetPriority` in `O(1)`. The BCL `PriorityQueue<,>` can do none of these — its only substitute is lazy deletion, which grows the heap by one entry per update. Each element is a key (appears once); custom `IComparer<TPriority>` for a max-heap. For plain enqueue/dequeue with duplicate elements, the BCL `PriorityQueue<,>` is simpler. |
| **Prefix / autocomplete / longest-prefix** over string keys — list everything under a prefix, find the most specific stored key that prefixes a query, or iterate keys in order (typeahead, route/dispatch tables, tokenizer / dictionary matching, namespace listing) | `Trie<TValue>` | Ordered prefix tree: `GetByPrefix` yields every entry under a prefix in `O(prefix + matches)` and in ascending key order, `TryGetLongestPrefix` finds the longest stored prefix of a query in `O(query)`, and enumeration is sorted for free — none of which a `Dictionary<string, TValue>` can do without an `O(n)` scan + `StartsWith`. For **pure exact-key** `Add` / `TryGetValue` / `Remove` a `Dictionary` (one hash vs a per-character walk) is faster; the trie earns its place only when you use the prefix operations. Implements `IReadOnlyDictionary<string, TValue?>`; not thread-safe. |
| **Many occurrences of few distinct strings**, produced as slices of a buffer you already hold — CSV / log / JSON parsing, tokenizers, column stores, header dispatch: you want one canonical `string` per distinct token, not one per occurrence | `StringInternTable` | Probed with a `ReadOnlySpan<char>`: `GetOrAdd` returns the shared instance and **allocates only on a miss**, so a 10M-cell parse over 100 distinct tokens creates 100 strings, not 10,000,000. `HashSet<string>` cannot express this before .NET 9 — its `TryGetValue` takes a `string`, so you must allocate the string *before* you can discover you already had it. `string.Intern` is process-wide, never collected, and still needs a `string`; this table's lifetime is yours and `Clear` releases it. On .NET 9+ `Dictionary<string,V>.GetAlternateLookup` is comparable; this works on `net8.0` too. Not thread-safe. |
| **Look a string key up from a `ReadOnlySpan<char>`** you already hold (route dispatch, header lookup, parse-then-map) without allocating a `string` per probe | span overloads on `FrozenCelerityDictionary` / `FrozenCeleritySet` / `CelerityDictionary<string, …>` / `CeleritySet<string, …>` / `Trie<TValue>` | `TryGetValue(ReadOnlySpan<char>, …)` / `ContainsKey` / `Contains` probe the table directly, deleting the `new string(span)` allocation and copy per lookup. Available whenever the hasher implements `ISpanHashProvider` — every built-in `String*Hasher` does. Same results as the `string` overloads (ordinal comparison); an empty span means `""`, never the `null` key. See [span-keyed lookups](docs/api/collections.md#span-keyed-lookups). |
| **Sorted keys** — you need the entries in comparer order, or the ordered questions a hash table cannot answer: smallest / largest key, "first key at or after *x*", "every key in `[a, b)`" (time-series by timestamp, order books, LSM-style memtables, sweep-line events, interval endpoints) | `BTreeDictionary<TKey, TValue>` / `BTreeSet<T>` | B-tree with up to 31 keys per node in flat arrays: a lookup visits `log₃₂(n)` nodes instead of chasing `log₂(n)` pointers (~4 cache misses instead of ~20 at `n = 1M`), an in-order walk streams contiguous arrays rather than successor pointers, and allocation is one node per 31 entries instead of one object per entry. The BCL has no B-tree: `SortedDictionary<,>` / `SortedSet<>` are red-black trees, `SortedList<,>` is `O(n)` per middle insert, and `OrderedDictionary<,>` (.NET 9) is *insertion*-ordered, not sorted. Wins on the **interleaved insert + lookup + range-scan** load; for a few dozen entries a `SortedList<,>` is hard to beat, and if you never need order a hash table answers in `O(1)`. |
| **Prefix / range sums over a sequence you keep mutating** — running aggregates, rank / order-statistics counters (inversions, "how many ≤ x seen"), cumulative-frequency tables | `FenwickTree<T>` | Binary Indexed Tree (`T : INumber<T>`): **point update** and **prefix / range sum** both `O(log n)`, in one array with no per-node overhead. The BCL has no prefix-sum structure; a plain array forces `O(n)` per query (recompute the slice) *or* `O(n)` per update (fix the suffix). Wins precisely when updates and partial-sum queries interleave. If the data is immutable after build, a one-shot precomputed prefix-sum array answers in `O(1)` with less code; if you only update and never query a partial sum, a raw array is simpler. |
| **Range min / max / gcd / bit-mask over a sequence you keep mutating** — sliding-window extrema over a live history, "cheapest offer in this price band" over an order book, per-window capability masks, or any other **associative fold with no inverse** | `SegmentTree<T, TMonoid>` | Point update and range query both `O(log n)`, in one flat array of `2n` cells. `FenwickTree<T>` cannot answer these at all: it computes a range as the *difference* of two prefix folds, so the operation must be invertible. Five folds ship (`Sum` / `Min` / `Max` / `BitwiseAnd` / `BitwiseOr`) and any associative one you write is a field-free struct; non-commutative folds are safe. The BCL has no range-aggregate structure, so the alternative is an `O(n)` scan per query — **14.8× faster** on interleaved update + range-min at 100k, **81×** on a query batch, but only **1.4×** at 1k, where scanning a contiguous array is cache-friendly. If the fold is addition use `FenwickTree<T>` (half the memory); if the sequence never changes after build, a sparse table or a prefix array answers in `O(1)`. Range *updates* are not supported — that needs lazy propagation, a different contract. |
| **Which point is nearest, which points are within this radius or box** — nearest store / driver / sensor, viewport and map-tile culling, collision broadphase, the neighbour queries inside k-means and DBSCAN, snap-to-nearest, duplicate-coordinate detection | `KdTree<TValue>` | Build-once 2-D index over points in the plane. The BCL ships **no spatial index of any kind**, so the fallback is an array measured in full on every query; at 100,000 points the nearest query beats that scan by two orders of magnitude. Judge it, though, against the *hand-rolled* alternative — points sorted by x and scanned outward from the query — where the margin is a small constant factor — 2.5x at 100,000 points, and roughly nothing at a thousand. Immutable: adding a point means rebuilding, so it is for point sets that are built once and queried many times — if they move every frame, reach for `SpatialGrid<TValue>` instead. |
| **What is near me, for points that move** — game entities and projectiles, drivers and couriers on a map, cursors and drag targets, particles in a simulation, agents in a model | `SpatialGrid<TValue>` | Mutable uniform-cell index: `Move` / `Remove` are `O(1)` and `Add` is `O(1)` amortized, all addressed by a handle, so relocating a point is a coordinate write plus, only when it crossed a cell boundary, an unlink and a relink — no search, no rehash, and no equality contract needed on the payload. The baseline is the `Dictionary<(int, int), List<T>>` bucketed grid a competent developer writes, and on a frame of 100,000 entities (move a tenth, then one radius query each) it measures **5.0x** that, against **13.3x** for rebuilding a `KdTree` every frame. ⚠️ Read the caveat with the number: both structures test the same candidates, so everything this type saves is **per cell** rather than per point, and the margin thins as the cells fill — **1.12x** at ten points per cell, and **0.52x on clustered data, where the hand-roll wins outright** because a long cell list is a serial chain of dependent loads. It wants points spread *evenly*; if yours cluster hard, the hand-roll beats it and a per-frame `KdTree` rebuild is level with it, so neither Celerity type is the answer there. The world rectangle and cell size are declared up front (points outside are clamped into the edge cells), and there is no k-nearest query. |
| **Which boxes overlap this box, which contain this point** — collision broadphase for bodies with a size, map label and marker placement, canvas and UI hit-testing, viewport culling of sized objects, spatial joins between two box sets | `RTree<TValue>` | Build-once 2-D index over axis-aligned *extents*. `KdTree` cannot stand in: a box can overlap the query while its centre sits far outside it. The BCL ships no extent index either, so the fallback is an array tested in full on every query; at 100,000 boxes the overlap query beats that scan by two orders of magnitude and beats the *hand-rolled* alternative — boxes sorted by `minX`, scanned across the slab the query can still reach — by **9.6x**. The standard advice that it only pays off on extents spanning orders of magnitude is measured rather than repeated, and does not survive: its margin is *widest* on uniform ones. Immutable: adding a box means rebuilding, so it is for box sets built once and queried many times, not ones that move every frame. |
| **Which ranges cover this point, or overlap this window** — booking / scheduling conflict checks, IP-range and CIDR-to-owner lookup, effective-dated pricing and feature-flag windows, "which trace spans were live at *t*" | `IntervalTree<TKey, TValue>` | Build-once index over half-open `[start, end)` ranges, costing what the matches cost rather than what the collection costs — `O(log n + k)` when matches cluster, `O(min(n, (k + 1) log n))` when scattered, `O(n)` worst case — against the `O(n)` `List<T>` scan the BCL leaves you with — it ships no interval structure at all, and sorting the list by start only lets the scan stop at the upper bound (it cannot skip the front, since a range beginning far to the left can still cover the point), so it stays linear. At 100k intervals (CI's same-runner A/B): **129x** on the point query, **75x** on the window query, **19x** on the first-overlap conflict check — and **19x** on the point query against the *better* baseline, a start-sorted list that stops at the upper bound, which is the number to judge it by. The predicate / count / copy tiers allocate nothing. The ratio tracks **selectivity**, not just size — on a shape ten times denser (~1,250 matches per point) the point query falls to **8.3x** in a local control, since the tree still has to visit every match. One input defeats the pruning outright: a stored **zero-length** interval cannot be pruned in bulk (it raises its subtree's maximum end, then fails the emptiness test per node), so a tree full of them is `O(n)` per query having matched nothing — filter them before building if you have many. Build is the cost: **28x** a `List<T>` fill and 2.3x its memory, paid once. Immutable, so adding a range means rebuilding; overlapping ranges stay distinct entries rather than being coalesced. |
| **Walk a graph, or resolve a dependency order** — build and package ordering, module and import resolution, link / follower / call graphs, reachability and impact analysis | `CompressedGraph` | Build-once compressed-sparse-row graph over dense vertex ids: an offset per vertex and one contiguous target array, so `Neighbors(v)` is a `ReadOnlySpan<int>` slice rather than a `List<int>` object per vertex. .NET ships no graph type of any kind, so the alternative is `Dictionary<int, List<int>>` or `List<int>[]`; both are benchmarked. Every ratio names its baseline, because the three disagree by a lot; all are CI same-runner A/B figures, given as ranges over two runs since the allocation-heavy arms move up to 20% between them. Against `Dictionary<int, List<int>>` at 100k vertices: traversal 2.5–2.6x, build 3.1–3.8x, **3.6x** less retained memory. Against `List<int>[]`: traversal 1.7–1.8x, topological order 1.95x, transpose 8.6–13.6x. Against an **`int[][]` sized exactly and filled in vertex order — the best hand-roll and the one to judge it by, held to this type's own standards (count array reused as the scatter cursor, `Array.Empty` for empty rows)**: traversal **1.15–1.28x** *and a loss at 1,000 vertices*, build **1.16–1.29x** *and a loss at 1,000 vertices*, transpose **2.5–3.0x**, retained memory **1.8x** less. **So this is not a speed win over a carefully written hand-roll.** What survives is the transpose (structurally: the jagged form must allocate a row per vertex where CSR scatters into arrays it already holds), the footprint, and not writing or maintaining the traversal, Kahn's algorithm, the transpose, the deduplication and the sorted-target invariant — none of which the BCL ships at all. A pre-registered kill criterion (≥3x on the traversal) was **missed**, at 2.5–2.6x; the type ships on the BCL-gap limb of the rule instead, recorded rather than rounded away. Immutable, so adding an edge means rebuilding; duplicate edges collapse and vertex ids must be dense. No edge weights and no shortest paths. |
| **Find a substring in one block of text, many times over** — log / document / source-code search, "how many times does X appear", near-duplicate and plagiarism detection, bioinformatics | `SuffixArray` | Build-once **text index**: every suffix in sorted order plus the longest-common-prefix array, so a query is a binary search at `O(m log n)` in the *pattern* length against `string.IndexOf`'s `O(n)` in the text — every query, forever (`IndexOf` adds an `O(k)` pass over the matches; sorted retrieval adds `O(k log k)`). Measured at 100,000 characters: **68x** on counting, **64x** on ruling out an absent pattern, 1.3x at 1,000. The BCL has no text index; `SearchValues<string>` indexes the needles, not the haystack. Also answers *what is the longest substring occurring twice*, which no scan can. **The build has to be amortized** — one query against a text read once is a loss, and the index costs about 10 bytes per character. If the patterns are all one known length, a `Dictionary<string, int[]>` k-gram index is faster per query and this is the wrong tool; if the text changes, so is it. |
| **Find any of many patterns in a text, in one pass** — log and alert scanning, keyword / profanity / brand filters, WAF and IOC rule sets, dictionary tokenizers, DLP scanning | `AhoCorasick` | Build-once **multi-pattern automaton**: every occurrence of every pattern in one left-to-right pass at `O(n + matches)`, independent of how many patterns there are, reporting **overlapping** matches and which pattern each was. `Trie` matches a prefix of the *query* and `SuffixArray` indexes one fixed *text*, so neither can answer this; the BCL's `string.IndexOf` is a single needle per pass and `SearchValues<string>` answers only *where is the first of these*. Against a compiled `Regex` alternation of 256 patterns over 100,000 characters (driven through the allocation-free `Regex.EnumerateMatches`, so the baseline is not charged for a `Match` object per hit): **10.0x** on membership and **4.8x** on enumerating every match; building the automaton is 4.7x constructing that alternation *uncompiled*, which is the only fair build comparison, since `RegexOptions.Compiled` emits IL. ⚠️ **Against the plain k-`IndexOf` loop it wins one shape and loses the other**: 4.1x on counting *present* patterns, but **0.98x — a loss — on ruling 256 *absent* ones out**, because a scan with no candidate to verify stays inside its vectorized sweep and covers many characters per step; at **eight** patterns that loop wins by 5.0x. Break-even is near fifty patterns for counting and several hundred for absent membership. Ordinal, immutable, and the empty pattern throws rather than matching everywhere. |
| **Which of these pending things have timed out** — request and RPC timeouts, connection idle-reaping, lease and session expiry, retry backoff, rate-limiter windows, delayed-message queues | `TimerWheel<TValue>` | **Hierarchical timing wheel**: `O(1)` handle-addressed `Cancel`, amortized-`O(1)` `Schedule`, and an `Advance` bounded by `O(levels x slots + fired + cascaded)` rather than by the ticks it crosses — a jump of a million ticks costs at worst a walk of every slot at every level, which at the default geometry is 1,025 of them. A data structure, not a scheduler: no thread, no clock, no callback. The BCL `PriorityQueue` **cannot cancel** (no `Remove` on `net8.0`; `O(n)` on .NET 9), so the standard workaround is lazy deletion, which grows the heap with timers that will never fire and holds their payloads until they are popped — and cancellation is what this workload is made of. On a round of 100,000 timeouts (schedule, cancel nine in ten, run the clock out) CI measures **6.81x** that heap and **7.09x** `IndexedPriorityQueue`, falling to 2.56x at a thousand. ⚠️ Four trades: the **horizon** is finite (2^32 ticks by default, about 49 days at a millisecond) and a longer delay is rejected; a fired batch comes back in **no particular order**, because a wheel buckets rather than sorts; a round allocates about a fifth *more* than either heap, since a 24-byte entry plus a payload plus a handle costs more per timer than an `(int, long)` pair; and **`Schedule` is an outright loss against the BCL heap — about 2x at 100,000** — because appending to a contiguous array beats writing a scattered slot head and a back-link, though it still beats the addressable heap by 2.71x. At a thousand timers `Cancel` is 2.2x slower (against a "cancel" that removes nothing) and the tick-by-tick drive 1.8x slower — **this is a large-population type**. |
| **Edit a large block of text in the middle** — editor and IDE buffers, template splicing, log and transcript assembly, diff and patch application, streaming transforms that insert out of order | `Rope` | **Balanced tree of bounded character runs**: an edit costs the *depth* of the document rather than its length, so `O(log n + ChunkSize)` — the second term being the shift inside one leaf, which is the caller's own knob and a rounding error at the default 512 — where a `StringBuilder` charges `O(n)` on every `Insert` and `Remove` — it is a chunk list whose head is the *end*, excellent at appending and linear at everything else. Also `Split` and `AppendAndClear`, which cut a document in two and join two back in `O(log n)`; the BCL has no such operation at any cost, only full copies. On a round of 200 scattered insert/remove pairs: **98x** at a million characters, **1.49x** at ten thousand, and **713x** on a hundred split-and-rejoin cycles at a million (186 KB against 400 MB). The editing arms **allocate nothing**, because leaves are filled to three quarters of `ChunkSize` so the ordinary short edit lands in one with room. ⚠️ **Three operations are outright losses and this is not a general `StringBuilder` replacement**: appending is **36.8x slower** (a bounds check and a store, against a tree descent — if appending is all you do, use a `StringBuilder`), random access **7.1x** (a builder constructed from a string is a *single chunk*, so it indexes directly), and `ToString()` 1.68x. Memory is about 2.7 bytes per character against two. Indices are UTF-16 code units, so a cut can split a surrogate pair. |
| **Set algebra over two lists you already hold in sorted order** — intersect / union / diff sorted ID, row-id or posting lists, or just ask how many values they share (inverted indexes, cohort intersection, join-key pre-filters) | `SortedSpan.Intersect` / `Union` / `Except` / `IntersectCount` / `Overlaps` (in `Celerity.Primitives`) | Not a collection — static set algebra over spans. A two-cursor merge exploits the ordering the data already has, so it touches each element once and writes into caller-owned memory: **4.2× faster than `HashSet<int>` at 1M × 1M and 0 bytes allocated against 17.9 MB**, and **257× faster** on the asymmetric 1k × 10M shape where it gallops. `IntersectCount` / `Overlaps` need no buffer at all. ⚠️ **Both spans must be sorted ascending** — unsorted input silently returns a wrong answer. If your data is not already sorted, sorting it first to use this is usually a loss; reach for a set instead. See [sorted-span set algebra](docs/api/utilities.md#sortedspan-sorted-span-set-algebra). |
| **Ranked order** — you need the sorted questions *and* the positional ones on a set that keeps changing: the rank of an element, the k-th smallest, an exact percentile, removal by rank (live leaderboards, exact quantiles over a moving window, a sweep line that needs the median of its active set) | `RankedSet<T>` | Sorted buckets whose capacity tracks `√n` with a Fenwick tree over the bucket lengths, so `IndexOf` is a prefix sum and `set[k]` is one binary-lifting descent plus an array index — both `O(log n)`, with insert and remove at `O(√n)` (one bounded, contiguous memmove of at most a bucket, which still measures **1.5–1.8x faster** than `SortedSet<T>`'s `O(log n)` pointer chase at 100k — buckets are not narrowed as elements leave, so after a bulk removal that bound is the *high-water* `√n` until you call `TrimExcess()`). At 100,000 elements a rank is **9,240x** the `SortedSet<T>` answer and a select **33,900x** — both `O(n)` there — and the churn-plus-query workload is **137x** it and **1.62x** the hand-rolled sorted `List<T>`. **At a thousand elements that hand-roll wins**, so this is a large-set type. **Nothing in .NET is `O(log n)` on both halves**: `SortedSet<T>` has no rank and no indexer at all (`ElementAt(k)` is `O(k)`, counting smaller elements is `O(n)`), while `SortedList<,>` and a hand-rolled sorted `List<T>` index in `O(1)` and memmove half the array on every insert. If the set is built once and only queried, that sorted `List<T>` wins on selection and this type does not pretend otherwise; if the positional questions are never asked, use `BTreeSet<T>`. |
| Need a stable iteration order or multi-threaded access | `BTreeDictionary<,>` / `BTreeSet<>` for sorted order, `Trie<TValue>` for ordered string keys; BCL `ConcurrentDictionary<,>` for concurrency | Celerity is single-threaded, and the **hash-based** collections leave iteration order unspecified. The ordered collections do promise order by contract: the B-trees iterate in comparer order, `Trie<TValue>` in ascending ordinal key order. |

**Celerity is not the right answer when** you need concurrent access (use `ConcurrentDictionary<,>` or your own lock — Celerity is single-threaded), or a guaranteed iteration order from the **hash-based** collections (they implement `IDictionary<,>` / `IReadOnlyDictionary<,>` and `ISet<>` / `IReadOnlySet<>`, but none promises an order across versions). When you do need ordered iteration, reach for the ordered collections instead: `BTreeDictionary<,>` / `BTreeSet<>` iterate in comparer order and support bounds and range scans, and `Trie<TValue>` gives ascending ordinal order over string keys. Interface support is no longer a reason to choose one over another — every mutable dictionary implements `IDictionary<,>` / `IReadOnlyDictionary<,>` and every mutable set implements `ISet<>` / `IReadOnlySet<>`.

## Choosing a hasher

Once the collection is settled, pick a hasher for your key shape. Defaults are good; escalate only with evidence (clustering, adversarial input). The [full hasher matrix](docs/api/hashing.md) documents every option and its tradeoff.

| Key type | Default | When to escalate |
|---|---|---|
| `int` / `long` | `Int32WangNaiveHasher` / `Int64WangNaiveHasher` (built into `IntDictionary` / `LongDictionary`) | Uniform / trusted keys (dense sequential IDs) → *drop* to `Int32IdentityHasher` (the zero-work floor — no mixing, nothing beats it on speed), or `Int64IdentityHasher` **when the low 32 bits are the discriminating ones** — it truncates, so `long` keys carrying a shard tag or timestamp in the high word want `Int64WangNaiveHasher` instead. Clustered keys → `Int32WangHasher` → `Int32Murmur3Hasher` (the Wang full finalizer is a cheaper middle tier than Murmur3). |
| `uint` / `ulong` | `UInt32WangNaiveHasher` (cheap XOR-fold) / `UInt64Murmur3Hasher` (`fmix64`) | Uniform / trusted keys → *drop* to `UInt32IdentityHasher` (the zero-work floor; the cast keeps all 32 bits, so nothing is lost), or `UInt64IdentityHasher` **when the low 32 bits are the discriminating ones** — it truncates, so `ulong` keys carrying a shard tag or timestamp in the high word want `UInt64WangNaiveHasher` instead. `uint`: → `UInt32WangHasher` → `UInt32Murmur3Hasher`. `ulong`: drop to `UInt64WangHasher` / `UInt64WangNaiveHasher` when the `fmix64` multiplies cost more than they buy on uniform keys. |
| `string` (ASCII) | `StringFnV1AHasher` (folds the low byte per char) | Non-ASCII or long keys → `StringFnV1AFullHasher` / `StringFnV1A64Hasher`. Clustered keys → strong-avalanche `StringMurmur3Hasher`, `StringXxHash3Hasher`, etc. |
| `string` (untrusted input) | `DefaultHasher<string>` (BCL Marvin32, per-process-randomized) | A **keyed** PRF — `StringSipHash13Hasher` (Rust's default), `StringSipHash24Hasher`, `StringHalfSipHash24Hasher`, or `StringHighwayHash64Hasher` — but only resists hash-flooding if seeded with a *secret, per-process-random* key; with a fixed seed it is deterministic, not DoS-resistant (see caveat below). |
| `Guid` | `GuidHasher` | — |
| Any other type | `DefaultHasher<T>` (delegates to `EqualityComparer<T>.Default`) | Replace with a hand-written struct hasher if profiling shows `Hash` on the hot path. |

> **Counting past ~10^8 distinct elements? Pick a 64-bit hasher.** The probabilistic sketches (`HyperLogLog`, `BloomFilter`, `CuckooFilter`, `XorFilter`, `CountMinSketch`) never store the element, so two elements that hash alike are indistinguishable forever — and a 32-bit hash reaches only 2^32 ≈ 4.3 billion values, no matter how it is widened. Hashers that carry genuine 64-bit entropy implement **`IHashProvider64<T>`** and the sketches route through it automatically: `Int64WangHasher` / `Int64Murmur3Hasher` (`long`), `UInt64WangHasher` / `UInt64Murmur3Hasher` (`ulong`), `GuidHasher` (`Guid`), and the nine 64-bit `string` hashers (`StringXxHash64Hasher`, `StringXxHash3Hasher`, `StringCityHash64Hasher`, `StringMetroHash64Hasher`, `StringHighwayHash64Hasher`, `StringSipHash13Hasher`, `StringSipHash24Hasher`, `StringFnV1A64Hasher`, `StringFnV164Hasher`). They already computed 64 bits internally and folded them away, so `Hash64` costs nothing extra. With a 32-bit-only hasher the sketches still work — `HyperLogLog` applies the classical large-range correction so its estimate stays honest — but the entropy floor is real. See [`IHashProvider64<T>`](docs/api/hashing.md#ihashprovider64t).

The value of a struct hasher is **distribution quality (avalanche), determinism, and the zero-cost devirtualized generic** — *not* raw hashing speed. For `int` / `uint` keys especially, `GetHashCode()` is already the identity (zero work), so no mixing hasher beats it on speed; the `Int32` / `Int64` / `UInt32` / `UInt64` `IdentityHasher`s expose that zero-work floor explicitly — one per integer width, because the collections invoke the hasher through the `IHashProvider<TKey>` constraint themselves, so there is no call site at which a `uint` key could be cast to reach the `int` one. Skip mixing when keys are already uniform, and escalate to a mixer only when distribution (not speed) demands it.

> **Fixed-seed hashers are not a HashDoS defence.** `string.GetHashCode()` is already a purpose-built **Marvin32** with per-process random seeding; a hardcoded-seed Murmur3 / FNV / xxHash is *not* more flood-resistant — usually **less**, because an attacker who knows the fixed algorithm and seed can precompute colliding keys offline. What stops hash-flooding is a **keyed** PRF with a *secret, per-process-random* key, not merely picking a "stronger" fixed hash. For untrusted `string` keys, the BCL `string.GetHashCode()` (`DefaultHasher<string>`) is the safe default; reach for the keyed SipHash / HighwayHash hashers only when you also supply a secret seed. The fixed-seed hashers' real strength is **reproducibility** (same code across processes and runtimes), which `GetHashCode()` deliberately does not give you.

> **Probing from a `ReadOnlySpan<char>`? Any `String*Hasher` will do.** All 23 implement **`ISpanHashProvider`** — a `Hash(ReadOnlySpan<char>)` overload that returns exactly what `Hash(string)` returns for the same characters — which is what unlocks the [span-keyed lookups](docs/api/collections.md#span-keyed-lookups) and `StringInternTable`. The two overloads share one body, so they cannot drift; a custom string hasher only needs to implement the interface to work the same way. See [`ISpanHashProvider`](docs/api/hashing.md#ispanhashprovider).

The hashing library also ships classic / compatibility hashes (djb2, sdbm, ELF/PJW, CRC-32, Adler-32, FNV-1, MurmurHash2, CityHash, MetroHash, xxHash32/64) for matching an external system's key distribution — see [`docs/api/hashing.md`](docs/api/hashing.md) for the complete list, costs, and avalanche notes, and use `HashQualityEvaluator` (below) to compare candidates on your own keys.

## Benchmarks

**Up to 2.4&times; faster than `Dictionary<int, int>`** on lookups, with zero allocations — this is a *collection-layout* win (open addressing with direct `==` key comparison and no per-call `EqualityComparer<T>` dispatch), **independent of the hasher**. It does *not* mean the hashers beat `GetHashCode()` on speed (they don't, and for `int` cannot — see [Choosing a hasher](#choosing-a-hasher)). The [live dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) tracks every shipped collection against its BCL counterpart on every `main` push, with historical trends and per-PR regression comparisons. For high-precision local numbers, run `dotnet run -c Release` in [`src/Celerity.Benchmarks`](src/Celerity.Benchmarks) — hosted CI runners are noisier than your laptop.

The suite also includes `StringHasherBenchmark` and `IntegerHasherBenchmark` (every built-in hasher bracketed by two baselines — the direct `GetHashCode()` and `EqualityComparer<T>.Default.GetHashCode()`, the per-probe call a BCL `Dictionary<,>` actually makes; rendered under **Hash function throughput** on the dashboard; run locally with `--filter "*HasherBenchmark*"`). Treat these as a **raw-mixing-cost diagnostic only** and read them alongside the distribution metrics from `HashQualityEvaluator` — a fast hasher that clusters is not a win. The isolated `Hash()` number alone is misleading (for `int`, `GetHashCode()` is identity — *zero* work — so no mixer can beat it), so the extended suite adds `HasherEndToEndBenchmark`, which times each hasher **through the dictionary** across all four key shapes, and a deterministic probe-length report (`dotnet run -c Release -- --probe-analysis`) — the cases where a strong hasher "loses" the microbench but wins end-to-end. See [measuring probe length](docs/performance.md#measure-probe-length-not-just-hash-speed).

An **extended local suite** answers the harder questions a single random-key benchmark can't: multiple key distributions (uniform / sequential / clustered / adversarial), million-item scale, allocation profiling, concurrent read scaling, cache locality, mixed read-heavy workloads, and a `FrozenDictionary<,>` comparison. These run on demand — e.g. `dotnet run -c Release -- --filter "*Distribution*"`. See the [extended benchmark suite](docs/performance.md#extended-benchmark-suite).

### Spatial index

`KdTree<TValue>` is one of the two shipped types whose headline depends entirely on **which baseline you pick** (see [Extent index](#extent-index) for the other), so both are measured. At 100,000 uniformly scattered points, 1,000 queries per measurement, taken from CI's same-runner A/B on `ubuntu-latest` — hosted runners are noisy, so treat the ratios as the signal and watch the trend on the [dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/):

| Query | vs. the naive scan | vs. a hand-rolled sorted scan |
| --- | --- | --- |
| **Nearest neighbour** | 103.50 ms → 297 µs — **348x** | 733 µs → 296 µs — **2.5x** |
| **Within a radius** (~0.1% of the domain) | 104.44 ms → 1.98 ms — **53x** | 6.98 ms → 1.98 ms — **3.5x** |
| **10 nearest** | 194.49 ms → 1.81 ms — **107x** | *(baseline is already the smart one: a bounded max-heap scan, heapsorted to the same ascending order)* |

The naive scan is the array-and-a-loop the BCL leaves you with, and against it the tree is one to two orders of magnitude ahead. The second column is the honest one. A caller can sort the points by x, binary-search to the query and work outward, abandoning each direction once the horizontal gap alone exceeds the best distance so far — a real optimization, and effectively a **one-dimensional spatial index**. Against that, the tree wins by a small constant factor, because the second dimension is the only thing it adds.

**At 1,000 points the margin against the hand-roll all but disappears**: 1.4x on the nearest query, and level on the radius query (117 µs against 120 µs, the scan a hair ahead). The tree needs tens of thousands of points before its extra dimension clearly pays for its extra indirection.

**Build is the price**, paid once: 750 µs → 22.2 ms at 100,000 points (**30x** the cost of merely copying the array, at 1.83x its memory). The sorted-scan alternative pays its own `O(n log n)` ordering, so that multiple overstates the gap against the baseline you would actually compare with.

Two further caveats worth stating rather than burying:

- **A k-d tree has no useful worst-case bound.** An adversarial point set makes every query visit every node; the classic `O(log n)` for nearest-neighbour is an average over uniform points, not a guarantee. What is guaranteed is that a query visits at most `n` nodes, so each family stays bounded by the loop it replaces — `O(n)` for the nearest and range queries, and `O(n log k)` for k-nearest, whose per-candidate heap sift the hand-rolled bounded-heap scan pays as well.
- **The data's shape does not just move the ratio, it can reverse it.** Pruning discards subtrees that cannot hold a result, so a query answering with much of the tree converges on the scan. Clustered points — which is what real spatial data usually looks like — are measured as **worse** for the tree, not better: against the sorted scan it goes from 1.92x on uniform points to **0.94x on clustered ones, where the hand-roll wins outright**. The cause is the distance to the query's nearest neighbour rather than density: inside a cluster that neighbour is very close, and a tiny best distance is exactly what lets the scan abandon after a handful of points. `KdTreeShapeBenchmark` in the extended suite carries the measurement, the reasoning, and the sampling bias that made an earlier version of it flatter the tree.

### Moving points

`SpatialGrid<TValue>` answers the workload `KdTree` documents itself out of. The unit is a **frame**: move 10% of the population, then run one radius query per moved entity. Both baselines are hand-rolls, because the BCL has nothing here either — the `Dictionary<(int, int), List<int>>` bucketed grid a competent developer writes, and rebuilding a `KdTree` every frame, which is what this library offered before. At 100,000 entities over a 10,000-unit square, with a 30-unit cell and a 25-unit query radius — the cell sized just over the radius, which is the tuning rule the type documents — taken from CI's same-runner A/B on `ubuntu-latest`:

| Per frame | vs. the bucketed `Dictionary` hand-roll | vs. rebuilding a `KdTree` |
| --- | --- | --- |
| **Move a tenth, then query each** | 9.83 ms → 1.96 ms — **5.0x** | 26.15 ms → 1.96 ms — **13.3x** |
| **Query only, nothing moving** | 8.27 ms → 1.72 ms — **4.8x** | — |
| **Churn a tenth (remove + re-add)** | 1.34 ms → 184 µs — **7.3x** | — |

At 1,000 entities the margins are *wider*, not narrower — 11.9x on the frame — because the cells are emptier still.

**Read the frame number carefully.** The issue that asked for this type set a hard bar of **≥5x or it does not ship**, and 9.83 / 1.96 is **5.02x** — cleared by 0.4%, against a combined run spread of about 1.5% on the two measurements. It passes, and it passes by less than the noise. A development machine said 5.5x; taking CI's number instead of that one is the difference between "comfortably" and "marginally", and marginally is the honest word.

**The margin is a property of how full the cells are, not of the type**, and that belongs next to the headline rather than under it. Both structures walk the same cells and run the same distance test on the same candidates; everything the grid saves is **per cell** — an array index instead of a tuple hash and a bucket probe, an intrusive link instead of a separately allocated `List<T>` — so the ratio is per-cell overhead against per-candidate work, and it collapses as the cells fill:

The shape sweep rides the extended suite, which CI does not run, so unlike the table above these three are development-machine figures — read them against each other rather than against the 5.0x above, which the same machine put at 5.5x:

| Shape at 100,000 entities | Ratio |
| --- | --- |
| **Tight** — ~1 point per cell, ~2 matches per query (the broadphase shape, and the one charted) | **6.5x** |
| **Wide** — ~10 points per cell, ~25 matches, cell still sized to the radius (100 and 90) | **1.12x** |
| **Clustered** — 200 blobs, hundreds of points per cell | **0.52x** — the hand-roll wins |

The clustered row is the one worth reading twice, and it is the opposite of what the class was written to show. The expectation was that clustering would hurt both structures equally, since the baseline is the same grid. It does not: inside a dense cell the layouts genuinely differ. The hand-roll walks a contiguous `List<int>` and issues two independent loads per candidate, which the processor overlaps; the grid walks an intrusive linked list, so each step is a load that must complete before the next address is known — a cell holding five hundred entries is a five-hundred-long chain of dependent cache misses. So the documented failure mode is sharper than "it degrades to a scan": it degrades to a scan the hand-roll does faster.

**And there is no consolation elsewhere in this library — which had to be measured rather than assumed.** An earlier draft of this section sent clustered points to `KdTree`, reasoning that pruning adapts to density where a fixed cell size cannot. That inference chained two unrelated comparisons and nobody had measured the one a caller actually faces, so `SpatialGridShapeBenchmark` now carries a third arm that does. It is not there: on clustered *moving* points a per-frame `KdTree` rebuild measures **24.3 ms against the grid's 24.0 ms** — level — while the hand-roll does the same frame in **12.5 ms**. For heavily clustered points that move, the bucketed `Dictionary` of contiguous lists wins and neither Celerity type helps. (At the other two shapes the grid beats a per-frame rebuild comfortably: **14.9x** tight, **3.2x** wide.)

One more number worth stating plainly because it was predicted otherwise: the issue that asked for this type expected **≥50x** over the per-frame `KdTree` rebuild and called it the easy bar. It is 13.3x. A frame is not only the rebuild — both arms also run 10,000 radius queries, and that shared work sits in the denominator however cheap the index makes it.

### Extent index

`RTree<TValue>` indexes axis-aligned **rectangles** rather than points, which `KdTree` structurally cannot: a box can overlap the query while its centre sits far outside it. Same two-baseline treatment, at 100,000 boxes whose extents span three orders of magnitude, 1,000 queries per measurement, taken from CI's same-runner A/B on `ubuntu-latest`, and the selectivity each query family actually reaches:

| Query | Selectivity | vs. the naive scan | vs. a hand-rolled sorted scan |
| --- | --- | --- | --- |
| **Boxes overlapping a box** | 0.0835% | 433.74 ms → 3.08 ms — **141x** | 29.77 ms → 3.09 ms — **9.6x** |
| **Boxes containing a point** | 0.0050% | 401.29 ms → 1.67 ms — **240x** | 17.78 ms → 1.67 ms — **10.6x** |

**The two rows are not like-for-like**, and the difference is inherent rather than an oversight: the query box is tuned so the overlap query lands on the ~0.1% the kill criterion names, but a point query's answer size is fixed by the extents alone and comes out twenty times more selective, which flatters its ratio. Raising it to 0.1% would need boxes that blanket the map and would drag the overlap arm well past 0.1% in the process. The benchmark fails its own run if either figure drifts out of band.

The second column is the honest one and was the bar set **before** implementation, deliberately at a modest 3x because `KdTree`'s analogous one-dimensional hand-roll came in at a surprising 2.5x. The hand-roll here orders the boxes by `minX`, binary-searches to the query's `minX` less the widest stored box, and scans forward while `minX` stays at or below the query's `maxX` — effectively a **one-dimensional R-tree**, and the second dimension plus the extent hierarchy are the whole of what the tree adds over it.

**At 1,000 boxes the margin against the hand-roll goes negative on one query**: 1.48x on the overlap query, but **0.91x on the point query — the scan wins**. The index has not paid for its indirection at that size, and this is exactly why the repo quotes CI: a development machine put the tree *ahead* on that arm. **Build is the price**, paid once: 436 µs → 62.60 ms at 100,000 boxes (**144x** the cost of merely copying the array) — a sort per level, which is what "Sort-Tile-Recursive" costs.

Two caveats worth stating rather than burying:

- **An R-tree has no useful worst-case bound.** Overlapping node boxes mean a query can descend into several children per level, and an adversarial box set forces it into all of them. What is guaranteed is that a query visits each node and each entry at most once, so every family stays `O(n)` — bounded by the loop it replaces. The ratios track **selectivity**: a query ten times wider narrows them considerably.
- **The received wisdom about shape did not survive being measured.** The standard advice is that an R-tree only earns its keep on extents spanning orders of magnitude, and that uniformly sized boxes belong to a bucketed grid. `RTreeShapeBenchmark` in the extended suite gives the grid an arm of its own rather than inferring it, and the tree comes out **1.30x** ahead of the grid on the varying shape and **3.01x** ahead on the uniform one — its widest margin is on the shape it was expected to lose. An R-tree's node boxes get tighter as extents get more alike, so the query settles in a shorter descent, while the grid barely moves between the two shapes. Both shapes hold the same mean extent, so the control varies only the spread; an earlier version used the geometric rather than arithmetic mean and was confounded by 21x less box area, which review caught. The qualification is that a grid's cell size is a tuning knob and this one is sized by the data rather than the query; the real reason to reach for a grid is that it is **mutable** and this type is not. The same benchmark also settles the packing choice: sort-tile is 1.04x ahead of a Hilbert order on varying extents and 1.21x ahead on uniform ones.

## Custom hashing

Implement `IHashProvider<T>` as a **struct** (required by `where THasher : struct, IHashProvider<T>`) so the JIT can devirtualize and inline `Hash()`:

```csharp
public interface IHashProvider<T>
{
    int Hash(T key);
}
```

For the probabilistic sketches there is a 64-bit sibling. Implement it alongside when your hasher genuinely carries 64 bits of entropy — see [`IHashProvider64<T>`](docs/api/hashing.md#ihashprovider64t) for the contract and why widening a 32-bit code does not qualify:

```csharp
public interface IHashProvider64<T>
{
    ulong Hash64(T key);
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

And **`SortedSpan`** is set algebra over spans that are **already sorted ascending** — `Intersect` / `Union` / `Except` straight into a caller-owned `Span<T>`, plus `IntersectCount` and `Overlaps` that need no buffer and **allocate nothing at all**. The BCL has none of this: `MemoryExtensions` has no set operation and `TensorPrimitives` none either, so the alternatives are `HashSet<T>.IntersectWith` (allocate a table, then hash and probe every element) or LINQ `Intersect` — **neither exploits sortedness**. A two-cursor merge touches each element once instead: intersecting two 1M-element sorted `int` arrays runs at **6.1 ms vs 25.7 ms** for `HashSet<int>` (**4.2×**, and 5.7× vs LINQ) while allocating **0 bytes against 17.9 MB**. When one side is ≥32× the other it gallops (exponential search), which is where the win gets large: **1k against 10M** takes **0.37 ms vs 94.3 ms** — **257×**. ⚠️ **Sorted by construction, or this is worthless**: unsorted input silently returns a wrong answer (Debug builds assert; Release deliberately does not check).

```csharp
Span<int> buffer = stackalloc int[Math.Min(a.Length, b.Length)];
int n = SortedSpan.Intersect(a, b, buffer);   // a, b sorted ascending; result in buffer[..n]
bool any = SortedSpan.Overlaps(a, b);         // allocation-free, early-exit
```

Finally, **`MortonCurve`** and **`HilbertCurve`** are the **space-filling curves** — map a 2-D or 3-D integer coordinate to one `ulong` whose ordering keeps nearby points nearby, and back. This is the primitive that lets a *one-dimensional* structure answer a spatially local question: sort a point set by its curve key and a plain array, a `BTreeSet<T>` or a `SortedSpan` becomes a cache-coherent spatial container; it is also the standard packing order for a bounding-volume index and the standard way to build a tile key that survives a sort or a hash-partition. **The BCL has neither** — `BitOperations` ships popcount and rotates but no bit-interleave, and there is no Hilbert anything — so today a caller writes the bit-twiddling themselves, and the shipped spread runs **9.8×** the bit-by-bit interleave that is the obvious way to write it by hand. **Morton** is the cheap default: a straight-line bit spread that keeps each axis monotone. **Hilbert** costs **43×** more per conversion (a loop over the bit levels) and buys the one property Morton cannot give — **consecutive indices are always neighbouring cells**, at every scale — which is what you want when the ordering itself backs a range query rather than just a sort. The payoff is the layout, not the conversion: sorting a 2 M-point set by its curve key made an identical batch of randomly-located neighbourhood queries **1.8× faster** than the same points unsorted — but only **1.19×** against the row-major cell key a caller would otherwise sort by, which is the number to judge it on, and **nothing at all** at a size that fits in cache — that control ships as a benchmark parameter rather than a footnote. 2-D is lossless over both `uint` axes; 3-D packs three axes into the same 64 bits, so it is capped at 21 bits each and `Encode3D` throws above that rather than silently masking.

```csharp
// One key per point, then sort the two together: neighbours in space become neighbours in memory.
ulong[] keys = points.Select(p => MortonCurve.Encode2D(p.CellX, p.CellY)).ToArray();
Array.Sort(keys, points);

ulong index = HilbertCurve.Encode2D(cellX, cellY);
if (index < ulong.MaxValue)                         // the last index has no successor — it wraps to 0
{
    var (nx, ny) = HilbertCurve.Decode2D(index + 1);  // Hilbert: one cell away, along one axis
}
```

See [`docs/api/utilities.md`](docs/api/utilities.md#fastmod--fastdiv) for the full surface and the generator-selection table.

## Sorting

The **`Celerity.Sorting`** package fills the one gap the BCL structurally *cannot* close. `Array.Sort` and `MemoryExtensions.Sort<T>` route through a scalar **comparison** introsort for primitive keys on every current runtime — there is no radix, counting, or selection path anywhere in the BCL — and `Array.Sort` is contractually **in-place**, while a radix sort needs `O(n)` scratch. Trading that in-place guarantee for a scratch buffer is exactly the flexibility-for-speed deal Celerity exists to make.

```bash
dotnet add package Celerity.Sorting
```

| Type | What it gives you |
|---|---|
| `RadixSort` | LSD radix over `uint` / `int` / `ulong` / `long` / `float` / `double`: four (32-bit) or eight (64-bit) counting passes with purely sequential reads and **no comparisons and no data-dependent branches**, where introsort's every partition step is a branch the predictor cannot learn on random data. Keys alone, keys with a parallel payload, or `ArgSort` — the index permutation that ranks without moving a wide payload (the BCL has no argsort). **Stable**, unlike `Array.Sort(keys, items)`. |
| `CountingSort` | Bounded key ranges — `byte`, `ushort`, or `int` over a declared `[min, max]`: one histogram pass and one run-fill, `O(n + range)`, for the shape that enum ordinals, bucket ids and quantized scores take. The keys-only forms never move an element twice and allocate nothing at all for `byte` keys. **Stable** with a payload. |
| `PartialSort` | Selection instead of sorting: `Select` / `Sort` are an `O(n)` in-place introselect for the *k* smallest, `TopK` an `O(n log k)` bounded heap over a span it never writes to. A three-way partition keeps duplicate-heavy input linear; a depth budget bounds the adversarial case. |

`RadixSort` and `CountingSort` each pair their entry points with a **`SortWithScratch` twin that allocates nothing**, so a hot loop supplies its buffers once instead of renting per call. `PartialSort` needs no scratch and allocates nothing in any form:

```csharp
using Celerity.Sorting;

int[] ids = LoadIds();
RadixSort.Sort(ids.AsSpan());                       // rents its scratch

int[] scratch = new int[ids.Length];                // ...or supply it once
foreach (var batch in batches)
{
    batch.CopyTo(ids);
    RadixSort.SortWithScratch(ids.AsSpan(), scratch.AsSpan());
}

int[] worst = new int[10];                          // top 10 of a million,
PartialSort.TopK<int>(latencies, worst);            // without reordering the source
```

**Where it does not win, stated plainly.** `RadixSort` **loses below a few hundred elements** — the histogram's fixed cost dominates, so use `Array.Sort` for small spans; the [benchmark dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) sweeps from 100 to 1,000,000 elements precisely so the crossover is a measured number rather than a guess. `CountingSort` loses once `range` approaches `n`. And `PartialSort` is **not** asymptotically better than LINQ: `OrderBy().Take(k)` has applied its own partial-sort optimization since .NET 6, so the win there is allocation and boxing, not complexity. One more thing to know before sorting reals: `RadixSort` orders `NaN` by sign bit and `-0.0` before `+0.0`, where `Array.Sort` moves all NaNs to the front and calls the zeros equal. Full details in the [sorting API reference](docs/api/sorting.md).

## Streaming statistics — `Celerity.Statistics`

The **`Celerity.Statistics`** package covers a gap the BCL never filled at all: summarizing a stream you cannot hold. `System.Linq` gives you `Average`, `Sum`, `Min` and `Max`, and stops. There is no quantile type, no sampler, no variance, no higher moment, and no accumulator that can be fed one value at a time — so the honest alternative to each of these is a `List<T>` holding the whole stream.

```bash
dotnet add package Celerity.Statistics
```

```csharp
using Celerity.Statistics;

// Quantiles with a relative error bound, in bounded memory.
var latencies = new DDSketch(relativeAccuracy: 0.01);
foreach (double ms in requestLatencies)
    latencies.Add(ms);

Console.WriteLine($"p99 {latencies.GetQuantile(0.99):F2} ms");

foreach (DDSketch shard in perShardSketches)   // bucket-exact cross-shard merge
    latencies.Merge(shard);

// A 1,000-item uniform sample of a stream of unknown length.
var sample = new ReservoirSampler<string>(capacity: 1_000, seed: 42);
foreach (string line in logLines)
    sample.Add(line);

// Single-pass, allocation-free moments — and an array element is already a ref.
var perEndpoint = new RunningStatistics[endpointCount];
perEndpoint[endpoint].Add(latencyMs);
```

**Where it does not win, stated plainly.** `DDSketch.Add` is **slower than a `List<double>` append** — appending is a bounds check and a store, and the sketch pays a `log()` and a `ceil()` on top of its own — and if your data is static and fits in memory, a **pre-sorted array indexed in `O(1)` beats the sketch by a wide margin**. Both arms are on the [dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) rather than left out. What the sketch buys is the query on a stream that keeps moving, and a footprint that does not grow with the sample count; when its bin budget runs out it collapses the *lowest* buckets and says so through `HasCollapsed`, because a guarantee that has quietly stopped holding is worse than none. `ReservoirSampler` deliberately ships **without a `Merge`**: a uniform merge needs a hypergeometric draw over the two stream lengths, and replaying one side into the other over-weights the shorter one. And `RunningStatistics` buys **correctness, not speed**: CI measures it at **1.9× the two-pass LINQ shape**, because it maintains all four moments on every `Add`. What it buys is a single pass over a stream it never retains, and an answer the one-pass `sum` / `sumOfSquares` shortcut — cheaper still — gets catastrophically wrong at `1e10 ± 6`. `ReservoirSampler` has its own crossover: **6.2× slower** than materializing at a thousand items, **1.7× faster** at a hundred thousand. Full details in the [statistics API reference](docs/api/statistics.md).

## Native AOT & trimming

Celerity is **Native AOT and trimming compatible** — no reflection, runtime code generation, or dynamic type loading. Every collection is a generic over a struct hasher, and the only BCL primitives on the hot paths (`MemoryMarshal`, `Unsafe`, `EqualityComparer<T>.Default`) are AOT-safe. The assembly is marked [`<IsAotCompatible>true</IsAotCompatible>`](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#aot-compatibility-analyzers), so a `PublishAot` app gets **no trim or AOT warnings**. Compatibility is enforced on every build (the trim/AOT analyzers run during compilation) and CI publishes a Native AOT smoke-test binary exercising every collection and hasher. See [`docs/aot.md`](docs/aot.md).

## API at a glance

The dictionaries mirror the parts of `Dictionary<TKey, TValue>` most callers reach for: indexer get/set, `ContainsKey`, `TryGetValue`, `Add`, `TryAdd`, `Remove` (both overloads), `Clear`, `EnsureCapacity` / `TrimExcess`, `Count`, `Keys`, `Values`, `GetEnumerator()`. The string-keyed types additionally take a `ReadOnlySpan<char>` on `TryGetValue` / `ContainsKey` / `Contains`, so a caller holding a slice of a buffer never allocates a `string` to probe. They implement `IDictionary<TKey, TValue?>` and `IReadOnlyDictionary<TKey, TValue?>` — the `Keys` / `Values` views widen to read-only `ICollection<T>`s through the mutable interface, whose mutators throw `NotSupportedException` exactly as `Dictionary<,>.KeyCollection` does — and accept an `IEnumerable<KeyValuePair<TKey, TValue>>` at construction. The sets expose `Add`, `TryAdd`, `Contains`, `Remove`, `Clear`, `EnsureCapacity` / `TrimExcess`, `Count`, and a struct enumerator. `EnsureCapacity(n)` pre-grows the table once for a known-size bulk insert (no incremental rehashes); `TrimExcess()` rehashes back down to fit `Count`. The zero / `default(TKey)` key (or element) is stored out-of-band so it never collides with the empty-slot sentinel.

Full constructors, signatures, exceptions, and per-type examples: **[API reference](docs/README.md)**.

## Project docs

- [`docs/`](docs/README.md) — documentation index & [API reference](docs/README.md#api-reference).
- [Sorting API](docs/api/sorting.md) · [Statistics API](docs/api/statistics.md) · [Performance tuning](docs/performance.md) · [Migration guide](docs/migration.md) · [Troubleshooting](docs/troubleshooting.md) · [FAQ](docs/faq.md) · [Testing & coverage](docs/testing.md).
- [`ROADMAP.md`](ROADMAP.md) · [`CHANGELOG.md`](CHANGELOG.md) · [`CONTRIBUTING.md`](CONTRIBUTING.md) · [GitHub Issues](https://github.com/marius-bughiu/Celerity/issues).
