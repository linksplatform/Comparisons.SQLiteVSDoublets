// Profiles which ILinks calls make C# Doublets blog post creation grow with the number of stored posts.
// Usage: dotnet run -c Release -- <united|split> <n> [united index tree type] [internal]
// Wraps the store in a decorator that counts and times Each, Count, Create, Update and Delete calls and
// prints them per created post for the first and the last tenth of the posts.
// Results with Platform.Data.Doublets 0.18.1 and 10,000 posts: only the update that attaches a post to its
// type marker grows, so the united store's size balanced trees degenerate when thousands of links share a source.
// Mean time of that update for posts 1..1,000 -> 9,001..10,000:
//   united Default                          27 µs -> 322 µs
//   united SizeBalancedTree                 82 µs -> 369 µs
//   united RecursionlessSizeBalancedTree    47 µs -> 742 µs
//   united SizedAndThreadedAVLBalancedTree  22 µs -> 2.2 µs
//   split                                  0.9 µs -> 0.2 µs
// `internal` (no external references) degenerates the same way (Default: 50 µs -> 584 µs), and plain hubs
// without the rest of the blog post links do not, so the benchmarks use the AVL trees for the united store.
using System.Diagnostics;
using Comparisons.SQLiteVSDoublets;
using Platform.Data;
using Platform.Data.Doublets;
using Platform.Data.Doublets.Memory;
using Platform.Data.Doublets.Memory.United.Generic;
using Platform.Delegates;
using Platform.Memory;

var kind = args[0];
var n = int.Parse(args[1]);
Console.WriteLine($"tree types: {string.Join(", ", Enum.GetNames<IndexTreeType>())}");
ILinks<ulong> store = kind == "united"
    ? new UnitedMemoryLinks<ulong>(new HeapResizableDirectMemory(), UnitedMemoryLinks<ulong>.DefaultLinksSizeStep,
        new LinksConstants<ulong>(args.Length <= 3 || args[3] != "internal"), args.Length > 2 ? Enum.Parse<IndexTreeType>(args[2]) : IndexTreeType.Default)
    : Harness.Split<ulong>(new HeapResizableDirectMemory(), new HeapResizableDirectMemory(), external: true);
var profiled = new Profiled(store);
var posts = new DoubletsBlogPosts<ulong>(profiled, cacheSequences: true);
var tenth = n / 10;
for (var i = 1; i <= n; i++)
{
    if (i == 1 || i == n - tenth + 1)
    {
        profiled.Reset();
    }
    posts.Create(Dataset.BlogPost((ulong)i));
    if (i == tenth || i == n)
    {
        Console.WriteLine($"{kind} posts {i - tenth + 1}..{i}: {profiled.Report(tenth)}");
    }
}

sealed class Profiled(ILinks<ulong> links) : ILinks<ulong>
{
    private readonly Dictionary<string, (long Calls, TimeSpan Time)> _calls = [];

    public LinksConstants<ulong> Constants => links.Constants;

    public void Reset() => _calls.Clear();

    public string Report(int posts) => string.Join(", ", _calls.Select(call =>
        $"{call.Key} {(double)call.Value.Calls / posts:F1} calls {call.Value.Time.TotalNanoseconds / posts:F0} ns"));

    private TResult Timed<TResult>(string name, Func<TResult> work)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = work();
        var (calls, time) = _calls.GetValueOrDefault(name);
        _calls[name] = (calls + 1, time + stopwatch.Elapsed);
        return result;
    }

    private static string Shape(IList<ulong>? restriction, ulong any) =>
        restriction is null ? "null" : string.Concat(restriction.Select(value => value == any ? "*" : "v"));

    public ulong Count(IList<ulong>? restriction) => Timed($"Count[{Shape(restriction, Constants.Any)}]", () => links.Count(restriction));

    public ulong Each(IList<ulong>? restriction, ReadHandler<ulong>? handler) =>
        Timed($"Each[{Shape(restriction, Constants.Any)}]", () => links.Each(restriction, handler));

    public ulong Create(IList<ulong>? substitution, WriteHandler<ulong>? handler) => Timed("Create", () => links.Create(substitution, handler));

    public ulong Update(IList<ulong>? restriction, IList<ulong>? substitution, WriteHandler<ulong>? handler) =>
        Timed($"Update[{Kind(substitution![1])},{Kind(substitution[2])}]", () => links.Update(restriction, substitution, handler));

    private string Kind(ulong address) => Constants.IsExternalReference(address) ? "raw" : address <= 7 ? $"marker{address}" : "link";

    public ulong Delete(IList<ulong>? restriction, WriteHandler<ulong>? handler) => Timed("Delete", () => links.Delete(restriction, handler));
}
