using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="Trie{TValue}"/> and <see cref="SuccinctTrie{TValue}"/> routing examples published in
/// <c>docs/collections.md</c> and <c>docs/api/collections.md</c>, and asserts what their comments claim.
///
/// <para>
/// All three copies once named the <c>foreach</c>'s value <c>handler</c>, the name of the <c>out</c> variable in the
/// <c>TryGetLongestPrefix</c> call below it. An <c>out</c> variable declared in an <c>if</c> condition belongs to the
/// enclosing block, so the two clashed (CS0136) and none of the examples compiled.
/// </para>
/// </summary>
public class TrieDocumentationExampleTests
{
    [Fact]
    public void TrieExample_ShouldListUnderThePrefixInOrder_AndRouteToTheLongestPrefix()
    {
        // ---- the published example: keep in sync with docs/collections.md and docs/api/collections.md -----
        //
        // var routes = new Trie<string>();
        // routes["/"] = "home";
        // routes["/api"] = "api-root";
        // routes["/api/v1/users"] = "users-v1";
        // routes["/api/v1/orders"] = "orders-v1";
        //
        // // Autocomplete: every entry under a prefix, already sorted.
        // foreach (var (path, target) in routes.GetByPrefix("/api/v1/"))
        //     Console.WriteLine($"{path} -> {target}"); // /api/v1/orders, then /api/v1/users
        //
        // // Longest-prefix routing: the most specific stored route that prefixes the request.
        // if (routes.TryGetLongestPrefix("/api/v1/users/42", out string? route, out string? handler))
        //     Console.WriteLine($"{route} -> {handler}"); // /api/v1/users -> users-v1
        //
        // ---------------------------------------------------------------------------------------------------

        var routes = new Trie<string>();
        routes["/"] = "home";
        routes["/api"] = "api-root";
        routes["/api/v1/users"] = "users-v1";
        routes["/api/v1/orders"] = "orders-v1";

        var listed = new List<string>();
        foreach (var (path, target) in routes.GetByPrefix("/api/v1/"))
            listed.Add($"{path} -> {target}");
        Assert.Equal(["/api/v1/orders -> orders-v1", "/api/v1/users -> users-v1"], listed);

        Assert.True(routes.TryGetLongestPrefix("/api/v1/users/42", out string? route, out string? handler));
        Assert.Equal("/api/v1/users -> users-v1", $"{route} -> {handler}");
    }

    [Fact]
    public void SuccinctTrieExample_ShouldListUnderThePrefixInOrder_AndRouteToTheLongestPrefix()
    {
        // ---- the published example: keep in sync with docs/api/collections.md -----------------------------
        //
        // var routes = new SuccinctTrie<string>(
        // [
        //     new("/",                "home"),
        //     new("/api",             "api-root"),
        //     new("/api/v1/users",    "users-v1"),
        //     new("/api/v1/orders",   "orders-v1"),
        // ]);
        //
        // foreach (var (path, target) in routes.GetByPrefix("/api/v1/"))
        //     Console.WriteLine($"{path} -> {target}");         // /api/v1/orders, then /api/v1/users
        //
        // if (routes.TryGetLongestPrefix("/api/v1/users/42", out string? route, out string? handler))
        //     Console.WriteLine($"matched {route} -> {handler}"); // matched /api/v1/users -> users-v1
        //
        // ---------------------------------------------------------------------------------------------------

        var routes = new SuccinctTrie<string>(
        [
            new("/",                "home"),
            new("/api",             "api-root"),
            new("/api/v1/users",    "users-v1"),
            new("/api/v1/orders",   "orders-v1"),
        ]);

        var listed = new List<string>();
        foreach (var (path, target) in routes.GetByPrefix("/api/v1/"))
            listed.Add($"{path} -> {target}");
        Assert.Equal(["/api/v1/orders -> orders-v1", "/api/v1/users -> users-v1"], listed);

        Assert.True(routes.TryGetLongestPrefix("/api/v1/users/42", out string? route, out string? handler));
        Assert.Equal("matched /api/v1/users -> users-v1", $"matched {route} -> {handler}");
    }
}
