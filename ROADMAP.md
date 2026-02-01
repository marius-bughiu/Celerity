# Celerity Roadmap

This document outlines the planned features, improvements, and enhancements for the Celerity high-performance collections library.

## Vision

Celerity aims to provide a comprehensive suite of high-performance, specialized collections for .NET developers who need optimized data structures for specific use cases. The library focuses on:
- **Performance**: Faster than standard .NET collections where it matters
- **Flexibility**: Customizable hash functions and load factors
- **Ease of Use**: Simple APIs that feel familiar to .NET developers
- **Quality**: Well-tested, benchmarked, and documented code

## Current State

- ✅ Core dictionary implementations (CelerityDictionary, IntDictionary)
- ✅ Multiple hash function implementations (Wang, Murmur3, FNV-1a)
- ✅ Comprehensive test coverage
- ✅ Benchmark suite with BenchmarkDotNet
- ✅ NuGet package published

## Roadmap Items

### Phase 1: Infrastructure & Tooling (Q1 2026)

#### 1. Set up github-action-benchmark
**Priority: High**
- Integrate automated benchmark tracking into CI/CD pipeline
- Track performance regressions over time
- Visualize performance trends on GitHub Pages
- Reference: https://github.com/benchmark-action/github-action-benchmark

#### 2. Create hash function evaluator
**Priority: Medium**
- Build a tool to evaluate hash function quality
- Measure distribution uniformity
- Test collision rates
- Compare performance across different data patterns
- Help users choose the right hash function for their use case

#### 3. Improve documentation
**Priority: High**
- Add usage examples to README
- Create API documentation with detailed XML comments
- Add performance tuning guide
- Document when to use which collection
- Add migration guide from standard .NET collections

### Phase 2: New Collections (Q2 2026)

#### 4. Implement CeleritySet
**Priority: High**
- High-performance set implementation with custom hashers
- Similar API to HashSet<T>
- Support for configurable load factors
- Optimized for membership testing

#### 5. Implement LongDictionary
**Priority: Medium**
- Specialized dictionary for Int64 keys
- Similar to IntDictionary but for long keys
- Optimized memory layout for 64-bit keys

#### 6. Implement CelerityMultiMap
**Priority: Medium**
- Dictionary that allows multiple values per key
- Efficient storage and retrieval of key-value groups
- Common use case in many applications

#### 7. Implement CelerityList
**Priority: Low**
- High-performance list with customizable growth strategies
- Optimized for specific access patterns (sequential, random)
- Pooled allocations to reduce GC pressure

### Phase 3: Advanced Features (Q3 2026)

#### 8. Add thread-safe collections
**Priority: High**
- ConcurrentCelerityDictionary
- Lock-free or fine-grained locking strategies
- Benchmarks comparing to ConcurrentDictionary

#### 9. Memory-pooled collections
**Priority: Medium**
- Use ArrayPool<T> for backing arrays
- Reduce GC pressure in high-throughput scenarios
- Add pooled variants of existing collections

#### 10. Implement frozen collections
**Priority: Medium**
- Immutable, read-only collections optimized for lookups
- Similar to .NET 8's FrozenDictionary but with custom hashers
- Perfect hash function generation for static data

#### 11. Add SIMD optimizations
**Priority: Low**
- Use Vector<T> for batch operations where applicable
- SIMD-accelerated hash functions
- Vectorized search operations

### Phase 4: Additional Hash Functions (Q3 2026)

#### 12. Add more hash function implementations
**Priority: Medium**
- XXHash family (XXH3, XXH32, XXH64)
- CityHash
- SipHash (cryptographic quality)
- HighwayHash
- MetroHash
- Benchmark and document characteristics of each

#### 13. Adaptive hash function selection
**Priority: Low**
- Automatically choose optimal hash function based on data patterns
- Runtime profiling to detect poor hash distribution
- Switch hash functions and rehash if needed

### Phase 5: Performance & Quality (Q4 2026)

#### 14. Comprehensive benchmark suite expansion
**Priority: High**
- More realistic workload scenarios
- Memory allocation benchmarks
- Concurrent access benchmarks
- Cache locality measurements
- Comparison with other high-performance libraries

#### 15. Performance optimizations
**Priority: High**
- Profile and optimize hot paths
- Reduce allocations
- Improve cache locality
- Branch prediction optimization
- Inline critical methods

#### 16. Cross-platform testing
**Priority: Medium**
- Test on Linux, macOS, Windows
- Test on ARM64 and x64 architectures
- Ensure performance characteristics across platforms

#### 17. Code coverage improvements
**Priority: Medium**
- Achieve 95%+ code coverage
- Edge case testing
- Fuzz testing for robustness
- Property-based testing

### Phase 6: Advanced Use Cases (Q1 2027)

#### 18. Specialized collections for specific domains
**Priority: Low**
- StringDictionary with specialized string hashing
- StructDictionary for value-type keys without boxing
- BitSet for dense boolean arrays
- BloomFilter for probabilistic membership testing
- CountMinSketch for frequency estimation

#### 19. Serialization support
**Priority: Medium**
- Efficient binary serialization
- JSON serialization support
- MessagePack support
- Zero-copy deserialization where possible

#### 20. Native AOT support
**Priority: Medium**
- Ensure full compatibility with Native AOT compilation
- Benchmark performance with AOT
- Trim-friendly design

### Phase 7: Ecosystem & Community (Ongoing)

#### 21. Community engagement
**Priority: Medium**
- Create discussions for feature requests
- Regular community updates
- Accept and review community contributions
- Create contributor guidelines

#### 22. Educational content
**Priority: Low**
- Blog posts about performance optimization
- Video tutorials
- Sample projects demonstrating use cases
- Conference talks / presentations

#### 23. Integration examples
**Priority: Low**
- Integration with EF Core
- Integration with ASP.NET Core
- Caching scenarios
- Game development use cases

## Contributing

We welcome contributions! If you'd like to work on any of these items, please:
1. Check if there's already an issue for it
2. Comment on the issue to express interest
3. Submit a PR with your implementation

## Feedback

This roadmap is a living document. If you have suggestions for additions or changes, please open an issue or discussion on GitHub.

---

Last updated: February 2026
