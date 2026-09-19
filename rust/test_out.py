#!/usr/bin/env python3
"""Regression tests for the Criterion result parser and Rust report CLI."""

import os
import tempfile
import unittest

import out


def complete_output():
    lines = ["Gnuplot not found, using plotters backend"]
    value = 1_000
    for operation, _label in out.report.OPERATIONS:
        for variant, _variant_label, _color in out.report.VARIANTS:
            lines.append(
                f"test {operation}/{variant}/10 ... bench: {value:,} ns/iter (+/- 10)"
            )
            value += 100
    return "\n".join(lines) + "\n"


class ParserTests(unittest.TestCase):
    def test_parses_every_operation_and_variant(self):
        results = out.parse_text(complete_output())

        self.assertFalse(out.report.missing_measurements(results))
        self.assertEqual(results["create"]["Doublets_United_Volatile"], 1_000)

    def test_accepts_error_text_inside_a_record(self):
        results = out.parse_text(
            "test create/SQLite_Memory/10 ... Criterion.rs ERROR: stale cache\n"
            "bench: 12,345 ns/iter (+/- 10)\n"
        )

        self.assertEqual(results["create"]["SQLite_Memory"], 12_345)

    def test_does_not_borrow_the_next_record_measurement(self):
        results = out.parse_text(
            "test create/SQLite_Memory/10 ...\n"
            "test create/SQLite_File/10 ... bench: 42 ns/iter (+/- 1)\n"
        )

        self.assertNotIn("SQLite_Memory", results["create"])
        self.assertEqual(results["create"]["SQLite_File"], 42)


class MainTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.input = os.path.join(self.temporary.name, "out.txt")
        self.results = os.path.join(self.temporary.name, "results.md")
        self.readme = os.path.join(self.temporary.name, "README.md")

    def write(self, path, content):
        with open(path, "w", encoding="utf-8") as handle:
            handle.write(content)

    def test_generates_results_and_updates_each_readme(self):
        self.write(self.input, complete_output())
        self.write(
            self.readme,
            "# Results\n\n"
            f"{out.report.RUST_START_MARKER}\nold\n"
            f"{out.report.RUST_END_MARKER}\n",
        )

        result = out.main(
            [
                self.input,
                "--results",
                self.results,
                "--readme",
                self.readme,
                "--output-dir",
                self.temporary.name,
            ]
        )

        self.assertEqual(result, 0)
        with open(self.results, "r", encoding="utf-8") as handle:
            self.assertIn("Objects Delete List", handle.read())
        with open(self.readme, "r", encoding="utf-8") as handle:
            self.assertIn("Doublets Split NonVolatile", handle.read())

    def test_rejects_partial_output(self):
        self.write(
            self.input,
            "test create/SQLite_Memory/10 ... bench: 42 ns/iter (+/- 1)\n",
        )

        self.assertEqual(out.main([self.input, "--results", self.results]), 1)
        self.assertFalse(os.path.exists(self.results))

    def test_empty_input_reports_failure(self):
        self.write(self.input, "")

        self.assertEqual(out.main([self.input, "--results", self.results]), 1)


if __name__ == "__main__":
    unittest.main()
