//! SQLite vs Doublets benchmark library
//!
//! This library provides implementations for benchmarking SQLite and Doublets
//! storage systems on basic CRUD operations with links.

#![feature(allocator_api)]

pub mod benched;
pub mod doublets_impl;
pub mod exclusive;
pub mod fork;
pub mod sqlite_impl;

pub use benched::Benched;
pub use exclusive::Exclusive;
pub use fork::Fork;

use once_cell::sync::Lazy;
use std::env;

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

/// Macro for running benchmarks with proper setup and teardown
#[macro_export]
macro_rules! bench {
    ($name:expr, $benched:expr, $op:expr) => {{
        use $crate::Benched;
        let mut fork = $benched.fork();
        $op(&mut *fork);
    }};
}
