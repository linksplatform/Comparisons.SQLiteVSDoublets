//! Doublets storage implementation for links

use crate::{Link, Links};
use doublets::{
    mem::Alloc,
    split::{self, DataPart, IndexPart},
    unit::{self, LinkPart},
    Doublets, DoubletsExt,
};
use std::alloc::Global;

/// Type alias for Doublets united (unit) store with volatile (in-memory) storage.
/// Each link is stored as a contiguous unit containing (id, source, target).
pub type DoubletsUnitedVolatile<T = usize> = unit::Store<T, Alloc<LinkPart<T>, Global>>;

/// Type alias for Doublets split store with volatile (in-memory) storage.
/// Separates data and index into different memory regions for better cache efficiency.
pub type DoubletsSplitVolatile<T = usize> =
    split::Store<T, Alloc<DataPart<T>, Global>, Alloc<IndexPart<T>, Global>>;

/// Wrapper to adapt doublets::Doublets to our Links trait
pub struct DoubletsLinks<S> {
    store: S,
}

impl<S> DoubletsLinks<S> {
    pub fn new(store: S) -> Self {
        Self { store }
    }

    pub fn into_inner(self) -> S {
        self.store
    }
}

impl<S: Doublets<usize> + DoubletsExt<usize>> Links for DoubletsLinks<S> {
    fn create(&mut self, source: u64, target: u64) -> u64 {
        self.store
            .create_by([source as usize, target as usize])
            .expect("Failed to create link") as u64
    }

    fn create_point(&mut self) -> u64 {
        self.store.create_point().expect("Failed to create point") as u64
    }

    fn update(&mut self, id: u64, source: u64, target: u64) {
        self.store
            .update(id as usize, source as usize, target as usize)
            .expect("Failed to update link");
    }

    fn delete(&mut self, id: u64) {
        self.store
            .delete(id as usize)
            .expect("Failed to delete link");
    }

    fn delete_all(&mut self) {
        let any = self.store.constants().any;
        let ids: Vec<usize> = self
            .store
            .each_iter([any, any, any])
            .map(|link| link.index)
            .collect();
        for id in ids {
            let _ = self.store.delete(id);
        }
    }

    fn query_all(&self) -> Vec<Link> {
        let any = self.store.constants().any;
        self.store
            .each_iter([any, any, any])
            .map(|link| Link::new(link.index as u64, link.source as u64, link.target as u64))
            .collect()
    }

    fn query_by_id(&self, id: u64) -> Option<Link> {
        self.store.get_link(id as usize).map(|link| {
            Link::new(link.index as u64, link.source as u64, link.target as u64)
        })
    }

    fn query_by_source(&self, source: u64) -> Vec<Link> {
        let any = self.store.constants().any;
        self.store
            .each_iter([any, source as usize, any])
            .map(|link| Link::new(link.index as u64, link.source as u64, link.target as u64))
            .collect()
    }

    fn query_by_target(&self, target: u64) -> Vec<Link> {
        let any = self.store.constants().any;
        self.store
            .each_iter([any, any, target as usize])
            .map(|link| Link::new(link.index as u64, link.source as u64, link.target as u64))
            .collect()
    }

    fn query_by_source_target(&self, source: u64, target: u64) -> Vec<Link> {
        let any = self.store.constants().any;
        self.store
            .each_iter([any, source as usize, target as usize])
            .map(|link| Link::new(link.index as u64, link.source as u64, link.target as u64))
            .collect()
    }

    fn count(&self) -> usize {
        self.store.count()
    }
}

/// Create a new in-memory doublets united store
pub fn create_united_volatile() -> DoubletsLinks<DoubletsUnitedVolatile> {
    let mem = Alloc::new(Global);
    let store = DoubletsUnitedVolatile::new(mem).expect("Failed to create doublets store");
    DoubletsLinks::new(store)
}

/// Create a new in-memory doublets split store
pub fn create_split_volatile() -> DoubletsLinks<DoubletsSplitVolatile> {
    let data_mem = Alloc::new(Global);
    let index_mem = Alloc::new(Global);
    let store =
        DoubletsSplitVolatile::new(data_mem, index_mem).expect("Failed to create doublets store");
    DoubletsLinks::new(store)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_create_and_query_united() {
        let mut db = create_united_volatile();
        let id = db.create_point();
        assert_eq!(id, 1);

        let link = db.query_by_id(id).unwrap();
        assert_eq!(link.source, id);
        assert_eq!(link.target, id);
    }

    #[test]
    fn test_create_and_query_split() {
        let mut db = create_split_volatile();
        let id = db.create_point();
        assert_eq!(id, 1);

        let link = db.query_by_id(id).unwrap();
        assert_eq!(link.source, id);
        assert_eq!(link.target, id);
    }

    #[test]
    fn test_update() {
        let mut db = create_united_volatile();
        let id = db.create(1, 2);
        db.update(id, 3, 4);

        let link = db.query_by_id(id).unwrap();
        assert_eq!(link.source, 3);
        assert_eq!(link.target, 4);
    }

    #[test]
    fn test_delete() {
        let mut db = create_united_volatile();
        let id = db.create_point();
        db.delete(id);
        assert!(db.query_by_id(id).is_none());
    }

    #[test]
    fn test_query_by_source() {
        let mut db = create_united_volatile();
        let id1 = db.create_point();
        let id2 = db.create_point();
        db.update(id1, id1, id2);
        db.update(id2, id1, id1);

        let links = db.query_by_source(id1);
        assert_eq!(links.len(), 2);
    }
}
