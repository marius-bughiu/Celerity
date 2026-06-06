# Troubleshooting

Symptoms you might hit when adopting Celerity, and what causes them. Each entry lists the message or behaviour, the cause, and the fix. For conceptual "why does it work this way" questions, see the [FAQ](faq.md).

## Compile-time errors

### `The type 'X' must be ... 'IHashProvider<T>' ... and 'struct'`

```
error CS0315: The type 'MyHasher' cannot be used as type parameter 'THasher'.
error CS0452: The type 'MyHasher' must be a non-nullable value type ...
```

**Cause:** Your hasher is a `class`, or doesn't implement `IHashProvider<T>` for the right `T`.

**Fix:** Hashers must be **structs** — the `where THasher : struct, IHashProvider<TKey>` constraint is what lets the JIT devirtualize and inline `Hash()`. Make it a `struct` and implement `IHashProvider<TKey>` for the dictionary's exact key type:

```csharp
struct MyHasher : IHashProvider<string>          // struct, not class
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(string key) => /* ... */;
}
```

### `Using the generic type 'CelerityDictionary<,,>' requires 3 type arguments`

**Cause:** `CelerityDictionary` always needs a hasher type argument — there's no two-argument form.

**Fix:** Supply a struct hasher, e.g. `new CelerityDictionary<Guid, string, GuidHasher>()`. If you don't have a purpose-built hasher for the key type, use `DefaultHasher<TKey>`. For `int` / `long` keys, prefer the `IntDictionary<TValue>` / `LongDictionary<TValue>` convenience types, which pick the hasher for you.

### `'IntDictionary<int>' does not contain a definition for 'UnionWith'` (and similar)

**Cause:** Celerity sets are membership-only; they don't implement set-algebra operations, and the dictionaries implement `IReadOnlyDictionary<,>`, not `IDictionary<,>`.

**Fix:** Compose the operation manually, or keep the BCL `HashSet<T>` / `Dictionary<,>` for that code path. See the [migration guide](migration.md#what-not-to-migrate).

### `Cannot implicitly convert 'IntDictionary<string>' to 'IDictionary<int, string>'`

**Cause:** Celerity dictionaries implement the **read-only** `IReadOnlyDictionary<TKey, TValue?>`, not the mutable `IDictionary<,>`.

**Fix:** Use the concrete type (it's fully mutable) or `IReadOnlyDictionary<TKey, TValue?>` at the boundary. Change method signatures that demand `IDictionary<,>` accordingly. See [migration §1](migration.md#1-read-only-interface-not-idictionary).

### Nullable warnings: `Converting null literal or possible null value` after `TryGetValue`

**Cause:** The interface is `IReadOnlyDictionary<TKey, TValue?>`, so `TryGetValue(key, out TValue? value)` and the indexer getter yield `TValue?`.

**Fix:** This is expected. Once the `bool` from `TryGetValue` is `true` you've established presence — use `value!` or a null check:

```csharp
if (dict.TryGetValue(key, out string? value))
    Use(value!);
```

## Runtime exceptions

### `KeyNotFoundException` from the indexer getter

```csharp
var v = dict[missingKey];   // throws KeyNotFoundException
```

**Cause:** The indexer **getter** throws on a missing key, matching BCL `Dictionary<,>`.

**Fix:** Use `TryGetValue` (or `ContainsKey` first) when the key may be absent:

```csharp
if (dict.TryGetValue(key, out var v)) Use(v);
```

Note `CelerityMultiMap`'s indexer is the exception — it returns an **empty group** for an absent key and never throws.

### `ArgumentException: An item with the same key has already been added`

**Cause:** `Add(key, value)` throws on a duplicate key, as does the `IEnumerable<KeyValuePair<,>>` constructor when the source contains duplicate keys (including duplicate `default(TKey)` / zero-key entries). This is intentional BCL parity.

**Fix:** Use `TryAdd` (returns `false` instead of throwing) or the indexer set (overwrites) when duplicates are expected:

```csharp
dict.TryAdd(key, value);   // false if present, no overwrite
dict[key] = value;         // insert or overwrite
```

When building from a source that may contain duplicates, de-duplicate first (e.g. `source.GroupBy(...).Select(g => g.Last())`).

### `InvalidOperationException` during `foreach`

```
Collection was modified; enumeration operation may not execute.
```

**Cause:** You mutated the dictionary/set (add, remove, `Clear`, indexer-insert of a new key) while enumerating it. The struct enumerator detects this on the next `MoveNext` / `Reset`, matching BCL semantics.

**Fix:** Don't mutate during enumeration. Snapshot the keys to remove, then mutate after the loop:

```csharp
var toRemove = new List<int>();
foreach (var kvp in dict)
    if (ShouldDrop(kvp)) toRemove.Add(kvp.Key);
foreach (var k in toRemove) dict.Remove(k);
```

### `ArgumentOutOfRangeException` from a constructor

**Cause:** `capacity < 0`, or `loadFactor <= 0` / `loadFactor >= 1`. The valid load-factor range is strictly `0 < loadFactor < 1`.

**Fix:** Pass a non-negative capacity and a load factor inside `(0, 1)`, e.g. `0.6f`–`0.85f`. See the [performance guide](performance.md#4-tune-the-load-factor).

### `ArgumentNullException` from the enumerable constructor

**Cause:** The `IEnumerable<KeyValuePair<TKey, TValue>>` constructor was passed `null`.

**Fix:** Pass a non-null source. An empty (but non-null) sequence is fine.

## Behavioural surprises

### Key `0`, `null`, or `Guid.Empty` "doesn't work" / seems to collide

**Cause:** *It does work* — this is a frequently-suspected non-bug. `default(TKey)` (zero for `int`/`long`, `null` for reference types, `Guid.Empty` for `Guid`) would normally collide with the empty-slot sentinel used during probing.

**Fix:** Nothing to fix. Celerity stores the default/zero key **out-of-band** in a dedicated slot, so `dict[0]`, `dict[null]`, and `dict[Guid.Empty]` round-trip correctly and are surfaced through enumeration and every interface member. If you see corruption around the zero key, it's a bug worth [filing](https://github.com/marius-bughiu/Celerity/issues) — the older corruption bugs in this area are fixed and regression-tested.

### Enumeration order differs from `Dictionary<,>` / changed after an upgrade

**Cause:** Celerity's iteration order reflects the open-addressed probe layout and is **unspecified** — it can differ from the BCL and may change between versions or after a resize.

**Fix:** Never rely on iteration order. Sort explicitly when you need a stable order: `foreach (var kvp in dict.OrderBy(k => k.Key))`.

### `FrozenCelerityDictionary.IsPerfectlyHashed` is `false`

**Cause:** The chosen hasher produced a collision between two keys' raw hash codes, so the build fell back to short linear probing. Lookups are still **correct** — just not single-probe.

**Fix:** This is often acceptable. To restore the single-probe fast path, supply a stronger/wider hasher via `FrozenCelerityDictionary<TValue, THasher>` — e.g. `StringFnV1AFullHasher` for keys with non-ASCII characters (the default `StringFnV1AHasher` folds only the low byte). See the [performance guide](performance.md#6-build-once-read-many--freeze-it).

### Lookups feel slow / no speedup vs `Dictionary<,>`

**Likely causes and fixes:**

- **Enumerating or querying through the boxed interface or LINQ.** Keep hot loops on the concrete struct type — see [performance §2](performance.md#2-use-the-struct-fast-paths-not-the-boxed-interface).
- **Table repeatedly resizing.** Pre-size `capacity` to the known final count — [performance §3](performance.md#3-pre-size-the-table-capacity).
- **Hasher clustering your keys.** Measure with `HashQualityEvaluator` and escalate if the distribution score is poor — [performance §5](performance.md#5-choose-the-hasher-deliberately).
- **Wrong type for the key.** Use `IntDictionary` / `LongDictionary` for integer keys instead of a generic `CelerityDictionary`.
- **Benchmarking in Debug, or trusting noisy CI numbers.** Measure locally in Release — [performance §7](performance.md#7-benchmark-on-your-own-machine).

### AOT / trim warnings in my app

**Cause:** Celerity itself is `<IsAotCompatible>true</IsAotCompatible>` and emits **no** trim/AOT warnings — warnings almost always originate elsewhere in your app or another dependency.

**Fix:** Confirm the warning's source assembly. If it genuinely points at Celerity, that's a bug to [file](https://github.com/marius-bughiu/Celerity/issues). See [`docs/aot.md`](aot.md).

## Still stuck?

- Check the [FAQ](faq.md) for the "why" behind a behaviour.
- Re-read the relevant [API reference](api/collections.md) for exact method semantics and thrown exceptions.
- Search or open an issue on the [tracker](https://github.com/marius-bughiu/Celerity/issues) — include the collection type, hasher, a minimal repro, and your runtime/OS.
