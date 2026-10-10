using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using ZeroVoice.Desktop;

AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing=false }).SetupWithoutStarting();
ZeroVoice.Desktop.Program.Transcript="えーと、クロードコードでギットハブのプルリクを確認してください。";
var window = new ReviewWindow(); window.Show(); Dispatcher.UIThread.RunJobs();
var fields=window.GetLogicalDescendants().OfType<TextBox>().ToArray();
var checkbox=window.GetLogicalDescendants().OfType<CheckBox>().Single(c=>c.Content?.ToString()?.StartsWith("入力する文の意味") == true);
var copy=window.GetLogicalDescendants().OfType<Button>().Single(c=>c.Content?.ToString()=="確認した文をコピー");
var paste=window.GetLogicalDescendants().OfType<Button>().Single(c=>c.Content?.ToString()=="元の入力欄へ貼り付け");
var checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
Check(!copy.IsEnabled && !paste.IsEnabled,"画面の初期状態で入力操作は無効");
checkbox.IsChecked=true; Check(copy.IsEnabled,"確認するとコピーを有効化");
Check(!paste.IsEnabled,"入力先がなければ貼り付けは有効にしない");
fields[1].Text="Claude CodeでGitHubのプルリクを確認してください。";
Check(checkbox.IsChecked!=true && !copy.IsEnabled,"文を編集したら確認を解除");
checkbox.IsChecked=true; fields[2].Text="ソフトウェア開発の依頼文です。";
Check(!copy.IsEnabled,"文脈の変更で確認を解除");
fields[0].Text="自動更新は有効にしないでください。";
Check(fields[1].Text==fields[0].Text && !copy.IsEnabled,"原文変更時に古い補正案を使い回さない");
fields[0].Text="えーと、クロードコードでギットハブのプルリクを確認してください。";
fields[1].Text="Claude CodeでGitHubのプルリクを確認してください。";
Dispatcher.UIThread.RunJobs();
AvaloniaHeadlessPlatform.ForceRenderTimerTick();
if(args.Length==1) {using var frame=window.CaptureRenderedFrame();if(frame is null)throw new Exception("Frame missing");frame.Save(args[0],Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
window.Close(); Console.WriteLine($"{checks} UI checks passed");
