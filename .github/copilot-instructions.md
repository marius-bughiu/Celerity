# Copilot Instructions for Celerity

## Repository Overview

Celerity is a .NET library of specialized high-performance collections, hashers and supporting primitives, shipped as eight NuGet packages (see Project Structure). The library focuses on data structures designed for better speed or memory efficiency compared to standard .NET collections.

### Key Features
- High-performance dictionary implementations (`CelerityDictionary`, `IntDictionary`)
- Configurable load factors for fine-tuned memory usage
- Multiple built-in hash functions (Wang Naive, Murmur3, FNV-1a)
- Support for custom hash functions via `IHashProvider<T>` interface

## Project Structure

```
src/
├── Celerity/                    # The Celerity.Collections package (assembly Celerity.dll)
│   ├── Collections/             # Collection implementations (CelerityDictionary.cs, IntDictionary.cs, …)
│   ├── TypeForwarders.cs        # Binary back-compat for types moved out in the 2.0.0 package split
│   └── README.md                # The package's NuGet readme
├── Celerity.Hashing/            # IHashProvider<T> and the struct hashers (Int32WangNaiveHasher.cs, StringFnV1AHasher.cs, …)
├── Celerity.Primitives/         # FastUtils, struct PRNGs, VarInt, SpanBits, BitWriter/BitReader, FastGuid, SortedSpan, Morton/Hilbert curves
├── Celerity.Sorting/            # RadixSort, CountingSort, PartialSort, StringSort, KWayMerge
├── Celerity.Statistics/         # DDSketch, KllSketch, ReservoirSampler, RunningStatistics, RollingQuantile, AliasTable
├── Celerity.Ring/               # Showcase: consistent-hash, rendezvous and Maglev routing
├── Celerity.Sentinel/           # Showcase: streaming abuse / heavy-hitter detection
├── Celerity.Cardinality/        # Showcase: mergeable approximate COUNT(DISTINCT) and windowed dedup
├── Celerity.Tests/              # xUnit tests for the core packages (Collections/, Hashing/, Primitives/, Sorting/, Statistics/, …)
├── Celerity.Ring.Tests/         # One test project per showcase package
├── Celerity.Sentinel.Tests/
├── Celerity.Cardinality.Tests/
├── Celerity.Benchmarks/         # BenchmarkDotNet benchmarks
├── Celerity.Fuzz/               # Differential fuzz harness
├── Celerity.AotSmokeTest/       # Native AOT publish/run target
├── Directory.Build.props        # Shared build settings: target framework, MinVer version floor, packaging
├── coverage.runsettings         # Coverage filter: one entry per shipping assembly
└── Celerity.sln                 # Solution file
```

## Build and Test Commands

All commands should be run from the `src/` directory:

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test

# Run tests with coverage (CI gates 100% line and branch)
dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings

# Build without restore
dotnet build --no-restore

# Create NuGet package
dotnet pack --configuration Release
```

## Code Conventions and Standards

### General Guidelines
- **Target Framework**: .NET 10.0 (`net10.0`)
- **Language**: C# with nullable reference types enabled
- **Implicit Usings**: Enabled
- **Documentation**: XML comments are required for all public APIs (`GenerateDocumentationFile` is enabled)

### Naming Conventions
- Use PascalCase for public members, types, and methods
- Use camelCase with underscore prefix (`_fieldName`) for private fields
- Use PascalCase for constants too — fields and method-local alike (e.g., `DefaultCapacity`, `MaxKicks`, `FnvPrime`), never UPPER_CASE. `scripts/check_constant_naming.js` enforces this over the shipping packages in CI

### Code Style
- Use file-scoped namespaces (e.g., `namespace Celerity.Hashing;`)
- Prefer explicit types over `var` where it improves clarity
- Use expression-bodied members for simple properties (e.g., `public int Count => _count;`)
- Follow standard C# formatting conventions

### Testing
- Use xUnit for unit tests
- Test class names should match the class being tested with `Tests` suffix (e.g., `CelerityDictionaryTests`)
- Use descriptive test method names
- Use `[Fact]` for parameterless tests, `[Theory]` with `[InlineData]` for parameterized tests

### Performance Considerations
- This is a performance-focused library - always consider performance implications
- Use structs for hash providers to enable zero-cost abstractions via generics
- Prefer array-based storage for better cache locality
- Avoid unnecessary allocations in hot paths

### Documentation
- All public APIs must have XML documentation comments
- Include `<summary>`, `<param>`, `<returns>`, and `<exception>` tags where applicable
- Reference related types using `<see cref="Type"/>` tags
- Keep documentation concise but informative

## Common Tasks

### Adding a New Hash Provider
1. Create a new struct in `src/Celerity.Hashing/`
2. Implement `IHashProvider<T>` interface
3. Add XML documentation
4. Create corresponding tests in `src/Celerity.Tests/Hashing/`
5. Add benchmarks if comparing with existing implementations

### Adding a New Collection Type
1. Create class in `src/Celerity/Collections/`
2. Follow existing patterns for capacity and load factor management
3. Use appropriate hash provider via generic constraint
4. Add comprehensive unit tests
5. Document it: an API reference section in `docs/api/collections.md`, a bullet and worked example in `docs/collections.md`, a row in `docs/choosing-a-collection.md`, its name in the Collections table of both `README.md` and `src/Celerity/README.md` and in the `docs/README.md` index, and a block in `src/Celerity.AotSmokeTest` with its entry in `docs/aot.md` (see CONTRIBUTING.md, "Documentation")

### Updating Documentation
- README.md is the repository's landing page: keep it short, and put examples, decision tables and benchmark results under `docs/`
- Each package's NuGet readme is the README.md in its own project folder (`src/Celerity/README.md` for `Celerity.Collections`). nuget.org renders a relative link with no destination, so every link in those is absolute
- Update benchmarks when performance characteristics change
- Keep examples simple and focused on common use cases

## Dependencies

### Shipping packages (all eight)
- MinVer: Automated versioning based on git tags

### Test projects (Celerity.Tests and the three showcase test projects)
- xUnit (with xunit.runner.visualstudio): Testing framework
- Microsoft.NET.Test.Sdk: Test runner
- CsCheck: Property-based tests
- coverlet.collector: Coverage collection

### Benchmarks (Celerity.Benchmarks)
- BenchmarkDotNet: Performance benchmarking
- System.Numerics.Tensors: `TensorPrimitives` baselines for the SIMD reduction benchmarks

## Versioning (critical — read before touching any `.csproj`)

**Version numbers are determined solely by git tags.** The library uses [MinVer](https://github.com/adamralph/minver) which reads the nearest `v`-prefixed tag (e.g. `v1.0.1`) from git history and sets the NuGet package version automatically at build time.

**Rules for coding agents:**
- **NEVER** add `<Version>`, `<PackageVersion>`, or `<AssemblyVersion>` to any `.csproj` file
- **NEVER** hardcode a version string anywhere in the source tree
- The `<MinVerTagPrefix>v</MinVerTagPrefix>` and `<MinVerDefaultPreReleaseIdentifiers>beta</MinVerDefaultPreReleaseIdentifiers>` settings in each shipping package's `.csproj`, and the `<MinVerMinimumMajorMinor>` pre-release floor in `src/Directory.Build.props`, are the only version-related MSBuild properties — do not change them
- Pre-release builds (commits after a tag) automatically get versions like `1.0.2-beta.1` — this is correct behavior, not a bug
- To check the current version: `git tag -l 'v*' --sort=-v:refname | head -1`
- To see what MinVer computes: `dotnet build /p:MinVerVerbosity=diagnostic`

## Release Process

Releases are automated via GitHub Actions workflow (`.github/workflows/release.yml`):
1. Move the `CHANGELOG.md` `[Unreleased]` block to `[X.Y.Z]` in a PR and merge it to `main`
2. Tag the merge commit and push the tag: `git tag -a v1.2.0 -m "Release 1.2.0"` then `git push origin v1.2.0`
3. The tag push fires the workflow, which builds, packs, publishes to NuGet.org and creates the GitHub Release from the `## [X.Y.Z]` section of `CHANGELOG.md`
4. `workflow_dispatch` is only a manual fallback, for re-publishing when a NuGet push fails partway through
