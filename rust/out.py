#!/usr/bin/env python3
"""Parse Criterion bencher output and publish the Rust benchmark report."""

import argparse
import os
import re
import sys

REPOSITORY_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(REPOSITORY_ROOT, "scripts"))

import benchmark_report as report  # noqa: E402


# Criterion writes the beginning and end of a bencher record separately. Error
# text can therefore occur between ``test ...`` and ``bench: ...``. Stop at the
# next test record so an aborted benchmark cannot borrow its successor's value.
BENCHER_PATTERN = re.compile(
    r"test\s+(\w+)/(\w+)/(\d+)\s+\.\.\.\s*"
    r"(?:(?!\btest\s)[\s\S])*?"
    r"bench:\s+([\d,]+)\s+ns/iter"
)


def parse_text(content):
    """Parse bencher text into ``{operation: {variant: ns_per_iteration}}``."""
    results = report.empty_results()
    for match in BENCHER_PATTERN.finditer(content):
        operation, variant, _size, nanoseconds = match.groups()
        if operation in results:
            results[operation][variant] = int(nanoseconds.replace(",", ""))
    return results


def parse_results(path):
    if not os.path.exists(path):
        return report.empty_results()
    with open(path, "r", encoding="utf-8") as handle:
        return parse_text(handle.read())


def parse_args(argv):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", nargs="?", default="out.txt")
    parser.add_argument("--results", default="results.md")
    parser.add_argument("--readme", action="append", default=[])
    parser.add_argument("--docs-dir")
    parser.add_argument("--output-dir", default="")
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv if argv is not None else sys.argv[1:])
    results = parse_results(args.input)
    if not report.has_any_results(results):
        print(f"No benchmark data found in {args.input}")
        print(report.report_input_excerpt(args.input))
        return 1

    missing = report.missing_measurements(results)
    if missing:
        print("Benchmark output is incomplete; missing:")
        print("\n".join(missing))
        return 1

    provenance = report.build_provenance(language="Rust")
    section = report.render_results_section(results, provenance)
    section += (
        "\n\n![Rust benchmark comparison](docs/benchmarks/bench_rust.png)"
        "\n\n![Rust benchmark comparison, logarithmic scale]"
        "(docs/benchmarks/bench_rust_log_scale.png)"
    )
    with open(args.results, "w", encoding="utf-8") as handle:
        handle.write(section + "\n")
    print(report.format_results_table(results))
    print(f"Generated {args.results}")

    charts = report.generate_charts(
        results,
        "bench_rust",
        "Benchmark Comparison: SQLite vs Doublets (Rust)",
        args.output_dir,
    )
    report.copy_charts(charts, args.docs_dir)

    for readme in args.readme:
        changed = report.update_markers(
            readme,
            section,
            report.RUST_START_MARKER,
            report.RUST_END_MARKER,
        )
        print(f"{'Updated' if changed else 'No changes needed in'} {readme}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
