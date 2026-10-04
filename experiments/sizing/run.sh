#!/usr/bin/env bash
# Times one repetition of every variant to plan sizes and CI job splits.
# Usage: experiments/sizing/run.sh <links|objects> <32|64> <size> [extra args]
set -euo pipefail
category=$1 bits=$2 size=$3
shift 3
binary="$(dirname "$0")/../../rust/target/release/sqlite-vs-doublets"
log="/tmp/sizing-$category-$bits-$size.log"
start=$(date +%s)
"$binary" "$category" "$bits" "$size" --repetitions 1 --directory "/tmp/sizing-$category-$bits-$size" \
  --output "/tmp/sizing-$category-$bits-$size.json" "$@" > "$log" 2>&1
echo "$category $bits $size: $(( $(date +%s) - start )) s"
grep '#1' "$log"
