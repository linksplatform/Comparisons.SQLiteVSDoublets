using System.Numerics;
using Microsoft.Data.Sqlite;
using Platform.Data;
using Platform.Data.Doublets;

namespace Comparisons.SQLiteVSDoublets;

public readonly record struct Link<T>(T Id, T From, T To);

public interface ILinksStorage<T> : IDisposable where T : struct
{
    T Create(T from, T to);
    void Update(T id, T from, T to);
    void Delete(T id);
    Link<T>? Get(T id);
    T? Search(T from, T to);
    void Each(Action<Link<T>> visit);
    void EachWithFrom(T from, Action<Link<T>> visit);
    void EachWithTo(T to, Action<Link<T>> visit);
    ulong Count();

    TResult Transaction<TResult>(Func<TResult> work) => work();
}

public sealed class SQLiteLinks<T> : SQLiteStorage, ILinksStorage<T> where T : struct, IBinaryInteger<T>
{
    private static readonly (string, SqliteType) IdParameter = ("$id", SqliteType.Integer);
    private static readonly (string, SqliteType) FromParameter = ("$from", SqliteType.Integer);
    private static readonly (string, SqliteType) ToParameter = ("$to", SqliteType.Integer);

    private readonly SqliteCommand _create;
    private readonly SqliteCommand _update;
    private readonly SqliteCommand _delete;
    private readonly SqliteCommand _get;
    private readonly SqliteCommand _search;
    private readonly SqliteCommand _each;
    private readonly SqliteCommand _eachWithFrom;
    private readonly SqliteCommand _eachWithTo;

    public static SQLiteLinks<T> Open(string path) => new(path);

    public static SQLiteLinks<T> InMemory() => new(":memory:");

    private SQLiteLinks(string path) : base(path, "links", """
        CREATE TABLE links (id INTEGER PRIMARY KEY, "from" INTEGER NOT NULL, "to" INTEGER NOT NULL);
        CREATE INDEX links_from_to ON links ("from", "to");
        CREATE INDEX links_to_from ON links ("to", "from");
        """)
    {
        _create = Command("""INSERT INTO links ("from", "to") VALUES ($from, $to) RETURNING id""", FromParameter, ToParameter);
        _update = Command("""UPDATE links SET "from" = $from, "to" = $to WHERE id = $id""", IdParameter, FromParameter, ToParameter);
        _delete = Command("DELETE FROM links WHERE id = $id", IdParameter);
        _get = Command("""SELECT "from", "to" FROM links WHERE id = $id""", IdParameter);
        _search = Command("""SELECT id FROM links WHERE "from" = $from AND "to" = $to""", FromParameter, ToParameter);
        _each = Command("""SELECT id, "from", "to" FROM links""");
        _eachWithFrom = Command("""SELECT id, "from", "to" FROM links WHERE "from" = $from""", FromParameter);
        _eachWithTo = Command("""SELECT id, "from", "to" FROM links WHERE "to" = $to""", ToParameter);
    }

    private static T Id(long sql) => T.CreateChecked(sql);

    private static SqliteCommand With(SqliteCommand command, params ReadOnlySpan<T> values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            command.Parameters[i].Value = long.CreateChecked(values[i]);
        }
        return command;
    }

    public T Create(T from, T to) => Id((long)With(_create, from, to).ExecuteScalar()!);

    public void Update(T id, T from, T to) => With(_update, id, from, to).ExecuteNonQuery();

    public void Delete(T id) => With(_delete, id).ExecuteNonQuery();

    public Link<T>? Get(T id)
    {
        using var reader = With(_get, id).ExecuteReader();
        return reader.Read() ? new Link<T>(id, Id(reader.GetInt64(0)), Id(reader.GetInt64(1))) : null;
    }

    public T? Search(T from, T to) => With(_search, from, to).ExecuteScalar() is long id ? Id(id) : null;

    public void Each(Action<Link<T>> visit) => Read(_each, visit);

    public void EachWithFrom(T from, Action<Link<T>> visit) => Read(With(_eachWithFrom, from), visit);

    public void EachWithTo(T to, Action<Link<T>> visit) => Read(With(_eachWithTo, to), visit);

    private static void Read(SqliteCommand command, Action<Link<T>> visit)
    {
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            visit(new Link<T>(Id(reader.GetInt64(0)), Id(reader.GetInt64(1)), Id(reader.GetInt64(2))));
        }
    }
}

public sealed class DoubletsLinks<T>(ILinks<T> links) : ILinksStorage<T>
    where T : struct, IUnsignedNumber<T>, IComparisonOperators<T, T, bool>
{
    private readonly T _any = links.Constants.Any;
    private readonly T _continue = links.Constants.Continue;

    public T Create(T from, T to) => links.CreateAndUpdate(from, to);

    public void Update(T id, T from, T to) => links.Update(id, from, to);

    public void Delete(T id) => links.Delete(id, handler: null);

    public Link<T>? Get(T id) => links.Exists(id) ? Link(links.GetLink(id)!) : null;

    public T? Search(T from, T to) => links.SearchOrDefault(from, to) is var id && id != T.Zero ? id : null;

    public void Each(Action<Link<T>> visit) => Each(visit, _any, _any, _any);

    public void EachWithFrom(T from, Action<Link<T>> visit) => Each(visit, _any, from, _any);

    public void EachWithTo(T to, Action<Link<T>> visit) => Each(visit, _any, _any, to);

    private void Each(Action<Link<T>> visit, params T[] query) => links.Each(link =>
    {
        visit(Link(link!));
        return _continue;
    }, query);

    private static Link<T> Link(IList<T> link) => new(link[0], link[1], link[2]);

    public ulong Count() => ulong.CreateChecked(links.Count());

    public void Dispose() => (links as IDisposable)?.Dispose();
}
