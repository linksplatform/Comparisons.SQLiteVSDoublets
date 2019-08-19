# Comparisons.SQLiteVSDoublets ([русская версия](README.ru.md))

![Comparison of models](https://github.com/LinksPlatform/Documentation/raw/master/doc/ModelsComparison/relational_model_vs_associative_model_vs_links.png)

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

![Image with result of comparison between SQLite and Doublets.](https://raw.githubusercontent.com/linksplatform/Documentation/719831c184ee8a4d709d9a8d9cb4d9abea316dcb/doc/Examples/sqlite_vs_doublets_comparison_result.png "Result of comparison between SQLite and Doublets")

First is SQLite, second is Doublets.

![Image of SQLite RAM usage.](https://github.com/linksplatform/Documentation/raw/719831c184ee8a4d709d9a8d9cb4d9abea316dcb/doc/Examples/sqlite_ram_usage.png "SQLite RAM usage")
![Image of Doublets RAM usage.](https://raw.githubusercontent.com/linksplatform/Documentation/719831c184ee8a4d709d9a8d9cb4d9abea316dcb/doc/Examples/doublets_ram_usage.png "Doublets RAM usage")

## Conclusion

Doublets is faster and uses less RAM than SQLite + EntityFramework, but uses more memory on disk. This is due to mapping of all 65536 UTF-16 characters to links space and using 64 bit per link reference. The example will be updated later and it should be even better in memory usage.
