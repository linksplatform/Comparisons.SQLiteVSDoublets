using System;
using System.Text;

namespace Comparisons.SQLiteVSDoublets
{
    public class TestRunResults
    {
        public TimeSpan PrepareTime { get; set; }
        public long DbSizeAfterPrepare { get; set; }
        public TimeSpan ListCreationTime { get; set; }
        public long DbSizeAfterCreation { get; set; }
        public TimeSpan ListReadingTime { get; set; }
        public long DbSizeAfterReading { get; set; }
        public TimeSpan ListDeletionTime { get; set; }
        public long DbSizeAfterDeletion { get; set; }

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
