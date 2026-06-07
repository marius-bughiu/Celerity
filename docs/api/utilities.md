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
