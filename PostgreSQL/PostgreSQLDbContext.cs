using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets.PostgreSQL
{
    /// <summary>
    /// <para>
    /// Represents the PostgreSQL db context.
    /// </para>
    /// <para></para>
    /// </summary>
    /// <seealso cref="DbContext"/>
    public class PostgreSQLDbContext : DbContext
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
        /// Gets or sets the blog posts value.
        /// </para>
        /// <para></para>
        /// </summary>
        public DbSet<BlogPost> BlogPosts { get; set; }

        /// <summary>
        /// <para>
        /// Initializes a new <see cref="PostgreSQLDbContext"/> instance.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="connectionString">
        /// <para>A connection string.</para>
        /// <para></para>
        /// </param>
        public PostgreSQLDbContext(string connectionString) => _connectionString = connectionString;

        /// <summary>
        /// <para>
        /// Ons the configuring using the specified options builder.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="optionsBuilder">
        /// <para>The options builder.</para>
        /// <para></para>
        /// </param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_connectionString, options =>
            {
                options.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
            });
            base.OnConfiguring(optionsBuilder);
        }

        /// <summary>
        /// <para>
        /// Ons the model creating using the specified model builder.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="modelBuilder">
        /// <para>The model builder.</para>
        /// <para></para>
        /// </param>
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