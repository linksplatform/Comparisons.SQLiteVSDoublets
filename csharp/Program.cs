using System;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

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

        if (args.Length == 0)
        {
            // Preserve the original entry point for the object-like benchmark.
            BenchmarkRunner.Run<Benchmarks>();
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchmarkConfig());
    }

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
