using System;
using System.Collections.Generic;

namespace Comparisons.SQLiteVSDoublets.Links;

public interface ILinksStorage : IDisposable
{
    uint Create(uint source, uint target);

    uint CreatePoint();

    void Update(uint id, uint source, uint target);

    void Delete(uint id);

    IReadOnlyList<LinkRecord> QueryAll();

    LinkRecord? QueryById(uint id);

    IReadOnlyList<LinkRecord> QueryBySourceTarget(uint source, uint target);

    IReadOnlyList<LinkRecord> QueryBySource(uint source);

    IReadOnlyList<LinkRecord> QueryByTarget(uint target);

    int Count { get; }
}
