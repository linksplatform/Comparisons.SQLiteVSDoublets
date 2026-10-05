using System.Data.SQLite;

using var connection = new SQLiteConnection("Data Source=:memory:;Pooling=False");
connection.Open();
using var command = connection.CreateCommand();
command.CommandText = "SELECT sqlite_version()";
Console.WriteLine($"System.Data.SQLite {typeof(SQLiteConnection).Assembly.GetName().Version}: SQLite {command.ExecuteScalar()}");
