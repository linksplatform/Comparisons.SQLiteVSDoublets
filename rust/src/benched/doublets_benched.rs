//! Benched implementations for Doublets

use crate::{Benched, Fork, Links};
use crate::doublets_impl::{
    create_split_volatile, create_united_volatile, DoubletsLinks, DoubletsSplitVolatile,
    DoubletsUnitedVolatile,
};

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
