#!/usr/bin/env python3
"""Turns the benchmark JSON reports into the README results sections and charts."""

# Usage: benchmark_report.py RESULTS_DIR [--configurations DIR] [--readme README.md] [--readme README.ru.md]
#        [--charts DIR]
#
# Every JSON report (written by `rust` and `csharp` with `--output`) is one table: a language,
# a category (links or objects), an address/id space (32 or 64 bit) and a size, measured on one machine.
# The README section between the markers is regenerated in the hierarchy
# category -> language -> bits -> size, and charts are written per category, language and bits.
# Reports labelled with a `configuration` (see benchmark_configuration.py) are measured in a non-default
# setup, a durable store or a memory limit, and follow as separate tables.

import argparse
import json
import math
import re
import statistics
from pathlib import Path
from typing import Any

from benchmark_provenance import library_versions

START_MARKER = "<!--BENCHMARK_RESULTS_START-->"
END_MARKER = "<!--BENCHMARK_RESULTS_END-->"
# The wide tables cannot be wrapped, and the hierarchy repeats headings under different parents.
LINT_OFF = "<!-- markdownlint-disable MD013 MD024 -->"
LINT_ON = "<!-- markdownlint-restore -->"

CATEGORIES = ("links", "objects")
CONFIGURATIONS = ("durable", "memory_limit")
TRANSACTIONAL = "Doublets_United_NonVolatile_Transactional"
# The variants of each configured table, in table order. The durable table pairs the transactional store
# with both SQLite files, which sync at every commit. Under a memory limit the tuned SQLite cache would be
# anonymous memory charged to the limit, so the stock SQLite file is measured; the transactional store
# keeps its journal in memory and cannot take part.
CONFIGURED_VARIANTS = {
    "durable": ("SQLite_File", "SQLite_File_Tuned", TRANSACTIONAL),
    "memory_limit": ("SQLite_File", "Doublets_United_NonVolatile", "Doublets_Split_NonVolatile"),
}
# A memory limit table is measured by one job per storage of one run; these fields belong to each job.
JOB_FIELDS = ("machine", "date")
JOB_CONFIGURATION = ("disk", "peak_bytes", "major_faults")
NOISE = 0.05
LANGUAGES = ("Rust", "C#")
BITS = (32, 64)

OPERATIONS = {
    "links": (
        "create",
        "query_all",
        "query_by_id",
        "query_by_from_to",
        "query_by_from",
        "query_by_to",
        "update",
        "delete",
    ),
    "objects": ("create", "read_all", "read_by_id", "delete"),
}


def russian_plural(count, one, few, many):
    if count % 10 == 1 and count % 100 != 11:
        return one
    return few if 2 <= count % 10 <= 4 and not 12 <= count % 100 <= 14 else many


TEXT: dict[str, dict[str, Any]] = {
    "en": {
        "category": {
            "links": "Doublets vs SQLite as storage for links",
            "objects": "Doublets vs SQLite as storage for objects",
        },
        "language": "{language} doublets vs SQLite",
        "bits": "{bits} bit address/id space benchmarks",
        "size": {"links": "{size} links", "objects": "{size} blog posts"},
        "operations": {
            "create": "Create",
            "query_all": "Read all",
            "query_by_id": "Read by id",
            "query_by_from_to": "Search (from, to)",
            "query_by_from": "Read by from",
            "query_by_to": "Read by to",
            "update": "Update",
            "delete": "Delete",
            "read_all": "Read all",
            "read_by_id": "Read by id",
        },
        "storage": "Storage",
        "file_size": "File size",
        "server_size": "Server relations",
        "faster": "{ratio}× faster",
        "slower": "{ratio}× slower",
        "same": "≈ same",
        "provenance": "{repetitions} after a warm-up, median time per operation. SQLite {sqlite}; {software}. {machine}, {source}.",
        "unrecorded": "version at measurement not recorded",
        "toolchain_missing": "toolchain version not recorded",
        "log_scale": "log scale",
        "linear_scale": "linear scale",
        "conclusions": "Conclusions",
        "summary": "{language}, {category}: {faster} faster, {slower} slower, {same} approximately equal Doublets operation comparisons with SQLite.",
        "summary_note": "Volatile Doublets stores are compared with `SQLite Memory`. Non-volatile ones are file-backed without durability guarantees (no journal, no `fsync`) and are compared with `SQLite File Tuned` (or `SQLite File` where no tuned row was measured), which still syncs at every commit; the transactional store is compared with `SQLite File`, both durable at every commit. The same noise rule as in the tables applies. Counts span the measured variants, sizes and id widths; they are not an overall speed ranking. RAM usage is measured only as the cgroup peak of the memory-limit tables.",
        "configured_summary": "{language}, {category}, {configuration}: {faster} faster, {slower} slower, {same} approximately equal Doublets operation comparisons with SQLite.",
        "configurations": {
            "durable": "Durable file storage",
            "memory_limit": "Under memory pressure",
        },
        "configuration_names": {"durable": "durable", "memory_limit": "memory limit"},
        "configuration_intro": {
            "durable": "`Doublets United NonVolatile Transactional` is the united file-mapped store behind doublets' `TransactionsDecorator` with the shipped `FileTransitionLog`, which calls `sync_all` after every journal record. One transaction covers each lifecycle phase that writes: it begins at the phase's first write and commits at its end, so the read phases begin none. It is compared with `SQLite File`, which syncs its rollback journal and database at every `COMMIT` (`synchronous=FULL`): both are durable at commit.",
            "memory_limit": "The benchmark process runs in a cgroup with a memory limit (`memory.max`, swap disabled). The limit charges the page cache too, so neither the SQLite file nor the mapped Doublets store fits in memory, and pages that are not resident are read from the disk. With the kernel's default dirty page thresholds, a mapped store's dirty pages and pages under writeback filled the limit, which reclaim cannot free, and the store was killed. So the runner's `vm.dirty_bytes` and `vm.dirty_background_bytes` are set to 20% and 10% of the limit. The kernel applies byte thresholds to a cgroup as a fraction of the runner's whole memory, rounded up to 1/4096, of the memory the cgroup can use, so the cgroup's own thresholds are a few pages under 24 MiB and below 1 MiB under 240 MiB: the mapped stores write back their pages almost as soon as they dirty them. The transactional store cannot take part: its decorator keeps every transition and a full snapshot of the links in memory, which exceeds any limit small enough to create pressure. Under pressure a store can take hours, so each storage runs in its own job of one run, and the second table shows each job's runner, disk latencies, peak cgroup memory and major page faults. Major page faults count the misses of the mapped stores only: SQLite reads its file with `read`, whose misses are not page faults.",
        },
        "configured_heading": "{language}, {bits} bit, {size}",
        "limit_heading": ", memory limit {limit}",
        "limit_provenance": "Memory limit {limit}; peak cgroup memory {peak}; {faults} major page faults.",
        "dirty_provenance": "`vm.dirty_bytes` {dirty}, `vm.dirty_background_bytes` {background}.",
        "disk_provenance": "Disk of the stores: {latencies}.",
        "job_machines": "Each storage in its own job (below)",
        "job_limit_provenance": "Memory limit {limit}.",
        "at_least": "> {value}",
        "slower_at_least": "> {ratio}× slower",
        "unfinished": "_{variants}: stopped after {minutes} min, the time a job has for its storage. `>` marks the time per operation the phase in progress had already spent, a lower bound, and `—` the phases it did not reach._",
        "job_header": (
            "Storage",
            "Machine",
            "Random 4 KiB read",
            "`fsync`",
            "Peak cgroup memory",
            "Major page faults",
        ),
        "random_read": "random 4 KiB read {value}",
        "fsync": "`fsync` {value}",
        "repetitions": lambda count: f"{count} repetition" + ("" if count == 1 else "s"),
        "machine": "unknown machine",
        "local": "a local run",
        "run": "[GitHub Actions run]({url}) on {date}",
        "missing": "_No results yet._",
        "chart": "{language} doublets vs SQLite, {bits} bit, {category}",
        "nouns": {"links": "links", "objects": "objects"},
    },
    "ru": {
        "category": {
            "links": "Дуплеты против SQLite как хранилище связей",
            "objects": "Дуплеты против SQLite как хранилище объектов",
        },
        "language": "Дуплеты на {language} против SQLite",
        "bits": "Тесты с {bits}-битным пространством адресов/идентификаторов",
        "size": {"links": "{size} связей", "objects": "{size} записей блога"},
        "operations": {
            "create": "Создание",
            "query_all": "Чтение всех",
            "query_by_id": "Чтение по id",
            "query_by_from_to": "Поиск (from, to)",
            "query_by_from": "Чтение по from",
            "query_by_to": "Чтение по to",
            "update": "Обновление",
            "delete": "Удаление",
            "read_all": "Чтение всех",
            "read_by_id": "Чтение по id",
        },
        "storage": "Хранилище",
        "file_size": "Размер файлов",
        "server_size": "Размер на сервере",
        "faster": "в {ratio}× быстрее",
        "slower": "в {ratio}× медленнее",
        "same": "≈ так же",
        "provenance": "{repetitions} после прогрева, медианное время одной операции. SQLite {sqlite}; {software}. {machine}, {source}.",
        "unrecorded": "версия при измерении не записана",
        "toolchain_missing": "версия среды разработки не записана",
        "log_scale": "логарифмическая шкала",
        "linear_scale": "линейная шкала",
        "conclusions": "Выводы",
        "summary": "{language}, {category}: Дуплеты быстрее в {faster}, медленнее в {slower}, примерно равны SQLite в {same} сравнениях операций.",
        "summary_note": "Энергозависимые хранилища Дуплетов сравниваются с `SQLite Memory`. Энергонезависимые хранятся в файле без гарантий надёжности (без журнала и `fsync`) и сравниваются с `SQLite File Tuned` (или `SQLite File`, если настроенный вариант не измерялся), который всё равно синхронизирует данные при каждой фиксации; транзакционное хранилище сравнивается с `SQLite File`, и оба надёжны при каждой фиксации. Применяется то же правило учёта шума, что и в таблицах. Числа охватывают измеренные варианты, размеры и разрядности id и не являются общим рейтингом скорости. Использование ОЗУ измеряется только как пиковое потребление cgroup в таблицах с ограничением памяти.",
        "configured_summary": "{language}, {category}, {configuration}: Дуплеты быстрее в {faster}, медленнее в {slower}, примерно равны SQLite в {same} сравнениях операций.",
        "configurations": {
            "durable": "Надёжное хранение в файле",
            "memory_limit": "При нехватке памяти",
        },
        "configuration_names": {"durable": "надёжное хранение", "memory_limit": "ограничение памяти"},
        "configuration_intro": {
            "durable": "`Doublets United NonVolatile Transactional` — объединённое хранилище в отображаемом файле за `TransactionsDecorator` из doublets с поставляемым `FileTransitionLog`, который вызывает `sync_all` после каждой записи журнала. Каждая записывающая фаза жизненного цикла — одна транзакция: она начинается при первой записи фазы и фиксируется в её конце, поэтому фазы чтения транзакций не начинают. Оно сравнивается с `SQLite File`, который синхронизирует журнал отката и базу при каждом `COMMIT` (`synchronous=FULL`): оба надёжны при фиксации.",
            "memory_limit": "Процесс теста работает в cgroup с ограничением памяти (`memory.max`, без подкачки). Ограничение учитывает и страничный кэш, поэтому ни файл SQLite, ни отображаемое хранилище Дуплетов не помещаются в память, и отсутствующие в памяти страницы читаются с диска. С порогами грязных страниц ядра по умолчанию грязные страницы отображаемого хранилища и страницы в процессе записи заполнили ограничение, освободить которое возврат памяти не может, и хранилище было завершено принудительно. Поэтому `vm.dirty_bytes` и `vm.dirty_background_bytes` машины задаются равными 20% и 10% от ограничения. Ядро применяет пороги в байтах к cgroup как долю всей памяти машины, округлённую вверх до 1/4096, от памяти, доступной cgroup, поэтому собственные пороги cgroup составляют несколько страниц при 24 МиБ и меньше 1 МиБ при 240 МиБ: отображаемые хранилища записывают страницы на диск почти сразу после изменения. Транзакционное хранилище в этом участвовать не может: его декоратор хранит в памяти каждый переход и полный снимок связей, что больше любого ограничения, достаточно малого для нехватки памяти. При нехватке памяти хранилище может работать часами, поэтому каждое хранилище измеряется в отдельном задании одного запуска, а вторая таблица показывает машину, задержки диска, пиковое потребление cgroup и основные страничные отказы каждого задания. Основные страничные отказы учитывают промахи только отображаемых хранилищ: SQLite читает файл вызовом `read`, промахи которого не являются страничными отказами.",
        },
        "configured_heading": "{language}, {bits} бит, {size}",
        "limit_heading": ", ограничение памяти {limit}",
        "limit_provenance": "Ограничение памяти {limit}; пиковое потребление cgroup {peak}; {faults} основных страничных отказов.",
        "dirty_provenance": "`vm.dirty_bytes` {dirty}, `vm.dirty_background_bytes` {background}.",
        "disk_provenance": "Диск хранилищ: {latencies}.",
        "job_machines": "Каждое хранилище в отдельном задании (ниже)",
        "job_limit_provenance": "Ограничение памяти {limit}.",
        "at_least": "> {value}",
        "slower_at_least": "более чем в {ratio}× медленнее",
        "unfinished": "_{variants}: остановлено через {minutes} мин, время, которое задание отводит хранилищу. `>` отмечает время одной операции, уже затраченное незавершённым этапом, то есть нижнюю границу, а `—` — этапы, до которых измерение не дошло._",
        "job_header": (
            "Хранилище",
            "Машина",
            "Случайное чтение 4 КиБ",
            "`fsync`",
            "Пиковое потребление cgroup",
            "Основные страничные отказы",
        ),
        "random_read": "случайное чтение 4 КиБ {value}",
        "fsync": "`fsync` {value}",
        "repetitions": lambda count: f"{count} повтор" + russian_plural(count, "", "а", "ов"),
        "machine": "неизвестная машина",
        "local": "локальный запуск",
        "run": "[запуск GitHub Actions]({url}) от {date}",
        "missing": "_Результатов пока нет._",
        "chart": "Дуплеты на {language} против SQLite, {bits} бит, {category}",
        "nouns": {"links": "связи", "objects": "объекты"},
    },
}


def load(directory):
    """All reports in `directory`, keyed by (category, language, bits, size)."""
    reports = {}
    for path in sorted(Path(directory).glob("*.json")):
        report = json.loads(path.read_text(encoding="utf-8"))
        validate(report, path)
        if "configuration" in report:
            raise ValueError(f"{path} is measured in a configuration; keep it with the configured reports")
        key = (report["category"], report["language"], report["bits"], report["size"])
        if key in reports:
            raise ValueError(f"{path} duplicates the results of {key}")
        reports[key] = report
    if not reports:
        raise ValueError(f"No benchmark reports found in {directory}")
    return reports


def load_configurations(directory):
    """The configured reports in `directory`, if it exists, keyed by configuration_key().

    The reports of one memory limit table, measured by separate jobs of one run, are merged into one.
    """
    groups: dict[tuple, list[tuple[Path, dict]]] = {}
    for path in sorted(Path(directory).glob("*.json")):
        report = json.loads(path.read_text(encoding="utf-8"))
        validate(report, path)
        if "configuration" not in report:
            raise ValueError(f"{path} has no configuration; keep it with the default reports")
        key = configuration_key(report)
        if key in groups and report["configuration"]["kind"] != "memory_limit":
            raise ValueError(f"{path} duplicates the results of {key}")
        groups.setdefault(key, []).append((path, report))
    reports = {}
    for key, group in groups.items():
        report = group[0][1] if len(group) == 1 else merge_jobs(group)
        if "SQLite_File" not in (result["variant"] for result in report["results"]):
            raise ValueError(f"{group[0][0]} has no SQLite File baseline in its table")
        reports[key] = report
    return reports


def merge_jobs(group):
    """One table of the (path, report) pairs that separate jobs of one run measured, a storage each.

    Everything but the runner, the date and the disk and memory of each job must match; they are kept
    in the merged report's `jobs`, and its date is the date of the last job.
    """
    first_path, first = group[0]
    shared = {key: value for key, value in first.items() if key not in (*JOB_FIELDS, "results")}
    shared["configuration"] = {
        key: value for key, value in first["configuration"].items() if key not in JOB_CONFIGURATION
    }
    jobs: list[dict] = []
    results: list[dict] = []
    for path, report in group:
        configuration = {
            key: value for key, value in report["configuration"].items() if key not in JOB_CONFIGURATION
        }
        others = {key: value for key, value in report.items() if key not in (*JOB_FIELDS, "results")}
        if {**others, "configuration": configuration} != shared:
            raise ValueError(f"{path} and {first_path} are not measured by one run in one configuration")
        variants = [result["variant"] for result in report["results"]]
        if any(result["variant"] in variants for result in results):
            raise ValueError(f"{path} duplicates a storage of {first_path}")
        results += report["results"]
        jobs.append(
            {
                "variants": variants,
                **{key: report[key] for key in JOB_FIELDS if key in report},
                **{
                    key: report["configuration"][key]
                    for key in JOB_CONFIGURATION
                    if key in report["configuration"]
                },
            }
        )
    order = CONFIGURED_VARIANTS[shared["configuration"]["kind"]]

    def position(variant):
        return (order.index(variant), "") if variant in order else (len(order), variant)

    jobs.sort(key=lambda job: position(job["variants"][0]))
    dates = [job["date"] for job in jobs if "date" in job]
    return {
        **shared,
        **({"date": max(dates)} if dates else {}),
        "results": sorted(results, key=lambda result: position(result["variant"])),
        "jobs": jobs,
    }


def configuration_key(report):
    configuration = report["configuration"]
    return (
        CONFIGURATIONS.index(configuration["kind"]),
        configuration.get("memory_limit_bytes", 0),
        CATEGORIES.index(report["category"]),
        LANGUAGES.index(report["language"]),
        report["bits"],
        report["size"],
    )


def validate(report, path):
    """Reject incomplete measurements before publishing tables or charts."""
    try:
        validate_metadata(report)
        if "configuration" in report:
            validate_configuration(report)
        else:
            validate_variants(report["results"])
        validate_postgresql(report)
        for result in report["results"]:
            if "unfinished" in result:
                validate_unfinished(report, result)
            elif set(result["operations"]) != set(OPERATIONS[report["category"]]):
                raise ValueError("missing or unexpected operations")
            for measurement in result["operations"].values():
                validate_measurement(measurement)
    except (KeyError, TypeError, ValueError) as error:
        raise ValueError(f"Invalid benchmark report {path}: {error}") from error


def validate_unfinished(report, result):
    """A store stopped under a memory limit has finished the first phases and been stopped in the next."""
    if report.get("configuration", {}).get("kind") != "memory_limit":
        raise ValueError("only a memory limit report may have a stopped store")
    operations = OPERATIONS[report["category"]]
    finished = len(result["operations"])
    unfinished = result["unfinished"]
    if (
        report["repetitions"] != 1
        or set(result["operations"]) != set(operations[:finished])
        or finished == len(operations)
        or unfinished.get("operation") != operations[finished]
        or not positive_integer(unfinished.get("after_seconds"))
        or ("lower_bound_ns" in unfinished and not positive_number(unfinished["lower_bound_ns"]))
        or set(unfinished) - {"operation", "after_seconds", "lower_bound_ns"}
    ):
        raise ValueError(f"invalid stopped store {result['variant']}")


def validate_metadata(report):
    if report["category"] not in CATEGORIES or report["language"] not in LANGUAGES:
        raise ValueError("unknown category or language")
    if report["bits"] not in BITS or not positive_integer(report["size"]):
        raise ValueError("invalid bits or size")
    if not positive_integer(report["repetitions"]):
        raise ValueError("invalid repetitions")
    if not report["sqlite_version"]:
        raise ValueError("missing SQLite version")
    if "software" in report:
        software = report["software"]
        library = "doublets" if report["language"] == "Rust" else "Platform.Data.Doublets"
        if (
            not isinstance(software, dict)
            or not isinstance(software.get("libraries"), dict)
            or not isinstance(software["libraries"].get(library), str)
            or not software["libraries"][library].strip()
            or not isinstance(software.get("toolchain"), str)
            or not software["toolchain"].strip()
        ):
            raise ValueError("missing or invalid software provenance")


def validate_variants(results):
    variants = [result["variant"] for result in results]
    if len(variants) != len(set(variants)):
        raise ValueError("duplicate variants")
    if not {"SQLite_Memory", "SQLite_File"}.issubset(variants):
        raise ValueError("missing SQLite baseline")


def validate_configuration(report):
    """A durable report pairs the transactional store with SQLite File; a memory limit excludes it."""
    configuration = report["configuration"]
    variants = [result["variant"] for result in report["results"]]
    if len(variants) != len(set(variants)):
        raise ValueError("duplicate variants")
    if any(variant.startswith("Doublets_") and "_NonVolatile" not in variant for variant in variants):
        raise ValueError("configured reports measure file-backed stores only")
    disk = configuration.get("disk", {})
    if not isinstance(disk, dict) or not all(positive_number(value) for value in disk.values()):
        raise ValueError("invalid disk latencies")
    if configuration["kind"] == "durable":
        if TRANSACTIONAL not in variants or "SQLite_File" not in variants:
            raise ValueError(f"a durable report needs {TRANSACTIONAL} and SQLite File")
    elif configuration["kind"] == "memory_limit":
        if TRANSACTIONAL in variants:
            raise ValueError(
                "the transactional store keeps its journal in memory and cannot run under a limit"
            )
        if not positive_integer(configuration.get("memory_limit_bytes")) or not positive_number(
            disk.get("random_read_4k_ns")
        ):
            raise ValueError("a memory limit report needs the limit and the disk's random 4 KiB read latency")
        for key in ("peak_bytes", "major_faults"):
            if key in configuration and not isinstance(configuration[key], int):
                raise ValueError(f"invalid {key}")
        for key in ("dirty_bytes", "dirty_background_bytes"):
            if key in configuration and not positive_integer(configuration[key]):
                raise ValueError(f"invalid {key}")
    else:
        raise ValueError("unknown configuration")


def positive_number(value):
    return (
        isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value) and value > 0
    )


def validate_postgresql(report):
    """Require PostgreSQL provenance and keep server bytes separate from local files."""
    postgres = [result for result in report["results"] if result["variant"] == "PostgreSQL_EFCore"]
    if not postgres:
        return
    metadata = report.get("postgresql", {})
    if (
        report["language"] != "C#"
        or report["category"] != "objects"
        or metadata.get("provider") != "Npgsql.EntityFrameworkCore.PostgreSQL"
        or not all(metadata.get(key) for key in ("provider_version", "ef_core_version", "server_version"))
        or postgres[0].get("file_bytes") is not None
        or not positive_integer(postgres[0].get("server_bytes"))
    ):
        raise ValueError("missing or invalid PostgreSQL provenance or server size")


def positive_integer(value):
    return isinstance(value, int) and not isinstance(value, bool) and value > 0


def validate_measurement(measurement):
    values = [measurement[key] for key in ("median_ns", "min_ns", "max_ns")]
    values += measurement["samples_ns"]
    if not measurement["samples_ns"] or any(
        not isinstance(value, (int, float))
        or isinstance(value, bool)
        or not math.isfinite(value)
        or value <= 0
        for value in values
    ):
        raise ValueError("measurements must be finite and positive")
    if not measurement["min_ns"] <= measurement["median_ns"] <= measurement["max_ns"]:
        raise ValueError("median must be between min and max")


def baseline(variant, measured=()):
    """The Microsoft.Data.Sqlite row each variant is compared with, among the `measured` variants.

    Volatile stores are compared with SQLite Memory. File-backed stores without durability guarantees are
    compared with the tuned SQLite file, whose page cache and memory map cover the file like the mapped
    Doublets store, if it was measured. The transactional store pays for durability like SQLite File.
    The tuned SQLite rows are compared with the stock ones and System.Data.SQLite with the same storage.
    """
    if variant.startswith("SQLite_File_"):
        return "SQLite_File"
    if variant.startswith("SQLite") or variant == "PostgreSQL_EFCore":
        return None
    if variant.startswith("SystemDataSQLite_"):
        return variant.replace("SystemDataSQLite_", "SQLite_", 1)
    if variant == TRANSACTIONAL:
        return "SQLite_File"
    if "_NonVolatile" in variant:
        return "SQLite_File_Tuned" if "SQLite_File_Tuned" in measured else "SQLite_File"
    return "SQLite_Memory"


def significant(value):
    """Three significant digits without an exponent: 999.6 is 1000, not 1e+03."""
    return f"{float(f'{value:.3g}'):g}"


def duration(nanoseconds):
    rounded = float(significant(nanoseconds))
    for unit, scale in (("s", 1e9), ("ms", 1e6), ("µs", 1e3)):
        if rounded >= scale:
            return f"{significant(rounded / scale)} {unit}"
    return f"{significant(rounded)} ns"


def size(count):
    return f"{count:,}"


def file_size(count):
    return "—" if count is None else f"{count / 2**20:.1f} MiB"


def quartiles(measured):
    """The middle half of the samples: unlike the full range, one outlier repetition does not widen it."""
    samples = measured["samples_ns"]
    if len(samples) == 1:
        return samples[0], samples[0]
    first, _, third = statistics.quantiles(samples, n=4, method="inclusive")
    return first, third


def comparison(measured, reference, text):
    """How `measured` compares with `reference`."""
    # Overlapping interquartile ranges and medians within NOISE of each other (single samples have no range)
    # are not called a difference.
    (low, high), (reference_low, reference_high) = quartiles(measured), quartiles(reference)
    overlapping = low <= reference_high and reference_low <= high
    ratio = max(measured["median_ns"], reference["median_ns"]) / min(
        measured["median_ns"], reference["median_ns"]
    )
    if overlapping or ratio < 1 + NOISE:
        return text["same"]
    if measured["median_ns"] <= reference["median_ns"]:
        return text["faster"].format(ratio=significant(reference["median_ns"] / measured["median_ns"]))
    return text["slower"].format(ratio=significant(measured["median_ns"] / reference["median_ns"]))


def table(report, text):
    operations = OPERATIONS[report["category"]]
    by_variant = {result["variant"]: result for result in report["results"]}
    has_server = "PostgreSQL_EFCore" in by_variant
    header = [
        text["storage"],
        *(text["operations"][operation] for operation in operations),
        text["file_size"],
        *([text["server_size"]] if has_server else []),
    ]
    lines = ["| " + " | ".join(header) + " |", "| --- |" + " ---: |" * (len(header) - 1)]
    for variant, result in by_variant.items():
        reference = by_variant.get(baseline(variant, by_variant))
        cells = [variant.replace("_", " ")]
        for operation in operations:
            cells.append(cell(result, reference, operation, text))
        cells.append(file_size(result["file_bytes"]))
        if has_server:
            cells.append(file_size(result.get("server_bytes")))
        lines.append("| " + " | ".join(cells) + " |")
    return "\n".join(lines)


def cell(result, reference, operation, text):
    """The time of `operation` compared with the `reference` row; a stopped store may have a lower bound."""
    if operation not in result["operations"]:
        unfinished = result.get("unfinished", {})
        if unfinished.get("operation") != operation or "lower_bound_ns" not in unfinished:
            return "—"
        bound = unfinished["lower_bound_ns"]
        value = text["at_least"].format(value=duration(bound))
        other = reference["operations"].get(operation) if reference is not None else None
        if other is not None and bound > other["median_ns"] * (1 + NOISE):
            ratio = significant(bound / other["median_ns"])
            value += f" ({text['slower_at_least'].format(ratio=ratio)})"
        return value
    measured = result["operations"][operation]
    value = duration(measured["median_ns"])
    if reference is not None and operation in reference["operations"]:
        value += f" ({comparison(measured, reference['operations'][operation], text)})"
    return value


def unfinished_note(report, text):
    """Which stores of `report` were stopped and after how long, or None."""
    stopped = [result for result in report["results"] if "unfinished" in result]
    if not stopped:
        return None
    minutes = {result["unfinished"]["after_seconds"] // 60 for result in stopped}
    return text["unfinished"].format(
        variants=", ".join(result["variant"].replace("_", " ") for result in stopped),
        minutes="/".join(str(value) for value in sorted(minutes)),
    )


def provenance(report, text):
    run = report.get("run_url")
    software = report.get("software")
    if software:
        libraries = software["libraries"]
        versions = "; ".join(f"{name} {version}" for name, version in libraries.items())
        versions += f"; {software['toolchain']}"
    else:
        # Older reports remain readable, but today's manifests are not measured provenance.
        versions = "; ".join(
            f"{name} {version}" for name, version in library_versions(report["language"]).items()
        )
        versions += f" ({text['unrecorded']}); {text['toolchain_missing']}"
    value = text["provenance"].format(
        repetitions=text["repetitions"](report["repetitions"]),
        sqlite=report["sqlite_version"],
        software=versions,
        machine=text["job_machines"] if "jobs" in report else report.get("machine") or text["machine"],
        source=text["run"].format(url=run, date=report.get("date")) if run else text["local"],
    )
    if "postgresql" in report:
        postgres = report["postgresql"]
        value += (
            f" PostgreSQL {postgres['server_version']}; "
            f"Npgsql.EntityFrameworkCore.PostgreSQL {postgres['provider_version']}; "
            f"EF Core {postgres['ef_core_version']}."
        )
    configuration = report.get("configuration", {})
    if "memory_limit_bytes" in configuration:
        limit = memory(configuration["memory_limit_bytes"])
        if "jobs" in report:
            value += " " + text["job_limit_provenance"].format(limit=limit)
        else:
            value += " " + text["limit_provenance"].format(
                limit=limit,
                peak=memory(configuration["peak_bytes"]) if "peak_bytes" in configuration else "—",
                faults=size(configuration["major_faults"]) if "major_faults" in configuration else "—",
            )
        if "dirty_bytes" in configuration:
            value += " " + text["dirty_provenance"].format(
                dirty=memory(configuration["dirty_bytes"]),
                background=memory(configuration.get("dirty_background_bytes", configuration["dirty_bytes"])),
            )
    disk = configuration.get("disk", {})
    latencies = [
        text[name].format(value=duration(disk[key]))
        for name, key in (("random_read", "random_read_4k_ns"), ("fsync", "fsync_ns"))
        if key in disk
    ]
    if latencies:
        value += " " + text["disk_provenance"].format(latencies=", ".join(latencies))
    return f"_{value}_"


def job_table(report, text):
    """The runner, the disk and the cgroup memory of each job that measured a part of `report`."""
    header = text["job_header"]
    lines = ["| " + " | ".join(header) + " |", "| --- | --- |" + " ---: |" * (len(header) - 2)]
    for job in report["jobs"]:
        disk = job.get("disk", {})
        cells = [
            ", ".join(variant.replace("_", " ") for variant in job["variants"]),
            job.get("machine") or text["machine"],
            *(duration(disk[key]) if key in disk else "—" for key in ("random_read_4k_ns", "fsync_ns")),
            memory(job["peak_bytes"]) if "peak_bytes" in job else "—",
            size(job["major_faults"]) if "major_faults" in job else "—",
        ]
        lines.append("| " + " | ".join(cells) + " |")
    return "\n".join(lines)


def memory(count):
    """Memory limits are whole MiB, peaks are rounded to one decimal."""
    return f"{count // 2**20} MiB" if count % 2**20 == 0 else file_size(count)


def chart_name(category, language, bits, scale="log"):
    suffix = "-linear" if scale == "linear" else ""
    return f"{category}-{language.lower().replace('#', 'sharp')}-{bits}{suffix}.png"


def count_outcomes(reports, text):
    """How the Doublets operations of `reports` compare with their SQLite baselines."""
    counts = {"faster": 0, "slower": 0, "same": 0}
    for data in reports:
        variants = {result["variant"]: result for result in data["results"]}
        for variant, result in variants.items():
            if not variant.startswith("Doublets_"):
                continue
            reference = variants[baseline(variant, variants)]
            # A stopped phase is counted only when the time it had already spent is clearly slower.
            stopped = result.get("unfinished", {})
            other = reference["operations"].get(stopped.get("operation"))
            if other and stopped.get("lower_bound_ns", 0) > other["median_ns"] * (1 + NOISE):
                counts["slower"] += 1
            for operation, measured in result["operations"].items():
                if operation not in reference["operations"]:
                    continue
                other = reference["operations"][operation]
                outcome = (
                    "same"
                    if comparison(measured, other, text) == text["same"]
                    else "faster"
                    if measured["median_ns"] < other["median_ns"]
                    else "slower"
                )
                counts[outcome] += 1
    return counts


def conclusions(reports, text, configured=None):
    """Summarize the measured Doublets comparisons with the table's durability and noise rules."""
    lines = [f"## {text['conclusions']}", "", text["summary_note"], ""]
    for category in CATEGORIES:
        for language in LANGUAGES:
            counts = count_outcomes(
                (data for key, data in reports.items() if key[:2] == (category, language)), text
            )
            if sum(counts.values()):
                lines.append(
                    "- "
                    + text["summary"].format(language=language, category=text["nouns"][category], **counts)
                )
    for kind in CONFIGURATIONS:
        for category in CATEGORIES:
            for language in LANGUAGES:
                counts = count_outcomes(
                    (
                        data
                        for data in (configured or {}).values()
                        if (data["configuration"]["kind"], data["category"], data["language"])
                        == (kind, category, language)
                    ),
                    text,
                )
                if sum(counts.values()):
                    lines.append(
                        "- "
                        + text["configured_summary"].format(
                            language=language,
                            category=text["nouns"][category],
                            configuration=text["configuration_names"][kind],
                            **counts,
                        )
                    )
    return lines


def configured_sections(configured, text):
    """One section per configuration, with a table per report in the configuration_key() order."""
    lines = []
    for kind in CONFIGURATIONS:
        reports = [configured[key] for key in sorted(configured) if CONFIGURATIONS[key[0]] == kind]
        if not reports:
            continue
        lines += [f"## {text['configurations'][kind]}", "", text["configuration_intro"][kind], ""]
        for report in reports:
            heading = text["configured_heading"].format(
                language=report["language"],
                bits=report["bits"],
                size=text["size"][report["category"]].format(size=size(report["size"])),
            )
            if "memory_limit_bytes" in report["configuration"]:
                heading += text["limit_heading"].format(
                    limit=memory(report["configuration"]["memory_limit_bytes"])
                )
            lines += [f"### {heading}", "", provenance(report, text), "", table(report, text), ""]
            note = unfinished_note(report, text)
            if note:
                lines += [note, ""]
            if "jobs" in report:
                lines += [job_table(report, text), ""]
    return lines


def section(reports, language_code, charts_link, configured=None):
    """The Markdown results section in the category -> language -> bits -> size hierarchy,
    followed by the configured reports and the conclusions."""
    text = TEXT[language_code]
    lines = []
    for category in CATEGORIES:
        lines += [f"## {text['category'][category]}", ""]
        for language in LANGUAGES:
            lines += [f"### {text['language'].format(language=language)}", ""]
            for bits in BITS:
                lines += [f"#### {text['bits'].format(bits=bits)}", ""]
                sizes = sorted(key[3] for key in reports if key[:3] == (category, language, bits))
                if not sizes:
                    lines += [text["missing"], ""]
                    continue
                for count in sizes:
                    report = reports[(category, language, bits, count)]
                    lines += [
                        f"##### {text['size'][category].format(size=size(count))}",
                        "",
                        provenance(report, text),
                        "",
                        table(report, text),
                        "",
                    ]
                if charts_link is not None:
                    title = text["chart"].format(
                        language=language, bits=bits, category=text["nouns"][category]
                    )
                    for scale in ("linear", "log"):
                        name = chart_name(category, language, bits, scale)
                        lines += [f"![{title}, {text[f'{scale}_scale']}]({charts_link}/{name})", ""]
    lines += configured_sections(configured or {}, text)
    lines += conclusions(reports, text, configured)
    return "\n".join(lines).rstrip() + "\n"


def replace_section(document, generated):
    pattern = re.compile(re.escape(START_MARKER) + ".*?" + re.escape(END_MARKER), re.DOTALL)
    if not pattern.search(document):
        raise ValueError(f"{START_MARKER} ... {END_MARKER} markers are missing")
    return pattern.sub(lambda _: f"{START_MARKER}\n{LINT_OFF}\n{generated}{LINT_ON}\n{END_MARKER}", document)


def charts(reports, directory):
    """Linear and log charts per category, language and bits, with a panel per size."""
    import matplotlib

    matplotlib.use("Agg")
    directory.mkdir(parents=True, exist_ok=True)
    written = []
    for category in CATEGORIES:
        for language in LANGUAGES:
            for bits in BITS:
                sizes = sorted(key[3] for key in reports if key[:3] == (category, language, bits))
                if not sizes:
                    continue
                for scale in ("linear", "log"):
                    path = directory / chart_name(category, language, bits, scale)
                    draw_chart(reports, category, language, bits, sizes, scale, path)
                    written.append(path)
    return written


def draw_chart(reports, category, language, bits, sizes, scale, path):
    """Draw each size in its own panel, with a visibility floor only on the linear scale."""
    import matplotlib.pyplot as plt

    operations = OPERATIONS[category]
    labels = [TEXT["en"]["operations"][operation] for operation in operations]
    figure, axes = plt.subplots(1, len(sizes), figsize=(6 * len(sizes), 6), squeeze=False, sharey=False)
    try:
        for axis, count in zip(axes[0], sizes, strict=True):
            results = reports[(category, language, bits, count)]["results"]
            width = 0.8 / len(results)
            maximum = max(
                result["operations"][operation]["median_ns"] for result in results for operation in operations
            )
            minimum = maximum * 0.005 if scale == "linear" else 0
            for index, result in enumerate(results):
                axis.bar(
                    [position + index * width for position in range(len(operations))],
                    [max(result["operations"][operation]["median_ns"], minimum) for operation in operations],
                    width,
                    label=result["variant"].replace("_", " "),
                    color=plt.get_cmap("tab20" if len(results) > 10 else "tab10")(index),
                )
            axis.set_yscale(scale)
            if scale == "linear":
                axis.set_ylim(bottom=0)
            axis.set_title(TEXT["en"]["size"][category].format(size=size(count)))
            axis.set_ylabel(f"median ns per operation ({scale} scale)")
            axis.set_xticks([position + 0.4 - width / 2 for position in range(len(operations))])
            axis.set_xticklabels(labels, rotation=30, ha="right")
            axis.grid(axis="y", which="major", alpha=0.3)
        handles, names = axes[0][0].get_legend_handles_labels()
        footer = 0.04 if scale == "linear" else 0
        figure.legend(
            handles, names, loc="lower center", ncol=3, fontsize="small", bbox_to_anchor=(0.5, footer)
        )
        figure.suptitle(TEXT["en"]["chart"].format(language=language, bits=bits, category=category))
        if scale == "linear":
            figure.text(
                0.5,
                0.01,
                "Bars below 0.5% of each panel's maximum are raised for visibility.\nSee tables for exact times.",
                ha="center",
                fontsize="small",
            )
        legend_height = 0.05 + math.ceil(len(names) / 3) * 0.035
        figure.tight_layout(rect=(0, legend_height + footer, 1, 1))
        figure.savefig(path, dpi=80)
    finally:
        plt.close(figure)


def main(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("results", type=Path, help="directory with the JSON reports")
    parser.add_argument("--configurations", type=Path, help="directory with the configured JSON reports")
    parser.add_argument("--readme", type=Path, action="append", default=[], help="README to update in place")
    parser.add_argument("--charts", type=Path, help="directory for the charts, relative to the READMEs")
    options = parser.parse_args(arguments)
    reports = load(options.results)
    configured = {} if options.configurations is None else load_configurations(options.configurations)
    if options.charts is not None:
        charts(reports, options.charts)
    for readme in options.readme:
        language_code = "ru" if readme.name.endswith(".ru.md") else "en"
        link = (
            None
            if options.charts is None
            else options.charts.resolve().relative_to(readme.resolve().parent).as_posix()
        )
        document = readme.read_text(encoding="utf-8")
        readme.write_text(
            replace_section(document, section(reports, language_code, link, configured)), encoding="utf-8"
        )
    if not options.readme:
        print(section(reports, "en", None, configured))


if __name__ == "__main__":
    main()
