"""Index preserved issue evidence without loading large logs into memory."""

import argparse
import hashlib
import json
import re
from itertools import islice
from pathlib import Path

DIAGNOSTIC = re.compile(
    r"##\[(?:error|warning)\]|\bFAILED\b|\bpanic(?:ked)?\b|\bMSB\d+\b|"
    r"timed? out|404 Not Found|\berror\[|\bwarning:",
    re.IGNORECASE,
)
SECRET = re.compile(r"\b(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{40,})\b")


def index(directory):
    files, diagnostics = [], []
    for path in sorted(directory.rglob("*")):
        if not path.is_file() or path.name in ("manifest.json", "diagnostic-index.json"):
            continue
        digest = hashlib.sha256()
        with path.open("rb") as source:
            for block in iter(lambda: source.read(65536), b""):
                digest.update(block)
        relative = str(path.relative_to(directory))
        files.append({"path": relative, "bytes": path.stat().st_size, "sha256": digest.hexdigest()})
        if path.suffix not in (".log", ".txt", ".md", ".json", ".tsv"):
            continue
        with path.open(encoding="utf-8", errors="replace") as source:
            line_number = 0
            while chunk := list(islice(source, 1500)):
                for line in chunk:
                    line_number += 1
                    if SECRET.search(line):
                        raise ValueError(f"Unredacted token pattern in {relative}:{line_number}")
                    if path.suffix == ".log" and DIAGNOSTIC.search(line):
                        # Keep the readable index within GitHub's per-file diff budget;
                        # original, unabridged lines remain in the archived logs.
                        diagnostics.append({"path": relative, "line": line_number, "text": line[:500]})
    (directory / "manifest.json").write_text(json.dumps(files, indent=2) + "\n", encoding="utf-8")
    (directory / "diagnostic-index.json").write_text(
        json.dumps(diagnostics, indent=2) + "\n", encoding="utf-8"
    )
    print(f"Indexed {len(files)} files ({sum(item['bytes'] for item in files):,} bytes)")
    print(f"Preserved {len(diagnostics)} diagnostic matches with original paths and line numbers")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    index(parser.parse_args().directory)
