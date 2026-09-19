using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Comparisons.SQLiteVSDoublets.Doublets;
using Comparisons.SQLiteVSDoublets.Model;
using Comparisons.SQLiteVSDoublets.SQLite;

namespace Comparisons.SQLiteVSDoublets;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test")
        {
            LinksSelfTest.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--manual-object-test")
        {
            RunManualObjectTest();
            return;
        }

        if (args.Length == 0)
        {
            // Preserve the original entry point for the object-like benchmark.
            BenchmarkRunner.Run<Benchmarks>();
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchmarkConfig());
    }

    private static void RunManualObjectTest()
    {
        const int numberOfTestRuns = 1;
        const int numberOfRecordsPerTestRun = 1;
        BlogPosts.GenerateData(numberOfRecordsPerTestRun);
        var sqliteTestRuns = new List<SQLiteTestRun>();
        var doubletsTestRuns = new List<DoubletsTestRun>();
        for (var index = 0; index < numberOfTestRuns; index++)
        {
            var sqliteTestRun = new SQLiteTestRun("test.db");
            sqliteTestRun.Run();
            sqliteTestRuns.Add(sqliteTestRun);
            var doubletsTestRun = new DoubletsTestRun("test.links");
            doubletsTestRun.Run();
            doubletsTestRuns.Add(doubletsTestRun);
        }
        Console.WriteLine("SQLite results:");
        Console.WriteLine(GetResultsAverage(sqliteTestRuns));
        Console.WriteLine("Doublets results:");
        Console.WriteLine(GetResultsAverage(doubletsTestRuns));
    }

    private static TestRunResults GetResultsAverage(IEnumerable<TestRun> testRuns) => new()
    {
        PrepareTime = new TimeSpan(
            (long)testRuns.Select(run => run.Results.PrepareTime.Ticks).Average()),
        DbSizeAfterPrepare = (long)testRuns.Select(
            run => run.Results.DbSizeAfterPrepare).Average(),
        ListCreationTime = new TimeSpan(
            (long)testRuns.Select(run => run.Results.ListCreationTime.Ticks).Average()),
        DbSizeAfterCreation = (long)testRuns.Select(
            run => run.Results.DbSizeAfterCreation).Average(),
        ListReadingTime = new TimeSpan(
            (long)testRuns.Select(run => run.Results.ListReadingTime.Ticks).Average()),
        DbSizeAfterReading = (long)testRuns.Select(
            run => run.Results.DbSizeAfterReading).Average(),
        ListDeletionTime = new TimeSpan(
            (long)testRuns.Select(run => run.Results.ListDeletionTime.Ticks).Average()),
        DbSizeAfterDeletion = (long)testRuns.Select(
            run => run.Results.DbSizeAfterDeletion).Average(),
    };

    private static IConfig BenchmarkConfig()
    {
        var warmups = EnvironmentValue("BENCHMARK_WARMUP_COUNT", 1);
        var iterations = EnvironmentValue("BENCHMARK_ITERATION_COUNT", 3);
        var job = Job.Default
            .WithWarmupCount(warmups)
            .WithIterationCount(iterations);
        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(job)
            .AddExporter(JsonExporter.Full)
            .WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(40));
    }

    private static int EnvironmentValue(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0
            ? value
            : fallback;
}
