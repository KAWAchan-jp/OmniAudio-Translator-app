using OmniAudioTranslator.Core.Settings;

namespace OmniAudioTranslator.Core.Tests;

public class ApiKeyPersistenceTests
{
    private static string NewConfigPath() =>
        Path.Combine(Path.GetTempPath(), $"omniaudio-apikey-{Guid.NewGuid()}.json");

    [Fact]
    public void LoadApiKey_NoFileYet_ReturnsEmpty()
    {
        var store = new SettingsStore(NewConfigPath());

        Assert.Equal(string.Empty, store.LoadApiKey(TranslationApiKeyKind.DeepL));
        Assert.Equal(string.Empty, store.LoadApiKey(TranslationApiKeyKind.Gemini));
    }

    [Theory]
    [InlineData(TranslationApiKeyKind.DeepL)]
    [InlineData(TranslationApiKeyKind.Gemini)]
    public void SaveThenLoadApiKey_RoundTrips(TranslationApiKeyKind kind)
    {
        string path = NewConfigPath();
        var store = new SettingsStore(path);

        store.SaveApiKey(kind, "test-api-key-12345");

        // 別インスタンスで読み直しても復元できること(=ファイル経由で永続化されている)
        Assert.Equal("test-api-key-12345", new SettingsStore(path).LoadApiKey(kind));
    }

    [Fact]
    public void SaveApiKey_DoesNotAffectOtherProviders()
    {
        string path = NewConfigPath();
        var store = new SettingsStore(path);

        store.SaveApiKey(TranslationApiKeyKind.DeepL, "deepl-key");
        store.SaveApiKey(TranslationApiKeyKind.Gemini, "gemini-key");

        var reloaded = new SettingsStore(path);
        Assert.Equal("deepl-key", reloaded.LoadApiKey(TranslationApiKeyKind.DeepL));
        Assert.Equal("gemini-key", reloaded.LoadApiKey(TranslationApiKeyKind.Gemini));
    }

    [Fact]
    public void SaveApiKey_EmptyClearsStoredValue()
    {
        string path = NewConfigPath();
        var store = new SettingsStore(path);
        store.SaveApiKey(TranslationApiKeyKind.DeepL, "deepl-key");

        store.SaveApiKey(TranslationApiKeyKind.DeepL, string.Empty);

        Assert.Equal(string.Empty, new SettingsStore(path).LoadApiKey(TranslationApiKeyKind.DeepL));
    }

    [Fact]
    public void SavedApiKey_IsNotStoredAsPlainTextOnWindows()
    {
        // Windowsでは設定ファイルに平文で残らないこと(DPAPI保護)を確認する。
        // 非WindowsではDPAPIが使えず保護をスキップする実装のため、この検証はWindowsに限る。
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string path = NewConfigPath();
        new SettingsStore(path).SaveApiKey(TranslationApiKeyKind.Gemini, "super-secret-key");

        string json = File.ReadAllText(path);

        Assert.DoesNotContain("super-secret-key", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Unprotect_PlainTextValue_ReturnsAsIs()
    {
        // 保護導入前に保存された平文の設定ファイルからも読めること(後方互換)。
        Assert.Equal("legacy-plain-key", SecretProtector.Unprotect("legacy-plain-key"));
    }

    [Fact]
    public void Protect_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SecretProtector.Protect(string.Empty));
        Assert.Equal(string.Empty, SecretProtector.Unprotect(string.Empty));
    }
}
