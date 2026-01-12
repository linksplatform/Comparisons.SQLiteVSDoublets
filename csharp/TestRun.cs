using System.IO;
using System.Collections.Generic;
using Platform.IO;
using Platform.Diagnostics;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets
{
    /// <summary>
    /// <para>
    /// Represents the test run.
    /// </para>
    /// <para></para>
    /// </summary>
    public abstract class TestRun
    {
        /// <summary>
        /// <para>
        /// Gets the db filename value.
        /// </para>
        /// <para></para>
        /// </summary>
        public string DbFilename { get; }
        /// <summary>
        /// <para>
        /// Gets the results value.
        /// </para>
        /// <para></para>
        /// </summary>
        public TestRunResults Results { get; }
        /// <summary>
        /// <para>
        /// Gets the read blog posts value.
        /// </para>
        /// <para></para>
        /// </summary>
        public List<BlogPost> ReadBlogPosts { get; }

        /// <summary>
        /// <para>
        /// Initializes a new <see cref="TestRun"/> instance.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="dbFilename">
        /// <para>A db filename.</para>
        /// <para></para>
        /// </param>
        protected TestRun(string dbFilename)
        {
            DbFilename = dbFilename;
            Results = new TestRunResults();
            ReadBlogPosts = new List<BlogPost>();
        }

        /// <summary>
        /// <para>
        /// Runs this instance.
        /// </para>
        /// <para></para>
        /// </summary>
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

        /// <summary>
        /// <para>
        /// Gets the database size in bytes.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <returns>
        /// <para>The long</para>
        /// <para></para>
        /// </returns>
        protected virtual long GetDatabaseSizeInBytes()
        {
            return FileHelpers.GetSize(DbFilename);
        }

        /// <summary>
        /// <para>
        /// Deletes the database.
        /// </para>
        /// <para></para>
        /// </summary>
        protected virtual void DeleteDatabase()
        {
            File.Delete(DbFilename);
        }

        /// <summary>
        /// <para>
        /// Prepares this instance.
        /// </para>
        /// <para></para>
        /// </summary>
        public abstract void Prepare();

        /// <summary>
        /// <para>
        /// Creates the list.
        /// </para>
        /// <para></para>
        /// </summary>
        public abstract void CreateList();

        /// <summary>
        /// <para>
        /// Reads the list.
        /// </para>
        /// <para></para>
        /// </summary>
        public abstract void ReadList();

        /// <summary>
        /// <para>
        /// Deletes the list.
        /// </para>
        /// <para></para>
        /// </summary>
        public abstract void DeleteList();
    }
}
