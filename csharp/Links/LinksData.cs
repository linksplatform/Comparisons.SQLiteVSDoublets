using System;
using System.Collections.Generic;

namespace Comparisons.SQLiteVSDoublets.Links;

public static class LinksData
{
    public static uint[] FillBackground(ILinksStorage storage, int count)
    {
        var ids = new uint[count];
        for (var index = 0; index < count; index++)
        {
            ids[index] = storage.CreatePoint();
        }
        return ids;
    }

    public static LinkRecord[] FillBenchmarked(
        ILinksStorage storage,
        IReadOnlyList<uint> background,
        int count)
    {
        if (background.Count == 0)
        {
            throw new ArgumentException("Background links are required.", nameof(background));
        }

        var links = new LinkRecord[count];
        for (var index = 0; index < count; index++)
        {
            var source = background[index % background.Count];
            var target = background[(index % background.Count + 1 + index / background.Count)
                % background.Count];
            links[index] = new LinkRecord(storage.Create(source, target), source, target);
        }
        return links;
    }
}
