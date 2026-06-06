# FAQ

Conceptual questions about Celerity — the "why does it work this way" behind the design. For error messages and fixes, see [Troubleshooting](troubleshooting.md). For tuning, see the [Performance guide](performance.md).

## General

### What is Celerity, in one sentence?

A .NET library of specialized high-performance collections that beat the BCL on specific key shapes and access patterns by trading away generality (thread safety, stable iteration order, the mutable `IDictionary<,>` interface).

### Is it really faster than `Dictionary<,>`?

On the shapes it targets, yes — **up to 2.4× faster than `Dictionary<int, int>`** on lookups, with zero allocations, tracked on the [live benchmark dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) against the BCL on every `main` push. It is **not** universally faster: used through the boxed interface, mis-sized, or paired with a clustering hasher, it can be slower. See the [performance guide](performance.md).

### Is Celerity a drop-in replacement for `Dictionary<,>`?

Almost, for the common API surface (indexer, `TryGetValue`, `Add`, `TryAdd`, `Remove`, `Clear`, `Count`, `Keys`, `Values`, enumeration). The differences that bite: it implements `IReadOnlyDictionary<TKey, TValue?>` not `IDictionary<,>`, values surface as `TValue?`, and iteration order is unspecified. The [migration guide](migration.md) covers each.

### Which collection should I use?

See the [decision table in the README](../README.md#choosing-a-collection). Short version: `IntDictionary`/`LongDictionary` for integer keys, `CelerityDictionary` for other keys (with a struct hasher), `FrozenCelerityDictionary` for build-once string lookups, `CelerityMultiMap` for one-to-many, the `*Set` types for membership-only.

## Thread safety

### Are Celerity collections thread-safe?

**No.** They are single-threaded by design — that's part of how they stay fast and allocation-free. Concurrent reads *and* writes will corrupt state.

### How do I use one across threads?

Either wrap it in your own lock, or use a BCL `ConcurrentDictionary<,>` for that workload. Concurrent multi-threaded access is an explicit [non-goal](../ROADMAP.md#non-goals). Many read-only-after-build scenarios are naturally safe: build the collection on one thread, publish it (e.g. via a `volatile` field or `FrozenCelerityDictionary`), then read from many threads — concurrent reads with no writer are fine.

## Behaviour

### Why is the iteration order different from `Dictionary<,>`, and is it stable?

Order reflects the open-addressed probe layout, so it differs from the BCL and is **unspecified** — it can change across versions or after a resize. Never depend on it; sort explicitly if you need a stable order.

### Why are values typed `TValue?` everywhere?

Because the public read interface is `IReadOnlyDictionary<TKey, TValue?>`. `TryGetValue` returns `default` (i.e. `null` for reference types) on a miss, and the nullable annotation makes that explicit. Once `TryGetValue` returns `true`, the value is present — assert with `!` or a null check.

### Does the zero key / `null` key / `Guid.Empty` work?

Yes. `default(TKey)` would normally collide with the empty-slot sentinel used during probing, so Celerity stores it **out-of-band** in a dedicated slot. `dict[0]`, `dict[null]`, and `dict[Guid.Empty]` round-trip correctly and appear in enumeration. This is one of the most-tested areas of the library.

### What happens when I `Add` a duplicate key?

`Add` throws `ArgumentException` (BCL parity). Use `TryAdd` (returns `false`, no overwrite) or the indexer set (overwrites) when duplicates are expected. The `IEnumerable` constructor also throws on duplicate keys in the source.

### Does `Remove` leave tombstones that degrade performance?

No. Removal uses **backward-shift deletion**: the following entries in the probe chain are shifted into the freed slot, so the table stays tombstone-free and lookup performance doesn't degrade after many removals — no periodic rehash needed.

## Hashers

### Why must hashers be structs?

So the JIT can **devirtualize and inline** the `Hash()` call. The constraint is `where THasher : struct, IHashProvider<T>`; calling through a generic type parameter constrained to a value type lets the JIT resolve the concrete method and inline it onto the probe path, eliminating virtual dispatch. A class hasher would reintroduce that per-probe cost. This is a hard design rule, not an incidental choice.

### Do I have to pick a hasher?

For `int` / `long` keys, no — `IntDictionary<TValue>`, `LongDictionary<TValue>`, `IntSet`, and `LongSet` default to a sensible naive Wang hasher. For other key types via `CelerityDictionary` / `CeleritySet` / `CelerityMultiMap`, yes — pass one from `Celerity.Hashing` (e.g. `GuidHasher`, `StringFnV1AHasher`) or `DefaultHasher<T>` for arbitrary types.

### Which string hasher should I use?

`StringFnV1AHasher` is the fast default for ASCII identifiers. Escalate only with evidence of clustering. For untrusted/adversarial keys, use a keyed PRF (`StringSipHash13Hasher` / `StringSipHash24Hasher` / `StringHalfSipHash24Hasher`). For non-ASCII keys, use a full-character-fold hasher like `StringFnV1AFullHasher`. The full matrix with rationale is in the [README](../README.md#choosing-a-collection) and [`hashing.md`](api/hashing.md).

### How do I know if my hasher is good for my keys?

Run a representative key sample through `HashQualityEvaluator.Evaluate<T, THasher>(keys)` offline. It returns a `HashQualityReport` with collision rate, max bucket load, chi-squared, and a normalized distribution score (`1.0` = ideal uniform). Compare candidates and pick the one that's both uniform and fast. See [hash quality evaluation](api/hashing.md#hash-quality-evaluation).

### Can I bring my own hash function?

Yes — implement `IHashProvider<T>` as a `struct` and pass it as the hasher type argument:

```csharp
struct MyHasher : IHashProvider<int> {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(int key) => key * 2654435761;   // Knuth multiplicative
}
```

The dictionary masks the result to the table size, so negative returns are fine. See [custom hashing](../README.md#custom-hashing).

## FrozenCelerityDictionary

### When should I use `FrozenCelerityDictionary`?

When a `string`-keyed table is built once and read many times for the rest of the process — route tables, config maps, interned vocabularies. It pays a one-time construction cost to find a perfect (collision-free) hash so each later lookup is single-probe. It's immutable: no `Add` / `Remove`.

### `IsPerfectlyHashed` is `false` — is that a problem?

Not for correctness — lookups are always correct, just not single-probe when it's `false` (it falls back to short linear probing on a raw-code collision). Supplying a stronger/wider hasher (e.g. `StringFnV1AFullHasher`) often restores the perfect layout. See [performance §6](performance.md#6-build-once-read-many--freeze-it).

### How is it different from `System.Collections.Frozen.FrozenDictionary`?

Same build-once, read-many spirit, but Celerity's is tunable via `IHashProvider<T>` and explicitly searches for a perfect hash you can verify through `IsPerfectlyHashed`. Pick whichever fits; if you're already standardized on `FrozenDictionary` and happy with it, there's no need to switch.

## CelerityMultiMap

### How is it different from `ILookup<,>` / `ToLookup`?

`ToLookup` produces an *immutable* `ILookup<,>`. `CelerityMultiMap` is a *mutable* one-to-many map (`Add` appends, `Remove(key, value)` / `RemoveAll(key)` remove) that **also** implements `ILookup<TKey, TValue?>`, so it drops into the same LINQ code while letting you mutate.

### Why does indexing an absent key not throw?

By design — the indexer returns an empty `ValueGroup` for any absent key, so `foreach (var v in map[key])` over a missing key simply iterates nothing. `Count` is the number of distinct keys; `ValueCount` is the total number of values across all keys.

## Platform & packaging

### Does Celerity support Native AOT and trimming?

Yes — the assembly is marked `<IsAotCompatible>true</IsAotCompatible>`, carries no reflection / runtime codegen / dynamic loading, and produces **no** trim or AOT warnings. CI runs the trim/AOT analyzers on every build and publishes a Native AOT smoke-test binary that exercises every collection and hasher. See [`docs/aot.md`](aot.md).

### What target frameworks are supported?

Install from NuGet with `dotnet add package Celerity.Collections`. Multi-targeting `net8.0;net9.0` is on the [roadmap](../ROADMAP.md) for the 2.0 restructure; check the package page for the current target.

### Is it one package or several?

Today everything ships in `Celerity.Collections`. The [roadmap](../ROADMAP.md) plans a 2.0 split into `Celerity.Collections`, `Celerity.Hashing`, and `Celerity.Primitives`, mirroring the .NET package layout.

## Contributing & roadmap

### Something's missing / I found a bug. Where do I go?

Open an issue or PR on the [GitHub tracker](https://github.com/marius-bughiu/Celerity/issues). See [`CONTRIBUTING.md`](../CONTRIBUTING.md) for build/test/PR conventions, and the [`ROADMAP.md`](../ROADMAP.md) for planned work (e.g. `SmallDictionary`, Robin Hood probing, SIMD-accelerated lookups).

### Will you add `<feature>`?

Celerity is narrowly scoped: a type ships only if it beats the BCL on at least one *documented* benchmark. Thread-safety, serialization formatters, and general-purpose data structures are explicit [non-goals](../ROADMAP.md#non-goals). If your idea fits the "measurably faster on a specific shape" bar, open an issue with the workload in mind.

## See also

- [Performance tuning guide](performance.md)
- [Migration guide](migration.md)
- [Troubleshooting](troubleshooting.md)
- [Collections](api/collections.md) and [Hashing](api/hashing.md) API references
