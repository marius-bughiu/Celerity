# Celerity Documentation

This folder contains reference documentation for the Celerity high-performance collections library.

## Guides

- [Performance tuning](performance.md) — capacity, load factor, hasher selection, struct fast paths, and benchmarking.
- [Migration from BCL collections](migration.md) — mapping `Dictionary<,>`, `HashSet<>`, `ILookup<,>`, and `FrozenDictionary<,>` to Celerity types.
- [Troubleshooting](troubleshooting.md) — common errors and behavioural surprises, with fixes.
- [FAQ](faq.md) — conceptual questions about the design.
- [Testing & coverage](testing.md) — the test layers, property-based and fuzz harnesses, and how coverage is measured and gated.

## API reference

- [Collections](api/collections.md) — dictionaries (`CelerityDictionary`, `RobinHoodDictionary`, `SwissDictionary`, `HashCachingDictionary`, `PooledCelerityDictionary`, `IntDictionary`, `LongDictionary`, `SmallDictionary`, `EnumMap`, `FrozenCelerityDictionary`), sets (`CeleritySet`, `SwissSet`, `RobinHoodSet`, `HashCachingSet`, `PooledCeleritySet`, `IntSet`, `LongSet`, `SmallSet`, `EnumSet`, `FrozenCeleritySet`), multi-collections (`CelerityMultiMap`, `CelerityMultiSet`), and probabilistic / bit collections (`BitSet`, `BloomFilter`, `CuckooFilter`, `XorFilter`, `HyperLogLog`, `CountMinSketch`, `TopKSketch`).
- [Hashing](api/hashing.md) — `IHashProvider<T>` interface and built-in hashers (`Int32WangNaiveHasher`, `Int32Murmur3Hasher`, `Int64WangHasher`, `Int64Murmur3Hasher`, `UInt32Hasher`, `UInt64Hasher`, `GuidHasher`, the `String*` hasher family, `DefaultHasher<T>`), and the `HashQualityEvaluator`.
- [Utilities](api/utilities.md) — `FastUtils` helper methods.
- [Native AOT & trimming](aot.md) — AOT / trim compatibility and how it is enforced.

## Built with Celerity

Standalone packages built on top of `Celerity.Collections` — each ships as its own NuGet package, so you add only the one you need. See each package's README for the full API and runnable examples.

- [`Celerity.Ring`](../src/Celerity.Ring/README.md) — deterministic consistent-hash & rendezvous (HRW) rings for sharding and request routing, with byte-identical node assignment across OS / architecture / runtime.
- [`Celerity.Sentinel`](../src/Celerity.Sentinel/README.md) — streaming abuse / heavy-hitter detection (top offenders, per-key rate, fan-out cardinality) in a fixed footprint regardless of key cardinality.
- [`Celerity.Cardinality`](../src/Celerity.Cardinality/README.md) — mergeable approximate `COUNT(DISTINCT)` and windowed dedup over unbounded streams, with deterministic cross-shard merge.

## Quick links

- [README](../README.md) — project overview and benchmarks.
- [ROADMAP](../ROADMAP.md) — planned milestones.
- [CONTRIBUTING](../CONTRIBUTING.md) — how to build, test, and submit PRs.
- [CHANGELOG](../CHANGELOG.md) — release history.
