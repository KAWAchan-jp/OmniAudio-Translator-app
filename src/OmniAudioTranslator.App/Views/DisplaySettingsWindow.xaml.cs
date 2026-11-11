using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using OmniAudioTranslator.Core.Localization;
using OmniAudioTranslator.Core.Settings;

namespace OmniAudioTranslator.App.Views;

/// <summary>
/// 字幕オーバーレイの表示スタイル(行ごとの表示可否・フォントサイズ・色・背景の不透明度)を編集するウィンドウ。
/// 変更のたびに<see cref="SettingsChanged"/>を発火し、呼び出し元がオーバーレイへ即時反映・永続化する。
/// </summary>
public partial class DisplaySettingsWindow : Window
{
    private static readonly string[] Palette =
    [
        "#FFFFFFFF", "#FFD9D9D9", "#FFAAAAAA", "#FF808080", "#FF404040", "#FF000000",
        "#FFFF5252", "#FFFF9800", "#FFFFEB3B", "#FF4CAF50", "#FF03A9F4", "#FF9C27B0",
    ];

    private readonly OverlaySettings _settings;
    private bool _suppressEvents;
    private Action<string>? _pendingColorApply;

    public DisplaySettingsWindow(OverlaySettings initial)
    {
        InitializeComponent();
        _settings = initial;

        BuildColorPalette();
        ApplyLocalizedText();
        LoadIntoControls();

        Strings.LanguageChanged += ApplyLocalizedText;
        Closed += (_, _) => Strings.LanguageChanged -= ApplyLocalizedText;
    }

    /// <summary>設定が変更されるたびに、その時点の値のコピーを渡して発火する。</summary>
    public event Action<OverlaySettings>? SettingsChanged;

    private void ApplyLocalizedText()
    {
        Title = Strings.Get("display_settings.window_title");
        HeaderText.Text = Strings.Get("display_settings.window_title");
        DescriptionText.Text = Strings.Get("display_settings.description");

        StatusSectionHeader.Text = Strings.Get("display_settings.section_status");
        StatusShowCheck.Content = Strings.Get("display_settings.show");
        StatusSizeLabel.Text = Strings.Get("display_settings.size_px");
        StatusColorLabel.Text = Strings.Get("display_settings.color");

        SourceSectionHeader.Text = Strings.Get("display_settings.section_source");
        SourceShowCheck.Content = Strings.Get("display_settings.show");
        SourceSizeLabel.Text = Strings.Get("display_settings.size_px");
        SourceColorLabel.Text = Strings.Get("display_settings.color");

        TranslatedSectionHeader.Text = Strings.Get("display_settings.section_translated");
        TranslatedShowCheck.Content = Strings.Get("display_settings.show");
        TranslatedSizeLabel.Text = Strings.Get("display_settings.size_px");
        TranslatedColorLabel.Text = Strings.Get("display_settings.color");

        OpacityLabel.Text = Strings.Get("display_settings.opacity_label");
        ResetButton.Content = Strings.Get("display_settings.reset_button");
    }

    private void BuildColorPalette()
    {
        foreach (string hex in Palette)
        {
            var cell = new Border
            {
                Width = 18,
                Height = 18,
                Margin = new Thickness(2),
                Background = ParseColor(hex),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
            };
            cell.MouseLeftButtonUp += (_, _) =>
            {
                _pendingColorApply?.Invoke(hex);
                ColorPalettePopup.IsOpen = false;
            };
            ColorPaletteGrid.Children.Add(cell);
        }
    }

    private void LoadIntoControls()
    {
        _suppressEvents = true;

        StatusShowCheck.IsChecked = _settings.ShowStatus;
        StatusSizeBox.Text = _settings.StatusFontSize.ToString();
        StatusColorSwatch.Background = ParseColor(_settings.StatusColor);

        SourceShowCheck.IsChecked = _settings.ShowSourceText;
        SourceSizeBox.Text = _settings.SourceFontSize.ToString();
        SourceColorSwatch.Background = ParseColor(_settings.SourceColor);

        TranslatedShowCheck.IsChecked = _settings.ShowTranslatedText;
        TranslatedSizeBox.Text = _settings.TranslatedFontSize.ToString();
        TranslatedColorSwatch.Background = ParseColor(_settings.TranslatedColor);

        OpacitySlider.Value = _settings.BackgroundOpacity;
        OpacityValueText.Text = _settings.BackgroundOpacity.ToString("0.00");

        _suppressEvents = false;
    }

    private void OnControlChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        _settings.ShowStatus = StatusShowCheck.IsChecked == true;
        _settings.StatusFontSize = ParseSize(StatusSizeBox.Text, _settings.StatusFontSize);

        _settings.ShowSourceText = SourceShowCheck.IsChecked == true;
        _settings.SourceFontSize = ParseSize(SourceSizeBox.Text, _settings.SourceFontSize);

        _settings.ShowTranslatedText = TranslatedShowCheck.IsChecked == true;
        _settings.TranslatedFontSize = ParseSize(TranslatedSizeBox.Text, _settings.TranslatedFontSize);

        SettingsChanged?.Invoke(_settings.Clone());
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        OpacityValueText.Text = e.NewValue.ToString("0.00");

        if (_suppressEvents)
        {
            return;
        }

        _settings.BackgroundOpacity = e.NewValue;
        SettingsChanged?.Invoke(_settings.Clone());
    }

    private void StatusColorSwatch_Click(object sender, RoutedEventArgs e) =>
        OpenColorPalette((Button)sender, hex =>
        {
            _settings.StatusColor = hex;
            StatusColorSwatch.Background = ParseColor(hex);
            SettingsChanged?.Invoke(_settings.Clone());
        });

    private void SourceColorSwatch_Click(object sender, RoutedEventArgs e) =>
        OpenColorPalette((Button)sender, hex =>
        {
            _settings.SourceColor = hex;
            SourceColorSwatch.Background = ParseColor(hex);
            SettingsChanged?.Invoke(_settings.Clone());
        });

    private void TranslatedColorSwatch_Click(object sender, RoutedEventArgs e) =>
        OpenColorPalette((Button)sender, hex =>
        {
            _settings.TranslatedColor = hex;
            TranslatedColorSwatch.Background = ParseColor(hex);
            SettingsChanged?.Invoke(_settings.Clone());
        });

    private void OpenColorPalette(Button swatchButton, Action<string> apply)
    {
        _pendingColorApply = apply;
        ColorPalettePopup.PlacementTarget = swatchButton;
        ColorPalettePopup.IsOpen = true;
    }

    /// <summary>「表示設定を初期値に戻す」ボタン。既定値をコントロールへ再読込し、即座に反映・通知する。</summary>
    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        OverlaySettings defaults = OverlaySettings.CreateDefault();
        _settings.ShowStatus = defaults.ShowStatus;
        _settings.StatusFontSize = defaults.StatusFontSize;
        _settings.StatusColor = defaults.StatusColor;
        _settings.ShowSourceText = defaults.ShowSourceText;
        _settings.SourceFontSize = defaults.SourceFontSize;
        _settings.SourceColor = defaults.SourceColor;
        _settings.ShowTranslatedText = defaults.ShowTranslatedText;
        _settings.TranslatedFontSize = defaults.TranslatedFontSize;
        _settings.TranslatedColor = defaults.TranslatedColor;
        _settings.BackgroundOpacity = defaults.BackgroundOpacity;

        LoadIntoControls();
        SettingsChanged?.Invoke(_settings.Clone());
    }

    private static int ParseSize(string text, int fallback) =>
        int.TryParse(text, out int value) && value > 0 ? value : fallback;

    private static SolidColorBrush ParseColor(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
}
