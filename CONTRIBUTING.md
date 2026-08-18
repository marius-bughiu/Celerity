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
- New collections are expected to carry parity coverage at every layer: behavioural tests, a CsCheck property test against the closest BCL oracle, and a `Celerity.Fuzz` target. See the [Testing & coverage guide](docs/testing.md) for how each layer works and how to run them.
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

[`.github/workflows/benchmarks.yml`](.github/workflows/benchmarks.yml) runs the CI-tracked core suite (the `CoreBenchmarks` array in `Program.cs`) at full BenchmarkDotNet accuracy, sharded across a parallel matrix. On a PR each shard measures its slice of both the PR head and the `main` tip back-to-back on the same runner, so hardware variance cancels; an aggregate job stitches the shard reports back together.

Results are parsed by [`benchmark-action/github-action-benchmark`](https://github.com/benchmark-action/github-action-benchmark) and:

- **On a PR**: a comment is posted with the same-runner A/B comparison vs `main`. A row is flagged when it moves past ±10% *and* the gap exceeds **3σ** of the two measurements' combined standard deviation (added in quadrature); the flags are advisory, so a noisy row does not fail the job. The comment also publishes that run's **observed spread** — the p50, p90 and p95 of |Δ| across every paired row — so a flag can be read against the run it arrived in rather than against an assumed floor. On a typical PR that spread is almost all runner drift, but a change to a shared primitive moves many rows at once and raises those figures itself, so it is not a floor the PR cannot have caused. If any shard failed to report, the comment says so above the fold — a partial comparison is otherwise indistinguishable from a clean one.
- **On a push to `main`**: the new measurement is appended to the `gh-pages`-stored history powering the dashboard at <https://marius-bughiu.github.io/Celerity/dev/bench/>.

Three things about the run are worth knowing before you wonder why it did or did not happen:

- **It supersedes itself.** Pushing to a PR cancels that PR's in-flight benchmark run rather than stacking another eight-runner matrix behind it; only the newest numbers are ever read. Pushes to `main` are keyed per commit instead, so none is ever cancelled and the published history has no gaps.
- **It is skipped when the diff cannot move a number.** [`scripts/benchmark_relevant_changes.js`](scripts/benchmark_relevant_changes.js) gates the PR path: a diff that touches only documentation, only the test / fuzz / AOT-smoke projects, or only comments inside `.cs` files does not buy a three-hour A/B run. The gate is one-directional — anything it cannot prove inert (an added or deleted file, a `.csproj`, a git command that fails) runs the suite — and it never applies to `main`, so a wrongly-skipped PR is still measured on merge. Run it yourself with `node scripts/benchmark_relevant_changes.js <base> <head>`.
- **Shard *i* means the same slice on both sides.** The base run replays the class list the head resolved instead of packing its own. Shard membership comes from bin-packing over the benchmark class list, so a PR that *adds* a benchmark class would otherwise pack the two sides differently and could pair a light head slice with a heavy base one.
- **A flag is evidence, not a verdict.** Even at 3σ, two rows in a ~750-row run still flagged on a diff whose IL was byte-identical to `main`: a case whose two builds land in different code or data layouts shifts by tens of percent with a tight spread on both sides, and nothing inside a single A/B pass separates that from a real change. Re-run before acting on a flag near the run's published p95. The rule lives in [`scripts/benchmark_comment.js`](scripts/benchmark_comment.js), which documents how the 3σ bar was calibrated; `node scripts/benchmark_comment.js --self-test` pins it against the measurements it was chosen against.

If a change is motivated by performance, include before/after numbers from a local Release run in the PR description — the CI job is a guardrail, not a precision instrument. Numbers without `-c Release` are not useful — BenchmarkDotNet refuses to run in Debug.

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

[`scripts/check_doc_anchors.js`](scripts/check_doc_anchors.js) resolves every same-file `](#fragment)`, every relative `](other.md#fragment)`, and every relative file target across all tracked markdown. Don't hand-write an anchor — ask the script:

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

## Constant naming

Every `const` in the shipping packages is `PascalCase`, whatever its accessibility and whether it is a field or a method-local: `DefaultCapacity`, `MaxKicks`, `Ln2Squared`, `FnvPrime`. Not `DEFAULT_CAPACITY`, not `fnvPrime`. This is [dotnet/runtime's own rule](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md), and it replaces the `UPPER_CASE` this guide used to state — which the code had never actually followed. The split this produced was invisible enough that `XorFilter` carried both spellings inside one type, and that the same concept shipped as `HyperLogLog.DEFAULT_PRECISION` in one package and `Distinct.DefaultPrecision` in another.

Two allowances exist, both for constants transcribed from a published algorithm, so the code can be read against its reference:

- a name of at most two characters may be all upper-case — `C1`, `K0`, `M`, `R`;
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

# 3. Once the packages are published and indexed on NuGet.org, bump
#    <CelerityPackageValidationBaseline> in src/Directory.Build.props to X.Y.Z
#    in a follow-up commit. See "Package validation" below.
```

The workflow extracts the `## [X.Y.Z]` section of `CHANGELOG.md` and uses it as the GitHub Release body. Two things can go wrong with that — no section exists for the tag's version, or the section exceeds GitHub's ~125k release-body cap — and both are checked in the `build` job, **before** anything is pushed to NuGet.org. A failure there means nothing shipped: fix `CHANGELOG.md` and re-tag. You can check a section before tagging:

```bash
./.github/scripts/extract-release-notes.sh 1.2.0
```

`workflow_dispatch` is still wired up as a manual fallback for ad-hoc re-publishes (e.g. if a NuGet push fails partway through), but the normal flow is tag-push.

### Package validation

Every `dotnet pack` validates each package against its last published version and **fails the build on any breaking API change**, across all three TFMs. Since the NuGet push is irreversible, this guard has to run before it — so it runs on every release build, and locally whenever you pack.

The baseline is one property, `<CelerityPackageValidationBaseline>` in `src/Directory.Build.props`, shared by every package that has shipped at least once — all seven today, now that `Celerity.Sorting` has had its first release and come off the escape hatch below. **Bump it to X.Y.Z in a follow-up commit, once vX.Y.Z is published and indexed on NuGet.org** — not in the release commit itself, because the value becomes a `PackageDownload` and a version that is not published yet fails the release build's restore.

This is the part that rots: a stale baseline keeps validating against an older surface, so a break introduced after it slips through — and it did, for the whole v2.6.0 cycle. Since [#364](https://github.com/marius-bughiu/Celerity/issues/364) the `package-baseline` CI job checks it on every PR, comparing the property against what NuGet.org has actually published rather than against the newest tag, so the bump comes due exactly when the release is indexed. You can run it yourself:

```bash
node scripts/check_package_baseline.js
```

If it fails, the message names the value to set. See [docs/testing.md](docs/testing.md#the-baseline-guard).

Two situations need a deliberate decision rather than a workaround:

- **An intentional break.** Run `dotnet pack -p:ApiCompatGenerateSuppressionFile=true` on the offending project, which writes a `CompatibilitySuppressions.xml` next to its `.csproj`. Commit it with a comment explaining each entry, so the break is reviewed in the PR instead of discovered by a consumer.
- **A package's first release.** There is no published predecessor to validate against, and asking for one fails the restore. Set `<CelerityNoPublishedBaseline>true</CelerityNoPublishedBaseline>` in that package's `.csproj`, ship it once, then delete the property.

Both gates, plus the package-metadata check, also run on every PR as the `release-gates` job — see [docs/testing.md](docs/testing.md#release-gates).

## Scope

Celerity is narrowly scoped: specialized high-performance collections, hashers, non-comparison sorts, streaming summary statistics, and the minimal supporting utilities they need. The common thread is a workload where the BCL either has no counterpart or structurally cannot host one — not breadth for its own sake. We are unlikely to accept:

- General-purpose extension methods that aren't used by a collection in the library.
- Wrappers around BCL types that don't add a performance benefit backed by benchmarks.
- Features that require reflection on hot paths.
- Thread-safety primitives. Use `ConcurrentDictionary<,>` or external locking.

If you're unsure whether something fits, open an issue and ask — it's cheaper for both of us.
