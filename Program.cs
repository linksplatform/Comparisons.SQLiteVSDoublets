using Comparisons.SQLiteVSDoublets.SQLite;
using Comparisons.SQLiteVSDoublets.Doublets;

namespace Comparisons.SQLiteVSDoublets
{
    class Program
    {
        static void Main()
        {
            new SQLiteTestRun("test.db").Run();
            new DoubletsTestRun("test.links").Run();
        }
    }
}
