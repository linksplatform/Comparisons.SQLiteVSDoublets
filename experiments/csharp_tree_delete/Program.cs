// Shows why the C# benchmarks delete links with `Platform.Data.Doublets.ILinksExtensions.Delete(links, id, handler)`.
// The `links.Delete(id)` overload from Platform.Data frees the link without detaching it from the source and
// target trees, so the trees keep a stale entry that soon hides other links from `SearchOrDefault`.
// The Platform.Data.Doublets overload resets the link to (0, 0) first, which detaches it.
// Usage: dotnet run -c Release [-- --self] [-- --reset]
// --self also creates links that reference themselves; --reset deletes with the resetting overload.
// Results with Platform.Data.Doublets 0.18.1 (random stores of 3..151 links, every unreferenced link deleted):
//   (no flags)      united: fails at 7 links,  split: fails at 13 links
//   --self          united: fails at 8 links,  split: fails at 10 links
//   --reset         united and split: no failure up to 151 links
//   --self --reset  united and split: no failure up to 151 links
using Platform.Data;
using Platform.Data.Doublets;
using Platform.Data.Doublets.Memory.Split.Generic;
using Platform.Data.Doublets.Memory.United.Generic;
using Platform.Memory;

var self = args.Contains("--self");
var reset = args.Contains("--reset");
foreach (var kind in new[] { "united", "split" })
{
    var failure = Enumerable.Range(3, 149).Select(n => TreeDelete.Run(kind, n, self, reset)).FirstOrDefault(failure => failure is not null);
    Console.WriteLine($"{kind}: {failure ?? "no failure up to 151 links"}");
}

static class TreeDelete
{
    public static string? Run(string kind, int n, bool self, bool reset)
    {
        ILinks<ulong> links = kind == "united"
            ? new UnitedMemoryLinks<ulong>(new HeapResizableDirectMemory())
            : new SplitMemoryLinks<ulong>(new HeapResizableDirectMemory(), new HeapResizableDirectMemory());
        // Every store preallocates its memory, so it must be released before the next run.
        using var disposable = (IDisposable)links;
        var random = new Random(n);
        var created = CreateRandomLinks(links, random, n, self);
        return DeleteUnreferenced(links, random, created, reset);
    }

    private static List<(ulong Id, ulong Source, ulong Target)> CreateRandomLinks(ILinks<ulong> links, Random random, int n, bool self)
    {
        // Two points, so that new (source, target) pairs exist without --self.
        var created = new List<(ulong Id, ulong Source, ulong Target)> { (links.CreatePoint(), 1, 1), (links.CreatePoint(), 2, 2) };
        while (created.Count < n)
        {
            var next = created[^1].Id + 1;
            var source = self && random.Next(4) == 0 ? next : created[random.Next(created.Count)].Id;
            var target = created[random.Next(created.Count)].Id;
            if (links.SearchOrDefault(source, target) == 0)
            {
                created.Add((links.CreateAndUpdate(source, target), source, target));
            }
        }
        return created;
    }

    private static string? DeleteUnreferenced(ILinks<ulong> links, Random random, List<(ulong Id, ulong Source, ulong Target)> created, bool reset)
    {
        var alive = created.ToDictionary(link => link.Id);
        foreach (var victim in created.OrderBy(_ => random.Next()))
        {
            if (alive.Values.Any(link => link.Id != victim.Id && (link.Source == victim.Id || link.Target == victim.Id)))
            {
                continue;
            }
            Delete(links, victim.Id, reset);
            alive.Remove(victim.Id);
            var lost = alive.Values.FirstOrDefault(link => links.SearchOrDefault(link.Source, link.Target) != link.Id);
            if (lost != default)
            {
                return $"{created.Count} links {string.Join(" ", created.Select(l => $"({l.Id}: {l.Source} {l.Target})"))}; after deleting {victim.Id} search({lost.Source}, {lost.Target}) = {links.SearchOrDefault(lost.Source, lost.Target)} instead of {lost.Id}";
            }
        }
        return null;
    }

    private static void Delete(ILinks<ulong> links, ulong id, bool reset)
    {
        if (reset)
        {
            links.Delete(id, handler: null);
        }
        else
        {
            links.Delete(id);
        }
    }
}
