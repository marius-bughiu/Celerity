using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Exercises <see cref="CelerityMultiSet{T, THasher}"/> under maximum
/// hash-collision pressure (every element probes the same chain) and with the
/// backward-shift deletion that runs when an element's last occurrence is removed.
/// Mirrors <c>CelerityMultiMapCollisionTests</c> for the counting semantics: a
/// single linear-probing cluster must keep every element's count distinct, and
/// collapsing one element must not orphan the others sharing its chain.
/// </summary>
public class CelerityMultiSetCollisionTests
{
    /// <summary>A test-only hasher that returns a constant for every int element.</summary>
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    /// <summary>A test-only hasher that returns a constant for every string element.</summary>
    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    /// <summary>A test-only hasher returning the element itself (predictable slots).</summary>
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    [Fact]
    public void Add_ShouldKeepCountsDistinct_UnderFullCollision()
    {
        var set = new CelerityMultiSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            set.Add(i, i);

        Assert.Equal(10, set.Count);
        for (int i = 1; i <= 10; i++)
            Assert.Equal(i, set[i]);
    }

    [Fact]
    public void RemoveAll_FromMiddleOfCluster_ShouldNotOrphanOthers()
    {
        var set = new CelerityMultiSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 8; i++)
            set.Add(i, i);

        // Collapse an element in the middle of the probe cluster.
        Assert.True(set.RemoveAll(4));

        Assert.Equal(7, set.Count);
        Assert.False(set.Contains(4));
        for (int i = 1; i <= 8; i++)
        {
            if (i == 4) continue;
            Assert.True(set.Contains(i));
            Assert.Equal(i, set[i]);
        }
    }

    [Fact]
    public void Remove_LastOccurrence_ShouldBackwardShift_UnderFullCollision()
    {
        var set = new CelerityMultiSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            set.Add(i);

        // Remove the only occurrence of element 3 — triggers backward-shift deletion.
        Assert.True(set.Remove(3));
        Assert.False(set.Contains(3));
        Assert.Equal(5, set.Count);

        // Re-add 3; it must find a slot and stay distinct from the others.
        set.Add(3, 9);
        Assert.Equal(9, set[3]);
        Assert.Equal(6, set.Count);
        for (int i = 1; i <= 6; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void StringElements_ShouldCountCorrectly_UnderFullCollision()
    {
        var set = new CelerityMultiSet<string, ConstantStringHasher>(16);
        string[] elements = { "alpha", "beta", "gamma", "delta", "epsilon" };
        foreach (string e in elements)
            set.Add(e, 2);

        Assert.Equal(elements.Length, set.Count);
        foreach (string e in elements)
            Assert.Equal(2, set[e]);

        // Remove all of one wrapped-cluster element; others must survive.
        Assert.True(set.RemoveAll("gamma"));
        Assert.False(set.Contains("gamma"));
        foreach (string e in elements.Where(e => e != "gamma"))
            Assert.Equal(2, set[e]);
    }

    [Fact]
    public void WrappedCluster_RemoveAll_ShouldPreserveAllSurvivors()
    {
        // IdentityIntHasher + a known table size builds a cluster that wraps the
        // end of the array, exercising the wrapped branch of backward-shift.
        var set = new CelerityMultiSet<int, IdentityIntHasher>(8); // size 8, mask 7
        int[] elements = { 6, 7, 14, 15, 22 }; // all land near the high end and wrap
        foreach (int e in elements)
            set.Add(e, e);

        Assert.True(set.RemoveAll(7));
        Assert.False(set.Contains(7));
        foreach (int e in elements)
        {
            if (e == 7) continue;
            Assert.True(set.Contains(e));
            Assert.Equal(e, set[e]);
        }
    }

    [Fact]
    public void NullElement_ShouldCoexistWithCollidingStringElements()
    {
        var set = new CelerityMultiSet<string, ConstantStringHasher>(16);
        set.Add(null!, 3);
        for (int i = 1; i <= 5; i++)
            set.Add($"k{i}", i);

        Assert.Equal(6, set.Count);
        Assert.Equal(3, set[null!]);

        Assert.True(set.RemoveAll(null!));
        Assert.False(set.Contains(null!));
        Assert.Equal(5, set.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, set[$"k{i}"]);
    }

    [Fact]
    public void RemoveAll_OnWrapAroundCluster_ShouldKeepHomedElement()
    {
        // With an identity hasher and capacity 8, elements 1, 2, 9 form one probe
        // cluster: 1->slot1, 2->slot2, 9(home 1)->slot3. Removing element 1 forces
        // the backward shift to *skip* element 2 (already at its home slot — the
        // bypassesGap branch) while relocating element 9 into the freed slot.
        var set = new CelerityMultiSet<int, IdentityIntHasher>(8);
        set.Add(1, 10);
        set.Add(2, 20);
        set.Add(9, 90);

        Assert.True(set.RemoveAll(1));

        Assert.False(set.Contains(1));
        Assert.Equal(20, set[2]);
        Assert.Equal(90, set[9]);
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void RemoveAll_OnWrapAroundCluster_WithHomeAboveGap_ExercisesWrappedKeepBranch()
    {
        // Table size 8 (mask = 7), identity hasher. Elements 6, 7, 15, 23 land as:
        //   slot 6 -> 6  (natural 6) — removed, becomes the gap at i = 6
        //   slot 7 -> 7  (natural 7)
        //   slot 0 -> 15 (natural 7; wrapped past 7)
        //   slot 1 -> 23 (natural 7; wrapped past 7, 0)
        // Removing element 6 scans the wrapped slots 0 and 1, whose natural slot (7)
        // is GREATER than the gap (6), so the `i > j` bypass takes its `i < k`
        // == true path — the branch the homed-element cluster above never reaches.
        var set = new CelerityMultiSet<int, IdentityIntHasher>(8);
        set.Add(6, 60);
        set.Add(7, 70);
        set.Add(15, 150);
        set.Add(23, 230);

        Assert.True(set.RemoveAll(6));

        Assert.False(set.Contains(6));
        Assert.Equal(70, set[7]);
        Assert.Equal(150, set[15]);
        Assert.Equal(230, set[23]);
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void SetCountToZero_FromMiddleOfCluster_ShouldBackwardShift()
    {
        // SetCount(e, 0) takes the same backward-shift path as RemoveAll under a
        // full-collision chain; verify it does not orphan neighbours.
        var set = new CelerityMultiSet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            set.Add(i, i);

        Assert.Equal(3, set.SetCount(3, 0));
        Assert.False(set.Contains(3));
        Assert.Equal(5, set.Count);
        for (int i = 1; i <= 6; i++)
        {
            if (i == 3) continue;
            Assert.Equal(i, set[i]);
        }
    }
}
