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
and [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite))
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

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.72 µs | 125 ns | 606 ns | 693 ns | 765 ns | 766 ns | 3.36 µs | 2.17 µs | — |
| SQLite File | 2.09 µs | 128 ns | 621 ns | 727 ns | 782 ns | 790 ns | 12.3 µs | 6.04 µs | 4.4 MiB |
| Doublets United Volatile | 458 ns (3.76× faster) | 1.91 ns (65.7× faster) | 5.72 ns (106× faster) | 194 ns (3.57× faster) | 248 ns (3.09× faster) | 267 ns (2.86× faster) | 1.24 µs (2.72× faster) | 459 ns (4.72× faster) | — |
| Doublets United NonVolatile | 499 ns (4.19× faster) | 1.91 ns (67.3× faster) | 5.88 ns (106× faster) | 214 ns (3.39× faster) | 278 ns (2.81× faster) | 288 ns (2.75× faster) | 1.29 µs (9.52× faster) | 484 ns (12.5× faster) | 32.0 MiB |
| Doublets Split Volatile | 97.1 ns (17.7× faster) | 3.68 ns (34× faster) | 5.56 ns (109× faster) | 58.7 ns (11.8× faster) | 29.4 ns (26× faster) | 30.1 ns (25.5× faster) | 174 ns (19.3× faster) | 607 ns (3.57× faster) | — |
| Doublets Split NonVolatile | 119 ns (17.6× faster) | 3.57 ns (36× faster) | 7.43 ns (83.6× faster) | 61.8 ns (11.8× faster) | 31.2 ns (25.1× faster) | 32 ns (24.7× faster) | 184 ns (66.8× faster) | 649 ns (9.31× faster) | 40.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.39 µs | 124 ns | 874 ns | 983 ns | 997 ns | 1 µs | 6.48 µs | 4.26 µs | — |
| SQLite File | 5.76 µs | 128 ns | 1.96 µs | 1.9 µs | 1.92 µs | 1.91 µs | 19 µs | 10.6 µs | 48.2 MiB |
| Doublets United Volatile | 755 ns (3.16× faster) | 1.84 ns (67.5× faster) | 20.7 ns (42.2× faster) | 417 ns (2.36× faster) | 579 ns (1.72× faster) | 620 ns (1.61× faster) | 2.39 µs (2.71× faster) | 828 ns (5.14× faster) | — |
| Doublets United NonVolatile | 868 ns (6.64× faster) | 2.11 ns (60.5× faster) | 22.7 ns (86.3× faster) | 502 ns (3.79× faster) | 681 ns (2.82× faster) | 689 ns (2.77× faster) | 2.62 µs (7.27× faster) | 914 ns (11.6× faster) | 32.0 MiB |
| Doublets Split Volatile | 162 ns (14.7× faster) | 3.87 ns (32.1× faster) | 23 ns (38.1× faster) | 159 ns (6.2× faster) | 116 ns (8.58× faster) | 112 ns (8.96× faster) | 457 ns (14.2× faster) | 1.2 µs (3.55× faster) | — |
| Doublets Split NonVolatile | 174 ns (33.1× faster) | 3.91 ns (32.7× faster) | 24.9 ns (78.6× faster) | 167 ns (11.4× faster) | 129 ns (14.9× faster) | 124 ns (15.4× faster) | 494 ns (38.6× faster) | 1.42 µs (7.5× faster) | 40.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 3.34 µs | 124 ns | 1.39 µs | 1.47 µs | 1.51 µs | 1.49 µs | 8.53 µs | 5.59 µs | — |
| SQLite File | 9.22 µs | 127 ns | 2.18 µs | 2.57 µs | 2.56 µs | 2.53 µs | 24 µs | 14.5 µs | 501.0 MiB |
| Doublets United Volatile | 2.16 µs (1.55× faster) | 2.26 ns (54.6× faster) | 35 ns (39.7× faster) | 1.26 µs (1.17× faster) | 1.56 µs (≈ same) | 1.61 µs (1.08× slower) | 6.55 µs (1.3× faster) | 2.73 µs (2.05× faster) | — |
| Doublets United NonVolatile | 2.42 µs (3.81× faster) | 2.36 ns (53.5× faster) | 32.6 ns (66.8× faster) | 1.27 µs (2.02× faster) | 1.52 µs (1.68× faster) | 1.56 µs (1.62× faster) | 6.91 µs (3.48× faster) | 2.96 µs (4.89× faster) | 320.0 MiB |
| Doublets Split Volatile | 317 ns (10.5× faster) | 3.88 ns (31.9× faster) | 30.1 ns (46.2× faster) | 240 ns (6.14× faster) | 170 ns (8.84× faster) | 169 ns (8.82× faster) | 752 ns (11.3× faster) | 2.64 µs (2.12× faster) | — |
| Doublets Split NonVolatile | 467 ns (19.8× faster) | 3.94 ns (32.1× faster) | 34.7 ns (62.7× faster) | 251 ns (10.2× faster) | 181 ns (14.1× faster) | 187 ns (13.5× faster) | 781 ns (30.8× faster) | 2.92 µs (4.96× faster) | 400.0 MiB |

![Rust doublets vs SQLite, 32 bit, links](docs/benchmarks/links-rust-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.69 µs | 126 ns | 616 ns | 721 ns | 752 ns | 743 ns | 3.12 µs | 2.01 µs | — |
| SQLite File | 2.02 µs | 130 ns | 639 ns | 762 ns | 789 ns | 788 ns | 10.4 µs | 5.18 µs | 4.4 MiB |
| Doublets United Volatile | 449 ns (3.77× faster) | 1.81 ns (69.8× faster) | 3.2 ns (192× faster) | 179 ns (4.04× faster) | 234 ns (3.21× faster) | 253 ns (2.94× faster) | 1.13 µs (2.75× faster) | 444 ns (4.54× faster) | — |
| Doublets United NonVolatile | 471 ns (4.29× faster) | 1.89 ns (68.9× faster) | 3.52 ns (182× faster) | 198 ns (3.85× faster) | 255 ns (3.1× faster) | 269 ns (2.93× faster) | 1.19 µs (8.73× faster) | 462 ns (11.2× faster) | 64.0 MiB |
| Doublets Split Volatile | 114 ns (14.8× faster) | 3.77 ns (33.5× faster) | 4.12 ns (149× faster) | 56.1 ns (12.9× faster) | 27.6 ns (27.2× faster) | 29.2 ns (25.4× faster) | 164 ns (19× faster) | 591 ns (3.41× faster) | — |
| Doublets Split NonVolatile | 106 ns (19× faster) | 3.74 ns (34.8× faster) | 3.91 ns (164× faster) | 57 ns (13.4× faster) | 28.7 ns (27.5× faster) | 29.9 ns (26.4× faster) | 171 ns (61× faster) | 609 ns (8.49× faster) | 80.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 2.09 µs | 124 ns | 745 ns | 839 ns | 879 ns | 858 ns | 4.77 µs | 3.14 µs | — |
| SQLite File | 5.65 µs | 127 ns | 1.83 µs | 1.82 µs | 1.85 µs | 1.84 µs | 18.4 µs | 10.3 µs | 48.2 MiB |
| Doublets United Volatile | 750 ns (2.79× faster) | 3.05 ns (40.4× faster) | 20.2 ns (36.9× faster) | 385 ns (2.18× faster) | 471 ns (1.87× faster) | 567 ns (1.51× faster) | 2.55 µs (1.87× faster) | 856 ns (3.66× faster) | — |
| Doublets United NonVolatile | 840 ns (6.72× faster) | 3.5 ns (36.2× faster) | 22.7 ns (80.7× faster) | 495 ns (3.67× faster) | 622 ns (2.97× faster) | 642 ns (2.87× faster) | 2.91 µs (6.31× faster) | 1.02 µs (10× faster) | 64.0 MiB |
| Doublets Split Volatile | 184 ns (11.4× faster) | 4.05 ns (30.5× faster) | 21.6 ns (34.5× faster) | 171 ns (4.9× faster) | 130 ns (6.76× faster) | 132 ns (6.5× faster) | 485 ns (9.83× faster) | 1.18 µs (2.65× faster) | — |
| Doublets Split NonVolatile | 190 ns (29.8× faster) | 4.4 ns (28.8× faster) | 23.8 ns (76.9× faster) | 181 ns (10.1× faster) | 141 ns (13.1× faster) | 141 ns (13.1× faster) | 532 ns (34.5× faster) | 1.38 µs (7.43× faster) | 80.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 3.34 µs | 105 ns | 1.62 µs | 1.62 µs | 1.57 µs | 1.5 µs | 9.58 µs | 6.3 µs | — |
| SQLite File | 7.41 µs | 106 ns | 1.81 µs | 2.05 µs | 1.98 µs | 1.97 µs | 18.3 µs | 10.9 µs | 501.0 MiB |
| Doublets United Volatile | 2.38 µs (1.41× faster) | 6.42 ns (16.4× faster) | 22.4 ns (72.3× faster) | 1.3 µs (1.25× faster) | 1.43 µs (1.1× faster) | 2.3 µs (1.53× slower) | 6.59 µs (1.45× faster) | 2.48 µs (2.54× faster) | — |
| Doublets United NonVolatile | 2.72 µs (2.72× faster) | 6.31 ns (16.9× faster) | 22.4 ns (81× faster) | 1.42 µs (1.44× faster) | 1.67 µs (1.19× faster) | 1.65 µs (1.19× faster) | 7.75 µs (2.36× faster) | 3.36 µs (3.25× faster) | 640.0 MiB |
| Doublets Split Volatile | 363 ns (9.19× faster) | 6.9 ns (15.3× faster) | 23.4 ns (69.3× faster) | 246 ns (6.59× faster) | 141 ns (11.1× faster) | 148 ns (10.1× faster) | 728 ns (13.2× faster) | 3.05 µs (2.07× faster) | — |
| Doublets Split NonVolatile | 644 ns (11.5× faster) | 7.01 ns (15.2× faster) | 26.2 ns (69.2× faster) | 240 ns (8.57× faster) | 143 ns (13.9× faster) | 145 ns (13.6× faster) | 709 ns (25.8× faster) | 3.62 µs (3.01× faster) | 800.0 MiB |

![Rust doublets vs SQLite, 64 bit, links](docs/benchmarks/links-rust-64.png)

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 6.27 µs | 380 ns | 1.2 µs | 1.46 µs | 1.51 µs | 1.5 µs | 4.27 µs | 2.84 µs | — |
| SQLite File | 5.75 µs | 385 ns | 1.21 µs | 1.48 µs | 1.52 µs | 1.52 µs | 12.2 µs | 6.22 µs | 4.4 MiB |
| Doublets United Volatile | 1.02 µs (6.12× faster) | 14.9 ns (25.5× faster) | 60.1 ns (20× faster) | 226 ns (6.45× faster) | 350 ns (4.32× faster) | 264 ns (5.67× faster) | 2.03 µs (2.1× faster) | 1.05 µs (2.7× faster) | — |
| Doublets United NonVolatile | 1.08 µs (5.32× faster) | 14.8 ns (26× faster) | 60.5 ns (20× faster) | 258 ns (5.73× faster) | 366 ns (4.17× faster) | 267 ns (5.68× faster) | 2.11 µs (5.76× faster) | 1.06 µs (5.89× faster) | 32.0 MiB |
| Doublets Split Volatile | 192 ns (32.7× faster) | 16 ns (23.7× faster) | 69.6 ns (17.2× faster) | 107 ns (13.7× faster) | 84.5 ns (17.9× faster) | 84.8 ns (17.7× faster) | 218 ns (19.6× faster) | 276 ns (10.3× faster) | — |
| Doublets Split NonVolatile | 241 ns (23.9× faster) | 16.5 ns (23.4× faster) | 69.1 ns (17.5× faster) | 110 ns (13.5× faster) | 86.7 ns (17.6× faster) | 86.4 ns (17.6× faster) | 225 ns (54.1× faster) | 263 ns (23.6× faster) | 40.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, Intel(R) Xeon(R) 6973P-C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 7.05 µs | 428 ns | 1.51 µs | 1.69 µs | 1.76 µs | 1.77 µs | 6.76 µs | 4.12 µs | — |
| SQLite File | 6.89 µs | 385 ns | 1.81 µs | 1.95 µs | 2.02 µs | 2.04 µs | 12.3 µs | 8.07 µs | 48.2 MiB |
| Doublets United Volatile | 1.7 µs (4.16× faster) | 16.4 ns (26.1× faster) | 117 ns (12.9× faster) | 574 ns (2.94× faster) | 691 ns (2.55× faster) | 824 ns (2.15× faster) | 4.3 µs (1.57× faster) | 1.85 µs (2.23× faster) | — |
| Doublets United NonVolatile | 1.85 µs (3.72× faster) | 16.5 ns (23.4× faster) | 123 ns (14.7× faster) | 688 ns (2.83× faster) | 861 ns (2.35× faster) | 982 ns (2.07× faster) | 4.93 µs (2.49× faster) | 2.25 µs (3.59× faster) | 32.0 MiB |
| Doublets Split Volatile | 400 ns (17.6× faster) | 16.8 ns (25.5× faster) | 231 ns (6.54× faster) | 396 ns (4.26× faster) | 324 ns (5.44× faster) | 338 ns (5.24× faster) | 594 ns (11.4× faster) | 674 ns (6.11× faster) | — |
| Doublets Split NonVolatile | 467 ns (14.8× faster) | 17.6 ns (21.9× faster) | 238 ns (7.58× faster) | 439 ns (4.43× faster) | 369 ns (5.49× faster) | 357 ns (5.7× faster) | 618 ns (19.9× faster) | 601 ns (13.4× faster) | 40.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 7.41 µs | 316 ns | 1.86 µs | 2.01 µs | 2.06 µs | 2.06 µs | 9.49 µs | 6.49 µs | — |
| SQLite File | 18.2 µs | 318 ns | 2.72 µs | 3.16 µs | 3.11 µs | 3.1 µs | 24.6 µs | 14.8 µs | 501.0 MiB |
| Doublets United Volatile | 3.01 µs (2.46× faster) | 10 ns (31.6× faster) | 181 ns (10.3× faster) | 1.45 µs (1.39× faster) | 1.67 µs (1.23× faster) | 1.88 µs (1.1× faster) | 8.16 µs (1.16× faster) | 3.52 µs (1.85× faster) | — |
| Doublets United NonVolatile | 3.95 µs (4.61× faster) | 14.1 ns (22.6× faster) | 187 ns (14.5× faster) | 1.44 µs (2.19× faster) | 1.64 µs (1.9× faster) | 1.93 µs (1.61× faster) | 8.99 µs (2.74× faster) | 4.35 µs (3.39× faster) | 320.0 MiB |
| Doublets Split Volatile | 431 ns (17.2× faster) | 10.3 ns (30.8× faster) | 199 ns (9.3× faster) | 475 ns (4.23× faster) | 323 ns (6.38× faster) | 324 ns (6.37× faster) | 795 ns (11.9× faster) | 574 ns (11.3× faster) | — |
| Doublets Split NonVolatile | 1.16 µs (15.7× faster) | 9.1 ns (35× faster) | 211 ns (12.9× faster) | 508 ns (6.21× faster) | 354 ns (8.79× faster) | 357 ns (8.69× faster) | 1.01 µs (24.5× faster) | 780 ns (18.9× faster) | 400.0 MiB |

![C# doublets vs SQLite, 32 bit, links](docs/benchmarks/links-csharp-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 links

_10 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, Intel(R) Xeon(R) 6973P-C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.63 µs | 355 ns | 882 ns | 1.07 µs | 1.12 µs | 1.13 µs | 3.45 µs | 2.3 µs | — |
| SQLite File | 3.83 µs | 363 ns | 844 ns | 1.03 µs | 1.07 µs | 1.08 µs | 7.17 µs | 4.04 µs | 4.4 MiB |
| Doublets United Volatile | 1.12 µs (4.12× faster) | 19.6 ns (18.1× faster) | 65 ns (13.6× faster) | 223 ns (4.81× faster) | 265 ns (4.23× faster) | 371 ns (3.04× faster) | 2.49 µs (1.39× faster) | 1.24 µs (1.86× faster) | — |
| Doublets United NonVolatile | 1.2 µs (3.19× faster) | 20.8 ns (17.4× faster) | 73 ns (11.6× faster) | 279 ns (3.68× faster) | 360 ns (2.96× faster) | 299 ns (3.61× faster) | 2.57 µs (2.8× faster) | 1.24 µs (3.26× faster) | 64.0 MiB |
| Doublets Split Volatile | 247 ns (18.8× faster) | 15.4 ns (23.1× faster) | 91.2 ns (9.67× faster) | 130 ns (8.23× faster) | 118 ns (9.54× faster) | 113 ns (9.98× faster) | 234 ns (14.8× faster) | 296 ns (7.76× faster) | — |
| Doublets Split NonVolatile | 278 ns (13.8× faster) | 14 ns (26× faster) | 86.6 ns (9.74× faster) | 128 ns (8.03× faster) | 118 ns (9.03× faster) | 116 ns (9.32× faster) | 236 ns (30.4× faster) | 266 ns (15.2× faster) | 80.0 MiB |

##### 1,000,000 links

_3 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 7.39 µs | 386 ns | 1.49 µs | 1.8 µs | 1.83 µs | 1.83 µs | 7.12 µs | 4.86 µs | — |
| SQLite File | 9.97 µs | 386 ns | 2.64 µs | 2.76 µs | 2.84 µs | 2.86 µs | 21 µs | 11.7 µs | 48.2 MiB |
| Doublets United Volatile | 2.15 µs (3.44× faster) | 22.3 ns (17.3× faster) | 162 ns (9.17× faster) | 576 ns (3.12× faster) | 687 ns (2.66× faster) | 918 ns (2× faster) | 5.14 µs (1.39× faster) | 2.32 µs (2.1× faster) | — |
| Doublets United NonVolatile | 2.2 µs (4.54× faster) | 21.6 ns (17.8× faster) | 169 ns (15.6× faster) | 710 ns (3.89× faster) | 826 ns (3.44× faster) | 1.01 µs (2.83× faster) | 5.66 µs (3.7× faster) | 2.58 µs (4.55× faster) | 64.0 MiB |
| Doublets Split Volatile | 366 ns (20.2× faster) | 20.8 ns (18.6× faster) | 220 ns (6.75× faster) | 328 ns (5.49× faster) | 280 ns (6.51× faster) | 282 ns (6.51× faster) | 658 ns (10.8× faster) | 567 ns (8.57× faster) | — |
| Doublets Split NonVolatile | 458 ns (21.8× faster) | 21.2 ns (18.2× faster) | 245 ns (10.8× faster) | 369 ns (7.48× faster) | 317 ns (8.97× faster) | 323 ns (8.84× faster) | 716 ns (29.3× faster) | 580 ns (20.2× faster) | 80.0 MiB |

##### 10,000,000 links

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Search (from, to) | Read by from | Read by to | Update | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 8.4 µs | 384 ns | 1.89 µs | 2.21 µs | 2.18 µs | 2.16 µs | 9.45 µs | 6.63 µs | — |
| SQLite File | 20 µs | 383 ns | 2.88 µs | 3.38 µs | 3.32 µs | 3.32 µs | 25.4 µs | 15.3 µs | 501.0 MiB |
| Doublets United Volatile | 3.04 µs (2.76× faster) | 13.9 ns (27.7× faster) | 180 ns (10.5× faster) | 1.31 µs (1.69× faster) | 1.5 µs (1.45× faster) | 1.75 µs (1.24× faster) | 8.6 µs (1.1× faster) | 3.84 µs (1.72× faster) | — |
| Doublets United NonVolatile | 4.43 µs (4.52× faster) | 21.6 ns (17.7× faster) | 172 ns (16.8× faster) | 1.36 µs (2.49× faster) | 1.55 µs (2.14× faster) | 1.65 µs (2.01× faster) | 9.64 µs (2.63× faster) | 4.78 µs (3.19× faster) | 640.0 MiB |
| Doublets Split Volatile | 458 ns (18.4× faster) | 13 ns (29.5× faster) | 308 ns (6.12× faster) | 445 ns (4.97× faster) | 329 ns (6.64× faster) | 350 ns (6.17× faster) | 908 ns (10.4× faster) | 725 ns (9.14× faster) | — |
| Doublets Split NonVolatile | 1.26 µs (15.8× faster) | 11.9 ns (32.3× faster) | 326 ns (8.84× faster) | 467 ns (7.24× faster) | 332 ns (10× faster) | 345 ns (9.64× faster) | 1.05 µs (24.2× faster) | 875 ns (17.4× faster) | 800.0 MiB |

![C# doublets vs SQLite, 64 bit, links](docs/benchmarks/links-csharp-64.png)

## Doublets vs SQLite as storage for objects

### Rust doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, Intel(R) Xeon(R) 6973P-C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 838 ns | 485 ns | 1.37 µs | 1.49 µs | — |
| SQLite File | 4.96 µs | 476 ns | 1.63 µs | 8.03 µs | 78.3 MiB |
| Doublets United Volatile Cached | 4.58 µs (5.46× slower) | 1.35 µs (2.79× slower) | 2 µs (1.46× slower) | 4.51 µs (3.03× slower) | — |
| Doublets United Volatile Uncached | 95.5 µs (114× slower) | 7.59 µs (15.7× slower) | 8.21 µs (5.99× slower) | 4.52 µs (3.04× slower) | — |
| Doublets United NonVolatile Cached | 4.87 µs (≈ same) | 1.41 µs (2.96× slower) | 2.25 µs (1.38× slower) | 4.82 µs (1.66× faster) | 32.0 MiB |
| Doublets United NonVolatile Uncached | 92.7 µs (18.7× slower) | 7.8 µs (16.4× slower) | 8.54 µs (5.24× slower) | 4.86 µs (1.65× faster) | 32.0 MiB |
| Doublets Split Volatile Cached | 2.89 µs (3.45× slower) | 698 ns (1.44× slower) | 601 ns (2.28× faster) | 2.18 µs (1.47× slower) | — |
| Doublets Split Volatile Uncached | 36.3 µs (43.3× slower) | 9.78 µs (20.2× slower) | 10.1 µs (7.36× slower) | 2.11 µs (1.42× slower) | — |
| Doublets Split NonVolatile Cached | 2.84 µs (1.75× faster) | 641 ns (1.35× slower) | 588 ns (2.77× faster) | 2.33 µs (3.45× faster) | 40.0 MiB |
| Doublets Split NonVolatile Uncached | 35.9 µs (7.23× slower) | 9.33 µs (19.6× slower) | 9.69 µs (5.94× slower) | 2.37 µs (3.39× faster) | 40.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 965 ns | 478 ns | 2.06 µs | 2.7 µs | — |
| SQLite File | 5.93 µs | 470 ns | 2.71 µs | 11.1 µs | 783.2 MiB |
| Doublets United Volatile Cached | 6.88 µs (7.13× slower) | 1.64 µs (3.42× slower) | 3.23 µs (1.56× slower) | 10.7 µs (3.97× slower) | — |
| Doublets United Volatile Uncached | 113 µs (117× slower) | 8.34 µs (17.4× slower) | 10.3 µs (5× slower) | 9.72 µs (3.6× slower) | — |
| Doublets United NonVolatile Cached | 7.85 µs (1.32× slower) | 1.61 µs (3.43× slower) | 3.15 µs (1.16× slower) | 10.4 µs (1.06× faster) | 320.0 MiB |
| Doublets United NonVolatile Uncached | 121 µs (20.3× slower) | 8.46 µs (18× slower) | 10.4 µs (3.83× slower) | 10.4 µs (1.06× faster) | 320.0 MiB |
| Doublets Split Volatile Cached | 4.16 µs (4.32× slower) | 914 ns (1.91× slower) | 1.06 µs (1.95× faster) | 4.99 µs (1.85× slower) | — |
| Doublets Split Volatile Uncached | 39.3 µs (40.7× slower) | 11.2 µs (23.4× slower) | 11.7 µs (5.69× slower) | 4.8 µs (1.78× slower) | — |
| Doublets Split NonVolatile Cached | 5.52 µs (1.08× faster) | 913 ns (1.94× slower) | 1.12 µs (2.42× faster) | 5.7 µs (1.94× faster) | 400.0 MiB |
| Doublets Split NonVolatile Uncached | 41.6 µs (7.02× slower) | 11.1 µs (23.7× slower) | 11.7 µs (4.3× slower) | 5.88 µs (1.88× faster) | 400.0 MiB |

![Rust doublets vs SQLite, 32 bit, objects](docs/benchmarks/objects-rust-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 1.22 µs | 548 ns | 1.44 µs | 1.7 µs | — |
| SQLite File | 2.01 µs | 625 ns | 2.6 µs | 7.27 µs | 78.3 MiB |
| Doublets United Volatile Cached | 5.17 µs (4.22× slower) | 1.46 µs (2.67× slower) | 2.25 µs (1.57× slower) | 6.12 µs (3.6× slower) | — |
| Doublets United Volatile Uncached | 95.7 µs (78.2× slower) | 9.79 µs (17.9× slower) | 10.7 µs (7.47× slower) | 5.33 µs (3.13× slower) | — |
| Doublets United NonVolatile Cached | 5.58 µs (2.78× slower) | 1.45 µs (2.32× slower) | 2.13 µs (1.22× faster) | 5.65 µs (1.29× faster) | 64.0 MiB |
| Doublets United NonVolatile Uncached | 99.7 µs (49.7× slower) | 9.73 µs (15.6× slower) | 11 µs (4.22× slower) | 5.68 µs (1.28× faster) | 64.0 MiB |
| Doublets Split Volatile Cached | 3.39 µs (2.77× slower) | 771 ns (1.41× slower) | 608 ns (2.37× faster) | 2.94 µs (1.73× slower) | — |
| Doublets Split Volatile Uncached | 48.1 µs (39.3× slower) | 14.1 µs (25.7× slower) | 14.5 µs (10.1× slower) | 2.97 µs (1.75× slower) | — |
| Doublets Split NonVolatile Cached | 3.75 µs (1.87× slower) | 801 ns (1.28× slower) | 634 ns (4.09× faster) | 3.68 µs (1.98× faster) | 80.0 MiB |
| Doublets Split NonVolatile Uncached | 47.4 µs (23.6× slower) | 14.1 µs (22.5× slower) | 14.6 µs (5.6× slower) | 3.23 µs (2.25× faster) | 80.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.2, Ubuntu 24.04.5 LTS, INTEL(R) XEON(R) PLATINUM 8573C, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 971 ns | 481 ns | 2.07 µs | 2.66 µs | — |
| SQLite File | 5.28 µs | 466 ns | 2.69 µs | 10.8 µs | 783.2 MiB |
| Doublets United Volatile Cached | 7.42 µs (7.64× slower) | 1.77 µs (3.69× slower) | 3.27 µs (1.58× slower) | 11 µs (4.13× slower) | — |
| Doublets United Volatile Uncached | 114 µs (117× slower) | 8.55 µs (17.8× slower) | 10.8 µs (5.19× slower) | 11.8 µs (4.44× slower) | — |
| Doublets United NonVolatile Cached | 9.17 µs (1.74× slower) | 1.72 µs (3.69× slower) | 3.45 µs (1.28× slower) | 11.8 µs (1.1× slower) | 640.0 MiB |
| Doublets United NonVolatile Uncached | 124 µs (23.4× slower) | 9.19 µs (19.7× slower) | 11.2 µs (4.17× slower) | 12.3 µs (1.14× slower) | 640.0 MiB |
| Doublets Split Volatile Cached | 4.83 µs (4.98× slower) | 919 ns (1.91× slower) | 1.19 µs (1.74× faster) | 6.82 µs (2.56× slower) | — |
| Doublets Split Volatile Uncached | 43.2 µs (44.5× slower) | 11 µs (22.9× slower) | 11.7 µs (5.65× slower) | 5.93 µs (2.23× slower) | — |
| Doublets Split NonVolatile Cached | 6.98 µs (1.32× slower) | 896 ns (1.92× slower) | 1.23 µs (2.18× faster) | 7.21 µs (1.49× faster) | 800.0 MiB |
| Doublets Split NonVolatile Uncached | 46.4 µs (8.78× slower) | 11.3 µs (24.3× slower) | 12 µs (4.45× slower) | 7.58 µs (1.42× faster) | 800.0 MiB |

![Rust doublets vs SQLite, 64 bit, objects](docs/benchmarks/objects-rust-64.png)

### C# doublets vs SQLite

#### 32 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.16 µs | 1.51 µs | 2.86 µs | 2.09 µs | — |
| SQLite File | 6.4 µs | 1.6 µs | 4.03 µs | 8.14 µs | 78.3 MiB |
| Doublets United Volatile Cached | 16.7 µs (3.23× slower) | 8.6 µs (5.68× slower) | 2.77 µs (≈ same) | 11.7 µs (5.59× slower) | — |
| Doublets United Volatile Uncached | 178 µs (34.5× slower) | 322 µs (213× slower) | 324 µs (113× slower) | 10.7 µs (5.13× slower) | — |
| Doublets United NonVolatile Cached | 17 µs (2.65× slower) | 8.53 µs (5.31× slower) | 2.88 µs (1.4× faster) | 11.6 µs (1.43× slower) | 32.0 MiB |
| Doublets United NonVolatile Uncached | 178 µs (27.8× slower) | 330 µs (205× slower) | 331 µs (82.2× slower) | 12.4 µs (1.52× slower) | 32.0 MiB |
| Doublets Split Volatile Cached | 8.93 µs (1.73× slower) | 9.8 µs (6.48× slower) | 4.17 µs (1.46× slower) | 7.87 µs (3.77× slower) | — |
| Doublets Split Volatile Uncached | 113 µs (21.8× slower) | 389 µs (257× slower) | 392 µs (137× slower) | 7.86 µs (3.76× slower) | — |
| Doublets Split NonVolatile Cached | 9.64 µs (1.51× slower) | 9.74 µs (6.07× slower) | 4.26 µs (1.06× slower) | 8.29 µs (≈ same) | 40.0 MiB |
| Doublets Split NonVolatile Uncached | 110 µs (17.2× slower) | 379 µs (236× slower) | 381 µs (94.6× slower) | 8.03 µs (≈ same) | 40.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 9V74 80-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 4.17 µs | 1.24 µs | 3.03 µs | 2.93 µs | — |
| SQLite File | 8.39 µs | 1.27 µs | 4.81 µs | 16 µs | 783.2 MiB |
| Doublets United Volatile Cached | 16.5 µs (3.96× slower) | 7.17 µs (5.77× slower) | 3.77 µs (1.25× slower) | 14 µs (4.77× slower) | — |
| Doublets United Volatile Uncached | 192 µs (46× slower) | 248 µs (200× slower) | 251 µs (82.9× slower) | 14.1 µs (4.8× slower) | — |
| Doublets United NonVolatile Cached | 23.3 µs (2.77× slower) | 7.07 µs (5.59× slower) | 4.05 µs (1.19× faster) | 14.3 µs (1.12× faster) | 320.0 MiB |
| Doublets United NonVolatile Uncached | 196 µs (23.3× slower) | 250 µs (197× slower) | 251 µs (52.2× slower) | 14.7 µs (1.09× faster) | 320.0 MiB |
| Doublets Split Volatile Cached | 7.97 µs (1.91× slower) | 8.1 µs (6.52× slower) | 3.82 µs (1.26× slower) | 9.2 µs (3.14× slower) | — |
| Doublets Split Volatile Uncached | 87.2 µs (20.9× slower) | 335 µs (270× slower) | 327 µs (108× slower) | 9.22 µs (3.15× slower) | — |
| Doublets Split NonVolatile Cached | 15.6 µs (1.86× slower) | 8.73 µs (6.9× slower) | 3.9 µs (1.23× faster) | 12.9 µs (1.25× faster) | 400.0 MiB |
| Doublets Split NonVolatile Uncached | 98.9 µs (11.8× slower) | 320 µs (253× slower) | 323 µs (67.2× slower) | 10.2 µs (1.57× faster) | 400.0 MiB |

![C# doublets vs SQLite, 32 bit, objects](docs/benchmarks/objects-csharp-32.png)

#### 64 bit address/id space benchmarks

##### 100,000 blog posts

_5 repetitions after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.06 µs | 1.43 µs | 2.73 µs | 1.9 µs | — |
| SQLite File | 6.09 µs | 1.56 µs | 3.81 µs | 7.65 µs | 78.3 MiB |
| Doublets United Volatile Cached | 17.8 µs (3.52× slower) | 9.46 µs (6.62× slower) | 2.83 µs (≈ same) | 12.7 µs (6.7× slower) | — |
| Doublets United Volatile Uncached | 187 µs (36.9× slower) | 346 µs (243× slower) | 362 µs (133× slower) | 12.6 µs (6.65× slower) | — |
| Doublets United NonVolatile Cached | 18.5 µs (3.04× slower) | 9.61 µs (6.16× slower) | 3.09 µs (1.24× faster) | 12.9 µs (1.69× slower) | 64.0 MiB |
| Doublets United NonVolatile Uncached | 183 µs (30.1× slower) | 351 µs (225× slower) | 356 µs (93.3× slower) | 13.2 µs (1.72× slower) | 64.0 MiB |
| Doublets Split Volatile Cached | 8.24 µs (1.63× slower) | 9.57 µs (6.71× slower) | 4.19 µs (1.53× slower) | 7.52 µs (3.96× slower) | — |
| Doublets Split Volatile Uncached | 109 µs (21.5× slower) | 383 µs (268× slower) | 389 µs (142× slower) | 7.54 µs (3.97× slower) | — |
| Doublets Split NonVolatile Cached | 9.47 µs (1.56× slower) | 9.56 µs (6.13× slower) | 4.11 µs (1.08× slower) | 7.7 µs (≈ same) | 80.0 MiB |
| Doublets Split NonVolatile Uncached | 108 µs (17.8× slower) | 377 µs (242× slower) | 385 µs (101× slower) | 8.35 µs (1.09× slower) | 80.0 MiB |

##### 1,000,000 blog posts

_1 repetition after a warm-up, median time per operation. SQLite 3.53.3, Ubuntu 24.04.5 LTS, AMD EPYC 7763 64-Core Processor, 4 cores, 16 GiB, [GitHub Actions run](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37226110636) on 2026-10-04._

| Storage | Create | Read all | Read by id | Delete | File size |
| --- | ---: | ---: | ---: | ---: | ---: |
| SQLite Memory | 5.71 µs | 1.59 µs | 3.41 µs | 3.18 µs | — |
| SQLite File | 7.73 µs | 1.66 µs | 5.17 µs | 11.5 µs | 783.2 MiB |
| Doublets United Volatile Cached | 22.3 µs (3.9× slower) | 9.91 µs (6.22× slower) | 4.25 µs (1.25× slower) | 20.3 µs (6.38× slower) | — |
| Doublets United Volatile Uncached | 232 µs (40.7× slower) | 318 µs (200× slower) | 320 µs (93.9× slower) | 18.2 µs (5.73× slower) | — |
| Doublets United NonVolatile Cached | 29.2 µs (3.78× slower) | 9.25 µs (5.58× slower) | 4.04 µs (1.28× faster) | 18.3 µs (1.59× slower) | 640.0 MiB |
| Doublets United NonVolatile Uncached | 236 µs (30.5× slower) | 315 µs (190× slower) | 319 µs (61.8× slower) | 20.7 µs (1.8× slower) | 640.0 MiB |
| Doublets Split Volatile Cached | 10.6 µs (1.85× slower) | 10.3 µs (6.46× slower) | 4.65 µs (1.36× slower) | 12 µs (3.76× slower) | — |
| Doublets Split Volatile Uncached | 111 µs (19.4× slower) | 387 µs (243× slower) | 405 µs (119× slower) | 13 µs (4.09× slower) | — |
| Doublets Split NonVolatile Cached | 20.4 µs (2.64× slower) | 11 µs (6.61× slower) | 5.27 µs (≈ same) | 15 µs (1.3× slower) | 800.0 MiB |
| Doublets Split NonVolatile Uncached | 132 µs (17.1× slower) | 402 µs (242× slower) | 407 µs (78.7× slower) | 16.6 µs (1.45× slower) | 800.0 MiB |

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
