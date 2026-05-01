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

```
src/
├── Celerity/                 Main library. All public APIs live here.
│   ├── Collections/          CelerityDictionary, IntDictionary, ...
│   ├── Hashing/              IHashProvider<T> and built-in implementations.
│   └── FastUtils.cs          Low-level helpers (e.g. NextPowerOfTwo).
├── Celerity.Tests/           xUnit test project. Mirrors the main project's layout.
├── Celerity.Benchmarks/      BenchmarkDotNet project. Not built in CI by default.
└── Celerity.sln
```

## Making changes

1. Open (or comment on) a GitHub issue before starting a large change. Small bug fixes can skip this step. Browse open work via the [Issues](https://github.com/marius-bughiu/Celerity/issues) tab or by [milestone](https://github.com/marius-bughiu/Celerity/milestones).
2. Create a branch off `main`.
3. Write the change together with the test that would have caught the bug. Bug fixes without regression tests will be asked to add one.
4. Run `dotnet test` locally.
5. Open a PR. CI will run `dotnet build` and `dotnet test` on your branch automatically (`.github/workflows/ci.yml`).

## Coding conventions

These are enforced by review, not by an analyzer. Reading the existing code is the fastest way to get a feel for the style.

- Target framework is `net8.0`. Nullable reference types are enabled.
- File-scoped namespaces (`namespace Celerity.Hashing;`).
- `PascalCase` for public members, `_camelCase` for private fields, `UPPER_CASE` for constants.
- Every public type and member has an XML doc comment. `GenerateDocumentationFile` is on, so missing docs produce warnings.
- Hash providers are structs that implement `IHashProvider<T>`. This is load-bearing: passing them as a generic constraint (`where THasher : struct, IHashProvider<T>`) lets the JIT devirtualize `hasher.Hash(...)` calls. Please do not change them to classes or interfaces.
- Prefer explicit types over `var` where it meaningfully helps readability (e.g. in tight numeric loops). Use `var` freely for obvious right-hand-sides.
- Avoid allocations on hot paths. If you add a new dependency or a LINQ call inside a probe loop, expect pushback.

## Tests

- Use xUnit.
- Name tests `Method_ShouldExpectedBehavior_WhenCondition`.
- Prefer `[Fact]` for a single case, `[Theory] + [InlineData]` for parameterized cases.
- When fixing a bug, add a test that fails on `main` and passes on your branch. It's fine to reference the issue number in a comment.

## Benchmarks

Benchmarks live in `src/Celerity.Benchmarks`. Run locally with:

```bash
cd src/Celerity.Benchmarks
dotnet run -c Release
```

If a change is motivated by performance, include before/after numbers in the PR description. Numbers without `-c Release` are not useful — BenchmarkDotNet will refuse to run in Debug and the README benchmarks are all Release.

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

### Cutting a release

Releases are automated. Pushing a `v`-prefixed tag fires `.github/workflows/release.yml`, which builds, packs, publishes to NuGet.org, and creates a matching GitHub Release with notes extracted from `CHANGELOG.md`.

```bash
# 1. Move the CHANGELOG [Unreleased] block to [X.Y.Z] (with today's date if you
#    want one — the workflow does not require a date), commit, and merge to main.
# 2. Tag the merge commit and push the tag.
git tag -a v1.2.0 -m "Release 1.2.0"
git push origin v1.2.0
```

The workflow extracts the `## [X.Y.Z]` section of `CHANGELOG.md` and uses it as the GitHub Release body. If no matching section exists for the tag's version, the workflow fails loudly — the fix is to update `CHANGELOG.md` and re-tag.

`workflow_dispatch` is still wired up as a manual fallback for ad-hoc re-publishes (e.g. if a NuGet push fails partway through), but the normal flow is tag-push.

## Scope

Celerity is narrowly scoped: specialized high-performance collections, hashers, and the minimal supporting utilities they need. We are unlikely to accept:

- General-purpose extension methods that aren't used by a collection in the library.
- Wrappers around BCL types that don't add a performance benefit backed by benchmarks.
- Features that require reflection on hot paths.
- Thread-safety primitives. Use `ConcurrentDictionary<,>` or external locking.

If you're unsure whether something fits, open an issue and ask — it's cheaper for both of us.
