namespace ZeroVoice.Core;

public sealed record TargetInfo(bool Available, string App, string Context, string Token, string Reason);
public sealed record HelperRequest(string Operation, string Token = "", string Text = "");
public sealed record HelperReply(bool Ok, TargetInfo? Target = null, string Reason = "");
