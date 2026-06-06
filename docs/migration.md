# Migration Guide — from BCL collections to Celerity

Celerity is designed to be a near drop-in for the parts of the BCL collection API that most code actually uses. This guide maps each standard .NET collection to its Celerity counterpart, shows the mechanical edits, and calls out the behavioural differences you must account for before switching.

> **Before you migrate:** Celerity wins on *specific shapes* — `int`/`long`-keyed tables, build-once string lookups, one-to-many maps, single-threaded hot paths. It is **not** a universal replacement for `Dictionary<,>`. If a collection isn't on the [decision table](../README.md#choosing-a-collection), staying on the BCL type is the right call. See [non-goals](#what-not-to-migrate).

## Type mapping at a glance

| BCL type | Celerity replacement | Notes |
|---|---|---|
| `Dictionary<int, V>` | `IntDictionary<V>` | No hasher needed; defaults to `Int32WangNaiveHasher`. |
| `Dictionary<long, V>` | `LongDictionary<V>` | Defaults to `Int64WangNaiveHasher`. |
| `Dictionary<K, V>` (other keys) | `CelerityDictionary<K, V, THasher>` | Pick a struct hasher for `K`. |
| `FrozenDictionary<string, V>` | `FrozenCelerityDictionary<V>` | Build-once, read-many; perfect hashing. |
| `HashSet<int>` | `IntSet` | Membership only. |
| `HashSet<long>` | `LongSet` | Membership only. |
| `HashSet<T>` (other) | `CeleritySet<T, THasher>` | Pick a struct hasher for `T`. |
| `ILookup<K, V>` / `IEnumerable.ToLookup` | `CelerityMultiMap<K, V, THasher>` | One key → many values; mutable. |
| `ConcurrentDictionary<,>` | *(stay on BCL)* | Celerity is single-threaded. |

## The three differences that affect every migration

These apply to **all** Celerity dictionaries and sets. Internalize them once.

### 1. Read-only interface, not `IDictionary<,>`

Celerity dictionaries implement `IReadOnlyDictionary<TKey, TValue?>` — **not** the mutable `IDictionary<TKey, TValue>`. The concrete type is fully mutable (indexer set, `Add`, `Remove`, `Clear`), but code that passes the dictionary around as `IDictionary<,>` won't compile.

```csharp
// Before
IDictionary<int, string> map = new Dictionary<int, string>();

// After — either keep the concrete type, or use the read-only interface
IntDictionary<string> map = new IntDictionary<string>();   // mutable via concrete API
IReadOnlyDictionary<int, string?> ro = map;                // read-only view
```

If you have a method signature `void Consume(IDictionary<int,string> d)`, change it to take the concrete `IntDictionary<string>` or `IReadOnlyDictionary<int, string?>`.

### 2. Values are nullable (`TValue?`)

The read interface is `IReadOnlyDictionary<TKey, TValue?>`, and `TryGetValue`, the indexer getter, and enumeration all surface `TValue?`. For reference types under nullable-aware code this means an extra null check (or `!`) at the call site:

```csharp
if (dict.TryGetValue(key, out string? value))   // note: string?, not string
    Use(value!);                                  // you asserted presence via the bool
```

For value types this is just `Nullable<T>` ergonomics and usually needs no change.

### 3. No guaranteed iteration order

BCL `Dictionary<,>` has an *incidental* insertion-ish order that people sometimes rely on. Celerity's iteration order is **unspecified and may change between versions** (it reflects the open-addressed probe layout). If you depend on order, sort explicitly or keep the BCL type:

```csharp
foreach (var kvp in dict.OrderBy(k => k.Key)) { ... }   // make the order explicit
```

## `Dictionary<int, V>` → `IntDictionary<V>`

The closest mapping in the library. The API surface matches the parts of `Dictionary<,>` people reach for:

```csharp
// Before
var counts = new Dictionary<int, int>();
counts[42] = 1;
counts[42]++;
counts.TryAdd(7, 100);
counts.Add(8, 200);                 // throws if 8 present
if (counts.TryGetValue(42, out int hits)) { ... }
counts.Remove(7);
foreach (var kvp in counts) { ... }

// After
var counts = new IntDictionary<int>();
counts[42] = 1;
counts[42]++;
counts.TryAdd(7, 100);
counts.Add(8, 200);                 // same throw-on-duplicate semantics
if (counts.TryGetValue(42, out int hits)) { ... }
counts.Remove(7);
foreach (var kvp in counts) { ... } // struct enumerator, zero allocation
```

The key `0` is a legitimate key (stored out-of-band so it never collides with the empty-slot sentinel) — `counts[0]` round-trips correctly. `LongDictionary<V>` is the identical migration for `long` keys.

**Constructor differences:** Celerity takes `(int capacity = 16, float loadFactor = 0.75f)`. There is no `IEqualityComparer<TKey>` parameter — equality is `==` for the integer types, or the supplied struct hasher for `CelerityDictionary`. To replicate a custom comparer's hashing, write a struct hasher (see below).

## `Dictionary<K, V>` (other key types) → `CelerityDictionary<K, V, THasher>`

You must name a struct hasher as the third type argument. Pick from `Celerity.Hashing` or write your own:

```csharp
// Before
var byId   = new Dictionary<Guid, string>();
var byName = new Dictionary<string, int>();

// After
var byId   = new CelerityDictionary<Guid, string, GuidHasher>();
var byName = new CelerityDictionary<string, int, StringFnV1AHasher>();
```

For a key type with no built-in hasher, `DefaultHasher<T>` delegates to `EqualityComparer<T>.Default.GetHashCode()` and works for anything:

```csharp
var byDate = new CelerityDictionary<DateOnly, string, DefaultHasher<DateOnly>>();
```

### Replacing a custom `IEqualityComparer<TKey>`

If your BCL dictionary used a custom comparer purely for hashing, port the `GetHashCode` logic into a struct hasher:

```csharp
// Before
class MyComparer : IEqualityComparer<MyKey> {
    public bool Equals(MyKey a, MyKey b) => a.Id == b.Id;
    public int GetHashCode(MyKey k) => k.Id;
}
var d = new Dictionary<MyKey, V>(new MyComparer());

// After
struct MyKeyHasher : IHashProvider<MyKey> {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(MyKey k) => k.Id;
}
var d = new CelerityDictionary<MyKey, V, MyKeyHasher>();
```

Note: equality of *keys* themselves still uses `EqualityComparer<TKey>.Default` inside `CelerityDictionary`. If your custom comparer changed **equality semantics** (not just hashing), Celerity can't reproduce that today — keep the BCL `Dictionary<,>` with its comparer.

## `FrozenDictionary<string, V>` → `FrozenCelerityDictionary<V>`

Both are immutable, build-once, read-many tables. Celerity searches for a *perfect* (collision-free) hash and is tunable via `IHashProvider<T>`:

```csharp
// Before
var routes = pairs.ToFrozenDictionary();

// After
var routes = new FrozenCelerityDictionary<int>(pairs);
Console.WriteLine(routes.IsPerfectlyHashed);   // True → single-probe lookups
```

No `Add` / `Remove` (same as `FrozenDictionary`). For non-ASCII keys or to restore a perfect layout when the default collides, use `FrozenCelerityDictionary<V, StringFnV1AFullHasher>`. See the [performance guide](performance.md#6-build-once-read-many--freeze-it).

## `HashSet<T>` → `IntSet` / `LongSet` / `CeleritySet<T, THasher>`

```csharp
// Before
var seen = new HashSet<int>();
seen.Add(1);
bool isNew = seen.Add(2);     // false if already present
seen.Contains(1);
seen.Remove(2);

// After
var seen = new IntSet();
seen.Add(1);
bool isNew = seen.TryAdd(2);  // returns true on first add, false on duplicate
seen.Contains(1);
seen.Remove(2);
```

**Gotcha:** Celerity sets expose `Add` (insert) and `TryAdd` (returns the BCL `HashSet.Add` boolean). Where you used `HashSet<T>.Add`'s return value to detect first-insertion, switch to `TryAdd`. Celerity sets are membership-only — there are no set-algebra operations (`UnionWith`, `IntersectWith`, etc.); compose those manually or stay on `HashSet<T>` if you need them.

## `ILookup<K, V>` / `ToLookup` → `CelerityMultiMap<K, V, THasher>`

`ToLookup` produces an **immutable** `ILookup<,>`; `CelerityMultiMap` is a **mutable** one-to-many map that also implements `ILookup<TKey, TValue?>`, so it flows through the same LINQ code:

```csharp
// Before
ILookup<string, string> subs = events.ToLookup(e => e.Topic, e => e.Handler);
foreach (var handler in subs["orders"]) { ... }

// After
var subs = new CelerityMultiMap<string, string, StringFnV1AHasher>();
subs.Add("orders", "billing");
subs.Add("orders", "fulfilment");          // appends, does not overwrite
foreach (var handler in subs["orders"]) { ... }
subs.Remove("orders", "billing");          // drop one value
subs.RemoveAll("orders");                  // drop the whole key
```

The indexer returns an **empty group** for an absent key (it never throws — unlike indexing a `Dictionary<,>`). `Count` is distinct keys; `ValueCount` is total values.

## What *not* to migrate

Keep the BCL type when any of these hold — Celerity explicitly does not target them:

- **Concurrent access from multiple threads.** Celerity is single-threaded. Use `ConcurrentDictionary<,>` or your own locking around a BCL `Dictionary<,>`.
- **You need the mutable `IDictionary<,>` interface** at an API boundary you don't control.
- **You depend on a specific or stable iteration order.**
- **You need set-algebra operations** (`UnionWith`, `IntersectWith`, `IsSubsetOf`, …) — use `HashSet<T>`.
- **Custom *equality* semantics** (not just custom hashing) via `IEqualityComparer<TKey>`.
- **The collection isn't on a documented winning shape.** If Celerity doesn't beat the BCL on your workload, there's nothing to gain.

## Migration checklist

1. ☐ Confirm the workload is a shape Celerity wins on (see the [decision table](../README.md#choosing-a-collection)).
2. ☐ Swap the type; for non-`int`/`long` keys, choose a struct hasher.
3. ☐ Change `IDictionary<,>` parameters/fields to the concrete type or `IReadOnlyDictionary<,>`.
4. ☐ Add `?`/null handling where `TryGetValue` / the indexer / enumeration now yield `TValue?`.
5. ☐ Replace any reliance on iteration order with an explicit sort.
6. ☐ For sets, switch first-insertion checks from `Add` to `TryAdd`.
7. ☐ Pre-size `capacity` and validate the swap with a benchmark — see the [performance guide](performance.md).

## See also

- [Choosing a collection](../README.md#choosing-a-collection)
- [Performance tuning guide](performance.md)
- [Troubleshooting](troubleshooting.md) and [FAQ](faq.md)
- [Collections API reference](api/collections.md)
