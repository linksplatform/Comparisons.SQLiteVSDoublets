using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace Comparisons.SQLiteVSDoublets;

public enum SQLiteProvider
{
    MicrosoftDataSqlite,
    SystemDataSQLite,
}

/// <summary>A connection with reusable prepared commands that all join the current transaction.</summary>
public abstract class SQLiteStorage : IDisposable
{
    private readonly DbConnection _connection;
    private readonly List<DbCommand> _commands = [];
    private readonly string _table;

    protected SQLiteStorage(string path, string table, string schema, SQLiteProvider provider)
    {
        _connection = OpenConnection(path, provider);
        _table = table;
        using var create = _connection.CreateCommand();
        create.CommandText = schema;
        create.ExecuteNonQuery();
    }

    private static DbConnection OpenConnection(string path, SQLiteProvider provider)
    {
        DbConnection connection = provider switch
        {
            SQLiteProvider.MicrosoftDataSqlite => new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()),
            SQLiteProvider.SystemDataSQLite => new SQLiteConnection(new SQLiteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()),
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };
        connection.Open();
        return connection;
    }

    public static string Version(SQLiteProvider provider)
    {
        using var connection = OpenConnection(":memory:", provider);
        using var version = connection.CreateCommand();
        version.CommandText = "SELECT sqlite_version()";
        return (string)version.ExecuteScalar()!;
    }

    protected DbCommand Command(string sql, params (string Name, DbType Type)[] parameters)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, type) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = type;
            command.Parameters.Add(parameter);
        }
        _commands.Add(command);
        return command;
    }

    /// <summary>Like <c>RETURNING id</c>, but without its cost (3× slower inserts, see experiments/sqlite_returning).</summary>
    protected long LastInsertRowId => _connection switch
    {
        SqliteConnection microsoft => raw.sqlite3_last_insert_rowid(microsoft.Handle),
        SQLiteConnection system => system.LastInsertRowId,
        _ => throw new InvalidOperationException("unknown SQLite provider"),
    };

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
