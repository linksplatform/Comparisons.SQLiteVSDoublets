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
                DeleteDatabase();
            }
            Results.PrepareTime = Performance.Measure(Prepare);
            Results.DbSizeAfterPrepare = GetDatabaseSizeInBytes();
            Results.ListCreationTime = Performance.Measure(CreateList);
            Results.DbSizeAfterCreation = GetDatabaseSizeInBytes();
            Results.ListReadingTime = Performance.Measure(ReadList);
            Results.DbSizeAfterReading = GetDatabaseSizeInBytes();
            Results.ListDeletionTime = Performance.Measure(DeleteList);
            Results.DbSizeAfterDeletion = GetDatabaseSizeInBytes();
            DeleteDatabase();
        }

        protected virtual long GetDatabaseSizeInBytes()
        {
            return FileHelpers.GetSize(DbFilename);
        }

        protected virtual void DeleteDatabase()
        {
            File.Delete(DbFilename);
        }

        public abstract void Prepare();

        public abstract void CreateList();

        public abstract void ReadList();

        public abstract void DeleteList();
    }
}
