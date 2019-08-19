# Comparisons.SQLiteVSDoublets ([english version](README.md))

![Сравнение моделей данных](https://github.com/LinksPlatform/Documentation/raw/master/doc/ModelsComparison/relational_model_vs_associative_model_vs_links_ru.png)

Сравнение SQLite и Дуплетов ПлатформыСвязей на базовых операциях в качестве встариваемых баз данных с объектами (создание списка, чтение списка, удаление списка).

Основано на примерах из https://github.com/FahaoTang/dotnetcore-examples и https://github.com/Konard/LinksPlatform

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

## Дуплеты
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

## Результат

![Изображение результата сравнения SQLite и Дуплетов.](https://raw.githubusercontent.com/linksplatform/Documentation/719831c184ee8a4d709d9a8d9cb4d9abea316dcb/doc/Examples/sqlite_vs_doublets_comparison_result.png "Результат сравнения SQLite и Дуплетов")

Первый это SQLite, второй это Дуплеты.

![Изображение использования ОЗУ SQLite.](https://raw.githubusercontent.com/linksplatform/Documentation/719831c184ee8a4d709d9a8d9cb4d9abea316dcb/doc/Examples/sqlite_ram_usage.png "Использование ОЗУ SQLite")
![Изображение использования ОЗУ Дуплетами.](https://raw.githubusercontent.com/linksplatform/Documentation/719831c184ee8a4d709d9a8d9cb4d9abea316dcb/doc/Examples/doublets_ram_usage.png "Использование ОЗУ Дуплетами")

## Заключение

Дуплеты быстрее и используют меньше ОЗУ чем SQLite + EntityFramework, но используют больше памяти на диске. В первую очередь это из-за того, что происходит отображение всех 65536 символов UTF-16 на адресное пространство связей и на каждую ссылку на любую связь используется 64 бита. Этот пример будет обновлён позднее и использование памяти должно стать ещё меньше.
