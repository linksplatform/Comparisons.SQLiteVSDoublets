#!/usr/bin/env python3
"""Turns the benchmark JSON reports into the README results sections and charts."""

# Usage: benchmark_report.py RESULTS_DIR [--readme README.md] [--readme README.ru.md] [--charts DIR]
#
# Every JSON report (written by `rust` and `csharp` with `--output`) is one table: a language,
# a category (links or objects), an address/id space (32 or 64 bit) and a size, measured on one machine.
# The README section between the markers is regenerated in the hierarchy
# category -> language -> bits -> size, and charts are written per category, language and bits.

import argparse
import json
import math
import re
import statistics
from pathlib import Path
from typing import Any

START_MARKER = "<!--BENCHMARK_RESULTS_START-->"
END_MARKER = "<!--BENCHMARK_RESULTS_END-->"
# The wide tables cannot be wrapped, and the hierarchy repeats headings under different parents.
LINT_OFF = "<!-- markdownlint-disable MD013 MD024 -->"
LINT_ON = "<!-- markdownlint-restore -->"

CATEGORIES = ("links", "objects")
NOISE = 0.05
LANGUAGES = ("Rust", "C#")
BITS = (32, 64)

OPERATIONS = {
    "links": (
        "create",
        "query_all",
        "query_by_id",
        "query_by_from_to",
        "query_by_from",
        "query_by_to",
        "update",
        "delete",
    ),
    "objects": ("create", "read_all", "read_by_id", "delete"),
}


def russian_plural(count, one, few, many):
    if count % 10 == 1 and count % 100 != 11:
        return one
    return few if 2 <= count % 10 <= 4 and not 12 <= count % 100 <= 14 else many


TEXT: dict[str, dict[str, Any]] = {
    "en": {
        "category": {
            "links": "Doublets vs SQLite as storage for links",
            "objects": "Doublets vs SQLite as storage for objects",
        },
        "language": "{language} doublets vs SQLite",
        "bits": "{bits} bit address/id space benchmarks",
        "size": {"links": "{size} links", "objects": "{size} blog posts"},
        "operations": {
            "create": "Create",
            "query_all": "Read all",
            "query_by_id": "Read by id",
            "query_by_from_to": "Search (from, to)",
            "query_by_from": "Read by from",
            "query_by_to": "Read by to",
            "update": "Update",
            "delete": "Delete",
            "read_all": "Read all",
            "read_by_id": "Read by id",
        },
        "storage": "Storage",
        "file_size": "File size",
        "faster": "{ratio}× faster",
        "slower": "{ratio}× slower",
        "same": "≈ same",
        "provenance": "_{repetitions} after a warm-up, median time per operation. SQLite {sqlite}, {machine}, {source}._",
        "repetitions": lambda count: f"{count} repetition" + ("" if count == 1 else "s"),
        "machine": "unknown machine",
        "local": "a local run",
        "run": "[GitHub Actions run]({url}) on {date}",
        "missing": "_No results yet._",
        "chart": "{language} doublets vs SQLite, {bits} bit, {category}",
        "nouns": {"links": "links", "objects": "objects"},
    },
    "ru": {
        "category": {
            "links": "Дуплеты против SQLite как хранилище связей",
            "objects": "Дуплеты против SQLite как хранилище объектов",
        },
        "language": "Дуплеты на {language} против SQLite",
        "bits": "Тесты с {bits}-битным пространством адресов/идентификаторов",
        "size": {"links": "{size} связей", "objects": "{size} записей блога"},
        "operations": {
            "create": "Создание",
            "query_all": "Чтение всех",
            "query_by_id": "Чтение по id",
            "query_by_from_to": "Поиск (from, to)",
            "query_by_from": "Чтение по from",
            "query_by_to": "Чтение по to",
            "update": "Обновление",
            "delete": "Удаление",
            "read_all": "Чтение всех",
            "read_by_id": "Чтение по id",
        },
        "storage": "Хранилище",
        "file_size": "Размер файлов",
        "faster": "в {ratio}× быстрее",
        "slower": "в {ratio}× медленнее",
        "same": "≈ так же",
        "provenance": "_{repetitions} после прогрева, медианное время одной операции. SQLite {sqlite}, {machine}, {source}._",
        "repetitions": lambda count: f"{count} повтор" + russian_plural(count, "", "а", "ов"),
        "machine": "неизвестная машина",
        "local": "локальный запуск",
        "run": "[запуск GitHub Actions]({url}) от {date}",
        "missing": "_Результатов пока нет._",
        "chart": "Дуплеты на {language} против SQLite, {bits} бит, {category}",
        "nouns": {"links": "связи", "objects": "объекты"},
    },
}


def load(directory):
    """All reports in `directory`, keyed by (category, language, bits, size)."""
    reports = {}
    for path in sorted(Path(directory).glob("*.json")):
        report = json.loads(path.read_text(encoding="utf-8"))
        validate(report, path)
        key = (report["category"], report["language"], report["bits"], report["size"])
        if key in reports:
            raise ValueError(f"{path} duplicates the results of {key}")
        reports[key] = report
    if not reports:
        raise ValueError(f"No benchmark reports found in {directory}")
    return reports


def validate(report, path):
    """Reject incomplete measurements before publishing tables or charts."""
    try:
        category = report["category"]
        if category not in CATEGORIES or report["language"] not in LANGUAGES:
            raise ValueError("unknown category or language")
        if report["bits"] not in BITS or type(report["size"]) is not int or report["size"] <= 0:
            raise ValueError("invalid bits or size")
        if type(report["repetitions"]) is not int or report["repetitions"] <= 0:
            raise ValueError("invalid repetitions")
        if not report["sqlite_version"]:
            raise ValueError("missing SQLite version")
        variants = [result["variant"] for result in report["results"]]
        if len(variants) != len(set(variants)):
            raise ValueError("duplicate variants")
        if not {"SQLite_Memory", "SQLite_File"}.issubset(variants):
            raise ValueError("missing SQLite baseline")
        for result in report["results"]:
            if set(result["operations"]) != set(OPERATIONS[category]):
                raise ValueError("missing or unexpected operations")
            for measurement in result["operations"].values():
                values = [measurement[key] for key in ("median_ns", "min_ns", "max_ns")]
                values += measurement["samples_ns"]
                if not measurement["samples_ns"] or any(
                    type(value) not in (int, float) or not math.isfinite(value) or value <= 0
                    for value in values
                ):
                    raise ValueError("measurements must be finite and positive")
                if not measurement["min_ns"] <= measurement["median_ns"] <= measurement["max_ns"]:
                    raise ValueError("median must be between min and max")
    except (KeyError, TypeError, ValueError) as error:
        raise ValueError(f"Invalid benchmark report {path}: {error}") from error


def baseline(variant):
    """Doublets are compared with SQLite of the same durability: volatile with memory, non-volatile with file."""
    if variant.startswith("SQLite"):
        return None
    return "SQLite_File" if "_NonVolatile" in variant else "SQLite_Memory"


def significant(value):
    """Three significant digits without an exponent: 999.6 is 1000, not 1e+03."""
    return f"{float(f'{value:.3g}'):g}"


def duration(nanoseconds):
    rounded = float(significant(nanoseconds))
    for unit, scale in (("s", 1e9), ("ms", 1e6), ("µs", 1e3)):
        if rounded >= scale:
            return f"{significant(rounded / scale)} {unit}"
    return f"{significant(rounded)} ns"


def size(count):
    return f"{count:,}"


def file_size(count):
    return "—" if count is None else f"{count / 2**20:.1f} MiB"


def quartiles(measured):
    """The middle half of the samples: unlike the full range, one outlier repetition does not widen it."""
    samples = measured["samples_ns"]
    if len(samples) == 1:
        return samples[0], samples[0]
    first, _, third = statistics.quantiles(samples, n=4, method="inclusive")
    return first, third


def comparison(measured, reference, text):
    """How `measured` compares with `reference`."""
    # Overlapping interquartile ranges and medians within NOISE of each other (single samples have no range)
    # are not called a difference.
    (low, high), (reference_low, reference_high) = quartiles(measured), quartiles(reference)
    overlapping = low <= reference_high and reference_low <= high
    ratio = max(measured["median_ns"], reference["median_ns"]) / min(
        measured["median_ns"], reference["median_ns"]
    )
    if overlapping or ratio < 1 + NOISE:
        return text["same"]
    if measured["median_ns"] <= reference["median_ns"]:
        return text["faster"].format(ratio=significant(reference["median_ns"] / measured["median_ns"]))
    return text["slower"].format(ratio=significant(measured["median_ns"] / reference["median_ns"]))


def table(report, text):
    operations = OPERATIONS[report["category"]]
    by_variant = {result["variant"]: result for result in report["results"]}
    header = [
        text["storage"],
        *(text["operations"][operation] for operation in operations),
        text["file_size"],
    ]
    lines = ["| " + " | ".join(header) + " |", "| --- |" + " ---: |" * (len(header) - 1)]
    for variant, result in by_variant.items():
        reference = by_variant.get(baseline(variant))
        cells = [variant.replace("_", " ")]
        for operation in operations:
            measured = result["operations"][operation]
            cell = duration(measured["median_ns"])
            if reference is not None:
                cell += f" ({comparison(measured, reference['operations'][operation], text)})"
            cells.append(cell)
        cells.append(file_size(result["file_bytes"]))
        lines.append("| " + " | ".join(cells) + " |")
    return "\n".join(lines)


def provenance(report, text):
    run = report.get("run_url")
    return text["provenance"].format(
        repetitions=text["repetitions"](report["repetitions"]),
        sqlite=report["sqlite_version"],
        machine=report.get("machine") or text["machine"],
        source=text["run"].format(url=run, date=report.get("date")) if run else text["local"],
    )


def chart_name(category, language, bits):
    return f"{category}-{language.lower().replace('#', 'sharp')}-{bits}.png"


def section(reports, language_code, charts_link):
    """The Markdown results section in the category -> language -> bits -> size hierarchy."""
    text = TEXT[language_code]
    lines = []
    for category in CATEGORIES:
        lines += [f"## {text['category'][category]}", ""]
        for language in LANGUAGES:
            lines += [f"### {text['language'].format(language=language)}", ""]
            for bits in BITS:
                lines += [f"#### {text['bits'].format(bits=bits)}", ""]
                sizes = sorted(key[3] for key in reports if key[:3] == (category, language, bits))
                if not sizes:
                    lines += [text["missing"], ""]
                    continue
                for count in sizes:
                    report = reports[(category, language, bits, count)]
                    lines += [
                        f"##### {text['size'][category].format(size=size(count))}",
                        "",
                        provenance(report, text),
                        "",
                        table(report, text),
                        "",
                    ]
                if charts_link is not None:
                    name = chart_name(category, language, bits)
                    title = text["chart"].format(
                        language=language, bits=bits, category=text["nouns"][category]
                    )
                    lines += [f"![{title}]({charts_link}/{name})", ""]
    return "\n".join(lines).rstrip() + "\n"


def replace_section(document, generated):
    pattern = re.compile(re.escape(START_MARKER) + ".*?" + re.escape(END_MARKER), re.DOTALL)
    if not pattern.search(document):
        raise ValueError(f"{START_MARKER} ... {END_MARKER} markers are missing")
    return pattern.sub(lambda _: f"{START_MARKER}\n{LINT_OFF}\n{generated}{LINT_ON}\n{END_MARKER}", document)


def charts(reports, directory):
    """One logarithmic bar chart per category, language and bits, with a panel per size."""
    import matplotlib

    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    directory.mkdir(parents=True, exist_ok=True)
    written = []
    for category in CATEGORIES:
        operations = OPERATIONS[category]
        labels = [TEXT["en"]["operations"][operation] for operation in operations]
        for language in LANGUAGES:
            for bits in BITS:
                sizes = sorted(key[3] for key in reports if key[:3] == (category, language, bits))
                if not sizes:
                    continue
                figure, axes = plt.subplots(
                    1, len(sizes), figsize=(6 * len(sizes), 5), squeeze=False, sharey=False
                )
                for axis, count in zip(axes[0], sizes, strict=True):
                    results = reports[(category, language, bits, count)]["results"]
                    width = 0.8 / len(results)
                    for index, result in enumerate(results):
                        axis.bar(
                            [position + index * width for position in range(len(operations))],
                            [result["operations"][operation]["median_ns"] for operation in operations],
                            width,
                            label=result["variant"].replace("_", " "),
                            color=plt.get_cmap("tab10")(index % 10),
                        )
                    axis.set_yscale("log")
                    axis.set_title(TEXT["en"]["size"][category].format(size=size(count)))
                    axis.set_ylabel("median ns per operation (log scale)")
                    axis.set_xticks([position + 0.4 - width / 2 for position in range(len(operations))])
                    axis.set_xticklabels(labels, rotation=30, ha="right")
                    axis.grid(axis="y", which="major", alpha=0.3)
                axes[0][0].legend(fontsize="small")
                figure.suptitle(TEXT["en"]["chart"].format(language=language, bits=bits, category=category))
                figure.tight_layout()
                path = directory / chart_name(category, language, bits)
                figure.savefig(path, dpi=80)
                plt.close(figure)
                written.append(path)
    return written


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("results", type=Path, help="directory with the JSON reports")
    parser.add_argument("--readme", type=Path, action="append", default=[], help="README to update in place")
    parser.add_argument("--charts", type=Path, help="directory for the charts, relative to the READMEs")
    options = parser.parse_args(arguments)
    reports = load(options.results)
    if options.charts is not None:
        charts(reports, options.charts)
    for readme in options.readme:
        language_code = "ru" if readme.name.endswith(".ru.md") else "en"
        link = (
            None
            if options.charts is None
            else options.charts.resolve().relative_to(readme.resolve().parent).as_posix()
        )
        document = readme.read_text(encoding="utf-8")
        readme.write_text(replace_section(document, section(reports, language_code, link)), encoding="utf-8")
    if not options.readme:
        print(section(reports, "en", None))


if __name__ == "__main__":
    main()
