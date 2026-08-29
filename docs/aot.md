# Native AOT & trimming

Celerity is compatible with [Native AOT](https://learn.microsoft.com/dotnet/core/deploying/native-aot/) compilation and [trimming](https://learn.microsoft.com/dotnet/core/deploying/trimming/trimming-options). You can reference any of the eight shipping packages from a `PublishAot` or trimmed application and it will produce no `IL2xxx` (trim) or `IL3xxx` (AOT) warnings.

## Why it works

The design choices that make Celerity fast also make it AOT-friendly:

- **No reflection.** No package in the family contains `System.Reflection` usage, no `Activator.CreateInstance`, no `Type.MakeGenericType`, no `System.Linq.Expressions`, and no IL emit. The one place anything is read from metadata at run time is `EnumSet<TEnum>` / `EnumMap<TEnum, TValue>`, whose type initializer sizes its backing store from the enum's declared constants via `Enum.GetValuesAsUnderlyingType` — which is AOT-safe (it hands back the underlying-type array rather than constructing an enum-typed one) and which ILC keeps the metadata for. The smoke test constructs both, so this is checked by the native binary rather than assumed.
- **Struct hashers, resolved at compile time.** Every hash-based collection is generic over `where THasher : struct, IHashProvider<T>`. The concrete hasher is a value type baked into the generic instantiation, so the AOT compiler emits a fully specialized, devirtualized `Hash()` call — there is no virtual dispatch to keep alive. The same pattern carries the struct comparers (`where TComparer : struct, IComparer<T>`) on the ordered collections and the struct monoids on `SegmentTree`.
- **AOT-safe BCL and runtime APIs only.** The hot paths use `MemoryMarshal`, `System.Runtime.CompilerServices.Unsafe`, `BitOperations`, `EqualityComparer<T>.Default`, `ArrayPool<T>.Shared`, the SIMD types (`Vector<T>`, `Vector128<T>`), `[InlineArray]` traversal buffers, and generic math (`INumber<T>`, `IMinMaxValue<T>`) — all fully supported under Native AOT. The generic-math constraints resolve through constrained calls that ILC specializes per type argument, and `RuntimeHelpers.IsReferenceOrContainsReferences<T>()` folds to a compile-time constant rather than a runtime branch.

## How it is enforced

Compatibility is not a one-time claim; it is checked on every build and in CI.

1. **Static analyzers.** All eight shipping packages set `<IsAotCompatible>true</IsAotCompatible>` — `Celerity.Collections`, `Celerity.Hashing`, `Celerity.Primitives`, `Celerity.Sorting`, `Celerity.Statistics`, `Celerity.Ring`, `Celerity.Sentinel` and `Celerity.Cardinality`. The switch marks each assembly trimmable and turns on the trim, AOT, and single-file Roslyn analyzers, so any reflection or trim-unsafe pattern introduced into any package becomes a build warning at compile time.
2. **End-to-end publish smoke test.** [`src/Celerity.AotSmokeTest`](../src/Celerity.AotSmokeTest) is a console app (`<PublishAot>true</PublishAot>`) that drives the family through roughly 2,900 lines and 667 runtime assertions. It references `Celerity.Collections`, `Celerity.Sorting`, `Celerity.Statistics`, `Celerity.Ring`, `Celerity.Sentinel` and `Celerity.Cardinality`, picking up `Celerity.Hashing` and `Celerity.Primitives` transitively — all eight shipping packages — and covers:

   - **All 51 collection types** in `Celerity.Collections` — the dictionary and set families in full (`EnumSet`, `SwissSet`, `RobinHoodSet`, `HashCachingSet`, `PooledCeleritySet` and `SmallSet` alongside their dictionary siblings), the frozen/perfect-hash pair, `CelerityMultiMap` / `CelerityMultiSet`, `LruCache`, `Deque`, `DisjointSet`, `IndexedPriorityQueue`, `SparseSet`, `CompressedIntSet`, `FenwickTree`, `SegmentTree`, the spatial indexes (`KdTree`, `SpatialGrid`, `RTree`, `IntervalTree`), `TimerWheel`, `CompressedGraph`, the text indexes (`SuffixArray`, `AhoCorasick`, `Trie`), the ordered `BTreeDictionary` / `BTreeSet` / `RankedSet`, `BitSet` / `RankSelectBitVector`, and the probabilistic structures (`BloomFilter`, `CuckooFilter`, `XorFilter`, `HyperLogLog`, `CountMinSketch`, `TopKSketch`).
   - **Every hasher in `Celerity.Hashing`** — all 43, across the integer, string, GUID and identity families. `DefaultHasher<T>` is one of the 43, and is the most AOT-sensitive of them because it routes through `EqualityComparer<T>.Default`. The `IHashProvider64<T>` dispatch is exercised alongside them.
   - **The whole `Celerity.Primitives` surface** — `FastUtils` (`FastMod` / `FastDiv`, `CountDigits` / `Log10`, the alignment helpers, `MinTableSizeFor`), all five struct PRNGs (`SplitMix64`, `Xoshiro256StarStar`, `Xoroshiro128Plus`, `WyRand`, `Pcg32`) and the constrained-generic `RandomSourceExtensions`, `VarInt`, `SpanBits`, `BitWriter` / `BitReader`, `FastGuid` / `GuidV7Generator`, `MortonCurve` / `HilbertCurve`, `SimdReductions`, `Branchless` and `SortedSpan`.
   - **`Celerity.Sorting`** — `RadixSort` (both radix widths, the signed and IEEE-754 key transforms, key+payload, argsort), `CountingSort` and both `PartialSort` comparer forms.
   - **`Celerity.Statistics`** — `DDSketch`, `ReservoirSampler` (over value- and reference-typed elements, and two generators) and `RunningStatistics`.
   - **The three showcase-tier packages** — `Celerity.Ring` (`ConsistentHashRing` and `RendezvousHash` plus their `String*` subclasses: weighted placement, the replica walk, and the removal that only remaps the departing node's keys), `Celerity.Sentinel` (`AbuseTracker` and `StripedAbuseTracker`, both arms of the optional first-seen filter, the report and the merge) and `Celerity.Cardinality` (`Distinct` on either side of its promotion from the exact set to HyperLogLog, and `DedupFilter`). Each closes the core sketches and collections over its own key type and hasher, so these are instantiations no other section roots.

   The `aot-publish` job in [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) Native-AOT-publishes this app on every push and pull request — once per shipped target framework (`net8.0`, `net9.0`, `net10.0`) via a matrix — and then runs each resulting native binary; a non-zero exit code fails the build. This forces the AOT compiler to compile every generic instantiation down to native code and proves the library behaves correctly under AOT, not just that the analyzers are satisfied.

   Coverage here is opt-in, not automatic: a type the smoke test never constructs is not rooted by the publish, whatever the analyzers say about it. Adding a collection means adding a section to `Program.cs`.

## Publishing a Native AOT app that uses Celerity

```bash
dotnet add package Celerity.Collections
dotnet publish -r linux-x64 -c Release
```

`Celerity.Collections` transitively pulls in `Celerity.Hashing` and `Celerity.Primitives`; `Celerity.Sorting`, `Celerity.Statistics` and the three showcase-tier packages are separate installs. Nothing about the AOT setup differs between them.

On Linux you need the AOT prerequisites (`clang` and `zlib1g-dev`); on Windows, the "Desktop development with C++" Visual Studio workload. See the [Native AOT prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites) for the full list.

## Not yet covered

- An **AOT-vs-JIT performance comparison** is not part of the benchmark suite yet; the live dashboard tracks JIT numbers only. [#32](https://github.com/marius-bughiu/Celerity/issues/32), the Native AOT issue this page came from, shipped and is closed — the comparison is not currently tracked by an open issue.
