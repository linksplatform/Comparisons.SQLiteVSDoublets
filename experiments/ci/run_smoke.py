"""Run all eight small benchmark cells and validate their real JSON artifacts."""

import json
import subprocess  # nosec B404 # only local benchmark binaries and fixed Git queries run, without a shell.
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
import benchmark_ci as ci  # noqa: E402
from ci_process import executable  # noqa: E402

OUT = ROOT / "dev/log/issues/110/pulls/111/validation/smoke-results"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    revision = subprocess.run(  # nosec B603 # resolved Git executable and fixed read-only arguments.
        [executable("git"), "rev-parse", "HEAD"], cwd=ROOT, text=True, capture_output=True, check=True
    )
    sha = revision.stdout.strip()
    for row in ci.matrix([1000]):
        binary = (
            [str(ROOT / "rust/target/release/sqlite-vs-doublets")]
            if row["language"] == "rust"
            else [
                executable("dotnet"),
                str(ROOT / "csharp/SQLiteVSDoublets/bin/Release/net10.0/sqlite-vs-doublets.dll"),
            ]
        )
        output = OUT / ci.filename(row)
        with tempfile.TemporaryDirectory(prefix="benchmark-smoke-") as directory:
            print(f"Running {output.name}", flush=True)
            subprocess.run(  # nosec B603 # bounded matrix values and generated temporary paths, no shell.
                [
                    *binary,
                    row["category"],
                    str(row["bits"]),
                    str(row["size"]),
                    "--repetitions",
                    "1",
                    "--directory",
                    directory,
                    "--output",
                    str(output),
                ],
                cwd=ROOT,
                check=True,
            )
        data = json.loads(output.read_text())
        data.update(source_sha=sha, machine="local bounded smoke test", date="2026-10-05")
        output.write_text(json.dumps(data, indent=2) + "\n")
    ci.verify(OUT, [1000], sha, verbose=True)


if __name__ == "__main__":
    main()
