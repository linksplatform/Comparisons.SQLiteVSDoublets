# PostgreSQL comparison investigation

Issue #33 requests Npgsql.EntityFrameworkCore.PostgreSQL. The PR review asks
that it fit the current benchmark architecture and preserve its value.

## Historical CI and merge conflicts

The branch head at the start was `e32623e`, committed on 2025-09-14.
CI run `17704198070`, created at 2025-09-14T00:48:35Z, matches that SHA and
failed on Linux, Windows and macOS. Earlier failed run `17704191247` matches
implementation commit `4d3d2c6`. Downloading both logs returns HTTP 410;
the available Linux annotation says only “Process completed with exit code 1.”
The historical build diagnostics cannot be recovered from those annotations.

Current main moved C# into `csharp/` and replaced the old BenchmarkDotNet
project with a deterministic, checksum-validating harness. The draft still
modified deleted root files, producing modify/delete conflicts. Merge commit
`a59755e` preserves history and adopts the maintained harness. Its Release
build succeeds with zero warnings and errors before adding the new provider.
The PostgreSQL comparison is implemented in that harness, rather than reviving
the obsolete root project or downgrading the current .NET 10 target.

## Reproduction before implementation

`PostgreSQLUsesTheObjectsHarness` failed because `PostgreSQL_EFCore` was absent
from `Harness.ObjectsVariants`.

Three report regressions failed before implementation:

- `baseline("PostgreSQL_EFCore")` returned `SQLite_Memory`.
- A PostgreSQL row was labeled “3× slower” against embedded SQLite memory,
  and its server relation size was not displayed.
- PostgreSQL results without server/provider versions were accepted.

These tests now pass. The artifact regression also requires PostgreSQL in
each C# object benchmark cell, so a missing measurement fails publication.

## Architecture and scope

The provider implements `IBlogPostsStorage<T>` and uses `ObjectsLifecycle` for
create, read all, scattered reads by id and delete with 32-bit and 64-bit ids.
All operations use the shared deterministic posts and checksum validation.
One transaction covers each timed operation, with per-record EF saves,
untracked reads and deletes by id. Clearing tracking after each save prevents
the read benchmark from returning cached entities and bounds tracker growth.

Each warm-up/repetition owns a unique schema. Creation and schema teardown
happen outside timing. Teardown drops only that generated schema, including
on failed lifecycles. No caller database or shared table is deleted. No unique
title constraint or generated date default changes the shared workload.
The date remains integer Unix seconds, including zero, as in the SQLite store.

The provider is an optional local object variant enabled through
`POSTGRESQL_CONNECTION_STRING`; explicit selection without configuration
fails. Existing embedded variants and links benchmarks remain available.
CI supplies a pinned PostgreSQL service beside all C# object variants, with
another job for PostgreSQL integration and configuration/report tests.

Server timings include EF and local client/server communication, so the report
does not assign an embedded SQLite durability baseline to PostgreSQL.
`server_bytes` is measured separately from `file_bytes`: table, indexes and
TOAST through `pg_total_relation_size`, excluding shared WAL and server memory.
Provider, EF Core and server versions are reported without connection strings.

The implementation follows the [Npgsql EF Core documentation](https://www.npgsql.org/efcore/)
and [PostgreSQL size functions](https://www.postgresql.org/docs/current/functions-admin.html).

## Local verification

- Release build with warnings as errors and C# whitespace checks pass.
- 43 C# tests pass with PostgreSQL 16.15: both id widths, repeated validated
  lifecycles, Unicode/empty strings, duplicate titles, date zero, fresh server
  reads after an external update, rollback/reuse and schema isolation/cleanup.
- The executable PostgreSQL report probe passes for both id widths, checks
  metadata and server bytes, preserves all 12 embedded variants without server
  configuration and rejects explicit unconfigured selection.
- 59 Python tests and six evidence integrity tests pass.
- Rust formatting, Clippy with warnings denied and all nine Rust tests pass.
- The existing SQLite executable probe passes all four provider report cells.
- Ruff, mypy, Bandit, Markdown, workflow policies, actionlint/ShellCheck,
  both zizmor profiles and the direct/transitive NuGet audit pass.

Docker cannot start containers in this workspace's cgroup configuration;
PostgreSQL was extracted from Ubuntu packages into a temporary directory and
run as the workspace user with 64 MiB shared buffers and 20 connections.
Hosted integration uses the pinned PostgreSQL 18.3 service. The bounded tests
establish correctness and report integration, not a general performance claim.

## Hosted static analysis

Codacy on `522ee59` reported S2339 for the public environment-variable constant
and S3459 for the EF-generated identity property. The configuration name now
uses a static read-only property. New rows explicitly start with the unset
identity value zero, which EF replaces with the generated key after saving.
The PostgreSQL integration tests and executable report probe verify that ids
are generated and round-trip correctly with these changes.
