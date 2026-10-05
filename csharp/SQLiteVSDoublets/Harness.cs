using System.Diagnostics;
using System.Numerics;
using System.Text.Json.Nodes;
using Platform.Data;
using Platform.Data.Doublets;
using Platform.Data.Doublets.Memory;
using Platform.Data.Doublets.Memory.Split.Generic;
using Platform.Data.Doublets.Memory.United.Generic;
using Platform.Memory;
using static Comparisons.SQLiteVSDoublets.Dataset;

namespace Comparisons.SQLiteVSDoublets;

/// <summary>
/// Runs every storage through the same lifecycle on a fresh store per repetition:
/// a timed batch per operation, each validated against the expected count and checksum.
/// </summary>
public static class Harness
{
    public static readonly string[] LinksVariants =
    [
        "SQLite_Memory",
        "SQLite_File",
        "Doublets_United_Volatile",
        "Doublets_United_NonVolatile",
        "Doublets_Split_Volatile",
        "Doublets_Split_NonVolatile",
    ];

    public static readonly string[] ObjectsVariants =
    [
        "SQLite_Memory",
        "SQLite_File",
        "Doublets_United_Volatile_Cached",
        "Doublets_United_Volatile_Uncached",
        "Doublets_United_NonVolatile_Cached",
        "Doublets_United_NonVolatile_Uncached",
        "Doublets_Split_Volatile_Cached",
        "Doublets_Split_Volatile_Uncached",
        "Doublets_Split_NonVolatile_Cached",
        "Doublets_Split_NonVolatile_Uncached",
    ];

    public static ulong WarmUpSize => 10_000;

    /// <summary>Count and order-sensitive checksum of the records an operation has seen.</summary>
    public record struct Tally(ulong Count, ulong Checksum)
    {
        public void Add(ulong checksum)
        {
            Count++;
            Checksum += checksum;
        }

        public void AddLink<T>(Link<T> link) where T : IBinaryInteger<T> =>
            Add(LinkChecksum(ulong.CreateChecked(link.Id), ulong.CreateChecked(link.From), ulong.CreateChecked(link.To)));
    }

    private static void Timed(List<(string, TimeSpan)> timings, string operation, Tally expected, Func<Tally> work)
    {
        var stopwatch = Stopwatch.StartNew();
        var actual = work();
        timings.Add((operation, stopwatch.Elapsed));
        Check(actual == expected, $"{operation} returned unexpected records: {actual} instead of {expected}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static List<(string, TimeSpan)> LinksLifecycle<T>(ILinksStorage<T> storage, ulong n, Action created)
        where T : struct, IBinaryInteger<T>
    {
        static T Int(ulong value) => T.CreateChecked(value);
        var expected = new Tally(n, LinksChecksum(n, false));
        var timings = new List<(string, TimeSpan)>();

        Timed(timings, "create", expected, () => storage.Transaction(() =>
        {
            var tally = new Tally();
            for (ulong i = 1; i <= n; i++)
            {
                var (from, to) = Link(i);
                var id = storage.Create(Int(from), Int(to));
                tally.AddLink(new Link<T>(id, Int(from), Int(to)));
            }
            return tally;
        }));
        created();

        Timed(timings, "query_all", expected, () => storage.Transaction(() =>
        {
            var tally = new Tally();
            storage.Each(link => tally.AddLink(link));
            return tally;
        }));
        Timed(timings, "query_by_id", expected, () => storage.Transaction(() =>
        {
            var tally = new Tally();
            foreach (var id in Scattered(n))
            {
                tally.AddLink(storage.Get(Int(id))!.Value);
            }
            return tally;
        }));
        Timed(timings, "query_by_from_to", expected, () => storage.Transaction(() =>
        {
            var tally = new Tally();
            foreach (var i in Scattered(n))
            {
                var (from, to) = Link(i);
                var id = storage.Search(Int(from), Int(to))!.Value;
                tally.AddLink(new Link<T>(id, Int(from), Int(to)));
            }
            return tally;
        }));
        Timed(timings, "query_by_from", expected, () => storage.Transaction(() =>
        {
            var tally = new Tally();
            foreach (var from in Scattered(n))
            {
                storage.EachWithFrom(Int(from), link => tally.AddLink(link));
            }
            return tally;
        }));
        Timed(timings, "query_by_to", expected, () => storage.Transaction(() =>
        {
            var tally = new Tally();
            foreach (var to in Scattered(n))
            {
                storage.EachWithTo(Int(to), link => tally.AddLink(link));
            }
            return tally;
        }));

        var swapped = new Tally(n, LinksChecksum(n, true));
        Timed(timings, "update", swapped, () => storage.Transaction(() =>
        {
            var tally = new Tally();
            foreach (var id in Scattered(n))
            {
                var (newTo, newFrom) = Link(id);
                storage.Update(Int(id), Int(newFrom), Int(newTo));
                tally.Add(LinkChecksum(id, newFrom, newTo));
            }
            return tally;
        }));
        var stored = new Tally();
        storage.Each(link => stored.AddLink(link));
        Check(stored == swapped, "update did not swap every link");

        Timed(timings, "delete", new Tally(n, 0), () => storage.Transaction(() =>
        {
            var tally = new Tally();
            foreach (var id in Scattered(n))
            {
                storage.Delete(Int(id));
                tally.Add(0);
            }
            return tally;
        }));
        Check(storage.Count() == 0, "delete left links behind");
        return timings;
    }

    public static List<(string, TimeSpan)> ObjectsLifecycle<T>(IBlogPostsStorage<T> storage, ulong n, Action created)
        where T : struct, IBinaryInteger<T>
    {
        var expected = new Tally(n, BlogPostsChecksum(n));
        var timings = new List<(string, TimeSpan)>();
        var ids = new List<T>((int)n);

        Timed(timings, "create", expected, () => storage.Transaction(() =>
        {
            var tally = new Tally();
            for (ulong i = 1; i <= n; i++)
            {
                var post = BlogPost(i);
                ids.Add(storage.Create(post));
                tally.Add(post.Checksum);
            }
            return tally;
        }));
        created();

        Timed(timings, "read_all", expected, () => storage.Transaction(() =>
        {
            var tally = new Tally();
            storage.Each((_, post) => tally.Add(post.Checksum));
            return tally;
        }));
        Timed(timings, "read_by_id", new Tally(n, BlogPostsByNumberChecksum(n)), () => storage.Transaction(() =>
        {
            var tally = new Tally();
            foreach (var i in Scattered(n))
            {
                var post = storage.Get(ids[(int)i - 1])!;
                tally.Add(Mix(i, post.Checksum));
            }
            return tally;
        }));
        Timed(timings, "delete", new Tally(n, 0), () => storage.Transaction(() =>
        {
            var tally = new Tally();
            foreach (var i in Scattered(n))
            {
                storage.Delete(ids[(int)i - 1]);
                tally.Add(0);
            }
            return tally;
        }));
        Check(storage.Count() == 0, "delete left blog posts behind");
        return timings;
    }

    public sealed record Measurement(string Variant, ulong? FileBytes, List<(string Operation, List<double> Samples)> Operations)
    {
        public JsonObject ToJson()
        {
            var operations = new JsonObject();
            foreach (var (operation, samples) in Operations)
            {
                var sorted = samples.Order().ToList();
                var middle = sorted.Count / 2;
                var median = sorted.Count % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
                operations[operation] = new JsonObject
                {
                    ["median_ns"] = median,
                    ["min_ns"] = sorted[0],
                    ["max_ns"] = sorted[^1],
                    ["samples_ns"] = new JsonArray(samples.Select(sample => (JsonNode)sample).ToArray()),
                };
            }
            return new JsonObject { ["variant"] = Variant, ["file_bytes"] = FileBytes, ["operations"] = operations };
        }
    }

    private static ulong DirectoryBytes(string directory) =>
        (ulong)new DirectoryInfo(directory).GetFiles().Sum(file => file.Length);

    /// <summary>
    /// Times <paramref name="lifecycle"/> on <paramref name="repetitions"/> fresh stores opened in empty directories,
    /// after one discarded warm-up.
    /// </summary>
    private static Measurement Measure<TStorage>(
        string variant,
        ulong n,
        int repetitions,
        string directory,
        Func<string, TStorage> open,
        Func<TStorage, ulong, Action, List<(string, TimeSpan)>> lifecycle)
        where TStorage : IDisposable
    {
        var measurement = new Measurement(variant, null, []);
        for (var repetition = 0; repetition <= repetitions; repetition++)
        {
            var size = repetition == 0 ? Math.Min(n, WarmUpSize) : n;
            var workspace = Path.Combine(directory, $"{variant}-{repetition}");
            Directory.CreateDirectory(workspace);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            ulong fileBytes = 0;
            List<(string, TimeSpan)> timings;
            using (var storage = open(workspace))
            {
                timings = lifecycle(storage, size, () => fileBytes = DirectoryBytes(workspace));
            }
            Directory.Delete(workspace, true);
            if (repetition == 0)
            {
                continue;
            }
            Console.Error.WriteLine($"{variant} #{repetition}: {Summary(timings, n)}");
            measurement = measurement with { FileBytes = fileBytes > 0 ? fileBytes : null };
            foreach (var (operation, elapsed) in timings)
            {
                var nsPerOperation = elapsed.TotalNanoseconds / n;
                var index = measurement.Operations.FindIndex(entry => entry.Operation == operation);
                if (index < 0)
                {
                    measurement.Operations.Add((operation, [nsPerOperation]));
                }
                else
                {
                    measurement.Operations[index].Samples.Add(nsPerOperation);
                }
            }
        }
        return measurement;
    }

    private static string Summary(List<(string Operation, TimeSpan Elapsed)> timings, ulong n) =>
        string.Join(", ", timings.Select(timing => $"{timing.Operation} {timing.Elapsed.TotalNanoseconds / n:F0} ns/op"));

    private static string File(string directory, string name) => Path.Combine(directory, name);

    private static FileMappedResizableDirectMemory Mapped(string directory, string name) => new(File(directory, name));

    /// <summary>Objects store numbers and Unicode symbols as raw numbers, which are external references.</summary>
    /// <remarks>
    /// The size balanced index trees (the default) degenerate when many links share a source or a target,
    /// which makes every blog post creation linear in the number of stored posts;
    /// the AVL trees stay logarithmic (see experiments/csharp_objects_profile).
    /// </remarks>
    public static ILinks<T> United<T>(IResizableDirectMemory memory, bool external) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T> =>
        new UnitedMemoryLinks<T>(memory, UnitedMemoryLinks<T>.DefaultLinksSizeStep, new LinksConstants<T>(external), IndexTreeType.SizedAndThreadedAVLBalancedTree);

    public static ILinks<T> Split<T>(IResizableDirectMemory data, IResizableDirectMemory index, bool external) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T> =>
        new SplitMemoryLinks<T>(data, index, SplitMemoryLinks<T>.DefaultLinksSizeStep, new LinksConstants<T>(external));

    public static Measurement MeasureLinks<T>(string variant, ulong n, int repetitions, string directory)
        where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        Func<string, ILinksStorage<T>> open = variant switch
        {
            "SQLite_Memory" => _ => SQLiteLinks<T>.InMemory(),
            "SQLite_File" => dir => SQLiteLinks<T>.Open(File(dir, "links.db")),
            "Doublets_United_Volatile" => _ => new DoubletsLinks<T>(United<T>(new HeapResizableDirectMemory(), external: false)),
            "Doublets_United_NonVolatile" => dir => new DoubletsLinks<T>(United<T>(Mapped(dir, "links.links"), external: false)),
            "Doublets_Split_Volatile" => _ => new DoubletsLinks<T>(Split<T>(new HeapResizableDirectMemory(), new HeapResizableDirectMemory(), external: false)),
            "Doublets_Split_NonVolatile" => dir => new DoubletsLinks<T>(Split<T>(Mapped(dir, "data.links"), Mapped(dir, "index.links"), external: false)),
            _ => throw new ArgumentException($"unknown links variant {variant}"),
        };
        return Measure(variant, n, repetitions, directory, open, LinksLifecycle);
    }

    public static Measurement MeasureObjects<T>(string variant, ulong n, int repetitions, string directory)
        where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        var cached = variant.EndsWith("_Cached");
        Func<string, IBlogPostsStorage<T>> open = variant.Replace("_Uncached", "").Replace("_Cached", "") switch
        {
            "SQLite_Memory" => _ => SQLiteBlogPosts<T>.InMemory(),
            "SQLite_File" => dir => SQLiteBlogPosts<T>.Open(File(dir, "blog_posts.db")),
            "Doublets_United_Volatile" => _ => new DoubletsBlogPosts<T>(United<T>(new HeapResizableDirectMemory(), external: true), cached),
            "Doublets_United_NonVolatile" => dir => new DoubletsBlogPosts<T>(United<T>(Mapped(dir, "links.links"), external: true), cached),
            "Doublets_Split_Volatile" => _ => new DoubletsBlogPosts<T>(Split<T>(new HeapResizableDirectMemory(), new HeapResizableDirectMemory(), external: true), cached),
            "Doublets_Split_NonVolatile" => dir => new DoubletsBlogPosts<T>(Split<T>(Mapped(dir, "data.links"), Mapped(dir, "index.links"), external: true), cached),
            _ => throw new ArgumentException($"unknown objects variant {variant}"),
        };
        return Measure(variant, n, repetitions, directory, open, ObjectsLifecycle);
    }
}
