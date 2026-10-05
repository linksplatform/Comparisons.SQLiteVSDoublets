using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace Comparisons.SQLiteVSDoublets;

/// <summary>A connection with reusable prepared commands that all join the current transaction.</summary>
public abstract class SQLiteStorage : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly List<SqliteCommand> _commands = [];
    private readonly string _table;

    protected SQLiteStorage(string path, string table, string schema)
    {
        _connection = new SqliteConnection($"Data Source={path};Pooling=False");
        _connection.Open();
        _table = table;
        using var create = _connection.CreateCommand();
        create.CommandText = schema;
        create.ExecuteNonQuery();
    }

    protected SqliteCommand Command(string sql, params (string Name, SqliteType Type)[] parameters)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, type) in parameters)
        {
            command.Parameters.Add(name, type);
        }
        _commands.Add(command);
        return command;
    }

    /// <summary>Like <c>RETURNING id</c>, but without its cost (3× slower inserts, see experiments/sqlite_returning).</summary>
    protected long LastInsertRowId => raw.sqlite3_last_insert_rowid(_connection.Handle);

    public ulong Count()
    {
        using var count = _connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM {_table}";
        return (ulong)(long)count.ExecuteScalar()!;
    }

    public TResult Transaction<TResult>(Func<TResult> work)
    {
        using var transaction = _connection.BeginTransaction();
        _commands.ForEach(command => command.Transaction = transaction);
        try
        {
            var result = work();
            transaction.Commit();
            return result;
        }
        finally
        {
            _commands.ForEach(command => command.Transaction = null);
        }
    }

    public void Dispose()
    {
        _commands.ForEach(command => command.Dispose());
        _connection.Dispose();
    }
}
