//! Runs every storage through the same lifecycle on a fresh store per repetition:
//! a timed batch per operation, each validated against the expected count and checksum.

use crate::{
    dataset::{self, blog_post, link, mix, scattered},
    links::{DoubletsLinks, Link, LinksStorage, SqliteLinks, TransactionalLinks},
    memory::volatile,
    objects::{BlogPostsStorage, DoubletsBlogPosts, SqliteBlogPosts},
};
use doublets::{
    data::{LinkReference, LinksConstants},
    decorators::{DecoratorsExt, FileTransitionLog},
    split, unit,
};
use serde_json::{Value, json};
use std::{
    cell::Cell,
    fs::{self, OpenOptions},
    io::Write,
    path::{Path, PathBuf},
    sync::OnceLock,
    time::{Duration, Instant},
};

pub const LINKS_VARIANTS: [&str; 7] = [
    "SQLite_Memory",
    "SQLite_File",
    "SQLite_File_Tuned",
    "Doublets_United_Volatile",
    "Doublets_United_NonVolatile",
    "Doublets_Split_Volatile",
    "Doublets_Split_NonVolatile",
];

/// Links variants that run only when named with `--variants`: the transactional store calls
/// `fsync` for every written link, which would not fit the time of the regular runs.
pub const OPTIONAL_LINKS_VARIANTS: [&str; 1] = ["Doublets_United_NonVolatile_Transactional"];

pub const OBJECTS_VARIANTS: [&str; 11] = [
    "SQLite_Memory",
    "SQLite_File",
    "SQLite_File_Tuned",
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
pub const WARM_UP_TIME: Duration = Duration::from_secs(1);

type Timings = Vec<(&'static str, Duration)>;

/// The phase log of [`log_phases`] and the time it was opened.
static PHASE_LOG: OnceLock<(PathBuf, Instant)> = OnceLock::new();

thread_local! {
    /// The variant, repetition and size of the measured run in progress; warm-ups are not logged.
    static MEASURED: Cell<Option<(&'static str, usize, u64)>> = const { Cell::new(None) };
}

/// Appends a JSON line to `path` (and to stderr) when a measured run starts, when each of its phases
/// begins and when it ends and passes its check, and when its store's size is known, with the
/// nanoseconds since this call: a run that is cut short, like one that exceeds its time under a memory
/// limit, still shows the phases it finished and when the one in progress began.
pub fn log_phases(path: PathBuf) {
    PHASE_LOG
        .set((path, Instant::now()))
        .expect("the phase log is opened once");
}

fn log_phase(event: &str, mut fields: Value) {
    let (Some((path, opened)), Some((variant, repetition, _))) = (PHASE_LOG.get(), MEASURED.get())
    else {
        return;
    };
    fields["event"] = json!(event);
    fields["variant"] = json!(variant);
    fields["repetition"] = json!(repetition);
    fields["at_ns"] = json!(opened.elapsed().as_nanos() as u64);
    let mut file = OpenOptions::new().append(true).open(path).unwrap();
    writeln!(file, "{fields}").unwrap();
    eprintln!("{fields}");
}

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
    log_phase("begin", json!({ "operation": operation }));
    let start = Instant::now();
    let actual = work();
    let elapsed = start.elapsed();
    timings.push((operation, elapsed));
    assert_eq!(actual, expected, "{operation} returned unexpected records");
    if let Some((_, _, size)) = MEASURED.get() {
        let ns_per_operation = elapsed.as_nanos() as f64 / size as f64;
        log_phase(
            "end",
            json!({ "operation": operation, "ns_per_operation": ns_per_operation }),
        );
    }
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
                let (new_to, new_from) = link(id);
                links.update(int(id), int(new_from), int(new_to));
                tally.add(dataset::link_checksum(id, new_from, new_to));
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

/// Times `lifecycle` on `repetitions` fresh stores opened in empty directories,
/// after discarded warm-ups that run for at least [`WARM_UP_TIME`], like in C#.
fn measure<S>(
    variant: &'static str,
    n: u64,
    repetitions: usize,
    directory: &Path,
    open: impl Fn(&Path) -> S,
    lifecycle: fn(&mut S, u64, &mut dyn FnMut()) -> Timings,
) -> Measurement {
    let run = |size| {
        let workspace = directory.join(variant);
        fs::create_dir_all(&workspace).unwrap();
        let mut storage = open(&workspace);
        let mut file_bytes = 0;
        log_phase("start", json!({}));
        let timings = lifecycle(&mut storage, size, &mut || {
            file_bytes = directory_bytes(&workspace);
            if file_bytes > 0 {
                log_phase("file", json!({ "file_bytes": file_bytes }));
            }
        });
        drop(storage);
        fs::remove_dir_all(&workspace).unwrap();
        (timings, file_bytes)
    };
    let warm_up = Instant::now();
    run(n.min(WARM_UP_SIZE));
    while warm_up.elapsed() < WARM_UP_TIME {
        run(n.min(WARM_UP_SIZE));
    }
    let mut measurement = Measurement {
        variant,
        file_bytes: None,
        operations: Vec::new(),
    };
    for repetition in 1..=repetitions {
        MEASURED.set(Some((variant, repetition, n)));
        let (timings, file_bytes) = run(n);
        MEASURED.set(None);
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

fn mapped<P>(directory: &Path, name: &str) -> crate::memory::ZeroExtended<P> {
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
        "SQLite_File_Tuned" => measure(
            variant,
            n,
            repetitions,
            directory,
            |dir| SqliteLinks::<T>::open_tuned(file(dir, "links.db")),
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
        "Doublets_United_NonVolatile_Transactional" => measure(
            variant,
            n,
            repetitions,
            directory,
            // the journal as shipped, which calls `sync_all` after every record it appends
            |dir| {
                TransactionalLinks::new(
                    unit::Store::<T, _>::new(mapped(dir, "links.links"))
                        .unwrap()
                        .with_transactions(
                            FileTransitionLog::open(file(dir, "links.journal")).unwrap(),
                        )
                        .unwrap(),
                )
            },
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
        "SQLite_File_Tuned" => measure(
            variant,
            n,
            repetitions,
            directory,
            |dir| SqliteBlogPosts::<T>::open_tuned(file(dir, "blog_posts.db")),
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
