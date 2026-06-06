# Celerity Documentation

This folder contains reference documentation for the Celerity high-performance collections library.

## Guides

- [Performance tuning](performance.md) — capacity, load factor, hasher selection, struct fast paths, and benchmarking.
- [Migration from BCL collections](migration.md) — mapping `Dictionary<,>`, `HashSet<>`, `ILookup<,>`, and `FrozenDictionary<,>` to Celerity types.
- [Troubleshooting](troubleshooting.md) — common errors and behavioural surprises, with fixes.
- [FAQ](faq.md) — conceptual questions about the design.
- [Testing & coverage](testing.md) — the test layers, property-based and fuzz harnesses, and how coverage is measured and gated.

## API reference

- [Collections](api/collections.md) — `CelerityDictionary`, `IntDictionary`, `LongDictionary`, `FrozenCelerityDictionary`, `CelerityMultiMap`, `CeleritySet`, `IntSet`, `LongSet`.
- [Hashing](api/hashing.md) — `IHashProvider<T>` interface and built-in hashers (`Int32WangNaiveHasher`, `Int32Murmur3Hasher`, `Int64WangHasher`, `Int64Murmur3Hasher`, `UInt32Hasher`, `UInt64Hasher`, `GuidHasher`, the `String*` hasher family, `DefaultHasher<T>`), and the `HashQualityEvaluator`.
- [Utilities](api/utilities.md) — `FastUtils` helper methods.
- [Native AOT & trimming](aot.md) — AOT / trim compatibility and how it is enforced.

## Quick links

- [README](../README.md) — project overview and benchmarks.
- [ROADMAP](../ROADMAP.md) — planned milestones.
- [CONTRIBUTING](../CONTRIBUTING.md) — how to build, test, and submit PRs.
- [CHANGELOG](../CHANGELOG.md) — release history.
