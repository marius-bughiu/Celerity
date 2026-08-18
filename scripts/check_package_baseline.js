#!/usr/bin/env node
//
// Fails when the package-validation baseline no longer points at the last published release.
//
// `CelerityPackageValidationBaseline` in src/Directory.Build.props is what every package
// is compared against when `dotnet pack` runs the binary-compatibility gate, and it is
// bumped by a *manual follow-up commit* after each release — deliberately, because the
// value becomes a PackageDownload and a version that is not indexed yet fails the release
// build's restore. Nothing verified that the follow-up ever happened. It did not happen
// for v2.6.0, and the miss went unnoticed for the whole cycle (#364): every package went
// on validating against its 2.5.0 predecessor, so a break introduced *in* 2.6.0 would have
// packed green. Worse, Celerity.Sorting still carried CelerityNoPublishedBaseline — the
// first-release escape hatch — so the most recently shipped package sat outside the gate
// entirely rather than merely behind it.
//
// Both failures are silent by construction: CI stays green, `dotnet pack` stays green, and
// the gate simply stops covering the surface most likely to have moved. That is the same
// shape as #314 (coverage filter), #339 (doc anchors) and #301 (dashboard coverage), each
// of which was closed by a script that asserts the invariant out loud.
//
// The invariant, stated exactly:
//
//     the baseline == the highest stable version published by *every gated* package.
//
// Not "the highest version on NuGet", and not "the newest git tag". The baseline is a
// single shared property, so it resolves to a PackageDownload for all gated packages at
// once and has to be a version each of them actually has — Celerity.Sorting first shipped
// at 2.6.0 and has no 2.5.0 to download, so the intersection is the constraint and the
// maximum is not. "Gated" excludes a package still on the escape hatch, which resolves no
// baseline at all: counting an unpublished newcomer would empty the intersection and
// pronounce every other package's correct baseline wrong.
//
// Stating it against *published* versions rather than git tags is what makes the release
// window fall out for free instead of needing a grace period. Between tagging vX.Y.Z and
// the packages appearing on NuGet.org the baseline is legitimately one release behind, and
// a tag-based rule can only tolerate that by allowing a one-release lag in general — which
// is precisely the drift that went unnoticed, so such a rule could never have caught it.
// Asking NuGet closes the window on the real condition: the moment the release is indexed
// the bump is due, and not one moment before. It also catches the bump landing too *early*,
// which the props comment warns about and which breaks the release build's restore.
//
// The cost is a network call in CI, where the three sibling guards are hermetic. It is
// deliberately not allowed to invent failures: an unreachable NuGet is reported and skipped
// rather than failed, because a guard that reds a pull request over a transient outage gets
// ignored, and this check runs on every PR so a genuine miss is caught by the next one. The
// offline half of the check always runs.
//
// What is checked:
//   1. (offline) the baseline property exists, parses, and is a stable version — a
//      prerelease baseline silently validates against a nightly preview;
//   2. (offline) every shipped package is discovered from the repository rather than from
//      a hardcoded list — by the same `IsPackable != false` test the gate itself uses, with
//      the SDK's default PackageId derived rather than treated as an absence, so an eighth
//      package cannot join without joining this check too;
//   3. (network) the baseline equals the highest stable version published by all the gated
//      ones;
//   4. (network) no package sets CelerityNoPublishedBaseline while it *has* a published
//      stable release — the condition that removes it from the gate entirely.
//
// Everything that decides pass or fail is a pure function of (baseline, packages), so
// --self-test drives all of it from fixtures: the version rules, the project-discovery
// rules, and every failure direction listed above. Without that, the only case CI ever
// executed was the all-equal happy path, and a regression in any failure branch would have
// left both steps green — the same "a guard too permissive reads exactly like a clean
// report" problem that ci.yml's benchmark-gate comment records.
//
// Usage:
//   node scripts/check_package_baseline.js              # offline checks + NuGet
//   node scripts/check_package_baseline.js --offline    # skip the network half
//   node scripts/check_package_baseline.js --self-test  # pin the rules against fixtures
// CI runs the default and --self-test modes. Run from the repository root.

'use strict';

const fs = require('fs');
const path = require('path');
const https = require('https');

// Written with forward slashes because these strings are read back to the user as much as
// they are passed to fs; Node resolves either separator on Windows.
const PROPS = 'src/Directory.Build.props';
const SRC = 'src';
const SELF = 'scripts/check_package_baseline.js';
const FLAT_CONTAINER = 'https://api.nuget.org/v3-flatcontainer';

function fail(message) {
  console.error(`error: ${message}`);
  process.exit(1);
}

// ---- Version rules ------------------------------------------------------------------
// NuGet versions are `Major.Minor.Patch[.Revision][-prerelease][+metadata]`, compared
// numerically part by part. Two details are the guessable ones and are pinned by the
// self-test below: a missing part is zero, so 2.6 and 2.6.0 are the same version and
// 2.6.0.1 is *newer* than 2.6.0; and a prerelease sorts below the release it precedes,
// which is why they are filtered out entirely rather than ranked.

// A prerelease or metadata suffix is dot-separated *non-empty* identifiers, not "any run
// of those characters" — `2.6.0-.`, `2.6.0-foo..bar` and `2.6.0+.` are malformed, and a
// lenient class would accept `2.6.0+.` as a stable 2.6.0 that restore then cannot resolve.
// Rejecting them outright routes such a baseline to the "not a version number" problem
// instead of letting it compare equal to a real release.
const IDENTIFIERS = '[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*';
const VERSION = new RegExp(`^(\\d+(?:\\.\\d+){0,3})(?:-(${IDENTIFIERS}))?(?:\\+${IDENTIFIERS})?$`);

function parseVersion(text) {
  const m = VERSION.exec(String(text).trim());
  if (!m) return null;
  const parts = m[1].split('.').map(Number);
  while (parts.length < 4) parts.push(0);
  return { parts, prerelease: m[2] || null };
}

function isStable(text) {
  const v = parseVersion(text);
  return v !== null && v.prerelease === null;
}

// Release-part comparison only; every caller has already filtered prereleases out.
function compareVersions(a, b) {
  const va = parseVersion(a);
  const vb = parseVersion(b);
  if (va === null || vb === null) return 0;
  for (let i = 0; i < 4; i += 1) {
    if (va.parts[i] !== vb.parts[i]) return va.parts[i] < vb.parts[i] ? -1 : 1;
  }
  return 0;
}

// `2.6` and `2.6.0.0` are the same version to NuGet but not to string equality, and the
// props file is hand-edited — so the baseline is compared on value, and displayed as the
// three-part form the file is expected to carry.
function normalize(text) {
  const v = parseVersion(text);
  if (v === null) return String(text);
  const parts = v.parts.slice();
  while (parts.length > 3 && parts[parts.length - 1] === 0) parts.pop();
  return parts.join('.');
}

function maxStable(versions) {
  return versions
    .filter(isStable)
    .reduce((best, v) => (best === null || compareVersions(v, best) > 0 ? v : best), null);
}

// ---- Reading the repository ---------------------------------------------------------
// These are text scans, not an MSBuild evaluation — which is the right trade for a check
// that has to answer in seconds, but it means the scan has to agree with MSBuild about
// what is *live*. A commented-out element is inert to MSBuild and must be inert here too,
// or the guard reads a value the build never sees: a stale `<CelerityPackageValidationBaseline>`
// left in an example above the real one would be matched first and reported as correct,
// and a commented `<IsPackable>false</IsPackable>` would drop a shipping package out of the
// guard while the gate went on validating it. Both fail green, which is the one direction
// this file is not allowed to fail in.
//
// This repository invites exactly that: the props file carries a long comment block about
// the bump ritual directly above the property it describes.

function stripXmlComments(source) {
  return source.replace(/<!--[\s\S]*?-->/g, '');
}

// Pure, so the self-test can drive it; readBaseline() adds the file handling.
function findBaseline(source) {
  const m = /<CelerityPackageValidationBaseline>\s*([^<\s]+)\s*<\/CelerityPackageValidationBaseline>/
    .exec(stripXmlComments(source));
  return m ? m[1] : null;
}

function readBaseline() {
  let source;
  try {
    source = fs.readFileSync(PROPS, 'utf8');
  } catch (e) {
    fail(`could not read ${PROPS}: ${e.message}`);
  }
  const found = findBaseline(source);
  if (found === null) {
    fail(
      `could not find <CelerityPackageValidationBaseline> in ${PROPS}. Either the property ` +
      `was renamed — in which case update ${SELF} and src/Directory.Build.targets together — ` +
      `or the binary-compatibility gate has lost its baseline entirely.`
    );
  }
  return found;
}

// Returns the package a project ships, or null when it ships nothing.
//
// The packable test is `IsPackable != false`, which is exactly the condition
// Directory.Build.targets applies when it sets PackageValidationBaselineVersion — so this
// sees the same set the gate does. Deciding it on the *presence of a PackageId* instead
// would leave a hole shaped like the one this whole check exists to close: the SDK defaults
// PackageId to AssemblyName, and AssemblyName to the project's file name, so a new project
// with no explicit <PackageId> element packs and ships perfectly well — possibly with the
// escape hatch set — while a PackageId-keyed guard skipped it silently and forever.
//
// The default is therefore *derived* rather than treated as an absence. Resolving it the
// way MSBuild would means evaluating the project, which is a restore this seconds-long
// check has no business doing; the two-step SDK default is reproduced instead, and the
// explicit element still wins where there is one (Celerity.csproj ships as
// Celerity.Collections, which is precisely why the element cannot be assumed away).
function readProject(raw, projectPath) {
  const source = stripXmlComments(raw);
  if (/<IsPackable>\s*false\s*<\/IsPackable>/i.test(source)) return null;

  const explicit = /<PackageId>\s*([^<\s]+)\s*<\/PackageId>/.exec(source);
  const assembly = /<AssemblyName>\s*([^<\s]+)\s*<\/AssemblyName>/.exec(source);
  const fileName = projectPath.split(/[\\/]/).pop().replace(/\.csproj$/i, '');

  return {
    id: explicit ? explicit[1] : (assembly ? assembly[1] : fileName),
    implicitId: !explicit,
    project: projectPath.split(path.sep).join('/'),
    noBaseline: /<CelerityNoPublishedBaseline>\s*true\s*<\/CelerityNoPublishedBaseline>/i.test(source),
  };
}

function discoverPackages() {
  const projects = [];
  for (const entry of fs.readdirSync(SRC, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const dir = path.join(SRC, entry.name);

    // Every project file in the directory, not `${dir}/${dir}.csproj` — the two agree
    // today, but a guard that silently sees nothing when they stop agreeing is the exact
    // failure being guarded against.
    for (const file of fs.readdirSync(dir)) {
      if (!file.toLowerCase().endsWith('.csproj')) continue;
      const proj = path.join(dir, file);
      const found = readProject(fs.readFileSync(proj, 'utf8'), proj);
      if (found) projects.push(found);
    }
  }
  return projects.sort((a, b) => a.id.localeCompare(b.id));
}

// ---- Asking NuGet -------------------------------------------------------------------
// The flat-container index is the cheapest published-versions endpoint there is: a static
// JSON blob off the CDN, no auth, no search index lag. A 404 is a real answer — it means
// the package id has never been published — so it is mapped to "no versions" rather than
// to an error. Anything else is a transport problem and stops the network half.

// `validate` runs on the parsed body and turns an unexpected *shape* into a transport
// failure. That distinction is load-bearing: a 404 means "never published" and is an answer
// this check acts on, while a 200 carrying something other than the expected document means
// the lookup did not work — and treating the latter as "no versions" would report a
// perfectly healthy package as gated-but-never-published, a hard repository failure, on the
// strength of a bad response. Raising it inside the promise puts it on the retry-then-warn
// path with every other transport problem.
function getJson(url, validate, attempt = 1) {
  return new Promise((resolve, reject) => {
    const request = https.get(url, { timeout: 15000 }, (response) => {
      const { statusCode } = response;
      if (statusCode === 404) {
        response.resume();
        resolve(null);
        return;
      }
      if (statusCode !== 200) {
        response.resume();
        reject(new Error(`HTTP ${statusCode} from ${url}`));
        return;
      }
      let body = '';
      response.setEncoding('utf8');
      // A connection dropped after the headers emits on the *response*, not the request.
      // Without this listener Node raises it as an unhandled 'error' event and takes the
      // process down — skipping the retry-and-warn policy this file documents.
      response.on('error', reject);
      response.on('aborted', () => reject(new Error(`response aborted: ${url}`)));
      response.on('data', (chunk) => { body += chunk; });
      response.on('end', () => {
        let parsed;
        try {
          parsed = JSON.parse(body);
        } catch (e) {
          reject(new Error(`malformed JSON from ${url}: ${e.message}`));
          return;
        }
        const complaint = validate ? validate(parsed) : null;
        if (complaint) reject(new Error(`${complaint}: ${url}`));
        else resolve(parsed);
      });
    });
    request.on('timeout', () => request.destroy(new Error(`timed out after 15s: ${url}`)));
    request.on('error', reject);
  }).catch((e) => {
    // One retry, because the alternative to tolerating a blip is a red pull request that
    // says nothing about the repository.
    if (attempt >= 2) throw e;
    return new Promise((resolve) => setTimeout(resolve, 2000)).then(() => getJson(url, validate, attempt + 1));
  });
}

function validateIndex(body) {
  if (body === null || typeof body !== 'object' || !Array.isArray(body.versions)) {
    return 'flat-container index has no versions array';
  }
  return null;
}

async function publishedVersions(id) {
  // null is the 404: the package id has never been published, which is a real answer.
  const index = await getJson(`${FLAT_CONTAINER}/${id.toLowerCase()}/index.json`, validateIndex);
  return index === null ? [] : index.versions;
}

// ---- Self-test ----------------------------------------------------------------------
// The version rules are the part of this file that is reasoned about rather than observed,
// and getting them wrong fails in the direction that costs the most: a comparison that
// mis-ranks 2.10.0 below 2.9.0 pronounces a correctly bumped baseline stale, and a
// prerelease filter that lets `2.6.1-beta.6` through would have called the *nightly*
// preview the latest release and demanded the baseline point at it.

const VERSION_CASES = [
  ['2.6.0', '2.5.0', 1],
  ['2.10.0', '2.9.0', 1],        // numeric, not lexicographic
  ['10.0.0', '9.9.9', 1],
  ['2.6.0', '2.6.0', 0],
  ['2.6', '2.6.0', 0],           // a missing part is zero
  ['2.6.0.1', '2.6.0', 1],       // ...and a fourth part still counts
  ['1.0.1', '1.1.0', -1],
];

const STABILITY_CASES = [
  ['2.6.0', true],
  ['2.6.1-beta.6', false],       // the nightly preview stream this repo publishes
  ['0.0.0-beta.7', false],
  ['2.0.1-beta.1', false],
  ['2.6.0+build.5', true],       // build metadata is not a prerelease
  ['not-a-version', false],
];

function selfTest() {
  const failures = [];

  for (const [a, b, expected] of VERSION_CASES) {
    const actual = compareVersions(a, b);
    if (actual !== expected) failures.push(`  compare("${a}", "${b}") expected ${expected}, got ${actual}`);
  }

  for (const [text, expected] of STABILITY_CASES) {
    const actual = isStable(text);
    if (actual !== expected) failures.push(`  isStable("${text}") expected ${expected}, got ${actual}`);
  }

  // The real shape this check reads: an ascending list with the nightly prereleases
  // interleaved, where the newest entry is a prerelease and must not be chosen.
  const sample = ['2.5.0', '2.5.1-beta.4', '2.6.0', '2.6.1-beta.1', '2.6.1-beta.6'];
  if (maxStable(sample) !== '2.6.0') {
    failures.push(`  maxStable(${JSON.stringify(sample)}) expected "2.6.0", got "${maxStable(sample)}"`);
  }
  if (maxStable(['1.0.0-beta.1']) !== null) {
    failures.push('  maxStable of a prerelease-only package expected null');
  }

  // The intersection rule, which is what makes a newly added package's first release
  // survivable: Sorting shipped at 2.6.0 only, so 2.6.0 is the newest baseline that all
  // of them can resolve, even though the older packages have plenty of earlier releases.
  const common = commonBaseline([
    { id: 'Celerity.Collections', stable: ['2.4.0', '2.5.0', '2.6.0'] },
    { id: 'Celerity.Sorting', stable: ['2.6.0'] },
  ]);
  if (common !== '2.6.0') failures.push(`  commonBaseline expected "2.6.0", got "${common}"`);

  const none = commonBaseline([
    { id: 'A', stable: ['2.6.0'] },
    { id: 'B', stable: [] },
  ]);
  if (none !== null) failures.push(`  commonBaseline with an unpublished gated package expected null, got "${none}"`);

  // The same unpublished package on the escape hatch is out of the gate, so it must not
  // drag the baseline down with it — this is the eighth-package case.
  const newcomer = commonBaseline([
    { id: 'Celerity.Collections', stable: ['2.5.0', '2.6.0'] },
    { id: 'Celerity.Statistics', stable: [], noBaseline: true },
  ]);
  if (newcomer !== '2.6.0') failures.push(`  commonBaseline ignoring the escape hatch expected "2.6.0", got "${newcomer}"`);

  for (const [label, source, file, expected] of PROJECT_CASES) {
    const actual = readProject(source, file);
    const got = actual === null ? 'none' : `${actual.id}${actual.noBaseline ? ' (hatched)' : ''}`;
    if (got !== expected) failures.push(`  readProject/${label} expected "${expected}", got "${got}"`);
  }

  for (const [label, source, expected] of BASELINE_CASES) {
    const actual = findBaseline(source);
    if (actual !== expected) failures.push(`  findBaseline/${label} expected ${expected}, got ${actual}`);
  }

  // A 200 whose body is not a flat-container index is a failed lookup, not an unpublished
  // package — misreading it turns a healthy package into a hard "never published" failure.
  for (const [label, body, shouldComplain] of [
    ['a real index', { versions: ['2.6.0'] }, false],
    ['an empty index', { versions: [] }, false],
    ['no versions array', { message: 'blocked' }, true],
    ['not an object', 'nope', true],
    ['null', null, true],
  ]) {
    if ((validateIndex(body) !== null) !== shouldComplain) {
      failures.push(`  validateIndex/${label} expected complain=${shouldComplain}`);
    }
  }

  for (const [label, baseline, packages, expected] of ANALYSIS_CASES) {
    const found = analyze(baseline, packages, true).length;
    if (found !== expected) {
      failures.push(
        `  analyze/${label} expected ${expected} problem(s), got ${found}` +
        (found > 0 ? `:\n      ${analyze(baseline, packages, true).join('\n      ')}` : '')
      );
    }
  }

  // The offline half must report the baseline's own shape and nothing else — a package
  // list with no version data cannot be allowed to invent a stale-baseline finding.
  const offline = analyze('2.5.0', [{ id: 'A', project: 'a', noBaseline: false }], false);
  if (offline.length !== 0) failures.push(`  analyze/offline expected 0 problems, got ${offline.length}`);

  if (failures.length > 0) {
    console.error('error: the package-baseline rules no longer hold.\n');
    console.error(failures.join('\n'));
    process.exit(1);
  }
  const total =
    VERSION_CASES.length + STABILITY_CASES.length + 5 + PROJECT_CASES.length +
    BASELINE_CASES.length + 5 + ANALYSIS_CASES.length + 1;
  console.log(`ok: ${total} case(s) pinned.`);
}

// ---- Self-test fixtures --------------------------------------------------------------

// Project shapes, as csproj text. The implicit-PackageId rows are the ones that matter:
// the gate keys on IsPackable alone, so a project the SDK names for itself still ships and
// must still be seen here.
const PROJECT_CASES = [
  ['explicit id wins over the file name',
    '<Project><PropertyGroup><PackageId>Celerity.Collections</PackageId></PropertyGroup></Project>',
    'src/Celerity/Celerity.csproj', 'Celerity.Collections'],
  ['implicit id falls back to the file name',
    '<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>',
    'src/Celerity.Statistics/Celerity.Statistics.csproj', 'Celerity.Statistics'],
  ['implicit id prefers AssemblyName',
    '<Project><PropertyGroup><AssemblyName>Celerity.Renamed</AssemblyName></PropertyGroup></Project>',
    'src/Celerity.Statistics/Celerity.Statistics.csproj', 'Celerity.Renamed'],
  ['IsPackable=false ships nothing',
    '<Project><PropertyGroup><IsPackable>false</IsPackable></PropertyGroup></Project>',
    'src/Celerity.Tests/Celerity.Tests.csproj', 'none'],
  ['the escape hatch is detected',
    '<Project><PropertyGroup><PackageId>Celerity.Sorting</PackageId>' +
    '<CelerityNoPublishedBaseline>true</CelerityNoPublishedBaseline></PropertyGroup></Project>',
    'src/Celerity.Sorting/Celerity.Sorting.csproj', 'Celerity.Sorting (hatched)'],

  // A commented element is inert to MSBuild and must be inert here. Getting this wrong
  // fails green: the package silently leaves the guard while the gate goes on validating it.
  ['a commented IsPackable does not unship a package',
    '<Project><PropertyGroup><!-- <IsPackable>false</IsPackable> -->' +
    '<PackageId>Celerity.Collections</PackageId></PropertyGroup></Project>',
    'src/Celerity/Celerity.csproj', 'Celerity.Collections'],
  ['a commented PackageId does not name the package',
    '<Project><PropertyGroup><!-- <PackageId>Celerity.Old</PackageId> --></PropertyGroup></Project>',
    'src/Celerity.Statistics/Celerity.Statistics.csproj', 'Celerity.Statistics'],
  ['a commented escape hatch does not hatch the package',
    '<Project><PropertyGroup><PackageId>Celerity.Sorting</PackageId>' +
    '<!-- <CelerityNoPublishedBaseline>true</CelerityNoPublishedBaseline> --></PropertyGroup></Project>',
    'src/Celerity.Sorting/Celerity.Sorting.csproj', 'Celerity.Sorting'],
];

// The props file carries a long comment block about the bump ritual directly above the
// property, so an example element written in it is the realistic way this goes wrong — and
// it goes wrong green, reporting a stale baseline as correct.
const BASELINE_CASES = [
  ['the plain property', '<Project><PropertyGroup><CelerityPackageValidationBaseline>2.6.0' +
    '</CelerityPackageValidationBaseline></PropertyGroup></Project>', '2.6.0'],
  ['a commented example above the live one',
    '<Project><!-- e.g. <CelerityPackageValidationBaseline>2.5.0</CelerityPackageValidationBaseline> -->' +
    '<PropertyGroup><CelerityPackageValidationBaseline>2.6.0</CelerityPackageValidationBaseline>' +
    '</PropertyGroup></Project>', '2.6.0'],
  ['a commented-out property and no live one',
    '<Project><!-- <CelerityPackageValidationBaseline>2.6.0</CelerityPackageValidationBaseline> --></Project>',
    null],
];

// Every documented failure direction, plus the two shapes that must *not* fail. Before
// these existed the only case CI ever ran was `healthy`.
const shipped = (id, stable, noBaseline) => ({ id, project: `src/${id}/${id}.csproj`, stable, noBaseline });

const ANALYSIS_CASES = [
  ['healthy', '2.6.0',
    [shipped('Celerity.Collections', ['2.5.0', '2.6.0']), shipped('Celerity.Sorting', ['2.6.0'])], 0],

  // The v2.6.0 miss itself: baseline left a release behind, and the newest package still
  // on the hatch. Two independent findings, which is how it actually presented.
  ['the v2.6.0 miss', '2.5.0',
    [shipped('Celerity.Collections', ['2.5.0', '2.6.0']), shipped('Celerity.Sorting', ['2.6.0'], true)], 2],

  ['stale by one release', '2.5.0',
    [shipped('Celerity.Collections', ['2.5.0', '2.6.0']), shipped('Celerity.Sorting', ['2.6.0'])], 1],

  ['bumped before the release was indexed', '2.7.0',
    [shipped('Celerity.Collections', ['2.6.0']), shipped('Celerity.Sorting', ['2.6.0'])], 1],

  ['a prerelease baseline', '2.6.1-beta.6',
    [shipped('Celerity.Collections', ['2.6.0']), shipped('Celerity.Sorting', ['2.6.0'])], 1],

  ['a malformed baseline', '2.6.0+.',
    [shipped('Celerity.Collections', ['2.6.0']), shipped('Celerity.Sorting', ['2.6.0'])], 1],

  ['the hatch left on after shipping', '2.6.0',
    [shipped('Celerity.Collections', ['2.6.0']), shipped('Celerity.Sorting', ['2.6.0'], true)], 1],

  ['gated but never published', '2.6.0',
    [shipped('Celerity.Collections', ['2.6.0']), shipped('Celerity.Statistics', [])], 2],

  // The eighth package, correctly hatched: out of the gate, so it neither empties the
  // intersection nor drags the baseline down.
  ['an eighth package on the hatch', '2.6.0',
    [shipped('Celerity.Collections', ['2.6.0']), shipped('Celerity.Statistics', [], true)], 0],

  // The release window: tagged but not yet indexed, so being one behind is correct.
  ['the release window', '2.6.0',
    [shipped('Celerity.Collections', ['2.6.0']), shipped('Celerity.Sorting', ['2.6.0'])], 0],

  // Sorting's 2.6.0 is the only version both have; the maximum on NuGet is not resolvable.
  ['the intersection, not the maximum', '2.6.0',
    [shipped('Celerity.Collections', ['2.5.0', '2.6.0']), shipped('Celerity.Sorting', ['2.6.0'])], 0],

  ['a first release, every package hatched', '2.6.0',
    [shipped('Celerity.Collections', [], true), shipped('Celerity.Sorting', [], true)], 0],
];

// The highest stable version that every *gated* package has published — the newest value
// the shared baseline can take and still resolve a PackageDownload for all of them.
//
// Packages on the escape hatch are excluded rather than counted as unpublished: they
// resolve no baseline at all, so letting one empty the intersection would pronounce the
// other packages' correct baseline wrong the moment an eighth package was added.
//
// Declared below its first use; function declarations hoist, and the self-test reads
// better next to the cases it pins.
function commonBaseline(packages) {
  const gated = packages.filter((p) => !p.noBaseline);
  if (gated.length === 0) return null;
  const candidates = gated[0].stable.filter((v) =>
    gated.every((p) => p.stable.some((other) => compareVersions(other, v) === 0))
  );
  return maxStable(candidates);
}

// ---- Checks -------------------------------------------------------------------------
// Pure: everything that decides pass or fail takes the baseline string and the resolved
// package list and returns the problems, with no filesystem, network or process.exit in
// reach. That is what lets the self-test drive every failure direction below — without it,
// the only path CI ever executed was today's all-equal happy path, and a regression in any
// of the failure branches would have left both steps green.

function analyze(baseline, packages, online) {
  const problems = [];

  // (1) A baseline that is not a stable version resolves to a preview package, so the
  // gate would compare the release surface against a nightly.
  if (parseVersion(baseline) === null) {
    problems.push(`the baseline in ${PROPS} is "${baseline}", which is not a version number.`);
  } else if (!isStable(baseline)) {
    problems.push(
      `the baseline in ${PROPS} is "${baseline}", a prerelease. The gate would validate ` +
      `every package against a preview build rather than against the last release.`
    );
  }

  if (!online) return problems;

  // Only the gated packages constrain the baseline. A package on the escape hatch resolves
  // no PackageDownload at all, so an unpublished newcomer must not be allowed to empty the
  // intersection and pronounce every other package's correct baseline wrong — which is the
  // shape of drift this check would itself have introduced.
  const gated = packages.filter((p) => !p.noBaseline);
  const expected = commonBaseline(gated);

  // (4) The escape hatch is only correct while a package has never shipped. Left in place
  // afterwards it does not narrow the gate, it removes the package from it.
  for (const p of packages) {
    if (p.noBaseline && p.stable.length > 0) {
      problems.push(
        `${p.project} still sets <CelerityNoPublishedBaseline>true</CelerityNoPublishedBaseline>, ` +
        `but ${p.id} has published ${maxStable(p.stable)}. That property is the first-release ` +
        `escape hatch: while it is set, src/Directory.Build.targets leaves ` +
        `PackageValidationBaselineVersion unset and the package is validated against nothing at ` +
        `all. Delete the property.`
      );
    }
    if (!p.noBaseline && p.stable.length === 0) {
      problems.push(
        `${p.id} has no published stable release, but ${p.project} does not set ` +
        `<CelerityNoPublishedBaseline>true</CelerityNoPublishedBaseline>. The baseline resolves ` +
        `to a PackageDownload that does not exist, so the next restore fails.`
      );
    }
  }

  // (3) The bump itself — but only once the baseline is a version this can rank. Comparing
  // a prerelease or a malformed string against a release compares release parts alone, so
  // `2.6.1-beta.6` would report as "ahead of 2.6.0" on top of the finding that already says
  // what is actually wrong with it. One clear problem beats two, one of which misleads.
  if (!isStable(baseline)) {
    return problems;
  }

  if (expected === null) {
    if (gated.length > 0) {
      problems.push(
        `no single version is published by all ${gated.length} gated package(s), so no shared ` +
        `baseline can resolve. Published: ${gated.map((p) => `${p.id}=${maxStable(p.stable) || 'none'}`).join(', ')}.`
      );
    }
  } else if (compareVersions(baseline, expected) < 0) {
    problems.push(
      `the baseline in ${PROPS} is ${normalize(baseline)}, but ${expected} is published and ` +
      `indexed on NuGet.org for all ${gated.length} gated packages. Every package is being ` +
      `validated against a superseded surface, so a break introduced in ${expected} packs ` +
      `green. Set <CelerityPackageValidationBaseline> to ${expected}.`
    );
  } else if (compareVersions(baseline, expected) > 0) {
    problems.push(
      `the baseline in ${PROPS} is ${normalize(baseline)}, but the highest version published ` +
      `by all ${gated.length} gated packages is ${expected}. The baseline becomes a ` +
      `PackageDownload, so pointing it at a version that is not indexed yet fails the next ` +
      `restore — including the release build's. Bump it in a follow-up commit *after* the ` +
      `release is published, not in the release commit itself.`
    );
  }

  return problems;
}

// The one place the process is allowed to end.
async function main() {
  if (process.argv.includes('--self-test')) {
    selfTest();
    return;
  }

  const baseline = readBaseline();
  const packages = discoverPackages();

  // (2) Nothing ships without being discoverable here.
  if (packages.length === 0) {
    fail(
      `found no shipped packages under ${SRC}/. A project ships when it does not set ` +
      `<IsPackable>false</IsPackable>; if that convention changed, update ${SELF} and ` +
      `src/Directory.Build.targets together.`
    );
  }

  if (process.argv.includes('--offline')) {
    report(analyze(baseline, packages, false), packages, baseline, false);
    return;
  }

  let resolved;
  try {
    resolved = await Promise.all(
      packages.map(async (p) => ({ ...p, stable: (await publishedVersions(p.id)).filter(isStable) }))
    );
  } catch (e) {
    // Reported, not failed: see the header. The offline findings still stand.
    console.warn(`warning: could not reach NuGet.org (${e.message}); skipped the published-version checks.`);
    report(analyze(baseline, packages, false), packages, baseline, false);
    return;
  }

  report(analyze(baseline, resolved, true), resolved, baseline, true);
}

function report(problems, packages, baseline, online) {
  if (problems.length > 0) {
    console.error(`Package-baseline check failed (${problems.length} problem(s)):\n`);
    for (const p of problems) console.error(`  - ${p}`);
    console.error(
      `\nThe baseline is what \`dotnet pack\` compares every package against, and the push to ` +
      `NuGet.org is irreversible. See src/Directory.Build.props and CONTRIBUTING.md, ` +
      `"Package validation".`
    );
    process.exit(1);
  }

  const gated = packages.filter((p) => !p.noBaseline).length;
  console.log(
    `Package baseline OK: ${gated}/${packages.length} shipped package(s) validate against ` +
    `${normalize(baseline)}` +
    (online
      ? `, the newest release published by all of them.`
      : ` (offline — published versions not checked).`)
  );
}

main().catch((e) => fail(e.stack || e.message));
