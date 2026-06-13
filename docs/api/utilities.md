# Utilities API Reference

## FastUtils

```csharp
namespace Celerity.Primitives;

public static class FastUtils
```

Lives in the `Celerity.Primitives` namespace and ships in the **`Celerity.Primitives`** NuGet package. Provides low-level helper methods used internally by the collection implementations. Public so that advanced users can reuse them.

> **Moved in 2.0.0:** `FastUtils` was in the root `Celerity` namespace before the package split. Add `using Celerity.Primitives;` (or qualify as `Celerity.Primitives.FastUtils`). See the [migration guide](../migration.md#200--the-package-split).

### NextPowerOfTwo

```csharp
public static int NextPowerOfTwo(int n)
```

Returns the smallest power of two that is greater than or equal to `n`.

**Special cases:**

- `n <= 0` returns `1`.
- `n >= 2^30` returns `2^30` (1,073,741,824) to prevent integer overflow.
- If `n` is already a power of two, it is returned unchanged.

**Used by** all the Celerity collections (`CelerityDictionary`, `IntDictionary`, `LongDictionary`, `CeleritySet`, `IntSet`, `LongSet`) to round the user-supplied capacity to a power of two, which enables fast index computation via bitwise AND instead of modulo.

### FastMod / FastDiv

```csharp
public static ulong   GetFastModMultiplier(uint  divisor);
public static uint     FastMod(uint  value, uint  divisor, ulong   multiplier);
public static uint     FastDiv(uint  value,               ulong   multiplier);

public static UInt128 GetFastModMultiplier(ulong divisor);
public static ulong    FastMod(ulong value, ulong divisor, UInt128 multiplier);
public static ulong    FastDiv(ulong value,               UInt128 multiplier);
```

Daniel Lemire's [reciprocal modulo and division](https://lemire.me/blog/2019/02/08/faster-remainder-by-direct-computation/) (Lemire, Kaser, Kurz, 2019): when a **divisor is fixed at run time** and reused across many operations, precompute a reciprocal once and replace each `value % divisor` / `value / divisor` with a widening multiply and a shift. A hardware integer `DIV` is long-latency (~20–40 cycles on x64) and does not pipeline; the multiply-based form runs **2–4× faster** when the same divisor is reused millions of times.

The BCL has the same technique internally (`System.Collections.HashHelpers.GetFastModMultiplier` / `FastMod`) but it is not public. `FastUtils` exposes it, plus a `FastDiv` companion and a 64-bit (`ulong`) variant.

**Workloads:** hash-bucket indexing, ring buffers, sharding / partitioning, rate limiting, time-wheel timers — anywhere the divisor is a value (not a compile-time constant, which the JIT already strength-reduces) reused across a hot loop.

**Usage** — compute the multiplier once, reuse it per operation:

```csharp
using Celerity;

uint slots = ReadShardCountFromConfig();      // divisor known only at run time
ulong multiplier = FastUtils.GetFastModMultiplier(slots);

foreach (uint key in keys)
{
    uint shard = FastUtils.FastMod(key, slots, multiplier);  // == key % slots
    uint page  = FastUtils.FastDiv(key, multiplier);          // == key / slots
    Route(shard, page);
}
```

The 64-bit overloads are identical with `ulong` operands and a `UInt128` multiplier.

**Contract and special cases:**

- `GetFastModMultiplier` throws `ArgumentOutOfRangeException` for a `0` divisor.
- `FastMod` is exact for every `value` and every `divisor >= 1` — it reproduces the built-in `%` operator bit-for-bit.
- `FastDiv` is exact for every `value` provided the multiplier was produced for a `divisor >= 2`. For `divisor == 1` the multiplier overflows to `0`, which is still correct for `FastMod` (every value mod 1 is 0) but makes `FastDiv` return `0` instead of `value` — guard or special-case `divisor == 1` at the call site.
- The multiplier is the only state; recomputing it per operation throws away the win. Compute it once per divisor and reuse it.

Both methods are allocation-free, `[MethodImpl(AggressiveInlining)]`, and AOT-safe (no reflection).

## Struct PRNGs

```csharp
namespace Celerity.Primitives;

public interface IRandomSource              { ulong NextUInt64(); }

public struct SplitMix64        : IRandomSource { public SplitMix64(ulong seed); … }
public struct Xoshiro256StarStar: IRandomSource { public Xoshiro256StarStar(ulong seed); … }
public struct Xoroshiro128Plus  : IRandomSource { public Xoroshiro128Plus(ulong seed); … }
public struct WyRand            : IRandomSource { public WyRand(ulong seed); … }
public struct Pcg32             : IRandomSource { public Pcg32(ulong seed); public Pcg32(ulong seed, ulong sequence); public uint NextUInt32(); … }

// Shared, zero-cost surface over any IRandomSource (Celerity.Primitives.RandomSourceExtensions):
public static uint   NextUInt32<TRng>(this ref TRng rng)                           where TRng : struct, IRandomSource;
public static double NextDouble<TRng>(this ref TRng rng)                           where TRng : struct, IRandomSource; // [0, 1)
public static float  NextSingle<TRng>(this ref TRng rng)                           where TRng : struct, IRandomSource; // [0, 1)
public static bool   NextBool<TRng>(this ref TRng rng)                             where TRng : struct, IRandomSource;
public static int    NextInt<TRng>(this ref TRng rng, int maxExclusive)            where TRng : struct, IRandomSource; // [0, max)
public static int    NextInt<TRng>(this ref TRng rng, int min, int maxExclusive)   where TRng : struct, IRandomSource; // [min, max)
public static long   NextInt64<TRng>(this ref TRng rng, long min, long maxExclusive) where TRng : struct, IRandomSource;
public static void   NextBytes<TRng>(this ref TRng rng, Span<byte> buffer)         where TRng : struct, IRandomSource;
```

A curated suite of **value-type, allocation-free, seedable-deterministic** pseudo-random generators. `System.Random` is a heap class behind virtual dispatch, `Random.Shared` is not inlinable, and a **seeded** `new Random(seed)` falls back to the legacy Knuth subtractive algorithm — so the reproducible path people rely on is both non-xoshiro and slower. These structs fill that gap: each is a small mutable `struct` advancing its own state, so a tight inner loop allocates nothing and the `NextUInt64` call inlines through the `where TRng : struct, IRandomSource` constraint (the same zero-cost-devirtualization pattern the [struct hashers](hashing.md) use).

**Choosing a generator:**

| Generator | State | Period | Pick it for |
|---|---|---|---|
| `Xoshiro256StarStar` | 256-bit | 2²⁵⁶ − 1 | **General-purpose default.** Strong on every output bit; the algorithm .NET uses internally for `Random.Shared`. |
| `Xoroshiro128Plus` | 128-bit | 2¹²⁸ − 1 | Fastest **doubles**. The `+` scrambler has weak *low* bits — use only via the high-bit helpers (`NextDouble`/`NextSingle`/`NextInt`), not raw low bits. |
| `WyRand` | 64-bit | 2⁶⁴ | **Raw throughput** — a flood of decent numbers (procedural gen, sampling, jitter) where a 2⁶⁴ period suffices. |
| `SplitMix64` | 64-bit | 2⁶⁴ | The **seed expander** for the others; usable standalone for non-critical randomness. |
| `Pcg32` | 64-bit | 2⁶⁴ | **Statistical reputation** + independent **streams** (same seed, different `sequence` ⇒ uncorrelated). 32-bit native output. |

**Workloads:** bounded RNG in tight inner loops (Monte-Carlo, shuffles, fuzzers, procedural generation) and fast, reproducible **seeded** runs.

**Usage** — seed once, draw repeatedly; the generator is a mutable local:

```csharp
using Celerity.Primitives;

var rng = new Xoshiro256StarStar(seed: 12345);   // deterministic from the seed

ulong  bits   = rng.NextUInt64();        // raw 64-bit
double unit   = rng.NextDouble();        // [0, 1)
int    dieRoll = rng.NextInt(1, 7);      // [1, 7)  — unbiased (Lemire nearly-divisionless)
bool   coin   = rng.NextBool();

// Works generically over any generator via the struct constraint — a zero-cost shuffle:
static void Shuffle<TRng>(int[] a, ref TRng rng) where TRng : struct, IRandomSource
{
    for (int i = a.Length - 1; i > 0; i--)
    {
        int j = rng.NextInt(i + 1);
        (a[i], a[j]) = (a[j], a[i]);
    }
}
```

**Contract and special cases:**

- **Deterministic by construction.** Every constructor takes an explicit `ulong` seed (there is no entropy-seeded overload); the same seed always yields the same sequence. Multi-word generators (`Xoshiro256StarStar`, `Xoroshiro128Plus`) expand the seed through `SplitMix64`, so **every** seed is valid — the degenerate all-zero state that would lock those generators is unreachable, including from `seed: 0`.
- **Half-open ranges.** `NextDouble` / `NextSingle` return `[0, 1)` using the full mantissa (top 53 / 24 bits). The bounded `NextInt` / `NextInt64` return `[min, max)` and are **unbiased** (Lemire's nearly-divisionless rejection); they throw `ArgumentOutOfRangeException` for a non-positive `maxExclusive` (single-arg overload) or `maxExclusive < minInclusive` (two-arg overloads), and return `min` when `min == max`.
- **Mutable struct, by-ref helpers.** The generator mutates in place, so the extension helpers take it `ref this` — call them on a variable or field, not a temporary or readonly value. Copying a generator **forks** the stream (both copies then produce the same sequence).
- **`Pcg32` specifics.** Its native output is 32-bit (`NextUInt32`); `NextUInt64` concatenates two successive 32-bit draws. The `(seed, sequence)` constructor selects an independent stream. (Through the generic `RandomSourceExtensions.NextUInt32`, the high 32 bits of `NextUInt64` are returned; call `Pcg32.NextUInt32` directly for the efficient native path.)
- **Not thread-safe and not cryptographic.** Share one generator per thread; for security-sensitive randomness use `System.Security.Cryptography.RandomNumberGenerator`.

All generators are AOT-safe (no reflection); `NextUInt64` and the derived helpers are `[MethodImpl(AggressiveInlining)]` and allocation-free.

## VarInt (span varint codec)

```csharp
namespace Celerity.Primitives;

public static class VarInt
{
    public const int MaxVarIntLength32 = 5;   // max bytes for a uint / zig-zagged int
    public const int MaxVarIntLength64 = 10;  // max bytes for a ulong / zig-zagged long

    // Size helpers
    public static int VarIntLength(uint  value);
    public static int VarIntLength(ulong value);
    public static int VarIntLength(int   value);   // zig-zag length
    public static int VarIntLength(long  value);   // zig-zag length

    // Unsigned LEB128
    public static bool TryWriteVarInt(Span<byte> destination, uint  value, out int bytesWritten);
    public static bool TryWriteVarInt(Span<byte> destination, ulong value, out int bytesWritten);
    public static bool TryReadVarInt(ReadOnlySpan<byte> source, out uint  value, out int bytesRead);
    public static bool TryReadVarInt(ReadOnlySpan<byte> source, out ulong value, out int bytesRead);

    // Signed (zig-zag + LEB128)
    public static bool TryWriteVarInt(Span<byte> destination, int   value, out int bytesWritten);
    public static bool TryWriteVarInt(Span<byte> destination, long  value, out int bytesWritten);
    public static bool TryReadVarInt(ReadOnlySpan<byte> source, out int   value, out int bytesRead);
    public static bool TryReadVarInt(ReadOnlySpan<byte> source, out long  value, out int bytesRead);

    // Zig-zag transforms (public, usable standalone)
    public static uint  ZigZagEncode(int  value);
    public static int   ZigZagDecode(uint value);
    public static ulong ZigZagEncode(long value);
    public static long  ZigZagDecode(ulong value);
}
```

A **span-based variable-length integer codec**: LEB128 for unsigned 32-/64-bit values and zig-zag + LEB128 for signed values, encoding directly over a caller-owned `Span<byte>` / `ReadOnlySpan<byte>` with **no stream and no allocation**.

A varint stores a small magnitude in fewer bytes than its fixed width: each byte carries 7 payload bits in its low bits and a continuation flag (`0x80`) in its high bit, least-significant group first (LEB128). It is the wire format Protocol Buffers, the .NET metadata tables, and most custom binary serializers use for length prefixes and field tags. The BCL exposes this **only** as `BinaryWriter.Write7BitEncodedInt` / `BinaryReader.Read7BitEncodedInt` — bound to a `Stream` and allocating a writer/reader (see [runtime #24473](https://github.com/dotnet/runtime/issues/24473)). `VarInt` fills the span gap.

**Workloads:** Protobuf-style wire codecs, custom binary serializers, packet builders, append-only logs, and no-stream / no-allocation encoding hot paths that own their byte buffer.

**Usage** — write into a buffer and advance by `bytesWritten`; read back and advance by `bytesRead`:

```csharp
using Celerity.Primitives;

Span<byte> buffer = stackalloc byte[VarInt.MaxVarIntLength64];

VarInt.TryWriteVarInt(buffer, 300u, out int n);   // n == 2, bytes 0xAC 0x02
VarInt.TryReadVarInt(buffer, out uint value, out int read);   // value == 300, read == 2

// A length prefix followed by a packed sequence of signed deltas:
int offset = 0;
foreach (int delta in deltas)
{
    VarInt.TryWriteVarInt(scratch.AsSpan(offset), delta, out int w);  // signed ⇒ zig-zag
    offset += w;
}
```

**Signed values are zig-zag encoded.** Two's-complement makes every negative number occupy the full width (a naive LEB128 of `-1` is always 10 bytes); zig-zag maps signed values to unsigned so small magnitudes of either sign stay short — `0 → 0, -1 → 1, 1 → 2, -2 → 3, …`. The `int` / `long` overloads apply it automatically; the `uint` / `ulong` overloads are plain LEB128.

**Contract and special cases:**

- **Overload selection is by argument type.** An untyped integer literal binds to the **signed** (zig-zag) overload, and the four `TryReadVarInt` overloads differ only in their `out` value type — so use an explicit type (`out uint v`), not `out var`, at the call site, and add a `u` / `UL` suffix when you want plain unsigned LEB128 for a literal.
- **Bounds-safe, never throws.** `TryWrite*` returns `false` and reports `0` bytes when the destination is too small (nothing partial is left behind); `TryRead*` returns `false` and reports `0` bytes when the source is truncated (a continuation bit with no further byte) or malformed (more than the maximum length for the width, or a final byte whose bits overflow the target width — e.g. an `int` whose 5th byte exceeds `0x0F`, or a `ulong` whose 10th byte exceeds `0x01`).
- **Length is exact.** `VarIntLength(value)` returns the same byte count a `TryWrite*` of that value reports — use it (or the `MaxVarIntLength32` / `MaxVarIntLength64` ceilings) to size buffers up front.
- **`0` encodes to a single `0x00` byte;** `uint.MaxValue` is 5 bytes, `ulong.MaxValue` is 10.

All methods are allocation-free and AOT-safe (no reflection); the transforms and length helpers are `[MethodImpl(AggressiveInlining)]`.

## CountDigits / Log10 (base-10 digit count)

```csharp
namespace Celerity.Primitives;

public static class FastUtils
{
    public static int CountDigits(uint  value);   // 1 .. 10
    public static int CountDigits(ulong value);   // 1 .. 20
    public static int CountDigits(int   value);   // magnitude, sign excluded (1 .. 10)
    public static int CountDigits(long  value);   // magnitude, sign excluded (1 .. 19)

    public static int Log10(uint  value);         // floor(log10(value)); 0 for value 0
    public static int Log10(ulong value);
}
```

The number of **decimal digits** of an integer — exactly what you need to size a buffer before `TryFormat`, align a fixed-width numeric column, or pre-measure log / CSV / JSON output. The BCL has a fast LZCNT-based counter (`System.Buffers.Text.FormattingHelpers.CountDigits`) but it is `internal`; the only public base-10 log is the floating-point `Math.Log10`, which is slower and **mis-rounds at exact powers of ten** (rounding can make `(int)Math.Log10(1000)` come out as `2`). `FastUtils.CountDigits` exposes an exact integer counter and `Log10` its companion.

The 32-bit path is Lemire's [digit-count algorithm](https://lemire.me/blog/2021/06/03/computing-the-number-of-digits-of-an-integer-even-faster/): a single `Log2` (one LZCNT) indexes a 32-entry magic table whose value, added to `value` and shifted right by 32, is the digit count — **no branches, no division**. The 64-bit path reduces the value to its top decimal group with at most one division and finishes with a short comparison ladder. (We deliberately do not try to beat `int.ToString` / `TryFormat` itself — those are already optimized — only the digit-count primitive the BCL keeps internal.)

**Workloads:** buffer sizing before `TryFormat`, fixed-width column alignment, log / CSV / JSON number formatting.

**Usage** — size a span, then format into it:

```csharp
using Celerity;

int width = FastUtils.CountDigits(value);             // e.g. 4 for 1234
Span<char> buffer = stackalloc char[width];
value.TryFormat(buffer, out _);

// Signed: the sign is not counted, so add one for the minus when negative.
int signedWidth = FastUtils.CountDigits(n) + (n < 0 ? 1 : 0);
```

**Contract and special cases:**

- **`0` counts as one digit** (`"0"` has length 1), so `CountDigits` returns `1` for `0` across every overload.
- **The signed overloads count the magnitude only** — the sign is excluded (`CountDigits(-5) == 1`). `int.MinValue` / `long.MinValue` are handled without overflow (the magnitude is computed by unsigned two's-complement negation, not `Math.Abs`).
- **`Log10(value)` is `CountDigits(value) - 1`** — exact at every power of ten, where the floating-point `Math.Log10` can round to the wrong side. `log10(0)` is mathematically undefined; `Log10(0)` returns `0` (treating `0` as a one-digit value). `Log10` is provided for the unsigned widths only.

All methods are allocation-free and AOT-safe (no reflection); the 32-bit and signed counters and both `Log10` overloads are `[MethodImpl(AggressiveInlining)]`.

## FastGuid (fast non-crypto GUID v4 / v7)

```csharp
namespace Celerity.Primitives;

public static class FastGuid
{
    // Non-cryptographic random version 4 (122 random bits), filled from a struct PRNG.
    public static Guid CreateVersion4<TRng>(ref TRng rng)                              where TRng : struct, IRandomSource;

    // RFC 9562 version 7: 48-bit Unix-ms timestamp (big-endian) + 74 random bits. Sortable.
    public static Guid CreateVersion7<TRng>(ref TRng rng, long unixTimeMilliseconds)   where TRng : struct, IRandomSource;
}

// Strictly monotonic version 7 — each call > the last, even within one millisecond.
public struct GuidV7Generator<TRng> where TRng : struct, IRandomSource
{
    public GuidV7Generator(TRng rng);
    public Guid Next();                              // stamps DateTimeOffset.UtcNow
    public Guid Next(long unixTimeMilliseconds);     // explicit / testable clock
}
```

Fast, allocation-free GUID generation that fills the random bits from a [struct PRNG](#struct-prngs) rather than the OS cryptographic RNG: a non-cryptographic **version 4** (fully random) and an RFC 9562 **version 7** (Unix-millisecond time-ordered, big-endian, sortable).

> [!WARNING]
> **`FastGuid` is NOT cryptographically secure.** Both versions draw from the supplied PRNG, not from `RandomNumberGenerator`. Use them for high-rate **trace / correlation / ephemeral IDs** where uniqueness — not unpredictability — matters. When an identifier must be **unguessable** (security tokens, password-reset links, session IDs), use `Guid.NewGuid()` or `System.Security.Cryptography.RandomNumberGenerator` instead.

**Workloads:** high-rate ID generation (distributed tracing, correlation IDs, ephemeral keys) and **sortable database primary keys** (version 7), where `Guid.NewGuid()`'s RNG-backed cost dominates and the IDs need not be unpredictable.

### Why version 7 is big-endian

RFC 9562 lays out version 7 with the 48-bit timestamp in the **most-significant bytes, network byte order**, so the GUID's canonical string form sorts in creation order — which keeps database B-tree indexes compact and inserts local (the whole point of a time-ordered UUID). .NET 9's `Guid.CreateVersion7` stores the timestamp in the mixed-endian in-memory `Guid` layout, which **scrambles the lexical / database sort order** versus the spec (community analysis measured ~35% larger indexes). `FastGuid.CreateVersion7` emits the on-the-wire big-endian layout, so `ToString()` ordering matches time ordering:

```csharp
using Celerity.Primitives;

var rng = new Xoshiro256StarStar(seed: 12345);

Guid trace = FastGuid.CreateVersion4(ref rng);              // random, non-crypto
Guid key   = FastGuid.CreateVersion7(ref rng, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
// key.ToString() begins with the hex of the timestamp → lexical sort == time sort
```

### Strict monotonicity within a millisecond

The stateless `CreateVersion7` orders by timestamp **across** milliseconds, but within a single millisecond it orders only by random bits — so a rapid burst is sortable but not strictly increasing. `GuidV7Generator<TRng>` closes that gap with RFC 9562's monotonic-counter method: it keeps the last timestamp and a 12-bit counter in the `rand_a` field, advancing the counter when the clock has not moved so every GUID in a same-millisecond run is **strictly greater** than the previous one. If the counter is exhausted inside one millisecond (more than ~4096 IDs) it borrows from the next millisecond, preserving monotonicity at the cost of letting the embedded timestamp run a hair ahead of the wall clock. The 62-bit `rand_b` tail stays random on every draw, so independent generators do not collide.

```csharp
var gen = new GuidV7Generator<Xoshiro256StarStar>(new Xoshiro256StarStar(seed: 1));

Guid a = gen.Next();   // ↘ strictly increasing
Guid b = gen.Next();   //   even if a and b land in the same millisecond,
Guid c = gen.Next();   //   string.CompareOrdinal(a, b) < 0 < c
```

**Contract and special cases:**

- **Not cryptographically secure; not thread-safe.** Like the [struct PRNGs](#struct-prngs), `GuidV7Generator` is a mutable `struct` — call `Next` on a variable or field, use one generator per thread, and do not copy it (a copy forks both the PRNG stream and the monotonic counter).
- **Version / variant bits are always correct.** Version 4 sets the version nibble to `4`; version 7 to `7`; both set the RFC 4122 variant (`10xx`) in the high bits of byte 8.
- **The version 7 timestamp uses the low 48 bits** of the supplied `unixTimeMilliseconds` (valid through year 10889). The big-endian placement is what makes the canonical string sortable.
- **Deterministic from the seed.** A seeded PRNG makes `CreateVersion4` / `CreateVersion7` (and the generator, for a fixed timestamp stream) reproduce the same GUID sequence — useful for tests and golden fixtures.

All methods are allocation-free and AOT-safe (no reflection); `CreateVersion4` / `CreateVersion7` are `[MethodImpl(AggressiveInlining)]`.

## Alignment helpers (AlignUp / AlignDown / IsAligned)

```csharp
namespace Celerity;

public static class FastUtils
{
    public static int   AlignUp  (int   value, int   alignment);
    public static int   AlignDown(int   value, int   alignment);
    public static bool  IsAligned(int   value, int   alignment);

    public static long  AlignUp  (long  value, long  alignment);
    public static long  AlignDown(long  value, long  alignment);
    public static bool  IsAligned(long  value, long  alignment);

    public static nuint AlignUp  (nuint value, nuint alignment);   // pointer-sized (addresses)
    public static nuint AlignDown(nuint value, nuint alignment);
    public static bool  IsAligned(nuint value, nuint alignment);
}
```

Round a size or a (pointer-sized) address to a **power-of-two boundary** — what you need when sub-allocating from a buffer, padding a struct stride to a SIMD width, finding the start of the cache line / page a pointer sits in, or sizing a backing array in machine words. The arithmetic is the classic mask trick (`(v + (a - 1)) & ~(a - 1)`), which the BCL keeps in an `internal` `Align` helper rather than expose; `FastUtils` exposes it for the common widths (`int` / `long` for sizes and offsets, `nuint` for raw addresses).

**Workloads:** buffer sub-allocation, SIMD / cache-line / page alignment, struct-stride padding, machine-word array sizing.

**Usage:**

```csharp
using Celerity.Primitives;

int padded   = FastUtils.AlignUp(length, 16);          // round a byte count up to a 16-byte boundary
nuint lineStart = FastUtils.AlignDown(address, 64);    // start of the cache line containing `address`
if (FastUtils.IsAligned(ptr, 32)) { /* AVX2-safe load */ }
```

**Contract and special cases:**

- **`alignment` must be a power of two** (`1, 2, 4, 8, …`). A non-power-of-two — including `0` and (for the signed overloads) a negative — throws `ArgumentOutOfRangeException`. The check is `BitOperations.IsPow2`.
- **An already-aligned value is returned unchanged** by both `AlignUp` and `AlignDown`; `AlignDown <= value <= AlignUp` always holds, and both results satisfy `IsAligned`.
- **`AlignUp` can overflow** when `value` is within `alignment - 1` of the type's maximum (wrapping for `nuint`, going negative for the signed widths), exactly as the underlying `+` would — guard at the call site if that range is reachable.

All methods are allocation-free, `[MethodImpl(AggressiveInlining)]`, and AOT-safe.

## SpanBits (span bit-packing)

```csharp
namespace Celerity.Primitives;

public static class SpanBits
{
    public static int  WordCount(int bitCount);                            // ceil(bitCount / 64)

    public static bool Get  (ReadOnlySpan<ulong> bits, int index);
    public static void Set  (Span<ulong> bits, int index);                 // → 1
    public static void Clear(Span<ulong> bits, int index);                 // → 0
    public static bool Flip (Span<ulong> bits, int index);                 // toggles; returns new value

    public static int  PopCount  (ReadOnlySpan<ulong> bits);               // total set bits (POPCNT)
    public static int  NextSetBit(ReadOnlySpan<ulong> bits, int fromIndex); // forward scan; -1 if none
}
```

Bit get / set / clear / flip / population-count / next-set-bit **scan** over **caller-owned** bit storage — a `Span<ulong>` / `ReadOnlySpan<ulong>` of 64-bit words — with no allocation and no heap object. Each bit lives in word `index / 64` at position `index % 64` (least-significant bit first), so a span of length `n` holds `64·n` bits indexed `[0, 64·n)`; size the span from a bit count with `WordCount`. `PopCount` / `NextSetBit` use the hardware `POPCNT` / `TZCNT` (via `BitOperations`) and skip whole empty words.

**`SpanBits` is the non-owning counterpart to [`BitSet`](collections.md#bitset).** `BitSet` is a length-tracking collection that **owns** its backing array and offers bulk boolean ops and enumeration; `SpanBits` owns nothing — it is a thin set of static operations over memory you already manage (a `stackalloc` buffer, a slice of a larger array, a pooled / rented buffer, or memory mapped from elsewhere). Reach for `SpanBits` when you are already managing the storage and only need the bit arithmetic; reach for `BitSet` when you want a self-contained bit vector. (`System.Collections.BitArray`, the other BCL option, is a heap class with no span access, no population count, and no set-bit scan.)

**Workloads:** bitmaps over a `stackalloc` / pooled buffer, free-slot tracking in an allocator, visited-set / marker bits during a traversal, packed flags inside a larger structure.

**Usage** — a 200-bit scratch bitmap on the stack:

```csharp
using Celerity.Primitives;

Span<ulong> bits = stackalloc ulong[SpanBits.WordCount(200)];  // 4 words = 256 bits of capacity

SpanBits.Set(bits, 5);
SpanBits.Set(bits, 130);

int set = SpanBits.PopCount(bits);                            // 2

for (int i = SpanBits.NextSetBit(bits, 0); i >= 0; i = SpanBits.NextSetBit(bits, i + 1))
{
    // visits 5, then 130
}
```

**Contract and special cases:**

- **`WordCount(bitCount)` is `ceil(bitCount / 64)`** (`0` for `0`); it throws `ArgumentOutOfRangeException` for a negative count.
- **The single-bit operations index the span directly**, so an `index` outside `[0, 64·bits.Length)` throws `IndexOutOfRangeException` from the underlying span access — these helpers do not silently mask or grow the storage.
- **`NextSetBit` is inclusive of `fromIndex`** and returns `-1` when no bit at or after it is set. A `fromIndex < 0` is treated as `0`; a `fromIndex` at or beyond the end yields `-1`. Feed the previous result `+ 1` back in to iterate set bits in order.
- **`Flip` returns the bit's new value** after toggling (matching `BitSet.Flip`).

All methods are allocation-free; the single-bit operations and `WordCount` are `[MethodImpl(AggressiveInlining)]`, and the whole type is AOT-safe.

## SimdReductions (fused / specialized SIMD reductions)

```csharp
namespace Celerity.Primitives;

public static class SimdReductions
{
    public static (int    Min, int    Max) MinMax(ReadOnlySpan<int>    values);
    public static (long   Min, long   Max) MinMax(ReadOnlySpan<long>   values);
    public static (uint   Min, uint   Max) MinMax(ReadOnlySpan<uint>   values);
    public static (ulong  Min, ulong  Max) MinMax(ReadOnlySpan<ulong>  values);

    public static int CheckedSum(ReadOnlySpan<int> values);   // throws OverflowException on overflow
}
```

`System.Numerics.Tensors.TensorPrimitives` is now generic over `INumber<T>` and SIMD/AVX-512 accelerated for `Sum` / `Min` / `Max` / `Dot` / `IndexOfMax` and friends — **use it for those.** `SimdReductions` ships only the two reductions that fill a genuine gap **TensorPrimitives does not cover**, each with a measured BCL-beating workload (the guiding rule):

- **`MinMax` — the fused single pass.** Computing both extrema as `TensorPrimitives.Min(s)` *and* `TensorPrimitives.Max(s)` reads the span **twice**; `MinMax` folds two running vectors in a **single pass**, so it does the same work for roughly half the memory traffic. This is a **memory-bandwidth win, not a per-element-kernel win**: on a large, out-of-cache span (1,000,000 `int`) the fused pass measures **~1.8× faster** than the two-pass composition, while on a small in-cache span (1,024 `int`) the BCL's heavily-tuned AVX-512 kernels make the two passes a wash (the fused pass is ~15% slower). Reach for it when the span is large enough to spill out of cache.
- **`CheckedSum` — the safe, fast integer sum.** `TensorPrimitives.Sum` **wraps silently** on integer overflow, and the only safe BCL alternative — a scalar `checked` loop — cannot vectorize (the per-element overflow check has a side effect). `CheckedSum` widens each `int` lane to `long` so the SIMD accumulation provably cannot overflow for any reachable span, and range-checks **only the final narrowing** to `int`, throwing `OverflowException` on a true overflow. It measures **~4.6× faster** than the scalar `checked` loop at 1,024 elements (~3.2× at 1,000,000) — i.e. it sits between the slow-but-safe scalar loop and the fast-but-unsafe `TensorPrimitives.Sum`, beating the only *correct* option on speed.

**Workloads:** min+max range scans over large numeric arrays (audio/sensor sample ranges, bounding intervals, normalization passes); overflow-safe summation of untrusted or large integer data where a silent wrap would be a correctness bug.

**Usage:**

```csharp
using Celerity.Primitives;

ReadOnlySpan<int> samples = GetSensorWindow();

var (lo, hi) = SimdReductions.MinMax(samples);   // both extrema, one pass over the data
int total    = SimdReductions.CheckedSum(samples); // throws OverflowException rather than wrapping
```

**Contract and special cases:**

- **`MinMax` throws `ArgumentException` on an empty span** — a minimum and maximum are undefined with no elements. It is exact for all four integer element types (no NaN ambiguity); `float` / `double` are intentionally out of scope because correct NaN-propagation semantics deserve an explicit policy rather than whatever `Vector.Min` / `Vector.Max` happen to do (use `TensorPrimitives.Min` / `Max`, which define their NaN behaviour, and pay the second pass).
- **`CheckedSum` returns `0` for an empty span** (the sum of no elements), and returns the **exact** mathematical sum whenever it does not throw — the widened SIMD accumulation never overflows, so a returned value is never a wrapped one.
- Both use portable `Vector<T>` with a scalar tail, so they accelerate where SIMD is available and stay correct (and allocation-free) everywhere else.

All methods are allocation-free and AOT-safe. (The #197 spike's third candidate, an integer **histogram / bincount**, was evaluated and **not** shipped: its only BCL alternative is LINQ `GroupBy().Count()`, the win is purely allocation avoidance, and a one-line `counts[values[i]]++` loop covers it without a named primitive — the scatter pattern does not vectorize portably.)
