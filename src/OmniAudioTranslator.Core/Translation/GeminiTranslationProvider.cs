using System.Text;
using System.Text.Json;
using OmniAudioTranslator.Core.Localization;

namespace OmniAudioTranslator.Core.Translation;

/// <summary>Gemini API (generateContent, REST)にプロンプトで翻訳を依頼する。</summary>
public sealed class GeminiTranslationProvider : ITranslationProvider, IDisposable
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public GeminiTranslationProvider(string apiKey, string model = "gemini-3.5-flash", HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _model = model;
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken cancellationToken = default)
    {
        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={Uri.EscapeDataString(_apiKey)}";
        string prompt = $"Translate the following text to {targetLanguage}. Output only the translated text, with no explanation, no quotes, and no additional commentary.\n\n{text}";

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new TranslationProviderException(
                Strings.Format("translation.gemini_error", (int)response.StatusCode, body),
                RateLimitClassifier.IsRateLimitError(response.StatusCode, body));
        }

        using var document = JsonDocument.Parse(body);
        string translated = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        return translated.Trim();
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
