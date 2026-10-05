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

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.37 µs | 99.6 ns | 490 ns | 545 ns | 605 ns | 612 ns | 2.67 µs | 1.72 µs | — |
| SQLite File | 2.01 µs | 104 ns | 498 ns | 566 ns | 622 ns | 625 ns | 10.4 µs | 5.22 µs | 4.4 MiB |
| Doublets United Volatile | 353 ns (3.89× faster) | 1.46 ns (68.4× faster) | 4.37 ns (112× faster) | 142 ns (3.82× faster) | 187 ns (3.24× faster) | 201 ns (3.05× faster) | 940 ns (2.84× faster) | 359 ns (4.79× faster) | — |
| Doublets United NonVolatile | 382 ns (5.27× faster) | 1.44 ns (71.8× faster) | 4.39 ns (114× faster) | 162 ns (3.49× faster) | 208 ns (2.99× faster) | 218 ns (2.88× faster) | 989 ns (10.5× faster) | 373 ns (14× faster) | 32.0 MiB |
| Doublets Split Volatile | 76.9 ns (17.9× faster) | 2.78 ns (35.8× faster) | 4.41 ns (111× faster) | 44.6 ns (12.2× faster) | 22.6 ns (26.7× faster) | 22.9 ns (26.7× faster) | 133 ns (20× faster) | 471 ns (3.65× faster) | — |
| Doublets Split NonVolatile | 80.8 ns (24.9× faster) | 2.76 ns (37.5× faster) | 4.76 ns (105× faster) | 46 ns (12.3× faster) | 23.4 ns (26.5× faster) | 23.9 ns (26.1× faster) | 140 ns (74.4× faster) | 495 ns (10.5× faster) | 40.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.26 µs | 127 ns | 776 ns | 921 ns | 926 ns | 920 ns | 5.71 µs | 3.65 µs | — |
| SQLite File | 5.76 µs | 132 ns | 1.89 µs | 1.91 µs | 1.92 µs | 1.94 µs | 18.6 µs | 10.4 µs | 48.2 MiB |
| Doublets United Volatile | 695 ns (3.26× faster) | 1.98 ns (64.2× faster) | 20.4 ns (38× faster) | 362 ns (2.54× faster) | 442 ns (2.1× faster) | 573 ns (1.61× faster) | 2.24 µs (2.55× faster) | 823 ns (4.44× faster) | — |
| Doublets United NonVolatile | 804 ns (7.17× faster) | 2.28 ns (57.9× faster) | 21.8 ns (87× faster) | 500 ns (3.82× faster) | 682 ns (2.82× faster) | 724 ns (2.67× faster) | 2.82 µs (6.6× faster) | 836 ns (12.5× faster) | 32.0 MiB |
| Doublets Split Volatile | 166 ns (13.6× faster) | 3.93 ns (32.3× faster) | 23 ns (33.8× faster) | 169 ns (5.46× faster) | 116 ns (7.99× faster) | 115 ns (7.99× faster) | 484 ns (11.8× faster) | 1.18 µs (3.1× faster) | — |
| Doublets Split NonVolatile | 158 ns (36.5× faster) | 4.02 ns (32.8× faster) | 24.8 ns (76.5× faster) | 159 ns (12× faster) | 107 ns (17.9× faster) | 117 ns (16.6× faster) | 458 ns (40.6× faster) | 1.3 µs (8× faster) | 40.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 3.99 µs | 121 ns | 1.74 µs | 1.76 µs | 1.79 µs | 1.83 µs | 10.3 µs | 7.02 µs | — |
| SQLite File | 11.3 µs | 126 ns | 2.63 µs | 3.07 µs | 2.97 µs | 2.92 µs | 28.8 µs | 17.7 µs | 501.0 MiB |
| Doublets United Volatile | 2.5 µs (1.59× faster) | 1.92 ns (63.1× faster) | 34.4 ns (50.6× faster) | 1.68 µs (≈ same) | 2 µs (1.12× slower) | 2.01 µs (1.1× slower) | 8.53 µs (1.21× faster) | 3.33 µs (2.11× faster) | — |
| Doublets United NonVolatile | 3.1 µs (3.65× faster) | 1.9 ns (66× faster) | 33 ns (79.7× faster) | 1.69 µs (1.82× faster) | 2.05 µs (1.45× faster) | 2.08 µs (1.41× faster) | 7.94 µs (3.62× faster) | 3.28 µs (5.4× faster) | 320.0 MiB |
| Doublets Split Volatile | 370 ns (10.8× faster) | 3.63 ns (33.5× faster) | 30 ns (58.2× faster) | 251 ns (7.02× faster) | 184 ns (9.72× faster) | 184 ns (9.96× faster) | 743 ns (13.9× faster) | 3.11 µs (2.26× faster) | — |
| Doublets Split NonVolatile | 555 ns (20.4× faster) | 3.61 ns (34.8× faster) | 31.2 ns (84.5× faster) | 262 ns (11.7× faster) | 191 ns (15.6× faster) | 191 ns (15.3× faster) | 781 ns (36.8× faster) | 3.58 µs (4.96× faster) | 400.0 MiB |

![Rust doublets vs SQLite, 32 bit, links](docs/benchmarks/links-rust-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.79 µs | 130 ns | 617 ns | 752 ns | 788 ns | 791 ns | 3.65 µs | 2.27 µs | — |
| SQLite File | 2.17 µs | 136 ns | 651 ns | 790 ns | 822 ns | 828 ns | 11.4 µs | 5.64 µs | 4.4 MiB |
| Doublets United Volatile | 524 ns (3.42× faster) | 3.45 ns (37.8× faster) | 5.25 ns (117× faster) | 267 ns (2.82× faster) | 433 ns (1.82× faster) | 462 ns (1.71× faster) | 1.57 µs (2.33× faster) | 547 ns (4.14× faster) | — |
| Doublets United NonVolatile | 581 ns (3.74× faster) | 3.91 ns (34.7× faster) | 8.41 ns (77.4× faster) | 331 ns (2.39× faster) | 482 ns (1.7× faster) | 552 ns (1.5× faster) | 1.77 µs (6.4× faster) | 557 ns (10.1× faster) | 64.0 MiB |
| Doublets Split Volatile | 141 ns (12.7× faster) | 4.5 ns (29× faster) | 6.6 ns (93.5× faster) | 73.3 ns (10.3× faster) | 41.9 ns (18.8× faster) | 48.8 ns (16.2× faster) | 285 ns (12.8× faster) | 866 ns (2.62× faster) | — |
| Doublets Split NonVolatile | 134 ns (16.2× faster) | 4.73 ns (28.6× faster) | 7.68 ns (84.8× faster) | 75.4 ns (10.5× faster) | 54.3 ns (15.1× faster) | 50 ns (16.6× faster) | 299 ns (37.9× faster) | 885 ns (6.37× faster) | 80.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.42 µs | 66.3 ns | 532 ns | 580 ns | 626 ns | 601 ns | 4.48 µs | 2.92 µs | — |
| SQLite File | 4.08 µs | 66.3 ns | 1.29 µs | 1.21 µs | 1.21 µs | 1.2 µs | 13.2 µs | 7.5 µs | 48.2 MiB |
| Doublets United Volatile | 765 ns (1.86× faster) | 1.77 ns (37.4× faster) | 20.2 ns (26.4× faster) | 425 ns (1.36× faster) | 574 ns (1.09× faster) | 529 ns (≈ same) | 2.61 µs (1.72× faster) | 819 ns (3.56× faster) | — |
| Doublets United NonVolatile | 814 ns (5.02× faster) | 1.76 ns (37.6× faster) | 21.2 ns (60.9× faster) | 512 ns (2.36× faster) | 804 ns (1.5× faster) | 823 ns (1.46× faster) | 2.59 µs (5.08× faster) | 803 ns (9.35× faster) | 64.0 MiB |
| Doublets Split Volatile | 244 ns (5.82× faster) | 2.69 ns (24.7× faster) | 22.2 ns (24× faster) | 196 ns (2.97× faster) | 119 ns (5.26× faster) | 127 ns (4.71× faster) | 525 ns (8.54× faster) | 1.21 µs (2.4× faster) | — |
| Doublets Split NonVolatile | 213 ns (19.2× faster) | 2.54 ns (26.1× faster) | 22 ns (58.5× faster) | 195 ns (6.19× faster) | 118 ns (10.3× faster) | 121 ns (9.91× faster) | 489 ns (27× faster) | 1.32 µs (5.69× faster) | 80.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 3.61 µs | 125 ns | 1.47 µs | 1.6 µs | 1.7 µs | 1.6 µs | 9.01 µs | 5.89 µs | — |
| SQLite File | 9.29 µs | 129 ns | 2.3 µs | 2.61 µs | 2.57 µs | 2.55 µs | 24.6 µs | 14.8 µs | 501.0 MiB |
| Doublets United Volatile | 2.88 µs (1.26× faster) | 3.5 ns (35.6× faster) | 28.1 ns (52.3× faster) | 1.72 µs (1.07× slower) | 1.86 µs (1.1× slower) | 1.89 µs (1.18× slower) | 7.69 µs (1.17× faster) | 3.15 µs (1.87× faster) | — |
| Doublets United NonVolatile | 3.73 µs (2.49× faster) | 3.65 ns (35.4× faster) | 29.2 ns (78.9× faster) | 1.96 µs (1.33× faster) | 2.18 µs (1.18× faster) | 2.19 µs (1.16× faster) | 9.55 µs (2.57× faster) | 4.26 µs (3.47× faster) | 640.0 MiB |
| Doublets Split Volatile | 396 ns (9.12× faster) | 4.46 ns (27.9× faster) | 29 ns (50.8× faster) | 260 ns (6.17× faster) | 187 ns (9.09× faster) | 188 ns (8.5× faster) | 818 ns (11× faster) | 3.22 µs (1.83× faster) | — |
| Doublets Split NonVolatile | 742 ns (12.5× faster) | 4.78 ns (27.1× faster) | 31 ns (74.2× faster) | 267 ns (9.75× faster) | 204 ns (12.6× faster) | 208 ns (12.2× faster) | 880 ns (27.9× faster) | 4.36 µs (3.39× faster) | 800.0 MiB |

![Rust doublets vs SQLite, 64 bit, links](docs/benchmarks/links-rust-64.png)

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.4 µs | 384 ns | 1.16 µs | 1.43 µs | 1.47 µs | 1.47 µs | 4.15 µs | 2.83 µs | — |
| SQLite File | 2.75 µs | 391 ns | 1.17 µs | 1.44 µs | 1.48 µs | 1.5 µs | 11.8 µs | 6.15 µs | 4.4 MiB |
| SystemDataSQLite Memory | 2.58 µs (1.07× slower) | 320 ns (1.2× faster) | 1.42 µs (1.23× slower) | 1.74 µs (1.22× slower) | 1.79 µs (1.22× slower) | 1.74 µs (1.18× slower) | 4.09 µs (≈ same) | 2.98 µs (1.05× slower) | — |
| SystemDataSQLite File | 2.92 µs (1.06× slower) | 325 ns (1.2× faster) | 1.43 µs (1.22× slower) | 1.76 µs (1.21× slower) | 1.8 µs (1.22× slower) | 1.77 µs (1.18× slower) | 11.9 µs (≈ same) | 6.39 µs (≈ same) | 4.4 MiB |
| Doublets United Volatile | 1.01 µs (2.38× faster) | 15.2 ns (25.3× faster) | 57.5 ns (20.2× faster) | 220 ns (6.51× faster) | 255 ns (5.77× faster) | 327 ns (4.5× faster) | 2.05 µs (2.02× faster) | 1.05 µs (2.7× faster) | — |
| Doublets United NonVolatile | 1.08 µs (2.54× faster) | 15.4 ns (25.5× faster) | 58.2 ns (20.1× faster) | 237 ns (6.11× faster) | 268 ns (5.53× faster) | 331 ns (4.52× faster) | 2.13 µs (5.53× faster) | 1.07 µs (5.73× faster) | 32.0 MiB |
| Doublets Split Volatile | 190 ns (12.7× faster) | 14.9 ns (25.7× faster) | 65.4 ns (17.8× faster) | 92.2 ns (15.5× faster) | 77.6 ns (18.9× faster) | 79.7 ns (18.5× faster) | 219 ns (18.9× faster) | 280 ns (10.1× faster) | — |
| Doublets Split NonVolatile | 232 ns (11.9× faster) | 14.9 ns (26.2× faster) | 64.9 ns (18.1× faster) | 93.4 ns (15.5× faster) | 78.9 ns (18.8× faster) | 81.2 ns (18.4× faster) | 225 ns (52.2× faster) | 255 ns (24.1× faster) | 40.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.94 µs | 383 ns | 1.37 µs | 1.67 µs | 1.69 µs | 1.68 µs | 6.62 µs | 4.48 µs | — |
| SQLite File | 6.74 µs | 386 ns | 2.64 µs | 2.75 µs | 2.78 µs | 2.77 µs | 20.7 µs | 11.6 µs | 48.2 MiB |
| SystemDataSQLite Memory | 3.02 µs (≈ same) | 314 ns (1.22× faster) | 1.63 µs (1.19× slower) | 1.97 µs (1.18× slower) | 1.96 µs (1.16× slower) | 1.97 µs (1.17× slower) | 6.34 µs (≈ same) | 4.43 µs (≈ same) | — |
| SystemDataSQLite File | 6.88 µs (≈ same) | 323 ns (1.2× faster) | 2.93 µs (1.11× slower) | 3.08 µs (1.12× slower) | 3.14 µs (1.13× slower) | 3.12 µs (1.13× slower) | 20.3 µs (≈ same) | 11.8 µs (≈ same) | 48.2 MiB |
| Doublets United Volatile | 1.41 µs (2.08× faster) | 15.3 ns (25× faster) | 107 ns (12.9× faster) | 458 ns (3.65× faster) | 497 ns (3.41× faster) | 625 ns (2.69× faster) | 3.34 µs (1.98× faster) | 1.59 µs (2.82× faster) | — |
| Doublets United NonVolatile | 1.39 µs (4.83× faster) | 15.1 ns (25.5× faster) | 94.3 ns (28× faster) | 496 ns (5.54× faster) | 527 ns (5.28× faster) | 538 ns (5.15× faster) | 3.36 µs (6.15× faster) | 1.58 µs (7.37× faster) | 32.0 MiB |
| Doublets Split Volatile | 277 ns (10.6× faster) | 12.4 ns (31× faster) | 123 ns (11.2× faster) | 187 ns (8.94× faster) | 177 ns (9.56× faster) | 195 ns (8.65× faster) | 447 ns (14.8× faster) | 390 ns (11.5× faster) | — |
| Doublets Split NonVolatile | 331 ns (20.3× faster) | 12.4 ns (31.1× faster) | 122 ns (21.6× faster) | 205 ns (13.4× faster) | 186 ns (15× faster) | 188 ns (14.7× faster) | 496 ns (41.7× faster) | 391 ns (29.7× faster) | 40.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.87 µs | 357 ns | 2.36 µs | 2.65 µs | 2.6 µs | 2.64 µs | 13.4 µs | 9.16 µs | — |
| SQLite File | 11.5 µs | 367 ns | 3.31 µs | 3.71 µs | 3.67 µs | 3.61 µs | 28.9 µs | 17 µs | 501.0 MiB |
| SystemDataSQLite Memory | 4.78 µs (≈ same) | 275 ns (1.3× faster) | 2.37 µs (≈ same) | 2.61 µs (≈ same) | 2.69 µs (≈ same) | 2.7 µs (≈ same) | 11.1 µs (1.21× faster) | 7.34 µs (1.25× faster) | — |
| SystemDataSQLite File | 11.1 µs (≈ same) | 268 ns (1.37× faster) | 3.37 µs (≈ same) | 3.94 µs (1.06× slower) | 3.81 µs (≈ same) | 3.77 µs (≈ same) | 27.7 µs (≈ same) | 16.6 µs (≈ same) | 501.0 MiB |
| Doublets United Volatile | 3.54 µs (1.38× faster) | 13.5 ns (26.4× faster) | 191 ns (12.4× faster) | 1.43 µs (1.85× faster) | 1.57 µs (1.66× faster) | 1.74 µs (1.52× faster) | 9.17 µs (1.46× faster) | 4.06 µs (2.26× faster) | — |
| Doublets United NonVolatile | 4.72 µs (2.44× faster) | 12.9 ns (28.5× faster) | 223 ns (14.9× faster) | 1.8 µs (2.06× faster) | 2 µs (1.83× faster) | 2.12 µs (1.7× faster) | 11.4 µs (2.53× faster) | 5.2 µs (3.27× faster) | 320.0 MiB |
| Doublets Split Volatile | 473 ns (10.3× faster) | 11.7 ns (30.4× faster) | 210 ns (11.3× faster) | 513 ns (5.17× faster) | 352 ns (7.39× faster) | 355 ns (7.44× faster) | 874 ns (15.3× faster) | 616 ns (14.9× faster) | — |
| Doublets Split NonVolatile | 1.19 µs (9.66× faster) | 11.5 ns (32× faster) | 234 ns (14.1× faster) | 569 ns (6.52× faster) | 395 ns (9.3× faster) | 404 ns (8.93× faster) | 1.29 µs (22.4× faster) | 855 ns (19.9× faster) | 400.0 MiB |

![C# doublets vs SQLite, 32 bit, links](docs/benchmarks/links-csharp-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.12 µs | 446 ns | 1.05 µs | 1.29 µs | 1.36 µs | 1.37 µs | 3.84 µs | 2.67 µs | — |
| SQLite File | 2.49 µs | 451 ns | 1.06 µs | 1.3 µs | 1.37 µs | 1.38 µs | 8.65 µs | 4.92 µs | 4.4 MiB |
| SystemDataSQLite Memory | 2.12 µs (≈ same) | 297 ns (1.5× faster) | 1.15 µs (1.09× slower) | 1.57 µs (1.22× slower) | 1.38 µs (≈ same) | 1.57 µs (1.15× slower) | 3.8 µs (≈ same) | 2.79 µs (≈ same) | — |
| SystemDataSQLite File | 2.53 µs (≈ same) | 303 ns (1.49× faster) | 1.16 µs (1.09× slower) | 1.59 µs (1.23× slower) | 1.4 µs (≈ same) | 1.61 µs (1.16× slower) | 8.81 µs (≈ same) | 5.04 µs (≈ same) | 4.4 MiB |
| Doublets United Volatile | 1.25 µs (1.69× faster) | 20.8 ns (21.5× faster) | 75.6 ns (13.9× faster) | 270 ns (4.77× faster) | 308 ns (4.41× faster) | 330 ns (4.15× faster) | 2.72 µs (1.41× faster) | 1.32 µs (2.02× faster) | — |
| Doublets United NonVolatile | 1.34 µs (1.86× faster) | 20.8 ns (21.7× faster) | 78.2 ns (13.6× faster) | 306 ns (4.25× faster) | 345 ns (3.98× faster) | 339 ns (4.08× faster) | 2.81 µs (3.08× faster) | 1.34 µs (3.68× faster) | 64.0 MiB |
| Doublets Split Volatile | 249 ns (8.5× faster) | 12.5 ns (35.8× faster) | 92 ns (11.4× faster) | 132 ns (9.74× faster) | 124 ns (11× faster) | 123 ns (11.1× faster) | 256 ns (15× faster) | 330 ns (8.1× faster) | — |
| Doublets Split NonVolatile | 308 ns (8.09× faster) | 12 ns (37.5× faster) | 89.2 ns (11.9× faster) | 135 ns (9.6× faster) | 126 ns (10.9× faster) | 123 ns (11.2× faster) | 260 ns (33.3× faster) | 307 ns (16× faster) | 80.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.98 µs | 388 ns | 1.44 µs | 1.71 µs | 1.77 µs | 1.75 µs | 7.02 µs | 4.68 µs | — |
| SQLite File | 6.8 µs | 389 ns | 2.64 µs | 2.79 µs | 2.81 µs | 2.81 µs | 20.9 µs | 11.6 µs | 48.2 MiB |
| SystemDataSQLite Memory | 3.04 µs (≈ same) | 321 ns (1.21× faster) | 1.67 µs (1.15× slower) | 2.01 µs (1.18× slower) | 2.02 µs (1.14× slower) | 2.02 µs (1.15× slower) | 6.51 µs (1.08× faster) | 4.64 µs (≈ same) | — |
| SystemDataSQLite File | 6.91 µs (≈ same) | 326 ns (1.2× faster) | 2.95 µs (1.12× slower) | 3.13 µs (1.12× slower) | 3.11 µs (1.1× slower) | 3.11 µs (1.11× slower) | 20.7 µs (≈ same) | 12 µs (≈ same) | 48.2 MiB |
| Doublets United Volatile | 1.91 µs (1.56× faster) | 21.8 ns (17.8× faster) | 147 ns (9.84× faster) | 624 ns (2.74× faster) | 708 ns (2.49× faster) | 817 ns (2.15× faster) | 4.92 µs (1.43× faster) | 2.19 µs (2.14× faster) | — |
| Doublets United NonVolatile | 2.23 µs (3.05× faster) | 21.6 ns (18.1× faster) | 176 ns (15× faster) | 707 ns (3.95× faster) | 998 ns (2.82× faster) | 839 ns (3.35× faster) | 5.79 µs (3.61× faster) | 2.55 µs (4.56× faster) | 64.0 MiB |
| Doublets Split Volatile | 365 ns (8.16× faster) | 21.4 ns (18.1× faster) | 215 ns (6.71× faster) | 352 ns (4.85× faster) | 282 ns (6.27× faster) | 290 ns (6.06× faster) | 685 ns (10.3× faster) | 606 ns (7.73× faster) | — |
| Doublets Split NonVolatile | 473 ns (14.4× faster) | 21.4 ns (18.2× faster) | 246 ns (10.7× faster) | 369 ns (7.57× faster) | 306 ns (9.19× faster) | 323 ns (8.72× faster) | 718 ns (29.1× faster) | 578 ns (20.1× faster) | 80.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.34 µs | 395 ns | 2.08 µs | 2.4 µs | 2.39 µs | 2.37 µs | 10 µs | 6.7 µs | — |
| SQLite File | 10.4 µs | 387 ns | 3.05 µs | 3.52 µs | 3.44 µs | 3.43 µs | 26.1 µs | 15.7 µs | 501.0 MiB |
| SystemDataSQLite Memory | 4.12 µs (1.05× faster) | 314 ns (1.26× faster) | 2.21 µs (1.06× slower) | 2.49 µs (≈ same) | 2.52 µs (1.06× slower) | 2.53 µs (1.07× slower) | 9.34 µs (1.07× faster) | 6.6 µs (≈ same) | — |
| SystemDataSQLite File | 10.5 µs (≈ same) | 318 ns (1.22× faster) | 3.17 µs (≈ same) | 3.78 µs (1.07× slower) | 3.81 µs (1.11× slower) | 3.81 µs (1.11× slower) | 26.2 µs (≈ same) | 15.9 µs (≈ same) | 501.0 MiB |
| Doublets United Volatile | 3.25 µs (1.34× faster) | 20.9 ns (18.9× faster) | 171 ns (12.2× faster) | 1.34 µs (1.79× faster) | 1.39 µs (1.72× faster) | 1.69 µs (1.4× faster) | 8.72 µs (1.15× faster) | 3.94 µs (1.7× faster) | — |
| Doublets United NonVolatile | 4.63 µs (2.26× faster) | 21 ns (18.4× faster) | 187 ns (16.3× faster) | 1.45 µs (2.42× faster) | 1.71 µs (2.01× faster) | 1.59 µs (2.17× faster) | 9.73 µs (2.68× faster) | 4.77 µs (3.28× faster) | 640.0 MiB |
| Doublets Split Volatile | 447 ns (9.71× faster) | 20.4 ns (19.3× faster) | 296 ns (7.02× faster) | 439 ns (5.46× faster) | 315 ns (7.58× faster) | 332 ns (7.13× faster) | 888 ns (11.3× faster) | 711 ns (9.42× faster) | — |
| Doublets Split NonVolatile | 1.27 µs (8.21× faster) | 20.5 ns (18.9× faster) | 337 ns (9.05× faster) | 493 ns (7.14× faster) | 346 ns (9.94× faster) | 362 ns (9.49× faster) | 1.21 µs (21.6× faster) | 868 ns (18× faster) | 800.0 MiB |

![C# doublets vs SQLite, 64 bit, links](docs/benchmarks/links-csharp-64.png)

## Doublets vs SQLite as storage for objects

### Rust doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.28 µs | 589 ns | 1.57 µs | 2.05 µs | — |
| SQLite File | 2 µs | 645 ns | 2.6 µs | 7.38 µs | 78.3 MiB |
| Doublets United Volatile Cached | 5.33 µs (4.16× slower) | 1.28 µs (2.17× slower) | 2.24 µs (1.43× slower) | 7.15 µs (3.49× slower) | — |
| Doublets United Volatile Uncached | 90.8 µs (70.8× slower) | 9.51 µs (16.1× slower) | 10.8 µs (6.89× slower) | 6.76 µs (3.3× slower) | — |
| Doublets United NonVolatile Cached | 5.52 µs (2.76× slower) | 1.26 µs (1.95× slower) | 2.2 µs (1.18× faster) | 6.41 µs (1.15× faster) | 32.0 MiB |
| Doublets United NonVolatile Uncached | 92.1 µs (46× slower) | 9.47 µs (14.7× slower) | 10.9 µs (4.18× slower) | 6.73 µs (1.1× faster) | 32.0 MiB |
| Doublets Split Volatile Cached | 3.47 µs (2.71× slower) | 777 ns (1.32× slower) | 647 ns (2.42× faster) | 3.37 µs (1.65× slower) | — |
| Doublets Split Volatile Uncached | 44.4 µs (34.7× slower) | 14.3 µs (24.2× slower) | 14.5 µs (9.28× slower) | 3.52 µs (1.71× slower) | — |
| Doublets Split NonVolatile Cached | 3.47 µs (1.73× slower) | 763 ns (1.18× slower) | 634 ns (4.1× faster) | 3.72 µs (1.99× faster) | 40.0 MiB |
| Doublets Split NonVolatile Uncached | 45.1 µs (22.5× slower) | 14.4 µs (22.3× slower) | 14.6 µs (5.61× slower) | 3.97 µs (1.86× faster) | 40.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 898 ns | 503 ns | 1.73 µs | 2.3 µs | — |
| SQLite File | 5.15 µs | 412 ns | 2.59 µs | 10.6 µs | 783.2 MiB |
| Doublets United Volatile Cached | 5.22 µs (5.81× slower) | 1.29 µs (2.57× slower) | 2.97 µs (1.71× slower) | 10.7 µs (4.66× slower) | — |
| Doublets United Volatile Uncached | 59.9 µs (66.7× slower) | 6.13 µs (12.2× slower) | 8.16 µs (4.71× slower) | 9.84 µs (4.28× slower) | — |
| Doublets United NonVolatile Cached | 6.4 µs (1.24× slower) | 1.28 µs (3.1× slower) | 2.89 µs (1.11× slower) | 10.7 µs (≈ same) | 320.0 MiB |
| Doublets United NonVolatile Uncached | 65.2 µs (12.6× slower) | 6.26 µs (15.2× slower) | 8.38 µs (3.23× slower) | 12 µs (1.13× slower) | 320.0 MiB |
| Doublets Split Volatile Cached | 4.09 µs (4.55× slower) | 650 ns (1.29× slower) | 921 ns (1.88× faster) | 7.08 µs (3.08× slower) | — |
| Doublets Split Volatile Uncached | 26.7 µs (29.7× slower) | 8.07 µs (16× slower) | 8.58 µs (4.95× slower) | 6.07 µs (2.64× slower) | — |
| Doublets Split NonVolatile Cached | 4.93 µs (≈ same) | 599 ns (1.46× slower) | 870 ns (2.98× faster) | 6.39 µs (1.66× faster) | 400.0 MiB |
| Doublets Split NonVolatile Uncached | 26.8 µs (5.2× slower) | 8.05 µs (19.6× slower) | 8.34 µs (3.21× slower) | 6.51 µs (1.63× faster) | 400.0 MiB |

![Rust doublets vs SQLite, 32 bit, objects](docs/benchmarks/objects-rust-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.03 µs | 545 ns | 1.41 µs | 1.85 µs | — |
| SQLite File | 5.88 µs | 537 ns | 2.46 µs | 14.1 µs | 78.3 MiB |
| Doublets United Volatile Cached | 4.73 µs (4.61× slower) | 1.48 µs (2.71× slower) | 2.73 µs (1.93× slower) | 8.41 µs (4.56× slower) | — |
| Doublets United Volatile Uncached | 70 µs (68.2× slower) | 8.08 µs (14.8× slower) | 9.72 µs (6.9× slower) | 8.68 µs (4.7× slower) | — |
| Doublets United NonVolatile Cached | 5.36 µs (1.1× faster) | 1.5 µs (2.8× slower) | 2.94 µs (1.2× slower) | 8.13 µs (1.74× faster) | 64.0 MiB |
| Doublets United NonVolatile Uncached | 72.6 µs (12.3× slower) | 8.08 µs (15.1× slower) | 9.92 µs (4.04× slower) | 8.18 µs (1.72× faster) | 64.0 MiB |
| Doublets Split Volatile Cached | 3.16 µs (3.08× slower) | 718 ns (1.32× slower) | 875 ns (1.61× faster) | 4.39 µs (2.38× slower) | — |
| Doublets Split Volatile Uncached | 30.2 µs (29.4× slower) | 10.8 µs (19.8× slower) | 11.3 µs (8.04× slower) | 4.45 µs (2.41× slower) | — |
| Doublets Split NonVolatile Cached | 3.71 µs (1.59× faster) | 713 ns (1.33× slower) | 967 ns (2.54× faster) | 4.54 µs (3.11× faster) | 80.0 MiB |
| Doublets Split NonVolatile Uncached | 30.1 µs (5.12× slower) | 11 µs (20.4× slower) | 11.6 µs (4.72× slower) | 4.58 µs (3.08× faster) | 80.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 873 ns | 489 ns | 1.56 µs | 2.22 µs | — |
| SQLite File | 5.35 µs | 406 ns | 2.44 µs | 11.1 µs | 783.2 MiB |
| Doublets United Volatile Cached | 5.55 µs (6.36× slower) | 1.54 µs (3.16× slower) | 3.17 µs (2.03× slower) | 11.6 µs (5.2× slower) | — |
| Doublets United Volatile Uncached | 60.2 µs (68.9× slower) | 6.51 µs (13.3× slower) | 8.89 µs (5.7× slower) | 10.7 µs (4.81× slower) | — |
| Doublets United NonVolatile Cached | 7.01 µs (1.31× slower) | 1.42 µs (3.49× slower) | 3.09 µs (1.27× slower) | 10.6 µs (≈ same) | 640.0 MiB |
| Doublets United NonVolatile Uncached | 64.1 µs (12× slower) | 6.61 µs (16.3× slower) | 8.47 µs (3.47× slower) | 11.4 µs (≈ same) | 640.0 MiB |
| Doublets Split Volatile Cached | 4.16 µs (4.76× slower) | 638 ns (1.31× slower) | 955 ns (1.63× faster) | 7.57 µs (3.41× slower) | — |
| Doublets Split Volatile Uncached | 29.1 µs (33.3× slower) | 8.17 µs (16.7× slower) | 8.73 µs (5.59× slower) | 7.2 µs (3.24× slower) | — |
| Doublets Split NonVolatile Cached | 6.1 µs (1.14× slower) | 600 ns (1.48× slower) | 1 µs (2.44× faster) | 7.22 µs (1.54× faster) | 800.0 MiB |
| Doublets Split NonVolatile Uncached | 30.2 µs (5.65× slower) | 8.02 µs (19.8× slower) | 8.56 µs (3.51× slower) | 7.38 µs (1.5× faster) | 800.0 MiB |

![Rust doublets vs SQLite, 64 bit, objects](docs/benchmarks/objects-rust-64.png)

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.52 µs | 4.69 µs | 5.84 µs | 1.92 µs | — |
| SQLite File | 8.62 µs | 4.81 µs | 6.37 µs | 9.7 µs | 78.3 MiB |
| SystemDataSQLite Memory | 5.38 µs (≈ same) | 4.63 µs (≈ same) | 6.07 µs (≈ same) | 2.04 µs (1.06× slower) | — |
| SystemDataSQLite File | 8.6 µs (≈ same) | 4.65 µs (≈ same) | 6.57 µs (≈ same) | 8.77 µs (≈ same) | 78.3 MiB |
| Doublets United Volatile Cached | 18.3 µs (3.31× slower) | 10.8 µs (2.3× slower) | 5.54 µs (1.05× faster) | 8.7 µs (4.52× slower) | — |
| Doublets United Volatile Uncached | 156 µs (28.2× slower) | 262 µs (55.7× slower) | 265 µs (45.3× slower) | 8.39 µs (4.36× slower) | — |
| Doublets United NonVolatile Cached | 18.1 µs (2.1× slower) | 10.4 µs (2.17× slower) | 5.52 µs (1.15× faster) | 8.87 µs (≈ same) | 32.0 MiB |
| Doublets United NonVolatile Uncached | 158 µs (18.4× slower) | 261 µs (54.4× slower) | 262 µs (41.2× slower) | 9.08 µs (≈ same) | 32.0 MiB |
| Doublets Split Volatile Cached | 10.3 µs (1.86× slower) | 12.4 µs (2.64× slower) | 5.07 µs (1.15× faster) | 4.75 µs (2.47× slower) | — |
| Doublets Split Volatile Uncached | 111 µs (20.1× slower) | 387 µs (82.5× slower) | 390 µs (66.8× slower) | 4.98 µs (2.59× slower) | — |
| Doublets Split NonVolatile Cached | 11.3 µs (1.31× slower) | 12.8 µs (2.67× slower) | 5.35 µs (1.19× faster) | 5.23 µs (1.85× faster) | 40.0 MiB |
| Doublets Split NonVolatile Uncached | 113 µs (13.1× slower) | 387 µs (80.5× slower) | 391 µs (61.3× slower) | 5.48 µs (1.77× faster) | 40.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.93 µs | 4.27 µs | 6.02 µs | 3.12 µs | — |
| SQLite File | 9.21 µs | 4.32 µs | 6.84 µs | 13.4 µs | 783.2 MiB |
| SystemDataSQLite Memory | 4.84 µs (≈ same) | 4.1 µs (≈ same) | 6.35 µs (1.05× slower) | 3.17 µs (≈ same) | — |
| SystemDataSQLite File | 9.27 µs (≈ same) | 4.09 µs (1.06× faster) | 7.11 µs (≈ same) | 12.2 µs (1.1× faster) | 783.2 MiB |
| Doublets United Volatile Cached | 20.4 µs (4.13× slower) | 11.6 µs (2.71× slower) | 6.91 µs (1.15× slower) | 15.7 µs (5.03× slower) | — |
| Doublets United Volatile Uncached | 193 µs (39.2× slower) | 283 µs (66.3× slower) | 287 µs (47.7× slower) | 15 µs (4.79× slower) | — |
| Doublets United NonVolatile Cached | 28.1 µs (3.05× slower) | 11.6 µs (2.69× slower) | 6.81 µs (≈ same) | 16.9 µs (1.26× slower) | 320.0 MiB |
| Doublets United NonVolatile Uncached | 228 µs (24.7× slower) | 289 µs (66.8× slower) | 288 µs (42× slower) | 15.5 µs (1.15× slower) | 320.0 MiB |
| Doublets Split Volatile Cached | 12.6 µs (2.54× slower) | 13.1 µs (3.08× slower) | 7.15 µs (1.19× slower) | 9.07 µs (2.91× slower) | — |
| Doublets Split Volatile Uncached | 112 µs (22.7× slower) | 388 µs (90.9× slower) | 392 µs (65.1× slower) | 8.85 µs (2.83× slower) | — |
| Doublets Split NonVolatile Cached | 20 µs (2.17× slower) | 13.3 µs (3.07× slower) | 7.53 µs (1.1× slower) | 10.1 µs (1.33× faster) | 400.0 MiB |
| Doublets Split NonVolatile Uncached | 130 µs (14.2× slower) | 385 µs (89.1× slower) | 396 µs (57.9× slower) | 10.3 µs (1.3× faster) | 400.0 MiB |

![C# doublets vs SQLite, 32 bit, objects](docs/benchmarks/objects-csharp-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.69 µs | 4.55 µs | 6.02 µs | 2.39 µs | — |
| SQLite File | 6.59 µs | 4.73 µs | 7.12 µs | 8.01 µs | 78.3 MiB |
| SystemDataSQLite Memory | 5.49 µs (≈ same) | 4.47 µs (≈ same) | 6.22 µs (≈ same) | 2.15 µs (1.11× faster) | — |
| SystemDataSQLite File | 6.63 µs (≈ same) | 4.61 µs (≈ same) | 7.38 µs (≈ same) | 8.01 µs (≈ same) | 78.3 MiB |
| Doublets United Volatile Cached | 21.8 µs (3.83× slower) | 12.7 µs (2.8× slower) | 6.51 µs (1.08× slower) | 14.9 µs (6.24× slower) | — |
| Doublets United Volatile Uncached | 194 µs (34.2× slower) | 352 µs (77.5× slower) | 358 µs (59.5× slower) | 14.4 µs (6.02× slower) | — |
| Doublets United NonVolatile Cached | 23.3 µs (3.54× slower) | 12.7 µs (2.68× slower) | 6.74 µs (1.06× faster) | 15.4 µs (1.92× slower) | 64.0 MiB |
| Doublets United NonVolatile Uncached | 202 µs (30.7× slower) | 367 µs (77.7× slower) | 372 µs (52.3× slower) | 15.9 µs (1.98× slower) | 64.0 MiB |
| Doublets Split Volatile Cached | 12.8 µs (2.24× slower) | 13.5 µs (2.98× slower) | 6.74 µs (1.12× slower) | 10.2 µs (4.29× slower) | — |
| Doublets Split Volatile Uncached | 120 µs (21.2× slower) | 420 µs (92.3× slower) | 423 µs (70.3× slower) | 9.88 µs (4.14× slower) | — |
| Doublets Split NonVolatile Cached | 13.8 µs (2.09× slower) | 13.5 µs (2.87× slower) | 6.32 µs (1.13× faster) | 9.91 µs (1.24× slower) | 80.0 MiB |
| Doublets Split NonVolatile Uncached | 124 µs (18.8× slower) | 414 µs (87.6× slower) | 419 µs (58.9× slower) | 10.5 µs (1.31× slower) | 80.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37314225380) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.38 µs | 4.15 µs | 6.33 µs | 3.26 µs | — |
| SQLite File | 7.48 µs | 4.25 µs | 7.89 µs | 11.5 µs | 783.2 MiB |
| SystemDataSQLite Memory | 5.06 µs (1.06× faster) | 4 µs (≈ same) | 6.65 µs (1.05× slower) | 3.12 µs (≈ same) | — |
| SystemDataSQLite File | 7.6 µs (≈ same) | 4.12 µs (≈ same) | 8.29 µs (1.05× slower) | 12.6 µs (1.09× slower) | 783.2 MiB |
| Doublets United Volatile Cached | 26 µs (4.83× slower) | 12.8 µs (3.09× slower) | 7.63 µs (1.2× slower) | 22.1 µs (6.79× slower) | — |
| Doublets United Volatile Uncached | 234 µs (43.4× slower) | 325 µs (78.3× slower) | 328 µs (51.8× slower) | 18.3 µs (5.61× slower) | — |
| Doublets United NonVolatile Cached | 34 µs (4.55× slower) | 12.9 µs (3.03× slower) | 7.74 µs (≈ same) | 20.4 µs (1.77× slower) | 640.0 MiB |
| Doublets United NonVolatile Uncached | 254 µs (33.9× slower) | 333 µs (78.5× slower) | 333 µs (42.2× slower) | 19.7 µs (1.7× slower) | 640.0 MiB |
| Doublets Split Volatile Cached | 13.1 µs (2.44× slower) | 13 µs (3.13× slower) | 7.48 µs (1.18× slower) | 12.3 µs (3.78× slower) | — |
| Doublets Split Volatile Uncached | 113 µs (21× slower) | 404 µs (97.5× slower) | 396 µs (62.6× slower) | 12.1 µs (3.72× slower) | — |
| Doublets Split NonVolatile Cached | 22.4 µs (2.99× slower) | 13.1 µs (3.07× slower) | 7.94 µs (≈ same) | 14.6 µs (1.27× slower) | 800.0 MiB |
| Doublets Split NonVolatile Uncached | 138 µs (18.4× slower) | 394 µs (92.8× slower) | 397 µs (50.3× slower) | 14.9 µs (1.29× slower) | 800.0 MiB |

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
