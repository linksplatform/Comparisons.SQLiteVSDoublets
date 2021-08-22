using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Comparisons.SQLiteVSDoublets
{
    /// <summary>
    /// <para>
    /// Represents the size after creation column.
    /// </para>
    /// <para></para>
    /// </summary>
    /// <seealso cref="IColumn"/>
    public class SizeAfterCreationColumn : IColumn
    {
        /// <summary>
        /// <para>
        /// The sq lite vs doublets.
        /// </para>
        /// <para></para>
        /// </summary>
        public static readonly string DbSizeOutputFolder = Path.Combine(Path.GetTempPath(), nameof(Comparisons), nameof(SQLiteVSDoublets));

        /// <summary>
        /// <para>
        /// Gets the id value.
        /// </para>
        /// <para></para>
        /// </summary>
        public string Id => nameof(SizeAfterCreationColumn);

        /// <summary>
        /// <para>
        /// Gets the column name value.
        /// </para>
        /// <para></para>
        /// </summary>
        public string ColumnName => "SizeAfterCreation";

        /// <summary>
        /// <para>
        /// Gets the legend value.
        /// </para>
        /// <para></para>
        /// </summary>
        public string Legend => "Allocated memory on disk after all records are created (1KB = 1024B)";

        /// <summary>
        /// <para>
        /// Gets the unit type value.
        /// </para>
        /// <para></para>
        /// </summary>
        public UnitType UnitType => UnitType.Size;

        /// <summary>
        /// <para>
        /// Gets the always show value.
        /// </para>
        /// <para></para>
        /// </summary>
        public bool AlwaysShow => true;

        /// <summary>
        /// <para>
        /// Gets the category value.
        /// </para>
        /// <para></para>
        /// </summary>
        public ColumnCategory Category => ColumnCategory.Metric;

        /// <summary>
        /// <para>
        /// Gets the priority in category value.
        /// </para>
        /// <para></para>
        /// </summary>
        public int PriorityInCategory => 0;

        /// <summary>
        /// <para>
        /// Gets the is numeric value.
        /// </para>
        /// <para></para>
        /// </summary>
        public bool IsNumeric => true;

        /// <summary>
        /// <para>
        /// Determines whether this instance is available.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="summary">
        /// <para>The summary.</para>
        /// <para></para>
        /// </param>
        /// <returns>
        /// <para>The bool</para>
        /// <para></para>
        /// </returns>
        public bool IsAvailable(Summary summary) => true;

        /// <summary>
        /// <para>
        /// Determines whether this instance is default.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="summary">
        /// <para>The summary.</para>
        /// <para></para>
        /// </param>
        /// <param name="benchmarkCase">
        /// <para>The benchmark case.</para>
        /// <para></para>
        /// </param>
        /// <returns>
        /// <para>The bool</para>
        /// <para></para>
        /// </returns>
        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

        /// <summary>
        /// <para>
        /// Gets the value using the specified summary.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="summary">
        /// <para>The summary.</para>
        /// <para></para>
        /// </param>
        /// <param name="benchmarkCase">
        /// <para>The benchmark case.</para>
        /// <para></para>
        /// </param>
        /// <returns>
        /// <para>The string</para>
        /// <para></para>
        /// </returns>
        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        /// <summary>
        /// <para>
        /// Gets the value using the specified summary.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="summary">
        /// <para>The summary.</para>
        /// <para></para>
        /// </param>
        /// <param name="benchmarkCase">
        /// <para>The benchmark case.</para>
        /// <para></para>
        /// </param>
        /// <param name="style">
        /// <para>The style.</para>
        /// <para></para>
        /// </param>
        /// <returns>
        /// <para>The string</para>
        /// <para></para>
        /// </returns>
        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var benchmarkName = benchmarkCase.Descriptor.WorkloadMethod.Name.ToLower();
            var parameter = benchmarkCase.Parameters.Items.FirstOrDefault(x => x.Name == "N");
            if (parameter == null)
            {
                return "no parameter";
            }
            var N = Convert.ToInt32(parameter.Value);
            var filename = Path.Combine(DbSizeOutputFolder, $"disk-size.{benchmarkName}.{N}.txt");
            return File.Exists(filename) ? File.ReadAllText(filename) : "no file";
        }

        /// <summary>
        /// <para>
        /// Returns the string.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <returns>
        /// <para>The string</para>
        /// <para></para>
        /// </returns>
        public override string ToString() => ColumnName;
    }
}
