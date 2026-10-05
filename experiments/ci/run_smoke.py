"""Run all eight small benchmark cells and validate their real JSON artifacts."""

import json
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
import benchmark_ci as ci  # noqa: E402

OUT = ROOT / "dev/log/issues/110/pulls/111/validation/smoke-results"


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    for row in ci.matrix([1000]):
        binary = (
            [str(ROOT / "rust/target/release/sqlite-vs-doublets")]
            if row["language"] == "rust"
            else ["dotnet", str(ROOT / "csharp/SQLiteVSDoublets/bin/Release/net10.0/sqlite-vs-doublets.dll")]
        )
        output = OUT / ci.filename(row)
        with tempfile.TemporaryDirectory(prefix="benchmark-smoke-") as directory:
            print(f"Running {output.name}", flush=True)
            subprocess.run(
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
