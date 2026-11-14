# Getting started

Celerity is a .NET library of specialized, high-performance collections — drop-in alternatives to the BCL that trade flexibility for speed or memory on specific workloads. Hashers are structs supplied as generic constraints (so the JIT inlines them), load factors are configurable, and you can plug in your own hash functions.

## Install

```bash
dotnet add package Celerity.Collections
```

## Packages

Celerity's core ships as layered NuGet packages. **`Celerity.Collections` pulls in the hashing and primitives packages transitively, so a single `dotnet add package Celerity.Collections` still gives you everything on that line** — add the lower packages directly only if you want the hashers or primitives *without* the collections. `Celerity.Sorting` and `Celerity.Statistics` are siblings rather than dependencies of the collections: add them when you want the sorts or the streaming summaries.

| Package | What it adds | Depends on |
|---|---|---|
| [`Celerity.Collections`](https://www.nuget.org/packages/Celerity.Collections/) | dictionaries, sets, frozen/perfect-hash collections, streaming sketches | `Celerity.Hashing`, `Celerity.Primitives` |
| [`Celerity.Hashing`](https://www.nuget.org/packages/Celerity.Hashing/) | `IHashProvider<T>` / `IHashProvider64<T>`, the struct hashers, `HashQualityEvaluator` | `Celerity.Primitives` |
| [`Celerity.Primitives`](https://www.nuget.org/packages/Celerity.Primitives/) | `FastUtils`, struct PRNGs, `VarInt`, `FastGuid`, `SortedSpan`, `MortonCurve` / `HilbertCurve` | — |
| [`Celerity.Sorting`](https://www.nuget.org/packages/Celerity.Sorting/) | `RadixSort`, `CountingSort`, `PartialSort` — non-comparison sorts and selection over primitive keys; `StringSort` — a stable ordinal string sort; `KWayMerge` — a stable k-way merge of sorted runs | `Celerity.Primitives` |
| [`Celerity.Statistics`](https://www.nuget.org/packages/Celerity.Statistics/) | `DDSketch`, `KllSketch`, `ReservoirSampler`, `RunningStatistics`, `RollingQuantile`, `AliasTable` — streaming quantiles (relative-error and rank-error), sampling and moments in bounded memory, exact sliding-window quantiles, and `O(1)` weighted random sampling | `Celerity.Primitives` |

> **Upgrading from 1.x?** Namespaces are unchanged except `FastUtils`, which moved from `Celerity` to `Celerity.Primitives`. See the [migration guide](migration.md#200--the-package-split).

All eight packages — these five and the three [showcase packages](README.md#built-with-celerity) — **target `net10.0`** (LTS). On .NET 8 or 9, stay on the 3.6.x line: 3.6.0 is the last release built for those runtimes, and security fixes for it ship from the [`support/net8`](https://github.com/marius-bughiu/Celerity/tree/support/net8) branch.

## First steps

Two examples cover the common surface. The [collections guide](collections.md) walks through each family, and the [API reference](api/collections.md) documents every type.

`IntDictionary<TValue>` defaults to `Int32WangNaiveHasher`, so most callers don't pick a hasher:

```csharp
using Celerity.Collections;

var counts = new IntDictionary<int>();
counts[42] = 1;
counts[42]++;            // indexer get/set
counts.TryAdd(7, 100);   // false if present, no overwrite
counts.Add(8, 200);      // throws if present

if (counts.TryGetValue(42, out var hits))
    Console.WriteLine(hits); // 2

counts.Remove(7);
foreach (var kvp in counts) // allocation-free struct enumerator
    Console.WriteLine($"{kvp.Key} -> {kvp.Value}");
```

The zero key is a legitimate value, not the sentinel — `counts[0] = 99` round-trips. `LongDictionary<TValue>` is the same surface for `long` keys.

For non-`int`/`long` keys, pick a hasher from `Celerity.Hashing` (or supply your own); `DefaultHasher<T>` falls back to `EqualityComparer<T>.Default`:

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var byId = new CelerityDictionary<Guid, string, GuidHasher>();
byId[Guid.NewGuid()] = "alice";

var byName = new CelerityDictionary<string, int, StringFnV1AHasher>();
byName["bob"] = 1;
```

The hasher is a `struct` generic constraint, so the JIT devirtualizes and inlines `Hash()` on the probe path.

## Constructing from an existing collection

The dictionaries accept any `IEnumerable<KeyValuePair<TKey, TValue>>`; an `ICollection<T>` source is used to pre-size the backing storage so the bulk fill avoids resizes. Duplicate keys (including duplicate `default(TKey)`) throw `ArgumentException`, matching BCL `Dictionary<,>`.

```csharp
var bcl = new Dictionary<int, string> { [1] = "a", [2] = "b", [3] = "c" };
var fast = new IntDictionary<string>(bcl);

var fromKvps = new CelerityDictionary<string, int, StringFnV1AHasher>(new[]
{
    new KeyValuePair<string, int>("alice", 1),
    new KeyValuePair<string, int>("bob",   2),
});
```

## API at a glance

The dictionaries mirror the parts of `Dictionary<TKey, TValue>` most callers reach for: indexer get/set, `ContainsKey`, `TryGetValue`, `Add`, `TryAdd`, `Remove` (both overloads), `Clear`, `EnsureCapacity` / `TrimExcess`, `Count`, `Keys`, `Values`, `GetEnumerator()`. The string-keyed types listed under [span-keyed lookups](api/collections.md#span-keyed-lookups) additionally take a `ReadOnlySpan<char>` on `TryGetValue` / `ContainsKey` / `Contains`, so a caller holding a slice of a buffer never allocates a `string` to probe. They implement `IDictionary<TKey, TValue?>` and `IReadOnlyDictionary<TKey, TValue?>` — the `Keys` / `Values` views widen to read-only `ICollection<T>`s through the mutable interface, whose mutators throw `NotSupportedException` exactly as `Dictionary<,>.KeyCollection` does — and accept an `IEnumerable<KeyValuePair<TKey, TValue>>` at construction. The sets expose `Add`, `TryAdd`, `Contains`, `Remove`, `Clear`, `EnsureCapacity` / `TrimExcess`, `Count`, and a struct enumerator. `EnsureCapacity(n)` pre-grows the table once for a known-size bulk insert (no incremental rehashes); `TrimExcess()` rehashes back down to fit `Count`. The open-addressed tables store the zero / `default(TKey)` key (or element) out-of-band so it never collides with the empty-slot sentinel; the chained `LinkedDictionary` / `LinkedSet` / `BiDictionary` keep it in the table like any other key.

Full constructors, signatures, exceptions, and per-type examples: **[API reference](README.md#api-reference)**.

## Where next

- [Collections guide](collections.md) — every collection family, the tradeoff each type buys, and worked examples.
- [Choosing a collection](choosing-a-collection.md) — find your workload in the decision table and get the type for it.
- [Choosing a hasher](choosing-a-hasher.md) — the struct hasher for your key shape, and how to write your own.
- [Performance tuning](performance.md) — capacity, load factor, and the struct fast paths.
- [Migration from BCL collections](migration.md) — `Dictionary<,>`, `HashSet<>`, `ILookup<,>` and `FrozenDictionary<,>` to their Celerity counterparts.
- [Documentation index](README.md) — every guide and the full API reference.
