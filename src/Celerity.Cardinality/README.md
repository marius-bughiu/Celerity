# Celerity.Cardinality

Mergeable, deterministic **distinct-count** (approximate `COUNT(DISTINCT)`) over managed streams — plus a removable **dedup filter** for sliding windows. Built on [Celerity](https://github.com/marius-bughiu/Celerity)'s `HyperLogLog`, `CeleritySet`, and `CuckooFilter`, generic over your key type and a zero-cost inlined hasher.

```bash
dotnet add package Celerity.Cardinality
```

## `Distinct` — exact when small, fixed-memory when large

Counting distinct values with a `HashSet<T>` stores every value — tens of GB and an eventual OOM at high cardinality. `Distinct` holds an exact `CeleritySet` while the count is small (so you get a *precise* answer for free), then promotes to a fixed ~16 KB `HyperLogLog` once it crosses a threshold — a **run-vs-cannot-run** difference: it estimates a billion distinct keys from the same 16 KB it uses for a thousand.

```csharp
using Celerity.Cardinality;

var uniques = new StringDistinct();
foreach (var e in events)
    uniques.Add(e.UserId);

long n = uniques.Count();          // exact while small, ~0.8% error once large
bool exact = uniques.IsExact;      // true until it promotes
```

Mergeable for cross-shard / cross-window roll-ups — exact when both stay small, otherwise a HyperLogLog union that is **byte-identical across runtimes** given a deterministic hasher, so shard sub-totals combine to the same global estimate everywhere:

```csharp
shardA.Merge(shardB);              // COUNT(DISTINCT) across shards, from ~16 KB each
long global = shardA.Count();
```

Generic over any key type:

```csharp
var byId = new Distinct<long, Int64WangNaiveHasher>();
```

## `DedupFilter` — windowed, removable dedup

When HyperLogLog's error is unacceptable and you need a per-key "have I seen this?" that can also expire, `DedupFilter` (a Cuckoo filter) gives fixed-memory dedup with **no false negatives** and — unlike a Bloom filter — **removal**, so it fits a sliding window:

```csharp
var dedup = new StringDedupFilter(expectedItems: 100_000);

if (dedup.TryMarkSeen(evt.Id))     // true the first time, false for a repeat
    Process(evt);

dedup.Remove(agedOutId);           // keep the fill bounded to the live window
```

## Why managed (and not a native binding / Redis)

The hot loop is per-element hashing of a managed key through an inlined struct hasher — tens of millions of events/sec that a native HyperLogLog would have to marshal one at a time, and that Redis `PFADD` would send over the wire per element. Cross-shard merge **requires** byte-identical register computation, so a fixed managed hasher that hashes identically on every RID is a genuine correctness feature. These run in trimmed / AOT / WASM sidecars where a per-RID native `.so` is pure tax.

Part of the [Celerity](https://github.com/marius-bughiu/Celerity) family.
