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

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.06 µs | 66.2 ns | 386 ns | 427 ns | 455 ns | 449 ns | 2.03 µs | 1.3 µs | — |
| SQLite File | 1.35 µs | 69.4 ns | 388 ns | 445 ns | 471 ns | 468 ns | 7.6 µs | 3.81 µs | 4.4 MiB |
| Doublets United Volatile | 302 ns (3.5× faster) | 1.22 ns (54.4× faster) | 2.67 ns (145× faster) | 123 ns (3.49× faster) | 145 ns (3.14× faster) | 156 ns (2.89× faster) | 780 ns (2.6× faster) | 301 ns (4.32× faster) | — |
| Doublets United NonVolatile | 300 ns (4.48× faster) | 1.21 ns (57.2× faster) | 2.49 ns (156× faster) | 130 ns (3.42× faster) | 152 ns (3.09× faster) | 159 ns (2.95× faster) | 777 ns (9.78× faster) | 298 ns (12.8× faster) | 32.0 MiB |
| Doublets Split Volatile | 60.8 ns (17.4× faster) | 2.34 ns (28.3× faster) | 2.5 ns (155× faster) | 37.3 ns (11.5× faster) | 18.4 ns (24.7× faster) | 18.6 ns (24.2× faster) | 110 ns (18.4× faster) | 367 ns (3.55× faster) | — |
| Doublets Split NonVolatile | 59.2 ns (22.8× faster) | 2.35 ns (29.5× faster) | 2.34 ns (166× faster) | 38.1 ns (11.7× faster) | 19 ns (24.8× faster) | 18.9 ns (24.8× faster) | 113 ns (67× faster) | 383 ns (9.94× faster) | 40.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.35 µs | 112 ns | 895 ns | 989 ns | 998 ns | 998 ns | 6.6 µs | 4.55 µs | — |
| SQLite File | 4.82 µs | 116 ns | 1.52 µs | 1.52 µs | 1.57 µs | 1.57 µs | 13.3 µs | 8.58 µs | 48.2 MiB |
| Doublets United Volatile | 931 ns (2.53× faster) | 3.54 ns (31.5× faster) | 12.7 ns (70.4× faster) | 553 ns (1.79× faster) | 737 ns (1.35× faster) | 771 ns (1.29× faster) | 2.58 µs (2.56× faster) | 912 ns (5× faster) | — |
| Doublets United NonVolatile | 1.1 µs (4.37× faster) | 3.76 ns (30.9× faster) | 15.8 ns (96.7× faster) | 632 ns (2.4× faster) | 915 ns (1.71× faster) | 975 ns (1.61× faster) | 3.19 µs (4.17× faster) | 1.12 µs (7.64× faster) | 32.0 MiB |
| Doublets Split Volatile | 178 ns (13.2× faster) | 4.46 ns (25.1× faster) | 16.4 ns (54.5× faster) | 105 ns (9.45× faster) | 65.5 ns (15.2× faster) | 66 ns (15.1× faster) | 309 ns (21.4× faster) | 1.14 µs (4× faster) | — |
| Doublets Split NonVolatile | 185 ns (26× faster) | 4.47 ns (26× faster) | 20.8 ns (73.1× faster) | 127 ns (12× faster) | 97.8 ns (16× faster) | 90.7 ns (17.4× faster) | 391 ns (34.1× faster) | 1.54 µs (5.59× faster) | 40.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.29 µs | 130 ns | 1.75 µs | 1.76 µs | 1.78 µs | 1.82 µs | 10 µs | 6.68 µs | — |
| SQLite File | 11 µs | 130 ns | 2.68 µs | 2.99 µs | 2.94 µs | 3 µs | 28.9 µs | 17.8 µs | 501.0 MiB |
| Doublets United Volatile | 2.54 µs (1.69× faster) | 1.91 ns (68.1× faster) | 33.6 ns (52× faster) | 1.66 µs (1.06× faster) | 1.94 µs (1.09× slower) | 1.99 µs (1.09× slower) | 7.41 µs (1.35× faster) | 2.8 µs (2.39× faster) | — |
| Doublets United NonVolatile | 3.2 µs (3.45× faster) | 1.89 ns (68.9× faster) | 38.8 ns (68.9× faster) | 1.68 µs (1.78× faster) | 2.1 µs (1.4× faster) | 2.14 µs (1.4× faster) | 8.63 µs (3.35× faster) | 3.63 µs (4.89× faster) | 320.0 MiB |
| Doublets Split Volatile | 394 ns (10.9× faster) | 3.63 ns (35.8× faster) | 29.6 ns (59× faster) | 255 ns (6.9× faster) | 185 ns (9.59× faster) | 182 ns (10× faster) | 760 ns (13.2× faster) | 3.25 µs (2.06× faster) | — |
| Doublets Split NonVolatile | 569 ns (19.4× faster) | 3.6 ns (36.2× faster) | 30.6 ns (87.5× faster) | 261 ns (11.4× faster) | 188 ns (15.6× faster) | 188 ns (16× faster) | 807 ns (35.8× faster) | 3.89 µs (4.57× faster) | 400.0 MiB |

![Rust doublets vs SQLite, 32 bit, links](docs/benchmarks/links-rust-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.7 µs | 128 ns | 597 ns | 725 ns | 756 ns | 759 ns | 3.16 µs | 2.05 µs | — |
| SQLite File | 2.04 µs | 133 ns | 630 ns | 773 ns | 795 ns | 807 ns | 10.5 µs | 5.18 µs | 4.4 MiB |
| Doublets United Volatile | 449 ns (3.79× faster) | 1.79 ns (71.2× faster) | 3.19 ns (187× faster) | 179 ns (4.05× faster) | 234 ns (3.23× faster) | 253 ns (2.99× faster) | 1.13 µs (2.79× faster) | 449 ns (4.58× faster) | — |
| Doublets United NonVolatile | 460 ns (4.44× faster) | 1.68 ns (79× faster) | 3.38 ns (186× faster) | 193 ns (4× faster) | 249 ns (3.19× faster) | 265 ns (3.05× faster) | 1.18 µs (8.9× faster) | 457 ns (11.3× faster) | 64.0 MiB |
| Doublets Split Volatile | 111 ns (15.3× faster) | 3.76 ns (33.9× faster) | 4 ns (149× faster) | 56.8 ns (12.7× faster) | 28.2 ns (26.8× faster) | 29.8 ns (25.5× faster) | 167 ns (18.9× faster) | 604 ns (3.4× faster) | — |
| Doublets Split NonVolatile | 105 ns (19.5× faster) | 3.77 ns (35.2× faster) | 4.21 ns (150× faster) | 55.6 ns (13.9× faster) | 28.6 ns (27.9× faster) | 29.8 ns (27.1× faster) | 169 ns (61.9× faster) | 606 ns (8.55× faster) | 80.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, Intel(R) Xeon(R) 6973P-C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.9 µs | 80.4 ns | 763 ns | 808 ns | 835 ns | 814 ns | 5.73 µs | 3.88 µs | — |
| SQLite File | 3.9 µs | 86.5 ns | 1.25 µs | 1.24 µs | 1.3 µs | 1.28 µs | 10.8 µs | 7.03 µs | 48.2 MiB |
| Doublets United Volatile | 1.22 µs (1.55× faster) | 3.12 ns (25.7× faster) | 22.4 ns (34.1× faster) | 823 ns (≈ same) | 1.08 µs (1.3× slower) | 1.15 µs (1.42× slower) | 3.6 µs (1.59× faster) | 1.37 µs (2.83× faster) | — |
| Doublets United NonVolatile | 1.36 µs (2.86× faster) | 4.25 ns (20.3× faster) | 24.3 ns (51.2× faster) | 875 ns (1.41× faster) | 1.11 µs (1.17× faster) | 1.13 µs (1.14× faster) | 4.21 µs (2.57× faster) | 1.33 µs (5.29× faster) | 64.0 MiB |
| Doublets Split Volatile | 189 ns (10.1× faster) | 4.23 ns (19× faster) | 20.9 ns (36.5× faster) | 147 ns (5.48× faster) | 107 ns (7.81× faster) | 113 ns (7.23× faster) | 396 ns (14.4× faster) | 1.27 µs (3.05× faster) | — |
| Doublets Split NonVolatile | 183 ns (21.3× faster) | 6.08 ns (14.2× faster) | 19.6 ns (63.4× faster) | 115 ns (10.7× faster) | 78.6 ns (16.6× faster) | 80 ns (16× faster) | 442 ns (24.5× faster) | 1.51 µs (4.67× faster) | 80.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 3.56 µs | 124 ns | 1.59 µs | 1.63 µs | 1.74 µs | 1.71 µs | 9.14 µs | 5.63 µs | — |
| SQLite File | 9.15 µs | 129 ns | 2.24 µs | 2.58 µs | 2.5 µs | 2.49 µs | 23.6 µs | 14.1 µs | 501.0 MiB |
| Doublets United Volatile | 2.25 µs (1.58× faster) | 3.49 ns (35.6× faster) | 26.6 ns (59.6× faster) | 1.34 µs (1.21× faster) | 1.52 µs (1.15× faster) | 1.5 µs (1.14× faster) | 6.37 µs (1.43× faster) | 2.5 µs (2.25× faster) | — |
| Doublets United NonVolatile | 2.59 µs (3.53× faster) | 3.53 ns (36.4× faster) | 27.2 ns (82.4× faster) | 1.34 µs (1.92× faster) | 1.48 µs (1.69× faster) | 1.46 µs (1.71× faster) | 6.94 µs (3.4× faster) | 3.4 µs (4.14× faster) | 640.0 MiB |
| Doublets Split Volatile | 342 ns (10.4× faster) | 4.27 ns (29.2× faster) | 27 ns (58.7× faster) | 242 ns (6.73× faster) | 177 ns (9.82× faster) | 179 ns (9.54× faster) | 763 ns (12× faster) | 2.76 µs (2.04× faster) | — |
| Doublets Split NonVolatile | 630 ns (14.5× faster) | 4.97 ns (25.9× faster) | 31 ns (72.1× faster) | 242 ns (10.6× faster) | 181 ns (13.8× faster) | 190 ns (13.1× faster) | 817 ns (28.9× faster) | 3.75 µs (3.75× faster) | 800.0 MiB |

![Rust doublets vs SQLite, 64 bit, links](docs/benchmarks/links-rust-64.png)

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.58 µs | 327 ns | 801 ns | 968 ns | 1.03 µs | 1.03 µs | 2.75 µs | 1.87 µs | — |
| SQLite File | 1.94 µs | 330 ns | 796 ns | 957 ns | 1.04 µs | 1.04 µs | 9.27 µs | 4.65 µs | 4.4 MiB |
| SystemDataSQLite Memory | 1.59 µs (≈ same) | 189 ns (1.73× faster) | 876 ns (1.09× slower) | 1.07 µs (1.1× slower) | 1.1 µs (1.07× slower) | 1.09 µs (1.06× slower) | 2.71 µs (≈ same) | 1.94 µs (≈ same) | — |
| SystemDataSQLite File | 1.89 µs (≈ same) | 190 ns (1.74× faster) | 891 ns (1.12× slower) | 1.07 µs (1.12× slower) | 1.09 µs (≈ same) | 1.05 µs (≈ same) | 8.63 µs (1.07× faster) | 4.61 µs (≈ same) | 4.4 MiB |
| Doublets United Volatile | 598 ns (2.64× faster) | 7.81 ns (41.8× faster) | 28.2 ns (28.5× faster) | 144 ns (6.74× faster) | 175 ns (5.89× faster) | 243 ns (4.24× faster) | 1.33 µs (2.06× faster) | 650 ns (2.87× faster) | — |
| Doublets United NonVolatile | 628 ns (3.08× faster) | 7.92 ns (41.7× faster) | 27.6 ns (28.9× faster) | 149 ns (6.41× faster) | 181 ns (5.72× faster) | 245 ns (4.23× faster) | 1.33 µs (6.95× faster) | 653 ns (7.12× faster) | 32.0 MiB |
| Doublets Split Volatile | 105 ns (15.1× faster) | 8.42 ns (38.8× faster) | 34.1 ns (23.5× faster) | 52.4 ns (18.5× faster) | 48 ns (21.5× faster) | 46.2 ns (22.3× faster) | 126 ns (21.8× faster) | 149 ns (12.6× faster) | — |
| Doublets Split NonVolatile | 138 ns (14× faster) | 7.96 ns (41.5× faster) | 33.9 ns (23.5× faster) | 52.1 ns (18.4× faster) | 48.2 ns (21.5× faster) | 47 ns (22× faster) | 128 ns (72.6× faster) | 141 ns (33× faster) | 40.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.56 µs | 308 ns | 1.27 µs | 1.45 µs | 1.49 µs | 1.47 µs | 6.49 µs | 4.43 µs | — |
| SQLite File | 6.34 µs | 311 ns | 2.43 µs | 2.45 µs | 2.51 µs | 2.51 µs | 19.7 µs | 12 µs | 48.2 MiB |
| SystemDataSQLite Memory | 2.61 µs (≈ same) | 246 ns (1.25× faster) | 1.38 µs (1.08× slower) | 1.64 µs (1.14× slower) | 1.67 µs (1.12× slower) | 1.63 µs (1.11× slower) | 6.14 µs (1.06× faster) | 4.35 µs (≈ same) | — |
| SystemDataSQLite File | 6.52 µs (≈ same) | 249 ns (1.25× faster) | 2.66 µs (1.1× slower) | 2.69 µs (1.1× slower) | 2.75 µs (1.1× slower) | 2.74 µs (1.09× slower) | 19.8 µs (≈ same) | 11.6 µs (≈ same) | 48.2 MiB |
| Doublets United Volatile | 1.25 µs (2.05× faster) | 11.7 ns (26.3× faster) | 82.4 ns (15.5× faster) | 448 ns (3.23× faster) | 489 ns (3.05× faster) | 648 ns (2.26× faster) | 3.28 µs (1.98× faster) | 1.55 µs (2.86× faster) | — |
| Doublets United NonVolatile | 1.53 µs (4.14× faster) | 11.6 ns (26.7× faster) | 93.5 ns (25.9× faster) | 677 ns (3.62× faster) | 776 ns (3.23× faster) | 783 ns (3.21× faster) | 4.48 µs (4.4× faster) | 2.04 µs (5.91× faster) | 32.0 MiB |
| Doublets Split Volatile | 278 ns (9.18× faster) | 8.88 ns (34.6× faster) | 111 ns (11.5× faster) | 279 ns (5.19× faster) | 213 ns (7× faster) | 214 ns (6.84× faster) | 509 ns (12.8× faster) | 405 ns (10.9× faster) | — |
| Doublets Split NonVolatile | 327 ns (19.4× faster) | 8.98 ns (34.7× faster) | 115 ns (21.1× faster) | 321 ns (7.64× faster) | 229 ns (11× faster) | 228 ns (11× faster) | 526 ns (37.6× faster) | 390 ns (30.9× faster) | 40.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.42 µs | 386 ns | 2.06 µs | 2.36 µs | 2.42 µs | 2.39 µs | 10.5 µs | 7.26 µs | — |
| SQLite File | 10.3 µs | 390 ns | 2.97 µs | 3.43 µs | 3.37 µs | 3.36 µs | 26 µs | 16 µs | 501.0 MiB |
| SystemDataSQLite Memory | 4.51 µs (≈ same) | 312 ns (1.24× faster) | 2.36 µs (1.15× slower) | 2.64 µs (1.12× slower) | 2.64 µs (1.09× slower) | 2.62 µs (1.09× slower) | 10.1 µs (≈ same) | 7.15 µs (≈ same) | — |
| SystemDataSQLite File | 10.7 µs (≈ same) | 316 ns (1.24× faster) | 3.29 µs (1.11× slower) | 3.84 µs (1.12× slower) | 3.78 µs (1.12× slower) | 3.78 µs (1.12× slower) | 26.2 µs (≈ same) | 16.4 µs (≈ same) | 501.0 MiB |
| Doublets United Volatile | 3.2 µs (1.38× faster) | 15 ns (25.8× faster) | 174 ns (11.9× faster) | 1.17 µs (2.01× faster) | 1.54 µs (1.58× faster) | 1.43 µs (1.68× faster) | 7.78 µs (1.35× faster) | 3.56 µs (2.04× faster) | — |
| Doublets United NonVolatile | 4.01 µs (2.57× faster) | 15.2 ns (25.8× faster) | 196 ns (15.1× faster) | 1.59 µs (2.16× faster) | 1.96 µs (1.72× faster) | 1.75 µs (1.92× faster) | 9.68 µs (2.69× faster) | 4.64 µs (3.45× faster) | 320.0 MiB |
| Doublets Split Volatile | 493 ns (8.98× faster) | 14.7 ns (26.3× faster) | 306 ns (6.75× faster) | 452 ns (5.24× faster) | 325 ns (7.45× faster) | 343 ns (6.97× faster) | 932 ns (11.3× faster) | 732 ns (9.92× faster) | — |
| Doublets Split NonVolatile | 856 ns (12× faster) | 15.2 ns (25.8× faster) | 352 ns (8.44× faster) | 524 ns (6.53× faster) | 371 ns (9.07× faster) | 381 ns (8.81× faster) | 1.13 µs (23.1× faster) | 924 ns (17.3× faster) | 400.0 MiB |

![C# doublets vs SQLite, 32 bit, links](docs/benchmarks/links-csharp-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.45 µs | 402 ns | 1.17 µs | 1.45 µs | 1.5 µs | 1.5 µs | 4.33 µs | 2.92 µs | — |
| SQLite File | 2.86 µs | 408 ns | 1.18 µs | 1.48 µs | 1.51 µs | 1.51 µs | 13.9 µs | 7.15 µs | 4.4 MiB |
| SystemDataSQLite Memory | 2.61 µs (1.07× slower) | 309 ns (1.3× faster) | 1.45 µs (1.25× slower) | 1.76 µs (1.22× slower) | 1.8 µs (1.2× slower) | 1.76 µs (1.17× slower) | 4.33 µs (≈ same) | 3.16 µs (1.08× slower) | — |
| SystemDataSQLite File | 3.02 µs (1.05× slower) | 316 ns (1.29× faster) | 1.47 µs (1.25× slower) | 1.78 µs (1.2× slower) | 1.81 µs (1.2× slower) | 1.77 µs (1.17× slower) | 14 µs (≈ same) | 7.39 µs (≈ same) | 4.4 MiB |
| Doublets United Volatile | 1.26 µs (1.94× faster) | 23 ns (17.5× faster) | 68.3 ns (17.1× faster) | 277 ns (5.21× faster) | 411 ns (3.66× faster) | 337 ns (4.46× faster) | 2.87 µs (1.51× faster) | 1.31 µs (2.23× faster) | — |
| Doublets United NonVolatile | 1.38 µs (2.08× faster) | 24.3 ns (16.8× faster) | 67.6 ns (17.4× faster) | 343 ns (4.32× faster) | 451 ns (3.35× faster) | 372 ns (4.07× faster) | 2.98 µs (4.67× faster) | 1.36 µs (5.26× faster) | 64.0 MiB |
| Doublets Split Volatile | 209 ns (11.7× faster) | 21.7 ns (18.5× faster) | 77.5 ns (15× faster) | 133 ns (10.9× faster) | 116 ns (13× faster) | 109 ns (13.7× faster) | 263 ns (16.5× faster) | 300 ns (9.73× faster) | — |
| Doublets Split NonVolatile | 301 ns (9.49× faster) | 21.7 ns (18.8× faster) | 78.1 ns (15.1× faster) | 145 ns (10.2× faster) | 120 ns (12.6× faster) | 112 ns (13.5× faster) | 270 ns (51.6× faster) | 299 ns (23.9× faster) | 80.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 3.04 µs | 385 ns | 1.43 µs | 1.74 µs | 1.78 µs | 1.75 µs | 6.68 µs | 4.6 µs | — |
| SQLite File | 6.75 µs | 391 ns | 2.58 µs | 2.74 µs | 2.75 µs | 2.75 µs | 20.5 µs | 11.6 µs | 48.2 MiB |
| SystemDataSQLite Memory | 3.06 µs (≈ same) | 310 ns (1.24× faster) | 1.64 µs (1.15× slower) | 1.99 µs (1.14× slower) | 2.01 µs (1.13× slower) | 2.02 µs (1.15× slower) | 6.53 µs (≈ same) | 4.45 µs (≈ same) | — |
| SystemDataSQLite File | 6.91 µs (≈ same) | 314 ns (1.25× faster) | 2.9 µs (1.12× slower) | 3.05 µs (1.11× slower) | 3.1 µs (1.13× slower) | 3.14 µs (1.14× slower) | 20.8 µs (≈ same) | 11.9 µs (≈ same) | 48.2 MiB |
| Doublets United Volatile | 1.85 µs (1.65× faster) | 21.2 ns (18.1× faster) | 142 ns (10.1× faster) | 520 ns (3.35× faster) | 624 ns (2.84× faster) | 722 ns (2.43× faster) | 4.84 µs (1.38× faster) | 2.08 µs (2.21× faster) | — |
| Doublets United NonVolatile | 2.06 µs (3.27× faster) | 21.1 ns (18.5× faster) | 172 ns (15× faster) | 687 ns (3.99× faster) | 841 ns (3.27× faster) | 765 ns (3.6× faster) | 5.34 µs (3.85× faster) | 2.38 µs (4.87× faster) | 64.0 MiB |
| Doublets Split Volatile | 344 ns (8.85× faster) | 12.3 ns (31.2× faster) | 202 ns (7.06× faster) | 330 ns (5.28× faster) | 265 ns (6.7× faster) | 277 ns (6.34× faster) | 611 ns (10.9× faster) | 536 ns (8.58× faster) | — |
| Doublets Split NonVolatile | 452 ns (14.9× faster) | 12.8 ns (30.5× faster) | 234 ns (11× faster) | 375 ns (7.32× faster) | 303 ns (9.08× faster) | 316 ns (8.71× faster) | 695 ns (29.5× faster) | 578 ns (20.1× faster) | 80.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.05 µs | 314 ns | 2.01 µs | 2.1 µs | 2.15 µs | 2.1 µs | 10.1 µs | 7.11 µs | — |
| SQLite File | 10.1 µs | 317 ns | 2.81 µs | 3.25 µs | 3.16 µs | 3.14 µs | 25.5 µs | 15.5 µs | 501.0 MiB |
| SystemDataSQLite Memory | 3.9 µs (≈ same) | 238 ns (1.32× faster) | 2.05 µs (≈ same) | 2.29 µs (1.09× slower) | 2.33 µs (1.08× slower) | 2.32 µs (1.11× slower) | 9.72 µs (≈ same) | 6.87 µs (≈ same) | — |
| SystemDataSQLite File | 10.3 µs (≈ same) | 241 ns (1.32× faster) | 3.04 µs (1.08× slower) | 3.53 µs (1.09× slower) | 3.41 µs (1.08× slower) | 3.42 µs (1.09× slower) | 25.5 µs (≈ same) | 15.6 µs (≈ same) | 501.0 MiB |
| Doublets United Volatile | 3.68 µs (1.1× faster) | 18.2 ns (17.2× faster) | 188 ns (10.7× faster) | 1.61 µs (1.31× faster) | 2.03 µs (1.06× faster) | 1.88 µs (1.12× faster) | 9.62 µs (≈ same) | 4.35 µs (1.64× faster) | — |
| Doublets United NonVolatile | 5.41 µs (1.87× faster) | 18.2 ns (17.5× faster) | 191 ns (14.7× faster) | 1.59 µs (2.04× faster) | 1.97 µs (1.6× faster) | 1.84 µs (1.71× faster) | 10.7 µs (2.39× faster) | 5.45 µs (2.85× faster) | 640.0 MiB |
| Doublets Split Volatile | 466 ns (8.68× faster) | 10.1 ns (30.9× faster) | 214 ns (9.39× faster) | 519 ns (4.06× faster) | 357 ns (6.03× faster) | 357 ns (5.87× faster) | 888 ns (11.3× faster) | 637 ns (11.2× faster) | — |
| Doublets Split NonVolatile | 2.02 µs (5.02× faster) | 10.5 ns (30.4× faster) | 231 ns (12.2× faster) | 553 ns (5.87× faster) | 368 ns (8.57× faster) | 382 ns (8.23× faster) | 1.36 µs (18.7× faster) | 920 ns (16.9× faster) | 800.0 MiB |

![C# doublets vs SQLite, 64 bit, links](docs/benchmarks/links-csharp-64.png)

## Doublets vs SQLite as storage for objects

### Rust doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V45 96-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 785 ns | 492 ns | 1.18 µs | 1.41 µs | — |
| SQLite File | 5.36 µs | 423 ns | 1.69 µs | 11 µs | 78.3 MiB |
| Doublets United Volatile Cached | 3.11 µs (3.96× slower) | 972 ns (1.97× slower) | 1.49 µs (1.26× slower) | 3.56 µs (2.52× slower) | — |
| Doublets United Volatile Uncached | 50.8 µs (64.7× slower) | 5.77 µs (11.7× slower) | 6.44 µs (5.46× slower) | 3.51 µs (2.48× slower) | — |
| Doublets United NonVolatile Cached | 3.17 µs (1.69× faster) | 974 ns (2.3× slower) | 1.81 µs (≈ same) | 4.08 µs (2.7× faster) | 32.0 MiB |
| Doublets United NonVolatile Uncached | 50.9 µs (9.5× slower) | 5.76 µs (13.6× slower) | 6.81 µs (4.03× slower) | 4.87 µs (2.26× faster) | 32.0 MiB |
| Doublets Split Volatile Cached | 2.05 µs (2.61× slower) | 443 ns (1.11× faster) | 475 ns (2.49× faster) | 2.02 µs (1.43× slower) | — |
| Doublets Split Volatile Uncached | 23.2 µs (29.5× slower) | 7.58 µs (15.4× slower) | 7.83 µs (6.63× slower) | 1.57 µs (1.11× slower) | — |
| Doublets Split NonVolatile Cached | 2.06 µs (2.6× faster) | 460 ns (1.09× slower) | 418 ns (4.05× faster) | 2.2 µs (5.01× faster) | 40.0 MiB |
| Doublets Split NonVolatile Uncached | 24.3 µs (4.54× slower) | 7.28 µs (17.2× slower) | 7.54 µs (4.45× slower) | 2.79 µs (3.95× faster) | 40.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.38 µs | 587 ns | 2 µs | 2.7 µs | — |
| SQLite File | 3.41 µs | 636 ns | 3.56 µs | 10.1 µs | 783.2 MiB |
| Doublets United Volatile Cached | 7.4 µs (5.37× slower) | 1.5 µs (2.55× slower) | 3.06 µs (1.53× slower) | 11.6 µs (4.31× slower) | — |
| Doublets United Volatile Uncached | 101 µs (73.6× slower) | 9.69 µs (16.5× slower) | 11.5 µs (5.75× slower) | 11.1 µs (4.13× slower) | — |
| Doublets United NonVolatile Cached | 8.31 µs (2.44× slower) | 1.51 µs (2.38× slower) | 2.95 µs (1.21× faster) | 10.9 µs (1.08× slower) | 320.0 MiB |
| Doublets United NonVolatile Uncached | 107 µs (31.4× slower) | 9.62 µs (15.1× slower) | 11.5 µs (3.22× slower) | 10.7 µs (1.06× slower) | 320.0 MiB |
| Doublets Split Volatile Cached | 4.78 µs (3.47× slower) | 870 ns (1.48× slower) | 904 ns (2.22× faster) | 5.87 µs (2.18× slower) | — |
| Doublets Split Volatile Uncached | 45.1 µs (32.7× slower) | 14.4 µs (24.6× slower) | 14.5 µs (7.24× slower) | 5.39 µs (2× slower) | — |
| Doublets Split NonVolatile Cached | 5.84 µs (1.71× slower) | 819 ns (1.29× slower) | 837 ns (4.26× faster) | 5.96 µs (1.69× faster) | 400.0 MiB |
| Doublets Split NonVolatile Uncached | 47.8 µs (14× slower) | 14.3 µs (22.6× slower) | 15 µs (4.21× slower) | 6.52 µs (1.55× faster) | 400.0 MiB |

![Rust doublets vs SQLite, 32 bit, objects](docs/benchmarks/objects-rust-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.23 µs | 562 ns | 1.45 µs | 1.77 µs | — |
| SQLite File | 1.96 µs | 631 ns | 2.44 µs | 6.93 µs | 78.3 MiB |
| Doublets United Volatile Cached | 4.88 µs (3.95× slower) | 1.37 µs (2.45× slower) | 1.88 µs (1.3× slower) | 5.37 µs (3.04× slower) | — |
| Doublets United Volatile Uncached | 94.6 µs (76.6× slower) | 9.92 µs (17.7× slower) | 10.8 µs (7.46× slower) | 5.57 µs (3.16× slower) | — |
| Doublets United NonVolatile Cached | 5.18 µs (2.64× slower) | 1.4 µs (2.22× slower) | 2.07 µs (1.18× faster) | 5.89 µs (1.18× faster) | 64.0 MiB |
| Doublets United NonVolatile Uncached | 94.9 µs (48.4× slower) | 10.2 µs (16.2× slower) | 11.2 µs (4.6× slower) | 5.85 µs (1.18× faster) | 64.0 MiB |
| Doublets Split Volatile Cached | 3.32 µs (2.69× slower) | 758 ns (1.35× slower) | 634 ns (2.28× faster) | 3.1 µs (1.75× slower) | — |
| Doublets Split Volatile Uncached | 48.6 µs (39.4× slower) | 13.9 µs (24.8× slower) | 14.3 µs (9.86× slower) | 2.94 µs (1.66× slower) | — |
| Doublets Split NonVolatile Cached | 3.53 µs (1.8× slower) | 758 ns (1.2× slower) | 617 ns (3.95× faster) | 3.47 µs (2× faster) | 80.0 MiB |
| Doublets Split NonVolatile Uncached | 48.3 µs (24.6× slower) | 14.6 µs (23.2× slower) | 15 µs (6.14× slower) | 3.75 µs (1.85× faster) | 80.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.37 µs | 558 ns | 1.93 µs | 2.56 µs | — |
| SQLite File | 3.41 µs | 631 ns | 3.53 µs | 9.96 µs | 783.2 MiB |
| Doublets United Volatile Cached | 7.26 µs (5.29× slower) | 1.59 µs (2.85× slower) | 2.85 µs (1.48× slower) | 10.2 µs (3.97× slower) | — |
| Doublets United Volatile Uncached | 106 µs (77.5× slower) | 10.2 µs (18.3× slower) | 12.1 µs (6.26× slower) | 11.1 µs (4.32× slower) | — |
| Doublets United NonVolatile Cached | 9.55 µs (2.8× slower) | 1.61 µs (2.55× slower) | 3.07 µs (1.15× faster) | 11.3 µs (1.13× slower) | 640.0 MiB |
| Doublets United NonVolatile Uncached | 120 µs (35.1× slower) | 10.4 µs (16.5× slower) | 12.3 µs (3.48× slower) | 11.5 µs (1.15× slower) | 640.0 MiB |
| Doublets Split Volatile Cached | 5.11 µs (3.72× slower) | 895 ns (1.6× slower) | 893 ns (2.16× faster) | 6.51 µs (2.54× slower) | — |
| Doublets Split Volatile Uncached | 49.7 µs (36.2× slower) | 14.1 µs (25.3× slower) | 14.6 µs (7.56× slower) | 6.78 µs (2.65× slower) | — |
| Doublets Split NonVolatile Cached | 7.74 µs (2.27× slower) | 888 ns (1.41× slower) | 955 ns (3.7× faster) | 7.02 µs (1.42× faster) | 800.0 MiB |
| Doublets Split NonVolatile Uncached | 56.6 µs (16.6× slower) | 15.1 µs (23.9× slower) | 15 µs (4.26× slower) | 7.47 µs (1.33× faster) | 800.0 MiB |

![Rust doublets vs SQLite, 64 bit, objects](docs/benchmarks/objects-rust-64.png)

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.05 µs | 4.19 µs | 5.58 µs | 2.03 µs | — |
| SQLite File | 9.45 µs | 4.19 µs | 5.99 µs | 10.4 µs | 78.3 MiB |
| SystemDataSQLite Memory | 4.76 µs (1.06× faster) | 4 µs (≈ same) | 5.62 µs (≈ same) | 2.25 µs (1.11× slower) | — |
| SystemDataSQLite File | 9.18 µs (≈ same) | 4.04 µs (≈ same) | 6.04 µs (≈ same) | 9.8 µs (1.06× faster) | 78.3 MiB |
| Doublets United Volatile Cached | 16.6 µs (3.28× slower) | 10.8 µs (2.58× slower) | 5.21 µs (1.07× faster) | 8.98 µs (4.43× slower) | — |
| Doublets United Volatile Uncached | 154 µs (30.4× slower) | 300 µs (71.6× slower) | 302 µs (54.1× slower) | 8.61 µs (4.25× slower) | — |
| Doublets United NonVolatile Cached | 16.8 µs (1.77× slower) | 10.8 µs (2.57× slower) | 5.31 µs (1.13× faster) | 9.28 µs (1.12× faster) | 32.0 MiB |
| Doublets United NonVolatile Uncached | 158 µs (16.8× slower) | 300 µs (71.5× slower) | 302 µs (50.5× slower) | 9.97 µs (≈ same) | 32.0 MiB |
| Doublets Split Volatile Cached | 10.6 µs (2.1× slower) | 12.6 µs (3.02× slower) | 5.4 µs (≈ same) | 5.33 µs (2.63× slower) | — |
| Doublets Split Volatile Uncached | 113 µs (22.4× slower) | 408 µs (97.4× slower) | 410 µs (73.5× slower) | 5.2 µs (2.57× slower) | — |
| Doublets Split NonVolatile Cached | 11.2 µs (1.19× slower) | 12.7 µs (3.03× slower) | 5.51 µs (1.09× faster) | 5.59 µs (1.86× faster) | 40.0 MiB |
| Doublets Split NonVolatile Uncached | 115 µs (12.1× slower) | 408 µs (97.4× slower) | 410 µs (68.5× slower) | 5.73 µs (1.81× faster) | 40.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.36 µs | 4.05 µs | 5.81 µs | 2.97 µs | — |
| SQLite File | 7.44 µs | 4.15 µs | 7.58 µs | 10.8 µs | 783.2 MiB |
| SystemDataSQLite Memory | 5.09 µs (1.05× faster) | 3.98 µs (≈ same) | 6.15 µs (1.06× slower) | 2.86 µs (≈ same) | — |
| SystemDataSQLite File | 7.53 µs (≈ same) | 4.08 µs (≈ same) | 7.91 µs (≈ same) | 10.7 µs (≈ same) | 783.2 MiB |
| Doublets United Volatile Cached | 21.7 µs (4.05× slower) | 12.3 µs (3.03× slower) | 6.44 µs (1.11× slower) | 15.2 µs (5.13× slower) | — |
| Doublets United Volatile Uncached | 216 µs (40.4× slower) | 340 µs (83.8× slower) | 344 µs (59.3× slower) | 14.7 µs (4.95× slower) | — |
| Doublets United NonVolatile Cached | 24.9 µs (3.35× slower) | 12.3 µs (2.96× slower) | 6.52 µs (1.16× faster) | 15.8 µs (1.46× slower) | 320.0 MiB |
| Doublets United NonVolatile Uncached | 229 µs (30.7× slower) | 343 µs (82.6× slower) | 340 µs (44.8× slower) | 15.1 µs (1.4× slower) | 320.0 MiB |
| Doublets Split Volatile Cached | 12.2 µs (2.28× slower) | 12.2 µs (3.02× slower) | 6.13 µs (1.06× slower) | 10.1 µs (3.4× slower) | — |
| Doublets Split Volatile Uncached | 107 µs (20.1× slower) | 368 µs (90.8× slower) | 370 µs (63.8× slower) | 10.5 µs (3.54× slower) | — |
| Doublets Split NonVolatile Cached | 16.2 µs (2.17× slower) | 12.3 µs (2.95× slower) | 6.46 µs (1.17× faster) | 11.5 µs (1.06× slower) | 400.0 MiB |
| Doublets Split NonVolatile Uncached | 118 µs (15.9× slower) | 364 µs (87.8× slower) | 369 µs (48.7× slower) | 11.6 µs (1.08× slower) | 400.0 MiB |

![C# doublets vs SQLite, 32 bit, objects](docs/benchmarks/objects-csharp-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.75 µs | 4.05 µs | 5.59 µs | 1.97 µs | — |
| SQLite File | 9.36 µs | 4.09 µs | 5.91 µs | 9.32 µs | 78.3 MiB |
| SystemDataSQLite Memory | 4.66 µs (≈ same) | 3.89 µs (≈ same) | 5.47 µs (≈ same) | 2.12 µs (1.08× slower) | — |
| SystemDataSQLite File | 9.25 µs (≈ same) | 3.98 µs (≈ same) | 5.92 µs (≈ same) | 9.19 µs (≈ same) | 78.3 MiB |
| Doublets United Volatile Cached | 18.9 µs (3.97× slower) | 11.2 µs (2.76× slower) | 5.82 µs (≈ same) | 13.2 µs (6.71× slower) | — |
| Doublets United Volatile Uncached | 160 µs (33.7× slower) | 295 µs (72.9× slower) | 300 µs (53.6× slower) | 12.6 µs (6.4× slower) | — |
| Doublets United NonVolatile Cached | 19.4 µs (2.08× slower) | 11.2 µs (2.74× slower) | 5.91 µs (≈ same) | 13.1 µs (1.41× slower) | 64.0 MiB |
| Doublets United NonVolatile Uncached | 169 µs (18.1× slower) | 289 µs (70.7× slower) | 292 µs (49.4× slower) | 13.5 µs (1.45× slower) | 64.0 MiB |
| Doublets Split Volatile Cached | 10.6 µs (2.22× slower) | 12.9 µs (3.2× slower) | 5.47 µs (≈ same) | 5.79 µs (2.94× slower) | — |
| Doublets Split Volatile Uncached | 117 µs (24.6× slower) | 398 µs (98.4× slower) | 401 µs (71.8× slower) | 5.8 µs (2.95× slower) | — |
| Doublets Split NonVolatile Cached | 11.4 µs (1.21× slower) | 13 µs (3.18× slower) | 5.66 µs (≈ same) | 6.21 µs (1.5× faster) | 80.0 MiB |
| Doublets Split NonVolatile Uncached | 119 µs (12.7× slower) | 398 µs (97.5× slower) | 402 µs (68× slower) | 6.66 µs (1.4× faster) | 80.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37297329698) on 2026-10-05._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.66 µs | 3.57 µs | 5.32 µs | 2.84 µs | — |
| SQLite File | 9 µs | 3.62 µs | 6.93 µs | 24.6 µs | 783.2 MiB |
| SystemDataSQLite Memory | 4.2 µs (1.11× faster) | 3.45 µs (≈ same) | 5.52 µs (≈ same) | 2.73 µs (≈ same) | — |
| SystemDataSQLite File | 8.91 µs (≈ same) | 3.5 µs (≈ same) | 7.32 µs (1.06× slower) | 20.2 µs (1.22× faster) | 783.2 MiB |
| Doublets United Volatile Cached | 19.6 µs (4.2× slower) | 9.66 µs (2.7× slower) | 6.45 µs (1.21× slower) | 17.2 µs (6.05× slower) | — |
| Doublets United Volatile Uncached | 195 µs (41.9× slower) | 255 µs (71.5× slower) | 256 µs (48.2× slower) | 16.7 µs (5.88× slower) | — |
| Doublets United NonVolatile Cached | 35.8 µs (3.98× slower) | 9.77 µs (2.7× slower) | 6.66 µs (≈ same) | 18.3 µs (1.34× faster) | 640.0 MiB |
| Doublets United NonVolatile Uncached | 223 µs (24.8× slower) | 250 µs (69.1× slower) | 254 µs (36.6× slower) | 19.4 µs (1.27× faster) | 640.0 MiB |
| Doublets Split Volatile Cached | 11.2 µs (2.41× slower) | 11 µs (3.08× slower) | 7.33 µs (1.38× slower) | 11.9 µs (4.19× slower) | — |
| Doublets Split Volatile Uncached | 96.1 µs (20.6× slower) | 336 µs (94.1× slower) | 341 µs (64.1× slower) | 12.1 µs (4.26× slower) | — |
| Doublets Split NonVolatile Cached | 27.5 µs (3.06× slower) | 11.3 µs (3.13× slower) | 7.89 µs (1.14× slower) | 14.7 µs (1.68× faster) | 800.0 MiB |
| Doublets Split NonVolatile Uncached | 126 µs (14× slower) | 325 µs (89.7× slower) | 323 µs (46.6× slower) | 12.6 µs (1.96× faster) | 800.0 MiB |

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
