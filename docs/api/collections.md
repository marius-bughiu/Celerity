# Collections API Reference

All collection types live in the `Celerity.Collections` namespace.

## CelerityDictionary&lt;TKey, TValue, THasher&gt;

A high-performance generic dictionary parameterized on a custom hash provider. Uses open addressing with linear probing and power-of-two sizing for fast index computation. Implements both `IDictionary<TKey, TValue?>` and `IReadOnlyDictionary<TKey, TValue?>`.

```csharp
public class CelerityDictionary<TKey, TValue, THasher>
    : IDictionary<TKey, TValue?>, IReadOnlyDictionary<TKey, TValue?>
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

### Span-keyed lookups (string keys)

When `TKey` is `string` and the hasher also implements `ISpanHashProvider` (every built-in `String*Hasher` does), `TryGetValue(ReadOnlySpan<char>, out TValue?)` and `ContainsKey(ReadOnlySpan<char>)` probe directly from a slice of a caller-held buffer — no `new string(span)` per lookup. See [span-keyed lookups](#span-keyed-lookups).

### IReadOnlyDictionary&lt;TKey, TValue?&gt;

`CelerityDictionary` implements `IReadOnlyDictionary<TKey, TValue?>` via thin explicit interface forwarders on top of the existing struct `KeyCollection` / `ValueCollection` / `Enumerator` types. The zero-allocation `foreach` fast path is preserved; the interface path boxes the enumerator exactly once per `GetEnumerator()` call, matching BCL `Dictionary<,>` behaviour. The out-of-band default-key entry is surfaced through every interface member.

### IDictionary&lt;TKey, TValue?&gt;

`CelerityDictionary` also implements the **mutable** BCL interface, so it can be passed to any existing API whose parameter is `IDictionary<TKey, TValue>` — the same drop-in goal as `IReadOnlyDictionary<,>`, one level up. `ISet<T>` does not derive from `IReadOnlySet<T>` and `IDictionary<,>` does not derive from `IReadOnlyDictionary<,>`, so the two have to be declared separately, exactly as BCL `Dictionary<,>` does.

Every member is an explicit-interface forwarder onto the existing public surface; no existing public signature changed, and the concrete indexer still returns the non-nullable `TValue`. The semantics worth knowing:

| Interface member | Behaviour |
| --- | --- |
| `IsReadOnly` | `false`. |
| `Add(TKey, TValue?)` | The **throwing** duplicate contract of `Dictionary<,>` — `ArgumentException` when the key is already present. `TryAdd` remains the non-throwing path on the concrete type. |
| `this[key]` (set) | Insert-or-overwrite, matching the concrete indexer. Overwriting an existing key is not a structural change and does not invalidate a live enumerator. |
| `Keys` / `Values` | The existing struct views, widened to `ICollection<TKey>` / `ICollection<TValue?>`. They are **read-only**: `Add`, `Clear` and `Remove` throw `NotSupportedException`, exactly as `Dictionary<,>.KeyCollection` does. |
| `Contains(KeyValuePair<,>)` | Matches on the **pair** — a present key carrying a different value reports `false`. |
| `Remove(KeyValuePair<,>)` | Same pair matching; a stale value must not delete the current entry. |
| `CopyTo` | Also exposed publicly as `CopyTo(KeyValuePair<TKey, TValue?>[], int)`. Throws `ArgumentNullException` for a null array, `ArgumentOutOfRangeException` for a negative index or one past the end, and `ArgumentException` when the destination has insufficient space. |

Because `IDictionary<,>` is invariant in its value type, the declared interface is `IDictionary<TKey, TValue?>` — matching the existing `IReadOnlyDictionary<TKey, TValue?>` declaration. For an unconstrained `TValue` the annotation is erased at the IL level, so a `CelerityDictionary<int, string, …>` is an `IDictionary<int, string?>`.

```csharp
// Any BCL-shaped API taking the mutable interface now accepts a Celerity dictionary.
static void Seed(IDictionary<int, string?> target)
{
    target.Add(1, "one");
    target[2] = "two";
}

var dict = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
Seed(dict);

IDictionary<int, string?> view = dict;
view.Contains(new KeyValuePair<int, string?>(1, "one"));  // true
view.Contains(new KeyValuePair<int, string?>(1, "uno"));  // false — the pair must match
view.Keys.Add(3);                                         // throws NotSupportedException
```

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

A drop-in peer of `CelerityDictionary` that resolves collisions with **Robin Hood** open addressing instead of plain linear probing. The public surface — constructors, indexer, `ContainsKey` / `ContainsValue` / `TryGetValue` / `Add` / `TryAdd` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator` / `KeyCollection` / `ValueCollection`, and both `IDictionary<TKey, TValue?>` and `IReadOnlyDictionary<TKey, TValue?>` — is identical to `CelerityDictionary`. Only the probing strategy differs.

```csharp
public class RobinHoodDictionary<TKey, TValue, THasher>
    : IDictionary<TKey, TValue?>, IReadOnlyDictionary<TKey, TValue?>
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

An allocation-conscious peer of `CelerityDictionary` whose backing arrays are **rented from [`ArrayPool<T>.Shared`](https://learn.microsoft.com/dotnet/api/system.buffers.arraypool-1)** instead of being allocated on the managed heap. The public surface is identical to `CelerityDictionary` — same indexer, `ContainsKey` / `ContainsValue` / `TryGetValue` / `Add` / `TryAdd` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator` / `KeyCollection` / `ValueCollection`, and both `IDictionary<TKey, TValue?>` and `IReadOnlyDictionary<TKey, TValue?>` — with one addition: it implements `IDisposable`.

```csharp
public class PooledCelerityDictionary<TKey, TValue, THasher>
    : IDictionary<TKey, TValue?>, IReadOnlyDictionary<TKey, TValue?>, IDisposable
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

A drop-in peer of `CelerityDictionary` that resolves collisions with **SIMD-accelerated group probing** in the spirit of Google's Swiss Tables and Facebook's `F14`, instead of scalar linear probing. The public surface — constructors, indexer, `ContainsKey` / `ContainsValue` / `TryGetValue` / `Add` / `TryAdd` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator` / `KeyCollection` / `ValueCollection`, and both `IDictionary<TKey, TValue?>` and `IReadOnlyDictionary<TKey, TValue?>` — is identical to `CelerityDictionary`. Only the probing strategy differs.

```csharp
public class SwissDictionary<TKey, TValue, THasher>
    : IDictionary<TKey, TValue?>, IReadOnlyDictionary<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
```

### What SIMD group probing does

The table keeps a parallel array of one-byte **control** tags — one per slot — separate from the key/value arrays. Each control byte is either `Empty`, `Deleted` (a tombstone), or, for an occupied slot, the low 7 bits of the key's hash (its *h2* fragment). Slots are grouped into aligned blocks of 16, so a single `Vector128<sbyte>` compare tests all 16 control bytes in a group at once: a lookup loads the 16 tags, compares them against the broadcast h2, and turns the result into a 16-bit candidate mask via `Vector128.ExtractMostSignificantBits`. Only the (usually one) candidate slots then pay a full key comparison; a group with any `Empty` slot ends the probe. Two consequences matter to callers:

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

A drop-in peer of `CelerityDictionary` that pushes the **struct-of-arrays layout** one step further: alongside the parallel `keys` / `values` arrays it keeps a dense side array of 32-bit hash **fingerprints**, one per slot. A probe scan touches only that compact metadata buffer — comparing the cached fingerprint before it ever reads a key — so cache-cold lookups and lookups with expensive key equality (long strings, large structs) short-circuit on a single integer compare. The public surface — constructors, indexer, `ContainsKey` / `ContainsValue` / `TryGetValue` / `Add` / `TryAdd` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator` / `KeyCollection` / `ValueCollection`, and both `IDictionary<TKey, TValue?>` and `IReadOnlyDictionary<TKey, TValue?>` — is identical to `CelerityDictionary`. Only the probe metadata differs.

```csharp
public class HashCachingDictionary<TKey, TValue, THasher>
    : IDictionary<TKey, TValue?>, IReadOnlyDictionary<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
```

### What the cached-fingerprint side array does

The fingerprint of an occupied slot is the key's hash with its top bit forced set (`hash | 0x80000000`), which makes it always non-zero; an empty slot is the array default of `0`. The fingerprint array therefore doubles as the occupancy bitmap — probing, enumeration, and `ContainsValue` test it rather than comparing keys against `default(TKey)`. Two consequences matter to callers:

- **Metadata-only probe scans.** A linear probe walks the dense `int[]` fingerprint array and only dereferences a key (running the full, possibly expensive `EqualityComparer<TKey>` check) when the cached fingerprint matches. On a cache-cold table the probe stays inside one compact buffer, and on keys with costly equality the integer compare filters out almost every non-match — the win over scalar linear probing grows with key-equality cost and table size.
- **Rehash without re-hashing.** Because the forced occupied bit sits above every power-of-two table mask, the cached fingerprint also yields the slot index directly (`fingerprint & mask`). A resize re-homes every entry straight from its stored fingerprint without invoking the hasher once, and backward-shift deletion reads each candidate's natural slot from its fingerprint too.

### When to choose it over `CelerityDictionary`

Reach for `HashCachingDictionary` for **lookup-dominated** workloads, **expensive-equality keys** (long strings, large value-type keys), or large cache-cold tables where the metadata-only scan pays off, and where four bytes of metadata per slot is an acceptable cost. For small tables of cheap (e.g. `int`) keys, `CelerityDictionary` has the smaller footprint and is roughly a wash. It is complementary to `SwissDictionary`: both keep a metadata side array, but `HashCachingDictionary` is a scalar, wider-fingerprint design with backward-shift (tombstone-free) deletion, while `SwissDictionary` uses SIMD group probing over one-byte tags. Both are single-threaded and make no iteration-order guarantee.

`string` keys are the case worth calling out. The BCL `Dictionary<string, TValue>` already stores a hash code per entry, so `CelerityDictionary<string, …>` — which stores keys and nothing else — gives up a full ordinal string compare per probed slot and measures *behind* the BCL there. `HashCachingDictionary` restores that structure and the gap largely closes; on the negative-lookup path it turns into a win. See [reference-type keys: cache the hash](../performance.md#reference-type-keys-cache-the-hash) for the measured table.

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

A high-performance dictionary keyed by `int`, parameterized on a custom hash provider. This is a separate implementation from `CelerityDictionary` that avoids the boxing/equality-comparer overhead of generic key types by working directly with `int` keys and using `==` for comparisons. Implements both `IDictionary<int, TValue?>` and `IReadOnlyDictionary<int, TValue?>`.

```csharp
public class IntDictionary<TValue, THasher>
    : IDictionary<int, TValue?>, IReadOnlyDictionary<int, TValue?>
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

`IntDictionary<TValue, THasher>` also implements `IDictionary<int, TValue?>` and `IReadOnlyDictionary<int, TValue?>` with the same explicit-interface forwarding pattern as [`CelerityDictionary`](#idictionarytkey-tvalue).

### Zero-key handling

The key `0` collides with the internal `EmptyKey` sentinel. Like `CelerityDictionary`'s default-key handling, `IntDictionary` stores key `0` out-of-band via a `_hasZeroKey` flag and a dedicated value slot. This is invisible to callers.

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

A high-performance dictionary keyed by `long`, parameterized on a custom hash provider. Mirrors `IntDictionary` but for 64-bit keys. Defaults to `Int64WangNaiveHasher` when used through the convenience subclass. Implements both `IDictionary<long, TValue?>` and `IReadOnlyDictionary<long, TValue?>`.

```csharp
public class LongDictionary<TValue, THasher>
    : IDictionary<long, TValue?>, IReadOnlyDictionary<long, TValue?>
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

The key `0L` collides with the `EmptyKey` sentinel and is stored out-of-band the same way `IntDictionary` handles the key `0`. Two keys that share the lower 32 bits but differ in the upper 32 bits are kept distinct (the probe path does not truncate).

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

A high-performance generic set parameterized on a custom hash provider. Set counterpart to `CelerityDictionary`. Implements `ISet<T>` and `IReadOnlySet<T>` (and therefore `ICollection<T>` / `IEnumerable<T>`), so it is a drop-in for `HashSet<T>` wherever set algebra is used.

```csharp
public class CeleritySet<T, THasher> : ISet<T>, IReadOnlySet<T>
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

### Set operations (`ISet<T>` and `IReadOnlySet<T>`)

The full BCL `HashSet<T>` set-algebra surface is available and follows `HashSet<T>` semantics exactly (duplicate-tolerant `other`, self-aliasing `other == this`, and the out-of-band `default(T)`/zero element all handled):

- **Mutating:** `void UnionWith(IEnumerable<T> other)`, `void IntersectWith(IEnumerable<T> other)`, `void ExceptWith(IEnumerable<T> other)`, `void SymmetricExceptWith(IEnumerable<T> other)`.
- **Query:** `bool IsSubsetOf(...)`, `bool IsProperSubsetOf(...)`, `bool IsSupersetOf(...)`, `bool IsProperSupersetOf(...)`, `bool Overlaps(...)`, `bool SetEquals(...)`.

Each throws `ArgumentNullException` when `other` is `null`. The subset / equality shapes materialize `other` once into a distinct `HashSet<T>` keyed by `EqualityComparer<T>.Default` (the same equality the set itself uses); the superset / overlap shapes stream `other` directly against the set's O(1) membership test.

> **`Add` note.** `ISet<T>.Add(T)` returns `bool` (the non-throwing add, equivalent to `TryAdd`). The concrete `public void Add(T)` keeps its throw-on-duplicate behaviour — cast to `ISet<T>`, or use `TryAdd`, when you want the boolean result. `ICollection<T>.Add(T)` ignores duplicates (never throws).

The six query members are also reachable through `IReadOnlySet<T>`, which the set declares alongside `ISet<T>`. The two have to be declared separately — `ISet<T>` does not derive from `IReadOnlySet<T>`, exactly as `IDictionary<,>` does not derive from `IReadOnlyDictionary<,>` — and declaring both is what lets a Celerity set be passed to an API whose parameter is the read-only interface:

```csharp
static bool IsAuthorized(IReadOnlySet<string> allowed, string scope) => allowed.Contains(scope);

var scopes = new CeleritySet<string, DefaultHasher<string>>();
scopes.TryAdd("read");
scopes.TryAdd("write");

bool ok = IsAuthorized(scopes, "write"); // true
```

> **Overload-resolution caveat.** Carrying both interfaces can make a call ambiguous when a method is overloaded on `ISet<T>` *and* `IReadOnlySet<T>` and neither is more specific — the same situation BCL `HashSet<T>` has been in since .NET 5, which is why libraries that overload on both (xUnit's `Assert.Contains`, for instance) also ship a concrete `HashSet<T>` overload to break the tie. Celerity's sets get no such special-casing, so such a call site needs a cast — `Assert.Contains(x, (IReadOnlySet<int>)set)` — or the concrete member, `Assert.True(set.Contains(x))`.

### Span-keyed lookups (string elements)

When `T` is `string` and the hasher also implements `ISpanHashProvider` (every built-in `String*Hasher` does), `Contains(ReadOnlySpan<char>)` probes directly from a slice of a caller-held buffer — no `new string(span)` per lookup. See [span-keyed lookups](#span-keyed-lookups).

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

A drop-in peer of `CeleritySet` that resolves collisions with **SIMD-accelerated group probing** in the spirit of Google's Swiss Tables and Facebook's `F14`, instead of scalar linear probing. It is the set counterpart of `SwissDictionary` — the same control-byte machinery with no value array. The public surface — constructors, `Add` / `TryAdd` / `Contains` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator`, `CopyTo`, and the full `ISet<T>` / `IReadOnlySet<T>` set-algebra surface (see [`CeleritySet`](#celeritysett-thasher)) — is identical to `CeleritySet`. Only the probing strategy differs.

```csharp
public class SwissSet<T, THasher> : ISet<T>, IReadOnlySet<T>
    where THasher : struct, IHashProvider<T>
```

### What SIMD group probing does

The set keeps a parallel array of one-byte **control** tags — one per slot — separate from the element array. Each control byte is either `Empty`, `Deleted` (a tombstone), or, for an occupied slot, the low 7 bits of the element's hash (its *h2* fragment). Slots are grouped into aligned blocks of 16, so a single `Vector128<sbyte>` compare tests all 16 control bytes in a group at once: a membership test loads the 16 tags, compares them against the broadcast h2, and turns the result into a 16-bit candidate mask via `Vector128.ExtractMostSignificantBits`. Only the (usually one) candidate slots then pay a full element comparison; a group with any `Empty` slot ends the probe. Two consequences matter to callers:

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

A drop-in peer of `CeleritySet` that resolves collisions with **Robin Hood open addressing** instead of plain linear probing. It is the set counterpart of `RobinHoodDictionary` — the same probe-sequence-length (PSL) machinery with no value array. The public surface — constructors, `Add` / `TryAdd` / `Contains` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator`, `CopyTo`, and the full `ISet<T>` / `IReadOnlySet<T>` set-algebra surface (see [`CeleritySet`](#celeritysett-thasher)) — is identical to `CeleritySet`. Only the probing strategy differs.

```csharp
public class RobinHoodSet<T, THasher> : ISet<T>, IReadOnlySet<T>
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

A drop-in peer of `CeleritySet` that takes the struct-of-arrays layout one step further: alongside the `items` array it keeps a dense side array of 32-bit hash **fingerprints**. It is the set counterpart of `HashCachingDictionary` — the same cached-fingerprint machinery with no value array. The public surface — constructors, `Add` / `TryAdd` / `Contains` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator`, `CopyTo`, and the full `ISet<T>` / `IReadOnlySet<T>` set-algebra surface (see [`CeleritySet`](#celeritysett-thasher)) — is identical to `CeleritySet`. Only the probe representation differs.

```csharp
public class HashCachingSet<T, THasher> : ISet<T>, IReadOnlySet<T>
    where THasher : struct, IHashProvider<T>
```

### What the cached fingerprint does

Every occupied slot stores its element's hash with the top bit forced set (`hash | 0x80000000`), which makes the fingerprint always non-zero; an empty slot is the array default of `0`. A probe scan touches **only** the compact fingerprint buffer — comparing the cached fingerprint before it ever reads an element — so a candidate element is dereferenced (and the full equality check run) only on a fingerprint match. Two consequences matter to callers:

- **Cache-friendly probing.** The dense `int[]` metadata buffer packs many more slots per cache line than the element array, so cache-cold lookups walk metadata instead of chasing element references.
- **Short-circuited equality.** Elements with expensive equality (long strings, large structs) are compared in full only when their fingerprint matches, so negative lookups and colliding-slot probes reject on a single integer compare. Because the forced occupied bit sits above every power-of-two table mask, the cached fingerprint also yields the slot index directly (`fingerprint & mask`), so a resize re-homes every entry without recomputing a single hash. Deletion uses backward-shift (driven by the cached natural slot), so the table stays contiguous (no tombstones).

### When to choose it over `CeleritySet`

Reach for `HashCachingSet` on **lookup-dominated** workloads — large tables, many negative "have I seen this?" checks, or elements whose equality is expensive — where the fingerprint filter earns back its four bytes of metadata per slot. On tiny tables of cheap (e.g. `int`) elements it is roughly a wash versus `CeleritySet`, so it is an opt-in type, not a default. It is complementary to the SIMD-probing [`SwissSet`](#swisssett-thasher): both cut probe cost, one via a control-byte group compare, the other via a cached fingerprint. Both are single-threaded and make no iteration-order guarantee.

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

An allocation-conscious peer of `CeleritySet` whose backing array is **rented from [`ArrayPool<T>.Shared`](https://learn.microsoft.com/dotnet/api/system.buffers.arraypool-1)** instead of being allocated on the managed heap. It is the set counterpart of `PooledCelerityDictionary` — the same rent / return lifecycle applied to a single element array rather than parallel key/value arrays. The public surface is identical to `CeleritySet` — same constructors, `Add` / `TryAdd` / `Contains` / `Remove` / `Clear` / `EnsureCapacity` / `TrimExcess`, the struct `Enumerator`, `CopyTo`, and the full `ISet<T>` / `IReadOnlySet<T>` set-algebra surface (see [`CeleritySet`](#celeritysett-thasher)) — with one addition: it implements `IDisposable`.

```csharp
public class PooledCeleritySet<T, THasher> : ISet<T>, IReadOnlySet<T>, IDisposable
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

A high-performance set of `int` values, parameterized on a custom hash provider. Implements `ISet<int>` and `IReadOnlySet<int>` (and therefore `ICollection<int>` / `IEnumerable<int>`) — the full `HashSet<int>` set-algebra surface (`UnionWith` / `IntersectWith` / `ExceptWith` / `SymmetricExceptWith` / `IsSubsetOf` / … / `SetEquals`, plus `CopyTo`) is available with BCL semantics; see [`CeleritySet`](#celeritysett-thasher).

```csharp
public class IntSet<THasher> : ISet<int>, IReadOnlySet<int>
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

The element `0` collides with the `EmptySlot` sentinel and is stored out-of-band, same pattern as `IntDictionary`'s zero-key handling.

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

A high-performance set of `long` values, parameterized on a custom hash provider. Implements `ISet<long>` and `IReadOnlySet<long>` (and therefore `ICollection<long>` / `IEnumerable<long>`) — the full `HashSet<long>` set-algebra surface (`UnionWith` / `IntersectWith` / `ExceptWith` / `SymmetricExceptWith` / `IsSubsetOf` / … / `SetEquals`, plus `CopyTo`) is available with BCL semantics; see [`CeleritySet`](#celeritysett-thasher).

```csharp
public class LongSet<THasher> : ISet<long>, IReadOnlySet<long>
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

The element `0L` collides with the `EmptySlot` sentinel and is stored out-of-band, same pattern as `LongDictionary`'s zero-key handling.

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

Both the convenience type and this one also carry `TryGetValue(ReadOnlySpan<char>, out TValue?)` and `ContainsKey(ReadOnlySpan<char>)` — see [span-keyed lookups](#span-keyed-lookups).

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

Both the convenience type and this one also carry `Contains(ReadOnlySpan<char>)` — see [span-keyed lookups](#span-keyed-lookups).

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
    : IDictionary<TKey, TValue?>, IReadOnlyDictionary<TKey, TValue?>
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

It implements both [`IDictionary<TKey, TValue?>`](#idictionarytkey-tvalue) and
`IReadOnlyDictionary<TKey, TValue?>`, ships allocation-free struct `Keys` / `Values`
views (read-only `ICollection<T>`s through the interface) and a struct enumerator, and
accepts an `IEnumerable<KeyValuePair<TKey, TValue>>` source at construction — the same
surface as the other Celerity dictionaries.

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

## SmallSet&lt;T&gt;

```csharp
public class SmallSet<T> : ISet<T>, IReadOnlySet<T>
```

The set counterpart to `SmallDictionary`, tuned for the **very-small** case
(`n <= ~16`), where a linear scan over a flat backing array beats a probe-based
hash table. This is the shape you hit constantly for per-scope "seen" sets, small
membership guards, and deduplicating a handful of items — most instances stay tiny,
and for a tiny `n` the cost of computing a hash, masking it, and chasing a probe
chain is pure overhead next to a cache-friendly scan of a handful of elements.

Unlike the hash-table sets, `SmallSet` stores elements in an insertion-dense flat
array and answers every query with a linear scan using `EqualityComparer<T>.Default`.
There is **no hasher** (and so no `THasher` type parameter): you do not pick a hash
function, because it never hashes. The trade-offs that follow directly from that:

- `Contains`, `Add`/`TryAdd` (duplicate detection), and `Remove` are `O(n)` rather
  than `O(1)`. The type is built for small `n` and **degrades for large sets** — keep
  it to the small-`n` workloads it is designed for. It does *not* auto-promote to a
  hash table; it simply grows its array and keeps scanning.
- Because nothing is hashed, there is **no empty-slot sentinel** and therefore no
  special-casing of `default(T)`. A `0`, `null`, or `Guid.Empty` element is stored
  inline like any other — a small simplification over the hash-table sets, which keep
  the default element out-of-band.
- `Remove` moves the last element into the vacated slot (an `O(1)` swap once the
  element is found), so the relative order of the surviving elements is not
  preserved. Enumeration order is unspecified in general.

It implements `ISet<T>` and `IReadOnlySet<T>` (and therefore `ICollection<T>` / `IEnumerable<T>`), ships an
allocation-free struct enumerator, and accepts an `IEnumerable<T>` source at
construction — the same surface as the other Celerity sets.

### Constructors

```csharp
SmallSet(int capacity = 4)
SmallSet(IEnumerable<T> source, int capacity = 4)
```

- `capacity` is the number of elements the backing array is sized for up front.
  Unlike the hash-table sets it is used **verbatim** (it is not rounded to a power of
  two), since there is no probe mask. `0` defers allocation until the first insert.
- Throws `ArgumentOutOfRangeException` for a negative `capacity`. There is **no
  `loadFactor`** parameter.
- The `source` constructor silently deduplicates (matching BCL `HashSet<T>(IEnumerable<T>)`
  semantics — sets have no duplicate-element contract), and throws
  `ArgumentNullException` if `source` is `null` (the null check beats the capacity
  validation).

### Methods

- `void Add(T item)` — throws `ArgumentException` on duplicate.
- `bool TryAdd(T item)` — `true` on success, `false` if already present.
- `bool Contains(T item)` — `O(n)` scan.
- `bool Remove(T item)` — `O(1)` swap-removal after the scan.
- `void Clear()` — capacity preserved.
- `int EnsureCapacity(int capacity)` — grow the backing array to hold at least
  `capacity` elements, returning the resulting array length. Like the constructor,
  the length is **verbatim** (not rounded to a power of two). Throws
  `ArgumentOutOfRangeException` on a negative capacity.
- `void TrimExcess()` / `void TrimExcess(int capacity)` — shrink the backing array to
  exactly the current `Count` (or `capacity`), reclaiming memory. `TrimExcess(capacity)`
  throws if `capacity < Count`.
- `int Count { get; }`
- `Enumerator GetEnumerator()` — allocation-free struct enumerator.
- `void CopyTo(T[] array, int arrayIndex)` — copies every element into `array`,
  matching `HashSet<T>.CopyTo` argument validation.

### Set operations (`ISet<T>` and `IReadOnlySet<T>`)

The full BCL `HashSet<T>` set-algebra surface is available and follows `HashSet<T>`
semantics exactly (duplicate-tolerant `other`, self-aliasing `other == this`):

- **Mutating:** `void UnionWith(IEnumerable<T> other)`, `void IntersectWith(IEnumerable<T> other)`, `void ExceptWith(IEnumerable<T> other)`, `void SymmetricExceptWith(IEnumerable<T> other)`.
- **Query:** `bool IsSubsetOf(...)`, `bool IsProperSubsetOf(...)`, `bool IsSupersetOf(...)`, `bool IsProperSupersetOf(...)`, `bool Overlaps(...)`, `bool SetEquals(...)`.

Each throws `ArgumentNullException` when `other` is `null`. As with the hash-table
sets, `ISet<T>.Add(T)` returns `bool` (equivalent to `TryAdd`), the concrete
`public void Add(T)` keeps its throw-on-duplicate behaviour, and `ICollection<T>.Add(T)`
ignores duplicates.

### Default-element handling

A `0`, `null`, or `Guid.Empty` element is an ordinary inline entry — add it, test it,
and remove it exactly like any other element. There is no out-of-band slot.

### Usage example

```csharp
using Celerity.Collections;

// A tiny per-scope "already seen" set — almost always a handful of elements.
var seen = new SmallSet<string>();
seen.Add("x");
seen.Add("y");
Console.WriteLine(seen.TryAdd("x")); // False — already present, unchanged
Console.WriteLine(seen.Contains("y")); // True
Console.WriteLine(seen.Count); // 2

seen.Remove("x"); // O(1) swap-removal after the scan
foreach (var item in seen) { /* "y" */ }
```

---

## EnumSet&lt;TEnum&gt;

```csharp
public class EnumSet<TEnum> : ISet<TEnum>, IReadOnlySet<TEnum>
    where TEnum : struct, Enum
```

A set specialized for **enum element types**, backed by a dense bit vector indexed on
the enum's underlying integer value — the .NET analogue of Java's
`java.util.EnumSet`. Where `HashSet<TEnum>` hashes and boxes each element through
`EqualityComparer<TEnum>` and stores an open-addressed table, `EnumSet` stores **one
bit per possible element**:

- `Add` / `Contains` / `Remove` are a shift, a mask, and a single-`ulong` bit
  operation — no hash, no probe chain, no per-element allocation.
- Set algebra against another `EnumSet<TEnum>` is `O(words)` word-wise `OR` / `AND` /
  `AND-NOT` / `XOR` over a handful of `ulong`s (usually just **one**), versus
  `HashSet<T>`'s element-by-element rehash-and-probe.
- Enumeration is **deterministic and ascending by underlying value** (the bit vector
  is walked low bit first) — a bonus over the hash-table sets' unspecified order.

This is the classic bit-flags-set win, made type-safe and generic.

### Supported enums

The backing store is sized once from the enum's **maximum defined underlying value**,
so `EnumSet` supports enums whose members are **small, non-negative integers** — the
default `0, 1, 2, …` declaration, which covers the overwhelming majority of enums.

- An enum that declares a **negative** member throws `NotSupportedException` from the
  constructor (a bit vector cannot be indexed by a negative value).
- An enum whose **maximum value exceeds `65535`** — a sparse or `[Flags]`
  power-of-two enum, for which a dense bit vector would waste enormous memory — also
  throws `NotSupportedException`. Use `CeleritySet<TEnum, THasher>` for those.
- A runtime value **outside the supported range** (an out-of-range cast such as
  `(MyEnum)9999`) is rejected by `Add` / `TryAdd` with `ArgumentOutOfRangeException`,
  and reported as absent by `Contains` / `Remove`.

### Constructors

```csharp
EnumSet()
EnumSet(IEnumerable<TEnum> source)
```

- The parameterless constructor creates an empty set. There is **no capacity or
  `loadFactor` parameter** — the storage size is fixed by the enum, not by the element
  count.
- The `source` constructor silently deduplicates (matching BCL
  `HashSet<T>(IEnumerable<T>)` semantics) and throws `ArgumentNullException` if
  `source` is `null`. Copying from another `EnumSet<TEnum>` copies the bit vector
  wholesale.
- Both throw `NotSupportedException` if `TEnum` is unsupported (see above).

### Static factory

```csharp
static EnumSet<TEnum> All()
```

Returns a set containing **every declared constant** of `TEnum` — the full universe of
the enum (exactly the declared members, not every bit position).

### Methods

- `void Add(TEnum item)` — throws `ArgumentException` on duplicate,
  `ArgumentOutOfRangeException` for an out-of-range value.
- `bool TryAdd(TEnum item)` — `true` on success, `false` if already present; throws
  `ArgumentOutOfRangeException` for an out-of-range value.
- `bool Contains(TEnum item)` — single bit test.
- `bool Remove(TEnum item)` — single bit clear.
- `void Clear()`
- `int Count { get; }`
- `Enumerator GetEnumerator()` — allocation-free struct enumerator, ascending order.
- `void CopyTo(TEnum[] array, int arrayIndex)` — copies in ascending order, matching
  `HashSet<T>.CopyTo` argument validation.

### Set operations (`ISet<TEnum>` and `IReadOnlySet<TEnum>`)

The full BCL `HashSet<T>` set-algebra surface is available with `HashSet<T>` semantics.
When the operand is another `EnumSet<TEnum>`, each operation runs as word-wise bitwise
work; for a general `IEnumerable<TEnum>` it falls back to the shared element-by-element
path.

- **Mutating:** `UnionWith`, `IntersectWith`, `ExceptWith`, `SymmetricExceptWith`.
- **Query:** `IsSubsetOf`, `IsProperSubsetOf`, `IsSupersetOf`, `IsProperSupersetOf`,
  `Overlaps`, `SetEquals`.

Each throws `ArgumentNullException` when `other` is `null`. As with the hash-table
sets, `ISet<TEnum>.Add` returns `bool` (equivalent to `TryAdd`), the concrete
`public void Add` keeps its throw-on-duplicate behaviour, and `ICollection<TEnum>.Add`
ignores duplicates.

### Usage example

```csharp
using Celerity.Collections;

enum Permission { Read, Write, Execute, Delete, Admin }

var granted = new EnumSet<Permission> { Permission.Read, Permission.Write };
Console.WriteLine(granted.Contains(Permission.Write)); // True — a single bit test

// Set algebra between two EnumSets is a word-wise bitwise op.
var required = new EnumSet<Permission> { Permission.Read, Permission.Execute };
Console.WriteLine(granted.IsSupersetOf(required)); // False — Execute not granted

granted.UnionWith(required);                        // grant the missing ones
Console.WriteLine(granted.Count);                   // 3

var everything = EnumSet<Permission>.All();         // all declared constants
Console.WriteLine(everything.Count);                // 5
foreach (var p in granted) { /* ascending: Read, Write, Execute */ }
```

---

## SparseSet

```csharp
public class SparseSet : ISet<int>, IReadOnlySet<int>
```

A set of **non-negative integers over a bounded universe** `[0, Universe)`, backed by
the classic **Briggs–Torczon sparse-set representation**: a *dense* array holding the
present values contiguously, paired with a *sparse* array — indexed by value — that
points each present value back at its slot in the dense array. Membership is the
round-trip `sparse[v] < Count && dense[sparse[v]] == v`, which is correct even for a *stale*
sparse entry — one left over from before a `Clear`, or the zero a never-written slot still
holds. That single fact is what buys the type its two wins over `HashSet<int>`:

- **`Clear()` is `O(1)`** — it resets the count *without scanning or clearing the
  backing arrays*. `HashSet<int>.Clear()` is `O(capacity)` (it zeroes the whole entry table).
  This is the headline: per-frame / per-query "visited" sets in graph traversal (BFS/DFS), ECS
  entity membership, register-allocation liveness, and sweep-line algorithms clear on every
  iteration.
- **Dense, cache-friendly iteration** — present elements live contiguously in `[0, Count)`
  of the dense array, so enumeration is a linear scan over exactly `Count` ints with no
  empty-slot skipping.

`Add` / `Contains` / `Remove` are each `O(1)` with **no hashing**, no probe chain, and no
per-element allocation — a direct array index and the round-trip check. There is **no
hasher** (and so no `THasher` type parameter).

The trade-offs, stated honestly:

- The sparse index array is **`O(Universe)` memory**, sized once at construction. The type
  is worth it when the universe is bounded and the set is cleared / rebuilt / iterated
  often — not as a general `HashSet<int>` replacement. For an unbounded or huge-and-sparse
  key space, use [`IntSet`](#intset) / `HashSet<int>`.
- It stores **only non-negative values below `Universe`**. A value outside `[0, Universe)`
  is rejected by `Add` / `TryAdd` with `ArgumentOutOfRangeException`, and reported as absent
  by `Contains` / `Remove` (the bounded-universe analogue of `EnumSet`).
- `Remove` moves the last dense element into the vacated slot (an `O(1)` swap), so the
  relative order of the surviving elements is not preserved. Enumeration order is
  unspecified in general.

It implements `ISet<int>` and `IReadOnlySet<int>` (and therefore `ICollection<int>` / `IEnumerable<int>`), ships an
allocation-free struct enumerator, and accepts an `IEnumerable<int>` source at construction.

### Constructors

```csharp
SparseSet(int universe)
SparseSet(int universe, IEnumerable<int> source)
```

- `universe` is the **exclusive upper bound** of storable values; the set can hold any
  non-negative integer strictly less than it. It sizes the sparse index array once, so
  it is the dominant memory cost — choose it to match the actual value range. `0` creates
  a set that can store nothing.
- Throws `ArgumentOutOfRangeException` for a negative `universe`. There is **no
  `loadFactor`** parameter.
- The `source` constructor pre-sizes the dense array from an `ICollection<int>` source,
  silently deduplicates (matching BCL `HashSet<int>(IEnumerable<int>)` semantics), throws
  `ArgumentNullException` if `source` is `null` (the null check beats the universe
  validation), and throws `ArgumentOutOfRangeException` if any source value is outside
  `[0, universe)`.

### Methods

- `void Add(int item)` — throws `ArgumentException` on duplicate, `ArgumentOutOfRangeException` out of range.
- `bool TryAdd(int item)` — `true` on success, `false` if already present; throws out of range.
- `bool Contains(int item)` — `O(1)`; `false` for an out-of-range value.
- `bool Remove(int item)` — `O(1)` swap-removal; `false` for an absent or out-of-range value.
- `void Clear()` — **`O(1)`**, touches no memory; the set stays reusable.
- `int EnsureCapacity(int capacity)` — grow the dense array to hold at least `capacity`
  elements (clamped to `Universe`), returning the resulting dense-array length. Throws
  `ArgumentOutOfRangeException` on a negative capacity.
- `void TrimExcess()` / `void TrimExcess(int capacity)` — shrink the dense array to exactly
  the current `Count` (or `capacity`). `TrimExcess(capacity)` throws if `capacity < Count`
  or `capacity > Universe`. The sparse index array is unaffected.
- `int Count { get; }`, `int Universe { get; }`
- `Enumerator GetEnumerator()` — allocation-free struct enumerator over the dense array.
- `void CopyTo(int[] array, int arrayIndex)` — matches `HashSet<int>.CopyTo` argument validation.

### Set operations (`ISet<int>` and `IReadOnlySet<int>`)

The full BCL `HashSet<int>` set-algebra surface is available and follows `HashSet<int>`
semantics exactly within the universe (duplicate-tolerant `other`, self-aliasing
`other == this`):

- **Mutating:** `UnionWith`, `IntersectWith`, `ExceptWith`, `SymmetricExceptWith`.
- **Query:** `IsSubsetOf`, `IsProperSubsetOf`, `IsSupersetOf`, `IsProperSupersetOf`, `Overlaps`, `SetEquals`.

Each throws `ArgumentNullException` when `other` is `null`. The one bounded-universe caveat:
a **mutating** operation that would *add* a value outside `[0, Universe)` (e.g. `UnionWith`
with such an element) throws `ArgumentOutOfRangeException` rather than silently growing an
unbounded set. Query operations tolerate out-of-range values in `other` (they simply read
as absent). As with the other sets, `ISet<int>.Add(int)` returns `bool` (equivalent to
`TryAdd`), the concrete `public void Add(int)` keeps its throw-on-duplicate behaviour, and
`ICollection<int>.Add(int)` ignores duplicates.

### Usage example

```csharp
using Celerity.Collections;

// A BFS "visited" set over a graph whose nodes are ids in [0, nodeCount).
var visited = new SparseSet(nodeCount);

for (int start = 0; start < nodeCount; start++)
{
    visited.Clear(); // O(1) — no memory touched, ready for the next traversal
    var queue = new Queue<int>();
    queue.Enqueue(start);
    visited.Add(start);

    while (queue.Count > 0)
    {
        int node = queue.Dequeue();
        foreach (int next in Neighbors(node))
        {
            if (visited.TryAdd(next)) // false if already seen
                queue.Enqueue(next);
        }
    }

    // Iterate exactly the reached nodes — a dense, contiguous scan.
    Process(visited);
}
```

---

## CompressedIntSet

```csharp
public sealed class CompressedIntSet : ISet<int>, IReadOnlySet<int>
```

**Two caveats first, because they decide whether this type is for you.**

1. **There is no portable serialization format.** Celerity does not ship serializers, so
   `CompressedIntSet` cannot read or write the portable **Roaring** format. The Lucene / Druid /
   Spark posting-list interop that is the most common reason to reach for a Roaring bitmap is
   **not available here**. This is an in-process data structure, not an interop codec — which is
   why it is named for what it does rather than for Roaring.
2. **This is not the only compressed integer set in .NET.** A maintained pure-C# Roaring
   implementation exists (`Equativ.RoaringBitmaps`). What this type offers instead of novelty is
   integration: full mutability, verified Native AOT and trim compatibility on `net8.0` /
   `net9.0` / `net10.0`, published benchmarks against `HashSet<int>` on the project dashboard, and
   membership of the same [`BitSet`](#bitset) / [`SparseSet`](#sparseset) / [`IntSet`](#intset)
   family and shared test suites.

With that said: it is an **exact** set of 32-bit integers that partitions the value space into
**65,536-value chunks** and stores each chunk in the form that suits its density. Note the third
row: run encoding is **opt-in**, not something ordinary inserts and removals reach for.

| Container | Layout | Chosen when |
|---|---|---|
| **Sorted array** | `ushort[]` of offsets | the chunk is sparse (≤ 4096 values) |
| **Bitmap** | 1024 × 64-bit words (8 KB) | the chunk is dense (> 4096 values) |
| **Run-length** | `(start, length)` pairs | the chunk is clustered, runs cost less than either of the above, **and** `Optimize()` or `AddRange` has been asked to compress it |

4096 is where a sorted `ushort[]` and a 1024-word bitmap both cost 8 KB, so above it the bitmap is
never larger and answers `Contains` in `O(1)` instead of `O(log n)`.

It closes a real hole in the integer-set family:

| Shape | Type |
|---|---|
| dense and bounded | [`BitSet`](#bitset) |
| small and bounded, cleared often | [`SparseSet`](#sparseset) |
| unbounded, hash-probed | [`IntSet`](#intset) / `HashSet<int>` |
| **huge and sparse, set-algebra-heavy** | **`CompressedIntSet`** |

### The documented BCL-beating workload

**Set algebra over large, sparse integer sets** — intersecting or unioning ~1M values drawn from a
~100M-value space: inverted-index posting lists, bitmap analytics, column-store row-id sets, cohort
intersection. Two mechanisms:

- Inside a chunk the work is a sorted merge or a **whole-word bitmap operation** (64 values per
  ANDed word), not one hash probe and one random memory access per element.
- The chunk index is sorted, so an entire 65,536-value range is **skipped with a single
  comparison** whenever one side has nothing there. Cost tracks the number of *populated chunks*,
  not the number of elements.

Memory drops roughly **10x** against `HashSet<int>` for the sparse case, and far more for dense or
clustered data: a dense region collapses to one bit per value, a clustered one to four bytes per
run. `MemoryUsageInBytes` reports the current footprint.

`Contains` is where `HashSet<int>` still wins — a hash probe beats a binary search inside a chunk.
If point lookups are the whole workload and memory is not a concern, stay with `IntSet` /
`HashSet<int>`.

### Enumeration order

Chunk keys are the value's high 16 bits **with the sign bit flipped**, so the chunk index is sorted
by *signed* value and the set enumerates **in ascending order** from `int.MinValue` to
`int.MaxValue` — a guarantee `HashSet<int>` does not make. The full 32-bit range is storable,
negatives included.

### Compression is explicit

Run containers are produced by `Optimize()` and by `AddRange`, never speculatively on a single
`TryAdd`: deciding on every insert would cost more than it saves. This is the same
"compress once it has settled" contract as Roaring's own `runOptimize`.

- A single-element `TryAdd` / `Remove` landing in a run-encoded chunk **expands that chunk** back
  to its natural form first. Call `Optimize()` again after a burst of mutation.
- `Remove` never demotes a representation — a chunk that grew into a bitmap stays one until
  `Optimize()` (or a bulk set operation that rewrites it) says otherwise.
- Reading a chunk never changes it, so passing an optimized set as the *right-hand* operand of any
  set operation leaves its representation intact.

### Constructors

```csharp
CompressedIntSet()
CompressedIntSet(IEnumerable<int> source)
```

- The default constructor allocates no chunk storage until the first value is added.
- The `source` constructor silently deduplicates (matching BCL `HashSet<int>(IEnumerable<int>)`)
  and throws `ArgumentNullException` when `source` is `null`. There is **no capacity and no
  `loadFactor`** parameter — the structure has no table to pre-size.

### Properties

- `long Cardinality { get; }` — the number of elements. Always correct.
- `int Count { get; }` — the same number as an `int`. **Throws `OverflowException`** when the set
  holds more than `int.MaxValue` elements, which only a very wide `AddRange` can produce (the set
  can hold all 2^32 `int` values). Use `Cardinality` if that is reachable for your data.
- `long MemoryUsageInBytes { get; }` — the chunk index plus every container payload, excluding
  object headers. A measure of how well the data compressed; watch it across `Optimize()`. It counts
  the index's *capacity*, not just its used length, so it stays non-zero after a `Clear()` until
  `Optimize()` trims it.

### Methods

- `void Add(int item)` — throws `ArgumentException` on a duplicate.
- `bool TryAdd(int item)` — `true` if added, `false` if already present.
- `long AddRange(int start, int endInclusive)` — adds every value in the inclusive range and
  returns how many were **new**. A range landing in a chunk the set does not yet touch is stored as
  a **single run pair — four bytes of payload, whatever the range's width** (plus the array header,
  which `MemoryUsageInBytes` excludes too) — so this is the cheap way to
  build a clustered set. Throws `ArgumentOutOfRangeException` if `endInclusive < start`.
- `bool Contains(int item)` — `O(1)` in a bitmap chunk, `O(log n)` in an array or run chunk, and a
  single comparison against the chunk index when nothing covers the value.
- `bool Remove(int item)` — `true` if removed. A chunk emptied by its last removal is dropped.
- `void Clear()` — empties the set, dropping every container payload. The chunk index keeps its
  capacity so the set can be refilled without regrowing it, so `MemoryUsageInBytes` does not fall to
  zero after a `Clear()` — call `Optimize()` to hand that back. A `Clear()` on an already-empty set
  changes nothing and leaves active enumerators valid.
- `void Optimize()` — re-encodes every chunk in its smallest form (the only thing that produces
  run containers from existing data) and trims the chunk index and array containers to exact size.
  Purely a representation change: no element is added or removed, and active enumerators stay
  valid.
- `long IntersectCount(CompressedIntSet other)` — the size of the intersection, without building
  it. Allocation-free, and it skips a whole chunk with one key comparison wherever one side is
  empty. Throws `ArgumentNullException` for a `null` argument.
- `Enumerator GetEnumerator()` — allocation-free struct enumerator, ascending signed order.
- `void CopyTo(int[] array, int arrayIndex)` — matches `HashSet<int>.CopyTo` argument validation.

### Set operations (`ISet<int>` and `IReadOnlySet<int>`)

The full BCL `HashSet<int>` set-algebra surface, with `HashSet<int>` semantics exactly
(duplicate-tolerant `other`, self-aliasing `other == this`):

- **Mutating:** `UnionWith`, `IntersectWith`, `ExceptWith`, `SymmetricExceptWith`.
- **Query:** `IsSubsetOf`, `IsProperSubsetOf`, `IsSupersetOf`, `IsProperSupersetOf`, `Overlaps`, `SetEquals`.

Each throws `ArgumentNullException` when `other` is `null`. **Every one of them takes the chunk-wise
fast path when `other` is also a `CompressedIntSet`** — that is the workload the type exists for —
and otherwise falls back to the same element-at-a-time implementation the rest of the set family
uses, which is correct but forfeits the whole-chunk skipping. If you are intersecting two of these
sets, keep both as `CompressedIntSet`; do not project one through LINQ first.

As with the other sets, `ISet<int>.Add(int)` returns `bool` (equivalent to `TryAdd`), the concrete
`public void Add(int)` keeps its throw-on-duplicate behaviour, and `ICollection<int>.Add(int)`
ignores duplicates.

The type is single-threaded, and any structural mutation invalidates active enumerators.

### Usage example

```csharp
using Celerity.Collections;

// Two inverted-index posting lists: document ids drawn from a ~100M-document corpus.
var termA = new CompressedIntSet(PostingsFor("celerity"));
var termB = new CompressedIntSet(PostingsFor("collections"));

// The data has settled — re-encode each chunk in its smallest form.
termA.Optimize();
termB.Optimize();

// How many documents match both? No intersection is materialized.
long both = termA.IntersectCount(termB);

// Materialize the conjunction. Chunks only one side populates are skipped by key comparison.
termA.IntersectWith(termB);

foreach (int documentId in termA) // ascending order, no allocation
    Render(documentId);

// Ranges are the cheap case: a contiguous block in a fresh chunk is one run pair.
var recentlyIngested = new CompressedIntSet();
recentlyIngested.AddRange(90_000_000, 99_999_999); // 10M ids
recentlyIngested.Optimize();
Console.WriteLine(recentlyIngested.MemoryUsageInBytes); // hundreds of bytes, not tens of megabytes
```

---

## EnumMap&lt;TEnum, TValue&gt;

```csharp
public class EnumMap<TEnum, TValue>
    : IDictionary<TEnum, TValue?>, IReadOnlyDictionary<TEnum, TValue?>
    where TEnum : struct, Enum
```

A dictionary specialized for **enum keys**, backed by a dense value array indexed on the
enum's underlying integer value plus a parallel occupancy bit vector — the .NET analogue
of Java's `java.util.EnumMap`. It is the **dictionary counterpart of `EnumSet<TEnum>`**:
where `EnumSet` stores one *bit* per possible element, `EnumMap` stores one *value slot*
per possible key. Where `Dictionary<TEnum, TValue>` runs every key through
`EqualityComparer<TEnum>` and a hash table, `EnumMap` maps the underlying value straight
to an array slot:

- `this[key]` / `TryGetValue` / `ContainsKey` / `Add` / `Remove` are a shift, a mask, a
  single-`ulong` bit test, and a contiguous array access — no hash, no probe chain, no
  per-entry node allocation.
- Storage is contiguous and cache-resident (a `TValue[]` plus a `ulong[]` occupancy
  vector), so a full sweep is a linear array walk.
- Enumeration is **deterministic and ascending by underlying value** (the occupancy
  vector is walked low bit first) — a bonus over the hash-table dictionaries' unspecified
  order.

Presence is tracked out-of-band in the occupancy bit vector, so a key mapped to
`default(TValue)` (`0`, `null`, …) is a genuine entry, distinct from an absent key.

This is the classic dense direct-indexed map, made type-safe and generic — the map sibling
of the bit-flags set.

### Supported enums

The backing store is sized once from the enum's **maximum defined underlying value**
(shared with `EnumSet` via the same internal layout metadata), so `EnumMap` supports enums
whose members are **small, non-negative integers** — the default `0, 1, 2, …` declaration,
which covers the overwhelming majority of enums.

- An enum that declares a **negative** member throws `NotSupportedException` from the
  constructor (an array cannot be indexed by a negative value).
- An enum whose **maximum value exceeds `65535`** — a sparse or `[Flags]` power-of-two
  enum, for which a dense array would waste enormous memory — also throws
  `NotSupportedException`. Use `CelerityDictionary<TEnum, TValue, THasher>` for those.
- A runtime key **outside the supported range** (an out-of-range cast such as
  `(MyEnum)9999`) is rejected by the write surface (`Add` / `TryAdd` / `this[key] = …`)
  with `ArgumentOutOfRangeException`, and reported as absent by the read surface
  (`ContainsKey` / `TryGetValue` / `Remove`; the indexer getter throws
  `KeyNotFoundException`).

That bounded key universe is the one place `EnumMap` differs from the rest of the family
when consumed as [`IDictionary<TEnum, TValue?>`](#idictionarytkey-tvalue): `Add` on an
out-of-range cast throws instead of storing. It is still an honest implementation —
`ArgumentOutOfRangeException` *is* an `ArgumentException`, the failure the interface
already documents for a key it cannot accept — but code that funnels arbitrary casts
through the interface should expect it. Every other interface member behaves exactly as it
does on the hash-table dictionaries.

### Constructors

```csharp
EnumMap()
EnumMap(IEnumerable<KeyValuePair<TEnum, TValue>> source)
```

- The parameterless constructor creates an empty map. There is **no capacity or
  `loadFactor` parameter** — the storage size is fixed by the enum, not by the entry
  count.
- The `source` constructor copies the pairs, throwing `ArgumentException` on a duplicate
  key (matching `Add` and the BCL dictionaries) and `ArgumentNullException` if `source` is
  `null`. Copying from another `EnumMap<TEnum, TValue>` copies both backing arrays
  wholesale.
- Both throw `NotSupportedException` if `TEnum` is unsupported (see above).

### Methods and properties

- `TValue this[TEnum key] { get; set; }` — getter throws `KeyNotFoundException` for an
  absent key; setter adds a new entry or overwrites an existing one. A pure overwrite does
  not invalidate active enumerators (matching `Dictionary<,>`).
- `void Add(TEnum key, TValue value)` — throws `ArgumentException` on duplicate,
  `ArgumentOutOfRangeException` for an out-of-range key.
- `bool TryAdd(TEnum key, TValue value)` — `true` on success, `false` if the key already
  exists; throws `ArgumentOutOfRangeException` for an out-of-range key.
- `bool ContainsKey(TEnum key)` — single bit test.
- `bool ContainsValue(TValue? value)` — `O(n)` scan of occupied slots, `EqualityComparer<T>.Default` semantics.
- `bool TryGetValue(TEnum key, out TValue? value)`.
- `bool Remove(TEnum key)` / `bool Remove(TEnum key, out TValue? value)` — clears the slot
  (releasing any reference) and the occupancy bit.
- `void Clear()`
- `int Count { get; }`
- `KeyCollection Keys` / `ValueCollection Values` — allocation-free struct views, ascending
  by key.
- `Enumerator GetEnumerator()` — allocation-free struct enumerator, ascending order.

### Usage example

```csharp
using Celerity.Collections;

enum Priority { Low, Normal, High, Critical }

var queued = new EnumMap<Priority, int>
{
    [Priority.Low] = 3,
    [Priority.High] = 7,
};

queued[Priority.High]++;                             // direct array index, no hashing
Console.WriteLine(queued[Priority.High]);            // 8

Console.WriteLine(queued.ContainsKey(Priority.Normal)); // False — a single bit test
Console.WriteLine(queued.TryGetValue(Priority.Low, out var n)); // True, n == 3

foreach (var (p, count) in queued.Select(kvp => (kvp.Key, kvp.Value)))
{
    // ascending by underlying value: Low, High
}
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

> **Hash entropy.** The 64 bits come from the hasher's own `Hash64` when it implements
> [`IHashProvider64<T>`](hashing.md#ihashprovider64t), and otherwise from widening its 32-bit
> code. Widening spreads the code over the 64-bit range but creates no entropy: the reachable
> space stays 2<sup>32</sup>, which puts a floor of roughly `n / 2^32` under the realized
> false-positive rate no matter how many bits you allocate — about 0.12% at 10<sup>7</sup>
> elements and 1.2% at 10<sup>8</sup>. Past ~10<sup>8</sup> elements, use a 64-bit hasher.

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

> **Hash entropy.** The 64 bits come from the hasher's own `Hash64` when it implements
> [`IHashProvider64<T>`](hashing.md#ihashprovider64t), and otherwise from widening its 32-bit
> code. Widening spreads the code over the 64-bit range but creates no entropy: the reachable
> space stays 2<sup>32</sup>, which puts a floor of roughly `n / 2^32` under the realized
> false-positive rate no matter how many bits you allocate — about 0.12% at 10<sup>7</sup>
> elements and 1.2% at 10<sup>8</sup>. Past ~10<sup>8</sup> elements, use a 64-bit hasher.

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

## XorFilter&lt;T, THasher&gt;

A **build-once, immutable** **probabilistic** set membership filter that is **smaller and
faster to query** than `BloomFilter` or `CuckooFilter` at the same false-positive rate,
parameterized on a custom hash provider. Like the other filters it answers "is this element
*possibly* in the set?" with **no false negatives** and a bounded false-positive rate — but
it is the *static* member of the family: the whole element set is supplied once at
construction and the filter is then immutable (there is **no `Add`, `Remove`, or `Clear`**).

```csharp
public class XorFilter<T, THasher>
    where THasher : struct, IHashProvider<T>
```

### How it works

The structure is the **xor filter** of Graf &amp; Lemire (*"Xor Filters: Faster and Smaller
Than Bloom and Cuckoo Filters"*, ACM JEA 2020). The backing store is a byte array of
`3 · blockLength ≈ 1.23 · n` 8-bit fingerprints, split into three equal segments; each
element maps to one slot in each segment (`h0`, `h1`, `h2`), and the filter is built so that

```
fingerprint(x) == store[h0] XOR store[h1] XOR store[h2]
```

holds for every element `x` of the set. Construction assigns the fingerprints by **peeling**
the 3-uniform hypergraph of element→slot incidences (repeatedly claiming a slot touched by
exactly one remaining element, then back-filling in reverse), retrying with a fresh internal
seed on the rare peel failure. A query recomputes the three slots and the fingerprint from a
**single** `IHashProvider<T>.Hash` call and compares — exactly **three memory probes and two
XORs**, with no probe loop and no data-dependent branch.

> **Hash entropy.** The 64-bit element key comes from the hasher's own `Hash64` when it
> implements [`IHashProvider64<T>`](hashing.md#ihashprovider64t), and otherwise from widening
> its 32-bit code with `fmix64`. Widening spreads the code over the 64-bit range but creates
> no entropy: the reachable space stays 2<sup>32</sup>, which puts a floor of roughly
> `n / 2^32` under the realized false-positive rate — and, because the construction
> deduplicates on the key, elements that share one are silently merged. Past ~10<sup>8</sup>
> elements, use a 64-bit hasher.

### Xor vs. Bloom vs. Cuckoo

| | `BloomFilter` | `CuckooFilter` | `XorFilter` |
|---|---|---|---|
| No false negatives | ✅ | ✅ | ✅ |
| Mutable after build (`Add`) | ✅ | ✅ | ❌ (build-once) |
| Delete individual elements | ❌ | ✅ | ❌ |
| Lookup cost | `k` bit probes | ≤ 2 buckets | **3 probes + 2 XORs (branch-free)** |
| Bits per element (@ ~0.4%) | ~12–14 | ~12 | **~9.84** |

Reach for `XorFilter` when the element set is **known up front and does not change** — static
allow/deny lists, a precomputed "have I seen this key?" gate in front of an expensive exact
lookup, read-only shard membership. If the set grows over the filter's lifetime use
`BloomFilter`; if it also shrinks use `CuckooFilter`; if you need exact membership or to
enumerate the elements use `FrozenCeleritySet` / `CeleritySet`.

### Sizing and the false-positive rate

The fingerprint width is fixed at **8 bits**, so the false-positive probability is a constant
`1 / 2⁸ ≈ 0.39%`, independent of the element count — unlike a Bloom filter, an xor filter does
not degrade as it fills; it is sized exactly for its element set at build time. The store is
`3 · blockLength ≈ 1.23 · n` bytes (`SlotCount`), giving ≈ 9.84 `BitsPerElement`.

### Constructor

```csharp
public XorFilter(IEnumerable<T> source)
```

Builds a filter holding exactly the elements of `source`. Because the source is a *set*, the
constructor **deduplicates internally**: two elements that hash to the same 64-bit value
collapse to one entry (harmless — both still test present), so `Count` is the number of
*distinct* element hashes, which can be below the source length.

**Throws:**

- `ArgumentNullException` if `source` is `null`.
- `InvalidOperationException` if the peeling construction fails to converge after many reseeds
  — only possible with a pathologically degenerate hasher, and effectively never in practice.

### Methods and properties

- `bool Contains(T item)` — `false` ⇒ definitely absent (no false negatives); `true` ⇒
  probably present, subject to the ~0.4% false-positive rate.
- `int Count { get; }` — the number of distinct element hashes represented (the deduplicated
  element count).
- `int SlotCount { get; }` — the number of 8-bit fingerprint slots (`3 · blockLength`), which
  is also the filter's size in bytes.
- `const int FingerprintBits` — the fingerprint width in bits, fixed at 8 for every instance,
  so it is a constant on the type rather than an instance property (unlike `CuckooFilter`,
  whose width is chosen per filter).
- `double FalsePositiveRate { get; }` — the fixed theoretical rate, `1 / 2⁸ ≈ 0.0039`.
- `double BitsPerElement { get; }` — the storage cost per represented element
  (`SlotCount · 8 / Count`), ≈ 9.84 for a well-sized filter; `0` for an empty filter.

### Default-element handling

Because the filter stores fingerprints, not keys, it needs **no out-of-band handling** for
`default(T)` — a zero `int`, `Guid.Empty`, or the empty string is hashed like any other
element. A `null` reference is mapped to a fixed base hash so the filter never invokes the
hasher with `null` (the string hashers throw on `null`).

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// A precomputed allow-list of known-good API keys, built once at startup and never mutated.
string[] issuedKeys = LoadIssuedApiKeys();
var known = new XorFilter<string, StringXxHash3Hasher>(issuedKeys);

bool MightBeIssued(string apiKey)
{
    // A false ends the request immediately (no false negatives); a true falls through to the
    // authoritative — and more expensive — exact lookup, wrong only ~0.4% of the time.
    if (!known.Contains(apiKey))
        return false;
    return Database.ApiKeyExists(apiKey);
}
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

## RankSelectBitVector

```csharp
public sealed class RankSelectBitVector
```

An **immutable** succinct index over a dense bit vector that answers the two positional
queries `BitSet` cannot:

- **`Rank(i)`** — how many bits are set *below* position `i`, in `O(1)`.
- **`Select(k)`** — the position of the `k`-th set bit, in `O(log n)`.

### When *not* to use this — read first

The vector is a **snapshot taken at construction** and the type has no mutating member.
A caller who changes the underlying bits must build a new `RankSelectBitVector`, which
costs `O(Length / 64)`. That makes this the wrong type for anything that mutates while
it queries — free-slot allocators, ECS id compaction, live "visited" sets — where a
rebuild per update is strictly *worse* than the naive counting loop. Use `BitSet` (or
`SparseSet`) there, and snapshot into a `RankSelectBitVector` only once the bits have
settled.

### The documented BCL-beating workload

There is no BCL counterpart at all: `BitArray` exposes neither rank nor select,
`BitOperations.PopCount` is per-word, and .NET 8/9/10 ship no succinct data-structure
support — so the honest baseline is the loop a caller writes by hand, popcounting every
word below the query position. That loop is `O(index / 64)`: at the midpoint of a
100-million-bit vector it is roughly 780,000 iterations, which this type replaces with
two index loads and one masked population count, at a cost independent of the position.

The winning workloads are all **build-once**: dense↔sparse index remapping in column
stores (map a dense row ordinal to its position in a sparse column and back), succinct
and compressed tries, and wavelet trees. See the
[rank/select benchmark](https://marius-bughiu.github.io/Celerity/dev/bench/?collection=RankSelectBitVector)
on the dashboard, whose baseline arm *is* that hand-rolled loop.

### How it works, and what it costs

The index is two arrays: an `int` per **256-bit superblock** holding the number of set
bits before it, plus a `byte` per **64-bit word** holding the number of set bits before
that word *within its superblock* (at most 192, so a byte suffices — which is what pins
the superblock at 256 bits). `Rank` is then one load from each array plus a single
masked `POPCNT` of the query word. `Select` binary-searches the superblock array, walks
the at most four words inside the chosen superblock, and resolves the position within
that word by a six-step popcount narrowing.

Together the two arrays cost 8 bytes per 32 bytes of vector — **25% over the bits
themselves**, the same price as the classic rank9 layout and the standard cost of an
exact rank that touches a single word. `IndexSizeInBytes` reports the exact figure for a
given instance.

### Constructors

```csharp
public RankSelectBitVector(BitSet bits)
public RankSelectBitVector(int length, ReadOnlySpan<ulong> words)
public RankSelectBitVector(int length, IEnumerable<int> positions)
```

- `RankSelectBitVector(BitSet bits)` — snapshots and indexes an existing `BitSet`. Later
  mutations of `bits` do not affect the vector.
- `RankSelectBitVector(int length, ReadOnlySpan<ulong> words)` — reads packed words,
  where bit `i` is bit `i % 64` of word `i / 64`. The span must hold at least
  `ceil(length / 64)` entries; any further words, and any bits at or beyond `length` in
  the final word, are ignored.
- `RankSelectBitVector(int length, IEnumerable<int> positions)` — sets the bits at the
  given positions, in any order. Repeated positions are idempotent.

**Throws:**

- `ArgumentNullException` if `bits` or `positions` is `null`.
- `ArgumentOutOfRangeException` if `length` is negative, or a position is outside
  `[0, length)`.
- `ArgumentException` if `words` is too short to hold `length` bits.

### Methods and properties

| Member | Description |
| --- | --- |
| `int Length { get; }` | The number of bits in the vector. |
| `int Count { get; }` | The number of set bits. Precomputed at construction, so this is a field read rather than the `O(Length / 64)` scan `BitSet.Count` performs. |
| `int IndexSizeInBytes { get; }` | The size of the rank/select index, excluding the bits themselves — the space price of the constant-time rank. |
| `bool this[int index] { get; }` / `bool Get(int index)` | The value of a single bit. `ArgumentOutOfRangeException` outside `[0, Length)`. |
| `int Rank(int index)` | The number of set bits strictly below `index`, in `O(1)`. `index` runs from `0` to `Length` **inclusive**: `Rank(0)` is `0` and `Rank(Length)` is `Count`. `ArgumentOutOfRangeException` outside that range. |
| `int Rank0(int index)` | The number of *clear* bits strictly below `index` — the complement identity `index - Rank(index)`, with the same bounds. |
| `int Select(int rank)` | The position of the `rank`-th set bit, counting from zero, in `O(log n)`. Satisfies `Rank(Select(k)) == k`. Throws `ArgumentOutOfRangeException` if `rank` is outside `[0, Count)`. |
| `bool TrySelect(int rank, out int position)` | The non-throwing form: returns `false` and sets `position` to `-1` when `rank` is outside `[0, Count)`. |
| `BitSet ToBitSet()` | A new, mutable `BitSet` holding a copy of the indexed bits — the way to edit a vector and rebuild the index over the result. |

The type holds no mutable state after construction, so instances are safe to share
across threads.

### Choosing it

Reach for `RankSelectBitVector` when a bit vector is **filled, frozen, and then queried
many times** by position, and the query is "how many before here" or "where is the k-th
one". If you only need the total set-bit count, `BitSet.Count` already gives it in one
pass with no index. If the bits keep changing, stay on `BitSet` — the rebuild dominates.
If you need prefix *sums over numbers* rather than counts over bits, and the values
mutate, that is `FenwickTree<T>`.

### Usage example

```csharp
using Celerity.Collections;

// A column store keeps values only for the rows that are non-null. `present` marks
// which logical rows those are; `values` is the dense array of just those rows.
var present = new BitSet(rowCount);
foreach (int row in nonNullRows)
    present[row] = true;

// Freeze the presence mask and index it once, after the load has finished.
var index = new RankSelectBitVector(present);

// Logical row -> dense slot: the number of present rows before it.
if (index[row])
    Console.WriteLine(values[index.Rank(row)]);

// Dense slot -> logical row: the inverse direction, in O(log n).
int logicalRow = index.Select(slot);

// Both directions without the index would be a scan of the whole mask.
Console.WriteLine($"{index.Count} present rows, index costs {index.IndexSizeInBytes} bytes");
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
bias-prone raw formula.

### The hash space, and why the hasher choice matters past ~10<sup>8</sup>

The 64-bit hash comes from a **single** hasher call either way, but how much entropy that
call carries depends on the hasher, and the estimator adapts:

- **A hasher implementing [`IHashProvider64<T>`](hashing.md#ihashprovider64t)** — for example
  `Int64WangHasher`, `Int64Murmur3Hasher`, `GuidHasher`, or any of the nine 64-bit `string`
  hashers — supplies all 64 bits directly. The hash space is then 2<sup>64</sup>, which
  dwarfs any realistic cardinality, so no large-range correction is needed and the estimate
  holds its `StandardError` across the whole range. **Use one of these when counting beyond
  ~10<sup>8</sup> distinct elements.**
- **A 32-bit-only hasher** has its code avalanched into 64 bits by the SplitMix64 finalizer.
  That finalizer is a bijection, so it loses nothing — but it also creates nothing: the
  reachable hash space is still 2<sup>32</sup>, and distinct elements start sharing a hash
  as the count approaches it (~0.12% of elements at 10<sup>7</sup>, 1.2% at
  10<sup>8</sup>, 10.8% at 10<sup>9</sup>). The estimator detects this case and applies the
  classical Flajolet **large-range correction**, `−2^32 · ln(1 − E / 2^32)`, which recovers
  the true cardinality from the saturated distinct-hash count.

Either way `Add` costs one hasher call, and the selection is a compile-time type test the
JIT folds away — each instantiation compiles to a single straight-line path, and neither
allocates.

### Constructors

```csharp
public HyperLogLog(int precision = 14)

public HyperLogLog(
    IEnumerable<T> source,
    int precision = 14)
```

The first overload creates an empty estimator with `2^precision` registers. The
`IEnumerable<T>` overload pre-populates it with the source's distinct elements.

`precision` must be between `MinPrecision` (4, `m = 16`) and `MaxPrecision` (16,
`m = 65536`) inclusive. Larger values cost more memory but lower the standard error.

**Throws:**

- `ArgumentOutOfRangeException` if `precision` is outside `[MinPrecision, MaxPrecision]`.
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
// Int64Murmur3Hasher implements IHashProvider64<long>, so the estimator sees the full
// 2^64 hash space and stays accurate past 10^8 distinct ids.
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

> **Hash entropy.** The 64 bits come from the hasher's own `Hash64` when it implements
> [`IHashProvider64<T>`](hashing.md#ihashprovider64t), and otherwise from widening its 32-bit
> code. Widening spreads the code over the 64-bit range but creates no entropy: the reachable
> space stays 2<sup>32</sup>, so past ~10<sup>8</sup> distinct elements a growing fraction of
> them share a code and have their counts pooled — an overestimate the `epsilon` budget does
> not account for. Past that scale, use a 64-bit hasher.

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

## LruCache&lt;TKey, TValue, THasher&gt;

A fixed-capacity **least-recently-used (LRU) cache** parameterized on a custom hash provider: an
`O(1)` get/put map that automatically **evicts the least-recently-used entry** when a new key would
push the count past `Capacity`.

```csharp
public class LruCache<TKey, TValue, THasher>
    : IReadOnlyCollection<KeyValuePair<TKey, TValue?>>
    where THasher : struct, IHashProvider<TKey>
```

The BCL ships no bounded LRU cache. The idiomatic .NET LRU pairs a
`Dictionary<TKey, LinkedListNode<(TKey, TValue)>>` with a `LinkedList<(TKey, TValue)>`, which
**heap-allocates a `LinkedListNode` per insertion** and threads its recency order through pointers
scattered across the managed heap. `LruCache` instead threads an **intrusive doubly-linked list
through fixed-size node arrays** (allocated once, sized to `Capacity`) alongside an open-addressed
key→node-slot index, so after construction the hot get/put/evict path performs **no allocation at
all**. The documented BCL-beating workload is a hot, bounded cache under continuous eviction churn
(memoize the last `N` results), where the array-backed list wins on allocation and locality.

### How it works

Every entry occupies a slot in the fixed node arrays. Occupied slots form a doubly-linked
**most-recently-used → least-recently-used** chain via parallel `prev`/`next` index arrays; the
free slots form a stack via the same `next` array. A key→slot index dogfoods
`CelerityDictionary<TKey, int, THasher>` — which is where `THasher` is used, and which also supplies
the out-of-band handling for a `default(TKey)` or `null` key (so a string hasher is never invoked
with `null`). Because a slot index is **stable across recency reordering**, a cache hit relinks the
chain but never touches the index. When a new key arrives at capacity, the tail (LRU) slot is
evicted and **recycled in place** for the newcomer, so steady-state churn neither allocates nor
frees.

### Reads are mutating

LRU semantics require a lookup to count as a *use*. The indexer getter and `TryGet` therefore
**promote the entry to most-recently-used**, which reorders the recency list and invalidates any
in-progress enumerator (matching "collection was modified" semantics). The one exception is a hit on
the entry that is *already* most-recently-used: there is nothing to reorder, so the promotion is a
no-op and active enumerators stay valid. To inspect the cache without
disturbing recency order — and without invalidating an active enumerator — use `TryPeek`,
`ContainsKey`, or the `TryPeekLeastRecentlyUsed` / `TryPeekMostRecentlyUsed` inspectors.

### Constructors

```csharp
public LruCache(int capacity)

public LruCache(
    int capacity,
    IEnumerable<KeyValuePair<TKey, TValue?>> source)
```

The first overload creates an empty cache that retains at most `capacity` entries. The `source`
overload primes it by inserting each pair in enumeration order, so if the source yields more than
`capacity` distinct keys the earliest ones are evicted and the last `capacity` survive as the
most-recently-used entries.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity` is less than 1.
- `ArgumentNullException` if `source` is `null` (enumerable overload).

### Methods and properties

- `int Capacity` — the maximum number of entries retained before eviction.
- `int Count` — the current number of entries (never greater than `Capacity`).
- `TValue? this[TKey key]` — **get** promotes the entry to most-recently-used and throws
  `KeyNotFoundException` if absent; **set** adds the key (evicting the LRU entry first if full) or
  overwrites an existing value, and in either case promotes it to most-recently-used.
- `bool TryGet(TKey key, out TValue? value)` — a *use*: promotes the entry to most-recently-used on
  a hit.
- `bool TryPeek(TKey key, out TValue? value)` — reads without changing recency (does not count as a
  use).
- `bool ContainsKey(TKey key)` — membership test; does not change recency.
- `void AddOrUpdate(TKey key, TValue? value)` — inserts (evicting the LRU entry if full) or
  overwrites, promoting to most-recently-used.
- `void Add(TKey key, TValue? value)` — inserts as most-recently-used; throws `ArgumentException`
  if the key already exists.
- `bool TryAdd(TKey key, TValue? value)` — inserts as most-recently-used if absent (evicting the LRU
  entry if full); returns `false` and leaves the cache unchanged if the key exists.
- `bool Remove(TKey key)` / `bool Remove(TKey key, out TValue? value)` — removes an entry, optionally
  returning its value.
- `void Clear()` — removes all entries; the backing storage (sized to `Capacity`) is retained.
- `bool TryPeekLeastRecentlyUsed(out TKey? key, out TValue? value)` — reads the next eviction
  candidate without changing recency.
- `bool TryPeekMostRecentlyUsed(out TKey? key, out TValue? value)` — reads the freshest entry
  without changing recency.
- `Enumerator GetEnumerator()` — an allocation-free struct enumerator yielding entries in
  **most-recently-used → least-recently-used** order. Enumeration is a peek and does not change
  recency.

### Default-key handling

`default(TKey)` — `0` for `int`, `null` for reference types — is a valid key. The dogfooded index
stores it out-of-band, so the whole surface (get / set / peek / remove) works with it and the hasher
is never invoked with `null`.

### Thread safety

`LruCache` is not thread-safe; concurrent callers must synchronize externally. In particular, note
that reads mutate recency, so even a read-mostly workload needs a write lock (or an external
concurrent cache) under concurrency.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// A bounded memoization cache for an expensive lookup, keyed by user id.
var cache = new LruCache<long, Profile, Int64WangHasher>(capacity: 10_000);

Profile GetProfile(long userId)
{
    if (cache.TryGet(userId, out Profile? cached))
        return cached!;               // a hit promotes the entry to most-recently-used

    Profile fresh = LoadFromDatabase(userId);
    cache[userId] = fresh;            // insert; the least-recently-used profile is evicted if full
    return fresh;
}

// Inspect the cache without disturbing eviction order.
if (cache.TryPeekLeastRecentlyUsed(out long coldKey, out _))
    Console.WriteLine($"Next to be evicted: user {coldKey}");
```

## Deque&lt;T&gt;

A growable **double-ended queue** backed by a single **circular buffer**: an array with a moving
front index, so pushing and popping at **either** end is `O(1)` amortized and the elements stay
contiguous.

```csharp
public sealed class Deque<T> : IReadOnlyList<T>
```

The BCL ships no double-ended queue. `Queue<T>` is FIFO-only (no push-front / pop-back), `Stack<T>`
is LIFO-only, and the only type that supports `O(1)` at **both** ends — `LinkedList<T>` —
**heap-allocates a node per element** and threads its order through pointers scattered across the
managed heap. `Deque<T>` instead keeps every element in one array indexed by a moving `head` plus a
count, wrapping around the ends, so it is the array-backed deque the BCL lacks — the .NET analogue of
Java's `ArrayDeque` or C++'s `std::deque`. It also offers `O(1)` random access by index, which a
linked list cannot.

The documented BCL-beating workload is any sequence that pushes and pops at both ends — a bounded
FIFO queue, a sliding window, a work-stealing / undo buffer — where `Deque<T>` wins on **allocation**
(a warm bounded churn reuses the array with wrap-around and allocates nothing, where `LinkedList<T>`
allocates and frees a node per operation) and on **cache locality** (contiguous storage versus
pointer-chased nodes).

### How it works

Elements live in a single `T[]`; a `head` index marks the front and a `count` marks how many slots
are occupied, wrapping modulo the array length. A `PushFront` steps `head` back one slot (wrapping to
the end); a `PushBack` writes at `head + count` (wrapping); the pops mirror them and clear the vacated
slot so references are released for GC. When the buffer fills, it grows by doubling, **re-linearizing**
the elements into a fresh array so the front returns to index `0` (making a push `O(1)` amortized,
`O(n)` on the growth step). Because the storage is contiguous, the front-relative indexer, `ToArray`,
`CopyTo`, and enumeration are simple index arithmetic — at most two `Array.Copy` runs across the
wrap point.

### Constructors

```csharp
public Deque()
public Deque(int capacity)
public Deque(IEnumerable<T> collection)
```

- The parameterless constructor allocates nothing until the first push.
- The `capacity` overload pre-sizes the backing array so an expected number of pushes avoids growth.
- The `collection` overload copies the elements in enumeration order, so the first element yielded
  becomes the front and the last becomes the back.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity` is negative.
- `ArgumentNullException` if `collection` is `null`.

### Methods and properties

- `int Count` — the number of elements currently in the deque.
- `int Capacity` — the number of elements the deque can hold before its backing array must grow.
- `T this[int index]` — the element at position `index` **counting from the front** (`0` is the
  front, `Count - 1` the back); get and set both throw `ArgumentOutOfRangeException` if out of range.
  A set is an in-place replacement and does **not** invalidate an active enumerator (matching
  `List<T>`).
- `void PushFront(T item)` / `void PushBack(T item)` — add at the front / back (`O(1)` amortized).
- `T PopFront()` / `T PopBack()` — remove and return the front / back element; throw
  `InvalidOperationException` if empty.
- `T PeekFront()` / `T PeekBack()` — read the front / back element without removing it; throw
  `InvalidOperationException` if empty.
- `bool TryPopFront(out T item)` / `bool TryPopBack(out T item)` — non-throwing pops; return `false`
  when empty.
- `bool TryPeekFront(out T item)` / `bool TryPeekBack(out T item)` — non-throwing peeks; return
  `false` when empty.
- `bool Contains(T item)` — linear `O(n)` membership test using `EqualityComparer<T>.Default`.
- `T[] ToArray()` — a new array of the elements in front-to-back order.
- `void CopyTo(T[] array, int arrayIndex)` — copies the elements, front to back, into `array`.
- `int EnsureCapacity(int capacity)` — grows the backing array if needed; returns the resulting
  capacity.
- `void TrimExcess()` — shrinks the backing array to exactly `Count`, re-linearizing so the front
  sits at index `0`.
- `void Clear()` — removes all elements; the backing array is retained (use `TrimExcess` to release
  it). Clearing an **already-empty** deque is a true no-op and does **not** invalidate an active
  enumerator, matching the rest of the collection family.
- `Enumerator GetEnumerator()` — an allocation-free struct enumerator yielding elements **front to
  back**; a structural modification during enumeration throws `InvalidOperationException`.

### Thread safety

`Deque<T>` is not thread-safe; concurrent callers must synchronize externally.

### Usage example

```csharp
using Celerity.Collections;

// A fixed sliding window over the most recent N samples: push new samples at the back and drop the
// oldest off the front. The circular buffer is reused with wrap-around, so this loop allocates nothing.
var window = new Deque<double>(capacity: 100);

void Record(double sample)
{
    window.PushBack(sample);
    if (window.Count > 100)
        window.PopFront();          // evict the oldest — O(1), no shifting
}

// Random access by position, newest at the back.
double newest = window[window.Count - 1];

// A deque doubles as a double-ended work queue: take work from either end.
var work = new Deque<int>(new[] { 1, 2, 3 });
work.PushFront(0);                  // [0, 1, 2, 3]
int hi = work.PopFront();           // 0 — high-priority, from the front
int lo = work.PopBack();            // 3 — low-priority, from the back
```


## DisjointSet&lt;T&gt;

A **disjoint-set** (union-find) over arbitrary elements. It partitions the elements it holds into non-overlapping sets and answers *"are these two in the same set?"* (`Connected`) and *"merge these two sets"* (`Union`) in near-constant amortized time. Implements `IReadOnlyCollection<T>`.

```csharp
public sealed class DisjointSet<T> : IReadOnlyCollection<T>
    where T : notnull
```

The element type must be non-null; equality uses `EqualityComparer<T>.Default`.

The BCL ships no union-find structure. The idiomatic substitutes are both super-linear for a run of merges: keeping a `Dictionary<T, HashSet<T>>` from element to its group and copying the smaller group into the larger on every union is `O(n)` per merge (`O(n²)` to build one component from `n` singletons), and rebuilding a graph to run a BFS/DFS per connectivity query is `O(V + E)` *every* query. `DisjointSet<T>` is the near-`O(1)` structure they approximate.

### How it works

Each set is a forest of parent pointers packed into dense `int[]` arrays (an element→slot map turns arbitrary keys into dense indices). Two classic optimizations keep the trees flat:

- **Union by size** — the smaller tree is hung under the larger tree's root, so heights grow slowly.
- **Path halving** — every `Find` points each node it walks at its grandparent, flattening the path it just traversed.

Together these give `Union`, `Find`, and `Connected` an `O(α(n))` amortized cost, where `α` is the inverse-Ackermann function and is `≤ 4` for any practical `n` — effectively `O(1)`.

### The documented BCL-beating workload

Any **incremental connectivity / connected-components** pass — a stream of `Union` operations interleaved with `Connected` queries: union of equivalence classes, Kruskal's minimum spanning tree, clustering, image segmentation, cycle detection in an undirected graph. `DisjointSet<T>` runs the whole stream in near-linear total time where the `Dictionary`-of-`HashSet` merge approach is quadratic. See the [union-find benchmark](https://marius-bughiu.github.io/Celerity/dev/bench/?collection=DisjointSet) on the dashboard.

### Constructors

```csharp
public DisjointSet()
public DisjointSet(int capacity)
public DisjointSet(IEnumerable<T> elements)
```

- The parameterless constructor starts empty with a small default capacity.
- The `capacity` overload pre-sizes the backing storage to hold at least `capacity` elements before the first growth.
- The `IEnumerable<T>` overload seeds each distinct element as its own singleton set, in enumeration order; duplicates after the first are ignored.

**Throws:**

- `ArgumentOutOfRangeException` if `capacity < 0`.
- `ArgumentNullException` if `elements` is `null` (enumerable overload).

### Methods and properties

| Member | Description |
|--------|-------------|
| `int Count` | Number of elements. |
| `int SetCount` | Number of disjoint sets (connected components). Starts equal to `Count` and drops by one on every effective `Union`. |
| `int Capacity` | Elements the backing storage can hold before it must grow. |
| `bool Add(T element)` | Adds `element` as a new singleton. Returns `false` if already present. |
| `bool Contains(T element)` | Whether `element` is present. |
| `bool Union(T a, T b)` | Merges the sets containing `a` and `b`, **auto-adding either if absent** (so it doubles as the edge-insertion primitive). Returns `true` if they were in different sets and are now merged; `false` if already together. |
| `T Find(T element)` | The representative element of `element`'s set. Two elements are in the same set iff their representatives are equal. Throws `KeyNotFoundException` if absent. |
| `bool TryFind(T element, out T representative)` | Non-throwing `Find`. |
| `bool Connected(T a, T b)` | Whether `a` and `b` are in the same set. A **pure query** — unlike `Union` it never adds a missing element, returning `false` if either is absent. |
| `int ComponentSize(T element)` | Number of elements in `element`'s set (`≥ 1`). Throws `KeyNotFoundException` if absent. |
| `IReadOnlyList<IReadOnlyList<T>> GetComponents()` | A snapshot of the current partition as grouped element lists (`Count == SetCount`). `O(n)`. |
| `void Clear()` | Removes all elements. |
| `Enumerator GetEnumerator()` | A struct enumerator over the elements in insertion order. |

The representative returned by `Find` is stable only between mutations — a later `Union` may change which element represents a set. `Connected` / `Find` compress internal paths but do not count as structural changes, so they do not invalidate an in-flight enumerator; `Add`, an effective `Union`, and `Clear` do.

### Choosing it

Reach for `DisjointSet<T>` when you are tracking connectivity or equivalence classes that only ever **grow by merging** — you union pairs and ask whether two elements are connected, or how many distinct groups remain. It does not support splitting a set back apart (no `un-union`), and it is not an `ISet<T>`: if you want element membership with add/remove/set-algebra, use `CeleritySet` or the BCL `HashSet<T>` instead.

### Usage example

```csharp
using Celerity.Collections;

// Detect a cycle while adding undirected edges (union-find cycle detection).
var uf = new DisjointSet<string>();
(string, string)[] edges =
{
    ("a", "b"), ("b", "c"), ("d", "e"), ("c", "a") // the last edge closes a cycle a-b-c-a
};

foreach (var (u, v) in edges)
{
    if (uf.Connected(u, v))
        Console.WriteLine($"Edge {u}-{v} closes a cycle");
    else
        uf.Union(u, v);
}

Console.WriteLine($"{uf.SetCount} connected component(s)");   // 2: {a,b,c} and {d,e}

// Enumerate the components.
foreach (var component in uf.GetComponents())
    Console.WriteLine(string.Join(", ", component));
```

## IndexedPriorityQueue&lt;TElement, TPriority, THasher&gt;

An **addressable (indexed) priority queue**: a binary min-heap that maps each element to its position in the heap, so — unlike the BCL `PriorityQueue<TElement, TPriority>` — it can **change a queued element's priority** (`Update` / decrease-key / increase-key) and **remove an arbitrary element** (`Remove`) in `O(log n)`, and answer `Contains` / `TryGetPriority` in `O(1)`. Implements `IReadOnlyCollection<KeyValuePair<TElement, TPriority>>`.

```csharp
public sealed class IndexedPriorityQueue<TElement, TPriority, THasher>
    : IReadOnlyCollection<KeyValuePair<TElement, TPriority>>
    where THasher : struct, IHashProvider<TElement>
```

Each element is a **key**: it appears in the queue at most once, and equality uses `EqualityComparer<TElement>.Default` through the supplied `THasher`. The `THasher` is a struct implementing `IHashProvider<TElement>`, so the element hashing behind the index devirtualizes and inlines.

The BCL `PriorityQueue<TElement, TPriority>` is a plain binary heap with no handle to an element already inside it: it exposes neither a priority update nor an arbitrary remove. The idiomatic workaround is **lazy deletion** — re-enqueue the element with its new priority and skip stale copies when they surface at the top — which lets the heap grow to `O(operations)` rather than `O(distinct elements)` and still cannot answer *"what is this element's current priority?"*. `IndexedPriorityQueue` keeps the heap at exactly the live elements.

### How it works

Two parallel arrays hold the heap (`_elements[i]` / `_priorities[i]`, a 0-based binary heap: node `i`'s children are `2i+1` and `2i+2`). Beside them, an **element→heap-slot index** — a dogfooded `CelerityDictionary<TElement, int, THasher>` — records where each element currently sits. Every sift/swap updates the index in lockstep, so `Update` and `Remove` locate their element in `O(1)` and then restore the heap invariant in `O(log n)` by sifting the affected slot up or down. Because the index is a `CelerityDictionary`, the out-of-band `default(TElement)` / `null` element is handled for free, exactly as in the rest of the family.

It is a **min-heap** by default (`Comparer<TPriority>.Default`): `Peek` and `Dequeue` return the element with the smallest priority. Pass a custom `IComparer<TPriority>` to invert the order (a max-heap) or to order by any other key.

### The documented BCL-beating workload

The **priority-relaxation loop** at the heart of Dijkstra's shortest paths, Prim's minimum spanning tree, A\*, and discrete-event simulation: seed the frontier, then repeatedly `Update` (decrease-key) an element's priority and `Dequeue` the current minimum. The addressable heap keeps its size at `O(distinct elements)` and updates a priority in `O(log n)`, where the lazy-deletion substitute over a BCL `PriorityQueue` grows the heap by one entry per relaxation and pays to skip the stale ones. It pairs with [`DisjointSet<T>`](#disjointsett) (union-find / Kruskal's MST) to cover the graph-algorithm primitives the BCL omits. See the [priority-queue benchmark](https://marius-bughiu.github.io/Celerity/dev/bench/?collection=IndexedPriorityQueue) on the dashboard.

### Constructors

```csharp
public IndexedPriorityQueue()
public IndexedPriorityQueue(int capacity)
public IndexedPriorityQueue(IComparer<TPriority>? comparer)
public IndexedPriorityQueue(int capacity, IComparer<TPriority>? comparer)
public IndexedPriorityQueue(IEnumerable<KeyValuePair<TElement, TPriority>> items)
public IndexedPriorityQueue(IEnumerable<KeyValuePair<TElement, TPriority>> items, IComparer<TPriority>? comparer)
```

- The `capacity` overloads pre-size the backing storage to hold at least `capacity` elements before the first growth.
- A `null` `comparer` means `Comparer<TPriority>.Default` (a min-heap). Invert it for a max-heap.
- The `IEnumerable` overloads seed the queue with element/priority pairs; a **duplicate element keeps its last-seen priority** (the seeding is an upsert, matching `EnqueueOrUpdate`).

**Throws:**

- `ArgumentOutOfRangeException` if `capacity < 0`.
- `ArgumentNullException` if `items` is `null` (enumerable overloads).

### Methods and properties

| Member | Description |
|--------|-------------|
| `int Count` | Number of elements currently in the queue. |
| `int Capacity` | Elements the backing storage can hold before it must grow. |
| `IComparer<TPriority> Comparer` | The comparer used to order priorities. |
| `void Enqueue(TElement element, TPriority priority)` | Adds `element`. Throws `ArgumentException` if it is already present. |
| `bool TryEnqueue(TElement element, TPriority priority)` | Adds `element`; returns `false` (queue unchanged) if it is already present. |
| `bool EnqueueOrUpdate(TElement element, TPriority priority)` | Adds `element` if absent (returns `true`) or changes its priority if present (returns `false`). |
| `TElement Peek()` | The minimum-priority element. Throws `InvalidOperationException` if empty. |
| `bool TryPeek(out TElement element, out TPriority priority)` | Non-throwing `Peek`. |
| `TElement Dequeue()` | Removes and returns the minimum-priority element. Throws `InvalidOperationException` if empty. |
| `bool TryDequeue(out TElement element, out TPriority priority)` | Non-throwing `Dequeue`. |
| `bool Contains(TElement element)` | Whether `element` is present. `O(1)`. |
| `TPriority GetPriority(TElement element)` | `element`'s current priority. Throws `KeyNotFoundException` if absent. `O(1)`. |
| `bool TryGetPriority(TElement element, out TPriority priority)` | Non-throwing `GetPriority`. |
| `void Update(TElement element, TPriority priority)` | Changes `element`'s priority (decrease- or increase-key) and restores its position. Throws `KeyNotFoundException` if absent. `O(log n)`. |
| `bool TryUpdate(TElement element, TPriority priority)` | Non-throwing `Update`. |
| `bool Remove(TElement element)` | Removes `element` wherever it sits in the heap. Returns `false` if absent. `O(log n)`. |
| `bool Remove(TElement element, out TPriority priority)` | `Remove` that also returns the removed element's priority. |
| `void Clear()` | Removes all elements. The backing storage is retained. |
| `int EnsureCapacity(int capacity)` | Grows the backing storage to hold at least `capacity` elements; returns the resulting capacity. |
| `void TrimExcess()` | Shrinks the backing storage to fit the current count. |
| `Enumerator GetEnumerator()` | A struct enumerator over the element/priority pairs in **heap order** (not priority order). |

Enumeration yields the pairs in heap-array order, which is **not** priority order. To visit elements by priority, `Dequeue` them (which empties the queue) or copy the pairs out and sort them. A pure read (`Peek`, `Contains`, `TryGetPriority`) does not invalidate an in-flight enumerator; every mutation (`Enqueue`, `Dequeue`, `Update`, `Remove`, `Clear`, and a capacity change that reallocates) does.

### Choosing it

Reach for `IndexedPriorityQueue` when you need a priority queue whose elements' priorities **change while they are queued**, or where you must **remove or look up a specific element** by value — the shortest-path / MST / A\* relaxation loop, an event scheduler that can cancel or reschedule a pending event, or any "best-so-far" frontier. If you only ever `Enqueue` and `Dequeue` and never touch an element already inside, the BCL `PriorityQueue<TElement, TPriority>` is simpler and allows duplicate elements; `IndexedPriorityQueue` trades that for the addressable operations and the one-element-per-key constraint. This type is not thread-safe; concurrent callers must synchronize externally.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

// Dijkstra's shortest paths over a tiny weighted graph, using decrease-key.
var dist = new IndexedPriorityQueue<int, int, Int32WangHasher>();
foreach (int v in new[] { 0, 1, 2, 3, 4 })
    dist.Enqueue(v, v == 0 ? 0 : int.MaxValue); // source at 0, everything else at infinity

// adjacency: node -> (neighbour, weight)
var graph = new Dictionary<int, (int to, int w)[]>
{
    [0] = new[] { (1, 4), (2, 1) },
    [1] = new[] { (3, 1) },
    [2] = new[] { (1, 2), (3, 5) },
    [3] = new[] { (4, 3) },
    [4] = Array.Empty<(int, int)>(),
};

var final = new Dictionary<int, int>();
while (dist.TryDequeue(out int u, out int du))
{
    final[u] = du;
    if (du == int.MaxValue) continue; // unreachable
    foreach (var (to, w) in graph[u])
    {
        // relax the edge: decrease-key if we found a shorter path
        if (dist.TryGetPriority(to, out int old) && du + w < old)
            dist.Update(to, du + w);
    }
}

Console.WriteLine(string.Join(", ", final.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}")));
// 0:0, 1:3, 2:1, 3:4, 4:7
```

## Trie&lt;TValue&gt;

An ordered **prefix tree** (trie) mapping `string` keys to values. Every key is stored as a path of characters from a shared root, so keys sharing a prefix share that prefix's nodes. Implements `IReadOnlyDictionary<string, TValue?>`.

```csharp
public sealed class Trie<TValue> : IReadOnlyDictionary<string, TValue?>
```

The BCL ships no trie. `Dictionary<string, TValue>` answers an exact-key lookup in `O(1)` but has **no efficient prefix operation**: listing every key that starts with a prefix, or finding the longest stored key that is a prefix of a query, both force an `O(n)` scan of the whole dictionary plus a `StartsWith` per key. A trie answers those directly from its structure.

### How it works

Each node holds its child edges in two parallel arrays kept sorted ascending by edge character, so a child lookup is a binary search and a pre-order walk visits children in ordinal order — which is why enumeration is sorted for free. A key terminates at the node reached by walking its characters from the root; the empty string is a valid key (it terminates at the root). Removal prunes bottom-up any node that no longer leads to a key, so the structure never retains dead paths, and the `Count` / `ContainsPrefix` invariants hold.

Keys are compared and ordered by their UTF-16 code units (ordinal) — the same comparison `Dictionary<string, TValue>` uses with the ordinal comparer. Culture-aware comparison is not applied.

### The documented BCL-beating workload

The **prefix operations**:

- `GetByPrefix` / `GetKeysWithPrefix` yield every entry whose key starts with a prefix in `O(prefix length + matches)` — autocomplete, typeahead, listing a namespace or route table — where a `Dictionary` must scan and `StartsWith`-filter every entry.
- `TryGetLongestPrefix` finds the longest stored key that is a prefix of a query in `O(query length)` — routing tables, tokenizer / dictionary matching, filesystem-style longest-match.
- Enumeration yields keys in ascending ordinal order for free, where a `Dictionary` is unordered.

An exact `Add` or `TryGetValue` walks the key character by character rather than hashing it once, so for **pure exact-key** workloads a `Dictionary` is competitive or faster — the trie's value is the prefix and ordering operations, not raw exact-lookup speed. See the [trie benchmark](https://marius-bughiu.github.io/Celerity/dev/bench/?collection=Trie) on the dashboard.

> The complexities above count each character step as `O(1)`. Strictly, navigating one node's children is a binary search, so a character step is `O(log b)` in that node's branching factor `b`; for the common bounded-alphabet case `b` is a small constant and the length-proportional forms hold, while on a pathologically wide alphabet the character-length terms gain a `log b` factor.

### Constructors

```csharp
public Trie()
public Trie(IEnumerable<KeyValuePair<string, TValue>> entries)
```

- The parameterless constructor starts empty.
- The `entries` overload bulk-loads the pairs; a later duplicate key overwrites the value set by an earlier one (indexer semantics).

**Throws:**

- `ArgumentNullException` if `entries` is `null`, or any key in it is `null`.

### Indexer

```csharp
public TValue this[string key] { get; set; }
```

The getter throws `KeyNotFoundException` if `key` is absent (an interior prefix that was never stored counts as absent). The setter adds the key or overwrites its existing value. Both throw `ArgumentNullException` if `key` is `null`.

### Methods and properties

| Member | Description |
|--------|-------------|
| `int Count` | Number of keys. |
| `void Add(string key, TValue value)` | Adds a key. Throws `ArgumentException` if it already exists. |
| `bool TryAdd(string key, TValue value)` | Adds a key, leaving an existing entry unchanged. Returns `false` if already present. |
| `bool ContainsKey(string key)` | Whether `key` is a stored key (an interior-only prefix returns `false`). |
| `bool ContainsKey(ReadOnlySpan<char> key)` | The same, from a character span — no `string` is materialized. See [span-keyed lookups](#span-keyed-lookups). |
| `bool TryGetValue(string key, out TValue? value)` | Non-throwing exact lookup. |
| `bool TryGetValue(ReadOnlySpan<char> key, out TValue? value)` | The same, from a character span. |
| `bool Remove(string key)` | Removes a key, pruning any newly-dead nodes. Returns `false` if absent. |
| `bool Remove(string key, out TValue? value)` | `Remove` returning the removed value (`default` when the key was absent). |
| `void Clear()` | Removes all keys. |
| `bool ContainsPrefix(string prefix)` | Whether any stored key starts with `prefix` (a key equal to the prefix counts). The empty prefix matches iff the trie is non-empty. |
| `bool ContainsPrefix(ReadOnlySpan<char> prefix)` | The same, from a character span. |
| `IEnumerable<KeyValuePair<string, TValue?>> GetByPrefix(string prefix)` | Every entry whose key starts with `prefix`, in ascending key order (lazy). |
| `IEnumerable<string> GetKeysWithPrefix(string prefix)` | The keys of `GetByPrefix`, in ascending order (lazy). |
| `bool TryGetLongestPrefix(string query, out string? key, out TValue? value)` | The longest stored key that is a prefix of `query` (an exact match qualifies and is longest). On a miss (`false`), `key` is `null` and `value` is `default`. |
| `IEnumerable<string> Keys` / `IEnumerable<TValue?> Values` | Keys in ascending order and their aligned values. |
| `Enumerator GetEnumerator()` | An allocation-free struct enumerator over the entries in ascending key order (the traversal lazily allocates a small stack only when the trie has children to walk). |

Every key-taking member throws `ArgumentNullException` on a `null` argument. `Add`, `TryAdd` (when it adds), the setter, `Remove` (when it removes), and `Clear` are structural changes that invalidate an in-flight enumerator (including a `GetByPrefix` stream); a pure lookup does not.

### Empty-string and default handling

The empty string is an ordinary key. The trie stores no `TValue` out-of-band, so any `TValue` — including `default`/`null` — is a valid value. `null` keys are rejected.

### Choosing it

Reach for `Trie<TValue>` when the workload needs **prefix or ordered** access: autocomplete / typeahead, longest-prefix routing, ordered key iteration, or listing everything under a namespace. If you only ever do exact-key `Add` / `TryGetValue` / `Remove`, a `Dictionary<string, TValue>` (or `CelerityDictionary`) is the better fit — the trie earns its place only when you use the prefix operations. It is not thread-safe.

### Usage example

```csharp
using Celerity.Collections;

var routes = new Trie<string>();
routes["/"] = "home";
routes["/api"] = "api-root";
routes["/api/v1/users"] = "users-v1";
routes["/api/v1/orders"] = "orders-v1";

// Autocomplete: every route under a prefix, already in sorted order.
foreach (var (path, handler) in routes.GetByPrefix("/api/v1/"))
    Console.WriteLine($"{path} -> {handler}");        // /api/v1/orders, then /api/v1/users

// Longest-prefix routing: the most specific stored route that prefixes the request.
if (routes.TryGetLongestPrefix("/api/v1/users/42", out string? route, out string? handler))
    Console.WriteLine($"matched {route} -> {handler}"); // matched /api/v1/users -> users-v1
```

## Span-keyed lookups

Every string-keyed Celerity collection can be probed with a `ReadOnlySpan<char>` — a slice of a buffer the caller already holds — without first materializing a `string`.

| Type | Span members |
|------|--------------|
| `FrozenCelerityDictionary<TValue, THasher>` | `TryGetValue(ReadOnlySpan<char>, out TValue?)`, `ContainsKey(ReadOnlySpan<char>)` |
| `FrozenCeleritySet<THasher>` | `Contains(ReadOnlySpan<char>)` |
| `CelerityDictionary<string, TValue, THasher>` | `TryGetValue(ReadOnlySpan<char>, out TValue?)`, `ContainsKey(ReadOnlySpan<char>)` |
| `CeleritySet<string, THasher>` | `Contains(ReadOnlySpan<char>)` |
| `Trie<TValue>` | `TryGetValue(ReadOnlySpan<char>, out TValue?)`, `ContainsKey(ReadOnlySpan<char>)`, `ContainsPrefix(ReadOnlySpan<char>)` |

### Why it matters

Without them, a tokenizer, CSV / log reader, or route dispatcher holding a `ReadOnlySpan<char>` must call `new string(span)` to probe the collection: **one allocation plus a copy per lookup**, on the hot path of exactly the workloads these types exist for. The span overloads delete both. Nothing else changes — stored keys are compared ordinally against the span, which is what `EqualityComparer<string>.Default` does, so a span lookup and the equivalent string lookup always agree.

> **.NET 9+.** `Dictionary<string,V>.GetAlternateLookup<ReadOnlySpan<char>>()` gives the BCL the same capability on .NET 9 and later. These overloads work on **all three** of Celerity's target frameworks, including the `net8.0` floor where the BCL has no answer.

### How they are exposed

On the four hashed collections the span overloads are **extension methods** in `Celerity.Collections` (`SpanLookupExtensions`), because the span probe needs the hasher to implement [`ISpanHashProvider`](hashing.md#ispanhashprovider) as well as `IHashProvider<string>` — and adding that to the class's own constraint would break every existing instantiation. The extra constraint therefore lives on the methods:

```csharp
where THasher : struct, IHashProvider<string>, ISpanHashProvider
```

They bind only when the hasher supplies both, resolve statically (no boxing — the JIT still devirtualizes the hash call through the struct type parameter), and read like instance methods at the call site as long as `Celerity.Collections` is in scope. Every built-in `String*Hasher` implements both. `Trie<TValue>` takes no hasher, so its span overloads are ordinary instance methods.

### The empty span

A span has no `null` state, so an **empty span means the empty string `""`** — an ordinary key — and never the out-of-band `null` key. Look the `null` key up through the `string` overload.

### Usage example

```csharp
using Celerity.Collections;
using Celerity.Hashing;

var routes = new FrozenCelerityDictionary<int, StringXxHash3Hasher>(
[
    new("/api/v1/users",  1),
    new("/api/v1/orders", 2),
]);

ReadOnlySpan<char> line = "GET /api/v1/users HTTP/1.1".AsSpan();
ReadOnlySpan<char> path = line.Slice(4, 14);

if (routes.TryGetValue(path, out int handler))   // no string allocated
    Dispatch(handler);
```

## StringInternTable

A canonicalizing table of strings: probe it with a `ReadOnlySpan<char>` and it returns the one shared `string` for those characters, allocating **only on a miss**.

```csharp
public sealed class StringInternTable : StringInternTable<StringFnV1AFullHasher>

public class StringInternTable<THasher> : IReadOnlyCollection<string>
    where THasher : struct, IHashProvider<string>, ISpanHashProvider
```

### The documented BCL-beating workload

**A 10M-cell CSV or log parse over ~100 distinct tokens.** The intern table allocates **100 strings instead of 10,000,000** — only the miss path materializes one. Downstream reference equality then works, and the GC never sees the other 9,999,900 copies.

This is the one collection you cannot build on the pre-.NET-9 BCL: `HashSet<string>.TryGetValue` takes a `string`, so you must **allocate the string before you can discover you already had it** — the very allocation you were trying to avoid. `string.Intern` is not a substitute either: it is process-wide, never collected for the life of the process, and still requires a `string` to hand it. A `StringInternTable` is an ordinary object — its scope is yours, `Clear` releases everything it holds, and dropping the table drops the interned strings with it.

> **.NET 9+.** `Dictionary<string,V>.GetAlternateLookup<ReadOnlySpan<char>>()` can express the same pattern on .NET 9 and later. This type works on all three of Celerity's target frameworks, including the `net8.0` floor.

### Default hasher

The non-generic `StringInternTable` uses `StringFnV1AFullHasher` — cheap, and **full Unicode width**, so tokens that differ only in a character's high byte do not collide. (This deliberately differs from the frozen collections' `StringFnV1AHasher` default, which folds only the low byte: an intern table's inputs are arbitrary parsed text rather than curated identifiers.) A collision is never a correctness problem in either case — the ordinal span comparison resolves it — but it costs probes. Supply any `String*Hasher` through `StringInternTable<THasher>` for a different speed/quality point.

### Constructors

```csharp
public StringInternTable(int capacity = 16, float loadFactor = 0.75f)
```

**Throws:**

- `ArgumentOutOfRangeException` if `capacity` is negative, or `loadFactor` is not in the open interval (0, 1).

### Methods and properties

| Member | Description |
|--------|-------------|
| `int Count` | Number of distinct strings interned. |
| `string GetOrAdd(ReadOnlySpan<char> key)` | The canonical instance for those characters, allocating one only on a miss. |
| `string GetOrAdd(string key)` | The canonical instance; on a miss the supplied instance itself becomes canonical, so this never allocates a `string` (an insert past the load factor still grows the backing array). Throws `ArgumentNullException` on `null`. |
| `bool TryGet(ReadOnlySpan<char> key, out string? value)` | Pure lookup — never interns. `value` is `null` on a miss. |
| `bool Contains(ReadOnlySpan<char> key)` | Whether those characters are already interned. |
| `bool Contains(string key)` | The same. Throws `ArgumentNullException` on `null`. |
| `void Clear()` | Drops every interned string; the backing capacity is preserved. |
| `Enumerator GetEnumerator()` | An allocation-free struct enumerator over the interned strings. Order is unspecified. |

`GetOrAdd` (when it interns) and `Clear` are structural changes that invalidate an in-flight enumerator; `TryGet` and `Contains` never mutate.

### Empty-string and `null` handling

The empty string is an ordinary entry, and an empty span means `""`. `null` is not storable — the `string` overloads reject it.

### Choosing it

Reach for `StringInternTable` when you are producing **many occurrences of few distinct strings** from a buffer you already hold: parsers, tokenizers, log ingestion, column stores, header dispatch. If you need to associate a *value* with each token, use a `CelerityDictionary<string, TValue, THasher>` with the [span lookups](#span-keyed-lookups) instead. It is not thread-safe.

### Usage example

```csharp
using Celerity.Collections;

var interned = new StringInternTable();

foreach (ReadOnlySpan<char> cell in SplitCells(line))
{
    string token = interned.GetOrAdd(cell);   // allocates only the first time each token is seen
    Consume(token);
}

Console.WriteLine(interned.Count);            // distinct tokens, == strings allocated
```

## FenwickTree&lt;T&gt;

```csharp
public sealed class FenwickTree<T> : IReadOnlyCollection<T>
    where T : struct, INumber<T>
```

A **Fenwick tree** (Binary Indexed Tree) is a fixed-length, array-backed sequence of numeric values that answers **prefix sums** — and therefore arbitrary **range sums** — and applies **point updates** in `O(log n)` each, over a single flat array of `n + 1` elements (index `0` is unused by the 1-based layout), with no per-node object overhead. It is generic over `System.Numerics.INumber<T>`, so it works for `int`, `long`, `uint`, `ulong`, `double`, `decimal`, and any other value type with generic-math addition and subtraction.

The BCL ships nothing for the **interleaved point-update + prefix-sum-query** workload, and a plain `T[]` forces a losing tradeoff: keep the raw values and every prefix / range query is `O(n)` (sum a slice); precompute a running-total array and queries are `O(1)` but every point update is `O(n)` (fix the whole suffix). A Fenwick tree gives **both** in `O(log n)`.

### How it works

Each stored cell holds the partial sum of a contiguous range of the logical sequence whose length is the lowest set bit of its (1-based) index. A prefix query accumulates `O(log n)` cells by repeatedly stripping the lowest set bit (`k -= k & -k`); a point update touches the `O(log n)` cells whose ranges cover the changed index by repeatedly adding it back (`k += k & -k`). A range sum is the difference of two prefix sums. The constructor from a value sequence builds the tree in `O(n)` (one ascending pass that pushes each cell into its parent), not `O(n log n)` point-inserts.

### The documented BCL-beating workload

Any stream that **mixes updates with range-sum queries**: running / rolling aggregates, order-statistics and rank counters (counting inversions, "how many seen values are ≤ x"), cumulative-frequency tables, sliding-window sums over a mutating history, and gradient / weight accumulators. Against a plain array these are `O(n·q)`; against the Fenwick tree they are `O(q·log n)`. See the [Fenwick-tree benchmark](https://marius-bughiu.github.io/Celerity/dev/bench/?collection=FenwickTree) on the dashboard.

### Constructors

```csharp
public FenwickTree(int length)                 // length logical elements, all zero
public FenwickTree(IEnumerable<T> values)      // O(n) build seeded with values, in order
```

`length` must be non-negative and at most `Array.MaxLength - 1` — the 1-based Fenwick layout reserves one array slot (`ArgumentOutOfRangeException` otherwise). The length is **fixed** at construction — the tree does not grow; `Clear` resets the values to zero but keeps the length. The `IEnumerable<T>` overload throws `ArgumentNullException` on a null source and never aliases a caller-supplied array (it copies).

### Methods and properties

| Member | Description |
| --- | --- |
| `int Count { get; }` | The number of logical elements (the fixed length). |
| `T Total { get; }` | The sum of every logical element — `PrefixSum(Count)`. |
| `T this[int index] { get; set; }` | Get/set the logical value at `index`. Both are `O(log n)`; the getter is `RangeSum(index, index + 1)`, the setter applies the delta to reach the new value. Assigning the value already stored is a no-op. |
| `void Add(int index, T delta)` | Add `delta` to the value at `index`, in `O(log n)`. A negative `delta` subtracts (for signed `T`); a zero `delta` is a no-op. |
| `T PrefixSum(int endExclusive)` | Sum of the logical elements in `[0, endExclusive)`, in `O(log n)`. `PrefixSum(0)` is zero; `PrefixSum(Count)` is `Total`. |
| `T RangeSum(int start, int endExclusive)` | Sum of the logical elements in the half-open range `[start, endExclusive)`, in `O(log n)`. An empty range sums to zero. |
| `void Clear()` | Reset every logical element to zero (`O(n)`); the length is unchanged. |
| `Enumerator GetEnumerator()` | Struct enumerator yielding the logical values in index order (`O(n log n)` total). |

Index and range arguments are bounds-checked (`ArgumentOutOfRangeException`): `index` must be in `[0, Count)`, a prefix bound in `[0, Count]`, and a range must satisfy `0 ≤ start ≤ endExclusive ≤ Count`. Reads never mutate, so they never invalidate an enumerator; `Add`, the indexer setter, and `Clear` do — except when they are no-ops (a zero delta, or assigning the value already stored), which leave both the state and any active enumerator untouched. Not thread-safe.

### Choosing it

Reach for `FenwickTree<T>` when you maintain a **mutable numeric sequence** and repeatedly ask for prefix or range sums *while* the values change — running totals, rank / order-statistics counters, cumulative-frequency tables, or windowed aggregates over a history you also edit. If your data is **immutable** after you build it, a one-shot precomputed prefix-sum `T[]` answers queries in `O(1)` with less code; if you **only ever update** and never query a partial sum, a raw array is simpler. The Fenwick tree wins precisely when both happen — updates *and* partial-sum queries interleave. For range **updates** with point queries, apply the tree to the difference array. For a fold with **no inverse** — minimum, maximum, gcd, bitwise and/or — a Fenwick tree cannot answer the query at all, because it computes a range as the *difference* of two prefix sums; reach for [`SegmentTree<T, TMonoid>`](#segmenttreet-tmonoid) instead. This type is not thread-safe; concurrent callers must synchronize externally.

### Usage example

```csharp
using Celerity.Collections;

// Count inversions with a rank counter: how many already-seen values exceed the current one.
int[] data = { 5, 2, 6, 1, 3, 4 };
int maxValue = 6;

var seen = new FenwickTree<int>(maxValue + 1); // one counter slot per possible value
long inversions = 0;
foreach (int x in data)
{
    // values already seen that are strictly greater than x -> an inversion each
    inversions += seen.RangeSum(x + 1, maxValue + 1);
    seen.Add(x, 1); // record that we have now seen x
}

Console.WriteLine(inversions); // 8
```

## SegmentTree&lt;T, TMonoid&gt;

```csharp
public sealed class SegmentTree<T, TMonoid> : IReadOnlyList<T>
    where TMonoid : struct, IMonoid<T>
```

A **segment tree** is a fixed-length, array-backed sequence that answers the aggregate of any half-open range under an arbitrary **associative** operation, and applies point updates, in `O(log n)` each — over a single flat array of `2n` elements with no per-node object overhead.

It is the half of the range-query space [`FenwickTree<T>`](#fenwicktreet) cannot reach. A Fenwick range query is the *difference* of two prefix folds, so the operation must have an inverse — which is why that type is constrained to `INumber<T>` and answers sums only. A segment tree stores each node's fold outright and never subtracts, so range **minimum**, **maximum**, **gcd**, bitwise **and**/**or** — and any fold you write yourself — are all in reach. Where sums are what you want, prefer `FenwickTree<T>`: it does the same job in half the memory.

The BCL has no range-aggregate structure at all, so the baseline is a plain `T[]` and a loop that folds the slice element by element — `O(n)` per query, whatever the fold — while precomputing the answers instead makes every point update `O(n)`. There is not even a span helper to lean on: `Span<T>` has no `Min` or `Max`, let alone an arbitrary combine, so the loop is written out by hand. (The benchmark measures the range-**minimum** instance of it, which is the cheapest per element the baseline gets.)

### The fold: `IMonoid<T>`

```csharp
public interface IMonoid<T>
{
    T Identity { get; }
    T Combine(T left, T right);
}
```

`TMonoid` is a `struct, IMonoid<T>` **type parameter** rather than an interface-typed instance, for the same reason the hashed collections take their hasher that way: the JIT specializes the tree for the concrete struct and inlines `Combine` instead of emitting an interface call, and a query or an update calls it `O(log n)` times.

An implementation must satisfy the two monoid laws, because the tree relies on both to answer a query from precomputed partial folds:

- **Associativity** — `Combine(Combine(a, b), c)` equals `Combine(a, Combine(b, c))`. The tree chooses its own bracketing, so a non-associative operation gives an unspecified answer.
- **Identity** — `Combine(Identity, a)` and `Combine(a, Identity)` both equal `a`. `Identity` is the aggregate of an empty range and the value of every element of a freshly constructed tree.

Both laws are required only over the implementation's **domain** — the set of values it declares itself defined for — not over every bit pattern `T` can hold. An implementation that restricts its domain must say so, because a value outside it produces an unspecified aggregate rather than a thrown exception. Two of the shipped monoids do restrict it: `MinMonoid<T>` and `MaxMonoid<T>` are defined over the *finite* values of a floating-point `T` (see the caveat below). The other three are defined over all of `T`.

**Commutativity is not required.** The query folds the nodes it takes from the left and from the right into two separate accumulators and combines them in index order at the end, so a non-commutative operation (matrix product, "first non-zero wins", string concatenation) gets the same answer a left-to-right scan would.

Five folds ship with the library:

| Monoid | `Identity` | `Combine` | Constraint on `T` |
| --- | --- | --- | --- |
| `SumMonoid<T>` | `T.Zero` | `left + right` | `struct, INumberBase<T>` |
| `MinMonoid<T>` | `T.MaxValue` | the smaller | `struct, INumber<T>, IMinMaxValue<T>` |
| `MaxMonoid<T>` | `T.MinValue` | the larger | `struct, INumber<T>, IMinMaxValue<T>` |
| `BitwiseAndMonoid<T>` | `~T.Zero` (all ones) | `left & right` | `struct, INumberBase<T>, IBitwiseOperators<T, T, T>` |
| `BitwiseOrMonoid<T>` | `T.Zero` | `left \| right` | `struct, INumberBase<T>, IBitwiseOperators<T, T, T>` |

Anything else is a field-free struct you write:

```csharp
public readonly struct GcdMonoid : IMonoid<uint>
{
    public uint Identity => 0;                  // gcd(0, a) == a

    public uint Combine(uint left, uint right)
    {
        while (right != 0)
            (left, right) = (right, left % right);

        return left;
    }
}

var tree = new SegmentTree<uint, GcdMonoid>(values);
```

That example is written over `uint` deliberately. A signed gcd has to normalize its sign, and the obvious `Math.Abs` throws on `int.MinValue` — whose true gcd with `0` is `2147483648`, a value no `int` can hold. Restricting the domain to unsigned values removes the corner rather than papering over it.

**Floating-point caveat on `MinMonoid<T>` / `MaxMonoid<T>`.** The identity is `T.MaxValue` / `T.MinValue`, which for `float` and `double` are the largest and smallest *finite* values, not the infinities. A stored `+∞` therefore aggregates to `T.MaxValue` under `MinMonoid<double>`. And `NaN` loses every `<` comparison, so `Combine(NaN, x)` is `x` while `Combine(x, NaN)` is `NaN` — the aggregate of a range containing a `NaN` depends on where it sits. Both are the ordinary consequences of ordering IEEE values by `<`; if you need IEEE-exact semantics, pass a custom monoid that calls `T.Min` / `T.Max`.

### How it works

The logical element at index `i` lives at `tree[n + i]`, and every internal node `k` in `[1, n)` holds `Combine(tree[2k], tree[2k + 1])` — exactly `2n` cells, with index `0` unused. A point update writes the leaf and refolds each ancestor from its two children. A range query walks outward from both ends, taking each node that is fully inside the range and halving the bounds one level per step.

The usual segment-tree layout pads the leaf count up to a power of two and pays up to `4n` cells. This one does not, and the reason it can get away with `2n` is worth stating: at a length that is not a power of two the leaves sit in a rotated order, so an internal node can span a wrapped, non-contiguous range — but a query that keeps its two directions in separate accumulators never combines such a node into the wrong side. One visible consequence is that `tree[1]` is the whole-sequence fold *only* at power-of-two lengths, which is why `Aggregate` is a query rather than a root read. The claim is pinned by an exhaustive differential sweep over every length and every range under a non-commutative fold, which is the only kind that can observe a violation.

### Range updates are not supported

Applying an operation to every element of a range in `O(log n)` needs lazy propagation, which needs a second monoid describing how updates compose plus a distributive law relating the two. That is a different type with a different contract, not an overload of this one. Update point by point, or apply this tree to a difference sequence.

### The documented BCL-beating workload

Any stream that **mixes point updates with range-aggregate queries under a non-invertible fold**: sliding-window minima and maxima over a mutating history, "cheapest offer in this price band" over a live order book, per-window capability masks (`BitwiseAndMonoid`), and range gcd. Against a plain array these are `O(n·q)`; against the segment tree they are `O(q·log n)`. Measured on the short-run local sweep at 100,000 elements: interleaved update + range-minimum **14.8x** faster than the array scan, and a batch of range-minimum queries against a pre-built tree **81x** faster. At 1,000 elements the margins narrow to **1.4x** and **3.7x** — a scan of a thousand contiguous `long`s is cache-friendly enough that `O(log n)` barely pays for itself. See the [segment-tree benchmark](https://marius-bughiu.github.io/Celerity/dev/bench/?collection=SegmentTree) on the dashboard.

### Constructors

```csharp
public SegmentTree(int length)                              // length elements, all Identity
public SegmentTree(int length, TMonoid monoid)
public SegmentTree(IEnumerable<T> values)                   // O(n) build seeded with values, in order
public SegmentTree(IEnumerable<T> values, TMonoid monoid)
```

`length` must be non-negative and at most `Array.MaxLength / 2` — the layout stores two cells per element (`ArgumentOutOfRangeException` otherwise). The length is **fixed** at construction; the tree does not grow. `Clear` resets the values to the identity but keeps the length. The `IEnumerable<T>` overloads throw `ArgumentNullException` on a null source and never alias a caller-supplied array (they copy). The `monoid`-taking overloads exist for a fold that carries state; a field-free monoid needs neither, since the other two close over `default(TMonoid)`.

### Methods and properties

| Member | Description |
| --- | --- |
| `int Count { get; }` | The number of logical elements (the fixed length). |
| `T Aggregate { get; }` | The fold of every logical element — `Query(0, Count)`, and `Identity` for an empty tree. `O(log n)`, not a root read. |
| `T this[int index] { get; set; }` | Get/set the logical value at `index`. The getter is `O(1)` (a direct leaf read); the setter is `O(log n)` (it refolds the path to the root). |
| `void Combine(int index, T value)` | Fold `value` into the element at `index` — it becomes `Combine(current, value)` — in `O(log n)`. The monoid-native update: it needs no inverse, and the stored value stays on the left. |
| `T Query(int start, int endExclusive)` | The fold of the logical elements in the half-open range `[start, endExclusive)`, in `O(log n)`. An empty range yields `Identity`. |
| `void Clear()` | Reset every logical element to `Identity` (`O(n)`); the length is unchanged. |
| `Enumerator GetEnumerator()` | Struct enumerator yielding the logical values in index order (`O(n)` total — the leaves are stored outright). |

It implements `IReadOnlyList<T>`, not merely `IReadOnlyCollection<T>` as `FenwickTree<T>` does, because the leaves are stored outright: the indexer is a direct array read, so a consumer that indexes in a loop pays what it expects. A Fenwick tree recovers each value from a difference of prefix folds, which would make the same loop `O(n log n)`.

Index and range arguments are bounds-checked (`ArgumentOutOfRangeException`): `index` must be in `[0, Count)`, and a range must satisfy `0 ≤ start ≤ endExclusive ≤ Count`. Reads never mutate, so they never invalidate an enumerator. **Every** mutation bumps the version: unlike `FenwickTree<T>`, an assignment that stores the value already there is not detected as a no-op, because `IMonoid<T>` carries no equality obligation and the tree will not impose one. Not thread-safe.

### Choosing it

Reach for `SegmentTree<T, TMonoid>` when you maintain a **mutable sequence** and repeatedly ask for the aggregate of a range *while* the values change, under a fold with **no inverse**. If the fold is addition, use `FenwickTree<T>` — same asymptotics, half the memory, shorter constant. If the sequence is **immutable** after you build it, a sparse table answers range minima in `O(1)` and a precomputed prefix array answers sums in `O(1)`, both with less code. If you **only ever update** and never query a range, a raw array is simpler. And if you need to update whole ranges at a time, this is not the type — see above. This type is not thread-safe; concurrent callers must synchronize externally.

### Usage example

```csharp
using Celerity.Collections;

// A live order book: the cheapest ask in any price band, while prices keep moving.
long[] asks = { 105, 102, 108, 101, 110, 103, 107, 104 };
var book = new SegmentTree<long, MinMonoid<long>>(asks);

Console.WriteLine(book.Query(0, 4));   // 101 — cheapest in the first band
Console.WriteLine(book.Aggregate);     // 101 — cheapest overall

book[3] = 999;                         // that order was filled and replaced

Console.WriteLine(book.Query(0, 4));   // 102 — refolded in O(log n)
Console.WriteLine(book.Aggregate);     // 102

// A different fold over the same shape: which flags does every entry in the window still set?
var masks = new SegmentTree<int, BitwiseAndMonoid<int>>(new[] { 0b1111, 0b1110, 0b1100, 0b0101 });
Console.WriteLine(Convert.ToString(masks.Query(0, 3), 2));   // 1100
```

## KdTree&lt;TValue&gt;

```csharp
public sealed class KdTree<TValue> : IReadOnlyList<SpatialPoint<TValue>>
```

A **2-D k-d tree**: a build-once, immutable spatial index over points in the plane. It answers *which point is nearest to this one*, *which points lie within this radius* and *which lie inside this box* without measuring every stored point.

.NET ships nothing for this question. There is no k-d tree, no quadtree, no R-tree and no spatial index of any kind in the BCL — `System.Drawing` ships geometry *primitives* with no index over them, and `System.Numerics` ships vectors, not containers. So the idiomatic answer is an array of points and a loop that measures all of them: `O(n)` per query, on every query.

The sibling range structures index one ordered axis and cannot stand in. [`BTreeSet<T, TComparer>`](#btreesett-tcomparer) orders keys and [`SegmentTree<T, TMonoid>`](#segmenttreet-tmonoid) folds values stored at positions, but neither expresses proximity in two dimensions at once, because there is no total order on the plane under which near points are neighbours — sorting by x puts two points a hair apart vertically at opposite ends of the array.

### The element: `SpatialPoint<TValue>`

```csharp
public readonly struct SpatialPoint<TValue>
{
    public SpatialPoint(double x, double y, TValue? value);

    public double X { get; }
    public double Y { get; }
    public TValue? Value { get; }

    public void Deconstruct(out double x, out double y, out TValue? value);
}
```

Coordinates are `double` rather than a generic numeric type. `INumber<T>` is available on the `net8.0` floor and would admit `int` or `float` coordinates, but every query compares *squared* distances, and a squared distance silently overflows an integer type at coordinates a map or a game world reaches easily.

### How it works

The points are permuted into one interleaved coordinate array (`x, y, x, y, …`) and a balanced binary tree is laid over them *implicitly*: the node for the index range `[lo, hi)` sits at its midpoint and its subtree is exactly `[lo, hi)`. Each level splits on the axis the level before it did not — x, then y, then x — and the build puts the median on that axis at the midpoint, so everything left of a node is at or before it on that axis and everything right is at or after.

There are no nodes, no child pointers and no per-point heap object: the whole structure is one coordinate array and one payload array. The coordinates are interleaved (`x, y, x, y, …`) rather than kept in an array per axis, which keeps a node contiguous; that was measured against the split layout and came out inside the run-to-run spread, so it is a simpler invariant rather than a speed win, and the field comment says so. The median is found by **introselect** rather than a full sort, which keeps the build to an *expected* `O(n log n)` rather than the `O(n log² n)` a sort per level would cost — and the depth budget behind the *intro* bounds the **worst** case at that same `O(n log² n)` rather than letting it go quadratic.

That budget earns its keep on an input that is not exotic. A middle-element pivot settles ordered and reverse-ordered data in `log m` passes, but an **organ pipe** — ascending values interleaved with descending ones, which is the shape of any path that goes out and comes back — puts an extreme at the midpoint of every subrange and peels off one element per pass: 4,096 points take 2,048 passes against a budget of 24. After the budget runs out the range is sorted outright and the answer read off it. This mirrors `PartialSort.SelectCore` in `Celerity.Sorting` deliberately, so there is one introselect idea in the library rather than two.

### What the complexity really is

A k-d tree has **no useful worst-case query bound**. An adversarial point set forces every query to visit every node, and even on friendly data the classic `O(log n)` figure for nearest-neighbour is an average over uniformly distributed points, not a guarantee. What *is* guaranteed is that a query visits at most `n` nodes, so each family stays bounded by the hand-written loop it replaces: `O(n)` for the nearest and range queries, whose per-node work is constant, and `O(n log k)` for the k-nearest queries, whose per-candidate work is a sift through the `k`-element heap — the same factor the equivalent hand-rolled bounded-heap scan pays.

The useful statement is empirical, and it is about **selectivity** rather than size: pruning works by discarding subtrees that cannot hold a result, so a query whose answer is a large fraction of the tree prunes little and converges on the scan.

### Two dimensions specifically, not a generic `k`

The dimensionality is a design decision rather than a type parameter. A generic-`k` tree has to take every coordinate as a `ReadOnlySpan<double>` at the API boundary and store an array per point in the layout, which costs exactly the flat-array design this type exists for. The plane is where the .NET workloads above live.

### Constructors

```csharp
public KdTree(IEnumerable<SpatialPoint<TValue>> points)
```

Builds the index. The sequence is read once and copied; when it implements `ICollection<T>` it is sized and copied in one pass.

**Throws:**

- `ArgumentNullException` if `points` is `null`.
- `ArgumentException` if a point has a coordinate that is **not finite**, or that exceeds **`1e153`** in magnitude. A `NaN` coordinate has no position, so it can be neither ordered at build nor measured at query time; an infinite one measures `NaN` against *itself* (`Infinity - Infinity`), so a stored infinite point could not be found even by a query for its own coordinates. The magnitude bound exists because every query compares *squared* distances: past ~`1e153` a squared separation overflows to infinity, at which point two far-apart points compare equal rather than merely losing precision. All three are rejected at build rather than stored as points no query could answer for.

### Methods and properties

| Member | Description |
| --- | --- |
| `Count` | Number of points, counting duplicate coordinates separately. |
| `this[int index]` | The point at that position in the tree's **layout order** — deterministic for a given input, but neither insertion nor spatial order, and an implementation detail. |
| `TryFindNearest(x, y, out point)` | The closest point by Euclidean distance. `false` only for an empty tree or a `NaN` query coordinate. Allocation-free. |
| `TryFindNearest(x, y, maxDistance, out point)` | The closest point no further than `maxDistance` away. The bound **seeds the search's pruning radius**, so a tight bound is materially cheaper than an unbounded query followed by a distance test. |
| `CopyNearest(x, y, destination, destinationIndex = 0)` | Fills the buffer's remaining room with that many nearest points, in ascending distance. Allocation-free. |
| `GetNearest(x, y, count)` | The `count` nearest points in ascending distance. Allocates. |
| `ContainsWithin(x, y, radius)` | Whether any point lies within the (inclusive) radius. Stops at the first match. |
| `CountWithin(x, y, radius)` | How many points lie within the radius. |
| `CopyWithin(x, y, radius, destination, destinationIndex = 0)` | Writes the matches into a caller buffer. Allocation-free. |
| `GetWithin(x, y, radius)` | The matches as an array. Allocates. |
| `ContainsInRectangle(minX, minY, maxX, maxY)` | Whether any point lies inside the **closed** box. Stops at the first match. |
| `CountInRectangle(...)` / `CopyInRectangle(...)` / `GetInRectangle(...)` | The same three tiers for a box query. |
| `GetEnumerator()` | Struct enumerator over every stored point in layout order. |

**Throws:**

- `ArgumentOutOfRangeException` if a radius or distance bound is negative or `NaN`, if `count` is negative, or if `destinationIndex` is outside `[0, destination.Length]`.
- `ArgumentException` if a box's upper edge precedes its lower edge.
- `ArgumentNullException` if a destination buffer is `null`.

### Boundaries, ties and `NaN`

- **Radius and distance bounds are inclusive.** A point exactly `r` away is within radius `r`, and `CountWithin(x, y, 0)` counts the points exactly at `(x, y)`.
- **The box is closed** on all four edges, so a degenerate box with `minX == maxX` matches the points on that line.
- **Ties are unspecified.** Which of several equidistant points a nearest query returns, and the relative order of equidistant results, are not part of the contract. The *distances* are.
- **A `NaN` query coordinate matches nothing**: the nearest queries return `false` and the range queries return empty. Stored coordinates can never be `NaN`, infinite, or past the magnitude bound. *Query* coordinates are not range-checked — that would be a per-query cost for a case no real coordinate system reaches — so a query beyond the same magnitude yields distances the type cannot order.
- **The domain has a floor as well as a ceiling**, documented rather than enforced because no build-time check could see it: a separation below roughly `1e-162` squares below the smallest subnormal and underflows to zero, so two points that close are indistinguishable from coincident ones. Points that genuinely coincide are unaffected. Comparing squared distances is what keeps every query off the square root; paying a scaled comparison on the hot path to resolve separations no coordinate system produces is not a trade this type makes.
- **Duplicate coordinates stay distinct.** Two points at the same position are two entries and every query reports both.
- **Match order is unspecified** for the radius and box queries; only `GetNearest` and `CopyNearest` order their results, by ascending distance.

### Allocation-free and convenience tiers

`TryFindNearest`, the `Contains` and `Count` members and the three `Copy` methods allocate nothing at all. `GetNearest`, `GetWithin` and `GetInRectangle` are the convenience tier and allocate the result array; the two range ones walk the tree twice, once to size the array exactly and once to fill it.

The k-nearest copy tier is allocation-free by a small trick worth knowing about: the caller's buffer *is* the search's bounded max-heap, and each candidate's distance is recomputed from its own stored coordinates rather than parked in a parallel array — five floating-point operations against the cache miss that array would cost. The results are heapsorted in place at the end, which is where the ascending order comes from.

All queries share two traversals — one for the range queries, generic over both a `struct` region and a `struct` visitor, and one for the nearest queries — so the JIT specializes each per call site and inlines the per-match work rather than paying a delegate or an interface call per hit.

### Build-once

The tree is immutable; adding a point means building a new one, as with `FrozenCelerityDictionary<TValue>` and [`RankSelectBitVector`](#rankselectbitvector). Keeping a k-d tree balanced under insertion needs periodic subtree rebuilds, which is a different type with a different cost profile, not an overload of this one.

Because nothing mutates, enumeration is never invalidated and concurrent readers need no synchronization — and unlike the comparer-parameterized trees there is no caveat to attach to that, since every query is arithmetic on `double` and calls nothing the caller supplied.

### Choosing it

Reach for `KdTree<TValue>` when you build a set of points once and then query it for proximity **repeatedly** — nearest store, driver or sensor to a coordinate; viewport and map-tile culling; collision broadphase; the neighbour queries inside k-means and DBSCAN; snap-to-nearest in an editor; duplicate-coordinate detection.

Do not reach for it when the points **change constantly** — it is build-once, and rebuilding per frame costs more than the queries save — or when you have only a **few thousand points**, where the measurements below put a hand-rolled sorted scan roughly level with it. If your queries only ever discriminate on **one** axis, an ordered array and a binary search is the smaller tool.

### The documented BCL-beating workload

Against the array-and-a-loop the BCL leaves you with, at 100,000 uniformly scattered points and 1,000 queries, the nearest-neighbour query is **348x** faster and the radius query **53x** — measured on CI's same-runner A/B; the full table, and the important caveat beside it, are in the README's [spatial index section](../../README.md#spatial-index).

The caveat is that a **hand-rolled** partial index does much better than the naive scan: order the points by x, binary-search to the query's x and work outward, abandoning each direction once the horizontal gap alone exceeds the best distance so far. That is a real optimization and effectively a one-dimensional spatial index, and against it the tree's margin is **2.5x** on the nearest query and **3.5x** on the radius one rather than two orders of magnitude — falling to 1.4x and to nothing at all at 1,000 points. Both baselines are measured, and the second is the one to judge the type by.

### Usage example

```csharp
using Celerity.Collections;

// A depot network, indexed once and queried per request.
var depots = new KdTree<string>(new[]
{
    new SpatialPoint<string>(51.51, -0.13, "London"),
    new SpatialPoint<string>(53.48, -2.24, "Manchester"),
    new SpatialPoint<string>(55.95, -3.19, "Edinburgh"),
    new SpatialPoint<string>(52.49, -1.89, "Birmingham"),
});

// Which depot serves this address?
if (depots.TryFindNearest(52.20, -2.00, out SpatialPoint<string> nearest))
    Console.WriteLine(nearest.Value);                  // Birmingham

// The three closest, nearest first — for a fallback list.
foreach (SpatialPoint<string> depot in depots.GetNearest(52.20, -2.00, 3))
    Console.WriteLine(depot.Value);                    // Birmingham, Manchester, London

// Everything inside a delivery radius, counted without allocating.
Console.WriteLine(depots.CountWithin(53.00, -2.00, 1.5));   // 2 — Manchester and Birmingham

// Everything inside the current map viewport, into a buffer you own.
var visible = new SpatialPoint<string>[4];
int shown = depots.CopyInRectangle(51.0, -3.0, 54.0, 0.0, visible);
Console.WriteLine(shown);                              // 3

// A tight bound is not just a filter — it prunes the search.
Console.WriteLine(depots.TryFindNearest(0, 0, maxDistance: 1, out _));   // False
```

## SpatialGrid&lt;TValue&gt;

```csharp
public sealed class SpatialGrid<TValue> : IReadOnlyCollection<SpatialPoint<TValue>>
```

A **mutable uniform-cell spatial index** over points in the plane: constant-time `Move` and `Remove`, amortized constant-time `Add`, and radius, rectangle and nearest queries that touch only the cells the query covers. It is the counterpart to [`KdTree<TValue>`](#kdtreetvalue) for points that **move**, and it shares that type's element, [`SpatialPoint<TValue>`](#the-element-spatialpointtvalue).

`KdTree` is build-once and its own documentation says where that leaves you: *do not reach for it when the points change constantly — it is build-once, and rebuilding per frame costs more than the queries save*. That is a hole the docs point at and nothing filled, and it is the **commonest** spatial workload rather than an edge case — game entities and projectiles, drivers and couriers on a map, cursors and drag targets, particles in a simulation, agents in a model. All of them move every tick, and all of them ask *what is near me* every tick. .NET has nothing here either.

### The baseline is not a strawman

What a caller writes instead is a bucketed grid:

```csharp
var cells = new Dictionary<(int, int), List<Entity>>();
```

Insert by `(x / cellSize, y / cellSize)`, query by walking the cells the radius covers. That is a genuinely reasonable structure — the same cell idea, the same query shape — and it is the baseline this type is measured against, not the linear scan. What it costs is a tuple hash and a bucket probe per cell touched, a `List<T>` object per occupied cell, and a pointer chase into it. One honest qualification the benchmark insists on: in steady state neither side allocates, because a cell that empties keeps its list, so the difference is resident memory and per-cell work rather than garbage.

### How it works

A populated grid is three arrays and no per-cell or per-entry object. One holds the cells' list heads; one holds fixed-size entry records — coordinates, owning cell, and the two links that thread the entry through its cell's **intrusive doubly-linked list**; and one holds the payloads, kept separate precisely so the cell walk never touches them and a query reads coordinates and links without dragging `TValue` through the cache.

The double links are what make `Remove` and a cell-crossing `Move` unlink in constant time instead of scanning the cell to find the predecessor. A vacated slot goes on a free list and is reused by the next `Add`.

The world rectangle and the cell size are **declared up front**, which buys a dense cell array and index arithmetic in place of hashing a cell key. A point outside the declared world is *clamped* into the nearest edge cell rather than rejected: the clamp is monotone, and a query's own cell range is clamped the same way, so every query stays exactly correct — but a population that drifts outside piles onto the edge cells and degrades to a scan of them.

### Handles

```csharp
public readonly struct SpatialGridHandle : IEquatable<SpatialGridHandle>
```

`Add` returns a handle, and `Move` / `Remove` / `TryGetPoint` take one. That is what makes `Move` a coordinate write plus — only when the point crossed a cell boundary — an unlink and a relink, rather than a search: there is no hash of the coordinates and no comparison of `TValue`, so the type needs no equality contract on its payload and holds duplicates happily. It is the same idea that makes [`IndexedPriorityQueue`](#indexedpriorityqueuetelement-tpriority-thasher)'s decrease-key addressable.

A handle stays valid, addressing the same entry, for as long as that entry is in the grid — through any number of moves, and through other entries being added and removed around it. `Remove` and `Clear` **retire** it: each slot carries a version that is stepped when the slot is vacated, so a stale handle is rejected rather than silently addressing whatever reused the storage. The `default` handle refers to nothing and is always rejected.

A handle belongs to the grid that issued it. Passing one to a *different* `SpatialGrid<TValue>` is a programming error the type cannot detect, because the versions are per-grid — keep handles with their grid, as you would an index into an array.

### The two failure modes

A uniform grid is the right structure for **evenly spread** moving objects and the wrong one for heavily clustered ones. Both are stated on the type rather than left to be discovered:

1. **Non-uniform density.** If most of the population lands in one cell, every query in that neighbourhood degenerates to a scan of that cell.
2. **A query radius much larger than the cell.** The number of cells a query touches grows with the square of `radius / cellSize`, so a wide query over a fine grid is worse than a scan.

The first is worse than "it degrades to a scan", and it is measured rather than rounded to a caveat: on clustered data this type is **about twice as slow as the hand-roll it replaces**. Inside a cell the two layouts genuinely differ — the hand-roll walks a contiguous `List<T>` and issues independent loads per candidate, which the processor overlaps, while this type walks an intrusive linked list, so each step is a load that must complete before the next address is known. A cell holding two entries never shows that; a cell holding five hundred is a five-hundred-long chain of dependent cache misses.

**And there is no consolation elsewhere in this library**, which was worth measuring rather than assuming. An earlier draft of this page sent clustered points to [`KdTree<TValue>`](#kdtreetvalue), reasoning that pruning adapts to density where a fixed cell size cannot. The measurement does not support it: on clustered *moving* points a per-frame `KdTree` rebuild is **level with this type** (24.3 ms against 24.0 ms), both about twice the hand-roll's 12.5 ms — and `KdTreeShapeBenchmark` separately finds the tree losing to *its* own hand-roll under clustering. So the honest advice for heavily clustered points that move is the unflattering one: the bucketed `Dictionary` of contiguous lists wins, and neither type here helps.

`cellSize` is the knob that trades these against each other, and it is a constructor parameter for that reason: roughly one that puts a handful of points in the average cell, and no smaller than the typical query radius.

### What the complexity really is

`Move`, `Remove` and `TryGetPoint` are `O(1)` outright. `Add` is `O(1)` **amortized**: the one call in a growth cycle that finds the free list empty and the entry array full resizes and copies both backing arrays, which is `O(n)` for that call. A range query is `O(cells touched + points in them)` — a statement about density rather than about `Count`. That is asymptotic and nothing more: the clustered measurement above is the reminder that a matching bound does not mean matching time, since the intrusive list adds a dependent load per candidate that a contiguous bucket does not.

The nearest query expands square rings outward from the query's cell and stops once the next ring's own distance floor cannot beat what it has, so on a populated grid it settles in a handful of rings. On a **sparse** one it can walk out to the world's edge, which is `O(cells)`; pass the `maxDistance` overload when there is a distance beyond which the answer does not interest you, since the bound caps the ring expansion rather than merely filtering the result.

### Constructors

```csharp
public SpatialGrid(double minX, double minY, double maxX, double maxY, double cellSize, int capacity = 0)
```

Creates an empty grid over the world rectangle `[minX, maxX] × [minY, maxY]`, divided into square cells of side `cellSize`. `capacity` pre-sizes the entry storage; it grows as needed. A degenerate world — a point, or a line — is a legal one-cell or one-row grid rather than an error.

**Throws:**

- `ArgumentException` if an upper edge precedes its lower edge.
- `ArgumentOutOfRangeException` if an edge is not finite or exceeds `1e153` in magnitude, if `cellSize` is not a positive finite number, if `capacity` is negative, or if the world and cell size together call for more cells than an array can hold.

### Methods and properties

| Member | Description |
| --- | --- |
| `Count` | Number of points currently in the grid. |
| `CellSize` / `Columns` / `Rows` | The cell side, and how many cells span the world's width and height. Always at least one of each. |
| `MinX` / `MinY` / `MaxX` / `MaxY` | The declared world rectangle. |
| `Add(x, y, value)` | Adds a point and returns its handle. `O(1)` amortized — the call that grows the entry array is `O(n)`. |
| `Move(handle, x, y)` | Relocates the entry. `O(1)`: two coordinate writes, plus an unlink and relink only if the cell changed. |
| `Remove(handle)` | Removes the entry and retires the handle. `O(1)`. |
| `TryGetPoint(handle, out point)` | Reads the point back, or `false` when the handle is not live. Also the way to test a handle without risking an exception. |
| `Clear()` | Removes every entry and retires every handle. Storage is kept and returned to the free list. |
| `ContainsWithin(x, y, radius)` | Whether any point lies within the (inclusive) radius. Stops at the first match. |
| `CountWithin(x, y, radius)` | How many points lie within the radius. |
| `CopyWithin(x, y, radius, destination, destinationIndex = 0)` | Writes the matches into a caller buffer. Allocation-free. |
| `GetWithin(x, y, radius)` | The matches as an array. Allocates. |
| `ContainsInRectangle(minX, minY, maxX, maxY)` | Whether any point lies inside the **closed** box. Stops at the first match. |
| `CountInRectangle(...)` / `CopyInRectangle(...)` / `GetInRectangle(...)` | The same three tiers for a box query. |
| `TryFindNearest(x, y, out point)` | The closest point by Euclidean distance. `false` only for an empty grid or a `NaN` query coordinate. |
| `TryFindNearest(x, y, maxDistance, out point)` | The closest point no further than `maxDistance` away. The bound **caps the ring expansion**, so it is materially cheaper than an unbounded query followed by a distance test. |
| `GetEnumerator()` | Struct enumerator over every live entry, in an unspecified order. |

**Throws:**

- `ArgumentException` if a handle does not address a live entry of this grid, or if a box's upper edge precedes its lower edge.
- `ArgumentOutOfRangeException` if a coordinate passed to `Add` or `Move` is not finite or exceeds `1e153` in magnitude, if a radius or distance bound is negative or `NaN`, or if `destinationIndex` is outside `[0, destination.Length]`. **Query** coordinates are not range-checked — see below.
- `ArgumentNullException` if a destination buffer is `null`.

### Boundaries, ties and `NaN`

- **Radius, distance bounds and the box are inclusive**, matching `KdTree`: a point exactly `r` away is within radius `r`, and a degenerate box matches the points on its line.
- **Stored coordinates must be finite and at most `1e153` in magnitude** — the same domain `SpatialPoint<TValue>` documents, and for the same reason: every query compares *squared* distances, and past that bound a squared separation overflows and two far-apart points stop comparing as far apart. `Add` and `Move` reject anything outside it.
- **The domain has a floor as well as a ceiling**, documented rather than enforced because no check could see it, and identical to [`KdTree<TValue>`](#kdtreetvalue)'s: a separation below roughly `1e-162` squares below the smallest subnormal and underflows to zero, so two points that close are indistinguishable from coincident ones *by the distance test*, and a nearest query may order them arbitrarily. Whether a query even **reaches** the other point is a second question with no promise attached — the cell range comes from the same coordinates, so two points astride a cell boundary at that separation are in different cells and a zero-radius query visits only one. Below this scale the type says nothing either way. Points that genuinely coincide are unaffected.
- **Query coordinates are *not* range-checked**, exactly as on [`KdTree<TValue>`](#kdtreetvalue) — that would be a per-query cost for a case no real coordinate system reaches. One beyond the same magnitude does not throw; it yields distances the type cannot order, so the answer is meaningless rather than merely imprecise.
- **A `NaN` query coordinate matches nothing**: the nearest queries return `false` and the range queries return empty. That is the one query coordinate that *is* special-cased.
- **Ties are unspecified.** Which of several equidistant points a nearest query returns is not part of the contract; the *distance* is.
- **Duplicate coordinates stay distinct.** Two points at the same position are two entries with two handles, and every query reports both.
- **Match order is unspecified** for the radius and box queries.

### Mutation and enumeration

Enumeration yields every live entry in slot order — deterministic for a given sequence of operations, but neither insertion nor spatial order, and an implementation detail. It is invalidated by `Add`, `Remove` and a `Clear` that removes something.

`Move` deliberately does **not** invalidate it. Moving neither adds nor removes an entry nor changes which slot holds it, so the sequence an enumerator is walking is unaffected — an entry not yet reached is simply reported at its new position. That is the family's own rule (an operation that changes nothing about the sequence must not invalidate) applied to the operation this type exists for.

### Capacity only grows, and k-nearest is absent

There is no `TrimExcess`: a handle *is* a position in the entry array, so compacting it would invalidate every handle a caller is holding, which is the one thing this type promises not to do. `Clear` retires all outstanding handles and returns the storage to the free list for reuse, but keeps it.

There is no k-nearest query either. Bounding a ring search by the *k*-th best rather than the best turns it into a heap walk whose ring bound is much weaker, and the workload this type exists for — one proximity query per moving entity per frame — is a radius query. Use `CopyWithin`, or `KdTree<TValue>` when the point set is static enough to build.

### Choosing it

Reach for `SpatialGrid<TValue>` when the points **move** and you query them as often as you move them, and when they are spread reasonably evenly over a world whose extent you can declare.

Reach for [`KdTree<TValue>`](#kdtreetvalue) instead when the point set is **static** enough to build once, when you need **k-nearest** rather than a radius, or when there is no natural world rectangle to declare. Do **not** reach for it merely because your points are clustered and moving — measured, it is level with this type there and both lose to the hand-roll.

### The documented BCL-beating workload

The unit is a **frame**: move 10% of the population, then run one radius query per moved entity. `SpatialGridBenchmark` measures that against the `Dictionary<(int, int), List<int>>` hand-roll above and against rebuilding a `KdTree` each frame, at 1,000 and 100,000 entities over a 10,000-unit square with a 30-unit cell and a 25-unit query radius — the cell sized just over the radius, which is the tuning rule the type documents. At 100,000 entities the frame measures **5.0x** the hand-roll and **13.3x** the per-frame `KdTree` rebuild, on CI's same-runner A/B. Note how little room that leaves: the issue's hard bar was ≥5x, and 9.83 ms / 1.96 ms is 5.02x — cleared by 0.4%, against a combined run spread of about 1.5%. Met, but marginally.

**The margin is a property of how full the cells are, not of the type**, and the README carries that next to the headline rather than underneath it. Both structures walk the same cells and run the same distance test on the same candidates; everything gained here is *per cell*, so the ratio is per-cell overhead against per-candidate work and it thins as the cells fill. The figure above is the broadphase shape — about one point per cell, about two matches per query. At ten points per cell and twenty-five matches, with the cell size still tuned to the radius, it is **1.12x**; on clustered points it is **0.52x** and the hand-roll wins. `SpatialGridShapeBenchmark` in the extended suite carries all three, plus a per-frame `KdTree` rebuild for each. Full tables are in the README's [spatial index section](../../README.md#spatial-index).

### Usage example

```csharp
using Celerity.Collections;

// A world 10,000 units square, in 100-unit cells — roughly the query radius, which is the tuning rule.
var world = new SpatialGrid<string>(0, 0, 10_000, 10_000, cellSize: 100, capacity: 4);

SpatialGridHandle courier = world.Add(120, 340, "courier-7");
SpatialGridHandle rider   = world.Add(180, 300, "rider-3");
world.Add(9_000, 9_000, "depot");

// Every tick: move what moved. No search, no rehash — the handle addresses the entry directly.
world.Move(courier, 150, 320);

// ...then ask what is near it, without allocating.
Console.WriteLine(world.CountWithin(150, 320, 90));          // 2 — the courier itself and the rider

var nearby = new SpatialPoint<string>[8];
int found = world.CopyWithin(150, 320, 90, nearby);
Console.WriteLine(found);                                     // 2

// Nearest, with a bound that caps the search rather than just filtering it.
if (world.TryFindNearest(150, 320, maxDistance: 200, out SpatialPoint<string> closest))
    Console.WriteLine(closest.Value);                         // courier-7

Console.WriteLine(world.TryFindNearest(5_000, 5_000, maxDistance: 100, out _));   // False

// Everything in the current viewport.
Console.WriteLine(world.CountInRectangle(0, 0, 1_000, 1_000)); // 2

// Removing retires the handle: it is rejected afterwards rather than addressing a recycled slot.
world.Remove(rider);
Console.WriteLine(world.TryGetPoint(rider, out _));           // False
```

## RTree&lt;TValue&gt;

```csharp
public sealed class RTree<TValue> : IReadOnlyList<SpatialBox<TValue>>
```

An **R-tree**: a build-once, immutable spatial index over axis-aligned *rectangles*. It answers *which of these boxes overlap this box* and *which contain this point* without testing every stored box.

.NET ships nothing for this question either. There is no R-tree, no bounding-volume hierarchy and no spatial index of any kind in the BCL — `System.Drawing` ships a `Rectangle` with an `IntersectsWith` on it and no index over a *collection* of them. So the idiomatic answer is an array of boxes and a loop that tests all of them: `O(n)` per query, on every query.

[`KdTree<TValue>`](#kdtreetvalue) cannot stand in. A point index answers *what is near this coordinate*; it has nothing to say about an object that **occupies an area**, because a box can overlap the query while its centre sits far outside it — a long thin road segment crossing the viewport is the ordinary case, not a contrived one. That is the two-dimensional form of exactly the argument that made [`IntervalTree`](#intervaltreetkey-tvalue-tcomparer) necessary next to `BTreeSet` on one axis.

### The element: `SpatialBox<TValue>`

```csharp
public readonly struct SpatialBox<TValue>
{
    public SpatialBox(double minX, double minY, double maxX, double maxY, TValue? value);

    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }
    public TValue? Value { get; }

    public void Deconstruct(out double minX, out double minY, out double maxX, out double maxY, out TValue? value);
}
```

The edges are **closed**: a box covers `[MinX, MaxX] × [MinY, MaxY]`, so two boxes sharing only an edge or a corner do overlap and a point exactly on an edge is inside. A box may be degenerate — equal edges make it a point, equal edges on one axis make it a segment — which is deliberate, since that is how a point is filed alongside extents in the same index.

Coordinates are `double`, matching `SpatialPoint<TValue>` so the two spatial element types agree at the boundary. Unlike the point, nothing here is ever squared — every query is a comparison — so there is **no magnitude bound**; only finiteness and non-inverted edges are required.

### How it works

The boxes are permuted by **STR (Sort-Tile-Recursive) packing** and a fixed-fanout tree (16 children) is laid over them *implicitly*: leaf `i` owns entries `[i×16, i×16+16)`, and a node at any level above owns the same fixed run of the level below it. There are no per-node heap objects and no child pointers — the whole structure is one flat array of entry extents, one payload array, and one flat array of node bounding boxes with the leaf level first and the root last.

STR is what makes the implicit layout legitimate. Sorting a level by centre-x, cutting it into `√(node count)` vertical slices and sorting each slice by centre-y puts spatially near boxes at adjacent indices, so a run of consecutive entries has a **tight** bounding box rather than an arbitrary one. The `√` is what leaves the tiles roughly square, which is what keeps a node's box tight in *both* axes instead of one.

The packing is applied **recursively from the root down** rather than once per level, which is what keeps the tree implicit: the tiling at the root partitions the entries into the subtree-sized runs its children own, and each run is then tiled again for the level below. A sort per level makes the build `O(n log n)` times the level count — five sorts at 100,000 boxes — which is the price of the index and what a caller amortizes by querying it repeatedly.

The sort is `Array.Sort` over a `(double[] keys, SpatialBox<TValue>[] items)` pair rather than an `IComparer<SpatialBox<TValue>>`, which would box on the way in and pay an interface call per comparison on the one path whose whole cost *is* comparisons. Centres are computed as `0.5·min + 0.5·max` rather than `(min + max) / 2`, which cannot overflow for any pair of finite doubles.

### What the complexity really is

An R-tree has **no useful worst-case query bound**. Overlapping node boxes mean a query can descend into several children at each level, and an adversarial box set forces it into all of them. What *is* guaranteed is that a query visits each node and each entry at most once, so every family is `O(n)` — bounded by the hand-written loop it replaces rather than by anything worse.

The useful statement is empirical and about **selectivity**: pruning discards subtrees whose bounding box misses the query, so a query whose answer is a large fraction of the tree prunes little and converges on the scan.

### The shape of the data — measured, and not what the received wisdom says

The standard advice is that an R-tree earns its keep only when **extents vary by orders of magnitude** — a few huge boxes among many small ones, which is what real map and scene data looks like — and that for boxes of roughly **uniform** size a bucketed uniform-cell grid is the simpler and faster answer, because then a cell size exists that fits them all.

`RTreeShapeBenchmark` measures that rather than repeating it, with a bucketed grid as its own arm, and **it does not hold on query cost**. At 100,000 boxes and 1,000 queries:

| Extents | vs sorted hand-roll | vs bucketed grid |
| --- | --- | --- |
| Varying (three orders of magnitude) | 10.0x | 1.30x |
| Uniform | **13.4x** | **3.01x** |

Both shapes hold the same **mean extent** (the log-uniform range's arithmetic mean, 72.3), so they carry the same expected box area and the same grid cell size, and the control varies only the spread. An earlier version used the *geometric* mean instead and was confounded: those boxes had 21x less area, which shrank the grid's cells and made it walk 48 cells per query instead of 2.3. The conclusion survived the correction and strengthened — but it was not established until the control stopped moving two things at once.

The R-tree's margin is *widest* on the shape it was expected to give way on. An R-tree's node boxes get **tighter** as the extents get more alike, so the same query settles in a shorter descent — the tree goes from 1.71 ms to 0.74 ms across the two shapes. The grid barely moves (2.21 ms to 2.24 ms), because with the mean extent held equal its cell size is the same on both and its query cost is dominated by the cells a query covers rather than the boxes in them.

The honest qualification is that a grid's cell size is a tuning knob, and this one is sized by the data (twice the mean extent, the standard heuristic) rather than by the query; a grid tuned to a known query size would close some of that gap. **What is not supported is the flat claim that uniform extents belong to the grid.** The real reason to reach for a grid is that it is *mutable* and this type is not.

The same benchmark also settles the **packing** choice rather than assuming it: against ordering the boxes along a Hilbert curve and cutting the result into runs — the standard alternative, on a common harness where only the permutation differs — sort-tile is 1.04x ahead on varying extents and 1.21x ahead on uniform ones.

### Constructors

```csharp
public RTree(IEnumerable<SpatialBox<TValue>> boxes)
```

Builds the index. The sequence is read once and copied; when it implements `ICollection<T>` it is sized and copied in one pass.

**Throws:**

- `ArgumentNullException` if `boxes` is `null`.
- `ArgumentException` if a box has a coordinate that is **not finite**, or an **upper edge that precedes its lower edge**. A `NaN` edge fails every comparison, so such a box could not be found even by a query for its own extent; an infinite one would overlap every query while giving the packing a centre it cannot order. An inverted box describes no region at all.

### Methods and properties

| Member | Description |
| --- | --- |
| `Count` | Number of boxes, counting duplicate extents separately. |
| `this[int index]` | The box at that position in the tree's **packed order** — deterministic for a given input, but neither insertion nor spatial order, and an implementation detail. |
| `TryGetBounds(out minX, out minY, out maxX, out maxY)` | The bounding box of every stored box, read straight off the root, so it costs nothing. `false` for an empty tree. The cheapest way to reject a query that cannot match anything. |
| `ContainsOverlapping(minX, minY, maxX, maxY)` | Whether any stored box overlaps the **closed** query box. Stops at the first match. |
| `CountOverlapping(...)` | How many stored boxes overlap the query. |
| `CopyOverlapping(..., destination, destinationIndex = 0)` | Writes the matches into a caller buffer. Allocation-free. |
| `GetOverlapping(...)` | The matches as an array. Allocates. |
| `ContainsAtPoint(x, y)` | Whether any stored box contains the point, edges included. Stops at the first match. |
| `CountAtPoint(x, y)` / `CopyAtPoint(...)` / `GetAtPoint(x, y)` | The same three tiers for a point query. |
| `GetEnumerator()` | Struct enumerator over every stored box in packed order. |

**Throws:**

- `ArgumentException` if a query box's upper edge precedes its lower edge.
- `ArgumentOutOfRangeException` if `destinationIndex` is outside `[0, destination.Length]`, or an indexer index is outside `[0, Count)`.
- `ArgumentNullException` if a destination buffer is `null`.

### Boundaries, degenerate boxes and `NaN`

- **Edges are closed** on all four sides, so boxes touching along an edge or at a corner do overlap, and a point exactly on an edge is inside. That is also what makes a point query an overlap query against a degenerate box rather than one that matches nothing.
- **Degenerate boxes are legal.** Equal edges on both axes store a point; equal edges on one axis store a segment. Both are indexed and queried like any other extent.
- **A `NaN` query coordinate matches nothing** — it fails every comparison, so the query prunes the root and reports nothing rather than throwing. Stored coordinates can never be `NaN` or infinite.
- **Duplicate extents stay distinct.** Two boxes with the same edges are two entries and every query reports both.
- **Match order is unspecified** for every query. There is no distance ordering here to fall back on, as there is for a nearest query.
- **Writing stops when the buffer is full**, so a `Copy*` return value equal to the remaining room may mean the matches were truncated. Size the buffer with the matching `Count*` when every match is needed.

### Allocation-free and convenience tiers

The `Contains` and `Count` members and the two `Copy` methods allocate nothing at all. `GetOverlapping` and `GetAtPoint` are the convenience tier and allocate the result array, walking the tree twice — once to size the array exactly and once to fill it.

All queries share **one** traversal, generic over a `struct` visitor, so the JIT specializes it per call site and inlines the per-match work rather than paying a delegate or an interface call per hit. There is one query *region* rather than two, because a point query is an overlap query against a degenerate box and splitting them would buy nothing.

### Build-once

The tree is immutable; adding a box means building a new one, as with [`KdTree<TValue>`](#kdtreetvalue), `FrozenCelerityDictionary<TValue>` and [`RankSelectBitVector`](#rankselectbitvector). Keeping an R-tree balanced under insertion needs node splits and reinsertion, which is a different type with a different cost profile, not an overload of this one.

Because nothing mutates, enumeration is never invalidated and concurrent readers need no synchronization, with no comparer caveat to attach to that: every query is a comparison on `double` and calls nothing the caller supplied.

### Choosing it

Reach for `RTree<TValue>` when you build a set of **sized** objects once and then query it **repeatedly** — collision broadphase for bodies with an extent rather than particles; map label and marker placement; hit-testing a UI or a canvas; viewport culling of sized objects; spatial joins between two box sets.

Do not reach for it when the extents **change every frame** — it is build-once, and rebuilding per frame costs more than the queries save, which is the case a mutable bucketed grid exists for. Do not reach for it for **points** — that is [`KdTree<TValue>`](#kdtreetvalue), which additionally answers nearest-neighbour, a question an extent index does not. And do not reach for it for **a few thousand boxes**, where the measurements below put a hand-rolled sorted scan nearly level with it.

Note that *uniform* extents are **not** a reason to avoid it, despite the received wisdom — see the section above, where that claim is measured and does not survive.

### The documented BCL-beating workload

At 100,000 boxes whose extents span three orders of magnitude, 1,000 queries per measurement:

| Query | Selectivity | vs naive scan | vs sorted hand-roll |
| --- | --- | --- | --- |
| Overlap (`CountOverlapping`) | 0.0835% (83.5 matches) | **141x** | **9.6x** |
| Point (`CountAtPoint`) | 0.0050% (5.04 matches) | **240x** | **10.6x** |

All figures are from CI's same-runner A/B on `ubuntu-latest` rather than a development machine, which matters here: the two disagreed on the *sign* of the 1,000-box point comparison below.

**The two rows are not like-for-like.** The query box is tuned so the overlap query lands on the ~0.1% the kill criterion names; a point query has no such knob, because its answer size is fixed by the extents alone. On this distribution it is twenty times more selective, which flatters its ratio — a more selective query prunes more. Raising it to 0.1% would need boxes that blanket the map, and would drag the overlap arm far above 0.1% in the process, so the difference is inherent rather than an oversight. `RTreeBenchmark` fails its own run if either figure drifts out of band.

The second column is the honest one, and it was the bar set **before** implementation: the hand-roll orders the boxes by `minX`, binary-searches to `query.minX` less the widest stored box, and scans forward while `minX` stays at or below `query.maxX`. That is effectively a one-dimensional R-tree, and the second dimension plus the extent hierarchy are the whole of what this type adds over it.

Building the index costs **~144x** what merely copying the box array costs (436 µs → 62.6 ms), which is what the queries above amortize. A sort per level is what "Sort-Tile-Recursive" costs, and the multiple is much worse on CI than on a development machine because the hosted runner's array copy is quicker and its sorts slower.

**The small-`n` crossover is real, and on the point query it goes negative.** At 1,000 boxes the margin against that hand-roll falls to **1.48x** on the overlap query, and the point query **loses outright at 0.91x** — the scan is ahead. The index has not paid for its indirection at that size, and a slab scan over a thousand boxes is a handful of cache lines. And the ratios track **selectivity**: a query ten times wider narrows the gap considerably, because a query answering with much of the tree prunes little.

Figures are measured in `RTreeBenchmark`; the README's [extent index section](../../README.md#extent-index) carries the same table.

### Usage example

```csharp
using Celerity.Collections;

// Map features, indexed once and queried per frame. Note the extents: a country-sized box
// alongside street-sized ones is exactly the shape this type is for.
var features = new RTree<string>(new[]
{
    new SpatialBox<string>(-8.6, 49.9, 1.8, 60.9, "United Kingdom"),
    new SpatialBox<string>(-0.51, 51.28, 0.33, 51.69, "Greater London"),
    new SpatialBox<string>(-0.14, 51.50, -0.12, 51.51, "Covent Garden"),
    new SpatialBox<string>(-3.20, 55.94, -3.18, 55.96, "Edinburgh Old Town"),
});

// Which features cover this coordinate? Every enclosing extent, whatever its size.
foreach (SpatialBox<string> feature in features.GetAtPoint(-0.13, 51.51))
    Console.WriteLine(feature.Value);        // United Kingdom, Greater London, Covent Garden

// Which features are visible in the current viewport, counted without allocating.
Console.WriteLine(features.CountOverlapping(-1.0, 51.0, 0.5, 52.0));   // 3

// Hit-testing: stop at the first match rather than collecting them all.
Console.WriteLine(features.ContainsAtPoint(-3.19, 55.95));             // True

// Into a buffer you own, on a hot path.
var visible = new SpatialBox<string>[8];
int shown = features.CopyOverlapping(-4.0, 55.0, 2.0, 61.0, visible);
Console.WriteLine(shown);                              // 2 — the UK and Edinburgh Old Town

// The root's extent, free — the cheapest way to reject a query outright.
features.TryGetBounds(out double minX, out double minY, out double maxX, out double maxY);
Console.WriteLine($"{minX}, {minY} .. {maxX}, {maxY}");                // -8.6, 49.9 .. 1.8, 60.9
```

## IntervalTree&lt;TKey, TValue, TComparer&gt;

```csharp
public class IntervalTree<TKey, TValue, TComparer> : IReadOnlyList<Interval<TKey, TValue>>
    where TComparer : struct, IComparer<TKey>

public sealed class IntervalTree<TKey, TValue> : IntervalTree<TKey, TValue, DefaultComparer<TKey>>
```

An **interval tree** is a build-once, immutable index over half-open `[start, end)` ranges that answers *which ranges cover this point* and *which ranges overlap this window* in time that tracks the matches found rather than the intervals stored — `O(log n + k)` when the matches cluster and `O(min(n, (k + 1) log n))` when they are scattered, where `k` is the number of matches. Both assume no stored *empty* intervals; with those the worst case is `O(n)`, for the reason given below.

.NET ships nothing for this question — there is no interval tree, no interval map, and no range-overlap query anywhere in `System.Collections`, on any of `net8.0` / `net9.0` / `net10.0`. The idiomatic answer is a `List<T>` of ranges and a linear scan, which is `O(n)` per query. Sorting that list by start helps less than it looks, and the benchmark measures both: a sorted scan can stop once a start passes the query, roughly halving the work on a uniformly distributed query, but it cannot skip the *front* — an interval that begins far to the left can still cover the query point, so there is no lower bound to seek to and the scan stays linear. That is the gap this type fills — booking- and scheduling-conflict checks, IP-range and CIDR-to-owner lookup, effective-dated pricing and feature-flag windows, "which trace spans were live at time `t`", and the genomics overlap query the structure is named for.

The sibling range structures index the *position* axis and cannot answer it. [`FenwickTree<T>`](#fenwicktreet) and [`SegmentTree<T, TMonoid>`](#segmenttreet-tmonoid) fold values stored **at** positions; neither can enumerate the intervals that stab one.

### The entry type: `Interval<TKey, TValue>`

```csharp
public readonly struct Interval<TKey, TValue>
{
    public Interval(TKey start, TKey end, TValue? value);

    public TKey Start { get; }        // inclusive
    public TKey End { get; }          // exclusive
    public TValue? Value { get; }

    public void Deconstruct(out TKey start, out TKey end, out TValue? value);
}
```

Intervals are **half-open**, matching `SegmentTree`'s range convention and the BCL's own start/length slicing. Two of them overlap when each starts strictly before the other ends, so adjacent ranges such as `[0, 10)` and `[10, 20)` tile a line without reporting a conflict at the seam. An interval whose endpoints are equal is **empty**: it covers no point, so no query ever reports it — but it is still stored and still appears in the `IReadOnlyList<T>` surface, so your input is never silently discarded. An interval whose end *precedes* its start is rejected at construction with `ArgumentException`.

### How it works

The intervals are sorted by start into flat parallel arrays, and a balanced binary search tree is laid over that array **implicitly**: the node for the index range `[lo, hi)` sits at its midpoint, and its subtree is exactly `[lo, hi)`. There are no nodes, no child pointers and no per-interval heap object — the whole structure is four arrays. Each node additionally carries the **maximum end over its subtree**, and that augmentation is what turns the scan into a search: a subtree whose maximum end is at or before the query start cannot hold a match and is skipped whole, as is one whose starts are all at or after the query end.

Every query shape shares a single traversal that is generic over a `struct` visitor, so the JIT specializes it per call site and inlines the per-match work rather than paying a delegate or an interface call per hit — the same zero-cost-abstraction rule the struct hashers, `IMonoid<T>` and `DefaultComparer<T>` follow.

**What the bound really is.** The augmentation proves only that a subtree *contains* a candidate, not where it sits, so `k` matches spread across the tree can force up to `k` separate root-to-match descents: the bound is `O(min(n, (k + 1) log n))` — the `+ 1` because a query that matches nothing still descends — rather than the `O(log n + k)` a *centered* interval tree with per-node sorted endpoint lists would guarantee. The clustered case is the common one here because entries are stored in start order, so overlapping ranges are neighbours and their descents share almost the whole path. A query never does more work than the full scan the baseline pays on every query regardless.

**One input defeats the pruning outright, and it is why `O(n)` is the unconditional worst case: stored empty intervals.** An empty `[x, x)` raises its subtree's maximum end exactly as a real interval would, but is then rejected by the per-node emptiness test *after* the walk has already descended to it — so it can never be pruned in bulk, only discarded one node at a time. A tree of nothing but empty intervals is therefore `O(n)` per query with `k = 0` matches. Neutralizing that inside the tree would need a second per-node array marking the subtrees that hold no real interval, which is a per-query cost on every caller to make a degenerate input asymptotically nicer, so it is documented rather than paid for. If your data carries many zero-length entries and you do not need them back out of the `IReadOnlyList<T>` surface, filter them before building — they can never match anything. Measured by CI's same-runner A/B at 100,000 intervals: **129x** on the point query, **75x** on the window query and **19x** on the first-overlap conflict check. Against the *better* baseline — the start-sorted list that stops at the upper bound, benchmarked as the `SortedScan` arm — the point query is **19x** (7x at 1,000), and that is the number to judge the type by. A local control on a shape with roughly 1,250 matches per point instead of ~70 falls to **8.3x** against the unsorted scan. This type is for *selective* interval sets.

### API

Queries come in two tiers. The **allocation-free** tier allocates nothing at all; the **convenience** tier allocates the result array and walks the tree twice, once to size it exactly and once to fill it.

| Member | Behaviour |
| --- | --- |
| `IntervalTree(IEnumerable<Interval<TKey, TValue>> intervals)` | Build over a sequence, read once and copied. `ArgumentNullException` on `null`, `ArgumentException` when an interval's end precedes its start. |
| `IntervalTree(IEnumerable<Interval<TKey, TValue>> intervals, TComparer comparer)` | The same, with an explicit comparer — pass this only when the comparer carries state. |
| `int Count { get; }` | Number of stored intervals, counting duplicates and empty ranges. |
| `Interval<TKey, TValue> this[int index] { get; }` | The interval at that position in ascending start order. |
| `bool ContainsPoint(TKey point)` | Does any interval cover `point`? Stops at the first match. |
| `bool Overlaps(TKey start, TKey end)` | Does any interval overlap `[start, end)`? Stops at the first match — the member to use for a conflict check. |
| `int CountContaining(TKey point)` / `CountOverlapping(TKey start, TKey end)` | How many match. |
| `int CopyContaining(TKey point, Interval<TKey, TValue>[] destination, int destinationIndex = 0)` | Write the matches into a caller-owned buffer; returns how many were written. |
| `int CopyOverlapping(TKey start, TKey end, Interval<TKey, TValue>[] destination, int destinationIndex = 0)` | The window form of the same. |
| `Interval<TKey, TValue>[] GetContaining(TKey point)` / `GetOverlapping(TKey start, TKey end)` | The convenience tier: allocates and returns an exactly-sized array. |
| `Enumerator GetEnumerator()` | Struct enumerator over every stored interval in ascending start order. |

Both `Copy` methods stop when the buffer fills, so a return value equal to the remaining room may mean the matches were truncated; size the buffer with the matching `Count` method when every match is needed. The window members throw `ArgumentException` when `end` precedes `start`; an **empty** window (`start` equal to `end`) is well defined and matches nothing.

Intervals are kept distinct: two overlapping ranges stay two entries and a query reports both. This is **not a coalescing interval map**, and duplicates are preserved. Two entries with the same start and end have an unspecified relative order.

### Caveats

- **Build-once.** The tree is immutable; adding an interval means building a new one, as with [`FrozenCelerityDictionary<TValue>`](#frozenceleritydictionarytvalue), `XorFilter<T, THasher>` and [`RankSelectBitVector`](#rankselectbitvector). Keeping the augmentation correct under insertion needs a rebalancing tree with a fix-up per rotation — a different type with a different cost profile, not an overload of this one.
- **Nothing mutates, so nothing can be invalidated.** An enumerator survives any concurrent query, and concurrent *readers* need no synchronization as far as the tree is concerned — with one caveat: every query calls `TComparer`, so a comparer that is not itself thread-safe makes concurrent queries unsafe however immutable the tree is. `DefaultComparer<T>` is stateless, so the default case is safe; a stateful comparer, which the two-argument constructor exists to accept, is yours to reason about.
- **The comparer defines everything.** `TComparer` orders the endpoints, decides which intervals are empty, and therefore decides what overlaps what. Use the two-parameter `IntervalTree<TKey, TValue>` alias for the natural order.
- **Build costs a sort.** Construction is `O(n log n)` and the sort is the one comparer call in the type that is not devirtualized. It is paid once; every query after it runs on the specialized path.

### Usage example

```csharp
using Celerity.Collections;

// A room's bookings for the day. Build once; query many.
DateTime day = DateTime.Today;
var bookings = new IntervalTree<DateTime, string>(new[]
{
    new Interval<DateTime, string>(day.AddHours(9),  day.AddHours(10), "standup"),
    new Interval<DateTime, string>(day.AddHours(11), day.AddHours(12), "1:1"),
    new Interval<DateTime, string>(day.AddHours(13), day.AddHours(15), "design review"),
});

// The conflict check: stops at the first overlap, allocates nothing.
bool free = !bookings.Overlaps(day.AddHours(10), day.AddHours(11));   // true — the seam does not conflict

// Who is in the room at 14:00?
foreach (Interval<DateTime, string> meeting in bookings.GetContaining(day.AddHours(14)))
    Console.WriteLine(meeting.Value);                                 // design review

// The allocation-free form of the same query, into a buffer you own.
var matches = new Interval<DateTime, string>[8];
int found = bookings.CopyOverlapping(day.AddHours(9), day.AddHours(12), matches);
Console.WriteLine(found);                                             // 2
```

## BTreeDictionary&lt;TKey, TValue, TComparer&gt;

```csharp
public class BTreeDictionary<TKey, TValue, TComparer>
    : IDictionary<TKey, TValue?>, IReadOnlyDictionary<TKey, TValue?>
    where TComparer : struct, IComparer<TKey>

// Ordered by Comparer<TKey>.Default:
public class BTreeDictionary<TKey, TValue> : BTreeDictionary<TKey, TValue, DefaultComparer<TKey>>
```

A **sorted dictionary backed by a B-tree**. Keys are kept in ascending `TComparer` order across nodes that hold up to **31 keys each in flat arrays**, so a lookup visits `log₃₂(n)` nodes instead of chasing `log₂(n)` pointers, and it adds the ordered surface a hash table cannot answer: `Min`, `Max`, `TryGetLowerBound`, `TryGetUpperBound`, `EnumerateRange`, and in-order enumeration.

### When to choose it over `SortedDictionary` / `SortedList`

The BCL has no B-tree. `SortedDictionary<TKey, TValue>` is a red-black tree: one heap object per entry and roughly `log₂(n)` dependent pointer chases — about 20 potential cache misses at `n = 1M` — for a single lookup. `SortedList<TKey, TValue>` is array-backed, so lookups are a clean binary search but every insert in the middle memmoves the tail (`O(n)`). `OrderedDictionary<TKey, TValue>` (.NET 9) is *insertion*-ordered and does not close the gap at all.

With a fan-out of 32, the same million entries sit about **4 node visits** deep, the keys inside a node are one or two cache lines the prefetcher handles well, and allocation drops from one object per entry to one node per 31 entries (a leaf holds a key and a value array; an internal node adds a child array). The documented BCL-beating workload is a large ordered map under an **interleaved insert + lookup + in-order range-scan** load — time-series keyed by timestamp, order books, LSM-style memtables. See the [BTreeDictionary benchmark](https://marius-bughiu.github.io/Celerity/dev/bench/?collection=BTreeDictionary) on the dashboard.

Where it does **not** win: small maps (at a thousand entries the red-black tree is competitive, and a `SortedList` of a few dozen entries is hard to beat); a **delete-dominated** load, where rebalancing by borrow/merge measures a few percent behind `SortedDictionary`'s rotations; and any workload that never needs order — a hash table answers those in `O(1)`, so reach for `CelerityDictionary` instead.

Measured on the short-run local sweep at 100k entries (see the dashboard for the tracked CI numbers): mixed insert + lookup + range-scan **0.59x** the time of `SortedDictionary`, bulk add **0.74x**, lookup **0.84x**, remove **1.12x** (the loss above), and **0.36x** the allocation. `BTreeSet` against `SortedSet` at the same size: mixed **0.79x**, add **0.72x**, contains **0.91x**, `GetViewBetween`-equivalent range scan **0.43x**, remove **1.06x**, allocation **0.24x**.

### Why the struct comparer?

`TComparer` is a `struct, IComparer<TKey>` type parameter rather than an `IComparer<TKey>` instance for the same reason the hashers are struct type parameters: an interface-typed comparer costs a virtual call for **every key inspected inside a node** — several per node visit, on the hottest path in the tree. `DefaultComparer<T>` wraps `Comparer<T>.Default`, and the two-parameter `BTreeDictionary<TKey, TValue>` alias closes over it so the common case needs no extra type argument. To order by something else, write your own struct comparer and pass it as the type argument.

### Constructors

```csharp
public BTreeDictionary()
public BTreeDictionary(TComparer comparer)
public BTreeDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source)
public BTreeDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source, TComparer comparer)
```

There is no capacity or load-factor parameter — a B-tree grows one node at a time, and an empty dictionary owns no node arrays at all. The `comparer` overloads exist for a **stateful** `TComparer` (a culture, a sort direction, a key selector); without one the ordering is `default(TComparer)`. The `IEnumerable` overloads throw `ArgumentNullException` on a null source and `ArgumentException` on a duplicate key, and never alias a caller-supplied collection.

### Methods and properties

| Member | Description |
| --- | --- |
| `int Count { get; }` | The number of entries. |
| `TComparer Comparer { get; }` | The comparer defining the key order. |
| `TValue this[TKey key] { get; set; }` | Get throws `KeyNotFoundException` when absent; set inserts or overwrites. Both `O(log n)`. An in-place overwrite is not a structural change and does not invalidate active enumerators. |
| `KeyCollection Keys { get; }` / `ValueCollection Values { get; }` | Allocation-free views in ascending key order. Read-only `ICollection<T>`s: the mutating members throw `NotSupportedException`. |
| `void Add(TKey key, TValue? value)` | Insert; throws `ArgumentException` when the key is already present. |
| `bool TryAdd(TKey key, TValue? value)` | Non-throwing insert. A rejected duplicate is a true no-op — it does not restructure the tree or invalidate enumerators. |
| `bool TryGetValue(TKey key, out TValue? value)` / `bool ContainsKey(TKey key)` | `O(log n)` lookup. |
| `bool ContainsValue(TValue? value)` | `O(n)` scan — the tree is indexed by key, not by value. |
| `bool Remove(TKey key)` / `bool Remove(TKey key, out TValue? value)` | `O(log n)` removal, rebalancing by borrowing from a sibling or merging two nodes. |
| `void Clear()` | Drop every entry; the tree releases all of its nodes. |
| `KeyValuePair<TKey, TValue?> Min { get; }` / `Max { get; }` | First / last entry in key order, `O(log n)`. Throws `InvalidOperationException` when empty. |
| `bool TryGetMin(out KeyValuePair<TKey, TValue?> entry)` / `TryGetMax(...)` | The non-throwing forms. |
| `bool TryGetLowerBound(TKey key, out KeyValuePair<TKey, TValue?> entry)` | First entry with a key **≥** `key` (`lower_bound`); an exact match is its own lower bound. |
| `bool TryGetUpperBound(TKey key, out KeyValuePair<TKey, TValue?> entry)` | First entry with a key **>** `key` (`upper_bound`). |
| `RangeEnumerable EnumerateRange(TKey fromInclusive, TKey toExclusive)` | The entries of the half-open range `[fromInclusive, toExclusive)`, ascending, in `O(log n + k)`. Throws `ArgumentException` when the bounds are inverted. |
| `Enumerator GetEnumerator()` | Struct enumerator over the entries in ascending key order. The traversal path is held in an inline buffer, so a `foreach` allocates nothing. |
| `void CopyTo(KeyValuePair<TKey, TValue?>[] array, int arrayIndex)` | Copy every entry in key order. |

Unlike `SortedDictionary`, a **`null` key is legal**: `Comparer<TKey>.Default` orders `null` before every non-`null` key (a custom `TComparer` that rejects `null` overrides that). There is no out-of-band `default(TKey)` slot as in the hash-based family — a value-type `default(TKey)` is an ordinary key that sorts wherever the comparer puts it, so `0` follows every negative `int` rather than coming first. Adding, removing, and clearing invalidate active enumerators (`InvalidOperationException` from `MoveNext`); lookups, a rejected duplicate `TryAdd`, a `Remove` of an absent key, and an in-place value overwrite do not. Not thread-safe.

### Usage example

```csharp
using Celerity.Collections;

// A time-series keyed by timestamp: append as samples arrive, then scan a window in order.
var series = new BTreeDictionary<long, double>();
foreach ((long timestamp, double value) in ReadSamples())
{
    series[timestamp] = value;
}

// Every sample in [start, end) — O(log n) to seek, then a walk over contiguous node arrays.
double sum = 0;
int count = 0;
foreach (KeyValuePair<long, double> sample in series.EnumerateRange(start, end))
{
    sum += sample.Value;
    count++;
}

Console.WriteLine($"window mean: {sum / count}");

// The ordered questions a hash table cannot answer.
Console.WriteLine(series.Min.Key);                       // earliest timestamp
Console.WriteLine(series.Max.Key);                       // latest timestamp
series.TryGetLowerBound(start, out var firstAtOrAfter);  // first sample at or after `start`
```

## BTreeSet&lt;T, TComparer&gt;

```csharp
public class BTreeSet<T, TComparer> : ISet<T>, IReadOnlySet<T>
    where TComparer : struct, IComparer<T>

// Ordered by Comparer<T>.Default:
public class BTreeSet<T> : BTreeSet<T, DefaultComparer<T>>
```

The set counterpart of `BTreeDictionary`: elements kept in ascending `TComparer` order, up to **31 per node** in flat arrays, with the same ordered surface — `Min`, `Max`, `TryGetLowerBound`, `TryGetUpperBound`, `EnumerateRange`, and in-order enumeration.

### When to choose it over `SortedSet`

`SortedSet<T>` is a red-black tree with one heap object per element and roughly `log₂(n)` dependent pointer chases per lookup; this type reaches the same element in about `log₃₂(n)` node visits — around 4 instead of 20 potential cache misses at `n = 1M`. Because it stores no values, a node is a single element array plus its children, so the memory saving over `SortedSet` is larger still. The documented BCL-beating workload is a large ordered set under an **interleaved insert + membership + in-order range-scan** load — sorted id sets, sweep-line event sets, interval endpoints. See the [BTreeSet benchmark](https://marius-bughiu.github.io/Celerity/dev/bench/?collection=BTreeSet) on the dashboard.

Where it does **not** win: small sets (at a thousand elements `SortedSet` is competitive), a delete-dominated load (a few percent behind its rotations), and any workload that never needs order — `CeleritySet` or `IntSet` answer those in `O(1)`.

### Constructors

```csharp
public BTreeSet()
public BTreeSet(TComparer comparer)
public BTreeSet(IEnumerable<T> source)
public BTreeSet(IEnumerable<T> source, TComparer comparer)
```

As with the dictionary, there is no capacity or load factor. The `IEnumerable` overloads throw `ArgumentNullException` on a null source and silently ignore duplicates.

### Methods and properties

| Member | Description |
| --- | --- |
| `int Count { get; }` / `TComparer Comparer { get; }` | Element count; the comparer defining the order. |
| `void Add(T item)` | Insert; throws `ArgumentException` when the element is already present (the family-wide set convention). |
| `bool TryAdd(T item)` | Non-throwing insert. `ISet<T>.Add` and `ICollection<T>.Add` both map to this. |
| `bool Contains(T item)` / `bool Remove(T item)` | `O(log n)`. |
| `void Clear()` | Drop every element; the tree releases all of its nodes. |
| `T Min { get; }` / `T Max { get; }` | Smallest / largest element, `O(log n)`. Throws `InvalidOperationException` when empty. |
| `bool TryGetMin(out T item)` / `TryGetMax(out T item)` | The non-throwing forms. |
| `bool TryGetLowerBound(T item, out T bound)` / `TryGetUpperBound(T item, out T bound)` | Smallest element **≥** / **>** `item`, `O(log n)`. |
| `RangeEnumerable EnumerateRange(T fromInclusive, T toExclusive)` | The elements of the half-open range, ascending, in `O(log n + k)`. Throws `ArgumentException` when the bounds are inverted. |
| `UnionWith` / `IntersectWith` / `ExceptWith` / `SymmetricExceptWith` | In-place `ISet<T>` algebra, with `HashSet<T>` semantics. |
| `IsSubsetOf` / `IsProperSubsetOf` / `IsSupersetOf` / `IsProperSupersetOf` / `Overlaps` / `SetEquals` | The `ISet<T>` / `IReadOnlySet<T>` queries. |
| `void CopyTo(T[] array, int arrayIndex)` | Copy every element in ascending order. |
| `Enumerator GetEnumerator()` | Allocation-free struct enumerator in ascending order. |

Membership is defined by `TComparer` — two elements are the same element when the comparer orders them equal. The set-algebra members materialize the right-hand side into a `HashSet<T>`, so they compare *that* side with `EqualityComparer<T>.Default` (matching the rest of the family). A custom comparer that treats two values as equal when `EqualityComparer<T>.Default` does not — a case-insensitive order, say — can therefore disagree with `SortedSet<T>` on those members alone. A `null` element is legal and `Comparer<T>.Default` orders it before every non-`null` one; a value-type `default(T)` is just an ordinary element, sorted wherever the comparer puts it. Not thread-safe.

### Usage example

```csharp
using Celerity.Collections;

// A sweep line over interval endpoints: insert as events arrive, query the neighbourhood in order.
var active = new BTreeSet<int>();
foreach (int endpoint in endpoints)
{
    active.TryAdd(endpoint);
}

// The nearest active endpoint at or after `x`, and the next one strictly after it.
if (active.TryGetLowerBound(x, out int atOrAfter))
{
    Console.WriteLine(atOrAfter);
}

if (active.TryGetUpperBound(x, out int strictlyAfter))
{
    Console.WriteLine(strictlyAfter);
}

// Everything in the window, in order, without scanning the whole set.
foreach (int endpoint in active.EnumerateRange(windowStart, windowEnd))
{
    Process(endpoint);
}
```

## CompressedGraph

```csharp
public sealed class CompressedGraph : IReadOnlyList<GraphEdge>
```

A **compressed sparse row (CSR) graph**: a build-once, immutable directed graph over dense vertex ids `[0, VertexCount)`, stored as two flat `int[]` so a vertex's neighbours are a `ReadOnlySpan<int>` slice of one contiguous array rather than a heap object of their own.

.NET ships nothing for this. There is no graph, no adjacency list, no adjacency matrix and no traversal anywhere in `System.Collections`, on any of `net8.0` / `net9.0` / `net10.0`, so the idiom that fills the vacuum is `Dictionary<int, List<int>>` — or, once a developer notices the ids are dense, `List<int>[]`. Both are benchmarked. What they pay is a hash lookup per vertex visited (the dictionary form only), one `List<int>` object plus its backing array per vertex, and an indirection through both on the one loop a traversal actually runs.

The two shipped types that sit next to graphs do not replace this and are not replaced by it. [`SparseSet`](#sparseset) is the **visited set** a traversal needs — its own documentation says so — which is bookkeeping, not the graph. [`DisjointSet<T>`](#disjointsett) answers *are these two connected* after a sequence of unions, which is a connectivity oracle: it cannot enumerate a vertex's neighbours, and it is undirected by construction.

### The entry type: `GraphEdge`

```csharp
public readonly struct GraphEdge : IEquatable<GraphEdge>
{
    public GraphEdge(int source, int target);

    public int Source { get; }
    public int Target { get; }

    public void Deconstruct(out int source, out int target);
}
```

Both endpoints are dense vertex ids. The edge is **directed**, so `(1, 2)` and `(2, 1)` are different edges and compare unequal. The struct carries no payload deliberately: a graph on dense ids lets you keep per-**vertex** data in a parallel array of your own indexed by the same id, which is why `CompressedGraph` is not generic. Per-**edge** data is a different matter and this does not give it to you — the build sorts each vertex's targets and collapses duplicates, so an array parallel to the *input* edge order no longer lines up with anything the graph exposes.

### How it works

`_offsets` holds `VertexCount + 1` entries, where vertex `v`'s targets occupy `_targets[_offsets[v].._offsets[v + 1]]`; the trailing entry is the edge count, which removes the bounds special case from every slice. Building is a counting scatter — count each vertex's out-degree, prefix-sum it into starting positions, scatter the targets — then one sort per vertex, so `O(V + E log d)` for maximum out-degree `d`. The sort is what makes each target slice ascending, which is what makes `ContainsEdge` a binary search rather than a scan.

Duplicate edges **collapse** during that pass: this is an edge *set*, which is what makes `Degree` a count of distinct neighbours. A self-loop `(v, v)` is a legal edge and is preserved. An **undirected** graph is built by supplying each edge in both directions; there is no factory for it, because silently doubling your input is worse than one documented line.

Both traversals use the caller's destination buffer as the traversal queue, so the only other state is a bitmap or an in-degree array rented from `ArrayPool<T>` and returned. Once the pool is warm a repeated traversal allocates nothing — `ArrayPool<T>.Shared` allocates when it has no suitable buffer, so a first or contended call can still allocate.

### Measured

At 100,000 vertices and 800,000 distinct edges (average degree 8, a random DAG), against the two hand-rolls:

The figures below are **ranges across two CI same-runner A/B runs of identical code**, not single measurements. That is deliberate, and the spread is the point: the allocation-heavy arms move by up to 20% between runs while the pure-compute ones are stable to about 1%. A single ratio quoted to three digits here would imply a precision this benchmark does not have — an earlier draft of this section did exactly that, twice, from local runs that were wrong by up to 35%.

| Arm | vs `Dictionary<int, List<int>>` | vs `List<int>[]` | vs `int[][]` exact-sized, vertex order |
| --- | --- | --- | --- |
| Neighbour iteration | — | **1.36x** | — |
| Breadth-first traversal | 2.5–2.6x | 1.7–1.8x | **1.15–1.28x** — and a **loss** at 1k vertices |
| Topological order | — | **1.95x** | — (no tight arm; expect the same shrinkage) |
| Transpose | — | 8.6–13.6x | **2.5–3.0x** |
| Build | 3.1–3.8x | — | **1.16–1.29x** — and a **loss** at 1k vertices |
| Retained heap | 3.60x less | 2.98x less | **1.83x less** |

The `int[][]` column is held to the same standard as the type it bounds: it reuses the count array as the scatter cursor rather than allocating a second one (as `CompressedGraph` reuses its own offsets), and it shares `Array.Empty<int>()` for empty rows rather than allocating an object per isolated vertex (`CompressedGraph` has no per-row object at all). Both corrections came out of review and both moved the numbers against this type.

The retained-heap row is a settled-GC measurement rather than a CI benchmark, and is the one row here that is a single figure rather than a range.

**Every ratio above is a statement about one baseline, and the three sets disagree by a lot.** That is the single most important thing to take from them. The same breadth-first walk over the same edge set is about 2.5x, 1.8x or **1.2x** depending only on how the neighbour lists were stored — so a number quoted without its baseline means nothing. The step from `List<int>[]` to `int[][]` is the mechanism: a `List<int>` grows its backing array on demand, in the order the edges happen to arrive, so the neighbour data ends up scattered however tidily the list objects were created. Size the arrays exactly, fill them in vertex order, and a caller has recovered almost everything flattening them into one array would give.

**So the honest case for this type is narrow, and it is not speed.** Against a competent `int[][]` hand-roll the traversal and the build are both within about 1.2x at a hundred thousand vertices — near enough a wash — and both are *losses* at a thousand, with the build allocating about 1.5x more while it runs. What genuinely survives is:

- the **transpose, 2.5–3.0x** — the one operation the flat layout does structurally better, because the jagged form has to allocate a fresh row per vertex where CSR scatters into two arrays it already has;
- the **footprint, 1.83x smaller** — one `int[]` header instead of 100,000 of them;
- and the fact that the breadth-first order, Kahn's algorithm, the transpose, the deduplication and the sorted-target invariant are code you do not write, test or maintain, on a structure the BCL does not ship at all.

If those are not what you want, an `int[][]` you fill yourself is a perfectly good answer and this page will not pretend otherwise.

**One pre-registered kill criterion was missed.** [Issue #381](https://github.com/marius-bughiu/Celerity/issues/381) set two bars before implementation: at least 3x on a full breadth-first traversal against `Dictionary<int, List<int>>`, and at least 2x less managed heap. The heap bar clears at 3.60x; the traversal bar is **missed** on CI's own measurements, which put it at 2.5–2.6x across two runs — short of 3x on both. The first explanation published for that miss was itself wrong — it blamed a consecutive baseline layout the benchmark never built — and the `int[][]` arms were added to test it and refuted it. The type ships on the roadmap's other limb, a genuine BCL gap, which is a judgement call **awaiting maintainer confirmation** rather than something the numbers settle.

**Read the traversal rows as end-to-end comparisons.** The baselines mark visits in a `bool[]`, as a hand-rolled traversal does, while the type packs the same marks into a `ulong` bitmap — 12.5 KB against 100 KB at 100,000 vertices — so a smaller clear and a cache-resident visited set are part of every traversal ratio. The neighbour-iteration row is the only one with no queue and no visited set at all. The topological row has no `int[][]` counterpart measured, so read its 1.95x as being against `List<int>[]` and expect the same shrinkage a tight baseline produced everywhere else.

### API

| Member | Behaviour |
| --- | --- |
| `CompressedGraph(int vertexCount, IEnumerable<GraphEdge> edges)` | Build. `ArgumentNullException` on a `null` sequence, `ArgumentOutOfRangeException` on a negative vertex count, `ArgumentException` when an edge has an endpoint outside `[0, vertexCount)`. |
| `int VertexCount { get; }` | Number of vertices. Ids run over `[0, VertexCount)`. |
| `int EdgeCount { get; }` | Number of distinct directed edges, after duplicates collapsed. |
| `GraphEdge this[int index] { get; }` | The edge at that position in source-major, then ascending-target, order. `O(log V)` — the `IReadOnlyList<T>` contract, not the member to loop over. |
| `ReadOnlySpan<int> Neighbors(int vertex)` | The vertex's targets, ascending — a window onto the graph's own storage. No copy, no allocation, no enumerator. |
| `int Degree(int vertex)` | The out-degree. |
| `bool ContainsEdge(int source, int target)` | Is that directed edge present? `O(log d)` by binary search. |
| `CompressedGraph Reverse()` | The transpose, `O(V + E)`. |
| `int CopyBreadthFirstOrder(int source, Span<int> destination)` | Reachable vertices in breadth-first order, `source` first. Returns how many were written. |
| `int[] GetBreadthFirstOrder(int source)` | The convenience tier of the same. |
| `bool TryCopyTopologicalOrder(Span<int> destination)` | Kahn's algorithm: every vertex before its targets. `false` when the graph has a cycle. |
| `bool TryGetTopologicalOrder(out int[] order)` | The convenience tier of the same; `order` is empty on `false`. |
| `Enumerator GetEnumerator()` | Struct enumerator over every edge in source-major order. |

`Neighbors`, `Degree` and `ContainsEdge` throw `ArgumentOutOfRangeException` for a vertex outside `[0, VertexCount)` — an absent *edge* is an ordinary answer and returns `false`, but an out-of-range *vertex* is a caller bug. `CopyBreadthFirstOrder` stops when the destination fills, and because the order is generated front to back a truncated result is exactly the first `destination.Length` vertices of the full order rather than an arbitrary subset. `TryCopyTopologicalOrder` is the one member whose buffer may **not** be short: it throws `ArgumentException` below `VertexCount`, because a prefix of a topological order is not a useful answer. Which order it produces among the several a graph may admit is unspecified and may change.

`Count` is implemented **explicitly** and is not on the public surface: a graph has two counts and neither owns the bare name, so `VertexCount` and `EdgeCount` say which one they mean. `((IReadOnlyCollection<GraphEdge>)graph).Count` is the edge count, since the list surface is over the edges.

### Caveats

- **Build-once.** The graph is immutable; adding an edge means building a new one, as with [`KdTree<TValue>`](#kdtreetvalue), [`RTree<TValue>`](#rtreetvalue) and [`IntervalTree<TKey, TValue, TComparer>`](#intervaltreetkey-tvalue-tcomparer). Nothing mutates, so enumeration is never invalidated and concurrent readers need no synchronization — and unlike the ordered types there is no comparer caveat, because nothing but `int` ids is ever compared.
- **Dense ids or nothing.** Vertices are `[0, VertexCount)`. If your domain keys are not already dense integers, map them once through a dictionary and keep the graph on the ids; a sparse id space costs an offset entry per unused id.
- **No weights, no shortest paths.** Those are algorithms over a container rather than the container, and shipping one would commit the type to a weight representation before anything needs it.
- **The traversal win needs scale, and below it there is none.** See the measured tables above: at a thousand vertices the graph fits in cache and both the traversal and the build measure **below 1.0x against the exact-sized `int[][]`** — losses, not merely a narrower win.

### Usage example

```csharp
using Celerity.Collections;

// Five packages; an edge means "must be built before". Vertex 2 depends on nothing.
var builds = new CompressedGraph(5, new[]
{
    new GraphEdge(0, 1),
    new GraphEdge(0, 3),
    new GraphEdge(1, 4),
    new GraphEdge(3, 4),
});

// Neighbour iteration: a slice of the graph's own storage, no allocation and no enumerator.
foreach (int dependent in builds.Neighbors(0))
    Console.WriteLine(dependent);                       // 1, then 3

Console.WriteLine(builds.Degree(0));                    // 2
Console.WriteLine(builds.ContainsEdge(0, 4));           // False - the path is 0 -> 1 -> 4, not a direct edge

// Build order, or false if the dependencies are circular.
if (builds.TryGetTopologicalOrder(out int[] order))
    Console.WriteLine(string.Join(" ", order));         // 0 2 1 3 4

// "What does vertex 4 depend on?" - the transpose, O(V + E), not a rebuild. Mind the direction: an edge
// means "must be built before", so 4's in-neighbours are its dependencies, not its dependents.
CompressedGraph incoming = builds.Reverse();
Console.WriteLine(string.Join(" ", incoming.Neighbors(4).ToArray()));     // 1 3

// Everything reachable from 0, in breadth-first order, into a buffer you own.
var reached = new int[builds.VertexCount];
int count = builds.CopyBreadthFirstOrder(0, reached);
Console.WriteLine(string.Join(" ", reached[..count]));  // 0 1 3 4
```

## SuffixArray

```csharp
public sealed class SuffixArray : IReadOnlyList<int>
```

A **suffix array**: a build-once, immutable index over a block of text that answers *where does this substring occur* in time proportional to the **pattern** rather than to the text, plus the longest-common-prefix (LCP) array beside it.

.NET has no text index. `string.IndexOf`, `MemoryExtensions.IndexOf` and `Regex` are vectorized **scans**: every query re-reads the text, so a query costs `O(n)` however many queries follow, and counting every occurrence re-scans from each hit. .NET 9's `SearchValues<string>` is not a counter-example — it indexes the **patterns**, answering "where is the first of these needles" over a haystack it has never seen. Indexing the *text* is the direction none of them go, and it is the one that pays off when the text is fixed and the queries keep coming.

### When to reach for it

Any workload where one block of text is queried many times: log and document search, source-code search, near-duplicate and plagiarism detection, bioinformatics, and any "how many times does X appear" counter over a corpus that does not change. The crossover is the whole story, because **a single query against a text read once is a loss here**, and stays one until the query count amortizes the build — roughly 1,000 counting queries at 100,000 characters, and it *improves* as the text grows. See [Measured](#measured-1).

It is also the only structure here that can answer *what is the longest substring that occurs twice*. `TryGetLongestRepeatedSubstring` reads that off the LCP array in one linear pass; the naive answer is quadratic and no scan-shaped API can express the question at all.

### How it works

The suffixes are ordered by **prefix doubling**. Every position is first ranked by its opening character; each round then sorts the positions by a *pair* of ranks that already covers a prefix of length `w`, which yields ranks covering `2w`, so `log n` rounds settle the order. Each round is a counting sort rather than a comparison sort, because the ranks are dense — and it only has to sort by the *first* half of each pair, since subtracting `w` from the previous round's order produces the ordering by the second half for free. That makes the build `O(n log n)`.

The text is treated as its **cyclic shifts with a sentinel appended** — a symbol smaller than every character, occurring once — so no shift wraps into a tie and the cyclic order restricted to the real positions is exactly the suffix order. The LCP array follows in `O(n)` by **Kasai's algorithm**: walking the text in position order rather than rank order bounds the total comparison work, because dropping a suffix's first character costs its predecessor's match at most one.

**Locating** a pattern is a binary search over that order — two of them when both ends of the range are needed. The suffixes starting with a pattern are **contiguous** in it, so `CountOccurrences` is the width of that range at `O(m log n)` and nothing is enumerated, and `TryGetOccurrences` hands back a slice of the index's own array with no copy at all. That `O(m log n)` is the floor the members build on and not the cost of every one of them: `Contains` needs only one of the two searches, `IndexOf` adds an `O(k)` pass because the order groups the matches without sorting them by position, `CopyOccurrences` and `GetOccurrences` add an `O(k log k)` sort, and `TryGetLongestRepeatedSubstring` is `O(n)` over the LCP array and does no search at all. The API table below states each.

What the index **retains** is the text copy and the two result arrays and nothing else — every scratch buffer the build needs is rented from `ArrayPool<T>` and returned. That is not the same as allocating nothing: `ArrayPool<T>.Shared` allocates when it has no suitable buffer, so a first or contended build still allocates its scratch, exactly as [`CompressedGraph`](#compressedgraph)'s traversals do.

### Measured

Every arm runs a batch of **16 queries** against one index, so a per-query figure is the row divided by 16. The numbers are from this PR's **CI benchmark run**, on the same runner in the same job, rather than from a local machine.

At **100,000 characters** of vocabulary-generated text, against the `string` scan the BCL ships:

| Workload | `string` scan | `SuffixArray` | Ratio |
| --- | ---: | ---: | ---: |
| `Contains`, pattern **absent** | 72.20 µs | 1.12 µs | **64x** |
| `CountOccurrences`, pattern present | 146.55 µs | 2.15 µs | **68x** |
| Every position retrieved into a caller's buffer | 144.96 µs | 12.18 µs | **11.9x** |

And against the `Dictionary<string, int[]>` k-gram index, which is the baseline that matters:

| Workload | k-gram index | `SuffixArray` | Ratio |
| --- | ---: | ---: | ---: |
| `CountOccurrences`, 8-character pattern | 209.5 ns | 2.15 µs | **0.10x — the hand-roll wins by 10.3x** |
| Build | 12.24 ms | 8.78 ms | 1.39x |

At **1,000 characters** the scan margins all but vanish — **1.26x**, **1.34x** and **1.29x** on those same three rows — and the k-gram index still wins the query it can answer, by 6.5x. A small text fits in cache and a vectorized scan over it is already fast; this type is for the case where it is not.

**The crossover, stated rather than left to the reader.** The build is 8.78 ms at 100,000 characters, and a query saves 9.02 µs on counting or 4.44 µs on ruling an absent pattern out. So the index pays for itself at roughly **1,000 counting queries, or 2,000 absent-membership queries**, against the same text. At 1,000 characters the same arithmetic needs about **3,900** queries, because the scan it is replacing costs so little — the crossover improves as the text grows, which is the whole shape of the trade. Below it, `IndexOf` is the right answer and this type is a loss.

**Both pre-registered bars from [#386](https://github.com/marius-bughiu/Celerity/issues/386) clear**, at 100,000 characters: ≥20x the scan on counting a present pattern (**68x**) and ≥20x on ruling out an absent one (**64x**). The k-gram arm is published where it wins, as the issue required, and the build arm sits next to the query arms with the crossover above.

**What the numbers do not say.** The `CountIndexed` row is the honest ceiling on the query claim: if your patterns are all one known length and the text is fixed, an inverted index of that length is an order of magnitude faster per query and you should write one. `SuffixArray` earns its place by answering *every* pattern length out of one structure that costs 10 bytes per character, and by answering the longest-repeated-substring question that neither the scan nor the k-gram index can express at all.

### API

| Member | Behaviour |
| --- | --- |
| `SuffixArray(ReadOnlySpan<char> text)` | Build, `O(n log n)`. The text is copied, so the caller's buffer may be reused. A `null` `string` converts to an empty span and builds an empty index rather than throwing. |
| `int Length { get; }` | Characters in the text, which is also the number of suffixes. |
| `ReadOnlySpan<char> Text { get; }` | The indexed text — slice it to turn a match back into characters. |
| `ReadOnlySpan<int> Suffixes { get; }` | Every start position, ordered by the suffix beginning there. This is the index itself. |
| `ReadOnlySpan<int> LongestCommonPrefixes { get; }` | Entry `i` is the characters shared by the suffixes at ranks `i - 1` and `i`; entry `0` is `0`. |
| `int this[int rank] { get; }` | The start position at that lexicographic rank. `ArgumentOutOfRangeException` outside `[0, Length)`. |
| `bool Contains(ReadOnlySpan<char> pattern)` | Does it occur? `O(m log n)` — **one** binary search, so cheaper than `CountOccurrences`. |
| `int CountOccurrences(ReadOnlySpan<char> pattern)` | How many times, `O(m log n)`. Overlapping occurrences all count. |
| `int IndexOf(ReadOnlySpan<char> pattern)` | The **lowest** matching position, or `-1`. `O(m log n + k)` for `k` matches — the order groups them but does not sort them by position. |
| `bool TryGetOccurrences(ReadOnlySpan<char> pattern, out ReadOnlySpan<int> occurrences)` | The matches as a slice of the index, copying nothing. **Lexicographic** order, not positional. |
| `int CopyOccurrences(ReadOnlySpan<char> pattern, int[] destination, int destinationIndex = 0)` | The matches in **ascending position** order into a caller-owned buffer; returns how many were written. |
| `int[] GetOccurrences(ReadOnlySpan<char> pattern)` | The convenience tier of the same. |
| `bool TryGetLongestRepeatedSubstring(out int start, out int length)` | The longest substring occurring at least twice, `O(n)`. `false` when no character repeats. |
| `Enumerator GetEnumerator()` | Struct enumerator over the start positions in lexicographic order. |

`CopyOccurrences` throws `ArgumentNullException` on a `null` buffer and `ArgumentOutOfRangeException` for a `destinationIndex` outside `[0, destination.Length]`. Writing stops when the buffer fills, so a return value equal to the remaining room may mean the matches were truncated — size the buffer with `CountOccurrences` when every match is needed.

`Count` is implemented **explicitly** and is not on the public surface: the suffix count and the text length are the same number, and `Length` is the name that says which. `((IReadOnlyCollection<int>)index).Count` is that number.

### Caveats

- **Build-once.** The index is immutable; changing the text means building a new one, as with [`KdTree<TValue>`](#kdtreetvalue), [`RTree<TValue>`](#rtreetvalue), [`IntervalTree<TKey, TValue, TComparer>`](#intervaltreetkey-tvalue-tcomparer) and [`CompressedGraph`](#compressedgraph). Nothing mutates, so enumeration is never invalidated and concurrent readers need no synchronization.
- **The build has to be amortized, and one query does not do it.** The build costs what many scans of the same text would. At 100,000 characters it pays for itself at roughly 1,000 counting queries against the same text, and at 1,000 characters at roughly 3,900 — see [Measured](#measured-1). Below the crossover `IndexOf` is the right answer.
- **Ordinal, over UTF-16 code units.** Suffixes are ordered by `char` value — what `StringComparison.Ordinal` compares. There is no culture-aware, case-insensitive or normalizing mode and there will not be: a linguistic comparison is not a total order over fixed-length units, so the suffixes could not be sorted once and binary-searched. Fold the text and the pattern the same way before indexing when case-insensitive matching is wanted. A surrogate pair sorts as its two code units, so a match can begin at a low surrogate — check the boundary in your own text when that matters.
- **About 10 bytes per character, retained.** The text copy is 2, and the suffix and LCP arrays are 4 each. That is five times what the text alone costs, and it is the other half of the reason to check the crossover. The build's scratch is five more `int` buffers, rented from `ArrayPool<T>` and returned rather than held.

  That arithmetic is also the honest way to compare footprints with the `Dictionary<string, int[]>` k-gram index, and the benchmark's allocation column is **not**: `MemoryDiagnoser` measures allocation *volume* over the run, so it charges the k-gram build for the transient `List<int>` per gram that the finished index does not hold, and it does not charge this type for pooled scratch a warm pool hands back for free. Read that column as build allocations. The retained comparison is arithmetic: 10 bytes per character here, against — for the 8-gram index over `n` positions — a `string` key of about 40 bytes, an `int[]` of at least 32, and the dictionary's own entry and bucket per *distinct* gram, which on text with few exact repeats is an order of magnitude more.
- **The empty pattern matches at every start position.** It occurs `Length` times, so `IndexOf` returns `0` and `Contains` returns `true` on a non-empty text — and over an *empty* text it occurs nowhere, so `CountOccurrences` is `0`, `Contains` is `false` and `IndexOf` is `-1`. That is one rule with no special case, rather than `string`'s, which reports the empty needle as found at `0` even in the empty string.
- **`TryGetOccurrences` is not sorted.** Its span is in suffix order. Use `CopyOccurrences` or `GetOccurrences` when ascending positions are wanted; both pay an `O(k log k)` sort for it.

### Usage example

```csharp
var index = new SuffixArray("the cat sat on the mat");

Console.WriteLine(index.CountOccurrences("at"));        // 3 - one pair of binary searches, no scan
Console.WriteLine(index.IndexOf("the"));                // 0
Console.WriteLine(index.Contains("dog"));               // False

// Every position, ascending.
Console.WriteLine(string.Join(" ", index.GetOccurrences("at")));   // 5 9 20

// The zero-copy tier: a slice of the index itself, in suffix order rather than positional.
if (index.TryGetOccurrences("the", out ReadOnlySpan<int> found))
    Console.WriteLine(found.Length);                    // 2

// The question no scan can express: the longest substring that occurs at least twice.
if (index.TryGetLongestRepeatedSubstring(out int start, out int length))
    Console.WriteLine(index.Text.Slice(start, length).ToString());  // "the "
```

## TimerWheel&lt;TValue&gt;

```csharp
public sealed class TimerWheel<TValue> : IReadOnlyCollection<ScheduledTimer<TValue>>
```

A **hierarchical timing wheel**: a container of pending **deadlines**, with constant-time `Cancel`, amortized constant-time `Schedule` — the one call in a growth cycle resizes both backing arrays — and an `Advance` bounded by the wheel's own geometry and the timers it moves — `O(levels x slots + fired + cascaded)` — rather than by the ticks it crosses. It is the structure behind the Linux kernel's timers, Netty's `HashedWheelTimer` and Kafka's request purgatory, and it is what answers *which of these hundred thousand pending things have timed out*.

**It is a data structure, not a scheduler.** There is no thread, no clock and no callback: the caller owns time and drives the wheel with `Advance`. That is what keeps it deterministic, testable, and inside this library's [non-goals](https://github.com/marius-bughiu/Celerity/blob/main/ROADMAP.md#non-goals). A tick is whatever unit you schedule and advance in — milliseconds, frames, sequence numbers.

.NET has nothing for this. `PriorityQueue<TElement, TPriority>` is what a developer reaches for and **it cannot cancel**: there is no `Remove` on the `net8.0` floor at all, and the one .NET 9 added is documented `O(n)`. `System.Threading.Timer` is a scheduler rather than a container — one object and one registration per timeout, which nobody allocates a hundred thousand of. [`IndexedPriorityQueue<TElement, TPriority, THasher>`](#indexedpriorityqueuetelement-tpriority-thasher) — this library's own addressable heap — *can* cancel, in `O(log n)`, and is the strong baseline this type is measured against rather than a reason to skip it.

### The workload is defined by cancellation

Almost every timeout is cancelled rather than fired: the reply arrived, the lease was renewed, the connection closed cleanly. That is the axis the obvious structures fail on. The standard `PriorityQueue` workaround is **lazy deletion** — push everything, keep a `HashSet` of cancelled ids, discard tombstones on pop — which makes the cancel itself cheap and then charges for it at drain time, on a heap that has grown with the timers that will never fire and still holds their payloads.

Reach for this type when you have a **population** of pending deadlines: request and RPC timeouts, connection idle-reaping, lease and session expiry, retry backoff, rate-limiter windows, delayed-message queues. Do not reach for it for a handful of timers — see [Measured](#measured-2), where a thousand timers puts it level with the BCL heap on the piecewise operations.

### How it works

`levels` wheels of `slotsPerWheel` slots each, both powers of two, so every index is a shift and a mask. Level `L` spans `slots^(L+1)` ticks, and a timer lands in the **lowest level that can express its delay**, in the slot its deadline's level-`L` digit names. The slots are heads of intrusive doubly-linked lists threaded through one flat entry array — the same layout [`SpatialGrid<TValue>`](#spatialgridtvalue) uses for its cells — so there is no object per timer and no allocation per schedule once the array has grown. A cancel is two pointer writes.

**`Advance` is not the textbook tick-at-a-time loop.** The classical wheel steps one tick, then one more, cascading whenever the low wheel wraps, which makes a jump cost `O(ticks)` — a caller that misses a second of wall clock at millisecond granularity would pay a thousand iterations over empty slots for it. This one computes, per level, exactly the slots the move crosses and walks them from the top level down, firing what is due and staging what is not for re-insertion against the new time. The work is `O(levels × slots + fired + cascaded)` **however far the clock jumped**, and `O(ticks + fired + cascaded)` for the ordinary small step — the cascade term belongs in both, because the single tick that carries the clock across a level boundary reaches that level's whole slot and moves everything on it down, which can be any number of timers and fire none of them. A cascade only ever moves a timer *down* a level, so each timer is touched at most once per level over its whole life, which is what keeps that term amortized rather than repeated.

A timer scheduled with a **zero** delay is due at the current tick, which no wheel slot can hold — every slot is strictly in the future — so it goes on a separate already-due list that the next `Advance` drains whatever tick it names.

### Measured

Every ratio names its baseline, and there are two: the BCL `PriorityQueue<int, long>` with the lazy-deletion workaround, and `IndexedPriorityQueue`, which cancels for real. The unit that matters is **Round** — schedule *n* timeouts at random delays across a 10,000-tick span, cancel nine in ten of them, then run the clock out and drain what survived — and the other four groups take that round apart.

At **100,000 timers**:

| Workload | `PriorityQueue` | `IndexedPriorityQueue` | `TimerWheel` | vs BCL | vs addressable |
| --- | ---: | ---: | ---: | ---: | ---: |
| **Round** — schedule, cancel 90%, drain | 11,117 µs | 8,246 µs | 1,116 µs | **9.96x** | **7.4x** |
| Schedule | 614 µs | 2,471 µs | 365 µs | 1.68x | 6.8x |
| Cancel | 440 µs | 4,114 µs | 232 µs | 1.90x | 17.7x |
| Drain, nothing cancelled | 9,620 µs | 18,879 µs | 1,223 µs | 7.9x | 15.4x |
| Tick — the clock driven one tick at a time | 9,806 µs | 18,916 µs | 1,835 µs | 5.3x | 10.3x |

**It is not the lightest of the three, and the first draft of this section claimed it was.** A round at 100,000 allocates 4.01 MB here against the heap's 3.34 MB and the addressable heap's 3.30 MB — this type is the *heaviest*, by about a fifth, because a 24-byte entry record plus a payload slot plus the handle the caller keeps costs more per timer than the heap's `(int, long)` pair. What the heap holds that this does not is *cancelled* timers, which stay in its array with their payloads reachable until something pops them — a statement about when memory is released, not about how much is asked for, and it was wrong to publish it as the latter.

At **1,000 timers** the picture changes, and this is the honest half of it. `Round` still wins by **3.4x** over the BCL heap and 4.5x over the addressable one, and `Drain` by 18.4x — but **`Cancel` is 1.9x slower**, `Schedule` a little slower, and `Tick` level:

| Workload | `PriorityQueue` | `TimerWheel` | vs BCL |
| --- | ---: | ---: | ---: |
| Round | 22.20 µs | 6.51 µs | **3.4x** |
| Drain, nothing cancelled | 213.7 µs | 11.28 µs | 18.4x |
| Tick | 67.2 µs | 63.3 µs | 1.06x — level |
| Schedule | 10.92 µs | 11.98 µs | **0.91x — the heap wins by 1.10x** |
| Cancel | 7.46 µs | 13.92 µs | **0.54x — the heap wins by 1.9x** |

A thousand-element heap fits in cache and its sift is a handful of predictable compares, while this type pays a scattered write into a 1,025-slot bucket array whatever the population. **This is a large-population type**, and the round is 3.4x at a thousand only because the drain is where the heap pays for its cheap cancels — which is also why the two losses are on the piecewise groups and not on the workload that contains both halves.

Two of those rows deserve their qualification rather than a footnote:

- **The `Cancel` loss is a comparison against something that does not cancel.** The BCL arm adds an id to a `HashSet` and removes nothing from the heap, because it cannot; the work reappears in `Drain`, where every cancelled timer still has to be popped, sifted and discarded. `Round` is the row where both halves are charged to the same arm, which is why it is the ship gate.
- **`Drain` was expected to be this type's worst case and is not.** With nothing cancelled the wheel gets no benefit from its constant-time removal and still pays the slot sweep — but draining a 100,000-element heap is 100,000 sift-downs of a cache-missing array, against one walk of 1,025 buckets and a linked-list step per timer. It is also the arm that pays for the delivery guarantee described under [Caveats](#caveats-3): handing payloads over is a *second* pass over the fired timers rather than something interleaved with the walk, and on the all-fire arm at 100,000 that pass costs about 80% — 1,223 µs against the 680 µs an interleaved drain measured. On the `Round` workload, where one timer in ten fires, it is worth about 5%, which is what makes it a cheap guarantee to buy.

**Both pre-registered bars from [#393](https://github.com/marius-bughiu/Celerity/issues/393) clear**: at least 3x both baselines on the round at 100,000 (9.96x and 7.4x), and at least 2x `IndexedPriorityQueue` on `Schedule` and `Cancel` in isolation (6.8x and 17.7x). The third — the uncancelled drain — was pre-registered as a loss to be documented rather than a gate, and is a 7.9x win instead.

### API

| Member | Behaviour |
| --- | --- |
| `TimerWheel(int slotsPerWheel = 256, int levels = 4, int capacity = 0)` | The defaults give a 2^32-tick horizon — about 49 days at a millisecond tick — in one flat array of 1,025 slot heads, 4 KiB whatever the wheel goes on to hold. `slotsPerWheel` must be a power of two of at least two, `levels` at least one, and the two together must not put `Horizon` past `2^62`. |
| `TimerHandle Schedule(long delayTicks, TValue? value)` | Schedule `delayTicks` from now, `O(1)` amortized. Zero means due now. `ArgumentOutOfRangeException` for a negative delay, one at or beyond `Horizon`, or one that would put the deadline past `long.MaxValue` — so every timer the wheel accepts is reachable by some advance. |
| `TimerHandle ScheduleAt(long deadline, TValue? value)` | The same, by absolute tick on the same clock as `CurrentTick`. A deadline in the past is rejected rather than silently fired. |
| `bool Cancel(TimerHandle handle)` | Cancel in `O(1)`, releasing the payload. `false` — not an exception — when the handle does not address a pending timer, because losing the race against your own clock is the normal outcome rather than a programming error. |
| `bool TryGetDeadline(TimerHandle handle, out long deadline)` | The absolute deadline, `O(1)`. Also the way to ask whether a timer is still pending. |
| `int Advance(long tick, ICollection<TValue?> expired)` | Move the clock and append the payload of everything due at or before `tick`; returns how many fired. The destination is **not** cleared first, so one reused `List<T>` can collect several advances — reusing one is what makes a steady-state advance allocation-free. A read-only destination is rejected before the clock moves. |
| `List<TValue?> Advance(long tick)` | The convenience tier of the same; allocates a list per call. |
| `void Clear()` | Cancel everything, retiring every handle. Keeps the storage and **leaves `CurrentTick` where it stands** — this empties the container, it does not rewind the clock. |
| `long CurrentTick { get; }` | Where the clock stands. Starts at zero and never moves backwards. |
| `int Count { get; }` | Timers scheduled and neither fired nor cancelled. |
| `long Horizon { get; }` | `slotsPerWheel^levels` — the exclusive upper bound on a schedulable delay. |
| `int SlotsPerWheel { get; }` / `int Levels { get; }` | The geometry, as constructed. |
| `Enumerator GetEnumerator()` | Struct enumerator over the pending timers as `ScheduledTimer<TValue>`, in an unspecified order. |

`TimerHandle` is an opaque generational token — a slot index and a version — mirroring `SpatialGridHandle` exactly: the `default` handle resolves to nothing, a handle is retired the moment its timer fires or is cancelled, and a handle belongs to the wheel that issued it. `ScheduledTimer<TValue>` is a `Deadline` and a `Value`, with a `Deconstruct`.

### Caveats

- **The horizon is the trade.** A wheel buys its constant time by bucketing rather than ordering, and the bucketing is finite: a delay of `Horizon` ticks or more is rejected rather than silently misplaced. Widen it by adding a level — each multiplies the horizon by `slotsPerWheel` and costs another `slotsPerWheel` slot heads, 1 KiB at the default width — or by choosing a coarser tick.
- **A batch of fired timers comes back in no particular order.** Only *due-ness* is promised: every payload `Advance` appends has a deadline at or before the tick advanced to. Stepping tick by tick makes the question moot, since every timer in a batch then shares one deadline; it becomes visible only on a jump. A caller who needs the earliest deadline first wants a priority queue and pays `O(log n)` for it.
- **Capacity only grows.** There is no `TrimExcess`: a handle *is* a position in the entry array, so compacting it would invalidate every handle a caller is holding, which is the one thing this type promises not to do. `Clear` returns the storage to the free list but keeps it. This is the same trade [`SpatialGrid<TValue>`](#spatialgridtvalue) makes and for the same reason.
- **A destination whose `Add` throws cannot damage the wheel**, which is why `Advance` hands the payloads over as its last step rather than interleaving delivery with the slot walk. A timer that could not be delivered is still pending, still counted, still addressable by its handle, and delivered by the next advance — though not necessarily *before* the timers that advance makes due, since a batch has no promised order. The clock has still moved, because that part succeeded. A read-only destination is rejected before the clock moves at all. The guarantee is not free — see the `Drain` row above — but it is paid per *fired* timer, so the workload the type exists for barely feels it.
- **The wheel may not be modified from the destination it is delivering into.** `Advance` is the one place this type runs code it does not own, and a destination whose `Add` calls back into `Schedule`, `Cancel` or `Clear` would mutate the buckets and the free list under the loop walking both. Such a call throws `InvalidOperationException`. Reschedule after the advance returns, which is what a `foreach` over the destination does anyway.
- **Not thread-safe**, like every collection here. One clock, one driver.
- **Enumeration is invalidated** by `Schedule`, a successful `Cancel`, an `Advance` that fired something, and a `Clear` that removed something. An `Advance` that fires nothing deliberately does *not* invalidate it, even when it cascaded timers between levels: cascading changes neither the set of pending timers nor the slot each occupies, so the sequence an enumerator is walking is unaffected. That is the family's own rule, the one `SpatialGrid<TValue>.Move` is held to.
- **Handle versions cycle** through `[1, uint.MaxValue]`, so they repeat after 4,294,967,295 vacations of the *same* slot. Every generational slot map has this ceiling.

### Usage example

```csharp
// A tick is a millisecond here; the default geometry reaches about 49 days.
var timeouts = new TimerWheel<PendingRequest>();
var fired = new List<PendingRequest?>();          // reused, so a steady-state tick allocates nothing

// Send a request and arm its timeout. Keep the handle with the request.
PendingRequest request = Send(query);
request.Timeout = timeouts.Schedule(delayTicks: 30_000, request);

// The reply beat the clock: cancel in O(1), and the wheel stops holding the payload.
timeouts.Cancel(request.Timeout);

// Drive the clock from wherever your time comes from. Everything due comes back at once.
fired.Clear();
timeouts.Advance(Environment.TickCount64 - started, fired);
foreach (PendingRequest? timedOut in fired)
    timedOut!.Fail(new TimeoutException());

// A jump costs the wheel, not the distance: at worst every slot at every level, never the tick count.
timeouts.Advance(timeouts.CurrentTick + 5_000_000, fired);

// What is still pending, and how long each has left.
foreach ((long deadline, PendingRequest? pending) in timeouts)
    Console.WriteLine($"{pending} has {deadline - timeouts.CurrentTick} ticks left");
```
