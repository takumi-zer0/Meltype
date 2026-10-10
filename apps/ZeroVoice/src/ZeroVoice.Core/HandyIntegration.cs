using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ZeroVoice.Core;

public static class HandyIntegration
{
    static readonly string[] Keys = ["paste_method", "external_script_path", "auto_submit", "post_process_enabled", "clipboard_handling"];
    public static JsonObject Desired(string executable) => new() {
        ["paste_method"]="external_script", ["external_script_path"]=executable,
        ["auto_submit"]=false, ["post_process_enabled"]=false, ["clipboard_handling"]="dont_modify"
    };
    public static (JsonObject Store, JsonObject Backup) Prepare(JsonObject store, string executable)
    {
        if (!Path.IsPathFullyQualified(executable)) throw new ArgumentException("実行ファイルは絶対パスで指定してください。");
        if (store["settings"] is not JsonObject settings || settings["paste_method"] is null)
            throw new InvalidDataException("Handy 0.9.8形式の設定が必要です。");
        var old = new JsonObject(); var missing = new JsonArray(); var applied = Desired(executable);
        foreach (var key in Keys) { if (settings.ContainsKey(key)) old[key]=settings[key]?.DeepClone(); else missing.Add(key); }
        foreach (var key in Keys) settings[key]=applied[key]?.DeepClone();
        return (store, new JsonObject { ["schema"]=1, ["before"]=old, ["missing"]=missing, ["applied"]=applied });
    }
    public static JsonObject Restore(JsonObject store, JsonObject backup)
    {
        if (backup["schema"]?.GetValue<int>() != 1 || store["settings"] is not JsonObject settings || backup["applied"] is not JsonObject applied || backup["before"] is not JsonObject before)
            throw new InvalidDataException("復旧用データを確認できません。");
        // A crash after the backup write but before the settings write leaves the original intact.
        if (Keys.All(key => settings.ContainsKey(key) == before.ContainsKey(key) && JsonNode.DeepEquals(settings[key],before[key]))) return store;
        foreach (var key in Keys) if (!JsonNode.DeepEquals(settings[key],applied[key]))
            throw new InvalidOperationException("Handyの連携設定が後から変更されています。上書きせず停止しました。");
        foreach (var key in Keys) { if (before.ContainsKey(key)) settings[key]=before[key]?.DeepClone(); else settings.Remove(key); }
        return store;
    }
    public static void ApplyFile(string file, string executable, bool restore)
    {
        if (Process.GetProcesses().Any(p => { using(p) { try { return p.ProcessName.Equals("Handy",StringComparison.OrdinalIgnoreCase); } catch { return false; } } }))
            throw new InvalidOperationException("Handyをトレイ/メニューバーから終了してください。");
        file=Path.GetFullPath(file); var backup=file+".zerovoice-backup.json";
        var store=JsonNode.Parse(File.ReadAllText(file))?.AsObject() ?? throw new InvalidDataException();
        if (restore)
        {
            var prior=JsonNode.Parse(File.ReadAllText(backup))!.AsObject();
            AtomicWrite(file,Restore(store,prior));
            File.Move(backup,backup+".restored-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            return;
        }
        if (File.Exists(backup)) throw new InvalidOperationException("復旧用データが既にあります。先に連携解除してください。");
        var prepared=Prepare(store,executable);
        // The backup contains only the five changed fields; API keys and unrelated settings are not copied.
        AtomicWrite(backup,prepared.Backup);
        AtomicWrite(file,prepared.Store);
    }
    static void AtomicWrite(string file, JsonObject document)
    {
        var temporary=file+".tmp-"+Guid.NewGuid().ToString("N");
        try { using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
            if(!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary,UnixFileMode.UserRead|UnixFileMode.UserWrite);
            JsonSerializer.Serialize(stream,document,new JsonSerializerOptions {WriteIndented=true}); stream.Flush(true);
        } File.Move(temporary,file,overwrite:true); }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}
