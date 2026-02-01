#!/bin/bash

# Script to create GitHub issues for Celerity roadmap
# Prerequisites: Install GitHub CLI (gh) and authenticate with: gh auth login
# Usage: ./create-roadmap-issues.sh

set -e

echo "Creating GitHub issues for Celerity roadmap..."
echo "Note: This assumes you have gh CLI installed and authenticated."
echo ""

# Phase 1: Infrastructure & Tooling (skip #1 and #2 as they already exist)

echo "Creating Phase 1 issues..."

gh issue create \
  --title "Improve documentation" \
  --label "documentation,priority:high" \
  --body "**Priority:** High
**Phase:** 1 - Infrastructure & Tooling (Q1 2026)

## Description
Expand and improve documentation to help users get started and optimize their usage of Celerity collections.

## Tasks
- [ ] Add usage examples to README
- [ ] Expand API documentation with detailed XML comments
- [ ] Create performance tuning guide
- [ ] Document when to use which collection
- [ ] Add migration guide from standard .NET collections
- [ ] Add troubleshooting section
- [ ] Create FAQ

## Related
See ROADMAP.md for context"

# Phase 2: New Collections

echo "Creating Phase 2 issues..."

gh issue create \
  --title "Implement CeleritySet" \
  --label "enhancement,new-collection,priority:high" \
  --body "**Priority:** High
**Phase:** 2 - New Collections (Q2 2026)

## Description
Implement a high-performance set collection with custom hashers, similar to HashSet<T> but optimized for specific use cases.

## Requirements
- Similar API to HashSet<T>
- Support for custom hash functions via IHashProvider<T>
- Configurable load factors
- Optimized for membership testing
- Comprehensive unit tests
- Benchmarks comparing to HashSet<T>
- XML documentation

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Implement LongDictionary" \
  --label "enhancement,new-collection,priority:medium" \
  --body "**Priority:** Medium
**Phase:** 2 - New Collections (Q2 2026)

## Description
Create a specialized dictionary for Int64/long keys, similar to IntDictionary but optimized for 64-bit keys.

## Requirements
- Optimized memory layout for 64-bit keys
- Custom hasher support
- API consistent with IntDictionary
- Comprehensive unit tests
- Benchmarks comparing to Dictionary<long, T>
- XML documentation

## Related
- Similar to IntDictionary implementation
- See ROADMAP.md for context"

gh issue create \
  --title "Implement CelerityMultiMap" \
  --label "enhancement,new-collection,priority:medium" \
  --body "**Priority:** Medium
**Phase:** 2 - New Collections (Q2 2026)

## Description
Implement a dictionary that allows multiple values per key (MultiMap/MultiDictionary).

## Requirements
- Efficient storage and retrieval of key-value groups
- Add, Remove, RemoveAll operations
- Get all values for a key
- Custom hasher support
- Comprehensive unit tests
- Benchmarks
- XML documentation

## Use Cases
- Grouping related items
- One-to-many relationships
- Event handlers with multiple callbacks per event

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Implement CelerityList" \
  --label "enhancement,new-collection,priority:low" \
  --body "**Priority:** Low
**Phase:** 2 - New Collections (Q2 2026)

## Description
Create a high-performance list with customizable growth strategies and optimizations for specific access patterns.

## Requirements
- Customizable growth strategies
- Optimized for different access patterns (sequential, random)
- Pooled allocations to reduce GC pressure
- API similar to List<T>
- Comprehensive unit tests
- Benchmarks comparing to List<T>
- XML documentation

## Related
See ROADMAP.md for context"

# Phase 3: Advanced Features

echo "Creating Phase 3 issues..."

gh issue create \
  --title "Add thread-safe collections" \
  --label "enhancement,concurrency,priority:high" \
  --body "**Priority:** High
**Phase:** 3 - Advanced Features (Q3 2026)

## Description
Implement thread-safe versions of Celerity collections for concurrent scenarios.

## Requirements
- ConcurrentCelerityDictionary implementation
- Lock-free or fine-grained locking strategies
- Comprehensive concurrency tests
- Benchmarks comparing to ConcurrentDictionary
- Documentation on thread-safety guarantees
- XML documentation

## Considerations
- Trade-offs between performance and safety
- Different locking strategies for different scenarios
- Memory barriers and synchronization primitives

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Memory-pooled collections" \
  --label "enhancement,performance,priority:medium" \
  --body "**Priority:** Medium
**Phase:** 3 - Advanced Features (Q3 2026)

## Description
Use ArrayPool<T> for backing arrays to reduce GC pressure in high-throughput scenarios.

## Requirements
- Pooled variants of existing collections
- Proper pool rental and return lifecycle
- Documentation on when to use pooled collections
- Benchmarks showing GC impact
- Examples of usage patterns
- XML documentation

## Considerations
- When to rent/return from pool
- Handling of pool exhaustion
- Memory leak prevention

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Implement frozen collections" \
  --label "enhancement,new-collection,priority:medium" \
  --body "**Priority:** Medium
**Phase:** 3 - Advanced Features (Q3 2026)

## Description
Create immutable, read-only collections optimized for lookups with custom hashers.

## Requirements
- Similar to .NET 8's FrozenDictionary but with custom hashers
- Perfect hash function generation for static data
- Immutable design
- Optimized for read-heavy workloads
- Comprehensive tests
- Benchmarks comparing to FrozenDictionary and ImmutableDictionary
- XML documentation

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Add SIMD optimizations" \
  --label "enhancement,performance,priority:low" \
  --body "**Priority:** Low
**Phase:** 3 - Advanced Features (Q3 2026)

## Description
Use Vector<T> and SIMD instructions for batch operations where applicable.

## Requirements
- SIMD-accelerated hash functions
- Vectorized search operations
- Platform-specific optimizations (AVX2, AVX-512, NEON)
- Benchmarks showing SIMD improvements
- Fallback for platforms without SIMD support
- Documentation

## Considerations
- Platform detection
- Alignment requirements
- When SIMD provides actual benefits

## Related
See ROADMAP.md for context"

# Phase 4: Additional Hash Functions

echo "Creating Phase 4 issues..."

gh issue create \
  --title "Add more hash function implementations" \
  --label "enhancement,hashing,priority:medium" \
  --body "**Priority:** Medium
**Phase:** 4 - Additional Hash Functions (Q3 2026)

## Description
Implement additional high-quality hash functions to give users more options.

## Hash Functions to Add
- [ ] XXHash family (XXH3, XXH32, XXH64)
- [ ] CityHash
- [ ] SipHash (cryptographic quality)
- [ ] HighwayHash
- [ ] MetroHash

## Requirements
- Implement each hash function
- Comprehensive unit tests
- Benchmarks comparing all hash functions
- Document characteristics of each (speed, distribution, collision resistance)
- Usage recommendations

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Adaptive hash function selection" \
  --label "enhancement,hashing,priority:low" \
  --body "**Priority:** Low
**Phase:** 4 - Additional Hash Functions (Q3 2026)

## Description
Automatically choose optimal hash function based on data patterns and runtime profiling.

## Requirements
- Runtime profiling to detect poor hash distribution
- Automatic hash function switching with rehashing
- Configurable thresholds for switching
- Performance impact analysis
- Documentation on when adaptive mode is beneficial

## Considerations
- Cost of profiling vs benefits
- When to trigger rehashing
- Memory overhead during transition

## Related
See ROADMAP.md for context"

# Phase 5: Performance & Quality

echo "Creating Phase 5 issues..."

gh issue create \
  --title "Comprehensive benchmark suite expansion" \
  --label "benchmarks,priority:high" \
  --body "**Priority:** High
**Phase:** 5 - Performance & Quality (Q4 2026)

## Description
Expand benchmark suite to cover more realistic workload scenarios and metrics.

## Tasks
- [ ] More realistic workload scenarios
- [ ] Memory allocation benchmarks
- [ ] Concurrent access benchmarks
- [ ] Cache locality measurements
- [ ] Comparison with other high-performance libraries (e.g., FastHashSet, FASTER)
- [ ] Different data distributions (sequential, random, clustered)
- [ ] Large dataset benchmarks (millions of items)

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Performance optimizations" \
  --label "performance,priority:high" \
  --body "**Priority:** High
**Phase:** 5 - Performance & Quality (Q4 2026)

## Description
Profile and optimize hot paths in existing collections.

## Tasks
- [ ] Profile with BenchmarkDotNet and profilers
- [ ] Reduce allocations in critical paths
- [ ] Improve cache locality
- [ ] Branch prediction optimization
- [ ] Inline critical methods with AggressiveInlining
- [ ] Reduce bounds checking where safe
- [ ] Optimize resize operations

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Cross-platform testing" \
  --label "testing,infrastructure,priority:medium" \
  --body "**Priority:** Medium
**Phase:** 5 - Performance & Quality (Q4 2026)

## Description
Ensure Celerity works correctly and performs well across different platforms and architectures.

## Tasks
- [ ] Set up CI for Linux, macOS, Windows
- [ ] Test on ARM64 and x64 architectures
- [ ] Benchmark performance characteristics across platforms
- [ ] Document platform-specific considerations
- [ ] Test with different .NET versions

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Code coverage improvements" \
  --label "testing,quality,priority:medium" \
  --body "**Priority:** Medium
**Phase:** 5 - Performance & Quality (Q4 2026)

## Description
Improve test coverage to ensure robustness and catch edge cases.

## Goals
Achieve 95%+ code coverage with edge case testing, fuzz testing, and property-based testing.

## Tasks
- [ ] Set up code coverage reporting
- [ ] Add tests for uncovered edge cases
- [ ] Implement property-based tests
- [ ] Set up fuzzing infrastructure
- [ ] Document testing approach

## Related
See ROADMAP.md for context"

# Phase 6: Advanced Use Cases

echo "Creating Phase 6 issues..."

gh issue create \
  --title "Specialized collections for specific domains" \
  --label "enhancement,new-collection,priority:low" \
  --body "**Priority:** Low
**Phase:** 6 - Advanced Use Cases (Q1 2027)

## Description
Implement specialized collections for specific use cases and domains.

## Collections to Consider
- [ ] StringDictionary with specialized string hashing
- [ ] StructDictionary for value-type keys without boxing
- [ ] BitSet for dense boolean arrays
- [ ] BloomFilter for probabilistic membership testing
- [ ] CountMinSketch for frequency estimation
- [ ] HyperLogLog for cardinality estimation

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Serialization support" \
  --label "enhancement,serialization,priority:medium" \
  --body "**Priority:** Medium
**Phase:** 6 - Advanced Use Cases (Q1 2027)

## Description
Add efficient serialization support for Celerity collections.

## Requirements
- [ ] Efficient binary serialization
- [ ] JSON serialization support (System.Text.Json)
- [ ] MessagePack support
- [ ] Zero-copy deserialization where possible
- [ ] Versioning strategy
- [ ] Tests for serialization round-trips
- [ ] Performance benchmarks

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Native AOT support" \
  --label "enhancement,aot,priority:medium" \
  --body "**Priority:** Medium
**Phase:** 6 - Advanced Use Cases (Q1 2027)

## Description
Ensure full compatibility with Native AOT compilation.

## Tasks
- [ ] Remove reflection usage or make AOT-compatible
- [ ] Test with PublishAot
- [ ] Benchmark performance with AOT vs JIT
- [ ] Make trim-friendly
- [ ] Document AOT considerations
- [ ] Add CI job for AOT compilation

## Related
See ROADMAP.md for context"

# Phase 7: Ecosystem & Community

echo "Creating Phase 7 issues..."

gh issue create \
  --title "Community engagement" \
  --label "community,priority:medium" \
  --body "**Priority:** Medium
**Phase:** 7 - Ecosystem & Community (Ongoing)

## Description
Build and engage with the Celerity community.

## Tasks
- [ ] Create GitHub Discussions
- [ ] Set up templates for feature requests
- [ ] Create contributor guidelines (CONTRIBUTING.md)
- [ ] Set up code of conduct
- [ ] Regular community updates
- [ ] Accept and review community contributions
- [ ] Recognize contributors

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Educational content" \
  --label "documentation,community,priority:low" \
  --body "**Priority:** Low
**Phase:** 7 - Ecosystem & Community (Ongoing)

## Description
Create educational content to help users learn about high-performance collections and Celerity.

## Ideas
- [ ] Blog posts about performance optimization
- [ ] Video tutorials
- [ ] Sample projects demonstrating use cases
- [ ] Conference talks / presentations
- [ ] Comparison guides with other libraries
- [ ] Performance tuning workshops

## Related
See ROADMAP.md for context"

gh issue create \
  --title "Integration examples" \
  --label "documentation,examples,priority:low" \
  --body "**Priority:** Low
**Phase:** 7 - Ecosystem & Community (Ongoing)

## Description
Create examples showing how to integrate Celerity with popular frameworks and scenarios.

## Examples Needed
- [ ] Integration with EF Core
- [ ] Integration with ASP.NET Core (caching)
- [ ] Caching scenarios (Redis alternatives)
- [ ] Game development use cases (entity systems)
- [ ] Real-time processing pipelines
- [ ] In-memory databases

## Related
See ROADMAP.md for context"

echo ""
echo "✅ All roadmap issues created successfully!"
echo "Note: Issues #1 (Set up github-action-benchmark) and #2 (Create hash function evaluator) already exist."
