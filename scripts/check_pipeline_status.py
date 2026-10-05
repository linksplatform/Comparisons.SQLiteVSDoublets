"""Make failed jobs and unexpected timeouts visible in the terminal status check."""

import json
import os
import subprocess  # nosec B404 # only a fixed git query is executed, without a shell.
from pathlib import Path

from ci_process import executable
from workflow_yaml import load_workflow


def failures(needs, workflow, superseded=False):
    if not needs:
        raise ValueError("Empty pipeline status")
    failed = []
    for name, details in needs.items():
        result = details.get("result", "unknown")
        if result in ("success", "skipped"):
            continue
        policy = (
            workflow.get("jobs", {})
            .get(name, {})
            .get("concurrency", workflow.get("concurrency", {}))
            .get("cancel-in-progress")
        )
        if result == "cancelled" and superseded and str(policy).lower() == "true":
            print(f"{name}: superseded reader cancelled as configured")
            continue
        failed.append(f"{name}: {result}")
    return failed


def main():
    needs = json.loads(os.environ["NEEDS_JSON"])
    workflow = load_workflow(Path(os.environ["WORKFLOW_FILE"]).read_text())
    superseded = False
    if any(details.get("result") == "cancelled" for details in needs.values()):
        # GitHub's ref is a separate argument to this fixed read-only query; no shell.
        query = subprocess.run(  # nosec B603 # resolved Git executable, fixed query and separate ref argument.
            [executable("git"), "ls-remote", "origin", os.environ["RUN_REF"]],
            capture_output=True,
            text=True,
            check=True,
        )
        remote = query.stdout.split()
        superseded = bool(remote and remote[0] != os.environ["RUN_SHA"])
    errors = failures(needs, workflow, superseded)
    if errors:
        raise SystemExit("Pipeline failed: " + "; ".join(errors))
    print("Pipeline passed")


if __name__ == "__main__":
    main()
