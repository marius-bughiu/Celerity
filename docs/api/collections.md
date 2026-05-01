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
public TValue? this[TKey key] { get; set; }
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

Removes the entry for `key`. Returns `true` if the key was found and removed, `false` otherwise. After removal, adjacent entries are rehashed to maintain probe-chain correctness.

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

A convenience subclass of `LongDictionary<TValue, Int64WangHasher>` for the common case of 64-bit integer-keyed dictionaries.

```csharp
public class LongDictionary<TValue>
    : LongDictionary<TValue, Int64WangHasher>
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

A high-performance dictionary keyed by `long`, parameterized on a custom hash provider. Mirrors `IntDictionary` but for 64-bit keys. Defaults to `Int64WangHasher` when used through the convenience subclass. Implements `IReadOnlyDictionary<long, TValue?>`.

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
```

Throws the same `ArgumentOutOfRangeException`s as the dictionaries.

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
```

---

## IntSet&lt;THasher&gt;

A high-performance set of `int` values, parameterized on a custom hash provider. Implements `IEnumerable<int>`.

```csharp
public class IntSet<THasher> : IEnumerable<int>
    where THasher : struct, IHashProvider<int>
```

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
