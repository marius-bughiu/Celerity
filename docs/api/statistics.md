# Statistics API reference

`Celerity.Statistics` ships **streaming summary statistics in bounded memory** — quantiles,
sampling and moments over a stream you cannot hold. The BCL summarizes a sequence with `Average`,
`Sum`, `Min` and `Max` and stops there: there is no quantile type, no sampler, no variance, no
higher moment, and no accumulator that can be fed one value at a time. So summarizing a stream
means keeping the stream, which is the one thing a stream will not let you do.

```bash
dotnet add package Celerity.Statistics
```

```csharp
using Celerity.Statistics;
```

The package depends only on `Celerity.Primitives` (for the seedable struct PRNGs); it does not pull
in the collections.

## Contents

- [Choosing a summary](#choosing-a-summary)
- [DDSketch](#ddsketch)
- [ReservoirSampler](#reservoirsampler)
- [RunningStatistics](#runningstatistics)
- [Where these lose](#where-these-lose)

## Choosing a summary

| Your workload | Use | Why |
|---|---|---|
| p50 / p90 / p99 latency over a stream that never ends | `DDSketch` | A relative error bound at every quantile, in memory proportional to the log of the value range rather than to the sample count. |
| Percentiles per shard, combined into a global one | `DDSketch.Merge` | Two sketches of the same accuracy merge exactly, without re-reading either input. |
| An exact median of data you already hold and will not add to | `Array.Sort` + index | Nothing beats a pre-sorted array indexed in `O(1)`. The sketch is for the case where you cannot keep the data or it keeps changing. |
| A bounded sample of a stream of unknown length — log lines, trace spans, request bodies | `ReservoirSampler<T>` | `O(k)` memory and `O(k · log(n / k))` random draws, exactly uniform, and it never needs to know `n`. |
| Mean and standard deviation of a stream, or of one bucket among many | `RunningStatistics` | One pass, no allocation, numerically stable, and a `struct` so per-bucket accumulators live inline. |
| Skewness / kurtosis | `RunningStatistics` | Welford extended to the fourth moment (Terriberry). There is no BCL equivalent at all. |
| Mean of a sequence you can enumerate twice and that fits in memory | `Enumerable.Average` | If the two-pass shape is available and the magnitudes are ordinary, LINQ is fine. |

## DDSketch

A mergeable quantile sketch with a **relative**-error guarantee.

```csharp
var latencies = new DDSketch(relativeAccuracy: 0.01);

foreach (double ms in requestLatencies)
{
    latencies.Add(ms);
}

Console.WriteLine($"p50 {latencies.GetQuantile(0.5):F2} ms");
Console.WriteLine($"p99 {latencies.GetQuantile(0.99):F2} ms");
```

### How it works

Each value is mapped to bucket `ceil(log_γ(v))` of a geometric ladder with `γ = (1 + α) / (1 − α)`,
and the sketch counts how many values landed in each bucket. Because the ladder is geometric rather
than uniform, every reported quantile is within a **relative** `α` of the true value:

```
|reported − actual| ≤ α · |actual|
```

That is the guarantee latency work wants — 1% of 10 ms and 1% of 10 s, not a fixed number of
milliseconds that is meaningless at one end of the range and useless at the other. It holds for any
quantile of any distribution, and unlike a t-digest it holds at the median as strongly as at the
tail. At the default 1% accuracy a bucket spans 2%, so covering nanoseconds to hours takes about
1,500 buckets — inside the default 2,048 budget.

### Constructors

| Constructor | Notes |
|---|---|
| `DDSketch()` | 1% relative accuracy, 2048 bins per ladder. |
| `DDSketch(double relativeAccuracy)` | Accuracy in `[1e-6, 1)`; the default bin budget. |
| `DDSketch(double relativeAccuracy, int maxBins)` | `maxBins` in `[1, 2^26]`. |

Below `1e-6` the bucket index of an extreme `double` no longer fits in an `int`, so the constructor
rejects it rather than overflowing silently.

### Adding

| Member | Notes |
|---|---|
| `Add(double value)` | Finite values only; **zero and negatives are accepted**. |
| `Add(double value, long count)` | The same, with a multiplicity. `count` must be positive. |
| `Add(ReadOnlySpan<double> values)` | Every value in the span. |

Negatives go into a mirrored second ladder and zero into its own counter, because `log` has nothing
to say about either. Only `NaN` and the infinities are rejected, with an
`ArgumentOutOfRangeException`.

### Querying

| Member | Returns |
|---|---|
| `GetQuantile(double quantile)` | The bucketed value at that quantile, or `NaN` if the sketch is empty. |
| `GetQuantiles(ReadOnlySpan<double> quantiles, Span<double> destination)` | Several at once, into a caller-owned span. Allocates nothing. |
| `Count` / `Sum` / `Average` / `Min` / `Max` | **Exact**, not subject to `α`. |
| `BinCount` | Live buckets across both ladders — the memory footprint, in buckets. |
| `HasCollapsed` | Whether the bin budget has bound. See below. |
| `RelativeAccuracy` / `MaxBins` | What the sketch was built with. |

`GetQuantile(0)` and `GetQuantile(1)` return the **bucketed** extremes. Use `Min` / `Max` when the
exact ones are wanted.

### The bin budget, and when the guarantee stops holding

A stream spanning an unexpectedly wide range would allocate a bucket per decade forever, so a bin
budget bounds each ladder. When it is exhausted the **lowest** buckets collapse together — the
choice that protects the high quantiles people actually query — and `HasCollapsed` turns `true`, at
which point the `α` guarantee no longer holds for values in the collapsed low tail.

The range a budget covers is proportional to `maxBins × α`, so it shrinks as the accuracy tightens:
2048 bins span about 17 decades at the default 1% accuracy and under two at 0.1%. If you tighten
`relativeAccuracy`, raise `maxBins` with it, and check `HasCollapsed` rather than assuming:

```csharp
var precise = new DDSketch(relativeAccuracy: 0.001, maxBins: 16_384);
// ... add values ...

if (precise.HasCollapsed)
{
    // The low tail is no longer accurate to 0.1%; widen the budget.
}
```

### Merging

```csharp
var global = new DDSketch(0.01);

foreach (DDSketch shard in perShardSketches)
{
    global.Merge(shard);
}
```

`Merge` requires the same `RelativeAccuracy` on both sides — the ladders must line up — and throws
`ArgumentException` otherwise. The two may differ in `MaxBins`; the result keeps the target's
budget. The operand is left unchanged, and merging an empty sketch is a no-op.

## ReservoirSampler

A fixed-size uniform sample of a stream whose length is not known in advance.

```csharp
var sample = new ReservoirSampler<string>(capacity: 1_000, seed: 42);

foreach (string line in logLines)
{
    sample.Add(line);
}

foreach (string retained in sample.Sample)
{
    Console.WriteLine(retained);
}
```

After any number of items, each of the `n` seen has probability `k / n` of being one of the `k`
retained. The sampler never needs to know `n` in advance and never stores more than `k` items.

### Algorithm L, not Algorithm R

The textbook reservoir sampler (Algorithm R) draws one random number per item. This is Li's
**Algorithm L**, which draws a geometric *skip* and jumps over the items that cannot win, so it
makes `O(k · log(n / k))` draws over the whole stream instead of `n`. Both are exactly uniform — the
difference is only the cost. The skip counter is maintained inside `Add`, so the sampler is still
fed one item at a time and the caller does not have to be able to seek forward in its source.

### Surface

| Member | Notes |
|---|---|
| `ReservoirSampler<T>(int capacity, ulong seed)` | The convenience form, on a seeded `Pcg32`. |
| `ReservoirSampler<T, TRng>(int capacity, TRng rng)` | Any `struct` `IRandomSource` — `Xoshiro256StarStar`, `WyRand`, … |
| `Add(T item)` | Returns whether the item was retained. |
| `Add(ReadOnlySpan<T> items)` | Every item in the span, in order. |
| `Sample` | A `ReadOnlySpan<T>` over the sampler's own storage — no copy, no allocation. |
| `Count` / `Capacity` / `TotalSeen` / `IsFull` | Retained, budget, stream length, and whether the reservoir filled. |
| `this[int index]`, `GetEnumerator()` | The type implements `IReadOnlyList<T>`. |
| `Clear()` | Discards the sample and the stream position, leaving the generator where it is. |

The retained items are in arbitrary order, not stream order. The `Sample` span is invalidated by any
subsequent `Add` or `Clear`.

Sampling is reproducible: the same seed and the same stream produce the same sample on every OS,
architecture and runtime.

There is no `IEnumerable<T>` overload of `Add`, because an overload set holding both a span and a
sequence makes an array argument ambiguous under C# 12. A sequence is a one-line `foreach`.

### Why there is no `Merge`

Combining two reservoirs into one that is still uniform over the union requires drawing how many of
the `k` output slots come from each side from a hypergeometric distribution over the two stream
lengths. Replaying one side's retained items into the other over-weights the shorter stream. Rather
than ship a subtly biased merge, the sampler ships without one: sample the shards separately, or
feed one sampler.

## RunningStatistics

Single-pass, allocation-free moments.

```csharp
var stats = new RunningStatistics();

foreach (double value in stream)
{
    stats.Add(value);
}

Console.WriteLine($"{stats.Count} samples, mean {stats.Mean:F2} ± {stats.StandardDeviation:F2}");
Console.WriteLine($"range {stats.Min:F2} … {stats.Max:F2}, skew {stats.Skewness:F3}");
```

### Why not `sum` and `sumOfSquares`

A LINQ variance is a **two-pass** shape — average the sequence, then average the squared deviations
— which needs the sequence to be re-enumerable and so cannot summarize a stream. The one-pass
alternative everyone writes instead, accumulating `sum` and `sumOfSquares` and subtracting, is
numerically catastrophic when the mean is large relative to the spread: the two terms of
`sumOfSquares / n - mean²` agree in their leading digits and cancel, so the answer is built entirely
out of the digits that rounding already destroyed. It can and does return a negative variance.

At `1e10 ± 6` the shortcut misses a true variance of 30 by hundreds. Welford's recurrence — extended
here to the fourth moment (Terriberry) — updates the mean and the central moments directly and never
forms a large intermediate to subtract away.

### Surface

| Member | Notes |
|---|---|
| `Count` / `Sum` / `Mean` / `Min` / `Max` | `Sum` is `Mean × Count`. |
| `Variance` / `StandardDeviation` | The unbiased `n − 1` estimators. |
| `PopulationVariance` / `PopulationStandardDeviation` | The `n` denominator forms. |
| `Skewness` / `Kurtosis` | The biased `g₁` / `g₂` estimators; kurtosis is **excess** (a normal scores 0). |
| `Add(double)` / `Add(ReadOnlySpan<double>)` | One value, or a span in order. |
| `Merge(in RunningStatistics)` / `Combine(in, in)` | Chan's parallel formulas. |
| `Clear()` | Back to the empty state. |

Every statistic that is undefined for the number of values seen returns `NaN` rather than throwing:
`Mean` / `Min` / `Max` on an empty accumulator, `Variance` below two values, `Skewness` below three,
`Kurtosis` below four, and both shape statistics when every value was identical.

**The domain is the finite doubles.** `Add` rejects `NaN` and the infinities with an
`ArgumentOutOfRangeException`, as `DDSketch.Add` does. A recurrence over deltas has no good answer
for them: `NaN` poisons the moments while leaving the extrema untouched, because a comparison
against `NaN` is false either way; and `∞ − ∞` is `NaN`, so a second infinity destroys the mean a
first one survived, and merging an infinite accumulator gives a different answer depending on which
side it is on. Rejecting at the boundary turns a silently poisoned statistic into a stack trace at
the value that caused it.

`Add` also throws `InvalidOperationException` once the accumulator holds `long.MaxValue` values, and
`Merge` throws `ArgumentException` when the combined count would overflow — reachable in sixty-odd
calls, because merging an accumulator into itself doubles its count.

### It is a mutable struct, deliberately

That makes `default` a valid empty accumulator, lets an array of per-bucket statistics live inline
with zero allocation, and keeps the add path free of an indirection:

```csharp
var perEndpoint = new RunningStatistics[endpointCount];
perEndpoint[endpoint].Add(latency);   // an array element is already a ref
```

The usual caveat applies: a copy is an independent snapshot, so `list[i].Add(x)` updates a temporary
and is lost. For a dictionary value, accumulate through
`CollectionsMarshal.GetValueRefOrAddDefault`:

```csharp
ref RunningStatistics slot = ref CollectionsMarshal.GetValueRefOrAddDefault(byEndpoint, endpoint, out _);
slot.Add(latency);
```

## Where these lose

Celerity documents its tradeoffs rather than claiming a blanket win. All three of these are on the
[benchmark dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/), including the arms they
lose:

- **`DDSketch.Add` is slower than a `List<double>` append**, and should be — appending is a bounds
  check and a store, and the sketch pays a `log()` and a `ceil()` on top of its own store.
- **`DDSketch` loses badly to a pre-sorted array** when nothing has changed since the last query.
  If the data is static and fits in memory, sort it once and index. That arm is charted
  (`QueryPresorted`) rather than left out.
- **`DDSketch` is not exact**, and its guarantee is relative. Near zero a relative error is a very
  small absolute one, and far from zero a large one. That is the right shape for latencies and
  sizes, and the wrong shape for a value that can be any magnitude with equal significance.
- **`ReservoirSampler` has no `Merge`**, for the reason above.
- **`RunningStatistics` wins on accuracy, not speed.** The `sum` / `sumOfSquares` shortcut is a
  shade cheaper per value. It is also the thing that returns a negative variance.

## See also

- [Collections API](collections.md) — including the sketches (`HyperLogLog`, `CountMinSketch`,
  `TopKSketch`) that summarize a stream's *cardinality* and *frequencies* rather than its
  distribution.
- [Utilities API](utilities.md) — the seedable struct PRNGs the sampler is parameterized on.
- [Testing & coverage](../testing.md) — the differential and fuzz targets that reconcile each of
  these against an exact oracle.
