//! Minimal reproductions of a `doublets` 0.5.0 split store bug.
//!
//! `split::Store::update_links` detaches the link and only then asks `is_virtual(new_source)`,
//! which is `is_unused`: `size_as_target == 0 && source != 0`. Right after the detach that is true
//! for the link itself, so updating a link to reference itself attaches it to the external
//! (header) trees. A later delete looks for it in its own internal trees and panics with
//! "index out of bounds" in `InternalSourcesRecursionlessTree::detach_core`.
//! Creating such a link works, because a fresh link has `source == 0`.
//!
//! Usage: `cargo run --release -- <unit|split|dump> <scenario index>`, exit code 101 is the panic.
use doublets::{Doublets, mem::Global, split, unit};

type Step = (&'static str, u32, u32, u32);

fn run<D: Doublets<u32>>(mut store: D, steps: &[Step]) -> u32 {
    for &(operation, id, from, to) in steps {
        match operation {
            "create" => assert_eq!(store.create_link(from, to).unwrap(), id),
            "update" => drop(store.update(id, from, to).unwrap()),
            _ => drop(store.delete(id).unwrap()),
        }
    }
    store.count()
}

fn main() {
    let scenarios: [(&str, Vec<Step>); 6] = [
        ("point: update to itself, delete", vec![("create", 1, 1, 1), ("update", 1, 1, 1), ("delete", 1, 0, 0)]),
        ("point: delete", vec![("create", 1, 1, 1), ("delete", 1, 0, 0)]),
        (
            "swap a link that references itself, delete",
            vec![("create", 1, 1, 1), ("create", 2, 1, 2), ("update", 2, 2, 1), ("delete", 2, 0, 0), ("delete", 1, 0, 0)],
        ),
        (
            "swap a link that references itself, no delete",
            vec![("create", 1, 1, 1), ("create", 2, 1, 2), ("update", 2, 2, 1)],
        ),
        (
            "swap links without self references, delete",
            vec![
                ("create", 1, 1, 1),
                ("create", 2, 2, 2),
                ("create", 3, 1, 2),
                ("create", 4, 3, 1),
                ("update", 3, 2, 1),
                ("update", 4, 1, 3),
                ("delete", 4, 0, 0),
                ("delete", 3, 0, 0),
                ("delete", 1, 0, 0),
            ],
        ),
        (
            "create a link that references itself, delete",
            vec![("create", 1, 1, 1), ("create", 2, 1, 2), ("create", 3, 3, 1), ("delete", 2, 0, 0), ("delete", 3, 0, 0), ("delete", 1, 0, 0)],
        ),
    ];
    let kind = std::env::args().nth(1).unwrap_or_default();
    let scenario: usize = std::env::args().nth(2).unwrap().parse().unwrap();
    let (name, steps) = &scenarios[scenario];
    print!("{kind:5} {name:40} ");
    let count = match kind.as_str() {
        "unit" => run(unit::Store::<u32, _>::new(Global::new()).unwrap(), steps),
        "dump" => {
            let mut store = split::Store::<u32, _, _>::new(Global::new(), Global::new()).unwrap();
            for &(operation, id, from, to) in steps {
                match operation {
                    "create" => drop(store.create_link(from, to).unwrap()),
                    "update" => drop(store.update(id, from, to).unwrap()),
                    _ => drop(store.delete(id).unwrap()),
                }
                eprintln!("after {operation} {id} {from} {to}:");
                for link in 1..=3 {
                    eprintln!("  {link}: {:?} {:?} unused={}", store.get_data_part(link), store.get_index_part(link), store.is_unused(link));
                }
            }
            store.count()
        }
        _ => run(split::Store::<u32, _, _>::new(Global::new(), Global::new()).unwrap(), steps),
    };
    println!("ok, count = {count}");
}
