using Platform.Memory;
using Xunit;
using static Comparisons.SQLiteVSDoublets.Dataset;

namespace Comparisons.SQLiteVSDoublets.Tests;

public class StoragesTests
{
    private const ulong Size = 1_000;

    [Fact]
    public void LinksAreUniqueAndReferenceCreatedIds()
    {
        var links = new HashSet<(ulong, ulong)>();
        for (ulong i = 1; i <= Size; i++)
        {
            var (from, to) = Link(i);
            Assert.InRange(from, 1UL, i);
            Assert.InRange(to, 1UL, i);
            links.Add((from, to));
        }
        Assert.Equal((int)Size, links.Count);
    }

    [Theory]
    [InlineData(1UL), InlineData(2UL), InlineData(3UL), InlineData(10UL), InlineData(1_000UL), InlineData(1_024UL), InlineData(65_536UL)]
    public void ScatteredVisitsEveryIdOnce(ulong n) =>
        Assert.Equal(Enumerable.Range(1, (int)n).Select(i => (ulong)i), Scattered(n).Order());

    public static TheoryData<string> LinksVariants => [.. Harness.LinksVariants];

    public static TheoryData<string> ObjectsVariants => [.. Harness.ObjectsVariants];

    [Theory, MemberData(nameof(LinksVariants))]
    public void EveryLinksVariantPassesTheLifecycle(string variant)
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        Harness.MeasureLinks<uint>(variant, Size, 1, directory);
        Harness.MeasureLinks<ulong>(variant, Size, 1, directory);
    }

    [Theory, MemberData(nameof(ObjectsVariants))]
    public void EveryObjectsVariantPassesTheLifecycle(string variant)
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        Harness.MeasureObjects<uint>(variant, Size / 10, 1, directory);
        Harness.MeasureObjects<ulong>(variant, Size / 10, 1, directory);
    }

    [Fact]
    public void SplitStoreKeepsLinksUpdatedToReferenceThemselves()
    {
        using var links = new DoubletsLinks<uint>(Harness.Split<uint>(new HeapResizableDirectMemory(), new HeapResizableDirectMemory(), external: false));
        var point = links.Create(1, 1);
        var link = links.Create(point, 2);
        links.Update(point, point, point);
        links.Update(link, link, point);
        Assert.Equal(new Link<uint>(link, link, point), links.Get(link));
        links.Delete(link);
        links.Delete(point);
        Assert.Equal(0UL, links.Count());
    }

    private sealed class Lossy(SQLiteLinks<uint> links) : ILinksStorage<uint>
    {
        public uint Create(uint from, uint to) => links.Create(from, to);
        public void Update(uint id, uint from, uint to) => links.Update(id, from, to);
        public void Delete(uint id) => links.Delete(id);
        public Link<uint>? Get(uint id) => links.Get(id);
        public uint? Search(uint from, uint to) => links.Search(from, to);
        public void Each(Action<Link<uint>> visit) => links.EachWithFrom(1, visit);
        public void EachWithFrom(uint from, Action<Link<uint>> visit) => links.EachWithFrom(from, visit);
        public void EachWithTo(uint to, Action<Link<uint>> visit) => links.EachWithTo(to, visit);
        public ulong Count() => links.Count();
        public void Dispose() => links.Dispose();
    }

    [Fact]
    public void LifecycleRejectsAStorageThatLosesRecords()
    {
        using var lossy = new Lossy(SQLiteLinks<uint>.InMemory());
        var error = Assert.Throws<InvalidOperationException>(() => Harness.LinksLifecycle(lossy, Size, () => { }));
        Assert.StartsWith("query_all returned unexpected records", error.Message);
    }

    private static void RoundTrip<T>(IBlogPostsStorage<T> posts) where T : struct
    {
        using var _ = posts;
        var inputs = Enumerable.Range(1, 20).Select(i => BlogPost((ulong)i)).ToList();
        inputs.Add(new BlogPost("Ünïcödé 🌍 ✓", "a", 0));
        inputs.Add(new BlogPost("", "ab", uint.MaxValue / 2));
        var ids = inputs.Select(posts.Create).ToList();
        Assert.Equal(inputs, ids.Select(id => posts.Get(id)));
        var stored = new List<(T, BlogPost)>();
        posts.Each((id, post) => stored.Add((id, post)));
        Assert.Equal(ids.Zip(inputs), stored.OrderBy(entry => entry.Item1));
        ids.ForEach(posts.Delete);
        Assert.Equal(0UL, posts.Count());
    }

    [Theory, InlineData(false), InlineData(true)]
    public void EveryObjectsStorageReturnsTheStoredPosts(bool cached)
    {
        RoundTrip(SQLiteBlogPosts<uint>.InMemory());
        RoundTrip(SQLiteBlogPosts<ulong>.InMemory());
        RoundTrip(new DoubletsBlogPosts<uint>(Harness.United<uint>(new HeapResizableDirectMemory(), external: true), cached));
        RoundTrip(new DoubletsBlogPosts<ulong>(Harness.United<ulong>(new HeapResizableDirectMemory(), external: true), cached));
        RoundTrip(new DoubletsBlogPosts<uint>(Harness.Split<uint>(new HeapResizableDirectMemory(), new HeapResizableDirectMemory(), external: true), cached));
        RoundTrip(new DoubletsBlogPosts<ulong>(Harness.Split<ulong>(new HeapResizableDirectMemory(), new HeapResizableDirectMemory(), external: true), cached));
    }
}
