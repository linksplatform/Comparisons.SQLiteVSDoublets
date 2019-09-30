[![Build Status](https://travis-ci.com/linksplatform/Comparisons.SQLiteVSDoublets.svg?branch=master)](https://travis-ci.com/linksplatform/Comparisons.SQLiteVSDoublets)

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

## Дуплеты
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

## Результат

### Производительность
![Изображение с результатом сравнения производительности SQLite и Дуплетов.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_performance.png "Результат сравнения производительности SQLite и Дуплетов")

### Использование пространства на диске
![Изображение с результатом сравнения использования пространства на диске SQLite и Дуплетов.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_disk_usage.png "Результат сравнения использования пространства на диске SQLite и Дуплетов")

### Использование оперативной памяти
![Изображение с результатом сравнения использования оперативной памяти SQLite и Дуплетов.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_ram_usage.png "Результат сравнения использования оперативной памяти SQLite и Дуплетов")

### Исходные данные
```
// * Summary *

BenchmarkDotNet=v0.11.5, OS=Windows 10.0.18362
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.0.100
  [Host]     : .NET Core 2.2.7 (CoreCLR 4.6.28008.02, CoreFX 4.6.28008.03), 64bit RyuJIT
  Job-PSBABD : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.8.4018.0
  Job-LILDDA : .NET Core 2.2.7 (CoreCLR 4.6.28008.02, CoreFX 4.6.28008.03), 64bit RyuJIT

InvocationCount=1  IterationCount=1  UnrollFactor=1
WarmupCount=2

|   Method | Runtime |      N |        Mean | Error |        Gen 0 |       Gen 1 |     Gen 2 |   Allocated | SizeAfterCreation |
|--------- |-------- |------- |------------:|------:|-------------:|------------:|----------:|------------:|------------------:|
|   SQLite |     Clr |   1000 |    157.1 ms |    NA |    5000.0000 |   2000.0000 |         - |    28.05 MB |            917504 |
| Doublets |     Clr |   1000 |    106.9 ms |    NA |   35000.0000 |   1000.0000 |         - |   142.43 MB |            767616 |
|   SQLite |    Core |   1000 |    144.1 ms |    NA |    4000.0000 |   1000.0000 |         - |    27.17 MB |            917504 |
| Doublets |    Core |   1000 |    121.2 ms |    NA |   35000.0000 |   2000.0000 |         - |   141.93 MB |            767616 |
|   SQLite |     Clr |  10000 |  1,669.2 ms |    NA |   57000.0000 |  19000.0000 |         - |    283.3 MB |           9064448 |
| Doublets |     Clr |  10000 |  1,016.3 ms |    NA |  309000.0000 |  24000.0000 |         - |  1246.77 MB |           6528256 |
|   SQLite |    Core |  10000 |  1,363.3 ms |    NA |   53000.0000 |  16000.0000 |         - |   274.84 MB |           9064448 |
| Doublets |    Core |  10000 |    944.7 ms |    NA |  308000.0000 |  32000.0000 |         - |  1242.26 MB |           6528256 |
|   SQLite |     Clr | 100000 | 16,270.9 ms |    NA |  595000.0000 | 154000.0000 | 1000.0000 |   2855.7 MB |          90714112 |
| Doublets |     Clr | 100000 | 11,093.7 ms |    NA | 3147000.0000 | 327000.0000 |         - | 12628.43 MB |          64192256 |
|   SQLite |    Core | 100000 | 15,128.5 ms |    NA |  575000.0000 | 152000.0000 | 1000.0000 |  2771.32 MB |          90714112 |
| Doublets |    Core | 100000 | 11,758.4 ms |    NA | 3136000.0000 | 335000.0000 |         - | 12584.04 MB |          64192256 |

// * Legends *
  N                 : Value of the 'N' parameter
  Mean              : Arithmetic mean of all measurements
  Error             : Half of 99.9% confidence interval
  Gen 0             : GC Generation 0 collects per 1000 operations
  Gen 1             : GC Generation 1 collects per 1000 operations
  Gen 2             : GC Generation 2 collects per 1000 operations
  Allocated         : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)
  SizeAfterCreation : Allocated memory on disk after all records are created (1KB = 1024B)
  1 ms              : 1 Millisecond (0.001 sec)
```


## Заключение

В этом конкретном сравнении Дуплеты работают быстрее и используют меньше памяти на диске, но это достигается за счёт дополнительного использования оперативной памяти (Sqlite использует её меньше).
