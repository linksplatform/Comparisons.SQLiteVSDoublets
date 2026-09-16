//! SQLite vs Doublets benchmark library
//!
//! This library provides implementations for benchmarking SQLite and Doublets
//! storage systems on basic CRUD operations with links.

#![feature(allocator_api)]

pub mod benched;
pub mod doublets_impl;
pub mod exclusive;
pub mod fork;
pub mod objects;
pub mod sqlite_impl;

pub use benched::Benched;
pub use exclusive::Exclusive;
pub use fork::Fork;
pub use objects::{generate_posts, BlogPost, Objects, BENCHMARK_OBJECT_COUNT};

use once_cell::sync::Lazy;
use std::{
    env,
    path::PathBuf,
    process,
    sync::atomic::{AtomicUsize, Ordering},
};

/// Number of links to use for benchmarking
pub static BENCHMARK_LINK_COUNT: Lazy<usize> = Lazy::new(|| {
    env::var("BENCHMARK_LINK_COUNT")
        .ok()
        .and_then(|s| s.parse().ok())
        .unwrap_or(1000)
});

/// Number of background links to create before benchmarking
pub static BACKGROUND_LINK_COUNT: Lazy<usize> = Lazy::new(|| {
    env::var("BACKGROUND_LINK_COUNT")
        .ok()
        .and_then(|s| s.parse().ok())
        .unwrap_or(3000)
});

/// Returns a unique path inside the temporary directory of the machine.
///
/// The non-volatile (file backed) benchmark subjects store their data there, so
/// that parallel runs of the benchmarks never share a file, and so that nothing
/// is left in the working directory.
pub fn temp_path(name: &str) -> PathBuf {
    static COUNTER: AtomicUsize = AtomicUsize::new(0);
    let unique = COUNTER.fetch_add(1, Ordering::Relaxed);
    env::temp_dir().join(format!(
        "sqlite-vs-doublets-{}-{}-{}",
        process::id(),
        unique,
        name
    ))
}

/// A link structure representing a doublet (source -> target relationship)
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Link {
    pub id: u64,
    pub source: u64,
    pub target: u64,
}

impl Link {
    pub fn new(id: u64, source: u64, target: u64) -> Self {
        Self { id, source, target }
    }
}

/// Trait for database operations on links
pub trait Links {
    /// Create a new link and return its ID
    fn create(&mut self, source: u64, target: u64) -> u64;

    /// Create a point link (self-referencing link)
    fn create_point(&mut self) -> u64 {
        let id = self.create(0, 0);
        self.update(id, id, id);
        id
    }

    /// Update an existing link
    fn update(&mut self, id: u64, source: u64, target: u64);

    /// Delete a link by ID
    fn delete(&mut self, id: u64);

    /// Delete all links
    fn delete_all(&mut self);

    /// Query all links
    fn query_all(&self) -> Vec<Link>;

    /// Query a link by ID
    fn query_by_id(&self, id: u64) -> Option<Link>;

    /// Query links by source
    fn query_by_source(&self, source: u64) -> Vec<Link>;

    /// Query links by target
    fn query_by_target(&self, target: u64) -> Vec<Link>;

    /// Query links by source and target
    fn query_by_source_target(&self, source: u64, target: u64) -> Vec<Link>;

    /// Count all links
    fn count(&self) -> usize;
}

/// Creates `count` background point links and returns their identifiers.
///
/// Background links make every benchmark run against a non-empty storage, so
/// that queries have to search through unrelated data, as they would in a real
/// application.
pub fn fill_background(links: &mut impl Links, count: usize) -> Vec<u64> {
    (0..count).map(|_| links.create_point()).collect()
}

/// Creates `count` links between the given background links.
///
/// Link number `i` connects `background[i % len]` to a background link further
/// down the list, so that every created doublet is unique (Doublets stores
/// every doublet exactly once) while sources and targets are shared by several
/// links — which is what makes the outgoing and incoming queries meaningful.
pub fn fill_benchmarked(links: &mut impl Links, background: &[u64], count: usize) -> Vec<Link> {
    assert!(
        !background.is_empty(),
        "background links are required to create benchmarked links"
    );
    let len = background.len();
    (0..count)
        .map(|index| {
            let source = background[index % len];
            let target = background[(index % len + 1 + index / len) % len];
            let id = links.create(source, target);
            Link::new(id, source, target)
        })
        .collect()
}

/// Macro for running benchmarks with proper setup and teardown
#[macro_export]
macro_rules! bench {
    ($name:expr, $benched:expr, $op:expr) => {{
        use $crate::Benched;
        let mut fork = $benched.fork();
        $op(&mut *fork);
    }};
}
