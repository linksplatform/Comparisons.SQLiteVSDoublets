//! Fork mechanism for isolated benchmark runs

use crate::Benched;
use std::ops::{Deref, DerefMut};

/// A fork that provides an isolated environment for benchmark iterations.
/// When dropped, it calls unfork() to clean up the state.
pub struct Fork<'a, B: Benched> {
    benched: &'a mut B,
}

impl<'a, B: Benched> Fork<'a, B> {
    /// Creates a new Fork from a Benched implementation
    pub fn new(benched: &'a mut B) -> Self {
        Self { benched }
    }
}

impl<'a, B: Benched> Deref for Fork<'a, B> {
    type Target = B;

    fn deref(&self) -> &Self::Target {
        self.benched
    }
}

impl<'a, B: Benched> DerefMut for Fork<'a, B> {
    fn deref_mut(&mut self) -> &mut Self::Target {
        self.benched
    }
}

impl<'a, B: Benched> Drop for Fork<'a, B> {
    fn drop(&mut self) {
        // SAFETY: We ensure the benched is properly cleaned up
        unsafe {
            self.benched.unfork();
        }
    }
}
