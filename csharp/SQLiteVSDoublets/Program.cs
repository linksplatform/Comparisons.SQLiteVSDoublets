// Usage: sqlite-vs-doublets <links|objects> <32|64> <size> [--work N | --repetitions N]
//        [--variants A,B] [--directory DIR] [--output FILE]
//
// Repetitions default to `work / size` clamped to 1..=MaxRepetitions, so smaller sizes get more,
// but not endless, repetitions.

using System.Text.Json;
using System.Text.Json.Nodes;
using Comparisons.SQLiteVSDoublets;
using Microsoft.Data.Sqlite;

const ulong DefaultWork = 3_000_000;
const ulong MaxRepetitions = 10;

var (category, bits, sizeText) = (args[0], args[1], args[2]);
var options = args[3..].Chunk(2).ToDictionary(pair => pair[0].TrimStart('-'), pair => pair[1]);
static ulong Number(string text) => ulong.Parse(text.Replace("_", ""));
var size = Number(sizeText);
var work = options.TryGetValue("work", out var workText) ? Number(workText) : DefaultWork;
var repetitions = options.TryGetValue("repetitions", out var count)
    ? int.Parse(count)
    : (int)Math.Clamp(work / size, 1, MaxRepetitions);
var allVariants = category == "links" ? Harness.LinksVariants : Harness.ObjectsVariants;
var variants = options.TryGetValue("variants", out var names)
    ? names.Split(',').Select(name => allVariants.Contains(name) ? name : throw new ArgumentException(name)).ToArray()
    : allVariants;
var directory = options.GetValueOrDefault("directory") ?? Path.GetTempPath();

var measurements = variants.Select(variant => (category, bits) switch
{
    ("links", "32") => Harness.MeasureLinks<uint>(variant, size, repetitions, directory),
    ("links", "64") => Harness.MeasureLinks<ulong>(variant, size, repetitions, directory),
    ("objects", "32") => Harness.MeasureObjects<uint>(variant, size, repetitions, directory),
    ("objects", "64") => Harness.MeasureObjects<ulong>(variant, size, repetitions, directory),
    _ => throw new ArgumentException("category must be links or objects and bits must be 32 or 64"),
}).ToList();

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();
using var version = connection.CreateCommand();
version.CommandText = "SELECT sqlite_version()";
var report = new JsonObject
{
    ["language"] = "C#",
    ["category"] = category,
    ["bits"] = int.Parse(bits),
    ["size"] = size,
    ["repetitions"] = repetitions,
    ["warm_up_size"] = Math.Min(size, Harness.WarmUpSize),
    ["sqlite_version"] = (string)version.ExecuteScalar()!,
    ["results"] = new JsonArray(measurements.Select(measurement => (JsonNode)measurement.ToJson()).ToArray()),
};
var json = report.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
if (options.TryGetValue("output", out var output))
{
    File.WriteAllText(output, json);
}
else
{
    Console.WriteLine(json);
}
