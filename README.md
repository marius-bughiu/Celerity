# Celerity
[![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/v/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/vpre/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![Live benchmarks](https://img.shields.io/badge/benchmarks-live-0d6e6e?style=flat-square)](https://marius-bughiu.github.io/Celerity/dev/bench/) [![Coverage](https://marius-bughiu.github.io/Celerity/coverage/badge.svg)](https://marius-bughiu.github.io/Celerity/coverage/)

Celerity is a .NET library of specialized, high-performance collections — drop-in alternatives to the BCL that trade flexibility for speed or memory on specific workloads. Hashers are structs supplied as generic constraints (so the JIT inlines them), load factors are configurable, and you can plug in your own hash functions.

```bash
dotnet add package Celerity.Collections
```

## Quick start

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// int keys: the hasher is built in.
var counts = new IntDictionary<int>();
counts[42] = 1;
counts[42]++;

// Any other key type: name a struct hasher, and the JIT inlines it on every probe.
var byName = new CelerityDictionary<string, int, StringFnV1AHasher>();
byName["bob"] = 1;

if (byName.TryGetValue("bob", out int id))
    Console.WriteLine(id); // 1
```

[Getting started](docs/getting-started.md) walks through the rest of the surface, and the [collections guide](docs/collections.md) has a worked example for every other type.

## What's inside

| Family | Types |
|---|---|
| [Dictionaries](docs/collections.md#dictionaries) | `CelerityDictionary`, `IntDictionary`, `LongDictionary`, `RobinHoodDictionary`, `SwissDictionary`, `HashCachingDictionary`, `PooledCelerityDictionary`, `LinkedDictionary`, `BiDictionary`, `FrozenCelerityDictionary`, `CelerityMultiMap`, `CelerityMultiSet`, `SmallDictionary`, `EnumMap`, `SparseMap`, `SlotMap`, `PersistentHashMap`, `PersistentSortedMap` |
| [Sets](docs/collections.md#sets) | `CeleritySet`, `IntSet`, `LongSet`, `SwissSet`, `RobinHoodSet`, `HashCachingSet`, `PooledCeleritySet`, `LinkedSet`, `FrozenCeleritySet`, `SmallSet`, `EnumSet`, `SparseSet`, `HierarchicalBitSet`, `CompressedIntSet`, `PersistentHashSet` |
| [Caches](docs/collections.md#caches) | `LruCache`, `LfuCache`, `SieveCache` |
| [Sequences](docs/collections.md#sequences) | `Deque`, `BTreeList`, `GapBuffer`, `SegmentedList`, `PersistentVector` |
| [Union-find](docs/collections.md#union-find) | `DisjointSet` |
| [Priority queues](docs/collections.md#priority-queues) | `IndexedPriorityQueue`, `MinMaxHeap`, `RadixHeap` |
| [Prefix trees](docs/collections.md#prefix-trees) | `Trie`, `SuccinctTrie` |
| [Span-keyed string lookups](docs/collections.md#span-keyed-string-lookups) | `StringInternTable` |
| [Sorted collections](docs/collections.md#sorted-ordered-collections) | `BTreeDictionary`, `BTreeSet`, `RankedSet`, `RankedMultiSet`, `FrozenSortedSet` |
| [Range aggregates](docs/collections.md#range-aggregates) | `FenwickTree`, `SegmentTree`, `SparseTable`, `WaveletTree` |
| [Spatial & interval indexes](docs/collections.md#spatial--interval-indexes) | `KdTree`, `SpatialGrid`, `RTree`, `IntervalTree`, `RangeMap` |
| [Graphs](docs/collections.md#graphs) | `CompressedGraph` |
| [Text](docs/collections.md#text) | `SuffixArray`, `AhoCorasick`, `EditDistanceIndex`, `Rope` |
| [Timers](docs/collections.md#timers) | `TimerWheel` |
| [Probabilistic & bit-level](docs/collections.md#probabilistic--bit-level) | `BloomFilter`, `CuckooFilter`, `XorFilter`, `HyperLogLog`, `SlidingHyperLogLog`, `MinHashSketch`, `CountMinSketch`, `TopKSketch`, `BitSet`, `BitPackedArray`, `RankSelectBitVector`, `EliasFanoSequence` |

Not sure which one you need? The [decision table](docs/choosing-a-collection.md) maps a workload to a type in one line, and [Choosing a hasher](docs/choosing-a-hasher.md) does the same for the hasher.

### Packages

`Celerity.Collections` pulls in `Celerity.Hashing` and `Celerity.Primitives` transitively, so the one `dotnet add package` above is all most projects need. `Celerity.Sorting` and `Celerity.Statistics` are siblings rather than dependencies: add them for the sorts or the streaming summaries.

| Package | What it adds |
|---|---|
| [`Celerity.Collections`](https://www.nuget.org/packages/Celerity.Collections/) | the collections above |
| `Celerity.Hashing` | `IHashProvider<T>`, the struct hashers, `HashQualityEvaluator` — [API](docs/api/hashing.md) |
| `Celerity.Primitives` | `FastUtils`, struct PRNGs, `VarInt`, `FastGuid`, `SpanBits`, `SortedSpan`, space-filling curves — [API](docs/api/utilities.md) |
| [`Celerity.Sorting`](https://www.nuget.org/packages/Celerity.Sorting/) | `RadixSort`, `CountingSort`, `PartialSort`, `StringSort`, `KWayMerge` — [API](docs/api/sorting.md) |
| [`Celerity.Statistics`](https://www.nuget.org/packages/Celerity.Statistics/) | `DDSketch`, `KllSketch`, `ReservoirSampler`, `RunningStatistics`, `RollingQuantile`, `AliasTable` — [API](docs/api/statistics.md) |

Every package multi-targets `net8.0`, `net9.0` and `net10.0`, and is [Native AOT and trimming compatible](docs/aot.md). Upgrading from 1.x? See the [migration guide](docs/migration.md#200--the-package-split).

## Performance

**Up to 2.4&times; faster than `Dictionary<int, int>`** on lookups, with zero allocations — a *collection-layout* win (open addressing with direct `==` key comparison and no per-call `EqualityComparer<T>` dispatch), independent of the hasher. The [live dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) tracks every shipped collection against its BCL counterpart on every `main` push, and [Benchmarks](docs/benchmarks.md) covers what the suites measure and how to run them locally.

Celerity wins on specific shapes, not everywhere. It is single-threaded, and the docs state where each type loses as plainly as where it wins — see [when not to use Celerity](docs/choosing-a-collection.md#when-not-to-use-celerity).

## Built with Celerity

Standalone libraries built **on top of** Celerity, each shipped as its own NuGet package that depends on `Celerity.Collections` — add only the one you need.

| Package | What it gives you |
|---|---|
| [`Celerity.Ring`](https://www.nuget.org/packages/Celerity.Ring/)<br/>[![NuGet](https://img.shields.io/nuget/v/Celerity.Ring.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Ring/) | Consistent-hash & rendezvous (HRW) **rings** and **Maglev** lookup-table routing for sharding and request routing, generic over your key type and hasher — with **byte-identical** node assignment across OS / architecture / runtime (x64, arm64, Blazor WASM), so every node in a cluster agrees on the mapping. Fills a gap the BCL has no type for. [README](src/Celerity.Ring/README.md) |
| [`Celerity.Sentinel`](https://www.nuget.org/packages/Celerity.Sentinel/)<br/>[![NuGet](https://img.shields.io/nuget/v/Celerity.Sentinel.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Sentinel/) | Streaming **abuse / heavy-hitter detection** — surfaces the top offenders, per-key rate, and fan-out cardinality of a request stream in a **fixed footprint regardless of key cardinality**, so it survives the attacker key-rotation that drives a `Dictionary<,>` counter to OOM. Includes a striped tracker for concurrent hot paths. [README](src/Celerity.Sentinel/README.md) |
| [`Celerity.Cardinality`](https://www.nuget.org/packages/Celerity.Cardinality/)<br/>[![NuGet](https://img.shields.io/nuget/v/Celerity.Cardinality.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Cardinality/) | Mergeable approximate **`COUNT(DISTINCT)`** and windowed **dedup** over unbounded managed streams — exact for small inputs, promoting to a fixed ~16&#160;KB estimator past a threshold, with **deterministic cross-shard merge** identical on every runtime. [README](src/Celerity.Cardinality/README.md) |

> These are a separate tier from the core `Celerity.Collections` family above: they depend on it, but you don't get them by installing it — reach for one only when its problem is yours. (The NuGet badges populate on first publish.)

## Documentation

- **Start here:** [Getting started](docs/getting-started.md) · [Collections guide](docs/collections.md) · [Choosing a collection](docs/choosing-a-collection.md) · [Choosing a hasher](docs/choosing-a-hasher.md)
- **Guides:** [Performance tuning](docs/performance.md) · [Benchmarks](docs/benchmarks.md) · [Migrating from the BCL](docs/migration.md) · [Troubleshooting](docs/troubleshooting.md) · [FAQ](docs/faq.md) · [Native AOT & trimming](docs/aot.md)
- **API reference:** [Collections](docs/api/collections.md) · [Hashing](docs/api/hashing.md) · [Sorting](docs/api/sorting.md) · [Statistics](docs/api/statistics.md) · [Primitives](docs/api/utilities.md)

The [documentation index](docs/README.md) lists every page.

## Contributing

Issues and pull requests are welcome. [`CONTRIBUTING.md`](CONTRIBUTING.md) covers building, testing and the project's scope, [`ROADMAP.md`](ROADMAP.md) what is planned, and [`CHANGELOG.md`](CHANGELOG.md) what has shipped.

## License

[MIT](LICENSE)
