using System.Text.RegularExpressions;

namespace ZeroVoice.Core;

// Small, auditable lexicon. Require two distinct hints and a clear lead over every alternative.
// This supplements the model; it never authorizes input or claims to resolve every ambiguity.
public static class ContextTerms
{
    sealed record Sense(string Word, string[] Hints);
    static readonly Sense[][] Groups = [
        [new("仕様", ["設計", "要件", "実装", "仕様書", "動作定義"]),
         new("使用", ["操作", "使い方", "利用", "使用方法", "手順"]),
         new("私用", ["私事", "個人的", "プライベート", "仕事以外"])],
        [new("校正", ["誤字", "脱字", "表記", "校閲", "原稿", "編集"]),
         new("公正", ["公平", "偏り", "審査", "中立", "不正"]),
         new("構成", ["構造", "組み立て", "配置", "章立て", "構成要素"]),
         new("更生", ["立ち直り", "社会復帰", "矯正", "更生施設"])],
        [new("精算", ["領収書", "経費", "交通費", "旅費", "立替", "返金"]),
         new("生産", ["工場", "製造", "製品", "産業", "生産ライン"]),
         new("清算", ["債務", "会社解散", "残債", "清算人", "債権者"])],
        [new("回答", ["問い合わせ", "返信", "アンケート", "返答", "質問への対応"]),
         new("解答", ["試験", "採点", "正解", "答案", "問題集"])],
        [new("保証", ["メーカー", "製品不良", "修理", "保証書", "品質"]),
         new("保障", ["社会保障", "人権", "生活保護", "安全保障"]),
         new("補償", ["損害", "賠償", "事故", "被害", "弁償"])]
    ];

    public static CorrectionRequest Prepare(CorrectionRequest request)
    {
        Correction.ValidateRequest(request);
        if (string.IsNullOrWhiteSpace(request.Context)) return request;
        var text = request.Transcript;
        foreach (var group in Groups)
        {
            var scores = group.Select(s => (Sense:s, Score:s.Hints.Count(h => request.Context.Contains(h, StringComparison.Ordinal)))).OrderByDescending(s => s.Score).ToArray();
            if (scores[0].Score < 2 || scores[0].Score - scores[1].Score < 2) continue;
            var chosen = scores[0].Sense.Word;
            foreach (var other in group.Where(s => s.Word != chosen))
            {
                // Leave compound nouns intact (e.g. 使用方法). Match an independent noun/verb stem.
                var pattern = @"(?<=^|[\s、。「」『』]|を|の|は|が|に|で|と|も|や)" + Regex.Escape(other.Word) + @"(?=$|[\s、。！？「」『』]|を|の|は|が|に|で|と|も|や|し|する|して|してください)";
                text = Regex.Replace(text, pattern, chosen, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            }
        }
        return request with { Transcript=text };
    }
}
