_Generated 2026-09-19 18:46 UTC for Rust by [GitHub Actions run 35459522519](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/35459522519) — 1000 benchmarked links, 3000 background links, 1000 objects._

| Operation           | Doublets United Volatile | Doublets United NonVolatile | Doublets Split Volatile | Doublets Split NonVolatile | SQLite Memory | SQLite File |
|---------------------|--------------------------|-----------------------------|-------------------------|----------------------------|---------------|-------------|
| Create              | 141747 (40.7x faster)    | 143659 (40.2x faster)       | 69524 (83.0x faster)    | 69702 (82.8x faster)       | 5772002       | 993908545   |
| Update              | 283809 (29.6x faster)    | 277912 (30.2x faster)       | 63882 (131.3x faster)   | 72899 (115.1x faster)      | 8390541       | 1067410860  |
| Delete              | 168149 (26.8x faster)    | 166496 (27.1x faster)       | 77711 (58.0x faster)    | 78217 (57.6x faster)       | 4509130       | 977528491   |
| Each All            | 31671 (15.9x faster)     | 31638 (15.9x faster)        | 34426 (14.6x faster)    | 34487 (14.6x faster)       | 504031        | 595624      |
| Each Identity       | 2132 (1269.3x faster)    | 2136 (1266.9x faster)       | 2130 (1270.5x faster)   | 2127 (1272.3x faster)      | 2706180       | 10678850    |
| Each Concrete       | 124784 (34.7x faster)    | 120544 (36.0x faster)       | 45647 (95.0x faster)    | 43793 (99.0x faster)       | 4335748       | 12668554    |
| Each Outgoing       | 210057 (18.1x faster)    | 208064 (18.2x faster)       | 44112 (86.0x faster)    | 45302 (83.7x faster)       | 3792959       | 12033519    |
| Each Incoming       | 212266 (19.3x faster)    | 210393 (19.4x faster)       | 50873 (80.4x faster)    | 46396 (88.1x faster)       | 4089215       | 12477405    |
| Objects Create List | 8759735 (10.5x slower)   | 8759735 (10.5x slower)      | 5388349 (6.5x slower)   | 5436404 (6.5x slower)      | 832974        | 2098461     |
| Objects Read List   | 5889568 (18.5x slower)   | 5585959 (17.6x slower)      | 5393697 (17.0x slower)  | 4889083 (15.4x slower)     | 317868        | 343282      |
| Objects Delete List | 2709133 (75.1x slower)   | 2667861 (74.0x slower)      | 1083231 (30.0x slower)  | 1106546 (30.7x slower)     | 36069         | 992614      |

![Rust benchmark comparison](docs/benchmarks/bench_rust.png)

![Rust benchmark comparison, logarithmic scale](docs/benchmarks/bench_rust_log_scale.png)
