//! Memories of the stores. doublets 0.5.0 needed a wrapper here that returned the whole memory
//! after growing it; doublets 0.21.0 does not, see experiments/unit_store_growth.

use doublets::mem::{FileMapped, Global, RawMem, Result};
use std::{mem::MaybeUninit, path::Path};

/// Memory of the volatile stores.
pub fn volatile<T>() -> Global<T> {
    Global::new()
}

/// Memory of the non-volatile stores, mapped from `path`.
pub fn mapped<T>(path: impl AsRef<Path>) -> ZeroExtended<T> {
    ZeroExtended(FileMapped::from_path(path).unwrap())
}

/// A mapped file whose growth is not written over.
///
/// doublets 0.21.0 grows its memory with `grow_filled(_, Default::default())`, and `FileMapped`
/// of platform-mem 0.4.0 counts as initialized only the bytes the file had when it was opened, so
/// every growth writes zeros over the whole new region: a united store of 32-bit links dirties
/// 32 MiB of mapped pages (its reservation step of 2^20 links of 32 bytes) when it is created and
/// at every growth, which a cgroup limit of 24 MiB can't hold, so the process is killed before
/// the first link. The file grows with `set_len`, which reads as zeros, so the new region
/// already holds the default parts and stays a hole until links are written to it.
///
/// Only for the parts of the stores, whose default is all zero bits.
pub struct ZeroExtended<T>(FileMapped<T>);

impl<T> RawMem for ZeroExtended<T> {
    type Item = T;

    fn allocated(&self) -> &[T] {
        self.0.allocated()
    }

    fn allocated_mut(&mut self) -> &mut [T] {
        self.0.allocated_mut()
    }

    unsafe fn grow(
        &mut self,
        addition: usize,
        fill: impl FnOnce(usize, (&mut [T], &mut [MaybeUninit<T>])),
    ) -> Result<&mut [T]> {
        // `fill` initializes the part of the new region after the count it is given.
        unsafe {
            self.0.grow(addition, |_, (inited, uninit)| {
                fill(uninit.len(), (inited, uninit))
            })
        }
    }

    fn shrink(&mut self, cap: usize) -> Result<()> {
        self.0.shrink(cap)
    }

    fn size_hint(&self) -> Option<usize> {
        self.0.size_hint()
    }
}
