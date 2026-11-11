using System.Net;

namespace OmniAudioTranslator.Core.Tests;

/// <summary>翻訳プロバイダのHTTPユニットテスト用。実際にはリクエストを送らず、固定の応答を返す。</summary>
internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody),
        };
    }
}
