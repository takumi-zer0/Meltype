using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using ZeroVoice.Core;

namespace ZeroVoice.Windows;

internal static class Program
{
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    static AutomationElement? target;
    static TextPatternRange? selection;
    static IntPtr window;
    static int pid;
    static long started;
    static string snapshot = "", session = "";
    [STAThread]
    static int Main()
    {
        Console.InputEncoding = Encoding.UTF8; Console.OutputEncoding = new UTF8Encoding(false);
        while (Console.ReadLine() is { } line)
        {
            if (line.Length > 40000) return 1;
            HelperReply reply;
            try { var request = JsonSerializer.Deserialize<HelperRequest>(line, Json)!;
                reply = request.Operation switch { "capture" => Capture(), "paste" => Paste(request), _ => new(false, Reason:"未対応の操作です。") };
            } catch { reply = new(false, Reason:"入力欄を確認できません。コピーを利用してください。"); }
            Console.WriteLine(JsonSerializer.Serialize(reply, Json));
        }
        return 0;
    }
    static HelperReply Capture()
    {
        target = null; selection = null; session = "";
        window = GetForegroundWindow();
        GetWindowThreadProcessId(window, out var processId); pid = (int)processId;
        using var process = Process.GetProcessById(pid);
        var name = process.ProcessName;
        if (new[] { "WindowsTerminal", "powershell", "pwsh", "cmd", "conhost", "mintty", "Handy", "ZeroVoice" }.Contains(name, StringComparer.OrdinalIgnoreCase))
            return new(false, Reason:"ターミナルや音声入力アプリには貼り付けません。コピーを利用してください。");
        started = process.StartTime.ToUniversalTime().Ticks;
        var element = AutomationElement.FocusedElement;
        if (!Allowed(element) || element.Current.ProcessId != pid) return new(false, Reason:"安全に確認できる入力欄が見つかりません。コピーを利用してください。");
        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var raw)) return new(false, Reason:"この入力欄は文脈取得に未対応です。コピーを利用してください。");
        var ranges = ((TextPattern)raw).GetSelection();
        if (ranges.Length != 1) return new(false, Reason:"入力位置を特定できません。");
        target = element; selection = ranges[0].Clone(); snapshot = Context(ranges[0]); session = Guid.NewGuid().ToString("N");
        return new(true, new(true, name, snapshot, session, ""));
    }
    static bool Allowed(AutomationElement element) => !element.Current.IsPassword && element.Current.IsEnabled &&
        (element.Current.ControlType == ControlType.Edit || element.Current.ControlType == ControlType.Document);
    static string Context(TextPatternRange range)
    {
        var around = range.Clone();
        around.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -600);
        around.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 600);
        return around.GetText(Correction.MaxContext);
    }
    static HelperReply Paste(HelperRequest request)
    {
        if (target is null || selection is null || request.Token != session || string.IsNullOrEmpty(session)) return new(false, Reason:"入力先の有効期限が切れました。");
        session = ""; // One attempt only, including failures.
        if (request.Text.Length is < 1 or > Correction.MaxTranscript) return new(false, Reason:"入力文の長さが範囲外です。");
        using var process = Process.GetProcessById(pid);
        if (process.StartTime.ToUniversalTime().Ticks != started || !IsWindow(window)) return new(false, Reason:"元のアプリが終了しています。");
        if (new[] { 0x10, 0x11, 0x12, 0x5b, 0x5c }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0)) return new(false, Reason:"修飾キーを離してからコピーを利用してください。");
        if (!SetForegroundWindow(window)) return new(false, Reason:"元のアプリに戻れません。コピーを利用してください。");
        Thread.Sleep(150);
        if (GetForegroundWindow() != window) return new(false, Reason:"入力先が変わりました。");
        var focus = AutomationElement.FocusedElement;
        if (!Allowed(focus) || !Automation.Compare(target, focus)) return new(false, Reason:"入力欄が変わりました。コピーを利用してください。");
        if (!focus.TryGetCurrentPattern(TextPattern.Pattern, out var raw)) return new(false, Reason:"入力欄を確認できません。");
        var ranges = ((TextPattern)raw).GetSelection();
        if (ranges.Length != 1 || ranges[0].CompareEndpoints(TextPatternRangeEndpoint.Start, selection, TextPatternRangeEndpoint.Start) != 0 ||
            ranges[0].CompareEndpoints(TextPatternRangeEndpoint.End, selection, TextPatternRangeEndpoint.End) != 0 || Context(ranges[0]) != snapshot)
            return new(false, Reason:"入力位置または周辺の文章が変わりました。コピーを利用してください。");
        SetClipboard(request.Text);
        if (GetForegroundWindow() != window || !Automation.Compare(target, AutomationElement.FocusedElement)) return new(false, Reason:"入力先が変わったため、コピーだけ行いました。");
        var events = new[] { Key(0x11, 0), Key(0x56, 0), Key(0x56, 2), Key(0x11, 2) };
        var sent = SendInput((uint)events.Length, events, Marshal.SizeOf<INPUT>());
        if (sent != events.Length) { SendInput(1, [Key(0x11, 2)], Marshal.SizeOf<INPUT>()); return new(false, Reason:"貼り付けキーを送れませんでした。コピーした文を手動で貼り付けてください。"); }
        return new(true, Reason:"確認した文の貼り付け操作を送りました。クリップボードにも残っています。");
    }
    static void SetClipboard(string text)
    {
        var owner = CreateWindowEx(0, "STATIC", "ZeroVoiceClipboard", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (owner == IntPtr.Zero) throw new InvalidOperationException();
        if (!OpenClipboard(owner)) { DestroyWindow(owner); throw new InvalidOperationException(); }
        try { if (!EmptyClipboard()) throw new InvalidOperationException();
            SetClipboardBytes(13, Encoding.Unicode.GetBytes(text + "\0"));
            SetClipboardBytes(RegisterClipboardFormat("CanIncludeInClipboardHistory"), new byte[4]);
            SetClipboardBytes(RegisterClipboardFormat("CanUploadToCloudClipboard"), new byte[4]);
        } finally { CloseClipboard(); DestroyWindow(owner); }
    }
    static void SetClipboardBytes(uint format, byte[] bytes)
    {
        var handle = GlobalAlloc(0x42, (nuint)bytes.Length); if (handle == IntPtr.Zero) throw new OutOfMemoryException();
        var ptr = GlobalLock(handle); if (ptr == IntPtr.Zero) { GlobalFree(handle); throw new InvalidOperationException(); }
        Marshal.Copy(bytes, 0, ptr, bytes.Length); GlobalUnlock(handle);
        if (SetClipboardData(format, handle) == IntPtr.Zero) { GlobalFree(handle); throw new InvalidOperationException(); }
    }
    static INPUT Key(ushort key, uint flags) => new() { type = 1, data = new INPUTUNION { keyboard = new KEYBDINPUT { wVk = key, dwFlags = flags } } };
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION data; }
    [StructLayout(LayoutKind.Explicit)] struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT keyboard; [FieldOffset(0)] public MOUSEINPUT mouse; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, flags, time; public UIntPtr extra; }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint id);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int k);
    [DllImport("user32.dll")] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr h);
    [DllImport("user32.dll")] static extern bool EmptyClipboard();
    [DllImport("user32.dll")] static extern bool CloseClipboard();
    [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint format, IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern uint RegisterClipboardFormat(string format);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint flags, nuint size);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr h);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr h);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr CreateWindowEx(uint ex, string cls, string name, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
}
