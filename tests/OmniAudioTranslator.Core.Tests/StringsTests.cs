using OmniAudioTranslator.Core.Localization;

namespace OmniAudioTranslator.Core.Tests;

public class StringsTests
{
    public StringsTests()
    {
        // Strings.CurrentLanguageは静的状態なので、他テストへ影響しないよう既定値に戻してから始める。
        Strings.CurrentLanguage = AppLanguage.Japanese;
    }

    [Fact]
    public void Get_JapaneseByDefault_ReturnsJapaneseText()
    {
        Assert.Equal("停止しました", Strings.Get("session.stopped"));
    }

    [Fact]
    public void Get_EnglishSelected_ReturnsEnglishText()
    {
        Strings.CurrentLanguage = AppLanguage.English;

        Assert.Equal("Stopped", Strings.Get("session.stopped"));

        Strings.CurrentLanguage = AppLanguage.Japanese;
    }

    [Fact]
    public void Get_UnknownKey_ReturnsKeyItself()
    {
        Assert.Equal("no.such.key", Strings.Get("no.such.key"));
    }

    [Fact]
    public void Format_SubstitutesArguments()
    {
        Strings.CurrentLanguage = AppLanguage.English;

        string result = Strings.Format("session.recognition_error", "boom");

        Assert.Equal("Recognition error: boom", result);

        Strings.CurrentLanguage = AppLanguage.Japanese;
    }

    [Fact]
    public void SetLanguage_SameValue_DoesNotRaiseLanguageChanged()
    {
        Strings.CurrentLanguage = AppLanguage.Japanese;
        bool raised = false;
        Strings.LanguageChanged += OnChanged;

        Strings.CurrentLanguage = AppLanguage.Japanese;

        Strings.LanguageChanged -= OnChanged;
        Assert.False(raised);

        void OnChanged() => raised = true;
    }

    [Fact]
    public void SetLanguage_DifferentValue_RaisesLanguageChanged()
    {
        Strings.CurrentLanguage = AppLanguage.Japanese;
        bool raised = false;
        Strings.LanguageChanged += OnChanged;

        Strings.CurrentLanguage = AppLanguage.English;

        Strings.LanguageChanged -= OnChanged;
        Strings.CurrentLanguage = AppLanguage.Japanese;
        Assert.True(raised);

        void OnChanged() => raised = true;
    }
}
