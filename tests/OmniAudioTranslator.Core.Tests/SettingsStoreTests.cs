using OmniAudioTranslator.Core.Localization;
using OmniAudioTranslator.Core.Settings;

namespace OmniAudioTranslator.Core.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void LoadOverlaySettings_NoFileYet_ReturnsDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omniaudio-settings-{Guid.NewGuid()}.json");
        var store = new SettingsStore(path);

        OverlaySettings settings = store.LoadOverlaySettings();

        Assert.Equal(OverlaySettings.CreateDefault().StatusFontSize, settings.StatusFontSize);
        Assert.True(settings.ShowTranslatedText);
    }

    [Fact]
    public void LoadLanguage_NoFileYet_ReturnsJapanese()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omniaudio-settings-{Guid.NewGuid()}.json");
        var store = new SettingsStore(path);

        Assert.Equal(AppLanguage.Japanese, store.LoadLanguage());
    }

    [Fact]
    public void SaveThenLoadLanguage_RoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omniaudio-settings-{Guid.NewGuid()}.json");
        try
        {
            var store = new SettingsStore(path);

            store.SaveLanguage(AppLanguage.English);

            Assert.Equal(AppLanguage.English, store.LoadLanguage());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveLanguage_DoesNotClobberPreviouslySavedOverlaySettings()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omniaudio-settings-{Guid.NewGuid()}.json");
        try
        {
            var store = new SettingsStore(path);
            var overlay = new OverlaySettings { StatusFontSize = 42 };
            store.SaveOverlaySettings(overlay);

            store.SaveLanguage(AppLanguage.English);

            Assert.Equal(42, store.LoadOverlaySettings().StatusFontSize);
            Assert.Equal(AppLanguage.English, store.LoadLanguage());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadChunkMillis_NoFileYet_ReturnsSixSeconds()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omniaudio-settings-{Guid.NewGuid()}.json");
        var store = new SettingsStore(path);

        Assert.Equal(6000, store.LoadChunkMillis());
    }

    [Fact]
    public void SaveThenLoadChunkMillis_RoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omniaudio-settings-{Guid.NewGuid()}.json");
        try
        {
            var store = new SettingsStore(path);

            store.SaveChunkMillis(3000);

            Assert.Equal(3000, store.LoadChunkMillis());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadMainWindowPosition_NoFileYet_ReturnsNull()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omniaudio-settings-{Guid.NewGuid()}.json");
        var store = new SettingsStore(path);

        Assert.Null(store.LoadMainWindowPosition());
    }

    [Fact]
    public void SaveThenLoadMainWindowPosition_RoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omniaudio-settings-{Guid.NewGuid()}.json");
        try
        {
            var store = new SettingsStore(path);

            store.SaveMainWindowPosition(new WindowPosition { Left = 123.5, Top = -42.25 });
            WindowPosition? loaded = store.LoadMainWindowPosition();

            Assert.NotNull(loaded);
            Assert.Equal(123.5, loaded.Left);
            Assert.Equal(-42.25, loaded.Top);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveThenLoadTranslationSelections_RoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omniaudio-settings-{Guid.NewGuid()}.json");
        try
        {
            var store = new SettingsStore(path);

            store.SaveTranslationSelections("ko", "de", 4);

            Assert.Equal(("ko", "de", 4), store.LoadTranslationSelections());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omniaudio-settings-{Guid.NewGuid()}.json");
        try
        {
            var store = new SettingsStore(path);
            var settings = new OverlaySettings
            {
                ShowStatus = false,
                StatusFontSize = 11,
                StatusColor = "#FF112233",
                ShowSourceText = false,
                SourceFontSize = 17,
                SourceColor = "#FF445566",
                ShowTranslatedText = true,
                TranslatedFontSize = 30,
                TranslatedColor = "#FF778899",
                BackgroundOpacity = 0.25,
            };

            store.SaveOverlaySettings(settings);
            OverlaySettings loaded = store.LoadOverlaySettings();

            Assert.False(loaded.ShowStatus);
            Assert.Equal(11, loaded.StatusFontSize);
            Assert.Equal("#FF112233", loaded.StatusColor);
            Assert.False(loaded.ShowSourceText);
            Assert.Equal(17, loaded.SourceFontSize);
            Assert.Equal(30, loaded.TranslatedFontSize);
            Assert.Equal(0.25, loaded.BackgroundOpacity);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadOverlaySettings_CorruptedFile_FallsBackToDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"omniaudio-settings-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(path, "{ this is not valid json");
            var store = new SettingsStore(path);

            OverlaySettings settings = store.LoadOverlaySettings();

            Assert.Equal(OverlaySettings.CreateDefault().TranslatedFontSize, settings.TranslatedFontSize);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
