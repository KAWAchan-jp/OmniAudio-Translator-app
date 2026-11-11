using System.Net.Http.Headers;
using System.Text.Json;
using OmniAudioTranslator.Core.Localization;

namespace OmniAudioTranslator.Core.Translation;

/// <summary>DeepL API (v2, REST)による翻訳。APIキー末尾の":fx"で無料/有料エンドポイントを判別する。</summary>
public sealed class DeepLProvider : ITranslationProvider, IDisposable
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public DeepLProvider(string apiKey, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    private string Endpoint => _apiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase)
        ? "https://api-free.deepl.com/v2/translate"
        : "https://api.deepl.com/v2/translate";

    public async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["text"] = text,
                ["target_lang"] = NormalizeTargetLanguage(targetLanguage),
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", _apiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new TranslationProviderException(
                Strings.Format("translation.deepl_error", (int)response.StatusCode, body),
                RateLimitClassifier.IsRateLimitError(response.StatusCode, body));
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("translations")[0].GetProperty("text").GetString() ?? string.Empty;
    }

    /// <summary>DeepLは"EN"単体を受け付けず"EN-US"等の地域付きコードを要求するため、既知の言語だけ補完する。</summary>
    public static string NormalizeTargetLanguage(string targetLanguage) => targetLanguage.ToUpperInvariant() switch
    {
        "EN" => "EN-US",
        "PT" => "PT-PT",
        var code => code,
    };

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
