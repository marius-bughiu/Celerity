# Hashing API Reference

All hashing types live in the `Celerity.Hashing` namespace.

## IHashProvider&lt;T&gt;

The core abstraction for plugging custom hash functions into Celerity collections.

```csharp
public interface IHashProvider<T>
{
    int Hash(T key);
}
```

Implementations **must be structs** when used with `CelerityDictionary` or `IntDictionary`. The generic constraint `where THasher : struct, IHashProvider<T>` allows the JIT to devirtualize and inline the `Hash` call, eliminating virtual-dispatch overhead on every probe.

### Implementing a custom hasher

```csharp
using System.Runtime.CompilerServices;
using Celerity.Hashing;

public struct MyInt32Hasher : IHashProvider<int>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(int key)
    {
        // Your hash logic here. Must return an int.
        // The dictionary masks the result to fit the table size,
        // so negative values are fine.
        return key * 2654435761; // Knuth multiplicative hash
    }
}
```

Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` to hint the JIT. All built-in hashers do this.

---

## Built-in Hashers

### Int32WangNaiveHasher

```csharp
public struct Int32WangNaiveHasher : IHashProvider<int>
```

A lightweight 32-bit integer hash that XORs the key with its upper 16 bits shifted down. Fast and sufficient for uniformly distributed integer keys. This is the default hasher used by `IntDictionary<TValue>`.

**Algorithm:** `key ^ (key >> 16)`

### Int64WangNaiveHasher

```csharp
public struct Int64WangNaiveHasher : IHashProvider<long>
```

The 64-bit counterpart to `Int32WangNaiveHasher`: an extremely cheap XOR-fold over the upper 32 bits and the middle 16 bits of the key. Modest avalanche but very low latency — the cheapest hasher in the `long` family. This is the default hasher used by `LongDictionary<TValue>` and `LongSet`.

**Algorithm:** `(int)key ^ (int)((ulong)key >> 32) ^ (int)((ulong)key >> 16)`

The extra middle-16-bits fold over a naive `key.GetHashCode()` keeps a chunk of the high-half entropy in the result, which materially improves distribution on keys whose low 32 bits are sequential (e.g. monotonically allocated IDs whose upper bits carry type / shard). Prefer it when the key distribution is already reasonably uniform and latency matters more than collision resistance; for adversarial or heavily clustered keys, switch to `Int64WangHasher` (full Thomas-Wang finalizer) or `Int64Murmur3Hasher`.

### Int64Murmur3Hasher

```csharp
public struct Int64Murmur3Hasher : IHashProvider<long>
```

The finalizer stage of MurmurHash3 applied to 64-bit integer keys. Produces well-distributed hashes even for clustered input. Returns the lower 32 bits of the mixed result.

**Algorithm:** Three rounds of XOR-shift and multiply using the standard Murmur3 constants (`0xff51afd7ed558ccd`, `0xc4ceb9fe1a85ec53`), followed by a final XOR-shift, truncated to `int`.

### StringFnV1AHasher

```csharp
public struct StringFnV1AHasher : IHashProvider<string>
```

FNV-1a 32-bit hash for string keys. Iterates over each `char` in the string, XORing the lower byte with the running hash and multiplying by the FNV prime.

**Parameters:** offset basis = `2166136261`, prime = `16777619`.

**Note:** This hasher uses only the lower byte of each character (`c & 0xFF`), which means it does not fully distinguish characters that differ only in their upper byte. For most ASCII-dominated workloads this is fine; for keys with significant non-ASCII content, prefer `StringMurmur3Hasher` (below), which consumes the full character.

### StringMurmur3Hasher

```csharp
public struct StringMurmur3Hasher : IHashProvider<string>
```

The MurmurHash3 x86_32 algorithm applied to the string's UTF-16 code units. This is the string counterpart to `Int32Murmur3Hasher` / `Int64Murmur3Hasher`: it gives strings the same strong-escalation option that `int` and `long` already have over their fast-fold defaults. Two properties set it apart from `StringFnV1AHasher`:

- It consumes the **full** 16-bit value of every character (treating the string as its native little-endian UTF-16 byte stream), so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.
- The MurmurHash3 finalizer (`fmix32`) gives every input bit influence over every output bit, holding distribution up on clustered or adversarial keys that would push FNV-1a into long probe chains.

Prefer it over `StringFnV1AHasher` for keys with significant non-ASCII content, or when collision resistance matters more than the few cycles FNV-1a saves on short ASCII keys.

**Algorithm:** standard MurmurHash3 x86_32 — pairs of characters are read as little-endian 32-bit blocks (mixed with `0xcc9e2d51` / `0x1b873593` and `ROL`/`ROL`/`*5 + 0xe6546b64`), a trailing odd character is folded as a 2-byte tail, then the byte length is XOR-ed in and the result run through `fmix32`.

**Note:** maps the empty string `""` → `0` (the fixed point of `fmix32` over a zero accumulator), just as the integer Murmur3 hashers map `0 → 0`. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### Int32Murmur3Hasher

```csharp
public struct Int32Murmur3Hasher : IHashProvider<int>
```

The MurmurHash3 32-bit finalizer (`fmix32`) applied to `int` keys. Better avalanche than `Int32WangNaiveHasher` — prefer when key distribution is clustered or adversarial.

**Note:** maps `0 → 0` (a fixed point of `fmix32`), so the dictionaries' out-of-band zero-key slot is engaged just as it is with the simpler hashers.

### Int64WangHasher

```csharp
public struct Int64WangHasher : IHashProvider<long>
```

Thomas Wang's 64-bit integer hash for `long` keys. Faster than `Int64Murmur3Hasher` while still providing better avalanche than a simple XOR-fold. Bijective on `ulong`, so the only source of collisions is truncation to 32 bits when the result is returned. Prefer it over the default `Int64WangNaiveHasher` when the key distribution is clustered or adversarial and the XOR-fold's collision behaviour is no longer acceptable; prefer `Int64Murmur3Hasher` when even better avalanche is needed.

### UInt32Hasher

```csharp
public struct UInt32Hasher : IHashProvider<uint>
```

Wang/Jenkins-style bit-mixer for `uint` keys. Counterpart to `Int32WangNaiveHasher` for unsigned 32-bit integers.

### UInt64Hasher

```csharp
public struct UInt64Hasher : IHashProvider<ulong>
```

MurmurHash3 64-bit finalizer (`fmix64`) for `ulong` keys. Counterpart to `Int64Murmur3Hasher` for unsigned 64-bit integers.

### GuidHasher

```csharp
public struct GuidHasher : IHashProvider<Guid>
```

Reinterprets the 128-bit `Guid` as two 64-bit halves (via `Unsafe.As<Guid, ulong>`, no stack buffer), runs `fmix64` on each, and XORs the mixed halves. Prefer over `DefaultHasher<Guid>` on hot paths — it is fully inlineable and avoids the `EqualityComparer<Guid>.Default` virtual dispatch.

**Note:** maps `Guid.Empty → 0`, so dictionaries / sets keyed on `Guid` engage the out-of-band default-key slot for `Guid.Empty`.

### DefaultHasher&lt;T&gt;

```csharp
public struct DefaultHasher<T> : IHashProvider<T>
```

A general-purpose `IHashProvider<T>` that delegates to `EqualityComparer<T>.Default.GetHashCode()`. Use it when no specialized hasher exists for a key type — custom structs, reference types without a built-in hasher, or to bridge an existing `IEqualityComparer<T>`-based design into the Celerity API.

It is a struct, so the JIT devirtualizes the outer call on the probe path. The inner `EqualityComparer<T>` dispatch is unavoidable but acceptable for non-hot-path types — if `Hash` is on the hot path for a known key type, write a struct-specific hasher instead.

### Choosing a hasher

| Key type | Default | Alternative |
|---|---|---|
| `int` | `Int32WangNaiveHasher` (used by `IntDictionary` / `IntSet`) | `Int32Murmur3Hasher` for clustered or adversarial keys |
| `long` | `Int64WangNaiveHasher` (used by `LongDictionary` / `LongSet`) | `Int64WangHasher` (full Thomas-Wang finalizer) or `Int64Murmur3Hasher` for clustered or adversarial keys |
| `uint` | `UInt32Hasher` | — |
| `ulong` | `UInt64Hasher` | — |
| `Guid` | `GuidHasher` | `DefaultHasher<Guid>` (slower but BCL-equivalent) |
| `string` | `StringFnV1AHasher` | `StringMurmur3Hasher` for non-ASCII content or clustered / adversarial keys; `DefaultHasher<string>` (uses the BCL string hasher) |
| anything else | `DefaultHasher<T>` | a struct hasher you write |
