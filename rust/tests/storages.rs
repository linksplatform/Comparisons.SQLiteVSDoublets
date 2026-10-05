use doublets::{
    data::{LinkReference, LinksConstants},
    mem::Global,
    split, unit,
};
use sqlite_vs_doublets::{
    dataset::{BlogPost, blog_post, link, scattered},
    harness::{self, LINKS_VARIANTS, OBJECTS_VARIANTS},
    links::{DoubletsLinks, Link, LinksStorage, SqliteLinks},
    memory::volatile,
    objects::{BlogPostsStorage, DoubletsBlogPosts, SqliteBlogPosts},
};
use std::collections::HashSet;

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
    std::fs::create_dir_all(&directory).unwrap();
    directory
}
