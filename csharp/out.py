#!/usr/bin/env python3
"""Parse BenchmarkDotNet JSON and publish the C# link benchmark report."""

import argparse
import json
import os
import sys
from urllib.parse import parse_qs

REPOSITORY_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(REPOSITORY_ROOT, "scripts"))

import benchmark_report as report  # noqa: E402


METHODS = {
    "Create": "create",
    "Update": "update",
    "Delete": "delete",
    "EachAll": "query_all",
    "EachIdentity": "query_by_id",
    "EachConcrete": "query_by_source_target",
    "EachOutgoing": "query_by_source",
    "EachIncoming": "query_by_target",
}


def parse_document(document):
    """Parse a BenchmarkDotNet full JSON export into shared report results."""
    results = report.empty_results(report.LINK_OPERATIONS)
    for benchmark in document.get("Benchmarks", []):
        operation = METHODS.get(benchmark.get("Method"))
        parameters = parse_qs(benchmark.get("Parameters", ""))
        variant = parameters.get("Variant", [None])[0]
        mean = benchmark.get("Statistics", {}).get("Mean")
        if operation in results and variant and isinstance(mean, (int, float)) and mean > 0:
            results[operation][variant] = round(mean)
    return results


def parse_results(path):
    if not os.path.exists(path):
        return report.empty_results(report.LINK_OPERATIONS)
    with open(path, "r", encoding="utf-8") as handle:
        return parse_document(json.load(handle))


def parse_args(argv):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", help="BenchmarkDotNet *-report-full.json file")
    parser.add_argument("--results", default="results.md")
    parser.add_argument("--readme", action="append", default=[])
    parser.add_argument("--docs-dir")
    parser.add_argument("--output-dir", default="")
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv if argv is not None else sys.argv[1:])
    try:
        results = parse_results(args.input)
    except (json.JSONDecodeError, OSError) as error:
        print(f"Cannot parse {args.input}: {error}")
        print(report.report_input_excerpt(args.input))
        return 1

    if not report.has_any_results(results):
        print(f"No benchmark data found in {args.input}")
        print(report.report_input_excerpt(args.input))
        return 1

    missing = report.missing_measurements(
        results,
        report.LINK_OPERATIONS,
        report.VARIANTS,
    )
    if missing:
        print("Benchmark output is incomplete; missing:")
        print("\n".join(missing))
        return 1

    provenance = report.build_provenance(language="C#", object_count=False)
    section = report.render_results_section(
        results,
        provenance,
        operations=report.LINK_OPERATIONS,
    )
    section += (
        "\n\n![C# benchmark comparison](docs/benchmarks/bench_csharp.png)"
        "\n\n![C# benchmark comparison, logarithmic scale]"
        "(docs/benchmarks/bench_csharp_log_scale.png)"
    )
    with open(args.results, "w", encoding="utf-8") as handle:
        handle.write(section + "\n")
    print(report.format_results_table(results, operations=report.LINK_OPERATIONS))
    print(f"Generated {args.results}")

    charts = report.generate_charts(
        results,
        "bench_csharp",
        "Benchmark Comparison: SQLite vs Doublets (C#)",
        args.output_dir,
        operations=report.LINK_OPERATIONS,
    )
    report.copy_charts(charts, args.docs_dir)

    for readme in args.readme:
        changed = report.update_markers(
            readme,
            section,
            report.CSHARP_START_MARKER,
            report.CSHARP_END_MARKER,
        )
        print(f"{'Updated' if changed else 'No changes needed in'} {readme}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
