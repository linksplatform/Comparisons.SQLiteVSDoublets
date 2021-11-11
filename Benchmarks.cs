using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Comparisons.SQLiteVSDoublets.Model;
using Comparisons.SQLiteVSDoublets.SQLite;
using Comparisons.SQLiteVSDoublets.Doublets;

namespace Comparisons.SQLiteVSDoublets
{
    /// <summary>
    /// <para>
    /// Represents the benchmarks.
    /// </para>
    /// <para></para>
    /// </summary>
    [SimpleJob]
    [MemoryDiagnoser]
    [WarmupCount(2)]
    [IterationCount(1)]
    [Config(typeof(Config))]
    public class Benchmarks
    {
        private class Config : ManualConfig
        {
            /// <summary>
            /// <para>
            /// Initializes a new <see cref="Config"/> instance.
            /// </para>
            /// <para></para>
            /// </summary>
            public Config() => Add(new SizeAfterCreationColumn());
        }

        /// <summary>
        /// <para>
        /// The .
        /// </para>
        /// <para></para>
        /// </summary>
        [Params(1000, 10000, 100000)]
        public int N;
        private SQLiteTestRun _sqliteTestRun;
        private DoubletsTestRun _doubletsTestRun;

        /// <summary>
        /// <para>
        /// Setup this instance.
        /// </para>
        /// <para></para>
        /// </summary>
        [GlobalSetup]
        public void Setup()
        {
            BlogPosts.GenerateData(N);
            _sqliteTestRun = new SQLiteTestRun("test.db");
            _doubletsTestRun = new DoubletsTestRun("test.links");
        }

        /// <summary>
        /// <para>
        /// Sqs the lite.
        /// </para>
        /// <para></para>
        /// </summary>
        [Benchmark]
        public void SQLite() => _sqliteTestRun.Run();

        /// <summary>
        /// <para>
        /// Sqs the lite output.
        /// </para>
        /// <para></para>
        /// </summary>
        [IterationCleanup(Target = "SQLite")]
        public void SQLiteOutput()
        {
            Directory.CreateDirectory(SizeAfterCreationColumn.DbSizeOutputFolder);
            File.WriteAllText(Path.Combine(SizeAfterCreationColumn.DbSizeOutputFolder, $"disk-size.sqlite.{N}.txt"), _sqliteTestRun.Results.DbSizeAfterCreation.ToString());
        }

        /// <summary>
        /// <para>
        /// Doubletses this instance.
        /// </para>
        /// <para></para>
        /// </summary>
        [Benchmark]
        public void Doublets() => _doubletsTestRun.Run();

        /// <summary>
        /// <para>
        /// Doubletses the output.
        /// </para>
        /// <para></para>
        /// </summary>
        [IterationCleanup(Target = "Doublets")]
        public void DoubletsOutput()
        {
            Directory.CreateDirectory(SizeAfterCreationColumn.DbSizeOutputFolder);
            File.WriteAllText(Path.Combine(SizeAfterCreationColumn.DbSizeOutputFolder, $"disk-size.doublets.{N}.txt"), _doubletsTestRun.Results.DbSizeAfterCreation.ToString());
        }
    }
}
