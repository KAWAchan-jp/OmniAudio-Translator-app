namespace OmniAudioTranslator.Core.Translation;

/// <summary>翻訳プロバイダのHTTP呼び出しが失敗したことを表す。<see cref="IsRateLimited"/>でレート制限起因かを判別できる。</summary>
public sealed class TranslationProviderException(string message, bool isRateLimited) : Exception(message)
{
    public bool IsRateLimited { get; } = isRateLimited;
}
