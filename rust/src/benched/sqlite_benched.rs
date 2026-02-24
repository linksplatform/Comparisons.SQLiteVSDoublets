//! Benched implementation for SQLite

use crate::{Benched, Fork};
use crate::sqlite_impl::SqliteLinks;

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
