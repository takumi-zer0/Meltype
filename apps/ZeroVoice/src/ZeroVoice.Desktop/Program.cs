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
