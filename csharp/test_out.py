#!/usr/bin/env python3
"""Regression tests for the BenchmarkDotNet JSON report pipeline."""

import json
import os
import tempfile
import unittest

import out


def complete_document():
    benchmarks = []
    mean = 1_000.4
    for method in out.METHODS:
        for variant, _label, _color in out.report.VARIANTS:
            benchmarks.append(
                {
                    "Method": method,
                    "Parameters": f"N=10&Variant={variant}",
                    "Statistics": {"Mean": mean},
                }
            )
            mean += 100
    return {"Title": "test", "Benchmarks": benchmarks}


class ParserTests(unittest.TestCase):
    def test_parses_every_operation_and_variant(self):
        results = out.parse_document(complete_document())

        self.assertFalse(
            out.report.missing_measurements(
                results,
                out.report.LINK_OPERATIONS,
                out.report.VARIANTS,
            )
        )
        self.assertEqual(results["create"]["Doublets_United_Volatile"], 1_000)

    def test_ignores_legacy_object_benchmarks(self):
        document = complete_document()
        document["Benchmarks"].append(
            {
                "Method": "SQLite",
                "Parameters": "N=10",
                "Statistics": {"Mean": 42},
            }
        )

        self.assertEqual(out.parse_document(document), out.parse_document(complete_document()))

    def test_ignores_failed_benchmarks_without_statistics(self):
        results = out.parse_document(
            {
                "Benchmarks": [
                    {
                        "Method": "Create",
                        "Parameters": "N=10&Variant=SQLite_Memory",
                    }
                ]
            }
        )

        self.assertFalse(out.report.has_any_results(results))


class MainTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.input = os.path.join(self.temporary.name, "results.json")
        self.results = os.path.join(self.temporary.name, "results.md")
        self.readme = os.path.join(self.temporary.name, "README.md")

    def write_json(self, document):
        with open(self.input, "w", encoding="utf-8") as handle:
            json.dump(document, handle)

    def test_generates_results_and_updates_readme(self):
        self.write_json(complete_document())
        with open(self.readme, "w", encoding="utf-8") as handle:
            handle.write(
                f"{out.report.CSHARP_START_MARKER}\nold\n"
                f"{out.report.CSHARP_END_MARKER}\n"
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
            self.assertIn("Each Concrete", handle.read())
        with open(self.readme, "r", encoding="utf-8") as handle:
            self.assertIn("SQLite File", handle.read())

    def test_rejects_incomplete_results(self):
        document = complete_document()
        document["Benchmarks"].pop()
        self.write_json(document)

        self.assertEqual(out.main([self.input, "--results", self.results]), 1)
        self.assertFalse(os.path.exists(self.results))

    def test_rejects_malformed_json(self):
        with open(self.input, "w", encoding="utf-8") as handle:
            handle.write("not json")

        self.assertEqual(out.main([self.input, "--results", self.results]), 1)


if __name__ == "__main__":
    unittest.main()
