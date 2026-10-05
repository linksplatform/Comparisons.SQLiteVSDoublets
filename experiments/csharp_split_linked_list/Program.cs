// Compares the C# split store with and without the linked list of internal sources, which the Rust split store
// of doublets 0.5.0 does not use (USE_LIST = false), on the links and objects workloads of the benchmarks.
// Usage: dotnet run -c Release -- <links|objects> <n>
// Results with Platform.Data.Doublets 0.18.1 (ns per operation, the warm rounds 1 and 2): the list is the default,
// and it makes updates faster while the other operations stay close, so the benchmarks keep the default.
//   links 100,000, list:    create 588-703, query_by_id 388-459, query_by_from 553-624, update 1093-1120
//   links 100,000, no list: create 695-855, query_by_id 388-1094, query_by_from 485-541, update 1704-1863
//   objects 20,000 (cached), list: create 21-24 µs, read_by_id 9-11 µs; no list: create 24-26 µs, read_by_id 7-8 µs
using System.Reflection;
using Comparisons.SQLiteVSDoublets;
using Platform.Data;
using Platform.Data.Doublets.Memory;
using Platform.Data.Doublets.Memory.Split.Generic;
using Platform.Memory;

var category = args[0];
var n = ulong.Parse(args[1]);
var defaultStore = new SplitMemoryLinks<ulong>(new HeapResizableDirectMemory(), new HeapResizableDirectMemory(),
    SplitMemoryLinks<ulong>.DefaultLinksSizeStep, new LinksConstants<ulong>(false));
var field = typeof(SplitMemoryLinksBase<ulong>).GetField("_useLinkedList", BindingFlags.NonPublic | BindingFlags.Instance)!;
Console.WriteLine($"default useLinkedList: {field.GetValue(defaultStore)}");

for (var round = 0; round < 3; round++)
{
    foreach (var useLinkedList in new[] { true, false })
    {
        var store = new SplitMemoryLinks<ulong>(new HeapResizableDirectMemory(), new HeapResizableDirectMemory(),
            SplitMemoryLinks<ulong>.DefaultLinksSizeStep, new LinksConstants<ulong>(category == "objects"),
            IndexTreeType.Default, useLinkedList);
        var timings = category == "links"
            ? Harness.LinksLifecycle(new DoubletsLinks<ulong>(store), n, () => { })
            : Harness.ObjectsLifecycle(new DoubletsBlogPosts<ulong>(store, cacheSequences: true), n, () => { });
        Console.WriteLine($"round {round}, useLinkedList {useLinkedList,-5}: " +
            string.Join(", ", timings.Select(timing => $"{timing.Item1} {timing.Item2.TotalNanoseconds / n:F0}")));
    }
}
