using System.Text.Json;
using OmniAudioTranslator.Core.Localization;

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
        return config.ChunkMillis;
    }

    public void SaveChunkMillis(int chunkMillis)
    {
        AppConfig config = ReadConfig();
        config.ChunkMillis = chunkMillis;
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
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        WriteConfig(config);
    }

    private static string GetStoredApiKey(AppConfig config, TranslationApiKeyKind kind) => kind switch
    {
        TranslationApiKeyKind.DeepL => config.DeepLApiKey,
        TranslationApiKeyKind.Gemini => config.GeminiApiKey,
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

        public int ChunkMillis { get; set; } = 6000;

        public WindowPosition? MainWindowPosition { get; set; }

        // 言語コードはComboBoxItem.Tagと同じ値を保持し、表示言語の切替による文言変更の影響を受けないようにする。
        public string SourceLanguage { get; set; } = "ja";

        public string TargetLanguage { get; set; } = "en";

        public int TranslationProviderIndex { get; set; }

        public int TranslationProviderSelectionVersion { get; set; }

        // APIキーはSecretProtectorで保護した文字列として保存する(平文では残さない)。
        public string DeepLApiKey { get; set; } = string.Empty;

        public string GeminiApiKey { get; set; } = string.Empty;
    }
}
