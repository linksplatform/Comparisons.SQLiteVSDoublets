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

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.76 µs | 130 ns | 633 ns | 766 ns | 802 ns | 805 ns | 3.27 µs | 2.12 µs | — |
| SQLite File | 2.11 µs | 134 ns | 662 ns | 814 ns | 836 ns | 842 ns | 10.8 µs | 5.33 µs | 4.4 MiB |
| Doublets United Volatile | 430 ns (4.09× faster) | 1.77 ns (73.3× faster) | 4.26 ns (149× faster) | 174 ns (4.41× faster) | 230 ns (3.49× faster) | 243 ns (3.31× faster) | 1.12 µs (2.93× faster) | 437 ns (4.86× faster) | — |
| Doublets United NonVolatile | 446 ns (4.72× faster) | 1.89 ns (71.1× faster) | 4.25 ns (156× faster) | 185 ns (4.41× faster) | 235 ns (3.55× faster) | 244 ns (3.44× faster) | 1.12 µs (9.69× faster) | 438 ns (12.2× faster) | 32.0 MiB |
| Doublets Split Volatile | 97.7 ns (18× faster) | 3.77 ns (34.4× faster) | 4.05 ns (156× faster) | 52.1 ns (14.7× faster) | 27.2 ns (29.4× faster) | 28.4 ns (28.4× faster) | 156 ns (21× faster) | 560 ns (3.79× faster) | — |
| Doublets Split NonVolatile | 98.6 ns (21.4× faster) | 3.75 ns (35.8× faster) | 4.04 ns (164× faster) | 55 ns (14.8× faster) | 28.7 ns (29.1× faster) | 29.8 ns (28.2× faster) | 165 ns (65.5× faster) | 594 ns (8.98× faster) | 40.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.5 µs | 125 ns | 926 ns | 988 ns | 1.05 µs | 1.05 µs | 6.71 µs | 4.66 µs | — |
| SQLite File | 6.87 µs | 129 ns | 2.23 µs | 2.15 µs | 2.21 µs | 2.2 µs | 22.4 µs | 12.8 µs | 48.2 MiB |
| Doublets United Volatile | 1.1 µs (2.28× faster) | 1.89 ns (66.4× faster) | 24 ns (38.5× faster) | 651 ns (1.52× faster) | 846 ns (1.24× faster) | 955 ns (1.1× faster) | 3.54 µs (1.9× faster) | 1.11 µs (4.18× faster) | — |
| Doublets United NonVolatile | 1.15 µs (6× faster) | 1.89 ns (68.4× faster) | 23.4 ns (95.3× faster) | 589 ns (3.65× faster) | 820 ns (2.69× faster) | 990 ns (2.23× faster) | 3.45 µs (6.47× faster) | 1.32 µs (9.64× faster) | 32.0 MiB |
| Doublets Split Volatile | 174 ns (14.4× faster) | 3.63 ns (34.6× faster) | 20.9 ns (44.4× faster) | 151 ns (6.54× faster) | 117 ns (8.95× faster) | 105 ns (9.99× faster) | 381 ns (17.6× faster) | 1.28 µs (3.63× faster) | — |
| Doublets Split NonVolatile | 208 ns (33.1× faster) | 3.63 ns (35.6× faster) | 22.4 ns (99.3× faster) | 179 ns (12× faster) | 134 ns (16.5× faster) | 124 ns (17.8× faster) | 466 ns (47.9× faster) | 1.57 µs (8.1× faster) | 40.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.93 µs | 110 ns | 1.11 µs | 1.24 µs | 1.27 µs | 1.26 µs | 8.48 µs | 5.58 µs | — |
| SQLite File | 6.82 µs | 113 ns | 1.75 µs | 1.99 µs | 1.95 µs | 1.95 µs | 18.2 µs | 10.8 µs | 501.0 MiB |
| Doublets United Volatile | 1.57 µs (1.87× faster) | 4.18 ns (26.2× faster) | 30.4 ns (36.6× faster) | 1.21 µs (≈ same) | 1.41 µs (1.11× slower) | 1.5 µs (1.19× slower) | 5.78 µs (1.47× faster) | 2.27 µs (2.46× faster) | — |
| Doublets United NonVolatile | 2.04 µs (3.34× faster) | 4.17 ns (27.1× faster) | 25.5 ns (68.8× faster) | 1.14 µs (1.75× faster) | 1.38 µs (1.42× faster) | 1.42 µs (1.37× faster) | 7.11 µs (2.56× faster) | 2.83 µs (3.8× faster) | 320.0 MiB |
| Doublets Split Volatile | 276 ns (10.6× faster) | 4.78 ns (22.9× faster) | 26.6 ns (41.8× faster) | 207 ns (5.97× faster) | 136 ns (9.29× faster) | 128 ns (9.8× faster) | 585 ns (14.5× faster) | 2.42 µs (2.3× faster) | — |
| Doublets Split NonVolatile | 396 ns (17.2× faster) | 4.58 ns (24.7× faster) | 27.8 ns (63× faster) | 200 ns (9.95× faster) | 132 ns (14.8× faster) | 132 ns (14.8× faster) | 567 ns (32.1× faster) | 2.8 µs (3.85× faster) | 400.0 MiB |

![Rust doublets vs SQLite, 32 bit, links](docs/benchmarks/links-rust-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.07 µs | 68.9 ns | 382 ns | 436 ns | 469 ns | 480 ns | 2.1 µs | 1.32 µs | — |
| SQLite File | 1.45 µs | 74 ns | 389 ns | 448 ns | 478 ns | 481 ns | 8 µs | 3.8 µs | 4.4 MiB |
| Doublets United Volatile | 373 ns (2.86× faster) | 1.43 ns (48.2× faster) | 2.28 ns (168× faster) | 133 ns (3.27× faster) | 185 ns (2.53× faster) | 212 ns (2.26× faster) | 878 ns (2.39× faster) | 330 ns (3.99× faster) | — |
| Doublets United NonVolatile | 340 ns (4.26× faster) | 1.24 ns (59.8× faster) | 2.18 ns (178× faster) | 147 ns (3.05× faster) | 179 ns (2.67× faster) | 188 ns (2.56× faster) | 878 ns (9.11× faster) | 325 ns (11.7× faster) | 64.0 MiB |
| Doublets Split Volatile | 63.5 ns (16.8× faster) | 2.49 ns (27.7× faster) | 2.37 ns (161× faster) | 38.3 ns (11.4× faster) | 18.3 ns (25.6× faster) | 18.5 ns (26× faster) | 116 ns (18× faster) | 408 ns (3.23× faster) | — |
| Doublets Split NonVolatile | 66.4 ns (21.8× faster) | 2.43 ns (30.4× faster) | 2.1 ns (186× faster) | 39.7 ns (11.3× faster) | 19 ns (25.2× faster) | 19.2 ns (25.1× faster) | 124 ns (64.7× faster) | 427 ns (8.91× faster) | 80.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.32 µs | 126 ns | 814 ns | 937 ns | 947 ns | 981 ns | 5.66 µs | 3.83 µs | — |
| SQLite File | 5.81 µs | 131 ns | 1.94 µs | 1.89 µs | 1.92 µs | 1.92 µs | 18.7 µs | 10.4 µs | 48.2 MiB |
| Doublets United Volatile | 936 ns (2.48× faster) | 3.05 ns (41.4× faster) | 20.5 ns (39.6× faster) | 513 ns (1.83× faster) | 652 ns (1.45× faster) | 758 ns (1.29× faster) | 3.01 µs (1.88× faster) | 1.03 µs (3.74× faster) | — |
| Doublets United NonVolatile | 1.08 µs (5.39× faster) | 3.37 ns (38.8× faster) | 22.7 ns (85.5× faster) | 691 ns (2.73× faster) | 855 ns (2.25× faster) | 868 ns (2.22× faster) | 3.52 µs (5.3× faster) | 1.24 µs (8.42× faster) | 64.0 MiB |
| Doublets Split Volatile | 216 ns (10.7× faster) | 4 ns (31.5× faster) | 21.1 ns (38.6× faster) | 186 ns (5.05× faster) | 138 ns (6.85× faster) | 138 ns (7.08× faster) | 531 ns (10.7× faster) | 1.42 µs (2.7× faster) | — |
| Doublets Split NonVolatile | 239 ns (24.3× faster) | 4.4 ns (29.7× faster) | 24.1 ns (80.5× faster) | 193 ns (9.8× faster) | 154 ns (12.5× faster) | 154 ns (12.5× faster) | 610 ns (30.6× faster) | 1.65 µs (6.33× faster) | 80.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.08 µs | 125 ns | 1.67 µs | 1.77 µs | 1.73 µs | 1.79 µs | 10.3 µs | 6.71 µs | — |
| SQLite File | 9.64 µs | 129 ns | 2.4 µs | 2.71 µs | 2.67 µs | 2.67 µs | 25.2 µs | 15 µs | 501.0 MiB |
| Doublets United Volatile | 2.76 µs (1.48× faster) | 3.61 ns (34.7× faster) | 28.6 ns (58.5× faster) | 1.52 µs (1.17× faster) | 1.77 µs (≈ same) | 1.88 µs (1.05× slower) | 7.75 µs (1.33× faster) | 3.17 µs (2.12× faster) | — |
| Doublets United NonVolatile | 3.43 µs (2.81× faster) | 3.86 ns (33.4× faster) | 29.3 ns (82.1× faster) | 1.78 µs (1.52× faster) | 2 µs (1.33× faster) | 2.25 µs (1.18× faster) | 9.61 µs (2.62× faster) | 4.42 µs (3.4× faster) | 640.0 MiB |
| Doublets Split Volatile | 404 ns (10.1× faster) | 5.02 ns (25× faster) | 29.8 ns (56× faster) | 266 ns (6.66× faster) | 192 ns (8.98× faster) | 191 ns (9.37× faster) | 836 ns (12.4× faster) | 3.24 µs (2.07× faster) | — |
| Doublets Split NonVolatile | 752 ns (12.8× faster) | 4.89 ns (26.3× faster) | 33.1 ns (72.6× faster) | 270 ns (10× faster) | 204 ns (13.1× faster) | 207 ns (12.9× faster) | 911 ns (27.6× faster) | 4.66 µs (3.22× faster) | 800.0 MiB |

![Rust doublets vs SQLite, 64 bit, links](docs/benchmarks/links-rust-64.png)

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.87 µs | 315 ns | 925 ns | 1.12 µs | 1.18 µs | 1.17 µs | 3.31 µs | 2.26 µs | — |
| SQLite File | 2.55 µs | 319 ns | 923 ns | 1.12 µs | 1.18 µs | 1.17 µs | 11.5 µs | 6.65 µs | 4.4 MiB |
| SystemDataSQLite Memory | 2.03 µs (1.08× slower) | 244 ns (1.29× faster) | 1.11 µs (1.2× slower) | 1.36 µs (1.22× slower) | 1.39 µs (1.18× slower) | 1.35 µs (1.16× slower) | 3.35 µs (≈ same) | 2.44 µs (1.08× slower) | — |
| SystemDataSQLite File | 2.76 µs (≈ same) | 248 ns (1.29× faster) | 1.12 µs (1.21× slower) | 1.38 µs (1.23× slower) | 1.39 µs (1.18× slower) | 1.37 µs (1.17× slower) | 11.3 µs (≈ same) | 6.17 µs (≈ same) | 4.4 MiB |
| Doublets United Volatile | 784 ns (2.39× faster) | 11.3 ns (27.8× faster) | 44.9 ns (20.6× faster) | 198 ns (5.66× faster) | 208 ns (5.67× faster) | 218 ns (5.37× faster) | 1.66 µs (2× faster) | 844 ns (2.67× faster) | — |
| Doublets United NonVolatile | 843 ns (3.03× faster) | 11 ns (28.9× faster) | 43.8 ns (21.1× faster) | 215 ns (5.21× faster) | 228 ns (5.17× faster) | 230 ns (5.08× faster) | 1.73 µs (6.63× faster) | 869 ns (7.66× faster) | 32.0 MiB |
| Doublets Split Volatile | 153 ns (12.3× faster) | 10.5 ns (30.1× faster) | 50.7 ns (18.2× faster) | 81.2 ns (13.8× faster) | 65.7 ns (17.9× faster) | 64.5 ns (18.1× faster) | 171 ns (19.3× faster) | 210 ns (10.7× faster) | — |
| Doublets Split NonVolatile | 186 ns (13.7× faster) | 10.3 ns (31.1× faster) | 51 ns (18.1× faster) | 84.3 ns (13.3× faster) | 66.8 ns (17.7× faster) | 66.6 ns (17.5× faster) | 179 ns (64.1× faster) | 202 ns (33× faster) | 40.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.9 µs | 394 ns | 1.51 µs | 1.72 µs | 1.78 µs | 1.74 µs | 6.67 µs | 4.4 µs | — |
| SQLite File | 6.69 µs | 398 ns | 2.67 µs | 2.77 µs | 2.79 µs | 2.79 µs | 20.3 µs | 11.6 µs | 48.2 MiB |
| SystemDataSQLite Memory | 2.99 µs (≈ same) | 315 ns (1.25× faster) | 1.62 µs (1.07× slower) | 1.96 µs (1.14× slower) | 1.96 µs (1.1× slower) | 1.97 µs (1.13× slower) | 6.25 µs (1.07× faster) | 4.25 µs (≈ same) | — |
| SystemDataSQLite File | 6.87 µs (≈ same) | 318 ns (1.25× faster) | 2.95 µs (1.1× slower) | 3.1 µs (1.12× slower) | 3.11 µs (1.12× slower) | 3.12 µs (1.12× slower) | 20.5 µs (≈ same) | 11.8 µs (≈ same) | 48.2 MiB |
| Doublets United Volatile | 1.35 µs (2.14× faster) | 15 ns (26.3× faster) | 92.2 ns (16.4× faster) | 456 ns (3.78× faster) | 599 ns (2.97× faster) | 545 ns (3.19× faster) | 3.46 µs (1.93× faster) | 1.52 µs (2.89× faster) | — |
| Doublets United NonVolatile | 1.6 µs (4.19× faster) | 15.1 ns (26.3× faster) | 96 ns (27.8× faster) | 553 ns (5× faster) | 693 ns (4.03× faster) | 572 ns (4.88× faster) | 3.91 µs (5.2× faster) | 1.69 µs (6.86× faster) | 32.0 MiB |
| Doublets Split Volatile | 266 ns (10.9× faster) | 14.3 ns (27.5× faster) | 128 ns (11.8× faster) | 204 ns (8.45× faster) | 186 ns (9.59× faster) | 195 ns (8.91× faster) | 439 ns (15.2× faster) | 386 ns (11.4× faster) | — |
| Doublets Split NonVolatile | 320 ns (20.9× faster) | 14.2 ns (28.1× faster) | 120 ns (22.2× faster) | 211 ns (13.1× faster) | 185 ns (15× faster) | 186 ns (15× faster) | 450 ns (45.1× faster) | 375 ns (30.8× faster) | 40.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 3.8 µs | 309 ns | 1.79 µs | 1.93 µs | 2 µs | 1.94 µs | 8.97 µs | 6.05 µs | — |
| SQLite File | 7.02 µs | 299 ns | 1.95 µs | 2.25 µs | 2.24 µs | 2.25 µs | 17.5 µs | 10.7 µs | 501.0 MiB |
| SystemDataSQLite Memory | 3.28 µs (1.16× faster) | 181 ns (1.71× faster) | 1.76 µs (≈ same) | 1.86 µs (≈ same) | 1.9 µs (1.05× faster) | 1.89 µs (≈ same) | 8.68 µs (≈ same) | 6.39 µs (1.05× slower) | — |
| SystemDataSQLite File | 7.16 µs (≈ same) | 185 ns (1.62× faster) | 2.11 µs (1.08× slower) | 2.49 µs (1.11× slower) | 2.45 µs (1.09× slower) | 2.4 µs (1.06× slower) | 17.6 µs (≈ same) | 10.9 µs (≈ same) | 501.0 MiB |
| Doublets United Volatile | 2.39 µs (1.59× faster) | 7.23 ns (42.7× faster) | 151 ns (11.9× faster) | 1.09 µs (1.77× faster) | 1.28 µs (1.56× faster) | 1.4 µs (1.39× faster) | 6.58 µs (1.36× faster) | 2.81 µs (2.15× faster) | — |
| Doublets United NonVolatile | 3.3 µs (2.13× faster) | 7.5 ns (39.9× faster) | 163 ns (12× faster) | 1.13 µs (2× faster) | 1.33 µs (1.69× faster) | 1.37 µs (1.64× faster) | 7.93 µs (2.2× faster) | 3.71 µs (2.89× faster) | 320.0 MiB |
| Doublets Split Volatile | 372 ns (10.2× faster) | 5.43 ns (56.8× faster) | 164 ns (11× faster) | 451 ns (4.28× faster) | 305 ns (6.56× faster) | 296 ns (6.57× faster) | 685 ns (13.1× faster) | 507 ns (11.9× faster) | — |
| Doublets Split NonVolatile | 1.07 µs (6.54× faster) | 5.65 ns (53× faster) | 180 ns (10.8× faster) | 499 ns (4.51× faster) | 338 ns (6.62× faster) | 326 ns (6.92× faster) | 736 ns (23.8× faster) | 707 ns (15.2× faster) | 400.0 MiB |

![C# doublets vs SQLite, 32 bit, links](docs/benchmarks/links-csharp-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.5 µs | 388 ns | 1.23 µs | 1.55 µs | 1.55 µs | 1.54 µs | 4.6 µs | 3.03 µs | — |
| SQLite File | 2.86 µs | 394 ns | 1.24 µs | 1.58 µs | 1.56 µs | 1.57 µs | 12.7 µs | 6.56 µs | 4.4 MiB |
| SystemDataSQLite Memory | 2.7 µs (1.08× slower) | 321 ns (1.21× faster) | 1.48 µs (1.21× slower) | 1.92 µs (1.24× slower) | 1.86 µs (1.2× slower) | 1.8 µs (1.17× slower) | 4.69 µs (≈ same) | 3.28 µs (1.08× slower) | — |
| SystemDataSQLite File | 3.03 µs (1.06× slower) | 326 ns (1.21× faster) | 1.49 µs (1.21× slower) | 1.92 µs (1.22× slower) | 1.85 µs (1.18× slower) | 1.82 µs (1.16× slower) | 12.7 µs (≈ same) | 6.82 µs (≈ same) | 4.4 MiB |
| Doublets United Volatile | 1.44 µs (1.73× faster) | 23.8 ns (16.3× faster) | 75.3 ns (16.3× faster) | 376 ns (4.11× faster) | 564 ns (2.75× faster) | 476 ns (3.24× faster) | 4 µs (1.15× faster) | 1.66 µs (1.82× faster) | — |
| Doublets United NonVolatile | 1.67 µs (1.72× faster) | 24 ns (16.4× faster) | 76.3 ns (16.2× faster) | 453 ns (3.48× faster) | 657 ns (2.38× faster) | 491 ns (3.2× faster) | 4.17 µs (3.04× faster) | 1.68 µs (3.91× faster) | 64.0 MiB |
| Doublets Split Volatile | 299 ns (8.37× faster) | 14.1 ns (27.5× faster) | 85.3 ns (14.4× faster) | 221 ns (7.02× faster) | 172 ns (9.02× faster) | 185 ns (8.37× faster) | 423 ns (10.9× faster) | 419 ns (7.24× faster) | — |
| Doublets Split NonVolatile | 427 ns (6.71× faster) | 14.2 ns (27.7× faster) | 86.8 ns (14.3× faster) | 234 ns (6.73× faster) | 173 ns (9.03× faster) | 190 ns (8.29× faster) | 493 ns (25.7× faster) | 409 ns (16× faster) | 80.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.99 µs | 383 ns | 1.45 µs | 1.7 µs | 1.75 µs | 1.74 µs | 6.76 µs | 4.53 µs | — |
| SQLite File | 6.77 µs | 385 ns | 2.62 µs | 2.72 µs | 2.79 µs | 2.78 µs | 20.6 µs | 11.7 µs | 48.2 MiB |
| SystemDataSQLite Memory | 2.98 µs (≈ same) | 312 ns (1.22× faster) | 1.6 µs (1.1× slower) | 1.95 µs (1.14× slower) | 1.95 µs (1.11× slower) | 1.96 µs (1.13× slower) | 6 µs (1.13× faster) | 4.17 µs (≈ same) | — |
| SystemDataSQLite File | 6.85 µs (≈ same) | 316 ns (1.22× faster) | 2.93 µs (1.12× slower) | 3.11 µs (1.14× slower) | 3.11 µs (1.12× slower) | 3.1 µs (1.12× slower) | 20.5 µs (≈ same) | 11.8 µs (≈ same) | 48.2 MiB |
| Doublets United Volatile | 1.8 µs (1.65× faster) | 20.7 ns (18.5× faster) | 144 ns (10× faster) | 503 ns (3.39× faster) | 558 ns (3.13× faster) | 775 ns (2.24× faster) | 4.65 µs (1.45× faster) | 2.03 µs (2.23× faster) | — |
| Doublets United NonVolatile | 2.23 µs (3.04× faster) | 21.2 ns (18.1× faster) | 173 ns (15.2× faster) | 642 ns (4.23× faster) | 868 ns (3.21× faster) | 757 ns (3.67× faster) | 5.86 µs (3.5× faster) | 2.65 µs (4.41× faster) | 64.0 MiB |
| Doublets Split Volatile | 398 ns (7.49× faster) | 21 ns (18.2× faster) | 246 ns (5.88× faster) | 395 ns (4.32× faster) | 291 ns (6.01× faster) | 306 ns (5.67× faster) | 744 ns (9.07× faster) | 610 ns (7.42× faster) | — |
| Doublets Split NonVolatile | 487 ns (13.9× faster) | 21.2 ns (18.2× faster) | 254 ns (10.3× faster) | 420 ns (6.47× faster) | 330 ns (8.45× faster) | 341 ns (8.15× faster) | 809 ns (25.4× faster) | 618 ns (18.9× faster) | 80.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.25 µs | 382 ns | 2 µs | 2.31 µs | 2.32 µs | 2.32 µs | 9.85 µs | 6.67 µs | — |
| SQLite File | 10.2 µs | 387 ns | 2.91 µs | 3.43 µs | 3.36 µs | 3.36 µs | 25.6 µs | 15.6 µs | 501.0 MiB |
| SystemDataSQLite Memory | 4.38 µs (≈ same) | 328 ns (1.17× faster) | 2.24 µs (1.12× slower) | 2.53 µs (1.09× slower) | 2.55 µs (1.1× slower) | 2.54 µs (1.09× slower) | 9.46 µs (≈ same) | 6.77 µs (≈ same) | — |
| SystemDataSQLite File | 10.6 µs (≈ same) | 332 ns (1.17× faster) | 3.21 µs (1.1× slower) | 3.77 µs (1.1× slower) | 3.7 µs (1.1× slower) | 3.7 µs (1.1× slower) | 25.8 µs (≈ same) | 16 µs (≈ same) | 501.0 MiB |
| Doublets United Volatile | 3.55 µs (1.2× faster) | 21.3 ns (17.9× faster) | 175 ns (11.4× faster) | 1.53 µs (1.51× faster) | 1.6 µs (1.45× faster) | 1.83 µs (1.27× faster) | 9.22 µs (1.07× faster) | 4.17 µs (1.6× faster) | — |
| Doublets United NonVolatile | 5.23 µs (1.95× faster) | 21.3 ns (18.2× faster) | 190 ns (15.3× faster) | 1.58 µs (2.16× faster) | 1.98 µs (1.7× faster) | 1.82 µs (1.85× faster) | 10.9 µs (2.34× faster) | 5.71 µs (2.74× faster) | 640.0 MiB |
| Doublets Split Volatile | 499 ns (8.52× faster) | 13.5 ns (28.2× faster) | 317 ns (6.32× faster) | 468 ns (4.95× faster) | 331 ns (7.03× faster) | 345 ns (6.75× faster) | 930 ns (10.6× faster) | 746 ns (8.94× faster) | — |
| Doublets Split NonVolatile | 1.36 µs (7.5× faster) | 13.3 ns (29× faster) | 351 ns (8.29× faster) | 513 ns (6.68× faster) | 354 ns (9.5× faster) | 369 ns (9.12× faster) | 1.25 µs (20.5× faster) | 860 ns (18.2× faster) | 800.0 MiB |

![C# doublets vs SQLite, 64 bit, links](docs/benchmarks/links-csharp-64.png)

## Doublets vs SQLite as storage for objects

### Rust doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, Intel(R) Xeon(R) 6973P-C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 760 ns | 426 ns | 1.16 µs | 1.24 µs | — |
| SQLite File | 5.26 µs | 428 ns | 1.49 µs | 8.52 µs | 78.3 MiB |
| Doublets United Volatile Cached | 3.76 µs (4.95× slower) | 1.04 µs (2.44× slower) | 1.56 µs (1.34× slower) | 3.9 µs (3.14× slower) | — |
| Doublets United Volatile Uncached | 84.6 µs (111× slower) | 6.84 µs (16× slower) | 7.3 µs (6.3× slower) | 4.08 µs (3.28× slower) | — |
| Doublets United NonVolatile Cached | 3.99 µs (1.32× faster) | 1.02 µs (2.37× slower) | 1.41 µs (1.06× faster) | 4.1 µs (2.08× faster) | 32.0 MiB |
| Doublets United NonVolatile Uncached | 82.2 µs (15.6× slower) | 6.98 µs (16.3× slower) | 7.45 µs (5× slower) | 4.08 µs (2.09× faster) | 32.0 MiB |
| Doublets Split Volatile Cached | 2.2 µs (2.9× slower) | 523 ns (1.23× slower) | 424 ns (2.74× faster) | 1.82 µs (1.46× slower) | — |
| Doublets Split Volatile Uncached | 31.6 µs (41.6× slower) | 9.37 µs (22× slower) | 9.61 µs (8.29× slower) | 1.94 µs (1.56× slower) | — |
| Doublets Split NonVolatile Cached | 2.32 µs (2.26× faster) | 531 ns (1.24× slower) | 441 ns (3.38× faster) | 2.03 µs (4.19× faster) | 40.0 MiB |
| Doublets Split NonVolatile Uncached | 31.8 µs (6.05× slower) | 8.84 µs (20.6× slower) | 9.06 µs (6.09× slower) | 2.08 µs (4.1× faster) | 40.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.34 µs | 579 ns | 1.98 µs | 2.56 µs | — |
| SQLite File | 3.39 µs | 623 ns | 3.46 µs | 10.1 µs | 783.2 MiB |
| Doublets United Volatile Cached | 6.91 µs (5.14× slower) | 1.39 µs (2.41× slower) | 2.82 µs (1.43× slower) | 10.8 µs (4.22× slower) | — |
| Doublets United Volatile Uncached | 101 µs (75.4× slower) | 9.65 µs (16.7× slower) | 11.5 µs (5.84× slower) | 11.2 µs (4.37× slower) | — |
| Doublets United NonVolatile Cached | 8.22 µs (2.43× slower) | 1.46 µs (2.34× slower) | 3.07 µs (1.13× faster) | 10.9 µs (1.08× slower) | 320.0 MiB |
| Doublets United NonVolatile Uncached | 106 µs (31.3× slower) | 9.68 µs (15.5× slower) | 11.6 µs (3.35× slower) | 11.2 µs (1.1× slower) | 320.0 MiB |
| Doublets Split Volatile Cached | 4.81 µs (3.58× slower) | 882 ns (1.52× slower) | 896 ns (2.21× faster) | 6.05 µs (2.36× slower) | — |
| Doublets Split Volatile Uncached | 44.9 µs (33.4× slower) | 14.4 µs (24.8× slower) | 14.8 µs (7.47× slower) | 6.17 µs (2.41× slower) | — |
| Doublets Split NonVolatile Cached | 6.21 µs (1.83× slower) | 846 ns (1.36× slower) | 870 ns (3.98× faster) | 6.39 µs (1.58× faster) | 400.0 MiB |
| Doublets Split NonVolatile Uncached | 48.1 µs (14.2× slower) | 14.5 µs (23.2× slower) | 15.5 µs (4.48× slower) | 6.57 µs (1.54× faster) | 400.0 MiB |

![Rust doublets vs SQLite, 32 bit, objects](docs/benchmarks/objects-rust-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 831 ns | 526 ns | 1.18 µs | 1.44 µs | — |
| SQLite File | 5.53 µs | 450 ns | 1.8 µs | 9.86 µs | 78.3 MiB |
| Doublets United Volatile Cached | 3.5 µs (4.21× slower) | 1.24 µs (2.35× slower) | 1.96 µs (1.66× slower) | 5.07 µs (3.53× slower) | — |
| Doublets United Volatile Uncached | 55.2 µs (66.4× slower) | 6.64 µs (12.6× slower) | 7.99 µs (6.76× slower) | 5.27 µs (3.67× slower) | — |
| Doublets United NonVolatile Cached | 3.68 µs (1.5× faster) | 1.29 µs (2.87× slower) | 2.33 µs (1.29× slower) | 5.73 µs (1.72× faster) | 64.0 MiB |
| Doublets United NonVolatile Uncached | 55.2 µs (9.98× slower) | 6.58 µs (14.6× slower) | 7.67 µs (4.26× slower) | 5.08 µs (1.94× faster) | 64.0 MiB |
| Doublets Split Volatile Cached | 2.35 µs (2.82× slower) | 504 ns (≈ same) | 548 ns (2.16× faster) | 3.08 µs (2.14× slower) | — |
| Doublets Split Volatile Uncached | 28.4 µs (34.2× slower) | 8.02 µs (15.3× slower) | 8.49 µs (7.17× slower) | 2.73 µs (1.9× slower) | — |
| Doublets Split NonVolatile Cached | 2.28 µs (2.43× faster) | 490 ns (1.09× slower) | 547 ns (3.3× faster) | 2.83 µs (3.49× faster) | 80.0 MiB |
| Doublets Split NonVolatile Uncached | 28.9 µs (5.23× slower) | 8.51 µs (18.9× slower) | 8.72 µs (4.84× slower) | 2.91 µs (3.39× faster) | 80.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 890 ns | 506 ns | 1.68 µs | 2.35 µs | — |
| SQLite File | 6.09 µs | 424 ns | 2.67 µs | 11.3 µs | 783.2 MiB |
| Doublets United Volatile Cached | 5.88 µs (6.61× slower) | 1.51 µs (2.99× slower) | 3.2 µs (1.91× slower) | 11.8 µs (5.04× slower) | — |
| Doublets United Volatile Uncached | 62.6 µs (70.4× slower) | 6.62 µs (13.1× slower) | 9.18 µs (5.47× slower) | 14.2 µs (6.03× slower) | — |
| Doublets United NonVolatile Cached | 9.25 µs (1.52× slower) | 1.67 µs (3.93× slower) | 4.07 µs (1.52× slower) | 14.3 µs (1.26× slower) | 640.0 MiB |
| Doublets United NonVolatile Uncached | 69.9 µs (11.5× slower) | 6.67 µs (15.7× slower) | 9.18 µs (3.43× slower) | 14.1 µs (1.25× slower) | 640.0 MiB |
| Doublets Split Volatile Cached | 4.52 µs (5.08× slower) | 712 ns (1.41× slower) | 1.03 µs (1.63× faster) | 8.89 µs (3.78× slower) | — |
| Doublets Split Volatile Uncached | 29.9 µs (33.6× slower) | 8.19 µs (16.2× slower) | 8.8 µs (5.24× slower) | 8.94 µs (3.8× slower) | — |
| Doublets Split NonVolatile Cached | 8.36 µs (1.37× slower) | 631 ns (1.49× slower) | 1.04 µs (2.57× faster) | 9.19 µs (1.23× faster) | 800.0 MiB |
| Doublets Split NonVolatile Uncached | 33.6 µs (5.52× slower) | 8.74 µs (20.6× slower) | 9.2 µs (3.44× slower) | 9.3 µs (1.21× faster) | 800.0 MiB |

![Rust doublets vs SQLite, 64 bit, objects](docs/benchmarks/objects-rust-64.png)

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.66 µs | 4.81 µs | 6.47 µs | 2.48 µs | — |
| SQLite File | 9.24 µs | 4.92 µs | 6.91 µs | 12.4 µs | 78.3 MiB |
| SystemDataSQLite Memory | 5.47 µs (≈ same) | 4.5 µs (1.07× faster) | 6.44 µs (≈ same) | 2.65 µs (1.07× slower) | — |
| SystemDataSQLite File | 9.19 µs (≈ same) | 4.76 µs (≈ same) | 7.02 µs (≈ same) | 11.8 µs (≈ same) | 78.3 MiB |
| Doublets United Volatile Cached | 19.7 µs (3.49× slower) | 12.3 µs (2.56× slower) | 6.64 µs (≈ same) | 11.6 µs (4.68× slower) | — |
| Doublets United Volatile Uncached | 175 µs (31× slower) | 336 µs (69.8× slower) | 339 µs (52.4× slower) | 11 µs (4.42× slower) | — |
| Doublets United NonVolatile Cached | 20.2 µs (2.18× slower) | 12.3 µs (2.49× slower) | 6.67 µs (≈ same) | 11.8 µs (≈ same) | 32.0 MiB |
| Doublets United NonVolatile Uncached | 181 µs (19.6× slower) | 336 µs (68.3× slower) | 339 µs (49× slower) | 11.9 µs (≈ same) | 32.0 MiB |
| Doublets Split Volatile Cached | 12 µs (2.11× slower) | 14.4 µs (2.99× slower) | 6.67 µs (≈ same) | 6.39 µs (2.58× slower) | — |
| Doublets Split Volatile Uncached | 128 µs (22.7× slower) | 459 µs (95.3× slower) | 461 µs (71.3× slower) | 6.43 µs (2.59× slower) | — |
| Doublets Split NonVolatile Cached | 12.6 µs (1.36× slower) | 14.4 µs (2.92× slower) | 6.79 µs (≈ same) | 6.71 µs (1.85× faster) | 40.0 MiB |
| Doublets Split NonVolatile Uncached | 130 µs (14.1× slower) | 459 µs (93.4× slower) | 462 µs (66.9× slower) | 7.12 µs (1.75× faster) | 40.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.12 µs | 4.15 µs | 6.19 µs | 3.22 µs | — |
| SQLite File | 9.38 µs | 4.27 µs | 6.97 µs | 15.4 µs | 783.2 MiB |
| SystemDataSQLite Memory | 4.8 µs (1.07× faster) | 4.11 µs (≈ same) | 6.38 µs (≈ same) | 3.13 µs (≈ same) | — |
| SystemDataSQLite File | 9.31 µs (≈ same) | 4.11 µs (≈ same) | 7.17 µs (≈ same) | 11.5 µs (1.34× faster) | 783.2 MiB |
| Doublets United Volatile Cached | 19 µs (3.71× slower) | 11.4 µs (2.74× slower) | 6.71 µs (1.08× slower) | 14.9 µs (4.63× slower) | — |
| Doublets United Volatile Uncached | 187 µs (36.5× slower) | 289 µs (69.6× slower) | 292 µs (47.2× slower) | 14.9 µs (4.62× slower) | — |
| Doublets United NonVolatile Cached | 26.5 µs (2.82× slower) | 11.4 µs (2.67× slower) | 6.71 µs (≈ same) | 17.2 µs (1.12× slower) | 320.0 MiB |
| Doublets United NonVolatile Uncached | 215 µs (23× slower) | 280 µs (65.5× slower) | 287 µs (41.3× slower) | 15.3 µs (≈ same) | 320.0 MiB |
| Doublets Split Volatile Cached | 12.2 µs (2.39× slower) | 12.9 µs (3.11× slower) | 7.23 µs (1.17× slower) | 9.29 µs (2.89× slower) | — |
| Doublets Split Volatile Uncached | 110 µs (21.5× slower) | 385 µs (92.8× slower) | 390 µs (63× slower) | 8.77 µs (2.72× slower) | — |
| Doublets Split NonVolatile Cached | 19.9 µs (2.12× slower) | 13.1 µs (3.07× slower) | 7.45 µs (1.07× slower) | 9.81 µs (1.57× faster) | 400.0 MiB |
| Doublets Split NonVolatile Uncached | 123 µs (13.1× slower) | 379 µs (88.7× slower) | 390 µs (56× slower) | 10.1 µs (1.52× faster) | 400.0 MiB |

![C# doublets vs SQLite, 32 bit, objects](docs/benchmarks/objects-csharp-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.4 µs | 4.31 µs | 5.71 µs | 2.45 µs | — |
| SQLite File | 6.25 µs | 4.44 µs | 6.78 µs | 7.9 µs | 78.3 MiB |
| SystemDataSQLite Memory | 5.25 µs (≈ same) | 4.24 µs (≈ same) | 6.06 µs (1.06× slower) | 2.28 µs (1.07× faster) | — |
| SystemDataSQLite File | 6.38 µs (≈ same) | 4.38 µs (≈ same) | 7.17 µs (1.06× slower) | 8.17 µs (≈ same) | 78.3 MiB |
| Doublets United Volatile Cached | 21.4 µs (3.97× slower) | 12.3 µs (2.85× slower) | 6.35 µs (1.11× slower) | 14 µs (5.71× slower) | — |
| Doublets United Volatile Uncached | 194 µs (35.9× slower) | 347 µs (80.6× slower) | 348 µs (60.9× slower) | 14 µs (5.73× slower) | — |
| Doublets United NonVolatile Cached | 22.3 µs (3.57× slower) | 11.9 µs (2.67× slower) | 6.28 µs (1.08× faster) | 13.9 µs (1.75× slower) | 64.0 MiB |
| Doublets United NonVolatile Uncached | 195 µs (31.2× slower) | 346 µs (78× slower) | 349 µs (51.4× slower) | 14.8 µs (1.88× slower) | 64.0 MiB |
| Doublets Split Volatile Cached | 12.4 µs (2.3× slower) | 13.1 µs (3.04× slower) | 6.02 µs (1.05× slower) | 9.6 µs (3.92× slower) | — |
| Doublets Split Volatile Uncached | 117 µs (21.7× slower) | 411 µs (95.4× slower) | 400 µs (70.1× slower) | 8.43 µs (3.44× slower) | — |
| Doublets Split NonVolatile Cached | 12.9 µs (2.07× slower) | 12.9 µs (2.9× slower) | 5.78 µs (1.17× faster) | 9.07 µs (1.15× slower) | 80.0 MiB |
| Doublets Split NonVolatile Uncached | 118 µs (18.9× slower) | 401 µs (90.3× slower) | 408 µs (60.1× slower) | 9.43 µs (1.19× slower) | 80.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37347766917) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.71 µs | 4.63 µs | 6.98 µs | 3.33 µs | — |
| SQLite File | 10.5 µs | 4.76 µs | 7.86 µs | 15.9 µs | 783.2 MiB |
| SystemDataSQLite Memory | 5.39 µs (1.06× faster) | 4.49 µs (≈ same) | 6.96 µs (≈ same) | 3.28 µs (≈ same) | — |
| SystemDataSQLite File | 10.2 µs (≈ same) | 4.66 µs (≈ same) | 8.01 µs (≈ same) | 16.5 µs (≈ same) | 783.2 MiB |
| Doublets United Volatile Cached | 24.9 µs (4.37× slower) | 13.5 µs (2.91× slower) | 8.25 µs (1.18× slower) | 21.2 µs (6.36× slower) | — |
| Doublets United Volatile Uncached | 242 µs (42.3× slower) | 359 µs (77.6× slower) | 363 µs (52× slower) | 21 µs (6.3× slower) | — |
| Doublets United NonVolatile Cached | 41.6 µs (3.96× slower) | 13.5 µs (2.83× slower) | 8.55 µs (1.09× slower) | 21.7 µs (1.36× slower) | 640.0 MiB |
| Doublets United NonVolatile Uncached | 272 µs (26× slower) | 359 µs (75.3× slower) | 365 µs (46.5× slower) | 23.1 µs (1.45× slower) | 640.0 MiB |
| Doublets Split Volatile Cached | 14.4 µs (2.53× slower) | 15.3 µs (3.31× slower) | 8.65 µs (1.24× slower) | 11.6 µs (3.49× slower) | — |
| Doublets Split Volatile Uncached | 139 µs (24.3× slower) | 469 µs (101× slower) | 474 µs (67.9× slower) | 11.1 µs (3.33× slower) | — |
| Doublets Split NonVolatile Cached | 32.2 µs (3.07× slower) | 15.3 µs (3.22× slower) | 8.85 µs (1.13× slower) | 19.1 µs (1.2× slower) | 800.0 MiB |
| Doublets Split NonVolatile Uncached | 166 µs (15.9× slower) | 469 µs (98.5× slower) | 475 µs (60.5× slower) | 13.4 µs (1.19× faster) | 800.0 MiB |

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
