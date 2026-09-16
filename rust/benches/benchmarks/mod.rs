//! Shared infrastructure of the benchmark suite.
//!
//! Every operation is measured on the same six subjects, so the modules below
//! only describe *what* is prepared and *what* is measured, and the
//! [`benchmark_operation`] macro takes care of running it everywhere:
//!
//! | Subject                       | Database | Memory        |
//! |-------------------------------|----------|---------------|
//! | `SQLite_Memory`               | SQLite   | volatile      |
//! | `SQLite_File`                 | SQLite   | non-volatile  |
//! | `Doublets_United_Volatile`    | Doublets | volatile      |
//! | `Doublets_United_NonVolatile` | Doublets | non-volatile  |
//! | `Doublets_Split_Volatile`     | Doublets | volatile      |
//! | `Doublets_Split_NonVolatile`  | Doublets | non-volatile  |
//!
//! The data of an iteration is prepared *outside* of the measured region with
//! [`Criterion::iter_custom`](criterion::Bencher::iter_custom), so that the
//! reported time is the time of the operation itself and not the time of
//! filling the storage.

use criterion::{Bencher, Criterion};
use sqlite_vs_doublets::{Benched, Links, Objects};
use std::{
    env,
    ops::DerefMut,
    time::{Duration, Instant},
};

/// Prepares the storage of every iteration and measures `operation` on it.
///
/// `prepare` runs on an empty (forked) storage and its result is handed to
/// `operation`, which is the only part of the iteration that is timed. Dropping
/// the fork empties the storage again for the next iteration.
pub fn measure<B, P, S, O>(bencher: &mut Bencher<'_>, mut prepare: P, mut operation: O)
where
    B: Benched<Builder = ()> + DerefMut,
    B::Target: Links + Objects + Sized,
    P: FnMut(&mut B::Target) -> S,
    O: FnMut(&mut B::Target, S),
{
    let mut benched = B::setup(());
    bencher.iter_custom(|iterations| {
        let mut elapsed = Duration::ZERO;
        for _ in 0..iterations {
            let mut fork = benched.fork();
            let storage = &mut **fork;
            let prepared = prepare(storage);

            let start = Instant::now();
            operation(storage, prepared);
            elapsed += start.elapsed();
        }
        elapsed
    });
}

/// Measures one operation on one subject.
macro_rules! benchmark_subject {
    ($group:expr, $count:expr, $name:literal, $subject:ty, $prepare:expr, $operation:expr) => {
        $group.bench_with_input(
            criterion::BenchmarkId::new($name, $count),
            &$count,
            |bencher, _| {
                crate::benchmarks::measure::<$subject, _, _, _>(bencher, $prepare, $operation)
            },
        );
    };
}

/// Measures one operation on every subject of the comparison.
///
/// `$prepare` and `$operation` are repeated for each subject, so that both
/// closures are inferred against the storage type of that subject.
macro_rules! benchmark_operation {
    ($criterion:expr, $name:literal, $count:expr, $prepare:expr, $operation:expr $(,)?) => {{
        let count = $count;
        let mut group = $criterion.benchmark_group($name);
        group.throughput(criterion::Throughput::Elements(count as u64));
        benchmark_subject!(
            group,
            count,
            "SQLite_Memory",
            sqlite_vs_doublets::benched::SqliteMemoryBenched,
            $prepare,
            $operation
        );
        benchmark_subject!(
            group,
            count,
            "SQLite_File",
            sqlite_vs_doublets::benched::SqliteFileBenched,
            $prepare,
            $operation
        );
        benchmark_subject!(
            group,
            count,
            "Doublets_United_Volatile",
            sqlite_vs_doublets::benched::DoubletsUnitedVolatileBenched,
            $prepare,
            $operation
        );
        benchmark_subject!(
            group,
            count,
            "Doublets_United_NonVolatile",
            sqlite_vs_doublets::benched::DoubletsUnitedNonVolatileBenched,
            $prepare,
            $operation
        );
        benchmark_subject!(
            group,
            count,
            "Doublets_Split_Volatile",
            sqlite_vs_doublets::benched::DoubletsSplitVolatileBenched,
            $prepare,
            $operation
        );
        benchmark_subject!(
            group,
            count,
            "Doublets_Split_NonVolatile",
            sqlite_vs_doublets::benched::DoubletsSplitNonVolatileBenched,
            $prepare,
            $operation
        );
        group.finish();
    }};
}

pub mod each;
pub mod links;
pub mod objects;

fn env_value<T: std::str::FromStr>(name: &str, default: T) -> T {
    env::var(name)
        .ok()
        .and_then(|value| value.parse().ok())
        .unwrap_or(default)
}

/// Criterion configuration of the suite.
///
/// The defaults keep a pull request run short; the full run of the `main`
/// branch raises them through the environment.
pub fn configure_criterion() -> Criterion {
    Criterion::default()
        .sample_size(env_value("BENCHMARK_SAMPLE_SIZE", 10usize))
        .measurement_time(Duration::from_secs(env_value(
            "BENCHMARK_MEASUREMENT_SECONDS",
            1u64,
        )))
        .warm_up_time(Duration::from_millis(env_value(
            "BENCHMARK_WARM_UP_MILLISECONDS",
            500u64,
        )))
}
