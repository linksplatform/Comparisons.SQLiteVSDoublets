//! Thread-safe exclusive access wrapper

use std::cell::UnsafeCell;
use std::ops::{Deref, DerefMut};

/// A wrapper that provides mutable access through an immutable reference.
/// This is useful for benchmark scenarios where we need interior mutability.
pub struct Exclusive<T> {
    inner: UnsafeCell<T>,
}

impl<T> Exclusive<T> {
    /// Creates a new Exclusive wrapper
    pub fn new(value: T) -> Self {
        Self {
            inner: UnsafeCell::new(value),
        }
    }

    /// Gets a mutable reference to the inner value
    ///
    /// # Safety
    /// The caller must ensure that no other references exist to the inner value.
    #[allow(clippy::mut_from_ref)]
    pub fn get_mut(&self) -> &mut T {
        unsafe { &mut *self.inner.get() }
    }
}

impl<T> Deref for Exclusive<T> {
    type Target = T;

    fn deref(&self) -> &Self::Target {
        unsafe { &*self.inner.get() }
    }
}

impl<T> DerefMut for Exclusive<T> {
    fn deref_mut(&mut self) -> &mut Self::Target {
        unsafe { &mut *self.inner.get() }
    }
}

// SAFETY: We ensure exclusive access through the API design
unsafe impl<T: Send> Send for Exclusive<T> {}
unsafe impl<T: Send> Sync for Exclusive<T> {}
