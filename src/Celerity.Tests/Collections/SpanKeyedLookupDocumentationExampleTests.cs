using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the span-keyed route lookup and <see cref="StringInternTable"/> usage examples published in
/// <c>docs/api/collections.md</c> and <c>docs/collections.md</c>, and asserts what their comments claim.
///
/// <para>
/// A published example is documentation a reader will copy, so a wrong one is a defect. The route example once
/// sliced 14 characters out of the request line for a 13-character path: the slice carried the space after the
/// path, the lookup missed, and the example never dispatched.
/// </para>
/// </summary>
public class SpanKeyedLookupDocumentationExampleTests
{
    [Fact]
    public void RouteExample_ShouldFindTheHandler_WhenProbedWithTheSlicedPath()
    {
        // ---- the published example: keep in sync with docs/api/collections.md and docs/collections.md -----
        //
        // var routes = new FrozenCelerityDictionary<int, StringXxHash3Hasher>(
        // [
        //     new("/api/v1/users",  1),
        //     new("/api/v1/orders", 2),
        // ]);
        //
        // ReadOnlySpan<char> line = "GET /api/v1/users HTTP/1.1".AsSpan();
        // ReadOnlySpan<char> path = line.Slice(4, 13);
        //
        // if (routes.TryGetValue(path, out int handler))   // no string allocated
        //     Dispatch(handler);
        //
        // ---------------------------------------------------------------------------------------------------

        var routes = new FrozenCelerityDictionary<int, StringXxHash3Hasher>(
        [
            new("/api/v1/users",  1),
            new("/api/v1/orders", 2),
        ]);

        ReadOnlySpan<char> line = "GET /api/v1/users HTTP/1.1".AsSpan();
        ReadOnlySpan<char> path = line.Slice(4, 13);

        Assert.Equal("/api/v1/users", path.ToString());
        Assert.True(routes.TryGetValue(path, out int handler));
        Assert.Equal(1, handler);

        // "no string allocated": the probe finds the route without building the path as a string. A lambda
        // cannot capture the span, so it slices the same literal again.
        Assert.Equal(0, AllocationProbe.Quietest(() => routes.TryGetValue("GET /api/v1/users HTTP/1.1".AsSpan(4, 13), out _)));
    }

    [Fact]
    public void InternExample_ShouldReturnOneSharedStringPerDistinctToken()
    {
        // ---- the published example: keep in sync with docs/api/collections.md and docs/collections.md -----
        //
        // var interned = new StringInternTable();
        //
        // foreach (ReadOnlySpan<char> cell in SplitCells(line))
        // {
        //     string token = interned.GetOrAdd(cell);   // allocates only the first time each token is seen
        //     Consume(token);
        // }
        //
        // Console.WriteLine(interned.Count);            // distinct tokens, == strings allocated
        //
        // ---------------------------------------------------------------------------------------------------

        const string Line = "buy,sell,buy,hold,sell,buy";
        var interned = new StringInternTable();
        var tokens = new List<string>();

        foreach (Range cell in Line.AsSpan().Split(','))
            tokens.Add(interned.GetOrAdd(Line.AsSpan()[cell]));

        Assert.Equal(["buy", "sell", "buy", "hold", "sell", "buy"], tokens);
        Assert.Equal(3, interned.Count);    // buy, sell, hold
        Assert.Same(tokens[0], tokens[2]);  // a repeated token comes back as the first occurrence's string
        Assert.Same(tokens[0], tokens[5]);
        Assert.Same(tokens[1], tokens[4]);

        // "allocates only the first time each token is seen": a token already in the table costs nothing.
        Assert.Equal(0, AllocationProbe.Quietest(() => interned.GetOrAdd("buy".AsSpan())));
    }
}
