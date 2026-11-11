using System.Net;
using OmniAudioTranslator.Core.Translation;

namespace OmniAudioTranslator.Core.Tests;

public class GoogleTranslateFreeProviderTests
{
    [Fact]
    public async Task TranslateAsync_ParsesTranslatedTextFromResponse()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            """[[["こんにちは","Hello",null,null,3]],null,"en"]""");
        using var provider = new GoogleTranslateFreeProvider(new HttpClient(handler));

        string result = await provider.TranslateAsync("Hello", "ja");

        Assert.Equal("こんにちは", result);
        Assert.Contains("tl=ja", handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("q=Hello", handler.LastRequest.RequestUri!.Query);
        Assert.Contains("client=gtx", handler.LastRequest.RequestUri!.Query);
    }

    [Fact]
    public async Task TranslateAsync_ConcatenatesMultipleSegments()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            """[[["こんにちは、","Hello, ",null,null,3],["元気ですか？","how are you?",null,null,3]],null,"en"]""");
        using var provider = new GoogleTranslateFreeProvider(new HttpClient(handler));

        string result = await provider.TranslateAsync("Hello, how are you?", "ja");

        Assert.Equal("こんにちは、元気ですか？", result);
    }

    [Fact]
    public async Task TranslateAsync_TooManyRequests_ThrowsWithIsRateLimitedTrue()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, "{}");
        using var provider = new GoogleTranslateFreeProvider(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<TranslationProviderException>(() => provider.TranslateAsync("Hello", "ja"));

        Assert.True(ex.IsRateLimited);
    }

    [Fact]
    public async Task TranslateAsync_ServerError_ThrowsWithIsRateLimitedFalse()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "boom");
        using var provider = new GoogleTranslateFreeProvider(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<TranslationProviderException>(() => provider.TranslateAsync("Hello", "ja"));

        Assert.False(ex.IsRateLimited);
    }
}
