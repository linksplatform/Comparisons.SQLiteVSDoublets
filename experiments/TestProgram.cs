using System;
using Comparisons.SQLiteVSDoublets.Experiments;

namespace Comparisons.SQLiteVSDoublets.TestApp
{
    class TestProgram
    {
        static void Main()
        {
            Console.WriteLine("Testing System.Data.SQLite integration...");
            TestSystemDataSQLite.RunTest();
            Console.WriteLine("Test completed.");
        }
    }
}