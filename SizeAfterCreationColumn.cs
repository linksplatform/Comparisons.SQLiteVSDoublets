using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Comparisons.SQLiteVSDoublets
{
    public class SizeAfterCreationColumn : IColumn, IMetricDescriptor
    {
        public string Id => nameof(SizeAfterCreationColumn);

        public string DisplayName => ColumnName;

        public string Legend => "Allocated memory on disk after all records are created (1KB = 1024B)";

        public string NumberFormat => "N0";

        public UnitType UnitType => UnitType.Size;

        public string Unit => SizeUnit.B.Name;

        public bool TheGreaterTheBetter => false;

        public string ColumnName => "SizeAfterCreation";

        public bool AlwaysShow => true;

        public ColumnCategory Category => ColumnCategory.Metric;

        public int PriorityInCategory => 0;

        public bool IsNumeric => true;

        public bool IsAvailable(Summary summary) => true;

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var benchmarkName = benchmarkCase.Descriptor.WorkloadMethod.Name;
            if (!benchmarkCase.Parameters.Items.Any(x => x.Name == "N"))
            {
                return "no parameter";
            }
            var N = Convert.ToInt32(benchmarkCase.Parameters.Items.Where(x => x.Name == "N").Select(x => x.Value).First());
            var filename = $"disk-size.{benchmarkName}.{N}.txt";
            return File.Exists(filename) ? File.ReadAllText(filename) : "no file";
        }

        public override string ToString() => ColumnName;
    }
}
