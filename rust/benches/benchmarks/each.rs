//! Link enumeration and restriction benchmarks.

use criterion::Criterion;
use sqlite_vs_doublets::{
    fill_background, fill_benchmarked, Links, BACKGROUND_LINK_COUNT, BENCHMARK_LINK_COUNT,
};

macro_rules! query_benchmark {
    ($function:ident, $group:literal, $query:expr) => {
        pub fn $function(criterion: &mut Criterion) {
            benchmark_operation!(
                criterion,
                $group,
                *BENCHMARK_LINK_COUNT,
                |storage| {
                    let background = fill_background(storage, *BACKGROUND_LINK_COUNT);
                    fill_benchmarked(storage, &background, *BENCHMARK_LINK_COUNT)
                },
                $query,
            );
        }
    };
}

query_benchmark!(all, "query_all", |storage, _created| {
    criterion::black_box(storage.query_all());
});

query_benchmark!(identity, "query_by_id", |storage, created| {
    for link in created {
        criterion::black_box(storage.query_by_id(link.id));
    }
});

query_benchmark!(concrete, "query_by_source_target", |storage, created| {
    for link in created {
        criterion::black_box(storage.query_by_source_target(link.source, link.target));
    }
});

query_benchmark!(outgoing, "query_by_source", |storage, created| {
    for link in created {
        criterion::black_box(storage.query_by_source(link.source));
    }
});

query_benchmark!(incoming, "query_by_target", |storage, created| {
    for link in created {
        criterion::black_box(storage.query_by_target(link.target));
    }
});
