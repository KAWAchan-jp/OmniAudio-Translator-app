namespace OmniAudioTranslator.Core.Settings;

/// <summary>字幕オーバーレイの表示スタイル設定。各行の表示可否・フォントサイズ・色と背景の不透明度を保持する。</summary>
public sealed class OverlaySettings
{
    public bool ShowStatus { get; set; } = true;

    public int StatusFontSize { get; set; } = 13;

    public string StatusColor { get; set; } = "#FFAAAAAA";

    public bool ShowSourceText { get; set; } = true;

    public int SourceFontSize { get; set; } = 15;

    public string SourceColor { get; set; } = "#FFAAAAAA";

    public bool ShowTranslatedText { get; set; } = true;

    public int TranslatedFontSize { get; set; } = 23;

    public string TranslatedColor { get; set; } = "#FFFFFFFF";

    public double BackgroundOpacity { get; set; } = 0.59;

    public static OverlaySettings CreateDefault() => new();

    public OverlaySettings Clone() => (OverlaySettings)MemberwiseClone();
}
