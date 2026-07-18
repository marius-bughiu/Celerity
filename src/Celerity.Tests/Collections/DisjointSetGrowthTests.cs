using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for <see cref="DisjointSet{T}"/> across backing-array growth: adding far more elements than any
/// initial capacity must re-home every element's parent/size slot correctly, so connectivity, component
/// sizes, and the set count all stay consistent after many resizes.
/// </summary>
public class DisjointSetGrowthTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void ManySingletons_GrowWithoutCorruption(int startCapacity)
    {
        const int N = 5000;
        var ds = new DisjointSet<int>(startCapacity);

        for (int i = 0; i < N; i++)
            Assert.True(ds.Add(i));

        Assert.Equal(N, ds.Count);
        Assert.Equal(N, ds.SetCount);
        Assert.True(ds.Capacity >= N);

        for (int i = 0; i < N; i++)
        {
            Assert.True(ds.Contains(i));
            Assert.Equal(i, ds.Find(i));
            Assert.Equal(1, ds.ComponentSize(i));
        }
    }

    [Fact]
    public void ChainUnionAcrossGrowth_CollapsesToOneComponent()
    {
        const int N = 10_000;
        var ds = new DisjointSet<int>(0);

        // Union sequential neighbours; auto-add drives repeated growth as the universe expands.
        for (int i = 1; i < N; i++)
            Assert.True(ds.Union(i - 1, i));

        Assert.Equal(N, ds.Count);
        Assert.Equal(1, ds.SetCount);
        Assert.Equal(N, ds.ComponentSize(0));
        Assert.True(ds.Connected(0, N - 1));

        var components = ds.GetComponents();
        Assert.Single(components);
        Assert.Equal(N, components[0].Count);
    }

    [Fact]
    public void PairwiseUnionsAcrossGrowth_HalveComponentCount()
    {
        const int N = 8192; // power of two so every element pairs off cleanly
        var ds = new DisjointSet<int>(2);

        for (int i = 0; i < N; i++)
            ds.Add(i);
        Assert.Equal(N, ds.SetCount);

        // Union adjacent pairs (0-1, 2-3, ...): exactly N/2 merges.
        for (int i = 0; i + 1 < N; i += 2)
            Assert.True(ds.Union(i, i + 1));

        Assert.Equal(N, ds.Count);
        Assert.Equal(N / 2, ds.SetCount);
        for (int i = 0; i + 1 < N; i += 2)
        {
            Assert.True(ds.Connected(i, i + 1));
            Assert.Equal(2, ds.ComponentSize(i));
        }
    }
}
