using System;
using System.Linq;
using Comparisons.SQLiteVSDoublets.Links;

namespace Comparisons.SQLiteVSDoublets;

public static class LinksSelfTest
{
    public static void Run()
    {
        foreach (var variant in Enum.GetValues<LinksVariant>())
        {
            Check(variant);
            Console.WriteLine($"{variant}: passed");
        }
    }

    private static void Check(LinksVariant variant)
    {
        using var storage = LinksStorageFactory.Create(variant);
        var background = LinksData.FillBackground(storage, 8);
        var created = LinksData.FillBenchmarked(storage, background, 16);
        Ensure(storage.Count == 24, $"{variant}: unexpected link count");
        Ensure(storage.QueryAll().Count == 24, $"{variant}: query all failed");

        var sample = created[created.Length / 2];
        Ensure(storage.QueryById(sample.Id) == sample, $"{variant}: identity query failed");
        Ensure(
            storage.QueryBySourceTarget(sample.Source, sample.Target).Contains(sample),
            $"{variant}: concrete query failed");
        Ensure(
            storage.QueryBySource(sample.Source).Contains(sample),
            $"{variant}: outgoing query failed");
        Ensure(
            storage.QueryByTarget(sample.Target).Contains(sample),
            $"{variant}: incoming query failed");

        var unused = storage.CreatePoint();
        storage.Update(sample.Id, unused, background[0]);
        var updated = storage.QueryById(sample.Id);
        Ensure(
            updated is { Source: var source, Target: var target }
                && source == unused
                && target == background[0],
            $"{variant}: update failed");

        storage.Delete(sample.Id);
        Ensure(storage.QueryById(sample.Id) is null, $"{variant}: delete failed");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
