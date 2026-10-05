"""Regression tests for complete, current-run benchmark artifacts."""

import copy
import json
import tempfile
import unittest
from pathlib import Path

import benchmark_ci as ci
from test_benchmark_report import sample


def complete_sample(category, language, bits, size, **extra):
    data = sample(category, language, bits, size, **extra)
    measured = data["results"][0]
    data["results"] = [
        dict(copy.deepcopy(measured), variant=variant) for variant in sorted(ci.VARIANTS[category])
    ]
    return data


class ArtifactTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.sha = "a" * 40
        for row in ci.matrix([1000]):
            data = complete_sample(
                row["category"],
                "Rust" if row["language"] == "rust" else "C#",
                row["bits"],
                1000,
                source_sha=self.sha,
            )
            (self.root / ci.filename(row)).write_text(json.dumps(data), encoding="utf-8")

    def test_complete_current_run(self):
        ci.verify(self.root, [1000], self.sha)

    def test_missing_artifact_fails_even_when_other_tables_are_valid(self):
        next(self.root.glob("*.json")).unlink()
        with self.assertRaisesRegex(ValueError, "Missing"):
            ci.verify(self.root, [1000], self.sha)

    def test_missing_storage_variant_fails_even_with_valid_baselines(self):
        path = next(self.root.glob("*.json"))
        data = json.loads(path.read_text())
        data["results"] = [result for result in data["results"] if result["variant"].startswith("SQLite")]
        path.write_text(json.dumps(data), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "variants"):
            ci.verify(self.root, [1000], self.sha)

    def test_stale_result_is_not_a_substitute_for_a_failed_job(self):
        path = next(self.root.glob("*.json"))
        data = json.loads(path.read_text())
        data["source_sha"] = "b" * 40
        path.write_text(json.dumps(data), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "source_sha"):
            ci.verify(self.root, [1000], self.sha)

    def test_different_merge_trees_are_rejected(self):
        for path in self.root.glob("*.json"):
            data = json.loads(path.read_text())
            data["tree_sha"] = "b" * 40
            path.write_text(json.dumps(data), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "tree_sha"):
            ci.verify(self.root, [1000], self.sha, tree="c" * 40)

    def test_wrong_table_cannot_hide_behind_expected_filename(self):
        path = next(self.root.glob("*.json"))
        data = json.loads(path.read_text())
        data["size"] = 2000
        path.write_text(json.dumps(data), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "table identity"):
            ci.verify(self.root, [1000], self.sha)

    def test_extra_old_artifacts_are_rejected(self):
        (self.root / "stale.json").write_text("{}", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Unexpected"):
            ci.verify(self.root, [1000], self.sha)


class MatrixTests(unittest.TestCase):
    def test_default_matrix_preserves_twenty_tables(self):
        rows = ci.matrix([100000, 1000000, 10000000])
        self.assertEqual(len(rows), 20)
        self.assertFalse(any(row["category"] == "objects" and row["size"] > 1000000 for row in rows))

    def test_invalid_or_unbounded_inputs_fail_before_starting_jobs(self):
        for sizes in ([], [0], [-1], [True], [1.5], [1000, 1000], [100000000], "1000"):
            with self.subTest(sizes=sizes), self.assertRaises(ValueError):
                ci.matrix(sizes)

    def test_matrix_cannot_exceed_github_job_limit(self):
        with self.assertRaisesRegex(ValueError, "256 jobs"):
            ci.matrix(list(range(1, 34)))


if __name__ == "__main__":
    unittest.main()
