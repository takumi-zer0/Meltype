namespace ZeroVoice.Core;

// A reviewed version is single-use; any edit or fresh inference revokes approval.
public sealed class ReviewSession
{
    public string Original { get; }
    public string Candidate { get; private set; }
    public int Revision { get; private set; }
    private int? approvedRevision;
    private bool consumed;
    public ReviewSession(string original) { Original = original; Candidate = original; }
    public void Edit(string text) { Candidate = text; Revision++; approvedRevision = null; }
    public void Approve() { if (string.IsNullOrWhiteSpace(Candidate)) throw new InvalidOperationException("入力する文が空です。"); approvedRevision = Revision; }
    public string Consume()
    {
        if (consumed || approvedRevision != Revision) throw new InvalidOperationException("現在の文を確認してください。");
        consumed = true;
        return Candidate;
    }
    public bool CanApply => !consumed && approvedRevision == Revision;
}
