# Contributing to Celerity

Thanks for your interest in contributing! Celerity is a small, focused library and we try to keep the contribution process light. Reading this whole file should take about five minutes.

## Getting the code

```bash
git clone https://github.com/marius-bughiu/Celerity.git
cd Celerity/src
dotnet restore
dotnet build
dotnet test
```

Requirements: .NET 8 SDK. Everything else is fetched via NuGet.

## Project layout

As of 2.0.0 the library is split into layered packages (`Celerity.Primitives` ← `Celerity.Hashing` ← `Celerity.Collections`, with `Celerity.Sorting` and `Celerity.Statistics` two further consumers of `Celerity.Primitives`); see the [migration guide](docs/migration.md#200--the-package-split). On top of that core sit three standalone **showcase** packages — `Celerity.Ring`, `Celerity.Sentinel` and `Celerity.Cardinality` — which depend on `Celerity.Collections` and carry their own test projects. Eight packages ship in total.

```
src/
├── Celerity/                 The Celerity.Collections package (assembly Celerity.dll).
│   ├── Collections/          CelerityDictionary, IntDictionary, ...
│   └── TypeForwarders.cs     [TypeForwardedTo] for every type moved to a lower package (binary back-compat).
├── Celerity.Hashing/         The Celerity.Hashing package. IHashProvider<T>, the hashers, the evaluators.
├── Celerity.Primitives/      The Celerity.Primitives package. FastUtils, struct PRNGs, VarInt, FastGuid.
├── Celerity.Sorting/         The Celerity.Sorting package. RadixSort, CountingSort, PartialSort.
├── Celerity.Statistics/      The Celerity.Statistics package. DDSketch, ReservoirSampler, RunningStatistics.
├── Celerity.Ring/            The Celerity.Ring package. Consistent-hash and rendezvous rings.
├── Celerity.Sentinel/        The Celerity.Sentinel package. Streaming abuse / heavy-hitter detection.
├── Celerity.Cardinality/     The Celerity.Cardinality package. Approximate COUNT(DISTINCT) and windowed dedup.
├── Celerity.Tests/           xUnit tests (behavioural, edge-case, and property-based). Mirrors the main project's layout.
├── Celerity.Ring.Tests/      The showcase packages' own xUnit suites, one per package.
├── Celerity.Sentinel.Tests/
├── Celerity.Cardinality.Tests/
├── Celerity.Benchmarks/      BenchmarkDotNet project. Runs in CI on every PR and main push.
├── Celerity.Fuzz/            Differential fuzz harness. Nightly soak; reproduces failures from a seed.
├── Celerity.AotSmokeTest/    Native AOT publish + run target. Proves AOT/trim compatibility.
└── Celerity.sln
```

## Making changes

1. Open (or comment on) a GitHub issue before starting a large change. Small bug fixes can skip this step. Browse open work via the [Issues](https://github.com/marius-bughiu/Celerity/issues) tab or by [milestone](https://github.com/marius-bughiu/Celerity/milestones).
2. Create a branch off `main`.
3. Write the change together with the test that would have caught the bug. Bug fixes without regression tests will be asked to add one.
4. Run `dotnet test` locally.
5. Open a PR. CI will run `dotnet build` and `dotnet test` on your branch automatically (`.github/workflows/ci.yml`).

## Coding conventions

Most of these are enforced by review rather than by an analyzer — the constant-naming rule is the exception, and has a CI check behind it. Reading the existing code is the fastest way to get a feel for the style.

- The packages multi-target `net8.0;net9.0;net10.0` (the shared list lives in [`src/Directory.Build.props`](src/Directory.Build.props); bump it there). `net8.0` is the lowest target, so shared code must not use net9/net10-only APIs unguarded — gate any newer-runtime path with `#if NET9_0_OR_GREATER` / `NET10_0_OR_GREATER` and keep a net8.0 fallback. Nullable reference types are enabled.
- File-scoped namespaces (`namespace Celerity.Hashing;`).
- `PascalCase` for public members, `_camelCase` for private fields.
- `PascalCase` for **every** `const` too — any accessibility, field or method-local. See [Constant naming](#constant-naming) below; it is checked in CI.
- Every public type and member has an XML doc comment. `GenerateDocumentationFile` is on and every shipping package promotes both **CS1591** (missing doc comment) and **CS1570** (badly formed XML in a doc comment) to build errors, so a doc comment must be present *and* parse. The second gate matters because the doc writer drops the whole member element rather than truncating it, so a stray unclosed tag ships a type with no documentation at all.
- Hash providers are structs that implement `IHashProvider<T>`. This is load-bearing: passing them as a generic constraint (`where THasher : struct, IHashProvider<T>`) lets the JIT devirtualize `hasher.Hash(...)` calls. Please do not change them to classes or interfaces.
- Prefer explicit types over `var` where it meaningfully helps readability (e.g. in tight numeric loops). Use `var` freely for obvious right-hand-sides.
- Avoid allocations on hot paths. If you add a new dependency or a LINQ call inside a probe loop, expect pushback.

## Tests

- Use xUnit.
- Name tests `Method_ShouldExpectedBehavior_WhenCondition`.
- Prefer `[Fact]` for a single case, `[Theory] + [InlineData]` for parameterized cases.
- When fixing a bug, add a test that fails on `main` and passes on your branch. It's fine to reference the issue number in a comment.
- New collections are expected to carry parity coverage at every layer: behavioural tests, a CsCheck property test against the closest BCL oracle, and a `Celerity.Fuzz` target. See the [Testing & coverage guide](docs/testing.md) for how each layer works and how to run them. Three points the rule leaves implicit:
  - **The property test and the fuzz target are not alternatives.** They share an oracle but not a job: the property test is bounded and runs on every pull request, shrinking a failure to a minimal reproduction; the fuzz target is unbounded and runs nightly, reaching sequences no per-PR budget can. A type with only the second one is un-gated on the pull request that breaks it.
  - **"The closest BCL oracle" may be a definition rather than a type.** The BCL ships no cache, no timer wheel and no rope, so `LfuCache` models against the definition of LFU, `TimerWheel` against a list of deadlines scanned linearly, and `Rope` against a plain `string` rebuilt after every edit. What matters is that the oracle shares no code with the type under test.
  - **Property tests live in two places — check both before concluding a type has none.** A type that is a member of an existing family gets a block in `Celerity.Tests/Properties/CollectionModelPropertyTests.cs`, next to its siblings; every structurally distinctive type gets its own `Celerity.Tests/Collections/<Type>DifferentialTests.cs`. Both run on every pull request. Looking only in `Properties/` is what made [#416](https://github.com/marius-bughiu/Celerity/issues/416) report six collections as uncovered when each had a differential suite.
- A new collection must also be added to the **cross-collection suites** that assert one rule across the whole family, not only to its own `*Tests.cs`. `grep` `src/Celerity.Tests/Collections/` for them before assuming you have found them all; `ClearNoOpVersionTests.cs` (a `Clear()` that removes nothing must not bump the version) is the one that applies to every count-based collection. A type that is absent from these suites is not covered for the invariants the rest of the family guarantees.
- Coverage is gated in CI (`.github/workflows/coverage.yml`) at **100% line and 100% branch**, across all eight shipping packages. New code arrives with its tests, or the gate goes red. If you hit a branch no test can reach, exclude it at the source with `[ExcludeFromCodeCoverage(Justification = "…")]` explaining why — do not lower the floor. See the [Testing & coverage guide](docs/testing.md) for the current exclusions and the reasoning behind each.
- Adding a new shipping package? Add its assembly to `src/coverage.runsettings` and its test project to the coverage workflow. Coverlet's assembly filter is exact-match, so an unlisted package is silently unmeasured.

## Benchmarks

Benchmarks live in `src/Celerity.Benchmarks` and cover every public collection (`CelerityDictionary`, `IntDictionary`, `LongDictionary`, `CeleritySet`, `IntSet`) against its `.NET` BCL counterpart. Each operation (Insert/Add, Lookup/Contains, Remove) is grouped via `[BenchmarkCategory]` with the BCL method marked `Baseline = true`, so BenchmarkDotNet's output table includes a `Ratio` column showing the speedup directly.

### Run locally

```bash
cd src/Celerity.Benchmarks
dotnet run -c Release                 # interactive switcher — pick which class to run
dotnet run -c Release -- --filter '*' # run everything with the default (slow, high-precision) job
```

### CI

[`.github/workflows/benchmarks.yml`](.github/workflows/benchmarks.yml) runs the CI-tracked core suite (the `CoreBenchmarks` array in `Program.cs`) at full BenchmarkDotNet accuracy, sharded across a parallel matrix; an aggregate job stitches the shard reports back together and [`benchmark-action/github-action-benchmark`](https://github.com/benchmark-action/github-action-benchmark) appends the result to the `gh-pages`-stored history powering the dashboard at <https://marius-bughiu.github.io/Celerity/dev/bench/>.

**It runs on merges to `main`, not on pull requests.** The suite is by a wide margin the most expensive thing in this repository's CI, and per-PR it was paid for again on every review commit — the head *and* a same-runner `main` base, twice over eight runners. What it protects is the published time series, which is a property of `main`. The trade is that a regression is seen on the merge commit that introduced it rather than on the PR that proposed it, so **a perf-motivated change is expected to carry local before/after numbers in its PR description**. Numbers without `-c Release` are not useful — BenchmarkDotNet refuses to run in Debug.

To measure a branch before it merges, run the workflow by hand: *Actions → Benchmarks → Run workflow*, against any ref. A dispatch on `main` publishes to the dashboard like a push does; on any other ref it measures and uploads its report as an artifact without touching the series.

Two more things about the run are worth knowing before you wonder why it did or did not happen:

- **It is skipped when the commit cannot move a number.** [`scripts/benchmark_relevant_changes.js`](scripts/benchmark_relevant_changes.js) gates the workflow: a diff that touches only documentation, only the test / fuzz / AOT-smoke projects, or only comments inside `.cs` files does not buy a three-hour eight-runner run, and simply contributes no point of its own to the series. The gate is one-directional — anything it cannot prove inert (an added or deleted file, a `.csproj`, a git command that fails) runs the suite — and a manual dispatch is never gated. Run it yourself with `node scripts/benchmark_relevant_changes.js <base> <head>`.
- **A move is evidence, not a verdict.** Hosted runners vary 20–50% run to run, and a case whose build lands in a different code or data layout shifts by tens of percent with a tight spread on both sides. Confirm a dashboard step change with a local Release run before acting on it.

### The dashboard

The dashboard reads BenchmarkDotNet result *names*, so a benchmark's naming is part of its contract with the site. A name that the page's parser does not recognise is dropped silently — the data publishes to `gh-pages` correctly and the card just renders blank, with nothing red anywhere.

Two rules keep a benchmark chartable:

- Methods are named `{TypeName}_{Op}` — `EnumSet_Contains`, `Dictionary_Lookup`. The type name decides whether the row is the Celerity arm or the BCL baseline (`BCL_TYPES` in the dashboard source), and `{Op}` is what the card is titled.
- A `[Params]` sweep property must be called **`ItemCount`**. A class may declare no sweep at all — `EnumMap` / `EnumSet` are bounded by the enum universe, so a synthetic item count would chart a dimension that does not exist — in which case the dashboard renders a single bucket. Any *other* property name is rejected rather than charted under an "items" label it does not mean.

Adding a collection to the site means updating three lists by hand, since the published data alone does not tell the page what to draw: the ship card in `web/index.html`, and the `COLLECTIONS` array in **both** `web/dev/bench/index.html` and `web/dev/bench/detail.html` (`items: [NO_SWEEP]` for an unparameterized class).

Write the `title` and `vs` in those arrays as plain text — `BTreeSet<int>`, not `BTreeSet&lt;int&gt;`. They are escaped at render time, so a pre-escaped label renders its entities literally. The flip side is that a label must never be concatenated into an `innerHTML` template raw: a generic parameter is then parsed as a start tag and vanishes from the heading. The check below fails CI on that.

[`scripts/check_dashboard_coverage.js`](scripts/check_dashboard_coverage.js) enforces all of this so the failure mode is a red check rather than a blank card. It lifts the `COLLECTIONS` tables and the name parsers out of the dashboard HTML rather than reimplementing them, so it validates the code that actually ships. Run it any time you touch a benchmark or the dashboard:

```bash
node scripts/check_dashboard_coverage.js                                   # structural checks
node scripts/check_dashboard_coverage.js path/to/joined-report-full.json   # + verify the data
```

The structural half — the two `COLLECTIONS` arrays agree, every charted collection has a `{Key}Benchmark` registered in `CoreBenchmarks`, and no label reaches an `innerHTML` template unescaped — runs on every PR in `ci.yml`. The full check runs in the aggregate job of `benchmarks.yml`, against the merged report, and additionally asserts that every published name parses and that every card resolves to both a BCL and a Celerity measurement.

## Documentation links

Anchor links inside the docs are checked in CI, because a broken one is invisible: the markdown is well-formed, the diff looks right, and the only symptom is that clicking the link scrolls nowhere.

The trap is that GitHub slugs a heading by lowercasing its **rendered** text and deleting punctuation *without substituting a separator*. Most of the API reference headings are generic type names, so this bites constantly:

| Heading | Anchor | Not |
|---|---|---|
| `## CeleritySet&lt;T, THasher&gt;` | `#celeritysett-thasher` | `#celerityset-t-thasher` |
| `## PooledCeleritySet<T, THasher>` | `#pooledceleritysett-thasher` | `#pooledcelerityset` |
| `## 6. Build-once, read-many → freeze it` | `#6-build-once-read-many--freeze-it` | `#6-build-once-read-many-freeze-it` |

The doubled `t` in the first two rows is `…Set` meeting `T` once the `<` between them is deleted — and it happens whether the generic is entity-encoded or written with bare angle brackets, since bare `<T, THasher>` is not a valid HTML tag and renders as text. The doubled dash in the third row is the arrow vanishing between two spaces. None of the three is what anyone writes by hand.

[`scripts/check_doc_anchors.js`](scripts/check_doc_anchors.js) resolves every same-file `](#fragment)`, every relative `](other.md#fragment)`, and every relative file target across all tracked markdown, and rejects links to any anchor that a repeated heading can renumber. Never *guess* a heading's anchor — ask the script:

```bash
node scripts/check_doc_anchors.js --list    # every anchor each file defines
node scripts/check_doc_anchors.js           # check every link
node scripts/check_doc_anchors.js --self-test
```

`--self-test` pins the slug rule itself against ids GitHub actually rendered, so a rewrite of the rule cannot quietly start inventing anchors. If you need to re-confirm the rule after a heading rename, ask GitHub directly:

```bash
gh api repos/marius-bughiu/Celerity/contents/docs/api/collections.md \
  -H "Accept: application/vnd.github.html" | grep -oE 'id="user-content-[a-z0-9-]*"'
```

### Never link to a repeated heading's generated anchor

GitHub tells repeated headings apart by numbering them: where `### Measured` occurs five times, the ids are `#measured`, `#measured-1` … `#measured-4`. **Linking to any of them is banned, and the script fails the build on it.** The anchor resolves — that is the whole problem. Each id names a *position*, not a heading, so inserting one more `### Measured` above renames every one of them downward and silently moves the links onto the wrong collection's table, in a diff that touches none of them. That is not hypothetical: all five `#measured-N` links in the API reference had drifted onto the wrong collection's benchmarks before the rule existed ([#409](https://github.com/marius-bughiu/Celerity/issues/409)).

Note that the ban covers the **unsuffixed** first occurrence too. `#measured` looks like the stable one and is not: it is first only until something is inserted above it.

It also covers an id that merely *looks* numbered. If any heading slugs to `#foo`, then `#foo-1` is on that heading's numbering line whoever holds it — so a `### Foo 1` that owns `#foo-1` today loses it the moment a second `### Foo` appears above it, and moves to `#foo-1-1`. Further out the same is true of `#foo-2`, which two more `### Foo` headings would reach. Nothing about `### Foo 1` gives that away, which is why the script checks it for you.

Only suffixes GitHub can actually generate count: the disambiguator starts at `-1` and never pads, so `#foo-0` and `#foo-01` are on no numbering line and are fine to link to.

Repeated headings are fine to *have* — `CHANGELOG.md` is built on them. If you need to link to one, this is the one place you *should* hand-write an anchor. Give the heading an id of its own and point at that:

```markdown
<a id="measured-timerwheel"></a>

### Measured
```

An id you wrote is stable because nothing counts it — but it has to be **unique**. An `<a id="measured-1">` rescues nothing: the page then carries that id twice, once on your anchor and once on the heading GitHub numbered, and the fragment resolves to whichever comes first in the document. The script rejects any id two elements answer to, whether the other claimant is a numbered heading, an ordinary one, or a second `<a id>`. `--list` marks every generated anchor `(positional — do not link)` so you can tell the two apart at a glance.

## Constant naming

Every `const` in the shipping packages is `PascalCase`, whatever its accessibility and whether it is a field or a method-local: `DefaultCapacity`, `MaxKicks`, `Ln2Squared`, `FnvPrime`. Not `DEFAULT_CAPACITY`, not `fnvPrime`. This is [dotnet/runtime's own rule](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md), and it replaces the `UPPER_CASE` this guide used to state — which the code had never actually followed. The split this produced was invisible enough that `XorFilter` carried both spellings inside one type, and that the same concept shipped as `HyperLogLog.DEFAULT_PRECISION` in one package and `Distinct.DefaultPrecision` in another.

Two allowances exist, both for constants transcribed from a published algorithm, so the code can be read against its reference:

- an acronym of at most two letters stays upper-case — `C1`, `K0`, `M`, `R`, and `IOStream` in the general case. Three or more are PascalCased the way the framework guidelines ask: `XmlParser`, not `XMLParser`;
- a trailing `_<digits>` index is kept — `Prime64_1`, `Prime32_3`.

[`scripts/check_constant_naming.js`](scripts/check_constant_naming.js) enforces this in the `constant-naming` CI job, over the eight shipping packages only. Test, benchmark, fuzz and AOT-smoke code is exempt: a throwaway `const int n = 5;` in a test tells a consumer nothing, and renaming a few hundred of them would bury the rule it was meant to serve.

```bash
node scripts/check_constant_naming.js             # check the shipping packages
node scripts/check_constant_naming.js --list      # every constant it can see
node scripts/check_constant_naming.js --self-test # pin the name rule and the scan
```

`--self-test` pins both halves — the name rule (including the two allowances) and the declaration scan that has to tell a real `const` from one inside a comment or a string literal — so a later rewrite cannot quietly stop finding violations. It also fails on a project under `src/` that the script cannot classify, which is how a newly added package gets covered instead of silently skipped.

## Versioning

Celerity uses [MinVer](https://github.com/adamralph/minver) to derive NuGet package versions exclusively from **git tags**. There is no `<Version>` or `<PackageVersion>` property in any `.csproj` file — the single source of truth is the `v`-prefixed annotated tag on the commit that represents a release.

### How it works

1. MinVer walks the git history from `HEAD` looking for the nearest tag matching `v{major}.{minor}.{patch}`.
2. If `HEAD` **is** the tagged commit, the package version is exactly `{major}.{minor}.{patch}` (e.g. tag `v1.0.1` → version `1.0.1`).
3. If `HEAD` is **ahead** of the latest tag, MinVer appends a pre-release suffix (e.g. `1.0.2-beta.1`). The default pre-release identifier is `beta`, configured via `<MinVerDefaultPreReleaseIdentifiers>` in `Celerity.csproj`.
4. The tag prefix `v` is configured via `<MinVerTagPrefix>v</MinVerTagPrefix>` in `Celerity.csproj`.

To check what version MinVer computes locally, run:

```bash
cd src
dotnet build /p:MinVerVerbosity=diagnostic 2>&1 | grep MinVer
```

To see the current released version:

```bash
git tag -l 'v*' --sort=-v:refname | head -1
```

### Important for coding agents

- **Never** add `<Version>`, `<PackageVersion>`, or `<AssemblyVersion>` to any `.csproj`. MinVer owns versioning.
- Pre-release builds (any commit after a tag) produce versions like `1.0.2-beta.1`. This is expected and correct.
- When preparing a release, update `CHANGELOG.md` first, then tag the merge commit.

### Changelog entries

`CHANGELOG.md` follows [Keep a Changelog](https://keepachangelog.com/); new entries go under `## [Unreleased]` in the matching `### Added` / `### Changed` / `### Fixed` subsection and are promoted into a versioned section at release time.

**Keep each entry short and user-facing — a few sentences at most.** State *what* observably changed and *why it matters to a caller*, not how it's implemented. Don't name private fields, list bit-shift/probe steps, or explain JIT/codegen internals — those belong in the PR description or code comments. One tight entry per change: if it needs a paragraph, put the paragraph in the PR body and leave a one-line pointer here. End the entry with `Closes #NNN` — a traceability convention here, since the issue is actually auto-closed by the PR description or commit message that references it, not by the changelog text. (GitHub treats `Closes`/`Fixes`/`Resolves` as equivalent for that; this repo standardizes on `Closes` so the changelog reads consistently.)

This is a release-safety rule, not only a style preference: the release workflow extracts the whole `## [X.Y.Z]` section verbatim as the GitHub Release body, and GitHub caps release bodies (~125k characters). A single release section full of paragraph-per-change entries can exceed that limit and fail the release — terse sections keep releases publishable. `CLAUDE.md` carries the same convention for coding agents.

### Cutting a release

Releases are automated. Pushing a `v`-prefixed tag fires `.github/workflows/release.yml`, which builds, packs, publishes to NuGet.org, and creates a matching GitHub Release with notes extracted from `CHANGELOG.md`.

```bash
# 1. Move the CHANGELOG [Unreleased] block to [X.Y.Z] (with today's date if you
#    want one — the workflow does not require a date).
# 2. Commit, merge to main, then tag the merge commit and push the tag.
git tag -a v1.2.0 -m "Release 1.2.0"
git push origin v1.2.0
```

The workflow extracts the `## [X.Y.Z]` section of `CHANGELOG.md` and uses it as the GitHub Release body. Two things can go wrong with that — no section exists for the tag's version, or the section exceeds GitHub's ~125k release-body cap — and both are checked in the `build` job, **before** anything is pushed to NuGet.org. A failure there means nothing shipped: fix `CHANGELOG.md` and re-tag. You can check a section before tagging:

```bash
./.github/scripts/extract-release-notes.sh 1.2.0
```

`workflow_dispatch` is still wired up as a manual fallback for ad-hoc re-publishes (e.g. if a NuGet push fails partway through), but the normal flow is tag-push.

### API compatibility

Nothing in the build compares the public surface against the last published release. There was a `PackageValidation` gate here that did, resolving a pinned baseline version from NuGet.org on every `pack`; it cost more than it caught. The baseline was a hand-bumped property that had to move in a follow-up commit after each release — never in the release commit, since an unpublished version fails restore — which needed its own CI job to check that the ritual had happened, which in turn reached the network on every PR. Three moving parts guarding a surface this small, on a project where a break is nearly always deliberate.

The replacement is judgement, recorded where it is already recorded: **a break goes in `CHANGELOG.md` and, if a caller has to do something about it, in [docs/migration.md](docs/migration.md)** — both source-breaking (a rename, a narrowed parameter) and binary-breaking (a removed member or type forward, which needs a recompile rather than a rebuild). Say which kind it is; that distinction is what a consumer reads the entry for. Semantic versioning still applies: a break belongs in a major release.

If you want the machine answer for a specific change, it is one command away without any of it being wired into CI — point [`Microsoft.DotNet.ApiCompat.Tool`](https://www.nuget.org/packages/Microsoft.DotNet.ApiCompat.Tool) at the two assemblies, or diff the generated `.xml` doc files, which list every public member.

## Scope

Celerity is narrowly scoped: specialized high-performance collections, hashers, non-comparison sorts, streaming summary statistics, and the minimal supporting utilities they need. The common thread is a workload where the BCL either has no counterpart or structurally cannot host one — not breadth for its own sake. We are unlikely to accept:

- General-purpose extension methods that aren't used by a collection in the library.
- Wrappers around BCL types that don't add a performance benefit backed by benchmarks.
- Features that require reflection on hot paths.
- Thread-safety primitives. Use `ConcurrentDictionary<,>` or external locking.

If you're unsure whether something fits, open an issue and ask — it's cheaper for both of us.
