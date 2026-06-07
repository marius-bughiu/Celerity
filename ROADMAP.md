# Celerity Roadmap

This document tracks the planned direction for the Celerity project. It is a living document maintained by the project maintainers and updated as priorities shift.

Status legend: `planned`, `in-progress`, `done`, `deferred`.

## Guiding Principles

1. **Correctness first.** A "fast" collection that returns wrong answers is worthless. Every optimization must be covered by tests.
2. **Zero-cost abstractions.** Hashers are structs, generic constraints are `struct, IHashProvider<T>`, so the JIT can devirtualize and inline. This is a hard rule.
3. **Parity with BCL where it makes sense.** `TryGetValue`, `Clear`, enumeration, `IReadOnlyDictionary<TKey, TValue>` — users should be able to drop Celerity in wherever they use `Dictionary<,>`.
4. **Benchmark every perf claim.** The README's numbers are the contract. If a change regresses them, it ships only with a written justification.
5. **Document the tradeoffs.** Celerity is not "always faster." Each collection should document the workloads where it wins and where it loses.

## Vision

Celerity currently ships a single NuGet package (`Celerity.Collections`). Long-term, the project will expand into a family of focused packages — each targeting a specific area where specialized, high-performance implementations can outperform the BCL in niche scenarios. The package structure will mirror the .NET ecosystem's own organization:

- `Celerity.Collections` — dictionaries, sets, and specialized collection types
- `Celerity.Hashing` — hash providers, hash evaluation utilities
- `Celerity.Primitives` — low-level utilities (e.g. fast math, bit manipulation)

Each package will remain narrowly scoped: if a type doesn't offer a measurable performance advantage over its BCL counterpart in at least one documented workload, it doesn't ship.

## Completed milestones

### 1.0.0 / 1.0.1 — Stability & correctness

- Fixed `IntDictionary<TValue>` constructor argument forwarding bug. Status: `done`.
- Fixed `IntDictionary` key-`0` corruption. Status: `done`.
- Fixed `CelerityDictionary` `default(TKey)` corruption. Status: `done`.
- Added constructor validation (`capacity`, `loadFactor`). Status: `done`.
- Added `TryGetValue`, `Clear`, `Add`, `TryAdd` on both dictionaries. Status: `done`.
- Stood up CI workflow (`.github/workflows/ci.yml`). Status: `done`.
- Comprehensive test suites: collision tests, load-factor boundary tests, constructor validation. Status: `done`.
- Added `CONTRIBUTING.md`, `CHANGELOG.md`, `ROADMAP.md`. Status: `done`.

## Milestone 1.1.0 — API parity, hashers, and benchmarks

The next release rounds out the `Celerity.Collections` package with missing collection types, expands the hasher library, and stands up CI benchmark tracking.

### Collections

- Implement `CeleritySet<T, THasher>` — set counterpart to `CelerityDictionary`. Status: `done`.
- Implement `LongDictionary<TValue>` — `IntDictionary` equivalent for `long` keys. Status: `done`.
- Implement `LongSet<T>` — `IntSet` equivalent for `long` values; completes the dictionary-to-set parity (`IntDictionary` → `IntSet`, `CelerityDictionary` → `CeleritySet`, `LongDictionary` → `LongSet`). Status: `done` — shipped in v1.3.0.
- Implement `IReadOnlyDictionary<TKey, TValue>` on `CelerityDictionary` and `IntDictionary`. Status: `done`.
- Add `Keys` / `Values` enumerable views and `GetEnumerator()` on the dictionaries. Status: `done`.
- Constructor accepting `IEnumerable<KeyValuePair<TKey, TValue>>`. Status: `done`.
- Add `GetEnumerator()` and `IEnumerable<T>` conformance on the sets (`IntSet`, `CeleritySet`, `LongSet`). Status: `done`.

### Hashers

- Add `Int32Murmur3Hasher`, `Int64WangHasher`, `GuidHasher`, `UInt32Hasher`, `UInt64Hasher` — all `done`.
- Add `DefaultHasher<T>` fallback to `EqualityComparer<T>.Default.GetHashCode()`. Status: `done`.

### Infrastructure

- Set up `github-action-benchmark` for continuous performance tracking. Status: `done` — `benchmarks` job in `ci.yml` runs the full suite on every PR and pushes results to `gh-pages` on `main`, with PR comments and a 200% regression fail-threshold.
- Create hash function evaluator for comparing distribution quality. Status: `done` — `HashQualityEvaluator.Evaluate<T, THasher>(keys, bucketCount)` returns a `HashQualityReport` (collision count / rate, bucket occupancy, max bucket load, chi-squared, and a normalized distribution score) so callers can compare candidate hashers for a given key shape offline. See [`docs/api/hashing.md`](docs/api/hashing.md#hash-quality-evaluation). Tracked in [#2](https://github.com/marius-bughiu/Celerity/issues/2).
- Comprehensive benchmark suite: uniform, clustered, and adversarial key distributions. Status: `done` — `DistributionBenchmark` sweeps uniform/sequential/clustered shapes and `AdversarialHasherBenchmark` shows the naive hasher degrading to O(n) while Murmur3 recovers. Tracked in [#60](https://github.com/marius-bughiu/Celerity/issues/60).
- Benchmark suite expansion: realistic workloads, memory-allocation, concurrent-access, cache-locality, large-dataset (millions), and `FrozenDictionary<,>` comparison benchmarks. Status: `done` — added as an extended, on-demand suite kept out of the per-PR CI regression run; see [`docs/performance.md`](docs/performance.md#extended-benchmark-suite). Tracked in [#26](https://github.com/marius-bughiu/Celerity/issues/26).
- Cross-platform testing (Windows, Linux, macOS). Status: `done`.
- Improve code coverage. Status: `done` — coverage reporting is gated in CI (`coverage.yml`, 100% line coverage on the library, rendered by an in-repo generator and published to the [coverage dashboard](https://marius-bughiu.github.io/Celerity/coverage/)), edge-case tests close the non-generic enumerator / throw / backward-shift corners, property-based parity tests (CsCheck) and a seedable differential fuzzer (`Celerity.Fuzz`, nightly soak) check every collection against its BCL oracle, and the approach is written up in [`docs/testing.md`](docs/testing.md). Tracked in [#29](https://github.com/marius-bughiu/Celerity/issues/29).
- Improve documentation. Status: `done` — added a performance tuning guide, a BCL migration guide, a troubleshooting guide, and a FAQ, alongside the existing README usage examples, "choosing a collection" table, and API reference. Tracked in [#15](https://github.com/marius-bughiu/Celerity/issues/15).
- Bump XML doc coverage; treat missing docs as warning-as-error. Status: `done` — `Celerity.csproj` promotes CS1591 to error.

## Milestone 1.2.0 — Performance & advanced collections

Focus on raw performance and specialized collection types that serve more advanced use cases.

### Collections

- `FrozenCelerityDictionary` — build-once, read-many variant with perfect hashing for string keys, comparable in spirit to `System.Collections.Frozen` but tunable via `IHashProvider<T>`. Status: `done` — `FrozenCelerityDictionary<TValue>` / `<TValue, THasher>` search for a collision-free single-probe layout at construction and fall back to linear probing when the chosen hasher collides two keys' raw codes, so lookups are always correct. Tracked in [#62](https://github.com/marius-bughiu/Celerity/issues/62).
- Frozen collections family — the set counterpart `FrozenCeleritySet` / `FrozenCeleritySet<THasher>` completes the build-once read-many family (`FrozenCelerityDictionary` → `FrozenCeleritySet`), sharing the same perfect-hash-with-linear-probing-fallback build and implementing `IReadOnlySet<string>`. Status: `done`. Tracked in [#22](https://github.com/marius-bughiu/Celerity/issues/22).
- `CelerityMultiMap<TKey, TValue, THasher>` — multi-value dictionary. Status: `done` — a one-to-many map that reuses `CelerityDictionary`'s open-addressed key table and stores a `List<TValue?>` value group per key; `Add` appends rather than overwrites, `Remove(key, value)` / `RemoveAll(key)` are the two removal shapes, the indexer returns an empty group for an absent key, and the type implements `ILookup<TKey, TValue?>`. Tracked in [#18](https://github.com/marius-bughiu/Celerity/issues/18).
- `SmallDictionary<TKey, TValue>` — flat-array implementation optimized for `n <= ~16`. Status: `done` — `SmallDictionary<TKey, TValue>` linear-scans insertion-dense parallel arrays with `EqualityComparer<TKey>.Default` (no hasher, so the default key is stored inline rather than out-of-band), trading `O(1)` for `O(n)` to win at small `n`; it implements `IReadOnlyDictionary<TKey, TValue?>` with the full dictionary surface. Tracked in [#61](https://github.com/marius-bughiu/Celerity/issues/61).

### Performance

- Robin Hood hashing experiment as alternative to linear probing. Status: `done` — shipped as a new collection type, `RobinHoodDictionary<TKey, TValue, THasher>`, a drop-in peer of `CelerityDictionary` that uses Robin Hood open addressing (per-slot probe sequence length, displace-the-richer-resident inserts, backward-shift-with-PSL-decrement deletes) to bound probe-length variance and keep worst-case lookups close to the average on clustered / adversarial keys; negative lookups terminate early via the PSL invariant. The default is unchanged — this is an additional opt-in type for the clustered case, not a replacement (the per-slot PSL `int` and extra insert work make it a wash or a slight loss on uniform keys). Tracked in [#63](https://github.com/marius-bughiu/Celerity/issues/63).
- Performance optimizations across existing collections.
- Native AOT support and trimming compatibility. Status: `done` — the library is marked `<IsAotCompatible>true</IsAotCompatible>` (trim + AOT analyzers run on every build), and a Native AOT publish smoke test runs the full collection / hasher surface as a native binary in CI. See [`docs/aot.md`](docs/aot.md). An AOT-vs-JIT benchmark comparison remains a follow-up. Tracked in [#32](https://github.com/marius-bughiu/Celerity/issues/32).

## Milestone 2.0.0 — Multi-package restructure

Split the monolithic `Celerity.Collections` into focused packages mirroring the .NET package structure. This is a breaking change in packaging (not necessarily in API).

### Package split

- `Celerity.Collections` — dictionaries, sets, and specialized collections
- `Celerity.Hashing` — `IHashProvider<T>`, built-in hashers, hash evaluation tools
- `Celerity.Primitives` — low-level utilities (`FastUtils`, bit manipulation, etc.)

### New collections

- Specialized collections for domain-specific workloads (e.g. graph traversal, spatial indexing). Status: `in-progress` — two specialized types ship so far. `BloomFilter<T, THasher>` is a probabilistic membership filter with bit-array storage, no false negatives, and a tunable false-positive rate, sizing `m` / `k` from the expected element count and deriving its `k` bit probes from a single `IHashProvider<T>` call by double hashing. `BitSet` is its exact, deterministic counterpart: a dense fixed-length bit vector packed into 64-bit words with `O(n/64)` hardware-popcount cardinality (`Count`) and SIMD-accelerated bulk `And` / `Or` / `Xor` / `Not`, a faster, count-aware alternative to `System.Collections.BitArray`. Remaining candidates on the issue checklist (`CountMinSketch`, `HyperLogLog`, `StringDictionary`, `StructDictionary`, …) are still planned. Tracked in [#30](https://github.com/marius-bughiu/Celerity/issues/30).
- Memory-pooled collections for zero-allocation hot paths. Status: `done` — `PooledCelerityDictionary<TKey, TValue, THasher>` is a drop-in, `IDisposable` peer of `CelerityDictionary` whose backing key/value arrays are rented from `ArrayPool<T>.Shared` and returned on `Dispose` (and on every internal resize), recycling buffers across build/use/dispose cycles to cut Gen 0 / LOH pressure on hot paths that rebuild dictionaries frequently. It tracks its logical power-of-two capacity independently of the (possibly over-provisioned) rented array length, clears reference-type buffers on return to prevent leaks, and throws `ObjectDisposedException` after disposal. Tracked in [#21](https://github.com/marius-bughiu/Celerity/issues/21).
- SIMD-accelerated probing (SSE2/AVX2) similar to Swiss Tables / `F14`. Status: `done` — shipped as a new opt-in collection type, `SwissDictionary<TKey, TValue, THasher>`, a drop-in peer of `CelerityDictionary` that keeps a parallel one-byte control-tag array so a single portable `Vector128` compare tests a whole 16-slot group per probe, filtering candidates by a 7-bit hash fragment before any key comparison; deletion uses tombstones reclaimed by an occasional rehash. The default is unchanged — this is an additional type for lookup-heavy workloads (large tables, many negative lookups, clustered keys), at the cost of one control byte per slot. Tracked in [#64](https://github.com/marius-bughiu/Celerity/issues/64).
- Struct-of-arrays layout experiment for cache-friendly memory access. Status: `done` — shipped as a new opt-in collection type, `HashCachingDictionary<TKey, TValue, THasher>`, a drop-in peer of `CelerityDictionary` that keeps a dense side array of 32-bit hash fingerprints alongside the parallel key/value arrays. A probe scans only that compact metadata buffer and dereferences a key (running the full equality check) only on a fingerprint match, so cache-cold lookups and lookups with expensive key equality short-circuit on a single integer compare; because the forced occupied bit sits above the table mask, the cached fingerprint also yields the slot index directly, so a resize re-homes every entry without recomputing a single hash. The default is unchanged — this is an additional type for lookup-dominated / costly-equality workloads, complementary to the SIMD-probing `SwissDictionary` (#64), at the cost of four bytes of metadata per slot. Tracked in [#65](https://github.com/marius-bughiu/Celerity/issues/65).

### Infrastructure

- Multi-target `net8.0;net9.0`.
- Publish a results dashboard so users can track performance over time.

## Non-goals

- We are **not** trying to replace `Dictionary<,>` in every scenario. Celerity trades flexibility for speed on specific shapes; that tradeoff must be documented, not hidden.
- We are **not** a thread-safe collections library. Callers that need concurrency should compose with locks or use `ConcurrentDictionary<,>`.
- We are **not** a serialization library. Celerity collections should be straightforward to serialize via System.Text.Json / MessagePack, but we won't ship formatters ourselves.
- We are **not** a general-purpose data structures library. If a collection doesn't beat the BCL on a documented benchmark, it doesn't belong here.
