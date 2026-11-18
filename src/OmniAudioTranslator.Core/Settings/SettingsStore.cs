using System.Text.Json;
using OmniAudioTranslator.Core.Capture;
using OmniAudioTranslator.Core.Localization;
using OmniAudioTranslator.Core.Transcription;

namespace OmniAudioTranslator.Core.Settings;

/// <summary>
/// アプリ設定を<c>%AppData%\OmniAudioTranslator\config.json</c>へ永続化する。
/// オーバーレイ表示・UI言語・各セレクターの選択状態・メインウィンドウ位置を扱う。
/// APIキーはDPAPIで保護して保存する。
/// </summary>
public sealed class SettingsStore
{
    private readonly string _filePath;

    public SettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OmniAudioTranslator",
            "config.json");
    }

    public OverlaySettings LoadOverlaySettings()
    {
        AppConfig config = ReadConfig();
        return config.Overlay ?? OverlaySettings.CreateDefault();
    }

    public void SaveOverlaySettings(OverlaySettings settings)
    {
        AppConfig config = ReadConfig();
        config.Overlay = settings;
        WriteConfig(config);
    }

    public AppLanguage LoadLanguage()
    {
        AppConfig config = ReadConfig();
        return config.Language == "en" ? AppLanguage.English : AppLanguage.Japanese;
    }

    public void SaveLanguage(AppLanguage language)
    {
        AppConfig config = ReadConfig();
        config.Language = language == AppLanguage.English ? "en" : "ja";
        WriteConfig(config);
    }

    public int LoadChunkMillis()
    {
        AppConfig config = ReadConfig();
        return config.ChunkMillis > 0 ? config.ChunkMillis : AudioChunker.DefaultChunkMillis;
    }

    public void SaveChunkMillis(int chunkMillis)
    {
        AppConfig config = ReadConfig();
        config.ChunkMillis = chunkMillis;
        WriteConfig(config);
    }

    /// <summary>
    /// 発話ありと判定するピーク閾値(%)を返す。配信音量が小さい環境では低い値にする必要がある。
    /// 正規化(AudioNormalizer)で増幅する前提のため、既定値は低めの2%。
    /// </summary>
    public int LoadVadThresholdPercent()
    {
        AppConfig config = ReadConfig();
        return config.VadThresholdPercent is >= 1 and <= 100 ? config.VadThresholdPercent : AudioNormalizer.DefaultVadThresholdPercent;
    }

    public void SaveVadThresholdPercent(int thresholdPercent)
    {
        AppConfig config = ReadConfig();
        config.VadThresholdPercent = Math.Clamp(thresholdPercent, 1, 100);
        WriteConfig(config);
    }

    /// <summary>ウィンドウ選択ダイアログで強調表示するキーワードを返す(未設定なら空)。</summary>
    public IReadOnlyList<string> LoadWindowKeywords() =>
        WindowKeywordMatcher.Normalize(ReadConfig().WindowKeywords ?? []);

    /// <summary>強調表示用キーワードを保存する。前後の空白・空要素・重複は取り除く。</summary>
    public void SaveWindowKeywords(IEnumerable<string?> keywords)
    {
        AppConfig config = ReadConfig();
        config.WindowKeywords = WindowKeywordMatcher.Normalize(keywords).ToList();
        WriteConfig(config);
    }

    /// <summary>保存済みのメインウィンドウ位置を返す。初回起動時は<c>null</c>。</summary>
    public WindowPosition? LoadMainWindowPosition() => ReadConfig().MainWindowPosition;

    /// <summary>メインウィンドウの通常表示位置を保存する。</summary>
    public void SaveMainWindowPosition(WindowPosition position)
    {
        AppConfig config = ReadConfig();
        config.MainWindowPosition = position;
        WriteConfig(config);
    }

    /// <summary>元言語・宛先言語・翻訳プロバイダの選択状態を返す。</summary>
    public (string SourceLanguage, string TargetLanguage, int TranslationProviderIndex) LoadTranslationSelections()
    {
        AppConfig config = ReadConfig();
        if (config.TranslationProviderSelectionVersion == 0)
        {
            // 旧構成: 0=なし, 1=Google無料, 2=Google Cloud, 3=DeepL, 4=Gemini。
            // Google Cloud削除後の構成へ読み替え、既存利用者のDeepL/Gemini選択を維持する。
            config.TranslationProviderIndex = config.TranslationProviderIndex switch
            {
                3 => 2,
                4 => 3,
                _ => config.TranslationProviderIndex,
            };
        }
        return (config.SourceLanguage, config.TargetLanguage, config.TranslationProviderIndex);
    }

    /// <summary>元言語・宛先言語・翻訳プロバイダの選択状態をまとめて保存する。</summary>
    public void SaveTranslationSelections(string sourceLanguage, string targetLanguage, int translationProviderIndex)
    {
        AppConfig config = ReadConfig();
        config.SourceLanguage = sourceLanguage;
        config.TargetLanguage = targetLanguage;
        config.TranslationProviderIndex = translationProviderIndex;
        config.TranslationProviderSelectionVersion = 1;
        WriteConfig(config);
    }

    /// <summary>認識エンジン(0=内蔵Whisper, 1=Groq)とGroqモデルの選択状態を返す。</summary>
    public (int ProviderIndex, string GroqModel) LoadTranscriptionSelection()
    {
        AppConfig config = ReadConfig();
        int providerIndex = config.TranscriptionProviderIndex is 0 or 1 ? config.TranscriptionProviderIndex : 0;
        string groqModel = GroqTranscriptionProvider.SupportedModels.Contains(config.GroqModel)
            ? config.GroqModel
            : GroqTranscriptionProvider.DefaultModel;
        return (providerIndex, groqModel);
    }

    /// <summary>認識エンジンとGroqモデルの選択状態を保存する。不正値は既定値へフォールバックする。</summary>
    public void SaveTranscriptionSelection(int providerIndex, string groqModel)
    {
        AppConfig config = ReadConfig();
        config.TranscriptionProviderIndex = providerIndex is 0 or 1 ? providerIndex : 0;
        config.GroqModel = GroqTranscriptionProvider.SupportedModels.Contains(groqModel)
            ? groqModel
            : GroqTranscriptionProvider.DefaultModel;
        WriteConfig(config);
    }

    /// <summary>保存済みAPIキーを復号して返す(未設定なら空文字)。</summary>
    public string LoadApiKey(TranslationApiKeyKind kind)
    {
        AppConfig config = ReadConfig();
        return SecretProtector.Unprotect(GetStoredApiKey(config, kind));
    }

    /// <summary>APIキーを保護した上で保存する(空文字を渡すと保存済みの値を消す)。</summary>
    public void SaveApiKey(TranslationApiKeyKind kind, string apiKey)
    {
        AppConfig config = ReadConfig();
        string stored = SecretProtector.Protect(apiKey);

        switch (kind)
        {
            case TranslationApiKeyKind.DeepL:
                config.DeepLApiKey = stored;
                break;
            case TranslationApiKeyKind.Gemini:
                config.GeminiApiKey = stored;
                break;
            case TranslationApiKeyKind.Groq:
                config.GroqApiKey = stored;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        WriteConfig(config);
    }

    private static string GetStoredApiKey(AppConfig config, TranslationApiKeyKind kind) => kind switch
    {
        TranslationApiKeyKind.DeepL => config.DeepLApiKey,
        TranslationApiKeyKind.Gemini => config.GeminiApiKey,
        TranslationApiKeyKind.Groq => config.GroqApiKey,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private void WriteConfig(AppConfig config)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    private AppConfig ReadConfig()
    {
        if (!File.Exists(_filePath))
        {
            return new AppConfig();
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch (JsonException)
        {
            // 壊れた設定ファイルは初期値扱いにする(アプリを起動不能にしないことを優先)。
            return new AppConfig();
        }
    }

    private sealed class AppConfig
    {
        public OverlaySettings Overlay { get; set; } = OverlaySettings.CreateDefault();

        public string Language { get; set; } = "ja";

        public int ChunkMillis { get; set; } = AudioChunker.DefaultChunkMillis;

        // 発話ありと判定するピーク閾値(%)。小さい音声を拾えるよう低めの既定値にしている。
        public int VadThresholdPercent { get; set; } = AudioNormalizer.DefaultVadThresholdPercent;

        public WindowPosition? MainWindowPosition { get; set; }

        // ウィンドウ選択ダイアログで強調表示するキーワード(大文字小文字を区別しない部分一致)。
        public List<string>? WindowKeywords { get; set; } = new();

        // 言語コードはComboBoxItem.Tagと同じ値を保持し、表示言語の切替による文言変更の影響を受けないようにする。
        public string SourceLanguage { get; set; } = "ja";

        public string TargetLanguage { get; set; } = "en";

        public int TranslationProviderIndex { get; set; }

        public int TranslationProviderSelectionVersion { get; set; }

        // 認識エンジン: 0=内蔵Whisper(既定), 1=Groq(クラウド)。
        public int TranscriptionProviderIndex { get; set; }

        public string GroqModel { get; set; } = GroqTranscriptionProvider.DefaultModel;

        // APIキーはSecretProtectorで保護した文字列として保存する(平文では残さない)。
        public string DeepLApiKey { get; set; } = string.Empty;

        public string GeminiApiKey { get; set; } = string.Empty;

        public string GroqApiKey { get; set; } = string.Empty;
    }
}
