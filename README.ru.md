[![Rust](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/rust.yml/badge.svg)](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/rust.yml) [![C#](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/csharp.yml/badge.svg)](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/csharp.yml) [![Benchmarks](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/benchmarks.yml/badge.svg)](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/benchmarks.yml)

# Comparisons.SQLiteVSDoublets ([english version](README.md))

![Сравнение моделей данных](https://github.com/LinksPlatform/Documentation/raw/master/doc/ModelsComparison/relational_model_vs_associative_model_vs_links_ru.png)

Сравнение SQLite и Дуплетов ПлатформыСвязей на базовых операциях встроенных баз данных со связями и объектоподобными структурами.

Основано на примерах из https://github.com/FahaoTang/dotnetcore-examples и https://github.com/Konard/LinksPlatform

## Тесты производительности

Rust ([doublets](https://crates.io/crates/doublets) и [rusqlite](https://crates.io/crates/rusqlite) со встроенной SQLite) и C# ([Platform.Data.Doublets](https://www.nuget.org/packages/Platform.Data.Doublets), [Platform.Data.Doublets.Sequences](https://www.nuget.org/packages/Platform.Data.Doublets.Sequences) и [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite)) выполняют одинаковую нагрузку на одинаковых детерминированных данных с 32-битными (`u32`/`uint`) и 64-битными (`u64`/`ulong`) идентификаторами:

- **Связи**: SQLite хранит таблицу `links(id INTEGER PRIMARY KEY, "from", "to")` с индексами `("from", "to")` и `("to", "from")`; Дуплеты хранят сами связи. Операции: создание, чтение всех, чтение по id, поиск по `(from, to)`, чтение по `from`, чтение по `to`, обновление (обмен `from` и `to`) и удаление.
- **Объекты**: записи блога с заголовком, содержимым и датой публикации. SQLite хранит таблицу `blog_posts(id, title, content, publication_date)`; Дуплеты хранят каждую запись в виде связей, а строки — в виде последовательностей символов Юникода, как это делает [Platform.Data.Doublets.Sequences](https://github.com/linksplatform/Data.Doublets.Sequences). Операции: создание, чтение всех, чтение по id и удаление. Каждое хранилище Дуплетов проверяется с кэшем последовательностей строк и без него (`Cached`/`Uncached`).

Хранилища: SQLite в памяти и в файле; Дуплеты объединённые (один массив связей с деревьями индексов) и разделённые (отдельные массивы данных и индексов), каждое энергозависимое (в памяти) и энергонезависимое (в отображаемых в память файлах). Дуплеты сравниваются с SQLite той же надёжности хранения: энергозависимые с `SQLite Memory`, энергонезависимые с `SQLite File`.

Каждый повтор выполняется на новом хранилище в пустой папке после одного отбрасываемого прогревочного повтора на не более чем 10 000 записей. Каждая операция — одна измеряемая транзакция по всем записям, точечные операции обходят записи в перемешанном порядке, а каждый результат сверяется с ожидаемым количеством и зависящей от порядка контрольной суммой, поэтому хранилище, которое теряет, дублирует или путает записи, завершает запуск ошибкой, а не попадает в отчёт как быстрое. Число повторов — `3 000 000 / размер` для связей и `500 000 / размер` для объектов, но от 1 до 10. В таблицах указано медианное время одной операции; разница указывается, только если межквартильные диапазоны (средняя половина) повторов не пересекаются и медианы отличаются больше чем на 5%, иначе пишется `≈ так же`. Размер файлов измеряется после создания; файлы Дуплетов — заранее выделенные отображаемые в память файлы, поэтому для небольших хранилищ показан размер предварительного выделения.

Каждая таблица измеряется отдельной задачей GitHub Actions со всеми вариантами на одной машине в [workflow Benchmarks](.github/workflows/benchmarks.yml): связи на 100 000, 1 000 000 и 10 000 000 записей, объекты на 100 000 и 1 000 000 записей (большие размеры не укладываются в 6-часовое ограничение задачи). Pull request проверяют весь процесс на 1 000 записей, а push в `main` обновляет результаты ниже. Workflow можно запустить и вручную с другими размерами.

Локальный запуск:

```bash
mkdir -p results
cargo run --release --manifest-path rust/Cargo.toml -- links 64 100000 --output results/links-rust-64-100000.json
dotnet run -c Release --project csharp/SQLiteVSDoublets -- objects 32 1000 --variants SQLite_File,Doublets_Split_NonVolatile_Cached
python3 scripts/benchmark_report.py results  # печатает таблицы, добавьте --readme README.ru.md --charts docs/benchmarks для обновления этого файла
```

Замечания:

- Разделённые хранилища doublets 0.5.0 (Rust) теряют связь, обновлённую так, чтобы она ссылалась на саму себя, поэтому тест сначала обновляет связь до `(0, 0)` — состояния, в котором связи создаются ([experiments/split_store_delete](experiments/split_store_delete)).
- Хранилища doublets 0.5.0 (Rust) принимают часть памяти, которую `platform-mem` 0.3.0 возвращает после её увеличения, за всю память, поэтому хранилище ломается после 1 040 384 связей; тесты оборачивают память в [`memory::Whole`](rust/src/memory.rs), которая возвращает всю память ([experiments/unit_store_growth](experiments/unit_store_growth)).
- В C# связи удаляются через `Delete(id, handler: null)`, который сбрасывает связь перед удалением; простой `Delete(id)` оставляет связь в деревьях индексов, и последующий поиск ломается ([experiments/csharp_tree_delete](experiments/csharp_tree_delete)).
- Объединённые хранилища на C# используют АВЛ-деревья индексов: деревья по умолчанию, сбалансированные по размеру, вырождаются, когда много связей имеют общее начало или конец, и создание каждой записи блога становится линейным по числу записей ([experiments/csharp_objects_profile](experiments/csharp_objects_profile)).
- Хранилища объектов используют внешние ссылки для чисел и символов Юникода, поэтому исходные значения никогда не совпадают с идентификаторами связей.
- Чтение строки без кэша обходит её последовательность связь за связью; в C# каждый вызов `GetSource`/`GetTarget` из `Platform.Data` выделяет обработчик и список, поэтому чтение без кэша на C# значительно медленнее, чем на Rust.

<!--BENCHMARK_RESULTS_START-->
## Дуплеты против SQLite как хранилище связей

### Дуплеты на Rust против SQLite

#### Тесты с 32-битным пространством адресов/идентификаторов

_Результатов пока нет._

#### Тесты с 64-битным пространством адресов/идентификаторов

_Результатов пока нет._

### Дуплеты на C# против SQLite

#### Тесты с 32-битным пространством адресов/идентификаторов

_Результатов пока нет._

#### Тесты с 64-битным пространством адресов/идентификаторов

_Результатов пока нет._

## Дуплеты против SQLite как хранилище объектов

### Дуплеты на Rust против SQLite

#### Тесты с 32-битным пространством адресов/идентификаторов

_Результатов пока нет._

#### Тесты с 64-битным пространством адресов/идентификаторов

_Результатов пока нет._

### Дуплеты на C# против SQLite

#### Тесты с 32-битным пространством адресов/идентификаторов

_Результатов пока нет._

#### Тесты с 64-битным пространством адресов/идентификаторов

_Результатов пока нет._
<!--BENCHMARK_RESULTS_END-->

## Исходное сравнение

Исходное сравнение объектов на C# и его исторические результаты.

### SQLite
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
            using var dbContext = new SQLiteDbContext(DbFilename);
            dbContext.Database.EnsureCreated();
        }

        public override void CreateList()
        {
            using var dbContext = new SQLiteDbContext(DbFilename);
            dbContext.BlogPosts.AddRange(BlogPosts.List);
            dbContext.SaveChanges();
        }

        public override void ReadList()
        {
            using var dbContext = new SQLiteDbContext(DbFilename);
            foreach (var blogPost in dbContext.BlogPosts)
            {
                ReadBlogPosts.Add(blogPost);
            }
        }

        public override void DeleteList()
        {
            using var dbContext = new SQLiteDbContext(DbFilename);
            var blogPostsToDelete = dbContext.BlogPosts.ToList();
            dbContext.BlogPosts.RemoveRange(blogPostsToDelete);
            dbContext.SaveChanges();
        }
    }
}
```

### Дуплеты
``` C#
using System.IO;
using Platform.IO;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets.Doublets
{
    public class DoubletsTestRun : TestRun
    {
        public string DbIndexFilename { get; }

        public DoubletsTestRun(string dbFilename) : base(dbFilename) => DbIndexFilename = $"{Path.GetFileNameWithoutExtension(dbFilename)}.links.index";

        public override void Prepare()
        {
            using var dbContext = new DoubletsDbContext(DbFilename, DbIndexFilename);
        }

        public override void CreateList()
        {
            using var dbContext = new DoubletsDbContext(DbFilename, DbIndexFilename);
            foreach (var blogPost in BlogPosts.List)
            {
                dbContext.SaveBlogPost(blogPost);
            }
        }

        public override void ReadList()
        {
            using var dbContext = new DoubletsDbContext(DbFilename, DbIndexFilename);
            foreach (var blogPost in dbContext.BlogPosts)
            {
                ReadBlogPosts.Add(blogPost);
            }
        }

        public override void DeleteList()
        {
            using var dbContext = new DoubletsDbContext(DbFilename, DbIndexFilename);
            var blogPostsToDelete = dbContext.BlogPosts;
            foreach (var blogPost in blogPostsToDelete)
            {
                dbContext.Delete((uint)blogPost.Id);
            }
        }

        protected override void DeleteDatabase()
        {
            File.Delete(DbFilename);
            File.Delete(DbIndexFilename);
        }

        protected override long GetDatabaseSizeInBytes() => FileHelpers.GetSize(DbFilename) + FileHelpers.GetSize(DbIndexFilename);
    }
}
```

### [Результат](https://www.icloud.com/keynote/0cYVNWkWD5RLU0k-XIBs3qWkA#Sqlite_vs_Doublets)

#### Производительность
![Изображение с результатом сравнения производительности SQLite и Дуплетов.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_performance.png "Результат сравнения производительности SQLite и Дуплетов")

#### Использование пространства на диске
![Изображение с результатом сравнения использования пространства на диске SQLite и Дуплетов.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_disk_usage.png "Результат сравнения использования пространства на диске SQLite и Дуплетов")

#### Использование оперативной памяти
![Изображение с результатом сравнения использования оперативной памяти SQLite и Дуплетов.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_ram_usage.png "Результат сравнения использования оперативной памяти SQLite и Дуплетов")

#### Исходные данные
``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18362
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.0.100
  [Host]     : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT
  Job-AYEMIX : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT

InvocationCount=1  IterationCount=1  UnrollFactor=1  
WarmupCount=2  

```
|   Method |      N |        Mean | Error |        Gen 0 |       Gen 1 | Gen 2 |   Allocated | SizeAfterCreation |
|--------- |------- |------------:|------:|-------------:|------------:|------:|------------:|------------------:|
|   **SQLite** |   **1000** |    **719.1 ms** |    **NA** |    **5000.0000** |           **-** |     **-** |    **30.67 MB** |            **925696** |
| Doublets |   1000 |    145.0 ms |    NA |   34000.0000 |   1000.0000 |     - |   139.37 MB |            767616 |
|   **SQLite** |  **10000** |  **2,770.3 ms** |    **NA** |   **64000.0000** |  **19000.0000** |     **-** |   **315.71 MB** |           **9056256** |
| Doublets |  10000 |  1,003.8 ms |    NA |  304000.0000 |  31000.0000 |     - |  1220.55 MB |           6528256 |
|   **SQLite** | **100000** | **35,853.8 ms** |    **NA** |  **680000.0000** | **151000.0000** |     **-** |  **3234.09 MB** |          **90890240** |
| Doublets | 100000 | 13,083.4 ms |    NA | 3088000.0000 | 328000.0000 |     - | 12356.33 MB |          64192256 |


### Заключение

В этом конкретном сравнении Дуплеты работают быстрее и используют меньше памяти на диске, но это достигается за счёт дополнительного использования оперативной памяти (Sqlite использует её меньше).
