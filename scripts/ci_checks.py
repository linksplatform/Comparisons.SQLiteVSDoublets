"""Check repository CI invariants and local documentation links."""

import re
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[1]
WRITER_GROUP = "main-writer-${{ github.repository }}-main"
DOCUMENT_SECTIONS = {
    "README.md": (
        "## Benchmarks",
        "## Original comparison",
        "<!--BENCHMARK_RESULTS_START-->",
        "<!--BENCHMARK_RESULTS_END-->",
    ),
    "README.ru.md": (
        "## Тесты производительности",
        "## Исходное сравнение",
        "<!--BENCHMARK_RESULTS_START-->",
        "<!--BENCHMARK_RESULTS_END-->",
    ),
    "CONTRIBUTING.md": ("## Local checks", "## Dependencies and diagnostics"),
    "cpp/README.md": ("# C++ placeholder",),
}


def workflow_errors(workflow):
    errors = []
    if workflow.get("permissions") != {"contents": "read"}:
        errors.append("workflow must default to contents: read")
    for name, job in workflow.get("jobs", {}).items():
        if not job.get("timeout-minutes"):
            errors.append(f"{name}: missing job timeout")
        matrix = job.get("strategy", {}).get("matrix", {})
        runners = [job.get("runs-on", "")] + (matrix.get("os", []) if isinstance(matrix, dict) else [])
        if any("latest" in runner for runner in runners):
            errors.append(f"{name}: mutable runner label")
        if job.get("continue-on-error"):
            errors.append(f"{name}: continue-on-error hides failed checks")
        writes = "write" in job.get("permissions", {}).values()
        if writes:
            concurrency = job.get("concurrency", {})
            if concurrency.get("group") != WRITER_GROUP or concurrency.get("cancel-in-progress") != "false":
                errors.append(f"{name}: writer must serialize without cancellation")
            if "concurrency" in workflow:
                errors.append(f"{name}: workflow concurrency can cancel a writer")
        if "pull_request_target" in workflow.get("on", {}):
            if any("checkout@" in step.get("uses", "") for step in job.get("steps", [])):
                errors.append(f"{name}: privileged pull_request_target must not check out code")
    return errors


def local_link_errors(document, root=ROOT):
    errors = []
    for link in re.findall(r"!?\[[^\]]*\]\(([^\s)]+)(?:\s+[^)]*)?\)", document.read_text(encoding="utf-8")):
        if re.match(r"\w+://|mailto:|#", link):
            continue
        target = link.split("#", 1)[0].split("?", 1)[0]
        if target and not (document.parent / target).exists():
            errors.append(f"{document.relative_to(root)}: missing local link {target}")
    return errors


def main():
    errors = []
    for path in sorted((ROOT / ".github/workflows").glob("*.yml")):
        workflow = yaml.load(path.read_text(encoding="utf-8"), Loader=yaml.BaseLoader)
        errors += [f"{path.name}: {error}" for error in workflow_errors(workflow)]
    if (ROOT / ".github/workflows/cpp.yml").exists() and not (ROOT / "cpp/CMakeLists.txt").exists():
        errors.append("C++ build workflow has no CMake project")
    for directory in ("scripts", "rust/src", "rust/tests", "csharp", "experiments"):
        for path in (ROOT / directory).rglob("*"):
            if path.suffix not in (".py", ".rs", ".cs", ".sh") or any(
                part in ("bin", "obj", "target") for part in path.parts
            ):
                continue
            lines = len(path.read_text(encoding="utf-8").splitlines())
            if lines > 1500:
                errors.append(f"{path.relative_to(ROOT)}: {lines} lines exceeds 1500")
    for name, sections in DOCUMENT_SECTIONS.items():
        path = ROOT / name
        if not path.is_file():
            errors.append(f"Missing required document: {name}")
            continue
        errors += local_link_errors(path)
        content = path.read_text(encoding="utf-8")
        errors += [
            f"{name}: missing required section {section}" for section in sections if section not in content
        ]
        if len(path.read_text(encoding="utf-8").splitlines()) > 2500:
            errors.append(f"{name}: exceeds 2500 lines")
    if errors:
        raise SystemExit("\n".join(errors))
    print("Workflow policies, source line limits, and local documentation links passed")


if __name__ == "__main__":
    main()
