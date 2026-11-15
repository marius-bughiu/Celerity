# Benchmarks

**Up to 2.4&times; faster than `Dictionary<int, int>`** on lookups, with zero allocations — this is a *collection-layout* win (open addressing with direct `==` key comparison and no per-call `EqualityComparer<T>` dispatch), **independent of the hasher**. It does *not* mean the hashers beat `GetHashCode()` on speed (they don't, and for `int` cannot — see [Choosing a hasher](choosing-a-hasher.md)). The [live dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/) tracks every shipped collection against its BCL counterpart on each merge to `main` that can move a number, with historical trends; to measure a branch before it merges, run the workflow by hand (see [CONTRIBUTING](../CONTRIBUTING.md#ci)). For high-precision local numbers, run `dotnet run -c Release` in [`src/Celerity.Benchmarks`](../src/Celerity.Benchmarks) — hosted CI runners are noisier than your laptop.

The suite also includes the `string` hasher benchmarks (`StringHasherBenchmark` and its per-family siblings) and `IntegerHasherBenchmark` (every built-in hasher bracketed by two baselines — the direct `GetHashCode()` and `EqualityComparer<T>.Default.GetHashCode()`, the per-probe call a BCL `Dictionary<,>` actually makes; rendered under **Hash function throughput** on the dashboard; run locally with `--filter "*HasherBenchmark*"`). Treat these as a **raw-mixing-cost diagnostic only** and read them alongside the distribution metrics from `HashQualityEvaluator` — a fast hasher that clusters is not a win. The isolated `Hash()` number alone is misleading (for `int`, `GetHashCode()` is identity — *zero* work — so no mixer can beat it), so the extended suite adds `HasherEndToEndBenchmark`, which times each hasher **through the dictionary** across all four key shapes, and a deterministic probe-length report (`dotnet run -c Release -- --probe-analysis`) — the cases where a strong hasher "loses" the microbench but wins end-to-end. See [measuring probe length](performance.md#measure-probe-length-not-just-hash-speed).

An **extended suite** answers the harder questions a single random-key benchmark can't: multiple key distributions (uniform / sequential / clustered / adversarial), million-item scale, allocation profiling, concurrent read scaling, cache locality, mixed read-heavy workloads, and a `FrozenDictionary<,>` comparison. CI runs it weekly, publishing to a [separate dashboard](https://marius-bughiu.github.io/Celerity/dev/bench-extended/); locally, run it on demand — e.g. `dotnet run -c Release -- --filter "*Distribution*"`. See the [extended benchmark suite](performance.md#extended-benchmark-suite).

## Results that depend on the baseline

The three spatial indexes are each measured against two baselines. For `KdTree` and `RTree`, which one you compare against decides the headline; for `SpatialGrid`, the margin depends on how full its cells are.

### Spatial index

`KdTree<TValue>` is one of the two shipped types whose headline depends entirely on **which baseline you pick** (see [Extent index](#extent-index) for the other), so both are measured. At 100,000 uniformly scattered points, 1,000 queries per measurement, taken from CI's same-runner A/B on `ubuntu-latest` — hosted runners are noisy, so treat the ratios as the signal and watch the trend on the [dashboard](https://marius-bughiu.github.io/Celerity/dev/bench/):

| Query | vs. the naive scan | vs. a hand-rolled sorted scan |
| --- | --- | --- |
| **Nearest neighbour** | 103.50 ms → 297 µs — **348x** | 733 µs → 296 µs — **2.5x** |
| **Within a radius** (~0.1% of the domain) | 104.44 ms → 1.98 ms — **53x** | 6.98 ms → 1.98 ms — **3.5x** |
| **10 nearest** | 194.49 ms → 1.81 ms — **107x** | *(baseline is already the smart one: a bounded max-heap scan, heapsorted to the same ascending order)* |

The naive scan is the array-and-a-loop the BCL leaves you with, and against it the tree is one to two orders of magnitude ahead. The second column is the honest one. A caller can sort the points by x, binary-search to the query and work outward, abandoning each direction once the horizontal gap alone exceeds the best distance so far — a real optimization, and effectively a **one-dimensional spatial index**. Against that, the tree wins by a small constant factor, because the second dimension is the only thing it adds.

**At 1,000 points the margin against the hand-roll all but disappears**: 1.4x on the nearest query, and level on the radius query (117 µs against 120 µs, the scan a hair ahead). The tree needs tens of thousands of points before its extra dimension clearly pays for its extra indirection.

**Build is the price**, paid once: 750 µs → 22.2 ms at 100,000 points (**30x** the cost of merely copying the array, at 1.83x its memory). The sorted-scan alternative pays its own `O(n log n)` ordering, so that multiple overstates the gap against the baseline you would actually compare with.

Two further caveats worth stating rather than burying:

- **A k-d tree has no useful worst-case bound.** An adversarial point set makes every query visit every node; the classic `O(log n)` for nearest-neighbour is an average over uniform points, not a guarantee. What is guaranteed is that a query visits at most `n` nodes, so each family stays bounded by the loop it replaces — `O(n)` for the nearest and range queries, and `O(n log k)` for k-nearest, whose per-candidate heap sift the hand-rolled bounded-heap scan pays as well.
- **The data's shape does not just move the ratio, it can reverse it.** Pruning discards subtrees that cannot hold a result, so a query answering with much of the tree converges on the scan. Clustered points — which is what real spatial data usually looks like — are measured as **worse** for the tree, not better: against the sorted scan it goes from 1.92x on uniform points to **0.94x on clustered ones, where the hand-roll wins outright**. The cause is the distance to the query's nearest neighbour rather than density: inside a cluster that neighbour is very close, and a tiny best distance is exactly what lets the scan abandon after a handful of points. `KdTreeShapeBenchmark` in the extended suite carries the measurement, the reasoning, and the sampling bias that made an earlier version of it flatter the tree.

### Moving points

`SpatialGrid<TValue>` answers the workload `KdTree` documents itself out of. The unit is a **frame**: move 10% of the population, then run one radius query per moved entity. Both baselines are hand-rolls, because the BCL has nothing here either — the `Dictionary<(int, int), List<int>>` bucketed grid a competent developer writes, and rebuilding a `KdTree` every frame, which is what this library offered before. At 100,000 entities over a 10,000-unit square, with a 30-unit cell and a 25-unit query radius — the cell sized just over the radius, which is the tuning rule the type documents — taken from CI's same-runner A/B on `ubuntu-latest`, on .NET 8:

| Per frame | vs. the bucketed `Dictionary` hand-roll | vs. rebuilding a `KdTree` |
| --- | --- | --- |
| **Move a tenth, then query each** | 9.83 ms → 1.96 ms — **5.0x** | 26.15 ms → 1.96 ms — **13.3x** |
| **Query only, nothing moving** | 8.27 ms → 1.72 ms — **4.8x** | — |
| **Churn a tenth (remove + re-add)** | 1.34 ms → 184 µs — **7.3x** | — |

At 1,000 entities the margins are *wider*, not narrower — 11.9x on the frame — because the cells are emptier still.

**Read the frame number carefully.** The issue that asked for this type set a hard bar of **≥5x or it does not ship**, and 9.83 / 1.96 is **5.02x** — cleared by 0.4%, against a combined run spread of about 1.5% on the two measurements. It passes, and it passes by less than the noise. A development machine said 5.5x; taking CI's number instead of that one is the difference between "comfortably" and "marginally", and marginally is the honest word.

**The margin is a property of how full the cells are, not of the type**, and that belongs next to the headline rather than under it. Both structures walk the same cells and run the same distance test on the same candidates; everything the grid saves is **per cell** — an array index instead of a tuple hash and a bucket probe, an intrusive link instead of a separately allocated `List<T>` — so the ratio is per-cell overhead against per-candidate work, and it collapses as the cells fill:

The shape sweep rides the extended suite, which CI runs weekly rather than on each merge, publishing to [its own dashboard](https://marius-bughiu.github.io/Celerity/dev/bench-extended/). The figures below come from a single run that is not on that dashboard: a manual run of the suite, its first on .NET 10 ([run 43575145503](https://github.com/marius-bughiu/Celerity/actions/runs/43575145503), `ubuntu-latest`, November 2026), at a commit whose measured code matches `main`. Read them as one sample: on .NET 8, the twelve weekly runs that reached this class, six of which the timeout cut off (one partway through it), spread the same ratios up to 20% either side of their midpoints. Each run measures every arm on one runner, so a row compares like with like:

| Shape at 100,000 entities | vs. the bucketed `Dictionary` hand-roll | vs. rebuilding a `KdTree` |
| --- | --- | --- |
| **Tight** — ~1 point per cell, ~2 matches per query (the broadphase shape, and the one charted) | **2.3x** | **13.5x** |
| **Wide** — ~10 points per cell, ~25 matches, cell still sized to the radius (100 and 90) | **0.86x** — the hand-roll is ahead | **2.1x** |
| **Clustered** — 200 blobs, hundreds of points per cell | **0.60x** — the hand-roll wins | **0.78x** — the rebuild wins |

The tight row is the shape of the frame above, and it reads well under 5.0x because that figure was measured on .NET 8. On matched runners (the same CPU model, Ubuntu release and instruction set), the core suite measured this shape on both runtimes a day apart: .NET 10 runs the hand-roll's side 1.8x faster than .NET 8 did, and the grid's only 1.06x faster.

A development machine read the first column as 6.5x, 1.12x and 0.52x and put the clustered rebuild level with the grid. CI reverses two of those verdicts in this run: the hand-roll is ahead on the wide row, and the rebuild beats the grid on clustered points, as it did in every .NET 8 run that measured it. On .NET 8 the wide row sat near parity, 0.91–1.03x, with the grid narrowly ahead in three runs and never near 1.12x.

The clustered row is the one worth reading twice, and it is the opposite of what the class was written to show. The expectation was that clustering would hurt both structures equally, since the baseline is the same grid. It does not: inside a dense cell the layouts genuinely differ. The hand-roll walks a contiguous `List<int>` and issues two independent loads per candidate, which the processor overlaps; the grid walks an intrusive linked list, so each step is a load that must complete before the next address is known — a cell holding five hundred entries is a five-hundred-long chain of dependent cache misses. So the documented failure mode is sharper than "it degrades to a scan": it degrades to a scan the hand-roll does faster.

**And there is no way out elsewhere in this library — which had to be measured rather than assumed.** An earlier draft of this section sent clustered points to `KdTree`, reasoning that pruning adapts to density where a fixed cell size cannot. That inference chained two unrelated comparisons and nobody had measured the one a caller actually faces, so `SpatialGridShapeBenchmark` now carries a third arm that does. It is not a way out: on clustered *moving* points a per-frame `KdTree` rebuild takes **22% less time than the grid**, but still **1.3x the hand-roll's**. For heavily clustered points that move, the bucketed `Dictionary` of contiguous lists wins and neither Celerity type beats it.

One more number worth stating plainly because it was predicted otherwise: the issue that asked for this type expected **≥50x** over the per-frame `KdTree` rebuild and called it the easy bar. It is 13.3x on .NET 8, and the .NET 10 run above puts the same shape at 13.5x. A frame is not only the rebuild — both arms also run 10,000 radius queries, and that shared work sits in the denominator however cheap the index makes it.

### Extent index

`RTree<TValue>` indexes axis-aligned **rectangles** rather than points, which `KdTree` structurally cannot: a box can overlap the query while its centre sits far outside it. Same two-baseline treatment, at 100,000 boxes whose extents span three orders of magnitude, 1,000 queries per measurement, taken from CI's same-runner A/B on `ubuntu-latest`, and the selectivity each query family actually reaches:

| Query | Selectivity | vs. the naive scan | vs. a hand-rolled sorted scan |
| --- | --- | --- | --- |
| **Boxes overlapping a box** | 0.0835% | 433.74 ms → 3.08 ms — **141x** | 29.77 ms → 3.09 ms — **9.6x** |
| **Boxes containing a point** | 0.0050% | 401.29 ms → 1.67 ms — **240x** | 17.78 ms → 1.67 ms — **10.6x** |

**The two rows are not like-for-like**, and the difference is inherent rather than an oversight: the query box is tuned so the overlap query lands on the ~0.1% the kill criterion names, but a point query's answer size is fixed by the extents alone and comes out twenty times more selective, which flatters its ratio. Raising it to 0.1% would need boxes that blanket the map and would drag the overlap arm well past 0.1% in the process. The benchmark fails its own run if either figure drifts out of band.

The second column is the honest one and was the bar set **before** implementation, deliberately at a modest 3x because `KdTree`'s analogous one-dimensional hand-roll came in at a surprising 2.5x. The hand-roll here orders the boxes by `minX`, binary-searches to the query's `minX` less the widest stored box, and scans forward while `minX` stays at or below the query's `maxX` — effectively a **one-dimensional R-tree**, and the second dimension plus the extent hierarchy are the whole of what the tree adds over it.

**At 1,000 boxes the margin against the hand-roll goes negative on one query**: 1.48x on the overlap query, but **0.91x on the point query — the scan wins**. The index has not paid for its indirection at that size, and this is exactly why the repo quotes CI: a development machine put the tree *ahead* on that arm. **Build is the price**, paid once: 436 µs → 62.60 ms at 100,000 boxes (**144x** the cost of merely copying the array) — a sort per level, which is what "Sort-Tile-Recursive" costs.

Two caveats worth stating rather than burying:

- **An R-tree has no useful worst-case bound.** Overlapping node boxes mean a query can descend into several children per level, and an adversarial box set forces it into all of them. What is guaranteed is that a query visits each node and each entry at most once, so every family stays `O(n)` — bounded by the loop it replaces. The ratios track **selectivity**: a query ten times wider narrows them considerably.
- **The received wisdom about shape did not survive being measured.** The standard advice is that an R-tree only earns its keep on extents spanning orders of magnitude, and that uniformly sized boxes belong to a bucketed grid. `RTreeShapeBenchmark` in the extended suite gives the grid an arm of its own rather than inferring it, and the tree comes out **1.30x** ahead of the grid on the varying shape and **3.01x** ahead on the uniform one — its widest margin is on the shape it was expected to lose. An R-tree's node boxes get tighter as extents get more alike, so the query settles in a shorter descent, while the grid barely moves between the two shapes. Both shapes hold the same mean extent, so the control varies only the spread; an earlier version used the geometric rather than arithmetic mean and was confounded by 21x less box area, which review caught. The qualification is that a grid's cell size is a tuning knob and this one is sized by the data rather than the query; the real reason to reach for a grid is that it is **mutable** and this type is not. The same benchmark also settles the packing choice: sort-tile is 1.04x ahead of a Hilbert order on varying extents and 1.21x ahead on uniform ones.
