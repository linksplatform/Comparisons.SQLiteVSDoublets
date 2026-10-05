# Issue 110 investigation and implementation

Repository: linksplatform/Comparisons.SQLiteVSDoublets. Implementation and review:
[PR 111](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/pull/111).
All times below are UTC. Initial default branch: `d2ee41e17b1bf6a8f3bea6c96827c41311cedb19`.
Prepared branch: `issue-110-e1f96d946132`, initial commit `3b19ef1`.

## Evidence and collection scope

`github/` preserves the complete issue, conversation comments, inline review
comments, reviews, PR details/diff, recent related PRs, repository tree, commit
history, and run timestamps/SHAs. There were no issue or PR comments/reviews in
the initial collection. `ci-logs/` preserves run metadata, all job pages, and
every downloadable completed-run log since October 3, including all five runs
named in the issue. Unfinished runs have metadata/job records; GitHub does not
provide a completed-run log until the run ends. Refresh with
`python experiments/ci/collect_evidence.py`.

The initial completed-log corpus contains 67 run logs. Completed job logs
from unfinished runs are also preserved via the job-log API. Logs were inspected in
chunks of at most 1,500 lines. `ci-logs/diagnostics.json` records diagnostic
matches and original line numbers; `ci-logs/diagnostics.md` lists distinct
messages. `validation/` contains failing reproductions, complete local test
output, audits, tool versions, and eight real small benchmark reports.
`research/` preserves the referenced best-practice document, action SHAs,
upstream source/issue evidence, and submitted reports. GitHub service tokens in logs are masked; collector commands never print
authentication tokens.

Three complete template snapshots, exact commit SHAs, and repository metadata
are under `templates/`. The full-tree comparison TSVs classify **every tracked
file**: Rust 162, C# 73, Python 101. Application examples are classified rather
than replacing this repository's working benchmark implementations. The
`*-ci-review.txt` files record workflow/script/configuration details used in
the semantic comparison below; snapshot archives preserve the complete source.

## Every requirement and its disposition

| Requirement | Evidence, solution, and verification |
| --- | --- |
| Read the issue and all comments; investigate the named main runs | Complete API snapshots in `github/`, named-run logs/job metadata in `ci-logs/`; correlate SHAs rather than treating old failures as current regressions. |
| Find all CI false positives, false negatives, warnings, and errors | Diagnostic index and causes below; reproduce missing/invalid/incomplete reports, classify write rejections, reject unexpected cancellations, and restrict Dependabot automation to its own PRs. |
| Compare the complete Rust, C#, and Python template trees and CI scripts | Three immutable archives, full file inventories with applicability classifications, semantic comparison, and all 16 best-practice decisions below. |
| Reuse all applicable practices in this single PR | Fixed runners, pinned actions, read permissions, safe writer concurrency, fresh merge checks, formatting, workflow/security lint, docs checks, secret scans, complete dependency audits, bounded inputs, terminal status gates, and local contributor commands. |
| Report the same issue in templates | Rust runner drift already reported as template #180; C# floating matrix reported as template #65 with reproduction, workaround, and a suggested regression check. Python template has fixed runner labels; no duplicate report created. |
| Download logs and compile issue data in the requested folder | This directory contains raw metadata, logs, snapshots, research, reproductions, and analysis; reusable collector can refresh it. |
| Deep analysis, timeline, root cause, alternatives, solution plans | Sections below distinguish observed failures, inherited fixes, reproduced false successes, and environmental/tool limitations. |
| Search online for facts and reusable components | Primary sources and selected components below; exact versions/SHAs are preserved and exercised locally. |
| Add default-off diagnostics when needed | `workflow_dispatch.verbose` enables full Rust backtraces and artifact/publication diagnostics; Python `--verbose` defaults off. Validation always identifies the offending artifact, expected SHA/tree, or actual Git rejection. |
| Report related project defects with reproductions and fixes | Reproduced the `doublets` memory contract defect with finite inputs and a 512 MiB limit; report URL/body preserved under `research/`. Existing `Whole` workaround remains in all Rust storage paths. |
| Apply each fix across the whole codebase | All workflows have runner/time/permission policies; NuGet audits enumerate main and experimental projects, Cargo audits enumerate every lockfile, Dependabot covers both sets, Python/report checks cover both languages/categories/address widths. |
| Regression tests before fixes; local checks and fresh CI | Baseline failure logs in `validation/`, meaningful artifact/schema/race/status/audit tests, all language tests, real eight-cell smoke run, workflow lint/security scans, and fresh run metadata keyed to implementation SHA. |
| Preserve history; update existing PR and finish ready | Commit atomic changes on the prepared branch, synchronize main, update title/body, inspect the final diff, check all latest-commit statuses, and mark PR 111 ready. |

## Reconstructed sequence

| UTC event | Consequence and source |
| --- | --- |
| Oct 4, 17:05–17:40 | PR 108 rewrites the Rust/C# harnesses to stable Rust edition 2024 and .NET 10. Old workflows still refer to a 2022 nightly and a removed `.sln`. |
| Oct 4, 17:53:53 | Rust run 37222262227 and C# run 37222262316 fail at `4f8d0c5`; old-toolchain parsing and MSB1009 are actual historical failures. |
| Oct 4, 18:21:28 | Commit `4d83c843` updates the language workflows and benchmark pipeline. Later passing runs demonstrate those historical failures are already fixed. |
| Oct 4, 18:32–18:52 | Full benchmarks at `b8c842ed` expose the Rust allocation contract mismatch; run 37224809082 has four Rust exit-101 failures and cancellation of other jobs. |
| Oct 4, 18:52:35 | `e965c93d` adds the `Whole` adapter, returning the complete allocation after growth. This PR preserves and verifies that fix. |
| Oct 4, 19:31 → Oct 5, 01:55 | Run 37228604862 tries 100 million links and reaches the 355-minute job budget. This is workload exhaustion, not evidence that a larger timeout fixes correctness. |
| Oct 5, 00:27–01:58 | PR 108 updates obsolete action runtimes, Markdown formatting, and documents 10-million-link/1-million-object practical limits. |
| Oct 5, 10:00:35 | PR 108 merges as `f61d583`; C# 37293771766 and Rust 37293771933 pass. The main benchmark run starts on this source. |
| Oct 5, 10:00–10:04 | Legacy C++ runs 37293772008 and 37294124568 fail on LLVM 13 packages for Noble. PR 109 bumps setup-clang v1 to v2 and merges as `d2ee41e`, but the compiler/repository mismatch remains. |
| Oct 5, 10:24:35–10:25 | Issue 110 is opened; the prepared PR 111 is created on `3b19ef1`. The issue's in-progress benchmark entry is a status snapshot, not a reported success or failure. |
| Oct 5, investigation | Archive logs/templates, reproduce false report success and upstream growth, implement regression checks and workflow changes, and run local validation before pushing. |
| Oct 5, 11:24:41 | First source revision `73c7cff` runs hosted CI: all three OS matrices, eight benchmark cells/report, dependency audit and workflow security pass. Quality fails on a documentation link whose evidence target had not yet been committed (`quality-37302748264.log:716`). |
| Oct 5, 11:25–11:26 | CodeFactor reports validator complexity and two B506 warnings. Codacy's public API supplies all 31 findings despite the GitHub check containing no annotations; browser snapshots and API JSON are preserved. |
| Oct 5, 11:49:43 | Revision `ffd5966` includes the archive. Rust, C#, dependency audits and benchmarks pass. Quality run 37305441174 fails on two B603 warnings; Codacy reports the same two locations and CodeFactor passes. |
| Oct 5, 11:58:24 | Revision `7d7f9bc` adds narrow explanations for the two fixed Git queries. Python 3.13.15 local checks pass, including all 55 tests; the fresh hosted quality, CodeFactor and Codacy checks pass. |
| Oct 5, 12:08:22 | Revision `0dba0f5` separates nested Git-output parsing after successful checks exposed two spurious unused-annotation warnings. A three-case Bandit probe reproduces the warning and verifies the workaround; confirmation and a code-fix suggestion are posted on existing upstream issue 1041. |
| Oct 5, 12:15–12:19 | Evidence revision `4311b79` passes all five maintained workflows and CodeFactor. Main benchmark run 37297329698 completes and publishes `4528bc4`, including both C# SQLite providers; the new default branch is merged without altering its results. Codacy temporarily posts ACTION_REQUIRED while its API reports analysis in progress and zero new issues. |
| Oct 5, final collection | An actual job-log download reproduces GitHub CLI's terminal-escape refusal: exit 1 and zero bytes. Adding its documented `--allow-escape-sequences` option returns exit 0 and 23,091 bytes. The collector uses the option for all raw job downloads and retries earlier affected files while retaining the original errors. |

## Root causes and solution choices

### 1. Phantom C++ build/release pipeline

Run `37294124568.log:349` shows the apt 404. Lines 366–377 identify
`llvm-toolchain-noble-13` without a Release file and missing clang-tidy,
clang-format, LLVM, and lld 13 packages. The earlier run has the same mismatch
at lines 323 and 370–372. Bumping the setup action cannot make that repository
exist. [LLVM's package repository](https://apt.llvm.org/) describes the
maintained distribution/version combinations.

The full repository tree contains only `cpp/conanfile.txt`, with no requirements,
no C++ source, no CMake project, tests, package metadata, or release artifact.
The inherited workflow would next invoke nonexistent CMake targets, combine
flags into one quoted argument, and download moving external release scripts.
An action-only repair would merely advance to another failure.

Options: reconstruct a new C++ implementation/build/release system (a separate
feature with no issue requirement), or remove its unsupported automation and
record prerequisites. Implemented the latter: remove `cpp.yml`, retain the
placeholder manifest, add `cpp/README.md`, and add a policy regression guard.
No working implementation or benchmark comparison is removed.

### 2. Partial, absent, stale, and invalid benchmark reports could pass

The former report job permitted failed benchmark dependencies, downloaded into
the directory containing checked-in old results, and uploaded missing files as
a warning. `benchmark_report.load(empty)` returned an empty mapping and the
renderer could publish a successful missing-results report. Valid SQLite
baselines also concealed omitted Doublets variants.

Reproduction: `validation/report-reproduction.log` records five failing
schema/empty-input assertions against the original script;
`validation/variant-reproduction.log` records a further false success with all
Doublets results removed. New artifact tests check missing/extra files, stale
source SHA, wrong table identity, different merge trees, and missing variants.

Implemented: emit a single bounded matrix plan, require all benchmark jobs to
succeed, download to a fresh temporary directory, fail missing uploads, and
verify every expected file, table, variant, source commit, and measured tree.
Schema validation rejects duplicate variants, missing operations/baselines,
empty samples, nonfinite/nonpositive timings, and invalid metadata. Rendering
an empty directory fails. Both language reports go through the same validator.
Existing comparison statistics and chart/report hierarchy are preserved.

### 3. Cancellation and publication races hid actual outcomes

Workflow-level cancellation can interrupt a writer even if its job says not
to cancel. Separate workflows can race on main when groups include workflow
names. Blind push retries misclassify protection/authentication failures;
rebasing generated reports can also carry stale documents onto new source.
[GitHub concurrency documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax)
confirms groups span workflows in a repository and do not promise FIFO order.

Implemented: readers cancel at their own scope; main writers use one shared
job-level group with cancellation disabled. Publication is a separate privileged
job. Every attempt fetches main, refuses results if measured sources changed,
creates a fresh temporary worktree, copies validated measurements, and rebuilds
both READMEs/charts. Only non-fast-forward/fetch-first races retry (three finite
attempts); GH006/GH013, credentials, network, and other rejections fail with
their actual output. No forced push or history rewrite is used.

`test_publish_benchmarks.py` creates a real bare origin and injects a concurrent
documentation commit immediately before the first push. It verifies two
attempts, regenerated sections, and preservation of both commits. A separate
test proves new benchmark source prevents publication. Terminal status tests
reject timeout/cancelled writers; only readers with proven supersession and an
enabled cancellation policy may be treated as expected churn.

### 4. Auto-merge warnings were self-inflicted

`37295743786.log:191` and earlier runs warn about human PR authors; the workflow
ran Dependabot-specific parsing on every PR. `37294036931.log:198` warns that
the action cannot parse a v1→v2 action title. Human PRs and major updates should
be ordinary skips, not warnings or accidental merges.

Implemented: gate the job on the Dependabot PR author, obtain update types from
the official [fetch-metadata action](https://github.com/dependabot/fetch-metadata),
and enable GitHub auto-merge only for patch/minor updates. Pin its Node 24 v3
revision. The privileged metadata workflow checks out and executes no PR code.
The existing Dependabot token fallback is retained. Its writer group matches
benchmark publication. No title-regex parser remains.

Fresh PR logs still run the old auto-merge action from the default branch:
`37306375540.log:189` records its human-author warning. `pull_request_target`
intentionally loads the base-branch workflow, so a PR cannot replace privileged
automation before its changes merge. The new metadata-only job is checked by
workflow/security policy checks; human-author skips take effect after merging.

### 5. Missing local/CI policy and dependency coverage

Before this PR there was no actionlint/ShellCheck/zizmor gate, Python formatting
or type check, C# formatting gate, required document/link check, secret scan, or
scheduled complete dependency audit. Floating runner labels and actions allowed
unreviewed changes; old logs also contain already-resolved Node 20 deprecations.

Implemented: workflow security/policy tests, pinned runners and action SHAs,
read-only defaults, explicit writer permissions, meaningful time budgets,
Ruff/mypy, Rust fmt/strict Clippy, C# whitespace formatting and warning-as-error
builds, Markdown/local-link and source/document line limits, secretlint,
pre-commit configuration, and `CONTRIBUTING.md` with matching commands.

Audits cover every Cargo.lock and every main/experimental C# project, including
transitive dependencies, plus runtime/CI Python dependencies. Cargo warnings
are denied; NuGet JSON findings explicitly fail because listing advisories is
not itself a failing process exit. Tests cover direct/transitive audit handling
and incomplete audit output. Dependabot covers these locations with a seven-day
cooldown. Current local audits found no vulnerabilities.

Upstream reports: [C# template #65](https://github.com/link-foundation/csharp-ai-driven-development-pipeline-template/issues/65),
[Rust template #180 (existing)](https://github.com/link-foundation/rust-ai-driven-development-pipeline-template/issues/180),
and [doublets #68](https://github.com/linksplatform/doublets-rs/issues/68).

### 6. Historical library growth panic and oversized workloads

`37224809082.log:3229` records an index of 1,040,384 against that exact slice
length; line 5777 shows `NotExists(1040384)`. `doublets::resize_mem` treats the
slice returned by `platform-mem::grow_filled` as the whole allocation; that
backend returns only the added portion. Current upstream main retains this
call, so the contract defect remains reportable despite the repository's fix.

The existing finite `unit_store_growth` probe, capped to 512 MiB virtual memory,
reproduces a panic at link 1,040,384 (`validation/unit-store-bare.log:17–18`).
The existing adapter succeeds through 2,097,153 links and multiple growths
(`unit-store-whole.log`). An upstream report includes the runnable experiment,
workaround, and suggested `grow_filled(...)?; Ok(allocated_mut())` correction.
No second workaround is introduced here. Matrix inputs now enforce the
documented maximum sizes and at most 256 jobs before starting costly work.

### 7. External and historical warnings

The diagnostic index distinguishes current failures from cancellation of
superseded runs and already-fixed obsolete actions/toolchains. C# `.slnx` and
stable Rust are verified locally; reverting to old scaffolding would regress
them. Dependabot's service-owned `rehash` warning in `37293778052.log:63` refers
to its bundled CA certificate file containing multiple certificates. That run
completed successfully; it is not a project build or dependency finding. Its
container initialization cannot be edited in this repository. Preserve the
evidence rather than changing trust stores or suppressing unrelated diagnostics.

## Template comparison and all best-practice decisions

The reviewed [best-practice document](https://github.com/link-assistant/hive-mind/blob/main/docs/CI-CD-BEST-PRACTICES.md)
is preserved in `research/CI-CD-BEST-PRACTICES.md`. These are concrete decisions,
not a claim that unrelated application/release scaffolding was copied.

| Principle | Applied behavior or justified applicability |
| --- | --- |
| 1. Relevant file changes | Workflow path sets cover code, scripts, manifests, own workflows, and the shared merge action; manual runs remain possible. Docs/Python have dedicated validation. Fixed Ubuntu 24.04/macOS 15/Windows 2025 environments prevent runner drift. |
| 2. File size limits | Check maintained source across languages and experiments at 1,500 lines and required documents at 2,500; exclude immutable downloaded evidence/build outputs. |
| 3. Automated formatting | Rust fmt, C# whitespace verification, Python Ruff formatting, Markdown lint, and optional local hooks. |
| 4. Static analysis/lints | Clippy `-D warnings`, C# `-warnaserror`, Ruff/mypy, workflow/security policy checks; no continue-on-error. |
| 5. Fast fail ordering | Formatting precedes language build/tests; script tests/input validation precede the matrix; workflow lint precedes Python tests. |
| 6. Changesets | No distributed package/release workflow exists; application and experiment package versions are local. Contributor guidance records requirements for future publishing. |
| 7. Actual merge validation | Shared composite fetches/merges current PR base. The benchmark plan freezes that validated base commit so every matrix job reconstructs the same tree even if main advances during the run. |
| 8. Pre-commit | Configured Ruff, rustfmt, C# format hooks; local commands match CI. |
| 9. Release automation | No NuGet/PyPI/crates release exists. Remove the fictitious C++/NuGet path instead of adding registry secrets or a version bump without a distributable artifact. |
| 10. Concurrency | Readers cancel; main writers share one job-level non-cancelling group. Regenerate on proven branch races; fail GH006/GH013 without retry. Fresh-worktree retries preserve history. Superseded pending reports are intentionally replaceable; FIFO/release-queue guarantees are not required. |
| 11. Secret detection | Scan maintained tracked source with versioned secretlint rules; exclude immutable third-party evidence and generated artifacts. |
| 12. Documentation | Lint both READMEs and contributor/C++ prerequisites; enforce required sections and local links. Docs changes trigger quality checks. |
| 13. Native image architecture | No published runtime image; actionlint uses a native-compatible digest-pinned validation container. No emulated/cross-architecture release matrix applies. |
| 14. Workflow lint/security | actionlint/ShellCheck plus regular/pedantic zizmor, SHA pinning policy. Narrow self-repository exceptions document unsupported new self syntax in actionlint 1.7.12. Privileged metadata exception documents its no-checkout boundary. |
| 15. Complete dependency audit | Weekly and relevant-change audits of every Rust lockfile, every C# project including transitive packages, and Python runtime/CI tools. Cargo warning and NuGet JSON false-success semantics explicitly fail. |
| 16. Publisher preflight | Registry credential/write/OIDC probes are inapplicable: no package or image publishing exists. Benchmark publication requires complete validated artifacts, commit/tree consistency, and unchanged measured sources before writing, and preserves actual credential/ruleset errors. No speculative login or no-op push is presented as proof of write access. |


Rust template's security/workflow scripts supplied audit-warning and terminal
status patterns. C# template supplied fixed Ubuntu, formatting, local layout,
and strict-build guidance, but its macOS/Windows matrix still floats. Python
template supplied Ruff/mypy, transitive audits, policy-aware cancellation and
fresh writer regeneration patterns. Its package versioning, PyPI OIDC, Docker
publishing, docs-site deployment, and application scaffolding are recorded as
inapplicable because this repository has no such delivery targets. CodeQL or
network-wide external link crawling are optional additional services; the
required workflow security audit, dependency graphs, Markdown and local links
are validated without introducing unavailable service permissions.

## Online facts and existing components

- [LLVM apt repositories](https://apt.llvm.org/): determine which Ubuntu/LLVM
  combinations actually exist; do not infer availability from an action version.
- [GitHub workflow/concurrency syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax):
  shared groups, cancellation, conditions, runner/path behavior, and pending-run
  replacement limits. Publication needs fresh preflight even with concurrency.
- [GitHub same-repository actions announcement](https://github.blog/changelog/2026-07-30-reference-same-repository-actions-with-self-repository-syntax/):
  newer self syntax exists; `validation/actionlint-after.log` from the draft
  showed current actionlint rejects it, so conventional checkout/local actions
  are used with a specific documented exception.
- [actionlint](https://github.com/rhysd/actionlint) and
  [zizmor](https://docs.zizmor.sh/): selected existing workflow/ShellCheck and
  security analyzers; versions/digests pinned and checked locally.
- [Ruff](https://docs.astral.sh/ruff/), [mypy](https://mypy.readthedocs.io/),
  [secretlint](https://github.com/secretlint/secretlint): selected maintained
  format/lint/type/secret components instead of inventing replacements.
- [cargo-audit](https://github.com/rustsec/rustsec/tree/main/cargo-audit),
  [pip-audit](https://github.com/pypa/pip-audit), and
  [NuGet auditing](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages):
  reuse advisory databases and include transitive dependencies; enforce findings
  in process outcomes and avoid a direct-only audit false negative.
- [dotnet package list JSON](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list)
  and [dotnet test](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test):
  use machine-readable graph reports and the configured Microsoft.Testing.Platform
  runner. Run tests from `csharp/` so its `global.json` selects that runner.
- [Dependabot metadata](https://github.com/dependabot/fetch-metadata) and
  [options](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference):
  structured update types and explicit cooldowns avoid custom title parsing.
- [Conan 2 profiles](https://docs.conan.io/2/reference/config_files/profiles.html):
  confirms legacy C++ setup would need more than a Clang action bump if a real
  C++ implementation is introduced later.

## Validation and remaining practical limits

The 55-test local Python suite includes schema/artifact, actual Git race/source-change,
workflow policy, audit JSON, terminal status, report statistics, and chart tests.
Rust formatting, locked strict Clippy, locked Release tests, C# formatting,
warning-free Release build, and all 37 C# tests pass. The eight-cell real smoke
run covers Rust/C#, links/objects, 32/64-bit addresses at size 1,000, one measured
repetition; every complete variant set and operation validates. These are
correctness smoke measurements, not replacement published performance results.

Dependency audit logs cover four Rust lockfiles, all six C# projects, and Python
runtime/CI tools. Workflow lint/security, Python type/format checks, document
lint/links, and source secret scans are recorded under `validation/`.
Fresh hosted-run evidence is collected after pushing and must match the latest
implementation commit. Old cancelled runs remain evidence, not a claim that
old commits now pass. Main-size performance measurements still have an explicit
355-minute per-job budget; oversized inputs are rejected rather than increasing
that limit. No new feature, registry release, or default-branch merge is performed.

## Default-branch changes during the investigation

PR 100 merged System.Data.SQLite into main while this work was running. The
branch merges that history, preserves its C# provider smoke check, and requires
the additional provider variants in every current C# artifact. The original
main benchmark run 37293771868 subsequently published commit `669369c` from
`f61d583` measurements after the source had advanced to `2c43a22`. Its checked-in
C# tables still have six/ten variants rather than the current eight/twelve.
This is observed evidence of the stale-source publication flaw, in addition
to the reproducing Git tests. The new publisher refuses that source mismatch.
Main-size measurements for the new source are a separate run; old results
are preserved as historical data rather than relabelled as new measurements.

Formatting coverage was also checked across all experiment projects. One old
Rust experiment needed rustfmt; its scenarios are unchanged. Python formatting
now covers all experiment scripts, including the inherited provider checker.
All four experimental C# projects pass whitespace verification. The expanded
workflow paths and formatter loops enforce these checks on subsequent changes.

## Findings from implementation CI and external analysis

The first hosted quality failure is an archive-ordering issue, not a stale test
result: `CONTRIBUTING.md` links to this analysis, but its first pushed commit did
not yet include the evidence directory. The link check correctly failed. The
archive is included before rerunning the same check; no link validation is relaxed.

CodeFactor's three annotations and Codacy's complete 31 findings are retained
under `github/` and `research/`. Each finding was traced to its source:

- The report validator's complexity is reduced by separating metadata, variants
  and individual measurements; existing schema regression behavior remains covered.
- B506 on `yaml.BaseLoader` is a false positive. [PyYAML's loader contract](https://pyyaml.org/wiki/PyYAMLDocumentation)
  supports only strings, lists and dictionaries, while the preserved Bandit plugin
  only recognizes SafeLoader/CSafeLoader. A harmless object-tag probe reproduces
  B506 with Bandit 1.9.4. Both workflow readers now share `yaml.safe_load` with
  explicit `on` normalization. Tests prevent losing privileged-event detection
  or cancellation semantics. [Bandit #1477](https://github.com/PyCQA/bandit/issues/1477) reports the reproduction, workaround and suggested fix.
- Pylint's `isinstance` advice would accept JSON booleans if applied mechanically,
  because `bool` subclasses `int`. Updated checks use `isinstance` and explicitly
  reject booleans; the existing bounded-matrix regression still rejects `[true]`.
- B404/B603/B607 concern necessary CI subprocesses. Each invocation was reviewed:
  fixed tools/options, trusted tracked paths or CI metadata as separate arguments,
  no shell. Tools are now resolved to absolute paths with a missing-tool error.
  Remaining generic B404/B603 warnings have per-call, rule-specific explanations;
  no global subprocess/security exclusion is introduced. Local Bandit is clean.
- Codacy's MD043 expects `[None]` for the new documents: an empty required-heading
  policy is unsuitable for different document structures. Shared Markdown config
  disables that generic structure rule; the repository policy checker enforces
  actual required sections per document. Codacy's wrapper gives UI patterns
  precedence over the config file, so the two affected documents also carry
  a specific MD043 directive. Its published default disables MD043 with an
  empty headings array; this is repository/service configuration, not an upstream
  Markdown parser defect. Other Markdown rules remain enabled.
- Downloaded evidence is excluded from Codacy with its documented `exclude_paths`
  setting, matching local maintained-source scans. Source, tests, experiments,
  workflows and maintained documentation remain analyzed.

Two additional policy regressions initially fail in
`validation/yaml-policy-reproduction.log`: quoted `continue-on-error: false`
must not be treated as enabled, and a real YAML boolean cancellation policy must
allow a proven superseded reader. Both now pass. Safe-loader tests additionally
reject object tags and non-mapping workflow documents. `manifest.json` records
SHA-256/size for each collected file; `diagnostic-index.json` retains diagnostic
paths and original line numbers, generated in chunks of at most 1,500 lines.

## Final verification and review

All five maintained workflows pass on implementation revision
`0dba0f522015bd5200c885d4267f96f4b510cef6`: quality, Rust and C# on all
three operating systems, dependency audit, and all eight benchmark cells plus
the complete report. Codacy's latest-head check also passes. CodeFactor's PR
page reports no issues (`research/codefactor-final-snapshot.txt`); its last
posted GitHub check is successful on `7d7f9bc`, and it has not posted a new
GitHub check for `0dba0f5`. GitHub reports the PR merge state as CLEAN.
Main-only publication
is intentionally skipped on the pull request. `github/implementation-final-*`
preserves exact check conclusions, run timestamps/SHAs and ready-for-review
state; the final PR revision is checked again before completion.

The subsequent main result commit `4528bc4` is merged as `9505fbc`. Its 20
reports are preserved separately in `validation/main-result-snapshot-4528bc4`;
schema validation passes and the C# reports retain all 8 link / 12 object
variants. The original historical snapshot is retained. README formatting,
local links, all 55 Python tests and the warning-free security scan pass again.

Raw job logs may contain ANSI terminal controls. GitHub CLI refuses to emit
these without its explicit `--allow-escape-sequences` option, even when stdout
is redirected. `validation/job-log-download-probe.json` records the exact
before/after command outcomes, and `research/gh-api-help.txt` documents the
option. The collector now applies it and recovers previously affected jobs;
the original `.error.txt` files remain evidence alongside successful `.log`
downloads. Two cancelled historical jobs have no available log, with their
actual `log not found` responses preserved rather than claiming recovery.

The remaining B603 findings in `quality-37305441174.log:717–771` identify
`experiments/ci/run_smoke.py:19` and `scripts/check_pipeline_status.py:39`.
Both invoke resolved Git executables with fixed read-only commands and separate
arguments. Per-call B603 explanations address these findings without changing
the commands or disabling the rule elsewhere. Reproduction on Python 3.13.15
and 3.14.7 gives the same two findings: the earlier local result was stale,
not an analyzer version or recursive-scan defect. `python313-final.log` records
the passing combined lint, format, type, security, policy and 55-test checks.

The passing `37306378854.log:722–723` scan then exposed two false unused-nosec
warnings: Bandit visits both the subprocess and its chained `.strip()`/`.split()`
call, applying the same line annotation to both. The correct B603 finding is
suppressed, but the outer call emits a warning. The three-case
`experiments/ci/bandit_nosec_probe.py` verifies the original finding, the spurious
warning, and the warning-free separate-assignment workaround. The two production
queries now separate output parsing; `python313-warning-cleanup.log` passes
without scanner warnings. The existing [Bandit 1041 report](https://github.com/PyCQA/bandit/issues/1041#issuecomment-5994134386)
has the current-version reproduction, workaround, and a proposed per-file
aggregation fix, avoiding a duplicate issue.

`validation/main-result-snapshot` preserves all 20 historical checked-in
measurements at the merged default branch. `validation/hosted-smoke-results`
preserves all eight real hosted artifacts; `hosted-artifact-verification.log`
validates their exact source SHA and common tree. The measured PR merge commit
`9edb881` has parents `669369c` (main) and `7d7f9bc` (PR head), recorded in
`github/implementation-measured-merge.json`. This distinguishes GitHub's synthetic
measured merge from the PR-head SHA stored in workflow run metadata.

GitHub's complete PR-diff endpoint returns HTTP 406 above 300 changed files
because the immutable evidence archive exceeds that limit. The failure is
preserved in `github/pr-final-diff.error.txt`; review uses paginated
`github/pr-files.json` and `github/pr-final-source.diff`, generated from the
merged default branch with the evidence directory excluded. The source diff
was read in bounded chunks. The removed C++ automation had no build target;
working Rust/C# comparisons, provider checks, historical reports and existing
memory-growth workarounds remain present.
