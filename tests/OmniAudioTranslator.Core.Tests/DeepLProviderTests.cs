using System.Net;
using OmniAudioTranslator.Core.Translation;

namespace OmniAudioTranslator.Core.Tests;

public class DeepLProviderTests
{
    [Fact]
    public async Task TranslateAsync_ParsesTranslatedTextFromResponse()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            """{"translations":[{"detected_source_language":"EN","text":"こんにちは"}]}""");
        using var provider = new DeepLProvider("test-key", new HttpClient(handler));

        string result = await provider.TranslateAsync("Hello", "ja");

        Assert.Equal("こんにちは", result);
        Assert.Equal("DeepL-Auth-Key", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("test-key", handler.LastRequest.Headers.Authorization!.Parameter);
        Assert.Contains("target_lang=JA", handler.LastRequestBody);
    }

    [Fact]
    public async Task TranslateAsync_FreeTierKey_UsesFreeEndpoint()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"translations":[{"text":"ok"}]}""");
        using var provider = new DeepLProvider("test-key:fx", new HttpClient(handler));

        await provider.TranslateAsync("Hello", "ja");

        Assert.Equal("https://api-free.deepl.com/v2/translate", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task TranslateAsync_ProTierKey_UsesProEndpoint()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"translations":[{"text":"ok"}]}""");
        using var provider = new DeepLProvider("test-key", new HttpClient(handler));

        await provider.TranslateAsync("Hello", "ja");

        Assert.Equal("https://api.deepl.com/v2/translate", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task TranslateAsync_TooManyRequests_ThrowsWithIsRateLimitedTrue()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, "{}");
        using var provider = new DeepLProvider("test-key", new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<TranslationProviderException>(() => provider.TranslateAsync("Hello", "ja"));

        Assert.True(ex.IsRateLimited);
    }

    [Theory]
    [InlineData("en", "EN-US")]
    [InlineData("EN", "EN-US")]
    [InlineData("pt", "PT-PT")]
    [InlineData("ja", "JA")]
    [InlineData("de", "DE")]
    public void NormalizeTargetLanguage_AppliesKnownRegionCodes(string input, string expected)
    {
        Assert.Equal(expected, DeepLProvider.NormalizeTargetLanguage(input));
    }
}
