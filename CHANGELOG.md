# Changelog

All notable changes to Celerity are documented here. This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Version numbers are produced by MinVer from the `v`-prefixed git tags.

## [Unreleased]

### Added

- `IntDictionary.GetEnumerator()`, `IntDictionary.Keys`, and `IntDictionary.Values` — struct-based, allocation-free enumeration over an `IntDictionary<TValue, THasher>`. `Keys` and `Values` expose `KeyCollection` / `ValueCollection` readonly structs, each with their own struct enumerator, so `foreach (var kvp in map)` / `foreach (int k in map.Keys)` / `foreach (var v in map.Values)` do not box. The out-of-band zero-key entry is yielded first. The enumerators track a `_version` counter and throw `InvalidOperationException` on `MoveNext` / `Reset` if the dictionary is mutated mid-enumeration, matching BCL `Dictionary<,>` semantics. First step toward implementing `IReadOnlyDictionary<int, TValue>` (#10). `CelerityDictionary` will get the equivalent surface in a follow-up.
- `Int32Murmur3Hasher` in `Celerity.Hashing` — Murmur3 32-bit finalizer ("fmix32") for `int` keys. Struct hasher, `AggressiveInlining`. Provides excellent avalanche properties; prefer over `Int32WangNaiveHasher` when key distribution is clustered or adversarial. Maps `0 → 0` (fixed point of fmix32).
- `Int64WangHasher` in `Celerity.Hashing` — Thomas Wang 64-bit integer hash for `long` keys. Struct hasher, `AggressiveInlining`. Faster than `Int64Murmur3Hasher` while providing better avalanche than a simple XOR-fold; prefer when throughput matters more than adversarial collision resistance. Invertible (bijective on `ulong`) so truncation to 32 bits is the only source of collisions.
- `Int32Murmur3HasherTests` — exact anchor values for key extremes, determinism, high-bit avalanche check, 1000-value distinctness sweep, and integration tests driving `CelerityDictionary` and `CeleritySet` including the `default(int)` out-of-band slot.
- `Int64WangHasherTests` — exact anchor values for key extremes, determinism, high-bit avalanche check, 1000-value distinctness sweep, and integration tests driving `CelerityDictionary` and `CeleritySet` including the `default(long)` out-of-band slot.
- `GuidHasher` in `Celerity.Hashing` — reinterprets the 128-bit `Guid` as two 64-bit halves, runs Murmur3 `fmix64` on each, and XORs the mixed halves. Struct hasher, `AggressiveInlining`, zero-allocation (no stack buffer — reinterpret via `Unsafe.As<Guid, ulong>`). Prefer over `DefaultHasher<Guid>` on hot paths: fully inlineable and avoids the `EqualityComparer<T>.Default` virtual dispatch.
- `GuidHasherTests` — `Guid.Empty → 0` anchor, determinism across calls and struct instances, avalanche on both the low and high 64-bit halves, shared-prefix/shared-suffix divergence (guards against hashers that weight one half too heavily), two 1000-value distinctness sweeps (sequential low-half keys and `Guid.NewGuid()`), and integration tests confirming `GuidHasher` satisfies the hasher constraint on `CeleritySet<Guid,THasher>` and `CelerityDictionary<Guid,TValue,THasher>` (including the `Guid.Empty` out-of-band slot).
- `UInt32Hasher` in `Celerity.Hashing` — Wang/Jenkins-style bit-mixer for `uint` keys. Struct hasher, `AggressiveInlining`. Counterpart to `Int32WangNaiveHasher`.
- `UInt64Hasher` in `Celerity.Hashing` — Murmur3 `fmix64` finalizer for `ulong` keys. Struct hasher, `AggressiveInlining`. Counterpart to `Int64Murmur3Hasher`.
- `UInt32HasherTests` and `UInt64HasherTests` — exact-value cases (including values crossing the sign bit), determinism, avalanche on the top bit, and a 1000-value distinctness sweep for the 64-bit mixer.
- `DefaultHasher<T>` in `Celerity.Hashing` — a general-purpose `IHashProvider<T>` that delegates to `EqualityComparer<T>.Default.GetHashCode()`. Use it when no specialized hasher exists for a type (e.g. `Guid`, custom structs, or reference types). It is a struct, so the JIT devirtualizes the outer call on the probe path; the inner `EqualityComparer<T>` dispatch is unavoidable but acceptable for non-hot-path types.
- XML doc comments added to `IHashProvider<T>`, `Int32WangNaiveHasher`, `Int64Murmur3Hasher`, and `StringFnV1AHasher`. All public hasher types now carry full XML documentation.
- `DefaultHasherTests` — verifies BCL contract equivalence for int, string, and Guid keys; determinism across calls and struct instances; and integration tests confirming `DefaultHasher<T>` satisfies the hasher constraints on `CeleritySet<T,THasher>`, `IntSet<THasher>`, and `CelerityDictionary<TKey,TValue,THasher>`.
- `Add(TKey, TValue)` on `CelerityDictionary` and `IntDictionary` — inserts a key/value pair and throws `ArgumentException` if the key already exists, matching BCL `Dictionary<,>` semantics.
- `TryAdd(TKey, TValue)` on `CelerityDictionary` and `IntDictionary` — inserts without overwriting; returns `true` on success, `false` if the key already exists.  Both methods correctly handle the zero/default-key out-of-band slot.
- `TryGetValue(TKey, out TValue?)` on `CelerityDictionary` and `IntDictionary`, following BCL semantics.
- `Clear()` on `CelerityDictionary` and `IntDictionary` — resets the map without releasing the backing arrays, so pooled/reused instances don't pay an allocation on every generation.
- `.github/workflows/ci.yml` — `dotnet build` and `dotnet test` now run automatically on every push to `main` and every pull request.
- `ROADMAP.md` — prioritized plan through 1.0.
- `ISSUES.md` — snapshot of the known issue backlog.
- `CONTRIBUTING.md` — build, test, and PR conventions.
- `CHANGELOG.md` — this file.
- Forced-collision test suites for both `IntDictionary` and `CelerityDictionary` using a constant-hash `IHashProvider`, exercising insert, overwrite, remove, remove-then-reinsert, and resize under maximum probing pressure.
- String-key tests for `CelerityDictionary` covering the `null` default-key path (`null` insert, remove, `TryGetValue`, `Clear`).
- Remove-then-reinsert stress test for `CelerityDictionary` with the standard hasher (parity with the existing `IntDictionary` test).
- Load-factor boundary test suite (`LoadFactorBoundaryTests.cs`) covering low load factor (0.5), high load factor (0.95), multiple sequential resizes from a tiny initial capacity, default/zero-key coexistence with the resize threshold, and a parameterized Theory across `{0.25, 0.5, 0.75, 0.95}` for both `IntDictionary` and `CelerityDictionary`. Closes the remaining gap from issue #7.

### Fixed

- **`IntDictionary<TValue>` constructor arguments were silently discarded.** The convenience subclass `IntDictionary<TValue>` accepted `capacity` and `loadFactor` parameters but forwarded to `: base()` with no arguments, so every instance was created with the defaults regardless of what the caller passed. It now forwards `capacity` and `loadFactor` to the base constructor.
- **`IntDictionary` could not store the key `0`.** `EMPTY_KEY = 0` was used as the "empty slot" sentinel, which collided with the legitimate key value `0`. `map[0] = x` appeared to succeed but subsequent `ContainsKey(0)`, `map[0]`, and `Count` returned wrong answers. The zero key is now stored out-of-band via a dedicated flag + value slot, a pattern borrowed from `fastutil` / `HPPC`.
- **`CelerityDictionary` could not store `default(TKey)`.** Same root cause as above, generalized: `default(int)` / `default(long)` / `default(Guid)` / `null` strings were all lost. Fixed the same way, via a `_hasDefaultKey` flag and a dedicated value slot.

- Constructor validation test suite (`ConstructorValidationTests.cs`) covering rejection of invalid `loadFactor` (≤0, ≥1) and negative `capacity` for both `IntDictionary` and `CelerityDictionary`, plus acceptance of valid edge values.

### Fixed (additional)

- **`CelerityDictionary` and `IntDictionary` accepted invalid constructor arguments.** `loadFactor >= 1.0` caused an infinite loop in `ProbeForInsert` once the table was full; `loadFactor <= 0` caused a resize on every insert. Both constructors now throw `ArgumentOutOfRangeException` for `capacity < 0`, `loadFactor <= 0`, or `loadFactor >= 1`.

### Changed

- The `IntDictionary` `EMPTY_VALUE` field is now `static readonly` instead of an instance field. No behavior change; just removes per-instance overhead.

## [0.1.0] - initial releases

Initial public versions, including `CelerityDictionary<TKey, TValue, THasher>`, `IntDictionary<TValue>`, the `Int32WangNaiveHasher`, `Int64Murmur3Hasher`, and `StringFnV1AHasher` hash providers, and the BenchmarkDotNet benchmark suite comparing `CelerityDictionary` against the BCL `Dictionary<int, int>`. See the git history under tags `v0.1.*` for specifics.

[Unreleased]: https://github.com/marius-bughiu/Celerity/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/marius-bughiu/Celerity/releases/tag/v0.1.0
