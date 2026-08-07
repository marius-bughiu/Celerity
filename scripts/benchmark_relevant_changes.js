#!/usr/bin/env node
//
// Decides whether a pull request's diff can move a benchmark number.
//
// `benchmarks.yml` fires on `src/**`, which is a path filter, not a semantic one: an XML
// doc-comment edit to a `.cs` file is a `src/**` change and buys the PR a full sharded
// A/B run — the PR head and the `main` base, twice over eight runners, for a diff with
// zero IL in it. Two such pull requests overlapped and every shard ran 1.75-2.0x its
// baseline, one of them straight into the 120-minute cap. The suite is the
// expensive thing in this repository's CI, so the cheapest correct win is not to run it
// when the diff provably cannot change what it measures.
//
// The rule is deliberately one-directional. Skipping is only ever claimed when *every*
// changed path is one of:
//
//   1. outside `src/` (documentation, the dashboard, the changelog, ...);
//   2. inside a project the benchmark process never loads — every `*.Tests` project plus
//      `Celerity.Fuzz` and `Celerity.AotSmokeTest`, none of which is reachable from
//      `Celerity.Benchmarks.csproj`, so nothing they contain reaches a measurement;
//   3. a modified `.cs` file whose text is unchanged once comments are stripped.
//
// Everything else — an added or deleted file, a rename, a non-`.cs` file under `src/`
// (a `.csproj`, `Directory.Build.props`), the benchmark workflow itself, this script,
// a git command that fails, an argument that is missing — is treated as significant and
// the suite runs. Every unknown resolves toward running, because the cost of a wrong
// "run" is wasted runner minutes and the cost of a wrong "skip" is an unmeasured
// regression.
//
// The gate is applied to the pull-request path only. The `main` push path always runs,
// so the gh-pages time series never gains a hole and a wrongly-skipped PR is still
// measured on merge — which caps the worst case at "the PR comment was missing", never
// "the regression was never seen".
//
// Usage:
//   node scripts/benchmark_relevant_changes.js <base-ref> <head-ref>
//   node scripts/benchmark_relevant_changes.js --self-test   # pin the C# lexer
//
// Writes `run=true|false` to $GITHUB_OUTPUT when set, and always prints the per-path
// reasoning. Exits 0 unless --self-test fails. Run from the repository root.

'use strict';

const fs = require('fs');
const { execFileSync } = require('child_process');

// Projects whose assemblies the benchmark process loads, following
// `Celerity.Benchmarks.csproj`'s <ProjectReference> list transitively. A change anywhere
// in one of these can move a number.
const BENCHMARKED_PROJECTS = new Set([
  'Celerity',
  'Celerity.Hashing',
  'Celerity.Primitives',
  'Celerity.Sorting',
  'Celerity.Ring',
  'Celerity.Sentinel',
  'Celerity.Cardinality',
  'Celerity.Benchmarks',
]);

// Everything else under `src/` is only inert if we can say *why*. Matching the repo's
// `*.Tests` convention plus the two named harnesses covers it, and a project that fits
// neither is treated as significant rather than assumed harmless — which is also what
// `checkProjectRoster` in the self-test refuses to let pass silently.
function isUnbenchmarkedProject(project) {
  if (BENCHMARKED_PROJECTS.has(project)) return false;
  return project.endsWith('.Tests')
    || project === 'Celerity.Fuzz'
    || project === 'Celerity.AotSmokeTest';
}

// Files outside `src/` that still change what the run does or checks.
const ALWAYS_SIGNIFICANT = new Set([
  '.github/workflows/benchmarks.yml',
  'scripts/benchmark_relevant_changes.js',
  'scripts/check_dashboard_coverage.js',
]);

// ---- C# comment stripping -----------------------------------------------------------
// A line-level "does this line start with //" test is not enough: `//` occurs inside
// string literals, and C#'s literal forms are varied enough (verbatim, interpolated,
// raw, and interpolated-raw with a hole that contains further literals) that guessing
// desynchronises the scan. So this is a real scanner with a mode stack — code frames and
// string frames, where an interpolation hole pushes a code frame back on top.
//
// Line structure is preserved: a stripped comment leaves its newlines behind, so a line
// that moves still reads as a change. Verbatim text inside literals is preserved exactly,
// including whitespace, so `"a  b"` and `"a b"` are not conflated.
//
// A newline *inside* a literal is emitted as LITERAL_NEWLINE instead. `normalize` trims
// per line and drops blank ones, which is right for code and wrong for a multi-line
// verbatim or raw literal, where the indentation and the blank lines are part of the
// string value and reach the IL. Collapsing those newlines keeps each literal on one
// logical line, so trimming can only ever touch the code around it.
const LITERAL_NEWLINE = '\u0000';

function stripComments(source) {
  const out = [];
  const stack = [{ type: 'code', braces: 0, isHole: false }];
  const n = source.length;
  let i = 0;

  while (i < n) {
    const frame = stack[stack.length - 1];
    const c = source[i];

    if (frame.type === 'string') {
      if (frame.raw) {
        if (c === '"') {
          let k = 0;
          while (i + k < n && source[i + k] === '"') k++;
          out.push(source.slice(i, i + k));
          i += k;
          // A raw string closes on at least as many quotes as opened it.
          if (k >= frame.quotes) stack.pop();
          continue;
        }
        if (frame.dollars > 0 && c === '{') {
          let k = 0;
          while (i + k < n && source[i + k] === '{') k++;
          if (k >= frame.dollars) {
            out.push(source.slice(i, i + frame.dollars));
            i += frame.dollars;
            stack.push({ type: 'code', braces: 0, isHole: true });
            continue;
          }
          out.push(source.slice(i, i + k));
          i += k;
          continue;
        }
        out.push(c === '\n' ? LITERAL_NEWLINE : c);
        i++;
        continue;
      }

      if (frame.verbatim) {
        if (c === '"') {
          // `""` is an escaped quote inside a verbatim literal.
          if (source[i + 1] === '"') { out.push('""'); i += 2; continue; }
          out.push(c); i++; stack.pop(); continue;
        }
      } else {
        if (c === '\\') { out.push(source.slice(i, i + 2)); i += 2; continue; }
        if (c === '"') { out.push(c); i++; stack.pop(); continue; }
        // A non-verbatim literal cannot span a line. Rather than run away to the end of
        // the file on malformed input, resync at the newline.
        if (c === '\n') { out.push(c); i++; stack.pop(); continue; }
      }

      if (frame.dollars > 0) {
        if (c === '{') {
          if (source[i + 1] === '{') { out.push('{{'); i += 2; continue; }
          out.push(c); i++;
          stack.push({ type: 'code', braces: 0, isHole: true });
          continue;
        }
        if (c === '}' && source[i + 1] === '}') { out.push('}}'); i += 2; continue; }
      }

      out.push(c === '\n' ? LITERAL_NEWLINE : c);
      i++;
      continue;
    }

    // ---- code frame ----
    if (c === '/' && source[i + 1] === '/') {
      while (i < n && source[i] !== '\n') i++;
      continue; // the newline itself is copied on the next pass
    }

    if (c === '/' && source[i + 1] === '*') {
      i += 2;
      while (i < n && !(source[i] === '*' && source[i + 1] === '/')) {
        if (source[i] === '\n') out.push('\n');
        i++;
      }
      i = Math.min(i + 2, n);
      out.push(' ');
      continue;
    }

    if (c === "'") {
      out.push(c);
      i++;
      while (i < n && source[i] !== "'" && source[i] !== '\n') {
        if (source[i] === '\\') { out.push(source.slice(i, i + 2)); i += 2; continue; }
        out.push(source[i]);
        i++;
      }
      if (i < n && source[i] === "'") { out.push("'"); i++; }
      continue;
    }

    if (c === '"' || c === '$' || c === '@') {
      // Collect the `$`/`@` prefix, which may appear in either order.
      let j = i;
      let dollars = 0;
      let ats = 0;
      while (j < n && (source[j] === '$' || source[j] === '@')) {
        if (source[j] === '$') dollars++; else ats++;
        j++;
      }
      if (j < n && source[j] === '"') {
        let quotes = 0;
        while (j + quotes < n && source[j + quotes] === '"') quotes++;
        // Three or more quotes open a raw literal; `""` is just an empty regular one.
        const raw = quotes >= 3;
        if (!raw) quotes = 1;
        out.push(source.slice(i, j + quotes));
        i = j + quotes;
        stack.push({ type: 'string', quotes, raw, verbatim: ats > 0, dollars });
        continue;
      }
      if (j > i) {
        // A bare `@` prefixing an identifier (`@class`), or a stray `$`.
        out.push(source.slice(i, j));
        i = j;
        continue;
      }
    }

    if (frame.isHole) {
      if (c === '{') { frame.braces++; out.push(c); i++; continue; }
      if (c === '}') {
        if (frame.braces === 0) { stack.pop(); out.push(c); i++; continue; }
        frame.braces--;
        out.push(c);
        i++;
        continue;
      }
    }

    out.push(c);
    i++;
  }

  return out.join('');
}

// Comment-free text, trimmed per line with blank lines dropped, so reindentation and
// comment removal do not register but a moved or edited statement does.
function normalize(source) {
  return stripComments(source)
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .join('\n');
}

// ---- git plumbing -------------------------------------------------------------------

// core.quotePath=false keeps a path with non-ASCII characters readable rather than
// octal-escaped, so `git show <rev>:<path>` can be handed straight back what
// `--name-status` printed. Without it such a file would fail to read and be treated as
// significant — safe, but it would run the suite for a diff that could not move a number.
function git(args) {
  return execFileSync('git', ['-c', 'core.quotePath=false', ...args], {
    encoding: 'utf8',
    maxBuffer: 256 * 1024 * 1024,
  });
}

function classify(filePath) {
  if (ALWAYS_SIGNIFICANT.has(filePath)) return 'significant';
  if (!filePath.startsWith('src/')) return 'ignored';
  const project = filePath.slice('src/'.length).split('/')[0];
  if (isUnbenchmarkedProject(project)) return 'ignored';
  if (!filePath.endsWith('.cs')) return 'significant';
  return 'compare';
}

function decide(baseRef, headRef) {
  const reasons = [];

  let mergeBase;
  try {
    mergeBase = git(['merge-base', baseRef, headRef]).trim();
  } catch (err) {
    return { run: true, reasons: [`could not resolve a merge base for ${baseRef}..${headRef}: ${err.message}`] };
  }

  let entries;
  try {
    entries = git(['diff', '--name-status', '-M', mergeBase, headRef])
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);
  } catch (err) {
    return { run: true, reasons: [`could not diff ${mergeBase}..${headRef}: ${err.message}`] };
  }

  if (entries.length === 0) {
    return { run: false, reasons: ['the diff is empty'] };
  }

  let significant = false;
  for (const entry of entries) {
    const fields = entry.split('\t');
    const status = fields[0];
    // A rename/copy reports both the old and the new path; judge the new one.
    const filePath = fields[fields.length - 1];
    const verdict = classify(filePath);

    if (verdict === 'ignored') {
      reasons.push(`  skip  ${filePath} (cannot reach a measurement)`);
      continue;
    }
    if (verdict === 'significant') {
      reasons.push(`  RUN   ${filePath} (${status})`);
      significant = true;
      continue;
    }

    // Only a plain modification can be compared; an add, delete, rename or type change
    // has no pair of texts to compare and is taken at face value.
    if (status !== 'M') {
      reasons.push(`  RUN   ${filePath} (${status}, not a plain modification)`);
      significant = true;
      continue;
    }

    let before;
    let after;
    try {
      before = git(['show', `${mergeBase}:${filePath}`]);
      after = git(['show', `${headRef}:${filePath}`]);
    } catch (err) {
      reasons.push(`  RUN   ${filePath} (could not read both revisions: ${err.message})`);
      significant = true;
      continue;
    }

    if (normalize(before) === normalize(after)) {
      reasons.push(`  skip  ${filePath} (comments only)`);
    } else {
      reasons.push(`  RUN   ${filePath} (code changed)`);
      significant = true;
    }
  }

  return { run: significant, reasons };
}

// ---- self-test ----------------------------------------------------------------------
// Pins the lexer against the literal forms that make a naive `//`-prefix test wrong. Each
// case is [source, expected-normalized-text].
const SELF_TEST_CASES = [
  ['int x = 1; // trailing', 'int x = 1;'],
  ['/// <summary>doc</summary>\nint x = 1;', 'int x = 1;'],
  ['/* block */ int x = 1;', 'int x = 1;'],
  ['/* multi\n   line */ int x = 1;', 'int x = 1;'],
  // A `//` inside a literal is data, not a comment.
  ['var url = "https://example.com"; // real comment', 'var url = "https://example.com";'],
  ['var s = "/* not a comment */";', 'var s = "/* not a comment */";'],
  // Escapes must not end the literal early.
  ['var s = "he said \\"//\\" here"; // c', 'var s = "he said \\"//\\" here";'],
  // Verbatim literals: `""` is an escaped quote, and backslashes are literal.
  ['var s = @"C:\\path"; // c', 'var s = @"C:\\path";'],
  ['var s = @"a ""//"" b"; // c', 'var s = @"a ""//"" b";'],
  ['var s = @"line1\nline2 // still text";', `var s = @"line1${LITERAL_NEWLINE}line2 // still text";`],
  // Interpolation holes are code and are scanned as such, including nested literals.
  ['var s = $"{dict["k"]}"; // c', 'var s = $"{dict["k"]}";'],
  // A stripped block comment leaves one space behind, so the neighbouring spaces
  // survive; that is deterministic on both sides of a diff, which is all this needs.
  ['var s = $"{a /* h */ + b}";', 'var s = $"{a   + b}";'],
  ['var s = $"{{literal}}"; // c', 'var s = $"{{literal}}";'],
  ['var s = $@"{x} // text";', 'var s = $@"{x} // text";'],
  ['var s = @$"{x} // text";', 'var s = @$"{x} // text";'],
  // Raw literals close only on a matching quote run; `$$` needs `{{` to open a hole.
  ['var s = """a "b" // c""";', 'var s = """a "b" // c""";'],
  ['var s = $$"""{{x}} // text""";', 'var s = $$"""{{x}} // text""";'],
  // Char literals, including the quote and backslash cases.
  ["var c = '\\''; // c", "var c = '\\'';"],
  ["var c = '\"'; // c", "var c = '\"';"],
  // A verbatim identifier is not a literal.
  ['var @class = 1; // c', 'var @class = 1;'],
  // Reindentation and blank lines are not changes; a moved statement is.
  ['        int x = 1;\n\n\n        int y = 2;', 'int x = 1;\nint y = 2;'],
  // A multi-line literal keeps its own indentation and blank lines, because they are part
  // of the string value. It collapses onto one logical line so the per-line trim below
  // cannot reach inside it.
  ['var s = @"a\n   b";', `var s = @"a${LITERAL_NEWLINE}   b";`],
  ['var s = """\n  x\n\n  y\n  """;', `var s = """${LITERAL_NEWLINE}  x${LITERAL_NEWLINE}${LITERAL_NEWLINE}  y${LITERAL_NEWLINE}  """;`],
];

// The set of projects under `src/` must be classified deliberately, one way or the other.
// A new project that is neither benchmark-reachable nor a recognised harness would
// otherwise be silently taken as significant forever (safe, but wasteful) or — the shape
// this guard really exists for — a new test project would go on buying full benchmark
// runs because nobody remembered this file. Fail here instead, in seconds, on every PR.
function checkProjectRoster() {
  const dirs = fs.readdirSync('src', { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .filter((name) => fs.readdirSync(`src/${name}`).some((f) => f.endsWith('.csproj')));

  const unclassified = dirs.filter((d) => !BENCHMARKED_PROJECTS.has(d) && !isUnbenchmarkedProject(d));
  if (unclassified.length > 0) {
    console.error(`FAIL  unclassified project(s) under src/: ${unclassified.join(', ')}`);
    console.error('      Add each to BENCHMARKED_PROJECTS if the benchmark process loads it,');
    console.error('      or teach isUnbenchmarkedProject why it cannot reach a measurement.');
    return 1;
  }

  // The converse: a name listed as benchmark-reachable that no longer exists means the
  // list has drifted the other way and is quietly over-claiming.
  const stale = [...BENCHMARKED_PROJECTS].filter((p) => !dirs.includes(p));
  if (stale.length > 0) {
    console.error(`FAIL  BENCHMARKED_PROJECTS names project(s) that do not exist: ${stale.join(', ')}`);
    return 1;
  }

  console.log(`ok: ${dirs.length} project(s) under src/ classified.`);
  return 0;
}

function selfTest() {
  let failures = 0;
  for (const [source, expected] of SELF_TEST_CASES) {
    const actual = normalize(source);
    if (actual !== expected) {
      failures++;
      console.error(`FAIL  ${JSON.stringify(source)}`);
      console.error(`      expected ${JSON.stringify(expected)}`);
      console.error(`      actual   ${JSON.stringify(actual)}`);
    }
  }

  // The property the gate actually rests on: editing only a comment must normalize
  // identically, and editing code next to it must not.
  const before = 'class C {\n  // old note\n  void M() { Run("a"); }\n}';
  const commentOnly = 'class C {\n  /// new note, reworded\n  void M() { Run("a"); }\n}';
  const codeChanged = 'class C {\n  // old note\n  void M() { Run("b"); }\n}';
  if (normalize(before) !== normalize(commentOnly)) {
    failures++;
    console.error('FAIL  a comment-only edit did not normalize identically');
  }
  if (normalize(before) === normalize(codeChanged)) {
    failures++;
    console.error('FAIL  a code edit normalized identically to its original');
  }

  // Reindenting a multi-line literal changes the compiled string, so it must not read as
  // "comments only" — the failure mode a per-line trim introduces if it reaches inside a
  // literal.
  const literal = 'var s = @"a\n   b"; // note';
  const reindented = 'var s = @"a\n       b"; // note';
  const literalCommentOnly = 'var s = @"a\n   b"; // reworded';
  if (normalize(literal) === normalize(reindented)) {
    failures++;
    console.error('FAIL  reindenting a multi-line literal normalized identically');
  }
  if (normalize(literal) !== normalize(literalCommentOnly)) {
    failures++;
    console.error('FAIL  a comment edit beside a multi-line literal did not normalize identically');
  }

  failures += checkProjectRoster();

  if (failures > 0) {
    console.error(`\n${failures} lexer case(s) failed.`);
    process.exit(1);
  }
  console.log(`ok: ${SELF_TEST_CASES.length + 2} C# lexer case(s) pinned.`);
}

// ---- entry point --------------------------------------------------------------------

function main() {
  const args = process.argv.slice(2);

  if (args.includes('--self-test')) {
    selfTest();
    return;
  }

  const [baseRef, headRef] = args;
  let result;
  if (!baseRef || !headRef) {
    result = { run: true, reasons: ['usage: benchmark_relevant_changes.js <base-ref> <head-ref>'] };
  } else {
    result = decide(baseRef, headRef);
  }

  console.log(result.run
    ? 'Benchmarks will run — this diff can move a measured number.'
    : 'Benchmarks will be skipped — nothing in this diff can reach a measurement.');
  for (const reason of result.reasons) {
    console.log(reason);
  }

  if (process.env.GITHUB_OUTPUT) {
    fs.appendFileSync(process.env.GITHUB_OUTPUT, `run=${result.run}\n`);
  }
  console.log(`run=${result.run}`);
}

main();
