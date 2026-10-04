//! Usage: sqlite-vs-doublets <links|objects> <32|64> <size> [--work N | --repetitions N]
//!        [--variants A,B] [--directory DIR] [--output FILE]
//!
//! Repetitions default to `work / size` clamped to `1..=MAX_REPETITIONS`, so smaller sizes get more,
//! but not endless, repetitions.

use doublets::data::LinkReference;
use serde_json::json;
use sqlite_vs_doublets::harness::{self, LINKS_VARIANTS, Measurement, OBJECTS_VARIANTS};
use std::{collections::HashMap, env, fs, path::Path};

const DEFAULT_WORK: u64 = 3_000_000;
const MAX_REPETITIONS: u64 = 10;

fn main() {
    let arguments: Vec<String> = env::args().skip(1).collect();
    let [category, bits, size] = [0, 1, 2].map(|index| {
        arguments
            .get(index)
            .expect("missing positional argument")
            .as_str()
    });
    let options: HashMap<&str, &str> = arguments[3..]
        .chunks(2)
        .map(|pair| (pair[0].trim_start_matches("--"), pair[1].as_str()))
        .collect();
    let size: u64 = size.replace('_', "").parse().unwrap();
    let work: u64 = options
        .get("work")
        .map_or(DEFAULT_WORK, |work| work.replace('_', "").parse().unwrap());
    let repetitions: usize = options
        .get("repetitions")
        .map_or((work / size).clamp(1, MAX_REPETITIONS) as usize, |count| {
            count.parse().unwrap()
        });
    let all_variants: &[&'static str] = if category == "links" {
        &LINKS_VARIANTS
    } else {
        &OBJECTS_VARIANTS
    };
    let variants: Vec<&'static str> = match options.get("variants") {
        Some(names) => names
            .split(',')
            .map(|name| {
                *all_variants
                    .iter()
                    .find(|known| **known == name)
                    .expect(name)
            })
            .collect(),
        None => all_variants.to_vec(),
    };
    let directory = options
        .get("directory")
        .map_or_else(env::temp_dir, |directory| directory.into());

    let measurements: Vec<Measurement> = match bits {
        "32" => measure::<u32>(category, &variants, size, repetitions, &directory),
        "64" => measure::<u64>(category, &variants, size, repetitions, &directory),
        _ => panic!("bits must be 32 or 64"),
    };
    let report = json!({
        "language": "Rust",
        "category": category,
        "bits": bits.parse::<u32>().unwrap(),
        "size": size,
        "repetitions": repetitions,
        "warm_up_size": size.min(harness::WARM_UP_SIZE),
        "sqlite_version": rusqlite::version(),
        "results": measurements.iter().map(Measurement::to_json).collect::<Vec<_>>(),
    });
    let report = serde_json::to_string_pretty(&report).unwrap();
    match options.get("output") {
        Some(path) => fs::write(path, report).unwrap(),
        None => println!("{report}"),
    }
}

fn measure<T: LinkReference>(
    category: &str,
    variants: &[&'static str],
    size: u64,
    repetitions: usize,
    directory: &Path,
) -> Vec<Measurement> {
    variants
        .iter()
        .map(|variant| match category {
            "links" => harness::measure_links::<T>(variant, size, repetitions, directory),
            "objects" => harness::measure_objects::<T>(variant, size, repetitions, directory),
            _ => panic!("category must be links or objects"),
        })
        .collect()
}
