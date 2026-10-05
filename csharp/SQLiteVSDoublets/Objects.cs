using System.Data;
using System.Data.Common;
using System.Numerics;
using Platform.Collections.Stacks;
using Platform.Converters;
using Platform.Data;
using Platform.Data.Doublets;
using Platform.Data.Doublets.CriterionMatchers;
using Platform.Data.Doublets.PropertyOperators;
using Platform.Data.Doublets.Sequences.Converters;
using Platform.Data.Doublets.Sequences.Unicode;
using Platform.Data.Doublets.Sequences.Walkers;
using Platform.Data.Numbers.Raw;

namespace Comparisons.SQLiteVSDoublets;

public interface IBlogPostsStorage<T> : IDisposable where T : struct
{
    T Create(BlogPost post);
    BlogPost? Get(T id);
    void Each(Action<T, BlogPost> visit);
    void Delete(T id);
    ulong Count();

    TResult Transaction<TResult>(Func<TResult> work) => work();
}

public sealed class SQLiteBlogPosts<T> : SQLiteStorage, IBlogPostsStorage<T> where T : struct, IBinaryInteger<T>
{
    private readonly DbCommand _create;
    private readonly DbCommand _get;
    private readonly DbCommand _each;
    private readonly DbCommand _delete;

    public static SQLiteBlogPosts<T> Open(string path) => Open(path, SQLiteProvider.MicrosoftDataSqlite);

    public static SQLiteBlogPosts<T> Open(string path, SQLiteProvider provider) => new(path, provider);

    public static SQLiteBlogPosts<T> InMemory() => InMemory(SQLiteProvider.MicrosoftDataSqlite);

    public static SQLiteBlogPosts<T> InMemory(SQLiteProvider provider) => new(":memory:", provider);

    private SQLiteBlogPosts(string path, SQLiteProvider provider) : base(path, "blog_posts",
        "CREATE TABLE blog_posts (id INTEGER PRIMARY KEY, title TEXT NOT NULL, content TEXT NOT NULL, publication_date INTEGER NOT NULL)", provider)
    {
        _create = Command(
            "INSERT INTO blog_posts (title, content, publication_date) VALUES ($title, $content, $publication_date)",
            ("$title", DbType.String), ("$content", DbType.String), ("$publication_date", DbType.Int64));
        _get = Command("SELECT title, content, publication_date FROM blog_posts WHERE id = $id", ("$id", DbType.Int64));
        _each = Command("SELECT title, content, publication_date, id FROM blog_posts");
        _delete = Command("DELETE FROM blog_posts WHERE id = $id", ("$id", DbType.Int64));
    }

    private static BlogPost BlogPost(DbDataReader reader) =>
        new(reader.GetString(0), reader.GetString(1), (ulong)reader.GetInt64(2));

    public T Create(BlogPost post)
    {
        _create.Parameters[0].Value = post.Title;
        _create.Parameters[1].Value = post.Content;
        _create.Parameters[2].Value = (long)post.PublicationDate;
        _create.ExecuteNonQuery();
        return T.CreateChecked(LastInsertRowId);
    }

    public BlogPost? Get(T id)
    {
        _get.Parameters[0].Value = long.CreateChecked(id);
        using var reader = _get.ExecuteReader();
        return reader.Read() ? BlogPost(reader) : null;
    }

    public void Each(Action<T, BlogPost> visit)
    {
        using var reader = _each.ExecuteReader();
        while (reader.Read())
        {
            visit(T.CreateChecked(reader.GetInt64(3)), BlogPost(reader));
        }
    }

    public void Delete(T id)
    {
        _delete.Parameters[0].Value = long.CreateChecked(id);
        _delete.ExecuteNonQuery();
    }
}

/// <summary>
/// Stores each blog post as a <c>(blog_post, itself)</c> link with <c>(post, property) -> value</c> properties,
/// strings as balanced-variant sequences of Unicode symbols and dates as raw numbers.
/// </summary>
public sealed class DoubletsBlogPosts<T> : IBlogPostsStorage<T>
    where T : struct, IUnsignedNumber<T>, IComparisonOperators<T, T, bool>
{
    private readonly ILinks<T> _links;
    private readonly T _any;
    private readonly T _title;
    private readonly T _content;
    private readonly T _publicationDate;
    private readonly T _blogPost;
    private readonly PropertiesOperator<T> _properties;
    private readonly AddressToRawNumberConverter<T> _addressToNumber = new();
    private readonly RawNumberToAddressConverter<T> _numberToAddress = new();
    private readonly IConverter<string, T> _stringToSequence;
    private readonly IConverter<T, string> _sequenceToString;

    public DoubletsBlogPosts(ILinks<T> links, bool cacheSequences)
    {
        _links = links;
        _any = links.Constants.Any;
        var meaningRoot = links.CreatePoint();
        T Marker()
        {
            var marker = links.Create();
            return links.Update(marker, meaningRoot, marker);
        }
        var unicodeSymbol = Marker();
        var unicodeSequence = Marker();
        _title = Marker();
        _content = Marker();
        _publicationDate = Marker();
        _blogPost = Marker();
        _properties = new PropertiesOperator<T>(links);

        var unicodeSymbolMatcher = new TargetMatcher<T>(links, unicodeSymbol);
        IConverter<string, T> stringToSequence = new StringToUnicodeSequenceConverter<T>(
            links,
            new CharToUnicodeSymbolConverter<T>(links, _addressToNumber, unicodeSymbol),
            new BalancedVariantConverter<T>(links),
            unicodeSequence);
        IConverter<T, string> sequenceToString = new UnicodeSequenceToStringConverter<T>(
            links,
            new RightSequenceWalker<T>(links, new DefaultStack<T>(), unicodeSymbolMatcher.IsMatched),
            new UnicodeSymbolToCharConverter<T>(links, _numberToAddress, unicodeSymbolMatcher),
            unicodeSequence);
        _stringToSequence = cacheSequences ? new CachingConverterDecorator<string, T>(stringToSequence) : stringToSequence;
        _sequenceToString = cacheSequences ? new CachingConverterDecorator<T, string>(sequenceToString) : sequenceToString;
    }

    public T Create(BlogPost post)
    {
        var blogPost = _links.Create();
        _links.Update(blogPost, _blogPost, blogPost);
        _properties.SetValue(blogPost, _title, _stringToSequence.Convert(post.Title));
        _properties.SetValue(blogPost, _content, _stringToSequence.Convert(post.Content));
        _properties.SetValue(blogPost, _publicationDate, _addressToNumber.Convert(T.CreateChecked(post.PublicationDate)));
        return blogPost;
    }

    public BlogPost? Get(T id)
    {
        var title = _properties.GetValue(id, _title);
        var content = _properties.GetValue(id, _content);
        var publicationDate = _properties.GetValue(id, _publicationDate);
        if (title == T.Zero || content == T.Zero || publicationDate == T.Zero)
        {
            return null;
        }
        return new BlogPost(
            _sequenceToString.Convert(title),
            _sequenceToString.Convert(content),
            ulong.CreateChecked(_numberToAddress.Convert(publicationDate)));
    }

    public void Each(Action<T, BlogPost> visit)
    {
        var ids = new List<T>();
        _links.Each(post =>
        {
            ids.Add(post![0]);
            return _links.Constants.Continue;
        }, _any, _blogPost, _any);
        foreach (var id in ids)
        {
            visit(id, Get(id)!);
        }
    }

    public void Delete(T id)
    {
        foreach (var property in (ReadOnlySpan<T>)[_title, _content, _publicationDate])
        {
            var objectProperty = _links.SearchOrDefault(id, property);
            _links.Delete(_links.SingleOrDefault([_any, objectProperty, _any])![0], handler: null);
            _links.Delete(objectProperty, handler: null);
        }
        _links.Delete(id, handler: null);
    }

    public ulong Count() => ulong.CreateChecked(_links.Count(_any, _blogPost, _any));

    public void Dispose() => (_links as IDisposable)?.Dispose();
}
