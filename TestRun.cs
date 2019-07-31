using System;
using System.IO;
using Platform.IO;
using Platform.Helpers;

namespace Comparisons.SQLiteVSDoublets
{
    public abstract class TestRun
    {
        public string DbFilename { get; }

        protected TestRun(string dbFilename)
        {
            DbFilename = dbFilename;
        }

        public void Run()
        {
            if (File.Exists(DbFilename))
            {
                File.Delete(DbFilename);
            }
            var prepareTime = PerformanceHelpers.Measure(Prepare);
            Console.WriteLine($"Prepare execution time: {prepareTime}, db size after prepare: {FileHelpers.GetSize(DbFilename)}.");
            var createListTime = PerformanceHelpers.Measure(CreateList);
            Console.WriteLine($"Create list execution time: {createListTime}, db size after create list: {FileHelpers.GetSize(DbFilename)}.");
            var readListTime = PerformanceHelpers.Measure(ReadList);
            Console.WriteLine($"Read list execution time: {readListTime}.");
            var deleteListTime = PerformanceHelpers.Measure(DeleteList);
            Console.WriteLine($"Delete list execution time: {deleteListTime}, db size after delete list: {FileHelpers.GetSize(DbFilename)}.");
            File.Delete(DbFilename);
        }

        public abstract void Prepare();

        public abstract void CreateList();

        public abstract void ReadList();

        public abstract void DeleteList();
    }
}
