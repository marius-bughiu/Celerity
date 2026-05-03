# Celerity
[![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/v/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/vpre/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/)

Celerity is a .NET library that provides specialized high-performance collections optimized for specific use cases. It includes data structures designed for better speed or memory efficiency compared to standard .NET collections. The package supports configurable load factors, multiple built-in hash functions, and allows users to define custom hash functions for fine-tuned performance.

## Collections

- `CelerityDictionary<TKey, TValue, THasher>` — generic dictionary with a struct hasher constraint.
- `IntDictionary<TValue>` / `IntDictionary<TValue, THasher>` — `int`-keyed specialization. Defaults to `Int32WangNaiveHasher`.
- `LongDictionary<TValue>` / `LongDictionary<TValue, THasher>` — `long`-keyed specialization. Defaults to `Int64WangHasher`.
- `CeleritySet<T, THasher>` — generic set counterpart to `CelerityDictionary`.
- `IntSet` / `IntSet<THasher>` — `int`-keyed set specialization.

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

The zero key is a legitimate value, not the empty-slot sentinel — `counts[0] = 99` round-trips correctly. `LongDictionary<TValue>` follows the exact same surface for `long` keys (defaulting to `Int64WangHasher`).

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
| Dictionary keyed by `long` | `LongDictionary<TValue>` | 64-bit equivalent of `IntDictionary`; defaults to `Int64WangHasher`. |
| Dictionary keyed by `Guid`, `string`, or any other type | `CelerityDictionary<TKey, TValue, THasher>` | Pick a struct hasher from `Celerity.Hashing` (e.g. `GuidHasher`, `StringFnV1AHasher`) so the JIT can inline `Hash()` on the probe path. |
| Set of `int` values | `IntSet` | Same fast path as `IntDictionary`, membership only. |
| Set of any other type | `CeleritySet<T, THasher>` | Same hasher choice as `CelerityDictionary`. |
| Need a stable iteration order, multi-threaded access, or a frozen / read-only post-build view | BCL `Dictionary<,>`, `ConcurrentDictionary<,>`, `FrozenDictionary<,>` | Celerity is single-threaded, iteration order is unspecified, and `FrozenCelerityDictionary` is still on the [1.2.0 roadmap](ROADMAP.md). |

Notes on picking a hasher once the collection is settled:

- For `int` / `long` keys, the convenience subclasses (`IntDictionary<TValue>`, `IntSet`, `LongDictionary<TValue>`) already pick a sensible default — only override when you have evidence of clustered or adversarial keys, in which case switch to `Int32Murmur3Hasher` / `Int64Murmur3Hasher`.
- For arbitrary types, `DefaultHasher<T>` (which delegates to `EqualityComparer<T>.Default.GetHashCode()`) is a safe fallback. It still benefits from the struct-hasher devirtualisation; the inner `EqualityComparer<T>` dispatch is the only unavoidable cost. Replace it with a hand-written struct hasher if profiling shows `Hash` on the hot path.
- The full hasher matrix lives in [`docs/api/hashing.md`](docs/api/hashing.md).

A few cases where Celerity is **not** the right answer today:

- **Concurrent reads/writes from multiple threads.** Celerity collections are single-threaded; use `ConcurrentDictionary<,>` or wrap a BCL `Dictionary<,>` in your own lock.
- **You need `IDictionary<,>` (mutable interface), `LINQ`-heavy code that relies on `Count`-via-extension on the boxed surface, or anything that depends on a specific iteration order.** Celerity exposes `IReadOnlyDictionary<,>` only and does not guarantee iteration order across versions.
- **Build-once / read-many lookup tables for string keys.** Today the BCL `FrozenDictionary<,>` will outperform `CelerityDictionary` on lookups; the Celerity equivalent (`FrozenCelerityDictionary`) is planned for 1.2.0 ([#62](https://github.com/marius-bughiu/Celerity/issues/62)).

## Benchmarks

#### CelerityDictionary

`CelerityDictionary` allows you to bring your own custom key hasher to best suit your needs. Below you have a benchmark between a standard .NET `Dictionary<int, int>` and a `CelerityDictionary<int, int, Int32WangNaiveHasher>` using a random key distribution.

| Method                    | ItemCount | Mean           | Error        | StdDev       | Allocated |
|-------------------------- |---------- |---------------:|-------------:|-------------:|----------:|
| Dictionary_Insert         | 1000      |    13,768.8 ns |    104.56 ns |     92.69 ns |   73168 B |
| CelerityDictionary_Insert | 1000      |     8,540.0 ns |     82.35 ns |     73.00 ns |   33072 B |
| Dictionary_Lookup         | 1000      |     2,842.1 ns |      7.55 ns |      7.06 ns |         - |
| CelerityDictionary_Lookup | 1000      |     1,660.1 ns |     14.09 ns |     12.49 ns |         - |
| Dictionary_Remove         | 1000      |     1,358.9 ns |     13.87 ns |     12.97 ns |         - |
| CelerityDictionary_Remove | 1000      |       870.6 ns |      2.69 ns |      2.38 ns |         - |
| Dictionary_Insert         | 100000    | 2,466,978.2 ns | 49,091.20 ns | 50,413.05 ns | 6037813 B |
| CelerityDictionary_Insert | 100000    | 2,860,774.8 ns | 50,391.63 ns | 47,136.36 ns | 4195120 B |
| Dictionary_Lookup         | 100000    | 1,021,650.8 ns | 11,702.28 ns | 10,373.77 ns |       1 B |
| CelerityDictionary_Lookup | 100000    |   422,466.0 ns |  4,472.81 ns |  3,965.03 ns |         - |
| Dictionary_Remove         | 100000    |   149,102.9 ns |    926.90 ns |    867.02 ns |         - |
| CelerityDictionary_Remove | 100000    |   129,491.3 ns |  2,092.50 ns |  1,854.94 ns |         - |

## Custom hashing

You can bring your own custom hash provider by implementing the `IHashProvider<T>` interface.

```csharp
public interface IHashProvider<T>
{
    int Hash(T key);
}
```

Hashers must be **structs** when used with Celerity collections (`where THasher : struct, IHashProvider<T>`) so the JIT can devirtualize and inline `Hash()`. The package ships built-in hashers for `int`, `long`, `uint`, `ulong`, `Guid`, and `string`, plus a `DefaultHasher<T>` fallback that delegates to `EqualityComparer<T>.Default.GetHashCode()`. See [`docs/api/hashing.md`](docs/api/hashing.md) for the full list.

## API overview

The dictionaries (`CelerityDictionary`, `IntDictionary`, `LongDictionary`) expose a compact, allocation-conscious API that mirrors the parts of `Dictionary<TKey, TValue>` most users actually reach for: indexer get/set, `ContainsKey`, `TryGetValue`, `Add`, `TryAdd`, `Remove` (both the `bool Remove(key)` and `bool Remove(key, out TValue?)` overloads), `Clear`, `Count`, `Keys`, `Values`, and `GetEnumerator()`. They implement `IReadOnlyDictionary<TKey, TValue?>` and accept an `IEnumerable<KeyValuePair<TKey, TValue>>` source at construction.

The sets (`CeleritySet`, `IntSet`) expose `Add`, `TryAdd`, `Contains`, `Remove`, `Clear`, `Count`, and a struct enumerator.

The zero / `default(TKey)` key (or element, for sets) is stored out-of-band so it never collides with the empty-slot sentinel used during probing. This includes `null` for reference-type keys.

For full API details — constructors, method signatures, parameters, exceptions, and usage examples — see the **[API reference docs](docs/README.md)**.

## Project docs

- [`docs/`](docs/README.md) — API reference (collections, hashing, utilities).
- [`ROADMAP.md`](ROADMAP.md) — planned milestones and long-term vision.
- [`CHANGELOG.md`](CHANGELOG.md) — release notes.
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — build, test, PR conventions.
- [GitHub Issues](https://github.com/marius-bughiu/Celerity/issues) — open backlog and bug reports.
