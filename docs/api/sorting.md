# Sorting API reference

`Celerity.Sorting` ships **non-comparison sorts and selection over primitive keys, plus an ordinal
string sort** — the paths the BCL has no equivalent for. `Array.Sort` and `MemoryExtensions.Sort<T>`
run a scalar comparison introsort for primitive keys on every current runtime, and `Array.Sort` is
contractually in-place while a radix sort needs `O(n)` scratch, so this is a gap the BCL
structurally cannot close. For strings the BCL offers only comparison sorts, whose every comparison
dereferences two strings and re-reads the prefix they share.

```bash
dotnet add package Celerity.Sorting
```

```csharp
using Celerity.Sorting;
```

The package depends only on `Celerity.Primitives`; it does not pull in the collections.

## Contents

- [Choosing a sort](#choosing-a-sort)
- [`Sort` vs `SortWithScratch`](#sort-vs-sortwithscratch)
- [RadixSort](#radixsort)
- [CountingSort](#countingsort)
- [PartialSort](#partialsort)
- [StringSort](#stringsort)
- [Floating-point ordering](#floating-point-ordering)

## Choosing a sort

| Your workload | Use | Why |
|---|---|---|
| Sorting many primitive keys — ids, join keys, timestamps, particle indices | `RadixSort.Sort` | Four (32-bit) or eight (64-bit) counting passes with sequential reads and no comparisons, versus introsort's `O(n log n)` mispredicting branches. Stable. |
| Sorting keys that carry a parallel payload | `RadixSort.Sort(keys, values)` | Same passes, moving the payload alongside. Unlike `Array.Sort(keys, items)` it is **stable**, so equal keys keep their payload order. |
| Ranking without moving a wide payload | `RadixSort.ArgSort` | Produces the sorting permutation and leaves the keys untouched — gather the payload yourself afterwards. The BCL has no argsort. |
| Sorting values drawn from **few distinct keys** — enum ordinals, bucket ids, quantized scores | `CountingSort.Sort` | One histogram pass and one run-fill, `O(n + range)`. The keys-only forms never move an element twice, and the `byte`-keyed one allocates nothing at all. |
| Only the *k* smallest of *n* are wanted, order within them irrelevant | `PartialSort.Select` | `O(n)` introselect instead of an `O(n log n)` sort of everything. |
| Only the *k* smallest are wanted, **in order** | `PartialSort.Sort` | The same selection, then a sort of just the `k`. |
| The *k* largest of a span you **must not reorder** | `PartialSort.TopK` | `O(n log k)` bounded heap into a caller-supplied destination; the source is never written. |
| Sorting many **strings** by ordinal order — ids, URLs, file paths, composite keys like `tenant:42:user:000123` | `StringSort.Sort` | A multikey quicksort that partitions on four characters at a time packed into one integer, so a shared prefix is read once per partition level instead of once per comparison. Stable, and nulls sort first like `StringComparer.Ordinal`. |
| Strings that carry a payload, or ranking strings without moving them | `StringSort.Sort(keys, values)` / `StringSort.ArgSort` | The same sort; equal keys keep their payload order, which `Array.Sort(keys, items, comparer)` does not promise. |
| Culture-aware or case-insensitive string order | `Array.Sort` with a `StringComparer` | `StringSort` is ordinal only. With a comparer in the loop, the BCL's introsort is the right tool. |
| Fewer than a few hundred elements | `Array.Sort` | Below the crossover the histogram's fixed cost dominates. This is measured, not assumed — see the [dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/). |
| A key range as wide as (or wider than) the element count | `RadixSort` or `Array.Sort` | `CountingSort`'s histogram costs `range` regardless of `n`. Rule of thumb: `range ≲ n`. |

## `Sort` vs `SortWithScratch`

`RadixSort` and `CountingSort` expose their sorts that need pooled buffers in two forms:

- **`Sort(...)`** — the convenience form. Rents whatever scratch it needs from `ArrayPool<T>.Shared`
  and returns it before returning.
- **`SortWithScratch(...)`** — the same sort with every buffer supplied by the caller. **Allocates
  nothing**, so a hot loop rents once and reuses.

Byte keys-only `CountingSort.Sort` already allocates nothing and has no scratch twin.
`RadixSort.ArgSort` has no caller-supplied-scratch overload; for two or more keys it rents three
buffers. `PartialSort` has no `SortWithScratch` overloads: it needs no scratch and already allocates
nothing in every form, so there would be nothing for a second form to supply.

The two names are kept apart rather than overloaded on purpose. `Sort(keys, values)` has to keep meaning
key-and-payload the way `Array.Sort(keys, items)` does; a `Sort(keys, scratch)` overload would
silently win that call whenever the payload happens to have the same element type as the keys, so
sorting `int` ids alongside `int` indices would quietly overwrite the payload.

Scratch buffers must be **at least as long as the keys**, and **no two of the buffers a call writes
may share storage** — not the keys and the payload, not a scratch buffer and the span it serves, and
not a key-side buffer and a payload-side one. Every overlap between buffers of the same element
type is checked, and a violation throws `ArgumentException` before anything is written; buffers of
different element types can only overlap through a deliberate reinterpretation such as
`MemoryMarshal.Cast`, which is out of contract and not detected. The reason it is an exception rather than a
documented caveat: the radix passes ping-pong between the two buffer pairs, writing a key and a
payload element to the same index of whichever pair is the destination, so aliased buffers produce a
silently wrong answer rather than a failure. Disjoint slices of one array are fine — only genuine
overlap is rejected.
Radix sorting additionally uses ~4 KB (32-bit keys) or ~8 KB (64-bit keys) of stack for its digit
histograms, in both forms.

## RadixSort

`RadixSort` is a static class over `Span<T>`. Every method exists for six key types: `uint`, `int`,
`ulong`, `long`, `float`, `double`.

| Member | Description |
|---|---|
| `Sort(Span<TKey> keys)` | Sorts the keys ascending, renting a scratch buffer. |
| `SortWithScratch(Span<TKey> keys, Span<TKey> scratch)` | The same, with a caller-supplied buffer. Allocates nothing. |
| `Sort<TValue>(Span<TKey> keys, Span<TValue> values)` | Sorts the keys, permuting `values` to match. |
| `SortWithScratch<TValue>(Span<TKey> keys, Span<TValue> values, Span<TKey> keyScratch, Span<TValue> valueScratch)` | The same, with caller-supplied buffers. Allocates nothing. |
| `ArgSort(ReadOnlySpan<TKey> keys, Span<int> indices)` | Writes into `indices` the permutation that sorts `keys` ascending. `keys` is not modified. |

**Exceptions.** `ArgumentException` when `values` / `indices` / a scratch buffer is shorter than
`keys`, or when two of the call's buffers share storage — `values` or `indices` with
`keys`, a scratch buffer with the span it serves, or a key-side buffer with a payload-side one.

**Properties worth relying on.**

- **Stable.** Equal keys keep their input order, so the key+payload form keeps equal-keyed payloads
  in order too — which `Array.Sort(keys, items)` does not guarantee.
- **Signed keys cost nothing extra.** There is no transform pass: the last digit's prefix sum simply
  starts at the sign-bit bucket, which places negatives ahead of the rest.
- **Small key ranges cost fewer passes.** A digit that is identical across every key is skipped
  entirely, so a set of keys all below 256 costs one pass rather than four.
- **`float` / `double` pay two extra linear passes** for the order-preserving bit transform, so
  unsigned keys are the fastest shape.

```csharp
using Celerity.Sorting;

// Keys only.
int[] ids = [5, -3, 0, int.MinValue, 42];
RadixSort.Sort(ids.AsSpan());
// ids is now [int.MinValue, -3, 0, 5, 42]

// Keys carrying a payload, stable.
int[] scores = [7, 7, 3];
string[] players = ["ann", "bob", "cy"];
RadixSort.Sort<string>(scores.AsSpan(), players.AsSpan());
// scores: [3, 7, 7]   players: ["cy", "ann", "bob"]   (ann still before bob)

// A hot loop that allocates nothing after the first iteration.
int[] keyScratch = new int[ids.Length];
foreach (var batch in batches)
{
    batch.CopyTo(ids);
    RadixSort.SortWithScratch(ids.AsSpan(), keyScratch.AsSpan());
}

// Rank without moving a wide payload: sort once, then gather.
Order[] orders = LoadOrders();
long[] timestamps = orders.Select(o => o.Timestamp).ToArray();
int[] byTime = new int[orders.Length];
RadixSort.ArgSort(timestamps, byTime);
foreach (int i in byTime)
{
    Process(orders[i]);   // orders and timestamps are untouched
}
```

## CountingSort

`CountingSort` is a static class over `Span<T>` for **bounded** key ranges.

| Member | Description |
|---|---|
| `ByteRange` / `UInt16Range` | The counter counts a `byte`- / `ushort`-keyed sort uses (256 / 65,536). |
| `RequiredCounts(int min, int max)` | The counter count an `int`-keyed sort over `[min, max]` needs — for sizing the `counts` buffer. |
| `Sort(Span<byte> keys)` | Sorts `byte` keys. Allocates nothing (the 256 counters are stack-allocated). |
| `Sort(Span<ushort> keys)` | Sorts `ushort` keys, renting the counters. |
| `SortWithScratch(Span<ushort> keys, Span<int> counts)` | The same, with caller-supplied counters. |
| `Sort(Span<int> keys, int min, int max)` | Sorts `int` keys given that every key lies in `[min, max]`. |
| `SortWithScratch(Span<int> keys, int min, int max, Span<int> counts)` | The same, with caller-supplied counters. |
| `Sort<TValue>(...)` / `SortWithScratch<TValue>(...)` | The key+payload form of each of the above. **Stable.** Needs one payload-sized scratch buffer — but no key scratch, because the consumed counters double as the run-end positions the key rewrite needs. |

**Exceptions.** `ArgumentOutOfRangeException` when `max < min`, when the range needs more counters
than an array can hold, or when a key falls outside the declared `[min, max]`. `ArgumentException`
when a buffer is too short or two of the buffers a call writes share storage — `values` with
`keys`, `valueScratch` with either, or `counts` with `keys`, `values` or `valueScratch`.

```csharp
using Celerity.Sorting;

// Bucket ids: 100k values over a 256-wide range.
byte[] buckets = LoadBuckets();
CountingSort.Sort(buckets.AsSpan());

// Enum ordinals, carrying their rows, stable.
int[] states = rows.Select(r => (int)r.State).ToArray();
Row[] byState = (Row[])rows.Clone();
CountingSort.Sort<Row>(states.AsSpan(), byState.AsSpan(), 0, MaxStateOrdinal);

// A hot loop that allocates nothing.
int[] counts = new int[CountingSort.RequiredCounts(0, 1023)];
foreach (var batch in batches)
{
    CountingSort.SortWithScratch(batch.Span, 0, 1023, counts);
}
```

## PartialSort

`PartialSort` is a static class over `Span<T>`. Each entry point comes in two forms: one constrained
to `IComparable<T>` for the natural order, and one taking a **`struct` comparer** as a type parameter
so the JIT devirtualizes and inlines every comparison — the same zero-cost-abstraction rule the
struct hashers follow. Nulls sort first under the natural order, matching `Comparer<T>.Default`.

| Member | Description |
|---|---|
| `Select<T>(Span<T> keys, int count)` | Rearranges so the first `count` elements are the `count` smallest, in unspecified order. |
| `Select<T, TComparer>(Span<T> keys, int count, TComparer comparer)` | The same under a custom order. |
| `Sort<T>(Span<T> keys, int count)` | The same, but the prefix is left **in ascending order**. |
| `Sort<T, TComparer>(Span<T> keys, int count, TComparer comparer)` | The same under a custom order. |
| `TopK<T>(ReadOnlySpan<T> source, Span<T> destination)` | Copies the largest `destination.Length` elements into `destination` in **descending** order without modifying `source`. Returns the number written. |
| `TopK<T, TComparer>(ReadOnlySpan<T> source, Span<T> destination, TComparer comparer)` | The same under a custom order — pass a reversing comparer to take the *smallest*. |

**Exceptions.** `ArgumentOutOfRangeException` when `count` is negative or greater than `keys.Length`.
`ArgumentException` when a `TopK` `destination` shares storage with its `source`. As everywhere else
in the package, only genuine overlap is rejected — two disjoint slices of one array are fine.

**Properties worth relying on.**

- **Duplicate-heavy input stays linear.** The partition is three-way (Dutch national flag), so an
  all-equal span finishes in one pass rather than quadratically.
- **Adversarial input is bounded.** `Select` is an *intro*select: past a depth budget proportional to
  `log n` it falls back to an in-place heap sort of the remaining range, capping the worst case at
  `O(n log n)`. The same budget is what stops an inconsistent comparer from spinning forever.
- **Nothing here allocates**, in any form. That is why the internal ordering is insertion sort and
  heap sort rather than `MemoryExtensions.Sort<T, TComparer>`, which reaches the BCL's sort through
  an `IComparer<T>`-typed helper and so boxes a `struct` comparer on every call. If you want
  introsort's constant factor over a whole span, call the BCL directly.
- **Not stable**, in any form.
- **`TopK` never writes to `source`** and allocates nothing — the destination *is* the heap, which
  is why a `destination` overlapping `source` is rejected rather than silently answered wrongly.

```csharp
using Celerity.Sorting;

// The 10 fastest of a million, order within them irrelevant.
long[] durations = LoadDurations();
PartialSort.Select(durations.AsSpan(), 10);
// durations[..10] holds the 10 smallest

// The 10 fastest, in order.
PartialSort.Sort(durations.AsSpan(), 10);

// The 10 worst offenders, leaving the source alone.
int[] worst = new int[10];
int written = PartialSort.TopK<int>(latencies, worst);
// worst[0] is the largest, worst[written - 1] the tenth largest

// A custom order via a struct comparer, devirtualized.
readonly struct ByLength : IComparer<string>
{
    public int Compare(string? x, string? y) => (x?.Length ?? 0).CompareTo(y?.Length ?? 0);
}

string[] longest = new string[5];
PartialSort.TopK<string, ByLength>(words, longest, default);
```

## StringSort

`StringSort` is a static class over spans of `string`. It orders by **UTF-16 code unit**, exactly as
`StringComparer.Ordinal` and `string.CompareOrdinal` do: a proper prefix sorts before every string
that extends it, `"B"` sorts before `"a"`, and nothing is culture-aware.

| Member | Description |
|---|---|
| `Sort(Span<string> keys)` | Sorts the keys ascending, in place. |
| `Sort<TValue>(Span<string> keys, Span<TValue> values)` | Sorts the keys, permuting `values` to match. |
| `ArgSort(ReadOnlySpan<string> keys, Span<int> indices)` | Writes into `indices` the permutation that sorts `keys` ascending. `keys` is not modified. |

**Exceptions.** `ArgumentException` when `values` or `indices` is shorter than `keys`, or when a
`values` span of strings shares storage with `keys`. Disjoint slices of one array are fine.

**How it works.** Each key becomes a 16-byte entry: the next four characters packed into one
`ulong` (first character in the high 16 bits, a position past the end packing as zero), and its
input position. A three-way partition on that integer splits a range into less, equal and greater;
the equal part moves four characters deeper and is re-packed. Comparisons are integer compares, and
each string is dereferenced once per four characters of the prefix that tells it apart, where a
comparison sort re-reads the whole shared prefix on every one of its `O(n log n)` comparisons.

**Properties worth relying on.**

- **Stable**, in every form. Ties are broken by input position, so equal strings keep their input
  order, the key+payload form keeps equal-keyed payloads in order, and `ArgSort` lists the indices of
  equal keys ascending. `Array.Sort(keys, items, comparer)` guarantees neither.
- **Nulls are allowed and sort first**, before the empty string, matching `StringComparer.Ordinal`.
  The parameters are annotated non-nullable so a `string[]` call site needs no suppression; pass a
  `string?[]` with the null-forgiving operator, `StringSort.Sort(keys.AsSpan()!)`.
- **U+0000 is an ordinary character.** A position past the end of a string and a literal U+0000 pack
  the same, so when a window's last lane is zero the group is split by how far each string actually
  reaches before going deeper. `"a" < "a\0" < "a\0\0"` comes out right.
- **No quadratic case.** Partitioning carries an introsort-style depth budget (two levels per halving)
  that falls back to an in-place heap sort, and the stack is `O(log n)` deep however long the shared
  prefixes are: the loop always continues with the largest part and recurses only into parts of at
  most half the range.
- **Allocation.** 16 bytes of working state per key, stack-allocated up to 256 keys and rented from
  `ArrayPool<T>` above that; a run of more than 32 identical strings also rents 8 bytes per string
  to radix-sort their positions. `Sort` gathers the result from a pooled copy of the keys (and of
  the payload), cleared before it is returned to the pool. Once the pool is warm, a call does not
  allocate. There is no `SortWithScratch` form, because the working state is an internal entry type.

**Where it does not win.** Input that is exactly in order is recognized by one linear scan and left
alone, but input that is only *nearly* in order pays for that scan, then sorts in full, and loses to
the BCL's introsort by up to 16%. Mostly-duplicate input gains less than distinct
keys, because a run of equal strings has to be read to its end before it is known to be equal.
Measured figures for both are below and on the
[dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/).

Measured with BenchmarkDotNet against `Array.Sort(keys, StringComparer.Ordinal)` (and the two-array
`Array.Sort(keys, items, StringComparer.Ordinal)` for the payload and argsort rows), on a development
machine (Apple Silicon, .NET 10 runtime). Higher is better for `StringSort`; the CI figures on the
dashboard supersede these.

| Shape | 100 | 1,000 | 100,000 | 1,000,000 |
|---|---|---|---|---|
| Random alphanumerics, 8–20 chars | 1.72x | 1.93x | 2.44x | 2.36x |
| Shared prefix (`tenant:42:user:%09d`) | 2.01x | 2.53x | 2.90x | 2.84x |
| 100 distinct values, repeated | 1.39x | 1.32x | 1.37x | 1.43x |
| Already in order (one scan, then return) | 5.73x | 8.38x | 9.54x | 7.54x |
| ⚠️ In order but for the last two keys | 0.94x | **0.88x** | 1.04x | **0.84x** |
| Random keys + `int` payload | 1.48x | 1.79x | 2.56x | 2.48x |
| `ArgSort` of random keys | 2.00x | 2.23x | 2.76x | 2.66x |

The "already in order" row measures the ordered-input check, not the sort: the input is recognized
in one linear pass and left alone. The row below it is the honest sorted-input case. That input
fails the check only at its last key and is then sorted in full, and there introsort's cheapness on
almost-ordered data wins by up to 16%. Once the pool is warm, every row allocates nothing.

```csharp
using Celerity.Sorting;

// Keys only: ordinal, so upper case sorts before lower case.
string[] names = ["pear", "Apple", "apple", "fig"];
StringSort.Sort(names.AsSpan());
// names is now ["Apple", "apple", "fig", "pear"]

// Keys carrying a payload, stable.
string[] tags = ["db", "api", "db", "api"];
int[] requestIds = [10, 11, 12, 13];
StringSort.Sort(tags.AsSpan(), requestIds.AsSpan());
// tags: ["api", "api", "db", "db"]   requestIds: [11, 13, 10, 12]   (input order kept within ties)

// Rank without moving a wide payload: sort once, then gather.
Document[] docs = LoadDocuments();
string[] paths = docs.Select(d => d.Path).ToArray();
int[] byPath = new int[docs.Length];
StringSort.ArgSort(paths, byPath);
foreach (int i in byPath)
{
    Emit(docs[i]);   // docs and paths are untouched
}

// A string?[] goes in with the null-forgiving operator; nulls sort first.
string?[] maybe = ["b", null, "a"];
StringSort.Sort(maybe.AsSpan()!);
// maybe is now [null, "a", "b"]
```

## Floating-point ordering

`RadixSort` orders `float` and `double` keys by an order-preserving transform of their bit patterns.
Two consequences diverge from `Array.Sort`, both deliberate and both documented on the type:

- **`NaN` keys are ordered by their sign bit** — sign-bit-set NaNs sort before every number, the rest
  after every number — where `Array.Sort` moves *all* NaNs to the front. (Note that .NET's own
  `float.NaN` / `double.NaN` constants have the sign bit **set**, so they land at the front.)
- **`-0.0` sorts strictly before `+0.0`**, where the BCL comparer calls them equal and leaves their
  relative order to the partitioning.

If you need the BCL's placement, filter or normalize NaNs before sorting. Everything else — including
both infinities and subnormals — matches `Array.Sort` exactly, and is reconciled against it by a
differential fuzz target on every nightly soak.
