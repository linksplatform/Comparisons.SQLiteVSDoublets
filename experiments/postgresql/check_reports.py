"""Check bounded PostgreSQL object benchmarks, metadata and explicit configuration."""

import json
import os
import subprocess  # nosec B404 # fixed executable and argument lists, without a shell.
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from benchmark_report import validate  # noqa: E402
from ci_process import executable  # noqa: E402

BENCHMARK = ROOT / "csharp/SQLiteVSDoublets/bin/Release/net10.0/sqlite-vs-doublets.dll"
VARIABLE = "POSTGRESQL_CONNECTION_STRING"
VARIANTS = "SQLite_Memory,SQLite_File,PostgreSQL_EFCore"


class PostgreSQLReports(unittest.TestCase):
    def run_benchmark(self, directory, bits, variants=None, configured=True):
        arguments = [
            executable("dotnet"),
            str(BENCHMARK),
            "objects",
            str(bits),
            "100",
            "--repetitions",
            "2",
            "--directory",
            directory,
        ]
        if variants:
            arguments += ["--variants", variants]
        environment = os.environ.copy()
        if not configured:
            environment.pop(VARIABLE, None)
        return subprocess.run(  # nosec B603 # fixed benchmark, bounded inputs and separate arguments.
            arguments, capture_output=True, text=True, env=environment, check=False
        )

    def test_both_id_widths_report_server_bytes_and_versions(self):
        self.assertTrue(os.environ.get(VARIABLE), f"Set {VARIABLE} for integration tests")
        with tempfile.TemporaryDirectory() as directory:
            for bits in (32, 64):
                with self.subTest(bits=bits):
                    result = self.run_benchmark(directory, bits, VARIANTS)
                    self.assertEqual(result.returncode, 0, result.stderr)
                    data = json.loads(result.stdout)
                    validate(data, "PostgreSQL benchmark")
                    postgres = data["postgresql"]
                    self.assertEqual(postgres["provider"], "Npgsql.EntityFrameworkCore.PostgreSQL")
                    self.assertTrue(all(postgres.values()))
                    self.assertNotIn("Password", result.stdout)
                    measured = next(row for row in data["results"] if row["variant"] == "PostgreSQL_EFCore")
                    self.assertIsNone(measured["file_bytes"])
                    self.assertGreater(measured["server_bytes"], 0)
                    self.assertEqual(
                        set(measured["operations"]), {"create", "read_all", "read_by_id", "delete"}
                    )
                    for operation in measured["operations"].values():
                        self.assertEqual(len(operation["samples_ns"]), 2)
                    self.assertFalse(list(Path(directory).iterdir()))

    def test_unconfigured_default_preserves_embedded_variants(self):
        with tempfile.TemporaryDirectory() as directory:
            result = self.run_benchmark(directory, 32, configured=False)
            self.assertEqual(result.returncode, 0, result.stderr)
            data = json.loads(result.stdout)
            self.assertNotIn("postgresql", data)
            self.assertEqual(len(data["results"]), 12)
            self.assertNotIn("PostgreSQL_EFCore", [row["variant"] for row in data["results"]])

    def test_explicit_unconfigured_postgresql_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            result = self.run_benchmark(directory, 32, "PostgreSQL_EFCore", configured=False)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn(f"Set {VARIABLE}", result.stderr)
            self.assertFalse(list(Path(directory).iterdir()))


if __name__ == "__main__":
    unittest.main()
