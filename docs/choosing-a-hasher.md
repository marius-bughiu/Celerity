# Choosing a hasher

Once the [collection is settled](choosing-a-collection.md), pick a hasher for your key shape. Defaults are good; escalate only with evidence (clustering, adversarial input). The [full hasher matrix](api/hashing.md) documents every option and its tradeoff.

| Key type | Default | When to escalate |
|---|---|---|
| `int` / `long` | `Int32WangNaiveHasher` / `Int64WangNaiveHasher` (built into `IntDictionary` / `LongDictionary`) | Uniform / trusted keys (dense sequential IDs) → *drop* to `Int32IdentityHasher` (the zero-work floor — no mixing, nothing beats it on speed), or `Int64IdentityHasher` **when the low 32 bits are the discriminating ones** — it truncates, so `long` keys carrying a shard tag or timestamp in the high word want `Int64WangNaiveHasher` instead. Clustered keys → `Int32WangHasher` → `Int32Murmur3Hasher` (the Wang full finalizer is a cheaper middle tier than Murmur3). |
| `uint` / `ulong` | `UInt32WangNaiveHasher` (cheap XOR-fold) / `UInt64Murmur3Hasher` (`fmix64`) | Uniform / trusted keys → *drop* to `UInt32IdentityHasher` (the zero-work floor; the cast keeps all 32 bits, so nothing is lost), or `UInt64IdentityHasher` **when the low 32 bits are the discriminating ones** — it truncates, so `ulong` keys carrying a shard tag or timestamp in the high word want `UInt64WangNaiveHasher` instead. `uint`: → `UInt32WangHasher` → `UInt32Murmur3Hasher`. `ulong`: drop to `UInt64WangHasher` / `UInt64WangNaiveHasher` when the `fmix64` multiplies cost more than they buy on uniform keys. |
| `string` (ASCII) | `StringFnV1AHasher` (folds the low byte per char) | Non-ASCII or long keys → `StringFnV1AFullHasher` / `StringFnV1A64Hasher`. Clustered keys → strong-avalanche `StringMurmur3Hasher`, `StringXxHash3Hasher`, etc. |
| `string` (untrusted input) | `DefaultHasher<string>` (BCL Marvin32, per-process-randomized) | A **keyed** PRF — `StringSipHash13Hasher` (Rust's default), `StringSipHash24Hasher`, `StringHalfSipHash24Hasher`, or `StringHighwayHash64Hasher` — but only resists hash-flooding if seeded with a *secret, per-process-random* key; with a fixed seed it is deterministic, not DoS-resistant (see caveat below). |
| `Guid` | `GuidHasher` | — |
| Any other type | `DefaultHasher<T>` (delegates to `EqualityComparer<T>.Default`) | Replace with a hand-written struct hasher if profiling shows `Hash` on the hot path. |

> **Counting past ~10^8 distinct elements? Pick a 64-bit hasher.** The probabilistic sketches (`HyperLogLog`, `SlidingHyperLogLog`, `MinHashSketch`, `BloomFilter`, `CuckooFilter`, `XorFilter`, `CountMinSketch`) never store the element, so two elements that hash alike are indistinguishable forever — and a 32-bit hash reaches only 2^32 ≈ 4.3 billion values, no matter how it is widened. Hashers that carry genuine 64-bit entropy implement **`IHashProvider64<T>`** and the sketches route through it automatically: `Int64WangHasher` / `Int64Murmur3Hasher` (`long`), `UInt64WangHasher` / `UInt64Murmur3Hasher` (`ulong`), `GuidHasher` (`Guid`), and the nine 64-bit `string` hashers (`StringXxHash64Hasher`, `StringXxHash3Hasher`, `StringCityHash64Hasher`, `StringMetroHash64Hasher`, `StringHighwayHash64Hasher`, `StringSipHash13Hasher`, `StringSipHash24Hasher`, `StringFnV1A64Hasher`, `StringFnV164Hasher`). They already computed 64 bits internally and folded them away, so `Hash64` costs nothing extra. With a 32-bit-only hasher the sketches still work — `HyperLogLog` applies the classical large-range correction so its estimate stays honest — but the entropy floor is real. See [`IHashProvider64<T>`](api/hashing.md#ihashprovider64t).

The value of a struct hasher is **distribution quality (avalanche), determinism, and the zero-cost devirtualized generic** — *not* raw hashing speed. For `int` / `uint` keys especially, `GetHashCode()` is already the identity (zero work), so no mixing hasher beats it on speed; the `Int32` / `Int64` / `UInt32` / `UInt64` `IdentityHasher`s expose that zero-work floor explicitly — one per integer width, because the collections invoke the hasher through the `IHashProvider<TKey>` constraint themselves, so there is no call site at which a `uint` key could be cast to reach the `int` one. Skip mixing when keys are already uniform, and escalate to a mixer only when distribution (not speed) demands it.

> **Fixed-seed hashers are not a HashDoS defence.** `string.GetHashCode()` is already a purpose-built **Marvin32** with per-process random seeding; a hardcoded-seed Murmur3 / FNV / xxHash is *not* more flood-resistant — usually **less**, because an attacker who knows the fixed algorithm and seed can precompute colliding keys offline. What stops hash-flooding is a **keyed** PRF with a *secret, per-process-random* key, not merely picking a "stronger" fixed hash. For untrusted `string` keys, the BCL `string.GetHashCode()` (`DefaultHasher<string>`) is the safe default; reach for the keyed SipHash / HighwayHash hashers only when you also supply a secret seed. The fixed-seed hashers' real strength is **reproducibility** (same code across processes and runtimes), which `GetHashCode()` deliberately does not give you.

> **Probing from a `ReadOnlySpan<char>`? Any `String*Hasher` will do.** All 23 implement **`ISpanHashProvider`** — a `Hash(ReadOnlySpan<char>)` overload that returns exactly what `Hash(string)` returns for the same characters — which is what unlocks the [span-keyed lookups](api/collections.md#span-keyed-lookups) and `StringInternTable`. The two overloads share one body, so they cannot drift; a custom string hasher only needs to implement the interface to work the same way. See [`ISpanHashProvider`](api/hashing.md#ispanhashprovider).

The hashing library also ships classic / compatibility hashes (djb2, sdbm, ELF/PJW, CRC-32, Adler-32, FNV-1, MurmurHash2, CityHash, MetroHash, xxHash32/64) for matching an external system's key distribution — see [`docs/api/hashing.md`](api/hashing.md) for the complete list, costs, and avalanche notes, and use `HashQualityEvaluator` (below) to compare candidates on your own keys.

## Custom hashing

Implement `IHashProvider<T>` as a **struct** (required by `where THasher : struct, IHashProvider<T>`) so the JIT can devirtualize and inline `Hash()`:

```csharp
public interface IHashProvider<T>
{
    int Hash(T key);
}
```

For the probabilistic sketches there is a 64-bit sibling. Implement it alongside when your hasher genuinely carries 64 bits of entropy — see [`IHashProvider64<T>`](api/hashing.md#ihashprovider64t) for the contract and why widening a 32-bit code does not qualify:

```csharp
public interface IHashProvider64<T>
{
    ulong Hash64(T key);
}
```

The package ships built-in hashers for `int`, `long`, `uint`, `ulong`, `Guid`, and `string`, plus a `DefaultHasher<T>` fallback. Not sure which fits your key shape? `HashQualityEvaluator.Evaluate<T, THasher>(keys)` runs a key sample through a hasher and returns a `HashQualityReport` (collision count, bucket occupancy, max bucket load, chi-squared, and a normalized distribution score where `1.0` = ideal uniform) — a diagnostic to compare candidates offline before committing. For the metric a lookup actually pays, `ProbeStatisticsEvaluator.Evaluate<T, THasher>(keys)` replays the real open-addressed linear-probing placement and returns a `ProbeStatistics` (average / worst-case **probe length** and the open-addressing collision rate). See [`docs/api/hashing.md`](api/hashing.md#hash-quality-evaluation).
