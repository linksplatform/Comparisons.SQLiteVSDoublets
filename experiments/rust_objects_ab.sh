#!/usr/bin/env bash
# Compares the Rust objects medians of the working tree with HEAD on the same machine.
set -euo pipefail
size=${1:-100000}
variants=${VARIANTS:-SQLite_Memory,Doublets_Split_Volatile_Cached,Doublets_Split_Volatile_Uncached}
run() {
  cargo build --release --quiet --manifest-path rust/Cargo.toml
  rust/target/release/sqlite-vs-doublets objects 64 "$size" --variants "$variants" --output "/tmp/ab-$1.json" 2> /dev/null
  python3 -c "
import json, sys
for r in json.load(open(sys.argv[1]))['results']:
    print(sys.argv[2], r['variant'].ljust(34), {k: round(v['median_ns']) for k, v in r['operations'].items()})" "/tmp/ab-$1.json" "$1"
}
git stash --quiet
trap 'git stash pop --quiet' EXIT
run head
git stash pop --quiet
trap - EXIT
run tree
