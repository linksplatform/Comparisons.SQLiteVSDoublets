#!/usr/bin/env python3
"""Tests for shared result formatting and Markdown publication helpers."""

import os
import tempfile
import unittest

import benchmark_report as report


class ResultTests(unittest.TestCase):
    def test_fastest_sqlite_variant_is_the_baseline(self):
        self.assertEqual(
            report.baseline_of({"SQLite_Memory": 200, "SQLite_File": 100}), 100
        )

    def test_formats_faster_and_slower_measurements(self):
        self.assertEqual(report.format_speedup(100, 1_000), "100 (10.0x faster)")
        self.assertEqual(report.format_speedup(1_000, 100), "1000 (10.0x slower)")

    def test_reports_every_missing_measurement(self):
        results = report.empty_results([("create", "Create")])

        missing = report.missing_measurements(
            results,
            [("create", "Create")],
            [("SQLite_Memory", "SQLite Memory", "blue")],
        )

        self.assertEqual(missing, ["create/SQLite_Memory"])


class MarkerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.path = os.path.join(self.temporary.name, "README.md")

    def write(self, content):
        with open(self.path, "w", encoding="utf-8") as handle:
            handle.write(content)

    def read(self):
        with open(self.path, "r", encoding="utf-8") as handle:
            return handle.read()

    def test_updates_only_the_selected_language(self):
        self.write(
            f"{report.RUST_START_MARKER}\nold rust\n{report.RUST_END_MARKER}\n"
            f"{report.CSHARP_START_MARKER}\nold csharp\n{report.CSHARP_END_MARKER}\n"
        )

        report.update_markers(
            self.path,
            r"new rust \g<0> $1",
            report.RUST_START_MARKER,
            report.RUST_END_MARKER,
        )

        self.assertIn(r"new rust \g<0> $1", self.read())
        self.assertIn("old csharp", self.read())

    def test_update_is_idempotent(self):
        self.write(
            f"{report.RUST_START_MARKER}\nold\n{report.RUST_END_MARKER}\n"
        )
        report.update_markers(
            self.path,
            "new",
            report.RUST_START_MARKER,
            report.RUST_END_MARKER,
        )

        self.assertFalse(
            report.update_markers(
                self.path,
                "new",
                report.RUST_START_MARKER,
                report.RUST_END_MARKER,
            )
        )

    def test_missing_markers_raise(self):
        self.write("# No results\n")

        with self.assertRaises(ValueError):
            report.update_markers(
                self.path,
                "new",
                report.RUST_START_MARKER,
                report.RUST_END_MARKER,
            )


if __name__ == "__main__":
    unittest.main()
