#!/usr/bin/env python3
"""Tests for benchmark_configuration.py: run with `python -m unittest discover -s scripts`."""

import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

import benchmark_configuration as configuration
import benchmark_report as report
from test_benchmark_report import results


class PlanTests(unittest.TestCase):
    def test_runs_become_named_matrix_rows_with_their_variants(self):
        rows = configuration.plan(
            [
                {"kind": "durable", "bits": 64, "size": 10_000_000},
                {"kind": "memory_limit", "bits": 32, "size": 1_000_000, "limit_mib": 24},
            ]
        )
        limited = "memory-limit-24MiB-links-rust-32-1000000"
        # Under a limit a store can take hours, so each storage gets its own job.
        self.assertEqual(
            [(row["name"], row["limit_mib"], row["variants"]) for row in rows],
            [
                (
                    "durable-links-rust-64-10000000",
                    0,
                    "SQLite_File,SQLite_File_Tuned,Doublets_United_NonVolatile_Transactional",
                ),
                (f"{limited}-sqlite-file", 24, "SQLite_File"),
                (f"{limited}-doublets-united-nonvolatile", 24, "Doublets_United_NonVolatile"),
                (f"{limited}-doublets-split-nonvolatile", 24, "Doublets_Split_NonVolatile"),
            ],
        )
        # The transactional store keeps its journal in memory, so it never runs under a limit.
        self.assertNotIn(report.TRANSACTIONAL, ",".join(row["variants"] for row in rows[1:]))

    def test_invalid_plans_are_rejected(self):
        for plan in (
            [],
            {"kind": "durable", "bits": 32, "size": 1},
            [{"kind": "volatile", "bits": 32, "size": 1}],
            [{"kind": "durable", "bits": 16, "size": 1}],
            [{"kind": "durable", "bits": 32, "size": 0}],
            [{"kind": "durable", "bits": 32, "size": True}],
            [{"kind": "durable", "bits": 32, "size": 10_000_001}],
            [{"kind": "durable", "bits": 32, "size": 1, "limit_mib": 24}],
            [{"kind": "memory_limit", "bits": 32, "size": 1}],
            [{"kind": "memory_limit", "bits": 32, "size": 1, "limit_mib": "24; reboot"}],
            [{"kind": "durable", "bits": 32, "size": 1}] * 2,
            [{"kind": "durable", "bits": 32, "size": 1, "variants": "SQLite_Memory"}],
        ):
            with self.subTest(plan=plan), self.assertRaises(ValueError):
                configuration.plan(plan)

    def test_main_prints_the_matrix(self):
        with contextlib.redirect_stdout(io.StringIO()) as output:
            configuration.main(["plan", '[{"kind": "durable", "bits": 32, "size": 1000}]'])
        self.assertEqual(json.loads(output.getvalue())["include"][0]["name"], "durable-links-rust-32-1000")


class RecordTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def measured(self, name, variants):
        data = results("links", dict.fromkeys(variants, 1000))
        data.update(
            language="Rust", bits=32, size=1000, repetitions=1, sqlite_version="3.53.2", source_sha="abc"
        )
        path = self.root / name
        path.write_text(json.dumps(data), encoding="utf-8")
        return path

    def test_cgroup_stats_read_the_peak_and_the_major_faults(self):
        stats = self.root / "stats"
        stats.write_text("25165824\nanon 1\nfile 2\npgfault 900\npgmajfault 345\noom 0\n", encoding="utf-8")
        self.assertEqual(configuration.cgroup_stats(stats), {"peak_bytes": 25_165_824, "major_faults": 345})

    def test_recorded_reports_pass_validation_and_render(self):
        durable = self.measured("durable.json", configuration.VARIANTS["durable"])
        stats = self.root / "stats"
        disk = '{"random_read_4k_ns": 90000, "fsync_ns": 1500000}'
        configuration.main(["record", str(durable), "--kind", "durable", "--disk", disk])
        # Each storage under the limit is measured and recorded by its own job.
        for peak, variant in enumerate(configuration.VARIANTS["memory_limit"]):
            limited = self.measured(f"limited-{variant}.json", [variant])
            stats.write_text(f"{25_000_000 + peak}\npgmajfault 7\n", encoding="utf-8")
            configuration.main(
                [
                    "record",
                    str(limited),
                    "--kind",
                    "memory_limit",
                    "--disk",
                    disk,
                    "--memory-limit-bytes",
                    str(24 * 2**20),
                    "--cgroup-stats",
                    str(stats),
                    "--dirty-bytes",
                    str(24 * 2**20 // 5),
                    "--dirty-background-bytes",
                    str(24 * 2**20 // 10),
                ]
            )
        limited = self.root / "limited-SQLite_File.json"
        recorded = json.loads(limited.read_text(encoding="utf-8"))
        self.assertEqual(recorded["source_sha"], "abc")
        self.assertEqual(
            recorded["configuration"],
            {
                "kind": "memory_limit",
                "memory_limit_bytes": 24 * 2**20,
                "peak_bytes": 25_000_000,
                "major_faults": 7,
                "dirty_bytes": 5_033_164,
                "dirty_background_bytes": 2_516_582,
                "disk": {"random_read_4k_ns": 90000, "fsync_ns": 1500000},
            },
        )
        tables = configuration.report(self.root, "abc")
        self.assertIn("memory limit 24 MiB", tables)
        self.assertIn("Dirty page cache limit 4.8 MiB, background writeback from 2.4 MiB.", tables)
        # One table compares the three jobs' storages, and a second one shows each job's peak.
        self.assertEqual(tables.count("| Doublets United NonVolatile | "), 2)
        self.assertEqual(tables.count("| Doublets Split NonVolatile | "), 2)
        self.assertIn("Each storage in its own job (below)", tables)
        self.assertEqual(tables.count(" | 90 µs | 1.5 ms | 23.8 MiB | 7 |"), 3)
        self.assertIn(report.TEXT["en"]["configurations"]["durable"], tables)
        with self.assertRaisesRegex(ValueError, "not measured from"):
            configuration.report(self.root, "def")

    def test_a_memory_limit_needs_its_bytes(self):
        path = self.measured("limited.json", configuration.VARIANTS["memory_limit"])
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            configuration.main(["record", str(path), "--kind", "memory_limit"])

    def test_the_dirty_thresholds_belong_to_a_memory_limit(self):
        path = self.measured("durable.json", configuration.VARIANTS["durable"])
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            configuration.main(["record", str(path), "--kind", "durable", "--dirty-bytes", "1048576"])

    def test_an_empty_download_has_no_tables(self):
        with self.assertRaisesRegex(ValueError, "No configured reports"):
            configuration.report(self.root, "abc")

    def test_probe_measures_the_disk_and_removes_its_files(self):
        try:
            latencies = configuration.probe(self.root / "probe", file_mib=1, reads=5, syncs=3)
        except OSError as error:  # tmpfs and some overlay file systems reject O_DIRECT
            self.skipTest(f"O_DIRECT is unavailable here: {error}")
        self.assertEqual(set(latencies), {"random_read_4k_ns", "fsync_ns"})
        self.assertTrue(all(value > 0 for value in latencies.values()))
        self.assertEqual(list((self.root / "probe").iterdir()), [])


if __name__ == "__main__":
    unittest.main()
