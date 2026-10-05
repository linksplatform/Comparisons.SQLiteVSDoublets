"""Publish validated benchmark results, recomputing documents after a lost race."""

import argparse
import json
import shutil
import subprocess  # nosec B404 # resolved CI tools use argument lists; no subprocess enables a shell.
import tempfile
from pathlib import Path

import benchmark_ci
import benchmark_report
from ci_process import executable


def is_race(message):
    blocked = ("GH006", "GH013", "protected branch", "rule violations", "pre-receive hook declined")
    return not any(item in message for item in blocked) and any(
        item in message for item in ("non-fast-forward", "fetch first")
    )


def git(*args, cwd=None, check=True):
    # Callers supply fixed Git operations and separately validated artifact/CI metadata.
    result = subprocess.run(  # nosec B603 # argument-list invocation, no shell or command from PR data.
        [executable("git"), *args], cwd=cwd, capture_output=True, text=True, check=False
    )
    if result.returncode and check:
        raise RuntimeError(f"git {args[0]} failed:\n{result.stdout}{result.stderr}")
    return result


def publish(directory, sizes, sha, branch="main", verbose=False):
    benchmark_ci.verify(directory, sizes, sha, verbose)
    root = Path(git("rev-parse", "--show-toplevel").stdout.strip())
    for attempt in range(1, 4):
        git("fetch", "origin", branch, cwd=root)
        # New source must be measured before its documentation is replaced.
        changed = git(
            "diff",
            "--name-only",
            sha,
            f"origin/{branch}",
            "--",
            "rust",
            "csharp",
            "scripts",
            ".github/workflows/benchmarks.yml",
            cwd=root,
        ).stdout.strip()
        if changed:
            raise RuntimeError(f"Benchmark source changed since {sha}; rerun benchmarks:\n{changed}")
        with tempfile.TemporaryDirectory(prefix="benchmark-publish-") as temporary:
            worktree = Path(temporary) / "report"
            git("worktree", "add", "--detach", str(worktree), f"origin/{branch}", cwd=root)
            try:
                results = worktree / "docs/benchmarks/results"
                results.mkdir(parents=True, exist_ok=True)
                for path in directory.glob("*.json"):
                    shutil.copy2(path, results / path.name)
                reports = benchmark_report.load(results)
                benchmark_report.charts(reports, worktree / "docs/benchmarks")
                for name in ("README.md", "README.ru.md"):
                    readme = worktree / name
                    text = benchmark_report.section(
                        reports, "ru" if ".ru." in name else "en", "docs/benchmarks"
                    )
                    readme.write_text(
                        benchmark_report.replace_section(readme.read_text(encoding="utf-8"), text),
                        encoding="utf-8",
                    )
                subprocess.run(  # nosec B603 # fixed versioned Markdown tool and document names, no shell.
                    [executable("npx"), "--yes", "markdownlint-cli@0.49.1", "README.md", "README.ru.md"],
                    cwd=worktree,
                    check=True,
                )
                git("add", "README.md", "README.ru.md", "docs/benchmarks", cwd=worktree)
                if git("diff", "--staged", "--quiet", cwd=worktree, check=False).returncode == 0:
                    print("Benchmark report is already current")
                    return
                git(
                    "-c",
                    "user.email=linksplatform@gmail.com",
                    "-c",
                    "user.name=LinksPlatformBencher",
                    "commit",
                    "-m",
                    "Update benchmark results [skip ci]",
                    cwd=worktree,
                )
                pushed = git("push", "origin", f"HEAD:{branch}", cwd=worktree, check=False)
                message = pushed.stdout + pushed.stderr
                if pushed.returncode == 0:
                    print(f"Published benchmark report on attempt {attempt}")
                    return
                if not is_race(message) or attempt == 3:
                    raise RuntimeError(f"Benchmark publication failed:\n{message}")
                print(f"Publication lost a branch race on attempt {attempt}; fetching and regenerating")
                if verbose:
                    print(message)
            finally:
                git("worktree", "remove", "--force", str(worktree), cwd=root)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--sizes", required=True)
    parser.add_argument("--sha", required=True)
    parser.add_argument("--branch", default="main")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()
    publish(args.directory.resolve(), json.loads(args.sizes), args.sha, args.branch, args.verbose)


if __name__ == "__main__":
    main()
