//! Object-like structures (blog posts) shared by both storages.
//!
//! The C# part of this repository compares SQLite and Doublets on an object
//! like structure — a blog post with a title, a content and a publication date.
//! This module provides the same model for the Rust benchmarks, so that both
//! languages measure the same three operations:
//!
//! | Operation             | Meaning                                       |
//! |-----------------------|-----------------------------------------------|
//! | `Objects Create List` | save a list of blog posts into empty storage  |
//! | `Objects Read List`   | read every stored blog post back              |
//! | `Objects Delete List` | delete every stored blog post                 |
//!
//! The generated data matches `csharp/Model/BlogPosts.cs`: the title is
//! `Blog post {n}`, the content is one of five lorem ipsum paragraphs and the
//! publication date is within the last 30 days. Generation is deterministic
//! (a small xorshift generator seeded by [`OBJECT_SEED`]) so that every run and
//! every storage is benchmarked on exactly the same data.

use once_cell::sync::Lazy;
use std::env;

/// Number of blog posts used by the object benchmarks.
///
/// Can be overridden with the `BENCHMARK_OBJECT_COUNT` environment variable,
/// which the CI workflow lowers for pull request runs.
pub static BENCHMARK_OBJECT_COUNT: Lazy<usize> = Lazy::new(|| {
    env::var("BENCHMARK_OBJECT_COUNT")
        .ok()
        .and_then(|value| value.parse().ok())
        .unwrap_or(1000)
});

/// Seed of the deterministic generator of blog posts.
pub const OBJECT_SEED: u64 = 0x5148_5157_2D31_3235;

/// Lorem ipsum paragraphs used as blog post contents.
///
/// The same five paragraphs are used by `csharp/Model/BlogPosts.cs`.
pub const CONTENTS: [&str; 5] = [
    "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis malesuada blandit mauris nec bibendum.",
    "Curabitur tincidunt nibh sit amet finibus dictum. Suspendisse aliquet arcu non rutrum ultrices.",
    "Donec vitae felis lectus. Aenean velit sapien, porttitor ut feugiat a, consectetur et risus.",
    "Aliquam sed egestas felis. Maecenas sollicitudin nisl in sapien posuere vulputate.",
    "Ut a eleifend augue, eget posuere augue. Proin purus neque, pretium condimentum ipsum ut.",
];

/// An object like structure: a blog post.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BlogPost {
    /// Storage assigned identifier, `0` before the post is saved.
    pub id: u64,
    /// Unique title of the post.
    pub title: String,
    /// Body of the post.
    pub content: String,
    /// Publication date as a Unix timestamp in seconds.
    pub publication_date_time: i64,
}

impl BlogPost {
    /// Creates a not yet stored blog post.
    pub fn new(
        title: impl Into<String>,
        content: impl Into<String>,
        publication_date_time: i64,
    ) -> Self {
        Self {
            id: 0,
            title: title.into(),
            content: content.into(),
            publication_date_time,
        }
    }
}

/// Deterministic xorshift64* generator, so every benchmark sees the same data.
struct Generator(u64);

impl Generator {
    fn next(&mut self) -> u64 {
        let mut state = self.0;
        state ^= state << 13;
        state ^= state >> 7;
        state ^= state << 17;
        self.0 = state;
        state
    }
}

/// Generates `count` blog posts, mirroring `BlogPosts.GenerateData` of the C# benchmark.
pub fn generate_posts(count: usize) -> Vec<BlogPost> {
    // A fixed "now" keeps the generated data stable between runs.
    const NOW: i64 = 1_700_000_000;
    const SECONDS_IN_30_DAYS: i64 = 30 * 24 * 60 * 60;

    let mut generator = Generator(OBJECT_SEED);
    (0..count)
        .map(|index| {
            let content = CONTENTS[(generator.next() % CONTENTS.len() as u64) as usize];
            let age = (generator.next() % SECONDS_IN_30_DAYS as u64) as i64;
            BlogPost::new(format!("Blog post {}", index + 1), content, NOW - age)
        })
        .collect()
}

/// Storage of object like structures.
///
/// Implemented natively by every benchmarked backend: SQLite stores blog posts
/// as rows of a table, Doublets stores them as links (with strings represented
/// as sequences of links).
pub trait Objects {
    /// Saves every post of the list, returns the assigned identifiers.
    fn create_posts(&mut self, posts: &[BlogPost]) -> Vec<u64>;

    /// Reads every stored post back.
    fn read_posts(&self) -> Vec<BlogPost>;

    /// Deletes every stored post.
    fn delete_posts(&mut self);

    /// Number of stored posts.
    fn count_posts(&self) -> usize;
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn generated_data_is_deterministic() {
        assert_eq!(generate_posts(16), generate_posts(16));
    }

    #[test]
    fn generated_titles_match_the_csharp_benchmark() {
        let posts = generate_posts(3);
        assert_eq!(posts[0].title, "Blog post 1");
        assert_eq!(posts[2].title, "Blog post 3");
    }

    #[test]
    fn generated_contents_are_taken_from_the_lorem_ipsum_paragraphs() {
        for post in generate_posts(64) {
            assert!(CONTENTS.contains(&post.content.as_str()));
        }
    }

    #[test]
    fn generated_dates_are_within_the_last_30_days() {
        let posts = generate_posts(64);
        let newest = posts
            .iter()
            .map(|post| post.publication_date_time)
            .max()
            .unwrap();
        let oldest = posts
            .iter()
            .map(|post| post.publication_date_time)
            .min()
            .unwrap();
        assert!(newest - oldest <= 30 * 24 * 60 * 60);
    }
}
