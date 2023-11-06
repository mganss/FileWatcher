using FileWatcher.TestCommand;
using System.Collections;
using System.Text.Json;

if (args.Length > 1 && args[0] == "delay")
    Thread.Sleep(TimeSpan.FromSeconds(int.Parse(args[1])));

var info = new CommandInfo
{
    WorkingDirectory = Environment.CurrentDirectory,
    Arguments = args.ToList(),
    Environment = Environment.GetEnvironmentVariables()
        .OfType<DictionaryEntry>()
        .Where(e => e.Key.ToString()?.StartsWith("FileWatcher") == true && e.Value != null)
        .ToDictionary(e => e.Key.ToString()!, e => e.Value!.ToString()!)
};

var json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });

File.WriteAllText(Path.Combine(AppContext.BaseDirectory, $"test.{DateTime.UtcNow.Ticks}.json"), json);

Console.Out.WriteLine("Out");
Console.Error.WriteLine("Error");