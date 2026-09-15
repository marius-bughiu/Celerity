using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// RangeMap<int, int> against the hand-roll it replaces. .NET ships no range map of any kind, and
// SortedDictionary cannot stand in — it has no floor query, so it cannot even find the range covering a key
// without walking — so the honest baseline is what a caller writes instead: a List<T> of disjoint ranges kept
// sorted by start, binary-searched for a lookup and patched in place on every assignment. The baseline arms are
// named List_* so the dashboard classifies them as the reference series.
//
// The hand-roll is written the way a careful caller would write it: one binary search to find the first range
// an assignment touches, a forward walk to find the last, then the replacement written over the removed slots
// so the list shifts at most once. It does *not* merge equal neighbours, which RangeMap always does — so it is
// doing strictly less work per assignment, and the Assign ratio understates the gap rather than inflating it.
// The values are drawn wide enough that merges are rare anyway, so both sides hold about the same number of
// ranges throughout.
//
// Three categories. Lookup is the point query ("who owns key x"), where a flat sorted array's binary search is
// the strongest thing the BCL offers and is expected to hold its own: this row is here to report that
// honestly, not to be won. Assign is the edit that motivates the type — overwrite a short random range, which
// splits the ranges at both edges — where the list pays an O(n) memmove per call and the map O(log n). Overlap
// walks the ranges touching a short window, the "what does this span cover" read.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RangeMapBenchmark
{
    private const int OperationCount = 1000;
    private const int RangeWidth = 10;

    private List<Interval<int, int>> list = null!;
    private RangeMap<int, int> map = null!;
    private int[] points = null!;
    private int[] assignStarts = null!;
    private int[] assignWidths = null!;
    private int[] assignValues = null!;
    private int domain;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        domain = ItemCount * RangeWidth;
        var rand = new Random(42);

        points = new int[OperationCount];
        assignStarts = new int[OperationCount];
        assignWidths = new int[OperationCount];
        assignValues = new int[OperationCount];
        for (int i = 0; i < OperationCount; i++)
        {
            points[i] = rand.Next(domain);
            assignStarts[i] = rand.Next(domain);
            assignWidths[i] = rand.Next(1, 3 * RangeWidth);
            assignValues[i] = rand.Next();
        }

        BuildBoth();
    }

    // ItemCount adjacent ranges tiling [0, domain), each with its own value so none of them merge.
    private void BuildBoth()
    {
        list = new List<Interval<int, int>>(ItemCount + (2 * OperationCount));
        map = new RangeMap<int, int>();
        for (int i = 0; i < ItemCount; i++)
        {
            list.Add(new Interval<int, int>(i * RangeWidth, (i + 1) * RangeWidth, i));
            map.Set(i * RangeWidth, (i + 1) * RangeWidth, i);
        }
    }

    // ---- Lookup: the value at a key — binary search over a flat array vs one B-tree descent ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int List_Lookup()
    {
        int sum = 0;
        for (int i = 0; i < points.Length; i++)
        {
            int index = FindCovering(list, points[i]);
            if (index >= 0)
                sum += list[index].Value;
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int RangeMap_Lookup()
    {
        int sum = 0;
        for (int i = 0; i < points.Length; i++)
        {
            if (map.TryGetValue(points[i], out int value))
                sum += value;
        }

        return sum;
    }

    // ---- Assign: overwrite a short range, splitting at both edges — O(n) memmove vs O(log n) ----

    [IterationSetup(Targets = new[] { nameof(List_Assign), nameof(RangeMap_Assign) })]
    public void SetupForAssign() => BuildBoth();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Assign")]
    public int List_Assign()
    {
        for (int i = 0; i < assignStarts.Length; i++)
            ListAssign(list, assignStarts[i], assignStarts[i] + assignWidths[i], assignValues[i]);

        return list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Assign")]
    public int RangeMap_Assign()
    {
        for (int i = 0; i < assignStarts.Length; i++)
            map.Set(assignStarts[i], assignStarts[i] + assignWidths[i], assignValues[i]);

        return map.Count;
    }

    // ---- Overlap: every range touching a short window, reported whole ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Overlap")]
    public int List_Overlap()
    {
        int sum = 0;
        for (int i = 0; i < assignStarts.Length; i++)
        {
            int start = assignStarts[i];
            int end = start + (4 * RangeWidth);
            for (int j = FirstEndingAfter(list, start); j < list.Count && list[j].Start < end; j++)
                sum += list[j].Value;
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Overlap")]
    public int RangeMap_Overlap()
    {
        int sum = 0;
        for (int i = 0; i < assignStarts.Length; i++)
        {
            int start = assignStarts[i];
            foreach (Interval<int, int> range in map.EnumerateOverlapping(start, start + (4 * RangeWidth)))
                sum += range.Value;
        }

        return sum;
    }

    // ---- the hand-roll ----

    // The index of the range containing point, or -1.
    private static int FindCovering(List<Interval<int, int>> ranges, int point)
    {
        int index = FirstEndingAfter(ranges, point);
        return index < ranges.Count && ranges[index].Start <= point ? index : -1;
    }

    // The index of the first range whose end is past key. The ranges are disjoint and sorted, so their ends
    // ascend too and can be binary-searched directly.
    private static int FirstEndingAfter(List<Interval<int, int>> ranges, int key)
    {
        int lo = 0;
        int hi = ranges.Count;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (ranges[mid].End <= key)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    private static void ListAssign(List<Interval<int, int>> ranges, int start, int end, int value)
    {
        int first = FirstEndingAfter(ranges, start);
        int last = first;
        while (last < ranges.Count && ranges[last].Start < end)
            last++;

        // At most three ranges replace the [first, last) run: the left remainder of the first, the assignment,
        // and the right remainder of the last.
        Span<Interval<int, int>> replacement = stackalloc Interval<int, int>[3];
        int count = 0;
        if (first < last && ranges[first].Start < start)
            replacement[count++] = new Interval<int, int>(ranges[first].Start, start, ranges[first].Value);

        replacement[count++] = new Interval<int, int>(start, end, value);

        if (first < last && ranges[last - 1].End > end)
            replacement[count++] = new Interval<int, int>(end, ranges[last - 1].End, ranges[last - 1].Value);

        // Shift the tail exactly once for the difference, then write the replacement over [first, first + count).
        // Growing goes through SetCount and one span copy rather than an Insert per extra piece — a split that
        // turns one range into three would otherwise memmove the whole tail twice.
        int removed = last - first;
        if (removed > count)
        {
            ranges.RemoveRange(first + count, removed - count);
        }
        else if (removed < count)
        {
            int oldCount = ranges.Count;
            CollectionsMarshal.SetCount(ranges, oldCount + count - removed);
            Span<Interval<int, int>> all = CollectionsMarshal.AsSpan(ranges);
            all.Slice(first + removed, oldCount - first - removed).CopyTo(all.Slice(first + count));
        }

        replacement.Slice(0, count).CopyTo(CollectionsMarshal.AsSpan(ranges).Slice(first, count));
    }
}
