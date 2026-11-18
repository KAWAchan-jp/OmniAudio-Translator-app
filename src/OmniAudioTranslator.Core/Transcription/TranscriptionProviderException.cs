using System.Net;

namespace OmniAudioTranslator.Core.Transcription;

/// <summary>
/// 音声認識プロバイダの呼び出しが失敗したことを表す。
/// レート制限や認証エラーをUI・ログで切り分けられるよう、HTTPステータスと応答本文を保持する。
/// </summary>
public sealed class TranscriptionProviderException : Exception
{
    public TranscriptionProviderException(
        string message,
        HttpStatusCode? statusCode = null,
        bool isRateLimited = false,
        string? responseBody = null) : base(message)
    {
        StatusCode = statusCode;
        IsRateLimited = isRateLimited;
        ResponseBody = responseBody;
    }

    /// <summary>HTTPステータス。通信自体に失敗した場合は<c>null</c>。</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>レート制限(HTTP 429等)に起因する失敗か。</summary>
    public bool IsRateLimited { get; }

    /// <summary>デバッグ用の応答本文。<see cref="GroqTranscriptionProvider"/>が長さを制限して格納する。</summary>
    public string? ResponseBody { get; }
}
