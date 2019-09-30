using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Comparisons.SQLiteVSDoublets.Model;
using Comparisons.SQLiteVSDoublets.SQLite;
using Comparisons.SQLiteVSDoublets.Doublets;

namespace Comparisons.SQLiteVSDoublets
{
    [ClrJob, CoreJob]
    [MemoryDiagnoser]
    [MaxWarmupCount(2)]
    [IterationCount(1)]
    [Config(typeof(Config))]
    public class Benchmarks
    {
        [Params(1000, 10000, 100000)]
        public int N;
        private SQLiteTestRun _sqliteTestRun;
        private DoubletsTestRun _doubletsTestRun;

        private class Config : ManualConfig
        {
            public Config() => Add(new SizeAfterCreationColumn());
        }

        [GlobalSetup]
        public void Setup()
        {
            BlogPosts.GenerateData(N);
            _sqliteTestRun = new SQLiteTestRun("test.db");
            _doubletsTestRun = new DoubletsTestRun("test.links");
        }

        //[GlobalCleanup]
        //public void Cleanup()
        //{
        //    if (_sqliteTestRun.Results.DbSizeAfterCreation > 0)
        //    {
        //        File.WriteAllText($"disk-size.sqlite.{N}.txt", _sqliteTestRun.Results.DbSizeAfterCreation.ToString());
        //    }
        //    if (_doubletsTestRun.Results.DbSizeAfterCreation > 0)
        //    {
        //        File.WriteAllText($"disk-size.doublets.{N}.txt", _doubletsTestRun.Results.DbSizeAfterCreation.ToString());
        //    }
        //}

        [Benchmark]
        public void SQLite() => _sqliteTestRun.Run();

        [IterationCleanup(Target = "SQLite")]
        public void SQLiteOutput() => File.WriteAllText($"disk-size.sqlite.{N}.txt", _sqliteTestRun.Results.DbSizeAfterCreation.ToString());

        [Benchmark]
        public void Doublets() => _doubletsTestRun.Run();

        [IterationCleanup(Target = "Doublets")]
        public void DoubletsOutput() => File.WriteAllText($"disk-size.doublets.{N}.txt", _doubletsTestRun.Results.DbSizeAfterCreation.ToString());
    }
}
