using System;
using System.Text;

namespace Comparisons.SQLiteVSDoublets
{
    /// <summary>
    /// <para>
    /// Represents the test run results.
    /// </para>
    /// <para></para>
    /// </summary>
    public class TestRunResults
    {
        /// <summary>
        /// <para>
        /// Gets or sets the prepare time value.
        /// </para>
        /// <para></para>
        /// </summary>
        public TimeSpan PrepareTime { get; set; }
        /// <summary>
        /// <para>
        /// Gets or sets the db size after prepare value.
        /// </para>
        /// <para></para>
        /// </summary>
        public long DbSizeAfterPrepare { get; set; }
        /// <summary>
        /// <para>
        /// Gets or sets the list creation time value.
        /// </para>
        /// <para></para>
        /// </summary>
        public TimeSpan ListCreationTime { get; set; }
        /// <summary>
        /// <para>
        /// Gets or sets the db size after creation value.
        /// </para>
        /// <para></para>
        /// </summary>
        public long DbSizeAfterCreation { get; set; }
        /// <summary>
        /// <para>
        /// Gets or sets the list reading time value.
        /// </para>
        /// <para></para>
        /// </summary>
        public TimeSpan ListReadingTime { get; set; }
        /// <summary>
        /// <para>
        /// Gets or sets the db size after reading value.
        /// </para>
        /// <para></para>
        /// </summary>
        public long DbSizeAfterReading { get; set; }
        /// <summary>
        /// <para>
        /// Gets or sets the list deletion time value.
        /// </para>
        /// <para></para>
        /// </summary>
        public TimeSpan ListDeletionTime { get; set; }
        /// <summary>
        /// <para>
        /// Gets or sets the db size after deletion value.
        /// </para>
        /// <para></para>
        /// </summary>
        public long DbSizeAfterDeletion { get; set; }

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
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (DbSizeAfterPrepare != 0)
            {
                sb.AppendLine($"Prepare execution time: {PrepareTime}, db size after prepare: {DbSizeAfterPrepare}.");
            }
            else
            {
                sb.AppendLine($"Prepare execution time: {PrepareTime}.");
            }
            if (DbSizeAfterCreation != 0)
            {
                sb.AppendLine($"Create list execution time: {ListCreationTime}, db size after list creation: {DbSizeAfterCreation}.");
            }
            else
            {
                sb.AppendLine($"Create list execution time: {ListCreationTime}.");
            }
            if (DbSizeAfterReading != 0)
            {
                sb.AppendLine($"Read list execution time: {ListReadingTime}, db size after list reading: {DbSizeAfterReading}.");
            }
            else
            {
                sb.AppendLine($"Read list execution time: {ListReadingTime}.");
            }
            if (DbSizeAfterDeletion != 0)
            {
                sb.AppendLine($"Delete list execution time: {ListDeletionTime}, db size after list deletion: {DbSizeAfterDeletion}.");
            }
            else
            {
                sb.AppendLine($"Delete list execution time: {ListDeletionTime}.");
            }
            return sb.ToString();
        }
    }
}
