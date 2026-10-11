using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ZeroVoice.Core;
using ZeroVoice.Worker;

Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
var json = new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive=true, Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var root = AppContext.BaseDirectory;
try
{
    var config = JsonSerializer.Deserialize<WorkerConfig>(await File.ReadAllTextAsync(Path.Combine(root,"worker-model.json")),json) ?? throw new InvalidDataException();
    using var model = new ResidentModel(root,config);
    if (args.Length == 3 && args[0] == "--benchmark")
    {
        var cases = JsonSerializer.Deserialize<BenchCase[]>(await File.ReadAllTextAsync(args[1]),json) ?? throw new InvalidDataException();
        var results = new List<object>();
        Console.Error.WriteLine($"Verified and loaded in {model.LoadMs}ms");
        foreach(var item in cases)
        {
            var timer=Stopwatch.StartNew();
            var result=model.Correct(new(item.Text,item.Context,Correction.DefaultTerms));
            results.Add(new {item.Id,result.Original,result.Text,result.Rejected,result.Warnings,ElapsedMs=timer.ElapsedMilliseconds});
            Console.Error.WriteLine($"{item.Id}: {timer.ElapsedMilliseconds}ms rejected={result.Rejected}");
        }
        await File.WriteAllTextAsync(args[2],JsonSerializer.Serialize(new{model.LoadMs,results},json));
        return 0;
    }
    if (args.Length != 1 || args[0] != "--stdio") return 2;
    Console.WriteLine(JsonSerializer.Serialize(new{ready=true,loadMs=model.LoadMs},json));
    while (await ReadBoundedLine(Console.In, 32000) is { } line)
    {
        try
        {
            var request=JsonSerializer.Deserialize<Input>(line,json) ?? throw new InvalidDataException();
            if(request.Operation=="quit") break;
            if(request.Operation!="correct" || request.Text is null || request.Context is null) throw new InvalidDataException();
            var result=model.Correct(new(request.Text,request.Context,Correction.DefaultTerms));
            Console.WriteLine(JsonSerializer.Serialize(new{request.Id,result},json));
        }
        catch (Exception e) when(e is ArgumentException or JsonException or InvalidDataException or InvalidOperationException)
        { Console.WriteLine("{\"error\":\"Correction unavailable\"}"); }
    }
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(args.Length > 0 && args[0] == "--benchmark" ? error.ToString() : "Local correction worker unavailable."); return 1; }

static async Task<string?> ReadBoundedLine(TextReader reader,int limit)
{
    var line=new StringBuilder(); var character=new char[1];
    while(await reader.ReadAsync(character)==1)
    { if(character[0]=='\n') return line.ToString(); if(line.Length>=limit) throw new InvalidDataException(); if(character[0]!='\r') line.Append(character[0]); }
    return line.Length==0 ? null : throw new InvalidDataException();
}
record Input(long Id,string Operation,string? Text,string? Context);
record BenchCase(string Id,string Text,string Context);
