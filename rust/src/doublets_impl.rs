//! Doublets storage implementation for links

use crate::{BlogPost, Link, Links, Objects};
use doublets::{
    mem::{Alloc, FileMapped},
    split::{self, DataPart, IndexPart},
    unit::{self, LinkPart},
    Doublets, DoubletsExt,
};
use std::{alloc::Global, collections::HashMap, fs::OpenOptions, path::Path};

/// Type alias for Doublets united (unit) store with volatile (in-memory) storage.
/// Each link is stored as a contiguous unit containing (id, source, target).
pub type DoubletsUnitedVolatile<T = usize> = unit::Store<T, Alloc<LinkPart<T>, Global>>;

/// Type alias for Doublets split store with volatile (in-memory) storage.
/// Separates data and index into different memory regions for better cache efficiency.
pub type DoubletsSplitVolatile<T = usize> =
    split::Store<T, Alloc<DataPart<T>, Global>, Alloc<IndexPart<T>, Global>>;

/// Type alias for Doublets united (unit) store with non-volatile (file-mapped) storage.
/// Same layout as [`DoubletsUnitedVolatile`], but the links live in a memory-mapped file.
pub type DoubletsUnitedNonVolatile<T = usize> = unit::Store<T, FileMapped<LinkPart<T>>>;

/// Type alias for Doublets split store with non-volatile (file-mapped) storage.
/// Same layout as [`DoubletsSplitVolatile`], but data and index live in two memory-mapped files.
pub type DoubletsSplitNonVolatile<T = usize> =
    split::Store<T, FileMapped<DataPart<T>>, FileMapped<IndexPart<T>>>;

/// Opens (creating it when missing) a file and maps it as Doublets memory.
pub fn map_file<T: Default, P: AsRef<Path>>(path: P) -> FileMapped<T> {
    let file = OpenOptions::new()
        .read(true)
        .write(true)
        .create(true)
        .open(path)
        .expect("Failed to open the links file");
    FileMapped::new(file).expect("Failed to map the links file")
}

/// Markers of the object like structures stored as links.
///
/// The markers mirror the ones of the C# `DoubletsDbContext`: a marker for the
/// blog post itself and one marker per property.
#[derive(Debug, Clone, Copy)]
struct Markers {
    blog_post: usize,
    title: usize,
    content: usize,
    publication_date_time: usize,
    empty_string: usize,
}

/// State needed to store object like structures as links.
///
/// Doublets has no notion of a string, so every string is stored as a sequence
/// of links: every distinct character becomes a point link (a "symbol"), and a
/// string becomes a left fold of `(sequence, symbol)` links. Identical strings
/// (and identical prefixes) are therefore stored exactly once, which is how the
/// C# benchmark stores unicode sequences as well.
#[derive(Default)]
struct ObjectsState {
    markers: Option<Markers>,
    symbols: HashMap<char, usize>,
    characters: HashMap<usize, char>,
}

impl ObjectsState {
    fn clear(&mut self) {
        self.markers = None;
        self.symbols.clear();
        self.characters.clear();
    }
}

/// Wrapper to adapt doublets::Doublets to our Links trait
pub struct DoubletsLinks<S> {
    store: S,
    objects: ObjectsState,
}

impl<S> DoubletsLinks<S> {
    pub fn new(store: S) -> Self {
        Self {
            store,
            objects: ObjectsState::default(),
        }
    }

    pub fn into_inner(self) -> S {
        self.store
    }
}

impl<S: Doublets<usize> + DoubletsExt<usize>> Links for DoubletsLinks<S> {
    fn create(&mut self, source: u64, target: u64) -> u64 {
        // `create_by` passes its argument as a *restriction* of the query, and
        // the memory stores ignore it — it always creates an empty link. The
        // source and the target are only assigned by the update that follows,
        // which is exactly what `create_link` does (the equivalent of
        // `CreateAndUpdate` of the C# `Platform.Data.Doublets`).
        self.store
            .create_link(source as usize, target as usize)
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
        // The markers and the symbols are links too, they are gone as well.
        self.objects.clear();
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

impl<S: Doublets<usize> + DoubletsExt<usize>> DoubletsLinks<S> {
    /// Returns the markers of the object like structures, creating them on first use.
    fn markers(&mut self) -> Markers {
        if let Some(markers) = self.objects.markers {
            return markers;
        }
        let markers = Markers {
            blog_post: self.point(),
            title: self.point(),
            content: self.point(),
            publication_date_time: self.point(),
            empty_string: self.point(),
        };
        self.objects.markers = Some(markers);
        markers
    }

    fn point(&mut self) -> usize {
        self.store.create_point().expect("Failed to create a marker")
    }

    /// Returns the point link representing `character`, creating it on first use.
    fn symbol(&mut self, character: char) -> usize {
        if let Some(&symbol) = self.objects.symbols.get(&character) {
            return symbol;
        }
        let symbol = self.point();
        self.objects.symbols.insert(character, symbol);
        self.objects.characters.insert(symbol, character);
        symbol
    }

    /// Stores `text` as a sequence of links and returns the link of the sequence.
    fn create_sequence(&mut self, text: &str) -> usize {
        let mut sequence: Option<usize> = None;
        for character in text.chars() {
            let symbol = self.symbol(character);
            sequence = Some(match sequence {
                None => symbol,
                Some(previous) => self
                    .store
                    .get_or_create(previous, symbol)
                    .expect("Failed to create a sequence link"),
            });
        }
        sequence.unwrap_or_else(|| self.markers().empty_string)
    }

    /// Restores the string stored as the sequence of links `sequence`.
    fn read_sequence(&self, sequence: usize) -> String {
        if Some(sequence) == self.objects.markers.map(|markers| markers.empty_string) {
            return String::new();
        }
        let mut characters = Vec::new();
        let mut current = sequence;
        loop {
            if let Some(&character) = self.objects.characters.get(&current) {
                characters.push(character);
                break;
            }
            match self.store.get_link(current) {
                Some(link) => {
                    match self.objects.characters.get(&link.target) {
                        Some(&character) => characters.push(character),
                        // Not a sequence of this store, stop instead of looping.
                        None => break,
                    }
                    current = link.source;
                }
                None => break,
            }
        }
        characters.reverse();
        characters.into_iter().collect()
    }

    /// Sets the value of a property of an object, as `PropertiesOperator` does in C#.
    fn set_property(&mut self, object: usize, marker: usize, value: usize) {
        let property = self
            .store
            .get_or_create(object, marker)
            .expect("Failed to create a property link");
        self.store
            .get_or_create(property, value)
            .expect("Failed to create a property value link");
    }

    /// Returns the value of a property of an object.
    fn property(&self, object: usize, marker: usize) -> Option<usize> {
        let any = self.store.constants().any;
        let property = self.store.search(object, marker)?;
        self.store
            .find([any, property, any])
            .map(|link| link.target)
    }
}

impl<S: Doublets<usize> + DoubletsExt<usize>> Objects for DoubletsLinks<S> {
    fn create_posts(&mut self, posts: &[BlogPost]) -> Vec<u64> {
        let markers = self.markers();
        let mut ids = Vec::with_capacity(posts.len());
        for post in posts {
            let title = self.create_sequence(&post.title);
            let content = self.create_sequence(&post.content);
            let date = self.create_sequence(&post.publication_date_time.to_string());

            // The blog post link is `(blog post marker, itself)`, exactly like
            // `CreateAndUpdate(_blogPostMarker, Constants.Itself)` in C#.
            let object = self.store.create().expect("Failed to create a blog post");
            self.store
                .update(object, markers.blog_post, object)
                .expect("Failed to mark a blog post");

            self.set_property(object, markers.title, title);
            self.set_property(object, markers.content, content);
            self.set_property(object, markers.publication_date_time, date);
            ids.push(object as u64);
        }
        ids
    }

    fn read_posts(&self) -> Vec<BlogPost> {
        let markers = match self.objects.markers {
            Some(markers) => markers,
            None => return Vec::new(),
        };
        self.objects_of(markers)
            .into_iter()
            .map(|object| {
                let title = self
                    .property(object, markers.title)
                    .map(|value| self.read_sequence(value))
                    .unwrap_or_default();
                let content = self
                    .property(object, markers.content)
                    .map(|value| self.read_sequence(value))
                    .unwrap_or_default();
                let date = self
                    .property(object, markers.publication_date_time)
                    .map(|value| self.read_sequence(value))
                    .unwrap_or_default();
                BlogPost {
                    id: object as u64,
                    title,
                    content,
                    publication_date_time: date.parse().unwrap_or_default(),
                }
            })
            .collect()
    }

    fn delete_posts(&mut self) {
        let markers = match self.objects.markers {
            Some(markers) => markers,
            None => return,
        };
        let any = self.store.constants().any;
        for object in self.objects_of(markers) {
            let properties: Vec<usize> = self
                .store
                .each_iter([any, object, any])
                .map(|link| link.index)
                .filter(|&index| index != object)
                .collect();
            for property in properties {
                let values: Vec<usize> = self
                    .store
                    .each_iter([any, property, any])
                    .map(|link| link.index)
                    .filter(|&index| index != property)
                    .collect();
                for value in values {
                    let _ = self.store.delete(value);
                }
                let _ = self.store.delete(property);
            }
            let _ = self.store.delete(object);
        }
    }

    fn count_posts(&self) -> usize {
        match self.objects.markers {
            Some(markers) => self.objects_of(markers).len(),
            None => 0,
        }
    }
}

impl<S: Doublets<usize> + DoubletsExt<usize>> DoubletsLinks<S> {
    /// Links of all stored blog posts: `(blog post marker, itself)` links.
    fn objects_of(&self, markers: Markers) -> Vec<usize> {
        let any = self.store.constants().any;
        self.store
            .each_iter([any, markers.blog_post, any])
            .map(|link| link.index)
            .filter(|&index| index != markers.blog_post)
            .collect()
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

/// Create a new file-mapped doublets united store
pub fn create_united_non_volatile<P: AsRef<Path>>(
    path: P,
) -> DoubletsLinks<DoubletsUnitedNonVolatile> {
    let mem = map_file(path);
    let store = DoubletsUnitedNonVolatile::new(mem).expect("Failed to create doublets store");
    DoubletsLinks::new(store)
}

/// Create a new file-mapped doublets split store
pub fn create_split_non_volatile<P: AsRef<Path>, Q: AsRef<Path>>(
    data_path: P,
    index_path: Q,
) -> DoubletsLinks<DoubletsSplitNonVolatile> {
    let data_mem = map_file(data_path);
    let index_mem = map_file(index_path);
    let store = DoubletsSplitNonVolatile::new(data_mem, index_mem)
        .expect("Failed to create doublets store");
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

    /// Regression test: `create` used to call `create_by([source, target])`,
    /// which the memory stores read as a restriction and ignore, so every
    /// benchmarked link was created empty instead of connecting two links.
    #[test]
    fn test_create_assigns_source_and_target() {
        let mut db = create_united_volatile();
        let first = db.create_point();
        let second = db.create_point();
        let id = db.create(first, second);

        let link = db.query_by_id(id).unwrap();
        assert_eq!(link.source, first);
        assert_eq!(link.target, second);
        assert_eq!(db.query_by_source(first).len(), 2);
        assert_eq!(db.query_by_target(second).len(), 2);
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
