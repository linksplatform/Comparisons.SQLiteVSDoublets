// The smallest store found by experiments/csharp_tree_delete in which `links.Delete(id)` loses other links.
// Five links are created, link 4 (2 1) is deleted, and the links (2 2) and (2 3) are no longer found by
// SearchOrDefault. The Platform.Data.Doublets overload Delete(id, handler) resets the link first and keeps them.
// Usage: dotnet run -c Release
// Results with Platform.Data.Doublets 1.0.0:
//   Delete(4): count = 4, search(1, 1) = 1, search(2, 2) = 0, search(1, 2) = 3, search(2, 3) = 0
//   Delete(4, handler: null): count = 4, search(1, 1) = 1, search(2, 2) = 2, search(1, 2) = 3, search(2, 3) = 5
using Platform.Data;
using Platform.Data.Doublets;
using Platform.Data.Doublets.Memory.United.Generic;
using Platform.Memory;

foreach (var reset in new[] { false, true })
{
    using var links = new UnitedMemoryLinks<ulong>(new HeapResizableDirectMemory());
    foreach (var (source, target) in new (ulong, ulong)[] { (1, 1), (2, 2), (1, 2), (2, 1), (2, 3) })
    {
        links.CreateAndUpdate(source, target); // ids 1..5
    }
    if (reset)
    {
        Platform.Data.Doublets.ILinksExtensions.Delete(links, 4UL, null); // resets (2 1) to (0 0) first
    }
    else
    {
        links.Delete(4UL); // Platform.Data.ILinksExtensions.Delete
    }
    Console.WriteLine($"{(reset ? "Delete(4, handler: null)" : "Delete(4)")}: count = {links.Count()}, " +
        $"search(1, 1) = {links.SearchOrDefault(1UL, 1UL)}, search(2, 2) = {links.SearchOrDefault(2UL, 2UL)}, " +
        $"search(1, 2) = {links.SearchOrDefault(1UL, 2UL)}, search(2, 3) = {links.SearchOrDefault(2UL, 3UL)}");
}
