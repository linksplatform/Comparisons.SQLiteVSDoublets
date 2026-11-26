use doublets::{
    Doublets, Error, Links,
    data::{Flow, LinkReference, LinksConstants, ReadHandler, WriteHandler},
    decorators::{Transaction, TransactionsDecorator, TransitionLog},
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

    /// The storage a [`transaction`](LinksStorage::transaction) hands to its work.
    type Phase<'a>: LinksStorage<T>
    where
        Self: 'a;

    /// Runs one lifecycle phase as a single transaction.
    fn transaction<R>(&mut self, work: impl FnOnce(&mut Self::Phase<'_>) -> R) -> R;
}

pub struct SqliteLinks<T> {
    connection: Connection,
    id_type: PhantomData<T>,
}

impl<T: LinkReference> SqliteLinks<T> {
    pub fn open(path: impl AsRef<Path>) -> Self {
        Self::new(Connection::open(path).unwrap(), "")
    }

    /// Opens the file with a 1 GiB page cache and a 1 GiB memory map, see [`TUNING`].
    pub fn open_tuned(path: impl AsRef<Path>) -> Self {
        Self::new(Connection::open(path).unwrap(), TUNING)
    }

    pub fn in_memory() -> Self {
        Self::new(Connection::open_in_memory().unwrap(), "")
    }

    fn new(connection: Connection, pragmas: &str) -> Self {
        connection.execute_batch(pragmas).unwrap();
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

/// Pragmas of the `_Tuned` SQLite variants, run before the schema is created: a 1 GiB page cache
/// (a negative `cache_size` is in KiB) instead of the default 2 MiB, and reads through a 1 GiB
/// memory map instead of `read` calls. The durability stays the same.
pub const TUNING: &str = "PRAGMA cache_size = -1048576; PRAGMA mmap_size = 1073741824;";

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

    type Phase<'a>
        = Self
    where
        Self: 'a;

    fn transaction<R>(&mut self, work: impl FnOnce(&mut Self) -> R) -> R {
        self.connection.execute_batch("BEGIN IMMEDIATE").unwrap();
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

    type Phase<'a>
        = Self
    where
        Self: 'a;

    fn transaction<R>(&mut self, work: impl FnOnce(&mut Self) -> R) -> R {
        work(self)
    }
}

/// A store behind [`TransactionsDecorator`]: every phase is one Doublets transaction, begun on its
/// first write and committed at its end, so phases that only read write nothing to the journal.
/// Writes outside a phase are committed one by one by the decorator.
pub struct TransactionalLinks<T: LinkReference, D: Doublets<T>, J: TransitionLog<T>> {
    links: DoubletsLinks<T, TransactionsDecorator<T, D, J>>,
}

impl<T: LinkReference, D: Doublets<T>, J: TransitionLog<T>> TransactionalLinks<T, D, J> {
    pub fn new(store: TransactionsDecorator<T, D, J>) -> Self {
        Self {
            links: DoubletsLinks::new(store),
        }
    }

    /// The decorator, for inspecting its journal and transitions.
    pub fn store(&self) -> &TransactionsDecorator<T, D, J> {
        &self.links.store
    }
}

impl<T: LinkReference, D: Doublets<T> + 'static, J: TransitionLog<T> + 'static> LinksStorage<T>
    for TransactionalLinks<T, D, J>
{
    fn create(&mut self, from: T, to: T) -> T {
        self.links.create(from, to)
    }

    fn update(&mut self, id: T, from: T, to: T) {
        self.links.update(id, from, to);
    }

    fn delete(&mut self, id: T) {
        self.links.delete(id);
    }

    fn get(&self, id: T) -> Option<Link<T>> {
        self.links.get(id)
    }

    fn search(&self, from: T, to: T) -> Option<T> {
        self.links.search(from, to)
    }

    fn each(&self, visit: impl FnMut(Link<T>)) {
        self.links.each(visit);
    }

    fn each_with_from(&self, from: T, visit: impl FnMut(Link<T>)) {
        self.links.each_with_from(from, visit);
    }

    fn each_with_to(&self, to: T, visit: impl FnMut(Link<T>)) {
        self.links.each_with_to(to, visit);
    }

    fn count(&self) -> u64 {
        self.links.count()
    }

    type Phase<'a>
        = DoubletsLinks<T, LazyTransaction<'a, T, D, J>>
    where
        Self: 'a;

    fn transaction<R>(&mut self, work: impl FnOnce(&mut Self::Phase<'_>) -> R) -> R {
        let mut phase = DoubletsLinks::new(LazyTransaction::Idle(&mut self.links.store));
        let result = work(&mut phase);
        if let LazyTransaction::Active(transaction) = phase.store {
            transaction.commit().unwrap();
        }
        result
    }
}

/// The store of one phase: reads go to the decorator, and the first write begins the transaction
/// with [`TransactionsDecorator::begin_transaction`], which then takes every later operation.
pub enum LazyTransaction<'a, T: LinkReference, D: Doublets<T>, J: TransitionLog<T>> {
    Idle(&'a mut TransactionsDecorator<T, D, J>),
    Active(Transaction<'a, T, D, J>),
    /// Only seen while [`LazyTransaction::Idle`] turns into [`LazyTransaction::Active`].
    Beginning,
}

impl<'a, T: LinkReference, D: Doublets<T>, J: TransitionLog<T>> LazyTransaction<'a, T, D, J> {
    fn store(&self) -> &TransactionsDecorator<T, D, J> {
        match self {
            Self::Idle(store) => store,
            Self::Active(transaction) => transaction,
            Self::Beginning => unreachable!("a transaction failed to begin"),
        }
    }

    fn transaction(&mut self) -> &mut Transaction<'a, T, D, J> {
        if let Self::Idle(_) = self {
            let Self::Idle(store) = std::mem::replace(self, Self::Beginning) else {
                unreachable!()
            };
            *self = Self::Active(store.begin_transaction().unwrap());
        }
        match self {
            Self::Active(transaction) => transaction,
            _ => unreachable!("a transaction failed to begin"),
        }
    }
}

impl<T: LinkReference, D: Doublets<T>, J: TransitionLog<T>> Links<T>
    for LazyTransaction<'_, T, D, J>
{
    fn constants(&self) -> &LinksConstants<T> {
        self.store().constants()
    }

    fn count_links(&self, query: &[T]) -> T {
        self.store().count_links(query)
    }

    fn create_links(
        &mut self,
        query: &[T],
        handler: WriteHandler<'_, T>,
    ) -> Result<Flow, Error<T>> {
        self.transaction().create_links(query, handler)
    }

    fn each_links(&self, query: &[T], handler: ReadHandler<'_, T>) -> Flow {
        self.store().each_links(query, handler)
    }

    fn update_links(
        &mut self,
        query: &[T],
        change: &[T],
        handler: WriteHandler<'_, T>,
    ) -> Result<Flow, Error<T>> {
        self.transaction().update_links(query, change, handler)
    }

    fn delete_links(
        &mut self,
        query: &[T],
        handler: WriteHandler<'_, T>,
    ) -> Result<Flow, Error<T>> {
        self.transaction().delete_links(query, handler)
    }
}

impl<T: LinkReference, D: Doublets<T>, J: TransitionLog<T>> Doublets<T>
    for LazyTransaction<'_, T, D, J>
{
    fn get_link(&self, index: T) -> Option<doublets::Link<T>> {
        self.store().get_link(index)
    }
}
