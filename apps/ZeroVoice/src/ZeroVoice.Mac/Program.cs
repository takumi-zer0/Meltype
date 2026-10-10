using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ZeroVoice.Core;

namespace ZeroVoice.Mac;

internal static class Program
{
    const string AX = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    const string CG = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    const string ObjC = "/usr/lib/libobjc.A.dylib";
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive=true, PropertyNamingPolicy=JsonNamingPolicy.CamelCase };
    static IntPtr system, target, app;
    static CFRange selected;
    static string context = "", session = "";
    static int pid;
    static long started;
    static int Main()
    {
        if (!OperatingSystem.IsMacOS()) return 2;
        NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit");
        system = AXUIElementCreateSystemWide();
        Console.InputEncoding = Encoding.UTF8; Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            while (Console.ReadLine() is { } line)
            {
                if (line.Length > 40000) return 1;
                HelperReply reply;
                try { var r = JsonSerializer.Deserialize<HelperRequest>(line, Json)!; reply = r.Operation switch {
                    "capture" => Capture(), "paste" => Paste(r), _ => new(false, Reason:"未対応の操作です。") }; }
                catch { reply = new(false, Reason:"入力欄を確認できません。コピーを利用してください。"); }
                Console.WriteLine(JsonSerializer.Serialize(reply, Json));
            }
        }
        finally { Release(target); Release(app); Release(system); }
        return 0;
    }
    static HelperReply Capture()
    {
        Release(target); Release(app); target = app = IntPtr.Zero; session = "";
        if (!AXIsProcessTrusted()) return new(false, Reason:"アクセシビリティ権限が未許可です。確認・コピーは利用できます。");
        app = Attribute(system, "AXFocusedApplication"); target = Attribute(system, "AXFocusedUIElement");
        if (app == IntPtr.Zero || target == IntPtr.Zero || !Allowed(target) || AXUIElementGetPid(target, out pid) != 0)
            return new(false, Reason:"この入力欄は文脈取得に対応していません。コピーを利用してください。");
        using var process = Process.GetProcessById(pid); var name = process.ProcessName;
        if (new[] {"Terminal","iTerm2","kitty","alacritty","wezterm-gui","Handy","ZeroVoice"}.Contains(name, StringComparer.OrdinalIgnoreCase))
            return new(false, Reason:"ターミナルや音声入力アプリへは貼り付けません。");
        started = process.StartTime.ToUniversalTime().Ticks;
        if (!Selection(target, out selected) || ReadContext(target, selected) is not { } surrounding)
            return new(false, Reason:"カーソル位置と周辺文を確認できません。コピーを利用してください。");
        context = surrounding; session = Guid.NewGuid().ToString("N");
        return new(true, new(true, name, context, session, ""));
    }
    static HelperReply Paste(HelperRequest request)
    {
        if (string.IsNullOrEmpty(session) || request.Token != session || target == IntPtr.Zero || app == IntPtr.Zero)
            return new(false, Reason:"入力先の有効期限が切れました。");
        session = "";
        if (request.Text.Length is < 1 or > Correction.MaxTranscript) return new(false, Reason:"入力文の長さが範囲外です。");
        using var process = Process.GetProcessById(pid);
        if (process.StartTime.ToUniversalTime().Ticks != started) return new(false, Reason:"元のアプリが終了しています。");
        if ((CGEventSourceFlagsState(0) & ((1UL<<17)|(1UL<<18)|(1UL<<19)|(1UL<<20))) != 0)
            return new(false, Reason:"修飾キーを離してからコピーを利用してください。");
        var front = String("AXFrontmost");
        var library = NativeLibrary.Load(CF);
        var trueValue = Marshal.ReadIntPtr(NativeLibrary.GetExport(library, "kCFBooleanTrue"));
        var result = AXUIElementSetAttributeValue(app, front, trueValue); Release(front);
        if (result != 0) return new(false, Reason:"元のアプリに戻れません。コピーを利用してください。");
        Thread.Sleep(150);
        if (!SameFocus() || !Allowed(target) || !Selection(target, out var now) || now.location != selected.location || now.length != selected.length || ReadContext(target, now) != context)
            return new(false, Reason:"入力欄・カーソル位置・周辺文が変わりました。コピーを利用してください。");
        var pool = objc_autoreleasePoolPush();
        try
        {
            var board = Send(objc_getClass("NSPasteboard"), sel_registerName("generalPasteboard"));
            Send(board, sel_registerName("clearContents"));
            var text = String(request.Text); var type = String("public.utf8-plain-text");
            var copied = SendBool(board, sel_registerName("setString:forType:"), text, type); Release(text); Release(type);
            if (!copied) return new(false, Reason:"クリップボードを設定できません。");
            if (!SameFocus()) return new(false, Reason:"入力先が変わったためコピーだけ行いました。");
            var down = CGEventCreateKeyboardEvent(IntPtr.Zero, 9, true); var up = CGEventCreateKeyboardEvent(IntPtr.Zero, 9, false);
            if (down == IntPtr.Zero || up == IntPtr.Zero) { Release(down); Release(up); return new(false, Reason:"貼り付けキーを生成できません。"); }
            CGEventSetFlags(down, 1UL<<20); CGEventSetFlags(up, 1UL<<20); CGEventPost(0, down); CGEventPost(0, up); Release(down); Release(up);
            return new(true, Reason:"確認した文の貼り付け操作を送りました。クリップボードにも残っています。");
        }
        finally { objc_autoreleasePoolPop(pool); }
    }
    static bool SameFocus()
    {
        var current = Attribute(system, "AXFocusedUIElement"); var currentApp = Attribute(system, "AXFocusedApplication");
        try { return current != IntPtr.Zero && currentApp != IntPtr.Zero && CFEqual(current, target) && CFEqual(currentApp, app); }
        finally { Release(current); Release(currentApp); }
    }
    static bool Allowed(IntPtr element) => TextAttribute(element, "AXSubrole") != "AXSecureTextField" && TextAttribute(element, "AXRole") is "AXTextField" or "AXTextArea";
    static bool Selection(IntPtr element, out CFRange range)
    {
        range = default; var value = Attribute(element, "AXSelectedTextRange");
        try { return value != IntPtr.Zero && CFGetTypeID(value) == AXValueGetTypeID() && AXValueGetValue(value, 4, out range); } finally { Release(value); }
    }
    static string? ReadContext(IntPtr element, CFRange range)
    {
        var countValue = Attribute(element, "AXNumberOfCharacters"); long count = 0;
        try { if (countValue == IntPtr.Zero || CFGetTypeID(countValue) != CFNumberGetTypeID() || !CFNumberGetValue(countValue, 4, out count)) return null; }
        finally { Release(countValue); }
        var start = Math.Max(0, (long)range.location - 600);
        var around = new CFRange { location = (nint)start, length = (nint)Math.Min(1200, Math.Max(0, count-start)) };
        var parameter = AXValueCreate(4, ref around); var key = String("AXStringForRange");
        try { if (AXUIElementCopyParameterizedAttributeValue(element,key,parameter,out var value) != 0) return null;
            try { return ReadString(value); } finally { Release(value); } }
        finally { Release(parameter); Release(key); }
    }
    static IntPtr Attribute(IntPtr element, string name)
    { var key=String(name); try { return AXUIElementCopyAttributeValue(element,key,out var value)==0 ? value : IntPtr.Zero; } finally { Release(key); } }
    static string? TextAttribute(IntPtr element,string name) { var value=Attribute(element,name); try {return ReadString(value);} finally {Release(value);} }
    static string? ReadString(IntPtr value)
    {
        if(value==IntPtr.Zero || CFGetTypeID(value)!=CFStringGetTypeID()) return null;
        var length=CFStringGetLength(value); if(length>1200) return null;
        var buffer=new byte[(int)length*4+1]; return CFStringGetCString(value,buffer,buffer.Length,0x08000100) ? Encoding.UTF8.GetString(buffer,0,Array.IndexOf(buffer,(byte)0)) : null;
    }
    static IntPtr String(string text) => CFStringCreateWithCString(IntPtr.Zero,text,0x08000100);
    static void Release(IntPtr value) {if(value!=IntPtr.Zero) CFRelease(value);}
    [StructLayout(LayoutKind.Sequential)] struct CFRange {public nint location,length;}
    [DllImport(AX)] static extern IntPtr AXUIElementCreateSystemWide();
    [DllImport(AX)] [return:MarshalAs(UnmanagedType.I1)] static extern bool AXIsProcessTrusted();
    [DllImport(AX)] static extern int AXUIElementCopyAttributeValue(IntPtr e,IntPtr key,out IntPtr value);
    [DllImport(AX)] static extern int AXUIElementCopyParameterizedAttributeValue(IntPtr e,IntPtr key,IntPtr parameter,out IntPtr value);
    [DllImport(AX)] static extern int AXUIElementSetAttributeValue(IntPtr e,IntPtr key,IntPtr value);
    [DllImport(AX)] static extern int AXUIElementGetPid(IntPtr e,out int pid);
    [DllImport(AX)] static extern nuint AXValueGetTypeID();
    [DllImport(AX)] static extern IntPtr AXValueCreate(int type,ref CFRange value);
    [DllImport(AX)] [return:MarshalAs(UnmanagedType.I1)] static extern bool AXValueGetValue(IntPtr value,int type,out CFRange result);
    [DllImport(CF)] static extern nuint CFGetTypeID(IntPtr value);
    [DllImport(CF)] static extern nuint CFStringGetTypeID();
    [DllImport(CF)] static extern nuint CFNumberGetTypeID();
    [DllImport(CF)] [return:MarshalAs(UnmanagedType.I1)] static extern bool CFNumberGetValue(IntPtr value,int type,out long number);
    [DllImport(CF)] static extern IntPtr CFStringCreateWithCString(IntPtr alloc,[MarshalAs(UnmanagedType.LPUTF8Str)]string value,uint encoding);
    [DllImport(CF)] static extern nint CFStringGetLength(IntPtr value);
    [DllImport(CF)] [return:MarshalAs(UnmanagedType.I1)] static extern bool CFStringGetCString(IntPtr value,byte[] buffer,nint size,uint encoding);
    [DllImport(CF)] static extern void CFRelease(IntPtr value);
    [DllImport(CF)] [return:MarshalAs(UnmanagedType.I1)] static extern bool CFEqual(IntPtr a,IntPtr b);
    [DllImport(CG)] static extern ulong CGEventSourceFlagsState(int state);
    [DllImport(CG)] static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source,ushort key,[MarshalAs(UnmanagedType.I1)]bool down);
    [DllImport(CG)] static extern void CGEventSetFlags(IntPtr e,ulong flags);
    [DllImport(CG)] static extern void CGEventPost(int tap,IntPtr e);
    [DllImport(ObjC)] static extern IntPtr objc_getClass(string name);
    [DllImport(ObjC)] static extern IntPtr sel_registerName(string name);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] static extern IntPtr Send(IntPtr receiver,IntPtr selector);
    [DllImport(ObjC,EntryPoint="objc_msgSend")] [return:MarshalAs(UnmanagedType.I1)] static extern bool SendBool(IntPtr receiver,IntPtr selector,IntPtr a,IntPtr b);
    [DllImport(ObjC)] static extern IntPtr objc_autoreleasePoolPush();
    [DllImport(ObjC)] static extern void objc_autoreleasePoolPop(IntPtr pool);
}
