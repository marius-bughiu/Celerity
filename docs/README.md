# Celerity Documentation

The guides explain how to choose and use Celerity's types; the API reference documents every one of them. New to the library? Start with [Getting started](getting-started.md).

## Start here

- [Getting started](getting-started.md) — install, the package layout, first examples, and the API at a glance.
- [Collections guide](collections.md) — every collection family: the tradeoff each type buys, with worked examples.
- [Choosing a collection](choosing-a-collection.md) — the decision table: find your workload, get the type.
- [Choosing a hasher](choosing-a-hasher.md) — the struct hasher for your key shape, and how to write your own.

## Guides

- [Performance tuning](performance.md) — capacity, load factor, hasher selection, struct fast paths, and benchmarking.
- [Benchmarks](benchmarks.md) — the headline numbers, what the benchmark suites measure, and the spatial indexes measured against two baselines each.
- [Migration from BCL collections](migration.md) — mapping `Dictionary<,>`, `HashSet<>`, `ILookup<,>`, and `FrozenDictionary<,>` to Celerity types.
- [Troubleshooting](troubleshooting.md) — common errors and behavioural surprises, with fixes.
- [FAQ](faq.md) — conceptual questions about the design.
- [Native AOT & trimming](aot.md) — AOT / trim compatibility and how it is enforced.
- [Testing & coverage](testing.md) — the test layers, property-based and fuzz harnesses, and how coverage is measured and gated.

## API reference

- [Collections](api/collections.md) — dictionaries (`CelerityDictionary`, `RobinHoodDictionary`, `SwissDictionary`, `HashCachingDictionary`, `PooledCelerityDictionary`, the insertion-ordered `LinkedDictionary`, the one-to-one `BiDictionary`, `IntDictionary`, `LongDictionary`, `SmallDictionary`, `EnumMap`, `SparseMap`, `SlotMap`, `FrozenCelerityDictionary`, `PersistentHashMap`, the sorted `PersistentSortedMap`), sets (`CeleritySet`, `SwissSet`, `RobinHoodSet`, `HashCachingSet`, `PooledCeleritySet`, the insertion-ordered `LinkedSet`, `IntSet`, `LongSet`, `SmallSet`, `EnumSet`, `FrozenCeleritySet`, `SparseSet`, `CompressedIntSet`, `PersistentHashSet`), multi-collections (`CelerityMultiMap`, `CelerityMultiSet`), sorted and order-statistics collections (`BTreeDictionary`, `BTreeSet`, `RankedSet`, the duplicate-keeping `RankedMultiSet`, the immutable SIMD-searched `FrozenSortedSet` and its map sibling `FrozenSortedMap`, the bounded-universe `HierarchicalBitSet`, and the range-keyed `RangeMap`), text collections (`Trie`, `SuccinctTrie`, `StringInternTable`, `SuffixArray`, `AhoCorasick`, the fuzzy-lookup `EditDistanceIndex`, `Rope`), spatial indexes (`KdTree`, `SpatialGrid`, `RTree`, `IntervalTree`), range aggregates and range order statistics (`FenwickTree`, `SegmentTree`, `SparseTable`, `WaveletTree`), graphs (`CompressedGraph`), probabilistic / bit collections (`BitSet`, the fixed-width packed integer array `BitPackedArray`, `RankSelectBitVector`, the compressed sorted sequence `EliasFanoSequence`, `BloomFilter`, `CuckooFilter`, `XorFilter`, `HyperLogLog`, the windowed `SlidingHyperLogLog`, `MinHashSketch`, `CountMinSketch`, `TopKSketch`), caches (`LruCache`, `LfuCache`, `SieveCache`), sequences (`Deque`, the positional `BTreeList`, the cursor-edited `GapBuffer`, the append-built `SegmentedList`, `PersistentVector`), timers (`TimerWheel`), priority queues (`IndexedPriorityQueue`, `MinMaxHeap`, the monotone integer `RadixHeap`), and union-find (`DisjointSet`).
- [Hashing](api/hashing.md) — `IHashProvider<T>` interface and built-in hashers (`Int32WangNaiveHasher`, `Int32Murmur3Hasher`, `Int64WangHasher`, `Int64Murmur3Hasher`, `UInt32WangNaiveHasher`, `UInt64Murmur3Hasher`, `GuidHasher`, the `String*` hasher family, `DefaultHasher<T>`), and the `HashQualityEvaluator`.
- [Sorting](api/sorting.md) — non-comparison sorts and selection over primitive keys (`RadixSort`, `CountingSort`, `PartialSort`), an ordinal string sort (`StringSort`), and a k-way merge of sorted runs (`KWayMerge`).
- [Statistics](api/statistics.md) — streaming summary statistics in bounded memory (`DDSketch` relative-error quantiles, `ReservoirSampler` uniform sampling, `RunningStatistics` single-pass moments, `RollingQuantile` exact sliding-window quantiles, `AliasTable` `O(1)` weighted random sampling, `KllSketch` rank-error quantiles over any comparable type).
- [Utilities](api/utilities.md) — the `Celerity.Primitives` helpers: `FastUtils` (`FastMod` / `FastDiv`, `CountDigits`, alignment), the struct PRNGs, `VarInt`, `FastGuid`, `SpanBits`, `BitWriter` / `BitReader`, `SimdReductions`, `Branchless`, `SortedSpan`, and `MortonCurve` / `HilbertCurve`.

## Built with Celerity

Standalone libraries built **on top of** Celerity — each solves a real problem in a domain where a pure-managed .NET implementation is the right call (chatty per-element work over managed keys, fixed-memory streaming over unbounded input, or hashing that must be identical across runtimes) and dropping to a native C/C++ library would be a net loss. Each ships as its own NuGet package that depends on `Celerity.Collections`, so you add only the one you need. See each package's README for the full API and runnable examples.

- [`Celerity.Ring`](../src/Celerity.Ring/README.md) — deterministic consistent-hash, rendezvous (HRW) and Maglev routing for sharding and request routing, with byte-identical node assignment across OS / architecture / runtime.
- [`Celerity.Sentinel`](../src/Celerity.Sentinel/README.md) — streaming abuse / heavy-hitter detection (top offenders, per-key rate, fan-out cardinality) in a fixed footprint regardless of key cardinality.
- [`Celerity.Cardinality`](../src/Celerity.Cardinality/README.md) — mergeable approximate `COUNT(DISTINCT)` and windowed dedup over unbounded streams, with deterministic cross-shard merge.

## Quick links

- [README](../README.md) — project overview.
- [ROADMAP](../ROADMAP.md) — planned milestones.
- [CONTRIBUTING](../CONTRIBUTING.md) — how to build, test, and submit PRs.
- [CHANGELOG](../CHANGELOG.md) — release history.
