# Testing & Coverage

Celerity's first guiding principle is *correctness first* — "a fast collection that returns wrong answers is worthless." This document describes how that principle is enforced: the layers of tests, the property-based and fuzz harnesses that hunt for the bugs example-based tests miss, how code coverage is measured and gated, and how to run each layer locally.

## TL;DR

| Layer | Project / file | What it proves | Run locally |
|---|---|---|---|
| Behavioural unit tests | `Celerity.Tests` | Each public method does the right thing on hand-picked inputs, including collisions, resizes, and the out-of-band default/zero/null key. | `dotnet test` |
| Edge-case coverage | alongside each type's tests (`*Tests.cs`, `*EnumerationTests.cs`, `*CollisionTests.cs`) | The corners example tests skip: non-generic `IEnumerable`/`IEnumerator` paths, `Reset()`, indexer misses, `Clear()` on empty, wrap-around backward-shift. | `dotnet test` |
| Property-based tests (CsCheck) | **two homes** — `Celerity.Tests/Properties/CollectionModelPropertyTests.cs` and most of `Celerity.Tests/Collections/*DifferentialTests.cs` | Over CsCheck-generated operation sequences or inputs, a collection stays observably equal to its oracle; a failure shrinks and replays from a seed. | `dotnet test` |
| Accuracy / guarantee tests | `Celerity.Tests/Collections/*AccuracyTests.cs`, plus the differential suites that drive a bare `Random` | The same oracle idea without CsCheck: fixed or seeded datasets, checking that an approximate type stays inside the guarantee it advertises. Randomized, but not generated — so no shrinking, and these do **not** satisfy the parity rule's property-test requirement. | `dotnet test` |
| Differential fuzzer | `Celerity.Fuzz` | A long random walk finds no divergence from the BCL; failures replay deterministically from a seed. | `dotnet run -c Release` |
| Native AOT smoke test | `Celerity.AotSmokeTest` | Every collection/hasher works in a trimmed, AOT-compiled native binary. | see [aot.md](aot.md) |
| Release gates | `.github/scripts/`, the `release-gates` CI job | The pre-publish guards hold: every package packs with its symbols and metadata intact, and a missing or over-cap `CHANGELOG` section fails before anything reaches NuGet.org. | `dotnet pack -c Release`; `./.github/scripts/test-extract-release-notes.sh` |

All of these run in CI. Coverage is measured on all eight shipping assemblies and gated at 100% line and branch; the rendered report is published to [the coverage dashboard](https://marius-bughiu.github.io/Celerity/coverage/).

## Philosophy: example tests, then adversarial tests

Example-based unit tests are necessary but not sufficient for a data-structure library. They prove the cases the author *thought of*. The bugs that actually ship in open-addressed hash tables live in the cases nobody enumerated: a particular interleaving of inserts and deletes that leaves a tombstone in the wrong slot, a resize triggered mid-probe-chain, a backward-shift that wraps the table boundary and orphans a colliding key.

Celerity attacks those with two adversarial layers that don't rely on the author's imagination:

- **Property-based testing** generates a randomized input — an operation sequence for a mutable type, or the construction input for a build-once one — and checks an *invariant*, equivalence to a known-correct oracle, rather than a fixed expected output.
- **Differential fuzzing** runs the same idea as an unbounded soak: keep generating sequences until something diverges, and when it does, hand back a seed that reproduces it.

The oracle *is* the specification, but what it asserts depends on what the type promises:

- **Exact types** — the hash family, the ordered containers, `Rope`, the text indexes — must match the oracle observably, and any difference is a bug. Where the BCL ships a counterpart it is the oracle (`Dictionary<,>`, `HashSet<>`, `SortedSet<T>` for `RankedSet`, `StringBuilder` for `Rope`); where it does not, the oracle is the definition or the naive loop the type replaces (LFU eviction order for `LfuCache`, a linearly-scanned list of deadlines for `TimerWheel`, every pattern tested at every position for `AhoCorasick`).
- **Approximate types** are *supposed* to differ from the exact answer, so the oracle checks the guarantee instead of equality — and the guarantees are not all of one kind:
  - **Deterministic bounds**, which hold for every input and can be asserted outright: `BloomFilter` and `XorFilter` never return a false negative, and `CuckooFilter` does not either **provided you only remove elements you added** — deleting one that was never inserted can clear a fingerprint belonging to a different element, which is why its differential model records only the copies it successfully stored; `CountMinSketch` never underestimates; `TopKSketch` implements Space-Saving, whose `[count − error, count]` interval, no-underestimate property and heavy-hitter-never-missed theorem hold regardless of eviction tie-breaking, and which is exact while capacity is not exceeded.
  - **A statistical bound**, which needs a tolerance: `HyperLogLog`'s standard error is a dispersion rather than a per-estimate ceiling, so its suite allows three standard errors plus a small absolute slack for the small-range regime.

  Asserting equality for any of these would be asserting the wrong thing; so would asserting a statistical tolerance where a deterministic theorem applies.

What matters in both cases is that the oracle shares no code with the type under test.

## Behavioural unit tests

The bulk of the suite lives in `Celerity.Tests`, mirroring the library's folder layout. Test names follow `Method_ShouldExpectedBehavior_WhenCondition`. Notable categories:

- **Collision tests** (`*CollisionTests.cs`) — force every key down one probe chain with a constant hasher, then verify lookups, removals, and backward-shift deletion keep every entry findable.
- **Enumeration tests** (`*EnumerationTests.cs`) — the struct enumerators, `Keys`/`Values` views, mid-enumeration mutation detection, and the non-generic interface surface (`IEnumerable.GetEnumerator()`, `object IEnumerator.Current`, `IEnumerator.Reset()`).
- **Load-factor / constructor validation** — boundary resizes and argument checking.
- **Family-wide invariant suites** — a single file asserting one rule once per collection, so a new type (or an edit to an existing one) cannot quietly drift out of the family. `ClearNoOpVersionTests.cs` is the model: it pins *a `Clear()` that removes nothing does not bump the version*, so a defensive clear leaves active enumerators valid, across every count-based collection — and pins the three deliberate exceptions (`BitSet`, `FenwickTree` and `SegmentTree` are fixed-length, so establishing "already empty" costs the same scan as the clear) so they read as decisions rather than as oversights. `Deque<T>` shipped as the one outlier precisely because this rule was only pinned per-collection beforehand.
- **Edge cases** live next to the type they exercise rather than in a catch-all file: indexer misses on the out-of-band key and `Clear()` on an empty collection sit in `*Tests.cs`; the wrap-around cluster that exercises the `bypassesGap` branch of backward-shift deletion sits in `*CollisionTests.cs`.

Run them with:

```bash
dotnet test
```

## Property-based tests (CsCheck)

[CsCheck](https://github.com/AnthonyLloyd/CsCheck) makes parity the explicit contract: a test generates a randomized input, drives the Celerity type and an oracle from it, and asserts the relationship the type promises — observable equality for an exact type, the advertised guarantee or error bound for an approximate one (see [above](#philosophy-example-tests-then-adversarial-tests)). All of it runs on **every pull request** — `ci.yml` runs the test project unfiltered.

**These tests live in two places, and you need to look in both.** This guide previously named only the first, which led a reviewer to conclude that six collections had no property coverage when each in fact had a suite in the second ([#416](https://github.com/marius-bughiu/Celerity/issues/416)).

- **`Celerity.Tests/Properties/CollectionModelPropertyTests.cs`** — the cross-family model suite. One file holding a property per type for the collections that share a shape and an oracle: the hash-table dictionaries and sets, the multi-map and multi-set, the frozen pair, `BloomFilter`, `CuckooFilter`, `CountMinSketch`, `HyperLogLog` and `BitSet`. It is not the whole of either family — `SmallSet`, `XorFilter` and `TopKSketch` are covered elsewhere or not at all; see the table below for how to check a given type. Grouping them together is what makes the *family* comparable — every dictionary is driven by the same generator against the same oracle shape, so a divergence in one is read against its siblings. Each test samples independently; it is the generator that is shared, not one sequence. `LfuCache` also lives here, as the exception: its storage is a hash table, but eviction order is its whole contract, so it shares neither the oracle (the definition of LFU, not a BCL type) nor the property that order matters only through resize and deletion.
- **`Celerity.Tests/Collections/*DifferentialTests.cs`** — the differential suites, and where every structurally distinctive type lives. Usually one file per collection, named `<Type>DifferentialTests.cs`, but **not always**: `SetAlgebraDifferentialTests` carries `SmallSet` alongside seven hash-backed sets, and `EnumSetAlgebraDifferentialTests` carries `EnumSet`, which cannot join the `int`-parameterized battery. Types with a file of their own include: `RankedSet`, `Rope`, `TimerWheel`, `CompressedGraph`, `SuffixArray`, `AhoCorasick`, the B-trees, the spatial indexes, `CompressedIntSet`, `RankSelectBitVector`. These oracles are type-specific — `SortedSet` plus its enumeration order for `RankedSet`'s ranks, `StringBuilder` for `Rope`, a naive scan for the text indexes — so they do not generalize into the shared file. They are randomized, but not all in the same way, and that shows up when one fails: see [what a failure gives you](#what-a-failure-gives-you) below.

**How to tell what covers a given type — and why you should check rather than infer.** Oracle coverage lives under at least four naming conventions, and no one of them is the index. Not all of it is *property-based*: the third column says which conventions are CsCheck-generated, and only those satisfy the parity rule in `CONTRIBUTING.md`.

| Convention | Example | Property test? | What it is |
|---|---|---|---|
| `Properties/CollectionModelPropertyTests.cs` | `CelerityDictionary_ShouldMatch_BclDictionary` | **Yes** | The cross-family model suite above. |
| `Collections/<Type>DifferentialTests.cs` | `RankedSetDifferentialTests` | **Mostly** — see the table below; some drive a bare `Random` | The per-type suites, the largest group. |
| `Collections/<Operation>DifferentialTests.cs` | `SetAlgebraDifferentialTests` (carries `SmallSet`), `EnumSetAlgebraDifferentialTests` | Seeded, not CsCheck | Named for the *operation family*, so a search for `SmallSetDifferentialTests` finds nothing. |
| `Collections/<Type>AccuracyTests.cs` | `TopKSketchAccuracyTests`, `CountMinSketchAccuracyTests`, `HyperLogLogAccuracyTests` | **No** — ordinary xUnit `[Fact]`/`[Theory]` cases; they build data from hard-coded seeds or fixed datasets, but nothing is CsCheck-generated, so nothing shrinks | Approximate types checked against an exact model for the guarantee each advertises, deterministic for `TopKSketch` and `CountMinSketch` and statistical for `HyperLogLog`. `TopKSketch` and `CountMinSketch` build their streams from hard-coded `Random` seeds; `HyperLogLog` uses fixed sequential and string datasets sized to its error bound. Oracle coverage, but **not** the CsCheck property the parity rule asks for. |

Plus `src/Celerity.Fuzz/Differential.cs`, whose `Differential.All` roster is the nightly layer.

So: **`grep -rl "\bYourType\b" src/Celerity.Tests/` and check `Differential.All`.** Do not conclude a type is uncovered because a file you expected does not exist — that inference is what produced [#416](https://github.com/marius-bughiu/Celerity/issues/416), and it went on to produce four wrong gap lists in the process of fixing it. Known gaps are tracked in [#418](https://github.com/marius-bughiu/Celerity/issues/418) rather than listed here, so there is one place to correct when they close.

**Where a new property test goes:** into `Collections/<Type>DifferentialTests.cs` unless your type is a member of an existing family in the model suite, in which case add a block there next to its siblings.

The input takes one of two shapes:

- **Mutable types** generate a list of operations (`Set` / `Remove` / `TryAdd` / `Clear` for dictionaries; insert / remove / split / join for `Rope`; schedule / cancel / advance for `TimerWheel`) and apply the **identical** sequence to both sides, checking each operation's own result as it goes and reconciling the full observable state — `Count`, lookups across the whole domain, enumeration — at the end.
- **Build-once types** — `CompressedGraph`, `SuffixArray`, `AhoCorasick` — have no operation sequence. They generate the *input* instead (an edge list, a text, a pattern set), construct the type from it, and reconcile every query against the naive answer computed from the same input.

The generated domains are deliberately narrow — small key ranges that include `0` and negatives, alphabets of two to six letters, wheels of two slots — so that the interesting cases fire densely rather than rarely: collisions, resizes, the special zero/default/null-key slot and backward-shift deletion for the hash family; a suffix that parts late, a pattern nested inside another, a cascade between wheel levels or an edit landing on a leaf boundary for the rest.

### What a failure gives you

Five patterns are in use, and they differ in exactly the way that matters when one goes red — whether you get a reduced counterexample, a replayable seed, or neither. Check which kind you are looking at before hunting for a seed that may not exist.

| Pattern | Suites | On failure |
|---|---|---|
| **Directly generated** — CsCheck generates the operation list or the input itself | `CollectionModelPropertyTests`, `RankedSet`, `BTreeDictionary`, `BTreeSet` | CsCheck **shrinks** to a minimal failing sequence and prints a seed. The best case: the counterexample is usually small enough to read directly. |
| **Seed-driven, with a generated size** — CsCheck generates a `uint` *and* the dimensions, and `new Random(seed)` fills them in | `CompressedGraph` (vertex and edge counts), `SuffixArray` (text length, alphabet), `AhoCorasick` (pattern count and length, text length, alphabet), `KdTree`, `RTree`, `SpatialGrid`, `IntervalTree`, `CompressedIntSet`, `RankSelectBitVector` | The seed replays exactly, and the **size axes shrink** — CsCheck will find the smallest text or point count that still fails. What it cannot reduce is the *content* at that size, since a neighbouring seed is an unrelated draw. Usually enough to make a case readable. |
| **Seed-driven, fixed length** — only the geometry is generated; the trace length is a constant in the test body | `Rope` (chunk size; 250 operations), `TimerWheel` (slots and levels; 400 steps) | The seed replays exactly and the geometry shrinks, but **the trace does not get shorter**. Expect to read the whole run. |
| **Seed as a theory argument** — no CsCheck; `[Theory]` + `[InlineData(seed)]` | `Trie`, `FenwickTree`, `SegmentTree`, `SparseSet`, `StringInternTable`, and the two algebra suites (`SetAlgebra`, `EnumSetAlgebra`) | xUnit names the failing seed in the test case, so you know which run broke and rerun just that one. Still no reduction. |
| **Seed looped inside** — `[Theory]` over some other parameter, seeds iterated in the body | `Deque`, `LruCache`, `LfuCache`, `DisjointSet`, `IndexedPriorityQueue`, `SketchMerge` | The failing *parameter* is named (a starting capacity), the failing **seed is not**. Add a print or a breakpoint to find it. |

For the first three, replay by setting the seed and filtering to the class the failure names:

```bash
# PowerShell
$env:CsCheck_Seed = '0000LASTpRINTED'; dotnet test --filter RankedSetDifferentialTests
```

```bash
# bash
CsCheck_Seed='0000LASTpRINTED' dotnet test --filter RankedSetDifferentialTests
```

What separates these is how much of the case CsCheck is handed *as data*. Hand it the operations and it reduces them to a minimal counterexample. Hand it the dimensions plus a seed and it reduces the dimensions but not the content, because a neighbouring seed is an unrelated draw. Hand it only a seed and a fixed loop count and it can reduce nothing about the trace. Directly generated is the better shape where it is practical, since a reduced counterexample beats a long reproducible one — but a generated size axis recovers most of the benefit for a fraction of the effort, which is why nine of the eleven seed-driven suites have one.

## Differential fuzzing (`Celerity.Fuzz`)

The property tests are bounded — a fixed number of sequences per CI run. The fuzzer is the unbounded soak counterpart: it keeps generating cases until you stop it or it finds a divergence. It lives in `src/Celerity.Fuzz` and shares the same differential idea (drive Celerity and a BCL oracle in lock-step, fail on the first observable difference).

Every case is a pure function of a single 32-bit seed, so a failure is perfectly reproducible. Run it locally:

```bash
cd src/Celerity.Fuzz

# 100k random cases across all collections
dotnet run -c Release -- --iterations 100000

# soak for 60 seconds
dotnet run -c Release -- --time 60

# focus one collection
dotnet run -c Release -- --target CelerityMultiMap --iterations 200000

# list the targets
dotnet run -c Release -- --list
```

On a failure it prints the target, the `caseSeed`, and a ready-to-paste replay command:

```
================ FUZZ FAILURE ================
target   : CelerityDictionary
caseSeed : 1734023
replay   : dotnet run -c Release -- --seed 1734023 --iterations 1
detail   : DivergenceException: value[3] 70 != 71
==============================================
```

Reproduce it with exactly that command. (Note: `--target` changes the RNG stream, so when replaying a reported `caseSeed`, omit `--target` — the seed already determines which collection ran.)

In CI the fuzzer runs as a **nightly** job (`.github/workflows/fuzz.yml`) with a wall-clock budget, plus on-demand via *workflow_dispatch* (where you can pass a `seed`, `time`, or `target`). It is intentionally **not** a per-PR gate — a soak job belongs on a schedule, while the bounded property tests cover the per-PR signal.

### Adding a fuzz target

Add an entry to `Differential.All` in `src/Celerity.Fuzz/Differential.cs` and write a method that drives your collection against a BCL oracle, calling `Check(condition, message)` on every observable. The driver discovers it automatically (including in `--list`).

## Release gates

Publishing to NuGet.org is irreversible, so the checks that decide whether a release is well-formed all run *before* the push, in the `build` job of `.github/workflows/release.yml` ([#315](https://github.com/marius-bughiu/Celerity/issues/315)). Two of them:

| Gate | Fails on | Where |
|---|---|---|
| Package metadata | A missing symbol package, license, README, icon, or SourceLink stamp; a missing or unexpected package id. | [`validate-packages.ps1`](../.github/scripts/validate-packages.ps1) |
| Release notes | No `## [X.Y.Z]` section for the tag, or one at/over the 120,000-byte guard for GitHub's ~125k release-body cap. | [`extract-release-notes.sh`](../.github/scripts/extract-release-notes.sh) |

Both also run on **every PR**, as the `release-gates` job in `ci.yml`. Nothing else in CI packs, so without that job a mis-packed package would only surface in the nightly preview or in the release itself — long after review, and for the release, too late.

There is deliberately **no API-compatibility gate**. A `PackageValidation` baseline used to fail `pack` on any breaking public-API change; it was removed because its hand-bumped baseline needed a CI job of its own to keep it honest, and the whole apparatus cost more than it caught on a surface this size. Breaks are now tracked by hand in `CHANGELOG.md` and [migration.md](migration.md) — see [CONTRIBUTING.md](../CONTRIBUTING.md#api-compatibility) for what that asks of a change.

The release-notes gate is the one piece that is pure shell and therefore outside `dotnet test`. [`test-extract-release-notes.sh`](../.github/scripts/test-extract-release-notes.sh) covers it — happy path, section boundaries, missing section, oversized section, and that a failed run leaves no partial file for a later step to publish.

```bash
./.github/scripts/test-extract-release-notes.sh

# preview the notes for a version before tagging
./.github/scripts/extract-release-notes.sh 2.4.0
```

## Script guards

Four checks are plain Node scripts rather than xUnit tests, because what they inspect is not compiled: markdown links, source-file naming, the dashboard's collection roster, and which paths make a benchmark run worth spending. Each is its own `ci.yml` job.

| Guard | Fails on | Job |
|---|---|---|
| [`check_doc_anchors.js`](../scripts/check_doc_anchors.js) | A markdown link to a missing file, a missing anchor, or an anchor no heading can keep — see below. | `doc-anchors` |
| [`check_constant_naming.js`](../scripts/check_constant_naming.js) | A `const` in a shipping package that is not `PascalCase`. | `constant-naming` |
| [`check_dashboard_coverage.js`](../scripts/check_dashboard_coverage.js) | A benchmarked collection the dashboard does not list, so its data publishes to a card that never renders. | `dashboard-coverage` |
| [`benchmark_relevant_changes.js`](../scripts/benchmark_relevant_changes.js) | Nothing — it decides whether a commit can move a measured number, so the benchmark run can be skipped when it cannot. | `benchmark-gate` |

**Three of the four have a `--self-test` mode, and CI runs it as a separate step from the check itself.** The distinction is the point: the check tells you whether the repository is currently well-formed, and `--self-test` tells you whether the check still knows what well-formed means. A guard whose own rule has silently drifted passes everything, which is the failure mode that costs the most to notice.

```bash
node scripts/check_doc_anchors.js --self-test   # is the rule still right?
node scripts/check_doc_anchors.js               # is the repository still right?
node scripts/check_doc_anchors.js --list        # every anchor each file defines
```

### Anchors that resolve to the wrong place

`check_doc_anchors.js` resolves every same-file `](#fragment)`, every relative `](other.md#fragment)`, and every relative file target across all tracked markdown. It also rejects a link to an anchor that resolves *today* and will not stay put — a heading's id is only stable if nothing can renumber it out from under the link:

- when a heading text **repeats**, GitHub numbers the repeats (`#measured`, `#measured-1`, …) and every one of those ids is a position, the unsuffixed first included;
- when an id has the shape `#base-<n>` and some heading in the file slugs to `#base`, it sits on that heading's numbering line — enough further `#base` repeats get numbered onto it and take it, even though it was never generated from it. Only suffixes the disambiguator can produce count: it starts at `-1` and never pads, so `#foo-0` and `#foo-01` are on no numbering line;
- when two elements answer to the same id — a heading and a hand-written `<a id>`, or two anchors — GitHub emits it on both and the link resolves to whichever the document reaches first.

Both are silent: the markdown is well-formed, the link resolves, and the only symptom is landing in the wrong section. All five `#measured-N` links in the API reference had drifted onto the wrong collection's benchmark table this way, with CI green throughout ([#409](https://github.com/marius-bughiu/Celerity/issues/409)). Link to a unique hand-written `<a id>` instead; [CONTRIBUTING.md](../CONTRIBUTING.md#never-link-to-a-repeated-headings-generated-anchor) has the recipe.

The rule is pinned two ways: slug cases against ids GitHub actually rendered, and fixtures that run whole documents through the real parser and checker so a rejection path cannot be deleted without `--self-test` noticing.

## Code coverage

Coverage is collected with [coverlet](https://github.com/coverlet-coverage/coverlet) and scoped to all eight shipping assemblies — `Celerity`, `Celerity.Hashing`, `Celerity.Primitives`, `Celerity.Sorting`, `Celerity.Statistics`, `Celerity.Ring`, `Celerity.Sentinel`, `Celerity.Cardinality` — via [`src/coverage.runsettings`](../src/coverage.runsettings). The test, benchmark, fuzz, and AOT-smoke assemblies are tooling, not the subject under measurement.

Four test projects contribute: `Celerity.Tests` for the five core packages, plus `Celerity.Ring.Tests` / `Celerity.Sentinel.Tests` / `Celerity.Cardinality.Tests` for the showcase tier. Their Cobertura reports are merged on (source file, line number), so a line covered by any run counts as covered — which matters because the showcase projects also exercise `Celerity.Collections` transitively.

The suite covers **100% of lines and 100% of branches** across all eight. A small number of guards are excluded at the source with `[ExcludeFromCodeCoverage(Justification = "…")]`, and only where no test could ever reach them:

| Guard | Why no test can reach it |
|---|---|
| `Deque.ClampToArrayMaxLength` | Needs a backing array above 2³⁰ elements. Pinned by a real `[MemoryIntensiveFact(3100)]` test in `DequeGrowthTests`, which allocates ~3 GiB and skips on memory-capped runners — excluded so the gate does not depend on whether the runner had the headroom. |
| `IndexedPriorityQueue.ClampGrowth` | Needs 2³⁰ *live* entries with distinct elements, pushing the backing array past the 2 GiB single-object limit. `EnsureCapacity` calls `Resize` directly, so capacity cannot be pre-inflated into it. |
| `FrozenCelerityDictionary.ThrowIfKeyCountExceedsCeiling`, `FrozenCeleritySet.ThrowIfElementCountExceedsCeiling` | The count is taken *after* materializing the source into a `List<string>`, so reaching 2³⁰ needs an 8.6 GB `string[]` — past the 2 GiB array limit. A source that merely reports a huge `ICollection.Count` cannot reach it; that count is only a capacity hint. |
| `CuckooFilter.AtLeastOne`, `XorFilter.AtLeastOne` | Dead by construction: the constructors' own argument validation already forces both sizing expressions above the floor. |
| `XorFilter.BuildOrThrow`, `XorFilter.TryBuild` | The peel retry schedule is independent of the element set, so no hasher can stall all `MaxConstructionAttempts` seeds. Individual attempts *do* stall and retry — that path lives in `TryPeel`, which stays measured. |
| `Hash64Source.CreateNative` | Its `null` arm is unobservable. `Native` is read only by `Hash64`, and every caller guards that on `IsNative64` being true, so the class is never initialized for a 32-bit-only `THasher` — the arm is evaluated only if the runtime runs the `beforefieldinit` initializer eagerly, which is its option and not a contract. |

That table is the complete set; `grep -rn "ExcludeFromCodeCoverage" src/Celerity*/` should return nothing beyond it.

The rule for new code: exclusions are for genuinely unreachable code and must carry a `Justification` that says *why*. Anything a test can reach gets a test.

> **Adding a shipping package?** Add its assembly to the `<Include>` list in `src/coverage.runsettings`, and its test project to `.github/workflows/coverage.yml`. Coverlet's assembly filter is **exact-match, not a prefix** — `[Celerity]*` compiles to `^Celerity$` and matches only the `Celerity.Collections` assembly. That is how the 2.0.0 package split left five of six packages silently outside the gate until [#314](https://github.com/marius-bughiu/Celerity/issues/314). An unlisted package is unmeasured, and the gate stays green no matter what its coverage is.

Collect and render a report locally:

```bash
# 1. collect Cobertura coverage for the five core packages.
#    Clear stale results first: the four reports are merged by source-file path, and
#    SourceLink resolves those paths from the build's git state — so mixing reports
#    from different commits makes the same file appear twice under two spellings and
#    the merged totals come out roughly halved.
cd src
rm -rf ./TestResults/coverage ./TestResults/showcase
dotnet test Celerity.Tests/Celerity.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --settings coverage.runsettings \
  --results-directory ./TestResults/coverage

# 2. and for the three showcase packages
for project in Ring Sentinel Cardinality; do
  dotnet test "Celerity.${project}.Tests/Celerity.${project}.Tests.csproj" \
    --collect:"XPlat Code Coverage" \
    --settings coverage.runsettings \
    --results-directory "./TestResults/showcase/${project}"
done

# 3. render the HTML report + badge (pure Python, no extra tooling).
#    --input is repeatable; the reports are merged.
python3 ../scripts/coverage_report.py \
  --input "./TestResults/coverage/**/coverage.cobertura.xml" \
  --input "./TestResults/showcase/*/**/coverage.cobertura.xml" \
  --outdir ../coveragereport --min-line 100 --min-branch 100

# 4. open coveragereport/index.html
```

The report is rendered by [`scripts/coverage_report.py`](../scripts/coverage_report.py) — a small generator that reads the Cobertura XML coverlet produces and emits an `index.html` styled like the rest of the Celerity site, a `badge.svg`, and a `summary.md`. It exists so the report carries the project's own look and no third-party "sponsors only" upsell; there is no dependency on ReportGenerator.

### CI gate

The `coverage` workflow (`.github/workflows/coverage.yml`) runs on every PR and on `main`:

- Collects coverage, renders the report + badge with `scripts/coverage_report.py`, and uploads it as a build artifact.
- **Fails the build** if line coverage drops below `MIN_LINE_COVERAGE` (100%) or branch coverage below `MIN_BRANCH_COVERAGE` (100%). The floor is deliberately a hair-trigger: new code arrives with its tests, or the gate goes red. If you hit a genuinely unreachable branch, exclude it at the source with a justification rather than lowering the floor.
- Posts a coverage summary comment on the PR.
- On `main`, publishes the HTML report to `gh-pages` under [`/coverage`](https://marius-bughiu.github.io/Celerity/coverage/) and refreshes the README badge.

## Continuous integration summary

| Workflow | Trigger | What it does |
|---|---|---|
| [`ci.yml`](../.github/workflows/ci.yml) | push / PR | `dotnet build` + `dotnet test` on Linux, Windows, macOS; Native AOT publish + smoke run. |
| [`coverage.yml`](../.github/workflows/coverage.yml) | push / PR | Collect + gate coverage, comment on PRs, publish report on `main`. |
| [`fuzz.yml`](../.github/workflows/fuzz.yml) | nightly / manual | Differential fuzz soak with a time budget. |
| [`benchmarks.yml`](../.github/workflows/benchmarks.yml) | merge to `main` / manual | Sharded core benchmark run, published to the dashboard. Not a per-PR job, and skipped entirely when the commit cannot move a measured number (see [CONTRIBUTING.md](../CONTRIBUTING.md#ci)). |

## Contributing tests

When fixing a bug, add a test that fails on `main` and passes on your branch (see [CONTRIBUTING.md](../CONTRIBUTING.md)). For a new collection, the expectation is parity coverage at every layer: behavioural tests, a property-based parity test against the closest BCL oracle, and a fuzz target. Reusing the existing helpers in each file is the fastest way to get there.
