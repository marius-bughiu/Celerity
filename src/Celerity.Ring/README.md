# Celerity.Ring

Deterministic **consistent-hashing** and **rendezvous (HRW)** rings for sharding and request routing — generic over your key type and a zero-cost, JIT-inlined [Celerity](https://github.com/marius-bughiu/Celerity) hasher.

```bash
dotnet add package Celerity.Ring
```

The BCL has no consistent-hashing type, so the status quo is "hand-roll a `SortedDictionary<uint, TNode>` + a hash, or take a stale NuGet." Celerity.Ring fills that gap and adds the property a sharded fleet actually needs: **byte-identical key→node assignment across processes, runtimes, and CPU architectures** (x64 / arm64 / Blazor WASM). A ring built from `string.GetHashCode()` reshards silently between processes because that hash is randomized per run; Celerity.Ring routes through a *specified* deterministic hash (`StringXxHash3Hasher`) and pure integer mixing — no randomized hash, no `float`/`log`, no endianness-dependent step.

## Consistent-hash ring

```csharp
using Celerity.Ring;

var ring = new StringConsistentHashRing<string>();   // string keys, deterministic StringXxHash3Hasher
ring.Add("cache-a", "10.0.0.1");
ring.Add("cache-b", "10.0.0.2");
ring.Add("cache-c", "10.0.0.3", weight: 2);          // twice the virtual nodes -> ~2x the keys

string owner = ring.GetNode("user:42");              // single node for this key
var replicas = ring.GetReplicas("user:42", 3);       // primary + 2 distinct successors

ring.Remove("cache-b");                              // only cache-b's ~1/N keys remap
```

Each node is placed at `VirtualNodesPerNode` (default 160) positions on a `[0, 2^32)` ring, so load is even and a departing node hands its keys to many successors instead of one. Adding/removing a node remaps only about `1/NodeCount` of keys — versus the near-total reshuffle of `hash % nodeCount`.

## Rendezvous (HRW) hash

No ring array, nothing to rebuild on membership change — ideal for small, churning clusters:

```csharp
var pool = new StringRendezvousHash<string>();
pool.Add("node-1", "10.0.0.1");
pool.Add("node-2", "10.0.0.2");

string owner = pool.GetNode("tenant:7");             // highest-scoring node for this key
var ranked = pool.GetReplicas("tenant:7", 2);        // ranked preference list
```

Scoring is pure-integer `max` (weights are realized as integer sub-labels), so HRW stays deterministic across platforms with no floating-point `log`. A lookup is `O(NodeCount)` scaled by total weight.

## Generic over your key type

Both types are generic over `TKey` and a struct `IHashProvider<TKey>` the JIT inlines on the routing hot path:

```csharp
using Celerity.Hashing;

var byId  = new ConsistentHashRing<Endpoint, long, Int64WangNaiveHasher>();
var byGuid = new RendezvousHash<Endpoint, Guid, GuidHasher>();
```

For cross-process agreement, use a **specified deterministic** hasher (`StringXxHash3Hasher`, `GuidHasher`, an integer hasher) — **not** `DefaultHasher<string>`, which delegates to the per-run-randomized BCL string hash.

## Concurrency

Reads (`GetNode` / `TryGetNode` / `GetReplicas`) are **lock-free**: each mutation publishes an immutable snapshot with a single volatile write, so a reader always sees a consistent topology. Mutations (`Add` / `Remove`) must be serialized by the caller.

## Why managed (and not a native binding)

`GetNode(key)` is one chatty call over one small managed key on the request hot path. Binding a native ring (libketama) means marshaling the key on every lookup, it can only hash byte buffers (not your inlined generic hasher), and it needs a per-RID native binary that Blazor WASM / Native AOT / IL2CPP can't load. Determinism across a heterogeneous fleet is a **correctness** requirement a randomized `GetHashCode` and a per-arch `.so` cannot meet. A pure-managed ring runs identically on every RID with no native dependency.

Part of the [Celerity](https://github.com/marius-bughiu/Celerity) family.
