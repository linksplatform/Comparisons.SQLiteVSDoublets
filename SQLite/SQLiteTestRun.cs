using System.Linq;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets.SQLite
{
    /// <summary>
    /// <para>
    /// Represents the sq lite test run.
    /// </para>
    /// <para></para>
    /// </summary>
    /// <seealso cref="TestRun"/>
    public class SQLiteTestRun : TestRun
    {
        /// <summary>
        /// <para>
        /// Initializes a new <see cref="SQLiteTestRun"/> instance.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="dbFilename">
        /// <para>A db filename.</para>
        /// <para></para>
        /// </param>
        public SQLiteTestRun(string dbFilename) : base(dbFilename) { }

        /// <summary>
        /// <para>
        /// Prepares this instance.
        /// </para>
        /// <para></para>
        /// </summary>
        public override void Prepare()
        {
            using var dbContext = new SQLiteDbContext(DbFilename);
            dbContext.Database.EnsureCreated();
        }

        /// <summary>
        /// <para>
        /// Creates the list.
        /// </para>
        /// <para></para>
        /// </summary>
        public override void CreateList()
        {
            using var dbContext = new SQLiteDbContext(DbFilename);
            dbContext.BlogPosts.AddRange(BlogPosts.List);
            dbContext.SaveChanges();
        }

        /// <summary>
        /// <para>
        /// Reads the list.
        /// </para>
        /// <para></para>
        /// </summary>
        public override void ReadList()
        {
            using var dbContext = new SQLiteDbContext(DbFilename);
            foreach (var blogPost in dbContext.BlogPosts)
            {
                ReadBlogPosts.Add(blogPost);
            }
        }

        /// <summary>
        /// <para>
        /// Deletes the list.
        /// </para>
        /// <para></para>
        /// </summary>
        public override void DeleteList()
        {
            using var dbContext = new SQLiteDbContext(DbFilename);
            var blogPostsToDelete = dbContext.BlogPosts.ToList();
            dbContext.BlogPosts.RemoveRange(blogPostsToDelete);
            dbContext.SaveChanges();
        }
    }
}
