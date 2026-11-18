using System.Net;
using System.Text;
using OmniAudioTranslator.Core.Transcription;

namespace OmniAudioTranslator.Core.Tests;

public class GroqTranscriptionProviderTests
{
    private static MemoryStream WavStream() => new(Encoding.ASCII.GetBytes("RIFF....WAVEfmt "));

    [Fact]
    public async Task TranscribeAsync_ParsesTextAndLanguage()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"text":"  こんにちは  ","language":"ja"}""");
        using var provider = new GroqTranscriptionProvider("test-key", httpClient: new HttpClient(handler));

        TranscriptionResult result = await provider.TranscribeAsync(WavStream(), "ja");

        Assert.Equal("こんにちは", result.Text);
        Assert.Equal("ja", result.Language);
        Assert.Empty(result.Segments);
    }

    [Fact]
    public async Task TranscribeAsync_ParsesSegments()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            """{"text":"hello world","language":"en","segments":[{"start":0.0,"end":1.5,"text":"hello"},{"start":1.5,"end":2.5,"text":" world"}]}""");
        using var provider = new GroqTranscriptionProvider("test-key", httpClient: new HttpClient(handler));

        TranscriptionResult result = await provider.TranscribeAsync(WavStream(), "en");

        Assert.Equal("hello world", result.Text);
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(TimeSpan.FromSeconds(1.5), result.Segments[1].Start);
        Assert.Equal(" world", result.Segments[1].Text);
    }

    [Fact]
    public async Task TranscribeAsync_SendsMultipartWithModelAndVerboseJson()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"text":"ok"}""");
        using var provider = new GroqTranscriptionProvider("test-key", httpClient: new HttpClient(handler));

        await provider.TranscribeAsync(WavStream(), "ja");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.groq.com/openai/v1/audio/transcriptions", handler.LastRequest.RequestUri!.ToString());
        Assert.Contains("name=model", handler.LastRequestBody);
        Assert.Contains("whisper-large-v3-turbo", handler.LastRequestBody);
        Assert.Contains("name=response_format", handler.LastRequestBody);
        Assert.Contains("verbose_json", handler.LastRequestBody);
        Assert.Contains("audio.wav", handler.LastRequestBody);
    }

    [Fact]
    public async Task TranscribeAsync_CustomModel_IsUsedInRequest()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"text":"ok"}""");
        using var provider = new GroqTranscriptionProvider("test-key", "whisper-large-v3", new HttpClient(handler));

        await provider.TranscribeAsync(WavStream(), "ja");

        Assert.Contains("whisper-large-v3", handler.LastRequestBody);
    }

    [Fact]
    public async Task TranscribeAsync_SendsBearerToken()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"text":"ok"}""");
        using var provider = new GroqTranscriptionProvider("test-key", httpClient: new HttpClient(handler));

        await provider.TranscribeAsync(WavStream(), "ja");

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("test-key", handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task TranscribeAsync_AutoLanguage_OmitsLanguageField()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"text":"ok"}""");
        using var provider = new GroqTranscriptionProvider("test-key", httpClient: new HttpClient(handler));

        await provider.TranscribeAsync(WavStream(), "auto");

        Assert.DoesNotContain("name=language", handler.LastRequestBody);
    }

    [Fact]
    public async Task TranscribeAsync_ExplicitLanguage_SendsLanguageField()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"text":"ok"}""");
        using var provider = new GroqTranscriptionProvider("test-key", httpClient: new HttpClient(handler));

        await provider.TranscribeAsync(WavStream(), "ja");

        Assert.Contains("name=language", handler.LastRequestBody);
        Assert.Contains("ja", handler.LastRequestBody);
    }

    [Fact]
    public async Task TranscribeAsync_TooManyRequests_ThrowsWithIsRateLimitedTrueAndStatusCode429()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.TooManyRequests,
            """{"error":{"message":"Rate limit reached for requests","type":"rate_limit_exceeded"}}""");
        using var provider = new GroqTranscriptionProvider("test-key", httpClient: new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<TranscriptionProviderException>(() => provider.TranscribeAsync(WavStream(), "ja"));

        Assert.True(ex.IsRateLimited);
        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Contains("Rate limit reached", ex.ResponseBody);
    }

    [Fact]
    public async Task TranscribeAsync_Unauthorized_ThrowsWithStatusCodeAndBody()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.Unauthorized,
            """{"error":{"message":"Invalid API Key","type":"invalid_request_error"}}""");
        using var provider = new GroqTranscriptionProvider("bad-key", httpClient: new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<TranscriptionProviderException>(() => provider.TranscribeAsync(WavStream(), "ja"));

        Assert.False(ex.IsRateLimited);
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains("Invalid API Key", ex.ResponseBody);
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_JsonTextWithoutLanguage_ReturnsNullLanguage()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{"text":"ok"}""");
        using var provider = new GroqTranscriptionProvider("test-key", httpClient: new HttpClient(handler));

        TranscriptionResult result = await provider.TranscribeAsync(WavStream(), "ja");

        Assert.Null(result.Language);
    }

    [Fact]
    public async Task TranscribeAsync_MalformedJson_ThrowsProviderExceptionWithBody()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "not json");
        using var provider = new GroqTranscriptionProvider("test-key", httpClient: new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<TranscriptionProviderException>(() => provider.TranscribeAsync(WavStream(), "ja"));

        Assert.Null(ex.StatusCode);
        Assert.Equal("not json", ex.ResponseBody);
    }
}
