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

### MaxPowerOfTwoCapacity / DoubleCapacity / TryDoubleCapacity

```csharp
public const int MaxPowerOfTwoCapacity = 1 << 30;   // 1,073,741,824
public static int  DoubleCapacity(int currentCapacity);
public static bool TryDoubleCapacity(int currentCapacity, out int doubled);
```

`MaxPowerOfTwoCapacity` is the hard ceiling on the backing-array size of every open-addressed Celerity collection: the next power of two (`2^31`) overflows a signed `int`, so a table can never grow past `2^30` slots. `NextPowerOfTwo` already caps its result here.

`DoubleCapacity` is the guarded `* 2` the collections use in their `Resize()` paths. It returns `currentCapacity * 2`, but throws `InvalidOperationException` when `currentCapacity` is already at or above `MaxPowerOfTwoCapacity` rather than computing `2^31` and overflowing to a negative size (which would corrupt the `newSize - 1` slot mask).

`TryDoubleCapacity` is the non-throwing sibling for loops that *probe* a sequence of candidate sizes and need to halt cleanly at the ceiling rather than throw — for example the frozen collections' perfect-hash build search. It sets `doubled = currentCapacity * 2` and returns `true` below the ceiling, or leaves `doubled` unchanged and returns `false` at/above it. Advancing such a loop with a bare `size <<= 1` is unsafe: once `size` reaches `2^30` the shift wraps to a negative value that still passes a `size <= 2^30` guard, so the next iteration allocates a negative-length array and throws `OverflowException` (the bug fixed in #228).

**Special cases:**

- `currentCapacity < 2^30` returns `currentCapacity * 2` (`TryDoubleCapacity` returns `true`).
- `currentCapacity == 2^29` returns exactly `2^30` (the last legal growth).
- `currentCapacity >= 2^30`: `DoubleCapacity` throws `InvalidOperationException` ("cannot grow beyond its maximum capacity"); `TryDoubleCapacity` returns `false` and leaves `doubled` at `currentCapacity`.

**Capacity limit.** In practice this ceiling is only reached by a single collection holding on the order of ~800M live entries (8+ GB of backing arrays at the default 0.75 load factor). At that point the collection throws a clear capacity error on the *next* growth instead of silently corrupting its state — Celerity collections are not designed to hold more than `2^30` slots; partition across multiple instances if you need more.

### MinTableSizeFor

```csharp
public static int MinTableSizeFor(int entryCount, float loadFactor)
```

The sizing primitive behind the collections' `EnsureCapacity` / `TrimExcess` methods: it returns the smallest power-of-two table size whose truncated load-factor threshold (`(int)(size * loadFactor)`) admits `entryCount` entries — i.e. the size a hash table must reach to hold that many entries without resizing. `NextPowerOfTwo(entryCount)` alone only guarantees `size >= entryCount`, not `threshold >= entryCount`, so this doubles past the rounding shortfall until the threshold fits. For a load factor strictly between 0 and 1 the result always strictly exceeds `entryCount`, so the table is guaranteed at least one vacant slot.

**Special cases:**

- `entryCount <= 0` returns the minimum table size of `1`.
- A `loadFactor` outside `(0, 1)` is clamped into that range rather than dividing by a non-positive or `>= 1` factor (so the helper never loops forever or throws).
- An `entryCount` too large for any power-of-two table at the given load factor saturates at `MaxPowerOfTwoCapacity` rather than overflowing.

```csharp
int size = FastUtils.MinTableSizeFor(1000, 0.75f);   // 2048: (int)(2048 * 0.75) == 1536 >= 1000
```

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

## BitWriter / BitReader (sequential sub-byte bit I/O)

```csharp
namespace Celerity.Primitives;

public ref struct BitWriter
{
    public BitWriter(Span<byte> destination);

    public readonly int  BitsWritten   { get; }   // current cursor position
    public readonly int  BytesWritten  { get; }   // ceil(BitsWritten / 8)
    public readonly long CapacityInBits { get; }
    public readonly long BitsRemaining  { get; }

    public static int ByteCount(int bitCount);     // ceil(bitCount / 8), for buffer sizing

    public bool TryWriteBit (bool value);
    public bool TryWriteBits(ulong value, int bitCount);   // bitCount in [0, 64]
}

public ref struct BitReader
{
    public BitReader(ReadOnlySpan<byte> source);

    public readonly int  BitsRead      { get; }
    public readonly long CapacityInBits { get; }
    public readonly long BitsRemaining  { get; }

    public bool TryReadBit (out bool value);
    public bool TryReadBits(int bitCount, out ulong value);   // bitCount in [0, 64]
}
```

A **sequential, bounds-safe pair of cursors for packing and unpacking arbitrary-width bit fields** over a caller-owned `Span<byte>` / `ReadOnlySpan<byte>`, with **no stream and no allocation**. Where a field's width is not a whole byte — a 3-bit flag group, a 12-bit sample, a 20-bit offset — `BitWriter` appends it with a single multi-bit write and `BitReader` reads it back, so a record of odd-width fields occupies exactly `ceil(total_bits / 8)` bytes instead of one byte per field.

This is the **sequential, sub-byte counterpart** to the other bit primitives: [`VarInt`](#varint-span-varint-codec) is byte-granular (whole-byte variable-length integers), and [`SpanBits`](#spanbits-span-bit-packing) is random-access (get/set one bit at a fixed index over a `Span<ulong>`). `BitWriter` / `BitReader` append and consume **whole multi-bit fields at a moving cursor**. The BCL has no equivalent: `System.Collections.BitArray` is a heap object that sets one bit at a time and cannot append a multi-bit field, and `System.Buffers.Binary.BinaryPrimitives` is byte-granular.

**Bit order is LSB-first (little-endian bits):** bit position `p` is byte `p / 8`, bit `p % 8` counting from the least-significant bit; a field's least-significant bit sits at the current position and higher bits follow toward higher positions, spilling into the next byte as needed. This is the convention DEFLATE and most bit-packed codecs use, and the writer and reader are exact inverses when fields are read back in the **same order and widths** they were written.

**Workloads:** bit-packed wire protocols and packet headers, compression bitstreams, packed columnar / bitmap-index encodings, and fixed-width-field records where a byte-per-field layout would waste space.

**Usage** — pack a record of odd-width fields, then read it back:

```csharp
using Celerity.Primitives;

Span<byte> buffer = stackalloc byte[BitWriter.ByteCount(3 + 12 + 20)];  // 5 bytes

var writer = new BitWriter(buffer);
writer.TryWriteBits(5, 3);        // a 3-bit flag group
writer.TryWriteBits(3000, 12);    // a 12-bit sample
writer.TryWriteBits(0xABCDE, 20); // a 20-bit offset
int packedBytes = writer.BytesWritten;   // 5

var reader = new BitReader(buffer);
reader.TryReadBits(3,  out ulong flags);   // 5
reader.TryReadBits(12, out ulong sample);  // 3000
reader.TryReadBits(20, out ulong offset);  // 0xABCDE
```

**Contract and special cases:**

- **`ByteCount(bitCount)` is `ceil(bitCount / 8)`** (`0` for `0`); it throws `ArgumentOutOfRangeException` for a negative count.
- **Every `TryWrite` / `TryRead` is bounds-safe:** it returns `false` and leaves the cursor and buffer **unchanged** when the field would not fit (or the source has too few bits left), so a partial field is never written or consumed. Check the return value between fields, or size the buffer up front with `ByteCount`.
- **Only the low `bitCount` bits of a written value are stored** — any higher bits are ignored, so an out-of-range value can never corrupt a following field. A read yields the value in its low `bitCount` bits (higher bits `0`).
- **`bitCount` must be in `[0, 64]`**; a value outside that range throws `ArgumentOutOfRangeException`. A `0`-bit field is a no-op success (nothing written / value `0`), so a computed width of `0` needs no special-casing.
- **Each field overwrites exactly the bits it occupies** (it clears them before depositing), so the destination need not be pre-zeroed and a field can be rewritten.
- The types are mutable `ref struct` cursors — construct one over the buffer and write/read fields in sequence. Use them as locals; pass by `ref` if a helper method must advance the same cursor. As `ref struct`s they cannot be boxed, stored on the heap, or used across `await` / `yield` (the same constraints as `Span<T>` itself).

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

## Branchless (guaranteed branch-free conditional select)

```csharp
namespace Celerity.Primitives;

public static class Branchless
{
    // scalar: condition ? whenTrue : whenFalse, with no conditional jump
    public static int    Select(bool condition, int    whenTrue, int    whenFalse);
    public static long   Select(bool condition, long   whenTrue, long   whenFalse);
    public static uint   Select(bool condition, uint   whenTrue, uint   whenFalse);
    public static ulong  Select(bool condition, ulong  whenTrue, ulong  whenFalse);
    public static float  Select(bool condition, float  whenTrue, float  whenFalse);   // bit-exact (signed zero / NaN)
    public static double Select(bool condition, double whenTrue, double whenFalse);   // bit-exact

    // bulk per-element blend: destination[i] = condition[i] ? whenTrue[i] : whenFalse[i]
    public static void Select(ReadOnlySpan<bool> condition, ReadOnlySpan<int>    whenTrue, ReadOnlySpan<int>    whenFalse, Span<int>    destination);
    public static void Select(ReadOnlySpan<bool> condition, ReadOnlySpan<long>   whenTrue, ReadOnlySpan<long>   whenFalse, Span<long>   destination);
    public static void Select(ReadOnlySpan<bool> condition, ReadOnlySpan<float>  whenTrue, ReadOnlySpan<float>  whenFalse, Span<float>  destination);
    public static void Select(ReadOnlySpan<bool> condition, ReadOnlySpan<double> whenTrue, ReadOnlySpan<double> whenFalse, Span<double> destination);
}
```

The JIT already lowers the recognised `cmov` idioms — `Math.Min` / `Max` / `Abs` / `Clamp` on integers — to branchless instructions, so **use those when they fit.** What it does *not* reliably do is "if-convert" a general data-dependent `condition ? a : b`: in a loop over an **unpredictable** `bool`, RyuJIT emits a real conditional branch, and on data the CPU cannot predict the misprediction penalty dominates the loop. `Branchless.Select` removes the branch.

- **The mechanism.** A `bool` is reinterpreted to its `0`/`1` byte and negated to an all-zero / all-one mask, then `whenFalse ^ ((whenTrue ^ whenFalse) & mask)` picks a value with pure arithmetic — no comparison, no jump, a fixed data dependency the CPU never mispredicts. The float/double overloads reinterpret to integer bit-patterns, select, and reinterpret back, so the chosen value is returned **bit-exactly** (signed zero and `NaN` payloads are preserved verbatim). The bulk span overloads apply the same straight-line arithmetic per element; because the body has no branch, the JIT **auto-vectorises** it, so the blend wins at array scale too.
- **The measured win.** The #198 spike timed a per-element blend over a 1,000,000-element `int` array with a 50/50 **unpredictable** condition: the branchy ternary ran at ~3.0 ms while the branch-free blend ran at ~0.5 ms — **~6× faster**, the textbook branch-misprediction signature. That reproducible end-to-end win is why this primitive ships (the spike's "ship only if it wins" bar).

**Workloads:** tight numeric / data-processing loops where a per-element decision depends on **unpredictable** data — masking / blending two arrays, conditional accumulation, sorting-network compare-exchange, clamping with a data-dependent bound, branch-free state machines.

**Usage:**

```csharp
using Celerity.Primitives;

// scalar — inlines branch-free into the caller's loop
int clamped = Branchless.Select(value > limit, limit, value);

// bulk blend — destination[i] = mask[i] ? a[i] : b[i], no per-element branch
Branchless.Select(mask, a, b, destination);
```

**Contract and special cases:**

- **Branchless select is for the *unpredictable-condition* case only.** When the branch is well-predicted (a loop-invariant flag, a monotone threshold), the predicted branch is essentially free and a plain ternary is just as fast or faster — reach for `Branchless.Select` only when the condition is genuinely data-dependent and random. The benchmark's `Predictable` arm is the documented control where the branchless version wins little or nothing.
- **`condition` must be an ordinary `bool`** — value `0` (`false`) or `1` (`true`), as produced by every C# comparison and the runtime. A `bool` forged from an out-of-range byte via unsafe reinterpretation is outside the contract.
- **The bulk overloads throw `ArgumentException`** unless `condition`, `whenTrue`, `whenFalse`, and `destination` all have the same length; `destination` may alias `whenTrue` or `whenFalse` (each element is read before it is written).

The scalar overloads are aggressively inlined so they stay branch-free at the call site. Every overload is allocation-free and AOT-safe. (The #198 spike's premise — that `Math.Min`/`Max`/`Abs`/`Clamp` already get `cmov` — is why those are deliberately **not** re-shipped here: use the BCL for them.)

## SortedSpan (sorted-span set algebra)

```csharp
namespace Celerity.Primitives;

public static class SortedSpan
{
    // T : IComparisonOperators<T, T, bool> — int, long, uint, ulong, and the other primitives
    public static int  Intersect<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> destination);
    public static int  Union<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> destination);
    public static int  Except<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> destination);
    public static int  IntersectCount<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b);   // allocation-free
    public static bool Overlaps<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b);         // allocation-free
}
```

> **Every input span must be sorted in ascending order. Sorted by construction, or this is worthless.** The whole point is that ordering lets each element be touched once; unsorted input silently produces a **wrong answer**, not an error. Debug builds assert the precondition (an `O(n)` scan per call); Release builds do not check it at all, deliberately — a Release-mode check would cost exactly what the algorithm saves.

**The BCL has no set algebra over spans.** `MemoryExtensions` gained `CommonPrefixLength`, `Split` and `SearchValues`, but nothing that intersects two ranges; `TensorPrimitives` has none either, and .NET 10 added none. The two things a developer writes today are `HashSet<T>.IntersectWith` (allocate a table, then hash and probe every element of one side) and LINQ `Intersect` (a `Set<T>` plus an iterator chain) — **neither exploits sortedness at all**. `SortedSpan` walks both sides in order instead: sequential, prefetch-friendly reads, and results written straight into caller-owned memory.

**The measured win** (1,000,000 x 1,000,000 sorted, distinct `int` spans over a 2,000,000 universe, ~50% overlap):

| Operation | `HashSet<int>` | LINQ | `SortedSpan` | Speedup |
| --- | --- | --- | --- | --- |
| Intersect | 25.7 ms | 34.8 ms | **6.1 ms** | **4.2x** / 5.7x vs LINQ |
| Union | 26.5 ms | — | **6.6 ms** | **4.0x** |
| Except | 17.6 ms | — | **6.4 ms** | **2.8x** |
| Count common | 15.9 ms | — | **6.1 ms** | **2.6x** |

Allocation over the same intersect: **17.9 MB** for the `HashSet` form, **17.7 MB** for LINQ, **0 bytes** for `SortedSpan`.

**Galloping** turns the asymmetric posting-list shape into the real headline. When one side is at least **32x** the length of the other, `Intersect` / `Except` / `IntersectCount` / `Overlaps` abandon the linear merge for exponential ("galloping") search of the long side — `O(k log m)` instead of `O(n + m)`. At **1,000 against 10,000,000**, intersecting takes **0.37 ms** where `HashSet` takes 94.3 ms and LINQ 88.9 ms: a **257x** speedup, and **422x** for `IntersectCount`. `Union` deliberately has **no** galloping path — its result is at least as long as the longer input, so writing those elements out dominates and skipping comparisons cannot help — and `Except` gallops only when the *subtrahend* is the long side, since when the left side is the long one every element of it is a candidate output and the merge is already proportional to the result.

**Workloads:** intersecting sorted ID / row-id / posting lists (inverted indexes, cohort and audience intersection, join-key pre-filters), diffing two sorted key sets, and any "do these two sorted ranges share anything" test. It composes directly with [`CompressedIntSet`](collections.md#compressedintset) and [`BTreeSet`](collections.md#btreesett-tcomparer): both enumerate in ascending order, so their contents feed straight in without a re-sort.

**Usage:**

```csharp
using Celerity.Primitives;

ReadOnlySpan<int> current = LoadSortedIds();   // sorted ascending, by construction
ReadOnlySpan<int> incoming = LoadSortedIds();

// Intersection into a caller-owned buffer: min(a.Length, b.Length) is always enough.
Span<int> buffer = stackalloc int[Math.Min(current.Length, incoming.Length)];
int count = SortedSpan.Intersect(current, incoming, buffer);
foreach (int id in buffer[..count]) { /* ... */ }

// The two questions that need no buffer at all, and allocate nothing.
int shared = SortedSpan.IntersectCount(current, incoming);
bool any = SortedSpan.Overlaps(current, incoming);
```

**Contract and special cases:**

- **Set semantics.** Repeated equal values in an input are treated as one element, so every result is **strictly** ascending — the same answer the equivalent `HashSet<T>` operation gives. This is what makes the differential test against a `HashSet<T>` oracle meaningful.
- **Destination sizing.** `min(a.Length, b.Length)` always suffices for `Intersect`, `a.Length` for `Except`, and `a.Length + b.Length` for `Union`. A destination that is too short raises `ArgumentException`; because the shortfall is discovered while writing rather than up front, the destination's contents are then undefined. `IntersectCount` and `Overlaps` take no destination.
- **The destination must not overlap either input.** The merge writes its result while it is still reading both sources, so an aliasing buffer can overwrite elements that have not been consumed yet — silently, the same way unsorted input does. There is no in-place mode. Like the ordering precondition, this is asserted in Debug builds (via `MemoryExtensions.Overlaps`) and unchecked in Release.
- **Empty inputs** are valid: an intersection with an empty side is empty, a union with one is the other side (de-duplicated), and `a \ {}` is `a` de-duplicated.
- **Element type.** The constraint is `IComparisonOperators<T, T, bool>`, so the merge compares with the type's own `<` and `==`. For the primitive integer types the JIT specializes the generic per value type and each comparison becomes a single instruction — which is why there are no hand-written `int` / `long` / `uint` / `ulong` overloads. Floating-point types compile but are not the intended use: `NaN` compares `false` against everything, so a span containing one is not ordered under `<` and violates the precondition.
- **No vectorized path ships.** A `Vector256` merge was scoped as opt-in only if it beat the scalar merge by >=25% on the 1M x 1M case; the scalar merge already runs at ~6 ms there (memory-bound, one sequential pass over both inputs), and merge is branch-heavy enough that vectorizing it is frequently a wash. The scalar path is the whole implementation.

## MortonCurve / HilbertCurve (space-filling curves)

```csharp
namespace Celerity.Primitives;

public static class MortonCurve
{
    public const uint MaxCoordinate3D = (1u << 21) - 1;   // 2,097,151

    public static ulong                  Encode2D(uint x, uint y);          // 32 bits per axis, bijective
    public static (uint X, uint Y)       Decode2D(ulong code);
    public static ulong                  Encode3D(uint x, uint y, uint z);  // 21 bits per axis, 63-bit code
    public static (uint X, uint Y, uint Z) Decode3D(ulong code);
}

public static class HilbertCurve
{
    public const uint MaxCoordinate3D = MortonCurve.MaxCoordinate3D;

    public static ulong                  Encode2D(uint x, uint y);
    public static (uint X, uint Y)       Decode2D(ulong index);
    public static ulong                  Encode3D(uint x, uint y, uint z);
    public static (uint X, uint Y, uint Z) Decode3D(ulong index);
}
```

A space-filling curve maps a 2-D or 3-D coordinate to **one integer** such that points near each other in space get numbers near each other in the ordering. That is what lets a one-dimensional structure answer a spatially local question: sort points by their curve index and a plain sorted array, a `BTreeSet<T>` or a `SortedSpan` becomes a cache-coherent spatial container; the same order is the standard packing order for bulk-loading a bounding-volume index, and the standard way to build a tile / cell key that survives a sort or a hash-partition.

**The BCL has no bit-interleave.** `BitOperations` ships popcount, leading/trailing-zero counts and rotates, but nothing that scatters a value's bits across a mask, so a caller who wants a Morton code writes the magic-number sequence themselves. There is no Hilbert anything.

### Which curve

|  | `MortonCurve` (Z-order) | `HilbertCurve` |
| --- | --- | --- |
| **Aligned `2^k` cell is a contiguous index range** | yes | yes |
| **Consecutive indices are neighbouring cells** | **no** — the Z pattern jumps a quadrant width at every crossing | **yes**, at every scale |
| **Index rises with one axis while the other is fixed** | **yes** | no — the curve folds back on itself |
| **Cost** | a straight-line bit spread | a loop over the bit levels |

Reach for **Morton** when you want *a* locality-preserving sort key, a cell id, or a packing order — it is the cheaper of the two and the right default. Reach for **Hilbert** when the ordering itself backs a range query, because a contiguous run of Hilbert indices is a compact, connected region of the plane rather than a set of scattered strips.

### Precision

2-D is lossless over the whole `uint` range on both axes: 32 bits per axis fill the 64-bit key exactly, and both `Encode2D` methods are **bijections** onto `ulong` — every key decodes, and re-encodes to itself. 3-D packs three axes into the same 64 bits, so it is capped at 21 bits per axis (`MaxCoordinate3D`) and produces a 63-bit key with bit 63 always clear. `Encode3D` **throws `ArgumentOutOfRangeException`** on a larger coordinate rather than keeping the low 21 bits, because silently masking would round-trip a *different* point back to the caller. `Decode3D` ignores bit 63, so every `ulong` decodes.

The curves are fixed-precision — there is no order parameter. You do not need one to work in a smaller universe: both curves are self-similar, so coordinates confined to an aligned `2^k`-sided sub-square still land in one contiguous run of indices. What you do not get is agreement with an independently-computed order-`k` curve, since the sub-square is traversed in whatever rotation the enclosing curve reaches it in.

### Why there is no BMI2 path

x86 `BMI2` computes a Morton code in two instructions — `PDEP` to scatter each axis across its mask, `PEXT` to gather it back — and `SpaceFillingCurveBenchmark` carries that implementation as its own arm. It is **3.8× faster than the portable spread** that ships, measured, not assumed. It is still not what ships, for two reasons:

- **`Bmi2.IsSupported` is true on hardware where the instruction is a trap.** On AMD Zen 1 and Zen 2, `PDEP` / `PEXT` are microcoded and roughly an order of magnitude *slower* than the ten-instruction portable sequence. .NET exposes nothing that tells those parts apart from the ones where the intrinsic wins, so dispatching on `IsSupported` alone trades a win on one vendor for a large regression on another.
- **A hardware dispatch cannot be held to this repository's coverage gate.** Coverage is enforced at 100% line *and* branch across the shipping packages, and exactly one arm of an `IsSupported` branch is reachable on any given runner — so the other ships unexecuted by the suite. `[ExcludeFromCodeCoverage]` would be excluding the arithmetic itself rather than a guard, which is not what that attribute is for here.

The benchmark arm stays so the decision remains a measurement rather than an assumption, and can be revisited if either constraint changes. In the meantime the portable spread is already **9.8× the bit-by-bit loop** it replaces, at roughly 1.9 ns per conversion — small enough that in a real spatial workload the memory traffic, not the codec, is where the time goes. Which is the point of the next section.

### The measured payoff

A codec nobody builds on does not earn a place in the library, so `SpaceFillingCurveLocalityBenchmark` measures the thing a curve is actually *for*: laying spatially-near points near each other in memory. It buckets a point set into a uniform cell grid, then walks an aligned block of cells summing the weights of each cell's points and its eight neighbours. Four arms run that identical sweep over identical indirection; the **only** difference is the order the point records are stored in.

| Point layout | 2 M points (32 MB) | 100 k points (1.6 MB, in cache) |
| --- | ---: | ---: |
| Unsorted (insertion order) | 3.00 ms | 558 µs |
| Sorted by row-major cell id — *the hand-roll* | 1.95 ms (1.54×) | 547 µs (1.02×) |
| Sorted by `MortonCurve.Encode2D` | 1.69 ms (**1.77×**) | 541 µs (1.03×) |
| Sorted by `HilbertCurve.Encode2D` | 1.63 ms (**1.84×**) | 535 µs (1.04×) |

Against unsorted the curves win **1.8×**. Against row-major — the baseline that matters — they win **1.15×** (Morton) and **1.19×** (Hilbert). Hilbert's extra 4% over Morton is its better locality showing up exactly where the theory says it should, on the query that straddles a boundary.

Two things in that table matter more than the headline. The baseline it should be judged against is **row-major**, not unsorted — a caller who wants locality and has no curve sorts by `y * side + x`, which gets the horizontal neighbours for free and loses on the vertical ones, a whole grid row away. And the small size is a **control, not a footnote**: the win is a memory-hierarchy effect, so when the point set fits in cache there is nothing to win and the arms measure the same.

**Usage:**

```csharp
using Celerity.Primitives;

// A cell key that sorts spatially.
ulong code = MortonCurve.Encode2D(cellX, cellY);
var (x, y) = MortonCurve.Decode2D(code);

// Lay a point set out along the curve so neighbours are neighbours in memory.
ulong[] keys = points.Select(p => MortonCurve.Encode2D(Quantize(p.X), Quantize(p.Y))).ToArray();
Array.Sort(keys, points);

// Hilbert, when a run of indices has to be a connected region.
ulong index = HilbertCurve.Encode2D(cellX, cellY);
var (nx, ny) = HilbertCurve.Decode2D(index + 1);   // always one cell away, along one axis
```

**Contract and special cases:**

- **Orientation.** The Hilbert curve starts at the origin and ends at `(2^32 - 1, 0)` in 2-D — the conventional orientation, and the traversal the textbook `d2xy` produces, which the tests pin cell by cell over the first sixteen indices.
- **Morton's axis order.** `Encode2D` puts `x` on the even bit positions and `y` on the odd ones; `Encode3D` assigns bit position modulo three to `x`, `y`, `z` in that order. Swapping the arguments transposes the curve, which is harmless but changes every key you have already stored.
- **Locality is not distance.** Neither curve promises that nearby indices are the *nearest* points, only that the mapping keeps regions together. Two points either side of a top-level boundary can be adjacent in space and far apart in index — this is a property of every space-filling curve, and Hilbert's adjacency guarantee runs the other way (index-adjacent implies space-adjacent, not the converse).
- Every method is static, allocation-free and AOT-safe. Hilbert's transform runs in a two- or three-word `stackalloc` scratch.
