using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Comparisons.SQLiteVSDoublets;

/// <summary>EF Core blog posts in a fresh, owned schema on a configured PostgreSQL server.</summary>
public sealed class PostgreSQLBlogPosts<T> : IBlogPostsStorage<T> where T : struct, IBinaryInteger<T>
{
    public const string ConnectionStringVariable = "POSTGRESQL_CONNECTION_STRING";
    private readonly NpgsqlConnection _connection;
    private readonly PostsContext _context;
    private bool _disposed;

    public string SchemaName { get; } = $"benchmark_{Guid.NewGuid():N}";
    public string ServerVersion => _connection.ServerVersion;

    public static string ConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {ConnectionStringVariable} to run PostgreSQL_EFCore.");

    public PostgreSQLBlogPosts(string connectionString)
    {
        _connection = new NpgsqlConnection(connectionString);
        var options = new DbContextOptionsBuilder<PostsContext>().UseNpgsql(_connection);
        if (Environment.GetEnvironmentVariable("POSTGRESQL_VERBOSE") == "1")
        {
            options.LogTo(Console.Error.WriteLine);
        }
        _context = new PostsContext(options.Options);
        try
        {
            _connection.Open();
            // Identifiers contain only a fixed prefix and a generated hexadecimal GUID.
            Execute($"CREATE SCHEMA \"{SchemaName}\"");
            Execute($"SET search_path TO \"{SchemaName}\"");
            _context.Database.ExecuteSqlRaw(_context.Database.GenerateCreateScript());
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public T Create(BlogPost post)
    {
        var row = new PostRow
        {
            Title = post.Title,
            Content = post.Content,
            PublicationDate = checked((long)post.PublicationDate),
        };
        _context.Posts.Add(row);
        _context.SaveChanges();
        // Point reads must hit the server, and the tracker must not grow with the dataset.
        _context.ChangeTracker.Clear();
        return T.CreateChecked(row.Id);
    }

    public BlogPost? Get(T id)
    {
        var key = long.CreateChecked(id);
        var row = _context.Posts.AsNoTracking().SingleOrDefault(post => post.Id == key);
        return row is null ? null : Post(row);
    }

    public void Each(Action<T, BlogPost> visit)
    {
        foreach (var row in _context.Posts.AsNoTracking())
        {
            visit(T.CreateChecked(row.Id), Post(row));
        }
    }

    public void Delete(T id)
    {
        var key = long.CreateChecked(id);
        _context.Posts.Where(post => post.Id == key).ExecuteDelete();
    }

    public ulong Count() => checked((ulong)_context.Posts.LongCount());

    public TResult Transaction<TResult>(Func<TResult> work)
    {
        using var transaction = _context.Database.BeginTransaction();
        try
        {
            var result = work();
            transaction.Commit();
            return result;
        }
        finally
        {
            _context.ChangeTracker.Clear();
        }
    }

    /// <summary>Server table, index and TOAST bytes; excludes shared WAL and server memory.</summary>
    public ulong ServerBytes()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(SUM(pg_total_relation_size(c.oid)), 0)::bigint
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = $1 AND c.relkind = 'r'
            """;
        command.Parameters.AddWithValue(SchemaName);
        return checked((ulong)(long)command.ExecuteScalar()!);
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        try
        {
            if (_connection.State == System.Data.ConnectionState.Open)
            {
                Execute($"DROP SCHEMA IF EXISTS \"{SchemaName}\" CASCADE");
            }
        }
        finally
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }

    private static BlogPost Post(PostRow row) => new(row.Title, row.Content, checked((ulong)row.PublicationDate));

    private sealed class PostRow
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public long PublicationDate { get; set; }
    }

    private sealed class PostsContext(DbContextOptions<PostsContext> options) : DbContext(options)
    {
        public DbSet<PostRow> Posts => Set<PostRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var post = modelBuilder.Entity<PostRow>();
            post.ToTable("blog_posts");
            post.HasKey(row => row.Id);
            post.Property(row => row.Id).HasColumnName("id");
            post.Property(row => row.Title).HasColumnName("title");
            post.Property(row => row.Content).HasColumnName("content");
            post.Property(row => row.PublicationDate).HasColumnName("publication_date");
        }
    }
}
