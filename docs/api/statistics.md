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
- [RollingQuantile](#rollingquantile)
- [Where these lose](#where-these-lose)

## Choosing a summary

| Your workload | Use | Why |
|---|---|---|
| p50 / p90 / p99 latency over a stream that never ends | `DDSketch` | A relative error bound at every quantile, in memory proportional to the log of the value range rather than to the sample count. |
| Percentiles per shard, combined into a global one | `DDSketch.Merge` | Two sketches of the same accuracy merge without re-reading either input — bucket-exactly, unless an operand has already collapsed. |
| An exact median of data you already hold and will not add to | `Array.Sort` + index | Nothing beats a pre-sorted array indexed in `O(1)`. The sketch is for the case where you cannot keep the data or it keeps changing. |
| A rolling median or p99 over the last *w* values — a median filter, a "spike over the recent median" alarm | `RollingQuantile<T>` | Exact, `O(log w)` per value and `O(1)` to read. It holds the window, which is what makes it exact; `DDSketch` cannot forget old values. |
| A bounded sample of a stream of unknown length — log lines, trace spans, request bodies | `ReservoirSampler<T>` | `O(k)` memory and `O(k · log(n / k))` random draws, uniform to the precision of the generator, and it never needs to know `n`. |
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
| `Add(double value, long count)` | The same, with a multiplicity. `count` must be positive, and must not push `Count` past `long.MaxValue`. |
| `Add(ReadOnlySpan<double> values)` | Every value in the span. |

Negatives go into a mirrored second ladder and zero into its own counter, because `log` has nothing
to say about either. The only values rejected are `NaN` and the infinities, with an
`ArgumentOutOfRangeException` — which every `Add` form also throws when the addition would overflow
`Count`. The span form has already added the values before the one that throws.

### Querying

| Member | Returns |
|---|---|
| `GetQuantile(double quantile)` | The bucketed value at that quantile, or `NaN` if the sketch is empty. |
| `GetQuantiles(ReadOnlySpan<double> quantiles, Span<double> destination)` | Several at once, into a caller-owned span. Allocates nothing. The destination must not overlap the quantile list — it is written while the list is still being read — though disjoint slices of one array are fine. |
| `Count` / `Sum` / `Average` / `Min` / `Max` | Tracked directly rather than read off the buckets, so none of them is subject to `α`. `Count`, `Min` and `Max` are exact; `Sum` is accumulated with Neumaier compensation, which makes it correct to the last bit or two rather than exact, and is `±∞` once the *running* total leaves the range — which can happen before the final sum would have, since compensation cannot recover an already-infinite total. `Average` derives from `Sum`. |
| `BinCount` | Live buckets across both ladders — the memory footprint, in buckets. |
| `HasCollapsed` | Whether the bin budget has bound. See below. |
| `RelativeAccuracy` / `MaxBins` | What the sketch was built with. |

`GetQuantile(0)` and `GetQuantile(1)` return the **bucketed** extremes. Use `Min` / `Max` when the
exact ones are wanted.

### The bin budget, and when the guarantee stops holding

A stream spanning an unexpectedly wide range would allocate a bucket per decade forever, so a bin
budget bounds each ladder. When it is exhausted the **lowest** buckets collapse together — the low
end of *that ladder*, which is where a geometric ladder packs its buckets most densely and so loses
the least by folding them — and `HasCollapsed` turns `true`, at which point the `α` guarantee no
longer holds for any quantile resolving to a collapsed bucket.

That is **not** necessarily a low quantile of the stream. The positive and negative ladders are
budgeted independently, so if most of the stream is negative, a collapsed bucket in the positive
ladder sits at a high global rank — p99 can land in it. `HasCollapsed` tells you the sketch has lost
resolution somewhere; it does not tell you which quantiles are affected, so treat it as a signal to
widen the budget rather than as a bound on where to trust the answer.

The range a budget covers is proportional to `maxBins × α`, so it shrinks as the accuracy tightens:
2048 bins span about 17 decades at the default 1% accuracy and under two at 0.1%. If you tighten
`relativeAccuracy`, raise `maxBins` with it, and check `HasCollapsed` rather than assuming:

```csharp
var precise = new DDSketch(relativeAccuracy: 0.001, maxBins: 16_384);
// ... add values ...

if (precise.HasCollapsed)
{
    // Some buckets were folded together; quantiles resolving to them are no longer
    // accurate to 0.1%. Widen the budget.
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
`ArgumentException` otherwise, or when the combined `Count` would overflow. The operand is left
unchanged, and merging an empty sketch is a no-op.

Bucket counts add, so the result is identical to a sketch that consumed both streams —
**as long as neither operand has collapsed**. A collapsed operand arrives carrying only the buckets
that survived, and no merge can unfold them however wide the target's budget is; `HasCollapsed`
travels with the counts so the result says so. The two may differ in `MaxBins` and the result keeps
the target's budget, but a shard fleet that wants replay-equivalent merges should give every shard
the same budget as well as the same accuracy.

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
makes `O(k · log(n / k))` draws over the whole stream instead of `n`. As algorithms both are exactly
uniform — the difference is only the cost.

**Uniform to the precision of the generator, not beyond it.** The retention probabilities are exact
in real arithmetic, but the skip is drawn by comparing logarithms of draws taken from a 2^52-point
grid, so each decision lands within about `2^-53` of its true probability. On a two-item stream at
capacity 1, for instance, the second item is retained exactly when `U₁ + U₂ > 1`; the tie at `1` has
nonzero mass on a discrete grid, so the realized probability differs from `½` by about `2^-53`.
Which way that tie falls is decided by the last bit of the library logarithms and is not fixed
across runtimes — the same reason this type promises no cross-platform reproducibility. Algorithm R
would have none of this, its decision being an exact integer comparison, but it costs a draw per
item, which is the entire reason this type is Algorithm L. The deviation sits around fourteen orders
of magnitude below the sampling noise a k-item sample already carries. The skip counter is maintained inside `Add`, so the sampler is still
fed one item at a time and the caller does not have to be able to seek forward in its source.

### Surface

| Member | Notes |
|---|---|
| `ReservoirSampler<T>(int capacity, ulong seed)` | The convenience form, on a seeded `Pcg32`. |
| `ReservoirSampler<T, TRng>(int capacity, TRng rng)` | Any `struct` `IRandomSource` — `Xoshiro256StarStar`, `WyRand`, … |
| `Add(T item)` | Returns whether the item was retained. |
| `Add(ReadOnlySpan<T> items)` | Every item in the span, in order. |
| `Sample` | A `ReadOnlySpan<T>` over the sampler's own storage — no copy, no allocation. Prefer it to enumerating, which allocates an iterator. |
| `Count` / `Capacity` / `TotalSeen` / `IsFull` | Retained, budget, stream length, and whether the reservoir filled. |
| `this[int index]`, `GetEnumerator()` | The type implements `IReadOnlyList<T>`. |
| `Clear()` | Discards the sample and the stream position, leaving the generator where it is. |

The retained items are in arbitrary order, not stream order. The `Sample` span is invalidated by any
subsequent `Add` or `Clear`, and so is an enumerator — which enforces it: its next `MoveNext` throws
`InvalidOperationException`, as every mutable Celerity collection's does. That includes an `Add` that
discards its item, so whether a loop that adds while it reads fails does not depend on the draw.

Sampling is seeded: the same seed and the same stream produce the same sample on the same runtime
and platform. It is deliberately **not** promised byte-identical *across* platforms the way
`Celerity.Ring` is — the skip arithmetic goes through `Math.Log` and `Math.Exp`, whose last bit .NET
does not contractually fix across runtime versions or architectures, and one ulp either side of a
`Math.Floor` boundary changes the next replacement index and every draw after it. The PRNG is
exactly portable; the transcendentals on top of it are not.

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

Undefined statistics return `NaN` rather than throwing: `Mean` / `Min` / `Max` /
`PopulationVariance` / `PopulationStandardDeviation` on an empty accumulator, `Variance` /
`StandardDeviation` below two values, `Skewness` below three, `Kurtosis` below four, and both shape
statistics when the stream has zero variance.

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

## RollingQuantile

The exact quantile of the most recent `w` values of a stream — a rolling median, p95 or any other
fraction — updated in `O(log w)` per value and read in `O(1)`.

```csharp
// The median of the last 1,001 latency samples, updated as each one arrives.
var median = new RollingQuantile<double>(windowSize: 1_001, quantile: 0.5);

foreach (double latency in samples)
{
    median.Add(latency);
    if (median.IsFull && latency > 3 * median.Value)
    {
        Console.WriteLine($"spike: {latency}");
    }
}
```

Each `Add` appends to a fixed-size sliding window, evicting the oldest value once the window is
full. `Value` is the window's element of zero-based rank `⌊q · (n − 1)⌋` in sorted order, where `q`
is the quantile and `n` the window's count. NumPy calls this the `"lower"` method. A median over an
even window is therefore the lower of the two middle values, `q = 0` is the window minimum, and
`q = 1` the maximum. The answer is always a value from the window, never an interpolation or an
estimate.

### How it works

The window lives in two heaps that meet at the quantile: a max-heap of the values at or below it,
whose root is the answer, and a min-heap of the values above. A side table records, for every
window slot, where that slot's value currently sits in its heap. When the window is full, the new
value takes the expiring value's slot and its heap position, so nothing is searched for. If the new
value belongs on the other side, it trades places with that side's root instead. Either way an
`Add` costs at most two heap sifts and the heaps keep their sizes. Both heaps share one array of
`w` entries, so the footprint is `w` values plus `w` positions, whatever the quantile.

The BCL has no windowed quantile. The careful hand-roll keeps the window sorted in an array beside
a `Queue<T>` of arrival order, binary-searches the expiring and the new value, and shifts the
values between them by one place. That is a memmove, linear in the window, so it is quick for a
small window and slow for a large one. The more obvious `List<T>.RemoveAt` + `Insert` moves the
tail twice, and re-sorting per read is worse again. Against that careful hand-roll, sliding a full
window and reading the median after every sample measured **2.5× faster at 1,000 values, 5.0× at
10,000 and 32× at 100,000** on a local run (Apple M-series, .NET 10). Neither side allocates. The
CI-tracked numbers are on the [dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/).

### Surface

| Member | Notes |
|---|---|
| `RollingQuantile<T>(int windowSize, double quantile)` | `windowSize ≥ 1`; `quantile` in `[0, 1]`. `NaN` is rejected. `T : IComparable<T>`. |
| `Add(T value)` / `Add(ReadOnlySpan<T> values)` | One value, or a span in order. Evicts the oldest value once the window is full. |
| `Value` / `TryGetValue(out T)` | The current quantile. `Value` throws `InvalidOperationException` on an empty window. |
| `Count` / `WindowSize` / `IsFull` / `Quantile` | Values in the window, its capacity, whether it has filled, and the fraction. |
| `Clear()` | Empties the window in `O(Count)`, releasing any references it held. |

While the window is still filling, `Value` is taken over the values seen so far. Check `IsFull`
when a partial window should not be read as a full one. Values are ordered by
`Comparer<T>.Default`, which puts a `null` reference first and, for `double`, `NaN` below every
number. One instance answers one quantile; for a median *and* a p99 over the same window, use two.

There is no enumerator. The window is a position in the caller's stream, and a caller who needs
its contents already has them.

## Where these lose

Celerity documents its tradeoffs rather than claiming a blanket win. All four of these are on the
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
- **`ReservoirSampler` loses on short streams.** Algorithm L pays several transcendentals per
  replacement, and at a thousand items that is most of the work: CI measures it **6.2× slower**
  than materializing and shuffling. By a hundred thousand it is **1.7× faster** and holds 100
  items instead of 100,000. The crossover is around ten thousand — below that, materialize.
- **`RollingQuantile`'s margin is narrow on a small window.** Against the careful sorted-array
  hand-roll, a local run (Apple M-series, .NET 10) measured **2.5× faster at a 1,000-value window,
  5.0× at 10,000 and 32× at 100,000**, and still about 2× at 16 and 100. No crossover turned up, but
  below a few hundred values the whole sample costs tens of nanoseconds either way, so pick the
  type there for the code it saves rather than the time.
- **`RollingQuantile` answers one quantile, and holds the window.** It is `w` values plus `w`
  positions, against `DDSketch`'s fixed footprint. A second quantile over the same window needs a
  second instance and a second copy of the window.
- **`RunningStatistics` is slower than LINQ, measurably.** CI puts `Moments` at **1.9× the
  two-pass LINQ shape** at both sizes, because the accumulator maintains all four moments on
  every `Add` while that caller reads two. It buys a single pass over a stream it never
  retains, and an answer the `sum` / `sumOfSquares` shortcut gets wrong — not speed.

The numbers above are the CI-tracked ones from the
[dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/); the losing arms are charted
next to the winning ones so the crossovers can be read off rather than guessed.

## See also

- [Collections API](collections.md) — including the sketches (`HyperLogLog`, `CountMinSketch`,
  `TopKSketch`, `MinHashSketch`) that summarize a stream's *cardinality*, *frequencies* and
  *overlap with another stream* rather than its distribution.
- [Utilities API](utilities.md) — the seedable struct PRNGs the sampler is parameterized on.
- [Testing & coverage](../testing.md) — the fuzz targets. `DDSketch`, `RunningStatistics` and
  `RollingQuantile` reconcile against exact oracles; the sampler's is invariant fuzzing, because uniformity is
  distributional and no single case can measure it.
