"""Missing CI tools must fail before starting a subprocess."""

import unittest
from unittest.mock import patch

from ci_process import executable


class ExecutableTests(unittest.TestCase):
    def test_resolved_path_is_used(self):
        with patch("ci_process.shutil.which", return_value="/tools/git"):
            self.assertEqual(executable("git"), "/tools/git")

    def test_missing_tool_is_an_error(self):
        with patch("ci_process.shutil.which", return_value=None), self.assertRaises(FileNotFoundError):
            executable("git")


if __name__ == "__main__":
    unittest.main()
