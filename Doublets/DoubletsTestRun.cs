using System.IO;
using Platform.IO;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets.Doublets
{
    /// <summary>
    /// <para>
    /// Represents the doublets test run.
    /// </para>
    /// <para></para>
    /// </summary>
    /// <seealso cref="TestRun"/>
    public class DoubletsTestRun : TestRun
    {
        /// <summary>
        /// <para>
        /// Gets the db index filename value.
        /// </para>
        /// <para></para>
        /// </summary>
        public string DbIndexFilename { get; }

        /// <summary>
        /// <para>
        /// Initializes a new <see cref="DoubletsTestRun"/> instance.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="dbFilename">
        /// <para>A db filename.</para>
        /// <para></para>
        /// </param>
        public DoubletsTestRun(string dbFilename) : base(dbFilename) => DbIndexFilename = $"{Path.GetFileNameWithoutExtension(dbFilename)}.links.index";

        /// <summary>
        /// <para>
        /// Prepares this instance.
        /// </para>
        /// <para></para>
        /// </summary>
        public override void Prepare()
        {
            using var dbContext = new DoubletsDbContext(DbFilename, DbIndexFilename);
        }

        /// <summary>
        /// <para>
        /// Creates the list.
        /// </para>
        /// <para></para>
        /// </summary>
        public override void CreateList()
        {
            using var dbContext = new DoubletsDbContext(DbFilename, DbIndexFilename);
            foreach (var blogPost in BlogPosts.List)
            {
                dbContext.SaveBlogPost(blogPost);
            }
        }

        /// <summary>
        /// <para>
        /// Reads the list.
        /// </para>
        /// <para></para>
        /// </summary>
        public override void ReadList()
        {
            using var dbContext = new DoubletsDbContext(DbFilename, DbIndexFilename);
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
            using var dbContext = new DoubletsDbContext(DbFilename, DbIndexFilename);
            var blogPostsToDelete = dbContext.BlogPosts;
            foreach (var blogPost in blogPostsToDelete)
            {
                dbContext.Delete((uint)blogPost.Id);
            }
        }

        /// <summary>
        /// <para>
        /// Deletes the database.
        /// </para>
        /// <para></para>
        /// </summary>
        protected override void DeleteDatabase()
        {
            File.Delete(DbFilename);
            File.Delete(DbIndexFilename);
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
        protected override long GetDatabaseSizeInBytes() => FileHelpers.GetSize(DbFilename) + FileHelpers.GetSize(DbIndexFilename);
    }
}
