"""Reproduce B506 on a loader that cannot construct Python objects."""

import subprocess  # nosec B404 # fixed local probe commands run without a shell.
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from ci_process import executable  # noqa: E402

FIXTURE = """import yaml
print(yaml.load("!!python/object:collections.Counter {}", Loader=yaml.BaseLoader))
"""


def main():
    with tempfile.TemporaryDirectory(prefix="bandit-loader-") as temporary:
        path = Path(temporary) / "fixture.py"
        path.write_text(FIXTURE, encoding="utf-8")
        subprocess.run(  # nosec B603 # fixed harmless fixture and the current Python executable.
            [sys.executable, str(path)], check=True
        )
        result = subprocess.run(  # nosec B603 # fixed analyzer options and generated fixture path.
            [executable("bandit"), "-t", "B506", "-f", "json", str(path)], check=False
        )
        if result.returncode != 1:
            raise SystemExit(f"Expected a B506 false positive, got exit {result.returncode}")


if __name__ == "__main__":
    main()
