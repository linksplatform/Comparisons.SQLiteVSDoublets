"""Resolve trusted CI tools to absolute paths before invoking argument lists."""

import shutil
from pathlib import Path


def executable(name: str) -> str:
    path = shutil.which(name)
    if path is None:
        raise FileNotFoundError(f"Required CI executable not found: {name}")
    return str(Path(path).absolute())
