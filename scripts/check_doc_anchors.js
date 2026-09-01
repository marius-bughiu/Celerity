#!/usr/bin/env node
//
// Fails when a markdown link in this repository points at an anchor that does not exist,
// or at one that exists only by position and will not stay where it is.
//
// A broken intra-document link is invisible in review: the markdown is well-formed, the
// diff looks right, and the only symptom is that clicking the link scrolls nowhere. The
// class of rot that produced this check is worse than a typo, because the wrong anchor
// is the *intuitive* one. GitHub slugs a heading by lowercasing its rendered text and
// deleting punctuation without substituting a separator, so an entity-encoded generic
// heading such as
//
//     ## CeleritySet&lt;T, THasher&gt;
//
// renders as "CeleritySet<T, THasher>" and slugs to `celeritysett-thasher` — a double
// `t`, from `...Set` meeting `T` once the `<` between them is deleted — and not to the
// `celerityset-t-thasher` that everyone writes by hand.
//
// What is checked, over every tracked markdown file:
//   1. every same-file `](#fragment)` resolves to a heading slug or an explicit HTML
//      anchor in that file;
//   2. every relative `](other.md#fragment)` resolves in the file it names;
//   3. every relative link target that is not a URL exists on disk;
//   4. no link points at *any* member of a repeated-heading group. When a heading text
//      occurs more than once, GitHub numbers the repeats — `#measured`, `#measured-1`,
//      `#measured-2` — and every one of those ids is a position rather than a heading.
//      Inserting one more repeat *above* renames all of them downward, including the
//      unsuffixed first, so a link to the base id drifts exactly as a link to `-1` does.
//      Repeated headings are fine to *have* — CHANGELOG.md is built on them. This bans
//      linking *to* one. Give the target a hand-written `<a id>` of its own — a *unique*
//      one: an `<a id="measured-1">` that collides with a generated id renders a second
//      element carrying that id, and the fragment still resolves to whichever comes
//      first in the document, so a colliding anchor rescues nothing.
//
// The slug rule below mirrors github-slugger, which is what GitHub itself renders with.
// It was checked against the live rendering rather than inferred; to re-confirm it after
// a heading rename, ask GitHub for the rendered ids directly:
//
//   gh api repos/marius-bughiu/Celerity/contents/docs/api/collections.md \
//     -H "Accept: application/vnd.github.html" | grep -oE 'id="user-content-[a-z0-9-]*"'
//
// Usage:
//   node scripts/check_doc_anchors.js              # check every markdown file
//   node scripts/check_doc_anchors.js --list       # print every file's anchors and exit
//   node scripts/check_doc_anchors.js --self-test  # pin the slug rule itself
// CI runs the default and --self-test modes. Run from the repository root.

'use strict';

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const SKIP_DIRS = new Set([
  '.git', '.claude', 'node_modules', 'bin', 'obj', 'artifacts', 'TestResults',
]);

// ---- Slug rule ----------------------------------------------------------------------
// github-slugger: lowercase, delete punctuation, symbols and controls outright — note
// that no separator is substituted, which is the whole reason this check exists — then
// turn the surviving spaces into dashes. ASCII `-` and `_` are the two exceptions that
// survive; their Unicode cousins do not, so an em-dash or an arrow between two words
// leaves a doubled dash (`read-many--freeze-it`) rather than a single one.
//
// Stated as a keep-list rather than github-slugger's generated strip-list: keep letters,
// numbers, combining marks, spaces, `-` and `_`. That agrees with the live GitHub render
// on every heading in this repository, which the `gh api` recipe at the top re-confirms.
// A heading led by an emoji is the one shape not pinned that way; there are none here.

const SLUG_STRIP = /[^\p{L}\p{N}\p{M} _-]/gu;

function slugify(text) {
  return text.toLowerCase().trim().replace(SLUG_STRIP, '').replace(/ /g, '-');
}

// Repeated headings disambiguate with a `-1`, `-2`, ... suffix, counted per document.
// CHANGELOG.md leans on this heavily: every release repeats `### Added`.
//
// `base` is the slug before disambiguation, which is what groups the repeats together for
// rule 4. It matters that this is the *pre-suffix* slug rather than a regex strip of a
// trailing `-N`: a heading that slugs to `foo-1` on its own text — `### Foo 1` — has base
// `foo-1`, is in no group with `### Foo`, and stays a perfectly stable target.
function makeSlugger() {
  const occurrences = Object.create(null);
  return function slug(text) {
    const original = slugify(text);
    let result = original;
    while (occurrences[result] !== undefined) {
      occurrences[original] += 1;
      result = `${original}-${occurrences[original]}`;
    }
    occurrences[result] = 0;
    return { slug: result, base: original };
  };
}

// ---- Rendering a heading to its text content ----------------------------------------

const ENTITIES = {
  amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ',
  mdash: '—', ndash: '–', hellip: '…', copy: '©',
  reg: '®', times: '×', divide: '÷',
};

function decodeEntities(text) {
  return text.replace(/&(#x[0-9a-f]+|#\d+|[a-z][a-z0-9]*);/gi, (match, body) => {
    if (body[0] === '#') {
      const code = body[1] === 'x' || body[1] === 'X'
        ? parseInt(body.slice(2), 16)
        : parseInt(body.slice(1), 10);
      return Number.isFinite(code) ? String.fromCodePoint(code) : match;
    }
    const named = ENTITIES[body.toLowerCase()];
    return named === undefined ? match : named;
  });
}

// CommonMark's raw-HTML rule, not "anything between angle brackets". The distinction is
// load-bearing: `## PooledCeleritySet<T, THasher>` is written with bare angle brackets and
// is *not* a tag, because a tag name may only be followed by whitespace, `/` or `>` — so
// it renders as literal text and contributes its `T` to the slug, exactly as the
// entity-encoded headings elsewhere in the same file do.
const HTML_TAG =
  /<\/[A-Za-z][A-Za-z0-9-]*\s*>|<[A-Za-z][A-Za-z0-9-]*(?:\s+[A-Za-z_:][A-Za-z0-9_.:-]*(?:\s*=\s*(?:[^\s"'=<>`]+|'[^']*'|"[^"]*"))?)*\s*\/?>|<!--[\s\S]*?-->/g;

// Everything outside a code span renders: images contribute nothing, links contribute
// their label, emphasis markers and real tags vanish, and entities decode.
function renderProse(text) {
  return decodeEntities(
    text
      .replace(/!\[[^\]]*\]\([^)]*\)/g, '')
      .replace(/!\[[^\]]*\]\[[^\]]*\]/g, '')
      .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
      .replace(/\[([^\]]*)\]\[[^\]]*\]/g, '$1')
      .replace(HTML_TAG, '')
      .replace(/(\*\*\*|___|\*\*|__|\*|_|~~)/g, '')
      .replace(/\\([\\`*_{}\[\]()#+\-.!])/g, '$1')
  );
}

// A code span renders its content literally: no entity decoding, no tag stripping. That
// distinction matters here, because the same generic type name appears both ways in the
// docs — bare in a `##` heading and fenced in backticks in a `####` one.
function renderHeadingText(raw) {
  let out = '';
  let rest = raw;
  const fence = /(`+)([\s\S]*?)\1/;
  for (;;) {
    const m = fence.exec(rest);
    if (!m) {
      out += renderProse(rest);
      return out.trim();
    }
    out += renderProse(rest.slice(0, m.index));
    out += m[2].trim();
    rest = rest.slice(m.index + m[0].length);
  }
}

// ---- Parsing a markdown file --------------------------------------------------------

// Fenced blocks hold sample markdown and shell transcripts; neither defines an anchor nor
// is a link the reader can click. Strip them before doing anything else, keeping the line
// count intact so reported line numbers stay usable.
function blankFences(lines) {
  const out = lines.slice();
  let fence = null;
  for (let i = 0; i < out.length; i += 1) {
    const m = /^\s{0,3}(`{3,}|~{3,})/.exec(out[i]);
    if (fence === null) {
      if (m) {
        fence = m[1][0];
        out[i] = '';
      }
    } else {
      const closes = m && m[1][0] === fence;
      out[i] = '';
      if (closes) fence = null;
    }
  }
  return out;
}

// Link syntax inside a code span is a *specimen* of a link, not one — CONTRIBUTING.md
// quotes anchors verbatim to explain the slug rule, and none of them should be resolved.
function blankInlineCode(line) {
  return line.replace(/(`+)[\s\S]*?\1/g, '');
}

function parseFile(file) {
  return parseMarkdown(fs.readFileSync(file, 'utf8'));
}

function parseMarkdown(text) {
  const lines = blankFences(text.split(/\r?\n/));
  const slug = makeSlugger();
  const anchors = new Set();
  const groups = new Map(); // base slug -> the ids GitHub numbered off it, in order
  const links = [];

  lines.forEach((line, index) => {
    const heading = /^\s{0,3}#{1,6}\s+(.*?)\s*$/.exec(line);
    if (heading) {
      const text = renderHeadingText(heading[1].replace(/\s+#+\s*$/, ''));
      if (text) {
        const id = slug(text);
        anchors.add(id.slug);
        if (!groups.has(id.base)) groups.set(id.base, []);
        groups.get(id.base).push(id.slug);
      }
    }

    // Headings keep their code spans (the content renders and slugs); everything below
    // reads the line with them removed.
    const prose = blankInlineCode(line);

    // A hand-written `<a id>` / `<a name>` is an anchor too, and setext-style or HTML
    // headings are the reason to look for one.
    const declared = /<a\s[^>]*(?:id|name)\s*=\s*["']([^"']+)["']/gi;
    for (let m = declared.exec(prose); m; m = declared.exec(prose)) {
      anchors.add(m[1]);
    }

    // Inline links, reference definitions and raw `<a href>` all point somewhere.
    const targets = [];
    const inline = /\[[^\]]*\]\(\s*<?([^)\s>]+)>?(?:\s+"[^"]*")?\s*\)/g;
    for (let m = inline.exec(prose); m; m = inline.exec(prose)) targets.push(m[1]);
    const reference = /^\s{0,3}\[[^\]]+\]:\s*<?([^\s>]+)>?/.exec(prose);
    if (reference) targets.push(reference[1]);
    const href = /<a\s[^>]*href\s*=\s*["']([^"']+)["']/gi;
    for (let m = href.exec(prose); m; m = href.exec(prose)) targets.push(m[1]);

    for (const target of targets) {
      links.push({ target, line: index + 1 });
    }
  });

  // An id is stable only when it is the slug of its own heading's text *and* no other
  // heading wanted that slug. Two ways to fail: the heading repeats, so every id in the
  // group is a position — or the heading is unique but its slug was already taken by
  // someone else's repeat, so GitHub renumbered this one and will renumber it again if
  // that collision goes away.
  const positional = new Map();
  for (const [base, ids] of groups) {
    ids.forEach((id, index) => {
      if (ids.length < 2 && id === base) return;
      positional.set(id, { base, index, count: ids.length, renumbered: id !== base });
    });
  }
  return { anchors, positional, links };
}

// ---- Walking the tree ---------------------------------------------------------------

function walk(dir, found) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      if (!SKIP_DIRS.has(entry.name)) walk(path.join(dir, entry.name), found);
    } else if (entry.isFile() && entry.name.toLowerCase().endsWith('.md')) {
      found.push(path.join(dir, entry.name));
    }
  }
  return found;
}

// Tracked files only. A checkout can carry scratch worktrees and vendored copies of the
// docs, and reporting a stale duplicate of a link the working tree has already fixed is
// how a guard like this earns a reputation for crying wolf.
function collectMarkdown() {
  let files;
  try {
    files = execFileSync('git', ['ls-files', '-z', '--', '*.md', '*.MD'], { encoding: 'utf8' })
      .split('\0')
      .filter(Boolean);
  } catch {
    files = walk('.', []);
  }
  return files.map((f) => f.split(path.sep).join('/').replace(/^\.\//, '')).sort();
}

const POSITIONAL_PREFIX = 'positional anchor';

function positionalReason(fragment, where, group) {
  if (group.count < 2) {
    return `${POSITIONAL_PREFIX}: #${fragment} in ${where} is a renumbering — this `
      + `heading's own slug is "#${group.base}", which another heading had already `
      + 'taken, so the number moves if that one does';
  }
  const ordinal = `${group.index + 1} of ${group.count}`;
  return `${POSITIONAL_PREFIX}: #${fragment} is heading ${ordinal} sharing the slug `
    + `"#${group.base}" in ${where} — GitHub numbers repeats by position, so this `
    + 'retargets when another is inserted above it';
}

function isExternal(target) {
  return /^[a-z][a-z0-9+.-]*:/i.test(target) || target.startsWith('//');
}

// ---- Self-test ----------------------------------------------------------------------
// The slug rule is the guessable part of this file, and a wrong rule fails in the worst
// direction: it invents anchors nobody links to and pronounces the real ones missing.
// Every case below is a heading that exists in this repository, paired with the id GitHub
// actually rendered for it (via the `gh api` recipe at the top), so the rule is pinned to
// observed output rather than to a reading of github-slugger's source.

const SELF_TEST_CASES = [
  ['CeleritySet&lt;T, THasher&gt;', 'celeritysett-thasher'],
  ['SwissSet&lt;T, THasher&gt;', 'swisssett-thasher'],
  ['PooledCeleritySet<T, THasher>', 'pooledceleritysett-thasher'],
  ['IReadOnlyDictionary&lt;TKey, TValue?&gt;', 'ireadonlydictionarytkey-tvalue'],
  ['Why the struct constraint?', 'why-the-struct-constraint'],
  ['6. Build-once, read-many → freeze it', '6-build-once-read-many--freeze-it'],
  ['Dictionary → set parity: the specialized set family', 'dictionary--set-parity-the-specialized-set-family'],
  ['Milestone 2.4.0 — rolling post-roadmap work', 'milestone-240--rolling-post-roadmap-work'],
  ['When to choose it over `CelerityDictionary`', 'when-to-choose-it-over-celeritydictionary'],
  ['Span-keyed lookups (string keys)', 'span-keyed-lookups-string-keys'],
  ['VarInt (span varint codec)', 'varint-span-varint-codec'],
  ['`IsPerfectlyHashed` is `false` — is that a problem?', 'isperfectlyhashed-is-false--is-that-a-problem'],
];

function selfTest() {
  const failures = [];
  for (const [heading, expected] of SELF_TEST_CASES) {
    const actual = slugify(renderHeadingText(heading));
    if (actual !== expected) failures.push(`  ${heading}\n      expected #${expected}, got #${actual}`);
  }

  // Repeated headings disambiguate rather than collide — CHANGELOG.md depends on it.
  const slug = makeSlugger();
  const repeats = ['Added', 'Added', 'Added'].map((t) => slug(t).slug).join(' ');
  if (repeats !== 'added added-1 added-2') {
    failures.push(`  repeated headings\n      expected "added added-1 added-2", got "${repeats}"`);
  }

  for (const failure of ruleFourCases()) failures.push(failure);

  if (failures.length > 0) {
    console.error('error: the guard no longer behaves as pinned.\n');
    console.error(failures.join('\n'));
    process.exit(1);
  }
  console.log(`ok: ${SELF_TEST_CASES.length + 1 + RULE_FOUR_CASES.length} case(s) pinned.`);
}

// ---- Rule 4, pinned end to end ------------------------------------------------------
// The slug cases above prove the ids; these prove the *verdicts*, over whole documents
// run through the real `parseMarkdown` and `checkLinks`. Pinning the rule at the slugger
// alone would stay green if the wiring in either were deleted, which is most of what
// there is to get wrong here.

const RULE_FOUR_CASES = [
  {
    name: 'a link to a generated repeat is rejected',
    files: { 'a.md': '## Dup\n## Dup\n\n[x](#dup-1)\n' },
    expect: ['a.md:4 positional'],
  },
  {
    name: 'a link to the *unsuffixed* first of a repeated group is rejected too',
    files: { 'a.md': '## Dup\n## Dup\n\n[x](#dup)\n' },
    expect: ['a.md:4 positional'],
  },
  {
    name: 'a heading that occurs once is a stable target',
    files: { 'a.md': '## Solo\n## Other\n\n[x](#solo)\n' },
    expect: [],
  },
  {
    name: 'an authored suffix is not a generated one — `### Measured 1` stays linkable',
    files: { 'a.md': '## Measured\n## Measured 1\n\n[x](#measured-1)\n' },
    expect: [],
  },
  {
    name: 'a unique authored anchor rescues a repeated heading',
    files: { 'a.md': '## Dup\n<a id="dup-second"></a>\n\n## Dup\n\n[x](#dup-second)\n' },
    expect: [],
  },
  {
    name: 'an authored anchor colliding with a generated id rescues nothing',
    files: { 'a.md': '## Dup\n## Dup\n<a id="dup-1"></a>\n\n[x](#dup-1)\n' },
    expect: ['a.md:5 positional'],
  },
  {
    name: 'the cross-file path applies rule 4 as well',
    files: { 'a.md': '[x](b.md#dup-1)\n', 'b.md': '## Dup\n## Dup\n' },
    expect: ['a.md:1 positional'],
  },
  {
    name: 'a cross-file link to a unique heading still passes',
    files: { 'a.md': '[x](b.md#solo)\n', 'b.md': '## Solo\n' },
    expect: [],
  },
  {
    name: 'a heading renumbered around someone else\'s repeat is unstable too',
    files: { 'a.md': '## Foo\n## Foo\n## Foo 1\n\n[x](#foo-1-1)\n' },
    expect: ['a.md:5 positional'],
  },
  {
    name: 'a missing anchor is still a missing anchor',
    files: { 'a.md': '## Dup\n## Dup\n\n[x](#dup-7)\n' },
    expect: ['a.md:4 missing'],
  },
];

function ruleFourCases() {
  const failures = [];
  for (const { name, files, expect } of RULE_FOUR_CASES) {
    const names = Object.keys(files);
    const parsed = new Map(names.map((f) => [f, parseMarkdown(files[f])]));
    const problems = checkLinks(names, parsed, (f) => Object.hasOwn(files, f));
    const actual = problems.map(
      (p) => `${p.file}:${p.line} ${p.reason.startsWith(POSITIONAL_PREFIX) ? 'positional' : 'missing'}`
    );
    if (actual.join(' | ') !== expect.join(' | ')) {
      failures.push(
        `  ${name}\n      expected [${expect.join(', ')}], got [${actual.join(', ')}]`
      );
    }
  }
  return failures;
}

function checkLinks(files, parsed, exists) {
  const problems = [];

  for (const file of files) {
    const dir = path.posix.dirname(file);
    for (const { target, line } of parsed.get(file).links) {
      if (isExternal(target)) continue;

      const hash = target.indexOf('#');
      const rawPath = hash === -1 ? target : target.slice(0, hash);
      const encoded = hash === -1 ? '' : target.slice(hash + 1);
      let fragment = encoded;
      try {
        fragment = decodeURIComponent(encoded);
      } catch {
        // A malformed escape is not a percent-encoding; compare the fragment as written.
      }

      const verdict = (other, where) => {
        if (!other.anchors.has(fragment)) {
          return where === 'this file' ? 'no such anchor in this file' : `no such anchor in ${where}`;
        }
        const group = other.positional.get(fragment);
        return group ? positionalReason(fragment, where, group) : null;
      };

      if (rawPath === '') {
        const reason = fragment ? verdict(parsed.get(file), 'this file') : null;
        if (reason) problems.push({ file, line, target, reason });
        continue;
      }

      const resolved = path.posix.normalize(path.posix.join(dir, rawPath));
      if (resolved.startsWith('..')) continue; // outside the repository; not ours to check

      if (!exists(resolved)) {
        problems.push({ file, line, target, reason: `no such file: ${resolved}` });
        continue;
      }
      if (!fragment || !resolved.toLowerCase().endsWith('.md')) continue;

      const other = parsed.get(resolved);
      if (!other) continue; // an untracked or skipped markdown file

      const reason = verdict(other, resolved);
      if (reason) problems.push({ file, line, target, reason });
    }
  }

  return problems;
}

function main() {
  if (process.argv.includes('--self-test')) {
    selfTest();
    return;
  }

  const files = collectMarkdown();

  const parsed = new Map();
  for (const file of files) parsed.set(file, parseFile(file));

  if (process.argv.includes('--list')) {
    for (const file of files) {
      console.log(`${file}:`);
      const { anchors, positional } = parsed.get(file);
      for (const anchor of anchors) {
        console.log(`  #${anchor}${positional.has(anchor) ? '   (positional — do not link)' : ''}`);
      }
    }
    return;
  }

  const problems = checkLinks(files, parsed, (f) => fs.existsSync(f));

  if (problems.length > 0) {
    console.error(`error: ${problems.length} broken markdown link(s).\n`);
    for (const p of problems) {
      console.error(`  ${p.file}:${p.line}  ${p.target}`);
      console.error(`      ${p.reason}`);
    }
    console.error(
      '\nHeading anchors follow GitHub\'s slug rule: lowercase the *rendered* text, delete\n' +
      'punctuation without substituting a separator, then turn spaces into dashes. Run\n' +
      '`node scripts/check_doc_anchors.js --list` to see the anchors a file actually defines.'
    );
    if (problems.some((p) => p.reason.startsWith(POSITIONAL_PREFIX))) {
      console.error(
        '\nWhen a heading text repeats, GitHub numbers the repeats — `#measured`, `#measured-1`,\n' +
        '`#measured-2` — and none of those ids belongs to a heading. They belong to positions,\n' +
        'and one more repeat inserted *above* renames every one of them downward, the\n' +
        'unsuffixed first included. Put an anchor of its own on the heading you meant:\n\n' +
        '    <a id="measured-timerwheel"></a>\n\n' +
        '    ### Measured\n\n' +
        'then write `](#measured-timerwheel)`. Make it unique — an `<a id="measured-1">` only\n' +
        'adds a second element carrying an id GitHub already generated, and the fragment still\n' +
        'resolves to whichever of the two the document reaches first.'
      );
    }
    process.exit(1);
  }

  const anchorCount = files.reduce((sum, f) => sum + parsed.get(f).anchors.size, 0);
  const linkCount = files.reduce((sum, f) => sum + parsed.get(f).links.length, 0);
  console.log(
    `ok: ${linkCount} link(s) across ${files.length} markdown file(s) resolve; ` +
    `${anchorCount} anchor(s) defined.`
  );
}

main();
