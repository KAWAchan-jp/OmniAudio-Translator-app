using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OmniAudioTranslator.Core.Settings;

namespace OmniAudioTranslator.App.Views;

public partial class OverlayWindow : Window
{
    private OverlaySettings _settings = OverlaySettings.CreateDefault();
    private string _sourceText = string.Empty;
    private string? _translatedText;
    private string _statusText = string.Empty;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    /// <summary>表示スタイル設定(フォントサイズ・色・不透明度・行の表示可否)を反映する。</summary>
    public void ApplySettings(OverlaySettings settings)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ApplySettings(settings));
            return;
        }

        _settings = settings;
        Render();
    }

    /// <summary>読み取りステータス行(処理中メッセージ)を更新する。</summary>
    public void SetStatus(string status)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => SetStatus(status));
            return;
        }

        _statusText = status;
        Render();
    }

    /// <summary>
    /// <paramref name="sourceText"/>は認識結果(元言語)、<paramref name="translatedText"/>は翻訳結果。
    /// 翻訳結果がある場合は元言語を上段に小さく、翻訳結果を下段に大きく表示する。
    /// 翻訳結果が無い(未翻訳)場合は元言語のみを大きく表示する。
    /// </summary>
    public void SetText(string sourceText, string? translatedText = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => SetText(sourceText, translatedText));
            return;
        }

        _sourceText = sourceText;
        _translatedText = translatedText;
        Render();
    }

    private void Render()
    {
        BackgroundBorder.Background = new SolidColorBrush(
            Color.FromArgb((byte)Math.Clamp(_settings.BackgroundOpacity * 255, 0, 255), 0, 0, 0));

        StatusTextBlock.Text = _statusText;
        StatusTextBlock.FontSize = _settings.StatusFontSize;
        StatusTextBlock.Foreground = ParseColor(_settings.StatusColor);
        StatusTextBlock.Visibility = _settings.ShowStatus && !string.IsNullOrEmpty(_statusText)
            ? Visibility.Visible
            : Visibility.Collapsed;

        bool hasTranslation = !string.IsNullOrEmpty(_translatedText) && _translatedText != _sourceText;

        SourceText.Text = _sourceText;
        SourceText.FontSize = _settings.SourceFontSize;
        SourceText.Foreground = ParseColor(_settings.SourceColor);
        SourceText.Visibility = _settings.ShowSourceText && hasTranslation
            ? Visibility.Visible
            : Visibility.Collapsed;

        // 翻訳が無い場合はオーバーレイが空にならないよう、表示トグルに関わらずメイン行に元言語を出す。
        TranscriptText.Text = hasTranslation ? _translatedText! : _sourceText;
        TranscriptText.FontSize = _settings.TranslatedFontSize;
        TranscriptText.Foreground = ParseColor(_settings.TranslatedColor);
        TranscriptText.Visibility = !hasTranslation || _settings.ShowTranslatedText
            ? Visibility.Visible
            : Visibility.Collapsed;

        // AllowsTransparency=Trueのウィンドウでは、Show()後に中身が増えてもSizeToContentの再測定が
        // 自動では走らない。そのため後から可視化した行(ステータス・元言語)が高さ0に潰れて見えなくなる
        // 不具合を実機で確認した。内容を変えるたびに明示的に測り直してウィンドウを伸縮させる。
        InvalidateMeasure();
        UpdateLayout();
    }

    private static SolidColorBrush ParseColor(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
