using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Input.Platform;
using ZeroVoice.Core;

namespace ZeroVoice.Desktop;

internal sealed class ReviewWindow : Window
{
    readonly TextBox original = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 210, MaxLength = Correction.MaxTranscript, PlaceholderText = "Handyで話すか、校正したい日本語を入力してください。" };
    readonly TextBox candidate = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 210, MaxLength = Correction.MaxTranscript };
    readonly TextBox context = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 85, MaxLength = Correction.MaxContext, PlaceholderText = "関連する文章を必要な範囲で追加できます（最大1200文字）。" };
    readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray };
    readonly CheckBox reviewed = new() { Content = "入力する文の意味・固有名詞・数値を確認しました" };
    readonly CheckBox useContext = new() { Content = "表示した文脈を補正に使う" };
    readonly Button correct = new() { Content = "ローカルで補正", Padding = new Thickness(20, 10) };
    readonly Button paste = new() { Content = "元の入力欄へ貼り付け", IsEnabled = false, Padding = new Thickness(20, 10) };
    readonly Button copy = new() { Content = "確認した文をコピー", IsEnabled = false, Padding = new Thickness(20, 10) };
    readonly Button cancel = new() { Content = "補正を中止", IsEnabled = false };
    ReviewSession session;
    CancellationTokenSource? active;
    bool updating, consumed;
    readonly LocalModel? model;

    public ReviewWindow()
    {
        Title = "ZeroVoice — 日本語を確認して入力"; Width = 960; Height = 800; MinWidth = 720; MinHeight = 640;
        Background = new SolidColorBrush(Color.Parse("#F5F6F8"));
        session = new(Program.Transcript); original.Text = Program.Transcript; candidate.Text = Program.Transcript;
        try { model = Program.LoadModel(); } catch { model = null; }
        var target = Program.Native.Target;
        context.Text = target.Context; useContext.IsChecked = target.Available && target.Context.Length > 0;
        var header = new TextBlock { Text = "話したことを、その意味のまま。", FontSize = 25, FontWeight = FontWeight.SemiBold };
        var subhead = new TextBlock { Text = "PC内で補正 → 内容を確認 → コピーまたは貼り付け", Foreground = Brushes.DimGray };
        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,20,*") };
        var left = new StackPanel { Spacing = 8 }; left.Children.Add(new TextBlock { Text = "認識した文", FontWeight = FontWeight.SemiBold }); left.Children.Add(original);
        var right = new StackPanel { Spacing = 8 }; right.Children.Add(new TextBlock { Text = "入力する文（編集できます）", FontWeight = FontWeight.SemiBold }); right.Children.Add(candidate);
        columns.Children.Add(left); Grid.SetColumn(right, 2); columns.Children.Add(right);
        var contextBlock = new StackPanel { Spacing = 8 };
        contextBlock.Children.Add(new TextBlock { Text = target.Available ? "文脈・入力先: " + target.App : "文脈（任意）", FontWeight = FontWeight.SemiBold });
        contextBlock.Children.Add(context); contextBlock.Children.Add(useContext);
        contextBlock.Children.Add(new TextBlock { Text = target.Available ? "貼り付け直前にも、入力欄と周辺の文章が変わっていないか確認します。" : target.Reason, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12 });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        actions.Children.Add(correct); actions.Children.Add(cancel);
        var apply = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        apply.Children.Add(copy); apply.Children.Add(paste);
        var panel = new StackPanel { Margin = new Thickness(28), Spacing = 16 };
        foreach (var item in new Control[] { header, subhead, columns, contextBlock, actions, status, reviewed, apply,
            new TextBlock { Text = "補正後も意味が正しいか確認してください。送信キーは押しません。コピーした文はクリップボードに残ります。", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.DimGray } }) panel.Children.Add(item);
        Content = new ScrollViewer { Content = panel };
        status.Text = model is null ? "モデルが未設定です。原文を編集・確認してコピーできます。" : "ローカル処理 · 文章の履歴は保存しません";
        candidate.PropertyChanged += (_, change) => { if (updating || change.Property != TextBox.TextProperty) return; session.Edit(candidate.Text ?? ""); reviewed.IsChecked = false; Refresh(); };
        original.PropertyChanged += (_, change) => { if (updating || change.Property != TextBox.TextProperty) return; session = new(original.Text ?? ""); SetCandidate(original.Text ?? ""); status.Text = "原文を変更しました。補正を実行できます。"; };
        context.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) InvalidateReview(); }; useContext.IsCheckedChanged += (_, _) => InvalidateReview();
        reviewed.IsCheckedChanged += (_, _) => { if (reviewed.IsChecked == true) session.Approve(); else session.Edit(candidate.Text ?? ""); Refresh(); };
        correct.Click += async (_, _) => await CorrectAsync(); cancel.Click += (_, _) => active?.Cancel();
        copy.Click += async (_, _) => await ApplyAsync(false); paste.Click += async (_, _) => await ApplyAsync(true);
        Closed += (_, _) => { active?.Cancel(); Program.Native.Dispose(); };
        Opened += async (_, _) => { if (!Program.Demo && Program.Transcript.Length > 0 && model is not null) await CorrectAsync(); };
        Refresh();
    }
    void InvalidateReview() { if (!updating) { reviewed.IsChecked = false; session.Edit(candidate.Text ?? ""); Refresh(); } }
    void SetCandidate(string text)
    {
        updating = true; candidate.Text = text; updating = false; session.Edit(text); reviewed.IsChecked = false; Refresh();
    }
    void Refresh()
    {
        var idle = active is null && !consumed;
        original.IsEnabled = idle; candidate.IsEnabled = idle; context.IsEnabled = idle; useContext.IsEnabled = idle;
        reviewed.IsEnabled = idle && !string.IsNullOrWhiteSpace(candidate.Text);
        correct.IsEnabled = idle && model is not null && !string.IsNullOrWhiteSpace(original.Text);
        copy.IsEnabled = idle && session.CanApply;
        paste.IsEnabled = idle && session.CanApply && Program.Native.Target.Available;
        cancel.IsEnabled = active is not null;
    }
    async Task CorrectAsync()
    {
        if (active is not null || model is null) return;
        reviewed.IsChecked = false; session.Edit(candidate.Text ?? "");
        active = new(); Refresh(); status.Text = "ローカルモデルで補正しています…";
        try
        {
            var result = await model.CorrectAsync(new(original.Text ?? "", useContext.IsChecked == true ? context.Text ?? "" : "", Correction.DefaultTerms), active.Token);
            SetCandidate(result.Text);
            status.Text = $"{result.ElapsedMs / 1000.0:F1}秒 · " + (result.Warnings.Length == 0 ? "変更なし。内容を確認してください。" : string.Join(" ", result.Warnings));
        }
        catch (ArgumentException ex) { status.Text = ex.Message; }
        catch { status.Text = "補正を実行できませんでした。原文を確認してください。"; SetCandidate(original.Text ?? ""); }
        finally { active.Dispose(); active = null; Refresh(); }
    }
    async Task ApplyAsync(bool toTarget)
    {
        if (!session.CanApply || active is not null || consumed) return;
        // Disable both actions before awaiting. A second click cannot enqueue another paste.
        var text = session.Consume(); consumed = true; Refresh();
        if (toTarget)
        {
            var reply = await Program.Native.PasteAsync(text);
            status.Text = reply.Reason;
            if (reply.Ok) { await Task.Delay(400); Close(); }
            else { consumed = false; session = new(original.Text ?? ""); session.Edit(text); reviewed.IsChecked = false; Refresh(); Activate(); }
        }
        else
        {
            try { if (Clipboard is null) throw new InvalidOperationException(); await Clipboard.SetTextAsync(text); status.Text = "確認した文をコピーしました。入力先で貼り付けてください。"; }
            catch { status.Text = "コピーできませんでした。"; }
            consumed = false; session = new(original.Text ?? ""); session.Edit(text); reviewed.IsChecked = false; Refresh();
        }
    }
}
