//! SQLite versus Doublets benchmark suite.
//!
//! Preparation is performed by `benchmarks::measure` outside the measured
//! region. Every operation is run against all six storage variants.

#![feature(allocator_api)]

mod benchmarks;

use criterion::{criterion_group, criterion_main};

criterion_group! {
    name = benches;
    config = benchmarks::configure_criterion();
    targets =
        benchmarks::links::create,
        benchmarks::links::update,
        benchmarks::links::delete,
        benchmarks::each::all,
        benchmarks::each::identity,
        benchmarks::each::concrete,
        benchmarks::each::outgoing,
        benchmarks::each::incoming,
        benchmarks::objects::create,
        benchmarks::objects::read,
        benchmarks::objects::delete,
}

criterion_main!(benches);
