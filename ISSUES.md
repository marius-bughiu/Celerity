# Known Issues / Issue Backlog

This file is a snapshot of the issue backlog for Celerity, maintained alongside the source tree until the repository's GitHub Issues are populated. Each entry is written so it can be copy-pasted into a GitHub issue. `type` follows the convention used in `ROADMAP.md`.

---

## #1 — `IntDictionary<TValue>` ignores `capacity` and `loadFactor` constructor arguments

- **type**: bug
- **severity**: medium
- **milestone**: 0.2.0
- **status**: fixed in 0.2.0

### Description

`IntDictionary<TValue>` is a convenience subclass of `IntDictionary<TValue, Int32WangNaiveHasher>`. Its constructor accepts `capacity` and `loadFactor` parameters but forwards to the base constructor with no arguments (`: base()`), so user-supplied values are silently discarded and the dictionary is always created with the defaults.

### Repro

```csharp
var map = new IntDictionary<int>(capacity: 1024, loadFactor: 0.5f);
// Expected: underlying storage sized for 1024 with loadFactor 0.5
// Actual:   underlying storage sized for DEFAULT_CAPACITY (16) with DEFAULT_LOAD_FACTOR (0.75)
```

### Fix

Forward both arguments to the base constructor: `: base(capacity, loadFactor)`.

---

## #2 — `IntDictionary` cannot store the key `0`

- **type**: bug
- **severity**: high
- **milestone**: 0.2.0
- **status**: fixed in 0.2.0

### Description

`IntDictionary` reserves `EMPTY_KEY = 0` as the "empty slot" sentinel. The legitimate key value `0` is therefore indistinguishable from an unused slot. Inserting `map[0] = x` appears to succeed but:

- `ContainsKey(0)` returns `false`.
- `map[0]` throws `KeyNotFoundException`.
- `Count` is incorrect after inserts/removes involving key `0`.
- Re-inserting an existing key `0` will double-count once the `0` value collides with an unused slot, then under-count on removal — corrupting the load-factor math and eventually the internal table.

This is the single most important correctness bug in the library.

### Repro

```csharp
var map = new IntDictionary<string>();
map[0] = "zero";
Console.WriteLine(map.ContainsKey(0)); // prints False — wrong
Console.WriteLine(map[0]);             // throws KeyNotFoundException — wrong
```

### Fix

Store the `0` key out-of-band: a `bool _hasZeroKey` flag plus a dedicated `TValue? _zeroValue` slot. All CRUD paths short-circuit on key `0`. This is the same pattern used by `fastutil`, `HPPC`, and other mature primitive-keyed hashmap libraries.

---

## #3 — `CelerityDictionary` cannot store `default(TKey)`

- **type**: bug
- **severity**: high
- **milestone**: 0.2.0
- **status**: fixed in 0.2.0

### Description

Same root cause as #2, generalized. `CelerityDictionary` uses `EqualityComparer<TKey>.Default.Equals(_keys[index], default(TKey))` to test for "empty slot," so the key value `default(TKey)` cannot be stored:

- For `int` keys, key `0` is lost.
- For `long` keys, key `0L` is lost.
- For `Guid` keys, `Guid.Empty` is lost.
- For `string` or any reference-type key, the `null` key is lost (BCL `Dictionary<,>` also rejects `null`, but silently corrupting is worse than throwing).

### Fix

A `bool _hasDefaultKey` flag plus a dedicated `TValue? _defaultKeyValue` slot. CRUD operations short-circuit on `EqualityComparer<TKey>.Default.Equals(key, default(TKey))`.

---

## #4 — No CI workflow for PRs

- **type**: infra
- **severity**: medium
- **milestone**: 0.2.0
- **status**: fixed in 0.2.0

### Description

`.github/workflows/release.yml` only runs on `workflow_dispatch`, so PRs and pushes to `main` are not validated. Contributors can merge code that doesn't build or breaks tests without any automated signal.

### Fix

Add `.github/workflows/ci.yml` that runs `dotnet restore`, `dotnet build --no-restore`, and `dotnet test --no-build --verbosity normal` on every push to `main` and every pull request.

---

## #5 — Missing `TryGetValue` API

- **type**: feature
- **severity**: low
- **milestone**: 0.2.0
- **status**: fixed in 0.2.0

### Description

Both dictionaries expose `this[key]` which throws on missing keys. That's a footgun for the common "check and read" pattern, and forces users to double-probe via `ContainsKey` + indexer.

### Fix

Add `bool TryGetValue(TKey key, out TValue? value)` on both `CelerityDictionary` and `IntDictionary`, following BCL semantics.

---

## #6 — Missing `Clear` API

- **type**: feature
- **severity**: low
- **milestone**: 0.2.0
- **status**: fixed in 0.2.0

### Description

There is no way to reset a dictionary without allocating a new one. For pooled / reused map instances this is a hole.

### Fix

Add `void Clear()` on both dictionaries that resets `_count`, clears the `_keys` / `_values` arrays, and resets any default-key bookkeeping added in #2 / #3.

---

## #7 — Thin test coverage

- **type**: test
- **severity**: medium
- **milestone**: 0.2.0 and ongoing
- **status**: fixed in 0.2.0

### Description

The existing test suite covers happy-path inserts, overwrites, and one resize. It does not cover:

- Storing key `0` / `default(TKey)` (the bugs above).
- Removing keys followed by inserting the same keys (the re-hash after remove path).
- Collisions forced via a stub `IHashProvider` that returns a constant hash.
- Multi-resize behavior (insert many items, remove half, insert more).
- Load-factor boundary conditions.

### Fix

Added across two sessions:
- `IntDictionaryCollisionTests.cs` and `CelerityDictionaryCollisionTests.cs` — forced-collision, remove-reinsert, string/null-key, and multi-resize tests.
- `LoadFactorBoundaryTests.cs` — low load factor (0.5), high load factor (0.95), multiple sequential resizes, default/zero-key coexistence with load-factor accounting, and a parameterized Theory across {0.25, 0.5, 0.75, 0.95} for both dictionaries.

---

## #8 — No `CONTRIBUTING.md` / `CHANGELOG.md`

- **type**: docs
- **severity**: low
- **milestone**: 0.2.0
- **status**: fixed in 0.2.0

### Description

Contributors have no single place to read the build/test instructions or the release history. The copilot-instructions file is useful but not contributor-facing.

### Fix

Add `CONTRIBUTING.md` (build, test, conventions, PR process) and `CHANGELOG.md` (Keep a Changelog format).

---

## #21 — No constructor validation on `capacity` or `loadFactor`

- **type**: bug
- **severity**: high
- **milestone**: 0.2.0
- **status**: fixed in 0.2.0

### Description

Neither `CelerityDictionary` nor `IntDictionary` validates constructor arguments. Invalid values cause silent, severe runtime failures:

- `loadFactor >= 1.0`: The resize threshold exceeds the array size, so `ProbeForInsert` loops forever once every slot is occupied — an infinite loop.
- `loadFactor <= 0`: The threshold is 0 (or negative, cast to 0-ish), so every single insert triggers a `Resize()`, doubling the array each time and eventually causing `OutOfMemoryException`.
- `capacity < 0`: Harmless in practice (`NextPowerOfTwo` clamps to 1), but the caller clearly made a mistake and deserves a loud error.

### Repro

```csharp
// Infinite loop — hangs forever on the 17th insert
var map = new IntDictionary<int>(capacity: 16, loadFactor: 1.0f);
for (int i = 1; i <= 17; i++)
    map[i] = i;
```

### Fix

Both constructors now throw `ArgumentOutOfRangeException` for `capacity < 0`, `loadFactor <= 0`, or `loadFactor >= 1`. Regression tests in `ConstructorValidationTests.cs` cover both dictionaries and the convenience subclass.

---

## Backlog (post-0.2.0)

- **#9** — Implement `IReadOnlyDictionary<TKey, TValue>` (0.3.0). Requires `Keys`, `Values`, and `GetEnumerator()` first.
- **#10** — Add `Keys` / `Values` / `GetEnumerator()` (0.3.0). Next item to tackle.
- **#11** — Add `Add` / `TryAdd` with duplicate-throwing semantics (0.3.0). Status: `fixed in 0.3.0`.
- **#12** — `Int32Murmur3Hasher`, `Int64WangHasher`, `GuidHasher`, `UInt32Hasher`, `UInt64Hasher` (0.4.0). Status: `fixed in 1.1.0` — `UInt32Hasher`, `UInt64Hasher`, `GuidHasher`, `Int32Murmur3Hasher`, and `Int64WangHasher` all complete.
- **#13** — `DefaultHasher<T>` fallback to `EqualityComparer<T>.Default.GetHashCode()`. Status: `fixed in 1.1.0`.
- **#14** — Expanded benchmark suite: uniform vs clustered vs adversarial key distributions (0.4.0).
- **#15** — `CeleritySet<T, THasher>` and `IntSet`. Status: `fixed in 1.1.0`.
- **#16** — `SmallDictionary<TKey, TValue>` optimized for `n <= ~16` (0.5.0).
- **#17** — `FrozenCelerityDictionary` with perfect hashing for string keys (0.5.0).
- **#18** — Robin Hood probing experiment (0.6.0).
- **#19** — SIMD-accelerated probing experiment (0.6.0).
- **#20** — Struct-of-arrays layout experiment (0.6.0).
