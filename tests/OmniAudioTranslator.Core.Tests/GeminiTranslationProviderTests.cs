using System.Net;
using OmniAudioTranslator.Core.Translation;

namespace OmniAudioTranslator.Core.Tests;

public class GeminiTranslationProviderTests
{
    [Fact]
    public async Task TranslateAsync_ParsesTranslatedTextFromResponse()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            """{"candidates":[{"content":{"parts":[{"text":"こんにちは"}]}}]}""");
        using var provider = new GeminiTranslationProvider("test-key", httpClient: new HttpClient(handler));

        string result = await provider.TranslateAsync("Hello", "ja");

        Assert.Equal("こんにちは", result);
        Assert.Contains("key=test-key", handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("models/gemini-3.5-flash:generateContent", handler.LastRequest.RequestUri!.ToString());
        Assert.Contains("Hello", handler.LastRequestBody);
    }

    [Fact]
    public async Task TranslateAsync_TrimsWhitespaceFromResponse()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            """{"candidates":[{"content":{"parts":[{"text":"  こんにちは\n"}]}}]}""");
        using var provider = new GeminiTranslationProvider("test-key", httpClient: new HttpClient(handler));

        string result = await provider.TranslateAsync("Hello", "ja");

        Assert.Equal("こんにちは", result);
    }

    [Fact]
    public async Task TranslateAsync_CustomModel_IsUsedInEndpoint()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""");
        using var provider = new GeminiTranslationProvider("test-key", "gemini-1.5-pro", new HttpClient(handler));

        await provider.TranslateAsync("Hello", "ja");

        Assert.Contains("models/gemini-1.5-pro:generateContent", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task TranslateAsync_TooManyRequests_ThrowsWithIsRateLimitedTrue()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, "{}");
        using var provider = new GeminiTranslationProvider("test-key", httpClient: new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<TranslationProviderException>(() => provider.TranslateAsync("Hello", "ja"));

        Assert.True(ex.IsRateLimited);
    }

    [Fact]
    public async Task TranslateAsync_ResourceExhaustedBody_IsClassifiedAsRateLimited()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, """{"error":{"status":"RESOURCE_EXHAUSTED"}}""");
        using var provider = new GeminiTranslationProvider("test-key", httpClient: new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<TranslationProviderException>(() => provider.TranslateAsync("Hello", "ja"));

        Assert.True(ex.IsRateLimited);
    }
}
