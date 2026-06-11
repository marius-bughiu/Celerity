# Celerity.Hashing

Zero-cost struct hash providers and hash-quality tooling. Part of the
[Celerity](https://github.com/marius-bughiu/Celerity) family of high-performance
.NET libraries.

The hashers are `struct`s implementing `IHashProvider<T>`, so when a Celerity
collection is parameterised on one the JIT devirtualizes and inlines the hash
call — there is no interface dispatch on the hot path. They are positioned on
**distribution quality (avalanche), determinism, and adversarial resistance** —
not on beating `GetHashCode()` for raw speed (for `int` keys it *can't*:
`int.GetHashCode()` is identity, i.e. zero work).

## What's in the box

- **`IHashProvider<T>`** — the one-method (`int Hash(T)`) contract every hasher
  implements; the generic constraint `where THasher : struct, IHashProvider<T>`
  is what makes the collections zero-cost.
- **Integer hashers** — `Int32IdentityHasher` / `Int64IdentityHasher` (the
  zero-work floor), `*WangNaiveHasher`, `*WangHasher`, `*Murmur3Hasher` across
  `int` / `long` / `uint` / `ulong`, plus `GuidHasher`.
- **String hashers** — a wide ladder from cheap (`Djb2`, `Sdbm`, `Fnv1a`) to
  strong/keyed (`Murmur3`, `xxHash3`, `SipHash13/24`, `HighwayHash64`,
  `MetroHash64`, …). See the docs for the speed-vs-quality tradeoff and the
  HashDoS caveat (fixed-seed hashers are **not** a flooding defence).
- **`DefaultHasher<T>`** — fallback to `EqualityComparer<T>.Default.GetHashCode()`.
- **`HashQualityEvaluator`** — collision rate, bucket occupancy, chi-squared and
  a normalized distribution score for a candidate hasher / key shape.
- **`ProbeStatisticsEvaluator`** — replays the real open-addressed linear-probing
  placement to report average / worst-case probe length end-to-end.

See the [hashing API reference](https://github.com/marius-bughiu/Celerity/blob/main/docs/api/hashing.md)
for full docs, the "choosing a hasher" guide, and runnable examples.

## License

MIT
