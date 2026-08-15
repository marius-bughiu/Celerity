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
// single shared property, so it resolves to a PackageDownload for all seven packages at
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
//      a hardcoded list, so an eighth package cannot join without joining this check too;
//   3. (network) the baseline equals the highest stable version published by all the gated
//      ones;
//   4. (network) no package sets CelerityNoPublishedBaseline while it *has* a published
//      stable release — the condition that removes it from the gate entirely.
//
// Usage:
//   node scripts/check_package_baseline.js              # offline checks + NuGet
//   node scripts/check_package_baseline.js --offline    # skip the network half
//   node scripts/check_package_baseline.js --self-test  # pin the version rules
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

function parseVersion(text) {
  const m = /^(\d+(?:\.\d+){0,3})(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$/.exec(String(text).trim());
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

function readBaseline() {
  let source;
  try {
    source = fs.readFileSync(PROPS, 'utf8');
  } catch (e) {
    fail(`could not read ${PROPS}: ${e.message}`);
  }
  const m = /<CelerityPackageValidationBaseline>\s*([^<\s]+)\s*<\/CelerityPackageValidationBaseline>/.exec(source);
  if (!m) {
    fail(
      `could not find <CelerityPackageValidationBaseline> in ${PROPS}. Either the property ` +
      `was renamed — in which case update ${SELF} and src/Directory.Build.targets together — ` +
      `or the binary-compatibility gate has lost its baseline entirely.`
    );
  }
  return m[1];
}

// The shipped set is derived, never listed. A hardcoded roster is the same failure this
// check exists to prevent: it would go stale the moment an eighth package is added, and
// the new package would be the one silently outside the gate.
//
// A project ships when it has a PackageId and has not opted out with IsPackable=false —
// which is exactly the condition Directory.Build.targets applies when it sets
// PackageValidationBaselineVersion.
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

      const source = fs.readFileSync(proj, 'utf8');
      if (/<IsPackable>\s*false\s*<\/IsPackable>/i.test(source)) continue;

      const id = /<PackageId>\s*([^<\s]+)\s*<\/PackageId>/.exec(source);
      if (!id) continue;

      projects.push({
        id: id[1],
        project: proj.split(path.sep).join('/'),
        noBaseline: /<CelerityNoPublishedBaseline>\s*true\s*<\/CelerityNoPublishedBaseline>/i.test(source),
      });
    }
  }
  return projects.sort((a, b) => a.id.localeCompare(b.id));
}

// ---- Asking NuGet -------------------------------------------------------------------
// The flat-container index is the cheapest published-versions endpoint there is: a static
// JSON blob off the CDN, no auth, no search index lag. A 404 is a real answer — it means
// the package id has never been published — so it is mapped to "no versions" rather than
// to an error. Anything else is a transport problem and stops the network half.

function getJson(url, attempt = 1) {
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
      response.on('data', (chunk) => { body += chunk; });
      response.on('end', () => {
        try {
          resolve(JSON.parse(body));
        } catch (e) {
          reject(new Error(`malformed JSON from ${url}: ${e.message}`));
        }
      });
    });
    request.on('timeout', () => request.destroy(new Error(`timed out after 15s: ${url}`)));
    request.on('error', reject);
  }).catch((e) => {
    // One retry, because the alternative to tolerating a blip is a red pull request that
    // says nothing about the repository.
    if (attempt >= 2) throw e;
    return new Promise((resolve) => setTimeout(resolve, 2000)).then(() => getJson(url, attempt + 1));
  });
}

async function publishedVersions(id) {
  const index = await getJson(`${FLAT_CONTAINER}/${id.toLowerCase()}/index.json`);
  return index && Array.isArray(index.versions) ? index.versions : [];
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

  if (failures.length > 0) {
    console.error('error: the version rules no longer hold.\n');
    console.error(failures.join('\n'));
    process.exit(1);
  }
  console.log(`ok: ${VERSION_CASES.length + STABILITY_CASES.length + 5} version case(s) pinned.`);
}

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

async function main() {
  if (process.argv.includes('--self-test')) {
    selfTest();
    return;
  }

  const baseline = readBaseline();
  const packages = discoverPackages();
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

  // (2) Nothing ships without being discoverable here.
  if (packages.length === 0) {
    fail(
      `found no shipped packages under ${SRC}/. A project ships when it declares <PackageId> ` +
      `and does not set <IsPackable>false</IsPackable>; if that convention changed, update ` +
      `${SELF} and src/Directory.Build.targets together.`
    );
  }

  if (process.argv.includes('--offline')) {
    report(problems, packages, baseline, null);
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
    report(problems, packages, baseline, null);
    return;
  }

  // Only the gated packages constrain the baseline. A package on the escape hatch resolves
  // no PackageDownload at all, so an unpublished newcomer must not be allowed to empty the
  // intersection and pronounce every other package's correct baseline wrong — which is the
  // shape of drift this check would itself have introduced.
  const gated = resolved.filter((p) => !p.noBaseline);
  const expected = commonBaseline(gated);

  // (4) The escape hatch is only correct while a package has never shipped. Left in place
  // afterwards it does not narrow the gate, it removes the package from it.
  for (const p of resolved) {
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

  // (3) The bump itself.
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

  report(problems, resolved, baseline, expected);
}

function report(problems, packages, baseline, expected) {
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
    (expected === null
      ? ` (offline — published versions not checked).`
      : `, the newest release published by all of them.`)
  );
}

main().catch((e) => fail(e.stack || e.message));
