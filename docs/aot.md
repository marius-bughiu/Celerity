# Native AOT & trimming

Celerity is compatible with [Native AOT](https://learn.microsoft.com/dotnet/core/deploying/native-aot/) compilation and [trimming](https://learn.microsoft.com/dotnet/core/deploying/trimming/trimming-options). You can reference `Celerity.Collections` from a `PublishAot` or trimmed application and it will produce no `IL2xxx` (trim) or `IL3xxx` (AOT) warnings.

## Why it works

The design choices that make Celerity fast also make it AOT-friendly:

- **No reflection.** The library contains no `System.Reflection` usage, no `Activator.CreateInstance`, no `Type.MakeGenericType`, no `System.Linq.Expressions`, and no IL emit. Nothing depends on metadata that the trimmer might remove or that the AOT compiler cannot resolve statically.
- **Struct hashers, resolved at compile time.** Every collection is generic over `where THasher : struct, IHashProvider<T>`. The concrete hasher is a value type baked into the generic instantiation, so the AOT compiler emits a fully specialized, devirtualized `Hash()` call — there is no virtual dispatch to keep alive.
- **AOT-safe BCL primitives only.** The hot paths use `MemoryMarshal.GetArrayDataReference`, `System.Runtime.CompilerServices.Unsafe`, and `EqualityComparer<T>.Default` (in `DefaultHasher<T>` and the generic `CelerityDictionary` key comparison). All three are fully supported under Native AOT.

## How it is enforced

Compatibility is not a one-time claim; it is checked on every build and in CI.

1. **Static analyzers.** The library project sets `<IsAotCompatible>true</IsAotCompatible>`, which marks the assembly trimmable and turns on the trim, AOT, and single-file Roslyn analyzers. Any reflection or trim-unsafe pattern introduced into the library becomes a build warning at compile time.
2. **End-to-end publish smoke test.** [`src/Celerity.AotSmokeTest`](../src/Celerity.AotSmokeTest) is a console app (`<PublishAot>true</PublishAot>`) that constructs every collection shape (`IntDictionary`, `LongDictionary`, `CelerityDictionary`, `IntSet`, `LongSet`, `CeleritySet`) and a representative spread of hashers (`GuidHasher`, `StringMurmur3Hasher`, `StringFnV1AHasher`, `DefaultHasher<T>`, `UInt32Hasher`, `UInt64Hasher`, the Murmur3 / Wang integer hashers), runs the core operations against them, and asserts the results. The `aot-publish` job in [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) Native-AOT-publishes this app on every push and pull request and then runs the resulting native binary — a non-zero exit code fails the build. This forces the AOT compiler to compile every generic instantiation down to native code and proves the collections behave correctly under AOT, not just that the analyzers are satisfied.

## Publishing a Native AOT app that uses Celerity

```bash
dotnet add package Celerity.Collections
dotnet publish -r linux-x64 -c Release
```

On Linux you need the AOT prerequisites (`clang` and `zlib1g-dev`); on Windows, the "Desktop development with C++" Visual Studio workload. See the [Native AOT prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites) for the full list.

## Not yet covered

- An **AOT-vs-JIT performance comparison** is not part of the benchmark suite yet; the live dashboard tracks JIT numbers only. Tracked under [#32](https://github.com/marius-bughiu/Celerity/issues/32).
