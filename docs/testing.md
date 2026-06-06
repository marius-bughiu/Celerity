# Testing & Coverage

Celerity's first guiding principle is *correctness first* — "a fast collection that returns wrong answers is worthless." This document describes how that principle is enforced: the layers of tests, the property-based and fuzz harnesses that hunt for the bugs example-based tests miss, how code coverage is measured and gated, and how to run each layer locally.

## TL;DR

| Layer | Project / file | What it proves | Run locally |
|---|---|---|---|
| Behavioural unit tests | `Celerity.Tests` | Each public method does the right thing on hand-picked inputs, including collisions, resizes, and the out-of-band default/zero/null key. | `dotnet test` |
| Edge-case coverage tests | `Celerity.Tests/Collections/EdgeCaseCoverageTests.cs` | The corners example tests skip: non-generic `IEnumerable`/`IEnumerator` paths, `Reset()`, indexer misses, `Clear()` on empty, wrap-around backward-shift. | `dotnet test` |
| Property-based tests | `Celerity.Tests/Properties/` | Across thousands of randomized operation sequences, every collection stays observably equal to its BCL oracle. | `dotnet test` |
| Differential fuzzer | `Celerity.Fuzz` | A long random walk finds no divergence from the BCL; failures replay deterministically from a seed. | `dotnet run -c Release` |
| Native AOT smoke test | `Celerity.AotSmokeTest` | Every collection/hasher works in a trimmed, AOT-compiled native binary. | see [aot.md](aot.md) |

All of these run in CI. Coverage is measured on the library assembly and gated; the rendered report is published to [the coverage dashboard](https://marius-bughiu.github.io/Celerity/coverage/).

## Philosophy: example tests, then adversarial tests

Example-based unit tests are necessary but not sufficient for a data-structure library. They prove the cases the author *thought of*. The bugs that actually ship in open-addressed hash tables live in the cases nobody enumerated: a particular interleaving of inserts and deletes that leaves a tombstone in the wrong slot, a resize triggered mid-probe-chain, a backward-shift that wraps the table boundary and orphans a colliding key.

Celerity attacks those with two adversarial layers that don't rely on the author's imagination:

- **Property-based testing** generates random operation sequences and checks an *invariant* — here, equivalence to a known-correct BCL collection — rather than a fixed expected output.
- **Differential fuzzing** runs the same idea as an unbounded soak: keep generating sequences until something diverges, and when it does, hand back a seed that reproduces it.

Both compare against a BCL oracle (`Dictionary<,>`, `HashSet<>`, or a `Dictionary<TKey, List<TValue>>` model for the multi-map). The oracle *is* the specification: Celerity claims drop-in parity, so any observable difference is a bug in Celerity.

## Behavioural unit tests

The bulk of the suite lives in `Celerity.Tests`, mirroring the library's folder layout. Test names follow `Method_ShouldExpectedBehavior_WhenCondition`. Notable categories:

- **Collision tests** (`*CollisionTests.cs`) — force every key down one probe chain with a constant hasher, then verify lookups, removals, and backward-shift deletion keep every entry findable.
- **Enumeration tests** (`*EnumerationTests.cs`) — the struct enumerators, `Keys`/`Values` views, and mid-enumeration mutation detection.
- **Load-factor / constructor validation** — boundary resizes and argument checking.
- **Edge-case coverage** (`EdgeCaseCoverageTests.cs`) — the non-generic interface surface (`IEnumerable.GetEnumerator()`, `object IEnumerator.Current`, `IEnumerator.Reset()`), indexer misses on the out-of-band key, `Clear()` on an empty collection, and a hand-built wrap-around cluster that exercises the `bypassesGap` branch of backward-shift deletion.

Run them with:

```bash
dotnet test
```

## Property-based tests (CsCheck)

`Celerity.Tests/Properties/CollectionModelPropertyTests.cs` uses [CsCheck](https://github.com/AnthonyLloyd/CsCheck) to make parity the explicit contract. Each test:

1. Generates a randomized list of mutating operations (`Set`, `Remove`, `TryAdd`, `Clear` for dictionaries; `Add`/`RemoveValue`/`RemoveAll` for the multi-map; etc.).
2. Applies the **identical** sequence to a Celerity collection and a BCL oracle.
3. Asserts the two are observably equal — `Count`, per-key lookups across the whole key domain, and full enumeration.

The key domains are deliberately tiny (and include `0` and negatives) so collisions, resizes, the special zero/default/null-key slot, and backward-shift deletion all fire densely. Each test runs 2 000 sequences per invocation.

When a property fails, CsCheck **shrinks** the failing sequence to a minimal reproduction and prints a seed. Replay it by setting the seed:

```bash
# PowerShell
$env:CsCheck_Seed = '0000LASTpRINTED'; dotnet test --filter CollectionModelPropertyTests
```

```bash
# bash
CsCheck_Seed='0000LASTpRINTED' dotnet test --filter CollectionModelPropertyTests
```

Coverage targets: `CelerityDictionary`, `IntDictionary`, `LongDictionary`, `CeleritySet`, `IntSet`, `LongSet`, `CelerityMultiMap`, and `FrozenCelerityDictionary`.

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

## Code coverage

Coverage is collected with [coverlet](https://github.com/coverlet-coverage/coverlet) (already referenced by the test project) and scoped to the shipping `Celerity` assembly via [`src/coverage.runsettings`](../src/coverage.runsettings) — the test, benchmark, fuzz, and AOT-smoke assemblies are tooling, not the subject under measurement.

Collect and render a report locally:

```bash
# 1. collect Cobertura coverage for the library only
cd src
dotnet test Celerity.Tests/Celerity.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --settings coverage.runsettings \
  --results-directory ./TestResults/coverage

# 2. render the HTML report + badge (pure Python, no extra tooling)
python3 ../scripts/coverage_report.py \
  --input "./TestResults/coverage/**/coverage.cobertura.xml" \
  --outdir ../coveragereport --min-line 95 --min-branch 90

# 3. open coveragereport/index.html
```

The report is rendered by [`scripts/coverage_report.py`](../scripts/coverage_report.py) — a small generator that reads the Cobertura XML coverlet produces and emits an `index.html` styled like the rest of the Celerity site, a `badge.svg`, and a `summary.md`. It exists so the report carries the project's own look and no third-party "sponsors only" upsell; there is no dependency on ReportGenerator.

### CI gate

The `coverage` workflow (`.github/workflows/coverage.yml`) runs on every PR and on `main`:

- Collects coverage, renders the report + badge with `scripts/coverage_report.py`, and uploads it as a build artifact.
- **Fails the build** if line coverage drops below `MIN_LINE_COVERAGE` (95%) or branch coverage below `MIN_BRANCH_COVERAGE` (90%). The suite sits far above these (~99.9% line) — the floor guards against silent regressions; it is not the target.
- Posts a coverage summary comment on the PR.
- On `main`, publishes the HTML report to `gh-pages` under [`/coverage`](https://marius-bughiu.github.io/Celerity/coverage/) and refreshes the README badge.

### What is deliberately not covered

A single line — the integer-overflow guard in `FrozenCelerityDictionary`'s table sizing (`if (size <= n) size <<= 1;`) — is unreachable without ~2³⁰ keys, so it is left uncovered by design rather than tested with an impractically large input. It is defensive code, kept for safety.

## Continuous integration summary

| Workflow | Trigger | What it does |
|---|---|---|
| [`ci.yml`](../.github/workflows/ci.yml) | push / PR | `dotnet build` + `dotnet test` on Linux, Windows, macOS; Native AOT publish + smoke run. |
| [`coverage.yml`](../.github/workflows/coverage.yml) | push / PR | Collect + gate coverage, comment on PRs, publish report on `main`. |
| [`fuzz.yml`](../.github/workflows/fuzz.yml) | nightly / manual | Differential fuzz soak with a time budget. |
| [`benchmarks.yml`](../.github/workflows/benchmarks.yml) | push / PR | Same-runner A/B benchmark comparison vs `main`. |

## Contributing tests

When fixing a bug, add a test that fails on `main` and passes on your branch (see [CONTRIBUTING.md](../CONTRIBUTING.md)). For a new collection, the expectation is parity coverage at every layer: behavioural tests, a property-based parity test against the closest BCL oracle, and a fuzz target. Reusing the existing helpers in each file is the fastest way to get there.
