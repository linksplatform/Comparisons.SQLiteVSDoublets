//! Benched implementation for SQLite

use crate::sqlite_impl::SqliteLinks;
use crate::{temp_path, Benched, Fork};
use std::path::PathBuf;

/// Benched implementation for SQLite with in-memory database
pub struct SqliteMemoryBenched {
    links: SqliteLinks,
}

impl Benched for SqliteMemoryBenched {
    type Builder = ();

    fn setup(_builder: Self::Builder) -> Self {
        Self {
            links: SqliteLinks::new_memory(),
        }
    }

    fn fork(&mut self) -> Fork<Self> {
        Fork::new(self)
    }

    unsafe fn unfork(&mut self) {
        self.links.reset();
    }
}

impl std::ops::Deref for SqliteMemoryBenched {
    type Target = SqliteLinks;

    fn deref(&self) -> &Self::Target {
        &self.links
    }
}

impl std::ops::DerefMut for SqliteMemoryBenched {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.links
    }
}

/// Benched implementation for SQLite with a file based database
///
/// The file lives in the temporary directory of the machine and is removed when
/// the benchmark subject is dropped.
pub struct SqliteFileBenched {
    links: SqliteLinks,
    path: PathBuf,
}

impl Benched for SqliteFileBenched {
    type Builder = ();

    fn setup(_builder: Self::Builder) -> Self {
        let path = temp_path("sqlite.db");
        // Start from an empty database even if a previous run left a file.
        let _ = std::fs::remove_file(&path);
        Self {
            links: SqliteLinks::new_file(&path),
            path,
        }
    }

    fn fork(&mut self) -> Fork<Self> {
        Fork::new(self)
    }

    unsafe fn unfork(&mut self) {
        self.links.reset();
    }
}

impl std::ops::Deref for SqliteFileBenched {
    type Target = SqliteLinks;

    fn deref(&self) -> &Self::Target {
        &self.links
    }
}

impl std::ops::DerefMut for SqliteFileBenched {
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.links
    }
}

impl Drop for SqliteFileBenched {
    fn drop(&mut self) {
        let _ = std::fs::remove_file(&self.path);
    }
}
