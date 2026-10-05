CI could publish partial or stale benchmark results, suppress cancelled/failed outcomes, and run an unsupported C++ build. This PR validates complete current-run measurements, makes failures explicit, and applies the applicable practices from all three templates while preserving the Rust/C# harnesses and both C# SQLite providers.

## Changes

- Remove the C++ workflow with no source/CMake project; retain its placeholder and document prerequisites.
- Freeze the validated benchmark merge tree, bound workloads, require every artifact/variant/operation, and separate reporting from publication. Serialize main writers, regenerate after concurrent writes, reject changed measured source, and retry only actual branch races.
- Gate Dependabot automation on its own patch/minor updates using official Node 24 metadata. Pin runners/actions, permissions, time budgets and current-base merge checks.
- Enforce formatting, strict builds, workflow/security scans, Python type/security checks, documentation/links, secret scans, complete transitive dependency audits and truthful terminal status across maintained code and experiments.
- Preserve the investigation, every template file inventory, raw logs (including recovered terminal-escape downloads), reproductions, analyzer findings and upstream reports under `dev/log/issues/110/pulls/111`.

## Reproduction and validation

The original reporter accepted empty/invalid measurements and valid baselines with missing Doublets variants. Regression logs demonstrate these failures before the fix. Git integration tests reproduce concurrent publication and changed-source rejection; artifact tests reject missing/stale/extra files and inconsistent merge trees. YAML policy tests preserve privileged-event and cancellation behavior.

Local validation passes: 55 Python tests, 9 Rust tests, 37 C# tests, provider-report checks, and eight real benchmark cells at size 1,000. Formatting, strict builds, Ruff/mypy/Bandit, actionlint/ShellCheck/zizmor, Markdown/links, secretlint, and Python/all-Rust/all-NuGet dependency audits pass. All hosted implementation workflows pass, including Rust/C# on Ubuntu, macOS and Windows, plus Codacy. CodeFactor's PR page reports no issues; its last posted GitHub check passes. Archived run/check metadata identifies the exact implementation SHA; the final PR revision is checked again at its own head.

## Evidence and upstream reports

[Timeline, causes, requirements and best-practice decisions](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/blob/issue-110-e1f96d946132/dev/log/issues/110/pulls/111/ANALYSIS.md), [solution choices and plans](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/blob/issue-110-e1f96d946132/dev/log/issues/110/pulls/111/SOLUTION_PLANS.md), and [complete evidence archive](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/tree/issue-110-e1f96d946132/dev/log/issues/110/pulls/111).

Reported [C# template #65](https://github.com/link-foundation/csharp-ai-driven-development-pipeline-template/issues/65), [doublets #68](https://github.com/linksplatform/doublets-rs/issues/68), and [Bandit #1477](https://github.com/PyCQA/bandit/issues/1477); added a current-version reproduction to [Bandit #1041](https://github.com/PyCQA/bandit/issues/1041#issuecomment-5994134386) and linked the existing [Rust template #180](https://github.com/link-foundation/rust-ai-driven-development-pipeline-template/issues/180). Historical toolchain failures were already fixed; service-owned CA warnings and practical benchmark budgets are documented with their evidence. The old human-author warning persists in `pull_request_target` logs until this workflow change reaches the default branch.

Closes #110.
