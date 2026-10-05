//! Works around a `doublets` 0.5.0 and `platform-mem` 0.3.0 mismatch: stores take the slice returned by
//! `RawMem::grow` for their whole memory, but the memories return only the grown part. A fresh store then
//! sees 2^20 − 8,192 = 1,040,384 links of its 2^20 and panics with "index out of bounds" when it creates
//! more, and any later growth would leave it with an empty memory. [`Whole`] returns the whole memory.

use doublets::mem::{FileMapped, Global, RawMem, Result};
use std::{mem::MaybeUninit, path::Path};

pub struct Whole<M>(pub M);

impl<M: RawMem> RawMem for Whole<M> {
    type Item = M::Item;

    fn allocated(&self) -> &[Self::Item] {
        self.0.allocated()
    }

    fn allocated_mut(&mut self) -> &mut [Self::Item] {
        self.0.allocated_mut()
    }

    unsafe fn grow(
        &mut self,
        addition: usize,
        fill: impl FnOnce(usize, (&mut [Self::Item], &mut [MaybeUninit<Self::Item>])),
    ) -> Result<&mut [Self::Item]> {
        // SAFETY: the caller's contract is passed on unchanged.
        unsafe { self.0.grow(addition, fill)? };
        Ok(self.0.allocated_mut())
    }

    fn shrink(&mut self, cap: usize) -> Result<()> {
        self.0.shrink(cap)
    }

    fn size_hint(&self) -> Option<usize> {
        self.0.size_hint()
    }
}

/// Memory of the volatile stores.
pub fn volatile<T>() -> Whole<Global<T>> {
    Whole(Global::new())
}

/// Memory of the non-volatile stores, mapped from `path`.
pub fn mapped<T>(path: impl AsRef<Path>) -> Whole<FileMapped<T>> {
    Whole(FileMapped::from_path(path).unwrap())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn grow_returns_the_whole_memory() {
        let mut memory = Global::<u64>::new();
        memory.grow_filled(2, 1).unwrap();
        assert_eq!(memory.grow_filled(3, 2).unwrap(), [2, 2, 2]);
        let mut memory = volatile::<u64>();
        memory.grow_filled(2, 1).unwrap();
        assert_eq!(memory.grow_filled(3, 2).unwrap(), [1, 1, 2, 2, 2]);
    }
}
