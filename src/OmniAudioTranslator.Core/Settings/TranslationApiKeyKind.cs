namespace OmniAudioTranslator.Core.Settings;

/// <summary>APIキーを必要とするプロバイダの種別（設定の保存キーとして使う）。</summary>
public enum TranslationApiKeyKind
{
    DeepL,
    Gemini,

    /// <summary>クラウド音声認識(Groq)用。名称は歴史的経緯でTranslationのまま。</summary>
    Groq,
}
