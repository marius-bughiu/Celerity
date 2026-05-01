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
- Added `CONTRIBUTING.md`, `CHANGELOG.md`, `ROADMAP.md`, `ISSUES.md`. Status: `done`.

## Milestone 1.1.0 — API parity, hashers, and benchmarks

The next release rounds out the `Celerity.Collections` package with missing collection types, expands the hasher library, and stands up CI benchmark tracking.

### Collections

- Implement `CeleritySet<T, THasher>` — set counterpart to `CelerityDictionary`. (#16) Status: `done`.
- Implement `LongDictionary<TValue>` — `IntDictionary` equivalent for `long` keys. (#17) Status: `done`.
- Implement `IReadOnlyDictionary<TKey, TValue>` on `CelerityDictionary` and `IntDictionary`. Status: `done`.
- Add `Keys` / `Values` enumerable views and `GetEnumerator()` on the dictionaries. Status: `done`.
- Constructor accepting `IEnumerable<KeyValuePair<TKey, TValue>>`. Status: `done`.
- Add `GetEnumerator()` and `IEnumerable<T>` conformance on the sets (`IntSet`, `CeleritySet`). (#23) Status: `done`.

### Hashers

- Add `Int32Murmur3Hasher`, `Int64WangHasher`, `GuidHasher`, `UInt32Hasher`, `UInt64Hasher`. (#24) — all `done`.
- Add `DefaultHasher<T>` fallback to `EqualityComparer<T>.Default.GetHashCode()`.

### Infrastructure

- Set up `github-action-benchmark` for continuous performance tracking. (#1)
- Create hash function evaluator for comparing distribution quality. (#2)
- Comprehensive benchmark suite: uniform, clustered, and adversarial key distributions. (#26)
- Cross-platform testing (Windows, Linux, macOS). (#28)
- Improve code coverage. (#29)
- Improve documentation. (#15)
- Bump XML doc coverage; treat missing docs as warning-as-error. Status: `done` — `Celerity.csproj` now promotes CS1591 to error via `<WarningsAsErrors>$(WarningsAsErrors);CS1591</WarningsAsErrors>`. Library was already at 100% public-symbol doc coverage at the time of the change; this is a guardrail for future PRs.

## Milestone 1.2.0 — Performance & advanced collections

Focus on raw performance and specialized collection types that serve more advanced use cases.

### Collections

- `FrozenCelerityDictionary` — build-once, read-many variant with perfect hashing for string keys, comparable in spirit to `System.Collections.Frozen` but tunable via `IHashProvider<T>`. (#22)
- `CelerityMultiMap<TKey, TValue, THasher>` — multi-value dictionary. (#18)

### Performance

- Robin Hood hashing experiment as alternative to linear probing.
- Performance optimizations across existing collections. (#27)
- Native AOT support and trimming compatibility. (#32)

## Milestone 2.0.0 — Multi-package restructure

Split the monolithic `Celerity.Collections` into focused packages mirroring the .NET package structure. This is a breaking change in packaging (not necessarily in API).

### Package split

- `Celerity.Collections` — dictionaries, sets, and specialized collections
- `Celerity.Hashing` — `IHashProvider<T>`, built-in hashers, hash evaluation tools
- `Celerity.Primitives` — low-level utilities (`FastUtils`, bit manipulation, etc.)

### New collections

- Specialized collections for domain-specific workloads (e.g. graph traversal, spatial indexing). (#30)
- Memory-pooled collections for zero-allocation hot paths. (#21)
- SIMD-accelerated probing (SSE2/AVX2) similar to Swiss Tables / `F14`. (#23)
- Struct-of-arrays layout experiment for cache-friendly memory access.

### Infrastructure

- Multi-target `net8.0;net9.0`.
- Publish a results dashboard so users can track performance over time.

## Non-goals

- We are **not** trying to replace `Dictionary<,>` in every scenario. Celerity trades flexibility for speed on specific shapes; that tradeoff must be documented, not hidden.
- We are **not** a thread-safe collections library. Callers that need concurrency should compose with locks or use `ConcurrentDictionary<,>`.
- We are **not** a serialization library. Celerity collections should be straightforward to serialize via System.Text.Json / MessagePack, but we won't ship formatters ourselves.
- We are **not** a general-purpose data structures library. If a collection doesn't beat the BCL on a documented benchmark, it doesn't belong here.
