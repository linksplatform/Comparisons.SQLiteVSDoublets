"""A cancelled writer or an unproven timeout must never become a green pipeline."""

import unittest

from check_pipeline_status import failures


class StatusTests(unittest.TestCase):
    def test_success_and_intentional_skips(self):
        self.assertEqual(failures({"test": {"result": "success"}, "publish": {"result": "skipped"}}, {}), [])

    def test_timeout_or_failure_fails_current_run(self):
        for result in ("failure", "cancelled", "unknown"):
            self.assertTrue(failures({"test": {"result": result}}, {}))

    def test_only_proven_superseded_cancellable_readers_are_accepted(self):
        workflow = {
            "jobs": {
                "test": {"concurrency": {"cancel-in-progress": "true"}},
                "publish": {"concurrency": {"cancel-in-progress": "false"}},
            }
        }
        self.assertEqual(failures({"test": {"result": "cancelled"}}, workflow, True), [])
        self.assertTrue(failures({"publish": {"result": "cancelled"}}, workflow, True))
        self.assertTrue(failures({"test": {"result": "cancelled"}}, workflow, False))

    def test_empty_status_is_an_error(self):
        with self.assertRaises(ValueError):
            failures({}, {})


if __name__ == "__main__":
    unittest.main()
