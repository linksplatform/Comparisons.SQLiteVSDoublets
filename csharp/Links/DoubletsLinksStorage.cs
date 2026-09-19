using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Platform.Data.Doublets;
using Platform.Disposables;

namespace Comparisons.SQLiteVSDoublets.Links;

public sealed class DoubletsLinksStorage : ILinksStorage
{
    private readonly ILinks<uint> _links;
    private readonly IReadOnlyList<string> _paths;

    public DoubletsLinksStorage(ILinks<uint> links, params string[] paths)
    {
        _links = links;
        _paths = paths;
    }

    public uint Create(uint source, uint target) => _links.CreateAndUpdate(source, target);

    public uint CreatePoint() => _links.CreatePoint();

    public void Update(uint id, uint source, uint target) => _links.Update(id, source, target);

    public void Delete(uint id) => Platform.Data.ILinksExtensions.Delete(_links, id);

    public IReadOnlyList<LinkRecord> QueryAll()
    {
        var any = _links.Constants.Any;
        return Convert(_links.All(any, any, any));
    }

    public LinkRecord? QueryById(uint id)
    {
        var any = _links.Constants.Any;
        var result = _links.All(id, any, any);
        return result.Count == 0 || result[0] is null ? null : Convert(result[0]!);
    }

    public IReadOnlyList<LinkRecord> QueryBySourceTarget(uint source, uint target)
    {
        var any = _links.Constants.Any;
        return Convert(_links.All(any, source, target));
    }

    public IReadOnlyList<LinkRecord> QueryBySource(uint source)
    {
        var any = _links.Constants.Any;
        return Convert(_links.All(any, source, any));
    }

    public IReadOnlyList<LinkRecord> QueryByTarget(uint target)
    {
        var any = _links.Constants.Any;
        return Convert(_links.All(any, any, target));
    }

    public int Count => QueryAll().Count;

    public void Dispose()
    {
        _links.DisposeIfPossible();
        foreach (var path in _paths.Where(File.Exists))
        {
            File.Delete(path);
        }
    }

    private LinkRecord Convert(IList<uint> link) => new(
        _links.GetIndex(link),
        _links.GetSource(link),
        _links.GetTarget(link));

    private List<LinkRecord> Convert(IList<IList<uint>?> links) =>
        links.Where(link => link is not null).Select(link => Convert(link!)).ToList();
}
