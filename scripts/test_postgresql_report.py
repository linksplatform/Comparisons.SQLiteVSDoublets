"""Keep PostgreSQL measurements distinct from embedded storage measurements."""

import unittest

import benchmark_report as report
from test_benchmark_report import results, sample


class PostgreSQLReportTests(unittest.TestCase):
    def test_all_postgresql_versions_stay_inside_the_italic_provenance(self):
        data = sample(
            "objects",
            "C#",
            32,
            1000,
            postgresql={"server_version": "18.3", "provider_version": "10.0.3", "ef_core_version": "10.0.4"},
        )
        for language in ("en", "ru"):
            text = report.provenance(data, report.TEXT[language])
            self.assertIn("PostgreSQL 18.3", text)
            self.assertIn("EF Core 10.0.4", text)
            self.assertEqual(text.count("_"), 2)
            self.assertTrue(text.endswith("._"))

    def test_server_provider_has_no_embedded_durability_baseline(self):
        self.assertIsNone(report.baseline("PostgreSQL_EFCore"))

    def test_server_size_is_shown_separately_from_local_files(self):
        data = sample("objects", "C#", 32, 1000)
        data["results"] += results("objects", {"PostgreSQL_EFCore": 3000})["results"]
        data["results"][-1]["server_bytes"] = 2**20
        table = report.table(data, report.TEXT["en"])
        self.assertIn("Server relations", table)
        row = next(line for line in table.splitlines() if "PostgreSQL EFCore" in line)
        self.assertIn("1.0 MiB", row)
        self.assertNotIn("slower", row)

    def test_postgresql_results_require_server_and_provider_versions(self):
        data = sample("objects", "C#", 32, 1000)
        data["results"] += results("objects", {"PostgreSQL_EFCore": 3000})["results"]
        with self.assertRaisesRegex(ValueError, "PostgreSQL"):
            report.validate(data, "postgresql.json")


if __name__ == "__main__":
    unittest.main()
