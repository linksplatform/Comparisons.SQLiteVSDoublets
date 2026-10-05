"""Fail CI on direct or transitive NuGet vulnerabilities (dotnet itself exits zero)."""

import json
import subprocess  # nosec B404 # all calls use a resolved tool and argument lists without a shell.
from pathlib import Path

from ci_process import executable


def vulnerabilities(report):
    if report.get("errors") or not report.get("projects"):
        raise ValueError("NuGet audit did not produce a complete project report")
    found = []
    for project in report["projects"]:
        for framework in project.get("frameworks", []):
            for group in ("topLevelPackages", "transitivePackages"):
                for package in framework.get(group, []):
                    for advisory in package.get("vulnerabilities", []):
                        found.append(
                            (project["path"], package["id"], advisory["severity"], advisory["advisoryurl"])
                        )
    return found


def main():
    root = Path(__file__).resolve().parents[1]
    projects = sorted(
        path
        for directory in ("csharp", "experiments")
        for path in (root / directory).rglob("*.csproj")
        if not any(part in ("bin", "obj") for part in path.parts)
    )
    if not projects:
        raise SystemExit("No NuGet projects found")
    findings = []
    for project in projects:
        subprocess.run(  # nosec B603 # tracked project paths are separate arguments to the fixed restore command.
            [executable("dotnet"), "restore", str(project), "-warnaserror"], check=True
        )
        result = subprocess.run(  # nosec B603 # fixed audit options and a tracked project path; no shell.
            [
                executable("dotnet"),
                "list",
                str(project),
                "package",
                "--vulnerable",
                "--include-transitive",
                "--format",
                "json",
            ],
            capture_output=True,
            text=True,
            check=True,
        )
        print(result.stdout)
        findings.extend(vulnerabilities(json.loads(result.stdout)))
    for path, package, severity, url in findings:
        print(f"{path}: {package}: {severity}: {url}")
    if findings:
        raise SystemExit("NuGet dependency audit failed")


if __name__ == "__main__":
    main()
