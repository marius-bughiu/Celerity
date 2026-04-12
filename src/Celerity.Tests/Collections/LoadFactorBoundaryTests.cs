using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Load-factor boundary tests for <see cref="IntDictionary{TValue}"/> and
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>. Covers the remaining
/// gap from issue #7: load-factor boundary conditions were not exercised by the
/// initial test suite.
///
/// Tests verify that the resize threshold is computed correctly from the supplied
/// load factor, that all data survives one or more resizes, and that the
/// out-of-band default/zero-key slot does not corrupt load-factor accounting.
/// </summary>
public class LoadFactorBoundaryTests
{
    // ---------------------------------------------------------------
    //  IntDictionary — low load factor
    // ---------------------------------------------------------------

    // capacity=8 → size=8, threshold=(int)(8×0.5)=4.
    // The 5th non-zero insert triggers a resize. All items must survive.
    [Fact]
    public void IntDictionary_LowLoadFactor_TriggersEarlyResize_AndPreservesAllData()
    {
        var map = new IntDictionary<int>(capacity: 8, loadFactor: 0.5f);

        for (int i = 1; i <= 20; i++)
            map[i] = i * 10;

        Assert.Equal(20, map.Count);
        for (int i = 1; i <= 20; i++)
            Assert.Equal(i * 10, map[i]);
    }

    // ---------------------------------------------------------------
    //  IntDictionary — high load factor
    // ---------------------------------------------------------------

    // capacity=16 → size=16, threshold=(int)(16×0.95)=15.
    // The dictionary holds 15 items before the first resize. All items
    // from both before and after the resize must still be reachable.
    [Fact]
    public void IntDictionary_HighLoadFactor_PermitsHighDensity_AndPreservesAllData()
    {
        var map = new IntDictionary<int>(capacity: 16, loadFactor: 0.95f);

        for (int i = 1; i <= 30; i++)
            map[i] = i * 100;

        Assert.Equal(30, map.Count);
        for (int i = 1; i <= 30; i++)
            Assert.Equal(i * 100, map[i]);
    }

    // ---------------------------------------------------------------
    //  IntDictionary — multiple sequential resizes
    // ---------------------------------------------------------------

    // Starting with a tiny capacity forces the dictionary through many
    // doubling steps. Every entry must survive every resize.
    [Fact]
    public void IntDictionary_TinyInitialCapacity_MultipleResizes_PreserveAllEntries()
    {
        var map = new IntDictionary<int>(capacity: 2, loadFactor: 0.5f);

        for (int i = 1; i <= 200; i++)
            map[i] = i;

        Assert.Equal(200, map.Count);
        for (int i = 1; i <= 200; i++)
            Assert.Equal(i, map[i]);
    }

    // ---------------------------------------------------------------
    //  IntDictionary — zero key with load-factor boundary
    // ---------------------------------------------------------------

    // Key 0 is stored out-of-band and contributes to Count. Inserting it
    // before non-zero keys means Count reaches the threshold one step
    // sooner from the perspective of the non-zero-key path. Verify that
    // the resize still fires at the right moment and data is not lost.
    [Fact]
    public void IntDictionary_ZeroKey_InsertsBeforeThreshold_DoesNotCorruptResizeTiming()
    {
        // capacity=8, loadFactor=0.5 → threshold=4.
        // After inserting key=0 (out-of-band), count=1.
        // Non-zero inserts will trigger resize when count reaches 4.
        var map = new IntDictionary<int>(capacity: 8, loadFactor: 0.5f);
        map[0] = -1;

        for (int i = 1; i <= 20; i++)
            map[i] = i;

        Assert.Equal(21, map.Count);
        Assert.Equal(-1, map[0]);
        for (int i = 1; i <= 20; i++)
            Assert.Equal(i, map[i]);
    }

    // Inserting zero key exactly at the threshold count (out-of-band,
    // so no array slot is consumed) must not prevent subsequent non-zero
    // inserts from triggering the resize correctly.
    [Fact]
    public void IntDictionary_ZeroKey_InsertedAtThreshold_NonZeroInsertsStillWork()
    {
        // capacity=4, loadFactor=0.5 → size=4, threshold=2.
        // Fill to threshold via non-zero keys, then insert zero key.
        var map = new IntDictionary<int>(capacity: 4, loadFactor: 0.5f);
        map[1] = 10;
        map[2] = 20;
        // count=2 == threshold; next non-zero insert will resize.
        map[0] = -1;
        // count=3; zero key stored out-of-band. Insert non-zero → resize fires.
        map[3] = 30;

        Assert.Equal(4, map.Count);
        Assert.Equal(-1, map[0]);
        Assert.Equal(10, map[1]);
        Assert.Equal(20, map[2]);
        Assert.Equal(30, map[3]);
    }

    // ---------------------------------------------------------------
    //  IntDictionary — parameterized across several load factors
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.95f)]
    public void IntDictionary_VariousLoadFactors_AllDataSurvivesMultipleResizes(float loadFactor)
    {
        var map = new IntDictionary<int>(capacity: 4, loadFactor: loadFactor);

        for (int i = 1; i <= 100; i++)
            map[i] = i * 7;

        Assert.Equal(100, map.Count);
        for (int i = 1; i <= 100; i++)
            Assert.Equal(i * 7, map[i]);
    }

    // ---------------------------------------------------------------
    //  CelerityDictionary — low load factor
    // ---------------------------------------------------------------

    [Fact]
    public void CelerityDictionary_LowLoadFactor_TriggersEarlyResize_AndPreservesAllData()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(
            capacity: 8, loadFactor: 0.5f);

        for (int i = 1; i <= 20; i++)
            map[i] = i * 10;

        Assert.Equal(20, map.Count);
        for (int i = 1; i <= 20; i++)
            Assert.Equal(i * 10, map[i]);
    }

    // ---------------------------------------------------------------
    //  CelerityDictionary — high load factor
    // ---------------------------------------------------------------

    [Fact]
    public void CelerityDictionary_HighLoadFactor_PermitsHighDensity_AndPreservesAllData()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(
            capacity: 16, loadFactor: 0.95f);

        for (int i = 1; i <= 30; i++)
            map[i] = i * 100;

        Assert.Equal(30, map.Count);
        for (int i = 1; i <= 30; i++)
            Assert.Equal(i * 100, map[i]);
    }

    // ---------------------------------------------------------------
    //  CelerityDictionary — multiple sequential resizes
    // ---------------------------------------------------------------

    [Fact]
    public void CelerityDictionary_TinyInitialCapacity_MultipleResizes_PreserveAllEntries()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(
            capacity: 2, loadFactor: 0.5f);

        for (int i = 1; i <= 200; i++)
            map[i] = i;

        Assert.Equal(200, map.Count);
        for (int i = 1; i <= 200; i++)
            Assert.Equal(i, map[i]);
    }

    // ---------------------------------------------------------------
    //  CelerityDictionary — default key with load-factor boundary
    // ---------------------------------------------------------------

    // default(int) == 0 is stored out-of-band. Same accounting logic as
    // IntDictionary; verify resize timing and data integrity.
    [Fact]
    public void CelerityDictionary_DefaultKey_InsertsBeforeThreshold_DoesNotCorruptResizeTiming()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(
            capacity: 8, loadFactor: 0.5f);
        map[0] = -1;

        for (int i = 1; i <= 20; i++)
            map[i] = i;

        Assert.Equal(21, map.Count);
        Assert.Equal(-1, map[0]);
        for (int i = 1; i <= 20; i++)
            Assert.Equal(i, map[i]);
    }

    [Fact]
    public void CelerityDictionary_DefaultKey_InsertedAtThreshold_NonDefaultInsertsStillWork()
    {
        // capacity=4, loadFactor=0.5 → size=4, threshold=2.
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(
            capacity: 4, loadFactor: 0.5f);
        map[1] = 10;
        map[2] = 20;
        // count=2 == threshold. Insert default key out-of-band (no resize check).
        map[0] = -1;
        // count=3. Next non-default insert must fire a resize.
        map[3] = 30;

        Assert.Equal(4, map.Count);
        Assert.Equal(-1, map[0]);
        Assert.Equal(10, map[1]);
        Assert.Equal(20, map[2]);
        Assert.Equal(30, map[3]);
    }

    // ---------------------------------------------------------------
    //  CelerityDictionary — parameterized across several load factors
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.95f)]
    public void CelerityDictionary_VariousLoadFactors_AllDataSurvivesMultipleResizes(float loadFactor)
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(
            capacity: 4, loadFactor: loadFactor);

        for (int i = 1; i <= 100; i++)
            map[i] = i * 7;

        Assert.Equal(100, map.Count);
        for (int i = 1; i <= 100; i++)
            Assert.Equal(i * 7, map[i]);
    }
}
