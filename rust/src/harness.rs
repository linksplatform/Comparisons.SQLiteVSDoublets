//! Runs every storage through the same lifecycle on a fresh store per repetition:
//! a timed batch per operation, each validated against the expected count and checksum.

use crate::{
    dataset::{self, blog_post, link, mix, scattered},
    links::{DoubletsLinks, Link, LinksStorage, SqliteLinks},
    memory::{Whole, volatile},
    objects::{BlogPostsStorage, DoubletsBlogPosts, SqliteBlogPosts},
};
use doublets::{
    data::{LinkReference, LinksConstants},
    mem::FileMapped,
    split, unit,
};
use serde_json::{Value, json};
use std::{
    fs,
    path::{Path, PathBuf},
    time::{Duration, Instant},
};

pub const LINKS_VARIANTS: [&str; 6] = [
    "SQLite_Memory",
    "SQLite_File",
    "Doublets_United_Volatile",
    "Doublets_United_NonVolatile",
    "Doublets_Split_Volatile",
    "Doublets_Split_NonVolatile",
];

pub const OBJECTS_VARIANTS: [&str; 10] = [
    "SQLite_Memory",
    "SQLite_File",
    "Doublets_United_Volatile_Cached",
    "Doublets_United_Volatile_Uncached",
    "Doublets_United_NonVolatile_Cached",
    "Doublets_United_NonVolatile_Uncached",
    "Doublets_Split_Volatile_Cached",
    "Doublets_Split_Volatile_Uncached",
    "Doublets_Split_NonVolatile_Cached",
    "Doublets_Split_NonVolatile_Uncached",
];

pub const WARM_UP_SIZE: u64 = 10_000;

type Timings = Vec<(&'static str, Duration)>;

/// Count and order-sensitive checksum of the records an operation has seen.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct Tally {
    pub count: u64,
    pub checksum: u64,
}

impl Tally {
    fn add(&mut self, checksum: u64) {
        self.count += 1;
        self.checksum = self.checksum.wrapping_add(checksum);
    }

    fn add_link<T: LinkReference>(&mut self, link: Link<T>) {
        self.add(dataset::link_checksum(
            uint(link.id),
            uint(link.from),
            uint(link.to),
        ));
    }

    fn of(count: u64, checksum: u64) -> Self {
        Self { count, checksum }
    }
}

fn timed(
    timings: &mut Timings,
    operation: &'static str,
    expected: Tally,
    work: impl FnOnce() -> Tally,
) {
    let start = Instant::now();
    let actual = work();
    timings.push((operation, start.elapsed()));
    assert_eq!(actual, expected, "{operation} returned unexpected records");
}

fn int<T: LinkReference>(value: u64) -> T {
    T::try_from(value).unwrap()
}

fn uint<T: LinkReference>(value: T) -> u64 {
    value.try_into().unwrap()
}

pub fn links_lifecycle<T: LinkReference, S: LinksStorage<T>>(
    storage: &mut S,
    n: u64,
    created: &mut dyn FnMut(),
) -> Timings {
    let expected = Tally::of(n, dataset::links_checksum(n, false));
    let mut timings = Timings::new();

    timed(&mut timings, "create", expected, || {
        storage.transaction(|links| {
            let mut tally = Tally::default();
            for i in 1..=n {
                let (from, to) = link(i);
                let id = links.create(int(from), int(to));
                tally.add_link(Link {
                    id,
                    from: int(from),
                    to: int(to),
                });
            }
            tally
        })
    });
    created();

    timed(&mut timings, "query_all", expected, || {
        storage.transaction(|links| {
            let mut tally = Tally::default();
            links.each(|link| tally.add_link(link));
            tally
        })
    });
    timed(&mut timings, "query_by_id", expected, || {
        storage.transaction(|links| {
            let mut tally = Tally::default();
            for id in scattered(n) {
                tally.add_link(links.get(int(id)).unwrap());
            }
            tally
        })
    });
    timed(&mut timings, "query_by_from_to", expected, || {
        storage.transaction(|links| {
            let mut tally = Tally::default();
            for i in scattered(n) {
                let (from, to) = link(i);
                let id = links.search(int(from), int(to)).unwrap();
                tally.add_link(Link {
                    id,
                    from: int(from),
                    to: int(to),
                });
            }
            tally
        })
    });
    timed(&mut timings, "query_by_from", expected, || {
        storage.transaction(|links| {
            let mut tally = Tally::default();
            for from in scattered(n) {
                links.each_with_from(int(from), |link| tally.add_link(link));
            }
            tally
        })
    });
    timed(&mut timings, "query_by_to", expected, || {
        storage.transaction(|links| {
            let mut tally = Tally::default();
            for to in scattered(n) {
                links.each_with_to(int(to), |link| tally.add_link(link));
            }
            tally
        })
    });

    let swapped = Tally::of(n, dataset::links_checksum(n, true));
    timed(&mut timings, "update", swapped, || {
        storage.transaction(|links| {
            let mut tally = Tally::default();
            for id in scattered(n) {
                let (from, to) = link(id);
                links.update(int(id), int(to), int(from));
                tally.add(dataset::link_checksum(id, to, from));
            }
            tally
        })
    });
    let mut stored = Tally::default();
    storage.each(|link| stored.add_link(link));
    assert_eq!(stored, swapped, "update did not swap every link");

    timed(&mut timings, "delete", Tally::of(n, 0), || {
        storage.transaction(|links| {
            let mut tally = Tally::default();
            for id in scattered(n) {
                links.delete(int(id));
                tally.add(0);
            }
            tally
        })
    });
    assert_eq!(storage.count(), 0, "delete left links behind");
    timings
}

pub fn objects_lifecycle<T: LinkReference, S: BlogPostsStorage<T>>(
    storage: &mut S,
    n: u64,
    created: &mut dyn FnMut(),
) -> Timings {
    let expected = Tally::of(n, dataset::blog_posts_checksum(n));
    let mut timings = Timings::new();
    let mut ids = Vec::with_capacity(n as usize);

    timed(&mut timings, "create", expected, || {
        storage.transaction(|posts| {
            let mut tally = Tally::default();
            for i in 1..=n {
                let post = blog_post(i);
                ids.push(posts.create(&post));
                tally.add(post.checksum());
            }
            tally
        })
    });
    created();

    timed(&mut timings, "read_all", expected, || {
        storage.transaction(|posts| {
            let mut tally = Tally::default();
            posts.each(|_, post| tally.add(post.checksum()));
            tally
        })
    });
    timed(
        &mut timings,
        "read_by_id",
        Tally::of(n, dataset::blog_posts_by_number_checksum(n)),
        || {
            storage.transaction(|posts| {
                let mut tally = Tally::default();
                for i in scattered(n) {
                    let post = posts.get(ids[i as usize - 1]).unwrap();
                    tally.add(mix(i, post.checksum()));
                }
                tally
            })
        },
    );
    timed(&mut timings, "delete", Tally::of(n, 0), || {
        storage.transaction(|posts| {
            let mut tally = Tally::default();
            for i in scattered(n) {
                posts.delete(ids[i as usize - 1]);
                tally.add(0);
            }
            tally
        })
    });
    assert_eq!(storage.count(), 0, "delete left blog posts behind");
    timings
}

pub struct Measurement {
    pub variant: &'static str,
    pub file_bytes: Option<u64>,
    pub operations: Vec<(&'static str, Vec<f64>)>,
}

impl Measurement {
    pub fn to_json(&self) -> Value {
        let operations: serde_json::Map<String, Value> = self
            .operations
            .iter()
            .map(|(operation, samples)| {
                let mut sorted = samples.clone();
                sorted.sort_by(f64::total_cmp);
                let middle = sorted.len() / 2;
                let median = if sorted.len() % 2 == 0 {
                    (sorted[middle - 1] + sorted[middle]) / 2.0
                } else {
                    sorted[middle]
                };
                let summary = json!({
                    "median_ns": median,
                    "min_ns": sorted[0],
                    "max_ns": sorted[sorted.len() - 1],
                    "samples_ns": samples,
                });
                (operation.to_string(), summary)
            })
            .collect();
        json!({ "variant": self.variant, "file_bytes": self.file_bytes, "operations": operations })
    }
}

fn directory_bytes(directory: &Path) -> u64 {
    fs::read_dir(directory)
        .unwrap()
        .map(|entry| entry.unwrap().metadata().unwrap().len())
        .sum()
}

/// Times `lifecycle` on `repetitions` fresh stores opened in empty directories, after one discarded warm-up.
fn measure<S>(
    variant: &'static str,
    n: u64,
    repetitions: usize,
    directory: &Path,
    open: impl Fn(&Path) -> S,
    lifecycle: fn(&mut S, u64, &mut dyn FnMut()) -> Timings,
) -> Measurement {
    let mut measurement = Measurement {
        variant,
        file_bytes: None,
        operations: Vec::new(),
    };
    for repetition in 0..=repetitions {
        let size = if repetition == 0 {
            n.min(WARM_UP_SIZE)
        } else {
            n
        };
        let workspace = directory.join(format!("{variant}-{repetition}"));
        fs::create_dir_all(&workspace).unwrap();
        let mut storage = open(&workspace);
        let mut file_bytes = 0;
        let timings = lifecycle(&mut storage, size, &mut || {
            file_bytes = directory_bytes(&workspace)
        });
        drop(storage);
        fs::remove_dir_all(&workspace).unwrap();
        if repetition == 0 {
            continue;
        }
        eprintln!("{variant} #{repetition}: {}", summary(&timings, n));
        measurement.file_bytes = (file_bytes > 0).then_some(file_bytes);
        for (operation, elapsed) in timings {
            let ns_per_operation = elapsed.as_nanos() as f64 / n as f64;
            match measurement
                .operations
                .iter_mut()
                .find(|(name, _)| *name == operation)
            {
                Some((_, samples)) => samples.push(ns_per_operation),
                None => measurement
                    .operations
                    .push((operation, vec![ns_per_operation])),
            }
        }
    }
    measurement
}

fn summary(timings: &Timings, n: u64) -> String {
    timings
        .iter()
        .map(|(operation, elapsed)| {
            format!(
                "{operation} {:.0} ns/op",
                elapsed.as_nanos() as f64 / n as f64
            )
        })
        .collect::<Vec<_>>()
        .join(", ")
}

fn file(directory: &Path, name: &str) -> PathBuf {
    directory.join(name)
}

fn mapped<P>(directory: &Path, name: &str) -> Whole<FileMapped<P>> {
    crate::memory::mapped(file(directory, name))
}

pub fn measure_links<T: LinkReference>(
    variant: &'static str,
    n: u64,
    repetitions: usize,
    directory: &Path,
) -> Measurement {
    match variant {
        "SQLite_Memory" => measure(
            variant,
            n,
            repetitions,
            directory,
            |_| SqliteLinks::<T>::in_memory(),
            links_lifecycle,
        ),
        "SQLite_File" => measure(
            variant,
            n,
            repetitions,
            directory,
            |dir| SqliteLinks::<T>::open(file(dir, "links.db")),
            links_lifecycle,
        ),
        "Doublets_United_Volatile" => measure(
            variant,
            n,
            repetitions,
            directory,
            |_| DoubletsLinks::new(unit::Store::<T, _>::new(volatile()).unwrap()),
            links_lifecycle,
        ),
        "Doublets_United_NonVolatile" => measure(
            variant,
            n,
            repetitions,
            directory,
            |dir| DoubletsLinks::new(unit::Store::<T, _>::new(mapped(dir, "links.links")).unwrap()),
            links_lifecycle,
        ),
        "Doublets_Split_Volatile" => measure(
            variant,
            n,
            repetitions,
            directory,
            |_| DoubletsLinks::new(split::Store::<T, _, _>::new(volatile(), volatile()).unwrap()),
            links_lifecycle,
        ),
        "Doublets_Split_NonVolatile" => measure(
            variant,
            n,
            repetitions,
            directory,
            |dir| {
                DoubletsLinks::new(
                    split::Store::<T, _, _>::new(
                        mapped(dir, "data.links"),
                        mapped(dir, "index.links"),
                    )
                    .unwrap(),
                )
            },
            links_lifecycle,
        ),
        _ => panic!("unknown links variant {variant}"),
    }
}

pub fn measure_objects<T: LinkReference>(
    variant: &'static str,
    n: u64,
    repetitions: usize,
    directory: &Path,
) -> Measurement {
    let cached = variant.ends_with("_Cached");
    match variant
        .trim_end_matches("_Cached")
        .trim_end_matches("_Uncached")
    {
        "SQLite_Memory" => measure(
            variant,
            n,
            repetitions,
            directory,
            |_| SqliteBlogPosts::<T>::in_memory(),
            objects_lifecycle,
        ),
        "SQLite_File" => measure(
            variant,
            n,
            repetitions,
            directory,
            |dir| SqliteBlogPosts::<T>::open(file(dir, "blog_posts.db")),
            objects_lifecycle,
        ),
        "Doublets_United_Volatile" => measure(
            variant,
            n,
            repetitions,
            directory,
            |_| {
                DoubletsBlogPosts::new(
                    unit::Store::<T, _>::with_constants(volatile(), LinksConstants::external())
                        .unwrap(),
                    cached,
                )
            },
            objects_lifecycle,
        ),
        "Doublets_United_NonVolatile" => measure(
            variant,
            n,
            repetitions,
            directory,
            |dir| {
                DoubletsBlogPosts::new(
                    unit::Store::<T, _>::with_constants(
                        mapped(dir, "links.links"),
                        LinksConstants::external(),
                    )
                    .unwrap(),
                    cached,
                )
            },
            objects_lifecycle,
        ),
        "Doublets_Split_Volatile" => measure(
            variant,
            n,
            repetitions,
            directory,
            |_| {
                DoubletsBlogPosts::new(
                    split::Store::<T, _, _>::with_constants(
                        volatile(),
                        volatile(),
                        LinksConstants::external(),
                    )
                    .unwrap(),
                    cached,
                )
            },
            objects_lifecycle,
        ),
        "Doublets_Split_NonVolatile" => measure(
            variant,
            n,
            repetitions,
            directory,
            |dir| {
                DoubletsBlogPosts::new(
                    split::Store::<T, _, _>::with_constants(
                        mapped(dir, "data.links"),
                        mapped(dir, "index.links"),
                        LinksConstants::external(),
                    )
                    .unwrap(),
                    cached,
                )
            },
            objects_lifecycle,
        ),
        _ => panic!("unknown objects variant {variant}"),
    }
}
