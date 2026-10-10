using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZeroVoice.Core;

public sealed record RuntimeFile(string Path, string Sha256);
public sealed record LocalModelConfig(string Executable, string Model, string ModelSha256, RuntimeFile[] Files, int GpuLayers = 99);

public sealed class LocalModel(string baseDirectory, LocalModelConfig config, Action<string>? diagnostic = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<CorrectionResult> CorrectAsync(CorrectionRequest request, CancellationToken cancellation = default)
    {
        Correction.ValidateRequest(request);
        if (!await gate.WaitAsync(0, cancellation)) throw new InvalidOperationException("補正は1件ずつ実行してください。");
        var timer = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        Process? process = null;
        var locks = new List<FileStream>();
        try
        {
            // Keep validated files open without write/delete sharing throughout execution.
            foreach (var file in config.Files)
                locks.Add(await VerifyAsync(Resolve(file.Path), file.Sha256, token));
            var exe = Resolve(config.Executable);
            if (!config.Files.Any(f => Resolve(f.Path) == exe)) throw new InvalidDataException("推論実行ファイルの検証情報がありません。");
            var model = Resolve(config.Model);
            locks.Add(await VerifyAsync(model, config.ModelSha256, token));
            var input = Correction.BuildInput(ContextTerms.Prepare(request));
            var start = new ProcessStartInfo(exe) {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe)!,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            // Do not inherit model RPC/download settings, credentials or Vulkan overlay injection settings.
            start.Environment.Clear();
            foreach (var key in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "HOME", "TMPDIR" })
                if (Environment.GetEnvironmentVariable(key) is { } value) start.Environment[key] = value;
            start.Environment["PATH"] = OperatingSystem.IsWindows() ? Environment.SystemDirectory : "/usr/bin:/bin";
            start.Environment["VK_LOADER_LAYERS_DISABLE"] = "~implicit~";
            foreach (var arg in new[] { "-m", model, "-p", Correction.PromptPrefix, "--in-suffix", Correction.PromptSuffix,
                "--no-conversation", "--interactive-first", "--single-turn", "--no-display-prompt", "--simple-io", "--no-escape", "--log-verbosity", "0", "--offline",
                "-c", "4096", "-n", "512", "--temp", "0", "--seed", "42", "-ngl", config.GpuLayers.ToString(),
                "--json-schema", "{\"type\":\"object\",\"properties\":{\"text\":{\"type\":\"string\"}},\"required\":[\"text\"],\"additionalProperties\":false}" })
                start.ArgumentList.Add(arg);
            process = Process.Start(start) ?? throw new InvalidOperationException("推論プロセスを起動できません。");
            using var abortProcess = token.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree:true); } catch { } });
            var outputTask = ReadBoundedAsync(process.StandardOutput, 65536, token);
            var errorTask = ReadBoundedAsync(process.StandardError, 65536, token);
            // An inherited anonymous pipe is connected to this exact child. No discoverable endpoint exists.
            await process.StandardInput.WriteLineAsync(input.AsMemory(), token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(token);
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0) diagnostic?.Invoke(error); // Only the explicit benchmark supplies this callback.
            if (process.ExitCode != 0) return Correction.Reject(request.Transcript, "ローカルモデルを実行できませんでした。原文を確認できます。", timer.ElapsedMilliseconds);
            var result = Correction.Parse(request, Unframe(output), timer.ElapsedMilliseconds);
            if (result.Rejected) diagnostic?.Invoke("Rejected benchmark output: " + JsonSerializer.Serialize(output));
            return result;
        }
        catch (OperationCanceledException) { return Correction.Reject(request.Transcript, "補正を中止しました。原文を残しました。", timer.ElapsedMilliseconds); }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        { diagnostic?.Invoke(e.GetType().Name + ": " + e.Message); return Correction.Reject(request.Transcript, "モデルまたは実行ファイルの検証・実行に失敗しました。原文を残しました。", timer.ElapsedMilliseconds); }
        finally
        {
            if (process is not null) { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } process.Dispose(); }
            foreach (var file in locks) file.Dispose();
            gate.Release();
        }
    }
    private string Resolve(string path)
    {
        if (Path.IsPathRooted(path)) throw new InvalidDataException("配布フォルダー内の相対パスだけを使用します。");
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory)) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("配布フォルダー外は使用できません。");
        return full;
    }
    private static async Task<FileStream> VerifyAsync(string path, string expected, CancellationToken token)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        try { var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
            if (!hash.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ファイルのSHA-256が一致しません。");
            return stream;
        } catch { stream.Dispose(); throw; }
    }
    public static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken token)
    {
        var result = new StringBuilder(); var buffer = new char[4096];
        while (true) { var count = await reader.ReadAsync(buffer.AsMemory(), token); if (count == 0) return result.ToString();
            if (result.Length + count > limit) throw new InvalidDataException("応答サイズの上限を超えました。"); result.Append(buffer, 0, count); }
    }
    public static string Unframe(string output)
    {
        // Remove only the pinned completion tool's exact framing, not arbitrary model explanations.
        output = output.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var prefix = Correction.PromptSuffix.Trim();
        if (output.StartsWith(prefix, StringComparison.Ordinal)) output = output[prefix.Length..].TrimStart();
        foreach (var trailer in new[] { "\nEOF by user", "[end of text]" })
            if (output.EndsWith(trailer, StringComparison.Ordinal)) output = output[..^trailer.Length].TrimEnd();
        return output;
    }
}
