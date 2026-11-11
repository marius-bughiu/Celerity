# Celerity.Ring

Deterministic **consistent-hashing**, **rendezvous (HRW)** and **Maglev** routing for sharding and request routing — generic over your key type and a zero-cost, JIT-inlined [Celerity](https://github.com/marius-bughiu/Celerity) hasher.

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

Each position is a 64-bit mix of the node's hash and the virtual-node index, so two nodes' positions are independent. The one exception is two node ids whose 32-bit hashes collide outright. They share the position of every virtual-node index both have, and the ordinally smaller id wins each one, so at equal weights the later id gets no keys.

> **Placement changed after v3.2.0** ([#459](https://github.com/marius-bughiu/Celerity/issues/459)). A ring's layout is redrawn, so about `(N − 1)/N` of its keys change owner — most of them in any real fleet. A rendezvous pool whose nodes all have weight 1 routes as before, but once any node has a higher weight, keys can move to or from any node, weight-1 nodes included, because their scores are compared with the weighted node's changed ones. Processes on either side of that change route differently, so do not let them share a fleet's routing during a rolling upgrade.

## Rendezvous (HRW) hash

No ring array: a membership change re-sorts the node identities and rebuilds the lookup snapshot
in `O(NodeCount log NodeCount)`, which suits small, churning clusters:

```csharp
var pool = new StringRendezvousHash<string>();
pool.Add("node-1", "10.0.0.1");
pool.Add("node-2", "10.0.0.2");

string owner = pool.GetNode("tenant:7");             // highest-scoring node for this key
var ranked = pool.GetReplicas("tenant:7", 2);        // ranked preference list
```

Scoring is pure-integer `max` (weights are realized as integer sub-labels), so HRW stays deterministic across platforms with no floating-point `log`. A lookup is `O(NodeCount)` scaled by total weight.

## Maglev hash

The lookup-table scheme load balancers use (Eisenbud et&#160;al., *Maglev*, NSDI 2016). A prime-sized table is filled by letting every node walk its own permutation of the slots in turn, so a key routes with **one hash, one modulo and one array load**, and every node owns its weighted share of the table to within one round of the fill (to within one slot when all weights are 1):

```csharp
var lb = new StringMaglevHash<string>();             // 65,537-slot table
lb.Add("backend-a", "10.0.0.1");
lb.Add("backend-b", "10.0.0.2");
lb.Add("backend-c", "10.0.0.3", weight: 2);          // two turns per round -> ~2x the slots

string owner = lb.GetNode("flow:10.1.2.3:443");      // table[hash(key) mod 65,537]
var backups = lb.GetReplicas("flow:10.1.2.3:443", 3); // owner, then the next distinct nodes in the table
```

The table size must be a **prime** no smaller than the total weight; keep it at **100 or more slots per node** (65,537 covers ~650 weight-1 nodes; pass `655_373` or `6_553_577` for larger fleets). Against a hand-rolled ring of 160 virtual nodes per node with the same key hash, `GetNode` is **3.6x faster at 10 nodes, 5.1x at 100 and 7.0x at 1,000**, with no allocation, because the ring's binary search deepens with the fleet and the table lookup does not.

What it costs, so you can decide:

- **Every membership change refills the whole table**: `O(M log M)` and a fresh `4·M`-byte array. One node leaving and rejoining takes **1.4 ms at 10 nodes, where a careful ring that caches each node's positions takes 5 µs (290x)**, and 1.9 ms at 100 (28x). Only at 1,000 nodes, where the ring's arrays have grown to 160,000 entries, does Maglev win (3.2 ms against 8.7 ms). Fine for a fleet that changes every few seconds; wrong for one that changes on every request.
- **Disruption is near-minimal, not minimal.** A departing node's keys move, as on any consistent hash, and a small share of everyone else's move too because the refill lands differently: removing one of 100 nodes from a 65,537-slot table moved about 0.6% of the other nodes' keys, against the ring's 0%. If a moved key is expensive (a cold cache, a migrated shard), use the ring or rendezvous.
- **Only the owner carries Maglev's guarantees.** `GetReplicas` walks forward through the table from the key's slot, so backups are well spread, but a backup set can change when an unrelated node joins.

### Which one?

| You need | Use |
|---|---|
| The fastest lookup and the tightest balance, with membership that changes rarely (load balancing, connection routing, a cache tier behind a hot path) | `MaglevHash` |
| Strictly minimal key movement on every change, or a stable replica chain per key | `ConsistentHashRing` |
| A small, churning pool where a rebuild must be cheap and every key's ranked node list must be stable | `RendezvousHash` |

## Replica sets without allocating

`GetReplicas(key, count)` returns a fresh array. A replicated store asks for a replica set on every write and every quorum read, so all three types also take a caller-owned buffer — its length is the replica count, and the return value is how many nodes were written (fewer when the cluster is smaller):

```csharp
var replicaSet = new string[3];                      // owned by the caller, reused across requests
int written = ring.GetReplicas("user:42", replicaSet);
foreach (string node in replicaSet.AsSpan(0, written))
    Send(node);
```

Both overloads return the same nodes in the same order. The span form allocates nothing up to 1,024 physical nodes on the ring and on Maglev, and up to 32 replicas on the rendezvous hash, where its scratch lives on the stack; past either it rents from `ArrayPool<T>.Shared`, which allocates only when the pool has no buffer to lend. Rendezvous scores every node once but ranks only the replicas asked for, so a short preference list from a large pool costs little more than `GetNode`.

## Generic over your key type

All three types are generic over `TKey` and a struct `IHashProvider<TKey>` the JIT inlines on the routing hot path:

```csharp
using Celerity.Hashing;

var byId  = new ConsistentHashRing<Endpoint, long, Int64WangNaiveHasher>();
var byGuid = new RendezvousHash<Endpoint, Guid, GuidHasher>();
var byFlow = new MaglevHash<Endpoint, ulong, UInt64WangHasher>(tableSize: 655_373);
```

For cross-process agreement, use a **specified deterministic** hasher (`StringXxHash3Hasher`, `GuidHasher`, an integer hasher) — **not** `DefaultHasher<string>`, which delegates to the per-run-randomized BCL string hash.

## Concurrency

Reads (`GetNode` / `TryGetNode` / `GetReplicas`) are **lock-free**: each mutation publishes an immutable snapshot with a single volatile write, so a reader always sees a consistent topology. Mutations (`Add` / `Remove`) must be serialized by the caller.

## Why managed (and not a native binding)

`GetNode(key)` is one chatty call over one small managed key on the request hot path. Binding a native ring (libketama) means marshaling the key on every lookup, it can only hash byte buffers (not your inlined generic hasher), and it needs a per-RID native binary that Blazor WASM / Native AOT / IL2CPP can't load. Determinism across a heterogeneous fleet is a **correctness** requirement a randomized `GetHashCode` and a per-arch `.so` cannot meet. A pure-managed ring runs identically on every RID with no native dependency.

Part of the [Celerity](https://github.com/marius-bughiu/Celerity) family.
