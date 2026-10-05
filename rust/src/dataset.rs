//! Deterministic data shared by every storage and by the C# implementation.

use std::sync::{Arc, LazyLock};

const GOLDEN_GAMMA: u64 = 0x9E37_79B9_7F4A_7C15;

pub fn splitmix64(x: u64) -> u64 {
    let mut z = x.wrapping_add(GOLDEN_GAMMA);
    z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
    z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
    z ^ (z >> 31)
}

/// The `(from, to)` pair of the link with id `i`: both reference already created ids
/// and `from + to == i + 1`, so every pair is unique.
pub fn link(i: u64) -> (u64, u64) {
    let from = 1 + splitmix64(2 * i) % i;
    (from, i + 1 - from)
}

/// Order-sensitive mix of two numbers; summing mixes detects records that are lost,
/// duplicated or associated with the wrong id.
pub fn mix(a: u64, b: u64) -> u64 {
    (a.wrapping_mul(GOLDEN_GAMMA) ^ b).wrapping_mul(GOLDEN_GAMMA)
}

pub fn link_checksum(id: u64, from: u64, to: u64) -> u64 {
    mix(mix(id, from), to)
}

pub fn links_checksum(n: u64, swapped: bool) -> u64 {
    (1..=n)
        .map(|i| {
            let (from, to) = link(i);
            if swapped {
                link_checksum(i, to, from)
            } else {
                link_checksum(i, from, to)
            }
        })
        .fold(0, u64::wrapping_add)
}

/// Visits `1..=n` once each in a scattered order, so point operations do not hit
/// neighbouring records one after another.
pub fn scattered(n: u64) -> impl Iterator<Item = u64> {
    let mut stride = GOLDEN_GAMMA % n.max(1);
    while gcd(stride, n) != 1 {
        stride += 1;
    }
    (0..n).map(move |k| 1 + (k * stride) % n)
}

fn gcd(a: u64, b: u64) -> u64 {
    if b == 0 { a } else { gcd(b, a % b) }
}

/// Strings are shared like in C#, so neither the dataset nor a sequences cache copies them.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct BlogPost {
    pub title: Arc<str>,
    pub content: Arc<str>,
    /// Seconds since the Unix epoch.
    pub publication_date: u64,
}

impl BlogPost {
    pub fn checksum(&self) -> u64 {
        let bytes = |text: &str| text.bytes().map(u64::from).sum::<u64>();
        bytes(&self.title) + bytes(&self.content) + self.publication_date
    }
}

const SEPTEMBER_2020: u64 = 1_600_000_000;
const THIRTY_DAYS: u64 = 30 * 24 * 60 * 60;

const PARAGRAPHS: [&str; 5] = [
    "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Duis malesuada blandit mauris nec bibendum. Phasellus feugiat vehicula mauris et aliquet. Integer et gravida velit, in rutrum leo. Duis pretium, nunc ac posuere porttitor, augue sapien commodo tortor, nec consequat lorem eros ultricies odio. Aliquam varius congue ex nec viverra. Pellentesque eu velit tellus. Donec ac luctus nisi. Curabitur dignissim sodales mauris eu semper. Ut pretium lorem nulla, sit amet auctor arcu placerat vitae. Quisque lacinia dolor et consectetur fermentum. Nam ac orci vitae nulla aliquam tempor ac a nibh. Ut ac tincidunt lacus. Morbi vitae felis lorem.",
    "Curabitur tincidunt nibh sit amet finibus dictum. Suspendisse aliquet arcu non rutrum ultrices. Integer ullamcorper mauris sit amet nibh aliquam, et tempor turpis hendrerit. In molestie elit et mauris rutrum, non auctor ligula ultricies. Vestibulum dignissim mauris finibus libero interdum hendrerit. Nunc vitae ipsum porttitor, egestas magna ut, sagittis sem. Donec euismod ac tortor vel porta. Vivamus convallis, ex at vestibulum rutrum, velit purus venenatis metus, sit amet aliquam sapien nibh quis elit. Aenean id neque a orci sodales venenatis. Integer ut orci ligula. Interdum et malesuada fames ac ante ipsum primis in faucibus. Praesent molestie dolor non lobortis ornare. Duis quis nisl sollicitudin, accumsan ante sed, eleifend velit. Maecenas maximus sed ante nec auctor.",
    "Donec vitae felis lectus. Aenean velit sapien, porttitor ut feugiat a, consectetur et risus. Proin ac viverra sem. Nullam sagittis ex tortor, eu pellentesque tellus efficitur at. Nunc non egestas leo. Nam sed suscipit neque. Nam sodales vel neque eget eleifend. Vivamus in condimentum elit, consectetur commodo ex. Suspendisse rutrum, sapien efficitur cursus sodales, dolor orci pulvinar mauris, eu fringilla leo ex id leo. Interdum et malesuada fames ac ante ipsum primis in faucibus. Proin rhoncus sapien massa, molestie vestibulum augue hendrerit nec. Aliquam malesuada varius sapien id accumsan. Duis blandit aliquet felis, nec pellentesque lacus tincidunt et. Cras sed ligula vel nisl laoreet sagittis. Vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia Curae; Praesent tristique a neque aliquet aliquam.",
    "Aliquam sed egestas felis. Maecenas sollicitudin nisl in sapien posuere vulputate. Suspendisse eleifend sem magna, interdum consectetur augue venenatis at. Vivamus ornare orci vel orci sodales maximus. Donec ultricies felis ac nulla fermentum gravida. Phasellus vulputate turpis odio, a varius nibh luctus et. Aliquam tincidunt, metus ut congue porttitor, nibh dui ullamcorper quam, a eleifend elit ipsum sit amet quam. Aenean venenatis mollis interdum. Nunc cursus ex sit amet enim lacinia hendrerit. Nullam at libero iaculis, consectetur velit in, porta sem. Ut mattis ut ex in imperdiet. Maecenas pellentesque sit amet dui eget vehicula. Sed posuere, arcu pretium convallis tincidunt, turpis leo dignissim felis, non euismod diam magna a risus. Suspendisse a arcu nec turpis pulvinar ullamcorper. Nunc iaculis malesuada elit eu pretium. Aenean a neque a sapien tincidunt faucibus.",
    "Ut a eleifend augue, eget posuere augue. Proin purus neque, pretium condimentum ipsum ut, venenatis tincidunt nunc. In vitae odio in justo pharetra tincidunt. Maecenas vel tellus interdum, suscipit tellus sit amet, cursus justo. Mauris sollicitudin euismod molestie. Cras eros nisi, molestie vel elementum ut, consequat ac nunc. In consectetur nulla vitae interdum elementum. Praesent faucibus magna et iaculis congue. Curabitur convallis cursus porttitor. Praesent hendrerit justo ut sem convallis sollicitudin eu at odio.",
];

static CONTENTS: LazyLock<[Arc<str>; 5]> = LazyLock::new(|| PARAGRAPHS.map(Arc::from));

pub fn blog_post(i: u64) -> BlogPost {
    BlogPost {
        title: format!("Blog post {i}").into(),
        content: Arc::clone(&CONTENTS[(i % 5) as usize]),
        publication_date: SEPTEMBER_2020 + splitmix64(i) % THIRTY_DAYS,
    }
}

pub fn blog_posts_checksum(n: u64) -> u64 {
    (1..=n).map(|i| blog_post(i).checksum()).sum()
}

/// Like [`blog_posts_checksum`], but also checks that the `i`-th created id returns the `i`-th post.
pub fn blog_posts_by_number_checksum(n: u64) -> u64 {
    (1..=n)
        .map(|i| mix(i, blog_post(i).checksum()))
        .fold(0, u64::wrapping_add)
}
