# Celerity.Collections

Specialized, high-performance collections for .NET — drop-in alternatives to the BCL that trade flexibility for speed or memory on specific workloads. Part of the [Celerity](https://github.com/marius-bughiu/Celerity) family.

Hashers are structs supplied as generic constraints, so the JIT inlines them on every probe. Load factors are configurable, and you can plug in your own hash functions. Installing this package also brings in `Celerity.Hashing` and `Celerity.Primitives`.

## Quick start

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// int and long keys (IntDictionary, LongDictionary): the hasher is built in.
var counts = new IntDictionary<int>();
counts[42] = 1;
counts[42]++;

// Any other key type: name a struct hasher, and the JIT inlines it on every probe.
var byName = new CelerityDictionary<string, int, StringFnV1AHasher>();
byName["bob"] = 1;

if (byName.TryGetValue("bob", out int id))
    Console.WriteLine(id); // 1
```

[Getting started](https://github.com/marius-bughiu/Celerity/blob/main/docs/getting-started.md) covers the rest of the surface, and the [collections guide](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md) walks through each family.

## Collections

| Family | Types |
|---|---|
| [Dictionaries](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#dictionaries) | `CelerityDictionary`, `IntDictionary`, `LongDictionary`, `RobinHoodDictionary`, `SwissDictionary`, `HashCachingDictionary`, `PooledCelerityDictionary`, `LinkedDictionary`, `BiDictionary`, `FrozenCelerityDictionary`, `CelerityMultiMap`, `CelerityMultiSet`, `SmallDictionary`, `EnumMap`, `SparseMap`, `SlotMap`, `PersistentHashMap`, `PersistentSortedMap` |
| [Sets](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#sets) | `CeleritySet`, `IntSet`, `LongSet`, `SwissSet`, `RobinHoodSet`, `HashCachingSet`, `PooledCeleritySet`, `LinkedSet`, `FrozenCeleritySet`, `SmallSet`, `EnumSet`, `SparseSet`, `HierarchicalBitSet`, `CompressedIntSet`, `PersistentHashSet` |
| [Caches](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#caches) | `LruCache`, `LfuCache`, `SieveCache` |
| [Sequences](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#sequences) | `Deque`, `BTreeList`, `GapBuffer`, `SegmentedList`, `PersistentVector` |
| [Union-find](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#union-find) | `DisjointSet` |
| [Priority queues](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#priority-queues) | `IndexedPriorityQueue`, `MinMaxHeap`, `RadixHeap` |
| [Prefix trees](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#prefix-trees) | `Trie`, `SuccinctTrie` |
| [Span-keyed string lookups](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#span-keyed-string-lookups) | `StringInternTable` |
| [Sorted collections](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#sorted-ordered-collections) | `BTreeDictionary`, `BTreeSet`, `RankedSet`, `RankedMultiSet`, `FrozenSortedSet`, `FrozenSortedMap` |
| [Range aggregates](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#range-aggregates) | `FenwickTree`, `SegmentTree`, `SparseTable`, `WaveletTree` |
| [Spatial & interval indexes](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#spatial--interval-indexes) | `KdTree`, `SpatialGrid`, `RTree`, `IntervalTree`, `RangeMap` |
| [Graphs](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#graphs) | `CompressedGraph` |
| [Text](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#text) | `SuffixArray`, `AhoCorasick`, `EditDistanceIndex`, `Rope` |
| [Timers](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#timers) | `TimerWheel` |
| [Probabilistic & bit-level](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md#probabilistic--bit-level) | `BloomFilter`, `CuckooFilter`, `XorFilter`, `HyperLogLog`, `SlidingHyperLogLog`, `MinHashSketch`, `CountMinSketch`, `TopKSketch`, `BitSet`, `BitPackedArray`, `RankSelectBitVector`, `EliasFanoSequence` |

## Choosing a collection

The [decision table](https://github.com/marius-bughiu/Celerity/blob/main/docs/choosing-a-collection.md) maps a workload to a type in one line, and [Choosing a hasher](https://github.com/marius-bughiu/Celerity/blob/main/docs/choosing-a-hasher.md) does the same for the hasher. Celerity is single-threaded and wins on specific shapes rather than everywhere, so the decision table also covers [when not to use it](https://github.com/marius-bughiu/Celerity/blob/main/docs/choosing-a-collection.md#when-not-to-use-celerity).

## Benchmarks

**Up to 2.4× faster than `Dictionary<int, int>`** on lookups, with zero allocations — a *collection-layout* win (open addressing with direct `==` key comparison and no per-call `EqualityComparer<T>` dispatch), independent of the hasher. The [live dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) tracks every shipped collection against its BCL counterpart, and [Benchmarks](https://github.com/marius-bughiu/Celerity/blob/main/docs/benchmarks.md) covers what the suites measure.

## Related packages

[`Celerity.Sorting`](https://www.nuget.org/packages/Celerity.Sorting/) and [`Celerity.Statistics`](https://www.nuget.org/packages/Celerity.Statistics/) are siblings rather than dependencies: add them for the sorts or the streaming summaries. Three standalone libraries are built on this package: [`Celerity.Ring`](https://www.nuget.org/packages/Celerity.Ring/) for consistent-hash, rendezvous and Maglev routing, [`Celerity.Sentinel`](https://www.nuget.org/packages/Celerity.Sentinel/) for streaming abuse and heavy-hitter detection, and [`Celerity.Cardinality`](https://www.nuget.org/packages/Celerity.Cardinality/) for mergeable approximate `COUNT(DISTINCT)` and windowed dedup.

Every package targets `net10.0` and is [Native AOT and trimming compatible](https://github.com/marius-bughiu/Celerity/blob/main/docs/aot.md). On .NET 8 or 9, stay on the [3.6.x line](https://github.com/marius-bughiu/Celerity/blob/main/docs/migration.md#net80--net90--net100--after-36x). Upgrading from 1.x? See the [migration guide](https://github.com/marius-bughiu/Celerity/blob/main/docs/migration.md#200--the-package-split).

## Documentation

- **Start here:** [Getting started](https://github.com/marius-bughiu/Celerity/blob/main/docs/getting-started.md) · [Collections guide](https://github.com/marius-bughiu/Celerity/blob/main/docs/collections.md) · [Choosing a collection](https://github.com/marius-bughiu/Celerity/blob/main/docs/choosing-a-collection.md) · [Choosing a hasher](https://github.com/marius-bughiu/Celerity/blob/main/docs/choosing-a-hasher.md)
- **Guides:** [Performance tuning](https://github.com/marius-bughiu/Celerity/blob/main/docs/performance.md) · [Benchmarks](https://github.com/marius-bughiu/Celerity/blob/main/docs/benchmarks.md) · [Migrating from the BCL](https://github.com/marius-bughiu/Celerity/blob/main/docs/migration.md) · [Troubleshooting](https://github.com/marius-bughiu/Celerity/blob/main/docs/troubleshooting.md) · [FAQ](https://github.com/marius-bughiu/Celerity/blob/main/docs/faq.md) · [Native AOT & trimming](https://github.com/marius-bughiu/Celerity/blob/main/docs/aot.md)
- **API reference:** [Collections](https://github.com/marius-bughiu/Celerity/blob/main/docs/api/collections.md) · [Hashing](https://github.com/marius-bughiu/Celerity/blob/main/docs/api/hashing.md)

Source, issues and the changelog live in the [GitHub repository](https://github.com/marius-bughiu/Celerity).

## License

[MIT](https://github.com/marius-bughiu/Celerity/blob/main/LICENSE)
