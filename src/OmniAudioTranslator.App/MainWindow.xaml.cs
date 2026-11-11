using System.IO;
using System.Windows;
using System.Windows.Controls;
using OmniAudioTranslator.App.Views;
using OmniAudioTranslator.Core.Capture;
using OmniAudioTranslator.Core.Localization;
using OmniAudioTranslator.Core.Orchestration;
using OmniAudioTranslator.Core.Settings;
using OmniAudioTranslator.Core.Transcription;
using OmniAudioTranslator.Core.Translation;

namespace OmniAudioTranslator.App;

public partial class MainWindow : Window
{
    private static readonly (string Key, string Tag)[] SourceLanguageOptions =
    [
        ("language.auto", "auto"),
        ("language.ja", "ja"),
        ("language.en", "en"),
        ("language.zh", "zh"),
        ("language.ko", "ko"),
        ("language.es", "es"),
        ("language.fr", "fr"),
        ("language.de", "de"),
        ("language.ru", "ru"),
        ("language.pt", "pt"),
        ("language.it", "it"),
    ];

    private static readonly (string Key, string Tag)[] TargetLanguageOptions =
    [
        ("language.ja", "ja"),
        ("language.en", "en"),
        ("language.zh", "zh"),
        ("language.ko", "ko"),
        ("language.es", "es"),
        ("language.fr", "fr"),
        ("language.de", "de"),
        ("language.ru", "ru"),
        ("language.pt", "pt"),
        ("language.it", "it"),
    ];

    private static readonly string[] TranslationProviderKeys =
    [
        "translation_provider.none",
        "translation_provider.google_free",
        "translation_provider.deepl",
        "translation_provider.gemini",
    ];

    private readonly SettingsStore _settingsStore = new();

    private CapturableWindow? _selectedWindow;
    private TranslationSessionService? _session;
    private OverlayWindow? _overlay;
    private WhisperNetProvider? _transcriptionProvider;
    private ITranslationProvider? _translationProvider;
    private OverlaySettings _overlaySettings;
    private DisplaySettingsWindow? _displaySettingsWindow;
    private string? _engineInfo;
    private bool _suppressLanguageEvents;
    private bool _suppressChunkIntervalEvents;
    private bool _suppressApiKeyEvents;
    private bool _suppressTranslationSelectionEvents = true;

    public MainWindow()
    {
        InitializeComponent();
        _overlaySettings = _settingsStore.LoadOverlaySettings();
        RestoreMainWindowPosition();

        AppLanguage savedLanguage = _settingsStore.LoadLanguage();
        Strings.CurrentLanguage = savedLanguage;

        _suppressLanguageEvents = true;
        LanguageCombo.SelectedIndex = savedLanguage == AppLanguage.English ? 1 : 0;
        _suppressLanguageEvents = false;

        _suppressChunkIntervalEvents = true;
        ChunkIntervalSlider.Value = _settingsStore.LoadChunkMillis() / 1000.0;
        _suppressChunkIntervalEvents = false;

        LoadSavedApiKeys();
        ApplyLocalizedText();
        RestoreTranslationSelections();
        _suppressTranslationSelectionEvents = false;
        StatusText.Text = Strings.Get("mainwindow.waiting");
    }

    /// <summary>
    /// 前回位置が現在の仮想デスクトップ内に見える場合だけ復元する。
    /// モニター構成変更などで画面外になった場合は、XAMLのCenterScreenへフォールバックする。
    /// </summary>
    private void RestoreMainWindowPosition()
    {
        WindowPosition? position = _settingsStore.LoadMainWindowPosition();
        if (position is null || !double.IsFinite(position.Left) || !double.IsFinite(position.Top))
        {
            return;
        }

        const double visibleMargin = 40;
        bool isVisible = position.Left + Width >= SystemParameters.VirtualScreenLeft + visibleMargin
            && position.Left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - visibleMargin
            && position.Top + Height >= SystemParameters.VirtualScreenTop + visibleMargin
            && position.Top <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - visibleMargin;

        if (!isVisible)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = position.Left;
        Top = position.Top;
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageEvents)
        {
            return;
        }

        string tag = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "ja";
        AppLanguage language = tag == "en" ? AppLanguage.English : AppLanguage.Japanese;

        Strings.CurrentLanguage = language;
        ApplyLocalizedText();

        try
        {
            _settingsStore.SaveLanguage(language);
        }
        catch (IOException ex)
        {
            StatusText.Text = Strings.Format("mainwindow.language_save_failed", ex.Message);
        }
    }

    /// <summary>UIラベル・ボタン・コンボの選択肢テキストを現在の<see cref="Strings.CurrentLanguage"/>で再構築する。</summary>
    private void ApplyLocalizedText()
    {
        string? previousSourceTag = (SourceLanguageCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        string? previousTargetTag = (TargetLanguageCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        int previousProviderIndex = TranslationProviderCombo.SelectedIndex;

        PopulateLanguageCombo(SourceLanguageCombo, SourceLanguageOptions, previousSourceTag ?? "ja");
        PopulateLanguageCombo(TargetLanguageCombo, TargetLanguageOptions, previousTargetTag ?? "en");
        PopulateTranslationProviderCombo(previousProviderIndex);

        SourceLanguageLabel.Text = Strings.Get("mainwindow.label_source_language");
        TranslationLabel.Text = Strings.Get("mainwindow.label_translation");
        TargetLanguageLabel.Text = Strings.Get("mainwindow.label_target_language");
        DeepLApiKeyLabel.Text = Strings.Get("mainwindow.label_api_key_deepl");
        GeminiApiKeyLabel.Text = Strings.Get("mainwindow.label_api_key_gemini");

        foreach ((_, _, CheckBox show, _) in ApiKeyFields)
        {
            show.Content = Strings.Get("mainwindow.api_key_show");
        }

        SelectWindowButton.Content = Strings.Get("mainwindow.button_select_window");
        StartButton.Content = Strings.Get("mainwindow.button_start");
        StopButton.Content = Strings.Get("mainwindow.button_stop");
        DisplaySettingsButton.Content = Strings.Get("mainwindow.button_display_settings");

        SelectedWindowText.Text = _selectedWindow is null
            ? Strings.Get("mainwindow.no_window_selected")
            : Strings.Format("mainwindow.window_selected", _selectedWindow.ProcessName, _selectedWindow.Title);

        ChunkIntervalLabel.Text = Strings.Get("mainwindow.label_chunk_interval");
        ChunkIntervalHintText.Text = Strings.Get("mainwindow.chunk_interval_hint");
        UpdateChunkIntervalValueText();

        UpdateTitle();
    }

    private void UpdateChunkIntervalValueText() =>
        ChunkIntervalValueText.Text = Strings.Format("mainwindow.chunk_interval_value", (int)ChunkIntervalSlider.Value);

    private void ChunkIntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // InitializeComponent()のXAML解析中、SliderのMinimum設定でValueが強制変更された時点で
        // このイベントが発火するが、XAML上でスライダーより後に定義されているChunkIntervalValueTextは
        // まだ生成されていない(null)。ガードしないと起動時にNullReferenceExceptionでクラッシュする。
        if (ChunkIntervalValueText is null)
        {
            return;
        }

        UpdateChunkIntervalValueText();

        if (_suppressChunkIntervalEvents)
        {
            return;
        }

        try
        {
            _settingsStore.SaveChunkMillis((int)ChunkIntervalSlider.Value * 1000);
        }
        catch (IOException ex)
        {
            StatusText.Text = Strings.Format("mainwindow.chunk_interval_save_failed", ex.Message);
        }
    }

    private static void PopulateLanguageCombo(ComboBox combo, (string Key, string Tag)[] options, string preserveTag)
    {
        combo.Items.Clear();
        foreach ((string key, string tag) in options)
        {
            combo.Items.Add(new ComboBoxItem { Content = Strings.Get(key), Tag = tag });
        }

        int index = Array.FindIndex(options, o => o.Tag == preserveTag);
        combo.SelectedIndex = index >= 0 ? index : 0;
    }

    private void PopulateTranslationProviderCombo(int preserveIndex)
    {
        TranslationProviderCombo.Items.Clear();
        foreach (string key in TranslationProviderKeys)
        {
            TranslationProviderCombo.Items.Add(new ComboBoxItem { Content = Strings.Get(key) });
        }

        TranslationProviderCombo.SelectedIndex = preserveIndex >= 0 && preserveIndex < TranslationProviderKeys.Length
            ? preserveIndex
            : 0;
    }

    /// <summary>保存済みのセレクター状態を復元する。不正な値は既定値へフォールバックする。</summary>
    private void RestoreTranslationSelections()
    {
        (string sourceLanguage, string targetLanguage, int providerIndex) = _settingsStore.LoadTranslationSelections();
        PopulateLanguageCombo(SourceLanguageCombo, SourceLanguageOptions, sourceLanguage);
        PopulateLanguageCombo(TargetLanguageCombo, TargetLanguageOptions, targetLanguage);
        PopulateTranslationProviderCombo(providerIndex);
    }

    private void UpdateTitle() => Title = _engineInfo is null
        ? "OmniAudio Translator"
        : Strings.Format("mainwindow.title_with_engine", _engineInfo);

    /// <summary>選択中の翻訳プロバイダに応じて、対応するAPIキー欄だけを表示する(どのキーを入れればよいか一目でわかるように)。</summary>
    private void TranslationProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DeepLApiKeyPanel.Visibility = TranslationProviderCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        GeminiApiKeyPanel.Visibility = TranslationProviderCombo.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        SaveTranslationSelections();

        if (_session is null)
        {
            return;
        }

        try
        {
            // 進行中のチャンクは従来プロバイダで完了させ、次チャンクから新しい選択を使う。
            _translationProvider = CreateTranslationProvider();
            _session.UpdateTranslationProvider(_translationProvider);
        }
        catch (Exception ex)
        {
            StatusText.Text = Strings.Format("mainwindow.start_failed", ex.Message);
        }
    }

    /// <summary>元言語・宛先言語の変更を保存し、実行中なら次の音声チャンクから反映する。</summary>
    private void TranslationLanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SaveTranslationSelections();

        if (_session is not null)
        {
            _session.UpdateLanguages(
                GetSelectedLanguageCode(SourceLanguageCombo, "ja"),
                GetSelectedLanguageCode(TargetLanguageCombo, "en"));
        }
    }

    private void SaveTranslationSelections()
    {
        if (_suppressTranslationSelectionEvents)
        {
            return;
        }

        try
        {
            _settingsStore.SaveTranslationSelections(
                GetSelectedLanguageCode(SourceLanguageCombo, "ja"),
                GetSelectedLanguageCode(TargetLanguageCombo, "en"),
                TranslationProviderCombo.SelectedIndex);
        }
        catch (IOException ex)
        {
            StatusText.Text = Strings.Format("mainwindow.language_save_failed", ex.Message);
        }
    }

    private void SelectWindowButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new WindowPickerDialog { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedWindow is { } window)
            {
                _selectedWindow = window;
                SelectedWindowText.Text = Strings.Format("mainwindow.window_selected", window.ProcessName, window.Title);
                StartButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = Strings.Format("mainwindow.window_select_error", ex);
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWindow is null)
        {
            return;
        }

        SelectWindowButton.IsEnabled = false;
        StartButton.IsEnabled = false;
        StatusText.Text = Strings.Get("mainwindow.preparing_model");

        try
        {
            var modelManager = new WhisperModelManager();
            string modelPath = await modelManager.EnsureModelAsync();
            _transcriptionProvider = new WhisperNetProvider(modelPath);
            _engineInfo = _transcriptionProvider.RuntimeInfo;
            UpdateTitle();

            string sourceLanguage = GetSelectedLanguageCode(SourceLanguageCombo, "auto");
            string targetLanguage = GetSelectedLanguageCode(TargetLanguageCombo, "ja");
            _translationProvider = CreateTranslationProvider();

            var capture = new ProcessLoopbackCapture();
            var chunker = new AudioChunker(capture.SampleRate, capture.Channels, capture.BitsPerSample, (int)ChunkIntervalSlider.Value * 1000);
            _session = new TranslationSessionService(capture, chunker, _transcriptionProvider, _translationProvider, targetLanguage, sourceLanguage);
            _session.TranscriptReady += OnTranscriptReady;
            _session.StatusChanged += OnStatusChanged;

            _overlay = new OverlayWindow();
            _overlay.ApplySettings(_overlaySettings);
            _overlay.Show();

            await _session.StartAsync(_selectedWindow.ProcessId);

            StopButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = Strings.Format("mainwindow.start_failed", ex.Message);
            SelectWindowButton.IsEnabled = true;
            StartButton.IsEnabled = true;
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _session?.Dispose();
        _session = null;
        _transcriptionProvider?.Dispose();
        _transcriptionProvider = null;
        (_translationProvider as IDisposable)?.Dispose();
        _translationProvider = null;
        _overlay?.Close();
        _overlay = null;
        _engineInfo = null;
        UpdateTitle();

        StatusText.Text = Strings.Get("mainwindow.stopped");
        SelectWindowButton.IsEnabled = true;
        StartButton.IsEnabled = _selectedWindow is not null;
        StopButton.IsEnabled = false;
    }

    /// <summary>言語選択コンボの選択項目からTagに設定した言語コードを取得する。</summary>
    private static string GetSelectedLanguageCode(ComboBox combo, string fallback) =>
        (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;

    /// <summary>
    /// 選択中の翻訳プロバイダと、そのプロバイダ専用のAPIキー欄からインスタンスを生成する。
    /// プロバイダ未選択(「なし」)なら翻訳しない。Google翻訳（無料）はAPIキー不要。
    /// それ以外のプロバイダを選んだのにAPIキーが未入力の場合は、翻訳が効かないまま気付かれず認識結果だけが
    /// 表示され続ける事故を避けるため、ここで例外にして開始を止める(呼び出し元のtry-catchで表示される)。
    /// </summary>
    private ITranslationProvider? CreateTranslationProvider() => TranslationProviderCombo.SelectedIndex switch
    {
        0 => null,
        1 => new GoogleTranslateFreeProvider(),
        2 => new DeepLProvider(RequireApiKey(DeepLApiKeyBox)),
        3 => new GeminiTranslationProvider(RequireApiKey(GeminiApiKeyBox)),
        _ => null,
    };

    private static string RequireApiKey(PasswordBox box)
    {
        string apiKey = box.Password.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(Strings.Get("mainwindow.translation_key_required"));
        }

        return apiKey;
    }

    /// <summary>APIキー欄(伏字/平文)とチェックボックスを種別ごとに束ねたもの。</summary>
    private (PasswordBox Masked, TextBox Plain, CheckBox Show, TranslationApiKeyKind Kind)[] ApiKeyFields =>
    [
        (DeepLApiKeyBox, DeepLApiKeyPlainBox, DeepLApiKeyShow, TranslationApiKeyKind.DeepL),
        (GeminiApiKeyBox, GeminiApiKeyPlainBox, GeminiApiKeyShow, TranslationApiKeyKind.Gemini),
    ];

    /// <summary>保存済みAPIキーを復号して各入力欄へ復元する。</summary>
    private void LoadSavedApiKeys()
    {
        _suppressApiKeyEvents = true;
        try
        {
            foreach ((PasswordBox masked, TextBox plain, _, TranslationApiKeyKind kind) in ApiKeyFields)
            {
                string apiKey = _settingsStore.LoadApiKey(kind);
                masked.Password = apiKey;
                plain.Text = apiKey;
            }
        }
        catch (IOException ex)
        {
            StatusText.Text = Strings.Format("mainwindow.api_key_load_failed", ex.Message);
        }
        finally
        {
            _suppressApiKeyEvents = false;
        }
    }

    /// <summary>伏字欄が編集されたら平文欄へ同期し、保護した上で保存する。</summary>
    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressApiKeyEvents || sender is not PasswordBox masked)
        {
            return;
        }

        foreach ((PasswordBox m, TextBox plain, _, TranslationApiKeyKind kind) in ApiKeyFields)
        {
            if (!ReferenceEquals(m, masked))
            {
                continue;
            }

            _suppressApiKeyEvents = true;
            plain.Text = masked.Password;
            _suppressApiKeyEvents = false;
            SaveApiKey(kind, masked.Password);
            return;
        }
    }

    /// <summary>平文欄(表示中)が編集されたら伏字欄へ同期し、保護した上で保存する。</summary>
    private void ApiKeyPlainBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressApiKeyEvents || sender is not TextBox plain)
        {
            return;
        }

        foreach ((PasswordBox masked, TextBox p, _, TranslationApiKeyKind kind) in ApiKeyFields)
        {
            if (!ReferenceEquals(p, plain))
            {
                continue;
            }

            _suppressApiKeyEvents = true;
            masked.Password = plain.Text;
            _suppressApiKeyEvents = false;
            SaveApiKey(kind, plain.Text);
            return;
        }
    }

    private void SaveApiKey(TranslationApiKeyKind kind, string apiKey)
    {
        try
        {
            _settingsStore.SaveApiKey(kind, apiKey.Trim());
        }
        catch (IOException ex)
        {
            StatusText.Text = Strings.Format("mainwindow.api_key_save_failed", ex.Message);
        }
    }

    /// <summary>「表示」チェックで伏字欄と平文欄を入れ替える(保存済みキーを後から確認できるように)。</summary>
    private void ApiKeyShow_Changed(object sender, RoutedEventArgs e)
    {
        foreach ((PasswordBox masked, TextBox plain, CheckBox show, _) in ApiKeyFields)
        {
            if (!ReferenceEquals(show, sender))
            {
                continue;
            }

            bool reveal = show.IsChecked == true;
            plain.Visibility = reveal ? Visibility.Visible : Visibility.Collapsed;
            masked.Visibility = reveal ? Visibility.Collapsed : Visibility.Visible;
            return;
        }
    }

    private void OnTranscriptReady(string sourceText, string? translatedText) => _overlay?.SetText(sourceText, translatedText);

    private void OnStatusChanged(string status) => Dispatcher.BeginInvoke(() =>
    {
        StatusText.Text = status;
        _overlay?.SetStatus(status);
    });

    private void DisplaySettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_displaySettingsWindow is null)
        {
            _displaySettingsWindow = new DisplaySettingsWindow(_overlaySettings.Clone()) { Owner = this };
            _displaySettingsWindow.SettingsChanged += OnOverlaySettingsChanged;
            _displaySettingsWindow.Closed += (_, _) => _displaySettingsWindow = null;
            _displaySettingsWindow.Show();
        }
        else
        {
            _displaySettingsWindow.Activate();
        }
    }

    private void OnOverlaySettingsChanged(OverlaySettings settings)
    {
        _overlaySettings = settings;
        _overlay?.ApplySettings(settings);

        try
        {
            _settingsStore.SaveOverlaySettings(settings);
        }
        catch (IOException ex)
        {
            StatusText.Text = Strings.Format("mainwindow.display_settings_save_failed", ex.Message);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Rect bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;

        try
        {
            _settingsStore.SaveMainWindowPosition(new WindowPosition { Left = bounds.Left, Top = bounds.Top });
        }
        catch (IOException)
        {
            // 終了時はエラー表示が間に合わないため、位置保存の失敗だけで終了を妨げない。
        }

        _session?.Dispose();
        _transcriptionProvider?.Dispose();
        (_translationProvider as IDisposable)?.Dispose();
        _overlay?.Close();
        _displaySettingsWindow?.Close();
        base.OnClosed(e);
    }
}
