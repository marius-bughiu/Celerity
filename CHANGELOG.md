# Changelog

All notable changes to Celerity are documented here. This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Version numbers are produced by MinVer from the `v`-prefixed git tags.

## [Unreleased]

### Added

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
