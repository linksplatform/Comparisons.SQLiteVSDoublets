//! Benched implementations for Doublets

use crate::doublets_impl::{
    create_split_non_volatile, create_split_volatile, create_united_non_volatile,
    create_united_volatile, DoubletsLinks, DoubletsSplitNonVolatile, DoubletsSplitVolatile,
    DoubletsUnitedNonVolatile, DoubletsUnitedVolatile,
};
use crate::{temp_path, Benched, Fork, Links};
use std::path::PathBuf;

/// Benched implementation for Doublets United (unit) store with volatile storage
pub struct DoubletsUnitedVolatileBenched {
    links: DoubletsLinks<DoubletsUnitedVolatile>,
}

impl Benched for DoubletsUnitedVolatileBenched {
    type Builder = ();

    fn setup(_builder: Self::Builder) -> Self {
        Self {
            links: create_united_volatile(),
        }
    }

    fn fork(&mut self) -> Fork<Self> {
        Fork::new(self)
    }

    unsafe fn unfork(&mut self) {
        self.links.delete_all();
    }
}

impl std::ops::Deref for DoubletsUnitedVolatileBenched {
    type Target = DoubletsLinks<DoubletsUnitedVolatile>;

    fn deref(&self) -> &Self::Target {
        &self.links
    }
}

impl std::ops::DerefMut for DoubletsUnitedVolatileBenched {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.links
    }
}

/// Benched implementation for Doublets Split store with volatile storage
pub struct DoubletsSplitVolatileBenched {
    links: DoubletsLinks<DoubletsSplitVolatile>,
}

impl Benched for DoubletsSplitVolatileBenched {
    type Builder = ();

    fn setup(_builder: Self::Builder) -> Self {
        Self {
            links: create_split_volatile(),
        }
    }

    fn fork(&mut self) -> Fork<Self> {
        Fork::new(self)
    }

    unsafe fn unfork(&mut self) {
        self.links.delete_all();
    }
}

impl std::ops::Deref for DoubletsSplitVolatileBenched {
    type Target = DoubletsLinks<DoubletsSplitVolatile>;

    fn deref(&self) -> &Self::Target {
        &self.links
    }
}

impl std::ops::DerefMut for DoubletsSplitVolatileBenched {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.links
    }
}

/// Benched implementation for Doublets United (unit) store with non-volatile storage
///
/// The links are stored in a memory-mapped file inside the temporary directory
/// of the machine, which is removed when the benchmark subject is dropped.
pub struct DoubletsUnitedNonVolatileBenched {
    links: DoubletsLinks<DoubletsUnitedNonVolatile>,
    path: PathBuf,
}

impl Benched for DoubletsUnitedNonVolatileBenched {
    type Builder = ();

    fn setup(_builder: Self::Builder) -> Self {
        let path = temp_path("united.links");
        // Start from an empty store even if a previous run left a file.
        let _ = std::fs::remove_file(&path);
        Self {
            links: create_united_non_volatile(&path),
            path,
        }
    }

    fn fork(&mut self) -> Fork<Self> {
        Fork::new(self)
    }

    unsafe fn unfork(&mut self) {
        self.links.delete_all();
    }
}

impl std::ops::Deref for DoubletsUnitedNonVolatileBenched {
    type Target = DoubletsLinks<DoubletsUnitedNonVolatile>;

    fn deref(&self) -> &Self::Target {
        &self.links
    }
}

impl std::ops::DerefMut for DoubletsUnitedNonVolatileBenched {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.links
    }
}

impl Drop for DoubletsUnitedNonVolatileBenched {
    fn drop(&mut self) {
        let _ = std::fs::remove_file(&self.path);
    }
}

/// Benched implementation for Doublets Split store with non-volatile storage
///
/// The data and the index are stored in two memory-mapped files inside the
/// temporary directory of the machine, both removed when the benchmark subject
/// is dropped.
pub struct DoubletsSplitNonVolatileBenched {
    links: DoubletsLinks<DoubletsSplitNonVolatile>,
    data_path: PathBuf,
    index_path: PathBuf,
}

impl Benched for DoubletsSplitNonVolatileBenched {
    type Builder = ();

    fn setup(_builder: Self::Builder) -> Self {
        let data_path = temp_path("split.data.links");
        let index_path = temp_path("split.index.links");
        let _ = std::fs::remove_file(&data_path);
        let _ = std::fs::remove_file(&index_path);
        Self {
            links: create_split_non_volatile(&data_path, &index_path),
            data_path,
            index_path,
        }
    }

    fn fork(&mut self) -> Fork<Self> {
        Fork::new(self)
    }

    unsafe fn unfork(&mut self) {
        self.links.delete_all();
    }
}

impl std::ops::Deref for DoubletsSplitNonVolatileBenched {
    type Target = DoubletsLinks<DoubletsSplitNonVolatile>;

    fn deref(&self) -> &Self::Target {
        &self.links
    }
}

impl std::ops::DerefMut for DoubletsSplitNonVolatileBenched {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.links
    }
}

impl Drop for DoubletsSplitNonVolatileBenched {
    fn drop(&mut self) {
        let _ = std::fs::remove_file(&self.data_path);
        let _ = std::fs::remove_file(&self.index_path);
    }
}
