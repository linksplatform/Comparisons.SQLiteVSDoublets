"""Collect reproducible GitHub CI evidence for issue 110 without logging tokens."""

import concurrent.futures
import json
import subprocess  # nosec B404 # collector invokes the authenticated CLI with fixed API paths and no shell.
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "dev/log/issues/110/pulls/111"
REPO = "linksplatform/Comparisons.SQLiteVSDoublets"
sys.path.insert(0, str(ROOT / "scripts"))
from ci_process import executable  # noqa: E402


def capture(name, *args):
    path = OUT / name
    path.parent.mkdir(parents=True, exist_ok=True)
    result = subprocess.run(  # nosec B603 # all callers construct fixed CLI/API queries as argument lists.
        [executable(args[0]), *args[1:]], capture_output=True, text=True, check=False
    )
    path.write_text(result.stdout)
    if result.returncode:
        path.with_suffix(path.suffix + ".error.txt").write_text(result.stderr)
    print(f"{name}: exit {result.returncode}, {len(result.stdout)} bytes", flush=True)
    return result


def capture_job_log(run_id, job_id):
    prefix = OUT / f"ci-logs/{run_id}-job-{job_id}"
    result = subprocess.run(  # nosec B603 # fixed API path and numeric job ID, no shell.
        [
            executable("gh"),
            "api",
            "--allow-escape-sequences",
            f"repos/{REPO}/actions/jobs/{job_id}/logs",
        ],
        capture_output=True,
        check=False,
    )
    if result.returncode == 0:
        prefix.with_suffix(".log").write_bytes(result.stdout)
    else:
        prefix.with_suffix(".error.txt").write_bytes(result.stderr)
    print(f"job {job_id}: exit {result.returncode}, {len(result.stdout)} bytes", flush=True)


def main():
    queries = {
        "issue-110.json": [
            "gh",
            "issue",
            "view",
            "110",
            "--repo",
            REPO,
            "--json",
            "number,title,body,comments,createdAt,updatedAt,state,author,url",
        ],
        "pr-111.json": [
            "gh",
            "pr",
            "view",
            "111",
            "--repo",
            REPO,
            "--json",
            "number,title,body,comments,createdAt,updatedAt,state,isDraft,headRefName,baseRefName,commits,statusCheckRollup,url",
        ],
        "issue-comments.json": ["gh", "api", f"repos/{REPO}/issues/110/comments", "--paginate", "--slurp"],
        "pr-comments.json": ["gh", "api", f"repos/{REPO}/issues/111/comments", "--paginate", "--slurp"],
        "pr-review-comments.json": ["gh", "api", f"repos/{REPO}/pulls/111/comments", "--paginate", "--slurp"],
        "pr-reviews.json": ["gh", "api", f"repos/{REPO}/pulls/111/reviews", "--paginate", "--slurp"],
        "recent-pulls.json": [
            "gh",
            "api",
            f"repos/{REPO}/pulls?state=closed&sort=updated&direction=desc&per_page=20",
        ],
        "recent-runs.json": [
            "gh",
            "api",
            f"repos/{REPO}/actions/runs?created=>=2026-10-03&per_page=100",
            "--paginate",
            "--slurp",
        ],
        "branch-runs.json": [
            "gh",
            "run",
            "list",
            "--repo",
            REPO,
            "--branch",
            "issue-110-e1f96d946132",
            "--limit",
            "5",
            "--json",
            "databaseId,conclusion,createdAt,headSha,status,workflowName",
        ],
        "repository.json": ["gh", "api", f"repos/{REPO}"],
        "pr.diff": ["gh", "pr", "diff", "111", "--repo", REPO],
    }
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        futures = [pool.submit(capture, f"github/{name}", *args) for name, args in queries.items()]
        for future in futures:
            future.result()
    pages = json.loads((OUT / "github/recent-runs.json").read_text())
    ids = {run["id"] for page in pages for run in page["workflow_runs"]}
    ids.update([37294124568, 37293779105, 37293771868, 37293771766, 37293771933])

    def collect_run(run_id):
        prefix = f"ci-logs/{run_id}"
        metadata = capture(prefix + ".json", "gh", "api", f"repos/{REPO}/actions/runs/{run_id}")
        jobs = capture(
            prefix + "-jobs.json",
            "gh",
            "api",
            f"repos/{REPO}/actions/runs/{run_id}/jobs?filter=all&per_page=100",
            "--paginate",
            "--slurp",
        )
        if metadata.returncode == 0 and json.loads(metadata.stdout)["status"] == "completed":
            capture(prefix + ".log", "gh", "run", "view", str(run_id), "--repo", REPO, "--log")
        else:
            if jobs.returncode == 0:
                pages = json.loads(jobs.stdout)
                for page in pages:
                    for job in page["jobs"]:
                        if job["status"] != "completed":
                            continue
                        capture_job_log(run_id, job["id"])
            print(f"run {run_id}: preserved available job logs; refresh after completion", flush=True)

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        for future in [pool.submit(collect_run, run_id) for run_id in sorted(ids)]:
            future.result()
    # Keep the original error as evidence and recover downloads from earlier runs,
    # including jobs whose workflow has now completed and has a combined log.
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        futures = []
        for path in (OUT / "ci-logs").glob("*-job-*.error.txt"):
            if "terminal escape sequences" not in path.read_text():
                continue
            run_id, job_id = path.name.removesuffix(".error.txt").split("-job-")
            if run_id.isdigit() and job_id.isdigit():
                futures.append(pool.submit(capture_job_log, int(run_id), int(job_id)))
        for future in futures:
            future.result()
    return 0


if __name__ == "__main__":
    sys.exit(main())
