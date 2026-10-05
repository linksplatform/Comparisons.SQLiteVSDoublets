"""Reproduce an unnecessary-nosec warning on a correctly annotated nested call."""

import json
import subprocess  # nosec B404 # fixed local analyzer commands run without a shell.
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts"))
from ci_process import executable  # noqa: E402

FIXTURES = {
    "unannotated": 'import subprocess\nvalue = subprocess.run(["/usr/bin/true"]).stdout.strip()\n',
    "nested": ('import subprocess\nvalue = subprocess.run(["/usr/bin/true"]).stdout.strip() # nosec B603\n'),
    "separate": (
        'import subprocess\nresult = subprocess.run(["/usr/bin/true"]) # nosec B603\n'
        "value = result.stdout.strip()\n"
    ),
}


def main():
    with tempfile.TemporaryDirectory(prefix="bandit-nosec-") as temporary:
        for name, source in FIXTURES.items():
            path = Path(temporary) / f"{name}.py"
            path.write_text(source, encoding="utf-8")
            result = subprocess.run(  # nosec B603 # fixed analyzer options and generated fixture path.
                [executable("bandit"), "-q", "-t", "B603", "-f", "json", str(path)],
                capture_output=True,
                text=True,
                check=False,
            )
            findings = json.loads(result.stdout)["results"]
            expected = 1 if name == "unannotated" else 0
            if len(findings) != expected or result.returncode != expected:
                raise SystemExit(f"Unexpected {name} result: {result.stdout}{result.stderr}")
            warning = "nosec encountered (B603), but no failed test" in result.stderr
            if warning != (name == "nested"):
                raise SystemExit(f"Unexpected {name} warning: {result.stderr}")
            print(f"{name}: exit {result.returncode}, findings {len(findings)}, warning {warning}")
            print(result.stderr, end="")


if __name__ == "__main__":
    main()
