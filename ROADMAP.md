# Celerity Roadmap

This document tracks the planned direction for the Celerity high-performance collections library. It is a living document maintained by the project maintainers and updated as priorities shift.

Status legend: `planned`, `in-progress`, `done`, `deferred`.

## Guiding Principles

1. **Correctness first.** A "fast" collection that returns wrong answers is worthless. Every optimization must be covered by tests.
2. **Zero-cost abstractions.** Hashers are structs, generic constraints are `struct, IHashProvider<T>`, so the JIT can devirtualize and inline. This is a hard rule.
3. **Parity with BCL where it makes sense.** `TryGetValue`, `Clear`, enumeration, `IReadOnlyDictionary<TKey, TValue>` — users should be able to drop Celerity in wherever they use `Dictionary<,>`.
4. **Benchmark every perf claim.** The README's numbers are the contract. If a change regresses them, it ships only with a written justification.
5. **Document the tradeoffs.** Celerity is not "always faster." Each collection should document the workloads where it wins and where it loses.

## Milestone 0.2.0 — Correctness & foundations (current)

The first release after 0.1.x focuses on fixing real bugs exposed by reading the code carefully, filling in obvious API gaps, and standing up CI so future PRs get validated automatically.

### Bug fixes

- `IntDictionary<TValue>` parameterless-ctor-forwarding bug: the convenience subclass accepts `capacity` and `loadFactor` but its initializer calls `: base()`, silently discarding both arguments. Status: `in-progress`.
- `IntDictionary` cannot store the key `0`: `EMPTY_KEY = 0` is used as the "empty slot" sentinel, which collides with the legitimate key value `0`. Inserting key `0` corrupts `Count` and makes `ContainsKey(0)` and the indexer return wrong answers. Fix via a dedicated `_hasZeroKey` flag and separate value slot. Status: `in-progress`.
- `CelerityDictionary<TKey, TValue, THasher>` cannot store `default(TKey)`: same root cause as above, generalized. For value-type keys this means `0` / `0L` / `Guid.Empty`, for reference-type keys it means `null`. Fix via a `_hasDefaultKey` flag and a dedicated value slot. Status: `in-progress`.

### API additions

- `TryGetValue(TKey key, out TValue? value)` on both dictionaries.
- `Clear()` on both dictionaries.
- `ContainsKey` / `Remove` / `TryGetValue` / `Clear` / `Count` are all covered by unit tests, including the edge cases for the bug fixes above.

### Infrastructure

- Add `.github/workflows/ci.yml` running `dotnet restore`, `dotnet build --no-restore`, and `dotnet test --no-build` on every PR and push to `main`. The existing `release.yml` only runs on `workflow_dispatch`, so PRs are currently unverified.
- Add `CONTRIBUTING.md` documenting build/test steps, conventions, and the PR process.
- Add `CHANGELOG.md` following Keep a Changelog.

## Milestone 0.3.0 — API parity

- Implement `IReadOnlyDictionary<TKey, TValue>` on `CelerityDictionary` and `IntDictionary`.
- Add `Keys` / `Values` enumerable views.
- Add `Add(TKey, TValue)` that throws on duplicate keys (matching BCL semantics).
- Add `GetEnumerator()` returning `KeyValuePair<TKey, TValue>`.
- Add `TryAdd`.
- Ctor that accepts an `IEnumerable<KeyValuePair<TKey, TValue>>`.
- Bump the bar on XML doc coverage; treat missing docs as a warning-as-error in the main project.

## Milestone 0.4.0 — More hashers

- `Int32Murmur3Hasher` (currently only the 64-bit version exists).
- `Int64WangHasher` for symmetry with the 32-bit version.
- `GuidHasher`.
- `UInt32Hasher`, `UInt64Hasher`.
- A `DefaultHasher<T>` that falls back to `EqualityComparer<T>.Default.GetHashCode()` so `CelerityDictionary` can be used without a custom hasher for types we don't ship a specialized hasher for.
- Benchmark suite comparing hashers against each other and against `Dictionary<,>` across uniform, clustered, and adversarial key distributions.

## Milestone 0.5.0 — New collections

- `CeleritySet<T, THasher>` — set counterpart to `CelerityDictionary`.
- `IntSet` — set counterpart to `IntDictionary`.
- `SmallDictionary<TKey, TValue>` — a low-allocation dictionary for `n <= ~16` that uses a linear-scan array and upgrades to a hashed layout on overflow. Aims to beat `Dictionary<,>` for tiny, short-lived maps (a common hot path in parsers and AST visitors).
- `FrozenCelerityDictionary` — build-once, read-many variant with perfect-hashing for string keys, comparable in spirit to `System.Collections.Frozen` but tunable via `IHashProvider<T>`.

## Milestone 0.6.0 — Memory layout & probing

- Investigate Robin Hood hashing as an alternative to linear probing. Benchmark on clustered workloads where linear probing degrades.
- Investigate SIMD-accelerated probing (SSE2/AVX2) similar to Swiss Tables / `F14`.
- Explore a struct-of-arrays layout split `(int[] hashes, TKey[] keys, TValue[] values)` to avoid paying the `TKey?` nullability overhead for reference types.

## Milestone 1.0.0 — Stability

- Finalize the public API surface. No breaking changes after 1.0 without a major version bump.
- Commit to semantic versioning.
- Publish a results dashboard so users can see perf over time.
- Multi-target `net8.0;net9.0` once `net9.0` is the current LTS.

## Non-goals

- We are **not** trying to replace `Dictionary<,>` in every scenario. Celerity trades flexibility for speed on specific shapes; that tradeoff must be documented, not hidden.
- We are **not** a thread-safe collections library. Callers that need concurrency should compose with locks or use `ConcurrentDictionary<,>`.
- We are **not** a serialization library. Celerity collections should be straightforward to serialize via System.Text.Json / MessagePack, but we won't ship formatters ourselves in 1.0.
