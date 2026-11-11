using System.Net;

namespace OmniAudioTranslator.Core.Translation;

/// <summary>
/// レート制限エラーの判定を各翻訳プロバイダで共有する。
/// Stream-Speech-Layerのbackground.jsではプロバイダごとに同種の判定処理が重複していたため、
/// ここに集約した。
/// </summary>
public static class RateLimitClassifier
{
    private static readonly string[] RateLimitMarkers =
    [
        "RESOURCE_EXHAUSTED",
        "rate limit",
        "quota",
    ];

    public static bool IsRateLimitError(HttpStatusCode statusCode, string? responseBody)
    {
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return true;
        }

        if (string.IsNullOrEmpty(responseBody))
        {
            return false;
        }

        foreach (string marker in RateLimitMarkers)
        {
            if (responseBody.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
