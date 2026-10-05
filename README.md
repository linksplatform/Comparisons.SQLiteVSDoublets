# Comparisons.SQLiteVSDoublets ([русская версия](README.ru.md))

[![Rust](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/rust.yml/badge.svg)](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/rust.yml)
[![C#](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/csharp.yml/badge.svg)](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/csharp.yml)
[![Benchmarks](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/benchmarks.yml/badge.svg)](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/workflows/benchmarks.yml)

![Comparison of models](https://github.com/LinksPlatform/Documentation/raw/master/doc/ModelsComparison/relational_model_vs_associative_model_vs_links.png)

Comparison of SQLite and LinksPlatform's Doublets (links) on basic embedded
database operations with links and object-like structures.

Based on examples from <https://github.com/FahaoTang/dotnetcore-examples> and
<https://github.com/Konard/LinksPlatform>

## Benchmarks

Rust ([doublets](https://crates.io/crates/doublets) and
[rusqlite](https://crates.io/crates/rusqlite) with bundled SQLite) and C#
([Platform.Data.Doublets](https://www.nuget.org/packages/Platform.Data.Doublets),
[Platform.Data.Doublets.Sequences](https://www.nuget.org/packages/Platform.Data.Doublets.Sequences)
and the providers [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite)
and [System.Data.SQLite](https://www.nuget.org/packages/System.Data.SQLite))
run the same workloads on the same deterministic data, with 32 bit
(`u32`/`uint`) and 64 bit (`u64`/`ulong`) ids:

- **Links**: SQLite stores the table
  `links(id INTEGER PRIMARY KEY, "from", "to")` with the indices
  `("from", "to")` and `("to", "from")`; Doublets stores the links themselves.
  The operations are create, read all, read by id, search by `(from, to)`,
  read by `from`, read by `to`, update (swap `from` and `to`) and delete.
- **Objects**: blog posts with a title, content and publication date. SQLite
  stores the table `blog_posts(id, title, content, publication_date)`;
  Doublets stores every post as links, with strings as sequences of Unicode
  symbols, like
  [Platform.Data.Doublets.Sequences](https://github.com/linksplatform/Data.Doublets.Sequences)
  does. The operations are create, read all, read by id and delete. Every
  Doublets store runs with and without a cache of the string sequences
  (`Cached`/`Uncached`).

Storages: SQLite in memory and in a file; Doublets united (one array of links
with index trees) and split (separate data and index arrays), each volatile
(in memory) and non-volatile (in memory-mapped files). Doublets are compared
with SQLite of the same durability: volatile with `SQLite Memory`,
non-volatile with `SQLite File`.

C# also runs `SystemDataSQLite_Memory` and `SystemDataSQLite_File` with
System.Data.SQLite 2.0.4. `SQLite_Memory` and `SQLite_File` use
Microsoft.Data.Sqlite. Both providers use the same schemas, parameterized
commands, immediate transactions and native `e_sqlite3` library supplied by
Microsoft.Data.Sqlite, on Linux, macOS and Windows. The JSON reports record
each provider and engine version in `sqlite_providers`. The System.Data.SQLite
rows compare with Microsoft.Data.Sqlite of the same durability. Published
tables from older runs include only the providers measured in those runs;
the next Benchmarks run adds the new rows.

C# object benchmarks also support `PostgreSQL_EFCore` using
[Npgsql.EntityFrameworkCore.PostgreSQL](https://www.npgsql.org/efcore/).
Set `POSTGRESQL_CONNECTION_STRING` to include it in the default object comparison;
embedded benchmarks work without a PostgreSQL server. Explicitly selecting it
without configuration fails with a setup message. Both id widths use the same
deterministic posts, scattered point reads, validated operations, warm-ups and
transaction boundaries as the other object stores. EF saves one post at a time,
clears tracking after each save, reads without tracking and deletes by id.
Each warm-up and repetition creates and drops its own generated schema; the
database and other schemas remain intact. The configured user needs permission
to connect and create schemas in a benchmark database.

PostgreSQL is a server database and EF Core adds ORM and network costs. Its row
shows absolute timings without an embedded-storage speed ratio. `server_bytes`
measures the benchmark table, indexes and TOAST storage after creation, shown in
a separate server-relations column; it excludes shared WAL, server memory and
local client files. JSON and generated tables record PostgreSQL, Npgsql EF Core
and EF Core versions. Existing published measurements stay intact. CI adds a
pinned PostgreSQL service to each C# object benchmark job so all compared
variants still run on the same machine. Set `POSTGRESQL_VERBOSE=1` to log EF
diagnostics to stderr; logging is off by default and sensitive values are disabled.

Every repetition runs on a fresh store in an empty directory, after discarded
warm-up repetitions on up to 10,000 records that run for at least a second, so
the .NET JIT has already optimized the code. Each operation is one
timed transaction over all records, point operations visit the records in a
scattered order, and each result is checked against the expected count and
order-sensitive checksum, so a storage that loses, duplicates or mixes up
records fails the run instead of being reported as fast. Repetitions are
`3,000,000 / size` for links and `500,000 / size` for objects, clamped to
`1..=10`. The tables show the median time per operation; a difference is
reported only if the interquartile ranges (the middle half) of the
repetitions do not overlap and the medians differ by more than 5%, otherwise
it is `≈ same`. The file size is measured after creation; Doublets files are
preallocated memory-mapped files, so small stores show the preallocation size.

Every table is measured by its own GitHub Actions job, with all variants on
the same runner, so that a difference is never a difference between machines,
by the [Benchmarks workflow](.github/workflows/benchmarks.yml): links with
100,000, 1,000,000 and 10,000,000 records and objects with 100,000 and
1,000,000 records. These are the largest sizes that fit into the 6 hour limit
of a job: one SQLite File repetition on 100,000,000 links did not finish in
4.5 hours
([run 37228604862](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37228604862)),
and 1,000,000 blog posts already take an hour in C#. Pull requests check the
whole pipeline on 1,000 records, and pushes to `main` update the results below.
The workflow can also be started manually with other sizes.

Run locally:

```bash
mkdir -p results
cargo run --release --manifest-path rust/Cargo.toml -- \
  links 64 100000 --output results/links-rust-64-100000.json
dotnet run -c Release --project csharp/SQLiteVSDoublets -- \
  objects 32 1000 --variants SQLite_File,Doublets_Split_NonVolatile_Cached
dotnet run -c Release --project csharp/SQLiteVSDoublets -- \
  links 64 1000 --repetitions 3 \
  --variants SQLite_Memory,SystemDataSQLite_Memory \
  --output results/links-csharp-64-1000.json
# print the tables, add --readme README.md --charts docs/benchmarks to update
python3 scripts/benchmark_report.py results
```

Run a bounded PostgreSQL comparison:

```bash
docker run -d --name benchmark-postgres -p 127.0.0.1:5432:5432 \
  -e POSTGRES_USER=benchmark -e POSTGRES_PASSWORD=benchmark \
  -e POSTGRES_DB=benchmark postgres:18.3
export POSTGRESQL_CONNECTION_STRING='Host=127.0.0.1;Database=benchmark;Username=benchmark;Password=benchmark'
dotnet run -c Release --project csharp/SQLiteVSDoublets -- \
  objects 64 1000 --repetitions 3 \
  --variants SQLite_Memory,SQLite_File,PostgreSQL_EFCore \
  --output results/objects-csharp-64-1000.json
python3 scripts/benchmark_report.py results
python3 experiments/postgresql/check_reports.py
docker stop benchmark-postgres
docker rm benchmark-postgres
```

Notes:

- The split stores of doublets 0.5.0 (Rust) lose a link that is updated to
  reference itself, so the benchmark updates a link to `(0, 0)` first, which
  is the state links are created in
  ([experiments/split_store_delete](experiments/split_store_delete)).
- The stores of doublets 0.5.0 (Rust) take the part of the memory that
  `platform-mem` 0.3.0 returns after growing it for the whole memory, so a
  store fails after 1,040,384 links; the benchmarks wrap the memory in
  [`memory::Whole`](rust/src/memory.rs), which returns the whole memory
  ([experiments/unit_store_growth](experiments/unit_store_growth)).
- In C#, links are deleted with `Delete(id, handler: null)`, which resets the
  link before deleting it; the bare `Delete(id)` leaves the link in the index
  trees, and later searches fail
  ([experiments/csharp_tree_delete](experiments/csharp_tree_delete)).
- The C# united stores use AVL index trees: the default size balanced trees
  degenerate when many links share a source or a target, which makes each
  blog post creation linear in the number of posts
  ([experiments/csharp_objects_profile](experiments/csharp_objects_profile)).
- The C# split stores keep the default linked list of the internal sources,
  which doublets 0.5.0 (Rust) does not have; it makes the C# updates faster
  and the other operations take about the same time
  ([experiments/csharp_split_linked_list](experiments/csharp_split_linked_list)).
- Objects stores use external references for numbers and Unicode symbols, so
  raw values never collide with link ids.
- Reading a string without the cache walks its sequence link by link; in C#
  each `GetSource`/`GetTarget` call of `Platform.Data` allocates a handler and
  a list, which makes the uncached C# reads much slower than the Rust ones.

<!--BENCHMARK_RESULTS_START-->
<!-- markdownlint-disable MD013 MD024 -->
## Doublets vs SQLite as storage for links

### Rust doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.73 µs | 128 ns | 600 ns | 733 ns | 770 ns | 771 ns | 3.18 µs | 2.07 µs | — |
| SQLite File | 2.07 µs | 133 ns | 624 ns | 773 ns | 803 ns | 806 ns | 10.5 µs | 5.22 µs | 4.4 MiB |
| Doublets United Volatile | 428 ns (4.04× faster) | 1.77 ns (72.5× faster) | 4.21 ns (142× faster) | 171 ns (4.28× faster) | 225 ns (3.42× faster) | 238 ns (3.24× faster) | 1.1 µs (2.9× faster) | 427 ns (4.84× faster) | — |
| Doublets United NonVolatile | 440 ns (4.69× faster) | 1.88 ns (70.7× faster) | 4.13 ns (151× faster) | 185 ns (4.18× faster) | 237 ns (3.39× faster) | 242 ns (3.33× faster) | 1.12 µs (9.39× faster) | 434 ns (12× faster) | 32.0 MiB |
| Doublets Split Volatile | 98.7 ns (17.5× faster) | 3.76 ns (34.2× faster) | 4.13 ns (145× faster) | 52.7 ns (13.9× faster) | 27.5 ns (28× faster) | 28.6 ns (26.9× faster) | 158 ns (20.2× faster) | 572 ns (3.62× faster) | — |
| Doublets Split NonVolatile | 102 ns (20.2× faster) | 3.75 ns (35.5× faster) | 3.88 ns (161× faster) | 54.9 ns (14.1× faster) | 28.6 ns (28.1× faster) | 29.8 ns (27× faster) | 166 ns (63.7× faster) | 593 ns (8.79× faster) | 40.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.21 µs | 126 ns | 798 ns | 920 ns | 950 ns | 930 ns | 5.56 µs | 3.71 µs | — |
| SQLite File | 5.61 µs | 131 ns | 1.84 µs | 1.82 µs | 1.85 µs | 1.86 µs | 18.5 µs | 10.3 µs | 48.2 MiB |
| Doublets United Volatile | 704 ns (3.14× faster) | 1.98 ns (63.8× faster) | 19.2 ns (41.5× faster) | 368 ns (2.5× faster) | 547 ns (1.74× faster) | 615 ns (1.51× faster) | 2.26 µs (2.46× faster) | 822 ns (4.51× faster) | — |
| Doublets United NonVolatile | 760 ns (7.38× faster) | 2.3 ns (56.9× faster) | 18.7 ns (98.8× faster) | 466 ns (3.91× faster) | 585 ns (3.17× faster) | 570 ns (3.27× faster) | 2.35 µs (7.87× faster) | 801 ns (12.9× faster) | 32.0 MiB |
| Doublets Split Volatile | 200 ns (11× faster) | 3.97 ns (31.8× faster) | 23.1 ns (34.5× faster) | 173 ns (5.31× faster) | 124 ns (7.65× faster) | 118 ns (7.91× faster) | 521 ns (10.7× faster) | 1.4 µs (2.65× faster) | — |
| Doublets Split NonVolatile | 192 ns (29.3× faster) | 4.01 ns (32.6× faster) | 25.5 ns (72.3× faster) | 184 ns (9.9× faster) | 120 ns (15.4× faster) | 124 ns (15.1× faster) | 537 ns (34.5× faster) | 1.51 µs (6.83× faster) | 40.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 3.14 µs | 98 ns | 1.39 µs | 1.38 µs | 1.41 µs | 1.39 µs | 8.44 µs | 5.81 µs | — |
| SQLite File | 8.78 µs | 99.8 ns | 2.12 µs | 2.39 µs | 2.32 µs | 2.32 µs | 23.2 µs | 13.8 µs | 501.0 MiB |
| Doublets United Volatile | 1.97 µs (1.59× faster) | 1.49 ns (65.7× faster) | 34.1 ns (40.7× faster) | 1.28 µs (1.07× faster) | 1.58 µs (1.12× slower) | 1.63 µs (1.17× slower) | 6.81 µs (1.24× faster) | 2.81 µs (2.07× faster) | — |
| Doublets United NonVolatile | 2.4 µs (3.65× faster) | 1.48 ns (67.2× faster) | 32 ns (66.1× faster) | 1.34 µs (1.79× faster) | 1.73 µs (1.34× faster) | 1.71 µs (1.36× faster) | 7.49 µs (3.1× faster) | 3.07 µs (4.49× faster) | 320.0 MiB |
| Doublets Split Volatile | 337 ns (9.32× faster) | 2.83 ns (34.7× faster) | 30.9 ns (44.9× faster) | 238 ns (5.79× faster) | 171 ns (8.23× faster) | 170 ns (8.18× faster) | 689 ns (12.3× faster) | 2.78 µs (2.09× faster) | — |
| Doublets Split NonVolatile | 490 ns (17.9× faster) | 2.8 ns (35.7× faster) | 31.6 ns (67× faster) | 244 ns (9.81× faster) | 177 ns (13.1× faster) | 180 ns (12.9× faster) | 737 ns (31.5× faster) | 3.13 µs (4.39× faster) | 400.0 MiB |

![Rust doublets vs SQLite, 32 bit, links](docs/benchmarks/links-rust-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.72 µs | 128 ns | 604 ns | 732 ns | 767 ns | 770 ns | 3.2 µs | 2.06 µs | — |
| SQLite File | 2.06 µs | 133 ns | 630 ns | 775 ns | 798 ns | 802 ns | 10.6 µs | 5.26 µs | 4.4 MiB |
| Doublets United Volatile | 447 ns (3.85× faster) | 1.86 ns (68.9× faster) | 3.27 ns (184× faster) | 180 ns (4.07× faster) | 235 ns (3.26× faster) | 254 ns (3.03× faster) | 1.15 µs (2.79× faster) | 452 ns (4.56× faster) | — |
| Doublets United NonVolatile | 475 ns (4.34× faster) | 2.34 ns (56.8× faster) | 3.43 ns (183× faster) | 202 ns (3.84× faster) | 270 ns (2.95× faster) | 281 ns (2.85× faster) | 1.22 µs (8.67× faster) | 467 ns (11.3× faster) | 64.0 MiB |
| Doublets Split Volatile | 115 ns (15× faster) | 3.78 ns (33.9× faster) | 4.39 ns (137× faster) | 56.5 ns (13× faster) | 27.7 ns (27.7× faster) | 28.9 ns (26.6× faster) | 167 ns (19.1× faster) | 590 ns (3.49× faster) | — |
| Doublets Split NonVolatile | 105 ns (19.5× faster) | 3.71 ns (35.9× faster) | 4.24 ns (149× faster) | 55.9 ns (13.9× faster) | 28.7 ns (27.8× faster) | 29.9 ns (26.8× faster) | 170 ns (62.6× faster) | 610 ns (8.62× faster) | 80.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.51 µs | 126 ns | 917 ns | 1.05 µs | 1.1 µs | 1.06 µs | 6.46 µs | 4.43 µs | — |
| SQLite File | 6 µs | 131 ns | 2.01 µs | 1.97 µs | 2 µs | 1.99 µs | 19.3 µs | 10.6 µs | 48.2 MiB |
| Doublets United Volatile | 1.03 µs (2.43× faster) | 3 ns (41.8× faster) | 21.7 ns (42.2× faster) | 542 ns (1.94× faster) | 760 ns (1.44× faster) | 808 ns (1.31× faster) | 3.73 µs (1.73× faster) | 1.11 µs (4.01× faster) | — |
| Doublets United NonVolatile | 1.28 µs (4.69× faster) | 3.56 ns (36.7× faster) | 24.7 ns (81.2× faster) | 763 ns (2.58× faster) | 1 µs (2× faster) | 1.04 µs (1.91× faster) | 4.11 µs (4.7× faster) | 1.47 µs (7.21× faster) | 64.0 MiB |
| Doublets Split Volatile | 263 ns (9.53× faster) | 4.25 ns (29.5× faster) | 23 ns (39.8× faster) | 203 ns (5.19× faster) | 149 ns (7.34× faster) | 150 ns (7.08× faster) | 594 ns (10.9× faster) | 1.54 µs (2.87× faster) | — |
| Doublets Split NonVolatile | 255 ns (23.6× faster) | 4.49 ns (29.1× faster) | 25.9 ns (77.4× faster) | 220 ns (8.95× faster) | 167 ns (12× faster) | 161 ns (12.4× faster) | 666 ns (29× faster) | 1.92 µs (5.53× faster) | 80.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 3.32 µs | 96.1 ns | 1.52 µs | 1.46 µs | 1.47 µs | 1.46 µs | 8.72 µs | 6.07 µs | — |
| SQLite File | 9.04 µs | 99.2 ns | 2.14 µs | 2.41 µs | 2.32 µs | 2.33 µs | 24.6 µs | 15.2 µs | 501.0 MiB |
| Doublets United Volatile | 2.44 µs (1.36× faster) | 2.02 ns (47.5× faster) | 26 ns (58.5× faster) | 1.45 µs (≈ same) | 1.5 µs (≈ same) | 1.61 µs (1.11× slower) | 6.88 µs (1.27× faster) | 2.77 µs (2.19× faster) | — |
| Doublets United NonVolatile | 2.72 µs (3.33× faster) | 1.9 ns (52.2× faster) | 27 ns (79.1× faster) | 1.35 µs (1.78× faster) | 1.68 µs (1.38× faster) | 1.77 µs (1.32× faster) | 7.51 µs (3.28× faster) | 3.03 µs (5.02× faster) | 640.0 MiB |
| Doublets Split Volatile | 381 ns (8.71× faster) | 3.88 ns (24.7× faster) | 28 ns (54.4× faster) | 247 ns (5.91× faster) | 174 ns (8.47× faster) | 176 ns (8.31× faster) | 719 ns (12.1× faster) | 2.78 µs (2.19× faster) | — |
| Doublets Split NonVolatile | 677 ns (13.4× faster) | 4.19 ns (23.7× faster) | 36.7 ns (58.3× faster) | 248 ns (9.71× faster) | 173 ns (13.4× faster) | 179 ns (13.1× faster) | 755 ns (32.6× faster) | 3.64 µs (4.18× faster) | 800.0 MiB |

![Rust doublets vs SQLite, 64 bit, links](docs/benchmarks/links-rust-64.png)

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.48 µs | 386 ns | 1.18 µs | 1.49 µs | 1.52 µs | 1.52 µs | 4.38 µs | 2.91 µs | — |
| SQLite File | 2.84 µs | 392 ns | 1.2 µs | 1.5 µs | 1.54 µs | 1.56 µs | 12.4 µs | 6.49 µs | 4.4 MiB |
| SystemDataSQLite Memory | 2.65 µs (1.07× slower) | 337 ns (1.15× faster) | 1.47 µs (1.24× slower) | 1.8 µs (1.21× slower) | 1.85 µs (1.22× slower) | 1.81 µs (1.19× slower) | 4.37 µs (≈ same) | 3.14 µs (1.08× slower) | — |
| SystemDataSQLite File | 3.04 µs (1.07× slower) | 342 ns (1.15× faster) | 1.49 µs (1.24× slower) | 1.85 µs (1.24× slower) | 1.9 µs (1.23× slower) | 1.87 µs (1.2× slower) | 12.8 µs (≈ same) | 6.82 µs (1.05× slower) | 4.4 MiB |
| Doublets United Volatile | 1.08 µs (2.29× faster) | 15 ns (25.6× faster) | 71.6 ns (16.5× faster) | 287 ns (5.17× faster) | 300 ns (5.06× faster) | 323 ns (4.71× faster) | 2.49 µs (1.76× faster) | 1.25 µs (2.33× faster) | — |
| Doublets United NonVolatile | 1.13 µs (2.51× faster) | 15.1 ns (25.9× faster) | 71.3 ns (16.9× faster) | 334 ns (4.48× faster) | 332 ns (4.63× faster) | 329 ns (4.74× faster) | 2.55 µs (4.89× faster) | 1.29 µs (5.02× faster) | 32.0 MiB |
| Doublets Split Volatile | 216 ns (11.5× faster) | 18.2 ns (21.2× faster) | 76.2 ns (15.5× faster) | 137 ns (10.8× faster) | 105 ns (14.5× faster) | 100 ns (15.2× faster) | 258 ns (16.9× faster) | 338 ns (8.59× faster) | — |
| Doublets Split NonVolatile | 281 ns (10.1× faster) | 17.8 ns (22× faster) | 77.9 ns (15.4× faster) | 157 ns (9.55× faster) | 124 ns (12.4× faster) | 120 ns (13× faster) | 272 ns (45.7× faster) | 321 ns (20.2× faster) | 40.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.97 µs | 377 ns | 1.42 µs | 1.69 µs | 1.71 µs | 1.72 µs | 6.61 µs | 4.72 µs | — |
| SQLite File | 6.89 µs | 389 ns | 2.73 µs | 2.76 µs | 2.78 µs | 2.78 µs | 20.7 µs | 11.6 µs | 48.2 MiB |
| SystemDataSQLite Memory | 3.19 µs (1.07× slower) | 327 ns (1.15× faster) | 1.77 µs (1.25× slower) | 2.1 µs (1.24× slower) | 2.03 µs (1.19× slower) | 2 µs (1.17× slower) | 6.74 µs (≈ same) | 4.89 µs (≈ same) | — |
| SystemDataSQLite File | 6.81 µs (≈ same) | 323 ns (1.2× faster) | 2.9 µs (1.06× slower) | 3.03 µs (1.1× slower) | 3.08 µs (1.11× slower) | 3.06 µs (1.1× slower) | 20.4 µs (≈ same) | 11.7 µs (≈ same) | 48.2 MiB |
| Doublets United Volatile | 1.49 µs (1.99× faster) | 14.6 ns (25.7× faster) | 115 ns (12.3× faster) | 523 ns (3.23× faster) | 568 ns (3.01× faster) | 785 ns (2.18× faster) | 3.87 µs (1.71× faster) | 1.68 µs (2.8× faster) | — |
| Doublets United NonVolatile | 1.61 µs (4.27× faster) | 14.7 ns (26.5× faster) | 99.6 ns (27.4× faster) | 578 ns (4.77× faster) | 625 ns (4.46× faster) | 673 ns (4.13× faster) | 3.98 µs (5.19× faster) | 1.81 µs (6.42× faster) | 32.0 MiB |
| Doublets Split Volatile | 307 ns (9.65× faster) | 11.1 ns (33.9× faster) | 135 ns (10.5× faster) | 247 ns (6.85× faster) | 205 ns (8.34× faster) | 211 ns (8.13× faster) | 534 ns (12.4× faster) | 479 ns (9.84× faster) | — |
| Doublets Split NonVolatile | 369 ns (18.7× faster) | 11.3 ns (34.3× faster) | 142 ns (19.2× faster) | 248 ns (11.1× faster) | 207 ns (13.4× faster) | 212 ns (13.1× faster) | 571 ns (36.2× faster) | 479 ns (24.3× faster) | 40.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.67 µs | 471 ns | 2.22 µs | 2.51 µs | 2.58 µs | 2.57 µs | 11.4 µs | 7.45 µs | — |
| SQLite File | 8.01 µs | 470 ns | 2.5 µs | 2.89 µs | 2.9 µs | 2.91 µs | 19.9 µs | 11.9 µs | 501.0 MiB |
| SystemDataSQLite Memory | 4.35 µs (1.08× faster) | 300 ns (1.57× faster) | 2.21 µs (≈ same) | 2.48 µs (≈ same) | 2.54 µs (≈ same) | 2.54 µs (≈ same) | 10.5 µs (1.09× faster) | 7.36 µs (≈ same) | — |
| SystemDataSQLite File | 8.17 µs (≈ same) | 308 ns (1.53× faster) | 2.68 µs (1.07× slower) | 3.12 µs (1.08× slower) | 3.07 µs (1.06× slower) | 3.08 µs (1.06× slower) | 19.9 µs (≈ same) | 12.4 µs (≈ same) | 501.0 MiB |
| Doublets United Volatile | 3.12 µs (1.5× faster) | 12.2 ns (38.5× faster) | 148 ns (15× faster) | 1.21 µs (2.07× faster) | 1.43 µs (1.81× faster) | 1.44 µs (1.79× faster) | 7.61 µs (1.5× faster) | 3.45 µs (2.16× faster) | — |
| Doublets United NonVolatile | 4.55 µs (1.76× faster) | 12.2 ns (38.4× faster) | 201 ns (12.4× faster) | 1.58 µs (1.83× faster) | 1.77 µs (1.64× faster) | 1.72 µs (1.69× faster) | 10.7 µs (1.86× faster) | 5.28 µs (2.25× faster) | 320.0 MiB |
| Doublets Split Volatile | 486 ns (9.61× faster) | 10 ns (47.1× faster) | 176 ns (12.6× faster) | 431 ns (5.82× faster) | 265 ns (9.74× faster) | 257 ns (10× faster) | 694 ns (16.4× faster) | 563 ns (13.2× faster) | — |
| Doublets Split NonVolatile | 1.27 µs (6.33× faster) | 10.1 ns (46.4× faster) | 205 ns (12.2× faster) | 507 ns (5.7× faster) | 309 ns (9.38× faster) | 305 ns (9.52× faster) | 979 ns (20.4× faster) | 817 ns (14.5× faster) | 400.0 MiB |

![C# doublets vs SQLite, 32 bit, links](docs/benchmarks/links-csharp-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.9 µs | 313 ns | 909 ns | 1.12 µs | 1.16 µs | 1.17 µs | 3.34 µs | 2.27 µs | — |
| SQLite File | 2.4 µs | 318 ns | 910 ns | 1.13 µs | 1.16 µs | 1.17 µs | 11 µs | 5.87 µs | 4.4 MiB |
| SystemDataSQLite Memory | 2.04 µs (1.07× slower) | 246 ns (1.27× faster) | 1.14 µs (1.25× slower) | 1.4 µs (1.24× slower) | 1.39 µs (1.2× slower) | 1.37 µs (1.17× slower) | 3.41 µs (≈ same) | 2.49 µs (1.09× slower) | — |
| SystemDataSQLite File | 2.4 µs (≈ same) | 251 ns (1.27× faster) | 1.15 µs (1.26× slower) | 1.42 µs (1.26× slower) | 1.41 µs (1.22× slower) | 1.4 µs (1.19× slower) | 11.1 µs (≈ same) | 5.75 µs (≈ same) | 4.4 MiB |
| Doublets United Volatile | 948 ns (2.01× faster) | 17.7 ns (17.7× faster) | 51.9 ns (17.5× faster) | 218 ns (5.14× faster) | 319 ns (3.65× faster) | 236 ns (4.95× faster) | 2.06 µs (1.62× faster) | 971 ns (2.34× faster) | — |
| Doublets United NonVolatile | 1.02 µs (2.36× faster) | 18.9 ns (16.8× faster) | 51.3 ns (17.8× faster) | 240 ns (4.7× faster) | 346 ns (3.35× faster) | 264 ns (4.44× faster) | 2.18 µs (5.05× faster) | 1.03 µs (5.7× faster) | 64.0 MiB |
| Doublets Split Volatile | 170 ns (11.2× faster) | 17 ns (18.4× faster) | 63.2 ns (14.4× faster) | 103 ns (10.9× faster) | 84 ns (13.9× faster) | 79.8 ns (14.6× faster) | 192 ns (17.4× faster) | 224 ns (10.1× faster) | — |
| Doublets Split NonVolatile | 233 ns (10.3× faster) | 16.9 ns (18.8× faster) | 61.6 ns (14.8× faster) | 101 ns (11.1× faster) | 82.6 ns (14.1× faster) | 80.6 ns (14.5× faster) | 201 ns (54.7× faster) | 217 ns (27× faster) | 80.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.62 µs | 311 ns | 1.23 µs | 1.43 µs | 1.52 µs | 1.47 µs | 6.59 µs | 4.61 µs | — |
| SQLite File | 6.4 µs | 313 ns | 2.46 µs | 2.47 µs | 2.54 µs | 2.5 µs | 19.9 µs | 11.4 µs | 48.2 MiB |
| SystemDataSQLite Memory | 2.63 µs (≈ same) | 242 ns (1.29× faster) | 1.48 µs (1.2× slower) | 1.68 µs (1.18× slower) | 1.71 µs (1.12× slower) | 1.69 µs (1.15× slower) | 6.88 µs (≈ same) | 4.79 µs (≈ same) | — |
| SystemDataSQLite File | 6.43 µs (≈ same) | 247 ns (1.27× faster) | 2.79 µs (1.14× slower) | 2.77 µs (1.12× slower) | 2.82 µs (1.11× slower) | 2.82 µs (1.13× slower) | 20.2 µs (≈ same) | 11.5 µs (≈ same) | 48.2 MiB |
| Doublets United Volatile | 2.27 µs (1.15× faster) | 18.1 ns (17.2× faster) | 161 ns (7.64× faster) | 684 ns (2.08× faster) | 1.03 µs (1.48× faster) | 962 ns (1.53× faster) | 5.78 µs (1.14× faster) | 2.43 µs (1.9× faster) | — |
| Doublets United NonVolatile | 2.51 µs (2.55× faster) | 18.2 ns (17.2× faster) | 187 ns (13.1× faster) | 902 ns (2.74× faster) | 1.27 µs (1.99× faster) | 1.19 µs (2.1× faster) | 6.67 µs (2.98× faster) | 2.71 µs (4.19× faster) | 64.0 MiB |
| Doublets Split Volatile | 368 ns (7.13× faster) | 10.1 ns (30.9× faster) | 183 ns (6.72× faster) | 402 ns (3.55× faster) | 310 ns (4.92× faster) | 310 ns (4.75× faster) | 650 ns (10.1× faster) | 535 ns (8.62× faster) | — |
| Doublets Split NonVolatile | 460 ns (13.9× faster) | 10.7 ns (29.1× faster) | 202 ns (12.1× faster) | 467 ns (5.29× faster) | 348 ns (7.3× faster) | 352 ns (7.11× faster) | 732 ns (27.2× faster) | 572 ns (19.9× faster) | 80.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.5 µs | 459 ns | 2.09 µs | 2.35 µs | 2.42 µs | 2.4 µs | 9.69 µs | 6.16 µs | — |
| SQLite File | 10.5 µs | 464 ns | 2.94 µs | 3.47 µs | 3.34 µs | 3.35 µs | 25.4 µs | 15.4 µs | 501.0 MiB |
| SystemDataSQLite Memory | 4.14 µs (1.09× faster) | 305 ns (1.5× faster) | 2.16 µs (≈ same) | 2.57 µs (1.09× slower) | 2.51 µs (≈ same) | 2.5 µs (≈ same) | 8.99 µs (1.08× faster) | 6.26 µs (≈ same) | — |
| SystemDataSQLite File | 10.4 µs (≈ same) | 312 ns (1.49× faster) | 3.2 µs (1.09× slower) | 3.78 µs (1.09× slower) | 3.7 µs (1.11× slower) | 3.72 µs (1.11× slower) | 25.5 µs (≈ same) | 15.7 µs (≈ same) | 501.0 MiB |
| Doublets United Volatile | 3.1 µs (1.45× faster) | 22.2 ns (20.7× faster) | 156 ns (13.4× faster) | 1.21 µs (1.95× faster) | 1.48 µs (1.64× faster) | 1.38 µs (1.74× faster) | 8.52 µs (1.14× faster) | 3.93 µs (1.57× faster) | — |
| Doublets United NonVolatile | 4.46 µs (2.34× faster) | 21.4 ns (21.7× faster) | 184 ns (15.9× faster) | 1.52 µs (2.28× faster) | 1.83 µs (1.83× faster) | 1.5 µs (2.24× faster) | 10.2 µs (2.49× faster) | 4.85 µs (3.18× faster) | 640.0 MiB |
| Doublets Split Volatile | 449 ns (10× faster) | 12.6 ns (36.6× faster) | 294 ns (7.12× faster) | 443 ns (5.32× faster) | 317 ns (7.65× faster) | 330 ns (7.26× faster) | 859 ns (11.3× faster) | 688 ns (8.95× faster) | — |
| Doublets Split NonVolatile | 1.27 µs (8.22× faster) | 12.6 ns (36.8× faster) | 331 ns (8.88× faster) | 484 ns (7.17× faster) | 339 ns (9.86× faster) | 351 ns (9.55× faster) | 1.19 µs (21.4× faster) | 853 ns (18.1× faster) | 800.0 MiB |

![C# doublets vs SQLite, 64 bit, links](docs/benchmarks/links-csharp-64.png)

## Doublets vs SQLite as storage for objects

### Rust doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 807 ns | 512 ns | 1.21 µs | 1.49 µs | — |
| SQLite File | 5.32 µs | 419 ns | 1.73 µs | 9.08 µs | 78.3 MiB |
| Doublets United Volatile Cached | 3.18 µs (3.94× slower) | 1.02 µs (1.99× slower) | 1.74 µs (1.44× slower) | 3.87 µs (2.6× slower) | — |
| Doublets United Volatile Uncached | 52.9 µs (65.5× slower) | 6.07 µs (11.9× slower) | 7.05 µs (5.83× slower) | 5.48 µs (3.68× slower) | — |
| Doublets United NonVolatile Cached | 3.69 µs (1.44× faster) | 1.13 µs (2.69× slower) | 2.23 µs (1.29× slower) | 6.36 µs (1.43× faster) | 32.0 MiB |
| Doublets United NonVolatile Uncached | 51.5 µs (9.68× slower) | 5.79 µs (13.8× slower) | 7.41 µs (4.28× slower) | 5.91 µs (1.54× faster) | 32.0 MiB |
| Doublets Split Volatile Cached | 1.9 µs (2.36× slower) | 433 ns (1.18× faster) | 411 ns (2.94× faster) | 1.91 µs (1.28× slower) | — |
| Doublets Split Volatile Uncached | 24.8 µs (30.7× slower) | 7.9 µs (15.4× slower) | 8.13 µs (6.73× slower) | 2.8 µs (1.88× slower) | — |
| Doublets Split NonVolatile Cached | 2.29 µs (2.33× faster) | 475 ns (1.13× slower) | 513 ns (3.37× faster) | 3.21 µs (2.83× faster) | 40.0 MiB |
| Doublets Split NonVolatile Uncached | 25.1 µs (4.71× slower) | 7.73 µs (18.5× slower) | 7.88 µs (4.55× slower) | 2.98 µs (3.04× faster) | 40.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.43 µs | 594 ns | 2.03 µs | 2.84 µs | — |
| SQLite File | 3.42 µs | 659 ns | 4.06 µs | 11.4 µs | 783.2 MiB |
| Doublets United Volatile Cached | 7.57 µs (5.3× slower) | 1.51 µs (2.55× slower) | 3.13 µs (1.55× slower) | 10.9 µs (3.83× slower) | — |
| Doublets United Volatile Uncached | 97 µs (67.9× slower) | 9.71 µs (16.4× slower) | 11.4 µs (5.6× slower) | 10.4 µs (3.65× slower) | — |
| Doublets United NonVolatile Cached | 8.84 µs (2.59× slower) | 1.52 µs (2.3× slower) | 3.09 µs (1.31× faster) | 11.2 µs (≈ same) | 320.0 MiB |
| Doublets United NonVolatile Uncached | 104 µs (30.3× slower) | 9.86 µs (15× slower) | 12 µs (2.95× slower) | 11.1 µs (≈ same) | 320.0 MiB |
| Doublets Split Volatile Cached | 4.56 µs (3.19× slower) | 853 ns (1.44× slower) | 960 ns (2.11× faster) | 5.73 µs (2.02× slower) | — |
| Doublets Split Volatile Uncached | 33.7 µs (23.6× slower) | 14.8 µs (24.9× slower) | 15 µs (7.39× slower) | 6.14 µs (2.16× slower) | — |
| Doublets Split NonVolatile Cached | 6.46 µs (1.89× slower) | 882 ns (1.34× slower) | 961 ns (4.23× faster) | 6.75 µs (1.68× faster) | 400.0 MiB |
| Doublets Split NonVolatile Uncached | 36.6 µs (10.7× slower) | 14.5 µs (22.1× slower) | 14.8 µs (3.64× slower) | 6.98 µs (1.63× faster) | 400.0 MiB |

![Rust doublets vs SQLite, 32 bit, objects](docs/benchmarks/objects-rust-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 799 ns | 498 ns | 1.18 µs | 1.49 µs | — |
| SQLite File | 5.8 µs | 425 ns | 1.77 µs | 10.1 µs | 78.3 MiB |
| Doublets United Volatile Cached | 3.44 µs (4.3× slower) | 1.21 µs (2.43× slower) | 1.98 µs (1.68× slower) | 4.91 µs (3.3× slower) | — |
| Doublets United Volatile Uncached | 53.2 µs (66.5× slower) | 6.41 µs (12.9× slower) | 7.63 µs (6.47× slower) | 5.34 µs (3.59× slower) | — |
| Doublets United NonVolatile Cached | 3.51 µs (1.65× faster) | 1.22 µs (2.87× slower) | 2.19 µs (1.24× slower) | 4.88 µs (2.07× faster) | 64.0 MiB |
| Doublets United NonVolatile Uncached | 50.5 µs (8.71× slower) | 6.11 µs (14.4× slower) | 7.2 µs (4.08× slower) | 5.15 µs (1.96× faster) | 64.0 MiB |
| Doublets Split Volatile Cached | 2.11 µs (2.64× slower) | 472 ns (1.05× faster) | 543 ns (2.17× faster) | 3.1 µs (2.08× slower) | — |
| Doublets Split Volatile Uncached | 26.1 µs (32.7× slower) | 7.68 µs (15.4× slower) | 7.84 µs (6.65× slower) | 2.82 µs (1.89× slower) | — |
| Doublets Split NonVolatile Cached | 2.43 µs (2.39× faster) | 507 ns (1.19× slower) | 556 ns (3.18× faster) | 3.02 µs (3.35× faster) | 80.0 MiB |
| Doublets Split NonVolatile Uncached | 28 µs (4.83× slower) | 8.19 µs (19.3× slower) | 8.48 µs (4.8× slower) | 2.68 µs (3.77× faster) | 80.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.37 µs | 569 ns | 1.99 µs | 2.58 µs | — |
| SQLite File | 3.53 µs | 636 ns | 3.53 µs | 10.1 µs | 783.2 MiB |
| Doublets United Volatile Cached | 7.49 µs (5.47× slower) | 1.62 µs (2.85× slower) | 2.96 µs (1.49× slower) | 11.2 µs (4.32× slower) | — |
| Doublets United Volatile Uncached | 108 µs (78.8× slower) | 9.99 µs (17.5× slower) | 12 µs (6.02× slower) | 12.1 µs (4.66× slower) | — |
| Doublets United NonVolatile Cached | 10.1 µs (2.86× slower) | 1.66 µs (2.61× slower) | 3.27 µs (1.08× faster) | 12.2 µs (1.2× slower) | 640.0 MiB |
| Doublets United NonVolatile Uncached | 118 µs (33.4× slower) | 10.4 µs (16.4× slower) | 12.5 µs (3.55× slower) | 12.6 µs (1.24× slower) | 640.0 MiB |
| Doublets Split Volatile Cached | 5.33 µs (3.89× slower) | 943 ns (1.66× slower) | 988 ns (2.01× faster) | 7.1 µs (2.75× slower) | — |
| Doublets Split Volatile Uncached | 49.9 µs (36.4× slower) | 14.2 µs (24.9× slower) | 14.9 µs (7.48× slower) | 7.37 µs (2.85× slower) | — |
| Doublets Split NonVolatile Cached | 8.1 µs (2.3× slower) | 952 ns (1.5× slower) | 1.02 µs (3.45× faster) | 7.65 µs (1.32× faster) | 800.0 MiB |
| Doublets Split NonVolatile Uncached | 55.9 µs (15.9× slower) | 14.7 µs (23.1× slower) | 15.4 µs (4.35× slower) | 8.04 µs (1.26× faster) | 800.0 MiB |

![Rust doublets vs SQLite, 64 bit, objects](docs/benchmarks/objects-rust-64.png)

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._ PostgreSQL 18.3 (Debian 18.3-1.pgdg13+1); Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3.0; EF Core 10.0.4.0.

| Storage | Create | Read all | Read by id | Delete | File size | Server relations |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| PostgreSQL EFCore | 672 µs | 4.6 µs | 323 µs | 298 µs | — | 80.3 MiB |
| SQLite Memory | 5.35 µs | 4.06 µs | 5.42 µs | 2.39 µs | — | — |
| SQLite File | 6.03 µs | 4.18 µs | 6.53 µs | 7.61 µs | 78.3 MiB | — |
| SystemDataSQLite Memory | 5.03 µs (1.06× faster) | 3.92 µs (≈ same) | 5.77 µs (1.06× slower) | 2.15 µs (1.11× faster) | — | — |
| SystemDataSQLite File | 6.09 µs (≈ same) | 4.12 µs (≈ same) | 6.84 µs (≈ same) | 7.9 µs (≈ same) | 78.3 MiB | — |
| Doublets United Volatile Cached | 18.5 µs (3.46× slower) | 10.8 µs (2.66× slower) | 5.48 µs (≈ same) | 9.95 µs (4.17× slower) | — | — |
| Doublets United Volatile Uncached | 177 µs (33.1× slower) | 324 µs (79.8× slower) | 324 µs (59.8× slower) | 11 µs (4.59× slower) | — | — |
| Doublets United NonVolatile Cached | 19.3 µs (3.2× slower) | 11.2 µs (2.68× slower) | 5.47 µs (1.19× faster) | 10.3 µs (1.35× slower) | 32.0 MiB | — |
| Doublets United NonVolatile Uncached | 176 µs (29.1× slower) | 305 µs (73× slower) | 307 µs (47× slower) | 10.3 µs (1.35× slower) | 32.0 MiB | — |
| Doublets Split Volatile Cached | 10.1 µs (1.89× slower) | 11.5 µs (2.84× slower) | 4.67 µs (1.16× faster) | 5.92 µs (2.48× slower) | — | — |
| Doublets Split Volatile Uncached | 107 µs (20× slower) | 373 µs (92× slower) | 377 µs (69.5× slower) | 6.15 µs (2.58× slower) | — | — |
| Doublets Split NonVolatile Cached | 11 µs (1.82× slower) | 11.7 µs (2.8× slower) | 4.87 µs (1.34× faster) | 6.38 µs (1.19× faster) | 40.0 MiB | — |
| Doublets Split NonVolatile Uncached | 109 µs (18× slower) | 379 µs (90.8× slower) | 376 µs (57.6× slower) | 7.53 µs (≈ same) | 40.0 MiB | — |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._ PostgreSQL 18.3 (Debian 18.3-1.pgdg13+1); Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3.0; EF Core 10.0.4.0.

| Storage | Create | Read all | Read by id | Delete | File size | Server relations |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| PostgreSQL EFCore | 411 µs | 9.41 µs | 231 µs | 258 µs | — | 802.9 MiB |
| SQLite Memory | 4.51 µs | 3.63 µs | 5.66 µs | 3.36 µs | — | — |
| SQLite File | 9.05 µs | 3.69 µs | 6.81 µs | 10.8 µs | 783.2 MiB | — |
| SystemDataSQLite Memory | 4.31 µs (≈ same) | 3.47 µs (≈ same) | 5.86 µs (≈ same) | 3.33 µs (≈ same) | — | — |
| SystemDataSQLite File | 7.92 µs (1.14× faster) | 3.53 µs (≈ same) | 7.01 µs (≈ same) | 11.5 µs (1.07× slower) | 783.2 MiB | — |
| Doublets United Volatile Cached | 22.6 µs (5× slower) | 11.7 µs (3.23× slower) | 6.34 µs (1.12× slower) | 16.3 µs (4.86× slower) | — | — |
| Doublets United Volatile Uncached | 216 µs (47.9× slower) | 343 µs (94.4× slower) | 346 µs (61.1× slower) | 15.4 µs (4.58× slower) | — | — |
| Doublets United NonVolatile Cached | 30.4 µs (3.36× slower) | 11.9 µs (3.22× slower) | 6.46 µs (1.05× faster) | 19 µs (1.76× slower) | 320.0 MiB | — |
| Doublets United NonVolatile Uncached | 252 µs (27.9× slower) | 351 µs (95.1× slower) | 347 µs (51× slower) | 17 µs (1.58× slower) | 320.0 MiB | — |
| Doublets Split Volatile Cached | 12.3 µs (2.74× slower) | 13.9 µs (3.83× slower) | 6.67 µs (1.18× slower) | 9.82 µs (2.92× slower) | — | — |
| Doublets Split Volatile Uncached | 129 µs (28.5× slower) | 465 µs (128× slower) | 463 µs (81.9× slower) | 9.17 µs (2.73× slower) | — | — |
| Doublets Split NonVolatile Cached | 21 µs (2.32× slower) | 14 µs (3.79× slower) | 7.1 µs (≈ same) | 10.8 µs (≈ same) | 400.0 MiB | — |
| Doublets Split NonVolatile Uncached | 142 µs (15.7× slower) | 460 µs (125× slower) | 460 µs (67.5× slower) | 11.2 µs (≈ same) | 400.0 MiB | — |

![C# doublets vs SQLite, 32 bit, objects](docs/benchmarks/objects-csharp-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._ PostgreSQL 18.3 (Debian 18.3-1.pgdg13+1); Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3.0; EF Core 10.0.4.0.

| Storage | Create | Read all | Read by id | Delete | File size | Server relations |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| PostgreSQL EFCore | 441 µs | 3.94 µs | 236 µs | 217 µs | — | 80.3 MiB |
| SQLite Memory | 4.38 µs | 3.52 µs | 4.81 µs | 2.06 µs | — | — |
| SQLite File | 8.69 µs | 3.62 µs | 5.8 µs | 12.5 µs | 78.3 MiB | — |
| SystemDataSQLite Memory | 4.24 µs (≈ same) | 3.47 µs (≈ same) | 5.01 µs (≈ same) | 1.93 µs (1.07× faster) | — | — |
| SystemDataSQLite File | 7.79 µs (1.12× faster) | 3.55 µs (≈ same) | 6.09 µs (1.05× slower) | 12.5 µs (≈ same) | 78.3 MiB | — |
| Doublets United Volatile Cached | 16.4 µs (3.75× slower) | 9.29 µs (2.64× slower) | 5.39 µs (1.12× slower) | 12.1 µs (5.87× slower) | — | — |
| Doublets United Volatile Uncached | 158 µs (36× slower) | 251 µs (71.4× slower) | 254 µs (52.9× slower) | 12.2 µs (5.89× slower) | — | — |
| Doublets United NonVolatile Cached | 17.4 µs (2× slower) | 9.28 µs (2.56× slower) | 5.55 µs (≈ same) | 12.4 µs (≈ same) | 64.0 MiB | — |
| Doublets United NonVolatile Uncached | 163 µs (18.7× slower) | 251 µs (69.4× slower) | 254 µs (43.8× slower) | 12.7 µs (≈ same) | 64.0 MiB | — |
| Doublets Split Volatile Cached | 9.12 µs (2.08× slower) | 10.4 µs (2.96× slower) | 5.18 µs (1.08× slower) | 7.38 µs (3.58× slower) | — | — |
| Doublets Split Volatile Uncached | 89.4 µs (20.4× slower) | 310 µs (88× slower) | 313 µs (65.1× slower) | 7.24 µs (3.51× slower) | — | — |
| Doublets Split NonVolatile Cached | 10.6 µs (1.22× slower) | 10.6 µs (2.92× slower) | 5.07 µs (1.14× faster) | 7.46 µs (1.68× faster) | 80.0 MiB | — |
| Doublets Split NonVolatile Uncached | 93.2 µs (10.7× slower) | 313 µs (86.6× slower) | 317 µs (54.7× slower) | 7.92 µs (1.58× faster) | 80.0 MiB | — |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37381004025) on 2026-10-05._ PostgreSQL 18.3 (Debian 18.3-1.pgdg13+1); Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3.0; EF Core 10.0.4.0.

| Storage | Create | Read all | Read by id | Delete | File size | Server relations |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| PostgreSQL EFCore | 439 µs | 8.64 µs | 242 µs | 283 µs | — | 802.9 MiB |
| SQLite Memory | 4.33 µs | 3.7 µs | 5.26 µs | 2.89 µs | — | — |
| SQLite File | 8.51 µs | 3.59 µs | 6.93 µs | 16.9 µs | 783.2 MiB | — |
| SystemDataSQLite Memory | 4.1 µs (1.06× faster) | 3.54 µs (≈ same) | 5.61 µs (1.07× slower) | 2.73 µs (1.06× faster) | — | — |
| SystemDataSQLite File | 8.63 µs (≈ same) | 3.52 µs (≈ same) | 7.26 µs (≈ same) | 21.4 µs (1.27× slower) | 783.2 MiB | — |
| Doublets United Volatile Cached | 19.8 µs (4.58× slower) | 9.67 µs (2.62× slower) | 6.55 µs (1.24× slower) | 17.9 µs (6.21× slower) | — | — |
| Doublets United Volatile Uncached | 196 µs (45.1× slower) | 250 µs (67.6× slower) | 252 µs (47.8× slower) | 17.8 µs (6.17× slower) | — | — |
| Doublets United NonVolatile Cached | 34.8 µs (4.08× slower) | 9.82 µs (2.74× slower) | 6.91 µs (≈ same) | 20.9 µs (1.23× slower) | 640.0 MiB | — |
| Doublets United NonVolatile Uncached | 222 µs (26× slower) | 243 µs (67.7× slower) | 247 µs (35.6× slower) | 20.7 µs (1.22× slower) | 640.0 MiB | — |
| Doublets Split Volatile Cached | 11.3 µs (2.61× slower) | 11.1 µs (2.99× slower) | 7.28 µs (1.38× slower) | 12.3 µs (4.24× slower) | — | — |
| Doublets Split Volatile Uncached | 97.6 µs (22.5× slower) | 335 µs (90.7× slower) | 329 µs (62.5× slower) | 11.3 µs (3.9× slower) | — | — |
| Doublets Split NonVolatile Cached | 27.6 µs (3.25× slower) | 11.3 µs (3.14× slower) | 7.63 µs (1.1× slower) | 13 µs (1.3× faster) | 800.0 MiB | — |
| Doublets Split NonVolatile Uncached | 121 µs (14.2× slower) | 323 µs (90× slower) | 320 µs (46.2× slower) | 12.5 µs (1.35× faster) | 800.0 MiB | — |

![C# doublets vs SQLite, 64 bit, objects](docs/benchmarks/objects-csharp-64.png)
<!-- markdownlint-restore -->
<!--BENCHMARK_RESULTS_END-->

## Original comparison

The original C# object comparison and its historical results.

<!-- markdownlint-disable MD013 MD060 -->

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

<!-- markdownlint-restore -->

### Conclusion

In this particular comparison, Doublets are faster and use less memory on disk,
but this comes with the cost of additional use of RAM (Sqlite uses it less).
