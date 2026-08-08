#!/usr/bin/env node
//
// Builds the pull-request benchmark comparison comment from the merged BenchmarkDotNet
// reports, and decides which rows are flagged.
//
// ---- Why a row gets flagged, and why the old rule did not work --------------------------
//
// A row was flagged when it moved past ±10% AND `|prMean - baseMean| > prStdDev +
// baseStdDev`. That second term is a one-sigma bar, and it is far too low to survive this
// workflow's noise. On PR #350 — a diff whose library IL is byte-identical to `main`, so
// every delta in it is measurement noise by construction — the rule flagged **13 rows**
// (8 regressions, 5 improvements) out of 757, four of them on `Dictionary<,>` / `HashSet<>`
// baseline arms whose code is the same on both sides by definition (#351).
//
// The measured spread of that run, which is the reason no purely per-row rule can
// reach zero: |Δ| has a p50 of 0.7% and a p90 of 6.2%, but 43 rows exceed ±10%,
// 16 exceed ±25% and the worst is 222%. The distribution is sharply peaked with a fat tail
// of individually unstable cases — sensitive to code and data layout, which differs
// between two builds even when their IL does not.
//
// One premise of #351 turns out not to hold, and it matters for the fix: BenchmarkDotNet's
// `Statistics.StandardDeviation` is *not* within-launch. It is computed over the pooled
// `Workload`/`Result` iterations of every launch (`Job.Default.WithLaunchCount(2)` gives
// e.g. N = 32 + 100 = 132 for one case), so it already contains the launch-to-launch
// process, JIT and layout variation. Deriving a separate between-launch estimate from the
// per-measurement data was tried and is *worse*: standardising the observed deltas by each
// candidate spread and taking the robust scale of the result over all 757 rows gives 0.81
// for the pooled iteration SD against 1.13 for a launch-mean SD and 4.80 for the standard
// error — i.e. the pooled SD is the one already on the right scale, and the SE is
// mis-calibrated by ~5x. What the old rule got wrong was not *which* dispersion it used
// but *how many* of them it demanded.
//
// So the guard is now a three-sigma bar, with the two sides combined in quadrature rather
// than summed (independent errors add in quadrature; summing overstates the bar when one
// side is noisy and understates the confidence when neither is). Replayed over the same
// zero-IL run:
//
//              false positives   +15% regression detected   +25% detected
//   old rule         13                    86%                   96%
//   3σ quadrature     2                    79%                   87%
//
// and, restricted to the 200 least-noisy rows — the ones where a real regression is
// legible at all — detection is *identical* to the old rule from +12% upward (100%). The
// sensitivity given up is entirely on rows too noisy to have carried a trustworthy signal.
//
// The two survivors are the pathological shape: a large shift with a tight spread on both
// sides (`SmallDictionaryBenchmark.Dictionary_Remove(ItemCount: 64)`, +33.1% with 3.4% and
// 3.5% relative SD). Both launches of each side agree closely, so nothing inside a single
// A/B pass can tell it from a real change; only re-measuring against a fresh pair of builds
// can. That residual is why the comment publishes the run's own observed spread rather
// than presenting a flag as a verdict.
//
// Usage:
//   node scripts/benchmark_comment.js --pr <pr-report.json> --base <base-report.json> \
//        --out <body.md>
//   node scripts/benchmark_comment.js --self-test
//
// Environment (all optional): ALERT_THRESHOLD_RATIO, ALERT_NOISE_SIGMAS, MISSING_SHARDS,
// BASE_SHA, SHARD_TOTAL.

'use strict';

const fs = require('fs');

const DEFAULT_THRESHOLD_RATIO = 1.10;

// Three sigma. Calibrated, not conventional: see the table above. Raising it to 4 removes
// one more false positive from the reference run and costs another ~3 points of detection
// on the noisy tail, which is not a trade worth making — the remaining pair is not the
// kind a bigger bar reaches.
const DEFAULT_NOISE_SIGMAS = 3;

const COMMENT_MARKER = '<!-- celerity-benchmarks-comment -->';

function formatNs(ns) {
  if (ns == null || !Number.isFinite(ns)) return 'n/a';
  if (ns < 1000) return `${ns.toFixed(1)} ns`;
  if (ns < 1_000_000) return `${(ns / 1000).toFixed(2)} μs`;
  if (ns < 1_000_000_000) return `${(ns / 1_000_000).toFixed(2)} ms`;
  return `${(ns / 1_000_000_000).toFixed(2)} s`;
}

// A mean has to be finite and positive for the ratio between two of them to mean anything.
// A zero or missing base makes the ratio Infinity, which clears the regression gate; a zero
// PR mean makes it 0, which clears the improvement gate. Either way the row would be
// flagged on the strength of a broken measurement, which is precisely the failure mode this
// file exists to remove.
function isComparable(prMean, baseMean) {
  return Number.isFinite(prMean) && prMean > 0 && Number.isFinite(baseMean) && baseMean > 0;
}

// The flag decision for one paired row, kept as a named function so the self-test can
// exercise it directly against the real measurements that motivated it.
//
// `spread` is 0 when BenchmarkDotNet reported no dispersion for a side (a degenerate
// single-measurement case). The guard then reduces to the ratio gate alone, which is the
// behaviour the previous rule had for the same input — this decides how much evidence a
// delta needs, and it must not silently drop a row it cannot judge.
function classifyDelta(prMean, prStdDev, baseMean, baseStdDev, thresholdRatio, noiseSigmas) {
  if (!isComparable(prMean, baseMean)) return null;

  const ratio = prMean / baseMean;
  const ps = Number.isFinite(prStdDev) ? prStdDev : 0;
  const bs = Number.isFinite(baseStdDev) ? baseStdDev : 0;
  const spread = Math.sqrt(ps * ps + bs * bs);
  const beyondNoise = Math.abs(prMean - baseMean) > noiseSigmas * spread;

  if (ratio >= thresholdRatio && beyondNoise) return 'regression';
  // Symmetric in the multiplicative sense: 1.10x slower and 1.10x faster are the same
  // sized move, so the improvement side is gated on 1/threshold, not on -10%.
  if (ratio <= 1 / thresholdRatio && beyondNoise) return 'improvement';
  return null;
}

// Nearest-rank percentile over an already-sorted ascending array: the reported value is
// always an |Δ| some row actually exhibited, never an interpolation between two rows. That
// is the right choice for a published spread — "p95 is 10.3%" should name an observed
// measurement — but it does mean p50 is not the textbook median on an even-length sample,
// which is why nothing below calls it one.
function percentile(sorted, p) {
  if (sorted.length === 0) return null;
  const rank = Math.ceil((p / 100) * sorted.length);
  return sorted[Math.min(sorted.length - 1, Math.max(0, rank - 1))];
}

// The run's own delta distribution, measured rather than assumed (#351). On a typical pull
// request — a handful of the ~750 tracked cases touched — this is a noise floor: almost
// every row in it is the runner's own drift between the head slice and the base slice.
//
// It is not *only* that, which is why neither this function nor the footer it feeds calls
// it a noise floor. A change to a shared primitive or to the benchmark configuration moves
// many rows at once, and those real effects sit in the same sample. Deriving the floor from
// a control group instead was considered and rejected: the obvious controls are the BCL
// baseline arms, but a layout shift moves those too, so they would understate the tail
// while looking authoritative. Reporting the distribution and saying what is in it is the
// honest version.
//
// `noiseSigmas` and `ALERT_NOISE_SIGMAS` keep their names on purpose. That guard is scaled
// by the two measurements' own standard deviations, which really are per-row measurement
// noise — a different quantity from the run-wide distribution measured here, and one the
// word describes correctly.
function spreadProfile(deltaPercents) {
  if (deltaPercents.length === 0) return null;
  const sorted = [...deltaPercents].sort((a, b) => a - b);
  return {
    n: sorted.length,
    p50: percentile(sorted, 50),
    p90: percentile(sorted, 90),
    p95: percentile(sorted, 95),
  };
}

// A setting that silently falls back is how a gate gets weakened without anyone noticing,
// and a setting that silently *accepts* nonsense is worse. `ALERT_THRESHOLD_RATIO=1` flags
// every row in the run; `0.9` inverts the two gates so a speed-up is called a regression;
// a negative `ALERT_NOISE_SIGMAS` makes the noise guard vacuously true and restores exactly
// the behaviour this file was written to remove. None of those announce themselves in the
// output, so they are refused here instead.
//
// An unset or empty variable is not an error — it means "use the calibrated default".
function readSetting(name, raw, fallback, isValid, expectation) {
  const text = (raw ?? '').trim();
  if (text === '') return fallback;

  const value = Number(text);
  if (!Number.isFinite(value) || !isValid(value)) {
    throw new Error(`${name}=${JSON.stringify(text)} is not usable: expected ${expectation}.`);
  }
  return value;
}

function buildComment(prReport, baseReport, options = {}) {
  const thresholdRatio = options.thresholdRatio ?? DEFAULT_THRESHOLD_RATIO;
  const noiseSigmas = options.noiseSigmas ?? DEFAULT_NOISE_SIGMAS;
  const thresholdPct = ((thresholdRatio - 1) * 100).toFixed(0);

  const baseMap = new Map();
  for (const b of baseReport.Benchmarks || []) {
    baseMap.set(b.FullName, b.Statistics);
  }

  const entries = [];
  const deltaPercents = [];
  let regressions = 0;
  let improvements = 0;
  let errored = 0;

  for (const b of prReport.Benchmarks || []) {
    const name = b.FullName;

    // BenchmarkDotNet emits a benchmark with null Statistics and empty Measurements when
    // that case errored or was not run (a throwing [GlobalSetup], a cancelled shard, an
    // OOM, ...). Surface it as a row rather than dereferencing null — one errored case
    // must never crash the aggregate job and red-X the whole PR gate.
    if (b.Statistics == null) {
      errored++;
      const baseStats = baseMap.get(name);
      entries.push({
        name,
        isHasher: /Hasher/.test(name),
        prCell: '⚠️ errored',
        stdCell: 'n/a',
        baseCell: baseStats ? formatNs(baseStats.Mean) : 'n/a',
        deltaCell: 'no measurements',
        flag: null,
      });
      continue;
    }

    const prMean = b.Statistics.Mean;
    const prStdDev = b.Statistics.StandardDeviation;
    const baseStats = baseMap.get(name);
    let deltaCell;
    let flag = null;

    if (!baseStats) {
      // Present in the base report but with no statistics means the base side of this pair
      // errored, so there is nothing to compare against. Counted, because a subtitle
      // reading "No significant change" over a row that could not be measured is the same
      // false reassurance a too-permissive guard gives.
      if (baseMap.has(name)) {
        errored++;
        deltaCell = '⚠️ base errored';
      } else {
        deltaCell = '🆕 new';
      }
    } else {
      const baseMean = baseStats.Mean;
      // A row whose ratio cannot mean anything is reported as such rather than rendered as
      // `+Infinity%` and flagged. It is counted with the errored rows because it is the
      // same thing from a reviewer's point of view — a case that produced no usable
      // comparison — and a silently uncounted anomaly reads exactly like a clean report.
      if (!isComparable(prMean, baseMean)) {
        errored++;
        deltaCell = '⚠️ not comparable';
      } else {
        const pct = (prMean / baseMean - 1) * 100;
        deltaCell = `${pct >= 0 ? '+' : ''}${pct.toFixed(1)}%`;
        deltaPercents.push(Math.abs(pct));

        flag = classifyDelta(prMean, prStdDev, baseMean, baseStats.StandardDeviation, thresholdRatio, noiseSigmas);
        if (flag === 'regression') { deltaCell += ' ⚠️'; regressions++; }
        else if (flag === 'improvement') { deltaCell += ' ✅'; improvements++; }
      }
    }

    entries.push({
      name,
      // Hasher throughput benchmarks (StringHasherBenchmark, IntegerHasherBenchmark) get
      // their own section; everything else is a collection benchmark.
      isHasher: /Hasher/.test(name),
      prCell: formatNs(prMean),
      stdCell: formatNs(prStdDev),
      baseCell: baseStats ? formatNs(baseStats.Mean) : 'n/a',
      deltaCell,
      flag,
    });
  }

  let subtitle;
  if (regressions > 0) {
    subtitle = `${regressions} regression${regressions === 1 ? '' : 's'} ⚠️ vs main` +
      (improvements > 0 ? `, ${improvements} improvement${improvements === 1 ? '' : 's'} ✅` : '') +
      ` (rows past ±${thresholdPct}% and beyond ${noiseSigmas}σ).`;
  } else if (improvements > 0) {
    subtitle = `No regressions — ${improvements} improvement${improvements === 1 ? '' : 's'} ✅ vs main.`;
  } else {
    subtitle = `No significant change vs main (all within ±${thresholdPct}% or inside ${noiseSigmas}σ of the measurement noise).`;
  }
  if (errored > 0) {
    subtitle += ` ⚠️ ${errored} benchmark${errored === 1 ? '' : 's'} produced no usable comparison — see the table.`;
  }

  // The script is runnable outside Actions (that is the point of extracting it), where
  // neither of these is set. Say so rather than rendering `main (``)` and `sharded -way`,
  // which read like a bug in the report.
  const baseSha = (options.baseSha || '').slice(0, 7);
  const baseLabel = baseSha ? `main (\`${baseSha}\`)` : 'main';
  const shardTotal = String(options.shardTotal || '').trim();
  const shardLabel = shardTotal ? `Same-runner A/B (sharded ${shardTotal}-way)` : 'Same-runner A/B';
  const footer = [
    `<sub>${shardLabel}: ${baseLabel} and this PR were ` +
    `built and benchmarked back-to-back on the same runner per shard, so hardware variance cancels out. ` +
    `⚠️ = PR mean is at least ${thresholdPct}% slower than main **and** the gap exceeds ${noiseSigmas}σ of the two ` +
    `measurements' combined standard deviation; ✅ = correspondingly faster.</sub>`,
  ];

  const spread = spreadProfile(deltaPercents);
  if (spread) {
    // Named for what it is rather than for what it usually is. On a narrow change this
    // reads as the run's noise floor; on a broad one it moves with the change itself, and
    // a reviewer told "this is drift" would talk themselves out of a real regression.
    // Reported as percentiles, not as a mean and a "median": each figure is the |Δ| of an
    // actual row (see `percentile`), and naming them p50/p90/p95 says so.
    footer.push('');
    footer.push(
      `<sub>**Observed spread of this run** — |Δ| across all ${spread.n} paired rows: p50 ` +
      `${spread.p50.toFixed(1)}%, p90 ${spread.p90.toFixed(1)}%, p95 ${spread.p95.toFixed(1)}% ` +
      `(nearest-rank). A pull request usually touches a handful of these, so this is mostly the ` +
      `runner's own drift between the two slices — but a change to a shared primitive moves many ` +
      `rows at once and would raise these figures itself, so read it as the run's spread rather ` +
      `than as a floor the PR cannot have caused. A flag that does not stand out against it is ` +
      `worth re-running before acting on.</sub>`
    );
  }

  const header = ['| Benchmark | This PR | StdDev | main | Δ |', '|---|---:|---:|---:|---:|'];
  const toRow = (e) => `| \`${e.name}\` | ${e.prCell} | ${e.stdCell} | ${e.baseCell} | ${e.deltaCell} |`;

  // A collapsible section per benchmark family, collapsed by default so the (often large)
  // tables stay below the fold.
  const section = (title, list) => {
    if (list.length === 0) return [];
    return ['<details>', `<summary><b>${title}</b> (${list.length})</summary>`, '', ...header, ...list.map(toRow), '</details>', ''];
  };

  // Highlights: only the flagged rows stay above the fold so reviewers see what actually
  // moved without expanding either table.
  const flagged = entries.filter((e) => e.flag);
  const highlights = flagged.length === 0 ? [] : ['**Highlights**', '', ...header, ...flagged.map(toRow), ''];

  // A partial merge otherwise reads exactly like a complete one: the tables are
  // well-formed and simply have fewer rows, so a silently-dropped shard looks like a clean
  // report. Say it above the fold, before any numbers.
  const missingShards = (options.missingShards || '').trim();
  const incomplete = missingShards.length === 0 ? [] : [
    '> [!WARNING]',
    `> **Incomplete report.** Shard(s) \`${missingShards}\` produced no measurements, so every`,
    '> benchmark class packed onto them is missing from the tables below — including, possibly,',
    '> the ones this PR changed. Treat the comparison as partial rather than as a clean run.',
    '',
  ];

  const body = [
    COMMENT_MARKER,
    '## Benchmarks',
    '',
    ...incomplete,
    subtitle,
    '',
    ...highlights,
    ...section('Collections', entries.filter((e) => !e.isHasher)),
    ...section('Hashers', entries.filter((e) => e.isHasher)),
    ...footer,
  ].join('\n');

  return { body, entries, regressions, improvements, errored, spread, missingShards, thresholdRatio, noiseSigmas };
}

// ---- self-test ------------------------------------------------------------------------

// Every row the *old* rule flagged on PR #350 (run 31145509269), whose library IL is
// byte-identical to `main` — so all thirteen are noise, and this is the calibration sample
// the threshold above was chosen against. Columns: name, PR mean, PR SD, base mean, base
// SD, all in nanoseconds as BenchmarkDotNet reported them.
const ZERO_IL_FALSE_POSITIVES = [
  ['PooledCelerityDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)', 5372.3, 525.2, 4707.0, 117.8],
  ['SwissDictionaryBenchmark.Dictionary_Lookup(ItemCount: 1000)', 4733.2, 118.0, 6184.6, 1172.1],
  ['SwissDictionaryBenchmark.SwissDictionary_Lookup(ItemCount: 1000)', 3238.2, 252.6, 2878.8, 14.1],
  ['SortedSpanBenchmark.Linq_UnionLinq(ItemCount: 100000)', 5238281.3, 292129.3, 6180764.8, 540588.1],
  ['SmallDictionaryBenchmark.SmallDictionary_Remove(ItemCount: 8)', 1112.9, 112.8, 1517.7, 282.5],
  ['SmallDictionaryBenchmark.Dictionary_Remove(ItemCount: 64)', 6465.2, 219.1, 4857.3, 168.2],
  ['RobinHoodDictionaryBenchmark.Dictionary_Insert(ItemCount: 1000)', 12780.6, 134.9, 14072.8, 966.2],
  ['CompressedIntSetBenchmark.HashSet_Union(ItemCount: 1000)', 34161.6, 4392.5, 27518.2, 446.8],
  ['CelerityMultiMapBenchmark.Dictionary_Lookup(ItemCount: 1000)', 3732.9, 198.9, 3362.1, 34.0],
  ['CelerityMultiMapBenchmark.Dictionary_Remove(ItemCount: 1000)', 20905.7, 1438.3, 32881.8, 5033.1],
  ['TopKSketchBenchmark.Dictionary_TopK(ItemCount: 1000)', 31.1, 1.7, 28.0, 0.5],
  ['TrieBenchmark.Trie_PrefixMatch(ItemCount: 100000)', 8333433.0, 1170557.0, 6119614.7, 110363.8],
  ['TrieBenchmark.Trie_SpanLookup(ItemCount: 100000)', 10914909.6, 100245.4, 9784525.7, 293232.4],
];

// The rule this file replaced, kept only so the improvement above is pinned rather than
// asserted: ±10% and a *summed* one-sigma bar.
function legacyClassify(prMean, prStdDev, baseMean, baseStdDev, thresholdRatio) {
  const ratio = prMean / baseMean;
  const beyondNoise = Math.abs(prMean - baseMean) > (prStdDev + baseStdDev);
  if (ratio >= thresholdRatio && beyondNoise) return 'regression';
  if (ratio <= 1 / thresholdRatio && beyondNoise) return 'improvement';
  return null;
}

function statsFor(mean, sd) {
  return { Mean: mean, StandardDeviation: sd };
}

function selfTest() {
  let failures = 0;
  let checks = 0;
  const check = (label, actual, expected) => {
    checks++;
    const a = JSON.stringify(actual);
    const e = JSON.stringify(expected);
    if (a !== e) {
      failures++;
      console.error(`FAIL  ${label}\n      expected ${e}\n      actual   ${a}`);
    }
  };

  // ---- unit formatting ----
  check('formatNs(0)', formatNs(0), '0.0 ns');
  check('formatNs(999.94)', formatNs(999.94), '999.9 ns');
  check('formatNs(1000)', formatNs(1000), '1.00 μs');
  check('formatNs(1e6)', formatNs(1e6), '1.00 ms');
  check('formatNs(1e9)', formatNs(1e9), '1.00 s');
  check('formatNs(null)', formatNs(null), 'n/a');

  // ---- the calibration this file exists for (#351) ----
  const legacyFlags = ZERO_IL_FALSE_POSITIVES
    .filter(([, pm, ps, bm, bs]) => legacyClassify(pm, ps, bm, bs, DEFAULT_THRESHOLD_RATIO) !== null);
  check('legacy rule flags every row in the zero-IL sample', legacyFlags.length, ZERO_IL_FALSE_POSITIVES.length);

  const stillFlagged = ZERO_IL_FALSE_POSITIVES
    .filter(([, pm, ps, bm, bs]) => classifyDelta(pm, ps, bm, bs, DEFAULT_THRESHOLD_RATIO, DEFAULT_NOISE_SIGMAS) !== null)
    .map(([name]) => name);
  // Both survivors are large shifts with a tight spread on *both* sides, which no
  // single-pass rule can separate from a real change. If this list grows, the guard has
  // been loosened; if it shrinks to zero on a change that was not about the guard, the
  // sample or the parsing has drifted.
  check('3σ quadrature rule on the same sample', stillFlagged, [
    'SmallDictionaryBenchmark.Dictionary_Remove(ItemCount: 64)',
    'TrieBenchmark.Trie_SpanLookup(ItemCount: 100000)',
  ]);

  // ---- the rule itself ----
  // Past ±10% but inside 3σ: not a flag. 100 vs 90 is +11.1%, and 3 * hypot(4, 4) = 17.0.
  check('inside 3σ', classifyDelta(100, 4, 90, 4, 1.10, 3), null);
  // Same delta with a tight spread on both sides: 3 * hypot(1, 1) = 4.24 < 10.
  check('beyond 3σ', classifyDelta(100, 1, 90, 1, 1.10, 3), 'regression');
  // Beyond 3σ but inside the ratio gate: not a flag. Precision is not materiality.
  check('inside the ratio gate', classifyDelta(105, 0.1, 100, 0.1, 1.10, 3), null);
  // The improvement side is the multiplicative mirror, so 1/1.10 = 0.909... qualifies and
  // a plain -9% does not.
  check('improvement at 1/threshold', classifyDelta(90, 1, 100, 1, 1.10, 3), 'improvement');
  check('improvement just inside the gate', classifyDelta(91, 1, 100, 1, 1.10, 3), null);
  // A side with no reported dispersion cannot be judged on noise, so the ratio gate alone
  // decides rather than the row being silently dropped.
  check('zero dispersion falls back to the ratio gate', classifyDelta(120, 0, 100, 0, 1.10, 3), 'regression');
  check('null dispersion is treated as zero', classifyDelta(120, null, 100, undefined, 1.10, 3), 'regression');
  // The old rule's headline false positive, at the real numbers.
  check('legacy flags Dictionary_Remove(64)', legacyClassify(6465.2, 219.1, 4857.3, 168.2, 1.10), 'regression');
  // ... and one it should never have: 4707 -> 5372.3 is +14.1%, but 3σ is 3 * hypot(525.2,
  // 117.8) = 1614.7 against a 665.3 gap.
  check('3σ clears PooledCelerityDictionary_Lookup', classifyDelta(5372.3, 525.2, 4707.0, 117.8, 1.10, 3), null);

  // ---- spread profile ----
  const profile = spreadProfile([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
  check('spread profile', [profile.n, profile.p50, profile.p90, profile.p95], [10, 5, 9, 10]);
  check('spread profile of nothing', spreadProfile([]), null);
  // Nearest-rank, so every reported figure is a value that appears in the sample. Pinned
  // because the footer names the statistics after this definition.
  check('percentiles are order statistics, not interpolations', spreadProfile([1, 2]).p50, 1);

  // ---- comment composition ----
  const prReport = {
    Benchmarks: [
      { FullName: 'ABenchmark.A_Slow(ItemCount: 8)', Statistics: statsFor(120, 1) },
      { FullName: 'ABenchmark.A_Steady(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'ABenchmark.A_Noisy(ItemCount: 8)', Statistics: statsFor(120, 40) },
      { FullName: 'ABenchmark.A_New(ItemCount: 8)', Statistics: statsFor(50, 1) },
      { FullName: 'ABenchmark.A_Errored(ItemCount: 8)', Statistics: null, Measurements: [] },
      { FullName: 'ABenchmark.A_BaseErrored(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'StringHasherBenchmark.Fnv1a(Length: 8)', Statistics: statsFor(10, 0.1) },
    ],
  };
  const baseReport = {
    Benchmarks: [
      { FullName: 'ABenchmark.A_Slow(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'ABenchmark.A_Steady(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'ABenchmark.A_Noisy(ItemCount: 8)', Statistics: statsFor(100, 40) },
      { FullName: 'ABenchmark.A_Errored(ItemCount: 8)', Statistics: statsFor(100, 1) },
      // Present, but the base side of this pair produced no statistics.
      { FullName: 'ABenchmark.A_BaseErrored(ItemCount: 8)', Statistics: null },
      { FullName: 'StringHasherBenchmark.Fnv1a(Length: 8)', Statistics: statsFor(10, 0.1) },
    ],
  };

  const built = buildComment(prReport, baseReport, { baseSha: 'abcdef1234', shardTotal: '8' });
  check('regressions', built.regressions, 1);
  check('improvements', built.improvements, 0);
  check('errored', built.errored, 2);
  check('marker', built.body.startsWith(COMMENT_MARKER), true);
  check('highlights hold only the flagged row', (built.body.match(/A_Slow/g) || []).length, 2);
  check('a noisy row is not flagged', built.body.includes('A_Noisy(ItemCount: 8)` | 120.0 ns | 40.0 ns | 100.0 ns | +20.0% |'), true);
  check('a new row is reported as new', built.body.includes('🆕 new'), true);
  check('an errored row is reported', built.body.includes('⚠️ errored'), true);
  // An unusable comparison must reach the count, not just the table: a subtitle reading
  // "No significant change" over a pair that could not be compared is false reassurance.
  check('a base-side failure is reported', built.body.includes('⚠️ base errored'), true);
  check('a base-side failure reaches the subtitle', built.body.includes('2 benchmarks produced no usable comparison'), true);
  check('hashers get their own section', built.body.includes('<summary><b>Hashers</b> (1)</summary>'), true);
  check('collections section excludes hashers', built.body.includes('<summary><b>Collections</b> (6)</summary>'), true);
  // The errored row has no delta, the base-errored row has no base statistics, and the new
  // row has no base at all. Four rows remain.
  check('spread profile counts only paired rows', built.spread.n, 4);
  check('the spread is published', built.body.includes('**Observed spread of this run**'), true);
  check('the spread is not called a floor', /noise floor/i.test(built.body), false);
  check('no incomplete banner by default', built.body.includes('[!WARNING]'), false);

  const partial = buildComment(prReport, baseReport, { missingShards: '3, 5' });
  check('incomplete banner', partial.body.includes('Shard(s) `3, 5` produced no measurements'), true);

  const clean = buildComment(
    { Benchmarks: [{ FullName: 'ABenchmark.A_Steady(ItemCount: 8)', Statistics: statsFor(100, 1) }] },
    { Benchmarks: [{ FullName: 'ABenchmark.A_Steady(ItemCount: 8)', Statistics: statsFor(100, 1) }] },
    {},
  );
  check('clean run subtitle', clean.body.includes('No significant change vs main'), true);
  check('clean run has no highlights', clean.body.includes('**Highlights**'), false);

  // Zero sigmas is a legitimate setting — the ratio gate alone — and must not be read as
  // "unset" anywhere between the environment and the rule.
  const noGuard = buildComment(prReport, baseReport, { noiseSigmas: 0 });
  check('zero sigmas leaves the ratio gate alone', [noGuard.regressions, noGuard.improvements], [2, 0]);
  check('zero sigmas is not replaced by the default', noGuard.noiseSigmas, 0);

  // A degenerate base must not poison the published percentiles.
  const degenerate = buildComment(
    { Benchmarks: [
      { FullName: 'ABenchmark.A_Ok(ItemCount: 8)', Statistics: statsFor(102, 1) },
      { FullName: 'ABenchmark.A_ZeroBase(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'ABenchmark.A_ZeroPr(ItemCount: 8)', Statistics: statsFor(0, 0) },
      { FullName: 'ABenchmark.A_NanPr(ItemCount: 8)', Statistics: statsFor(NaN, 1) },
    ] },
    { Benchmarks: [
      { FullName: 'ABenchmark.A_Ok(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'ABenchmark.A_ZeroBase(ItemCount: 8)', Statistics: statsFor(0, 0) },
      { FullName: 'ABenchmark.A_ZeroPr(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'ABenchmark.A_NanPr(ItemCount: 8)', Statistics: statsFor(100, 1) },
    ] },
    {},
  );
  check('a non-finite delta is kept out of the spread profile', [degenerate.spread.n, degenerate.spread.p95.toFixed(1)], [1, '2.0']);
  // A zero base makes the ratio Infinity, a zero PR mean makes it 0. Both used to clear a
  // gate; neither may now be reported as a change.
  check('a broken comparison is never flagged', [degenerate.regressions, degenerate.improvements], [0, 0]);
  check('a broken comparison is counted and shown', [degenerate.errored, (degenerate.body.match(/not comparable/g) || []).length], [3, 3]);
  check('no Infinity or NaN reaches the table', /Infinity|NaN/.test(degenerate.body), false);
  check('the subtitle owns up to it', degenerate.body.includes('3 benchmarks produced no usable comparison'), true);
  check('classifyDelta refuses a zero base', classifyDelta(100, 1, 0, 0, 1.10, 3), null);
  check('classifyDelta refuses a zero pr mean', classifyDelta(0, 0, 100, 1, 1.10, 3), null);
  check('formatNs(NaN)', formatNs(NaN), 'n/a');

  // Run outside Actions, neither BASE_SHA nor SHARD_TOTAL is set. The footer has to stay
  // readable rather than rendering an empty backtick pair and a shard count of nothing.
  check('footer names the base sha when it has one', built.body.includes('main (`abcdef1`)'), true);
  check('footer says sharded when it knows the width', built.body.includes('sharded 8-way'), true);
  check('footer degrades without a base sha', clean.body.includes('<sub>Same-runner A/B: main and this PR'), true);
  check('footer leaves no empty backticks', clean.body.includes('``'), false);
  check('footer leaves no orphan shard count', clean.body.includes('-way'), false);

  // An empty base (every shard's base run produced nothing) must still render.
  const noBase = buildComment(prReport, { Benchmarks: [] }, {});
  check('an empty base still renders', noBase.regressions, 0);
  check('an empty base reports every row as new', (noBase.body.match(/🆕 new/g) || []).length, 6);

  // ---- settings ----
  // Unset means "use the calibrated default"; a value that would quietly change what a
  // flag means is refused rather than accepted.
  const ratio = (raw) => readSetting('ALERT_THRESHOLD_RATIO', raw, DEFAULT_THRESHOLD_RATIO, (v) => v > 1, 'x');
  const sigmas = (raw) => readSetting('ALERT_NOISE_SIGMAS', raw, DEFAULT_NOISE_SIGMAS, (v) => v >= 0, 'x');
  const refused = (fn, raw) => { try { fn(raw); return false; } catch { return true; } };

  check('unset falls back to the default', [ratio(undefined), ratio(''), ratio('  ')], [1.10, 1.10, 1.10]);
  check('a valid override is taken', [ratio('1.25'), sigmas('2.5')], [1.25, 2.5]);
  check('zero sigmas survives the parser', sigmas('0'), 0);
  // 1 makes every row past the gate; below 1 swaps the regression and improvement arms.
  check('a threshold of 1 is refused', refused(ratio, '1'), true);
  check('a threshold below 1 is refused', refused(ratio, '0.9'), true);
  // Negative sigmas makes the noise guard vacuously true — the defect this file fixes.
  check('negative sigmas is refused', refused(sigmas, '-1'), true);
  check('a non-numeric setting is refused', [refused(ratio, 'high'), refused(sigmas, 'lots')], [true, true]);
  // parseFloat('1.10abc') is 1.10; Number() is NaN. The stricter reading is the right one
  // for a setting that decides what gets called a regression.
  check('a partly-numeric setting is refused', refused(ratio, '1.10abc'), true);

  if (failures > 0) {
    console.error(`\n${failures} of ${checks} check(s) failed.`);
    process.exit(1);
  }
  console.log(`ok: ${checks} benchmark-comment check(s) pinned.`);
}

// ---- entry point ----------------------------------------------------------------------

function argValue(args, flag) {
  const i = args.indexOf(flag);
  if (i < 0) return null;
  if (i + 1 >= args.length) {
    console.error(`${flag} requires a value.`);
    process.exit(1);
  }
  return args[i + 1];
}

function main() {
  const args = process.argv.slice(2);

  if (args.includes('--self-test')) {
    selfTest();
    return;
  }

  const prPath = argValue(args, '--pr');
  const basePath = argValue(args, '--base');
  const outPath = argValue(args, '--out');
  if (!prPath || !basePath || !outPath) {
    console.error('usage: benchmark_comment.js --pr <pr.json> --base <base.json> --out <body.md>');
    console.error('       benchmark_comment.js --self-test');
    process.exit(1);
  }

  let thresholdRatio;
  let noiseSigmas;
  try {
    thresholdRatio = readSetting(
      'ALERT_THRESHOLD_RATIO', process.env.ALERT_THRESHOLD_RATIO, DEFAULT_THRESHOLD_RATIO,
      (v) => v > 1, 'a ratio greater than 1 (1.10 flags a row that moved by a tenth)');
    // Zero is allowed and means "ratio gate only" — a legitimate way to see every row past
    // the threshold regardless of its spread.
    noiseSigmas = readSetting(
      'ALERT_NOISE_SIGMAS', process.env.ALERT_NOISE_SIGMAS, DEFAULT_NOISE_SIGMAS,
      (v) => v >= 0, 'zero or more sigmas');
  } catch (err) {
    console.error(err.message);
    process.exit(1);
  }

  const result = buildComment(
    JSON.parse(fs.readFileSync(prPath, 'utf8')),
    JSON.parse(fs.readFileSync(basePath, 'utf8')),
    {
      thresholdRatio,
      noiseSigmas,
      missingShards: process.env.MISSING_SHARDS,
      baseSha: process.env.BASE_SHA,
      shardTotal: process.env.SHARD_TOTAL,
    },
  );

  fs.writeFileSync(outPath, result.body);

  console.log(`Wrote ${outPath}: ${result.entries.length} row(s), ${result.regressions} regression(s), ` +
    `${result.improvements} improvement(s), ${result.errored} errored ` +
    `(±${((result.thresholdRatio - 1) * 100).toFixed(0)}% and ${result.noiseSigmas}σ).`);
  if (result.spread) {
    console.log(`Observed spread over ${result.spread.n} paired row(s): p50 ` +
      `${result.spread.p50.toFixed(1)}%, p90 ${result.spread.p90.toFixed(1)}%, p95 ${result.spread.p95.toFixed(1)}%.`);
  }
}

if (require.main === module) {
  main();
}

module.exports = { buildComment, classifyDelta, formatNs, isComparable, spreadProfile, readSetting, COMMENT_MARKER };
