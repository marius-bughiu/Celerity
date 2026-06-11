# Celerity.Primitives

Low-level, allocation-free primitives that fill genuine BCL gaps. Part of the
[Celerity](https://github.com/marius-bughiu/Celerity) family of high-performance
.NET libraries.

Every type here ships only because it beats its BCL counterpart on a documented
workload — Celerity deliberately does **not** reimplement what
`System.Numerics.BitOperations` / `TensorPrimitives` already inline.

## What's in the box

- **`FastUtils.FastMod` / `FastDiv`** — Lemire reciprocal modulo & division by a
  runtime-constant divisor; 2–4× over `%` / `/` for repeated mod by the same
  divisor (hash buckets, ring buffers, sharding). The BCL's equivalent is
  `internal`-only.
- **`FastUtils.CountDigits` / `Log10`** — exact integer base-10 digit count for
  buffer sizing and column alignment (the BCL's LZCNT-based one is `internal`;
  `Math.Log10` mis-rounds at exact powers of ten).
- **`FastUtils.NextPowerOfTwo`** — rounds up to the next power of two.
- **Struct PRNGs** — value-type, seed-deterministic `SplitMix64`,
  `Xoshiro256StarStar`, `Xoroshiro128Plus`, `WyRand`, `Pcg32`, all implementing
  the one-method `IRandomSource`, with a zero-cost `RandomSourceExtensions`
  surface (`NextDouble`/`NextInt`/`NextBytes`/…). No heap, no virtual dispatch,
  no legacy seeded fallback like `System.Random`.
- **`VarInt`** — span-based LEB128 + zig-zag `Try(Write|Read)` over
  `Span<byte>`, bounds-safe and allocation-free (the BCL's 7-bit codec is bound
  to `BinaryReader`/`BinaryWriter`).
- **`FastGuid` / `GuidV7Generator`** — fast non-crypto random GUID v4 and
  RFC 9562 **big-endian** v7 (sortable, DB-index-friendly). *Not* for security —
  use `Guid.NewGuid()` for unguessable IDs.

See the [utilities API reference](https://github.com/marius-bughiu/Celerity/blob/main/docs/api/utilities.md)
for full docs and runnable examples.

## License

MIT
