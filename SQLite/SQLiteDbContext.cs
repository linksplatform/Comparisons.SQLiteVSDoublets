using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets.SQLite
{
    public class SQLiteDbContext : DbContext
    {
        private readonly string _dbFilename;

        public DbSet<BlogPost> BlogPosts { get; set; }

        public SQLiteDbContext(string dbFilename) => _dbFilename = dbFilename;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Filename={_dbFilename}", options =>
            {
                options.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
            });
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BlogPost>().ToTable("BlogPosts", "test");
            modelBuilder.Entity<BlogPost>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Title).IsUnique();
                entity.Property(e => e.PublicationDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}
