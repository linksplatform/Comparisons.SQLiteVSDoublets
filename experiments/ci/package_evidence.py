"""Package and verify raw evidence while keeping GitHub PR diffs reviewable."""

import argparse
import gzip
import hashlib
import json
import tarfile
from pathlib import Path

DIRECTORIES = ("ci-logs", "github", "research", "templates", "validation")
ARCHIVE = "raw-evidence.tar.gz"


def package(directory):
    missing = [name for name in DIRECTORIES if not (directory / name).is_dir()]
    if missing:
        raise FileNotFoundError(f"Extract {ARCHIVE} before packaging; missing directories: {missing}")
    target = directory / ARCHIVE
    count = 0
    with target.open("wb") as destination:
        with gzip.GzipFile(filename="", fileobj=destination, mode="wb", mtime=0) as compressed:
            with tarfile.open(fileobj=compressed, mode="w") as archive:
                for name in DIRECTORIES:
                    for path in sorted((directory / name).rglob("*")):
                        if path.is_symlink():
                            raise ValueError(f"Evidence must not contain symbolic links: {path}")
                        if not path.is_file():
                            continue
                        info = archive.gettarinfo(str(path), arcname=path.relative_to(directory).as_posix())
                        info.uid = info.gid = info.mtime = 0
                        info.uname = info.gname = ""
                        with path.open("rb") as source:
                            archive.addfile(info, source)
                        count += 1
    print(f"Packaged {count} raw files in {target} ({target.stat().st_size:,} bytes)")


def verify_member(archive, member, record):
    if member.size != record["bytes"]:
        raise ValueError(f"Evidence size mismatch: {member.name}")
    digest = hashlib.sha256()
    source = archive.extractfile(member)
    if source is None:
        raise ValueError(f"Unreadable evidence member: {member.name}")
    with source:
        while block := source.read(65536):
            digest.update(block)
    if digest.hexdigest() != record["sha256"]:
        raise ValueError(f"Evidence hash mismatch: {member.name}")


def verify(directory):
    records = json.loads((directory / "manifest.json").read_text())
    expected = {
        record["path"]: record for record in records if record["path"].split("/", 1)[0] in DIRECTORIES
    }
    seen = set()
    with tarfile.open(directory / ARCHIVE, mode="r:gz") as archive:
        for member in archive:
            if member.name not in expected or member.name in seen or not member.isfile():
                raise ValueError(f"Unexpected evidence member: {member.name}")
            verify_member(archive, member, expected[member.name])
            seen.add(member.name)
    if seen != set(expected):
        raise ValueError(f"Missing evidence members: {sorted(set(expected) - seen)}")
    print(f"Verified all {len(seen)} archived files against their original SHA-256 hashes")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--verify", action="store_true")
    options = parser.parse_args()
    if options.verify:
        verify(options.directory)
    else:
        package(options.directory)
