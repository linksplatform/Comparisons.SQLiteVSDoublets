#!/usr/bin/env bash
# Prints the C# medians per variant twice: with a warm JIT the Volatile/NonVolatile pairs of the same
# store should agree, whatever position the variant has in the run.
# Before the warm-up ran for a second, the first variant looked up to 3x slower than its twin, e.g. links 1000:
# Doublets_United_Volatile create 6011 ns vs Doublets_United_NonVolatile 2011 ns.
set -euo pipefail
category=${1:-links}
size=${2:-1000}
dotnet build -c Release csharp/SQLiteVSDoublets > /dev/null
for run in 1 2; do
  start=$SECONDS
  dotnet csharp/SQLiteVSDoublets/bin/Release/net10.0/sqlite-vs-doublets.dll "$category" 64 "$size" \
    --output "/tmp/warm-up-$run.json" --directory /tmp/warm-up-storage 2> /dev/null
  echo "run $run took $((SECONDS - start)) s"
  python3 -c "
import json, sys
for r in json.load(open(sys.argv[1]))['results']:
    print(r['variant'].ljust(38), {k: round(v['median_ns']) for k, v in r['operations'].items()})" "/tmp/warm-up-$run.json"
done
