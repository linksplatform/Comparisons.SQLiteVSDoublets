[![Rust](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/rust.yml/badge.svg)](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/rust.yml) [![C#](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/csharp.yml/badge.svg)](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/csharp.yml) [![Benchmarks](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/benchmarks.yml/badge.svg)](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/benchmarks.yml)

# Comparisons.SQLiteVSDoublets ([русская версия](README.ru.md))

![Comparison of models](https://github.com/LinksPlatform/Documentation/raw/master/doc/ModelsComparison/relational_model_vs_associative_model_vs_links.png)

Comparison of SQLite and LinksPlatform's Doublets (links) on basic embedded database operations with links and object-like structures.

Based on examples from https://github.com/FahaoTang/dotnetcore-examples and https://github.com/Konard/LinksPlatform

## Benchmarks

Rust ([doublets](https://crates.io/crates/doublets) and [rusqlite](https://crates.io/crates/rusqlite) with bundled SQLite) and C# ([Platform.Data.Doublets](https://www.nuget.org/packages/Platform.Data.Doublets), [Platform.Data.Doublets.Sequences](https://www.nuget.org/packages/Platform.Data.Doublets.Sequences) and [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite)) run the same workloads on the same deterministic data, with 32 bit (`u32`/`uint`) and 64 bit (`u64`/`ulong`) ids:

- **Links**: SQLite stores the table `links(id INTEGER PRIMARY KEY, "from", "to")` with the indices `("from", "to")` and `("to", "from")`; Doublets stores the links themselves. The operations are create, read all, read by id, search by `(from, to)`, read by `from`, read by `to`, update (swap `from` and `to`) and delete.
- **Objects**: blog posts with a title, content and publication date. SQLite stores the table `blog_posts(id, title, content, publication_date)`; Doublets stores every post as links, with strings as sequences of Unicode symbols, like [Platform.Data.Doublets.Sequences](https://github.com/linksplatform/Data.Doublets.Sequences) does. The operations are create, read all, read by id and delete. Every Doublets store runs with and without a cache of the string sequences (`Cached`/`Uncached`).

Storages: SQLite in memory and in a file; Doublets united (one array of links with index trees) and split (separate data and index arrays), each volatile (in memory) and non-volatile (in memory-mapped files). Doublets are compared with SQLite of the same durability: volatile with `SQLite Memory`, non-volatile with `SQLite File`.

Every repetition runs on a fresh store in an empty directory, after one discarded warm-up repetition on up to 10,000 records. Each operation is one timed transaction over all records, point operations visit the records in a scattered order, and each result is checked against the expected count and order-sensitive checksum, so a storage that loses, duplicates or mixes up records fails the run instead of being reported as fast. Repetitions are `3,000,000 / size` for links and `500,000 / size` for objects, clamped to `1..=10`. The tables show the median time per operation; a difference is reported only if the interquartile ranges (the middle half) of the repetitions do not overlap and the medians differ by more than 5%, otherwise it is `≈ same`. The file size is measured after creation; Doublets files are preallocated memory-mapped files, so small stores show the preallocation size.

Every table is measured by its own GitHub Actions job, with all variants on the same runner, by the [Benchmarks workflow](.github/workflows/benchmarks.yml): links with 100,000, 1,000,000 and 10,000,000 records and objects with 100,000 and 1,000,000 records (larger sizes do not fit into the 6 hour limit of a job). Pull requests check the whole pipeline on 1,000 records, and pushes to `main` update the results below. The workflow can also be started manually with other sizes.

Run locally:

```bash
mkdir -p results
cargo run --release --manifest-path rust/Cargo.toml -- links 64 100000 --output results/links-rust-64-100000.json
dotnet run -c Release --project csharp/SQLiteVSDoublets -- objects 32 1000 --variants SQLite_File,Doublets_Split_NonVolatile_Cached
python3 scripts/benchmark_report.py results  # prints the tables, add --readme README.md --charts docs/benchmarks to update this file
```

Notes:

- The split stores of doublets 0.5.0 (Rust) lose a link that is updated to reference itself, so the benchmark updates a link to `(0, 0)` first, which is the state links are created in ([experiments/split_store_delete](experiments/split_store_delete)).
- The stores of doublets 0.5.0 (Rust) take the part of the memory that `platform-mem` 0.3.0 returns after growing it for the whole memory, so a store fails after 1,040,384 links; the benchmarks wrap the memory in [`memory::Whole`](rust/src/memory.rs), which returns the whole memory ([experiments/unit_store_growth](experiments/unit_store_growth)).
- In C#, links are deleted with `Delete(id, handler: null)`, which resets the link before deleting it; the bare `Delete(id)` leaves the link in the index trees, and later searches fail ([experiments/csharp_tree_delete](experiments/csharp_tree_delete)).
- The C# united stores use AVL index trees: the default size balanced trees degenerate when many links share a source or a target, which makes each blog post creation linear in the number of posts ([experiments/csharp_objects_profile](experiments/csharp_objects_profile)).
- Objects stores use external references for numbers and Unicode symbols, so raw values never collide with link ids.
- Reading a string without the cache walks its sequence link by link; in C# each `GetSource`/`GetTarget` call of `Platform.Data` allocates a handler and a list, which makes the uncached C# reads much slower than the Rust ones.

<!--BENCHMARK_RESULTS_START-->
## Doublets vs SQLite as storage for links

### Rust doublets vs SQLite

#### 32 bit address/id space benchmarks

_No results yet._

#### 64 bit address/id space benchmarks

_No results yet._

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

_No results yet._

#### 64 bit address/id space benchmarks

_No results yet._

## Doublets vs SQLite as storage for objects

### Rust doublets vs SQLite

#### 32 bit address/id space benchmarks

_No results yet._

#### 64 bit address/id space benchmarks

_No results yet._

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

_No results yet._

#### 64 bit address/id space benchmarks

_No results yet._
<!--BENCHMARK_RESULTS_END-->

## Original comparison

The original C# object comparison and its historical results.

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

### Doublets
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

### [Result](https://www.icloud.com/keynote/0cYVNWkWD5RLU0k-XIBs3qWkA#Sqlite_vs_Doublets)

#### Performance
![Image with result of performance comparison between SQLite and Doublets.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_performance.png "Result of performance comparison between SQLite and Doublets")

#### Disk usage
![Image with result of disk usage comparison between SQLite and Doublets.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_disk_usage.png "Result of disk usage comparison between SQLite and Doublets")

#### RAM usage
![Image with result of RAM usage comparison between SQLite and Doublets.](https://raw.githubusercontent.com/linksplatform/Documentation/master/doc/Examples/sqlite_vs_doublets_ram_usage.png "Result of RAM usage comparison between SQLite and Doublets")

#### Source data
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

### Conclusion

In this particular comparison, Doublets are faster and use less memory on disk, but this comes with the cost of additional use of RAM (Sqlite uses it less).
