using System.Linq;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets.PostgreSQL
{
    /// <summary>
    /// <para>
    /// Represents the PostgreSQL test run.
    /// </para>
    /// <para></para>
    /// </summary>
    /// <seealso cref="TestRun"/>
    public class PostgreSQLTestRun : TestRun
    {
        /// <summary>
        /// <para>
        /// The connection string.
        /// </para>
        /// <para></para>
        /// </summary>
        private readonly string _connectionString;

        /// <summary>
        /// <para>
        /// Initializes a new <see cref="PostgreSQLTestRun"/> instance.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="connectionString">
        /// <para>A connection string.</para>
        /// <para></para>
        /// </param>
        public PostgreSQLTestRun(string connectionString) : base("postgresql")
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// <para>
        /// Prepares this instance.
        /// </para>
        /// <para></para>
        /// </summary>
        public override void Prepare()
        {
            using var dbContext = new PostgreSQLDbContext(_connectionString);
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
            using var dbContext = new PostgreSQLDbContext(_connectionString);
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
            using var dbContext = new PostgreSQLDbContext(_connectionString);
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
            using var dbContext = new PostgreSQLDbContext(_connectionString);
            var blogPostsToDelete = dbContext.BlogPosts.ToList();
            dbContext.BlogPosts.RemoveRange(blogPostsToDelete);
            dbContext.SaveChanges();
        }
    }
}