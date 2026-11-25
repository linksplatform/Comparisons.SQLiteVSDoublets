"""Probe the disk of a benchmark directory and label a report measured in a non-default configuration.

Usage:
  benchmark_configuration.py plan PLAN_JSON
  benchmark_configuration.py report DIRECTORY --sha SHA
  benchmark_configuration.py probe DIRECTORY [--file-mib N] [--reads N] [--syncs N]
  benchmark_configuration.py record REPORT --kind durable|memory_limit [--disk JSON]
      [--memory-limit-bytes N] [--cgroup-stats FILE]

`probe` prints the median latency of random 4 KiB reads that bypass the page cache (O_DIRECT) and of
an `fsync` after a small append, the two disk operations the configured tables pay for: page misses under
a memory limit and the journal syncs of the transactional store.
`record` adds the configuration, the probed latencies and the cgroup's memory.peak and major page
faults (a file with `memory.peak` followed by `memory.stat`, as the configured workflow writes it).
`plan` validates the runs of a configured benchmark workflow and prints its job matrix, and `report`
validates the reports the runs wrote and prints their tables.
"""

import argparse
import json
import mmap
import os
import random
import statistics
import time
from pathlib import Path

import benchmark_report

PAGE = 4096
# The durable table pairs the transactional store with both SQLite files, which sync at every commit.
# Under a memory limit the tuned SQLite cache would be anonymous memory charged to the limit, so the stock
# SQLite file is measured; the transactional store keeps its journal in memory and cannot take part.
VARIANTS = {
    "durable": ["SQLite_File", "SQLite_File_Tuned", "Doublets_United_NonVolatile_Transactional"],
    "memory_limit": ["SQLite_File", "Doublets_United_NonVolatile", "Doublets_Split_NonVolatile"],
}


def plan(runs):
    """The matrix rows of `runs`: Rust links tables with their variants and, under a limit, its MiB."""
    if not isinstance(runs, list) or not 1 <= len(runs) <= 16:
        raise ValueError("the plan must list between 1 and 16 runs")
    rows = []
    for run in runs:
        if not isinstance(run, dict) or run.get("kind") not in VARIANTS:
            raise ValueError(f"unknown configuration in {run}")
        expected = {"kind", "bits", "size"} | ({"limit_mib"} if run["kind"] == "memory_limit" else set())
        if set(run) != expected:
            raise ValueError(f"{run} must have exactly {sorted(expected)}")
        if run["bits"] not in (32, 64) or any(
            not isinstance(run[key], int) or isinstance(run[key], bool) or run[key] < 1
            for key in expected - {"kind"}
        ):
            raise ValueError(f"invalid bits, size or limit in {run}")
        if run["size"] > 10_000_000:
            raise ValueError("sizes stop at 10,000,000")
        limit = f"-{run['limit_mib']}MiB" if "limit_mib" in run else ""
        name = f"{run['kind'].replace('_', '-')}{limit}-links-rust-{run['bits']}-{run['size']}"
        rows.append(
            {
                **run,
                "limit_mib": run.get("limit_mib", 0),
                "name": name,
                "variants": ",".join(VARIANTS[run["kind"]]),
            }
        )
    if len({row["name"] for row in rows}) != len(rows):
        raise ValueError("runs must be unique")
    return rows


def random_read_4k_ns(path, reads, seed=0):
    """Median time of a 4 KiB read at a random page of `path`, bypassing the page cache."""
    pages = path.stat().st_size // PAGE
    generator = random.Random(seed)  # nosec B311 # disk offsets to sample, not a secret.
    # O_DIRECT needs a page-aligned buffer, which an anonymous map is.
    buffer = mmap.mmap(-1, PAGE)
    descriptor = os.open(path, os.O_RDONLY | os.O_DIRECT)
    try:
        samples = []
        for _ in range(reads):
            offset = generator.randrange(pages) * PAGE
            start = time.perf_counter_ns()
            if os.preadv(descriptor, [buffer], offset) != PAGE:
                raise OSError(f"short read at {offset} of {path}")
            samples.append(time.perf_counter_ns() - start)
    finally:
        os.close(descriptor)
        buffer.close()
    return statistics.median(samples)


def fsync_ns(path, syncs):
    """Median time of `fsync` after appending one small record, like a journal does."""
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_APPEND, 0o600)
    try:
        samples = []
        for index in range(syncs):
            os.write(descriptor, index.to_bytes(8, "little") * 8)
            start = time.perf_counter_ns()
            os.fsync(descriptor)
            samples.append(time.perf_counter_ns() - start)
    finally:
        os.close(descriptor)
    return statistics.median(samples)


def probe(directory, file_mib=512, reads=2000, syncs=200):
    directory.mkdir(parents=True, exist_ok=True)
    data = directory / "disk-probe.data"
    journal = directory / "disk-probe.journal"
    try:
        # Incompressible pages, synced, so that the reads come from the disk and not from zero pages.
        chunk = os.urandom(2**20)
        with data.open("wb") as file:
            for _ in range(file_mib):
                file.write(chunk)
            file.flush()
            os.fsync(file.fileno())
        return {
            "random_read_4k_ns": random_read_4k_ns(data, reads),
            "fsync_ns": fsync_ns(journal, syncs),
        }
    finally:
        data.unlink(missing_ok=True)
        journal.unlink(missing_ok=True)


def cgroup_stats(path):
    """memory.peak and the major page faults from a file with memory.peak followed by memory.stat."""
    lines = path.read_text(encoding="utf-8").split("\n")
    stats = dict(line.split(" ", 1) for line in lines[1:] if " " in line)
    return {"peak_bytes": int(lines[0]), "major_faults": int(stats["pgmajfault"])}


def record(path, kind, disk=None, memory_limit_bytes=None, stats=None, dirty=None):
    """Add the configuration while preserving every measurement and existing metadata field.

    `dirty` holds the kernel's dirty page thresholds of a memory limit run, `dirty_bytes` and
    `dirty_background_bytes`.
    """
    data = json.loads(path.read_text(encoding="utf-8"))
    configuration = {"kind": kind}
    if memory_limit_bytes is not None:
        configuration["memory_limit_bytes"] = memory_limit_bytes
        configuration.update(stats or {})
        configuration.update(dirty or {})
    if disk:
        configuration["disk"] = disk
    data["configuration"] = configuration
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def report(directory, sha):
    """The configured tables of the reports in `directory`, which must be measured from `sha`."""
    reports = benchmark_report.load_configurations(directory)
    if not reports:
        raise ValueError(f"No configured reports found in {directory}")
    for data in reports.values():
        if data.get("source_sha") != sha:
            raise ValueError(f"a {data['configuration']['kind']} report was not measured from {sha}")
    text = benchmark_report.TEXT["en"]
    lines = benchmark_report.configured_sections(reports, text)
    lines += benchmark_report.conclusions({}, text, reports)[4:]
    return "\n".join(lines).rstrip() + "\n"


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    commands = parser.add_subparsers(dest="command", required=True)
    planning = commands.add_parser("plan")
    planning.add_argument("plan", type=json.loads)
    reporting = commands.add_parser("report")
    reporting.add_argument("directory", type=Path)
    reporting.add_argument("--sha", required=True)
    probing = commands.add_parser("probe")
    probing.add_argument("directory", type=Path)
    probing.add_argument("--file-mib", type=int, default=512)
    probing.add_argument("--reads", type=int, default=2000)
    probing.add_argument("--syncs", type=int, default=200)
    recording = commands.add_parser("record")
    recording.add_argument("report", type=Path)
    recording.add_argument("--kind", choices=("durable", "memory_limit"), required=True)
    recording.add_argument("--disk", type=json.loads, help="the JSON printed by probe")
    recording.add_argument("--memory-limit-bytes", type=int)
    recording.add_argument("--cgroup-stats", type=Path)
    recording.add_argument("--dirty-bytes", type=int, help="vm.dirty_bytes of a memory limit run")
    recording.add_argument(
        "--dirty-background-bytes", type=int, help="vm.dirty_background_bytes of a memory limit run"
    )
    options = parser.parse_args(arguments)
    if options.command == "plan":
        print(json.dumps({"include": plan(options.plan)}, separators=(",", ":")))
        return
    if options.command == "report":
        print(report(options.directory, options.sha), end="")
        return
    if options.command == "probe":
        print(json.dumps(probe(options.directory, options.file_mib, options.reads, options.syncs)))
        return
    if (options.kind == "memory_limit") != (options.memory_limit_bytes is not None):
        parser.error("--memory-limit-bytes is required for, and only for, --kind memory_limit")
    dirty = {
        key: value
        for key, value in (
            ("dirty_bytes", options.dirty_bytes),
            ("dirty_background_bytes", options.dirty_background_bytes),
        )
        if value is not None
    }
    if dirty and options.kind != "memory_limit":
        parser.error("the dirty page thresholds are recorded only for --kind memory_limit")
    stats = None if options.cgroup_stats is None else cgroup_stats(options.cgroup_stats)
    record(options.report, options.kind, options.disk, options.memory_limit_bytes, stats, dirty)


if __name__ == "__main__":
    main()
