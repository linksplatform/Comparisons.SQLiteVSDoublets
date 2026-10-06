"""Recover provenance for committed results from their original CI logs and source commit."""

import argparse
import json
import re
import subprocess  # nosec B404 # Only local git object reads, with argument lists and no shell.
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from benchmark_provenance import record  # noqa: E402
from ci_process import executable  # noqa: E402


def backfill(directory, log):
    versions = {}
    source_shas = {}
    for line in log.read_text(encoding="utf-8").splitlines():
        parts = line.split("\t", 2)
        if len(parts) != 3:
            continue
        job, _, timed_message = parts
        message = timed_message.partition(" ")[2]
        source = re.search(r"\bSOURCE_SHA: ([0-9a-f]{40})\b", message)
        if source:
            source_shas[job] = source[1]
        if re.fullmatch(r"rustc \d+\.\d+\.\d+ .*", message):
            versions[job] = message
        match = re.search(r"\.NET Core SDK with version '([^']+)'", message)
        if match:
            versions[job] = f".NET SDK {match[1]}"
    for path in sorted(directory.glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        language = "rust" if data["language"] == "Rust" else "csharp"
        job = f"{language} {data['category']} {data['bits']} bit {data['size']}"
        toolchain = versions[job]
        sha = data["source_sha"]
        if not re.fullmatch(r"[0-9a-f]{40}", sha):
            raise ValueError(f"Invalid measured source SHA: {sha}")
        if source_shas.get(job) != sha:
            raise ValueError(f"{path}: logs do not match the measured source commit")
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = (
                "rust/Cargo.lock" if language == "rust" else "csharp/SQLiteVSDoublets/SQLiteVSDoublets.csproj"
            )
            original = subprocess.run(  # nosec B603 # Validated SHA and fixed manifest paths; no shell.
                [executable("git"), "show", f"{sha}:{manifest}"],
                capture_output=True,
                text=True,
                check=True,
            ).stdout
            target = root / manifest
            target.parent.mkdir(parents=True)
            target.write_text(original, encoding="utf-8")
            record(path, toolchain, root)
        print(f"{path.name}: {toolchain}, libraries from {sha}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("results", type=Path)
    parser.add_argument("log", type=Path, help="gh run view RUN --log output from the measured run")
    args = parser.parse_args()
    backfill(args.results, args.log)


if __name__ == "__main__":
    main()
