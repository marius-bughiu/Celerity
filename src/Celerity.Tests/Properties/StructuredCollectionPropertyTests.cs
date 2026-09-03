using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Properties;

// Issue #416 — the property-test roster covered only the hash-table family.
//
// CollectionModelPropertyTests.cs holds the flat, hash-shaped half of the library: every type there models
// as a Dictionary or a HashSet, and the sequence of operations matters only through resize and deletion.
// This file holds the other half — the tree, the graph, the text structures and the timer wheel — where the
// answer depends on how the structure got into its current shape, not only on what it currently holds:
//
//   RankedSet        sqrt-decomposed buckets with a Fenwick tree over their lengths
//   Rope             an AVL tree of character leaves, rebalanced and defragmented as it is edited
//   TimerWheel       hierarchical slots, with entries cascading down a level as the clock advances
//   CompressedGraph  a build-once CSR layout, read back through binary search
//   SuffixArray      prefix doubling, whose failure mode is a plausible order a few suffixes out
//   AhoCorasick      failure and output links, whose failure mode is losing only the nested matches
//
// Each of these already has a Celerity.Fuzz target, but that is a nightly soak job — these run on every
// pull request, and CsCheck shrinks a failure to a minimal reproduction and prints the seed for replay.
// The generated domains are deliberately narrow (tiny alphabets, small key ranges, small wheels) so that
// the interesting cases — a suffix that parts late, a pattern nested inside another, a cascade across
// levels, an edit landing on a leaf boundary — are the norm rather than a rarity.
public class StructuredCollectionPropertyTests
{
    // A three-letter alphabet, for the reason the text structures need one: it makes long agreeing runs the
    // common case, which is where prefix doubling and the failure links actually go wrong.
    private const int Alphabet = 3;

    private static readonly Gen<string> GenText =
        Gen.Int[0, Alphabet - 1].List[0, 60].Select(Render);

    private static string Render(List<int> letters)
    {
        var text = new StringBuilder(letters.Count);
        foreach (int letter in letters)
            text.Append((char)('a' + letter));

        return text.ToString();
    }

    // ---- RankedSet vs SortedSet<T> + List<T> --------------------------------

    // The oracle is the pair a caller writes instead: a SortedSet for membership and order, and the list it
    // enumerates into for rank and select. What is on trial is everything between them — the bucket a value
    // lands in, the offset inside it, and the Fenwick tree that turns the two into a global rank. A bug there
    // is invisible to Contains and shows up only when a rank is asked for, so every assertion below is
    // reconciled against the oracle's *position*, both directions round: element i of the sorted sequence
    // must be at rank i, and IndexOf of that element must be i.
    private enum RankedOp { Add, TryAdd, Remove, RemoveAt, TrimExcess, Clear }

    private static readonly Gen<List<(RankedOp op, int item)>> GenRankedOps =
        Gen.Select(
            Gen.Int[0, 99].Select(n => n < 40 ? RankedOp.Add
                                     : n < 55 ? RankedOp.TryAdd
                                     : n < 80 ? RankedOp.Remove
                                     : n < 92 ? RankedOp.RemoveAt
                                     : n < 97 ? RankedOp.TrimExcess
                                     : RankedOp.Clear),
            Gen.Int[-4, 40])
        .Select((op, item) => (op, item))
        .List[0, 200];

    [Fact]
    public void RankedSet_ShouldMatch_SortedSetModel()
    {
        GenRankedOps.Sample(ops =>
        {
            var sut = new RankedSet<int>();
            var oracle = new SortedSet<int>();

            foreach ((RankedOp op, int item) in ops)
            {
                switch (op)
                {
                    case RankedOp.Add:
                        // The throwing overload, whose contract is exactly "TryAdd, but loudly".
                        if (oracle.Add(item))
                            sut.Add(item);
                        else
                            Assert.Throws<ArgumentException>(() => sut.Add(item));
                        break;
                    case RankedOp.TryAdd:
                        Assert.Equal(oracle.Add(item), sut.TryAdd(item));
                        break;
                    case RankedOp.Remove:
                        Assert.Equal(oracle.Remove(item), sut.Remove(item));
                        break;
                    case RankedOp.RemoveAt:
                    {
                        if (oracle.Count == 0)
                            break;

                        // The item doubles as the rank, folded into range — removal by rank is the operation
                        // no BCL ordered container offers at all, so the oracle has to be told which element
                        // that rank named.
                        int rank = (int)((uint)item % (uint)oracle.Count);
                        int removed = sut[rank];
                        sut.RemoveAt(rank);
                        Assert.True(oracle.Remove(removed));
                        break;
                    }
                    case RankedOp.TrimExcess:
                        // A rebuild of every bucket and the index over them, which must leave the set
                        // observably identical — the one thing an oracle can check about it.
                        sut.TrimExcess();
                        break;
                    case RankedOp.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                }
            }

            AssertRankedEquivalent(sut, oracle, -6, 42);
        }, iter: 1_000);
    }

    // The same model at a size that splits a bucket. A bucket holds 512 elements before it divides, so the
    // property above — over a domain of 45 values — never reaches SplitBucket, the shift of every later slot,
    // or the Fenwick rebuild behind them, and neither does the fuzz target, whose domain is narrower still.
    // This one inserts past that boundary in a random order, then deletes back through it, so the structural
    // half of the type is reached by a generated sequence rather than only by a fixed-size fixture.
    [Fact]
    public void RankedSet_ShouldMatch_SortedSetModel_AcrossABucketSplit()
    {
        Gen.Int[0, 2_999].List[1_200, 1_600].Sample(items =>
        {
            var sut = new RankedSet<int>();
            var oracle = new SortedSet<int>();

            foreach (int item in items)
                Assert.Equal(oracle.Add(item), sut.TryAdd(item));

            AssertRankedEquivalent(sut, oracle, sampleOnly: true);

            // Delete roughly half, taken from alternating ends so buckets empty unevenly and the removal has
            // to drop one and shift the rest rather than always shrinking the last.
            var order = items.Distinct().ToList();
            for (int i = 0; i < order.Count; i += 2)
                Assert.Equal(oracle.Remove(order[i]), sut.Remove(order[i]));

            AssertRankedEquivalent(sut, oracle, sampleOnly: true);

            sut.TrimExcess();
            AssertRankedEquivalent(sut, oracle, sampleOnly: true);
        }, iter: 20);
    }

    private static void AssertRankedEquivalent(RankedSet<int> sut, SortedSet<int> oracle, int from, int to)
    {
        AssertRankedEquivalent(sut, oracle, sampleOnly: false);

        // The whole probe domain, one past each end, so the "orders after every element" and "orders before
        // the first" branches of the bucket search are both reached.
        for (int k = from; k <= to; k++)
        {
            Assert.Equal(oracle.Contains(k), sut.Contains(k));
            Assert.Equal(oracle.Count(v => v < k), sut.CountLessThan(k));
            Assert.Equal(oracle.Count(v => v <= k), sut.CountLessThanOrEqual(k));

            int? lower = oracle.Where(v => v >= k).Cast<int?>().FirstOrDefault();
            Assert.Equal(lower.HasValue, sut.TryGetLowerBound(k, out int lowerBound));
            if (lower.HasValue) Assert.Equal(lower.Value, lowerBound);

            int? upper = oracle.Where(v => v > k).Cast<int?>().FirstOrDefault();
            Assert.Equal(upper.HasValue, sut.TryGetUpperBound(k, out int upperBound));
            if (upper.HasValue) Assert.Equal(upper.Value, upperBound);

            Assert.Equal(
                oracle.Where(v => v >= k && v < k + 9).ToList(),
                sut.EnumerateRange(k, k + 9).ToList());
        }
    }

    private static void AssertRankedEquivalent(RankedSet<int> sut, SortedSet<int> oracle, bool sampleOnly)
    {
        Assert.Equal(oracle.Count, sut.Count);
        Assert.Equal(oracle.ToList(), sut.ToList());

        Assert.Equal(oracle.Count > 0, sut.TryGetMin(out int min));
        Assert.Equal(oracle.Count > 0, sut.TryGetMax(out int max));
        if (oracle.Count > 0)
        {
            Assert.Equal(oracle.Min, min);
            Assert.Equal(oracle.Max, max);
            Assert.Equal(oracle.Min, sut.Min);
            Assert.Equal(oracle.Max, sut.Max);
        }

        // Rank and select, reconciled against the oracle's own order in both directions. Over a large set
        // only a sample of ranks is checked, since the point there is the bucket boundaries rather than
        // exhaustiveness — and a wrong bucket misplaces a whole run, not one element.
        var sorted = oracle.ToList();
        int step = sampleOnly ? Math.Max(1, sorted.Count / 200) : 1;
        for (int index = 0; index < sorted.Count; index += step)
        {
            Assert.Equal(sorted[index], sut[index]);
            Assert.Equal(index, sut.IndexOf(sorted[index]));
        }
    }

    // ---- Rope vs string ------------------------------------------------------

    // The oracle is a plain string rebuilt after every edit — quadratic, and deliberately so: it shares no
    // code with the rope and has no state of its own to get wrong. Rope is the only mutable tree in the
    // library, so its bugs are path-dependent — a rebalance, a leaf split or the amortized defragmenting
    // rebuild goes wrong for a particular *history* of edits, and surfaces many operations later as a
    // character read from the wrong leaf. Chunk sizes are drawn from MinChunkSize upwards so a case of a few
    // hundred characters builds a tree several levels deep; at the shipped 512-character default the same
    // case would never leave the root leaf.
    private enum RopeOp { InsertText, InsertChar, Append, Remove, Split, Join, TrimExcess, Clear }

    private static readonly Gen<(int chunkSize, List<(RopeOp op, int index, string text)> ops)> GenRopeOps =
        Gen.Select(
            Gen.Int[Rope.MinChunkSize, Rope.MinChunkSize + 24],
            Gen.Select(
                Gen.Int[0, 99].Select(n => n < 26 ? RopeOp.InsertText
                                         : n < 40 ? RopeOp.InsertChar
                                         : n < 54 ? RopeOp.Append
                                         : n < 78 ? RopeOp.Remove
                                         : n < 84 ? RopeOp.Split
                                         : n < 90 ? RopeOp.Join
                                         : n < 98 ? RopeOp.TrimExcess
                                         : RopeOp.Clear),
                Gen.Int[0, 999],
                GenText)
            .Select((op, index, text) => (op, index, text))
            .List[0, 90])
        .Select((chunkSize, ops) => (chunkSize, ops));

    [Fact]
    public void Rope_ShouldMatch_StringModel()
    {
        GenRopeOps.Sample(input =>
        {
            (int chunkSize, List<(RopeOp op, int index, string text)> ops) = input;

            var sut = new Rope(chunkSize);
            string oracle = string.Empty;

            foreach ((RopeOp op, int rawIndex, string text) in ops)
            {
                // Folded into range rather than generated in it, so that a shrunk index still names a valid
                // position after the shrinking has changed the length it was drawn against. Both ends are
                // reachable: an insert at Length, a remove of nothing and a split at either end are all
                // documented no-ops, and a no-op that is not one is exactly what a stray version bump makes.
                int index = oracle.Length == 0 ? 0 : rawIndex % (oracle.Length + 1);

                switch (op)
                {
                    case RopeOp.InsertText:
                        sut.Insert(index, text);
                        oracle = oracle.Insert(index, text);
                        break;
                    case RopeOp.InsertChar:
                    {
                        char value = text.Length > 0 ? text[0] : 'a';
                        sut.Insert(index, value);
                        oracle = oracle.Insert(index, value.ToString());
                        break;
                    }
                    case RopeOp.Append:
                        sut.Append(text);
                        oracle += text;
                        break;
                    case RopeOp.Remove:
                    {
                        // Bounded to a window rather than the whole tail: a remove that can take everything
                        // after `index` truncates instead of editing, and a case whose length keeps
                        // collapsing never builds a tree more than a level or two deep. The window still
                        // spans several chunks, so a remove can swallow whole leaves and force a merge.
                        int count = text.Length % (Math.Min(oracle.Length - index, chunkSize * 3) + 1);
                        sut.Remove(index, count);
                        oracle = oracle.Remove(index, count);
                        break;
                    }
                    case RopeOp.Split:
                    {
                        // Split and AppendAndClear are inverses, and the pair rewires the tree wholesale
                        // rather than along one path. The round trip is checked before the tail is either put
                        // back or dropped, so a later edit may run over a tree built by a join.
                        Rope tail = sut.Split(index);
                        Assert.Equal(oracle[..index], sut.ToString());
                        Assert.Equal(oracle[index..], tail.ToString());
                        Assert.Equal(chunkSize, tail.ChunkSize);

                        // Rejoining is the common case for the reason the remove above is bounded: dropping
                        // the tail truncates, and a case that keeps truncating never gets deep.
                        if (text.Length % 4 != 0)
                        {
                            sut.AppendAndClear(tail);
                            Assert.Equal(0, tail.Length);
                        }
                        else
                        {
                            oracle = oracle[..index];
                        }

                        break;
                    }
                    case RopeOp.Join:
                    {
                        // A source built independently, so the leaves the join adopts are ones this tree
                        // never laid out — the case the equal-chunk-size rule exists to make safe.
                        var source = new Rope(text, chunkSize);
                        sut.AppendAndClear(source);
                        oracle += text;
                        Assert.Equal(0, source.Length);
                        break;
                    }
                    case RopeOp.TrimExcess:
                        sut.TrimExcess();
                        break;
                    case RopeOp.Clear:
                        sut.Clear();
                        oracle = string.Empty;
                        break;
                }

                Assert.Equal(oracle.Length, sut.Length);

                // The path buffer the enumerators walk is a fixed-size inline array with no runtime guard, so
                // an unbalanced tree would corrupt memory rather than assert. This is the only place the
                // bound is observable, and 48 is the type's own MaxDepth.
                Assert.True(sut.Depth <= 48, $"Depth {sut.Depth} exceeded the enumerators' path buffer");
            }

            Assert.Equal(oracle, sut.ToString());

            // Three independent read paths over the same tree: the indexer seeks per character, the
            // enumerator walks the leaves in order, and GetChunks hands out the leaf buffers untouched.
            for (int i = 0; i < oracle.Length; i++)
                Assert.Equal(oracle[i], sut[i]);

            Assert.Equal(oracle, new string(sut.ToArray()));

            var chunked = new StringBuilder(oracle.Length);
            foreach (ReadOnlySpan<char> chunk in sut.GetChunks())
            {
                Assert.True(chunk.Length > 0, "GetChunks yielded an empty chunk");
                Assert.True(chunk.Length <= chunkSize, "GetChunks yielded a chunk wider than the chunk size");
                chunked.Append(chunk);
            }

            Assert.Equal(oracle, chunked.ToString());

            // The seek-then-scan reads are sampled rather than exhaustive: each is O(n) on its own, so
            // checking every start would make the case quadratic in a length the generator lets reach a few
            // thousand. Both ends are always included, since those are the boundary cases.
            foreach (int start in SamplePositions(oracle.Length))
            {
                for (int letter = 0; letter < Alphabet; letter++)
                {
                    char value = (char)('a' + letter);
                    Assert.Equal(oracle.IndexOf(value, start), sut.IndexOf(value, start));
                }

                int count = oracle.Length - start;
                Assert.Equal(oracle.Substring(start, count), sut.ToString(start, count));

                var copied = new char[count];
                sut.CopyTo(start, copied, count);
                Assert.Equal(oracle.Substring(start, count), new string(copied));
            }
        }, iter: 1_000);
    }

    // At most 17 positions spread over [0, length], always including both ends.
    private static IEnumerable<int> SamplePositions(int length)
    {
        int step = Math.Max(1, (length / 16) + 1);
        for (int position = 0; position < length; position += step)
            yield return position;

        yield return length;
    }

    // ---- TimerWheel vs a list of deadlines -----------------------------------

    // The BCL ships no timer wheel, so the oracle is the list of pending deadlines a caller keeps instead,
    // scanned linearly on every advance. What is on trial is the hierarchy: a timer scheduled beyond the
    // first level's reach is parked on an outer wheel and has to *cascade* inwards as the clock approaches
    // it, and a cascade that drops or misplaces an entry fires it at the wrong tick — or never. The wheels
    // are generated small (as few as two slots, as few as one level) so the horizon is tiny and almost every
    // schedule lands beyond the innermost wheel, and advances mix single ticks with jumps past a full
    // revolution, which take different paths through the walk. Retired handles are kept and offered back, so
    // a stale one is resolved after its slot has been reused.
    private enum TimerOp { Schedule, ScheduleAt, Cancel, Tick, Jump, Clear }

    private static readonly Gen<(int slots, int levels, List<(TimerOp op, int a, int b)> ops)> GenTimerOps =
        Gen.Select(
            Gen.Int[1, 3].Select(n => 1 << n),
            Gen.Int[1, 3],
            Gen.Select(
                Gen.Int[0, 99].Select(n => n < 34 ? TimerOp.Schedule
                                         : n < 46 ? TimerOp.ScheduleAt
                                         : n < 62 ? TimerOp.Cancel
                                         : n < 80 ? TimerOp.Tick
                                         : n < 97 ? TimerOp.Jump
                                         : TimerOp.Clear),
                Gen.Int[0, 999],
                Gen.Int[0, 999])
            .Select((op, a, b) => (op, a, b))
            .List[0, 120])
        .Select((slots, levels, ops) => (slots, levels, ops));

    [Fact]
    public void TimerWheel_ShouldMatch_DeadlineListModel()
    {
        GenTimerOps.Sample(input =>
        {
            (int slots, int levels, List<(TimerOp op, int a, int b)> ops) = input;

            var sut = new TimerWheel<int>(slots, levels);
            long horizon = sut.Horizon;

            var live = new Dictionary<int, (TimerHandle Handle, long Deadline)>();
            var retired = new List<TimerHandle>();
            var fired = new List<int>();
            int next = 0;

            foreach ((TimerOp op, int a, int b) in ops)
            {
                switch (op)
                {
                    case TimerOp.Schedule:
                    {
                        long delay = a % horizon;
                        live[next] = (sut.Schedule(delay, next), sut.CurrentTick + delay);
                        next++;
                        break;
                    }
                    case TimerOp.ScheduleAt:
                    {
                        // The absolute overload takes the same deadlines by a different route, including the
                        // "due now" boundary that Schedule reaches only with a zero delay.
                        long deadline = sut.CurrentTick + (a % horizon);
                        live[next] = (sut.ScheduleAt(deadline, next), deadline);
                        next++;
                        break;
                    }
                    case TimerOp.Cancel:
                    {
                        if (live.Count == 0)
                            break;

                        int victim = live.Keys.ElementAt(a % live.Count);
                        Assert.True(sut.Cancel(live[victim].Handle));
                        retired.Add(live[victim].Handle);
                        live.Remove(victim);
                        break;
                    }
                    case TimerOp.Tick:
                    case TimerOp.Jump:
                    {
                        // A single tick crosses one slot; a jump past a full revolution visits every slot on
                        // a level instead of only the ones it crossed, and forces the outer wheels to cascade.
                        long jump = op == TimerOp.Tick ? a % 2 : b % (horizon + 1);
                        long target = sut.CurrentTick + jump;

                        fired.Clear();
                        int count = sut.Advance(target, fired!);

                        int[] due = [.. live.Where(t => t.Value.Deadline <= target).Select(t => t.Key).Order()];
                        Assert.Equal(due.Length, count);
                        Assert.Equal(due, fired.Order().ToArray());
                        Assert.Equal(target, sut.CurrentTick);

                        foreach (int id in due)
                        {
                            retired.Add(live[id].Handle);
                            live.Remove(id);
                        }

                        break;
                    }
                    case TimerOp.Clear:
                        foreach (var entry in live)
                            retired.Add(entry.Value.Handle);

                        sut.Clear();
                        live.Clear();
                        break;
                }

                Assert.Equal(live.Count, sut.Count);

                // A handle that has fired, been cancelled or been cleared away must stay dead even after its
                // slot has been handed to a later timer — the failure a generation counter exists to prevent.
                foreach (TimerHandle stale in retired)
                {
                    Assert.False(sut.TryGetDeadline(stale, out _));
                    Assert.False(sut.Cancel(stale));
                }

                foreach (var entry in live)
                {
                    Assert.True(sut.TryGetDeadline(entry.Value.Handle, out long deadline));
                    Assert.Equal(entry.Value.Deadline, deadline);
                }

                Assert.Equal(
                    live.Values.Select(t => t.Deadline).Order().ToList(),
                    sut.Select(timer => timer.Deadline).Order().ToList());

                Assert.Equal(
                    live.Keys.Order().ToList(),
                    sut.Select(timer => timer.Value).Order().ToList());
            }
        }, iter: 1_000);
    }

    // ---- CompressedGraph vs an adjacency map ---------------------------------

    // The oracle is the SortedSet-per-vertex adjacency map a caller writes instead, which has the same
    // edge-set semantics: duplicates collapse, and neighbours come back in ascending order. What the CSR
    // layout adds over that map is a single pair of flat arrays and an offset index, read back by binary
    // search — so the failure mode is an off-by-one in one vertex's slice that hands back a neighbour
    // belonging to its predecessor. Edge lists are generated dense against a small vertex count so most
    // vertices have several neighbours and duplicate edges are common, and a third of cases are oriented
    // low-to-high so the topological path is reached rather than always rejected as cyclic.
    private static readonly Gen<(int vertexCount, bool acyclic, List<(int a, int b)> edges)> GenGraph =
        Gen.Select(
            Gen.Int[1, 10],
            Gen.Int[0, 2].Select(n => n == 0),
            Gen.Select(Gen.Int[0, 9], Gen.Int[0, 9])
                .Select((a, b) => (a, b))
                .List[0, 40])
        .Select((vertexCount, acyclic, edges) => (vertexCount, acyclic, edges));

    [Fact]
    public void CompressedGraph_ShouldMatch_AdjacencyModel()
    {
        GenGraph.Sample(input =>
        {
            (int vertexCount, bool acyclic, List<(int a, int b)> raw) = input;

            var edges = new List<GraphEdge>();
            foreach ((int rawSource, int rawTarget) in raw)
            {
                int source = rawSource % vertexCount;
                int target = rawTarget % vertexCount;
                if (!acyclic)
                {
                    edges.Add(new GraphEdge(source, target));
                    continue;
                }

                // Orienting low-to-high makes the graph acyclic, but only if the self-loops go too — one is a
                // cycle on its own, and would make the whole "acyclic" half of the generator a no-op.
                if (source != target)
                    edges.Add(new GraphEdge(Math.Min(source, target), Math.Max(source, target)));
            }

            var sut = new CompressedGraph(vertexCount, edges);

            var oracle = new SortedSet<int>[vertexCount];
            for (int v = 0; v < vertexCount; v++)
                oracle[v] = [];

            foreach (GraphEdge edge in edges)
                oracle[edge.Source].Add(edge.Target);

            Assert.Equal(vertexCount, sut.VertexCount);
            Assert.Equal(oracle.Sum(targets => targets.Count), sut.EdgeCount);

            var flattened = new List<GraphEdge>();
            for (int v = 0; v < vertexCount; v++)
            {
                Assert.Equal(oracle[v].Count, sut.Degree(v));
                Assert.Equal(oracle[v].ToArray(), sut.Neighbors(v).ToArray());
                flattened.AddRange(oracle[v].Select(target => new GraphEdge(v, target)));

                for (int t = 0; t < vertexCount; t++)
                    Assert.Equal(oracle[v].Contains(t), sut.ContainsEdge(v, t));
            }

            // The indexer recovers a source by binary search over the offsets — a different path over the
            // same data than the per-vertex slice above, and the one an off-by-one shows up in first.
            Assert.Equal(flattened, sut.ToList());
            for (int i = 0; i < flattened.Count; i++)
                Assert.Equal(flattened[i], sut[i]);

            CompressedGraph reversed = sut.Reverse();
            Assert.Equal(sut.EdgeCount, reversed.EdgeCount);
            Assert.Equal(
                flattened.Select(e => new GraphEdge(e.Target, e.Source)).OrderBy(e => e.Source).ThenBy(e => e.Target).ToList(),
                reversed.ToList());

            for (int source = 0; source < vertexCount; source++)
            {
                int[] order = sut.GetBreadthFirstOrder(source);
                Assert.Equal(source, order[0]);
                Assert.Equal(order.Length, order.Distinct().Count());

                // The reachable set, which is what a traversal that skipped or over-reached would get wrong.
                Assert.Equal(ReachableFrom(oracle, source), order.ToHashSet());

                // And the *breadth-first* part of it, which a depth-first walk over the same edges would
                // satisfy the set check while failing: distance from the source must never decrease along
                // the order, so every tier is complete before the next one starts.
                Dictionary<int, int> distance = Distances(oracle, source);
                for (int i = 1; i < order.Length; i++)
                    Assert.True(distance[order[i - 1]] <= distance[order[i]], "the walk left a tier early");

                var buffer = new int[vertexCount];
                Assert.Equal(order.Length, sut.CopyBreadthFirstOrder(source, buffer));
                Assert.Equal(order, buffer[..order.Length]);
            }

            if (sut.TryGetTopologicalOrder(out int[] topological))
            {
                Assert.Equal(vertexCount, topological.Length);
                Assert.Equal(vertexCount, topological.Distinct().Count());

                var position = new int[vertexCount];
                for (int i = 0; i < topological.Length; i++)
                    position[topological[i]] = i;

                foreach (GraphEdge edge in flattened)
                    Assert.True(position[edge.Source] < position[edge.Target], $"{edge} runs backwards");
            }
            else
            {
                // A refusal has to be justified, or a type that always refused would pass. Peeling
                // zero-in-degree vertices must genuinely leave something behind.
                Assert.True(HasCycle(oracle), "the graph was acyclic but no topological order was produced");
            }
        }, iter: 1_000);
    }

    // Kahn's algorithm run as the oracle's own answer to "is there a cycle", which is the question
    // TryGetTopologicalOrder answers by failing.
    private static bool HasCycle(SortedSet<int>[] adjacency)
    {
        var inDegree = new int[adjacency.Length];
        foreach (SortedSet<int> targets in adjacency)
        {
            foreach (int target in targets)
                inDegree[target]++;
        }

        var ready = new Queue<int>(Enumerable.Range(0, adjacency.Length).Where(v => inDegree[v] == 0));
        int peeled = 0;
        while (ready.Count > 0)
        {
            peeled++;
            foreach (int target in adjacency[ready.Dequeue()])
            {
                if (--inDegree[target] == 0)
                    ready.Enqueue(target);
            }
        }

        return peeled < adjacency.Length;
    }

    private static HashSet<int> ReachableFrom(SortedSet<int>[] adjacency, int source) =>
        [.. Distances(adjacency, source).Keys];

    // Hop count from the source to every vertex it reaches — the tier a breadth-first walk must emit each
    // vertex in.
    private static Dictionary<int, int> Distances(SortedSet<int>[] adjacency, int source)
    {
        var distance = new Dictionary<int, int> { [source] = 0 };
        var queue = new Queue<int>();
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            int vertex = queue.Dequeue();
            foreach (int target in adjacency[vertex])
            {
                if (distance.TryAdd(target, distance[vertex] + 1))
                    queue.Enqueue(target);
            }
        }

        return distance;
    }

    // ---- SuffixArray vs the suffixes sorted as strings -----------------------

    // The oracle is the naive answer throughout: the suffixes sorted with an ordinal string comparison, the
    // longest common prefixes counted character by character, and every query resolved by testing the pattern
    // at every position. Prefix doubling does not fail by crashing — it fails by producing a plausible order
    // with a handful of suffixes one place out, which only happens when suffixes agree for a long stretch and
    // part late. The three-letter alphabet makes that the common case rather than a rarity.
    [Fact]
    public void SuffixArray_ShouldMatch_SortedSuffixModel()
    {
        // A shorter text than the other cases use, because the oracle's longest-repeat answer is quadratic
        // in it and the probe set below is every substring of the text.
        Gen.Select(Gen.Int[0, Alphabet - 1].List[0, 40].Select(Render), Gen.Int[0, Alphabet - 1].List[0, 5])
            .Sample((source, patternLetters) =>
            {
                var sut = new SuffixArray(source);

                Assert.Equal(source.Length, sut.Length);
                Assert.Equal(source, sut.Text.ToString());

                int[] expected = [.. Enumerable.Range(0, source.Length)
                    .OrderBy(start => source[start..], StringComparer.Ordinal)];
                Assert.Equal(expected, sut.ToArray());
                for (int rank = 0; rank < source.Length; rank++)
                    Assert.Equal(expected[rank], sut[rank]);

                for (int rank = 1; rank < source.Length; rank++)
                {
                    Assert.Equal(
                        CommonPrefixLength(source, expected[rank - 1], expected[rank]),
                        sut.LongestCommonPrefixes[rank]);
                }

                if (source.Length > 0)
                    Assert.Equal(0, sut.LongestCommonPrefixes[0]);

                // A generated pattern, plus every substring of the text, plus one letter the alphabet does
                // not contain — so the present, the absent and the empty pattern are all covered.
                // Bounded by the alphabet rather than by the text: over three letters there are at most
                // 3 + 9 + 27 + 81 distinct substrings of length 4 or less, however long the text runs.
                var patterns = new List<string> { string.Empty, "#", Render(patternLetters) };
                for (int start = 0; start < source.Length; start++)
                {
                    for (int length = 1; start + length <= source.Length && length <= 4; length++)
                        patterns.Add(source.Substring(start, length));
                }

                foreach (string pattern in patterns.Distinct(StringComparer.Ordinal))
                {
                    int[] occurrences = NaiveOccurrences(source, pattern);

                    Assert.Equal(occurrences.Length > 0, sut.Contains(pattern));
                    Assert.Equal(occurrences.Length, sut.CountOccurrences(pattern));
                    Assert.Equal(occurrences.Length > 0 ? occurrences[0] : -1, sut.IndexOf(pattern));
                    Assert.Equal(occurrences, sut.GetOccurrences(pattern));

                    // The allocation-free tier hands back a slice of the index itself, so its order is
                    // lexicographic rather than ascending — which is exactly the sub-sequence of the suffix
                    // order restricted to the matching positions.
                    Assert.Equal(occurrences.Length > 0, sut.TryGetOccurrences(pattern, out ReadOnlySpan<int> slice));
                    Assert.Equal(expected.Where(occurrences.Contains).ToArray(), slice.ToArray());

                    var copied = new int[occurrences.Length];
                    Assert.Equal(occurrences.Length, sut.CopyOccurrences(pattern, copied));
                    Assert.Equal(occurrences, copied);
                }

                // The longest repeat, against the quadratic answer the LCP array exists to avoid.
                int longest = 0;
                for (int a = 0; a < source.Length; a++)
                {
                    for (int b = a + 1; b < source.Length; b++)
                        longest = Math.Max(longest, CommonPrefixLength(source, a, b));
                }

                Assert.Equal(longest > 0, sut.TryGetLongestRepeatedSubstring(out int start2, out int length2));
                Assert.Equal(longest, length2);
                if (longest > 0)
                    Assert.True(sut.CountOccurrences(source.AsSpan(start2, length2)) >= 2);
            }, iter: 500);
    }

    // ---- AhoCorasick vs testing every pattern at every position ---------------

    // The oracle is the loop the automaton replaces — every pattern tested at every position — ordered the
    // way the type documents: ascending end position, longest first among matches ending together. What is
    // on trial is the failure and output links. An automaton whose links are wrong still finds every pattern
    // that starts where the scan happens to be at the root; what it loses is the patterns that start *inside*
    // a partial match of another one. Over a wide alphabet that case barely arises; over three letters nearly
    // every pattern is a suffix of some prefix of another, so it is the norm.
    [Fact]
    public void AhoCorasick_ShouldMatch_NaiveMultiPatternScan()
    {
        Gen.Select(
            Gen.Int[0, Alphabet - 1].List[1, 5].Select(Render).List[0, 8],
            GenText)
        .Sample((patterns, source) =>
        {
            var sut = new AhoCorasick(patterns);

            // Duplicates collapse to the id of the first appearance, which is the only ordering promise the
            // type makes about ids — and the one every match below is reported against.
            string[] distinct = [.. patterns.Distinct(StringComparer.Ordinal)];
            Assert.Equal(distinct.Length, sut.Count);
            Assert.Equal(distinct, sut.ToArray());
            for (int id = 0; id < distinct.Length; id++)
                Assert.Equal(distinct[id], sut[id]);

            var expected = new List<PatternMatch>();
            for (int id = 0; id < distinct.Length; id++)
            {
                foreach (int position in NaiveOccurrences(source, distinct[id]))
                    expected.Add(new PatternMatch(id, position, distinct[id].Length));
            }

            PatternMatch[] oracle = [.. expected.OrderBy(m => m.End).ThenByDescending(m => m.Length)];
            PatternMatch[] found = sut.FindAll(source);

            Assert.Equal(oracle, found);
            Assert.Equal(oracle.Length, sut.CountMatches(source));
            Assert.Equal(oracle.Length > 0, sut.ContainsAny(source));

            // The allocation-free read path over the same scan. MatchEnumerator is a ref struct, so it is
            // drained by hand rather than through LINQ.
            var streamed = new List<PatternMatch>();
            foreach (PatternMatch match in sut.EnumerateMatches(source))
                streamed.Add(match);

            Assert.Equal(oracle, streamed);

            Assert.Equal(oracle.Length > 0, sut.TryFindFirst(source, out PatternMatch first));
            if (oracle.Length > 0)
                Assert.Equal(oracle[0], first);

            var copied = new PatternMatch[oracle.Length];
            Assert.Equal(oracle.Length, sut.CopyMatches(source, copied));
            Assert.Equal(oracle, copied);

            // Every reported range must actually hold its pattern. The comparison above would miss a length
            // the oracle and the automaton got wrong in the same way.
            foreach (PatternMatch match in found)
            {
                Assert.Equal(sut[match.PatternId].Length, match.Length);
                Assert.Equal(sut[match.PatternId], source.Substring(match.Start, match.Length));
            }
        }, iter: 1_000);
    }

    // The empty pattern occurs at every position, which is the answer SuffixArray gives and the reason
    // AhoCorasick rejects it outright — an automaton that matched it would report every position too.
    private static int[] NaiveOccurrences(string text, string pattern)
    {
        var found = new List<int>();
        if (pattern.Length == 0)
            return [.. Enumerable.Range(0, text.Length)];

        for (int position = 0; position + pattern.Length <= text.Length; position++)
        {
            if (string.CompareOrdinal(text, position, pattern, 0, pattern.Length) == 0)
                found.Add(position);
        }

        return [.. found];
    }

    private static int CommonPrefixLength(string text, int first, int second)
    {
        int matched = 0;
        while (first + matched < text.Length && second + matched < text.Length &&
               text[first + matched] == text[second + matched])
        {
            matched++;
        }

        return matched;
    }
}
