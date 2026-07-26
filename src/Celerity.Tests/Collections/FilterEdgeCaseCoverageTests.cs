using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Boundary behaviour of the two probabilistic filters that only shows up in states the ordinary
/// tests never reach: <see cref="CuckooFilter{T,THasher}"/>'s single-entry <em>victim cache</em>, and
/// <see cref="XorFilter{T,THasher}"/>'s peel-and-reseed construction loop.
///
/// <para>
/// <b>The victim cache.</b> When an insertion exhausts its eviction budget the cuckoo filter parks the
/// one homeless fingerprint in a side slot and reports itself full. That fingerprint is stored
/// <em>outside</em> the bucket table, so every read and write path carries a second, compound
/// membership test — <c>hasVictim &amp;&amp; victimFingerprint == fingerprint &amp;&amp; (i1 == victimIndex
/// || i2 == victimIndex)</c> — that the happy path never evaluates past its first term. These tests pin
/// all three terms in both <see cref="CuckooFilter{T,THasher}.Contains"/> and
/// <see cref="CuckooFilter{T,THasher}.Remove"/>: deleting the parked victim itself, deleting a stranger
/// whose fingerprint matches the victim's but whose candidate buckets do not (which must <em>not</em>
/// register as a hit), and deleting an element elsewhere in the table — which frees a slot the victim
/// cannot use, so the filter stays full, exactly as <see cref="CuckooFilter{T,THasher}.IsFull"/>
/// documents. <see cref="CuckooFilter{T,THasher}.UnionWith"/> gets the same treatment from both sides:
/// a source victim that the destination can absorb, and one it cannot.
/// </para>
///
/// <para>
/// <b>Constructing those states without internals.</b> The library exposes no seam into the bucket
/// table, so the elements here are <em>discovered</em> through the public API instead of hard-coded.
/// Two probe filters holding a single element act as oracles: a one-bucket filter (<c>expectedItems ==
/// 1</c>) collapses both candidate buckets onto bucket&#160;0, so <c>Contains</c> on it is exactly
/// "same fingerprint"; a four-bucket filter holding the same element then splits those fingerprint
/// siblings by whether their candidate-bucket pair overlaps the anchor's. That yields, from public
/// behaviour alone, a family of elements that collide on fingerprint but land in disjoint halves of the
/// table — the only way to reach the third term of the victim test. The oracles are stated as
/// assertions in the tests that depend on them, so a change in the hashing would fail loudly rather
/// than quietly turn a test vacuous.
/// </para>
///
/// <para>
/// <b>Degenerate sizing.</b> Both constructors clamp their computed geometry to a floor. The tests
/// pin the smallest geometry each type can actually be asked for — a cuckoo filter at a near-1
/// false-positive rate, a cuckoo filter sized from a lazily-enumerated empty sequence, and an xor
/// filter over an empty source — which documents that the formulas bottom out well above their clamps
/// (4 fingerprint bits, 1 bucket, 30 xor slots), so the clamps are defensive rather than live.
/// </para>
///
/// <para>
/// <b>The xor peel retry.</b> Xor-filter construction peels a 3-uniform hypergraph and reseeds when a
/// 2-core survives. A stalled first attempt is invisible from outside — the filter simply works — so
/// the retry is pinned with an element pair chosen to collapse onto one another under the first
/// internal seed; construction must transparently reseed and still produce a filter with no false
/// negatives.
/// </para>
/// </summary>
public class FilterEdgeCaseCoverageTests
{
    // expectedItems == 1 → ceil(1 / (4 · 0.94)) == 1 bucket of four slots. Both candidate buckets of
    // every element are bucket 0, so the table holds four fingerprints plus one parked victim.
    private const int SingleBucketCapacity = 1;

    // expectedItems == 8 → ceil(8 / (4 · 0.94)) == 3, rounded up to 4 buckets (16 slots). Enough
    // buckets for an element's candidate pair to be disjoint from another's.
    private const int FourBucketCapacity = 8;

    // ---------------------------------------------------------------
    //  CuckooFilter — degenerate sizing
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldFloorFingerprintAtFourBits_WhenFalsePositiveRateIsNearOne()
    {
        // f = ceil(log2(2·BucketSize / p)); p is constrained to (0, 1), so 8/p > 8 and f can never
        // drop below 4 however loose the requested rate is. Likewise the bucket count bottoms out at
        // a single bucket for the smallest legal capacity. The filter must still be a working filter
        // at that floor.
        var filter = new CuckooFilter<int, Int32IdentityHasher>(1, 0.99);

        Assert.Equal(4, filter.FingerprintBits);
        Assert.Equal(1, filter.BucketCount);
        Assert.Equal(1, filter.Capacity);

        filter.Add(7);
        Assert.True(filter.Contains(7));
        Assert.Equal(1, filter.Count);
    }

    [Fact]
    public void Constructor_ShouldSizeForASingleItem_WhenLazySourceIsEmpty()
    {
        // A lazy sequence is not an ICollection<T>, so the constructor counts it by enumeration. An
        // empty count would size the table for zero elements; it is lifted to one so the filter is
        // usable rather than degenerate.
        var filter = new CuckooFilter<int, Int32IdentityHasher>(EmptyLazySequence());

        Assert.Equal(1, filter.Capacity);
        Assert.Equal(0, filter.Count);
        Assert.Equal(1, filter.BucketCount);

        filter.Add(42);
        Assert.True(filter.Contains(42));
    }

    // ---------------------------------------------------------------
    //  CuckooFilter — removing the parked victim
    // ---------------------------------------------------------------

    [Fact]
    public void Remove_ShouldDeleteEveryElement_WhenOneOfThemIsTheParkedVictim()
    {
        // Five elements with pairwise distinct fingerprints in a one-bucket filter: four occupy the
        // bucket and the fifth is left homeless in the victim cache. Which one ends up parked is an
        // internal eviction detail, so every element is removed in turn from an identically built
        // filter — one of those removals necessarily takes the victim-cache path, and all five must
        // behave identically from the outside: the element goes, the count drops by one, the other
        // four survive, and the filter is no longer full.
        List<int> elements = FindDistinctFingerprintElements(5);
        Assert.Equal(5, elements.Count);

        foreach (int removed in elements)
        {
            CuckooFilter<int, Int32IdentityHasher> filter = BuildFullSingleBucketFilter(elements);
            Assert.Equal(5, filter.Count);
            Assert.True(filter.IsFull);

            Assert.True(filter.Remove(removed), $"a full filter refused to remove {removed}");
            Assert.Equal(4, filter.Count);
            Assert.False(filter.Contains(removed));
            Assert.False(filter.IsFull);

            foreach (int survivor in elements)
            {
                if (survivor == removed)
                    continue;
                Assert.True(filter.Contains(survivor), $"removing {removed} lost {survivor}");
            }
        }
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenFingerprintDiffersFromTheParkedVictim()
    {
        // Second term of the victim test: an element that was never added and whose fingerprint does
        // not match the parked one must not be reported as removed, and must not disturb the count.
        List<int> elements = FindDistinctFingerprintElements(5);
        CuckooFilter<int, Int32IdentityHasher> filter = BuildFullSingleBucketFilter(elements);
        Assert.True(filter.IsFull);

        int stranger = FindDifferentFingerprintElement(elements);

        Assert.False(filter.Contains(stranger));
        Assert.False(filter.Remove(stranger));
        Assert.Equal(5, filter.Count);
        Assert.True(filter.IsFull);
    }

    // ---------------------------------------------------------------
    //  CuckooFilter — the victim's candidate buckets
    // ---------------------------------------------------------------

    [Fact]
    public void Contains_ShouldReturnFalse_WhenFingerprintMatchesTheVictimButBucketsDoNot()
    {
        // Third term of the victim test. The victim is only a member of the two buckets it could have
        // been homed in; an element that shares its fingerprint but hashes into the other half of the
        // table is a different element and must report absent — otherwise the victim cache would leak
        // false positives across the whole table.
        (List<int> siblings, List<int> strangers) = FindFingerprintSiblings(anchor: 0, wantedSiblings: 12);
        int stranger = strangers[0];

        // The oracle this test rests on: the stranger really does share the anchor's fingerprint.
        Assert.True(SharesFingerprint(0, stranger));

        CuckooFilter<int, Int32IdentityHasher> filter = BuildFilterWithVictim(siblings);

        Assert.False(filter.Contains(stranger));
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenFingerprintMatchesTheVictimButBucketsDoNot()
    {
        // The same third term on the delete path: a fingerprint match alone must not consume the
        // parked victim, or removing an element that was never added would silently delete one that
        // was.
        (List<int> siblings, List<int> strangers) = FindFingerprintSiblings(anchor: 0, wantedSiblings: 12);
        int stranger = strangers[0];
        Assert.True(SharesFingerprint(0, stranger));

        CuckooFilter<int, Int32IdentityHasher> filter = BuildFilterWithVictim(siblings);
        int before = filter.Count;

        Assert.False(filter.Remove(stranger));
        Assert.Equal(before, filter.Count);
        Assert.True(filter.IsFull);
    }

    [Fact]
    public void Remove_ShouldLeaveTheFilterFull_WhenTheFreedSlotIsOutsideTheVictimsBuckets()
    {
        // IsFull documents that a removal elsewhere in the table leaves the filter full: the parked
        // victim can only be re-homed into one of its own two candidate buckets, and a slot freed in
        // an unrelated bucket is no use to it.
        (List<int> siblings, List<int> strangers) = FindFingerprintSiblings(anchor: 0, wantedSiblings: 12);
        int stranger = strangers[0];

        var filter = new CuckooFilter<int, Int32IdentityHasher>(FourBucketCapacity);
        Assert.True(filter.TryAdd(stranger)); // lands in the half of the table the victim cannot reach
        FillUntilFull(filter, siblings);

        int before = filter.Count;

        Assert.True(filter.Remove(stranger));
        Assert.Equal(before - 1, filter.Count);
        Assert.True(filter.IsFull); // the freed slot is unreachable for the victim
        Assert.False(filter.Contains(stranger));
    }

    // ---------------------------------------------------------------
    //  CuckooFilter — merging a filter that has a victim
    // ---------------------------------------------------------------

    [Fact]
    public void UnionWith_ShouldAbsorbTheSourcesVictim_WhenTheDestinationHasRoom()
    {
        // A full source is still a source: its parked fingerprint is part of its membership, so the
        // merge must carry it over rather than drop it — even though absorbing it fills the
        // destination in turn.
        List<int> elements = FindDistinctFingerprintElements(5);
        CuckooFilter<int, Int32IdentityHasher> source = BuildFullSingleBucketFilter(elements);
        Assert.True(source.IsFull);

        var destination = new CuckooFilter<int, Int32IdentityHasher>(SingleBucketCapacity);
        destination.UnionWith(source);

        Assert.Equal(5, destination.Count);
        Assert.True(destination.IsFull);
        foreach (int element in elements)
            Assert.True(destination.Contains(element), $"the union lost {element}");

        // The source is left untouched.
        Assert.Equal(5, source.Count);
        Assert.True(source.IsFull);
    }

    [Fact]
    public void UnionWith_ShouldThrow_WhenTheDestinationFillsBeforeTheSourcesVictim()
    {
        // The destination already holds one element, so the source's four bucket fingerprints fill it
        // exactly — the last of them takes the destination's own victim slot. The source's parked
        // fingerprint then has nowhere to go, which is a failed merge, not a silently dropped element.
        List<int> elements = FindDistinctFingerprintElements(5);
        CuckooFilter<int, Int32IdentityHasher> source = BuildFullSingleBucketFilter(elements);

        var destination = new CuckooFilter<int, Int32IdentityHasher>(SingleBucketCapacity);
        destination.Add(elements[0]);

        var error = Assert.Throws<InvalidOperationException>(() => destination.UnionWith(source));
        Assert.Contains("merging", error.Message);

        // A partial merge is documented: what was absorbed stays, and nothing already stored is lost.
        Assert.Equal(5, destination.Count);
        Assert.True(destination.IsFull);
        Assert.True(destination.Contains(elements[0]));
    }

    // ---------------------------------------------------------------
    //  XorFilter — construction
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldRetryWithAFreshSeed_WhenTheFirstPeelAttemptStalls()
    {
        // Two keys that map to the same three segment slots cannot be peeled: every slot they touch
        // has degree two, so the queue of degree-1 slots starts empty and the 2-core survives. 10 and
        // 92 are exactly such a pair under the first internal construction seed at this size, so the
        // first peel attempt stalls and construction must reseed and try again. From the outside the
        // only evidence is that the filter builds at all and holds both elements.
        var filter = new XorFilter<int, Int32IdentityHasher>(new[] { 10, 92 });

        Assert.Equal(2, filter.Count);
        // 32 + ceil(1.23 · 2) = 35 slots requested → blockLength 11 → three segments of 11. The
        // stalling pair above is specific to this block length, so a change here retires the case.
        Assert.Equal(33, filter.SlotCount);
        Assert.True(filter.Contains(10));
        Assert.True(filter.Contains(92));
    }

    [Fact]
    public void Constructor_ShouldSizeToTheMinimumThreeSegmentStore_WhenSourceIsEmpty()
    {
        // The slot budget is 32 + ceil(1.23 · n), so even an empty set is given 32 slots — 10 per
        // segment after the split. The per-segment length therefore never approaches its floor of one,
        // and the empty filter is a valid, queryable filter rather than a zero-length store.
        var filter = new XorFilter<int, Int32IdentityHasher>(Array.Empty<int>());

        Assert.Equal(0, filter.Count);
        Assert.Equal(30, filter.SlotCount); // three segments of ten
        Assert.Equal(0d, filter.BitsPerElement);
        Assert.False(filter.Contains(0));
        Assert.False(filter.Contains(12345));
    }

    // ---------------------------------------------------------------
    //  Helpers — building the states above out of public behaviour only
    // ---------------------------------------------------------------

    private static IEnumerable<int> EmptyLazySequence()
    {
        yield break;
    }

    /// <summary>
    /// Fingerprint-equality oracle. In a one-bucket filter both candidate buckets of every element are
    /// bucket 0, so a filter holding exactly one element reports <c>Contains</c> for another element
    /// if and only if the two share a fingerprint.
    /// </summary>
    private static bool SharesFingerprint(int anchor, int candidate)
    {
        var probe = new CuckooFilter<int, Int32IdentityHasher>(SingleBucketCapacity);
        probe.Add(anchor);
        return probe.Contains(candidate);
    }

    /// <summary>
    /// Finds elements with pairwise distinct fingerprints, so that a one-bucket filter holding them is
    /// guaranteed to park a victim whose fingerprint appears nowhere in the bucket.
    /// </summary>
    private static List<int> FindDistinctFingerprintElements(int wanted)
    {
        var picks = new List<int>();
        for (int candidate = 0; candidate < 10_000 && picks.Count < wanted; candidate++)
        {
            bool clashes = false;
            foreach (int pick in picks)
            {
                if (SharesFingerprint(pick, candidate))
                {
                    clashes = true;
                    break;
                }
            }

            if (!clashes)
                picks.Add(candidate);
        }

        Assert.Equal(wanted, picks.Count);
        return picks;
    }

    /// <summary>
    /// Finds an element whose fingerprint differs from every one of <paramref name="elements"/>, so a
    /// membership query for it cannot get past the fingerprint comparison.
    /// </summary>
    private static int FindDifferentFingerprintElement(List<int> elements)
    {
        int found = -1;
        for (int candidate = 0; candidate < 10_000 && found < 0; candidate++)
        {
            bool clashes = false;
            foreach (int element in elements)
            {
                if (SharesFingerprint(element, candidate))
                {
                    clashes = true;
                    break;
                }
            }

            if (!clashes)
                found = candidate;
        }

        Assert.True(found >= 0, "no element with a fingerprint unused by the filter was found");
        return found;
    }

    /// <summary>
    /// Splits the elements that share <paramref name="anchor"/>'s fingerprint into those whose
    /// candidate-bucket pair overlaps the anchor's (returned first, anchor included) and those whose
    /// pair is disjoint from it (returned second). The split is read off a four-bucket probe filter
    /// holding only the anchor: it reports <c>Contains</c> for a fingerprint sibling exactly when one
    /// of the sibling's two candidate buckets is where the anchor's fingerprint sits.
    /// </summary>
    private static (List<int> Siblings, List<int> Strangers) FindFingerprintSiblings(int anchor, int wantedSiblings)
    {
        var fingerprintProbe = new CuckooFilter<int, Int32IdentityHasher>(SingleBucketCapacity);
        fingerprintProbe.Add(anchor);

        var bucketProbe = new CuckooFilter<int, Int32IdentityHasher>(FourBucketCapacity);
        bucketProbe.Add(anchor);

        var siblings = new List<int> { anchor };
        var strangers = new List<int>();

        for (int candidate = anchor + 1;
             candidate < 400_000 && (siblings.Count < wantedSiblings || strangers.Count == 0);
             candidate++)
        {
            if (!fingerprintProbe.Contains(candidate))
                continue; // a different fingerprint entirely

            if (bucketProbe.Contains(candidate))
                siblings.Add(candidate);
            else
                strangers.Add(candidate);
        }

        Assert.True(siblings.Count >= wantedSiblings,
            $"found only {siblings.Count} fingerprint siblings sharing the anchor's buckets");
        Assert.NotEmpty(strangers);
        return (siblings, strangers);
    }

    private static CuckooFilter<int, Int32IdentityHasher> BuildFullSingleBucketFilter(List<int> elements)
    {
        var filter = new CuckooFilter<int, Int32IdentityHasher>(SingleBucketCapacity);
        foreach (int element in elements)
            filter.Add(element);

        Assert.True(filter.IsFull, "the one-bucket filter did not park a victim");
        return filter;
    }

    /// <summary>
    /// Builds a four-bucket filter whose victim is parked in the half of the table reachable from
    /// <paramref name="siblings"/>. Every sibling shares one fingerprint and one candidate-bucket
    /// pair, so the eviction chain never leaves those two buckets.
    /// </summary>
    private static CuckooFilter<int, Int32IdentityHasher> BuildFilterWithVictim(List<int> siblings)
    {
        var filter = new CuckooFilter<int, Int32IdentityHasher>(FourBucketCapacity);
        FillUntilFull(filter, siblings);
        return filter;
    }

    private static void FillUntilFull(CuckooFilter<int, Int32IdentityHasher> filter, List<int> siblings)
    {
        foreach (int sibling in siblings)
        {
            if (filter.IsFull)
                break;

            // Nothing is refused until a victim is parked: even the insertion that exhausts its
            // eviction budget succeeds, because the homeless fingerprint is kept rather than dropped.
            Assert.True(filter.TryAdd(sibling), $"the filter refused {sibling} before reporting full");
        }

        Assert.True(filter.IsFull, "the colliding elements did not exhaust an eviction budget");
    }
}
