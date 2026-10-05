<!-- markdownlint-disable MD043 -->

# System.Data.SQLite native library probe

System.Data.SQLite 2.0.4 contains the managed provider and requires a native
`e_sqlite3` library, as described in the
[upstream build documentation](https://system.data.sqlite.org/home/doc/trunk/www/build.md).
Microsoft.Data.Sqlite 10.0.12 supplies this library for Linux, macOS and Windows.

From the repository root, reproduce the missing native dependency in a
separate output directory:

```bash
dotnet run --project experiments/system_data_sqlite -c Release \
  --artifacts-path /tmp/system-data-sqlite-missing-native \
  -p:IncludeNativeSQLite=false
```

The command fails with `DllNotFoundException` for `e_sqlite3`. Include the
native library with the default configuration:

```bash
dotnet run --project experiments/system_data_sqlite -c Release
```

This opens one in-memory database and prints the provider and SQLite versions.
The benchmark project uses both providers with the same native library and
validates their results through the same lifecycle.

After building the C# solution, check the executable's reports for both
categories, both id sizes and both storage types:

```bash
python3 experiments/system_data_sqlite/check_reports.py
```

The check uses 1,000 records and three repetitions per variant, verifies the
reported provider/engine versions, and removes its temporary files. C# CI
runs it on all three operating systems.
