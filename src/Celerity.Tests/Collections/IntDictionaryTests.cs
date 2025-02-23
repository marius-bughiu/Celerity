using Celerity.Collections;

namespace Celerity.Tests.Collections;

public class IntDictionaryTests
{
    [Fact]
    public void Indexer_ShouldInsertAndRetrieveValue()
    {
        var map = new IntDictionary<int>();
        map[10] = 100;

        Assert.Equal(100, map[10]);
    }

    [Fact]
    public void Indexer_ShouldThrowException_WhenKeyDoesNotExist()
    {
        var map = new IntDictionary<int>();
        Assert.Throws<KeyNotFoundException>(() => { var value = map[99]; });
    }

    [Fact]
    public void Indexer_ShouldOverwriteExistingValue()
    {
        var map = new IntDictionary<int>();
        map[5] = 500;
        map[5] = 999; // Overwrite

        Assert.Equal(999, map[5]);
    }

    [Fact]
    public void Remove_ShouldDeleteKeyAndMakeItUnreachable()
    {
        var map = new IntDictionary<int>();
        map[7] = 700;

        bool removed = map.Remove(7);
        Assert.True(removed);
        Assert.False(map.ContainsKey(7));

        Assert.Throws<KeyNotFoundException>(() => { var value = map[7]; });
    }

    [Fact]
    public void Map_ShouldResize_WhenThresholdExceeded()
    {
        var map = new IntDictionary<int>(4);
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;
        map[4] = 40; // Triggers resize

        Assert.Equal(4, map.Count);
        Assert.Equal(10, map[1]);
        Assert.Equal(20, map[2]);
        Assert.Equal(30, map[3]);
        Assert.Equal(40, map[4]);
    }
}
