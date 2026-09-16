//! Prints what every benchmarked storage contains after a benchmark iteration.
//!
//! Useful to check by hand that all six subjects of the benchmarks behave the
//! same way — the assumption the whole comparison is based on.
//!
//! ```text
//! cargo run --release --example inspect_storage
//! ```

#![feature(allocator_api)]

use sqlite_vs_doublets::benched::{
    DoubletsSplitNonVolatileBenched, DoubletsSplitVolatileBenched,
    DoubletsUnitedNonVolatileBenched, DoubletsUnitedVolatileBenched, SqliteFileBenched,
    SqliteMemoryBenched,
};
use sqlite_vs_doublets::{
    fill_background, fill_benchmarked, generate_posts, Benched, Links, Objects,
};
use std::ops::DerefMut;

const BACKGROUND: usize = 100;
const BENCHMARKED: usize = 100;
const POSTS: usize = 10;

fn inspect<B>(name: &str, subject: &mut B)
where
    B: Benched + DerefMut,
    B::Target: Links + Objects + Sized,
{
    let mut fork = subject.fork();
    let storage = &mut **fork;

    let background = fill_background(storage, BACKGROUND);
    let created = fill_benchmarked(storage, &background, BENCHMARKED);
    let sample = created[created.len() / 2];
    let posts = generate_posts(POSTS);
    storage.create_posts(&posts);

    println!("{name}:");
    println!("  links                  {}", storage.count());
    println!("  by identity            {:?}", storage.query_by_id(sample.id));
    let outgoing = storage.query_by_source(sample.source).len();
    println!("  outgoing of the sample {outgoing}");
    let incoming = storage.query_by_target(sample.target).len();
    println!("  incoming of the sample {incoming}");
    println!("  blog posts             {}", storage.count_posts());
    println!("  first blog post        {:?}", storage.read_posts().first());
}

fn main() {
    inspect("SQLite_Memory", &mut SqliteMemoryBenched::setup(()));
    inspect("SQLite_File", &mut SqliteFileBenched::setup(()));
    inspect(
        "Doublets_United_Volatile",
        &mut DoubletsUnitedVolatileBenched::setup(()),
    );
    inspect(
        "Doublets_United_NonVolatile",
        &mut DoubletsUnitedNonVolatileBenched::setup(()),
    );
    inspect(
        "Doublets_Split_Volatile",
        &mut DoubletsSplitVolatileBenched::setup(()),
    );
    inspect(
        "Doublets_Split_NonVolatile",
        &mut DoubletsSplitNonVolatileBenched::setup(()),
    );
}
