using System;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using Comparisons.SQLiteVSDoublets.Model;

namespace Comparisons.SQLiteVSDoublets.SystemDataSQLite
{
    /// <summary>
    /// <para>
    /// Represents the System.Data.SQLite test run.
    /// </para>
    /// <para></para>
    /// </summary>
    /// <seealso cref="TestRun"/>
    public class SystemDataSQLiteTestRun : TestRun
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
        /// Initializes a new <see cref="SystemDataSQLiteTestRun"/> instance.
        /// </para>
        /// <para></para>
        /// </summary>
        /// <param name="dbFilename">
        /// <para>A db filename.</para>
        /// <para></para>
        /// </param>
        public SystemDataSQLiteTestRun(string dbFilename) : base(dbFilename) 
        {
            _connectionString = $"Data Source={dbFilename};Version=3;";
        }

        /// <summary>
        /// <para>
        /// Prepares this instance.
        /// </para>
        /// <para></para>
        /// </summary>
        public override void Prepare()
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            
            const string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS BlogPosts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL UNIQUE,
                    Content TEXT NOT NULL,
                    PublicationDateTime TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                )";
            
            using var command = new SQLiteCommand(createTableQuery, connection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// <para>
        /// Creates the list.
        /// </para>
        /// <para></para>
        /// </summary>
        public override void CreateList()
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            
            using var transaction = connection.BeginTransaction();
            
            const string insertQuery = @"
                INSERT INTO BlogPosts (Title, Content, PublicationDateTime) 
                VALUES (@Title, @Content, @PublicationDateTime)";
            
            using var command = new SQLiteCommand(insertQuery, connection, transaction);
            
            var titleParam = command.Parameters.Add("@Title", DbType.String);
            var contentParam = command.Parameters.Add("@Content", DbType.String);
            var dateParam = command.Parameters.Add("@PublicationDateTime", DbType.String);
            
            foreach (var blogPost in BlogPosts.List)
            {
                titleParam.Value = blogPost.Title;
                contentParam.Value = blogPost.Content;
                dateParam.Value = blogPost.PublicationDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                
                command.ExecuteNonQuery();
            }
            
            transaction.Commit();
        }

        /// <summary>
        /// <para>
        /// Reads the list.
        /// </para>
        /// <para></para>
        /// </summary>
        public override void ReadList()
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            
            const string selectQuery = @"SELECT Id, Title, Content, PublicationDateTime FROM BlogPosts";
            
            using var command = new SQLiteCommand(selectQuery, connection);
            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                var blogPost = new BlogPost
                {
                    Id = reader.GetInt32("Id"),
                    Title = reader.GetString("Title"),
                    Content = reader.GetString("Content"),
                    PublicationDateTime = DateTime.Parse(reader.GetString("PublicationDateTime"))
                };
                
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
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            
            const string deleteQuery = @"DELETE FROM BlogPosts";
            
            using var command = new SQLiteCommand(deleteQuery, connection);
            command.ExecuteNonQuery();
        }
    }
}