"""Behavioral checks for CI permission and cancellation policies."""

import copy
import tempfile
import unittest
from pathlib import Path
from typing import Any

import ci_checks as ci


class PolicyTests(unittest.TestCase):
    def setUp(self):
        self.workflow: dict[str, Any] = {
            "permissions": {"contents": "read"},
            "jobs": {"test": {"runs-on": "ubuntu-24.04", "timeout-minutes": "10"}},
        }

    def test_read_only_workflow(self):
        self.assertEqual(ci.workflow_errors(self.workflow), [])

    def test_writer_cannot_be_cancelled_or_race_another_workflow(self):
        job = self.workflow["jobs"]["test"]
        job["permissions"] = {"contents": "write"}
        self.assertTrue(ci.workflow_errors(self.workflow))
        job["concurrency"] = {"group": ci.WRITER_GROUP, "cancel-in-progress": "false"}
        self.assertEqual(ci.workflow_errors(self.workflow), [])
        self.workflow["concurrency"] = {"cancel-in-progress": "true"}
        self.assertTrue(ci.workflow_errors(self.workflow))

    def test_matrix_runner_aliases_are_checked(self):
        self.workflow["jobs"]["test"]["strategy"] = {"matrix": {"os": ["windows-latest"]}}
        self.assertIn("mutable runner", " ".join(ci.workflow_errors(self.workflow)))

    def test_privileged_event_rejects_checkout(self):
        self.workflow["on"] = {"pull_request_target": {}}
        self.workflow["jobs"]["test"]["steps"] = [{"uses": "actions/checkout@" + "a" * 40}]
        self.assertIn("must not check out", " ".join(ci.workflow_errors(self.workflow)))

    def test_failure_suppression_is_rejected(self):
        broken = copy.deepcopy(self.workflow)
        broken["jobs"]["test"]["continue-on-error"] = "true"
        self.assertIn("hides failed checks", " ".join(ci.workflow_errors(broken)))

    def test_local_missing_link_is_a_failure(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            path = root / "README.md"
            path.write_text(
                "[code](missing.py) [web](https://example.com) [section](#local)", encoding="utf-8"
            )
            self.assertEqual(len(ci.local_link_errors(path, root)), 1)
            (root / "missing.py").touch()
            self.assertEqual(ci.local_link_errors(path, root), [])


if __name__ == "__main__":
    unittest.main()
