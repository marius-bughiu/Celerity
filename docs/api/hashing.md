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

### Int32IdentityHasher

```csharp
public struct Int32IdentityHasher : IHashProvider<int>
```

The **zero-work floor** of the `int` family: a pass-through that returns the key unchanged with no mixing at all. Because `int.GetHashCode()` is itself the identity function, this hasher reproduces the framework's own `int` hash exactly — and since it does *no* work, **no mixing hasher can beat it on raw throughput.** It exists as an explicit opt-out from mixing (the F14 / ahash / FxHash position): when keys are already uniform and trusted, any avalanche step is pure overhead.

**Algorithm:** `key` (identity)

**Decision rule:** uniform / trusted keys (dense sequential IDs, contiguous indices) → *skip* mixing with this hasher; clustered or adversarial keys → *mix* with `Int32WangHasher` (full Thomas-Wang finalizer) or `Int32Murmur3Hasher`. Celerity's open-addressed, power-of-two-masked tables are **more** sensitive to a weak integer hash than the prime-bucketed BCL `Dictionary<,>` — because the table masks off the low bits, keys whose low bits collide (e.g. all multiples of the capacity) cluster into long probe chains. Identity is the right call only when the *low bits* of the keys are themselves well distributed (which they are for `0, 1, 2, …`).

**Not a HashDoS defence:** a fixed identity (or any fixed-seed) hasher lets an attacker who can choose keys force collisions. For untrusted integer keys, prefer a strong mixer.

### Int64IdentityHasher

```csharp
public struct Int64IdentityHasher : IHashProvider<long>
```

The **zero-work floor** of the `long` family and the 64-bit counterpart to `Int32IdentityHasher`: a pass-through that returns the key's low 32 bits with no mixing, strictly cheaper than even the XOR-fold `Int64WangNaiveHasher`.

**Algorithm:** `(int)key` — keeps only the low 32 bits.

Because it keeps only the low half, two keys that differ **only** in their upper 32 bits collide — unlike `Int64WangNaiveHasher`, which folds the high half back in. That makes it the right call only when the discriminating entropy lives in the low 32 bits (dense sequential `long` IDs), and the wrong call when the upper bits carry the distinguishing information (type / shard tags, a timestamp in the high word). For those, escalate to `Int64WangNaiveHasher` (cheap XOR-fold that keeps high-half entropy), `Int64WangHasher` (full Thomas-Wang finalizer), or `Int64Murmur3Hasher`. The same open-addressed-table sensitivity and the same "not a HashDoS defence" caveat as `Int32IdentityHasher` apply.

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

Its tradeoff is weaker avalanche: djb2 has no shift/xor diffusion step, so a single changed input byte propagates less thoroughly than under `StringJenkinsOaatHasher`. It sits at the cheapest, classic end of the `string` escalation ladder, a peer to `StringDjb2AHasher`, `StringSdbmHasher`, `StringElfHasher`, the FNV-1a variants, and `StringJenkinsOaatHasher`: `StringDjb2Hasher` / `StringDjb2AHasher` / `StringSdbmHasher` / `StringElfHasher` (cheapest, multiply-free classics, weaker avalanche) / `StringFnV1AHasher` (low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (cheap FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche than djb2 or FNV-1a) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Prefer it when you want the simplest, most familiar cheap hash and your keys are short ASCII identifiers, or `StringDjb2AHasher` for the same cost with the XOR fold's slightly cleaner low-bit diffusion; step up to `StringJenkinsOaatHasher` (still cheap, but with proper shift/xor diffusion) or the FNV-1a variants when djb2's weak avalanche starts clustering keys, and escalate to the throughput-oriented strong family for clustered or adversarial keys.

**Note:** maps the empty string `""` → the seed constant `5381` — no characters are folded — exactly as FNV-1a maps the empty string to its offset basis. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringDjb2AHasher

```csharp
public struct StringDjb2AHasher : IHashProvider<string>
```

The **djb2a** variant of Bernstein's djb2 hash — the XOR-folding sibling — applied to the string's native little-endian UTF-16 byte stream, folding both bytes of every character (low byte then high byte). It seeds the accumulator with the same magic constant `5381` as `StringDjb2Hasher` and folds each byte with `hash = hash * 33 ^ b`. The **only** difference from `StringDjb2Hasher` is the final combine: djb2 *adds* the byte (`* 33 + b`), djb2a *XORs* it (`* 33 ^ b`). This is exactly the relationship FNV-1 and FNV-1a have to each other, which is why this type mirrors the `StringFnV1Hasher` / `StringFnV1AHasher` naming. As in djb2 the multiply by `33` lowers to a shift-and-add (`(hash << 5) + hash`), so it uses **no real multiply, no table, and no finalizer**. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

Replacing the trailing add with an XOR avoids the low-bit carry bias that addition introduces, so djb2a tends to diffuse the freshly-folded byte a touch more evenly than djb2 — but it is still a single combine per byte with no shift/xor avalanche step, so a single changed input byte propagates less thoroughly than under `StringJenkinsOaatHasher`. It sits at the cheapest, classic end of the `string` escalation ladder, a peer to `StringDjb2Hasher`, `StringSdbmHasher`, `StringElfHasher`, the FNV-1a variants, and `StringJenkinsOaatHasher`: `StringDjb2Hasher` / `StringDjb2AHasher` / `StringSdbmHasher` / `StringElfHasher` (cheapest, multiply-free classics, weaker avalanche) / `StringFnV1AHasher` (low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (cheap FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche than djb2/djb2a or FNV-1a) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Prefer it over `StringDjb2Hasher` when you want the same minimal, familiar djb2 cost but slightly cleaner low-bit diffusion from the XOR fold; step up to `StringJenkinsOaatHasher` (still cheap, but with proper shift/xor diffusion) or the FNV-1a variants when djb2a's weak avalanche starts clustering keys, and escalate to the throughput-oriented strong family for clustered or adversarial keys.

**Note:** maps the empty string `""` → the seed constant `5381` — no characters are folded — exactly as `StringDjb2Hasher` maps the empty string to its seed. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringSdbmHasher

```csharp
public struct StringSdbmHasher : IHashProvider<string>
```

The classic **sdbm** hash — from Ozan Yigit's public-domain re-implementation of the `sdbm` database library, also used in gawk and Berkeley DB — applied to the string's native little-endian UTF-16 byte stream, folding both bytes of every character (low byte then high byte). The accumulator is seeded with `0`, and each byte is folded with the single step `hash = b + (hash << 6) + (hash << 16) - hash`. That lattice is exactly `hash = hash * 65599 + b` (because `64 + 65536 - 1 = 65599`), so the multiply by the prime `65599` lowers to two shifts and a subtract — **no real multiply, no table, and no finalizer**, the same cheapest cost class as `StringDjb2Hasher`. Where djb2 mixes with one shift and two adds (`* 33`), sdbm mixes with two shifts, an add, and a subtract (`* 65599`); the larger multiplier spreads each folded byte across more of the accumulator, which tends to give sdbm slightly better distribution on short keys, though both lack a true avalanche step. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

Its tradeoff is the same weak avalanche as djb2: sdbm has no shift/xor diffusion or finalizer, so a single changed input byte propagates less thoroughly than under `StringJenkinsOaatHasher`. It sits at the cheapest, classic end of the `string` escalation ladder, a peer to `StringDjb2Hasher`, `StringDjb2AHasher`, `StringElfHasher`, the FNV-1a variants, and `StringJenkinsOaatHasher`: `StringDjb2Hasher` / `StringDjb2AHasher` / `StringSdbmHasher` / `StringElfHasher` (cheapest, multiply-free classics, weaker avalanche) / `StringFnV1AHasher` (low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (cheap FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche than the classics or FNV-1a) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Prefer it when you want a simple, familiar cheap hash and your keys are short ASCII identifiers; step up to `StringJenkinsOaatHasher` (still cheap, but with proper shift/xor diffusion) or the FNV-1a variants when sdbm's weak avalanche starts clustering keys, and escalate to the throughput-oriented strong family for clustered or adversarial keys.

**Note:** maps the empty string `""` → the seed constant `0` — no characters are folded. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringElfHasher

```csharp
public struct StringElfHasher : IHashProvider<string>
```

The classic **PJW** / **ELF** hash — Peter J. Weinberger's hash from the "Dragon Book" in the exact 32-bit form standardized as the `elf_hash` used by the System V ABI for ELF object-file symbol tables — applied to the string's native little-endian UTF-16 byte stream, folding both bytes of every character (low byte then high byte). The accumulator is seeded with `0`, and each byte `b` is folded with the step `hash = (hash << 4) + b; high = hash & 0xF0000000; if (high != 0) hash ^= high >> 24; hash &= ~high`. The shift-by-4 walks new bytes up through the accumulator; whenever data reaches the top nibble, those four bits are folded back down into bits 4–7 (`high >> 24`) and then cleared, so the state never grows past 28 bits and high-order entropy is recirculated instead of discarded. Like djb2 and sdbm it uses **no real multiply, no table, and no separate finalizer**. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

Its high-nibble feedback gives it a touch more diffusion than the pure shift-and-add classics — a changed byte that has climbed into the top nibble is XORed back across the low byte — but it still has no full avalanche step, so a single changed input byte propagates less thoroughly than under `StringJenkinsOaatHasher`. Because the top nibble is always cleared, the result occupies only 28 bits (always in `[0, 0x0FFFFFFF]`, never negative); this is immaterial once the dictionary masks the hash down to the table size. It sits at the cheapest, classic end of the `string` escalation ladder, a peer to `StringDjb2Hasher`, `StringDjb2AHasher`, `StringSdbmHasher`, the FNV-1a variants, and `StringJenkinsOaatHasher`: `StringDjb2Hasher` / `StringDjb2AHasher` / `StringSdbmHasher` / `StringElfHasher` (cheapest, multiply-free classics, weaker avalanche) / `StringFnV1AHasher` (low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (cheap FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche than the classics or FNV-1a) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Reach for it when you specifically want the familiar ELF/PJW hash; otherwise prefer one of the other cheap classics. **The measured [distribution report](#measured-distribution-quality) flags a real weakness here:** on short ASCII keys the ELF hash clusters badly (`DistributionScore` ≈ 11.4, max bucket load 31 versus ~4–5 for every other hasher), because its low output bits mix poorly for ASCII-range inputs and the dictionary masks the hash down to those bits — so for ASCII-dominated keys prefer the equally cheap `StringDjb2Hasher` / `StringDjb2AHasher` / `StringSdbmHasher`, or step up to `StringJenkinsOaatHasher` (still cheap, but with proper shift/xor diffusion) or the FNV-1a variants. The clustering eases on keys with significant non-ASCII content (score ≈ 2.27), but it is still the weakest distributor in the family there; escalate to the throughput-oriented strong family for clustered or adversarial keys.

**Note:** maps the empty string `""` → the seed constant `0` — no characters are folded. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringCrc32Hasher

```csharp
public struct StringCrc32Hasher : IHashProvider<string>
```

The standard **CRC-32** (ISO-HDLC / IEEE 802.3 — the same variant used by zlib, gzip, PNG, and Ethernet) applied to the string's native little-endian UTF-16 byte stream, folding both bytes of every character (low byte then high byte). CRC-32 is a cyclic redundancy check, not a designed hash: it is the remainder of the message — interpreted as a polynomial over GF(2) — divided by the reflected generator polynomial `0xEDB88320`, with the accumulator pre-set to `0xFFFFFFFF` and the final value bit-inverted (XOR `0xFFFFFFFF`). It is implemented with the classic 256-entry byte lookup table (one table lookup, one XOR, and one shift per byte), making it the **first table-driven** member of the `string` family — `StringDjb2Hasher`, `StringSdbmHasher`, and `StringElfHasher` are all table-free. The table is a single `static readonly` array built once at type initialization, so the per-call path stays allocation-free. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

Because CRC-32 is a **linear** function over GF(2), it has weaker avalanche than the designed mixers — flipping one input bit flips a fixed, input-independent set of output bits, and the low bits in particular mix poorly — so for adversarial or heavily clustered keys it clusters sooner than `StringJenkinsOaatHasher` or the strong block hashes. Its value here is **compatibility**: CRC-32 is one of the most widely deployed checksums, and many external systems shard, route, or bucket by it, so this hasher is the in-box answer when you need to reproduce a CRC-32-based key distribution exactly — the same role `StringMurmur2Hasher` and the FNV-1 hashers fill for their respective external systems. On its own merits it still distributes the short, structured identifiers typical of real key sets reasonably well, at a per-byte cost comparable to the cheap classics. It sits at the cheapest, classic end of the `string` escalation ladder, a peer of the table-free classics but distinguished as a table-driven checksum: `StringDjb2Hasher` / `StringDjb2AHasher` / `StringSdbmHasher` / `StringElfHasher` / `StringCrc32Hasher` (cheapest classics, weaker avalanche) / `StringFnV1AHasher` (low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (cheap FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Reach for `StringCrc32Hasher` when you specifically need CRC-32 compatibility; for general use prefer `StringFnV1AFullHasher` or `StringJenkinsOaatHasher`, and escalate to the strong family for clustered or adversarial keys.

**Parameters:** reflected generator polynomial = `0xEDB88320`, initial value = `0xFFFFFFFF`, final XOR = `0xFFFFFFFF`. The universal CRC-32 check value holds: the CRC of the ASCII bytes `"123456789"` is `0xCBF43926`.

**Note:** `Hash(s)` equals the standard CRC-32 (zlib / IEEE 802.3) over `Encoding.Unicode.GetBytes(s)`. The empty string maps to `0` (the initial `0xFFFFFFFF` XORed with the final `0xFFFFFFFF`) — no characters are folded. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringAdler32Hasher

```csharp
public struct StringAdler32Hasher : IHashProvider<string>
```

The standard **Adler-32** checksum (RFC 1950 — the same variant used by zlib's DEFLATE wrapper and the zlib chunk of PNG) applied to the string's native little-endian UTF-16 byte stream, folding both bytes of every character (low byte then high byte). It is the other widely deployed checksum from the zlib family, alongside `StringCrc32Hasher`. Adler-32 carries two running 16-bit sums modulo `65521` (the largest prime below 2^16): `a` accumulates the bytes (seeded to `1`) and `b` accumulates the running value of `a` after each byte (seeded to `0`); the 32-bit result is `(b << 16) | a`. It is computed with the straightforward per-byte modulo form — two adds and two modulo reductions per byte, with **no table** (unlike the table-driven `StringCrc32Hasher`) and no finalizer — so the per-call path is allocation-free. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

As a checksum Adler-32 is **even weaker than CRC-32** as a hash function: for short inputs both sums stay numerically small, so the high bits of the 32-bit result are barely populated and many short keys land in a narrow range — it clusters sooner than `StringCrc32Hasher`, and far sooner than `StringJenkinsOaatHasher` or the designed mixers. (Concretely, the low 16 bits are just the running byte-sum, so unrelated same-length keys collide whenever their bytes sum to the same value.) Its value here is purely **compatibility**: Adler-32 is one of the most widely deployed checksums (zlib, PNG, the rsync rolling-checksum family), and external systems that shard, route, or bucket by it can be reproduced exactly with this hasher — the same role `StringCrc32Hasher`, `StringMurmur2Hasher`, and the FNV-1 hashers fill for their respective external systems. Do not reach for it as a general-purpose hash. It sits at the cheapest, classic end of the `string` escalation ladder, a checksum peer of `StringCrc32Hasher`: `StringDjb2Hasher` / `StringDjb2AHasher` / `StringSdbmHasher` / `StringElfHasher` / `StringCrc32Hasher` / `StringAdler32Hasher` (cheapest classics and checksums, weaker avalanche) / `StringFnV1AHasher` (low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (cheap FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Reach for `StringAdler32Hasher` when you specifically need Adler-32 compatibility; for general use prefer `StringFnV1AFullHasher` or `StringJenkinsOaatHasher`, and escalate to the strong family for clustered or adversarial keys.

**Parameters:** modulus = `65521` (largest prime below 2^16), `a` seed = `1`, `b` seed = `0`, result packing = `(b << 16) | a`. The universal Adler-32 check value holds: the Adler-32 of the ASCII bytes `"Wikipedia"` is `0x11E60398`.

**Note:** `Hash(s)` equals the standard Adler-32 (RFC 1950 / zlib) over `Encoding.Unicode.GetBytes(s)`. The empty string maps to `1` (`a` stays at its seed `1`, `b` at `0`, so `(0 << 16) | 1`) — no characters are folded. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringFnV1Hasher

```csharp
public struct StringFnV1Hasher : IHashProvider<string>
```

The original **FNV-1** 32-bit hash (multiply-then-XOR) for string keys over the **full** UTF-16 representation — it folds both bytes of every character (low byte then high byte), which is exactly FNV-1 of the string's native little-endian UTF-16 byte stream. FNV-1 and FNV-1a share the same constants and differ only in the order of the two per-byte steps: FNV-1 multiplies first and XORs second (`hash = hash * prime; hash ^= b`), while `StringFnV1AFullHasher` XORs first and multiplies second. This is the FNV-1 counterpart to `StringFnV1AFullHasher`, provided for users who specifically want the original FNV-1 ordering (for example, to match an external system that hashes with FNV-1).

The ordering matters for avalanche: in FNV-1 the very last byte folded is only XORed into the accumulator with no subsequent multiply to diffuse it, so a change in the final byte propagates less thoroughly than in FNV-1a, whose last operation is always a multiply. **For this reason FNV-1a (`StringFnV1AFullHasher`) is the generally preferred member of the family and the recommended default** — reach for `StringFnV1Hasher` only when you specifically need FNV-1's ordering. For the same FNV-1 ordering at a wider 64-bit state — which clusters less on long or numerous keys — step up to `StringFnV164Hasher`. Unlike the low-byte `StringFnV1AHasher`, there is intentionally only one 32-bit FNV-1 variant and it folds the full 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits at the cheap, classic end of the `string` escalation ladder, a peer of the FNV-1a variants: `StringDjb2Hasher` / `StringFnV1Hasher` / `StringFnV1AHasher` (low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Prefer `StringFnV1AFullHasher` for general use; escalate to the throughput-oriented strong family when FNV's weaker avalanche pushes clustered or adversarial keys into long probe chains.

**Parameters:** offset basis = `2166136261`, prime = `16777619`.

**Note:** maps the empty string `""` → the offset basis (`2166136261` unsigned, `-2128831035` signed) — no characters are folded — exactly as `StringFnV1AFullHasher` maps the empty string. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

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

### StringFnV164Hasher

```csharp
public struct StringFnV164Hasher : IHashProvider<string>
```

The original **FNV-1 64-bit** hash (multiply-then-XOR) for string keys over the **full** UTF-16 representation — it folds the same little-endian UTF-16 byte stream as `StringFnV1Hasher` (low byte then high byte of every character), but accumulates into a 64-bit state using the FNV-1 64-bit parameters, then xor-folds the result down to a signed 32-bit value. This is the wide-accumulator counterpart to `StringFnV1Hasher`, and the FNV-1 counterpart to `StringFnV1A64Hasher`: FNV-1 multiplies first and XORs second (`hash = hash * prime; hash ^= b`), while `StringFnV1A64Hasher` XORs first and multiplies second. It is provided for users who specifically want the original FNV-1 ordering at 64-bit width (for example, to match an external system that hashes with FNV-1-64).

The ordering matters for avalanche: in FNV-1 the very last byte folded is only XORed into the accumulator with no subsequent multiply to diffuse it, so a change in the final byte propagates less thoroughly than in FNV-1a, whose last operation is always a multiply. **For this reason FNV-1a (`StringFnV1A64Hasher`) is the generally preferred 64-bit member of the family and the recommended default** — reach for `StringFnV164Hasher` only when you specifically need FNV-1's ordering. Carrying twice as many bits through the accumulation as `StringFnV1Hasher` means intermediate values collide far less often, so for longer keys and larger key sets the distribution holds up better before the final fold (`h ^ (h >> 32)`, the standard FNV retraction). Like `StringFnV1Hasher` it folds the full 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits at the cheap, classic end of the `string` escalation ladder, a peer of the FNV-1a variants: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Prefer `StringFnV1A64Hasher` for general use; reach for `StringFnV164Hasher` when you need FNV-1's ordering at the wider 64-bit width, and escalate to the throughput-oriented strong family when FNV's weaker avalanche pushes clustered or adversarial keys into long probe chains.

**Parameters:** offset basis = `14695981039346656037`, prime = `1099511628211`.

**Note:** maps the empty string `""` → the 64-bit offset basis xor-folded to 32 bits — no characters are folded — exactly as `StringFnV1A64Hasher` maps the empty string (FNV-1 and FNV-1a share the same offset basis). The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringJenkinsOaatHasher

```csharp
public struct StringJenkinsOaatHasher : IHashProvider<string>
```

Bob Jenkins' **one-at-a-time** (OAAT) hash applied to the string's native little-endian UTF-16 byte stream — it folds both bytes of every character (low byte then high byte). For each byte it runs the add / shift / xor lattice `hash += b; hash += hash << 10; hash ^= hash >> 6`, then finishes with a three-step avalanche `hash += hash << 3; hash ^= hash >> 11; hash += hash << 15`. Where FNV-1a folds each byte with a single xor and one multiply — and so has comparatively weak per-bit avalanche — the shift / xor steps give every input bit influence over the whole 32-bit result, yet the function uses **no multiplies at all** (only adds, shifts, and xors), keeping it in the same cheap cost class. It is the classic "better distributed than FNV-1a, cheaper than the block hashes" middle option. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits in the cheap tier of the `string` escalation ladder, between the FNV-1a variants and the strong block hashes: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (cheap FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche than FNV-1a) → `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Prefer it over the FNV-1a hashers when FNV-1a's single-multiply mixing clusters your keys but you do not want to pay for a block hash; escalate to the throughput-oriented strong family when the keys are clustered or adversarial enough to push long probe chains regardless.

**Note:** maps the empty string `""` → `0` — the accumulator starts at zero and the three finalization steps leave zero unchanged. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

### StringMurmur2Hasher

```csharp
public struct StringMurmur2Hasher : IHashProvider<string>
```

Austin Appleby's original **MurmurHash2** (32-bit) algorithm applied to the string's native little-endian UTF-16 byte stream. This is the MurmurHash2 predecessor of `StringMurmur3Hasher`: the two share the same family and the same multiply constant (`m = 0x5bd1e995`), but MurmurHash2 uses a single mixing pass per 4-byte block (`k *= m; k ^= k >> 24; k *= m; h *= m; h ^= k`) and a lighter two-shift finalization (`h ^= h >> 13; h *= m; h ^= h >> 15`), where MurmurHash3 adds a rotate-based block mix and the stronger `fmix32` finalizer. The MurmurHash3 finalizer gives every input bit influence over every output bit and avoids the few known MurmurHash2 weaknesses (most notably that XOR-equal differences in adjacent blocks can cancel), so `StringMurmur3Hasher` is the generally preferred member of the family. Like the other full-width string hashers it consumes the **full** 16-bit value of every character, so it distinguishes characters that differ only in their upper byte — for example `'A'` (`U+0041`) and `'Ł'` (`U+0141`), which `StringFnV1AHasher` collides on.

It sits at the strong-distribution top of the `string` escalation ladder as a same-family peer of `StringMurmur3Hasher`: `StringFnV1AHasher` (cheapest, low-byte only) → `StringFnV1AFullHasher` / `StringFnV1A64Hasher` (cheap FNV-1a, full Unicode width) → `StringJenkinsOaatHasher` (cheap, full Unicode width, multiply-free, stronger per-bit avalanche than FNV-1a) → `StringMurmur2Hasher` / `StringMurmur3Hasher` / `StringXxHash32Hasher` / `StringXxHash64Hasher` / `StringMetroHash64Hasher` / `StringCityHash64Hasher` / `StringXxHash3Hasher` (strong avalanche, maximum throughput). Prefer `StringMurmur3Hasher` for general use; reach for `StringMurmur2Hasher` when you specifically need MurmurHash2 — for example to match an external system (Hadoop, Cassandra, Elasticsearch, and many client libraries have historically hashed with MurmurHash2) — or want its slightly cheaper single-multiply-per-block mix on trusted, already-uniform keys.

**Algorithm:** standard MurmurHash2 (seed `0`) — pairs of characters are read as little-endian 32-bit blocks, each mixed with `k *= m; k ^= k >> 24; k *= m` and folded with `h = h * m ^ k`; a trailing odd character is folded as a 2-byte tail (`h ^= char; h *= m`), then the result is run through the two-shift finalization mix.

**Note:** `Hash(s)` equals canonical MurmurHash2 (seed `0`) over `Encoding.Unicode.GetBytes(s)`. The empty string maps to `0` (seed `0` XOR a zero byte length, left unchanged by the finalization mix), just as `StringMurmur3Hasher` maps the empty string. The dictionaries store the out-of-band `null`-key entry without calling the hasher, so this does not collide with the empty-slot sentinel.

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

**Read this first — the hashers are not positioned on speed.** For `int` keys, `GetHashCode()` *is* the identity (zero work), so no mixing hasher can beat it on throughput; for `string` keys, `GetHashCode()` is already a purpose-built **Marvin32** with per-process random seeding. The value a Celerity struct hasher adds is **distribution quality (avalanche), determinism (reproducible across processes and runtimes), adversarial resistance, and the zero-cost devirtualized generic** — *not* raw hashing speed. Pick on those axes, not on the isolated `Hash()` microbench.

The decision reduces to a **speed-vs-quality curve**, the same tradeoff F14 / ahash / FxHash document:

- **Trusted, uniform keys** (dense sequential IDs, already-random hashes): a cheap, weak hash is fine — the input already carries entropy, so mixing only adds cost. *Drop* to the cheapest option (identity / XOR-fold).
- **Clustered keys** (low-entropy, structured, or shared-prefix): a weak hash leaves the clustering in place, so probe chains lengthen and lookups pay it on every read. Escalate to a strong-avalanche mixer (Wang finalizer → Murmur3 / xxHash). This is the case where a hasher that "loses" the speed microbench *wins* end-to-end — see [end-to-end probe analysis](#end-to-end-probe-analysis).
- **Untrusted / adversarial input**: see the HashDoS caveat below.

> **Fixed-seed hashers are not a HashDoS defence.** A hardcoded-seed Murmur3 / FNV / xxHash is *not* more flood-resistant than `string.GetHashCode()` — usually **less**, because an attacker who knows the (fixed, public) algorithm and seed can precompute colliding keys offline, whereas the BCL's Marvin32 re-seeds per process so the collision set is not knowable ahead of time. What actually stops hash-flooding is a **keyed** PRF with a *secret, per-process-random* key. The keyed hashers here (`StringSipHash13Hasher`, `StringSipHash24Hasher`, `StringHalfSipHash24Hasher`, `StringHighwayHash64Hasher`) give you a strong PRF, but only resist flooding if you seed them with a secret the attacker can't observe; with their default fixed seed they are deterministic-but-not-secret, i.e. good for reproducibility, not for DoS resistance. When in doubt for untrusted string keys, the BCL `string.GetHashCode()` (`DefaultHasher<string>`) is the safe default.

| Key type | Default | Alternative |
|---|---|---|
| `int` | `Int32WangNaiveHasher` (used by `IntDictionary` / `IntSet`) | `Int32IdentityHasher` (the zero-work floor — *drop* to it for uniform/trusted keys like dense sequential IDs, where any mixing is pure overhead); `Int32WangHasher` (full Thomas-Wang finalizer) or `Int32Murmur3Hasher` for clustered or adversarial keys |
| `long` | `Int64WangNaiveHasher` (used by `LongDictionary` / `LongSet`) | `Int64IdentityHasher` (the zero-work floor — *drop* to it for keys whose low 32 bits are well distributed, e.g. dense sequential IDs; note it ignores the upper 32 bits); `Int64WangHasher` (full Thomas-Wang finalizer) or `Int64Murmur3Hasher` for clustered, high-half-distinct, or adversarial keys |
| `uint` | `UInt32Hasher` | `UInt32WangHasher` (full Thomas-Wang finalizer) or `UInt32Murmur3Hasher` (Murmur3 `fmix32`) for clustered or adversarial keys |
| `ulong` | `UInt64Hasher` (Murmur3 `fmix64`) | `UInt64WangHasher` (full Thomas-Wang finalizer) when the two `fmix64` multiplies are a hot-path cost and keys are already reasonably uniform; `UInt64WangNaiveHasher` (XOR-fold) for the cheapest option on already-uniform keys |
| `Guid` | `GuidHasher` | `DefaultHasher<Guid>` (slower but BCL-equivalent) |
| `string` | `StringFnV1AHasher` | `StringDjb2Hasher` (Bernstein's djb2 — the simplest, cheapest classic, shift-and-add with no real multiply, full-character fold) when you want a familiar minimal hash on short ASCII identifiers and djb2's weaker avalanche is acceptable; `StringDjb2AHasher` (the djb2a XOR-folding variant — same `* 33` cost, but XORs the byte instead of adding it, mirroring the FNV-1/FNV-1a split; avoids djb2's low-bit carry bias for slightly cleaner diffusion at the same cheapest cost class); `StringSdbmHasher` (the sdbm classic — same cheapest cost class, `* 65599` via two shifts and a subtract, full-character fold; its larger multiplier tends to distribute slightly better than djb2 on short keys, with the same weak avalanche); `StringElfHasher` (the PJW / ELF symbol-table hash — same cheapest cost class, a shift-and-add with a top-nibble fold-back that recirculates high-order bits, full-character fold, 28-bit non-negative result; **but the [measured distribution report](#measured-distribution-quality) shows it clusters badly on ASCII keys — prefer djb2 / sdbm over it there**); `StringCrc32Hasher` (the standard CRC-32 / zlib / IEEE 802.3 checksum — the family's only table-driven member, full-character fold; a linear checksum with weaker avalanche than the designed mixers, provided primarily to reproduce a CRC-32-based key distribution exactly when matching an external system); `StringAdler32Hasher` (the standard Adler-32 / zlib / RFC 1950 checksum — table-free running 16-bit sums, full-character fold; even weaker than CRC-32 as a hash because its low 16 bits are just the byte-sum so short keys cluster, provided purely to reproduce an Adler-32-based key distribution exactly, e.g. zlib / PNG / rsync); `StringFnV1Hasher` (the original FNV-1 multiply-then-XOR ordering, full-character fold) when you specifically need FNV-1 rather than the generally preferred FNV-1a, or `StringFnV164Hasher` for that same FNV-1 ordering folded into a 64-bit accumulator when keys are long or numerous enough to cluster the 32-bit state; `StringFnV1AFullHasher` (same FNV-1a cost, folds the full character) for non-ASCII content the low-byte fold would collide; `StringFnV1A64Hasher` (full-character fold into a 64-bit accumulator) when keys are long or numerous enough to cluster the 32-bit state; `StringJenkinsOaatHasher` (Bob Jenkins' one-at-a-time hash — multiply-free, with stronger per-bit avalanche than FNV-1a at the same cheap cost class) when FNV-1a's single-multiply mixing clusters keys but a block hash is more than you want to pay; `StringMurmur3Hasher` (the `fmix32`-finalized MurmurHash3, with `StringMurmur2Hasher` as its older same-family sibling for MurmurHash2 compatibility), `StringXxHash32Hasher`, `StringXxHash64Hasher`, `StringMetroHash64Hasher`, `StringCityHash64Hasher`, or `StringXxHash3Hasher` (the throughput-oriented strong-avalanche options for longer keys — XXH64 widens the accumulators and stripe further for longer keys on 64-bit platforms, MetroHash64 is a peer worth profiling against on mid-length keys, CityHash64 is length-classed so it often edges ahead on short-to-mid keys, and XXH3 is the third-generation xxHash that is length-classed *and* runs an eight-lane bulk loop, typically the fastest across both short and long keys) for clustered / adversarial keys that need strong avalanche; `StringHalfSipHash24Hasher` (HalfSipHash-2-4, keyed — the cheaper 32-bit-word variant for short keys / 32-bit targets, with a native 32-bit output and no fold), `StringSipHash13Hasher` (SipHash-1-3, keyed — the faster reduced-round 64-bit variant Rust's `HashMap` uses by default), `StringSipHash24Hasher` (SipHash-2-4, keyed — the conservative variant), or `StringHighwayHash64Hasher` (HighwayHash64, keyed — the SIMD-oriented alternative, scalar today) when the keys are untrusted and you need hash-flooding resistance rather than maximum throughput; `DefaultHasher<string>` (uses the BCL string hasher) |
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

---

## End-to-end probe analysis

`HashQualityEvaluator` models a **separate-chaining** table: it counts how many keys land in each bucket, so its `MaxBucketLoad` tells you how many keys *share* a slot. That is the right model for a chained table, but the Celerity collections do not chain — they **linearly probe**. On a collision they walk to the next slot, which makes neighbouring clusters *merge* (primary clustering), so the cost a lookup actually pays is its **probe length**: the number of slots it reads, starting at the key's natural slot, before it finds the key. Two hashers with the same bucket-load profile can have very different probe lengths once you account for that merging.

`ProbeStatisticsEvaluator` answers the question users actually feel — *how many slots does a lookup read on average and in the worst case?* — by replaying the **exact** open-addressed, power-of-two, linear-probing placement the collections use (`index = hash & (TableSize - 1)`, then `(index + 1) & mask` on a collision).

### ProbeStatisticsEvaluator

```csharp
public static class ProbeStatisticsEvaluator
{
    public const float DefaultLoadFactor = 0.75f;

    public static ProbeStatistics Evaluate<T, THasher>(
        IEnumerable<T> keys,
        int? capacity = null,
        float loadFactor = DefaultLoadFactor)
        where THasher : struct, IHashProvider<T>;
}
```

Places every key into a linearly-probed table sized for the key count at `loadFactor` — including load-factor headroom, exactly as the collections' bulk constructors size their backing arrays — and reports the resulting probe-length distribution. Keys equal to an already-placed key (by `EqualityComparer<T>.Default`) are counted as duplicates and not inserted twice, matching the dictionaries' set semantics.

Like `HashQualityEvaluator` this is a **diagnostic** API, not a hot path: it materializes the sequence and allocates a table-sized buffer, so call it offline. It throws `ArgumentNullException` for a null `keys`, and `ArgumentOutOfRangeException` for a negative `capacity` or a `loadFactor` outside `(0, 1)`.

### ProbeStatistics

```csharp
public readonly struct ProbeStatistics
```

An immutable bag of metrics returned by `Evaluate`. Its `ToString()` renders a one-line summary.

| Member | Meaning |
|---|---|
| `KeyCount` | Number of keys read from the input (duplicates included). |
| `EntryCount` | Distinct entries actually placed (`KeyCount - DuplicateKeyCount`). Probe metrics are over these. |
| `DuplicateKeyCount` | Input keys equal to an already-placed key, so not inserted again. |
| `TableSize` | The power-of-two table size the entries were placed into (sized with load-factor headroom). |
| `LoadFactor` | The achieved fill ratio, `EntryCount / TableSize`. Probe length climbs steeply as this nears `1.0`. |
| `CollisionCount` | Entries that did **not** land in their natural slot (probe length > 1) — the open-addressing notion of a collision, which accounts for primary clustering. |
| `CollisionRate` | `CollisionCount / EntryCount`, in `[0, 1]`. |
| `TotalProbeLength` | Sum of every entry's probe length. |
| `AverageProbeLength` | Mean probes for a successful lookup, `TotalProbeLength / EntryCount`. **`1.0` = every entry in its natural slot**; higher is worse. |
| `MaxProbeLength` | Worst-case probe length — the longest run a single lookup walks, i.e. the tail-latency driver an adversarial key set inflates. |

### Example

```csharp
using Celerity.Hashing;

int[] keys = LoadProductionKeySample();

ProbeStatistics naive  = ProbeStatisticsEvaluator.Evaluate<int, Int32WangNaiveHasher>(keys);
ProbeStatistics murmur = ProbeStatisticsEvaluator.Evaluate<int, Int32Murmur3Hasher>(keys);

Console.WriteLine(naive);   // e.g. AvgProbe=58.10, MaxProbe=4112 on clustered keys
Console.WriteLine(murmur);  // e.g. AvgProbe=1.35,  MaxProbe=12

// If the cheap default already keeps AverageProbeLength near 1, a stronger mixer buys nothing.
// If MaxProbeLength is large, your keys cluster under this hasher — escalate.
```

The `Celerity.Benchmarks` project prints a full hasher × distribution probe table with `dotnet run -c Release -- --probe-analysis`, and times the same hashers end-to-end through the dictionaries in `HasherEndToEndBenchmark` (see [the performance guide](../performance.md#extended-benchmark-suite)). Read the deterministic probe numbers and the throughput numbers together: a strong hasher that "loses" the isolated `Hash()` microbench still **wins end-to-end** on clustered or adversarial keys because it collapses these probe chains.

---

## Benchmarking the string hashers

> **These are raw-mixing-cost diagnostics, not the headline metric.** An isolated `Hash()` loop ranks hashers by the nanoseconds spent mixing — which for `int` makes identity / `GetHashCode()` unbeatable and is *actively misleading*, because a hasher's real cost is the probe chains it produces, not the time inside `Hash`. Read these alongside the end-to-end numbers ([`HasherEndToEndBenchmark`](../performance.md#measure-probe-length-not-just-hash-speed) and the deterministic `--probe-analysis` report), where a strong hasher that "loses" here wins on clustered / adversarial keys.

The `Celerity.Benchmarks` project includes `StringHasherBenchmark`, a head-to-head BenchmarkDotNet comparison of every built-in `string` hasher. It hashes a deterministic sample of distinct keys through each hasher via the `where THasher : struct, IHashProvider<string>` constraint — so the JIT inlines `Hash` exactly as it does on a real collection's probe path — and sweeps three key shapes via its `Shape` parameter. Two baselines bracket each run: the direct `string.GetHashCode()` (`Bcl_GetHashCode`) and `EqualityComparer<string>.Default.GetHashCode()` (`EqualityComparer_Default`) — the latter being the call a BCL `Dictionary<string,>` actually makes per probe, i.e. the realistic thing a developer replaces. Both are the speed floor of a *randomized, DoS-resistant* Marvin32 hash, not a zero-work one. The key shapes:

| `Shape` | Keys |
|---|---|
| `ShortAscii` | Short lowercase-alphanumeric identifiers (6–12 chars) — the common map-key case. |
| `LongAscii` | Long ASCII path / URL-like keys (48–80 chars). |
| `NonAscii` | Shorter mixed Latin + CJK text (10–20 chars) that exercises the full-width fold. |

The three shapes matter because the length-classed hashers (`StringCityHash64Hasher`, `StringXxHash3Hasher`) and the four-accumulator stripe hashers (`StringXxHash32Hasher`, `StringXxHash64Hasher`) behave very differently across lengths, and the full-width hashers do extra work on non-ASCII upper bytes. The benchmark is registered in `Program.cs` so it joins the CI report and the gh-pages benchmark history on every push to `main`.

A companion `IntegerHasherBenchmark` does the same for the fixed-width integer and `Guid` hashers, grouped by key type (`int` / `long` / `uint` / `ulong` / `Guid`), each bracketed by two baselines — the direct `GetHashCode()` (`{Type}_Bcl`) and `EqualityComparer<T>.Default.GetHashCode()` (`{Type}_EqualityComparer`, the per-probe call a BCL `Dictionary<,>` makes). For `int` / `long` the `{Type}_Identity` row is the explicit **zero-work floor**: a passthrough that does no mixing, which for `int` tracks the `_Bcl` baseline (since `int.GetHashCode()` is itself the identity), confirming that no mixing hasher beats it on raw throughput. It has no key-shape parameter: hashing a fixed-width integer is branch-free and constant-time regardless of the key's value, so the sample's distribution affects collision *quality* but not `Hash` throughput.

Both benchmarks are rendered on the [live benchmark dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) under **Hash function throughput** — one ranked bar per hasher within each key type / shape group, relative to the framework `GetHashCode()` baseline, refreshed on every push to `main`. To run them locally:

```bash
cd src/Celerity.Benchmarks
dotnet run -c Release -- --filter "*HasherBenchmark*"
```

> The hasher benchmarks measure **raw mixing cost only** — they are a diagnostic, not the metric you choose a hasher by. A fast hasher that clusters is not a win (see the ROADMAP guiding principles), so read these numbers alongside the distribution metrics below *and* the [end-to-end probe analysis](#end-to-end-probe-analysis) before committing a hasher: prefer the cheapest hasher whose `DistributionScore` stays near `1.0` and whose `MaxBucketLoad` (and average probe length) is low on a representative sample of your keys.

### Measured distribution quality

Throughput is only half the picture. The companion `--hash-quality` report runs [`HashQualityEvaluator`](#hashqualityevaluator) over the **same** deterministic key samples the throughput benchmarks use (`HasherKeySamples`), so a hasher's speed and its distribution describe the same keys. Distribution quality is deterministic — it does not depend on CI hardware or timing noise — so unlike throughput these numbers are stable enough to cite directly. Reproduce them with:

```bash
cd src/Celerity.Benchmarks
dotnet run -c Release -- --hash-quality
```

The numbers below are over **2000 distinct keys spread across 4096 buckets** (load factor ≈ 0.49, a representative healthy table). `Score` is `DistributionScore` (**1.00 = ideal uniform**, above 1.00 = clustering / longer probe chains); `Max load` is the worst-case bucket occupancy; `Collisions` counts raw 32-bit hash-code collisions.

**The headline: on these samples almost every hasher distributes near-ideally (score ≈ 0.97–1.03), with one sharp outlier — `StringElfHasher` clusters hard on ASCII keys** (score **11.4**, max bucket load **31** on short ASCII, vs ~4–5 for everyone else). The PJW/ELF hash's low output bits are poorly mixed for ASCII-range inputs, so masking to a power-of-two table concentrates keys into a few buckets — a textbook case of "cheap to compute, but the clustering costs you far more on the probe path than the hash saved." It recovers on non-ASCII keys (score 2.27) once CJK code points feed the upper bytes. **Avoid `StringElfHasher` for ASCII-dominated keys**; reach for `StringDjb2Hasher` / `StringDjb2AHasher` / `StringSdbmHasher` (equally cheap, all near 1.0) or step up to `StringFnV1AFullHasher` / `StringJenkinsOaatHasher`.

<details>
<summary><strong>string hashers · ShortAscii</strong> (6–12-char identifiers)</summary>

| Hasher | Score | Max load | Collisions |
|---|---|---|---|
| `StringFnV1AFullHasher` | 0.978 | 4 | 0 |
| `StringXxHash3Hasher` | 0.980 | 4 | 0 |
| `StringFnV1Hasher` | 0.983 | 4 | 0 |
| `StringXxHash64Hasher` | 0.987 | 5 | 0 |
| `StringHighwayHash64Hasher` | 0.987 | 4 | 0 |
| `StringDjb2AHasher` | 0.988 | 4 | 0 |
| `StringMetroHash64Hasher` | 0.992 | 4 | 0 |
| `StringFnV1A64Hasher` | 0.992 | 4 | 0 |
| `StringCityHash64Hasher` | 0.993 | 5 | 0 |
| `StringFnV1AHasher` | 0.993 | 5 | 0 |
| `DefaultHasher<string>` (BCL) | 0.995 | 5 | 0 |
| `StringSdbmHasher` | 0.995 | 5 | 0 |
| `StringDjb2Hasher` | 0.997 | 5 | 0 |
| `StringXxHash32Hasher` | 0.998 | 4 | 0 |
| `StringSipHash13Hasher` | 1.002 | 5 | 0 |
| `StringSipHash24Hasher` | 1.003 | 4 | 0 |
| `StringCrc32Hasher` | 1.004 | 5 | 0 |
| `StringMurmur3Hasher` | 1.009 | 4 | 0 |
| `StringFnV164Hasher` | 1.015 | 5 | 0 |
| `StringHalfSipHash24Hasher` | 1.015 | 5 | 0 |
| `StringMurmur2Hasher` | 1.015 | 4 | 0 |
| `StringJenkinsOaatHasher` | 1.023 | 5 | 0 |
| `StringElfHasher` | **11.387** | **31** | 0 |

</details>

<details>
<summary><strong>string hashers · LongAscii</strong> (48–80-char path/URL-like keys)</summary>

| Hasher | Score | Max load | Collisions |
|---|---|---|---|
| `StringFnV1A64Hasher` | 0.971 | 4 | 0 |
| `StringMurmur3Hasher` | 0.974 | 4 | 0 |
| `StringHalfSipHash24Hasher` | 0.977 | 4 | 0 |
| `StringXxHash64Hasher` | 0.979 | 4 | 0 |
| `StringJenkinsOaatHasher` | 0.980 | 4 | 0 |
| `StringCityHash64Hasher` | 0.985 | 5 | 0 |
| `StringXxHash3Hasher` | 0.987 | 5 | 0 |
| `StringSipHash13Hasher` | 0.988 | 5 | 0 |
| `StringSdbmHasher` | 0.989 | 4 | 0 |
| `StringFnV1AHasher` | 0.995 | 3 | 0 |
| `DefaultHasher<string>` (BCL) | 0.998 | 4 | 0 |
| `StringSipHash24Hasher` | 0.999 | 5 | 0 |
| `StringDjb2Hasher` | 1.000 | 4 | 0 |
| `StringXxHash32Hasher` | 1.001 | 4 | 0 |
| `StringFnV1AFullHasher` | 1.005 | 4 | 0 |
| `StringCrc32Hasher` | 1.005 | 5 | 0 |
| `StringMurmur2Hasher` | 1.005 | 5 | 0 |
| `StringHighwayHash64Hasher` | 1.009 | 4 | 0 |
| `StringFnV164Hasher` | 1.011 | 5 | 0 |
| `StringFnV1Hasher` | 1.013 | 6 | 0 |
| `StringMetroHash64Hasher` | 1.028 | 5 | 0 |
| `StringDjb2AHasher` | 1.033 | 4 | 0 |
| `StringElfHasher` | **11.125** | **26** | 2 |

</details>

<details>
<summary><strong>string hashers · NonAscii</strong> (10–20-char Latin + CJK)</summary>

| Hasher | Score | Max load | Collisions |
|---|---|---|---|
| `StringXxHash3Hasher` | 0.977 | 5 | 0 |
| `StringSipHash13Hasher` | 0.980 | 5 | 0 |
| `StringFnV1AHasher` | 0.980 | 6 | 0 |
| `DefaultHasher<string>` (BCL) | 0.983 | 4 | 0 |
| `StringFnV164Hasher` | 0.983 | 4 | 0 |
| `StringMurmur3Hasher` | 0.989 | 4 | 0 |
| `StringXxHash64Hasher` | 0.989 | 5 | 0 |
| `StringSdbmHasher` | 0.990 | 4 | 0 |
| `StringFnV1Hasher` | 0.994 | 4 | 0 |
| `StringFnV1AFullHasher` | 0.997 | 4 | 0 |
| `StringDjb2AHasher` | 1.000 | 4 | 0 |
| `StringJenkinsOaatHasher` | 1.000 | 4 | 0 |
| `StringCityHash64Hasher` | 1.001 | 5 | 0 |
| `StringXxHash32Hasher` | 1.002 | 5 | 0 |
| `StringHalfSipHash24Hasher` | 1.005 | 4 | 0 |
| `StringMurmur2Hasher` | 1.005 | 4 | 0 |
| `StringDjb2Hasher` | 1.009 | 4 | 0 |
| `StringSipHash24Hasher` | 1.013 | 5 | 0 |
| `StringMetroHash64Hasher` | 1.015 | 4 | 0 |
| `StringCrc32Hasher` | 1.017 | 5 | 0 |
| `StringFnV1A64Hasher` | 1.018 | 5 | 0 |
| `StringHighwayHash64Hasher` | 1.029 | 5 | 0 |
| `StringElfHasher` | **2.267** | **12** | 0 |

</details>

For the fixed-width integer and `Guid` hashers every option — including the cheap XOR-fold defaults and the BCL `GetHashCode()` — distributes near-ideally on a uniform random sample (all scores ≈ 0.98–1.03, max load 4–6), so the cheap default is the right call until you have evidence of clustered or adversarial keys:

<details>
<summary><strong>int / long / uint / ulong / Guid hashers</strong></summary>

| Hasher | Score | Max load | Collisions |
|---|---|---|---|
| `Int32Murmur3Hasher` | 0.991 | 4 | 0 |
| `Int32WangNaiveHasher` (default) | 1.020 | 5 | 0 |
| `Int32WangHasher` | 1.027 | 5 | 0 |
| `Int64WangNaiveHasher` (default) | 1.001 | 6 | 0 |
| `Int64WangHasher` | 1.011 | 4 | 0 |
| `Int64Murmur3Hasher` | 1.014 | 5 | 0 |
| `UInt32Hasher` (default) | 1.020 | 5 | 0 |
| `UInt32WangHasher` | 1.027 | 5 | 0 |
| `UInt32Murmur3Hasher` | 0.991 | 4 | 0 |
| `UInt64Hasher` (default) | 1.014 | 5 | 0 |
| `UInt64WangHasher` | 1.011 | 4 | 0 |
| `UInt64WangNaiveHasher` | 1.001 | 6 | 0 |
| `GuidHasher` | 0.983 | 4 | 0 |

</details>

> These distribution numbers describe a uniform-ish random sample. They are a guide, not a guarantee: if your real keys are clustered or adversarial, re-run `--hash-quality` (or call `HashQualityEvaluator.Evaluate` directly) on **your** key sample, since that is exactly the case where the cheap hashers' weaker avalanche starts to matter.
