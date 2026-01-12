//! SQLite implementation for links storage

use crate::{Link, Links};
use rusqlite::{params, Connection};
use std::path::Path;

/// SQLite-based links storage
pub struct SqliteLinks {
    conn: Connection,
    next_id: u64,
}

impl SqliteLinks {
    /// Create a new SQLite links storage with in-memory database
    pub fn new_memory() -> Self {
        let conn = Connection::open_in_memory().expect("Failed to open in-memory SQLite database");
        Self::init(conn)
    }

    /// Create a new SQLite links storage with file-based database
    pub fn new_file<P: AsRef<Path>>(path: P) -> Self {
        let conn = Connection::open(path).expect("Failed to open SQLite database file");
        Self::init(conn)
    }

    fn init(conn: Connection) -> Self {
        conn.execute(
            "CREATE TABLE IF NOT EXISTS links (
                id INTEGER PRIMARY KEY,
                source INTEGER NOT NULL,
                target INTEGER NOT NULL
            )",
            [],
        )
        .expect("Failed to create links table");

        // Create indexes for efficient queries
        conn.execute(
            "CREATE INDEX IF NOT EXISTS idx_source ON links(source)",
            [],
        )
        .expect("Failed to create source index");

        conn.execute(
            "CREATE INDEX IF NOT EXISTS idx_target ON links(target)",
            [],
        )
        .expect("Failed to create target index");

        conn.execute(
            "CREATE INDEX IF NOT EXISTS idx_source_target ON links(source, target)",
            [],
        )
        .expect("Failed to create source_target index");

        // Get the max ID to continue from
        let next_id: u64 = conn
            .query_row("SELECT COALESCE(MAX(id), 0) + 1 FROM links", [], |row| {
                row.get(0)
            })
            .unwrap_or(1);

        Self { conn, next_id }
    }

    /// Drop all tables and recreate them
    pub fn reset(&mut self) {
        self.conn
            .execute("DROP TABLE IF EXISTS links", [])
            .expect("Failed to drop links table");
        self.conn
            .execute(
                "CREATE TABLE links (
                id INTEGER PRIMARY KEY,
                source INTEGER NOT NULL,
                target INTEGER NOT NULL
            )",
                [],
            )
            .expect("Failed to create links table");

        self.conn
            .execute("CREATE INDEX idx_source ON links(source)", [])
            .expect("Failed to create source index");

        self.conn
            .execute("CREATE INDEX idx_target ON links(target)", [])
            .expect("Failed to create target index");

        self.conn
            .execute(
                "CREATE INDEX idx_source_target ON links(source, target)",
                [],
            )
            .expect("Failed to create source_target index");

        self.next_id = 1;
    }
}

impl Links for SqliteLinks {
    fn create(&mut self, source: u64, target: u64) -> u64 {
        let id = self.next_id;
        self.conn
            .execute(
                "INSERT INTO links (id, source, target) VALUES (?1, ?2, ?3)",
                params![id as i64, source as i64, target as i64],
            )
            .expect("Failed to insert link");
        self.next_id += 1;
        id
    }

    fn update(&mut self, id: u64, source: u64, target: u64) {
        self.conn
            .execute(
                "UPDATE links SET source = ?1, target = ?2 WHERE id = ?3",
                params![source as i64, target as i64, id as i64],
            )
            .expect("Failed to update link");
    }

    fn delete(&mut self, id: u64) {
        self.conn
            .execute("DELETE FROM links WHERE id = ?1", params![id as i64])
            .expect("Failed to delete link");
    }

    fn delete_all(&mut self) {
        self.conn
            .execute("DELETE FROM links", [])
            .expect("Failed to delete all links");
        self.next_id = 1;
    }

    fn query_all(&self) -> Vec<Link> {
        let mut stmt = self
            .conn
            .prepare("SELECT id, source, target FROM links")
            .expect("Failed to prepare query");

        stmt.query_map([], |row| {
            Ok(Link {
                id: row.get::<_, i64>(0)? as u64,
                source: row.get::<_, i64>(1)? as u64,
                target: row.get::<_, i64>(2)? as u64,
            })
        })
        .expect("Failed to query links")
        .filter_map(|r| r.ok())
        .collect()
    }

    fn query_by_id(&self, id: u64) -> Option<Link> {
        self.conn
            .query_row(
                "SELECT id, source, target FROM links WHERE id = ?1",
                params![id as i64],
                |row| {
                    Ok(Link {
                        id: row.get::<_, i64>(0)? as u64,
                        source: row.get::<_, i64>(1)? as u64,
                        target: row.get::<_, i64>(2)? as u64,
                    })
                },
            )
            .ok()
    }

    fn query_by_source(&self, source: u64) -> Vec<Link> {
        let mut stmt = self
            .conn
            .prepare("SELECT id, source, target FROM links WHERE source = ?1")
            .expect("Failed to prepare query");

        stmt.query_map(params![source as i64], |row| {
            Ok(Link {
                id: row.get::<_, i64>(0)? as u64,
                source: row.get::<_, i64>(1)? as u64,
                target: row.get::<_, i64>(2)? as u64,
            })
        })
        .expect("Failed to query links")
        .filter_map(|r| r.ok())
        .collect()
    }

    fn query_by_target(&self, target: u64) -> Vec<Link> {
        let mut stmt = self
            .conn
            .prepare("SELECT id, source, target FROM links WHERE target = ?1")
            .expect("Failed to prepare query");

        stmt.query_map(params![target as i64], |row| {
            Ok(Link {
                id: row.get::<_, i64>(0)? as u64,
                source: row.get::<_, i64>(1)? as u64,
                target: row.get::<_, i64>(2)? as u64,
            })
        })
        .expect("Failed to query links")
        .filter_map(|r| r.ok())
        .collect()
    }

    fn query_by_source_target(&self, source: u64, target: u64) -> Vec<Link> {
        let mut stmt = self
            .conn
            .prepare("SELECT id, source, target FROM links WHERE source = ?1 AND target = ?2")
            .expect("Failed to prepare query");

        stmt.query_map(params![source as i64, target as i64], |row| {
            Ok(Link {
                id: row.get::<_, i64>(0)? as u64,
                source: row.get::<_, i64>(1)? as u64,
                target: row.get::<_, i64>(2)? as u64,
            })
        })
        .expect("Failed to query links")
        .filter_map(|r| r.ok())
        .collect()
    }

    fn count(&self) -> usize {
        self.conn
            .query_row("SELECT COUNT(*) FROM links", [], |row| row.get::<_, i64>(0))
            .unwrap_or(0) as usize
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_create_and_query() {
        let mut db = SqliteLinks::new_memory();
        let id = db.create(1, 2);
        assert_eq!(id, 1);

        let link = db.query_by_id(id).unwrap();
        assert_eq!(link.source, 1);
        assert_eq!(link.target, 2);
    }

    #[test]
    fn test_create_point() {
        let mut db = SqliteLinks::new_memory();
        let id = db.create_point();
        assert_eq!(id, 1);

        let link = db.query_by_id(id).unwrap();
        assert_eq!(link.source, id);
        assert_eq!(link.target, id);
    }

    #[test]
    fn test_update() {
        let mut db = SqliteLinks::new_memory();
        let id = db.create(1, 2);
        db.update(id, 3, 4);

        let link = db.query_by_id(id).unwrap();
        assert_eq!(link.source, 3);
        assert_eq!(link.target, 4);
    }

    #[test]
    fn test_delete() {
        let mut db = SqliteLinks::new_memory();
        let id = db.create(1, 2);
        db.delete(id);
        assert!(db.query_by_id(id).is_none());
    }

    #[test]
    fn test_query_by_source() {
        let mut db = SqliteLinks::new_memory();
        db.create(1, 2);
        db.create(1, 3);
        db.create(2, 3);

        let links = db.query_by_source(1);
        assert_eq!(links.len(), 2);
    }

    #[test]
    fn test_query_by_target() {
        let mut db = SqliteLinks::new_memory();
        db.create(1, 3);
        db.create(2, 3);
        db.create(2, 4);

        let links = db.query_by_target(3);
        assert_eq!(links.len(), 2);
    }
}
