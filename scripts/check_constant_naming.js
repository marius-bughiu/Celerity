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
//   - no upper-case run longer than a two-letter acronym, which is the .NET rule and the
//     one that rejects `NIL`, `EMPTY` and `DefaultCAPACITY` while keeping the transcribed
//     algorithm constants `C1`, `K0`, `M`, `R`. A run is measured as the acronym it
//     contains: in `IOStream` the run `IOS` ends a letter early, because that `S` opens
//     the next word, so the acronym is `IO` and it passes — where `XMLParser` and
//     `ParseXML` do not, and are spelled `XmlParser` / `ParseXml`;
//   - carries no underscore, except a trailing `_<digits>` index, which keeps
//     xxHash's `Prime64_1` family recognisable against the reference implementation.
// Case is judged in Unicode, not ASCII, and an identifier written with `\uXXXX` escapes
// is judged as the letters it means — a name the scan cannot read is one it cannot
// reject, which is the failure worth avoiding in a gate.
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

const PASCAL_CASE = /^\p{Lu}[\p{L}\p{Nd}]*(_[0-9]+)?$/u;
const UPPER_RUN = /\p{Lu}+/gu;
const MAX_ACRONYM = 2;

// C# identifiers are Unicode, and a character may also be written as an escape, so
// `échec` and `\u00e9chec` are one name spelled two ways. Both are judged as the
// letters they mean: the rule is about the reader, who sees the letter either way.
function decodeEscapes(name) {
  return name.replace(
    /\\u([0-9A-Fa-f]{4})|\\U([0-9A-Fa-f]{8})/g,
    (_, short, long) => String.fromCodePoint(parseInt(short || long, 16)),
  );
}

function isCompliant(name) {
  // `@` only escapes an identifier from the keyword list; the name is what follows it.
  const bare = decodeEscapes(name.startsWith('@') ? name.slice(1) : name);
  if (!PASCAL_CASE.test(bare)) return false;

  UPPER_RUN.lastIndex = 0;
  let run;
  while ((run = UPPER_RUN.exec(bare)) !== null) {
    // A run followed by a lower-case letter spends its last character on the next word,
    // so `IOStream` holds a two-letter acronym and `XMLParser` a three-letter one.
    const opensNextWord = /\p{Ll}/u.test(bare[run.index + run[0].length] || '');
    if (run[0].length - (opensNextWord ? 1 : 0) > MAX_ACRONYM) return false;
  }
  return true;
}

// ---- C# comment and literal stripping -----------------------------------------------
// `const` occurs inside comments and strings often enough that a raw regex over the file
// reports constants that do not exist. This blanks comments and literal *bodies* while
// preserving every newline, so reported line numbers stay exact.
//
// It is a mode stack rather than a scan for the next quote, because an interpolation hole
// is code that may hold further literals: in `$"{Format("const int Ghost = 1;")}"` the
// inner literal closes the outer one if you only count quotes, and the text between them
// resurfaces as a declaration nobody wrote.

function strip(source) {
  const out = [];
  const n = source.length;
  const stack = [{ kind: 'code', braces: 0, parens: 0, hole: false }];
  let i = 0;

  const blank = (text) => text.replace(/[^\n]/g, ' ');
  const emit = (count) => { out.push(blank(source.slice(i, i + count))); i += count; };

  while (i < n) {
    const frame = stack[stack.length - 1];
    const c = source[i];

    if (frame.kind === 'string') {
      if (frame.quotes >= 3) {
        // A raw literal closes on at least as many quotes as opened it, and needs as many
        // braces to open a hole as it has dollars.
        if (c === '"') {
          let k = 0;
          while (source[i + k] === '"') k++;
          emit(k);
          if (k >= frame.quotes) stack.pop();
          continue;
        }
        if (frame.dollars > 0 && c === '{') {
          let k = 0;
          while (source[i + k] === '{') k++;
          if (k >= frame.dollars) {
            emit(frame.dollars);
            stack.push({ kind: 'code', braces: 0, parens: 0, hole: true });
            continue;
          }
          emit(k);
          continue;
        }
        emit(1);
        continue;
      }

      if (frame.verbatim) {
        if (c === '"') {
          if (source[i + 1] === '"') { emit(2); continue; }   // an escaped quote
          emit(1);
          stack.pop();
          continue;
        }
      } else {
        if (c === '\\') { emit(2); continue; }
        if (c === frame.quote) { emit(1); stack.pop(); continue; }
        // A non-verbatim literal cannot span a line; resync rather than run away.
        if (c === '\n') { out.push('\n'); i++; stack.pop(); continue; }
      }

      if (frame.dollars > 0) {
        if (c === '{') {
          if (source[i + 1] === '{') { emit(2); continue; }
          emit(1);
          stack.push({ kind: 'code', braces: 0, parens: 0, hole: true });
          continue;
        }
        if (c === '}' && source[i + 1] === '}') { emit(2); continue; }
      }

      emit(1);
      continue;
    }

    if (c === '/' && source[i + 1] === '/') {
      const end = source.indexOf('\n', i);
      emit((end === -1 ? n : end) - i);
      continue;
    }

    if (c === '/' && source[i + 1] === '*') {
      const end = source.indexOf('*/', i + 2);
      emit((end === -1 ? n : end + 2) - i);
      continue;
    }

    // A literal may carry any mix of `$` and `@` in front of it: `$@"`, `@$"`, `$$"""`.
    const prefix = /^[$@]*/.exec(source.slice(i, i + 8))[0];
    if (source[i + prefix.length] === '"') {
      let k = 0;
      while (source[i + prefix.length + k] === '"') k++;
      const dollars = (prefix.match(/\$/g) || []).length;
      const quotes = k >= 3 ? k : 1;
      emit(prefix.length + quotes);
      stack.push({
        kind: 'string',
        quote: '"',
        verbatim: prefix.includes('@'),
        dollars: k >= 3 ? dollars : Math.min(dollars, 1),
        quotes,
      });
      continue;
    }

    if (c === "'") {
      emit(1);
      stack.push({ kind: 'string', quote: "'", verbatim: false, dollars: 0, quotes: 1 });
      continue;
    }

    if (frame.hole) {
      // The hole ends at the `}` matching the brace that opened it, handing the rest of
      // the literal back to the string frame underneath.
      if (c === '{') { frame.braces++; out.push(c); i++; continue; }
      if (c === '}') {
        if (frame.braces === 0) { emit(1); stack.pop(); continue; }
        frame.braces--;
        out.push(c);
        i++;
        continue;
      }
      if (c === '(' || c === '[') { frame.parens++; out.push(c); i++; continue; }
      if (c === ')' || c === ']') { frame.parens--; out.push(c); i++; continue; }

      // A colon at the top level of a hole ends the expression and opens a format
      // specifier, whose text is literal — `$"{42:const int bad_name = 1;}"` declares
      // nothing. This is unambiguous precisely because C# rejects an unparenthesized
      // conditional in a hole for the same reason, so any ternary colon is inside the
      // parentheses that tracking above accounts for. `::` is qualification, not a
      // separator.
      if (c === ':' && frame.parens === 0 && frame.braces === 0
          && source[i + 1] !== ':' && source[i - 1] !== ':') {
        const close = source.indexOf('}', i);
        emit((close === -1 ? n : close) - i);
        continue;
      }
    }

    out.push(c);
    i++;
  }

  return out.join('');
}

// ---- The scan -----------------------------------------------------------------------
// A declaration is `const <type> <name> = ...`, where the type may be generic, an array,
// nullable or qualified, and where further `, <name> = ...` declarators may follow on the
// same statement. The type may be alias-qualified (`global::System.Int32`), and either
// identifier may be `@`-escaped, which is only a way past the keyword list and no part of
// the name — a `const int @bad_name` the pattern failed to match would walk past this
// gate untouched. The same goes for the Unicode a C# identifier is allowed: a declaration
// the pattern cannot see is not judged at all, which is worse than one it rejects, so the
// identifier class here is the language's rather than ASCII.

const ID_CHAR = '(?:[\\p{L}\\p{Nl}\\p{Nd}\\p{Mn}\\p{Mc}\\p{Pc}\\p{Cf}]|\\\\u[0-9A-Fa-f]{4}|\\\\U[0-9A-Fa-f]{8})';
const IDENTIFIER = `@?${ID_CHAR}+`;
const CONST_DECL = new RegExp(
  `\\bconst\\s+(?:${ID_CHAR}|[@.:<>,\\[\\]?\\\\\\s])+?\\s+(${IDENTIFIER})\\s*=`,
  'gu',
);

function findConstants(source) {
  const stripped = strip(source);
  const found = [];
  const lineAt = (index) => stripped.slice(0, index).split('\n').length;

  CONST_DECL.lastIndex = 0;
  let match;
  while ((match = CONST_DECL.exec(stripped)) !== null) {
    // Locate the identifier itself, not the start of the statement: a declaration may
    // wrap, and the line reported is the line someone will open.
    found.push({ name: match[1], line: lineAt(match.index + match[0].lastIndexOf(match[1])) });

    // Trailing declarators: `const int A = 1, B = 2;` declares B as well. Each is located
    // on its own, because a statement may wrap and reporting the first declarator's line
    // for all of them sends a contributor to the wrong one.
    const tailStart = CONST_DECL.lastIndex;
    const semicolon = stripped.indexOf(';', match.index);
    const tail = stripped.slice(tailStart, semicolon === -1 ? undefined : semicolon);
    const extra = new RegExp(`,\\s*(${IDENTIFIER})\\s*=`, 'gu');
    let more;
    while ((more = extra.exec(tail)) !== null) {
      found.push({
        name: more[1],
        line: lineAt(tailStart + more.index + more[0].lastIndexOf(more[1])),
      });
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
  ['Prime64_1', true],    // transcribed from the xxHash reference
  ['Prime32_3', true],
  ['C1', true],           // two-character algorithm symbols keep their spec names
  ['K0', true],
  ['M', true],
  ['IOStream', true],     // a two-letter acronym; the `S` belongs to the next word
  ['@Ok', true],
  ['DEFAULT_CAPACITY', false],
  ['MAX_GRID', false],
  ['TWO_POW_32', false],
  ['EMPTY', false],
  ['NIL', false],         // three characters is past the acronym allowance
  ['fnvPrime', false],
  ['offsetBasis', false],
  ['_seed', false],
  ['Max_Kicks', false],   // an underscore may only introduce a digit index
  ['DefaultCAPACITY', false],  // half-converted, and what a looser rule lets through
  ['XMLParser', false],   // a three-letter acronym is PascalCased: XmlParser
  ['ParseXML', false],    // still three letters when it ends the name
  ['@bad_name', false],   // the escape is not part of the name
  ['Écran', true],     // the rule is Unicode: an upper-case letter is one
  ['échec', false],
  ['\\u00e9chec', false],   // ...and as an escape, it is still that letter
  ['\\u00c9cran', true],
];

const SCAN_CASES = [
  ['class C { private const int MaxKicks = 500; }', ['MaxKicks']],
  ['class C { public const double Ln2 = 0.69; }', ['Ln2']],
  ['void M() { const ulong FnvPrime = 1099511628211UL; }', ['FnvPrime']],
  ['class C { const int A = 1, B = 2; }', ['A', 'B']],
  ['class C { private const Vector128<sbyte> Mask = default; }', ['Mask']],
  ['class C { private const int?  Wide = null; }', ['Wide']],
  ['class C { const int @bad_name = 1; }', ['@bad_name']],
  ['class C { const global::System.Int32 Ok = 1; }', ['Ok']],
  // A declaration the scan cannot see is never judged, which is the worse failure:
  // Unicode identifiers are found and then rejected by the name rule, not skipped.
  ['class C { const int échec = 1; }', ['échec']],
  ['class C { const int \\u00e9chec = 1; }', ['\\u00e9chec']],
  // Comments and literals are not declarations, however much they look like one.
  ['class C { /* const int Ghost = 1; */ }', []],
  ['class C { // const int Ghost = 1;\n }', []],
  ['class C { string s = "const int Ghost = 1;"; }', []],
  ['class C { string s = @"const int Ghost = 1;"; }', []],
  ['class C { string s = """const int Ghost = 1;"""; }', []],
  ['class C { char c = \'"\'; const int Ok = 1; }', ['Ok']],
  // An interpolation hole is code, so a literal inside one does not close the literal
  // around it. Count quotes instead of tracking holes and the text between the two inner
  // quotes reads as a declaration.
  ['class C { string s = $"{Format("const int Ghost = 1;")}"; }', []],
  ['class C { string s = $@"{Format("const int Ghost = 1;")}"; }', []],
  ['class C { string s = $"""{Format("const int Ghost = 1;")}"""; }', []],
  // The converse: a real declaration inside a hole is still a declaration.
  ['class C { string s = $"{Run(() => { const int Held = 1; return Held; })}"; }', ['Held']],
  // Text after a top-level colon in a hole is a format specifier, so it is literal...
  ['class C { string s = $"{42:const int Ghost = 1;}"; }', []],
  // ...while a colon inside parentheses is not one. C# rejects the unparenthesized
  // conditional that would make the two ambiguous, so the code after it is still scanned.
  ['class C { string s = $"{(f ? 0 : Run(() => { const int Held = 1; return Held; }))}"; }', ['Held']],
  ['class C { string s = $"{global::C.M()}"; const int Ok = 1; }', ['Ok']],
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

  // A wrapped statement puts its declarators on different lines, and each has to report
  // its own — the first one's line is where a contributor would look and find nothing.
  const wrapped = 'class C {\n  const int Ok = 1,\n    AlsoOk = 2;\n}';
  const declarators = findConstants(wrapped).map((c) => `${c.name}:${c.line}`).join(' ');
  if (declarators !== 'Ok:2 AlsoOk:3') {
    failures++;
    console.error(`FAIL  expected "Ok:2 AlsoOk:3" from a wrapped declaration, got "${declarators}"`);
  }

  failures += checkProjectRoster();

  if (failures > 0) {
    console.error(`\n${failures} case(s) failed.`);
    process.exit(1);
  }
  console.log(`ok: ${NAME_CASES.length + SCAN_CASES.length + 2} constant-naming case(s) pinned.`);
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
    console.error('"Constant naming"). These are not:\n');
    for (const v of violations) {
      console.error(`  ${v.file}:${v.line}  ${v.name}`);
    }
    console.error(`\n${violations.length} constant(s) to rename.`);
    process.exit(1);
  }

  console.log(`ok: ${all.length} constant(s) in the shipping packages are PascalCase.`);
}

main();
