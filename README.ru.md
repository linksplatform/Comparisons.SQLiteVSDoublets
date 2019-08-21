# Comparisons.SQLiteVSDoublets ([english version](README.md))

![Сравнение моделей данных](https://github.com/LinksPlatform/Documentation/raw/master/doc/ModelsComparison/relational_model_vs_associative_model_vs_links_ru.png)

Сравнение SQLite и Дуплетов ПлатформыСвязей на базовых операциях в качестве встариваемых баз данных с объектами (создание списка, чтение списка, удаление списка).

Основано на примерах из https://github.com/FahaoTang/dotnetcore-examples и https://github.com/Konard/LinksPlatform

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
                    ReadBlogPosts.Add(blogPost);
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
                    foreach (var blogPost in BlogPosts.List)
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
                    ReadBlogPosts.Add(blogPost);
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

![Изображение результата сравнения SQLite и Дуплетов.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_comparison_result.png "Результат сравнения SQLite и Дуплетов")

Первый это SQLite, второй это Дуплеты:

![Изображение использования ОЗУ SQLite.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_ram_usage.png "Использование ОЗУ SQLite")
![Изображение использования ОЗУ Дуплетами.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/doublets_ram_usage.png "Использование ОЗУ Дуплетами")

## Заключение

Дублеты быстрее и используют меньше оперативной памяти, чем SQLite + EntityFramework, но используют больше памяти на диске.
Это включает в себя 1 тестовый прогон и 5 записей списка.
Если мы увеличим количество тестов и записей, то будут Дублеты становиться всё медленее и медленнее чем SQLite.
Таким образом, настоящий победитель здесь - SQLite.