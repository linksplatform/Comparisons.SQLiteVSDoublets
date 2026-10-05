//! Times 100,000 inserts of a blog post in one transaction with each way of getting the new id.
//! Results with rusqlite 0.40.2 (SQLite 3.53.2): `RETURNING id` makes inserts about 3x slower, so both
//! languages read `sqlite3_last_insert_rowid` instead (C# through SQLitePCLRaw).
//!   execute + last_insert_rowid: 1968-2181 ns/insert
//!   query_row RETURNING id:      5804-6202 ns/insert
//!   query + drain RETURNING id:  5862-6105 ns/insert
use rusqlite::Connection;
use std::time::Instant;

const N: i64 = 100_000;

fn main() {
    let content = "Lorem ipsum ".repeat(60);
    for (name, sql) in [
        (
            "execute + last_insert_rowid",
            "INSERT INTO posts (title, content) VALUES (?1, ?2)",
        ),
        (
            "query_row RETURNING id",
            "INSERT INTO posts (title, content) VALUES (?1, ?2) RETURNING id",
        ),
        (
            "query + drain RETURNING id",
            "INSERT INTO posts (title, content) VALUES (?1, ?2) RETURNING id",
        ),
    ] {
        for _ in 0..3 {
            let connection = Connection::open_in_memory().unwrap();
            connection
                .execute_batch("CREATE TABLE posts (id INTEGER PRIMARY KEY, title TEXT NOT NULL, content TEXT NOT NULL); BEGIN IMMEDIATE")
                .unwrap();
            let mut statement = connection.prepare_cached(sql).unwrap();
            let start = Instant::now();
            for i in 1..=N {
                let title = format!("Blog post {i}");
                let id: i64 = match name {
                    "execute + last_insert_rowid" => {
                        statement.execute((&title, &content)).unwrap();
                        connection.last_insert_rowid()
                    }
                    "query_row RETURNING id" => statement
                        .query_row((&title, &content), |row| row.get(0))
                        .unwrap(),
                    _ => {
                        let mut rows = statement.query((&title, &content)).unwrap();
                        let id = rows.next().unwrap().unwrap().get(0).unwrap();
                        assert!(rows.next().unwrap().is_none());
                        id
                    }
                };
                assert_eq!(id, i);
            }
            println!(
                "{name}: {:.0} ns/insert",
                start.elapsed().as_nanos() as f64 / N as f64
            );
        }
    }
}
