using System;
using System.IO;
using Platform.Data.Doublets;
using Platform.Data.Doublets.Memory.Split.Generic;
using Platform.Data.Doublets.Memory.United.Generic;
using Platform.Memory;

namespace Comparisons.SQLiteVSDoublets.Links;

public enum LinksVariant
{
    SQLite_Memory,
    SQLite_File,
    Doublets_United_Volatile,
    Doublets_United_NonVolatile,
    Doublets_Split_Volatile,
    Doublets_Split_NonVolatile,
}

public static class LinksStorageFactory
{
    public static ILinksStorage Create(LinksVariant variant) => variant switch
    {
        LinksVariant.SQLite_Memory => new SQLiteLinksStorage(inMemory: true),
        LinksVariant.SQLite_File => new SQLiteLinksStorage(inMemory: false),
        LinksVariant.Doublets_United_Volatile => new DoubletsLinksStorage(
            new UnitedMemoryLinks<uint>(new HeapResizableDirectMemory())),
        LinksVariant.Doublets_United_NonVolatile => CreateUnitedFile(),
        LinksVariant.Doublets_Split_Volatile => new DoubletsLinksStorage(
            new SplitMemoryLinks<uint>(
                new HeapResizableDirectMemory(),
                new HeapResizableDirectMemory())),
        LinksVariant.Doublets_Split_NonVolatile => CreateSplitFile(),
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
    };

    private static DoubletsLinksStorage CreateUnitedFile()
    {
        var path = TemporaryPath("united.links");
        ILinks<uint> links = new UnitedMemoryLinks<uint>(path);
        return new DoubletsLinksStorage(links, path);
    }

    private static DoubletsLinksStorage CreateSplitFile()
    {
        var dataPath = TemporaryPath("split.data.links");
        var indexPath = TemporaryPath("split.index.links");
        ILinks<uint> links = new SplitMemoryLinks<uint>(dataPath, indexPath);
        return new DoubletsLinksStorage(links, dataPath, indexPath);
    }

    private static string TemporaryPath(string suffix) => Path.Combine(
        Path.GetTempPath(),
        $"sqlite-vs-doublets-{Guid.NewGuid():N}-{suffix}");
}
