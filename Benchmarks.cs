using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Comparisons.SQLiteVSDoublets.Model;
using Comparisons.SQLiteVSDoublets.SQLite;
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
        private DoubletsTestRun _doubletsTestRun;

        [GlobalSetup]
        public void Setup()
        {
            BlogPosts.GenerateData(N);
            _sqliteTestRun = new SQLiteTestRun("test.db");
            _doubletsTestRun = new DoubletsTestRun("test.links");
        }

        [Benchmark]
        public void SQLite() => _sqliteTestRun.Run();

        [IterationCleanup(Target = "SQLite")]
        public void SQLiteOutput() => File.WriteAllText($@"C:\disk-size.sqlite.{N}.txt", _sqliteTestRun.Results.DbSizeAfterCreation.ToString());

        [Benchmark]
        public void Doublets() => _doubletsTestRun.Run();

        [IterationCleanup(Target = "Doublets")]
        public void DoubletsOutput() => File.WriteAllText($@"C:\disk-size.doublets.{N}.txt", _doubletsTestRun.Results.DbSizeAfterCreation.ToString());
    }
}
