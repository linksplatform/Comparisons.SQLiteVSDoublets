use crate::dataset::BlogPost;
use doublets::{
    Doublets,
    data::{AddrToRaw, Flow, LinkReference, RawToAddr},
};
use rusqlite::{Connection, OptionalExtension, params};
use std::{collections::HashMap, marker::PhantomData, path::Path, sync::Arc};

pub trait BlogPostsStorage<T> {
    fn create(&mut self, post: &BlogPost) -> T;
    fn get(&mut self, id: T) -> Option<BlogPost>;
    fn each(&mut self, visit: impl FnMut(T, BlogPost));
    fn delete(&mut self, id: T);
    fn count(&mut self) -> u64;

    fn transaction<R>(&mut self, work: impl FnOnce(&mut Self) -> R) -> R {
        work(self)
    }
}

pub struct SqliteBlogPosts<T> {
    connection: Connection,
    id_type: PhantomData<T>,
}

impl<T: LinkReference> SqliteBlogPosts<T> {
    pub fn open(path: impl AsRef<Path>) -> Self {
        Self::new(Connection::open(path).unwrap())
    }

    pub fn in_memory() -> Self {
        Self::new(Connection::open_in_memory().unwrap())
    }

    fn new(connection: Connection) -> Self {
        connection
            .execute_batch(
                "CREATE TABLE blog_posts (id INTEGER PRIMARY KEY, title TEXT NOT NULL, content TEXT NOT NULL, publication_date INTEGER NOT NULL)",
            )
            .unwrap();
        Self {
            connection,
            id_type: PhantomData,
        }
    }
}

fn blog_post(row: &rusqlite::Row<'_>) -> rusqlite::Result<BlogPost> {
    Ok(BlogPost {
        title: row.get(0)?,
        content: row.get(1)?,
        publication_date: row.get(2)?,
    })
}

fn sql<T: LinkReference>(id: T) -> i64 {
    id.try_into().unwrap()
}

impl<T: LinkReference> BlogPostsStorage<T> for SqliteBlogPosts<T> {
    fn create(&mut self, post: &BlogPost) -> T {
        let mut statement = self
            .connection
            .prepare_cached(
                "INSERT INTO blog_posts (title, content, publication_date) VALUES (?1, ?2, ?3)",
            )
            .unwrap();
        statement
            .execute(params![post.title, post.content, post.publication_date])
            .unwrap();
        T::try_from(self.connection.last_insert_rowid()).unwrap()
    }

    fn get(&mut self, id: T) -> Option<BlogPost> {
        let mut statement = self
            .connection
            .prepare_cached("SELECT title, content, publication_date FROM blog_posts WHERE id = ?1")
            .unwrap();
        statement
            .query_row([sql(id)], blog_post)
            .optional()
            .unwrap()
    }

    fn each(&mut self, mut visit: impl FnMut(T, BlogPost)) {
        let mut statement = self
            .connection
            .prepare_cached("SELECT title, content, publication_date, id FROM blog_posts")
            .unwrap();
        let mut rows = statement.query([]).unwrap();
        while let Some(row) = rows.next().unwrap() {
            visit(
                T::try_from(row.get::<_, i64>(3).unwrap()).unwrap(),
                blog_post(row).unwrap(),
            );
        }
    }

    fn delete(&mut self, id: T) {
        let mut statement = self
            .connection
            .prepare_cached("DELETE FROM blog_posts WHERE id = ?1")
            .unwrap();
        statement.execute([sql(id)]).unwrap();
    }

    fn count(&mut self) -> u64 {
        self.connection
            .query_row("SELECT COUNT(*) FROM blog_posts", [], |row| row.get(0))
            .unwrap()
    }

    fn transaction<R>(&mut self, work: impl FnOnce(&mut Self) -> R) -> R {
        self.connection.execute_batch("BEGIN IMMEDIATE").unwrap();
        let result = work(self);
        self.connection.execute_batch("COMMIT").unwrap();
        result
    }
}

/// Stores each blog post as a `(blog_post, itself)` link with `(post, property) -> value` properties,
/// strings as balanced-variant sequences of Unicode symbols and dates as raw numbers,
/// using the same layout as `Platform.Data.Doublets.Sequences` in C#.
pub struct DoubletsBlogPosts<T, D> {
    links: D,
    unicode_symbol: T,
    unicode_sequence: T,
    title: T,
    content: T,
    publication_date: T,
    blog_post: T,
    cache: Option<SequencesCache<T>>,
}

#[derive(Default)]
struct SequencesCache<T> {
    sequences: HashMap<Arc<str>, T>,
    strings: HashMap<T, Arc<str>>,
}

impl<T: LinkReference, D: Doublets<T>> DoubletsBlogPosts<T, D> {
    pub fn new(mut links: D, cache_sequences: bool) -> Self {
        let meaning_root = links.create_point().unwrap();
        let mut marker = || {
            let marker = links.create().unwrap();
            links.update(marker, meaning_root, marker).unwrap()
        };
        Self {
            unicode_symbol: marker(),
            unicode_sequence: marker(),
            title: marker(),
            content: marker(),
            publication_date: marker(),
            blog_post: marker(),
            cache: cache_sequences.then(SequencesCache::default),
            links,
        }
    }

    fn sequence(&mut self, string: &Arc<str>) -> T {
        if let Some(sequence) = self
            .cache
            .as_ref()
            .and_then(|cache| cache.sequences.get(&**string))
        {
            return *sequence;
        }
        if string.is_empty() {
            return self.unicode_sequence;
        }
        let symbols: Vec<T> = string
            .encode_utf16()
            .map(|char| {
                self.links
                    .get_or_create(
                        AddrToRaw.convert(T::try_from(char).unwrap()),
                        self.unicode_symbol,
                    )
                    .unwrap()
            })
            .collect();
        let balanced = self.balanced_variant(symbols);
        let sequence = self
            .links
            .get_or_create(balanced, self.unicode_sequence)
            .unwrap();
        if let Some(cache) = &mut self.cache {
            cache.sequences.insert(Arc::clone(string), sequence);
        }
        sequence
    }

    fn balanced_variant(&mut self, mut layer: Vec<T>) -> T {
        while layer.len() > 1 {
            layer = layer
                .chunks(2)
                .map(|pair| match *pair {
                    [source, target] => self.links.get_or_create(source, target).unwrap(),
                    [last] => last,
                    _ => unreachable!(),
                })
                .collect();
        }
        layer[0]
    }

    fn string(&mut self, sequence: T) -> Arc<str> {
        if let Some(string) = self
            .cache
            .as_ref()
            .and_then(|cache| cache.strings.get(&sequence))
        {
            return Arc::clone(string);
        }
        if sequence == self.unicode_sequence {
            return Arc::default();
        }
        let mut utf16 = Vec::new();
        self.walk(self.source(sequence), |symbol| {
            utf16.push(RawToAddr.convert(self.source(symbol)).try_into().unwrap());
        });
        let string: Arc<str> = String::from_utf16(&utf16).unwrap().into();
        if let Some(cache) = &mut self.cache {
            cache.strings.insert(sequence, Arc::clone(&string));
        }
        string
    }

    /// Visits the symbols of a balanced sequence from left to right, like `RightSequenceWalker` in C#.
    fn walk(&self, sequence: T, mut visit: impl FnMut(T)) {
        let is_symbol = |link| self.target(link) == self.unicode_symbol;
        let mut stack = Vec::new();
        let mut element = sequence;
        if is_symbol(element) {
            return visit(element);
        }
        loop {
            if is_symbol(element) {
                let Some(pair) = stack.pop() else { break };
                let (source, target) = (self.source(pair), self.target(pair));
                for part in [source, target] {
                    if is_symbol(part) {
                        visit(part);
                    }
                }
                element = target;
            } else {
                stack.push(element);
                element = self.source(element);
            }
        }
    }

    fn source(&self, link: T) -> T {
        self.links.get_link(link).unwrap().source
    }

    fn target(&self, link: T) -> T {
        self.links.get_link(link).unwrap().target
    }

    fn set_property(&mut self, object: T, property: T, value: T) {
        let object_property = self.links.get_or_create(object, property).unwrap();
        let any = self.links.constants().any;
        self.links
            .delete_query_with([any, object_property, any], |_, _| Flow::Continue)
            .unwrap();
        self.links.get_or_create(object_property, value).unwrap();
    }

    fn property(&self, object: T, property: T) -> Option<T> {
        let object_property = self.links.search(object, property)?;
        let any = self.links.constants().any;
        self.links
            .single([any, object_property, any])
            .map(|value| value.target)
    }

    fn delete_property(&mut self, object: T, property: T) {
        let object_property = self.links.search(object, property).unwrap();
        let any = self.links.constants().any;
        let value = self.links.single([any, object_property, any]).unwrap();
        self.links.delete(value.index).unwrap();
        self.links.delete(object_property).unwrap();
    }
}

impl<T: LinkReference, D: Doublets<T>> BlogPostsStorage<T> for DoubletsBlogPosts<T, D> {
    fn create(&mut self, post: &BlogPost) -> T {
        let blog_post = self.links.create().unwrap();
        self.links
            .update(blog_post, self.blog_post, blog_post)
            .unwrap();
        let title = self.sequence(&post.title);
        self.set_property(blog_post, self.title, title);
        let content = self.sequence(&post.content);
        self.set_property(blog_post, self.content, content);
        let publication_date = AddrToRaw.convert(T::try_from(post.publication_date).unwrap());
        self.set_property(blog_post, self.publication_date, publication_date);
        blog_post
    }

    fn get(&mut self, id: T) -> Option<BlogPost> {
        let title = self.property(id, self.title)?;
        let content = self.property(id, self.content)?;
        let publication_date = self.property(id, self.publication_date)?;
        Some(BlogPost {
            title: self.string(title),
            content: self.string(content),
            publication_date: RawToAddr.convert(publication_date).try_into().unwrap(),
        })
    }

    fn each(&mut self, mut visit: impl FnMut(T, BlogPost)) {
        let any = self.links.constants().any;
        let mut ids = Vec::new();
        self.links.each_by([any, self.blog_post, any], |post| {
            ids.push(post.index);
            Flow::Continue
        });
        for id in ids {
            let post = self.get(id).unwrap();
            visit(id, post);
        }
    }

    fn delete(&mut self, id: T) {
        for property in [self.title, self.content, self.publication_date] {
            self.delete_property(id, property);
        }
        self.links.delete(id).unwrap();
    }

    fn count(&mut self) -> u64 {
        let any = self.links.constants().any;
        self.links
            .count_by([any, self.blog_post, any])
            .try_into()
            .unwrap()
    }
}
