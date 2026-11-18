using OmniAudioTranslator.Core.Capture;

namespace OmniAudioTranslator.Core.Tests;

public class WindowKeywordMatcherTests
{
    [Fact]
    public void Matches_SubstringOfTitle_ReturnsTrue()
    {
        Assert.True(WindowKeywordMatcher.Matches("[brave.exe]: (3) zackrawrr - Twitch - Brave", ["twitch"]));
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        Assert.True(WindowKeywordMatcher.Matches("[Brave.exe]: Twitch", ["BRAVE"]));
        Assert.True(WindowKeywordMatcher.Matches("[Brave.exe]: Twitch", ["twitch"]));
    }

    [Fact]
    public void Matches_ProcessName_ReturnsTrue()
    {
        Assert.True(WindowKeywordMatcher.Matches("[OmniAudioTranslator.App.exe]: OmniAudio Translator", ["omniaudiotranslator"]));
    }

    [Fact]
    public void Matches_NoKeywords_ReturnsFalse()
    {
        Assert.False(WindowKeywordMatcher.Matches("[brave.exe]: Twitch", []));
    }

    [Fact]
    public void Matches_BlankKeyword_IsIgnored()
    {
        Assert.False(WindowKeywordMatcher.Matches("[brave.exe]: Twitch", ["", "   "]));
    }

    [Fact]
    public void Matches_AnyKeyword_ReturnsTrue()
    {
        Assert.True(WindowKeywordMatcher.Matches("[code.exe]: Command Code", ["slack", "command"]));
    }

    [Fact]
    public void Normalize_TrimsDropsEmptyAndDeduplicatesIgnoringCase()
    {
        IReadOnlyList<string> normalized = WindowKeywordMatcher.Normalize([" Twitch ", "", "twitch", "Brave", null, "  "]);

        Assert.Equal(["Twitch", "Brave"], normalized);
    }
}
