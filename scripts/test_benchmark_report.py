#!/usr/bin/env python3
"""Tests for benchmark_report.py: run with `python -m unittest discover -s scripts`."""

import contextlib
import importlib.util
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

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

    def test_system_data_sqlite_is_compared_with_the_matching_provider_storage(self):
        self.assertEqual(report.baseline("SystemDataSQLite_Memory"), "SQLite_Memory")
        self.assertEqual(report.baseline("SystemDataSQLite_File"), "SQLite_File")
        data = results(
            "objects",
            {
                "SQLite_Memory": 1000,
                "SQLite_File": 2000,
                "SystemDataSQLite_Memory": 500,
                "SystemDataSQLite_File": 500,
            },
        )
        table = report.table(data, self.text).splitlines()
        self.assertEqual(table[4].count("2× faster"), 4)
        self.assertEqual(table[5].count("4× faster"), 4)

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
    def test_recorded_software_versions_are_inside_the_provenance_line(self):
        data = sample(software={"libraries": {"doublets": "0.4.0"}, "toolchain": "rustc 1.97.0"})
        text = report.provenance(data, report.TEXT["en"])
        self.assertIn("doublets 0.4.0", text)
        self.assertIn("rustc 1.97.0", text)
        self.assertEqual(text.count("_"), 2)

    def test_conclusions_are_generated_from_the_current_measurements(self):
        text = report.section({("links", "Rust", 64, 100_000): sample()}, "en", None)
        self.assertIn("## Conclusions", text)
        self.assertIn("8 faster, 0 slower, 0 approximately equal", text)
        data = sample()
        for measured in data["results"][-1]["operations"].values():
            measured.update(measurement(10_000))
        text = report.section({("links", "Rust", 64, 100_000): data}, "en", None)
        self.assertIn("0 faster, 8 slower, 0 approximately equal", text)

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
                "## Conclusions",
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
            f"[GitHub Actions run]({url}) on 2026-10-04._",
            text,
        )
        self.assertIn("toolchain version not recorded", text)
        self.assertNotIn("![", text)

    def test_charts_are_linked_per_category_language_and_bits(self):
        text = report.section(
            {("objects", "C#", 32, 100_000): sample("objects", "C#", 32)}, "ru", "docs/benchmarks"
        )
        self.assertIn("(docs/benchmarks/objects-csharp-32.png)", text)
        self.assertIn("## Дуплеты против SQLite как хранилище объектов", text)
        self.assertIn("локальный запуск", text)


def configured(kind="durable", size=1_000, **configuration):
    doublets = {"durable": report.TRANSACTIONAL, "memory_limit": "Doublets_United_NonVolatile"}[kind]
    tuned = {"SQLite_File_Tuned": 1000} if kind == "durable" else {}
    data = results("links", {"SQLite_File": 2000, **tuned, doublets: 4000})
    data.update(language="Rust", bits=32, size=size, repetitions=1, sqlite_version="3.53.2")
    data["configuration"] = {"kind": kind, "disk": {"random_read_4k_ns": 90_000, "fsync_ns": 1_500_000}}
    data["configuration"].update(configuration)
    return data


class ConfigurationTests(unittest.TestCase):
    text = report.TEXT["en"]

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def test_file_backed_stores_are_compared_with_the_tuned_sqlite_file_when_it_was_measured(self):
        tuned = {"SQLite_File": None, "SQLite_File_Tuned": None}
        self.assertEqual(report.baseline("Doublets_Split_NonVolatile", tuned), "SQLite_File_Tuned")
        self.assertEqual(report.baseline("Doublets_Split_NonVolatile", {"SQLite_File": None}), "SQLite_File")
        self.assertEqual(report.baseline(report.TRANSACTIONAL, tuned), "SQLite_File")
        self.assertEqual(report.baseline("SQLite_File_Tuned", tuned), "SQLite_File")
        self.assertEqual(report.baseline("SystemDataSQLite_File_Tuned"), "SQLite_File_Tuned")

    def test_transactional_store_is_compared_with_the_syncing_sqlite_file(self):
        table = report.table(configured(), self.text).splitlines()
        # SQLite File Tuned is twice as fast as SQLite File; the transactional store twice as slow.
        self.assertEqual(table[3].count("2× faster"), len(report.OPERATIONS["links"]))
        self.assertEqual(table[4].count("2× slower"), len(report.OPERATIONS["links"]))

    def test_configured_and_default_reports_stay_in_their_own_directories(self):
        (self.root / "configured.json").write_text(json.dumps(configured()), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "configured reports"):
            report.load(self.root)
        self.assertEqual(len(report.load_configurations(self.root)), 1)
        (self.root / "configured.json").write_text(json.dumps(sample()), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "default reports"):
            report.load_configurations(self.root)
        self.assertEqual(report.load_configurations(self.root / "missing"), {})

    def test_invalid_configurations_are_rejected(self):
        broken = {
            "durable without the transactional store": configured(),
            "limit with the transactional store": configured("memory_limit", memory_limit_bytes=2**20),
            "limit without the limit": configured("memory_limit"),
            "limit without the read latency": configured("memory_limit", memory_limit_bytes=2**20),
            "volatile store": configured(),
            "unknown": configured(),
            "negative latency": configured(),
            "zero dirty bytes": configured("memory_limit", memory_limit_bytes=2**20, dirty_bytes=0),
            "fractional dirty background bytes": configured(
                "memory_limit", memory_limit_bytes=2**20, dirty_background_bytes=0.5
            ),
        }
        broken["durable without the transactional store"]["results"].pop()
        broken["limit with the transactional store"]["results"][-1]["variant"] = report.TRANSACTIONAL
        del broken["limit without the read latency"]["configuration"]["disk"]["random_read_4k_ns"]
        broken["volatile store"]["results"][-1]["variant"] = "Doublets_United_Volatile"
        broken["unknown"]["configuration"]["kind"] = "unknown"
        broken["negative latency"]["configuration"]["disk"]["fsync_ns"] = -1
        for name, data in broken.items():
            with self.subTest(name=name), self.assertRaises(ValueError):
                report.validate(data, "report.json")

    def job(self, variant, number, **fields):
        """The report of one storage's job of a 24 MiB memory limit table, written to the directory."""
        data = configured("memory_limit", 1_000_000, memory_limit_bytes=24 * 2**20, dirty_bytes=2**20)
        data["results"] = results("links", {variant: 1000 * (number + 1)})["results"]
        data["configuration"].update(peak_bytes=20 * 2**20 + number, major_faults=100 + number)
        data["configuration"]["disk"] = {"random_read_4k_ns": 90_000 + number, "fsync_ns": 1_500_000}
        data.update(
            {
                "run_url": "https://github.com/o/r/actions/runs/1",
                "source_sha": "abc",
                "date": f"2026-11-2{number}",
                "machine": f"runner {number}",
                **fields,
            }
        )
        (self.root / f"{number}.json").write_text(json.dumps(data), encoding="utf-8")
        return data

    def test_the_jobs_of_one_memory_limit_table_are_merged(self):
        # The files are read in name order; the table keeps the order of the configured variants.
        self.job("Doublets_Split_NonVolatile", 1)
        self.job("SQLite_File", 2)
        self.job("Doublets_United_NonVolatile", 3)
        (merged,) = report.load_configurations(self.root).values()
        self.assertEqual(
            [result["variant"] for result in merged["results"]],
            list(report.CONFIGURED_VARIANTS["memory_limit"]),
        )
        self.assertEqual([job["machine"] for job in merged["jobs"]], ["runner 2", "runner 3", "runner 1"])
        self.assertEqual(merged["jobs"][0]["peak_bytes"], 20 * 2**20 + 2)
        self.assertEqual(merged["date"], "2026-11-23")
        self.assertNotIn("disk", merged["configuration"])
        for language in ("en", "ru"):
            with self.subTest(language=language):
                text = report.TEXT[language]
                document = "\n".join(report.configured_sections({(1,): merged}, text))
                self.assertIn(text["job_machines"], document)
                self.assertIn(text["job_limit_provenance"].format(limit="24 MiB"), document)
                self.assertIn(text["dirty_provenance"].format(dirty="1 MiB", background="1 MiB"), document)
                self.assertIn("| " + " | ".join(text["job_header"]) + " |", document)
                self.assertIn("| SQLite File | runner 2 | 90 µs | 1.5 ms | 20.0 MiB | 102 |", document)
                self.assertIn(
                    "| Doublets Split NonVolatile | runner 1 | 90 µs | 1.5 ms | 20.0 MiB | 101 |", document
                )
                # SQLite File's job measured 3 µs, the united store's 4 µs.
                slower = text["slower"].format(ratio="1.33")
                self.assertIn(f"| Doublets United NonVolatile | 4 µs ({slower})", document)

    def test_jobs_of_different_runs_or_storages_are_not_merged(self):
        cases = {
            "not measured by one run": {"run_url": "https://github.com/o/r/actions/runs/2"},
            "duplicates a storage": {"variant": "SQLite_File"},
            "no SQLite File baseline": {
                "variant": "Doublets_Split_NonVolatile",
                "first": "Doublets_United_NonVolatile",
            },
        }
        for message, fields in cases.items():
            with self.subTest(message=message):
                for stale in self.root.glob("*.json"):
                    stale.unlink()
                self.job(fields.pop("first", "SQLite_File"), 1)
                self.job(fields.pop("variant", "Doublets_United_NonVolatile"), 2, **fields)
                with self.assertRaisesRegex(ValueError, message):
                    report.load_configurations(self.root)
        # Only the memory limit tables are measured by several jobs.
        for number in (1, 2):
            (self.root / f"{number}.json").write_text(json.dumps(configured()), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "duplicates the results"):
            report.load_configurations(self.root)

    def test_configured_sections_label_the_limit_and_the_disk(self):
        reports = {
            report.configuration_key(data): data
            for data in (
                configured(),
                configured(
                    "memory_limit",
                    1_000_000,
                    memory_limit_bytes=24 * 2**20,
                    peak_bytes=25_000_000,
                    major_faults=12_345,
                    dirty_bytes=24 * 2**20 // 5,
                    dirty_background_bytes=24 * 2**20 // 10,
                ),
                configured("memory_limit", 1_000, memory_limit_bytes=24 * 2**20),
            )
        }
        for language in ("en", "ru"):
            with self.subTest(language=language):
                text = report.TEXT[language]
                document = report.section(
                    {("links", "Rust", 32, 1_000): sample(bits=32, size=1_000)}, language, None, reports
                )
                headings = [line for line in document.splitlines() if line.startswith("##")]
                durable, limit = (f"## {text['configurations'][kind]}" for kind in report.CONFIGURATIONS)
                self.assertLess(headings.index(durable), headings.index(limit))
                self.assertLess(headings.index(limit), headings.index(f"## {text['conclusions']}"))
                limited = [
                    heading for heading in headings if heading.startswith("### ") and "24 MiB" in heading
                ]
                self.assertEqual(len(limited), 2)
                self.assertLess(document.index("1,000 "), document.index("1,000,000 "))
                self.assertIn("23.8 MiB", document)
                self.assertIn("12,345", document)
                self.assertIn(
                    text["dirty_provenance"].format(dirty="4.8 MiB", background="2.4 MiB"), document
                )
                self.assertIn("90 µs", document)
                self.assertIn("1.5 ms", document)
                self.assertEqual(document.count(text["configuration_intro"]["memory_limit"]), 1)
                self.assertEqual(
                    sum(
                        text["configuration_names"]["memory_limit"] in line
                        for line in document.splitlines()
                        if line.startswith("- ")
                    ),
                    1,
                )


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

    def test_recorded_provenance_requires_the_measured_library_and_toolchain(self):
        for software in (
            {},
            {"libraries": {"doublets": "0.5.0"}, "toolchain": ""},
            {"libraries": {"other": "0.5.0"}, "toolchain": "rustc 1.99.0"},
        ):
            with self.subTest(software=software), self.assertRaisesRegex(ValueError, "software provenance"):
                report.validate(sample(software=software), "report.json")

    @unittest.skipUnless(importlib.util.find_spec("matplotlib"), "matplotlib is not installed")
    def test_linear_charts_keep_small_bars_visible_and_log_charts_keep_actual_values(self):
        data = sample()
        data["results"][-1]["operations"]["create"] = measurement(1)
        captured = {}

        def capture(figure, path, **kwargs):
            captured[path.name] = figure

        with patch("matplotlib.figure.Figure.savefig", autospec=True, side_effect=capture):
            paths = report.charts({("links", "Rust", 64, 100_000): data}, self.root)
        self.assertEqual({path.name for path in paths}, {"links-rust-64.png", "links-rust-64-linear.png"})
        for name, scale, minimum in (
            ("links-rust-64.png", "log", 1),
            ("links-rust-64-linear.png", "linear", 10),
        ):
            axis = captured[name].axes[0]
            self.assertEqual(axis.get_yscale(), scale)
            self.assertEqual(min(bar.get_height() for bar in axis.patches), minimum)
        self.assertEqual(data["results"][-1]["operations"]["create"]["median_ns"], 1)

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
            [
                "links-rust-64-linear.png",
                "links-rust-64.png",
                "objects-csharp-32-linear.png",
                "objects-csharp-32.png",
            ],
        )
        english = (self.root / "README.md").read_text(encoding="utf-8")
        self.assertIn("![Rust doublets vs SQLite, 64 bit, links, log scale](docs/links-rust-64.png)", english)
        self.assertIn(
            "![Rust doublets vs SQLite, 64 bit, links, linear scale](docs/links-rust-64-linear.png)", english
        )
        self.assertIn("Дуплеты на C# против SQLite", (self.root / "README.ru.md").read_text(encoding="utf-8"))


class RepositoryReportsTests(unittest.TestCase):
    def test_both_committed_readmes_match_the_generator_without_a_stale_tail(self):
        root = Path(__file__).resolve().parents[1]
        reports = report.load(root / "docs/benchmarks/results")
        configured = report.load_configurations(root / "docs/benchmarks/configurations")
        for name, language in (("README.md", "en"), ("README.ru.md", "ru")):
            with self.subTest(name=name):
                document = (root / name).read_text(encoding="utf-8")
                self.assertTrue(document.rstrip().endswith(report.END_MARKER))
                self.assertEqual(
                    document,
                    report.replace_section(
                        document, report.section(reports, language, "docs/benchmarks", configured)
                    ),
                )


if __name__ == "__main__":
    unittest.main()
