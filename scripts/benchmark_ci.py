"""Plan bounded benchmark jobs and verify all artifacts before publication."""

import argparse
import itertools
import json
import sys
from pathlib import Path

import benchmark_report

VARIANTS = {
    "links": {"SQLite_Memory", "SQLite_File"}
    | {
        f"Doublets_{layout}_{durability}"
        for layout, durability in itertools.product(("United", "Split"), ("Volatile", "NonVolatile"))
    },
    "objects": {"SQLite_Memory", "SQLite_File"}
    | {
        f"Doublets_{layout}_{durability}_{cache}"
        for layout, durability, cache in itertools.product(
            ("United", "Split"), ("Volatile", "NonVolatile"), ("Cached", "Uncached")
        )
    },
}


def expected_variants(category, language):
    """C# also benchmarks the System.Data.SQLite provider introduced on main."""
    variants = VARIANTS[category]
    if language in ("C#", "csharp"):
        variants = variants | {"SystemDataSQLite_Memory", "SystemDataSQLite_File"}
    return variants


def matrix(sizes):
    if not isinstance(sizes, list) or not 1 <= len(sizes) <= 32:
        raise ValueError("sizes must contain between 1 and 32 integers (at most 256 jobs)")
    if any(
        not isinstance(size, int) or isinstance(size, bool) or not 1 <= size <= 10000000 for size in sizes
    ):
        raise ValueError("sizes must be integers between 1 and 10,000,000")
    if len(sizes) != len(set(sizes)):
        raise ValueError("sizes must be unique")
    return [
        dict(language=language, category=category, bits=bits, size=size)
        for language, category, bits, size in itertools.product(
            ("rust", "csharp"), ("links", "objects"), (32, 64), sizes
        )
        if category != "objects" or size <= 1000000
    ]


def filename(row):
    return f"{row['category']}-{row['language']}-{row['bits']}-{row['size']}.json"


def verify(directory, sizes, sha, verbose=False, tree=None):
    rows = matrix(sizes)
    expected = {filename(row) for row in rows}
    actual = {path.name for path in directory.glob("*.json")}
    if expected - actual:
        raise ValueError(f"Missing benchmark artifacts: {sorted(expected - actual)}")
    if actual - expected:
        raise ValueError(f"Unexpected benchmark artifacts: {sorted(actual - expected)}")
    benchmark_report.load(directory)
    for row in rows:
        path = directory / filename(row)
        data = json.loads(path.read_text(encoding="utf-8"))
        if data.get("source_sha") != sha:
            raise ValueError(f"{path}: source_sha does not match measured commit {sha}")
        if tree is not None and data.get("tree_sha") != tree:
            raise ValueError(f"{path}: tree_sha differs from the validated merge tree {tree}")
        identity = (data["category"], data["language"], data["bits"], data["size"])
        expected_identity = (
            row["category"],
            "Rust" if row["language"] == "rust" else "C#",
            row["bits"],
            row["size"],
        )
        if identity != expected_identity:
            raise ValueError(f"{path}: table identity {identity} differs from {expected_identity}")
        variants = {result["variant"] for result in data["results"]}
        if variants != expected_variants(row["category"], row["language"]):
            raise ValueError(f"{path}: storage variants differ from the complete benchmark plan")
        if verbose:
            print(f"Verified {path.name} from {sha}", file=sys.stderr)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("matrix", "verify"))
    parser.add_argument("--sizes", required=True, help="JSON array of benchmark sizes")
    parser.add_argument("--directory", type=Path)
    parser.add_argument("--sha")
    parser.add_argument("--tree")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()
    sizes = json.loads(args.sizes)
    if args.command == "matrix":
        print(json.dumps({"include": matrix(sizes)}, separators=(",", ":")))
    else:
        if args.directory is None or not args.sha:
            parser.error("verify requires --directory and --sha")
        verify(args.directory, sizes, args.sha, args.verbose, args.tree)
        print(f"Verified all {len(matrix(sizes))} benchmark artifacts")


if __name__ == "__main__":
    main()
