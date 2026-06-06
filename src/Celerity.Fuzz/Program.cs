using System.Diagnostics;
using Celerity.Fuzz;

// Differential fuzz driver for the Celerity collections (issue #29).
//
// Each "case" is one randomized operation sequence run in lock-step against a
// BCL oracle. A case is fully determined by a single 32-bit seed, so any failure
// CI reports can be replayed locally with:
//
//     dotnet run -c Release -- --seed <caseSeed> --iterations 1
//
// Usage:
//     --seed <int>          Base seed. Case i uses (seed + i). Defaults to a
//                           time-derived value, which is printed for replay.
//     --iterations <int>    Number of cases to run (default 100000).
//     --time <seconds>      Stop after this wall-clock budget, whichever comes
//                           first with --iterations. 0 = no time limit (default).
//     --target <name>       Restrict to one collection (changes the RNG stream,
//                           so omit it when replaying a reported caseSeed).
//     --list                Print the registered targets and exit.
//     --help                Print this help and exit.

int baseSeed = unchecked((int)(Stopwatch.GetTimestamp() & 0x7FFFFFFF));
long iterations = 100_000;
double timeBudgetSeconds = 0;
string? targetName = null;

for (int i = 0; i < args.Length; i++)
{
    string a = args[i];
    switch (a)
    {
        case "--help" or "-h":
            PrintHelp();
            return 0;
        case "--list":
            Console.WriteLine("Targets: " + Differential.TargetList());
            return 0;
        case "--seed":
            baseSeed = int.Parse(RequireValue(args, ref i, a));
            break;
        case "--iterations" or "--iters":
            iterations = long.Parse(RequireValue(args, ref i, a));
            break;
        case "--time":
            timeBudgetSeconds = double.Parse(RequireValue(args, ref i, a));
            break;
        case "--target":
            targetName = RequireValue(args, ref i, a);
            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {a}");
            PrintHelp();
            return 2;
    }
}

int? fixedTarget = null;
if (targetName is not null)
{
    int idx = Array.FindIndex(Differential.All, t => string.Equals(t.Name, targetName, StringComparison.OrdinalIgnoreCase));
    if (idx < 0)
    {
        Console.Error.WriteLine($"Unknown target '{targetName}'. Known: {Differential.TargetList()}");
        return 2;
    }
    fixedTarget = idx;
}

Console.WriteLine($"Celerity fuzz — baseSeed={baseSeed}, iterations={iterations}" +
    (timeBudgetSeconds > 0 ? $", time={timeBudgetSeconds}s" : "") +
    (targetName is not null ? $", target={targetName}" : ", target=all"));

var sw = Stopwatch.StartNew();
long done = 0;
long nextHeartbeat = 10_000;

for (long i = 0; i < iterations; i++)
{
    int caseSeed = unchecked(baseSeed + (int)i);
    var rng = new Random(caseSeed);
    int target = fixedTarget ?? rng.Next(Differential.All.Length);

    try
    {
        Differential.All[target].Run(rng);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("================ FUZZ FAILURE ================");
        Console.Error.WriteLine($"target   : {Differential.All[target].Name}");
        Console.Error.WriteLine($"caseSeed : {caseSeed}");
        Console.Error.WriteLine($"replay   : dotnet run -c Release -- --seed {caseSeed} --iterations 1");
        Console.Error.WriteLine($"detail   : {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine("==============================================");
        return 1;
    }

    done++;
    if (done >= nextHeartbeat)
    {
        Console.WriteLine($"  {done:N0} cases ok ({done / Math.Max(sw.Elapsed.TotalSeconds, 1e-9):N0}/s)");
        nextHeartbeat += 10_000;
    }

    if (timeBudgetSeconds > 0 && sw.Elapsed.TotalSeconds >= timeBudgetSeconds)
    {
        Console.WriteLine($"Time budget reached after {done:N0} cases.");
        break;
    }
}

Console.WriteLine($"OK — {done:N0} cases passed in {sw.Elapsed.TotalSeconds:N1}s (baseSeed={baseSeed}).");
return 0;

static string RequireValue(string[] args, ref int i, string flag)
{
    if (i + 1 >= args.Length)
    {
        Console.Error.WriteLine($"{flag} requires a value");
        Environment.Exit(2);
    }
    return args[++i];
}

static void PrintHelp()
{
    Console.WriteLine("""
        Celerity differential fuzzer

        Drives randomized operation sequences against each Celerity collection and a
        BCL oracle in lock-step, failing on the first observable divergence.

          --seed <int>        Base seed; case i uses (seed + i). Printed for replay.
          --iterations <int>  Number of cases (default 100000).
          --time <seconds>    Wall-clock budget; stops at the earlier of this or
                              --iterations. 0 = unlimited (default).
          --target <name>     Restrict to one collection (see --list). Changes the
                              RNG stream, so omit when replaying a reported caseSeed.
          --list              List registered targets.
          --help              This help.

        Replay a reported failure:
          dotnet run -c Release -- --seed <caseSeed> --iterations 1
        """);
}
