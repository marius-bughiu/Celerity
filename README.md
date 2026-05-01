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
