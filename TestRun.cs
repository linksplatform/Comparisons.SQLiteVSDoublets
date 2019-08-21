using System.IO;
using System.Collections.Generic;
using Platform.IO;
using Platform.Diagnostics;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets
{
    public abstract class TestRun
    {
        public string DbFilename { get; }
        public TestRunResults Results { get; }
        public List<BlogPost> ReadBlogPosts { get; }

        protected TestRun(string dbFilename)
        {
            DbFilename = dbFilename;
            Results = new TestRunResults();
            ReadBlogPosts = new List<BlogPost>();
        }

        public void Run()
        {
            if (File.Exists(DbFilename))
            {
                File.Delete(DbFilename);
            }
            Results.PrepareTime = Performance.Measure(Prepare);
            Results.DbSizeAfterPrepare = FileHelpers.GetSize(DbFilename);
            Results.ListCreationTime = Performance.Measure(CreateList);
            Results.DbSizeAfterCreation = FileHelpers.GetSize(DbFilename);
            Results.ListReadingTime = Performance.Measure(ReadList);
            Results.DbSizeAfterReading = FileHelpers.GetSize(DbFilename);
            Results.ListDeletionTime = Performance.Measure(DeleteList);
            Results.DbSizeAfterDeletion = FileHelpers.GetSize(DbFilename);
            File.Delete(DbFilename);
        }

        public abstract void Prepare();

        public abstract void CreateList();

        public abstract void ReadList();

        public abstract void DeleteList();
    }
}
