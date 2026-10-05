"""Safe YAML loading must retain event and concurrency policy semantics."""

import unittest

import ci_checks
import yaml
from workflow_yaml import load_workflow


class WorkflowYamlTests(unittest.TestCase):
    def test_unquoted_privileged_event_is_still_checked(self):
        workflow = load_workflow(
            "on:\n  pull_request_target:\npermissions:\n  contents: read\n"
            "jobs:\n  test:\n    runs-on: ubuntu-24.04\n    timeout-minutes: 5\n"
            "    steps:\n      - uses: actions/checkout@revision\n"
        )
        self.assertIn("must not check out", " ".join(ci_checks.workflow_errors(workflow)))

    def test_quoted_events_and_boolean_policy(self):
        workflow = load_workflow("'on': [push]\nconcurrency:\n  cancel-in-progress: false\n")
        self.assertEqual(workflow["on"], ["push"])
        self.assertIs(workflow["concurrency"]["cancel-in-progress"], False)

    def test_python_object_tags_are_rejected(self):
        with self.assertRaises(yaml.constructor.ConstructorError):
            load_workflow("!!python/object:collections.Counter {}")

    def test_non_mapping_is_rejected(self):
        with self.assertRaises(ValueError):
            load_workflow("[]")


if __name__ == "__main__":
    unittest.main()
