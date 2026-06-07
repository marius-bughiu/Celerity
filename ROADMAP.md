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
- `Celerity.Hashing` — hash providers, hash evaluation utilities (positioned on *distribution quality, determinism, and zero-cost devirtualization* — not on beating `GetHashCode()` for speed; see milestone 1.6.0)
- `Celerity.Primitives` — low-level utilities that fill genuine BCL gaps: `FastMod`/`FastDiv`, struct PRNGs, span varint, integer digit-count, fast/compliant GUID, alignment/bit-packing (see milestone 2.1.0; we deliberately do not reimplement what `BitOperations`/`TensorPrimitives` already inline)

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

## Milestone 1.6.0 — Hasher performance audit & honest positioning

A correctness-of-claims pass on the hashing layer, prompted by the observation that **the hashers are not necessarily faster than `GetHashCode()`** — and, for `int` keys, *cannot* be (`int.GetHashCode()` is identity, i.e. zero work). The real value of the struct hashers is **distribution quality (avalanche), determinism, adversarial resistance, and the zero-cost devirtualized generic** — not raw hashing speed. This milestone makes the benchmarks and the docs tell that honest story. It ships in the current single package, before the 2.0.0 restructure.

- Benchmark hashers **end-to-end through the dictionaries** (insert/lookup across uniform / sequential / clustered / adversarial key distributions), reporting collision rate and avg/max probe length — not just an isolated `Hash()` loop. The clustered/adversarial cases are where a strong hasher wins end-to-end even though it "loses" the isolated microbench. Status: `done` — `HasherEndToEndBenchmark` (extended suite) times every integer hasher through `IntDictionary` for insert + lookup across all four key shapes vs the BCL `Dictionary`, and the new public `ProbeStatisticsEvaluator` / `ProbeStatistics` ([`docs/api/hashing.md`](docs/api/hashing.md#end-to-end-probe-analysis)) replays the real open-addressed linear-probing placement to report average / worst-case probe length and the open-addressing collision rate (surfaced as a deterministic `--probe-analysis` markdown report and a measured table in [`docs/performance.md`](docs/performance.md#measure-probe-length-not-just-hash-speed)). The numbers show the cheap hashers winning on uniform/sequential keys and the naive fold collapsing on clustered/adversarial keys while the Wang/Murmur3 finalizers hold near a 1.75 average probe. Tracked in [#182](https://github.com/marius-bughiu/Celerity/issues/182).
- Make the isolated microbenchmarks honest: consume results (the identity `int` hash is otherwise dead-code-eliminated), add an `EqualityComparer<T>.Default` baseline (the realistic thing a dev replaces), and label identity/`GetHashCode()` as the **zero-work floor** no mixing hasher can beat. Status: `done` — every hasher microbenchmark already XOR-folds its codes into a returned value (BDN consumes it, so no DCE), and both `IntegerHasherBenchmark` (`{Type}_EqualityComparer` per `int`/`long`/`uint`/`ulong`/`Guid`) and `StringHasherBenchmark` (`EqualityComparer_Default`) now carry an `EqualityComparer<T>.Default.GetHashCode()` baseline arm alongside the direct `GetHashCode()` one — the per-probe call a BCL `Dictionary<,>` actually makes. The class remarks label the microbenchmarks a raw-mixing-cost diagnostic and the `int`/`long` identity/`_Bcl` rows the zero-work floor; the new arms auto-render on the gh-pages **Hash function throughput** dashboard. Tracked in [#183](https://github.com/marius-bughiu/Celerity/issues/183).
- Reposition the hasher docs/README away from "faster hashing" toward distribution/avalanche/determinism, with an honest "choosing a hasher" guide (the speed-vs-quality curve, the F14/ahash/FxHash framing, the Marvin32 string-determinism tradeoff, and the caveat that **fixed-seed hashers are not a HashDoS defence**). Status: `done` — `README.md` reframes the "up to 2.4× faster" headline as a *collection-layout* win independent of the hasher, and both `README.md` and `docs/api/hashing.md` lead with distribution/determinism and carry an explicit HashDoS caveat (fixed-seed hashers are not a flooding defence; what stops flooding is a keyed PRF with a secret, per-process-random key, so BCL Marvin32 is the safe default for untrusted string keys). `hashing.md`'s "Choosing a hasher" section gains a speed-vs-quality-curve framing block, and the benchmark docs across `hashing.md` / `docs/performance.md` reframe the isolated `Hash()` sweeps as a raw-mixing-cost diagnostic. Tracked in [#184](https://github.com/marius-bughiu/Celerity/issues/184).
- Add explicit identity/passthrough integer hashers (`Int32IdentityHasher` / `Int64IdentityHasher`) as the zero-work floor, and document the rule: uniform/trusted keys → skip mixing; clustered/adversarial keys → mix. Status: `done` — `Int32IdentityHasher` (`Hash(key) => key`) and `Int64IdentityHasher` (`Hash(key) => (int)key`) ship as the labelled floor of the integer hasher ladder (no mixing hasher beats identity on speed; the value of the struct hashers is distribution/determinism, not hashing speed), are exercised as `*_Identity` rows in `IntegerHasherBenchmark`, and carry the skip-vs-mix decision rule plus the open-addressed-table-sensitivity and not-a-HashDoS-defence caveats in `docs/api/hashing.md` and the README. Library defaults are unchanged (identity is opt-in). Tracked in [#185](https://github.com/marius-bughiu/Celerity/issues/185).

## Milestone 2.0.0 — Multi-package restructure

Split the monolithic `Celerity.Collections` into focused packages mirroring the .NET package structure. This is a breaking change in packaging (not necessarily in API). The *new collections* and *infrastructure* work below has shipped; the **package restructure itself is the remaining, defining work of this milestone** and is now tracked as issues.

### Package split

Still a single `net8.0` assembly with `<PackageId>Celerity.Collections</PackageId>`; the hashers already live under the `Celerity.Hashing` namespace but ship inside the collections package. The split is a packaging breaking change (namespaces unchanged → source-compatible).

- `Celerity.Collections` — dictionaries, sets, and specialized collections.
- `Celerity.Hashing` — `IHashProvider<T>`, built-in hashers, `HashQualityEvaluator`. Status: `planned`. Tracked in [#186](https://github.com/marius-bughiu/Celerity/issues/186).
- `Celerity.Primitives` — low-level utilities, seeded with `FastUtils`; content expansion is milestone 2.1.0. Status: `planned`. Tracked in [#187](https://github.com/marius-bughiu/Celerity/issues/187).
- Preserve back-compat for existing `Celerity.Collections` consumers (meta-package and/or `[TypeForwardedTo]`). Status: `planned`. Tracked in [#188](https://github.com/marius-bughiu/Celerity/issues/188).
- CI: build, pack, and publish three packages with shared MinVer versioning. Status: `planned`. Tracked in [#190](https://github.com/marius-bughiu/Celerity/issues/190).

### New collections

- Specialized collections for domain-specific workloads (e.g. graph traversal, spatial indexing). Status: `done` — four specialized types shipped and [#30](https://github.com/marius-bughiu/Celerity/issues/30) was closed as substantially complete (the two remaining checklist entries, `StringDictionary` and `StructDictionary`, were descoped as redundant with `CelerityDictionary`'s existing string-hasher surface and zero-boxing struct-key support — per the guiding rule, a type that doesn't beat the BCL on a documented workload doesn't ship). `BloomFilter<T, THasher>` is a probabilistic membership filter with bit-array storage, no false negatives, and a tunable false-positive rate, sizing `m` / `k` from the expected element count and deriving its `k` bit probes from a single `IHashProvider<T>` call by double hashing. `BitSet` is its exact, deterministic counterpart: a dense fixed-length bit vector packed into 64-bit words with `O(n/64)` hardware-popcount cardinality (`Count`) and SIMD-accelerated bulk `And` / `Or` / `Xor` / `Not`, a faster, count-aware alternative to `System.Collections.BitArray`. `HyperLogLog<T, THasher>` is the probabilistic cardinality estimator: it counts the number of *distinct* elements in a stream of any size from a fixed array of `2^precision` one-byte registers (16&#160;KB by default) with a ~0.8% relative standard error and no growth with the data, derives its 64-bit hash from a single `IHashProvider<T>` call by SplitMix64 avalanche, applies linear counting for small cardinalities, and merges equal-precision estimators with `UnionWith` for distributed counting. `CountMinSketch<T, THasher>` completes the streaming-sketch trio (membership → cardinality → frequency): it estimates *how many times* each element occurs from a fixed `depth × width` grid of counters sized from an `epsilon` / `delta` error budget, **never underestimates** (overestimates bounded by `epsilon · TotalCount` with confidence `1 − delta`), derives its `depth` counter columns from a single `IHashProvider<T>` call by double hashing, and merges equally-sized sketches with `UnionWith` for distributed heavy-hitter / frequency counting. Tracked in [#30](https://github.com/marius-bughiu/Celerity/issues/30) (closed).
- Memory-pooled collections for zero-allocation hot paths. Status: `done` — `PooledCelerityDictionary<TKey, TValue, THasher>` is a drop-in, `IDisposable` peer of `CelerityDictionary` whose backing key/value arrays are rented from `ArrayPool<T>.Shared` and returned on `Dispose` (and on every internal resize), recycling buffers across build/use/dispose cycles to cut Gen 0 / LOH pressure on hot paths that rebuild dictionaries frequently. It tracks its logical power-of-two capacity independently of the (possibly over-provisioned) rented array length, clears reference-type buffers on return to prevent leaks, and throws `ObjectDisposedException` after disposal. Tracked in [#21](https://github.com/marius-bughiu/Celerity/issues/21).
- SIMD-accelerated probing (SSE2/AVX2) similar to Swiss Tables / `F14`. Status: `done` — shipped as a new opt-in collection type, `SwissDictionary<TKey, TValue, THasher>`, a drop-in peer of `CelerityDictionary` that keeps a parallel one-byte control-tag array so a single portable `Vector128` compare tests a whole 16-slot group per probe, filtering candidates by a 7-bit hash fragment before any key comparison; deletion uses tombstones reclaimed by an occasional rehash. The default is unchanged — this is an additional type for lookup-heavy workloads (large tables, many negative lookups, clustered keys), at the cost of one control byte per slot. Tracked in [#64](https://github.com/marius-bughiu/Celerity/issues/64).
- Struct-of-arrays layout experiment for cache-friendly memory access. Status: `done` — shipped as a new opt-in collection type, `HashCachingDictionary<TKey, TValue, THasher>`, a drop-in peer of `CelerityDictionary` that keeps a dense side array of 32-bit hash fingerprints alongside the parallel key/value arrays. A probe scans only that compact metadata buffer and dereferences a key (running the full equality check) only on a fingerprint match, so cache-cold lookups and lookups with expensive key equality short-circuit on a single integer compare; because the forced occupied bit sits above the table mask, the cached fingerprint also yields the slot index directly, so a resize re-homes every entry without recomputing a single hash. The default is unchanged — this is an additional type for lookup-dominated / costly-equality workloads, complementary to the SIMD-probing `SwissDictionary` (#64), at the cost of four bytes of metadata per slot. Tracked in [#65](https://github.com/marius-bughiu/Celerity/issues/65).

### Infrastructure

- Multi-target `net8.0;net9.0` (evaluate `net10.0`) across all three packages, so newer-runtime consumers get TFM-gated optimizations (AVX-512 SIMD paths, JIT improvements). Status: `planned`. Tracked in [#189](https://github.com/marius-bughiu/Celerity/issues/189).
- Publish a results dashboard so users can track performance over time. Status: `done` — the core per-commit dashboard and the weekly extended dashboard are published to `gh-pages` and linked from the site nav.

## Milestone 2.1.0 — Celerity.Primitives: fast math & low-level utilities

The "fast-utils" expansion that fills `Celerity.Primitives` with specialized BCL alternatives. Comprehensive research against the current .NET (8/9/10) surface shows the BCL has closed most classic gaps — `System.Numerics.BitOperations`, `System.Numerics.Tensors.TensorPrimitives`, `Convert.ToHexString`, `System.Buffers.Text.Base64`, and generic-math `Math` already inline to optimal/SIMD code. So this milestone ships only the **defensible white space**, each with a documented BCL-beating workload (the hard rule), and deliberately **does not** reinvent what the BCL already does well.

### Ship — real gaps with a documented workload

- `FastMod` / `FastDiv` — Lemire reciprocal modulo & division by a runtime-constant divisor (the BCL's `HashHelpers.FastMod` is `internal`-only); 2–4× over `%`/`/` for repeated mod by the same divisor (hash buckets, ring buffers, sharding). Status: `done` — shipped on `FastUtils` with 32-bit (`uint` → `ulong` multiplier) and 64-bit (`ulong` → `UInt128` multiplier) overloads: `GetFastModMultiplier` precomputes the `ceil(2^W / d)` reciprocal once, then `FastMod` / `FastDiv` reduce each operation to a widening multiply + shift. `FastMod` is exact for every value and `divisor >= 1`; `FastDiv` for `divisor >= 2` (the `divisor == 1` multiplier overflows to 0 — a documented call-site guard). Correctness is fuzzed against `%` / `/` across both widths (representative + extreme divisors, boundary + random + exhaustive-low-range dividends), benchmarked vs the hardware operators in the extended-suite `FastModBenchmark`, documented in [`docs/api/utilities.md`](docs/api/utilities.md#fastmod--fastdiv) and the README, and exercised by the Native AOT smoke test. Tracked in [#191](https://github.com/marius-bughiu/Celerity/issues/191).
- Struct PRNG suite — value-type, allocation-free, inlinable, seed-deterministic xoshiro256\*\* / xoroshiro128+ / SplitMix64 (+wyrand/PCG). `System.Random` is a heap class behind virtual dispatch and its **seeded** path falls back to the legacy Knuth algorithm. Curated, no marginal variants. Status: `done` — `Celerity.Primitives.SplitMix64` / `Xoshiro256StarStar` / `Xoroshiro128Plus` / `WyRand` / `Pcg32` ship as mutable `struct`s implementing a one-method `IRandomSource` (`ulong NextUInt64()`); the shared `NextUInt32` / `NextDouble` / `NextSingle` / `NextBool` / bounded-and-unbiased (Lemire) `NextInt` / `NextInt64` / `NextBytes` surface is built once over the interface as `ref this` extension methods constrained to `where TRng : struct, IRandomSource`, so it devirtualizes, inlines, and runs zero-cost over any generator (e.g. a generic Fisher–Yates shuffle). Every constructor is explicitly seeded and deterministic; the multi-word generators expand the seed through `SplitMix64` so every seed (including `0`) is valid. Cross-checked against independent reimplementations and the published SplitMix64 seed-0 vector, covered family-wide by `RandomSourceContractTests`, benchmarked vs seeded/shared `System.Random` in the extended-suite `PrngBenchmark`, documented in [`docs/api/utilities.md`](docs/api/utilities.md#struct-prngs) and the README, and exercised by the Native AOT smoke test. Tracked in [#192](https://github.com/marius-bughiu/Celerity/issues/192).
- Span-based varint codec — LEB128 + zig-zag `Try(Write|Read)` over spans (the BCL's 7-bit-encoded int is only on `BinaryReader`/`BinaryWriter`, stream-bound and allocating). Status: `planned`. Tracked in [#193](https://github.com/marius-bughiu/Celerity/issues/193).
- Integer digit-count / `Log10` — public `CountDigits` (the BCL's LZCNT-based one is `internal`); for buffer sizing and column alignment. Status: `planned`. Tracked in [#194](https://github.com/marius-bughiu/Celerity/issues/194).
- Fast non-crypto GUID v4 (from the struct PRNG) + RFC-9562 big-endian v7 (sortable, DB-friendly; the BCL's `CreateVersion7` uses a non-big-endian layout that bloats DB indexes). Status: `planned`. Tracked in [#195](https://github.com/marius-bughiu/Celerity/issues/195).
- Alignment helpers + span bit-packing over caller-owned memory (`AlignUp`/`AlignDown`/`IsAligned`, span bit get/set/scan/popcount), distinct from the owning `BitSet` collection. Status: `planned` (low priority). Tracked in [#196](https://github.com/marius-bughiu/Celerity/issues/196).

### Research — verify the win before shipping

- Fused/specialized SIMD reductions **not** covered by `TensorPrimitives` (simultaneous min+max, integer histogram, overflow-checked sum) — spike, ship only the winners. Status: `planned`. Tracked in [#197](https://github.com/marius-bughiu/Celerity/issues/197).
- Guaranteed-branchless conditional `Select` — verify the JIT actually branches (it already emits `cmov` for `Math.Min`/`Max`/`Abs`/`Clamp`) before shipping. Status: `planned`. Tracked in [#198](https://github.com/marius-bughiu/Celerity/issues/198).

### Explicitly out of scope — the BCL already does these well

Per the guiding rule, these are **not** worth shipping because they already inline to optimal/SIMD code: next-power-of-two / `IsPow2` / `Log2` / `PopCount` / `LeadingZeroCount` / `TrailingZeroCount` / `RotateLeft`/`Right` (`System.Numerics.BitOperations`); SIMD `Sum`/`Min`/`Max`/`Dot`/`IndexOf`/`Contains` (`TensorPrimitives`, generic over `INumber<T>`, + `MemoryExtensions`/`SearchValues`); hex and Base64 encode/decode (`Convert.ToHexString`, `System.Buffers.Text.Base64`, AVX-512); byte-swap/endianness (`BinaryPrimitives`); branchless `Min`/`Max`/`Abs`/`Clamp` (JIT `cmov`); and generic xxHash/CRC span hashing (`System.IO.Hashing` — depend on it rather than reimplement).

## Non-goals

- We are **not** trying to replace `Dictionary<,>` in every scenario. Celerity trades flexibility for speed on specific shapes; that tradeoff must be documented, not hidden.
- We are **not** a thread-safe collections library. Callers that need concurrency should compose with locks or use `ConcurrentDictionary<,>`.
- We are **not** a serialization library. Celerity collections should be straightforward to serialize via System.Text.Json / MessagePack, but we won't ship formatters ourselves.
- We are **not** a general-purpose data structures library. If a collection doesn't beat the BCL on a documented benchmark, it doesn't belong here.
