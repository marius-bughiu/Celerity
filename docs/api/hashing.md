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

### StringDjb2Hasher

```csharp
public struct StringDjb2Hasher : IHashProvider<string>
```

Daniel J. Bernstein's classic **djb2** hash applied to the string's native little-endian UTF-16 byte stream — it folds both bytes of every character (low byte then high byte). The accumulator is seeded with the magic constant `5381`, and each byte is folded with the single step `hash = hash * 33 + b`. Because `33` is a power of two plus one, the multiply lowers to a shift-and-add (`(hash << 5) + hash`), so the function uses **no real multiply, no table, and no finalizer** — making it the simplest and cheapest of the classic string hashes (even FNV-1a pays a multiply by its 32-bit prime per byte). Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

Its tradeoff is weaker avalanche: djb2 has no shift/xor diffusion step, so a single changed input byte propagates less thoroughly than under `StringJenkinsOaatHasher`. It sits at the cheapest, classic end of the `string` escalation ladder, a peer to the FNV-1a variants and `StringJenkinsOaatHasher`: `StringDjb2Hasher` (cheapest, shift-and-add, classic, weaker avalanche) / `StringFnV1AHasher` (low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (cheap FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche than djb2 or FNV-1a) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Prefer it when you want the simplest, most familiar cheap hash and your keys are short ASCII identifiers; step up to `StringJenkinsOaatHasher` (still cheap, but with proper shift/xor diffusion) or the FNV-1a variants when djb2's weak avalanche starts clustering keys, and escalate to the throughput-oriented strong family for clustered or adversarial keys.

**Note:** maps the empty string `""` → the seed constant `5381` — no characters are folded — exactly as FNV-1a maps the empty string to its offset basis. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringFnV1AHasher

```csharp
public struct StringFnV1AHasher : IHashProvider<string>
```

FNV-1a 32-bit hash for string keys. Iterates over each `char` in the string, XORing the lower byte with the running hash and multiplying by the FNV prime.

**Parameters:** offset basis = `2166136261`, prime = `16777619`.

**Note:** This hasher uses only the lower byte of each character (`c & 0xFF`), which means it does not fully distinguish characters that differ only in their upper byte. For most ASCII-dominated workloads this is fine; for keys with significant non-ASCII content, prefer `StringFnV1AFullHasher` (below — same FNV-1a cost class, but folds the full character) or `StringMurmur3Hasher` (the strong-avalanche option).

### StringFnV1AFullHasher

```csharp
public struct StringFnV1AFullHasher : IHashProvider<string>
```

FNV-1a 32-bit hash for string keys over the **full** UTF-16 representation — it folds both bytes of every character (low byte then high byte), which is exactly the FNV-1a of the string's native little-endian UTF-16 byte stream. This is the full-character-width counterpart to `StringFnV1AHasher`, and directly answers that hasher's "for keys with significant non-ASCII content, a full UTF-8 or UTF-16 hash is preferable" note. Where `StringFnV1AHasher` folds only the low byte and so collides characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`) — this hasher keeps them distinct, while still costing only a pair of XOR / multiply steps per character (no block reads, rotates, or `fmix32` finalizer).

It sits in the `string` escalation ladder: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` (cheap, full Unicode width, 32-bit state) → `StringFnV1A64Hasher` (full Unicode width, 64-bit state) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` (strong avalanche). Prefer it over `StringFnV1AHasher` whenever keys contain non-ASCII characters the low-byte fold would collide; step up to `StringFnV1A64Hasher` when keys are long or numerous enough that the 32-bit accumulator starts clustering; escalate to `StringMurmur3Hasher`, `StringXxHash32Hasher`, or `StringXxHash64Hasher` when FNV-1a's weaker avalanche pushes clustered or adversarial keys into long probe chains.

**Parameters:** offset basis = `2166136261`, prime = `16777619`.

**Note:** maps the empty string `""` → the offset basis (`2166136261` unsigned, `-2128831035` signed) — no characters are folded — exactly as `StringFnV1AHasher` maps the empty string. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringFnV1A64Hasher

```csharp
public struct StringFnV1A64Hasher : IHashProvider<string>
```

FNV-1a **64-bit** hash for string keys over the **full** UTF-16 representation — it folds the same little-endian UTF-16 byte stream as `StringFnV1AFullHasher` (low byte then high byte of every character), but accumulates into a 64-bit state using the FNV-1a 64-bit parameters, then xor-folds the result down to a signed 32-bit value. Carrying twice as many bits through the accumulation means intermediate values collide far less often, so for longer keys and larger key sets the distribution holds up better than the 32-bit FNV-1a hashers before the final fold. The fold (`h ^ (h >> 32)`) is the standard FNV retraction and keeps the extra high-half entropy in the returned value.

It sits one rung above `StringFnV1AFullHasher` on the `string` escalation ladder: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` (cheap, full Unicode width, 32-bit state) → `StringFnV1A64Hasher` (full Unicode width, 64-bit state) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` (strong avalanche). Like `StringFnV1AFullHasher` it costs only a pair of XOR / multiply steps per character — but each multiply is 64-bit, which on 64-bit platforms is the same throughput as a 32-bit multiply, so the wider state is effectively free there. Prefer it over `StringFnV1AFullHasher` when keys are long or numerous enough that the narrower 32-bit accumulator starts clustering; escalate to `StringMurmur3Hasher`, `StringXxHash32Hasher`, or `StringXxHash64Hasher` when FNV-1a's weaker per-bit avalanche pushes clustered or adversarial keys into long probe chains regardless of state width.

**Parameters:** offset basis = `14695981039346656037`, prime = `1099511628211`.

**Note:** maps the empty string `""` → the 64-bit offset basis xor-folded to 32 bits — no characters are folded. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringJenkinsOaatHasher

```csharp
public struct StringJenkinsOaatHasher : IHashProvider<string>
```

Bob Jenkins' **one-at-a-time** (OAAT) hash applied to the string's native little-endian UTF-16 byte stream — it folds both bytes of every character (low byte then high byte). For each byte it runs the add / shift / xor lattice `hash += b; hash += hash << 10; hash ^= hash >> 6`, then finishes with a three-step avalanche `hash += hash << 3; hash ^= hash >> 11; hash += hash << 15`. Where FNV-1a folds each byte with a single xor and one multiply — and so has comparatively weak per-bit avalanche — the shift / xor steps give every input bit influence over the whole 32-bit result, yet the function uses **no multiplies at all** (only adds, shifts, and xors), keeping it in the same cheap cost class. It is the classic "better distributed than FNV-1a, cheaper than the block hashes" middle option. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits in the cheap tier of the `string` escalation ladder, between the FNV-1a variants and the strong block hashes: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (cheap FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche than FNV-1a) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Prefer it over the FNV-1a hashers when FNV-1a's single-multiply mixing clusters your keys but you do not want to pay for a block hash; escalate to the throughput-oriented strong family when the keys are clustered or adversarial enough to push long probe chains regardless.

**Note:** maps the empty string `""` → `0` — the accumulator starts at zero and the three finalization steps leave zero unchanged. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

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

### StringXxHash32Hasher

```csharp
public struct StringXxHash32Hasher : IHashProvider<string>
```

The xxHash32 (XXH32) algorithm, seed `0`, applied to the string's native little-endian UTF-16 byte stream. This is the throughput-oriented sibling of `StringMurmur3Hasher` at the strong-distribution top of the `string` ladder. xxHash processes the input in 16-byte stripes across **four** independent accumulators — so the work parallelises within a single core's pipeline — then folds them together and runs an avalanche finalizer that gives every input bit influence over every output bit. Where MurmurHash3 mixes one 32-bit block at a time through a single accumulator, XXH32 keeps four in flight and only combines them once the bulk of the input is consumed, which wins on throughput as keys get longer. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits alongside `StringMurmur3Hasher` on the `string` escalation ladder: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` (cheap, full Unicode width, 32-bit state) → `StringFnV1A64Hasher` (full Unicode width, 64-bit state) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` (strong avalanche). Prefer `StringXxHash32Hasher` over `StringMurmur3Hasher` when keys are long enough that the four-accumulator stripe loop wins on throughput; prefer `StringMurmur3Hasher` for very short keys, where its simpler single-accumulator loop has less fixed overhead. For longer keys on a 64-bit platform, `StringXxHash64Hasher` widens the accumulators and the stripe further. All three are good answers when FNV-1a's weaker avalanche pushes clustered or adversarial keys into long probe chains.

**Algorithm:** standard XXH32 — for inputs of at least 16 bytes, four accumulators are advanced by `acc = rotl(acc + lane * PRIME32_2, 13) * PRIME32_1` over 16-byte stripes (eight chars) and merged via `rotl(v1,1) + rotl(v2,7) + rotl(v3,12) + rotl(v4,18)`; shorter inputs start from `PRIME32_5`. The byte length is then added, remaining 4-byte lanes (`+= lane * PRIME32_3`, `rotl 17`, `* PRIME32_4`) and the trailing 2-byte char (two per-byte `+= b * PRIME32_5`, `rotl 11`, `* PRIME32_1` steps) are mixed in, and the result is run through the XXH32 avalanche.

**Note:** `Hash(s)` equals canonical XXH32 (seed `0`) over `Encoding.Unicode.GetBytes(s)`. The empty string maps to the well-known canonical empty-input vector `0x02CC5D05` (its UTF-16 byte stream is zero bytes). The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringXxHash64Hasher

```csharp
public struct StringXxHash64Hasher : IHashProvider<string>
```

The xxHash64 (XXH64) algorithm, seed `0`, applied to the string's native little-endian UTF-16 byte stream, xor-folded down to a signed 32-bit result. This is the wide-accumulator counterpart to `StringXxHash32Hasher`: it processes the input in **32-byte stripes** (sixteen chars) across four **64-bit** accumulators and finishes with the XXH64 avalanche. Carrying twice as many bits through the accumulation means intermediate values collide far less often, so for longer keys and larger key sets the distribution holds up even better than XXH32 before the final fold; the 64-bit result is reduced to 32 bits by xor-folding the high half into the low half (`h ^ (h >> 32)`), keeping the extra high-half entropy in the returned value. Each multiply is 64-bit, which on 64-bit platforms is the same throughput as a 32-bit multiply, and the wider stripe lets the loop consume input twice as fast, so the wider state is effectively free there. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits at the strong-distribution top of the `string` escalation ladder alongside `StringMurmur3Hasher` and `StringXxHash32Hasher`: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` (cheap, full Unicode width, 32-bit state) → `StringFnV1A64Hasher` (full Unicode width, 64-bit state) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` (strong avalanche). Prefer `StringXxHash64Hasher` over `StringXxHash32Hasher` when keys are long enough that the wider 64-bit accumulator and 32-byte stripe pull ahead on a 64-bit platform; prefer `StringXxHash32Hasher` or `StringMurmur3Hasher` for shorter keys, where the narrower-state loops have less fixed overhead. All three are good answers when FNV-1a's weaker avalanche pushes clustered or adversarial keys into long probe chains.

**Algorithm:** standard XXH64 — for inputs of at least 32 bytes, four accumulators are advanced by `acc = rotl(acc + lane * PRIME64_2, 31) * PRIME64_1` over 32-byte stripes (sixteen chars), merged via `rotl(v1,1) + rotl(v2,7) + rotl(v3,12) + rotl(v4,18)` and four merge rounds; shorter inputs start from `PRIME64_5`. The byte length is then added, remaining 8-byte lanes (the same round, XOR-ed in, then `rotl 27`, `* PRIME64_1`, `+ PRIME64_4`), a remaining 4-byte lane (`^= lane * PRIME64_1`, `rotl 23`, `* PRIME64_2`, `+ PRIME64_3`), and the trailing 2-byte char (two per-byte `^= b * PRIME64_5`, `rotl 11`, `* PRIME64_1` steps) are mixed in, the result is run through the XXH64 avalanche, and finally xor-folded to 32 bits.

**Note:** `Hash(s)` equals canonical XXH64 (seed `0`) over `Encoding.Unicode.GetBytes(s)`, xor-folded to 32 bits. The empty string maps to the well-known canonical empty-input vector `0xEF46DB3751D8E999` folded to 32 bits (its UTF-16 byte stream is zero bytes). The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel. For longer keys, `StringMetroHash64Hasher` is a peer worth profiling against.

### StringMetroHash64Hasher

```csharp
public struct StringMetroHash64Hasher : IHashProvider<string>
```

The MetroHash64 algorithm (J. Andrew Rogers' `metrohash64_1` variant), seed `0`, applied to the string's native little-endian UTF-16 byte stream, xor-folded down to a signed 32-bit result. MetroHash is a family of statistically robust hash functions designed for maximum throughput on modern 64-bit CPUs. Like `StringXxHash64Hasher` it processes the input in **32-byte stripes** (sixteen chars) across four independent **64-bit** accumulators, but its lattice of multiply / rotate / add steps gives the out-of-order core four independent multiply chains to keep in flight, so on mid-length keys it is competitive with — and often beats — the xxHash family. The 64-bit result is reduced to 32 bits by xor-folding the high half into the low half (`h ^ (h >> 32)`), keeping the extra high-half entropy in the returned value. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits at the strong-distribution top of the `string` escalation ladder alongside `StringMurmur3Hasher`, `StringXxHash32Hasher`, `StringXxHash64Hasher`, and `StringCityHash64Hasher`: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` (cheap, full Unicode width, 32-bit state) → `StringFnV1A64Hasher` (full Unicode width, 64-bit state) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche). `StringMetroHash64Hasher`, `StringXxHash64Hasher`, `StringXxHash32Hasher`, and `StringCityHash64Hasher` are peers at the throughput-oriented top of the ladder — profile on your own key shape to pick between them; prefer `StringMurmur3Hasher` for very short keys, where its simpler single-accumulator loop has less fixed overhead. All are good answers when FNV-1a's weaker avalanche pushes clustered or adversarial keys into long probe chains.

**Algorithm:** standard `metrohash64_1` — the hash starts at `(k2 * k0) + byteLength`; for inputs of at least 32 bytes, four accumulators advance by `v += lane * kN; v = rotr(v, 29) + vNext` over 32-byte stripes (sixteen chars), then a post-loop lattice of `rotr(…, 33)` mixes and folds them back in. The remaining tail is consumed as a 16-byte block (two lanes mixed through `rotr 33` / `rotr 35`), an 8-byte lane (`rotr 33`), a 4-byte block (`rotr 15`), and a trailing 2-byte char (`rotr 13`); the result is finalized with `rotr 33` / `* k0` / `rotr 33` and finally xor-folded to 32 bits. Constants: `k0 = 0xC83A91E1`, `k1 = 0x8648DBDB`, `k2 = 0x7BDEC03B`, `k3 = 0x2F5870A5`.

**Note:** `Hash(s)` equals canonical MetroHash64 (`metrohash64_1`, seed `0`) over `Encoding.Unicode.GetBytes(s)`, xor-folded to 32 bits. The empty string maps to the algorithm's length-`0` result — `finalize(k2 * k0)` — folded to 32 bits (its UTF-16 byte stream is zero bytes, so no stripes or lanes are consumed). The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringCityHash64Hasher

```csharp
public struct StringCityHash64Hasher : IHashProvider<string>
```

Google's CityHash64 algorithm (the `CityHash64` entry point of the v1.1 reference), applied to the string's native little-endian UTF-16 byte stream, xor-folded down to a signed 32-bit result. CityHash is a family of strong, fast non-cryptographic hash functions built around 64-bit multiplies and rotates. Unlike the single stripe loop of the xxHash / MetroHash family, CityHash64 is **length-classed**: inputs of 16 bytes or fewer, 17–32 bytes, and 33–64 bytes each take a dedicated branch of multiply / rotate / `ShiftMix` steps, and only inputs over 64 bytes enter the 56-byte-state main loop (two `WeakHashLen32WithSeeds` blocks plus `x`, `y`, `z`) consuming 64 bytes (thirty-two chars) per iteration. This makes it especially strong on the short-to-mid key lengths typical of identifiers and dictionary keys, where a stripe-oriented hash spends most of its time in tail handling. The 64-bit result is reduced to 32 bits by xor-folding the high half into the low half (`h ^ (h >> 32)`), keeping the extra high-half entropy in the returned value. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits at the strong-distribution top of the `string` escalation ladder alongside `StringMurmur3Hasher`, `StringXxHash32Hasher`, `StringXxHash64Hasher`, and `StringMetroHash64Hasher`: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` (cheap, full Unicode width, 32-bit state) → `StringFnV1A64Hasher` (full Unicode width, 64-bit state) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche). `StringCityHash64Hasher`, `StringMetroHash64Hasher`, `StringXxHash64Hasher`, and `StringXxHash32Hasher` are peers at the throughput-oriented top of the ladder — profile on your own key shape to pick between them; CityHash's length-classed short paths often edge ahead on short keys where the stripe-oriented hashers pay more fixed tail overhead, while `StringMurmur3Hasher` stays the simplest choice for very short keys. All are good answers when FNV-1a's weaker avalanche pushes clustered or adversarial keys into long probe chains.

**Algorithm:** standard CityHash64 (v1.1) — dispatch on the byte length into four classes: ≤ 16 bytes (`HashLen0to16` — a `ShiftMix` of two multiplied words, or the single-/four-byte tail forms), 17–32 bytes (`HashLen17to32` — four words mixed through `HashLen16`), 33–64 bytes (`HashLen33to64` — eight words mixed with three `bswap_64` steps), and over 64 bytes (the 56-byte-state main loop over 64-byte chunks). All paths finalize through `HashLen16(u, v) = let a = (u^v)*kMul; a ^= a>>47; b = (v^a)*kMul; b ^= b>>47; b*kMul`, and the result is xor-folded to 32 bits. Constants: `k0 = 0xC3A5C85C97CB3127`, `k1 = 0xB492B66FBE98F273`, `k2 = 0x9AE16A3B2F90404F`, `kMul = 0x9DDFEA08EB382D69`.

**Note:** `Hash(s)` equals canonical CityHash64 (v1.1) over `Encoding.Unicode.GetBytes(s)`, xor-folded to 32 bits. The empty string maps to the algorithm's length-`0` result — the constant `k2` (`0x9AE16A3B2F90404F`) — folded to 32 bits (its UTF-16 byte stream is zero bytes, so the shortest length class returns `k2` directly). The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel. `StringXxHash3Hasher` is the third-generation xxHash peer in the same throughput-oriented tier; when the keys come from an untrusted source, the keyed `StringHalfSipHash24Hasher` / `StringSipHash13Hasher` / `StringSipHash24Hasher` / `StringHighwayHash64Hasher` trade some of this throughput for hash-flooding resistance.

### StringXxHash3Hasher

```csharp
public struct StringXxHash3Hasher : IHashProvider<string>
```

The XXH3 algorithm (64-bit output, default secret, seed `0`), applied to the string's native little-endian UTF-16 byte stream, xor-folded down to a signed 32-bit result. XXH3 is the third-generation member of the xxHash family — the successor to `StringXxHash32Hasher` (XXH32) and `StringXxHash64Hasher` (XXH64) — and the highest-throughput option in the box across most key shapes. Unlike the single stripe loop of XXH32 / XXH64, XXH3 mixes the input against a fixed 192-byte **secret** and is **length-classed**: short inputs take dedicated branches (1–3, 4–8, 9–16, 17–128, and 129–240 bytes) that fold the relevant bytes against slices of the secret through a 128-bit multiply, and only inputs longer than 240 bytes enter the eight-lane accumulator loop that consumes 64-byte stripes against the secret and periodically scrambles the lanes. This makes it both very fast on the short-to-mid keys typical of identifiers (its dedicated short branches avoid stripe-loop tail overhead) and extremely fast in bulk on long keys. The 64-bit result is reduced to 32 bits by xor-folding the high half into the low half (`h ^ (h >> 32)`), keeping the extra high-half entropy in the returned value. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits at the strong-distribution top of the `string` escalation ladder alongside `StringMurmur3Hasher`, `StringXxHash32Hasher`, `StringXxHash64Hasher`, `StringMetroHash64Hasher`, and `StringCityHash64Hasher`: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` (cheap, full Unicode width, 32-bit state) → `StringFnV1A64Hasher` (full Unicode width, 64-bit state) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche). `StringXxHash3Hasher`, `StringCityHash64Hasher`, `StringMetroHash64Hasher`, `StringXxHash64Hasher`, and `StringXxHash32Hasher` are peers at the throughput-oriented top of the ladder — profile on your own key shape to pick between them; XXH3 typically leads on both short keys (its dedicated length classes) and long keys (its eight-lane accumulator), while `StringMurmur3Hasher` stays the simplest choice for very short keys. All are good answers when FNV-1a's weaker avalanche pushes clustered or adversarial keys into long probe chains.

**Algorithm:** standard XXH3 64-bit with the default secret and seed `0`, dispatched on the byte length: the 1–3, 4–8, and 9–16-byte classes XOR the relevant words against secret slices and run the `XXH64` / `rrmxmx` / `XXH3` avalanche; 17–128 and 129–240 bytes accumulate a sum of `XXH3_mix16B` 128-bit folds (`mul128_fold64(lo ^ secret, hi ^ secret)`) over the input; and inputs over 240 bytes run the eight-lane accumulator (`acc[lane ^ 1] += data; acc[lane] += (data ^ secret).lo32 * (data ^ secret).hi32`) over 64-byte stripes, scrambling (`xorshift 47`, `^ secret`, `* PRIME32_1`) once per 1024-byte block, then merge the lanes through four `mul128_fold64` rounds and a final `XXH3` avalanche. The result is xor-folded to 32 bits.

**Note:** `Hash(s)` equals canonical XXH3 64-bit (default secret, seed `0`) over `Encoding.Unicode.GetBytes(s)`, xor-folded to 32 bits. The empty string maps to the well-known canonical empty-input vector `0x2D06800538D394C2` folded to 32 bits (its UTF-16 byte stream is zero bytes). The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel. When the keys come from an untrusted source, the keyed `StringHalfSipHash24Hasher` / `StringSipHash13Hasher` / `StringSipHash24Hasher` / `StringHighwayHash64Hasher` trade some of this throughput for hash-flooding resistance.

### StringSipHash13Hasher

```csharp
public struct StringSipHash13Hasher : IHashProvider<string>
```

The SipHash-1-3 keyed pseudorandom function (Jean-Philippe Aumasson & Daniel J. Bernstein), applied to the string's native little-endian UTF-16 byte stream, xor-folded down to a signed 32-bit result. It is the **reduced-round** sibling of `StringSipHash24Hasher`: the two-digit suffix is the round count, so SipHash-1-3 runs **one** `SipRound` of compression per 8-byte (four-char) message word and **three** `SipRound`s of finalization, versus two and four for SipHash-2-4. Halving the compression work makes it materially faster on every message word while keeping SipHash's keyed, hash-flooding-resistant avalanche — which is exactly why it is the construction Rust's standard-library `HashMap` uses by default. Like SipHash-2-4 it is an add-rotate-xor (ARX) construction over four 64-bit state words and carries no large table. The 64-bit result is reduced to 32 bits by xor-folding the high half into the low half (`h ^ (h >> 32)`), keeping the extra high-half entropy in the returned value. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits at the keyed top of the `string` escalation ladder, as a peer to `StringSipHash24Hasher` and `StringHighwayHash64Hasher`: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` (cheap, full Unicode width, 32-bit state) → `StringFnV1A64Hasher` (full Unicode width, 64-bit state) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput, unkeyed) → `StringHalfSipHash24Hasher` / `StringSipHash13Hasher` / `StringSipHash24Hasher` / `StringHighwayHash64Hasher` (strong avalanche, **keyed** hash-flooding resistance). Prefer one of the unkeyed throughput peers when the keys are trusted — they are faster. Reach for a keyed option when the keys come from an untrusted source (request paths, header names, user-supplied identifiers) and an adversary could otherwise deliberately drive worst-case collisions into the probe path. Between the SipHash variants, prefer `StringSipHash13Hasher` (Rust's choice) for the keyed defense at the lowest cost, and `StringSipHash24Hasher` for the extra cryptographic margin of the conservative round counts; `StringHighwayHash64Hasher` is the SIMD-oriented keyed alternative.

**Keyed, but with a fixed built-in key.** Celerity hashers are zero-state structs that collections construct via `new THasher()`, so this hasher carries a *fixed* key (the canonical SipHash reference key, bytes `00..0f`) rather than a per-process secret. That gives every collection SipHash's strong, well-distributed avalanche and defeats collision sets crafted against a weaker published algorithm, but an attacker who knows the table uses this exact fixed key could still precompute collisions against it. Callers who need full secret-keyed flooding resistance (a key the attacker cannot know) should seed a per-process key out of band; this type's value is a strong, standards-based mixing function with a clear upgrade path, not a turnkey secret.

**Algorithm:** standard SipHash-1-3 with key `k0 = 0x0706050403020100`, `k1 = 0x0F0E0D0C0B0A0908`. Initialize `v0..v3` from the constants `0x736F6D6570736575`, `0x646F72616E646F6D`, `0x6C7967656E657261`, `0x7465646279746573` XORed with the key halves. For each 8-byte little-endian message word `m`: `v3 ^= m`, **one** `SipRound`, `v0 ^= m`. The final block packs the leftover 0–6 tail bytes into the low bytes and the input length (mod 256) into the top byte, then runs the same `v3 ^= b` / one round / `v0 ^= b` step. Finalize with `v2 ^= 0xFF` then **three** `SipRound`s; the result is `v0 ^ v1 ^ v2 ^ v3`, xor-folded to 32 bits. `SipRound` is the fixed add / rotate / xor lattice with left-rotation constants 13, 32, 16, 21, 17, 32 — identical to SipHash-2-4; only the round counts differ.

**Note:** `Hash(s)` equals canonical SipHash-1-3 (with the fixed key above) over `Encoding.Unicode.GetBytes(s)`, xor-folded to 32 bits. The empty string maps to the length-`0` reference vector `0xABAC0158050FC4DC` folded to 32 bits (its UTF-16 byte stream is zero bytes). The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel. For the conservative-round-count sibling with more cryptographic margin, see `StringSipHash24Hasher`.

### StringSipHash24Hasher

```csharp
public struct StringSipHash24Hasher : IHashProvider<string>
```

The SipHash-2-4 keyed pseudorandom function (Jean-Philippe Aumasson & Daniel J. Bernstein), applied to the string's native little-endian UTF-16 byte stream, xor-folded down to a signed 32-bit result. Unlike the throughput-oriented `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` peers, SipHash is built to resist **hash flooding**: an adversary who controls the keys cannot construct a flood of inputs that collide into one bucket and degrade the table to O(n) probe chains without recovering the secret key, which SipHash is designed to make infeasible. This is why it is the default string hasher in Python, Ruby, and Rust's `HashMap`, whose tables are routinely fed untrusted input. It is an add-rotate-xor (ARX) construction over four 64-bit state words: two `SipRound`s of compression per 8-byte (four-char) message word, then four `SipRound`s of finalization — so it carries no large table and stays branch-light, but it is slower than the multiply-and-rotate throughput family (and than its own reduced-round sibling `StringSipHash13Hasher`, which runs one compression and three finalization rounds for less cost). The 64-bit result is reduced to 32 bits by xor-folding the high half into the low half (`h ^ (h >> 32)`), keeping the extra high-half entropy in the returned value. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits one rung beyond the throughput-oriented peers at the top of the `string` escalation ladder, as a keyed peer to `StringSipHash13Hasher` and `StringHighwayHash64Hasher`: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` (cheap, full Unicode width, 32-bit state) → `StringFnV1A64Hasher` (full Unicode width, 64-bit state) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput) → `StringHalfSipHash24Hasher` / `StringSipHash13Hasher` / `StringSipHash24Hasher` / `StringHighwayHash64Hasher` (strong avalanche, **keyed** hash-flooding resistance). Prefer one of the xxHash / MetroHash / CityHash peers when the keys are trusted — they are faster. Reach for `StringSipHash24Hasher` when the keys come from an untrusted source (request paths, header names, user-supplied identifiers) and an adversary could otherwise deliberately drive worst-case collisions into the probe path; `StringSipHash13Hasher` is the faster reduced-round SipHash variant (Rust's `HashMap` default) and `StringHighwayHash64Hasher` is the SIMD-oriented keyed alternative.

**Keyed, but with a fixed built-in key.** Celerity hashers are zero-state structs that collections construct via `new THasher()`, so this hasher carries a *fixed* key (the canonical SipHash reference key, bytes `00..0f`) rather than a per-process secret. That gives every collection SipHash's strong, well-distributed avalanche and defeats collision sets crafted against a weaker published algorithm, but an attacker who knows the table uses this exact fixed key could still precompute collisions against it. Callers who need full secret-keyed flooding resistance (a key the attacker cannot know) should seed a per-process key out of band; this type's value is a strong, standards-based mixing function with a clear upgrade path, not a turnkey secret.

**Algorithm:** standard SipHash-2-4 with key `k0 = 0x0706050403020100`, `k1 = 0x0F0E0D0C0B0A0908`. Initialize `v0..v3` from the constants `0x736F6D6570736575`, `0x646F72616E646F6D`, `0x6C7967656E657261`, `0x7465646279746573` XORed with the key halves. For each 8-byte little-endian message word `m`: `v3 ^= m`, two `SipRound`s, `v0 ^= m`. The final block packs the leftover 0–6 tail bytes into the low bytes and the input length (mod 256) into the top byte, then runs the same `v3 ^= b` / two rounds / `v0 ^= b` step. Finalize with `v2 ^= 0xFF` then four `SipRound`s; the result is `v0 ^ v1 ^ v2 ^ v3`, xor-folded to 32 bits. `SipRound` is the fixed add / rotate / xor lattice with left-rotation constants 13, 32, 16, 21, 17, 32.

**Note:** `Hash(s)` equals canonical SipHash-2-4 (with the fixed key above) over `Encoding.Unicode.GetBytes(s)`, xor-folded to 32 bits. The empty string maps to the published length-`0` vector `0x726FDB47DD0E0E31` folded to 32 bits (its UTF-16 byte stream is zero bytes). The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel. For the faster reduced-round variant, see `StringSipHash13Hasher`; for the cheaper 32-bit-word variant tuned for short keys, see `StringHalfSipHash24Hasher`; for a keyed option whose wider state is designed to vectorize, see `StringHighwayHash64Hasher`.

### StringHalfSipHash24Hasher

```csharp
public struct StringHalfSipHash24Hasher : IHashProvider<string>
```

The HalfSipHash-2-4 keyed pseudorandom function (Jean-Philippe Aumasson & Daniel J. Bernstein), applied to the string's native little-endian UTF-16 byte stream, producing a **native 32-bit** result. HalfSipHash is the 32-bit-word sibling of `StringSipHash24Hasher`: the same keyed add-rotate-xor (ARX) construction and the same **hash-flooding** defence — an adversary who controls the keys cannot construct a flood of inputs that collide into one bucket and degrade the table to O(n) probe chains without recovering the secret key — but built on four **32-bit** state words (`v0..v3`) advanced over 4-byte (two-char) message words instead of SipHash's 64-bit words over 8-byte words. That narrower state makes each round cheaper and the per-call fixed overhead lower, so on the short keys typical of identifiers — and on 32-bit-leaning targets — it is the faster keyed option, at the cost of the wider cryptographic margin the full 64-bit SipHash carries on long inputs. The `2-4` suffix is the round count: two `SipRound`s of compression per 4-byte (two-char) message word and four `SipRound`s of finalization, matching `StringSipHash24Hasher`. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

Unlike every other `string` hasher here, HalfSipHash's primitive output is *already* 32 bits wide (`v1 ^ v3` after finalization), so this hasher returns it directly — there is no 64-bit-to-32-bit xor-fold to lose entropy through. It sits on the keyed tier of the `string` escalation ladder alongside its 64-bit relatives, as the cheaper keyed choice for shorter keys: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` (cheap, full Unicode width, 32-bit state) → `StringFnV1A64Hasher` (full Unicode width, 64-bit state) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput, unkeyed) → `StringHalfSipHash24Hasher` / `StringSipHash13Hasher` / `StringSipHash24Hasher` / `StringHighwayHash64Hasher` (strong avalanche, **keyed** hash-flooding resistance). Prefer one of the unkeyed throughput peers when the keys are trusted — they are faster. Reach for a keyed option when the keys come from an untrusted source (request paths, header names, user-supplied identifiers) and an adversary could otherwise deliberately drive worst-case collisions into the probe path. Among the keyed options, prefer `StringHalfSipHash24Hasher` for short keys / 32-bit targets, `StringSipHash13Hasher` (Rust's `HashMap` default) for the reduced-round 64-bit defence, `StringSipHash24Hasher` for the conservative round counts, and `StringHighwayHash64Hasher` for the SIMD-oriented wide-state alternative.

**Keyed, but with a fixed built-in key.** As with `StringSipHash24Hasher`, the hasher carries a *fixed* built-in key (the canonical HalfSipHash reference key, bytes `00..07`, read as two little-endian 32-bit halves `k0 = 0x03020100`, `k1 = 0x07060504`) rather than a per-process secret, because collections construct it via `new THasher()`. That defeats collision sets crafted against a weaker published algorithm, but an attacker who knows the table uses this exact fixed key could still precompute collisions; callers who need full secret-keyed flooding resistance should seed a per-process key out of band.

**Algorithm:** standard HalfSipHash-2-4 (4-byte output) with the key above. Initialize `v0 = k0`, `v1 = k1`, `v2 = 0x6C796765 ^ k0`, `v3 = 0x74656462 ^ k1` (the `v2`/`v3` constants are the high 32 bits of SipHash's own initialization words; `v0`/`v1` start at zero). For each 4-byte little-endian message word `m`: `v3 ^= m`, two `SipRound`s, `v0 ^= m`. The final block packs the leftover 0–3 tail bytes into the low bytes and the input length (mod 256) into the top byte (for the always-even UTF-16 stream the tail is empty or one char), then runs the same `v3 ^= b` / two rounds / `v0 ^= b` step. Finalize with `v2 ^= 0xFF` then four `SipRound`s; the result is `v1 ^ v3`, returned directly as a signed 32-bit integer (no fold). `SipRound` here is the 32-bit add / rotate / xor lattice with left-rotation constants 5, 16, 8, 7, 13, 16.

**Note:** `Hash(s)` equals canonical HalfSipHash-2-4 (4-byte output, with the fixed key above) over `Encoding.Unicode.GetBytes(s)`, reinterpreted as a signed 32-bit integer. The empty string maps to HalfSipHash-2-4's published length-`0` vector `0x5B9F35A9` (its UTF-16 byte stream is zero bytes); because the output is natively 32 bits there is no fold. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel. For the full 64-bit-state SipHash variants with more cryptographic margin, see `StringSipHash13Hasher` / `StringSipHash24Hasher`.

### StringHighwayHash64Hasher

```csharp
public struct StringHighwayHash64Hasher : IHashProvider<string>
```

Google's HighwayHash (64-bit output), applied to the string's native little-endian UTF-16 byte stream, xor-folded down to a signed 32-bit result. HighwayHash is a **keyed** pseudorandom hash that Google designed as a faster successor to SipHash for the same job — resisting **hash flooding**, where an adversary who controls the keys floods a table with values that collide into one bucket and degrade it to O(n) probe chains. Like `StringSipHash24Hasher` it is keyed, so an attacker cannot construct such a collision set without recovering the secret key. Where SipHash is a compact ARX lattice, HighwayHash keeps a wide four-lane state (`v0`, `v1`, `mul0`, `mul1`, four 64-bit words each) that it advances 32 bytes (sixteen chars) at a time through per-lane 32×32→64-bit multiplies, a byte-shuffling *zipper merge*, and four permute-and-update finalization rounds. That structure is built to map onto SIMD lanes (AVX2 / NEON), which is where HighwayHash earns its throughput edge over SipHash. The 64-bit result is reduced to 32 bits by xor-folding the high half into the low half (`h ^ (h >> 32)`). Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits at the keyed top of the `string` escalation ladder, as a peer to `StringSipHash13Hasher` and `StringSipHash24Hasher`: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` (cheap, full Unicode width, 32-bit state) → `StringFnV1A64Hasher` (full Unicode width, 64-bit state) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput, unkeyed) → `StringHalfSipHash24Hasher` / `StringSipHash13Hasher` / `StringSipHash24Hasher` / `StringHighwayHash64Hasher` (strong avalanche, **keyed** hash-flooding resistance). Prefer the unkeyed throughput family when the keys are trusted — they are faster. Among the keyed options, `StringSipHash13Hasher` is the faster reduced-round SipHash (Rust's `HashMap` default) and `StringSipHash24Hasher` is the conservative, well-established default; `StringHighwayHash64Hasher` is the SIMD-oriented alternative whose wider state pays off most once vectorized.

**Portable scalar implementation.** This type produces the exact HighwayHash64 output (verified against the published reference test vectors) but the current implementation is **scalar**, not SIMD — explicit AVX2 / NEON acceleration is a potential future optimization. On a scalar code path do not assume it out-runs the throughput-oriented unkeyed family above; its present value is the strong, standards-based keyed distribution, with vectorization as the upgrade path.

**Keyed, but with a fixed built-in key.** As with `StringSipHash24Hasher`, the hasher carries a *fixed* 256-bit key (the canonical HighwayHash reference key, bytes `00..1f` read as four little-endian 64-bit words) rather than a per-process secret, because collections construct it via `new THasher()`. That defeats collision sets crafted against a weaker published algorithm, but an attacker who knows the table uses this exact fixed key could still precompute collisions; callers who need full secret-keyed flooding resistance should seed a per-process key out of band.

**Algorithm:** standard HighwayHash64. Initialize the four lanes from the fixed key XORed with the `mul0` / `mul1` constants (`v0[i] = mul0[i] ^ key[i]`, `v1[i] = mul1[i] ^ rot32(key[i])`). For each 32-byte packet read four little-endian 64-bit lanes and run the per-lane mix (`v1 += mul0 + lane`, `mul0 ^= (v1 & 0xffffffff) * (v0 >> 32)`, `v0 += mul1`, `mul1 ^= (v0 & 0xffffffff) * (v1 >> 32)`) followed by four `ZipperMergeAndAdd` byte-shuffle steps. A trailing partial packet (1–30 bytes here, since the UTF-16 stream is even) mixes its length into `v0`, rotates `v1` by that length, and packs the leftover bytes into a zero-filled 32-byte block under the reference's alignment rules. Finalize with four permute-and-update rounds (reorder the `v0` lanes and swap their 32-bit halves, then re-run the update) and return `v0[0] + v1[0] + mul0[0] + mul1[0]`, xor-folded to 32 bits.

**Note:** `Hash(s)` equals canonical HighwayHash64 (with the fixed key above) over `Encoding.Unicode.GetBytes(s)`, xor-folded to 32 bits. The empty string maps to the published length-`0` reference vector `0x907A56DE22C26E53` folded to 32 bits (its UTF-16 byte stream is zero bytes). The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### Int32Murmur3Hasher

```csharp
public struct Int32Murmur3Hasher : IHashProvider<int>
```

The MurmurHash3 32-bit finalizer (`fmix32`) applied to `int` keys. Better avalanche than `Int32WangNaiveHasher` — prefer when key distribution is clustered or adversarial.

**Note:** maps `0 → 0` (a fixed point of `fmix32`), so the dictionaries' out-of-band zero-key slot is engaged just as it is with the simpler hashers.

### Int32WangHasher

```csharp
public struct Int32WangHasher : IHashProvider<int>
```

Thomas Wang's 32-bit integer hash (`hash32shift`) for `int` keys. The full-mixer middle tier of the `int` family: it sits between `Int32WangNaiveHasher` (the cheap XOR-fold default) and `Int32Murmur3Hasher` on the cost-vs-avalanche curve, mirroring the role `Int64WangHasher` plays for `long` keys. It uses a single (shift-add-encoded) multiply plus a chain of XOR-shift / shift-add rounds, so it is cheaper than the two-multiply `Int32Murmur3Hasher` finalizer while still giving every input bit influence over the result. Bijective on `uint`, so the only source of collisions is truncation-free key structure, not the mixer. Prefer it over the default `Int32WangNaiveHasher` when the cheap XOR-fold produces measurable clustering; escalate to `Int32Murmur3Hasher` when even better avalanche is needed.

**Algorithm:** `~key + (key << 15)`, `^ (key >> 12)`, `+ (key << 2)`, `^ (key >> 4)`, `* 2057` (encoded as `+ (key << 3) + (key << 11)`), `^ (key >> 16)`, computed on the `uint` reinterpretation and truncated to `int`.

**Note:** unlike the Murmur3 finalizer, this function does **not** map `0 → 0`. The dictionaries store the out-of-band zero-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

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

### UInt32WangHasher

```csharp
public struct UInt32WangHasher : IHashProvider<uint>
```

Thomas Wang's 32-bit integer hash (`hash32shift`) for `uint` keys. The full-mixer middle tier of the `uint` family: it sits between `UInt32Hasher` (the cheap XOR-fold) and `UInt32Murmur3Hasher` on the cost-vs-avalanche curve, the `uint` counterpart to `Int32WangHasher` and mirroring the role `Int64WangHasher` plays for `long` keys. It uses a single (shift-add-encoded) multiply plus a chain of XOR-shift / shift-add rounds, so it is cheaper than the two-multiply `UInt32Murmur3Hasher` finalizer while still giving every input bit influence over the result. Bijective on `uint`, so the only source of collisions is key structure, not the mixer. Prefer it over `UInt32Hasher` when the cheap XOR-fold produces measurable clustering; escalate to `UInt32Murmur3Hasher` when even better avalanche is needed.

**Algorithm:** `~key + (key << 15)`, `^ (key >> 12)`, `+ (key << 2)`, `^ (key >> 4)`, `* 2057` (encoded as `+ (key << 3) + (key << 11)`), `^ (key >> 16)`, computed on the `uint` directly and truncated to `int`. For any given 32-bit pattern it returns exactly what `Int32WangHasher` returns for the same bits.

**Note:** unlike the Murmur3 finalizer, this function does **not** map `0 → 0`. The dictionaries store the out-of-band zero-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### UInt32Murmur3Hasher

```csharp
public struct UInt32Murmur3Hasher : IHashProvider<uint>
```

MurmurHash3 32-bit finalizer (`fmix32`) for `uint` keys. The `uint` counterpart to `Int32Murmur3Hasher`, and the strong-avalanche escalation option for `UInt32Hasher` (the cheap XOR-fold default) — every input bit affects every output bit. Prefer it over `UInt32Hasher` when the XOR-fold produces measurable clustering or collision resistance matters more than raw throughput.

**Algorithm:** `h ^= h >> 16`, `h *= 0x85ebca6b`, `h ^= h >> 13`, `h *= 0xc2b2ae35`, `h ^= h >> 16`, computed on the `uint` directly and reinterpreted as `int`.

**Note:** maps `0 → 0` (a fixed point of `fmix32`), so the dictionaries' out-of-band zero-key slot is engaged just as it is with the simpler `UInt32Hasher`.

### UInt64Hasher

```csharp
public struct UInt64Hasher : IHashProvider<ulong>
```

MurmurHash3 64-bit finalizer (`fmix64`) for `ulong` keys. Counterpart to `Int64Murmur3Hasher` for unsigned 64-bit integers.

### UInt64WangHasher

```csharp
public struct UInt64WangHasher : IHashProvider<ulong>
```

Thomas Wang's 64-bit integer hash (`hash64shift`) for `ulong` keys. The `ulong` counterpart to `Int64WangHasher`, and a cheaper alternative to `UInt64Hasher` (the Murmur3 `fmix64` finalizer) on the cost-vs-avalanche curve. The mixer uses only shifts and adds — no multiplies — so it is cheaper than the two 64-bit multiplies of `UInt64Hasher` while still giving every input bit influence over the result. Bijective on `ulong`, so the only source of collisions is truncation to 32 bits when the result is returned. Prefer it over `UInt64Hasher` when profiling shows the two `fmix64` multiplies are a hot-path cost and the keys are already reasonably uniform; escalate back to `UInt64Hasher` for adversarial workloads that need maximum avalanche.

**Algorithm:** `u = ~u + (u << 21)`, `^ (u >> 24)`, `+ (u << 3) + (u << 8)`, `^ (u >> 14)`, `+ (u << 2) + (u << 4)`, `^ (u >> 28)`, `+ (u << 31)`, computed on the `ulong` directly and truncated to `int`. For any given 64-bit pattern it returns exactly what `Int64WangHasher` returns for the same bits.

**Note:** unlike the Murmur3 finalizer, this function does **not** map `0 → 0`. The dictionaries store the out-of-band zero-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### UInt64WangNaiveHasher

```csharp
public struct UInt64WangNaiveHasher : IHashProvider<ulong>
```

An extremely cheap XOR-fold for `ulong` keys — the cheap-default tier of the `ulong` family, and the `ulong` counterpart to `Int64WangNaiveHasher`. Until now the `ulong` ladder jumped straight from `UInt64WangHasher` (full Thomas-Wang `hash64shift`) to `UInt64Hasher` (Murmur3 `fmix64`) with no cheap XOR-fold option, even though `int`, `long`, and `uint` all ship one. The extra `(int)(key >> 16)` fold over a naive `key.GetHashCode()` keeps a chunk of the high-half entropy in the result, which materially improves distribution on keys whose low 32 bits are sequential (e.g. monotonically allocated IDs whose upper bits carry type / shard). Prefer it when the key distribution is already reasonably uniform and latency matters more than collision resistance; escalate to `UInt64WangHasher` (full Thomas-Wang finalizer) or `UInt64Hasher` (Murmur3 `fmix64`) for clustered or adversarial keys.

**Algorithm:** `(int)key ^ (int)(key >> 32) ^ (int)(key >> 16)`. For any given 64-bit pattern it returns exactly what `Int64WangNaiveHasher` returns for the same bits.

**Note:** the XOR-fold maps `0 → 0`. The dictionaries store the out-of-band zero-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

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
| `int` | `Int32WangNaiveHasher` (used by `IntDictionary` / `IntSet`) | `Int32WangHasher` (full Thomas-Wang finalizer) or `Int32Murmur3Hasher` for clustered or adversarial keys |
| `long` | `Int64WangNaiveHasher` (used by `LongDictionary` / `LongSet`) | `Int64WangHasher` (full Thomas-Wang finalizer) or `Int64Murmur3Hasher` for clustered or adversarial keys |
| `uint` | `UInt32Hasher` | `UInt32WangHasher` (full Thomas-Wang finalizer) or `UInt32Murmur3Hasher` (Murmur3 `fmix32`) for clustered or adversarial keys |
| `ulong` | `UInt64Hasher` (Murmur3 `fmix64`) | `UInt64WangHasher` (full Thomas-Wang finalizer) when the two `fmix64` multiplies are a hot-path cost and keys are already reasonably uniform; `UInt64WangNaiveHasher` (XOR-fold) for the cheapest option on already-uniform keys |
| `Guid` | `GuidHasher` | `DefaultHasher<Guid>` (slower but BCL-equivalent) |
| `string` | `StringFnV1AHasher` | `StringDjb2Hasher` (Bernstein's djb2 — the simplest, cheapest classic, shift-and-add with no real multiply, full-character fold) when you want a familiar minimal hash on short ASCII identifiers and djb2's weaker avalanche is acceptable; `StringFnV1AFullHasher` (same FNV-1a cost, folds the full character) for non-ASCII content the low-byte fold would collide; `StringFnV1A64Hasher` (full-character fold into a 64-bit accumulator) when keys are long or numerous enough to cluster the 32-bit state; `StringJenkinsOaatHasher` (Bob Jenkins' one-at-a-time hash — multiply-free, with stronger per-bit avalanche than FNV-1a at the same cheap cost class) when FNV-1a's single-multiply mixing clusters keys but a block hash is more than you want to pay; `StringMurmur3Hasher`, `StringXxHash32Hasher`, `StringXxHash64Hasher`, `StringMetroHash64Hasher`, `StringCityHash64Hasher`, or `StringXxHash3Hasher` (the throughput-oriented strong-avalanche options for longer keys — XXH64 widens the accumulators and stripe further for longer keys on 64-bit platforms, MetroHash64 is a peer worth profiling against on mid-length keys, CityHash64 is length-classed so it often edges ahead on short-to-mid keys, and XXH3 is the third-generation xxHash that is length-classed *and* runs an eight-lane bulk loop, typically the fastest across both short and long keys) for clustered / adversarial keys that need strong avalanche; `StringHalfSipHash24Hasher` (HalfSipHash-2-4, keyed — the cheaper 32-bit-word variant for short keys / 32-bit targets, with a native 32-bit output and no fold), `StringSipHash13Hasher` (SipHash-1-3, keyed — the faster reduced-round 64-bit variant Rust's `HashMap` uses by default), `StringSipHash24Hasher` (SipHash-2-4, keyed — the conservative variant), or `StringHighwayHash64Hasher` (HighwayHash64, keyed — the SIMD-oriented alternative, scalar today) when the keys are untrusted and you need hash-flooding resistance rather than maximum throughput; `DefaultHasher<string>` (uses the BCL string hasher) |
| anything else | `DefaultHasher<T>` | a struct hasher you write |

---

## Hash quality evaluation

### HashQualityEvaluator

```csharp
public static class HashQualityEvaluator
{
    public static HashQualityReport Evaluate<T, THasher>(IEnumerable<T> keys, int bucketCount = 1024)
        where THasher : struct, IHashProvider<T>;
}
```

A diagnostic tool for comparing how well candidate hashers distribute a representative sample of keys *before* you commit one to a collection. It hashes every key with `THasher` (instantiated via `default` — the built-in hashers are stateless structs) and reports both the hasher's intrinsic collision behaviour and how the codes spread across power-of-two buckets.

This is **not** a hot-path API: it allocates working buffers and walks an `IEnumerable<T>`, so call it offline — in a test, a benchmark, or a one-off analysis — not on a request path. Codes are masked into buckets with `code & (bucketCount - 1)`, exactly as the Celerity collections index their backing arrays, so the bucket metrics reflect the clustering a real table of that size would experience.

`bucketCount` is rounded up to the next power of two (matching the collections' array sizing). `Evaluate` throws `ArgumentNullException` for a null `keys` sequence and `ArgumentOutOfRangeException` when `bucketCount < 1`.

> Pass **distinct** keys to measure a hasher's intrinsic quality. Duplicate keys in the sample naturally hash to the same code and are counted as collisions.

### HashQualityReport

```csharp
public readonly struct HashQualityReport
```

An immutable bag of metrics returned by `Evaluate`. Its `ToString()` renders a one-line summary.

| Member | Meaning |
|---|---|
| `KeyCount` | Number of keys hashed. |
| `DistinctHashCount` | Number of distinct 32-bit codes produced. |
| `CollisionCount` | `KeyCount - DistinctHashCount` — raw-code collisions, independent of `BucketCount`. Measures the hasher's intrinsic injectivity. |
| `CollisionRate` | `CollisionCount / KeyCount`, in `[0, 1)`. |
| `BucketCount` | The power-of-two bucket count actually used. |
| `OccupiedBucketCount` / `EmptyBucketCount` | Buckets that received at least one / no keys. |
| `MaxBucketLoad` | Largest number of keys in any single bucket — the worst-case probe-chain seed. |
| `MeanBucketLoad` | `KeyCount / BucketCount`. |
| `ChiSquared` | Pearson's chi-squared of the bucket loads vs a uniform expectation. Lower is closer to uniform; ≈ `BucketCount - 1` for a good hash on a uniform sample. |
| `DistributionScore` | Observed sum of squared bucket loads ÷ the value expected from an ideal uniform hash. **`1.0` = ideal uniform**; above `1.0` = clustering (worse, longer probe chains); below `1.0` = more even than random (e.g. near-perfect hashing). |

### Example

```csharp
using Celerity.Hashing;

// A representative sample of the keys your collection will actually hold.
long[] keys = LoadProductionKeySample();

HashQualityReport naive = HashQualityEvaluator.Evaluate<long, Int64WangNaiveHasher>(keys, bucketCount: 4096);
HashQualityReport wang  = HashQualityEvaluator.Evaluate<long, Int64WangHasher>(keys, bucketCount: 4096);

Console.WriteLine($"naive: {naive}");
Console.WriteLine($"wang:  {wang}");

// Pick the hasher whose DistributionScore is closest to 1.0 (and whose MaxBucketLoad is lowest)
// for your key shape. If the cheap default already scores near 1.0, there's no reason to pay
// for a stronger mixer.
if (naive.DistributionScore <= wang.DistributionScore * 1.1)
{
    // The naive fold distributes these keys well enough — keep the cheaper default.
}
```
