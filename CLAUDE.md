# CLAUDE.md

Guidance for AI coding agents working in this repository. [`CONTRIBUTING.md`](CONTRIBUTING.md) is the canonical, human-facing contributor guide — read it for the full picture. This file only adds the agent-specific rules and the conventions worth restating up front.

## What Celerity is

A .NET library of specialized, high-performance collections, hashers, and supporting primitives — drop-in BCL alternatives that trade flexibility for speed or memory on specific workloads. Narrowly scoped: see the **Scope** section of `CONTRIBUTING.md` for what will and won't be accepted.

## Layout

Everything lives under `src/` (three layered packages plus their support projects):

- `Celerity/` — the `Celerity.Collections` package (assembly `Celerity.dll`): dictionaries, sets, frozen/perfect-hash collections, sketches.
- `Celerity.Hashing/` — `IHashProvider<T>` and the struct hashers.
- `Celerity.Primitives/` — `FastUtils`, struct PRNGs, `VarInt`, `SpanBits`, `FastGuid`.
- `Celerity.Tests/` — xUnit tests, mirroring the main project's layout.
- `Celerity.Benchmarks/` — BenchmarkDotNet; runs in CI on every PR.
- `Celerity.Fuzz/` — differential fuzz harness.
- `Celerity.AotSmokeTest/` — Native AOT publish/run target.

The three shipping packages layer as `Celerity.Primitives` ← `Celerity.Hashing` ← `Celerity.Collections`.

## Build & test

```bash
cd src
dotnet build                 # multi-targets net8.0;net9.0;net10.0
dotnet test                  # xUnit
```

- `net8.0` is the floor. Shared code must not use net9/net10-only APIs unguarded — gate newer paths with `#if NET9_0_OR_GREATER` / `NET10_0_OR_GREATER` and keep a net8.0 fallback. The target list lives in `src/Directory.Build.props`.
- Coverage is gated in CI: keep line ≥ 95%, branch ≥ 90%.
- Every public type/member needs an XML doc comment (`GenerateDocumentationFile` is on; missing docs warn).
- Hashers are `struct`s passed as generic constraints (`where THasher : struct, IHashProvider<T>`) so the JIT devirtualizes them — do not turn them into classes/interfaces.
- Avoid allocations on hot paths.

Full coding conventions, the test-naming scheme (`Method_ShouldExpectedBehavior_WhenCondition`), and the parity-coverage expectation for new collections are in `CONTRIBUTING.md`.

## CHANGELOG

`CHANGELOG.md` follows [Keep a Changelog](https://keepachangelog.com/); versions come from git tags via MinVer. Put new entries under `## [Unreleased]` in the matching `### Added` / `### Changed` / `### Fixed` subsection.

**Keep entries short and user-facing — a few sentences at most.** State *what* observably changed and *why it matters to a caller*, not a walkthrough of the implementation.

- Do **not** name private fields, list bit-shift/probe steps, or explain JIT/codegen internals. Those belong in code comments or the PR description, not the changelog.
- One tight entry per change. If you need a paragraph, it probably belongs in the PR body with a one-line changelog pointer.
- End the entry with `Closes #NNN` (or `Closes [#NNN](...)`).

This is not only a style rule: `.github/workflows/release.yml` extracts the whole `## [X.Y.Z]` section verbatim as the GitHub Release body, and an over-long section can exceed GitHub's size limit and break the release. Terse sections keep releases publishable.

Good:

```
- `BitSet.Flip(int)` is now marked `[MethodImpl(AggressiveInlining)]`, matching the sibling `Get`/`Set` accessors. Codegen-hint consistency only — no behavioural change. Closes #253.
```

Too verbose (avoid): a full paragraph reciting the `(uint)index >= (uint)_length` guard, the `_words[index >> WordShift]` word ref, `_version++`, and the JIT's inlining freedom.

## Commits, branches, PRs

- Branch off `main`; open PRs with `--base main`.
- Conventional-commit style subjects (`perf(BitSet): …`, `fix(...)`, `docs(...)`).
- Bug fixes ship with a regression test that fails on `main`.
- Never add `<Version>` / `<PackageVersion>` / `<AssemblyVersion>` to any `.csproj` — MinVer owns versioning from git tags.
