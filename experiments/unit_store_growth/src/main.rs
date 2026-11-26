//! Minimal reproduction of a `doublets` 0.5.0 and `platform-mem` 0.3.0 mismatch.
//!
//! `doublets::mem::resize_mem` returns what `RawMem::grow_filled` returns, and the stores use it as
//! their whole memory. `platform-mem` returns only the grown part. `Store::init` grows the memory
//! from 8,192 to 2^20 links, so a fresh store sees 1,040,384 links, while its header says that
//! 2^20 − 1 are reserved: creating link 1,040,384 panics with "Data part should be in data memory"
//! (or "index out of bounds" in a tree). A later growth would leave the store with an empty memory.
//!
//! With doublets 0.5.0, `bare` panicked and `whole` used the benchmark's former `memory::Whole`
//! wrapper, which returned the whole memory, to create 2^21 + 1 links, growing the memory twice.
//! doublets 0.21.0 fixes it: both print `Ok(Ok(2097153))`, so the wrapper is gone and `whole`
//! now runs the benchmark's own memory, which is the bare `Global`.
//!
//! Usage: `cargo run --release -- <bare|whole>`.
use doublets::{Doublets, Error, mem::Global, unit};
use sqlite_vs_doublets::memory::volatile;
use std::{env, panic};

const LINKS: u64 = (1 << 21) + 1;

fn create(mut store: impl Doublets<u64>) -> Result<u64, Error<u64>> {
    for i in 1..=LINKS {
        if i % 100_000 == 0 || (1_040_380..=1_040_390).contains(&i) {
            eprintln!("creating link {i}");
        }
        store.create_link(1, i.min(2))?;
    }
    assert_eq!(store.get_link(1).unwrap().target, 1);
    Ok(store.count())
}

fn main() {
    let result = panic::catch_unwind(|| match env::args().nth(1).as_deref() {
        Some("bare") => create(unit::Store::<u64, _>::new(Global::new()).unwrap()),
        Some("whole") => create(unit::Store::<u64, _>::new(volatile()).unwrap()),
        _ => panic!("usage: unit_store_growth <bare|whole>"),
    });
    println!("{result:?}");
}
