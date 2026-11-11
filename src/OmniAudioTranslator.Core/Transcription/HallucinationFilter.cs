namespace OmniAudioTranslator.Core.Transcription;

/// <summary>
/// Whisper系ASRの定型ハルシネーション（無音時に出やすい「ご視聴ありがとうございました」等の
/// 定型文、および同一に近い短文の反復）を検出する。
/// Stream-Speech-Layerの transcriber.js の isLikelyHallucination()/hasRepeatedSentenceLoop()/
/// sentenceSimilarity() をほぼ1:1移植したもの。
/// </summary>
public static class HallucinationFilter
{
    private static readonly string[] CannedPhrases =
    [
        "ご視聴ありがとうございました",
        "ご視聴ありがとうございます",
        "チャンネル登録よろしくお願いします",
        "thankyouforwatching",
        "thanksforwatching",
        "pleasesubscribe",
    ];

    private static readonly char[] SentenceDelimiters = ['。', '！', '？', '.', '!', '?'];

    public static bool IsLikelyHallucination(string text)
    {
        if (HasRepeatedSentenceLoop(text))
        {
            return true;
        }

        string normalized = Normalize(text);
        if (normalized.Length == 0)
        {
            return false;
        }

        foreach (string phrase in CannedPhrases)
        {
            if (normalized == Normalize(phrase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whisperの反復ハルシネーション検出: ほぼ同一の短文が連続して繰り返される場合を検出する。</summary>
    public static bool HasRepeatedSentenceLoop(string text)
    {
        string[] sentences = text
            .Split(SentenceDelimiters, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length >= 4)
            .ToArray();

        if (sentences.Length < 3)
        {
            return false;
        }

        int repeatCount = 0;
        for (int i = 1; i < sentences.Length; i++)
        {
            if (SentenceSimilarity(sentences[i - 1], sentences[i]) >= 0.7)
            {
                repeatCount++;
            }
        }

        return repeatCount >= 2;
    }

    /// <summary>文字位置ベースの類似度（同じ長さ付近の文の一致率）。編集距離までは計算せず軽量に判定する。</summary>
    public static double SentenceSimilarity(string a, string b)
    {
        int len = Math.Max(a.Length, b.Length);
        if (len == 0)
        {
            return 1;
        }

        int minLen = Math.Min(a.Length, b.Length);
        int same = 0;
        for (int i = 0; i < minLen; i++)
        {
            if (a[i] == b[i])
            {
                same++;
            }
        }

        return (double)same / len;
    }

    private static string Normalize(string text)
    {
        Span<char> buffer = stackalloc char[text.Length];
        int count = 0;
        foreach (char c in text.ToLowerInvariant())
        {
            if (c is '。' or '、' or '！' or '？' or '!' or '?' or ',' or '.' || char.IsWhiteSpace(c))
            {
                continue;
            }

            buffer[count++] = c;
        }

        return new string(buffer[..count]);
    }
}
