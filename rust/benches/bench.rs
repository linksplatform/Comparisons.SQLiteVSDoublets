//! Benchmark suite comparing SQLite vs Doublets performance
//!
//! This benchmark measures basic CRUD operations with links on both storage systems.

use criterion::{criterion_group, criterion_main, Criterion, Throughput};
use sqlite_vs_doublets::{
    benched::{DoubletsSplitVolatileBenched, DoubletsUnitedVolatileBenched, SqliteMemoryBenched},
    Benched, Links, BACKGROUND_LINK_COUNT, BENCHMARK_LINK_COUNT,
};
use std::time::Duration;

/// Run create operations benchmark
fn bench_create(c: &mut Criterion) {
    let mut group = c.benchmark_group("create");
    let link_count = *BENCHMARK_LINK_COUNT;
    group.throughput(Throughput::Elements(link_count as u64));

    group.bench_function("SQLite_Memory", |b| {
        let mut benched = SqliteMemoryBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            // Create background links
            for _ in 0..*BACKGROUND_LINK_COUNT {
                fork.create_point();
            }
            // Benchmark operation
            for _ in 0..link_count {
                fork.create_point();
            }
        });
    });

    group.bench_function("Doublets_United_Volatile", |b| {
        let mut benched = DoubletsUnitedVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            for _ in 0..*BACKGROUND_LINK_COUNT {
                fork.create_point();
            }
            for _ in 0..link_count {
                fork.create_point();
            }
        });
    });

    group.bench_function("Doublets_Split_Volatile", |b| {
        let mut benched = DoubletsSplitVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            for _ in 0..*BACKGROUND_LINK_COUNT {
                fork.create_point();
            }
            for _ in 0..link_count {
                fork.create_point();
            }
        });
    });

    group.finish();
}

/// Run delete operations benchmark
fn bench_delete(c: &mut Criterion) {
    let mut group = c.benchmark_group("delete");
    let link_count = *BENCHMARK_LINK_COUNT;
    group.throughput(Throughput::Elements(link_count as u64));

    group.bench_function("SQLite_Memory", |b| {
        let mut benched = SqliteMemoryBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            // Create links to delete
            let mut ids = Vec::with_capacity(link_count);
            for _ in 0..link_count {
                ids.push(fork.create_point());
            }
            // Benchmark delete
            for id in ids {
                fork.delete(id);
            }
        });
    });

    group.bench_function("Doublets_United_Volatile", |b| {
        let mut benched = DoubletsUnitedVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            let mut ids = Vec::with_capacity(link_count);
            for _ in 0..link_count {
                ids.push(fork.create_point());
            }
            for id in ids {
                fork.delete(id);
            }
        });
    });

    group.bench_function("Doublets_Split_Volatile", |b| {
        let mut benched = DoubletsSplitVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            let mut ids = Vec::with_capacity(link_count);
            for _ in 0..link_count {
                ids.push(fork.create_point());
            }
            for id in ids {
                fork.delete(id);
            }
        });
    });

    group.finish();
}

/// Run update operations benchmark
fn bench_update(c: &mut Criterion) {
    let mut group = c.benchmark_group("update");
    let link_count = *BENCHMARK_LINK_COUNT;
    group.throughput(Throughput::Elements(link_count as u64));

    group.bench_function("SQLite_Memory", |b| {
        let mut benched = SqliteMemoryBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            // Create links to update
            let mut ids = Vec::with_capacity(link_count);
            for _ in 0..link_count {
                ids.push(fork.create_point());
            }
            // Benchmark update
            for (i, id) in ids.iter().enumerate() {
                fork.update(*id, (i + 1) as u64, (i + 2) as u64);
            }
        });
    });

    group.bench_function("Doublets_United_Volatile", |b| {
        let mut benched = DoubletsUnitedVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            let mut ids = Vec::with_capacity(link_count);
            for _ in 0..link_count {
                ids.push(fork.create_point());
            }
            for (i, id) in ids.iter().enumerate() {
                fork.update(*id, (i + 1) as u64, (i + 2) as u64);
            }
        });
    });

    group.bench_function("Doublets_Split_Volatile", |b| {
        let mut benched = DoubletsSplitVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            let mut ids = Vec::with_capacity(link_count);
            for _ in 0..link_count {
                ids.push(fork.create_point());
            }
            for (i, id) in ids.iter().enumerate() {
                fork.update(*id, (i + 1) as u64, (i + 2) as u64);
            }
        });
    });

    group.finish();
}

/// Run query all benchmark
fn bench_query_all(c: &mut Criterion) {
    let mut group = c.benchmark_group("query_all");
    let link_count = *BENCHMARK_LINK_COUNT;
    group.throughput(Throughput::Elements(link_count as u64));

    group.bench_function("SQLite_Memory", |b| {
        let mut benched = SqliteMemoryBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            // Create links to query
            for _ in 0..link_count {
                fork.create_point();
            }
            // Benchmark query
            let _ = fork.query_all();
        });
    });

    group.bench_function("Doublets_United_Volatile", |b| {
        let mut benched = DoubletsUnitedVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            for _ in 0..link_count {
                fork.create_point();
            }
            let _ = fork.query_all();
        });
    });

    group.bench_function("Doublets_Split_Volatile", |b| {
        let mut benched = DoubletsSplitVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            for _ in 0..link_count {
                fork.create_point();
            }
            let _ = fork.query_all();
        });
    });

    group.finish();
}

/// Run query by ID benchmark
fn bench_query_by_id(c: &mut Criterion) {
    let mut group = c.benchmark_group("query_by_id");
    let link_count = *BENCHMARK_LINK_COUNT;
    group.throughput(Throughput::Elements(link_count as u64));

    group.bench_function("SQLite_Memory", |b| {
        let mut benched = SqliteMemoryBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            // Create links
            let mut ids = Vec::with_capacity(link_count);
            for _ in 0..link_count {
                ids.push(fork.create_point());
            }
            // Benchmark query
            for id in &ids {
                let _ = fork.query_by_id(*id);
            }
        });
    });

    group.bench_function("Doublets_United_Volatile", |b| {
        let mut benched = DoubletsUnitedVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            let mut ids = Vec::with_capacity(link_count);
            for _ in 0..link_count {
                ids.push(fork.create_point());
            }
            for id in &ids {
                let _ = fork.query_by_id(*id);
            }
        });
    });

    group.bench_function("Doublets_Split_Volatile", |b| {
        let mut benched = DoubletsSplitVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            let mut ids = Vec::with_capacity(link_count);
            for _ in 0..link_count {
                ids.push(fork.create_point());
            }
            for id in &ids {
                let _ = fork.query_by_id(*id);
            }
        });
    });

    group.finish();
}

/// Run query by source benchmark
fn bench_query_by_source(c: &mut Criterion) {
    let mut group = c.benchmark_group("query_by_source");
    let link_count = *BENCHMARK_LINK_COUNT;
    group.throughput(Throughput::Elements(link_count as u64));

    group.bench_function("SQLite_Memory", |b| {
        let mut benched = SqliteMemoryBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            // Create links with various sources
            for i in 0..link_count {
                let id = fork.create_point();
                fork.update(id, (i % 10 + 1) as u64, id);
            }
            // Benchmark query
            for i in 1..=10 {
                let _ = fork.query_by_source(i as u64);
            }
        });
    });

    group.bench_function("Doublets_United_Volatile", |b| {
        let mut benched = DoubletsUnitedVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            for i in 0..link_count {
                let id = fork.create_point();
                fork.update(id, (i % 10 + 1) as u64, id);
            }
            for i in 1..=10 {
                let _ = fork.query_by_source(i as u64);
            }
        });
    });

    group.bench_function("Doublets_Split_Volatile", |b| {
        let mut benched = DoubletsSplitVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            for i in 0..link_count {
                let id = fork.create_point();
                fork.update(id, (i % 10 + 1) as u64, id);
            }
            for i in 1..=10 {
                let _ = fork.query_by_source(i as u64);
            }
        });
    });

    group.finish();
}

/// Run query by target benchmark
fn bench_query_by_target(c: &mut Criterion) {
    let mut group = c.benchmark_group("query_by_target");
    let link_count = *BENCHMARK_LINK_COUNT;
    group.throughput(Throughput::Elements(link_count as u64));

    group.bench_function("SQLite_Memory", |b| {
        let mut benched = SqliteMemoryBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            // Create links with various targets
            for i in 0..link_count {
                let id = fork.create_point();
                fork.update(id, id, (i % 10 + 1) as u64);
            }
            // Benchmark query
            for i in 1..=10 {
                let _ = fork.query_by_target(i as u64);
            }
        });
    });

    group.bench_function("Doublets_United_Volatile", |b| {
        let mut benched = DoubletsUnitedVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            for i in 0..link_count {
                let id = fork.create_point();
                fork.update(id, id, (i % 10 + 1) as u64);
            }
            for i in 1..=10 {
                let _ = fork.query_by_target(i as u64);
            }
        });
    });

    group.bench_function("Doublets_Split_Volatile", |b| {
        let mut benched = DoubletsSplitVolatileBenched::setup(());
        b.iter(|| {
            let mut fork = benched.fork();
            for i in 0..link_count {
                let id = fork.create_point();
                fork.update(id, id, (i % 10 + 1) as u64);
            }
            for i in 1..=10 {
                let _ = fork.query_by_target(i as u64);
            }
        });
    });

    group.finish();
}

/// Configure criterion for faster CI runs
fn configure_criterion() -> Criterion {
    Criterion::default()
        .sample_size(10)
        .measurement_time(Duration::from_secs(1))
        .warm_up_time(Duration::from_millis(500))
}

criterion_group! {
    name = benches;
    config = configure_criterion();
    targets = bench_create,
              bench_delete,
              bench_update,
              bench_query_all,
              bench_query_by_id,
              bench_query_by_source,
              bench_query_by_target
}

criterion_main!(benches);
