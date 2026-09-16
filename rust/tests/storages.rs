//! Behaviour tests shared by every benchmarked storage.
//!
//! The benchmarks only make sense when every variant implements exactly the
//! same semantics, so the same checks are executed against all six subjects:
//! SQLite (in-memory and file based) and Doublets (united and split stores,
//! each of them volatile and non-volatile).

#![feature(allocator_api)]

use sqlite_vs_doublets::benched::{
    DoubletsSplitNonVolatileBenched, DoubletsSplitVolatileBenched,
    DoubletsUnitedNonVolatileBenched, DoubletsUnitedVolatileBenched, SqliteFileBenched,
    SqliteMemoryBenched,
};
use sqlite_vs_doublets::{
    fill_background, fill_benchmarked, generate_posts, Benched, Links, Objects,
};
use std::ops::DerefMut;

const BACKGROUND: usize = 16;
const BENCHMARKED: usize = 32;
const POSTS: usize = 8;

/// Checks the link related operations benchmarked by `benches/benchmarks`.
fn check_links<B>(subject: &mut B)
where
    B: Benched + DerefMut,
    B::Target: Links + Sized,
{
    {
        let mut fork = subject.fork();
        let links = &mut **fork;

        let background = fill_background(links, BACKGROUND);
        assert_eq!(background.len(), BACKGROUND);
        let created = fill_benchmarked(links, &background, BENCHMARKED);
        assert_eq!(created.len(), BENCHMARKED);
        assert_eq!(links.count(), BACKGROUND + BENCHMARKED);
        assert_eq!(links.query_all().len(), BACKGROUND + BENCHMARKED);

        // Every created doublet has to be unique, otherwise Doublets would
        // return an already existing link instead of creating a new one.
        let mut doublets: Vec<(u64, u64)> = created.iter().map(|l| (l.source, l.target)).collect();
        doublets.sort_unstable();
        doublets.dedup();
        assert_eq!(doublets.len(), BENCHMARKED);

        let sample = created[BENCHMARKED / 2];
        assert_eq!(links.query_by_id(sample.id), Some(sample));

        let all = links.query_all();
        let outgoing = all.iter().filter(|l| l.source == sample.source).count();
        assert_eq!(links.query_by_source(sample.source).len(), outgoing);
        let incoming = all.iter().filter(|l| l.target == sample.target).count();
        assert_eq!(links.query_by_target(sample.target).len(), incoming);
        let concrete = all
            .iter()
            .filter(|l| l.source == sample.source && l.target == sample.target)
            .count();
        let queried = links.query_by_source_target(sample.source, sample.target);
        assert_eq!(queried.len(), concrete);

        // Updating to a doublet that is not stored yet, as the update benchmark does.
        let unused = links.create_point();
        links.update(sample.id, unused, background[0]);
        let updated = links.query_by_id(sample.id).expect("updated link is missing");
        assert_eq!((updated.source, updated.target), (unused, background[0]));

        let before = links.count();
        links.delete(sample.id);
        assert_eq!(links.count(), before - 1);
        assert!(links.query_by_id(sample.id).is_none());

        links.delete_all();
        assert_eq!(links.count(), 0);
        assert!(links.query_all().is_empty());
    }

    // Dropping the fork has to restore the empty state for the next iteration.
    assert_eq!(subject.count(), 0);
    assert!(subject.query_all().is_empty());
}

/// Checks the object like (blog post) operations benchmarked by `benches/benchmarks/objects`.
fn check_objects<B>(subject: &mut B)
where
    B: Benched + DerefMut,
    B::Target: Objects + Links + Sized,
{
    let expected = generate_posts(POSTS);
    {
        let mut fork = subject.fork();
        let storage = &mut **fork;

        let ids = storage.create_posts(&expected);
        assert_eq!(ids.len(), POSTS);
        assert_eq!(storage.count_posts(), POSTS);

        let mut read = storage.read_posts();
        read.sort_by(|left, right| left.title.cmp(&right.title));
        let mut expected = expected.clone();
        expected.sort_by(|left, right| left.title.cmp(&right.title));
        assert_eq!(read.len(), expected.len());
        for (read, expected) in read.iter().zip(expected.iter()) {
            assert_eq!(read.title, expected.title);
            assert_eq!(read.content, expected.content);
            assert_eq!(read.publication_date_time, expected.publication_date_time);
        }

        storage.delete_posts();
        assert_eq!(storage.count_posts(), 0);
        assert!(storage.read_posts().is_empty());
    }

    assert_eq!(subject.count_posts(), 0);
}

/// Objects and links live in the same storage, they must not disturb each other.
fn check_objects_and_links<B>(subject: &mut B)
where
    B: Benched + DerefMut,
    B::Target: Objects + Links + Sized,
{
    let mut fork = subject.fork();
    let storage = &mut **fork;

    let background = fill_background(storage, BACKGROUND);
    fill_benchmarked(storage, &background, BENCHMARKED);
    let posts = generate_posts(POSTS);
    storage.create_posts(&posts);

    assert_eq!(storage.count_posts(), POSTS);
    assert_eq!(storage.read_posts().len(), POSTS);

    storage.delete_posts();
    assert_eq!(storage.count_posts(), 0);
}

macro_rules! storage_tests {
    ($module:ident, $benched:ty) => {
        mod $module {
            use super::*;

            #[test]
            fn links() {
                check_links(&mut <$benched>::setup(()));
            }

            #[test]
            fn objects() {
                check_objects(&mut <$benched>::setup(()));
            }

            #[test]
            fn objects_and_links() {
                check_objects_and_links(&mut <$benched>::setup(()));
            }
        }
    };
}

storage_tests!(sqlite_memory, SqliteMemoryBenched);
storage_tests!(sqlite_file, SqliteFileBenched);
storage_tests!(doublets_united_volatile, DoubletsUnitedVolatileBenched);
storage_tests!(doublets_united_non_volatile, DoubletsUnitedNonVolatileBenched);
storage_tests!(doublets_split_volatile, DoubletsSplitVolatileBenched);
storage_tests!(doublets_split_non_volatile, DoubletsSplitNonVolatileBenched);
