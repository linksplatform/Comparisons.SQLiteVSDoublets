using System;
using System.Linq;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets.Doublets
{
    public class DoubletsTestRun : TestRun
    {
        public DoubletsTestRun(string dbFilename) : base(dbFilename) { }

        public override void Prepare()
        {
            using (var dbContext = new DoubletsDbContext(DbFilename))
            {
            }
        }

        public override void CreateList()
        {
            using (var dbContext = new DoubletsDbContext(DbFilename))
            {
                if (!dbContext.BlogPosts.Any())
                {
                    var blogPosts = BlogPosts.List;
                    foreach (BlogPost blogPost in blogPosts)
                    {
                        dbContext.CreateBlogPost(blogPost);
                    }
                }
            }
        }

        public override void ReadList()
        {
            Console.WriteLine();
            using (var dbContext = new DoubletsDbContext(DbFilename))
            {
                foreach (var blogPost in dbContext.BlogPosts)
                {
                    Console.WriteLine(blogPost);
                }
            }
        }

        public override void DeleteList()
        {
            using (var dbContext = new DoubletsDbContext(DbFilename))
            {
                var blogPosts = dbContext.BlogPosts;
                foreach (var blogPost in blogPosts)
                {
                    dbContext.Delete((uint)blogPost.Id);
                }
            }
        }
    }
}
