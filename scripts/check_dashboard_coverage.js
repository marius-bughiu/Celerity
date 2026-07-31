#!/usr/bin/env node
//
// Fails when the benchmark dashboard would silently drop what it was asked to render.
//
// The dashboard parses BenchmarkDotNet result names with regexes, and a name that does
// not match is dropped without a trace: the data publishes to gh-pages correctly and the
// card just renders blank. That has happened twice — EnumMap / EnumSet declare no
// [Params] at all, and DisjointSet once named its params property ElementCount — and
// neither produced any CI signal. The same silence applies to the card *labels*: a title
// like `IntDictionary<int>` written straight to innerHTML has its generic parameters
// parsed as a start tag and swallowed, so the card renders with a truncated heading.
//
// This check closes those gaps. It lifts the COLLECTIONS tables and the two name parsers
// straight out of the dashboard HTML rather than reimplementing them, so it validates
// the parser that actually ships and cannot drift from it.
//
// Structural checks, run on every PR (no benchmark run needed):
//   1. index.html and detail.html agree on the collection keys and their item counts;
//   2. every charted collection has a matching benchmark class registered in the
//      CoreBenchmarks array of src/Celerity.Benchmarks/Program.cs;
//   3. no markup-shaped label is concatenated into an innerHTML template unescaped.
//
// Report checks, run in the benchmark job once the sharded suite has been merged:
//   4. every benchmark name in the report is understood by one of the dashboard parsers;
//   5. every (collection, op) pair the dashboard draws a card for resolves to both a BCL
//      and a Celerity measurement.
//
// Usage:
//   node scripts/check_dashboard_coverage.js                             # 1-3 only
//   node scripts/check_dashboard_coverage.js <joined-report-full.json>   # 1-5
// Run from the repository root.

'use strict';

const fs = require('fs');
const path = require('path');

const INDEX_HTML = path.join('web', 'dev', 'bench', 'index.html');
const DETAIL_HTML = path.join('web', 'dev', 'bench', 'detail.html');
const PROGRAM_CS = path.join('src', 'Celerity.Benchmarks', 'Program.cs');
const SELF = path.join('scripts', 'check_dashboard_coverage.js');

function fail(message) {
  console.error(`error: ${message}`);
  process.exit(1);
}

// ---- Lift declarations out of the dashboard source ----------------------------------
// Each pattern is anchored on the closing token at its known indentation, so a partial
// match is impossible: either the whole declaration comes out or extraction fails loudly.

function extract(source, file, label, pattern) {
  const m = source.match(pattern);
  if (!m) {
    fail(
      `could not extract ${label} from ${file}. The dashboard source was restructured; ` +
      `update the patterns in ${SELF} to match.`
    );
  }
  return m[0];
}

function loadDashboard(file) {
  const source = fs.readFileSync(file, 'utf8');
  const parts = [
    extract(source, file, 'NO_SWEEP', /var NO_SWEEP = [^;]+;/),
    extract(source, file, 'BCL_TYPES', /var BCL_TYPES = new Set\(\[[^\]]*\]\);/),
    extract(source, file, 'COLLECTIONS', /var COLLECTIONS = \[[\s\S]*?\n {2}\];/),
    extract(source, file, 'parseName', /function parseName\(name\) \{[\s\S]*?\n {2}\}/),
  ];
  // Only index.html carries the key builder and the hasher parser; detail.html is
  // consulted for its COLLECTIONS table alone.
  const idxKey = source.match(/function idxKey\([\s\S]*?\n {2}\}/);
  const parseHasher = source.match(/function parseHasher\(name, value\) \{[\s\S]*?\n {2}\}/);
  if (idxKey) parts.push(idxKey[0]);
  if (parseHasher) parts.push(parseHasher[0]);
  parts.push(
    'return { NO_SWEEP: NO_SWEEP, COLLECTIONS: COLLECTIONS, parseName: parseName,' +
    ' idxKey: typeof idxKey === "function" ? idxKey : null,' +
    ' parseHasher: typeof parseHasher === "function" ? parseHasher : null };'
  );
  return new Function(parts.join('\n'))();
}

// The CI-tracked suite, as `{Prefix}Benchmark` type names.
function loadCoreBenchmarks() {
  const source = fs.readFileSync(PROGRAM_CS, 'utf8');
  const block = source.match(/CoreBenchmarks\s*=\s*\{([\s\S]*?)\};/);
  if (!block) {
    fail(`could not find the CoreBenchmarks array in ${PROGRAM_CS}; update the pattern in ${SELF}.`);
  }
  return new Set([...block[1].matchAll(/typeof\((\w+)\)/g)].map((m) => m[1]));
}

function loadBenchmarkNames(reportPath) {
  let report;
  try {
    report = JSON.parse(fs.readFileSync(reportPath, 'utf8'));
  } catch (e) {
    fail(`could not read ${reportPath}: ${e.message}`);
  }
  if (!report || !Array.isArray(report.Benchmarks)) {
    fail(`${reportPath} has no Benchmarks array — is it a BenchmarkDotNet *-report-full.json?`);
  }
  return report.Benchmarks.map((b) => b.FullName).filter(Boolean);
}

// ---- Unescaped-label detection ------------------------------------------------------
// The COLLECTIONS titles and `vs` baselines are trusted in-repo literals, but they are
// markup-shaped: `IntDictionary<int>` concatenated into an innerHTML template is parsed
// as a start tag, so the visible heading loses its generic parameters (and, for
// `EnumSet<TEnum>`, materializes a stray <tenum> element). Every such label has to pass
// through the page's escapeHtml() on its way to an HTML sink.
//
// Both concatenation directions are matched, but only inside a statement that is building
// markup: one feeding an HTML sink, or one splicing the label into a string literal that
// opens a tag. Concatenating a label anywhere else is fine — the attribute API, textContent
// and a query string all take text, not markup — so those are not flagged.
const LABEL_FIELDS = '(?:title|vs|sub)';
const LABEL_OWNERS = '(?:col|collection|meta|c)';
const RAW_LABEL = new RegExp(
  `\\+\\s*${LABEL_OWNERS}\\.${LABEL_FIELDS}\\b|\\b${LABEL_OWNERS}\\.${LABEL_FIELDS}\\s*\\+`
);
const MARKUP_CONTEXT = /\.(?:inner|outer)HTML\b|insertAdjacentHTML\(|document\.write\(|['"]</;

// A template is routinely spread over several source lines, and the sink that says whether
// it is markup at all sits on the first of them — so lines are folded into logical
// statements first, joining any line that ends on a continuation token with the next.
const CONTINUES = /[,+(=]$|&&$|\|\|$/;

function logicalLines(source) {
  const raw = source.split(/\r?\n/);
  const out = [];
  let buf = null;
  raw.forEach((line, i) => {
    const trimmed = line.trim();
    if (buf === null) buf = { line: i + 1, text: trimmed };
    else buf.text += ' ' + trimmed;
    if (!CONTINUES.test(trimmed)) {
      out.push(buf);
      buf = null;
    }
  });
  if (buf !== null) out.push(buf);
  return out;
}

function findRawLabels(file) {
  return logicalLines(fs.readFileSync(file, 'utf8'))
    .filter((s) => MARKUP_CONTEXT.test(s.text) && RAW_LABEL.test(s.text))
    .map((s) => ({
      line: s.line,
      match: s.text.match(RAW_LABEL)[0].trim(),
      text: s.text.length > 160 ? s.text.slice(0, 157) + '...' : s.text,
    }));
}

// ---- Checks -------------------------------------------------------------------------

function main() {
  const reportPath = process.argv[2] || null;

  const index = loadDashboard(INDEX_HTML);
  const detail = loadDashboard(DETAIL_HTML);
  if (!index.idxKey) fail(`could not extract idxKey from ${INDEX_HTML}`);
  if (!index.parseHasher) fail(`could not extract parseHasher from ${INDEX_HTML}`);

  const problems = [];

  // (1) The two tables must agree, or a card that renders on the grid dead-ends on click.
  const detailByKey = new Map(detail.COLLECTIONS.map((c) => [c.key, c]));
  for (const col of index.COLLECTIONS) {
    const twin = detailByKey.get(col.key);
    if (!twin) {
      problems.push(`${col.key} is charted in ${INDEX_HTML} but missing from ${DETAIL_HTML}'s COLLECTIONS — its detail page will show "Unknown benchmark".`);
      continue;
    }
    const a = JSON.stringify(col.items || null);
    const b = JSON.stringify(twin.items || null);
    if (a !== b) {
      problems.push(`${col.key} declares items ${a} in ${INDEX_HTML} but ${b} in ${DETAIL_HTML} — the detail page will reject the item count the grid links to.`);
    }
  }
  for (const col of detail.COLLECTIONS) {
    if (!index.COLLECTIONS.some((c) => c.key === col.key)) {
      problems.push(`${col.key} is listed in ${DETAIL_HTML} but missing from ${INDEX_HTML}'s COLLECTIONS — it has no card on the grid.`);
    }
  }

  // (2) A charted collection whose class is not in the CI suite can never receive data.
  const core = loadCoreBenchmarks();
  for (const col of index.COLLECTIONS) {
    if (!core.has(`${col.key}Benchmark`)) {
      problems.push(`${col.key} is charted in ${INDEX_HTML} but ${col.key}Benchmark is not registered in the CoreBenchmarks array of ${PROGRAM_CS} — it will never be measured in CI.`);
    }
  }

  // (3) A markup-shaped label written raw to innerHTML renders truncated.
  for (const file of [INDEX_HTML, DETAIL_HTML]) {
    for (const hit of findRawLabels(file)) {
      problems.push(
        `${file}:${hit.line} concatenates \`${hit.match}\` into a markup string without escapeHtml() — ` +
        `a generic type name in that label is parsed as a tag and dropped from the rendered heading. ` +
        `Line: ${hit.text}`
      );
    }
  }

  if (reportPath) {
    const names = loadBenchmarkNames(reportPath);

    // (4) Nothing in the report may be silently unrenderable.
    const unparsed = names.filter((n) => !index.parseName(n) && !index.parseHasher(n, 0));
    if (unparsed.length > 0) {
      const classes = [...new Set(unparsed.map((n) => n.split('.')[0]))];
      problems.push(
        `${unparsed.length} benchmark name(s) match no dashboard parser and would be dropped at render time, ` +
        `from: ${classes.join(', ')}. First: "${unparsed[0]}". ` +
        `The usual cause is a [Params] property not named ItemCount — rename it, or teach both dashboard parsers the new shape.`
      );
    }

    // (5) Every card the dashboard draws must have both series behind it.
    const idx = {};
    for (const name of names) {
      const p = index.parseName(name);
      if (!p) continue;
      const key = index.idxKey(p.collection, p.op, p.itemCount);
      if (!idx[key]) idx[key] = {};
      idx[key][p.isBcl ? 'bcl' : 'celerity'] = true;
    }

    for (const col of index.COLLECTIONS) {
      // The primary count, exactly as index.html picks it: a card's ratio text may fall
      // back to the smaller count, but its chart is always seriesFor(..., primaryN), and
      // that is what decides between a sparkline and "awaiting data". So the primary
      // count is the one that has to resolve.
      const items = col.items || [1000, 100000];
      const primaryN = items[items.length - 1];
      for (const op of col.ops) {
        const pair = idx[index.idxKey(col.key, op, primaryN)];
        if (!pair || !pair.bcl || !pair.celerity) {
          const at = primaryN == null ? 'no sweep' : primaryN;
          problems.push(`${col.key}.${op} has no BCL+Celerity pair at ${at} — that card renders "awaiting data".`);
        }
      }
    }
  }

  if (problems.length > 0) {
    console.error(`Dashboard coverage check failed (${problems.length} problem(s)):\n`);
    for (const p of problems) console.error(`  - ${p}`);
    console.error(
      `\nEvery collection in the COLLECTIONS arrays of ${INDEX_HTML} and ${DETAIL_HTML} must be measured ` +
      `by the CI suite and must resolve to real measurements. See CONTRIBUTING.md, "The dashboard".`
    );
    process.exit(1);
  }

  const cards = index.COLLECTIONS.reduce((acc, c) => acc + c.ops.length, 0);
  console.log(
    `Dashboard coverage OK: ${cards} cards across ${index.COLLECTIONS.length} collections` +
    (reportPath ? ` resolve from the joined report.` : ` are wired to registered benchmark classes (structural checks only — pass a joined report to verify the data).`)
  );
}

main();
