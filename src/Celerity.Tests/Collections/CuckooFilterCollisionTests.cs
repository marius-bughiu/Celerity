using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Worst-case hashing tests for <see cref="CuckooFilter{T,THasher}"/>: a degenerate hasher that maps every
/// element to the same base hash collapses every element onto one fingerprint and one bucket pair, so the
/// filter holds only a handful of copies before it reports full — but it never loses an inserted element (no
/// false negatives) and the alternate-bucket XOR stays in range under adversarial inputs.
/// </summary>
public class CuckooFilterCollisionTests
{
    // Maps every value to the same base hash, so all elements get the same fingerprint and the same candidate
    // buckets — the pathological case for a cuckoo filter.
    private struct ConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    // Maps every value to a fixed negative constant unrelated to the input magnitude.
    private struct NegativeConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => int.MinValue;
    }

    [Fact]
    public void ConstantHasher_StillHasNoFalseNegatives()
    {
        var filter = new CuckooFilter<int, ConstantHasher>(1000);

        // Every element collapses onto one fingerprint/bucket pair, so only a few fit before the filter fills.
        var added = new List<int>();
        for (int i = 0; i < 100; i++)
        {
            if (!filter.TryAdd(i))
                break;
            added.Add(i);
        }

        Assert.NotEmpty(added);
        foreach (int a in added)
            Assert.True(filter.Contains(a), $"false negative under constant hasher for {a}");
    }

    [Fact]
    public void ConstantHasher_MakesEverythingLookPresent()
    {
        // With all elements colliding on the same fingerprint and bucket, adding one makes the filter report any
        // element as present. Graceful degradation (the false-positive rate goes to 1), not a correctness failure.
        var filter = new CuckooFilter<int, ConstantHasher>(1000);
        filter.Add(1);

        Assert.True(filter.Contains(1));
        Assert.True(filter.Contains(99999)); // never added, but collides
    }

    [Fact]
    public void EmptyFilter_WithConstantHasher_ReportsAbsent()
    {
        var filter = new CuckooFilter<int, ConstantHasher>(1000);
        Assert.False(filter.Contains(0));
        Assert.False(filter.Contains(123));
    }

    [Fact]
    public void ConstantHasher_FillsToFull_ThenRejects()
    {
        var filter = new CuckooFilter<int, ConstantHasher>(1000);

        int v = 0;
        while (filter.TryAdd(v))
        {
            v++;
            if (v > 10_000) break; // safety valve
        }

        Assert.True(filter.IsFull);
        Assert.False(filter.TryAdd(v));
    }

    [Fact]
    public void NegativeConstantBaseHash_DoesNotBreakIndexing()
    {
        // The base hash int.MinValue is cast to uint and avalanched; the masked fingerprint and bucket index
        // must stay in range and round-trip.
        var filter = new CuckooFilter<int, NegativeConstantHasher>(1000);
        filter.Add(5);
        Assert.True(filter.Contains(5));
        Assert.True(filter.Contains(6)); // collides on the constant base hash
    }

    [Fact]
    public void ConstantHasher_Clear_Resets()
    {
        var filter = new CuckooFilter<int, ConstantHasher>(1000);
        filter.Add(1);
        Assert.True(filter.Contains(2)); // collides → looks present

        filter.Clear();
        Assert.False(filter.Contains(2));
        Assert.Equal(0, filter.Count);
        Assert.False(filter.IsFull);
    }

    [Fact]
    public void ConstantHasher_RemoveAfterFull_FreesSpace()
    {
        var filter = new CuckooFilter<int, ConstantHasher>(1000);

        var added = new List<int>();
        int v = 0;
        while (filter.TryAdd(v))
        {
            added.Add(v);
            v++;
            if (v > 10_000) break;
        }
        Assert.True(filter.IsFull);

        // All added elements share one fingerprint, so removing one frees a slot for the parked victim.
        Assert.True(filter.Remove(added[0]));
        Assert.False(filter.IsFull);
    }
}
