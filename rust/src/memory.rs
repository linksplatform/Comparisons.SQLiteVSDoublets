//! Memories of the stores. doublets 0.5.0 needed a wrapper here that returned the whole memory
//! after growing it; doublets 0.21.0 does not, see experiments/unit_store_growth.

use doublets::mem::{FileMapped, Global};
use std::path::Path;

/// Memory of the volatile stores.
pub fn volatile<T>() -> Global<T> {
    Global::new()
}

/// Memory of the non-volatile stores, mapped from `path`.
pub fn mapped<T>(path: impl AsRef<Path>) -> FileMapped<T> {
    FileMapped::from_path(path).unwrap()
}
