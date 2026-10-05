#!/usr/bin/env python3
"""Check bounded C# provider benchmarks and their JSON reports on every supported OS."""

import json
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BENCHMARK = ROOT / "csharp/SQLiteVSDoublets/bin/Release/net10.0/sqlite-vs-doublets.dll"
VARIANTS = ("SQLite_Memory", "SQLite_File", "SystemDataSQLite_Memory", "SystemDataSQLite_File")
OPERATIONS = {
    "links": {"create", "query_all", "query_by_id", "query_by_from_to", "query_by_from", "query_by_to", "update", "delete"},
    "objects": {"create", "read_all", "read_by_id", "delete"},
}


class ProviderReportsTests(unittest.TestCase):
    """Validate the executable's provider selection, native loading and report output."""

    def test_reports(self):
        """Run both categories and id sizes with memory and file storage for each provider."""
        with tempfile.TemporaryDirectory() as directory:
            for category, operations in OPERATIONS.items():
                for bits in (32, 64):
                    with self.subTest(category=category, bits=bits):
                        output = Path(directory) / f"{category}-{bits}.json"
                        subprocess.run([
                            "dotnet", str(BENCHMARK), category, str(bits), "1000",
                            "--repetitions", "3", "--variants", ",".join(VARIANTS),
                            "--directory", directory, "--output", str(output),
                        ], check=True)
                        report = json.loads(output.read_text(encoding="utf-8"))
                        self.assertEqual(
                            (report["language"], report["category"], report["bits"], report["size"], report["repetitions"]),
                            ("C#", category, bits, 1000, 3),
                        )
                        providers = report["sqlite_providers"]
                        self.assertEqual(set(providers), {"Microsoft.Data.Sqlite", "System.Data.SQLite"})
                        self.assertTrue(all(provider["provider_version"] for provider in providers.values()))
                        self.assertEqual({provider["sqlite_version"] for provider in providers.values()}, {report["sqlite_version"]})
                        self.assertEqual([result["variant"] for result in report["results"]], list(VARIANTS))
                        for result in report["results"]:
                            self.assertEqual(set(result["operations"]), operations)
                            self.assertTrue(all(len(operation["samples_ns"]) == 3 for operation in result["operations"].values()))
                            self.assertEqual(result["file_bytes"] is None, result["variant"].endswith("_Memory"))
                            if result["file_bytes"] is not None:
                                self.assertGreater(result["file_bytes"], 0)


if __name__ == "__main__":
    unittest.main()
