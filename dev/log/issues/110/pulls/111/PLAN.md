# Issue 110 / PR 111 execution plan

Evidence is collected under this directory. Commands with substantial output write
logs here; experiments and reusable regression checks live under `experiments/ci`.

- [x] Read the complete issue, all three PR comment types, repository instructions,
  contributing guidance, recent related PRs, and the current branch/default branch.
- [x] Preserve issue/PR metadata, complete repository and template file trees,
  recent CI runs with timestamps and SHAs, job details, and downloadable logs.
- [x] Read the referenced CI/CD best practices and compare every workflow and
  CI-related script/configuration against the Rust, C#, and Python templates.
- [x] Reconstruct the event timeline and enumerate every issue requirement.
- [x] Read logs in chunks of at most 1,500 lines, identify failures, warnings,
  false positives/negatives, and correlate them with the exact source revision.
- [x] Research primary documentation and existing components; document options,
  root causes, reproductions, and a concrete solution for each requirement.
- [x] Add minimal failing regression checks before changing affected behavior.
- [x] Fix all affected code paths; retain default-off verbose diagnostics when
  evidence is insufficient. Bound resource-intensive probes.
- [x] Report reproduced upstream/template defects with examples, workarounds,
  and suggested code changes, when applicable and possible.
- [x] Run local CI checks and all applicable test suites, preserving their output.
- [x] Commit useful atomic changes and push only `issue-110-e1f96d946132`.
- [x] Merge the current default branch if needed; review the complete PR diff for
  unintended changes and verify a clean working tree.
- [x] Replace the placeholder PR title/body with the final solution, reproduction,
  validation, evidence links, and any remaining limits; mark PR 111 ready.
- [x] Verify fresh CI runs match the latest implementation commit; download and
  analyze every unsuccessful run, fix causes, and wait for checks to finish.

All implementation checks pass on `0dba0f522015bd5200c885d4267f96f4b510cef6`;
PR 111 is ready for review. The final evidence-only commit is checked again
at its own pushed head before completion. Archived run/check metadata identifies
the implementation revision explicitly.

## Scope assumptions

The requested template comparison concerns CI/CD behavior and supporting files;
language-specific application scaffolding is recorded in the complete inventories
and classified for applicability rather than copied into this benchmark repository.
Benchmark workloads must remain finite. CI fixes must preserve all languages and
the existing benchmark comparisons.
