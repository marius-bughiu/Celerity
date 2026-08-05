# Celerity.Sorting

Non-comparison sorts and selection over primitive keys. Part of the
[Celerity](https://github.com/marius-bughiu/Celerity) family of high-performance
.NET libraries.

`Array.Sort` / `MemoryExtensions.Sort<T>` route through a **scalar comparison
introsort** for primitive keys on every current runtime — there is no radix,
counting, or selection path anywhere in the BCL. And the BCL structurally cannot
add one: `Array.Sort` is contractually in-place, while a radix sort needs `O(n)`
scratch. That is exactly the flexibility-for-speed trade Celerity exists to make.

## What's in the box

- **`RadixSort`** — LSD radix over `uint` / `int` / `ulong` / `long` / `float` /
  `double`: four (32-bit) or eight (64-bit) counting passes with purely
  sequential reads, **no comparisons and no data-dependent branches**. Keys
  alone, keys with a parallel payload, or `ArgSort` — an index permutation that
  ranks without moving a wide payload. **Stable.**
- **`CountingSort`** — bounded key ranges (`byte`, `ushort`, or `int` over a
  declared `[min, max]`): one histogram pass and one run-fill, `O(n + range)`,
  for the shape that enum ordinals, bucket ids and quantized scores take. The
  keys-only forms never move an element twice.
- **`PartialSort`** — `Select` / `Sort` are an `O(n)` in-place introselect for
  the *k* smallest; `TopK` is an `O(n log k)` bounded heap over a read-only span.

`RadixSort` and `CountingSort` pair every entry point with a **`SortWithScratch`
twin that allocates nothing**, so a hot loop supplies its buffers once instead of
renting per call. (`Sort` is the convenience form and rents from `ArrayPool<T>`;
the two names are kept apart so `Sort(keys, values)` always means key-and-payload,
the way `Array.Sort(keys, items)` does.) `PartialSort` has no scratch overloads —
it needs no scratch and allocates nothing in any form.

## Where it wins, and where it does not

Celerity documents its tradeoffs rather than claiming a blanket win:

- **`RadixSort` loses below a few hundred elements** — the fixed cost of the
  histogram pass dominates. Use `Array.Sort` for small spans; the crossover is a
  measured number on the
  [benchmark dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/),
  which sweeps from 100 to 1,000,000 elements precisely so it can be read off.
- **`CountingSort` loses once `range` approaches `n`** — the histogram costs
  `range` no matter how few elements there are. Rule of thumb: `range ≲ n`.
- **`PartialSort` is not asymptotically better than LINQ.** `OrderBy().Take(k)`
  has applied its own partial-sort optimization since .NET 6. The win is that
  this works on a span in place, allocates nothing, boxes no comparer, and
  materializes no intermediate sequence.

## Two deliberate floating-point divergences

`RadixSort` orders `float` / `double` keys by their (transformed) bit pattern, so:

- **`NaN` keys sort by sign bit** — sign-bit-set NaNs before every number, the
  rest after — where `Array.Sort` moves every NaN to the front.
- **`-0.0` sorts before `+0.0`**, where the BCL comparer calls them equal.

Filter or normalize NaNs first if you need the BCL's placement.

## Quick start

```bash
dotnet add package Celerity.Sorting
```

```csharp
using Celerity.Sorting;

// Ten million ids, sorted with four branch-free passes.
int[] ids = LoadIds();
RadixSort.Sort(ids.AsSpan());

// Sort ids and carry a parallel payload, allocating nothing in the hot loop.
int[] keyScratch = new int[ids.Length];
string[] valueScratch = new string[ids.Length];
RadixSort.SortWithScratch(ids.AsSpan(), names.AsSpan(), keyScratch, valueScratch);

// Rank without moving a wide payload.
int[] order = new int[ids.Length];
RadixSort.ArgSort(ids, order);

// The 10 worst offenders out of a million, without touching the source.
int[] worst = new int[10];
PartialSort.TopK<int>(latencies, worst);
```

See the [sorting API reference](https://github.com/marius-bughiu/Celerity/blob/main/docs/api/sorting.md)
for full docs and runnable examples.

## License

MIT
