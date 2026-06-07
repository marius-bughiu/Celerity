# Utilities API Reference

## FastUtils

```csharp
public static class FastUtils
```

Lives in the `Celerity` namespace. Provides low-level helper methods used internally by the collection implementations. Public so that advanced users can reuse them.

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
