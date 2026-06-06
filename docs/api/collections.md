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

#### GetEnumerator

```csharp
public Enumerator GetEnumerator()
```

Returns a struct enumerator that yields `KeyValuePair<TKey, TValue?>`. The out-of-band default-key entry is yielded first if present. Mutating the dictionary during enumeration throws `InvalidOperationException` from the next `MoveNext` / `Reset` call, matching BCL `Dictionary<,>` semantics. Iteration order is unspecified and may change between versions.

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
- `Enumerator GetEnumerator()` — struct enumerator yielding `KeyValuePair<int, TValue?>`. The out-of-band zero-key entry is yielded first if present. Mutating the dictionary during enumeration throws `InvalidOperationException` from the next `MoveNext` / `Reset` call, matching BCL `Dictionary<,>` semantics. Iteration order is unspecified and may change between versions.

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

A high-performance generic set parameterized on a custom hash provider. Set counterpart to `CelerityDictionary`. Implements `IEnumerable<T>`.

```csharp
public class CeleritySet<T, THasher> : IEnumerable<T>
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
- `int Count { get; }`
- `Enumerator GetEnumerator()` — struct enumerator. The out-of-band `default(T)` entry (zero for primitives, `Guid.Empty`, `null` for reference types) is yielded first when present.

### Default-element handling

`default(T)` is stored out-of-band via a `_hasDefaultValue` flag and never collides with the empty-slot sentinel. Mutating the set during enumeration throws `InvalidOperationException` on the next `MoveNext` / `Reset`, matching BCL `HashSet<T>`.

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

A high-performance set of `int` values, parameterized on a custom hash provider. Implements `IEnumerable<int>`.

```csharp
public class IntSet<THasher> : IEnumerable<int>
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

A high-performance set of `long` values, parameterized on a custom hash provider. Implements `IEnumerable<long>`.

```csharp
public class LongSet<THasher> : IEnumerable<long>
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
