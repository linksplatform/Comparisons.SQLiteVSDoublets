using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Comparisons.SQLiteVSDoublets
{
    public class SizeAfterCreationColumn : IColumn
    {
        public string Id => nameof(SizeAfterCreationColumn);

        public string ColumnName => "SizeAfterCreation";

        public string Legend => "Allocated memory on disk after all records are created (1KB = 1024B)";

        public UnitType UnitType => UnitType.Size;

        public bool AlwaysShow => true;

        public ColumnCategory Category => ColumnCategory.Metric;

        public int PriorityInCategory => 0;

        public bool IsNumeric => true;

        public bool IsAvailable(Summary summary) => true;

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var benchmarkName = benchmarkCase.Descriptor.WorkloadMethod.Name.ToLower();
            var parameter = benchmarkCase.Parameters.Items.FirstOrDefault(x => x.Name == "N");
            if (parameter == null)
            {
                return "no parameter";
            }
            var N = Convert.ToInt32(parameter.Value);
            var filename = $"disk-size.{benchmarkName}.{N}.txt";
            return File.Exists(filename) ? File.ReadAllText(filename) : "no file";
        }

        public override string ToString() => ColumnName;
    }
}
