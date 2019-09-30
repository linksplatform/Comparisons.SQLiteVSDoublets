[![Build Status](https://travis-ci.com/linksplatform/Comparisons.SQLiteVSDoublets.svg?branch=master)](https://travis-ci.com/linksplatform/Comparisons.SQLiteVSDoublets)

# Comparisons.SQLiteVSDoublets ([русская версия](README.ru.md))

![Comparison of models](https://github.com/LinksPlatform/Documentation/raw/master/doc/ModelsComparison/relational_model_vs_associative_model_vs_links.png)

Comparison of SQLite and LinksPlatform's Doublets (links) on basic embeded database operations with objects (create list, read list, delete list).

Based on examples from https://github.com/FahaoTang/dotnetcore-examples and https://github.com/Konard/LinksPlatform

## SQLite
```C#
using System.Linq;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets.SQLite
{
    public class SQLiteTestRun : TestRun
    {
        public SQLiteTestRun(string dbFilename) : base(dbFilename) { }

        public override void Prepare()
        {
            using (var dbContext = new SQLiteDbContext(DbFilename))
            {
                dbContext.Database.EnsureCreated();
            }
        }

        public override void CreateList()
        {
            using (var dbContext = new SQLiteDbContext(DbFilename))
            {
                dbContext.BlogPosts.AddRange(BlogPosts.List);
                dbContext.SaveChanges();
            }
        }

        public override void ReadList()
        {
            using (var dbContext = new SQLiteDbContext(DbFilename))
            {
                foreach (var blogPost in dbContext.BlogPosts)
                {
                    ReadBlogPosts.Add(blogPost);
                }
            }
        }

        public override void DeleteList()
        {
            using (var dbContext = new SQLiteDbContext(DbFilename))
            {
                var blogPostsToDelete = dbContext.BlogPosts.ToList();
                dbContext.BlogPosts.RemoveRange(blogPostsToDelete);
                dbContext.SaveChanges();
            }
        }
    }
}
```

## Doublets
``` C#
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets.Doublets
{
    public class DoubletsTestRun : TestRun
    {
        public DoubletsTestRun(string dbFilename) : base(dbFilename) { }

        public override void Prepare()
        {
            using (var dbContext = new DoubletsDbContext(DbFilename))
            {
            }
        }

        public override void CreateList()
        {
            using (var dbContext = new DoubletsDbContext(DbFilename))
            {
                foreach (var blogPost in BlogPosts.List)
                {
                    dbContext.CreateBlogPost(blogPost);
                }
            }
        }

        public override void ReadList()
        {
            using (var dbContext = new DoubletsDbContext(DbFilename))
            {
                foreach (var blogPost in dbContext.BlogPosts)
                {
                    ReadBlogPosts.Add(blogPost);
                }
            }
        }

        public override void DeleteList()
        {
            using (var dbContext = new DoubletsDbContext(DbFilename))
            {
                var blogPostsToDelete = dbContext.BlogPosts;
                foreach (var blogPost in blogPostsToDelete)
                {
                    dbContext.Delete((ulong)blogPost.Id);
                }
            }
        }
    }
}
```

## Result

### Performance
![Image with result of performance comparison between SQLite and Doublets.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_performance.png "Result of performance comparison between SQLite and Doublets")

### Disk usage
![Image with result of disk usage comparison between SQLite and Doublets.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_disk_usage.png "Result of disk usage comparison between SQLite and Doublets")

### RAM usage
![Image with result of RAM usage comparison between SQLite and Doublets.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_ram_usage.png "Result of RAM usage comparison between SQLite and Doublets")

## Conclusion

In this particular comparison, Doublets are faster and use less memory on disk, but that comes with the cost of RAM usage (where Sqlite performs better).
