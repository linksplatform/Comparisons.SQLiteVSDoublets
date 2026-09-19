using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Comparisons.SQLiteVSDoublets.Links;

namespace Comparisons.SQLiteVSDoublets;

[MemoryDiagnoser]
public class LinksBenchmarks
{
    private ILinksStorage _storage = null!;
    private uint[] _background = null!;
    private uint[] _updateSources = null!;
    private LinkRecord[] _links = null!;

    [ParamsAllValues]
    public LinksVariant Variant { get; set; }

    [ParamsSource(nameof(LinkCounts))]
    public int N { get; set; }

    public IEnumerable<int> LinkCounts => new[] { EnvironmentValue("BENCHMARK_LINK_COUNT", 1000) };

    [IterationSetup(Target = nameof(Create))]
    public void SetupCreate() => Setup(createBenchmarked: false);

    [IterationSetup(Target = nameof(Update))]
    public void SetupUpdate()
    {
        Setup(createBenchmarked: true);
        _updateSources = LinksData.FillBackground(_storage, N);
    }

    [IterationSetup(Target = nameof(Delete))]
    public void SetupDelete() => Setup(createBenchmarked: true);

    [IterationSetup(Target = nameof(EachAll))]
    public void SetupEachAll() => Setup(createBenchmarked: true);

    [IterationSetup(Target = nameof(EachIdentity))]
    public void SetupEachIdentity() => Setup(createBenchmarked: true);

    [IterationSetup(Target = nameof(EachConcrete))]
    public void SetupEachConcrete() => Setup(createBenchmarked: true);

    [IterationSetup(Target = nameof(EachOutgoing))]
    public void SetupEachOutgoing() => Setup(createBenchmarked: true);

    [IterationSetup(Target = nameof(EachIncoming))]
    public void SetupEachIncoming() => Setup(createBenchmarked: true);

    [IterationCleanup]
    public void Cleanup() => _storage.Dispose();

    [Benchmark]
    public uint Create()
    {
        var created = LinksData.FillBenchmarked(_storage, _background, N);
        return created[^1].Id;
    }

    [Benchmark]
    public uint Update()
    {
        var last = 0U;
        for (var index = 0; index < _links.Length; index++)
        {
            _storage.Update(_links[index].Id, _updateSources[index], _background[0]);
            last = _links[index].Id;
        }
        return last;
    }

    [Benchmark]
    public uint Delete()
    {
        var last = 0U;
        foreach (var link in _links)
        {
            _storage.Delete(link.Id);
            last = link.Id;
        }
        return last;
    }

    [Benchmark]
    public int EachAll() => _storage.QueryAll().Count;

    [Benchmark]
    public ulong EachIdentity()
    {
        var checksum = 0UL;
        foreach (var link in _links)
        {
            checksum += _storage.QueryById(link.Id)?.Id ?? 0;
        }
        return checksum;
    }

    [Benchmark]
    public int EachConcrete()
    {
        var count = 0;
        foreach (var link in _links)
        {
            count += _storage.QueryBySourceTarget(link.Source, link.Target).Count;
        }
        return count;
    }

    [Benchmark]
    public int EachOutgoing()
    {
        var count = 0;
        foreach (var link in _links)
        {
            count += _storage.QueryBySource(link.Source).Count;
        }
        return count;
    }

    [Benchmark]
    public int EachIncoming()
    {
        var count = 0;
        foreach (var link in _links)
        {
            count += _storage.QueryByTarget(link.Target).Count;
        }
        return count;
    }

    private void Setup(bool createBenchmarked)
    {
        _storage = LinksStorageFactory.Create(Variant);
        _background = LinksData.FillBackground(
            _storage,
            EnvironmentValue("BACKGROUND_LINK_COUNT", 3000));
        _links = createBenchmarked
            ? LinksData.FillBenchmarked(_storage, _background, N)
            : Array.Empty<LinkRecord>();
        _updateSources = Array.Empty<uint>();
    }

    private static int EnvironmentValue(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0
            ? value
            : fallback;
}
