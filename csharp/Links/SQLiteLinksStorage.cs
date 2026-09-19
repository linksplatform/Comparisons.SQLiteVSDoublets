using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Comparisons.SQLiteVSDoublets.Links;

public sealed class SQLiteLinksStorage : ILinksStorage
{
    private readonly SqliteConnection _connection;
    private readonly string? _path;
    private uint _nextId = 1;

    public SQLiteLinksStorage(bool inMemory)
    {
        _path = inMemory
            ? null
            : Path.Combine(Path.GetTempPath(), $"sqlite-vs-doublets-{Guid.NewGuid():N}.db");
        var dataSource = _path ?? ":memory:";
        _connection = new SqliteConnection($"Data Source={dataSource}");
        _connection.Open();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE links (
                id INTEGER PRIMARY KEY,
                source INTEGER NOT NULL,
                target INTEGER NOT NULL
            );
            CREATE INDEX idx_source ON links(source);
            CREATE INDEX idx_target ON links(target);
            CREATE INDEX idx_source_target ON links(source, target);
            """;
        command.ExecuteNonQuery();
    }

    public uint Create(uint source, uint target)
    {
        var id = _nextId++;
        using var command = Command(
            "INSERT INTO links (id, source, target) VALUES ($id, $source, $target)",
            ("$id", id),
            ("$source", source),
            ("$target", target));
        command.ExecuteNonQuery();
        return id;
    }

    public uint CreatePoint()
    {
        var id = Create(0, 0);
        Update(id, id, id);
        return id;
    }

    public void Update(uint id, uint source, uint target)
    {
        using var command = Command(
            "UPDATE links SET source = $source, target = $target WHERE id = $id",
            ("$source", source),
            ("$target", target),
            ("$id", id));
        command.ExecuteNonQuery();
    }

    public void Delete(uint id)
    {
        using var command = Command("DELETE FROM links WHERE id = $id", ("$id", id));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<LinkRecord> QueryAll() => Query(
        "SELECT id, source, target FROM links");

    public LinkRecord? QueryById(uint id)
    {
        var links = Query(
            "SELECT id, source, target FROM links WHERE id = $id",
            ("$id", id));
        return links.Count == 0 ? null : links[0];
    }

    public IReadOnlyList<LinkRecord> QueryBySourceTarget(uint source, uint target) => Query(
        "SELECT id, source, target FROM links WHERE source = $source AND target = $target",
        ("$source", source),
        ("$target", target));

    public IReadOnlyList<LinkRecord> QueryBySource(uint source) => Query(
        "SELECT id, source, target FROM links WHERE source = $source",
        ("$source", source));

    public IReadOnlyList<LinkRecord> QueryByTarget(uint target) => Query(
        "SELECT id, source, target FROM links WHERE target = $target",
        ("$target", target));

    public int Count
    {
        get
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM links";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
        if (_path is not null)
        {
            File.Delete(_path);
        }
    }

    private SqliteCommand Command(
        string sql,
        params (string Name, uint Value)[] parameters)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }
        return command;
    }

    private List<LinkRecord> Query(
        string sql,
        params (string Name, uint Value)[] parameters)
    {
        using var command = Command(sql, parameters);
        using var reader = command.ExecuteReader();
        var result = new List<LinkRecord>();
        while (reader.Read())
        {
            result.Add(new LinkRecord(
                checked((uint)reader.GetInt64(0)),
                checked((uint)reader.GetInt64(1)),
                checked((uint)reader.GetInt64(2))));
        }
        return result;
    }
}
