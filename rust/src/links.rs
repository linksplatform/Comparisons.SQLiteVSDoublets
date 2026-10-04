use doublets::{
    Doublets,
    data::{Flow, LinkReference},
};
use rusqlite::{Connection, OptionalExtension, params};
use std::{marker::PhantomData, path::Path};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Link<T> {
    pub id: T,
    pub from: T,
    pub to: T,
}

pub trait LinksStorage<T> {
    fn create(&mut self, from: T, to: T) -> T;
    fn update(&mut self, id: T, from: T, to: T);
    fn delete(&mut self, id: T);
    fn get(&self, id: T) -> Option<Link<T>>;
    fn search(&self, from: T, to: T) -> Option<T>;
    fn each(&self, visit: impl FnMut(Link<T>));
    fn each_with_from(&self, from: T, visit: impl FnMut(Link<T>));
    fn each_with_to(&self, to: T, visit: impl FnMut(Link<T>));
    fn count(&self) -> u64;

    fn transaction<R>(&mut self, work: impl FnOnce(&mut Self) -> R) -> R {
        work(self)
    }
}

pub struct SqliteLinks<T> {
    connection: Connection,
    id_type: PhantomData<T>,
}

impl<T: LinkReference> SqliteLinks<T> {
    pub fn open(path: impl AsRef<Path>) -> Self {
        Self::new(Connection::open(path).unwrap())
    }

    pub fn in_memory() -> Self {
        Self::new(Connection::open_in_memory().unwrap())
    }

    fn new(connection: Connection) -> Self {
        connection
            .execute_batch(
                r#"
                CREATE TABLE links (id INTEGER PRIMARY KEY, "from" INTEGER NOT NULL, "to" INTEGER NOT NULL);
                CREATE INDEX links_from_to ON links ("from", "to");
                CREATE INDEX links_to_from ON links ("to", "from");
                "#,
            )
            .unwrap();
        Self {
            connection,
            id_type: PhantomData,
        }
    }

    fn each_where(
        &self,
        sql: &str,
        parameters: impl rusqlite::Params,
        mut visit: impl FnMut(Link<T>),
    ) {
        let mut statement = self.connection.prepare_cached(sql).unwrap();
        let mut rows = statement.query(parameters).unwrap();
        while let Some(row) = rows.next().unwrap() {
            visit(Link {
                id: id(row.get(0).unwrap()),
                from: id(row.get(1).unwrap()),
                to: id(row.get(2).unwrap()),
            });
        }
    }
}

fn sql<T: LinkReference>(id: T) -> i64 {
    id.try_into().unwrap()
}

fn id<T: LinkReference>(sql: i64) -> T {
    T::try_from(sql).unwrap()
}

impl<T: LinkReference> LinksStorage<T> for SqliteLinks<T> {
    fn create(&mut self, from: T, to: T) -> T {
        let mut statement = self
            .connection
            .prepare_cached(r#"INSERT INTO links ("from", "to") VALUES (?1, ?2)"#)
            .unwrap();
        statement.execute(params![sql(from), sql(to)]).unwrap();
        id(self.connection.last_insert_rowid())
    }

    fn update(&mut self, link: T, from: T, to: T) {
        let mut statement = self
            .connection
            .prepare_cached(r#"UPDATE links SET "from" = ?2, "to" = ?3 WHERE id = ?1"#)
            .unwrap();
        statement
            .execute(params![sql(link), sql(from), sql(to)])
            .unwrap();
    }

    fn delete(&mut self, link: T) {
        let mut statement = self
            .connection
            .prepare_cached("DELETE FROM links WHERE id = ?1")
            .unwrap();
        statement.execute([sql(link)]).unwrap();
    }

    fn get(&self, link: T) -> Option<Link<T>> {
        let mut statement = self
            .connection
            .prepare_cached(r#"SELECT "from", "to" FROM links WHERE id = ?1"#)
            .unwrap();
        statement
            .query_row([sql(link)], |row| {
                Ok(Link {
                    id: link,
                    from: id(row.get(0)?),
                    to: id(row.get(1)?),
                })
            })
            .optional()
            .unwrap()
    }

    fn search(&self, from: T, to: T) -> Option<T> {
        let mut statement = self
            .connection
            .prepare_cached(r#"SELECT id FROM links WHERE "from" = ?1 AND "to" = ?2"#)
            .unwrap();
        statement
            .query_row([sql(from), sql(to)], |row| row.get(0).map(id))
            .optional()
            .unwrap()
    }

    fn each(&self, visit: impl FnMut(Link<T>)) {
        self.each_where(r#"SELECT id, "from", "to" FROM links"#, [], visit);
    }

    fn each_with_from(&self, from: T, visit: impl FnMut(Link<T>)) {
        self.each_where(
            r#"SELECT id, "from", "to" FROM links WHERE "from" = ?1"#,
            [sql(from)],
            visit,
        );
    }

    fn each_with_to(&self, to: T, visit: impl FnMut(Link<T>)) {
        self.each_where(
            r#"SELECT id, "from", "to" FROM links WHERE "to" = ?1"#,
            [sql(to)],
            visit,
        );
    }

    fn count(&self) -> u64 {
        self.connection
            .query_row("SELECT COUNT(*) FROM links", [], |row| row.get(0))
            .unwrap()
    }

    fn transaction<R>(&mut self, work: impl FnOnce(&mut Self) -> R) -> R {
        self.connection.execute_batch("BEGIN").unwrap();
        let result = work(self);
        self.connection.execute_batch("COMMIT").unwrap();
        result
    }
}

fn link<T: LinkReference>(link: doublets::Link<T>) -> Link<T> {
    Link {
        id: link.index,
        from: link.source,
        to: link.target,
    }
}

pub struct DoubletsLinks<T, D> {
    store: D,
    id_type: PhantomData<T>,
}

impl<T: LinkReference, D: Doublets<T>> DoubletsLinks<T, D> {
    pub fn new(store: D) -> Self {
        Self {
            store,
            id_type: PhantomData,
        }
    }
}

impl<T: LinkReference, D: Doublets<T>> LinksStorage<T> for DoubletsLinks<T, D> {
    fn create(&mut self, from: T, to: T) -> T {
        self.store.create_link(from, to).unwrap()
    }

    fn update(&mut self, id: T, from: T, to: T) {
        // doublets 0.5.0 split stores lose a link updated to reference itself unless it starts
        // from the empty state `create_link` uses, see experiments/split_store_delete
        if from == id || to == id {
            self.store
                .update(id, T::from_byte(0), T::from_byte(0))
                .unwrap();
        }
        self.store.update(id, from, to).unwrap();
    }

    fn delete(&mut self, id: T) {
        self.store.delete(id).unwrap();
    }

    fn get(&self, id: T) -> Option<Link<T>> {
        self.store.get_link(id).map(link)
    }

    fn search(&self, from: T, to: T) -> Option<T> {
        self.store.search(from, to)
    }

    fn each(&self, mut visit: impl FnMut(Link<T>)) {
        self.store.each(|found| {
            visit(link(found));
            Flow::Continue
        });
    }

    fn each_with_from(&self, from: T, mut visit: impl FnMut(Link<T>)) {
        let any = self.store.constants().any;
        self.store.each_by([any, from, any], |found| {
            visit(link(found));
            Flow::Continue
        });
    }

    fn each_with_to(&self, to: T, mut visit: impl FnMut(Link<T>)) {
        let any = self.store.constants().any;
        self.store.each_by([any, any, to], |found| {
            visit(link(found));
            Flow::Continue
        });
    }

    fn count(&self) -> u64 {
        self.store.count().try_into().unwrap()
    }
}
