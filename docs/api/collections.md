# Collections API Reference

All collection types live in the `Celerity.Collections` namespace.

## CelerityDictionary&lt;TKey, TValue, THasher&gt;

A high-performance generic dictionary parameterized on a custom hash provider. Uses open addressing with linear probing and power-of-two sizing for fast index computation. Implements `IReadOnlyDictionary<TKey, TValue?>`.

```csharp
public class CelerityDictionary<TKey, TValue, THasher>
    : IReadOnlyDictionary<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
```

### Why the struct constraint?

The `where THasher : struct, IHashProvider<TKey>` constraint lets the JIT devirtualize and inline the `Hash()` call. This is a deliberate design choice; passing hash providers as interfaces or classes would add a virtual-dispatch cost on every probe.

### Constructors

```csharp
public CelerityDictionary(
    int capacity = 16,
    float loadFactor = 0.75f)

public CelerityDictionary(
    IEnumerable<KeyValuePair<TKey, TValue>> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Creates a new dictionary. `capacity` is rounded up to the next power of two. `loadFactor` controls the fill ratio before the internal arrays are resized.

The `IEnumerable<KeyValuePair<TKey, TValue>>` overload copies entries from `source`. When `source` implements `ICollection<T>`, its `Count` is used to size the backing storage so initial fills avoid resize work; for non-collection enumerables, the caller-supplied `capacity` parameter is used. The out-of-band `default(TKey)` slot is populated when the source contains an entry with `default(TKey)`.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity < 0`.
- `ArgumentOutOfRangeException` if `loadFactor <= 0` or `loadFactor >= 1`.
- `ArgumentNullException` if `source` is `null` (enumerable overload).
- `ArgumentException` if `source` contains duplicate keys (enumerable overload).

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count`  | `int` | Number of key/value pairs in the dictionary. |
| `Keys`   | `KeyCollection` | Allocation-free enumerable view over the keys. |
| `Values` | `ValueCollection` | Allocation-free enumerable view over the values. |

### Indexer

```csharp
public TValue this[TKey key] { get; set; }
```

**Get** returns the value associated with `key`, or throws `KeyNotFoundException` if the key is not present. **Set** inserts or overwrites the entry for `key`, resizing the internal arrays if the load factor threshold is exceeded.

### Methods

#### ContainsKey

```csharp
public bool ContainsKey(TKey key)
```

Returns `true` if `key` is present in the dictionary.

#### TryGetValue

```csharp
public bool TryGetValue(TKey key, out TValue? value)
```

If `key` is found, sets `value` and returns `true`. Otherwise sets `value` to `default` and returns `false`.

#### ContainsValue

```csharp
public bool ContainsValue(TValue? value)
```

Returns `true` if any entry's value equals `value` under `EqualityComparer<TValue?>.Default`, matching BCL `Dictionary<TKey, TValue>.ContainsValue(TValue)` semantics. The scan walks the probe table (skipping empty slots so the empty `default(TValue)` payload there is not mistaken for a real entry) and, when present, the out-of-band default-key slot.

This operation is `O(n)` in the dictionary's count. No allocation on the hot path beyond the cached `EqualityComparer<TValue?>.Default` access.

#### Add

```csharp
public void Add(TKey key, TValue value)
```

Inserts `key`/`value`. Throws `ArgumentException` if `key` already exists.

#### TryAdd

```csharp
public bool TryAdd(TKey key, TValue value)
```

Inserts `key`/`value` if the key is not already present. Returns `true` on success, `false` if the key already existed (the dictionary is not modified in that case).

#### Remove

```csharp
public bool Remove(TKey key)
public bool Remove(TKey key, out TValue? value)
```

Removes the entry for `key`. Returns `true` if the key was found and removed, `false` otherwise. After removal, the probe chain is repaired by back-shifting the following entries into the freed slot (backward-shift deletion), preserving lookup correctness without rehashing.

The capture overload sets `value` to the value that was associated with the key immediately before removal, or to `default(TValue)` if the key was not found. The out-of-band default-key slot is surfaced through this path identically to the regular probe table.

#### Clear

```csharp
public void Clear()
```

Removes all entries. The underlying array capacity is preserved.

#### EnsureCapacity

```csharp
public int EnsureCapacity(int capacity)
```

Grows the backing table in a single rehash so it can hold at least `capacity` entries without resizing, and returns the number of entries it can now hold before the next resize. Pre-sizing before a bulk insert of a known size avoids the `O(log n)` incremental rehashes an unsized dictionary pays as it doubles — matching BCL `Dictionary<TKey, TValue>.EnsureCapacity`. The dictionary is never shrunk by this call; if it already has room, no rehash occurs. Throws `ArgumentOutOfRangeException` if `capacity` is negative.

#### TrimExcess

```csharp
public void TrimExcess()
public void TrimExcess(int capacity)
```

Rehashes the entries into the smallest power-of-two table that still holds the current `Count` (the parameterless overload) or at least `capacity` entries (the parameterized overload), reclaiming memory after the dictionary has shrunk via `Remove` / `Clear`. The out-of-band default-key entry is preserved. `TrimExcess(capacity)` throws `ArgumentOutOfRangeException` if `capacity` is less than the current `Count`.

#### GetEnumerator

```csharp
public Enumerator GetEnumerator()
```

Returns a struct enumerator that yields `KeyValuePair<TKey, TValue?>`. The out-of-band default-key entry is yielded first if present. *Structurally* mutating the dictionary during enumeration — adding a new key, removing a key, or `Clear` — throws `InvalidOperationException` from the next `MoveNext` / `Reset` call, matching BCL `Dictionary<,>` semantics. Overwriting the value of an existing key via the indexer (`dict[existingKey] = newValue`) is *not* a structural change and does not invalidate an active enumerator, so the common "iterate and update values in place" pattern is legal. Iteration order is unspecified and may change between versions.

### IReadOnlyDictionary&lt;TKey, TValue?&gt;

`CelerityDictionary` implements `IReadOnlyDictionary<TKey, TValue?>` via thin explicit interface forwarders on top of the existing struct `KeyCollection` / `ValueCollection` / `Enumerator` types. The zero-allocation `foreach` fast path is preserved; the interface path boxes the enumerator exactly once per `GetEnumerator()` call, matching BCL `Dictionary<,>` behaviour. The out-of-band default-key entry is surfaced through every interface member.

### Default-key handling

`default(TKey)` (which is `null` for reference types, `0` for `int`, `Guid.Empty` for `Guid`, etc.) cannot be stored in the regular probe table because it doubles as the empty-slot sentinel. Celerity handles this transparently via a dedicated `_hasDefaultKey` flag and a separate value slot, so callers never need to worry about it.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var dict = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
dict[42] = "hello";
dict[0]  = "zero is fine";

if (dict.TryGetValue(42, out var val))
    Console.WriteLine(val); // "hello"

// Zero-allocation enumeration (struct enumerator):
foreach (var kvp in dict)
    Console.WriteLine($"{kvp.Key} -> {kvp.Value}");

foreach (int key in dict.Keys) { /* ... */ }
foreach (var value in dict.Values) { /* ... */ }
```

---

## RobinHoodDictionary&lt;TKey, TValue, THasher&gt;

A drop-in peer of `CelerityDictionary` that resolves collisions with **Robin Hood** open addressing instead of plain linear probing. The public surface — constructors, indexer, `ContainsKey` / `ContainsValue` / `TryGetValue` / `Add` / `TryAdd` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator` / `KeyCollection` / `ValueCollection`, and `IReadOnlyDictionary<TKey, TValue?>` — is identical to `CelerityDictionary`. Only the probing strategy differs.

```csharp
public class RobinHoodDictionary<TKey, TValue, THasher>
    : IReadOnlyDictionary<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
```

### What Robin Hood probing does

For every occupied slot the table tracks how far the entry sits from its ideal (hash) slot — its **probe sequence length** (PSL). On insert, an incoming key that has travelled further than the key already occupying a slot *displaces* it ("robs from the rich"): the resident is evicted and re-inserted further along. This keeps probe-length variance low, so the worst-case probe is much closer to the average than under linear probing. Two consequences matter to callers:

- **Bounded tail latency on clustered keys.** Where linear probing grows a single long run and degrades a lookup toward `O(n)`, Robin Hood spreads the cost evenly. The PSL invariant also lets a *negative* lookup stop early — as soon as the probe distance exceeds the resident slot's PSL, the key cannot be present.
- **A small, predictable overhead.** Each slot carries an extra `int` of PSL bookkeeping, so the dictionary allocates more than `CelerityDictionary`, and inserts do a little extra work for the displacement swaps. On uniform key distributions Robin Hood is typically a wash or a slight loss versus linear probing.

### When to choose it over `CelerityDictionary`

Reach for `RobinHoodDictionary` when your keys are **clustered or adversarial** (hash codes that bunch up, attacker-influenced keys, or a weak/identity hasher) and you care about **worst-case lookup latency**, not just the average. For uniformly distributed keys with a good hasher, stay on `CelerityDictionary` — it has the smaller footprint and matches or beats Robin Hood there. Both are single-threaded and make no iteration-order guarantee.

### Constructors

```csharp
public RobinHoodDictionary(
    int capacity = 16,
    float loadFactor = 0.75f)

public RobinHoodDictionary(
    IEnumerable<KeyValuePair<TKey, TValue>> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics, sizing (including the `ICollection<T>` count-with-load-factor-headroom rule), validation, and exceptions as `CelerityDictionary`.

### Default-key handling

Identical to `CelerityDictionary`: `default(TKey)` (`null` / `0` / `Guid.Empty` / …) doubles as the empty-slot sentinel, so it is stored out-of-band via a `_hasDefaultKey` flag and a dedicated value slot. Transparent to callers.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Clustered keys where linear probing would build long runs — Robin Hood
// keeps every lookup's probe length close to the average.
var dict = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
dict[42] = "hello";
dict[0]  = "zero is fine";

if (dict.TryGetValue(42, out var val))
    Console.WriteLine(val); // "hello"

foreach (var kvp in dict)
    Console.WriteLine($"{kvp.Key} -> {kvp.Value}");
```

---

## PooledCelerityDictionary&lt;TKey, TValue, THasher&gt;

An allocation-conscious peer of `CelerityDictionary` whose backing arrays are **rented from [`ArrayPool<T>.Shared`](https://learn.microsoft.com/dotnet/api/system.buffers.arraypool-1)** instead of being allocated on the managed heap. The public surface is identical to `CelerityDictionary` — same indexer, `ContainsKey` / `ContainsValue` / `TryGetValue` / `Add` / `TryAdd` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator` / `KeyCollection` / `ValueCollection`, and `IReadOnlyDictionary<TKey, TValue?>` — with one addition: it implements `IDisposable`.

```csharp
public class PooledCelerityDictionary<TKey, TValue, THasher>
    : IReadOnlyDictionary<TKey, TValue?>, IDisposable
    where THasher : struct, IHashProvider<TKey>
```

### Why pooled storage

In high-throughput code that builds and tears down many short-lived dictionaries (per request, per frame, per batch), the backing arrays are a steady source of Gen 0 garbage and, once they cross the [85 KB Large Object Heap threshold](https://learn.microsoft.com/dotnet/standard/garbage-collection/large-object-heap), of LOH pressure that a normal `Dictionary<,>` or `CelerityDictionary` cannot avoid. `PooledCelerityDictionary` borrows its key/value arrays from the shared pool and returns them on `Dispose` (and on every internal resize), so a build/use/dispose cycle reuses buffers across iterations rather than allocating fresh ones each time. The `PooledCelerityDictionaryBenchmark` reports the difference in its `Allocated` column.

### When to choose it over `CelerityDictionary`

Reach for the pooled variant when the dictionary is **short-lived and rebuilt frequently on a hot path**, GC pressure is a measured concern, and you can guarantee a `Dispose` (e.g. a `using` scope). For a long-lived dictionary that lives for the life of the process, the pooling buys nothing and the disposal contract is pure overhead — stay on `CelerityDictionary`. Like every Celerity collection it is **not thread-safe**.

### Lifecycle and pooling contract

- **Dispose returns the buffers.** Call `Dispose` (ideally via `using`) when finished so the arrays return to the pool for reuse. Disposal is idempotent, and after it every member throws `ObjectDisposedException`.
- **Not disposing is not a leak.** If you forget to dispose, the rented arrays are simply garbage-collected like any other managed array — you just forfeit the pooling benefit.
- **Pool exhaustion is handled for you.** `ArrayPool<T>.Shared` allocates a fresh buffer when it has none to hand out, so a "pool empty" condition never surfaces to the caller.
- **Reference types are cleared on return** so the pool does not keep your keys / values reachable after disposal (memory-leak prevention); value-type buffers skip the clear for speed.
- **Over-provisioned rents are handled.** `ArrayPool.Rent` may return an array larger than requested; the dictionary tracks its logical power-of-two capacity independently and only ever reads or writes the live region, so the (uncleared) tail of an oversized buffer never surfaces in `Count`, enumeration, or `ContainsValue`.

### Constructors

```csharp
public PooledCelerityDictionary(
    int capacity = 16,
    float loadFactor = 0.75f)

public PooledCelerityDictionary(
    IEnumerable<KeyValuePair<TKey, TValue>> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics, sizing (including the `ICollection<T>` count-with-load-factor-headroom rule), validation, and exceptions as `CelerityDictionary`.

### Default-key handling

Identical to `CelerityDictionary`: `default(TKey)` (`null` / `0` / `Guid.Empty` / …) is stored out-of-band so it never collides with the empty-slot sentinel. Transparent to callers.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// A dictionary built fresh on a hot path and thrown away each iteration —
// the rented buffers return to the pool instead of becoming GC garbage.
using (var dict = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>())
{
    dict[42] = "hello";
    dict[0]  = "zero is fine"; // out-of-band default key

    if (dict.TryGetValue(42, out var val))
        Console.WriteLine(val); // "hello"

    foreach (var kvp in dict)
        Console.WriteLine($"{kvp.Key} -> {kvp.Value}");
} // Dispose() returns the backing arrays to ArrayPool<T>.Shared here.
```

---

## SwissDictionary&lt;TKey, TValue, THasher&gt;

A drop-in peer of `CelerityDictionary` that resolves collisions with **SIMD-accelerated group probing** in the spirit of Google's Swiss Tables and Facebook's `F14`, instead of scalar linear probing. The public surface — constructors, indexer, `ContainsKey` / `ContainsValue` / `TryGetValue` / `Add` / `TryAdd` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator` / `KeyCollection` / `ValueCollection`, and `IReadOnlyDictionary<TKey, TValue?>` — is identical to `CelerityDictionary`. Only the probing strategy differs.

```csharp
public class SwissDictionary<TKey, TValue, THasher>
    : IReadOnlyDictionary<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
```

### What SIMD group probing does

The table keeps a parallel array of one-byte **control** tags — one per slot — separate from the key/value arrays. Each control byte is either `EMPTY`, `DELETED` (a tombstone), or, for an occupied slot, the low 7 bits of the key's hash (its *h2* fragment). Slots are grouped into aligned blocks of 16, so a single `Vector128<sbyte>` compare tests all 16 control bytes in a group at once: a lookup loads the 16 tags, compares them against the broadcast h2, and turns the result into a 16-bit candidate mask via `Vector128.ExtractMostSignificantBits`. Only the (usually one) candidate slots then pay a full key comparison; a group with any `EMPTY` slot ends the probe. Two consequences matter to callers:

- **One compare per group, not per slot.** The group compare amortizes the per-slot tag test across 16 slots, and the h2 tag filters out non-matching residents before any (potentially expensive) key comparison — so negative lookups and lookups on clustered keys stay cheap. The portable `Vector128` API JITs to SSE2 / AVX2 on x86, AdvSimd on Arm, and a scalar software fallback elsewhere, so the type is correct everywhere and fast where hardware SIMD is available.
- **A small, predictable overhead.** Each slot carries a one-byte control tag (so the dictionary allocates a little more than `CelerityDictionary`), and deletion uses tombstones that are reclaimed by a rehash once they accumulate, so a churn of insert/delete cycles cannot grow the table without bound.

### When to choose it over `CelerityDictionary`

Reach for `SwissDictionary` for **lookup-heavy** workloads where the group compare and h2 filtering pay off — large tables, many negative lookups, or clustered keys — and where one extra control byte per slot is an acceptable cost. For small tables or write-dominated workloads with a good hasher, `CelerityDictionary` has the smaller footprint and is competitive. Both are single-threaded and make no iteration-order guarantee.

### Constructors

```csharp
public SwissDictionary(
    int capacity = 16,
    float loadFactor = 0.75f)

public SwissDictionary(
    IEnumerable<KeyValuePair<TKey, TValue>> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics, sizing (including the `ICollection<T>` count-with-load-factor-headroom rule), validation, and exceptions as `CelerityDictionary`. The backing table is always a power of two and at least one SIMD group (16 slots), so a requested capacity below 16 is rounded up.

### Default-key handling

Identical to `CelerityDictionary`: `default(TKey)` (`null` / `0` / `Guid.Empty` / …) is stored out-of-band via a `_hasDefaultKey` flag and a dedicated value slot, so the hasher is never invoked with it (string hashers throw on `null`). Transparent to callers. Note that, unlike linear-probing tables, the Swiss layout tracks occupancy in the control bytes rather than by sentinel key value — the out-of-band slot is kept purely to honour the hasher contract.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Lookup-heavy table: each probe tests a whole 16-slot group with one SIMD compare.
var dict = new SwissDictionary<int, string, Int32WangNaiveHasher>();
dict[42] = "hello";
dict[0]  = "zero is fine";

if (dict.TryGetValue(42, out var val))
    Console.WriteLine(val); // "hello"

foreach (var kvp in dict)
    Console.WriteLine($"{kvp.Key} -> {kvp.Value}");
```

---

## HashCachingDictionary&lt;TKey, TValue, THasher&gt;

A drop-in peer of `CelerityDictionary` that pushes the **struct-of-arrays layout** one step further: alongside the parallel `keys` / `values` arrays it keeps a dense side array of 32-bit hash **fingerprints**, one per slot. A probe scan touches only that compact metadata buffer — comparing the cached fingerprint before it ever reads a key — so cache-cold lookups and lookups with expensive key equality (long strings, large structs) short-circuit on a single integer compare. The public surface — constructors, indexer, `ContainsKey` / `ContainsValue` / `TryGetValue` / `Add` / `TryAdd` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator` / `KeyCollection` / `ValueCollection`, and `IReadOnlyDictionary<TKey, TValue?>` — is identical to `CelerityDictionary`. Only the probe metadata differs.

```csharp
public class HashCachingDictionary<TKey, TValue, THasher>
    : IReadOnlyDictionary<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
```

### What the cached-fingerprint side array does

The fingerprint of an occupied slot is the key's hash with its top bit forced set (`hash | 0x80000000`), which makes it always non-zero; an empty slot is the array default of `0`. The fingerprint array therefore doubles as the occupancy bitmap — probing, enumeration, and `ContainsValue` test it rather than comparing keys against `default(TKey)`. Two consequences matter to callers:

- **Metadata-only probe scans.** A linear probe walks the dense `int[]` fingerprint array and only dereferences a key (running the full, possibly expensive `EqualityComparer<TKey>` check) when the cached fingerprint matches. On a cache-cold table the probe stays inside one compact buffer, and on keys with costly equality the integer compare filters out almost every non-match — the win over scalar linear probing grows with key-equality cost and table size.
- **Rehash without re-hashing.** Because the forced occupied bit sits above every power-of-two table mask, the cached fingerprint also yields the slot index directly (`fingerprint & mask`). A resize re-homes every entry straight from its stored fingerprint without invoking the hasher once, and backward-shift deletion reads each candidate's natural slot from its fingerprint too.

### When to choose it over `CelerityDictionary`

Reach for `HashCachingDictionary` for **lookup-dominated** workloads, **expensive-equality keys** (long strings, large value-type keys), or large cache-cold tables where the metadata-only scan pays off, and where four bytes of metadata per slot is an acceptable cost. For small tables of cheap (e.g. `int`) keys, `CelerityDictionary` has the smaller footprint and is roughly a wash. It is complementary to `SwissDictionary`: both keep a metadata side array, but `HashCachingDictionary` is a scalar, wider-fingerprint design with backward-shift (tombstone-free) deletion, while `SwissDictionary` uses SIMD group probing over one-byte tags. Both are single-threaded and make no iteration-order guarantee.

### Constructors

```csharp
public HashCachingDictionary(
    int capacity = 16,
    float loadFactor = 0.75f)

public HashCachingDictionary(
    IEnumerable<KeyValuePair<TKey, TValue>> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics, sizing (including the `ICollection<T>` count-with-load-factor-headroom rule), validation, and exceptions as `CelerityDictionary`. The backing table is always a power of two.

### Default-key handling

Identical to `CelerityDictionary`: `default(TKey)` (`null` / `0` / `Guid.Empty` / …) is stored out-of-band via a `_hasDefaultKey` flag and a dedicated value slot, so the hasher is never invoked with it (string hashers throw on `null`). Transparent to callers. The fingerprint array tracks occupancy, so the empty-slot sentinel (`0`) never collides with a real entry even when a key would hash to zero.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Lookup-heavy table of string keys: each probe compares cached hashes first,
// so the costly string equality only runs on a genuine fingerprint match.
var dict = new HashCachingDictionary<string, int, StringFnV1AHasher>();
dict["hello"] = 42;
dict[null!]   = 0; // null key stored out-of-band

if (dict.TryGetValue("hello", out var val))
    Console.WriteLine(val); // 42

foreach (var kvp in dict)
    Console.WriteLine($"{kvp.Key} -> {kvp.Value}");
```

---

## IntDictionary&lt;TValue&gt;

A convenience subclass of `IntDictionary<TValue, Int32WangNaiveHasher>` for the common case of integer-keyed dictionaries.

```csharp
public class IntDictionary<TValue>
    : IntDictionary<TValue, Int32WangNaiveHasher>
```

### Constructors

```csharp
public IntDictionary(
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics and validation as `CelerityDictionary`.

### Inherited API

`IntDictionary<TValue>` exposes the same public surface as `IntDictionary<TValue, THasher>` (see below).

---

## IntDictionary&lt;TValue, THasher&gt;

A high-performance dictionary keyed by `int`, parameterized on a custom hash provider. This is a separate implementation from `CelerityDictionary` that avoids the boxing/equality-comparer overhead of generic key types by working directly with `int` keys and using `==` for comparisons. Implements `IReadOnlyDictionary<int, TValue?>`.

```csharp
public class IntDictionary<TValue, THasher>
    : IReadOnlyDictionary<int, TValue?>
    where THasher : struct, IHashProvider<int>
```

### Constructors

```csharp
public IntDictionary(
    int capacity = 16,
    float loadFactor = 0.75f)

public IntDictionary(
    IEnumerable<KeyValuePair<int, TValue>> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

**Throws** the same exceptions as `CelerityDictionary`. The enumerable overload follows the same `source`-sizing and duplicate-key semantics described above.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Count`  | `int` | Number of entries in the dictionary. |
| `Keys`   | `KeyCollection` | Allocation-free enumerable view over the keys. |
| `Values` | `ValueCollection` | Allocation-free enumerable view over the values. |

### Methods

The method signatures and semantics match `CelerityDictionary`:

- `this[int key]` — indexer (get throws `KeyNotFoundException` on miss; set inserts or overwrites).
- `bool ContainsKey(int key)`
- `bool TryGetValue(int key, out TValue? value)`
- `bool ContainsValue(TValue? value)` — BCL-parity `O(n)` linear scan over the probe table and, when present, the out-of-band zero-key slot.
- `void Add(int key, TValue value)` — throws `ArgumentException` on duplicate.
- `bool TryAdd(int key, TValue value)`
- `bool Remove(int key)`
- `bool Remove(int key, out TValue? value)` — capture overload; `value` is the previous value or `default` if the key was absent.
- `void Clear()`
- `int EnsureCapacity(int capacity)` / `void TrimExcess()` / `void TrimExcess(int capacity)` — capacity management, identical in semantics to `CelerityDictionary` (and to BCL `Dictionary<,>`): `EnsureCapacity` pre-grows the table to hold `capacity` entries without resizing; `TrimExcess` rehashes down to the smallest table that still holds `Count` (or `capacity`).
- `Enumerator GetEnumerator()` — struct enumerator yielding `KeyValuePair<int, TValue?>`. The out-of-band zero-key entry is yielded first if present. *Structurally* mutating the dictionary during enumeration — adding a new key, removing a key, or `Clear` — throws `InvalidOperationException` from the next `MoveNext` / `Reset` call, matching BCL `Dictionary<,>` semantics. Overwriting the value of an existing key via the indexer is *not* a structural change and does not invalidate an active enumerator. Iteration order is unspecified and may change between versions.

`IntDictionary<TValue, THasher>` also implements `IReadOnlyDictionary<int, TValue?>` with the same explicit-interface forwarding pattern as `CelerityDictionary`.

### Zero-key handling

The key `0` collides with the internal `EMPTY_KEY` sentinel. Like `CelerityDictionary`'s default-key handling, `IntDictionary` stores key `0` out-of-band via a `_hasZeroKey` flag and a dedicated value slot. This is invisible to callers.

### Usage example

```csharp
using Celerity.Collections;

var ids = new IntDictionary<string>();
ids[0] = "zero works";
ids[99] = "ninety-nine";

Console.WriteLine(ids.Count); // 2
ids.Remove(0);
Console.WriteLine(ids.Count); // 1

// Zero-allocation enumeration (struct enumerator):
foreach (var kvp in ids)
    Console.WriteLine($"{kvp.Key} -> {kvp.Value}");

foreach (int key in ids.Keys) { /* ... */ }
foreach (var value in ids.Values) { /* ... */ }
```

---

## LongDictionary&lt;TValue&gt;

A convenience subclass of `LongDictionary<TValue, Int64WangNaiveHasher>` for the common case of 64-bit integer-keyed dictionaries.

```csharp
public class LongDictionary<TValue>
    : LongDictionary<TValue, Int64WangNaiveHasher>
```

### Constructors

```csharp
public LongDictionary(
    int capacity = 16,
    float loadFactor = 0.75f)

public LongDictionary(
    IEnumerable<KeyValuePair<long, TValue>> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics and validation as `IntDictionary`.

---

## LongDictionary&lt;TValue, THasher&gt;

A high-performance dictionary keyed by `long`, parameterized on a custom hash provider. Mirrors `IntDictionary` but for 64-bit keys. Defaults to `Int64WangNaiveHasher` when used through the convenience subclass. Implements `IReadOnlyDictionary<long, TValue?>`.

```csharp
public class LongDictionary<TValue, THasher>
    : IReadOnlyDictionary<long, TValue?>
    where THasher : struct, IHashProvider<long>
```

### API

The public surface and semantics match `IntDictionary`:

- `this[long key]`
- `bool ContainsKey(long key)`
- `bool TryGetValue(long key, out TValue? value)`
- `bool ContainsValue(TValue? value)` — BCL-parity `O(n)` linear scan over the probe table and, when present, the out-of-band zero-key slot.
- `void Add(long key, TValue value)` — throws `ArgumentException` on duplicate.
- `bool TryAdd(long key, TValue value)`
- `bool Remove(long key)`
- `bool Remove(long key, out TValue? value)`
- `void Clear()`
- `int EnsureCapacity(int capacity)` / `void TrimExcess()` / `void TrimExcess(int capacity)` — capacity management, identical in semantics to `CelerityDictionary`.
- `Enumerator GetEnumerator()` — struct enumerator yielding `KeyValuePair<long, TValue?>`.
- `KeyCollection Keys`, `ValueCollection Values` — allocation-free struct views.

### Zero-key handling

The key `0L` collides with the `EMPTY_KEY` sentinel and is stored out-of-band the same way `IntDictionary` handles the key `0`. Two keys that share the lower 32 bits but differ in the upper 32 bits are kept distinct (the probe path does not truncate).

### Usage example

```csharp
using Celerity.Collections;

var map = new LongDictionary<string>();
map[0L] = "zero is fine";
map[long.MaxValue] = "edge";
map[(long)int.MaxValue + 1L] = "no truncation";

Console.WriteLine(map.Count); // 3
```

---

## CeleritySet&lt;T, THasher&gt;

A high-performance generic set parameterized on a custom hash provider. Set counterpart to `CelerityDictionary`. Implements `ISet<T>` (and therefore `ICollection<T>` / `IEnumerable<T>`), so it is a drop-in for `HashSet<T>` wherever set algebra is used.

```csharp
public class CeleritySet<T, THasher> : ISet<T>
    where THasher : struct, IHashProvider<T>
```

### Constructors

```csharp
public CeleritySet(
    int capacity = 16,
    float loadFactor = 0.75f)

public CeleritySet(
    IEnumerable<T> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

The first overload creates an empty set; `capacity` is rounded up to the next power of two and `loadFactor` controls the fill ratio before resizing.

The `IEnumerable<T>` overload copies elements from `source`. When `source` implements `ICollection<T>`, its `Count` is used to size the backing storage so the initial fill avoids resize work; otherwise the caller-supplied `capacity` parameter is used. Unlike the dictionary `IEnumerable<KeyValuePair<,>>` constructor, duplicate elements (including duplicate `default(T)` entries) are silently deduplicated to match BCL `HashSet<T>(IEnumerable<T>)` semantics — sets do not have a duplicate-key contract. The out-of-band `default(T)` slot is populated when `source` contains it.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity < 0`.
- `ArgumentOutOfRangeException` if `loadFactor <= 0` or `loadFactor >= 1`.
- `ArgumentNullException` if `source` is `null` (enumerable overload).

### Methods

- `void Add(T item)` — throws `ArgumentException` on duplicate.
- `bool TryAdd(T item)` — `true` on success, `false` if already present.
- `bool Contains(T item)`
- `bool Remove(T item)`
- `void Clear()`
- `int EnsureCapacity(int capacity)` / `void TrimExcess()` / `void TrimExcess(int capacity)` — capacity management mirroring BCL `HashSet<T>`: `EnsureCapacity` pre-grows the table to hold `capacity` elements without resizing and returns the resulting capacity; `TrimExcess` rehashes down to the smallest table that still holds `Count` (or `capacity`). The out-of-band `default(T)` slot is preserved. `EnsureCapacity` throws `ArgumentOutOfRangeException` on a negative capacity; `TrimExcess(capacity)` throws if `capacity < Count`.
- `int Count { get; }`
- `Enumerator GetEnumerator()` — struct enumerator. The out-of-band `default(T)` entry (zero for primitives, `Guid.Empty`, `null` for reference types) is yielded first when present.
- `void CopyTo(T[] array, int arrayIndex)` — copies every element (the out-of-band `default(T)` entry first) into `array`, matching `HashSet<T>.CopyTo` argument validation.

### Set operations (`ISet<T>`)

The full BCL `HashSet<T>` set-algebra surface is available and follows `HashSet<T>` semantics exactly (duplicate-tolerant `other`, self-aliasing `other == this`, and the out-of-band `default(T)`/zero element all handled):

- **Mutating:** `void UnionWith(IEnumerable<T> other)`, `void IntersectWith(IEnumerable<T> other)`, `void ExceptWith(IEnumerable<T> other)`, `void SymmetricExceptWith(IEnumerable<T> other)`.
- **Query:** `bool IsSubsetOf(...)`, `bool IsProperSubsetOf(...)`, `bool IsSupersetOf(...)`, `bool IsProperSupersetOf(...)`, `bool Overlaps(...)`, `bool SetEquals(...)`.

Each throws `ArgumentNullException` when `other` is `null`. The subset / equality shapes materialize `other` once into a distinct `HashSet<T>` keyed by `EqualityComparer<T>.Default` (the same equality the set itself uses); the superset / overlap shapes stream `other` directly against the set's O(1) membership test.

> **`Add` note.** `ISet<T>.Add(T)` returns `bool` (the non-throwing add, equivalent to `TryAdd`). The concrete `public void Add(T)` keeps its throw-on-duplicate behaviour — cast to `ISet<T>`, or use `TryAdd`, when you want the boolean result. `ICollection<T>.Add(T)` ignores duplicates (never throws).

### Default-element handling

`default(T)` is stored out-of-band via a `_hasDefaultValue` flag and never collides with the empty-slot sentinel. Mutating the set during enumeration — including via a mutating set operation such as `UnionWith` — throws `InvalidOperationException` on the next `MoveNext` / `Reset`, matching BCL `HashSet<T>`.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var ids = new CeleritySet<Guid, GuidHasher>();
ids.Add(Guid.NewGuid());
ids.Add(Guid.Empty); // default-element slot
Console.WriteLine(ids.Contains(Guid.Empty)); // True

foreach (var id in ids) { /* ... */ }
```

---

## SwissSet&lt;T, THasher&gt;

A drop-in peer of `CeleritySet` that resolves collisions with **SIMD-accelerated group probing** in the spirit of Google's Swiss Tables and Facebook's `F14`, instead of scalar linear probing. It is the set counterpart of `SwissDictionary` — the same control-byte machinery with no value array. The public surface — constructors, `Add` / `TryAdd` / `Contains` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator`, `CopyTo`, and the full `ISet<T>` set-algebra surface (see [`CeleritySet`](#celerityset-t-thasher)) — is identical to `CeleritySet`. Only the probing strategy differs.

```csharp
public class SwissSet<T, THasher> : ISet<T>
    where THasher : struct, IHashProvider<T>
```

### What SIMD group probing does

The set keeps a parallel array of one-byte **control** tags — one per slot — separate from the element array. Each control byte is either `EMPTY`, `DELETED` (a tombstone), or, for an occupied slot, the low 7 bits of the element's hash (its *h2* fragment). Slots are grouped into aligned blocks of 16, so a single `Vector128<sbyte>` compare tests all 16 control bytes in a group at once: a membership test loads the 16 tags, compares them against the broadcast h2, and turns the result into a 16-bit candidate mask via `Vector128.ExtractMostSignificantBits`. Only the (usually one) candidate slots then pay a full element comparison; a group with any `EMPTY` slot ends the probe. Two consequences matter to callers:

- **One compare per group, not per slot.** The group compare amortizes the per-slot tag test across 16 slots, and the h2 tag filters out non-matching residents before any (potentially expensive) element comparison — so negative `Contains` lookups and lookups on clustered elements stay cheap. The portable `Vector128` API JITs to SSE2 / AVX2 on x86, AdvSimd on Arm, and a scalar software fallback elsewhere, so the type is correct everywhere and fast where hardware SIMD is available.
- **A small, predictable overhead.** Each slot carries a one-byte control tag (so the set allocates a little more than `CeleritySet`), and deletion uses tombstones that are reclaimed by a rehash once they accumulate, so a churn of add/remove cycles cannot grow the table without bound.

### When to choose it over `CeleritySet`

Reach for `SwissSet` for **membership-heavy** workloads where the group compare and h2 filtering pay off — large sets, many negative `Contains` lookups ("have I seen this?" dedup guards), or clustered elements — and where one extra control byte per slot is an acceptable cost. Membership is a set's primary operation, so the negative-lookup win is exactly the common case. For small sets or write-dominated workloads with a good hasher, `CeleritySet` has the smaller footprint and is competitive. Both are single-threaded and make no iteration-order guarantee.

### Constructors

```csharp
public SwissSet(
    int capacity = 16,
    float loadFactor = 0.75f)

public SwissSet(
    IEnumerable<T> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics, sizing (including the `ICollection<T>` count-with-load-factor-headroom rule), validation, and exceptions as `CeleritySet` — duplicate elements (including duplicate `default(T)`) are silently deduplicated. The backing table is always a power of two and at least one SIMD group (16 slots), so a requested capacity below 16 is rounded up.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity < 0`.
- `ArgumentOutOfRangeException` if `loadFactor <= 0` or `loadFactor >= 1`.
- `ArgumentNullException` if `source` is `null` (enumerable overload).

### Methods

- `void Add(T item)` — throws `ArgumentException` on duplicate.
- `bool TryAdd(T item)` — `true` on success, `false` if already present.
- `bool Contains(T item)`
- `bool Remove(T item)`
- `void Clear()`
- `int EnsureCapacity(int capacity)` / `void TrimExcess()` / `void TrimExcess(int capacity)` — capacity management mirroring `CeleritySet`; `TrimExcess` additionally drops accumulated tombstones. The out-of-band `default(T)` slot is preserved.
- `int Count { get; }`
- `Enumerator GetEnumerator()` — struct enumerator; the out-of-band `default(T)` entry is yielded first when present.

### Default-element handling

Identical to `CeleritySet`: `default(T)` (`null` / `0` / `Guid.Empty` / …) is stored out-of-band via a `_hasDefaultValue` flag, so the hasher is never invoked with it (string hashers throw on `null`). Unlike linear-probing tables, the Swiss layout tracks occupancy in the control bytes rather than by sentinel element value — the out-of-band slot is kept purely to honour the hasher contract. Mutating the set during enumeration throws `InvalidOperationException` on the next `MoveNext` / `Reset`.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Membership-heavy set: each Contains tests a whole 16-slot group with one SIMD compare.
var seen = new SwissSet<int, Int32WangNaiveHasher>();
seen.Add(42);
seen.Add(0); // default-element slot

Console.WriteLine(seen.Contains(42));   // True
Console.WriteLine(seen.Contains(999));  // False — negative lookup short-circuits on the group scan

foreach (var item in seen) { /* ... */ }
```

---

## RobinHoodSet&lt;T, THasher&gt;

A drop-in peer of `CeleritySet` that resolves collisions with **Robin Hood open addressing** instead of plain linear probing. It is the set counterpart of `RobinHoodDictionary` — the same probe-sequence-length (PSL) machinery with no value array. The public surface — constructors, `Add` / `TryAdd` / `Contains` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator`, `CopyTo`, and the full `ISet<T>` set-algebra surface (see [`CeleritySet`](#celerityset-t-thasher)) — is identical to `CeleritySet`. Only the probing strategy differs.

```csharp
public class RobinHoodSet<T, THasher> : ISet<T>
    where THasher : struct, IHashProvider<T>
```

### What Robin Hood probing does

The set keeps, for every occupied slot, the number of steps it sits away from its ideal (hash) slot — its **probe sequence length** (PSL), stored in a parallel `int` array alongside the elements. On insert, an incoming element that has travelled further than the element already occupying a slot **displaces** it ("robs from the rich"): the resident is evicted and re-inserted further along. This bounds the variance of probe lengths, so the worst-case lookup stays close to the average. Two consequences matter to callers:

- **Bounded probe variance.** On clustered or adversarial element distributions — where plain linear probing grows long runs and tail-latency lookups degrade toward O(n) — the displacement rule keeps every element close to its ideal slot.
- **Early-exit negative lookups.** The PSL invariant lets a *negative* `Contains` stop as soon as the probe distance exceeds the resident slot's PSL: if the element were present it would have displaced this shorter-distance resident, so it cannot be there. This is the common case for a set (presence checks, dedup guards). The cost is one extra `int` of PSL bookkeeping per slot and a small amount of displacement work per insert; deletion uses backward-shift-with-distance-decrement, so the table stays contiguous (no tombstones).

### When to choose it over `CeleritySet`

Reach for `RobinHoodSet` when your elements are **clustered or adversarial** and you want the worst-case `Contains` to track the average, or when negative lookups dominate and the PSL early-exit pays off. On uniform distributions with a good hasher it is typically a wash or a slight loss versus `CeleritySet` — the per-slot PSL `int` and the extra insert work are pure overhead there — so it is an opt-in type, not a default. Both are single-threaded and make no iteration-order guarantee.

### Constructors

```csharp
public RobinHoodSet(
    int capacity = 16,
    float loadFactor = 0.75f)

public RobinHoodSet(
    IEnumerable<T> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics, sizing (including the `ICollection<T>` count-with-load-factor-headroom rule), validation, and exceptions as `CeleritySet` — duplicate elements (including duplicate `default(T)`) are silently deduplicated. The backing table is always rounded up to the next power of two.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity < 0`.
- `ArgumentOutOfRangeException` if `loadFactor <= 0` or `loadFactor >= 1`.
- `ArgumentNullException` if `source` is `null` (enumerable overload).

### Methods

- `void Add(T item)` — throws `ArgumentException` on duplicate.
- `bool TryAdd(T item)` — `true` on success, `false` if already present.
- `bool Contains(T item)`
- `bool Remove(T item)`
- `void Clear()`
- `int EnsureCapacity(int capacity)` / `void TrimExcess()` / `void TrimExcess(int capacity)` — capacity management mirroring `CeleritySet`. The out-of-band `default(T)` slot is preserved.
- `int Count { get; }`
- `Enumerator GetEnumerator()` — struct enumerator; the out-of-band `default(T)` entry is yielded first when present.

### Default-element handling

Identical to `CeleritySet`: `default(T)` (`null` / `0` / `Guid.Empty` / …) is stored out-of-band via a `_hasDefaultValue` flag, so it never collides with the empty-slot sentinel and the hasher is never invoked with it (string hashers throw on `null`). Mutating the set during enumeration throws `InvalidOperationException` on the next `MoveNext` / `Reset`.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Clustered elements: Robin Hood keeps the worst-case Contains close to the average.
var seen = new RobinHoodSet<int, Int32WangNaiveHasher>();
seen.Add(42);
seen.Add(0); // default-element slot

Console.WriteLine(seen.Contains(42));   // True
Console.WriteLine(seen.Contains(999));  // False — the PSL invariant stops the probe early

foreach (var item in seen) { /* ... */ }
```

---

## HashCachingSet&lt;T, THasher&gt;

A drop-in peer of `CeleritySet` that takes the struct-of-arrays layout one step further: alongside the `items` array it keeps a dense side array of 32-bit hash **fingerprints**. It is the set counterpart of `HashCachingDictionary` — the same cached-fingerprint machinery with no value array. The public surface — constructors, `Add` / `TryAdd` / `Contains` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator`, `CopyTo`, and the full `ISet<T>` set-algebra surface (see [`CeleritySet`](#celerityset-t-thasher)) — is identical to `CeleritySet`. Only the probe representation differs.

```csharp
public class HashCachingSet<T, THasher> : ISet<T>
    where THasher : struct, IHashProvider<T>
```

### What the cached fingerprint does

Every occupied slot stores its element's hash with the top bit forced set (`hash | 0x80000000`), which makes the fingerprint always non-zero; an empty slot is the array default of `0`. A probe scan touches **only** the compact fingerprint buffer — comparing the cached fingerprint before it ever reads an element — so a candidate element is dereferenced (and the full equality check run) only on a fingerprint match. Two consequences matter to callers:

- **Cache-friendly probing.** The dense `int[]` metadata buffer packs many more slots per cache line than the element array, so cache-cold lookups walk metadata instead of chasing element references.
- **Short-circuited equality.** Elements with expensive equality (long strings, large structs) are compared in full only when their fingerprint matches, so negative lookups and colliding-slot probes reject on a single integer compare. Because the forced occupied bit sits above every power-of-two table mask, the cached fingerprint also yields the slot index directly (`fingerprint & mask`), so a resize re-homes every entry without recomputing a single hash. Deletion uses backward-shift (driven by the cached natural slot), so the table stays contiguous (no tombstones).

### When to choose it over `CeleritySet`

Reach for `HashCachingSet` on **lookup-dominated** workloads — large tables, many negative "have I seen this?" checks, or elements whose equality is expensive — where the fingerprint filter earns back its four bytes of metadata per slot. On tiny tables of cheap (e.g. `int`) elements it is roughly a wash versus `CeleritySet`, so it is an opt-in type, not a default. It is complementary to the SIMD-probing [`SwissSet`](#swissset-t-thasher): both cut probe cost, one via a control-byte group compare, the other via a cached fingerprint. Both are single-threaded and make no iteration-order guarantee.

### Constructors

```csharp
public HashCachingSet(
    int capacity = 16,
    float loadFactor = 0.75f)

public HashCachingSet(
    IEnumerable<T> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics, sizing (including the `ICollection<T>` count-with-load-factor-headroom rule), validation, and exceptions as `CeleritySet` — duplicate elements (including duplicate `default(T)`) are silently deduplicated. The backing table is always rounded up to the next power of two.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity < 0`.
- `ArgumentOutOfRangeException` if `loadFactor <= 0` or `loadFactor >= 1`.
- `ArgumentNullException` if `source` is `null` (enumerable overload).

### Methods

- `void Add(T item)` — throws `ArgumentException` on duplicate.
- `bool TryAdd(T item)` — `true` on success, `false` if already present.
- `bool Contains(T item)`
- `bool Remove(T item)`
- `void Clear()`
- `int EnsureCapacity(int capacity)` / `void TrimExcess()` / `void TrimExcess(int capacity)` — capacity management mirroring `CeleritySet`. The out-of-band `default(T)` slot is preserved.
- `int Count { get; }`
- `Enumerator GetEnumerator()` — struct enumerator; the out-of-band `default(T)` entry is yielded first when present.

### Default-element handling

Identical to `CeleritySet`: `default(T)` (`null` / `0` / `Guid.Empty` / …) is stored out-of-band via a `_hasDefaultValue` flag, so it never collides with the empty-slot sentinel (a `0` fingerprint) and the hasher is never invoked with it (string hashers throw on `null`). Mutating the set during enumeration throws `InvalidOperationException` on the next `MoveNext` / `Reset`.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Lookup-heavy dedup over long string keys: the cached fingerprint rejects
// most non-matches on a single integer compare, before any string equality.
var seen = new HashCachingSet<string, StringFnV1AHasher>();
seen.Add("alpha");
seen.Add(null!); // default-element slot

Console.WriteLine(seen.Contains("alpha")); // True
Console.WriteLine(seen.Contains("omega")); // False — rejected on the fingerprint compare

foreach (var item in seen) { /* ... */ }
```

---

## PooledCeleritySet<T, THasher>

An allocation-conscious peer of `CeleritySet` whose backing array is **rented from [`ArrayPool<T>.Shared`](https://learn.microsoft.com/dotnet/api/system.buffers.arraypool-1)** instead of being allocated on the managed heap. It is the set counterpart of `PooledCelerityDictionary` — the same rent / return lifecycle applied to a single element array rather than parallel key/value arrays. The public surface is identical to `CeleritySet` — same constructors, `Add` / `TryAdd` / `Contains` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator`, `CopyTo`, and the full `ISet<T>` set-algebra surface (see [`CeleritySet`](#celerityset-t-thasher)) — with one addition: it implements `IDisposable`.

```csharp
public class PooledCeleritySet<T, THasher> : ISet<T>, IDisposable
    where THasher : struct, IHashProvider<T>
```

### Why pooled storage

In high-throughput code that builds and tears down many short-lived sets (per request, per frame, per batch), the backing array is a steady source of Gen 0 garbage and, once it crosses the [85 KB Large Object Heap threshold](https://learn.microsoft.com/dotnet/standard/garbage-collection/large-object-heap), of LOH pressure that a normal `HashSet<T>` or `CeleritySet` cannot avoid. `PooledCeleritySet` borrows its element array from the shared pool and returns it on `Dispose` (and on every internal resize), so a build/use/dispose cycle reuses buffers across iterations rather than allocating fresh ones each time. The `PooledCeleritySetBenchmark` reports the difference in its `Allocated` column.

### When to choose it over `CeleritySet`

Reach for the pooled variant when the set is **short-lived and rebuilt frequently on a hot path**, GC pressure is a measured concern, and you can guarantee a `Dispose` (e.g. a `using` scope). For a long-lived set that lives for the life of the process, the pooling buys nothing and the disposal contract is pure overhead — stay on `CeleritySet`. Like every Celerity collection it is **not thread-safe**.

### Lifecycle and pooling contract

- **Dispose returns the buffer.** Call `Dispose` (ideally via `using`) when finished so the array returns to the pool for reuse. Disposal is idempotent, and after it every member throws `ObjectDisposedException`.
- **Not disposing is not a leak.** If you forget to dispose, the rented array is simply garbage-collected like any other managed array — you just forfeit the pooling benefit.
- **Pool exhaustion is handled for you.** `ArrayPool<T>.Shared` allocates a fresh buffer when it has none to hand out, so a "pool empty" condition never surfaces to the caller.
- **Reference types are cleared on return** so the pool does not keep your elements reachable after disposal (memory-leak prevention); value-type buffers skip the clear for speed.
- **Over-provisioned rents are handled.** `ArrayPool.Rent` may return an array larger than requested; the set tracks its logical power-of-two capacity independently and only ever reads or writes the live region, so the (uncleared) tail of an oversized buffer never surfaces in `Count` or enumeration.

### Constructors

```csharp
public PooledCeleritySet(
    int capacity = 16,
    float loadFactor = 0.75f)

public PooledCeleritySet(
    IEnumerable<T> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics, sizing (including the `ICollection<T>` count-with-load-factor-headroom rule), validation, and exceptions as `CeleritySet` — duplicate elements (including duplicate `default(T)`) are silently deduplicated.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity < 0`.
- `ArgumentOutOfRangeException` if `loadFactor <= 0` or `loadFactor >= 1`.
- `ArgumentNullException` if `source` is `null` (enumerable overload).

### Default-element handling

Identical to `CeleritySet`: `default(T)` (`null` / `0` / `Guid.Empty` / …) is stored out-of-band so it never collides with the empty-slot sentinel. Transparent to callers.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// A set built fresh on a hot path and thrown away each iteration — the rented
// buffer returns to the pool instead of becoming GC garbage.
using (var set = new PooledCeleritySet<int, Int32WangNaiveHasher>())
{
    set.Add(42);
    set.Add(0); // out-of-band default element

    if (set.Contains(42))
        Console.WriteLine("hit");

    foreach (var item in set)
        Console.WriteLine(item);
} // Dispose() returns the backing array to ArrayPool<T>.Shared here.
```

---

## IntSet

A convenience subclass of `IntSet<Int32WangNaiveHasher>` for the common case of integer sets.

```csharp
public class IntSet : IntSet<Int32WangNaiveHasher>
```

### Constructors

```csharp
public IntSet(
    int capacity = 16,
    float loadFactor = 0.75f)

public IntSet(
    IEnumerable<int> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics and validation as `IntSet<THasher>` (see below).

---

## IntSet&lt;THasher&gt;

A high-performance set of `int` values, parameterized on a custom hash provider. Implements `ISet<int>` (and therefore `ICollection<int>` / `IEnumerable<int>`) — the full `HashSet<int>` set-algebra surface (`UnionWith` / `IntersectWith` / `ExceptWith` / `SymmetricExceptWith` / `IsSubsetOf` / … / `SetEquals`, plus `CopyTo`) is available with BCL semantics; see [`CeleritySet`](#celerityset-t-thasher).

```csharp
public class IntSet<THasher> : ISet<int>
    where THasher : struct, IHashProvider<int>
```

### Constructors

```csharp
public IntSet(
    int capacity = 16,
    float loadFactor = 0.75f)

public IntSet(
    IEnumerable<int> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

The `IEnumerable<int>` overload copies elements from `source`, following the same `ICollection<T>`-sizing rule as `CeleritySet`. Duplicate elements (including the out-of-band zero element appearing more than once) are silently deduplicated, matching BCL `HashSet<int>(IEnumerable<int>)` semantics.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity < 0`.
- `ArgumentOutOfRangeException` if `loadFactor <= 0` or `loadFactor >= 1`.
- `ArgumentNullException` if `source` is `null` (enumerable overload).

### Methods

- `void Add(int item)` — throws `ArgumentException` on duplicate.
- `bool TryAdd(int item)`
- `bool Contains(int item)`
- `bool Remove(int item)`
- `void Clear()`
- `int EnsureCapacity(int capacity)` / `void TrimExcess()` / `void TrimExcess(int capacity)` — capacity management mirroring BCL `HashSet<T>` (the out-of-band zero element is preserved).
- `int Count { get; }`
- `Enumerator GetEnumerator()` — struct enumerator. The out-of-band zero entry is yielded first when present.

### Zero-element handling

The element `0` collides with the `EMPTY_SLOT` sentinel and is stored out-of-band, same pattern as `IntDictionary`'s zero-key handling.

### Usage example

```csharp
using Celerity.Collections;

var seen = new IntSet();
seen.Add(0);   // zero is fine
seen.Add(42);
Console.WriteLine(seen.Contains(0));  // True
Console.WriteLine(seen.Count);        // 2

foreach (int n in seen) { /* ... */ }
```

---

## LongSet

A convenience subclass of `LongSet<Int64WangNaiveHasher>` for the common case of 64-bit integer sets. Mirrors `IntSet` for `long` elements and defaults to the same `Int64WangNaiveHasher` `LongDictionary` uses.

```csharp
public class LongSet : LongSet<Int64WangNaiveHasher>
```

### Constructors

```csharp
public LongSet(
    int capacity = 16,
    float loadFactor = 0.75f)

public LongSet(
    IEnumerable<long> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

Same semantics and validation as `LongSet<THasher>` (see below).

---

## LongSet&lt;THasher&gt;

A high-performance set of `long` values, parameterized on a custom hash provider. Implements `ISet<long>` (and therefore `ICollection<long>` / `IEnumerable<long>`) — the full `HashSet<long>` set-algebra surface (`UnionWith` / `IntersectWith` / `ExceptWith` / `SymmetricExceptWith` / `IsSubsetOf` / … / `SetEquals`, plus `CopyTo`) is available with BCL semantics; see [`CeleritySet`](#celerityset-t-thasher).

```csharp
public class LongSet<THasher> : ISet<long>
    where THasher : struct, IHashProvider<long>
```

### Constructors

```csharp
public LongSet(
    int capacity = 16,
    float loadFactor = 0.75f)

public LongSet(
    IEnumerable<long> source,
    int capacity = 16,
    float loadFactor = 0.75f)
```

The `IEnumerable<long>` overload copies elements from `source`, following the same `ICollection<T>`-sizing rule as `IntSet`. Duplicate elements (including the out-of-band zero element appearing more than once) are silently deduplicated, matching BCL `HashSet<long>(IEnumerable<long>)` semantics.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity < 0`.
- `ArgumentOutOfRangeException` if `loadFactor <= 0` or `loadFactor >= 1`.
- `ArgumentNullException` if `source` is `null` (enumerable overload).

### Methods

- `void Add(long item)` — throws `ArgumentException` on duplicate.
- `bool TryAdd(long item)`
- `bool Contains(long item)`
- `bool Remove(long item)`
- `void Clear()`
- `int EnsureCapacity(int capacity)` / `void TrimExcess()` / `void TrimExcess(int capacity)` — capacity management mirroring BCL `HashSet<T>` (the out-of-band zero element is preserved).
- `int Count { get; }`
- `Enumerator GetEnumerator()` — struct enumerator. The out-of-band zero entry is yielded first when present.

### Zero-element handling

The element `0L` collides with the `EMPTY_SLOT` sentinel and is stored out-of-band, same pattern as `LongDictionary`'s zero-key handling.

### Usage example

```csharp
using Celerity.Collections;

var seen = new LongSet();
seen.Add(0L);            // zero is fine
seen.Add(long.MaxValue); // full 64-bit range
Console.WriteLine(seen.Contains(0L));  // True
Console.WriteLine(seen.Count);         // 2

foreach (long n in seen) { /* ... */ }
```

## FrozenCelerityDictionary&lt;TValue&gt;

```csharp
public sealed class FrozenCelerityDictionary<TValue>
    : FrozenCelerityDictionary<TValue, StringFnV1AHasher>
```

A build-once, read-many dictionary for `string` keys, in the spirit of the BCL
`System.Collections.Frozen.FrozenDictionary<TKey, TValue>` but tunable through
Celerity's `IHashProvider<T>`. The convenience type defaults to `StringFnV1AHasher`;
use the [generic overload](#frozenceleritydictionarytvalue-thasher) to supply a
different string hasher.

The dictionary is **immutable**: every key/value pair is supplied at construction and
there are no mutating members. In exchange the constructor searches a small parameter
space (table size × a mixing seed) for a **perfect** — collision-free — placement of
the keys. When one is found, a lookup is a single hash, a single array index, and a
single equality check: no probing, no probe chains.

### Constructors

```csharp
FrozenCelerityDictionary(IEnumerable<KeyValuePair<string, TValue>> source)
```

Freezes the supplied pairs. A single `null` key is allowed and stored out-of-band; the
empty string `""` is an ordinary key.

- Throws `ArgumentNullException` if `source` is `null`.
- Throws `ArgumentException` on a duplicate key (including a duplicate `null` key),
  matching BCL `FrozenDictionary` and the mutable Celerity dictionaries.
- Throws `ArgumentException` if `source` holds `2^30` or more distinct non-`null` keys —
  the frozen table is a power-of-two array and a fallback build needs at least one empty
  slot, so the non-`null` key count must stay below the `2^30` ceiling (`NextPowerOfTwo`
  caps there). In practice this is unreachable (a billion distinct string keys is tens of
  GB); the guard fails fast with a clear error rather than overflowing the build search.

### Properties

| Member | Description |
|---|---|
| `int Count` | Number of pairs, including the out-of-band `null`-key entry if present. |
| `bool IsPerfectlyHashed` | `true` when the build found a collision-free placement, so lookups take the single-probe fast path. `false` means it fell back to linear probing (see below). |

### Indexer

```csharp
TValue this[string key] { get; }
```

Get-only. Throws `KeyNotFoundException` if the key is absent.

### Methods

| Member | Description |
|---|---|
| `bool ContainsKey(string key)` | Whether the key is present. |
| `bool TryGetValue(string key, out TValue? value)` | Non-throwing lookup. |
| `bool ContainsValue(TValue? value)` | `O(n)` scan for a value (`EqualityComparer<T>.Default`). |
| `Enumerator GetEnumerator()` | Allocation-free struct enumerator; the `null`-key entry (if present) is yielded first. |
| `KeyCollection Keys` / `ValueCollection Values` | Allocation-free struct views. |

Implements `IReadOnlyDictionary<string, TValue?>`.

### The perfect-hash fast path and the fallback

A perfect (single-probe) placement is impossible when two distinct keys collide on the
chosen hasher's raw 32-bit hash code — for example `"A"` and `"Ł"` under the low-byte
`StringFnV1AHasher`, which returns the same code for both — because the mixing seed is a
pure function of that code and so cannot separate them. In that case the build falls
back to an open-addressed linear-probing table (`IsPerfectlyHashed` is then `false`).
**Lookups are always correct either way** — the equality check disambiguates colliding
keys — they simply cost a short probe instead of a single index. Supply a full-width or
strong hasher (`StringFnV1AFullHasher`, `StringMurmur3Hasher`, …) via the generic
overload if you want the perfect fast path for keys the default collides.

### Null-key handling

The `null` key is stored out-of-band — the hasher is never invoked with `null`, so it
never collides with the empty-slot sentinel. `ContainsKey(null)`, `this[null]`, and
`TryGetValue(null, out _)` all work; an absent `null` key misses like any other.

### Usage example

```csharp
using Celerity.Collections;

var routes = new FrozenCelerityDictionary<int>(new[]
{
    new KeyValuePair<string, int>("/",        0),
    new KeyValuePair<string, int>("/health",  1),
    new KeyValuePair<string, int>("/metrics", 2),
});

Console.WriteLine(routes.IsPerfectlyHashed);   // True (single-probe lookups)
Console.WriteLine(routes["/health"]);          // 1
Console.WriteLine(routes.ContainsKey("/nope")); // False

foreach (var kvp in routes) { /* ... */ }
```

## FrozenCelerityDictionary&lt;TValue, THasher&gt;

```csharp
public class FrozenCelerityDictionary<TValue, THasher>
    : IReadOnlyDictionary<string, TValue?>
    where THasher : struct, IHashProvider<string>
```

The hasher-parameterized base type of
[`FrozenCelerityDictionary<TValue>`](#frozenceleritydictionarytvalue). Identical API and
semantics; the only difference is that you choose the string hasher used to build and
probe the frozen table. Pick a full-width hasher (`StringFnV1AFullHasher`) for keys with
non-ASCII content, or a strong hasher (`StringMurmur3Hasher`, `StringXxHash3Hasher`) when
you want the perfect fast path for keys a cheaper hasher would collide.

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var byName = new FrozenCelerityDictionary<int, StringMurmur3Hasher>(new[]
{
    new KeyValuePair<string, int>("alice", 1),
    new KeyValuePair<string, int>("bob",   2),
});
Console.WriteLine(byName["alice"]); // 1
```

## FrozenCeleritySet

```csharp
public sealed class FrozenCeleritySet : FrozenCeleritySet<StringFnV1AHasher>
```

A build-once, read-many set of `string` elements — the set counterpart of
[`FrozenCelerityDictionary`](#frozenceleritydictionarytvalue), in the spirit of the
BCL `System.Collections.Frozen.FrozenSet<T>` but tunable through Celerity's
`IHashProvider<T>`. The convenience type defaults to `StringFnV1AHasher`; use the
[generic overload](#frozenceleritysetthasher) to supply a different string hasher.

The set is **immutable**: every element is supplied at construction and there are no
mutating members. In exchange the constructor searches a small parameter space (table
size × a mixing seed) for a **perfect** — collision-free — placement of the elements.
When one is found, a membership test is a single hash, a single array index, and a
single equality check: no probing, no probe chains.

### Constructors

```csharp
FrozenCeleritySet(IEnumerable<string> source)
```

Freezes the supplied elements. A single `null` element is allowed and stored
out-of-band; the empty string `""` is an ordinary element.

- Throws `ArgumentNullException` if `source` is `null`.
- Duplicate elements (including a duplicate `null`) are **silently deduplicated** —
  the defining property of a set, matching BCL `FrozenSet` and the mutable
  `CeleritySet`. (This is the one contract difference from `FrozenCelerityDictionary`,
  which *rejects* duplicate keys.)
- Throws `ArgumentException` if `source` holds `2^30` or more distinct non-`null`
  elements (the same power-of-two-table ceiling as `FrozenCelerityDictionary`;
  unreachable in practice, guarded for robustness).

### Properties

| Member | Description |
|---|---|
| `int Count` | Number of elements, including the out-of-band `null` element if present. |
| `bool IsPerfectlyHashed` | `true` when the build found a collision-free placement, so membership tests take the single-probe fast path. `false` means it fell back to linear probing (see below). |

### Methods

| Member | Description |
|---|---|
| `bool Contains(string item)` | Whether the element is present. |
| `Enumerator GetEnumerator()` | Allocation-free struct enumerator; the `null` element (if present) is yielded first. |

Implements `IReadOnlySet<string>`, so the set-algebra members
`SetEquals`, `IsSubsetOf`, `IsProperSubsetOf`, `IsSupersetOf`, `IsProperSupersetOf`,
and `Overlaps` are all available (each throws `ArgumentNullException` on a `null`
`other`). The superset / overlap shapes stream `other` directly against the `O(1)`
membership test; the subset / equality shapes materialize `other`'s distinct elements
once into an ordinal set, exactly as the BCL set types do internally.

### The perfect-hash fast path and the fallback

A perfect (single-probe) placement is impossible when two distinct elements collide on
the chosen hasher's raw 32-bit hash code — for example `"A"` and `"Ł"` under the
low-byte `StringFnV1AHasher`, which returns the same code for both — because the mixing
seed is a pure function of that code and so cannot separate them. In that case the
build falls back to an open-addressed linear-probing table (`IsPerfectlyHashed` is then
`false`). **Membership tests are always correct either way** — the equality check
disambiguates colliding elements — they simply cost a short probe instead of a single
index. Supply a full-width or strong hasher (`StringFnV1AFullHasher`,
`StringMurmur3Hasher`, …) via the generic overload if you want the perfect fast path
for elements the default collides.

### Null-element handling

The `null` element is stored out-of-band — the hasher is never invoked with `null`, so
it never collides with the empty-slot sentinel. `Contains(null)` works, and an absent
`null` element misses like any other.

### Usage example

```csharp
using Celerity.Collections;

var reserved = new FrozenCeleritySet(new[]
{
    "select", "from", "where", "join", "group", "order",
});

Console.WriteLine(reserved.IsPerfectlyHashed);     // True (single-probe membership)
Console.WriteLine(reserved.Contains("join"));      // True
Console.WriteLine(reserved.Contains("celerity"));  // False
Console.WriteLine(reserved.IsSupersetOf(new[] { "from", "where" })); // True

foreach (var keyword in reserved) { /* ... */ }
```

## FrozenCeleritySet&lt;THasher&gt;

```csharp
public class FrozenCeleritySet<THasher>
    : IReadOnlySet<string>
    where THasher : struct, IHashProvider<string>
```

The hasher-parameterized base type of [`FrozenCeleritySet`](#frozencelerityset).
Identical API and semantics; the only difference is that you choose the string hasher
used to build and probe the frozen table. Pick a full-width hasher
(`StringFnV1AFullHasher`) for elements with non-ASCII content, or a strong hasher
(`StringMurmur3Hasher`, `StringXxHash3Hasher`) when you want the perfect fast path for
elements a cheaper hasher would collide.

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var tags = new FrozenCeleritySet<StringMurmur3Hasher>(new[] { "alice", "bob" });
Console.WriteLine(tags.Contains("alice")); // True
```

## CelerityMultiMap&lt;TKey, TValue, THasher&gt;

```csharp
public class CelerityMultiMap<TKey, TValue, THasher>
    : ILookup<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
```

A **multi-map** (a.k.a. multi-dictionary or one-to-many map): each key maps to an
ordered *group* of values rather than a single value. It is the one-to-many
counterpart to [`CelerityDictionary`](#celeritydictionarytkey-tvalue-thasher) and
shares its storage — keys live in the same open-addressed, linear-probing table,
with the same struct-hasher constraint so the JIT devirtualizes and inlines the
key hash. Alongside each key slot is a value group (a `List<TValue?>` of the values
added under that key, in insertion order).

`Add` **always appends**: adding the same key twice groups the values rather than
rejecting the second add, and adding the same value twice under one key keeps both
copies. This is what you reach for when modelling one-to-many relationships — event
handlers per event, members per group, postings per term.

Reads are allocation-free: the indexer and `TryGetValues` hand back a lightweight
`ValueGroup` struct view over the live backing list, and the enumerator yields
struct `Grouping` values. The map is **not** allocation-free on the write path —
each *distinct* key allocates one backing list — which is inherent to storing a
group per key.

The map implements `ILookup<TKey, TValue?>`, so it flows through LINQ and any
consumer that accepts an `ILookup`. The indexer returns an **empty group for an
absent key** (matching `ILookup` semantics) rather than throwing.

### Constructors

```csharp
CelerityMultiMap(int capacity = 16, float loadFactor = 0.75f)
CelerityMultiMap(IEnumerable<KeyValuePair<TKey, TValue>> source,
                 int capacity = 16, float loadFactor = 0.75f)
```

- `capacity` is the initial *key* capacity, rounded up to the next power of two.
- Throws `ArgumentOutOfRangeException` for a negative `capacity` or a `loadFactor`
  outside the open interval `(0, 1)`.
- The `source` constructor groups pairs by key in source order; unlike a
  dictionary, **duplicate keys are not an error** — they are grouped. Throws
  `ArgumentNullException` if `source` is `null`.

### Properties

| Member | Description |
|---|---|
| `int Count` | Number of **distinct keys** (i.e. value groups), including the out-of-band default-key group if present. |
| `int ValueCount` | Total number of values across all keys (a key with `n` values, counting duplicates, contributes `n`). |

### Indexer

```csharp
ValueGroup this[TKey key] { get; }
```

Get-only. Returns a `ValueGroup` view over the values added under `key`, in
insertion order; returns an **empty group** if the key is absent (no throw).

### Methods

| Member | Description |
|---|---|
| `void Add(TKey key, TValue value)` | Append a value to the key's group, creating the group if absent. Always succeeds. |
| `void AddRange(TKey key, IEnumerable<TValue> values)` | Append all `values` to the key's group. Throws `ArgumentNullException` if `values` is `null`. |
| `bool Remove(TKey key, TValue? value)` | Remove a single occurrence of `value` (first match, by `EqualityComparer<T>.Default`) from the key's group. If that empties the group, the key is removed. Returns `false` if the key or value is absent. |
| `bool RemoveAll(TKey key)` | Remove the key and **all** of its values. Returns `false` if the key is absent. |
| `bool ContainsKey(TKey key)` | Whether the key has at least one value. |
| `bool Contains(TKey key, TValue? value)` | Whether `key` is present and its group contains `value`. |
| `bool ContainsValue(TValue? value)` | `O(ValueCount)` scan for a value under any key (`EqualityComparer<T>.Default`). |
| `int CountValues(TKey key)` | Number of values for the key, or `0` if absent. |
| `bool TryGetValues(TKey key, out ValueGroup values)` | Non-throwing group lookup. |
| `void Clear()` | Remove all keys and values; key capacity is preserved. |
| `int EnsureCapacity(int capacity)` | Pre-grow the **key** table to hold at least `capacity` distinct keys without resizing, returning the resulting key capacity. Only the key table is affected; value groups are untouched. Throws `ArgumentOutOfRangeException` on a negative capacity. |
| `void TrimExcess()` / `void TrimExcess(int capacity)` | Rehash the key table down to the smallest size that still holds the current distinct-key `Count` (or `capacity`). The out-of-band default-key group and the per-key value groups are preserved. `TrimExcess(capacity)` throws if `capacity < Count`. |
| `Enumerator GetEnumerator()` | Allocation-free struct enumerator yielding one `Grouping` per distinct key; the default-key group (if present) is yielded first. |
| `KeyCollection Keys` | Allocation-free struct view over the distinct keys. |

Implements `ILookup<TKey, TValue?>`: `Count` (distinct keys), `Contains(key)`,
the `IEnumerable<TValue?> this[key]` indexer (empty for an absent key), and
enumeration as `IGrouping<TKey, TValue?>`.

### Nested view types

- **`ValueGroup`** — a read-only struct view over one key's values. Implements
  `IReadOnlyList<TValue?>` (so `Count`, `this[int]`, and allocation-free `foreach`).
  It reflects the live backing group: mutating the map afterwards may change what a
  previously-obtained view yields.
- **`Grouping`** — a key together with its `ValueGroup`, yielded by the map's
  enumerator. Implements `IGrouping<TKey, TValue?>`, so `foreach (var g in map)`
  gives `g.Key` and `foreach (var v in g)` over the values.

### Default-key handling

`default(TKey)` (`null` for reference types, `0` for `int`, `Guid.Empty`, …)
collides with the empty-slot sentinel used during probing, so its value group is
stored **out-of-band** — the hasher is never invoked with the default key, so it
never collides with the sentinel. The default key behaves as an ordinary key for
`Add`, `Remove`, `RemoveAll`, the indexer, enumeration (yielded first), and `Keys`.

### Usage example

```csharp
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

// Subscribers per topic.
var subs = new CelerityMultiMap<string, string, StringFnV1AHasher>();
subs.Add("orders",   "billing");
subs.Add("orders",   "fulfilment");
subs.Add("shipments","tracking");

Console.WriteLine(subs.Count);                 // 2 distinct keys
Console.WriteLine(subs.ValueCount);            // 3 values
Console.WriteLine(subs["orders"].Count);       // 2
foreach (string handler in subs["orders"]) { /* billing, fulfilment */ }

subs.Remove("orders", "billing");              // drop one handler
subs.RemoveAll("shipments");                   // drop a whole topic

// Flows through LINQ as an ILookup<,>.
ILookup<string, string> lookup = subs;
var counts = lookup.ToDictionary(g => g.Key, g => g.Count());
```

## CelerityMultiSet&lt;T, THasher&gt;

```csharp
public class CelerityMultiSet<T, THasher>
    : IEnumerable<KeyValuePair<T, int>>
    where THasher : struct, IHashProvider<T>
```

A **counting multiset** (a.k.a. *bag* or *counter*): each distinct element maps to
its occurrence *count* (multiplicity) rather than being simply present or absent. It
is the natural sibling of [`CelerityMultiMap`](#celeritymultimaptkey-tvalue-thasher)
— where the multi-map maps one key to many values, the multiset maps one element to
a count — and shares the same open-addressed, linear-probing table and struct-hasher
constraint as [`CelerityDictionary`](#celeritydictionarytkey-tvalue-thasher).
Alongside each element slot is its multiplicity (a strictly positive `int`; an
element whose count would drop to zero is removed).

The headline workload is **frequency / histogram counting**. The idiomatic BCL
approach is `Dictionary<T,int>` with `d[x] = d.GetValueOrDefault(x) + 1`, which
performs *two* hash probes per item (one to read, one to write); `Add` does it in a
*single* probe-and-increment and runs the element hash through the devirtualized
struct hasher, so it also holds up on clustered / adversarial key shapes. The BCL
has no multiset / `Counter` type at all.

`Count` is the number of **distinct elements** (the number of entries you
enumerate); `TotalCount` is the sum of all multiplicities — mirroring
`CelerityMultiMap`'s `Count` / `ValueCount` split.

### Constructors

```csharp
CelerityMultiSet(int capacity = 16, float loadFactor = 0.75f)
CelerityMultiSet(IEnumerable<T> source, int capacity = 16, float loadFactor = 0.75f)
```

- `capacity` is the initial *distinct-element* capacity, rounded up to the next
  power of two.
- Throws `ArgumentOutOfRangeException` for a negative `capacity` or a `loadFactor`
  outside the open interval `(0, 1)`.
- The `source` constructor **counts occurrences**: each occurrence of an element
  increments its multiplicity, so a source with duplicate elements yields counts
  greater than one (it does **not** deduplicate). Throws `ArgumentNullException` if
  `source` is `null`.

### Properties

| Member | Description |
|---|---|
| `int Count` | Number of **distinct elements**, including the out-of-band default element if present. |
| `long TotalCount` | Sum of every element's multiplicity (an element added `n` times contributes `n`). |

### Indexer

```csharp
int this[T element] { get; set; }
```

Get returns the element's multiplicity (`0` if absent). Set is equivalent to
`SetCount(element, value)` — a value of `0` removes the element; a negative value
throws `ArgumentOutOfRangeException`.

### Methods

| Member | Description |
|---|---|
| `void Add(T element)` | Increment the element's multiplicity by one (creating it with count one if absent). Throws `OverflowException` if the count would exceed `int.MaxValue`. |
| `void Add(T element, int count)` | Add `count` occurrences. A `count` of `0` is a no-op (the element is not registered); a negative `count` throws `ArgumentOutOfRangeException`. |
| `bool Remove(T element)` | Remove one occurrence (decrement). If that empties the element, it is removed. Returns `false` if the element is absent. |
| `bool RemoveAll(T element)` | Remove the element entirely, discarding all occurrences. Returns `false` if the element is absent. |
| `int SetCount(T element, int count)` | Set the exact multiplicity (`0` removes; positive creates/overwrites), returning the **previous** count. Negative `count` throws. |
| `int GetCount(T element)` | The element's multiplicity, or `0` if absent (same as the indexer get). |
| `bool Contains(T element)` | Whether the element has a multiplicity of at least one. |
| `void Clear()` | Remove all elements; element capacity is preserved. |
| `int EnsureCapacity(int capacity)` | Pre-grow the element table to hold at least `capacity` distinct elements without resizing, returning the resulting capacity. Throws `ArgumentOutOfRangeException` on a negative capacity. |
| `void TrimExcess()` / `void TrimExcess(int capacity)` | Rehash the element table down to the smallest size that still holds the current distinct-element `Count` (or `capacity`). The out-of-band default element and every multiplicity are preserved. `TrimExcess(capacity)` throws if `capacity < Count`. |
| `Enumerator GetEnumerator()` | Allocation-free struct enumerator yielding one `KeyValuePair<T,int>` (element → count) per distinct element; the default element (if present) is yielded first. |
| `ElementCollection Elements` | Allocation-free struct view over the distinct elements (each yielded once, regardless of multiplicity). |

### Default-element handling

`default(T)` (`null` for reference types, `0` for `int`, `Guid.Empty`, …) collides
with the empty-slot sentinel used during probing, so its count is stored
**out-of-band** — the hasher is never invoked with the default element, so it never
collides with the sentinel. The default element behaves as an ordinary element for
`Add`, `Remove`, `RemoveAll`, `SetCount`, the indexer, enumeration (yielded first),
and `Elements`.

### Usage example

```csharp
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

// Word-frequency histogram.
var counts = new CelerityMultiSet<string, StringFnV1AHasher>();
foreach (string word in "the cat sat on the mat the".Split(' '))
    counts.Add(word);

Console.WriteLine(counts.Count);        // 5 distinct words
Console.WriteLine(counts.TotalCount);   // 7 total occurrences
Console.WriteLine(counts["the"]);       // 3

counts.Remove("the");                   // 2 left
counts.SetCount("cat", 0);              // remove "cat" entirely
counts["dog"] = 4;                      // set an exact multiplicity

// Enumerate (element, count) pairs, e.g. the top entry.
var top = counts.OrderByDescending(p => p.Value).First();
Console.WriteLine($"{top.Key}: {top.Value}");

// Count straight from a sequence.
var fromSeq = new CelerityMultiSet<int, Int32WangNaiveHasher>(new[] { 1, 1, 2, 3, 3, 3 });
Console.WriteLine(fromSeq[3]);          // 3
```

## SmallDictionary&lt;TKey, TValue&gt;

```csharp
public class SmallDictionary<TKey, TValue>
    : IReadOnlyDictionary<TKey, TValue?>
```

A dictionary tuned for the **very-small** case (`n <= ~16`), where a linear scan
over a flat backing array beats a probe-based hash table. This is the shape you
hit constantly in compilers and IL emitters (per-scope symbol tables), AST
attribute bags, and per-request maps — most instances stay tiny, and for a tiny
`n` the cost of computing a hash, masking it, and chasing a probe chain is pure
overhead next to a cache-friendly scan of a handful of keys.

Unlike the hash-table dictionaries, `SmallDictionary` stores entries in
insertion-dense parallel arrays and answers every query with a linear scan using
`EqualityComparer<TKey>.Default`. There is **no hasher** (and so no `THasher` type
parameter): you do not pick a hash function, because it never hashes. The
trade-offs that follow directly from that:

- Lookups, `Add`/`TryAdd` (duplicate detection), `ContainsKey`, and `Remove` are
  `O(n)` rather than `O(1)`. The type is built for small `n` and **degrades for
  large key sets** — keep it to the small-`n` workloads it is designed for. It does
  *not* auto-promote to a hash table; it simply grows its arrays and keeps scanning.
- Because nothing is hashed, there is **no empty-slot sentinel** and therefore no
  special-casing of `default(TKey)`. A `0`, `null`, or `Guid.Empty` key is stored
  inline like any other — a small simplification over the hash-table dictionaries,
  which keep the default key out-of-band.
- `Remove` moves the last entry into the vacated slot (an `O(1)` swap once the key
  is found), so the relative order of the surviving entries is not preserved.
  Enumeration order is unspecified in general.

It implements `IReadOnlyDictionary<TKey, TValue?>`, ships allocation-free struct
`Keys` / `Values` views and a struct enumerator, and accepts an
`IEnumerable<KeyValuePair<TKey, TValue>>` source at construction — the same surface
as the other Celerity dictionaries.

### Constructors

```csharp
SmallDictionary(int capacity = 4)
SmallDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source, int capacity = 4)
```

- `capacity` is the number of entries the backing arrays are sized for up front.
  Unlike the hash-table dictionaries it is used **verbatim** (it is not rounded to
  a power of two), since there is no probe mask. `0` defers allocation until the
  first insert.
- Throws `ArgumentOutOfRangeException` for a negative `capacity`. There is **no
  `loadFactor`** parameter.
- The `source` constructor copies pairs in order and throws `ArgumentException` on
  a duplicate key (matching the other dictionaries), and `ArgumentNullException` if
  `source` is `null` (the null check beats the capacity validation).

### Properties

| Member | Description |
|---|---|
| `int Count` | Number of key/value pairs. |

### Indexer

```csharp
TValue this[TKey key] { get; set; }
```

Get throws `KeyNotFoundException` if the key is absent. Set overwrites an existing
key or appends a new one; a pure overwrite never grows the backing arrays.

### Methods

| Member | Description |
|---|---|
| `bool ContainsKey(TKey key)` | `O(n)` scan for the key. |
| `bool ContainsValue(TValue? value)` | `O(n)` scan for the value (`EqualityComparer<T>.Default`). |
| `bool TryGetValue(TKey key, out TValue? value)` | Non-throwing lookup. |
| `void Add(TKey key, TValue value)` | Add a new pair; throws `ArgumentException` if the key exists. |
| `bool TryAdd(TKey key, TValue value)` | Add a new pair; returns `false` (no change) if the key exists. |
| `bool Remove(TKey key)` | Remove by key; returns `false` if absent. |
| `bool Remove(TKey key, out TValue? value)` | Remove by key, capturing the removed value. |
| `void Clear()` | Remove all entries; capacity is preserved. |
| `int EnsureCapacity(int capacity)` | Grow the backing arrays to hold at least `capacity` entries, returning the resulting array length. Like the constructor, the length is **verbatim** (not rounded to a power of two), since there is no probe mask. Throws `ArgumentOutOfRangeException` on a negative capacity. |
| `void TrimExcess()` / `void TrimExcess(int capacity)` | Shrink the backing arrays to exactly the current `Count` (or `capacity`), reclaiming memory. `TrimExcess(capacity)` throws if `capacity < Count`. |
| `Enumerator GetEnumerator()` | Allocation-free struct enumerator over the pairs. |
| `KeyCollection Keys` / `ValueCollection Values` | Allocation-free struct views. |

### Default-key handling

A `0`, `null`, or `Guid.Empty` key is an ordinary inline entry — store it, look it
up, and remove it exactly like any other key. There is no out-of-band slot.

### Usage example

```csharp
using Celerity.Collections;

// A tiny per-scope symbol table — almost always a handful of entries.
var scope = new SmallDictionary<string, int>();
scope["x"] = 1;
scope["y"] = 2;
scope.TryAdd("x", 99);            // false — already present, unchanged

Console.WriteLine(scope["x"]);    // 1
Console.WriteLine(scope.Count);   // 2

if (scope.TryGetValue("y", out int y)) { /* y == 2 */ }

scope.Remove("x");                // O(1) swap-removal after the scan
foreach (var kvp in scope) { /* ("y", 2) */ }
```

---

## BloomFilter&lt;T, THasher&gt;

A space-efficient **probabilistic** set membership filter parameterized on a custom
hash provider. It answers "is this element *possibly* in the set?" using nothing but
a bit array and a handful of hash functions, so for membership-only workloads it uses
a small fraction of the memory of a `HashSet<T>` and never grows with element size.

```csharp
public class BloomFilter<T, THasher>
    where THasher : struct, IHashProvider<T>
```

The contract is the defining Bloom-filter trade-off:

- **No false negatives.** `Contains` returning `false` is always correct — the element
  was definitely never added.
- **Bounded false positives.** `Contains` returning `true` is *probably* correct, with a
  tunable probability of being a false positive once the filter holds its expected
  element count.
- **No `Remove`.** Clearing a single bit could erase membership for an unrelated element
  that hashed onto it, introducing false negatives. Use `Clear()` to reset the whole
  filter; reach for a counting-filter variant if per-element deletion is required.

### Sizing

The filter sizes itself at construction from the expected element count `n` and the
target false-positive rate `p`, using the standard optimal formulas:

- Bit count `m = ceil(-n·ln(p) / (ln 2)²)`, then rounded **up to a power of two** so a bit
  index is computed with a mask rather than a modulo. The extra bits only lower the
  realized false-positive rate, never raise it.
- Hash count `k = round((m/n)·ln 2)`, at least one.

The `k` bit positions for an element are derived from a **single** `IHashProvider<T>`
call by double hashing (Kirsch–Mitzenmacher): the 32-bit base hash is avalanched into
64 bits whose two halves seed the recurrence `g_i = h1 + i·h2`, so adding more hash
functions costs arithmetic, not more `Hash()` calls.

### Constructors

```csharp
public BloomFilter(
    int expectedItems,
    double falsePositiveRate = 0.01)

public BloomFilter(
    IEnumerable<T> source,
    double falsePositiveRate = 0.01)
```

The first overload creates an empty filter sized for `expectedItems` elements at the
target `falsePositiveRate`.

The `IEnumerable<T>` overload pre-populates the filter and sizes it from the source's
element count — taken from `ICollection<T>.Count` when available, otherwise from a single
counting pass — so the realized false-positive rate honors `falsePositiveRate`.

**Throws:**

- `ArgumentOutOfRangeException` if `expectedItems <= 0`.
- `ArgumentOutOfRangeException` if `falsePositiveRate <= 0`, `>= 1`, or `NaN`.
- `ArgumentNullException` if `source` is `null` (enumerable overload). This check beats
  the rate validation, so a `null` source with a bad rate surfaces as
  `ArgumentNullException`.

### Methods and properties

- `void Add(T item)` — adds an element. Adding the same element twice is a no-op for
  membership but still increments `Count`.
- `bool Contains(T item)` — `false` ⇒ definitely absent; `true` ⇒ probably present.
- `void UnionWith(BloomFilter<T, THasher> other)` — merges another filter in place (bitwise
  OR), so this filter afterwards reports `true` for every element either held. Throws
  `ArgumentNullException` on a `null` argument and `ArgumentException` if the two filters
  have a different `BitCount` or `HashCount`.
- `void Clear()` — resets every bit; preserves the bit-array size and hash count.
- `int Count { get; }` — the number of `Add` calls since construction or the last `Clear`.
  This is an **insertion counter, not a distinct-element count** — a Bloom filter cannot
  tell whether an element was already present.
- `int Capacity { get; }` — the expected element count the filter was sized for.
- `int BitCount { get; }` — the number of bits in the backing array (`m`), a power of two.
- `int HashCount { get; }` — the number of hash functions applied per element (`k`).
- `double FalsePositiveRate { get; }` — the target rate the filter was sized for.
- `double CurrentFalsePositiveProbability { get; }` — an estimate of the *current* false-positive
  probability from how many bits are actually set (`(setBits / m)^k`). Climbs past
  `FalsePositiveRate` once the filter is holding more than its expected element count.

### Default-element handling

Because the filter stores only bits there is no empty-slot sentinel, so unlike the
hash-table collections it needs **no out-of-band handling** for `default(T)` — a zero
`int`, `Guid.Empty`, or the empty string is hashed and added like any other element. A
`null` reference is mapped to a fixed base hash so the filter never invokes the hasher
with `null` (the string hashers throw on `null`), matching the library's
out-of-band-`null` convention.

### Choosing it

Reach for `BloomFilter` when you need a **membership gate** and can tolerate a small,
bounded false-positive rate in exchange for a large memory saving: deduplication
pre-filters, "have I seen this URL / key / id before?" guards in front of an expensive
exact lookup (a database, a cache, a disk index), and set-reconciliation sketches. If
you need exact membership, enumeration, removal, or to retrieve the stored elements, use
`CeleritySet<T, THasher>` (or `FrozenCeleritySet` for build-once string sets) instead.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Sized for 1,000,000 expected URLs at a 0.1% false-positive rate.
var seen = new BloomFilter<string, StringMurmur3Hasher>(1_000_000, 0.001);

foreach (var url in crawlFrontier)
{
    if (seen.Contains(url))
        continue;              // probably already crawled — skip (0.1% may skip a new one)

    seen.Add(url);
    Crawl(url);                // definitely new — no false negatives
}

Console.WriteLine(seen.BitCount);    // power-of-two bit count, m
Console.WriteLine(seen.HashCount);   // hash functions per element, k
```

---

## CuckooFilter&lt;T, THasher&gt;

A space-efficient **probabilistic** set membership filter that — unlike `BloomFilter` —
**supports deletion**, parameterized on a custom hash provider. Like a Bloom filter it
answers "is this element *possibly* in the set?" with **no false negatives** and a tunable,
bounded false-positive rate, but instead of a bit array it stores a short *fingerprint* of
each element in a table of fixed-size buckets — which is what lets it answer the one
question a Bloom filter cannot: `Remove` deletes a single element without introducing false
negatives for the others. The BCL ships no probabilistic membership filter, so for
membership-only workloads this uses a small fraction of the memory of a `HashSet<T>`.

```csharp
public class CuckooFilter<T, THasher>
    where THasher : struct, IHashProvider<T>
```

### How it works

The structure is **partial-key cuckoo hashing** (Fan, Andersen, Kaminsky, Mitzenmacher —
*"Cuckoo Filter: Practically Better Than Bloom"*, CoNEXT 2014). Each element has two
candidate buckets, `i1 = h(x)` and `i2 = i1 XOR h(fingerprint)`; because the bucket count
is a power of two the XOR is an involution, so `i1` can be recovered from `i2` and the
stored fingerprint alone — no need to keep the original key. An insert places the
fingerprint in either candidate bucket; when both are full it evicts a resident fingerprint
and re-homes it in *its* alternate bucket, repeating up to a bounded number of "kicks". The
fingerprint and the primary index both come from a **single** `IHashProvider<T>.Hash` call
avalanched into 64 bits (the SplitMix64 finalizer). A lookup or delete touches at most two
buckets (≈ two cache lines) regardless of fill.

### Cuckoo vs. Bloom

| | `BloomFilter` | `CuckooFilter` |
|---|---|---|
| No false negatives | ✅ | ✅ |
| Tunable false-positive rate | ✅ | ✅ |
| **Delete individual elements** | ❌ | ✅ |
| Lookup cost | `k` bit probes | ≤ 2 buckets (≈ 2 cache lines) |
| Insertion can fail when very full | ❌ (never) | ✅ (at high load — reports *full*) |
| Storage | bit array | `f`-bit fingerprints (one `ushort` each) |

Reach for `CuckooFilter` when you need **deletable** approximate membership — a sliding
window of "seen" keys, a cache-admission filter, a set that shrinks as items expire. If your
working set only ever grows (or you reset it wholesale), `BloomFilter` is simpler and can be
more compact at high target false-positive rates.

### Sizing

The filter sizes itself at construction from the expected element count `n` and the target
false-positive rate `p`:

- **Fingerprint width** `f = ceil(log2(2·4 / p))` bits (four slots per bucket, two candidate
  buckets), clamped to `[1, 16]`. A lower target rate widens the fingerprint. The achievable
  minimum rate is bounded by the 16-bit ceiling (≈ `8 / 2¹⁶ ≈ 1.2e-4`); a stricter request is
  clamped to that floor.
- **Bucket count**, rounded **up to a power of two** from `n / (4 · 0.94)`, so the alternate-bucket
  XOR stays in range and the realized table runs below full.

### Constructors

```csharp
public CuckooFilter(
    int expectedItems,
    double falsePositiveRate = 0.01)

public CuckooFilter(
    IEnumerable<T> source,
    double falsePositiveRate = 0.01)
```

The first overload creates an empty filter sized for `expectedItems` elements. The
`IEnumerable<T>` overload pre-populates the filter and sizes it from the source's element
count — taken from `ICollection<T>.Count` when available, otherwise from a single counting
pass.

**Throws:**

- `ArgumentOutOfRangeException` if `expectedItems <= 0`.
- `ArgumentOutOfRangeException` if `falsePositiveRate <= 0`, `>= 1`, or `NaN`.
- `ArgumentNullException` if `source` is `null` (enumerable overload). This check beats the
  rate validation, so a `null` source with a bad rate surfaces as `ArgumentNullException`.
- `InvalidOperationException` (enumerable overload) if the filter becomes full before every
  element is added.

### Methods and properties

- `void Add(T item)` — adds an element. Adding the same element twice stores a second
  fingerprint copy and counts twice; a matching `Remove` removes one copy. **Throws
  `InvalidOperationException`** if the filter is full (an insertion exhausted its eviction
  budget) — only when loaded well beyond `Capacity`.
- `bool TryAdd(T item)` — the non-throwing form: returns `false` (leaving the filter
  unchanged) when full.
- `bool Contains(T item)` — `false` ⇒ definitely absent; `true` ⇒ probably present.
- `bool Remove(T item)` — removes one matching copy; returns `true` if found. **Only remove
  elements you know were added** — removing a never-added element can, with the false-positive
  probability, delete a different element that shares its fingerprint and bucket.
- `void UnionWith(CuckooFilter<T, THasher> other)` — merges another filter in place; both must
  have identical `BucketCount` and `FingerprintBits`. Throws `ArgumentNullException` on `null`,
  `ArgumentException` on incompatible geometry, and `InvalidOperationException` if this filter
  fills before absorbing all of `other`.
- `void Clear()` — resets to empty; preserves the bucket count and fingerprint width.
- `int Count { get; }` — the number of stored fingerprints (a **live** count: `Add` increments,
  `Remove` decrements).
- `int Capacity { get; }` — the expected element count the filter was sized for.
- `int BucketCount { get; }` — the number of buckets (a power of two; the table holds
  `BucketCount · 4` slots).
- `int FingerprintBits { get; }` — the fingerprint width in bits (`f`), in `[1, 16]`.
- `double FalsePositiveRate { get; }` — the target rate the filter was sized for.
- `double LoadFactor { get; }` — the fraction of slots occupied; insertions begin to risk
  failure as this approaches ~0.95.
- `bool IsFull { get; }` — whether an insertion has exhausted its eviction budget (a fingerprint
  is parked in the single-entry victim cache); a successful `Remove` clears it.

### Default-element handling

Because the filter stores fingerprints, not keys, it needs **no out-of-band handling** for
`default(T)` — a zero `int`, `Guid.Empty`, or the empty string is hashed and added like any
other element. A `null` reference is mapped to a fixed base hash so the filter never invokes
the hasher with `null` (the string hashers throw on `null`).

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// A sliding window of recently-seen request ids, sized for ~100k live entries.
var recent = new CuckooFilter<long, Int64WangHasher>(100_000, 0.001);

void OnRequest(long id)
{
    if (recent.Contains(id))
        return;                 // probably a retry — drop it
    recent.Add(id);
    Process(id);
}

void OnExpire(long id) => recent.Remove(id);   // shrink the window — Bloom cannot do this
```

---

## BitSet

A dense, fixed-length array of bits packed into 64-bit words. It is the **exact,
deterministic** counterpart to the probabilistic `BloomFilter<T, THasher>`: where a
Bloom filter trades exactness for memory, a `BitSet` stores one bit per index with no
error. It is a drop-in alternative to `System.Collections.BitArray` tuned for two
operations the BCL type does not offer directly:

```csharp
public sealed class BitSet : IEnumerable<bool>
```

- **Population count.** `Count` returns the number of set bits in `O(Length / 64)` using
  a hardware population count per 64-bit word (`BitOperations.PopCount`). `BitArray`
  exposes no cardinality at all, forcing callers into a bit-by-bit loop.
- **SIMD-accelerated bulk boolean ops.** `And` / `Or` / `Xor` / `Not` combine the whole
  vector a `Vector<ulong>` at a time when the hardware accelerates it (falling back to a
  scalar 64-bit-word loop otherwise), where `BitArray` walks 32-bit words.

Bit `i` lives in word `i / 64` at bit position `i % 64`. Any bits in the final word
beyond `Length` are kept clear at all times — after `SetAll`, `Not`, and the bulk
operators — so `Count`, `Any`, and `All` never observe a stray out-of-range bit.

### Constructors

```csharp
public BitSet(int length)
public BitSet(int length, bool defaultValue)
public BitSet(bool[] values)
```

- `BitSet(int length)` — a set of `length` bits, all clear. `length` of `0` is a valid
  empty set.
- `BitSet(int length, bool defaultValue)` — every bit initialized to `defaultValue`.
- `BitSet(bool[] values)` — bit `i` is set iff `values[i]` is `true`; the set's length
  is the array's length.

**Throws:**

- `ArgumentOutOfRangeException` if `length` is negative.
- `ArgumentNullException` if `values` is `null`.

### Methods and properties

- `int Length { get; }` — the number of bits in the set.
- `int Count { get; }` — the number of set bits (population count), in `O(Length / 64)`.
- `bool this[int index] { get; set; }` — gets or sets a single bit.
- `bool Get(int index)` / `void Set(int index, bool value)` — single-bit access; both
  throw `ArgumentOutOfRangeException` for an index outside `[0, Length)`.
- `bool Flip(int index)` — toggles a bit and returns its new value.
- `void SetAll(bool value)` — sets every bit to `value`.
- `void Clear()` — clears every bit (equivalent to `SetAll(false)`).
- `BitSet And(BitSet other)` / `Or` / `Xor` — in-place bitwise combine with another
  equal-length set, returning `this` for chaining. Throw `ArgumentNullException` on a
  `null` argument and `ArgumentException` if the lengths differ.
- `BitSet Not()` — inverts every bit in place (one's complement), returning `this`.
- `bool Any()` — `true` if any bit is set.
- `bool All()` — `true` if every bit is set (an empty set is vacuously `true`).
- `bool None()` — the negation of `Any()`.
- `SetBitEnumerable EnumerateSetBits()` — an allocation-free enumerable over the indices
  of the set bits in ascending order, skipping clear words a whole word at a time and
  locating set bits within a word via `BitOperations.TrailingZeroCount`.
- `Enumerator GetEnumerator()` — an allocation-free struct enumerator yielding each bit's
  *value* (`bool`) from index `0` to `Length - 1`, mirroring `BitArray`'s `IEnumerable`.

Both enumerators are invalidated by any structural mutation and throw
`InvalidOperationException` if the set is modified mid-iteration.

### Choosing it

Reach for `BitSet` when you have a **dense set of small integer indices** or a fixed
universe of flags and you care about counting set bits or combining whole vectors:
bitmap indexes, presence/visited masks over a contiguous id space, sieve-style
algorithms, and feature/permission flag sets. If your indices are sparse over a huge or
unbounded domain, a hash-based `IntSet` / `LongSet` is more memory-efficient; if you
only need approximate membership over arbitrary elements at a fraction of the memory,
use `BloomFilter<T, THasher>`.

### Usage example

```csharp
using Celerity.Collections;

// Sieve of Eratosthenes over [0, n): composite[i] == true means i is composite.
int n = 1_000_000;
var composite = new BitSet(n);
for (int p = 2; (long)p * p < n; p++)
{
    if (composite[p]) continue;
    for (int m = p * p; m < n; m += p)
        composite[m] = true;
}

// Flip to "is prime", clear 0 and 1, and count — all without a per-bit loop.
composite.Not();
composite[0] = false;
composite[1] = false;
Console.WriteLine(composite.Count);   // number of primes below n

// Walk the primes directly via the set-bit enumerator (skips runs of composites).
foreach (int prime in composite.EnumerateSetBits())
    Process(prime);

// Bulk set algebra: intersect two equal-length masks in place.
var aMask = new BitSet(n);
var bMask = new BitSet(n);
aMask.And(bMask);                     // SIMD over 64-bit words
```

---

## HyperLogLog&lt;T, THasher&gt;

A space-efficient **probabilistic cardinality estimator** parameterized on a custom
hash provider. It answers "roughly how many *distinct* elements have I seen?" from a
fixed array of small registers whose size does **not** grow with the number of
elements, so it counts the distinct values in a stream of any size from a few kilobytes
of memory — where a `HashSet<T>` must store every distinct element to count them.

```csharp
public class HyperLogLog<T, THasher>
    where THasher : struct, IHashProvider<T>
```

The contract is the defining HyperLogLog trade-off:

- **Fixed, tiny memory.** The estimator allocates `m = 2^precision` one-byte registers
  (16&#160;KB at the default precision 14) and never grows, whether it counts a thousand
  distinct elements or a billion.
- **Bounded relative error.** `EstimateCardinality()` returns an *approximate* distinct
  count with a relative standard error of about `1.04 / sqrt(m)` (≈ 0.8% at the default
  precision), rather than an exact value.
- **Add-and-estimate only.** Like a Bloom filter it cannot remove an element or report
  whether a specific element was seen — it tracks only the aggregate distinct count. Use
  `Clear()` to reset, or `UnionWith` to combine two estimators.

### How it works

Each element is hashed to 64 bits, routed to one of the `m` registers by its top
`precision` bits, and the register records the largest "rank" — one plus the number of
leading zeros — seen in the remaining bits. A stream of `n` distinct elements fills the
registers with a predictable rank pattern, and the harmonic mean of `2^register` across
all registers recovers an estimate of `n` (Flajolet et&#160;al., 2007). The estimate
applies the standard small-range **linear counting** correction, so low cardinalities —
where many registers are still zero — are estimated accurately rather than by the
bias-prone raw formula. No large-range correction is needed because the 64-bit hash
space dwarfs any realistic cardinality.

The 64-bit hash is derived from a **single** `IHashProvider<T>` call by avalanching the
32-bit base hash with the SplitMix64 finalizer (a bijection on 64 bits, so distinct base
hashes stay distinct), so any existing hasher plugs in unchanged and `Add` costs one
`Hash()` call.

### Constructors

```csharp
public HyperLogLog(int precision = 14)

public HyperLogLog(
    IEnumerable<T> source,
    int precision = 14)
```

The first overload creates an empty estimator with `2^precision` registers. The
`IEnumerable<T>` overload pre-populates it with the source's distinct elements.

`precision` must be between `MIN_PRECISION` (4, `m = 16`) and `MAX_PRECISION` (16,
`m = 65536`) inclusive. Larger values cost more memory but lower the standard error.

**Throws:**

- `ArgumentOutOfRangeException` if `precision` is outside `[MIN_PRECISION, MAX_PRECISION]`.
- `ArgumentNullException` if `source` is `null` (enumerable overload). This check beats
  the precision validation, so a `null` source with a bad precision surfaces as
  `ArgumentNullException`.

### Methods and properties

- `void Add(T item)` — adds an element, updating the register its hash routes to. Adding
  an element already represented is a no-op for the estimate (a register only increases).
- `long EstimateCardinality()` — the estimated number of distinct elements, rounded to
  the nearest whole number. An `O(m)` pass over the registers; an empty estimator returns
  `0` exactly.
- `void UnionWith(HyperLogLog<T, THasher> other)` — merges another estimator in place
  (per-register maximum), so this estimator afterwards estimates the cardinality of the
  **union** of both input streams. Unlike a Bloom-filter union this introduces no error
  beyond the usual estimate of the merged set. Throws `ArgumentNullException` on a `null`
  argument and `ArgumentException` if the two estimators have a different `Precision`.
- `void Clear()` — resets every register; preserves the precision and register count.
- `int Precision { get; }` — the register-index precision (`p`).
- `int RegisterCount { get; }` — the number of registers (`m = 2^precision`).
- `double StandardError { get; }` — the relative standard error, ≈ `1.04 / sqrt(m)`.

### Default-element handling

Because the estimator stores only register ranks there is no empty-slot sentinel, so
unlike the hash-table collections it needs **no out-of-band handling** for `default(T)` —
a zero `int`, `Guid.Empty`, or the empty string is hashed and counted like any other
element. A `null` reference is mapped to a fixed base hash so the estimator never invokes
the hasher with `null` (the string hashers throw on `null`), matching the library's
out-of-band-`null` convention.

### Choosing it

Reach for `HyperLogLog` when you need a **distinct count over a large or unbounded
stream** and can tolerate a small, bounded relative error in exchange for fixed,
tiny memory: unique-visitor / unique-event counting, distinct-value cardinality in
analytics and query planners, and deduplicated counting across distributed shards (count
locally, then `UnionWith` the partial estimators). If you need an exact count, to test
membership of a specific element, or to retrieve the stored elements, use a
`HashSet<T>` / `CeleritySet<T, THasher>` instead; for approximate *membership* (rather
than counting), use `BloomFilter<T, THasher>`.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Count distinct visitor ids in a high-volume stream from ~16 KB of registers.
var uniqueVisitors = new HyperLogLog<long, Int64Murmur3Hasher>();

foreach (long visitorId in eventStream)
    uniqueVisitors.Add(visitorId);

Console.WriteLine(uniqueVisitors.EstimateCardinality()); // ≈ distinct visitors (±0.8%)

// Merge two shards' partial estimators to count distinct across both.
var shardA = new HyperLogLog<long, Int64Murmur3Hasher>(shardAIds);
var shardB = new HyperLogLog<long, Int64Murmur3Hasher>(shardBIds);
shardA.UnionWith(shardB);
Console.WriteLine(shardA.EstimateCardinality());         // distinct across A ∪ B
```

## CountMinSketch&lt;T, THasher&gt;

A space-efficient **probabilistic frequency estimator** parameterized on a custom hash
provider. It answers "roughly how many times have I seen this element?" from a fixed grid
of counters whose size does **not** grow with the number of distinct elements, so it
estimates per-element frequencies in a stream of any size from a few kilobytes of memory —
where a `Dictionary<TKey, int>` frequency table must store every distinct key to count it.

```csharp
public class CountMinSketch<T, THasher>
    where THasher : struct, IHashProvider<T>
```

The contract is the defining Count-Min trade-off:

- **Fixed, small memory.** The sketch allocates a `depth × width` grid of counters sized
  from the error parameters and never grows, whether it counts a thousand distinct
  elements or a billion.
- **One-sided error.** `EstimateCount(item)` **never underestimates** an element's true
  frequency. With probability at least `1 − delta` it overestimates by no more than
  `epsilon · TotalCount` (collisions can only inflate counters, never deflate them).
- **Add-and-estimate only.** Like a Bloom filter it has no `Remove` (decrementing a
  counter could push an unrelated element's estimate below its true frequency, breaking the
  never-underestimate guarantee). Use `Clear()` to reset, or `UnionWith` to combine two
  sketches.
- **Saturating counters.** A counter (and `TotalCount`) that would exceed `long.MaxValue`
  clamps there rather than wrapping to a negative value, so the never-underestimate
  guarantee holds even under counts larger than an in-memory sketch could otherwise
  represent (whether reached via `Add(item, count)` or `UnionWith`).

### How it works

The sketch is a grid of `depth` rows × `width` counters. Each element is mapped to one
counter per row; `Add` increments those `depth` counters by the added amount, and
`EstimateCount` returns the **minimum** across them. Because every counter an element
touches accumulates that element's full count plus only non-negative contributions from
colliding elements, the minimum is the tightest of the `depth` overestimates and can never
fall below the truth (Cormode & Muthukrishnan, 2005).

The grid is sized from two error parameters: the relative error factor `epsilon` drives the
per-row counter count `w = ceil(e / epsilon)` (rounded up to a power of two so a column
index is a mask, not a modulo), and the failure probability `delta` drives the row count
`d = ceil(ln(1 / delta))`. The `d` counter columns for an element are derived from a
**single** `IHashProvider<T>` call by double hashing (Kirsch–Mitzenmacher): the 32-bit base
hash is avalanched into 64 bits whose two halves seed the recurrence `g_i = h1 + i·h2`
(the stride forced odd so the rows spread out), so adding rows costs arithmetic, not more
`Hash()` calls.

### Constructors

```csharp
public CountMinSketch(
    double epsilon = 0.01,
    double delta = 0.01)

public CountMinSketch(
    IEnumerable<T> source,
    double epsilon = 0.01,
    double delta = 0.01)
```

The first overload creates an empty sketch sized for the given error parameters. The
`IEnumerable<T>` overload pre-populates it by adding each element once (so duplicates in the
source raise the estimated count).

`epsilon` and `delta` must each be strictly between 0 and 1. Smaller `epsilon` widens each
row (lowering the error); smaller `delta` adds rows (raising the confidence). The width is
capped at `2^30` counters per row, and the **total** grid (`depth × width`) is capped at
`2^30` counters — a combination of `epsilon` and `delta` that would need a larger grid (for
example `epsilon = 1e-9` with `delta = 0.01`, which clamps the width to `2^30` and still asks
for several rows) is rejected rather than silently overflowing the allocation. Realistic
parameters stay far below this ceiling; if you hit it, relax `epsilon` and/or `delta`.

**Throws:**

- `ArgumentOutOfRangeException` if `epsilon` or `delta` is not strictly between 0 and 1, or if
  the two together demand a counter grid larger than `2^30` counters.
- `ArgumentNullException` if `source` is `null` (enumerable overload). This check beats the
  error-parameter validation, so a `null` source with a bad `epsilon` surfaces as
  `ArgumentNullException`.

### Methods and properties

- `void Add(T item)` — adds one occurrence, increasing the element's estimated frequency by
  one.
- `void Add(T item, long count)` — adds `count` occurrences. Throws
  `ArgumentOutOfRangeException` if `count` is not positive. A counter (and `TotalCount`)
  saturates at `long.MaxValue` rather than overflowing to a negative value.
- `long EstimateCount(T item)` — the estimated frequency of an element. Never less than the
  true count; with probability at least `1 − Delta` it exceeds it by no more than
  `Epsilon · TotalCount`. An element never added returns `0` unless collisions inflate it.
- `void UnionWith(CountMinSketch<T, THasher> other)` — merges another sketch in place
  (elementwise counter addition), so this sketch afterwards estimates frequencies over the
  combined streams. The result is exactly the counter state adding both streams to one
  sketch would have produced. Throws `ArgumentNullException` on a `null` argument and
  `ArgumentException` if the two sketches have a different `Width` or `Depth`.
- `void Clear()` — resets every counter; preserves the grid dimensions.
- `int Width { get; }` — the number of counters per row (`w`), a power of two.
- `int Depth { get; }` — the number of rows (`d`), the number of estimates minimized over.
- `double Epsilon { get; }` — the relative error factor the sketch was sized for.
- `double Delta { get; }` — the failure probability the sketch was sized for.
- `long TotalCount { get; }` — the total of all counts added (the `L1` norm the `Epsilon`
  bound is relative to).

### Default-element handling

Because the sketch stores only counters there is no empty-slot sentinel, so unlike the
hash-table collections it needs **no out-of-band handling** for `default(T)` — a zero
`int`, `Guid.Empty`, or the empty string is hashed and counted like any other element. A
`null` reference is mapped to a fixed base hash so the sketch never invokes the hasher with
`null` (the string hashers throw on `null`), matching the library's out-of-band-`null`
convention.

### Choosing it

Reach for `CountMinSketch` when you need **per-element frequency estimates over a large or
unbounded stream** and can tolerate a small, bounded overestimate in exchange for fixed,
small memory: heavy-hitter / top-k detection, approximate frequency counts in analytics and
network telemetry, rate limiting, and deduplicated frequency counting across distributed
shards (count locally, then `UnionWith` the partial sketches). If you need exact counts or
to enumerate the keys, use a `Dictionary<TKey, int>` (or a Celerity dictionary) frequency
table instead; for approximate *membership* use `BloomFilter<T, THasher>`, and for the
distinct-element *count* use `HyperLogLog<T, THasher>`.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Estimate per-URL request frequencies in a high-volume stream from a few KB of counters.
var requests = new CountMinSketch<string, StringMurmur3Hasher>(epsilon: 0.001, delta: 0.001);

foreach (string url in requestStream)
    requests.Add(url);

Console.WriteLine(requests.EstimateCount("/api/login")); // >= true count, +<=0.1% of total

// Merge two shards' partial sketches to count frequencies across both.
var shardA = new CountMinSketch<string, StringMurmur3Hasher>(shardAUrls);
var shardB = new CountMinSketch<string, StringMurmur3Hasher>(shardBUrls);
shardA.UnionWith(shardB);
Console.WriteLine(shardA.EstimateCount("/api/login"));   // frequency across A ∪ B
```

## TopKSketch&lt;T, THasher&gt;

A space-bounded **top-k / heavy-hitters sketch** parameterized on a custom hash provider. It
answers "which elements occur most often, and roughly how often?" from a fixed number of
*monitors* whose count does **not** grow with the number of distinct elements — so it finds
a stream's heaviest hitters in `O(k)` memory, where a `Dictionary<TKey, int>` frequency table
must store every distinct key before it can rank them.

```csharp
public class TopKSketch<T, THasher>
    where THasher : struct, IHashProvider<T>
```

It implements the **Space-Saving** algorithm (Metwally, Agrawal & El Abbadi, 2005):

- **Fixed, small memory.** The sketch keeps exactly `Capacity` monitors (`element`, `count`,
  `error` triples) regardless of stream cardinality — `O(k)` space, not one entry per
  distinct key.
- **No heavy hitter is ever missed.** Any element whose true frequency exceeds
  `TotalCount / Capacity` is guaranteed to still be monitored, so a large enough `Capacity`
  cannot miss a genuine heavy hitter.
- **Bounded, one-sided error.** A monitor's `Count` **never underestimates** its element's
  true frequency and overestimates it by at most that monitor's `Error`, so the true
  frequency lies in `[Count − Error, Count]`. A monitor that never shared its slot has
  `Error == 0`, i.e. an exact count.
- **Add-and-query only.** Like a Bloom filter it has no `Remove` (decrementing a monitor
  would break the never-underestimate guarantee). Unlike `CountMinSketch` / `HyperLogLog` it
  has **no `UnionWith`**: two bounded top-k summaries cannot be merged into the exact top-k of
  the combined stream without error beyond each summary's own, so no lossy merge is offered.
  Use `Clear()` to reset.
- **Saturating counters.** A monitor count (and `TotalCount`) that would exceed
  `long.MaxValue` clamps there rather than wrapping negative.

### How it works

The sketch keeps `Capacity` monitors. An observed element that is already monitored has its
counter incremented. While free monitors remain, an unmonitored element takes a fresh one with
`Error == 0`. Once all monitors are in use, the element in the monitor with the **smallest**
count is evicted: the newcomer reuses that monitor, inheriting the evicted count as its `Error`
and setting its count to that minimum plus the new occurrences. Handing the newcomer the
current minimum is what bounds the error and yields the guarantees above.

The monitors live in an indexed binary **min-heap** keyed on count, so the next eviction victim
(the minimum) sits at the root, and an element→monitor index dogfoods
`CelerityDictionary<T, int, THasher>` — which is where `THasher` is used, and which also
supplies the out-of-band handling for a `default(T)` or `null` element (so a string hasher is
never invoked with `null`). Both a repeat observation and an eviction cost `O(log Capacity)`;
`GetTopK` sorts the monitors, an `O(k log k)` query-time cost off the add hot path.

### Constructors

```csharp
public TopKSketch(int capacity = 128)

public TopKSketch(
    IEnumerable<T> source,
    int capacity = 128)
```

The first overload creates an empty sketch that monitors up to `capacity` elements. The
`IEnumerable<T>` overload pre-populates it by adding each element once (so duplicates in the
source raise the tracked frequency). A larger `capacity` tracks more candidates and tightens
the guarantees, at proportional memory.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity` is less than 1.
- `ArgumentNullException` if `source` is `null` (enumerable overload). This check beats the
  capacity validation, so a `null` source with a bad `capacity` surfaces as
  `ArgumentNullException`.

### Methods and properties

- `void Add(T item)` — records one occurrence of an element.
- `void Add(T item, long count)` — records `count` occurrences. Throws
  `ArgumentOutOfRangeException` if `count` is not positive. A monitor count (and `TotalCount`)
  saturates at `long.MaxValue` rather than overflowing.
- `bool TryGetCount(T item, out long count, out long error)` — reads a monitored element's
  count and error. Returns `false` if the element is not currently monitored (in which case its
  true frequency is at most the smallest monitored count).
- `TopKEntry<T>[] GetTopK()` — every monitored element, ordered by estimated count descending.
- `TopKEntry<T>[] GetTopK(int count)` — the `count` most frequent monitored elements. Values
  greater than `Count` return all monitors; `0` returns an empty array; a negative value throws
  `ArgumentOutOfRangeException`.
- `void Clear()` — discards every monitor; preserves the capacity.
- `int Capacity { get; }` — the number of monitors kept (the `k` in top-k).
- `int Count { get; }` — the number of elements currently monitored (`0..Capacity`).
- `long TotalCount { get; }` — the total occurrences observed (the stream length `N`, the
  denominator of the `N / Capacity` heavy-hitter threshold).

`TopKEntry<T>` is a small readonly struct with `Element`, `Count`, and `Error` — the monitored
element, its estimated (upper-bound) count, and the maximum amount that count may overestimate
the truth.

### Default-element handling

The element→monitor index is a `CelerityDictionary<T, int, THasher>`, so a `default(T)` element
(a zero `int`, `Guid.Empty`, …) is stored in that dictionary's out-of-band slot and a `null`
reference is routed out-of-band rather than hashed — the hasher is never invoked with the
zero / null element, matching the rest of the family.

### Choosing it

Reach for `TopKSketch` when you need the **most frequent elements of a large or unbounded,
high-cardinality stream** and only the heaviest hitters matter: top URLs / IPs in log
analytics, trending items, network flow monitoring, hot database keys. It holds only `k`
monitors, so its memory is independent of the distinct-key count — the win over a
`Dictionary<TKey, int>` that must materialize every distinct key just to sort out the top few.
If you need the exact, fully-ranked counts (and can afford `O(distinct)` memory), use a
dictionary frequency table; if you need the estimated frequency of a **specific** element
rather than the top set, use `CountMinSketch<T, THasher>`; for the distinct-element *count* use
`HyperLogLog<T, THasher>`, and for approximate *membership* use `BloomFilter<T, THasher>`.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Track the 100 most-requested URLs in a high-cardinality stream from ~100 monitors,
// regardless of how many distinct URLs appear.
var hot = new TopKSketch<string, StringMurmur3Hasher>(capacity: 100);

foreach (string url in requestStream)
    hot.Add(url);

foreach (TopKEntry<string> entry in hot.GetTopK(10))
    Console.WriteLine($"{entry.Element}: ~{entry.Count} (±{entry.Error})");

// A specific element's tracked frequency, if it survived as a heavy hitter.
if (hot.TryGetCount("/api/login", out long count, out long error))
    Console.WriteLine($"/api/login seen {count - error}..{count} times");
```
