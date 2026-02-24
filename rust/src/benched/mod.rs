//! Benched trait and implementations for SQLite and Doublets

mod doublets_benched;
mod sqlite_benched;

pub use doublets_benched::*;
pub use sqlite_benched::*;

use crate::Fork;

/// Trait for types that can be benchmarked with setup/teardown lifecycle
pub trait Benched: Sized {
    /// The builder type used to construct this benched type
    type Builder;

    /// Setup the benchmark environment
    fn setup(builder: Self::Builder) -> Self;

    /// Create a fork for an isolated benchmark iteration
    fn fork(&mut self) -> Fork<Self>;

    /// Clean up after a benchmark iteration
    ///
    /// # Safety
    /// This should only be called by Fork's Drop implementation
    unsafe fn unfork(&mut self);
}
