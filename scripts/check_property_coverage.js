#!/usr/bin/env node
//
// Fails when a collection type in `Celerity.Collections` has no randomized oracle coverage.
//
// The 100% line-and-branch gate proves every branch executed. It does not prove the result
// matched an oracle on a sequence nobody wrote down, and that is the thing a collection can
// get wrong. What answers it is a differential or property suite — and whether one exists for
// a given type has, until now, been tracked by hand.
//
// That roster drifted, repeatedly. Four successive versions of the table in #418 were wrong,
// each for the same reason: randomized oracle coverage lives under conventions that share no
// suffix and no directory, so *inferring* from a filename that does not exist is unsound.
// `SmallSet` is covered by `SetAlgebraDifferentialTests`, `EnumSet` by
// `EnumSetAlgebraDifferentialTests`, the three sketches by `*AccuracyTests`, twenty-odd
// hash-backed types by the shared `CollectionModelPropertyTests`, and the rest by a file of
// their own. A search for `SmallSetDifferentialTests` finds nothing and means nothing.
//
// So this asks the question mechanically instead, on every pull request, and answers it for
// the tree as it actually is. A collection that ships without oracle coverage fails on the PR
// that adds it, rather than being noticed a year later by someone auditing a table.
//
// What it checks:
//   1. Every public collection type in `src/Celerity/Collections/` is referenced by at least
//      one per-PR oracle suite. `Celerity.Fuzz` is the nightly layer and does not count on
//      its own — a type soaked nightly but ungated per PR is the #418 item 2 gap.
//   2. Every public *struct* declared there is classified in SUPPORTING_TYPES with a reason.
//      Every collection in the library is a class, so an unclassified struct is either a new
//      shape this script has to learn or a supporting type nobody wrote down. Both should
//      stop the build rather than be skipped in silence.
//   3. No SUPPORTING_TYPES entry is stale — a roster that outlives its type starts lying.
//   4. No oracle-suite naming convention has appeared that this script does not know about.
//      A sixth convention is the exact failure this check exists to prevent, and it would
//      otherwise present as a type quietly counted uncovered (or, worse, a file of real
//      coverage that nothing reads).
//
// A reference counts only if it appears in *code*. The type name is matched against source
// with comments and string literals removed, because a collection is routinely named in
// another one's XML docs — `SparseTableDifferentialTests` discusses `SegmentTree` at length
// — and crediting that as coverage is precisely the kind of wrong answer that produced #418.
// The comment lexer is lifted out of `scripts/benchmark_relevant_changes.js` rather than
// re-guessed here, for the reason recorded there: `//` occurs inside literals, and C#'s
// verbatim / interpolated / raw forms desynchronise a prefix test.
//
// Usage:
//   node scripts/check_property_coverage.js              # check every collection type
//   node scripts/check_property_coverage.js --list       # print the whole coverage map
//   node scripts/check_property_coverage.js --self-test  # pin the scanners and the roster
// CI runs the default and --self-test modes. Run from the repository root.

'use strict';

const fs = require('fs');
const path = require('path');

const COLLECTIONS_DIR = path.join('src', 'Celerity', 'Collections');
const TESTS_DIR = path.join('src', 'Celerity.Tests');
const FUZZ_DIFFERENTIAL = path.join('src', 'Celerity.Fuzz', 'Differential.cs');
const LEXER_SOURCE = path.join('scripts', 'benchmark_relevant_changes.js');
const SELF = path.join('scripts', 'check_property_coverage.js');
const ISSUE = 'https://github.com/marius-bughiu/Celerity/issues/418';
const TESTING_DOC = 'docs/testing.md#property-based-tests-cscheck';

function fail(message) {
  console.error(`error: ${message}`);
  process.exit(1);
}

// ---- The per-PR oracle-suite conventions --------------------------------------------
// Four naming conventions, three filename patterns: `<Type>DifferentialTests` and
// `<Operation>DifferentialTests` are indistinguishable by name, which is the point — a
// suite named for the operation family carries types whose own name appears nowhere in it.
// Matching is by suffix anywhere under the test project, so a suite that moves between
// `Collections/` and `Properties/` keeps counting.
const SUITE_SUFFIXES = ['DifferentialTests.cs', 'AccuracyTests.cs', 'PropertyTests.cs'];

// Names that read like oracle coverage but match no convention above. Any such file is a
// fifth convention nobody told this script about; it fails rather than being ignored, and
// the fix is to add its suffix to SUITE_SUFFIXES and say so in docs/testing.md.
const SUITE_SHAPED = /(differential|accuracy|propert|oracle|model)/i;

// ---- Types that are not collections --------------------------------------------------
// Every collection in `Celerity.Collections` is a class. The public structs are strategy,
// coordinate and result types: they carry no state machine of their own, so an oracle suite
// would be asserting a constructor against itself. Each is listed with the reason, and a
// public struct that is *not* listed fails — the roster is here so a struct-shaped
// collection has to be argued for rather than skipped.
const SUPPORTING_TYPES = new Map([
  ['SumMonoid', 'monoid strategy struct — a fold operator, not a container'],
  ['MinMonoid', 'monoid strategy struct — a fold operator, not a container'],
  ['MaxMonoid', 'monoid strategy struct — a fold operator, not a container'],
  ['BitwiseAndMonoid', 'monoid strategy struct — a fold operator, not a container'],
  ['BitwiseOrMonoid', 'monoid strategy struct — a fold operator, not a container'],
  ['DefaultComparer', 'comparer strategy struct forwarding to Comparer<T>.Default'],
  ['Interval', 'coordinate struct — the value IntervalTree stores'],
  ['SpatialPoint', 'coordinate struct — the value the spatial indexes store'],
  ['SpatialBox', 'coordinate struct — the query rectangle the spatial indexes take'],
  ['SpatialGridHandle', 'opaque handle returned by SpatialGrid.Add'],
  ['TimerHandle', 'opaque handle returned by TimerWheel.Schedule'],
  ['ScheduledTimer', 'result struct — one due timer returned by TimerWheel'],
  ['GraphEdge', 'result struct — one edge returned by CompressedGraph'],
  ['PatternMatch', 'result struct — one match returned by AhoCorasick'],
  ['TopKEntry', 'result struct — one ranked entry returned by TopKSketch'],
]);

// ---- The C# comment lexer, lifted from the benchmark relevance gate -------------------
// Re-implementing it here would give two scanners that disagree the first time one is
// fixed. `check_dashboard_coverage.js` lifts the dashboard's own parsers for the same
// reason; the extraction fails loudly if that file is restructured.

function loadStripComments() {
  const source = fs.readFileSync(LEXER_SOURCE, 'utf8');
  const literalNewline = source.match(/const LITERAL_NEWLINE = [^;]+;/);
  const stripComments = source.match(/function stripComments\(source\) \{[\s\S]*?\n\}/);
  if (!literalNewline || !stripComments) {
    fail(
      `could not lift the C# comment lexer out of ${LEXER_SOURCE}. That file was ` +
      `restructured; update the extraction patterns in ${SELF} to match.`
    );
  }
  return new Function(`${literalNewline[0]}\n${stripComments[0]}\nreturn stripComments;`)();
}

const stripComments = loadStripComments();

// `stripComments` preserves literals verbatim — it is built for a diff gate, where a
// changed string is a changed program. Here a literal is the opposite: `Differential.All`
// rosters its cases by name, and a type named in an assertion message is documentation,
// not a call. Both are blanked, so only code counts.
//
// Interpolation holes go with the literal that contains them. That loses the code inside
// `$"{tree.Count}"`, which is a false *negative* — a type reachable only from inside a
// hole would be reported uncovered, loudly, rather than credited in silence. That is the
// direction a gate should err in.
function blankLiterals(code) {
  const out = [];
  const n = code.length;
  let i = 0;

  while (i < n) {
    const c = code[i];

    // Raw string literals: opened by three or more quotes, closed by at least as many.
    if (c === '"' && code[i + 1] === '"' && code[i + 2] === '"') {
      let open = 0;
      while (i + open < n && code[i + open] === '"') open++;
      out.push('"'.repeat(open));
      i += open;
      while (i < n) {
        if (code[i] === '"') {
          let close = 0;
          while (i + close < n && code[i + close] === '"') close++;
          if (close >= open) {
            out.push('"'.repeat(close));
            i += close;
            break;
          }
          out.push(' '.repeat(close));
          i += close;
          continue;
        }
        out.push(code[i] === '\n' ? '\n' : ' ');
        i++;
      }
      continue;
    }

    // Verbatim literals: `@"…"`, where `""` is an escaped quote and `\` is not an escape.
    if (c === '@' && code[i + 1] === '"') {
      out.push('@"');
      i += 2;
      while (i < n) {
        if (code[i] === '"') {
          if (code[i + 1] === '"') {
            out.push('  ');
            i += 2;
            continue;
          }
          out.push('"');
          i++;
          break;
        }
        out.push(code[i] === '\n' ? '\n' : ' ');
        i++;
      }
      continue;
    }

    // Regular and interpolated literals, and character literals. `\` escapes the next
    // character in both.
    if (c === '"' || c === '\'') {
      out.push(c);
      i++;
      while (i < n) {
        if (code[i] === '\\') {
          out.push('  ');
          i += 2;
          continue;
        }
        if (code[i] === c) {
          out.push(c);
          i++;
          break;
        }
        out.push(code[i] === '\n' ? '\n' : ' ');
        i++;
      }
      continue;
    }

    out.push(c);
    i++;
  }

  return out.join('');
}

function toCode(source) {
  return blankLiterals(stripComments(source));
}

// ---- The type scanner ----------------------------------------------------------------
// Only top-level types are collected. Every file in `Celerity.Collections` uses a
// file-scoped namespace, so a namespace member starts at column 0 and a nested type —
// every `Enumerator` struct in the library, among others — is indented. That distinction
// is what keeps the public-struct rule (2) from flagging thirty enumerators, and the
// self-test pins it.
const TYPE_DECLARATION =
  /^(public|internal)((?: (?:sealed|abstract|static|partial|readonly|ref|unsafe))*) (class|struct|interface|enum|record)(?: (class|struct))? (\w+)/gm;

function scanTypes(source) {
  const types = [];
  for (const m of toCode(source).matchAll(TYPE_DECLARATION)) {
    const [, accessibility, modifiers, keyword, recordKind, name] = m;
    const kind = keyword === 'record' ? recordKind || 'class' : keyword;
    types.push({
      name,
      kind,
      accessibility,
      isStatic: / static\b/.test(modifiers),
    });
  }
  return types;
}

// A collection is a public, non-static class. Interfaces (`IMonoid`), enums, internal
// helpers (`SetOperations`, `EnumSetInfo`) and extension holders (`SpanLookupExtensions`)
// are not instances of anything, so there is nothing for an oracle to disagree with.
function isCollection(type) {
  return type.accessibility === 'public' && type.kind === 'class' && !type.isStatic;
}

// ---- File walking ---------------------------------------------------------------------

const SKIP_DIRS = new Set(['bin', 'obj', 'artifacts', 'TestResults']);

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!SKIP_DIRS.has(entry.name)) walk(path.join(dir, entry.name), out);
    } else if (entry.isFile() && entry.name.endsWith('.cs')) {
      out.push(path.join(dir, entry.name));
    }
  }
  return out;
}

// ---- Coverage resolution ---------------------------------------------------------------

function collectionTypes() {
  const byName = new Map();
  for (const file of walk(COLLECTIONS_DIR)) {
    const types = scanTypes(fs.readFileSync(file, 'utf8'));
    for (const type of types) {
      // Generic arity overloads — `IntDictionary<TValue>` and `IntDictionary<TValue,
      // THasher>` — are one type as far as coverage goes, and one suite covers both.
      if (!byName.has(type.name)) byName.set(type.name, { ...type, file });
    }
  }
  return byName;
}

function oracleSuites() {
  const suites = [];
  const misnamed = [];
  for (const file of walk(TESTS_DIR)) {
    const base = path.basename(file);
    if (SUITE_SUFFIXES.some((suffix) => base.endsWith(suffix))) {
      suites.push({ file, code: toCode(fs.readFileSync(file, 'utf8')) });
    } else if (SUITE_SHAPED.test(base)) {
      misnamed.push(file);
    }
  }
  return { suites, misnamed };
}

// The nightly roster, read as the names `Differential.All` actually registers rather than
// as the methods behind them — the driver prints these and `--filter` selects on them.
function fuzzRoster() {
  const source = fs.readFileSync(FUZZ_DIFFERENTIAL, 'utf8');
  const block = source.match(/All\s*=\s*\[([\s\S]*?)\n {4}\];/);
  if (!block) {
    fail(`could not find the Differential.All roster in ${FUZZ_DIFFERENTIAL}; update the pattern in ${SELF}.`);
  }
  return new Set([...block[1].matchAll(/\("(\w+)",/g)].map((m) => m[1]));
}

function resolve() {
  const types = collectionTypes();
  const { suites, misnamed } = oracleSuites();
  const fuzz = fuzzRoster();

  const rows = [];
  for (const [name, type] of types) {
    if (!isCollection(type)) continue;
    const word = new RegExp(`\\b${name}\\b`);
    rows.push({
      name,
      file: type.file,
      suites: suites.filter((s) => word.test(s.code)).map((s) => path.basename(s.file)),
      fuzzed: fuzz.has(name),
    });
  }
  rows.sort((a, b) => a.name.localeCompare(b.name));

  const unclassified = [...types.values()].filter(
    (t) => t.accessibility === 'public' && t.kind === 'struct' && !SUPPORTING_TYPES.has(t.name)
  );
  const stale = [...SUPPORTING_TYPES.keys()].filter((n) => !types.has(n));

  return { rows, misnamed, unclassified, stale, suiteCount: suites.length };
}

// ---- self-test --------------------------------------------------------------------------

function selfTest() {
  let failures = 0;

  function check(label, actual, expected) {
    const a = JSON.stringify(actual);
    const e = JSON.stringify(expected);
    if (a !== e) {
      failures++;
      console.error(`FAIL  ${label}`);
      console.error(`      expected ${e}`);
      console.error(`      actual   ${a}`);
    }
  }

  // The type scanner: what counts as a namespace member, and what each declaration means.
  const declarations = [
    'public sealed class Rope : IEnumerable<char>',
    'public readonly struct Interval<T> where T : IComparable<T>',
    'internal static class SetOperations',
    'public interface IMonoid<T>',
    'public static class SpanLookupExtensions',
    'public partial record struct Pair',
    'public enum Mode',
    '    public struct Enumerator',       // nested — indented, so not a namespace member
    '    public sealed class Node',       // nested
  ].join('\n');
  check(
    'type scanner',
    scanTypes(declarations).map((t) => `${t.accessibility} ${t.isStatic ? 'static ' : ''}${t.kind} ${t.name}`),
    [
      'public class Rope',
      'public struct Interval',
      'internal static class SetOperations',
      'public interface IMonoid',
      'public static class SpanLookupExtensions',
      'public struct Pair',
      'public enum Mode',
    ]
  );

  check(
    'collection predicate',
    scanTypes(declarations).filter(isCollection).map((t) => t.name),
    ['Rope']
  );

  // The matcher. A collection named in another type's XML docs, in an assertion message,
  // or in a `Differential.All` roster entry is not a call, and crediting any of them is
  // how a hand-maintained table gets to be wrong while looking right.
  const mentions = [
    '/// <summary>Unlike <see cref="SegmentTree{T}"/>, this is O(1).</summary>',
    'var sut = new SparseTable<int, MinMonoid>(values); // beats SuffixArray',
    'Assert.True(ok, "Trie diverged");',
    'Assert.True(ok, $"Deque diverged at {i}");',
    'var verbatim = @"C:\\LruCache\\path"";still KdTree";',
    'var raw = """ RankedSet """;',
    "var ch = 'x'; var esc = '\\''; var after = new BTreeSet<int>();",
  ].join('\n');
  const code = toCode(mentions);
  for (const absent of ['SegmentTree', 'SuffixArray', 'Trie', 'Deque', 'LruCache', 'KdTree', 'RankedSet']) {
    if (new RegExp(`\\b${absent}\\b`).test(code)) {
      failures++;
      console.error(`FAIL  ${absent} was credited from a comment or a string literal`);
    }
  }
  for (const present of ['SparseTable', 'MinMonoid', 'BTreeSet']) {
    if (!new RegExp(`\\b${present}\\b`).test(code)) {
      failures++;
      console.error(`FAIL  ${present} is written in code and was not found`);
    }
  }

  // Blanking must not shift line numbers, which is what a contributor navigates by.
  check('line count preserved', toCode(mentions).split('\n').length, mentions.split('\n').length);

  // The tree itself: the roster is only useful while it still describes the tree.
  const { rows, misnamed, unclassified, stale } = resolve();
  if (rows.length === 0) {
    failures++;
    console.error('FAIL  no collection types were found — the scanner or the path is wrong');
  }
  check('no unclassified public structs', unclassified.map((t) => t.name), []);
  check('no stale SUPPORTING_TYPES entries', stale, []);
  check('no unknown suite naming convention', misnamed, []);

  if (failures > 0) {
    console.error(`\n${failures} case(s) failed.`);
    process.exit(1);
  }
  console.log(`ok: type scanner, code matcher and roster pinned over ${rows.length} collection type(s).`);
}

// ---- entry point --------------------------------------------------------------------------

function main() {
  const args = process.argv.slice(2);

  if (args.includes('--self-test')) {
    selfTest();
    return;
  }

  const { rows, misnamed, unclassified, stale, suiteCount } = resolve();

  if (args.includes('--list')) {
    const width = Math.max(...rows.map((r) => r.name.length));
    for (const row of rows) {
      const where = row.suites.length > 0 ? row.suites.join(', ') : '(none)';
      console.log(`${row.name.padEnd(width)}  ${row.fuzzed ? 'fuzz+' : '     '} ${where}`);
    }
    console.log(`\n${rows.length} collection type(s), ${suiteCount} oracle suite(s).`);
    return;
  }

  const problems = [];

  const uncovered = rows.filter((r) => r.suites.length === 0);
  if (uncovered.length > 0) {
    problems.push(
      'These collection types are referenced by no per-PR oracle suite. The 100% coverage\n' +
      'gate proves their branches ran, not that their answers were right:\n' +
      uncovered.map((r) => `  ${r.file}  ${r.name}${r.fuzzed ? '  (nightly fuzz only)' : ''}`).join('\n') +
      `\n\nAdd a suite under one of the conventions in ${TESTING_DOC}, or, if the type is not\n` +
      `a collection, classify it in ${SELF}. Context: ${ISSUE}`
    );
  }

  if (unclassified.length > 0) {
    problems.push(
      'These public structs are declared in Celerity.Collections and classified nowhere.\n' +
      'Every collection in the library is a class, so a struct is either a supporting type\n' +
      '— add it to SUPPORTING_TYPES with the reason — or a new shape that needs an oracle\n' +
      `suite and a rule in ${SELF}:\n` +
      unclassified.map((t) => `  ${t.file}  ${t.name}`).join('\n')
    );
  }

  if (stale.length > 0) {
    problems.push(
      `These SUPPORTING_TYPES entries name types that no longer exist. Delete them from ${SELF}\n` +
      'so the roster keeps describing the tree:\n' +
      stale.map((n) => `  ${n}`).join('\n')
    );
  }

  if (misnamed.length > 0) {
    problems.push(
      'These test files are named like oracle suites but match no convention this check\n' +
      'knows, so nothing they cover is counted. Add the suffix to SUITE_SUFFIXES in\n' +
      `${SELF} and document the convention in ${TESTING_DOC}, or rename the file:\n` +
      misnamed.map((f) => `  ${f}`).join('\n')
    );
  }

  if (problems.length > 0) {
    console.error(`${problems.join('\n\n')}\n`);
    process.exit(1);
  }

  const fuzzed = rows.filter((r) => r.fuzzed).length;
  console.log(
    `ok: ${rows.length} collection type(s) covered by ${suiteCount} oracle suite(s) ` +
    `(${fuzzed} also on the nightly fuzz roster).`
  );
}

main();
