# Copilot Instructions for Celerity

## Repository Overview

Celerity is a .NET library providing specialized high-performance collections optimized for specific use cases. The library focuses on data structures designed for better speed or memory efficiency compared to standard .NET collections.

### Key Features
- High-performance dictionary implementations (`CelerityDictionary`, `IntDictionary`)
- Configurable load factors for fine-tuned memory usage
- Multiple built-in hash functions (Wang Naive, Murmur3, FNV-1a)
- Support for custom hash functions via `IHashProvider<T>` interface

## Project Structure

```
src/
├── Celerity/                    # Main library code
│   ├── Collections/             # Collection implementations
│   │   ├── CelerityDictionary.cs
│   │   └── IntDictionary.cs
│   ├── Hashing/                 # Hash providers
│   │   ├── IHashProvider.cs
│   │   ├── Int32WangNaiveHasher.cs
│   │   ├── Int64Murmur3Hasher.cs
│   │   └── StringFnV1AHasher.cs
│   └── FastUtils.cs             # Utility functions
├── Celerity.Tests/              # Unit tests (xUnit)
├── Celerity.Benchmarks/         # BenchmarkDotNet benchmarks
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

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Build without restore
dotnet build --no-restore

# Create NuGet package
dotnet pack --configuration Release
```

## Code Conventions and Standards

### General Guidelines
- **Target Framework**: .NET 8.0
- **Language**: C# with nullable reference types enabled
- **Implicit Usings**: Enabled
- **Documentation**: XML comments are required for all public APIs (`GenerateDocumentationFile` is enabled)

### Naming Conventions
- Use PascalCase for public members, types, and methods
- Use camelCase with underscore prefix (`_fieldName`) for private fields
- Use UPPER_CASE for constants (e.g., `DEFAULT_CAPACITY`, `DEFAULT_LOAD_FACTOR`)

### Code Style
- Use file-scoped namespaces (e.g., `namespace Celerity.Hashing;`)
- Prefer explicit types over `var` where it improves clarity
- Use expression-bodied members for simple properties (e.g., `public int Count => _count;`)
- Follow standard C# formatting conventions

### Testing
- Use xUnit for unit tests
- Test class names should match the class being tested with `Tests` suffix (e.g., `CelerityDictionaryTests`)
- Use descriptive test method names following pattern: `Method_ShouldExpectedBehavior_WhenCondition`
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
1. Create a new struct in `src/Celerity/Hashing/`
2. Implement `IHashProvider<T>` interface
3. Add XML documentation
4. Create corresponding tests in `src/Celerity.Tests/Hashing/`
5. Add benchmarks if comparing with existing implementations

### Adding a New Collection Type
1. Create class in `src/Celerity/Collections/`
2. Follow existing patterns for capacity and load factor management
3. Use appropriate hash provider via generic constraint
4. Add comprehensive unit tests
5. Update README.md with usage examples and benchmarks

### Updating Documentation
- README.md is packaged with the NuGet package
- Update benchmarks when performance characteristics change
- Keep examples simple and focused on common use cases

## Dependencies

### Main Library (Celerity)
- MinVer: Automated versioning based on git tags

### Tests (Celerity.Tests)
- xUnit: Testing framework
- Microsoft.NET.Test.Sdk: Test runner

### Benchmarks (Celerity.Benchmarks)
- BenchmarkDotNet: Performance benchmarking

## Release Process

Releases are automated via GitHub Actions workflow (`.github/workflows/release.yml`):
1. Triggered via workflow_dispatch
2. Builds the solution
3. Creates NuGet packages
4. Publishes to NuGet.org

Version numbers are determined by git tags using MinVer with prefix `v` (e.g., `v1.0.0`).
