#!/usr/bin/env python3
"""Shared benchmark reporting helpers for the SQLite vs Doublets comparison."""

# The language-specific pipelines parse their own benchmark formats, then use
# this module for consistent Markdown tables, speedup annotations, linear and
# logarithmic charts, and in-place result-section updates. The report format
# follows the sibling LinksPlatform database comparisons.

import os
import re
import shutil
from datetime import datetime, timezone

try:
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    import numpy as np

    HAS_MATPLOTLIB = True
except ImportError:  # pragma: no cover - exercised only without matplotlib
    print("Warning: matplotlib/numpy not installed, skipping chart generation")
    HAS_MATPLOTLIB = False

RUST_START_MARKER = "<!--RUST_BENCHMARK_RESULTS_START-->"
RUST_END_MARKER = "<!--RUST_BENCHMARK_RESULTS_END-->"
CSHARP_START_MARKER = "<!--CSHARP_BENCHMARK_RESULTS_START-->"
CSHARP_END_MARKER = "<!--CSHARP_BENCHMARK_RESULTS_END-->"

# Operations shared by every language of this comparison. The first element of
# each pair is the identifier used by the benchmark runner, the second one is
# the label used in reports.
LINK_OPERATIONS = (
    ("create", "Create"),
    ("update", "Update"),
    ("delete", "Delete"),
    ("query_all", "Each All"),
    ("query_by_id", "Each Identity"),
    ("query_by_source_target", "Each Concrete"),
    ("query_by_source", "Each Outgoing"),
    ("query_by_target", "Each Incoming"),
)

# Object-like structures (blog posts), the operations the C# comparison has
# been built around from the beginning.
OBJECT_OPERATIONS = (
    ("objects_create", "Objects Create List"),
    ("objects_read", "Objects Read List"),
    ("objects_delete", "Objects Delete List"),
)

OPERATIONS = LINK_OPERATIONS + OBJECT_OPERATIONS

# Benchmarked backends: identifier, label and chart color.
DOUBLETS_VARIANTS = (
    ("Doublets_United_Volatile", "Doublets United Volatile", "salmon"),
    ("Doublets_United_NonVolatile", "Doublets United NonVolatile", "red"),
    ("Doublets_Split_Volatile", "Doublets Split Volatile", "lightgreen"),
    ("Doublets_Split_NonVolatile", "Doublets Split NonVolatile", "green"),
)

SQLITE_VARIANTS = (
    ("SQLite_Memory", "SQLite Memory", "lightblue"),
    ("SQLite_File", "SQLite File", "royalblue"),
)

VARIANTS = DOUBLETS_VARIANTS + SQLITE_VARIANTS

# Doublets cells are annotated relative to the fastest SQLite measurement of
# the same operation, the same way Comparisons.Neo4jVSDoublets annotates
# against the fastest of the two Neo4j modes.
BASELINES = tuple(key for key, _label, _color in SQLITE_VARIANTS)


def empty_results(operations=OPERATIONS):
    """Build an empty ``{operation: {variant: nanoseconds}}`` mapping."""
    return {op: {} for op, _label in operations}


def has_any_results(results):
    """Return ``True`` when at least one measurement was parsed."""
    return any(measurements for measurements in results.values())


def missing_measurements(results, operations=OPERATIONS, variants=VARIANTS):
    """List expected ``operation/variant`` pairs absent from parsed output."""
    missing = []
    for operation, _operation_label in operations:
        measured = results.get(operation, {})
        for variant, _variant_label, _color in variants:
            if not measured.get(variant):
                missing.append(f"{operation}/{variant}")
    return missing


def baseline_of(measurements, baselines=BASELINES):
    """Fastest baseline (SQLite) measurement of a single operation, 0 if none."""
    values = [measurements.get(key, 0) for key in baselines]
    values = [value for value in values if value]
    return min(values) if values else 0


def format_speedup(value, baseline):
    """Annotate ``value`` with how it compares to the ``baseline`` measurement."""
    if not value:
        return "N/A"
    if not baseline:
        return f"{value}"
    if value <= baseline:
        return f"{value} ({baseline / value:.1f}x faster)"
    return f"{value} ({value / baseline:.1f}x slower)"


def format_results_table(results, operations=OPERATIONS, variants=VARIANTS, baselines=BASELINES):
    """Render the Markdown results table (all numbers in nanoseconds)."""
    labels = [label for _key, label, _color in variants]
    cells_by_variant = []
    for key, _label, _color in variants:
        column = []
        for op, _op_label in operations:
            measurements = results.get(op, {})
            value = measurements.get(key, 0)
            if key in baselines:
                column.append(str(value) if value else "N/A")
            else:
                column.append(format_speedup(value, baseline_of(measurements, baselines)))
        cells_by_variant.append(column)

    widths = [
        max(len(label), *(len(cell) for cell in column))
        for label, column in zip(labels, cells_by_variant)
    ]
    operation_width = max(len("Operation"), *(len(label) for _key, label in operations))

    header = "| " + "Operation".ljust(operation_width) + " | "
    header += " | ".join(label.ljust(width) for label, width in zip(labels, widths))
    header += " |"
    separator = "|" + "-" * (operation_width + 2)
    separator += "".join("|" + "-" * (width + 2) for width in widths) + "|"

    lines = [header, separator]
    for index, (_op, op_label) in enumerate(operations):
        row = "| " + op_label.ljust(operation_width) + " | "
        row += " | ".join(
            column[index].ljust(width) for column, width in zip(cells_by_variant, widths)
        )
        row += " |"
        lines.append(row)

    return "\n".join(lines)


def build_provenance(
    benchmark_links=None,
    background_links=None,
    object_count=None,
    generated_at=None,
    language=None,
):
    """Describe how and when the committed results were produced."""
    benchmark_links = benchmark_links or os.environ.get("BENCHMARK_LINK_COUNT", "1000")
    background_links = background_links or os.environ.get("BACKGROUND_LINK_COUNT", "3000")
    if object_count is not False:
        object_count = object_count or os.environ.get("BENCHMARK_OBJECT_COUNT", "1000")
    generated_at = generated_at or datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")

    source = "a local benchmark run"
    repository = os.environ.get("GITHUB_REPOSITORY")
    run_id = os.environ.get("GITHUB_RUN_ID")
    if repository and run_id:
        source = (
            f"[GitHub Actions run {run_id}]"
            f"(https://github.com/{repository}/actions/runs/{run_id})"
        )

    prefix = f"_Generated {generated_at}"
    if language:
        prefix += f" for {language}"
    quantities = (
        f"{benchmark_links} benchmarked links, "
        f"{background_links} background links"
    )
    if object_count is not False:
        quantities += f", {object_count} objects"
    return f"{prefix} by {source} — {quantities}._"


def render_results_section(results, provenance=None, **table_options):
    """Render a results section: provenance line plus the results table."""
    provenance = provenance if provenance is not None else build_provenance()
    return f"{provenance}\n\n{format_results_table(results, **table_options)}"


def update_markers(path, section, start_marker, end_marker):
    """Replace a marked Markdown section and report whether it changed."""
    with open(path, "r", encoding="utf-8") as handle:
        document = handle.read()

    if start_marker not in document or end_marker not in document:
        raise ValueError(f"{path} does not contain the {start_marker} / {end_marker} markers")

    pattern = re.compile(
        re.escape(start_marker) + r".*?" + re.escape(end_marker),
        re.DOTALL,
    )
    replacement = f"{start_marker}\n{section}\n{end_marker}"
    updated = pattern.sub(lambda _match: replacement, document, count=1)

    if updated == document:
        return False

    with open(path, "w", encoding="utf-8") as handle:
        handle.write(updated)
    return True


def _series(results, variant, operations):
    """Measurements of one variant across all operations, 0 when missing."""
    return [results.get(op, {}).get(variant, 0) for op, _label in operations]


def _ensure_min_visible(values, minimum):
    """Keep non-zero bars at least ``minimum`` wide so they stay visible."""
    return [max(value, minimum) if value > 0 else 0 for value in values]


def _plot(results, path, title, log_scale, operations, variants):
    positions = np.arange(len(operations))
    width = 0.8 / len(variants)
    figure, axes = plt.subplots(figsize=(12, 8))

    series = {key: _series(results, key, operations) for key, _label, _color in variants}

    if log_scale:
        plotted = series
    else:
        # On a linear scale Doublets bars are invisible next to SQLite, so give
        # every non-zero measurement a minimum visible width (~0.5% of the
        # maximum), matching the sibling benchmark charts.
        all_values = [value for values in series.values() for value in values]
        max_value = max(all_values) if all_values else 1
        minimum = max_value * 0.005
        plotted = {key: _ensure_min_visible(values, minimum) for key, values in series.items()}

    offset_base = (len(variants) - 1) / 2
    for index, (key, label, color) in enumerate(variants):
        offset = (index - offset_base) * width
        axes.barh(positions + offset, plotted[key], width, label=label, color=color)

    axes.set_xlabel("Time (ns) – log scale" if log_scale else "Time (ns)")
    axes.set_title(title)
    axes.set_yticks(positions)
    axes.set_yticklabels([label for _op, label in operations])
    if log_scale:
        axes.set_xscale("log")
    axes.legend()
    figure.tight_layout()

    directory = os.path.dirname(path)
    if directory:
        os.makedirs(directory, exist_ok=True)
    figure.savefig(path)
    plt.close(figure)
    print(f"Generated {path}")


def generate_charts(
    results,
    prefix,
    title,
    output_dir="",
    operations=OPERATIONS,
    variants=VARIANTS,
):
    """Generate linear/log charts and return their paths when available."""
    if not HAS_MATPLOTLIB:
        return []

    linear = os.path.join(output_dir, f"{prefix}.png") if output_dir else f"{prefix}.png"
    logarithmic = (
        os.path.join(output_dir, f"{prefix}_log_scale.png")
        if output_dir
        else f"{prefix}_log_scale.png"
    )
    _plot(results, linear, title, False, operations, variants)
    _plot(results, logarithmic, title, True, operations, variants)
    return [linear, logarithmic]


def copy_charts(charts, docs_dir):
    """Copy generated charts into the documentation directory."""
    if not docs_dir:
        return []
    os.makedirs(docs_dir, exist_ok=True)
    copied = []
    for chart in charts:
        if os.path.exists(chart):
            destination = os.path.join(docs_dir, os.path.basename(chart))
            shutil.copyfile(chart, destination)
            copied.append(destination)
            print(f"Copied {chart} -> {destination}")
    return copied


def report_input_excerpt(path, lines=20):
    """Describe the tail of ``path`` so an unparsable run can be diagnosed."""
    if not os.path.exists(path):
        return f"{path} does not exist"

    with open(path, "r", encoding="utf-8") as handle:
        content = handle.read()

    if not content.strip():
        return f"{path} is empty"

    tail = content.splitlines()[-lines:]
    return "\n".join([f"Last {len(tail)} line(s) of {path}:", *tail])
