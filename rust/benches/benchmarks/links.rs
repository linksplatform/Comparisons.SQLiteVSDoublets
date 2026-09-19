//! Create, update and delete benchmarks.

use criterion::Criterion;
use sqlite_vs_doublets::{
    fill_background, fill_benchmarked, Links, BACKGROUND_LINK_COUNT, BENCHMARK_LINK_COUNT,
};

pub fn create(criterion: &mut Criterion) {
    benchmark_operation!(
        criterion,
        "create",
        *BENCHMARK_LINK_COUNT,
        |storage| fill_background(storage, *BACKGROUND_LINK_COUNT),
        |storage, background| {
            let created = fill_benchmarked(storage, &background, *BENCHMARK_LINK_COUNT);
            criterion::black_box(created);
        },
    );
}

pub fn update(criterion: &mut Criterion) {
    benchmark_operation!(
        criterion,
        "update",
        *BENCHMARK_LINK_COUNT,
        |storage| {
            let background = fill_background(storage, *BACKGROUND_LINK_COUNT);
            let created = fill_benchmarked(storage, &background, *BENCHMARK_LINK_COUNT);
            let update_sources = fill_background(storage, *BENCHMARK_LINK_COUNT);
            (background, created, update_sources)
        },
        |storage, (background, created, update_sources)| {
            for (index, link) in created.into_iter().enumerate() {
                // A dedicated source makes the new doublet unique. Updating to
                // an existing pair is not a valid Doublets operation.
                storage.update(link.id, update_sources[index], background[0]);
            }
        },
    );
}

pub fn delete(criterion: &mut Criterion) {
    benchmark_operation!(
        criterion,
        "delete",
        *BENCHMARK_LINK_COUNT,
        |storage| {
            let background = fill_background(storage, *BACKGROUND_LINK_COUNT);
            fill_benchmarked(storage, &background, *BENCHMARK_LINK_COUNT)
        },
        |storage, created| {
            for link in created {
                storage.delete(link.id);
            }
        },
    );
}
