<!-- markdownlint-disable MD043 -->
# Contributing

Use a pull request and keep changes focused on the benchmark comparison. CI checks
the current merge tree, uses fixed runner versions, and checks formatting before
tests. Documentation and Python changes have their own validation workflow.

## Local checks

Use the SDK from `csharp/global.json`, stable Rust, Python 3.13 or later, and Node.js
for Markdown and secret checks. Create a virtual environment before installing
the Python tools:

```sh
python -m venv .venv
. .venv/bin/activate
python -m pip install -r scripts/requirements.txt -r scripts/requirements-ci.txt
ruff check scripts experiments
ruff format --check scripts experiments
mypy scripts
bandit -r scripts experiments -q
python scripts/ci_checks.py
python -m unittest discover -s scripts -v
python -m unittest discover -s experiments/ci -v
python experiments/ci/package_evidence.py dev/log/issues/110/pulls/111 --verify
cargo fmt --manifest-path rust/Cargo.toml --check
cargo clippy --manifest-path rust/Cargo.toml --locked --release \
  --all-targets -- -D warnings
cargo test --manifest-path rust/Cargo.toml --locked --release
dotnet format whitespace csharp/SQLiteVSDoublets.slnx --verify-no-changes
dotnet build csharp/SQLiteVSDoublets.slnx -c Release -warnaserror
(cd csharp && dotnet test --project SQLiteVSDoublets.Tests -c Release \
  --no-build -- --timeout 10m)
npx --yes markdownlint-cli@0.49.1 README.md README.ru.md CONTRIBUTING.md
```

Workflow validation also runs actionlint with ShellCheck and zizmor. The pinned
actionlint container command is in `.github/workflows/quality.yml`. Run both
zizmor commands from that workflow locally when changing automation. Optional
pre-commit hooks are configured in `.pre-commit-config.yaml`.

## Dependencies and diagnostics

Language workflows also verify formatting in every Rust and C# experiment;
the Python formatting commands cover all experiment scripts.

The dependency workflow audits Python, every committed Rust lockfile (including
experiments), and direct and transitive NuGet packages weekly and on relevant
changes. Run `pip-audit -r scripts/requirements.txt -r scripts/requirements-ci.txt`,
`cargo audit --file <Cargo.lock> --deny warnings`, and
`python scripts/audit_dotnet.py` locally with the workflow tool versions.

Benchmark inputs are bounded to ten million links and one million objects,
with at most 32 requested sizes (256 jobs). The
workflow's `verbose` input enables full Rust backtraces and artifact/publication
diagnostics; the default is off. Missing, stale, duplicate, or invalid reports
fail validation. Publication retries only concurrent branch updates and rebuilds
reports from the newly fetched branch on every retry.

This repository contains benchmark executables, without a package release or
container deployment pipeline. Package changesets, registry credentials, and
container build steps should be added with an actual distributable component.

Immutable investigation evidence is preserved in
[the issue 110 investigation](dev/log/issues/110/pulls/111/ANALYSIS.md).
Downloaded upstream logs and snapshots are excluded from source formatting and
secret scanning so historical third-party output is preserved unchanged.
