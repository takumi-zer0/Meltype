using System.Text.Json;
using ZeroVoice.Core;

namespace ZeroVoice.Desktop;

internal static class Benchmark
{
    // Only explicit synthetic test cases are written; the normal UI never persists text.
    public static async Task<int> Run(string[] args)
    {
        if (args.Length != 2) return 2;
        var model = Program.LoadModel(Console.Error.WriteLine); if (model is null) return 3;
        var cases = JsonSerializer.Deserialize<BenchCase[]>(await File.ReadAllTextAsync(args[0]), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var results = new List<object>();
        foreach (var item in cases)
        {
            var result = await model.CorrectAsync(new(item.Text, item.Context, Correction.DefaultTerms));
            results.Add(new { item.Id, result.Original, result.Text, result.Rejected, result.Warnings, result.ElapsedMs });
            Console.WriteLine($"{item.Id}: {result.ElapsedMs}ms rejected={result.Rejected}");
        }
        await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return 0;
    }
    sealed record BenchCase(string Id, string Text, string Context);
}
