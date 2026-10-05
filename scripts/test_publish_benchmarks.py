"""A write rejection must retain its actual cause and retry only lost races."""

import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import benchmark_ci
import benchmark_report
import publish_benchmarks as publish
from test_benchmark_ci import complete_sample


class PushTests(unittest.TestCase):
    def test_only_non_fast_forward_can_be_retried(self):
        self.assertTrue(publish.is_race("! [rejected] HEAD -> main (non-fast-forward)"))
        self.assertTrue(publish.is_race("! [rejected] HEAD -> main (fetch first)"))
        for error in (
            "remote: error: GH013: Repository rule violations found\n! [rejected] (non-fast-forward)",
            "remote: error: GH006: Protected branch update failed\n! [rejected] (fetch first)",
            "fatal: Authentication failed",
            "Could not resolve host: github.com",
            "remote: Permission denied",
            "pre-receive hook declined",
        ):
            with self.subTest(error=error):
                self.assertFalse(publish.is_race(error))


class PublicationTests(unittest.TestCase):
    """Exercise real Git races against a temporary bare origin; never contact GitHub."""

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.origin = self.root / "origin.git"
        self.local = self.root / "local"
        self.other = self.root / "other"
        self.command("init", "--bare", "--initial-branch=main", str(self.origin))
        self.command("clone", str(self.origin), str(self.local))
        for name in ("README.md", "README.ru.md"):
            (self.local / name).write_text(
                f"# Benchmarks\n\n{benchmark_report.START_MARKER}\nold\n{benchmark_report.END_MARKER}\n"
            )
        (self.local / "scripts").mkdir()
        (self.local / "scripts/source.py").write_text("version = 1\n")
        self.commit(self.local, "Initial source")
        self.command("push", "origin", "main", cwd=self.local)
        self.sha = self.command("rev-parse", "HEAD", cwd=self.local).stdout.strip()
        self.command("clone", str(self.origin), str(self.other))
        self.inputs = self.root / "inputs"
        self.inputs.mkdir()
        for row in benchmark_ci.matrix([1000]):
            data = complete_sample(
                row["category"],
                "Rust" if row["language"] == "rust" else "C#",
                row["bits"],
                1000,
                source_sha=self.sha,
            )
            (self.inputs / benchmark_ci.filename(row)).write_text(json.dumps(data))
        previous = Path.cwd()
        os.chdir(self.local)
        self.addCleanup(os.chdir, previous)

    def command(self, *args, cwd=None):
        return subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True, check=True)

    def commit(self, directory, message):
        self.command("add", ".", cwd=directory)
        self.command(
            "-c",
            "user.name=Test",
            "-c",
            "user.email=test@example.org",
            "commit",
            "-m",
            message,
            cwd=directory,
        )

    def test_lost_race_refetches_and_preserves_intervening_commit(self):
        original = publish.git
        pushes = []

        def race(*args, **kwargs):
            if args[0] == "push":
                pushes.append(args)
                if len(pushes) == 1:
                    readme = self.other / "README.md"
                    readme.write_text(readme.read_text() + "\nIntervening documentation update.\n")
                    self.commit(self.other, "Concurrent documentation")
                    self.command("push", "origin", "main", cwd=self.other)
            return original(*args, **kwargs)

        # Git remains real. Avoid invoking network-dependent Markdown tooling here.
        actual_run = subprocess.run
        with (
            patch.object(publish, "git", side_effect=race),
            patch.object(publish.benchmark_report, "charts"),
            patch.object(publish.subprocess, "run", wraps=subprocess.run) as run,
        ):
            run.side_effect = lambda args, **kw: (
                subprocess.CompletedProcess(args, 0) if args[0] == "npx" else actual_run(args, **kw)
            )
            publish.publish(self.inputs, [1000], self.sha)
        self.assertEqual(len(pushes), 2)
        readme = self.command("--git-dir", str(self.origin), "show", "main:README.md").stdout
        self.assertIn("Intervening documentation update", readme)
        self.assertNotIn("\nold\n", readme)
        log = self.command("--git-dir", str(self.origin), "log", "--format=%s", "main").stdout
        self.assertIn("Concurrent documentation", log)

    def test_new_source_requires_new_measurements(self):
        (self.other / "scripts/source.py").write_text("version = 2\n")
        self.commit(self.other, "Changed benchmark source")
        self.command("push", "origin", "main", cwd=self.other)
        with self.assertRaisesRegex(RuntimeError, "source changed"):
            publish.publish(self.inputs, [1000], self.sha)
        count = self.command("--git-dir", str(self.origin), "rev-list", "--count", "main").stdout.strip()
        self.assertEqual(count, "2")


if __name__ == "__main__":
    unittest.main()
