_Generated 2026-09-19 18:13 UTC for C# by [GitHub Actions run 35459522587](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/35459522587) — 1000 benchmarked links, 3000 background links._

| Operation     | Doublets United Volatile | Doublets United NonVolatile | Doublets Split Volatile | Doublets Split NonVolatile | SQLite Memory | SQLite File |
|---------------|--------------------------|-----------------------------|-------------------------|----------------------------|---------------|-------------|
| Create        | 2414327 (3.7x faster)    | 2512520 (3.5x faster)       | 867200 (10.2x faster)   | 896999 (9.8x faster)       | 8818051       | 2294476668  |
| Update        | 4566053 (2.3x faster)    | 4670899 (2.3x faster)       | 1541871 (7.0x faster)   | 1534119 (7.0x faster)      | 10720700      | 2733072615  |
| Delete        | 484008 (14.3x faster)    | 465524 (14.8x faster)       | 523910 (13.2x faster)   | 521322 (13.3x faster)      | 6907869       | 1682008343  |
| Each All      | 437356 (3.7x faster)     | 470885 (3.4x faster)        | 420518 (3.8x faster)    | 397666 (4.0x faster)       | 2113072       | 1598861     |
| Each Identity | 430962 (11.6x faster)    | 411173 (12.1x faster)       | 403128 (12.4x faster)   | 385531 (13.0x faster)      | 4995738       | 12044311    |
| Each Concrete | 1363130 (5.2x faster)    | 1408600 (5.1x faster)       | 647769 (11.0x faster)   | 917509 (7.8x faster)       | 7134655       | 13622920    |
| Each Outgoing | 1160157 (5.3x faster)    | 1180175 (5.3x faster)       | 862790 (7.2x faster)    | 830070 (7.5x faster)       | 6199124       | 12923172    |
| Each Incoming | 1139559 (5.7x faster)    | 1169325 (5.6x faster)       | 796694 (8.2x faster)    | 776158 (8.4x faster)       | 6533836       | 13510164    |

![C# benchmark comparison](docs/benchmarks/bench_csharp.png)

![C# benchmark comparison, logarithmic scale](docs/benchmarks/bench_csharp_log_scale.png)
