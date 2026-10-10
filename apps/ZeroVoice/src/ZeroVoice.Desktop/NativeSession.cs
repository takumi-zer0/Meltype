using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ZeroVoice.Core;

namespace ZeroVoice.Desktop;

internal sealed class NativeSession : IDisposable
{
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    Process? process;
    public TargetInfo Target { get; private set; } = new(false, "", "", "", "入力先を取得していません。コピーは利用できます。");
    public async Task CaptureAsync(string root)
    {
        try
        {
            var helper = Path.Combine(root, "native", OperatingSystem.IsWindows() ? "ZeroVoice.Windows.exe" : "ZeroVoice.Mac");
            var checksumFile = helper + ".sha256";
            using var helperStream = File.OpenRead(helper);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(helperStream));
            if (hash != (await File.ReadAllTextAsync(checksumFile)).Trim().ToUpperInvariant()) return;
            process = Process.Start(new ProcessStartInfo(helper) {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(helper)!,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
            });
            var reply = await SendAsync(new("capture"));
            Target = reply.Target ?? new(false, "", "", "", reply.Reason);
        }
        catch { Dispose(); }
    }
    public async Task<HelperReply> PasteAsync(string text) => await SendAsync(new("paste", Target.Token, text));
    async Task<HelperReply> SendAsync(HelperRequest request)
    {
        if (process is null || process.HasExited) return new(false, Reason:"入力先との接続が切れました。コピーを利用してください。");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        try
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, Json).AsMemory(), timeout.Token);
            await process.StandardInput.FlushAsync(timeout.Token);
            var response = await process.StandardOutput.ReadLineAsync(timeout.Token);
            if (response is null || response.Length > 16000) throw new InvalidDataException();
            return JsonSerializer.Deserialize<HelperReply>(response, Json) ?? new(false);
        }
        catch { Dispose(); return new(false, Reason:"入力先を時間内に確認できませんでした。コピーを利用してください。"); }
    }
    public void Dispose()
    {
        if (process is null) return;
        try { if (!process.HasExited) process.Kill(entireProcessTree:true); } catch { }
        process.Dispose(); process = null;
    }
}
