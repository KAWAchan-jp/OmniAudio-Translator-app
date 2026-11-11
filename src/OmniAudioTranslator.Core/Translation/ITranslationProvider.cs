namespace OmniAudioTranslator.Core.Translation;

/// <summary>翻訳プロバイダの抽象。認識済みテキストを受け取り、指定言語への翻訳結果を返す。</summary>
public interface ITranslationProvider
{
    Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken = default);
}
