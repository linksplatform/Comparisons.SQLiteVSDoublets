"""Make failed jobs and unexpected timeouts visible in the terminal status check."""

import json
import os
import subprocess
from pathlib import Path

import yaml


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
        if result == "cancelled" and superseded and policy == "true":
            print(f"{name}: superseded reader cancelled as configured")
            continue
        failed.append(f"{name}: {result}")
    return failed


def main():
    needs = json.loads(os.environ["NEEDS_JSON"])
    workflow = yaml.load(Path(os.environ["WORKFLOW_FILE"]).read_text(), Loader=yaml.BaseLoader)
    superseded = False
    if any(details.get("result") == "cancelled" for details in needs.values()):
        remote = subprocess.run(
            ["git", "ls-remote", "origin", os.environ["RUN_REF"]],
            capture_output=True,
            text=True,
            check=True,
        ).stdout.split()
        superseded = bool(remote and remote[0] != os.environ["RUN_SHA"])
    errors = failures(needs, workflow, superseded)
    if errors:
        raise SystemExit("Pipeline failed: " + "; ".join(errors))
    print("Pipeline passed")


if __name__ == "__main__":
    main()
