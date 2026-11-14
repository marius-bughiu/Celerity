# Collections guide

Every collection in the `Celerity.Collections` package, grouped by family. Each family lists its types and the tradeoff each one buys, then walks through the main ones with worked examples. Looking for the type that fits a workload? Start from [Choosing a collection](choosing-a-collection.md). For every constructor, member and exception of every type, see the [API reference](api/collections.md).

## Contents

- [Dictionaries](#dictionaries)
- [Sets](#sets)
- [Caches](#caches)
- [Sequences](#sequences)
- [Union-find](#union-find)
- [Priority queues](#priority-queues)
- [Prefix trees](#prefix-trees)
- [Span-keyed string lookups](#span-keyed-string-lookups)
- [Sorted (ordered) collections](#sorted-ordered-collections)
- [Range aggregates](#range-aggregates)
- [Spatial & interval indexes](#spatial--interval-indexes)
- [Graphs](#graphs)
- [Text](#text)
- [Timers](#timers)
- [Probabilistic & bit-level](#probabilistic--bit-level)

## Dictionaries

- `CelerityDictionary<TKey, TValue, THasher>` — the generic baseline: open-addressed dictionary with a struct hasher constraint.
- `RobinHoodDictionary<TKey, TValue, THasher>` — Robin Hood probing bounds probe-length variance, keeping worst-case lookups close to average on clustered / adversarial keys (cost: a per-slot probe-distance `int`).
- `SwissDictionary<TKey, TValue, THasher>` — Swiss-table SIMD group probing: one `Vector128` compare tests 16 slots per lookup, filtered by a 7-bit hash tag (cost: one control byte per slot). For lookup-heavy tables.
- `HashCachingDictionary<TKey, TValue, THasher>` — struct-of-arrays layout: a dense side array of 32-bit hash fingerprints lets probes scan metadata only and skip expensive key equality on a single integer compare (cost: four bytes per slot). For costly-equality keys.
- `PooledCelerityDictionary<TKey, TValue, THasher>` — backing arrays rented from `ArrayPool<T>.Shared` and returned on `Dispose`, cutting GC pressure for short-lived, frequently-rebuilt dictionaries. Same API plus `IDisposable`.
- `LinkedDictionary<TKey, TValue, THasher>` — **insertion-ordered** dictionary (the .NET `LinkedHashMap` / `OrderedDict`): enumerates oldest first and keeps that order through removals, with `O(1)` `Remove`, `MoveToEnd` / `MoveToFront`, and first/last access (`TryRemoveFirst` drains it in FIFO order). .NET 9's `OrderedDictionary` removes in `O(n)`, and `Dictionary` reuses freed slots, so its order breaks at the first removal. No index access (cost: two `int` order links and a cached hash per entry).
- `BiDictionary<TKey, TValue, TKeyHasher, TValueHasher>` — **one-to-one** dictionary (the .NET `BiMap` / `BidiMap`): `O(1)` lookup by key *and* by value (`TryGetKey`, `ContainsValue`, `RemoveValue`, an `Inverse` view), with value uniqueness enforced on every write. One table instead of two hand-synced `Dictionary` instances, at 0.6–0.75x their memory (cost: removal re-hashes, since no hash code is cached).
- `FrozenCelerityDictionary<TValue>` / `<TValue, THasher>` — build-once, read-many `string`-keyed dictionary: a perfect (collision-free) hash for small key sets, a half-full linear-probing table past that. Looks keys up slower than `FrozenDictionary`; builds faster past a few hundred keys.
- `CelerityMultiMap<TKey, TValue, THasher>` — one-to-many map: `Add` appends instead of overwriting. Implements `ILookup<TKey, TValue?>`.
- `CelerityMultiSet<T, THasher>` — counting multiset (bag): each element maps to its multiplicity. Single-probe `Add`-increment for frequency counting, vs the two-probe `Dictionary<T,int>` idiom. `Count` (distinct) / `TotalCount` (occurrences).
- `SmallDictionary<TKey, TValue>` — flat-array, linear-scan dictionary for the very-small (`n <= ~16`) case. No hasher; the default key is stored inline.
- `EnumMap<TEnum, TValue>` — dense array-backed dictionary for **enum** keys (the .NET `EnumMap`): a lookup is a direct array index — no hashing, no probing, no collisions. Enumerates in ascending underlying-value order. The dictionary counterpart of `EnumSet`.
- `SparseMap<TValue>` — bounded-universe **integer-keyed** dictionary (Briggs–Torczon sparse map): a dense key array and a parallel value array, so a lookup is a direct array index and iteration is a contiguous scan over exactly the present entries. `Clear` leaves the key and sparse arrays untouched — for clear-and-rebuild side tables over ids in `[0, N)` (graph traversal, ECS, sweep-line). At 100,000 entries over a 4x universe: lookup **3.1x** `Dictionary<int, V>`, remove **3.6x**, clear-and-rebuild **2.4x** (`Clear` alone ~500x), fill **2.0x**; ⚠️ enumeration is only 1.13x, it allocates 1.10x (the `O(Universe)` sparse array), and at 1,000 entries the fill is 17% *slower*. The dictionary counterpart of `SparseSet`.
- `SlotMap<TValue>` — **generational slot map**: `Add` returns a stable `SlotKey` (slot + version) and a lookup is one array index and one version check — no hash. A removed key stays absent even after its slot is reused (until that slot has been reused 2³¹ times), closing the ABA hazard of a `List` plus a free list. Against the `Dictionary<int, V>`-plus-counter hand-roll at 100,000 values: lookup **1.90x**, remove **1.92x**, remove-and-replace churn **1.46x**, fill 1.38x at 0.55x the allocation; enumeration is a wash. For entity tables, object pools and anything else addressed by handle.
- `IntDictionary<TValue>` / `LongDictionary<TValue>` — `int` / `long`-keyed specializations (default to `Int32WangNaiveHasher` / `Int64WangNaiveHasher`).
- `PersistentHashMap<TKey, TValue, THasher>` — **immutable** hash map over a **CHAMP** trie (the refinement of Clojure's HAMT that Scala's `HashMap` adopted in 2.13): a single-key edit returns a new map **sharing** all but one root-to-leaf path of the old one's storage, a write that changes nothing hands back the receiver, and the bulk methods reach as many branches as their keys do. Branching factor 32 with entries inline in flat arrays and a slot found by popcount, against `ImmutableDictionary<,>`'s AVL tree of branching factor two with a heap node per entry — so a lookup is a few array reads rather than a pointer chase per level. For state read far more often than it is written and still handed to readers as a snapshot. Implements `IReadOnlyDictionary<TKey, TValue?>`.
- `PersistentSortedMap<TKey, TValue, TComparer>` — **immutable sorted** map over a path-copying **B+ tree** of fan-out 32: a single-key edit returns a new map **sharing** all but one root-to-leaf path, and it answers the ordered queries `ImmutableSortedDictionary<,>` has none of — `Min` / `Max`, lower and upper bound, floor and predecessor, and `EnumerateRange` — in `O(log n)`. A lower-bound seek at 100,000 entries is **3.1x** the BCL's only `O(log n)` route, a key-ordered `ImmutableSortedSet` of the pairs, which has no range view. At 100,000 entries it also enumerates **3.9x**, inserts **1.85x** and looks up **1.29x** faster; ⚠️ at 1,000 entries a lookup is 1.40x *slower*, and writes allocate 1.23–1.36x the bytes. For sorted state handed to readers as a snapshot: a price ladder, a version history queried "as of *t*", a range table. Implements `IReadOnlyDictionary<TKey, TValue?>`.

The mutable dictionaries implement **both** `IDictionary<TKey, TValue?>` and `IReadOnlyDictionary<TKey, TValue?>`, so they drop into an existing API taking either BCL interface; the immutable `FrozenCelerityDictionary` and the prefix-tree `Trie<TValue>` implement the read-only one only. All of them ship allocation-free struct enumerators, `Keys` / `Values` views, and an `IEnumerable<KeyValuePair<TKey, TValue>>` constructor. The open-addressed hash tables store `default(TKey)` (zero / `null`) out-of-band so it never collides with the empty-slot sentinel. `SmallDictionary` stores it inline, and the chained `LinkedDictionary` / `BiDictionary` keep it in the table, so it holds its place in `LinkedDictionary`'s order.

### Examples

The two everyday dictionaries, `IntDictionary<TValue>` and `CelerityDictionary<TKey, TValue, THasher>`, are walked through in [Getting started](getting-started.md#first-steps), which also shows how to [build one from an existing collection](getting-started.md#constructing-from-an-existing-collection).

#### Specialized dictionaries — RobinHood, Swiss, HashCaching, Pooled, Linked, Bi, Frozen, MultiMap, MultiSet, Small, EnumMap, SparseMap, SlotMap

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
// forgetting is not a leak, just no pooling benefit. After Dispose, members that
// read or write the contents throw ObjectDisposedException.
using var pooled = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
pooled[42] = "hello";
```

`LinkedDictionary<TKey, TValue, THasher>` has the same API as `CelerityDictionary` and adds an order guarantee: entries enumerate oldest first, and the order survives removals. `Remove` is `O(1)`, where .NET 9's `OrderedDictionary` shifts its dense array in `O(n)`, and `Dictionary` reuses the freed slot, so the next new key appears in the removed one's place. Overwriting an existing key's value keeps its position. `MoveToEnd` / `MoveToFront` reorder it.

```csharp
var pending = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
pending.Add(17, "resize image");
pending.Add(4, "send email");
pending.Add(9, "rebuild index");
pending.Remove(4);            // O(1); 17 and 9 keep their order
pending.MoveToFront(9);       // escalate one job

while (pending.TryRemoveFirst(out int id, out string? job))
    Console.WriteLine($"{id}: {job}"); // 9, then 17
```

`BiDictionary<TKey, TValue, TKeyHasher, TValueHasher>` is a one-to-one map you can look up from either side in `O(1)`, so it replaces the forward and inverse `Dictionary` pair kept in sync by hand. Values are unique like keys: a write that would give a value a second key throws, and `ForceSet` moves the value instead.

```csharp
var columns = new BiDictionary<string, int, StringFnV1AHasher, Int32IdentityHasher>();
columns.Add("id", 0);
columns.Add("email", 1);
int ordinal = columns["email"];      // 1
string name = columns.GetKey(0);     // "id", O(1)
columns.ForceSet("created", 0);      // moves 0 to "created", removing "id"
```

`FrozenCelerityDictionary<TValue>` is build-once, read-many. A set of up to about 100 keys gets a perfect (collision-free) table, so each lookup is single-probe; larger sets mostly get a linear-probing table at most half full. Immutable; implements `IReadOnlyDictionary<string, TValue?>`. `FrozenDictionary` looks keys up 3.5–5.6x faster at 1,000 to 100,000 keys, because it hashes only a short substring that tells the keys apart. This type builds about 1.8x faster at those sizes (slower for most sets below 334 keys, where it searches for a perfect table), takes your hasher through the `<TValue, THasher>` overload (e.g. `StringFnV1AFullHasher` for non-ASCII keys), allows a `null` key, and rejects duplicate keys.

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

`SparseMap<TValue>` is the same idea for **non-negative integer** keys over a universe fixed at construction — the dictionary counterpart of `SparseSet`, and the Briggs–Torczon sparse map: a dense key array, a parallel dense value array, and a sparse index array pointing each present key at its dense slot. Lookup, insert and removal are `O(1)` with no hashing, and the point of the type is what `Dictionary<int, TValue>` can't match: `Clear()` leaves the key and sparse arrays exactly as they are (a stale sparse entry is rejected by the round-trip check), and enumeration is a contiguous scan over exactly the present entries. That is the winning shape for side tables a traversal rebuilds every pass — BFS distances, ECS components, sweep-line state, where at 100,000 entries lookup runs **3.1x** `Dictionary<int, V>`, removal **3.6x** and clear-and-rebuild **2.4x** (the `Clear` itself ~500x, though the refill it precedes is what dominates the 2.4x). The costs are published with the wins: the `O(Universe)` sparse array puts allocation at 1.10x `Dictionary`'s, enumeration is only 1.13x (a near-wash — both sides are memory-bound), and at 1,000 entries filling the map is 17% *slower*, because the fixed sparse-array cost has not been amortized yet. Pre-size with `EnsureCapacity` before a bulk fill — the universe constructor sizes only the sparse index array. Keys are non-negative-only: one outside `[0, Universe)` throws on the write surface and reads as absent on the read surface. And for a `TValue` that holds a reference, `Clear()` also clears the dense value prefix so nothing is retained — `O(Count)`, against `Dictionary<,>`'s whole bucket array.

```csharp
var distance = new SparseMap<int>(nodeCount);   // universe = ids in [0, nodeCount)

distance.Clear();                                // key + sparse arrays untouched
distance[start] = 0;
if (distance.TryAdd(neighbour, 1)) { /* first time this node was reached */ }

foreach (var (node, hops) in distance)   // struct enumerator, no allocation
{
    // a contiguous scan over exactly the reached nodes
}
```

`SlotMap<TValue>` turns the question around: the caller does not have a key, the map **issues** one. `Add` returns a `SlotKey` — a slot index plus a version — and the version is stepped every time the slot is filled or vacated, so a key retired by `Remove` or `Clear` stays absent even after its slot is reused — until that one slot has been reused 2³¹ times, the ceiling noted below. That is the ABA hazard a `List<T>` plus a free list of indices has, closed; the other hand-roll, a `Dictionary<int, T>` keyed by a counter, is safe but pays a hash, a bucket load and an entry load per access, where a slot map pays one load. At 100,000 values with handles visited in shuffled order, lookup runs **1.90x** that hand-roll, removal **1.92x**, remove-and-replace churn **1.46x**, and the fill 1.38x at 0.55x the allocation. Enumeration is a wash (1.03x); at 1,000 values the fill is a tie. Both pre-registered bars were missed narrowly (2x lookup, 1.5x churn) and the type ships on the BCL gap — there is no stable-handle container in .NET to beat. The slot array never shrinks, and versions are 32-bit, so a key retired 2³¹ reuses of the same slot ago resolves again.

```csharp
var entities = new SlotMap<Entity>();
SlotKey goblin = entities.Add(new Entity("goblin"));

entities.Remove(goblin);
SlotKey loot = entities.Add(new Entity("loot"));   // reuses the slot under a new version

Console.WriteLine(entities.ContainsKey(goblin));   // False — the stale handle does not see the loot
```

#### An immutable map — PersistentHashMap

`PersistentHashMap<TKey, TValue, THasher>` is the **immutable** map, and the associative half of the lane `PersistentVector<T>` opened. .NET ships exactly one — `ImmutableDictionary<TKey, TValue>` — and it is an **AVL tree keyed by hash code** with one heap node per entry carrying key, value, hash, two children and a height field, so its branching factor is two and every lookup is a pointer chase per level over nodes scattered across the heap. This is a **CHAMP** trie (Steindorfer & Vinju, OOPSLA 2015 — the refinement of the hash-array mapped trie behind Clojure's `PersistentHashMap`, which interleaves entries and sub-nodes under one bitmap; CHAMP is what Scala's `HashMap` adopted in 2.13): a node reads five bits of the hash, so the branching factor is 32 and 100,000 entries are four levels deep rather than seventeen, with entries inline in flat arrays and the slot found by a popcount over a 32-bit occupancy map.

Reach for it when a map is **read far more often than it is written but still has to change** — a config or feature-flag set swapped atomically and read on every request, a symbol table threaded through a compiler pass, an interpreter environment, per-version state handed to readers on another thread. At 100,000 entries it looks up **10.6x** faster than `ImmutableDictionary<int, string>`, inserts **2.1x** faster and retains about **20% less** memory. Memory is the weakest of those figures and is stated as such in the reference: the read is the reason to reach for this type. If the map never changes after it is built, `FrozenCelerityDictionary` or a plain `Dictionary<,>` is faster still; if nobody holds an old version, structural sharing buys nothing and `CelerityDictionary` writes one slot where this allocates a path. See [the API reference](api/collections.md#persistenthashmaptkey-tvalue-thasher).

```csharp
PersistentHashMap<string, bool, StringXxHash3Hasher> flags =
    PersistentHashMap<string, bool, StringXxHash3Hasher>.Empty
        .Add("new-checkout", false)
        .Add("dark-mode", true);

PersistentHashMap<string, bool, StringXxHash3Hasher> snapshot = flags; // hand to another thread
flags = flags.SetItem("new-checkout", true);  // snapshot still reads false

bool live = flags["new-checkout"];            // True
bool asPublished = snapshot["new-checkout"];  // False
```

#### An immutable sorted map — PersistentSortedMap

`PersistentSortedMap<TKey, TValue, TComparer>` is the sorted sibling of `PersistentHashMap` and the immutable sibling of `BTreeDictionary`. .NET's only immutable sorted map, `ImmutableSortedDictionary<TKey, TValue>`, is an **AVL tree** with one heap node per entry, and it answers only exact questions: it has no `Min` / `Max`, no floor or ceiling and no range view, so "the first key at or after *x*" on it is an `O(n)` scan, and the BCL's `O(log n)` route is a different type, a key-ordered `ImmutableSortedSet` of the pairs. This is a **B+ tree of fan-out 32** with path copying: entries sit in flat 32-wide leaf arrays, an edit copies the four or so nodes on one root-to-leaf path and shares the rest, and every ordered query is one `O(log n)` descent.

Reach for it when sorted state is **handed to readers as a snapshot and queried by position in the order** — a price ladder or order book republished on every change, a version history queried "as of *t*" with `TryGetFloor`, a range or route table swapped atomically. A lower-bound seek at 100,000 entries is **3.1x** that `ImmutableSortedSet` workaround; enumeration is **3.9x** and insertion **1.85x** faster than `ImmutableSortedDictionary`. The shared operations are a mixed result at small sizes, and that is stated rather than rounded away: at 1,000 entries a lookup is **1.40x slower** than the AVL tree, and writes allocate 1.23–1.36x more bytes everywhere. If nobody holds an old version, `BTreeDictionary` writes in place; if you never need order, `PersistentHashMap` is faster. See [the API reference](api/collections.md#persistentsortedmaptkey-tvalue-tcomparer).

```csharp
PersistentSortedMap<long, string, DefaultComparer<long>> versions =
    PersistentSortedMap<long, string, DefaultComparer<long>>.Empty
        .Add(1_000, "v1")
        .Add(2_000, "v2");

PersistentSortedMap<long, string, DefaultComparer<long>> snapshot = versions; // hand to a reader
versions = versions.Add(3_000, "v3");         // snapshot is untouched

versions.TryGetFloor(2_500, out var asOf);    // (2000, "v2") — the value as of t = 2500
foreach (var entry in versions.EnumerateRange(1_500, 3_500))
    Console.WriteLine(entry.Key);             // 2000, 3000
```

## Sets

- `CeleritySet<T, THasher>` — generic set counterpart to `CelerityDictionary`.
- `SwissSet<T, THasher>` — Swiss-table SIMD group probing for sets: one `Vector128` compare tests 16 slots per membership check, filtered by a 7-bit hash tag (cost: one control byte per slot). For membership-heavy sets with many negative lookups. The set counterpart of `SwissDictionary`.
- `RobinHoodSet<T, THasher>` — Robin Hood probing for sets: bounds probe-length variance and lets negative `Contains` lookups exit early on clustered / adversarial elements (cost: a per-slot probe-distance `int`). The set counterpart of `RobinHoodDictionary`.
- `HashCachingSet<T, THasher>` — struct-of-arrays layout for sets: a dense side array of 32-bit hash fingerprints lets probes scan metadata only and short-circuit expensive element equality on a single integer compare (cost: four bytes per slot). For lookup-heavy sets and costly-equality elements. The set counterpart of `HashCachingDictionary`.
- `PooledCeleritySet<T, THasher>` — backing array rented from `ArrayPool<T>.Shared` and returned on `Dispose`, cutting GC pressure for short-lived, frequently-rebuilt sets. Same API plus `IDisposable`. The set counterpart of `PooledCelerityDictionary`.
- `LinkedSet<T, THasher>` — **insertion-ordered** set (the .NET `LinkedHashSet`): enumerates oldest first and keeps that order through removals, with `O(1)` `Remove`, `MoveToEnd` / `MoveToFront`, and first/last access (`TryRemoveFirst` drains it in FIFO order). `HashSet`'s order breaks at the first removal and .NET ships no ordered-by-arrival set. No index access (cost: two `int` order links and a cached hash per element). The set counterpart of `LinkedDictionary`.
- `FrozenCeleritySet` / `<THasher>` — build-once, read-many `string` set with `FrozenCelerityDictionary`'s layout. Implements `IReadOnlySet<string>`.
- `IntSet` / `LongSet` — `int` / `long`-keyed set specializations.
- `SmallSet<T>` — flat-array, linear-scan set for the very-small (`n <= ~16`) case. No hasher; the default element is stored inline. The set counterpart of `SmallDictionary`.
- `EnumSet<TEnum>` — bit-vector set for enum keys (the .NET `EnumSet`): membership is a single bit test and set algebra is word-wise bitwise ops, with no hashing or boxing. Enumerates in ascending underlying-value order.
- `SparseSet` — bounded-universe integer set (Briggs–Torczon sparse set): `O(1)` `Clear` that leaves the backing arrays untouched, plus dense, cache-friendly iteration — for clear-and-rebuild "visited" sets over ids in `[0, N)` (graph traversal, ECS, sweep-line). Costs `O(Universe)` memory.
- `HierarchicalBitSet` — **ordered** bounded-universe integer set: a bit vector under a 64-ary tree of summary words, so `TryGetMin` / `TryGetMax`, successor (`TryGetLowerBound` / `TryGetUpperBound`) and predecessor (`TryGetFloor` / `TryGetPredecessor`) read a few words — `O(log₆₄ Universe)` — and enumeration is ascending. At 100,000 values in an 800,000 universe it answers a successor query **32x** faster than `SortedSet<int>` and **18.9x** faster than `BTreeSet<int>`, at 39x less memory. For lowest-free-id allocators, next-occupied-slot scans and integer priority queues that need `Remove`. Costs about `Universe / 8` bytes.
- `CompressedIntSet` — **exact, compressed** set of 32-bit integers: each 65,536-value chunk is stored as a sorted array or a bitmap by density, with an opt-in run-length form for clustered data that `Optimize()` and `AddRange` produce. Set algebra runs word-parallel inside a chunk and skips a whole chunk with one comparison, memory is ~10x below `HashSet<int>` (far more when dense or clustered), and enumeration is **in ascending order**. For huge-and-sparse integer sets — posting lists, row-id sets, cohort intersection. No portable Roaring format: this is an in-process structure, not an interop codec.
- `PersistentHashSet<T, THasher>` — **immutable** hash set: `PersistentHashMap`'s CHAMP trie with the values taken out. A single-element edit returns a new set **sharing** all but one root-to-leaf path of the old one's storage, an edit that changes nothing hands back the receiver, and `Union` / `Except` / `Intersect` / `SymmetricExcept` run through a builder. Against `ImmutableHashSet<T>`'s AVL tree of branching factor two with a heap node per distinct hash, a membership test is a few popcount-indexed array reads rather than a pointer chase per level. For membership state tested far more often than it is written and still handed to readers as a snapshot. Implements `IReadOnlySet<T>`.

The mutable sets (`CeleritySet`, `SwissSet`, `RobinHoodSet`, `HashCachingSet`, `PooledCeleritySet`, `LinkedSet`, `IntSet`, `LongSet`, `SmallSet`, `EnumSet`, `SparseSet`, `HierarchicalBitSet`, `CompressedIntSet`) all implement **`ISet<T>`** and **`IReadOnlySet<T>`** — the full `HashSet<T>` set-algebra surface (`UnionWith` / `IntersectWith` / `ExceptWith` / `SymmetricExceptWith` and the `IsSubsetOf` / `IsSupersetOf` / `Overlaps` / `SetEquals` query family, plus `CopyTo`) with BCL semantics — so they drop in wherever a `HashSet<T>` is used. (The bounded-domain sets, `EnumSet`, `SparseSet` and `HierarchicalBitSet`, are the exception to "drop in anywhere": they store only values in their fixed domain, so a mutating op that must add an out-of-domain value throws.)

### Examples

#### Sets — IntSet, CeleritySet, SwissSet, RobinHoodSet, HashCachingSet, LinkedSet, FrozenCeleritySet, SmallSet, EnumSet, SparseSet, HierarchicalBitSet, CompressedIntSet

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

`LinkedSet<T, THasher>` is the insertion-ordered set — the set counterpart of `LinkedDictionary`. Elements enumerate oldest first, and the order survives removals: `Remove` is `O(1)`, where `HashSet` reuses the freed slot, so the next new element appears in the removed one's place. Re-adding a present element keeps its position; `MoveToEnd` / `MoveToFront` reorder it, and `TryRemoveFirst` / `TryRemoveLast` pop either end.

```csharp
var firstSeen = new LinkedSet<int, Int32WangNaiveHasher>(new[] { 7, 3, 7, 9, 3, 1 }); // 7, 3, 9, 1
firstSeen.Remove(3);       // O(1); 7, 9, 1 keep their order
firstSeen.MoveToEnd(7);    // 9, 1, 7
firstSeen.TryRemoveFirst(out int oldest); // oldest == 9
```

`FrozenCeleritySet` is the build-once, read-many string set counterpart of `FrozenCelerityDictionary`, with the same layout and the same trade against its BCL counterpart, `FrozenSet<string>` — immutable, implements `IReadOnlySet<string>` (so `SetEquals`, `IsSubsetOf`, `Overlaps`, … are available), and silently dedupes. Use `FrozenCeleritySet<THasher>` (e.g. `StringFnV1AFullHasher`) for non-ASCII elements.

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

`HierarchicalBitSet` is the bounded-universe set for when **order** is the question. It keeps a bit vector over `[0, Universe)` and a 64-ary tree of summary words above it, each summary bit set exactly when the word below is non-zero. "The next present value at or after *x*" is a masked word read, a climb to the first level with a set bit on the right, and one trailing-zero count per level on the way down — at most four levels for a universe of 16.7 million. `SortedSet<int>` has no successor method, so the BCL route is `GetViewBetween(x, int.MaxValue).Min`; at 100,000 values in an 800,000 universe this type answers it **32x** faster, the predecessor 31x faster, and it beats the library's own `BTreeSet<int>.TryGetLowerBound` by **18.9x**. `Add` and `Remove` are 48x and 64x faster, at 39x less memory. The cost is that memory follows the universe, not the count — about `Universe / 8` bytes — so below roughly one value per 16 of the range `CompressedIntSet` is smaller, below about one per 75–105 `BTreeSet` is, and below one per 320 even `SortedSet<int>` is; and `Clear` zeroes the bit vector, so a set cleared every iteration without needing order still wants `SparseSet`.

```csharp
var free = new HierarchicalBitSet(65_536, Enumerable.Range(0, 65_536)); // every slot free
free.TryGetMin(out int slot);                       // the lowest free slot
free.Remove(slot);                                  // take it
free.TryGetLowerBound(1_000, out int near);         // the first free slot at or after 1,000
free.Add(slot);                                     // give it back
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

#### An immutable set — PersistentHashSet

`PersistentHashSet<T, THasher>` is the set half of `PersistentHashMap`: the same **CHAMP** trie with the value array taken out. .NET's only immutable hash set, `ImmutableHashSet<T>`, is an **AVL tree keyed by hash code** with a heap node per distinct hash, so its branching factor is two and every membership test is a pointer chase per level; here a node reads five bits of the hash, 100,000 elements are four levels deep, and elements sit inline in one flat array per node. `Union` / `Except` / `Intersect` / `SymmetricExcept` return new sets and hand back the receiver when nothing changed, and the `IReadOnlySet<T>` queries follow `HashSet<T>`'s semantics.

Reach for it when a set is **tested far more often than it is written but still has to change** — an allow- or deny-list swapped atomically and checked on every request, a visited set threaded through a backtracking search, the names in scope in a compiler pass. At 100,000 elements it answers `Contains` **5.2x** faster than `ImmutableHashSet<int>`, adds **2.4x** faster and retains **1.71x** less memory, though `Remove` allocates about 16% more while running 2.1x faster. If the set never changes after it is built, `FrozenCeleritySet` or a plain `HashSet<T>` is faster still; if nobody holds an old version, `CeleritySet` writes one slot where this allocates a path. See [the API reference](api/collections.md#persistenthashsett-thasher).

```csharp
PersistentHashSet<string, StringXxHash3Hasher> blocked =
    PersistentHashSet<string, StringXxHash3Hasher>.Empty
        .Add("203.0.113.7")
        .Add("198.51.100.23");

PersistentHashSet<string, StringXxHash3Hasher> snapshot = blocked; // hand to another thread
blocked = blocked.Add("192.0.2.99");          // snapshot is untouched

bool live = blocked.Contains("192.0.2.99");   // True
bool asPublished = snapshot.Contains("192.0.2.99"); // False
```

## Caches

- `LruCache<TKey, TValue, THasher>` — fixed-capacity **least-recently-used cache** with `O(1)` get/put and automatic eviction of the least-recently-used entry. Recency runs through an intrusive doubly-linked list over fixed-size arrays, so the hot get/put/evict path **allocates nothing** — unlike the idiomatic `Dictionary` + `LinkedList` LRU that heap-allocates a node per insert. Reads count as uses (a hit promotes the entry to most-recently-used); `TryPeek` / `ContainsKey` inspect without disturbing recency.
- `LfuCache<TKey, TValue, THasher>` — fixed-capacity **least-frequently-used cache** with expected-`O(1)` get/put and ties broken by least-recently-used. Its *eviction bookkeeping* is `O(1)` **worst case** rather than logarithmic; the key-index lookup is what leaves the end-to-end bound expected, as everywhere else in the library. It covers the failure mode recency cannot see: **an LRU is scan-vulnerable**, so one sequential pass over a capacity's worth of cold keys evicts the whole hot set. Here a scan key arrives at frequency 1, so **a scan of any length costs at most one entry that has been read more than once**. Entries still at frequency 1 are not protected: they sit at the scan's own frequency and are older, so a scan takes them. That is the policy, not a gap in it — an entry used exactly once has demonstrated no reuse to protect. Frequency buckets and their entry chains sit in fixed-size arrays, so the hot path **allocates nothing** — against an idiomatic `Dictionary` + `SortedSet` LFU that is `O(log n)` per operation and allocates a tree node per insert. Frequencies never age, which is the tradeoff: use it for a stable, skewed popularity distribution and keep `LruCache` for recency-dominated ones.
- `SieveCache<TKey, TValue, THasher>` — fixed-capacity cache with **SIEVE eviction** (NSDI 2024). A hit sets one visited flag instead of relinking a node, so reads are cheaper than an LRU's and **leave active enumerators valid**; an insert into a full cache sweeps a hand over an insertion-ordered queue and evicts the first entry not used since the hand last passed it. On a Zipf(1.0) trace it misses **40% of the time against `LruCache`'s 49%**, about level with `LfuCache` without counts that never age. Fixed node storage, so the hot path **allocates nothing**. Hits are 11.3x faster than an idiomatic `Dictionary` + `LinkedList` LRU and eviction churn is faster too; only misses over a wide key space are slower, about 2x, as they are for `LruCache`.

### Examples

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

`LfuCache` is the frequency-ordered sibling, and it exists for the one failure mode recency cannot see: an **LRU is scan-vulnerable**, so a single sequential pass over a capacity's worth of cold keys evicts the entire hot set no matter how often those keys were used — the ordinary shape of a table scan or a backfill sharing a cache with steady-state traffic. Under LFU a scan key arrives at frequency 1, so **a scan of any length costs at most one entry that has been read more than once** — one if the cache was full with nothing at frequency 1 when the scan began, none if it had spare room. The guarantee is conditional and the condition matters: an entry inserted and never read again sits at the scan's own frequency and is older than the incoming keys, so the scan takes it. LFU protects demonstrated reuse; a single use demonstrates none. Against an LRU, which loses its whole working set to the same scan however hot it was. Entries sit in **frequency buckets** held in an ascending-frequency list, both threaded through fixed-size arrays, which makes the eviction bookkeeping `O(1)` *worst case* and allocation-free — against an idiomatic `Dictionary` + `SortedSet` LFU that is `O(log n)` and allocates a tree node per insert. (A whole operation is expected `O(1)`: it also looks the key up in the key index, like every hash-backed type here.) The tradeoff is stated rather than hidden: **frequencies never age**, so a key that was hot long ago can hold its slot. Use `LfuCache` for a stable, skewed popularity distribution; use `LruCache` when what matters is what was touched recently. See [the API reference](api/collections.md#lfucachetkey-tvalue-thasher).

```csharp
var cache = new LfuCache<long, string, Int64WangHasher>(capacity: 3);
cache[1] = "one";
cache[2] = "two";
_ = cache[1];             // 1 is now at frequency 2; 2 is still at 1
cache[3] = "three";       // full
cache[4] = "four";        // evicts the least-frequently-used (2), not 1
Console.WriteLine(cache.ContainsKey(2)); // False (evicted)
Console.WriteLine(cache.ContainsKey(1)); // True  (spared by its frequency)

cache.TryGetFrequency(1, out long uses);
Console.WriteLine(uses);                 // 2 — the insert plus the read
```

`SieveCache` changes what a hit costs. Entries stay in insertion order, and a hit only sets a *visited* flag. When an insert finds the cache full, a "hand" walks from the oldest entry toward the newest, clearing flags as it passes, and evicts the first entry whose flag is already clear. It then rests there for the next sweep, wrapping back to the oldest entry when it passes the newest. An entry inserted and never read again goes on the hand's next pass. An entry that keeps being read survives every pass without being moved. That makes reads cheaper than `LruCache`'s relink, and it makes them non-structural, so **reads do not invalidate an enumerator**. It also misses less often than LRU on skewed traffic: 40.2% against 49.2% on a seeded Zipf(1.0) trace at capacity 1,024, level with `LfuCache` at 40.5%, without frequencies that never age. Eviction churn is faster than the BCL-style LRU too; only misses over a wide key space are slower, about 2x, as they are for `LruCache`. See [the API reference](api/collections.md#sievecachetkey-tvalue-thasher).

```csharp
var cache = new SieveCache<long, string, Int64WangHasher>(capacity: 3);
cache[1] = "one";
cache[2] = "two";
cache[3] = "three";       // full: newest..oldest = 3, 2, 1
_ = cache[1];             // a hit marks 1 visited; the order does not change
cache[4] = "four";        // the hand clears 1's flag, moves on, and evicts 2
Console.WriteLine(cache.ContainsKey(2)); // False (evicted)
Console.WriteLine(cache.ContainsKey(1)); // True  (spared once by the read)

foreach (var entry in cache)
    _ = cache[entry.Key]; // legal: a hit is not a structural change
```

## Sequences

- `Deque<T>` — growable **double-ended queue** backed by a **circular buffer**: `O(1)` amortized push / pop / peek at **both** ends, plus `O(1)` random access by index. The array-backed deque the BCL lacks (`Queue<T>` is FIFO-only, `Stack<T>` LIFO-only, and `LinkedList<T>` allocates a node per element) — a bounded FIFO / sliding-window churn reuses the buffer with wrap-around and **allocates nothing**, and enumeration walks contiguous memory. Implements `IReadOnlyList<T>`.
- `BTreeList<T>` — **positional list** over a **counted B+ tree**: `Insert` and `RemoveAt` at **any** index in `O(log n)` plus a shift inside one 128-element leaf, where `List<T>` moves every later element and pays `O(n)`. Leaves hold elements contiguously and are chained for enumeration; internal nodes hold each child's starting position, so a read is a short descent. The BCL's only `O(log n)` list, `ImmutableList<T>`, is an AVL tree with a heap node per element. At 100,000 elements a random `Insert` or `RemoveAt` is **~80x** faster than `List<T>`'s and an insert **3.3x** faster than `ImmutableList<T>.Builder`'s, and random reads are **9.1x** faster than the builder's. Random reads lose to `List<T>`'s single array load, so a list that is only appended to and read should stay a `List<T>`. Implements `IList<T>` and `IReadOnlyList<T>`.
- `GapBuffer<T>` — **positional list for cursor editing**, laid out as a **gap buffer**: one array whose free space follows the edits, so `Insert` and `RemoveAt` next to the previous edit are `O(1)` where `List<T>` moves every later element. An edit elsewhere costs the distance the gap moves, and `GapPosition` exposes it. Reads stay an array load at a branch-free offset (1.14–1.28x `List<T>`'s), and `GetSpans` hands out both halves without copying. At 100,000 elements cursor edits are **~257x** `List<T>` and **2.95x** `BTreeList<T>`; ⚠️ edits that jump around at random belong in `BTreeList<T>`.
- `SegmentedList<T>` — **append-built list** in fixed segments of up to 64 KB each: growth adds a segment instead of doubling and copying one array, so once the first segment is full no element is copied again and no segment lands on the Large Object Heap. Building a million `int`s by `Add` without pre-sizing is **1.28x** faster than `List<T>`, allocates **half** the bytes, and triggers **no gen-2 collections**, where `List<T>` triggers about 600 per 1,000 builds. `GetSegment` hands out each segment as a span and scans at array speed. A random read is two dependent loads (1.23–1.39x slower), `foreach` is 1.5x slower, and lists of ~1,000 build 1.27x slower, so a list whose size is known up front belongs in a pre-sized `List<T>`. Implements `IList<T>` and `IReadOnlyList<T>`.
- `PersistentVector<T>` — **immutable indexed sequence** over a **32-way bit-partitioned trie** with a tail buffer: every operation returns a new vector **sharing** all but `O(log32 n)` of the old one's storage, and both `this[int]` and `Add` are at most seven array hops — `Add` writes only the tail on 31 appends out of 32, and path-copies `O(log32 n)` nodes on the 32nd. The structure .NET does not ship, between the two that do — `ImmutableArray<T>` copies the whole array on every append, `ImmutableList<T>` is an AVL tree with a heap node per element and pays a pointer chase per level on **every read**. Concurrent readers need no synchronization, as for the build-once types — the difference is that this one is never mutated at all, so an edit produces another vector rather than requiring a rebuild. Implements `IReadOnlyList<T>`.

### Examples

`Deque<T>` is a growable **double-ended queue** backed by a **circular buffer**: `O(1)` amortized push / pop / peek at both ends, plus `O(1)` random access by index. The BCL has no deque — `Queue<T>` is FIFO-only, `Stack<T>` LIFO-only, and only `LinkedList<T>` supports both ends, at the cost of a heap-allocated node per element. A bounded FIFO or sliding-window churn reuses the buffer with wrap-around, so after warm-up it allocates nothing, and enumeration walks contiguous memory. See [the API reference](api/collections.md#dequet).

```csharp
var work = new Deque<int>(new[] { 1, 2, 3 }); // front-to-back: 1, 2, 3
work.PushFront(0);                            // [0, 1, 2, 3]
work.PushBack(4);                             // [0, 1, 2, 3, 4]
int hi = work.PopFront();                     // 0 — take from the front
int lo = work.PopBack();                      // 4 — take from the back
int mid = work[1];                            // 2 — O(1) random access, front-relative
```

`BTreeList<T>` is the **positional list** for a sequence edited in the middle. `List<T>` shifts every later element on each `Insert` and `RemoveAt`, so a list churned in the middle is quadratic. This type is a counted B+ tree: an edit is a short descent plus a shift inside one 128-element leaf, whatever the list's size. At 100,000 elements, a thousand random-position inserts take 37 µs against `List<T>`'s 2.9 ms (**~80x**), and `RemoveAt` is about the same. Against `ImmutableList<T>`, the BCL's only `O(log n)` list and an AVL tree with a heap node per element, inserts are **3.3x** faster and random reads **9.1x**. Random reads still lose **12x** to `List<T>`'s single array load, so a list that is only appended to and read should stay a `List<T>`. See [the API reference](api/collections.md#btreelistt).

```csharp
var playlist = new BTreeList<string>(new[] { "intro", "verse", "chorus", "outro" });
playlist.Insert(2, "bridge");          // intro, verse, bridge, chorus, outro — O(log n)
playlist.RemoveAt(0);                  // verse, bridge, chorus, outro
string second = playlist[1];           // "bridge" — a short descent, not one array load
```

`GapBuffer<T>` is the **positional list for cursor editing**: a text or token buffer behind a caret, a timeline edited at a playhead, any sequence changed in bursts around one spot. It is one array with its free space, the gap, kept where the edits are, so an insert or a removal next to the previous one moves nothing, and an edit elsewhere first moves the gap the distance the cursor travelled. Reads stay an array load at a branch-free offset. At 100,000 elements a thousand cursor edits take 9.1 µs against `List<T>`'s 2.3 ms (**~257x**) and `BTreeList<T>`'s 27 µs (**2.95x**). Random reads cost 1.14x `List<T>`'s, and enumeration is slightly faster than `List<T>`'s. Edits that jump around at random move the gap a third of the list each time, so they belong in `BTreeList<T>`. See [the API reference](api/collections.md#gapbuffert).

```csharp
var text = new GapBuffer<char>("hello world");
int caret = 5;
foreach (char c in ", dear")
    text.Insert(caret++, c);                  // hello, dear world — each keystroke is O(1)
text.RemoveAt(--caret);                       // backspace: hello, dea world — O(1) too
int gap = text.GapPosition;                   // 10 — the gap followed the caret
```

`SegmentedList<T>` is the list for **a large sequence built by appending** when the final size is not known. `List<T>` doubles one array: it copies every element on each growth, holds the old and new arrays at once while it does, and past about 21,000 `int`s allocates every new array on the Large Object Heap, where it counts toward a full gen-2 collection. This type stores elements in segments that each stay under the LOH threshold, and once the first segment is full, growth only adds a segment. Building a million `int`s is **1.28x** faster than an un-presized `List<int>`, allocates **half** the bytes, and runs **zero** gen-2 collections against `List<int>`'s ~600 per 1,000 builds. The cost is reads: an indexed read is two dependent loads (**1.23–1.39x** slower) and `foreach` is 1.5x slower, so hot sequential loops should walk `GetSegment`, which scans at span speed. See [the API reference](api/collections.md#segmentedlistt).

```csharp
var rows = new SegmentedList<int>();
for (int i = 0; i < 1_000_000; i++)
    rows.Add(i);                              // past the first segment: no copies, no LOH segments

long sum = 0;
for (int s = 0; s < rows.SegmentCount; s++)
    foreach (int x in rows.GetSegment(s))     // a span per segment: array-speed scans
        sum += x;
```

`PersistentVector<T>` is the **immutable** sequence: a 32-way bit-partitioned trie with a tail buffer, where every operation returns a new vector sharing all but one root-to-leaf path with the old one. .NET ships the two ends of this trade and nothing between them — `ImmutableArray<T>` indexes like an array but copies the whole array on every append (`O(n²)` to build), and `ImmutableList<T>` appends in `O(log n)` but is an AVL tree with a heap node per element, so it pays a pointer chase per level on every read too. At 100,000 elements this type indexes **14.3x** faster than `ImmutableList<T>`, appends **16.3x** faster and retains **9.4x** less memory. See [the API reference](api/collections.md#persistentvectort).

```csharp
PersistentVector<string> log = PersistentVector<string>.Empty;
log = log.Add("started").Add("connected");

PersistentVector<string> snapshot = log;      // hand to a reader on another thread — no copy
log = log.Add("closed");                      // snapshot still has 2; log has 3

string second = log[1];                       // "connected" — a handful of array hops
PersistentVector<string> amended = log.SetItem(0, "restarted"); // log is untouched
```

## Union-find

- `DisjointSet<T>` — **union-find** over arbitrary elements: partitions them into disjoint sets with near-`O(1)` amortized `Union` / `Find` / `Connected` via **union by size** + **path halving**. The union-find the BCL lacks — incremental connectivity, connected components, Kruskal MST, and undirected cycle detection in near-linear total time, where the `Dictionary` + `HashSet` set-merge substitute is quadratic. `GetComponents()` materializes the current partition.

### Examples

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

## Priority queues

- `IndexedPriorityQueue<TElement, TPriority, THasher>` — **addressable** binary min-heap: unlike the BCL `PriorityQueue<,>` it can **change a queued element's priority** (`Update` / decrease-key) and **remove an arbitrary element** in `O(log n)`, and answer `Contains` / `TryGetPriority` in `O(1)`. The heap the priority-relaxation loop of Dijkstra / Prim / A\* needs — no lazy-deletion heap growth. Each element is a key (appears once); pass a custom `IComparer<TPriority>` for a max-heap.
- `MinMaxHeap<TElement, TPriority, TComparer>` — **double-ended** priority queue (a min-max heap): `PeekMin` / `PeekMax` in `O(1)` and `DequeueMin` / `DequeueMax` in `O(log n)` from one flat array, duplicates welcome. For a capped buffer served from one end and evicted from the other, `EnqueueDequeueMax` admits a newcomer and drops the worst entry in one sift. **2.45–2.60x faster** at 100,000 entries than `SortedSet<(priority, seq)>`, the BCL's usual double-ended substitute, with a fraction of the allocation; ⚠️ about 1.07–1.30x slower than `PriorityQueue<,>` when only the minimum is ever taken.
- `RadixHeap<TElement>` — **monotone** priority queue over `ulong` priorities (a radix heap): `Enqueue` is an `O(1)` append and `Dequeue` amortized `O(log C)`, with no comparison on the push path, for queues where no priority enqueued is below the last one dequeued (Dijkstra / A\* with integer weights, discrete-event simulation over integer timestamps). At 100,000 entries it is **2.52x faster** than `PriorityQueue<,>` on lazy-deletion Dijkstra, **2.07x** on a drain and **1.52x** on the steady-state hold model; ⚠️ at 1,000 entries `PriorityQueue<,>` is 1.07–1.17x faster on Dijkstra and a drain.

### Examples

#### Addressable priority queue — IndexedPriorityQueue

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

#### Double-ended priority queue — MinMaxHeap

`MinMaxHeap<TElement, TPriority, TComparer>` is a **min-max heap**: an implicit binary heap whose levels alternate between min-ordered and max-ordered, so the minimum is the root and the maximum is one of its two children. It serves **both ends** in `O(log n)` from one flat array. The BCL's heap serves one end, and its sorted containers that reach both pay for a full ordering: `SortedSet<T>` is a tree with a node per entry that rejects duplicate priorities unless you pack a sequence number into the key, and `SortedList<,>` inserts in `O(n)`. The workload is a **bounded buffer served from one end and evicted from the other**: at 100,000 entries it is **2.60x** faster than `SortedSet` on a double-ended drain and **2.45x** on a capped queue, allocating about a sixth and a fiftieth as much. If you only ever take the minimum, use the BCL `PriorityQueue<,>`, which is 1.07–1.30x faster there. `MinMaxHeap<TElement, TPriority>` is the natural-order alias. See [the API reference](api/collections.md#minmaxheaptelement-tpriority-tcomparer).

```csharp
// A capped work queue: serve the most urgent job, drop the least urgent when full.
var queue = new MinMaxHeap<string, int>(capacity: 3);
queue.Enqueue("reindex", 5);
queue.Enqueue("page-oncall", 1);
queue.Enqueue("rotate-logs", 9);

string dropped = queue.EnqueueDequeueMax("renew-cert", 2); // rotate-logs: evicted to admit the newcomer
Console.WriteLine(queue.DequeueMin());                      // page-oncall
Console.WriteLine(queue.PeekMax());                         // reindex
```

#### Monotone integer priority queue — RadixHeap

`RadixHeap<TElement>` is a **radix heap**: 65 buckets keyed by the highest bit at which a priority differs from `Floor`, the last priority dequeued. `Enqueue` is one `LeadingZeroCount` and an append. `Dequeue` pops the bucket of entries equal to `Floor`, and when that is empty it redistributes the lowest non-empty bucket into lower ones in a single sequential pass. The contract is **monotonicity**: no priority enqueued may be below `Floor`, and `Enqueue` throws if one is. Dijkstra and A\* with non-negative integer weights, discrete-event simulation over integer timestamps, and an in-order deadline drain all meet it. The BCL `PriorityQueue<,>` sifts a 4-ary heap on every operation and cannot use the property. At 100,000 entries it is **2.52x** faster on Dijkstra, **2.07x** on a random-priority drain and **1.52x** on the hold model of an event simulation. Below a few thousand entries, use the BCL heap: at 1,000 it is 1.07–1.17x faster on Dijkstra and a drain. See [the API reference](api/collections.md#radixheaptelement).

```csharp
// Event simulation: events are scheduled at or after "now", so the queue is monotone.
var events = new RadixHeap<string>();
events.Enqueue("arrive", 10);
events.Enqueue("depart", 25);
events.Enqueue("refuel", 12);

Console.WriteLine(events.Dequeue());   // arrive (now = 10)
events.Enqueue("inspect", 11);         // fine: 11 >= Floor (10)
Console.WriteLine(events.Dequeue());   // inspect
// events.Enqueue("late", 5);          // throws ArgumentOutOfRangeException: 5 < Floor (11)
```

## Prefix trees

- `Trie<TValue>` — ordered **prefix tree** mapping string keys to values. `GetByPrefix` lists every entry whose key starts with a prefix in `O(prefix + matches)`, and `TryGetLongestPrefix` finds the longest stored key that is a prefix of a query in `O(query)`. The trie the BCL lacks — autocomplete, longest-prefix routing, and ordered (ascending-ordinal) iteration, where a `Dictionary<string, TValue>` has no prefix index and must scan every key and run `StartsWith`. Exact `Add` / `TryGetValue` favour a `Dictionary` (one hash vs a character walk); the trie earns its place on the prefix operations. Implements `IReadOnlyDictionary<string, TValue?>`.
- `SuccinctTrie<TValue>` — the **build-once** prefix tree, whose **tree shape costs two bits per node** (about 3 bytes a node once labels, the terminal bit and the rank/select indexes are counted): the same `GetByPrefix` / `TryGetLongestPrefix` / ordered-enumeration surface as `Trie<TValue>`, over a LOUDS bit vector plus a label array rather than a graph of node objects. This is the composition `RankSelectBitVector`'s own docs name and the library never shipped. What it buys is **footprint**: at 100,000 keys it retains **1.05 MB against `Trie`'s 40.16 MB — 38.1x smaller** — and it never stores the key strings at all, which a `Dictionary<string, TValue>` must. ⚠️ **What it costs is query time, on every arm**: exact `Lookup` is 28x a `Dictionary` and 6.2x `Trie`, and building it is 16.6x. The prefix win is the trie win and it is a matter of *selectivity*: on a nearly-complete token with a handful of completions it is **1,082x** a `Dictionary` (which must scan every key), but on bulk enumeration of a sixteenth of the table it is 3.7x *slower* than one — and so, measurably, is `Trie` — because the dictionary's scan hits a match every sixteen entries and either trie is charged for materializing every result string. Immutable, so a changed key set means a rebuild. Implements `IReadOnlyDictionary<string, TValue?>`.

### Examples

`Trie<TValue>` is the ordered **prefix tree** the BCL lacks: it maps string keys to values and answers the prefix queries a `Dictionary<string, TValue>` can't do without an `O(n)` scan. `GetByPrefix` lists every entry under a prefix in `O(prefix + matches)` and in ascending key order; `TryGetLongestPrefix` finds the most specific stored key that prefixes a query. Reach for it for autocomplete, longest-prefix routing, or ordered iteration — not for pure exact-key lookups, where a `Dictionary` (one hash vs a character walk) wins. See [the API reference](api/collections.md#trietvalue).

```csharp
var routes = new Trie<string>();
routes["/"] = "home";
routes["/api"] = "api-root";
routes["/api/v1/users"] = "users-v1";
routes["/api/v1/orders"] = "orders-v1";

// Autocomplete: every entry under a prefix, already sorted.
foreach (var (path, target) in routes.GetByPrefix("/api/v1/"))
    Console.WriteLine($"{path} -> {target}"); // /api/v1/orders, then /api/v1/users

// Longest-prefix routing: the most specific stored route that prefixes the request.
if (routes.TryGetLongestPrefix("/api/v1/users/42", out string? route, out string? handler))
    Console.WriteLine($"{route} -> {handler}"); // /api/v1/users -> users-v1
```

## Span-keyed string lookups

- `StringInternTable` / `StringInternTable<THasher>` — a **canonicalizing token table** probed with a `ReadOnlySpan<char>`: `GetOrAdd` returns the one shared `string` for those characters and allocates **only on a miss**. A 10M-cell parse over 100 distinct tokens creates 100 strings instead of 10,000,000. `HashSet<string>` does the same through `GetAlternateLookup<ReadOnlySpan<char>>()` (`TryGetValue`, then `Add` on a miss) and allocates the same strings; ⚠️ with its default hasher this type probes about 2x slower than that set, and with `StringCityHash64Hasher` 1.05–1.2x faster. `string.Intern` is process-wide, never collected, and still needs a `string`. Implements `IReadOnlyCollection<string>`.
- The same span-keyed probes ship on `FrozenCelerityDictionary`, `FrozenCeleritySet`, `CelerityDictionary<string, …>`, `CeleritySet<string, …>`, `Trie<TValue>`, and `SuccinctTrie<TValue>` — so a tokenizer, CSV/log reader, or route dispatcher holding a slice of its input buffer never has to call `new string(span)` per lookup. The BCL's equivalent is `Dictionary<string,V>.GetAlternateLookup` (.NET 9+), a separate lookup struct; here the overloads sit on the collection itself. See [span-keyed lookups](api/collections.md#span-keyed-lookups).

### Examples

A route table keyed by `string` takes the path as a slice of the request line, so the lookup builds no `string`:

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var routes = new FrozenCelerityDictionary<int, StringXxHash3Hasher>(
[
    new("/api/v1/users",  1),
    new("/api/v1/orders", 2),
]);

ReadOnlySpan<char> line = "GET /api/v1/users HTTP/1.1".AsSpan();
ReadOnlySpan<char> path = line.Slice(4, 13);

if (routes.TryGetValue(path, out int handler))   // no string allocated
    Dispatch(handler);
```

`StringInternTable` hands back one shared `string` per distinct token, so a parse allocates a string only for a token it has not seen before. See [the API reference](api/collections.md#stringinterntable).

```csharp
using Celerity.Collections;

var interned = new StringInternTable();

foreach (ReadOnlySpan<char> cell in SplitCells(line))
{
    string token = interned.GetOrAdd(cell);   // allocates only the first time each token is seen
    Consume(token);
}

Console.WriteLine(interned.Count);            // distinct tokens, == strings allocated
```

## Sorted (ordered) collections

- `BTreeDictionary<TKey, TValue, TComparer>` / `BTreeDictionary<TKey, TValue>` — a **sorted map backed by a B-tree**: up to **31 keys per node** in flat arrays, so a lookup visits `log₃₂(n)` nodes instead of chasing `log₂(n)` pointers — roughly **4 cache misses instead of ~20 at `n = 1M`**. Adds the ordered surface a hash table cannot answer: `Min`, `Max`, `TryGetLowerBound` / `TryGetUpperBound`, `EnumerateRange` in `O(log n + k)`, and in-order enumeration. The B-tree the BCL lacks — `SortedDictionary<,>` is a red-black tree with one heap object per entry, and `SortedList<,>` memmoves the tail on every insert. Implements `IDictionary<TKey, TValue?>` and `IReadOnlyDictionary<TKey, TValue?>`.
- `BTreeSet<T, TComparer>` / `BTreeSet<T>` — the set counterpart, with the same ordered surface and no values to store, so the memory saving over `SortedSet<T>` is larger still. Implements `ISet<T>` and `IReadOnlySet<T>`.
- `RankedSet<T, TComparer>` / `RankedSet<T>` — an **order-statistics set**: the same ordered surface, plus the two positional questions no BCL ordered container can answer — `IndexOf(item)` (the element's **rank**) and `set[k]` (the **k-th smallest**), both `O(log n)`, on a set that is still being inserted into and removed from — with `Add` / `Remove` at `O(√n)`, a bounded memmove rather than a whole-array one, which is the sqrt-decomposition trade — plus `CountLessThan` / `CountLessThanOrEqual` for the rank an *absent* element would take, and `RemoveAt(rank)`, which the BCL has no equivalent for at all. `SortedSet<T>` has neither rank nor positional access (`ElementAt(k)` is `O(k)`); `SortedList<,>` and a hand-rolled sorted `List<T>` have both but memmove half the array on every insert. Elements sit in sorted buckets whose capacity tracks `√n` (never below 512), with a Fenwick tree over the bucket lengths carrying the positional half. Implements `ISet<T>`, `IReadOnlySet<T>` **and `IReadOnlyList<T>`** — the only set here that is also a list, and it can be for exactly that reason.
- `RankedMultiSet<T, TComparer>` / `RankedMultiSet<T>` — `RankedSet` **with duplicates kept**: an order-statistics multiset. .NET has no sorted collection that keeps repeats, and `RankedSet` drops them, which makes its percentiles wrong over a window of samples with ties. `Add` always inserts, `CountOf(item)` counts the copies, and `CountLessThan`, `IndexOf` / `LastIndexOf` and `set[k]` are all `O(log n)`, with `Add` / `Remove` at `O(√n)` on the same bucket layout. Equal elements keep **insertion order**: a new copy goes after every equal one and `Remove` takes the earliest, so the type is stable. `RemoveAll(item)` drops a whole run in one pass, however many buckets it spans. Implements `ICollection<T>` and `IReadOnlyList<T>`.

All four take their ordering as a **struct** `IComparer<T>` type parameter (`DefaultComparer<T>` by default), exactly as the hashers are struct type parameters, so the comparison inlines instead of costing a virtual call per key inspected.

- `FrozenSortedSet<T>` — an **immutable sorted set of integer keys** laid out as a static B+ tree of cache-line nodes searched with SIMD compares, so `Contains`, rank (`CountLessThan`) and successor (`TryGetLowerBound` / `TryGetUpperBound`) take a handful of node reads instead of `log2(n)` dependent binary-search probes: about **10× faster than a span `BinarySearch`** over the same sorted `int[]` for a batch of lookups, and 2–3× when each answer feeds an unpredictable branch. `SliceRange(a, b)` returns the keys in `[a, b)` as a span without copying. Build-once; integers only (`IBinaryInteger<T>`), so it takes no comparer. Implements `IReadOnlyList<T>` and `IReadOnlySet<T>`.
- `FrozenSortedMap<TKey, TValue>` — the **map sibling of `FrozenSortedSet`**: the same SIMD-searched tree over integer keys, with the values in a parallel array, so `TryGetFloor` ("the value as of *t*", "which range owns key *k*") is about **9× faster than a span `BinarySearch`** over sorted key and value arrays at 1k to 1M keys. It also has `TryGetPredecessor` / `TryGetLowerBound` / `TryGetUpperBound` and a no-copy `GetIndexRange` over the key and value spans. ⚠️ On a shuffled half-hit stream an exact `TryGetValue` is 2.3–3.6× *slower* than `FrozenDictionary`, so use this map for the ordered queries. A duplicate key is rejected. Build-once; implements `IReadOnlyDictionary<TKey, TValue>`.

### Examples

#### Sorted maps and sets with range scans — BTreeDictionary / BTreeSet

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

#### Rank and select over a set that changes — RankedSet

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

#### Rank and select with duplicates — RankedMultiSet

`RankedMultiSet<T>` is `RankedSet<T>` for data with ties: a window of latency samples, a leaderboard with equal scores, or the "sorted list with duplicate keys" the BCL does not ship. Every copy is kept, and equal elements stay in arrival order. Against the usual workaround, `SortedSet<(T, long Seq)>`, a sliding-window median-and-rank workload at 100,000 elements measures **166x** faster locally, and **1.82x** a hand-rolled sorted `List<T>`. At a thousand elements that list is faster.

```csharp
var window = new RankedMultiSet<int>([250, 120, 250, 90, 250]);

Console.WriteLine(window.Count);                 // 5   — every copy kept
Console.WriteLine(window.CountOf(250));          // 3
Console.WriteLine(window[window.Count / 2]);     // 250 — the median, counting ties
Console.WriteLine(window.CountLessThan(250));    // 2   — samples below 250

window.Remove(250);                              // removes one copy, the earliest added
Console.WriteLine(window.LastIndexOf(250));      // 3
```

#### Ordered lookups over a key set that has stopped changing — FrozenSortedSet, FrozenSortedMap

`FrozenSortedSet<T>` is for a sorted integer key set that is read far more often than it changes: an allow-list of IDs, the start keys of range partitions, a timestamp index. The BCL's `FrozenSet<T>` is unordered, and a binary search over a sorted array is latency-bound (`log2(n)` loads, each waiting on the last). This type cuts the keys into 64-byte nodes and stacks a pointer-free index of separator nodes above them, then searches each node with four `Vector128` compares. A million `int` keys take 5 node reads, where a binary search takes 20 probes. A batch of lookups runs about 10× faster than a span `BinarySearch` over the same sorted `int[]` (9.5–10.9× from 1k to 1M keys), and 2–3× when each answer feeds an `if` that mispredicts, because each mispredict exposes one search's full latency. It costs about 6% more memory than the array for `int`, plus a sort to build. See [the API reference](api/collections.md#frozensortedsett).

```csharp
var shardStarts = new FrozenSortedSet<long>(startKeys);          // any order; repeats dropped
int shard = shardStarts.CountLessThan(key + 1) - 1;              // last start at or below key
                                                                 // (key + 1 wraps at long.MaxValue; use Count - 1 there)

var allowed = new FrozenSortedSet<int>(allowedIds);
bool ok = allowed.Contains(accountId);
ReadOnlySpan<int> band = allowed.SliceRange(10_000, 20_000);     // keys in [10k, 20k), no copy
```

`FrozenSortedMap<TKey, TValue>` is the same tree with a value behind every key, for when the question is *which entry* a key falls under: range partitions to their shard, effective dates to a price, address ranges to their owner. `TryGetFloor` returns the entry with the largest key at or below the probe, about 9× faster than a binary search over sorted key and value arrays. The values are sorted with their keys, and a duplicate key throws. For exact lookups alone, `FrozenDictionary` is 2.3–3.6× faster on a shuffled half-hit stream. See [the API reference](api/collections.md#frozensortedmaptkey-tvalue).

```csharp
var shards = new FrozenSortedMap<long, string>(shardStartToName);  // any order; duplicate keys throw
shards.TryGetFloor(key, out var owner);                            // owner.Value: the shard owning key
Range r = shards.GetIndexRange(from, to);                          // entries in [from, to), no copy
ReadOnlySpan<string> names = shards.Values[r];
```

## Range aggregates

- `FenwickTree<T>` — a **Binary Indexed Tree** over a fixed-length numeric sequence (`where T : struct, INumber<T>`): **point update** and **prefix / range sum** both in `O(log n)`, in one flat array with no per-node overhead. The prefix-sum structure the BCL lacks — running aggregates, rank / order-statistics counters, cumulative-frequency tables — where a plain array is `O(n)` per query (recompute the slice) *or* `O(n)` per update (fix the suffix). Wins precisely when updates and partial-sum queries interleave.
- `SegmentTree<T, TMonoid>` — range aggregates over an arbitrary **associative** fold: **point update** and **range query** both in `O(log n)`, in one flat array of `2n` cells. The half of the range-query space a Fenwick tree cannot reach — its query is the *difference* of two prefix folds, so it needs an inverse, while a segment tree stores each node's fold outright. That puts range **min**, **max**, **gcd**, bitwise **and**/**or** and any monoid you write in reach. The BCL has no range-aggregate structure at all, so the baseline is a plain array scanned per query. `SumMonoid` / `MinMonoid` / `MaxMonoid` / `BitwiseAndMonoid` / `BitwiseOrMonoid` ship built in, as struct type parameters so the fold inlines; non-commutative folds are safe, since the query preserves index order.
- `SparseTable<T, TMonoid>` — the **build-once** counterpart: the same range fold, answered in **`O(1)`** — two array reads and one combine, no descent. It precomputes the fold of every power-of-two window and covers a range with the two anchored at its ends; those windows *always overlap* — completely, when the range length is itself a power of two — so the fold must be **idempotent**, and that law is enforced by the compiler rather than by a doc note — the type takes `IIdempotentMonoid<T>`, which `Min` / `Max` / `BitwiseAnd` / `BitwiseOr` implement and `SumMonoid` deliberately does not, so `SparseTable<int, SumMonoid<int>>` will not build. The query measures roughly **9–11×** a `SegmentTree` over the same sequence at 1k and **16–19×** at 100k (and ~100× / ~8,200× the array scan), paid for with a **~4–5× slower build** and **5–8.5× the memory** — so it wins on a sequence built once and queried past roughly **800–1,000** times at 1k, **65,000** at 100k. Immutable, so once built an instance is safe to share for reading — provided the fold is too, since every query calls it.
- `WaveletTree` — an **immutable** succinct index over a fixed sequence of `int` values that answers the questions a range fold cannot: the **`k`-th smallest value inside a positional window** (`Quantile`), how many values in that window fall in a **value band** (`RangeCount`), and the rank of a single value — each `O(log σ)` over the *distinct* values, independent of how wide the window is: one descent for `Quantile` and `Rank`, two for `RangeCount` (one per band edge). `Select` is the one query outside that bound — it binary-searches `Rank`, so `O(log n · log σ)`. A `SegmentTree` folds a window down to one value and so structurally cannot reach the `k`-th; a `RankedSet` ranks a whole set with no positional window and no duplicates; the BCL has neither. The baseline is what a caller writes by hand — `Array.Sort` over a copy of the window for a quantile, a counting loop for a band — and at a million values with a 1% window the quantile is **~3,100× faster** and the band count **~45×**. Build-once, and it *loses* to a plain scan on a short window; both are measured in [the API reference](api/collections.md#wavelettree).

### Examples

#### Prefix sums with live updates — FenwickTree

`FenwickTree<T>` (`where T : struct, INumber<T>`) is a **Binary Indexed Tree**: a fixed-length numeric sequence that answers **prefix / range sums** and applies **point updates** both in `O(log n)`, in one array with no per-node overhead. The BCL ships nothing for the interleaved update + prefix-sum-query workload — a plain array is `O(n)` per query or `O(n)` per update. It wins precisely when both interleave (running aggregates, rank counters, cumulative-frequency tables).

```csharp
var tree = new FenwickTree<long>(new long[] { 3, 1, 4, 1, 5, 9 });

Console.WriteLine(tree.PrefixSum(3));   // 8  (3 + 1 + 4)
Console.WriteLine(tree.RangeSum(2, 5)); // 10 (4 + 1 + 5)

tree.Add(0, 10);                        // point update, O(log n)
Console.WriteLine(tree[0]);             // 13
Console.WriteLine(tree.Total);          // 33
```

#### Range min / max / any associative fold with live updates — SegmentTree

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

#### The same fold in O(1), once the sequence stops changing — SparseTable

`SparseTable<T, TMonoid>` is the **build-once** counterpart to `SegmentTree`. It precomputes the fold of every window whose length is a power of two, then answers an arbitrary range from the two such windows anchored at its ends — **`O(1)`**, two array reads and one combine, whatever the width. Those windows *always overlap* — by `2·2ᵏ - length` elements, which is never zero, and at an exact power of two they are the *same* window — so the fold must be **idempotent** (`Combine(a, a) == a`), and that is a constraint rather than a warning: the type takes `IIdempotentMonoid<T>`.

```csharp
// A finished run's latency history: fixed once the run ends, questioned repeatedly afterwards.
long[] latencies = { 105, 102, 108, 101, 110, 103 };
var history = new SparseTable<long, MinMonoid<long>>(latencies);

Console.WriteLine(history.Query(0, 4));   // 101 — two array reads, no descent
Console.WriteLine(history.Query(1, 6));   // 101 — a five-wide window costs the same as a two-wide one
Console.WriteLine(history.Aggregate);     // 101 — O(1), where SegmentTree.Aggregate is O(log n)

// Sum is not idempotent, so a sparse table would double-count the overlap. The compiler says so:
// var wrong = new SparseTable<long, SumMonoid<long>>(latencies);   // does not compile

// The gcd fold from the SegmentTree example qualifies — gcd(a, a) == a — so it only needs the
// derived interface: `public readonly struct GcdMonoid : IIdempotentMonoid<uint>`.
```

**The build is the price.** `O(n log n)` into `levels × n` cells against a segment tree's `O(n)` into `2n`: about 4–5× the build time and 5–8.5× the memory. It repays at roughly 800–1,000 queries at 1,000 elements and 65,000 at 100,000 — below that, `SegmentTree` is the better type even on a sequence that never changes.

#### The k-th smallest value inside a window — WaveletTree

`WaveletTree` is the build-once index over a sequence of `int` values that reaches the *value* axis a range fold collapses. `SegmentTree<long, MinMonoid<long>>` answers the smallest value in a window; nothing in the library — or in the BCL — could answer the **median** of one, because a monoid fold has no way to keep the values it discarded.

```csharp
// A million request latencies in arrival order, indexed once after the run.
var index = new WaveletTree(latencies);

// The median and p99 of requests 400,000 through 500,000. No sort, no copy, and the
// cost does not grow with the width of the window.
Console.WriteLine(index.Quantile(400_000, 100_000, 50_000));   // median
Console.WriteLine(index.Quantile(400_000, 100_000, 99_000));   // p99

// How many of those breached a 250 ms SLO, and how often exactly 200 ms was recorded.
Console.WriteLine(index.RangeCount(400_000, 100_000, 251, int.MaxValue));
Console.WriteLine(index.Rank(index.Length, 200));
```

The values are coordinate-compressed, so the cost follows the number of *distinct* values rather than their magnitude: a thousand distinct latencies cost ten levels whatever the milliseconds are. It is build-once — changing a value means rebuilding — and on a short window the fixed ten-level descent loses to the loop it replaces.

## Spatial & interval indexes

- `KdTree<TValue>` — a build-once **2-D spatial index**: *which point is nearest to this one*, *which lie within this radius*, *which lie inside this box*, without measuring every point. .NET ships **no spatial index of any kind** — no k-d tree, no quadtree, no R-tree — so the alternative is an array and a loop over all of it. The nearest, predicate, count and copy tiers allocate nothing (the k-nearest search heaps inside your own buffer), and the whole structure is one interleaved coordinate array plus the payloads, with no per-point node. Nearest store or driver, viewport culling, collision broadphase, k-means / DBSCAN neighbour queries. The ratio tracks **selectivity**, and a hand-rolled sorted scan is a much closer baseline than the naive one — see [Spatial index](benchmarks.md#spatial-index).
- `SpatialGrid<TValue>` — the **mutable** counterpart: constant-time **`Move`** and `Remove` (and amortized-constant `Add`) addressed by a handle, plus radius, rectangle and nearest queries that touch only the cells they cover. `KdTree` is build-once and its own docs say rebuilding it per frame costs more than the queries save, which leaves the commonest spatial workload unanswered — entities, couriers, cursors, particles and agents all move every tick and all ask *what is near me* every tick. The baseline is not a strawman (a `Dictionary<(int, int), List<T>>` bucketed grid is what a competent developer writes), and a populated grid here is three flat arrays — cell heads, entry records, payloads — with no per-cell or per-entry object. **5.0x** that hand-roll on a frame of 100,000 entities — but everything gained is *per cell*, not per point, so the margin thins as cells fill and **reverses on clustered data**; see [Moving points](benchmarks.md#moving-points).
- `RTree<TValue>` — a build-once **2-D extent index**: *which of these boxes overlap this box*, *which contain this point*, without testing every box. `KdTree` cannot stand in — a box can overlap the query while its centre sits far outside it — and .NET ships nothing for extents either: `System.Drawing` has a `Rectangle` with an `IntersectsWith` and no index over a *collection* of them. The boxes are permuted by **STR (Sort-Tile-Recursive) packing** with a fixed-fanout tree laid over them implicitly, so there are no per-node objects and no child pointers; the predicate, count and copy tiers allocate nothing. Collision broadphase for bodies with a size, map label placement, canvas hit-testing, viewport culling, spatial joins. The standard advice that it only earns its keep when extents vary by orders of magnitude, and that uniform ones belong to a bucketed grid, is measured against a grid arm rather than repeated — and does not survive: the margin is *widest* on uniform extents, and mutability rather than shape is the reason to prefer a grid. See [Extent index](benchmarks.md#extent-index).
- `IntervalTree<TKey, TValue>` — a **build-once index over half-open `[start, end)` ranges** answering *which ranges cover this point* and *which overlap this window* in time that tracks the matches found rather than the intervals stored (`O(log n + k)` when they cluster, `O(min(n, (k + 1) log n))` when scattered, `O(n)` worst case once zero-length intervals are stored). The BCL has no interval structure at all, so the alternative is a `List<T>` scanned per query; sorting it by start lets the scan stop at the upper bound but not skip the front, since a range beginning far to the left can still cover the point, so it stays linear (both baselines are benchmarked). Booking- and scheduling-conflict checks, IP-range lookup, effective-dated pricing, "which trace spans were live at *t*". The whole structure is four flat arrays with no per-interval node, every query shape shares one traversal specialized per `struct` visitor, and the predicate / count / copy tiers allocate nothing.
- `RangeMap<TKey, TValue>` — a **mutable map from disjoint half-open ranges** of keys to values: the coalescing interval map `IntervalTree` is not. Assigning a value to `[start, end)` overwrites whatever was there, splitting any range that straddles either edge, and ranges left adjacent with equal values merge, so the map always holds the maximal runs. The BCL has nothing for it; the alternative is a `List<T>` kept sorted by start, which binary-searches a lookup well but memmoves its tail on every assignment. Keyspace and IP-range ownership edited as shards move, an allocator's used-versus-free map, effective-dated configuration edited in place, style runs. A B-tree keyed by each range's end, so a lookup is one descent and an assignment `O((k + 1) log n)` for the `k` ranges it overwrites.

### Examples

#### Nearest point, points within a radius, points inside a box — KdTree

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

#### What is near me, for points that move — SpatialGrid

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

Pick the cell size to put a handful of points in the average cell, and no smaller than your typical query radius. Points spread **evenly** are what this wants. If yours cluster hard, measured advice: the hand-rolled `Dictionary`-of-`List`s beats this type there, and rebuilding a `KdTree` per frame is no better than it either — see [Moving points](benchmarks.md#moving-points).

#### Which boxes overlap this box, which contain this point — RTree

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

#### Which ranges cover this point / overlap this window — IntervalTree

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

Intervals are half-open, so `[0, 10)` and `[10, 20)` tile without conflicting. Overlapping ranges stay distinct entries — this is not a coalescing interval map, which is `RangeMap` below — and the tree is immutable, so adding a range means building a new one.

#### Map ranges of keys to values, and keep editing them — RangeMap

`RangeMap<TKey, TValue>` maps **disjoint half-open ranges** to values. Assigning a value to `[start, end)` overwrites whatever was there, splitting a range that straddles either edge, and ranges left adjacent with equal values merge — so the map always holds the maximal runs, and `Count` is how many there are. .NET ships nothing for this; the hand-roll is a `List<T>` sorted by start, which binary-searches a lookup well but memmoves its tail on every assignment.

```csharp
// Who owns each slice of a keyspace, edited as shards move.
var owners = new RangeMap<int, string>();
owners.Set(0, 1024, "node-a");
owners.Set(256, 512, "node-b");        // splits node-a's range in two
owners.Set(512, 768, "node-b");        // merges with the adjacent node-b range

Console.WriteLine(owners[300]);        // node-b
Console.WriteLine(owners.Count);       // 3
owners.TryGetRange(300, out var run);  // [256, 768) = node-b

owners.Remove(700, 800);               // trims the straddled ranges rather than dropping them
foreach (var range in owners.EnumerateOverlapping(200, 300))
    Console.WriteLine(range);          // [0, 256) = node-a, [256, 700) = node-b
```

A write that changes nothing — assigning a value a single range already holds, removing an unmapped range — leaves the map and its enumerators untouched. It is for maps you keep **editing**: if the ranges are built once and only read, a sorted array's binary search is the faster lookup.

## Graphs

- `CompressedGraph` — a build-once **graph**, stored compressed sparse row (CSR): two flat `int[]`, so a vertex's neighbours are a `ReadOnlySpan<int>` slice of one contiguous array rather than a heap object of their own. .NET ships no graph, no adjacency list and no traversal of any kind, so the idiom is `Dictionary<int, List<int>>` — or `List<int>[]` once you notice the ids are dense — and both are benchmarked. Dependency and build ordering, package and module resolution, link and follower graphs, reachability and impact analysis. Neighbour iteration, breadth-first traversal, `Reverse()` (the transpose, `O(V + E)`, which the adjacency map has to rebuild list by list) and a topological order, with the destination buffer doubling as the traversal queue so a repeated walk allocates nothing once the `ArrayPool` it borrows its visited set from is warm. **Not a speed win over a hand-roll you write carefully**, and the benchmark says so: against an `int[][]` sized exactly and filled in vertex order, the traversal and the build are both within about 1.2x at 100k vertices — a wash — and both *lose* at 1k. What survives is the **transpose (2.5–3.0x)**, a **1.8x smaller footprint**, and not writing or maintaining the traversal, Kahn's algorithm, the transpose and the sorted-target invariant. Against the idiomatic `Dictionary<int, List<int>>` the same arms are 2.5–2.6x, 3.1–3.8x and 3.6x less memory.

### Examples

#### Walk a graph, or resolve a dependency order — CompressedGraph

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

## Text

- `SuffixArray` — a build-once **text index**: every suffix of a block of text in sorted order, plus the longest-common-prefix array beside it, so *where does this substring occur* costs `O(m log n)` in the **pattern** length rather than `O(n)` in the text. .NET ships no text index at all — `string.IndexOf`, `MemoryExtensions.IndexOf` and `Regex` are scans that re-read the text on every query, and .NET 9's `SearchValues<string>` indexes the *needles*, not the haystack. `Contains` / `CountOccurrences` / `IndexOf`, a zero-copy `TryGetOccurrences` that returns a slice of the index itself, and `TryGetLongestRepeatedSubstring`, which no scan-shaped API can express. At 100,000 characters counting a pattern measures **68x** the scan and ruling an absent one out **64x**; at 1,000 characters both fall to about **1.3x**. **Build-once: one query against a text read once is a loss** — the build is repaid at roughly 1,000 counting queries, and a `Dictionary<string, int[]>` k-gram index beats this by 10.3x per query at the one pattern length it can answer.
- `AhoCorasick` — a build-once **multi-pattern matcher**: a fixed set of patterns compiled into one automaton that finds *every* occurrence of *every* pattern in **one** left-to-right pass, at a cost that does not grow with how many patterns there are. Neither neighbour reaches this — `Trie` matches a *prefix of the query* and cannot resume inside a text, and `SuffixArray` indexes one fixed *text* and answers one pattern at a time — and the BCL has nothing either: `string.IndexOf` is a single-needle scan, so `k` patterns cost `k` passes, and .NET 9's `SearchValues<string>` answers only *where is the first of these*. `ContainsAny` / `CountMatches` / `TryFindFirst`, an allocation-free `EnumerateMatches` that drives the scan as it is pulled, and `CopyMatches` into a caller-owned buffer. Overlapping matches are **reported, not resolved** — `"he"`, `"she"` and `"hers"` over `"ushers"` is three matches. Against a compiled `Regex` alternation of 256 patterns over 100,000 characters: **10.0x** on membership and **4.8x** on enumerating every match, both allocation-free. **The pattern count and the shape both decide** — against the k-`IndexOf` loop it is 4.1x on counting present patterns, but that loop *wins* the 256-pattern absent-membership arm (0.98x) and wins the eight-pattern arm by 5.0x, because `MemoryExtensions.IndexOf` sweeps many characters per step while this pass reads one.
- `EditDistanceIndex` — a build-once **fuzzy-lookup index**: every word within a Levenshtein distance of a query, found **exactly** without computing a distance to each word, by indexing every word's deletion variants (the symmetric-delete technique) and verifying only the words a query's own variants reach. The BCL has no approximate matching at all, and `Trie`, `AhoCorasick` and `SuffixArray` are all exact. Spell-check suggestions, did-you-mean, fuzzy lookup of names and identifiers. Over a real English dictionary, finding every word within two edits measures **12x** a careful length-filtered, banded scan at 1,000 words and **330x** at 100,000; within one edit, 30x and 1,200x. The price is memory, which grows with word length and steeply with the distance: about 11 postings per ten-letter word at distance 1 and 56 at distance 2, at 10–12 bytes each.
- `Rope` — a **rope**: a balanced tree of bounded character runs, so the cost of an edit stops scaling with the document. Where a contiguous buffer shifts `O(n)` characters on every edit whatever its size, this moves at most one leaf — `Insert` is `O(log n + k + ChunkSize)` and `Remove` `O(log n + ChunkSize)`, both amortized over an occasional defragmenting rebuild. The library's only *mutable* text type — `Trie`, `SuffixArray` and `AhoCorasick` all search text that does not change. `StringBuilder` is a chunk list whose head is the *end*, so appending is excellent and `Insert`, `Remove` and every other operation are linear in the **document**: ten times the text is ten times the cost of one edit. It also has two operations the BCL has at no cost at all — `Split` cuts a document in two in `O(log n + ChunkSize)` and `AppendAndClear` joins two back in `O(log n)`, where `Concat` / `Substring` / slice-and-copy are full copies. Leaves are filled to three quarters of `ChunkSize` on purpose, so the ordinary short edit lands in a leaf that has room and **allocates nothing**; filling them to capacity instead measured a 2.9x loss. On a round of 200 scattered insert/remove pairs it measures **98x** `StringBuilder` at a million characters and **1.49x** at ten thousand, and a hundred split-and-rejoin cycles are **713x** at a million (186 KB against 400 MB). **Three operations are outright losses**: appending is **36.8x slower** — use a `StringBuilder` if that is all you do — random access **7.1x**, since a builder built from a string is one chunk and indexes directly, and `ToString()` 1.68x. About 2.7 bytes per character.

### Examples

#### Search one block of text over and over — SuffixArray

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

#### Scan a text once for many patterns — AhoCorasick

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

#### Find the words a typo was meant to be — EditDistanceIndex

`EditDistanceIndex` answers *which of these words are within edit distance `k` of this query*, exactly, without scanning the word list. Two strings within distance `k` always share a string reachable from each by at most `k` deletions, so the build indexes every word's deletion variants and a query looks up its own. Only the words found that way are verified with a real Levenshtein distance. .NET ships nothing for approximate matching, and the other text types here are all exact.

```csharp
var index = new EditDistanceIndex(dictionary, maxDistance: 2);

// Did-you-mean: the closest word, ties going to the one listed first (put the common words first).
if (!index.ContainsWithin(typed, 0) && index.TryFindNearest(typed, 2, out FuzzyMatch nearest))
    Console.WriteLine($"Did you mean {index[nearest.WordId]}?");

// The five best suggestions, closest first, allocating nothing.
var suggestions = new FuzzyMatch[5];
int written = index.CopyWithin(typed, 2, suggestions);
```

Levenshtein over UTF-16 code units, so a transposition is two edits (`"teh"` is 2 from `"the"`); fold case yourself. **The margin grows with the word count and the price is memory**: within two edits over a real dictionary it measures 12x a careful length-filtered, banded scan at 1,000 words and 330x at 100,000, at about 600 bytes per word (59.5 MB for 100,000). Distance 1 is 104 bytes per word. A build costs 65–90 scan queries on short words and 155–180 on a real dictionary's.

#### Edit a large block of text in the middle — Rope

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

## Timers

- `TimerWheel<TValue>` — a **hierarchical timing wheel**: a container of pending **deadlines**, with `O(1)` `Cancel`, amortized-`O(1)` `Schedule` and an `Advance` bounded by the wheel's geometry — `O(levels x slots + fired + cascaded)` — rather than by the ticks it crosses. A data structure, not a scheduler — no thread, no clock, no callback; the caller owns time. The workload is defined by **cancellation** (the reply arrives, the lease is renewed), and that is where the BCL fails: `PriorityQueue`'s only removal, added in .NET 9, is `O(n)`, so the standard workaround is lazy deletion — a heap that grows with the timers that will never fire and a hash probe per pop. Timers thread through one flat entry array as intrusive lists, so there is no object per timer. On a round of 100,000 timeouts — schedule, cancel nine in ten, run the clock out — CI measures **6.81x** that heap and **7.09x** `IndexedPriorityQueue`, which cancels for real, falling to 2.56x at a thousand. Taken apart, `Cancel` and `Drain` are where it wins and **`Schedule` is where it loses — roughly 2x slower than a heap insert at 100,000**, because appending to a contiguous array beats writing a scattered slot head and a back-link. Also traded: a finite **horizon** (2^32 ticks by default, about 49 days at a millisecond), **no ordering** within a fired batch, and about a fifth more allocated per round than either heap. This is a large-population type — at a thousand timers `Cancel` is 2.2x slower and the tick-by-tick drive 1.8x slower.

### Examples

#### Which of these pending things have timed out — TimerWheel

`TimerWheel<TValue>` is a container of pending deadlines with `O(1)` `Cancel` and amortized-`O(1)` `Schedule`. It owns no thread and no clock: you drive it with `Advance`, and everything due comes back at once. The BCL answer is a `PriorityQueue`, which **cannot cancel** cheaply — its `Remove` (.NET 9+) is `O(n)` — so the workaround is lazy deletion, and the heap fills with timers that will never fire.

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

## Probabilistic & bit-level

- `BloomFilter<T, THasher>` — **probabilistic** membership: bit-array storage, **no false negatives**, tunable false-positive rate, a fraction of a `HashSet<T>`'s memory. Add-and-test only.
- `CuckooFilter<T, THasher>` — **probabilistic** membership that **supports deletion**: fingerprint buckets, **no false negatives**, tunable false-positive rate, ≤2 cache lines per lookup. The Bloom filter you can `Remove` from.
- `XorFilter<T, THasher>` — **probabilistic** membership that is **build-once & immutable**: ~9.84 bits/element (smaller than a Bloom filter at the same rate), three probes + two XORs per lookup (branch-free). The smallest, fastest-to-query filter for a fixed element set.
- `BitSet` — dense, **exact** bit vector in 64-bit words: `O(n/64)` hardware popcount (`Count`) and SIMD bulk `And`/`Or`/`Xor`/`Not`. A faster, count-aware `BitArray`.
- `BitPackedArray` — a **fixed-width packed integer array**: every element stored in exactly `w` bits (1 to 64), end to end in 64-bit words, with `O(1)` branch-free reads and in-place writes. A million 20-bit codes take **2.5 MB instead of an `int[]`'s 4 MB** (1.6× smaller); reads cost ~1.6–1.7× and writes ~2× the array's. Values wider than `w` are rejected, not truncated. .NET's only sub-byte storage is `BitArray` (one bit per element) and `BitVector32` (a few fields in one 32-bit integer).
- `RankSelectBitVector` — **immutable** succinct index over a dense bit vector: `Rank(i)` (set bits below `i`) in `O(1)` and `Select(k)` (position of the `k`-th set bit) in `O(log n)`, for 25% space over the bits. The BCL has no rank or select anywhere, so the baseline is a hand-rolled `O(i/64)` popcount loop. **Build-once** — any mutation means rebuilding the index, so keep mutating vectors in `BitSet` and snapshot once they settle.
- `EliasFanoSequence` — an **immutable, compressed sorted sequence** of non-negative `long` values (posting lists, record and byte offsets, monotone timestamps) in about `floor(log2(u / n)) + 4` bits a value, select indexes included: **6.3× smaller than the `long[]`** on offsets spaced ~100 apart, with random access and a lower-bound query (`CountLessThan`, `TryGetLowerBound`) that is **1.78× faster than `Array.BinarySearch`** at a million values. The price is reading: a random `this[i]` is ~12.7× the array's, and a sequential pass ~4.6×. The BCL has no compressed integer sequence of any kind.
- `HyperLogLog<T, THasher>` — **probabilistic** cardinality estimator: counts *distinct* elements from a fixed ~16&#160;KB of registers (~0.8% error), never growing with the data. Mergeable.
- `SlidingHyperLogLog<T, THasher>` — **probabilistic** distinct count over a **time window** ("unique users in the last 5 minutes"): `Add(item, timestamp)`, then `EstimateCardinality(since)` for any window chosen at query time, from a fixed ~1.7&#160;MB (~1.6% error). Out-of-order adds, exact merges, and bit-for-bit HyperLogLog's answer over the in-window elements.
- `MinHashSketch<T, THasher>` — **probabilistic** similarity sketch: estimates the *Jaccard similarity* of two sets from a fixed 2&#160;KB per set (standard error up to ~0.031 for large sets; not a hard bound), comparing in `O(registers)` however large the sets are. Mergeable.
- `CountMinSketch<T, THasher>` — **probabilistic** frequency estimator: estimates per-element counts from a fixed grid, **never underestimating** (overestimate bounded by `epsilon · TotalCount`). Mergeable.
- `TopKSketch<T, THasher>` — **probabilistic** top-k / heavy-hitters sketch (Space-Saving): reports a stream's most frequent elements from a fixed `k` monitors in `O(k)` memory, **never underestimating** and never missing a hitter above `TotalCount / k`.

### Examples

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

`SlidingHyperLogLog` is the same estimator with a **time axis**: each add carries a timestamp, and each query names the start of its window, so one sketch answers "distinct in the last minute", "in the last hour" and "ever" alike. Memory is fixed, about 1.7&#160;MB at the default precision 12. Adds may arrive out of order, and `UnionWith` merges node sketches exactly.

```csharp
var active = new SlidingHyperLogLog<long, Int64Murmur3Hasher>();
foreach (var e in events)
    active.Add(e.UserId, e.Timestamp.Ticks);
long now = DateTime.UtcNow.Ticks;
Console.WriteLine(active.EstimateCardinality(now - TimeSpan.FromMinutes(5).Ticks)); // distinct users, last 5 min
```

`MinHashSketch` estimates how much two sets **overlap** — the Jaccard similarity `|A ∩ B| / |A ∪ B|` — from a fixed 2&#160;KB per set, with a standard error of up to ~0.031 for large sets at the default precision. That is a dispersion, not a hard bound, so leave a margin around a threshold. Comparing two sketches is `O(registers)` however large the sets were, so build one per document once and compare sketches pairwise. Add-only; merge equal-precision sketches with `UnionWith`.

```csharp
var a = new MinHashSketch<string, StringXxHash64Hasher>(shinglesOfDocA);
var b = new MinHashSketch<string, StringXxHash64Hasher>(shinglesOfDocB);
Console.WriteLine(a.EstimateJaccard(b)); // ≈ |A ∩ B| / |A ∪ B|, from 2 KB each
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

`BitSet` is a dense, exact bit vector — see [the API reference](api/collections.md#bitset) for popcount, the SIMD bulk operators, and the set-bit enumerator.

`RankSelectBitVector` is the **build-once** succinct index over such a vector — see [the API reference](api/collections.md#rankselectbitvector) for the full surface. `Rank(i)` returns how many bits are set below `i` in `O(1)`, and `Select(k)` returns the position of the `k`-th set bit in `O(log n)`. Nothing in .NET offers either, so the alternative is the `O(i/64)` popcount loop a caller writes by hand — at the midpoint of a 100-million-bit vector, ~780,000 iterations replaced by two index loads and one masked popcount. The index costs 25% over the bits and is **invalidated by any mutation**, so mutate a `BitSet` and snapshot it once the bits have settled.

```csharp
var present = new BitSet(rowCount);
foreach (int row in nonNullRows) present[row] = true;

var index = new RankSelectBitVector(present);   // freeze, then query
int denseSlot = index.Rank(row);                // logical row -> dense slot, O(1)
int logicalRow = index.Select(denseSlot);       // and back, O(log n)
```

`EliasFanoSequence` applies the same machinery to a **sorted list of 64-bit integers**. It stores the low bits of each value verbatim and the high bits as a unary-coded bit vector, so a million offsets spaced ~100 apart take about 1.27 MB instead of a `long[]`'s 8 MB. It keeps random access, and the lower-bound query beats `Array.BinarySearch` (1.78× at a million values). Random reads are about 12.7× slower than the array's, so keep the array when memory is not the constraint. See [the API reference](api/collections.md#eliasfanosequence).

```csharp
var offsets = new EliasFanoSequence(recordOffsets);   // sorted, non-negative longs; repeats allowed
long start = offsets[123_456];                        // where record 123,456 starts
// Which record holds this byte: the last one starting at or before it. (byteOffset + 1 would wrap at
// long.MaxValue, where every record starts at or before the byte.)
int record = byteOffset == long.MaxValue ? offsets.Count - 1 : offsets.CountLessThan(byteOffset + 1) - 1;
```

`BitPackedArray` is for integers that are **not sorted** but have a known range below a primitive width: dictionary-encoded column codes, quantized features, ids below `2^w`. Each element takes exactly `w` bits, so a million 20-bit values take 2.5 MB rather than an `int[]`'s 4 MB, and every element is still read and written in place in `O(1)` — at about 1.6–1.7× the array's cost per read and 2× per write. A value wider than `w` throws instead of being silently truncated.

```csharp
var codes = new BitPackedArray(1_000_000, BitPackedArray.BitsRequired(699_999));  // 20 bits each
codes[0] = 123_456;
ulong code = codes[0];
Span<ulong> block = stackalloc ulong[256];
codes.GetRange(0, block);                        // unpack a block of rows in one pass
```
