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
// The measured noise floor of that run, which is the reason no purely per-row rule can
// reach zero: |Δ| has a median of 0.7% and a p90 of 6.2%, but 43 rows exceed ±10%,
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
// can. That residual is why the comment publishes the run's measured noise floor rather
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
  if (ns == null) return 'n/a';
  if (ns < 1000) return `${ns.toFixed(1)} ns`;
  if (ns < 1_000_000) return `${(ns / 1000).toFixed(2)} μs`;
  if (ns < 1_000_000_000) return `${(ns / 1_000_000).toFixed(2)} ms`;
  return `${(ns / 1_000_000_000).toFixed(2)} s`;
}

// The flag decision for one paired row, kept as a named function so the self-test can
// exercise it directly against the real measurements that motivated it.
//
// `spread` is 0 when BenchmarkDotNet reported no dispersion for a side (a degenerate
// single-measurement case). The guard then reduces to the ratio gate alone, which is the
// behaviour the previous rule had for the same input — this decides how much evidence a
// delta needs, and it must not silently drop a row it cannot judge.
function classifyDelta(prMean, prStdDev, baseMean, baseStdDev, thresholdRatio, noiseSigmas) {
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

// Nearest-rank percentile over an already-sorted ascending array.
function percentile(sorted, p) {
  if (sorted.length === 0) return null;
  const rank = Math.ceil((p / 100) * sorted.length);
  return sorted[Math.min(sorted.length - 1, Math.max(0, rank - 1))];
}

// The run's own noise floor, measured rather than assumed (#351). Any one pull request
// changes a handful of the ~750 tracked cases, so the spread of the whole delta
// distribution is dominated by what the runner did between the head slice and the base
// slice. Publishing it on every comment means the threshold can be argued about against
// evidence, and a run that was unusually hostile says so instead of looking clean.
function noiseProfile(deltaPercents) {
  if (deltaPercents.length === 0) return null;
  const sorted = [...deltaPercents].sort((a, b) => a - b);
  return {
    n: sorted.length,
    median: percentile(sorted, 50),
    p90: percentile(sorted, 90),
    p95: percentile(sorted, 95),
  };
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
    let deltaCell = baseMap.has(name) ? '⚠️ base errored' : '🆕 new';
    let flag = null;

    if (baseStats) {
      const baseMean = baseStats.Mean;
      const pct = (prMean / baseMean - 1) * 100;
      deltaCell = `${pct >= 0 ? '+' : ''}${pct.toFixed(1)}%`;
      // A degenerate base (a zero or missing mean) yields Infinity or NaN, which would
      // sort to the end of the noise profile and drag its upper percentiles with it. The
      // row still gets a delta cell and a flag; it just does not calibrate anything.
      if (Number.isFinite(pct)) deltaPercents.push(Math.abs(pct));

      flag = classifyDelta(prMean, prStdDev, baseMean, baseStats.StandardDeviation, thresholdRatio, noiseSigmas);
      if (flag === 'regression') { deltaCell += ' ⚠️'; regressions++; }
      else if (flag === 'improvement') { deltaCell += ' ✅'; improvements++; }
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
    subtitle += ` ⚠️ ${errored} benchmark${errored === 1 ? '' : 's'} errored (no measurements) — see the table.`;
  }

  const baseSha = (options.baseSha || '').slice(0, 7);
  const footer = [
    `<sub>Same-runner A/B (sharded ${options.shardTotal || ''}-way): main (\`${baseSha}\`) and this PR were ` +
    `built and benchmarked back-to-back on the same runner per shard, so hardware variance cancels out. ` +
    `⚠️ = PR mean is at least ${thresholdPct}% slower than main **and** the gap exceeds ${noiseSigmas}σ of the two ` +
    `measurements' combined standard deviation; ✅ = correspondingly faster.</sub>`,
  ];

  const noise = noiseProfile(deltaPercents);
  if (noise) {
    // Stated as what it is: a floor, not a verdict. A reviewer comparing a flagged row
    // against p95 can see immediately whether it stands out from the run it came in.
    footer.push('');
    footer.push(
      `<sub>**Measured noise floor for this run** — |Δ| across all ${noise.n} paired rows, of which a pull ` +
      `request changes only a handful, so the rest is the runner's own drift: median ` +
      `${noise.median.toFixed(1)}%, p90 ${noise.p90.toFixed(1)}%, p95 ${noise.p95.toFixed(1)}%. ` +
      `Read a flag near that p95 with suspicion, and re-run before acting on it.</sub>`
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

  return { body, entries, regressions, improvements, errored, noise, missingShards, thresholdRatio, noiseSigmas };
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

  // ---- noise profile ----
  const profile = noiseProfile([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
  check('noise profile', [profile.n, profile.median, profile.p90, profile.p95], [10, 5, 9, 10]);
  check('noise profile of nothing', noiseProfile([]), null);

  // ---- comment composition ----
  const prReport = {
    Benchmarks: [
      { FullName: 'ABenchmark.A_Slow(ItemCount: 8)', Statistics: statsFor(120, 1) },
      { FullName: 'ABenchmark.A_Steady(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'ABenchmark.A_Noisy(ItemCount: 8)', Statistics: statsFor(120, 40) },
      { FullName: 'ABenchmark.A_New(ItemCount: 8)', Statistics: statsFor(50, 1) },
      { FullName: 'ABenchmark.A_Errored(ItemCount: 8)', Statistics: null, Measurements: [] },
      { FullName: 'StringHasherBenchmark.Fnv1a(Length: 8)', Statistics: statsFor(10, 0.1) },
    ],
  };
  const baseReport = {
    Benchmarks: [
      { FullName: 'ABenchmark.A_Slow(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'ABenchmark.A_Steady(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'ABenchmark.A_Noisy(ItemCount: 8)', Statistics: statsFor(100, 40) },
      { FullName: 'ABenchmark.A_Errored(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'StringHasherBenchmark.Fnv1a(Length: 8)', Statistics: statsFor(10, 0.1) },
    ],
  };

  const built = buildComment(prReport, baseReport, { baseSha: 'abcdef1234', shardTotal: '8' });
  check('regressions', built.regressions, 1);
  check('improvements', built.improvements, 0);
  check('errored', built.errored, 1);
  check('marker', built.body.startsWith(COMMENT_MARKER), true);
  check('highlights hold only the flagged row', (built.body.match(/A_Slow/g) || []).length, 2);
  check('a noisy row is not flagged', built.body.includes('A_Noisy(ItemCount: 8)` | 120.0 ns | 40.0 ns | 100.0 ns | +20.0% |'), true);
  check('a new row is reported as new', built.body.includes('🆕 new'), true);
  check('an errored row is reported', built.body.includes('⚠️ errored'), true);
  check('hashers get their own section', built.body.includes('<summary><b>Hashers</b> (1)</summary>'), true);
  check('collections section excludes hashers', built.body.includes('<summary><b>Collections</b> (5)</summary>'), true);
  // The errored row has no delta, so it is outside the noise profile; the new row has no
  // base and is outside it too. Four rows remain.
  check('noise profile counts only paired rows', built.noise.n, 4);
  check('noise floor is published', built.body.includes('**Measured noise floor for this run**'), true);
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

  // A degenerate base must not poison the published noise floor.
  const degenerate = buildComment(
    { Benchmarks: [
      { FullName: 'ABenchmark.A_Ok(ItemCount: 8)', Statistics: statsFor(102, 1) },
      { FullName: 'ABenchmark.A_ZeroBase(ItemCount: 8)', Statistics: statsFor(100, 1) },
    ] },
    { Benchmarks: [
      { FullName: 'ABenchmark.A_Ok(ItemCount: 8)', Statistics: statsFor(100, 1) },
      { FullName: 'ABenchmark.A_ZeroBase(ItemCount: 8)', Statistics: statsFor(0, 0) },
    ] },
    {},
  );
  check('a non-finite delta is kept out of the noise profile', [degenerate.noise.n, degenerate.noise.p95.toFixed(1)], [1, '2.0']);

  // An empty base (every shard's base run produced nothing) must still render.
  const noBase = buildComment(prReport, { Benchmarks: [] }, {});
  check('an empty base still renders', noBase.regressions, 0);
  check('an empty base reports every row as new', (noBase.body.match(/🆕 new/g) || []).length, 5);

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

  const thresholdRatio = parseFloat(process.env.ALERT_THRESHOLD_RATIO || '');
  const noiseSigmas = parseFloat(process.env.ALERT_NOISE_SIGMAS || '');

  const result = buildComment(
    JSON.parse(fs.readFileSync(prPath, 'utf8')),
    JSON.parse(fs.readFileSync(basePath, 'utf8')),
    {
      // Number.isFinite, not `||`: setting ALERT_NOISE_SIGMAS to 0 is a legitimate way to
      // turn the noise guard off and leave the ratio gate alone, and `0 || 3` would
      // silently reinstate the guard instead.
      thresholdRatio: Number.isFinite(thresholdRatio) ? thresholdRatio : DEFAULT_THRESHOLD_RATIO,
      noiseSigmas: Number.isFinite(noiseSigmas) ? noiseSigmas : DEFAULT_NOISE_SIGMAS,
      missingShards: process.env.MISSING_SHARDS,
      baseSha: process.env.BASE_SHA,
      shardTotal: process.env.SHARD_TOTAL,
    },
  );

  fs.writeFileSync(outPath, result.body);

  console.log(`Wrote ${outPath}: ${result.entries.length} row(s), ${result.regressions} regression(s), ` +
    `${result.improvements} improvement(s), ${result.errored} errored ` +
    `(±${((result.thresholdRatio - 1) * 100).toFixed(0)}% and ${result.noiseSigmas}σ).`);
  if (result.noise) {
    console.log(`Measured noise floor over ${result.noise.n} paired row(s): median ` +
      `${result.noise.median.toFixed(1)}%, p90 ${result.noise.p90.toFixed(1)}%, p95 ${result.noise.p95.toFixed(1)}%.`);
  }
}

if (require.main === module) {
  main();
}

module.exports = { buildComment, classifyDelta, formatNs, noiseProfile, COMMENT_MARKER };
