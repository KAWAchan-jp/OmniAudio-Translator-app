namespace OmniAudioTranslator.Core.Capture;

/// <summary>
/// ウィンドウ選択ダイアログの強調表示に使うキーワード照合。
/// 大文字小文字を区別せず、表示文字列([exe.exe]: タイトル)への部分一致で判定する。
/// </summary>
public static class WindowKeywordMatcher
{
    /// <summary>表示文字列がいずれかのキーワードを含むかどうかを返す。空のキーワードは無視する。</summary>
    public static bool Matches(string text, IReadOnlyList<string> keywords)
    {
        foreach (string keyword in keywords)
        {
            if (MatchesKeyword(text, keyword))
            {
                return true;
            }
        }

        return false;
    }

    public static bool MatchesKeyword(string text, string keyword)
    {
        string trimmed = keyword.Trim();
        return trimmed.Length > 0 && text.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>前後の空白を除去し、空要素と大文字小文字だけが異なる重複を取り除いたキーワード一覧を返す(順序は維持)。</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string?> keywords)
    {
        var result = new List<string>();

        foreach (string? keyword in keywords)
        {
            if (keyword is null)
            {
                continue;
            }

            string trimmed = keyword.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (result.Exists(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(trimmed);
        }

        return result;
    }
}
