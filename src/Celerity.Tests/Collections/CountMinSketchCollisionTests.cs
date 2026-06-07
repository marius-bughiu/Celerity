using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Worst-case hashing tests for <see cref="CountMinSketch{T,THasher}"/>: a degenerate
/// hasher that maps every element to the same base hash routes all elements to the same
/// counters, so the never-underestimate guarantee still holds (estimates merely inflate
/// toward the total count), and adversarial constant base hashes still produce in-range
/// column indices.
/// </summary>
public class CountMinSketchCollisionTests
{
    // Maps every value to the same base hash, so all elements share the same counters —
    // the pathological case for a Count-Min sketch.
    private struct ConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    // Maps every value to a constant at the negative extreme; cast to uint and avalanched,
    // it must still yield an in-range column index.
    private struct NegativeConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => int.MinValue;
    }

    [Fact]
    public void ConstantHasher_StillNeverUnderestimates()
    {
        var sketch = new CountMinSketch<int, ConstantHasher>();
        for (int i = 0; i < 100; i++)
            sketch.Add(i);

        // Every element shares the same counters, so each estimate is the total count —
        // an overestimate, never an underestimate.
        for (int i = 0; i < 100; i++)
            Assert.True(sketch.EstimateCount(i) >= 1, $"underestimate under constant hasher for {i}");
    }

    [Fact]
    public void ConstantHasher_MakesEveryEstimateTheTotal()
    {
        // With all elements colliding on the same counters, each estimate equals the
        // total count. This is graceful degradation (a maximal overestimate), not a
        // correctness failure.
        var sketch = new CountMinSketch<int, ConstantHasher>();
        sketch.Add(1, 10);
        sketch.Add(2, 5);

        Assert.Equal(15, sketch.EstimateCount(1));
        Assert.Equal(15, sketch.EstimateCount(99999)); // never added, but collides
    }

    [Fact]
    public void EmptySketch_WithConstantHasher_EstimatesZero()
    {
        var sketch = new CountMinSketch<int, ConstantHasher>();
        Assert.Equal(0, sketch.EstimateCount(0));
        Assert.Equal(0, sketch.EstimateCount(123));
    }

    [Fact]
    public void NegativeConstantBaseHash_DoesNotBreakIndexing()
    {
        // The base hash int.MinValue is cast to uint and avalanched; the masked column
        // index must stay in range and round-trip.
        var sketch = new CountMinSketch<int, NegativeConstantHasher>();
        sketch.Add(5, 3);
        Assert.True(sketch.EstimateCount(5) >= 3);
        Assert.True(sketch.EstimateCount(6) >= 3); // collides on the constant base hash
    }

    [Fact]
    public void ConstantHasher_Clear_Resets()
    {
        var sketch = new CountMinSketch<int, ConstantHasher>();
        sketch.Add(1, 4);
        Assert.Equal(4, sketch.EstimateCount(2)); // collides → reads the total

        sketch.Clear();
        Assert.Equal(0, sketch.EstimateCount(2));
        Assert.Equal(0, sketch.TotalCount);
    }

    [Fact]
    public void ConstantHasher_Depth_AtLeastOne()
    {
        // Even at a loose delta the sketch keeps at least one row, so a single Add /
        // Estimate round-trips.
        var sketch = new CountMinSketch<int, ConstantHasher>(0.5, 0.5);
        Assert.True(sketch.Depth >= 1);
        sketch.Add(7, 2);
        Assert.True(sketch.EstimateCount(7) >= 2);
    }
}
