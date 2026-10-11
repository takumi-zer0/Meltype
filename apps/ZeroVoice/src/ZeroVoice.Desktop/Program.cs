using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using System.Text.Json;
using ZeroVoice.Core;

namespace ZeroVoice.Desktop;

internal static class Program
{
    public static string Transcript = "";
    public static NativeSession Native = new();
    public static readonly string Root = AppContext.BaseDirectory;
    public static bool Demo;
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--prepare-cache") return PrepareCache().GetAwaiter().GetResult();
        if (args.Length == 3 && args[0] == "--benchmark") return Benchmark.Run(args.Skip(1).ToArray()).GetAwaiter().GetResult();
        if (args.Length == 2 && args[0] is "--connect-handy" or "--disconnect-handy")
        {
            try { HandyIntegration.ApplyFile(args[1],Environment.ProcessPath!,args[0]=="--disconnect-handy"); Console.WriteLine("Handy settings updated."); return 0; }
            catch(Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
        }
        Demo = args.Length == 1 && args[0] == "--demo";
        Transcript = Demo ? "えーと、クロードコードでギットハブのプルリクを確認してください。" : args.Length == 1 ? args[0] : "";
        if (Transcript.Length > Correction.MaxTranscript) return 2;
        // The helper captures before this window takes focus. Demo never inspects another application.
        if (!Demo && args.Length == 1) Native.CaptureAsync(Root).GetAwaiter().GetResult();
        try { BuildApp().StartWithClassicDesktopLifetime([]); return 0; }
        finally { Native.Dispose(); }
    }
    public static AppBuilder BuildApp() => AppBuilder.Configure<App>().UsePlatformDetect();
    static async Task<int> PrepareCache()
    {
        try
        {
            var file = Path.Combine(Root, "model.json");
            var json = await File.ReadAllTextAsync(file);
            var config = JsonSerializer.Deserialize<LocalModelConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("モデル設定がありません。");
            var cache = await new LocalModel(Root, config).PrepareCacheAsync();
            // Setup publishes a complete manifest last. A failed preparation leaves the old configuration intact.
            if (await File.ReadAllTextAsync(file) != json) throw new InvalidOperationException("設定が変更されたため保存しません。");
            var temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(config with { PrefixCache = cache }, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, file, overwrite: true);
            Console.WriteLine("固定の校正指示を事前計算しました。利用者の文章は保存していません。");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or OperationCanceledException or JsonException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { Console.Error.WriteLine("事前計算に失敗しました。既存の設定は維持されます。"); return 1; }
    }
    public static LocalModel? LoadModel(Action<string>? diagnostic = null)
    {
        var file = Path.Combine(Root, "model.json");
        if (!File.Exists(file)) return null;
        var config = JsonSerializer.Deserialize<LocalModelConfig>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return config is null ? null : new LocalModel(Root, config, diagnostic);
    }
}
internal sealed class App : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new ReviewWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
