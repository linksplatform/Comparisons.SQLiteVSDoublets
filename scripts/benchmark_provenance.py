"""Record the library and toolchain versions used by a benchmark job."""

import argparse
import json
import re
import tomllib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def library_versions(language, root=ROOT):
    """Read the Rust lockfile or C# package reference for the measured Doublets library."""
    if language == "Rust":
        lock = tomllib.loads((root / "rust/Cargo.lock").read_text(encoding="utf-8"))
        versions = {entry["version"] for entry in lock["package"] if entry["name"] == "doublets"}
        if len(versions) != 1:
            raise ValueError("Cargo.lock must resolve exactly one doublets version")
        return {"doublets": versions.pop()}
    if language != "C#":
        raise ValueError(f"Unknown benchmark language: {language}")
    project = (root / "csharp/SQLiteVSDoublets/SQLiteVSDoublets.csproj").read_text(encoding="utf-8")
    for reference in re.findall(r"<PackageReference\b[^>]*>", project):
        attributes = dict(re.findall(r'([\w]+)\s*=\s*"([^"]*)"', reference))
        if attributes.get("Include") == "Platform.Data.Doublets" and attributes.get("Version"):
            return {"Platform.Data.Doublets": attributes["Version"]}
    raise ValueError("Missing Platform.Data.Doublets version in the C# project")


def record(path, toolchain, root=ROOT):
    """Add a software snapshot while preserving every measurement and existing metadata field."""
    if not toolchain.strip():
        raise ValueError("Missing measured toolchain version")
    data = json.loads(path.read_text(encoding="utf-8"))
    data["software"] = {"libraries": library_versions(data["language"], root), "toolchain": toolchain}
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    parser.add_argument("--toolchain", required=True, help="rustc --version or the measured .NET SDK version")
    args = parser.parse_args()
    record(args.report, args.toolchain)


if __name__ == "__main__":
    main()
