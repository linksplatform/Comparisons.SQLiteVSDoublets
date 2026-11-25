use doublets::{
    data::{LinkReference, LinksConstants},
    decorators::{DecoratorsExt, JournalEntry, MemoryTransitionLog},
    mem::Global,
    split, unit,
};
use sqlite_vs_doublets::{
    dataset::{BlogPost, blog_post, link, scattered},
    harness::{self, LINKS_VARIANTS, OBJECTS_VARIANTS, OPTIONAL_LINKS_VARIANTS},
    links::{DoubletsLinks, Link, LinksStorage, SqliteLinks, TransactionalLinks},
    memory::{mapped, volatile},
    objects::{BlogPostsStorage, DoubletsBlogPosts, SqliteBlogPosts},
};
use std::{collections::HashSet, fs};

const SIZE: u64 = 1_000;

#[test]
fn links_are_unique_and_reference_created_ids() {
    let links: HashSet<_> = (1..=SIZE).map(link).collect();
    assert_eq!(links.len(), SIZE as usize);
    for i in 1..=SIZE {
        let (from, to) = link(i);
        assert!((1..=i).contains(&from) && (1..=i).contains(&to));
    }
}

#[test]
fn scattered_visits_every_id_once() {
    for n in [1, 2, 3, 10, 1_000, 1_024, 65_536] {
        let mut ids: Vec<u64> = scattered(n).collect();
        ids.sort();
        assert_eq!(ids, (1..=n).collect::<Vec<_>>());
    }
}

#[test]
fn every_links_variant_passes_the_lifecycle() {
    let directory = tempdir("links");
    for variant in LINKS_VARIANTS {
        harness::measure_links::<u32>(variant, SIZE, 1, &directory);
        harness::measure_links::<u64>(variant, SIZE, 1, &directory);
    }
}

#[test]
fn every_optional_links_variant_passes_the_lifecycle() {
    // Every written link is an fsync of the journal, so this size keeps the test short.
    let directory = tempdir("optional-links");
    for variant in OPTIONAL_LINKS_VARIANTS {
        harness::measure_links::<u32>(variant, 100, 1, &directory);
        harness::measure_links::<u64>(variant, 100, 1, &directory);
    }
}

fn entries(
    links: &TransactionalLinks<u32, impl doublets::Doublets<u32>, MemoryTransitionLog<u32>>,
) -> Vec<String> {
    links
        .store()
        .journal()
        .entries()
        .iter()
        .map(|entry| match entry {
            JournalEntry::Begin { snapshot, .. } => format!("begin {}", snapshot.len()),
            JournalEntry::Commit { snapshot, .. } => format!("commit {}", snapshot.len()),
            JournalEntry::Rollback { .. } => "rollback".into(),
            JournalEntry::Transition(_) => "transition".into(),
        })
        .filter(|entry| entry != "transition")
        .collect()
}

#[test]
fn transactional_store_commits_one_transaction_per_written_phase() {
    let mut links = TransactionalLinks::new(
        unit::Store::<u32, _>::new(Global::new())
            .unwrap()
            .with_transactions(MemoryTransitionLog::default())
            .unwrap(),
    );
    harness::links_lifecycle(&mut links, 100, &mut || {});
    // create, update and delete; the five read phases begin nothing
    assert_eq!(
        entries(&links),
        [
            "begin 0",
            "commit 100",
            "begin 100",
            "commit 100",
            "begin 100",
            "commit 0"
        ]
    );
    let mut kinds = [0; 3];
    for entry in links.store().journal().entries() {
        if let JournalEntry::Transition(transition) = entry {
            kinds[transition.kind as usize] += 1;
        }
    }
    // creates, updates and deletes: each create_link is a create and an update, and the update
    // phase changes every link but the point (1, 1); every one is an fsync of a file journal
    assert_eq!(kinds, [100, 100 + 99, 100]);
}

#[test]
fn transactional_store_journals_writes_outside_a_phase_one_by_one() {
    let mut links = TransactionalLinks::new(
        unit::Store::<u32, _>::new(Global::new())
            .unwrap()
            .with_transactions(MemoryTransitionLog::default())
            .unwrap(),
    );
    let point = links.create(1, 1);
    let read = links.transaction(|links| links.get(point));
    assert_eq!(
        read,
        Some(Link {
            id: point,
            from: 1,
            to: 1
        })
    );
    // create_link is a create and an update, each committed by itself outside a phase,
    // and the read-only phase writes nothing
    assert_eq!(
        entries(&links),
        ["begin 0", "commit 1", "begin 1", "commit 1"]
    );
}

#[test]
fn every_objects_variant_passes_the_lifecycle() {
    let directory = tempdir("objects");
    for variant in OBJECTS_VARIANTS {
        harness::measure_objects::<u32>(variant, SIZE / 10, 1, &directory);
        harness::measure_objects::<u64>(variant, SIZE / 10, 1, &directory);
    }
}

#[test]
fn split_store_keeps_links_updated_to_reference_themselves() {
    let mut links =
        DoubletsLinks::new(split::Store::<u32, _, _>::new(Global::new(), Global::new()).unwrap());
    let point = links.create(1, 1);
    let link = links.create(point, 2);
    links.update(point, point, point);
    links.update(link, link, point);
    assert_eq!(
        links.get(link),
        Some(Link {
            id: link,
            from: link,
            to: point
        })
    );
    links.delete(link);
    links.delete(point);
    assert_eq!(links.count(), 0);
}

/// A fresh store reserves 2^20 links, so this many links make it grow once.
const GROWN: u64 = (1 << 20) + 1;

fn create_until_grown(mut links: impl LinksStorage<u32>) {
    for i in 1..=GROWN {
        let (from, to) = link(i);
        links.create(from as u32, to as u32);
    }
    assert_eq!(links.count(), GROWN);
    for i in [1, 1_040_384, 1_040_385, GROWN] {
        let (from, to) = link(i);
        let (id, from, to) = (i as u32, from as u32, to as u32);
        assert_eq!(links.get(id), Some(Link { id, from, to }));
    }
}

#[test]
fn stores_keep_their_links_when_their_memory_grows() {
    create_until_grown(DoubletsLinks::new(
        unit::Store::<u32, _>::new(volatile()).unwrap(),
    ));
    create_until_grown(DoubletsLinks::new(
        split::Store::<u32, _, _>::new(volatile(), volatile()).unwrap(),
    ));
}

#[test]
fn mapped_stores_keep_their_links_when_their_memory_grows() {
    let directory = tempdir("mapped-growth");
    create_until_grown(DoubletsLinks::new(
        unit::Store::<u32, _>::new(mapped(directory.join("links.links"))).unwrap(),
    ));
    create_until_grown(DoubletsLinks::new(
        split::Store::<u32, _, _>::new(
            mapped(directory.join("data.links")),
            mapped(directory.join("index.links")),
        )
        .unwrap(),
    ));
    fs::remove_dir_all(directory).unwrap();
}

/// The length of `path` and the bytes the file system allocated once its dirty pages are written
/// back.
#[cfg(target_os = "linux")]
fn written_bytes(path: &std::path::Path) -> (u64, u64) {
    use std::os::unix::fs::MetadataExt;
    fs::File::open(path).unwrap().sync_all().unwrap();
    let metadata = fs::metadata(path).unwrap();
    (metadata.len(), metadata.blocks() * 512)
}

fn assert_reopened(links: &impl LinksStorage<u32>) {
    assert_eq!(links.count(), SIZE);
    for i in [1, SIZE / 2, SIZE] {
        let (from, to) = link(i);
        let (id, from, to) = (i as u32, from as u32, to as u32);
        assert_eq!(links.get(id), Some(Link { id, from, to }));
    }
}

#[test]
fn mapped_stores_do_not_write_their_reservation_and_reopen_with_their_links() {
    let directory = tempdir("mapped-reservation");
    let (united, data, index) = (
        directory.join("links.links"),
        directory.join("data.links"),
        directory.join("index.links"),
    );
    {
        let mut unit_links =
            DoubletsLinks::new(unit::Store::<u32, _>::new(mapped(&united)).unwrap());
        let mut split_links = DoubletsLinks::new(
            split::Store::<u32, _, _>::new(mapped(&data), mapped(&index)).unwrap(),
        );
        for i in 1..=SIZE {
            let (from, to) = link(i);
            unit_links.create(from as u32, to as u32);
            split_links.create(from as u32, to as u32);
        }
    }
    // Each file reserves 2^20 links, which the file system holds as a hole until they are written.
    // Checked on Linux, where the memory-limit benchmarks run: whether an extension stays a hole
    // depends on the file system, and APFS allocated the whole 8 MiB of the split data file
    // (macOS 15, run 45603348935).
    #[cfg(target_os = "linux")]
    for path in [&united, &data, &index] {
        let (length, allocated) = written_bytes(path);
        assert!(length >= 1 << 23, "{path:?} is only {length} bytes");
        assert!(
            allocated < 1 << 20,
            "{path:?} allocated {allocated} of {length} bytes"
        );
    }
    assert_reopened(&DoubletsLinks::new(
        unit::Store::<u32, _>::new(mapped(&united)).unwrap(),
    ));
    assert_reopened(&DoubletsLinks::new(
        split::Store::<u32, _, _>::new(mapped(&data), mapped(&index)).unwrap(),
    ));
    fs::remove_dir_all(directory).unwrap();
}

#[test]
#[should_panic(expected = "query_all returned unexpected records")]
fn lifecycle_rejects_a_storage_that_loses_records() {
    struct Lossy(SqliteLinks<u32>);

    impl LinksStorage<u32> for Lossy {
        fn create(&mut self, from: u32, to: u32) -> u32 {
            self.0.create(from, to)
        }
        fn update(&mut self, id: u32, from: u32, to: u32) {
            self.0.update(id, from, to)
        }
        fn delete(&mut self, id: u32) {
            self.0.delete(id)
        }
        fn get(&self, id: u32) -> Option<Link<u32>> {
            self.0.get(id)
        }
        fn search(&self, from: u32, to: u32) -> Option<u32> {
            self.0.search(from, to)
        }
        fn each(&self, visit: impl FnMut(Link<u32>)) {
            self.0.each_with_from(1, visit)
        }
        fn each_with_from(&self, from: u32, visit: impl FnMut(Link<u32>)) {
            self.0.each_with_from(from, visit)
        }
        fn each_with_to(&self, to: u32, visit: impl FnMut(Link<u32>)) {
            self.0.each_with_to(to, visit)
        }
        fn count(&self) -> u64 {
            self.0.count()
        }

        type Phase<'a> = Self;

        fn transaction<R>(&mut self, work: impl FnOnce(&mut Self) -> R) -> R {
            work(self)
        }
    }

    harness::links_lifecycle(&mut Lossy(SqliteLinks::in_memory()), SIZE, &mut || {});
}

fn round_trip<T: LinkReference>(mut posts: impl BlogPostsStorage<T>) {
    let mut inputs: Vec<BlogPost> = (1..=20).map(blog_post).collect();
    inputs.push(BlogPost {
        title: "Ünïcödé 🌍 ✓".into(),
        content: "a".into(),
        publication_date: 0,
    });
    inputs.push(BlogPost {
        title: "".into(),
        content: "ab".into(),
        publication_date: u32::MAX as u64 / 2,
    });
    let ids: Vec<T> = inputs.iter().map(|post| posts.create(post)).collect();
    for (id, post) in ids.iter().zip(&inputs) {
        assert_eq!(posts.get(*id).as_ref(), Some(post));
    }
    let mut stored = Vec::new();
    posts.each(|id, post| stored.push((id, post)));
    stored.sort_by_key(|(id, _)| *id);
    assert_eq!(stored, ids.into_iter().zip(inputs).collect::<Vec<_>>());
}

#[test]
fn every_objects_storage_returns_the_stored_posts() {
    round_trip::<u32>(SqliteBlogPosts::in_memory());
    round_trip::<u64>(SqliteBlogPosts::in_memory());
    for cached in [false, true] {
        round_trip(DoubletsBlogPosts::new(
            unit::Store::<u32, _>::with_constants(Global::new(), LinksConstants::external())
                .unwrap(),
            cached,
        ));
        round_trip(DoubletsBlogPosts::new(
            unit::Store::<u64, _>::with_constants(Global::new(), LinksConstants::external())
                .unwrap(),
            cached,
        ));
        round_trip(DoubletsBlogPosts::new(
            split::Store::<u32, _, _>::with_constants(
                Global::new(),
                Global::new(),
                LinksConstants::external(),
            )
            .unwrap(),
            cached,
        ));
        round_trip(DoubletsBlogPosts::new(
            split::Store::<u64, _, _>::with_constants(
                Global::new(),
                Global::new(),
                LinksConstants::external(),
            )
            .unwrap(),
            cached,
        ));
    }
}

fn tempdir(name: &str) -> std::path::PathBuf {
    let directory = std::env::temp_dir().join(format!(
        "sqlite-vs-doublets-test-{name}-{}",
        std::process::id()
    ));
    fs::create_dir_all(&directory).unwrap();
    directory
}
