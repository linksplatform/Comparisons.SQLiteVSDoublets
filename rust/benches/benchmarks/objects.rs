//! Object-like (blog post) benchmarks.

use criterion::Criterion;
use sqlite_vs_doublets::{generate_posts, Objects, BENCHMARK_OBJECT_COUNT};

pub fn create(criterion: &mut Criterion) {
    benchmark_operation!(
        criterion,
        "objects_create",
        *BENCHMARK_OBJECT_COUNT,
        |_storage| generate_posts(*BENCHMARK_OBJECT_COUNT),
        |storage, posts| {
            criterion::black_box(storage.create_posts(&posts));
        },
    );
}

pub fn read(criterion: &mut Criterion) {
    benchmark_operation!(
        criterion,
        "objects_read",
        *BENCHMARK_OBJECT_COUNT,
        |storage| {
            let posts = generate_posts(*BENCHMARK_OBJECT_COUNT);
            storage.create_posts(&posts);
        },
        |storage, _prepared| {
            criterion::black_box(storage.read_posts());
        },
    );
}

pub fn delete(criterion: &mut Criterion) {
    benchmark_operation!(
        criterion,
        "objects_delete",
        *BENCHMARK_OBJECT_COUNT,
        |storage| {
            let posts = generate_posts(*BENCHMARK_OBJECT_COUNT);
            storage.create_posts(&posts);
        },
        |storage, _prepared| storage.delete_posts(),
    );
}
