using System;
using System.IO;
using Comparisons.SQLiteVSDoublets.Model;
using Comparisons.SQLiteVSDoublets.SQLite;
using Comparisons.SQLiteVSDoublets.SystemDataSQLite;

namespace Comparisons.SQLiteVSDoublets.Experiments
{
    /// <summary>
    /// <para>
    /// Simple test to verify System.Data.SQLite implementation works.
    /// </para>
    /// <para></para>
    /// </summary>
    public class TestSystemDataSQLite
    {
        public static void RunTest()
        {
            Console.WriteLine("Testing System.Data.SQLite implementation...");
            
            // Generate test data
            const int testDataSize = 10;
            BlogPosts.GenerateData(testDataSize);
            
            // Test System.Data.SQLite implementation
            var systemDataSqliteTestRun = new SystemDataSQLiteTestRun("test-systemdata.db");
            
            try
            {
                // Clean up any existing test file
                if (File.Exists("test-systemdata.db"))
                    File.Delete("test-systemdata.db");
                
                systemDataSqliteTestRun.Run();
                
                Console.WriteLine("✓ System.Data.SQLite test completed successfully");
                Console.WriteLine($"Results: {systemDataSqliteTestRun.Results}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ System.Data.SQLite test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            // Test Entity Framework SQLite implementation for comparison
            var efSqliteTestRun = new SQLiteTestRun("test-ef.db");
            
            try
            {
                // Clean up any existing test file
                if (File.Exists("test-ef.db"))
                    File.Delete("test-ef.db");
                
                efSqliteTestRun.Run();
                
                Console.WriteLine("✓ Entity Framework SQLite test completed successfully");
                Console.WriteLine($"Results: {efSqliteTestRun.Results}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Entity Framework SQLite test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            // Clean up test files
            try
            {
                if (File.Exists("test-systemdata.db"))
                    File.Delete("test-systemdata.db");
                if (File.Exists("test-ef.db"))
                    File.Delete("test-ef.db");
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}