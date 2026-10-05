using Npgsql;
using Xunit;
using static Comparisons.SQLiteVSDoublets.Dataset;

namespace Comparisons.SQLiteVSDoublets.Tests;

public class PostgreSQLTests
{
    public static bool IsConfigured => !string.IsNullOrEmpty(
        Environment.GetEnvironmentVariable(PostgreSQLBlogPosts<uint>.ConnectionStringVariable));

    [Fact]
    public void PostgreSQLUsesTheObjectsHarness()
    {
        Assert.Contains("PostgreSQL_EFCore", Harness.ObjectsVariants);
        Assert.DoesNotContain("PostgreSQL_EFCore", Harness.LinksVariants);
    }

    [Theory(Skip = "Set POSTGRESQL_CONNECTION_STRING for PostgreSQL integration tests.", SkipUnless = nameof(IsConfigured))]
    [InlineData(false), InlineData(true)]
    public void EveryIdWidthPassesValidatedRepetitions(bool wide)
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var measured = wide
                ? Harness.MeasureObjects<ulong>("PostgreSQL_EFCore", 100, 2, directory)
                : Harness.MeasureObjects<uint>("PostgreSQL_EFCore", 100, 2, directory);
            Assert.Null(measured.FileBytes);
            Assert.True(measured.ServerBytes > 0);
            Assert.Equal(new[] { "create", "read_all", "read_by_id", "delete" }, measured.Operations.Select(operation => operation.Operation));
            Assert.All(measured.Operations, operation => Assert.Equal(2, operation.Samples.Count));
            Assert.Empty(Directory.GetFileSystemEntries(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact(Skip = "Set POSTGRESQL_CONNECTION_STRING for PostgreSQL integration tests.", SkipUnless = nameof(IsConfigured))]
    public void PostsRoundTripWithoutAUniqueTitleConstraintOrTrackingCache()
    {
        using var posts = new PostgreSQLBlogPosts<ulong>(PostgreSQLBlogPosts<ulong>.ConnectionString());
        var inputs = new[] { BlogPost(1), new BlogPost("", "", 0), new BlogPost("Ünïcödé 🌍 ✓", "a", 1_600_000_000), BlogPost(1) };
        var ids = posts.Transaction(() => inputs.Select(posts.Create).ToArray());
        Assert.Equal(inputs, ids.Select(posts.Get));
        var stored = new List<(ulong, BlogPost)>();
        posts.Each((id, post) => stored.Add((id, post)));
        Assert.Equal(ids.Zip(inputs), stored.OrderBy(entry => entry.Item1));

        using var connection = new NpgsqlConnection(PostgreSQLBlogPosts<ulong>.ConnectionString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE \"{posts.SchemaName}\".blog_posts SET title = 'changed on server' WHERE id = $1";
        command.Parameters.AddWithValue(checked((long)ids[0]));
        command.ExecuteNonQuery();
        Assert.Equal("changed on server", posts.Get(ids[0])!.Title);
        foreach (var id in ids)
        {
            posts.Delete(id);
        }
        Assert.Equal(0UL, posts.Count());
        Assert.Null(posts.Get(ids[0]));
    }

    [Fact(Skip = "Set POSTGRESQL_CONNECTION_STRING for PostgreSQL integration tests.", SkipUnless = nameof(IsConfigured))]
    public void RollbackLeavesTheStoreReusable()
    {
        using var posts = new PostgreSQLBlogPosts<uint>(PostgreSQLBlogPosts<uint>.ConnectionString());
        Assert.Throws<InvalidOperationException>(() => posts.Transaction<int>(() =>
        {
            posts.Create(BlogPost(1));
            throw new InvalidOperationException("roll back the inserted post");
        }));
        Assert.Equal(0UL, posts.Count());
        var id = posts.Transaction(() => posts.Create(BlogPost(2)));
        Assert.Equal(BlogPost(2), posts.Get(id));
        Assert.Equal(1UL, posts.Count());
    }

    [Fact(Skip = "Set POSTGRESQL_CONNECTION_STRING for PostgreSQL integration tests.", SkipUnless = nameof(IsConfigured))]
    public void StoresAreIsolatedAndDisposeOnlyTheirOwnSchema()
    {
        using var first = new PostgreSQLBlogPosts<uint>(PostgreSQLBlogPosts<uint>.ConnectionString());
        using var second = new PostgreSQLBlogPosts<uint>(PostgreSQLBlogPosts<uint>.ConnectionString());
        Assert.NotEqual(first.SchemaName, second.SchemaName);
        var id = second.Create(BlogPost(2));
        first.Create(BlogPost(1));
        var schema = first.SchemaName;
        first.Dispose();
        first.Dispose();
        Assert.Equal(BlogPost(2), second.Get(id));
        using var connection = new NpgsqlConnection(PostgreSQLBlogPosts<uint>.ConnectionString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pg_namespace WHERE nspname = $1";
        command.Parameters.AddWithValue(schema);
        Assert.Equal(0L, command.ExecuteScalar());
    }
}
