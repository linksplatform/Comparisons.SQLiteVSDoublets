# Comparisons.SQLiteVSDoublets ([русская версия](README.ru.md))
Comparison of SQLite and LinksPlatform's Doublets (links) on basic embeded database operations with objects (create list, read list, delete list).

Based on examples from https://github.com/FahaoTang/dotnetcore-examples and https://github.com/Konard/LinksPlatform

## SQLite
```C#
using System;
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
                if (!dbContext.BlogPosts.Any())
                {
                    dbContext.BlogPosts.AddRange(BlogPosts.List);
                    dbContext.SaveChanges();
                }
            }
        }

        public override void ReadList()
        {
            using (var dbContext = new SQLiteDbContext(DbFilename))
            {
                foreach (var blogPost in dbContext.BlogPosts)
                {
                    Console.WriteLine(blogPost);
                }
            }
        }

        public override void DeleteList()
        {
            using (var dbContext = new SQLiteDbContext(DbFilename))
            {
                dbContext.BlogPosts.RemoveRange(dbContext.BlogPosts);
                dbContext.SaveChanges();
            }
        }
    }
}

```

## Doublets
``` C#
using System;
using System.Linq;
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
                if (!dbContext.BlogPosts.Any())
                {
                    var blogPosts = BlogPosts.List;
                    foreach (BlogPost blogPost in blogPosts)
                    {
                        dbContext.CreateBlogPost(blogPost);
                    }
                }
            }
        }

        public override void ReadList()
        {
            using (var dbContext = new DoubletsDbContext(DbFilename))
            {
                foreach (var blogPost in dbContext.BlogPosts)
                {
                    Console.WriteLine(blogPost);
                }
            }
        }

        public override void DeleteList()
        {
            using (var dbContext = new DoubletsDbContext(DbFilename))
            {
                var blogPosts = dbContext.BlogPosts;
                foreach (var blogPost in blogPosts)
                {
                    dbContext.Delete((ulong)blogPost.Id);
                }
            }
        }
    }
}

```

## Result

![Image with result of comparison between SQLite and Doublets.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_comparison_result.png "Result of comparison between SQLite and Doublets")

First is SQLite, second is Doublets.

## Conclusion

Doublets is faster than SQLite + EntityFramework, but uses more memory on disk. This is due to mapping of all 65536 UTF-16 characters to links space and using 64 bit per link reference. The example will be updated later and it should be even better in memory usage.
