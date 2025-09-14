using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Comparisons.SQLiteVSDoublets.Model;
using Comparisons.SQLiteVSDoublets.SQLite;
using Comparisons.SQLiteVSDoublets.PostgreSQL;
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
        private PostgreSQLTestRun _postgresqlTestRun;
        private DoubletsTestRun _doubletsTestRun;

        [GlobalSetup]
        public void Setup()
        {
            BlogPosts.GenerateData(N);
            _sqliteTestRun = new SQLiteTestRun("test.db");
            _postgresqlTestRun = new PostgreSQLTestRun("Host=localhost;Database=test;Username=test;Password=test");
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
        public void PostgreSQL() => _postgresqlTestRun.Run();

        [IterationCleanup(Target = "PostgreSQL")]
        public void PostgreSQLOutput()
        {
            Directory.CreateDirectory(SizeAfterCreationColumn.DbSizeOutputFolder);
            File.WriteAllText(Path.Combine(SizeAfterCreationColumn.DbSizeOutputFolder, $"disk-size.postgresql.{N}.txt"), _postgresqlTestRun.Results.DbSizeAfterCreation.ToString());
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
