# Celerity.Statistics

Streaming summary statistics in bounded memory. Part of the
[Celerity](https://github.com/marius-bughiu/Celerity) family of high-performance
.NET libraries.

The BCL summarizes a sequence with `Average`, `Sum`, `Min` and `Max`, and stops
there. There is no quantile type, no sampler, no variance, no higher moment and
no accumulator that can be fed one value at a time — so summarizing a stream
means keeping the stream, which is the one thing a stream will not let you do.

## What's in the box

- **`DDSketch`** — quantiles with a *relative* error guarantee: the reported
  value is within `α` of the true one at **every** quantile, in memory
  proportional to the log of the value range rather than to the sample count.
  1% of 10 ms and 1% of 10 s, which is what latency work wants. Handles
  negatives and zero, merges bucket-exactly across shards unless an operand
  has already collapsed, and says out loud
  (`HasCollapsed`) when a bin budget has run out and the guarantee has stopped
  holding for the quantiles that resolve to a collapsed bucket.
- **`ReservoirSampler<T>`** — a fixed-size uniform sample of a stream of unknown
  length, via Li's **Algorithm L**: `O(k)` memory and `O(k · log(n / k))` random
  draws over the whole stream, not one per item. Seeded, so the sample is
  reproducible on a given runtime and platform — but not promised byte-identical
  across them, because the skip arithmetic runs through `Math.Log` / `Math.Exp`,
  whose last bit .NET does not fix across runtimes.
- **`RunningStatistics`** — count, mean, variance, standard deviation, skewness,
  kurtosis, min and max in a **single pass**, using Welford's recurrence
  extended to the fourth moment. A mutable struct, so `default` is a valid empty
  accumulator and an array of per-bucket statistics costs no allocations.
  Mergeable by Chan's parallel formulas.

## Where it wins, and where it does not

- **`DDSketch` is not exact.** It trades a bounded relative error for bounded
  memory. If every sample fits comfortably in a `List<double>` and you query
  once, sort it — the sketch wins on memory and on repeated queries, not on a
  single exact answer.
- **`DDSketch`'s guarantee is relative, not absolute.** Near zero, a relative
  error is a very small absolute one, and far from zero it is a large one. That
  is the right shape for latencies and sizes, and the wrong shape for a value
  that can be any magnitude with equal significance.
- **`ReservoirSampler` has no `Merge`.** A uniform merge of two reservoirs needs
  a hypergeometric draw over the two stream lengths; replaying one side's
  retained items into the other over-weights the shorter stream. Rather than
  ship a subtly biased merge, it ships without one.
- **`RunningStatistics` wins on accuracy, not on speed.** It is measurably
  *slower* than the two-pass LINQ shape — CI puts it at 1.9× — because it
  maintains all four moments on every `Add`. What it buys is a single pass over
  a stream it never retains, and an answer the `sum` / `sumOfSquares` shortcut
  gets wrong: that one is cheaper still and can return a negative variance.
- **`ReservoirSampler` loses on short streams**, where Algorithm L's
  transcendentals dominate: 6.2× slower than materializing at a thousand items,
  1.7× faster at a hundred thousand. The crossover is around ten thousand.

## Quick start

```csharp
using Celerity.Statistics;

// Quantiles over an unbounded stream, 1% relative accuracy.
var latencies = new DDSketch(relativeAccuracy: 0.01);
foreach (double ms in requestLatencies)
{
    latencies.Add(ms);
}

Console.WriteLine($"p50 {latencies.GetQuantile(0.5):F2} ms");
Console.WriteLine($"p99 {latencies.GetQuantile(0.99):F2} ms");

// Merge per-shard sketches into a global one.
foreach (DDSketch shard in shards)
{
    latencies.Merge(shard);
}

// A 1,000-item uniform sample of a stream of unknown length.
var sample = new ReservoirSampler<string>(capacity: 1_000, seed: 42);
foreach (string line in logLines)
{
    sample.Add(line);
}

// Single-pass moments, no allocation.
var stats = new RunningStatistics();
stats.Add(payloadSizes);
Console.WriteLine($"{stats.Mean:F1} ± {stats.StandardDeviation:F1} bytes");
```

## Documentation

- [API reference](https://github.com/marius-bughiu/Celerity/blob/main/docs/api/statistics.md)
- [Benchmark dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/)
- [Main README](https://github.com/marius-bughiu/Celerity/blob/main/README.md)

## License

MIT
