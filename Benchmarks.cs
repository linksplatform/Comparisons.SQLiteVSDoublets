using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Comparisons.SQLiteVSDoublets.Model;
using Comparisons.SQLiteVSDoublets.SQLite;
using Comparisons.SQLiteVSDoublets.SystemDataSQLite;
using Comparisons.SQLiteVSDoublets.Doublets;

namespace Comparisons.SQLiteVSDoublets
{
    [SimpleJob]
    [MemoryDiagnoser]
    [WarmupCount(2)]
    [IterationCount(1)]
    [Config(typeof(Config))]
    public class Benchmarks
    {
        private class Config : ManualConfig
        {
            public Config() => Add(new SizeAfterCreationColumn());
        }

        [Params(1000, 10000, 100000)]
        public int N;
        private SQLiteTestRun _sqliteTestRun;
        private SystemDataSQLiteTestRun _systemDataSqliteTestRun;
        private DoubletsTestRun _doubletsTestRun;

        [GlobalSetup]
        public void Setup()
        {
            BlogPosts.GenerateData(N);
            _sqliteTestRun = new SQLiteTestRun("test.db");
            _systemDataSqliteTestRun = new SystemDataSQLiteTestRun("test-systemdata.db");
            _doubletsTestRun = new DoubletsTestRun("test.links");
        }

        [Benchmark]
        public void SQLite() => _sqliteTestRun.Run();

        [IterationCleanup(Target = "SQLite")]
        public void SQLiteOutput()
        {
            Directory.CreateDirectory(SizeAfterCreationColumn.DbSizeOutputFolder);
            File.WriteAllText(Path.Combine(SizeAfterCreationColumn.DbSizeOutputFolder, $"disk-size.sqlite.{N}.txt"), _sqliteTestRun.Results.DbSizeAfterCreation.ToString());
        }

        [Benchmark]
        public void SystemDataSQLite() => _systemDataSqliteTestRun.Run();

        [IterationCleanup(Target = "SystemDataSQLite")]
        public void SystemDataSQLiteOutput()
        {
            Directory.CreateDirectory(SizeAfterCreationColumn.DbSizeOutputFolder);
            File.WriteAllText(Path.Combine(SizeAfterCreationColumn.DbSizeOutputFolder, $"disk-size.systemdata-sqlite.{N}.txt"), _systemDataSqliteTestRun.Results.DbSizeAfterCreation.ToString());
        }

        [Benchmark]
        public void Doublets() => _doubletsTestRun.Run();

        [IterationCleanup(Target = "Doublets")]
        public void DoubletsOutput()
        {
            Directory.CreateDirectory(SizeAfterCreationColumn.DbSizeOutputFolder);
            File.WriteAllText(Path.Combine(SizeAfterCreationColumn.DbSizeOutputFolder, $"disk-size.doublets.{N}.txt"), _doubletsTestRun.Results.DbSizeAfterCreation.ToString());
        }
    }
}
