#!/usr/bin/env node
//
// Fails when a `const` in a shipping package is not named in PascalCase.
//
// The convention used to be stated as UPPER_CASE in CONTRIBUTING.md while the code was
// pervasively split between UPPER_CASE and PascalCase — in one file both styles collided
// inside a single type, and two constants meaning the same thing in two packages were
// named `DEFAULT_PRECISION` and `DefaultPrecision`. Nothing checked the rule, so every
// new collection re-litigated it and review caught it only sometimes. The convention is
// now PascalCase for every constant, matching dotnet/runtime's own style, and this is
// what holds it.
//
// Scope: the eight shipping packages under `src/`. Test, benchmark, fuzz and AOT-smoke
// code is deliberately exempt — those files are full of throwaway method-local constants
// (`const int n = 5;`) whose casing carries no meaning for a consumer of the library.
//
// What counts as PascalCase here:
//   - starts with an upper-case letter;
//   - contains a lower-case letter, so SCREAMING names such as `NIL` are rejected —
//     unless the whole name is at most two characters, which lets the transcribed
//     algorithm constants `C1`, `K0`, `M`, `R` keep the names their specifications use;
//   - carries no underscore, except a trailing `_<digits>` index, which keeps
//     xxHash's `Prime64_1` family recognisable against the reference implementation.
//
// Usage:
//   node scripts/check_constant_naming.js              # check every shipping package
//   node scripts/check_constant_naming.js --list       # print every constant it found
//   node scripts/check_constant_naming.js --self-test  # pin the name rule and the scan
// CI runs the default and --self-test modes. Run from the repository root.

'use strict';

const fs = require('fs');
const path = require('path');

// The packages that ship to NuGet. A constant in one of these is either part of the
// public surface or sits in code a consumer steps into, which is where a naming rule
// earns its keep.
const SHIPPING_PROJECTS = new Set([
  'Celerity',
  'Celerity.Hashing',
  'Celerity.Primitives',
  'Celerity.Sorting',
  'Celerity.Statistics',
  'Celerity.Ring',
  'Celerity.Sentinel',
  'Celerity.Cardinality',
]);

// Everything else under `src/` is exempt only if we can say why, so a project that fits
// neither list fails the self-test rather than being silently skipped.
function isExemptProject(project) {
  if (SHIPPING_PROJECTS.has(project)) return false;
  return project.endsWith('.Tests')
    || project === 'Celerity.Benchmarks'
    || project === 'Celerity.Fuzz'
    || project === 'Celerity.AotSmokeTest';
}

const SKIP_DIRS = new Set(['bin', 'obj', 'artifacts', 'TestResults']);

// ---- The name rule ------------------------------------------------------------------

const PASCAL_CASE = /^[A-Z][A-Za-z0-9]*(_[0-9]+)?$/;

function isCompliant(name) {
  if (!PASCAL_CASE.test(name)) return false;
  if (name.length <= 2) return true;
  return /[a-z]/.test(name);
}

// ---- C# comment and literal stripping -----------------------------------------------
// `const` occurs inside comments and strings often enough that a raw regex over the file
// reports constants that do not exist. This blanks comments and literal bodies while
// preserving every newline, so reported line numbers stay exact.

function strip(source) {
  const out = [];
  const n = source.length;
  let i = 0;

  const keepLines = (text) => text.replace(/[^\n]/g, ' ');

  while (i < n) {
    const c = source[i];
    const next = source[i + 1];

    if (c === '/' && next === '/') {
      const end = source.indexOf('\n', i);
      const stop = end === -1 ? n : end;
      out.push(keepLines(source.slice(i, stop)));
      i = stop;
      continue;
    }

    if (c === '/' && next === '*') {
      const end = source.indexOf('*/', i + 2);
      const stop = end === -1 ? n : end + 2;
      out.push(keepLines(source.slice(i, stop)));
      i = stop;
      continue;
    }

    // Raw string literal: closes on at least as many quotes as opened it. Interpolation
    // holes inside one are blanked along with the rest; a `const` declaration cannot
    // appear in a hole, so nothing is lost by not descending into it.
    if (c === '"' && next === '"' && source[i + 2] === '"') {
      let open = 0;
      while (source[i + open] === '"') open++;
      let j = i + open;
      for (; j < n; j++) {
        if (source[j] !== '"') continue;
        let close = 0;
        while (source[j + close] === '"') close++;
        if (close >= open) { j += close; break; }
        j += close - 1;
      }
      const stop = Math.min(j, n);
      out.push(keepLines(source.slice(i, stop)));
      i = stop;
      continue;
    }

    if (c === '@' && next === '"') {
      let j = i + 2;
      while (j < n) {
        if (source[j] === '"') {
          if (source[j + 1] === '"') { j += 2; continue; }
          j++;
          break;
        }
        j++;
      }
      out.push(keepLines(source.slice(i, j)));
      i = j;
      continue;
    }

    if (c === '"' || c === '\'') {
      let j = i + 1;
      while (j < n) {
        if (source[j] === '\\') { j += 2; continue; }
        if (source[j] === c) { j++; break; }
        // A non-verbatim literal cannot span a line; resync rather than run away.
        if (source[j] === '\n') break;
        j++;
      }
      out.push(keepLines(source.slice(i, j)));
      i = j;
      continue;
    }

    out.push(c);
    i++;
  }

  return out.join('');
}

// ---- The scan -----------------------------------------------------------------------
// A declaration is `const <type> <name> = ...`, where the type may be generic, an array,
// nullable or qualified, and where further `, <name> = ...` declarators may follow on
// the same statement.

const CONST_DECL = /\bconst\s+[A-Za-z_][A-Za-z_0-9.<>,\[\]\?\s]*?\s+([A-Za-z_][A-Za-z_0-9]*)\s*=/g;

function findConstants(source) {
  const stripped = strip(source);
  const found = [];

  CONST_DECL.lastIndex = 0;
  let match;
  while ((match = CONST_DECL.exec(stripped)) !== null) {
    const line = stripped.slice(0, match.index).split('\n').length;
    found.push({ name: match[1], line });

    // Trailing declarators: `const int A = 1, B = 2;` declares B as well.
    const semicolon = stripped.indexOf(';', match.index);
    const tail = stripped.slice(CONST_DECL.lastIndex, semicolon === -1 ? undefined : semicolon);
    const extra = /,\s*([A-Za-z_][A-Za-z_0-9]*)\s*=/g;
    let more;
    while ((more = extra.exec(tail)) !== null) {
      found.push({ name: more[1], line });
    }
  }

  return found;
}

function csharpFiles(dir) {
  const files = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (SKIP_DIRS.has(entry.name)) continue;
      files.push(...csharpFiles(path.join(dir, entry.name)));
    } else if (entry.name.endsWith('.cs')) {
      files.push(path.join(dir, entry.name));
    }
  }
  return files;
}

function scan() {
  const violations = [];
  const all = [];

  for (const project of [...SHIPPING_PROJECTS].sort()) {
    const dir = path.join('src', project);
    for (const file of csharpFiles(dir)) {
      const source = fs.readFileSync(file, 'utf8');
      for (const constant of findConstants(source)) {
        const where = { ...constant, file: file.split(path.sep).join('/') };
        all.push(where);
        if (!isCompliant(constant.name)) violations.push(where);
      }
    }
  }

  return { violations, all };
}

// ---- Self-test ----------------------------------------------------------------------

const NAME_CASES = [
  ['DefaultCapacity', true],
  ['MaxKicks', true],
  ['Ln2Squared', true],
  ['Bits2D', true],
  ['Prime64_1', true],   // transcribed from the xxHash reference
  ['Prime32_3', true],
  ['C1', true],          // two-character algorithm symbols keep their spec names
  ['K0', true],
  ['M', true],
  ['DEFAULT_CAPACITY', false],
  ['MAX_GRID', false],
  ['TWO_POW_32', false],
  ['EMPTY', false],
  ['NIL', false],        // three characters is past the acronym allowance
  ['fnvPrime', false],
  ['offsetBasis', false],
  ['_seed', false],
  ['Max_Kicks', false],  // an underscore may only introduce a digit index
];

const SCAN_CASES = [
  ['class C { private const int MaxKicks = 500; }', ['MaxKicks']],
  ['class C { public const double Ln2 = 0.69; }', ['Ln2']],
  ['void M() { const ulong FnvPrime = 1099511628211UL; }', ['FnvPrime']],
  ['class C { const int A = 1, B = 2; }', ['A', 'B']],
  ['class C { private const Vector128<sbyte> Mask = default; }', ['Mask']],
  ['class C { private const int?  Wide = null; }', ['Wide']],
  // Comments and literals are not declarations, however much they look like one.
  ['class C { /* const int Ghost = 1; */ }', []],
  ['class C { // const int Ghost = 1;\n }', []],
  ['class C { string s = "const int Ghost = 1;"; }', []],
  ['class C { string s = @"const int Ghost = 1;"; }', []],
  ['class C { string s = """const int Ghost = 1;"""; }', []],
  // `const` as part of a longer word is not the keyword.
  ['class C { int nonconstant = 1; }', []],
];

function checkProjectRoster() {
  const dirs = fs.readdirSync('src', { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .filter((name) => fs.readdirSync(path.join('src', name)).some((f) => f.endsWith('.csproj')));

  const unclassified = dirs.filter((d) => !SHIPPING_PROJECTS.has(d) && !isExemptProject(d));
  if (unclassified.length > 0) {
    console.error(`FAIL  unclassified project(s) under src/: ${unclassified.join(', ')}`);
    console.error('      Add each to SHIPPING_PROJECTS if it ships to NuGet,');
    console.error('      or teach isExemptProject why its constants are not covered.');
    return 1;
  }

  const stale = [...SHIPPING_PROJECTS].filter((p) => !dirs.includes(p));
  if (stale.length > 0) {
    console.error(`FAIL  SHIPPING_PROJECTS names project(s) that do not exist: ${stale.join(', ')}`);
    return 1;
  }

  console.log(`ok: ${dirs.length} project(s) under src/ classified.`);
  return 0;
}

function selfTest() {
  let failures = 0;

  for (const [name, expected] of NAME_CASES) {
    if (isCompliant(name) !== expected) {
      failures++;
      console.error(`FAIL  ${name}: expected ${expected ? 'compliant' : 'a violation'}`);
    }
  }

  for (const [source, expected] of SCAN_CASES) {
    const actual = findConstants(source).map((c) => c.name);
    if (JSON.stringify(actual) !== JSON.stringify(expected)) {
      failures++;
      console.error(`FAIL  ${JSON.stringify(source)}`);
      console.error(`      expected ${JSON.stringify(expected)}`);
      console.error(`      actual   ${JSON.stringify(actual)}`);
    }
  }

  // Line numbers are what a contributor navigates by, so the blanking must not shift
  // them — the reason comments are replaced with spaces rather than removed.
  const multiline = 'class C {\n  /* a\n     comment */\n  const int Ok = 1;\n}';
  const [only] = findConstants(multiline);
  if (!only || only.line !== 4) {
    failures++;
    console.error(`FAIL  expected the constant on line 4, got ${only ? only.line : 'nothing'}`);
  }

  failures += checkProjectRoster();

  if (failures > 0) {
    console.error(`\n${failures} case(s) failed.`);
    process.exit(1);
  }
  console.log(`ok: ${NAME_CASES.length + SCAN_CASES.length + 1} constant-naming case(s) pinned.`);
}

// ---- entry point --------------------------------------------------------------------

function main() {
  const args = process.argv.slice(2);

  if (args.includes('--self-test')) {
    selfTest();
    return;
  }

  const { violations, all } = scan();

  if (args.includes('--list')) {
    for (const c of all.sort((a, b) => a.file.localeCompare(b.file) || a.line - b.line)) {
      console.log(`${c.file}:${c.line}  ${c.name}`);
    }
    console.log(`\n${all.length} constant(s) in ${SHIPPING_PROJECTS.size} shipping package(s).`);
    return;
  }

  if (violations.length > 0) {
    console.error('Constants in the shipping packages are named in PascalCase (CONTRIBUTING.md,');
    console.error('"Coding conventions"). These are not:\n');
    for (const v of violations) {
      console.error(`  ${v.file}:${v.line}  ${v.name}`);
    }
    console.error(`\n${violations.length} constant(s) to rename.`);
    process.exit(1);
  }

  console.log(`ok: ${all.length} constant(s) in the shipping packages are PascalCase.`);
}

main();
