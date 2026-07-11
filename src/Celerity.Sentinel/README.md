# Celerity.Sentinel

Streaming **abuse / heavy-hitter detection in fixed memory** — flag the busiest IPs, tokens, or request fingerprints no matter how many distinct keys the stream contains. Built on [Celerity](https://github.com/marius-bughiu/Celerity)'s probabilistic sketches, generic over your key type and a zero-cost, JIT-inlined hasher.

```bash
dotnet add package Celerity.Sentinel
```

## The categorical win: survives instead of crashing

A naive `Dictionary<string,int>` (or `ConcurrentDictionary`) request counter stores one entry per distinct key — so an attacker rotating through millions of keys grows it without bound until the process OOMs. Celerity.Sentinel is sized **once** (a couple of MB at the defaults) and **never grows with cardinality**, so it survives exactly the adversarial input that kills the exact counter — while still surfacing the true heavy hitters.

One `Observe(key)` fans the key into four bounded sketches:

| Signal | Structure | Guarantee |
|---|---|---|
| per-key **rate** | `CountMinSketch` | never underestimates |
| **top offenders** | `TopKSketch` (Space-Saving) | `O(k)` memory; never misses a key above `Total / OffenderCapacity` |
| **distinct volume** | `HyperLogLog` | ~0.8% error from 16 KB |
| **first-seen** (new key) | `BloomFilter` | no false negatives |

## Usage

```csharp
using Celerity.Sentinel;

var sentinel = new StringAbuseTracker();   // string keys, StringXxHash3Hasher

foreach (var request in requests)
{
    ObservationResult r = sentinel.Observe(request.ClientIp);
    if (r.IsFirstSeen) ApplyNewClientFriction(request);
    if (r.EstimatedCount > threshold) Throttle(request);
}

AbuseReport<string> report = sentinel.Snapshot(topN: 20);
foreach (Offender<string> o in report.Offenders)
    Console.WriteLine($"{o.Key}: ~{o.EstimatedCount} (±{o.Error})");
Console.WriteLine($"{report.DistinctKeys:N0} distinct clients, {report.TotalObservations:N0} requests");
```

Generic over any key type:

```csharp
var byId = new AbuseTracker<long, Int64WangNaiveHasher>();
```

## Concurrency: per-core striping + merge

A single tracker is single-threaded (like every Celerity collection). For real edge QPS, use `StripedAbuseTracker` — independent lanes with no lock on the observe path, rolled up on demand:

```csharp
var striped = new StringStripedAbuseTracker(laneCount: Environment.ProcessorCount);

// each producer observes into a lane it exclusively owns:
striped.Observe(coreIndex, request.ClientIp);

// a coordinator periodically rolls the lanes up (quiesce or coordinate first):
AbuseReport<string> report = striped.Snapshot(topN: 20);
```

Merges are **exact** for rate / distinct / first-seen and use the standard Space-Saving approximation for offenders. The same `Merge` primitive rolls up per-shard trackers across a fleet.

## Windowing

Rate and offender counts are cumulative. For a time-windowed rate, reset on a tumbling interval with `Clear()` (or swap in a fresh instance).

## Why managed (and not a native binding / Redis)

The update path is maximally chatty — one or more sketch bumps per request over a managed key hashed by an inlined struct hasher. There is no in-process native sketch library to bind, and RedisBloom is an out-of-process network hop *per update* that cannot keep pace at edge QPS. The value — bounded, GC-friendly memory under adversarial key rotation, generic over the caller's fingerprint type — is something a native library working over flat buffers cannot express.

Part of the [Celerity](https://github.com/marius-bughiu/Celerity) family.
