#!/usr/bin/env python3
"""Tests for benchmark_report.py: run with `python -m unittest discover -s scripts`."""

import contextlib
import importlib.util
import io
import json
import tempfile
import unittest
from pathlib import Path

import benchmark_report as report


def measurement(median, *samples):
    ordered = sorted(samples or [median])
    return {"median_ns": median, "min_ns": ordered[0], "max_ns": ordered[-1], "samples_ns": ordered}


def results(category, variants, file_bytes=None):
    return {
        "category": category,
        "results": [
            {
                "variant": variant,
                "file_bytes": file_bytes,
                "operations": {operation: measurement(median) for operation in report.OPERATIONS[category]},
            }
            for variant, median in variants.items()
        ],
    }


def sample(category="links", language="Rust", bits=64, size=100_000, **extra):
    data = results(category, {"SQLite_Memory": 1000, "SQLite_File": 2000, "Doublets_United_Volatile": 100})
    data.update(language=language, bits=bits, size=size, repetitions=3, sqlite_version="3.53.2", **extra)
    return data


class FormattingTests(unittest.TestCase):
    def test_durations_keep_three_significant_digits(self):
        self.assertEqual(report.duration(999), "999 ns")
        self.assertEqual(report.duration(1_234), "1.23 µs")
        self.assertEqual(report.duration(45_600_000), "45.6 ms")
        self.assertEqual(report.duration(2e9), "2 s")

    def test_rounding_up_moves_to_the_next_unit_instead_of_an_exponent(self):
        # Rust links, 32 bit, 1,000,000 links, read by to: SQLite Memory took 999.6 ns.
        self.assertEqual(report.duration(999.6), "1 µs")
        self.assertEqual(report.duration(999_700), "1 ms")
        self.assertEqual(report.significant(1234.5), "1230")

    def test_sizes(self):
        self.assertEqual(report.size(10_000_000), "10,000,000")
        self.assertEqual(report.file_size(64 * 2**20), "64.0 MiB")
        self.assertEqual(report.file_size(None), "—")

    def test_russian_plurals(self):
        repetitions = report.TEXT["ru"]["repetitions"]
        self.assertEqual(
            [repetitions(count) for count in (1, 3, 5, 11, 21)],
            ["1 повтор", "3 повтора", "5 повторов", "11 повторов", "21 повтор"],
        )


class ComparisonTests(unittest.TestCase):
    text = report.TEXT["en"]

    def test_doublets_are_compared_with_sqlite_of_the_same_durability(self):
        self.assertEqual(report.baseline("Doublets_Split_Volatile"), "SQLite_Memory")
        self.assertEqual(report.baseline("Doublets_United_NonVolatile_Cached"), "SQLite_File")
        self.assertIsNone(report.baseline("SQLite_File"))

    def test_clear_differences(self):
        self.assertEqual(report.comparison(measurement(100), measurement(1000), self.text), "10× faster")
        self.assertEqual(report.comparison(measurement(2500), measurement(1000), self.text), "2.5× slower")
        self.assertEqual(report.comparison(measurement(1), measurement(1000), self.text), "1000× faster")

    def test_overlapping_interquartile_ranges_are_not_a_difference(self):
        measured, reference = (
            measurement(150, 90, 100, 150, 200, 300),
            measurement(190, 150, 180, 190, 210, 260),
        )
        self.assertEqual(report.quartiles(measured), (100, 200))
        self.assertEqual(report.comparison(measured, reference, self.text), "≈ same")

    def test_outlier_repetitions_do_not_hide_a_difference(self):
        # C# objects, 1,000 blog posts, read by id: Doublets Split NonVolatile Cached vs SQLite File (µs).
        doublets = measurement(3.75, 2.1, 2.6, 2.7, 3.1, 3.5, 4.0, 5.1, 6.3, 15.7, 23.0)
        sqlite = measurement(19.0, 5.7, 11.4, 16.9, 17.6, 18.2, 19.8, 20.6, 28.2, 29.1, 90.9)
        self.assertEqual(report.comparison(doublets, sqlite, self.text), "5.07× faster")

    def test_single_samples_within_noise_are_not_a_difference(self):
        self.assertEqual(report.comparison(measurement(1040), measurement(1000), self.text), "≈ same")
        self.assertEqual(report.comparison(measurement(1100), measurement(1000), self.text), "1.1× slower")

    def test_table_compares_every_operation_with_the_baseline(self):
        table = report.table(sample(), self.text).splitlines()
        self.assertEqual(len(table), 2 + 3)
        self.assertEqual(table[0].count("|"), len(report.OPERATIONS["links"]) + 3)
        self.assertEqual(table[1], "| --- |" + " ---: |" * (len(report.OPERATIONS["links"]) + 1))
        self.assertNotIn("faster", table[2])
        self.assertEqual(table[4].count("10× faster"), len(report.OPERATIONS["links"]))


class SectionTests(unittest.TestCase):
    def test_hierarchy_is_category_language_bits_size(self):
        reports = {
            ("links", "Rust", 64, 1_000_000): sample(size=1_000_000),
            ("links", "Rust", 64, 100_000): sample(),
            ("objects", "C#", 32, 100_000): sample("objects", "C#", 32),
        }
        headings = [
            line for line in report.section(reports, "en", "docs").splitlines() if line.startswith("#")
        ]
        self.assertEqual(
            headings,
            [
                "## Doublets vs SQLite as storage for links",
                "### Rust doublets vs SQLite",
                "#### 32 bit address/id space benchmarks",
                "#### 64 bit address/id space benchmarks",
                "##### 100,000 links",
                "##### 1,000,000 links",
                "### C# doublets vs SQLite",
                "#### 32 bit address/id space benchmarks",
                "#### 64 bit address/id space benchmarks",
                "## Doublets vs SQLite as storage for objects",
                "### Rust doublets vs SQLite",
                "#### 32 bit address/id space benchmarks",
                "#### 64 bit address/id space benchmarks",
                "### C# doublets vs SQLite",
                "#### 32 bit address/id space benchmarks",
                "##### 100,000 blog posts",
                "#### 64 bit address/id space benchmarks",
            ],
        )

    def test_missing_groups_and_provenance(self):
        url = "https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/1"
        reports = {
            ("links", "Rust", 64, 100_000): sample(run_url=url, date="2026-10-04", machine="ubuntu-24.04")
        }
        text = report.section(reports, "en", None)
        self.assertEqual(text.count("_No results yet._"), 7)
        self.assertIn(
            f"_3 repetitions after a warm-up, median time per operation. SQLite 3.53.2, ubuntu-24.04, "
            f"[GitHub Actions run]({url}) on 2026-10-04._",
            text,
        )
        self.assertNotIn("![", text)

    def test_charts_are_linked_per_category_language_and_bits(self):
        text = report.section(
            {("objects", "C#", 32, 100_000): sample("objects", "C#", 32)}, "ru", "docs/benchmarks"
        )
        self.assertIn("(docs/benchmarks/objects-csharp-32.png)", text)
        self.assertIn("## Дуплеты против SQLite как хранилище объектов", text)
        self.assertIn("локальный запуск", text)


class DocumentTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def test_only_the_marked_section_is_replaced(self):
        document = f"before\n{report.START_MARKER}\nold\n{report.END_MARKER}\nafter\n"
        self.assertEqual(
            report.replace_section(document, "new\n"),
            f"before\n{report.START_MARKER}\n{report.LINT_OFF}\nnew\n{report.LINT_ON}\n"
            f"{report.END_MARKER}\nafter\n",
        )
        with self.assertRaises(ValueError):
            report.replace_section("no markers", "new\n")

    def test_usage_starts_with_the_summary(self):
        with contextlib.redirect_stdout(io.StringIO()) as output, self.assertRaises(SystemExit):
            report.main(["--help"])
        self.assertIn(
            "Turns the benchmark JSON reports into the README results sections and charts.", output.getvalue()
        )

    def test_duplicate_reports_are_rejected(self):
        for name in ("a.json", "b.json"):
            (self.root / name).write_text(json.dumps(sample()), encoding="utf-8")
        with self.assertRaises(ValueError):
            report.load(self.root)

    def test_empty_artifact_download_cannot_generate_a_successful_report(self):
        with self.assertRaisesRegex(ValueError, "No benchmark reports"):
            report.load(self.root)

    def test_incomplete_and_nonfinite_measurements_are_rejected(self):
        for broken in ("missing operation", "missing baseline", "NaN", "negative"):
            with self.subTest(broken=broken):
                data = sample()
                if broken == "missing operation":
                    del data["results"][0]["operations"]["create"]
                elif broken == "missing baseline":
                    data["results"] = data["results"][2:]
                else:
                    data["results"][0]["operations"]["create"]["median_ns"] = (
                        float("nan") if broken == "NaN" else -1
                    )
                (self.root / "broken.json").write_text(json.dumps(data), encoding="utf-8")
                with self.assertRaises(ValueError):
                    report.load(self.root)

    @unittest.skipUnless(importlib.util.find_spec("matplotlib"), "matplotlib is not installed")
    def test_main_updates_readmes_and_writes_charts(self):
        results_directory = self.root / "results"
        results_directory.mkdir()
        for index, data in enumerate((sample(), sample(size=1_000_000), sample("objects", "C#", 32))):
            (results_directory / f"{index}.json").write_text(json.dumps(data), encoding="utf-8")
        for name in ("README.md", "README.ru.md"):
            (self.root / name).write_text(
                f"intro\n{report.START_MARKER}\n{report.END_MARKER}\n", encoding="utf-8"
            )
        report.main(
            [
                str(results_directory),
                "--readme",
                str(self.root / "README.md"),
                "--readme",
                str(self.root / "README.ru.md"),
                "--charts",
                str(self.root / "docs"),
            ]
        )
        self.assertEqual(
            sorted(path.name for path in (self.root / "docs").iterdir()),
            ["links-rust-64.png", "objects-csharp-32.png"],
        )
        english = (self.root / "README.md").read_text(encoding="utf-8")
        self.assertIn("![Rust doublets vs SQLite, 64 bit, links](docs/links-rust-64.png)", english)
        self.assertIn("Дуплеты на C# против SQLite", (self.root / "README.ru.md").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
