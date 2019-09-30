using System;
using System.Linq;
using System.Collections.Generic;
using Comparisons.SQLiteVSDoublets.SQLite;
using Comparisons.SQLiteVSDoublets.Doublets;
using Comparisons.SQLiteVSDoublets.Model;
using BenchmarkDotNet.Running;

namespace Comparisons.SQLiteVSDoublets
{
    class Program
    {
        static void Main()
        {
            BenchmarkRunner.Run<Benchmarks>();
            //Run();
        }

        private static void Run()
        {
            const int numberOfTestRuns = 1;
            const int numberOfRecordsPerTestRun = 5;
            BlogPosts.GenerateData(numberOfRecordsPerTestRun);
            var sqliteTestRuns = new List<SQLiteTestRun>();
            var doubletsTestRuns = new List<DoubletsTestRun>();
            for (int i = 0; i < numberOfTestRuns; i++)
            {
                var sqliteTestRun = new SQLiteTestRun("test.db");
                sqliteTestRun.Run();
                sqliteTestRuns.Add(sqliteTestRun);
                var doubletsTestRun = new DoubletsTestRun("test.links");
                doubletsTestRun.Run();
                doubletsTestRuns.Add(doubletsTestRun);
            }
            Console.WriteLine("SQLite results:");
            var averageSqliteResults = GetResultsAverage(sqliteTestRuns);
            Console.WriteLine(averageSqliteResults.ToString());
            Console.WriteLine("Doublets results:");
            var averageDoubletsResults = GetResultsAverage(doubletsTestRuns);
            Console.WriteLine(averageDoubletsResults.ToString());
        }

        private static TestRunResults GetResultsAverage(IEnumerable<TestRun> testRuns)
        {
            return new TestRunResults()
            {
                PrepareTime = new TimeSpan((long)testRuns.Select(x => x.Results.PrepareTime.Ticks).Average()),
                DbSizeAfterPrepare = (long)testRuns.Select(x => x.Results.DbSizeAfterPrepare).Average(),
                ListCreationTime = new TimeSpan((long)testRuns.Select(x => x.Results.ListCreationTime.Ticks).Average()),
                DbSizeAfterCreation = (long)testRuns.Select(x => x.Results.DbSizeAfterCreation).Average(),
                ListReadingTime = new TimeSpan((long)testRuns.Select(x => x.Results.ListReadingTime.Ticks).Average()),
                DbSizeAfterReading = (long)testRuns.Select(x => x.Results.DbSizeAfterReading).Average(),
                ListDeletionTime = new TimeSpan((long)testRuns.Select(x => x.Results.ListDeletionTime.Ticks).Average()),
                DbSizeAfterDeletion = (long)testRuns.Select(x => x.Results.DbSizeAfterDeletion).Average(),
            };
        }
    }
}
