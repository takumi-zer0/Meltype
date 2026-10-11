using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ZeroVoice.Core;

// Explicit synthetic-case benchmark only. Never reads Handy history, audio, or clipboard.
if (args.Length is < 5 or > 6 || args[4] is not ("none" or "low")) return 2;
var json = new JsonSerializerOptions { PropertyNameCaseInsensitive=true, PropertyNamingPolicy=JsonNamingPolicy.CamelCase, Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var cases = JsonSerializer.Deserialize<Case[]>(File.ReadAllText(args[0]),json) ?? throw new InvalidDataException();
if(args.Length==6) cases=cases.Take(int.Parse(args[5])).ToArray();
if(cases.Length is < 1 or > 100) return 2;
var instructions=File.ReadAllText(args[3]);
var key=File.ReadLines(args[2]).Where(s=>s.StartsWith("OPENAI_API_KEY=",StringComparison.Ordinal)).Select(s=>s[15..].Trim().Trim('"','\'')).Single();
if(!key.StartsWith("sk-",StringComparison.Ordinal) || key.Length<30) throw new InvalidDataException("API key unavailable");
using var handler = new SocketsHttpHandler { AllowAutoRedirect=false, AutomaticDecompression=DecompressionMethods.All };
using var client=new HttpClient(handler) { Timeout=TimeSpan.FromSeconds(30), MaxResponseContentBufferSize=65536 };
client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",key);
var results=new List<object>();
string? failure=null;
foreach(var item in cases)
{
    // Keep evaluation traffic below low project limits. Excluded from per-request timing.
    if(results.Count>0) await Task.Delay(TimeSpan.FromSeconds(5));
    var request=new CorrectionRequest(item.Text,item.Context,Correction.DefaultTerms);
    var user=Correction.BuildInput(ContextTerms.Prepare(request));
    var timer=Stopwatch.StartNew();
    try
    {
        var payload=new {
            model="gpt-6-luna", store=false, reasoning=new{effort=args[4]}, max_output_tokens=args[4]=="none"?512:1024,
            input=new[]{new{role="system",content=instructions},new{role="user",content=user}},
            text=new{format=new{type="json_schema",name="correction",strict=true,schema=new{type="object",properties=new{text=new{type="string"}},required=new[]{"text"},additionalProperties=false}}}
        };
        using var body=JsonContent.Create(payload,options:json);
        using var response=await client.PostAsync("https://api.openai.com/v1/responses",body);
        var bytes=await response.Content.ReadAsByteArrayAsync();
        if(bytes.Length>65536) throw new InvalidDataException("Response too large");
        using var doc=JsonDocument.Parse(bytes);
        var value=doc.RootElement;
        if(!response.IsSuccessStatusCode)
        {
            var code=value.TryGetProperty("error",out var error)&&error.TryGetProperty("code",out var c)?c.GetString():null;
            failure=$"HTTP {(int)response.StatusCode}: "+(code is not null && Regex.IsMatch(code,"^[a-zA-Z0-9_]{1,80}$")?code:"api_error");
            Console.Error.WriteLine(failure);
            foreach(var header in new[]{"retry-after","x-ratelimit-limit-requests","x-ratelimit-limit-tokens","x-ratelimit-reset-requests","x-ratelimit-reset-tokens"})
                if(response.Headers.TryGetValues(header,out var values))
                    foreach(var limit in values.Where(v=>Regex.IsMatch(v,"^[0-9a-zA-Z. -]{1,100}$"))) Console.Error.WriteLine($"{header}: {limit}");
            break;
        }
        var raw=string.Concat(value.GetProperty("output").EnumerateArray().Where(o=>o.GetProperty("type").GetString()=="message")
            .SelectMany(o=>o.GetProperty("content").EnumerateArray()).Where(c=>c.GetProperty("type").GetString()=="output_text").Select(c=>c.GetProperty("text").GetString()));
        timer.Stop();
        var result=value.GetProperty("status").GetString()=="completed" ? Correction.Parse(request,raw,timer.ElapsedMilliseconds) : Correction.Reject(item.Text,"Incomplete API response",timer.ElapsedMilliseconds);
        var usage=value.GetProperty("usage").Clone();
        results.Add(new{item.Id,original=item.Text,result.Text,result.Rejected,result.Warnings,elapsedMs=timer.ElapsedMilliseconds,rawOutput=raw,usage});
        Console.WriteLine($"{item.Id}: {timer.ElapsedMilliseconds}ms rejected={result.Rejected}");
    }
    catch(Exception error) when(error is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
    { failure=error.GetType().Name;Console.Error.WriteLine(failure);break; }
}
File.WriteAllText(args[1],JsonSerializer.Serialize(new{model="gpt-6-luna",effort=args[4],store=false,promptSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(instructions))),results,failure},json));
return failure is null?0:1;
record Case(string Id,string Text,string Context);
