# Performance Tuning Guide

Celerity is not magic — it is a set of deliberate tradeoffs. A Celerity collection that is mis-sized, paired with the wrong hasher, or used through a boxing interface can be *slower* than the BCL `Dictionary<,>` it replaced. This guide walks through the knobs that actually move the needle, roughly in the order they matter.

If you only read one section, read [Pick the right collection](#1-pick-the-right-collection) and [Pre-size the table](#3-pre-size-the-table-capacity). Those two decisions dominate everything else.

## TL;DR

| Knob | Default | When to change it |
|---|---|---|
| Collection type | — | Match the key type and access pattern — see the [README table](../README.md#choosing-a-collection). |
| `capacity` | `16` | Set it to (or above) the final element count when you know the size up front. |
| `loadFactor` | `0.75f` | Lower (e.g. `0.6`) to trade memory for fewer collisions; raise (e.g. `0.85`) to trade speed for density. |
| Hasher | per-type default | Escalate only when profiling shows clustering or you face adversarial keys. |
| Enumeration | struct `foreach` | Avoid the boxed `IReadOnlyDictionary<,>`/LINQ path on hot loops. |

## 1. Pick the right collection

The single biggest win is using the type whose layout matches your key. The specialized integer types (`IntDictionary`, `LongDictionary`, `IntSet`, `LongSet`) work directly on `int` / `long` keys with `==` comparisons and no `EqualityComparer<T>` dispatch — that is where the headline "up to 2.4× faster than `Dictionary<int,int>`" comes from. `CelerityDictionary<TKey, TValue, THasher>` is the generic fallback for every other key type.

| Access pattern | Use |
|---|---|
| `int` / `long` keyed, read + write | `IntDictionary<TValue>` / `LongDictionary<TValue>` |
| Any other key type, read + write | `CelerityDictionary<TKey, TValue, THasher>` |
| Built once, read many, `string` keys | `FrozenCelerityDictionary<TValue>` |
| One key → many values | `CelerityMultiMap<TKey, TValue, THasher>` |
| Membership only | `IntSet` / `LongSet` / `CeleritySet<T, THasher>` |

See the full decision table with rationale in the [README](../README.md#choosing-a-collection). If your workload isn't on it, the BCL collection is usually the right starting point.

## 2. Use the struct fast paths, not the boxed interface

Every Celerity dictionary ships a **struct** `Enumerator` and struct `KeyCollection` / `ValueCollection` views. A plain `foreach` binds to the struct enumerator and allocates nothing:

```csharp
foreach (var kvp in dict)          // struct enumerator — zero allocation
    Sum += kvp.Value;
```

The moment you go through `IReadOnlyDictionary<TKey, TValue?>`, `IEnumerable<>`, or most LINQ operators, the enumerator is **boxed once per enumeration** and you pay interface dispatch on every `MoveNext`. That is the same cost model as the BCL, but it erases Celerity's edge:

```csharp
IReadOnlyDictionary<int, string> boxed = dict;
foreach (var kvp in boxed) { ... }   // enumerator boxed once; dispatch per item

dict.Count(kvp => kvp.Value is null) // LINQ → boxed enumerator
```

Keep hot loops on the concrete type. Reach for the interface only at API boundaries where you genuinely need polymorphism.

## 3. Pre-size the table (capacity)

Celerity dictionaries use open addressing with **power-of-two** sizing. The `capacity` constructor argument is rounded up to the next power of two, and the table resizes (reallocating and re-probing every entry) once `Count` exceeds `capacity * loadFactor`.

If you know the final size, pass it. A dictionary that will hold 1,000 entries should not start at the default 16 and resize ~7 times on the way up:

```csharp
// Bad: starts at 16, resizes repeatedly as it fills to 1000.
var d = new IntDictionary<string>();
for (int i = 0; i < 1000; i++) d[i] = Work(i);

// Good: one allocation, no resizes.
var d = new IntDictionary<string>(capacity: 1024);
for (int i = 0; i < 1000; i++) d[i] = Work(i);
```

Account for the load factor when sizing: at the default `0.75`, a `capacity` of 1024 resizes once you pass 768 entries. To hold 1,000 entries without any resize, you need `capacity` ≥ ~1365 (which rounds to 2048), or a higher load factor.

**Building from an existing collection sizes itself.** The `IEnumerable<KeyValuePair<TKey, TValue>>` constructor reads `ICollection<T>.Count` when the source exposes it and pre-sizes the backing storage — **including the load-factor headroom** — so bulk-filling from a `List<>`, array, or BCL `Dictionary<,>` avoids resize work for free. Unlike the plain `capacity` constructor (where you account for the load factor yourself, per the note above), the source constructor scales the count up by `1 / loadFactor` for you, so the whole source lands below the resize threshold in a single allocation:

```csharp
var fast = new IntDictionary<string>(existingList);   // pre-sized from Count, no resize
```

For a non-`ICollection` enumerable (a LINQ query, a generator), `Count` is unknown — pass an explicit `capacity` if you can estimate the size.

## 4. Tune the load factor

`loadFactor` (default `0.75`, valid range `0 < loadFactor < 1`) is the fill ratio at which the table grows. It is a classic space-vs-time dial for open addressing:

- **Lower it (e.g. `0.6`)** to keep the table sparser. Fewer probe collisions → faster lookups and inserts, at the cost of more memory and earlier resizes.
- **Raise it (e.g. `0.85`)** to pack the table denser and save memory, accepting longer probe chains (more cache misses per lookup) as it fills.

```csharp
var sparse = new CelerityDictionary<string, int, StringFnV1AHasher>(
    capacity: 4096, loadFactor: 0.6f);   // latency-sensitive, memory-tolerant
```

`0.75` is a sound default; only move it with a benchmark in front of you. With linear probing, lookup cost climbs sharply as the load factor approaches 1, so values above ~0.9 are rarely worth it.

## 5. Choose the hasher deliberately

Hashers are `struct`s supplied as a generic constraint, so the JIT devirtualizes and inlines `Hash()` onto the probe path — the abstraction itself is free. What is *not* free is a hasher that clusters your keys: collisions lengthen probe chains and cost cache misses on every lookup.

The convenience types already pick sensible defaults — `Int32WangNaiveHasher` for `int`, `Int64WangNaiveHasher` for `long`, `StringFnV1AHasher` for `FrozenCelerityDictionary`. **Don't escalate without evidence.** A stronger hasher costs more per call; it only pays off if it removes enough collisions to offset that.

Escalation ladders (cheap → strong avalanche):

- **`int`:** `Int32WangNaiveHasher` → `Int32WangHasher` → `Int32Murmur3Hasher`
- **`long`:** `Int64WangNaiveHasher` → `Int64WangHasher` → `Int64Murmur3Hasher`
- **`string` (ASCII identifiers):** `StringFnV1AHasher` → `StringJenkinsOaatHasher` → `StringMurmur3Hasher` / `StringXxHash3Hasher`
- **`string`, untrusted/adversarial keys:** step to a keyed PRF — `StringHalfSipHash24Hasher`, `StringSipHash13Hasher`, or `StringSipHash24Hasher`.

The full hasher matrix, with the cost/avalanche rationale for every option, lives in [the README's hasher notes](../README.md#choosing-a-collection) and [`docs/api/hashing.md`](api/hashing.md).

### Measure distribution before you commit

Don't guess whether a hasher clusters *your* keys — measure it offline with `HashQualityEvaluator`:

```csharp
using Celerity.Hashing;

string[] keys = LoadRepresentativeKeys();
HashQualityReport report =
    HashQualityEvaluator.Evaluate<string, StringFnV1AHasher>(keys);

Console.WriteLine(report.CollisionRate);       // lower is better
Console.WriteLine(report.MaxBucketLoad);       // worst-case probe length driver
Console.WriteLine(report.DistributionScore);   // 1.0 == ideal uniform
```

Compare two candidate hashers on the same key sample and pick the one with the better distribution score *and* acceptable throughput — a fast hasher that clusters is not a win. See [hash quality evaluation](api/hashing.md#hash-quality-evaluation).

### Measure probe length, not just `Hash()` speed

`HashQualityEvaluator` scores the *distribution* of the codes; `ProbeStatisticsEvaluator` goes one step further and reports what a lookup actually pays inside Celerity's **open-addressed, linearly-probed** table — the **average and worst-case probe length** — by replaying the real placement (`index = hash & mask`, then linear probe on a collision):

```csharp
using Celerity.Hashing;

int[] keys = LoadRepresentativeKeys();
ProbeStatistics stats = ProbeStatisticsEvaluator.Evaluate<int, Int32WangNaiveHasher>(keys);

Console.WriteLine(stats.AverageProbeLength); // ~1.0 is ideal; higher means clustering
Console.WriteLine(stats.MaxProbeLength);     // the tail-latency driver
Console.WriteLine(stats.CollisionRate);      // fraction of keys not in their natural slot
```

This is the metric that explains the throughput numbers: an isolated `Hash()` microbench says a mixing hasher can never beat identity (for `int`, `GetHashCode()` is identity — zero work), yet on clustered or adversarial keys the cheap hasher's probe chains blow up and the strong finalizer wins *end-to-end*. The `Celerity.Benchmarks` project makes both halves visible:

```bash
cd src/Celerity.Benchmarks
dotnet run -c Release -- --probe-analysis              # deterministic probe table (no timing)
dotnet run -c Release -- --filter "*HasherEndToEnd*"   # the same hashers timed through the dictionary
```

The probe table is deterministic (it does not depend on timing or CI hardware), so it is stable enough to cite directly. A representative run over 10,000 `int` keys at the default 0.75 load factor — each cell is **average (worst-case)** probe length, `1.00 (1)` is ideal:

| Key shape | `Int32IdentityHasher` | `Int32WangNaiveHasher` | `Int32WangHasher` | `Int32Murmur3Hasher` |
|---|---|---|---|---|
| Uniform | 1.78 (29) | 1.76 (35) | 1.78 (39) | 1.78 (25) |
| Sequential | **1.00 (1)** | **1.00 (1)** | 1.78 (31) | 1.78 (31) |
| Clustered | 4891 (9892) | 103.6 (374) | **1.74 (33)** | 1.78 (36) |
| Adversarial | 1.00 (1) † | 5000 (10000) | 1.78 (35) | 1.73 (38) |

The story the numbers tell:

- On **uniform** keys every hasher behaves the same (~1.78 avg) — the input already carries entropy, so mixing changes nothing. Pick the cheapest.
- On **sequential** keys `Int32IdentityHasher` and `Int32WangNaiveHasher` are *perfect* (1.00 avg, 1 max — contiguous keys map to contiguous slots), while the strong finalizers **scatter** them and *add* collisions. Here mixing actively hurts: drop to identity/naive.
- On **clustered** keys the cheap hashers fall apart (the naive fold to a 103-avg / 374-max chain, identity to a catastrophic 4891-avg) while `Int32WangHasher` / `Int32Murmur3Hasher` stay near 1.75 — this is where escalating earns its cost.
- On **adversarial** keys the naive fold collapses completely (5000 avg, the whole table walked) and only the full finalizers survive.

> † The adversarial set is hand-built to defeat the *naive XOR-fold specifically*, so `Int32IdentityHasher` happens to pass it through cleanly — that is an artifact of this construction, **not** evidence that identity resists adversarial input. A fixed identity (or any fixed-seed) hasher is not a HashDoS defence; an attacker who knows the hasher can craft keys for *it*.

This is exactly why an isolated `Hash()` microbench is misleading: it would rank these hashers by raw speed (identity fastest, Murmur3 slowest), yet end-to-end the ranking *inverts* on clustered/adversarial keys. See [end-to-end probe analysis](api/hashing.md#end-to-end-probe-analysis).

## 6. Build-once, read-many → freeze it

If a `string`-keyed table is constructed once and then read for the rest of the process (route tables, config maps, interned vocabularies, lookup dictionaries), `FrozenCelerityDictionary<TValue>` pays a one-time construction cost to search for a **perfect (collision-free) hash**, after which every lookup is a single hash, a single array index, and a single equality check:

```csharp
var routes = new FrozenCelerityDictionary<int>(pairs);
Console.WriteLine(routes.IsPerfectlyHashed);  // True → single-probe lookups
```

Check `IsPerfectlyHashed`. When it is `true`, lookups are single-probe. When the default hasher collides two keys' raw codes it falls back to short linear probing (still correct, just not single-probe) — supplying a stronger hasher via `FrozenCelerityDictionary<TValue, THasher>` (e.g. `StringFnV1AFullHasher` for non-ASCII keys) often restores the perfect layout. The dictionary is immutable: there is no `Add` / `Remove`, which is exactly what makes the perfect-hash search worthwhile.

## 7. Benchmark on your own machine

The [live dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) tracks every collection against its BCL counterpart on each `main` push, with per-PR regression comparisons. It runs on hosted CI runners, which are **noisier than your laptop** — treat the dashboard as a trend and regression guard, not as the absolute number you will see in production.

For high-precision local numbers, run the BenchmarkDotNet suite in Release:

```bash
cd src/Celerity.Benchmarks
dotnet run -c Release
```

Filter to the comparison you care about:

```bash
# Just the string hasher throughput sweep (short-ASCII, long-ASCII, non-ASCII shapes)
dotnet run -c Release -- --filter "*HasherBenchmark*"
```

Read hasher throughput numbers **alongside** the `HashQualityReport` distribution metrics — the right hasher is the one that is both fast *and* uniform on your key shape. When you change `capacity`, `loadFactor`, or the hasher in production code, re-run the relevant benchmark; the README's published numbers are the contract, and a local benchmark is how you confirm a tuning change actually helped.

## Extended benchmark suite

The CI dashboard runs a deliberately **lean core suite** on every PR so the same-runner A/B regression comparison stays fast and low-variance. A second, heavier set of benchmarks lives in the same project but is **excluded from the CI run** — it exists to answer the questions a single random-key measurement can't, and you run it on demand. Each is a `*Benchmark` class you can target with `--filter`:

| Benchmark | Filter | What it measures |
|---|---|---|
| `DistributionBenchmark` | `*Distribution*` | Insert/Lookup across **uniform / sequential / clustered** key shapes (1k + 100k). The cheap XOR-fold hashers win on uniform/sequential keys; this is where you confirm that for *your* shape. |
| `AdversarialHasherBenchmark` | `*Adversarial*` | Keys engineered to collide under the naive hasher. Shows the probe chain degrading toward **O(n)** with `Int32WangNaiveHasher` and snapping back to O(1) with `Int32Murmur3Hasher` — the empirical case for choosing the right hasher. |
| `HasherEndToEndBenchmark` | `*HasherEndToEnd*` | Each integer hasher (identity / naive / Wang / Murmur3) driven **through the dictionary** for insert + lookup across **all four** key shapes (uniform / sequential / clustered / adversarial), vs the BCL `Dictionary<int,int>`. The honest companion to the isolated-`Hash()` `IntegerHasherBenchmark`: it measures what users feel (probe length + cache behaviour), so the cheap hashers win on uniform/sequential keys while only the strong finalizers survive the adversarial shape. |
| `LargeDatasetBenchmark` | `*LargeDataset*` | 1M and 5M items, where the working set spills out of cache and memory traffic dominates. |
| `MemoryAllocationBenchmark` | `*MemoryAllocation*` | Allocated bytes + GC counts for **grow-from-empty** vs **pre-sized** construction — the cost of *not* passing a capacity. |
| `ConcurrentAccessBenchmark` | `*ConcurrentAccess*` | Read scaling at 1/4/8 threads against `ConcurrentDictionary<,>`. Concurrent **reads** of a built-once, never-mutated Celerity map are safe and skip the concurrent-collection tax. |
| `CacheLocalityBenchmark` | `*CacheLocality*` | Same keys probed **in order** vs **shuffled** — isolates the cache-miss penalty and contrasts the parallel-array layout against the BCL entry-struct layout. |
| `LibraryComparisonBenchmark` | `*LibraryComparison*` | Lookups vs the BCL `FrozenDictionary<,>`, the strongest in-box read-optimized peer. (FASTER/Tsavorite is intentionally excluded — it's a log-structured store, not a drop-in dictionary.) |
| `RealWorldWorkloadBenchmark` | `*RealWorldWorkload*` | A mixed **~80% read / ~12% write / ~8% remove** stream with a hot 10% of keys — closer to a real cache than any single-op micro-benchmark. |

```bash
cd src/Celerity.Benchmarks
dotnet run -c Release -- --filter "*Distribution*"     # one class
dotnet run -c Release -- --filter "*Adversarial*" "*CacheLocality*"   # several
```

The adversarial and large-dataset benchmarks are intentionally slow (they demonstrate degradation and out-of-cache behaviour respectively); expect them to take minutes.

In CI, the extended suite runs **weekly** (and on demand via *Run workflow*) through [`benchmarks-extended.yml`](../.github/workflows/benchmarks-extended.yml), which invokes `dotnet run -c Release -- --ci-extended` and publishes the results to a **separate dashboard** at [`dev/bench-extended`](https://marius-bughiu.github.io/Celerity/dev/bench-extended/) (linked as **Extended** in the site nav). It is kept apart from the per-commit core dashboard on purpose: the extended numbers are a noisier, exploratory trend, not the regression gate, so they never feed the per-PR threshold.

## Checklist

1. ☐ Using the type that matches the key (`IntDictionary` for `int`, etc.)?
2. ☐ Pre-sized `capacity` to the known final count (accounting for load factor)?
3. ☐ Building from an `ICollection` source so it self-sizes?
4. ☐ Hot loops on the concrete struct type, not the boxed interface or LINQ?
5. ☐ Default hasher confirmed (or escalated) against a real key sample via `HashQualityEvaluator`?
6. ☐ Read-many `string` table frozen, with `IsPerfectlyHashed == true`?
7. ☐ Tuning change validated with a local Release benchmark?

## See also

- [Choosing a collection](../README.md#choosing-a-collection) — the decision table.
- [Hashing API reference](api/hashing.md) — every built-in hasher and the quality evaluator.
- [Migration guide](migration.md) — moving from BCL collections.
- [Troubleshooting](troubleshooting.md) and [FAQ](faq.md).
