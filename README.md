# Celerity
[![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/v/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/vpre/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![Live benchmarks](https://img.shields.io/badge/benchmarks-live-0d6e6e?style=flat-square)](https://marius-bughiu.github.io/Celerity/dev/bench/)

Celerity is a .NET library that provides specialized high-performance collections optimized for specific use cases. It includes data structures designed for better speed or memory efficiency compared to standard .NET collections. The package supports configurable load factors, multiple built-in hash functions, and allows users to define custom hash functions for fine-tuned performance.

## Collections

- `CelerityDictionary<TKey, TValue, THasher>` — generic dictionary with a struct hasher constraint.
- `IntDictionary<TValue>` / `IntDictionary<TValue, THasher>` — `int`-keyed specialization. Defaults to `Int32WangNaiveHasher`.
- `LongDictionary<TValue>` / `LongDictionary<TValue, THasher>` — `long`-keyed specialization. Defaults to `Int64WangNaiveHasher`.
- `CeleritySet<T, THasher>` — generic set counterpart to `CelerityDictionary`.
- `IntSet` / `IntSet<THasher>` — `int`-keyed set specialization.
- `LongSet` / `LongSet<THasher>` — `long`-keyed set specialization. Defaults to `Int64WangNaiveHasher`.

All dictionaries implement `IReadOnlyDictionary<TKey, TValue?>` and ship allocation-free struct enumerators, `Keys` / `Values` views, and an `IEnumerable<KeyValuePair<TKey, TValue>>` constructor. All collections handle `default(TKey)` (or zero for `int` / `long` keys, `null` for reference-type keys) out-of-band so it never collides with the empty-slot sentinel.

## Quick start

Install from NuGet:

```bash
dotnet add package Celerity.Collections
```

### `IntDictionary` — the int-keyed fast path

`IntDictionary<TValue>` defaults to `Int32WangNaiveHasher`, so most callers don't need to pick a hasher.

```csharp
using Celerity.Collections;

var counts = new IntDictionary<int>();
counts[42] = 1;
counts[42]++;            // indexer get/set
counts.TryAdd(7, 100);   // returns false if key already present, no overwrite
counts.Add(8, 200);      // throws ArgumentException if key already present

if (counts.TryGetValue(42, out var hits))
    Console.WriteLine(hits); // 2

counts.Remove(7);
Console.WriteLine(counts.Count); // 2

// foreach is allocation-free — Enumerator is a struct.
foreach (var kvp in counts)
    Console.WriteLine($"{kvp.Key} -> {kvp.Value}");
```

The zero key is a legitimate value, not the empty-slot sentinel — `counts[0] = 99` round-trips correctly. `LongDictionary<TValue>` follows the exact same surface for `long` keys (defaulting to `Int64WangNaiveHasher`).

### `CelerityDictionary` — generic keys with a struct hasher

For non-`int`/`long` keys, pick a hasher from `Celerity.Hashing` (or supply your own). `DefaultHasher<T>` falls back to `EqualityComparer<T>.Default.GetHashCode()` for arbitrary types.

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var byId = new CelerityDictionary<Guid, string, GuidHasher>();
byId[Guid.NewGuid()] = "alice";

var byName = new CelerityDictionary<string, int, StringFnV1AHasher>();
byName["bob"] = 1;

// DefaultHasher<T> works for any type but pays the EqualityComparer<T> dispatch.
var byKey = new CelerityDictionary<DateOnly, string, DefaultHasher<DateOnly>>();
byKey[DateOnly.FromDateTime(DateTime.UtcNow)] = "today";
```

The hasher is a `struct` and is supplied as a generic constraint, so the JIT devirtualizes and inlines the `Hash()` call on the probe path.

### Sets

`IntSet` and `CeleritySet<T, THasher>` mirror the dictionary types for membership-only workloads.

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var seen = new IntSet();
seen.Add(1);
seen.Add(2);
Console.WriteLine(seen.Contains(1)); // true
seen.Remove(2);

var visitedIds = new CeleritySet<Guid, GuidHasher>();
visitedIds.TryAdd(Guid.NewGuid()); // returns true on first add, false on duplicate
```

### Construct from an existing collection

The dictionaries accept any `IEnumerable<KeyValuePair<TKey, TValue>>`. When the source implements `ICollection<T>`, its `Count` is used to pre-size the backing storage so the bulk fill avoids resize work.

```csharp
var bcl = new Dictionary<int, string> { [1] = "a", [2] = "b", [3] = "c" };
var fast = new IntDictionary<string>(bcl);

var fromKvps = new CelerityDictionary<string, int, StringFnV1AHasher>(
    new[]
    {
        new KeyValuePair<string, int>("alice", 1),
        new KeyValuePair<string, int>("bob",   2),
    });
```

Duplicate keys (including duplicate `default(TKey)` / zero-key entries) throw `ArgumentException`, matching BCL `Dictionary<,>` semantics.

### Custom hasher

Implement `IHashProvider<T>` as a `struct` to plug in your own hash function. See [Custom hashing](#custom-hashing) below for the contract and a worked example.

## Choosing a collection

Celerity ships specialised types because each one buys a different tradeoff. Use the table below to pick the right one; if your workload doesn't appear here, the BCL collection is usually the right starting point.

| Your workload | Use | Why |
|---|---|---|
| Dictionary keyed by `int` | `IntDictionary<TValue>` | Avoids generic boxing / `EqualityComparer<int>` dispatch; defaults to `Int32WangNaiveHasher`. |
| Dictionary keyed by `long` | `LongDictionary<TValue>` | 64-bit equivalent of `IntDictionary`; defaults to `Int64WangNaiveHasher`. |
| Dictionary keyed by `Guid`, `string`, or any other type | `CelerityDictionary<TKey, TValue, THasher>` | Pick a struct hasher from `Celerity.Hashing` (e.g. `GuidHasher`, `StringFnV1AHasher`) so the JIT can inline `Hash()` on the probe path. |
| Set of `int` values | `IntSet` | Same fast path as `IntDictionary`, membership only. |
| Set of `long` values | `LongSet` | 64-bit equivalent of `IntSet`; defaults to `Int64WangNaiveHasher`. |
| Set of any other type | `CeleritySet<T, THasher>` | Same hasher choice as `CelerityDictionary`. |
| Need a stable iteration order, multi-threaded access, or a frozen / read-only post-build view | BCL `Dictionary<,>`, `ConcurrentDictionary<,>`, `FrozenDictionary<,>` | Celerity is single-threaded, iteration order is unspecified, and `FrozenCelerityDictionary` is still on the [1.2.0 roadmap](ROADMAP.md). |

Notes on picking a hasher once the collection is settled:

- For `int` / `long` keys, the convenience subclasses (`IntDictionary<TValue>`, `IntSet`, `LongDictionary<TValue>`, `LongSet`) already pick a sensible default — only override when you have evidence of clustered or adversarial keys, in which case escalate to `Int32WangHasher` then `Int32Murmur3Hasher` (for `int` keys) or `Int64WangHasher` then `Int64Murmur3Hasher` (for `long` keys). The Wang full-finalizer tier is a cheaper middle option than Murmur3 while still mixing every input bit.
- For `uint` keys, `UInt32Hasher` is the cheap XOR-fold default; escalate to `UInt32WangHasher` (the full Thomas-Wang finalizer) then `UInt32Murmur3Hasher` (the Murmur3 `fmix32` finalizer) when the fold produces clustering or you need strong avalanche on adversarial keys, mirroring the `int` family's two-step `Int32WangHasher` → `Int32Murmur3Hasher` escalation.
- For `string` keys, `StringFnV1AHasher` is the fast default for ASCII-dominated workloads. Switch to `StringMurmur3Hasher` for keys with significant non-ASCII content (it hashes the full UTF-16 character rather than just the low byte) or when key distribution is clustered or adversarial.
- For arbitrary types, `DefaultHasher<T>` (which delegates to `EqualityComparer<T>.Default.GetHashCode()`) is a safe fallback. It still benefits from the struct-hasher devirtualisation; the inner `EqualityComparer<T>` dispatch is the only unavoidable cost. Replace it with a hand-written struct hasher if profiling shows `Hash` on the hot path.
- The full hasher matrix lives in [`docs/api/hashing.md`](docs/api/hashing.md).

A few cases where Celerity is **not** the right answer today:

- **Concurrent reads/writes from multiple threads.** Celerity collections are single-threaded; use `ConcurrentDictionary<,>` or wrap a BCL `Dictionary<,>` in your own lock.
- **You need `IDictionary<,>` (mutable interface), `LINQ`-heavy code that relies on `Count`-via-extension on the boxed surface, or anything that depends on a specific iteration order.** Celerity exposes `IReadOnlyDictionary<,>` only and does not guarantee iteration order across versions.
- **Build-once / read-many lookup tables for string keys.** Today the BCL `FrozenDictionary<,>` will outperform `CelerityDictionary` on lookups; the Celerity equivalent (`FrozenCelerityDictionary`) is planned for 1.2.0 ([#62](https://github.com/marius-bughiu/Celerity/issues/62)).

## Benchmarks

**Up to 2.4&times; faster than `Dictionary<int, int>`** on lookups, with zero allocations. The [live dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) tracks all five collections against their .NET BCL counterparts on every `main` push, with historical trends and per-PR regression comparisons. For high-precision local numbers, run `dotnet run -c Release` in [`src/Celerity.Benchmarks`](src/Celerity.Benchmarks) — hosted CI runners are noisier than your laptop and the dashboard reflects that.

## Custom hashing

You can bring your own custom hash provider by implementing the `IHashProvider<T>` interface.

```csharp
public interface IHashProvider<T>
{
    int Hash(T key);
}
```

Hashers must be **structs** when used with Celerity collections (`where THasher : struct, IHashProvider<T>`) so the JIT can devirtualize and inline `Hash()`. The package ships built-in hashers for `int`, `long`, `uint`, `ulong`, `Guid`, and `string`, plus a `DefaultHasher<T>` fallback that delegates to `EqualityComparer<T>.Default.GetHashCode()`. See [`docs/api/hashing.md`](docs/api/hashing.md) for the full list.

Not sure which hasher to pick for your key shape? `HashQualityEvaluator.Evaluate<T, THasher>(keys)` runs a representative key sample through a hasher and returns a `HashQualityReport` of collision count, bucket occupancy, max bucket load, chi-squared, and a normalized distribution score (`1.0` = ideal uniform). It's a diagnostic tool — run it offline to compare candidate hashers before committing one. See the [hash quality evaluation](docs/api/hashing.md#hash-quality-evaluation) section.

## Native AOT & trimming

Celerity is **Native AOT and trimming compatible**. The library carries no reflection, runtime code generation, or dynamic type loading — every collection is a generic over a struct hasher, and the only BCL primitives on the hot paths are `MemoryMarshal`, `Unsafe`, and `EqualityComparer<T>.Default`, all of which are AOT-safe. The assembly is marked [`<IsAotCompatible>true</IsAotCompatible>`](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#aot-compatibility-analyzers), so consuming it from a `PublishAot` app produces **no trim or AOT warnings**.

```bash
dotnet publish -r linux-x64 -c Release   # in an app that references Celerity.Collections
```

Compatibility is enforced on every build: the trim and AOT Roslyn analyzers run as part of the library's compilation, and CI publishes a Native AOT smoke-test app that exercises every collection and hasher and runs the resulting native binary. See [`docs/aot.md`](docs/aot.md) for details.

## API overview

The dictionaries (`CelerityDictionary`, `IntDictionary`, `LongDictionary`) expose a compact, allocation-conscious API that mirrors the parts of `Dictionary<TKey, TValue>` most users actually reach for: indexer get/set, `ContainsKey`, `TryGetValue`, `Add`, `TryAdd`, `Remove` (both the `bool Remove(key)` and `bool Remove(key, out TValue?)` overloads), `Clear`, `Count`, `Keys`, `Values`, and `GetEnumerator()`. They implement `IReadOnlyDictionary<TKey, TValue?>` and accept an `IEnumerable<KeyValuePair<TKey, TValue>>` source at construction.

The sets (`CeleritySet`, `IntSet`, `LongSet`) expose `Add`, `TryAdd`, `Contains`, `Remove`, `Clear`, `Count`, and a struct enumerator.

The zero / `default(TKey)` key (or element, for sets) is stored out-of-band so it never collides with the empty-slot sentinel used during probing. This includes `null` for reference-type keys.

For full API details — constructors, method signatures, parameters, exceptions, and usage examples — see the **[API reference docs](docs/README.md)**.

## Project docs

- [`docs/`](docs/README.md) — API reference (collections, hashing, utilities).
- [`ROADMAP.md`](ROADMAP.md) — planned milestones and long-term vision.
- [`CHANGELOG.md`](CHANGELOG.md) — release notes.
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — build, test, PR conventions.
- [GitHub Issues](https://github.com/marius-bughiu/Celerity/issues) — open backlog and bug reports.
